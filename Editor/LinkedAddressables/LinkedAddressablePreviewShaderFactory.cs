using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    internal static class LinkedAddressablePreviewShaderFactory
    {
        public static Shader Create(Shader source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var shader = ShaderUtil.CreateShaderAsset(BuildSource(source), true);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"Unity could not create an editor preview shader for "
                        + $"linked shader '{source.name}'."
                );
            }

            var errors = ShaderUtil
                .GetShaderMessages(shader)
                .Where(
                    message =>
                        message.severity
                        == ShaderCompilerMessageSeverity.Error
                )
                .ToArray();
            if (errors.Length > 0)
            {
                var details = string.Join(
                    Environment.NewLine,
                    errors.Select(message =>
                        $"{message.message} ({message.file}:{message.line})"
                    )
                );
                UnityEngine.Object.DestroyImmediate(shader);
                throw new InvalidOperationException(
                    $"Could not compile the editor preview shader for linked "
                        + $"shader '{source.name}':{Environment.NewLine}{details}"
                );
            }

            shader.hideFlags = source.hideFlags;
            return shader;
        }

        private static string BuildSource(Shader source)
        {
            var properties = BuildProperties(source);
            var previewName =
                "Hidden/KSP2UnityTools/Linked Preview/"
                + GetStableNameHash(source.name)
                + "/"
                + EscapeShaderName(source.name);
            return $@"// Editor-only visual proxy for linked shader '{EscapeComment(source.name)}'.
// Player builds translate this persistent proxy PPtr back to the source shader.
Shader ""{previewName}""
{{
    Properties
    {{
{properties}
    }}
    SubShader
    {{
        Tags
        {{
            ""Queue"" = ""Transparent""
            ""IgnoreProjector"" = ""True""
            ""RenderType"" = ""Transparent""
            ""PreviewType"" = ""Plane""
            ""CanUseSpriteAtlas"" = ""True""
        }}

        Stencil
        {{
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }}

        Cull [_CullMode]
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {{
            Name ""KSP2UnityToolsLinkedPreview""
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include ""UnityCG.cginc""
            #include ""UnityUI.cginc""

            struct appdata_t
            {{
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            }};

            struct v2f
            {{
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            }};

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _MainTex_ST;
            float4 _ClipRect;

            v2f vert(appdata_t input)
            {{
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.color = input.color * _Color;
                return output;
            }}

            fixed4 frag(v2f input) : SV_Target
            {{
                fixed4 color =
                    (tex2D(_MainTex, input.texcoord) + _TextureSampleAdd)
                    * input.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(
                    input.worldPosition.xy,
                    _ClipRect
                );
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }}
            ENDCG
        }}
    }}
    Fallback Off
}}";
        }

        private static string BuildProperties(Shader source)
        {
            var result = new StringBuilder();
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < source.GetPropertyCount(); index++)
            {
                var name = source.GetPropertyName(index);
                if (!names.Add(name))
                    continue;
                result.Append("        ");
                result.Append(BuildAttributes(source, index));
                result.Append(name);
                result.Append(" (\"");
                result.Append(
                    EscapeQuotedString(source.GetPropertyDescription(index))
                );
                result.Append("\", ");
                result.Append(BuildPropertyDeclaration(source, index));
                result.AppendLine();
            }

            AddProperty(
                result,
                names,
                "[PerRendererData] _MainTex (\"Sprite Texture\", 2D) = \"white\" {}"
            );
            AddProperty(
                result,
                names,
                "_Color (\"Tint\", Color) = (1,1,1,1)"
            );
            AddProperty(
                result,
                names,
                "_StencilComp (\"Stencil Comparison\", Float) = 8"
            );
            AddProperty(
                result,
                names,
                "_Stencil (\"Stencil ID\", Float) = 0"
            );
            AddProperty(
                result,
                names,
                "_StencilOp (\"Stencil Operation\", Float) = 0"
            );
            AddProperty(
                result,
                names,
                "_StencilWriteMask (\"Stencil Write Mask\", Float) = 255"
            );
            AddProperty(
                result,
                names,
                "_StencilReadMask (\"Stencil Read Mask\", Float) = 255"
            );
            AddProperty(
                result,
                names,
                "_ColorMask (\"Color Mask\", Float) = 15"
            );
            AddProperty(
                result,
                names,
                "_CullMode (\"Cull Mode\", Float) = 0"
            );
            AddProperty(
                result,
                names,
                "[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip "
                    + "(\"Use Alpha Clip\", Float) = 0"
            );
            return result.ToString().TrimEnd();
        }

        private static void AddProperty(
            StringBuilder result,
            ISet<string> names,
            string declaration
        )
        {
            var nameStart = declaration.LastIndexOf(']') + 1;
            while (
                nameStart < declaration.Length
                && char.IsWhiteSpace(declaration[nameStart])
            )
            {
                nameStart++;
            }
            var nameEnd = declaration.IndexOf(' ', nameStart);
            var name = declaration.Substring(nameStart, nameEnd - nameStart);
            if (names.Add(name))
                result.AppendLine("        " + declaration);
        }

        private static string BuildAttributes(Shader source, int index)
        {
            var result = new List<string>();
            var flags = source.GetPropertyFlags(index).ToString();
            AddFlagAttribute(flags, "HideInInspector", result);
            AddFlagAttribute(flags, "PerRendererData", result);
            AddFlagAttribute(flags, "NoScaleOffset", result);
            AddFlagAttribute(flags, "Normal", result);
            AddFlagAttribute(flags, "HDR", result);
            AddFlagAttribute(flags, "Gamma", result);
            AddFlagAttribute(flags, "MainTexture", result);
            AddFlagAttribute(flags, "MainColor", result);
            foreach (var attribute in source.GetPropertyAttributes(index))
            {
                if (
                    !string.IsNullOrWhiteSpace(attribute)
                    && !result.Contains(attribute)
                )
                {
                    result.Add(attribute);
                }
            }
            return string.Concat(result.Select(attribute => $"[{attribute}] "));
        }

        private static void AddFlagAttribute(
            string flags,
            string attribute,
            ICollection<string> result
        )
        {
            if (
                flags.Split(',')
                    .Select(flag => flag.Trim())
                    .Contains(attribute, StringComparer.Ordinal)
            )
            {
                result.Add(attribute);
            }
        }

        private static string BuildPropertyDeclaration(
            Shader source,
            int index
        )
        {
            switch (source.GetPropertyType(index))
            {
                case ShaderPropertyType.Color:
                    return "Color) = "
                        + FormatVector(
                            source.GetPropertyDefaultVectorValue(index)
                        );
                case ShaderPropertyType.Vector:
                    return "Vector) = "
                        + FormatVector(
                            source.GetPropertyDefaultVectorValue(index)
                        );
                case ShaderPropertyType.Float:
                    return "Float) = "
                        + FormatFloat(
                            source.GetPropertyDefaultFloatValue(index)
                        );
                case ShaderPropertyType.Range:
                    var range = source.GetPropertyRangeLimits(index);
                    return $"Range({FormatFloat(range.x)}, {FormatFloat(range.y)})) = "
                        + FormatFloat(
                            source.GetPropertyDefaultFloatValue(index)
                        );
                case ShaderPropertyType.Int:
                    return "Int) = "
                        + source
                            .GetPropertyDefaultIntValue(index)
                            .ToString(CultureInfo.InvariantCulture);
                case ShaderPropertyType.Texture:
                    return BuildTextureDeclaration(source, index);
                default:
                    throw new InvalidOperationException(
                        $"Shader '{source.name}' property "
                            + $"'{source.GetPropertyName(index)}' uses unsupported "
                            + $"type '{source.GetPropertyType(index)}'."
                    );
            }
        }

        private static string BuildTextureDeclaration(
            Shader source,
            int index
        )
        {
            var dimension = source.GetPropertyTextureDimension(index);
            var type = dimension switch
            {
                TextureDimension.Tex2D => "2D",
                TextureDimension.Tex3D => "3D",
                TextureDimension.Cube => "Cube",
                TextureDimension.Tex2DArray => "2DArray",
                TextureDimension.CubeArray => "CubeArray",
                _ => "2D"
            };
            var defaultName = source.GetPropertyTextureDefaultName(index);
            if (string.IsNullOrWhiteSpace(defaultName))
                defaultName = "white";
            return $"{type}) = \"{EscapeQuotedString(defaultName)}\" {{}}";
        }

        private static string FormatVector(Vector4 value)
        {
            return $"({FormatFloat(value.x)},{FormatFloat(value.y)},"
                + $"{FormatFloat(value.z)},{FormatFloat(value.w)})";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string GetStableNameHash(string value)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter
                    .ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                    .Replace("-", "")
                    .Substring(0, 12);
            }
        }

        private static string EscapeShaderName(string value)
        {
            return EscapeQuotedString(value).Replace("/", "_");
        }

        private static string EscapeQuotedString(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private static string EscapeComment(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("*/", "* /");
        }
    }
}
