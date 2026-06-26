using KSP.OAB;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Wizards
{
    /// <summary>
    /// Specification for an attach-node definition the wizard writes into a newly-scaffolded part.
    /// </summary>
    public sealed class AttachNodeTemplate
    {
        /// <summary>Node ID (becomes the child GameObject name), e.g. "top" or "bottom".</summary>
        public string NodeId { get; }

        /// <summary>Position of the node relative to the part root.</summary>
        public Vector3 LocalPosition { get; }

        /// <summary>Outward-facing direction of the node, relative to the part root.</summary>
        public Vector3 LocalDirection { get; }

        /// <summary>Optional node size key. Null or empty means use the new part's selected size key.</summary>
        public string SizeKey { get; }

        /// <summary>
        /// Creates a new <see cref="AttachNodeTemplate" />.
        /// </summary>
        /// <param name="nodeId">The node ID, used as the child GameObject name.</param>
        /// <param name="localPosition">The node position relative to the part root.</param>
        /// <param name="localDirection">The outward-facing direction of the node, relative to the part root.</param>
        /// <param name="sizeKey">Optional node size key. Null or empty means use the new part's selected size key.</param>
        public AttachNodeTemplate(string nodeId, Vector3 localPosition, Vector3 localDirection, string sizeKey = null)
        {
            NodeId = nodeId;
            LocalPosition = localPosition;
            LocalDirection = localDirection;
            SizeKey = sizeKey;
        }
    }
}
