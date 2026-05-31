using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KSP.Modules;
using KSP.Sim.Definitions;
using Redux.Ksp1Import;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring
{
    internal static class Ksp1EditorPartModuleSync
    {
        public static void Sync(GameObject prefab, PartCore core, Ksp1ImportReport report)
        {
            if (prefab == null || core?.data?.serializedPartModules == null)
            {
                return;
            }

            Dictionary<Type, Queue<PartBehaviourModule>> existingByType = prefab
                .GetComponents<PartBehaviourModule>()
                .GroupBy(module => module.GetType())
                .ToDictionary(
                    group => group.Key,
                    group => new Queue<PartBehaviourModule>(group),
                    TypeComparer.Instance
                );

            foreach (SerializedPartModule serializedModule in core.data.serializedPartModules)
            {
                Type behaviourType = serializedModule.BehaviourType;
                if (behaviourType == null || !typeof(PartBehaviourModule).IsAssignableFrom(behaviourType))
                {
                    continue;
                }

                PartBehaviourModule module = null;
                if (existingByType.TryGetValue(behaviourType, out Queue<PartBehaviourModule> existing) &&
                    existing.Count > 0)
                {
                    module = existing.Dequeue();
                }

                if (module == null)
                {
                    module = prefab.AddComponent(behaviourType) as PartBehaviourModule;
                }

                if (module == null)
                {
                    report.Warn($"Part '{core.data.partName}' could not add module component '{behaviourType.FullName}'.");
                    continue;
                }

                module.hideFlags |= HideFlags.HideInInspector;
                Hydrate(module, serializedModule);
                ConfigureDeployableAnimator(module);
            }
        }

        private static void Hydrate(PartBehaviourModule module, SerializedPartModule serializedModule)
        {
            module.DataModules.Clear();
            foreach (SerializedModuleData moduleData in serializedModule.ModuleData ?? new List<SerializedModuleData>())
            {
                if (moduleData.DataObject == null)
                {
                    continue;
                }

                module.DataModules.Add(moduleData.DataObject.DataType, moduleData.DataObject);
                AssignModuleDataField(module, moduleData.DataObject);
            }
        }

        private static void AssignModuleDataField(PartBehaviourModule module, ModuleData data)
        {
            Type dataType = data.GetType();
            for (Type type = module.GetType(); type != null && type != typeof(PartBehaviourModule); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!field.FieldType.IsAssignableFrom(dataType))
                    {
                        continue;
                    }

                    field.SetValue(module, data);
                    return;
                }
            }
        }

        private static void ConfigureDeployableAnimator(PartBehaviourModule module)
        {
            if (module is not Module_Deployable deployable || deployable.animator != null)
            {
                return;
            }

            Animator animator = null;
            Data_Deployable data = GetDeployableData(deployable);
            if (data != null && !string.IsNullOrWhiteSpace(data.animationName))
            {
                Transform namedTransform = FindChildRecursive(deployable.gameObject.transform, data.animationName);
                if (namedTransform != null)
                {
                    animator = namedTransform.GetComponentInChildren<Animator>(true);
                }
            }

            if (animator == null)
            {
                animator = deployable.GetComponentInChildren<Animator>(true);
            }
            if (animator != null)
            {
                deployable.animator = animator;
            }
        }

        private static Data_Deployable GetDeployableData(Module_Deployable deployable)
        {
            for (Type type = deployable.GetType(); type != null && type != typeof(PartBehaviourModule); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (field.FieldType == typeof(Data_Deployable))
                    {
                        return field.GetValue(deployable) as Data_Deployable;
                    }
                }
            }
            return null;
        }

        private static Transform FindChildRecursive(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrWhiteSpace(childName))
            {
                return null;
            }
            if (string.Equals(parent.name, childName, StringComparison.Ordinal))
            {
                return parent;
            }

            foreach (Transform child in parent)
            {
                Transform match = FindChildRecursive(child, childName);
                if (match != null)
                {
                    return match;
                }
            }
            return null;
        }

        private sealed class TypeComparer : IEqualityComparer<Type>
        {
            public static readonly TypeComparer Instance = new();

            public bool Equals(Type x, Type y)
            {
                return x == y;
            }

            public int GetHashCode(Type obj)
            {
                return obj?.GetHashCode() ?? 0;
            }
        }
    }
}
