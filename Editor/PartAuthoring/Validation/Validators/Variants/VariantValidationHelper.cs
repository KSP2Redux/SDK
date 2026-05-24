using System.Collections.Generic;
using UnityEditor;
using VSwift.Modules.Behaviours;
using VSwift.Modules.Data;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Variants
{
    /// <summary>
    /// Shared helpers for variant validators: locating the part's <see cref="Data_PartSwitch" /> + <see cref="Module_PartSwitch" /> pair, and the standard Undo/dirty pattern used by their fixes.
    /// </summary>
    internal static class VariantValidationHelper
    {
        /// <summary>
        /// Returns the part's <see cref="Data_PartSwitch" /> instance, or null if the part has no PartSwitch module.
        /// </summary>
        public static Data_PartSwitch FindData(PartValidationContext context)
        {
            if (context?.Modules == null)
            {
                return null;
            }
            foreach (var moduleData in context.Modules)
            {
                if (moduleData is Data_PartSwitch ps)
                {
                    return ps;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the <see cref="Module_PartSwitch" /> component on the part GameObject, or null if absent. Used as the Undo target for fixes that mutate variant data.
        /// </summary>
        public static Module_PartSwitch FindModule(PartValidationContext context)
        {
            if (context?.Part == null)
            {
                return null;
            }
            return context.Part.GetComponent<Module_PartSwitch>();
        }

        /// <summary>
        /// Records Undo against <paramref name="module" />, runs the mutation, then marks the module dirty so the prefab change persists. No-op when <paramref name="module" /> is null.
        /// </summary>
        public static void RecordAndApply(Module_PartSwitch module, string undoLabel, System.Action mutate)
        {
            if (module == null || mutate == null)
            {
                return;
            }
            Undo.RecordObject(module, undoLabel);
            mutate();
            EditorUtility.SetDirty(module);
        }

        /// <summary>
        /// Returns a new identifier built from <paramref name="baseId" /> that doesn't collide with any name in <paramref name="taken" />. Append "_2", "_3", etc. until a free slot is found.
        /// </summary>
        public static string MakeUniqueId(string baseId, HashSet<string> taken)
        {
            if (string.IsNullOrEmpty(baseId))
            {
                baseId = "id";
            }
            if (!taken.Contains(baseId))
            {
                return baseId;
            }
            int n = 2;
            while (taken.Contains($"{baseId}_{n}"))
            {
                n++;
            }
            return $"{baseId}_{n}";
        }
    }
}
