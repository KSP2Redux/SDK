using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Widgets
{
    /// <summary>
    /// String editor with inline type-ahead autocomplete. Backs either a SerializedProperty or a plain in-memory value.
    /// </summary>
    /// <remarks>
    /// The suggestion list is hosted inside the same panel as the source field, attached to the
    /// topmost non-panel-root ancestor so it lives inside the inspector's USS scope (styles
    /// resolve) and renders on top of sibling rows / sections by being the last child of that
    /// ancestor. No separate <see cref="EditorWindow" />: that approach created focus-stealing
    /// flicker because the popup window competed with the inspector window for keyboard focus.
    ///
    /// In SerializedProperty mode commits edits via manual <c>Update</c>/<c>ApplyModifiedProperties</c>
    /// rather than <c>BindProperty</c> so the field stays out of the binding-context flicker zone
    /// alongside sibling <c>PropertyField</c> widgets. In plain-value mode it raises the
    /// <c>onValueChanged</c> callback instead, suitable for windows whose state is not held in a
    /// SerializedObject.
    ///
    /// Filtering uses case-insensitive <c>String.Contains</c> against the suggestion source.
    /// </remarks>
    public class AutocompleteField : VisualElement
    {
        private const int DEFAULT_MAX_VISIBLE_ROWS = 12;
        private const int RESULT_HARD_CAP = 500;
        private const float ROW_HEIGHT = 22f;

        private readonly SerializedProperty _prop;
        private readonly Action<string> _onValueChanged;
        private string _plainValue;

        private readonly Func<IEnumerable<string>> _source;
        private readonly int _maxVisibleRows;
        private readonly TextField _textField;
        private readonly ScrollView _suggestionList;
        private Rect _lastFieldBound;
        private bool _updatePolling;

        /// <summary>
        /// Creates a new <see cref="AutocompleteField" /> bound to <paramref name="prop" />.
        /// </summary>
        /// <param name="prop">The string SerializedProperty to read/write.</param>
        /// <param name="label">The author-facing label shown to the left of the field.</param>
        /// <param name="suggestionSource">
        /// Delegate returning the candidate strings each time the popup updates.
        /// </param>
        /// <param name="maxSuggestions">Maximum number of rows visible before the popup scrolls.</param>
        public AutocompleteField(
            SerializedProperty prop,
            string label,
            Func<IEnumerable<string>> suggestionSource,
            int maxSuggestions = DEFAULT_MAX_VISIBLE_ROWS)
            : this(label, prop?.stringValue ?? string.Empty, suggestionSource, maxSuggestions)
        {
            _prop = prop;
            _textField.TrackPropertyValue(prop, OnPropertyChanged);
        }

        /// <summary>
        /// Creates a new <see cref="AutocompleteField" /> backed by a plain in-memory value.
        /// </summary>
        /// <param name="initialValue">Starting value of the field.</param>
        /// <param name="label">The author-facing label shown to the left of the field.</param>
        /// <param name="suggestionSource">
        /// Delegate returning the candidate strings each time the popup updates.
        /// </param>
        /// <param name="onValueChanged">Raised whenever the field's value changes via typing or picking.</param>
        /// <param name="maxSuggestions">Maximum number of rows visible before the popup scrolls.</param>
        public AutocompleteField(
            string initialValue,
            string label,
            Func<IEnumerable<string>> suggestionSource,
            Action<string> onValueChanged,
            int maxSuggestions = DEFAULT_MAX_VISIBLE_ROWS)
            : this(label, initialValue ?? string.Empty, suggestionSource, maxSuggestions)
        {
            _onValueChanged = onValueChanged;
        }

        private AutocompleteField(
            string label,
            string initialValue,
            Func<IEnumerable<string>> suggestionSource,
            int maxSuggestions)
        {
            _source = suggestionSource;
            _maxVisibleRows = maxSuggestions > 0 ? maxSuggestions : DEFAULT_MAX_VISIBLE_ROWS;
            _plainValue = initialValue;

            AddToClassList("autocomplete-field");

            _textField = new TextField(label) { value = initialValue };
            _textField.AddToClassList("unity-base-field__aligned");
            _textField.RegisterValueChangedCallback(OnTextChanged);
            _textField.RegisterCallback<FocusInEvent>(OnFocusIn);
            _textField.RegisterCallback<FocusOutEvent>(OnFocusOut);
            _textField.RegisterCallback<KeyDownEvent>(OnKeyDown);
            Add(_textField);

            _suggestionList = new ScrollView(ScrollViewMode.Vertical);
            ApplyPopupStyles(_suggestionList);
            _suggestionList.style.maxHeight = ROW_HEIGHT * _maxVisibleRows + 4f;
            _suggestionList.style.display = DisplayStyle.None;

            RegisterCallback<DetachFromPanelEvent>(_ => HideSuggestions());
            _textField.RegisterCallback<GeometryChangedEvent>(_ => RepositionSuggestions());
        }

        /// <summary>Sets the field's value without raising the change callback.</summary>
        /// <remarks>
        /// Use this when external state (e.g. a selection change) needs to be reflected in the
        /// field without re-entering the change handler that drove that state.
        /// </remarks>
        public void SetValueWithoutNotify(string value)
        {
            string next = value ?? string.Empty;
            _plainValue = next;
            if (_textField.value != next)
            {
                _textField.SetValueWithoutNotify(next);
            }
        }

        private void OnTextChanged(ChangeEvent<string> evt)
        {
            string next = evt.newValue ?? string.Empty;
            if (_prop != null)
            {
                _prop.serializedObject.Update();
                _prop.stringValue = next;
                _prop.serializedObject.ApplyModifiedProperties();
            }
            else
            {
                _plainValue = next;
                _onValueChanged?.Invoke(next);
            }
            UpdateSuggestions(next);
        }

        private void OnPropertyChanged(SerializedProperty p)
        {
            if (_textField.value != p.stringValue)
            {
                _textField.SetValueWithoutNotify(p.stringValue);
            }
        }

        private void OnFocusIn(FocusInEvent _) => UpdateSuggestions(_textField.value);

        private void OnFocusOut(FocusOutEvent _) => HideSuggestions();

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                HideSuggestions();
                evt.StopPropagation();
            }
        }

        private void UpdateSuggestions(string filter)
        {
            var lower = (filter ?? string.Empty).Trim().ToLowerInvariant();
            var known = _source?.Invoke() ?? Array.Empty<string>();

            var matches = string.IsNullOrEmpty(lower)
                ? known.Where(n => n != null)
                : known.Where(n => n != null && n.ToLowerInvariant().Contains(lower));

            var top = matches
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Take(RESULT_HARD_CAP)
                .ToList();

            if (top.Count == 0 || (top.Count == 1 && string.Equals(top[0], filter, StringComparison.OrdinalIgnoreCase)))
            {
                HideSuggestions();
                return;
            }

            _suggestionList.Clear();
            foreach (var name in top)
            {
                _suggestionList.Add(BuildRow(name));
            }
            _suggestionList.scrollOffset = Vector2.zero;

            ShowSuggestions();
        }

        private VisualElement BuildRow(string option)
        {
            var row = new Label(option) { pickingMode = PickingMode.Position };
            ApplyRowStyles(row);

            var captured = option;
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                Pick(captured);
                evt.StopPropagation();
            });
            row.RegisterCallback<MouseEnterEvent>(_ =>
            {
                row.style.backgroundColor = new Color(95f / 255f, 150f / 255f, 210f / 255f, 0.55f);
            });
            row.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                row.style.backgroundColor = new Color(0, 0, 0, 0);
            });
            return row;
        }

        private void ShowSuggestions()
        {
            var popupParent = FindPopupParent();
            if (popupParent == null)
            {
                return;
            }
            if (_suggestionList.parent != popupParent)
            {
                _suggestionList.RemoveFromHierarchy();
                popupParent.Add(_suggestionList);
            }
            else
            {
                _suggestionList.BringToFront();
            }
            _suggestionList.style.display = DisplayStyle.Flex;
            RepositionSuggestions();
            StartPolling();
        }

        private void RepositionSuggestions()
        {
            if (_suggestionList.parent == null || _suggestionList.resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }
            // Anchor the popup to the inner input element rather than the whole TextField, so the
            // popup width matches the input box and not "label column + input" together.
            var input = _textField.Q(className: "unity-base-field__input") ?? _textField;
            var inputWorld = input.worldBound;
            var parentWorld = _suggestionList.parent.worldBound;
            if (float.IsNaN(inputWorld.xMin) || float.IsNaN(parentWorld.xMin))
            {
                return;
            }
            _suggestionList.style.left = inputWorld.xMin - parentWorld.xMin;
            _suggestionList.style.top = inputWorld.yMax - parentWorld.yMin + 2f;
            _suggestionList.style.width = inputWorld.width;
        }

        private void HideSuggestions()
        {
            _suggestionList.style.display = DisplayStyle.None;
            _suggestionList.RemoveFromHierarchy();
            StopPolling();
        }

        private void StartPolling()
        {
            if (_updatePolling)
            {
                return;
            }
            _updatePolling = true;
            EditorApplication.update += OnEditorUpdate;
        }

        private void StopPolling()
        {
            if (!_updatePolling)
            {
                return;
            }
            _updatePolling = false;
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (_suggestionList.parent == null || _suggestionList.resolvedStyle.display == DisplayStyle.None)
            {
                StopPolling();
                return;
            }
            var input = _textField.Q(className: "unity-base-field__input") ?? _textField;
            var fieldWorld = input.worldBound;
            if (fieldWorld != _lastFieldBound)
            {
                _lastFieldBound = fieldWorld;
                RepositionSuggestions();
            }
        }

        private void Pick(string name)
        {
            _textField.SetValueWithoutNotify(name);
            if (_prop != null)
            {
                _prop.serializedObject.Update();
                _prop.stringValue = name;
                _prop.serializedObject.ApplyModifiedProperties();
            }
            else
            {
                _plainValue = name;
                _onValueChanged?.Invoke(name);
            }
            HideSuggestions();
        }

        private VisualElement FindPopupParent()
        {
            // Walk up to the topmost ancestor below the panel root. For a CustomEditor, that's
            // the editor's root subtree - inside the inspector's StyleSheet scope, and tall
            // enough to overlay all sections / rows when the popup is the last child.
            var panelRoot = panel?.visualTree;
            VisualElement candidate = null;
            var current = parent;
            while (current != null && current != panelRoot)
            {
                candidate = current;
                current = current.parent;
            }
            return candidate;
        }

        private static void ApplyPopupStyles(VisualElement popup)
        {
            popup.style.position = Position.Absolute;
            popup.style.backgroundColor = new Color(28f / 255f, 32f / 255f, 40f / 255f);
            var border = new Color(85f / 255f, 100f / 255f, 125f / 255f);
            popup.style.borderLeftColor = border;
            popup.style.borderRightColor = border;
            popup.style.borderTopColor = border;
            popup.style.borderBottomColor = border;
            popup.style.borderLeftWidth = 1f;
            popup.style.borderRightWidth = 1f;
            popup.style.borderTopWidth = 1f;
            popup.style.borderBottomWidth = 1f;
            popup.style.borderTopLeftRadius = 3f;
            popup.style.borderTopRightRadius = 3f;
            popup.style.borderBottomLeftRadius = 3f;
            popup.style.borderBottomRightRadius = 3f;
            popup.style.paddingTop = 2f;
            popup.style.paddingBottom = 2f;
        }

        private static void ApplyRowStyles(VisualElement row)
        {
            row.style.color = Color.white;
            row.style.fontSize = 12f;
            row.style.height = ROW_HEIGHT;
            row.style.paddingLeft = 10f;
            row.style.paddingRight = 10f;
            row.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.style.backgroundColor = new Color(0, 0, 0, 0);
        }
    }
}
