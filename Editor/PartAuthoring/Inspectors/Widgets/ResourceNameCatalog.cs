using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Enumerates known resource definition names by loading addressable TextAssets labeled
    /// <c>"resources"</c> and parsing each one's JSON for its resource name.
    /// </summary>
    /// <remarks>
    /// The runtime path matches the convention that <c>PopulateResourceDefinitionDatabaseFlowAction</c> uses at game load time. Recipe entries expose the resource name under <c>recipeData.name</c>. Non-recipe entries use <c>data.name</c>.
    /// </remarks>
    public static class ResourceNameCatalog
    {
        private const string RESOURCES_LABEL = "resources";

        private static List<string> _cached;

        /// <summary>
        /// Returns the alphabetically-sorted list of known resource names.
        /// </summary>
        /// <returns>The cached, alphabetically-sorted resource-name list.</returns>
        public static IReadOnlyList<string> GetKnownResources()
        {
            return _cached ??= AddressablesJsonCatalog.Build(RESOURCES_LABEL, nameof(ResourceNameCatalog), TryExtractName);
        }

        private static string TryExtractName(string json)
        {
            try
            {
                var parsed = JObject.Parse(json);
                var isRecipe = parsed["isRecipe"]?.Value<bool>() ?? false;
                var dataKey = isRecipe ? "recipeData" : "data";
                return parsed[dataKey]?["name"]?.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
