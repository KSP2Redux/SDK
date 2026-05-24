using System.Globalization;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants.Editors
{
    /// <summary>
    /// Custom editor for <see cref="EngineModeSwapper" />. Renders <c>Modes</c> using the same per-mode card body as <see cref="EngineDataEditor" /> via <see cref="EngineModeBodyBuilder" />, so authoring an engine-mode swap feels identical to authoring the engine's stock modes.
    /// </summary>
    [TransformerEditor(typeof(EngineModeSwapper))]
    public sealed class EngineModeSwapperEditor : ITransformerEditor
    {
        public VisualElement Build(ITransformer transformer, SerializedProperty transformerProp, TransformerEditorContext context)
        {
            var outer = new VisualElement();

            SerializedProperty nameProp = transformerProp?.FindPropertyRelative("Name");
            SerializedProperty modesProp = transformerProp?.FindPropertyRelative("Modes");

            if (nameProp == null || modesProp == null)
            {
                outer.Add(new HelpBox("EngineModeSwapper fields not found.", HelpBoxMessageType.Error));
                return outer;
            }

            var nameField = new TextField("Name")
            {
                isDelayed = true,
                tooltip = "Identifier for this engine-mode swap. Defaults to the transformer's short name.",
            };
            nameField.AddToClassList("unity-base-field");
            nameField.AddToClassList("unity-base-field__aligned");
            nameField.BindProperty(nameProp);
            outer.Add(nameField);

            Transform partRoot = context?.Module != null ? context.Module.gameObject.transform : null;

            outer.Add(CardListSection.Build(modesProp, new CardListSection.Config
            {
                Title = "Modes",
                AddButtonText = "+ Add Mode",
                IdentityFieldName = "engineID",
                ChipFieldName = "maxThrust",
                ChipFormatter = p => $"{p.floatValue.ToString("F0", CultureInfo.InvariantCulture)} kN",
                BuildBody = (entry, body) => EngineModeBodyBuilder.Build(body, entry, partRoot),
                OnAddSeed = (entry, newIndex) =>
                {
                    var idProp = entry.FindPropertyRelative("engineID");
                    if (idProp != null)
                    {
                        idProp.stringValue = $"Mode{newIndex}";
                    }
                    var displayNameProp = entry.FindPropertyRelative("EngineDisplayName");
                    if (displayNameProp != null)
                    {
                        displayNameProp.stringValue = $"Mode{newIndex}";
                    }
                },
            }));

            return outer;
        }
    }
}
