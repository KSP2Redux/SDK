using KSP.VolumeCloud;
using Ksp2UnityTools.Editor.PlanetAuthoring.Clouds;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Inspector for <see cref="CloudRenderHelper" />: its wiring read-only, and the cloud configuration it loads edited
    /// inline.
    /// </summary>
    [CustomEditor(typeof(CloudRenderHelper))]
    public class CloudRenderHelperEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            Ksp2UnityToolsStyles.Apply(root);

            var wiring = new Foldout { text = "Wiring", value = false };
            wiring.AddToClassList("body-inspector-section");
            wiring.tooltip = "Written by Add Clouds. Refit or remove and add the clouds again rather than editing these.";
            foreach ((string path, string label) in new[]
                     {
                         ("HighQualityCloudConfiguration", "High Quality"),
                         ("MediumQualityCloudConfiguration", "Medium Quality"),
                         ("LowQualityCloudConfiguration", "Low Quality"),
                         ("AutoGetLight", "Lights From Star"),
                         ("UseSGTLight", "SGT Lights"),
                         ("MainLight", "Main Light"),
                         ("SecondLight", "Second Light"),
                     })
            {
                SerializedProperty property = serializedObject.FindProperty(path);
                if (property == null)
                    continue;

                var field = new PropertyField(property, label);
                field.SetEnabled(false);
                wiring.Add(field);
            }

            root.Add(wiring);
            root.Bind(serializedObject);

            VolumeCloudConfiguration configuration = CloudSetup.FindConfiguration((CloudRenderHelper)target);
            if (configuration == null)
            {
                root.Add(new HelpBox(
                    "No cloud configuration in the project to edit. The helper is empty or points at a stock configuration in the game's bundles.",
                    HelpBoxMessageType.Info
                ));
                return root;
            }

            root.Add(VolumeCloudConfigurationInspector.Build(configuration, new SerializedObject(configuration)));
            return root;
        }
    }
}
