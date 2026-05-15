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
        /// Clamps a trapezoid value to the axis-mode domain.
        /// </summary>
        /// <remarks>
        /// Height mode forces all four edges to be >= 0. Slope mode additionally clamps the
        /// plateau (Start / End) into <c>[0, SlopeAxisMax]</c>. Fades stay free since the
        /// shader caps the input axis anyway and a fade extending past 90 deg simply reads as
        /// "still ramping at the cap".
        /// </remarks>
        /// <param name="mode">Axis interpretation.</param>
        /// <param name="start">Plateau start edge.</param>
        /// <param name="end">Plateau end edge.</param>
        /// <param name="fadeIn">Width of the quadratic fade-in below <paramref name="start" />.</param>
        /// <param name="fadeOut">Width of the quadratic fade-out above <paramref name="end" />.</param>
        /// <returns>The clamped <c>(start, end, fadeIn, fadeOut)</c> tuple.</returns>
        public static (float start, float end, float fadeIn, float fadeOut) ClampToDomain(
            TrapezoidWindowField.AxisMode mode, float start, float end, float fadeIn, float fadeOut)
        {
            start   = Mathf.Max(0f, start);
            end     = Mathf.Max(start, end);
            fadeIn  = Mathf.Max(0f, fadeIn);
            fadeOut = Mathf.Max(0f, fadeOut);
            if (mode == TrapezoidWindowField.AxisMode.Slope)
            {
                start = Mathf.Min(start, SlopeAxisMax);
                end   = Mathf.Min(end,   SlopeAxisMax);
            }
            return (start, end, fadeIn, fadeOut);
        }

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
        /// Number of straight-line segments used to approximate each quadratic ramp. 24 is enough
        /// to look smooth at typical thumbnail and popup graph sizes.
        /// </summary>
        private const int RampSegments = 24;

        /// <summary>
        /// Strokes and fills the trapezoid for <paramref name="value" /> across the given axis range.
        /// </summary>
        /// <remarks>
        /// Matches the runtime shader's <c>ComputeFadeTri</c> shape exactly. Quadratic fade-in
        /// from the left foot up to the plateau, flat plateau, quadratic fade-out down to the
        /// right foot. The fade-in width is <c>downRange</c> (NOT <c>fade</c>). The fade-out
        /// width is <c>fade</c>. The two are independent.
        /// </remarks>
        /// <param name="painter">Painter to issue draw commands on.</param>
        /// <param name="value">Trapezoid vector in <c>(center, upRange, downRange, fadeOut)</c> form.</param>
        /// <param name="xMin">World-space X mapped to <c>rect.xMin</c>.</param>
        /// <param name="xMax">World-space X mapped to <c>rect.xMax</c>.</param>
        /// <param name="rect">Pixel-space rect to draw inside.</param>
        /// <param name="lineWidth">Stroke width for the trapezoid outline.</param>
        /// <returns>
        /// The four pixel-space corner points (leftFoot, leftShoulder, rightShoulder, rightFoot)
        /// where leftFoot is the start of the quadratic fade-in, leftShoulder is where the plateau
        /// begins (= <c>center</c>), rightShoulder is where the plateau ends (= <c>center + up</c>),
        /// and rightFoot is the end of the quadratic fade-out.
        /// </returns>
        public static (Vector2 leftFoot, Vector2 leftShoulder, Vector2 rightShoulder, Vector2 rightFoot) DrawTrapezoid(
            Painter2D painter, Vector4 value, float xMin, float xMax, Rect rect, float lineWidth)
        {
            var center = value.x;
            var up = Mathf.Max(0f, value.y);
            var down = Mathf.Max(0f, value.z);
            var fade = Mathf.Max(0f, value.w);

            var leftFootX = center - down;
            var leftShoulderX = center;
            var rightShoulderX = center + up;
            var rightFootX = center + up + fade;

            var leftFoot      = GraphWidgetCommon.WorldToPixel(leftFootX,      0f, xMin, xMax, rect);
            var leftShoulder  = GraphWidgetCommon.WorldToPixel(leftShoulderX,  1f, xMin, xMax, rect);
            var rightShoulder = GraphWidgetCommon.WorldToPixel(rightShoulderX, 1f, xMin, xMax, rect);
            var rightFoot     = GraphWidgetCommon.WorldToPixel(rightFootX,     0f, xMin, xMax, rect);

            painter.fillColor = FillColor;
            painter.BeginPath();
            TracePath(painter, leftFoot, leftFootX, leftShoulderX, leftShoulder, rightShoulder, rightShoulderX, rightFootX, rightFoot, xMin, xMax, rect);
            painter.ClosePath();
            painter.Fill();

            painter.strokeColor = CurveColor;
            painter.lineWidth = lineWidth;
            painter.BeginPath();
            TracePath(painter, leftFoot, leftFootX, leftShoulderX, leftShoulder, rightShoulder, rightShoulderX, rightFootX, rightFoot, xMin, xMax, rect);
            painter.Stroke();

            return (leftFoot, leftShoulder, rightShoulder, rightFoot);
        }

        // Walks the trapezoid outline: leftFoot -> quadratic fade-in -> leftShoulder ->
        // plateau -> rightShoulder -> quadratic fade-out -> rightFoot. Always reaches every
        // corner explicitly even when a width is zero, so ClosePath can run the bottom edge
        // back to leftFoot horizontally instead of cutting a diagonal across the fill.
        private static void TracePath(
            Painter2D painter,
            Vector2 leftFoot, float leftFootX, float leftShoulderX, Vector2 leftShoulder,
            Vector2 rightShoulder, float rightShoulderX, float rightFootX, Vector2 rightFoot,
            float xMin, float xMax, Rect rect)
        {
            painter.MoveTo(leftFoot);

            var fadeInWidth = leftShoulderX - leftFootX;
            if (fadeInWidth > 0.0001f)
            {
                for (int i = 1; i < RampSegments; i++)
                {
                    float t = i / (float)RampSegments;
                    float x = leftFootX + fadeInWidth * t;
                    float y = t * t;
                    painter.LineTo(GraphWidgetCommon.WorldToPixel(x, y, xMin, xMax, rect));
                }
            }
            painter.LineTo(leftShoulder);
            painter.LineTo(rightShoulder);

            var fadeOutWidth = rightFootX - rightShoulderX;
            if (fadeOutWidth > 0.0001f)
            {
                for (int i = 1; i < RampSegments; i++)
                {
                    float t = i / (float)RampSegments;
                    float x = rightShoulderX + fadeOutWidth * t;
                    float y = (1f - t) * (1f - t);
                    painter.LineTo(GraphWidgetCommon.WorldToPixel(x, y, xMin, xMax, rect));
                }
            }
            painter.LineTo(rightFoot);
        }
    }
}
