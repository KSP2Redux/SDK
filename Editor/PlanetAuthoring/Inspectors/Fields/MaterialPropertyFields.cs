using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Fields
{
    /// <summary>
    /// Static facade that composes the decoupled BaseField widgets with the material/PQSData binders.
    /// </summary>
    /// <remarks>
    /// The widget classes (<see cref="FadeCurveField" />, <see cref="TrapezoidWindowField" />,
    /// <see cref="Vector4ChannelsField" />, plus the bare Unity widgets) know nothing about
    /// <see cref="Material" /> or <see cref="SerializedObject" />. The binders (
    /// <see cref="MaterialBinder" />, <see cref="PqsDataBinder" />) wire a constructed widget
    /// to a source. This class is the convenience layer that does construct+configure+bind in one
    /// line so the per-section authoring builders stay compact. Section builders that need finer
    /// control can construct widgets and call binders directly.
    /// </remarks>
    public static class MaterialPropertyFields
    {
        // Looks up a SerializedProperty and warns once if it's missing. Mistyped paths would
        // otherwise produce dead fields that silently no-op.
        private static SerializedProperty FindOrWarn(SerializedObject so, string path, string contextLabel)
        {
            var prop = so?.FindProperty(path);
            if (prop == null)
                Debug.LogWarning($"[MaterialPropertyFields] '{path}' did not resolve on {so?.targetObject?.name} (field: {contextLabel}). The widget will be inert.");
            return prop;
        }

        // Bridges an Action onChanged callback into a value-changed listener on a field. Used by
        // legacy call sites that still pass an onChanged action to a facade method.
        private static void HookOnChanged<T>(BaseField<T> field, Action onChanged)
        {
            if (onChanged == null)
                return;
            field.RegisterValueChangedCallback(_ => onChanged());
        }

        /// <summary>Creates a texture field bound to a material texture property.</summary>
        public static ObjectField Texture(
            Material material, string propertyName, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new ObjectField(label) { objectType = typeof(Texture), tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            MaterialBinder.BindTexture(field, material, propertyName);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>Creates a color field bound to a material color property.</summary>
        public static ColorField Color(
            Material material, string propertyName, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new ColorField(label) { showAlpha = true, hdr = true, tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            MaterialBinder.BindColor(field, material, propertyName);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>Creates a Vector4 field bound to a material Vector4 property.</summary>
        public static Vector4Field Vector4(
            Material material, string propertyName, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new Vector4Field(label) { tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            MaterialBinder.BindVector4(field, material, propertyName);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>Creates a float field bound to a material float property.</summary>
        public static FloatField Float(
            Material material, string propertyName, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new FloatField(label) { tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            MaterialBinder.BindFloat(field, material, propertyName);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>Creates a slider bound to a material float property with a min/max range.</summary>
        public static Slider Range(
            Material material, string propertyName, string label,
            float min, float max, string tooltip = "", Action onChanged = null)
        {
            var field = new Slider(label, min, max) { showInputField = true, tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            MaterialBinder.BindSliderFloat(field, material, propertyName);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>Creates a toggle bound to a material shader keyword.</summary>
        public static Toggle Keyword(
            Material material, string keywordName, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new Toggle(label) { tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            MaterialBinder.BindKeyword(field, material, keywordName);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>Creates a fade-curve editor bound to a material Vector4 property.</summary>
        public static FadeCurveField FadeCurve(
            Material material, string propertyName, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new FadeCurveField(label, tooltip);
            MaterialBinder.BindFadeCurve(field, material, propertyName);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>Creates a trapezoid-window editor bound to a material Vector4 property.</summary>
        public static TrapezoidWindowField TrapezoidWindow(
            Material material, string propertyName, string label, string tooltip,
            TrapezoidWindowField.AxisMode axisMode, float xMaxOverride = 0f,
            Action onChanged = null)
        {
            var field = new TrapezoidWindowField(label, tooltip, axisMode, xMaxOverride);
            MaterialBinder.BindTrapezoidWindow(field, material, propertyName);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>Creates a float field bound to one channel of a Vector4 material property.</summary>
        public static FloatField Vector4ChannelFloat(
            Material material, string propertyName, int channelIndex, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new FloatField(label) { tooltip = tooltip };
            MaterialBinder.BindVector4Channel(field, material, propertyName, channelIndex);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>Creates an integer field bound to one channel of a Vector4 material property.</summary>
        public static IntegerField Vector4ChannelInt(
            Material material, string propertyName, int channelIndex, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new IntegerField(label) { tooltip = tooltip };
            MaterialBinder.BindVector4Channel(field, material, propertyName, channelIndex);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>Creates a toggle bound to one channel of a Vector4 material property (non-zero = on).</summary>
        public static Toggle Vector4ChannelToggle(
            Material material, string propertyName, int channelIndex, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new Toggle(label) { tooltip = tooltip };
            MaterialBinder.BindVector4Channel(field, material, propertyName, channelIndex);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>
        /// Creates a toggle bound to a material shader keyword, with no PQSData mirror.
        /// </summary>
        /// <remarks>Used when a keyword has no corresponding PQSData bool to mirror.</remarks>
        public static Toggle MaterialOnlyKeyword(
            Material material, string keywordName, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new Toggle(label) { tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            MaterialBinder.BindKeyword(field, material, keywordName);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>
        /// Creates a Texture2D-typed ObjectField bound to a material texture property, with no PQSData mirror.
        /// </summary>
        /// <remarks>
        /// Used when the slot is a bake-output target on the material (e.g. <c>_LargeGradienceR</c>)
        /// and there is no corresponding PQSData field to mirror to.
        /// </remarks>
        public static ObjectField MaterialOnlyTexture(
            Material material, string materialPropertyName, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new ObjectField(label) { objectType = typeof(Texture2D), tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            MaterialBinder.BindTexture(field, material, materialPropertyName);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>
        /// Creates a Texture2D field whose value lives on a PQSData SerializedObject and is mirrored to a surface material property.
        /// </summary>
        /// <remarks>
        /// Each edit writes both sides for immediate edit-mode preview.
        /// </remarks>
        public static ObjectField MirroredTexture(
            SerializedObject pqsDataSO, string pqsDataPath,
            Material material, string materialPropertyName, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new ObjectField(label) { objectType = typeof(Texture2D), tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            PqsDataBinder.BindTexture(field, pqsDataSO, pqsDataPath);
            MaterialBinder.BindTexture(field, material, materialPropertyName);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>
        /// Creates a boolean toggle bound to a PQSData bool field that also flips a shader keyword on the surface material.
        /// </summary>
        public static Toggle MirroredKeyword(
            SerializedObject pqsDataSO, string pqsDataBoolPath,
            Material material, string keywordName, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new Toggle(label) { tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            PqsDataBinder.BindBool(field, pqsDataSO, pqsDataBoolPath);
            MaterialBinder.BindKeyword(field, material, keywordName);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>
        /// Creates a read-only display of a shader keyword's current state.
        /// </summary>
        /// <remarks>
        /// Used for keywords that are runtime-managed (e.g. <c>DECALS_ENABLED</c>, <c>LOW_QUALITY</c>)
        /// so editing them by hand would just be undone.
        /// </remarks>
        public static Toggle KeywordReadOnly(
            Material material, string keywordName, string label, string tooltip = "")
        {
            var field = new Toggle(label) { tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            field.SetValueWithoutNotify(material != null && material.IsKeywordEnabled(keywordName));
            field.SetEnabled(false);
            return field;
        }

        /// <summary>Creates a Vector4 material property displayed as four named channel inputs.</summary>
        public static Vector4ChannelsField Vector4Channels(
            Material material, string propertyName, string label,
            string tooltip = "", string[] channelLabels = null, string[] channelTooltips = null,
            int channelCount = 4, Action onChanged = null)
        {
            var field = new Vector4ChannelsField(label, tooltip, channelLabels, channelTooltips, channelCount);
            MaterialBinder.BindVector4(field, material, propertyName);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>Creates a read-only display of a material float property.</summary>
        public static FloatField FloatReadOnly(
            Material material, string propertyName, string label, string tooltip = "")
        {
            var field = new FloatField(label) { tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            if (material != null)
                field.SetValueWithoutNotify(material.GetFloat(propertyName));
            field.SetEnabled(false);
            return field;
        }

        /// <summary>
        /// Creates a Vector4 mirrored between a PQSData field and a surface material property.
        /// </summary>
        /// <remarks>Each edit writes both sides for immediate edit-mode preview.</remarks>
        public static Vector4ChannelsField MirroredVector4Channels(
            SerializedObject pqsDataSO, string pqsDataPath,
            Material material, string materialPropertyName, string label,
            string tooltip = "", string[] channelLabels = null, string[] channelTooltips = null,
            int channelCount = 4, Action onChanged = null)
        {
            var field = new Vector4ChannelsField(label, tooltip, channelLabels, channelTooltips, channelCount);
            PqsDataBinder.BindVector4(field, pqsDataSO, pqsDataPath);
            MaterialBinder.BindVector4(field, material, materialPropertyName);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>
        /// Creates an integer field bound to a PQSData int field that also writes into a single component of a surface material Vector4.
        /// </summary>
        public static IntegerField MirroredIntChannel(
            SerializedObject pqsDataSO, string pqsDataPath,
            Material material, string materialVectorName, int channelIndex, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new IntegerField(label) { tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            PqsDataBinder.BindInt(field, pqsDataSO, pqsDataPath);
            MaterialBinder.BindVector4Channel(field, material, materialVectorName, channelIndex);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>Creates a float field bound to a PQSData float field with no material mirror.</summary>
        public static FloatField PqsDataFloat(
            SerializedObject pqsDataSO, string pqsDataPath, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new FloatField(label) { tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            PqsDataBinder.BindFloat(field, pqsDataSO, pqsDataPath);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>Creates a texture field bound to a PQSData texture field with no material mirror.</summary>
        public static ObjectField PqsDataTexture(
            SerializedObject pqsDataSO, string pqsDataPath, string label,
            string tooltip = "", Action onChanged = null)
        {
            var field = new ObjectField(label)
            {
                tooltip = tooltip,
                objectType = typeof(Texture2D),
                allowSceneObjects = false,
            };
            field.AddToClassList("unity-base-field__aligned");
            PqsDataBinder.BindTexture(field, pqsDataSO, pqsDataPath);
            HookOnChanged(field, onChanged);
            return field;
        }

        /// <summary>
        /// Creates a (Sx, Sy, Ox, Oy) UV scale and offset Vector4 with the canonical channel labels and tooltips.
        /// </summary>
        /// <remarks>Used by every triplanar layer and per-biome normal-map UV transform.</remarks>
        public static Vector4ChannelsField UVScaleOffset(
            Material material, string propertyName, string label, string tooltip)
        {
            return Vector4Channels(
                material, propertyName, label, tooltip,
                channelLabels: new[] { "Sx", "Sy", "Ox", "Oy" },
                channelTooltips: new[]
                {
                    "Triplanar projection scale on the X axis. Smaller values make tile prints larger on the surface.",
                    "Triplanar projection scale on the Y axis.",
                    "U-axis offset (shifts the projection horizontally).",
                    "V-axis offset (shifts the projection vertically).",
                }
            );
        }

        /// <summary>
        /// Creates a mirrored (sz0..sz3) subzone-filter Vector4 with canonical labels and per-channel tooltips referencing the biome character.
        /// </summary>
        /// <remarks>Used by every Large, Mid, and Subzone layer.</remarks>
        public static Vector4ChannelsField SubzoneFilter(
            SerializedObject pqsDataSO, string pqsDataPath,
            Material material, string materialPropertyName,
            string biomeChar, string layerLabel)
        {
            return MirroredVector4Channels(
                pqsDataSO, pqsDataPath,
                material, materialPropertyName,
                "Subzone filter",
                $"(sz0, sz1, sz2, sz3) dotted with the subzone mask to scale this biome's " +
                $"{layerLabel} contribution. Active only when SUB_ZONES_ENABLED. The inspector " +
                "writes both PQSData and material immediately for edit-mode preview.",
                channelLabels: new[] { "sz0", "sz1", "sz2", "sz3" },
                channelTooltips: new[]
                {
                    $"Filter weight for biome {biomeChar} in subzone channel 0.",
                    $"Filter weight for biome {biomeChar} in subzone channel 1.",
                    $"Filter weight for biome {biomeChar} in subzone channel 2.",
                    $"Filter weight for biome {biomeChar} in subzone channel 3.",
                }
            );
        }
    }
}
