using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ksp2community.ksp2unitytools.editor.API;
using ThunderKit.Core.Attributes;
using ThunderKit.Core.Pipelines;

namespace ksp2community.ksp2unitytools.editor.Editor.Modding.Thunderkit
{
    
    [PipelineSupport(typeof(Pipeline)), ManifestProcessor, RequiresManifestDatumType(typeof(CopyFolderDatum))]
    public class StageFolder : PipelineJob
    {
        public override Task Execute(Pipeline pipeline)
        {
            var datums = pipeline.manifest.Data.OfType<CopyFolderDatum>();
            foreach (var datum in datums)
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