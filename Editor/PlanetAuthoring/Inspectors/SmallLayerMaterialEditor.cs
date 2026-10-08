using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Custom inspector for <see cref="SmallLayerMaterial" /> that groups the SO's 15 visual-identity fields into the same sections the per-cell editor uses on a body.
    /// </summary>
    /// <remarks>
    /// Edits here propagate to every body whose <see cref="PQSDataAuthoring" /> references this SO,
    /// driven by <c>SmallLayerMaterialPostProcessor</c> on asset save. The inspector itself is a
    /// flat PropertyField list grouped by category - no override toggles, since the SO holds the
    /// defaults, not the overrides.
    /// </remarks>
    [CustomEditor(typeof(SmallLayerMaterial))]
    public class SmallLayerMaterialEditor : UnityEditor.Editor
    {
        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            Ksp2UnityToolsStyles.Apply(root);

            root.Add(BuildSection("Textures", new[]
            {
                ("AlbedoTexture", "Albedo"),
                ("NormalTexture", "Normal+SAO"),
                ("MetallicTexture", "Metallic"),
            }));

            root.Add(BuildSection("UV", new[]
            {
                ("UVScale", "Scale"),
                ("UVOffset", "Offset"),
            }));

            root.Add(BuildSection("Color grading", new[]
            {
                ("Tint", "Tint"),
                ("Brightness", "Brightness"),
                ("Contrast", "Contrast"),
                ("Saturation", "Saturation"),
            }));

            root.Add(BuildSection("PBR", new[]
            {
                ("NormalStrength", "Normal"),
                ("GlossStrength", "Gloss"),
                ("MetallicStrength", "Metallic"),
                ("AOStrength", "AO"),
            }));

            root.Add(BuildSection("Emission", new[]
            {
                ("EmissionStrength", "Strength"),
                ("EmissionColor", "Color"),
            }));

            return root;
        }

        private VisualElement BuildSection(string title, (string propertyName, string label)[] rows)
        {
            var foldout = new Foldout { text = title, value = true };
            foldout.AddToClassList("sdk-section");
            foreach (var (propertyName, label) in rows)
            {
                var prop = serializedObject.FindProperty(propertyName);
                if (prop == null) continue;
                var field = new PropertyField(prop, label);
                field.AddToClassList("unity-base-field__aligned");
                foldout.Add(field);
            }
            return foldout;
        }

        /// <inheritdoc />
        public override bool HasPreviewGUI() => true;

        /// <inheritdoc />
        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (Event.current.type == EventType.Repaint)
                background.Draw(r, false, false, false, false);

            var so = target as SmallLayerMaterial;
            if (so == null) return;

            var tex = SmallLayerMaterialPreview.Render(so);
            if (tex != null)
                GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, alphaBlend: false);
        }

        /// <inheritdoc />
        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            var so = AssetDatabase.LoadAssetAtPath<SmallLayerMaterial>(assetPath);
            if (so == null) return base.RenderStaticPreview(assetPath, subAssets, width, height);
            return SmallLayerMaterialPreview.Render(so);
        }
    }
}
