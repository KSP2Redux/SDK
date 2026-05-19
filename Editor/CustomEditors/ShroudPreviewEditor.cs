using System;
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
    internal static class ShroudPreviewEditor
    {
        private sealed class ShroudPreviewSettings
        {
            public bool Foldout = true;
            public bool Enabled = true;
            public string TargetSizeKey;
        }

        private struct ShroudPreviewMetrics
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
        private static GUIContent[] _sizeOptions;

        public static void DrawInspector(Module_Fairing module)
        {
            Data_Fairing fairing = GetFairingData(module);
            if (module == null || fairing == null)
            {
                return;
            }

            ShroudPreviewSettings settings = GetOrCreateSettings(module, fairing);

            EditorGUILayout.Space();
            settings.Foldout = EditorGUILayout.Foldout(settings.Foldout, "Generated Shape Preview", true);
            if (!settings.Foldout)
            {
                return;
            }

            int originalIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();

            settings.Enabled = EditorGUILayout.Toggle("Show Scene Preview", settings.Enabled);
            settings.TargetSizeKey = DrawBuiltInSizePopup("Target Part Size", settings.TargetSizeKey);

            Transform moduleTransform = module.gameObject.transform;
            Transform modelTransform = GetPreviewModelTransform(moduleTransform);
            ShroudPreviewMetrics metrics = CalculateMetrics(fairing, settings.TargetSizeKey, modelTransform);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("Host Diameter", FormatMeters(metrics.HostDiameter));
                EditorGUILayout.TextField("Target Diameter", FormatMeters(metrics.TargetDiameter));
                EditorGUILayout.TextField("Preview Diameter", FormatMeters(metrics.ResolvedDiameter));
                EditorGUILayout.TextField("Generated Length", FormatMeters(Mathf.Abs(metrics.GeneratedHeight)));
                EditorGUILayout.TextField(
                    "Height Range",
                    $"{FormatMeters(metrics.StartHeight)} to {FormatMeters(metrics.EndHeight)}"
                );
            }

            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }

            EditorGUI.indentLevel = originalIndent;
        }

        public static void DrawGizmo(Module_Fairing module)
        {
            if (module == null ||
                !SettingsByModule.TryGetValue(module.GetInstanceID(), out ShroudPreviewSettings settings) ||
                !settings.Enabled)
            {
                return;
            }

            Data_Fairing fairing = GetFairingData(module);
            if (fairing == null)
            {
                return;
            }

            Transform moduleTransform = module.gameObject.transform;
            Transform modelTransform = GetPreviewModelTransform(moduleTransform);
            ShroudPreviewMetrics metrics = CalculateMetrics(fairing, settings.TargetSizeKey, modelTransform);
            DrawFrustum(modelTransform != null ? modelTransform : moduleTransform, fairing, metrics);
        }

        private static Data_Fairing GetFairingData(Module_Fairing module)
        {
            return module == null || FairingDataField == null
                ? null
                : FairingDataField.GetValue(module) as Data_Fairing;
        }

        private static ShroudPreviewSettings GetOrCreateSettings(Module_Fairing module, Data_Fairing fairing)
        {
            int instanceId = module.GetInstanceID();
            if (SettingsByModule.TryGetValue(instanceId, out ShroudPreviewSettings settings))
            {
                if (!PartSizeRegistry.IsValidKey(settings.TargetSizeKey))
                {
                    settings.TargetSizeKey = GetDefaultTargetKey(module, fairing);
                }

                return settings;
            }

            settings = new ShroudPreviewSettings
            {
                TargetSizeKey = GetDefaultTargetKey(module, fairing)
            };
            SettingsByModule[instanceId] = settings;
            return settings;
        }

        private static string GetDefaultTargetKey(Module_Fairing module, Data_Fairing fairing)
        {
            CorePartData coreData = module.GetComponent<CorePartData>() ?? module.GetComponentInParent<CorePartData>();
            string partSizeKey = coreData?.Data == null ? null : PartSizeRegistry.GetPartSizeKey(coreData.Data);
            return PartSizeRegistry.IsValidKey(partSizeKey)
                ? partSizeKey
                : GetSmallestSizeKeyAtLeast(fairing.BaseRadius * 2f);
        }

        private static string GetSmallestSizeKeyAtLeast(float diameter)
        {
            foreach (PartSizeDefinition definition in PartSizeRegistry.Definitions)
            {
                if (definition.Diameter >= diameter)
                {
                    return definition.Key;
                }
            }

            return PartSizeRegistry.GetLargest().Key;
        }

        private static string DrawBuiltInSizePopup(string label, string currentKey)
        {
            EnsureSizeOptions();
            IReadOnlyList<PartSizeDefinition> definitions = PartSizeRegistry.Definitions;
            int selectedIndex = 0;
            for (int i = 0; i < definitions.Count; i++)
            {
                if (string.Equals(definitions[i].Key, currentKey, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }

            int newIndex = EditorGUILayout.Popup(new GUIContent(label), selectedIndex, _sizeOptions);
            return definitions[Mathf.Clamp(newIndex, 0, definitions.Count - 1)].Key;
        }

        private static void EnsureSizeOptions()
        {
            if (_sizeOptions != null)
            {
                return;
            }

            IReadOnlyList<PartSizeDefinition> definitions = PartSizeRegistry.Definitions;
            _sizeOptions = new GUIContent[definitions.Count];
            for (int i = 0; i < definitions.Count; i++)
            {
                PartSizeDefinition definition = definitions[i];
                _sizeOptions[i] = new GUIContent(
                    definition.DisplayName + " (" +
                    definition.Diameter.ToString("0.####", CultureInfo.InvariantCulture) + " m)"
                );
            }
        }

        private static ShroudPreviewMetrics CalculateMetrics(
            Data_Fairing fairing,
            string targetSizeKey,
            Transform modelTransform
        )
        {
            float hostDiameter = Mathf.Max(0.001f, fairing.BaseRadius * 2f);
            float targetDiameter = PartSizeRegistry.Get(targetSizeKey).Diameter;
            float resolvedDiameter = targetDiameter >= hostDiameter ? targetDiameter : hostDiameter;

            if (fairing.MaxAutoFairingTargetRadius > 0 && resolvedDiameter > hostDiameter)
            {
                float maxDiameter = PartSizeRegistry.GetLegacyAttachNodeSize(fairing.MaxAutoFairingTargetRadius)
                    .Diameter;
                resolvedDiameter = Mathf.Max(hostDiameter, Mathf.Min(maxDiameter, resolvedDiameter));
            }
            else if (fairing.MinAutoFairingTargetRadius > 0 && resolvedDiameter < hostDiameter)
            {
                float minDiameter = PartSizeRegistry.GetLegacyAttachNodeSize(fairing.MinAutoFairingTargetRadius)
                    .Diameter;
                resolvedDiameter = Mathf.Max(minDiameter, resolvedDiameter);
            }

            resolvedDiameter = PartSizeRegistry.SnapDownToKnownDiameter(resolvedDiameter);
            float maxRadius = fairing.MaxRadius > 0f ? fairing.MaxRadius : resolvedDiameter * 0.5f;
            float minRadius = Mathf.Min(Mathf.Max(0f, fairing.CapRadius), maxRadius);
            float startHeight = GetRuntimeStartHeight(fairing, modelTransform);
            float height = GetRuntimeGeneratedHeight(fairing, startHeight);

            return new ShroudPreviewMetrics
            {
                HostDiameter = hostDiameter,
                TargetDiameter = targetDiameter,
                ResolvedDiameter = resolvedDiameter,
                BaseRadius = Mathf.Max(0f, fairing.BaseRadius),
                TargetRadius = Mathf.Clamp(resolvedDiameter * 0.5f, minRadius, maxRadius),
                StartHeight = startHeight,
                EndHeight = startHeight + height,
                GeneratedHeight = height
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
            float localUpSign = Mathf.Sign(fairing.LocalUpAxis.x) * Mathf.Sign(fairing.LocalUpAxis.y) *
                Mathf.Sign(fairing.LocalUpAxis.z);
            float startHeight = fairing.FairingStartHeight * localUpSign;
            if (modelTransform == null)
            {
                return startHeight;
            }

            float modelTransformSign = Mathf.Sign(modelTransform.localPosition.x) *
                Mathf.Sign(modelTransform.localPosition.y) *
                Mathf.Sign(modelTransform.localPosition.z);
            return startHeight - Vector3.Scale(modelTransform.localPosition, fairing.LocalUpAxis).magnitude *
                localUpSign * modelTransformSign;
        }

        private static string FormatMeters(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture) + " m";
        }

        private static Transform GetPreviewModelTransform(Transform partTransform)
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
                Transform match = FindChildRecursive(child, childName);
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
            ShroudPreviewMetrics metrics
        )
        {
            Vector3 axis = fairing.LocalUpAxis.sqrMagnitude > 0.0001f
                ? fairing.LocalUpAxis.normalized
                : Vector3.up;
            Vector3 radial = Vector3.ProjectOnPlane(Vector3.forward, axis);
            if (radial.sqrMagnitude < 0.0001f)
            {
                radial = Vector3.ProjectOnPlane(Vector3.right, axis);
            }

            radial.Normalize();
            Vector3 tangent = Vector3.Cross(axis, radial).normalized;

            const int SegmentCount = 48;
            var baseRing = new Vector3[SegmentCount + 1];
            var targetRing = new Vector3[SegmentCount + 1];
            for (int i = 0; i <= SegmentCount; i++)
            {
                float radians = Mathf.PI * 2f * i / SegmentCount;
                Vector3 circleDirection = radial * Mathf.Cos(radians) + tangent * Mathf.Sin(radians);
                baseRing[i] = TransformPoint(
                    parentTransform,
                    fairing.Pivot,
                    axis,
                    circleDirection,
                    metrics.StartHeight,
                    metrics.BaseRadius
                );
                targetRing[i] = TransformPoint(
                    parentTransform,
                    fairing.Pivot,
                    axis,
                    circleDirection,
                    metrics.EndHeight,
                    metrics.TargetRadius
                );
            }

            CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = CompareFunction.LessEqual;
            Handles.color = PreviewFillColor;
            for (int i = 0; i < SegmentCount; i++)
            {
                Handles.DrawAAConvexPolygon(baseRing[i], baseRing[i + 1], targetRing[i + 1], targetRing[i]);
            }

            Handles.color = PreviewLineColor;
            Handles.DrawAAPolyLine(2f, baseRing);
            Handles.DrawAAPolyLine(2f, targetRing);
            for (int i = 0; i < SegmentCount; i += SegmentCount / 8)
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
            float radius
        )
        {
            return parentTransform.TransformPoint(pivot + axis * height + circleDirection * radius);
        }
    }
}
