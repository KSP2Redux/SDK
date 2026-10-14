using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation
{
    /// <summary>
    /// What the tech tree validators check: a tree and the reference nodes it is placed against.
    /// </summary>
    public class TechTreeValidationContext
    {
        private HashSet<string> _referenceIds;

        /// <summary>
        /// Creates a context.
        /// </summary>
        /// <param name="tree">The tree to check.</param>
        /// <param name="reference">The nodes of the tree it is placed against, or null for none.</param>
        /// <param name="snapsToGrid">Whether the editor snaps to the stock grid.</param>
        /// <param name="referenceTreeData">The tiers and start node of the tree it is placed against, or null for
        /// none.</param>
        public TechTreeValidationContext(
            TechTreeAsset tree,
            IReadOnlyList<TechTreeNode> reference = null,
            bool snapsToGrid = false,
            ReferenceTreeData referenceTreeData = null
        )
        {
            Tree = tree;
            Reference = reference ?? Array.Empty<TechTreeNode>();
            SnapsToGrid = snapsToGrid;
            ReferenceTreeData = referenceTreeData;
        }

        /// <summary>
        /// Gets the tree.
        /// </summary>
        public TechTreeAsset Tree { get; }

        /// <summary>
        /// Gets the reference nodes, empty when there are none.
        /// </summary>
        public IReadOnlyList<TechTreeNode> Reference { get; }

        /// <summary>
        /// Gets whether the editor snaps to the stock grid, so a node off it is worth noting.
        /// </summary>
        public bool SnapsToGrid { get; }

        /// <summary>
        /// Gets the tiers and start node of the reference tree, or null when it has none.
        /// </summary>
        public ReferenceTreeData ReferenceTreeData { get; }

        /// <summary>
        /// Gets the IDs of the reference nodes.
        /// </summary>
        public HashSet<string> ReferenceIds =>
            _referenceIds ??= new HashSet<string>(Reference.Select(node => node.Data?.ID).Where(id => id != null));

        /// <summary>
        /// Finds a node in the tree, then among the reference nodes.
        /// </summary>
        /// <param name="nodeId">The node's ID.</param>
        /// <returns>The node, or null when neither has it.</returns>
        public TechTreeNode FindAny(string nodeId) => TechTreeEditing.FindAny(Tree, Reference, nodeId);

        /// <summary>
        /// Gets the name a message shows a node by.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns>Its English name, or its ID when it has none.</returns>
        public static string NameOf(TechTreeNode node) =>
            string.IsNullOrEmpty(node.EnglishName) ? node.Data.ID : node.EnglishName;

        /// <summary>
        /// Changes the tree as a fix, recorded for undo.
        /// </summary>
        /// <param name="undoName">The undo step's name.</param>
        /// <param name="change">The change.</param>
        public void Change(string undoName, Action change)
        {
            Undo.RecordObject(Tree, undoName);
            change();
            EditorUtility.SetDirty(Tree);
        }
    }
}
