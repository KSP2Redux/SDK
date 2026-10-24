using System.Reflection;
using KSP.VFX;
using UnityEngine;

namespace KSP.Editor
{
    /// <summary>
    /// Shares effect initialization between engine and RCS editor previews.
    /// </summary>
    internal static class EngineVFXPreviewUtility
    {
        /// <summary>
        /// Initializes effect components whose runtime callbacks have not run in edit mode.
        /// </summary>
        /// <param name="behaviour">The component implementing the effect.</param>
        /// <param name="engineFxData">The effect interface exposed by the component.</param>
        internal static void EnsureEngineFxComponentInitialized(MonoBehaviour behaviour, IEngineFXData engineFxData)
        {
            MethodInfo awake = behaviour.GetType()
                .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            MethodInfo onEnable = behaviour.GetType()
                .GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            if (engineFxData is ThrottleParticleSystemData particleData && particleData.VFX == null)
            {
                awake?.Invoke(behaviour, null);
            }

            if (engineFxData.TriggerUpdateVisuals != null)
                return;

            awake?.Invoke(behaviour, null);
            onEnable?.Invoke(behaviour, null);
        }
    }
}
