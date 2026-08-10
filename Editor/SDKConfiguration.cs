namespace Ksp2UnityTools.Editor
{
    /// <summary>
    /// Provides source and generated asset paths used by KSP2UnityTools.
    /// </summary>
    public static class SDKConfiguration
    {
#if REDUX
        /// <summary>
        /// Root path of the in-project KSP2UnityTools source.
        /// </summary>
        public const string BasePath = "Assets/Modules/KSP2UnityTools";

        /// <summary>
        /// Root path of SDK assets that can be used for authoring.
        /// </summary>
        public const string AUTHORING_ASSETS_PATH = BasePath + "/Assets";
#else
        /// <summary>
        /// Root path of the installed KSP2UnityTools package.
        /// </summary>
        public const string BasePath = "Packages/ksp2community.ksp2unitytools";

        /// <summary>
        /// Root path of SDK assets that can be used for authoring.
        /// </summary>
        public const string AUTHORING_ASSETS_PATH = "Assets/KSP2UnityTools/GeneratedSDKAssets";
#endif
    }
}