using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Picker
{
    /// <summary>
    /// "Add Module" picker. A floating utility window with a search field, scrollable category list,
    /// and Cancel / Add Selected action buttons.
    /// </summary>
    /// <remarks>
    /// Opened via <see cref="Open" /> from the Modules tab. On confirm, the caller's callback fires
    /// with the selected <see cref="Type" /> and the window closes. Cancel closes without firing.
    /// </remarks>
    public sealed class AddModulePicker : EditorWindow
    {
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Inspectors/Picker/AddModulePicker.uss";

        private Action<Type> _onConfirm;
        private TextField _searchField;
        private ScrollView _listScroll;
        private Button _addButton;
        private ModuleCatalogEntry _selected;
        private string _filter = string.Empty;
        private readonly Dictionary<ModuleCatalogEntry, VisualElement> _rowByEntry = new();

        /// <summary>
        /// Opens the picker as a modal window and invokes <paramref name="onConfirm" /> when the
        /// user selects a module and presses Add. Does nothing if the user cancels.
        /// </summary>
        /// <remarks>
        /// <see cref="EditorWindow.ShowModal" /> blocks the editor until the window closes, which
        /// matches the design intent: the picker is a focused single-decision surface, not a
        /// reference panel the user keeps open alongside other work.
        /// </remarks>
        public static void Open(Action<Type> onConfirm)
        {
            var window = CreateInstance<AddModulePicker>();
            window.titleContent = new GUIContent("Add Module");
            window.minSize = new Vector2(360, 420);
            window._onConfirm = onConfirm;
            window.ShowModal();
        }

        private void CreateGUI()
        {
            rootVisualElement.AddToClassList("add-module-picker");
            Ksp2UnityToolsStyles.Apply(rootVisualElement, USS_PATH);

            _searchField = new TextField { value = string.Empty };
            _searchField.AddToClassList("add-module-search");
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _filter = evt.newValue ?? string.Empty;
                ApplyFilter();
            });
            rootVisualElement.Add(_searchField);

            var searchHint = new Label("Search modules...");
            searchHint.AddToClassList("add-module-search-hint");
            _searchField.Add(searchHint);
            _searchField.RegisterValueChangedCallback(evt =>
            {
                searchHint.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
            });

            _listScroll = new ScrollView(ScrollViewMode.Vertical);
            _listScroll.AddToClassList("add-module-list");
            rootVisualElement.Add(_listScroll);

            BuildList();

            var actionRow = new VisualElement();
            actionRow.AddToClassList("add-module-actions");

            var cancelBtn = new Button(Close) { text = "Cancel" };
            cancelBtn.AddToClassList("add-module-cancel-btn");
            actionRow.Add(cancelBtn);

            _addButton = new Button(ConfirmAndClose) { text = "Add Selected" };
            _addButton.AddToClassList("add-module-confirm-btn");
            _addButton.SetEnabled(false);
            actionRow.Add(_addButton);

            rootVisualElement.Add(actionRow);
        }

        private void BuildList()
        {
            _listScroll.Clear();
            _rowByEntry.Clear();

            foreach (var group in PartModuleCatalog.GetEntriesByCategory())
            {
                var section = new VisualElement();
                section.AddToClassList("add-module-category");

                var header = new Label(group.Key);
                header.AddToClassList("add-module-category-header");
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

        private VisualElement BuildEntryRow(ModuleCatalogEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList("add-module-entry");

            var name = new Label(entry.DisplayName);
            name.AddToClassList("add-module-entry-name");
            row.Add(name);

            if (!string.IsNullOrEmpty(entry.Description))
            {
                var desc = new Label(entry.Description);
                desc.AddToClassList("add-module-entry-desc");
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

        private void Select(ModuleCatalogEntry entry)
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
                    || entry.Description.ToLowerInvariant().Contains(lower);
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
                    if (child.ClassListContains("add-module-entry") && child.style.display != DisplayStyle.None)
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
            var type = _selected.ModuleType;
            var callback = _onConfirm;
            Close();
            callback?.Invoke(type);
        }
    }
}
