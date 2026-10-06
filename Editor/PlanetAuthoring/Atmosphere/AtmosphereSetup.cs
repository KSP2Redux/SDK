using System.Collections.Generic;
using KSP;
using KSP.Rendering;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Atmosphere
{
    /// <summary>
    /// Gives a celestial body a Bruneton atmosphere: the model asset, its baked lookup tables, the
    /// addressable entry and the scaled prefab's atmosphere component and shells.
    /// </summary>
    /// <remarks>
    /// Running it again on a body that already has an atmosphere refits the model's geometry to the
    /// body, re-bakes and re-wires without touching the model's optical settings.
    /// </remarks>
    public static class AtmosphereSetup
    {
        /// <summary>
        /// The folder, beside the scaled prefab, that holds the model and its baked tables.
        /// </summary>
        public const string ATMOSPHERE_FOLDER = "Atmosphere";

        /// <summary>
        /// The sun zenith cutoff, in degrees, that new models and applied presets take.
        /// </summary>
        /// <remarks>
        /// 180 has the tables cover every sun angle. Anything lower clamps the sun on a night side seen
        /// against a lit limb, which an atmosphere tall for its body shows as a glow cut off at the
        /// analytic horizon.
        /// </remarks>
        public const float SUN_ZENITH_ANGLE = 180f;

        private const string INNER_SHELL_NAME = "AtmosphereInner";
        private const string OUTER_SHELL_NAME = "AtmosphereOuter";

        /// <summary>
        /// Creates or refits the body's atmosphere and wires it into the scaled prefab.
        /// </summary>
        /// <remarks>
        /// The body must already have Has Atmosphere on with a positive Atmosphere Depth, and a
        /// scaled mesh, because the inner shell reuses that mesh. New models start from the
        /// <see cref="AtmosphereModel" /> defaults for everything but geometry and the sun zenith cutoff.
        /// </remarks>
        /// <param name="body">The body, either the scaled prefab asset or an instance of it.</param>
        /// <param name="model">The created or refitted model, or null on failure.</param>
        /// <param name="message">A status line describing the outcome or the reason for failure.</param>
        /// <returns>True if the atmosphere was created or refitted, false otherwise.</returns>
        public static bool TryAddAtmosphere(CoreCelestialBodyData body, out AtmosphereModel model, out string message)
        {
            model = null;
            if (body == null || body.Data == null)
            {
                message = "Could not resolve a body.";
                return false;
            }

            if (!body.Data.hasAtmosphere || body.Data.atmosphereDepth <= 0.0)
            {
                message = "Turn on Has Atmosphere and set a positive Atmosphere Depth first.";
                return false;
            }

            string prefabPath = ResolvePrefabPath(body);
            if (string.IsNullOrEmpty(prefabPath))
            {
                message = "The body is not part of a prefab, so there is nothing to wire the atmosphere into.";
                return false;
            }

            Mesh bodyMesh = FindScaledMesh(prefabPath);
            if (bodyMesh == null)
            {
                message = "The scaled prefab has no mesh yet. Run Bake Body Surface first, since the inner atmosphere shell reuses it.";
                return false;
            }

            string bodyName = string.IsNullOrEmpty(body.Data.bodyName) ? body.name : body.Data.bodyName;
            string folder = $"{System.IO.Path.GetDirectoryName(prefabPath)?.Replace('\\', '/')}/{ATMOSPHERE_FOLDER}";
            model = LoadOrCreateModel(folder, $"{bodyName}_Atmosphere", out bool created);

            Undo.RecordObject(model, "Fit Atmosphere To Body");
            model.PlanetName = bodyName;
            FitModelToBody(model, body.Data, created);
            EditorUtility.SetDirty(model);

            if (!BrunetonLutBaker.Bake(model, folder, out string error))
            {
                message = $"LUT bake failed: {error}";
                return false;
            }

            bool registered = RegisterAddressable(model);
            WireScaledPrefab(prefabPath, model, bodyMesh);
            AssetDatabase.SaveAssets();

            message = created
                ? $"Created {model.name}. Optical settings start from Earth-like defaults."
                : $"Refitted {model.name} to the body and re-baked.";
            if (!registered)
            {
                message += " No Celestial Bodies addressables group was found, so the model is not addressable yet.";
            }

            return true;
        }

        /// <summary>
        /// Gets a value indicating whether the body's scaled prefab carries an atmosphere component.
        /// </summary>
        /// <param name="body">The body, either the scaled prefab asset or an instance of it.</param>
        /// <returns>True if the body has a visual atmosphere wired in, false otherwise.</returns>
        public static bool HasAtmosphere(CoreCelestialBodyData body) =>
            body != null && body.TryGetComponent(out AtmosphereDataModelComponent _);

        /// <summary>
        /// Lists what <see cref="TryRemoveAtmosphere" /> would remove, for a confirmation prompt.
        /// </summary>
        /// <param name="body">The body, either the scaled prefab asset or an instance of it.</param>
        /// <returns>One line per thing removed, or an empty list when there is no atmosphere.</returns>
        public static List<string> DescribeRemoval(CoreCelestialBodyData body)
        {
            var lines = new List<string>();
            if (!HasAtmosphere(body))
                return lines;

            lines.Add("the atmosphere component and its two shells on the scaled prefab");
            body.TryGetComponent(out AtmosphereDataModelComponent component);
            string modelPath = FindModelPath(component.AtmosphereModelKey);
            if (string.IsNullOrEmpty(modelPath))
                return lines;

            lines.Add($"the addressable entry '{component.AtmosphereModelKey}'");
            lines.Add(modelPath);
            foreach (string texturePath in BakedTablePaths(AssetDatabase.LoadAssetAtPath<AtmosphereModel>(modelPath)))
            {
                lines.Add(texturePath);
            }

            return lines;
        }

        /// <summary>
        /// Takes the visual atmosphere off a body: the scaled prefab's component and shells, the
        /// model's addressable entry, and the model with its baked tables.
        /// </summary>
        /// <remarks>
        /// Assets go to the OS trash rather than being deleted outright. Has Atmosphere, Atmosphere
        /// Depth and the pressure curves are physics and stay as they are.
        /// </remarks>
        /// <param name="body">The body, either the scaled prefab asset or an instance of it.</param>
        /// <param name="message">A status line describing the outcome or the reason for failure.</param>
        /// <returns>True if the atmosphere was removed, false otherwise.</returns>
        public static bool TryRemoveAtmosphere(CoreCelestialBodyData body, out string message)
        {
            if (!HasAtmosphere(body))
            {
                message = "This body has no atmosphere to remove.";
                return false;
            }

            string prefabPath = ResolvePrefabPath(body);
            if (string.IsNullOrEmpty(prefabPath))
            {
                message = "The body is not part of a prefab, so there is nothing to remove the atmosphere from.";
                return false;
            }

            body.TryGetComponent(out AtmosphereDataModelComponent component);
            string key = component.AtmosphereModelKey;
            string modelPath = FindModelPath(key);

            UnwireScaledPrefab(prefabPath);

            var trashed = new List<string>();
            if (!string.IsNullOrEmpty(modelPath))
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings != null)
                {
                    settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(modelPath));
                }

                var assetPaths = new List<string>(BakedTablePaths(AssetDatabase.LoadAssetAtPath<AtmosphereModel>(modelPath)))
                {
                    modelPath,
                };
                foreach (string path in assetPaths)
                {
                    if (AssetDatabase.MoveAssetToTrash(path))
                    {
                        trashed.Add(System.IO.Path.GetFileName(path));
                    }
                }
            }

            AssetDatabase.SaveAssets();
            message = trashed.Count > 0
                ? $"Removed the atmosphere and moved {string.Join(", ", trashed)} to the trash."
                : "Removed the atmosphere component and shells. No model asset was found for its key.";
            return true;
        }

        /// <summary>
        /// Sizes a model to the body it renders for.
        /// </summary>
        /// <remarks>
        /// The model's bottom radius always follows the body. Its visual height is an art choice
        /// that stock sets well inside the physics atmosphere, Duna at 22 km of 50 and Kerbin at 60
        /// of 70, so it is only seeded from Atmosphere Depth when the model is new and a refit leaves
        /// it alone.
        /// </remarks>
        /// <param name="model">The model to fit. Its units are kilometers.</param>
        /// <param name="data">The body's data. Its units are meters.</param>
        /// <param name="created">True if the model was just created, false for a refit of an existing one.</param>
        public static void FitModelToBody(AtmosphereModel model, CelestialBodyData data, bool created)
        {
            model.BottomRadius = (float)(data.radius * 0.001);
            if (created)
            {
                model.AtmosphereHeight = (float)(data.atmosphereDepth * 0.001);
                model.SunZenithAngle = SUN_ZENITH_ANGLE;
            }
        }

        private static string ResolvePrefabPath(CoreCelestialBodyData body)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(body))
                return AssetDatabase.GetAssetPath(body);

            return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(body.gameObject);
        }

        private static Mesh FindScaledMesh(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null || !prefab.TryGetComponent(out MeshFilter filter))
                return null;

            Mesh mesh = filter.sharedMesh;
            return mesh != null && mesh.vertexCount > 0 ? mesh : null;
        }

        private static AtmosphereModel LoadOrCreateModel(string folder, string modelName, out bool created)
        {
            EnsureFolder(folder);
            string path = $"{folder}/{modelName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AtmosphereModel>(path);
            created = existing == null;
            if (!created)
                return existing;

            var model = ScriptableObject.CreateInstance<AtmosphereModel>();
            model.name = modelName;
            AssetDatabase.CreateAsset(model, path);
            return model;
        }

        // The runtime loads the model by its addressable key, so the key is the model's name.
        private static bool RegisterAddressable(AtmosphereModel model)
        {
            var group = PlanetAuthoringAddressables.ResolveCelestialBodiesGroup(model);
            if (group == null)
                return false;

            AddressablesTools.MakeAddressable(group, AssetDatabase.GetAssetPath(model), model.name);
            return true;
        }

        private static void WireScaledPrefab(string prefabPath, AtmosphereModel model, Mesh bodyMesh)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (!root.TryGetComponent(out AtmosphereDataModelComponent component))
                {
                    component = root.AddComponent<AtmosphereDataModelComponent>();
                }

                // The runtime sets the inner shell to unit scale, so it must be the body's own mesh.
                // The outer shell is rescaled to the atmosphere top, so any sphere serves.
                MeshRenderer inner = EnsureShell(root.transform, INNER_SHELL_NAME, bodyMesh);
                MeshRenderer outer = EnsureShell(root.transform, OUTER_SHELL_NAME, BuiltinSphereMesh());

                var serialized = new SerializedObject(component);
                serialized.FindProperty("_atmosphereModelKey").stringValue = model.name;
                serialized.FindProperty("_planetName").stringValue = model.PlanetName;
                serialized.FindProperty("_innerMeshRenderer").objectReferenceValue = inner;
                serialized.FindProperty("_outerMeshRenderer").objectReferenceValue = outer;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void UnwireScaledPrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (root.TryGetComponent(out AtmosphereDataModelComponent component))
                {
                    Object.DestroyImmediate(component);
                }

                foreach (string shellName in new[] { INNER_SHELL_NAME, OUTER_SHELL_NAME })
                {
                    Transform shell = root.transform.Find(shellName);
                    if (shell != null)
                    {
                        Object.DestroyImmediate(shell.gameObject);
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Resolves an atmosphere component's model key to the Bruneton model it names.
        /// </summary>
        /// <remarks>
        /// The runtime loads the model by addressable key. Redux and SDK bodies are authored in the
        /// project, so the key resolves through the project's own Addressables entries.
        /// </remarks>
        /// <param name="key">The model's addressable key.</param>
        /// <param name="problem">Why nothing was found, or an empty string on success.</param>
        /// <returns>The model, or null when the key resolves to nothing or to something else.</returns>
        public static AtmosphereModel FindModel(string key, out string problem)
        {
            problem = string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                problem = "the atmosphere component has no model key";
                return null;
            }

            string path = FindModelPath(key);
            if (string.IsNullOrEmpty(path))
            {
                problem = $"no addressable entry for '{key}'";
                return null;
            }

            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset is AtmosphereModel model)
                return model;

            problem = asset is HillaireAtmosphereProfile
                ? "Hillaire atmospheres have no authoring tools yet"
                : $"'{key}' is not an AtmosphereModel";
            return null;
        }

        /// <summary>
        /// Reports whether a model's baked lookup tables match its current settings.
        /// </summary>
        /// <param name="model">The atmosphere model.</param>
        /// <returns>The state of the model's baked tables.</returns>
        public static AtmosphereTableState GetTableState(AtmosphereModel model)
        {
            bool hasTables = model.TransmittanceTexture != null
                && model.IrradianceTexture != null
                && model.ScatteringTexture != null;
            AtmosphereModelAuthoring sidecar = AuthoringSidecars.Find(model);
            return ClassifyTables(hasTables, sidecar != null ? sidecar.BakedLutHash : null, BrunetonLutBaker.ComputeLutInputHash(model));
        }

        /// <summary>
        /// Classifies baked tables from what is known about them.
        /// </summary>
        /// <remarks>
        /// Tables with no recorded hash were baked by something that did not record one, such as the
        /// old wizard, so they count as stale rather than trusted.
        /// </remarks>
        /// <param name="hasTables">True if all three tables are assigned, false otherwise.</param>
        /// <param name="bakedHash">The table-input hash recorded at the last bake, or null when none was recorded.</param>
        /// <param name="currentHash">The model's current table-input hash.</param>
        /// <returns>The state of the baked tables.</returns>
        public static AtmosphereTableState ClassifyTables(bool hasTables, int? bakedHash, int currentHash)
        {
            if (!hasTables)
                return AtmosphereTableState.Missing;

            return bakedHash == currentHash ? AtmosphereTableState.Current : AtmosphereTableState.Stale;
        }

        private static string FindModelPath(string key)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || string.IsNullOrEmpty(key))
                return null;

            foreach (var group in settings.groups)
            {
                if (group == null)
                    continue;

                foreach (var entry in group.entries)
                {
                    if (entry.address == key)
                        return entry.AssetPath;
                }
            }

            return null;
        }

        private static IEnumerable<string> BakedTablePaths(AtmosphereModel model)
        {
            if (model == null)
                yield break;

            foreach (Texture table in new Texture[] { model.TransmittanceTexture, model.IrradianceTexture, model.ScatteringTexture })
            {
                string path = table != null ? AssetDatabase.GetAssetPath(table) : null;
                if (!string.IsNullOrEmpty(path))
                    yield return path;
            }
        }

        // Shells ship disabled with no material. The runtime assigns both and enables them, and a
        // material-less renderer would otherwise draw magenta over the body in the editor.
        private static MeshRenderer EnsureShell(Transform parent, string shellName, Mesh mesh)
        {
            Transform shell = parent.Find(shellName);
            if (shell == null)
            {
                shell = new GameObject(shellName).transform;
                shell.SetParent(parent, false);
            }

            if (!shell.TryGetComponent(out MeshFilter filter))
            {
                filter = shell.gameObject.AddComponent<MeshFilter>();
            }

            filter.sharedMesh = mesh;

            if (!shell.TryGetComponent(out MeshRenderer renderer))
            {
                renderer = shell.gameObject.AddComponent<MeshRenderer>();
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            shell.gameObject.layer = parent.gameObject.layer;
            return renderer;
        }

        private static Mesh BuiltinSphereMesh()
        {
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(primitive);
            return mesh;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
