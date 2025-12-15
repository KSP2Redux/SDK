using Redux.VFX.Plume.Services;
using UnityEngine;

namespace Ksp2UnityTools.Editor.Plumes.Services
{
    public class UnityLogger : IPlumeLogger
    {
        private const string Prefix = "[Plume]";

        public void LogInfo(object message)
        {
            Debug.Log($"{Prefix} {message}");
        }

        public void LogDebug(object message)
        {
            Debug.Log($"{Prefix} {message}");
        }

        public void LogWarning(object message)
        {
            Debug.LogWarning($"{Prefix} {message}");
        }

        public void LogError(object message)
        {
            Debug.LogError($"{Prefix} {message}");
        }
    }
}