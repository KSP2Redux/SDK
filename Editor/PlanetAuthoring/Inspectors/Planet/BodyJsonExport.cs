using System;
using System.IO;
using System.Reflection;
using KSP;
using KSP.IO;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// Where a body's saved JSON stands against the body as it is now.
    /// </summary>
    public enum BodyJsonState
    {
        /// <summary>The body is not saved as a prefab, so it has nowhere to write its JSON.</summary>
        NoPrefab,

        /// <summary>No JSON has been saved.</summary>
        Missing,

        /// <summary>The saved JSON differs from the body.</summary>
        OutOfDate,

        /// <summary>The saved JSON matches the body.</summary>
        Saved,
    }

    /// <summary>
    /// Writes a body's JSON next to its Scaled prefab and registers it, and tells whether the saved copy is current.
    /// </summary>
    public static class BodyJsonExport
    {
        private const string CELESTIAL_BODIES_LABEL = "celestial_bodies";

        private static bool _ioInitialized;

        /// <summary>
        /// Gets the path the body's JSON is written to.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The asset path, or null when the body is not saved as a prefab.</returns>
        public static string GetPath(CoreCelestialBodyData body)
        {
            string prefabPath = body != null ? PathUtils.GetPrefabOrAssetPath(body, body.gameObject) : null;
            if (string.IsNullOrEmpty(prefabPath))
                return null;

            return $"{Path.GetDirectoryName(prefabPath)?.Replace('\\', '/')}/{BodyName(body)}.json";
        }

        /// <summary>
        /// Compares the body with its saved JSON.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The JSON's state.</returns>
        public static BodyJsonState GetState(CoreCelestialBodyData body)
        {
            string path = GetPath(body);
            if (path == null)
                return BodyJsonState.NoPrefab;
            if (!File.Exists(path))
                return BodyJsonState.Missing;

            return Serialize(body) == File.ReadAllText(path) ? BodyJsonState.Saved : BodyJsonState.OutOfDate;
        }

        /// <summary>
        /// Writes the body's JSON and makes it addressable with the celestial_bodies label.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The message to show.</returns>
        public static string Save(CoreCelestialBodyData body)
        {
            string path = GetPath(body);
            if (path == null || body.Core == null)
                return "Save the body as a prefab before writing its JSON.";

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Assets");
            File.WriteAllText(path, Serialize(body));
            AssetDatabase.ImportAsset(path);

            AddressableAssetGroup group = PlanetAuthoringAddressables.ResolveCelestialBodiesGroup(body);
            if (group == null)
                return $"Saved {path}. No Celestial Bodies addressables group was found, so make it addressable by hand.";

            AddressablesTools.MakeAddressable(group, path, $"{BodyName(body)}.json", CELESTIAL_BODIES_LABEL);
            AssetDatabase.SaveAssets();
            return $"Saved {path}.";
        }

        private static string Serialize(CoreCelestialBodyData body)
        {
            EnsureIoInitialized();
            string json = IOProvider.ToJson(body.Core, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            return JObject.Parse(json).ToString(Formatting.Indented);
        }

        private static string BodyName(CoreCelestialBodyData body)
        {
            string bodyName = body.Core?.data?.bodyName;
            return string.IsNullOrEmpty(bodyName) ? body.gameObject.name : bodyName;
        }

        // IOProvider registers its converters in an internal Init the editor never runs.
        private static void EnsureIoInitialized()
        {
            if (_ioInitialized)
                return;

            typeof(IOProvider).GetMethod("Init", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(null, Array.Empty<object>());
            _ioInitialized = true;
        }
    }
}
