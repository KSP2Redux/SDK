using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Checks the serialized climb fixtures used for EVA and their outward collider clearance.
    /// </summary>
    public class PartCrewExitGeometryValidator : IPartValidator
    {
        // EVAUtils offsets the spawn against fixture.forward by 0.2 metres.
        private const float SPAWN_OFFSET = 0.2f;
        private const float LADDER_OFFSET = 0.1f;
        // One centimetre avoids reporting contact at the spawn point as an obstruction.
        private const float RAY_START = 0.01f;
        // This checks the first half metre of departure, not the full kerbal volume.
        private const float CLEARANCE = 0.5f;
        // Relative determinant cutoff ignores degenerate or nearly parallel triangles.
        private const float TRIANGLE_EPSILON = 0.0000001f;

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Part == null || context.Data == null || context.Data.crewCapacity <= 0)
                yield break;
            bool hasFixture = false;
            bool hasUnknownPreset = false;
            foreach (Component component in context.Prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().FullName != "KSP.Game.ClimbBehavior")
                    continue;
                var serialized = new SerializedObject(component);
                SerializedProperty instances = serialized.FindProperty("_instances");
                if (instances == null || !instances.isArray)
                    break;
                // EVAUtils uses only the first behavior, preferring its first ladder to its first airlock.
                int selectedIndex = SelectFixture(instances, out hasUnknownPreset);
                for (int index = 0; index < instances.arraySize; index++)
                {
                    if (index != selectedIndex)
                        continue;
                    SerializedProperty instance = instances.GetArrayElementAtIndex(index);
                    string guid = instance.FindPropertyRelative("ClimbPreset.m_AssetGUID")?.stringValue;
                    string path = string.IsNullOrEmpty(guid) ? "" : AssetDatabase.GUIDToAssetPath(guid);
                    Object preset = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (preset == null)
                    {
                        hasUnknownPreset = true;
                        continue;
                    }
                    var presetData = new SerializedObject(preset);
                    SerializedProperty type = presetData.FindProperty("Type");
                    // The game accepts Ladder (0) or Airlock (1), rather than a named hatch marker.
                    if (type == null || type.intValue < 0 || type.intValue > 1)
                        continue;
                    Collider trigger = instance.FindPropertyRelative("TriggerCollider")?.objectReferenceValue as Collider;
                    if (trigger == null || !trigger.transform.IsChildOf(context.Prefab.transform))
                    {
                        yield return new ValidationIssue("PART_CREW_EXIT_TRIGGER_MISSING", ValidationSeverity.Warning,
                            $"Climb fixture '{component.name}' instance {index} has no part-local trigger collider. Expected a box or capsule trigger for EVA.");
                        continue;
                    }
                    hasFixture = true;
                    Vector3 centre;
                    float height;
                    if (trigger is BoxCollider box)
                    {
                        centre = box.center;
                        height = box.size.y;
                    }
                    else if (trigger is CapsuleCollider capsule)
                    {
                        centre = capsule.center;
                        height = capsule.height;
                    }
                    else
                    {
                        yield return new ValidationIssue("PART_CREW_EXIT_TRIGGER_TYPE", ValidationSeverity.Warning,
                            $"Climb fixture '{trigger.name}' uses {trigger.GetType().Name}. Expected BoxCollider or CapsuleCollider as required by EVAUtils.");
                        continue;
                    }
                    Transform fixture = trigger.transform;
                    Vector3 outward = -fixture.forward;
                    // Match EVAUtils.GetColliderTopPos, including its scale treatment.
                    Vector3 spawn = fixture.position + fixture.TransformDirection(centre +
                        0.5f * height * fixture.lossyScale.y * Vector3.up) + outward * SPAWN_OFFSET;
                    if (type.intValue == 0)
                        spawn += fixture.up * LADDER_OFFSET;
                    foreach (Collider collider in context.Colliders)
                    {
                        if (collider == null || collider.isTrigger || !collider.enabled ||
                            !collider.gameObject.activeInHierarchy)
                            continue;
                        Vector3 start = collider.transform.InverseTransformPoint(spawn + outward * RAY_START);
                        Vector3 end = collider.transform.InverseTransformPoint(spawn + outward * CLEARANCE);
                        Vector3 delta = end - start;
                        bool isBlocked = false;
                        if (collider is BoxCollider obstacle)
                        {
                            var bounds = new Bounds(obstacle.center, obstacle.size);
                            isBlocked = bounds.Contains(start) || (bounds.IntersectRay(new Ray(start, delta.normalized), out float distance) && distance <= delta.magnitude);
                        }
                        else if (collider is MeshCollider mesh && mesh.sharedMesh != null)
                        {
                            // Convex cooking fills concavities. The mesh AABB conservatively encloses that hull.
                            Bounds meshBounds = mesh.sharedMesh.bounds;
                            isBlocked = mesh.convex
                                ? meshBounds.Contains(start) || (meshBounds.IntersectRay(new Ray(start, delta.normalized), out float distance) && distance <= delta.magnitude)
                                : IntersectsMesh(mesh.sharedMesh, start, delta);
                        }
                        if (isBlocked)
                        {
                            yield return new ValidationIssue("PART_CREW_EXIT_BLOCKED", ValidationSeverity.Warning,
                                $"Exit '{fixture.name}' spawn at part-local {context.Prefab.transform.InverseTransformPoint(spawn)} has collider obstruction '{collider.name}' within {CLEARANCE:F2} m along outward {context.Prefab.transform.InverseTransformDirection(outward)}. Expected a clear ray after {RAY_START:F2} m. Convex mesh obstacles use conservative mesh bounds rather than the cooked hull. Verify EVA and boarding.");
                        }
                    }
                }
                break;
            }
            if (!hasFixture)
            {
                yield return new ValidationIssue("PART_CREW_EXIT_NOT_VERIFIED", ValidationSeverity.Warning,
                    $"Part '{context.Data.partName}' crewCapacity {context.Data.crewCapacity} has 0 resolved ladder or airlock triggers. Expected at least 1. Unresolved external climb preset present: {hasUnknownPreset}. Check external seats and runtime additions.");
            }
        }

        private static int SelectFixture(SerializedProperty instances, out bool hasUnknownPreset)
        {
            hasUnknownPreset = false;
            int firstAirlock = -1;
            for (int index = 0; index < instances.arraySize; index++)
            {
                SerializedProperty instance = instances.GetArrayElementAtIndex(index);
                string guid = instance.FindPropertyRelative("ClimbPreset.m_AssetGUID")?.stringValue;
                string path = string.IsNullOrEmpty(guid) ? "" : AssetDatabase.GUIDToAssetPath(guid);
                Object preset = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (preset == null)
                {
                    hasUnknownPreset = true;
                    continue;
                }
                var data = new SerializedObject(preset);
                SerializedProperty type = data.FindProperty("Type");
                if (type == null)
                    continue;
                // includeDisabled is true in EVAUtils, so instance enabled state does not filter selection.
                if (type.intValue == 0)
                    return index;
                if (type.intValue == 1 && firstAirlock < 0)
                    firstAirlock = index;
            }
            return firstAirlock;
        }

        private static bool IntersectsMesh(Mesh mesh, Vector3 start, Vector3 delta)
        {
            // Editor snapshots also support meshes imported without CPU read access.
            using var snapshot = MeshUtility.AcquireReadOnlyMeshData(mesh);
            Mesh.MeshData data = snapshot[0];
            using var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp);
            data.GetVertices(vertices);
            for (int submesh = 0; submesh < data.subMeshCount; submesh++)
            {
                var descriptor = data.GetSubMesh(submesh);
                if (descriptor.topology != MeshTopology.Triangles)
                    continue;
                using var indices = new NativeArray<int>(descriptor.indexCount, Allocator.Temp);
                data.GetIndices(indices, submesh, true);
                for (int index = 0; index + 2 < indices.Length; index += 3)
                {
                    Vector3 a = vertices[indices[index]];
                    Vector3 edge1 = vertices[indices[index + 1]] - a;
                    Vector3 edge2 = vertices[indices[index + 2]] - a;
                    Vector3 cross = Vector3.Cross(delta, edge2);
                    float determinant = Vector3.Dot(edge1, cross);
                    if (Mathf.Abs(determinant) <= TRIANGLE_EPSILON * edge1.magnitude * edge2.magnitude * delta.magnitude)
                        continue;
                    Vector3 offset = start - a;
                    float u = Vector3.Dot(offset, cross) / determinant;
                    if (u < 0f || u > 1f)
                        continue;
                    Vector3 q = Vector3.Cross(offset, edge1);
                    float v = Vector3.Dot(delta, q) / determinant;
                    if (v < 0f || u + v > 1f)
                        continue;
                    float fraction = Vector3.Dot(edge2, q) / determinant;
                    if (fraction >= 0f && fraction <= 1f)
                        return true;
                }
            }
            return false;
        }
    }
}
