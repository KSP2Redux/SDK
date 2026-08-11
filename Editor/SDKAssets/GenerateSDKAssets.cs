using ThunderKit.Core.Config;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.SDKAssets
{
    /// <summary>
    /// Generates mod-facing SDK assets after ThunderKit imports the game assemblies.
    /// </summary>
    public sealed class GenerateSDKAssets : OptionalExecutor
    {
        private const int IMPORT_PRIORITY = 2_900_000;

        /// <inheritdoc />
        public override int Priority => IMPORT_PRIORITY;

        /// <inheritdoc />
        public override string Description =>
            "Generates SDK authoring assets whose script references target the imported game assemblies.";

        /// <inheritdoc />
        public override bool Execute()
        {
#if REDUX
            return true;
#else
            int generatedAssetCount = SDKAssetCompiler.Compile();
            Debug.Log($"Generated {generatedAssetCount} KSP2UnityTools authoring assets.");
            return true;
#endif
        }

#if !REDUX
        [MenuItem("Tools/KSP2 Unity Tools/SDK Assets/Regenerate Authoring Assets")]
        private static void RegenerateAuthoringAssets()
        {
            int generatedAssetCount = SDKAssetCompiler.Compile();
            Debug.Log($"Generated {generatedAssetCount} KSP2UnityTools authoring assets.");
        }
#endif
    }
}
