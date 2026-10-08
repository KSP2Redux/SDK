using System;
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.Validation
{
    /// <summary>
    /// One validation finding emitted by an <see cref="IValidator{T}" />.
    /// </summary>
    /// <remarks>
    /// Identified by <see cref="Code" /> for stable matching across runs. <see cref="Message" /> is the artist-facing display string.
    /// </remarks>
    public readonly struct ValidationIssue
    {
        private static readonly IReadOnlyList<ValidationFix> EmptyFixes = Array.Empty<ValidationFix>();

        /// <summary>
        /// Creates an issue with no fix actions.
        /// </summary>
        /// <param name="code">Stable identifier for this issue (matches the validator's check code).</param>
        /// <param name="severity">Severity level.</param>
        /// <param name="message">Display message shown in the inspector.</param>
        public ValidationIssue(string code, ValidationSeverity severity, string message)
            : this(code, severity, message, EmptyFixes)
        {
        }

        /// <summary>
        /// Creates an issue with one or more fix actions.
        /// </summary>
        /// <param name="code">Stable identifier for this issue (matches the validator's check code).</param>
        /// <param name="severity">Severity level.</param>
        /// <param name="message">Display message shown in the inspector.</param>
        /// <param name="fixes">Fix actions, rendered as buttons in the order given. The first fix is the recommended one.</param>
        public ValidationIssue(string code, ValidationSeverity severity, string message, IReadOnlyList<ValidationFix> fixes)
        {
            Code = code;
            Severity = severity;
            Message = message;
            Fixes = fixes ?? EmptyFixes;
        }

        /// <summary>Stable identifier for this issue.</summary>
        public string Code { get; }

        /// <summary>Severity level driving the inspector display.</summary>
        public ValidationSeverity Severity { get; }

        /// <summary>Display message shown in the inspector row.</summary>
        public string Message { get; }

        /// <summary>Fix actions attached to this issue. May be empty.</summary>
        public IReadOnlyList<ValidationFix> Fixes { get; }
    }
}
