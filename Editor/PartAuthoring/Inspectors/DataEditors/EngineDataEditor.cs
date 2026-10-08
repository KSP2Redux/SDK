using System.Globalization;
using KSP.Modules;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// Custom editor for <see cref="Data_Engine" />. Renders <c>engineModes[]</c> as collapsible
    /// mode cards (per <see cref="EngineModeBodyBuilder" />) plus a top-level "Emissive & Animation"
    /// section for the non-mode Data_Engine fields.
    /// </summary>
    [DataEditor(typeof(Data_Engine))]
    public sealed class EngineDataEditor : IDataEditor
    {
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

            var modesProp = dataProp.FindPropertyRelative("engineModes");
            root.Add(CardListSection.Build(modesProp, new CardListSection.Config
            {
                Title = "Engine Modes",
                AddButtonText = "+ Add Mode",
                IdentityFieldName = "engineID",
                ChipFieldName = "maxThrust",
                ChipFormatter = p => $"{p.floatValue.ToString("F0", CultureInfo.InvariantCulture)} kN",
                BuildBody = (entry, body) => EngineModeBodyBuilder.Build(body, entry, _partRoot),
                ApplyDefaultsToNew = (entry, newIndex) =>
                {
                    var idProp = entry.FindPropertyRelative("engineID");
                    if (idProp != null)
                    {
                        idProp.stringValue = $"Mode{newIndex}";
                    }
                    var nameProp = entry.FindPropertyRelative("EngineDisplayName");
                    if (nameProp != null)
                    {
                        nameProp.stringValue = $"Mode{newIndex}";
                    }
                },
            }));
            root.Add(BuildTopLevelSection(dataProp));

            return root;
        }

        private VisualElement BuildTopLevelSection(SerializedProperty dataProp)
        {
            var section = new VisualElement();
            section.AddToClassList("data-editor-section");

            var header = new Label("Emissive & Animation");
            header.AddToClassList("data-editor-section-header");
            section.Add(header);

            AddTopLevelField(section, dataProp, "UseEmissive");

            var emissiveNamesProp = dataProp.FindPropertyRelative("EmissiveMaterialNames");
            if (emissiveNamesProp != null)
            {
                section.Add(InlineStringListBlock.Build(emissiveNamesProp, "Emissive Material Names"));
            }

            AddTopLevelField(section, dataProp, "EmissiveTemperatureCurve");
            AddTopLevelField(section, dataProp, "EmissiveLerpRateUp");
            AddTopLevelField(section, dataProp, "EmissiveLerpRateDown");
            AddTopLevelField(section, dataProp, "DeployedModeAnimationStateShortName");

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
