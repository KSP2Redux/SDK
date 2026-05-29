using System;
using Ksp2UnityTools.Editor.Widgets;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Widgets
{
    /// <summary>
    /// <see cref="AutocompleteField" /> specialised for <c>MissionData.MissionGranterKey</c>.
    /// Suggestions come from <see cref="MissionGranterKeyCatalog" />.
    /// </summary>
    public sealed class MissionGranterKeyField : AutocompleteField
    {
        /// <summary>
        /// Creates a new <see cref="MissionGranterKeyField" /> backed by a plain in-memory string.
        /// </summary>
        /// <param name="label">The author-facing label shown to the left of the field.</param>
        /// <param name="initialValue">Starting value of the field.</param>
        /// <param name="onValueChanged">Raised whenever the field's value changes via typing or picking.</param>
        public MissionGranterKeyField(string label, string initialValue, Action<string> onValueChanged)
            : base(initialValue, label, MissionGranterKeyCatalog.GetKnownGranterKeys, onValueChanged)
        {
        }
    }
}
