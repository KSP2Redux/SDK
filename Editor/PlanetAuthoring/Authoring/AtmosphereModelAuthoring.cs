using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Editor-only sidecar for an <c>AtmosphereModel</c>, recording which inputs its baked lookup tables were computed from.
    /// </summary>
    /// <remarks>
    /// Lives in the <c>Data/</c> folder next to the model, resolved by <see cref="AuthoringSidecars" />. The
    /// stale-tables validator compares <see cref="BakedLutHash" /> with the model's current
    /// <c>BrunetonLutBaker.ComputeLutInputHash</c>.
    /// </remarks>
    public class AtmosphereModelAuthoring : ScriptableObject
    {
        /// <summary>
        /// Hash of the model's table inputs at the last successful bake.
        /// </summary>
        public int BakedLutHash;
    }
}
