using AwesomeTechnologies.VegetationSystem;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Overlays
{
    /// <summary>
    /// Renders the mask the scatter system samples, as an RGBA-channel visualization.
    /// </summary>
    /// <remarks>
    /// Reads <c>VegetationSystemPro.BiomeTextureMask</c>, which is its own reference and is free to
    /// differ from the body's <c>_BiomeMaskTex</c>, so the mask drawn here can be a different texture
    /// from the one the surface material samples.
    ///
    /// The tint alone cannot show which package owns a channel, so the legend section carries channel
    /// to biome to package and is where most of the value is - see <c>PreviewOverlayLegend</c>.
    /// </remarks>
    internal sealed class ScatterBiomePreviewOverlay : PreviewOverlay
    {
        private const string ShaderName = "Redux/PlanetAuthoring/Overlays/MaskOverlay";

        private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");

        private float _strength = 0.7f;

        /// <summary>
        /// Initializes a new scatter biome mask overlay.
        /// </summary>
        public ScatterBiomePreviewOverlay() : base(ShaderName)
        {
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
            VegetationSystemPro system = ScatterSystemLocator.Find(pqs);

            // Black rather than the body's mask when scatter has none. Falling back to the surface
            // mask would show ground the scatter system is not actually gated by.
            Texture2D mask = system != null && system.UseBiomeTextureMask ? system.BiomeTextureMask : null;

            OverlayMaterial.SetTexture(MaskTexId, mask != null ? mask : Texture2D.blackTexture);
            OverlayMaterial.SetFloat(StrengthId, _strength);
        }
    }
}
