using System.Collections.Generic;
using KSP;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Performance
{
    /// <summary>
    /// Warns when texture VRAM for a body approaches or exceeds the 2 GB budget.
    /// </summary>
    /// <remarks>
    /// Both thresholds are warnings. Exceeding the budget does not break loading. It just makes the body unusually
    /// heavy compared to stock bodies. Sizes come from
    /// <see cref="UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong" />, so the figure is a resident upper
    /// bound when streaming mip-maps are enabled. The breakdown panel in <see cref="Windows.ValidationReportWindow" />
    /// surfaces top contributors for the artist to act on. Expensive validator: walks every texture reachable from
    /// the body's data model.
    /// </remarks>
    public sealed class VramBudgetValidator : IPlanetValidator
    {
        /// <summary>Stable code emitted when VRAM exceeds the budget.</summary>
        public const string CodeOver = "VRAM_OVER_BUDGET";

        /// <summary>Stable code emitted when VRAM is within the soft-warning band of the budget.</summary>
        public const string CodeNear = "VRAM_NEAR_BUDGET";

        /// <summary>Hard budget in bytes (2 GB).</summary>
        public const long BudgetBytes = 2L * 1024L * 1024L * 1024L;

        /// <summary>Soft-warning band, expressed as a fraction of <see cref="BudgetBytes" />.</summary>
        public const double NearBudgetFraction = 0.8;

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;

            TextureBudgetEnumerator calc = TextureBudgetEnumerator.Compute(body);
            long total = calc.TotalBytes;
            if (total <= 0) yield break;

            string totals = $"{TextureBudgetEnumerator.FormatSize(total)} across {calc.Entries.Count} texture{(calc.Entries.Count == 1 ? string.Empty : "s")}";

            if (total >= BudgetBytes)
            {
                yield return new ValidationIssue(
                    CodeOver,
                    ValidationSeverity.Warning,
                    $"Texture VRAM exceeds the 2 GB budget ({totals}). Open the VRAM breakdown panel for per-category and per-texture detail.");
                yield break;
            }
            if (total >= (long)(BudgetBytes * NearBudgetFraction))
            {
                yield return new ValidationIssue(
                    CodeNear,
                    ValidationSeverity.Warning,
                    $"Texture VRAM is within 80% of the 2 GB budget ({totals}). Open the VRAM breakdown panel for per-category and per-texture detail.");
            }
        }
    }
}
