using System;
using System.Collections.Generic;
using System.Globalization;
using KSP;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Stock
{
    /// <summary>
    /// Warns when an authored field falls outside the corresponding stock bucket's
    /// observed range. Threshold is the bucket's literal [Min, Max] — i.e. "what stock
    /// actually does in this family-and-size bucket."
    /// </summary>
    /// <remarks>
    /// Reads from <see cref="StockStatsLookup" /> baked under the package's <c>Assets/</c>
    /// subfolder. One validator class yields multiple issues with distinct per-field codes
    /// so the validation report can filter by field shape. Each issue ships a "Use median"
    /// fix that invokes the field's <see cref="StockFieldEntry.Copier" /> with the bucket's
    /// median, putting the author one click from "snap to typical stock value."
    /// </remarks>
    public sealed class StockOutlierValidator : IPartValidator
    {
        private const string LookupAssetPath = SDKConfiguration.BasePath + "/Assets/StockStats/StockStatsLookup.asset";
        private const string CodePrefix = "STOCK_OUTLIER_";

        private static StockStatsLookup _cachedLookup;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            PartData data = context?.Data;
            CorePartData target = context?.Part;
            if (data == null || target == null || string.IsNullOrEmpty(data.family))
            {
                yield break;
            }

            StockStatsLookup lookup = GetLookup();
            if (lookup == null)
            {
                yield break;
            }

            string sizeCategory = data.sizeCategory.ToString();
            StockBucket bucket = lookup.FindBucket(data.family, sizeCategory);
            if (bucket?.Fields == null)
            {
                yield break;
            }

            foreach (StockField field in bucket.Fields)
            {
                if (field == null || field.Count == 0 || string.IsNullOrEmpty(field.Name))
                {
                    continue;
                }
                if (!ActivePartFieldReader.TryRead(field.Name, target, out float partValue))
                {
                    continue;
                }
                if (partValue >= field.Min && partValue <= field.Max)
                {
                    continue;
                }

                StockFieldEntry entry = StockFieldPaths.Find(field.Name);
                string display = entry?.DisplayName ?? field.Name;
                string units = entry?.UnitsSuffix ?? string.Empty;
                string format = entry?.Format ?? "{0:0.##}";
                string subKey = entry?.SubKey;

                string displayLabel = string.IsNullOrEmpty(subKey)
                    ? display
                    : $"{display} ({subKey})";

                string partFormatted = Format(format, partValue, units);
                string minFormatted = Format(format, field.Min, units);
                string maxFormatted = Format(format, field.Max, units);
                string medianFormatted = Format(format, field.Median, units);
                string partWord = field.Count == 1 ? "part" : "parts";
                string message = $"{displayLabel} = {partFormatted} is outside the stock bucket range " +
                                 $"{minFormatted} to {maxFormatted} " +
                                 $"(median {medianFormatted}, {field.Count} reference {partWord}).";

                ValidationFix[] fixes = null;
                if (entry?.Copier != null)
                {
                    float median = field.Median;
                    StockFieldCopier copier = entry.Copier;
                    string fixLabel = $"Use median ({medianFormatted})";
                    fixes = new[]
                    {
                        new ValidationFix(fixLabel, () =>
                        {
                            if (!copier(target, median, out string err))
                            {
                                Debug.LogError($"[StockOutlierValidator] Use-median fix failed: {err}");
                            }
                        })
                    };
                }

                string code = ComputeCode(field.Name, subKey);
                yield return new ValidationIssue(code, ValidationSeverity.Info, message, fixes);
            }
        }

        private static StockStatsLookup GetLookup()
        {
            if (_cachedLookup != null)
            {
                return _cachedLookup;
            }
            _cachedLookup = AssetDatabase.LoadAssetAtPath<StockStatsLookup>(LookupAssetPath);
            return _cachedLookup;
        }

        private static string Format(string format, float value, string units)
        {
            return string.Format(CultureInfo.InvariantCulture, format ?? "{0:0.##}", value) + (units ?? string.Empty);
        }

        private static string ComputeCode(string fieldName, string subKey)
        {
            string baseName = fieldName;
            if (!string.IsNullOrEmpty(subKey))
            {
                int lastDot = fieldName.LastIndexOf('.');
                if (lastDot > 0)
                {
                    baseName = fieldName.Substring(0, lastDot);
                }
            }
            return CodePrefix + baseName.Replace('.', '_').ToUpperInvariant();
        }
    }
}
