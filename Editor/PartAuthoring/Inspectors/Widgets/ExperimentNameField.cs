using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// <see cref="AutocompleteField" /> specialised for science experiment IDs.
    /// </summary>
    /// <remarks>
    /// Suggestions come from <see cref="ExperimentNameCatalog" />, which loads addressables labelled <c>"scienceExperiment"</c>.
    /// </remarks>
    public sealed class ExperimentNameField : AutocompleteField
    {
        /// <summary>
        /// Creates a new <see cref="ExperimentNameField" /> bound to the given string property.
        /// </summary>
        /// <param name="prop">The string SerializedProperty to read/write.</param>
        /// <param name="label">The author-facing label shown to the left of the field.</param>
        public ExperimentNameField(SerializedProperty prop, string label)
            : base(prop, label, ExperimentNameCatalog.GetKnownExperiments)
        {
        }
    }
}
