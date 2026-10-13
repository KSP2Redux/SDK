using System;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;

namespace Ksp2UnityTools.Editor.Modding
{
    /// <summary>
    /// <see cref="AutocompleteField" /> for a feature id, suggesting from <see cref="FeatureCatalog" /> with each
    /// suggestion's mod beside it.
    /// </summary>
    public class FeatureField : AutocompleteField
    {
        /// <summary>
        /// Creates a field bound to a string property.
        /// </summary>
        /// <param name="prop">The string SerializedProperty to read and write.</param>
        /// <param name="label">The label shown to the left of the field, or null for none.</param>
        /// <param name="owner">The mod the edited asset or feature belongs to, or null for Redux's own content.</param>
        public FeatureField(SerializedProperty prop, string label, Mod owner)
            : base(prop, label, () => FeatureCatalog.SuggestionsFor(owner), detailSource: FeatureCatalog.DetailFor)
        {
        }

        /// <summary>
        /// Creates a field backed by a plain value.
        /// </summary>
        /// <param name="initialValue">The starting value.</param>
        /// <param name="label">The label shown to the left of the field, or null for none.</param>
        /// <param name="owner">The mod the edited asset belongs to, or null for Redux's own content.</param>
        /// <param name="onValueChanged">Raised when the value changes by typing or picking.</param>
        public FeatureField(string initialValue, string label, Mod owner, Action<string> onValueChanged)
            : base(
                initialValue,
                label,
                () => FeatureCatalog.SuggestionsFor(owner),
                onValueChanged,
                detailSource: FeatureCatalog.DetailFor)
        {
        }
    }
}
