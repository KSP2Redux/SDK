using System;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Per-body slot wrapping a <see cref="SmallLayerMaterial" /> reference with per-field override toggles, mirroring the PQSDecal / PQSDecalInstance template+override pattern.
    /// </summary>
    /// <remarks>
    /// Stored as <c>smallLayerSlots[16]</c> on <see cref="PQSDataAuthoring" />, indexed by
    /// <c>PqsAuthoringNaming.CellIndex(biome, layer)</c>. For each SO field, the slot carries an
    /// <c>OverrideX</c> bool plus a local <c>X</c> value. When the override flag is on (or
    /// <see cref="Material" /> is null), the slot's local value wins. Otherwise the SO's value applies.
    /// The <c>EffectiveX</c> accessors do that resolution.
    ///
    /// Per-body parameters (height window, slope window, master weight, gradience weights, distance
    /// resample tier, subzone overrides) do NOT live here. They stay on the body's surface Material
    /// as they always have.
    /// </remarks>
    [Serializable]
    public class SmallLayerSlot
    {
        /// <summary>
        /// Reference to the shared <see cref="SmallLayerMaterial" /> template, or null when the slot's local values apply directly.
        /// </summary>
        public SmallLayerMaterial Material;

        public bool OverrideAlbedoTexture;
        public Texture2D AlbedoTexture;

        public bool OverrideNormalTexture;
        public Texture2D NormalTexture;

        public bool OverrideMetallicTexture;
        public Texture2D MetallicTexture;

        public bool OverrideUVScale;
        [Range(0.1f, 16f)] public float UVScale = 1f;

        public bool OverrideUVOffset;
        [Range(-1f, 1f)] public float UVOffset = 0f;

        public bool OverrideTint;
        public Color Tint = Color.white;

        public bool OverrideBrightness;
        [Range(-1f, 1f)] public float Brightness = 0f;

        public bool OverrideContrast;
        [Range(0f, 2f)] public float Contrast = 1f;

        public bool OverrideSaturation;
        [Range(0f, 2f)] public float Saturation = 1f;

        public bool OverrideNormalStrength;
        [Range(0f, 2f)] public float NormalStrength = 1f;

        public bool OverrideGlossStrength;
        [Range(0f, 16f)] public float GlossStrength = 1f;

        public bool OverrideMetallicStrength;
        [Range(0f, 16f)] public float MetallicStrength = 1f;

        public bool OverrideAOStrength;
        [Range(0f, 2f)] public float AOStrength = 1f;

        public bool OverrideEmissionStrength;
        [Range(0f, 50f)] public float EmissionStrength = 0f;

        public bool OverrideEmissionColor;
        public Color EmissionColor = Color.black;

        /// <summary>
        /// Clears every override flag and restores every local value to its default. Called when the slot's <see cref="Material" /> is removed so the slot returns to a clean blank state instead of keeping stale per-field overrides around.
        /// </summary>
        public void ResetLocals()
        {
            OverrideAlbedoTexture = false; AlbedoTexture = null;
            OverrideNormalTexture = false; NormalTexture = null;
            OverrideMetallicTexture = false; MetallicTexture = null;
            OverrideUVScale = false; UVScale = 1f;
            OverrideUVOffset = false; UVOffset = 0f;
            OverrideTint = false; Tint = Color.white;
            OverrideBrightness = false; Brightness = 0f;
            OverrideContrast = false; Contrast = 1f;
            OverrideSaturation = false; Saturation = 1f;
            OverrideNormalStrength = false; NormalStrength = 1f;
            OverrideGlossStrength = false; GlossStrength = 1f;
            OverrideMetallicStrength = false; MetallicStrength = 1f;
            OverrideAOStrength = false; AOStrength = 1f;
            OverrideEmissionStrength = false; EmissionStrength = 0f;
            OverrideEmissionColor = false; EmissionColor = Color.black;
        }

        public Texture2D EffectiveAlbedoTexture => (OverrideAlbedoTexture || Material == null) ? AlbedoTexture : Material.AlbedoTexture;
        public Texture2D EffectiveNormalTexture => (OverrideNormalTexture || Material == null) ? NormalTexture : Material.NormalTexture;
        public Texture2D EffectiveMetallicTexture => (OverrideMetallicTexture || Material == null) ? MetallicTexture : Material.MetallicTexture;
        public float EffectiveUVScale => (OverrideUVScale || Material == null) ? UVScale : Material.UVScale;
        public float EffectiveUVOffset => (OverrideUVOffset || Material == null) ? UVOffset : Material.UVOffset;
        public Color EffectiveTint => (OverrideTint || Material == null) ? Tint : Material.Tint;
        public float EffectiveBrightness => (OverrideBrightness || Material == null) ? Brightness : Material.Brightness;
        public float EffectiveContrast => (OverrideContrast || Material == null) ? Contrast : Material.Contrast;
        public float EffectiveSaturation => (OverrideSaturation || Material == null) ? Saturation : Material.Saturation;
        public float EffectiveNormalStrength => (OverrideNormalStrength || Material == null) ? NormalStrength : Material.NormalStrength;
        public float EffectiveGlossStrength => (OverrideGlossStrength || Material == null) ? GlossStrength : Material.GlossStrength;
        public float EffectiveMetallicStrength => (OverrideMetallicStrength || Material == null) ? MetallicStrength : Material.MetallicStrength;
        public float EffectiveAOStrength => (OverrideAOStrength || Material == null) ? AOStrength : Material.AOStrength;
        public float EffectiveEmissionStrength => (OverrideEmissionStrength || Material == null) ? EmissionStrength : Material.EmissionStrength;
        public Color EffectiveEmissionColor => (OverrideEmissionColor || Material == null) ? EmissionColor : Material.EmissionColor;
    }
}
