using System;
using System.Collections.Generic;
using KSP.OAB;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats
{
    /// <summary>
    /// Editor-only shipped lookup of base-game part statistics, grouped by (family, sizeKey).
    /// </summary>
    /// <remarks>
    /// Produced by <see cref="StockStatsBaker" /> scanning the base-game asset dump and consumed
    /// by ReferencePartsWindow and outlier validators. Lives in the package's <c>Assets/</c>
    /// subfolder and is loaded by editor code via <see cref="AssetDatabase.LoadAssetAtPath" />.
    /// The type sits in the editor assembly so it cannot be referenced at runtime regardless of
    /// where the .asset file lives on disk.
    /// </remarks>
    public sealed class StockStatsLookup : ScriptableObject
    {
        /// <summary>One bucket per (family, sizeKey) combination found in the source dump.</summary>
        public List<StockBucket> Buckets = new();

        /// <summary>Recipe-resolved mass per unit per resource, captured at bake time. Empty on legacy assets.</summary>
        public List<ResourceMassEntry> ResourceMasses = new();

        /// <summary>Cheap staleness fingerprint of the source folder, recomputed on bake.</summary>
        public string SourceHash;

        /// <summary>UTC ISO 8601 stamp recorded when the lookup was baked.</summary>
        public string BakedAt;

        /// <summary>Bumped when the schema changes in a way that invalidates stored entries.</summary>
        public int SchemaVersion = 3;

        /// <summary>Total parts contributing across all buckets, summarised at bake time.</summary>
        public int PartsScanned;

        /// <summary>Looks up the resource's recipe-resolved mass per unit. Returns false if the bake didn't resolve it (or the asset predates the schema).</summary>
        /// <param name="resourceName">Resource name to look up.</param>
        /// <param name="massPerUnit">Receives the mass per unit when the lookup succeeds, otherwise zero.</param>
        /// <returns>True if the resource mass is known, false otherwise.</returns>
        public bool TryGetResourceMass(string resourceName, out float massPerUnit)
        {
            massPerUnit = 0f;
            if (string.IsNullOrEmpty(resourceName) || ResourceMasses == null)
            {
                return false;
            }
            for (int i = 0; i < ResourceMasses.Count; i++)
            {
                ResourceMassEntry entry = ResourceMasses[i];
                if (entry != null && string.Equals(entry.Name, resourceName, StringComparison.OrdinalIgnoreCase))
                {
                    massPerUnit = entry.MassPerUnit;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Returns the bucket matching the given family / size, or null if none exists.</summary>
        /// <param name="family">Family value to match.</param>
        /// <param name="sizeCategory">Size key value to match.</param>
        /// <returns>The matching bucket, or null when none exists.</returns>
        public StockBucket FindBucket(string family, string sizeCategory)
        {
            if (Buckets == null)
            {
                return null;
            }
            string normalizedSizeKey = NormalizeSizeKey(sizeCategory);
            for (int i = 0; i < Buckets.Count; i++)
            {
                StockBucket bucket = Buckets[i];
                if (bucket != null &&
                    bucket.Family == family &&
                    string.Equals(NormalizeSizeKey(bucket.SizeCategory), normalizedSizeKey, StringComparison.OrdinalIgnoreCase))
                {
                    return bucket;
                }
            }
            return null;
        }

        /// <summary>
        /// Resolves a (<paramref name="family" />, <paramref name="sizeKey" />) query into a
        /// <see cref="BucketResolution" /> with the exact bucket, same-family adjacent buckets, and
        /// a closest-family fallback used when the exact match is missing.
        /// </summary>
        /// <remarks>
        /// Adjacency walks the natural size order (XS- through 6XL) using <c>_orderedSizeKeys</c>.
        /// </remarks>
        /// <param name="family">Family to look up.</param>
        /// <param name="sizeKey">Size key to look up.</param>
        /// <returns>A populated resolution describing the in-bucket, adjacent, and fallback rows.</returns>
        public BucketResolution ResolveBucket(string family, string sizeKey, float interpolationT = float.NaN)
        {
            sizeKey = NormalizeSizeKey(sizeKey);
            StockBucket inBucket = FindBucket(family, sizeKey);

            var adjacent = new List<StockBucket>();
            int rank = NaturalSizeRank(sizeKey);
            if (rank >= 0)
            {
                if (rank - 1 >= 0)
                {
                    StockBucket below = FindBucket(family, _orderedSizeKeys[rank - 1]);
                    if (below != null)
                    {
                        adjacent.Add(below);
                    }
                }
                if (rank + 1 < _orderedSizeKeys.Length)
                {
                    StockBucket above = FindBucket(family, _orderedSizeKeys[rank + 1]);
                    if (above != null)
                    {
                        adjacent.Add(above);
                    }
                }
            }

            var fallback = new List<StockBucket>();
            if (inBucket == null && Buckets != null)
            {
                for (int i = 0; i < Buckets.Count; i++)
                {
                    StockBucket b = Buckets[i];
                    if (b != null && b.Family == family)
                    {
                        fallback.Add(b);
                    }
                }
                fallback.Sort((a, b) => SizeDistance(a, sizeKey).CompareTo(SizeDistance(b, sizeKey)));
            }

            StockBucket interpolationLower = null;
            StockBucket interpolationUpper = null;
            StockBucket interpolated = null;
            float resolvedInterpolationT = 0f;
            if (inBucket == null &&
                TryFindInterpolationBounds(family, sizeKey, out interpolationLower, out interpolationUpper, out float naturalT))
            {
                resolvedInterpolationT = float.IsNaN(interpolationT) ? naturalT : Mathf.Clamp01(interpolationT);
                interpolated = BuildInterpolatedBucket(family, sizeKey, interpolationLower, interpolationUpper, resolvedInterpolationT);
            }

            return new BucketResolution(
                family,
                sizeKey,
                inBucket,
                adjacent,
                fallback,
                interpolated,
                interpolationLower,
                interpolationUpper,
                resolvedInterpolationT);
        }

        /// <summary>
        /// Finds the nearest lower and upper same-family stock buckets around <paramref name="sizeKey" />.
        /// </summary>
        /// <param name="family">Family to search.</param>
        /// <param name="sizeKey">Target size key.</param>
        /// <param name="lower">Nearest lower-size bucket when found.</param>
        /// <param name="upper">Nearest upper-size bucket when found.</param>
        /// <param name="naturalT">Target size's natural rank position between lower and upper buckets.</param>
        /// <returns>True when both bounds exist.</returns>
        public bool TryFindInterpolationBounds(
            string family,
            string sizeKey,
            out StockBucket lower,
            out StockBucket upper,
            out float naturalT)
        {
            lower = null;
            upper = null;
            naturalT = 0f;
            int targetRank = NaturalSizeRank(sizeKey);
            if (targetRank < 0 || Buckets == null)
            {
                return false;
            }

            int lowerRank = int.MinValue;
            int upperRank = int.MaxValue;
            for (int i = 0; i < Buckets.Count; i++)
            {
                StockBucket bucket = Buckets[i];
                if (bucket == null || bucket.Family != family)
                {
                    continue;
                }
                int bucketRank = BucketRank(bucket);
                if (bucketRank < 0 || bucketRank == targetRank)
                {
                    continue;
                }
                if (bucketRank < targetRank && bucketRank > lowerRank)
                {
                    lowerRank = bucketRank;
                    lower = bucket;
                }
                else if (bucketRank > targetRank && bucketRank < upperRank)
                {
                    upperRank = bucketRank;
                    upper = bucket;
                }
            }

            if (lower == null || upper == null || upperRank <= lowerRank)
            {
                return false;
            }
            naturalT = Mathf.InverseLerp(lowerRank, upperRank, targetRank);
            return true;
        }

        /// <summary>
        /// Builds a synthetic bucket by interpolating matching field medians from two stock buckets.
        /// </summary>
        /// <param name="family">Family to write on the synthetic bucket.</param>
        /// <param name="sizeKey">Target size key represented by the synthetic bucket.</param>
        /// <param name="lower">Lower stock-size bucket.</param>
        /// <param name="upper">Upper stock-size bucket.</param>
        /// <param name="t">Interpolation position, 0 at lower and 1 at upper.</param>
        /// <returns>A synthetic bucket, or null if no matching fields can be interpolated.</returns>
        public static StockBucket BuildInterpolatedBucket(
            string family,
            string sizeKey,
            StockBucket lower,
            StockBucket upper,
            float t)
        {
            if (lower?.Fields == null || upper?.Fields == null)
            {
                return null;
            }

            t = Mathf.Clamp01(t);
            var upperFields = new Dictionary<string, StockField>(StringComparer.Ordinal);
            for (int i = 0; i < upper.Fields.Count; i++)
            {
                StockField field = upper.Fields[i];
                if (field != null && field.Count > 0 && !string.IsNullOrEmpty(field.Name))
                {
                    upperFields[field.Name] = field;
                }
            }

            var fields = new List<StockField>();
            for (int i = 0; i < lower.Fields.Count; i++)
            {
                StockField lowerField = lower.Fields[i];
                if (lowerField == null || lowerField.Count == 0 || string.IsNullOrEmpty(lowerField.Name))
                {
                    continue;
                }
                if (!upperFields.TryGetValue(lowerField.Name, out StockField upperField))
                {
                    continue;
                }
                fields.Add(new StockField
                {
                    Name = lowerField.Name,
                    Min = Mathf.Lerp(lowerField.Min, upperField.Min, t),
                    Max = Mathf.Lerp(lowerField.Max, upperField.Max, t),
                    Mean = Mathf.Lerp(lowerField.Mean, upperField.Mean, t),
                    Median = Mathf.Lerp(lowerField.Median, upperField.Median, t),
                    Count = lowerField.Count + upperField.Count
                });
            }

            if (fields.Count == 0)
            {
                return null;
            }
            return new StockBucket
            {
                Family = family,
                SizeCategory = NormalizeSizeKey(sizeKey),
                Fields = fields,
                ContributingParts = new List<StockPartRef>()
            };
        }

        private static readonly string[] _orderedSizeKeys =
        {
            PartSizeRegistry.XsMinus,
            PartSizeRegistry.Xs,
            PartSizeRegistry.XsPlus,
            PartSizeRegistry.Sm,
            PartSizeRegistry.SmPlus,
            PartSizeRegistry.Md,
            PartSizeRegistry.MdPlus,
            PartSizeRegistry.Lg,
            PartSizeRegistry.LgPlus,
            PartSizeRegistry.Xl,
            PartSizeRegistry.XlPlus,
            PartSizeRegistry.TwoXl,
            PartSizeRegistry.ThreeXl,
            PartSizeRegistry.FourXl,
            PartSizeRegistry.FiveXl,
            PartSizeRegistry.SixXl
        };

        private static int NaturalSizeRank(string sizeKey)
        {
            string normalized = NormalizeSizeKey(sizeKey);
            for (int i = 0; i < _orderedSizeKeys.Length; i++)
            {
                if (string.Equals(_orderedSizeKeys[i], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private static int SizeDistance(StockBucket bucket, string target)
        {
            int targetRank = NaturalSizeRank(target);
            if (targetRank < 0 || bucket == null)
            {
                return int.MaxValue;
            }
            int actualRank = BucketRank(bucket);
            if (actualRank >= 0)
            {
                return Math.Abs(actualRank - targetRank);
            }
            return int.MaxValue;
        }

        private static int BucketRank(StockBucket bucket)
        {
            if (bucket == null)
            {
                return -1;
            }
            return NaturalSizeRank(bucket.SizeCategory);
        }

        public static string NormalizeSizeKey(string sizeKey)
        {
            if (string.IsNullOrWhiteSpace(sizeKey))
            {
                return PartSizeRegistry.DefaultSizeKey;
            }

            string trimmed = sizeKey.Trim();
            if (PartSizeRegistry.TryGet(trimmed, out PartSizeDefinition definition))
            {
                return definition.Key;
            }

            return trimmed switch
            {
                "XSMINUS" => PartSizeRegistry.XsMinus,
                "XSPLUS" => PartSizeRegistry.XsPlus,
                "S" => PartSizeRegistry.Sm,
                "SPLUS" => PartSizeRegistry.SmPlus,
                "M" => PartSizeRegistry.Md,
                "MPLUS" => PartSizeRegistry.MdPlus,
                "L" => PartSizeRegistry.Lg,
                "LPLUS" => PartSizeRegistry.LgPlus,
                "XXL" => PartSizeRegistry.TwoXl,
                "XXXL" => PartSizeRegistry.ThreeXl,
                "XXXXL" => PartSizeRegistry.FourXl,
                "XXXXXL" => PartSizeRegistry.FiveXl,
                "XXXXXXL" => PartSizeRegistry.SixXl,
                _ => trimmed
            };
        }
    }

    /// <summary>One stats bucket: all stock parts that share a (family, sizeKey) key.</summary>
    [Serializable]
    public sealed class StockBucket
    {
        /// <summary>The PartData.family value, e.g. "0100-Methalox".</summary>
        public string Family;

        /// <summary>The PartData.sizeKey value, e.g. "SM".</summary>
        public string SizeCategory;

        /// <summary>Per-tracked-field aggregate stats across the bucket's parts.</summary>
        public List<StockField> Fields = new();

        /// <summary>One entry per stock part in the bucket, with the values it contributed.</summary>
        public List<StockPartRef> ContributingParts = new();

        /// <summary>Returns the field with the given canonical name, or null if not tracked here.</summary>
        /// <param name="name">Canonical field name.</param>
        /// <returns>The matching field, or null when not tracked in this bucket.</returns>
        public StockField FindField(string name)
        {
            if (Fields == null)
            {
                return null;
            }
            for (int i = 0; i < Fields.Count; i++)
            {
                StockField field = Fields[i];
                if (field != null && field.Name == name)
                {
                    return field;
                }
            }
            return null;
        }
    }

    /// <summary>One tracked field's aggregate stats across the bucket.</summary>
    [Serializable]
    public sealed class StockField
    {
        /// <summary>Canonical name from <see cref="StockFieldNames" />.</summary>
        public string Name;

        /// <summary>Minimum value among contributing parts.</summary>
        public float Min;

        /// <summary>Maximum value among contributing parts.</summary>
        public float Max;

        /// <summary>Arithmetic mean across contributing parts.</summary>
        public float Mean;

        /// <summary>Median across contributing parts.</summary>
        public float Median;

        /// <summary>Number of parts that supplied a value (parts missing this field are skipped).</summary>
        public int Count;
    }

    /// <summary>One stock part's contribution to the bucket, with the values it provided.</summary>
    [Serializable]
    public sealed class StockPartRef
    {
        /// <summary>The PartData.partName value, e.g. "engine_1v_methalox_terrier".</summary>
        public string PartName;

        /// <summary>The values this part contributed, keyed by canonical field name.</summary>
        public List<StockPartFieldValue> FieldValues = new();

        /// <summary>Returns the value this part contributed for <paramref name="fieldName" />, or NaN if absent.</summary>
        /// <param name="fieldName">Canonical field name.</param>
        /// <returns>The contributed value, or <see cref="float.NaN" /> when this part did not supply the field.</returns>
        public float GetValue(string fieldName)
        {
            if (FieldValues == null)
            {
                return float.NaN;
            }
            for (int i = 0; i < FieldValues.Count; i++)
            {
                StockPartFieldValue entry = FieldValues[i];
                if (entry != null && entry.Name == fieldName)
                {
                    return entry.Value;
                }
            }
            return float.NaN;
        }
    }

    /// <summary>One field-name to field-value entry on a per-part record.</summary>
    [Serializable]
    public sealed class StockPartFieldValue
    {
        /// <summary>Canonical field name.</summary>
        public string Name;
        /// <summary>Value contributed for this field.</summary>
        public float Value;
    }

    /// <summary>Recipe-resolved mass per unit for one resource. Used by derived-value computations against live parts.</summary>
    [Serializable]
    public sealed class ResourceMassEntry
    {
        /// <summary>Resource name.</summary>
        public string Name;
        /// <summary>Mass per unit, recipe-resolved against the raw resources.</summary>
        public float MassPerUnit;
    }

    /// <summary>Canonical names for tracked stat fields. Use these when constructing or querying entries.</summary>
    public static class StockFieldNames
    {
        // PartData scalars
        public const string Mass = "mass";
        public const string Cost = "cost";
        public const string CrashTolerance = "crashTolerance";
        public const string BreakingForce = "breakingForce";
        public const string BreakingTorque = "breakingTorque";
        public const string ExplosionPotential = "explosionPotential";
        public const string MaxTemp = "maxTemp";
        public const string CrewCapacity = "crewCapacity";
        public const string HeatConductivity = "heatConductivity";
        public const string SkinMaxTemp = "skinMaxTemp";
        public const string MaxLength = "maxLength";
        public const string Buoyancy = "buoyancy";
        public const string AngularDrag = "angularDrag";
        public const string CoMassOffsetX = "coMassOffset.x";
        public const string CoMassOffsetY = "coMassOffset.y";
        public const string CoMassOffsetZ = "coMassOffset.z";
        public const string CoLiftOffsetX = "coLiftOffset.x";
        public const string CoLiftOffsetY = "coLiftOffset.y";
        public const string CoLiftOffsetZ = "coLiftOffset.z";
        public const string CoPressureOffsetX = "coPressureOffset.x";
        public const string CoPressureOffsetY = "coPressureOffset.y";
        public const string CoPressureOffsetZ = "coPressureOffset.z";

        // Engine
        public const string EngineMaxThrust = "engine.maxThrust";
        public const string EngineIspVac = "engine.isp_vac";
        public const string EngineIspSl = "engine.isp_sl";
        public const string EngineFuelFlow = "engine.fuelFlow";
        public const string EngineGimbalRange = "engine.gimbalRange";
        public const string EngineGimbalResponseSpeed = "engine.gimbalResponseSpeed";

        // Reaction wheel
        public const string ReactionWheelPitchTorque = "reactionWheel.pitchTorque";
        public const string ReactionWheelYawTorque = "reactionWheel.yawTorque";
        public const string ReactionWheelRollTorque = "reactionWheel.rollTorque";
        public const string ReactionWheelEcRate = "reactionWheel.ecRate";

        // Command (pod / probe core)
        public const string CommandEcRate = "command.ecRate";

        // Science experiment presence flag (dynamic: "science.experiment.{experimentId}")
        public const string ScienceExperiment = "science.experiment";

        // Intake (jet engine air intake)
        public const string IntakeArea = "intake.area";
        public const string IntakeSpeed = "intake.intakeSpeed";

        // Lifting surface (wings, tail fins)
        public const string LiftSurfaceDeflectionCoeff = "liftSurface.deflectionLiftCoeff";

        // Heatshield (ablator)
        public const string HeatshieldAblationTempThreshold = "heatshield.ablationTempThreshold";
        public const string HeatshieldAblationMaxOverThreshold = "heatshield.ablationMaxOverThreshold";
        public const string HeatshieldPyrolysisLossFactor = "heatshield.pyrolysisLossFactor";
        public const string HeatshieldShieldingScale = "heatshield.shieldingScale";

        // Radiator (active heat reject)
        public const string RadiatorFluxPerAreaUnit = "radiator.fluxPerAreaUnit";

        // Cargo bay
        public const string CargoBayInternalLength = "cargoBay.internalLength";
        public const string CargoBayLookUpRadius = "cargoBay.lookUpRadius";

        // Decoupler
        public const string DecoupleEjectionForce = "decouple.ejectionForce";

        // Tank / containers
        public const string TankCapacity = "tank.capacity";
        public const string TankResourcePercent = "tank.resourcePercent";

        // RCS
        public const string RcsThrust = "rcs.thrust";

        // Solar
        public const string SolarPeakOutput = "solar.peakOutput";

        // Antenna / transmitter
        public const string AntennaRange = "antenna.range";
        public const string AntennaDataRate = "antenna.dataRate";

        // Parachute
        public const string ParachuteDeployAltitude = "parachute.deployAltitude";
        public const string ParachuteAreaDeployed = "parachute.areaDeployed";
        public const string ParachuteMinPressureToOpen = "parachute.minPressureToOpen";
        public const string ParachuteMaxTemp = "parachute.maxTemp";

        // Generator / converter
        public const string GeneratorOutput = "generator.output";
        public const string ConverterConversionRate = "converter.conversionRate";

        // Wheel
        public const string WheelMaxBrakeTorque = "wheel.maxBrakeTorque";
        public const string WheelSuspensionDistance = "wheel.suspensionDistance";

        // Docking
        public const string DockingAcquireRange = "docking.acquireRange";
        public const string DockingAcquireForce = "docking.acquireForce";
        public const string DockingCaptureRange = "docking.captureRange";

        // Control surface
        public const string ControlSurfaceRange = "controlSurface.range";
        public const string ControlSurfaceArea = "controlSurface.area";
        public const string ControlSurfaceActuatorSpeed = "controlSurface.actuatorSpeed";
    }
}
