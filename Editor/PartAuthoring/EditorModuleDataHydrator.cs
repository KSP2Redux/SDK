using System;
using System.Reflection;
using KSP.Sim.Definitions;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring
{
    internal static class EditorModuleDataHydrator
    {
        private const BindingFlags FieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static void Hydrate(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (component is PartBehaviourModule module && module.DataModules == null)
            {
                module.DataModules = new ModuleDataList();
            }

            EnsureSerializedDataFields(component);

            MethodInfo addMethod =
                component.GetType().GetMethod("AddDataModules", BindingFlags.Instance | BindingFlags.NonPublic) ??
                component.GetType().GetMethod("AddDataModules", BindingFlags.Instance | BindingFlags.Public);
            addMethod?.Invoke(component, Array.Empty<object>());
        }

        private static void EnsureSerializedDataFields(Component component)
        {
            for (Type type = component.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(FieldFlags))
                {
                    if (!typeof(ModuleData).IsAssignableFrom(field.FieldType))
                    {
                        continue;
                    }
                    if (!field.IsPublic && !field.IsDefined(typeof(SerializeField), inherit: true))
                    {
                        continue;
                    }
                    if (field.GetValue(component) != null)
                    {
                        continue;
                    }

                    try
                    {
                        field.SetValue(component, Activator.CreateInstance(field.FieldType));
                    }
                    catch
                    {
                        // Some module data types may require explicit construction. Leave those for
                        // the module's own editor/runtime initialization path.
                    }
                }
            }
        }
    }
}
