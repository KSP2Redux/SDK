using KSP.Sim.Definitions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Slider that snaps its value to the nearest multiple of a step, clamped to a <see cref="SteppedRangeAttribute" />'s min and max.
    /// </summary>
    /// <remarks>
    /// Used for float SerializedProperties decorated with <c>[SteppedRange]</c>. The slider exposes its raw float to the UI but only writes the snapped value back to the SerializedProperty. A <see cref="VisualElementExtensions.TrackPropertyValue" /> hook keeps the slider in sync if the underlying property changes externally (Undo, programmatic edit, multi-target reconciliation).
    /// </remarks>
    public sealed class SteppedRangeField : Slider
    {
        private readonly SerializedProperty _prop;
        private readonly SteppedRangeAttribute _stepped;

        /// <summary>
        /// Creates a new <see cref="SteppedRangeField" /> bound to the given float property.
        /// </summary>
        /// <param name="prop">The float SerializedProperty to read and write.</param>
        /// <param name="stepped">The attribute supplying min, max, and step.</param>
        /// <param name="label">Optional label override. When null, the property's display name is used.</param>
        public SteppedRangeField(SerializedProperty prop, SteppedRangeAttribute stepped, string label = null)
            : base(label ?? prop.displayName, stepped.min, stepped.max)
        {
            _prop = prop;
            _stepped = stepped;

            showInputField = true;
            tooltip = prop.tooltip;
            AddToClassList("unity-base-field__aligned");
            SetValueWithoutNotify(prop.floatValue);

            this.RegisterValueChangedCallback(OnChanged);

            this.TrackPropertyValue(prop, p =>
            {
                if (!Mathf.Approximately(value, p.floatValue)) SetValueWithoutNotify(p.floatValue);
            });
        }

        private void OnChanged(ChangeEvent<float> evt)
        {
            var snapped = Mathf.Clamp(
                Mathf.Round(evt.newValue / _stepped.step) * _stepped.step,
                _stepped.min,
                _stepped.max);
            if (!Mathf.Approximately(snapped, _prop.floatValue))
            {
                _prop.serializedObject.Update();
                _prop.floatValue = snapped;
                _prop.serializedObject.ApplyModifiedProperties();
            }
            if (!Mathf.Approximately(snapped, evt.newValue))
            {
                SetValueWithoutNotify(snapped);
            }
        }
    }
}
