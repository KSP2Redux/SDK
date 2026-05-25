using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// Builds the shared author-facing table for any <c>List&lt;PartModuleResourceSetting&gt;</c>
    /// field (required resources, resource consumption, etc.) using the same
    /// <see cref="SerializedArrayTable" /> chrome as the Core tab's Storage and Build Costs tables.
    /// </summary>
    /// <remarks>
    /// One column per field on <c>PartModuleResourceSetting</c>: a <see cref="ResourceNameField" />
    /// for <c>ResourceName</c> (autocomplete dropdown over the project's resource catalog),
    /// a float cell for <c>Rate</c>, and a double cell for <c>AcceptanceThreshold</c>.
    /// </remarks>
    internal static class PartModuleResourceTable
    {
        /// <summary>
        /// Builds the table for the given array SerializedProperty.
        /// </summary>
        /// <param name="arrayProp">A SerializedProperty pointing at a <c>List&lt;PartModuleResourceSetting&gt;</c>.</param>
        /// <param name="title">Title shown above the table. Pass null or empty to omit.</param>
        /// <returns>The built table element.</returns>
        public static VisualElement Build(SerializedProperty arrayProp, string title)
        {
            var table = new SerializedArrayTable(
                arrayProp.serializedObject,
                arrayProp.propertyPath,
                title,
                "+ Add Resource",
                new[]
                {
                    new SerializedTableColumn
                    {
                        HeaderLabel = "Resource",
                        PropertyName = "ResourceName",
                        Kind = SerializedTableColumnKind.Custom,
                        CustomBuilder = prop => new ResourceNameField(prop, string.Empty),
                        Flex = 1f,
                    },
                    new SerializedTableColumn
                    {
                        HeaderLabel = "Rate",
                        PropertyName = "Rate",
                        Kind = SerializedTableColumnKind.Float,
                        FixedWidth = 80f,
                        Tooltip = "Units per second consumed (or produced) by the module.",
                    },
                    new SerializedTableColumn
                    {
                        HeaderLabel = "Threshold",
                        PropertyName = "AcceptanceThreshold",
                        Kind = SerializedTableColumnKind.Double,
                        FixedWidth = 80f,
                        Tooltip = "Minimum supply ratio (0..1) the module accepts. Below this it is considered starved.",
                    },
                });
            return table.Build();
        }
    }
}
