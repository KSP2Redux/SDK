using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ksp2UnityTools.Editor.API;
using ThunderKit.Core.Attributes;
using ThunderKit.Core.Paths;
using ThunderKit.Core.Pipelines;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace Ksp2UnityTools.Editor.Modding.Thunderkit
{
    [PipelineSupport(typeof(Pipeline))]
    [ManifestProcessor]
    [RequiresManifestDatumType(typeof(AddressablesGroupDatum))]
    public class StageAddressablesGroups : PipelineJob
    {
        public override async Task Execute(Pipeline pipeline)
        {
            AddressablesGroupDatum[] addressablesDatums =
                pipeline.Manifest.Data.OfType<AddressablesGroupDatum>().ToArray();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            foreach (AddressablesGroupDatum datum in addressablesDatums)
            {
                AddressableAssetSettingsDefaultObject.Settings.activeProfileId = datum.mod.addressablesProfileId;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                string outputFolder = datum.targetFolder.Resolve(pipeline, this);
                AddressableAssetGroup[] allGroups = datum.mod.AllGroups;
                int totalAssetCount = 0;
                foreach (AddressableAssetGroup group in settings.groups)
                {
                    if (allGroups.Contains(group))
                    {
                        if (group.Schemas.OfType<BundledAssetGroupSchema>().FirstOrDefault() is { } schema)
                        {
                            totalAssetCount += group.entries.Count;
                            schema.IncludeInBuild = true;
                        }
                    }
                    else
                    {
                        if (group.Schemas.OfType<BundledAssetGroupSchema>().FirstOrDefault() is { } schema)
                        {
                            schema.IncludeInBuild = false;
                        }
                    }
                }

                if (Directory.Exists(outputFolder))
                {
                    Directory.Delete(outputFolder, true);
                }

                if (totalAssetCount > 0)
                {
                    AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        pipeline.Log(LogLevel.Error, result.Error);
                        continue;
                    }

                    KSP2UnityTools.CopyDirectory("Library/com.unity.addressables/aa/Windows", outputFolder, true);
                }
                else
                {
                    pipeline.Log(
                        LogLevel.Information,
                        "No addressables were built for this mod, the addressables folder will not be copied"
                    );
                }
            }
        }
    }
}