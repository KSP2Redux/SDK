namespace Ksp2UnityTools.Editor.PlanetAuthoring.Atmosphere
{
    /// <summary>
    /// The state of an atmosphere model's baked lookup tables.
    /// </summary>
    public enum AtmosphereTableState
    {
        /// <summary>One or more tables are unassigned, so the game renders no atmosphere.</summary>
        Missing,

        /// <summary>The tables were baked from different settings than the model has now.</summary>
        Stale,

        /// <summary>The tables were baked from the model's current settings.</summary>
        Current,
    }
}
