using System.Reflection;
using KSP;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.Definitions;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Gizmos
{
    /// <summary>
    /// SceneView gizmos for the part-authoring system.
    /// </summary>
    /// <remarks>
    /// Unity discovers these methods through the <see cref="DrawGizmo" /> attribute scan, so they
    /// fire regardless of which custom editor is resolved for <see cref="CorePartData" />.
    /// Toggles route through <see cref="PartAuthoringGizmoSettings" />.
    /// </remarks>
    public static class PartAuthoringGizmos
    {
        /// <summary>
        /// Draws centre-of-mass, centre-of-lift, and attach-node gizmos for the selected <see cref="CorePartData" />.
        /// </summary>
        /// <param name="data">The selected part.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawCorePartData(CorePartData data, GizmoType gizmoType)
        {
            Matrix4x4 localToWorldMatrix = data.transform.localToWorldMatrix;

            if (PartAuthoringGizmoSettings.ShowCenterOfMass)
            {
                Vector3 centerOfMassPosition = localToWorldMatrix.MultiplyPoint(data.Data.coMassOffset);
                UnityEngine.Gizmos.DrawIcon(
                    centerOfMassPosition,
                    SDKConfiguration.BasePath + "/Assets/Gizmos/com_icon.png",
                    false
                );
            }

            if (PartAuthoringGizmoSettings.ShowCenterOfLift)
            {
                Vector3 centerOfLiftPosition = localToWorldMatrix.MultiplyPoint(data.Data.coLiftOffset);
                UnityEngine.Gizmos.DrawIcon(
                    centerOfLiftPosition,
                    SDKConfiguration.BasePath + "/Assets/Gizmos/col_icon.png",
                    false
                );
            }

            if (!PartAuthoringGizmoSettings.ShowAttachNodes)
            {
                return;
            }

            UnityEngine.Gizmos.color = new Color(Color.green.r, Color.green.g, Color.green.b, 0.5f);
            foreach (AttachNodeDefinition attachNode in data.Data.attachNodes)
            {
                Vector3d pos = attachNode.position;
                pos = localToWorldMatrix.MultiplyPoint(pos);
                Vector3d dir = attachNode.orientation;
                dir = localToWorldMatrix.MultiplyVector(dir);
                UnityEngine.Gizmos.DrawRay(pos, dir * 0.25f);
                UnityEngine.Gizmos.DrawSphere(pos, 0.05f);
            }
        }

        /// <summary>
        /// Draws the attach-node marker for a selected <see cref="AttachmentNode" /> component.
        /// </summary>
        /// <param name="node">The selected attachment node.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawAttachmentNode(AttachmentNode node, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowAttachNodes)
            {
                return;
            }

            UnityEngine.Gizmos.color = new Color(Color.green.r, Color.green.g, Color.green.b, 0.5f);
            Vector3 pos = node.transform.position;
            UnityEngine.Gizmos.DrawRay(pos, node.transform.rotation * Vector3.forward * 0.25f);
            UnityEngine.Gizmos.DrawSphere(pos, 0.05f);
        }

        private static FieldInfo _heatshieldDataField;

        /// <summary>
        /// Draws the heatshield shielding-direction arrow for a selected <see cref="Module_Heatshield" />.
        /// </summary>
        /// <param name="module">The selected heatshield module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawHeatshieldShieldingDirection(Module_Heatshield module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowHeatshieldShieldingDirection || module == null)
            {
                return;
            }
            _heatshieldDataField ??= typeof(Module_Heatshield).GetField(
                "_dataHeatshield",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (!(_heatshieldDataField?.GetValue(module) is Data_Heatshield data))
            {
                return;
            }
            Transform partTransform = module.gameObject.transform;
            Vector3 worldDir = partTransform.TransformDirection(data.ShieldingDirection);
            if (worldDir.sqrMagnitude < 1e-6f)
            {
                return;
            }
            Vector3 origin = partTransform.position;
            Vector3 endpoint = origin + worldDir.normalized;
            UnityEngine.Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.9f);
            UnityEngine.Gizmos.DrawLine(origin, endpoint);
            UnityEngine.Gizmos.DrawSphere(endpoint, 0.05f);
        }

        private static FieldInfo _wheelBogeyDataField;

        /// <summary>
        /// Draws the wheel-bogey rotation-axis arrow for a selected <see cref="Module_WheelBogey" />.
        /// </summary>
        /// <param name="module">The selected wheel-bogey module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawWheelBogeyAxis(Module_WheelBogey module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowWheelBogeyAxis)
            {
                return;
            }
            if (!TryGetWheelBogeyData(module, out var data))
            {
                return;
            }
            DrawBogeyAxisArrow(module, data.bogeyAxis, new Color(0.95f, 0.3f, 0.3f, 0.9f));
        }

        /// <summary>
        /// Draws the wheel-bogey up-axis arrow for a selected <see cref="Module_WheelBogey" />.
        /// </summary>
        /// <param name="module">The selected wheel-bogey module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawWheelBogeyUpAxis(Module_WheelBogey module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowWheelBogeyUpAxis)
            {
                return;
            }
            if (!TryGetWheelBogeyData(module, out var data))
            {
                return;
            }
            DrawBogeyAxisArrow(module, data.bogeyUpAxis, new Color(0.3f, 0.85f, 0.4f, 0.9f));
        }

        private static bool TryGetWheelBogeyData(Module_WheelBogey module, out Data_WheelBogey data)
        {
            data = null;
            if (module == null)
            {
                return false;
            }
            _wheelBogeyDataField ??= typeof(Module_WheelBogey).GetField(
                "dataWheelBogey",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_wheelBogeyDataField?.GetValue(module) is Data_WheelBogey resolved)
            {
                data = resolved;
                return true;
            }
            return false;
        }

        private static void DrawBogeyAxisArrow(Module_WheelBogey module, Vector3 localAxis, Color color)
        {
            Transform partTransform = module.gameObject.transform;
            Vector3 worldDir = partTransform.TransformDirection(localAxis);
            if (worldDir.sqrMagnitude < 1e-6f)
            {
                return;
            }
            Vector3 origin = partTransform.position;
            Vector3 endpoint = origin + worldDir.normalized;
            UnityEngine.Gizmos.color = color;
            UnityEngine.Gizmos.DrawLine(origin, endpoint);
            UnityEngine.Gizmos.DrawSphere(endpoint, 0.05f);
        }
    }
}
