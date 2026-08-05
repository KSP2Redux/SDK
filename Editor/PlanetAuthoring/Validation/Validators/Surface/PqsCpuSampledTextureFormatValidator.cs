using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Surface
{
    /// <summary>
    /// Errors when a CPU-sampled <c>PQSData.heightMapInfo</c> texture imports in a format the height
    /// sampling jobs cannot reinterpret.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>PQSJobUtil</c> hands these textures to the jobs as raw buffers and reinterprets them
    /// element-wise. The mask becomes <c>Color32</c> at 4 bytes per pixel and every height map becomes
    /// <c>ushort</c> at 2 bytes per pixel, then the jobs index those buffers up to width times height. A
    /// compressed import breaks that contract, because the raw buffer holds block data rather than
    /// pixels and is a fraction of the expected size, so the index runs off the end. With collections
    /// safety checks disabled in a player build the read does not trap, it faults, and the caller sees a
    /// NullReferenceException out of the sampling job.
    /// </para>
    /// <para>
    /// This shipped once already. Beyl's biome mask imported as DXT5 at 4096x4096, giving a 22 MB raw
    /// buffer where the job indexed 67 MB, so <c>PQS.GetSurfaceHeight</c> threw on most of the sphere.
    /// That took out the whole sim update loop rather than just the terrain, because
    /// <c>TelemetryComponent</c> calls it every frame a vessel is in the body's sphere of influence.
    /// </para>
    /// <para>
    /// Reads import metadata only and never texture contents, so it stays cheap enough for the inspector
    /// tick and fires while the artist still has the body open.
    /// </para>
    /// </remarks>
    public sealed class PqsCpuSampledTextureFormatValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code emitted when a slot's texture format cannot carry the element the jobs read.
        /// </summary>
        public const string CodeFormat = "PQS_CPU_TEXTURE_FORMAT";

        /// <summary>
        /// Stable code emitted when a slot's texture is not marked Read/Write.
        /// </summary>
        public const string CodeUnreadable = "PQS_CPU_TEXTURE_UNREADABLE";

        /// <summary>
        /// Stable code emitted when a slot's texture can lose its top mip to the global mipmap limit.
        /// </summary>
        public const string CodeMipmapLimit = "PQS_CPU_TEXTURE_MIPMAP_LIMIT";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null || body.Core?.data == null)
                yield break;

            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs == null)
                yield break;

            PQSData.HeightMapInfo info = pqs.data?.heightMapInfo;
            if (info == null)
                yield break;

            string bodyName = body.Core.data.bodyName;

            // The mask is the only slot the jobs read as Color32. Everything else is a 16-bit height map.
            foreach (ValidationIssue issue in CheckSlot(bodyName, "mask", info.mask, MASK_ELEMENT, MASK_FORMAT, MASK_BYTES_PER_PIXEL))
            {
                yield return issue;
            }

            foreach ((string slot, Texture2D texture) in EnumerateHeightSlots(info))
            {
                foreach (ValidationIssue issue in CheckSlot(bodyName, slot, texture, HEIGHT_ELEMENT, HEIGHT_FORMAT, HEIGHT_BYTES_PER_PIXEL))
                {
                    yield return issue;
                }
            }
        }

        private const string MASK_ELEMENT = "Color32";

        private const string MASK_FORMAT = "RGBA32";

        private const int MASK_BYTES_PER_PIXEL = 4;

        private const string HEIGHT_ELEMENT = "ushort";

        private const string HEIGHT_FORMAT = "R16";

        private const int HEIGHT_BYTES_PER_PIXEL = 2;

        private static IEnumerable<(string Slot, Texture2D Texture)> EnumerateHeightSlots(PQSData.HeightMapInfo info)
        {
            yield return ("globalHeightMap", info.globalHeightMap);
            yield return ("largeR", info.largeR?.heightMap);
            yield return ("largeG", info.largeG?.heightMap);
            yield return ("largeB", info.largeB?.heightMap);
            yield return ("largeA", info.largeA?.heightMap);
            yield return ("mediumR", info.mediumR?.heightMap);
            yield return ("mediumG", info.mediumG?.heightMap);
            yield return ("mediumB", info.mediumB?.heightMap);
            yield return ("mediumA", info.mediumA?.heightMap);
        }

        private static IEnumerable<ValidationIssue> CheckSlot(
            string bodyName,
            string slot,
            Texture2D texture,
            string elementName,
            string expectedFormat,
            int expectedBytesPerPixel)
        {
            // An empty slot is MissingLayerHeightmapValidator's call to make, not this one's.
            if (texture == null)
                yield break;

            if (!texture.isReadable)
            {
                yield return new ValidationIssue(
                    CodeUnreadable,
                    ValidationSeverity.Error,
                    $"Body '{bodyName}' heightMapInfo slot '{slot}' texture '{texture.name}' is not marked " +
                    "Read/Write. The height sampling jobs read its raw buffer on the CPU and fault without it. " +
                    "Enable Read/Write on the texture importer.");
            }

            if (BytesPerPixel(texture.format) != expectedBytesPerPixel)
            {
                yield return new ValidationIssue(
                    CodeFormat,
                    ValidationSeverity.Error,
                    $"Body '{bodyName}' heightMapInfo slot '{slot}' texture '{texture.name}' imports as " +
                    $"{texture.format}, which the sampling jobs cannot reinterpret as {elementName}. Set the " +
                    $"Default platform format to {expectedFormat} on the texture importer instead of leaving it " +
                    "Automatic, which resolves to a compressed format and truncates the raw buffer.");
            }

            // Mipmaps are not wrong on their own. A CPU-sampled map that does not opt out of the global
            // mipmap limit can lose its top mip at load, which shrinks the buffer the same way a
            // compressed import does.
            if (texture.mipmapCount > 1 && !IgnoresMipmapLimit(texture))
            {
                yield return new ValidationIssue(
                    CodeMipmapLimit,
                    ValidationSeverity.Warning,
                    $"Body '{bodyName}' heightMapInfo slot '{slot}' texture '{texture.name}' has mipmaps but " +
                    "does not set Ignore Mipmap Limit. A quality level that applies a mipmap limit drops its " +
                    "top mip and shrinks the buffer the sampling jobs index. Disable mipmaps or set Ignore " +
                    "Mipmap Limit.");
            }
        }

        /// <returns>
        /// Bytes per pixel for the uncompressed formats the jobs can reinterpret, or 0 for anything else
        /// including every compressed format.
        /// </returns>
        private static int BytesPerPixel(TextureFormat format) => format switch
        {
            TextureFormat.RGBA32 or TextureFormat.ARGB32 or TextureFormat.BGRA32 => 4,
            TextureFormat.R16 => 2,
            _ => 0,
        };

        private static bool IgnoresMipmapLimit(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
                return true;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            return importer == null || importer.ignoreMipmapLimit;
        }
    }
}
