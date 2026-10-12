using System;
using System.Collections.Generic;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Ocean
{
    /// <summary>
    /// Reads the oceans stock bodies ship and copies them onto a body's own spectrum and material.
    /// </summary>
    /// <remarks>
    /// Stock oceans live on each body's simulation prefab, loaded here through the base-game catalog. Only values are
    /// copied: stock's textures and shader stay in the game's bundles, where a project asset cannot reference them.
    /// </remarks>
    public static class OceanPresets
    {
        /// <summary>
        /// The stock bodies whose wave spectrum can be copied.
        /// </summary>
        public static readonly string[] SPECTRUM_BODIES = { "Kerbin", "Laythe", "Eve" };

        /// <summary>
        /// The stock bodies whose water material can be copied.
        /// </summary>
        /// <remarks>
        /// Vall ships a water material but no spectrum, so its ocean never starts in game. Its material is still a usable
        /// look.
        /// </remarks>
        public static readonly string[] MATERIAL_BODIES = { "Kerbin", "Laythe", "Eve", "Vall" };

        /// <summary>
        /// The flat colour stock Kerbin's scaled albedo paints its sea, sRGB (34, 56, 77), measured from the albedo
        /// where its shoreline mask is sea.
        /// </summary>
        /// <remarks>
        /// Laythe's and Eve's scaled albedos keep their seabed instead of painting a sea, so Kerbin's is the only stock
        /// colour there is to start from.
        /// </remarks>
        public static readonly Color KERBIN_SCALED_OCEAN_COLOR = new(34f / 255f, 56f / 255f, 77f / 255f, 1f);

        /// <summary>
        /// Loads a stock body's PQS renderer and hands it to <paramref name="use" />, releasing it afterwards.
        /// </summary>
        /// <param name="stockBody">The stock body's name.</param>
        /// <param name="use">Reads what it needs from the renderer. It must not keep references to stock assets.</param>
        /// <param name="problem">Why the body could not be loaded, or an empty string on success.</param>
        /// <returns>True if the renderer loaded and <paramref name="use" /> ran, false otherwise.</returns>
        public static bool TryUse(string stockBody, Action<PQSRenderer> use, out string problem)
        {
            if (!EditorPqsBootstrap.EnsureStockCatalog())
            {
                problem = "The base-game catalog is not registered. Run 'ThunderKit > Import Ksp2 To Editor'.";
                return false;
            }

            string key = $"Celestial.{stockBody}.Simulation.prefab";
            AsyncOperationHandle<IList<UnityEngine.ResourceManagement.ResourceLocations.IResourceLocation>> locations =
                Addressables.LoadResourceLocationsAsync(key, typeof(GameObject));
            locations.WaitForCompletion();
            bool found = locations.Result is { Count: > 0 };
            Addressables.Release(locations);
            if (!found)
            {
                problem = $"{stockBody} has no simulation prefab in the base-game catalog.";
                return false;
            }

            AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(key);
            try
            {
                GameObject local = handle.WaitForCompletion();
                PQSRenderer renderer = local != null ? local.GetComponentInChildren<PQSRenderer>(true) : null;
                if (renderer == null)
                {
                    problem = $"{stockBody}'s simulation prefab has no PQS renderer.";
                    return false;
                }

                use(renderer);
                problem = string.Empty;
                return true;
            }
            finally
            {
                Addressables.Release(handle);
            }
        }

        /// <summary>
        /// Copies a stock body's wave spectrum over <paramref name="target" />, keeping its name. Undoable.
        /// </summary>
        /// <param name="target">The spectrum to overwrite.</param>
        /// <param name="stockBody">The stock body, one of <see cref="SPECTRUM_BODIES" />.</param>
        /// <param name="message">A status line describing the outcome or the reason for failure.</param>
        /// <returns>True if the spectrum was copied, false otherwise.</returns>
        public static bool TryApplySpectrum(OceanWaveSpectrum target, string stockBody, out string message)
        {
            if (target == null)
            {
                message = "No spectrum to apply the preset to.";
                return false;
            }

            bool copied = false;
            if (!TryUse(stockBody, renderer =>
                {
                    if (renderer._oceanSpectrum == null)
                        return;

                    Undo.RecordObject(target, $"Apply {stockBody} Spectrum");
                    string name = target.name;
                    EditorUtility.CopySerialized(renderer._oceanSpectrum, target);
                    target.name = name;
                    EditorUtility.SetDirty(target);
                    copied = true;
                }, out message))
                return false;

            message = copied ? $"Applied {stockBody}'s wave spectrum." : $"{stockBody} has no wave spectrum.";
            return copied;
        }

        /// <summary>
        /// Copies a stock body's water material values and keywords onto <paramref name="target" />, keeping its own
        /// textures and shader. Undoable.
        /// </summary>
        /// <param name="target">The material to overwrite.</param>
        /// <param name="stockBody">The stock body, one of <see cref="MATERIAL_BODIES" />.</param>
        /// <param name="message">A status line describing the outcome or the reason for failure.</param>
        /// <returns>True if the material was copied, false otherwise.</returns>
        public static bool TryApplyMaterial(Material target, string stockBody, out string message)
        {
            if (target == null)
            {
                message = "No material to apply the preset to.";
                return false;
            }

            bool copied = false;
            if (!TryUse(stockBody, renderer =>
                {
                    if (renderer.OceanWaterMaterial == null)
                        return;

                    Undo.RecordObject(target, $"Apply {stockBody} Water");
                    CopyValues(renderer.OceanWaterMaterial, target);
                    EditorUtility.SetDirty(target);
                    copied = true;
                }, out message))
                return false;

            message = copied
                ? $"Applied {stockBody}'s water. Your textures are kept, since stock's stay in the game."
                : $"{stockBody} has no water material.";
            return copied;
        }

        /// <summary>
        /// Copies the values of <paramref name="source" /> onto <paramref name="target" />, every number, colour and
        /// vector the target's shader declares, along with the source's keywords.
        /// </summary>
        /// <remarks>
        /// Textures are not copied, so the target keeps its own along with their tiling, and its shader is untouched.
        /// Keywords are copied by name, so they carry over to a target on the SDK's stand-in, which does not declare
        /// them. The renderer's copy on the game's shader brings them back into use.
        /// </remarks>
        /// <param name="source">The material to copy from.</param>
        /// <param name="target">The material to copy onto.</param>
        public static void CopyValues(Material source, Material target)
        {
            Shader shader = target.shader;
            for (int index = 0; index < shader.GetPropertyCount(); index++)
            {
                string name = shader.GetPropertyName(index);
                if (!source.HasProperty(name))
                    continue;

                switch (shader.GetPropertyType(index))
                {
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        target.SetFloat(name, source.GetFloat(name));
                        break;
                    case ShaderPropertyType.Int:
                        target.SetInteger(name, source.GetInteger(name));
                        break;
                    case ShaderPropertyType.Color:
                        target.SetColor(name, source.GetColor(name));
                        break;
                    case ShaderPropertyType.Vector:
                        target.SetVector(name, source.GetVector(name));
                        break;
                }
            }

            target.shaderKeywords = source.shaderKeywords;
        }
    }
}
