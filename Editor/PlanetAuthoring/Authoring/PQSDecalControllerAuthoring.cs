using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Editor-only sidecar for a <c>PQSDecalController</c>'s bake state, holding a hash of the inputs that fed the last successful bake.
    /// </summary>
    /// <remarks>
    /// Lives in the <c>Data/</c> folder next to the owning <c>PQSDecalData</c> asset, resolved by <see cref="AuthoringSidecars" />. The validator computes the current input hash and warns when it differs from <see cref="LastBakeHash" />.
    /// </remarks>
    public class PQSDecalControllerAuthoring : ScriptableObject
    {
        /// <summary>
        /// Hash of the inputs that fed the last successful bake, computed by <see cref="PQSDecalBakeHash" />.
        /// </summary>
        public string LastBakeHash;
    }
}
