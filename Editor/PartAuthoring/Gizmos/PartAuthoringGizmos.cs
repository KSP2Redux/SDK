using System;
using System.Collections.Generic;
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
        private static readonly Dictionary<(Type, string), FieldInfo> _fieldCache = new();

        /// <summary>
        /// Reads a private or protected data field on a module by name and casts it to TData. Caches the resolved FieldInfo per (type, name) pair.
        /// </summary>
        private static bool TryGetData<TData>(object module, string fieldName, out TData data) where TData : class
        {
            data = null;
            if (module == null) return false;
            var moduleType = module.GetType();
            var key = (moduleType, fieldName);
            if (!_fieldCache.TryGetValue(key, out var field))
            {
                field = moduleType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                _fieldCache[key] = field;
            }
            if (field?.GetValue(module) is TData resolved)
            {
                data = resolved;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Draws a cone: a disc at the mouth, four arms from the origin to the mouth perimeter, and (optionally) an axis line from origin to mouth.
        /// </summary>
        private static void DrawCone(Vector3 origin, Vector3 mouth, Vector3 forward, Vector3 right, Vector3 up, float radius, bool drawAxis = true)
        {
            Handles.DrawWireDisc(mouth, forward, radius);
            if (drawAxis) Handles.DrawLine(origin, mouth);
            Handles.DrawLine(origin, mouth + right * radius);
            Handles.DrawLine(origin, mouth - right * radius);
            Handles.DrawLine(origin, mouth + up * radius);
            Handles.DrawLine(origin, mouth - up * radius);
        }

        /// <summary>
        /// Draws centre-of-mass, centre-of-lift, and attach-node gizmos for the selected <see cref="CorePartData" />.
        /// </summary>
        /// <param name="data">The selected part.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawCorePartData(CorePartData data, GizmoType gizmoType)
        {
            var localToWorldMatrix = data.transform.localToWorldMatrix;

            if (PartAuthoringGizmoSettings.ShowCenterOfMass)
            {
                var centerOfMassPosition = localToWorldMatrix.MultiplyPoint(data.Data.coMassOffset);
                UnityEngine.Gizmos.DrawIcon(
                    centerOfMassPosition,
                    SDKConfiguration.BasePath + "/Assets/Gizmos/com_icon.png",
                    false
                );
            }

            if (PartAuthoringGizmoSettings.ShowCenterOfLift)
            {
                var centerOfLiftPosition = localToWorldMatrix.MultiplyPoint(data.Data.coLiftOffset);
                UnityEngine.Gizmos.DrawIcon(
                    centerOfLiftPosition,
                    SDKConfiguration.BasePath + "/Assets/Gizmos/col_icon.png",
                    false
                );
            }

            if (!PartAuthoringGizmoSettings.ShowAttachNodes) return;

            foreach (var attachNode in data.Data.attachNodes)
            {
                var posWorld = localToWorldMatrix.MultiplyPoint(attachNode.position);
                var dirWorld = localToWorldMatrix.MultiplyVector(attachNode.orientation);
                if (dirWorld.sqrMagnitude < 1e-6f) continue;
                dirWorld.Normalize();

                // visualSize is the in-game orb display only. Physical mating size comes from sizeKey.
                var diameter = PartSizeRegistry.GetAttachNodeDiameter(attachNode);
                var radius = diameter * 0.5f;
                var arrowLen = Mathf.Max(radius * 0.3f, 0.05f);

                var discColor = ColorForNodeType(attachNode.nodeType);
                Handles.color = discColor;
                Handles.DrawWireDisc(posWorld, dirWorld, radius);
                Handles.DrawLine(posWorld, posWorld + dirWorld * arrowLen);
                UnityEngine.Gizmos.color = discColor;
                UnityEngine.Gizmos.DrawSphere(posWorld, 0.05f);

                var sizeKey = PartSizeRegistry.GetAttachNodeSizeKey(attachNode);
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
            var length = Mathf.Max(PartAuthoringGizmoSettings.VirtualPartLength, 0.01f);
            Vector3 axis;
            Vector3 centerOffset;
            if (nodeType == AttachNodeType.Surface)
            {
                // Axis perpendicular to nodeDir, wall tangent to the node disc, body outside the part.
                var candidate = Vector3.ProjectOnPlane(partRoot.up, nodeDir);
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

            var center = nodePos + centerOffset;
            var endA = center - axis * (length * 0.5f);
            var endB = center + axis * (length * 0.5f);

            Handles.color = VIRTUAL_PART_COLOR;
            Handles.DrawWireDisc(endA, axis, radius);
            Handles.DrawWireDisc(endB, axis, radius);

            // Equator connectors at 0, 90, 180, 270 degrees around the cylinder.
            var ringX = Vector3.Cross(axis, Vector3.up);
            if (ringX.sqrMagnitude < 0.01f)
            {
                ringX = Vector3.Cross(axis, Vector3.right);
            }
            ringX.Normalize();
            var ringY = Vector3.Cross(axis, ringX).normalized;
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
            if (!PartAuthoringGizmoSettings.ShowAttachNodes) return;

            UnityEngine.Gizmos.color = new Color(Color.green.r, Color.green.g, Color.green.b, 0.5f);
            var pos = node.transform.position;
            UnityEngine.Gizmos.DrawRay(pos, node.transform.rotation * Vector3.forward * 0.25f);
            UnityEngine.Gizmos.DrawSphere(pos, 0.05f);
        }

        /// <summary>
        /// Draws the heatshield shielding-direction arrow for a selected <see cref="Module_Heatshield" />.
        /// </summary>
        /// <param name="module">The selected heatshield module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawHeatshieldShieldingDirection(Module_Heatshield module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowHeatshieldShieldingDirection) return;
            if (!TryGetData<Data_Heatshield>(module, "_dataHeatshield", out var data)) return;

            var partTransform = module.gameObject.transform;
            var worldDir = partTransform.TransformDirection(data.ShieldingDirection);
            if (worldDir.sqrMagnitude < 1e-6f) return;
            var origin = partTransform.position;
            var endpoint = origin + worldDir.normalized;
            UnityEngine.Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.9f);
            UnityEngine.Gizmos.DrawLine(origin, endpoint);
            UnityEngine.Gizmos.DrawSphere(endpoint, 0.05f);
        }

        /// <summary>
        /// Draws the wheel-bogey rotation-axis arrow for a selected <see cref="Module_WheelBogey" />.
        /// </summary>
        /// <param name="module">The selected wheel-bogey module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawWheelBogeyAxis(Module_WheelBogey module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowWheelBogeyAxis) return;
            if (!TryGetData<Data_WheelBogey>(module, "dataWheelBogey", out var data)) return;
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
            if (!PartAuthoringGizmoSettings.ShowWheelBogeyUpAxis) return;
            if (!TryGetData<Data_WheelBogey>(module, "dataWheelBogey", out var data)) return;
            DrawBogeyAxisArrow(module, data.bogeyUpAxis, new Color(0.3f, 0.85f, 0.4f, 0.9f));
        }

        private static void DrawBogeyAxisArrow(Module_WheelBogey module, Vector3 localAxis, Color color)
        {
            var partTransform = module.gameObject.transform;
            var worldDir = partTransform.TransformDirection(localAxis);
            if (worldDir.sqrMagnitude < 1e-6f) return;
            var origin = partTransform.position;
            var endpoint = origin + worldDir.normalized;
            UnityEngine.Gizmos.color = color;
            UnityEngine.Gizmos.DrawLine(origin, endpoint);
            UnityEngine.Gizmos.DrawSphere(endpoint, 0.05f);
        }

        private static readonly Color ENGINE_THRUST_COLOR = new(255f / 255f, 140f / 255f, 60f / 255f, 0.95f);
        private static readonly Color GIMBAL_CONE_COLOR = new(255f / 255f, 200f / 255f, 60f / 255f, 0.95f);
        private static readonly Color RCS_THRUSTER_COLOR = new(255f / 255f, 140f / 255f, 60f / 255f, 0.95f);

        /// <summary>
        /// Draws the thrust-transform cones for a selected <see cref="Module_Engine" />.
        /// </summary>
        /// <remarks>
        /// Iterates every engine mode and renders each unique <c>ThrustTransformName</c> once. Modes
        /// without explicit ThrustTransformNamesMultipliers fall back to the legacy
        /// <c>thrustVectorTransformName</c>.
        /// </remarks>
        /// <param name="module">The selected engine module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawEngineThrustTransforms(Module_Engine module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowEngineThrustTransforms) return;
            if (!TryGetData<Data_Engine>(module, "dataEngine", out var data) || data.engineModes == null) return;

            var seenNames = new HashSet<string>();
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
            foreach (var t in FindTransformsByName(partRoot, name))
            {
                var origin = t.position;
                var forward = t.forward;
                var mouth = origin + forward * 0.4f;
                Handles.color = ENGINE_THRUST_COLOR;
                DrawCone(origin, mouth, forward, t.right, t.up, 0.1f);
                var label = Mathf.Approximately(multiplier, 1f) ? name : $"{name}  x{multiplier:0.##}";
                Handles.Label(mouth + forward * 0.05f, label);
            }
        }

        /// <summary>
        /// Draws the gimbal range cone for a selected <see cref="Module_Gimbal" />.
        /// </summary>
        /// <remarks>
        /// Renders the symmetric <c>gimbalRange</c> half-angle. Asymmetric XP/XN/YP/YN overrides are
        /// not yet rendered.
        /// </remarks>
        /// <param name="module">The selected gimbal module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawGimbalCone(Module_Gimbal module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowGimbalCone) return;
            if (!TryGetData<Data_Gimbal>(module, "dataGimbal", out var data)) return;
            if (string.IsNullOrEmpty(data.gimbalTransformName)) return;

            var halfAngleRad = data.gimbalRange * Mathf.Deg2Rad;
            var coneLength = 0.6f;
            var coneRadius = Mathf.Tan(halfAngleRad) * coneLength;

            foreach (var t in FindTransformsByName(module.gameObject.transform, data.gimbalTransformName))
            {
                var origin = t.position;
                var thrustDir = t.forward;
                var mouth = origin + thrustDir * coneLength;
                Handles.color = GIMBAL_CONE_COLOR;
                DrawCone(origin, mouth, thrustDir, t.right, t.up, coneRadius, drawAxis: false);
                Handles.DrawLine(origin - t.right * 0.05f, origin + t.right * 0.05f);
                Handles.DrawLine(origin - t.up * 0.05f, origin + t.up * 0.05f);
            }
        }

        /// <summary>
        /// Draws an arrow at each RCS thruster transform on a selected <see cref="Module_RCS" />.
        /// </summary>
        /// <remarks>
        /// Source is the public <c>ThrusterTransforms</c> array on the module. Per-axis colour
        /// modulation is intentionally omitted because the enable flags are runtime state, not
        /// authoring data.
        /// </remarks>
        /// <param name="module">The selected RCS module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawRCSThrusters(Module_RCS module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowRCSThrusters || module == null || module.ThrusterTransforms == null) return;
            Handles.color = RCS_THRUSTER_COLOR;
            foreach (var t in module.ThrusterTransforms)
            {
                if (t == null) continue;
                var origin = t.position;
                var forward = t.forward;
                var tip = origin + forward * 0.2f;
                Handles.DrawLine(origin, tip);
                Handles.DrawLine(tip, tip - forward * 0.05f + t.right * 0.03f);
                Handles.DrawLine(tip, tip - forward * 0.05f - t.right * 0.03f);
                Handles.DrawLine(tip, tip - forward * 0.05f + t.up * 0.03f);
                Handles.DrawLine(tip, tip - forward * 0.05f - t.up * 0.03f);
            }
        }

        private static IEnumerable<Transform> FindTransformsByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) yield break;
            foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
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

        /// <summary>
        /// Draws the lift-direction arrow for a selected <see cref="Module_LiftingSurface" />.
        /// </summary>
        /// <remarks>
        /// Anchor source is <c>transformName</c> (falling back to the part root), and the local axis
        /// comes from <c>transformDir</c> combined with <c>transformSign</c>.
        /// </remarks>
        /// <param name="module">The selected lifting-surface module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawLiftSurfaceArrow(Module_LiftingSurface module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowLiftSurfaceArrow) return;
            if (!TryGetData<Data_LiftingSurface>(module, "dataLiftingSurface", out var data)) return;

            var anchor = ResolveAnchorOrRoot(module.gameObject.transform, data.transformName);
            if (anchor == null) return;
            var localAxis = AxisForDir(data.transformDir) * Mathf.Sign(data.transformSign == 0 ? 1f : data.transformSign);
            var worldAxis = anchor.TransformDirection(localAxis);
            if (worldAxis.sqrMagnitude < 1e-6f) return;
            worldAxis.Normalize();

            var origin = anchor.position;
            var tip = origin + worldAxis * 0.5f;
            Handles.color = LIFT_ARROW_COLOR;
            Handles.DrawLine(origin, tip);
            DrawArrowHead(tip, worldAxis, 0.08f);
            Handles.Label(tip + worldAxis * 0.05f, "Lift");
        }

        /// <summary>
        /// Draws the control-surface deflection arc for a selected <see cref="Module_ControlSurface" />.
        /// </summary>
        /// <param name="module">The selected control-surface module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawControlSurfaceArc(Module_ControlSurface module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowControlSurfaceArc) return;
            if (!TryGetData<Data_ControlSurface>(module, "dataCtrlSurface", out var data)) return;

            var pivot = ResolveAnchorOrRoot(module.gameObject.transform, data.CtrlSurfacePivotTransformName);
            if (pivot == null) return;

            var rotAxisLocal = AxisForDir(data.CtrlTransformRotAxis);
            var rotAxisWorld = pivot.TransformDirection(rotAxisLocal);
            if (rotAxisWorld.sqrMagnitude < 1e-6f) return;
            rotAxisWorld.Normalize();

            var referenceLocal = rotAxisLocal == Vector3.right || rotAxisLocal == Vector3.left
                ? Vector3.forward
                : (rotAxisLocal == Vector3.up || rotAxisLocal == Vector3.down ? Vector3.forward : Vector3.up);
            var referenceWorld = pivot.TransformDirection(referenceLocal);
            var range = Mathf.Max(data.CtrlSurfaceRange, 0f);
            var arcRadius = 0.4f;

            Handles.color = CONTROL_ARC_COLOR;
            var startDir = Quaternion.AngleAxis(-range, rotAxisWorld) * referenceWorld;
            Handles.DrawWireArc(pivot.position, rotAxisWorld, startDir, range * 2f, arcRadius);
            Handles.DrawLine(pivot.position, pivot.position + (Quaternion.AngleAxis(-range, rotAxisWorld) * referenceWorld) * arcRadius);
            Handles.DrawLine(pivot.position, pivot.position + (Quaternion.AngleAxis(range, rotAxisWorld) * referenceWorld) * arcRadius);
            Handles.DrawLine(pivot.position, pivot.position + referenceWorld * arcRadius);
            Handles.Label(pivot.position + referenceWorld * (arcRadius * 1.1f), $"±{range:0.#}°");
        }

        /// <summary>
        /// Draws the cargo-bay look-up volume as three orthogonal wire discs at <c>lookUpCenter</c> with radius <c>lookUpRadius</c>.
        /// </summary>
        /// <param name="module">The selected cargo-bay module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawCargoBayVolume(Module_CargoBay module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowCargoBayVolume) return;
            if (!TryGetData<Data_CargoBay>(module, "dataCargoBay", out var data)) return;
            if (data.lookUpRadius <= 0f) return;

            var partRoot = module.gameObject.transform;
            var worldCenter = partRoot.TransformPoint(data.lookUpCenter);
            Handles.color = CARGO_VOLUME_COLOR;
            Handles.DrawWireDisc(worldCenter, partRoot.right, data.lookUpRadius);
            Handles.DrawWireDisc(worldCenter, partRoot.up, data.lookUpRadius);
            Handles.DrawWireDisc(worldCenter, partRoot.forward, data.lookUpRadius);
        }

        /// <summary>
        /// Draws the fairing ejection arrow at <c>FloatingNodePosition</c> along <c>FloatingNodeDirection</c>.
        /// </summary>
        /// <remarks>
        /// Arrow length scales with <c>log10(EjectionForce + 1)</c> so 10/100/1000 kN read distinctly.
        /// </remarks>
        /// <param name="module">The selected fairing module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawFairingEjection(Module_Fairing module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowFairingEjection) return;
            if (!TryGetData<Data_Fairing>(module, "_dataFairing", out var data)) return;
            if (data.FloatingNodeDirection.sqrMagnitude < 1e-6f) return;

            var partRoot = module.gameObject.transform;
            var origin = partRoot.TransformPoint(data.FloatingNodePosition);
            var worldDir = partRoot.TransformDirection(data.FloatingNodeDirection.normalized);
            var force = data.EjectionForce != null ? data.EjectionForce.GetValue() : 0f;
            var length = Mathf.Clamp(Mathf.Log10(Mathf.Max(force, 0f) + 1f) * 0.4f, 0.1f, 1.5f);
            var tip = origin + worldDir * length;

            Handles.color = FAIRING_EJECTION_COLOR;
            Handles.DrawLine(origin, tip);
            DrawArrowHead(tip, worldDir, 0.08f);
            Handles.Label(tip + worldDir * 0.05f, $"Ejection  {force:0.#} N");
        }

        private static Transform ResolveAnchorOrRoot(Transform partRoot, string transformName)
        {
            if (partRoot == null) return null;
            if (string.IsNullOrEmpty(transformName)) return partRoot;
            foreach (var t in FindTransformsByName(partRoot, transformName)) return t;
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
            var perp1 = Vector3.Cross(dir, Vector3.up);
            if (perp1.sqrMagnitude < 0.01f) perp1 = Vector3.Cross(dir, Vector3.right);
            perp1.Normalize();
            var perp2 = Vector3.Cross(dir, perp1).normalized;
            var baseCenter = tip - dir * size;
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
        /// Draws the incidence plane for a selected <see cref="Module_SolarPanel" />.
        /// </summary>
        /// <remarks>
        /// Anchors at the raycast transform and orients along the local <c>PanelIncidenceDirection</c>.
        /// </remarks>
        /// <param name="module">The selected solar-panel module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawSolarPanelIncidence(Module_SolarPanel module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowSolarPanelIncidence) return;
            if (!TryGetData<Data_SolarPanel>(module, "dataSolarPanel", out var data)) return;

            var anchor = ResolveAnchorOrRoot(module.gameObject.transform, data.RaycastTransformName);
            if (anchor == null) return;
            if (data.PanelIncidenceDirection.sqrMagnitude < 1e-6f) return;
            var worldNormal = anchor.TransformDirection(data.PanelIncidenceDirection.normalized);

            var origin = anchor.position;
            Handles.color = SOLAR_INCIDENCE_COLOR;
            Handles.DrawWireDisc(origin, worldNormal, 0.3f);
            var tip = origin + worldNormal * 0.4f;
            Handles.DrawLine(origin, tip);
            DrawArrowHead(tip, worldNormal, 0.07f);
            Handles.Label(tip + worldNormal * 0.05f, "Sun");
        }

        /// <summary>
        /// Draws the intake direction cone for a selected <see cref="Module_ResourceIntake" />.
        /// </summary>
        /// <remarks>
        /// Cone extends along <c>intakeTransform.forward</c>, which the runtime treats as the
        /// mouth-facing direction.
        /// </remarks>
        /// <param name="module">The selected resource-intake module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawResourceIntakeDirection(Module_ResourceIntake module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowResourceIntakeDirection) return;
            if (!TryGetData<Data_ResourceIntake>(module, "dataResourceIntake", out var data)) return;
            if (string.IsNullOrEmpty(data.intakeTransformName)) return;

            foreach (var t in FindTransformsByName(module.gameObject.transform, data.intakeTransformName))
            {
                var origin = t.position;
                var forward = t.forward;
                var mouth = origin + forward * 0.4f;
                Handles.color = INTAKE_DIRECTION_COLOR;
                DrawCone(origin, mouth, forward, t.right, t.up, 0.1f);
                Handles.Label(mouth + forward * 0.05f, data.intakeTransformName);
            }
        }

        /// <summary>
        /// Draws the radiator surface plane for a selected <see cref="Module_ActiveRadiator" />.
        /// </summary>
        /// <remarks>
        /// Anchors at the part root because Data_ActiveRadiator has no transform-name field. The local
        /// axis comes from the <c>RadiatorDirection</c> enum.
        /// </remarks>
        /// <param name="module">The selected active-radiator module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawActiveRadiatorSurface(Module_ActiveRadiator module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowActiveRadiatorSurface) return;
            if (!TryGetData<Data_ActiveRadiator>(module, "dataActiveRadiator", out var data)) return;

            var partRoot = module.gameObject.transform;
            var localNormal = AxisForDir(data.RadiatorDirection);
            var worldNormal = partRoot.TransformDirection(localNormal);
            if (worldNormal.sqrMagnitude < 1e-6f) return;
            worldNormal.Normalize();

            var origin = partRoot.position;
            var perp1 = Vector3.Cross(worldNormal, Vector3.up);
            if (perp1.sqrMagnitude < 0.01f) perp1 = Vector3.Cross(worldNormal, Vector3.right);
            perp1.Normalize();
            var perp2 = Vector3.Cross(worldNormal, perp1).normalized;

            var half = 0.25f;
            Vector3[] corners =
            {
                origin - perp1 * half - perp2 * half,
                origin - perp1 * half + perp2 * half,
                origin + perp1 * half + perp2 * half,
                origin + perp1 * half - perp2 * half,
            };
            Handles.color = RADIATOR_SURFACE_COLOR;
            Handles.DrawAAPolyLine(2f, corners[0], corners[1], corners[2], corners[3], corners[0]);
            var tip = origin + worldNormal * 0.3f;
            Handles.DrawLine(origin, tip);
            DrawArrowHead(tip, worldNormal, 0.06f);
        }

        /// <summary>
        /// Draws the decoupler split plane and ejection arrow for a selected <see cref="Module_Decouple" />.
        /// </summary>
        /// <remarks>
        /// Locates the explosive node by ID in the part's <c>attachNodes</c>. Silently skips if the
        /// node is missing.
        /// </remarks>
        /// <param name="module">The selected decoupler module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawDecoupleSplit(Module_Decouple module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowDecoupleSplit) return;
            if (!TryGetData<Data_Decouple>(module, "_dataDecouple", out var data)) return;
            if (string.IsNullOrEmpty(data.explosiveNodeID)) return;

            var part = module.GetComponent<CorePartData>();
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

            var localToWorld = module.gameObject.transform.localToWorldMatrix;
            var nodePos = localToWorld.MultiplyPoint(targetNode.Value.position);
            var nodeDir = localToWorld.MultiplyVector(targetNode.Value.orientation);
            if (nodeDir.sqrMagnitude < 1e-6f) return;
            nodeDir.Normalize();

            var diameter = PartSizeRegistry.GetAttachNodeDiameter(targetNode.Value);
            var planeSize = Mathf.Max(diameter * 1.5f, 0.3f);

            var perp1 = Vector3.Cross(nodeDir, Vector3.up);
            if (perp1.sqrMagnitude < 0.01f) perp1 = Vector3.Cross(nodeDir, Vector3.right);
            perp1.Normalize();
            var perp2 = Vector3.Cross(nodeDir, perp1).normalized;

            var half1 = perp1 * planeSize * 0.5f;
            var half2 = perp2 * planeSize * 0.5f;
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

            var arrowTip = nodePos + nodeDir * 0.4f;
            Handles.DrawAAPolyLine(2.5f, nodePos, arrowTip);
            DrawArrowHead(arrowTip, nodeDir, 0.08f);
            Handles.Label(arrowTip + nodeDir * 0.05f, data.explosiveNodeID);
        }

        /// <summary>
        /// Draws the docking-node alignment frame for a selected <see cref="Module_DockingNode" />.
        /// </summary>
        /// <remarks>
        /// Renders the target reticle, capture and acquire spheres, the approach cone at the docking
        /// transform, and a coordinate frame at the control transform.
        /// </remarks>
        /// <param name="module">The selected docking-node module.</param>
        /// <param name="gizmoType">Unity's selection-state flags for the draw call.</param>
        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        public static void DrawDockingFrame(Module_DockingNode module, GizmoType gizmoType)
        {
            if (!PartAuthoringGizmoSettings.ShowDockingFrame) return;
            if (!TryGetData<Data_DockingNode>(module, "_dataDockingNode", out var data)) return;

            var partRoot = module.gameObject.transform;

            foreach (var t in FindTransformsByName(partRoot, data.DockingTransformName))
            {
                var pos = t.position;
                var fwd = t.forward;
                var captureRadius = Mathf.Max(data.CaptureRange, 0.01f);
                var acquireRadius = Mathf.Max(data.AcquireRange, captureRadius * 1.5f);

                Handles.color = DOCKING_CAPTURE_COLOR;
                Handles.DrawWireDisc(pos, fwd, captureRadius * 0.5f);
                Handles.DrawWireDisc(pos, fwd, captureRadius * 0.75f);
                Handles.DrawWireDisc(pos, fwd, captureRadius);
                DrawWireSphere(pos, captureRadius);

                Handles.color = DOCKING_ACQUIRE_COLOR;
                DrawWireSphere(pos, acquireRadius);

                var halfAngleDeg = Mathf.Acos(Mathf.Clamp(data.AcquireMinFwdDot, -1f, 1f)) * Mathf.Rad2Deg;
                var mouthRadius = acquireRadius * Mathf.Tan(halfAngleDeg * Mathf.Deg2Rad);
                var mouth = pos + fwd * acquireRadius;
                DrawCone(pos, mouth, fwd, t.right, t.up, mouthRadius, drawAxis: false);

                Handles.color = DOCKING_CAPTURE_COLOR;
                Handles.Label(pos + t.up * (captureRadius + 0.05f),
                    $"capture {captureRadius:0.###}m   acquire {acquireRadius:0.##}m   ±{halfAngleDeg:0.#}°");
            }

            foreach (var t in FindTransformsByName(partRoot, data.ControlTransformName))
            {
                var origin = t.position;
                var axisLen = 0.15f;
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
