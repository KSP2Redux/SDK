using KSP.VolumeCloud;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Inspector for <see cref="ScaledCloudConfiguration" /> assets, which shows their derived layers read-only.
    /// </summary>
    [CustomEditor(typeof(ScaledCloudConfiguration))]
    public class ScaledCloudConfigurationEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var scaled = (ScaledCloudConfiguration)target;
            return ScaledCloudsInspector.Build(scaled, FindMirroredConfiguration(scaled));
        }

        // The volumetric configuration whose sidecar names this scaled configuration.
        private static VolumeCloudConfiguration FindMirroredConfiguration(ScaledCloudConfiguration scaled)
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(VolumeCloudConfigurationAuthoring).FullName}"))
            {
                var sidecar = AssetDatabase.LoadAssetAtPath<VolumeCloudConfigurationAuthoring>(AssetDatabase.GUIDToAssetPath(guid));
                if (sidecar != null && sidecar.ScaledConfiguration == scaled)
                    return AuthoringSidecars.GetRuntimeAsset<VolumeCloudConfiguration>(sidecar);
            }

            return null;
        }
    }
}
