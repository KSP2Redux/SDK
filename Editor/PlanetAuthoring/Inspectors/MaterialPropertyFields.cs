using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// UI Toolkit field widgets bound to <see cref="Material" /> shader properties.
    /// </summary>
    /// <remarks>
    /// Each widget reads and writes through <c>material.Get*</c> and <c>material.Set*</c>,
    /// records an undoable edit on change, raises a per-edit callback so the live-preview
    /// pipeline can respond, and triggers a SceneView repaint. One widget per editable property
    /// kind needed for the celestial body local inspector. The bound material is captured
    /// per-instance, so building section trees is just
    /// <c>new MaterialColorField(mat, "_Tint", "Tint")</c>.
    /// </remarks>
    public static class MaterialPropertyFields
    {
        private const string Vector4ChannelsUxmlPath =
            "/Assets/Windows/PlanetAuthoring/PropertyFields/Vector4Channels.uxml";

        private const string PropertyFieldsUssPath =
            "/Assets/Windows/PlanetAuthoring/PropertyFields/PropertyFields.uss";

        // Common write path for the single-channel-of-Vector4 helpers (Float/Int/Toggle).
        // Centralizes the undo, dirty, and repaint dance so the three variants don't drift.
        private static void WriteVector4Channel(
            Material material,
            string propertyName,
            int channelIndex,
            float value,
            string label,
            Action onChanged
        )
        {
            if (material == null)
                return;
            Undo.RecordObject(material, $"Edit {label}");
            var v = material.GetVector(propertyName);
            v[channelIndex] = value;
            material.SetVector(propertyName, v);
            EditorUtility.SetDirty(material);
            onChanged?.Invoke();
            SceneView.RepaintAll();
        }

        // Looks up a SerializedProperty and warns once if it's missing. Mistyped paths would
        // otherwise produce dead fields that silently no-op.
        private static SerializedProperty FindOrWarn(SerializedObject so, string path, string contextLabel)
        {
            var prop = so?.FindProperty(path);
            if (prop == null)
                Debug.LogWarning($"[MaterialPropertyFields] '{path}' did not resolve on {so?.targetObject?.name} (field: {contextLabel}). The widget will be inert.");
            return prop;
        }

        // Loads the shared Vector4Channels UXML/USS and wires the field-label header. Used by
        // both Vector4Channels and MirroredVector4Channels.
        private static VisualElement LoadVector4ChannelsRoot(string label, string tooltip)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                SDKConfiguration.BasePath + Vector4ChannelsUxmlPath
            );
            var root = tree != null ? tree.Instantiate() : new VisualElement();

            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                SDKConfiguration.BasePath + PropertyFieldsUssPath
            );
            if (styles != null)
                root.styleSheets.Add(styles);

            var labelEl = root.Q<Label>("field-label");
            if (labelEl != null)
            {
                labelEl.text = label;
                labelEl.tooltip = tooltip;
            }

            return root;
        }

        // Clamps channelCount to [1, 4] and warns when an out-of-range value is passed.
        private static int ClampChannelCount(int channelCount, string label)
        {
            if (channelCount < 1 || channelCount > 4)
            {
                Debug.LogWarning($"[MaterialPropertyFields] channelCount {channelCount} on '{label}' out of range [1, 4]. Clamping.");
                return Mathf.Clamp(channelCount, 1, 4);
            }
            return channelCount;
        }

        /// <summary>
        /// Creates a texture field bound to a material texture property.
        /// </summary>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="MaterialTextureField" />.</returns>
        public static MaterialTextureField Texture(
            Material material,
            string propertyName,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            return new MaterialTextureField(material, propertyName, label, tooltip, onChanged);
        }

        /// <summary>
        /// Creates a color field bound to a material color property.
        /// </summary>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="MaterialColorField" />.</returns>
        public static MaterialColorField Color(
            Material material,
            string propertyName,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            return new MaterialColorField(material, propertyName, label, tooltip, onChanged);
        }

        /// <summary>
        /// Creates a Vector4 field bound to a material Vector4 property.
        /// </summary>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="MaterialVector4Field" />.</returns>
        public static MaterialVector4Field Vector4(
            Material material,
            string propertyName,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            return new MaterialVector4Field(material, propertyName, label, tooltip, onChanged);
        }

        /// <summary>
        /// Creates a float field bound to a material float property.
        /// </summary>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="MaterialFloatField" />.</returns>
        public static MaterialFloatField Float(
            Material material,
            string propertyName,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            return new MaterialFloatField(material, propertyName, label, tooltip, onChanged);
        }

        /// <summary>
        /// Creates a slider field bound to a material float property with a min/max range.
        /// </summary>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="min">The minimum value of the slider.</param>
        /// <param name="max">The maximum value of the slider.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="MaterialRangeField" />.</returns>
        public static MaterialRangeField Range(
            Material material,
            string propertyName,
            string label,
            float min,
            float max,
            string tooltip = "",
            Action onChanged = null
        )
        {
            return new MaterialRangeField(material, propertyName, label, min, max, tooltip, onChanged);
        }

        /// <summary>
        /// Creates a toggle bound to a material shader keyword.
        /// </summary>
        /// <param name="material">The material whose keyword is edited.</param>
        /// <param name="keywordName">The shader keyword name.</param>
        /// <param name="label">The display label for the toggle.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="MaterialKeywordToggle" />.</returns>
        public static MaterialKeywordToggle Keyword(
            Material material,
            string keywordName,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            return new MaterialKeywordToggle(material, keywordName, label, tooltip, onChanged);
        }

        /// <summary>
        /// Creates a fade-curve editor bound to a material property.
        /// </summary>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="FadeCurveField" />.</returns>
        public static FadeCurveField FadeCurve(
            Material material,
            string propertyName,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            return new FadeCurveField(material, propertyName, label, tooltip, onChanged);
        }

        /// <summary>
        /// Creates a trapezoid-window editor bound to a material property.
        /// </summary>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="axisMode">The axis mode the window operates on.</param>
        /// <param name="xMaxOverride">Optional override for the maximum X value, 0 uses the default.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="TrapezoidWindowField" />.</returns>
        public static TrapezoidWindowField TrapezoidWindow(
            Material material,
            string propertyName,
            string label,
            string tooltip,
            TrapezoidWindowField.AxisMode axisMode,
            float xMaxOverride = 0f,
            Action onChanged = null
        )
        {
            return new TrapezoidWindowField(material, propertyName, label, tooltip, axisMode, xMaxOverride, onChanged);
        }

        /// <summary>
        /// Creates a float field bound to one channel of a <see cref="Vector4" /> material property.
        /// </summary>
        /// <remarks>
        /// Reads <c>material.GetVector(propertyName)[channelIndex]</c>, writes it back via
        /// <c>SetVector</c> with undo and a SceneView repaint on change.
        /// </remarks>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="channelIndex">The Vector4 channel index (0 to 3).</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="FloatField" />.</returns>
        public static FloatField Vector4ChannelFloat(
            Material material,
            string propertyName,
            int channelIndex,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            var field = new FloatField(label) { tooltip = tooltip };
            if (material != null)
                field.SetValueWithoutNotify(material.GetVector(propertyName)[channelIndex]);
            field.RegisterValueChangedCallback(evt =>
                WriteVector4Channel(material, propertyName, channelIndex, evt.newValue, label, onChanged));
            return field;
        }

        /// <summary>
        /// Creates an integer field bound to one channel of a <see cref="Vector4" /> material property.
        /// </summary>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="channelIndex">The Vector4 channel index (0 to 3).</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="IntegerField" />.</returns>
        public static IntegerField Vector4ChannelInt(
            Material material,
            string propertyName,
            int channelIndex,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            var field = new IntegerField(label) { tooltip = tooltip };
            if (material != null)
                field.SetValueWithoutNotify((int)material.GetVector(propertyName)[channelIndex]);
            field.RegisterValueChangedCallback(evt =>
                WriteVector4Channel(material, propertyName, channelIndex, evt.newValue, label, onChanged));
            return field;
        }

        /// <summary>
        /// Creates a toggle bound to one channel of a <see cref="Vector4" /> material property.
        /// </summary>
        /// <remarks>
        /// Treats the channel as boolean (non-zero = on). Writes 1 or 0 back into the channel.
        /// </remarks>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="channelIndex">The Vector4 channel index (0 to 3).</param>
        /// <param name="label">The display label for the toggle.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="Toggle" />.</returns>
        public static Toggle Vector4ChannelToggle(
            Material material,
            string propertyName,
            int channelIndex,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            var field = new Toggle(label) { tooltip = tooltip };
            if (material != null)
                field.SetValueWithoutNotify(material.GetVector(propertyName)[channelIndex] > 0.5f);
            field.RegisterValueChangedCallback(evt =>
                WriteVector4Channel(material, propertyName, channelIndex, evt.newValue ? 1f : 0f, label, onChanged));
            return field;
        }

        /// <summary>
        /// Creates a bare <see cref="Toggle" /> bound to a material shader keyword, with no PQSData mirror.
        /// </summary>
        /// <remarks>
        /// Used when a keyword has no corresponding PQSData bool to mirror. Differs from
        /// <see cref="Keyword" /> by returning a bare Toggle (not a wrapping VisualElement), so the
        /// toggle aligns with sibling bare-Toggle rows in the same section.
        /// </remarks>
        /// <param name="material">The material whose keyword is toggled.</param>
        /// <param name="keywordName">The shader keyword name.</param>
        /// <param name="label">The display label for the toggle.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="Toggle" />.</returns>
        public static Toggle MaterialOnlyKeyword(
            Material material,
            string keywordName,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            var field = new Toggle(label) { tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            field.SetValueWithoutNotify(material != null && material.IsKeywordEnabled(keywordName));
            field.RegisterValueChangedCallback(evt =>
            {
                ApplyMaterialKeyword(material, keywordName, evt.newValue, label);
                onChanged?.Invoke();
                SceneView.RepaintAll();
            });
            return field;
        }

        /// <summary>
        /// Creates a Texture2D field bound to a material texture property, with no PQSData mirror.
        /// </summary>
        /// <remarks>
        /// Used when the slot is a bake-output target on the material (e.g. <c>_LargeGradienceR</c>)
        /// and there is no corresponding PQSData field to mirror to. Differs from <see cref="Texture" />,
        /// which returns a <see cref="MaterialTextureField" /> typed to <see cref="UnityEngine.Texture" />,
        /// by returning a bare <see cref="ObjectField" /> typed to <see cref="Texture2D" />.
        /// </remarks>
        /// <param name="material">The surface material whose property is edited.</param>
        /// <param name="materialPropertyName">The shader property name on the material.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="ObjectField" />.</returns>
        public static ObjectField MaterialOnlyTexture(
            Material material,
            string materialPropertyName,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            var field = new ObjectField(label) { objectType = typeof(Texture2D), tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            field.SetValueWithoutNotify(material != null ? material.GetTexture(materialPropertyName) : null);
            field.RegisterValueChangedCallback(evt =>
            {
                if (material != null)
                {
                    Undo.RecordObject(material, $"Edit {label}");
                    material.SetTexture(materialPropertyName, evt.newValue as Texture2D);
                    EditorUtility.SetDirty(material);
                }

                onChanged?.Invoke();
                SceneView.RepaintAll();
            });
            return field;
        }

        /// <summary>
        /// Creates a texture field whose value lives on a PQSData SerializedObject and is mirrored to a surface material property.
        /// </summary>
        /// <remarks>
        /// On every edit the value is written to both the PQSData property and the matching
        /// shader property on the surface material. Gives the artist immediate visual feedback
        /// in edit mode rather than waiting for the runtime push.
        /// </remarks>
        /// <param name="pqsDataSO">The serialized PQSData object backing the inspector.</param>
        /// <param name="pqsDataPath">The serialized property path on the PQSData object.</param>
        /// <param name="material">The surface material to mirror writes to.</param>
        /// <param name="materialPropertyName">The shader property name on the material.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="ObjectField" />.</returns>
        public static ObjectField MirroredTexture(
            SerializedObject pqsDataSO,
            string pqsDataPath,
            Material material,
            string materialPropertyName,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            var prop = FindOrWarn(pqsDataSO, pqsDataPath, label);
            var field = new ObjectField(label) { objectType = typeof(Texture2D), tooltip = tooltip };
            field.SetValueWithoutNotify(prop?.objectReferenceValue);
            field.RegisterValueChangedCallback(evt =>
            {
                if (prop != null)
                {
                    pqsDataSO.Update();
                    prop.objectReferenceValue = evt.newValue;
                    pqsDataSO.ApplyModifiedProperties();
                }

                if (material != null)
                {
                    Undo.RecordObject(material, $"Edit {label}");
                    material.SetTexture(materialPropertyName, evt.newValue as Texture2D);
                    EditorUtility.SetDirty(material);
                }

                onChanged?.Invoke();
                SceneView.RepaintAll();
            });
            return field;
        }

        /// <summary>
        /// Creates a boolean toggle bound to a PQSData bool field that also flips a shader keyword on the surface material.
        /// </summary>
        /// <remarks>
        /// Both sides update atomically so they cannot drift.
        /// </remarks>
        /// <param name="pqsDataSO">The serialized PQSData object backing the inspector.</param>
        /// <param name="pqsDataBoolPath">The serialized property path on the PQSData object.</param>
        /// <param name="material">The surface material whose keyword is toggled.</param>
        /// <param name="keywordName">The shader keyword name.</param>
        /// <param name="label">The display label for the toggle.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="Toggle" />.</returns>
        public static Toggle MirroredKeyword(
            SerializedObject pqsDataSO,
            string pqsDataBoolPath,
            Material material,
            string keywordName,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            var prop = FindOrWarn(pqsDataSO, pqsDataBoolPath, label);
            var field = new Toggle(label) { tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            var initial = material != null && material.IsKeywordEnabled(keywordName);
            field.SetValueWithoutNotify(initial);
            field.RegisterValueChangedCallback(evt =>
            {
                if (prop != null)
                {
                    pqsDataSO.Update();
                    prop.boolValue = evt.newValue;
                    pqsDataSO.ApplyModifiedProperties();
                }

                ApplyMaterialKeyword(material, keywordName, evt.newValue, label);
                onChanged?.Invoke();
                SceneView.RepaintAll();
            });
            return field;
        }

        // Atomic apply for a material-side shader keyword. Records undo and dirties the
        // material so the next save picks it up. Single source of truth shared by
        // MaterialOnlyKeyword and MirroredKeyword so they cannot drift.
        private static void ApplyMaterialKeyword(Material material, string keywordName, bool value, string label)
        {
            if (material == null) return;
            Undo.RecordObject(material, $"Toggle {label}");
            if (value)
                material.EnableKeyword(keywordName);
            else
                material.DisableKeyword(keywordName);
            EditorUtility.SetDirty(material);
        }

        /// <summary>
        /// Creates a read-only display of a shader keyword's current state.
        /// </summary>
        /// <remarks>
        /// Used for keywords that are runtime-managed (e.g. <c>DECALS_ENABLED</c> by
        /// PQSDecalController, <c>LOW_QUALITY</c> by view-distance heuristics) so editing them
        /// by hand would just be undone.
        /// </remarks>
        /// <param name="material">The material whose keyword is displayed.</param>
        /// <param name="keywordName">The shader keyword name.</param>
        /// <param name="label">The display label for the toggle.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <returns>The constructed read-only <see cref="Toggle" />.</returns>
        public static Toggle KeywordReadOnly(
            Material material,
            string keywordName,
            string label,
            string tooltip = ""
        )
        {
            var field = new Toggle(label) { tooltip = tooltip };
            field.AddToClassList("unity-base-field__aligned");
            field.SetValueWithoutNotify(material != null && material.IsKeywordEnabled(keywordName));
            field.SetEnabled(false);
            return field;
        }

        /// <summary>
        /// Creates a Vector4 material property displayed as four named channel inputs.
        /// </summary>
        /// <remarks>
        /// Channel labels default to R/G/B/A rather than X/Y/Z/W. Used for biome-axis Vector4s
        /// where the channel identity is meaningful and the value is a raw float per channel,
        /// not a color.
        /// </remarks>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="label">The display label for the field group.</param>
        /// <param name="tooltip">The tooltip shown on the field group.</param>
        /// <param name="channelLabels">Optional per-channel labels, defaults to R/G/B/A.</param>
        /// <param name="channelTooltips">Optional per-channel tooltips.</param>
        /// <param name="channelCount">The number of channels to expose, clamped to [1, 4].</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed channel-group <see cref="VisualElement" />.</returns>
        public static VisualElement Vector4Channels(
            Material material,
            string propertyName,
            string label,
            string tooltip = "",
            string[] channelLabels = null,
            string[] channelTooltips = null,
            int channelCount = 4,
            Action onChanged = null
        )
        {
            channelLabels ??= new[] { "R", "G", "B", "A" };
            channelCount = ClampChannelCount(channelCount, label);

            var root = LoadVector4ChannelsRoot(label, tooltip);
            var current = material != null ? material.GetVector(propertyName) : default;

            for (var i = 0; i < 4; i++)
            {
                var idx = i;
                var field = root.Q<FloatField>($"ch{i}");
                if (field == null)
                    continue;

                if (i >= channelCount)
                {
                    field.style.display = DisplayStyle.None;
                    continue;
                }

                var perChannelTooltip = channelTooltips != null && i < channelTooltips.Length
                    ? channelTooltips[i]
                    : tooltip;

                field.label = channelLabels[i];
                field.tooltip = perChannelTooltip;
                field.SetValueWithoutNotify(current[idx]);

                var inner = field.Q<Label>(className: "unity-base-field__label");
                if (inner != null)
                    inner.tooltip = perChannelTooltip;

                field.RegisterValueChangedCallback(evt =>
                    WriteVector4Channel(material, propertyName, idx, evt.newValue, label, onChanged));
            }

            return root;
        }

        /// <summary>
        /// Creates a read-only display of a material float property.
        /// </summary>
        /// <remarks>
        /// Used for runtime-driven values (planet radius, transition fade) so the artist can
        /// see the current value without being able to change it.
        /// </remarks>
        /// <param name="material">The material whose property is displayed.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <returns>The constructed read-only <see cref="MaterialFloatField" />.</returns>
        public static MaterialFloatField FloatReadOnly(
            Material material,
            string propertyName,
            string label,
            string tooltip = ""
        )
        {
            var field = new MaterialFloatField(material, propertyName, label, tooltip, null);
            field.SetEnabled(false);
            return field;
        }

        /// <summary>
        /// Creates a Vector4 mirrored between a PQSData field and a surface material property.
        /// </summary>
        /// <remarks>
        /// Each edit writes both sides for immediate edit-mode preview. Channel labels can be
        /// customized (default R/G/B/A) and tooltips can be set per channel.
        /// </remarks>
        /// <param name="pqsDataSO">The serialized PQSData object backing the inspector.</param>
        /// <param name="pqsDataPath">The serialized property path on the PQSData object.</param>
        /// <param name="material">The surface material to mirror writes to.</param>
        /// <param name="materialPropertyName">The shader property name on the material.</param>
        /// <param name="label">The display label for the field group.</param>
        /// <param name="tooltip">The tooltip shown on the field group.</param>
        /// <param name="channelLabels">Optional per-channel labels, defaults to R/G/B/A.</param>
        /// <param name="channelTooltips">Optional per-channel tooltips.</param>
        /// <param name="channelCount">The number of channels to expose, clamped to [1, 4].</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed channel-group <see cref="VisualElement" />.</returns>
        public static VisualElement MirroredVector4Channels(
            SerializedObject pqsDataSO,
            string pqsDataPath,
            Material material,
            string materialPropertyName,
            string label,
            string tooltip = "",
            string[] channelLabels = null,
            string[] channelTooltips = null,
            int channelCount = 4,
            Action onChanged = null
        )
        {
            channelLabels ??= new[] { "R", "G", "B", "A" };
            channelCount = ClampChannelCount(channelCount, label);

            var root = LoadVector4ChannelsRoot(label, tooltip);
            var prop = FindOrWarn(pqsDataSO, pqsDataPath, label);
            var current = prop != null ? prop.vector4Value : default;

            for (var i = 0; i < 4; i++)
            {
                var idx = i;
                var field = root.Q<FloatField>($"ch{i}");
                if (field == null)
                    continue;

                if (i >= channelCount)
                {
                    field.style.display = DisplayStyle.None;
                    continue;
                }

                var perChannelTooltip = channelTooltips != null && i < channelTooltips.Length
                    ? channelTooltips[i]
                    : tooltip;

                field.label = channelLabels[i];
                field.tooltip = perChannelTooltip;
                field.SetValueWithoutNotify(current[idx]);

                var inner = field.Q<Label>(className: "unity-base-field__label");
                if (inner != null)
                    inner.tooltip = perChannelTooltip;

                field.RegisterValueChangedCallback(evt =>
                {
                    if (prop != null)
                    {
                        pqsDataSO.Update();
                        var v = prop.vector4Value;
                        v[idx] = evt.newValue;
                        prop.vector4Value = v;
                        pqsDataSO.ApplyModifiedProperties();
                    }

                    if (material != null)
                    {
                        Undo.RecordObject(material, $"Edit {label}");
                        var matV = material.GetVector(materialPropertyName);
                        matV[idx] = evt.newValue;
                        material.SetVector(materialPropertyName, matV);
                        EditorUtility.SetDirty(material);
                    }

                    onChanged?.Invoke();
                    SceneView.RepaintAll();
                });
            }

            return root;
        }

        /// <summary>
        /// Creates an integer field bound to a PQSData int field that also writes into a single component of a surface material Vector4.
        /// </summary>
        /// <remarks>
        /// Used for per-biome ints that pack into shared vectors at runtime (UV scales etc.).
        /// </remarks>
        /// <param name="pqsDataSO">The serialized PQSData object backing the inspector.</param>
        /// <param name="pqsDataPath">The serialized property path on the PQSData object.</param>
        /// <param name="material">The surface material to mirror writes to.</param>
        /// <param name="materialVectorName">The shader Vector4 property name on the material.</param>
        /// <param name="channelIndex">The Vector4 channel index (0 to 3) to write into.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="IntegerField" />.</returns>
        public static IntegerField MirroredIntChannel(
            SerializedObject pqsDataSO,
            string pqsDataPath,
            Material material,
            string materialVectorName,
            int channelIndex,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            var prop = FindOrWarn(pqsDataSO, pqsDataPath, label);
            var field = new IntegerField(label) { tooltip = tooltip };
            if (prop != null)
            {
                field.BindProperty(prop);
                field.TrackPropertyValue(prop, p =>
                {
                    if (material != null)
                    {
                        Undo.RecordObject(material, $"Edit {label}");
                        var vec = material.GetVector(materialVectorName);
                        vec[channelIndex] = p.intValue;
                        material.SetVector(materialVectorName, vec);
                        EditorUtility.SetDirty(material);
                    }
                    onChanged?.Invoke();
                    SceneView.RepaintAll();
                });
            }
            return field;
        }

        /// <summary>
        /// Creates a float field bound to a PQSData float field with no material mirror.
        /// </summary>
        /// <remarks>
        /// Used when the runtime calculation that consumes the value is non-trivial and the
        /// inspector defers to the next sphere boot for the material side.
        /// </remarks>
        /// <param name="pqsDataSO">The serialized PQSData object backing the inspector.</param>
        /// <param name="pqsDataPath">The serialized property path on the PQSData object.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="FloatField" />.</returns>
        public static FloatField PqsDataFloat(
            SerializedObject pqsDataSO,
            string pqsDataPath,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            var prop = FindOrWarn(pqsDataSO, pqsDataPath, label);
            var field = new FloatField(label) { tooltip = tooltip };
            if (prop != null)
            {
                field.BindProperty(prop);
                if (onChanged != null)
                {
                    field.TrackPropertyValue(prop, _ =>
                    {
                        onChanged.Invoke();
                        SceneView.RepaintAll();
                    });
                }
            }
            return field;
        }

        /// <summary>
        /// Creates a texture field bound to a PQSData texture field with no material mirror.
        /// </summary>
        /// <param name="pqsDataSO">The serialized PQSData object backing the inspector.</param>
        /// <param name="pqsDataPath">The serialized property path on the PQSData object.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        /// <returns>The constructed <see cref="ObjectField" />.</returns>
        public static ObjectField PqsDataTexture(
            SerializedObject pqsDataSO,
            string pqsDataPath,
            string label,
            string tooltip = "",
            Action onChanged = null
        )
        {
            var prop = FindOrWarn(pqsDataSO, pqsDataPath, label);
            var field = new ObjectField(label)
            {
                tooltip = tooltip,
                objectType = typeof(Texture2D),
                allowSceneObjects = false,
            };
            if (prop != null)
            {
                field.BindProperty(prop);
                if (onChanged != null)
                {
                    field.TrackPropertyValue(prop, _ =>
                    {
                        onChanged.Invoke();
                        SceneView.RepaintAll();
                    });
                }
            }
            return field;
        }

        /// <summary>
        /// Creates a (Sx, Sy, Ox, Oy) UV scale and offset Vector4 with the canonical channel labels and tooltips.
        /// </summary>
        /// <remarks>
        /// Used by every triplanar layer and per-biome normal-map UV transform.
        /// </remarks>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="label">The display label for the field group.</param>
        /// <param name="tooltip">The tooltip shown on the field group.</param>
        /// <returns>The constructed UV-scale-and-offset <see cref="VisualElement" />.</returns>
        public static VisualElement UVScaleOffset(
            Material material,
            string propertyName,
            string label,
            string tooltip
        )
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
        /// Creates a mirrored (sz0 to sz3) subzone-filter Vector4 with canonical labels and per-channel tooltips referencing the biome character.
        /// </summary>
        /// <remarks>
        /// Used by every Large, Mid, and Subzone layer.
        /// </remarks>
        /// <param name="pqsDataSO">The serialized PQSData object backing the inspector.</param>
        /// <param name="pqsDataPath">The serialized property path on the PQSData object.</param>
        /// <param name="material">The surface material to mirror writes to.</param>
        /// <param name="materialPropertyName">The shader property name on the material.</param>
        /// <param name="biomeChar">The biome character (R, G, B, or A) shown in tooltips.</param>
        /// <param name="layerLabel">The layer label shown in the group tooltip.</param>
        /// <returns>The constructed subzone-filter <see cref="VisualElement" />.</returns>
        public static VisualElement SubzoneFilter(
            SerializedObject pqsDataSO,
            string pqsDataPath,
            Material material,
            string materialPropertyName,
            string biomeChar,
            string layerLabel
        )
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

    /// <summary>
    /// Base class for all material-bound widgets.
    /// </summary>
    /// <remarks>
    /// Wires undo, change events, and repaint.
    /// </remarks>
    public abstract class MaterialFieldBase : VisualElement
    {
        /// <summary>
        /// The material the widget reads and writes.
        /// </summary>
        protected readonly Material Material;

        /// <summary>
        /// The shader property name (or keyword name, for keyword toggles) the widget binds to.
        /// </summary>
        protected readonly string PropertyName;

        /// <summary>
        /// Optional callback invoked after each successful edit.
        /// </summary>
        protected readonly Action OnChanged;

        /// <summary>
        /// Initializes a new <see cref="MaterialFieldBase" /> capturing the bound material, property name, and change callback.
        /// </summary>
        /// <param name="material">The material the widget binds to.</param>
        /// <param name="propertyName">The shader property name (or keyword name).</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        protected MaterialFieldBase(Material material, string propertyName, Action onChanged)
        {
            Material = material;
            PropertyName = propertyName;
            OnChanged = onChanged;
        }

        /// <summary>
        /// Records an undoable edit on the bound material.
        /// </summary>
        /// <param name="description">The undo description shown in the Edit menu.</param>
        protected void RecordUndo(string description)
        {
            if (Material != null)
                Undo.RecordObject(Material, description);
        }

        /// <summary>
        /// Marks the bound material dirty, raises the change callback, and repaints the SceneView.
        /// </summary>
        protected void NotifyChanged()
        {
            if (Material != null)
                EditorUtility.SetDirty(Material);
            OnChanged?.Invoke();
            SceneView.RepaintAll();
        }
    }

    /// <summary>
    /// Texture object field bound to a material texture property.
    /// </summary>
    public class MaterialTextureField : MaterialFieldBase
    {
        /// <summary>
        /// Initializes a new <see cref="MaterialTextureField" /> bound to the given material property.
        /// </summary>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        public MaterialTextureField(
            Material material,
            string propertyName,
            string label,
            string tooltip,
            Action onChanged
        ) : base(material, propertyName, onChanged)
        {
            var field = new ObjectField(label) { objectType = typeof(Texture), tooltip = tooltip };
            field.SetValueWithoutNotify(material != null ? material.GetTexture(propertyName) : null);
            field.RegisterValueChangedCallback(evt =>
            {
                RecordUndo($"Edit {label}");
                Material.SetTexture(PropertyName, evt.newValue as Texture);
                NotifyChanged();
            });
            Add(field);
        }
    }

    /// <summary>
    /// Color field bound to a material color property.
    /// </summary>
    public class MaterialColorField : MaterialFieldBase
    {
        /// <summary>
        /// Initializes a new <see cref="MaterialColorField" /> bound to the given material property.
        /// </summary>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        public MaterialColorField(
            Material material,
            string propertyName,
            string label,
            string tooltip,
            Action onChanged
        ) : base(material, propertyName, onChanged)
        {
            var field = new ColorField(label) { showAlpha = true, hdr = true, tooltip = tooltip };
            field.SetValueWithoutNotify(material != null ? material.GetColor(propertyName) : default);
            field.RegisterValueChangedCallback(evt =>
            {
                RecordUndo($"Edit {label}");
                Material.SetColor(PropertyName, evt.newValue);
                NotifyChanged();
            });
            Add(field);
        }
    }

    /// <summary>
    /// Vector4 field bound to a material Vector4 property.
    /// </summary>
    public class MaterialVector4Field : MaterialFieldBase
    {
        /// <summary>
        /// Initializes a new <see cref="MaterialVector4Field" /> bound to the given material property.
        /// </summary>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        public MaterialVector4Field(
            Material material,
            string propertyName,
            string label,
            string tooltip,
            Action onChanged
        ) : base(material, propertyName, onChanged)
        {
            var field = new Vector4Field(label) { tooltip = tooltip };
            field.SetValueWithoutNotify(material != null ? material.GetVector(propertyName) : default);
            field.RegisterValueChangedCallback(evt =>
            {
                RecordUndo($"Edit {label}");
                Material.SetVector(PropertyName, evt.newValue);
                NotifyChanged();
            });
            Add(field);
        }
    }

    /// <summary>
    /// Float field bound to a material float property.
    /// </summary>
    public class MaterialFloatField : MaterialFieldBase
    {
        /// <summary>
        /// Initializes a new <see cref="MaterialFloatField" /> bound to the given material property.
        /// </summary>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        public MaterialFloatField(
            Material material,
            string propertyName,
            string label,
            string tooltip,
            Action onChanged
        ) : base(material, propertyName, onChanged)
        {
            var field = new FloatField(label) { tooltip = tooltip };
            field.SetValueWithoutNotify(material != null ? material.GetFloat(propertyName) : 0f);
            field.RegisterValueChangedCallback(evt =>
            {
                RecordUndo($"Edit {label}");
                Material.SetFloat(PropertyName, evt.newValue);
                NotifyChanged();
            });
            Add(field);
        }
    }

    /// <summary>
    /// Slider field bound to a material float property with a min and max range.
    /// </summary>
    public class MaterialRangeField : MaterialFieldBase
    {
        /// <summary>
        /// Initializes a new <see cref="MaterialRangeField" /> bound to the given material property.
        /// </summary>
        /// <param name="material">The material whose property is edited.</param>
        /// <param name="propertyName">The shader property name.</param>
        /// <param name="label">The display label for the field.</param>
        /// <param name="min">The minimum value of the slider.</param>
        /// <param name="max">The maximum value of the slider.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        public MaterialRangeField(
            Material material,
            string propertyName,
            string label,
            float min,
            float max,
            string tooltip,
            Action onChanged
        ) : base(material, propertyName, onChanged)
        {
            var field = new Slider(label, min, max) { showInputField = true, tooltip = tooltip };
            field.SetValueWithoutNotify(material != null ? material.GetFloat(propertyName) : min);
            field.RegisterValueChangedCallback(evt =>
            {
                RecordUndo($"Edit {label}");
                Material.SetFloat(PropertyName, evt.newValue);
                NotifyChanged();
            });
            Add(field);
        }
    }

    /// <summary>
    /// Toggle bound to a material shader keyword.
    /// </summary>
    public class MaterialKeywordToggle : MaterialFieldBase
    {
        /// <summary>
        /// Initializes a new <see cref="MaterialKeywordToggle" /> bound to the given material keyword.
        /// </summary>
        /// <param name="material">The material whose keyword is toggled.</param>
        /// <param name="keywordName">The shader keyword name.</param>
        /// <param name="label">The display label for the toggle.</param>
        /// <param name="tooltip">The tooltip shown on hover.</param>
        /// <param name="onChanged">Optional callback invoked after each edit.</param>
        public MaterialKeywordToggle(
            Material material,
            string keywordName,
            string label,
            string tooltip,
            Action onChanged
        ) : base(material, keywordName, onChanged)
        {
            var field = new Toggle(label) { tooltip = tooltip };
            field.SetValueWithoutNotify(material != null && material.IsKeywordEnabled(keywordName));
            field.RegisterValueChangedCallback(evt =>
            {
                RecordUndo($"Toggle {label}");
                if (evt.newValue)
                    Material.EnableKeyword(PropertyName);
                else
                    Material.DisableKeyword(PropertyName);
                NotifyChanged();
            });
            Add(field);
        }
    }
}
