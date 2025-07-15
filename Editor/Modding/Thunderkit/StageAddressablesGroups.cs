using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ksp2community.ksp2unitytools.editor.API;
using ThunderKit.Core.Attributes;
using ThunderKit.Core.Paths;
using ThunderKit.Core.Pipelines;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace ksp2community.ksp2unitytools.editor.Editor.Modding.Thunderkit
{
    [PipelineSupport(typeof(Pipeline)), ManifestProcessor, RequiresManifestDatumType(typeof(AddressablesGroupDatum))]
    public class StageAddressablesGroups : PipelineJob
    {
        public override async Task Execute(Pipeline pipeline)
        {
            var addressablesDatums = pipeline.Manifest.Data.OfType<AddressablesGroupDatum>().ToArray();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            foreach (var datum in addressablesDatums)
            {
                AddressableAssetSettingsDefaultObject.Settings.activeProfileId = datum.mod.addressablesProfileId;
                var outputFolder = datum.targetFolder.Resolve(pipeline, this);
                var allGroups = datum.mod.AllGroups;
                foreach (var group in settings.groups)
                {
                    if (allGroups.Contains(group))
                    {
                        if (group.Schemas.OfType<BundledAssetGroupSchema>().FirstOrDefault() is {} schema)
                        {
                            schema.IncludeInBuild = true;
                        }
                    }
                    else
                    {
                        if (group.Schemas.OfType<BundledAssetGroupSchema>().FirstOrDefault() is {} schema)
                        {
                            schema.IncludeInBuild = false;
                        }
                    }
                }

                AddressableAssetSettings.BuildPlayerContent(out var result);
                if (!string.IsNullOrEmpty(result.Error))
                {
                    pipeline.Log(LogLevel.Error, result.Error);
                    continue;
                }

                if (Directory.Exists(outputFolder))
                {
                    Directory.Delete(outputFolder,true);
                }

                KSP2UnityTools.CopyDirectory("Library/com.unity.addressables/aa/Windows", outputFolder, true);
            }
        }
    }
}