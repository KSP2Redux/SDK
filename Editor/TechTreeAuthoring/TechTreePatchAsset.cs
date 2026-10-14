using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Changes to a tree the asset does not own, such as Redux's or another mod's: nodes it adds, reference nodes it
    /// changes, reference nodes it removes, and the reference's tier names and start node.
    /// </summary>
    /// <remarks>
    /// Its nodes are the new nodes and the overrides. An override is a reference node taken into the asset to change,
    /// with a baseline copy of the node as it was when taken. The bake writes the new nodes as JSON and a Patch Manager
    /// patch with only the fields each override changed from its baseline, so a field the author never touched is
    /// never written, even if the reference changes it later.
    /// </remarks>
    public class TechTreePatchAsset : BaseReferencedTechTreeAsset
    {
        /// <summary>
        /// Each override's node as it was in the reference when it was taken.
        /// </summary>
        [HideInInspector] public List<TechTreeNode> Baselines = new();

        /// <summary>
        /// The reference's tiers and start node as they were when the patch first saw them, or null before it has.
        /// </summary>
        /// <remarks>
        /// The patch's own <see cref="TechTreeAsset.Tiers" /> and <see cref="TechTreeAsset.StartNodeId" /> are compared
        /// with it, so only a tier or start node the author changed is written.
        /// </remarks>
        [SerializeReference, HideInInspector] public ReferenceTreeData TreeDataBaseline;

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

        /// <summary>
        /// Takes the reference's tree data as the baseline the first time the patch sees it, and starts the patch's
        /// tiers and start node from it when the patch has none of its own.
        /// </summary>
        /// <param name="reference">The reference's tree data, or null when the reference has none.</param>
        /// <returns>True if the patch took the baseline, false when it already has one or there is none to take.</returns>
        public bool TakeReferenceTreeData(ReferenceTreeData reference)
        {
            if (TreeDataBaseline != null || reference == null)
                return false;

            TreeDataBaseline = reference.Copy();
            if (Tiers.Count == 0 && string.IsNullOrEmpty(StartNodeId))
            {
                Tiers = ReferenceTreeData.CopyTiers(reference.Tiers);
                StartNodeId = reference.StartNodeId;
            }

            return true;
        }
    }
}
