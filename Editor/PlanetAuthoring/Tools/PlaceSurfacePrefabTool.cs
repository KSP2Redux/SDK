using KSP.Rendering.Planets;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Persistent SceneView EditorTool that places new <see cref="PrefabSpawner" /> children on
    /// the active planet at each left-click using a static <see cref="Template" /> prefab.
    /// </summary>
    /// <remarks>
    /// Activated via <see cref="Begin" /> from the Surface Prefab Manager's "Place new" button.
    /// One-shot per click. After a successful left-click placement the previous persistent tool is
    /// restored and the new spawner is selected. Esc or right-click cancels without placing.
    /// </remarks>
    [EditorTool("Place Surface Prefab")]
    public sealed class PlaceSurfacePrefabTool : EditorTool
    {
        /// <summary>The prefab placed at each click.</summary>
        public static GameObject Template { get; set; }

        private static PlaceSurfacePrefabTool _current;
        private GUIContent _toolbarIcon;

        /// <summary>The currently-active instance, or null if the tool is not active.</summary>
        public static PlaceSurfacePrefabTool Current => _current;

        /// <inheritdoc />
        public override GUIContent toolbarIcon =>
            _toolbarIcon ??= new GUIContent(EditorGUIUtility.IconContent("d_Prefab Icon").image, "Place Surface Prefab");

        /// <summary>Activates the tool with <paramref name="template" /> as the placement prefab.</summary>
        /// <param name="template">The prefab to spawn at each click. Must be addressable.</param>
        public static void Begin(GameObject template)
        {
            Template = template;
            ToolManager.SetActiveTool<PlaceSurfacePrefabTool>();
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
            // Clear the static template so a later re-activation without Begin doesn't silently
            // reuse the previous template.
            Template = null;
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

            if (Template == null)
            {
                if (ev.type == EventType.Repaint)
                {
                    PlanetEditorHud.Draw(sceneView, "No prefab template selected. Use 'Place new' on the Surface Prefab Manager.", null);
                }
                return;
            }
            var key = AddressableKeyLookup.GetKey(Template);
            if (string.IsNullOrEmpty(key))
            {
                if (ev.type == EventType.Repaint)
                {
                    PlanetEditorHud.Draw(sceneView, $"'{Template.name}' is not addressable. Add it to an addressables group before placing.", null);
                }
                return;
            }

            var ray = HandleUtility.GUIPointToWorldRay(ev.mousePosition);
            var hovering = PlanetSurfaceHit.TryHit(planet, ray, out var hitWorld, out var hitLatLon, out var hitAlt);

            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                if (hovering)
                {
                    CreateSpawnerAt(planet, hitWorld, key);
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
                var hint = $"Click to place '{Template.name}'. Esc or right-click to cancel.";
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

        private static void CreateSpawnerAt(PQS planet, Vector3 hitWorld, string addressableKey)
        {
            var go = new GameObject($"SurfacePrefab_{Template.name}");
            go.transform.SetParent(planet.transform, worldPositionStays: false);
            // Place the new spawner in the body's scene so multi-scene editing doesn't leak it
            // into whatever Unity happens to consider active.
            var bodyScene = planet.gameObject.scene;
            if (bodyScene.IsValid() && go.scene != bodyScene)
            {
                SceneManager.MoveGameObjectToScene(go, bodyScene);
            }
            go.transform.position = hitWorld;
            // Local-up matches surface normal so the spawned prefab plants flat on the terrain.
            var surfaceUp = (hitWorld - planet.transform.position).normalized;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, surfaceUp);
            Undo.RegisterCreatedObjectUndo(go, "Place Surface Prefab");
            var spawner = go.AddComponent<PrefabSpawner>();
            spawner.prefabName = addressableKey;
            EditorUtility.SetDirty(spawner);
            Selection.activeGameObject = go;
        }
    }
}
