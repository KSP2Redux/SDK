using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP;
using KSP.IO;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.Modding;
using Ksp2UnityTools.Editor.PartAuthoring.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Redux.Ksp1Import;
using Redux.Ksp1Import.Config;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.PartAuthoring
{
    internal static class Ksp1EditorAssetWriter
    {
        private const string PaintMaskProperty = "_PaintMaskGlossMap";
        private const string BlankPaintMaskName = "ksp1_blank_paint_mask";

        public static void SavePart(
            GameObject prefab,
            PartCore core,
            string rawJson,
            Mod targetMod,
            string partFolder,
            Ksp1EditorImportManifest manifest,
            bool overwriteGenerated,
            Ksp1ImportReport report
        )
        {
            CorePartData corePartData = prefab.GetComponent<CorePartData>() ?? prefab.AddComponent<CorePartData>();
            corePartData.Data = core.data;
            Ksp1EditorPartModuleSync.Sync(prefab, core, report);
            prefab.SetActive(true);

            DragCubeBaker.BakeResult dragResult = DragCubeBaker.Bake(prefab);
            if (dragResult.Success)
            {
                report.Info($"Part '{core.data.partName}' editor-baked {dragResult.CubeCount} drag cube{(dragResult.CubeCount == 1 ? string.Empty : "s")}.");
            }
            else if (!dragResult.IsSkipped)
            {
                report.Warn($"Part '{core.data.partName}' drag cube bake failed: {dragResult.Message}");
            }

            string prefabPath = $"{partFolder}/{core.data.partName}.prefab";
            string jsonPath = $"{partFolder}/{core.data.partName}.json";
            if (!Ksp1EditorAssetUtility.CanWrite(prefabPath, manifest, overwriteGenerated, report) ||
                !Ksp1EditorAssetUtility.CanWrite(jsonPath, manifest, overwriteGenerated, report))
            {
                return;
            }

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            if (savedPrefab == null)
            {
                report.Warn($"Part '{core.data.partName}' prefab could not be saved to '{prefabPath}'.");
                return;
            }

            File.WriteAllText(jsonPath, Ksp1EditorAssetUtility.FormatJson(SerializeCore(core, rawJson)));
            AssetDatabase.ImportAsset(jsonPath);
            manifest.MarkGenerated(prefabPath);
            manifest.MarkGenerated(jsonPath);

            AddressableAssetGroup group = targetMod.partsGroup ?? PartAuthoringAddressables.ResolveGroup(corePartData);
            if (group != null)
            {
                AddressablesTools.MakeAddressable(group, prefabPath, PartAuthoringAddressables.GetPrefabAddress(core.data.partName));
                AddressablesTools.MakeAddressable(group, jsonPath, $"{core.data.partName}.json", "parts_data");
            }
            else
            {
                report.Warn($"Part '{core.data.partName}' was saved but no parts addressables group was available.");
            }

            CorePartData savedCore = savedPrefab.GetComponent<CorePartData>();
            if (savedCore != null)
            {
                string iconPath = $"{partFolder}/{core.data.partName}_icon.png";
                if (Ksp1EditorAssetUtility.CanWrite(iconPath, manifest, overwriteGenerated, report))
                {
                    PartIconBaker.Bake(savedCore, false);
                    manifest.MarkGenerated(iconPath);
                }

                string reentryStatus = BakeReentryMeshesInPrefabContents(prefabPath);
                if (!string.IsNullOrWhiteSpace(reentryStatus))
                {
                    report.Info($"Part '{core.data.partName}' reentry mesh bake: {reentryStatus}");
                    MarkGeneratedReentryMeshes(partFolder, core.data.partName, manifest);
                }
            }
        }

        public static void ImportResources(
            IEnumerable<Ksp1ConfigFile> configFiles,
            Mod targetMod,
            string resourceRoot,
            Ksp1EditorImportManifest manifest,
            bool overwriteGenerated,
            Ksp1ImportReport report
        )
        {
            foreach (Ksp1ConfigFile file in configFiles)
            {
                foreach (Ksp1ConfigNode resourceNode in file.Node.GetNodes("RESOURCE_DEFINITION"))
                {
                    string name = resourceNode.GetValue("name");
                    if (string.IsNullOrWhiteSpace(name) || Ksp1ResourceMapper.IsKnownMappedResource(name))
                    {
                        continue;
                    }

                    string path = $"{resourceRoot}/{Ksp1EditorAssetUtility.SanitizePathSegment(name)}.json";
                    if (!Ksp1EditorAssetUtility.CanWrite(path, manifest, overwriteGenerated, report))
                    {
                        continue;
                    }

                    JObject wrapper = new()
                    {
                        ["version"] = 0.1,
                        ["useExternal"] = false,
                        ["data"] = JObject.FromObject(Ksp1ResourceMapper.ToResourceDefinition(resourceNode))
                    };
                    File.WriteAllText(path, wrapper.ToString(Formatting.Indented));
                    AssetDatabase.ImportAsset(path);
                    manifest.MarkGenerated(path);

                    if (targetMod.resourcesGroup != null)
                    {
                        AddressablesTools.MakeAddressable(targetMod.resourcesGroup, path, $"{name}.json", "resources");
                    }

                    report.ResourceImported(name);
                }
            }
        }

        public static void PersistTransientAssets(GameObject prefab, string partFolder)
        {
            string meshFolder = Ksp1EditorAssetUtility.EnsureFolder(partFolder, "Meshes");
            string materialFolder = Ksp1EditorAssetUtility.EnsureFolder(partFolder, "Materials");
            string textureFolder = Ksp1EditorAssetUtility.EnsureFolder(partFolder, "Textures");
            HashSet<Object> persisted = new();

            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                foreach (Material material in materials.Where(material => material != null))
                {
                    EnsureBlankPaintMask(material, textureFolder);
                    PersistMaterialTextures(material, textureFolder, persisted);
                    PersistAsset(material, materialFolder, material.name, "mat", persisted);
                }
            }

            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null)
                {
                    PersistAsset(filter.sharedMesh, meshFolder, filter.sharedMesh.name, "asset", persisted);
                }
            }

            foreach (SkinnedMeshRenderer renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh != null)
                {
                    PersistAsset(renderer.sharedMesh, meshFolder, renderer.sharedMesh.name, "asset", persisted);
                }
            }

            foreach (MeshCollider collider in prefab.GetComponentsInChildren<MeshCollider>(true))
            {
                if (collider.sharedMesh != null)
                {
                    PersistAsset(collider.sharedMesh, meshFolder, collider.sharedMesh.name, "asset", persisted);
                }
            }
        }

        private static string SerializeCore(PartCore core, string fallbackJson)
        {
            if (core == null)
            {
                return fallbackJson;
            }

            try
            {
                IOProvider.Init();
                return IOProvider.ToJson(core);
            }
            catch
            {
                return fallbackJson;
            }
        }

        private static void MarkGeneratedReentryMeshes(string partFolder, string partName, Ksp1EditorImportManifest manifest)
        {
            string directory = $"{partFolder}/ReentryMeshes";
            if (!Directory.Exists(directory))
            {
                return;
            }

            string safeName = SanitizeAssetName(partName);
            foreach (string file in Directory.GetFiles(directory, $"{safeName}_*_lod*.asset"))
            {
                manifest.MarkGenerated(file.Replace('\\', '/'));
            }
        }

        private static string BakeReentryMeshesInPrefabContents(string prefabPath)
        {
            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(prefabPath);
                CorePartData core = contents.GetComponent<CorePartData>();
                if (core == null)
                {
                    return string.Empty;
                }

                string status = ReentryMeshBaker.Bake(core, prefabPath, false);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                return status;
            }
            finally
            {
                if (contents != null)
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "part";
            }

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Path.GetInvalidFileNameChars().Contains(chars[i]) || chars[i] == ' ')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static void PersistMaterialTextures(Material material, string textureFolder, HashSet<Object> persisted)
        {
            foreach (string propertyName in material.GetTexturePropertyNames())
            {
                if (material.GetTexture(propertyName) is Texture2D texture)
                {
                    PersistAsset(texture, textureFolder, texture.name, "asset", persisted);
                }
            }
        }

        private static void EnsureBlankPaintMask(Material material, string textureFolder)
        {
            if (material == null ||
                !material.HasProperty(PaintMaskProperty) ||
                material.GetTexture(PaintMaskProperty) != null)
            {
                return;
            }

            string path = $"{textureFolder}/{BlankPaintMaskName}.asset";
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (mask == null)
            {
                mask = new Texture2D(1, 1, TextureFormat.RGBA32, false, true)
                {
                    name = BlankPaintMaskName,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat
                };
                mask.SetPixel(0, 0, Color.clear);
                mask.Apply(false, true);
                AssetDatabase.CreateAsset(mask, path);
                AssetDatabase.ImportAsset(path);
            }

            material.SetTexture(PaintMaskProperty, mask);
        }

        private static void PersistAsset(Object asset, string folder, string name, string extension, HashSet<Object> persisted)
        {
            if (asset == null || EditorUtility.IsPersistent(asset) || !persisted.Add(asset))
            {
                return;
            }

            string safeName = Ksp1EditorAssetUtility.SanitizePathSegment(string.IsNullOrWhiteSpace(name) ? asset.GetType().Name : name);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName}.{extension}");
            AssetDatabase.CreateAsset(asset, path);
        }
    }

    internal static class Ksp1EditorAssetUtility
    {
        public static bool CanWrite(
            string path,
            Ksp1EditorImportManifest manifest,
            bool overwriteGenerated,
            Ksp1ImportReport report
        )
        {
            if (!File.Exists(path))
            {
                return true;
            }

            if (overwriteGenerated && manifest.IsGenerated(path))
            {
                return true;
            }

            report.Warn($"Skipping existing non-generated asset '{path}'.");
            return false;
        }

        public static string EnsureFolder(string parent, string child)
        {
            parent = parent.Replace('\\', '/').TrimEnd('/');
            string path = $"{parent}/{child}";
            if (AssetDatabase.IsValidFolder(path))
            {
                return path;
            }

            string[] segments = child.Split('/');
            string current = parent;
            foreach (string segment in segments)
            {
                string next = $"{current}/{segment}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segment);
                }

                current = next;
            }

            return path;
        }

        public static string FormatJson(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return "";
            }

            return JObject.Parse(rawJson).ToString(Formatting.Indented);
        }

        public static string SanitizePathSegment(string value)
        {
            string sanitized = string.Join("_", (value ?? "unnamed").Split(Path.GetInvalidFileNameChars()));
            return string.IsNullOrWhiteSpace(sanitized) ? "unnamed" : sanitized;
        }
    }
}
