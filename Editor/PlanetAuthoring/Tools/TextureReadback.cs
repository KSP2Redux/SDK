using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>GPU-side texture readback helpers used by the planet authoring tools.</summary>
    public static class TextureReadback
    {
        /// <summary>
        /// Reads the pixels of <paramref name="source"/> through a temporary RenderTexture, so the
        /// source asset does not need to be Read / Write enabled.
        /// </summary>
        /// <param name="source">Texture to sample.</param>
        /// <param name="width">Output width, matching <c>source.width</c>.</param>
        /// <param name="height">Output height, matching <c>source.height</c>.</param>
        /// <returns>The pixel buffer in row-major order.</returns>
        public static Color[] BlitReadPixels(Texture source, out int width, out int height)
        {
            width = source.width;
            height = source.height;
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var prevActive = RenderTexture.active;
            var tmp = new Texture2D(width, height, TextureFormat.RGBAFloat, mipChain: false, linear: true);
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                tmp.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tmp.Apply();
                return tmp.GetPixels();
            }
            finally
            {
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(tmp);
            }
        }
    }
}
