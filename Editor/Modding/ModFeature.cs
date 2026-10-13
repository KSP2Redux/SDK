using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ksp2UnityTools.Editor.Modding
{
    /// <summary>
    /// A feature a mod declares in its swinfo, with its relations to other features.
    /// </summary>
    [Serializable]
    public class ModFeature
    {
        [Tooltip("The feature id, bare to belong to this mod")]
        public string id = "";

        [Tooltip("Whether the mod recommends the feature, which Enable Recommended turns on")]
        public bool recommended;

        [Tooltip("The features this one turns on with it")]
        public List<string> requires = new();

        [Tooltip("The features that cannot be on in the same save as this one")]
        public List<string> conflicts = new();

        [Tooltip("The features this one stands in for")]
        public List<string> provides = new();
    }
}
