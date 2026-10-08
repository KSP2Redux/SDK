using KSP.VolumeCloud;
using Ksp2UnityTools.Editor.PlanetAuthoring.Clouds;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Inspector for <see cref="CloudRenderHelper" />: its wiring, and the cloud configuration it loads edited inline.
    /// </summary>
    /// <remarks>
    /// The inline configuration is the one High Quality loads, and is rebuilt when that reference changes.
    /// </remarks>
    [CustomEditor(typeof(CloudRenderHelper))]
    public class CloudRenderHelperEditor : UnityEditor.Editor
    {
        private VisualElement _configurationSlot;

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            Ksp2UnityToolsStyles.Apply(root);

            var wiring = new Foldout { text = "Wiring", value = false };
            wiring.AddToClassList("body-inspector-section");
            wiring.tooltip = "Written by Add Clouds. Refit writes the quality tiers and Lights From Star again.";
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

                wiring.Add(new PropertyField(property, label));
            }

            root.Add(wiring);

            _configurationSlot = new VisualElement();
            root.Add(_configurationSlot);
            RebuildConfigurationSlot();

            SerializedProperty highQuality = serializedObject.FindProperty("HighQualityCloudConfiguration");
            if (highQuality != null)
            {
                _configurationSlot.TrackPropertyValue(highQuality, _ => RebuildConfigurationSlot());
            }

            root.Bind(serializedObject);
            return root;
        }

        private void RebuildConfigurationSlot()
        {
            _configurationSlot.Clear();
            VolumeCloudConfiguration configuration = CloudSetup.FindConfiguration((CloudRenderHelper)target);
            if (configuration == null)
            {
                _configurationSlot.Add(new HelpBox(
                    "No cloud configuration in the project to edit. The helper is empty or points at a stock configuration in the game's bundles.",
                    HelpBoxMessageType.Info
                ));
                return;
            }

            _configurationSlot.Add(VolumeCloudConfigurationInspector.Build(configuration, new SerializedObject(configuration)));
        }
    }
}
