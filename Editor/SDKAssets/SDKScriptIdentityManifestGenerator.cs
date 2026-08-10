using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.SDKAssets
{
    internal static class SDKScriptIdentityManifestGenerator
    {
        private static readonly string[] SERIALIZED_ASSET_EXTENSIONS = { ".asset", ".prefab", ".unity" };
        private static readonly string[] GAME_SOURCE_ROOTS = { "Assets/Code/", "Assets/I2/" };

        private static readonly Regex SCRIPT_REFERENCE_PATTERN = new(
            @"m_Script:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]{32}),\s*type:\s*3\s*\}",
            RegexOptions.Compiled
        );

#if REDUX
        [MenuItem("Tools/KSP2 Unity Tools/SDK Assets/Update Script Identity Manifest")]
        private static void UpdateManifestFromMenu()
        {
            bool wasUpdated = UpdateManifest();
            string result = wasUpdated ? "Updated" : "Verified";
            Debug.Log($"{result} the SDK script identity manifest.");
        }

        internal static bool UpdateManifest()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string sourceRoot = Path.Combine(
                projectRoot,
                (SDKConfiguration.BasePath + "/Assets").Replace('/', Path.DirectorySeparatorChar)
            );
            var identities = new Dictionary<string, SDKScriptIdentity>(StringComparer.Ordinal);

            foreach (string sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                if (!SERIALIZED_ASSET_EXTENSIONS.Contains(
                        Path.GetExtension(sourcePath),
                        StringComparer.OrdinalIgnoreCase
                    ))
                    continue;

                foreach (Match match in SCRIPT_REFERENCE_PATTERN.Matches(File.ReadAllText(sourcePath)))
                {
                    string sourceGuid = match.Groups[2].Value;
                    if (identities.ContainsKey(sourceGuid))
                        continue;

                    string scriptPath = AssetDatabase.GUIDToAssetPath(sourceGuid);
                    if (!GAME_SOURCE_ROOTS.Any(root => scriptPath.StartsWith(root, StringComparison.Ordinal)))
                        continue;

                    var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                    Type type = monoScript != null ? monoScript.GetClass() : null;
                    if (type == null)
                        throw new InvalidOperationException(
                            $"The source script at '{scriptPath}' does not resolve to a type."
                        );

                    identities.Add(
                        sourceGuid,
                        new SDKScriptIdentity
                        {
                            SourceGuid = sourceGuid,
                            SourceFileId = long.Parse(match.Groups[1].Value),
                            AssemblyName = type.Assembly.GetName().Name,
                            TypeName = type.FullName
                        }
                    );
                }
            }

            var manifest = new SDKScriptIdentityManifest
            {
                ScriptIdentities = identities.Values.OrderBy(identity => identity.SourceGuid).ToList()
            };
            string manifestPath = Path.Combine(
                projectRoot,
                (SDKConfiguration.BasePath + "/Editor/SDKAssets/ScriptIdentityManifest.json")
                .Replace('/', Path.DirectorySeparatorChar)
            );
            string manifestContents = JsonUtility.ToJson(manifest, true) + Environment.NewLine;
            if (File.Exists(manifestPath) && File.ReadAllText(manifestPath) == manifestContents)
                return false;

            File.WriteAllText(manifestPath, manifestContents, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(SDKConfiguration.BasePath + "/Editor/SDKAssets/ScriptIdentityManifest.json");
            Debug.Log($"Updated the SDK script identity manifest with {manifest.ScriptIdentities.Count} entries.");
            return true;
        }
#endif
    }
}