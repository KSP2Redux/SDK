using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Overlays
{
    /// <summary>
    /// Draws a small in-SceneView legend for each enabled preview overlay so the artist can read
    /// what the colors mean without leaving the viewport.
    /// </summary>
    /// <remarks>
    /// Registered with <see cref="SceneView.duringSceneGui" /> by <see cref="PreviewOverlayManager" />.
    /// Renders nothing when no overlays are enabled. Position is pinned to the top-right corner of
    /// the SceneView and stacks one section per active overlay.
    /// </remarks>
    internal static class PreviewOverlayLegend
    {
        // Mirror the shader Property defaults so the swatches match what the overlay actually draws.
        private static readonly Color BiomeR = new(1.00f, 0.30f, 0.30f);
        private static readonly Color BiomeG = new(0.30f, 1.00f, 0.30f);
        private static readonly Color BiomeB = new(0.30f, 0.50f, 1.00f);
        private static readonly Color BiomeA = new(1.00f, 0.95f, 0.30f);
        private static readonly Color SlopeFlat = new(0.10f, 0.85f, 0.20f);
        private static readonly Color SlopeVertical = new(1.00f, 0.10f, 0.10f);
        private static readonly Color ContourMinor = new(0.85f, 0.85f, 0.95f);
        private static readonly Color ContourMajor = new(1.00f, 0.85f, 0.30f);
        private static readonly float[] LayerBrightness = { 0.35f, 0.55f, 0.80f, 1.00f };

        private const float PanelWidth = 230f;
        private const float PanelMarginRight = 14f;
        // Clear Unity's bottom SceneView toolbar strip + status row so the panel never tucks under them.
        private const float PanelMarginBottom = 42f;
        private const float PanelPadding = 8f;
        private const float SectionGap = 6f;
        private const float RowHeight = 16f;
        private const float SwatchSize = 12f;
        private const float ContourSwatchWidth = 24f;

        private static Texture2D _slopeGradientTex;
        private static GUIStyle _headerStyle;
        private static GUIStyle _sectionStyle;
        private static GUIStyle _smallStyle;
        private static GUIStyle _smallStyleRight;
        private static GUIStyle _panelStyle;

        /// <summary>
        /// SceneView GUI callback that draws the legend panel when overlays are active.
        /// </summary>
        /// <param name="view">The SceneView currently being rendered.</param>
        public static void OnSceneGUI(SceneView view)
        {
            if (view == null || PreviewOverlayManager.EnabledKinds.Count == 0)
            {
                return;
            }
            // Overlays themselves only render under a live preview session, so the legend hides too.
            if (PlanetAuthoringSession.Active?.Pqs == null)
            {
                return;
            }

            EnsureStyles();

            Handles.BeginGUI();
            try
            {
                DrawPanel(view);
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        private static void DrawPanel(SceneView view)
        {
            var enabled = new List<PreviewOverlayKind>(PreviewOverlayManager.EnabledKinds);

            var totalHeight = PanelPadding * 2f + RowHeight;
            for (var i = 0; i < enabled.Count; i++)
            {
                totalHeight += GetSectionHeight(enabled[i]) + (i > 0 ? SectionGap : SectionGap);
            }

            var right = view.position.width - PanelMarginRight;
            var top = view.position.height - PanelMarginBottom - totalHeight;
            var panel = new Rect(right - PanelWidth, top, PanelWidth, totalHeight);

            GUI.Box(panel, GUIContent.none, _panelStyle);

            var innerX = panel.x + PanelPadding;
            var innerWidth = PanelWidth - PanelPadding * 2f;
            var y = panel.y + PanelPadding;

            GUI.Label(new Rect(innerX, y, innerWidth, RowHeight), "Overlays", _headerStyle);
            y += RowHeight;

            foreach (var kind in enabled)
            {
                y += SectionGap;
                y = DrawSection(new Rect(innerX, y, innerWidth, 0f), kind);
            }
        }

        private static float GetSectionHeight(PreviewOverlayKind kind) => kind switch
        {
            PreviewOverlayKind.BiomeMask     => RowHeight + RowHeight,                      // title + swatch row
            PreviewOverlayKind.SubzoneMask   => RowHeight + RowHeight,
            PreviewOverlayKind.Slope         => RowHeight + RowHeight,                      // title + gradient
            PreviewOverlayKind.AltitudeBands => RowHeight + RowHeight + RowHeight,          // title + minor + major
            PreviewOverlayKind.ActiveLayer   => RowHeight + RowHeight + 4f * SwatchSize + 4f, // title + biome row + 4x4 grid
            PreviewOverlayKind.ScienceRegion => ScienceRegionLegendHeight(),                // title + mode + optional stale row
            _ => RowHeight,
        };

        private static float ScienceRegionLegendHeight()
        {
            var overlay = PreviewOverlayManager.TryGetScienceRegionOverlay();
            var h = RowHeight + RowHeight; // title + mode
            if (overlay != null && (overlay.IsBakeStale || !overlay.HasScienceData || !overlay.HasBakedMap))
            {
                h += RowHeight;
            }
            return h;
        }

        private static float DrawSection(Rect r, PreviewOverlayKind kind) => kind switch
        {
            PreviewOverlayKind.BiomeMask     => DrawChannelSwatches(r, DrawTitle(r, r.y, "Biome mask")),
            PreviewOverlayKind.SubzoneMask   => DrawChannelSwatches(r, DrawTitle(r, r.y, "Subzone mask")),
            PreviewOverlayKind.Slope         => DrawSlopeGradient(r, DrawTitle(r, r.y, "Slope")),
            PreviewOverlayKind.AltitudeBands => DrawContourLegend(r, DrawTitle(r, r.y, "Altitude contours")),
            PreviewOverlayKind.ActiveLayer   => DrawActiveLayerGrid(r, DrawTitle(r, r.y, "Active small-biome layer")),
            PreviewOverlayKind.ScienceRegion => DrawScienceRegionLegend(r, DrawTitle(r, r.y, "Science region")),
            _ => r.y,
        };

        private static float DrawScienceRegionLegend(Rect r, float y)
        {
            var overlay = PreviewOverlayManager.TryGetScienceRegionOverlay();
            var modeLabel = PreviewOverlayManager.ScienceRegionMode == ScienceRegionPreviewOverlay.Mode.BakedPalette
                ? "Mode: baked palette"
                : "Mode: source texture";
            GUI.Label(new Rect(r.x, y, r.width, RowHeight), modeLabel, _smallStyle);
            y += RowHeight;

            if (overlay == null)
            {
                return y;
            }
            string warning = null;
            if (!overlay.HasScienceData)
            {
                warning = "No ScienceRegionData for this body.";
            }
            else if (!overlay.HasBakedMap)
            {
                warning = "Baked region map missing.";
            }
            else if (overlay.IsBakeStale)
            {
                warning = "Source newer than bake - re-bake.";
            }
            if (warning == null)
            {
                return y;
            }
            var prev = _smallStyle.normal.textColor;
            _smallStyle.normal.textColor = new Color(1.0f, 0.75f, 0.45f);
            GUI.Label(new Rect(r.x, y, r.width, RowHeight), warning, _smallStyle);
            _smallStyle.normal.textColor = prev;
            return y + RowHeight;
        }

        private static float DrawTitle(Rect r, float y, string title)
        {
            GUI.Label(new Rect(r.x, y, r.width, RowHeight), title, _sectionStyle);
            return y + RowHeight;
        }

        private static float DrawChannelSwatches(Rect r, float y)
        {
            (Color color, string label)[] entries =
            {
                (BiomeR, "R"), (BiomeG, "G"), (BiomeB, "B"), (BiomeA, "A"),
            };
            var cellWidth = r.width / entries.Length;
            for (var i = 0; i < entries.Length; i++)
            {
                var cellX = r.x + cellWidth * i;
                var swatchRect = new Rect(cellX, y + 1f, SwatchSize, SwatchSize);
                DrawSwatch(swatchRect, entries[i].color);
                GUI.Label(new Rect(swatchRect.xMax + 3f, y, cellWidth - SwatchSize - 4f, RowHeight), entries[i].label, _smallStyle);
            }
            return y + RowHeight;
        }

        private static float DrawSlopeGradient(Rect r, float y)
        {
            // Gradient bar fills the row width; labels sit beside it (not overlaid)
            // so neither end's label fights the underlying color.
            const float labelW = 36f;
            const float gap = 4f;
            var flatLabel = new Rect(r.x, y, labelW, RowHeight);
            var gradient = new Rect(r.x + labelW + gap, y + RowHeight * 0.5f - SwatchSize * 0.5f,
                                    r.width - labelW * 2f - gap * 2f, SwatchSize);
            var steepLabel = new Rect(gradient.xMax + gap, y, labelW, RowHeight);

            GUI.Label(flatLabel, "flat", _smallStyleRight);
            GUI.DrawTexture(gradient, GetSlopeGradient(), ScaleMode.StretchToFill);
            GUI.Label(steepLabel, "steep", _smallStyle);
            return y + RowHeight;
        }

        private static float DrawContourLegend(Rect r, float y)
        {
            var bandHeight = PreviewOverlayManager.BandHeightMeters;
            const float majorEvery = 5f; // Mirrors the shader default; expose if it becomes user-tunable.

            // Minor: thin 2px swatch centered vertically in the row.
            var minorSwatch = new Rect(r.x, y + RowHeight * 0.5f - 1f, ContourSwatchWidth, 2f);
            DrawSwatch(minorSwatch, ContourMinor);
            GUI.Label(
                new Rect(minorSwatch.xMax + 6f, y, r.width - ContourSwatchWidth - 6f, RowHeight),
                $"Minor every {bandHeight:0} m",
                _smallStyle);
            y += RowHeight;

            // Major: thicker 3px swatch.
            var majorSwatch = new Rect(r.x, y + RowHeight * 0.5f - 1.5f, ContourSwatchWidth, 3f);
            DrawSwatch(majorSwatch, ContourMajor);
            GUI.Label(
                new Rect(majorSwatch.xMax + 6f, y, r.width - ContourSwatchWidth - 6f, RowHeight),
                $"Major every {bandHeight * majorEvery:0} m",
                _smallStyle);
            return y + RowHeight;
        }

        private static float DrawActiveLayerGrid(Rect r, float y)
        {
            Color[] biomeColors = { BiomeR, BiomeG, BiomeB, BiomeA };
            string[] biomeLabels = { "R", "G", "B", "A" };

            // Column headers (Layer 1..4).
            const float headerW = 18f;
            const float cellSize = SwatchSize;
            const float cellGap = 2f;
            var gridX = r.x + headerW;

            for (var layer = 0; layer < 4; layer++)
            {
                var headerRect = new Rect(gridX + (cellSize + cellGap) * layer, y, cellSize, RowHeight);
                GUI.Label(headerRect, $"L{layer + 1}", _smallStyle);
            }
            y += RowHeight;

            for (var biome = 0; biome < 4; biome++)
            {
                GUI.Label(new Rect(r.x, y, headerW, cellSize), biomeLabels[biome], _smallStyle);
                for (var layer = 0; layer < 4; layer++)
                {
                    var cell = new Rect(gridX + (cellSize + cellGap) * layer, y, cellSize, cellSize);
                    var enabled = PreviewOverlayManager.IsLayerEnabled(biome, layer);
                    var c = biomeColors[biome] * LayerBrightness[layer];
                    c.a = enabled ? 1f : 0.18f;
                    DrawSwatch(cell, c);
                }
                y += cellSize + cellGap;
            }
            return y;
        }

        private static void DrawSwatch(Rect r, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, EditorGUIUtility.whiteTexture);
            GUI.color = prev;
        }

        private static Texture2D GetSlopeGradient()
        {
            if (_slopeGradientTex != null)
            {
                return _slopeGradientTex;
            }
            const int width = 64;
            _slopeGradientTex = new Texture2D(width, 1, TextureFormat.RGBA32, mipChain: false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "PreviewOverlayLegend_SlopeGradient",
            };
            var pixels = new Color32[width];
            for (var i = 0; i < width; i++)
            {
                var t = i / (float)(width - 1);
                var c = Color.Lerp(SlopeFlat, SlopeVertical, t);
                pixels[i] = c;
            }
            _slopeGradientTex.SetPixels32(pixels);
            _slopeGradientTex.Apply();
            return _slopeGradientTex;
        }

        private static void EnsureStyles()
        {
            if (_headerStyle != null)
            {
                return;
            }
            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.92f, 0.94f, 0.98f) },
            };
            _sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.82f, 0.86f, 0.92f) },
            };
            _smallStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.85f, 0.88f, 0.92f) },
            };
            _smallStyleRight = new GUIStyle(_smallStyle)
            {
                alignment = TextAnchor.MiddleRight,
            };
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(0, 0, 0, 0),
            };
        }
    }
}
