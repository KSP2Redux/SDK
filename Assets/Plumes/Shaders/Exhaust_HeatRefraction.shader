Shader "Redux/VFX/Exhaust_HeatRefraction"
{
	Properties
	{
		[Header(Main)] _ScrollSpeedX ("Scroll Speed X", Float) = 0
		_ScrollSpeedY ("Scroll Speed Y", Float) = 0
		[Header(Refraction)] _RefractionAmount ("Refraction Amount", Range(0, 0.5)) = 0
		_BottomRefractionPosOffset ("Bottom Refraction Pos Offset", Range(0, 1)) = 0
		_BottomRefractionFalloffGradient ("Bottom Refraction Falloff Gradient", Range(0, 5)) = 0
		_TextureScaleX ("Texture Scale X", Float) = 1
		_TextureScaleY ("Texture Scale Y", Float) = 1
		[Normal] _RefractionTex ("RefractionTex", 2D) = "bump" {}
		[Header(Vertex Displacement)] _VertexDispScale ("Vertex Disp Scale", Range(0, 10)) = 0
		_VertexDispContrast ("Vertex Disp Contrast", Range(0.5, 5)) = 0
		_VertexDispPosOffset ("Vertex Disp Pos Offset", Range(0, 1)) = 0
		_VertexDispFalloffGradient ("Vertex Disp Falloff Gradient", Range(0, 3)) = 0
		[Header(Bending)] _AccelerationDir ("AccelerationDir", Vector) = (0,0,0,0)
		_AccelerationScaleFactor ("AccelerationScaleFactor", Float) = 0
		_BendRotationOffset ("BendRotationOffset", Float) = 0
	}
	SubShader
	{
		LOD 100
		Tags { "QUEUE" = "Overlay" "RenderType" = "Transparent" }
		GrabPass { }
		Pass
		{
			LOD 100
			Tags { "QUEUE" = "Overlay" "RenderType" = "Transparent" }
			Cull Off
			GpuProgramID 31338

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			// Property-to-buffer-index mappings (from original compiled shader):
			//
			//   vertex_uniform_buffer_0[10]:
			//     [4]  = (_ScrollSpeedX, _ScrollSpeedY, _TextureScaleX, _TextureScaleY)
			//     [5]  = (_VertexDispContrast, _VertexDispScale, _VertexDispPosOffset, _VertexDispFalloffGradient)
			//     [8]  = (_BendRotationOffset, _AccelerationDir.x, _AccelerationDir.y, _AccelerationDir.z)
			//     [9]  = (_AccelerationScaleFactor, _, _, _)
			//
			//   vertex_uniform_buffer_1[6]:
			//     [0]  = _Time (x,y,z,w)
			//     [5]  = _ProjectionParams (x,y,z,w)
			//
			//   vertex_uniform_buffer_2[8]:
			//     [0..3] = unity_ObjectToWorld columns 0-3 (row-major transpose)
			//     [4..7] = unity_WorldToObject columns 0-3
			//
			//   vertex_uniform_buffer_3[21]:
			//     [9..12]  = unity_MatrixV columns 0-3
			//     [17..20] = unity_MatrixVP columns 0-3
			//
			//   fragment_uniform_buffer_0[8]:
			//     [4]  = (_ScrollSpeedX, _ScrollSpeedY, _TextureScaleX, _TextureScaleY)
			//     [6]  = (_RefractionAmount, _BottomRefractionPosOffset, _BottomRefractionFalloffGradient, _)
			//     [7]  = _CameraDepthTexture_TexelSize (x,y,z,w)
			//
			//   fragment_uniform_buffer_1[8]:
			//     [0]  = _Time (x,y,z,w)
			//     [4]  = (_WorldSpaceCameraPos.x, .y, .z, _)
			//     [7]  = _ZBufferParams (x,y,z,w)

			// ---- Shared uniforms (vertex + fragment) -------------------------
			float _ScrollSpeedX;
			float _ScrollSpeedY;
			float _TextureScaleX;
			float _TextureScaleY;
			float4 _Time;

			Texture2D<float4> _RefractionTex;
			SamplerState sampler_RefractionTex;

			// ---- Vertex uniforms ---------------------------------------------
			float _VertexDispContrast;
			float _VertexDispScale;
			float _VertexDispPosOffset;
			float _VertexDispFalloffGradient;
			float _BendRotationOffset;
			float3 _AccelerationDir;
			float _AccelerationScaleFactor;
			float4 _ProjectionParams;
			float4x4 unity_ObjectToWorld;
			float4x4 unity_WorldToObject;
			float4x4 unity_MatrixV;
			float4x4 unity_MatrixVP;

			// ---- Fragment uniforms -------------------------------------------
			float _RefractionAmount;
			float _BottomRefractionPosOffset;
			float _BottomRefractionFalloffGradient;
			float4 _CameraDepthTexture_TexelSize;
			float3 _WorldSpaceCameraPos;
			float4 _ZBufferParams;

			Texture2D<float4> _CameraDepthTexture;
			Texture2D<float4> _GrabTexture;
			SamplerState sampler_CameraDepthTexture;
			SamplerState sampler_GrabTexture;

			// ---- Stage structs -----------------------------------------------
			struct Vertex_Stage_Input
			{
				float4 in_pos : POSITION;
				float4 in_color : COLOR;
				float4 in_texcoord : TEXCOORD0;
				float3 in_normal : NORMAL;
			};

			struct Vertex_Stage_Output
			{
				float4 out_uv : TEXCOORD1;
				float4 out_worldNormal : TEXCOORD2;
				float4 out_screenPos : TEXCOORD3;
				float3 out_worldPos : TEXCOORD4;
				float4 gl_Position : SV_Position;
			};

			struct Fragment_Stage_Input
			{
				float4 in_uv : TEXCOORD1;
				float4 in_worldNormal : TEXCOORD2;
				float4 in_screenPos : TEXCOORD3;
				float3 in_worldPos : TEXCOORD4;
				bool gl_FrontFacing : SV_IsFrontFace;
			};

			struct Fragment_Stage_Output
			{
				float4 out_color : SV_Target0;
			};

			// ---- Helper functions --------------------------------------------

			// Reconstruct linear eye-space depth from raw depth buffer sample.
			// Uses _ZBufferParams: (1-f/n, f/n, (1-f/n)/f, (f/n)/f) (Unity convention).
			float LinearEyeDepth(float rawDepth) {
				return 1.0f / mad(_ZBufferParams.z, rawDepth, _ZBufferParams.w);
			}

			// Normalize a direction vector, returning a near-zero result for degenerate input.
			float3 SafeNormalize(float3 v) {
				return v * rsqrt(max(dot(v, v), 0.001f));
			}

			// Scroll a UV by _Time.y * _ScrollSpeed on each axis.
			float2 ScrolledUV(float2 baseUV) {
				return float2(_Time.y * _ScrollSpeedX + baseUV.x,
				              _Time.y * _ScrollSpeedY + baseUV.y);
			}

			// Smoothstep polynomial: maps t in [0,1] to a smooth 0→1 curve (no clamp).
			// Equivalent to smoothstep(0, 1, t) for pre-clamped input.
			// Internal squaring is marked precise so callers can assign to precise float.
			float SmootherT(float t) {
				precise float sq = t * t;
				return sq * mad(t, -2.0f, 3.0f);
			}

			// ---- Vertex shader -----------------------------------------------

			/**
			 * Vertex shader — exhaust heat-refraction particle
			 *
			 * 1. Computes view-space depth and passes through UV and world-space normal.
			 * 2. Applies acceleration-driven bending: per-axis falloff (pow(|a|+1, -2.5) - 1) × sign(a) × 4,
			 *    rotated by _BendRotationOffset into local-space U/V offsets with quadratic curvature.
			 * 3. Displaces vertices along their object-space normal using a scrolling _RefractionTex sample
			 *    contrast-remapped and smoothstep-faded by vertical UV position.
			 * 4. Outputs clip position, screen-space grab UVs, world normal/position, and UV with view depth.
			 */
			Vertex_Stage_Output vert(Vertex_Stage_Input stage_input)
			{
				// --- Unpack inputs ---
				float3 objectPos    = stage_input.in_pos.xyz;
				float  objectPosW   = stage_input.in_pos.w;
				float2 uv           = stage_input.in_texcoord.xy;
				float3 objectNormal = stage_input.in_normal;

				// View-space depth from original object position
				float4 worldPos0 = mul(unity_ObjectToWorld, float4(objectPos, 1.0));
				float viewDepth = -mul(unity_MatrixV, worldPos0).z;

				// World normal (object normal transformed by inverse-transpose)
				float3 worldNormal = normalize(mul(objectNormal, (float3x3)unity_WorldToObject));

				// --- Acceleration/bending ---
				// Unrolled: (1 - pow(|a|+1, -2.5)) * sign(a) * 4 per component — acceleration falloff curve
				precise float3 accelAbs1  = abs(_AccelerationDir) + 1.0f;
				precise float3 accelMag   = 1.0f - exp2(log2(accelAbs1) * (-2.5f));
				precise float3 bendForce  = accelMag * sign(_AccelerationDir) * 4.0f;
				precise float2 bendScaled = bendForce.xz * _AccelerationScaleFactor;
				precise float  uvYFlipped = 1.0f - uv.y;
				precise float  bendLenWeighted = uvYFlipped * length(bendForce);
				precise float  falloffV = uvYFlipped - _VertexDispPosOffset;
				float  bendLenFactor = bendLenWeighted * 0.01f + 1.0f;
				precise float  bendLenSq = bendLenFactor * bendLenFactor;
				float  bendLenShape = bendLenSq * (bendLenSq - 1.0f);
				precise float  bendRotRad = _BendRotationOffset * 3.14159274f;
				float  bendSin = sin(bendRotRad);
				float  bendCos = cos(bendRotRad);
				precise float2 bendDir = bendLenShape * float2(
				    dot(float2(-bendSin, bendCos), bendScaled),
				    dot(float2( bendCos, bendSin), bendScaled));
				precise float2 bendOffset2D = bendDir * abs(bendDir);

				// --- Vertex displacement ---
				precise float2 scaledUV     = uv * float2(_TextureScaleX, _TextureScaleY);
				precise float  contrastInv  = 1.0f - _VertexDispContrast;
				precise float  contrastRange = _VertexDispContrast - contrastInv;
				float dispSample = _RefractionTex.SampleLevel(sampler_RefractionTex, ScrolledUV(scaledUV), 0.0f).x * contrastRange + contrastInv;
				precise float3 dispVec = objectNormal * dispSample * _VertexDispScale;
				// Unrolled: saturate(falloffV / _VertexDispFalloffGradient) — linearstep for displacement falloff
				precise float  falloffInvGradient = 1.0f / _VertexDispFalloffGradient;
				precise float  falloffRaw = falloffV * falloffInvGradient;
				float  falloffT = clamp(falloffRaw, 0.0f, 1.0f);
				precise float  dispFalloff = SmootherT(falloffT);
				precise float3 localPos = mad(dispVec, dispFalloff, float3(bendOffset2D, 0.0f)) + objectPos;

				// --- Clip space and screen-space grab UVs ---
				float4 displWorldPos = mul(unity_ObjectToWorld, float4(localPos, 1.0));
				float4 clipPos = mul(unity_MatrixVP, displWorldPos);
				// Unrolled: ComputeScreenPos(clipPos) — clip-to-screen UV computation
				precise float screenYProj = clipPos.y * _ProjectionParams.x;
				precise float screenYHalf = screenYProj * 0.5f;
				precise float screenXHalf = clipPos.x * 0.5f;
				precise float halfW       = clipPos.w * 0.5f;
				precise float screenU     = halfW + screenXHalf;
				precise float screenV     = halfW + screenYHalf;

				// --- Pack outputs ---
				Vertex_Stage_Output o;
				o.out_uv          = float4(uv, viewDepth, 0.0f);
				o.out_worldNormal  = float4(worldNormal, 0.0f);
				o.out_screenPos   = float4(screenU, screenV, clipPos.z, clipPos.w);
				o.out_worldPos    = mul(unity_ObjectToWorld, float4(localPos, objectPosW)).xyz;
				o.gl_Position     = clipPos;
				return o;
			}

			// ---- Fragment shader ---------------------------------------------

			/**
			 * Fragment shader — exhaust heat-refraction particle
			 *
			 * Refracts the background grab-pass texture by an offset derived from a scrolling normal map,
			 * attenuated by a Fresnel (NdotV smoothstep) mask, bottom and top vertical UV fade, and a
			 * depth-difference test that prevents refracting through solid geometry. The refracted UV is
			 * snapped to the nearest texel to avoid sub-pixel blur.
			 */
			Fragment_Stage_Output frag(Fragment_Stage_Input stage_input)
			{
				// --- Unpack inputs ---
				float2 uv          = stage_input.in_uv.xy;
				float  viewDepth   = stage_input.in_uv.z;
				float3 worldNormal = stage_input.in_worldNormal.xyz;
				float3 worldPos    = stage_input.in_worldPos;
				float4 screenPos   = stage_input.in_screenPos;
				bool   isFrontFace = stage_input.gl_FrontFacing;

				// View direction: camera to fragment, safe-normalized
				float3 viewDir = SafeNormalize(_WorldSpaceCameraPos - worldPos);

				// World normal: double-sided (flip on back face), then normalize
				float3 fragNormal = normalize(isFrontFace ? worldNormal : -worldNormal);

				// --- Fresnel mask (NdotV smoothstep curve) ---
				float NdotV = min(abs(dot(fragNormal, viewDir)), 1.0f);
				precise float fresnelSmooth = SmootherT(NdotV);

				// --- Bottom-fade refraction mask (vertical UV gradient) ---
				precise float bottomFadeV       = uv.y - _BottomRefractionPosOffset;
				precise float bottomFadeInvGrad = 1.0f / _BottomRefractionFalloffGradient;
				precise float bottomFadeRaw     = bottomFadeInvGrad * bottomFadeV;
				float bottomFadeT = clamp(bottomFadeRaw, 0.0f, 1.0f);
				precise float bottomFade = SmootherT(bottomFadeT);

				// --- Top-edge fade (near UV=1 vertical edge) ---
				precise float uvYFlipped = 1.0f - uv.y;
				precise float topFadeRaw = uvYFlipped * 100.0f;
				float topFadeT = clamp(topFadeRaw, 0.0f, 1.0f);
				precise float topFade = SmootherT(topFadeT);

				// --- Combined refraction scale ---
				// fresnel * bottom-fade * top-fade * amount / view-depth
				precise float fadeMask          = min(bottomFade, 1.0f) * min(topFade, 1.0f);
				precise float refractionMask    = fresnelSmooth * fadeMask;
				precise float refractionStrength = refractionMask * _RefractionAmount;
				precise float refractionScale   = refractionStrength / max(viewDepth, 0.1f);

				// --- Projected screen UV (perspective divide) ---
				precise float2 screenUV = screenPos.xy / screenPos.w;

				// --- Depth-intersection test (clip refraction at geometry in front) ---
				precise float sceneDepth = LinearEyeDepth(_CameraDepthTexture.Sample(sampler_CameraDepthTexture, screenUV).x);
				precise float depthDiff  = sceneDepth - viewDepth;
				precise float refractionOffset = clamp(depthDiff, 0.0f, 1.0f) * refractionScale;

				// --- Sample refraction normal map (scrolling) ---
				precise float2 refTexUV = uv * float2(_TextureScaleX, _TextureScaleY);
				float4 refTex = _RefractionTex.Sample(sampler_RefractionTex, ScrolledUV(refTexUV));
				precise float refNormalXEnc = refTex.w * refTex.x;
				// Unrolled: float2 refNormal = encoded * 2.0 - 1.0 — unpack normal XY from [0,1] to [-1,1]
				float2 refNormal = float2(refNormalXEnc, refTex.y) * 2.0f - 1.0f;
				precise float2 refOffset = refractionOffset * refNormal;

				// --- Second depth test at refracted UV (avoid refracting through solid geometry) ---
				precise float refractedSceneDepth = LinearEyeDepth(_CameraDepthTexture.Sample(sampler_CameraDepthTexture, refNormal * refractionOffset + screenUV).x);
				precise float refractedDepthDiff  = refractedSceneDepth - viewDepth;
				float refractedDepthMask = clamp(refractedDepthDiff, 0.0f, 1.0f);

				// --- Snap refracted UV to texel grid (pixel-perfect grab sample) ---
				precise float2 snapPx      = mad(refOffset, refractedDepthMask, screenUV) * _CameraDepthTexture_TexelSize.zw;
				precise float2 snapAligned = floor(snapPx) + 0.5f;
				precise float2 grabUV      = snapAligned * _CameraDepthTexture_TexelSize.xy;

				float4 grabColor = _GrabTexture.Sample(sampler_GrabTexture, grabUV);

				// --- Pack output ---
				Fragment_Stage_Output o;
				o.out_color = grabColor;
				return o;
			}

			ENDHLSL
		}
	}
}
