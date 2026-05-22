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
        private const string PREF_KEY_SHOW_HEATSHIELD_SHIELDING_DIRECTION = "PartAuthoring.Gizmos.ShowHeatshieldShieldingDirection";
        private const string PREF_KEY_SHOW_WHEEL_BOGEY_AXIS = "PartAuthoring.Gizmos.ShowWheelBogeyAxis";
        private const string PREF_KEY_SHOW_WHEEL_BOGEY_UP_AXIS = "PartAuthoring.Gizmos.ShowWheelBogeyUpAxis";

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

        /// <summary>
        /// Gets or sets a value indicating whether the heatshield shielding-direction arrow is drawn in the SceneView.
        /// </summary>
        public static bool ShowHeatshieldShieldingDirection
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_HEATSHIELD_SHIELDING_DIRECTION, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_HEATSHIELD_SHIELDING_DIRECTION, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the wheel-bogey rotation-axis arrow is drawn in the SceneView.
        /// </summary>
        public static bool ShowWheelBogeyAxis
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_WHEEL_BOGEY_AXIS, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_WHEEL_BOGEY_AXIS, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the wheel-bogey up-axis arrow is drawn in the SceneView.
        /// </summary>
        public static bool ShowWheelBogeyUpAxis
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_WHEEL_BOGEY_UP_AXIS, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_WHEEL_BOGEY_UP_AXIS, value);
        }
    }
}
