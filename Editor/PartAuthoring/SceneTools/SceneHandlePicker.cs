using System;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.SceneTools
{
    /// <summary>
    /// Selects which inspector vector field is currently being edited via a SceneView handle.
    /// </summary>
    /// <remarks>
    /// One active session at a time. Engaging a new field disengages whatever was previously active.
    /// While engaged, hooks <see cref="SceneView.duringSceneGui" /> and draws a
    /// <see cref="Handles.PositionHandle" /> or <see cref="Handles.RotationHandle" /> at the field's
    /// world position, writing changes back through a freshly-resolved <see cref="SerializedProperty" />.
    /// The picker stores property paths rather than property references because the inspector's
    /// SerializedObject can be disposed across rebuilds and we'd otherwise dangle.
    ///
    /// The target is a <see cref="Component" /> so the picker works for properties on either the
    /// part's <c>CorePartData</c> or on any <c>Module_*</c> that lives on the same GameObject.
    /// World-space conversions go through <c>target.gameObject.transform</c> to avoid the KSP
    /// shadow of <c>Component.transform</c> on PartBehaviourModule subclasses.
    /// </remarks>
    public static class SceneHandlePicker
    {
        /// <summary>
        /// Kind of handle to draw and edit.
        /// </summary>
        public enum HandleMode
        {
            /// <summary>Translate-only handle. The field is the part-local position.</summary>
            Position,
            /// <summary>Rotate-only handle. The field is a unit-length direction (forward vector).</summary>
            Orientation,
        }

        /// <summary>
        /// Fires whenever <see cref="ActivePath" /> changes (including null transitions on disengage).
        /// </summary>
        public static event Action OnActiveChanged;

        private static Component _target;
        private static string _primaryPath;
        private static string _anchorPath;
        private static HandleMode _mode;
        private static string _activePath;
        private static bool _hooked;

        /// <summary>
        /// Gets the property path of the currently-engaged field, or null if none.
        /// </summary>
        public static string ActivePath => _activePath;

        /// <summary>
        /// Engages a handle session on <paramref name="primary" />.
        /// </summary>
        /// <param name="target">The Component the field lives on. Provides the SerializedObject and Undo target.</param>
        /// <param name="primary">The vector SerializedProperty being edited.</param>
        /// <param name="mode">Position or Orientation handle.</param>
        /// <param name="anchor">Anchor position for Orientation handles. Ignored for Position handles.</param>
        public static void Engage(Component target, SerializedProperty primary, HandleMode mode, SerializedProperty anchor = null)
        {
            if (target == null || primary == null)
            {
                return;
            }
            if (_activePath == primary.propertyPath && _target == target)
            {
                Disengage();
                return;
            }
            _target = target;
            _primaryPath = primary.propertyPath;
            _anchorPath = anchor?.propertyPath;
            _mode = mode;
            _activePath = primary.propertyPath;
            EnsureHooked();
            SceneView.RepaintAll();
            OnActiveChanged?.Invoke();
        }

        /// <summary>
        /// Disengages the active handle session, if any.
        /// </summary>
        public static void Disengage()
        {
            if (_activePath == null)
            {
                return;
            }
            _target = null;
            _primaryPath = null;
            _anchorPath = null;
            _activePath = null;
            UnhookIfNeeded();
            SceneView.RepaintAll();
            OnActiveChanged?.Invoke();
        }

        private static void EnsureHooked()
        {
            if (_hooked)
            {
                return;
            }
            SceneView.duringSceneGui += OnSceneGui;
            _hooked = true;
        }

        private static void UnhookIfNeeded()
        {
            if (!_hooked)
            {
                return;
            }
            SceneView.duringSceneGui -= OnSceneGui;
            _hooked = false;
        }

        private static void OnSceneGui(SceneView view)
        {
            if (_target == null || string.IsNullOrEmpty(_primaryPath))
            {
                Disengage();
                return;
            }

            using var so = new SerializedObject(_target);
            var primary = so.FindProperty(_primaryPath);
            if (primary == null || !IsVectorProperty(primary))
            {
                Disengage();
                return;
            }

            Transform partTransform = _target.gameObject.transform;
            Vector3 localValue = ReadVector3(primary);
            Vector3 worldPos = partTransform.TransformPoint(localValue);

            if (_mode == HandleMode.Position)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newWorld = Handles.PositionHandle(worldPos, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 newLocal = partTransform.InverseTransformPoint(newWorld);
                    Undo.RecordObject(_target, "Edit " + primary.displayName);
                    WriteVector3(primary, newLocal);
                    so.ApplyModifiedProperties();
                }
                return;
            }

            SerializedProperty anchor = !string.IsNullOrEmpty(_anchorPath) ? so.FindProperty(_anchorPath) : null;
            Vector3 anchorLocal = anchor != null && IsVectorProperty(anchor) ? ReadVector3(anchor) : Vector3.zero;
            Vector3 anchorWorld = partTransform.TransformPoint(anchorLocal);

            Vector3 currentWorldDir = partTransform.TransformDirection(localValue);
            if (currentWorldDir.sqrMagnitude < 1e-6f)
            {
                currentWorldDir = partTransform.forward;
            }
            Quaternion currentRotation = Quaternion.LookRotation(currentWorldDir.normalized, partTransform.up);

            EditorGUI.BeginChangeCheck();
            Quaternion newRotation = Handles.RotationHandle(currentRotation, anchorWorld);
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 newWorldDir = newRotation * Vector3.forward;
                Vector3 newLocalDir = partTransform.InverseTransformDirection(newWorldDir);
                Undo.RecordObject(_target, "Edit " + primary.displayName);
                WriteVector3(primary, newLocalDir);
                so.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// Returns true when <paramref name="prop" /> is a Vector3 SerializedProperty or a Generic
        /// SerializedProperty with x/y/z children (Vector3d and similar).
        /// </summary>
        public static bool IsVectorProperty(SerializedProperty prop)
        {
            if (prop == null) return false;
            if (prop.propertyType == SerializedPropertyType.Vector3) return true;
            return prop.FindPropertyRelative("x") != null
                && prop.FindPropertyRelative("y") != null
                && prop.FindPropertyRelative("z") != null;
        }

        private static Vector3 ReadVector3(SerializedProperty prop)
        {
            if (prop.propertyType == SerializedPropertyType.Vector3)
            {
                return prop.vector3Value;
            }
            var x = prop.FindPropertyRelative("x");
            var y = prop.FindPropertyRelative("y");
            var z = prop.FindPropertyRelative("z");
            return new Vector3(
                x != null ? x.floatValue : 0f,
                y != null ? y.floatValue : 0f,
                z != null ? z.floatValue : 0f);
        }

        private static void WriteVector3(SerializedProperty prop, Vector3 value)
        {
            if (prop.propertyType == SerializedPropertyType.Vector3)
            {
                prop.vector3Value = value;
                return;
            }
            var x = prop.FindPropertyRelative("x");
            var y = prop.FindPropertyRelative("y");
            var z = prop.FindPropertyRelative("z");
            if (x != null) x.doubleValue = value.x;
            if (y != null) y.doubleValue = value.y;
            if (z != null) z.doubleValue = value.z;
        }
    }
}
