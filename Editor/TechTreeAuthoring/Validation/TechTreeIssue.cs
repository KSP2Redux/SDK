using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation
{
    /// <summary>
    /// A problem a tech tree validator found, with the node it is about so the editor can go to it.
    /// </summary>
    public readonly struct TechTreeIssue
    {
        /// <summary>
        /// Creates an issue.
        /// </summary>
        /// <param name="issue">The problem.</param>
        /// <param name="nodeId">The node it is about, or null for the whole tree.</param>
        public TechTreeIssue(ValidationIssue issue, string nodeId)
        {
            Issue = issue;
            NodeId = nodeId;
        }

        /// <summary>
        /// Gets the problem, with its code, severity and any fixes.
        /// </summary>
        public ValidationIssue Issue { get; }

        /// <summary>
        /// Gets the ID of the node the problem is about, or null when it is about the tree as a whole.
        /// </summary>
        public string NodeId { get; }
    }
}
