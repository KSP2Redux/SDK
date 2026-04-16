namespace Ksp2UnityTools.Editor
{
    public static class SDKConfiguration
    {
#if REDUX
        public const string BasePath = "Assets/Modules/KSP2UnityTools";
#else
    public const string BasePath = "Packages/ksp2community.ksp2unitytools";
#endif
    }
}