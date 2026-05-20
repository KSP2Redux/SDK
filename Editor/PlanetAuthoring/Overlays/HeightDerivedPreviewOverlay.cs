using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Overlays
{
    /// <summary>
    /// Renders slope or altitude-band visualization derived from the per-vertex quad-mesh stream
    /// using the shared <c>Redux/PlanetAuthoring/Overlays/HeightDerivedOverlay</c> shader.
    /// </summary>
    internal sealed class HeightDerivedPreviewOverlay : PreviewOverlay
    {
        /// <summary>
        /// Selects which height-derived visualization the overlay produces.
        /// </summary>
        public enum Source
        {
            /// <summary>
            /// Color the surface by slope steepness.
            /// </summary>
            Slope,

            /// <summary>
            /// Draw altitude contour bands at fixed elevation intervals.
            /// </summary>
            AltitudeBands,
        }

        private const string ShaderName = "Redux/PlanetAuthoring/Overlays/HeightDerivedOverlay";

        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");
        private static readonly int PlanetRadiusId = Shader.PropertyToID("_PlanetRadius");
        private static readonly int BandHeightId = Shader.PropertyToID("_BandHeight");
        private static readonly int SlopeStepDegId = Shader.PropertyToID("_SlopeStepDeg");
        private static readonly int BiomeMaskTexId = Shader.PropertyToID("_BiomeMaskTex");
        private static readonly int LargeGradienceRId = Shader.PropertyToID("_LargeGradienceR");
        private static readonly int LargeGradienceGId = Shader.PropertyToID("_LargeGradienceG");
        private static readonly int LargeGradienceBId = Shader.PropertyToID("_LargeGradienceB");
        private static readonly int LargeGradienceAId = Shader.PropertyToID("_LargeGradienceA");
        private static readonly int MidGradienceRId = Shader.PropertyToID("_MidGradienceR");
        private static readonly int MidGradienceGId = Shader.PropertyToID("_MidGradienceG");
        private static readonly int MidGradienceBId = Shader.PropertyToID("_MidGradienceB");
        private static readonly int MidGradienceAId = Shader.PropertyToID("_MidGradienceA");
        private static readonly int LargeHeightMapUVScalesId = Shader.PropertyToID("_LargeHeightMapUVScales");
        private static readonly int MediumHeightMapUVScalesId = Shader.PropertyToID("_MediumHeightMapUVScales");

        private readonly Source _source;
        private float _strength = 0.7f;
        private float _bandHeight = 500f;
        private float _slopeStepDeg;

        /// <summary>
        /// Initializes a new height-derived overlay in the given mode.
        /// </summary>
        /// <param name="source">Whether the overlay renders slope shading or altitude contour bands.</param>
        public HeightDerivedPreviewOverlay(Source source) : base(ShaderName)
        {
            _source = source;
        }

        /// <summary>
        /// Gets or sets the 0..1 blend strength applied to the overlay color in the shader.
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

        /// <summary>
        /// Gets or sets the altitude-band height in meters used by the contour mode.
        /// </summary>
        public float BandHeightMeters
        {
            get => _bandHeight;
            set
            {
                _bandHeight = Mathf.Max(1f, value);
                OverlayMaterial.SetFloat(BandHeightId, _bandHeight);
            }
        }

        /// <summary>
        /// Gets or sets the slope quantization step in degrees used by the slope mode.
        /// </summary>
        /// <remarks>
        /// Zero disables quantization and produces the continuous green-to-red ramp. Positive
        /// values bin the displayed slope to multiples of the step using the same stretched
        /// scale the runtime and bake trapezoid windows operate on.
        /// </remarks>
        public float SlopeStepDegrees
        {
            get => _slopeStepDeg;
            set
            {
                _slopeStepDeg = Mathf.Clamp(value, 0f, 90f);
                OverlayMaterial.SetFloat(SlopeStepDegId, _slopeStepDeg);
            }
        }

        /// <inheritdoc />
        public override void RefreshBindings(PQS pqs)
        {
            OverlayMaterial.SetFloat(ModeId, _source == Source.Slope ? 0f : 1f);
            OverlayMaterial.SetFloat(StrengthId, _strength);
            OverlayMaterial.SetFloat(BandHeightId, _bandHeight);
            OverlayMaterial.SetFloat(SlopeStepDegId, _slopeStepDeg);

            var radius = 0f;
            var body = BodyResolver.FindBody(pqs);
            if (body?.Data != null)
            {
                radius = (float)body.Data.radius;
            }
            OverlayMaterial.SetFloat(PlanetRadiusId, radius);

            // Slope mode samples the same gradience heightmaps + biome mask the runtime
            // prepass samples (Prepass.cginc:343-346, AccumHeightBiome). Mirror those handles
            // off the surface material so the overlay reads what the trapezoid windows gate
            // against, not a downstream blended normal.
            if (_source == Source.Slope)
            {
                var surface = pqs?.data?.materialSettings?.surfaceMaterial;
                MirrorTexture(surface, BiomeMaskTexId, Texture2D.blackTexture);
                MirrorTexture(surface, LargeGradienceRId, Texture2D.blackTexture);
                MirrorTexture(surface, LargeGradienceGId, Texture2D.blackTexture);
                MirrorTexture(surface, LargeGradienceBId, Texture2D.blackTexture);
                MirrorTexture(surface, LargeGradienceAId, Texture2D.blackTexture);
                MirrorTexture(surface, MidGradienceRId, Texture2D.blackTexture);
                MirrorTexture(surface, MidGradienceGId, Texture2D.blackTexture);
                MirrorTexture(surface, MidGradienceBId, Texture2D.blackTexture);
                MirrorTexture(surface, MidGradienceAId, Texture2D.blackTexture);
                MirrorVector(surface, LargeHeightMapUVScalesId, Vector4.one);
                MirrorVector(surface, MediumHeightMapUVScalesId, Vector4.one);
            }
        }

        private void MirrorTexture(Material surface, int id, Texture fallback)
        {
            var tex = surface != null && surface.HasProperty(id) ? surface.GetTexture(id) : null;
            OverlayMaterial.SetTexture(id, tex != null ? tex : fallback);
        }

        private void MirrorVector(Material surface, int id, Vector4 fallback)
        {
            var v = surface != null && surface.HasProperty(id) ? surface.GetVector(id) : fallback;
            OverlayMaterial.SetVector(id, v);
        }
    }
}
