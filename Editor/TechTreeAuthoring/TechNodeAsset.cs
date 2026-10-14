using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// One new tech node as an authoring asset, placed and linked against a tree it does not own.
    /// </summary>
    /// <remarks>
    /// It is a <see cref="TechTreeAsset" /> that holds one node and no tiers, so it bakes, exports and takes features
    /// as a tree does. It adds nodes only. Changes to nodes that already exist belong to a patch.
    /// </remarks>
    public class TechNodeAsset : TechTreeAsset
    {
        /// <summary>
        /// Where the tree the node is placed against comes from.
        /// </summary>
        public TechNodeReferenceSource ReferenceSource;

        /// <summary>
        /// The tech tree asset, or the folder of tech node JSON, the node is placed against when
        /// <see cref="ReferenceSource" /> is <see cref="TechNodeReferenceSource.Asset" />.
        /// </summary>
        public Object Reference;

        /// <summary>
        /// Gets the node.
        /// </summary>
        public TechTreeNode Node => Nodes.Count > 0 ? Nodes[0] : null;
    }

    /// <summary>
    /// Where the tree a <see cref="TechNodeAsset" /> is placed against comes from.
    /// </summary>
    public enum TechNodeReferenceSource
    {
        /// <summary>
        /// Every tech node the game's catalogs and the project's own Addressables hold, before any patch.
        /// </summary>
        GameNodes,

        /// <summary>
        /// A tech tree asset or a folder of tech node JSON.
        /// </summary>
        Asset,
    }
}
