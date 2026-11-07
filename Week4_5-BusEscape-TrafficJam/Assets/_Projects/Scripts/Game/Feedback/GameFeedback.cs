using UnityEngine;

namespace BusJam
{
    public static class GameFeedback
    {
        private static CameraFeedback Cam => CameraFeedback.Instance;
        private static AudioManager Audio => AudioManager.Instance;
        private static VfxPool Vfx =>
            PoolManager.Instance != null ? PoolManager.Instance.Vfx : null;

        public static void VehicleClick()
        {
            Audio?.Play(Sfx.Click);
            HapticService.Light();
        }

        public static void VehicleLaunch() => Audio?.Play(Sfx.Engine, 0.8f);

        public static void VehicleBump(Vector3 contactPoint, float cellSize)
        {
            // Cam?.ShakeLight();
            Audio?.Play(Sfx.Thud);
            Audio?.Play(Sfx.Honk, 0.5f);
            Vfx?.Play(VfxType.Spark, contactPoint, cellSize);
            HapticService.Warning();
        }
        
        public static void NoSlot()
        {
            Audio?.Play(Sfx.Thud, 0.8f);
            HapticService.Warning();
        }

        public static void VehicleDepartStart() => Audio?.Play(Sfx.Honk);

        public static void VehicleDepartLaunch()
        {
            Cam?.ShakeMedium();
            Audio?.Play(Sfx.Engine);
            HapticService.Medium();
        }

        public static void Smoke(Vector3 position, float cellSize) =>
            Vfx?.Play(VfxType.Smoke, position, cellSize);

        public static void PassengerBoarded() => Audio?.PlayStep();

        public static void Win(Camera cam)
        {
            Audio?.Play(Sfx.Win);
            Cam?.ShakeWin();
            HapticService.Success();

            if (Vfx == null || cam == null) return;

            float h = cam.orthographicSize;
            float w = h * cam.aspect;
            Vector3 c = cam.transform.position;

            Vfx.Play(
                VfxType.Confetti,
                new Vector3(c.x - w * 0.85f, c.y - h * 0.9f, 0f),
                1f,
                -20f
            );

            Vfx.Play(
                VfxType.Confetti,
                new Vector3(c.x + w * 0.85f, c.y - h * 0.9f, 0f),
                1f,
                20f
            );
        }

        public static void Lose()
        {
            Audio?.Play(Sfx.Lose);
            Cam?.ShakeLight();
            HapticService.Warning();
        }
    }
}
