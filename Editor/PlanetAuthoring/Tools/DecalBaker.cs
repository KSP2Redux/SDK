using System.Collections.Generic;
using System.Reflection;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using UniLinq;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Builds <see cref="PQSDecalData" /> for a body from the per-decal texture references on its
    /// <see cref="PQSDecal" /> templates.
    /// </summary>
    /// <remarks>
    /// Texture2DArrays (Diffuse / Normal / AlphaMask / Peak / Slope) are built one slice per decal
    /// using the decal's matching field. Decals with a missing field for a given array contribute
    /// a black slice. The shared HeightData / AlphaData R16 textures come from the controller's
    /// SharedHeightmap and SharedAlphaMap - one per body, since the runtime has no per-instance
    /// height-region index.
    /// </remarks>
    public static class DecalBaker
    {
        // Coalesces auto-bake requests from DecalAutoBaker (template asset changes) and
        // DecalInstanceAutoBaker (instance edits / deletes) so back-to-back triggers produce one
        // bake instead of two.
        private static bool _bakeQueued;
        private static bool _surfaceJobReflectionWarned;
        private static bool _nativeCacheReflectionWarned;

        /// <summary>
        /// Queues a rebuild on the next editor tick, deduplicating concurrent requests.
        /// </summary>
        /// <remarks>
        /// Both auto-bakers route through this so a same-tick combination of "template asset changed"
        /// plus "instance edited" produces a single rebuild. Defers via delayCall so any in-flight
        /// PQS subdivision job releases the NativeArrays before disposal.
        /// </remarks>
        /// <param name="controller">The decal controller whose data should be re-baked.</param>
        public static void QueueRebuild(PQSDecalController controller)
        {
            if (controller == null || _bakeQueued) return;
            _bakeQueued = true;
            EditorApplication.delayCall += () =>
            {
                _bakeQueued = false;
                if (controller != null)
                {
                    RebuildForController(controller);
                }
            };
        }

        /// <summary>
        /// Rebuilds the body's <see cref="PQSDecalData" /> from the templates currently used by instances on <paramref name="controller" />.
        /// </summary>
        /// <remarks>
        /// Saves the asset and refreshes the runtime.
        /// </remarks>
        /// <param name="controller">The decal controller whose body data to rebuild.</param>
        public static void RebuildForController(PQSDecalController controller)
        {
            if (controller == null || controller.PqsDecalData == null) return;

            var templates = CollectTemplates(controller);
            var data = controller.PqsDecalData;
            data.BakedPqsDecalList = templates;
            data.BakedPqsDecalIDList = templates.Select(t => t.DecalID).ToList();
            data.Count = templates.Count;
            var templateAuthorings = templates
                .Select(AuthoringSidecars.GetOrCreate)
                .ToList();
            // Empty-template case: leave existing texture-array sub-assets in place but null the
            // references on the PQSDecalData so the runtime treats it as "nothing baked". Without
            // this, removing all decals would leave the bake hash drifted and stuck on the validator.
            if (templates.Count == 0)
            {
                data.DiffuseTextureArray = null;
                data.NormalTextureArray = null;
                data.AlphaMaskTextureArray = null;
                data.PeakTextureArray = null;
                data.SlopeTextureArray = null;
            }
            else
            {
                data.DiffuseTextureArray = ReplaceSubAsset(data, data.DiffuseTextureArray, BuildArray(templates, templateAuthorings, a => a.Diffuse, "Diffuse", linear: false), "DiffuseTextureArray");
                data.NormalTextureArray = ReplaceSubAsset(data, data.NormalTextureArray, BuildArray(templates, templateAuthorings, a => a.Normal, "Normal", linear: true), "NormalTextureArray");
                data.AlphaMaskTextureArray = ReplaceSubAsset(data, data.AlphaMaskTextureArray, BuildArray(templates, templateAuthorings, a => a.AlphaMaskTexture, "AlphaMask", linear: true), "AlphaMaskTextureArray");
                data.PeakTextureArray = ReplaceSubAsset(data, data.PeakTextureArray, BuildArray(templates, templateAuthorings, a => a.Peak, "Peak", linear: false), "PeakTextureArray");
                data.SlopeTextureArray = ReplaceSubAsset(data, data.SlopeTextureArray, BuildArray(templates, templateAuthorings, a => a.Slope, "Slope", linear: false), "SlopeTextureArray");
            }

            (data.HeightData, data.HeightWidth, data.HeightHeight) = GetR16(controller.SharedHeightmap);
            (data.AlphaData, data.AlphaWidth, data.AlphaHeight) = GetR16(controller.SharedAlphaMap);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(data));
            // PQS may have an in-flight UpdateSubdivisions job reading the cached NativeArrays. Force-complete it before disposal or the safety system throws.
            CompletePendingSurfaceJob(controller.Pqs);
            // Drop the controller's cached native arrays so the next GetNativeHeights / GetNativeAlpha rebuilds from the freshly-baked data. RefreshDecalInstances does not invalidate them on its own.
            InvalidateNativeCache(controller, "_nativeHeight");
            InvalidateNativeCache(controller, "_nativeAlpha");
            controller.RefreshDecalInstances();

            // Snapshot the input hash on the editor-only sidecar so the validator can detect when a re-bake is needed.
            var bakeAuthoring = AuthoringSidecars.GetOrCreate(data);
            if (bakeAuthoring != null)
            {
                bakeAuthoring.LastBakeHash = PQSDecalBakeHash.Compute(controller);
                EditorUtility.SetDirty(bakeAuthoring);
                AssetDatabase.SaveAssetIfDirty(bakeAuthoring);
            }
        }

        /// <summary>
        /// Force-completes any in-flight <c>PQS._surfaceJob</c> via reflection so its
        /// <c>NativeArray</c> consumers can be disposed without tripping the safety check.
        /// </summary>
        /// <remarks>
        /// Used both by the bake pass (before swapping the controller's native arrays) and by
        /// <see cref="PlanetAuthoringSession.End" /> (before <c>PQSDecalController.OnDisable</c>
        /// disposes its native arrays, which the in-flight subdivision job is still reading).
        /// </remarks>
        /// <param name="planet">The PQS whose subdivision job should be completed.</param>
        public static void CompletePendingSurfaceJob(PQS planet)
        {
            if (planet == null) return;
            var jobField = typeof(PQS).GetField("_surfaceJob", BindingFlags.NonPublic | BindingFlags.Instance);
            if (jobField == null)
            {
                if (!_surfaceJobReflectionWarned)
                {
                    Debug.LogWarning("[DecalBaker] PQS._surfaceJob private field not found via reflection. Decal bake will not wait for in-flight subdivision jobs to complete. Likely renamed; update DecalBaker.CompletePendingSurfaceJob.");
                    _surfaceJobReflectionWarned = true;
                }
                return;
            }
            var holder = jobField.GetValue(planet);
            if (holder == null) return;
            // _surfaceJob is a PQJob.PQSJobHolder struct with a JobHandle named "handle". Complete it directly so any in-flight UpdateSubdivisions read finishes before we dispose the NativeArrays it referenced.
            var handleField = holder.GetType().GetField("handle");
            if (handleField == null) return;
            var handle = (JobHandle)handleField.GetValue(holder);
            handle.Complete();
        }

        private static void InvalidateNativeCache(PQSDecalController controller, string fieldName)
        {
            var field = typeof(PQSDecalController).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                if (!_nativeCacheReflectionWarned)
                {
                    Debug.LogWarning($"[DecalBaker] PQSDecalController.{fieldName} private field not found via reflection. Cached native arrays will not be invalidated; stale decal heights may persist until domain reload. Likely renamed; update DecalBaker.InvalidateNativeCache.");
                    _nativeCacheReflectionWarned = true;
                }
                return;
            }
            var current = (NativeArray<ushort>)field.GetValue(controller);
            if (current.IsCreated)
            {
                current.Dispose();
            }
            field.SetValue(controller, default(NativeArray<ushort>));
        }

        private static List<PQSDecal> CollectTemplates(PQSDecalController controller)
        {
            var seen = new HashSet<PQSDecal>();
            var ordered = new List<PQSDecal>();
            if (controller.PqsDecalInstanceList == null) return ordered;
            foreach (var inst in controller.PqsDecalInstanceList)
            {
                if (inst == null || inst.PQSDecal == null) continue;
                if (seen.Add(inst.PQSDecal))
                {
                    ordered.Add(inst.PQSDecal);
                }
            }
            return ordered;
        }

        private static Texture2DArray ReplaceSubAsset(PQSDecalData host, Texture2DArray existing, Texture2DArray newArray, string name)
        {
            // Texture2DArrays built in code aren't asset-database tracked. Without registering them as sub-assets of the host PQSDecalData they show "Type mismatch" in the inspector and don't survive serialization.
            if (existing != null && AssetDatabase.IsSubAsset(existing))
            {
                Object.DestroyImmediate(existing, true);
            }
            if (newArray != null)
            {
                newArray.name = name;
                AssetDatabase.AddObjectToAsset(newArray, host);
            }
            return newArray;
        }

        private static Texture2DArray BuildArray(List<PQSDecal> templates, List<PQSDecalTemplateAuthoring> authorings, System.Func<PQSDecalTemplateAuthoring, Texture2D> selector, string label, bool linear)
        {
            // Resolve per-template source textures from each template's authoring sidecar.
            var sources = new Texture2D[templates.Count];
            Texture2D reference = null;
            for (var i = 0; i < templates.Count; i++)
            {
                if (authorings[i] == null) continue;
                sources[i] = selector(authorings[i]);
                if (reference == null && sources[i] != null)
                {
                    reference = sources[i];
                }
            }
            if (reference == null) return null;
            var width = reference.width;
            var height = reference.height;
            var format = reference.format;
            var mipCount = reference.mipmapCount;
            var array = new Texture2DArray(width, height, templates.Count, format, mipCount > 1, linear);
            int arrayMipCount = array.mipmapCount;
            for (var i = 0; i < templates.Count; i++)
            {
                var tex = sources[i];
                if (tex == null) continue;
                if (tex.width != width || tex.height != height || tex.format != format)
                {
                    Debug.LogWarning($"[DecalBaker] {label} for decal '{templates[i].name}' has different format/size than reference; skipping slice {i}.");
                    continue;
                }
                for (var m = 0; m < arrayMipCount; m++)
                    Graphics.CopyTexture(tex, 0, m, array, i, m);
            }
            array.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return array;
        }

        private static (ushort[] data, int width, int height) GetR16(Texture2D texture16)
        {
            if (texture16 == null) return (System.Array.Empty<ushort>(), 0, 0);
            // R16 stores one ushort per pixel. PNGs and most other imported textures default to RGBA32 (2 ushorts per pixel) or have mip chains, both of which inflate the raw buffer past width*height. The compute shader expects exactly width*height ushorts.
            if (texture16.format != TextureFormat.R16)
            {
                Debug.LogWarning($"[DecalBaker] Texture '{texture16.name}' is {texture16.format}, not R16. Set the texture's import format to R16 (Texture Type: Single Channel, Channel: Red, Format: R16) for correct decal height/alpha baking. Skipping.");
                return (System.Array.Empty<ushort>(), 0, 0);
            }
            var expected = texture16.width * texture16.height;
            var rawData = texture16.GetRawTextureData<ushort>();
            var data = new ushort[expected];
            var copyLen = System.Math.Min(rawData.Length, expected);
            NativeArray<ushort>.Copy(rawData, 0, data, 0, copyLen);
            return (data, texture16.width, texture16.height);
        }
    }
}
