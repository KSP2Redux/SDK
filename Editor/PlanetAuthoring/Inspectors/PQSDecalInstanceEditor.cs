using System.Globalization;
using KSP;
using KSP.Rendering.Planets;
using KSP.Tools.PQSFreeCamUtils;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Custom inspector for <see cref="PQSDecalInstance" /> following the planet authoring inspector
    /// styling. Sections: Location, Placement, Template (with embedded PQSDecal SO editing), and
    /// per-instance Overrides.
    /// </summary>
    /// <remarks>
    /// No scene-view handles - decals are authored entirely through the inspector's lat/lon/scale
    /// fields. <c>Tools.hidden</c> is still synced to the preview-session state so the standard
    /// transform gizmo doesn't appear, because dragging the GameObject's transform wouldn't update
    /// the decal's <c>LatLong</c> and would silently snap back on the next <c>UpdateDecalTransform</c>.
    /// Known caveat: SyncToolsHidden mutates the global <c>UnityEditor.Tools.hidden</c> flag from
    /// a per-target editor instance. Process-global state is being driven from a per-instance
    /// editor surface, which can race with other editors that touch the same flag.
    /// </remarks>
    [CustomEditor(typeof(PQSDecalInstance))]
    public class PQSDecalInstanceEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/Inspectors/PQSDecalInstanceInspector.uxml";
        private const string UssPath = "/Assets/Windows/PlanetAuthoring/Inspectors/PQSDecalInstanceInspector.uss";

        /// <summary>
        /// Per-instance override field pairs on <see cref="PQSDecalInstance" /> exposed to the
        /// inspector. Each entry is (override-flag field name, value field name, label, tooltip).
        /// </summary>
        /// <remarks>
        /// Internal so the surface-landmark inspector can reuse the same set when inlining the
        /// managed decal's override section in cosmetic mode.
        /// </remarks>
        internal static readonly (string overrideField, string valueField, string label, string tooltip)[] OverridePairs =
        {
            ("OverrideHeightScale", "HeightScale", "Height Scale", "Vertical scale of the decal heightmap contribution, in meters."),
            ("OverrideHeightBlendMode", "HeightBlendMode", "Height Blend Mode", "How the decal heightmap combines with the surface (Add, Subtract, Replace)."),
            ("OverrideHeightOffset", "HeightOffset", "Height Offset", "Vertical bias added to the decal heightmap before blending, normalized to [-1, 1]."),
            ("OverrideFadeShape", "FadeShape", "Fade Shape", "Edge falloff shape for the decal alpha. Square = sharp rectangular, Circular = round soft-edged."),
            ("OverrideFadeStrength", "FadeStrength", "Fade Strength", "How aggressively the edge fade ramps in. 0 = no fade, 2 = strong fade."),
            ("OverrideAlbedoOpacity", "AlbedoOpacity", "Albedo Opacity", "Weight of the Diffuse (RG - BA) diff applied to the surface color. 0 hides the albedo contribution entirely."),
            ("OverrideNormalOpacity", "NormalOpacity", "Normal Opacity", "Weight of the decal's normal map applied to the surface normal. 0 hides normals."),
            ("OverrideGradientOpacity", "GradientOpacity", "Gradient Opacity", "Master multiplier on the decal's overall contribution. 0 makes the decal invisible regardless of other settings."),
            ("OverrideTint", "Tint", "Tint", "Color multiplied into the decal's diffuse contribution."),
            ("OverrideNormalBlend", "NormalBlend", "Normal Blend", "How the decal's normal samples combine with the surface normal (Blend or Replace)."),
            ("OverrideSortOrder", "SortOrder", "Sort Order", "Render order among overlapping decals. Higher draws on top."),
            ("OverrideMaterialScaleFactor", "MaterialScaleFactor", "Scale Factor", "Multiplier applied to the decal's transform scale during material-scale baking. Below 1 shrinks, above 1 enlarges."),
            ("OverrideUseAlphaMask", "UseAlphaMask", "Alpha Mask", "Whether the decal samples its alpha-mask texture to gate contribution."),
            ("OverrideUseDecalTexturing", "UseDecalTexturing", "Decal Textures", "Whether the decal samples its diffuse and normal textures. Off leaves only the heightmap contribution."),
            ("OverrideUseTextureAlphaMask", "UseTextureAlphaMask", "Mask Texture", "Whether the alpha-mask texture is consulted at all. Off falls back to the fade-shape alpha only."),
            ("OverrideUseTextureHeightmapFade", "UseTextureHeightmapFade", "Heightmap Fade", "Whether the heightmap modulates the alpha fade. Off makes alpha independent of height."),
        };

        private FloatField _latField;
        private FloatField _lonField;
        private VisualElement _templateSlot;
        private VisualElement _overridesSlot;
        private UnityEditor.Editor _templateEditor;
        private PQSDecal _trackedTemplate;

        private void OnEnable()
        {
            EditorApplication.update += SyncToolsHidden;
        }

        private void OnDisable()
        {
            EditorApplication.update -= SyncToolsHidden;
            UnityEditor.Tools.hidden = false;
            if (_templateEditor != null)
                DestroyImmediate(_templateEditor);
        }

        private void SyncToolsHidden()
        {
            UnityEditor.Tools.hidden = PlanetAuthoringSession.Active != null;
        }

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load PQSDecalInstanceInspector.uxml"));
                return root;
            }
            tree.CloneTree(root);

            Ksp2UnityToolsStyles.Apply(root, UssPath);

            var decal = (PQSDecalInstance)target;

            _latField = root.Q<FloatField>("lat-field");
            _lonField = root.Q<FloatField>("lon-field");
            _latField.SetValueWithoutNotify(decal.LatLong.x);
            _lonField.SetValueWithoutNotify(decal.LatLong.y);
            _latField.RegisterValueChangedCallback(_ => ApplyLatLonFromFields());
            _lonField.RegisterValueChangedCallback(_ => ApplyLatLonFromFields());

            root.Q<Button>("pick-button").clicked += OnPickClicked;
            root.Q<Button>("copy-button").clicked += OnCopyClicked;
            root.Q<Button>("paste-button").clicked += OnPasteClicked;

            _templateSlot = root.Q<VisualElement>("template-slot");
            _overridesSlot = root.Q<VisualElement>("overrides-slot");
            BuildOverrideRows();
            RebuildTemplateSection();
            // The template field can change at runtime. Rebuild the embedded SO editor when it does.
            root.schedule.Execute(MaybeRebuildTemplateSection).Every(500);

            root.Bind(serializedObject);
            return root;
        }

        private void BuildOverrideRows()
        {
            _overridesSlot.Clear();
            foreach (var pair in OverridePairs)
            {
                var overrideProp = serializedObject.FindProperty(pair.overrideField);
                var valueProp = serializedObject.FindProperty(pair.valueField);
                if (overrideProp == null || valueProp == null) continue;
                _overridesSlot.Add(BuildOverrideRow(overrideProp, valueProp, pair.label, pair.tooltip));
            }
        }

        /// <summary>
        /// Builds an override row: a toggle bound to <paramref name="overrideProp" /> alongside a
        /// PropertyField for <paramref name="valueProp" /> that is enabled only while the toggle is on.
        /// </summary>
        /// <remarks>
        /// Internal so the surface-landmark inspector can reuse the same row layout when inlining
        /// the managed decal's override section in cosmetic mode.
        /// </remarks>
        internal static VisualElement BuildOverrideRow(SerializedProperty overrideProp, SerializedProperty valueProp, string label, string tooltip)
        {
            var row = new VisualElement();
            row.AddToClassList("decal-inspector-override-row");

            var toggle = new Toggle { tooltip = "Use the per-instance value instead of the template's value for this field." };
            toggle.AddToClassList("decal-inspector-override-toggle");
            toggle.BindProperty(overrideProp);
            row.Add(toggle);

            var field = new PropertyField(valueProp, label) { tooltip = tooltip };
            field.AddToClassList("decal-inspector-override-field");
            field.AddToClassList("unity-base-field__aligned");
            field.SetEnabled(overrideProp.boolValue);
            toggle.RegisterValueChangedCallback(evt => field.SetEnabled(evt.newValue));
            row.Add(field);

            return row;
        }

        private void RebuildTemplateSection()
        {
            _templateSlot.Clear();
            var decal = (PQSDecalInstance)target;
            _trackedTemplate = decal.PQSDecal;
            if (_templateEditor != null)
            {
                DestroyImmediate(_templateEditor);
                _templateEditor = null;
            }
            if (_trackedTemplate == null)
            {
                _templateSlot.Add(new Label("No template assigned. Drop a PQSDecal asset above to edit its fields here.") { style = { whiteSpace = WhiteSpace.Normal, color = new Color(0.6f, 0.6f, 0.6f, 1f) } });
                return;
            }
            _templateEditor = CreateEditor(_trackedTemplate);
            // InspectorElement dispatches to PQSDecalEditor.CreateInspectorGUI when present, so the per-decal Textures section comes through. A bare IMGUIContainer would force the IMGUI default-path and skip the override.
            _templateSlot.Add(new InspectorElement(_templateEditor));
        }

        private void MaybeRebuildTemplateSection()
        {
            var decal = target as PQSDecalInstance;
            if (decal == null || _templateSlot == null) return;
            if (decal.PQSDecal != _trackedTemplate)
                RebuildTemplateSection();
        }

        private void ApplyLatLonFromFields()
        {
            var decal = (PQSDecalInstance)target;
            if (decal == null) return;
            var next = new Vector2(_latField.value, _lonField.value);
            if (decal.LatLong == next) return;
            Undo.RecordObject(decal, "Edit decal lat/lon");
            decal.LatLong = next;
            decal.UpdateDecalTransform();
            EditorUtility.SetDirty(decal);
        }

        private void OnPickClicked()
        {
            var decal = (PQSDecalInstance)target;
            if (decal == null) return;
            PlanetSurfacePickTool.Begin(latLon =>
            {
                Undo.RecordObject(decal, "Pick decal location");
                decal.LatLong = latLon;
                decal.UpdateDecalTransform();
                _latField.SetValueWithoutNotify(latLon.x);
                _lonField.SetValueWithoutNotify(latLon.y);
                EditorUtility.SetDirty(decal);
            });
        }

        private void OnCopyClicked()
        {
            EditorGUIUtility.systemCopyBuffer =
                $"{_latField.value.ToString("0.000000", CultureInfo.InvariantCulture)},{_lonField.value.ToString("0.000000", CultureInfo.InvariantCulture)}";
        }

        private void OnPasteClicked()
        {
            var clip = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(clip)) return;
            var parts = clip.Split(',');
            if (parts.Length != 2) return;
            if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) return;
            if (!float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) return;
            _latField.value = lat;
            _lonField.value = lon;
        }

    }
}
