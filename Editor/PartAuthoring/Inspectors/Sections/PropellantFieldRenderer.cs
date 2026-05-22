using KSP.Modules;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// Registers <see cref="PropellantBlock" /> as the canonical renderer for any field whose
    /// declared type is <see cref="PropellantDefinition" />.
    /// </summary>
    [FieldRenderer(typeof(PropellantDefinition))]
    internal sealed class PropellantFieldRenderer : IFieldRenderer
    {
        /// <inheritdoc />
        public VisualElement Build(SerializedProperty prop, string title)
        {
            return PropellantBlock.Build(prop, title);
        }
    }
}
