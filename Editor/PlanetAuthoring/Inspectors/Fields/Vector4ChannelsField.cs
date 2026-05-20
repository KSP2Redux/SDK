using Ksp2UnityTools.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Fields
{
    /// <summary>
    /// Four labeled <see cref="FloatField" />s in a single row, exposed as a <see cref="BaseField{T}" /> of <see cref="Vector4" />.
    /// </summary>
    /// <remarks>
    /// Used wherever a shader Vector4 is authored with per-channel meaning (R/G/B/A weights, Sx/Sy/Ox/Oy
    /// UV transform, sz0..sz3 subzone filter). Decoupled from any source of truth — callers wire it to a
    /// material or PQSData via the binder helpers and listen for value changes via the standard
    /// <c>RegisterValueChangedCallback</c> path.
    /// </remarks>
    public class Vector4ChannelsField : BaseField<Vector4>
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/Vector4Channels.uxml";
        private const string UssPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/PropertyFields.uss";

        private static readonly string[] DefaultChannelLabels = { "R", "G", "B", "A" };

        private readonly FloatField[] _channelFields = new FloatField[4];

        /// <summary>
        /// Creates a four-channel Vector4 field.
        /// </summary>
        /// <param name="label">Inspector label shown to the left of the channel row.</param>
        /// <param name="tooltip">Tooltip shown on hover (routed onto the label by BaseField).</param>
        /// <param name="channelLabels">Per-channel labels, defaults to R/G/B/A.</param>
        /// <param name="channelTooltips">Optional per-channel tooltips.</param>
        /// <param name="channelCount">Number of channels to expose, clamped to <c>[1, 4]</c>. Hidden channels keep their <see cref="Vector4" /> slot zeroed by callers.</param>
        public Vector4ChannelsField(
            string label,
            string tooltip = "",
            string[] channelLabels = null,
            string[] channelTooltips = null,
            int channelCount = 4
        ) : base(label, null)
        {
            this.tooltip = tooltip;
            AddToClassList("unity-base-field__aligned");

            channelLabels ??= DefaultChannelLabels;
            channelCount = Mathf.Clamp(channelCount, 1, 4);

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree != null)
                tree.CloneTree(this);

            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + UssPath);
            if (styles != null)
                styleSheets.Add(styles);

            for (var i = 0; i < 4; i++)
            {
                var idx = i;
                var field = this.Q<FloatField>($"ch{i}");
                _channelFields[i] = field;
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
                var inner = field.Q<Label>(className: "unity-base-field__label");
                if (inner != null)
                    inner.tooltip = perChannelTooltip;

                field.RegisterValueChangedCallback(evt =>
                {
                    var v = value;
                    v[idx] = evt.newValue;
                    this.value = v;
                });
            }
        }

        /// <inheritdoc />
        public override void SetValueWithoutNotify(Vector4 newValue)
        {
            base.SetValueWithoutNotify(newValue);
            for (var i = 0; i < 4; i++)
                _channelFields[i]?.SetValueWithoutNotify(newValue[i]);
        }
    }
}
