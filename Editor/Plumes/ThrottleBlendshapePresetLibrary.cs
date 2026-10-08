using System;
using System.Collections.Generic;
using UnityEngine;

namespace KSP.Editor
{
    [Serializable]
    public class ThrottleBlendshapeUserPresetEntry
    {
        public string Name;
        [TextArea] public string SnapshotJson;
    }

    [CreateAssetMenu(
        fileName = "ThrottleBlendshapePresetLibrary",
        menuName = "KSP2 Redux/Throttle Blendshape Preset Library"
    )]
    public class ThrottleBlendshapePresetLibrary : ScriptableObject
    {
        public List<ThrottleBlendshapeUserPresetEntry> Presets = new();
    }
}
