#if REDUX
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats
{
    /// <summary>One-shot bake of the base-game asset dump into a shipped <see cref="StockStatsLookup" /> asset.</summary>
    /// <remarks>
    /// Two-pass: first walk over all <c>.bytes</c> files classifies each as raw resource, recipe,
    /// or part. Raw masses go into a dictionary, recipes are resolved recursively against the
    /// raw masses, parts are bucketed by (family, sizeCategory). The aggregation step then runs
    /// every <see cref="IStockFieldExtractor" /> across each bucket's parts. The lookup asset is
    /// either created fresh or updated in place so the asset GUID survives rebakes.
    /// </remarks>
    public static class StockStatsBaker
    {
        /// <summary>Bake settings. Currently only the verbose flag is exposed.</summary>
        public sealed class BakeOptions
        {
            /// <summary>When true, populates <see cref="BakeResult.VerboseLog" /> with per-file diagnostic messages.</summary>
            public bool Verbose;
            /// <summary>Default options with verbose disabled.</summary>
            public static BakeOptions Default => new();
        }

        /// <summary>Summary returned to the bake window for display after each run.</summary>
        public sealed class BakeResult
        {
            /// <summary>Number of part files successfully scanned.</summary>
            public int PartsScanned;
            /// <summary>Number of distinct (family, size) buckets produced.</summary>
            public int BucketCount;
            /// <summary>Count of raw resource masses read from the source.</summary>
            public int RawResourcesResolved;
            /// <summary>Count of recipes whose mass-per-unit resolved against the raw masses.</summary>
            public int RecipesResolved;
            /// <summary>Count of recipes that could not resolve against the raw masses.</summary>
            public int UnresolvedRecipes;
            /// <summary>Count of source files that failed to read or parse.</summary>
            public int FailedFiles;
            /// <summary>Source-folder hash captured at bake time, used to detect later staleness.</summary>
            public string SourceHash;
            /// <summary>Project-relative path of the asset that was written.</summary>
            public string OutputAssetPath;
            /// <summary>Per-file verbose log lines, populated when <see cref="BakeOptions.Verbose" /> is set.</summary>
            public List<string> VerboseLog = new();
        }

        /// <summary>Runs the bake and writes / updates the lookup asset.</summary>
        /// <param name="sourceDir">Absolute path to <c>ksp2-assets/Assets/</c>.</param>
        /// <param name="outputAssetPath">Project-relative path (under <c>Assets/...</c>) to write to.</param>
        /// <param name="options">Optional settings. Null defaults to <see cref="BakeOptions.Default" />.</param>
        /// <returns>Summary of the bake run.</returns>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="sourceDir" /> is empty or does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="outputAssetPath" /> is null or empty.</exception>
        public static BakeResult Bake(string sourceDir, string outputAssetPath, BakeOptions options = null)
        {
            options ??= BakeOptions.Default;
            var result = new BakeResult { OutputAssetPath = outputAssetPath };

            if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
            {
                throw new InvalidOperationException($"Source folder not found: {sourceDir}");
            }
            if (string.IsNullOrEmpty(outputAssetPath))
            {
                throw new ArgumentException("Output asset path required.", nameof(outputAssetPath));
            }

            var partTokens = new List<JObject>();
            var rawMass = new Dictionary<string, float>(StringComparer.Ordinal);
            var recipes = new Dictionary<string, RecipeMirror>(StringComparer.Ordinal);

            foreach (string file in Directory.EnumerateFiles(sourceDir, "*.bytes", SearchOption.TopDirectoryOnly))
            {
                ClassifyFile(file, partTokens, rawMass, recipes, result, options);
            }

            var massPerUnit = new Dictionary<string, float>(rawMass, StringComparer.Ordinal);
            foreach (var pair in recipes)
            {
                if (massPerUnit.ContainsKey(pair.Key))
                {
                    continue;
                }
                float resolved = ResolveRecipe(pair.Key, pair.Value, recipes, massPerUnit, new HashSet<string>(StringComparer.Ordinal));
                if (float.IsNaN(resolved))
                {
                    result.UnresolvedRecipes++;
                    if (options.Verbose)
                    {
                        result.VerboseLog.Add($"Recipe '{pair.Key}' could not be resolved (missing ingredient mass).");
                    }
                }
            }
            result.RawResourcesResolved = rawMass.Count;
            result.RecipesResolved = recipes.Count - result.UnresolvedRecipes;

            var ctx = new BakeContext(massPerUnit);
            var extractors = StockStatsExtractorRegistry.Create();
            var partsByBucket = new Dictionary<PartBucketKey, List<StockBakePartCore>>();

            foreach (JObject token in partTokens)
            {
                StockBakePartCore part;
                try
                {
                    part = token.ToObject<StockBakePartCore>();
                }
                catch (Exception ex)
                {
                    result.FailedFiles++;
                    if (options.Verbose)
                    {
                        result.VerboseLog.Add($"Part deserialize failed: {ex.Message}");
                    }
                    continue;
                }
                if (part?.Data == null || string.IsNullOrEmpty(part.Data.partName))
                {
                    continue;
                }
                var key = new PartBucketKey(part.Data.family ?? string.Empty, part.Data.sizeCategory ?? string.Empty);
                if (!partsByBucket.TryGetValue(key, out var list))
                {
                    list = new List<StockBakePartCore>();
                    partsByBucket[key] = list;
                }
                list.Add(part);
                result.PartsScanned++;
            }
            result.BucketCount = partsByBucket.Count;

            StockStatsLookup lookup = AssetDatabase.LoadAssetAtPath<StockStatsLookup>(outputAssetPath);
            if (lookup == null)
            {
                lookup = ScriptableObject.CreateInstance<StockStatsLookup>();
                EnsureDirectory(outputAssetPath);
                AssetDatabase.CreateAsset(lookup, outputAssetPath);
            }
            else
            {
                lookup.Buckets.Clear();
            }

            foreach (var pair in partsByBucket)
            {
                lookup.Buckets.Add(AggregateBucket(pair.Key, pair.Value, extractors, ctx));
            }
            lookup.ResourceMasses.Clear();
            foreach (var pair in massPerUnit)
            {
                lookup.ResourceMasses.Add(new ResourceMassEntry
                {
                    Name = pair.Key,
                    MassPerUnit = pair.Value,
                });
            }
            lookup.SourceHash = ComputeSourceHash(sourceDir);
            lookup.BakedAt = DateTime.UtcNow.ToString("o");
            lookup.SchemaVersion = 2;
            lookup.PartsScanned = result.PartsScanned;
            result.SourceHash = lookup.SourceHash;

            EditorUtility.SetDirty(lookup);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return result;
        }

        /// <summary>Recomputes only the source hash. Used by the bake window to detect staleness without rebaking.</summary>
        /// <param name="sourceDir">Absolute path to the source folder.</param>
        /// <returns>Hex-encoded SHA-256 of the file-count, total size, and modified-time fingerprint. Empty when the folder does not exist.</returns>
        public static string ComputeSourceHash(string sourceDir)
        {
            if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
            {
                return string.Empty;
            }
            long totalBytes = 0;
            long totalMtime = 0;
            int count = 0;
            foreach (string file in Directory.EnumerateFiles(sourceDir, "*.bytes", SearchOption.TopDirectoryOnly))
            {
                var info = new FileInfo(file);
                totalBytes += info.Length;
                totalMtime += info.LastWriteTimeUtc.Ticks;
                count++;
            }
            string combined = $"{count}-{totalBytes}-{totalMtime}";
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString();
        }

        private static void ClassifyFile(
            string file,
            List<JObject> partTokens,
            Dictionary<string, float> rawMass,
            Dictionary<string, RecipeMirror> recipes,
            BakeResult result,
            BakeOptions options)
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch
            {
                result.FailedFiles++;
                return;
            }
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            char first = SkipWhitespace(text);
            if (first != '{' && first != '[')
            {
                return;
            }

            JObject top;
            try
            {
                top = JObject.Parse(text);
            }
            catch
            {
                return;
            }

            if (top["isRecipe"]?.Value<bool>() == true)
            {
                StockBakeResourceDef def = SafeDeserialize<StockBakeResourceDef>(top);
                if (def?.RecipeData?.name != null)
                {
                    recipes[def.RecipeData.name] = def.RecipeData;
                }
                return;
            }

            JToken data = top["data"];
            if (data == null)
            {
                return;
            }

            string partName = data["partName"]?.ToString();
            if (!string.IsNullOrEmpty(partName))
            {
                partTokens.Add(top);
                return;
            }

            string resourceName = data["name"]?.ToString();
            if (!string.IsNullOrEmpty(resourceName) && data["massPerUnit"] != null)
            {
                StockBakeResourceDef def = SafeDeserialize<StockBakeResourceDef>(top);
                if (def?.Data?.name != null && def.Data.massPerUnit > 0f)
                {
                    rawMass[def.Data.name] = def.Data.massPerUnit;
                }
            }
        }

        private static T SafeDeserialize<T>(JObject token) where T : class
        {
            try
            {
                return token.ToObject<T>();
            }
            catch
            {
                return null;
            }
        }

        private static char SkipWhitespace(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
                {
                    return c;
                }
            }
            return '\0';
        }

        private static float ResolveRecipe(
            string name,
            RecipeMirror recipe,
            Dictionary<string, RecipeMirror> recipes,
            Dictionary<string, float> resolved,
            HashSet<string> visiting)
        {
            if (resolved.TryGetValue(name, out float cached))
            {
                return cached;
            }
            if (recipe?.ingredients == null || recipe.ingredients.Count == 0)
            {
                return float.NaN;
            }
            if (!visiting.Add(name))
            {
                return float.NaN;
            }
            float total = 0f;
            foreach (RecipeIngredientMirror ing in recipe.ingredients)
            {
                if (ing == null || string.IsNullOrEmpty(ing.name))
                {
                    visiting.Remove(name);
                    return float.NaN;
                }
                float ingMass;
                if (resolved.TryGetValue(ing.name, out ingMass))
                {
                    // raw or already-resolved recipe
                }
                else if (recipes.TryGetValue(ing.name, out RecipeMirror sub))
                {
                    ingMass = ResolveRecipe(ing.name, sub, recipes, resolved, visiting);
                    if (float.IsNaN(ingMass))
                    {
                        visiting.Remove(name);
                        return float.NaN;
                    }
                }
                else
                {
                    visiting.Remove(name);
                    return float.NaN;
                }
                total += ing.unitsPerRecipeUnit * ingMass;
            }
            visiting.Remove(name);
            resolved[name] = total;
            return total;
        }

        private static StockBucket AggregateBucket(
            PartBucketKey key,
            List<StockBakePartCore> parts,
            List<IStockFieldExtractor> extractors,
            BakeContext ctx)
        {
            var bucket = new StockBucket { Family = key.Family, SizeCategory = key.SizeCategory };
            var perPart = new List<(string PartName, Dictionary<string, float> Values)>(parts.Count);

            foreach (StockBakePartCore part in parts)
            {
                var values = new Dictionary<string, float>(StringComparer.Ordinal);
                foreach (IStockFieldExtractor ex in extractors)
                {
                    foreach (var (fieldName, value) in ex.Extract(part, ctx))
                    {
                        if (float.IsNaN(value) || float.IsInfinity(value))
                        {
                            continue;
                        }
                        if (string.IsNullOrEmpty(fieldName))
                        {
                            continue;
                        }
                        values[fieldName] = value;
                    }
                }
                perPart.Add((part.Data.partName, values));
            }

            var allFieldNames = new List<string>();
            var seenNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in perPart)
            {
                foreach (string n in entry.Values.Keys)
                {
                    if (seenNames.Add(n))
                    {
                        allFieldNames.Add(n);
                    }
                }
            }
            foreach (string fieldName in allFieldNames)
            {
                var samples = new List<float>(perPart.Count);
                foreach (var entry in perPart)
                {
                    if (entry.Values.TryGetValue(fieldName, out float v))
                    {
                        samples.Add(v);
                    }
                }
                if (samples.Count == 0)
                {
                    continue;
                }
                samples.Sort();
                bucket.Fields.Add(new StockField
                {
                    Name = fieldName,
                    Min = samples[0],
                    Max = samples[samples.Count - 1],
                    Mean = ComputeMean(samples),
                    Median = ComputeMedian(samples),
                    Count = samples.Count,
                });
            }

            foreach (var entry in perPart)
            {
                var partRef = new StockPartRef { PartName = entry.PartName };
                foreach (var kv in entry.Values)
                {
                    partRef.FieldValues.Add(new StockPartFieldValue { Name = kv.Key, Value = kv.Value });
                }
                bucket.ContributingParts.Add(partRef);
            }
            return bucket;
        }

        private static float ComputeMean(List<float> sortedSamples)
        {
            double sum = 0;
            for (int i = 0; i < sortedSamples.Count; i++)
            {
                sum += sortedSamples[i];
            }
            return (float)(sum / sortedSamples.Count);
        }

        private static float ComputeMedian(List<float> sortedSamples)
        {
            int n = sortedSamples.Count;
            if (n == 0)
            {
                return 0f;
            }
            if ((n & 1) == 1)
            {
                return sortedSamples[n / 2];
            }
            return 0.5f * (sortedSamples[n / 2 - 1] + sortedSamples[n / 2]);
        }

        private static void EnsureDirectory(string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(dir) || Directory.Exists(dir))
            {
                return;
            }
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }
    }
}
#endif
