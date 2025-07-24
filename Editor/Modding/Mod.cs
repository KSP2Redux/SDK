using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using ksp2community.ksp2unitytools.editor.Editor.Modding.Thunderkit;
using Newtonsoft.Json.Linq;
using ThunderKit.Core;
using ThunderKit.Core.Data;
using ThunderKit.Core.Manifests;
using ThunderKit.Core.Manifests.Datum;
using ThunderKit.Core.Manifests.Datums;
using ThunderKit.Core.Pipelines;
using ThunderKit.Core.Pipelines.Jobs;
using ThunderKit.Core.Pipelines.Jops;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ksp2community.ksp2unitytools.editor.Editor.Modding
{
    public class Mod : TextAssetGenerator
    {
        private static string[] _precompiledReferences;

        static Mod()
        {
            _precompiledReferences = Directory.GetFiles("Packages/KSP2_x64", "*.dll", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).ToArray();
        }
        
        // The basic information needed for a mod
        public override string PathInMod => "swinfo.json";
        public override bool ShouldGenerate => true;
        [Tooltip("The Mod ID, if this changes, you have to refresh the pipelines and addressables bundles, and delete the old assembly definition, recreating it.")]
        public string id = "sampleMod";
        [Tooltip("The name of the mod that gets shown in the settings menu")]
        public string name = "Sample Mod";
        [Tooltip("The author of the mod")]
        public string author = "nobody";
        [Multiline, Tooltip("The description of the mod")]
        public string description = "A sample mod for KSP2";
        [Tooltip("The mod version, should follow semantic versioning")]
        public string version = "0.1.0";
        [Tooltip("A location of a remote copy of swinfo.json for automatic version checking to work")]
        public string versionCheck = "";
        [Tooltip("The minimum KSP2 version this supports"), InspectorName("Minimum KSP2 Version")]
        public string minKsp2Version = "*";
        [Tooltip("The maximum KSP2 version this supports"), InspectorName("Maximum KSP2 Version")]
        public string maxKsp2Version = "*";
        [Tooltip("A repository that contains the source code for this mod")]
        public string source = "";
        public string Folder => Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));
        public string AssemblyPath => Folder + $"/Code/{id}.asmdef";
        public string MainPluginPath => Folder + $"/Code/{id}Plugin.cs";

        [HideInInspector] public AddressableAssetGroup allGroup;
        [HideInInspector] public AddressableAssetGroup partsGroup;
        [HideInInspector] public AddressableAssetGroup missionsGroup;
        [HideInInspector] public AddressableAssetGroup celestialBodiesGroup;
        [HideInInspector] public AddressableAssetGroup scienceExperimentGroup;
        [HideInInspector] public AddressableAssetGroup techNodeDataGroup;

        public AddressableAssetGroup[] AllGroups => new[]
            { allGroup, partsGroup, missionsGroup, celestialBodiesGroup, scienceExperimentGroup, techNodeDataGroup };
        [HideInInspector] public string addressablesProfileId;
        
        [SerializeField]
        [Tooltip("The mods that this mod depends on")]
        public List<ModDependency> dependencies = new()
        {
            new()
            {
                id = "SpaceWarp2",
                min = "2.0.0",
                max = "*"
            },
        };

        [SerializeField]
        [Tooltip("The mods that this mod cannot run with")]
        [InspectorName("Conflicts")]
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
                    ["min"] = dep.min,
                    ["max"] = dep.max
                };
                var depObj = new JObject
                {
                    ["id"] = dep.id,
                    ["version"] = versionObj
                };
                deps.Add(depObj);
            }

            var conflicts = new JArray();
            foreach (var conflict in incompatibilities)
            {
                var versionObj = new JObject
                {
                    ["min"] = conflict.min,
                    ["max"] = conflict.max
                };
                var conflictObj = new JObject
                {
                    ["id"] = conflict.id,
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
            if (File.Exists(AssemblyPath))
            {
                jObject["main-assembly"] = $"{id}.dll";
            }
            return jObject.ToString();
        }

        public void GeneratePipeline(string targetLocation, string manifestPath, string pipelinePath, [CanBeNull] string manifestId = null, bool includeBuiltObjects = false, bool buildZip = false)
        {
            var manifest = CreateInstance<Manifest>();
            manifest.Identity = CreateInstance<ManifestIdentity>();
            manifest.Identity.name = manifestId == null ? $"{id}" : $"{id} - {manifestId}";
            manifest.Identity.Author = author;
            manifest.Identity.Description = description;
            manifest.Identity.Version = version;
            manifest.Data = new ComposableElement[] { };
            AssetDatabase.CreateAsset(manifest, manifestPath);
            AssetDatabase.AddObjectToAsset(manifest.Identity, manifest);
            var files = CreateInstance<CopyFolderDatum>();
            files.sourcePath = Folder + "/Copied";
            files.destinationPath = targetLocation;
            manifest.InsertElement(files, manifest.Data.Length);
            var textAssets = CreateInstance<TextAssets>();
            textAssets.StagingPaths = new []{targetLocation};
            textAssets.possibleFolders = new[] { Folder };
            manifest.InsertElement(textAssets, manifest.Data.Length);
            if (includeBuiltObjects)
            {
                var addressablesGroups = CreateInstance<AddressablesGroupDatum>();
                addressablesGroups.mod = this;
                addressablesGroups.StagingPaths = new[] { targetLocation + "/addressables" };
                addressablesGroups.targetFolder = targetLocation + "/addressables";
                manifest.InsertElement(addressablesGroups, manifest.Data.Length);
            }
            if (includeBuiltObjects && File.Exists(Folder + $"/Code/{id}.asmdef"))
            {
                var assembly = CreateInstance<AssemblyDefinitions>();
                assembly.StagingPaths = new[] { targetLocation };
                assembly.definitions = new[]
                    { AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(Folder + $"/Code/{id}.asmdef") };
                manifest.InsertElement(assembly, manifest.Data.Length);
                if (Directory.Exists(Folder + "/Code/Lib"))
                {
                    var assembly2 = CreateInstance<AssemblyDefinitions>();
                    assembly2.StagingPaths = new[] { targetLocation + "/lib" };
                    var definitions = new List<AssemblyDefinitionAsset>();
                    foreach (var file in Directory.GetFiles(Folder + "/Code/Lib", "*.asmdef", SearchOption.AllDirectories))
                    {
                        var relative = Path.GetRelativePath(Folder + "/Code/Lib", file);
                        definitions.Add(AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(Folder + "/Lib/" + relative));
                    }
                    assembly2.definitions = definitions.ToArray();
                    manifest.InsertElement(assembly2, manifest.Data.Length);
                }
            }
            
            var pipeline = CreateInstance<Pipeline>();
            pipeline.Data = new ComposableElement[] { };
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
            pipeline.InsertElement(CreateInstance<StageFolder>(), pipeline.Data.Length);
            pipeline.InsertElement(CreateInstance<StageGeneratedTextAssets>(), pipeline.Data.Length);
            if (includeBuiltObjects)
            {
                pipeline.InsertElement(CreateInstance<StageAddressablesGroups>(), pipeline.Data.Length);
            }
            if (includeBuiltObjects && File.Exists(Folder + $"/Code/{id}.asmdef"))
            {
                pipeline.InsertElement(CreateInstance<StageAssemblies>(), pipeline.Data.Length);
            }

            if (buildZip)
            {
                var zip = CreateInstance<Zip>();
                zip.Source = targetLocation;
                zip.Output = $"{targetLocation}.zip";
                pipeline.InsertElement(zip, pipeline.Data.Length);
            }
            pipeline.manifest = manifest;
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
                AssetDatabase.CreateFolder(Folder, "Pipelines");
            }

            if (File.Exists(buildForEditor)) AssetDatabase.DeleteAsset(buildForEditor);
            if (File.Exists(buildForEditorManifest)) AssetDatabase.DeleteAsset(buildForEditorManifest);
            GeneratePipeline($"Assets/Mods/__Testing/{id}", buildForEditorManifest, buildForEditor, "Editor");
            if (File.Exists(buildForPlayer)) AssetDatabase.DeleteAsset(buildForPlayer);
            if (File.Exists(buildForPlayerManifest)) AssetDatabase.DeleteAsset(buildForPlayerManifest);
            GeneratePipeline($"{ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>().GamePath}/mods/__Testing/{id}", buildForPlayerManifest, buildForPlayer, "Editor",true);
            if (File.Exists(deployToZipFile)) AssetDatabase.DeleteAsset(deployToZipFile);
            if (File.Exists(deployToZipFileManifest)) AssetDatabase.DeleteAsset(deployToZipFileManifest);
            GeneratePipeline($"Deploy/{id}", deployToZipFileManifest, deployToZipFile, "Deploy",true,true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private const string Template = @"
using SpaceWarp2.API.Mods;
namespace %MOD%
{
    /* Extend KerbalMod instead if you need the MonoBehaviour update loop/references to game stuff like SW 1.x mods */
    public class %MOD%Plugin : GeneralMod
    {
        public override void OnPreInitialized() {
            /*
                Code that runs before addressables/assets are loaded goes here
                This is where you want to register loading actions or other such things 
            */
        }

        public override void OnInitialized() {
            /*
                Code that runs after addressables/assets are loaded goes here
                You are also generally free to interact with game code here
            */
            SWLogger.LogInfo(""Hello World!"");
        }

        public override void OnPostInitialized() {
            /*
                Code that runs after all mods have been initialized goes here
            */
        }
    }
}
";

        public void CreateAssembly()
        {
            if (File.Exists(AssemblyPath))
            {
                EditorUtility.DisplayDialog("Error", "Assembly already exists.", "OK");
                return;
            }

            if (!Directory.Exists(Folder + "/Code"))
            {
                AssetDatabase.CreateFolder(Folder, "Code");
            }
            // We need to reference the GUID of at least a few assets
            var assemblyDefinitionJObject = new JObject
            {
                ["name"] = id,
                ["rootNamespace"] = id,
                ["references"] = {},
                ["includePlatforms"] = new JArray("Editor"),
                ["allowUnsafeCode"] = true,
                ["overrideReferences"] = true,
                ["precompiledReferences"] = new JArray(_precompiledReferences),
                ["autoReferenced"] = true,
                ["defineConstants"] = {},
                ["versionDefines"] = {},
                ["noEngineReferences"] = false
            };
            File.WriteAllText(AssemblyPath, assemblyDefinitionJObject.ToString());
            File.WriteAllText(MainPluginPath, Template.Replace("%MOD%", id).Trim());
            AssetDatabase.Refresh();
        }

        private static string[] _usedLabels =
        {
            "parts_data",
            "celestial_bodies",
            "scienceExperiment",
            "missions",
            "techNodeData",
        };
        public void CreateAddressablesGroups()
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                    AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);
            }
            
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            foreach (var label in _usedLabels)
            {
                if (!settings.GetLabels().Contains(label))
                {
                    settings.AddLabel(label);
                }
            }
            
            addressablesProfileId = !settings.profileSettings.GetAllProfileNames().Contains(id) ? settings.profileSettings.AddProfile(id, settings.activeProfileId) : settings.profileSettings.GetProfileId(id);
            settings.profileSettings.SetValue(addressablesProfileId,"Local.BuildPath",$"Library/com.unity.addressables/aa/Windows/StandaloneWindows64");
            settings.profileSettings.SetValue(addressablesProfileId,"Local.LoadPath",$"{{SpaceWarpPaths.{id}}}/addressables/StandaloneWindows64");
            
            allGroup = settings.groups.FirstOrDefault(x => x.name == $"addressables_{id}_all") ?? settings.CreateGroup($"addressables_{id}_all", false, false, false, settings.DefaultGroup.Schemas);
            
            partsGroup = settings.groups.FirstOrDefault(x => x.name == $"{id}_parts") ?? settings.CreateGroup($"{id}_parts", false, false, false, settings.DefaultGroup.Schemas);
            missionsGroup = settings.groups.FirstOrDefault(x => x.name == $"{id}_missions") ?? settings.CreateGroup($"{id}_missions", false, false, false, settings.DefaultGroup.Schemas);
            celestialBodiesGroup = settings.groups.FirstOrDefault(x => x.name == $"{id}_cbs") ?? settings.CreateGroup($"{id}_cbs", false, false, false, settings.DefaultGroup.Schemas);
            scienceExperimentGroup = settings.groups.FirstOrDefault(x => x.name == $"{id}_experiments") ?? settings.CreateGroup($"{id}_experiments", false, false, false, settings.DefaultGroup.Schemas);
            techNodeDataGroup = settings.groups.FirstOrDefault(x => x.name == $"{id}_tech_nodes") ?? settings.CreateGroup($"{id}_tech_nodes", false, false, false, settings.DefaultGroup.Schemas);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }

        public void CreateVersionCheckSwinfo()
        {
            var text = this.Generate();
            var location = Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));
            File.WriteAllText(Folder + "/swinfo.json", text);
        }
    }
}