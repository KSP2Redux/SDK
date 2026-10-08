using KSP;
using KSP.Rendering;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Inspector for <see cref="ScaledCloudDataModelComponent" />: its wiring, and its scaled layers read-only with the
    /// bake.
    /// </summary>
    /// <remarks>
    /// The layer view is rebuilt when the wired scaled configuration changes.
    /// </remarks>
    [CustomEditor(typeof(ScaledCloudDataModelComponent))]
    public class ScaledCloudDataModelComponentEditor : UnityEditor.Editor
    {
        private VisualElement _layersSlot;

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            Ksp2UnityToolsStyles.Apply(root);

            var wiring = new Foldout { text = "Wiring", value = false };
            wiring.AddToClassList("body-inspector-section");
            wiring.tooltip = "Written by Add Clouds, and Refit writes it again. The scaled cloud objects themselves are created by the game at load.";
            SerializedProperty scaledProperty = serializedObject.FindProperty("ScaledCloudConfiguration");
            wiring.Add(new PropertyField(scaledProperty, "Scaled Configuration"));
            root.Add(wiring);

            _layersSlot = new VisualElement();
            root.Add(_layersSlot);
            RebuildLayersSlot();
            _layersSlot.TrackPropertyValue(scaledProperty, _ => RebuildLayersSlot());

            root.Bind(serializedObject);
            return root;
        }

        private void RebuildLayersSlot()
        {
            var component = (ScaledCloudDataModelComponent)target;
            _layersSlot.Clear();
            component.TryGetComponent(out CoreCelestialBodyData body);
            _layersSlot.Add(ScaledCloudsInspector.Build(component.ScaledCloudConfiguration, ScaledCloudsInspector.FindConfiguration(body)));
        }
    }
}
