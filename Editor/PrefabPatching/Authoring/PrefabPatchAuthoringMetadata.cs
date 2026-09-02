using System.Collections.Generic;
using PatchManager.PrefabPatching;
using UnityEngine;

namespace Ksp2UnityTools.PrefabPatchingAuthoring
{
    /// <summary>
    /// Authoring-only metadata stored on the root of a visual prefab-patch
    /// variant. The editor compiler consumes this component and excludes it
    /// from the generated runtime patch operations.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class PrefabPatchAuthoringMetadata : MonoBehaviour
    {
        public Object OwningMod;
        public string PatchName;
        public PrefabPatchPass Pass = PrefabPatchPass.Default;
        public PrefabPatchOrdering Ordering = PrefabPatchOrdering.Default;
        public List<string> NeedsMods = new();
        public List<string> ConflictsMods = new();
        public List<string> NeedsPatches = new();
        public List<string> ConflictsPatches = new();
        public List<string> BeforePatches = new();
        public List<string> AfterPatches = new();
        public List<string> BeforeMods = new();
        public List<string> AfterMods = new();
        public List<string> ConfigurationInputs = new();
        public bool AutoCompileOnSave = true;
    }
}
