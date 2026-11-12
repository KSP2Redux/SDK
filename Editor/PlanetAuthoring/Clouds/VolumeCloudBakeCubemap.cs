using KSP.VolumeCloud;
using Redux.Rendering;
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Clouds
{
    /// <summary>
    /// Owns temporary render resources for the SDK and Redux editor cloud exporters.
    /// </summary>
    /// <remarks>
    /// Each face is prepared when it is scheduled and recorded by a render hook the next time the camera renders, after
    /// post-processing, where stock baked it from OnRenderImage.
    /// </remarks>
    public class VolumeCloudBakeCubemap : ScriptableObject
    {

        private VolumeCloudConfiguration configuration;

        private VolumeCloudConfiguration.CumulusData firstCumulusData;

        private Vector2 allCloudsLayersRange;

        private Material cumulusMat;

        private Material currentMat;

        private RenderTargetIdentifier[] resultColorAndDistance;

        private RenderTexture[] raymarchedDistance;

        private RenderTexture normalRenderTarget;

        private RenderTexture undersampleBuffer;

        private RenderTexture downsampledDepth;

        private Camera mCamera;

        [Header("Shader references(DONT EDIT)")] [SerializeField]
        private Shader cumulusCloudShader;

        [SerializeField] private ComputeShader cloudsComputeShader;

        [SerializeField] private ComputeShader rayCheckGetTile;

        [SerializeField] private Shader listCreationShader;

        [SerializeField] private Shader listRenderingShader;

        [SerializeField] private Shader listDepthShader;

        private Material listCreationMat;

        private Material listRenderingMat;

        private Material listDepthMat;

        private CubemapFace currentShadowFace;

        // Increment when the density integration or footprint filtering changes.
        public const int BAKE_VERSION = Redux.Planets.CloudBakeData.VERSION;

        private int faceWidth;

        private int faceHeight;

        private int faceWidthOri;

        private int faceHeightOri;

        private ComputeBuffer cloudsLayerData;

        private bool EnableBakeCubemap;

        private Cubemap cloudsCubemap;

        private Cubemap cloudsBottomCubemap;

        private Cubemap cloudsNormalCubemap;

        private bool isBakeCloudBottom;

        private bool isBakeCloudBottomComplete;

        private CubemapFace currentFace;

        // Set for a hosted bake that renders each face on demand and reads it back without waiting on the player loop.
        private bool _isEditorHosted;

        private bool _isBakeComplete;

        private bool _bakeReadbackFailed;

        /// <summary>
        /// Width of the integrated top faces. The underside uses one quarter of this width.
        /// </summary>
        public int FaceSize = 2048;

        /// <summary>
        /// Whether the preset uses a neutral one-texel normal map.
        /// </summary>
        public bool FlatNormals;

        // Set when the hook has recorded a face the hosted bake reads back once the camera render returns.
        private bool _isEditorReadbackPending;

        private BakeHook _bakeHook;

        private const int MAX_EDITOR_BAKE_RENDERS = 64;

        /// <summary>
        /// Sets the baker up for the SDK, which hosts it on a hidden camera and bakes through <see cref="BakeForEditor" />.
        /// </summary>
        /// <remarks>
        /// Copy the shader references before initializing. Destroy the baker before releasing its borrowed camera.
        /// </remarks>
        public void InitializeForEditor(Camera camera)
        {
            _isEditorHosted = true;
            EnsureMaterial(true);
            currentMat = cumulusMat;
            resultColorAndDistance = new RenderTargetIdentifier[2];
            mCamera = camera;
            mCamera.aspect = 1f;
            RegisterBakeHook();
        }

        /// <summary>
        /// Bakes one layer's scaled clouds: the color cubemap, its underside and its normals.
        /// </summary>
        /// <remarks>
        /// Runs the same face-by-face sequence the game's baker does, from the planet's center outward. The planet
        /// rotation is reset to identity first, since the bake measures directions in the planet's own frame and
        /// nothing else sets it outside a running renderer.
        /// </remarks>
        /// <param name="config">The configuration the layer belongs to.</param>
        /// <param name="layer">The layer to bake.</param>
        /// <param name="color">The baked color cubemap.</param>
        /// <param name="bottom">The baked underside cubemap.</param>
        /// <param name="normals">The baked normal cubemap.</param>
        /// <returns>True if the bake finished, false otherwise.</returns>
        public bool BakeForEditor(
            VolumeCloudConfiguration config,
            VolumeCloudConfiguration.CumulusData layer,
            out Cubemap color,
            out Cubemap bottom,
            out Cubemap normals
        )
        {
            color = null;
            bottom = null;
            normals = null;
            if (!_isEditorHosted)
                return false;

            BeginHostedBake(config, layer);
            for (int render = 0; render < MAX_EDITOR_BAKE_RENDERS && !_isBakeComplete && !_bakeReadbackFailed; render++)
            {
                mCamera.Render();

                // The face the render recorded has executed by now, so it can be read back.
                if (!_isEditorReadbackPending)
                    continue;

                _isEditorReadbackPending = false;
                GenerateMipmapFile();
            }

            if (!_isBakeComplete || _bakeReadbackFailed)
                return false;

            if (FlatNormals)
            {
                FillNeutralNormals();
            }
            else
            {
                BakeNormalCubeMap();
            }
            if (_bakeReadbackFailed)
                return false;

            color = cloudsCubemap;
            bottom = cloudsBottomCubemap;
            normals = cloudsNormalCubemap;
            layer.bakedCloudVersion = BAKE_VERSION;
            cloudsCubemap = null;
            cloudsBottomCubemap = null;
            cloudsNormalCubemap = null;
            return true;
        }

        private void BeginHostedBake(VolumeCloudConfiguration config, VolumeCloudConfiguration.CumulusData layer)
        {
            isBakeCloudBottom = false;
            isBakeCloudBottomComplete = false;
            faceWidth = FaceSize;
            faceHeight = faceWidth;
            faceWidthOri = faceWidth;
            faceHeightOri = faceHeight;
            currentFace = CubemapFace.PositiveX;
            _isBakeComplete = false;
            _bakeReadbackFailed = false;
            config.SetEarthRotation(Matrix4x4.identity);
            config.EnsureVerticalColorMap();
            OnBakeCubemap(layer, config);
        }

        private void FillNeutralNormals()
        {
            for (int face = 0; face < 6; face++)
            {
                cloudsNormalCubemap.SetPixel((CubemapFace)face, 0, 0, new Color(0.5f, 0.5f, 1f, 1f));
            }
            cloudsNormalCubemap.Apply(false, false);
        }

        private void EnsureMaterial(bool force = false)
        {
            if (cumulusMat == null || force)
            {
                if (cumulusCloudShader != null)
                {
                    cumulusMat = new Material(cumulusCloudShader);
                }
                else
                {
                    Debug.LogError("cumulusCloudShader is null");
                }
            }

            if (listCreationMat == null || force)
            {
                if (listCreationShader != null)
                {
                    listCreationMat = new Material(listCreationShader);
                }
                else
                {
                    Debug.LogError("listCreationShader is null");
                }
            }

            if (listRenderingMat == null || force)
            {
                if (listRenderingShader != null)
                {
                    listRenderingMat = new Material(listRenderingShader);
                }
                else
                {
                    Debug.LogError("listRenderingShader is null");
                }
            }

            if (listDepthMat == null || force)
            {
                if (listDepthShader != null)
                {
                    listDepthMat = new Material(listDepthShader);
                }
                else
                {
                    Debug.LogError("listDepthShader is null");
                }
            }
        }

        private void OnDestroy()
        {
            CameraRenderHooks.Unregister(_bakeHook);
            if (raymarchedDistance != null)
            {
                for (int i = 0; i < raymarchedDistance.Length; i++)
                {
                    if (raymarchedDistance[i] != null)
                    {
                        DestroyOrImmediate(raymarchedDistance[i]);
                    }
                }
            }

            if (undersampleBuffer != null)
            {
                DestroyOrImmediate(undersampleBuffer);
            }

            if (downsampledDepth != null)
            {
                DestroyOrImmediate(downsampledDepth);
            }

            DestroyOrImmediate(normalRenderTarget);
            DestroyOrImmediate(cumulusMat);
            DestroyOrImmediate(listCreationMat);
            DestroyOrImmediate(listRenderingMat);
            DestroyOrImmediate(listDepthMat);
            DestroyOrImmediate(cloudsCubemap);
            DestroyOrImmediate(cloudsBottomCubemap);
            DestroyOrImmediate(cloudsNormalCubemap);

            if (resultColorAndDistance != null)
            {
                resultColorAndDistance = null;
            }

        }

        private void OnBakeCubemap(VolumeCloudConfiguration.CumulusData cumulusData, VolumeCloudConfiguration config)
        {
            // Sample positions are reflected through the shell. Keep the actual height profile and warps.
            firstCumulusData = cumulusData;
            configuration = config;
            cloudsCubemap = new Cubemap(faceWidth, TextureFormat.RGBAHalf, true);
            cloudsBottomCubemap = new Cubemap(faceWidth / 4, TextureFormat.RGBAHalf, true);
            cloudsNormalCubemap = new Cubemap(FlatNormals ? 1 : faceWidth, TextureFormat.RGBA32, !FlatNormals);
            BakeCubemap(firstCumulusData);
        }

        private void BakeCubemap(VolumeCloudConfiguration.CloudsData cloudsLayer)
        {
            EnsureMaterial();
            raymarchedDistance ??= new RenderTexture[2];
            EnsureRenderTarget(
                ref undersampleBuffer,
                faceWidth,
                faceHeight,
                RenderTextureFormat.ARGBHalf,
                FilterMode.Bilinear,
                "HistoryBuffer"
            );
            EnsureRenderTarget(
                ref raymarchedDistance[0],
                faceWidth,
                faceHeight,
                RenderTextureFormat.ARGB32,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                true
            );
            EnsureRenderTarget(
                ref raymarchedDistance[1],
                faceWidth,
                faceHeight,
                RenderTextureFormat.ARGB32,
                FilterMode.Bilinear,
                "RaymarchedDistance"
            );
            switch (currentFace)
            {
                case CubemapFace.PositiveX:
                    mCamera.transform.forward = Vector3.right;
                    break;
                case CubemapFace.NegativeX:
                    mCamera.transform.forward = Vector3.left;
                    break;
                case CubemapFace.PositiveY:
                    mCamera.transform.forward = Vector3.up;
                    break;
                case CubemapFace.NegativeY:
                    mCamera.transform.forward = Vector3.down;
                    break;
                case CubemapFace.PositiveZ:
                    mCamera.transform.forward = Vector3.forward;
                    break;
                case CubemapFace.NegativeZ:
                    mCamera.transform.forward = Vector3.back;
                    break;
                default:
                    mCamera.transform.forward = Vector3.right;
                    break;
            }

            PrepareCloudFace(firstCumulusData);
            EnableBakeCubemap = true;
        }

        private void RegisterBakeHook()
        {
            _bakeHook ??= new BakeHook(this);
            CameraRenderHooks.Register(_bakeHook);
        }

        private void AsyncMipmapFile()
        {
            AsyncNormalCallBack(ReadNow(normalRenderTarget, faceWidthOri, faceHeightOri, GraphicsFormat.R8G8B8A8_UNorm));
        }

        private void AsyncNormalCallBack(AsyncGPUReadbackRequest request)
        {
            if (request.hasError)
            {
                Debug.LogError("Failed to read normal cubemap data.");
                _bakeReadbackFailed = true;
                return;
            }

            cloudsNormalCubemap.SetPixelData(request.GetData<Color32>(), 0, currentShadowFace++);
            cloudsNormalCubemap.Apply();
        }

        private void BakeNormalCubeMap()
        {
            PrepareNormalCubeMap();
            using var normalCommands = new CommandBuffer { name = "Cloud Cubemap Normals" };
            for (int faceIndex = 0; faceIndex < 6; faceIndex++)
            {
                RenderNormalFace(normalCommands, faceIndex);
                AsyncMipmapFile();
            }
        }

        private void PrepareNormalCubeMap()
        {
            currentShadowFace = CubemapFace.PositiveX;
            currentMat.SetTexture("_CloudsShadowMap", cloudsCubemap);
            EnsureRenderTarget(
                ref normalRenderTarget,
                faceWidthOri,
                faceHeightOri,
                RenderTextureFormat.ARGBHalf,
                FilterMode.Bilinear,
                "normal"
            );
        }

        private void RenderNormalFace(CommandBuffer commands, int faceIndex)
        {
            mCamera.transform.forward = (CubemapFace)faceIndex switch
            {
                CubemapFace.PositiveX => Vector3.right,
                CubemapFace.NegativeX => Vector3.left,
                CubemapFace.PositiveY => Vector3.up,
                CubemapFace.NegativeY => Vector3.down,
                CubemapFace.PositiveZ => Vector3.forward,
                _ => Vector3.back
            };
            Matrix4x4 vpMatrix = mCamera.projectionMatrix * mCamera.worldToCameraMatrix;
            currentMat.SetMatrix("_VPMatrix", vpMatrix.inverse);
            commands.Clear();
            commands.SetRenderTarget(normalRenderTarget);
            commands.DrawProcedural(Matrix4x4.identity, currentMat, 12, MeshTopology.Triangles, 3);
            Graphics.ExecuteCommandBuffer(commands);
        }

        // The material and target set-up of a face. Stock did it from OnRenderImage, right before the draws that
        // RecordFace now records.
        private void PrepareCloudFace(VolumeCloudConfiguration.CloudsData cloudsLayer)
        {
            currentMat.EnableKeyword("BAKE_CLOUD");
            if (isBakeCloudBottom)
            {
                currentMat.EnableKeyword("BAKE_CLOUD_BOTTOM");
            }
            else
            {
                currentMat.DisableKeyword("BAKE_CLOUD_BOTTOM");
            }

            configuration.SetGameTimeScale(1f);
            configuration.SetEarthCenter(new Vector3(0f, 0f, 0f));
            configuration.SetCameraForward(currentMat, mCamera.transform.forward.normalized);
            configuration.SetCameraPosition(currentMat, mCamera.transform.position);
            currentMat.EnableKeyword("TRILINEAR_SAMPLE");

            currentMat.SetVector("_AllCloudsHeightRange", allCloudsLayersRange);
            configuration.ApplyToMaterial(currentMat);
            currentMat.SetVector("_ProjectionExtents", mCamera.GetProjectionExtents());
            currentMat.SetVector("_TexelSize", undersampleBuffer.texelSize);
            EnsureRenderTarget(
                ref downsampledDepth,
                faceWidth,
                faceHeight,
                RenderTextureFormat.RFloat,
                FilterMode.Point,
                "_DownsampledDepth"
            );
            currentMat.SetTexture("_DownsampledDepth", downsampledDepth);
            currentMat.SetTexture("_FormerResult", Texture2D.blackTexture);
            configuration.SetTextures(currentMat, cloudsLayer as VolumeCloudConfiguration.CumulusData);
            var layerData = new VolumeCloudConfiguration.CloudsLayerStructuredBuffer[1];
            Matrix4x4 cameraToWorldMatrix = mCamera.cameraToWorldMatrix;
            cameraToWorldMatrix.m01 = 0f - cameraToWorldMatrix.m01;
            cameraToWorldMatrix.m11 = 0f - cameraToWorldMatrix.m11;
            cameraToWorldMatrix.m21 = 0f - cameraToWorldMatrix.m21;
            cameraToWorldMatrix.m02 = 0f - cameraToWorldMatrix.m02;
            cameraToWorldMatrix.m12 = 0f - cameraToWorldMatrix.m12;
            cameraToWorldMatrix.m22 = 0f - cameraToWorldMatrix.m22;
            currentMat.SetVector(
                "_WindDirection",
                new Vector4(
                    cloudsLayer.windDirection.x,
                    cloudsLayer.windDirection.y,
                    cloudsLayer.movementSpeed,
                    cloudsLayer.evolveSpeed
                )
            );
            currentMat.SetFloat("_CloudTopOffset", cloudsLayer.topOffset);
            layerData[0].windDirection = new Vector4(
                cloudsLayer.windDirection.x,
                cloudsLayer.windDirection.y,
                cloudsLayer.movementSpeed,
                cloudsLayer.evolveSpeed
            );
            layerData[0].cloudTopOffset = cloudsLayer.topOffset;
            List<Vector4> lightsPos = new();
            List<float> lightsIntensity = new();
            List<Vector4> lightsColor = new();
            lightsPos.Add(new Vector4(0f, 0f, 0f, 0f));
            lightsIntensity.Add(1.4f);
            lightsColor.Add(new Color(1f, 1f, 1f, 1f));
            int num = 1;
            configuration.SetDirectionalLightCount(currentMat, num > 4 ? 4 : num);
            configuration.SetDirectionalLightDir(currentMat, lightsPos);
            configuration.SetDirectionalLightColor(currentMat, lightsColor);
            configuration.SetDirectionalLightIntensity(currentMat, lightsIntensity);
            configuration.SetRenderStartEnd(currentMat, 0f, float.MaxValue);
            currentMat.SetTexture("_AtmosphereColor", Texture2D.blackTexture);
            currentMat.SetTexture("_FormerSceneColor", Texture2D.blackTexture);
            currentMat.SetTexture("_FormerDistance", raymarchedDistance[0]);
            currentMat.SetMatrix("_MainCameraToWorld", cameraToWorldMatrix);
            currentMat.SetTexture("_FormerResult", Texture2D.blackTexture);
            currentMat.SetVector("_BakeSampling", BakeSampling(configuration, firstCumulusData, faceWidth));
            currentMat.SetInt("_FirstPass", 0);
            currentMat.SetInt("_CloudsLayerCount", 1);
            var cumulusData = cloudsLayer as VolumeCloudConfiguration.CumulusData;
            layerData[0].distributionMapIndex = 0;
            layerData[0].cloudsColorMapIndex = 0;
            layerData[0].cloudsShadowMapIndex = 0;
            layerData[0].cloudStartHeight = cumulusData.cloudHeightRange.x;
            layerData[0].cloudEndHeight = cumulusData.cloudHeightRange.y;
            layerData[0].bakedCloudHeight = cumulusData.bakedCloudHeight;
            layerData[0].rotation = Matrix4x4.identity;
            layerData[0].evolve = Matrix4x4.identity;
            layerData[0].baseTexIndex = 0;
            layerData[0].baseTile = cumulusData.baseTexureTile;
            layerData[0].cloudDensity = cumulusData.cloudsDensity;
            layerData[0].normalScale = cumulusData.normalScale;
            layerData[0].coverageScale = cumulusData.coverageScale;
            layerData[0].evanish = 0f;
            layerData[0].cloudsMaskBias = cumulusData.cloudsMaskBias;
            layerData[0].upperFalloff = cumulusData.upperFalloff;
            layerData[0].lowerFalloff = cumulusData.lowerFalloff;
            layerData[0].detailAmount = cumulusData.detailAmount;
            layerData[0].detailAltitudeShift = cumulusData.detailAltitudeShift;
            layerData[0].enableDetailTexture = cumulusData.enableDetailTexture ? 1 : 0;
            layerData[0].detailTexIndex = 0;
            layerData[0].detailTile = cumulusData.detailTextureTile;
            layerData[0].detailStrength = cumulusData.detailStrength;
            layerData[0].enableWind = cumulusData.enableWind ? 1 : 0;
            cloudsLayerData = configuration.SetCloudsLayersData(ref layerData);
            VolumeCloudConfiguration.BindCloudsLayers(currentMat, cloudsLayerData);
            resultColorAndDistance[0] = undersampleBuffer;
            resultColorAndDistance[1] = raymarchedDistance[1];
            currentMat.SetInt("_RTWidth", faceWidth);
            currentMat.SetInt("_RTHeight", faceHeight);
            currentMat.SetFloat("_ResolutionScale", faceWidth / (float)undersampleBuffer.width);
        }

        // Stock's OnRenderImage draws of a face: the camera depth copy, the distance clear and the raymarch, then the
        // read back and the face shown on the camera.
        private void RecordFace(CommandBuffer cmd, in CameraRenderTargets targets)
        {
            EnableBakeCubemap = false;
            Matrix4x4 projection = mCamera.projectionMatrix;
            cmd.SetGlobalMatrix(CloudPropertyHash.CLOUD_CAMERA_PROJECTION_ID, projection);
            cmd.SetGlobalMatrix(CloudPropertyHash.CLOUD_CAMERA_INV_PROJECTION_ID, projection.inverse);

            cmd.SetRenderTarget(downsampledDepth);
            cmd.DrawProcedural(Matrix4x4.identity, currentMat, 5, MeshTopology.Triangles, 3);
            // The prefab's stock shader has no kernels under D3D12.
            cloudsComputeShader = Redux.ReduxAssets.WithStockComputePort(cloudsComputeShader);
            int kernelIndex = cloudsComputeShader.FindKernel("ClearBuffer");
            cmd.SetComputeTextureParam(cloudsComputeShader, kernelIndex, "_LD_TexArray_Target", raymarchedDistance[0]);
            cmd.DispatchCompute(
                cloudsComputeShader,
                kernelIndex,
                (int)Math.Ceiling(faceWidth / 8f),
                (int)Math.Ceiling(faceHeight / 8f),
                1
            );
            cmd.SetRenderTarget(resultColorAndDistance, undersampleBuffer.depthBuffer);
            cmd.DrawProcedural(Matrix4x4.identity, currentMat, 0, MeshTopology.Triangles, 3);

            _isEditorReadbackPending = true;
            cmd.SetRenderTarget(targets.Color, targets.Depth);
        }

        // Reads back the face the hook recorded, for a hosted bake. The game's baker requests its read back from the
        // hook, behind the face's draws.
        private void GenerateMipmapFile()
        {
            AsyncGPUReadbackCallBack(ReadNow(undersampleBuffer, faceWidth, faceHeight));
        }

        private void AsyncGPUReadbackCallBack(AsyncGPUReadbackRequest request)
        {
            if (request.hasError)
            {
                Debug.LogError("Failed to read cubemap data.");
                _bakeReadbackFailed = true;
                return;
            }

            NativeArray<Vector2> randerTargetData = request.GetData<Vector2>();
            if (isBakeCloudBottom)
            {
                cloudsBottomCubemap.SetPixelData(randerTargetData, 0, currentFace);
            }
            else
            {
                cloudsCubemap.SetPixelData(randerTargetData, 0, currentFace);
            }

            switch (currentFace)
            {
                case CubemapFace.PositiveX:
                    currentFace = CubemapFace.NegativeX;
                    BakeCubemap(firstCumulusData);
                    break;
                case CubemapFace.NegativeX:
                    currentFace = CubemapFace.PositiveY;
                    BakeCubemap(firstCumulusData);
                    break;
                case CubemapFace.PositiveY:
                    currentFace = CubemapFace.NegativeY;
                    BakeCubemap(firstCumulusData);
                    break;
                case CubemapFace.NegativeY:
                    currentFace = CubemapFace.PositiveZ;
                    BakeCubemap(firstCumulusData);
                    break;
                case CubemapFace.PositiveZ:
                    currentFace = CubemapFace.NegativeZ;
                    BakeCubemap(firstCumulusData);
                    break;
                case CubemapFace.NegativeZ:
                    if (isBakeCloudBottom)
                    {
                        isBakeCloudBottomComplete = true;
                    }
                    else
                    {
                        isBakeCloudBottom = true;
                        faceWidth = cloudsBottomCubemap.width;
                        faceHeight = faceWidth;
                    }

                    if (isBakeCloudBottomComplete)
                    {
                        // Kept readable so the SDK can save them, and the host outlives the bake.
                        cloudsCubemap.Apply(true, false);
                        cloudsBottomCubemap.Apply(true, false);
                        _isBakeComplete = true;
                    }
                    else
                    {
                        currentFace = CubemapFace.PositiveX;
                        BakeCubemap(firstCumulusData);
                    }

                    break;
            }
        }

        // Reads a target back before returning, for a hosted bake, which drives the face sequence itself.
        private static AsyncGPUReadbackRequest ReadNow(RenderTexture target, int width, int height,
            GraphicsFormat format = GraphicsFormat.R16G16B16A16_SFloat)
        {
            AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(
                target,
                0,
                0,
                width,
                0,
                height,
                0,
                1,
                format
            );
            request.WaitForCompletion();
            return request;
        }

        // Keep density filtering finer than the integration step. Transition distance must not choose noise mips.
        public static Vector4 BakeSampling(VolumeCloudConfiguration config,
            VolumeCloudConfiguration.CumulusData layer, int faceSize)
        {
            float thickness = Mathf.Max(layer.cloudHeightRange.y - layer.cloudHeightRange.x, 0.001f);
            float step = Mathf.Clamp(config.raymarchStepSize, thickness / 2048f, thickness / 64f);
            float cloudSize = Mathf.Max(Mathf.Abs(config.overallSize), 0.001f);
            float baseVoxel = NoiseVoxelSize(layer.baseTexture, layer.baseTexureTile, cloudSize);
            float detailVoxel = layer.enableDetailTexture && layer.detailStrength > 0f && layer.detailTextureTile > 0f
                ? NoiseVoxelSize(layer.detailTexture, layer.detailTextureTile, cloudSize) : float.PositiveInfinity;
            float featureSize = Mathf.Min(thickness / 16f, Mathf.Min(baseVoxel, detailVoxel));
            float footprint = 2f * Mathf.Max(config.planetRadius + layer.cloudHeightRange.y, 1f) / Mathf.Max(faceSize, 1);
            int samples = Mathf.Clamp(Mathf.CeilToInt(footprint / Mathf.Max(featureSize, step)), 2, 4);
            // Each noise volume has its own resolution and tiling. Do not use a shared texture LOD.
            float baseLod = Mathf.Max(0f, Mathf.Log(step * 0.5f / baseVoxel, 2f));
            float detailLod = Mathf.Max(0f, Mathf.Log(step * 0.5f / detailVoxel, 2f));
            return new Vector4(baseLod, detailLod, step, samples);
        }

        private static float NoiseVoxelSize(Texture3D texture, float tiling, float cloudSize) =>
            texture != null && Mathf.Abs(tiling) > 0f
                ? cloudSize / (Mathf.Abs(tiling) * Mathf.Max(texture.width, Mathf.Max(texture.height, texture.depth)))
                : float.PositiveInfinity;

        // Records a scheduled face on the baker's own camera, after post-processing as stock's OnRenderImage ran.
        private static void DestroyOrImmediate(UnityEngine.Object value)
        {
            if (value != null)
                DestroyImmediate(value);
        }

        private static void EnsureRenderTarget(ref RenderTexture target, int width, int height,
            RenderTextureFormat format, FilterMode filter, string name, int depth = 0) =>
            EnsureRenderTarget(ref target, width, height, format, filter, TextureWrapMode.Clamp, false, depth, name);

        private static void EnsureRenderTarget(ref RenderTexture target, int width, int height,
            RenderTextureFormat format, FilterMode filter, TextureWrapMode wrap = TextureWrapMode.Clamp,
            bool randomWrite = false, int depth = 0, string name = "CloudBake")
        {
            if (target != null && target.width == width && target.height == height && target.format == format
                && target.filterMode == filter && target.wrapMode == wrap && target.enableRandomWrite == randomWrite)
                return;
            DestroyOrImmediate(target);
            target = new RenderTexture(width, height, depth, format)
            {
                filterMode = filter, wrapMode = wrap, enableRandomWrite = randomWrite, name = name
            };
            target.Create();
            using var commands = new CommandBuffer();
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(true, true, Color.clear);
            Graphics.ExecuteCommandBuffer(commands);
        }

        private sealed class BakeHook : CommandCameraRenderHook
        {
            private readonly VolumeCloudBakeCubemap _owner;

            public BakeHook(VolumeCloudBakeCubemap owner) : base("Volume Cloud Cubemap Bake")
            {
                _owner = owner;
            }

            public override RenderPassEvent Event => RenderPassEvent.AfterRenderingPostProcessing;

            // The face's depth copy samples _CameraDepthTexture, as stock's did.
            public override ScriptableRenderPassInput Inputs => ScriptableRenderPassInput.Depth;

            public override bool ShouldRender(Camera camera) =>
                _owner != null && _owner.EnableBakeCubemap && camera == _owner.mCamera;

            protected override void Execute(CommandBuffer cmd, in CameraRenderTargets targets) =>
                _owner.RecordFace(cmd, targets);
        }
    }
}
