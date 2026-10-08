using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Shared SceneView HUD overlay for planet authoring EditorTools.
    /// </summary>
    /// <remarks>
    /// Draws a centered top label with a dark fill behind white text. Used by Surface Pick and
    /// Place Decal tools so their hint banners look identical.
    /// </remarks>
    internal static class PlanetEditorHud
    {
        private static GUIStyle _style;

        /// <summary>
        /// Draws the HUD banner centered at the top of <paramref name="sceneView" />.
        /// </summary>
        /// <param name="sceneView">The SceneView to draw into.</param>
        /// <param name="hint">The hint line shown on top.</param>
        /// <param name="readout">The optional readout line shown beneath the hint, or null to omit it.</param>
        public static void Draw(SceneView sceneView, string hint, string readout)
        {
            Handles.BeginGUI();
            _style ??= new GUIStyle(EditorStyles.label)
            {
                richText = true,
                padding = new RectOffset(10, 10, 6, 6),
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
            };

            var text = readout != null ? hint + "\n" + readout : hint;
            var content = new GUIContent(text);
            var size = _style.CalcSize(content);
            var height = _style.CalcHeight(content, size.x);
            var viewWidth = sceneView.position.width;
            var rect = new Rect((viewWidth - size.x) * 0.5f, 8, size.x, height);

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
            GUI.color = prev;
            GUI.Label(rect, content, _style);
            Handles.EndGUI();
        }
    }
}
