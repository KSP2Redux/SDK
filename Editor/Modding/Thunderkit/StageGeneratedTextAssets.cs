using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ThunderKit.Core.Attributes;
using ThunderKit.Core.Paths;
using ThunderKit.Core.Pipelines;
using UnityEditor;

namespace Ksp2UnityTools.Editor.Modding.Thunderkit
{
    [PipelineSupport(typeof(Pipeline))]
    [ManifestProcessor]
    [RequiresManifestDatumType(typeof(TextAssets))]
    public class StageGeneratedTextAssets : PipelineJob
    {
        public override Task Execute(Pipeline pipeline)
        {
            TextAssets[] textAssetsDatums = pipeline.Manifest.Data.OfType<TextAssets>().ToArray();
            foreach (TextAssets datum in textAssetsDatums)
            {
                TextAssetGenerator[] assets = AssetDatabase.FindAssets("t:TextAssetGenerator", datum.possibleFolders)
                    .Select(x =>
                        AssetDatabase.LoadAssetAtPath<TextAssetGenerator>(AssetDatabase.GUIDToAssetPath(x))
                    )
                    .Where(x => x.ShouldGenerate)
                    .ToArray();

                foreach (string outputPath in datum.StagingPaths.Select(x => x.Resolve(pipeline, this)))
                {
                    foreach (TextAssetGenerator asset in assets)
                    {
                        string trueOutputPath = Path.Combine(outputPath, asset.PathInMod);
                        if (!Directory.Exists(Path.GetDirectoryName(trueOutputPath)))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(trueOutputPath)!);
                        }

                        if (File.Exists(trueOutputPath))
                        {
                            File.Delete(trueOutputPath);
                        }

                        File.WriteAllText(trueOutputPath, asset.Generate());
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}