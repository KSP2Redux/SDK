using Ksp2UnityTools.Editor.API;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Menu entries for the tech tree authoring workflow. Trees and patches start from the
    /// <see cref="TechTreeWizard" />, which sits above these in the same folder.
    /// </summary>
    public static class TechTreeAuthoringMenu
    {
        [MenuItem("Assets/Redux SDK/Tech Tree/New Tech Node", priority = KSP2UnityTools.MenuPriority + 1)]
        private static void NewTechNode()
        {
            var node = ScriptableObject.CreateInstance<TechNodeAsset>();
            TechTreeEditing.AddNode(node, Vector2.zero);
            ProjectWindowUtil.CreateAsset(node, "NewTechNode.asset");
        }
    }
}
