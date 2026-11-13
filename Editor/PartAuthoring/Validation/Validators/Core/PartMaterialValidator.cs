using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Part material, normal import and paint contract checks for SDK authors.
    /// </summary>
    public class PartMaterialValidator : IPartValidator
    {
        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Prefab == null)
                yield break;

            bool expectsPaint = false;
            // Match Module_Color's data contract without referencing the game assembly.
            foreach (var data in context.ModuleDatas)
            {
                expectsPaint |= data.GetType().FullName == "KSP.Modules.Data_Color";
            }
            bool hasPaintShader = false;
            var checkedNormals = new HashSet<Texture>();
            foreach (Material material in Materials(context))
            {
                Shader shader = material.shader;
                string label = $"Material '{material.name}' ({AssetDatabase.GetAssetPath(material)})";
                if (shader == null || shader.name == "Hidden/InternalErrorShader")
                {
                    yield return new ValidationIssue("PART_SHADER_MISSING", ValidationSeverity.Error,
                        $"{label} has a missing or error shader.");
                    continue;
                }
                if (material.GetTag("RenderPipeline", false, "") != "UniversalPipeline")
                    yield return new ValidationIssue("PART_SHADER_NON_URP", ValidationSeverity.Warning,
                        $"{label} uses '{shader.name}' without the UniversalPipeline tag. Check runtime shader replacement.");
                if (ShaderUtil.ShaderHasError(shader))
                    yield return new ValidationIssue("PART_SHADER_IMPORT_ERROR", ValidationSeverity.Warning,
                        $"{label} shader '{shader.name}' has recorded import errors. Inspect the affected variants.");

                bool supportsPaint = material.HasProperty("_PaintA") && material.HasProperty("_PaintB");
                hasPaintShader |= supportsPaint;
                if (expectsPaint && supportsPaint && (!material.HasProperty("_PaintMaskGlossMap") || material.GetTexture("_PaintMaskGlossMap") == null))
                    yield return new ValidationIssue("PART_PAINT_MASK_MISSING", ValidationSeverity.Warning,
                        $"{label} receives part colors but has no paint mask assigned.");

                foreach (string property in material.GetTexturePropertyNames())
                {
                    Texture texture = material.GetTexture(property);
                    if (texture == null)
                    {
                        // Part surface shaders also supply white albedo and can still receive part colors.
                        bool isPartAlbedo = property == "_MainTex" &&
                            (shader.name is "KSP2/Parts/Paintable" or "KSP2/Scenery/Standard (Opaque)");
                        if (isPartAlbedo)
                        {
                            yield return new ValidationIssue("PART_MAIN_TEXTURE_MISSING", ValidationSeverity.Warning,
                                $"{label} has no {property} albedo texture. Confirm the shader's white default is intended.");
                        }
                        else if (property is "_MainTex" or "_BaseMap")
                        {
                            yield return new ValidationIssue("PART_TEXTURE_UNASSIGNED", ValidationSeverity.Warning,
                                $"{label} has no {property} texture. Confirm a constant-color surface is intended.");
                        }
                        // Optional normal, emission and detail slots have valid shader defaults.
                        continue;
                    }
                    if (property is not ("_BumpMap" or "_DetailBumpMap" or "_NormalMap"))
                        continue;
                    if (!checkedNormals.Add(texture))
                        continue;

                    string path = AssetDatabase.GetAssetPath(texture);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    bool hasExplicitBc1 = false;
                    if (importer != null)
                    {
                        if (importer.textureType != TextureImporterType.NormalMap)
                            yield return new ValidationIssue("PART_NORMAL_IMPORT_TYPE", ValidationSeverity.Warning,
                                $"{label} {property}: '{path}' is not imported as NormalMap.");
                        if (importer.sRGBTexture)
                            yield return new ValidationIssue("PART_NORMAL_SRGB", ValidationSeverity.Warning,
                                $"{label} {property}: '{path}' has sRGB enabled. Normals require linear sampling.");
                        foreach (string platform in new[] { "DefaultTexturePlatform", "Standalone", "Android", "iPhone" })
                        {
                            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platform);
                            if (platform != "DefaultTexturePlatform" && !settings.overridden)
                                continue;
                            if (settings.format is TextureImporterFormat.DXT1 or TextureImporterFormat.DXT1Crunched)
                            {
                                hasExplicitBc1 = true;
                                yield return new ValidationIssue("PART_NORMAL_BC1", ValidationSeverity.Warning,
                                    $"{label} {property}: '{path}' explicitly uses {settings.format} on {platform}. Review normal channel precision and packing.");
                            }
                        }
                    }
                    if (!hasExplicitBc1 && texture is Texture2D normal &&
                        (normal.format is TextureFormat.DXT1 or TextureFormat.DXT1Crunched))
                        yield return new ValidationIssue("PART_NORMAL_BC1", ValidationSeverity.Warning,
                            $"{label} {property}: '{path}' is loaded as {normal.format}. Review normal channel precision and packing.");
                }
            }
            if (expectsPaint && !hasPaintShader)
                yield return new ValidationIssue("PART_PAINT_SHADER_MISSING", ValidationSeverity.Warning,
                    "Data_Color is present but no renderer material exposes both _PaintA and _PaintB. Check procedural materials if generated at runtime.");

            foreach (Renderer renderer in context.Prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                        yield return new ValidationIssue("PART_MATERIAL_MISSING", ValidationSeverity.Warning,
                            $"Renderer '{renderer.name}' has an empty material slot.");
                }
            }
        }

        internal static IEnumerable<Material> Materials(PartValidationContext context)
        {
            var seen = new HashSet<Material>();
            foreach (Renderer renderer in context.Prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null && seen.Add(material))
                        yield return material;
                }
            }
        }
    }
}
