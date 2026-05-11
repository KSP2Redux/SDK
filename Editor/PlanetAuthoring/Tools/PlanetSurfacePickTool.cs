using KSP.Rendering.Planets;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// SceneView EditorTool that converts a left-click on the active planet into a (latitude, longitude) callback.
    /// </summary>
    /// <remarks>
    /// Activated programmatically via <see cref="Begin" /> from "Pick on planet" buttons in the inspector and other
    /// authoring windows. The tool is one-shot - the callback fires once on a successful pick and the previous tool
    /// is restored. Hovering over the planet draws a yellow wire disc on the surface and a HUD readout of the live
    /// (lat, lon, altitude) under the cursor. Pressing Esc cancels without firing. The tool auto-deactivates when
    /// the active preview session ends.
    /// </remarks>
    [EditorTool("Pick Surface Point")]
    public sealed class PlanetSurfacePickTool : EditorTool
    {
        /// <summary>Callback signature for surface picks.</summary>
        /// <param name="latLon">(latitude, longitude) in degrees, in the body's local frame.</param>
        public delegate void PickHandler(Vector2 latLon);

        private const string ToolName = "Pick Surface Point";
        private const string HudHint = "Click on the planet to pick a point. Press Esc to cancel.";
        private const string NoSessionHint = "No planet preview is active. Enable preview on a body before using the surface pick tool.";

        // Staged before SetActiveTool so OnActivated picks it up synchronously. Avoids a race where
        // _current is briefly null between SetActiveTool's internal callback dispatch and Begin's
        // post-activation assignment.
        private static PickHandler _pendingOnPick;

        private static PlanetSurfacePickTool _current;
        private GUIContent _toolbarIcon;
        private PickHandler _onPick;

        /// <summary>The currently-active instance, or null if the tool is not active.</summary>
        public static PlanetSurfacePickTool Current => _current;

        /// <inheritdoc />
        public override GUIContent toolbarIcon =>
            _toolbarIcon ??= new GUIContent(EditorGUIUtility.IconContent("d_Grid.PickingTool").image, ToolName);

        /// <summary>
        /// Activates the tool and binds <paramref name="onPick" /> to the next successful pick.
        /// </summary>
        /// <remarks>
        /// Replaces any previously-bound callback. The tool deactivates after the callback fires, restoring the
        /// previously-active tool. Cancellation (Esc) clears the callback without firing.
        /// </remarks>
        /// <param name="onPick">Callback invoked with the picked (lat, lon).</param>
        public static void Begin(PickHandler onPick)
        {
            _pendingOnPick = onPick;
            ToolManager.SetActiveTool<PlanetSurfacePickTool>();
        }

        /// <inheritdoc />
        public override void OnActivated()
        {
            _current = this;
            _onPick = _pendingOnPick;
            _pendingOnPick = null;
            PlanetPreviewState.ActiveChanged += OnSessionChanged;
            SceneView.duringSceneGui += OnDuringSceneGui;
            // Focus the SceneView so Esc reaches our key handler instead of the originating window.
            SceneView.lastActiveSceneView?.Focus();
        }

        /// <inheritdoc />
        public override void OnWillBeDeactivated()
        {
            SceneView.duringSceneGui -= OnDuringSceneGui;
            PlanetPreviewState.ActiveChanged -= OnSessionChanged;
            if (_current == this)
            {
                _current = null;
                _onPick = null;
            }
        }

        private static void OnSessionChanged()
        {
            if (PlanetAuthoringSession.Active == null && ToolManager.activeToolType == typeof(PlanetSurfacePickTool))
            {
                ToolManager.RestorePreviousPersistentTool();
            }
        }

        private static void OnDuringSceneGui(SceneView sceneView)
        {
            // Belt-and-braces Esc handler. OnToolGUI only gets KeyDown when SceneView is focused.
            var ev = Event.current;
            if (ev.type != EventType.KeyDown || ev.keyCode != KeyCode.Escape) return;
            if (ToolManager.activeToolType != typeof(PlanetSurfacePickTool)) return;
            ev.Use();
            if (_current != null)
            {
                _current._onPick = null;
            }
            ToolManager.RestorePreviousPersistentTool();
        }

        /// <inheritdoc />
        public override void OnToolGUI(EditorWindow window)
        {
            if (window is not SceneView sceneView) return;

            sceneView.wantsMouseMove = true;
            var controlId = GUIUtility.GetControlID(FocusType.Passive);

            var ev = Event.current;

            if (ev.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlId);
                return;
            }

            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.Escape)
            {
                ev.Use();
                _onPick = null;
                ToolManager.RestorePreviousPersistentTool();
                return;
            }

            var planet = PlanetAuthoringSession.Active?.Pqs;
            if (planet == null)
            {
                if (ev.type == EventType.Repaint)
                {
                    PlanetEditorHud.Draw(sceneView, NoSessionHint, null);
                }
                return;
            }

            var ray = HandleUtility.GUIPointToWorldRay(ev.mousePosition);
            var hovering = PlanetSurfaceHit.TryHit(planet, ray, out var hitWorld, out var hitLatLon, out var hitAlt);

            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                if (hovering)
                {
                    var callback = _onPick;
                    _onPick = null;
                    ev.Use();
                    callback?.Invoke(hitLatLon);
                    ToolManager.RestorePreviousPersistentTool();
                }
                return;
            }

            if (ev.type == EventType.MouseMove)
            {
                sceneView.Repaint();
                return;
            }

            if (ev.type == EventType.Repaint)
            {
                if (hovering)
                {
                    var surfaceUp = (hitWorld - planet.transform.position).normalized;
                    var discSize = HandleUtility.GetHandleSize(hitWorld) * 0.1f;
                    using (new Handles.DrawingScope(Color.yellow))
                    {
                        Handles.DrawWireDisc(hitWorld, surfaceUp, discSize);
                    }
                    PlanetEditorHud.Draw(sceneView, HudHint, $"Lat {hitLatLon.x:0.000}°  Lon {hitLatLon.y:0.000}°  Alt {hitAlt:0} m");
                }
                else
                {
                    PlanetEditorHud.Draw(sceneView, HudHint, null);
                }
            }
        }
    }
}
