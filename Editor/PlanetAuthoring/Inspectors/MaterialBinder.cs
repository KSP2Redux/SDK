using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Fields;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Binds plain UI Toolkit <see cref="BaseField{T}" /> widgets to <see cref="Material" /> shader properties.
    /// </summary>
    /// <remarks>
    /// Each <c>Bind*</c> method takes an already-constructed field, seeds its current value from the material,
    /// and registers a value-changed callback that records an undo, writes the property, marks the material
    /// dirty, and repaints the scene view. Callers stay decoupled from the widget types: they construct the
    /// field once and call the matching <c>Bind*</c> to wire it to a material.
    /// </remarks>
    public static class MaterialBinder
    {
        /// <summary>Binds an <see cref="ObjectField" /> to a material texture property.</summary>
        public static void BindTexture(ObjectField field, Material material, string propertyName)
        {
            if (material != null)
                field.SetValueWithoutNotify(material.GetTexture(propertyName));
            field.RegisterValueChangedCallback(evt =>
            {
                if (material == null)
                    return;
                Undo.RecordObject(material, $"Edit {field.label}");
                material.SetTexture(propertyName, evt.newValue as Texture);
                EditorUtility.SetDirty(material);
                SceneView.RepaintAll();
            });
        }

        /// <summary>Binds a <see cref="ColorField" /> to a material color property.</summary>
        public static void BindColor(ColorField field, Material material, string propertyName)
        {
            if (material != null)
                field.SetValueWithoutNotify(material.GetColor(propertyName));
            field.RegisterValueChangedCallback(evt =>
            {
                if (material == null)
                    return;
                Undo.RecordObject(material, $"Edit {field.label}");
                material.SetColor(propertyName, evt.newValue);
                EditorUtility.SetDirty(material);
                SceneView.RepaintAll();
            });
        }

        /// <summary>Binds a <see cref="FloatField" /> to a material float property.</summary>
        public static void BindFloat(FloatField field, Material material, string propertyName)
        {
            if (material != null)
                field.SetValueWithoutNotify(material.GetFloat(propertyName));
            field.RegisterValueChangedCallback(evt =>
            {
                if (material == null)
                    return;
                Undo.RecordObject(material, $"Edit {field.label}");
                material.SetFloat(propertyName, evt.newValue);
                EditorUtility.SetDirty(material);
                SceneView.RepaintAll();
            });
        }

        /// <summary>Binds a <see cref="Slider" /> to a material float property.</summary>
        public static void BindSliderFloat(Slider field, Material material, string propertyName)
        {
            if (material != null)
                field.SetValueWithoutNotify(material.GetFloat(propertyName));
            field.RegisterValueChangedCallback(evt =>
            {
                if (material == null)
                    return;
                Undo.RecordObject(material, $"Edit {field.label}");
                material.SetFloat(propertyName, evt.newValue);
                EditorUtility.SetDirty(material);
                SceneView.RepaintAll();
            });
        }

        /// <summary>Binds a <see cref="Vector4Field" /> to a material Vector4 property.</summary>
        public static void BindVector4(Vector4Field field, Material material, string propertyName)
        {
            if (material != null)
                field.SetValueWithoutNotify(material.GetVector(propertyName));
            field.RegisterValueChangedCallback(evt =>
            {
                if (material == null)
                    return;
                Undo.RecordObject(material, $"Edit {field.label}");
                material.SetVector(propertyName, evt.newValue);
                EditorUtility.SetDirty(material);
                SceneView.RepaintAll();
            });
        }

        /// <summary>Binds a <see cref="Vector4ChannelsField" /> to a material Vector4 property.</summary>
        public static void BindVector4(Vector4ChannelsField field, Material material, string propertyName)
        {
            if (material != null)
                field.SetValueWithoutNotify(material.GetVector(propertyName));
            field.RegisterValueChangedCallback(evt =>
            {
                if (material == null)
                    return;
                Undo.RecordObject(material, $"Edit {field.label}");
                material.SetVector(propertyName, evt.newValue);
                EditorUtility.SetDirty(material);
                SceneView.RepaintAll();
            });
        }

        /// <summary>Binds a <see cref="FadeCurveField" /> to a material Vector4 property.</summary>
        public static void BindFadeCurve(FadeCurveField field, Material material, string propertyName)
        {
            if (material != null)
                field.SetValueWithoutNotify(material.GetVector(propertyName));
            field.RegisterValueChangedCallback(evt =>
            {
                if (material == null)
                    return;
                Undo.RecordObject(material, $"Edit {field.label}");
                material.SetVector(propertyName, evt.newValue);
                EditorUtility.SetDirty(material);
                SceneView.RepaintAll();
            });
        }

        /// <summary>Binds a <see cref="TrapezoidWindowField" /> to a material Vector4 property.</summary>
        public static void BindTrapezoidWindow(TrapezoidWindowField field, Material material, string propertyName)
        {
            if (material != null)
                field.SetValueWithoutNotify(material.GetVector(propertyName));
            field.RegisterValueChangedCallback(evt =>
            {
                if (material == null)
                    return;
                Undo.RecordObject(material, $"Edit {field.label}");
                material.SetVector(propertyName, evt.newValue);
                EditorUtility.SetDirty(material);
                SceneView.RepaintAll();
            });
        }

        /// <summary>Binds a <see cref="Toggle" /> to a material shader keyword.</summary>
        public static void BindKeyword(Toggle field, Material material, string keywordName)
        {
            if (material != null)
                field.SetValueWithoutNotify(material.IsKeywordEnabled(keywordName));
            field.RegisterValueChangedCallback(evt =>
            {
                if (material == null)
                    return;
                Undo.RecordObject(material, $"Toggle {field.label}");
                if (evt.newValue)
                    material.EnableKeyword(keywordName);
                else
                    material.DisableKeyword(keywordName);
                EditorUtility.SetDirty(material);
                SceneView.RepaintAll();
            });
        }

        /// <summary>Binds a <see cref="FloatField" /> to a single channel of a material Vector4 property.</summary>
        public static void BindVector4Channel(FloatField field, Material material, string propertyName, int channelIndex)
        {
            if (material != null)
                field.SetValueWithoutNotify(material.GetVector(propertyName)[channelIndex]);
            field.RegisterValueChangedCallback(evt => WriteVector4Channel(material, propertyName, channelIndex, evt.newValue, field.label));
        }

        /// <summary>Binds an <see cref="IntegerField" /> to a single channel of a material Vector4 property.</summary>
        public static void BindVector4Channel(IntegerField field, Material material, string propertyName, int channelIndex)
        {
            if (material != null)
                field.SetValueWithoutNotify((int)material.GetVector(propertyName)[channelIndex]);
            field.RegisterValueChangedCallback(evt => WriteVector4Channel(material, propertyName, channelIndex, evt.newValue, field.label));
        }

        /// <summary>Binds a <see cref="Toggle" /> to a single channel of a material Vector4 property treated as boolean (non-zero = true).</summary>
        public static void BindVector4Channel(Toggle field, Material material, string propertyName, int channelIndex)
        {
            if (material != null)
                field.SetValueWithoutNotify(material.GetVector(propertyName)[channelIndex] > 0.5f);
            field.RegisterValueChangedCallback(evt => WriteVector4Channel(material, propertyName, channelIndex, evt.newValue ? 1f : 0f, field.label));
        }

        private static void WriteVector4Channel(Material material, string propertyName, int channelIndex, float value, string label)
        {
            if (material == null)
                return;
            Undo.RecordObject(material, $"Edit {label}");
            var v = material.GetVector(propertyName);
            v[channelIndex] = value;
            material.SetVector(propertyName, v);
            EditorUtility.SetDirty(material);
            SceneView.RepaintAll();
        }
    }
}
