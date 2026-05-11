using System.IO;
using System.Text;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Bulk decal authoring for the active planet preview session: create new templates, list
    /// existing instances, jump-select them.
    /// </summary>
    /// <remarks>
    /// Gated on a live <see cref="PlanetAuthoringSession" />. With no session, the window shows a
    /// hint and disables creation. With a session, the "Place new" button creates a fresh PQSDecal
    /// asset alongside the body's authoring scene and activates <see cref="PlaceDecalTool" /> with
    /// it. The list refreshes on a 500ms tick but only rebuilds when the underlying instance set
    /// actually changes.
    /// </remarks>
    public class DecalManagerWindow : EditorWindow
    {
        private Label _statusLabel;
        private Button _placeNewButton;
        private Button _bakeButton;
        private ScrollView _list;
        private string _lastListFingerprint;

        /// <summary>Opens the Decal Manager window.</summary>
        [MenuItem(PlanetAuthoringWindows.MenuRoot + "Decal Manager", priority = PlanetAuthoringWindows.PriorityDecalManager)]
        public static void ShowWindow()
        {
            var window = GetWindow<DecalManagerWindow>();
            window.titleContent = new GUIContent("Decal Manager");
            window.minSize = new Vector2(280f, 240f);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _statusLabel = new Label { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 6f } };
            root.Add(_statusLabel);

            var actionRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 6f } };
            _placeNewButton = new Button(OnPlaceNewClicked) { text = "+ Place new decal", style = { flexGrow = 1f, height = 22f, marginRight = 4f } };
            actionRow.Add(_placeNewButton);
            _bakeButton = new Button(OnBakeClicked) { text = "Bake", style = { width = 80f, height = 22f } };
            actionRow.Add(_bakeButton);
            root.Add(actionRow);

            _list = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1f } };
            root.Add(_list);

            root.schedule.Execute(Refresh).Every(500);
            Refresh();
        }

        private void Refresh()
        {
            if (_statusLabel == null) return;
            var session = PlanetAuthoringSession.Active;
            var active = session != null && session.IsAlive;

            if (!active)
            {
                if (_lastListFingerprint != "no-session")
                {
                    _statusLabel.text = "No active preview. Enable a planet preview to manage its decals.";
                    _placeNewButton.SetEnabled(false);
                    _bakeButton.SetEnabled(false);
                    _list.Clear();
                    _lastListFingerprint = "no-session";
                }
                return;
            }

            var bodyName = session.Body != null ? session.Body.name : "(unknown)";
            _statusLabel.text = $"Decals on {bodyName}";
            _placeNewButton.SetEnabled(true);
            _bakeButton.SetEnabled(true);
            RebuildListIfChanged(session);
        }

        private void RebuildListIfChanged(PlanetAuthoringSession session)
        {
            var controller = DecalControllerHelper.Resolve(session.Pqs);
            if (controller == null)
            {
                if (_lastListFingerprint != "no-controller")
                {
                    _list.Clear();
                    _list.Add(new Label("No PQSDecalController in this body's hierarchy.") { style = { whiteSpace = WhiteSpace.Normal } });
                    _lastListFingerprint = "no-controller";
                }
                return;
            }

            var instances = controller.PqsDecalInstanceList;
            var fingerprint = ComputeFingerprint(instances);
            if (fingerprint == _lastListFingerprint) return;
            _lastListFingerprint = fingerprint;

            _list.Clear();
            if (instances == null || instances.Count == 0)
            {
                _list.Add(new Label("No decals yet. Click '+ Place new decal' to add one.") { style = { whiteSpace = WhiteSpace.Normal } });
                return;
            }
            foreach (var inst in instances)
            {
                if (inst == null) continue;
                _list.Add(BuildRow(inst));
            }
        }

        private static string ComputeFingerprint(System.Collections.Generic.List<PQSDecalInstance> instances)
        {
            if (instances == null) return "null";
            var sb = new StringBuilder();
            foreach (var inst in instances)
            {
                if (inst == null)
                {
                    sb.Append('|');
                    continue;
                }
                sb.Append(inst.gameObject.name).Append(',').Append(inst.LatLong.x.ToString("0.000")).Append(',').Append(inst.LatLong.y.ToString("0.000")).Append('|');
            }
            return sb.ToString();
        }

        private static VisualElement BuildRow(PQSDecalInstance inst)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 2f,
                    paddingBottom = 2f,
                },
            };
            var label = new Label($"{inst.gameObject.name}   {inst.LatLong.x:0.00}°, {inst.LatLong.y:0.00}°") { style = { flexGrow = 1f } };
            row.Add(label);
            row.Add(new Button(() =>
            {
                Selection.activeGameObject = inst.gameObject;
                EditorGUIUtility.PingObject(inst.gameObject);
            })
            { text = "Select", style = { width = 60f } });
            return row;
        }

        private void OnPlaceNewClicked()
        {
            var session = PlanetAuthoringSession.Active;
            if (session?.Body == null) return;

            var folder = ResolveBodyFolder(session.Body);
            var defaultName = (session.Body.name ?? "Body") + "_Decal";
            NewDecalPromptWindow.Show(defaultName, result =>
            {
                var template = result.ExistingTemplate != null
                    ? result.ExistingTemplate
                    : CreatePqsDecalAsset.CreateConfigured(folder, result);
                if (template == null) return;
                PlaceDecalTool.Begin(template);
            });
        }

        private void OnBakeClicked()
        {
            var session = PlanetAuthoringSession.Active;
            var controller = DecalControllerHelper.Resolve(session?.Pqs);
            if (controller == null) return;
            // Route through QueueRebuild to coalesce with any auto-bake already queued in this tick.
            DecalBaker.QueueRebuild(controller);
        }

        private static string ResolveBodyFolder(KSP.CoreCelestialBodyData body)
        {
            var scenePath = body.gameObject.scene.path;
            if (!string.IsNullOrEmpty(scenePath))
            {
                var dir = Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir) && AssetDatabase.IsValidFolder(dir)) return dir;
            }
            return "Assets";
        }
    }
}
