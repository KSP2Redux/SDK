namespace Ksp2UnityTools.Editor
{
    /// <summary>
    /// Provides source and generated asset paths used by the Redux SDK.
    /// </summary>
    public static class SDKConfiguration
    {
#if REDUX
        /// <summary>
        /// Root path of the in-project Redux SDK source.
        /// </summary>
        public const string BasePath = "Assets/Modules/KSP2UnityTools";

        /// <summary>
        /// Root path of SDK assets that can be used for authoring.
        /// </summary>
        public const string AUTHORING_ASSETS_PATH = BasePath + "/Assets";
#else
        /// <summary>
        /// Root path of the installed Redux SDK package.
        /// </summary>
        public const string BasePath = "Packages/ksp2community.ksp2unitytools";

        /// <summary>
        /// Root path of SDK assets that can be used for authoring.
        /// </summary>
        public const string AUTHORING_ASSETS_PATH = "Assets/ReduxSDK/Generated/AuthoringAssets";
#endif
    }
}
