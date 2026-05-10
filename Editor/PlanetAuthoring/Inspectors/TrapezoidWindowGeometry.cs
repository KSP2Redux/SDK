using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Trapezoid-window-specific palette and shape drawing.
    /// </summary>
    /// <remarks>
    /// Background, grid, handle, reference-line, and axis constants live on <see cref="GraphWidgetCommon" />.
    /// </remarks>
    internal static class TrapezoidWindowGeometry
    {
        /// <summary>
        /// Stroke color used for the trapezoid outline.
        /// </summary>
        public static readonly Color CurveColor = new(0.85f, 0.70f, 0.55f);
        /// <summary>
        /// Fill color used under the trapezoid.
        /// </summary>
        public static readonly Color FillColor = new(0.85f, 0.70f, 0.55f, 0.18f);
        /// <summary>
        /// Stroke color used for the foot-to-foot indicator drawn while a foot handle is active.
        /// </summary>
        public static readonly Color FootIndicatorColor = new(0.85f, 0.70f, 0.55f, 0.5f);

        /// <summary>
        /// Fixed upper bound used on the X axis when the field is in slope mode.
        /// </summary>
        public const float SlopeAxisMax = 90f;

        /// <summary>
        /// Computes the X-axis upper bound for a trapezoid value.
        /// </summary>
        /// <remarks>
        /// Slope mode is fixed at <see cref="SlopeAxisMax" />. Height mode uses
        /// <c>max(rightFoot * AxisHeadroom, max(xMaxOverride, MinAxisMax))</c>.
        /// </remarks>
        /// <param name="mode">Axis interpretation.</param>
        /// <param name="v">Trapezoid vector in <c>(center, upRange, downRange, fadeOut)</c> form.</param>
        /// <param name="xMaxOverride">Explicit X-axis upper bound for height mode. Non-positive values fall back to <see cref="GraphWidgetCommon.MinAxisMax" />.</param>
        /// <returns>The X-axis upper bound to use.</returns>
        public static float ComputeAxisMax(TrapezoidWindowField.AxisMode mode, Vector4 v, float xMaxOverride)
        {
            if (mode == TrapezoidWindowField.AxisMode.Slope)
                return SlopeAxisMax;
            var rightFoot = v.x + v.y + v.w;
            var minDefault = xMaxOverride > 0f ? xMaxOverride : GraphWidgetCommon.MinAxisMax;
            return Mathf.Max(rightFoot * GraphWidgetCommon.AxisHeadroom, minDefault);
        }

        /// <summary>
        /// Strokes and fills the trapezoid for <paramref name="value" /> across the given axis range.
        /// </summary>
        /// <remarks>
        /// The returned control points let the caller hit-test or draw handles on top of the shape.
        /// </remarks>
        /// <param name="painter">Painter to issue draw commands on.</param>
        /// <param name="value">Trapezoid vector in <c>(center, upRange, downRange, fadeOut)</c> form.</param>
        /// <param name="xMin">World-space X mapped to <c>rect.xMin</c>.</param>
        /// <param name="xMax">World-space X mapped to <c>rect.xMax</c>.</param>
        /// <param name="rect">Pixel-space rect to draw inside.</param>
        /// <param name="lineWidth">Stroke width for the trapezoid outline.</param>
        /// <returns>The four pixel-space control points (leftFoot, leftShoulder, rightShoulder, rightFoot).</returns>
        public static (Vector2 leftFoot, Vector2 leftShoulder, Vector2 rightShoulder, Vector2 rightFoot) DrawTrapezoid(
            Painter2D painter, Vector4 value, float xMin, float xMax, Rect rect, float lineWidth)
        {
            var center = value.x;
            var up = Mathf.Max(0f, value.y);
            var down = Mathf.Max(0f, value.z);
            var fade = Mathf.Max(0f, value.w);

            var leftFoot = GraphWidgetCommon.WorldToPixel(center - down - fade, 0f, xMin, xMax, rect);
            var leftShoulder = GraphWidgetCommon.WorldToPixel(center - down, 1f, xMin, xMax, rect);
            var rightShoulder = GraphWidgetCommon.WorldToPixel(center + up, 1f, xMin, xMax, rect);
            var rightFoot = GraphWidgetCommon.WorldToPixel(center + up + fade, 0f, xMin, xMax, rect);

            painter.fillColor = FillColor;
            painter.BeginPath();
            painter.MoveTo(leftFoot);
            painter.LineTo(leftShoulder);
            painter.LineTo(rightShoulder);
            painter.LineTo(rightFoot);
            painter.ClosePath();
            painter.Fill();

            painter.strokeColor = CurveColor;
            painter.lineWidth = lineWidth;
            painter.BeginPath();
            painter.MoveTo(leftFoot);
            painter.LineTo(leftShoulder);
            painter.LineTo(rightShoulder);
            painter.LineTo(rightFoot);
            painter.Stroke();

            return (leftFoot, leftShoulder, rightShoulder, rightFoot);
        }
    }
}
