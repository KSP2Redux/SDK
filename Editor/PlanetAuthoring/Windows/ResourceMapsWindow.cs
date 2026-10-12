using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Authoring window for a celestial body's resource density maps.
    /// </summary>
    /// <remarks>
    /// The editor itself is <see cref="ResourceMapsView" />, which the planet inspector's Science tab also hosts.
    /// </remarks>
    public class ResourceMapsWindow : EditorWindow
    {
        private const string TITLE = "Resource Maps";

        private ResourceMapsView _view;

        /// <summary>
        /// Opens or focuses the Resource Maps window.
        /// </summary>
        [MenuItem(PlanetAuthoringWindows.MenuRoot + TITLE, priority = PlanetAuthoringWindows.PriorityResourceMaps)]
        public static void ShowWindow()
        {
            var window = GetWindow<ResourceMapsWindow>();
            window.titleContent = new GUIContent(TITLE);
            window.minSize = new Vector2(420f, 480f);
        }

        private void CreateGUI()
        {
            _view = new ResourceMapsView(true);
            rootVisualElement.Add(_view.Root);
        }

        private void OnDestroy() => _view?.Dispose();
    }
}
