using System;
using System.IO;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.ResourceMaps
{
    /// <summary>
    /// A celestial body's biome mask, held as raw pixels for sampling off the main thread.
    /// </summary>
    /// <remarks>
    /// The masks live outside the Assets folder, so Unity never imports them and there are no
    /// importer settings to get wrong: the PNG is decoded here at its authored resolution rather
    /// than at whatever a texture importer would have downscaled it to.
    /// </remarks>
    public sealed class BiomeMask
    {
        private readonly Color32[] _pixels;

        private BiomeMask(int width, int height, Color32[] pixels)
        {
            Width = width;
            Height = height;
            _pixels = pixels;
        }

        /// <summary>
        /// Gets the mask's width in pixels.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the mask's height in pixels.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Decodes a biome mask from a PNG on disk.
        /// </summary>
        /// <remarks>
        /// Must be called from the main thread, since decoding goes through Unity's image
        /// conversion. The resulting mask is then safe to sample from worker threads.
        /// </remarks>
        /// <param name="absolutePath">Full path to the mask PNG.</param>
        /// <returns>The decoded mask.</returns>
        /// <exception cref="FileNotFoundException">Thrown when no file exists at <paramref name="absolutePath" />.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the file is not a decodable image.</exception>
        public static BiomeMask Load(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                throw new FileNotFoundException($"No biome mask at '{absolutePath}'.", absolutePath);

            byte[] bytes = File.ReadAllBytes(absolutePath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!texture.LoadImage(bytes, false))
                    throw new InvalidOperationException($"'{Path.GetFileName(absolutePath)}' could not be decoded as an image.");
                return new BiomeMask(texture.width, texture.height, texture.GetPixels32());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Returns the four coverage channels at a pixel, each normalized to the range 0 to 1.
        /// </summary>
        /// <param name="x">Column, with 0 at the antimeridian.</param>
        /// <param name="y">Row, with 0 at the south pole.</param>
        /// <returns>The red, green, blue and alpha coverage at that pixel.</returns>
        public Vector4 Sample(int x, int y)
        {
            int clampedX = x < 0 ? 0 : x >= Width ? Width - 1 : x;
            int clampedY = y < 0 ? 0 : y >= Height ? Height - 1 : y;
            Color32 pixel = _pixels[clampedY * Width + clampedX];
            const float INVERSE_BYTE = 1f / 255f;
            return new Vector4(
                pixel.r * INVERSE_BYTE,
                pixel.g * INVERSE_BYTE,
                pixel.b * INVERSE_BYTE,
                pixel.a * INVERSE_BYTE
            );
        }

        /// <summary>
        /// Box averages the mask down to a square of the given side length.
        /// </summary>
        /// <remarks>
        /// Used to build a cheap mask for the interactive previews so a preview pass does not have
        /// to walk sixteen million source pixels every time a slider moves.
        /// </remarks>
        /// <param name="size">Side length of the result, in pixels.</param>
        /// <returns>The downsampled mask, or this instance when it already matches the requested size.</returns>
        public BiomeMask Downsample(int size)
        {
            if (size <= 0 || (Width == size && Height == size))
                return this;

            var result = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                int sourceYStart = y * Height / size;
                int sourceYEnd = Mathf.Max(sourceYStart + 1, (y + 1) * Height / size);
                for (var x = 0; x < size; x++)
                {
                    int sourceXStart = x * Width / size;
                    int sourceXEnd = Mathf.Max(sourceXStart + 1, (x + 1) * Width / size);

                    int red = 0, green = 0, blue = 0, alpha = 0, count = 0;
                    for (int sourceY = sourceYStart; sourceY < sourceYEnd; sourceY++)
                    {
                        int rowOffset = sourceY * Width;
                        for (int sourceX = sourceXStart; sourceX < sourceXEnd; sourceX++)
                        {
                            Color32 pixel = _pixels[rowOffset + sourceX];
                            red += pixel.r;
                            green += pixel.g;
                            blue += pixel.b;
                            alpha += pixel.a;
                            count++;
                        }
                    }

                    result[y * size + x] = new Color32(
                        (byte)(red / count),
                        (byte)(green / count),
                        (byte)(blue / count),
                        (byte)(alpha / count)
                    );
                }
            }

            return new BiomeMask(size, size, result);
        }

        /// <summary>
        /// Reports the highest coverage each channel reaches anywhere in the mask.
        /// </summary>
        /// <remarks>
        /// A channel whose peak is zero carries no biome, which the window uses to grey out that
        /// channel's tab rather than inviting the artist to tune a field nothing will sample.
        /// </remarks>
        /// <returns>Four peaks, each 0 to 1, in red, green, blue and alpha order.</returns>
        public Vector4 GetChannelPeaks()
        {
            byte red = 0, green = 0, blue = 0, alpha = 0;
            foreach (Color32 pixel in _pixels)
            {
                if (pixel.r > red)
                {
                    red = pixel.r;
                }
                if (pixel.g > green)
                {
                    green = pixel.g;
                }
                if (pixel.b > blue)
                {
                    blue = pixel.b;
                }
                if (pixel.a > alpha)
                {
                    alpha = pixel.a;
                }
            }

            const float INVERSE_BYTE = 1f / 255f;
            return new Vector4(red * INVERSE_BYTE, green * INVERSE_BYTE, blue * INVERSE_BYTE, alpha * INVERSE_BYTE);
        }

        /// <summary>
        /// Builds a greyscale preview texture showing one channel of the mask in isolation.
        /// </summary>
        /// <param name="channel">Channel index, 0 for red through 3 for alpha.</param>
        /// <param name="size">Side length of the preview, in pixels.</param>
        /// <returns>A texture the caller owns and is responsible for destroying.</returns>
        public Texture2D CreateChannelPreview(int channel, int size)
        {
            BiomeMask source = Downsample(size);
            var pixels = new Color32[source.Width * source.Height];
            for (var index = 0; index < pixels.Length; index++)
            {
                Color32 pixel = source._pixels[index];
                byte value = channel switch
                {
                    1 => pixel.g,
                    2 => pixel.b,
                    3 => pixel.a,
                    _ => pixel.r,
                };
                pixels[index] = new Color32(value, value, value, 255);
            }

            var texture = new Texture2D(source.Width, source.Height, TextureFormat.RGBA32, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
