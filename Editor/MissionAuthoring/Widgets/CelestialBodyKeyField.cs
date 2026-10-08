using System;
using Ksp2UnityTools.Editor.Widgets;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Widgets
{
    /// <summary>
    /// <see cref="AutocompleteField" /> specialised for celestial body name keys.
    /// Suggestions come from <see cref="CelestialBodyKeyCatalog" />.
    /// </summary>
    public sealed class CelestialBodyKeyField : AutocompleteField
    {
        /// <summary>
        /// Creates a new <see cref="CelestialBodyKeyField" /> backed by a plain in-memory string.
        /// </summary>
        /// <param name="label">The author-facing label shown to the left of the field.</param>
        /// <param name="initialValue">Starting value of the field.</param>
        /// <param name="onValueChanged">Raised whenever the field's value changes via typing or picking.</param>
        public CelestialBodyKeyField(string label, string initialValue, Action<string> onValueChanged)
            : base(initialValue, label, CelestialBodyKeyCatalog.GetKnownBodyKeys, onValueChanged)
        {
        }
    }
}
