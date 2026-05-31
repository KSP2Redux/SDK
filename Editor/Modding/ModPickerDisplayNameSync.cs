using UnityEditor;

namespace Ksp2UnityTools.Editor.Modding
{
    [InitializeOnLoad]
    internal static class ModPickerDisplayNameSync
    {
        static ModPickerDisplayNameSync()
        {
            EditorApplication.delayCall += SyncAllLoadedMods;
        }

        private static void SyncAllLoadedMods()
        {
            if (!CanTouchAssets())
            {
                return;
            }

            bool changed = false;
            string[] guids = AssetDatabase.FindAssets("t:Mod");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Mod mod = AssetDatabase.LoadAssetAtPath<Mod>(path);
                if (mod != null)
                {
                    changed |= mod.SyncPickerDisplayName();
                }
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
            }
        }

        internal static bool CanTouchAssets()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode &&
                   !EditorApplication.isCompiling &&
                   !EditorApplication.isUpdating;
        }
    }
}
