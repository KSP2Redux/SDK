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
    /// topmost non-panel-root ancestor, so it lives inside the inspector's USS scope (styles
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
    /// Filtering ranks exact, prefix, contains, and normalized subsequence matches against the
    /// suggestion source, using case-insensitive comparisons.
    /// </remarks>
    public class AutocompleteField : VisualElement
    {
        private const int DEFAULT_MAX_VISIBLE_ROWS = 12;
        private const int RESULT_HARD_CAP = 500;
        private const float ROW_HEIGHT = 22f;
        private const string SUGGESTIONS_CLASS = "autocomplete-field__suggestions";
        private const string SUGGESTION_CLASS = "autocomplete-field__suggestion";
        private const string SUGGESTION_HIGHLIGHTED_CLASS = "autocomplete-field__suggestion--highlighted";
        private const string NO_MATCHES_CLASS = "autocomplete-field__no-matches";

        private readonly SerializedProperty? _prop;
        private readonly Action<string>? _onValueChanged;
        private readonly Func<IEnumerable<string>> _source;
        private readonly Func<string, string>? _detailSource;
        private readonly int _minCharacters;
        private readonly bool _showAllOnFocus;
        private readonly bool _preserveSourceOrderForEqualScores;
        private readonly TextField _textField;
        private readonly ScrollView _suggestionList;
        private readonly List<string> _currentSuggestions = new();
        
        private Rect _lastFieldBound;
        private bool _updatePolling;
        private int _highlightedIndex = -1;

        /// <summary>
        /// Raised when the author requests row submission from the keyboard, currently via Shift+Enter.
        /// </summary>
        public event Action<string>? SubmitRequested;

        /// <summary>
        /// Raised when the author requests deleting the current row from the keyboard, currently via Shift+Delete.
        /// </summary>
        public event Action? DeleteRequested;

        /// <summary>
        /// Creates a new <see cref="AutocompleteField" /> bound to <paramref name="prop" />.
        /// </summary>
        /// <param name="prop">The string SerializedProperty to read/write.</param>
        /// <param name="label">The author-facing label shown to the left of the field.</param>
        /// <param name="suggestionSource">
        /// Delegate returning the candidate strings each time the popup updates.
        /// </param>
        /// <param name="maxSuggestions">Maximum number of rows visible before the popup scrolls.</param>
        /// <param name="showAllOnFocus">When true, focusing an empty field opens the full suggestion list.</param>
        /// <param name="minCharacters">Minimum normalized characters required before filtering shows matches.</param>
        /// <param name="detailSource">Optional delegate returning subdued right-side detail text for a suggestion.</param>
        /// <param name="preserveSourceOrderForEqualScores">When true, equal-score suggestions keep source enumeration order.</param>
        public AutocompleteField(
            SerializedProperty prop,
            string? label,
            Func<IEnumerable<string>> suggestionSource,
            int maxSuggestions = DEFAULT_MAX_VISIBLE_ROWS,
            bool showAllOnFocus = true,
            int minCharacters = 0,
            Func<string, string>? detailSource = null,
            bool preserveSourceOrderForEqualScores = false)
            : this(label, prop.stringValue ?? string.Empty, suggestionSource, maxSuggestions, showAllOnFocus, minCharacters, detailSource, preserveSourceOrderForEqualScores)
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
        /// <param name="showAllOnFocus">When true, focusing an empty field opens the full suggestion list.</param>
        /// <param name="minCharacters">Minimum normalized characters required before filtering shows matches.</param>
        /// <param name="detailSource">Optional delegate returning subdued right-side detail text for a suggestion.</param>
        /// <param name="preserveSourceOrderForEqualScores">When true, equal-score suggestions keep source enumeration order.</param>
        public AutocompleteField(
            string initialValue,
            string? label,
            Func<IEnumerable<string>> suggestionSource,
            Action<string> onValueChanged,
            int maxSuggestions = DEFAULT_MAX_VISIBLE_ROWS,
            bool showAllOnFocus = true,
            int minCharacters = 0,
            Func<string, string>? detailSource = null,
            bool preserveSourceOrderForEqualScores = false)
            : this(label, initialValue ?? string.Empty, suggestionSource, maxSuggestions, showAllOnFocus, minCharacters, detailSource, preserveSourceOrderForEqualScores)
        {
            _onValueChanged = onValueChanged;
        }

        private AutocompleteField(
            string? label,
            string initialValue,
            Func<IEnumerable<string>> suggestionSource,
            int maxSuggestions,
            bool showAllOnFocus,
            int minCharacters,
            Func<string, string>? detailSource,
            bool preserveSourceOrderForEqualScores)
        {
            _source = suggestionSource;
            _detailSource = detailSource;
            var maxVisibleRows = maxSuggestions > 0 ? maxSuggestions : DEFAULT_MAX_VISIBLE_ROWS;
            _showAllOnFocus = showAllOnFocus;
            _minCharacters = Math.Max(0, minCharacters);
            _preserveSourceOrderForEqualScores = preserveSourceOrderForEqualScores;

            AddToClassList("autocomplete-field");

            _textField = new TextField(label ?? string.Empty) { value = initialValue };
            _textField.AddToClassList("unity-base-field__aligned");
            _textField.style.flexGrow = 1f;
            _textField.RegisterValueChangedCallback(OnTextChanged);
            _textField.RegisterCallback<FocusInEvent>(OnFocusIn);
            _textField.RegisterCallback<FocusOutEvent>(OnFocusOut);
            _textField.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Add(_textField);

            _suggestionList = new ScrollView(ScrollViewMode.Vertical);
            Ksp2UnityToolsStyles.Apply(_suggestionList);
            ApplyPopupStyles(_suggestionList);
            _suggestionList.style.maxHeight = ROW_HEIGHT * maxVisibleRows + 4f;
            _suggestionList.style.display = DisplayStyle.None;

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            _textField.RegisterCallback<GeometryChangedEvent>(OnTextFieldGeometryChanged);
        }

        /// <summary>Sets the field's value without raising the change callback.</summary>
        /// <remarks>
        /// Use this when external state (e.g. a selection change) needs to be reflected in the
        /// field without re-entering the change handler that drove that state.
        /// </remarks>
        public void SetValueWithoutNotify(string value)
        {
            var next = value ?? string.Empty;
            if (_textField.value != next)
            {
                _textField.SetValueWithoutNotify(next);
            }
        }

        /// <summary>Moves keyboard focus to the underlying text input.</summary>
        public void FocusInput()
        {
            _textField.Focus();
        }

        private void OnTextChanged(ChangeEvent<string> evt)
        {
            var next = evt.newValue ?? string.Empty;
            if (_prop != null)
            {
                _prop.serializedObject.Update();
                _prop.stringValue = next;
                _prop.serializedObject.ApplyModifiedProperties();
            }
            else
            {
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
                ConsumeKey(evt);
                return;
            }

            if (evt.keyCode == KeyCode.Delete && evt.shiftKey)
            {
                DeleteRequested?.Invoke();
                ConsumeKey(evt);
                return;
            }

            if (IsEnterKey(evt.keyCode) && evt.shiftKey)
            {
                EnsureSuggestionsAvailable();
                TryPickHighlightedOrFirst();
                SubmitRequested?.Invoke(_textField.value ?? string.Empty);
                ConsumeKey(evt);
                return;
            }

            if (IsEnterKey(evt.keyCode) || evt.keyCode == KeyCode.Tab)
            {
                EnsureSuggestionsAvailable();
                if (TryPickHighlightedOrFirst())
                {
                    ConsumeKey(evt);
                }
                return;
            }

            if (evt.keyCode == KeyCode.DownArrow)
            {
                if (MoveHighlight(1))
                {
                    ConsumeKey(evt);
                }
                return;
            }

            if (evt.keyCode == KeyCode.UpArrow)
            {
                if (MoveHighlight(-1))
                {
                    ConsumeKey(evt);
                }
            }
        }

        private void ConsumeKey(KeyDownEvent evt)
        {
            panel?.focusController?.IgnoreEvent(evt);
            evt.StopImmediatePropagation();
        }

        private static bool IsEnterKey(KeyCode keyCode)
        {
            return keyCode is KeyCode.Return or KeyCode.KeypadEnter;
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            HideSuggestions();
        }

        private void OnTextFieldGeometryChanged(GeometryChangedEvent evt)
        {
            RepositionSuggestions();
        }

        private void UpdateSuggestions(string filter)
        {
            var trimmedFilter = (filter ?? string.Empty).Trim();
            var lower = trimmedFilter.ToLowerInvariant();
            var normalizedFilter = NormalizeForFuzzyMatch(lower);

            if (string.IsNullOrEmpty(normalizedFilter) && !_showAllOnFocus)
            {
                HideSuggestions();
                return;
            }

            if (normalizedFilter.Length < _minCharacters)
            {
                HideSuggestions();
                return;
            }

            var previousHighlightedValue = _highlightedIndex >= 0 && _highlightedIndex < _currentSuggestions.Count
                ? _currentSuggestions[_highlightedIndex]
                : null;
            var known = _source.Invoke() ?? Array.Empty<string>();

            var matches = known
                .Where(n => n != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select((n, index) => new
                {
                    Name = n,
                    SourceIndex = index,
                    Score = ScoreMatch(n, lower, normalizedFilter)
                })
                .Where(match => match.Score > 0);

            List<string> top = (_preserveSourceOrderForEqualScores
                    ? matches
                        .OrderByDescending(match => match.Score)
                        .ThenBy(match => match.SourceIndex)
                    : matches
                        .OrderByDescending(match => match.Score)
                        .ThenBy(match => match.Name, StringComparer.OrdinalIgnoreCase))
                .Select(match => match.Name)
                .Take(RESULT_HARD_CAP)
                .ToList();

            if (top.Count == 0)
            {
                ShowNoMatches(trimmedFilter);
                return;
            }

            if (top.Count == 1 && string.Equals(top[0], trimmedFilter, StringComparison.OrdinalIgnoreCase))
            {
                HideSuggestions();
                return;
            }

            _currentSuggestions.Clear();
            _currentSuggestions.AddRange(top);
            _highlightedIndex = ResolveHighlightedIndex(top, previousHighlightedValue);
            _suggestionList.Clear();
            for (var i = 0; i < top.Count; i++)
            {
                _suggestionList.Add(BuildRow(top[i], i));
            }
            _suggestionList.scrollOffset = Vector2.zero;
            ApplyHighlight(scrollToHighlight: _highlightedIndex > 0);

            ShowSuggestions();
        }

        private int ResolveHighlightedIndex(List<string> suggestions, string? previousHighlightedValue)
        {
            if (!string.IsNullOrEmpty(previousHighlightedValue))
            {
                var previousIndex = suggestions.FindIndex(s => string.Equals(s, previousHighlightedValue, StringComparison.OrdinalIgnoreCase));
                if (previousIndex >= 0)
                {
                    return previousIndex;
                }
            }
            return suggestions.Count > 0 ? 0 : -1;
        }

        private void ShowNoMatches(string filter)
        {
            _currentSuggestions.Clear();
            _highlightedIndex = -1;

            if (string.IsNullOrEmpty(filter))
            {
                HideSuggestions();
                return;
            }

            _suggestionList.Clear();

            var noMatches = new Label("No matches");
            noMatches.AddToClassList(SUGGESTION_CLASS);
            noMatches.AddToClassList(NO_MATCHES_CLASS);
            noMatches.pickingMode = PickingMode.Ignore;

            _suggestionList.Add(noMatches);
            _suggestionList.scrollOffset = Vector2.zero;

            ShowSuggestions();
        }

        private static int ScoreMatch(string option, string lowerFilter, string normalizedFilter)
        {
            if (string.IsNullOrEmpty(normalizedFilter))
            {
                return 1;
            }

            var lowerOption = option.ToLowerInvariant();
            if (lowerOption == lowerFilter)
            {
                return 10000;
            }

            if (lowerOption.StartsWith(lowerFilter, StringComparison.Ordinal))
            {
                return 9000 - lowerOption.Length;
            }

            if (lowerOption.Contains(lowerFilter))
            {
                return 8000 - lowerOption.IndexOf(lowerFilter, StringComparison.Ordinal);
            }

            var normalizedOption = NormalizeForFuzzyMatch(lowerOption);
            var fuzzyScore = ScoreSubsequence(normalizedFilter, normalizedOption);
            return fuzzyScore > 0 ? 1000 + fuzzyScore : 0;
        }

        private static int ScoreSubsequence(string query, string candidate)
        {
            var queryIndex = 0;
            var score = 0;
            var consecutive = 0;

            for (var i = 0; i < candidate.Length && queryIndex < query.Length; i++)
            {
                if (candidate[i] != query[queryIndex])
                {
                    consecutive = 0;
                    continue;
                }

                score += 10 + consecutive * 5;
                consecutive++;
                queryIndex++;
            }

            if (queryIndex != query.Length)
            {
                return 0;
            }

            return score - candidate.Length;
        }

        private static string NormalizeForFuzzyMatch(string value)
        {
            return new string(value
                .Where(char.IsLetterOrDigit)
                .ToArray());
        }

        private VisualElement BuildRow(string option, int index)
        {
            string detail = _detailSource?.Invoke(option) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(detail))
            {
                var simpleRow = new Label(option)
                {
                    pickingMode = PickingMode.Position
                };

                ApplyRowStyles(simpleRow);

                simpleRow.RegisterCallback<MouseDownEvent, string>(OnRowMouseDown, option);
                simpleRow.RegisterCallback<MouseEnterEvent, int>(OnRowMouseEnter, index);

                return simpleRow;
            }

            var row = new VisualElement
            {
                pickingMode = PickingMode.Position
            };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 4f;
            row.style.paddingRight = 8f;
            ApplyRowStyles(row);

            var nameLabel = new Label(option)
            {
                pickingMode = PickingMode.Ignore
            };
            nameLabel.style.flexGrow = 1f;
            row.Add(nameLabel);

            var detailLabel = new Label(detail)
            {
                pickingMode = PickingMode.Ignore
            };
            detailLabel.style.color = new StyleColor(new Color(0.62f, 0.62f, 0.62f));
            detailLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            detailLabel.style.flexShrink = 0f;
            row.Add(detailLabel);

            row.RegisterCallback<MouseDownEvent, string>(OnRowMouseDown, option);
            row.RegisterCallback<MouseEnterEvent, int>(OnRowMouseEnter, index);

            return row;
        }

        private void OnRowMouseDown(MouseDownEvent evt, string option)
        {
            Pick(option);
            evt.StopPropagation();
        }

        private void OnRowMouseEnter(MouseEnterEvent evt, int index)
        {
            _highlightedIndex = index;
            ApplyHighlight();
        }

        private bool TryPickHighlightedOrFirst()
        {
            if (_suggestionList.parent == null ||
                _suggestionList.resolvedStyle.display == DisplayStyle.None ||
                _currentSuggestions.Count == 0)
            {
                return false;
            }

            var index = _highlightedIndex >= 0 && _highlightedIndex < _currentSuggestions.Count
                ? _highlightedIndex
                : 0;
            Pick(_currentSuggestions[index]);
            return true;
        }

        private bool EnsureSuggestionsAvailable()
        {
            if (_currentSuggestions.Count > 0 &&
                _suggestionList.parent != null &&
                _suggestionList.resolvedStyle.display != DisplayStyle.None)
            {
                return true;
            }

            UpdateSuggestions(_textField.value);
            return _currentSuggestions.Count > 0 &&
                _suggestionList.parent != null &&
                _suggestionList.resolvedStyle.display != DisplayStyle.None;
        }

        private bool MoveHighlight(int delta)
        {
            if (!EnsureSuggestionsAvailable())
            {
                return false;
            }

            _highlightedIndex += delta;
            if (_highlightedIndex < 0)
            {
                _highlightedIndex = _currentSuggestions.Count - 1;
            }
            else if (_highlightedIndex >= _currentSuggestions.Count)
            {
                _highlightedIndex = 0;
            }
            ApplyHighlight(scrollToHighlight: true);
            return true;
        }

        private void ApplyHighlight(bool scrollToHighlight = false)
        {
            var rows = _suggestionList.contentContainer;
            for (var i = 0; i < rows.childCount; i++)
            {
                var row = rows[i];
                var highlighted = i == _highlightedIndex;
                row.EnableInClassList(SUGGESTION_HIGHLIGHTED_CLASS, highlighted);
                if (highlighted && scrollToHighlight)
                {
                    _suggestionList.ScrollTo(row);
                }
            }
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
            _currentSuggestions.Clear();
            _highlightedIndex = -1;
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

        private void Pick(string selectedValue)
        {
            _textField.SetValueWithoutNotify(selectedValue);
            if (_prop != null)
            {
                _prop.serializedObject.Update();
                _prop.stringValue = selectedValue;
                _prop.serializedObject.ApplyModifiedProperties();
            }
            else
            {
                _onValueChanged?.Invoke(selectedValue);
            }
            HideSuggestions();
        }

        private VisualElement? FindPopupParent()
        {
            // Walk up to the topmost ancestor below the panel root. For a CustomEditor, that's
            // the editor's root subtree - inside the inspector's StyleSheet scope, and tall
            // enough to overlay all sections / rows when the popup is the last child.
            var panelRoot = panel?.visualTree;
            VisualElement? candidate = null;
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
            popup.AddToClassList(SUGGESTIONS_CLASS);
            popup.style.position = Position.Absolute;
        }

        private static void ApplyRowStyles(VisualElement row)
        {
            row.AddToClassList(SUGGESTION_CLASS);
            row.style.height = ROW_HEIGHT;
        }
    }
}
