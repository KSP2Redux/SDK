namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// One new tech node as an authoring asset, placed and linked against a tree it does not own.
    /// </summary>
    /// <remarks>
    /// It is a <see cref="TechTreeAsset" /> that holds one node and no tiers, so it bakes, exports and takes features
    /// as a tree does. It adds nodes only. Changes to nodes that already exist belong to a patch.
    /// </remarks>
    public class TechNodeAsset : BaseReferencedTechTreeAsset
    {
        /// <summary>
        /// Gets the node.
        /// </summary>
        public TechTreeNode Node => Nodes.Count > 0 ? Nodes[0] : null;
    }
}
