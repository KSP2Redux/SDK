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
        private static readonly int Prepass4Id = Shader.PropertyToID("_LocalSpacePrepassTex4");

        private readonly Source _source;
        private float _strength = 0.7f;
        private float _bandHeight = 500f;

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

        /// <inheritdoc />
        public override void RefreshBindings(PQS pqs)
        {
            OverlayMaterial.SetFloat(ModeId, _source == Source.Slope ? 0f : 1f);
            OverlayMaterial.SetFloat(StrengthId, _strength);
            OverlayMaterial.SetFloat(BandHeightId, _bandHeight);

            var radius = 0f;
            var body = BodyResolver.FindBody(pqs);
            if (body?.Data != null)
            {
                radius = (float)body.Data.radius;
            }
            OverlayMaterial.SetFloat(PlanetRadiusId, radius);

            // Slope mode samples the surface shader's prepass world-normal RT (LARGE + MID
            // heightmap normals folded in). Mirror that texture handle off the surface material.
            if (_source == Source.Slope)
            {
                var surface = pqs?.data?.materialSettings?.surfaceMaterial;
                var prepass4 = surface != null && surface.HasProperty(Prepass4Id)
                    ? surface.GetTexture(Prepass4Id)
                    : null;
                OverlayMaterial.SetTexture(Prepass4Id, prepass4 != null ? prepass4 : Texture2D.blackTexture);
            }
        }
    }
}
