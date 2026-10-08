using System;
using Ksp2UnityTools.Editor.Widgets;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Widgets
{
    /// <summary>
    /// <see cref="AutocompleteField" /> specialised for <c>MissionData.Layer</c>.
    /// Suggestions come from <see cref="MissionLayerCatalog" />.
    /// </summary>
    public class MissionLayerField : AutocompleteField
    {
        /// <summary>
        /// Creates a new <see cref="MissionLayerField" /> backed by a plain in-memory string.
        /// </summary>
        /// <param name="label">The author-facing label shown to the left of the field.</param>
        /// <param name="initialValue">Starting value of the field.</param>
        /// <param name="onValueChanged">Raised whenever the field's value changes via typing or picking.</param>
        public MissionLayerField(string label, string initialValue, Action<string> onValueChanged)
            : base(initialValue, label, MissionLayerCatalog.GetKnownLayers, onValueChanged)
        {
        }
    }
}
