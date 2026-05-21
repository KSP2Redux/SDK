using KSP;
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
    }
}
