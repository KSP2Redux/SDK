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
        private const string PREF_KEY_SHOW_VIRTUAL_ATTACHED_PARTS = "PartAuthoring.Gizmos.ShowVirtualAttachedParts";
        private const string PREF_KEY_VIRTUAL_PART_LENGTH = "PartAuthoring.Gizmos.VirtualPartLength";
        private const string PREF_KEY_SHOW_ENGINE_THRUST = "PartAuthoring.Gizmos.ShowEngineThrustTransforms";
        private const string PREF_KEY_SHOW_GIMBAL_CONE = "PartAuthoring.Gizmos.ShowGimbalCone";
        private const string PREF_KEY_SHOW_RCS_THRUSTERS = "PartAuthoring.Gizmos.ShowRCSThrusters";
        private const string PREF_KEY_SHOW_DRAG_CUBES = "PartAuthoring.Gizmos.ShowDragCubes";
        private const string PREF_KEY_SHOW_FAIRING_PREVIEW = "PartAuthoring.Gizmos.ShowFairingPreview";
        private const string PREF_KEY_SHOW_LIFT_ARROW = "PartAuthoring.Gizmos.ShowLiftSurfaceArrow";
        private const string PREF_KEY_SHOW_CONTROL_ARC = "PartAuthoring.Gizmos.ShowControlSurfaceArc";
        private const string PREF_KEY_SHOW_CARGO_VOLUME = "PartAuthoring.Gizmos.ShowCargoBayVolume";
        private const string PREF_KEY_SHOW_FAIRING_EJECTION = "PartAuthoring.Gizmos.ShowFairingEjection";
        private const string PREF_KEY_SHOW_DECOUPLE_SPLIT = "PartAuthoring.Gizmos.ShowDecoupleSplit";
        private const string PREF_KEY_SHOW_DOCKING_FRAME = "PartAuthoring.Gizmos.ShowDockingFrame";
        private const string PREF_KEY_SHOW_SOLAR_INCIDENCE = "PartAuthoring.Gizmos.ShowSolarPanelIncidence";
        private const string PREF_KEY_SHOW_INTAKE_DIRECTION = "PartAuthoring.Gizmos.ShowResourceIntakeDirection";
        private const string PREF_KEY_SHOW_RADIATOR_SURFACE = "PartAuthoring.Gizmos.ShowActiveRadiatorSurface";

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

        /// <summary>
        /// Gets or sets a value indicating whether the per-attach-node virtual-attached-part cylinders are drawn in the SceneView.
        /// </summary>
        public static bool ShowVirtualAttachedParts
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_VIRTUAL_ATTACHED_PARTS, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_VIRTUAL_ATTACHED_PARTS, value);
        }

        /// <summary>
        /// Gets or sets the length, in metres, of each virtual-attached-part cylinder.
        /// </summary>
        /// <remarks>
        /// Diameter is always derived from the node's sizeKey.
        /// </remarks>
        public static float VirtualPartLength
        {
            get => EditorPrefs.GetFloat(PREF_KEY_VIRTUAL_PART_LENGTH, 1.0f);
            set => EditorPrefs.SetFloat(PREF_KEY_VIRTUAL_PART_LENGTH, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether engine thrust-transform cones are drawn in the SceneView.
        /// </summary>
        public static bool ShowEngineThrustTransforms
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_ENGINE_THRUST, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_ENGINE_THRUST, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the gimbal range cone is drawn in the SceneView.
        /// </summary>
        public static bool ShowGimbalCone
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_GIMBAL_CONE, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_GIMBAL_CONE, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether RCS thruster arrows are drawn in the SceneView.
        /// </summary>
        public static bool ShowRCSThrusters
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_RCS_THRUSTERS, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_RCS_THRUSTERS, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether Module_Drag drag-cube faces are drawn in the SceneView.
        /// </summary>
        public static bool ShowDragCubes
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_DRAG_CUBES, false);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_DRAG_CUBES, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the Module_Fairing generated-shape preview is drawn in the SceneView.
        /// </summary>
        public static bool ShowFairingPreview
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_FAIRING_PREVIEW, false);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_FAIRING_PREVIEW, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the lift-direction arrow for a Module_LiftingSurface is drawn in the SceneView.
        /// </summary>
        public static bool ShowLiftSurfaceArrow
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_LIFT_ARROW, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_LIFT_ARROW, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the control-surface deflection arc is drawn in the SceneView.
        /// </summary>
        public static bool ShowControlSurfaceArc
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_CONTROL_ARC, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_CONTROL_ARC, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the cargo-bay look-up volume sphere is drawn in the SceneView.
        /// </summary>
        public static bool ShowCargoBayVolume
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_CARGO_VOLUME, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_CARGO_VOLUME, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the fairing ejection-direction arrow is drawn in the SceneView.
        /// </summary>
        public static bool ShowFairingEjection
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_FAIRING_EJECTION, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_FAIRING_EJECTION, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the decoupler split plane and ejection arrow are drawn in the SceneView.
        /// </summary>
        public static bool ShowDecoupleSplit
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_DECOUPLE_SPLIT, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_DECOUPLE_SPLIT, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the docking-node alignment frame is drawn in the SceneView.
        /// </summary>
        public static bool ShowDockingFrame
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_DOCKING_FRAME, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_DOCKING_FRAME, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the solar-panel incidence plane is drawn in the SceneView.
        /// </summary>
        public static bool ShowSolarPanelIncidence
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_SOLAR_INCIDENCE, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_SOLAR_INCIDENCE, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the resource-intake direction cone is drawn in the SceneView.
        /// </summary>
        public static bool ShowResourceIntakeDirection
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_INTAKE_DIRECTION, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_INTAKE_DIRECTION, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the active-radiator surface plane is drawn in the SceneView.
        /// </summary>
        public static bool ShowActiveRadiatorSurface
        {
            get => EditorPrefs.GetBool(PREF_KEY_SHOW_RADIATOR_SURFACE, true);
            set => EditorPrefs.SetBool(PREF_KEY_SHOW_RADIATOR_SURFACE, value);
        }
    }
}
