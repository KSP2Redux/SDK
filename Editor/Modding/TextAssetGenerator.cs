using UnityEngine;

namespace ksp2community.ksp2unitytools.editor.Modding
{
    public abstract class TextAssetGenerator : ScriptableObject
    {
        public abstract string PathInMod { get; }
        public abstract string Generate();
    }
}