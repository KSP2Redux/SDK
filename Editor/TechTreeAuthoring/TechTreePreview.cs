using System;
using System.Collections.Generic;
using System.Linq;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The R&amp;D Center's unlock rules, so the tech tree editor can preview a campaign working through a tree.
    /// </summary>
    /// <remarks>
    /// A node is unlockable once every prerequisite is unlocked. In game it also needs its missions and research done,
    /// which the preview takes as done, since it shows the tree's shape rather than a campaign's progress.
    /// </remarks>
    public static class TechTreePreview
    {
        /// <summary>
        /// Gets the nodes every campaign starts with, the ones that cost nothing and need nothing, as the game picks
        /// them.
        /// </summary>
        /// <param name="nodes">Every node in the preview.</param>
        /// <returns>The IDs of the nodes unlocked at the start.</returns>
        public static HashSet<string> GetStartingUnlocked(IEnumerable<TechTreeNode> nodes) =>
            new(nodes
                .Where(node => node.Data?.ID != null
                    && node.Data.RequiredSciencePoints == 0
                    && (node.Data.RequiredTechNodeIDs?.Length ?? 0) == 0
                    && (node.Data.RequiredMissionIDs?.Length ?? 0) == 0
                    && (node.Data.RequiredResearchData?.Length ?? 0) == 0)
                .Select(node => node.Data.ID));

        /// <summary>
        /// Gets a node's state.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="unlocked">The IDs of the unlocked nodes.</param>
        /// <returns>The node's state.</returns>
        public static TechTreePreviewState GetState(TechTreeNode node, ISet<string> unlocked)
        {
            if (unlocked.Contains(node.Data.ID))
                return TechTreePreviewState.Unlocked;

            return (node.Data.RequiredTechNodeIDs ?? Array.Empty<string>()).All(unlocked.Contains)
                ? TechTreePreviewState.Unlockable
                : TechTreePreviewState.Locked;
        }

        /// <summary>
        /// Unlocks an unlockable node, or locks an unlocked one again along with every node that needs it.
        /// </summary>
        /// <param name="nodes">Every node in the preview.</param>
        /// <param name="unlocked">The IDs of the unlocked nodes, changed in place.</param>
        /// <param name="nodeId">The node clicked.</param>
        /// <returns>True if anything changed, false if the node is locked.</returns>
        public static bool Toggle(IReadOnlyList<TechTreeNode> nodes, ISet<string> unlocked, string nodeId)
        {
            TechTreeNode node = nodes.FirstOrDefault(candidate => candidate.Data?.ID == nodeId);
            if (node == null)
                return false;

            switch (GetState(node, unlocked))
            {
                case TechTreePreviewState.Unlockable:
                    unlocked.Add(nodeId);
                    return true;
                case TechTreePreviewState.Unlocked:
                    Relock(nodes, unlocked, nodeId);
                    return true;
                default:
                    return false;
            }
        }

        private static void Relock(IReadOnlyList<TechTreeNode> nodes, ISet<string> unlocked, string nodeId)
        {
            var pending = new Stack<string>();
            pending.Push(nodeId);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (!unlocked.Remove(current))
                    continue;

                foreach (TechTreeNode dependent in nodes.Where(other => other.Data?.RequiredTechNodeIDs?.Contains(current) == true))
                {
                    pending.Push(dependent.Data.ID);
                }
            }
        }
    }

    /// <summary>
    /// A node's state in the unlock preview, as the R&amp;D Center draws it.
    /// </summary>
    public enum TechTreePreviewState
    {
        /// <summary>
        /// A prerequisite is still locked.
        /// </summary>
        Locked,

        /// <summary>
        /// Every prerequisite is unlocked, so the node can be researched.
        /// </summary>
        Unlockable,

        /// <summary>
        /// The node is researched.
        /// </summary>
        Unlocked,
    }
}
