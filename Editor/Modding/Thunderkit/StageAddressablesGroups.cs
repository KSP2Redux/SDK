using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ksp2UnityTools.Editor.API;
using ThunderKit.Core.Attributes;
using ThunderKit.Core.Manifests.Datums;
using ThunderKit.Core.Paths;
using ThunderKit.Core.Pipelines;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.Serialization;

namespace Ksp2UnityTools.Editor.Modding.Thunderkit
{
    /// <summary>
    /// Builds and stages the Addressables groups declared by a ThunderKit mod manifest.
    /// </summary>
    [PipelineSupport(typeof(Pipeline))]
    [ManifestProcessor]
    [RequiresManifestDatumType(typeof(AddressablesGroupDatum))]
    public class StageAddressablesGroups : PipelineJob
    {
        [Serializable]
        private sealed class AssemblyDefinitionNameDTO
        {
            [FormerlySerializedAs("name")]
            [SerializeField] private string _name;

            internal string Name => _name;
        }

        /// <inheritdoc />
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
                    HashSet<string> modAssemblyNames = GetModAssemblyNames(pipeline, datum.mod);
                    AddressablesPlayerBuildResult result;
                    using (LateBoundUxmlBuildConversion conversion =
                           LateBoundUxmlBuildConversion.Apply(allGroups, modAssemblyNames))
                    {
                        if (conversion.ConvertedReferenceCount > 0)
                        {
                            pipeline.Log(
                                LogLevel.Information,
                                $"Converted {conversion.ConvertedReferenceCount} late-bound UXML serialized data references"
                            );
                        }

                        AddressableAssetSettings.BuildPlayerContent(out result);
                    }

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

        private static HashSet<string> GetModAssemblyNames(Pipeline pipeline, Mod mod)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (mod != null && !string.IsNullOrWhiteSpace(mod.id))
            {
                result.Add(mod.id);
            }

            foreach (AssemblyDefinitions datum in pipeline.Manifest.Data.OfType<AssemblyDefinitions>())
            {
                foreach (UnityEditorInternal.AssemblyDefinitionAsset definition in datum.definitions)
                {
                    if (definition == null)
                    {
                        continue;
                    }

                    AssemblyDefinitionNameDTO definitionName =
                        JsonUtility.FromJson<AssemblyDefinitionNameDTO>(definition.text);
                    if (!string.IsNullOrWhiteSpace(definitionName?.Name))
                    {
                        result.Add(definitionName.Name);
                    }
                }
            }

            return result;
        }
    }
}
