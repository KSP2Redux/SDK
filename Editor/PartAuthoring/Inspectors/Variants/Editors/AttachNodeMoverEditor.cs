using KSP;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using Ksp2UnityTools.Editor.PartAuthoring.SceneTools;
using UnityEditor;
using UnityEngine.UIElements;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants.Editors
{
    /// <summary>
    /// Custom editor for <see cref="AttachNodeMover" />. Renders the <c>MovedNodes</c> dictionary as a grid table with two columns: the node ID (dropdown of the part's existing nodes) and the new local position (Vector3d-aware <see cref="VectorHandleField" /> with a draggable scene-view handle).
    /// </summary>
    /// <remarks>
    /// <see cref="SceneHandlePicker.IsVectorProperty" /> recognises Vector3d as a Generic struct with x/y/z children, so VectorHandleField binds directly without a dedicated Vector3d widget. Node-ID choices come from <see cref="CorePartData.Data" />'s attach-node list at Build time.
    /// </remarks>
    [TransformerEditor(typeof(AttachNodeMover))]
    public sealed class AttachNodeMoverEditor : ITransformerEditor
    {
        public VisualElement Build(ITransformer transformer, SerializedProperty transformerProp, TransformerEditorContext context)
        {
            var outer = new VisualElement();

            SerializedProperty movedProp = transformerProp?.FindPropertyRelative("MovedNodes");
            if (movedProp == null)
            {
                outer.Add(new HelpBox("MovedNodes not found on this transformer.", HelpBoxMessageType.Error));
                return outer;
            }
            SerializedProperty entriesProp = movedProp.FindPropertyRelative("_entries");
            if (entriesProp == null)
            {
                outer.Add(new HelpBox("MovedNodes backing _entries not found.", HelpBoxMessageType.Error));
                return outer;
            }

            CorePartData part = context?.Part;
            UnityEngine.Component target = context?.Module;

            var table = new SerializedArrayTable(
                entriesProp,
                title: null,
                addButtonText: "+ Add",
                columns: new[]
                {
                    new SerializedTableColumn
                    {
                        HeaderLabel = "Node",
                        PropertyName = "Key",
                        Kind = SerializedTableColumnKind.Custom,
                        Flex = 1f,
                        CustomBuilder = prop => new AttachNodeIdField(prop, label: null, target: part),
                    },
                    new SerializedTableColumn
                    {
                        HeaderLabel = "Position",
                        PropertyName = "Value",
                        Kind = SerializedTableColumnKind.Custom,
                        Flex = 2f,
                        CustomBuilder = prop => new VectorHandleField(prop, target, SceneHandlePicker.HandleMode.Position, label: string.Empty),
                    },
                });
            outer.Add(table.Build());
            return outer;
        }
    }
}
