using System.Collections.Generic;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Pushes the effective small-layer values from a <see cref="PQSDataAuthoring" />'s slot array into the corresponding shader properties on a surface <see cref="Material" />.
    /// </summary>
    /// <remarks>
    /// Called from the per-cell editor after every slot/SO field edit and from
    /// <c>SmallLayerMaterialPostProcessor</c> when a <see cref="SmallLayerMaterial" /> asset is saved.
    /// Per-(biome, layer) Color properties (<c>_SmallTint{c}{i}</c>, <c>_SmallEmissionColor{c}{i}</c>)
    /// are written one field at a time. Per-biome Vector4 properties whose channels index by layer
    /// (<c>_SmallUVScale{c}</c>, <c>_SmallBrightness{c}</c>, etc.) are aggregated across all four
    /// slots of the biome and written once.
    /// </remarks>
    public static class SmallLayerMaterialCompiler
    {
        // Debounce queue: editor edits typically fire many times per second during a slider drag.
        // RequestCompile collapses N requests against the same (authoring, material) pair into one
        // Compile run on the next editor tick. Compile itself stays cheap and idempotent.
        private static readonly HashSet<(PQSDataAuthoring authoring, Material material)> _pending = new();
        private static bool _flushScheduled;

        /// <summary>
        /// Schedules a <see cref="Compile" /> for the (<paramref name="authoring" />, <paramref name="material" />) pair on the next editor tick. Multiple requests in the same frame coalesce into one Compile run.
        /// </summary>
        public static void RequestCompile(PQSDataAuthoring authoring, Material material)
        {
            if (authoring == null || material == null) return;
            _pending.Add((authoring, material));
            if (_flushScheduled) return;
            _flushScheduled = true;
            EditorApplication.delayCall += Flush;
        }

        private static void Flush()
        {
            _flushScheduled = false;
            if (_pending.Count == 0) return;
            // Snapshot first - Compile writes can trigger more callbacks that re-enter RequestCompile.
            var snapshot = new (PQSDataAuthoring, Material)[_pending.Count];
            _pending.CopyTo(snapshot);
            _pending.Clear();
            foreach (var (auth, mat) in snapshot)
            {
                if (auth == null || mat == null) continue;
                Compile(auth, mat);
            }
        }

        /// <summary>
        /// Pushes effective numeric small-layer values from <paramref name="authoring" /> into <paramref name="material" />. Does not repack texture arrays - call <see cref="Texture2DArrayPacker.RepackSmallTiles" /> separately when texture-override state changes.
        /// </summary>
        /// <param name="authoring">The authoring sidecar carrying the small-layer slots.</param>
        /// <param name="material">The body's surface material that receives the compiled shader properties.</param>
        public static void Compile(PQSDataAuthoring authoring, Material material)
        {
            if (authoring == null || material == null) return;
            if (authoring.smallLayerSlots == null || authoring.smallLayerSlots.Length != 16) return;

            // No Undo.RecordObject here - the material is derived from the slot state, and the
            // slot edit that triggered this Compile already records its own undo on the authoring
            // sidecar. Recording the material on every Compile would also balloon the undo stack
            // to one entry per slider tick.

            for (int b = 0; b < 4; b++)
            {
                string c = PlanetAuthoringNaming.BiomeChannels[b];

                Vector4 uvScale = default, uvOffset = default;
                Vector4 brightness = default, contrast = default, saturation = default;
                Vector4 normalStrength = default, glossStrength = default, metallicStrength = default, aoStrength = default;
                Vector4 emissionStrength = default;

                for (int l = 0; l < 4; l++)
                {
                    var slot = authoring.smallLayerSlots[PlanetAuthoringNaming.CellIndex(b, l)];
                    if (slot == null) continue;

                    uvScale[l] = slot.EffectiveUVScale;
                    uvOffset[l] = slot.EffectiveUVOffset;
                    brightness[l] = slot.EffectiveBrightness;
                    contrast[l] = slot.EffectiveContrast;
                    saturation[l] = slot.EffectiveSaturation;
                    normalStrength[l] = slot.EffectiveNormalStrength;
                    glossStrength[l] = slot.EffectiveGlossStrength;
                    metallicStrength[l] = slot.EffectiveMetallicStrength;
                    aoStrength[l] = slot.EffectiveAOStrength;
                    emissionStrength[l] = slot.EffectiveEmissionStrength;

                    int i = l + 1;
                    material.SetColor($"_SmallTint{c}{i}", slot.EffectiveTint);
                    material.SetColor($"_SmallEmissionColor{c}{i}", slot.EffectiveEmissionColor);
                }

                material.SetVector($"_SmallUVScale{c}", uvScale);
                material.SetVector($"_SmallUVOffset{c}", uvOffset);
                material.SetVector($"_SmallBrightness{c}", brightness);
                material.SetVector($"_SmallContrast{c}", contrast);
                material.SetVector($"_SmallSaturation{c}", saturation);
                material.SetVector($"_SmallNormalStrength{c}", normalStrength);
                material.SetVector($"_SmallGlossStrength{c}", glossStrength);
                material.SetVector($"_SmallMetallicStrength{c}", metallicStrength);
                material.SetVector($"_SmallAOStrength{c}", aoStrength);
                material.SetVector($"_SmallEmissionStrength{c}", emissionStrength);
            }

            EditorUtility.SetDirty(material);
        }
    }
}
