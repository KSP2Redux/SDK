// ============================================================================
// Octahedron_Impostor.shader
//
// Far LOD for terrain scatter: draws a baked octahedral impostor, a camera
// facing quad that picks and blends the three nearest of _Frames x _Frames
// baked views. Vegetation Studio draws it as an item's BillboardCustomPrefab
// through an indirect instanced draw.
//
// Redux's own replica of the stock KSP2 Amplify octahedron impostor, which
// ships only in the stock body bundles and so cannot be bound reliably. The
// property names and texture layout match stock, and the math lives in
// Octahedron_Impostor.cginc.
// ============================================================================
Shader "Redux/Environment/Impostor/Octahedron_Impostor"
{
    Properties
    {
        [NoScaleOffset] _Albedo ("Albedo & Alpha", 2D) = "white" {}
        [NoScaleOffset] _Normals ("Normals & Depth", 2D) = "white" {}
        [NoScaleOffset] _Specular ("Specular & Smoothness", 2D) = "black" {}
        [NoScaleOffset] _Emission ("Emission & Occlusion", 2D) = "black" {}
        [HideInInspector] _Frames ("Frames", Float) = 16
        [HideInInspector] _ImpostorSize ("Impostor Size", Float) = 1
        [HideInInspector] _Offset ("Offset", Vector) = (0, 0, 0, 0)
        [HideInInspector] _DepthSize ("Depth Size", Float) = 1
        _TextureBias ("Texture Bias", Float) = -1
        _Parallax ("Parallax", Range(-1, 1)) = 1
        _ClipMask ("Clip", Range(0, 1)) = 0.5
        _AI_ShadowBias ("Shadow Bias", Range(0, 2)) = 0.25
        _AI_ShadowView ("Shadow View", Range(0, 1)) = 1
        _Color ("Color Tint", Color) = (1, 1, 1, 1)
        _LODDebugColor ("LOD Debug Color", Color) = (1, 1, 1, 1)
        _Brightness ("Brightness", Range(0, 10)) = 1
        _Contrast ("Contrast", Range(0, 1)) = 0.5
        _Saturation ("Saturation", Range(0, 1)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0
        _SpecularValue ("Specular", Range(0, 1)) = 0
        _SpecularTintColor ("Specular Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "DisableBatching" = "True" }

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodynlightmap nodirlightmap
            #pragma multi_compile _ FOG_LINEAR
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "Octahedron_Impostor.cginc"
            #include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"

            struct v2f
            {
                UNITY_POSITION(pos);
                float4 frame0 : TEXCOORD0;
                float4 frame1 : TEXCOORD1;
                float4 frame2 : TEXCOORD2;
                float2 grid : TEXCOORD3;
                float3 viewPosition : TEXCOORD4;
                float3 worldPosition : TEXCOORD5;
                float3 vertexLight : TEXCOORD6;
                UNITY_LIGHTING_COORDS(7, 8)
                UNITY_FOG_COORDS(9)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct FragmentOutput
            {
                half4 color : SV_Target;
                float depth : SV_Depth;
            };

            v2f vert(ImpostorAppData v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                ImpostorBillboard billboard = ImpostorBuildBillboard(v.texcoord.xy);
                v.vertex = float4(billboard.objectPosition, 1.0);
                float3 worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;

                o.pos = UnityWorldToClipPos(worldPosition);
                o.frame0 = billboard.frame0;
                o.frame1 = billboard.frame1;
                o.frame2 = billboard.frame2;
                o.grid = billboard.grid;
                o.viewPosition = UnityWorldToViewPos(worldPosition);
                o.worldPosition = worldPosition;

                #ifdef VERTEXLIGHT_ON
                float3 worldFacing = normalize(mul((float3x3)unity_ObjectToWorld, billboard.objectViewDirection));
                o.vertexLight = Shade4PointLights(
                    unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
                    unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb, unity_LightColor[3].rgb,
                    unity_4LightAtten0, worldPosition, worldFacing);
                #endif

                UNITY_TRANSFER_LIGHTING(o, v.texcoord1.xy);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            FragmentOutput frag(v2f i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                ImpostorApplyLodFade(i.pos.xy);
                ImpostorSurface surface = ImpostorSampleSurface(i.frame0, i.frame1, i.frame2, i.grid);

                SurfaceOutputStandardSpecular o;
                UNITY_INITIALIZE_OUTPUT(SurfaceOutputStandardSpecular, o);
                o.Albedo = surface.albedo;
                o.Specular = surface.specular;
                o.Smoothness = surface.smoothness;
                o.Occlusion = surface.occlusion;
                o.Emission = surface.emission;
                o.Normal = surface.worldNormal;
                o.Alpha = 1.0;

                float3 worldViewDirection = normalize(UnityWorldSpaceViewDir(i.worldPosition));
                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPosition);

                UnityGI gi;
                UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
                gi.light.color = _LightColor0.rgb;
                gi.light.dir = _WorldSpaceLightPos0.xyz;

                UnityGIInput giInput;
                UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
                giInput.light = gi.light;
                giInput.worldPos = i.worldPosition;
                giInput.worldViewDir = worldViewDirection;
                giInput.atten = attenuation;
                giInput.lightmapUV = 0.0;
                giInput.ambient = i.vertexLight;
                giInput.probeHDR[0] = unity_SpecCube0_HDR;
                giInput.probeHDR[1] = unity_SpecCube1_HDR;
                #if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
                giInput.boxMin[0] = unity_SpecCube0_BoxMin;
                #endif
                #ifdef UNITY_SPECCUBE_BOX_PROJECTION
                giInput.boxMax[0] = unity_SpecCube0_BoxMax;
                giInput.probePosition[0] = unity_SpecCube0_ProbePosition;
                giInput.boxMax[1] = unity_SpecCube1_BoxMax;
                giInput.boxMin[1] = unity_SpecCube1_BoxMin;
                giInput.probePosition[1] = unity_SpecCube1_ProbePosition;
                #endif
                LightingStandardSpecular_GI(o, giInput, gi);

                half4 color = LightingStandardSpecular(o, worldViewDirection, gi);
                color.rgb += o.Emission;
                UNITY_APPLY_FOG(i.fogCoord, color);
                UNITY_OPAQUE_ALPHA(color.a);

                FragmentOutput output;
                output.color = color;
                output.depth = ImpostorDeviceDepth(i.viewPosition, ImpostorDepthOffset(surface.depth));
                return output;
            }
            ENDCG
        }

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One
            ZWrite Off

            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile _ FOG_LINEAR
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "Octahedron_Impostor.cginc"
            #include "UnityPBSLighting.cginc"
            #include "AutoLight.cginc"

            struct v2f
            {
                UNITY_POSITION(pos);
                float4 frame0 : TEXCOORD0;
                float4 frame1 : TEXCOORD1;
                float4 frame2 : TEXCOORD2;
                float2 grid : TEXCOORD3;
                float3 viewPosition : TEXCOORD4;
                float3 worldPosition : TEXCOORD5;
                UNITY_LIGHTING_COORDS(6, 7)
                UNITY_FOG_COORDS(8)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct FragmentOutput
            {
                half4 color : SV_Target;
                float depth : SV_Depth;
            };

            v2f vert(ImpostorAppData v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                ImpostorBillboard billboard = ImpostorBuildBillboard(v.texcoord.xy);
                v.vertex = float4(billboard.objectPosition, 1.0);
                float3 worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;

                o.pos = UnityWorldToClipPos(worldPosition);
                o.frame0 = billboard.frame0;
                o.frame1 = billboard.frame1;
                o.frame2 = billboard.frame2;
                o.grid = billboard.grid;
                o.viewPosition = UnityWorldToViewPos(worldPosition);
                o.worldPosition = worldPosition;

                UNITY_TRANSFER_LIGHTING(o, v.texcoord1.xy);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            FragmentOutput frag(v2f i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                ImpostorApplyLodFade(i.pos.xy);
                ImpostorSurface surface = ImpostorSampleSurface(i.frame0, i.frame1, i.frame2, i.grid);

                SurfaceOutputStandardSpecular o;
                UNITY_INITIALIZE_OUTPUT(SurfaceOutputStandardSpecular, o);
                o.Albedo = surface.albedo;
                o.Specular = surface.specular;
                o.Smoothness = surface.smoothness;
                o.Occlusion = surface.occlusion;
                o.Normal = surface.worldNormal;
                o.Alpha = 1.0;

                float3 worldViewDirection = normalize(UnityWorldSpaceViewDir(i.worldPosition));
                UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPosition);

                UnityGI gi;
                UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
                gi.light.color = _LightColor0.rgb * attenuation;
                gi.light.dir = normalize(UnityWorldSpaceLightDir(i.worldPosition));

                half4 color = LightingStandardSpecular(o, worldViewDirection, gi);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, color, half4(0, 0, 0, 0));
                UNITY_OPAQUE_ALPHA(color.a);

                FragmentOutput output;
                output.color = color;
                output.depth = ImpostorDeviceDepth(i.viewPosition, ImpostorDepthOffset(surface.depth));
                return output;
            }
            ENDCG
        }

        Pass
        {
            Name "DEFERRED"
            Tags { "LightMode" = "Deferred" }

            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma exclude_renderers nomrt
            #pragma multi_compile_prepassfinal nolightmap nodynlightmap nodirlightmap
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "Octahedron_Impostor.cginc"
            #include "UnityPBSLighting.cginc"

            struct v2f
            {
                UNITY_POSITION(pos);
                float4 frame0 : TEXCOORD0;
                float4 frame1 : TEXCOORD1;
                float4 frame2 : TEXCOORD2;
                float2 grid : TEXCOORD3;
                float3 viewPosition : TEXCOORD4;
                float3 worldPosition : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(ImpostorAppData v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                ImpostorBillboard billboard = ImpostorBuildBillboard(v.texcoord.xy);
                float3 worldPosition = mul(unity_ObjectToWorld, float4(billboard.objectPosition, 1.0)).xyz;

                o.pos = UnityWorldToClipPos(worldPosition);
                o.frame0 = billboard.frame0;
                o.frame1 = billboard.frame1;
                o.frame2 = billboard.frame2;
                o.grid = billboard.grid;
                o.viewPosition = UnityWorldToViewPos(worldPosition);
                o.worldPosition = worldPosition;
                return o;
            }

            void frag(
                v2f i,
                out half4 outGBuffer0 : SV_Target0,
                out half4 outGBuffer1 : SV_Target1,
                out half4 outGBuffer2 : SV_Target2,
                out half4 outEmission : SV_Target3,
                out float outDepth : SV_Depth)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                ImpostorApplyLodFade(i.pos.xy);
                ImpostorSurface surface = ImpostorSampleSurface(i.frame0, i.frame1, i.frame2, i.grid);

                SurfaceOutputStandardSpecular o;
                UNITY_INITIALIZE_OUTPUT(SurfaceOutputStandardSpecular, o);
                o.Albedo = surface.albedo;
                o.Specular = surface.specular;
                o.Smoothness = surface.smoothness;
                o.Occlusion = surface.occlusion;
                o.Emission = surface.emission;
                o.Normal = surface.worldNormal;
                o.Alpha = 1.0;

                float3 worldViewDirection = normalize(UnityWorldSpaceViewDir(i.worldPosition));

                // Deferred lights are applied later, so only the ambient term is gathered here.
                UnityGI gi;
                UNITY_INITIALIZE_OUTPUT(UnityGI, gi);
                gi.light.color = 0.0;
                gi.light.dir = half3(0.0, 1.0, 0.0);

                UnityGIInput giInput;
                UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
                giInput.light = gi.light;
                giInput.worldPos = i.worldPosition;
                giInput.worldViewDir = worldViewDirection;
                giInput.atten = 1.0;
                giInput.lightmapUV = 0.0;
                giInput.ambient = 0.0;
                giInput.probeHDR[0] = unity_SpecCube0_HDR;
                giInput.probeHDR[1] = unity_SpecCube1_HDR;
                #if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
                giInput.boxMin[0] = unity_SpecCube0_BoxMin;
                #endif
                #ifdef UNITY_SPECCUBE_BOX_PROJECTION
                giInput.boxMax[0] = unity_SpecCube0_BoxMax;
                giInput.probePosition[0] = unity_SpecCube0_ProbePosition;
                giInput.boxMax[1] = unity_SpecCube1_BoxMax;
                giInput.boxMin[1] = unity_SpecCube1_BoxMin;
                giInput.probePosition[1] = unity_SpecCube1_ProbePosition;
                #endif
                LightingStandardSpecular_GI(o, giInput, gi);

                outEmission = LightingStandardSpecular_Deferred(o, worldViewDirection, gi, outGBuffer0, outGBuffer1, outGBuffer2);
                #ifndef UNITY_HDR_ON
                outEmission.rgb = exp2(-outEmission.rgb);
                #endif
                outDepth = ImpostorDeviceDepth(i.viewPosition, ImpostorDepthOffset(surface.depth));
            }
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "Octahedron_Impostor.cginc"

            #if defined(SHADOWS_CUBE) && !defined(SHADOWS_CUBE_IN_DEPTH_TEX)
            #define IMPOSTOR_SHADOW_DISTANCE 1
            #endif

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 frame0 : TEXCOORD0;
                float4 frame1 : TEXCOORD1;
                float4 frame2 : TEXCOORD2;
                float2 grid : TEXCOORD3;
                float3 viewPosition : TEXCOORD4;
                float3 lightVector : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(ImpostorAppData v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                // The shadow camera is the light, so the quad faces the light.
                ImpostorBillboard billboard = ImpostorBuildBillboard(v.texcoord.xy);
                float4 objectPosition = float4(billboard.objectPosition, 1.0);
                float3 worldPosition = mul(unity_ObjectToWorld, objectPosition).xyz;

                #ifdef IMPOSTOR_SHADOW_DISTANCE
                o.pos = UnityObjectToClipPos(objectPosition);
                o.lightVector = worldPosition - _LightPositionRange.xyz;
                #else
                o.pos = UnityApplyLinearShadowBias(UnityClipSpaceShadowCasterPos(objectPosition, billboard.objectViewDirection));
                #endif

                o.frame0 = billboard.frame0;
                o.frame1 = billboard.frame1;
                o.frame2 = billboard.frame2;
                o.grid = billboard.grid;
                o.viewPosition = UnityWorldToViewPos(worldPosition);
                return o;
            }

            #ifdef IMPOSTOR_SHADOW_DISTANCE
            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                ImpostorApplyLodFade(i.pos.xy);
                ImpostorClipAlpha(ImpostorResolveUV(i.frame0, i.frame1, i.frame2, i.grid));
                return UnityEncodeCubeShadowDepth((length(i.lightVector) + unity_LightShadowBias.x) * _LightPositionRange.w);
            }
            #else
            float4 frag(v2f i, out float outDepth : SV_Depth) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                ImpostorApplyLodFade(i.pos.xy);
                ImpostorFragmentUV uv = ImpostorResolveUV(i.frame0, i.frame1, i.frame2, i.grid);
                ImpostorClipAlpha(uv);

                // A directional light, the one with a normal bias, scales the baked depth by
                // Shadow View and pulls the caster back by Shadow Bias.
                float depthOffset = ImpostorDepthOffset(uv.depth);
                if (unity_LightShadowBias.z != 0.0)
                    depthOffset = depthOffset * _AI_ShadowView - _AI_ShadowBias;

                float4 clipPosition = mul(UNITY_MATRIX_P, float4(i.viewPosition.xy, i.viewPosition.z + depthOffset, 1.0));
                clipPosition = UnityApplyLinearShadowBias(clipPosition);
                outDepth = clipPosition.z / (clipPosition.w + 1e-5);
                return 0.0;
            }
            #endif
            ENDCG
        }
    }

    FallBack Off
}
