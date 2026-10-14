using System.Collections.Generic;
using System.Linq;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation
{
    /// <summary>
    /// One node of a tree, as a <see cref="ITechNodeValidator" /> checks it.
    /// </summary>
    public class TechNodeValidationContext
    {
        /// <summary>
        /// Creates a context for one node.
        /// </summary>
        /// <param name="tree">The tree's context.</param>
        /// <param name="node">The node.</param>
        /// <param name="index">The node's place in the tree.</param>
        public TechNodeValidationContext(TechTreeValidationContext tree, TechTreeNode node, int index)
        {
            Tree = tree;
            Node = node;
            Index = index;
        }

        /// <summary>
        /// Gets the tree's context.
        /// </summary>
        public TechTreeValidationContext Tree { get; }

        /// <summary>
        /// Gets the node.
        /// </summary>
        public TechTreeNode Node { get; }

        /// <summary>
        /// Gets the node's place in the tree.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the node's ID.
        /// </summary>
        public string Id => Node.Data.ID;

        /// <summary>
        /// Gets the name a message shows the node by.
        /// </summary>
        public string Name => TechTreeValidationContext.NameOf(Node);

        /// <summary>
        /// Gets the tree's nodes before this one, so a problem between two of them is reported once, on the later.
        /// </summary>
        public IEnumerable<TechTreeNode> EarlierNodes => Tree.Tree.Nodes.Take(Index);

        /// <summary>
        /// Gets the reference nodes the tree does not redefine.
        /// </summary>
        public IEnumerable<TechTreeNode> ReferenceOnlyNodes =>
            Tree.Reference.Where(node => node.Data?.ID != null && TechTreeEditing.Find(Tree.Tree, node.Data.ID) == null);
    }
}
