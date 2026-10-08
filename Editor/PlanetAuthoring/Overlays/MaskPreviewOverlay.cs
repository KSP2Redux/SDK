using KSP.Rendering.Planets;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Overlays
{
    /// <summary>
    /// Renders the body's biome or subzone mask texture as a colored RGBA-channel visualization
    /// using the shared <c>Redux/PlanetAuthoring/Overlays/MaskOverlay</c> shader.
    /// </summary>
    internal sealed class MaskPreviewOverlay : PreviewOverlay
    {
        /// <summary>
        /// Selects which mask texture the overlay reads from the surface material.
        /// </summary>
        public enum Source
        {
            /// <summary>
            /// Sample the body's biome mask texture.
            /// </summary>
            BiomeMask,

            /// <summary>
            /// Sample the body's subzone mask texture.
            /// </summary>
            SubzoneMask,
        }

        private const string ShaderName = "Redux/PlanetAuthoring/Overlays/MaskOverlay";
        private const string BiomeMaskTexProperty = "_BiomeMaskTex";
        private const string SubzoneMaskTexProperty = "_SubzoneMaskTex";

        private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");

        private readonly Source _source;
        private float _strength = 0.7f;

        /// <summary>
        /// Initializes a new mask overlay bound to the given mask source.
        /// </summary>
        /// <param name="source">Which mask texture (biome or subzone) the overlay should sample.</param>
        public MaskPreviewOverlay(Source source) : base(ShaderName)
        {
            _source = source;
        }

        /// <summary>
        /// Gets or sets the 0..1 blend strength applied to the mask color in the shader.
        /// </summary>
        public float Strength
        {
            get => _strength;
            set
            {
                _strength = Mathf.Clamp01(value);
                OverlayMaterial.SetFloat(StrengthId, _strength);
            }
        }

        /// <inheritdoc />
        public override void RefreshBindings(PQS pqs)
        {
            var surface = pqs?.data?.materialSettings?.surfaceMaterial;
            if (surface == null)
            {
                OverlayMaterial.SetTexture(MaskTexId, Texture2D.blackTexture);
                return;
            }

            var srcProp = _source == Source.BiomeMask ? BiomeMaskTexProperty : SubzoneMaskTexProperty;
            var mask = surface.HasProperty(srcProp) ? surface.GetTexture(srcProp) : null;
            OverlayMaterial.SetTexture(MaskTexId, mask != null ? mask : Texture2D.blackTexture);
            OverlayMaterial.SetFloat(StrengthId, _strength);
        }
    }
}
