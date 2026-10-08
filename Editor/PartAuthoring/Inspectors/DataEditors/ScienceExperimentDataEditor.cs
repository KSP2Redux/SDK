using System.Globalization;
using System.Reflection;
using KSP.Modules;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// Custom editor for <see cref="Data_ScienceExperiment" />. Renders the <c>Experiments</c>
    /// list as collapsible cards via <see cref="CardListSection" />, with an autocomplete on
    /// the experiment ID and a time-to-complete chip in each header.
    /// </summary>
    [DataEditor(typeof(Data_ScienceExperiment))]
    public sealed class ScienceExperimentDataEditor : IDataEditor
    {
        private const BindingFlags FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private const string USS_PATH = "/Assets/Windows/DataEditors.uss";

        private Transform _partRoot;

        /// <inheritdoc />
        public VisualElement Build(SerializedProperty dataProp, PartBehaviourModule module)
        {
            _partRoot = module == null ? null : module.gameObject.transform;

            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Column;

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + USS_PATH);
            if (sheet != null)
            {
                root.styleSheets.Add(sheet);
            }

            var experimentsProp = dataProp.FindPropertyRelative(nameof(Data_ScienceExperiment.Experiments));
            root.Add(CardListSection.Build(experimentsProp, new CardListSection.Config
            {
                Title = "Experiments",
                AddButtonText = "+ Add Experiment",
                IdentityFieldName = nameof(ExperimentConfiguration.ExperimentDefinitionID),
                BuildIdentityField = idProp => new ExperimentNameField(idProp, string.Empty),
                ChipFieldName = nameof(ExperimentConfiguration.TimeToComplete),
                ChipFormatter = p =>
                {
                    var time = p.floatValue;
                    return time > 0f
                        ? $"{time.ToString("F0", CultureInfo.InvariantCulture)}s"
                        : null;
                },
                BuildBody = BuildExperimentBody,
            }));

            root.Add(BuildTopLevelSection(dataProp));

            return root;
        }

        private void BuildExperimentBody(SerializedProperty entry, VisualElement body)
        {
            foreach (var field in typeof(ExperimentConfiguration).GetFields(FIELD_FLAGS))
            {
                if (field.Name == nameof(ExperimentConfiguration.ExperimentDefinitionID))
                {
                    continue;
                }
                if (field.IsDefined(typeof(HideInInspector), inherit: true))
                {
                    continue;
                }
                var prop = entry.FindPropertyRelative(field.Name);
                if (prop == null)
                {
                    continue;
                }
                var row = ReflectionModuleEditor.BuildFieldRowForCustomEditor(prop, field, _partRoot);
                if (row != null)
                {
                    body.Add(row);
                }
            }
        }

        private static VisualElement BuildTopLevelSection(SerializedProperty dataProp)
        {
            var section = new VisualElement();
            section.AddToClassList("data-editor-section");

            var header = new Label("Settings");
            header.AddToClassList("data-editor-section-header");
            section.Add(header);

            AddTopLevelField(section, dataProp, nameof(Data_ScienceExperiment.NotifyOnCompletion));
            AddTopLevelField(section, dataProp, nameof(Data_ScienceExperiment.ResourceThresholdMultiplier));

            return section;
        }

        private static void AddTopLevelField(VisualElement parent, SerializedProperty dataProp, string fieldName)
        {
            var prop = dataProp.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                return;
            }
            var field = new PropertyField(prop);
            field.AddToClassList("unity-base-field__aligned");
            parent.Add(field);
        }
    }
}
