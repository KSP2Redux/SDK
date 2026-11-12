using System;
using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Import a self-contained little-endian unsigned 16-bit decal grid without color conversion.
    /// </summary>
    /// <remarks>
    /// Unity requires a ScriptedImporter type for the pqsgrid extension. The 24-byte header contains
    /// PQS16LE followed by a zero byte, version 1, width, height and a reserved zero word.
    /// Row-major ushort pixels follow the header with no row or column transformation.
    /// </remarks>
    [ScriptedImporter(1, "pqsgrid")]
    public class PQSDecalGridImporter : ScriptedImporter
    {
        private const int MAX_DIMENSION = 8192;

        /// <inheritdoc />
        public override void OnImportAsset(AssetImportContext ctx)
        {
            byte[] pixels = ReadSource(ctx.assetPath, out int width, out int height);
            var texture = new Texture2D(width, height, TextureFormat.R16, false, true)
            {
                name = Path.GetFileNameWithoutExtension(ctx.assetPath),
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
            };
            try
            {
                texture.LoadRawTextureData(pixels);
                texture.Apply(false, false);
                ctx.AddObjectToAsset("grid", texture);
                ctx.SetMainObject(texture);
            }
            catch
            {
                DestroyImmediate(texture);
                throw;
            }
        }

        /// <summary>
        /// Read exact bounded grid words after validating the complete source header and length.
        /// </summary>
        /// <param name="path">
        /// The pqsgrid source path.
        /// </param>
        /// <param name="width">
        /// The even pixel width required by packed-ushort terrain sampling.
        /// </param>
        /// <param name="height">
        /// The pixel height.
        /// </param>
        /// <returns>
        /// Original little-endian ushort bytes with no numeric or color conversion.
        /// </returns>
        /// <exception cref="InvalidDataException">
        /// Thrown when the header, dimensions, reserved field or exact file length is invalid.
        /// </exception>
        public static byte[] ReadSource(string path, out int width, out int height)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.ASCII);
            if (stream.Length < 24 || Encoding.ASCII.GetString(reader.ReadBytes(8)) != "PQS16LE\0" || reader.ReadInt32() != 1)
                throw new InvalidDataException("Unsupported or truncated pqsgrid header.");
            width = reader.ReadInt32();
            height = reader.ReadInt32();
            if (reader.ReadInt32() != 0 || width < 2 || width > MAX_DIMENSION || width % 2 != 0 || height < 1 || height > MAX_DIMENSION)
                throw new InvalidDataException("Invalid pqsgrid dimensions or reserved field.");
            int bytes = checked(width * height * 2);
            if (stream.Length != 24L + bytes)
                throw new InvalidDataException("Pqsgrid length does not exactly cover its declared words.");
            byte[] pixels = reader.ReadBytes(bytes);
            if (pixels.Length != bytes)
                throw new InvalidDataException("Truncated pqsgrid words.");
            return pixels;
        }
    }
}
