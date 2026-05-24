using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// <see cref="AutocompleteField" /> specialised for the part-family string field on
    /// <see cref="KSP.Sim.Definitions.PartData" />. Suggestions come from
    /// <see cref="PartFamilyCatalog" />.
    /// </summary>
    public sealed class FamilyField : AutocompleteField
    {
        /// <summary>Creates a new <see cref="FamilyField" /> bound to the given string property.</summary>
        /// <param name="prop">The string SerializedProperty to read/write.</param>
        /// <param name="label">The author-facing label shown to the left of the field.</param>
        public FamilyField(SerializedProperty prop, string label)
            : base(prop, label, PartFamilyCatalog.GetKnownFamilies)
        {
        }
    }
}
