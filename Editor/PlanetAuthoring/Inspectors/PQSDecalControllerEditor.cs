using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Custom inspector for <see cref="PQSDecalController" /> in the planet authoring style.
    /// Surfaces the body's shared decal textures and a manual Bake action.
    /// </summary>
    [CustomEditor(typeof(PQSDecalController))]
    public class PQSDecalControllerEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "/Assets/Windows/PQSDecalControllerInspector.uxml";
        private const string UssPath = "/Assets/Windows/PQSDecalControllerInspector.uss";

        private Button _bakeButton;

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load PQSDecalControllerInspector.uxml"));
                return root;
            }
            tree.CloneTree(root);

            Ksp2UnityToolsStyles.Apply(root, UssPath);

            _bakeButton = root.Q<Button>("bake-button");
            if (_bakeButton != null)
                _bakeButton.clicked += OnBakeClicked;

            root.Bind(serializedObject);
            // Bake button only meaningful when there are decals (or stale baked data to clear).
            // Poll cheaply rather than hook every list mutation site.
            RefreshBakeButton();
            root.schedule.Execute(RefreshBakeButton).Every(500);
            return root;
        }

        private void RefreshBakeButton()
        {
            if (_bakeButton == null) return;
            var controller = target as PQSDecalController;
            if (controller == null)
            {
                _bakeButton.SetEnabled(false);
                return;
            }
            var liveInstances = CountLiveInstances(controller);
            var bakedCount = controller.PqsDecalData != null
                ? (controller.PqsDecalData.BakedPqsDecalIDList?.Count ?? 0)
                : 0;
            var clean = liveInstances == 0 && bakedCount == 0;
            _bakeButton.SetEnabled(!clean);
            _bakeButton.tooltip = clean
                ? "No decals on this body and nothing baked. Add a PQSDecalInstance child to enable baking."
                : "Rebuild PQSDecalData from the controller's instance list and shared textures. Required after editing shared textures or adding/removing instances outside an active session.";
        }

        private static int CountLiveInstances(PQSDecalController controller)
        {
            if (controller.PqsDecalInstanceList == null) return 0;
            var count = 0;
            foreach (var inst in controller.PqsDecalInstanceList)
            {
                if (inst != null && inst.PQSDecal != null)
                {
                    count++;
                }
            }
            return count;
        }

        private void OnBakeClicked()
        {
            var controller = (PQSDecalController)target;
            // Route through QueueRebuild so a manual click coalesces with any auto-bake already
            // queued from an asset change or instance edit in the same tick.
            DecalBaker.QueueRebuild(controller);
            RefreshBakeButton();
        }
    }
}
