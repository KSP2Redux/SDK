using KSP;
using KSP.OAB;
using KSP.Sim.Definitions;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Tools
{
    /// <summary>
    /// Rebuilds a part's <c>attachNodes</c> list from the <see cref="AttachmentNode" />
    /// components in its prefab hierarchy.
    /// </summary>
    /// <remarks>
    /// Each <see cref="AttachmentNode" /> in the prefab supplies its transform, sizing, and joint
    /// metadata. The generator converts world-space transforms into part-local position and
    /// orientation.
    /// </remarks>
    public static class AttachNodeAutoGenerator
    {
        /// <summary>
        /// Clears <paramref name="target" />'s attach-node list and rebuilds it from every
        /// <see cref="AttachmentNode" /> component in the part's hierarchy.
        /// </summary>
        /// <remarks>
        /// Marks <paramref name="target" /> dirty so the change is persisted. No-op when
        /// <paramref name="target" /> is null.
        /// </remarks>
        /// <param name="target">The part to regenerate attach nodes for.</param>
        public static void RegenerateFromHierarchy(CorePartData target)
        {
            if (target == null || target.Core == null) return;

            target.Core.data.attachNodes.Clear();
            foreach (var attachmentNode in target.gameObject.GetComponentsInChildren<AttachmentNode>())
            {
                var obj = attachmentNode.gameObject;
                var pos = target.transform.InverseTransformPoint(obj.transform.position);
                var dir = target.transform.InverseTransformDirection(obj.transform.forward);
                var newDefinition = new AttachNodeDefinition
                {
                    nodeID = obj.name,
                    NodeSymmetryGroupID = attachmentNode.nodeSymmetryGroupID,
                    nodeType = attachmentNode.nodeType,
                    attachMethod = attachmentNode.attachMethod,
                    IsMultiJoint = attachmentNode.isMultiJoint,
                    MultiJointMaxJoint = attachmentNode.multiJointMaxJoint,
                    MultiJointRadiusOffset = attachmentNode.multiJointRadiusOffset,
                    position = pos,
                    orientation = dir,
                    size = attachmentNode.size,
                    sizeKey = PartSizeRegistry.GetAttachNodeSizeKey(attachmentNode.sizeKey, attachmentNode.size),
                    visualSize = attachmentNode.visualSize,
                    angularStrengthMultiplier = attachmentNode.angularStrengthMultiplier,
                    contactArea = attachmentNode.contactArea,
                    overrideDragArea = attachmentNode.overrideDragArea,
                    isCompoundJoint = attachmentNode.isCompoundJoint
                };
                target.Core.data.attachNodes.Add(newDefinition);
            }

            EditorUtility.SetDirty(target);
        }
    }
}
