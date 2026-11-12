using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Ksp2UnityTools.Editor.Extensions;
using ThunderKit.Core.Attributes;
using ThunderKit.Core.Data;
using ThunderKit.Core.Pipelines;

namespace Ksp2UnityTools.Editor.Modding.Thunderkit
{
    /// <summary>
    /// Checks the selected Windows source player before the SDK changes imported content.
    /// </summary>
    [PipelineSupport(typeof(Pipeline))]
    public class ValidateSourcePlayerImport : PipelineJob
    {
        /// <inheritdoc />
        public override Task Execute(Pipeline pipeline)
        {
            var settings = ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>();
            string sourceRoot = ValidatePlayer(settings.GamePath, Redux.ReduxAssets.RuntimeLoadPath,
                Redux.ReduxAssets.Platform, out byte[] catalogHash);
#if !REDUX && TK_ADDRESSABLE
            LoadTkImportedCatalog.RequireSourcePlayerCatalog(sourceRoot.Replace('\\', '/'), catalogHash);
#endif
            return Task.CompletedTask;
        }

        internal static string ValidatePlayer(string gamePath, string relativeRoot, string platform, out byte[] catalogHash)
        {
            if (string.IsNullOrWhiteSpace(gamePath))
                throw new InvalidOperationException("Select a Windows source player in ThunderKit settings before importing.");

            string player = Path.GetFullPath(gamePath);
            string data = Path.Combine(player, "KSP2_x64_Data");
            string sourceRoot = Path.GetFullPath(Path.Combine(player, relativeRoot));
            string sourceCatalog = Path.Combine(sourceRoot, platform, "catalog.json");
            string playerCatalog = Path.Combine(data, "StreamingAssets", "aa", "catalog.json");
            foreach (string path in new[]
                     {
                         Path.Combine(player, "KSP2_x64.exe"),
                         Path.Combine(data, "Managed", "Ksp2Redux.dll"),
                         sourceCatalog,
                         playerCatalog,
                         Path.Combine(data, "StreamingAssets", "Audio", "GeneratedSoundBanks", "Windows", "Init.bnk")
                     })
            {
                if (!File.Exists(path))
                    throw new FileNotFoundException("A required Windows source player file is missing at " + path, path);
            }

            using var source = File.OpenRead(sourceCatalog);
            using var delivered = File.OpenRead(playerCatalog);
            using var hash = SHA256.Create();
            catalogHash = source.Length > 0 && source.Length == delivered.Length ? hash.ComputeHash(source) : null;
            if (catalogHash == null || !catalogHash.SequenceEqual(hash.ComputeHash(delivered)))
                throw new InvalidDataException("The selected game's Unity and Redux catalogs do not match. Select a complete source player.");
            return sourceRoot;
        }
    }
}
