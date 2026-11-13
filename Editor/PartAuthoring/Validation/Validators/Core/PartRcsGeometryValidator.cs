using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Checks serialized RCS nozzle references and describes isolated-part axis authority.
    /// </summary>
    public class PartRcsGeometryValidator : IPartValidator
    {
        // Ignore floating point residue and negligible torque arms below one millimetre.
        private const float FORCE_EPSILON = 0.001f;
        private const float TORQUE_EPSILON = 0.001f;

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Part == null || context.Data == null)
                yield break;
            foreach (Component component in context.Prefab.GetComponents<Component>())
            {
                if (component == null || component.GetType().FullName != "KSP.Modules.Module_RCS")
                    continue;
                var serialized = new SerializedObject(component);
                SerializedProperty nozzles = serialized.FindProperty("ThrusterTransforms");
                if (nozzles == null || !nozzles.isArray)
                    continue;
                if (nozzles.arraySize == 0)
                {
                    yield return new ValidationIssue("PART_RCS_NO_NOZZLES", ValidationSeverity.Error,
                        "RCS ThrusterTransforms contains 0 nozzles. Expected at least 1 authored thrust transform.");
                    continue;
                }
                var signs = new List<string>[12];
                for (int sign = 0; sign < signs.Length; sign++)
                {
                    signs[sign] = new List<string>();
                }
                var seen = new HashSet<Transform>();
                int contributingNozzles = 0;
                for (int index = 0; index < nozzles.arraySize; index++)
                {
                    Transform nozzle = nozzles.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                    if (nozzle == null || !nozzle.IsChildOf(context.Prefab.transform))
                    {
                        yield return new ValidationIssue("PART_RCS_NOZZLE_INVALID", ValidationSeverity.Error,
                            $"RCS ThrusterTransforms[{index}] is missing or outside the part. Expected a transform under '{context.Prefab.name}'.");
                        continue;
                    }
                    if (!seen.Add(nozzle))
                    {
                        yield return new ValidationIssue("PART_RCS_NOZZLE_DUPLICATE", ValidationSeverity.Warning,
                            $"RCS ThrusterTransforms[{index}] repeats '{nozzle.name}'. Expected each nozzle once.");
                    }
                    Vector3 position = context.Prefab.transform.InverseTransformPoint(nozzle.position);
                    if (position == Vector3.zero)
                    {
                        yield return new ValidationIssue("PART_RCS_NOZZLE_AT_ORIGIN", ValidationSeverity.Warning,
                            $"RCS ThrusterTransforms[{index}] '{nozzle.name}' is at the part origin. Module_RCS skips this nozzle.");
                        continue;
                    }
                    if (!nozzle.gameObject.activeInHierarchy)
                        continue;
                    contributingNozzles++;
                    // Module_RCS applies force opposite nozzle.up, with torque r cross force.
                    Vector3 force = context.Prefab.transform.InverseTransformDirection(-nozzle.up).normalized;
                    Vector3 lever = position - context.Data.coMassOffset;
                    Vector3 torque = Vector3.Cross(lever, force);
                    string nozzleLabel = $"[{index}] {nozzle.name}";
                    for (int axis = 0; axis < 3; axis++)
                    {
                        if (force[axis] > FORCE_EPSILON)
                            signs[axis * 2].Add(nozzleLabel);
                        if (force[axis] < -FORCE_EPSILON)
                            signs[axis * 2 + 1].Add(nozzleLabel);
                        if (torque[axis] > TORQUE_EPSILON)
                            signs[6 + axis * 2].Add(nozzleLabel);
                        if (torque[axis] < -TORQUE_EPSILON)
                            signs[7 + axis * 2].Add(nozzleLabel);
                    }
                }
                var axes = new List<string>();
                for (int axis = 0; axis < 6; axis++)
                {
                    string label = axis < 3 ? $"force {"XYZ"[axis]}" : $"torque {"XYZ"[axis - 3]}";
                    axes.Add($"{label}: +[{string.Join(", ", signs[axis * 2])}], -[{string.Join(", ", signs[axis * 2 + 1])}]");
                }
                yield return new ValidationIssue("PART_RCS_AXIS_COVERAGE", ValidationSeverity.Info,
                    $"RCS '{component.name}', {contributingNozzles} contributing of {nozzles.arraySize} authored nozzles, part CoM {context.Data.coMassOffset}: {string.Join(", ", axes)}. Expected axes depend on vessel mounting and enabled controls. Force cutoff {FORCE_EPSILON:G3}, torque cutoff {TORQUE_EPSILON:G3} m.");
            }
        }
    }
}
