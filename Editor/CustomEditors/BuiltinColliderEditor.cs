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
            Type builtinEditorType = typeof(UnityEditor.Editor).Assembly.GetType(BuiltinEditorTypeName);
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
                        "Unity did not expose the internal collider gizmo controls in this editor version.",
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
                        "Controls Unity's built-in Scene View wireframes for all collider component types."
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
                    "This is a Unity Scene View gizmo setting and applies to all built-in collider components.",
                    MessageType.None
                );
            }
        }
    }

    internal static class BuiltinColliderWireframeUtility
    {
        private static readonly Type[] ColliderTypes =
        {
            typeof(BoxCollider),
            typeof(SphereCollider),
            typeof(CapsuleCollider),
            typeof(MeshCollider),
            typeof(WheelCollider),
            typeof(TerrainCollider)
        };

        private static readonly Type AnnotationUtilityType =
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AnnotationUtility");

        private static readonly MethodInfo GetAnnotationMethod =
            AnnotationUtilityType?.GetMethod(
                "GetAnnotation",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );

        private static readonly MethodInfo SetGizmoEnabledMethod =
            FindAnnotationMethod("SetGizmoEnabled", 3);

        private static readonly MethodInfo SetGizmosDirtyMethod =
            AnnotationUtilityType?.GetMethod(
                "SetGizmosDirty",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
            );

        public static bool IsAvailable => GetAnnotationMethod != null && SetGizmoEnabledMethod != null;

        public static bool GetAllWireframesEnabled()
        {
            foreach (Type colliderType in ColliderTypes)
            {
                if (!GetWireframeEnabled(colliderType))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool HasMixedWireframeState()
        {
            bool firstValue = GetWireframeEnabled(ColliderTypes[0]);
            for (int i = 1; i < ColliderTypes.Length; i++)
            {
                if (GetWireframeEnabled(ColliderTypes[i]) != firstValue)
                {
                    return true;
                }
            }

            return false;
        }

        public static void SetAllWireframesEnabled(bool enabled)
        {
            foreach (Type colliderType in ColliderTypes)
            {
                SetWireframeEnabled(colliderType, enabled);
            }
        }

        public static bool GetWireframeEnabled(Type colliderType)
        {
            if (!TryGetAnnotation(colliderType, out int classId, out string scriptClass, out object annotation))
            {
                return true;
            }

            Type annotationType = annotation.GetType();
            object value = annotationType.GetField("gizmoEnabled")?.GetValue(annotation);
            return value switch
            {
                int intValue => intValue != 0,
                bool boolValue => boolValue,
                _ => true
            };
        }

        public static void SetWireframeEnabled(Type colliderType, bool enabled)
        {
            if (SetGizmoEnabledMethod == null ||
                !TryGetAnnotation(colliderType, out int classId, out string scriptClass, out _))
            {
                return;
            }

            InvokeAnnotationMethod(SetGizmoEnabledMethod, classId, scriptClass, enabled);
            SetGizmosDirtyMethod?.Invoke(null, null);
        }

        private static MethodInfo FindAnnotationMethod(string methodName, int minimumParameterCount)
        {
            if (AnnotationUtilityType == null)
            {
                return null;
            }

            foreach (MethodInfo method in AnnotationUtilityType.GetMethods(
                         BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
                     ))
            {
                if (method.Name != methodName)
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length >= minimumParameterCount &&
                    parameters[0].ParameterType == typeof(int) &&
                    parameters[1].ParameterType == typeof(string))
                {
                    return method;
                }
            }

            return null;
        }

        private static bool TryGetAnnotation(
            Type componentType,
            out int classId,
            out string scriptClass,
            out object annotation
        )
        {
            classId = GetClassId(componentType);
            scriptClass = string.Empty;
            annotation = null;

            if (classId == 0 || GetAnnotationMethod == null)
            {
                return false;
            }

            annotation = GetAnnotationMethod.Invoke(null, new object[] { classId, scriptClass });
            return annotation != null;
        }

        private static int GetClassId(Type componentType)
        {
            if (componentType == typeof(MeshCollider))
            {
                return 64;
            }

            if (componentType == typeof(BoxCollider))
            {
                return 65;
            }

            if (componentType == typeof(SphereCollider))
            {
                return 135;
            }

            if (componentType == typeof(CapsuleCollider))
            {
                return 136;
            }

            if (componentType == typeof(WheelCollider))
            {
                return 146;
            }

            if (componentType == typeof(TerrainCollider))
            {
                return 154;
            }

            return 0;
        }

        private static object InvokeAnnotationMethod(
            MethodInfo method,
            int classId,
            string scriptClass,
            bool enabled
        )
        {
            ParameterInfo[] parameters = method.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = classId;
            arguments[1] = scriptClass;
            if (arguments.Length > 2)
            {
                arguments[2] = enabled ? 1 : 0;
            }

            for (int i = 3; i < arguments.Length; i++)
            {
                arguments[i] = parameters[i].ParameterType == typeof(bool) ? true : Type.Missing;
            }

            return method.Invoke(null, arguments);
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
