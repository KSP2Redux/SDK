// Shader: Redux/VFX/Exhaust
// Merged from decompiled SPIRV-Cross output; variables renamed and cleaned.
//
// Pass 1 (FORWARDBASE): full forward-lit exhaust with displacement, bend,
//   Fresnel, color gradient, traces, and optional per-vertex lighting / SH / fog.
// Pass 2 (FORWARDADD): additive lighting stub — always outputs (0,0,0,1).

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
		Tags { "IsEmissive" = "true" "QUEUE" = "Transparent+0" "RenderType" = "Transparent" }

		// ── Pass 1: FORWARDBASE ────────────────────────────────────────────────
		// Full forward-lit pass: displacement + bend vertex animation, Fresnel,
		// three-zone color gradient, traces overlay, optional per-vertex lighting,
		// spherical harmonics, and linear fog.
		Pass
		{
			Name "FORWARD"
			Tags { "IsEmissive" = "true" "LIGHTMODE" = "FORWARDBASE" "QUEUE" = "Transparent+0" "RenderType" = "Transparent" }
			Blend SrcAlpha One, SrcAlpha One
			ZWrite Off
			Cull Off
			GpuProgramID 61899

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.0
			#pragma shader_feature DIRECTIONAL
			#pragma multi_compile _ FOG_LINEAR
			#pragma multi_compile _ LIGHTPROBE_SH
			#pragma multi_compile _ VERTEXLIGHT_ON

			// ── Uniforms: shared (vertex + fragment) ──────────────────────────
			float _ScrollSpeedX;
			float _ScrollSpeedY;
			float _TextureScaleX;
			float _TextureScaleY;
			float4 _Time;
			float4x4 unity_ObjectToWorld;
			Texture2D<float4> _DistortionTexture;
			SamplerState sampler_DistortionTexture;

			// ── Uniforms: vertex stage ─────────────────────────────────────────
			float _VertexDispContrast;
			float _VertexDispScale;
			float _VertexDispPosOffset;
			float _VertexDispFalloffGradient;
			float _BendCenterOffset;
			float _BendRotationOffset;
			float3 _AccelerationDir;
			float _AccelerationScaleFactor;
			float _BendCenterOffsetMultipler;
			float4 _texcoord_ST;
			#ifdef LIGHTPROBE_SH
			float4 unity_SHAr;
			float4 unity_SHAg;
			float4 unity_SHAb;
			float4 unity_SHBr;
			float4 unity_SHBg;
			float4 unity_SHBb;
			float4 unity_SHC;
			#endif
			#ifdef VERTEXLIGHT_ON
			float4 unity_4LightPosX0;
			float4 unity_4LightPosY0;
			float4 unity_4LightPosZ0;
			float4 unity_4LightAtten0;
			float4 unity_LightColor[8];
			#endif
			float4x4 unity_WorldToObject;
			float4x4 unity_MatrixVP;

			// ── Uniforms: fragment stage ───────────────────────────────────────
			float _ColorTintMiddlePos;
			float4 _ColorTintMiddle;
			float4 _ColorTintStart;
			float _ColorTintOffset;
			float _ColorTintFalloff;
			float _TextureOffsetX;
			float _TextureOffsetY;
			float _NoiseAmount;
			float4 _ColorTintEnd;
			float _ColorTintEndOffset;
			float _ColorTintEndGradient;
			float _ColorTintBoost;
			float _TracesCount;
			float _TracesThickness;
			float _TracesLength;
			float _TracesTopPosOffset;
			float _TracesTopFalloffGradient;
			float _TracesAmount;
			float _TracesStrength;
			float _TopGradientPosOffset;
			float _TopGradientFalloff;
			float _ErosionPosOffset;
			float _ErosionFalloffGradient;
			float _NoiseStrength;
			float _ErosionAmount;
			float _FresnelOuter;
			float _FresnelOuterBeneath;
			float _TdotVScale;
			float _CameraDistanceTopGradient;
			float _CameraDistanceFadeLength;
			float _CameraDistanceFalloff;
			float _FresnelOuterErosionOffset;
			float _FresnelOuterErosionFalloff;
			float _FresnelOuterErosionAmount;
			float _FresnelInner;
			float _FresnelInnerBeneath;
			float _Alpha;
			float3 _WorldSpaceCameraPos;
			#ifdef FOG_LINEAR
			float4 _ProjectionParams;
			float4 unity_FogColor;
			float4 unity_FogParams;
			#endif
			Texture2D<float4> _TracesTexture;
			SamplerState sampler_TracesTexture;

			// ── Vertex stage statics ───────────────────────────────────────────
			static float4 gl_Position;
			static float4 in_pos;
			static float4 in_tangent;
			static float3 in_normal;
			static float4 in_texcoord;
			static float4 in_texcoord1;
			static float4 in_texcoord2;
			static float4 in_texcoord3;
			static float4 in_color;
			static float2 out_uv;
			static float3 out_worldNormal;
			static float3 out_worldPos;
			static float3 out_lighting;
			static float4 out_extra;
			#ifdef FOG_LINEAR
			static float out_fogZ;
			#endif

			// ── Fragment stage statics ─────────────────────────────────────────
			static bool gl_FrontFacing;
			static float2 texcoord;
			static float3 worldNormal;
			static float3 worldPos;
			static float3 vertexLight;
			static float4 extraData;
			static float4 outColor;
			#ifdef FOG_LINEAR
			static float fogCoord;
			#endif

			// ── Vertex I/O structs ─────────────────────────────────────────────
			struct Vertex_Stage_Input
			{
				float4 in_pos : POSITION;
				float4 in_tangent : TANGENT;
				float3 in_normal : NORMAL;
				float4 in_texcoord : TEXCOORD;
				float4 in_texcoord1 : TEXCOORD1;
				float4 in_texcoord2 : TEXCOORD2;
				float4 in_texcoord3 : TEXCOORD3;
				float4 in_color : COLOR;
			};

			struct Vertex_Stage_Output
			{
				float2 out_uv : TEXCOORD;
				float3 out_worldNormal : TEXCOORD1;
				float3 out_worldPos : TEXCOORD2;
				float3 out_lighting : TEXCOORD3;
				float4 out_extra : TEXCOORD6;
			#ifdef FOG_LINEAR
				float out_fogZ : TEXCOORD4;
			#endif
				float4 gl_Position : SV_Position;
			};

			// ── Fragment I/O structs ───────────────────────────────────────────
			struct Fragment_Stage_Input
			{
				float2 texcoord : TEXCOORD;
				float3 worldNormal : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
				float3 vertexLight : TEXCOORD3;
				float4 extraData : TEXCOORD6;
			#ifdef FOG_LINEAR
				float fogCoord : TEXCOORD4;
			#endif
				bool gl_FrontFacing : SV_IsFrontFace;
			};

			struct Fragment_Stage_Output
			{
				float4 outColor : SV_Target0;
			};

			// ── Helper ─────────────────────────────────────────────────────────
			// Evaluates the smoothstep curve for a value already clamped to [0,1].
			// Returns t²·(3 - 2t), identical to smoothstep(0,1,t) without the inner clamp.
			precise float smoothstepT(precise float t)
			{
				precise float tSq = t * t;
				return tSq * mad(t, -2.0f, 3.0f);
			}

			// ── Vertex shader ──────────────────────────────────────────────────
			// Output: displaced world-space vertex position, world normal, UV, and per-vertex
			// lighting / SH contribution.  Displacement and bend are applied before the
			// ObjectToWorld transform so they operate in local particle space.
			Vertex_Stage_Output vert(Vertex_Stage_Input stage_input)
			{
				in_pos = stage_input.in_pos;
				in_tangent = stage_input.in_tangent;
				in_normal = stage_input.in_normal;
				in_texcoord = stage_input.in_texcoord;
				in_texcoord1 = stage_input.in_texcoord1;
				in_texcoord2 = stage_input.in_texcoord2;
				in_texcoord3 = stage_input.in_texcoord3;
				in_color = stage_input.in_color;
				// ── Displacement sample ────────────────────────────────────────
				// Sample _DistortionTexture scrolling in UV at _ScrollSpeedX/Y and apply a
				// contrast remap to get a displacement magnitude in [0,1].
				precise float dispU = in_texcoord.x * _TextureScaleX;
				precise float dispV = in_texcoord.y * _TextureScaleY;
				precise float negContrast    = -_VertexDispContrast;
				precise float contrastInv    = negContrast + 1.0f;      // 1 - contrast
				precise float negContrastInv = -contrastInv;
				precise float contrastRange  = negContrastInv + _VertexDispContrast;  // 2·contrast - 1
				float dispSample = mad(_DistortionTexture.SampleLevel(sampler_DistortionTexture,
				    float2(mad(_Time.y, _ScrollSpeedX, dispU), mad(_Time.y, _ScrollSpeedY, dispV)),
				    0.0f).x, contrastRange, contrastInv);

				// ── Displacement → local offset ────────────────────────────────
				// Project the remapped sample along the vertex normal and scale.
				precise float dispNormalX = dispSample * in_normal.x;
				precise float dispNormalY = dispSample * in_normal.y;
				precise float dispNormalZ = dispSample * in_normal.z;
				precise float dispX = dispNormalX * _VertexDispScale;
				precise float dispY = dispNormalY * _VertexDispScale;
				precise float dispZ = dispNormalZ * _VertexDispScale;

				// ── Acceleration / bend force ──────────────────────────────────
				// Convert _AccelerationDir to a signed bend force via 1 - (|a|+1)^-2.5,
				// then scale to [-4, 4] per axis.  Only X and Z feed into the rotation step.
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

				// ── Bend rotation ──────────────────────────────────────────────
				// Rotate the XZ bend force by _BendRotationOffset·π and scale by
				// _AccelerationScaleFactor to produce bend direction vectors (U, V).
				precise float bendScaledX = bendForceX * _AccelerationScaleFactor;
				precise float bendScaledZ = bendForceZ * _AccelerationScaleFactor;
				precise float bendRotRad = _BendRotationOffset * 3.1415927f;
				float bendSin = sin(bendRotRad);
				float bendCos = cos(bendRotRad);
				precise float negBendSin = -bendSin;
				float bendDirU = dot(float2(negBendSin, bendCos), float2(bendScaledX, bendScaledZ));
				float bendDirV = dot(float2(bendCos,    bendSin), float2(bendScaledX, bendScaledZ));

				// ── Bend center offset ─────────────────────────────────────────
				// Quadratic curvature of the bend direction, scaled by _BendCenterOffset, gives
				// the lateral offset applied to the particle center point.
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

				// ── Displacement falloff ───────────────────────────────────────
				// dispFalloffW: smoothstep ramp along the particle length (vFlipped axis)
				// that controls how much displacement and bend are applied at each vertex.
				// The bend curve shape adds an additional self-bending quadratic term.
				precise float falloffInvGradient = 1.0f / _VertexDispFalloffGradient;
				precise float negV       = -in_texcoord.y;
				precise float vFlipped   = negV + 1.0f;                   // 0 at top, 1 at base
				precise float negPosOffset = -_VertexDispPosOffset;
				precise float falloffV   = vFlipped + negPosOffset;
				precise float bendLenWeighted = length(float3(bendForceX, bendForceY, bendForceZ)) * vFlipped;
				float bendLenFactor  = mad(bendLenWeighted, 0.01f, 1.0f);
				precise float bendLenSq    = bendLenFactor * bendLenFactor;
				precise float negBendLenSq = -bendLenSq;
				float bendCurveShape = mad(bendLenSq, bendLenSq, negBendLenSq); // lenSq² - lenSq
				precise float bendCurveU = bendDirU * bendCurveShape;
				precise float bendCurveV = bendDirV * bendCurveShape;
				float dispFalloffT  = clamp(falloffInvGradient * falloffV, 0.0f, 1.0f);
				precise float dispFalloffW = smoothstepT(dispFalloffT);

				// ── Displaced local position ───────────────────────────────────
				// Combine displacement, bend offset, and bend curve into a local-space offset,
				// then add to the original vertex position before world-space transform.
				precise float bendCurveSqU    = bendCurveU * bendCurveU;
				precise float bendCurveSqV    = bendCurveV * bendCurveV;
				precise float bendCurveShapeU = bendCurveSqU * sign(bendCurveU);
				precise float bendCurveShapeV = bendCurveSqV * sign(bendCurveV);
				precise float localX = mad(dispX, dispFalloffW, bendOffsetU) + bendCurveShapeU;
				precise float localY = mad(dispY, dispFalloffW, bendOffsetV) + bendCurveShapeV;
				precise float localZ = dispZ * dispFalloffW;

				// ── World / clip transform ─────────────────────────────────────
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

				// ── UV, world position & normal outputs ────────────────────────
				out_worldPos = worldPos.xyz;
				out_uv.x = mad(in_texcoord.x, _texcoord_ST.x, _texcoord_ST.z);
				out_uv.y = mad(in_texcoord.y, _texcoord_ST.y, _texcoord_ST.w);
				// Normal: transpose(WorldToObj) × in_normal  (same as mul(in_normal, WorldToObj))
				float3 worldNormal = normalize(mul(in_normal, (float3x3)unity_WorldToObject));
				out_worldNormal = worldNormal;

				// ── Per-vertex lighting ────────────────────────────────────────
				// Accumulates diffuse contributions from up to 4 point lights into out_lighting.
				// Light positions in unity_4LightPosX0/Y0/Z0, attenuations in unity_4LightAtten0.
			#ifdef VERTEXLIGHT_ON
				{
					precise float negWorldPosX = -worldPos.x;
					precise float lightDeltaX0 = negWorldPosX + unity_4LightPosX0.x;
					precise float lightDeltaX1 = negWorldPosX + unity_4LightPosX0.y;
					precise float lightDeltaX2 = negWorldPosX + unity_4LightPosX0.z;
					precise float lightDeltaX3 = negWorldPosX + unity_4LightPosX0.w;
					precise float negWorldPosY = -worldPos.y;
					precise float lightDeltaY0 = negWorldPosY + unity_4LightPosY0.x;
					precise float lightDeltaY1 = negWorldPosY + unity_4LightPosY0.y;
					precise float lightDeltaY2 = negWorldPosY + unity_4LightPosY0.z;
					precise float lightDeltaY3 = negWorldPosY + unity_4LightPosY0.w;
					precise float negWorldPosZ = -worldPos.z;
					precise float lightDeltaZ0 = negWorldPosZ + unity_4LightPosZ0.x;
					precise float lightDeltaZ1 = negWorldPosZ + unity_4LightPosZ0.y;
					precise float lightDeltaZ2 = negWorldPosZ + unity_4LightPosZ0.z;
					precise float lightDeltaZ3 = negWorldPosZ + unity_4LightPosZ0.w;
					precise float nDotY0 = worldNormal.y * lightDeltaY0;
					precise float nDotY1 = worldNormal.y * lightDeltaY1;
					precise float nDotY2 = worldNormal.y * lightDeltaY2;
					precise float nDotY3 = worldNormal.y * lightDeltaY3;
					precise float deltaYSq0 = lightDeltaY0 * lightDeltaY0;
					precise float deltaYSq1 = lightDeltaY1 * lightDeltaY1;
					precise float deltaYSq2 = lightDeltaY2 * lightDeltaY2;
					precise float deltaYSq3 = lightDeltaY3 * lightDeltaY3;
					float lightDistSq0 = max(mad(lightDeltaZ0, lightDeltaZ0, mad(lightDeltaX0, lightDeltaX0, deltaYSq0)), 9.9999999747524270787835121154785e-07f);
					float lightDistSq1 = max(mad(lightDeltaZ1, lightDeltaZ1, mad(lightDeltaX1, lightDeltaX1, deltaYSq1)), 9.9999999747524270787835121154785e-07f);
					float lightDistSq2 = max(mad(lightDeltaZ2, lightDeltaZ2, mad(lightDeltaX2, lightDeltaX2, deltaYSq2)), 9.9999999747524270787835121154785e-07f);
					float lightDistSq3 = max(mad(lightDeltaZ3, lightDeltaZ3, mad(lightDeltaX3, lightDeltaX3, deltaYSq3)), 9.9999999747524270787835121154785e-07f);
					precise float lightAtten0 = 1.0f / mad(lightDistSq0, unity_4LightAtten0.x, 1.0f);
					precise float lightAtten1 = 1.0f / mad(lightDistSq1, unity_4LightAtten0.y, 1.0f);
					precise float lightAtten2 = 1.0f / mad(lightDistSq2, unity_4LightAtten0.z, 1.0f);
					precise float lightAtten3 = 1.0f / mad(lightDistSq3, unity_4LightAtten0.w, 1.0f);
					precise float NdotL0 = mad(lightDeltaZ0, worldNormal.z, mad(lightDeltaX0, worldNormal.x, nDotY0)) * rsqrt(lightDistSq0);
					precise float NdotL1 = mad(lightDeltaZ1, worldNormal.z, mad(lightDeltaX1, worldNormal.x, nDotY1)) * rsqrt(lightDistSq1);
					precise float NdotL2 = mad(lightDeltaZ2, worldNormal.z, mad(lightDeltaX2, worldNormal.x, nDotY2)) * rsqrt(lightDistSq2);
					precise float NdotL3 = mad(lightDeltaZ3, worldNormal.z, mad(lightDeltaX3, worldNormal.x, nDotY3)) * rsqrt(lightDistSq3);
					precise float lightContrib0 = lightAtten0 * max(NdotL0, 0.0f);
					precise float lightContrib1 = lightAtten1 * max(NdotL1, 0.0f);
					precise float lightContrib2 = lightAtten2 * max(NdotL2, 0.0f);
					precise float lightContrib3 = lightAtten3 * max(NdotL3, 0.0f);
					precise float light1R = lightContrib1 * unity_LightColor[1].x;
					precise float light1G = lightContrib1 * unity_LightColor[1].y;
					precise float light1B = lightContrib1 * unity_LightColor[1].z;
					out_lighting.x = mad(unity_LightColor[3].x, lightContrib3, mad(unity_LightColor[2].x, lightContrib2, mad(unity_LightColor[0].x, lightContrib0, light1R)));
					out_lighting.y = mad(unity_LightColor[3].y, lightContrib3, mad(unity_LightColor[2].y, lightContrib2, mad(unity_LightColor[0].y, lightContrib0, light1G)));
					out_lighting.z = mad(unity_LightColor[3].z, lightContrib3, mad(unity_LightColor[2].z, lightContrib2, mad(unity_LightColor[0].z, lightContrib0, light1B)));
				}
			#else
				out_lighting.x = 0.0f;
				out_lighting.y = 0.0f;
				out_lighting.z = 0.0f;
			#endif

				// ── Spherical harmonics ────────────────────────────────────────
				// Evaluates the L1+L2 SH irradiance for worldNormal using Unity's SH uniforms
				// and adds the result to out_lighting.
			#ifdef LIGHTPROBE_SH
				{
					// Basis functions: L1 = (ny·nx, nz·ny, nz², nx·nz), L2_diag = nx²-ny²
					precise float shNyNy = worldNormal.y * worldNormal.y;
					precise float negShNyNy = -shNyNy;
					float shBasisA = mad(worldNormal.x, worldNormal.x, negShNyNy); // nx*nx - ny*ny
					precise float shBasisB = worldNormal.y * worldNormal.x;
					precise float shBasisC = worldNormal.z * worldNormal.y;
					precise float shNzNz = worldNormal.z * worldNormal.z;
					precise float shBasisD = worldNormal.x * worldNormal.z;
					precise float shR = mad(unity_SHC.x, shBasisA, dot(float4(unity_SHBr), float4(shBasisB, shBasisC, shNzNz, shBasisD))) + dot(float4(unity_SHAr), float4(worldNormal.x, worldNormal.y, worldNormal.z, 1.0f));
					precise float shG = mad(unity_SHC.y, shBasisA, dot(float4(unity_SHBg), float4(shBasisB, shBasisC, shNzNz, shBasisD))) + dot(float4(unity_SHAg), float4(worldNormal.x, worldNormal.y, worldNormal.z, 1.0f));
					precise float shB = mad(unity_SHC.z, shBasisA, dot(float4(unity_SHBb), float4(shBasisB, shBasisC, shNzNz, shBasisD))) + dot(float4(unity_SHAb), float4(worldNormal.x, worldNormal.y, worldNormal.z, 1.0f));
					out_lighting.x += shR;
					out_lighting.y += shG;
					out_lighting.z += shB;
				}
			#endif

				out_extra = float4(0.0f, 0.0f, 0.0f, 0.0f);
				Vertex_Stage_Output stage_output;
				stage_output.gl_Position = gl_Position;
				stage_output.out_uv = out_uv;
				stage_output.out_worldNormal = out_worldNormal;
				stage_output.out_worldPos = out_worldPos;
				stage_output.out_lighting = out_lighting;
				stage_output.out_extra = out_extra;
			#ifdef FOG_LINEAR
				stage_output.out_fogZ = out_fogZ;
			#endif
				return stage_output;
			}

			// ── Fragment shader ────────────────────────────────────────────────
			// Output: rgba colour for the exhaust VFX particle.
			//   rgb = (traces · fresnelOuter + baseColor) · _Alpha,  blended with fog if FOG_LINEAR.
			//   a   = 1.0 (premultiplied alpha pass).
			Fragment_Stage_Output frag(Fragment_Stage_Input stage_input)
			{
				// Column 2 of objectToWorld — reconstructs the object-space long axis in world
				// space for the TdotV (axial dot view) Fresnel blend term.
				float4 objectToWorld_c2 = float4(unity_ObjectToWorld[0][2], unity_ObjectToWorld[1][2], unity_ObjectToWorld[2][2], unity_ObjectToWorld[3][2]);

				gl_FrontFacing = stage_input.gl_FrontFacing;
				texcoord       = stage_input.texcoord;
				worldNormal    = stage_input.worldNormal;
				worldPos       = stage_input.worldPos;
				vertexLight    = stage_input.vertexLight;
				extraData      = stage_input.extraData;
			#ifdef FOG_LINEAR
				fogCoord = stage_input.fogCoord;
			#endif

				// ── Face normal ────────────────────────────────────────────────
				// Flip the interpolated world normal on back-facing fragments so that N·V
				// lighting is consistent on both sides of the (double-sided) particle plane.
				bool isFrontFace = gl_FrontFacing;
				precise float negNormalX = -worldNormal.x;
				precise float negNormalY = -worldNormal.y;
				precise float negNormalZ = -worldNormal.z;
				float faceNormalX = isFrontFace ? worldNormal.x : negNormalX;
				float faceNormalY = isFrontFace ? worldNormal.y : negNormalY;
				float faceNormalZ = isFrontFace ? worldNormal.z : negNormalZ;
				float3 fragNormal = normalize(float3(faceNormalX, faceNormalY, faceNormalZ));

				// ── View direction & N·V ───────────────────────────────────────
				// fresnelSmooth = NdotV²·(3-2·NdotV)  — a smooth Fresnel polynomial in [0,1].
				// safeViewDirNorm guards against near-zero viewDir when computing N·V.
				float3 viewDir = _WorldSpaceCameraPos - worldPos;
				float viewDirLenSq = dot(viewDir, viewDir);
				float3 viewDirNorm    = normalize(viewDir);
				float3 safeViewDirNorm = normalize(viewDir * rsqrt(max(viewDirLenSq, 0.001f)));
				float NdotV = min(abs(dot(fragNormal, safeViewDirNorm)), 1.0f);
				float ndotVPoly = mad(NdotV, -2.0f, 3.0f);          // (3 - 2·NdotV)
				precise float NdotVSq = NdotV * NdotV;
				precise float negNdotVPoly = -ndotVPoly;
				precise float fresnelSmooth = NdotVSq * ndotVPoly;   // NdotV²·(3-2·NdotV)

				// ── Fresnel exponents — TdotV blend ────────────────────────────
				// TdotV blends the Fresnel exponents between a "front-on" and "beneath" value
				// so the effect looks different when viewed end-on vs. from the side.
				float3 objectNormDir = normalize(objectToWorld_c2.xyz);
				float TdotV = pow(abs(dot(objectNormDir, viewDirNorm)), _TdotVScale);
				precise float negFresnelInner = -_FresnelInner;
				precise float fresnelInnerRange = negFresnelInner + _FresnelInnerBeneath;
				float fresnelInnerExp = mad(TdotV, fresnelInnerRange, _FresnelInner);
				precise float negFresnelOuter = -_FresnelOuter;
				precise float fresnelOuterRange = negFresnelOuter + _FresnelOuterBeneath;
				float fresnelOuterExp = mad(TdotV, fresnelOuterRange, _FresnelOuter);

				// ── Camera top gradient & distance fade ────────────────────────
				// cameraFade = distanceFade · (1 - smoothstep(vFlipped / _CameraDistanceTopGradient))
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

				// ── Outer Fresnel / erosion threshold ──────────────────────────
				// fresnelOuterRaw: fresnelSmooth raised to an exponent modulated by cameraFade.
				// erosionThreshold: the noise level below which outer Fresnel starts to erode.
				precise float negFresnelOuterExp = -fresnelOuterExp;
				precise float negErosionAmount   = -_FresnelOuterErosionAmount;
				float fresnelOuterRaw = pow(fresnelSmooth, mad(cameraFade, negFresnelOuterExp, fresnelOuterExp));
				precise float erosionThreshold = (fresnelOuterRaw - _FresnelOuterErosionOffset)
				                               / _FresnelOuterErosionFalloff;

				// ── Distortion texture samples ─────────────────────────────────
				// Two samples at different scroll directions provide noise channels R and G
				// whose product forms a structured, animated noise pattern.
				precise float scrollTimeY   = _ScrollSpeedY * _Time.y;
				precise float negScrollSpeedX = -_ScrollSpeedX;
				float scaledU = mad(texcoord.x, _TextureScaleX, _TextureOffsetX);
				float scaledV = mad(texcoord.y, _TextureScaleY, _TextureOffsetY);
				float4 distortSample0 = _DistortionTexture.Sample(sampler_DistortionTexture,
				    float2(mad(_Time.y, _ScrollSpeedX, scaledU), mad(_Time.y, _ScrollSpeedY, scaledV)));
				float distortR = distortSample0.x;
				float4 distortSample1 = _DistortionTexture.Sample(sampler_DistortionTexture,
				    float2(mad(_Time.y, negScrollSpeedX, scaledU), mad(1.3f, scrollTimeY, scaledV)));
				float distortG = distortSample1.y;

				// ── Noise value ────────────────────────────────────────────────
				// noiseMask:  1 - _NoiseAmount·(1 - R·G),  masks the particle shape by noise.
				// noiseValue: R·G remapped with _NoiseStrength contrast for erosion thresholding.
				precise float noiseProduct = distortG * distortR;
				float noiseMask = clamp(mad(_NoiseAmount, mad(distortR, distortG, -1.0f), 1.0f), 0.0f, 1.0f);
				precise float negNoiseStrength    = -_NoiseStrength;
				precise float noiseStrengthInv    = negNoiseStrength + 1.0f;
				precise float negNoiseStrengthInv = -noiseStrengthInv;
				precise float noiseStrengthRange  = negNoiseStrengthInv + _NoiseStrength;
				float noiseValue = clamp(mad(noiseProduct, noiseStrengthRange, noiseStrengthInv), 0.0f, 1.0f);

				// ── Erosion blend → final outer Fresnel ────────────────────────
				// When noise < erosionThreshold the outer Fresnel is pulled back toward the
				// eroded version, controlled by _FresnelOuterErosionAmount and cameraFade.
				precise float negErosionThreshold   = -erosionThreshold;
				precise float noiseMinusThreshold   = negErosionThreshold + noiseValue;
				precise float negNoiseMinusThreshold = -noiseMinusThreshold;
				precise float fresnelMinusNoise     = negNoiseMinusThreshold + fresnelOuterRaw;
				precise float negFresnelOuterRaw    = -fresnelOuterRaw;
				precise float erosionBlend = negFresnelOuterRaw + clamp(fresnelMinusNoise, 0.0f, 1.0f);
				float fresnelOuter = mad(mad(cameraFade, negErosionAmount, _FresnelOuterErosionAmount),
				                         erosionBlend, fresnelOuterRaw);

				// ── Top gradient mask ──────────────────────────────────────────
				// Smoothstep fade that suppresses the top of the particle shape.
				// innerFresnel: pow(1 - fresnelSmooth, fresnelInnerExp) · fresnelOuter — adds
				// a bright glowing rim visible at grazing view angles.
				precise float topGradV = vFlipped - _TopGradientPosOffset;
				float topGradT  = clamp(topGradV / _TopGradientFalloff, 0.0f, 1.0f);
				float topGradMask = min(smoothstepT(topGradT), 1.0f);
				precise float innerFresnel = pow(max(mad(negNdotVPoly, NdotVSq, 1.0f), 0.001f),
				                                 fresnelInnerExp) * fresnelOuter;
				precise float fresnelGated = topGradMask * innerFresnel;

				// ── Erosion mask & base alpha ──────────────────────────────────
				// Noise-driven dissolve from the particle base upward: pixels where
				// (noiseValue - erosionPos - 1) < 0 are clipped by _ErosionAmount.
				// baseAlpha = noiseMask · erosionMask · fresnelGated.
				precise float erosionV      = vFlipped - _ErosionPosOffset;
				precise float erosionPosRaw = erosionV / _ErosionFalloffGradient;
				precise float negErosionPosRaw      = -erosionPosRaw;
				precise float noiseMinusErosionPos  = noiseValue + negErosionPosRaw;
				precise float erosionInput          = noiseMinusErosionPos + (-1.0f);
				float erosionMask   = clamp(mad(_ErosionAmount, erosionInput, 1.0f), 0.0f, 1.0f);
				precise float maskedFresnel = erosionMask * fresnelGated;
				precise float baseAlpha     = noiseMask * maskedFresnel;

				// ── Color gradient ─────────────────────────────────────────────
				// Three-zone gradient keyed on vFlipped: start (base) → middle → end (top).
				// Gradient widths are noise-modulated (_ColorTintEndGradient, _ColorTintFalloff).
				//
				// Upper zone (vFlipped >= _ColorTintMiddlePos): lerp Middle → End.
				precise float noisedEndGradient = noiseMask * _ColorTintEndGradient;
				precise float negMiddlePos   = -_ColorTintMiddlePos;
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
				float isUpperZone = (vFlipped >= _ColorTintMiddlePos) ? 1.0f : 0.0f;
				precise float upperColorR = isUpperZone * mad(colorEndSmooth, colorEndDeltaR, _ColorTintMiddle.x);
				precise float upperColorG = isUpperZone * mad(colorEndSmooth, colorEndDeltaG, _ColorTintMiddle.y);
				precise float upperColorB = isUpperZone * mad(colorEndSmooth, colorEndDeltaB, _ColorTintMiddle.z);
				// Lower zone (vFlipped < _ColorTintMiddlePos): lerp Middle → Start.
				precise float noisedFalloff  = noiseMask * _ColorTintFalloff;
				precise float lowerZoneT     = vFlipped / _ColorTintMiddlePos;  // normalised lower-zone pos
				precise float negLowerZoneT  = -lowerZoneT;
				precise float invLowerZoneT  = negLowerZoneT + 1.0f;            // 1 - lowerZoneT
				precise float negColorOffset = -_ColorTintOffset;
				precise float startGradV     = invLowerZoneT + negColorOffset;
				float colorStartT     = clamp((1.0f / noisedFalloff) * startGradV, 0.0f, 1.0f);
				precise float colorStartSmooth = smoothstepT(colorStartT);
				precise float colorStartDeltaR = negMiddleR + _ColorTintStart.x;
				precise float colorStartDeltaG = negMiddleG + _ColorTintStart.y;
				precise float colorStartDeltaB = negMiddleB + _ColorTintStart.z;
				float isLowerZone = (_ColorTintMiddlePos >= vFlipped) ? 1.0f : 0.0f;
				float tintR = mad(isLowerZone, mad(colorStartSmooth, colorStartDeltaR, _ColorTintMiddle.x), upperColorR);
				float tintG = mad(isLowerZone, mad(colorStartSmooth, colorStartDeltaG, _ColorTintMiddle.y), upperColorG);
				float tintB = mad(isLowerZone, mad(colorStartSmooth, colorStartDeltaB, _ColorTintMiddle.z), upperColorB);
				// Boost: tint · (1 + _ColorTintBoost), then scale by baseAlpha.
				float boostedR = mad(tintR, _ColorTintBoost, tintR);
				float boostedG = mad(tintG, _ColorTintBoost, tintG);
				float boostedB = mad(tintB, _ColorTintBoost, tintB);
				precise float colorR = baseAlpha * boostedR;
				precise float colorG = baseAlpha * boostedG;
				precise float colorB = baseAlpha * boostedB;

				// ── Traces ─────────────────────────────────────────────────────
				// Sinusoidal streak overlay sampled from the $Globals trace texture, then
				// masked by top gradient, erosion, and noise before compositing.
				precise float tracesFreq    = _TracesCount * 6.2831855f;   // TracesCount × 2π
				precise float tracesU       = tracesFreq * texcoord.x;
				float tracesMask = pow(max(sin(mad(tracesFreq, uFlipped, 1.5707964f)), 0.0001f), _TracesThickness);
				precise float tracesSinInput      = tracesU * 0.5f;
				precise float tracesLengthV       = vFlipped * _TracesLength;
				precise float tracesTopPos        = vFlipped - _TracesTopPosOffset;
				// Build UV into the $Globals trace atlas from the sin band and length offset.
				precise float tracesSinCentered    = abs(sin(tracesSinInput)) + (-0.5f);
				precise float tracesLengthCentered = tracesLengthV + (-0.5f);
				precise float tracesTexU = dot(float2(tracesSinCentered, tracesLengthCentered),
				                               float2(-1.0f, 1.2246468525851678544463796427522e-16f)) + 0.5f;
				precise float tracesTexV = dot(float2(tracesSinCentered, tracesLengthCentered),
				                               float2(-1.2246468525851678544463796427522e-16f, -1.0f)) + 0.5f;
				float4 tracesTex = _TracesTexture.Sample(sampler_TracesTexture, float2(tracesTexU, tracesTexV));
				// Mask by tracesMask, then apply top falloff smoothstep.
				precise float tracesBlendR = tracesMask * tracesTex.x;
				precise float tracesBlendG = tracesMask * tracesTex.y;
				precise float tracesBlendB = tracesMask * tracesTex.z;
				float tracesTopT    = clamp(tracesTopPos / _TracesTopFalloffGradient, 0.0f, 1.0f);
				float tracesTopMask = min(smoothstepT(tracesTopT), 1.0f);
				precise float tracesFinalR = tracesTopMask * tracesBlendR;
				precise float tracesFinalG = tracesTopMask * tracesBlendG;
				precise float tracesFinalB = tracesTopMask * tracesBlendB;
				// Tint, scale by _TracesAmount · _TracesStrength, then gate by topGrad and erosion masks.
				precise float tracesTintR = tracesFinalR * boostedR;
				precise float tracesTintG = tracesFinalG * boostedG;
				precise float tracesTintB = tracesFinalB * boostedB;
				precise float tracesAmountR = tracesTintR * _TracesAmount;
				precise float tracesAmountG = tracesTintG * _TracesAmount;
				precise float tracesAmountB = tracesTintB * _TracesAmount;
				precise float tracesStrR = tracesAmountR * _TracesStrength;
				precise float tracesStrG = tracesAmountG * _TracesStrength;
				precise float tracesStrB = tracesAmountB * _TracesStrength;
				precise float tracesMaskedR = topGradMask * tracesStrR;
				precise float tracesMaskedG = topGradMask * tracesStrG;
				precise float tracesMaskedB = topGradMask * tracesStrB;
				precise float tracesErodedR = erosionMask * tracesMaskedR;
				precise float tracesErodedG = erosionMask * tracesMaskedG;
				precise float tracesErodedB = erosionMask * tracesMaskedB;
				precise float tracesR = noiseMask * tracesErodedR;
				precise float tracesG = noiseMask * tracesErodedG;
				precise float tracesB = noiseMask * tracesErodedB;

				// ── Final composite ────────────────────────────────────────────
				// outRGB = saturate(traces · fresnelOuter + baseColor) · _Alpha
				// FOG_LINEAR variant blends with unity_FogColor using a linear depth fog factor.
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
				Fragment_Stage_Output stage_output;
				stage_output.outColor = outColor;
				return stage_output;
			}

			ENDHLSL
		}

		// ── Pass 2: FORWARDADD ─────────────────────────────────────────────────
		// Additive lighting stub.  The vertex stage computes world-space position,
		// normal, and light-space position for POINT/SPOT variants; the fragment
		// stage always outputs (0,0,0,1) so Unity's additive lighting pipeline can
		// accumulate per-pixel contributions on top of Pass 1's result.
		Pass
		{
			Name "FORWARD"
			Tags { "IsEmissive" = "true" "LIGHTMODE" = "FORWARDADD" "QUEUE" = "Transparent+0" "RenderType" = "Transparent" }
			Blend One One, One One
			ZWrite Off
			Cull Off
			GpuProgramID 76430

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.0
			#pragma shader_feature DIRECTIONAL
			#pragma multi_compile _ FOG_LINEAR
			#pragma multi_compile _ POINT SPOT

			// ── Uniforms ───────────────────────────────────────────────────────
			#if defined(POINT) || defined(SPOT)
			float4x4 unity_WorldToLight;
			#endif
			float _ScrollSpeedX;
			float _ScrollSpeedY;
			float _TextureScaleX;
			float _TextureScaleY;
			float _VertexDispContrast;
			float _VertexDispScale;
			float _VertexDispPosOffset;
			float _VertexDispFalloffGradient;
			float _BendCenterOffset;
			float _BendRotationOffset;
			float3 _AccelerationDir;
			float _AccelerationScaleFactor;
			float _BendCenterOffsetMultipler;
			float4 _Time;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_WorldToObject;
			float4x4 unity_MatrixVP;

			Texture2D<float4> _DistortionTexture;
			SamplerState sampler_DistortionTexture;

			// ── Vertex stage statics ───────────────────────────────────────────
			static float4 gl_Position;
			static float4 in_pos;
			static float4 in_tangent;
			static float3 in_normal;
			static float4 in_texcoord;
			static float4 in_texcoord1;
			static float4 in_texcoord2;
			static float4 in_texcoord3;
			static float4 in_color;
			static float3 out_worldNormal;
			static float3 out_worldPos;
			#if defined(POINT)
			static float3 out_lightPos;
			#elif defined(SPOT)
			static float4 out_lightPos;
			#endif
			#if defined(FOG_LINEAR)
			static float out_fogZ;
			#endif

			// ── Fragment stage statics ─────────────────────────────────────────
			static float3 worldNormal;
			static float3 worldPos;
			#if defined(POINT)
			static float3 lightPos;
			#elif defined(SPOT)
			static float4 lightPos;
			#endif
			#ifdef FOG_LINEAR
			static float fogCoord;
			#endif
			static float4 outColor;

			// ── Vertex I/O structs ─────────────────────────────────────────────
			struct Vertex_Stage_Input
			{
				float4 in_pos : POSITION;
				float4 in_tangent : TANGENT;
				float3 in_normal : NORMAL;
				float4 in_texcoord : TEXCOORD;
				float4 in_texcoord1 : TEXCOORD1;
				float4 in_texcoord2 : TEXCOORD2;
				float4 in_texcoord3 : TEXCOORD3;
				float4 in_color : COLOR;
			};

			struct Vertex_Stage_Output
			{
				float3 out_worldNormal : TEXCOORD;
				float3 out_worldPos : TEXCOORD1;
			#if defined(POINT)
				float3 out_lightPos : TEXCOORD2;
			#elif defined(SPOT)
				float4 out_lightPos : TEXCOORD2;
			#endif
			#if defined(FOG_LINEAR)
				float out_fogZ : TEXCOORD3;
			#endif
				float4 gl_Position : SV_Position;
			};

			// ── Fragment I/O structs ───────────────────────────────────────────
			struct Fragment_Stage_Input
			{
				float3 worldNormal : TEXCOORD;
				float3 worldPos : TEXCOORD1;
			#if defined(POINT)
				float3 lightPos : TEXCOORD2;
			#elif defined(SPOT)
				float4 lightPos : TEXCOORD2;
			#endif
			#ifdef FOG_LINEAR
				float fogCoord : TEXCOORD3;
			#endif
			};

			struct Fragment_Stage_Output
			{
				float4 outColor : SV_Target0;
			};

			// ── Helper ─────────────────────────────────────────────────────────
			// Evaluates the smoothstep curve for a value already clamped to [0,1].
			// Returns t²·(3 - 2t), identical to smoothstep(0,1,t) without the inner clamp.
			precise float smoothstepT(precise float t)
			{
				precise float tSq = t * t;
				return tSq * mad(t, -2.0f, 3.0f);
			}

			// ── Vertex shader ──────────────────────────────────────────────────
			// Output: displaced world-space vertex, world normal, and light-space position
			// for additive per-pixel lighting.  No UV or per-vertex lighting accumulation.
			Vertex_Stage_Output vert(Vertex_Stage_Input stage_input)
			{
				in_pos = stage_input.in_pos;
				in_tangent = stage_input.in_tangent;
				in_normal = stage_input.in_normal;
				in_texcoord = stage_input.in_texcoord;
				in_texcoord1 = stage_input.in_texcoord1;
				in_texcoord2 = stage_input.in_texcoord2;
				in_texcoord3 = stage_input.in_texcoord3;
				in_color = stage_input.in_color;
				// Pack loose shader properties into vectors matching the original uniform layout.
				float4 scroll = float4(_ScrollSpeedX, _ScrollSpeedY, _TextureScaleX, _TextureScaleY);
				float4 dispc  = float4(_VertexDispContrast, _VertexDispScale, _VertexDispPosOffset, _VertexDispFalloffGradient);
				float4 accel  = float4(_AccelerationDir[0], _AccelerationDir[1], _AccelerationDir[2], _AccelerationScaleFactor);

				// ── Displacement sample ────────────────────────────────────────
				// Sample _DistortionTexture scrolling in UV and apply contrast remap.
				precise float dispU = in_texcoord.x * scroll.z;
				precise float dispV = in_texcoord.y * scroll.w;
				precise float negContrast    = -dispc.x;
				precise float contrastInv    = negContrast + 1.0f;       // 1 - contrast
				precise float negContrastInv = -contrastInv;
				precise float contrastRange  = negContrastInv + dispc.x; // 2·contrast - 1
				float dispSample = mad(_DistortionTexture.SampleLevel(sampler_DistortionTexture,
				    float2(mad(_Time.y, scroll.x, dispU), mad(_Time.y, scroll.y, dispV)),
				    0.0f).x, contrastRange, contrastInv);

				// ── Displacement → local offset ────────────────────────────────
				precise float dispNormalX = dispSample * in_normal.x;
				precise float dispNormalY = dispSample * in_normal.y;
				precise float dispNormalZ = dispSample * in_normal.z;
				precise float dispX = dispNormalX * dispc.y;
				precise float dispY = dispNormalY * dispc.y;
				precise float dispZ = dispNormalZ * dispc.y;

				// ── Acceleration / bend force ──────────────────────────────────
				// Convert _AccelerationDir to signed bend force via 1 - (|a|+1)^2.5,
				// scaled to [-4, 4].  Note: uses positive exponent (pass2 variant).
				precise float accelAbsX1 = abs(accel.x) + 1.0f;
				precise float accelAbsY1 = abs(accel.y) + 1.0f;
				precise float accelAbsZ1 = abs(accel.z) + 1.0f;
				precise float negAccelCurveX = -pow(accelAbsX1, 2.5f);  // -(|ax|+1)^2.5
				precise float negAccelCurveY = -pow(accelAbsY1, 2.5f);
				precise float negAccelCurveZ = -pow(accelAbsZ1, 2.5f);
				precise float accelCurveX = negAccelCurveX + 1.0f;       // 1 - (|ax|+1)^2.5
				precise float accelCurveY = negAccelCurveY + 1.0f;
				precise float accelCurveZ = negAccelCurveZ + 1.0f;
				precise float accelSignedX = accelCurveX * sign(accel.x);
				precise float accelSignedY = accelCurveY * sign(accel.y);
				precise float accelSignedZ = accelCurveZ * sign(accel.z);
				precise float bendForceX = accelSignedX * 4.0f;
				precise float bendForceY = accelSignedY * 4.0f;
				precise float bendForceZ = accelSignedZ * 4.0f;

				// ── Bend rotation ──────────────────────────────────────────────
				// Rotate XZ bend force by _BendRotationOffset·π to produce (bendDirU, bendDirV).
				precise float bendScaledX = bendForceX * accel.w;
				precise float bendScaledZ = bendForceZ * accel.w;
				precise float bendRotRad = _BendRotationOffset * 3.1415927f;
				float bendSin = sin(bendRotRad);
				float bendCos = cos(bendRotRad);
				precise float negBendSin = -bendSin;
				float bendDirU = dot(float2(negBendSin, bendCos), float2(bendScaledX, bendScaledZ));
				float bendDirV = dot(float2(bendCos,    bendSin), float2(bendScaledX, bendScaledZ));

				// ── Bend center offset ─────────────────────────────────────────
				precise float bendScaledU    = bendDirU * 0.16f;
				precise float bendScaledV    = bendDirV * 0.16f;
				precise float bendSqU        = bendScaledU * bendScaledU;
				precise float bendSqV        = bendScaledV * bendScaledV;
				precise float bendCurvatureU = sign(bendDirU) * bendSqU;
				precise float bendCurvatureV = sign(bendDirV) * bendSqV;
				precise float bendCenterU    = bendCurvatureU * _BendCenterOffsetMultipler;
				precise float bendCenterV    = bendCurvatureV * _BendCenterOffsetMultipler;
				precise float bendOffsetU    = bendCenterU * _BendCenterOffset;
				precise float bendOffsetV    = bendCenterV * _BendCenterOffset;

				// ── Displacement falloff ───────────────────────────────────────
				// smoothstep ramp along vFlipped controls displacement and bend intensity.
				precise float falloffInvGradient = 1.0f / dispc.w;
				precise float negV       = -in_texcoord.y;
				precise float vFlipped   = negV + 1.0f;                    // 0 at top, 1 at base
				precise float negPosOffset = -dispc.z;
				precise float falloffV   = vFlipped + negPosOffset;
				precise float bendLenWeighted = length(float3(bendForceX, bendForceY, bendForceZ)) * vFlipped;
				float bendLenFactor  = mad(bendLenWeighted, 0.01f, 1.0f);
				precise float bendLenSq    = bendLenFactor * bendLenFactor;
				precise float negBendLenSq = -bendLenSq;
				float bendLenShape   = mad(bendLenSq, bendLenSq, negBendLenSq);
				precise float bendCurveU = bendDirU * bendLenShape;
				precise float bendCurveV = bendDirV * bendLenShape;
				float falloffT    = clamp(falloffInvGradient * falloffV, 0.0f, 1.0f);
				precise float dispFalloff = smoothstepT(falloffT);

				// ── Displaced local position ───────────────────────────────────
				precise float bendCurveSqU    = bendCurveU * bendCurveU;
				precise float bendCurveSqV    = bendCurveV * bendCurveV;
				precise float bendCurveShapeU = bendCurveSqU * sign(bendCurveU);
				precise float bendCurveShapeV = bendCurveSqV * sign(bendCurveV);
				precise float localOffsetX = mad(dispX, dispFalloff, bendOffsetU) + bendCurveShapeU;
				precise float localOffsetY = mad(dispY, dispFalloff, bendOffsetV) + bendCurveShapeV;
				precise float localOffsetZ = dispZ * dispFalloff;
				precise float localX = localOffsetX + in_pos.x;
				precise float localY = localOffsetY + in_pos.y;
				precise float localZ = localOffsetZ + in_pos.z;

				// ── World / clip transform ─────────────────────────────────────
				float4 worldPos = mul(unity_ObjectToWorld, float4(localX, localY, localZ, 1.0f));
				float4 clipPos  = mul(unity_MatrixVP, worldPos);

				gl_Position = clipPos;
			#ifdef FOG_LINEAR
				out_fogZ = clipPos.z;
			#endif

				// ── World normal & light-space outputs ─────────────────────────
				float3 worldNormal = normalize(mul((float3x3)unity_WorldToObject, in_normal));
				out_worldNormal = worldNormal;
				out_worldPos    = worldPos.xyz;

			#if defined(POINT) || defined(SPOT)
				// Light-space position for per-pixel attenuation in the additive lighting pass.
				float4 lightSpacePos = mul(unity_WorldToLight, worldPos);
				out_lightPos.xyz = lightSpacePos.xyz;
			#ifdef SPOT
				out_lightPos.w = lightSpacePos.w;
			#endif
			#endif

				Vertex_Stage_Output stage_output;
				stage_output.gl_Position = gl_Position;
				stage_output.out_worldNormal = out_worldNormal;
				stage_output.out_worldPos = out_worldPos;
			#if defined(POINT) || defined(SPOT)
				stage_output.out_lightPos = out_lightPos;
			#endif
			#ifdef FOG_LINEAR
				stage_output.out_fogZ = out_fogZ;
			#endif
				return stage_output;
			}

			// ── Fragment shader ────────────────────────────────────────────────
			// Output: always (0,0,0,1).
			// This pass is a deferred additive lighting stub — it emits a black fragment so
			// that Unity's lighting pipeline can accumulate per-pixel point/spot contributions
			// on top of Pass 1's forward-lit result.  The fragment inputs (worldNormal, worldPos,
			// lightPos, fogCoord) are received from the vertex stage but are not used here.
			Fragment_Stage_Output frag(Fragment_Stage_Input stage_input)
			{
				worldNormal = stage_input.worldNormal;
				worldPos    = stage_input.worldPos;
			#if defined(POINT) || defined(SPOT)
				lightPos = stage_input.lightPos;
			#endif
			#ifdef FOG_LINEAR
				fogCoord = stage_input.fogCoord;
			#endif
				outColor.x = 0.0f;
				outColor.y = 0.0f;
				outColor.z = 0.0f;
				outColor.w = 1.0f;
				Fragment_Stage_Output stage_output;
				stage_output.outColor = outColor;
				return stage_output;
			}

			ENDHLSL
		}
	}
	CustomEditor "ASEMaterialInspector"
}
