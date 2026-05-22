using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors
{
    /// <summary>
    /// Contract for a custom renderer of a single field whose type (or, for arrays, whose element
    /// type) has a canonical author-facing layout. Implementations register against a type plus a
    /// <see cref="FieldRendererKind" /> via <see cref="FieldRendererAttribute" /> and are
    /// dispatched from <see cref="ReflectionModuleEditor" />.
    /// </summary>
    public interface IFieldRenderer
    {
        /// <summary>
        /// Builds the renderer's content for the given SerializedProperty.
        /// </summary>
        /// <param name="prop">The SerializedProperty. For <see cref="FieldRendererKind.ArrayElement" />
        /// renderers this is the array property. For <see cref="FieldRendererKind.Direct" /> renderers
        /// this is the single-typed field property.</param>
        /// <param name="title">Author-facing title for the section (usually the field's display name).</param>
        VisualElement Build(SerializedProperty prop, string title);
    }
}
