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
    /// String SerializedProperty editor with inline type-ahead autocomplete.
    /// </summary>
    /// <remarks>
    /// The suggestion list is hosted inside the same panel as the source field, attached to the
    /// topmost non-panel-root ancestor so it lives inside the inspector's USS scope (styles
    /// resolve) and renders on top of sibling rows / sections by being the last child of that
    /// ancestor. No separate <see cref="EditorWindow" />: that approach created focus-stealing
    /// flicker because the popup window competed with the inspector window for keyboard focus.
    ///
    /// Commits edits via manual <c>Update</c>/<c>ApplyModifiedProperties</c> rather than
    /// <c>BindProperty</c> so the field stays out of the binding-context flicker zone alongside
    /// sibling <c>PropertyField</c> widgets.
    /// </remarks>
    public class AutocompleteField : VisualElement
    {
        private const int DEFAULT_MAX_SUGGESTIONS = 12;
        private const float ROW_HEIGHT = 22f;

        private readonly SerializedProperty _prop;
        private readonly Func<IEnumerable<string>> _source;
        private readonly int _maxSuggestions;
        private readonly TextField _textField;
        private readonly VisualElement _suggestionList;
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
        /// <param name="maxSuggestions">Maximum number of matches shown at a time.</param>
        public AutocompleteField(
            SerializedProperty prop,
            string label,
            Func<IEnumerable<string>> suggestionSource,
            int maxSuggestions = DEFAULT_MAX_SUGGESTIONS)
        {
            _prop = prop;
            _source = suggestionSource;
            _maxSuggestions = maxSuggestions;

            AddToClassList("autocomplete-field");

            _textField = new TextField(label) { value = prop.stringValue };
            _textField.AddToClassList("unity-base-field__aligned");
            _textField.RegisterValueChangedCallback(OnTextChanged);
            _textField.RegisterCallback<FocusInEvent>(OnFocusIn);
            _textField.RegisterCallback<FocusOutEvent>(OnFocusOut);
            _textField.RegisterCallback<KeyDownEvent>(OnKeyDown);
            _textField.TrackPropertyValue(prop, OnPropertyChanged);
            Add(_textField);

            _suggestionList = new VisualElement();
            ApplyPopupStyles(_suggestionList);
            _suggestionList.style.display = DisplayStyle.None;

            RegisterCallback<DetachFromPanelEvent>(_ => HideSuggestions());
            _textField.RegisterCallback<GeometryChangedEvent>(_ => RepositionSuggestions());
        }

        private void OnTextChanged(ChangeEvent<string> evt)
        {
            _prop.serializedObject.Update();
            _prop.stringValue = evt.newValue ?? string.Empty;
            _prop.serializedObject.ApplyModifiedProperties();
            UpdateSuggestions(evt.newValue);
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
                ? known
                : known.Where(n => n != null && n.ToLowerInvariant().Contains(lower));

            var top = matches.Take(_maxSuggestions).ToList();

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
            _prop.serializedObject.Update();
            _prop.stringValue = name;
            _prop.serializedObject.ApplyModifiedProperties();
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
