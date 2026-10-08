// SDK copy of the game's drag cube bake shader KSP2/Aerodynamics/DragRender, under the same name so
// DragRenderer finds a shader when the SDK bakes drag cubes in a mod project, where the game's copy is not
// loaded. The pass is the URP port's own (Ksp2Redux Assets/ReduxAssets/Shaders/URP/Misc/KSP2DragRender.hlsl),
// included rather than copied, so drag cubes baked in a mod project match the ones the game bakes. Keep it in
// step with KSP2_Aerodynamics_DragRender.shader.
Shader "KSP2/Aerodynamics/DragRender"
{
    Properties
    {
        _BumpMap ("_BumpMap", 2D) = "bump" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BumpMap_ST;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "DragRender"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex KSP2DragRender_Vertex
            #pragma fragment KSP2DragRender_Fragment
            #include "Assets/ReduxAssets/Shaders/URP/Misc/KSP2DragRender.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
