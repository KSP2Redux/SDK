using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Decal
{
    /// <summary>
    /// Warns when the body's PQSDecalController has decals or shared textures whose state isn't
    /// reflected in the baked <c>PQSDecalData</c>.
    /// </summary>
    /// <remarks>
    /// Two checks run together:
    /// <list type="bullet">
    /// <item>Any instance's template not in <c>PQSDecalData.BakedPqsDecalIDList</c>. Catches
    ///   never-baked, recently-added, or removed-and-readded templates.</item>
    /// <item>The current bake-input hash differs from the last-bake hash recorded on
    ///   <see cref="PQSDecalControllerAuthoring" />. Catches edits to shared textures, template
    ///   value fields, and per-decal source textures since the last bake.</item>
    /// </list>
    /// </remarks>
    public sealed class UnbakedDecalsValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "DECALS_UNBAKED";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;

            var controller = body.GetComponentInChildren<PQSDecalController>(includeInactive: true);
            if (controller == null || controller.PqsDecalData == null)
                yield break;

            int liveInstanceCount = CountLiveInstances(controller);
            int bakedCount = controller.PqsDecalData.BakedPqsDecalIDList?.Count ?? 0;
            // Clean-slate skip: no instances on the body and nothing baked either. The body isn't
            // using decals at all, so don't surface a "click Bake" warning the user can't act on.
            if (liveInstanceCount == 0 && bakedCount == 0)
                yield break;

            // Check 1: instance templates missing from the baked list.
            var unbakedTemplateNames = new List<string>();
            if (controller.PqsDecalInstanceList != null)
            {
                var bakedIds = controller.PqsDecalData.BakedPqsDecalIDList;
                var seen = new HashSet<string>();
                foreach (PQSDecalInstance inst in controller.PqsDecalInstanceList)
                {
                    if (inst == null || inst.PQSDecal == null) continue;
                    string id = inst.PQSDecal.DecalID;
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!seen.Add(id)) continue;
                    if (bakedIds != null && bakedIds.Contains(id)) continue;
                    unbakedTemplateNames.Add(string.IsNullOrEmpty(inst.PQSDecal.name) ? id : inst.PQSDecal.name);
                }
            }

            if (unbakedTemplateNames.Count > 0)
            {
                string list = string.Join(", ", unbakedTemplateNames);
                string message = $"Decal template(s) referenced by instances but not in PQSDecalData: {list}. Click Bake to include them.";
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    message,
                    new[] { new ValidationFix("Bake all decals", () => DecalBaker.RebuildForController(controller)) }
                );
                yield break;
            }

            // Check 2: input-hash drift since last bake.
            PQSDecalControllerAuthoring snapshot = AuthoringSidecars.Find(controller.PqsDecalData);
            string current = PQSDecalBakeHash.Compute(controller);
            string last = snapshot != null ? snapshot.LastBakeHash : null;
            if (string.Equals(current, last))
                yield break;

            string driftMessage;
            if (liveInstanceCount == 0 && bakedCount > 0)
            {
                driftMessage = "All decals were removed but PQSDecalData still holds the previous bake. Click Bake to clear it.";
            }
            else if (string.IsNullOrEmpty(last))
            {
                driftMessage = "Decals haven't been baked yet for this body. Click Bake to populate PQSDecalData.";
            }
            else
            {
                driftMessage = "Decal inputs have changed since the last bake (shared textures, template values, or per-decal source textures). Click Bake to refresh PQSDecalData.";
            }
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                driftMessage,
                new[] { new ValidationFix("Bake all decals", () => DecalBaker.RebuildForController(controller)) }
            );
        }

        private static int CountLiveInstances(PQSDecalController controller)
        {
            if (controller.PqsDecalInstanceList == null)
                return 0;
            int count = 0;
            foreach (PQSDecalInstance inst in controller.PqsDecalInstanceList)
            {
                if (inst != null && inst.PQSDecal != null)
                    count++;
            }
            return count;
        }
    }
}
