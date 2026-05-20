using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>Small helpers for component lookup that respect Unity's fake-null behavior.</summary>
    public static class PlanetAuthoringComponentHelpers
    {
        /// <summary>
        /// Returns the existing component of type <typeparamref name="T"/> on <paramref name="go"/>,
        /// adding one when none is present.
        /// </summary>
        /// <remarks>
        /// Uses explicit <c>== null</c> rather than the null-coalescing operator so destroyed or
        /// missing components return Unity's fake-null sentinel correctly.
        /// </remarks>
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }
    }
}
