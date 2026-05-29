using System.Reflection;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Widgets;
using Redux.Audio;
using Redux.Modules;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// Custom editor for <see cref="Data_PartAudioPreset" />. Renders the <c>Presets</c> list as
    /// collapsible cards via <see cref="CardListSection" /> with an autocomplete on each
    /// binding's preset ID, sourced from <see cref="PartAudioPresetRegistry.GetAuthoringPresetIds" />.
    /// </summary>
    [DataEditor(typeof(Data_PartAudioPreset))]
    public sealed class PartAudioPresetDataEditor : IDataEditor
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

            var presetsProp = dataProp.FindPropertyRelative(nameof(Data_PartAudioPreset.Presets));
            root.Add(CardListSection.Build(presetsProp, new CardListSection.Config
            {
                Title = "Audio Presets",
                AddButtonText = "+ Add Preset",
                IdentityFieldName = nameof(AudioPresetBinding.PresetId),
                BuildIdentityField = idProp => new AutocompleteField(idProp, string.Empty, PartAudioPresetRegistry.GetAuthoringPresetIds),
                BuildBody = BuildBindingBody,
            }));

            return root;
        }

        private void BuildBindingBody(SerializedProperty entry, VisualElement body)
        {
            foreach (var field in typeof(AudioPresetBinding).GetFields(FIELD_FLAGS))
            {
                if (field.Name == nameof(AudioPresetBinding.PresetId))
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
    }
}
