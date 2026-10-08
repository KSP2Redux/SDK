namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>Layer names referenced by the planet authoring tools.</summary>
    /// <remarks>
    /// The KSP2 runtime relies on layer-based camera and physics filtering to route the local and
    /// scaled body representations through their respective render passes. New prefabs must land on
    /// these layers or they will not be visible to the corresponding cameras.
    /// </remarks>
    public static class PlanetAuthoringLayers
    {
        /// <summary>Layer for the Scaled prefab root, rendered by the scaled-space camera.</summary>
        public const string Scaled = "Scaled.Scenery";

        /// <summary>Layer for the Local prefab root (PQS GameObject), rendered by the local-space camera and used by terrain physics.</summary>
        public const string Local = "Local.Terrain";
    }
}
