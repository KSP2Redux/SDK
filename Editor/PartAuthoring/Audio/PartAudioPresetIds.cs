using System;
using System.Collections.Generic;
using System.Linq;
using Redux.Audio;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring.Audio
{
    /// <summary>
    /// Supplies the set of part audio preset IDs available at authoring time.
    /// </summary>
    /// <remarks>
    /// This duplicates <c>PartAudioPresetRegistry.GetAuthoringPresetIds</c> rather than calling
    /// it. That method sits behind <c>#if UNITY_EDITOR</c> in the game code because it uses
    /// <see cref="AssetDatabase" />, so it is compiled out of the shipped Assembly-CSharp this
    /// package builds against. Calling it works inside the Redux repo and fails everywhere else.
    /// The scan belongs on this side of the boundary anyway, since this package is editor only
    /// and has <see cref="AssetDatabase" /> available.
    /// </remarks>
    public static class PartAudioPresetIds
    {
        /// <summary>
        /// Collects every preset ID declared by a <see cref="PartAudioPresetDefinition" /> asset in
        /// the project, falling back to the built-in definitions when the project declares none.
        /// </summary>
        /// <returns>The distinct preset IDs, ordered ordinally.</returns>
        public static IReadOnlyList<string> GetAuthoringPresetIds()
        {
            var presetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(PartAudioPresetDefinition)}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<PartAudioPresetDefinition>(path);
                if (!string.IsNullOrWhiteSpace(definition?.PresetId))
                {
                    presetIds.Add(definition.PresetId);
                }
            }

            // CreateBuiltinDefinitionsForAuthoring is outside the editor-only guard, so unlike
            // GetAuthoringPresetIds it does ship and can be called from here.
            if (presetIds.Count == 0)
            {
                foreach (var definition in PartAudioPresetRegistry.CreateBuiltinDefinitionsForAuthoring())
                {
                    if (!string.IsNullOrWhiteSpace(definition?.PresetId))
                    {
                        presetIds.Add(definition.PresetId);
                    }
                }
            }

            return presetIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
