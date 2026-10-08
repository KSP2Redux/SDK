using System.Globalization;
using System.Reflection;
using KSP.Modules;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// Custom editor for <see cref="Data_ResourceConverter" />. Renders the
    /// <c>FormulaDefinitions</c> list as collapsible cards via <see cref="CardListSection" />
    /// with a flux chip per card, and groups the scalar fields into Settings and Emissive sections.
    /// </summary>
    [DataEditor(typeof(Data_ResourceConverter))]
    public sealed class ResourceConverterDataEditor : IDataEditor
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

            var formulasProp = dataProp.FindPropertyRelative(nameof(Data_ResourceConverter.FormulaDefinitions));
            root.Add(CardListSection.Build(formulasProp, new CardListSection.Config
            {
                Title = "Formula Definitions",
                AddButtonText = "+ Add Formula",
                IdentityFieldName = nameof(ResourceConverterFormulaDefinition.InternalName),
                ChipFieldName = nameof(ResourceConverterFormulaDefinition.FluxGenerated),
                ChipFormatter = p =>
                {
                    var flux = p.floatValue;
                    return flux > 0f
                        ? $"{flux.ToString("F1", CultureInfo.InvariantCulture)} kW"
                        : null;
                },
                BuildBody = BuildFormulaBody,
            }));

            root.Add(BuildSettingsSection(dataProp));
            root.Add(BuildEmissiveSection(dataProp));

            return root;
        }

        private void BuildFormulaBody(SerializedProperty entry, VisualElement body)
        {
            foreach (var field in typeof(ResourceConverterFormulaDefinition).GetFields(FIELD_FLAGS))
            {
                if (field.Name == nameof(ResourceConverterFormulaDefinition.InternalName))
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

        private VisualElement BuildSettingsSection(SerializedProperty dataProp)
        {
            var section = new VisualElement();
            section.AddToClassList("data-editor-section");

            var header = new Label("Settings");
            header.AddToClassList("data-editor-section-header");
            section.Add(header);

            AddTopLevelField(section, dataProp, nameof(Data_ResourceConverter.ToggleName));
            AddTopLevelField(section, dataProp, nameof(Data_ResourceConverter.StartActionName));
            AddTopLevelField(section, dataProp, nameof(Data_ResourceConverter.StopActionName));
            AddTopLevelField(section, dataProp, nameof(Data_ResourceConverter.ToggleActionName));
            AddTopLevelField(section, dataProp, nameof(Data_ResourceConverter.ConvertByMass));
            AddTopLevelField(section, dataProp, nameof(Data_ResourceConverter.ResourceAutoShutdown));
            AddTopLevelField(section, dataProp, nameof(Data_ResourceConverter.AutoShutdownTemperatureRatio));

            return section;
        }

        private VisualElement BuildEmissiveSection(SerializedProperty dataProp)
        {
            var section = new VisualElement();
            section.AddToClassList("data-editor-section");

            var header = new Label("Emissive");
            header.AddToClassList("data-editor-section-header");
            section.Add(header);

            AddTopLevelField(section, dataProp, nameof(Data_ResourceConverter.UseEmissive));
            AddTopLevelField(section, dataProp, nameof(Data_ResourceConverter.UseEmissiveTemperature));
            AddTopLevelField(section, dataProp, nameof(Data_ResourceConverter.EmissiveMaterialNames));
            AddTopLevelField(section, dataProp, nameof(Data_ResourceConverter.EmissiveTemperatureCurve));
            AddTopLevelField(section, dataProp, nameof(Data_ResourceConverter.EmissiveLerpRateUp));
            AddTopLevelField(section, dataProp, nameof(Data_ResourceConverter.EmissiveLerpRateDown));

            return section;
        }

        private void AddTopLevelField(VisualElement parent, SerializedProperty dataProp, string fieldName)
        {
            var prop = dataProp.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                return;
            }
            var field = typeof(Data_ResourceConverter).GetField(fieldName, FIELD_FLAGS);
            if (field == null)
            {
                return;
            }
            var row = ReflectionModuleEditor.BuildFieldRowForCustomEditor(prop, field, _partRoot);
            if (row != null)
            {
                parent.Add(row);
            }
        }
    }
}
