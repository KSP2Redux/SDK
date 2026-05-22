using System.Collections.Generic;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Editor field for a string SerializedProperty whose value names a GROUP of transforms
    /// (multiple transforms sharing the same <c>gameObject.name</c>) that the runtime resolves
    /// together via <c>FindModelTransforms</c>. Renders as an autocomplete text field showing
    /// the deduplicated names available in the part hierarchy, plus a match-count chip so the
    /// grouping is visible at a glance.
    /// </summary>
    /// <remarks>
    /// Storage is the bare leaf name. The count chip turns red when no transforms match the
    /// stored name, surfacing a typo or a renamed asset before runtime.
    /// </remarks>
    public sealed class TransformGroupField : VisualElement
    {
        private const string NAME_SUFFIX = " Name";

        private static readonly Color CHIP_OK = new(180f / 255f, 195f / 255f, 215f / 255f);
        private static readonly Color CHIP_NONE = new(220f / 255f, 130f / 255f, 130f / 255f);

        private readonly SerializedProperty _prop;
        private readonly Transform _partRoot;
        private readonly AutocompleteField _autocomplete;
        private readonly Label _matchChip;

        public TransformGroupField(SerializedProperty prop, string label, Transform partRoot)
        {
            _prop = prop;
            _partRoot = partRoot;

            AddToClassList("transform-group-field");
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;

            var cleanLabel = label != null && label.EndsWith(NAME_SUFFIX)
                ? label.Substring(0, label.Length - NAME_SUFFIX.Length)
                : label;

            _autocomplete = new AutocompleteField(prop, cleanLabel, EnumerateNames);
            _autocomplete.style.flexGrow = 1f;
            Add(_autocomplete);

            _matchChip = new Label();
            _matchChip.style.flexShrink = 0;
            _matchChip.style.marginLeft = 6f;
            _matchChip.style.fontSize = 11f;
            Add(_matchChip);

            UpdateChip(prop.stringValue);
            _autocomplete.TrackPropertyValue(prop, p => UpdateChip(p.stringValue));
        }

        private IEnumerable<string> EnumerateNames()
        {
            if (_partRoot == null)
            {
                yield break;
            }
            var seen = new HashSet<string>();
            foreach (var t in _partRoot.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (t == null)
                {
                    continue;
                }
                var n = t.gameObject.name;
                if (seen.Add(n))
                {
                    yield return n;
                }
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
            _matchChip.style.color = matches == 0 ? CHIP_NONE : CHIP_OK;
        }
    }
}
