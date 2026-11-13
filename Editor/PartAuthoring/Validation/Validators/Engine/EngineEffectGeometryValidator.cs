using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Engine
{
    /// <summary>
    /// Compares authored visual nozzle exits with thrust axes and identifies procedural shroud limits.
    /// </summary>
    public class EngineEffectGeometryValidator : IPartValidator
    {
        // Nozzle exit planes can be shifted down the bell. Only lateral displacement is compared.
        private const float MIN_LATERAL_OFFSET = 0.05f;
        private const float RELATIVE_LATERAL_OFFSET = 0.05f;
        // Authored visual nozzles may be canted a little relative to physical thrust.
        private const float MAX_ANGLE = 15f;

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Part == null)
                yield break;
            var thrusts = new HashSet<Transform>();
            var names = new HashSet<string>();
            bool hasEngine = false;
            foreach (var module in context.ModuleDatas)
            {
                if (module is not Data_Engine engine || engine.engineModes == null)
                    continue;
                hasEngine = true;
                foreach (Data_Engine.EngineMode mode in engine.engineModes)
                {
                    if (mode == null)
                        continue;
                    if (mode.ThrustTransformNamesMultipliers != null && mode.ThrustTransformNamesMultipliers.Length > 0)
                    {
                        foreach (ThrustTransformGroup group in mode.ThrustTransformNamesMultipliers)
                        {
                            if (group != null && !string.IsNullOrEmpty(group.ThrustTransformName))
                                names.Add(group.ThrustTransformName);
                        }
                    }
                    else if (!string.IsNullOrEmpty(mode.thrustVectorTransformName))
                    {
                        names.Add(mode.thrustVectorTransformName);
                    }
                }
            }
            if (!hasEngine)
                yield break;

            // Resolve all modes in one walk, including repeated names for clustered nozzles.
            foreach (Transform transform in context.Prefab.GetComponentsInChildren<Transform>(true))
            {
                if (names.Contains(transform.name))
                    thrusts.Add(transform);
            }

            Transform root = context.Prefab.transform;
            float tolerance = MIN_LATERAL_OFFSET;
            if (PartGeometry.TryBounds(context, out Bounds bounds))
                tolerance = Mathf.Max(tolerance, bounds.size.magnitude * RELATIVE_LATERAL_OFFSET);
            foreach (Component component in context.Prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().FullName != "Redux.VFX.Plume.Volumetric.VolumetricPlumeSource")
                    continue;
                var serialized = new SerializedObject(component);
                SerializedProperty exits = serialized.FindProperty("_visualNozzles");
                // Empty visual overrides use the engine thrust transforms directly.
                if (exits == null || !exits.isArray || exits.arraySize == 0 || thrusts.Count == 0)
                    continue;
                for (int index = 0; index < exits.arraySize; index++)
                {
                    Transform exit = exits.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                    if (exit == null || !exit.IsChildOf(root))
                    {
                        yield return new ValidationIssue("ENGINE_PLUME_EXIT_INVALID", ValidationSeverity.Warning,
                            $"Plume source '{component.name}' visual nozzle {index} is missing or outside the part. Expected a part-local exit transform or an empty override list.");
                        continue;
                    }
                    Transform nearest = null;
                    float nearestDistance = float.PositiveInfinity;
                    foreach (Transform thrust in thrusts)
                    {
                        float distance = (root.InverseTransformPoint(exit.position) - root.InverseTransformPoint(thrust.position)).sqrMagnitude;
                        if (distance >= nearestDistance)
                            continue;
                        nearest = thrust;
                        nearestDistance = distance;
                    }
                    if (nearest == null)
                        continue;
                    Vector3 offset = root.InverseTransformPoint(exit.position) - root.InverseTransformPoint(nearest.position);
                    Vector3 axis = root.InverseTransformDirection(nearest.forward).normalized;
                    float lateral = Vector3.Cross(offset, axis).magnitude;
                    float angle = Vector3.Angle(root.InverseTransformDirection(exit.forward), axis);
                    if (lateral > tolerance || angle > MAX_ANGLE)
                    {
                        yield return new ValidationIssue("ENGINE_PLUME_EXIT_ALIGNMENT", ValidationSeverity.Warning,
                            $"Plume source '{component.name}' exit '{exit.name}' against thrust '{nearest.name}': lateral offset {lateral:F3} m, angle {angle:F1} degrees. Expected at most {tolerance:F3} m and {MAX_ANGLE:F1} degrees. Axial nozzle offsets are allowed.");
                    }
                }
            }
            foreach (var module in context.ModuleDatas)
            {
                if (module is not Data_Fairing fairing || !fairing.IsShroud)
                    continue;
                yield return new ValidationIssue("ENGINE_SHROUD_PROCEDURAL", ValidationSeverity.Info,
                    $"Engine '{context.Data?.partName}' shroud is generated from Data_Fairing. Expected final shell bounds against engine bounds after construction. Prefab bounds cannot verify the generated shell.");
            }
        }
    }
}
