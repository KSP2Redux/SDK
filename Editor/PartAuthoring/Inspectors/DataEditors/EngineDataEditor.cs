using System;
using System.Globalization;
using KSP.Modules;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections;
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
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Inspectors/DataEditors/DataEditors.uss";

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
                OnAddSeed = (entry, newIndex) =>
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
                section.Add(BuildEmissiveMaterialNamesBlock(emissiveNamesProp));
            }

            AddTopLevelField(section, dataProp, "EmissiveTemperatureCurve");
            AddTopLevelField(section, dataProp, "EmissiveLerpRateUp");
            AddTopLevelField(section, dataProp, "EmissiveLerpRateDown");
            AddTopLevelField(section, dataProp, "DeployedModeAnimationStateShortName");

            return section;
        }

        private VisualElement BuildEmissiveMaterialNamesBlock(SerializedProperty arrayProp)
        {
            return InlineListBlock.Build(
                arrayProp,
                titleFormat: "Emissive Material Names ({0})",
                addButtonText: "+ Add",
                emptyHint: "(none)",
                rowBuilder: BuildEmissiveMaterialNameRow);
        }

        private VisualElement BuildEmissiveMaterialNameRow(SerializedProperty entry, int index, Action onDelete)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2f;

            var textField = new TextField { value = entry.stringValue, isDelayed = true };
            textField.style.flexGrow = 1f;
            textField.style.marginRight = 4f;
            textField.RegisterValueChangedCallback(evt =>
            {
                entry.serializedObject.Update();
                entry.stringValue = evt.newValue ?? string.Empty;
                entry.serializedObject.ApplyModifiedProperties();
            });
            row.Add(textField);

            var removeBtn = new Button(onDelete) { text = "X" };
            removeBtn.AddToClassList("data-editor-card-remove-btn");
            row.Add(removeBtn);

            return row;
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
