using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants.Editors
{
    /// <summary>
    /// Custom editor for <see cref="PartScalarTransformer" />. Replaces the default reflection rendering of <c>Key</c> + <c>_valueSerialized</c> with a clean two-row form that names the JSON-string backing field as a regular author-facing "Value (JSON)" input.
    /// </summary>
    /// <remarks>
    /// V1 ships two plain TextFields. A future revision will upgrade Key to a keypath autocomplete over the part's Data_* schema and Value to a typed widget chosen by the resolved field's C# type. The Value cell is bound to the <c>_valueSerialized</c> string because the actual <c>JToken Value</c> field is invisible to Unity's serializer.
    /// </remarks>
    [TransformerEditor(typeof(PartScalarTransformer))]
    public sealed class PartScalarTransformerEditor : ITransformerEditor
    {
        /// <inheritdoc />
        public VisualElement Build(ITransformer transformer, SerializedProperty transformerProp, TransformerEditorContext context)
        {
            var outer = new VisualElement();

            SerializedProperty keyProp = transformerProp?.FindPropertyRelative("Key");
            SerializedProperty valueProp = transformerProp?.FindPropertyRelative("_valueSerialized");

            if (keyProp == null || valueProp == null)
            {
                outer.Add(new HelpBox("PartScalarTransformer fields not found.", HelpBoxMessageType.Error));
                return outer;
            }

            var keyField = new TextField("Key path")
            {
                isDelayed = true,
                tooltip = "Path on the part data identifying the field to set, e.g. maxTemp.",
            };
            keyField.AddToClassList("unity-base-field");
            keyField.AddToClassList("unity-base-field__aligned");
            keyField.BindProperty(keyProp);
            outer.Add(keyField);

            var valueField = new TextField("Value (JSON)")
            {
                isDelayed = true,
                tooltip = "JSON literal to write at the key path when the variant is active. Number (42), string (\"text\"), bool (true), or any JSON expression.",
            };
            valueField.AddToClassList("unity-base-field");
            valueField.AddToClassList("unity-base-field__aligned");
            valueField.BindProperty(valueProp);
            outer.Add(valueField);

            return outer;
        }
    }
}
