using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Editor-only sidecar for a <see cref="PQSDecal" /> template, holding the per-decal source
    /// textures the Redux baker reads but the runtime never samples directly.
    /// </summary>
    /// <remarks>
    /// Lives as a standalone <c>.asset</c> in the owning decal's <c>Data/</c> folder, resolved
    /// by <see cref="AuthoringSidecars" />. Runtime PQSDecal fields (HeightScale, BlendMode,
    /// opacity, flags, MaterialScaleFactor, ...) are base-game data and stay on the asset. Only
    /// the Redux-added textures live here so they don't pull source bytes into addressables bundles.
    /// </remarks>
    public class PQSDecalTemplateAuthoring : ScriptableObject
    {
        /// <summary>
        /// Source diffuse texture for the decal, packed into the body's PQSDecalData diffuse array by the baker.
        /// </summary>
        [Tooltip("Decal albedo, encoded as a 2-channel diff. The shader reads RG and BA separately and applies (RG - BA) as a delta lerped into the surface color diffs. NOT a normal RGBA albedo. Pair with AlbedoOpacity > 0 to actually see the contribution.")]
        public Texture2D Diffuse;

        /// <summary>
        /// Source normal map, packed into the body's PQSDecalData.NormalTextureArray by the baker.
        /// </summary>
        [Tooltip("Normal map. Packed into the body's PQSDecalData.NormalTextureArray by the baker.")]
        public Texture2D Normal;

        /// <summary>
        /// Source alpha mask, packed into the body's PQSDecalData.AlphaMaskTextureArray by the baker.
        /// </summary>
        [Tooltip("Alpha mask texture. Packed into the body's PQSDecalData.AlphaMaskTextureArray by the baker.")]
        public Texture2D AlphaMaskTexture;

        /// <summary>
        /// Source peak texture, packed into the body's PQSDecalData.PeakTextureArray by the baker.
        /// </summary>
        [Tooltip("Peak texture. Packed into the body's PQSDecalData.PeakTextureArray by the baker.")]
        public Texture2D Peak;

        /// <summary>
        /// Source slope texture, packed into the body's PQSDecalData.SlopeTextureArray by the baker.
        /// </summary>
        [Tooltip("Slope texture. Packed into the body's PQSDecalData.SlopeTextureArray by the baker.")]
        public Texture2D Slope;
    }
}
