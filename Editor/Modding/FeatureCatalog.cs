using System.Collections.Generic;
using System.Linq;
using SpaceWarp2.API.Features;
using UnityEditor;

namespace Ksp2UnityTools.Editor.Modding
{
    /// <summary>
    /// The feature ids an author can pick from: the owning mod's own, every other mod asset's in the project, and
    /// Redux's categories.
    /// </summary>
    public static class FeatureCatalog
    {
        /// <summary>
        /// The id Redux's own features are namespaced under. Redux has no mod asset.
        /// </summary>
        public const string REDUX_MOD_ID = "Ksp2Redux";

        /// <summary>
        /// The features Redux declares, bare.
        /// </summary>
        public static readonly IReadOnlyList<string> ReduxFeatures = new[]
        {
            "All", "Galaxy", "Gameplay", "Missions", "Nodes", "Parts", "Resources", "Science", "Visuals"
        };

        /// <summary>
        /// Lists the features an asset or feature of a mod can name: the mod's own bare, the rest qualified.
        /// </summary>
        /// <param name="owner">The mod the naming asset belongs to, or null for Redux's own content.</param>
        /// <returns>The suggestions, the owner's first.</returns>
        public static List<string> SuggestionsFor(Mod owner)
        {
            var suggestions = new List<string>();
            if (owner != null)
            {
                suggestions.Add(owner.id);
                suggestions.AddRange(FeatureIdsOf(owner));
            }

            foreach (Mod mod in ProjectMods().Where(mod => mod != owner))
            {
                suggestions.Add(FeatureRegistry.Qualify(mod.id, mod.id));
                suggestions.AddRange(FeatureIdsOf(mod).Select(featureId => FeatureRegistry.Qualify(mod.id, featureId)));
            }

            suggestions.AddRange(ReduxFeatures.Select(featureId => FeatureRegistry.Qualify(REDUX_MOD_ID, featureId)));
            return suggestions.Distinct().ToList();
        }

        /// <summary>
        /// Names the mod a suggestion belongs to, for the detail beside it.
        /// </summary>
        /// <param name="suggestion">A suggestion from <see cref="SuggestionsFor" />.</param>
        /// <returns>The owning mod's id, or "this mod" for a bare id.</returns>
        public static string DetailFor(string suggestion) =>
            suggestion.IndexOf(FeatureRegistry.SEPARATOR) < 0 ? "this mod" : FeatureRegistry.NamespaceOf(suggestion);

        private static IEnumerable<string> FeatureIdsOf(Mod mod) =>
            mod.features
                .Where(feature => !string.IsNullOrWhiteSpace(feature.id))
                .Select(feature => feature.id.Trim());

        /// <summary>
        /// Lists the mod assets in the project.
        /// </summary>
        /// <returns>The mods.</returns>
        public static IEnumerable<Mod> ProjectMods() =>
            AssetDatabase.FindAssets("t:" + nameof(Mod))
                .Select(guid => AssetDatabase.LoadAssetAtPath<Mod>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(mod => mod != null);
    }
}
