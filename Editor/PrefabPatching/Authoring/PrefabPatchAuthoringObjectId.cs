using UnityEngine;

namespace Ksp2UnityTools.PrefabPatchingAuthoring
{
    /// <summary>
    /// Stable and explicit authoring ID for a GameObject added in a prefab patch
    /// variant. The compiler emits the ID into the public Patch Manager
    /// manifest. Consumers target it with the owning patch ID plus this
    /// patch-local ID.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class PrefabPatchAuthoringObjectId : MonoBehaviour
    {
        [SerializeField]
        private string id;

        public string Id
        {
            get => id;
            set => id = value;
        }
    }
}
