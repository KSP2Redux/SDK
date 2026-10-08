using System;
using System.Collections.Generic;
using System.Reflection;
using KSP;
using KSP.IO;
using KSP.Sim.Definitions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Body
{
    /// <summary>
    /// Errors when serializing the body, re-parsing, and re-serializing produces a different JSON.
    /// </summary>
    /// <remarks>
    /// Catches serialization-layer bugs that would otherwise only surface in a built game. The validator runs the
    /// exact <see cref="IOProvider" /> path the Save Body JSON button uses, including the
    /// <see cref="ReferenceLoopHandling.Ignore" /> handling, then compares the two JSON strings via
    /// <see cref="JToken.DeepEquals" /> so property-order differences are ignored. Expensive validator: the second
    /// serialization round trip plus a JToken parse is meaningful work.
    /// </remarks>
    public sealed class JsonRoundTripValidator : IPlanetValidator
    {
        /// <summary>Stable code emitted when the round-trip drops or alters any field.</summary>
        public const string CodeLossy = "JSON_LOSSY";

        private static bool _ioProviderInitialized;

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            CelestialBodyCore core = body?.Core;
            if (core == null) yield break;

            EnsureIoProviderInitialized();

            string firstJson = null;
            string secondJson = null;
            string failure = null;
            try
            {
                var settings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
                firstJson = IOProvider.ToJson(core, settings);
                CelestialBodyCore reparsed = IOProvider.FromJson<CelestialBodyCore>(firstJson, settings);
                secondJson = IOProvider.ToJson(reparsed, settings);
            }
            catch (Exception e)
            {
                failure = e.Message;
            }

            if (failure != null)
            {
                yield return new ValidationIssue(
                    CodeLossy,
                    ValidationSeverity.Error,
                    $"JSON round-trip threw: {failure}");
                yield break;
            }

            JObject a = JObject.Parse(firstJson);
            JObject b = JObject.Parse(secondJson);
            if (JToken.DeepEquals(a, b)) yield break;

            string diff = DescribeFirstDiff(a, b) ?? "(unknown)";
            yield return new ValidationIssue(
                CodeLossy,
                ValidationSeverity.Error,
                $"JSON round-trip diverged at {diff}.");
        }

        private static void EnsureIoProviderInitialized()
        {
            if (_ioProviderInitialized) return;
            var init = typeof(IOProvider).GetMethod("Init", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (init != null)
            {
                try { init.Invoke(null, Array.Empty<object>()); }
                catch (Exception e) { Debug.LogWarning($"[JsonRoundTripValidator] IOProvider.Init threw: {e.Message}"); }
            }
            _ioProviderInitialized = true;
        }

        private static string DescribeFirstDiff(JToken a, JToken b)
        {
            if (JToken.DeepEquals(a, b)) return null;
            if (a is JObject objA && b is JObject objB)
            {
                foreach (var prop in objA.Properties())
                {
                    if (!objB.TryGetValue(prop.Name, out JToken bChild))
                        return $"path '{prop.Path}': missing on the round-tripped side";
                    string child = DescribeFirstDiff(prop.Value, bChild);
                    if (child != null) return child;
                }
                foreach (var prop in objB.Properties())
                    if (!objA.ContainsKey(prop.Name)) return $"path '{prop.Path}': appeared after round-trip";
                return $"path '{a.Path}': object contents differ";
            }
            if (a is JArray arrA && b is JArray arrB)
            {
                int n = Mathf.Min(arrA.Count, arrB.Count);
                for (int i = 0; i < n; i++)
                {
                    string child = DescribeFirstDiff(arrA[i], arrB[i]);
                    if (child != null) return child;
                }
                if (arrA.Count != arrB.Count)
                    return $"path '{a.Path}': length {arrA.Count} -> {arrB.Count}";
                return $"path '{a.Path}': array contents differ";
            }
            return $"path '{a.Path}': '{Format(a)}' -> '{Format(b)}'";
        }

        private static string Format(JToken token)
        {
            string s = token?.ToString(Formatting.None) ?? "null";
            return s.Length > 80 ? s.Substring(0, 77) + "..." : s;
        }
    }
}
