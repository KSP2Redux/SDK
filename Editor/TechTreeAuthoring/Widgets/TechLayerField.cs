using System.Collections.Generic;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Widgets
{
    /// <summary>
    /// <see cref="AutocompleteField" /> specialised for a tech tree's layer.
    /// </summary>
    /// <remarks>
    /// Suggestions are the default layer and every tech tree layer a campaign pack in the project selects.
    /// </remarks>
    public class TechLayerField : AutocompleteField
    {
        /// <summary>
        /// Creates a new <see cref="TechLayerField" /> bound to the given string property.
        /// </summary>
        /// <param name="prop">The string SerializedProperty to read/write.</param>
        /// <param name="label">The author-facing label shown to the left of the field.</param>
        public TechLayerField(SerializedProperty prop, string label)
            : base(prop, label, GetKnownLayers)
        {
        }

        /// <summary>
        /// Gets the known tech tree layers.
        /// </summary>
        /// <returns>The default layer and every tech tree layer a campaign pack in the project selects.</returns>
        public static IReadOnlyList<string> GetKnownLayers() =>
            CampaignPackLayerCatalog.GetKnownLayers(campaignPack => campaignPack.TechTreeLayers);
    }
}
