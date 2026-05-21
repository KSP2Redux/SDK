using KSP;
using Ksp2UnityTools.Editor.PartAuthoring.SceneTools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// Builds the attach-node list for the Attachment section.
    /// </summary>
    /// <remarks>
    /// Each list element is rendered as a self-contained card with the editable nodeID and a
    /// remove button at the top, followed by every other AttachNodeDefinition field as a
    /// PropertyField. A trailing "+ Add Attach Node" button appends a new default entry.
    /// </remarks>
    internal sealed class AttachNodesListBuilder
    {
        private readonly SerializedObject _so;
        private readonly SerializedProperty _arrayProp;
        private readonly CorePartData _target;
        private VisualElement _container;

        /// <summary>
        /// Creates a list builder bound to the attachNodes array on the supplied SerializedObject.
        /// </summary>
        /// <param name="so">The CorePartData's SerializedObject.</param>
        /// <param name="target">The CorePartData the array belongs to. Used by the SceneView handle pickers.</param>
        public AttachNodesListBuilder(SerializedObject so, CorePartData target)
        {
            _so = so;
            _arrayProp = so.FindProperty("core.data.attachNodes");
            _target = target;
        }

        /// <summary>
        /// Builds the list container and renders the current array contents.
        /// </summary>
        /// <returns>A bordered card containing the attach-node cards and the trailing add button.</returns>
        public VisualElement Build()
        {
            var outer = new VisualElement { name = "attach-nodes-list" };
            outer.AddToClassList("attach-nodes-list");

            var header = new Label("Attach Nodes");
            header.AddToClassList("attach-nodes-list-header");
            outer.Add(header);

            _container = new VisualElement { name = "attach-nodes-list-body" };
            outer.Add(_container);

            Refresh();
            return outer;
        }

        /// <summary>
        /// Rebuilds the list from the current array state.
        /// </summary>
        /// <remarks>
        /// Call after any mutation that reorders or resizes the underlying SerializedProperty array
        /// (add / remove / external regeneration).
        /// </remarks>
        public void Refresh()
        {
            if (_container == null || _arrayProp == null)
            {
                return;
            }
            _container.Clear();
            for (int i = 0; i < _arrayProp.arraySize; i++)
            {
                _container.Add(BuildCard(i));
            }
            _container.Add(BuildAddButton());
        }

        private VisualElement BuildCard(int index)
        {
            var elementProp = _arrayProp.GetArrayElementAtIndex(index);

            var card = new VisualElement();
            card.AddToClassList("attach-node-card");

            var headerRow = new VisualElement();
            headerRow.AddToClassList("attach-node-header-row");

            var body = new VisualElement();
            body.AddToClassList("attach-node-body");

            var disclosure = new Button { text = "▼" };
            disclosure.AddToClassList("attach-node-disclosure");
            disclosure.tooltip = "Collapse or expand this attach node's fields.";
            headerRow.Add(disclosure);

            bool expanded = true;
            disclosure.clicked += () =>
            {
                expanded = !expanded;
                body.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
                disclosure.text = expanded ? "▼" : "▶";
            };

            var nameLabel = new Label("Name");
            nameLabel.AddToClassList("attach-node-name-label");
            headerRow.Add(nameLabel);

            var nodeIdProp = elementProp.FindPropertyRelative("nodeID");
            var nameField = new TextField { value = nodeIdProp != null ? nodeIdProp.stringValue : string.Empty };
            nameField.AddToClassList("attach-node-name-field");
            if (nodeIdProp != null)
            {
                nameField.BindProperty(nodeIdProp);
            }
            headerRow.Add(nameField);

            int capturedIndex = index;
            var removeBtn = new Button(() => RemoveAt(capturedIndex))
            {
                text = "x",
                tooltip = "Remove this attach node.",
            };
            removeBtn.AddToClassList("attach-node-remove-btn");
            headerRow.Add(removeBtn);

            card.Add(headerRow);
            card.Add(body);

            var positionProp = elementProp.FindPropertyRelative("position");

            var iterator = elementProp.Copy();
            var endProp = iterator.GetEndProperty();
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, endProp))
                    {
                        break;
                    }
                    if (iterator.name == "nodeID")
                    {
                        continue;
                    }
                    if (iterator.name == "position")
                    {
                        body.Add(new Vector3dHandleField(iterator.Copy(), _target, SceneHandlePicker.HandleMode.Position));
                        continue;
                    }
                    if (iterator.name == "orientation")
                    {
                        body.Add(new Vector3dHandleField(iterator.Copy(), _target, SceneHandlePicker.HandleMode.Orientation, positionProp));
                        continue;
                    }
                    body.Add(new PropertyField(iterator.Copy()));
                }
                while (iterator.NextVisible(false));
            }

            return card;
        }

        private Button BuildAddButton()
        {
            var btn = new Button(AddOne)
            {
                text = "+ Add Attach Node",
            };
            btn.AddToClassList("attach-node-add-btn");
            return btn;
        }

        private void AddOne()
        {
            if (_arrayProp == null)
            {
                return;
            }
            _arrayProp.arraySize++;
            _so.ApplyModifiedProperties();
            Refresh();
        }

        private void RemoveAt(int index)
        {
            if (_arrayProp == null || index < 0 || index >= _arrayProp.arraySize)
            {
                return;
            }
            _arrayProp.DeleteArrayElementAtIndex(index);
            _so.ApplyModifiedProperties();
            Refresh();
        }
    }
}
