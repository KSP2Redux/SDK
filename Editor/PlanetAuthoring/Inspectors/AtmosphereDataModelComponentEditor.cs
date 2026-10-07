using KSP.Rendering;
using Ksp2UnityTools.Editor.PlanetAuthoring.Atmosphere;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Custom inspector for <see cref="AtmosphereDataModelComponent" /> on a scaled body.
    /// </summary>
    /// <remarks>
    /// Shows the wiring Add Atmosphere wrote, then the model it names inline, so the atmosphere is
    /// authored from the body without opening the model asset. The inline section is rebuilt when the
    /// model key changes.
    /// </remarks>
    [CustomEditor(typeof(AtmosphereDataModelComponent))]
    public class AtmosphereDataModelComponentEditor : UnityEditor.Editor
    {
        private VisualElement _modelSlot;
        private string _boundKey;

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            Ksp2UnityToolsStyles.Apply(root);

            var wiring = new Foldout { text = "Wiring", value = false };
            wiring.AddToClassList("body-inspector-section");
            wiring.tooltip = "Written by Add Atmosphere. Refit writes them again.";
            foreach ((string path, string label) in new[]
                     {
                         ("_atmosphereModelKey", "Model Key"),
                         ("_planetName", "Planet Name"),
                         ("_innerMeshRenderer", "Inner Shell"),
                         ("_outerMeshRenderer", "Outer Shell"),
                     })
            {
                SerializedProperty property = serializedObject.FindProperty(path);
                if (property == null)
                    continue;

                wiring.Add(new PropertyField(property, label));
            }

            root.Add(wiring);

            _modelSlot = new VisualElement();
            root.Add(_modelSlot);
            RebuildModelSlot();

            SerializedProperty keyProperty = serializedObject.FindProperty("_atmosphereModelKey");
            if (keyProperty != null)
            {
                var tracker = new VisualElement();
                tracker.TrackPropertyValue(keyProperty, _ => RebuildModelSlot());
                root.Add(tracker);
            }

            root.Bind(serializedObject);
            return root;
        }

        private void RebuildModelSlot()
        {
            var component = (AtmosphereDataModelComponent)target;
            string key = component != null ? component.AtmosphereModelKey : null;
            if (_modelSlot == null || (key == _boundKey && _modelSlot.childCount > 0))
                return;

            _boundKey = key;
            _modelSlot.Clear();
            AtmosphereModel model = AtmosphereSetup.FindModel(key, out string problem);
            if (model == null)
            {
                _modelSlot.Add(new HelpBox($"No atmosphere model to edit: {problem}.", HelpBoxMessageType.Warning));
                return;
            }

            _modelSlot.Add(AtmosphereModelInspector.Build(model, new SerializedObject(model)));
        }
    }
}
