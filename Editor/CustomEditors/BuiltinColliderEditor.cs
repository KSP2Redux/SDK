using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.CustomEditors
{
    internal abstract class BuiltinColliderEditor<TCollider> : UnityEditor.Editor
        where TCollider : Collider
    {
        private UnityEditor.Editor _builtinEditor;

        protected abstract string BuiltinEditorTypeName { get; }

        private void OnEnable()
        {
            Type builtinEditorType = BuiltinColliderWireframeUtility.FindUnityEditorType(BuiltinEditorTypeName);
            if (builtinEditorType != null)
            {
                _builtinEditor = CreateEditor(targets, builtinEditorType);
            }
        }

        private void OnDisable()
        {
            if (_builtinEditor != null)
            {
                DestroyImmediate(_builtinEditor);
                _builtinEditor = null;
            }
        }

        public override void OnInspectorGUI()
        {
            if (_builtinEditor != null)
            {
                _builtinEditor.OnInspectorGUI();
            }
            else
            {
                DrawDefaultInspector();
            }

            DrawColliderWireframeToggle();
        }

        private void DrawColliderWireframeToggle()
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Scene Preview", EditorStyles.boldLabel);
                if (!BuiltinColliderWireframeUtility.IsAvailable)
                {
                    EditorGUILayout.HelpBox(
                        "Collider wireframe controls are not available in this editor version.",
                        MessageType.Info
                    );
                    return;
                }

                bool isEnabled = BuiltinColliderWireframeUtility.GetAllWireframesEnabled();
                bool mixedValue = BuiltinColliderWireframeUtility.HasMixedWireframeState();
                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue = mixedValue;
                bool newValue = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Show Collider Wireframes",
                        "Controls Unity's built-in Scene View gizmos for 3D collider component types."
                    ),
                    isEnabled
                );
                EditorGUI.showMixedValue = false;
                if (EditorGUI.EndChangeCheck())
                {
                    BuiltinColliderWireframeUtility.SetAllWireframesEnabled(newValue);
                    SceneView.RepaintAll();
                }

                EditorGUILayout.HelpBox(
                    "This toggles the built-in BoxCollider, CapsuleCollider, MeshCollider, SphereCollider, and WheelCollider gizmo rows.",
                    MessageType.None
                );
            }
        }
    }

    internal static class BuiltinColliderWireframeUtility
    {
        private const string BUILT_IN_SCRIPT_CLASS = "";

        private static readonly ColliderGizmoAnnotation[] ColliderAnnotations =
        {
            new(65, "BoxCollider"),
            new(136, "CapsuleCollider"),
            new(64, "MeshCollider"),
            new(135, "SphereCollider"),
            new(146, "WheelCollider")
        };

        private static readonly Type AnnotationUtilityType =
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AnnotationUtility");

        private static readonly MethodInfo GetAnnotationMethod =
            AnnotationUtilityType?.GetMethod(
                "GetAnnotation",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(string) },
                null
            );

        private static readonly MethodInfo GetAnnotationsMethod =
            AnnotationUtilityType?.GetMethod(
                "GetAnnotations",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null
            );

        private static readonly MethodInfo SetGizmoEnabledMethod =
            AnnotationUtilityType?.GetMethod(
                "SetGizmoEnabled",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(string), typeof(int), typeof(bool) },
                null
            );

        private static readonly MethodInfo SetGizmosDirtyMethod =
            AnnotationUtilityType?.GetMethod(
                "SetGizmosDirty",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );

        private static bool _annotationCacheInitialized;

        public static bool IsAvailable => GetAnnotationMethod != null &&
                                          SetGizmoEnabledMethod != null &&
                                          HasAnyValidColliderAnnotation();

        public static Type FindUnityEditorType(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        public static bool GetAllWireframesEnabled()
        {
            bool any = false;
            foreach (ColliderGizmoAnnotation colliderAnnotation in ColliderAnnotations)
            {
                if (!TryGetAnnotation(colliderAnnotation, out object annotation))
                {
                    continue;
                }

                any = true;
                if (!GetGizmoEnabled(annotation))
                {
                    return false;
                }
            }

            return any;
        }

        public static bool HasMixedWireframeState()
        {
            bool? firstValue = null;
            foreach (ColliderGizmoAnnotation colliderAnnotation in ColliderAnnotations)
            {
                if (!TryGetAnnotation(colliderAnnotation, out object annotation))
                {
                    continue;
                }

                bool current = GetGizmoEnabled(annotation);
                if (firstValue == null)
                {
                    firstValue = current;
                    continue;
                }

                if (current != firstValue.Value)
                {
                    return true;
                }
            }

            return false;
        }

        public static void SetAllWireframesEnabled(bool enabled)
        {
            if (!IsAvailable)
            {
                return;
            }

            foreach (ColliderGizmoAnnotation colliderAnnotation in ColliderAnnotations)
            {
                if (TryGetAnnotation(colliderAnnotation, out _))
                {
                    SetGizmoEnabled(colliderAnnotation, enabled);
                }
            }

            SetGizmosDirtyMethod?.Invoke(null, null);
            SceneView.RepaintAll();
        }

        private static bool HasAnyValidColliderAnnotation()
        {
            foreach (ColliderGizmoAnnotation colliderAnnotation in ColliderAnnotations)
            {
                if (TryGetAnnotation(colliderAnnotation, out _))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetAnnotation(ColliderGizmoAnnotation colliderAnnotation, out object annotation)
        {
            annotation = null;

            if (GetAnnotationMethod == null)
            {
                return false;
            }

            EnsureAnnotationCacheInitialized();
            annotation = GetAnnotationMethod.Invoke(
                null,
                new object[] { colliderAnnotation.ClassId, BUILT_IN_SCRIPT_CLASS }
            );
            return IsValidAnnotation(annotation, colliderAnnotation.ClassId);
        }

        private static void EnsureAnnotationCacheInitialized()
        {
            if (_annotationCacheInitialized)
            {
                return;
            }

            try
            {
                GetAnnotationsMethod?.Invoke(null, null);
            }
            catch (TargetInvocationException)
            {
            }

            _annotationCacheInitialized = true;
        }

        private static bool IsValidAnnotation(object annotation, int expectedClassId)
        {
            if (annotation == null)
            {
                return false;
            }

            object annotationClassId = annotation.GetType().GetField("classID")?.GetValue(annotation);
            return annotationClassId is int intClassId && intClassId == expectedClassId;
        }

        private static bool GetGizmoEnabled(object annotation)
        {
            object value = annotation.GetType().GetField("gizmoEnabled")?.GetValue(annotation);
            return value switch
            {
                int intValue => intValue != 0,
                bool boolValue => boolValue,
                _ => true
            };
        }

        private static void SetGizmoEnabled(ColliderGizmoAnnotation colliderAnnotation, bool enabled)
        {
            try
            {
                SetGizmoEnabledMethod?.Invoke(
                    null,
                    new object[]
                    {
                        colliderAnnotation.ClassId,
                        BUILT_IN_SCRIPT_CLASS,
                        enabled ? 1 : 0,
                        true
                    }
                );
            }
            catch (TargetInvocationException)
            {
            }
        }

        private readonly struct ColliderGizmoAnnotation
        {
            public readonly int ClassId;
            public readonly string Name;

            public ColliderGizmoAnnotation(int classId, string name)
            {
                ClassId = classId;
                Name = name;
            }
        }
    }

    [CustomEditor(typeof(BoxCollider))]
    [CanEditMultipleObjects]
    internal sealed class BoxColliderEditor : BuiltinColliderEditor<BoxCollider>
    {
        protected override string BuiltinEditorTypeName => "UnityEditor.BoxColliderEditor";
    }

    [CustomEditor(typeof(SphereCollider))]
    [CanEditMultipleObjects]
    internal sealed class SphereColliderEditor : BuiltinColliderEditor<SphereCollider>
    {
        protected override string BuiltinEditorTypeName => "UnityEditor.SphereColliderEditor";
    }

    [CustomEditor(typeof(CapsuleCollider))]
    [CanEditMultipleObjects]
    internal sealed class CapsuleColliderEditor : BuiltinColliderEditor<CapsuleCollider>
    {
        protected override string BuiltinEditorTypeName => "UnityEditor.CapsuleColliderEditor";
    }

    [CustomEditor(typeof(MeshCollider))]
    [CanEditMultipleObjects]
    internal sealed class MeshColliderEditor : BuiltinColliderEditor<MeshCollider>
    {
        protected override string BuiltinEditorTypeName => "UnityEditor.MeshColliderEditor";
    }

    [CustomEditor(typeof(WheelCollider))]
    [CanEditMultipleObjects]
    internal sealed class WheelColliderEditor : BuiltinColliderEditor<WheelCollider>
    {
        protected override string BuiltinEditorTypeName => "UnityEditor.WheelColliderEditor";
    }

    [CustomEditor(typeof(TerrainCollider))]
    [CanEditMultipleObjects]
    internal sealed class TerrainColliderEditor : BuiltinColliderEditor<TerrainCollider>
    {
        protected override string BuiltinEditorTypeName => "UnityEditor.TerrainColliderEditor";
    }
}
