using System;
using System.Collections.Generic;
using UnityEngine;

namespace BusJam
{
  public enum SlotUnlockMode
  {
    PerLevel,
    Permanent
  }

  public enum LockedSlotVisibilityMode
  {
    ShowNextPurchasableOnly,
    ShowAll,
    HideAll
  }

  [CreateAssetMenu(fileName = "GameConfig", menuName = "Bus Jam/Game Config")]
  public sealed class GameConfigSO : ScriptableObject
  {
    private static GameConfigSO _current;

    public static GameConfigSO Current
    {
      get
      {
        if (_current == null)
        {
          _current = Resources.Load<GameConfigSO>("GameConfig");
          if (_current == null)
            _current = CreateInstance<GameConfigSO>();
        }
        return _current;
      }
    }

    [Header("Parking Slots & Unlock")]
    public ParkingConfig parking = new ParkingConfig();

    [Header("Economy")]
    public EconomyConfig economy = new EconomyConfig();

    [Header("Vehicles")]
    public List<VehicleKindStat> vehicleStats = new List<VehicleKindStat>
        {
            new VehicleKindStat { kind = VehicleKind.Car, length = 2, capacity = 4 },
            new VehicleKindStat { kind = VehicleKind.Bus, length = 3, capacity = 6 },
        };

    [Header("Color Palette")]
    public List<GameColorEntry> colorPalette = new List<GameColorEntry>
        {
            new GameColorEntry { gameColor = GameColor.Red,    color = new Color(0.93f, 0.27f, 0.27f) },
            new GameColorEntry { gameColor = GameColor.Blue,   color = new Color(0.25f, 0.50f, 0.95f) },
            new GameColorEntry { gameColor = GameColor.Green,  color = new Color(0.30f, 0.75f, 0.35f) },
            new GameColorEntry { gameColor = GameColor.Yellow, color = new Color(1.00f, 0.80f, 0.20f) },
        };

    [Header("Grid Layout & Visuals")]
    public GridVisualConfig gridVisual = new GridVisualConfig();

    [Header("Vehicle Movement & Departure Animation")]
    public VehicleAnimationConfig vehicleAnim = new VehicleAnimationConfig();

    [Header("Flow & UI Timing")]
    public FlowTimingConfig flowTiming = new FlowTimingConfig();

    public static void SetActive(GameConfigSO config)
    {
      if (config == null) return;
      _current = config;
      config.ApplyRuntimeCaches();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitOnLoad()
    {
      Current.ApplyRuntimeCaches();
    }

    private void OnEnable() => ApplyRuntimeCaches();
    private void OnValidate() => ApplyRuntimeCaches();

    public void ApplyRuntimeCaches()
    {
      VehicleExtensions.Configure(vehicleStats);
      GameColorExtensions.Configure(colorPalette);
    }

    [Serializable]
    public sealed class ParkingConfig
    {
      [Min(1)] public int defaultUnlockedSlots = 3;
      [Min(1)] public int maxSlots = 6;
      public SlotUnlockMode unlockMode = SlotUnlockMode.PerLevel;
      public LockedSlotVisibilityMode visibilityMode = LockedSlotVisibilityMode.ShowNextPurchasableOnly;
      public int[] unlockCosts = new int[] { 50, 100, 150 };
      [Header("Slot Strip Layout")]
      public float slotHeightCells = 3.1f;
      public float slotGapCells = -0.35f;
      public float queueGapCells = 0.8f;
      public float queueStepCells = 0.55f;
      public float passengerHeightCells = 0.7f;
      [Header("Road Alignment Offset")]
      public float roadYOffset = -0.15f;
      [Header("Depart Settings")]
      public float departDelay = 0.35f;
      public float departBackCells = 2f;
      public float departRightExtraCells = 3f;
    }

    [Serializable]
    public sealed class EconomyConfig
    {
      [Min(0)] public int startingCoins = 100;
      [Min(0)] public int defaultWinRewardCoins = 50;
      [Min(0)] public int coinFlyBurstCount = 10;
      [Min(0f)] public float postCoinFlyDelay = 0.3f;
    }

    [Serializable]
    public struct VehicleKindStat
    {
      public VehicleKind kind;
      [Min(1)] public int length;
      [Min(1)] public int capacity;
    }

    [Serializable]
    public struct GameColorEntry
    {
      public GameColor gameColor;
      public Color color;
    }

    [Serializable]
    public sealed class GridVisualConfig
    {
      [Min(3)] public int minGridSpanForMaxCell = 8;
      [Range(0.75f, 1f)] public float cellFillRatio = 0.94f;
      [Min(0f)] public float boardPaddingCells = 0.15f;
      public Color evenCellColor = new Color(0.93f, 0.95f, 0.98f, 1f); // #EEF3FA
      public Color oddCellColor = new Color(0.85f, 0.89f, 0.94f, 1f);  // #D9E3F0
      public Color boardBackdropColor = new Color(0.78f, 0.83f, 0.89f, 1f);
      public Color blockedCellColor = new Color(0.40f, 0.45f, 0.53f, 1f);
    }

    [Serializable]
    public sealed class VehicleAnimationConfig
    {
      [Header("Grid Movement")]
      public float moveSpeed = 14f;
      public float bumpCells = 0.15f;
      public float rotateDuration = 0.15f;

      [Header("Launch & Wobble Squash")]
      public Vector2 launchSquash1 = new Vector2(0.92f, 1.07f);
      public float launchDuration1 = 0.07f;
      public Vector2 launchSquash2 = new Vector2(1.15f, 0.90f);
      public float launchDuration2 = 0.10f;
      public float launchRecoverDuration = 0.28f;
      public float wobbleAmount = 0.08f;
      public float wobbleUpDuration = 0.06f;
      public float wobbleBackDuration = 0.18f;

      [Header("Departure Animation")]
      public float departDelay = 0.18f;
      public float readyPopAmount = 0.14f;
      public float readyPause = 0.14f;
      public float reverseSpeedMultiplier = 0.75f;
      public float turnRadiusCells = 0.85f;
      public float turnDuration = 0.18f;
      public float turnBankAngle = -10f;
      public float exitAccelerateDuration = 0.42f;
      public Vector2 departBoostScale = new Vector2(1.16f, 0.88f);
      public bool shakeCameraOnDepart = false;

      [Header("Smoke VFX")]
      public float smokeInterval = 0.07f;
      public float departSmokeInterval = 0.035f;
      public float smokeBackOffsetRatio = 0.4f;
    }

    [Serializable]
    public sealed class FlowTimingConfig
    {
      public float deadlockCheckInterval = 0.3f;
      public float initialDeadlockDelay = 1.0f;
      public float postLevelLoadDeadlockDelay = 1.0f; 
      public float winPopupDelay = 0.85f;
      public float losePopupDelay = 0.35f;
      public float popupOpenDuration = 0.35f;
      public float popupCloseDuration = 0.22f;
      public float popupInitialScale = 0.5f;
    }
  }
}