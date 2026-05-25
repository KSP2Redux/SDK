using System.Reflection;
using KSP;
using KSP.Modules;
using KSP.OAB;
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

            foreach (AttachNodeDefinition attachNode in data.Data.attachNodes)
            {
                Vector3 posWorld = localToWorldMatrix.MultiplyPoint(attachNode.position);
                Vector3 dirWorld = localToWorldMatrix.MultiplyVector(attachNode.orientation);
                if (dirWorld.sqrMagnitude < 1e-6f)
                {
                    continue;
                }
                dirWorld.Normalize();

                // visualSize is the in-game orb display only. Physical mating size comes from sizeKey.
                float diameter = PartSizeRegistry.GetAttachNodeDiameter(attachNode);
                float radius = diameter * 0.5f;
                float arrowLen = Mathf.Max(radius * 0.3f, 0.05f);

                Color discColor = ColorForNodeType(attachNode.nodeType);
                Handles.color = discColor;
                Handles.DrawWireDisc(posWorld, dirWorld, radius);
                Handles.DrawLine(posWorld, posWorld + dirWorld * arrowLen);
                UnityEngine.Gizmos.color = discColor;
                UnityEngine.Gizmos.DrawSphere(posWorld, 0.05f);

                string sizeKey = PartSizeRegistry.GetAttachNodeSizeKey(attachNode);
                Handles.Label(posWorld + dirWorld * arrowLen * 1.4f, $"{attachNode.nodeID}  {sizeKey}");

                if (PartAuthoringGizmoSettings.ShowVirtualAttachedParts)
                {
                    DrawVirtualAttachedPart(data.transform, posWorld, dirWorld, radius, attachNode.nodeType);
                }
            }
        }

        private static readonly Color VIRTUAL_PART_COLOR = new(160f / 255f, 120f / 255f, 240f / 255f, 0.95f);

        private static void DrawVirtualAttachedPart(Transform partRoot, Vector3 nodePos, Vector3 nodeDir, float radius, AttachNodeType nodeType)
        {
            float length = Mathf.Max(PartAuthoringGizmoSettings.VirtualPartLength, 0.01f);
            Vector3 axis;
            Vector3 centerOffset;
            if (nodeType == AttachNodeType.Surface)
            {
                // Axis perpendicular to nodeDir, wall tangent to the node disc, body outside the part.
                Vector3 candidate = Vector3.ProjectOnPlane(partRoot.up, nodeDir);
                if (candidate.sqrMagnitude < 0.01f)
                {
                    candidate = Vector3.ProjectOnPlane(partRoot.forward, nodeDir);
                }
                axis = candidate.normalized;
                centerOffset = nodeDir * radius;
            }
            else
            {
                axis = nodeDir;
                centerOffset = nodeDir * (length * 0.5f);
            }

            Vector3 center = nodePos + centerOffset;
            Vector3 endA = center - axis * (length * 0.5f);
            Vector3 endB = center + axis * (length * 0.5f);

            Handles.color = VIRTUAL_PART_COLOR;
            Handles.DrawWireDisc(endA, axis, radius);
            Handles.DrawWireDisc(endB, axis, radius);

            // Equator connectors: four lines at 0/90/180/270 degrees around the cylinder.
            Vector3 ringX = Vector3.Cross(axis, Vector3.up);
            if (ringX.sqrMagnitude < 0.01f)
            {
                ringX = Vector3.Cross(axis, Vector3.right);
            }
            ringX.Normalize();
            Vector3 ringY = Vector3.Cross(axis, ringX).normalized;
            Handles.DrawLine(endA + ringX * radius, endB + ringX * radius);
            Handles.DrawLine(endA - ringX * radius, endB - ringX * radius);
            Handles.DrawLine(endA + ringY * radius, endB + ringY * radius);
            Handles.DrawLine(endA - ringY * radius, endB - ringY * radius);
        }

        private static Color ColorForNodeType(AttachNodeType nodeType)
        {
            return nodeType switch
            {
                AttachNodeType.Stack => new Color(0.40f, 0.80f, 0.95f, 0.95f),
                AttachNodeType.Surface => new Color(0.95f, 0.40f, 0.80f, 0.95f),
                AttachNodeType.Dock => new Color(0.95f, 0.85f, 0.40f, 0.95f),
                _ => Color.white,
            };
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

        private static readonly Color ENGINE_THRUST_COLOR = new(255f / 255f, 140f / 255f, 60f / 255f, 0.95f);
        private static readonly Color GIMBAL_CONE_COLOR = new(255f / 255f, 200f / 255f, 60f / 255f, 0.95f);
        private static readonly Color RCS_THRUSTER_COLOR = new(255f / 255f, 140f / 255f, 60f / 255f, 0.95f);

        private static FieldInfo _engineDataField;
        private static FieldInfo _gimbalDataField;

        /// <summary>
        /// Draws the thrust-transform cones for a selected <see cref="Module_Engine" />. Iterates every engine mode and renders each unique <c>ThrustTransformName</c> once. Modes without explicit ThrustTransformNamesMultipliers fall back to the legacy <c>thrustVectorTransformName</c>.
        /// </summary>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawEngineThrustTransforms(Module_Engine module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowEngineThrustTransforms || module == null)
            {
                return;
            }
            _engineDataField ??= typeof(Module_Engine).GetField(
                "dataEngine",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_engineDataField?.GetValue(module) is not Data_Engine data || data.engineModes == null)
            {
                return;
            }

            var seenNames = new System.Collections.Generic.HashSet<string>();
            foreach (var mode in data.engineModes)
            {
                if (mode == null) continue;
                if (mode.ThrustTransformNamesMultipliers != null && mode.ThrustTransformNamesMultipliers.Length > 0)
                {
                    foreach (var group in mode.ThrustTransformNamesMultipliers)
                    {
                        if (group == null || string.IsNullOrEmpty(group.ThrustTransformName)) continue;
                        if (!seenNames.Add(group.ThrustTransformName)) continue;
                        DrawEngineThrustGroup(module.gameObject.transform, group.ThrustTransformName, group.ThrustTransformMultiplier);
                    }
                }
                else if (!string.IsNullOrEmpty(mode.thrustVectorTransformName)
                    && seenNames.Add(mode.thrustVectorTransformName))
                {
                    DrawEngineThrustGroup(module.gameObject.transform, mode.thrustVectorTransformName, 1f);
                }
            }
        }

        private static void DrawEngineThrustGroup(Transform partRoot, string name, float multiplier)
        {
            foreach (Transform t in FindTransformsByName(partRoot, name))
            {
                Vector3 origin = t.position;
                Vector3 forward = t.forward;
                Vector3 tip = origin + forward * 0.4f;
                Handles.color = ENGINE_THRUST_COLOR;
                Handles.DrawLine(origin, tip);
                Handles.DrawWireDisc(tip, forward, 0.1f);
                Handles.DrawLine(origin, tip + t.right * 0.1f);
                Handles.DrawLine(origin, tip - t.right * 0.1f);
                Handles.DrawLine(origin, tip + t.up * 0.1f);
                Handles.DrawLine(origin, tip - t.up * 0.1f);
                string label = Mathf.Approximately(multiplier, 1f) ? name : $"{name}  x{multiplier:0.##}";
                Handles.Label(tip + forward * 0.05f, label);
            }
        }

        /// <summary>
        /// Draws the gimbal range cone for a selected <see cref="Module_Gimbal" />. V1 renders the symmetric <c>gimbalRange</c> half-angle. Asymmetric XP/XN/YP/YN overrides are deferred to a future pass.
        /// </summary>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawGimbalCone(Module_Gimbal module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowGimbalCone || module == null)
            {
                return;
            }
            _gimbalDataField ??= typeof(Module_Gimbal).GetField(
                "dataGimbal",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_gimbalDataField?.GetValue(module) is not Data_Gimbal data) return;
            if (string.IsNullOrEmpty(data.gimbalTransformName)) return;

            float halfAngleRad = data.gimbalRange * Mathf.Deg2Rad;
            float coneLength = 0.6f;
            float coneRadius = Mathf.Tan(halfAngleRad) * coneLength;

            foreach (Transform t in FindTransformsByName(module.gameObject.transform, data.gimbalTransformName))
            {
                Vector3 origin = t.position;
                Vector3 thrustDir = t.forward;
                Vector3 mouth = origin + thrustDir * coneLength;
                Handles.color = GIMBAL_CONE_COLOR;
                Handles.DrawWireDisc(mouth, thrustDir, coneRadius);
                Handles.DrawLine(origin, mouth + t.right * coneRadius);
                Handles.DrawLine(origin, mouth - t.right * coneRadius);
                Handles.DrawLine(origin, mouth + t.up * coneRadius);
                Handles.DrawLine(origin, mouth - t.up * coneRadius);
                Handles.DrawLine(origin - t.right * 0.05f, origin + t.right * 0.05f);
                Handles.DrawLine(origin - t.up * 0.05f, origin + t.up * 0.05f);
            }
        }

        /// <summary>
        /// Draws an arrow at each RCS thruster transform on a selected <see cref="Module_RCS" />. Source is the public <c>ThrusterTransforms</c> array on the module. Per-axis colour modulation in the spec is dropped because the enable flags are runtime state, not authoring data.
        /// </summary>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawRCSThrusters(Module_RCS module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowRCSThrusters || module == null || module.ThrusterTransforms == null)
            {
                return;
            }
            Handles.color = RCS_THRUSTER_COLOR;
            foreach (Transform t in module.ThrusterTransforms)
            {
                if (t == null) continue;
                Vector3 origin = t.position;
                Vector3 forward = t.forward;
                Vector3 tip = origin + forward * 0.2f;
                Handles.DrawLine(origin, tip);
                Handles.DrawLine(tip, tip - forward * 0.05f + t.right * 0.03f);
                Handles.DrawLine(tip, tip - forward * 0.05f - t.right * 0.03f);
                Handles.DrawLine(tip, tip - forward * 0.05f + t.up * 0.03f);
                Handles.DrawLine(tip, tip - forward * 0.05f - t.up * 0.03f);
            }
        }

        private static System.Collections.Generic.IEnumerable<Transform> FindTransformsByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) yield break;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (t != null && t.name == name) yield return t;
            }
        }

        private static readonly Color LIFT_ARROW_COLOR = new(80f / 255f, 220f / 255f, 110f / 255f, 0.95f);
        private static readonly Color CONTROL_ARC_COLOR = new(80f / 255f, 200f / 255f, 230f / 255f, 0.95f);
        private static readonly Color CARGO_VOLUME_COLOR = new(140f / 255f, 220f / 255f, 240f / 255f, 0.95f);
        private static readonly Color FAIRING_EJECTION_COLOR = new(255f / 255f, 150f / 255f, 60f / 255f, 0.95f);
        private static readonly Color DECOUPLE_SPLIT_COLOR = new(220f / 255f, 80f / 255f, 80f / 255f, 0.95f);
        private static readonly Color DECOUPLE_FILL_COLOR = new(220f / 255f, 80f / 255f, 80f / 255f, 0.2f);
        private static readonly Color DOCKING_CAPTURE_COLOR = new(80f / 255f, 220f / 255f, 230f / 255f, 0.95f);
        private static readonly Color DOCKING_ACQUIRE_COLOR = new(140f / 255f, 220f / 255f, 230f / 255f, 0.7f);
        private static readonly Color SOLAR_INCIDENCE_COLOR = new(255f / 255f, 230f / 255f, 80f / 255f, 0.95f);
        private static readonly Color INTAKE_DIRECTION_COLOR = new(120f / 255f, 200f / 255f, 255f / 255f, 0.95f);
        private static readonly Color RADIATOR_SURFACE_COLOR = new(80f / 255f, 180f / 255f, 220f / 255f, 0.95f);

        private static FieldInfo _decoupleDataField;
        private static FieldInfo _dockingNodeDataField;
        private static FieldInfo _solarPanelDataField;
        private static FieldInfo _resourceIntakeDataField;
        private static FieldInfo _activeRadiatorDataField;

        private static FieldInfo _liftSurfaceDataField;
        private static FieldInfo _controlSurfaceDataField;
        private static FieldInfo _cargoBayDataField;
        private static FieldInfo _fairingDataField;

        /// <summary>
        /// Draws the lift-direction arrow for a selected <see cref="Module_LiftingSurface" />. Source: <c>transformName</c> for the anchor (fall back to part root) and <c>transformDir</c> + <c>transformSign</c> for the axis.
        /// </summary>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawLiftSurfaceArrow(Module_LiftingSurface module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowLiftSurfaceArrow || module == null)
            {
                return;
            }
            _liftSurfaceDataField ??= typeof(Module_LiftingSurface).GetField(
                "dataLiftingSurface",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_liftSurfaceDataField?.GetValue(module) is not Data_LiftingSurface data) return;

            Transform anchor = ResolveAnchorOrRoot(module.gameObject.transform, data.transformName);
            if (anchor == null) return;
            Vector3 localAxis = AxisForDir(data.transformDir) * Mathf.Sign(data.transformSign == 0 ? 1f : data.transformSign);
            Vector3 worldAxis = anchor.TransformDirection(localAxis);
            if (worldAxis.sqrMagnitude < 1e-6f) return;
            worldAxis.Normalize();

            Vector3 origin = anchor.position;
            Vector3 tip = origin + worldAxis * 0.5f;
            Handles.color = LIFT_ARROW_COLOR;
            Handles.DrawLine(origin, tip);
            DrawArrowHead(tip, worldAxis, 0.08f);
            Handles.Label(tip + worldAxis * 0.05f, "Lift");
        }

        /// <summary>
        /// Draws the control-surface deflection arc for a selected <see cref="Module_ControlSurface" />.
        /// </summary>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawControlSurfaceArc(Module_ControlSurface module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowControlSurfaceArc || module == null)
            {
                return;
            }
            _controlSurfaceDataField ??= typeof(Module_ControlSurface).GetField(
                "dataCtrlSurface",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_controlSurfaceDataField?.GetValue(module) is not Data_ControlSurface data) return;

            Transform pivot = ResolveAnchorOrRoot(module.gameObject.transform, data.CtrlSurfacePivotTransformName);
            if (pivot == null) return;

            Vector3 rotAxisLocal = AxisForDir(data.CtrlTransformRotAxis);
            Vector3 rotAxisWorld = pivot.TransformDirection(rotAxisLocal);
            if (rotAxisWorld.sqrMagnitude < 1e-6f) return;
            rotAxisWorld.Normalize();

            Vector3 referenceLocal = rotAxisLocal == Vector3.right || rotAxisLocal == Vector3.left
                ? Vector3.forward
                : (rotAxisLocal == Vector3.up || rotAxisLocal == Vector3.down ? Vector3.forward : Vector3.up);
            Vector3 referenceWorld = pivot.TransformDirection(referenceLocal);
            float range = Mathf.Max(data.CtrlSurfaceRange, 0f);
            float arcRadius = 0.4f;

            Handles.color = CONTROL_ARC_COLOR;
            Vector3 startDir = Quaternion.AngleAxis(-range, rotAxisWorld) * referenceWorld;
            Handles.DrawWireArc(pivot.position, rotAxisWorld, startDir, range * 2f, arcRadius);
            Handles.DrawLine(pivot.position, pivot.position + (Quaternion.AngleAxis(-range, rotAxisWorld) * referenceWorld) * arcRadius);
            Handles.DrawLine(pivot.position, pivot.position + (Quaternion.AngleAxis(range, rotAxisWorld) * referenceWorld) * arcRadius);
            Handles.DrawLine(pivot.position, pivot.position + referenceWorld * arcRadius);
            Handles.Label(pivot.position + referenceWorld * (arcRadius * 1.1f), $"±{range:0.#}°");
        }

        /// <summary>
        /// Draws the cargo-bay look-up volume as three orthogonal wire discs at <c>lookUpCenter</c> with radius <c>lookUpRadius</c>.
        /// </summary>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawCargoBayVolume(Module_CargoBay module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowCargoBayVolume || module == null)
            {
                return;
            }
            _cargoBayDataField ??= typeof(Module_CargoBay).GetField(
                "dataCargoBay",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_cargoBayDataField?.GetValue(module) is not Data_CargoBay data) return;
            if (data.lookUpRadius <= 0f) return;

            Transform partRoot = module.gameObject.transform;
            Vector3 worldCenter = partRoot.TransformPoint(data.lookUpCenter);
            Handles.color = CARGO_VOLUME_COLOR;
            Handles.DrawWireDisc(worldCenter, partRoot.right, data.lookUpRadius);
            Handles.DrawWireDisc(worldCenter, partRoot.up, data.lookUpRadius);
            Handles.DrawWireDisc(worldCenter, partRoot.forward, data.lookUpRadius);
        }

        /// <summary>
        /// Draws the fairing ejection arrow at <c>FloatingNodePosition</c> along <c>FloatingNodeDirection</c>. Arrow length scales with <c>log10(EjectionForce + 1)</c> so 10/100/1000 kN read distinctly.
        /// </summary>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawFairingEjection(Module_Fairing module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowFairingEjection || module == null)
            {
                return;
            }
            _fairingDataField ??= typeof(Module_Fairing).GetField(
                "_dataFairing",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_fairingDataField?.GetValue(module) is not Data_Fairing data) return;
            if (data.FloatingNodeDirection.sqrMagnitude < 1e-6f) return;

            Transform partRoot = module.gameObject.transform;
            Vector3 origin = partRoot.TransformPoint(data.FloatingNodePosition);
            Vector3 worldDir = partRoot.TransformDirection(data.FloatingNodeDirection.normalized);
            float force = data.EjectionForce != null ? data.EjectionForce.GetValue() : 0f;
            float length = Mathf.Clamp(Mathf.Log10(Mathf.Max(force, 0f) + 1f) * 0.4f, 0.1f, 1.5f);
            Vector3 tip = origin + worldDir * length;

            Handles.color = FAIRING_EJECTION_COLOR;
            Handles.DrawLine(origin, tip);
            DrawArrowHead(tip, worldDir, 0.08f);
            Handles.Label(tip + worldDir * 0.05f, $"Ejection  {force:0.#} N");
        }

        private static Transform ResolveAnchorOrRoot(Transform partRoot, string transformName)
        {
            if (partRoot == null) return null;
            if (string.IsNullOrEmpty(transformName)) return partRoot;
            foreach (Transform t in FindTransformsByName(partRoot, transformName)) return t;
            return partRoot;
        }

        private static Vector3 AxisForDir(Data_LiftingSurface.TransformDir dir)
        {
            return dir switch
            {
                Data_LiftingSurface.TransformDir.X => Vector3.right,
                Data_LiftingSurface.TransformDir.Y => Vector3.up,
                _ => Vector3.forward,
            };
        }

        private static void DrawArrowHead(Vector3 tip, Vector3 dir, float size)
        {
            Vector3 perp1 = Vector3.Cross(dir, Vector3.up);
            if (perp1.sqrMagnitude < 0.01f) perp1 = Vector3.Cross(dir, Vector3.right);
            perp1.Normalize();
            Vector3 perp2 = Vector3.Cross(dir, perp1).normalized;
            Vector3 baseCenter = tip - dir * size;
            Handles.DrawLine(tip, baseCenter + perp1 * size * 0.5f);
            Handles.DrawLine(tip, baseCenter - perp1 * size * 0.5f);
            Handles.DrawLine(tip, baseCenter + perp2 * size * 0.5f);
            Handles.DrawLine(tip, baseCenter - perp2 * size * 0.5f);
        }

        private static void DrawWireSphere(Vector3 center, float radius)
        {
            Handles.DrawWireDisc(center, Vector3.right, radius);
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.forward, radius);
        }

        /// <summary>
        /// Draws the incidence plane for a selected <see cref="Module_SolarPanel" />. Anchors at the raycast transform and orients along the local <c>PanelIncidenceDirection</c>.
        /// </summary>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawSolarPanelIncidence(Module_SolarPanel module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowSolarPanelIncidence || module == null)
            {
                return;
            }
            _solarPanelDataField ??= typeof(Module_SolarPanel).GetField(
                "dataSolarPanel",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_solarPanelDataField?.GetValue(module) is not Data_SolarPanel data) return;

            Transform anchor = ResolveAnchorOrRoot(module.gameObject.transform, data.RaycastTransformName);
            if (anchor == null) return;
            if (data.PanelIncidenceDirection.sqrMagnitude < 1e-6f) return;
            Vector3 worldNormal = anchor.TransformDirection(data.PanelIncidenceDirection.normalized);

            Vector3 origin = anchor.position;
            Handles.color = SOLAR_INCIDENCE_COLOR;
            Handles.DrawWireDisc(origin, worldNormal, 0.3f);
            Vector3 tip = origin + worldNormal * 0.4f;
            Handles.DrawLine(origin, tip);
            DrawArrowHead(tip, worldNormal, 0.07f);
            Handles.Label(tip + worldNormal * 0.05f, "Sun");
        }

        /// <summary>
        /// Draws the intake direction cone for a selected <see cref="Module_ResourceIntake" />. Cone extends along <c>intakeTransform.forward</c>, which the runtime treats as the mouth-facing direction.
        /// </summary>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawResourceIntakeDirection(Module_ResourceIntake module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowResourceIntakeDirection || module == null)
            {
                return;
            }
            _resourceIntakeDataField ??= typeof(Module_ResourceIntake).GetField(
                "dataResourceIntake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_resourceIntakeDataField?.GetValue(module) is not Data_ResourceIntake data) return;
            if (string.IsNullOrEmpty(data.intakeTransformName)) return;

            foreach (Transform t in FindTransformsByName(module.gameObject.transform, data.intakeTransformName))
            {
                Vector3 origin = t.position;
                Vector3 forward = t.forward;
                Vector3 tip = origin + forward * 0.4f;
                Handles.color = INTAKE_DIRECTION_COLOR;
                Handles.DrawLine(origin, tip);
                Handles.DrawWireDisc(tip, forward, 0.1f);
                Handles.DrawLine(origin, tip + t.right * 0.1f);
                Handles.DrawLine(origin, tip - t.right * 0.1f);
                Handles.DrawLine(origin, tip + t.up * 0.1f);
                Handles.DrawLine(origin, tip - t.up * 0.1f);
                Handles.Label(tip + forward * 0.05f, data.intakeTransformName);
            }
        }

        /// <summary>
        /// Draws the radiator surface plane for a selected <see cref="Module_ActiveRadiator" />. Anchors at the part root because Data_ActiveRadiator has no transform-name field. The local axis comes from the <c>RadiatorDirection</c> enum.
        /// </summary>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawActiveRadiatorSurface(Module_ActiveRadiator module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowActiveRadiatorSurface || module == null)
            {
                return;
            }
            _activeRadiatorDataField ??= typeof(Module_ActiveRadiator).GetField(
                "dataActiveRadiator",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_activeRadiatorDataField?.GetValue(module) is not Data_ActiveRadiator data) return;

            Transform partRoot = module.gameObject.transform;
            Vector3 localNormal = AxisForDir(data.RadiatorDirection);
            Vector3 worldNormal = partRoot.TransformDirection(localNormal);
            if (worldNormal.sqrMagnitude < 1e-6f) return;
            worldNormal.Normalize();

            Vector3 origin = partRoot.position;
            Vector3 perp1 = Vector3.Cross(worldNormal, Vector3.up);
            if (perp1.sqrMagnitude < 0.01f) perp1 = Vector3.Cross(worldNormal, Vector3.right);
            perp1.Normalize();
            Vector3 perp2 = Vector3.Cross(worldNormal, perp1).normalized;

            float half = 0.25f;
            Vector3[] corners =
            {
                origin - perp1 * half - perp2 * half,
                origin - perp1 * half + perp2 * half,
                origin + perp1 * half + perp2 * half,
                origin + perp1 * half - perp2 * half,
            };
            Handles.color = RADIATOR_SURFACE_COLOR;
            Handles.DrawAAPolyLine(2f, corners[0], corners[1], corners[2], corners[3], corners[0]);
            Vector3 tip = origin + worldNormal * 0.3f;
            Handles.DrawLine(origin, tip);
            DrawArrowHead(tip, worldNormal, 0.06f);
        }

        /// <summary>
        /// Draws the decoupler split plane and ejection arrow for a selected <see cref="Module_Decouple" />. Locates the explosive node by ID in the part's <c>attachNodes</c>; silently skips if missing.
        /// </summary>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawDecoupleSplit(Module_Decouple module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowDecoupleSplit || module == null)
            {
                return;
            }
            _decoupleDataField ??= typeof(Module_Decouple).GetField(
                "_dataDecouple",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_decoupleDataField?.GetValue(module) is not Data_Decouple data) return;
            if (string.IsNullOrEmpty(data.explosiveNodeID)) return;

            CorePartData part = module.GetComponent<CorePartData>();
            if (part?.Data?.attachNodes == null) return;

            AttachNodeDefinition? targetNode = null;
            foreach (var node in part.Data.attachNodes)
            {
                if (node.nodeID == data.explosiveNodeID)
                {
                    targetNode = node;
                    break;
                }
            }
            if (!targetNode.HasValue) return;

            Matrix4x4 localToWorld = module.gameObject.transform.localToWorldMatrix;
            Vector3 nodePos = localToWorld.MultiplyPoint(targetNode.Value.position);
            Vector3 nodeDir = localToWorld.MultiplyVector(targetNode.Value.orientation);
            if (nodeDir.sqrMagnitude < 1e-6f) return;
            nodeDir.Normalize();

            float diameter = PartSizeRegistry.GetAttachNodeDiameter(targetNode.Value);
            float planeSize = Mathf.Max(diameter * 1.5f, 0.3f);

            Vector3 perp1 = Vector3.Cross(nodeDir, Vector3.up);
            if (perp1.sqrMagnitude < 0.01f) perp1 = Vector3.Cross(nodeDir, Vector3.right);
            perp1.Normalize();
            Vector3 perp2 = Vector3.Cross(nodeDir, perp1).normalized;

            Vector3 half1 = perp1 * planeSize * 0.5f;
            Vector3 half2 = perp2 * planeSize * 0.5f;
            Vector3[] corners =
            {
                nodePos - half1 - half2,
                nodePos - half1 + half2,
                nodePos + half1 + half2,
                nodePos + half1 - half2,
            };

            Handles.color = DECOUPLE_FILL_COLOR;
            Handles.DrawAAConvexPolygon(corners);
            Handles.color = DECOUPLE_SPLIT_COLOR;
            Handles.DrawAAPolyLine(2.5f, corners[0], corners[1], corners[2], corners[3], corners[0]);

            Vector3 arrowTip = nodePos + nodeDir * 0.4f;
            Handles.DrawAAPolyLine(2.5f, nodePos, arrowTip);
            DrawArrowHead(arrowTip, nodeDir, 0.08f);
            Handles.Label(arrowTip + nodeDir * 0.05f, data.explosiveNodeID);
        }

        /// <summary>
        /// Draws the docking-node alignment frame for a selected <see cref="Module_DockingNode" />. Renders target reticle, capture and acquire spheres, approach cone at the docking transform, and a coordinate frame at the control transform.
        /// </summary>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawDockingFrame(Module_DockingNode module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowDockingFrame || module == null)
            {
                return;
            }
            _dockingNodeDataField ??= typeof(Module_DockingNode).GetField(
                "_dataDockingNode",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_dockingNodeDataField?.GetValue(module) is not Data_DockingNode data) return;

            Transform partRoot = module.gameObject.transform;

            foreach (Transform t in FindTransformsByName(partRoot, data.DockingTransformName))
            {
                Vector3 pos = t.position;
                Vector3 fwd = t.forward;
                float captureRadius = Mathf.Max(data.CaptureRange, 0.01f);
                float acquireRadius = Mathf.Max(data.AcquireRange, captureRadius * 1.5f);

                Handles.color = DOCKING_CAPTURE_COLOR;
                Handles.DrawWireDisc(pos, fwd, captureRadius * 0.5f);
                Handles.DrawWireDisc(pos, fwd, captureRadius * 0.75f);
                Handles.DrawWireDisc(pos, fwd, captureRadius);
                DrawWireSphere(pos, captureRadius);

                Handles.color = DOCKING_ACQUIRE_COLOR;
                DrawWireSphere(pos, acquireRadius);

                float halfAngleDeg = Mathf.Acos(Mathf.Clamp(data.AcquireMinFwdDot, -1f, 1f)) * Mathf.Rad2Deg;
                float mouthRadius = acquireRadius * Mathf.Tan(halfAngleDeg * Mathf.Deg2Rad);
                Vector3 mouth = pos + fwd * acquireRadius;
                Handles.DrawWireDisc(mouth, fwd, mouthRadius);
                Handles.DrawLine(pos, mouth + t.right * mouthRadius);
                Handles.DrawLine(pos, mouth - t.right * mouthRadius);
                Handles.DrawLine(pos, mouth + t.up * mouthRadius);
                Handles.DrawLine(pos, mouth - t.up * mouthRadius);

                Handles.color = DOCKING_CAPTURE_COLOR;
                Handles.Label(pos + t.up * (captureRadius + 0.05f),
                    $"capture {captureRadius:0.###}m   acquire {acquireRadius:0.##}m   ±{halfAngleDeg:0.#}°");
            }

            foreach (Transform t in FindTransformsByName(partRoot, data.ControlTransformName))
            {
                Vector3 origin = t.position;
                float axisLen = 0.15f;
                Handles.color = new Color(1f, 0.4f, 0.4f, 0.9f);
                Handles.DrawLine(origin, origin + t.right * axisLen);
                Handles.color = new Color(0.4f, 1f, 0.4f, 0.9f);
                Handles.DrawLine(origin, origin + t.up * axisLen);
                Handles.color = new Color(0.4f, 0.5f, 1f, 0.9f);
                Handles.DrawLine(origin, origin + t.forward * axisLen);
            }
        }
    }
}
