using System;
using UnityEditor.PackageManager;

namespace Ksp2UnityTools.Editor
{
    /// <summary>
    /// Provides source and generated asset paths used by the Redux SDK.
    /// </summary>
    public static class SDKConfiguration
    {
        /// <summary>
        /// Unity asset path of the Redux SDK package in both the Redux project and mod projects.
        /// </summary>
        public const string BasePath = "Packages/ksp2redux.sdk";

        // Registry and local packages can live outside the project's Packages directory.
        internal static string PackageRoot =>
            PackageInfo.FindForAssetPath($"{BasePath}/package.json")?.resolvedPath
            ?? throw new InvalidOperationException($"The Redux SDK package was not resolved at '{BasePath}'.");

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
