using Ksp2UnityTools.Editor.API;
using UnityEditor;

namespace Ksp2UnityTools.Editor.CampaignPacks
{
    /// <summary>
    /// Unity asset menu entries for creating campaign pack authoring assets.
    /// </summary>
    public static class CampaignPackAuthoringMenu
    {
        [MenuItem("Assets/Redux SDK/New Campaign Pack", priority = KSP2UnityTools.MenuPriority)]
        private static void NewCampaignPack()
        {
            KSP2UnityTools.CreateKsp2UnityToolsAssetAtSelectedPath<CampaignPack>("NewCampaignPack");
        }

        [MenuItem("Assets/Redux SDK/New Tech Tree Set", priority = KSP2UnityTools.MenuPriority)]
        private static void NewTechTreeSet()
        {
            KSP2UnityTools.CreateKsp2UnityToolsAssetAtSelectedPath<TechTreeSet>("NewTechTreeSet");
        }

        [MenuItem("Assets/Redux SDK/New Mission Set", priority = KSP2UnityTools.MenuPriority)]
        private static void NewMissionSet()
        {
            KSP2UnityTools.CreateKsp2UnityToolsAssetAtSelectedPath<MissionSet>("NewMissionSet");
        }

        [MenuItem("Assets/Redux SDK/New Science Set", priority = KSP2UnityTools.MenuPriority)]
        private static void NewScienceSet()
        {
            KSP2UnityTools.CreateKsp2UnityToolsAssetAtSelectedPath<ScienceSet>("NewScienceSet");
        }

        [MenuItem("Assets/Redux SDK/New Campaign Pack Extension", priority = KSP2UnityTools.MenuPriority)]
        private static void NewCampaignPackExtension()
        {
            KSP2UnityTools.CreateKsp2UnityToolsAssetAtSelectedPath<CampaignPackExtension>("NewCampaignPackExtension");
        }
    }
}
