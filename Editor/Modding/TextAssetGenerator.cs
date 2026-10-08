using UnityEngine;

namespace Ksp2UnityTools.Editor.Modding
{
    public abstract class TextAssetGenerator : ScriptableObject
    {
        public abstract bool ShouldGenerate { get; }
        public abstract string PathInMod { get; }
        public abstract string Generate();
    }
}