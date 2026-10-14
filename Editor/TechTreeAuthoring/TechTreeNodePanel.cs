using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The tech tree editor's side panel: every option of the selected node, or the tree's settings when nothing is
    /// selected.
    /// </summary>
    /// <remarks>
    /// Every edit records the tree for undo and writes it directly, as the mission editor's rows do. Text fields commit
    /// when they lose focus, so a name typed in one go is one undo step.
    /// </remarks>
    public class TechTreeNodePanel : VisualElement
    {
        private const string TIER_UNLOCK_TOOLTIP =
            "The tier this node opens when it is researched. The R&D Center plays the tier unlock and scrolls to it. " +
            "0 opens none.";

        private const string MYSTERY_TECH_TOOLTIP =
            "Hides the node's icon, name, description and parts until it can be unlocked.";

        // Kept across rebuilds, so a section the author closed stays closed from node to node
        private static readonly HashSet<string> CollapsedSections = new();

        private TechTreeAsset _tree;

        private SerializedObject _serializedTree;

        /// <summary>
        /// Raised with a node's ID when the author asks to go to that node.
        /// </summary>
        public event Action<string> OnNodeRequested;

        /// <summary>
        /// Raised after the panel changes the tree.
        /// </summary>
        public event Action OnTreeEdited;

        /// <summary>
        /// Raised with the old and new ID after the panel renames the selected node.
        /// </summary>
        public event Action<string, string> OnNodeRenamed;

        /// <summary>
        /// Raised with the reason when the panel refuses an edit.
        /// </summary>
        public event Action<string> OnRefused;

        /// <summary>
        /// Creates an empty panel.
        /// </summary>
        public TechTreeNodePanel()
        {
            AddToClassList("tech-tree-node-panel");
        }

        /// <summary>
        /// Shows a node's options, or the tree's settings.
        /// </summary>
        /// <param name="tree">The tree, or null to show nothing.</param>
        /// <param name="serializedTree">The tree's serialized object, which the bound fields use.</param>
        /// <param name="nodeId">The selected node's ID, or null for the tree's settings.</param>
        public void Show(TechTreeAsset tree, SerializedObject serializedTree, string nodeId)
        {
            _tree = tree;
            _serializedTree = serializedTree;
            Clear();
            if (_tree == null)
                return;

            // Nodes added since the last update would otherwise have no serialized property to bind
            _serializedTree.Update();

            TechTreeNode node = TechTreeEditing.Find(_tree, nodeId);
            if (node == null)
            {
                ShowTreeSettings();
                return;
            }

            Label title = AddLabel(this, DisplayName(node), "tech-tree-panel__title");
            AddLabel(this, node.Data.ID, "tech-tree-panel__id");
            AddIdentity(node, title);
            AddCost(node);
            AddPrerequisites(node);
            AddParts(node);
            AddGates(node);
            AddVisibility(node);
            AddPosition(node);
        }

        private void ShowTreeSettings()
        {
            AddLabel(this, _tree.name, "tech-tree-panel__title");
            var tiers = new PropertyField(_serializedTree.FindProperty(nameof(TechTreeAsset.Tiers)));
            tiers.Bind(_serializedTree);
            Add(tiers);
        }

        private void AddIdentity(TechTreeNode node, Label title)
        {
            Foldout section = AddSection("Identity");

            var id = new TextField("ID") { isDelayed = true, value = node.Data.ID };
            id.RegisterValueChangedCallback(evt => Rename(node, id, evt.newValue));
            section.Add(id);

            var englishName = new TextField("Name") { isDelayed = true, value = node.EnglishName ?? string.Empty };
            englishName.RegisterValueChangedCallback(evt =>
            {
                Edit("Rename Tech Node", () => node.EnglishName = evt.newValue);
                title.text = DisplayName(node);
            });
            section.Add(englishName);

            var description = new TextField("Description")
            {
                isDelayed = true,
                multiline = true,
                value = node.EnglishDescription ?? string.Empty,
            };
            description.AddToClassList("tech-tree-panel__description");
            description.RegisterValueChangedCallback(evt =>
                Edit("Edit Tech Node Description", () => node.EnglishDescription = evt.newValue));
            section.Add(description);

            section.Add(new AutocompleteField(
                node.Data.IconID ?? string.Empty,
                "Icon",
                TechTreePickerCatalog.GetIconAddresses,
                value => Edit("Change Tech Node Icon", () => node.Data.IconID = value)
            ));
        }

        private void AddCost(TechTreeNode node)
        {
            Foldout section = AddSection("Cost");

            var science = new IntegerField("Science") { isDelayed = true, value = node.Data.RequiredSciencePoints };
            science.RegisterValueChangedCallback(evt =>
                Edit("Change Tech Node Cost", () => node.Data.RequiredSciencePoints = Mathf.Max(0, evt.newValue)));
            section.Add(science);

            var tierUnlock = new IntegerField("Tier unlock")
            {
                isDelayed = true,
                value = node.Data.TierToUnlock,
                tooltip = TIER_UNLOCK_TOOLTIP,
            };
            tierUnlock.RegisterValueChangedCallback(evt =>
                Edit("Change Tech Node Tier Unlock", () => node.Data.TierToUnlock = Mathf.Max(0, evt.newValue)));
            section.Add(tierUnlock);
        }

        private void AddPrerequisites(TechTreeNode node)
        {
            Foldout section = AddSection("Prerequisites");

            CardListSection.ListHandle handle = null;
            handle = CardListSection.BuildFromList(() => node.Data.RequiredTechNodeIDs, new CardListSection.ListConfig<string>
            {
                Title = "Required",
                AddButtonText = "+ Add",
                OnAddClicked = () => ShowPrerequisitePicker(node, handle.Root),
                BuildCard = (prerequisiteId, index) => BuildNodeRow(prerequisiteId, () =>
                {
                    EditIf("Remove Tech Node Prerequisite", () =>
                        TechTreeEditing.RemovePrerequisite(_tree, node.Data.ID, prerequisiteId));
                    handle.Rebuild();
                }),
            });
            section.Add(handle.Root);

            List<string> dependents = TechTreeEditing.GetDependents(_tree, node.Data.ID);
            AddLabel(section, $"Required by ({dependents.Count})", "tech-tree-panel__subheading");
            foreach (string dependent in dependents)
            {
                section.Add(BuildNodeRow(dependent, null));
            }
        }

        // Every other node, with those that would close a loop shown but disabled
        private void ShowPrerequisitePicker(TechTreeNode node, VisualElement anchor)
        {
            string[] required = node.Data.RequiredTechNodeIDs ?? Array.Empty<string>();
            var menu = new GenericDropdownMenu();
            int offered = 0;
            foreach (TechTreeNode candidate in _tree.Nodes
                         .Where(candidate => !string.IsNullOrEmpty(candidate.Data?.ID))
                         .Where(candidate => candidate != node && !required.Contains(candidate.Data.ID))
                         .OrderBy(DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                string candidateId = candidate.Data.ID;
                string label = $"{DisplayName(candidate)} ({candidateId})";
                offered++;
                if (TechTreeEditing.WouldCreateLoop(_tree, node.Data.ID, candidateId))
                {
                    menu.AddDisabledItem(label, false);
                    continue;
                }

                menu.AddItem(label, false, () =>
                {
                    EditIf("Add Tech Node Prerequisite", () =>
                        TechTreeEditing.TryAddPrerequisite(_tree, node.Data.ID, candidateId));
                    Show(_tree, _serializedTree, node.Data.ID);
                });
            }

            if (offered == 0)
            {
                menu.AddDisabledItem("No other nodes", false);
            }

            menu.DropDown(anchor.worldBound, anchor, DropdownMenuSizeMode.Auto);
        }

        private void AddParts(TechTreeNode node)
        {
            Foldout section = AddSection("Parts");
            section.Add(BuildIdList(
                "Unlocked",
                () => node.Data.UnlockedPartsIDs,
                ids => node.Data.UnlockedPartsIDs = ids,
                TechTreePickerCatalog.GetPartNames,
                "Part"
            ));
        }

        private void AddGates(TechTreeNode node)
        {
            Foldout section = AddSection("Gates");
            section.Add(BuildIdList(
                "Missions",
                () => node.Data.RequiredMissionIDs,
                ids => node.Data.RequiredMissionIDs = ids,
                TechTreePickerCatalog.GetMissionIds,
                "Mission"
            ));

            int index = _tree.Nodes.IndexOf(node);
            SerializedProperty research = _serializedTree.FindProperty(
                $"{nameof(TechTreeAsset.Nodes)}.Array.data[{index}].{nameof(TechTreeNode.Data)}.{nameof(node.Data.RequiredResearchData)}"
            );
            if (research == null)
                return;

            var researchField = new PropertyField(research, "Research");
            researchField.Bind(_serializedTree);
            section.Add(researchField);
        }

        private void AddVisibility(TechTreeNode node)
        {
            Foldout section = AddSection("Visibility");
            var mystery = new Toggle("Mystery tech") { value = node.Data.MysteryTech, tooltip = MYSTERY_TECH_TOOLTIP };
            mystery.RegisterValueChangedCallback(evt =>
                Edit("Change Tech Node Visibility", () => node.Data.MysteryTech = evt.newValue));
            section.Add(mystery);
        }

        private void AddPosition(TechTreeNode node)
        {
            Foldout section = AddSection("Position");
            var x = new FloatField("X") { isDelayed = true, value = node.Data.TechTreePosition.x };
            x.RegisterValueChangedCallback(evt => Edit("Move Tech Node", () =>
                node.Data.TechTreePosition = new Vector2(evt.newValue, node.Data.TechTreePosition.y)));
            section.Add(x);

            var y = new FloatField("Y") { isDelayed = true, value = node.Data.TechTreePosition.y };
            y.RegisterValueChangedCallback(evt => Edit("Move Tech Node", () =>
                node.Data.TechTreePosition = new Vector2(node.Data.TechTreePosition.x, evt.newValue)));
            section.Add(y);
        }

        // A list of IDs typed or picked one per row. The pickers suggest, and any text is kept, since a tree may name
        // another mod's content.
        private VisualElement BuildIdList(
            string title,
            Func<string[]> read,
            Action<string[]> write,
            Func<IEnumerable<string>> suggestions,
            string what
        )
        {
            CardListSection.ListHandle handle = null;
            handle = CardListSection.BuildFromList(read, new CardListSection.ListConfig<string>
            {
                Title = title,
                AddButtonText = "+ Add",
                OnAddClicked = () =>
                {
                    Edit($"Add Tech Node {what}", () => write((read() ?? Array.Empty<string>()).Append(string.Empty).ToArray()));
                    handle.Rebuild();
                },
                BuildCard = (value, index) =>
                {
                    var row = new VisualElement();
                    row.AddToClassList("data-editor-inline-row");

                    var field = new AutocompleteField(value ?? string.Empty, null, suggestions, newValue =>
                        Edit($"Change Tech Node {what}", () =>
                        {
                            string[] ids = read();
                            if (index < ids.Length)
                            {
                                ids[index] = newValue;
                            }
                        }));
                    field.AddToClassList("data-editor-inline-row__grow");
                    row.Add(field);

                    var remove = new Button(() =>
                    {
                        Edit($"Remove Tech Node {what}", () => write(read().Where((_, i) => i != index).ToArray()));
                        handle.Rebuild();
                    }) { text = "X" };
                    remove.AddToClassList("data-editor-card-remove-btn");
                    row.Add(remove);
                    return row;
                },
            });
            return handle.Root;
        }

        // A node by name that goes to it on click, with a remove button when removing is offered
        private VisualElement BuildNodeRow(string nodeId, Action remove)
        {
            var row = new VisualElement();
            row.AddToClassList("data-editor-inline-row");

            TechTreeNode node = TechTreeEditing.Find(_tree, nodeId);
            var go = new Button(() => OnNodeRequested?.Invoke(nodeId))
            {
                text = node != null ? DisplayName(node) : nodeId,
                tooltip = nodeId,
            };
            go.AddToClassList("data-editor-inline-row__grow");
            go.AddToClassList("tech-tree-panel__node-link");
            go.SetEnabled(node != null);
            row.Add(go);

            if (remove != null)
            {
                var removeButton = new Button(remove) { text = "X" };
                removeButton.AddToClassList("data-editor-card-remove-btn");
                row.Add(removeButton);
            }

            return row;
        }

        private void Rename(TechTreeNode node, TextField field, string requested)
        {
            string oldId = node.Data.ID;
            string newId = requested?.Trim() ?? string.Empty;
            if (newId == oldId)
                return;

            if (newId.Length == 0 || TechTreeEditing.Find(_tree, newId) != null)
            {
                field.SetValueWithoutNotify(oldId);
                OnRefused?.Invoke(newId.Length == 0 ? "A node needs an ID." : $"{newId} is already a node in this tree.");
                return;
            }

            EditIf("Rename Tech Node", () => TechTreeEditing.TryRenameNode(_tree, oldId, newId));
            OnNodeRenamed?.Invoke(oldId, newId);
        }

        private void Edit(string undoName, Action change) =>
            EditIf(undoName, () =>
            {
                change();
                return true;
            });

        // For an edit that may turn out to change nothing
        private void EditIf(string undoName, Func<bool> change)
        {
            Undo.RecordObject(_tree, undoName);
            if (!change())
                return;

            EditorUtility.SetDirty(_tree);
            OnTreeEdited?.Invoke();
        }

        private Foldout AddSection(string title)
        {
            var section = new Foldout { text = title, value = !CollapsedSections.Contains(title) };
            section.AddToClassList("tech-tree-panel__section");
            section.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != section)
                    return;

                if (evt.newValue)
                {
                    CollapsedSections.Remove(title);
                }
                else
                {
                    CollapsedSections.Add(title);
                }
            });
            Add(section);
            return section;
        }

        private static Label AddLabel(VisualElement parent, string text, string className)
        {
            var label = new Label(text);
            label.AddToClassList(className);
            parent.Add(label);
            return label;
        }

        private static string DisplayName(TechTreeNode node) =>
            string.IsNullOrEmpty(node.EnglishName) ? node.Data.ID : node.EnglishName;
    }
}
