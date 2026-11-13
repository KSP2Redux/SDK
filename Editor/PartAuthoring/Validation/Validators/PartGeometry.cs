using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators
{
    // Shared coordinate conversion for node, shroud and plume validation.
    internal static class PartGeometry
    {
        // Contexts are rebuilt each validation tick. Weak keys keep the cache within that lifetime.
        private static readonly ConditionalWeakTable<PartValidationContext, Lazy<(bool HasBounds, Bounds Bounds)>> _bounds = new();

        internal static bool TryBounds(PartValidationContext context, out Bounds bounds)
        {
            var result = _bounds.GetValue(context, key => new Lazy<(bool, Bounds)>(() =>
            {
                bool hasBounds = TryBounds(key.Prefab.transform, key.Prefab.transform, out Bounds value);
                return (hasBounds, value);
            })).Value;
            bounds = result.Bounds;
            return result.HasBounds;
        }

        internal static bool IsFinite(Vector3 vector) =>
            !float.IsNaN(vector.x) && !float.IsInfinity(vector.x) &&
            !float.IsNaN(vector.y) && !float.IsInfinity(vector.y) &&
            !float.IsNaN(vector.z) && !float.IsInfinity(vector.z);

        internal static bool TryBounds(Transform root, Transform space, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            var effects = new Dictionary<Transform, bool>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                // Effects do not describe the solid part envelope.
                if (!renderer.gameObject.activeInHierarchy ||
                    (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer))
                    continue;
                if (IsEffect(renderer.transform, root, effects))
                    continue;
                Bounds local = renderer.localBounds;
                Transform boundsSpace = renderer is SkinnedMeshRenderer skinned && skinned.rootBone != null
                    ? skinned.rootBone : renderer.transform;
                Matrix4x4 matrix = space.worldToLocalMatrix * boundsSpace.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var offset = new Vector3((corner & 1) == 0 ? -local.extents.x : local.extents.x,
                        (corner & 2) == 0 ? -local.extents.y : local.extents.y,
                        (corner & 4) == 0 ? -local.extents.z : local.extents.z);
                    Vector3 point = matrix.MultiplyPoint3x4(local.center + offset);
                    if (!IsFinite(point))
                        continue;
                    if (!hasBounds)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }
            return hasBounds && bounds.size.sqrMagnitude > 0f;
        }

        private static bool IsEffect(Transform transform, Transform root, Dictionary<Transform, bool> effects)
        {
            if (effects.TryGetValue(transform, out bool isEffect))
                return isEffect;
            isEffect = transform != root && transform.parent != null && IsEffect(transform.parent, root, effects);
            if (!isEffect)
            {
                foreach (Component component in transform.GetComponents<Component>())
                {
                    if (component != null && (component.GetType().FullName == "KSP.VFX.ThrottleBlendshapeData" ||
                        component.GetType().FullName == "KSP.VFX.ThrottleParticleSystemData"))
                    {
                        isEffect = true;
                        break;
                    }
                }
            }
            effects[transform] = isEffect;
            return isEffect;
        }
    }
}
