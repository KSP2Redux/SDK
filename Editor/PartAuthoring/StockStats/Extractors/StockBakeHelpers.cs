#if REDUX
using System.Collections.Generic;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Locates module DataObject mirrors by type within a part.</summary>
    internal static class ModuleResolver
    {
        /// <summary>Returns the first module DataObject of type <typeparamref name="T" />, or null.</summary>
        /// <remarks>
        /// Type-safe lookup made possible by the polymorphic <see cref="DataObjectMirror" />
        /// hierarchy. Callers ask for "the engine module data" by type and skip string
        /// matching on module names.
        /// </remarks>
        /// <typeparam name="T">Concrete mirror type to look up.</typeparam>
        /// <param name="part">Part record to search.</param>
        /// <returns>The first matching DataObject, or null when none is present.</returns>
        public static T FindModuleData<T>(StockBakePartCore part) where T : DataObjectMirror
        {
            List<ModuleEnvelopeMirror> modules = part?.Data?.serializedPartModules;
            if (modules == null)
            {
                return null;
            }
            for (int i = 0; i < modules.Count; i++)
            {
                ModuleEnvelopeMirror module = modules[i];
                if (module?.ModuleData == null)
                {
                    continue;
                }
                for (int j = 0; j < module.ModuleData.Count; j++)
                {
                    ModuleDataMirror data = module.ModuleData[j];
                    if (data?.DataObject is T match)
                    {
                        return match;
                    }
                }
            }
            return null;
        }
    }

    /// <summary>Samples authored FloatCurve key arrays at arbitrary time values.</summary>
    internal static class CurveEvaluator
    {
        /// <summary>Returns the curve value at <paramref name="time" /> via clamped linear interpolation.</summary>
        /// <remarks>
        /// Ignores per-key tangents. KSP2 authored Isp curves have keys at the points the
        /// bake samples (atm 0 and atm 1), so linear interpolation between adjacent keys is a
        /// fine approximation. Returns NaN when the curve is empty.
        /// </remarks>
        /// <param name="curve">Curve to sample.</param>
        /// <param name="time">Time at which to evaluate the curve.</param>
        /// <returns>The interpolated value, or <see cref="float.NaN" /> when the curve has no keys.</returns>
        public static float EvaluateAt(CurveMirror curve, float time)
        {
            List<KeyMirror> keys = curve?.fCurve?.keys;
            if (keys == null || keys.Count == 0)
            {
                return float.NaN;
            }
            KeyMirror prev = keys[0];
            if (prev == null)
            {
                return float.NaN;
            }
            if (time <= prev.time)
            {
                return prev.value;
            }
            for (int i = 1; i < keys.Count; i++)
            {
                KeyMirror k = keys[i];
                if (k == null)
                {
                    continue;
                }
                if (k.time >= time)
                {
                    float span = k.time - prev.time;
                    if (span <= 0f)
                    {
                        return k.value;
                    }
                    float t = (time - prev.time) / span;
                    return Mathf.Lerp(prev.value, k.value, t);
                }
                prev = k;
            }
            return prev.value;
        }
    }
}
#endif
