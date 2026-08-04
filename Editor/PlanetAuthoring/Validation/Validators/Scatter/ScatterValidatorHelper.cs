using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Shared body to scatter-system lookup for the scatter validators.
    /// </summary>
    /// <remarks>
    /// A body without a scatter system should not produce a pile of findings about scatter it does
    /// not have, so every scatter validator resolves the system through here first and yields
    /// nothing when there is none.
    /// </remarks>
    internal static class ScatterValidatorHelper
    {
        /// <summary>
        /// Returns the scatter system on <paramref name="body" />, or <c>null</c> when it has none.
        /// </summary>
        /// <param name="body">The body being validated. May be null.</param>
        /// <returns>The scatter system, or <c>null</c>.</returns>
        public static VegetationSystemPro FindSystem(CoreCelestialBodyData body)
        {
            if (body == null)
                return null;

            PQS pqs = BodyResolver.FindPqs(body);
            return pqs == null ? null : ScatterSystemLocator.Find(pqs);
        }

        /// <summary>
        /// Enumerates every item on a body, paired with the package that holds it.
        /// </summary>
        /// <remarks>
        /// The item-level validators all need the package name for their message, since an item name
        /// alone does not locate anything on a body with several packages.
        /// </remarks>
        /// <param name="body">The body being validated. May be null.</param>
        /// <returns>Each package and item pair, skipping empty slots.</returns>
        public static IEnumerable<(VegetationPackagePro Package, VegetationItemInfoPro Item)> Items(
            CoreCelestialBodyData body)
        {
            VegetationSystemPro system = FindSystem(body);
            if (system == null)
                yield break;

            foreach (VegetationPackagePro package in system.VegetationPackageProList)
            {
                if (package == null || package.VegetationInfoList == null)
                    continue;

                foreach (VegetationItemInfoPro item in package.VegetationInfoList)
                {
                    if (item != null)
                    {
                        yield return (package, item);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the name to show for a package in a message.
        /// </summary>
        /// <remarks>
        /// <c>PackageName</c> is authored and can be blank, which would put empty quotes in a report
        /// and leave the reader with nothing to search for. The asset name is always there.
        /// </remarks>
        /// <param name="package">The package to name. May be null.</param>
        /// <returns>The package's display name, its asset name, or a placeholder.</returns>
        public static string PackageLabel(VegetationPackagePro package)
        {
            if (package == null)
                return "(missing package)";

            return string.IsNullOrWhiteSpace(package.PackageName) ? package.name : package.PackageName;
        }

    }
}
