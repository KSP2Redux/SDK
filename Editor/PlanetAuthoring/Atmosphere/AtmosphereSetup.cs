using KSP;
using KSP.Rendering;
using Ksp2UnityTools.Editor.API;
using UnityEditor;
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

        private const string INNER_SHELL_NAME = "AtmosphereInner";
        private const string OUTER_SHELL_NAME = "AtmosphereOuter";

        /// <summary>
        /// Creates or refits the body's atmosphere and wires it into the scaled prefab.
        /// </summary>
        /// <remarks>
        /// The body must already have Has Atmosphere on with a positive Atmosphere Depth, and a
        /// scaled mesh, because the inner shell reuses that mesh. New models start from the
        /// <see cref="AtmosphereModel" /> defaults for everything but geometry.
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
            model.BottomRadius = (float)(body.Data.radius * 0.001);
            model.AtmosphereHeight = (float)(body.Data.atmosphereDepth * 0.001);
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
