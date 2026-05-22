using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using KSP;
using KSP.Modules;
using KSP.OAB;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ksp2UnityTools.Editor.CustomEditors
{
    /// <summary>
    /// State holder, geometry calculator, and SceneView gizmo for the shroud preview shown in
    /// the Fairing data editor. The author-facing UI is now a pure-UITK surface in
    /// <see cref="PartAuthoring.Inspectors.DataEditors.FairingDataEditor" />; this class owns the
    /// shared per-module settings dictionary, the metrics calculation, and the
    /// <c>[DrawGizmo]</c> registration so both the inspector and the SceneView draw against the
    /// same numbers.
    /// </summary>
    internal static class ShroudPreviewEditor
    {
        internal sealed class ShroudPreviewSettings
        {
            public bool Enabled = true;
            public string TargetSizeKey;
        }

        internal struct ShroudPreviewMetrics
        {
            public float HostDiameter;
            public float TargetDiameter;
            public float ResolvedDiameter;
            public float BaseRadius;
            public float TargetRadius;
            public float StartHeight;
            public float EndHeight;
            public float GeneratedHeight;
        }

        private static readonly Dictionary<int, ShroudPreviewSettings> SettingsByModule = new();

        private static readonly FieldInfo FairingDataField =
            typeof(Module_Fairing).GetField("_dataFairing", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Color PreviewFillColor = new(0.16f, 0.55f, 0.95f, 0.16f);
        private static readonly Color PreviewLineColor = new(0.16f, 0.85f, 1f, 0.9f);

        [DrawGizmo(GizmoType.Active | GizmoType.Selected)]
        private static void DrawGizmoForFairing(Module_Fairing module, GizmoType gizmoType)
        {
            if (module == null ||
                !SettingsByModule.TryGetValue(module.GetInstanceID(), out var settings) ||
                !settings.Enabled)
            {
                return;
            }

            var fairing = GetFairingData(module);
            if (fairing == null)
            {
                return;
            }

            var moduleTransform = module.gameObject.transform;
            var modelTransform = GetPreviewModelTransform(moduleTransform);
            var metrics = CalculateMetrics(fairing, settings.TargetSizeKey, modelTransform);
            DrawFrustum(modelTransform != null ? modelTransform : moduleTransform, fairing, metrics);
        }

        internal static Data_Fairing GetFairingData(Module_Fairing module)
        {
            return module == null || FairingDataField == null
                ? null
                : FairingDataField.GetValue(module) as Data_Fairing;
        }

        internal static ShroudPreviewSettings GetOrCreateSettings(Module_Fairing module, Data_Fairing fairing)
        {
            var instanceId = module.GetInstanceID();
            if (SettingsByModule.TryGetValue(instanceId, out var settings))
            {
                if (!PartSizeRegistry.IsValidKey(settings.TargetSizeKey))
                {
                    settings.TargetSizeKey = GetDefaultTargetKey(module, fairing);
                }
                return settings;
            }
            settings = new ShroudPreviewSettings
            {
                TargetSizeKey = GetDefaultTargetKey(module, fairing),
            };
            SettingsByModule[instanceId] = settings;
            return settings;
        }

        private static string GetDefaultTargetKey(Module_Fairing module, Data_Fairing fairing)
        {
            var coreData = module.GetComponent<CorePartData>() ?? module.GetComponentInParent<CorePartData>();
            var partSizeKey = coreData?.Data == null ? null : PartSizeRegistry.GetPartSizeKey(coreData.Data);
            return PartSizeRegistry.IsValidKey(partSizeKey)
                ? partSizeKey
                : GetSmallestSizeKeyAtLeast(fairing.BaseRadius * 2f);
        }

        private static string GetSmallestSizeKeyAtLeast(float diameter)
        {
            foreach (var definition in PartSizeRegistry.Definitions)
            {
                if (definition.Diameter >= diameter)
                {
                    return definition.Key;
                }
            }
            return PartSizeRegistry.GetLargest().Key;
        }

        internal static ShroudPreviewMetrics CalculateMetrics(
            Data_Fairing fairing,
            string targetSizeKey,
            Transform modelTransform)
        {
            var hostDiameter = Mathf.Max(0.001f, fairing.BaseRadius * 2f);
            var targetDiameter = PartSizeRegistry.Get(targetSizeKey).Diameter;
            var resolvedDiameter = targetDiameter >= hostDiameter ? targetDiameter : hostDiameter;

            if (fairing.MaxAutoFairingTargetRadius > 0 && resolvedDiameter > hostDiameter)
            {
                var maxDiameter = PartSizeRegistry.GetLegacyAttachNodeSize(fairing.MaxAutoFairingTargetRadius).Diameter;
                resolvedDiameter = Mathf.Max(hostDiameter, Mathf.Min(maxDiameter, resolvedDiameter));
            }
            else if (fairing.MinAutoFairingTargetRadius > 0 && resolvedDiameter < hostDiameter)
            {
                var minDiameter = PartSizeRegistry.GetLegacyAttachNodeSize(fairing.MinAutoFairingTargetRadius).Diameter;
                resolvedDiameter = Mathf.Max(minDiameter, resolvedDiameter);
            }

            resolvedDiameter = PartSizeRegistry.SnapDownToKnownDiameter(resolvedDiameter);
            var maxRadius = fairing.MaxRadius > 0f ? fairing.MaxRadius : resolvedDiameter * 0.5f;
            var minRadius = Mathf.Min(Mathf.Max(0f, fairing.CapRadius), maxRadius);
            var startHeight = GetRuntimeStartHeight(fairing, modelTransform);
            var height = GetRuntimeGeneratedHeight(fairing, startHeight);

            return new ShroudPreviewMetrics
            {
                HostDiameter = hostDiameter,
                TargetDiameter = targetDiameter,
                ResolvedDiameter = resolvedDiameter,
                BaseRadius = Mathf.Max(0f, fairing.BaseRadius),
                TargetRadius = Mathf.Clamp(resolvedDiameter * 0.5f, minRadius, maxRadius),
                StartHeight = startHeight,
                EndHeight = startHeight + height,
                GeneratedHeight = height,
            };
        }

        private static float GetRuntimeGeneratedHeight(Data_Fairing fairing, float startHeight)
        {
            if (fairing.IsShroud)
            {
                return fairing.CrossSectionHeightMax;
            }
            return Mathf.Max(fairing.CrossSectionHeightMin, fairing.Length.GetValue() + startHeight);
        }

        private static float GetRuntimeStartHeight(Data_Fairing fairing, Transform modelTransform)
        {
            var localUpSign = Mathf.Sign(fairing.LocalUpAxis.x) * Mathf.Sign(fairing.LocalUpAxis.y) *
                Mathf.Sign(fairing.LocalUpAxis.z);
            var startHeight = fairing.FairingStartHeight * localUpSign;
            if (modelTransform == null)
            {
                return startHeight;
            }
            var modelTransformSign = Mathf.Sign(modelTransform.localPosition.x) *
                Mathf.Sign(modelTransform.localPosition.y) *
                Mathf.Sign(modelTransform.localPosition.z);
            return startHeight - Vector3.Scale(modelTransform.localPosition, fairing.LocalUpAxis).magnitude *
                localUpSign * modelTransformSign;
        }

        internal static string FormatMeters(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture) + " m";
        }

        internal static Transform GetPreviewModelTransform(Transform partTransform)
        {
            return partTransform == null ? null : FindChildRecursive(partTransform, "model");
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent.name == childName)
            {
                return parent;
            }
            foreach (Transform child in parent)
            {
                var match = FindChildRecursive(child, childName);
                if (match != null)
                {
                    return match;
                }
            }
            return null;
        }

        private static void DrawFrustum(
            Transform parentTransform,
            Data_Fairing fairing,
            ShroudPreviewMetrics metrics)
        {
            var axis = fairing.LocalUpAxis.sqrMagnitude > 0.0001f
                ? fairing.LocalUpAxis.normalized
                : Vector3.up;
            var radial = Vector3.ProjectOnPlane(Vector3.forward, axis);
            if (radial.sqrMagnitude < 0.0001f)
            {
                radial = Vector3.ProjectOnPlane(Vector3.right, axis);
            }
            radial.Normalize();
            var tangent = Vector3.Cross(axis, radial).normalized;

            const int SegmentCount = 48;
            var baseRing = new Vector3[SegmentCount + 1];
            var targetRing = new Vector3[SegmentCount + 1];
            for (var i = 0; i <= SegmentCount; i++)
            {
                var radians = Mathf.PI * 2f * i / SegmentCount;
                var circleDirection = radial * Mathf.Cos(radians) + tangent * Mathf.Sin(radians);
                baseRing[i] = TransformPoint(
                    parentTransform, fairing.Pivot, axis, circleDirection, metrics.StartHeight, metrics.BaseRadius);
                targetRing[i] = TransformPoint(
                    parentTransform, fairing.Pivot, axis, circleDirection, metrics.EndHeight, metrics.TargetRadius);
            }

            var previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;
            Handles.color = PreviewFillColor;
            for (var i = 0; i < SegmentCount; i++)
            {
                Handles.DrawAAConvexPolygon(baseRing[i], baseRing[i + 1], targetRing[i + 1], targetRing[i]);
            }
            Handles.color = PreviewLineColor;
            Handles.DrawAAPolyLine(2f, baseRing);
            Handles.DrawAAPolyLine(2f, targetRing);
            for (var i = 0; i < SegmentCount; i += SegmentCount / 8)
            {
                Handles.DrawAAPolyLine(2f, baseRing[i], targetRing[i]);
            }
            Handles.zTest = previousZTest;
        }

        private static Vector3 TransformPoint(
            Transform parentTransform,
            Vector3 pivot,
            Vector3 axis,
            Vector3 circleDirection,
            float height,
            float radius)
        {
            return parentTransform.TransformPoint(pivot + axis * height + circleDirection * radius);
        }
    }
}
