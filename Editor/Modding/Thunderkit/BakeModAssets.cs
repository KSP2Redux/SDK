using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThunderKit.Core.Attributes;
using ThunderKit.Core.Pipelines;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.Modding.Thunderkit
{
    /// <summary>
    /// Bakes every <see cref="IBakeableAsset" /> in a mod's folders that should bake, and fails the build if any asset
    /// cannot bake.
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
        /// Bakes every <see cref="IBakeableAsset" /> under the folders that should bake.
        /// </summary>
        /// <param name="folders">The asset folders to search.</param>
        /// <returns>One message per problem, each naming its asset, empty when every asset baked.</returns>
        internal static List<string> BakeAll(string[] folders)
        {
            var problems = new List<string>();

            // FindAssets searches the whole project when given no folders
            if (folders.Length == 0)
                return problems;

            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", folders).Distinct())
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) is IBakeableAsset { ShouldBake: true } asset)
                {
                    problems.AddRange(asset.Bake().Select(problem => $"{path}: {problem}"));
                }
            }

            return problems;
        }
    }
}
