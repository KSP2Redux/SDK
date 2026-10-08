using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Editor field for a string SerializedProperty whose value names a group of transforms (multiple transforms sharing the same <c>gameObject.name</c>) that the runtime resolves together via <c>FindModelTransforms</c>.
    /// </summary>
    /// <remarks>
    /// Renders as an autocomplete text field showing the deduplicated names available in the part hierarchy, plus a match-count chip so the grouping is visible at a glance. Storage is the bare leaf name. The count chip turns red when no transforms match the stored name, surfacing a typo or a renamed asset before runtime.
    /// </remarks>
    public sealed class TransformGroupField : VisualElement
    {
        private const string NAME_SUFFIX = " Name";
        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Inspectors/Widgets/TransformGroupField.uss";

        private readonly SerializedProperty _prop;
        private readonly Transform _partRoot;
        private readonly AutocompleteField _autocomplete;
        private readonly Label _matchChip;

        /// <summary>
        /// Creates a new <see cref="TransformGroupField" /> bound to the given string property.
        /// </summary>
        /// <param name="prop">The string SerializedProperty holding the transform group name.</param>
        /// <param name="label">The author-facing label. A trailing " Name" suffix is stripped for display.</param>
        /// <param name="partRoot">The part root used to enumerate candidate transform names and count matches.</param>
        public TransformGroupField(SerializedProperty prop, string label, Transform partRoot)
        {
            _prop = prop;
            _partRoot = partRoot;

            AddToClassList("transform-group-field");
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + USS_PATH);
            if (sheet != null) styleSheets.Add(sheet);

            var cleanLabel = label != null && label.EndsWith(NAME_SUFFIX)
                ? label.Substring(0, label.Length - NAME_SUFFIX.Length)
                : label;

            _autocomplete = new AutocompleteField(prop, cleanLabel, EnumerateNames);
            _autocomplete.AddToClassList("transform-group-field__autocomplete");
            Add(_autocomplete);

            _matchChip = new Label();
            _matchChip.AddToClassList("transform-group-field__match-chip");
            Add(_matchChip);

            UpdateChip(prop.stringValue);
            _autocomplete.TrackPropertyValue(prop, p => UpdateChip(p.stringValue));
        }

        private IEnumerable<string> EnumerateNames()
        {
            if (_partRoot == null) yield break;
            var seen = new HashSet<string>();
            foreach (var t in _partRoot.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (t == null) continue;
                var n = t.gameObject.name;
                if (seen.Add(n)) yield return n;
            }
        }

        private void UpdateChip(string transformName)
        {
            if (string.IsNullOrEmpty(transformName) || _partRoot == null)
            {
                _matchChip.text = string.Empty;
                _matchChip.style.display = DisplayStyle.None;
                return;
            }
            var matches = _partRoot.FindChildren(transformName).Count;
            _matchChip.text = matches == 1 ? "1 match" : $"{matches} matches";
            _matchChip.style.display = DisplayStyle.Flex;
            _matchChip.EnableInClassList("transform-group-field__match-chip--ok", matches > 0);
            _matchChip.EnableInClassList("transform-group-field__match-chip--none", matches == 0);
        }
    }
}
