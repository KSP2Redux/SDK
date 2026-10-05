using System;
using KSP.Rendering;
using KSP.Rendering.Utility;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Atmosphere
{
    /// <summary>
    /// Precomputes the Bruneton transmittance, irradiance and scattering lookup tables an
    /// <see cref="AtmosphereModel" /> renders from.
    /// </summary>
    /// <remarks>
    /// Runs Bruneton's 2017 reference precomputation on the GPU with four scattering orders. Stock
    /// KSP2 shipped its tables pre-baked and no precompute shader, so the compute shader is
    /// Redux-authored and ships with the SDK.
    /// </remarks>
    public static class BrunetonLutBaker
    {
        private const string COMPUTE_SHADER_PATH =
            SDKConfiguration.BasePath + "/Assets/Shaders/PlanetAuthoring/Atmosphere/Bruneton/Precomputation.compute";

        private const int READ = 0;
        private const int WRITE = 1;
        private const int SCATTERING_ORDERS = 4;

        /// <summary>
        /// Bakes the model's lookup tables to texture assets and assigns them to the model.
        /// </summary>
        /// <remarks>
        /// Writes <c>&lt;prefix&gt;_Transmittance</c>, <c>_Irradiance</c> and <c>_Scattering</c>
        /// assets into <paramref name="folder" />, overwriting earlier bakes in place so references
        /// to them survive.
        /// </remarks>
        /// <param name="model">The atmosphere model to bake.</param>
        /// <param name="folder">The project folder to write the texture assets into. Created if missing.</param>
        /// <param name="error">The failure reason, or an empty string on success.</param>
        /// <returns>True if the tables were baked and assigned, false otherwise.</returns>
        public static bool Bake(AtmosphereModel model, string folder, out string error)
        {
            ComputeShader compute = LoadCompute(model, out error);
            if (compute == null)
                return false;

            EnsureFolder(folder);
            var textures = new TextureSet();
            try
            {
                textures.Create();
                textures.Clear(compute);
                BindModel(compute, model);
                Precompute(compute, textures);

                string assetPrefix = string.IsNullOrWhiteSpace(model.name) ? model.PlanetName : model.name;
                Texture2D transmittance = ReadTexture2D(textures.Transmittance[READ], $"{assetPrefix}_Transmittance");
                Texture2D irradiance = ReadTexture2D(textures.Irradiance[READ], $"{assetPrefix}_Irradiance");
                Texture3D scattering = ReadTexture3D(compute, textures.Scattering[READ], $"{assetPrefix}_Scattering");

                Undo.RecordObject(model, "Bake Atmosphere LUTs");
                model.TransmittanceTexture = SaveAsset(transmittance, $"{folder}/{assetPrefix}_Transmittance.asset");
                model.IrradianceTexture = SaveAsset(irradiance, $"{folder}/{assetPrefix}_Irradiance.asset");
                model.ScatteringTexture = SaveAsset(scattering, $"{folder}/{assetPrefix}_Scattering.asset");
                EditorUtility.SetDirty(model);

                // Lets the stale-tables check tell these tables from ones baked before an edit.
                AtmosphereModelAuthoring sidecar = AuthoringSidecars.GetOrCreate(model);
                if (sidecar != null)
                {
                    sidecar.BakedLutHash = ComputeLutInputHash(model);
                    EditorUtility.SetDirty(sidecar);
                }

                AssetDatabase.SaveAssets();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                textures.Release();
            }
        }

        /// <summary>
        /// Computes the model's lookup tables into GPU render textures without reading them back.
        /// </summary>
        /// <remarks>
        /// The result suits a live preview: hand it to <see cref="AtmosphereModel.SetRealtimeTextures" />,
        /// which takes ownership. On failure every out texture is null.
        /// </remarks>
        /// <param name="model">The atmosphere model to compute.</param>
        /// <param name="transmittance">The transmittance table, owned by the caller.</param>
        /// <param name="irradiance">The irradiance table, owned by the caller.</param>
        /// <param name="scattering">The 3D scattering table, owned by the caller.</param>
        /// <param name="error">The failure reason, or an empty string on success.</param>
        /// <returns>True if the tables were computed, false otherwise.</returns>
        public static bool BakeRealtime(
            AtmosphereModel model,
            out RenderTexture transmittance,
            out RenderTexture irradiance,
            out RenderTexture scattering,
            out string error
        )
        {
            transmittance = null;
            irradiance = null;
            scattering = null;
            ComputeShader compute = LoadCompute(model, out error);
            if (compute == null)
                return false;

            var textures = new TextureSet();
            try
            {
                textures.Create();
                textures.Clear(compute);
                BindModel(compute, model);
                Precompute(compute, textures);
                textures.DetachFinalTextures(out transmittance, out irradiance, out scattering);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                ReleaseTexture(transmittance);
                ReleaseTexture(irradiance);
                ReleaseTexture(scattering);
                transmittance = null;
                irradiance = null;
                scattering = null;
                return false;
            }
            finally
            {
                textures.Release();
            }
        }

        /// <summary>
        /// Hashes every model field the lookup tables are computed from.
        /// </summary>
        /// <remarks>
        /// Two models with the same hash bake the same tables, so a change that leaves it alone, such
        /// as exposure or a tint, needs new material values but no rebake. Covers exactly the fields
        /// the precompute binds.
        /// </remarks>
        /// <param name="model">The atmosphere model.</param>
        /// <returns>The hash of the model's table inputs.</returns>
        public static int ComputeLutInputHash(AtmosphereModel model)
        {
            var hash = new HashCode();
            hash.Add(model.SolarIrradiance);
            hash.Add(model.RayleighScattering);
            hash.Add(model.RayleighScatteringScale);
            hash.Add(model.RayleighExponentialDistribution);
            hash.Add(model.MieScattering);
            hash.Add(model.MieScatteringScale);
            hash.Add(model.MieExponentialDistribution);
            hash.Add(model.MieAnisotropy);
            hash.Add(model.Absorption);
            hash.Add(model.AbsorptionScale);
            hash.Add(model.AbsorptionMaxDensity);
            hash.Add(model.AbsorptionHeightMinMax);
            hash.Add(model.GroundAlbedo);
            hash.Add(model.SunAngleRadius);
            hash.Add(model.SunZenithAngle);
            hash.Add(model.BottomRadius);
            hash.Add(model.AtmosphereHeight);
            return hash.ToHashCode();
        }

        private static ComputeShader LoadCompute(AtmosphereModel model, out string error)
        {
            error = string.Empty;
            if (model == null)
            {
                error = "No AtmosphereModel was provided.";
                return null;
            }

            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(COMPUTE_SHADER_PATH);
            if (compute == null)
            {
                error = $"Bruneton precompute shader is missing at {COMPUTE_SHADER_PATH}.";
            }

            return compute;
        }

        private static void Precompute(ComputeShader compute, TextureSet textures)
        {
            int computeTransmittance = compute.FindKernel("ComputeTransmittance");
            int computeDirectIrradiance = compute.FindKernel("ComputeDirectIrradiance");
            int computeSingleScattering = compute.FindKernel("ComputeSingleScattering");
            int computeScatteringDensity = compute.FindKernel("ComputeScatteringDensity");
            int computeIndirectIrradiance = compute.FindKernel("ComputeIndirectIrradiance");
            int computeMultipleScattering = compute.FindKernel("ComputeMultipleScattering");
            int numThreads = AtmosphereConstants.NUM_THREADS;

            compute.SetTexture(computeTransmittance, "transmittanceWrite", textures.Transmittance[WRITE]);
            compute.SetVector("blend", Vector4.zero);
            compute.Dispatch(
                computeTransmittance,
                AtmosphereConstants.TRANSMITTANCE_WIDTH / numThreads,
                AtmosphereConstants.TRANSMITTANCE_HEIGHT / numThreads,
                1
            );
            Swap(textures.Transmittance);

            compute.SetTexture(computeDirectIrradiance, "deltaIrradianceWrite", textures.DeltaIrradiance);
            compute.SetTexture(computeDirectIrradiance, "irradianceWrite", textures.Irradiance[WRITE]);
            compute.SetTexture(computeDirectIrradiance, "irradianceRead", textures.Irradiance[READ]);
            compute.SetTexture(computeDirectIrradiance, "transmittanceRead", textures.Transmittance[READ]);
            compute.SetVector("blend", Vector4.zero);
            compute.Dispatch(
                computeDirectIrradiance,
                AtmosphereConstants.IRRADIANCE_WIDTH / numThreads,
                AtmosphereConstants.IRRADIANCE_HEIGHT / numThreads,
                1
            );
            Swap(textures.Irradiance);

            compute.SetTexture(computeSingleScattering, "deltaRayleighScatteringWrite", textures.DeltaRayleighScattering);
            compute.SetTexture(computeSingleScattering, "deltaMieScatteringWrite", textures.DeltaMieScattering);
            compute.SetTexture(computeSingleScattering, "scatteringWrite", textures.Scattering[WRITE]);
            compute.SetTexture(computeSingleScattering, "scatteringRead", textures.Scattering[READ]);
            compute.SetTexture(computeSingleScattering, "singleMieScatteringWrite", textures.SingleMieScattering[WRITE]);
            compute.SetTexture(computeSingleScattering, "singleMieScatteringRead", textures.SingleMieScattering[READ]);
            compute.SetTexture(computeSingleScattering, "transmittanceRead", textures.Transmittance[READ]);
            compute.SetVector("blend", Vector4.zero);
            DispatchScatteringLayers(compute, computeSingleScattering);
            Swap(textures.Scattering);
            Swap(textures.SingleMieScattering);

            for (var scatteringOrder = 2; scatteringOrder <= SCATTERING_ORDERS; scatteringOrder++)
            {
                compute.SetTexture(computeScatteringDensity, "deltaScatteringDensityWrite", textures.DeltaScatteringDensity);
                compute.SetTexture(computeScatteringDensity, "transmittanceRead", textures.Transmittance[READ]);
                compute.SetTexture(computeScatteringDensity, "singleRayleighScatteringRead", textures.DeltaRayleighScattering);
                compute.SetTexture(computeScatteringDensity, "singleMieScatteringRead", textures.DeltaMieScattering);
                compute.SetTexture(computeScatteringDensity, "multipleScatteringRead", textures.DeltaMultipleScattering);
                compute.SetTexture(computeScatteringDensity, "irradianceRead", textures.DeltaIrradiance);
                compute.SetInt("scatteringOrder", scatteringOrder);
                compute.SetVector("blend", Vector4.zero);
                DispatchScatteringLayers(compute, computeScatteringDensity);

                compute.SetTexture(computeIndirectIrradiance, "deltaIrradianceWrite", textures.DeltaIrradiance);
                compute.SetTexture(computeIndirectIrradiance, "irradianceWrite", textures.Irradiance[WRITE]);
                compute.SetTexture(computeIndirectIrradiance, "irradianceRead", textures.Irradiance[READ]);
                compute.SetTexture(computeIndirectIrradiance, "singleRayleighScatteringRead", textures.DeltaRayleighScattering);
                compute.SetTexture(computeIndirectIrradiance, "singleMieScatteringRead", textures.DeltaMieScattering);
                compute.SetTexture(computeIndirectIrradiance, "multipleScatteringRead", textures.DeltaMultipleScattering);
                compute.SetInt("scatteringOrder", scatteringOrder - 1);
                compute.SetVector("blend", new Vector4(0f, 1f, 0f, 0f));
                compute.Dispatch(
                    computeIndirectIrradiance,
                    AtmosphereConstants.IRRADIANCE_WIDTH / numThreads,
                    AtmosphereConstants.IRRADIANCE_HEIGHT / numThreads,
                    1
                );
                Swap(textures.Irradiance);

                compute.SetTexture(computeMultipleScattering, "deltaMultipleScatteringWrite", textures.DeltaMultipleScattering);
                compute.SetTexture(computeMultipleScattering, "scatteringWrite", textures.Scattering[WRITE]);
                compute.SetTexture(computeMultipleScattering, "scatteringRead", textures.Scattering[READ]);
                compute.SetTexture(computeMultipleScattering, "transmittanceRead", textures.Transmittance[READ]);
                compute.SetTexture(computeMultipleScattering, "deltaScatteringDensityRead", textures.DeltaScatteringDensity);
                compute.SetVector("blend", new Vector4(0f, 1f, 0f, 0f));
                DispatchScatteringLayers(compute, computeMultipleScattering);
                Swap(textures.Scattering);
            }
        }

        private static void DispatchScatteringLayers(ComputeShader compute, int kernel)
        {
            int numThreads = AtmosphereConstants.NUM_THREADS;
            for (var layer = 0; layer < AtmosphereConstants.SCATTERING_DEPTH; layer++)
            {
                compute.SetInt("layer", layer);
                compute.Dispatch(
                    kernel,
                    AtmosphereConstants.SCATTERING_WIDTH / numThreads,
                    AtmosphereConstants.SCATTERING_HEIGHT / numThreads,
                    1
                );
            }
        }

        private static void BindModel(ComputeShader compute, AtmosphereModel model)
        {
            compute.SetInt("TRANSMITTANCE_TEXTURE_WIDTH", AtmosphereConstants.TRANSMITTANCE_WIDTH);
            compute.SetInt("TRANSMITTANCE_TEXTURE_HEIGHT", AtmosphereConstants.TRANSMITTANCE_HEIGHT);
            compute.SetInt("SCATTERING_TEXTURE_R_SIZE", AtmosphereConstants.SCATTERING_R);
            compute.SetInt("SCATTERING_TEXTURE_MU_SIZE", AtmosphereConstants.SCATTERING_MU);
            compute.SetInt("SCATTERING_TEXTURE_MU_S_SIZE", AtmosphereConstants.SCATTERING_MU_S);
            compute.SetInt("SCATTERING_TEXTURE_NU_SIZE", AtmosphereConstants.SCATTERING_NU);
            compute.SetInt("SCATTERING_TEXTURE_WIDTH", AtmosphereConstants.SCATTERING_WIDTH);
            compute.SetInt("SCATTERING_TEXTURE_HEIGHT", AtmosphereConstants.SCATTERING_HEIGHT);
            compute.SetInt("SCATTERING_TEXTURE_DEPTH", AtmosphereConstants.SCATTERING_DEPTH);
            compute.SetInt("IRRADIANCE_TEXTURE_WIDTH", AtmosphereConstants.IRRADIANCE_WIDTH);
            compute.SetInt("IRRADIANCE_TEXTURE_HEIGHT", AtmosphereConstants.IRRADIANCE_HEIGHT);

            compute.SetVector("SKY_SPECTRAL_RADIANCE_TO_LUMINANCE", Vector3.one);
            compute.SetVector("SUN_SPECTRAL_RADIANCE_TO_LUMINANCE", Vector3.one);
            compute.SetFloats("luminanceFromRadiance", IdentityMatrix());

            // The model stores its coefficients in the units the stock assets use, which differ
            // from the reference implementation's by these fixed factors.
            compute.SetVector("solar_irradiance", model.SolarIrradiance * 2f);
            compute.SetVector("rayleigh_scattering", model.RayleighScattering * model.RayleighScatteringScale * 0.1f);

            Vector3 mieScattering = model.MieScattering * model.MieScatteringScale * 0.01f;
            compute.SetVector("mie_scattering", mieScattering);
            compute.SetVector("mie_extinction", mieScattering * AtmosphereConstants.MIE_EXTINCTION_TO_SCATTERING_RATIO);
            compute.SetVector("absorption_extinction", model.Absorption * model.AbsorptionScale * 0.1f);
            compute.SetVector("ground_albedo", new Vector3(model.GroundAlbedo.r, model.GroundAlbedo.g, model.GroundAlbedo.b));
            compute.SetFloat("sun_angular_radius", model.SunAngleRadius * 0.1f);
            compute.SetFloat("bottom_radius", model.BottomRadius);
            compute.SetFloat("top_radius", model.BottomRadius + model.AtmosphereHeight);
            compute.SetFloat("mie_phase_function_g", model.MieAnisotropy);
            compute.SetFloat("mu_s_min", Mathf.Cos(model.SunZenithAngle * Mathf.Deg2Rad));

            BindDensityLayer(compute, "rayleigh", 0f, 1f, -1f / Mathf.Max(0.001f, model.RayleighExponentialDistribution), 0f, 0f);
            BindDensityLayer(compute, "mie", 0f, 1f, -1f / Mathf.Max(0.001f, model.MieExponentialDistribution), 0f, 0f);
            BindAbsorptionLayers(compute, model);
        }

        // The absorption band is a tent between the model's min and max heights, built from two
        // linear layers that meet at its middle.
        private static void BindAbsorptionLayers(ComputeShader compute, AtmosphereModel model)
        {
            float min = Mathf.Max(0f, model.AbsorptionHeightMinMax.x);
            float max = Mathf.Max(min + 0.001f, model.AbsorptionHeightMinMax.y);
            float middle = (min + max) * 0.5f;
            float halfWidth = Mathf.Max(0.001f, (max - min) * 0.5f);
            BindDensityLayer(compute, "absorption0", middle, 0f, 0f, model.AbsorptionMaxDensity / halfWidth, -min / halfWidth);
            BindDensityLayer(compute, "absorption1", 0f, 0f, 0f, -model.AbsorptionMaxDensity / halfWidth, max / halfWidth);
        }

        private static void BindDensityLayer(
            ComputeShader compute,
            string name,
            float width,
            float expTerm,
            float expScale,
            float linearTerm,
            float constantTerm
        )
        {
            compute.SetFloat($"{name}_width", width);
            compute.SetFloat($"{name}_exp_term", expTerm);
            compute.SetFloat($"{name}_exp_scale", expScale);
            compute.SetFloat($"{name}_linear_term", linearTerm);
            compute.SetFloat($"{name}_constant_term", constantTerm);
        }

        private static Texture2D ReadTexture2D(RenderTexture source, string name)
        {
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(
                source,
                0,
                0,
                source.width,
                0,
                source.height,
                0,
                1,
                TextureFormat.RGBAFloat
            );
            request.WaitForCompletion();
            if (request.hasError)
                throw new InvalidOperationException($"GPU readback failed for {name}.");

            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBAFloat, false, true)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            NativeArray<Color> data = request.GetData<Color>();
            texture.SetPixelData(data, 0);
            texture.Apply(false, false);
            return texture;
        }

        // Readback cannot target a 3D render texture directly, so each depth slice is copied to a
        // 2D texture first and read back from there.
        private static Texture3D ReadTexture3D(ComputeShader compute, RenderTexture source, string name)
        {
            RenderTexture slice = NewTexture2D(source.width, source.height);
            var pixels = new Color[source.width * source.height * source.volumeDepth];
            int kernel = compute.FindKernel("CopySlizeFromTex3D");
            try
            {
                for (var z = 0; z < source.volumeDepth; z++)
                {
                    compute.SetInt("layer", z);
                    compute.SetTexture(kernel, "targetWrite2D", slice);
                    compute.SetTexture(kernel, "targetRead3D", source);
                    compute.Dispatch(
                        kernel,
                        source.width / AtmosphereConstants.NUM_THREADS,
                        source.height / AtmosphereConstants.NUM_THREADS,
                        1
                    );

                    AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(
                        slice,
                        0,
                        0,
                        source.width,
                        0,
                        source.height,
                        0,
                        1,
                        TextureFormat.RGBAFloat
                    );
                    request.WaitForCompletion();
                    if (request.hasError)
                        throw new InvalidOperationException($"GPU readback failed for {name} slice {z}.");

                    NativeArray<Color> data = request.GetData<Color>();
                    int offset = z * source.width * source.height;
                    for (var i = 0; i < data.Length; i++)
                    {
                        pixels[offset + i] = data[i];
                    }
                }
            }
            finally
            {
                slice.Release();
                Object.DestroyImmediate(slice);
            }

            var texture = new Texture3D(source.width, source.height, source.volumeDepth, TextureFormat.RGBAFloat, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static T SaveAsset<T>(T asset, string path) where T : Object
        {
            path = path.Replace('\\', '/');
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }

            EditorUtility.CopySerialized(asset, existing);
            existing.name = asset.name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(asset);
            return existing;
        }

        private static RenderTexture NewTexture2D(int width, int height)
        {
            var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                enableRandomWrite = true
            };
            texture.Create();
            return texture;
        }

        private static RenderTexture NewTexture3D(int width, int height, int depth)
        {
            var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
            {
                volumeDepth = depth,
                dimension = TextureDimension.Tex3D,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                enableRandomWrite = true
            };
            texture.Create();
            return texture;
        }

        private static void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            Object.DestroyImmediate(texture);
        }

        private static void ClearTexture(ComputeShader compute, RenderTexture texture)
        {
            int numThreads = AtmosphereConstants.NUM_THREADS;
            if (texture.dimension == TextureDimension.Tex3D)
            {
                int kernel = compute.FindKernel("ClearTex3D");
                compute.SetTexture(kernel, "targetWrite3D", texture);
                compute.Dispatch(kernel, texture.width / numThreads, texture.height / numThreads, texture.volumeDepth / numThreads);
            }
            else
            {
                int kernel = compute.FindKernel("ClearTex2D");
                compute.SetTexture(kernel, "targetWrite2D", texture);
                compute.Dispatch(kernel, texture.width / numThreads, texture.height / numThreads, 1);
            }
        }

        private static float[] IdentityMatrix() => new[]
        {
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f
        };

        private static void Swap(RenderTexture[] textures)
        {
            (textures[READ], textures[WRITE]) = (textures[WRITE], textures[READ]);
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Replace('\\', '/').Split('/');
            string current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        // Read/write pairs are ping-ponged between passes. The final tables end up in the READ slot.
        private class TextureSet
        {
            public readonly RenderTexture[] Transmittance = new RenderTexture[2];
            public readonly RenderTexture[] Irradiance = new RenderTexture[2];
            public readonly RenderTexture[] Scattering = new RenderTexture[2];
            public readonly RenderTexture[] SingleMieScattering = new RenderTexture[2];
            public RenderTexture DeltaIrradiance;
            public RenderTexture DeltaRayleighScattering;
            public RenderTexture DeltaMieScattering;
            public RenderTexture DeltaScatteringDensity;

            // The reference implementation reuses the single-Rayleigh buffer for multiple scattering.
            public RenderTexture DeltaMultipleScattering => DeltaRayleighScattering;

            public void Create()
            {
                Transmittance[READ] = NewTexture2D(AtmosphereConstants.TRANSMITTANCE_WIDTH, AtmosphereConstants.TRANSMITTANCE_HEIGHT);
                Transmittance[WRITE] = NewTexture2D(AtmosphereConstants.TRANSMITTANCE_WIDTH, AtmosphereConstants.TRANSMITTANCE_HEIGHT);
                Irradiance[READ] = NewTexture2D(AtmosphereConstants.IRRADIANCE_WIDTH, AtmosphereConstants.IRRADIANCE_HEIGHT);
                Irradiance[WRITE] = NewTexture2D(AtmosphereConstants.IRRADIANCE_WIDTH, AtmosphereConstants.IRRADIANCE_HEIGHT);
                Scattering[READ] = NewScatteringTexture();
                Scattering[WRITE] = NewScatteringTexture();
                SingleMieScattering[READ] = NewScatteringTexture();
                SingleMieScattering[WRITE] = NewScatteringTexture();
                DeltaIrradiance = NewTexture2D(AtmosphereConstants.IRRADIANCE_WIDTH, AtmosphereConstants.IRRADIANCE_HEIGHT);
                DeltaRayleighScattering = NewScatteringTexture();
                DeltaMieScattering = NewScatteringTexture();
                DeltaScatteringDensity = NewScatteringTexture();
            }

            public void Clear(ComputeShader compute)
            {
                ClearTexture(compute, DeltaIrradiance);
                ClearTexture(compute, DeltaRayleighScattering);
                ClearTexture(compute, DeltaMieScattering);
                ClearTexture(compute, DeltaScatteringDensity);
                ClearAll(compute, Transmittance);
                ClearAll(compute, Irradiance);
                ClearAll(compute, Scattering);
                ClearAll(compute, SingleMieScattering);
            }

            public void DetachFinalTextures(
                out RenderTexture transmittance,
                out RenderTexture irradiance,
                out RenderTexture scattering
            )
            {
                transmittance = Transmittance[READ];
                irradiance = Irradiance[READ];
                scattering = Scattering[READ];
                Transmittance[READ] = null;
                Irradiance[READ] = null;
                Scattering[READ] = null;
                transmittance.name = "Bruneton Runtime Transmittance";
                irradiance.name = "Bruneton Runtime Irradiance";
                scattering.name = "Bruneton Runtime Scattering";
            }

            public void Release()
            {
                ReleaseTexture(DeltaIrradiance);
                ReleaseTexture(DeltaRayleighScattering);
                ReleaseTexture(DeltaMieScattering);
                ReleaseTexture(DeltaScatteringDensity);
                ReleaseAll(Transmittance);
                ReleaseAll(Irradiance);
                ReleaseAll(Scattering);
                ReleaseAll(SingleMieScattering);
            }

            private static RenderTexture NewScatteringTexture() => NewTexture3D(
                AtmosphereConstants.SCATTERING_WIDTH,
                AtmosphereConstants.SCATTERING_HEIGHT,
                AtmosphereConstants.SCATTERING_DEPTH
            );

            private static void ClearAll(ComputeShader compute, RenderTexture[] textures)
            {
                foreach (RenderTexture texture in textures)
                {
                    ClearTexture(compute, texture);
                }
            }

            private static void ReleaseAll(RenderTexture[] textures)
            {
                foreach (RenderTexture texture in textures)
                {
                    ReleaseTexture(texture);
                }
            }
        }
    }
}
