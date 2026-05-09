using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Services
{
    public sealed class DefaultGraphicsSettingsProvider : IGraphicsSettingsProvider
    {
        private const string SettingsAssetFilter = "t:" + nameof(PQSGlobalSettings);

        private PQSGlobalSettings _cached;

        public PQSGlobalSettings GetPQSGlobalSettings()
        {
            if (_cached != null)
                return _cached;

            string[] guids = AssetDatabase.FindAssets(SettingsAssetFilter);
            if (guids.Length == 0)
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _cached = AssetDatabase.LoadAssetAtPath<PQSGlobalSettings>(path);
            return _cached;
        }

        public bool TryGetBoolSetting(string name, out bool value)
        {
            value = false;
            return false;
        }
    }
}
