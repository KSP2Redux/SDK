using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Fields;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Binds plain UI Toolkit <see cref="BaseField{T}" /> widgets to <see cref="SerializedProperty" /> paths on a PQSData <see cref="SerializedObject" />.
    /// </summary>
    /// <remarks>
    /// Each <c>Bind*</c> method seeds the field's initial value from the property, then registers
    /// a value-changed callback that writes back via <c>SerializedObject.Update()</c> +
    /// <c>SerializedObject.ApplyModifiedProperties()</c>. The deliberately-manual write path
    /// avoids calling <c>BindingExtensions.BindProperty(...)</c>, which would register the field
    /// with the editor binding system and cause sibling <see cref="PropertyField" /> children of
    /// the same <see cref="SerializedObject" /> to rebuild their inner UI on every property
    /// change. The flicker that produces is visible during preview where the surface material
    /// updates frequently.
    /// </remarks>
    public static class PqsDataBinder
    {
        /// <summary>Binds a <see cref="Toggle" /> to a bool SerializedProperty.</summary>
        public static void BindBool(Toggle field, SerializedObject pqsDataSO, string path)
        {
            var prop = FindOrWarn(pqsDataSO, path, field.label);
            if (prop == null)
                return;
            field.SetValueWithoutNotify(prop.boolValue);
            field.RegisterValueChangedCallback(evt =>
            {
                pqsDataSO.Update();
                prop.boolValue = evt.newValue;
                pqsDataSO.ApplyModifiedProperties();
            });
        }

        /// <summary>Binds a <see cref="FloatField" /> to a float SerializedProperty.</summary>
        public static void BindFloat(FloatField field, SerializedObject pqsDataSO, string path)
        {
            var prop = FindOrWarn(pqsDataSO, path, field.label);
            if (prop == null)
                return;
            field.SetValueWithoutNotify(prop.floatValue);
            field.RegisterValueChangedCallback(evt =>
            {
                pqsDataSO.Update();
                prop.floatValue = evt.newValue;
                pqsDataSO.ApplyModifiedProperties();
            });
        }

        /// <summary>Binds an <see cref="IntegerField" /> to an int SerializedProperty.</summary>
        public static void BindInt(IntegerField field, SerializedObject pqsDataSO, string path)
        {
            var prop = FindOrWarn(pqsDataSO, path, field.label);
            if (prop == null)
                return;
            field.SetValueWithoutNotify(prop.intValue);
            field.RegisterValueChangedCallback(evt =>
            {
                pqsDataSO.Update();
                prop.intValue = evt.newValue;
                pqsDataSO.ApplyModifiedProperties();
            });
        }

        /// <summary>Binds an <see cref="ObjectField" /> to an object-reference SerializedProperty.</summary>
        public static void BindTexture(ObjectField field, SerializedObject pqsDataSO, string path)
        {
            var prop = FindOrWarn(pqsDataSO, path, field.label);
            if (prop == null)
                return;
            field.SetValueWithoutNotify(prop.objectReferenceValue);
            field.RegisterValueChangedCallback(evt =>
            {
                pqsDataSO.Update();
                prop.objectReferenceValue = evt.newValue;
                pqsDataSO.ApplyModifiedProperties();
            });
        }

        /// <summary>Binds a <see cref="Vector4ChannelsField" /> to a Vector4 SerializedProperty.</summary>
        public static void BindVector4(Vector4ChannelsField field, SerializedObject pqsDataSO, string path)
        {
            var prop = FindOrWarn(pqsDataSO, path, field.label);
            if (prop == null)
                return;
            field.SetValueWithoutNotify(prop.vector4Value);
            field.RegisterValueChangedCallback(evt =>
            {
                pqsDataSO.Update();
                prop.vector4Value = evt.newValue;
                pqsDataSO.ApplyModifiedProperties();
            });
        }

        /// <summary>
        /// Binds a single channel of a Vector4 SerializedProperty to an <see cref="IntegerField" />.
        /// </summary>
        /// <remarks>
        /// Mirrors the material-side <see cref="MaterialBinder.BindVector4Channel(IntegerField, Material, string, int)" />
        /// when a value is packed into one component of a serialized Vector4 (e.g. per-biome UV scale ints
        /// packed into a single Vector4 on PQSData).
        /// </remarks>
        public static void BindVector4Channel(IntegerField field, SerializedObject pqsDataSO, string path, int channelIndex)
        {
            var prop = FindOrWarn(pqsDataSO, path, field.label);
            if (prop == null)
                return;
            field.SetValueWithoutNotify((int)prop.vector4Value[channelIndex]);
            field.RegisterValueChangedCallback(evt =>
            {
                pqsDataSO.Update();
                var v = prop.vector4Value;
                v[channelIndex] = evt.newValue;
                prop.vector4Value = v;
                pqsDataSO.ApplyModifiedProperties();
            });
        }

        private static SerializedProperty FindOrWarn(SerializedObject pqsDataSO, string path, string contextLabel)
        {
            var prop = pqsDataSO?.FindProperty(path);
            if (prop == null)
                Debug.LogWarning($"[PqsDataBinder] '{path}' did not resolve on {pqsDataSO?.targetObject?.name} (field: {contextLabel}). The binding will be inert.");
            return prop;
        }
    }
}
