using UnityEditor;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    [InitializeOnLoad]
    public static class LinkedAddressablePlayerBuildOptions
    {
        public const string CopySourceMenuName =
            "Modding/Linked Addressables/Advanced/Include External Addressables in Player Build";

        private const string CopySourcePreferenceKey =
            "KSP2UnityTools.LinkedAddressables.CopyExternalAddressablesToPlayerBuild";

        public static bool CopySourceToPlayer;

        internal static bool UseTranslatedSceneBootstrap;

        static LinkedAddressablePlayerBuildOptions()
        {
            // External game content must be an explicit opt-in.
            CopySourceToPlayer = EditorPrefs.GetBool(
                CopySourcePreferenceKey,
                false
            );
        }

        [MenuItem(CopySourceMenuName, false, 220)]
        private static void ToggleCopySource()
        {
            CopySourceToPlayer = !CopySourceToPlayer;
            EditorPrefs.SetBool(
                CopySourcePreferenceKey,
                CopySourceToPlayer
            );
        }

        [MenuItem(CopySourceMenuName, true)]
        private static bool ValidateCopySource()
        {
            Menu.SetChecked(CopySourceMenuName, CopySourceToPlayer);
            return true;
        }
    }
}
