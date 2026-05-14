using System;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation
{
    /// <summary>
    /// Bitmask describing the body classes a validator applies to.
    /// </summary>
    /// <remarks>
    /// Computed for each body once per validation run by <see cref="BodyClassClassifier" />. Validators
    /// that declare a non-<see cref="All" /> mask are skipped when the body's class is not in the mask.
    /// </remarks>
    [Flags]
    public enum BodyClassFlags
    {
        /// <summary>No class, used as the classification of a null body.</summary>
        None = 0,
        /// <summary>Body has a PQS-rendered solid surface.</summary>
        SolidSurface = 1 << 0,
        /// <summary>Body is the primary light source of its system.</summary>
        Star = 1 << 1,
        /// <summary>Body is a gas giant with no solid surface and is not a star.</summary>
        GasGiant = 1 << 2,
        /// <summary>All body classes. Default for validators that do not declare a narrower scope.</summary>
        All = SolidSurface | Star | GasGiant,
    }
}
