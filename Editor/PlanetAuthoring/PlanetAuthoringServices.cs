using Ksp2UnityTools.Editor.PlanetAuthoring.Services;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    public static class PlanetAuthoringServices
    {
        private static IGraphicsSettingsProvider _graphicsSettings;
        private static IPhysicsInterestProvider _physicsInterest;
        private static ISurfaceHeightProvider _surfaceHeight;

        public static IGraphicsSettingsProvider GraphicsSettings
        {
            get => _graphicsSettings ??= new DefaultGraphicsSettingsProvider();
            set => _graphicsSettings = value;
        }

        public static IPhysicsInterestProvider PhysicsInterest
        {
            get => _physicsInterest ??= new DefaultPhysicsInterestProvider();
            set => _physicsInterest = value;
        }

        public static ISurfaceHeightProvider SurfaceHeight
        {
            get => _surfaceHeight ??= new DefaultSurfaceHeightProvider();
            set => _surfaceHeight = value;
        }

        public static void ResetToDefaults()
        {
            _graphicsSettings = null;
            _physicsInterest = null;
            _surfaceHeight = null;
        }
    }
}
