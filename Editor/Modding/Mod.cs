using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Ksp2UnityTools.Editor.Modding.Thunderkit;
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

namespace Ksp2UnityTools.Editor.Modding
{
    /// <summary>
    /// A mod authored in the SDK: its swinfo fields, Addressables groups, and the ThunderKit pipelines that build it.
    /// </summary>
    public class Mod : TextAssetGenerator
    {
        private const string Ksp2PackageReferenceDirectory = "Packages/KSP2_x64";
        private static string[] _precompiledReferences;

        /// <inheritdoc />
        public override string PathInMod => "swinfo.json";

        /// <inheritdoc />
        public override bool ShouldGenerate => true;

        /// <summary>
        /// The mod's id, which names its feature, labels, groups and assembly.
        /// </summary>
        [Tooltip(
            "The Mod ID, if this changes, you have to refresh the pipelines and addressables bundles, and delete the old assembly definition, recreating it."
        )]
        public string id = "sampleMod";

        /// <summary>
        /// Gets the Addressables label the mod's Lua patches load under.
        /// </summary>
        public string AddressableScriptLabel => $"{id}_patches";

        /// <summary>
        /// Gets the Addressables label the mod's prefab patches load under.
        /// </summary>
        public string AddressablePrefabPatchLabel =>
            $"{id}_prefab_patches";

        /// <summary>
        /// The mod's name, as the mods menu shows it.
        /// </summary>
        [Tooltip("The name of the mod that gets shown in the settings menu")]
        public string name = "Sample Mod";

        /// <summary>
        /// The mod's author.
        /// </summary>
        [Tooltip("The author of the mod")] public string author = "nobody";

        /// <summary>
        /// The mod's description.
        /// </summary>
        [Multiline] [Tooltip("The description of the mod")]
        public string description = "A sample mod for KSP2";

        /// <summary>
        /// The mod's version, in semantic versioning.
        /// </summary>
        [Tooltip("The mod version, should follow semantic versioning")]
        public string version = "0.1.0";

        /// <summary>
        /// The URL of a remote swinfo.json the game checks the mod's version against, or empty for none.
        /// </summary>
        [Tooltip("A location of a remote copy of swinfo.json for automatic version checking to work")]
        public string versionCheck = "";

        /// <summary>
        /// The lowest game version the mod supports, or * for any.
        /// </summary>
        [Tooltip("The minimum KSP2 version this supports")] [InspectorName("Minimum KSP2 Version")]
        public string minKsp2Version = "*";

        /// <summary>
        /// The highest game version the mod supports, or * for any.
        /// </summary>
        [Tooltip("The maximum KSP2 version this supports")] [InspectorName("Maximum KSP2 Version")]
        public string maxKsp2Version = "*";

        /// <summary>
        /// The URL of the mod's source code, or empty for none.
        /// </summary>
        [Tooltip("A repository that contains the source code for this mod")]
        public string source = "";

        /// <summary>
        /// Gets the name the mod asset takes in pickers: the mod's name and id, or the id alone when they match.
        /// </summary>
        public string PickerDisplayName
        {
            get
            {
                string displayName = string.IsNullOrWhiteSpace(name) ? id : name;
                string displayId = string.IsNullOrWhiteSpace(id) ? "no id" : id;
                if (string.Equals(displayName, displayId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return displayId;
                }

                return $"{displayName} [{displayId}]";
            }
        }

        /// <summary>
        /// Gets the folder the mod asset sits in, the root of the mod's files.
        /// </summary>
        public string Folder => Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));

        /// <summary>
        /// Gets the path of the mod's assembly definition.
        /// </summary>
        public string AssemblyPath => Folder + $"/Code/{id}.asmdef";

        /// <summary>
        /// Gets the folder holding the mod's integrations, one subfolder per integrated mod, named by its id.
        /// </summary>
        public string IntegrationsFolder => Folder + "/Code/Integrations";

        /// <summary>
        /// Gets the path of the mod's plugin class.
        /// </summary>
        public string MainPluginPath => Folder + $"/Code/{id}Plugin.cs";

