using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP.VFX;
using Redux.Ksp1Import;
using Redux.Ksp1Import.Config;
using Redux.Ksp1Import.Model;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.PartAuthoring
{
    internal static class Ksp1EditorPlumeVariantWriter
    {
        private const string PlumePrefabRoot = "Assets/Modules/KSP2UnityTools/Assets/Plumes/Prefabs";

        public static void Apply(
            Ksp1ConfigNode partNode,
            GameObject partPrefab,
            KSP.Sim.Definitions.PartData partData,
            Ksp1EnginePlumeCatalog plumeCatalog,
            string partFolder,
            bool overwriteGenerated,
            Ksp1ImportReport report,
            string partName
        )
        {
            RemoveRuntimePlumes(partPrefab);
            string plumeFolder = Ksp1EditorAssetUtility.EnsureFolder(partFolder, "Plumes");
            Dictionary<int, Dictionary<ThrottleVFXManager.FXmodeEvent, List<ThrottleVFXManager.EngineEffect>>> effectsByMode =
                new();

            int engineCount = ApplyEnginePlumes(partNode, partPrefab, partData, plumeCatalog, plumeFolder, overwriteGenerated, effectsByMode, report, partName);
            if (engineCount > 0)
            {
                Ksp1EnginePlumeBuilder.ApplyEffects(partPrefab, effectsByMode);
            }

            int rcsCount = ApplyRcsPlumes(partNode, partPrefab, partData, plumeFolder, overwriteGenerated, report, partName);
            if (rcsCount > 0 && partPrefab.GetComponent<RCSVFXManager>() == null)
            {
                partPrefab.AddComponent<RCSVFXManager>();
            }

            if (engineCount > 0 || rcsCount > 0)
            {
                report.Important($"Part '{partName}' saved {engineCount + rcsCount} KSP1 plume prefab variant(s).");
            }
        }

        private static int ApplyEnginePlumes(
            Ksp1ConfigNode partNode,
            GameObject partPrefab,
            KSP.Sim.Definitions.PartData partData,
            Ksp1EnginePlumeCatalog plumeCatalog,
            string plumeFolder,
            bool overwriteGenerated,
            Dictionary<int, Dictionary<ThrottleVFXManager.FXmodeEvent, List<ThrottleVFXManager.EngineEffect>>> effectsByMode,
            Ksp1ImportReport report,
            string partName
        )
        {
            int count = 0;
            Ksp1EnginePlumePlan plan = Ksp1EnginePlumeBuilder.BuildPlan(partNode, partData, plumeCatalog);
            HashSet<string> placed = new(StringComparer.OrdinalIgnoreCase);
            foreach (Ksp1EnginePlumeInstancePlan instancePlan in plan.Instances)
            {
                GameObject sourcePrefab = LoadPlumePrefab(instancePlan.PlumePrefabAddress, report);
                if (sourcePrefab == null)
                {
                    continue;
                }

                foreach (Transform parent in FindChildrenRecursive(partPrefab.transform, instancePlan.ParentTransformName))
                {
                    string key = instancePlan.ModeIndex + "|" + instancePlan.ParentTransformName + "|" + parent.GetInstanceID();
                    if (!placed.Add(key))
                    {
                        continue;
                    }

                    string variantPath =
                        $"{plumeFolder}/{Ksp1EditorAssetUtility.SanitizePathSegment(instancePlan.ModeName)}_{Ksp1EditorAssetUtility.SanitizePathSegment(parent.name)}_{count}.prefab";
                    GameObject variant = CreateEngineVariant(sourcePrefab, parent, partPrefab.transform, instancePlan, variantPath, overwriteGenerated, report);
                    if (variant == null)
                    {
                        continue;
                    }

                    variant.name = "[KSP1 Plume] " + instancePlan.ModeName + " " + parent.name;
                    Ksp1EnginePlumeBuilder.AddEffects(effectsByMode, instancePlan.ModeIndex, variant, instancePlan.IsSolidPlume);
                    count++;
                }
            }

            return count;
        }

        private static int ApplyRcsPlumes(
            Ksp1ConfigNode partNode,
            GameObject partPrefab,
            KSP.Sim.Definitions.PartData partData,
            string plumeFolder,
            bool overwriteGenerated,
            Ksp1ImportReport report,
            string partName
        )
        {
            int count = 0;
            Ksp1RcsPlumePlan plan = Ksp1RcsPlumeBuilder.BuildPlan(partNode, partData);
            if (plan.Instances.Count == 0)
            {
                return 0;
            }

            Ksp1RcsPlumeBuilder.RegisterThrusterTransforms(partPrefab, plan.Instances);
            GameObject sourcePrefab = LoadPlumePrefab("Monoprop Plume.prefab", report);
            if (sourcePrefab == null)
            {
                return 0;
            }

            HashSet<string> placed = new(StringComparer.OrdinalIgnoreCase);
            foreach (Ksp1RcsPlumeInstancePlan instancePlan in plan.Instances)
            {
                foreach (Transform parent in FindChildrenRecursive(partPrefab.transform, instancePlan.ParentTransformName))
                {
                    string key = instancePlan.ParentTransformName + "|" + parent.GetInstanceID();
                    if (!placed.Add(key))
                    {
                        continue;
                    }

                    string variantPath = $"{plumeFolder}/RCS_{Ksp1EditorAssetUtility.SanitizePathSegment(parent.name)}_{count}.prefab";
                    GameObject variant = CreateRcsVariant(sourcePrefab, parent, partPrefab.transform, instancePlan, variantPath, overwriteGenerated, report);
                    if (variant == null)
                    {
                        continue;
                    }

                    variant.name = "[KSP1 RCS Plume] " + parent.name;
                    count++;
                }
            }

            return count;
        }

        private static GameObject CreateEngineVariant(
            GameObject sourcePrefab,
            Transform parent,
            Transform partRoot,
            Ksp1EnginePlumeInstancePlan plan,
            string variantPath,
            bool overwriteGenerated,
            Ksp1ImportReport report
        )
        {
            GameObject temp = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            if (temp == null)
            {
                return null;
            }

            temp.transform.SetParent(parent, false);
            Ksp1EnginePlumeBuilder.ConfigurePlumeInstance(temp, parent, partRoot, plan);
            GameObject saved = SaveVariant(temp, variantPath, overwriteGenerated, report);
            Object.DestroyImmediate(temp);
            return saved == null ? null : PrefabUtility.InstantiatePrefab(saved, parent) as GameObject;
        }

        private static GameObject CreateRcsVariant(
            GameObject sourcePrefab,
            Transform parent,
            Transform partRoot,
            Ksp1RcsPlumeInstancePlan plan,
            string variantPath,
            bool overwriteGenerated,
            Ksp1ImportReport report
        )
        {
            GameObject temp = PrefabUtility.InstantiatePrefab(sourcePrefab) as GameObject;
            if (temp == null)
            {
                return null;
            }

            temp.transform.SetParent(parent, false);
            Ksp1RcsPlumeBuilder.ConfigurePlumeInstance(temp, plan, parent, partRoot);
            GameObject saved = SaveVariant(temp, variantPath, overwriteGenerated, report);
            Object.DestroyImmediate(temp);
            return saved == null ? null : PrefabUtility.InstantiatePrefab(saved, parent) as GameObject;
        }

        private static GameObject SaveVariant(GameObject temp, string path, bool overwriteGenerated, Ksp1ImportReport report)
        {
            if (File.Exists(path) && !overwriteGenerated)
            {
                report.Warn($"Skipping existing plume variant '{path}'.");
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return PrefabUtility.SaveAsPrefabAsset(temp, path);
        }

        private static GameObject LoadPlumePrefab(string prefabAddress, Ksp1ImportReport report)
        {
            string path = $"{PlumePrefabRoot}/{prefabAddress}";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                report.Warn($"KSP1 plume conversion could not find editor plume prefab '{path}'.");
            }

            return prefab;
        }

        private static void RemoveRuntimePlumes(GameObject partPrefab)
        {
            List<GameObject> generated = partPrefab.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("[KSP1 Plume]", StringComparison.Ordinal) ||
                            t.name.StartsWith("[KSP1 RCS Plume]", StringComparison.Ordinal))
                .Select(t => t.gameObject)
                .ToList();
            foreach (GameObject obj in generated)
            {
                Object.DestroyImmediate(obj);
            }

            foreach (ThrottleVFXManager manager in partPrefab.GetComponents<ThrottleVFXManager>())
            {
                Object.DestroyImmediate(manager);
            }

            foreach (RCSVFXManager manager in partPrefab.GetComponents<RCSVFXManager>())
            {
                Object.DestroyImmediate(manager);
            }
        }

        private static IEnumerable<Transform> FindChildrenRecursive(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrWhiteSpace(name))
            {
                yield break;
            }

            if (parent.name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                yield return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                foreach (Transform child in FindChildrenRecursive(parent.GetChild(i), name))
                {
                    yield return child;
                }
            }
        }
    }
}
