using System.Collections.Generic;
using KSP.OAB;
using KSP.Sim;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Checks authored node coordinates against the part's mesh envelope and size registry.
    /// </summary>
    public class PartNodeGeometryValidator : IPartValidator
    {
        // Two centimetres absorbs export rounding. Five percent allows small lips and recessed joints.
        private const float MIN_GAP = 0.02f;
        private const float RELATIVE_GAP = 0.05f;
        // Interior mounting planes can be recessed a few centimetres from their exterior counterpart.
        private const float INTERIOR_PAIR_DISTANCE = 0.05f;
        // A clearly inward node is over 120 degrees from the bounds-centre radial direction.
        private const float INWARD_DOT = -0.5f;
        // Only nearly axis-aligned nodes can be compared to an AABB end face.
        private const float AXIS_DOT = 0.999f;

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Part == null || context.Data?.attachNodes == null)
                yield break;

            bool hasBounds = PartGeometry.TryBounds(context, out Bounds bounds);
            string partSizeKey = PartSizeRegistry.GetPartSizeKey(context.Data);
            foreach (var node in context.Data.attachNodes)
            {
                Vector3 position = (Vector3)node.position;
                Vector3 direction = (Vector3)node.orientation;
                if (!PartGeometry.IsFinite(position) || !PartGeometry.IsFinite(direction) || direction.sqrMagnitude == 0f)
                {
                    yield return new ValidationIssue("PART_NODE_INVALID_VECTOR", ValidationSeverity.Error,
                        $"Node '{node.nodeID}' position {position} and direction {direction}: expected finite coordinates and a nonzero direction.");
                    continue;
                }
                if (node.nodeType != AttachNodeType.Stack)
                    continue;

                // Adapters legitimately have nodes smaller than the part tag. Only flag nodes larger than it.
                // An unresolved tag has no physical diameter to compare against.
                float partDiameter = partSizeKey == null ? 0f : PartSizeRegistry.GetPartDiameter(context.Data);
                float sizeTolerance = Mathf.Max(MIN_GAP, partDiameter * RELATIVE_GAP);
                float nodeDiameter = PartSizeRegistry.GetAttachNodeDiameter(node);
                if (partDiameter > 0f && nodeDiameter > partDiameter + sizeTolerance)
                {
                    yield return new ValidationIssue("PART_NODE_SIZE_EXCEEDS_TAG", ValidationSeverity.Warning,
                        $"Node '{node.nodeID}' size '{PartSizeRegistry.GetAttachNodeSizeKey(node)}' diameter {nodeDiameter:F3} m exceeds part tag '{partSizeKey}' diameter {partDiameter:F3} m. Expected at most {partDiameter + sizeTolerance:F3} m.");
                }
                if (!hasBounds)
                    continue;
                direction.Normalize();
                float tolerance = Mathf.Max(MIN_GAP, bounds.size.magnitude * RELATIVE_GAP);
                float outside = Vector3.Distance(position, bounds.ClosestPoint(position));
                if (outside > tolerance)
                {
                    yield return new ValidationIssue("PART_NODE_OUTSIDE_BOUNDS", ValidationSeverity.Warning,
                        $"Node '{node.nodeID}' at {position} is {outside:F3} m outside mesh bounds {bounds}. Expected at most {tolerance:F3} m. Check intentional stand-off joints.");
                }
                Vector3 radial = position - bounds.center;
                // Coincident opposite stack nodes expose both sides of an interior mounting plane.
                bool isInteriorPair = false;
                foreach (var other in context.Data.attachNodes)
                {
                    if (other.nodeType != AttachNodeType.Stack)
                        continue;
                    Vector3 otherPosition = (Vector3)other.position;
                    Vector3 otherDirection = (Vector3)other.orientation;
                    if (PartGeometry.IsFinite(otherPosition) && PartGeometry.IsFinite(otherDirection) &&
                        Vector3.Distance(position, otherPosition) <= INTERIOR_PAIR_DISTANCE &&
                        Vector3.Dot(otherDirection.normalized, direction) < -AXIS_DOT)
                    {
                        isInteriorPair = true;
                        break;
                    }
                }
                if (isInteriorPair)
                    continue;
                float outwardDot = Vector3.Dot(direction, radial.normalized);
                if (radial.magnitude > tolerance && outwardDot < INWARD_DOT)
                {
                    yield return new ValidationIssue("PART_NODE_POINTS_INWARD", ValidationSeverity.Warning,
                        $"Node '{node.nodeID}' direction {direction} has outward dot {outwardDot:F3} from mesh bounds centre {bounds.center}. Expected at least {INWARD_DOT:F3}.");
                }
                // Interior and off-centre nodes are often interstage or adapter connections.
                // Restrict flush checks to a centred, opposing pair of equal-size stack nodes.
                bool hasOpposite = false;
                foreach (var other in context.Data.attachNodes)
                {
                    if (other.nodeType == AttachNodeType.Stack && other.nodeID != node.nodeID &&
                        PartSizeRegistry.GetAttachNodeSizeKey(other) == PartSizeRegistry.GetAttachNodeSizeKey(node) &&
                        PartGeometry.IsFinite((Vector3)other.position) &&
                        Vector3.Dot(radial, (Vector3)other.position - bounds.center) < 0f &&
                        Vector3.Dot(((Vector3)other.orientation).normalized, direction) < -AXIS_DOT)
                    {
                        hasOpposite = true;
                        break;
                    }
                }
                if (!hasOpposite || Vector3.Cross(radial, direction).magnitude > tolerance)
                    continue;

                int axis = Mathf.Abs(direction.x) > AXIS_DOT ? 0 : Mathf.Abs(direction.y) > AXIS_DOT ? 1 : -1;
                if (Mathf.Abs(direction.z) > AXIS_DOT)
                    axis = 2;
                if (axis < 0)
                    continue;
                float expected = direction[axis] > 0f ? bounds.max[axis] : bounds.min[axis];
                float gap = Mathf.Abs(position[axis] - expected);
                if (outside <= tolerance && gap > tolerance)
                {
                    yield return new ValidationIssue("PART_NODE_NOT_FLUSH", ValidationSeverity.Warning,
                        $"Node '{node.nodeID}' axis {axis} coordinate {position[axis]:F3} m differs from mesh extent {expected:F3} m by {gap:F3} m. Expected within {tolerance:F3} m.");
                }
            }
        }
    }
}
