using System;

namespace Ksp2UnityTools.Editor.Validation
{
    /// <summary>
    /// One auto-fix action attached to a <see cref="ValidationIssue" />.
    /// </summary>
    /// <remarks>
    /// The inspector renders one button per fix, labeled with <see cref="Label" /> and invoking <see cref="Apply" /> on click.
    /// </remarks>
    public readonly struct ValidationFix
    {
        /// <summary>
        /// Creates a fix with the given button label and apply callback.
        /// </summary>
        /// <param name="label">Button label shown in the inspector.</param>
        /// <param name="apply">Action invoked when the user clicks the fix button.</param>
        public ValidationFix(string label, Action apply)
        {
            Label = label;
            Apply = apply;
        }

        /// <summary>The button label shown in the inspector.</summary>
        public string Label { get; }

        /// <summary>The action invoked when the user clicks the fix button.</summary>
        public Action Apply { get; }
    }
}
