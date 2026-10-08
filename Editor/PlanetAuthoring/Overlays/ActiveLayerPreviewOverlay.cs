using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Overlays
{
    /// <summary>
    /// Renders the "which of the 16 small-biome layers wins per pixel" visualization by sampling
    /// the surface shader's already-computed prepass weights (_LocalSpacePrepassTex0..3) and the
    /// body's biome mask, then colorizing the argmax.
    /// </summary>
    /// <remarks>
    /// The prepass RTs are screen-space textures written by the surface shader earlier in the same
    /// frame. PQSRenderer sets them on the surface material directly. This overlay mirrors the
    /// same handles onto its own material so a single sample suffices in the fragment.
    /// </remarks>
    internal sealed class ActiveLayerPreviewOverlay : PreviewOverlay
    {
        private const string ShaderName = "Redux/PlanetAuthoring/Overlays/ActiveLayerOverlay";

        private static readonly int Prepass0Id = Shader.PropertyToID("_LocalSpacePrepassTex0");
        private static readonly int Prepass1Id = Shader.PropertyToID("_LocalSpacePrepassTex1");
        private static readonly int Prepass2Id = Shader.PropertyToID("_LocalSpacePrepassTex2");
        private static readonly int Prepass3Id = Shader.PropertyToID("_LocalSpacePrepassTex3");
        private static readonly int BiomeMaskId = Shader.PropertyToID("_BiomeMaskTex");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");
        private static readonly int LayerEnableRId = Shader.PropertyToID("_LayerEnableR");
        private static readonly int LayerEnableGId = Shader.PropertyToID("_LayerEnableG");
        private static readonly int LayerEnableBId = Shader.PropertyToID("_LayerEnableB");
        private static readonly int LayerEnableAId = Shader.PropertyToID("_LayerEnableA");

        private float _strength = 0.7f;
        private int _layerEnableMask = 0xFFFF;

        /// <summary>
        /// Initializes a new active-layer overlay.
        /// </summary>
        public ActiveLayerPreviewOverlay() : base(ShaderName)
        {
        }

        /// <summary>
        /// Gets or sets the 0..1 blend strength applied to the colorized winner in the shader.
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
        /// Gets or sets the 16-bit per-(biome, layer) enable mask.
        /// </summary>
        /// <remarks>
        /// Bit (biome * 4 + layer) gates whether that pair contributes to the argmax. Bits outside
        /// the low 16 are masked off on assignment.
        /// </remarks>
        public int LayerEnableMask
        {
            get => _layerEnableMask;
            set
            {
                _layerEnableMask = value & 0xFFFF;
                ApplyEnableMask();
            }
        }

        /// <inheritdoc />
        public override void RefreshBindings(PQS pqs)
        {
            OverlayMaterial.SetFloat(StrengthId, _strength);
            ApplyEnableMask();

            var surface = pqs?.data?.materialSettings?.surfaceMaterial;
            if (surface == null)
            {
                OverlayMaterial.SetTexture(Prepass0Id, Texture2D.blackTexture);
                OverlayMaterial.SetTexture(Prepass1Id, Texture2D.blackTexture);
                OverlayMaterial.SetTexture(Prepass2Id, Texture2D.blackTexture);
                OverlayMaterial.SetTexture(Prepass3Id, Texture2D.blackTexture);
                OverlayMaterial.SetTexture(BiomeMaskId, Texture2D.blackTexture);
                return;
            }

            CopyTexture(surface, OverlayMaterial, Prepass0Id);
            CopyTexture(surface, OverlayMaterial, Prepass1Id);
            CopyTexture(surface, OverlayMaterial, Prepass2Id);
            CopyTexture(surface, OverlayMaterial, Prepass3Id);
            CopyTexture(surface, OverlayMaterial, BiomeMaskId);
        }

        private void ApplyEnableMask()
        {
            OverlayMaterial.SetVector(LayerEnableRId, BiomeVector(0));
            OverlayMaterial.SetVector(LayerEnableGId, BiomeVector(1));
            OverlayMaterial.SetVector(LayerEnableBId, BiomeVector(2));
            OverlayMaterial.SetVector(LayerEnableAId, BiomeVector(3));
        }

        private Vector4 BiomeVector(int biome)
        {
            return new Vector4(
                (_layerEnableMask & (1 << PlanetAuthoringNaming.CellIndex(biome, 0))) != 0 ? 1f : 0f,
                (_layerEnableMask & (1 << PlanetAuthoringNaming.CellIndex(biome, 1))) != 0 ? 1f : 0f,
                (_layerEnableMask & (1 << PlanetAuthoringNaming.CellIndex(biome, 2))) != 0 ? 1f : 0f,
                (_layerEnableMask & (1 << PlanetAuthoringNaming.CellIndex(biome, 3))) != 0 ? 1f : 0f);
        }

        private static void CopyTexture(Material from, Material to, int propertyId)
        {
            var tex = from.HasProperty(propertyId) ? from.GetTexture(propertyId) : null;
            to.SetTexture(propertyId, tex != null ? tex : Texture2D.blackTexture);
        }
    }
}
