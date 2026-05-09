using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    public class PreviewControlsWindow : EditorWindow
    {
        private const string MenuRoot = "Window/Redux Planet Authoring/";

        [MenuItem(MenuRoot + "Preview Controls", priority = 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<PreviewControlsWindow>();
            window.titleContent = new GUIContent("Planet Preview");
            window.minSize = new Vector2(280f, 160f);
        }

        [MenuItem(MenuRoot + "Validation Report", priority = 110)]
        public static void ShowValidationReportPlaceholder()
        {
            EditorUtility.DisplayDialog(
                "Redux Planet Authoring",
                "Validation Report window is not yet implemented.",
                "OK"
            );
        }

        [MenuItem(MenuRoot + "Environment", priority = 120)]
        public static void ShowEnvironmentPlaceholder()
        {
            EditorUtility.DisplayDialog(
                "Redux Planet Authoring",
                "Environment window is not yet implemented.",
                "OK"
            );
        }

        [MenuItem(MenuRoot + "Biome Painter", priority = 130)]
        public static void ShowBiomePainterPlaceholder()
        {
            EditorUtility.DisplayDialog(
                "Redux Planet Authoring",
                "Biome Painter is not yet implemented.",
                "OK"
            );
        }

        [MenuItem(MenuRoot + "Decal Manager", priority = 140)]
        public static void ShowDecalManagerPlaceholder()
        {
            EditorUtility.DisplayDialog(
                "Redux Planet Authoring",
                "Decal Manager is not yet implemented.",
                "OK"
            );
        }

        [MenuItem(MenuRoot + "Discoverable Manager", priority = 150)]
        public static void ShowDiscoverableManagerPlaceholder()
        {
            EditorUtility.DisplayDialog(
                "Redux Planet Authoring",
                "Discoverable Manager is not yet implemented.",
                "OK"
            );
        }

        [MenuItem(MenuRoot + "Preset Browser", priority = 160)]
        public static void ShowPresetBrowserPlaceholder()
        {
            EditorUtility.DisplayDialog(
                "Redux Planet Authoring",
                "Preset Browser is not yet implemented.",
                "OK"
            );
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            root.Add(new Label("Redux Planet Authoring") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4f } });
            root.Add(new Label("Scaffolding only; preview is not yet wired up.") { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 8f } });

            var enableButton = new Button { text = "Enable Preview" };
            enableButton.SetEnabled(false);
            enableButton.tooltip = "Available once PlanetAuthoringSession is wired up.";
            root.Add(enableButton);
        }
    }
}
