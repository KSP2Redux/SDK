using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Windows;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Persistent SceneView EditorTool that places new <see cref="SurfaceLandmark" /> children on
    /// the active planet at each left-click.
    /// </summary>
    /// <remarks>
    /// Activated via <see cref="Begin" /> with a settings payload staged from the
    /// <see cref="NewSurfaceLandmarkPromptWindow" />. One-shot per click. After a successful
    /// placement the previous persistent tool is restored and the new landmark is selected. Esc or
    /// right-click cancels without placing.
    /// </remarks>
    [EditorTool("Place Surface Landmark")]
    public sealed class PlaceSurfaceLandmarkTool : EditorTool
    {
        /// <summary>Staged settings applied to the next landmark dropped by this tool.</summary>
        public static NewSurfaceLandmarkPromptWindow.Result StagedSettings { get; set; }

        private static PlaceSurfaceLandmarkTool _current;
        private GUIContent _toolbarIcon;

        /// <summary>The currently-active instance, or null if the tool is not active.</summary>
        public static PlaceSurfaceLandmarkTool Current => _current;

        /// <inheritdoc />
        public override GUIContent toolbarIcon =>
            _toolbarIcon ??= new GUIContent(EditorGUIUtility.IconContent("d_HoloLensInputModule Icon").image, "Place Surface Landmark");

        /// <summary>Activates the tool with <paramref name="settings" /> staged for the next placement.</summary>
        /// <param name="settings">The form payload from the prompt window. New landmarks adopt these values.</param>
        public static void Begin(NewSurfaceLandmarkPromptWindow.Result settings)
        {
            StagedSettings = settings;
            ToolManager.SetActiveTool<PlaceSurfaceLandmarkTool>();
        }

        /// <inheritdoc />
        public override void OnActivated()
        {
            _current = this;
            SceneViewFocus.FocusNextFrame();
        }

        /// <inheritdoc />
        public override void OnWillBeDeactivated()
        {
            // Drop the staged settings so a later re-activation without Begin doesn't silently
            // reuse the previous payload.
            StagedSettings = null;
            if (_current == this)
            {
                _current = null;
            }
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
                ToolManager.RestorePreviousTool();
                return;
            }
            if (ev.type == EventType.MouseDown && ev.button == 1)
            {
                ev.Use();
                ToolManager.RestorePreviousTool();
                return;
            }

            var session = PlanetAuthoringSession.Active;
            var planet = session?.Pqs;
            if (planet == null)
            {
                if (ev.type == EventType.Repaint)
                {
                    PlanetEditorHud.Draw(sceneView, "No planet preview is active. Enable preview first.", null);
                }
                return;
            }

            var ray = HandleUtility.GUIPointToWorldRay(ev.mousePosition);
            var hovering = PlanetSurfaceHit.TryHit(planet, ray, out var hitWorld, out var hitLatLon, out var hitAlt);

            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                if (hovering)
                {
                    CreateLandmarkAt(planet, hitLatLon);
                    ev.Use();
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
                var hint = "Click to place a Surface Landmark. Esc or right-click to cancel.";
                if (hovering)
                {
                    var surfaceUp = (hitWorld - planet.transform.position).normalized;
                    var discSize = HandleUtility.GetHandleSize(planet.transform.position) * 0.04f;
                    using (new Handles.DrawingScope(Color.cyan))
                    {
                        Handles.DrawWireDisc(hitWorld, surfaceUp, discSize);
                    }
                    PlanetEditorHud.Draw(sceneView, hint, $"Lat {hitLatLon.x:0.000}°  Lon {hitLatLon.y:0.000}°  Alt {hitAlt:0} m");
                }
                else
                {
                    PlanetEditorHud.Draw(sceneView, hint, null);
                }
            }
        }

        private static void CreateLandmarkAt(PQS planet, Vector2 hitLatLon)
        {
            var settings = StagedSettings ?? new NewSurfaceLandmarkPromptWindow.Result();

            var go = new GameObject("SurfaceLandmark");
            go.transform.SetParent(planet.transform, worldPositionStays: false);
            var bodyScene = planet.gameObject.scene;
            if (bodyScene.IsValid() && go.scene != bodyScene)
            {
                SceneManager.MoveGameObjectToScene(go, bodyScene);
            }
            Undo.RegisterCreatedObjectUndo(go, "Place Surface Landmark");
            var landmark = go.AddComponent<SurfaceLandmark>();
            landmark.Latitude = hitLatLon.x;
            landmark.Longitude = hitLatLon.y;
            landmark.Altitude = 0.0;
            landmark.SmoothingRadius = settings.SmoothingRadius;
            landmark.SmoothingFadeStrength = settings.SmoothingFadeStrength;
            landmark.DiscoverableRadius = settings.DiscoverableRadius;
            landmark.EnableDecal = settings.EnableDecal;
            landmark.EnableSmoothing = settings.EnableSmoothing;
            landmark.SmoothingDecal = settings.SmoothingDecal;
            landmark.EnablePrefab = settings.EnablePrefab;
            landmark.EnableDiscoverable = settings.EnableDiscoverable;
            landmark.Prefab = settings.Prefab;
            landmark.PrefabAddressableKey = settings.PrefabAddressableKey;
            landmark.UseRawAddressableKey = settings.UseRawAddressableKey;
            landmark.PrefabWidth = settings.PrefabWidth;
            EditorUtility.SetDirty(landmark);
            // Sync explicitly so the managed children and wrapper transform exist before the artist
            // starts editing. The runtime SurfaceLandmark has no OnValidate hook, so this is the
            // only trigger.
            SurfaceLandmarkSync.Sync(landmark);
            Selection.activeGameObject = go;
        }
    }
}
