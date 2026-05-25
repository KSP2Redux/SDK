using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Enumerates known science experiment IDs by loading addressable TextAssets labeled
    /// <c>"scienceExperiment"</c> and parsing each one's JSON for <c>data.ExperimentID</c>.
    /// </summary>
    public static class ExperimentNameCatalog
    {
        private const string SCIENCE_EXPERIMENT_LABEL = "scienceExperiment";

        private static List<string> _cached;

        /// <summary>
        /// Returns the alphabetically-sorted list of known experiment IDs.
        /// </summary>
        /// <returns>The cached, alphabetically-sorted experiment-ID list.</returns>
        public static IReadOnlyList<string> GetKnownExperiments()
        {
            return _cached ??= AddressablesJsonCatalog.Build(SCIENCE_EXPERIMENT_LABEL, nameof(ExperimentNameCatalog), TryExtractId);
        }

        private static string TryExtractId(string json)
        {
            try
            {
                return JObject.Parse(json)["data"]?["ExperimentID"]?.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
