using KSP.Sim.Definitions;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// Registers <see cref="PartModuleResourceTable" /> as the canonical renderer for any
    /// array-typed field whose element type is <see cref="PartModuleResourceSetting" />.
    /// </summary>
    [FieldRenderer(typeof(PartModuleResourceSetting), FieldRendererKind.ArrayElement)]
    internal sealed class PartModuleResourceListRenderer : IFieldRenderer
    {
        /// <inheritdoc />
        public VisualElement Build(SerializedProperty arrayProp, string title)
        {
            return PartModuleResourceTable.Build(arrayProp, title);
        }
    }
}
