using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    public sealed class LinkedAddressableBrowserWindow : EditorWindow
    {
        private const string AllGroups = "All";
        private const string AllTypes = "All types";
        private const string GroupByDirectories = "Address folders";
        private const string GroupByLabels = "Labels";
        private const string Unlabeled = "Unlabeled";

        private readonly List<LinkedAddressableCatalogEntry> allEntries =
            new List<LinkedAddressableCatalogEntry>();
        private readonly List<LinkedAddressableCatalogEntry> visibleEntries =
            new List<LinkedAddressableCatalogEntry>();
        private readonly List<GroupItem> groupItems =
            new List<GroupItem>();

        private ToolbarSearchField searchField;
        private DropdownField groupingField;
        private DropdownField typeField;
        private ListView groupList;
        private ListView entryList;
        private Label groupHeader;
        private Label statusLabel;
        private Label detailAddress;
        private Label detailType;
        private Label detailProvider;
        private Label detailLabels;
        private Label detailInternalId;
        private Image detailIcon;
        private TextField outputDirectoryField;
        private Button createButton;
        private LinkedAddressableCatalogEntry selectedEntry;
        private string selectedGroup = AllGroups;

        [MenuItem("Modding/Linked Addressables/Open Browser...", false, 100)]
        private static void Open()
        {
            var window = GetWindow<LinkedAddressableBrowserWindow>();
            window.titleContent = new GUIContent("Linked Addressables");
            window.minSize = new Vector2(760f, 440f);
            window.Show();
        }

        private void OnEnable()
        {
            LinkedAddressableEditorCatalog.Changed -= OnCatalogChanged;
            LinkedAddressableEditorCatalog.Changed += OnCatalogChanged;
            EditorApplication.delayCall -= LoadCatalogAfterOpen;
            EditorApplication.delayCall += LoadCatalogAfterOpen;
        }

        private void OnDisable()
        {
            LinkedAddressableEditorCatalog.Changed -= OnCatalogChanged;
            EditorApplication.delayCall -= LoadCatalogAfterOpen;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1f;

            var toolbar = new Toolbar();
            searchField = new ToolbarSearchField
            {
                tooltip =
                    "Search address, label, type, or provider. "
                    + "Use t:TypeName or l:Label for focused filters."
            };
            searchField.style.flexGrow = 1f;
            searchField.RegisterValueChangedCallback(_ => ApplyFilters());
            toolbar.Add(searchField);

            groupingField = new DropdownField("Group")
            {
                choices = new List<string>
                {
                    GroupByDirectories,
                    GroupByLabels
                },
                index = 0
            };
            groupingField.style.minWidth = 190f;
            groupingField.RegisterValueChangedCallback(_ =>
            {
                selectedGroup = AllGroups;
                ApplyFilters();
            });
            toolbar.Add(groupingField);

            typeField = new DropdownField("Type")
            {
                choices = new List<string> { AllTypes },
                index = 0
            };
            typeField.style.minWidth = 210f;
            typeField.RegisterValueChangedCallback(_ => ApplyFilters());
            toolbar.Add(typeField);

            var refreshButton = new Button(() => RefreshCatalog(true))
            {
                text = "Refresh Catalog",
                tooltip =
                    "Reload the catalog from ThunderKit's configured Addressables source."
            };
            toolbar.Add(refreshButton);
            rootVisualElement.Add(toolbar);

            statusLabel = new Label("Loading external Addressables catalog…");
            statusLabel.style.paddingLeft = 6f;
            statusLabel.style.paddingRight = 6f;
            statusLabel.style.paddingTop = 4f;
            statusLabel.style.paddingBottom = 4f;
            rootVisualElement.Add(statusLabel);

            var splitView = new TwoPaneSplitView(
                0,
                220f,
                TwoPaneSplitViewOrientation.Horizontal
            );
            splitView.style.flexGrow = 1f;
            splitView.Add(BuildGroupPane());
            splitView.Add(BuildAssetPane());
            rootVisualElement.Add(splitView);

            rootVisualElement.Add(BuildDetailsPane());
            UpdateDetails();
        }

        private VisualElement BuildGroupPane()
        {
            var pane = new VisualElement();
            pane.style.flexGrow = 1f;
            groupHeader = CreateHeader("Address folders");
            pane.Add(groupHeader);

            groupList = new ListView
            {
                itemsSource = groupItems,
                fixedItemHeight = 22f,
                virtualizationMethod =
                    CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                makeItem = () => new Label(),
                bindItem = (element, index) =>
                {
                    var item = groupItems[index];
                    var label = (Label)element;
                    label.text = $"{item.Name} ({item.Count})";
                    label.tooltip = item.Name;
                    label.style.paddingLeft = 5f;
                    label.style.unityTextAlign = TextAnchor.MiddleLeft;
                }
            };
            groupList.selectionChanged += selection =>
            {
                var item = selection.OfType<GroupItem>().FirstOrDefault();
                selectedGroup = item?.Name ?? AllGroups;
                ApplyEntryFilter();
            };
            pane.Add(groupList);
            return pane;
        }

        private VisualElement BuildAssetPane()
        {
            var pane = new VisualElement();
            pane.style.flexGrow = 1f;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.height = 24f;
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);
            header.Add(CreateColumnLabel("Address", 3f));
            header.Add(CreateColumnLabel("Type", 1.5f));
            header.Add(CreateColumnLabel("Provider", 1.5f));
            pane.Add(header);

            entryList = new ListView
            {
                itemsSource = visibleEntries,
                fixedItemHeight = 34f,
                virtualizationMethod =
                    CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds =
                    AlternatingRowBackground.ContentOnly,
                makeItem = CreateEntryRow,
                bindItem = BindEntryRow
            };
            entryList.selectionChanged += selection =>
            {
                selectedEntry = selection
                    .OfType<LinkedAddressableCatalogEntry>()
                    .FirstOrDefault();
                UpdateDetails();
            };
            entryList.itemsChosen += _ => CreateSelectedLink();
            pane.Add(entryList);
            return pane;
        }

        private VisualElement BuildDetailsPane()
        {
            var details = new VisualElement();
            details.style.minHeight = 142f;
            details.style.paddingLeft = 8f;
            details.style.paddingRight = 8f;
            details.style.paddingTop = 6f;
            details.style.paddingBottom = 6f;
            details.style.borderTopWidth = 1f;
            details.style.borderTopColor = new Color(0.25f, 0.25f, 0.25f);

            var identityRow = new VisualElement();
            identityRow.style.flexDirection = FlexDirection.Row;
            detailIcon = new Image
            {
                scaleMode = ScaleMode.ScaleToFit
            };
            detailIcon.style.width = 48f;
            detailIcon.style.height = 48f;
            detailIcon.style.marginRight = 8f;
            identityRow.Add(detailIcon);

            var labels = new VisualElement();
            labels.style.flexGrow = 1f;
            detailAddress = new Label();
            detailAddress.style.unityFontStyleAndWeight = FontStyle.Bold;
            detailType = new Label();
            detailProvider = new Label();
            detailLabels = new Label();
            labels.Add(detailAddress);
            labels.Add(detailType);
            labels.Add(detailProvider);
            labels.Add(detailLabels);
            identityRow.Add(labels);
            details.Add(identityRow);

            detailInternalId = new Label();
            detailInternalId.style.marginTop = 4f;
            detailInternalId.style.whiteSpace = WhiteSpace.NoWrap;
            detailInternalId.style.overflow = Overflow.Hidden;
            detailInternalId.style.textOverflow = TextOverflow.Ellipsis;
            details.Add(detailInternalId);

            var actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.marginTop = 6f;
            outputDirectoryField = new TextField("Output")
            {
                value = LinkedAddressableAssetUtility.DefaultOutputDirectory
            };
            outputDirectoryField.style.flexGrow = 1f;
            actionRow.Add(outputDirectoryField);

            createButton = new Button(CreateSelectedLink)
            {
                text = "Create Linked Asset"
            };
            createButton.style.marginLeft = 6f;
            actionRow.Add(createButton);
            details.Add(actionRow);
            return details;
        }

        private static Label CreateHeader(string text)
        {
            var label = new Label(text);
            label.style.height = 24f;
            label.style.paddingLeft = 5f;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.borderBottomWidth = 1f;
            label.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);
            return label;
        }

        private static Label CreateColumnLabel(string text, float grow)
        {
            var label = new Label(text);
            label.style.flexGrow = grow;
            label.style.flexBasis = 0f;
            label.style.paddingLeft = 5f;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            return label;
        }

        private static VisualElement CreateEntryRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.Add(CreateEntryLabel("address", 3f));
            row.Add(CreateEntryLabel("type", 1.5f));
            row.Add(CreateEntryLabel("provider", 1.5f));
            return row;
        }

        private static Label CreateEntryLabel(string name, float grow)
        {
            var label = new Label { name = name };
            label.style.flexGrow = grow;
            label.style.flexBasis = 0f;
            label.style.paddingLeft = 5f;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            return label;
        }

        private void BindEntryRow(VisualElement element, int index)
        {
            var entry = visibleEntries[index];
            BindEntryLabel(
                element.Q<Label>("address"),
                entry.Address,
                entry.Address
            );
            BindEntryLabel(
                element.Q<Label>("type"),
                entry.AssetType.Name,
                entry.AssetType.FullName
            );
            BindEntryLabel(
                element.Q<Label>("provider"),
                ShortProviderName(entry.ProviderId),
                entry.ProviderId
            );
        }

        private static void BindEntryLabel(
            Label label,
            string text,
            string tooltip
        )
        {
            label.text = text;
            label.tooltip = tooltip;
        }

        private void LoadCatalogAfterOpen()
        {
            if (this != null)
                RefreshCatalog(false);
        }

        private void RefreshCatalog(bool force)
        {
            SetStatus("Loading external Addressables catalog…", false);
            try
            {
                if (force)
                    LinkedAddressableEditorCatalog.Refresh();
                else
                    LinkedAddressableEditorCatalog.EnsureLoaded();

                PopulateCatalog();
            }
            catch (Exception exception)
            {
                allEntries.Clear();
                visibleEntries.Clear();
                groupItems.Clear();
                RebuildLists();
                SetStatus(exception.Message, true);
                Debug.LogException(exception);
            }
        }

        private void OnCatalogChanged()
        {
            if (rootVisualElement?.panel != null)
                PopulateCatalog();
        }

        private void PopulateCatalog()
        {
            allEntries.Clear();
            allEntries.AddRange(LinkedAddressableEditorCatalog.Entries);

            var typeChoices = allEntries
                .Select(entry => entry.AssetType.FullName)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(type => type, StringComparer.Ordinal)
                .Prepend(AllTypes)
                .ToList();
            var previousType = typeField?.value;
            if (typeField != null)
            {
                typeField.choices = typeChoices;
                typeField.SetValueWithoutNotify(
                    typeChoices.Contains(previousType)
                        ? previousType
                        : AllTypes
                );
            }

            ApplyFilters();
            SetStatus(
                $"{allEntries.Count:N0} linkable assets from "
                    + $"'{LinkedAddressableEditorCatalog.SourceRoot}'",
                false
            );
        }

        private void ApplyFilters()
        {
            var searchMatches = allEntries
                .Where(MatchesTypeDropdown)
                .Where(MatchesSearch)
                .ToList();

            if (groupHeader != null)
            {
                groupHeader.text = IsGroupingByLabels
                    ? GroupByLabels
                    : GroupByDirectories;
            }
            groupItems.Clear();
            groupItems.Add(new GroupItem(AllGroups, searchMatches.Count));
            groupItems.AddRange(
                searchMatches
                    .SelectMany(
                        entry =>
                            GetGroups(entry).Select(
                                group => new
                                {
                                    Group = group,
                                    Entry = entry
                                }
                            )
                    )
                    .GroupBy(item => item.Group, StringComparer.Ordinal)
                    .Select(
                        group =>
                            new GroupItem(
                                group.Key,
                                group.Select(item => item.Entry).Distinct().Count()
                            )
                    )
                    .OrderBy(
                        item => item.Name,
                        StringComparer.OrdinalIgnoreCase
                    )
            );

            if (
                !groupItems.Any(
                    item =>
                        string.Equals(
                            item.Name,
                            selectedGroup,
                            StringComparison.Ordinal
                        )
                )
            )
            {
                selectedGroup = AllGroups;
            }

            groupList?.Rebuild();
            if (groupList != null)
            {
                groupList.selectedIndex = groupItems.FindIndex(
                    item =>
                        string.Equals(
                            item.Name,
                            selectedGroup,
                            StringComparison.Ordinal
                        )
                );
            }

            ApplyEntryFilter(searchMatches);
        }

        private void ApplyEntryFilter()
        {
            ApplyEntryFilter(
                allEntries
                    .Where(MatchesTypeDropdown)
                    .Where(MatchesSearch)
            );
        }

        private void ApplyEntryFilter(
            IEnumerable<LinkedAddressableCatalogEntry> searchMatches
        )
        {
            visibleEntries.Clear();
            visibleEntries.AddRange(
                searchMatches.Where(
                    entry =>
                        selectedGroup == AllGroups
                        || GetGroups(entry).Contains(
                            selectedGroup,
                            StringComparer.Ordinal
                        )
                )
            );

            selectedEntry = null;
            entryList?.ClearSelection();
            entryList?.Rebuild();
            UpdateDetails();
        }

        private bool MatchesTypeDropdown(
            LinkedAddressableCatalogEntry entry
        )
        {
            return typeField == null
                || string.IsNullOrWhiteSpace(typeField.value)
                || typeField.value == AllTypes
                || string.Equals(
                    entry.AssetType.FullName,
                    typeField.value,
                    StringComparison.Ordinal
                );
        }

        private bool MatchesSearch(LinkedAddressableCatalogEntry entry)
        {
            var search = searchField?.value;
            if (string.IsNullOrWhiteSpace(search))
                return true;

            foreach (
                var term in search.Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries
                )
            )
            {
                if (term.StartsWith("t:", StringComparison.OrdinalIgnoreCase))
                {
                    var typeTerm = term.Substring(2);
                    if (
                        !ContainsIgnoreCase(
                            entry.AssetType.FullName,
                            typeTerm
                        )
                    )
                    {
                        return false;
                    }
                }
                else if (
                    term.StartsWith(
                        "l:",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    var labelTerm = term.Substring(2);
                    if (
                        !entry.Labels.Any(
                            label =>
                                ContainsIgnoreCase(label, labelTerm)
                        )
                    )
                    {
                        return false;
                    }
                }
                else if (
                    !ContainsIgnoreCase(entry.Address, term)
                    && !ContainsIgnoreCase(
                        entry.AssetType.FullName,
                        term
                    )
                    && !ContainsIgnoreCase(entry.ProviderId, term)
                    && !entry.Labels.Any(
                        label => ContainsIgnoreCase(label, term)
                    )
                )
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return source?.IndexOf(
                    value ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase
                )
                >= 0;
        }

        private void UpdateDetails()
        {
            var hasSelection = selectedEntry != null;
            if (detailAddress != null)
            {
                detailAddress.text = hasSelection
                    ? selectedEntry.Address
                    : "Select an asset";
                detailType.text = hasSelection
                    ? selectedEntry.AssetType.FullName
                    : string.Empty;
                detailProvider.text = hasSelection
                    ? selectedEntry.ProviderId
                    : string.Empty;
                detailLabels.text = hasSelection
                    ? $"Labels: "
                        + (
                            selectedEntry.Labels.Count > 0
                                ? string.Join(", ", selectedEntry.Labels)
                                : Unlabeled
                        )
                    : string.Empty;
                detailInternalId.text = hasSelection
                    ? $"Internal ID: {selectedEntry.InternalId}"
                    : string.Empty;
                detailInternalId.tooltip = hasSelection
                    ? selectedEntry.InternalId
                    : null;
                detailIcon.image = hasSelection
                    ? EditorGUIUtility.ObjectContent(
                        null,
                        selectedEntry.AssetType
                    ).image
                    : null;
            }

            createButton?.SetEnabled(hasSelection);
        }

        private void CreateSelectedLink()
        {
            if (selectedEntry == null)
                return;

            try
            {
                var assetPath = LinkedAddressableAssetUtility.CreateLink(
                    selectedEntry,
                    outputDirectoryField.value.Trim()
                );
                var linkedAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                Selection.activeObject = linkedAsset;
                EditorGUIUtility.PingObject(linkedAsset);
                SetStatus($"Created '{assetPath}'.", false);
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message, true);
                Debug.LogException(exception);
            }
        }

        private void RebuildLists()
        {
            groupList?.Rebuild();
            entryList?.Rebuild();
            UpdateDetails();
        }

        private bool IsGroupingByLabels =>
            string.Equals(
                groupingField?.value,
                GroupByLabels,
                StringComparison.Ordinal
            );

        private IEnumerable<string> GetGroups(
            LinkedAddressableCatalogEntry entry
        )
        {
            if (!IsGroupingByLabels)
                return new[] { entry.Directory };

            return entry.Labels.Count > 0
                ? entry.Labels
                : new[] { Unlabeled };
        }

        private void SetStatus(string message, bool error)
        {
            if (statusLabel == null)
                return;

            statusLabel.text = message;
            statusLabel.tooltip = message;
            if (error)
                statusLabel.style.color = new Color(1f, 0.45f, 0.4f);
            else
                statusLabel.style.color = StyleKeyword.Null;
        }

        private static string ShortProviderName(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
                return string.Empty;

            var separator = provider.LastIndexOf('.');
            return separator >= 0
                ? provider.Substring(separator + 1)
                : provider;
        }

        private sealed class GroupItem
        {
            public GroupItem(string name, int count)
            {
                Name = name;
                Count = count;
            }

            public string Name { get; }

            public int Count { get; }
        }
    }
}
