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
    /// Every scatter validator has to answer "does this body even have a scatter system" before it
    /// can say anything useful, and must stay silent when the answer is no. Most bodies carry no
    /// scatter at all, so a validator that treated absence as a problem would bury the report in
    /// noise for every body in the system.
    /// </remarks>
    internal static class ScatterValidatorHelper
    {
        /// <summary>
        /// Returns the scatter system on <paramref name="body" />, or null when it has none.
        /// </summary>
        /// <param name="body">The body being validated. May be null.</param>
        /// <returns>The scatter system, or null.</returns>
        public static VegetationSystemPro FindSystem(CoreCelestialBodyData body)
        {
            if (body == null)
            {
                return null;
            }

            PQS pqs = BodyResolver.FindPqs(body);
            return pqs == null ? null : ScatterSystemLocator.Find(pqs);
        }
    }
}
