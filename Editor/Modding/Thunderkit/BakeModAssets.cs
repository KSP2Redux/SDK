using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ksp2UnityTools.Editor.Reflection;
using ThunderKit.Core.Attributes;
using ThunderKit.Core.Pipelines;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.Modding.Thunderkit
{
    /// <summary>
    /// Bakes every bakeable asset in a mod's folders through its <see cref="IModAssetBaker" />, and fails the build if
    /// any asset cannot bake.
    /// </summary>
    /// <remarks>
    /// Runs before every staging job, since a bake writes files that <see cref="StageGeneratedTextAssets" /> and
    /// <see cref="StageAddressablesGroups" /> pick up. It searches the folders of the manifest's
    /// <see cref="TextAssets" /> datum, which every mod manifest has.
    /// </remarks>
    [PipelineSupport(typeof(Pipeline))]
    [ManifestProcessor]
    [RequiresManifestDatumType(typeof(TextAssets))]
    public class BakeModAssets : PipelineJob
    {
        /// <inheritdoc />
        public override Task Execute(Pipeline pipeline)
        {
            string[] folders = pipeline.Manifest.Data.OfType<TextAssets>()
                .Where(datum => datum.possibleFolders != null)
                .SelectMany(datum => datum.possibleFolders)
                .Distinct()
                .ToArray();
            List<string> problems = BakeAll(folders);
            if (problems.Count == 0)
                return Task.CompletedTask;

            string message = $"{problems.Count} problem(s) stopped the mod's assets from baking:\n{string.Join("\n", problems)}";
            Debug.LogError($"[BakeModAssets] {message}");
            throw new InvalidOperationException(message);
        }

        /// <summary>
        /// Bakes every asset that an <see cref="IModAssetBaker" /> handles under the folders.
        /// </summary>
        /// <param name="folders">The asset folders to search.</param>
        /// <returns>One message per problem, each naming its asset, empty when every asset baked.</returns>
        internal static List<string> BakeAll(string[] folders)
        {
            var problems = new List<string>();

            // FindAssets searches the whole project when given no folders
            if (folders.Length == 0)
                return problems;

            foreach (Type bakerType in ReduxTypeCache.GetTypesDerivedFrom<IModAssetBaker>())
            {
                // A baker that cannot be made would leave its assets unbaked, so it fails the build rather than skipping
                if (bakerType.IsAbstract || bakerType.GetConstructor(Type.EmptyTypes) == null)
                {
                    problems.Add(
                        $"{bakerType.FullName} implements {nameof(IModAssetBaker)} but cannot be created: it is abstract or has no public parameterless constructor."
                    );
                    continue;
                }

                var baker = (IModAssetBaker)Activator.CreateInstance(bakerType);
                foreach (string guid in AssetDatabase.FindAssets($"t:{baker.AssetType.Name}", folders).Distinct())
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Object asset = AssetDatabase.LoadAssetAtPath(path, baker.AssetType);
                    if (asset == null)
                        continue;

                    problems.AddRange(baker.Bake(asset).Select(problem => $"{path}: {problem}"));
                }
            }

            return problems;
        }
    }
}
