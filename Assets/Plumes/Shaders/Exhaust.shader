// Shader: Redux/VFX/Exhaust
// Merged from decompiled SPIRV-Cross output, variables renamed and cleaned. URP version.
//
// One unlit pass: displacement and bend vertex animation, Fresnel, three-zone color gradient, traces and
// linear fog. The Built-in version's ForwardBase pass also computed per-vertex lights and SH that its
// fragment never read, and its ForwardAdd pass wrote black with One One blending. Both are gone.

Shader "Redux/VFX/Exhaust"
{
    Properties
    {
        [Header(Main)] _Alpha ("Alpha", Range(0, 1)) = 0
        _ScrollSpeedX ("Scroll Speed X", Float) = 0
        _ScrollSpeedY ("Scroll Speed Y", Float) = 0
        [Header(Color)] _ColorTintBoost ("Color Tint Boost", Range(0, 1)) = 0
        [HDR] _ColorTintStart ("Color Tint Start", Color) = (1,0,0,1)
        [HDR] _ColorTintMiddle ("Color Tint Middle", Color) = (0,1,0,0)
        [HDR] _ColorTintEnd ("Color Tint End", Color) = (0,0,1,1)
        _ColorTintOffset ("Color Tint Start Offset", Range(0, 1)) = 0
        _ColorTintFalloff ("Color Tint Start Gradient", Range(0, 1)) = 1
        _ColorTintMiddlePos ("Color Tint Middle Pos", Range(0, 1)) = 0.5
        _ColorTintEndOffset ("Color Tint End Offset", Range(0, 1)) = 0
        _ColorTintEndGradient ("Color Tint End Gradient", Range(0, 1)) = 1
        [Header(Noise)] _NoiseAmount ("Noise Amount", Range(0, 1)) = 1
        _NoiseStrength ("Noise Strength", Range(0.5, 5)) = 0
        _TextureOffsetX ("Texture Offset X", Float) = 0
        _TextureOffsetY ("Texture Offset Y", Float) = 0
        _TextureScaleX ("Texture Scale X", Float) = 1
        _TextureScaleY ("Texture Scale Y", Float) = 1
        [NoScaleOffset] _NoiseTexture ("NoiseTexture", 2D) = "white" {}
        [Header(Fresnel)] _TdotVScale ("TdotV Scale", Float) = 3
        _FresnelOuter ("Fresnel Outer", Range(0, 10)) = 10
        _FresnelOuterBeneath ("Fresnel Outer Beneath", Range(0, 10)) = 0
        _FresnelOuterErosionAmount ("Fresnel Outer Erosion Amount", Range(0, 1)) = 1
        _FresnelOuterErosionOffset ("Fresnel Outer Erosion Offset", Range(0, 1)) = 0.282353
        _FresnelOuterErosionFalloff ("Fresnel Outer Erosion Falloff", Range(0.001, 1)) = 0.8824706
        _FresnelInner ("Fresnel Inner", Range(0, 10)) = 10
        _FresnelInnerBeneath ("Fresnel Inner Beneath", Range(0, 10)) = 0
        [Header(Top Bottom Fade)] _TopGradientPosOffset ("Top Gradient Pos Offset", Range(0, 1)) = 0
        _TopGradientFalloff ("Top Gradient Falloff", Range(0, 1)) = 0.2439874
        _ErosionAmount ("Erosion Amount", Range(0, 1)) = 1
        _ErosionPosOffset ("Erosion Pos Offset", Range(-1, 1)) = 0.2945153
        _ErosionFalloffGradient ("Erosion Falloff Gradient", Range(0, 5)) = 0.2384171
        _ShockCellStrength ("Shock Cell Contraction", Range(0, .4)) = 0
        _ShockCellSpacing ("Shock Cell Spacing", Float) = .75
        _ShockCellOffset ("First Shock Cell Center", Float) = .492
        [Header(Vertex Displacement)] _VertexDispScale ("Vertex Disp Scale", Range(0, 10)) = 0
        _VertexDispContrast ("Vertex Disp Contrast", Range(0.5, 5)) = 0.5
        _VertexDispPosOffset ("Vertex Disp Pos Offset", Range(0, 1)) = 0
        _VertexDispFalloffGradient ("Vertex Disp Falloff Gradient", Range(0, 3)) = 0
        [NoScaleOffset] _DistortionTexture ("DistortionTexture", 2D) = "white" {}
        [Header(Exit Traces)] _TracesAmount ("Traces Amount", Range(0, 1)) = 0
        _TracesLength ("Traces Length", Range(1, 20)) = 1
        [IntRange] _TracesCount ("Traces Count", Range(0, 10)) = 3
        _TracesThickness ("Traces Thickness", Range(0.1, 4)) = 2
        _TracesStrength ("Traces Strength", Range(0, 5)) = 1
        _TracesVariation ("Traces Flow Variation", Range(0, 1)) = 0
        _LinearFlow ("Straight Radial Flow", Range(0, 1)) = 0
        _CoherentFlow ("Coherent Jet Flow", Range(0, 1)) = 0
        _JetShockEmission ("Integrated Jet Shock Emission", Range(0, 1)) = 0
        _TailFade ("Soft Plume Tail", Range(0, 1)) = 0
        _OuterLayerHighlights ("Outer Layer Warm Streaks", Range(0, 1)) = 0
        _PlumeAxialScale ("Plume Axial Scale", Float) = 1
        _VacuumDetailBlend ("Vacuum Methalox Detail", Range(0, 1)) = 0
        _FlameTail ("Turbulent Flame Tail", Range(0, 1)) = 0
        _LayerEdgeSoftness ("Soft Layer Boundaries", Range(0, 1)) = 0
        _TracesTopPosOffset ("Traces Top Pos Offset", Range(0, 1)) = 0.282353
        _TracesTopFalloffGradient ("Traces Top Falloff Gradient", Range(0, 2)) = 0.25
        [NoScaleOffset] _TracesTexture ("Traces Texture", 2D) = "white" {}
        [Header(Camera Distance Fade)] _CameraDistanceFadeLength ("Camera Distance Fade Length", Range(0, 50)) = 50
        _CameraDistanceFalloff ("Camera Distance Falloff", Range(0.01, 2)) = 1
        _CameraDistanceTopGradient ("Camera Distance Top Gradient", Range(0, 1)) = 0
        [Header(Bending)] _AccelerationDir ("AccelerationDir", Vector) = (0,0,0,0)
        _AccelerationScaleFactor ("AccelerationScaleFactor", Float) = 0
        _BendCenterOffset ("BendCenterOffset", Float) = 0
        _BendRotationOffset ("BendRotationOffset", Float) = 0
        _BendCenterOffsetMultipler ("BendCenterOffsetMultipler", Float) = 0
        [HideInInspector] _texcoord ("", 2D) = "white" {}
        [HideInInspector] __dirty ("", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "IsEmissive" = "true" "QUEUE" = "Transparent+0" "RenderType" = "Transparent" }

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorTintStart;
                float4 _ColorTintMiddle;
                float4 _ColorTintEnd;
                float4 _AccelerationDir;
                float4 _texcoord_ST;
                float _Alpha;
                float _ScrollSpeedX;
                float _ScrollSpeedY;
                float _ColorTintBoost;
                float _ColorTintOffset;
                float _ColorTintFalloff;
                float _ColorTintMiddlePos;
                float _ColorTintEndOffset;
                float _ColorTintEndGradient;
                float _NoiseAmount;
                float _NoiseStrength;
                float _TextureOffsetX;
                float _TextureOffsetY;
                float _TextureScaleX;
                float _TextureScaleY;
                float _TdotVScale;
                float _FresnelOuter;
                float _FresnelOuterBeneath;
                float _FresnelOuterErosionAmount;
                float _FresnelOuterErosionOffset;
                float _FresnelOuterErosionFalloff;
                float _FresnelInner;
                float _FresnelInnerBeneath;
                float _TopGradientPosOffset;
                float _TopGradientFalloff;
                float _ErosionAmount;
                float _ErosionPosOffset;
                float _ErosionFalloffGradient;
                float _ShockCellStrength;
                float _ShockCellSpacing;
                float _ShockCellOffset;
                float _VertexDispScale;
                float _VertexDispContrast;
                float _VertexDispPosOffset;
                float _VertexDispFalloffGradient;
                float _TracesAmount;
                float _TracesLength;
                float _TracesCount;
                float _TracesThickness;
                float _TracesStrength;
                float _TracesVariation;
                float _LinearFlow;
                float _CoherentFlow;
                float _JetShockEmission;
                float _TailFade;
                float _OuterLayerHighlights;
                float _PlumeAxialScale;
                float _VacuumDetailBlend;
                float _FlameTail;
                float _LayerEdgeSoftness;
                float _TracesTopPosOffset;
                float _TracesTopFalloffGradient;
                float _CameraDistanceFadeLength;
                float _CameraDistanceFalloff;
                float _CameraDistanceTopGradient;
                float _AccelerationScaleFactor;
                float _BendCenterOffset;
                float _BendRotationOffset;
                float _BendCenterOffsetMultipler;
                float __dirty;
            CBUFFER_END

            float flameHash(float3 p)
            {
                p = frac(p * .1031f);
                p += dot(p, p.yzx + 33.33f);
                return frac((p.x + p.y) * p.z);
            }

            float flameSpatialNoise(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                f = f * f * f * (f * (f * 6.0f - 15.0f) + 10.0f);
                float a = lerp(flameHash(cell), flameHash(cell + float3(1,0,0)), f.x);
                float b = lerp(flameHash(cell + float3(0,1,0)), flameHash(cell + float3(1,1,0)), f.x);
                float c = lerp(flameHash(cell + float3(0,0,1)), flameHash(cell + float3(1,0,1)), f.x);
                float d = lerp(flameHash(cell + float3(0,1,1)), flameHash(cell + float3(1,1,1)), f.x);
                return lerp(lerp(a,b,f.y), lerp(c,d,f.y), f.z);
            }
        ENDHLSL

        // Displacement and bend vertex animation, Fresnel, three-zone color gradient, traces overlay and
        // linear fog. Purely emissive, so URP draws it as an unlit pass in the transparent queue.
        Pass
        {
            Name "FORWARD"
            Tags { "IsEmissive" = "true" "QUEUE" = "Transparent+0" "RenderType" = "Transparent" }
            Blend SrcAlpha One, SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.0
            #pragma multi_compile _ FOG_LINEAR

            TEXTURE2D(_DistortionTexture);
            SAMPLER(sampler_DistortionTexture);
            TEXTURE2D(_TracesTexture);
            SAMPLER(sampler_TracesTexture);

            // Vertex stage statics
            static float4 gl_Position;
            static float4 in_pos;
            static float3 in_normal;
            static float4 in_texcoord;
            static float2 out_uv;
            static float3 out_worldNormal;
            static float3 out_worldPos;
            #ifdef FOG_LINEAR
            static float out_fogZ;
            #endif

            // Fragment stage statics
            static bool gl_FrontFacing;
            static float2 texcoord;
            static float3 worldNormal;
            static float3 worldPos;
            static float4 outColor;
            #ifdef FOG_LINEAR
            static float fogCoord;
            #endif

            struct Vertex_Stage_Input
            {
                float4 in_pos : POSITION;
                float3 in_normal : NORMAL;
                float4 in_texcoord : TEXCOORD;
            };

            struct Vertex_Stage_Output
            {
                float2 out_uv : TEXCOORD;
                float3 out_worldNormal : TEXCOORD1;
                float3 out_worldPos : TEXCOORD2;
            #ifdef FOG_LINEAR
                float out_fogZ : TEXCOORD4;
            #endif
                float4 gl_Position : SV_Position;
            };

            struct Fragment_Stage_Input
            {
                float2 texcoord : TEXCOORD;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            #ifdef FOG_LINEAR
                float fogCoord : TEXCOORD4;
            #endif
                bool gl_FrontFacing : SV_IsFrontFace;
            };

            struct Fragment_Stage_Output
            {
                float4 outColor : SV_Target0;
            };

            // Evaluates the smoothstep curve for a value already clamped to [0,1].
            // Returns t * t * (3 - 2t), identical to smoothstep(0,1,t) without the inner clamp.
            precise float smoothstepT(precise float t)
            {
                precise float tSq = t * t;
                return tSq * mad(t, -2.0f, 3.0f);
            }

            // Low-frequency value noise for dissipating gas, independent of the
            // curved filament atlas used by stock exhaust.
            float plumeHazeNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0f - 2.0f * f);
                // A wrapped angular lattice also supports seam-free outward advection.
                cell.x -= floor(cell.x / 32.0f) * 32.0f;
                float nextX = fmod(cell.x + 1.0f, 32.0f);
                float4 seeds = float4(dot(cell, float2(127.1f, 311.7f)),
                    dot(float2(nextX, cell.y), float2(127.1f, 311.7f)),
                    dot(cell + float2(0, 1), float2(127.1f, 311.7f)),
                    dot(float2(nextX, cell.y + 1.0f), float2(127.1f, 311.7f)));
                float4 values = frac(sin(seeds) * 43758.5453f);
                return lerp(lerp(values.x, values.y, f.x), lerp(values.z, values.w, f.x), f.y);
            }

            float shockCellRadius(float z)
            {
                float spacing = max(_ShockCellSpacing, .001f);
                float envelope = smoothstep(0.0f, spacing * .25f, z)
                               * (1.0f - smoothstep(_ShockCellOffset + spacing * 2.5f, _ShockCellOffset + spacing * 4.0f, z));
                float wave = .5f + .5f * cos(6.2831853f * (z - _ShockCellOffset) / spacing);
                return 1.0f - _ShockCellStrength * envelope * wave;
            }

            // Vertex shader. Output: displaced world-space vertex position, world normal and UV. Displacement and
            // bend are applied before the ObjectToWorld transform so they operate in local particle space.
            Vertex_Stage_Output vert(Vertex_Stage_Input stage_input)
            {
                in_pos = stage_input.in_pos;
                in_normal = stage_input.in_normal;
                in_texcoord = stage_input.in_texcoord;
                float3 plumeView = normalize(_WorldSpaceCameraPos - unity_ObjectToWorld._m03_m13_m23);
                float3 plumeAxis = normalize(unity_ObjectToWorld._m02_m12_m22);
                float axialSoftening = _LayerEdgeSoftness * smoothstep(.45f, .94f, abs(dot(plumeView, plumeAxis)));
                // Atmospheric mixing widens the tail into moving flame tongues.
                float flameV = saturate(1.0f - in_texcoord.y);
                float flameSpread = _FlameTail * smoothstep(.35f, .90f, flameV);
                float flameFlow = -in_pos.z * 4.0f - _Time.y * 32.0f;
                float flameWave = 2.0f * flameSpatialNoise(float3(in_pos.xy * 4.0f, flameFlow)) - 1.0f;
                float flameMotion = flameSpread * (1.0f - axialSoftening);
                // Keep the mixing layer wider than the core even between flame crests.
                float flameRadius = 1.0f + flameSpread * 1.15f + .35f * flameMotion * flameWave;
                float2 flameFold = float2(flameSpatialNoise(float3(7.1f, 3.7f, flameFlow)),
                    flameSpatialNoise(float3(19.3f, 11.9f, flameFlow))) - .5f;
                in_pos.xy += flameFold * length(in_pos.xy) * .65f * flameMotion;
                // Pressure-driven, opt-in extension keeps the nozzle anchored at z = 0.
                // Outer shells use a pressure curve to remain longer than the core.
                float vacuumLength = (1.0f + .12f * _VacuumDetailBlend) * max(_PlumeAxialScale, .001f);
                in_pos.z *= vacuumLength;
                in_normal.z /= vacuumLength;
                in_pos.xy *= flameRadius;
                in_normal = normalize(float3(in_normal.xy / flameRadius, in_normal.z));

                // Keep compression centers fixed on the shock cells while gas flows through.
                if (_ShockCellStrength > 0.0f)
                {
                    float z = -in_pos.z;
                    float radiusScale = lerp(shockCellRadius(z), 1.0f, axialSoftening * .95f);
                    float slope = (shockCellRadius(z + .001f) - shockCellRadius(z - .001f)) / .002f * (1.0f - axialSoftening * .95f);
                    in_normal = normalize(float3(in_normal.xy / radiusScale,
                        in_normal.z + dot(in_normal.xy, in_pos.xy) * slope / radiusScale));
                    in_pos.xy *= radiusScale;
                }

                // Displacement sample: _DistortionTexture scrolling in UV at _ScrollSpeedX/Y with a
                // contrast remap gives a displacement magnitude in [0,1].
                precise float dispU = in_texcoord.x * _TextureScaleX;
                precise float dispV = in_texcoord.y * _TextureScaleY;
                precise float negContrast    = -_VertexDispContrast;
                precise float contrastInv    = negContrast + 1.0f;      // 1 - contrast
                precise float negContrastInv = -contrastInv;
                precise float contrastRange  = negContrastInv + _VertexDispContrast;  // 2 * contrast - 1
                float dispSample = mad(SAMPLE_TEXTURE2D_LOD(_DistortionTexture, sampler_DistortionTexture,
                    float2(mad(_Time.y, _ScrollSpeedX, dispU), mad(_Time.y, _ScrollSpeedY, dispV)),
                    2.0f * _CoherentFlow).x, contrastRange, contrastInv);

                // Displacement to local offset: project the remapped sample along the vertex normal and scale.
                precise float dispNormalX = dispSample * in_normal.x;
                precise float dispNormalY = dispSample * in_normal.y;
                precise float dispNormalZ = dispSample * in_normal.z;
                precise float dispX = dispNormalX * _VertexDispScale;
                precise float dispY = dispNormalY * _VertexDispScale;
                precise float dispZ = dispNormalZ * _VertexDispScale;

                // Acceleration bend force: _AccelerationDir becomes a signed bend force via 1 - (|a|+1)^-2.5,
                // scaled to [-4, 4] per axis. Only X and Z feed into the rotation step.
                precise float accelAbsX1 = abs(_AccelerationDir.x) + 1.0f;
                precise float accelAbsY1 = abs(_AccelerationDir.y) + 1.0f;
                precise float accelAbsZ1 = abs(_AccelerationDir.z) + 1.0f;
                precise float accelCurveX = 1.0f - pow(accelAbsX1, -2.5f);  // in [0, 1)
                precise float accelCurveY = 1.0f - pow(accelAbsY1, -2.5f);
                precise float accelCurveZ = 1.0f - pow(accelAbsZ1, -2.5f);
                precise float accelSignedX = accelCurveX * sign(_AccelerationDir.x);
                precise float accelSignedY = accelCurveY * sign(_AccelerationDir.y);
                precise float accelSignedZ = accelCurveZ * sign(_AccelerationDir.z);
                precise float bendForceX = accelSignedX * 4.0f;
                precise float bendForceY = accelSignedY * 4.0f;
                precise float bendForceZ = accelSignedZ * 4.0f;

                // Bend rotation: rotate the XZ bend force by _BendRotationOffset * pi and scale by
                // _AccelerationScaleFactor to produce bend direction vectors (U, V).
                precise float bendScaledX = bendForceX * _AccelerationScaleFactor;
                precise float bendScaledZ = bendForceZ * _AccelerationScaleFactor;
                precise float bendRotRad = _BendRotationOffset * 3.1415927f;
                float bendSin = sin(bendRotRad);
                float bendCos = cos(bendRotRad);
                precise float negBendSin = -bendSin;
                float bendDirU = dot(float2(negBendSin, bendCos), float2(bendScaledX, bendScaledZ));
                float bendDirV = dot(float2(bendCos,    bendSin), float2(bendScaledX, bendScaledZ));

                // Bend center offset: quadratic curvature of the bend direction, scaled by _BendCenterOffset,
                // gives the lateral offset applied to the particle center point.
                precise float bendScaledU   = bendDirU * 0.16f;
                precise float bendScaledV   = bendDirV * 0.16f;
                precise float bendSqU       = bendScaledU * bendScaledU;
                precise float bendSqV       = bendScaledV * bendScaledV;
                precise float bendCurvatureU = sign(bendDirU) * bendSqU;
                precise float bendCurvatureV = sign(bendDirV) * bendSqV;
                precise float bendCenterU   = bendCurvatureU * _BendCenterOffsetMultipler;
                precise float bendCenterV   = bendCurvatureV * _BendCenterOffsetMultipler;
                precise float bendOffsetU   = bendCenterU * _BendCenterOffset;
                precise float bendOffsetV   = bendCenterV * _BendCenterOffset;

                // Displacement falloff: dispFalloffW is a smoothstep ramp along the particle length (vFlipped
                // axis) that controls how much displacement and bend each vertex gets. The bend curve shape
                // adds a further self-bending quadratic term.
                precise float falloffInvGradient = 1.0f / _VertexDispFalloffGradient;
                precise float negV       = -in_texcoord.y;
                precise float vFlipped   = negV + 1.0f;                   // 0 at top, 1 at base
                precise float negPosOffset = -_VertexDispPosOffset;
                precise float falloffV   = vFlipped + negPosOffset;
                precise float bendLenWeighted = length(float3(bendForceX, bendForceY, bendForceZ)) * vFlipped;
                float bendLenFactor  = mad(bendLenWeighted, 0.01f, 1.0f);
                precise float bendLenSq    = bendLenFactor * bendLenFactor;
                precise float negBendLenSq = -bendLenSq;
                float bendCurveShape = mad(bendLenSq, bendLenSq, negBendLenSq); // lenSq * lenSq - lenSq
                precise float bendCurveU = bendDirU * bendCurveShape;
                precise float bendCurveV = bendDirV * bendCurveShape;
                float dispFalloffT  = clamp(falloffInvGradient * falloffV, 0.0f, 1.0f);
                precise float dispFalloffW = smoothstepT(dispFalloffT);

                // Displaced local position: displacement, bend offset and bend curve make a local-space offset
                // added to the original vertex position before the world-space transform.
                precise float bendCurveSqU    = bendCurveU * bendCurveU;
                precise float bendCurveSqV    = bendCurveV * bendCurveV;
                precise float bendCurveShapeU = bendCurveSqU * sign(bendCurveU);
                precise float bendCurveShapeV = bendCurveSqV * sign(bendCurveV);
                precise float localX = mad(dispX, dispFalloffW, bendOffsetU) + bendCurveShapeU;
                precise float localY = mad(dispY, dispFalloffW, bendOffsetV) + bendCurveShapeV;
                precise float localZ = dispZ * dispFalloffW;

                // World and clip transform
                float4 worldPos = mul(unity_ObjectToWorld,
                    float4(localX + in_pos.x, localY + in_pos.y, localZ + in_pos.z, 1.0));
                float4 clipPos = mul(unity_MatrixVP, worldPos);
                gl_Position.x = clipPos.x;
                gl_Position.y = clipPos.y;
            #ifdef FOG_LINEAR
                gl_Position.z = clipPos.z;
                out_fogZ = clipPos.z;
            #else
                gl_Position.z = clipPos.z;
            #endif
                gl_Position.w = clipPos.w;

                // UV, world position and normal outputs
                out_worldPos = worldPos.xyz;
                out_uv.x = mad(in_texcoord.x, _texcoord_ST.x, _texcoord_ST.z);
                out_uv.y = mad(in_texcoord.y, _texcoord_ST.y, _texcoord_ST.w);
                // Normal: transpose(WorldToObj) * in_normal (same as mul(in_normal, WorldToObj))
                float3 worldNormal = normalize(mul(in_normal, (float3x3)unity_WorldToObject));
                out_worldNormal = worldNormal;

                Vertex_Stage_Output stage_output;
                stage_output.gl_Position = gl_Position;
                stage_output.out_uv = out_uv;
                stage_output.out_worldNormal = out_worldNormal;
                stage_output.out_worldPos = out_worldPos;
            #ifdef FOG_LINEAR
                stage_output.out_fogZ = out_fogZ;
            #endif
                return stage_output;
            }

            // Fragment shader. Output: rgba colour for the exhaust VFX particle.
            //   rgb = (traces * fresnelOuter + baseColor) * _Alpha, blended with fog if FOG_LINEAR.
            //   a   = 1.0
            Fragment_Stage_Output frag(Fragment_Stage_Input stage_input)
            {
                // Column 2 of objectToWorld: the object-space long axis in world space for the TdotV (axial dot
                // view) Fresnel blend term.
                float4 objectToWorld_c2 = float4(unity_ObjectToWorld[0][2], unity_ObjectToWorld[1][2], unity_ObjectToWorld[2][2], unity_ObjectToWorld[3][2]);

                gl_FrontFacing = stage_input.gl_FrontFacing;
                texcoord       = stage_input.texcoord;
                worldNormal    = stage_input.worldNormal;
                worldPos       = stage_input.worldPos;
            #ifdef FOG_LINEAR
                fogCoord = stage_input.fogCoord;
            #endif

                // Face normal: flip the interpolated world normal on back-facing fragments so that N.V
                // lighting is consistent on both sides of the (double-sided) particle plane.
                bool isFrontFace = gl_FrontFacing;
                precise float negNormalX = -worldNormal.x;
                precise float negNormalY = -worldNormal.y;
                precise float negNormalZ = -worldNormal.z;
                float faceNormalX = isFrontFace ? worldNormal.x : negNormalX;
                float faceNormalY = isFrontFace ? worldNormal.y : negNormalY;
                float faceNormalZ = isFrontFace ? worldNormal.z : negNormalZ;
                float3 fragNormal = normalize(float3(faceNormalX, faceNormalY, faceNormalZ));

                // View direction and N.V: fresnelSmooth = NdotV^2 * (3 - 2 NdotV), a smooth Fresnel polynomial
                // in [0,1]. safeViewDirNorm guards against near-zero viewDir when computing N.V.
                float3 viewDir = _WorldSpaceCameraPos - worldPos;
                float viewDirLenSq = dot(viewDir, viewDir);
                float3 viewDirNorm    = normalize(viewDir);
                float3 safeViewDirNorm = normalize(viewDir * rsqrt(max(viewDirLenSq, 0.001f)));
                float NdotV = min(abs(dot(fragNormal, safeViewDirNorm)), 1.0f);
                float ndotVPoly = mad(NdotV, -2.0f, 3.0f);          // (3 - 2 NdotV)
                precise float NdotVSq = NdotV * NdotV;
                precise float negNdotVPoly = -ndotVPoly;
                precise float fresnelSmooth = NdotVSq * ndotVPoly;   // NdotV^2 * (3 - 2 NdotV)

                // Fresnel exponents, TdotV blend: TdotV blends the Fresnel exponents between a front-on and a
                // beneath value so the effect looks different when viewed end-on and from the side.
                float3 objectNormDir = normalize(objectToWorld_c2.xyz);
                float TdotV = pow(abs(dot(objectNormDir, viewDirNorm)), _TdotVScale);
                precise float negFresnelInner = -_FresnelInner;
                precise float fresnelInnerRange = negFresnelInner + _FresnelInnerBeneath;
                float fresnelInnerExp = mad(TdotV, fresnelInnerRange, _FresnelInner);
                precise float negFresnelOuter = -_FresnelOuter;
                precise float fresnelOuterRange = negFresnelOuter + _FresnelOuterBeneath;
                float fresnelOuterExp = mad(TdotV, fresnelOuterRange, _FresnelOuter);

                // Camera top gradient and distance fade:
                // cameraFade = distanceFade * (1 - smoothstep(vFlipped / _CameraDistanceTopGradient)).
                // The top-gradient factor dims the particle near texcoord.y = 0 (top of mesh).
                // The distance factor fades it out beyond _CameraDistanceFadeLength.
                precise float camDistNorm = distance(worldPos, _WorldSpaceCameraPos) / _CameraDistanceFadeLength;
                precise float invCamTopGradient = 1.0f / _CameraDistanceTopGradient;
                precise float negV = -texcoord.y;
                precise float negU = -texcoord.x;
                precise float vFlipped = negV + 1.0f;   // 1 - v  (0 at top, 1 at base of particle)
                precise float uFlipped = negU + 1.0f;   // 1 - u
                float camTopGradT = clamp(invCamTopGradient * vFlipped, 0.0f, 1.0f);
                precise float cameraFade = min(pow(camDistNorm, _CameraDistanceFalloff), 1.0f)
                                         * max(1.0f - smoothstepT(camTopGradT), 0.0f);

                // Outer Fresnel and erosion threshold:
                // fresnelOuterRaw is fresnelSmooth raised to an exponent modulated by cameraFade.
                // erosionThreshold is the noise level below which outer Fresnel starts to erode.
                precise float negFresnelOuterExp = -fresnelOuterExp;
                precise float negErosionAmount   = -_FresnelOuterErosionAmount;
                float fresnelOuterRaw = pow(fresnelSmooth, mad(cameraFade, negFresnelOuterExp, fresnelOuterExp));
                precise float erosionThreshold = (fresnelOuterRaw - _FresnelOuterErosionOffset)
                                               / _FresnelOuterErosionFalloff;

                // Distortion texture samples: two samples at different scroll directions provide noise channels
                // R and G whose product forms a structured, animated noise pattern.
                precise float scrollTimeY   = _ScrollSpeedY * _Time.y;
                precise float negScrollSpeedX = -_ScrollSpeedX;
                float scaledU = mad(texcoord.x, _TextureScaleX, _TextureOffsetX);
                float scaledV = mad(texcoord.y, _TextureScaleY, _TextureOffsetY);
                float4 distortSample0 = SAMPLE_TEXTURE2D(_DistortionTexture, sampler_DistortionTexture,
                    float2(mad(_Time.y, _ScrollSpeedX, scaledU), mad(_Time.y, _ScrollSpeedY, scaledV)));
                float distortR = distortSample0.x;
                float4 distortSample1 = SAMPLE_TEXTURE2D(_DistortionTexture, sampler_DistortionTexture,
                    float2(mad(_Time.y, negScrollSpeedX, scaledU), mad(1.3f, scrollTimeY, scaledV)));
                float distortG = distortSample1.y;
                if (_CoherentFlow > 0.0f)
                {
                    // Both channels advect together, avoiding beats between scrolling layers.
                    float2 flowUV = float2(scaledU, scaledV) + _Time.y * float2(_ScrollSpeedX, _ScrollSpeedY);
                    float2 flowNoise = SAMPLE_TEXTURE2D_BIAS(_DistortionTexture, sampler_DistortionTexture, flowUV, 3.5f).rg;
                    distortR = lerp(distortR, (.5f + .22f * (flowNoise.r - .5f)), _CoherentFlow);
                    distortG = lerp(distortG, (.5f + .22f * (flowNoise.g - .5f)), _CoherentFlow);
                }
                // Angular intensity is independent of downstream position: texture features
                // form straight rays instead of bending through the sheet's radial UVs.
                float2 radialNoise = SAMPLE_TEXTURE2D(_DistortionTexture, sampler_DistortionTexture,
                    float2(scaledU, _Time.y * .04f)).rg;
                radialNoise = lerp(float2(.5f, .5f), radialNoise, .4f);
                float flowPulse = .85f + .15f * sin(vFlipped * 18.0f - _Time.y * 12.0f + radialNoise.r * 6.0f);
                distortR = lerp(distortR, radialNoise.r * flowPulse, _LinearFlow);
                distortG = lerp(distortG, radialNoise.g, _LinearFlow);

                // Noise value:
                // noiseMask:  1 - _NoiseAmount * (1 - R * G), masks the particle shape by noise.
                // noiseValue: R * G remapped with _NoiseStrength contrast for erosion thresholding.
                precise float noiseProduct = distortG * distortR;
                float noiseMask = clamp(mad(_NoiseAmount, mad(distortR, distortG, -1.0f), 1.0f), 0.0f, 1.0f);
                precise float negNoiseStrength    = -_NoiseStrength;
                precise float noiseStrengthInv    = negNoiseStrength + 1.0f;
                precise float negNoiseStrengthInv = -noiseStrengthInv;
                precise float noiseStrengthRange  = negNoiseStrengthInv + _NoiseStrength;
                float noiseValue = clamp(mad(noiseProduct, noiseStrengthRange, noiseStrengthInv), 0.0f, 1.0f);

                // Erosion blend to final outer Fresnel: when noise < erosionThreshold the outer Fresnel is pulled
                // back toward the eroded version, controlled by _FresnelOuterErosionAmount and cameraFade.
                precise float negErosionThreshold   = -erosionThreshold;
                precise float noiseMinusThreshold   = negErosionThreshold + noiseValue;
                precise float negNoiseMinusThreshold = -noiseMinusThreshold;
                precise float fresnelMinusNoise     = negNoiseMinusThreshold + fresnelOuterRaw;
                precise float negFresnelOuterRaw    = -fresnelOuterRaw;
                precise float erosionBlend = negFresnelOuterRaw + clamp(fresnelMinusNoise, 0.0f, 1.0f);
                float fresnelOuter = mad(mad(cameraFade, negErosionAmount, _FresnelOuterErosionAmount),
                                         erosionBlend, fresnelOuterRaw);
                fresnelOuter = lerp(fresnelOuter, sqrt(max(fresnelOuter, 0.0f)), _LinearFlow);

                // Top gradient mask: smoothstep fade that suppresses the top of the particle shape.
                // innerFresnel = pow(1 - fresnelSmooth, fresnelInnerExp) * fresnelOuter adds a bright glowing
                // rim visible at grazing view angles.
                precise float topGradV = vFlipped - _TopGradientPosOffset;
                float topGradT  = clamp(topGradV / _TopGradientFalloff, 0.0f, 1.0f);
                float topGradMask = min(smoothstepT(topGradT), 1.0f);
                precise float innerFresnel = pow(max(mad(negNdotVPoly, NdotVSq, 1.0f), 0.001f),
                                                 fresnelInnerExp) * fresnelOuter;
                precise float fresnelGated = topGradMask * innerFresnel;

                // Erosion mask and base alpha: noise-driven dissolve from the particle base upward. Pixels where
                // (noiseValue - erosionPos - 1) < 0 are clipped by _ErosionAmount.
                // baseAlpha = noiseMask * erosionMask * fresnelGated.
                precise float erosionV      = vFlipped - _ErosionPosOffset;
                precise float erosionPosRaw = erosionV / _ErosionFalloffGradient;
                precise float negErosionPosRaw      = -erosionPosRaw;
                precise float noiseMinusErosionPos  = noiseValue + negErosionPosRaw;
                precise float erosionInput          = noiseMinusErosionPos + (-1.0f);
                float erosionMask   = clamp(mad(_ErosionAmount, erosionInput, 1.0f), 0.0f, 1.0f);
                precise float maskedFresnel = erosionMask * fresnelGated;
                precise float baseAlpha     = noiseMask * maskedFresnel;

                // Color gradient: three zones keyed on vFlipped, start (base), middle, end (top).
                // Gradient widths are noise-modulated (_ColorTintEndGradient, _ColorTintFalloff).
                //
                // Upper zone (vFlipped >= _ColorTintMiddlePos): lerp Middle to End.
                precise float noisedEndGradient = noiseMask * _ColorTintEndGradient;
                // Keep methalox color mixing broad, independent of density noise.
                float colorMixing = saturate(max(_FlameTail, _VacuumDetailBlend));
                float colorMiddlePos = lerp(_ColorTintMiddlePos, max(_ColorTintMiddlePos, .42f), colorMixing);
                precise float negMiddlePos   = -colorMiddlePos;
                precise float upperZoneRange = negMiddlePos + 1.0f;          // 1 - middlePos
                precise float vFromMiddle    = vFlipped + negMiddlePos;      // vFlipped - middlePos
                precise float upperZoneT     = vFromMiddle / upperZoneRange; // normalised upper-zone pos
                precise float negEndOffset   = -_ColorTintEndOffset;
                precise float endGradV       = upperZoneT + negEndOffset;
                float colorEndT     = clamp((1.0f / noisedEndGradient) * endGradV, 0.0f, 1.0f);
                precise float colorEndSmooth = smoothstepT(colorEndT);
                precise float negMiddleR = -_ColorTintMiddle.x;
                precise float negMiddleG = -_ColorTintMiddle.y;
                precise float negMiddleB = -_ColorTintMiddle.z;
                precise float colorEndDeltaR = negMiddleR + _ColorTintEnd.x;
                precise float colorEndDeltaG = negMiddleG + _ColorTintEnd.y;
                precise float colorEndDeltaB = negMiddleB + _ColorTintEnd.z;
                float isUpperZone = (vFlipped >= colorMiddlePos) ? 1.0f : 0.0f;
                precise float upperColorR = isUpperZone * mad(colorEndSmooth, colorEndDeltaR, _ColorTintMiddle.x);
                precise float upperColorG = isUpperZone * mad(colorEndSmooth, colorEndDeltaG, _ColorTintMiddle.y);
                precise float upperColorB = isUpperZone * mad(colorEndSmooth, colorEndDeltaB, _ColorTintMiddle.z);
                // Lower zone (vFlipped < _ColorTintMiddlePos): lerp Middle to Start.
                precise float noisedFalloff  = lerp(noiseMask * _ColorTintFalloff, 1.0f, colorMixing);
                precise float lowerZoneT     = vFlipped / colorMiddlePos;  // normalised lower-zone pos
                precise float negLowerZoneT  = -lowerZoneT;
                precise float invLowerZoneT  = negLowerZoneT + 1.0f;            // 1 - lowerZoneT
                precise float negColorOffset = -_ColorTintOffset;
                precise float startGradV     = invLowerZoneT + negColorOffset;
                float colorStartT     = clamp((1.0f / noisedFalloff) * startGradV, 0.0f, 1.0f);
                precise float colorStartSmooth = smoothstepT(colorStartT);
                precise float colorStartDeltaR = negMiddleR + _ColorTintStart.x;
                precise float colorStartDeltaG = negMiddleG + _ColorTintStart.y;
                precise float colorStartDeltaB = negMiddleB + _ColorTintStart.z;
                float isLowerZone = (colorMiddlePos >= vFlipped) ? 1.0f : 0.0f;
                float tintR = mad(isLowerZone, mad(colorStartSmooth, colorStartDeltaR, _ColorTintMiddle.x), upperColorR);
                float tintG = mad(isLowerZone, mad(colorStartSmooth, colorStartDeltaG, _ColorTintMiddle.y), upperColorG);
                float tintB = mad(isLowerZone, mad(colorStartSmooth, colorStartDeltaB, _ColorTintMiddle.z), upperColorB);
                // Keep vacuum color transitions broad and independent of density noise.
                // Reach the warm tail color before the strand becomes transparent.
                float3 flowTint = lerp(_ColorTintStart.rgb, _ColorTintMiddle.rgb,
                    smoothstep(.02f, .40f, vFlipped));
                flowTint = lerp(flowTint, _ColorTintEnd.rgb, smoothstep(.26f, .60f, vFlipped));
                tintR = lerp(tintR, flowTint.r, _LinearFlow);
                tintG = lerp(tintG, flowTint.g, _LinearFlow);
                tintB = lerp(tintB, flowTint.b, _LinearFlow);
                // The diffuse envelope stays purple. Warmth is applied only to the moving
                // streak overlay below, never as a continuous gradient at the outer edge.
                float3 vacuumTint = float3(tintR, tintG, tintB);

                // Let the broad outer envelope survive, but dim it progressively downstream.
                vacuumTint *= 1.0f - .28f * _VacuumDetailBlend * smoothstep(.15f, .90f, vFlipped);
                tintR = vacuumTint.r;
                tintG = vacuumTint.g;
                tintB = vacuumTint.b;

                // Boost: tint * (1 + _ColorTintBoost), then scale by baseAlpha.
                float boostedR = mad(tintR, _ColorTintBoost, tintR);
                float boostedG = mad(tintG, _ColorTintBoost, tintG);
                float boostedB = mad(tintB, _ColorTintBoost, tintB);
                precise float colorR = baseAlpha * boostedR;
                precise float colorG = baseAlpha * boostedG;
                precise float colorB = baseAlpha * boostedB;

                // Traces: sinusoidal streak overlay sampled from the trace texture, then masked by top gradient,
                // erosion, and noise before compositing.
                precise float tracesFreq    = _TracesCount * 6.2831855f;   // TracesCount * 2 pi
                // Periodic angular fields keep the UV seam continuous while individual rays
                // wander and change reach. Variation defaults to zero for stock trace behavior.
                float traceAngle = texcoord.x * 6.2831855f;
                // In straight flow, unequal broad angular lobes replace the evenly spaced
                // narrow bands. No downstream coordinate enters their angular position.
                float broadRays = .5f + .22f * sin(traceAngle * 3.0f + _Time.y * .035f)
                                      + .16f * sin(traceAngle * 7.0f - 1.8f - _Time.y * .025f)
                                      + .10f * sin(traceAngle * 11.0f + .8f);
                float traceTime = _Time.y * lerp(.8f, 1.3f, _VacuumDetailBlend) + _TracesCount;
                float traceWarp = .055f * sin(traceAngle * 2.0f + traceTime)
                                + .025f * sin(traceAngle * 5.0f - traceTime * .73f + vFlipped * 3.0f);
                float traceFlowU = texcoord.x + _TracesVariation * traceWarp;
                precise float tracesU = tracesFreq * traceFlowU;
                // A lower band exponent widens vacuum streaks without extending their reach.
                float traceExponent = _TracesThickness * lerp(1.0f, .75f, _VacuumDetailBlend);
                float tracesMask = pow(max(sin(mad(tracesFreq, 1.0f - traceFlowU, 1.5707964f)), 0.0001f), traceExponent);
                float traceReach = lerp(.48f, .31f, _VacuumDetailBlend) + .13f * sin(traceAngle * 3.0f - traceTime * .61f);
                float traceEnvelope = 1.0f - smoothstep(traceReach - .20f, traceReach + .20f, vFlipped);
                float traceBreakup = (.72f + .28f * sin(traceAngle * 4.0f + traceTime * .47f))
                                   * lerp(.55f, 1.0f, saturate(distortR * distortG));
                tracesMask *= lerp(1.0f, traceEnvelope * traceBreakup, _TracesVariation);
                tracesMask = lerp(tracesMask, smoothstep(.32f, .82f, broadRays), _LinearFlow);
                precise float tracesSinInput      = tracesU * 0.5f;
                precise float tracesLengthV       = vFlipped * _TracesLength;
                precise float tracesTopPos        = vFlipped - _TracesTopPosOffset;
                // UV into the trace atlas from the sin band and length offset.
                precise float tracesSinCentered    = abs(sin(tracesSinInput)) + (-0.5f);
                precise float tracesLengthCentered = tracesLengthV + (-0.5f);
                precise float tracesTexU = dot(float2(tracesSinCentered, tracesLengthCentered),
                                               float2(-1.0f, 1.2246468525851678544463796427522e-16f)) + 0.5f;
                precise float tracesTexV = dot(float2(tracesSinCentered, tracesLengthCentered),
                                               float2(-1.2246468525851678544463796427522e-16f, -1.0f)) + 0.5f;
                float4 tracesTex = SAMPLE_TEXTURE2D(_TracesTexture, sampler_TracesTexture, float2(tracesTexU, tracesTexV));
                // The stock trace atlas contains curved filaments. Straight flow uses the
                // angular mask directly, with an irregular but smoothly fading radial reach.
                float radialReach = .48f + .22f * radialNoise.r;
                float radialFade = 1.0f - smoothstep(radialReach - .25f, radialReach, vFlipped);
                tracesTex = lerp(tracesTex, float4(radialFade, radialFade, radialFade, 1.0f), _LinearFlow);
                tracesTex.rgb *= 1.0f + _LinearFlow * .6f * (1.0f - smoothstep(.05f, .35f, vFlipped));
                // Mask by tracesMask, then apply top falloff smoothstep.
                precise float tracesBlendR = tracesMask * tracesTex.x;
                precise float tracesBlendG = tracesMask * tracesTex.y;
                precise float tracesBlendB = tracesMask * tracesTex.z;
                float tracesTopT    = clamp(tracesTopPos / _TracesTopFalloffGradient, 0.0f, 1.0f);
                float tracesTopMask = min(smoothstepT(tracesTopT), 1.0f);
                precise float tracesFinalR = tracesTopMask * tracesBlendR;
                precise float tracesFinalG = tracesTopMask * tracesBlendG;
                precise float tracesFinalB = tracesTopMask * tracesBlendB;
                // Tint, scale by _TracesAmount * _TracesStrength, then gate by topGrad and erosion masks.
                // Occasional packets of warm gas travel along the existing streaks. The
                // trace mask supplies their narrow shape. This noise only controls when each
                // portion warms. There is no shared gate that extinguishes every streak at once.
                float highlightPulse = plumeHazeNoise(float2(texcoord.x * 32.0f,
                    vFlipped * 3.0f - _Time.y * 2.4f));
                float highlightMix = smoothstep(.62f, .82f, highlightPulse)
                    * smoothstep(.10f, .22f, vFlipped)
                    * _OuterLayerHighlights * _VacuumDetailBlend;
                float3 traceTint = lerp(float3(boostedR, boostedG, boostedB),
                    float3(.65f, .17f, .035f) * (1.0f + _ColorTintBoost), highlightMix);
                precise float tracesTintR = tracesFinalR * traceTint.r;
                precise float tracesTintG = tracesFinalG * traceTint.g;
                precise float tracesTintB = tracesFinalB * traceTint.b;
                precise float tracesAmountR = tracesTintR * _TracesAmount;
                precise float tracesAmountG = tracesTintG * _TracesAmount;
                precise float tracesAmountB = tracesTintB * _TracesAmount;
                // Lift only the streak overlay and preserve the dim, diffuse purple envelope.
                float traceStrength = _TracesStrength * (1.0f + .08f * _VacuumDetailBlend);
                precise float tracesStrR = tracesAmountR * traceStrength;
                precise float tracesStrG = tracesAmountG * traceStrength;
                precise float tracesStrB = tracesAmountB * traceStrength;
                precise float tracesMaskedR = topGradMask * tracesStrR;
                precise float tracesMaskedG = topGradMask * tracesStrG;
                precise float tracesMaskedB = topGradMask * tracesStrB;
                precise float tracesErodedR = erosionMask * tracesMaskedR;
                precise float tracesErodedG = erosionMask * tracesMaskedG;
                precise float tracesErodedB = erosionMask * tracesMaskedB;
                precise float tracesR = noiseMask * tracesErodedR;
                precise float tracesG = noiseMask * tracesErodedG;
                precise float tracesB = noiseMask * tracesErodedB;

                // Final composite: outRGB = saturate(traces * fresnelOuter + baseColor) * _Alpha.
                // The FOG_LINEAR variant blends with unity_FogColor using a linear depth fog factor.
            #ifdef FOG_LINEAR
                precise float negFogR = -unity_FogColor.x;
                precise float negFogG = -unity_FogColor.y;
                precise float negFogB = -unity_FogColor.z;
                precise float fogDepthNorm  = fogCoord / _ProjectionParams.y;
                precise float negFogDepth   = -fogDepthNorm;
                precise float fogEyeDepth   = negFogDepth + 1.0f;
                precise float fogLinearDist = fogEyeDepth * _ProjectionParams.z;
                float fogFactor = clamp(mad(max(fogLinearDist, 0.0f), unity_FogParams.z, unity_FogParams.w), 0.0f, 1.0f);
                outColor.x = mad(fogFactor, mad(_Alpha, clamp(mad(tracesR, fresnelOuter, colorR), 0.0f, 1.0f), negFogR), unity_FogColor.x);
                outColor.y = mad(fogFactor, mad(_Alpha, clamp(mad(tracesG, fresnelOuter, colorG), 0.0f, 1.0f), negFogG), unity_FogColor.y);
                outColor.z = mad(fogFactor, mad(_Alpha, clamp(mad(tracesB, fresnelOuter, colorB), 0.0f, 1.0f), negFogB), unity_FogColor.z);
                outColor.w = 1.0f;
            #else
                precise float finalR = clamp(mad(tracesR, fresnelOuter, colorR), 0.0f, 1.0f) * _Alpha;
                precise float finalG = clamp(mad(tracesG, fresnelOuter, colorG), 0.0f, 1.0f) * _Alpha;
                precise float finalB = clamp(mad(tracesB, fresnelOuter, colorB), 0.0f, 1.0f) * _Alpha;
                outColor.x = finalR;
                outColor.y = finalG;
                outColor.z = finalB;
                outColor.w = 1.0f;
            #endif
                // Fade emission, including traces and fog, before the open mesh boundary.
                // Noise changes where the fade begins, but its endpoint always stays inside the mesh.
                float tailStart = .48f + .14f * saturate(distortR * distortG);
                float flameBlend = _FlameTail * smoothstep(.43f, .72f, vFlipped);
                if (flameBlend > 0.0f)
                {
                    float3 flameUV = mul(unity_WorldToObject, float4(worldPos, 1.0f)).xyz;
                    // Separate tongues across the widened shell, elongated along the flow.
                    // Independent transverse variation prevents whole cross-sections going dark.
                    flameUV = float3(flameUV.xy * 9.0f, -flameUV.z * 2.0f);
                    flameUV.z -= _Time.y * 16.0f;
                    float3 warpUV = flameUV * .43f;
                    float3 flameWarp = float3(flameSpatialNoise(warpUV),
                        flameSpatialNoise(warpUV + 9.7f), flameSpatialNoise(warpUV + 23.1f));
                    flameUV += (flameWarp - .5f) * 1.2f;
                    float flameNoise = 0.0f;
                    float flameWeight = 0.0f;
                    float octaveWeight = .56f;
                    for (int octave = 0; octave < 2; octave++)
                    {
                        float footprint = max(length(ddx(flameUV)), length(ddy(flameUV)));
                        float weight = octaveWeight * (1.0f - smoothstep(.35f, 1.0f, footprint));
                        flameNoise += flameSpatialNoise(flameUV) * weight;
                        flameWeight += weight;
                        flameUV = flameUV.yzx * 2.03f + float3(3.7f, 11.9f, 7.1f);
                        octaveWeight *= .18f;
                    }
                    flameNoise = flameWeight > .001f ? flameNoise / flameWeight : .5f;
                    float flameDensity = smoothstep(.22f, .70f, flameNoise);
                    tailStart = lerp(tailStart, .66f + .16f * flameNoise, flameBlend);
                    float3 flameTint = lerp(_ColorTintMiddle.rgb, _ColorTintEnd.rgb,
                        smoothstep(.30f, .75f, vFlipped));
                    float3 flameEmission = flameTint * _Alpha * fresnelOuter * topGradMask
                        * (.45f + 1.9f * flameDensity);
                    outColor.xyz = lerp(outColor.xyz, flameEmission, flameBlend);
                }
                float tailMask = 1.0f - smoothstep(tailStart, .985f, vFlipped);
                outColor.xyz *= lerp(1.0f, tailMask, _TailFade);
                // Each straight strand has one moving endpoint. A monotonic fade
                // keeps its tip connected to the nozzle instead of forming outer islands.
                float strandLength = plumeHazeNoise(float2(texcoord.x * 32.0f, _Time.y * 8.0f));
                float rayEnd = .44f + .14f * saturate(broadRays) + .08f * strandLength;
                float rayEndFade = 1.0f - smoothstep(rayEnd - .24f, rayEnd + .09f, vFlipped);
                outColor.xyz *= lerp(1.0f, rayEndFade, _LinearFlow);
                float rayBrightness = 1.4f * (.65f + .55f * smoothstep(.25f, .75f, broadRays));
                float centerBrightness = 1.0f + .25f * (1.0f - smoothstep(0.0f, .4f, vFlipped));
                outColor.xyz *= lerp(1.0f, rayBrightness * centerBrightness, _LinearFlow);
                if (_LinearFlow > 0.0f)
                {
                    // Translate density downstream at roughly one plume length per second.
                    // The nonzero floor keeps the flow connected rather than making dashes.
                    float stream = plumeHazeNoise(float2(texcoord.x * 32.0f,
                        vFlipped * 5.0f - _Time.y * 6.0f));
                    float streamIntensity = .55f + .9f * stream;
                    // Fine density detail follows the same downstream flow without bending strands.
                    float streamDetail = plumeHazeNoise(float2(texcoord.x * 64.0f,
                        vFlipped * 18.0f - _Time.y * 21.6f));
                    streamIntensity *= .88f + .24f * streamDetail;
                    outColor.xyz *= lerp(1.0f, streamIntensity,
                        _LinearFlow * smoothstep(.015f, .12f, vFlipped));
                    // Hot gas just outside the exit remains luminous from the side,
                    // where the broad sheet's normal-facing emission is much weaker.
                    float nozzleGlow = 1.0f - smoothstep(0.0f, .035f, vFlipped);
                    nozzleGlow *= nozzleGlow * topGradMask * (0.5f + 0.5f * fresnelOuter);
                    float3 nozzleTint = lerp(_ColorTintStart.rgb, float3(.8f, .85f, 1.0f), .6f);
                    outColor.xyz += nozzleTint * nozzleGlow * _Alpha * _LinearFlow * 2.0f;
                }
                // Compression cells brighten the continuous jet instead of separate opaque diamonds.
                // Their spacing matches the radial constrictions and their contrast fades downstream.
                if (_JetShockEmission > 0.0f)
                {
                    float jetZ = -mul(unity_WorldToObject, float4(worldPos, 1.0f)).z;
                    float spacing = max(_ShockCellSpacing, .001f);
                    float phase = (jetZ - _ShockCellOffset) / spacing;
                    float wave = .5f + .5f * cos(6.2831853f * phase);
                    float envelope = smoothstep(0.0f, spacing * .25f, jetZ)
                        * (1.0f - smoothstep(2.5f, 4.0f, phase));
                    float cell = smoothstep(.35f, .95f, wave) * envelope;
                    float downstreamFade = exp2(-.3f * max(phase, 0.0f));
                    float glow = .65f + 2.4f * cell * downstreamFade;
                    outColor.xyz *= lerp(1.0f, glow, _JetShockEmission);
                }
                // Feather the projected shell silhouette without hiding axial views.
                float3 shellAxis = normalize(objectNormDir);
                float3 radialView = viewDirNorm - shellAxis * dot(viewDirNorm, shellAxis);
                float3 radialNormal = worldNormal - shellAxis * dot(worldNormal, shellAxis);
                float radialViewLength = length(radialView);
                float edgeFacing = abs(dot(radialView, radialNormal))
                    / max(radialViewLength * length(radialNormal), .0001f);
                float edgeMask = smoothstep(0.0f, .85f, edgeFacing);
                float axialMask = 1.15f;
                edgeMask = lerp(axialMask, edgeMask, smoothstep(.12f, .55f, radialViewLength));
                outColor.xyz *= lerp(1.0f, edgeMask, _LayerEdgeSoftness);

                Fragment_Stage_Output stage_output;
                stage_output.outColor = outColor;
                return stage_output;
            }

            ENDHLSL
        }
    }
    CustomEditor "ASEMaterialInspector"
    FallBack Off
}