        /// <summary>
        /// The group for the mod's Addressables of no particular kind.
        /// </summary>
        [HideInInspector] public AddressableAssetGroup allGroup;

        /// <summary>
        /// The group for the mod's part data.
        /// </summary>
        [HideInInspector] public AddressableAssetGroup partsGroup;

        /// <summary>
        /// The group for the mod's missions.
        /// </summary>
        [HideInInspector] public AddressableAssetGroup missionsGroup;

        /// <summary>
        /// The group for the mod's celestial bodies.
        /// </summary>
        [HideInInspector] public AddressableAssetGroup celestialBodiesGroup;

        /// <summary>
        /// The group for the mod's science experiments.
        /// </summary>
        [HideInInspector] public AddressableAssetGroup scienceExperimentGroup;

        /// <summary>
        /// The group for the mod's tech nodes and tech tree data.
        /// </summary>
        [HideInInspector] public AddressableAssetGroup techNodeDataGroup;

        /// <summary>
        /// The group for the mod's resources.
        /// </summary>
        [HideInInspector] public AddressableAssetGroup resourcesGroup;

        /// <summary>
        /// Gets every Addressables group the mod owns.
        /// </summary>
        public AddressableAssetGroup[] AllGroups => new[]
            { allGroup, partsGroup, missionsGroup, celestialBodiesGroup, scienceExperimentGroup, techNodeDataGroup, resourcesGroup };

        /// <summary>
        /// The id of the Addressables profile that builds the mod's groups to its own load path.
        /// </summary>
        [HideInInspector] public string addressablesProfileId;

        /// <summary>
        /// The mods the mod needs, with the versions it accepts.
        /// </summary>
        [SerializeField] [Tooltip("The mods that this mod depends on")]
        public List<ModDependency> dependencies = new()
        {
            new ModDependency
            {
                id = "SpaceWarp2",
                min = "2.0.0",
                max = "*"
            }
        };

        /// <summary>
        /// The mods the mod cannot run beside, with the versions that conflict.
        /// </summary>
        [SerializeField] [Tooltip("The mods that this mod cannot run with")] [InspectorName("Conflicts")]
        public List<ModDependency> incompatibilities = new()
        {
        };

        /// <summary>
        /// The relations of the mod's own feature beyond what its dependencies and conflicts give it.
        /// </summary>
        [Tooltip("The relations of the mod's own feature beyond what its dependencies and conflicts give it")]
        public ModFeature ownFeature = new();

        /// <summary>
        /// The features the mod declares besides its own.
        /// </summary>
        [Tooltip("The features the mod declares besides its own")]
        public List<ModFeature> features = new();


        /// <summary>
        /// Renames the mod asset to <see cref="PickerDisplayName" /> when it differs.
        /// </summary>
        /// <returns>True if the asset was renamed, false otherwise.</returns>
        public bool SyncPickerDisplayName()
        {
            if (!ModPickerDisplayNameSync.CanTouchAssets())
            {
                return false;
            }

            string desiredName = PickerDisplayName;
            if (string.Equals(((Object)this).name, desiredName, System.StringComparison.Ordinal))
            {
                return false;
            }

            ((Object)this).name = desiredName;
            EditorUtility.SetDirty(this);
            return true;
        }

        private void OnEnable()
        {
            SyncPickerDisplayName();
        }

        private void OnValidate()
        {
            SyncPickerDisplayName();
        }

        /// <inheritdoc />
        public override string Generate()
        {
            var deps = new JArray();
            foreach (ModDependency dep in dependencies)
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
            foreach (ModDependency conflict in incompatibilities)
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
                ["addressable_script_label"] = AddressableScriptLabel,
                ["addressable_prefab_patch_label"] =
                    AddressablePrefabPatchLabel,
                ["ksp2_version"] = ksp2Version,
                ["dependencies"] = deps,
                ["conflicts"] = conflicts
            };
            if (File.Exists(AssemblyPath))
            {
                jObject["main_assembly"] = $"{id}.dll";
            }

