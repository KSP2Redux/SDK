using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Gizmos
{
    /// <summary>
    /// EditorPrefs-backed toggles for the part-authoring SceneView gizmos.
    /// </summary>
    /// <remarks>
    /// State is stored in <see cref="EditorPrefs" /> so the toggles persist across editor
    /// sessions. Every surface (IMGUI inspector, UI Toolkit gizmo settings panel, future
    /// SceneView overlay) reads and writes the same keys here, so toggle state stays consistent.
    /// </remarks>
    public static class PartAuthoringGizmoSettings
    {
        private const string PREF_KEY_SHOW_CENTER_OF_MASS = "PartAuthoring.Gizmos.ShowCenterOfMass";
        private const string PREF_KEY_SHOW_CENTER_OF_LIFT = "PartAuthoring.Gizmos.ShowCenterOfLift";
        private const string PREF_KEY_SHOW_ATTACH_NODES = "PartAuthoring.Gizmos.ShowAttachNodes";

        /// <summary>
        /// Gets or sets a value indicating whether the centre-of-mass icon is drawn in the SceneView.
        /// </summary>
        public static bool ShowCenterOfMass
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_CENTER_OF_MASS, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_CENTER_OF_MASS, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the centre-of-lift icon is drawn in the SceneView.
        /// </summary>
        public static bool ShowCenterOfLift
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_CENTER_OF_LIFT, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_CENTER_OF_LIFT, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether attach-node markers are drawn in the SceneView.
        /// </summary>
        public static bool ShowAttachNodes
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_ATTACH_NODES, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_ATTACH_NODES, value);
        }
    }
}
