using KSP;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEngine.UIElements;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants.Editors
{
    /// <summary>
    /// Custom editor for <see cref="AttachNodeAdder" />. Reuses <see cref="AttachNodesList" /> so the Variants tab gets the same per-node card UX (foldout, scene-view position and orientation handles) the Core tab uses for the part's own attach nodes.
    /// </summary>
    /// <remarks>
    /// nodeID is left as a free TextField rather than a constrained dropdown. AttachNodeAdder's runtime semantics treat a matching ID as "reposition existing", and a non-matching ID as "add new", so any constraint here would hurt the reposition path.
    /// </remarks>
    [TransformerEditor(typeof(AttachNodeAdder))]
    public sealed class AttachNodeAdderEditor : ITransformerEditor
    {
        /// <inheritdoc />
        public VisualElement Build(ITransformer transformer, SerializedProperty transformerProp, TransformerEditorContext context)
        {
            var outer = new VisualElement();

            var nodesProp = transformerProp?.FindPropertyRelative("Nodes");
            if (nodesProp == null)
            {
                outer.Add(new HelpBox("Nodes array not found on this transformer.", HelpBoxMessageType.Error));
                return outer;
            }

            var part = context?.Part;
            outer.Add(AttachNodesList.Build(nodesProp, part));
            return outer;
        }
    }
}
