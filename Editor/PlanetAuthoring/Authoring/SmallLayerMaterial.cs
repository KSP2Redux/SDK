using Ksp2UnityTools.Editor.API;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Shared visual-identity defaults for a small biome layer, reused across bodies.
    /// </summary>
    /// <remarks>
    /// One asset per material identity (Rock, Sand, Ice, etc.). Drop it into a small-layer slot on a
    /// body to pull the textures, color grading, PBR strengths, emission, and UV tiling along for the
    /// ride. The slot can override any individual field per-body. Where the material gets applied on
    /// the surface (altitude/slope windows, gradience weights, distance-resample tier, weight) stays
    /// on the body's surface Material.
    /// </remarks>
    public class SmallLayerMaterial : ScriptableObject
    {
        [MenuItem("Assets/Redux SDK/Planet Authoring/Small Layer Material", priority = KSP2UnityTools.MenuPriority + 2)]
        private static void CreateSmallLayerMaterial()
        {
            KSP2UnityTools.CreateKsp2UnityToolsAssetAtSelectedPath<SmallLayerMaterial>("New Small Layer Material");
        }

        /// <summary>
        /// Albedo (color) tile texture.
        /// </summary>
        public Texture2D AlbedoTexture;

        /// <summary>
        /// Normal+packed tile texture. RGBA encodes (metallic-influence, normalY, AO, normalX) DXT5nm-style.
        /// </summary>
        public Texture2D NormalTexture;

        /// <summary>
        /// Metallic mask tile texture. Sampled R channel is the per-tile metallic value.
        /// </summary>
        public Texture2D MetallicTexture;

        /// <summary>
        /// Per-layer UV scale. Larger value prints the tile smaller on the surface.
        /// </summary>
        [Range(0.1f, 16f)] public float UVScale = 1f;

        /// <summary>
        /// Per-layer UV offset. Breaks alignment between layers sharing the same tile.
        /// </summary>
        [Range(-1f, 1f)] public float UVOffset = 0f;

        /// <summary>
        /// Per-layer tint color. RGB multiplies the layer's albedo, alpha multiplies its height-blend alpha.
        /// </summary>
        public Color Tint = Color.white;

        /// <summary>
        /// Additive brightness applied before contrast and saturation.
        /// </summary>
        [Range(-1f, 1f)] public float Brightness = 0f;

        /// <summary>
        /// Contrast multiplier around mid-gray. 1 is neutral.
        /// </summary>
        [Range(0f, 2f)] public float Contrast = 1f;

        /// <summary>
        /// Saturation multiplier. 0 is grayscale, 1 is neutral.
        /// </summary>
        [Range(0f, 2f)] public float Saturation = 1f;

        /// <summary>
        /// Multiplier on the normal map. 0 is flat, 1 is neutral.
        /// </summary>
        [Range(0f, 2f)] public float NormalStrength = 1f;

        /// <summary>
        /// Smoothness multiplier. Values at or above 15 switch the shader to override mode.
        /// </summary>
        [Range(0f, 16f)] public float GlossStrength = 1f;

        /// <summary>
        /// Metallic multiplier. Values at or above 15 switch the shader to override mode.
        /// </summary>
        [Range(0f, 16f)] public float MetallicStrength = 1f;

        /// <summary>
        /// Ambient-occlusion power.
        /// </summary>
        [Range(0f, 2f)] public float AOStrength = 1f;

        /// <summary>
        /// Self-illumination multiplier. 0 disables emission.
        /// </summary>
        [Range(0f, 50f)] public float EmissionStrength = 0f;

        /// <summary>
        /// Per-layer emission color, HDR.
        /// </summary>
        public Color EmissionColor = Color.black;
    }
}
