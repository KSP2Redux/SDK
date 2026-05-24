using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants.Editors
{
    /// <summary>
    /// Custom editor for <see cref="TransformActivator" />. Renders the <c>Transforms</c> list as per-row <see cref="TransformPathField" /> entries with a Ping button and a Remove button, plus a trailing Add button.
    /// </summary>
    /// <remarks>
    /// The author drags a Transform from the prefab hierarchy into each row's ObjectField. <see cref="TransformPathField" /> resolves the relative path and writes the string back to the serialized list entry. Author identity is the path; the Ping button selects the resolved Transform in the scene to confirm the binding.
    /// </remarks>
    [TransformerEditor(typeof(TransformActivator))]
    public sealed class TransformActivatorEditor : ITransformerEditor
    {
        public VisualElement Build(ITransformer transformer, SerializedProperty transformerProp, TransformerEditorContext context)
        {
            var outer = new VisualElement();
            outer.AddToClassList("data-editor-section");

            SerializedProperty transformsProp = transformerProp?.FindPropertyRelative("Transforms");
            if (transformsProp == null)
            {
                outer.Add(new HelpBox("Transforms array not found on this transformer.", HelpBoxMessageType.Error));
                return outer;
            }

            var headerRow = new VisualElement();
            headerRow.AddToClassList("data-editor-section-header-row");

            var countLabel = new Label();
            countLabel.AddToClassList("data-editor-section-header");
            headerRow.Add(countLabel);

            var addBtn = new Button { text = "+ Add" };
            headerRow.Add(addBtn);
            outer.Add(headerRow);

            var rowsContainer = new VisualElement();
            outer.Add(rowsContainer);

            Transform partRoot = context?.Module != null ? context.Module.gameObject.transform : null;

            void Refresh()
            {
                rowsContainer.Clear();
                countLabel.text = $"Transforms ({transformsProp.arraySize})";
                for (int i = 0; i < transformsProp.arraySize; i++)
                {
                    int captured = i;
                    SerializedProperty itemProp = transformsProp.GetArrayElementAtIndex(captured);

                    var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                    row.style.marginBottom = 2f;

                    var pathField = new TransformPathField(itemProp, label: null, partRoot);
                    pathField.style.flexGrow = 1f;
                    pathField.style.marginRight = 4f;
                    row.Add(pathField);

                    var pingBtn = new Button(() => PingResolvedTransform(itemProp.stringValue, partRoot))
                    {
                        text = "Ping",
                        tooltip = "Select the resolved Transform in the SceneView.",
                    };
                    pingBtn.style.flexShrink = 0f;
                    pingBtn.style.marginRight = 4f;
                    pingBtn.SetEnabled(!string.IsNullOrEmpty(itemProp.stringValue) && ResolveTransform(itemProp.stringValue, partRoot) != null);
                    row.Add(pingBtn);

                    var removeBtn = new Button(() =>
                    {
                        transformsProp.serializedObject.Update();
                        transformsProp.DeleteArrayElementAtIndex(captured);
                        transformsProp.serializedObject.ApplyModifiedProperties();
                        context?.MarkDirty?.Invoke();
                        Refresh();
                    })
                    {
                        text = "X",
                        tooltip = "Remove this entry.",
                    };
                    removeBtn.AddToClassList("data-editor-card-remove-btn");
                    row.Add(removeBtn);

                    rowsContainer.Add(row);
                }
            }

            addBtn.clicked += () =>
            {
                transformsProp.serializedObject.Update();
                transformsProp.arraySize++;
                transformsProp.GetArrayElementAtIndex(transformsProp.arraySize - 1).stringValue = string.Empty;
                transformsProp.serializedObject.ApplyModifiedProperties();
                context?.MarkDirty?.Invoke();
                Refresh();
            };

            Refresh();
            return outer;
        }

        private static Transform ResolveTransform(string path, Transform partRoot)
        {
            if (string.IsNullOrEmpty(path) || partRoot == null)
            {
                return null;
            }
            return partRoot.Find(path);
        }

        private static void PingResolvedTransform(string path, Transform partRoot)
        {
            Transform resolved = ResolveTransform(path, partRoot);
            if (resolved == null)
            {
                return;
            }
            EditorGUIUtility.PingObject(resolved.gameObject);
            Selection.activeGameObject = resolved.gameObject;
        }
    }
}
