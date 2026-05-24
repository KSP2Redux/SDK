using KSP.OAB;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Wizards
{
    /// <summary>
    /// Specification for a child AttachmentNode the wizard places on a newly-scaffolded part.
    /// </summary>
    /// <remarks>
    /// The scaffold creates a child GameObject named <see cref="NodeId" />, attaches an
    /// AttachmentNode component, applies <see cref="LocalPosition" /> and <see cref="LocalDirection" />,
    /// sets <see cref="Size" />, and lets <c>AttachNodeAutoGenerator.RegenerateFromHierarchy</c>
    /// populate the part's <c>attachNodes</c> from the live components.
    /// </remarks>
    public sealed class AttachNodeTemplate
    {
        /// <summary>Node ID (becomes the child GameObject name), e.g. "top" or "bottom".</summary>
        public string NodeId { get; }

        /// <summary>Position of the node relative to the part root.</summary>
        public Vector3 LocalPosition { get; }

        /// <summary>Outward-facing direction of the node, relative to the part root.</summary>
        public Vector3 LocalDirection { get; }

        /// <summary>Size class assigned to the node's <c>sizeKey</c>.</summary>
        public MetaAssemblySizeFilterType Size { get; }

        public AttachNodeTemplate(string nodeId, Vector3 localPosition, Vector3 localDirection, MetaAssemblySizeFilterType size)
        {
            NodeId = nodeId;
            LocalPosition = localPosition;
            LocalDirection = localDirection;
            Size = size;
        }
    }
}
