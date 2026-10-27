using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PaintableShaderGUI : ShaderGUI
{
    private static readonly Dictionary<string, bool> Foldouts = new()
    {
        { "Surface", true },
        { "Paint", true },
        { "Normals", true },
        { "Occlusion", false },
        { "Emission", false },
        { "Rim", false },
        { "Advanced", false },
    };

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        EditorGUILayout.Space(2);
        EditorGUILayout.HelpBox(
            "KSP2 paintable part shader. Paint alpha controls coverage; alpha 0 shows the stock/unpainted material.",
            MessageType.None);

        DrawSurface(materialEditor, properties);
        DrawPaint(materialEditor, properties);
        DrawNormals(materialEditor, properties);
        DrawOcclusion(materialEditor, properties);
        DrawEmission(materialEditor, properties);
        DrawRim(materialEditor, properties);
        DrawAdvanced(materialEditor, properties);
    }

    private static void DrawSurface(MaterialEditor editor, MaterialProperty[] props)
    {
        if (!BeginSection("Surface", "Base textures and physical response for the unpainted stock material."))
        {
            return;
        }

        Texture(editor, props, "_MainTex", "Albedo", "Base color texture. Its RGB is preserved for colored metals such as foil or gold insulation.");
        Property(editor, props, "_Color", "Albedo Tint", "Multiplier applied to the albedo texture.");
        Texture(editor, props, "_MetallicGlossMap", "Metallic / Smoothness Map", "Packed stock part map. Metallic is read from green; smoothness is read from alpha.");
        Property(editor, props, "_Metallic", "Metallic Strength", "Scales the metallic channel from the packed map.");
        Property(editor, props, "_GlossMapScale", "Smoothness Strength", "Scales the smoothness channel from the packed map.");
        Property(editor, props, "_MipBias", "Texture Mip Bias", "Multiplies the final smoothness, as the game's part shader does with this value. Stock parts use 0.8.");

        EndSection();
    }

    private static void DrawPaint(MaterialEditor editor, MaterialProperty[] props)
    {
        if (!BeginSection("Paint", "Paint colors, masks, and the way painted areas override the underlying stock material."))
        {
            return;
        }

        Property(editor, props, "_PaintA", "Accent Paint", "Accent paint color. Alpha controls how strongly paint mask A is applied.");
        Property(editor, props, "_PaintB", "Base Paint", "Base paint color. Alpha controls how strongly paint mask B is applied.");
        Texture(editor, props, "_PaintMaskGlossMap", "Paint Mask / Paint Smoothness Map", "Packed paint map: red = accent paint, green = base paint, blue = paint brightness (dirt), alpha = optional paint smoothness.");
        Property(editor, props, "_PaintGlossMapScale", "Paint Smoothness Strength", "Scales paint smoothness on painted areas.");
        Property(editor, props, "_SmoothnessOverride", "Use Paint Mask Smoothness", "Use paint-mask alpha for painted smoothness instead of the metallic/smoothness map.");

        EndSection();
    }

    private static void DrawNormals(MaterialEditor editor, MaterialProperty[] props)
    {
        if (!BeginSection("Normals", "Normal maps for large stock details and optional fine detail."))
        {
            return;
        }

        Texture(editor, props, "_BumpMap", "Normal Map", "Main tangent-space normal map.");
        Texture(editor, props, "_DetailBumpMap", "Detail Normal Map", "Additional tiled normal detail blended over the main normal.");
        Texture(editor, props, "_DetailMask", "Detail Normal Mask", "Alpha controls where the detail normal is applied.");
        Property(editor, props, "_DetailBumpScale", "Detail Normal Strength", "Scales the detail normal intensity.");
        Property(editor, props, "_DetailBumpTiling", "Detail Normal Tiling", "Texture tiling multiplier for the detail normal.");

        EndSection();
    }

    private static void DrawOcclusion(MaterialEditor editor, MaterialProperty[] props)
    {
        if (!BeginSection("Occlusion", "Ambient occlusion used by the part shader and reflection fallback."))
        {
            return;
        }

        Texture(editor, props, "_OcclusionMap", "Occlusion Map", "Ambient occlusion texture. Green channel is used.");
        Property(editor, props, "_OcclusionStrength", "Occlusion Strength", "Controls how strongly the occlusion map darkens indirect light.");

        EndSection();
    }

    private static void DrawEmission(MaterialEditor editor, MaterialProperty[] props)
    {
        if (!BeginSection("Emission", "Static emissive texture plus optional gameplay/time-of-day controls."))
        {
            return;
        }

        Texture(editor, props, "_EmissionMap", "Emission Map", "RGB emission texture.");
        Property(editor, props, "_EmissionColor", "Emission Color", "HDR color multiplier for the emission map.");
        Property(editor, props, "_ReentryEmission", "Reentry Heating Enabled", "Lets the part glow with reentry heat. The game drives the glow per part.");
        Property(editor, props, "_UseTimeOfDay", "Use Sun-Angle Emission Fade", "Fade emission based on the surface angle to the sun.");
        Property(editor, props, "_TimeOfDayDotMin", "Sun Fade Start", "Sun dot-product value where time-of-day emission fade begins.");
        Property(editor, props, "_TimeOfDayDotMax", "Sun Fade End", "Sun dot-product value where time-of-day emission fade reaches full strength.");

        EndSection();
    }

    private static void DrawRim(MaterialEditor editor, MaterialProperty[] props)
    {
        if (!BeginSection("Rim", "Per-renderer rim emission used by some stock effects."))
        {
            return;
        }

        Property(editor, props, "_RimColor", "Rim Color", "RGB rim color. Alpha controls rim intensity.");
        Property(editor, props, "_RimFalloff", "Rim Falloff", "Controls how quickly rim intensity fades away from glancing angles.");

        EndSection();
    }

    private static void DrawAdvanced(MaterialEditor editor, MaterialProperty[] props)
    {
        if (!BeginSection("Advanced", "Low-level render-state controls. Most part materials should leave these at defaults."))
        {
            return;
        }

        Property(editor, props, "_Culling", "Cull Mode", "Triangle culling mode.");
        Property(editor, props, "_Offset", "Depth Offset", "Polygon offset used to resolve depth fighting.");

        EndSection();
    }

    private static bool BeginSection(string title, string description)
    {
        EditorGUILayout.Space(6);
        Foldouts[title] = EditorGUILayout.Foldout(Foldouts[title], title, true, EditorStyles.foldoutHeader);
        if (!Foldouts[title])
        {
            return false;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.HelpBox(description, MessageType.None);
        }

        EditorGUI.indentLevel++;
        return true;
    }

    private static void EndSection()
    {
        EditorGUI.indentLevel--;
    }

    private static void Property(MaterialEditor editor, MaterialProperty[] props, string name, string label, string tooltip)
    {
        var prop = FindOptionalProperty(props, name);
        if (prop == null)
        {
            return;
        }

        editor.ShaderProperty(prop, new GUIContent(label, tooltip));
    }

    private static void Texture(MaterialEditor editor, MaterialProperty[] props, string name, string label, string tooltip)
    {
        var prop = FindOptionalProperty(props, name);
        if (prop == null)
        {
            return;
        }

        editor.TexturePropertySingleLine(new GUIContent(label, tooltip), prop);
    }

    private static MaterialProperty FindOptionalProperty(MaterialProperty[] props, string name)
    {
        try
        {
            return FindProperty(name, props, false);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
