using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace ksp2community.ksp2unitytools.editor.Editor.Modding
{
    [Serializable]
    public class ModDependency
    {
        [FormerlySerializedAs("Id")] [Tooltip("The id of the dependency")]
        public string id = "";
        [FormerlySerializedAs("Min")] [Tooltip("The minimum version for the dependency")] public string min = "*";
        [FormerlySerializedAs("Max")] [Tooltip("The maximum version for the dependency")] public string max = "*";
    }
}