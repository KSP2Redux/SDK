using KSP;
using KSP.Rendering;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Inspector for <see cref="ScaledCloudDataModelComponent" />: its wiring and scaled layers read-only, with the bake.
    /// </summary>
    [CustomEditor(typeof(ScaledCloudDataModelComponent))]
    public class ScaledCloudDataModelComponentEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var component = (ScaledCloudDataModelComponent)target;
            var root = new VisualElement();
            Ksp2UnityToolsStyles.Apply(root);

            var wiring = new Foldout { text = "Wiring", value = false };
            wiring.AddToClassList("body-inspector-section");
            wiring.tooltip = "Written by Add Clouds. The scaled cloud objects themselves are created by the game at load.";
            var field = new PropertyField(serializedObject.FindProperty("ScaledCloudConfiguration"), "Scaled Configuration");
            field.SetEnabled(false);
            wiring.Add(field);
            root.Add(wiring);
            root.Bind(serializedObject);

            component.TryGetComponent(out CoreCelestialBodyData body);
            root.Add(ScaledCloudsInspector.Build(component.ScaledCloudConfiguration, ScaledCloudsInspector.FindConfiguration(body)));
            return root;
        }
    }
}
