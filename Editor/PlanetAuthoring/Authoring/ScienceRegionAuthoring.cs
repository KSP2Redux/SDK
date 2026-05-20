using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Editor-only sidecar for a <see cref="KSP.Game.Science.ScienceRegionData" /> asset, holding
    /// the bake fingerprint used to detect when the baked artifacts are stale.
    /// </summary>
    /// <remarks>
    /// Lives as a standalone <c>.asset</c> in the owning body's <c>Data/</c> folder, resolved by
    /// <see cref="AuthoringSidecars" />. The fingerprint hashes the source map + region rows at
    /// bake time, and the SR_BAKED_DRIFT validator recomputes the same hash from current state
    /// and compares.
    /// </remarks>
    public class ScienceRegionAuthoring : ScriptableObject
    {
        /// <summary>
        /// Hash of the bake inputs (source map GUID, source map importer version, and each region row's Id, MapId, and color), or the empty string when the region has never been baked.
        /// </summary>
        /// <remarks>
        /// The empty-string sentinel lets the SR_BAKED_DRIFT validator distinguish "stale" from "unbaked".
        /// </remarks>
        public string LastBakeFingerprint = string.Empty;

        /// <summary>
        /// Authoring preference for the scene-view discoverable overlay.
        /// </summary>
        /// <remarks>
        /// When true, each discoverable renders as a colored orb (sized by Radius) with a legend. When false, free-move handles still render but the orbs and legend are suppressed.
        /// </remarks>
        public bool ShowDiscoverableOrbs = true;
    }
}
