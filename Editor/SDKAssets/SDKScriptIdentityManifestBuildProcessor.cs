#if REDUX
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Ksp2UnityTools.Editor.SDKAssets
{
    internal sealed class SDKScriptIdentityManifestBuildProcessor : IPreprocessBuildWithReport
    {
        /// <inheritdoc />
        public int callbackOrder => int.MinValue;

        /// <inheritdoc />
        public void OnPreprocessBuild(BuildReport report)
        {
            SDKScriptIdentityManifestGenerator.UpdateManifest();
        }
    }
}
#endif
