using System.Collections.Generic;
using AwesomeTechnologies.Vegetation.PersistentStorage;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Warns when hand-placed instances refer to an item the body no longer has.
    /// </summary>
    /// <remarks>
    /// Persistent storage keys instances by <c>VegetationItemID</c>, and nothing removes them when
    /// the item they name is deleted or when a package is unassigned. The instances stay on disk,
    /// counted and serialized, and never appear.
    ///
    /// Reports only entries that hold instances, because storage keeps an empty record per item per
    /// cell as bookkeeping.
    /// </remarks>
    public sealed class ScatterStorageOrphanValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "SCATTER_ORPHANED_INSTANCE";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            VegetationSystemPro system = ScatterValidatorHelper.FindSystem(body);
            if (system == null)
                yield break;

            // Explicit comparison rather than the null-conditional operator, which bypasses Unity's
            // overloaded equality and so treats a destroyed component as live.
            PersistentVegetationStorage component = system.GetComponent<PersistentVegetationStorage>();
            if (component == null)
                yield break;

            PersistentVegetationStoragePackage storage = component.PersistentVegetationStoragePackage;
            if (storage == null || storage.PersistentVegetationCellList == null)
                yield break;

            var known = new HashSet<string>();
            foreach ((VegetationPackagePro _, VegetationItemInfoPro item) in ScatterValidatorHelper.Items(body))
            {
                if (!string.IsNullOrEmpty(item.VegetationItemID))
                {
                    known.Add(item.VegetationItemID);
                }
            }

            var orphaned = new Dictionary<string, int>();
            foreach (PersistentVegetationCell cell in storage.PersistentVegetationCellList)
            {
                if (cell?.PersistentVegetationInfoList == null)
                    continue;

                foreach (PersistentVegetationInfo info in cell.PersistentVegetationInfoList)
                {
                    int count = info?.VegetationItemList?.Count ?? 0;
                    if (count == 0 || string.IsNullOrEmpty(info.VegetationItemID) ||
                        known.Contains(info.VegetationItemID))
                        continue;

                    orphaned.TryGetValue(info.VegetationItemID, out int running);
                    orphaned[info.VegetationItemID] = running + count;
                }
            }

            foreach (KeyValuePair<string, int> entry in orphaned)
            {
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"{entry.Value} hand-placed instance(s) refer to item '{entry.Key}', which is not in any "
                    + "package on this body. They are stored but will never appear.");
            }
        }
    }
}
