using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Changes to a tree the asset does not own, such as Redux's or another mod's: nodes it adds, reference nodes it
    /// changes, and reference nodes it removes.
    /// </summary>
    /// <remarks>
    /// Its nodes are the new nodes and the overrides. An override is a reference node taken into the asset to change,
    /// with a baseline copy of the node as it was when taken. The bake writes the new nodes as JSON and a Patch Manager
    /// patch with only the fields each override changed from its baseline, so a field the author never touched is
    /// never written, even if the reference changes it later.
    /// </remarks>
    public class TechTreePatchAsset : TechTreeAsset
    {
        /// <summary>
        /// Where the tree the patch changes comes from.
        /// </summary>
        public TechNodeReferenceSource ReferenceSource;

        /// <summary>
        /// The tech tree asset, or the folder of tech node JSON, the patch changes when <see cref="ReferenceSource" />
        /// is <see cref="TechNodeReferenceSource.Asset" />.
        /// </summary>
        public Object Reference;

        /// <summary>
        /// Each override's node as it was in the reference when it was taken.
        /// </summary>
        [HideInInspector] public List<TechTreeNode> Baselines = new();

        /// <summary>
        /// The IDs of the reference nodes the patch removes.
        /// </summary>
        public List<string> RemovedNodeIds = new();

        /// <summary>
        /// The features the patch and its new nodes load under, beside the mod's own. Ids of the mod's own features
        /// are bare.
        /// </summary>
        public List<string> Features = new();

        /// <summary>
        /// The features that keep the patch and its new nodes from loading.
        /// </summary>
        public List<string> ExcludedFeatures = new();

        /// <summary>
        /// Gets the baseline of an override.
        /// </summary>
        /// <param name="nodeId">The node's ID.</param>
        /// <returns>The node as it was taken, or null when the node is not an override.</returns>
        public TechTreeNode FindBaseline(string nodeId) =>
            string.IsNullOrEmpty(nodeId) ? null : Baselines.FirstOrDefault(baseline => baseline.Data?.ID == nodeId);

        /// <summary>
        /// Checks whether one of the patch's nodes is an override of a reference node rather than a new node.
        /// </summary>
        /// <param name="nodeId">The node's ID.</param>
        /// <returns>True if the node is an override.</returns>
        public bool IsOverride(string nodeId) => FindBaseline(nodeId) != null;
    }
}
