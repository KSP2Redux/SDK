namespace Ksp2UnityTools.Editor
{
    /// <summary>
    /// Provides source and generated asset paths used by the Redux SDK.
    /// </summary>
    public static class SDKConfiguration
    {
        /// <summary>
        /// Root path of the Redux SDK package, the same in the Redux project and in mod projects.
        /// </summary>
        public const string BasePath = "Packages/ksp2redux.sdk";

#if REDUX
        /// <summary>
        /// Root path of SDK assets that can be used for authoring.
        /// </summary>
        public const string AUTHORING_ASSETS_PATH = BasePath + "/Assets";
#else
        /// <summary>
        /// Root path of SDK assets that can be used for authoring.
        /// </summary>
        public const string AUTHORING_ASSETS_PATH = "Assets/ReduxSDK/Generated/AuthoringAssets";
#endif
    }
}
