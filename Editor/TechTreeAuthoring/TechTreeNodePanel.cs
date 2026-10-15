using System;
using System.Collections.Generic;
using System.Linq;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.Modding;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
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

        private IReadOnlyList<TechTreeNode> _reference = Array.Empty<TechTreeNode>();

        private ReferenceTreeData _referenceTreeData;

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
        /// Raised after the panel changes which tree a patch changes.
        /// </summary>
        public event Action OnReferenceChanged;

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
        /// <param name="reference">The nodes of the tree this one is placed against, which can be required but never
        /// change, or null for none.</param>
        /// <param name="referenceTreeData">The tiers and start node of the tree this one is placed against, which a
        /// patch starts from, or null for none.</param>
        public void Show(
            TechTreeAsset tree,
            SerializedObject serializedTree,
            string nodeId,
            IReadOnlyList<TechTreeNode> reference = null,
            ReferenceTreeData referenceTreeData = null
        )
        {
            _tree = tree;
            _serializedTree = serializedTree;
            _reference = reference ?? Array.Empty<TechTreeNode>();
            _referenceTreeData = referenceTreeData;
            Clear();
            if (_tree == null)
                return;

            // Bookkeeping rather than an author's edit, so it records no undo step. Undoing one would only take it again.
            if (_tree is TechTreePatchAsset taking && taking.TakeReferenceTreeData(_referenceTreeData))
            {
                EditorUtility.SetDirty(taking);
                OnTreeEdited?.Invoke();
            }

            // Nodes added since the last update would otherwise have no serialized property to bind
            _serializedTree.Update();

            TechTreeNode node = TechTreeEditing.Find(_tree, nodeId);
            if (node == null && _tree is TechTreePatchAsset patch
                && TechTreeEditing.FindAny(_tree, _reference, nodeId) is { } referenceNode)
            {
                ShowReferenceNode(patch, referenceNode);
                return;
            }

            if (node == null)
            {
                ShowTreeSettings();
                return;
            }

            var owner = _tree as TechTreePatchAsset;
            bool isOverride = owner != null && owner.IsOverride(node.Data.ID);
            Label title = AddHeader(DisplayName(node), isOverride ? $"{node.Data.ID}, changed by this patch" : node.Data.ID);
            if (isOverride)
            {
                AddPatchActions(BuildChip("Revert to Reference", () =>
                    EditAndShow("Revert Tech Node", () => TechTreeEditing.RevertOverride(owner, node.Data.ID), node.Data.ID)));
            }

            AddIdentity(node, title);
            AddCost(node);
            AddPrerequisites(node);
            AddParts(node);
            AddGates(node);
            AddVisibility(node);
            AddPosition(node);
            AlignFields();
        }

        // A reference node is shown as it is, read only, until the patch takes it in to change or removes it
        private void ShowReferenceNode(TechTreePatchAsset patch, TechTreeNode node)
        {
            string nodeId = node.Data.ID;
            bool isRemoved = patch.RemovedNodeIds.Contains(nodeId);
            Label title = AddHeader(
                DisplayName(node),
                isRemoved ? $"{nodeId}, removed by this patch" : $"{nodeId}, in the reference tree"
            );
            if (isRemoved)
            {
                AddPatchActions(BuildChip("Restore", () =>
                    EditAndShow("Restore Tech Node", () => TechTreeEditing.RestoreReferenceNode(patch, nodeId), nodeId)));
            }
            else
            {
                AddPatchActions(
                    BuildChip("Change", () => EditAndShow("Change Tech Node", () =>
                    {
                        TechTreeEditing.TakeReferenceNode(patch, node);
                        return true;
                    }, nodeId)),
                    BuildChip("Remove", () =>
                        EditAndShow("Remove Tech Node", () => TechTreeEditing.RemoveReferenceNode(patch, nodeId), nodeId))
                );
            }

            var sections = new VisualElement();
            Add(sections);
            int before = childCount;
            AddIdentity(node, title);
            AddCost(node);
            AddPrerequisites(node);
            AddParts(node);
            AddGates(node);
            AddVisibility(node);
            AddPosition(node);
            while (childCount > before)
            {
                sections.Add(ElementAt(before));
            }

            sections.SetEnabled(false);
            AlignFields();
        }

        private void AddPatchActions(params VisualElement[] chips)
        {
            var row = new VisualElement();
            row.AddToClassList("sdk-inspector-chip-row");
            row.AddToClassList("tech-tree-panel__actions");
            foreach (VisualElement chip in chips)
            {
                row.Add(chip);
            }

            Add(row);
        }

        private static Button BuildChip(string text, Action clicked)
        {
            var chip = new Button(clicked) { text = text };
            chip.AddToClassList("sdk-tool-chip");
            return chip;
        }

        // An edit that changes which nodes the patch holds, so the panel shows the node again afterwards
        private void EditAndShow(string undoName, Func<bool> change, string nodeId)
        {
            EditIf(undoName, change);
            Show(_tree, _serializedTree, nodeId, _reference, _referenceTreeData);
        }

        private void ShowTreeSettings()
        {
            AddHeader(_tree.name, $"{_tree.Nodes.Count} nodes");
            if (_tree is TechTreePatchAsset patch)
            {
                ShowPatchSettings(patch);
                return;
            }

            // The baked files are what the game loads, so their labels carry the tree's features
            Foldout features = AddSection("Features");
            features.Add(AssetFeaturesSection.Build(() => _tree.BakedFiles, KSP2UnityTools.FindParentMod(_tree)));

            AddStartNodeAndTiers(_tree.Nodes.Select(node => node.Data?.ID));
            AlignFields();
        }

        private void AddStartNodeAndTiers(IEnumerable<string> startNodeCandidates)
        {
            Foldout start = AddSection("Start Node");
            start.Add(new AutocompleteField(
                _serializedTree.FindProperty(nameof(TechTreeAsset.StartNodeId)),
                "Start node",
                () => startNodeCandidates.Where(id => !string.IsNullOrEmpty(id)).Distinct()
            ));

            Foldout tiers = AddSection("Tiers");
            tiers.Add(BuildTierList());
        }

        // A patch names the tree it changes and the features it loads under. Its tiers and start node start as the
        // reference's, and only what the author changes is patched.
        private void ShowPatchSettings(TechTreePatchAsset patch)
        {
            Foldout reference = AddSection("Reference Tree");
            SerializedProperty sourceProperty = _serializedTree.FindProperty(nameof(TechTreePatchAsset.ReferenceSource));
            SerializedProperty referenceProperty = _serializedTree.FindProperty(nameof(TechTreePatchAsset.Reference));
            var source = new PropertyField(sourceProperty, "Source");
            var asset = new ObjectField("Tree or folder")
            {
                objectType = typeof(UnityEngine.Object),
                allowSceneObjects = false,
                bindingPath = nameof(TechTreePatchAsset.Reference),
            };
            void ShowAssetField() =>
                asset.style.display = patch.ReferenceSource == TechNodeReferenceSource.Asset ? DisplayStyle.Flex : DisplayStyle.None;

            ShowAssetField();
            reference.Add(source);
            reference.Add(asset);
            reference.TrackPropertyValue(sourceProperty, _ =>
            {
                ShowAssetField();
                OnReferenceChanged?.Invoke();
            });
            reference.TrackPropertyValue(referenceProperty, _ => OnReferenceChanged?.Invoke());
            reference.Bind(_serializedTree);

            // A patch's Lua has no labels to carry features, so the patch keeps them itself
            Foldout features = AddSection("Features");
            Mod owner = KSP2UnityTools.FindParentMod(patch);
            features.Add(BuildFeatureList("Features", () => patch.Features, owner));
            features.Add(BuildFeatureList("Excluded Features", () => patch.ExcludedFeatures, owner));

            if (owner != null)
            {
                AddPatchBuild(patch);
            }

            // A removed reference node cannot be where the R&D Center opens
            AddStartNodeAndTiers(patch.Nodes
                .Concat(_reference)
                .Select(node => node.Data?.ID)
                .Where(id => !patch.RemovedNodeIds.Contains(id)));
            AlignFields();
        }

        // Exporting writes or deletes the patch's Lua, which undo cannot take back, so the toggle records no undo step
        private void AddPatchBuild(TechTreePatchAsset patch)
        {
            Foldout section = AddSection("Build");
            var bakeOnBuild = new Toggle("Bake on build") { value = patch.BakeOnBuild };
            var exported = new ObjectField("Exported Lua") { objectType = typeof(UnityEngine.Object), allowSceneObjects = false };

            // Left enabled so a click pings the file, but the bake decides where it is
            exported.RegisterValueChangedCallback(evt => exported.SetValueWithoutNotify(evt.previousValue));
            void ShowExported()
            {
                exported.SetValueWithoutNotify(
                    patch.BakeOnBuild ? null : AssetDatabase.LoadMainAssetAtPath(TechTreeBaker.GetExportPath(patch))
                );
                exported.style.display = patch.BakeOnBuild ? DisplayStyle.None : DisplayStyle.Flex;
            }

            bakeOnBuild.RegisterValueChangedCallback(evt =>
            {
                if (!TechTreeBaker.SetBakeOnBuild(patch, evt.newValue))
                {
                    bakeOnBuild.SetValueWithoutNotify(patch.BakeOnBuild);
                }

                ShowExported();
            });
            ShowExported();
            section.Add(bakeOnBuild);
            section.Add(exported);
        }

        private VisualElement BuildFeatureList(string title, Func<List<string>> read, Mod owner)
        {
            CardListSection.ListHandle handle = null;
            handle = CardListSection.BuildFromList(() => read(), new CardListSection.ListConfig<string>
            {
                Title = title,
                AddButtonText = "+ Add",
                OnAddClicked = () =>
                {
                    Edit($"Add Tech Tree Patch {title}", () => read().Add(string.Empty));
                    handle.Rebuild();
                },
                BuildCard = (value, index) =>
                {
                    var row = new VisualElement();
                    row.AddToClassList("data-editor-inline-row");
                    var field = new FeatureField(value ?? string.Empty, null, owner, newValue =>
                        Edit($"Change Tech Tree Patch {title}", () =>
                        {
                            List<string> ids = read();
                            if (index < ids.Count)
                            {
                                ids[index] = newValue;
                            }
                        }));
                    field.AddToClassList("data-editor-inline-row__grow");
                    row.Add(field);

                    var remove = new Button(() =>
                    {
                        Edit($"Remove Tech Tree Patch {title}", () => read().RemoveAt(index));
                        handle.Rebuild();
                    }) { text = "X" };
                    remove.AddToClassList("data-editor-card-remove-btn");
                    row.Add(remove);
                    return row;
                },
            });
            return handle.Root;
        }

        // One name per tier page, the first page first. A tier without a name shows by its number in game.
        private VisualElement BuildTierList()
        {
            CardListSection.ListHandle handle = null;
            handle = CardListSection.BuildFromList(() => _tree.Tiers, new CardListSection.ListConfig<TechTreeTier>
            {
                Title = "Names",
                AddButtonText = "+ Add",
                OnAddClicked = () =>
                {
                    Edit("Add Tech Tree Tier", () => _tree.Tiers.Add(new TechTreeTier { EnglishName = string.Empty }));
                    handle.Rebuild();
                },
                BuildCard = (tier, index) =>
                {
                    var row = new VisualElement();
                    row.AddToClassList("data-editor-inline-row");

                    var number = new Label((index + 1).ToString());
                    number.AddToClassList("tech-tree-panel__tier-number");
                    row.Add(number);

                    var name = new TextField { isDelayed = true, value = tier.EnglishName ?? string.Empty };
                    name.AddToClassList("data-editor-inline-row__grow");
                    name.RegisterValueChangedCallback(evt => Edit("Rename Tech Tree Tier", () => tier.EnglishName = evt.newValue));
                    row.Add(name);

                    var remove = new Button(() =>
                    {
                        Edit("Remove Tech Tree Tier", () => _tree.Tiers.RemoveAt(index));
                        handle.Rebuild();
                    }) { text = "X" };
                    remove.AddToClassList("data-editor-card-remove-btn");
                    row.Add(remove);
                    return row;
                },
            });
            return handle.Root;
        }

        // The part and planet inspectors' header: the name, and a meta chip under it
        private Label AddHeader(string name, string meta)
        {
            var header = new VisualElement();
            header.AddToClassList("sdk-inspector-header");
            var identity = new VisualElement();
            identity.AddToClassList("sdk-inspector-identity-row");
            var text = new VisualElement();
            text.AddToClassList("sdk-inspector-identity-text");
            Label title = AddLabel(text, name, "sdk-inspector-name");
            var chips = new VisualElement();
            chips.AddToClassList("sdk-inspector-chip-row");
            AddLabel(chips, meta, "sdk-inspector-meta-chip");
            text.Add(chips);
            identity.Add(text);
            header.Add(identity);
            Add(header);
            return title;
        }

        // Labels line up as in an inspector
        private void AlignFields() =>
            this.Query<VisualElement>(className: BaseField<string>.ussClassName)
                .ForEach(field => field.AddToClassList(BaseField<string>.alignedFieldUssClassName));

        private void AddIdentity(TechTreeNode node, Label title)
        {
            Foldout section = AddSection("Identity");

            var id = new TextField("ID") { isDelayed = true, value = node.Data.ID };
            id.RegisterValueChangedCallback(evt => Rename(node, id, evt.newValue));

            // An override patches the reference node by its ID
            id.SetEnabled(!(_tree is TechTreePatchAsset patch && patch.IsOverride(node.Data.ID)));
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
                OnAddClicked = () =>
                {
                    Edit("Add Tech Node Prerequisite", () =>
                        node.Data.RequiredTechNodeIDs = (node.Data.RequiredTechNodeIDs ?? Array.Empty<string>())
                            .Append(string.Empty)
                            .ToArray());
                    handle.Rebuild();
                },
                BuildCard = (prerequisiteId, index) => BuildPrerequisiteRow(node, index, prerequisiteId, () =>
                {
                    Edit("Remove Tech Node Prerequisite", () =>
                        node.Data.RequiredTechNodeIDs = node.Data.RequiredTechNodeIDs.Where((_, i) => i != index).ToArray());
                    handle.Rebuild();
                }),
            });
            section.Add(handle.Root);

            List<string> dependents = TechTreeEditing.GetDependents(_tree, node.Data.ID, _reference);
            AddLabel(section, $"Required by ({dependents.Count})", "sdk-card-list__title");
            foreach (string dependent in dependents)
            {
                section.Add(BuildNodeRow(dependent, null));
            }
        }

        // A prerequisite typed or picked, with a button that goes to it
        private VisualElement BuildPrerequisiteRow(TechTreeNode node, int index, string value, Action remove)
        {
            var row = new VisualElement();
            row.AddToClassList("data-editor-inline-row");

            Button jump = null;
            var field = new AutocompleteField(
                value ?? string.Empty,
                null,
                () => GetPrerequisiteCandidates(node, index),
                newValue =>
                {
                    Edit("Change Tech Node Prerequisite", () =>
                    {
                        string[] required = node.Data.RequiredTechNodeIDs;
                        if (index < required.Length)
                        {
                            required[index] = newValue;
                        }
                    });
                    jump.SetEnabled(TechTreeEditing.FindAny(_tree, _reference, newValue) != null);
                },
                detailSource: candidateId => NameOf(candidateId)
            );
            field.AddToClassList("data-editor-inline-row__grow");
            row.Add(field);

            jump = new Button(() =>
            {
                string[] required = node.Data.RequiredTechNodeIDs;
                if (index < required.Length)
                {
                    OnNodeRequested?.Invoke(required[index]);
                }
            })
            {
                text = "●",
                tooltip = "Go to the node",
            };
            jump.AddToClassList("data-editor-card-remove-btn");
            jump.AddToClassList("tech-tree-panel__jump");
            jump.SetEnabled(TechTreeEditing.FindAny(_tree, _reference, value) != null);
            row.Add(jump);

            var removeButton = new Button(remove) { text = "X" };
            removeButton.AddToClassList("data-editor-card-remove-btn");
            row.Add(removeButton);
            return row;
        }

        // Every node this one could require: not itself, not one it requires already, not one a patch removes, and none
        // that would close a loop
        private IEnumerable<string> GetPrerequisiteCandidates(TechTreeNode node, int index)
        {
            string[] required = node.Data.RequiredTechNodeIDs ?? Array.Empty<string>();
            var patch = _tree as TechTreePatchAsset;
            return _tree.Nodes
                .Concat(_reference)
                .Select(candidate => candidate.Data?.ID)
                .Where(candidateId => !string.IsNullOrEmpty(candidateId) && candidateId != node.Data.ID)
                .Where(candidateId => patch == null || !patch.RemovedNodeIds.Contains(candidateId))
                .Distinct()
                .Where(candidateId => required.Where((_, i) => i != index).All(other => other != candidateId))
                .Where(candidateId => !TechTreeEditing.WouldCreateLoop(_tree, node.Data.ID, candidateId, _reference))
                .OrderBy(NameOf, StringComparer.OrdinalIgnoreCase);
        }

        private string NameOf(string nodeId)
        {
            TechTreeNode node = TechTreeEditing.FindAny(_tree, _reference, nodeId);
            return node != null ? DisplayName(node) : string.Empty;
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
            section.Add(BuildVariantList(node));
        }

        // One card per variant, picked part first, then its set, then the variant. Like the part list it suggests and
        // never restricts, since a tree may unlock another mod's variants.
        private VisualElement BuildVariantList(TechTreeNode node)
        {
            CardListSection.ListHandle handle = null;
            var config = new CardListSection.ListConfig<TechUnlockedVariant>
            {
                Title = "Variants",
                AddButtonText = "+ Add",
                OnAddClicked = () =>
                {
                    Edit("Add Tech Node Variant", () =>
                        node.Data.UnlockedVariants = (node.Data.UnlockedVariants ?? Array.Empty<TechUnlockedVariant>())
                            .Append(new TechUnlockedVariant())
                            .ToArray());
                    handle.Rebuild();
                },
                BuildCard = (variant, index) => BuildVariantCard(variant, () =>
                {
                    Edit("Remove Tech Node Variant", () =>
                        node.Data.UnlockedVariants = node.Data.UnlockedVariants.Where((_, i) => i != index).ToArray());
                    handle.Rebuild();
                }),
            };
            handle = CardListSection.BuildFromList(() => node.Data.UnlockedVariants, config);
            return handle.Root;
        }

        private VisualElement BuildVariantCard(TechUnlockedVariant variant, Action remove)
        {
            VisualElement card = CardShell.Build(out CardShell.Slots slots);
            var title = new Label(VariantTitle(variant));
            title.AddToClassList("data-editor-card-name-field");
            slots.Header.Add(title);
            var removeButton = new Button(remove) { text = "X" };
            removeButton.AddToClassList("data-editor-card-remove-btn");
            slots.Header.Add(removeButton);

            IEnumerable<TechUnlockedVariant> Known() => TechTreePickerCatalog.GetVariants();
            slots.Body.Add(new AutocompleteField(
                variant.PartName ?? string.Empty,
                "Part",
                () => Known().Select(known => known.PartName).Distinct(),
                value =>
                {
                    Edit("Change Tech Node Variant Part", () => variant.PartName = value);
                    title.text = VariantTitle(variant);
                }
            ));
            slots.Body.Add(new AutocompleteField(
                variant.VariantSetId ?? string.Empty,
                "Variant set",
                () => Known().Where(known => known.PartName == variant.PartName).Select(known => known.VariantSetId).Distinct(),
                value =>
                {
                    Edit("Change Tech Node Variant Set", () => variant.VariantSetId = value);
                    title.text = VariantTitle(variant);
                }
            ));
            slots.Body.Add(new AutocompleteField(
                variant.VariantId ?? string.Empty,
                "Variant",
                () => Known()
                    .Where(known => known.PartName == variant.PartName && known.VariantSetId == variant.VariantSetId)
                    .Select(known => known.VariantId),
                value =>
                {
                    Edit("Change Tech Node Variant", () => variant.VariantId = value);
                    title.text = VariantTitle(variant);
                }
            ));
            return card;
        }

        private static string VariantTitle(TechUnlockedVariant variant) =>
            string.IsNullOrEmpty(variant.PartName) ? "Variant" : $"{variant.PartName} - {variant.VariantId}";

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

            section.Add(BuildResearchList(node));
        }

        // One card per research gate, with its experiment, location and the coordinates the R&D Center shows instead
        // of the location's name
        private VisualElement BuildResearchList(TechTreeNode node)
        {
            CardListSection.ListHandle handle = null;
            var config = new CardListSection.ListConfig<TechRequiredResearchData>
            {
                Title = "Research",
                AddButtonText = "+ Add",
                OnAddClicked = () =>
                {
                    Edit("Add Tech Node Research", () =>
                        node.Data.RequiredResearchData = (node.Data.RequiredResearchData ?? Array.Empty<TechRequiredResearchData>())
                            .Append(new TechRequiredResearchData())
                            .ToArray());
                    handle.Rebuild();
                },
                BuildCard = (research, index) => BuildResearchCard(research, () =>
                {
                    Edit("Remove Tech Node Research", () =>
                        node.Data.RequiredResearchData = node.Data.RequiredResearchData.Where((_, i) => i != index).ToArray());
                    handle.Rebuild();
                }),
            };
            handle = CardListSection.BuildFromList(() => node.Data.RequiredResearchData, config);
            return handle.Root;
        }

        private VisualElement BuildResearchCard(TechRequiredResearchData research, Action remove)
        {
            VisualElement card = CardShell.Build(out CardShell.Slots slots);
            var title = new Label(string.IsNullOrEmpty(research.ExperimentID) ? "Experiment" : research.ExperimentID);
            title.AddToClassList("data-editor-card-name-field");
            slots.Header.Add(title);
            var removeButton = new Button(remove) { text = "X" };
            removeButton.AddToClassList("data-editor-card-remove-btn");
            slots.Header.Add(removeButton);

            slots.Body.Add(new AutocompleteField(
                research.ExperimentID ?? string.Empty,
                "Experiment",
                ExperimentNameCatalog.GetKnownExperiments,
                value =>
                {
                    Edit("Change Tech Node Research Experiment", () => research.ExperimentID = value);
                    title.text = string.IsNullOrEmpty(value) ? "Experiment" : value;
                }
            ));

            var location = new TextField("Location") { isDelayed = true, value = research.ResearchLocationID ?? string.Empty };
            location.RegisterValueChangedCallback(evt => Edit("Change Tech Node Research Location",
                () => research.ResearchLocationID = string.IsNullOrEmpty(evt.newValue) ? null : evt.newValue));
            slots.Body.Add(location);

            // Coordinates only change how the gate reads in the R&D Center, so they are off until asked for
            bool hasCoordinates = research.Coordinates is { Length: >= 2 };
            var coordinates = new Toggle("Coordinates") { value = hasCoordinates };
            var latitude = new DoubleField("Latitude") { isDelayed = true, value = hasCoordinates ? research.Coordinates[0] : 0d };
            var longitude = new DoubleField("Longitude") { isDelayed = true, value = hasCoordinates ? research.Coordinates[1] : 0d };
            void ShowCoordinates(bool shown)
            {
                latitude.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                longitude.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            }

            void WriteCoordinates() => Edit("Change Tech Node Research Coordinates", () =>
                research.Coordinates = coordinates.value ? new[] { latitude.value, longitude.value } : Array.Empty<double>());

            coordinates.RegisterValueChangedCallback(evt =>
            {
                ShowCoordinates(evt.newValue);
                WriteCoordinates();
            });
            latitude.RegisterValueChangedCallback(_ => WriteCoordinates());
            longitude.RegisterValueChangedCallback(_ => WriteCoordinates());
            ShowCoordinates(hasCoordinates);
            slots.Body.Add(coordinates);
            slots.Body.Add(latitude);
            slots.Body.Add(longitude);
            return card;
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

            TechTreeNode node = TechTreeEditing.FindAny(_tree, _reference, nodeId);
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

            string refusal = GetRenameRefusal(newId);
            if (refusal != null)
            {
                field.SetValueWithoutNotify(oldId);
                OnRefused?.Invoke(refusal);
                return;
            }

            EditIf("Rename Tech Node", () => TechTreeEditing.TryRenameNode(_tree, oldId, newId, _reference));
            OnNodeRenamed?.Invoke(oldId, newId);
        }

        private string GetRenameRefusal(string newId)
        {
            if (newId.Length == 0)
                return "A node needs an ID.";

            if (TechTreeEditing.Find(_tree, newId) != null)
                return $"{newId} is already a node in this tree.";

            return TechTreeEditing.FindAny(_tree, _reference, newId) != null
                ? $"{newId} is already a node in the reference tree."
                : null;
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
            section.AddToClassList("sdk-section");
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
