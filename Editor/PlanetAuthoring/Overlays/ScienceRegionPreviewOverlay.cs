using KSP;
using KSP.Game.Science;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Overlays
{
    /// <summary>
    /// Editor-side mirror of the runtime <c>PQSScienceOverlay</c>. Renders the body's science region
    /// data either as the post-bake colorized palette (what the runtime sees) or as the raw source
    /// texture (what the artist is authoring), so artists can confirm the source matches the bake.
    /// </summary>
    /// <remarks>
    /// Resolves the active body's <see cref="ScienceRegionData" /> via
    /// <see cref="ScienceRegionAssetLocator" />. The baked-palette texture is built and cached on
    /// demand. Switching mode or session swaps it. Same shader the runtime overlay uses
    /// (<c>KSP2/Environment/CelestialBody/CelestialBody_Local_Overlay</c>), no new shader needed.
    /// </remarks>
    internal sealed class ScienceRegionPreviewOverlay : PreviewOverlay
    {
        /// <summary>
        /// Selects which science region texture the overlay displays.
        /// </summary>
        public enum Mode
        {
            /// <summary>
            /// Show the post-bake colorized palette (what the runtime samples).
            /// </summary>
            BakedPalette,

            /// <summary>
            /// Show the raw source texture (what the artist is currently authoring).
            /// </summary>
            SourceTexture,
        }

        private const string ShaderName = "Redux/PlanetAuthoring/Overlays/ScienceRegionOverlay";

        private static readonly int OverlayTextureId = Shader.PropertyToID("_OverlayTexture");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");

        private float _strength = 0.7f;
        private Mode _mode = Mode.BakedPalette;
        private Texture2D _bakedPaletteTex;
        private CelestialBodyBakedScienceRegionMap _bakedMapForCachedTex;

        // Surfaced to the manager / legend so the UI can show a stale-bake warning when set.
        /// <summary>
        /// Gets whether the source texture is newer than the cached bake.
        /// </summary>
        public bool IsBakeStale { get; private set; }

        /// <summary>
        /// Gets whether a <see cref="ScienceRegionData" /> asset was found for the active body.
        /// </summary>
        public bool HasScienceData { get; private set; }

        /// <summary>
        /// Gets whether a baked region map asset was found for the active body.
        /// </summary>
        public bool HasBakedMap { get; private set; }

        /// <summary>
        /// Initializes a new science region overlay.
        /// </summary>
        public ScienceRegionPreviewOverlay() : base(ShaderName)
        {
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
        /// Gets or sets which texture the overlay displays.
        /// </summary>
        /// <remarks>
        /// Setting this only updates the cached mode. Call <see cref="RefreshBindings" /> (or have
        /// the manager call it) to re-bind the texture on the material.
        /// </remarks>
        public Mode CurrentMode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
            }
        }

        /// <inheritdoc />
        public override void RefreshBindings(PQS pqs)
        {
            OverlayMaterial.SetFloat(StrengthId, _strength);

            var body = BodyResolver.FindBody(pqs);
            var bodyName = body?.Data?.bodyName;
            var data = ScienceRegionAssetLocator.FindForBody(bodyName);
            HasScienceData = data != null;

            var baked = ScienceRegionAssetLocator.FindBakedMap(data);
            HasBakedMap = baked != null;
            IsBakeStale = ScienceRegionAssetLocator.IsBakeStale(data, baked);

            Texture overlayTex = _mode switch
            {
                Mode.SourceTexture => data?.scienceRegionMap,
                _                  => GetOrBuildPaletteTexture(baked),
            };
            OverlayMaterial.SetTexture(OverlayTextureId, overlayTex != null ? overlayTex : Texture2D.blackTexture);
        }

        /// <summary>
        /// Releases the underlying material and the cached baked-palette texture.
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            DestroyPaletteTexture();
        }

        private Texture GetOrBuildPaletteTexture(CelestialBodyBakedScienceRegionMap baked)
        {
            if (baked == null || baked.MapData == null) return null;
            if (_bakedPaletteTex != null && _bakedMapForCachedTex == baked) return _bakedPaletteTex;

            DestroyPaletteTexture();
            var width = Mathf.Max(1, baked.Width);
            var height = Mathf.Max(1, baked.Height);
            var expected = width * height;
            if (baked.MapData.Length < expected) return null;

            _bakedPaletteTex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                name = $"PreviewOverlay_BakedPalette_{baked.BodyName}",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[expected];
            var paletteLen = ScienceRegionsHelper.ScienceRegionsVisualizationPalette.Length;
            for (var i = 0; i < expected; i++)
            {
                var idx = Mathf.Clamp(baked.MapData[i], 0, paletteLen - 1);
                pixels[i] = ScienceRegionsHelper.ScienceRegionsVisualizationPalette[idx];
            }
            _bakedPaletteTex.SetPixelData(pixels, 0);
            _bakedPaletteTex.Apply();
            _bakedMapForCachedTex = baked;
            return _bakedPaletteTex;
        }

        private void DestroyPaletteTexture()
        {
            if (_bakedPaletteTex != null)
            {
                Object.DestroyImmediate(_bakedPaletteTex);
                _bakedPaletteTex = null;
            }
            _bakedMapForCachedTex = null;
        }
    }
}
