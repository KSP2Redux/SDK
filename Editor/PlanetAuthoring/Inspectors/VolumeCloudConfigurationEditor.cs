using KSP.VolumeCloud;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Inspector for <see cref="VolumeCloudConfiguration" /> assets.
    /// </summary>
    [CustomEditor(typeof(VolumeCloudConfiguration))]
    public class VolumeCloudConfigurationEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI() =>
            VolumeCloudConfigurationInspector.Build((VolumeCloudConfiguration)target, serializedObject);
    }
}
