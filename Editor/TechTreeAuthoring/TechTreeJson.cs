using KSP.Game.Science;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Reads and writes tech node JSON the way the game reads it.
    /// </summary>
    /// <remarks>
    /// The game loads nodes with <see cref="JsonUtility" />, so the SDK writes them with it too. A field it cannot
    /// read, such as a nullable report type, is then left out on both sides alike.
    /// </remarks>
    public static class TechTreeJson
    {
        /// <summary>
        /// Writes a node as indented JSON.
        /// </summary>
        /// <param name="node">The node to write.</param>
        /// <returns>The node's JSON.</returns>
        public static string ToJson(TechNodeData node) => JsonUtility.ToJson(node, true);

        /// <summary>
        /// Reads a node from JSON.
        /// </summary>
        /// <param name="json">The JSON to read.</param>
        /// <param name="node">The node read, or null when the JSON is not a tech node.</param>
        /// <returns>True if the JSON holds a tech node with an ID, false otherwise.</returns>
        public static bool TryFromJson(string json, out TechNodeData node)
        {
            try
            {
                node = JsonUtility.FromJson<TechNodeData>(json);
            }
            catch (System.ArgumentException)
            {
                node = null;
                return false;
            }

            if (node != null && !string.IsNullOrEmpty(node.ID))
                return true;

            node = null;
            return false;
        }
    }
}
