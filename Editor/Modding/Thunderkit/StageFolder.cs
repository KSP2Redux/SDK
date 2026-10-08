using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ksp2UnityTools.Editor.API;
using ThunderKit.Core.Attributes;
using ThunderKit.Core.Pipelines;

namespace Ksp2UnityTools.Editor.Modding.Thunderkit
{
    [PipelineSupport(typeof(Pipeline))]
    [ManifestProcessor]
    [RequiresManifestDatumType(typeof(CopyFolderDatum))]
    public class StageFolder : PipelineJob
    {
        public override Task Execute(Pipeline pipeline)
        {
            IEnumerable<CopyFolderDatum> datums = pipeline.manifest.Data.OfType<CopyFolderDatum>();
            foreach (CopyFolderDatum datum in datums)
            {
                if (Directory.Exists(datum.destinationPath))
                {
                    Directory.Delete(datum.destinationPath, true);
                }

                Directory.CreateDirectory(datum.destinationPath);
                KSP2UnityTools.CopyDirectory(datum.sourcePath, datum.destinationPath, true);
            }

            return Task.CompletedTask;
        }
    }
}