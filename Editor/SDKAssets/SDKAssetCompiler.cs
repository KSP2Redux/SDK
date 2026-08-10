using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.SDKAssets
{
    internal static class SDKAssetCompiler
    {
        private const int COMPILER_VERSION = 1;
        private const string GENERATED_ROOT = "Assets/KSP2UnityTools/GeneratedSDKAssets";
        private const string GENERATION_INFO_FILE = "GenerationInfo.json";
        private const string GUID_NAMESPACE = "ksp2unitytools-sdk-assets-v1:";
        private static readonly string[] SERIALIZED_ASSET_EXTENSIONS = { ".asset", ".prefab", ".unity" };

        private static readonly Regex META_GUID_PATTERN = new(
            @"^guid:\s*([0-9a-f]{32})\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline
        );

        internal static int Compile()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string sourceRoot = ToAbsolutePath(projectRoot, SDKConfiguration.BasePath + "/Assets");
            if (!Directory.Exists(sourceRoot))
                throw new DirectoryNotFoundException(
                    $"KSP2UnityTools SDK asset source was not found at '{sourceRoot}'."
                );

            SDKScriptIdentityManifest manifest = LoadManifest(projectRoot);
            Dictionary<string, CompiledScriptIdentity> compiledScripts = ResolveCompiledScripts(manifest);
            List<SourceAsset> sourceAssets = FindSourceAssets(sourceRoot, compiledScripts.Keys);
            Dictionary<string, string> generatedAssetGuids = BuildGeneratedAssetGuidMap(sourceAssets);
            string generatedRoot = ToAbsolutePath(projectRoot, GENERATED_ROOT);
            ValidateGeneratedRoot(generatedRoot);

            var generatedFiles = new List<string>(sourceAssets.Count);
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (SourceAsset sourceAsset in sourceAssets)
                {
                    string generatedAssetPath = GenerateAsset(
                        projectRoot,
                        sourceAsset,
                        compiledScripts,
                        generatedAssetGuids
                    );
                    generatedFiles.Add(generatedAssetPath);
                }

                PruneStaleFiles(projectRoot, generatedFiles);
                WriteGenerationInfo(projectRoot, generatedFiles);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }

            return generatedFiles.Count;
        }

        private static SDKScriptIdentityManifest LoadManifest(string projectRoot)
        {
            string manifestAssetPath = SDKConfiguration.BasePath + "/Editor/SDKAssets/ScriptIdentityManifest.json";
            string manifestPath = ToAbsolutePath(projectRoot, manifestAssetPath);
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("The SDK script identity manifest is missing.", manifestPath);

            var manifest = JsonUtility.FromJson<SDKScriptIdentityManifest>(File.ReadAllText(manifestPath));
            if (manifest is not { Version: COMPILER_VERSION } || manifest.ScriptIdentities.Count == 0)
                throw new InvalidDataException(
                    $"The SDK script identity manifest at '{manifestAssetPath}' is invalid or unsupported."
                );

            return manifest;
        }

        private static Dictionary<string, CompiledScriptIdentity> ResolveCompiledScripts(
            SDKScriptIdentityManifest manifest
        )
        {
            var result = new Dictionary<string, CompiledScriptIdentity>(StringComparer.Ordinal);
            foreach (SDKScriptIdentity identity in manifest.ScriptIdentities)
            {
                MonoScript monoScript = FindCompiledMonoScript(identity);
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(monoScript, out string guid, out long fileId))
                {
                    throw new InvalidOperationException(
                        $"Unity did not provide a serialized identity for '{identity.TypeName}' " +
                        $"from '{identity.AssemblyName}'."
                    );
                }

                result.Add(
                    identity.SourceGuid,
                    new CompiledScriptIdentity(identity.SourceFileId, guid, fileId)
                );
            }

            return result;
        }

        private static MonoScript FindCompiledMonoScript(SDKScriptIdentity identity)
        {
            string[] candidateGuids = AssetDatabase.FindAssets(identity.AssemblyName);
            MonoScript match = null;
            foreach (string candidateGuid in candidateGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(candidateGuid);
                if (!assetPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetFileNameWithoutExtension(assetPath) != identity.AssemblyName)
                {
                    continue;
                }

                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                {
                    if (asset is not MonoScript monoScript)
                        continue;

                    Type type = monoScript.GetClass();
                    if (type == null || type.FullName != identity.TypeName ||
                        type.Assembly.GetName().Name != identity.AssemblyName)
                    {
                        continue;
                    }

                    if (match != null)
                    {
                        throw new InvalidOperationException(
                            $"More than one imported MonoScript matches '{identity.TypeName}' from " +
                            $"'{identity.AssemblyName}'."
                        );
                    }

                    match = monoScript;
                }
            }

            if (match == null)
            {
                throw new InvalidOperationException(
                    $"The imported MonoScript '{identity.TypeName}' was not found in '{identity.AssemblyName}.dll'. " +
                    $"Run the ThunderKit game import first."
                );
            }

            return match;
        }

        private static List<SourceAsset> FindSourceAssets(string sourceRoot, ICollection<string> sourceScriptGuids)
        {
            var result = new List<SourceAsset>();
            foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                if (!SERIALIZED_ASSET_EXTENSIONS.Contains(
                        Path.GetExtension(sourcePath),
                        StringComparer.OrdinalIgnoreCase
                    ))
                    continue;

                string content = File.ReadAllText(sourcePath);
                if (!sourceScriptGuids.Any(sourceGuid => content.Contains(sourceGuid, StringComparison.Ordinal)))
                    continue;

                string metaPath = sourcePath + ".meta";
                if (!File.Exists(metaPath))
                    throw new FileNotFoundException("An SDK source asset has no metadata file.", metaPath);

                Match guidMatch = META_GUID_PATTERN.Match(File.ReadAllText(metaPath));
                if (!guidMatch.Success)
                    throw new InvalidDataException($"The SDK source metadata at '{metaPath}' has no asset GUID.");

                string relativePath = Path.GetRelativePath(sourceRoot, sourcePath).Replace('\\', '/');
                result.Add(new SourceAsset(sourcePath, metaPath, relativePath, guidMatch.Groups[1].Value));
            }

            return result;
        }

        private static Dictionary<string, string> BuildGeneratedAssetGuidMap(IEnumerable<SourceAsset> sourceAssets) =>
            sourceAssets.ToDictionary(
                sourceAsset => sourceAsset.SourceGuid,
                sourceAsset => CreateDeterministicGuid(sourceAsset.SourceGuid),
                StringComparer.Ordinal
            );

        private static string GenerateAsset(
            string projectRoot,
            SourceAsset sourceAsset,
            IReadOnlyDictionary<string, CompiledScriptIdentity> compiledScripts,
            IReadOnlyDictionary<string, string> generatedAssetGuids
        )
        {
            string content = File.ReadAllText(sourceAsset.SourcePath);
            foreach ((string sourceGuid, CompiledScriptIdentity compiledScript) in compiledScripts)
            {
                string pattern =
                    $@"(m_Script:\s*\{{fileID:\s*){compiledScript.SourceFileId}(,\s*guid:\s*){Regex.Escape(sourceGuid)}(,\s*type:\s*3\s*\}})";
                content = Regex.Replace(
                    content,
                    pattern,
                    match =>
                        $"{match.Groups[1].Value}{compiledScript.FileId}{match.Groups[2].Value}{compiledScript.Guid}{match.Groups[3].Value}",
                    RegexOptions.CultureInvariant
                );
            }

            foreach ((string sourceGuid, string generatedGuid) in generatedAssetGuids)
            {
                content = content.Replace($"guid: {sourceGuid}", $"guid: {generatedGuid}", StringComparison.Ordinal);
            }

            string generatedAssetPath = GENERATED_ROOT + "/" + sourceAsset.RelativePath;
            string outputPath = ToAbsolutePath(projectRoot, generatedAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            WriteIfChanged(outputPath, content);

            string metaContent = File.ReadAllText(sourceAsset.MetaPath);
            metaContent = META_GUID_PATTERN.Replace(
                metaContent,
                $"guid: {generatedAssetGuids[sourceAsset.SourceGuid]}",
                1
            );
            WriteIfChanged(outputPath + ".meta", metaContent);
            return generatedAssetPath;
        }

        private static void ValidateGeneratedRoot(string generatedRoot)
        {
            if (!Directory.Exists(generatedRoot))
            {
                Directory.CreateDirectory(generatedRoot);
                return;
            }

            string generationInfoPath = Path.Combine(generatedRoot, GENERATION_INFO_FILE);
            if (!File.Exists(generationInfoPath) && Directory.EnumerateFileSystemEntries(generatedRoot).Any())
            {
                throw new InvalidOperationException(
                    $"The generated SDK asset folder '{GENERATED_ROOT}' contains files that are not owned by the SDK compiler."
                );
            }
        }

        private static void PruneStaleFiles(string projectRoot, IReadOnlyCollection<string> generatedFiles)
        {
            string generationInfoPath = ToAbsolutePath(projectRoot, GENERATED_ROOT + "/" + GENERATION_INFO_FILE);
            if (!File.Exists(generationInfoPath))
                return;

            var previous = JsonUtility.FromJson<SDKAssetGenerationInfo>(File.ReadAllText(generationInfoPath));
            if (previous == null)
                return;

            var currentFiles = new HashSet<string>(generatedFiles, StringComparer.Ordinal);
            foreach (string staleAssetPath in previous.GeneratedFiles)
            {
                if (currentFiles.Contains(staleAssetPath) || !IsOwnedGeneratedPath(staleAssetPath))
                    continue;

                string stalePath = ToAbsolutePath(projectRoot, staleAssetPath);
                if (File.Exists(stalePath))
                    File.Delete(stalePath);
                if (File.Exists(stalePath + ".meta"))
                    File.Delete(stalePath + ".meta");
            }
        }

        private static void WriteGenerationInfo(string projectRoot, List<string> generatedFiles)
        {
            generatedFiles.Sort(StringComparer.Ordinal);
            var generationInfo = new SDKAssetGenerationInfo
            {
                CompilerVersion = COMPILER_VERSION,
                GeneratedFiles = generatedFiles
            };
            string generationInfoPath = ToAbsolutePath(projectRoot, GENERATED_ROOT + "/" + GENERATION_INFO_FILE);
            WriteIfChanged(generationInfoPath, JsonUtility.ToJson(generationInfo, true) + Environment.NewLine);

            string generationInfoMetaPath = generationInfoPath + ".meta";
            if (!File.Exists(generationInfoMetaPath))
            {
                string meta =
                    $"fileFormatVersion: 2{Environment.NewLine}guid: {CreateDeterministicGuid(GENERATION_INFO_FILE)}{Environment.NewLine}TextScriptImporter:{Environment.NewLine}  externalObjects: {{}}{Environment.NewLine}  userData: {Environment.NewLine}  assetBundleName: {Environment.NewLine}  assetBundleVariant: {Environment.NewLine}";
                File.WriteAllText(generationInfoMetaPath, meta, new UTF8Encoding(false));
            }
        }

        private static bool IsOwnedGeneratedPath(string assetPath) =>
            assetPath.StartsWith(GENERATED_ROOT + "/", StringComparison.Ordinal) &&
            !assetPath.Contains("../", StringComparison.Ordinal);

        private static string CreateDeterministicGuid(string identity)
        {
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(GUID_NAMESPACE + identity));
            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }

        private static void WriteIfChanged(string path, string content)
        {
            if (File.Exists(path) && File.ReadAllText(path) == content)
                return;

            File.WriteAllText(path, content, new UTF8Encoding(false));
        }

        private static string ToAbsolutePath(string projectRoot, string assetPath) =>
            Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));

        private sealed class CompiledScriptIdentity
        {
            internal CompiledScriptIdentity(long sourceFileId, string guid, long fileId)
            {
                SourceFileId = sourceFileId;
                Guid = guid;
                FileId = fileId;
            }

            internal long SourceFileId { get; }

            internal string Guid { get; }

            internal long FileId { get; }
        }

        private sealed class SourceAsset
        {
            internal SourceAsset(
                string sourcePath,
                string metaPath,
                string relativePath,
                string sourceGuid
            )
            {
                SourcePath = sourcePath;
                MetaPath = metaPath;
                RelativePath = relativePath;
                SourceGuid = sourceGuid;
            }

            internal string SourcePath { get; }

            internal string MetaPath { get; }

            internal string RelativePath { get; }

            internal string SourceGuid { get; }
        }
    }
}