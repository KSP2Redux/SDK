using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using UnityEditor;
using UnityEngine.UIElements;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants.Editors
{
    /// <summary>
    /// Custom editor for <see cref="ResourceContainerAdder" />. Renders the <c>Containers</c> list as a grid table with four columns: resource name (autocomplete), capacity, initial fill, and a non-stageable toggle.
    /// </summary>
    /// <remarks>
    /// The Name column uses <see cref="ResourceNameField" /> so authors get the same addressables-backed autocomplete the core Resources table offers. Capacity and Initial are dimensionless doubles. Cross-field validation (initial &lt;= capacity) is a separate validator concern.
    /// </remarks>
    [TransformerEditor(typeof(ResourceContainerAdder))]
    public sealed class ResourceContainerAdderEditor : ITransformerEditor
    {
        public VisualElement Build(ITransformer transformer, SerializedProperty transformerProp, TransformerEditorContext context)
        {
            var outer = new VisualElement();

            SerializedProperty containersProp = transformerProp?.FindPropertyRelative("Containers");
            if (containersProp == null)
            {
                outer.Add(new HelpBox("Containers array not found on this transformer.", HelpBoxMessageType.Error));
                return outer;
            }

            var table = new SerializedArrayTable(
                containersProp,
                title: null,
                addButtonText: "+ Add",
                columns: new[]
                {
                    new SerializedTableColumn
                    {
                        HeaderLabel = "Name",
                        PropertyName = "name",
                        Kind = SerializedTableColumnKind.Custom,
                        Flex = 2f,
                        CustomBuilder = prop => new ResourceNameField(prop, label: null),
                    },
                    new SerializedTableColumn
                    {
                        HeaderLabel = "Capacity",
                        PropertyName = "capacityUnits",
                        Kind = SerializedTableColumnKind.Double,
                        Flex = 1f,
                    },
                    new SerializedTableColumn
                    {
                        HeaderLabel = "Initial",
                        PropertyName = "initialUnits",
                        Kind = SerializedTableColumnKind.Double,
                        Flex = 1f,
                    },
                    new SerializedTableColumn
                    {
                        HeaderLabel = "Non-stageable",
                        PropertyName = "NonStageable",
                        Kind = SerializedTableColumnKind.Toggle,
                        Flex = 0f,
                        FixedWidth = 110f,
                    },
                });
            outer.Add(table.Build());
            return outer;
        }
    }
}
