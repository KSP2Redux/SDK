using System.Collections.Generic;
using System.IO;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Ocean
{
    /// <summary>
    /// Gives a celestial body an ocean: a wave spectrum and a water material, wired into the Local prefab's PQS renderer.
    /// </summary>
    /// <remarks>
    /// Both start as copies of a stock body's. The material keeps stock's values but not its textures, which live in the
    /// game's bundles where a project asset cannot reference them, so its own are generated beside it. Its shader is
    /// there too, so the material is authored on the SDK's stand-in, which carries stock's properties, and the renderer
    /// draws with a copy on the game's shader.
    /// Running it again on a body that already has an ocean re-wires the prefab and leaves the assets alone.
    /// </remarks>
    public static class OceanSetup
    {
        /// <summary>
        /// The folder, beside the scaled prefab, that holds the ocean's assets.
        /// </summary>
        public const string OCEAN_FOLDER = "Ocean";

        /// <summary>
        /// The stock bodies whose oceans can be copied.
        /// </summary>
        public static readonly string[] STOCK_BODIES = OceanPresets.SPECTRUM_BODIES;

        /// <summary>
        /// Creates or re-wires the body's ocean.
        /// </summary>
        /// <param name="body">The body, either the scaled prefab asset or an instance of it.</param>
        /// <param name="stockBody">The stock body to start from, one of <see cref="STOCK_BODIES" />. Only read when the assets are created.</param>
        /// <param name="message">A status line describing the outcome or the reason for failure.</param>
        /// <returns>True if the ocean was created or re-wired, false otherwise.</returns>
        public static bool TryAddOcean(CoreCelestialBodyData body, string stockBody, out string message)
        {
            if (body == null || body.Data == null)
            {
                message = "Could not resolve a body.";
                return false;
            }

            if (!body.Data.hasOcean)
            {
                message = "Turn on Has Ocean first. Sea level is the body radius, and Ocean Altitude sets how far the terrain base sits below it.";
                return false;
            }

            string scaledPath = ResolvePrefabPath(body);
            string localPath = ResolveLocalPrefabPath(body);
            if (string.IsNullOrEmpty(scaledPath) || string.IsNullOrEmpty(localPath))
            {
                message = "No Local prefab with a PQS was found. Oceans belong to solid bodies.";
                return false;
            }

            string bodyName = string.IsNullOrEmpty(body.Data.bodyName) ? body.name : body.Data.bodyName;
            string folder = $"{Path.GetDirectoryName(scaledPath)?.Replace('\\', '/')}/{OCEAN_FOLDER}";
            string spectrumPath = $"{folder}/{bodyName}_OceanSpectrum.asset";
            string materialPath = $"{folder}/{bodyName}_Ocean.mat";
            var spectrum = AssetDatabase.LoadAssetAtPath<OceanWaveSpectrum>(spectrumPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            bool created = spectrum == null || material == null;
            if (created && !TryCreateFromStock(stockBody, folder, spectrumPath, materialPath, ref spectrum, ref material, out message))
                return false;

            WireLocalPrefab(localPath, spectrum, material);
            string textures = string.Empty;
            if (created)
            {
                textures = OceanTextureBaker.TryBakeAll(material, out string bakeMessage)
                    ? " Generated its caustics, foam and normal maps."
                    : $" Its textures were not generated: {bakeMessage}";
                textures += OceanShoreline.TryBake(body, material, out string shorelineMessage)
                    ? " Generated its shoreline."
                    : $" Its shoreline was not generated: {shorelineMessage}";
            }

            WireScaledMaps(body);

            AssetDatabase.SaveAssets();
            message = created
                ? $"Created {spectrum.name} and {material.name} from {stockBody}'s ocean.{textures}"
                : $"Re-wired {spectrum.name} and {material.name} into the Local prefab.";
            return true;
        }

        /// <summary>
        /// Gets the colour the body's sea is painted into its scaled albedo with.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The ocean's Color From Orbit, or stock Kerbin's when the body has no ocean material yet.</returns>
        public static Color ResolveScaledOceanColor(CoreCelestialBodyData body)
        {
            PQSRenderer renderer = FindRenderer(body);
            Material material = renderer != null ? renderer.OceanWaterMaterial : null;
            OceanMaterialAuthoring sidecar = material != null ? AuthoringSidecars.FindOcean(material) : null;
            return sidecar != null ? sidecar.ScaledOceanColor : OceanPresets.KERBIN_SCALED_OCEAN_COLOR;
        }

        // Each scaled map the ocean fades into with altitude, and the prefix its fade opacities share.
        private static readonly (string Suffix, string Property, string FadePrefix)[] SCALED_MAPS =
        {
            ("_scaled_d", "_ScaleAlbedoTexture", "_ScaleAlbedo"),
            ("_scaled_n", "_ScaleNormalTexture", "_ScaleNormal"),
            ("_scaled_pk", "_ScalePackedTexture", "_ScalePacked"),
        };

        /// <summary>
        /// Gives the body's ocean material the body's own scaled-space maps, which the water fades into with altitude, as
        /// stock's does.
        /// </summary>
        /// <remarks>
        /// The maps are the ones Bake Body Surface writes. A map not baked yet would fade the water toward the shader's
        /// black default, so its fade is held at zero, with the opacities kept in the material's sidecar until the map
        /// exists.
        /// </remarks>
        /// <param name="body">The body.</param>
        public static void WireScaledMaps(CoreCelestialBodyData body)
        {
            PQSRenderer renderer = FindRenderer(body);
            Material material = renderer != null ? renderer.OceanWaterMaterial : null;
            string scaledFolder = BodySurfaceBakerOperation.ResolveScaledFolder(body);
            if (material == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)) || scaledFolder == null)
                return;

            OceanMaterialAuthoring sidecar = AuthoringSidecars.GetOrCreateOcean(material);
            string bodyName = string.IsNullOrEmpty(body.Data?.bodyName) ? body.name : body.Data.bodyName;
            Undo.RecordObject(material, "Wire Ocean Scaled Maps");
            foreach ((string suffix, string property, string fadePrefix) in SCALED_MAPS)
            {
                var map = AssetDatabase.LoadAssetAtPath<Texture2D>($"{scaledFolder}/{bodyName}{suffix}.png");
                WireScaledMap(material, sidecar, map, property, fadePrefix);
            }

            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(sidecar);
        }

        /// <summary>
        /// Assigns one scaled map, holding its fade at zero while it is missing and giving the fade back once it exists.
        /// </summary>
        /// <param name="material">The ocean material.</param>
        /// <param name="sidecar">The material's sidecar, which keeps held fades.</param>
        /// <param name="map">The map, or null when the body has not baked it.</param>
        /// <param name="property">The material's texture property for the map.</param>
        /// <param name="fadePrefix">The prefix the map's fade opacities share.</param>
        public static void WireScaledMap(Material material, OceanMaterialAuthoring sidecar, Texture map, string property, string fadePrefix)
        {
            string start = fadePrefix + "StartOpacity";
            string end = fadePrefix + "EndOpacity";
            material.SetTexture(property, map);
            OceanMaterialAuthoring.HeldFade held = sidecar.HeldScaledFades.Find(fade => fade.Prefix == fadePrefix);
            if (map != null)
            {
                if (held == null)
                    return;

                material.SetFloat(start, held.Start);
                material.SetFloat(end, held.End);
                sidecar.HeldScaledFades.Remove(held);
                return;
            }

            if (held != null)
                return;

            sidecar.HeldScaledFades.Add(new OceanMaterialAuthoring.HeldFade
            {
                Prefix = fadePrefix,
                Start = material.GetFloat(start),
                End = material.GetFloat(end),
            });
            material.SetFloat(start, 0f);
            material.SetFloat(end, 0f);
        }

        /// <summary>
        /// Gets a value indicating whether the body's PQS renderer has an ocean spectrum or material.
        /// </summary>
        /// <param name="body">The body, either the scaled prefab asset or an instance of it.</param>
        /// <returns>True if the renderer carries any ocean asset, false otherwise.</returns>
        public static bool HasOcean(CoreCelestialBodyData body)
        {
            PQSRenderer renderer = FindRenderer(body);
            return renderer != null
                && (renderer._oceanSpectrum != null || renderer.OceanWaterMaterial != null || renderer.OceanLavaMaterial != null);
        }

        /// <summary>
        /// Finds the PQS renderer on the body's Local prefab.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The renderer, or null when the body has none.</returns>
        public static PQSRenderer FindRenderer(CoreCelestialBodyData body)
        {
            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs == null)
                return null;

            return pqs.PQSRenderer != null ? pqs.PQSRenderer : pqs.GetComponentInChildren<PQSRenderer>(true);
        }

        /// <summary>
        /// Lists what <see cref="TryRemoveOcean" /> would remove, for a confirmation prompt.
        /// </summary>
        /// <param name="body">The body, either the scaled prefab asset or an instance of it.</param>
        /// <returns>One line per thing removed, or an empty list when there is no ocean.</returns>
        public static List<string> DescribeRemoval(CoreCelestialBodyData body)
        {
            var lines = new List<string>();
            if (!HasOcean(body))
                return lines;

            lines.Add("the ocean spectrum and materials on the Local prefab's PQS renderer");
            lines.AddRange(OwnedAssetPaths(body));
            return lines;
        }

        /// <summary>
        /// Takes the ocean off a body: the PQS renderer's ocean references, and the spectrum and material in the ocean
        /// folder.
        /// </summary>
        /// <remarks>
        /// Has Ocean is left as it is, since it also drives the terrain base and the physics. Assets go to the OS trash
        /// rather than being deleted outright, and assets kept outside the ocean folder are left where they are.
        /// </remarks>
        /// <param name="body">The body, either the scaled prefab asset or an instance of it.</param>
        /// <param name="message">A status line describing the outcome or the reason for failure.</param>
        /// <returns>True if the ocean was removed, false otherwise.</returns>
        public static bool TryRemoveOcean(CoreCelestialBodyData body, out string message)
        {
            if (!HasOcean(body))
            {
                message = "This body has no ocean to remove.";
                return false;
            }

            List<string> paths = OwnedAssetPaths(body);
            string localPath = ResolveLocalPrefabPath(body);
            if (!string.IsNullOrEmpty(localPath))
            {
                WireLocalPrefab(localPath, null, null);
            }

            var trashed = new List<string>();
            foreach (string path in paths)
            {
                if (AssetDatabase.MoveAssetToTrash(path))
                {
                    trashed.Add(Path.GetFileName(path));
                }
            }

            AssetDatabase.SaveAssets();
            message = trashed.Count > 0
                ? $"Removed the ocean and moved {string.Join(", ", trashed)} to the trash. Has Ocean is still on."
                : "Removed the ocean references. No ocean assets were found in the ocean folder. Has Ocean is still on.";
            return true;
        }

        private static bool TryCreateFromStock(
            string stockBody,
            string folder,
            string spectrumPath,
            string materialPath,
            ref OceanWaveSpectrum spectrum,
            ref Material material,
            out string message
        )
        {
            OceanWaveSpectrum createdSpectrum = spectrum;
            Material createdMaterial = material;
            bool complete = false;
            bool loaded = OceanPresets.TryUse(stockBody, stock =>
            {
                if (stock._oceanSpectrum == null || stock.OceanWaterMaterial == null)
                    return;

                EnsureFolder(folder);
                if (createdSpectrum == null)
                {
                    createdSpectrum = ScriptableObject.CreateInstance<OceanWaveSpectrum>();
                    EditorUtility.CopySerialized(stock._oceanSpectrum, createdSpectrum);
                    createdSpectrum.name = Path.GetFileNameWithoutExtension(spectrumPath);
                    AssetDatabase.CreateAsset(createdSpectrum, spectrumPath);
                }

                if (createdMaterial == null)
                {
                    // The game's shader cannot be referenced from a project asset, so the material is authored on the
                    // SDK's stand-in, which carries the same properties. The renderer draws with a copy on the real one.
                    createdMaterial = new Material(Shader.Find(PQSRenderer.OCEAN_WATER_STAND_IN_SHADER))
                    {
                        name = Path.GetFileNameWithoutExtension(materialPath),
                    };
                    OceanPresets.CopyValues(stock.OceanWaterMaterial, createdMaterial);
                    AssetDatabase.CreateAsset(createdMaterial, materialPath);
                }

                complete = true;
            }, out string problem);

            spectrum = createdSpectrum;
            material = createdMaterial;
            if (!loaded)
            {
                message = problem;
                return false;
            }

            message = complete ? string.Empty : $"{stockBody}'s stock ocean could not be loaded.";
            return complete;
        }

        // The spectrum, both materials, the water material's sidecar and the textures it uses, when they live in the ocean
        // folder beside the scaled prefab, which is where Add Ocean and the texture bake write them.
        private static List<string> OwnedAssetPaths(CoreCelestialBodyData body)
        {
            var paths = new List<string>();
            PQSRenderer renderer = FindRenderer(body);
            string scaledPath = ResolvePrefabPath(body);
            if (renderer == null || string.IsNullOrEmpty(scaledPath))
                return paths;

            string folder = $"{Path.GetDirectoryName(scaledPath)?.Replace('\\', '/')}/{OCEAN_FOLDER}/";
            var owned = new List<Object> { renderer._oceanSpectrum, renderer.OceanWaterMaterial, renderer.OceanLavaMaterial };
            Material water = renderer.OceanWaterMaterial;
            if (water != null)
            {
                owned.Add(AuthoringSidecars.FindOcean(water));
                foreach (string property in water.GetTexturePropertyNames())
                {
                    owned.Add(water.GetTexture(property));
                }
            }

            foreach (Object asset in owned)
            {
                string path = asset != null ? AssetDatabase.GetAssetPath(asset) : null;
                if (!string.IsNullOrEmpty(path) && path.StartsWith(folder) && !paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        private static void WireLocalPrefab(string localPath, OceanWaveSpectrum spectrum, Material material)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(localPath);
            try
            {
                PQS pqs = root.GetComponentInChildren<PQS>(true);
                PQSRenderer renderer = pqs == null ? null
                    : pqs.PQSRenderer != null ? pqs.PQSRenderer : pqs.GetComponentInChildren<PQSRenderer>(true);
                if (renderer == null)
                    return;

                renderer._oceanSpectrum = spectrum;
                renderer.OceanWaterMaterial = material;
                renderer.OceanLavaMaterial = null;
                PrefabUtility.SaveAsPrefabAsset(root, localPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static string ResolvePrefabPath(CoreCelestialBodyData body)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(body))
                return AssetDatabase.GetAssetPath(body);

            return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(body.gameObject);
        }

        private static string ResolveLocalPrefabPath(CoreCelestialBodyData body)
        {
            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs == null)
                return null;

            return PrefabUtility.IsPartOfPrefabAsset(pqs)
                ? AssetDatabase.GetAssetPath(pqs)
                : PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(pqs.gameObject);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
