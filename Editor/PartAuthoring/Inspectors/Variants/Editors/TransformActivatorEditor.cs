using System;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants.Editors
{
    /// <summary>
    /// Custom editor for <see cref="TransformActivator" />. Renders the <c>Transforms</c> list as
    /// per-row <see cref="TransformPathField" /> entries with a Ping button and a Remove button,
    /// plus a trailing Add button.
    /// </summary>
    /// <remarks>
    /// The author drags a Transform from the prefab hierarchy into each row's ObjectField.
    /// <see cref="TransformPathField" /> resolves the relative path and writes the string back to the
    /// serialized list entry. Author identity is the path; the Ping button selects the resolved
    /// Transform in the scene to confirm the binding.
    /// </remarks>
    [TransformerEditor(typeof(TransformActivator))]
    public sealed class TransformActivatorEditor : ITransformerEditor
    {
        /// <inheritdoc />
        public VisualElement Build(ITransformer transformer, SerializedProperty transformerProp, TransformerEditorContext context)
        {
            var outer = new VisualElement();
            outer.AddToClassList("data-editor-section");

            var transformsProp = transformerProp?.FindPropertyRelative("Transforms");
            if (transformsProp == null)
            {
                outer.Add(new HelpBox("Transforms array not found on this transformer.", HelpBoxMessageType.Error));
                return outer;
            }

            var partRoot = context?.Module != null ? context.Module.gameObject.transform : null;

            outer.Add(InlineListBlock.Build(
                transformsProp,
                titleFormat: "Transforms ({0})",
                addButtonText: "+ Add",
                emptyHint: "(no transforms - add one to activate when this variant is selected)",
                rowBuilder: (entry, index, onDelete) => BuildRow(entry, onDelete, partRoot, context),
                onAdd: entry =>
                {
                    entry.stringValue = string.Empty;
                    context?.MarkDirty?.Invoke();
                }));

            return outer;
        }

        private static VisualElement BuildRow(SerializedProperty entry, Action onDelete, Transform partRoot, TransformerEditorContext context)
        {
            var row = new VisualElement();
            row.AddToClassList("data-editor-inline-row");

            var pathField = new TransformPathField(entry, label: null, partRoot);
            pathField.AddToClassList("data-editor-inline-row__grow");
            row.Add(pathField);

            var pingBtn = new Button(() => PingResolvedTransform(entry.stringValue, partRoot))
            {
                text = "Ping",
                tooltip = "Select the resolved Transform in the SceneView.",
            };
            pingBtn.AddToClassList("data-editor-inline-row__btn");
            pingBtn.SetEnabled(!string.IsNullOrEmpty(entry.stringValue) && ResolveTransform(entry.stringValue, partRoot) != null);
            row.Add(pingBtn);

            var removeBtn = new Button(() =>
            {
                onDelete();
                context?.MarkDirty?.Invoke();
            })
            {
                text = "X",
                tooltip = "Remove this entry.",
            };
            removeBtn.AddToClassList("data-editor-card-remove-btn");
            row.Add(removeBtn);

            return row;
        }

        private static Transform ResolveTransform(string path, Transform partRoot)
        {
            if (string.IsNullOrEmpty(path) || partRoot == null) return null;
            return partRoot.Find(path);
        }

        private static void PingResolvedTransform(string path, Transform partRoot)
        {
            var resolved = ResolveTransform(path, partRoot);
            if (resolved == null) return;
            EditorGUIUtility.PingObject(resolved.gameObject);
            Selection.activeGameObject = resolved.gameObject;
        }
    }
}
