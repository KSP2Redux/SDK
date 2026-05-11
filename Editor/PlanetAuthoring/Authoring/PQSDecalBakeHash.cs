using System.Collections.Generic;
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
            if (controller == null) return string.Empty;

            var hash = new Hash128();
            hash.Append(GuidOrEmpty(controller.SharedHeightmap));
            hash.Append(GuidOrEmpty(controller.SharedAlphaMap));

            // Unique templates in instance order, deduplicated. Mirrors DecalBaker.CollectTemplates.
            var seen = new HashSet<PQSDecal>();
            if (controller.PqsDecalInstanceList != null)
            {
                foreach (var inst in controller.PqsDecalInstanceList)
                {
                    if (inst == null || inst.PQSDecal == null) continue;
                    if (!seen.Add(inst.PQSDecal)) continue;
                    AppendTemplate(ref hash, inst.PQSDecal);
                }
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
            var auth = PlanetAuthoringRegistry.Instance.FindDecalTemplate(t.DecalID);
            if (auth == null)
            {
                hash.Append("tex:null");
                return;
            }
            hash.Append(GuidOrEmpty(auth.Diffuse));
            hash.Append(GuidOrEmpty(auth.Normal));
            hash.Append(GuidOrEmpty(auth.AlphaMaskTexture));
            hash.Append(GuidOrEmpty(auth.Peak));
            hash.Append(GuidOrEmpty(auth.Slope));
        }

        private static string GuidOrEmpty(Object obj)
        {
            if (obj == null) return string.Empty;
            var path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return AssetDatabase.AssetPathToGUID(path);
        }
    }
}
