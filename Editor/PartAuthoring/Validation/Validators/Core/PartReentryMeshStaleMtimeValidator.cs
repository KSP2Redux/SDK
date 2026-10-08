using System.Collections.Generic;
using System.IO;
using Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Warns when any baked reentry mesh asset is older than the prefab.
    /// </summary>
    /// <remarks>
    /// ReentryMeshBaker writes <c>{prefabDir}/ReentryMeshes/{partName}_{group}_lod{i}.asset</c>
    /// per LOD per renderer group. If the prefab has been edited since the meshes were
    /// generated, the reentry visualization shows stale geometry under aerodynamic stress.
    /// Re-bake via Quick Tools > Bake Reentry Mesh. Warning severity because the runtime still
    /// functions (the rendered reentry effect just does not match the actual part shape).
    /// </remarks>
    public sealed class PartReentryMeshStaleMtimeValidator : IPartValidator
    {
        /// <summary>Stable code emitted when any reentry mesh is older than the prefab.</summary>
        public const string Code = "PART_REENTRY_MESH_STALE_MTIME";

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Part == null)
            {
                yield break;
            }
            string prefabPath = PartPathResolver.ResolvePrefabPath(context.Part);
            if (string.IsNullOrEmpty(prefabPath) || !File.Exists(prefabPath))
            {
                yield break;
            }
            string prefabDir = Path.GetDirectoryName(prefabPath);
            if (string.IsNullOrEmpty(prefabDir))
            {
                yield break;
            }
            string reentryDir = Path.Combine(prefabDir, "ReentryMeshes").Replace('\\', '/');
            if (!Directory.Exists(reentryDir))
            {
                yield break;
            }
            string[] meshes = Directory.GetFiles(reentryDir, "*.asset");
            if (meshes.Length == 0)
            {
                yield break;
            }
            System.DateTime prefabMtime = File.GetLastWriteTimeUtc(prefabPath);
            System.DateTime oldestMesh = System.DateTime.MaxValue;
            foreach (string mesh in meshes)
            {
                System.DateTime meshMtime = File.GetLastWriteTimeUtc(mesh);
                if (meshMtime < oldestMesh)
                {
                    oldestMesh = meshMtime;
                }
            }
            if (oldestMesh >= prefabMtime)
            {
                yield break;
            }
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Reentry meshes are {(prefabMtime - oldestMesh).TotalMinutes:0} min older than the most recent prefab edit. The reentry visual may not match the current part shape. Re-bake via Quick Tools > Bake Reentry Mesh.");
        }
    }
}
