using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using ThunderKit.Core;
using ThunderKit.Core.Data;
using ThunderKit.Core.Manifests;
using ThunderKit.Core.Manifests.Datum;
using ThunderKit.Core.Manifests.Datums;
using ThunderKit.Core.Paths.Components;
using ThunderKit.Core.Pipelines;
using ThunderKit.Core.Pipelines.Jobs;
using ThunderKit.Core.Pipelines.Jops;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ksp2community.ksp2unitytools.editor.Modding
{
    public class Mod : TextAssetGenerator
    {
        // The basic information needed for a mod
        public override string PathInMod => "swinfo.json";
        public string id = "sampleMod";
        public string name = "Sample Mod";
        public string author = "nobody";
        public string description = "A sample mod for KSP2";
        public string version = "0.1.0";
        public string versionCheck = "";
        public string minKsp2Version = "*";
        public string maxKsp2Version = "*";
        public string source = "";
        public string Folder => Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));

        [SerializeField]
        public List<ModDependency> dependencies = new()
        {
            new()
            {
                Id = "com.github.x606.spacewarp",
                Min = "1.5.1",
                Max = "*"
            },
        };

        [SerializeField]
        public List<ModDependency> incompatibilities = new()
        {

        };
        
        
        public override string Generate()
        {
            var deps = new JArray();
            foreach (var dep in dependencies)
            {
                var versionObj = new JObject
                {
                    ["min"] = dep.Min,
                    ["max"] = dep.Max
                };
                var depObj = new JObject
                {
                    ["id"] = dep.Id,
                    ["version"] = versionObj
                };
                deps.Add(depObj);
            }

            var conflicts = new JArray();
            foreach (var conflict in incompatibilities)
            {
                var versionObj = new JObject
                {
                    ["min"] = conflict.Min,
                    ["max"] = conflict.Max
                };
                var conflictObj = new JObject
                {
                    ["id"] = conflict.Id,
                    ["version"] = versionObj
                };
                conflicts.Add(conflictObj);
            }

            var ksp2Version = new JObject
            {
                ["min"] = minKsp2Version,
                ["max"] = maxKsp2Version
            };
            var jObject = new JObject
            {
                ["spec"] = "2.0",
                ["mod_id"] = id,
                ["name"] = name,
                ["author"] = author,
                ["description"] = description,
                ["source"] = source,
                ["version"] = version,
                ["version_check"] = versionCheck,
                ["ksp2_version"] = ksp2Version,
                ["dependencies"] = deps,
                ["conflicts"] = conflicts
            };
            return jObject.ToString();
        }

        public (Manifest manifest, Pipeline pipeline) GeneratePipeline(string targetLocation, [CanBeNull] string manifestId = null, bool includeAssembly = false, bool buildZip = false)
        {
            var manifest = CreateInstance<Manifest>();
            manifest.Identity = CreateInstance<ManifestIdentity>();
            manifest.Identity.name = manifestId == null ? $"{id}" : $"{id} - {manifestId}";
            manifest.Identity.Author = author;
            manifest.Identity.Description = description;
            manifest.Identity.Version = version;
            var files = CreateInstance<Files>();
            files.StagingPaths = new[] { targetLocation };
            files.includeMetaFiles = false;
            files.files = new [] {AssetDatabase.LoadAssetAtPath<Object>(Folder + "/Copied")};
            var textAssets = CreateInstance<TextAssets>();
            textAssets.StagingPaths = new []{targetLocation};
            textAssets.PossibleFolders = new[] { Folder };
            if (!includeAssembly || !File.Exists(Folder + $"/Code/{id}.asmdef"))
            {
                manifest.Data = new ComposableElement[] { files, textAssets };
            }
            else
            {
                var assembly = CreateInstance<AssemblyDefinitions>();
                assembly.StagingPaths = new[] { targetLocation };
                assembly.definitions = new[]
                    { AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(Folder + $"/Code/{id}.asmdef") };
                manifest.Data = new ComposableElement[] { files, textAssets, assembly };
            }
            
            var pipeline = CreateInstance<Pipeline>();
            var pipelineJobs = new List<PipelineJob>
            {
                CreateInstance<StageManifestFiles>(),
                CreateInstance<StageGeneratedTextAssets>()
            };
            if (includeAssembly && File.Exists(Folder + $"/Code/{id}.asmdef"))
            {
                pipelineJobs.Add(CreateInstance<StageAssemblies>());
            }

            if (buildZip)
            {
                pipelineJobs.Add(CreateInstance<Zip>());
            }
            pipeline.manifest = manifest;
            pipeline.Data = pipelineJobs.ToArray();
            return (manifest, pipeline);
        }

        public void RefreshPipelines() {
            var pipelinesFolder = Folder + "/Pipelines";
            var buildForEditor = pipelinesFolder + "/Build for Editor.asset";
            var buildForEditorManifest = pipelinesFolder + "/Build for Editor Manifest.asset";
            var buildForPlayer = pipelinesFolder + "/Build for Player.asset";
            var buildForPlayerManifest = pipelinesFolder + "/Build for Player Manifest.asset";
            var deployToZipFile = pipelinesFolder + "/Deploy to Zip File.asset";
            var deployToZipFileManifest = pipelinesFolder + "/Deploy to Zip File Manifest.asset";
            if (!Directory.Exists(pipelinesFolder))
            {
                Directory.CreateDirectory(pipelinesFolder);
            }

            if (File.Exists(buildForEditor)) File.Delete(buildForEditor);
            if (File.Exists(buildForEditorManifest)) File.Delete(buildForEditorManifest);
            var (editorManifest, editorPipeline) = GeneratePipeline($"Assets/Mods/__Testing/{id}", "Editor");
            AssetDatabase.CreateAsset(editorManifest, buildForEditorManifest);
            AssetDatabase.CreateAsset(editorPipeline, buildForEditor);
            if (File.Exists(buildForPlayer)) File.Delete(buildForPlayer);
            if (File.Exists(buildForPlayerManifest)) File.Delete(buildForPlayerManifest);
            var (playerManifest,playerPipeline) = GeneratePipeline($"{ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>().GamePath}/mods/__Testing/{id}", "Editor",true);
            AssetDatabase.CreateAsset(playerManifest, buildForPlayerManifest);
            AssetDatabase.CreateAsset(playerPipeline, buildForPlayer);
            if (File.Exists(deployToZipFile)) File.Delete(deployToZipFile);
            if (File.Exists(deployToZipFileManifest)) File.Delete(deployToZipFileManifest);
            var (deployManifest,deployPipeline) = GeneratePipeline($"Deploy/{id}", "Editor",true,true);
            AssetDatabase.CreateAsset(playerManifest, buildForPlayerManifest);
            AssetDatabase.CreateAsset(playerPipeline, buildForPlayer);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}