            JArray integrationsArray = IntegrationsJson();
            if (integrationsArray.Count > 0)
            {
                jObject["integrations"] = integrationsArray;
            }

            JObject featuresObj = FeaturesJson();
            if (featuresObj.Count > 0)
            {
                jObject["features"] = featuresObj;
            }

            return jObject.ToString();
        }

        /// <summary>
        /// Builds swinfo's features block: the mod's own feature when it has relations of its own, then every other
        /// feature, each with only the lists it fills.
        /// </summary>
        /// <returns>The features by id.</returns>
        public JObject FeaturesJson()
        {
            var featuresObj = new JObject();
            JObject own = FeatureJson(ownFeature);
            if (own.Count > 0)
            {
                featuresObj[id] = own;
            }

            foreach (ModFeature feature in features.Where(feature => !string.IsNullOrWhiteSpace(feature.id)))
            {
                featuresObj[feature.id.Trim()] = FeatureJson(feature);
            }

            return featuresObj;
        }

        private static JObject FeatureJson(ModFeature feature)
        {
            var featureObj = new JObject();
            AddIds(featureObj, "requires", feature.requires);
            AddIds(featureObj, "conflicts", feature.conflicts);
            AddIds(featureObj, "provides", feature.provides);
            if (feature.recommended)
            {
                featureObj["recommended"] = true;
            }

            return featureObj;
        }

        private static void AddIds(JObject featureObj, string key, IEnumerable<string> ids)
        {
            string[] filled = ids.Where(featureId => !string.IsNullOrWhiteSpace(featureId))
                .Select(featureId => featureId.Trim())
                .ToArray();
            if (filled.Length > 0)
            {
                featureObj[key] = new JArray(filled);
            }
        }

        /// <summary>
        /// Creates one ThunderKit pipeline that builds the mod to a folder, and the manifest it builds from.
        /// </summary>
        /// <remarks>
        /// The pipeline bakes the mod's assets first, then stages its copied files and generated text, and with
        /// <paramref name="includeBuiltObjects" /> its Addressables and assembly.
        /// </remarks>
        /// <param name="targetLocation">The folder the mod is built to.</param>
        /// <param name="manifestPath">The asset path the manifest is created at.</param>
        /// <param name="pipelinePath">The asset path the pipeline is created at.</param>
        /// <param name="manifestId">A name told apart from the mod's in the manifest's identity, or null for the mod's id alone.</param>
        /// <param name="includeBuiltObjects">Whether the build includes the mod's Addressables and assembly.</param>
        /// <param name="buildZip">Whether the build ends by zipping the folder.</param>
        public void GeneratePipeline(
            string targetLocation,
            string manifestPath,
            string pipelinePath,
            [CanBeNull] string manifestId = null,
            bool includeBuiltObjects = false,
            bool buildZip = false
        )
        {
            var manifest = CreateInstance<Manifest>();
            manifest.Identity = CreateInstance<ManifestIdentity>();
            manifest.Identity.name = manifestId == null ? $"{id}" : $"{id} - {manifestId}";
            manifest.Identity.Author = author;
            manifest.Identity.Description = description;
            manifest.Identity.Version = version;
            manifest.Identity.Dependencies = new Manifest[] {};
            manifest.Data = new ComposableElement[] { };
            AssetDatabase.CreateAsset(manifest, manifestPath);
            AssetDatabase.AddObjectToAsset(manifest.Identity, manifest);
            var files = CreateInstance<CopyFolderDatum>();
            files.sourcePath = Folder + "/Copied";
            files.destinationPath = targetLocation;
            manifest.InsertElement(files, manifest.Data.Length);
            var textAssets = CreateInstance<TextAssets>();
            textAssets.StagingPaths = new[] { targetLocation };
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
                    foreach (string file in Directory.GetFiles(
                            Folder + "/Code/Lib",
                            "*.asmdef",
                            SearchOption.AllDirectories
                        ))
                    {
                        string relative = Path.GetRelativePath(Folder + "/Code/Lib", file);
                        definitions.Add(
                            AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(Folder + "/Code/Lib/" + relative)
                        );
                    }

                    assembly2.definitions = definitions.ToArray();
                    manifest.InsertElement(assembly2, manifest.Data.Length);
                }

                // Integrations stay out of lib, whose assemblies the game always loads
                string[] integrationPaths = IntegrationAssemblyPaths();
                if (integrationPaths.Length > 0)
                {
                    var integrations = CreateInstance<AssemblyDefinitions>();
                    integrations.StagingPaths = new[] { targetLocation + "/" + INTEGRATIONS_FOLDER };
                    integrations.definitions = integrationPaths
                        .Select(AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>)
                        .ToArray();
                    manifest.InsertElement(integrations, manifest.Data.Length);
                }
            }

            var pipeline = CreateInstance<Pipeline>();
            pipeline.Data = new ComposableElement[] { };
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
            // Bakes first, since its files are what the staging jobs below pick up
            pipeline.InsertElement(CreateInstance<BakeModAssets>(), pipeline.Data.Length);
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

        /// <summary>
        /// Recreates the mod's three pipelines: build for the editor, build for the player, and deploy to a zip file.
        /// </summary>
        public void RefreshPipelines()
        {
            string pipelinesFolder = Folder + "/Pipelines";
            string buildForEditor = pipelinesFolder + "/Build for Editor.asset";
            string buildForEditorManifest = pipelinesFolder + "/Build for Editor Manifest.asset";
            string buildForPlayer = pipelinesFolder + "/Build for Player.asset";
            string buildForPlayerManifest = pipelinesFolder + "/Build for Player Manifest.asset";
            string deployToZipFile = pipelinesFolder + "/Deploy to Zip File.asset";
            string deployToZipFileManifest = pipelinesFolder + "/Deploy to Zip File Manifest.asset";
            if (!Directory.Exists(pipelinesFolder))
            {
                AssetDatabase.CreateFolder(Folder, "Pipelines");
            }

            if (File.Exists(buildForEditor))
            {
                AssetDatabase.DeleteAsset(buildForEditor);
            }

            if (File.Exists(buildForEditorManifest))
            {
                AssetDatabase.DeleteAsset(buildForEditorManifest);
            }

            GeneratePipeline($"Assets/Mods/__Testing/{id}", buildForEditorManifest, buildForEditor, "Editor",true);
            if (File.Exists(buildForPlayer))
            {
                AssetDatabase.DeleteAsset(buildForPlayer);
            }

            if (File.Exists(buildForPlayerManifest))
            {
                AssetDatabase.DeleteAsset(buildForPlayerManifest);
            }

            GeneratePipeline(
                $"{ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>().GamePath}/mods/__Testing/{id}",
                buildForPlayerManifest,
                buildForPlayer,
                "Editor",
                true
            );
            if (File.Exists(deployToZipFile))
            {
                AssetDatabase.DeleteAsset(deployToZipFile);
            }

            if (File.Exists(deployToZipFileManifest))
            {
                AssetDatabase.DeleteAsset(deployToZipFileManifest);
            }

            GeneratePipeline($"Deploy/{id}", deployToZipFileManifest, deployToZipFile, "Deploy", true, true);
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

        /// <summary>
        /// Builds swinfo's integrations block from the integration assemblies under <see cref="IntegrationsFolder" />.
        /// </summary>
        /// <returns>One entry per integration, naming the integrated mod and the built assembly.</returns>
        public JArray IntegrationsJson()
        {
            var integrations = new JArray();
            foreach (string path in IntegrationAssemblyPaths())
            {
                integrations.Add(new JObject
                {
                    ["mod_id"] = Path.GetFileName(Path.GetDirectoryName(path)),
                    ["assembly"] = $"{INTEGRATIONS_FOLDER}/{Path.GetFileNameWithoutExtension(path)}.dll"
                });
            }

            return integrations;
        }

        // One assembly definition directly inside each integrated mod's folder
        private string[] IntegrationAssemblyPaths() =>
            !Directory.Exists(IntegrationsFolder)
                ? Array.Empty<string>()
                : Directory.GetDirectories(IntegrationsFolder)
                    .SelectMany(folder => Directory.GetFiles(folder, "*.asmdef", SearchOption.TopDirectoryOnly))
                    .Select(path => path.Replace(Path.DirectorySeparatorChar, '/'))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();

        /// <summary>
        /// Creates an integration assembly for another mod and an integration class to start from, unless one exists.
        /// </summary>
        /// <remarks>
        /// The assembly references the mod's own assembly. Compiling against the integrated mod is left to the author,
        /// since how its assembly reaches this project depends on that mod.
        /// </remarks>
        /// <param name="integratedModId">The id of the mod to integrate with.</param>
        public void CreateIntegrationAssembly(string integratedModId)
        {
            integratedModId = integratedModId?.Trim();
            if (string.IsNullOrEmpty(integratedModId) || integratedModId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                EditorUtility.DisplayDialog("Error", "Enter the id of the mod to integrate with.", "OK");
                return;
            }

            if (!File.Exists(AssemblyPath))
            {
                EditorUtility.DisplayDialog("Error", "Create the mod's assembly definition first.", "OK");
                return;
            }

            string folder = $"{IntegrationsFolder}/{integratedModId}";
            if (Directory.Exists(folder))
            {
                EditorUtility.DisplayDialog("Error", $"An integration with {integratedModId} already exists.", "OK");
                return;
            }

            string identifier = new string(integratedModId.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
            if (char.IsDigit(identifier[0]))
            {
                identifier = "_" + identifier;
            }

            string assemblyName = $"{id}.Integrations.{identifier}";
            Directory.CreateDirectory(folder);
            var assemblyDefinitionJObject = new JObject
            {
                ["name"] = assemblyName,
                ["rootNamespace"] = assemblyName,
                ["references"] = new JArray(id),
                ["allowUnsafeCode"] = true,
                ["overrideReferences"] = true,
                ["precompiledReferences"] = new JArray(GetPrecompiledReferences()),
                ["autoReferenced"] = false,
                ["defineConstants"] = new JArray(),
                ["versionDefines"] = new JArray(),
                ["noEngineReferences"] = false
            };
            File.WriteAllText($"{folder}/{assemblyName}.asmdef", assemblyDefinitionJObject.ToString());
            File.WriteAllText($"{folder}/{identifier}Integration.cs",
                IntegrationTemplate.Replace("%NAMESPACE%", assemblyName).Replace("%CLASS%", identifier).Trim());
            AssetDatabase.Refresh();
        }

        private const string INTEGRATIONS_FOLDER = "integrations";

        private const string IntegrationTemplate = @"
using SpaceWarp2.API.Mods;
namespace %NAMESPACE%
{
    /* Runs only when the integrated mod is loaded too. Derive from ModIntegration<TCurrent, TIntegrated> instead for typed access to both plugins */
    public class %CLASS%Integration : ModIntegration
    {
        public override void OnInitialized() {
            /*
                Code that runs once both mods are initialized goes here
            */
        }
    }
}
";

        /// <summary>
        /// Creates the mod's assembly definition and a plugin class to start from, unless the assembly exists.
        /// </summary>
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
                ["references"] = { },
                ["allowUnsafeCode"] = true,
                ["overrideReferences"] = true,
                ["precompiledReferences"] = new JArray(GetPrecompiledReferences()),
                ["autoReferenced"] = true,
                ["defineConstants"] = { },
                ["versionDefines"] = { },
                ["noEngineReferences"] = false
            };
            File.WriteAllText(AssemblyPath, assemblyDefinitionJObject.ToString());
            File.WriteAllText(MainPluginPath, Template.Replace("%MOD%", id).Trim());
            AssetDatabase.Refresh();
        }

        private static string[] GetPrecompiledReferences()
        {
            if (_precompiledReferences != null)
            {
                return _precompiledReferences;
            }

            List<string> references = new();
            if (Directory.Exists(Ksp2PackageReferenceDirectory))
            {
                references.AddRange(
                    Directory.GetFiles(Ksp2PackageReferenceDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                        .Select(Path.GetFileName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                );
            }
            else
            {
                Debug.LogWarning(
                    $"Redux SDK: optional reference package '{Ksp2PackageReferenceDirectory}' was not found. " +
                    "Mod assembly generation will use only editor-available references."
                );
            }

            if (!references.Contains("Newtonsoft.Json.dll"))
            {
                references.Add("Newtonsoft.Json.dll");
            }

            _precompiledReferences = references.ToArray();
            return _precompiledReferences;
        }

        private static string[] _usedLabels =
        {
            "parts_data",
            "celestial_bodies",
            "scienceExperiment",
            "missions",
            "techNodeData",
            "resources"
        };

        /// <summary>
        /// Creates the mod's Addressables groups, labels and profile, reusing any that exist.
        /// </summary>
        public void CreateAddressablesGroups()
        {
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(
                    AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
                    AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName,
                    true,
                    true
                );
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            foreach (string label in _usedLabels)
            {
                if (!settings.GetLabels().Contains(label))
                {
                    settings.AddLabel(label);
                }
            }
            foreach (
                string label in new[]
                {
                    AddressableScriptLabel,
                    AddressablePrefabPatchLabel
                }
            )
            {
                if (!settings.GetLabels().Contains(label))
                {
                    settings.AddLabel(label);
                }
            }

            addressablesProfileId = !settings.profileSettings.GetAllProfileNames().Contains(id)
                ? settings.profileSettings.AddProfile(id, settings.activeProfileId)
                : settings.profileSettings.GetProfileId(id);
            settings.profileSettings.SetValue(
                addressablesProfileId,
                "Local.BuildPath",
                $"Library/com.unity.addressables/aa/Windows/StandaloneWindows64"
            );
            settings.profileSettings.SetValue(
                addressablesProfileId,
                "Local.LoadPath",
                $"{{SpaceWarpPaths.{id}}}/addressables/StandaloneWindows64"
            );

            allGroup = settings.groups.FirstOrDefault(x => x.name == $"addressables_{id}_all") ?? settings.CreateGroup(
                $"addressables_{id}_all",
                false,
                false,
                false,
                settings.DefaultGroup.Schemas
            );

            partsGroup = settings.groups.FirstOrDefault(x => x.name == $"{id}_parts") ?? settings.CreateGroup(
                $"{id}_parts",
                false,
                false,
                false,
                settings.DefaultGroup.Schemas
            );
            missionsGroup = settings.groups.FirstOrDefault(x => x.name == $"{id}_missions") ?? settings.CreateGroup(
                $"{id}_missions",
                false,
                false,
                false,
                settings.DefaultGroup.Schemas
            );
            celestialBodiesGroup = settings.groups.FirstOrDefault(x => x.name == $"{id}_cbs") ?? settings.CreateGroup(
                $"{id}_cbs",
                false,
                false,
                false,
                settings.DefaultGroup.Schemas
            );
            scienceExperimentGroup = settings.groups.FirstOrDefault(x => x.name == $"{id}_experiments") ??
                settings.CreateGroup($"{id}_experiments", false, false, false, settings.DefaultGroup.Schemas);
            techNodeDataGroup = settings.groups.FirstOrDefault(x => x.name == $"{id}_tech_nodes") ??
                settings.CreateGroup($"{id}_tech_nodes", false, false, false, settings.DefaultGroup.Schemas);
            resourcesGroup = settings.groups.FirstOrDefault(x => x.name == $"{id}_resources") ??
                settings.CreateGroup($"{id}_resources", false, false, false, settings.DefaultGroup.Schemas);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }

        /// <summary>
        /// Writes the mod's swinfo.json beside the mod asset, the copy a version check URL can point at.
        /// </summary>
        public void CreateVersionCheckSwinfo()
        {
            string text = Generate();
            string location = Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));
            File.WriteAllText(Folder + "/swinfo.json", text);
        }
    }
}
