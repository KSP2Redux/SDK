using System.Collections.Generic;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// <see cref="AutocompleteField" /> specialised for the part layer on <see cref="KSP.Sim.Definitions.PartData" />.
    /// </summary>
    /// <remarks>
    /// Suggestions are the default layer and every part layer a campaign pack in the project selects.
    /// </remarks>
    public class PartLayerField : AutocompleteField
    {
        /// <summary>
        /// Creates a new <see cref="PartLayerField" /> bound to the given string property.
        /// </summary>
        /// <param name="prop">The string SerializedProperty to read/write.</param>
        /// <param name="label">The author-facing label shown to the left of the field.</param>
        public PartLayerField(SerializedProperty prop, string label)
            : base(prop, label, GetKnownLayers)
        {
            tooltip = "The part layer this copy of the part belongs to. Empty means the default layer.";
        }

        private static IEnumerable<string> GetKnownLayers() =>
            CampaignPackLayerCatalog.GetKnownLayers(campaignPack => campaignPack.PartLayers);
    }
}
