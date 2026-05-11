using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Editor-only sidecar for a <c>PQSDecalController</c>'s bake state, holding a hash of the
    /// inputs that fed the last successful bake.
    /// </summary>
    /// <remarks>
    /// Stored as a sub-asset of <see cref="PlanetAuthoringRegistry" />, keyed by the
    /// PqsDecalData asset GUID (the most stable per-controller identifier). The validator computes
    /// the current input hash and warns when it differs from <see cref="LastBakeHash" />.
    /// </remarks>
    public class PQSDecalControllerAuthoring : ScriptableObject
    {
        /// <summary>
        /// AssetDatabase GUID of the controller's PqsDecalData asset, used as the sidecar key.
        /// </summary>
        public string PqsDecalDataGuid;

        /// <summary>
        /// Hash of the inputs that fed the last successful bake, computed by <see cref="PQSDecalBakeHash" />.
        /// </summary>
        public string LastBakeHash;
    }
}
