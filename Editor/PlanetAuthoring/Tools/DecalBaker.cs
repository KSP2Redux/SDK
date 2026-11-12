using System;
using System.Collections.Generic;
using System.Reflection;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Builds <see cref="PQSDecalData" /> for a body from the per-decal texture references on its
    /// <see cref="PQSDecal" /> templates.
    /// </summary>
    /// <remarks>
    /// Texture2DArrays (Diffuse / Normal / AlphaMask / Peak / Slope) are built one slice per decal
    /// using the decal's matching sidecar field and the exact source descriptor. An entirely
    /// unbound rendered channel is disabled. Partial channels reject missing slots.
    /// HeightData and AlphaData contain one R16 slice per template in baked-list order.
    /// Unbound template grids use the controller's shared maps as repeated fallback slices.
    /// </remarks>
    public static class DecalBaker
    {
        /// <summary>
        /// Configure an ordinary PNG grid with the native-verified unsigned 16-bit import settings.
        /// </summary>
        /// <remarks>
        /// Unity stores imported image rows in the opposite order to PNG image rows. Initial recovery
        /// orients the image once. Later artist edits use the standard importer without conversion callbacks.
        /// </remarks>
        /// <param name="assetPath">
        /// The existing project PNG asset path. Configuration preserves its GUID and source image.
        /// </param>
        /// <param name="maxDimension">
        /// The source's largest dimension, with no resizing permitted during import.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a standard PNG importer is absent or the size is outside the supported range.
        /// </exception>
        public static void ConfigurePngGrid(string assetPath, int maxDimension)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || maxDimension < 2 || maxDimension > 8192)
                throw new InvalidOperationException("A project PNG and its actual source dimensions are required.");
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.SingleChannel;
            settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
            settings.sRGBTexture = false;
            settings.ignorePngGamma = true;
            settings.readable = true;
            settings.mipmapEnabled = false;
            settings.npotScale = TextureImporterNPOTScale.None;
            importer.SetTextureSettings(settings);
            importer.maxTextureSize = maxDimension;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone", overridden = true, maxTextureSize = maxDimension,
                format = TextureImporterFormat.R16, textureCompression = TextureImporterCompression.Uncompressed,
                crunchedCompression = false,
            });
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Import and persist a standard PNG as a template's ordinary height or terrain alpha source.
        /// </summary>
        /// <param name="template">
        /// The persisted template receiving an editor-only source binding.
        /// </param>
        /// <param name="assetPath">
        /// The already imported project PNG path. Its existing GUID is retained.
        /// </param>
        /// <param name="alpha">
        /// True to bind the terrain alpha map, false to bind the heightmap.
        /// </param>
        /// <returns>
        /// The readable linear R16 texture published by Unity's standard importer.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the template, source dimensions or native import descriptor is invalid.
        /// </exception>
        public static Texture2D BindPngGrid(PQSDecal template, string assetPath, bool alpha)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (template == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(template)) || importer == null)
                throw new InvalidOperationException("A persisted template and standard PNG importer are required.");
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width < 2 || width % 2 != 0 || height < 1)
                throw new InvalidOperationException("PNG grid dimensions do not support packed-ushort sampling.");
            ConfigurePngGrid(assetPath, Math.Max(width, height));
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null || texture.width != width || texture.height != height || texture.format != TextureFormat.R16
                || !texture.isReadable || texture.isDataSRGB || texture.mipmapCount != 1)
                throw new InvalidOperationException("PNG grid import changed the source dimensions or R16 descriptor.");
            PQSDecalTemplateAuthoring authoring = AuthoringSidecars.GetOrCreate(template);
            if (alpha)
                authoring.AlphaMap = texture;
            else
                authoring.HeightMap = texture;
            EditorUtility.SetDirty(authoring);
            AssetDatabase.SaveAssetIfDirty(authoring);
            return texture;
        }

        /// <summary>
        /// Configure a lossless linear PNG source for the measured ARGB32 rendered-channel route.
        /// </summary>
        /// <param name="assetPath">The existing project PNG path.</param>
        /// <param name="maxDimension">The original source's largest dimension.</param>
        /// <returns>The readable ARGB32 source texture. Untouched byte equality still requires native validation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the importer or resulting native descriptor differs.</exception>
        public static Texture2D ConfigureArgbPng(string assetPath, int maxDimension)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null || maxDimension < 1 || maxDimension > 8192)
                throw new InvalidOperationException("A standard PNG importer and source dimensions are required.");
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Default;
            settings.sRGBTexture = false;
            settings.ignorePngGamma = true;
            settings.readable = true;
            settings.mipmapEnabled = false;
            settings.npotScale = TextureImporterNPOTScale.None;
            settings.alphaSource = TextureImporterAlphaSource.FromInput;
            settings.alphaIsTransparency = false;
            importer.SetTextureSettings(settings);
            importer.maxTextureSize = maxDimension;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone", overridden = true, maxTextureSize = maxDimension,
                format = TextureImporterFormat.ARGB32, textureCompression = TextureImporterCompression.Uncompressed,
                crunchedCompression = false,
            });
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null || texture.format != TextureFormat.ARGB32 || (int)texture.graphicsFormat != 88
                || texture.isDataSRGB || !texture.isReadable || texture.mipmapCount != 1)
                throw new InvalidOperationException("Standard PNG import did not preserve the measured linear ARGB32 descriptor.");
            return texture;
        }

        /// <summary>
        /// Import an ordinary linear data PNG with an explicit compressed or RGB24 platform format and regenerated mips.
        /// </summary>
        /// <remarks>
        /// Encoded normal and diffuse lanes are data. Default texture import preserves the channels without
        /// normal-map conversion or transparency processing. Regenerated BC blocks and mips are measured
        /// projections and are not required to equal original stock bytes after ordinary source edits.
        /// </remarks>
        /// <param name="assetPath">The existing project PNG path.</param>
        /// <param name="format">The explicit DXT1, DXT5 or RGB24 platform output format.</param>
        /// <param name="maxDimension">The source image's largest dimension.</param>
        /// <returns>The readable linear imported data texture.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the requested format, source or native descriptor is invalid.</exception>
        public static Texture2D ConfigureDataPng(string assetPath, TextureImporterFormat format, int maxDimension)
        {
            if (format is not (TextureImporterFormat.DXT1 or TextureImporterFormat.DXT5 or TextureImporterFormat.RGB24))
                throw new InvalidOperationException("An explicit DXT1, DXT5 or RGB24 data format is required.");
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || maxDimension < 2 || maxDimension > 8192)
                throw new InvalidOperationException("A standard data PNG and its source dimensions are required.");
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width < 1 || height < 1 || Math.Max(width, height) != maxDimension)
                throw new InvalidOperationException("The explicit data PNG size must match its actual source dimensions.");
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Default;
            settings.sRGBTexture = false;
            settings.ignorePngGamma = true;
            settings.readable = true;
            settings.mipmapEnabled = true;
            settings.mipmapFilter = TextureImporterMipFilter.BoxFilter;
            settings.npotScale = TextureImporterNPOTScale.None;
            settings.alphaSource = TextureImporterAlphaSource.FromInput;
            settings.alphaIsTransparency = false;
            importer.SetTextureSettings(settings);
            importer.maxTextureSize = maxDimension;
            importer.textureCompression = format == TextureImporterFormat.RGB24
                ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            importer.crunchedCompression = false;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone", overridden = true, maxTextureSize = maxDimension, format = format,
                textureCompression = importer.textureCompression, compressionQuality = 100, crunchedCompression = false,
            });
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null || texture.width != width || texture.height != height || (int)texture.format != (int)format
                || !texture.isReadable || texture.isDataSRGB || texture.mipmapCount != Mathf.FloorToInt(Mathf.Log(maxDimension, 2)) + 1)
                throw new InvalidOperationException("Ordinary data PNG import changed source dimensions, complete mips, format or readability.");
            return texture;
        }

        // Coalesces auto-bake requests from DecalAutoBaker (template asset changes) and
        // DecalInstanceAutoBaker (instance edits / deletes) so back-to-back triggers produce one
        // bake instead of two.
        private static bool _bakeQueued;
        private static PQSDecalController _queuedController;
        private static EditorApplication.CallbackFunction _pendingRebuild;
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
            if (controller == null || _bakeQueued)
                return;
            _bakeQueued = true;
            _queuedController = controller;
            _pendingRebuild = () =>
            {
                _bakeQueued = false;
                _queuedController = null;
                _pendingRebuild = null;
                if (controller != null)
                {
                    RebuildForController(controller);
                }
            };
            EditorApplication.delayCall += _pendingRebuild;
        }

        /// <summary>Cancel a delayed bake owned by the specified controller before preview teardown.</summary>
        /// <param name="controller">The controller whose pending work is being abandoned.</param>
        /// <returns>True if matching queued work was removed, false otherwise.</returns>
        public static bool CancelQueuedRebuild(PQSDecalController controller)
        {
            if (!_bakeQueued || _queuedController != controller)
                return false;
            EditorApplication.delayCall -= _pendingRebuild;
            _pendingRebuild = null;
            _queuedController = null;
            _bakeQueued = false;
            return true;
        }

        /// <summary>
        /// Rebuilds the body's <see cref="PQSDecalData" /> from the templates currently used by instances on <paramref name="controller" />.
        /// </summary>
        /// <remarks>
        /// Saves the asset and refreshes the runtime.
        /// </remarks>
        /// <param name="controller">The decal controller whose body data to rebuild.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a template identity, required slot, descriptor or runtime cache is invalid.
        /// Invalid source slots are rejected before existing baked data is replaced.
        /// </exception>
        public static void RebuildForController(PQSDecalController controller)
        {
            if (controller == null || controller.PqsDecalData == null)
                return;

            var templates = CollectTemplates(controller);
            var data = controller.PqsDecalData;
            var templateAuthorings = new List<PQSDecalTemplateAuthoring>(templates.Count);
            var ids = new List<string>(templates.Count);
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PQSDecal template in templates)
            {
                if (string.IsNullOrEmpty(template.DecalID) || !uniqueIds.Add(template.DecalID))
                    throw new InvalidOperationException($"Decal '{template.name}' has a missing or duplicate DecalID.");
                ids.Add(template.DecalID);
                templateAuthorings.Add(AuthoringSidecars.Find(template));
            }
            // Resolve and validate complete grids before changing the persisted lists or arrays.
            var heights = PackGrids(templates, templateAuthorings, a => a.HeightMap, controller.SharedHeightmap, "height");
            var alphas = PackGrids(templates, templateAuthorings, a => a.AlphaMap, controller.SharedAlphaMap, "alpha");
            var arrays = new Texture2DArray[5];
            try
            {
                arrays[0] = BuildArray(templates, templateAuthorings, a => a.Diffuse, "Diffuse");
                arrays[1] = BuildArray(templates, templateAuthorings, a => a.Normal, "Normal");
                arrays[2] = BuildArray(templates, templateAuthorings, a => a.AlphaMaskTexture, "AlphaMask");
                arrays[3] = BuildArray(templates, templateAuthorings, a => a.Peak, "Peak");
                arrays[4] = BuildArray(templates, templateAuthorings, a => a.Slope, "Slope");
                CompletePendingSurfaceJob(controller.Pqs);
                foreach (string fieldName in new[] { "_nativeHeight", "_nativeAlpha" })
                {
                    FieldInfo field = typeof(PQSDecalController).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field == null || field.FieldType != typeof(NativeArray<ushort>))
                        throw new InvalidOperationException($"Decal runtime cache '{fieldName}' cannot be refreshed.");
                }
                data.DiffuseTextureArray = ReplaceSubAsset(data, data.DiffuseTextureArray, arrays[0], "DiffuseTextureArray");
                data.NormalTextureArray = ReplaceSubAsset(data, data.NormalTextureArray, arrays[1], "NormalTextureArray");
                data.AlphaMaskTextureArray = ReplaceSubAsset(data, data.AlphaMaskTextureArray, arrays[2], "AlphaMaskTextureArray");
                data.PeakTextureArray = ReplaceSubAsset(data, data.PeakTextureArray, arrays[3], "PeakTextureArray");
                data.SlopeTextureArray = ReplaceSubAsset(data, data.SlopeTextureArray, arrays[4], "SlopeTextureArray");
                data.BakedPqsDecalList = templates;
                data.BakedPqsDecalIDList = ids;
                data.Count = templates.Count;
                (data.HeightData, data.HeightWidth, data.HeightHeight) = heights;
                (data.AlphaData, data.AlphaWidth, data.AlphaHeight) = alphas;
            }
            finally
            {
                foreach (Texture2DArray array in arrays)
                {
                    if (array != null && !EditorUtility.IsPersistent(array))
                        Object.DestroyImmediate(array);
                }
            }

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(data));
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
            if (planet == null)
                return;
            var jobField = typeof(PQS).GetField("_surfaceJob", BindingFlags.NonPublic | BindingFlags.Instance);
            if (jobField == null)
            {
                if (!_surfaceJobReflectionWarned)
                {
                    Debug.LogWarning("[DecalBaker] PQS._surfaceJob private field not found via reflection. Decal bake will not wait for in-flight subdivision jobs to complete. Likely renamed; update DecalBaker.CompletePendingSurfaceJob.");
                    _surfaceJobReflectionWarned = true;
                }
                throw new InvalidOperationException("PQS surface job cannot be completed before decal replacement.");
            }
            var holder = jobField.GetValue(planet);
            if (holder == null)
                return;
            // _surfaceJob is a PQJob.PQSJobHolder struct with a JobHandle named "handle". Complete it directly so any in-flight UpdateSubdivisions read finishes before we dispose the NativeArrays it referenced.
            var handleField = holder.GetType().GetField("handle");
            if (handleField == null)
                throw new InvalidOperationException("PQS surface job handle is inaccessible.");
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
                throw new InvalidOperationException($"Decal runtime cache '{fieldName}' is inaccessible.");
            }
            var current = (NativeArray<ushort>)field.GetValue(controller);
            if (current.IsCreated)
            {
                current.Dispose();
            }
            field.SetValue(controller, default(NativeArray<ushort>));
        }

        /// <summary>
        /// Collect live templates while retaining the existing baked order for every surviving template.
        /// </summary>
        /// <param name="controller">
        /// The controller owning the current instances and baked mapping.
        /// </param>
        /// <returns>
        /// Surviving baked templates followed by new templates in first-instance order.
        /// </returns>
        internal static List<PQSDecal> CollectTemplates(PQSDecalController controller)
        {
            var seen = new HashSet<PQSDecal>();
            var encountered = new List<PQSDecal>();
            if (controller.PqsDecalInstanceList == null)
                return encountered;
            foreach (var inst in controller.PqsDecalInstanceList)
            {
                if (inst == null)
                    continue;
                if (inst.PQSDecal == null)
                    throw new InvalidOperationException($"Decal instance '{inst.name}' has no template.");
                if (seen.Add(inst.PQSDecal))
                {
                    encountered.Add(inst.PQSDecal);
                }
            }
            var ordered = new List<PQSDecal>(encountered.Count);
            List<PQSDecal> baked = controller.PqsDecalData != null ? controller.PqsDecalData.BakedPqsDecalList : null;
            if (baked != null)
            {
                foreach (PQSDecal template in baked)
                {
                    if (template != null && seen.Remove(template))
                        ordered.Add(template);
                }
            }
            foreach (PQSDecal template in encountered)
            {
                if (seen.Remove(template))
                    ordered.Add(template);
            }
            return ordered;
        }

        private static Texture2DArray ReplaceSubAsset(PQSDecalData host, Texture2DArray existing, Texture2DArray newArray, string name)
        {
            bool owned = existing != null && AssetDatabase.IsSubAsset(existing)
                && AssetDatabase.GetAssetPath(existing) == AssetDatabase.GetAssetPath(host);
            if (owned && newArray != null)
            {
                // Keep the owned target and its external references. The caller owns the prepared source's cleanup.
                var backup = Object.Instantiate(existing);
                backup.name = existing.name;
                try
                {
                    newArray.name = name;
                    EditorUtility.CopySerialized(newArray, existing);
                    existing.Apply(false, false);
                    EditorUtility.SetDirty(existing);
                    return existing;
                }
                catch (Exception updateError)
                {
                    try
                    {
                        EditorUtility.CopySerialized(backup, existing);
                        existing.Apply(false, false);
                        EditorUtility.SetDirty(existing);
                    }
                    catch (Exception rollbackError)
                    {
                        throw new AggregateException("Retained array update and rollback failed. The existing identity was not recreated.", updateError, rollbackError);
                    }
                    throw new InvalidOperationException("Retained array update failed and its previous content was restored.", updateError);
                }
                finally
                {
                    Object.DestroyImmediate(backup);
                }
            }
            if (owned)
            {
                // An entirely unbound channel intentionally removes only the old host-owned subasset.
                Object.DestroyImmediate(existing, true);
            }
            if (newArray != null)
            {
                newArray.name = name;
                AssetDatabase.AddObjectToAsset(newArray, host);
            }
            return newArray;
        }

        private static Texture2DArray BuildArray(List<PQSDecal> templates, List<PQSDecalTemplateAuthoring> authorings, Func<PQSDecalTemplateAuthoring, Texture2D> selector, string label)
        {
            // Resolve per-template source textures from each template's authoring sidecar.
            var sources = new Texture2D[templates.Count];
            Texture2D reference = null;
            for (var i = 0; i < templates.Count; i++)
            {
                if (authorings[i] == null)
                    continue;
                sources[i] = selector(authorings[i]);
                if (reference == null && sources[i] != null)
                {
                    reference = sources[i];
                }
            }
            if (reference == null)
                return null;
            foreach (Texture2D source in sources)
            {
                if (source == null || !source.isReadable)
                    throw new InvalidOperationException($"{label} requires a readable source for every baked decal slot.");
                if (source.width != reference.width || source.height != reference.height || source.graphicsFormat != reference.graphicsFormat
                    || source.mipmapCount != reference.mipmapCount || source.filterMode != reference.filterMode || source.anisoLevel != reference.anisoLevel
                    || BitConverter.SingleToInt32Bits(source.mipMapBias) != BitConverter.SingleToInt32Bits(reference.mipMapBias)
                    || source.wrapModeU != reference.wrapModeU || source.wrapModeV != reference.wrapModeV
                    || source.wrapModeW != reference.wrapModeW)
                    throw new InvalidOperationException($"{label} source dimensions, format, mip count or sampler differ across baked slots.");
            }
            // Native format 88 is ARGB32 through the public legacy constructor. Preserve the actual
            // source TextureFormat and linear flag, then require exact GraphicsFormat projection.
            var array = new Texture2DArray(reference.width, reference.height, templates.Count, reference.format, reference.mipmapCount, !reference.isDataSRGB)
            {
                filterMode = reference.filterMode,
                anisoLevel = reference.anisoLevel,
                mipMapBias = reference.mipMapBias,
                wrapModeU = reference.wrapModeU,
                wrapModeV = reference.wrapModeV,
                wrapModeW = reference.wrapModeW,
            };
            try
            {
                if (array.graphicsFormat != reference.graphicsFormat || array.isDataSRGB != reference.isDataSRGB || array.mipmapCount != reference.mipmapCount)
                    throw new InvalidOperationException($"{label} legacy constructor changed the native source descriptor.");
                for (int slot = 0; slot < sources.Length; slot++)
                {
                    for (int mip = 0; mip < reference.mipmapCount; mip++)
                    {
                        // CPU block upload preserves unedited BC words. No blit, decompression or recompression runs here.
                        array.SetPixelData(sources[slot].GetPixelData<byte>(mip), mip, slot);
                    }
                }
                array.Apply(false, false);
                return array;
            }
            catch
            {
                Object.DestroyImmediate(array);
                throw;
            }
        }

        private static (ushort[] data, int width, int height) PackGrids(List<PQSDecal> templates,
            List<PQSDecalTemplateAuthoring> authorings, Func<PQSDecalTemplateAuthoring, Texture2D> selector, Texture2D fallback, string label)
        {
            if (templates.Count == 0)
                return (Array.Empty<ushort>(), 0, 0);
            var sources = new Texture2D[templates.Count];
            int width = 0;
            int height = 0;
            for (int slot = 0; slot < templates.Count; slot++)
            {
                Texture2D source = authorings[slot] != null ? selector(authorings[slot]) : null;
                sources[slot] = source != null ? source : fallback;
                source = sources[slot];
                if (source == null)
                    throw new InvalidOperationException($"Decal '{templates[slot].name}' slot {slot} has no {label} source or shared fallback.");
                if (source.format != TextureFormat.R16 || !source.isReadable || source.isDataSRGB)
                    throw new InvalidOperationException($"Decal '{templates[slot].name}' {label} source must be readable linear R16.");
                if (source.width < 2 || source.width % 2 != 0 || source.height < 1)
                    throw new InvalidOperationException($"Decal '{templates[slot].name}' {label} width must be positive and even for packed-ushort sampling.");
                if (slot == 0)
                {
                    width = source.width;
                    height = source.height;
                }
                if (source.width != width || source.height != height || source.GetPixelData<ushort>(0).Length != checked(width * height))
                    throw new InvalidOperationException($"Decal '{templates[slot].name}' {label} dimensions or base-mip word count differ from slot zero.");
            }
            int sliceWords = checked(width * height);
            var data = new ushort[checked(sliceWords * templates.Count)];
            for (int slot = 0; slot < sources.Length; slot++)
            {
                NativeArray<ushort>.Copy(sources[slot].GetPixelData<ushort>(0), 0, data, slot * sliceWords, sliceWords);
            }
            return (data, width, height);
        }
    }
}
