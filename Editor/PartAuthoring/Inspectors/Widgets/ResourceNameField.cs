using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// <see cref="AutocompleteField" /> specialised for resource definition names. Suggestions
    /// come from <see cref="ResourceNameCatalog" />, which loads addressables labelled
    /// <c>"resources"</c>.
    /// </summary>
    public sealed class ResourceNameField : AutocompleteField
    {
        /// <summary>
        /// Creates a new <see cref="ResourceNameField" /> bound to the given string property.
        /// </summary>
        /// <param name="prop">The string SerializedProperty to read/write.</param>
        /// <param name="label">The author-facing label shown to the left of the field.</param>
        public ResourceNameField(SerializedProperty prop, string label)
            : base(prop, label, ResourceNameCatalog.GetKnownResources)
        {
        }
    }
}
