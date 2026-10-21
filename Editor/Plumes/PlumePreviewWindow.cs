using KSP.VFX;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace KSP.Editor
{
    // A dockable host keeps the shared preview controls accessible beside long inspectors.
    internal class PlumePreviewWindow : EditorWindow
    {
        [SerializeField]
        private ThrottleVFXManager _manager;

        internal static void Open(ThrottleVFXManager manager)
        {
            bool wasOpen = HasOpenInstances<PlumePreviewWindow>();
            var window = GetWindow<PlumePreviewWindow>();
            window.titleContent = new GUIContent("Plume Preview");
            window.minSize = new Vector2(350f, 480f);
            if (!wasOpen)
            {
                Rect main = EditorGUIUtility.GetMainWindowPosition();
                window.position = new Rect(main.x + 40f, main.y + 100f, 380f, 560f);
            }
            window._manager = manager;
            window.CreateGUI();
            window.Show();
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            if (_manager != null)
                rootVisualElement.Add(new Label(_manager.name) { tooltip = "Preview follows the selected engine." });
            var scroll = new ScrollView();
            scroll.Add(ThrottleVFXPreviewBridge.CreatePreviewControls(_manager, false));
            rootVisualElement.Add(scroll);
        }

        private void OnSelectionChange()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
                return;

            var manager = selected.GetComponentInParent<ThrottleVFXManager>(true);
            if (manager == null || manager == _manager)
                return;

            _manager = manager;
            CreateGUI();
        }
    }
}
