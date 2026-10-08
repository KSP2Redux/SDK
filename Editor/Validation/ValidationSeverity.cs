namespace Ksp2UnityTools.Editor.Validation
{
    /// <summary>
    /// Severity classification for a <see cref="ValidationIssue" />.
    /// </summary>
    /// <remarks>
    /// Drives the inspector's color band on the issue row and the count summary in the section header.
    /// </remarks>
    public enum ValidationSeverity
    {
        /// <summary>Informational note. No action required.</summary>
        Info,
        /// <summary>Likely authoring mistake. Should be reviewed but does not block running the validation target.</summary>
        Warning,
        /// <summary>Hard error. The validation target will not work correctly until this is resolved.</summary>
        Error,
    }
}
