namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation
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
        /// <summary>Likely authoring mistake. Should be reviewed but does not block running the body.</summary>
        Warning,
        /// <summary>Hard error. The body will not work correctly until this is resolved.</summary>
        Error,
    }
}
