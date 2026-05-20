using KSP.Rendering.Planets;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Persistent SceneView EditorTool that places new <see cref="PQSDecalInstance" /> children on
    /// the active planet at each left-click using a static <see cref="Template" /> reference.
    /// </summary>
    /// <remarks>
    /// Activated via <see cref="Begin" /> from a "Place instance" button on a PQSDecal asset.
    /// One-shot. After a successful left-click placement the previous persistent tool is restored
    /// and the new decal is selected. Esc or right-click cancels without placing.
    /// </remarks>
    [EditorTool("Place Decal")]
    public sealed class PlaceDecalTool : EditorTool
    {
        /// <summary>The decal template assigned to instances placed by this tool.</summary>
        public static PQSDecal Template { get; set; }

        private static PlaceDecalTool _current;
        private GUIContent _toolbarIcon;

        /// <summary>The currently-active instance, or null if the tool is not active.</summary>
        public static PlaceDecalTool Current => _current;

        /// <inheritdoc />
        public override GUIContent toolbarIcon =>
            _toolbarIcon ??= new GUIContent(EditorGUIUtility.IconContent("d_RawImage Icon").image, "Place Decal");

        /// <summary>Activates the tool with <paramref name="template" /> as the placement template.</summary>
        /// <param name="template">The PQSDecal asset to instance at each click.</param>
        public static void Begin(PQSDecal template)
        {
            Template = template;
            ToolManager.SetActiveTool<PlaceDecalTool>();
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

            var controller = DecalControllerHelper.Resolve(planet);
            if (controller == null)
            {
                if (ev.type == EventType.Repaint)
                {
                    PlanetEditorHud.Draw(sceneView, "No PQSDecalController found in the planet hierarchy.", null);
                }
                return;
            }

            if (Template == null)
            {
                if (ev.type == EventType.Repaint)
                {
                    PlanetEditorHud.Draw(sceneView, "No decal template selected. Use 'Place instance' on a PQSDecal asset.", null);
                }
                return;
            }

            var ray = HandleUtility.GUIPointToWorldRay(ev.mousePosition);
            // Sample bare terrain (no decals): we're placing a decal, and stacking it on top of
            // existing decal displacement would compound the raise each pass.
            var hovering = PlanetSurfaceHit.TryHit(planet, ray, out var hitWorld, out var hitLatLon, out var hitAlt, includeDecals: false);

            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                if (hovering)
                {
                    CreateDecalAt(controller, hitLatLon);
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

        private static void CreateDecalAt(PQSDecalController controller, Vector2 latLon)
        {
            var go = new GameObject($"Decal_{Template.name}");
            go.transform.SetParent(controller.transform, worldPositionStays: false);
            // Place the new decal in the body's scene so multi-scene editing doesn't leak it into
            // whatever Unity happens to consider active. Parenting alone is insufficient because
            // GameObject() always lands in the active scene first.
            var bodyScene = controller.gameObject.scene;
            if (bodyScene.IsValid() && go.scene != bodyScene)
            {
                SceneManager.MoveGameObjectToScene(go, bodyScene);
            }
            Undo.RegisterCreatedObjectUndo(go, "Place Decal");
            var instance = go.AddComponent<PQSDecalInstance>();
            instance.PQSDecal = Template;
            instance.PqsDecalController = controller;
            instance.LatLong = latLon;
            instance.UpdateDecalTransform();
            // PQSDecalInstance.OnEnable adds itself to the controller's list but does not rebuild the NativeArray the PQS subdivision job reads. Without this the next OnPreCull NREs.
            controller.RefreshDecalInstances();
            EditorUtility.SetDirty(instance);
            // Queue the bake so back-to-back placements coalesce into one rebuild.
            DecalBaker.QueueRebuild(controller);
            Selection.activeGameObject = go;
        }
    }
}
