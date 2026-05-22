namespace Ksp2UnityTools.Editor.Validation
{
    /// <summary>
    /// Categorization for how often a validator should run.
    /// </summary>
    /// <remarks>
    /// The inspector tick refreshes validation every 500 ms. Cheap validators are safe to run on
    /// every tick (basic field reads, sidecar lookups, small array walks). Expensive ones hit
    /// AssetDatabase queries, hash large strings, walk texture pixels, or otherwise cost enough
    /// that they'd visibly stall the inspector at tick rate. Expensive validators are gated
    /// behind a manual "Run full validation" action in the inspector. Their last result persists
    /// alongside cheap-tick output until re-run.
    /// </remarks>
    public enum ValidatorCost
    {
        /// <summary>
        /// Safe to run on every inspector tick.
        /// </summary>
        Cheap,

        /// <summary>
        /// Run only when the user clicks "Run full validation".
        /// </summary>
        Expensive,
    }
}
