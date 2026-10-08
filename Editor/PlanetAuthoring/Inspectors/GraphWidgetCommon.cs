using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Drawing helpers, palette, and tuning shared by every graph-style property widget on this surface.
    /// </summary>
    /// <remarks>
    /// Used by trapezoid windows, fade curves, and future similar widgets. Widget-specific palette and
    /// shape-specific drawing live in their own geometry classes such as
    /// <see cref="TrapezoidWindowGeometry" /> and <see cref="FadeCurveGeometry" />.
    /// </remarks>
    internal static class GraphWidgetCommon
    {
        // Common palette
        /// <summary>
        /// Background fill color for graph rects.
        /// </summary>
        public static readonly Color BackgroundColor = new(0.16f, 0.16f, 0.16f);
        /// <summary>
        /// Stroke color used for grid lines.
        /// </summary>
        public static readonly Color GridColor = new(0.30f, 0.30f, 0.30f);
        /// <summary>
        /// Fill color for inactive drag handles.
        /// </summary>
        public static readonly Color HandleColor = new(1.0f, 1.0f, 1.0f);
        /// <summary>
        /// Fill color used to highlight the active drag handle.
        /// </summary>
        public static readonly Color HandleActiveColor = new(1.0f, 0.85f, 0.30f);
        /// <summary>
        /// Stroke color for the vertical reference line drawn at the current preview value.
        /// </summary>
        public static readonly Color RefLineColor = new(1.0f, 0.85f, 0.30f, 0.85f);

        // Axis tuning
        /// <summary>
        /// Minimum upper bound applied to the X axis when no override is in effect.
        /// </summary>
        public const float MinAxisMax = 5000f;
        /// <summary>
        /// Multiplier applied to the largest visible value when picking the X-axis upper bound.
        /// </summary>
        public const float AxisHeadroom = 1.1f;

        // Handle dimensions
        /// <summary>
        /// Visual radius of a drag handle in pixels.
        /// </summary>
        public const float HandleRadius = 6f;
        /// <summary>
        /// Hit-test radius of a drag handle in pixels.
        /// </summary>
        public const float HandleHitRadius = 14f;

        // Snap steps for fine-drag modifier-key support.
        /// <summary>
        /// Snap step in meters used for height-axis drags.
        /// </summary>
        public const float SnapStepHeight = 100f;
        /// <summary>
        /// Snap step in degrees used for slope-axis drags.
        /// </summary>
        public const float SnapStepSlope = 5f;
        /// <summary>
        /// Multiplier applied to drag deltas when the fine-drag modifier is held.
        /// </summary>
        public const float FineDragScale = 0.2f;

        /// <summary>
        /// Converts a world-space X coordinate and a normalized Y in <c>[0, 1]</c> to a pixel position inside <paramref name="rect" />.
        /// </summary>
        /// <param name="worldX">World-space X coordinate.</param>
        /// <param name="normalizedY">Normalized Y value in <c>[0, 1]</c>, where 0 is the bottom of the rect.</param>
        /// <param name="xMin">World-space X mapped to <c>rect.xMin</c>.</param>
        /// <param name="xMax">World-space X mapped to <c>rect.xMax</c>.</param>
        /// <param name="rect">Pixel-space rect to map into.</param>
        /// <returns>The pixel position corresponding to the supplied world coordinate.</returns>
        public static Vector2 WorldToPixel(float worldX, float normalizedY, float xMin, float xMax, Rect rect)
        {
            var px = Mathf.Lerp(rect.xMin, rect.xMax, Mathf.InverseLerp(xMin, xMax, worldX));
            var py = Mathf.Lerp(rect.yMax, rect.yMin, Mathf.Clamp01(normalizedY));
            return new Vector2(px, py);
        }

        /// <summary>
        /// Converts a pixel-space X coordinate inside <paramref name="rect" /> to a world-space X value in <c>[xMin, xMax]</c>.
        /// </summary>
        /// <param name="px">Pixel-space X coordinate.</param>
        /// <param name="xMin">World-space X mapped to <c>rect.xMin</c>.</param>
        /// <param name="xMax">World-space X mapped to <c>rect.xMax</c>.</param>
        /// <param name="rect">Pixel-space rect to map from.</param>
        /// <returns>The world-space X value at <paramref name="px" />.</returns>
        public static float PixelToWorldX(float px, float xMin, float xMax, Rect rect)
        {
            return Mathf.Lerp(xMin, xMax, Mathf.InverseLerp(rect.xMin, rect.xMax, px));
        }

        /// <summary>
        /// Fills <paramref name="rect" /> with the shared graph background color.
        /// </summary>
        /// <param name="painter">Painter to issue draw commands on.</param>
        /// <param name="rect">Rect to fill.</param>
        public static void DrawBackground(Painter2D painter, Rect rect)
        {
            painter.fillColor = BackgroundColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        /// <summary>
        /// Draws a 4x4 grid inside <paramref name="rect" /> using the shared grid color.
        /// </summary>
        /// <param name="painter">Painter to issue draw commands on.</param>
        /// <param name="rect">Rect to draw the grid inside.</param>
        public static void DrawGrid(Painter2D painter, Rect rect)
        {
            painter.strokeColor = GridColor;
            painter.lineWidth = 1f;
            for (var i = 1; i < 4; i++)
            {
                var py = Mathf.Lerp(rect.yMin, rect.yMax, i / 4f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, py));
                painter.LineTo(new Vector2(rect.xMax, py));
                painter.Stroke();
            }
            for (var i = 1; i < 4; i++)
            {
                var px = Mathf.Lerp(rect.xMin, rect.xMax, i / 4f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(px, rect.yMin));
                painter.LineTo(new Vector2(px, rect.yMax));
                painter.Stroke();
            }
        }

        /// <summary>
        /// Draws a circular drag handle at <paramref name="pos" />.
        /// </summary>
        /// <param name="painter">Painter to issue draw commands on.</param>
        /// <param name="pos">Pixel-space center of the handle.</param>
        /// <param name="active">True to render the handle in the active highlight color, false otherwise.</param>
        public static void DrawHandle(Painter2D painter, Vector2 pos, bool active)
        {
            painter.fillColor = active ? HandleActiveColor : HandleColor;
            painter.strokeColor = new Color(0, 0, 0, 0.6f);
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.Arc(pos, HandleRadius, Angle.Degrees(0), Angle.Degrees(360));
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }

        /// <summary>
        /// Draws a vertical reference line at <paramref name="worldX" /> when the value falls within the axis range.
        /// </summary>
        /// <remarks>
        /// Performs no <c>PlanetPreviewState</c> lookup. The caller picks the value source.
        /// Trapezoids use <c>TerrainElevationAtCamera</c>. Fade curves use <c>CameraDistanceFromSurface</c>.
        /// </remarks>
        /// <param name="painter">Painter to issue draw commands on.</param>
        /// <param name="worldX">World-space X coordinate at which to draw the line.</param>
        /// <param name="xMin">World-space X mapped to <c>rect.xMin</c>.</param>
        /// <param name="xMax">World-space X mapped to <c>rect.xMax</c>.</param>
        /// <param name="rect">Pixel-space rect to draw the line inside.</param>
        public static void DrawReferenceLineAt(Painter2D painter, float worldX, float xMin, float xMax, Rect rect)
        {
            if (worldX < xMin || worldX > xMax)
                return;
            var px = Mathf.Lerp(rect.xMin, rect.xMax, Mathf.InverseLerp(xMin, xMax, worldX));
            painter.strokeColor = RefLineColor;
            painter.lineWidth = 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(px, rect.yMin));
            painter.LineTo(new Vector2(px, rect.yMax));
            painter.Stroke();
        }

        /// <summary>
        /// Snaps <paramref name="value" /> to the nearest multiple of <paramref name="step" /> when <paramref name="snap" /> is true and <paramref name="step" /> is positive.
        /// </summary>
        /// <param name="value">Value to snap.</param>
        /// <param name="snap">True to apply snapping, false to return <paramref name="value" /> unchanged.</param>
        /// <param name="step">Snap step size. Non-positive values disable snapping.</param>
        /// <returns>The snapped value, or <paramref name="value" /> when snapping is disabled.</returns>
        public static float SnapIfNeeded(float value, bool snap, float step)
        {
            if (!snap || step <= 0f) return value;
            return Mathf.Round(value / step) * step;
        }

        /// <summary>
        /// Converts a shader property name into a human-readable title used for graph-popup window headings.
        /// </summary>
        /// <remarks>
        /// Strips a leading underscore and inserts spaces at lowercase-to-uppercase boundaries, runs of uppercase
        /// before a lowercase, and letter-to-digit or digit-to-letter boundaries. Turns
        /// <c>_SmallBiomeRHeightParams1</c> into <c>Small Biome R Height Params 1</c>.
        /// </remarks>
        /// <param name="raw">Raw shader property name.</param>
        /// <returns>The prettified display string, or <paramref name="raw" /> when it is null or empty.</returns>
        public static string PrettifyPropertyName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;
            var s = raw.TrimStart('_');
            var sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0)
                {
                    char prev = s[i - 1];
                    bool insertSpace =
                        (char.IsLower(prev) && char.IsUpper(c)) ||
                        (char.IsUpper(prev) && char.IsUpper(c) && i + 1 < s.Length && char.IsLower(s[i + 1])) ||
                        (char.IsLetter(prev) && char.IsDigit(c)) ||
                        (char.IsDigit(prev) && char.IsLetter(c));
                    if (insertSpace)
                        sb.Append(' ');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Builds the "Shift = fine drag, Ctrl = snap to ..." hint label shown below graph popups.
        /// </summary>
        /// <param name="snapUnit">Display string describing the snap unit (e.g. <c>"100 m"</c> or <c>"5 deg"</c>).</param>
        /// <returns>A styled label ready to be added to the popup root.</returns>
        public static Label BuildModifierHint(string snapUnit)
        {
            var hint = new Label($"Shift = fine drag ({FineDragScale}x), Ctrl = snap to {snapUnit}");
            hint.style.unityFontStyleAndWeight = FontStyle.Italic;
            hint.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            hint.style.fontSize = 10;
            hint.style.marginTop = 2;
            hint.style.marginLeft = 4;
            hint.style.marginRight = 4;
            return hint;
        }
    }
}
