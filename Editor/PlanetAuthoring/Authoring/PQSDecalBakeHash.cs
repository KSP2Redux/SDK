using System.Globalization;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Computes a stable hash of every input that drives <c>DecalBaker.RebuildForController</c>.
    /// </summary>
    /// <remarks>
    /// Compared against <see cref="PQSDecalControllerAuthoring.LastBakeHash" /> by the validator to
    /// detect when a bake is needed because shared textures, template values, or per-decal source
    /// textures changed since the last bake. Keep this in sync with whatever DecalBaker actually
    /// reads when producing PQSDecalData. Returns the hex-string form of a <see cref="Hash128" />
    /// so equality is fixed-size and the stored value is compact.
    /// </remarks>
    public static class PQSDecalBakeHash
    {
        /// <summary>
        /// Hashes the controller's instance template list, every contributing template's value fields and source textures, and the controller's shared heightmap and alpha map.
        /// </summary>
        /// <param name="controller">The decal controller whose bake inputs are hashed.</param>
        /// <returns>The hex-string form of the input <see cref="Hash128" />, or the empty string when <paramref name="controller" /> is null.</returns>
        public static string Compute(PQSDecalController controller)
        {
            if (controller == null)
                return string.Empty;

            var hash = new Hash128();
            hash.Append("decal-bake-input-v2");
            AppendTexture(ref hash, controller.SharedHeightmap);
            AppendTexture(ref hash, controller.SharedAlphaMap);
            hash.Append((int)controller.DecalHeightFilteringMode);
            hash.Append((int)controller.DecalAlphaFilteringMode);
            hash.Append((int)controller.PQSDataOutputType);
            hash.Append((int)controller.DecalMaskTextureSize);
            hash.Append(controller.UseMaskTexture ? 1 : 0);
            AppendTexture(ref hash, controller.DecalMaskTex);

            // The list survives runtime instance sorting and matches the baker's actual slice mapping.
            foreach (PQSDecal template in DecalBaker.CollectTemplates(controller))
            {
                AppendTemplate(ref hash, template);
            }
            return hash.ToString();
        }

        private static void AppendTemplate(ref Hash128 hash, PQSDecal t)
        {
            hash.Append(t.DecalID);
            hash.Append(t.HeightScale);
            hash.Append((int)t.HeightBlendMode);
            hash.Append(t.HeightOffset);
            hash.Append((int)t.FadeShape);
            hash.Append(t.FadeStrength);
            hash.Append(t.AlbedoOpacity);
            hash.Append(t.NormalOpacity);
            hash.Append(t.GradientOpacity);
            hash.Append(t.Tint.r);
            hash.Append(t.Tint.g);
            hash.Append(t.Tint.b);
            hash.Append(t.Tint.a);
            hash.Append((int)t.NormalBlend);
            hash.Append(t.SortOrder);
            hash.Append(t.UseAlphaMask ? 1 : 0);
            hash.Append(t.UseDecalTexturing ? 1 : 0);
            hash.Append(t.UseTextureAlphaMask ? 1 : 0);
            hash.Append(t.UseTextureHeightmapFade ? 1 : 0);
            hash.Append(t.MaterialScaleFactor);

            // Per-template source textures live on the editor-only sidecar.
            var auth = AuthoringSidecars.Find(t);
            if (auth == null)
            {
                hash.Append("tex:null");
                return;
            }
            AppendTexture(ref hash, auth.HeightMap);
            AppendTexture(ref hash, auth.AlphaMap);
            AppendTexture(ref hash, auth.Diffuse);
            AppendTexture(ref hash, auth.Normal);
            AppendTexture(ref hash, auth.AlphaMaskTexture);
            AppendTexture(ref hash, auth.Peak);
            AppendTexture(ref hash, auth.Slope);
        }

        private static void AppendTexture(ref Hash128 hash, Texture texture)
        {
            if (texture == null)
            {
                hash.Append("texture:null");
                return;
            }
            string path = AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture, out string guid, out long localId);
                hash.Append(guid);
                hash.Append(localId.ToString(CultureInfo.InvariantCulture));
                // Unity caches dependency hashes. Validator ticks never reread the source pixel buffers.
                hash.Append(AssetDatabase.GetAssetDependencyHash(path).ToString());
            }
            else
            {
                hash.Append(texture.GetEntityId().ToString());
            }
            hash.Append(texture.imageContentsHash.ToString());
            // Detect unsaved SetPixelData/Apply edits as well as importer-driven content changes.
            hash.Append(texture.updateCount.ToString(CultureInfo.InvariantCulture));
            hash.Append(texture.width);
            hash.Append(texture.height);
            hash.Append((int)texture.graphicsFormat);
            hash.Append(texture.mipmapCount);
            hash.Append(texture.isReadable ? 1 : 0);
            hash.Append((int)texture.filterMode);
            hash.Append(texture.anisoLevel);
            hash.Append(texture.mipMapBias);
            hash.Append((int)texture.wrapModeU);
            hash.Append((int)texture.wrapModeV);
            hash.Append((int)texture.wrapModeW);
        }
    }
}
