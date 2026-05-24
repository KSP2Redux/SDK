using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Fields;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants
{
    /// <summary>
    /// Generic fallback editor for any <see cref="ITransformer" /> that does not have a paired <see cref="TransformerEditorAttribute" />-registered editor. Walks the transformer's public fields via reflection and renders each via <see cref="AttributeAwareFieldRow" />, which dispatches to the same attribute-aware widgets the module editor uses.
    /// </summary>
    /// <remarks>
    /// Drives off the <see cref="SerializedProperty" /> for the transformer's <c>[SerializeReference]</c> array entry. Inner fields are resolved via <see cref="SerializedProperty.FindPropertyRelative" />; complex types (<c>SerializedDictionary</c>, <c>JToken</c>) without a registered <see cref="FieldRendererRegistry" /> entry fall through to <see cref="UnityEditor.UIElements.PropertyField" />, which Unity may render as a placeholder or empty. Custom editors should be registered for transformers with such fields.
    /// </remarks>
    internal static class ReflectionTransformerEditor
    {
        private const BindingFlags FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static VisualElement Build(SerializedProperty transformerProp, TransformerEditorContext context)
        {
            var root = new VisualElement();
            if (transformerProp == null)
            {
                root.Add(new HelpBox("Transformer SerializedProperty is null.", HelpBoxMessageType.Error));
                return root;
            }
            object boxed = transformerProp.managedReferenceValue;
            if (boxed == null)
            {
                root.Add(new HelpBox("Transformer reference is null.", HelpBoxMessageType.Error));
                return root;
            }
            Type t = boxed.GetType();
            Transform partRoot = context?.Module != null ? context.Module.gameObject.transform : null;

            foreach (FieldInfo field in EnumerateAuthorFields(t))
            {
                SerializedProperty fieldProp = transformerProp.FindPropertyRelative(field.Name);
                if (fieldProp == null)
                {
                    continue;
                }
                root.Add(AttributeAwareFieldRow.Build(fieldProp, field, partRoot));
            }
            if (root.childCount == 0)
            {
                root.Add(new HelpBox("No configuration.", HelpBoxMessageType.Info));
            }
            root.Bind(transformerProp.serializedObject);
            return root;
        }

        private static IEnumerable<FieldInfo> EnumerateAuthorFields(Type type)
        {
            foreach (FieldInfo field in type.GetFields(FIELD_FLAGS))
            {
                if (!field.IsPublic)
                {
                    continue;
                }
                if (field.IsInitOnly)
                {
                    continue;
                }
                if (field.Name.StartsWith("_", StringComparison.Ordinal))
                {
                    continue;
                }
                if (field.IsDefined(typeof(JsonIgnoreAttribute), inherit: true))
                {
                    continue;
                }
                if (field.IsDefined(typeof(NonSerializedAttribute), inherit: true))
                {
                    continue;
                }
                yield return field;
            }
        }
    }
}
