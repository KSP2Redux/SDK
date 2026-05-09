using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assemblies;

namespace Ksp2UnityTools.Editor
{
#if !REDUX
    public static class PlaymodeInitShim
    {
        private static readonly MethodInfo[] initMethods = GetAllReduxInitMethods();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitSubsystemRegistration() => RunInitMethodsOfType(RuntimeInitializeLoadType.SubsystemRegistration);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void InitAfterAssembliesLoaded() => RunInitMethodsOfType(RuntimeInitializeLoadType.AfterAssembliesLoaded);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitBeforeSceneLoad() => RunInitMethodsOfType(RuntimeInitializeLoadType.BeforeSceneLoad);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void InitBeforeSplashScreen() => RunInitMethodsOfType(RuntimeInitializeLoadType.BeforeSplashScreen);

        private static MethodInfo[] GetAllReduxInitMethods()
        {
            // For some reason, TypeCache.GetMethodsWithAttribute<RuntimeInitializeOnLoadMethodAttribute>(
            // doesn't return anything from Assembly-CSharp.
            // So manually reflect into the assmebly to find the methods.
            Assembly mainAssembly = null;
            foreach (var thing in CurrentAssemblies.GetLoadedAssemblies())
            {
                if (thing.FullName.StartsWith("Assembly-CSharp,"))
                {
                    mainAssembly = thing;
                    break;
                }
            }

            // RuntimeInitializeOnLoadMethod requires the method to be static; AutoRegisters are usually private static.
            // Default GetMethods() returns only Public|Instance, which silently misses every candidate.
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            return mainAssembly.GetTypes()
                      .SelectMany(t => t.GetMethods(flags))
                      .Where(m => m.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false).Length > 0)
                      .ToArray();
        }

        private static void RunInitMethodsOfType(RuntimeInitializeLoadType type)
        {
            Debug.Log($"KSP2UT: Running Assembly-CSharp [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.{type})] methods");

            foreach (var method in initMethods)
            {
                foreach (RuntimeInitializeOnLoadMethodAttribute attribute in method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false).Cast<RuntimeInitializeOnLoadMethodAttribute>())
                {
                    if (attribute.loadType == type)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
        }
    }
#endif
}
