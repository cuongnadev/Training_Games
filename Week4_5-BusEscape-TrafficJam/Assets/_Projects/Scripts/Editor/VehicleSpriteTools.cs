using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BusJam.Editor
{
    public class VehicleSpriteImportProcesser : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(VehicleSpriteTools.VehiclesRoot, StringComparison.OrdinalIgnoreCase)) return;

            VehicleSpriteTools.ApplyImportSettings((TextureImporter)assetImporter);
        }
    }

    public static class VehicleSpriteTools
    {
        public const string VehiclesRoot = "Assets/_Projects/Art/Sprites/Vehicles";
        public const string LibraryPath = "Assets/_Projects/Data/VehicleSpriteLibrary.asset";
        private const float PixelsPerUnit = 100f;

        public static void ApplyImportSettings(TextureImporter imp)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = PixelsPerUnit;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Compressed;

            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType = SpriteMeshType.Tight;
            imp.SetTextureSettings(settings);
        }

        [MenuItem("Tools/Bus Jam/Vehicles/Reimport Vehicle Sprites")]
        public static void Reimport()
        {
            AssetDatabase.ImportAsset(
                VehiclesRoot,
                ImportAssetOptions.ImportRecursive |
                ImportAssetOptions.ForceUpdate
            );
        }

        [MenuItem("Tools/Bus Jam/Vehicles/Rebuild Sprite Library")]
        public static void RebuildLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<VehicleSpriteLibrary>(LibraryPath);
            if (library == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath)!);
                library = ScriptableObject.CreateInstance<VehicleSpriteLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var entries = new List<VehicleSpriteLibrary.Entry>();
            var seen = new HashSet<string>();
            int skipped = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { VehiclesRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!TryParse(path, out var entry))
                {
                    Debug.LogWarning(
                        $"[VehicleSpriteTools] Không đọc được tên: {path}");

                    skipped++;
                    continue;
                }

                entry.spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (entry.spr == null) { skipped++; continue; }

                string id =
                    $"{entry.kind}/{entry.skin}/{entry.color}/{entry.dir}";

                if (!seen.Add(id))
                    Debug.LogWarning($"[VehicleSpriteTools] Trùng khoá {id}: {path}");

                entries.Add(entry);
            }

            library.SetEntries(entries);
            AssetDatabase.SaveAssets();
            Selection.activeObject = library;
            Debug.Log($"[VehicleSpriteTools] Đã điền {entries.Count} sprite, bỏ qua {skipped}.");

            ReportMissing(entries);
        }

        private static bool TryParse(string path, out VehicleSpriteLibrary.Entry entry)
        {
            entry = default;

            string folder = Path.GetFileName(Path.GetDirectoryName(path) ?? "");

            var folderTokens = folder.Split('_');

            if (folderTokens.Length < 2) return false;

            if (!TryEnum(folderTokens[0], out VehicleKind kind)) return false;
            if (!TryEnum(folderTokens[1], out VehicleSkin skin)) return false;

            bool hasColor = false, hasDir = false;
            GameColor color = default;
            Direction dir = default;

            foreach (var tok in Path.GetFileNameWithoutExtension(path).Split('_', ' ', '-'))
            {
                if (!hasColor && TryEnum(tok, out color))
                    hasColor = true;
                else if (!hasDir && TryEnum(tok, out dir))
                    hasDir = true;
            }

            if (!hasColor || !hasDir) return false;

            entry = new VehicleSpriteLibrary.Entry
            {
                kind = kind,
                skin = skin,
                color = color,
                dir = dir
            };
            return true;
        }

        private static bool TryEnum<T>(string token, out T value) where T : struct, Enum
        {
            foreach (var name in Enum.GetNames(typeof(T)))
            {
                if (string.Equals(name, token, StringComparison.OrdinalIgnoreCase))
                {
                    value = (T)Enum.Parse(typeof(T), name);
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static void ReportMissing(List<VehicleSpriteLibrary.Entry> entries)
        {
            var have = new HashSet<string>();
            foreach (var e in entries) have.Add($"{e.kind}/{e.skin}/{e.color}/{e.dir}");

            foreach (VehicleKind k in Enum.GetValues(typeof(VehicleKind)))
                foreach (VehicleSkin s in Enum.GetValues(typeof(VehicleSkin)))
                    foreach (GameColor c in Enum.GetValues(typeof(GameColor)))
                        foreach (Direction d in Enum.GetValues(typeof(Direction)))
                        {
                            string id = $"{k}/{s}/{c}/{d}";
                            if (!have.Contains(id)) Debug.LogWarning($"[VehicleSpriteTools] Thiếu: {id}");
                        }
        }
    }
}
