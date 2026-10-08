using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Fade-curve-specific palette and shape drawing.
    /// </summary>
    /// <remarks>
    /// Background, grid, handle, reference-line, and axis constants live on <see cref="GraphWidgetCommon" />.
    /// </remarks>
    internal static class FadeCurveGeometry
    {
        /// <summary>
        /// Stroke color used for the fade-curve outline.
        /// </summary>
        public static readonly Color CurveColor = new(0.55f, 0.85f, 1.0f);
        /// <summary>
        /// Fill color used under the fade curve.
        /// </summary>
        public static readonly Color FillColor = new(0.55f, 0.85f, 1.0f, 0.18f);

        /// <summary>
        /// Computes the X-axis upper bound for a fade-curve value.
        /// </summary>
        /// <param name="v">Fade-curve vector in <c>(start, range, near, far)</c> form.</param>
        /// <param name="xMaxOverride">Explicit X-axis upper bound. Non-positive values fall back to <see cref="GraphWidgetCommon.MinAxisMax" />.</param>
        /// <returns>The X-axis upper bound to use.</returns>
        public static float ComputeAxisMax(Vector4 v, float xMaxOverride = 0f)
        {
            var end = Mathf.Max(v.x + v.y, 0f);
            var minDefault = xMaxOverride > 0f ? xMaxOverride : GraphWidgetCommon.MinAxisMax;
            return Mathf.Max(end * GraphWidgetCommon.AxisHeadroom, minDefault);
        }

        /// <summary>
        /// Draws the fade-curve fill under the curve and the curve stroke on top.
        /// </summary>
        /// <remarks>
        /// The returned control points let the caller hit-test or draw handles on top of the shape.
        /// </remarks>
        /// <param name="painter">Painter to issue draw commands on.</param>
        /// <param name="value">Fade-curve vector in <c>(start, range, near, far)</c> form.</param>
        /// <param name="xMin">World-space X mapped to <c>rect.xMin</c>.</param>
        /// <param name="xMax">World-space X mapped to <c>rect.xMax</c>.</param>
        /// <param name="rect">Pixel-space rect to draw inside.</param>
        /// <param name="lineWidth">Stroke width for the curve outline.</param>
        /// <returns>The four pixel-space control points (pStart, pKneeLeft, pKneeRight, pEnd).</returns>
        public static (Vector2 pStart, Vector2 pKneeLeft, Vector2 pKneeRight, Vector2 pEnd) DrawCurve(
            Painter2D painter, Vector4 value, float xMin, float xMax, Rect rect, float lineWidth)
        {
            var pStart = GraphWidgetCommon.WorldToPixel(0f, value.z, xMin, xMax, rect);
            var pKneeLeft = GraphWidgetCommon.WorldToPixel(value.x, value.z, xMin, xMax, rect);
            var pKneeRight = GraphWidgetCommon.WorldToPixel(value.x + value.y, value.w, xMin, xMax, rect);
            var pEnd = GraphWidgetCommon.WorldToPixel(xMax, value.w, xMin, xMax, rect);

            painter.fillColor = FillColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMax));
            painter.LineTo(pStart);
            painter.LineTo(pKneeLeft);
            painter.LineTo(pKneeRight);
            painter.LineTo(pEnd);
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.ClosePath();
            painter.Fill();

            painter.strokeColor = CurveColor;
            painter.lineWidth = lineWidth;
            painter.BeginPath();
            painter.MoveTo(pStart);
            painter.LineTo(pKneeLeft);
            painter.LineTo(pKneeRight);
            painter.LineTo(pEnd);
            painter.Stroke();

            return (pStart, pKneeLeft, pKneeRight, pEnd);
        }
    }
}
