using UnityEditor;
using UnityEngine.UIElements;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants
{
    /// <summary>
    /// Contract for a custom editor that renders a specific <see cref="ITransformer" /> concrete type. Implementations decorate themselves with <see cref="TransformerEditorAttribute" /> to register against a transformer type.
    /// </summary>
    /// <remarks>
    /// Editors receive both the live boxed transformer instance and its array-element <see cref="SerializedProperty" />. The SerializedProperty is the preferred handle for attribute-aware field rendering (<see cref="Fields.AttributeAwareFieldRow" />) and free <c>[Tooltip]</c> / aligned-label behaviour via <see cref="UnityEditor.UIElements.PropertyField" />. The live instance is provided for editors that need direct manipulation (e.g. dictionaries, JToken values) where SerializedProperty navigation is awkward.
    ///
    /// The returned <see cref="VisualElement" /> is the CONTENT of the transformer row, not the header chrome.
    /// </remarks>
    public interface ITransformerEditor
    {
        /// <summary>
        /// Builds the editor's content for the given transformer.
        /// </summary>
        /// <param name="transformer">The live transformer object. Useful for direct mutation of complex fields.</param>
        /// <param name="transformerProp">The array-element SerializedProperty for the transformer (a <c>[SerializeReference]</c> entry). Useful for attribute-aware field rendering and free Undo / dirty-tracking.</param>
        /// <param name="context">The editing context (part, module, mark-dirty callback).</param>
        VisualElement Build(ITransformer transformer, SerializedProperty transformerProp, TransformerEditorContext context);
    }
}
