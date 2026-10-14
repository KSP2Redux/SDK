using System;
using System.Collections.Generic;
using System.Linq;
using KSP.Game.Science;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The edits the tech tree editor makes to a tree: adding, duplicating, deleting and renaming nodes, and linking
    /// them, each keeping every reference between nodes consistent.
    /// </summary>
    /// <remarks>
    /// These change the asset directly and record nothing, so a caller records the asset for undo first.
    /// </remarks>
    public static class TechTreeEditing
    {
        /// <summary>
        /// The ID a new node is given, made unique with a number when a node already has it.
        /// </summary>
        public const string NEW_NODE_ID = "tNode_new";

        /// <summary>
        /// Finds a node by its ID.
        /// </summary>
        /// <param name="tree">The tree to search.</param>
        /// <param name="nodeId">The node's ID.</param>
        /// <returns>The first node with the ID, or null when none has it.</returns>
        public static TechTreeNode Find(TechTreeAsset tree, string nodeId) =>
            string.IsNullOrEmpty(nodeId) ? null : tree.Nodes.FirstOrDefault(node => node.Data?.ID == nodeId);

        /// <summary>
        /// Finds a node by its ID in a tree, then in the reference nodes it is placed against.
        /// </summary>
        /// <param name="tree">The tree to search first.</param>
        /// <param name="reference">The reference nodes, or null for none.</param>
        /// <param name="nodeId">The node's ID.</param>
        /// <returns>The node, or null when neither has it.</returns>
        public static TechTreeNode FindAny(TechTreeAsset tree, IReadOnlyList<TechTreeNode> reference, string nodeId) =>
            Find(tree, nodeId) ?? reference?.FirstOrDefault(node => node.Data?.ID == nodeId);

        /// <summary>
        /// Gets the IDs of the nodes that require a node.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <param name="nodeId">The required node's ID.</param>
        /// <param name="reference">The reference nodes to search too, or null for none.</param>
        /// <returns>The requiring nodes' IDs, the tree's first, in order.</returns>
        public static List<string> GetDependents(
            TechTreeAsset tree,
            string nodeId,
            IReadOnlyList<TechTreeNode> reference = null
        ) =>
            tree.Nodes.Concat(reference ?? Array.Empty<TechTreeNode>())
                .Where(node => node.Data?.RequiredTechNodeIDs != null && node.Data.RequiredTechNodeIDs.Contains(nodeId))
                .Select(node => node.Data.ID)
                .Distinct()
                .ToList();

        /// <summary>
        /// Gets an ID no node in a tree has yet.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <param name="baseId">The ID wanted.</param>
        /// <returns>The ID itself when it is free, otherwise the ID with the lowest free number after it.</returns>
        public static string MakeUniqueId(TechTreeAsset tree, string baseId)
        {
            var taken = new HashSet<string>(tree.Nodes.Select(node => node.Data?.ID).Where(id => id != null));
            if (!taken.Contains(baseId))
                return baseId;

            int number = 2;
            while (taken.Contains($"{baseId}_{number}"))
            {
                number++;
            }

            return $"{baseId}_{number}";
        }

        /// <summary>
        /// Checks whether making one node require another would close a loop, which no node in it could ever unlock.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <param name="nodeId">The node that would require the other.</param>
        /// <param name="prerequisiteId">The node it would require.</param>
        /// <param name="reference">The reference nodes the tree is placed against, or null for none.</param>
        /// <returns>True if the two are the same node or the prerequisite already requires the node, directly or
        /// through others.</returns>
        public static bool WouldCreateLoop(
            TechTreeAsset tree,
            string nodeId,
            string prerequisiteId,
            IReadOnlyList<TechTreeNode> reference = null
        )
        {
            if (nodeId == prerequisiteId)
                return true;

            var visited = new HashSet<string>();
            var pending = new Stack<string>();
            pending.Push(prerequisiteId);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (!visited.Add(current))
                    continue;

                foreach (string required in FindAny(tree, reference, current)?.Data.RequiredTechNodeIDs ?? Array.Empty<string>())
                {
                    if (required == nodeId)
                        return true;

                    pending.Push(required);
                }
            }

            return false;
        }

        /// <summary>
        /// Makes one node require another.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <param name="nodeId">The node that will require the other.</param>
        /// <param name="prerequisiteId">The node it will require, in the tree or the reference nodes.</param>
        /// <param name="reference">The reference nodes the tree is placed against, or null for none.</param>
        /// <returns>True if the link was added, false if either node is missing, the link exists, or it would close a
        /// loop. A reference node never gains a prerequisite.</returns>
        public static bool TryAddPrerequisite(
            TechTreeAsset tree,
            string nodeId,
            string prerequisiteId,
            IReadOnlyList<TechTreeNode> reference = null
        )
        {
            TechTreeNode node = Find(tree, nodeId);
            if (node == null
                || FindAny(tree, reference, prerequisiteId) == null
                || WouldCreateLoop(tree, nodeId, prerequisiteId, reference))
            {
                return false;
            }

            string[] required = node.Data.RequiredTechNodeIDs ?? Array.Empty<string>();
            if (required.Contains(prerequisiteId))
                return false;

            node.Data.RequiredTechNodeIDs = required.Append(prerequisiteId).ToArray();
            return true;
        }

        /// <summary>
        /// Stops one node requiring another.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <param name="nodeId">The requiring node.</param>
        /// <param name="prerequisiteId">The node it requires.</param>
        /// <returns>True if the link was removed, false if there was none.</returns>
        public static bool RemovePrerequisite(TechTreeAsset tree, string nodeId, string prerequisiteId)
        {
            TechTreeNode node = Find(tree, nodeId);
            if (node?.Data.RequiredTechNodeIDs == null || !node.Data.RequiredTechNodeIDs.Contains(prerequisiteId))
                return false;

            node.Data.RequiredTechNodeIDs = node.Data.RequiredTechNodeIDs.Where(id => id != prerequisiteId).ToArray();
            return true;
        }

        /// <summary>
        /// Adds an empty node.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <param name="position">The node's tech tree position.</param>
        /// <returns>The new node.</returns>
        public static TechTreeNode AddNode(TechTreeAsset tree, Vector2 position)
        {
            var node = new TechTreeNode
            {
                Data = new TechNodeData { ID = MakeUniqueId(tree, NEW_NODE_ID), TechTreePosition = position },
                EnglishName = string.Empty,
                EnglishDescription = string.Empty,
            };
            tree.Nodes.Add(node);
            return node;
        }

        /// <summary>
        /// Copies a node, with its prerequisites, to a new ID beside it.
        /// </summary>
        /// <remarks>
        /// Nothing requires the copy, and its parts are left off, since a part can only be unlocked by one node.
        /// </remarks>
        /// <param name="tree">The tree.</param>
        /// <param name="nodeId">The node to copy.</param>
        /// <param name="offset">How far from the original the copy is placed, in tech tree units.</param>
        /// <returns>The copy, or null when the node is missing.</returns>
        public static TechTreeNode DuplicateNode(TechTreeAsset tree, string nodeId, Vector2 offset)
        {
            TechTreeNode original = Find(tree, nodeId);
            if (original == null || !TechTreeJson.TryFromJson(TechTreeJson.ToJson(original.Data), out TechNodeData data))
                return null;

            data.ID = MakeUniqueId(tree, $"{nodeId}_copy");
            data.TechTreePosition += offset;
            data.UnlockedPartsIDs = Array.Empty<string>();
            var copy = new TechTreeNode
            {
                Data = data,
                EnglishName = original.EnglishName,
                EnglishDescription = original.EnglishDescription,
            };
            tree.Nodes.Insert(tree.Nodes.IndexOf(original) + 1, copy);
            return copy;
        }

        /// <summary>
        /// Deletes a node and every link to it.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <param name="nodeId">The node to delete.</param>
        /// <returns>True if the node was deleted, false if it was missing.</returns>
        public static bool DeleteNode(TechTreeAsset tree, string nodeId)
        {
            // Deleting an override removes the reference node it changes
            if (tree is TechTreePatchAsset patch && patch.IsOverride(nodeId))
                return RemoveReferenceNode(patch, nodeId);

            TechTreeNode node = Find(tree, nodeId);
            if (node == null)
                return false;

            tree.Nodes.Remove(node);
            foreach (string dependent in GetDependents(tree, nodeId))
            {
                RemovePrerequisite(tree, dependent, nodeId);
            }

            return true;
        }

        /// <summary>
        /// Takes a reference node into a patch to change it, with a baseline copy of it as it is now.
        /// </summary>
        /// <param name="patch">The patch.</param>
        /// <param name="referenceNode">The reference node.</param>
        /// <returns>The patch's copy of the node, the one already taken when it was.</returns>
        public static TechTreeNode TakeReferenceNode(TechTreePatchAsset patch, TechTreeNode referenceNode)
        {
            string nodeId = referenceNode.Data.ID;
            TechTreeNode taken = Find(patch, nodeId);
            if (taken != null)
                return taken;

            patch.RemovedNodeIds.Remove(nodeId);
            patch.Baselines.Add(CopyNode(referenceNode));
            taken = CopyNode(referenceNode);
            patch.Nodes.Add(taken);
            return taken;
        }

        /// <summary>
        /// Removes a reference node, dropping any change the patch made to it.
        /// </summary>
        /// <param name="patch">The patch.</param>
        /// <param name="nodeId">The reference node's ID.</param>
        /// <returns>True if the node is now removed, false if it was already.</returns>
        public static bool RemoveReferenceNode(TechTreePatchAsset patch, string nodeId)
        {
            RevertOverride(patch, nodeId);
            if (patch.RemovedNodeIds.Contains(nodeId))
                return false;

            patch.RemovedNodeIds.Add(nodeId);
            return true;
        }

        /// <summary>
        /// Brings back a reference node the patch removed.
        /// </summary>
        /// <param name="patch">The patch.</param>
        /// <param name="nodeId">The reference node's ID.</param>
        /// <returns>True if the node was removed and is now back.</returns>
        public static bool RestoreReferenceNode(TechTreePatchAsset patch, string nodeId) => patch.RemovedNodeIds.Remove(nodeId);

        /// <summary>
        /// Drops a patch's changes to a reference node, so the reference's node shows again.
        /// </summary>
        /// <param name="patch">The patch.</param>
        /// <param name="nodeId">The node's ID.</param>
        /// <returns>True if the node was an override.</returns>
        public static bool RevertOverride(TechTreePatchAsset patch, string nodeId)
        {
            TechTreeNode baseline = patch.FindBaseline(nodeId);
            if (baseline == null)
                return false;

            patch.Baselines.Remove(baseline);
            patch.Nodes.RemoveAll(node => node.Data?.ID == nodeId);
            return true;
        }

        /// <summary>
        /// Gives a node a new ID and points every link to it at the new one.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <param name="oldId">The node's ID.</param>
        /// <param name="newId">The ID to give it.</param>
        /// <returns>True if the node was renamed, false if it is missing, the new ID is empty, or another node has
        /// it.</returns>
        public static bool TryRenameNode(TechTreeAsset tree, string oldId, string newId)
        {
            TechTreeNode node = Find(tree, oldId);
            if (node == null || string.IsNullOrWhiteSpace(newId) || Find(tree, newId) != null)
                return false;

            // An override patches the reference node by its ID, so it keeps the ID
            if (tree is TechTreePatchAsset patch && patch.IsOverride(oldId))
                return false;

            foreach (TechTreeNode other in tree.Nodes.Where(other => other.Data?.RequiredTechNodeIDs != null))
            {
                other.Data.RequiredTechNodeIDs = other.Data.RequiredTechNodeIDs
                    .Select(id => id == oldId ? newId : id)
                    .ToArray();
            }

            node.Data.ID = newId;
            return true;
        }

        /// <summary>
        /// Copies a node, its data included, so the copy shares nothing with the original.
        /// </summary>
        /// <param name="node">The node to copy.</param>
        /// <returns>The copy.</returns>
        public static TechTreeNode CopyNode(TechTreeNode node) =>
            new()
            {
                Data = TechTreePatchBake.Copy(node.Data),
                EnglishName = node.EnglishName,
                EnglishDescription = node.EnglishDescription,
            };
    }
}
