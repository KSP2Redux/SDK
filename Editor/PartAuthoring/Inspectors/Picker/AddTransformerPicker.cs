using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Picker
{
    /// <summary>
    /// "Add Transformer" picker. A floating utility window with a search field, scrollable category list, and Cancel / Add Selected action buttons.
    /// </summary>
    /// <remarks>
    /// Opened via <see cref="Open" /> from the Variants tab's transformer list. On confirm, the caller's callback fires with the selected <see cref="Type" /> and the window closes. Cancel closes without firing. Mirrors <see cref="AddModulePicker" /> structurally for visual and behavioural parity with the Modules-tab picker.
    /// </remarks>
    public sealed class AddTransformerPicker : EditorWindow
    {
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Inspectors/Picker/AddTransformerPicker.uss";

        private Action<Type> _onConfirm;
        private TextField _searchField;
        private ScrollView _listScroll;
        private Button _addButton;
        private TransformerCatalogEntry _selected;
        private string _filter = string.Empty;
        private readonly Dictionary<TransformerCatalogEntry, VisualElement> _rowByEntry = new();

        /// <summary>
        /// Opens the picker as a modal window and invokes <paramref name="onConfirm" /> when the user selects a transformer and presses Add. Does nothing if the user cancels.
        /// </summary>
        public static void Open(Action<Type> onConfirm)
        {
            var window = CreateInstance<AddTransformerPicker>();
            window.titleContent = new GUIContent("Add Transformer");
            window.minSize = new Vector2(360, 420);
            window._onConfirm = onConfirm;
            window.ShowModal();
        }

        private void CreateGUI()
        {
            rootVisualElement.AddToClassList("add-transformer-picker");
            Ksp2UnityToolsStyles.Apply(rootVisualElement, USS_PATH);

            _searchField = new TextField { value = string.Empty };
            _searchField.AddToClassList("add-transformer-search");
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _filter = evt.newValue ?? string.Empty;
                ApplyFilter();
            });
            rootVisualElement.Add(_searchField);

            var searchHint = new Label("Search transformers...");
            searchHint.AddToClassList("add-transformer-search-hint");
            _searchField.Add(searchHint);
            _searchField.RegisterValueChangedCallback(evt =>
            {
                searchHint.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
            });

            _listScroll = new ScrollView(ScrollViewMode.Vertical);
            _listScroll.AddToClassList("add-transformer-list");
            rootVisualElement.Add(_listScroll);

            BuildList();

            var actionRow = new VisualElement();
            actionRow.AddToClassList("add-transformer-actions");

            var cancelBtn = new Button(Close) { text = "Cancel" };
            cancelBtn.AddToClassList("add-transformer-cancel-btn");
            actionRow.Add(cancelBtn);

            _addButton = new Button(ConfirmAndClose) { text = "Add Selected" };
            _addButton.AddToClassList("add-transformer-confirm-btn");
            _addButton.SetEnabled(false);
            actionRow.Add(_addButton);

            rootVisualElement.Add(actionRow);
        }

        private void BuildList()
        {
            _listScroll.Clear();
            _rowByEntry.Clear();

            foreach (var group in TransformerCatalog.GetEntriesByCategory())
            {
                var section = new VisualElement();
                section.AddToClassList("add-transformer-category");

                var header = new Label(group.Key);
                header.AddToClassList("add-transformer-category-header");
                section.Add(header);

                foreach (var entry in group.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase))
                {
                    var row = BuildEntryRow(entry);
                    section.Add(row);
                    _rowByEntry[entry] = row;
                }

                _listScroll.Add(section);
            }
        }

        private VisualElement BuildEntryRow(TransformerCatalogEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList("add-transformer-entry");

            var name = new Label(entry.DisplayName);
            name.AddToClassList("add-transformer-entry-name");
            row.Add(name);

            if (!string.IsNullOrEmpty(entry.Description))
            {
                var desc = new Label(entry.Description);
                desc.AddToClassList("add-transformer-entry-desc");
                row.Add(desc);
            }

            row.RegisterCallback<ClickEvent>(_ => Select(entry));
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 2)
                {
                    Select(entry);
                    ConfirmAndClose();
                }
            });

            return row;
        }

        private void Select(TransformerCatalogEntry entry)
        {
            if (_selected != null && _rowByEntry.TryGetValue(_selected, out var oldRow))
            {
                oldRow.RemoveFromClassList("is-selected");
            }
            _selected = entry;
            if (_rowByEntry.TryGetValue(entry, out var newRow))
            {
                newRow.AddToClassList("is-selected");
            }
            _addButton.SetEnabled(true);
        }

        private void ApplyFilter()
        {
            var lower = _filter.Trim().ToLowerInvariant();
            foreach (var (entry, row) in _rowByEntry)
            {
                var match = string.IsNullOrEmpty(lower)
                    || entry.DisplayName.ToLowerInvariant().Contains(lower)
                    || entry.Category.ToLowerInvariant().Contains(lower)
                    || (entry.Description ?? string.Empty).ToLowerInvariant().Contains(lower);
                row.style.display = match ? DisplayStyle.Flex : DisplayStyle.None;
            }
            HideEmptyCategorySections();
        }

        private void HideEmptyCategorySections()
        {
            foreach (var section in _listScroll.Children())
            {
                var hasVisibleChild = false;
                foreach (var child in section.Children())
                {
                    if (child.ClassListContains("add-transformer-entry") && child.style.display != DisplayStyle.None)
                    {
                        hasVisibleChild = true;
                        break;
                    }
                }
                section.style.display = hasVisibleChild ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void ConfirmAndClose()
        {
            if (_selected == null)
            {
                return;
            }
            var type = _selected.TransformerType;
            var callback = _onConfirm;
            Close();
            callback?.Invoke(type);
        }
    }
}
