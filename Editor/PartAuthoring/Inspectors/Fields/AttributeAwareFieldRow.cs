using System;
using System.Reflection;
using KSP;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using Ksp2UnityTools.Editor.PartAuthoring.SceneTools;
using Redux.Modules.Attributes;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Fields
{
    /// <summary>
    /// Attribute-aware field-row builder shared by <see cref="ReflectionModuleEditor" /> and
    /// <see cref="Variants.ReflectionTransformerEditor" />. Reads decoration on <see cref="FieldInfo" />
    /// and dispatches to the matching specialized widget, then wraps the result in a unit row when
    /// <see cref="UnitAttribute" /> is present.
    /// </summary>
    /// <remarks>
    /// Resolution order:
    /// (1) <see cref="FieldRendererRegistry" /> hits (array-element or direct) — these are full
    /// rows that already render their own header chrome.
    /// (2) Per-attribute specialized widgets: TransformGroup / TransformName / TransformPath /
    /// ResourceName / ExperimentName / AttachNodeId / SceneViewHandle / InlineStringList / SteppedRange.
    /// (3) <see cref="PropertyField" /> with aligned-label class.
    /// (4) Wrap in unit-row when <see cref="UnitAttribute" /> is present.
    /// </remarks>
    internal static class AttributeAwareFieldRow
    {
        public static VisualElement Build(
            SerializedProperty prop,
            FieldInfo field,
            Transform partRoot,
            string labelOverride = null)
        {
            var label = labelOverride ?? prop.displayName;

            if (TryGetArrayElementType(field.FieldType, out var arrayElementType) &&
                FieldRendererRegistry.TryCreate(arrayElementType, FieldRendererKind.ArrayElement, out var arrayRenderer))
            {
                return arrayRenderer.Build(prop, label);
            }
            if (FieldRendererRegistry.TryCreate(field.FieldType, FieldRendererKind.Direct, out var directRenderer))
            {
                return directRenderer.Build(prop, label);
            }

            var stepped = field.GetCustomAttribute<SteppedRangeAttribute>();
            var unit = field.GetCustomAttribute<UnitAttribute>();
            var resourceName = field.GetCustomAttribute<ResourceNameAttribute>();
            var experimentName = field.GetCustomAttribute<ExperimentNameAttribute>();
            var attachNodeId = field.GetCustomAttribute<AttachNodeIdAttribute>();
            var sceneViewHandle = field.GetCustomAttribute<SceneViewHandleAttribute>();
            var inlineStringList = field.GetCustomAttribute<InlineStringListAttribute>();
            var transformGroup = field.GetCustomAttribute<TransformGroupAttribute>();
            var transformName = field.GetCustomAttribute<TransformNameAttribute>();
            var transformPath = field.GetCustomAttribute<TransformPathAttribute>();

            VisualElement fieldElement;
            if (transformGroup != null && prop.propertyType == SerializedPropertyType.String)
            {
                fieldElement = new TransformGroupField(prop, label, partRoot);
            }
            else if (transformName != null && prop.propertyType == SerializedPropertyType.String)
            {
                fieldElement = new TransformNameField(prop, label, partRoot);
            }
            else if (transformPath != null && prop.propertyType == SerializedPropertyType.String)
            {
                fieldElement = new TransformPathField(prop, label, partRoot);
            }
            else if (resourceName != null && prop.propertyType == SerializedPropertyType.String)
            {
                fieldElement = new ResourceNameField(prop, label);
            }
            else if (experimentName != null && prop.propertyType == SerializedPropertyType.String)
            {
                fieldElement = new ExperimentNameField(prop, label);
            }
            else if (attachNodeId != null && prop.propertyType == SerializedPropertyType.String)
            {
                var corePartData = partRoot == null ? null : partRoot.GetComponent<CorePartData>();
                fieldElement = new AttachNodeIdField(prop, label, corePartData);
            }
            else if (sceneViewHandle != null && SceneHandlePicker.IsVectorProperty(prop))
            {
                var target = prop.serializedObject.targetObject as Component;
                var pickerMode = sceneViewHandle.Mode == SceneViewHandleMode.Orientation
                    ? SceneHandlePicker.HandleMode.Orientation
                    : SceneHandlePicker.HandleMode.Position;
                fieldElement = new VectorHandleField(prop, target, pickerMode);
            }
            else if (inlineStringList != null
                && TryGetArrayElementType(field.FieldType, out var inlineStringElementType)
                && inlineStringElementType == typeof(string))
            {
                fieldElement = InlineStringListBlock.Build(prop, label);
            }
            else if (stepped != null && prop.propertyType == SerializedPropertyType.Float)
            {
                fieldElement = BuildSteppedSlider(prop, stepped, label);
            }
            else
            {
                fieldElement = new PropertyField(prop, label);
                fieldElement.AddToClassList("unity-base-field__aligned");
            }

            if (unit == null)
            {
                return fieldElement;
            }

            var row = new VisualElement();
            row.AddToClassList("reflection-module-editor__unit-row");

            fieldElement.AddToClassList("reflection-module-editor__unit-row-field");
            row.Add(fieldElement);

            var suffix = new Label(unit.Suffix);
            suffix.AddToClassList("reflection-module-editor__unit-suffix");
            row.Add(suffix);

            return row;
        }

        public static bool TryGetArrayElementType(Type type, out Type elementType)
        {
            elementType = null;
            if (type == null)
            {
                return false;
            }
            if (type.IsArray)
            {
                elementType = type.GetElementType();
                return elementType != null;
            }
            if (type.IsGenericType)
            {
                var args = type.GetGenericArguments();
                if (args.Length == 1)
                {
                    elementType = args[0];
                    return true;
                }
            }
            return false;
        }

        private static VisualElement BuildSteppedSlider(SerializedProperty prop, SteppedRangeAttribute stepped, string label = null)
        {
            var slider = new Slider(label ?? prop.displayName, stepped.min, stepped.max)
            {
                showInputField = true,
                tooltip = prop.tooltip,
            };
            slider.AddToClassList("unity-base-field__aligned");
            slider.SetValueWithoutNotify(prop.floatValue);

            slider.RegisterValueChangedCallback(evt =>
            {
                var snapped = Mathf.Clamp(
                    Mathf.Round(evt.newValue / stepped.step) * stepped.step,
                    stepped.min,
                    stepped.max);
                if (!Mathf.Approximately(snapped, prop.floatValue))
                {
                    prop.serializedObject.Update();
                    prop.floatValue = snapped;
                    prop.serializedObject.ApplyModifiedProperties();
                }
                if (!Mathf.Approximately(snapped, evt.newValue))
                {
                    slider.SetValueWithoutNotify(snapped);
                }
            });

            slider.TrackPropertyValue(prop, p =>
            {
                if (!Mathf.Approximately(slider.value, p.floatValue))
                {
                    slider.SetValueWithoutNotify(p.floatValue);
                }
            });

            return slider;
        }
    }
}
