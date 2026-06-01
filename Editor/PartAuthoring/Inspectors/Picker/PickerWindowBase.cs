using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Picker
{
    /// <summary>
    /// Shared scaffolding for "Add X" modal pickers: search field with placeholder hint,
    /// scrollable category-grouped entry list, select-on-click, double-click-confirm, and a
    /// Cancel / Add Selected action row at the bottom.
    /// </summary>
    /// <remarks>
    /// Subclasses define what catalog feeds the list, how an entry is named/categorized/described,
    /// and which <see cref="Type" /> is delivered to the confirm callback. The class-name prefix
    /// per subclass keeps USS rules isolated between the Module and Transformer pickers.
    /// </remarks>
    public abstract class PickerWindowBase<TEntry> : EditorWindow where TEntry : class
    {
        private Action<Type> _onConfirm;
        private ScrollView _listScroll;
        private Button _addButton;
        private TEntry _selected;
        private string _filter = string.Empty;
        private readonly Dictionary<TEntry, VisualElement> _rowByEntry = new();

        /// <summary>
        /// USS class-name prefix applied to every element built by this picker. Defaults to
        /// the shared <c>"picker-window"</c> chrome - subclasses only override when they
        /// genuinely need divergent styling.
        /// </summary>
        protected virtual string ClassPrefix => "picker-window";

        /// <summary>
        /// Placeholder hint shown inside the empty search field.
        /// </summary>
        protected abstract string SearchHintText { get; }

        /// <summary>
        /// SDK-relative path to the picker's USS stylesheet. Defaults to the shared chrome
        /// at <c>/Assets/Windows/PickerWindow.uss</c>.
        /// </summary>
        protected virtual string UssPath => "/Assets/Windows/PickerWindow.uss";

        /// <summary>
        /// Returns the catalog entries grouped and ordered for display.
        /// </summary>
        /// <returns>The grouped entries.</returns>
        protected abstract IEnumerable<IGrouping<string, TEntry>> GetEntriesByCategory();

        /// <summary>
        /// Returns the display name for an entry.
        /// </summary>
        /// <param name="entry">The catalog entry.</param>
        /// <returns>The entry's display name.</returns>
        protected abstract string GetDisplayName(TEntry entry);

        /// <summary>
        /// Returns the category bucket for an entry.
        /// </summary>
        /// <param name="entry">The catalog entry.</param>
        /// <returns>The entry's category.</returns>
        protected abstract string GetCategory(TEntry entry);

        /// <summary>
        /// Returns the one-line description for an entry, or empty when none is available.
        /// </summary>
        /// <param name="entry">The catalog entry.</param>
        /// <returns>The entry's description, possibly empty.</returns>
        protected abstract string GetDescription(TEntry entry);

        /// <summary>
        /// Returns the <see cref="Type" /> delivered to the confirm callback when an entry is selected.
        /// </summary>
        /// <param name="entry">The catalog entry.</param>
        /// <returns>The type the picker resolves the entry to.</returns>
        protected abstract Type GetTypeForEntry(TEntry entry);

        /// <summary>
        /// Subclass helper to instantiate the picker and show it modally.
        /// </summary>
        /// <typeparam name="TWindow">The picker subclass to instantiate.</typeparam>
        /// <param name="title">Title shown in the window's title bar.</param>
        /// <param name="onConfirm">Callback invoked with the selected entry's resolved type on confirm.</param>
        protected static void OpenWindow<TWindow>(string title, Action<Type> onConfirm)
            where TWindow : PickerWindowBase<TEntry>
        {
            var window = CreateInstance<TWindow>();
            window.titleContent = new GUIContent(title);
            window.minSize = new Vector2(360, 420);
            window._onConfirm = onConfirm;
            window.ShowModal();
        }

        private void CreateGUI()
        {
            rootVisualElement.AddToClassList(ClassPrefix);
            Ksp2UnityToolsStyles.Apply(rootVisualElement, UssPath);

            var searchField = new TextField { value = string.Empty };
            searchField.AddToClassList($"{ClassPrefix}-search");
            rootVisualElement.Add(searchField);

            var searchHint = new Label(SearchHintText);
            searchHint.AddToClassList($"{ClassPrefix}-search-hint");
            searchField.Add(searchHint);

            searchField.RegisterValueChangedCallback(evt =>
            {
                _filter = evt.newValue ?? string.Empty;
                searchHint.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
                ApplyFilter();
            });

            _listScroll = new ScrollView(ScrollViewMode.Vertical);
            _listScroll.AddToClassList($"{ClassPrefix}-list");
            rootVisualElement.Add(_listScroll);

            BuildList();

            var actionRow = new VisualElement();
            actionRow.AddToClassList($"{ClassPrefix}-actions");

            var cancelBtn = new Button(Close) { text = "Cancel" };
            cancelBtn.AddToClassList($"{ClassPrefix}-cancel-btn");
            actionRow.Add(cancelBtn);

            _addButton = new Button(ConfirmAndClose) { text = "Add Selected" };
            _addButton.AddToClassList($"{ClassPrefix}-confirm-btn");
            _addButton.SetEnabled(false);
            actionRow.Add(_addButton);

            rootVisualElement.Add(actionRow);
        }

        private void BuildList()
        {
            _listScroll.Clear();
            _rowByEntry.Clear();

            foreach (var group in GetEntriesByCategory())
            {
                var section = new VisualElement();
                section.AddToClassList($"{ClassPrefix}-category");

                var header = new Label(group.Key);
                header.AddToClassList($"{ClassPrefix}-category-header");
                section.Add(header);

                foreach (var entry in group.OrderBy(GetDisplayName, StringComparer.OrdinalIgnoreCase))
                {
                    var row = BuildEntryRow(entry);
                    section.Add(row);
                    _rowByEntry[entry] = row;
                }
                _listScroll.Add(section);
            }
        }

        private VisualElement BuildEntryRow(TEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList($"{ClassPrefix}-entry");

            var name = new Label(GetDisplayName(entry));
            name.AddToClassList($"{ClassPrefix}-entry-name");
            row.Add(name);

            var description = GetDescription(entry);
            if (!string.IsNullOrEmpty(description))
            {
                var desc = new Label(description);
                desc.AddToClassList($"{ClassPrefix}-entry-desc");
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

        private void Select(TEntry entry)
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
            var entryClass = $"{ClassPrefix}-entry";
            foreach (var (entry, row) in _rowByEntry)
            {
                var match = string.IsNullOrEmpty(lower)
                    || GetDisplayName(entry).ToLowerInvariant().Contains(lower)
                    || GetCategory(entry).ToLowerInvariant().Contains(lower)
                    || (GetDescription(entry) ?? string.Empty).ToLowerInvariant().Contains(lower);
                row.style.display = match ? DisplayStyle.Flex : DisplayStyle.None;
            }
            foreach (var section in _listScroll.Children())
            {
                var hasVisibleChild = false;
                foreach (var child in section.Children())
                {
                    if (child.ClassListContains(entryClass) && child.style.display != DisplayStyle.None)
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
            if (_selected == null) return;
            var type = GetTypeForEntry(_selected);
            var callback = _onConfirm;
            Close();
            callback?.Invoke(type);
        }
    }
}
