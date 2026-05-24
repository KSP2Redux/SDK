using System;
using System.Collections.Generic;
using KSP.OAB;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats
{
    /// <summary>
    /// Editor-only shipped lookup of base-game part statistics, grouped by (family, sizeCategory).
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
        /// <summary>One bucket per (family, sizeCategory) combination found in the source dump.</summary>
        public List<StockBucket> Buckets = new();

        /// <summary>Cheap staleness fingerprint of the source folder, recomputed on bake.</summary>
        public string SourceHash;

        /// <summary>UTC ISO 8601 stamp recorded when the lookup was baked.</summary>
        public string BakedAt;

        /// <summary>Bumped when the schema changes in a way that invalidates stored entries.</summary>
        public int SchemaVersion = 1;

        /// <summary>Total parts contributing across all buckets, summarised at bake time.</summary>
        public int PartsScanned;

        /// <summary>Returns the bucket matching the given family / size, or null if none exists.</summary>
        public StockBucket FindBucket(string family, string sizeCategory)
        {
            if (Buckets == null)
            {
                return null;
            }
            for (int i = 0; i < Buckets.Count; i++)
            {
                StockBucket bucket = Buckets[i];
                if (bucket != null && bucket.Family == family && bucket.SizeCategory == sizeCategory)
                {
                    return bucket;
                }
            }
            return null;
        }

        /// <summary>
        /// Resolves a (<paramref name="family" />, <paramref name="size" />) query into a
        /// <see cref="BucketResolution" /> with the exact bucket, same-family adjacent buckets, and
        /// a closest-family fallback used when the exact match is missing.
        /// </summary>
        /// <remarks>
        /// Adjacency walks the natural size order (XS- through 6XL) using <see cref="_orderedSizes" />.
        /// The enum's numeric order is not natural size order, so callers must not arithmetic the enum
        /// values directly.
        /// </remarks>
        public BucketResolution ResolveBucket(string family, MetaAssemblySizeFilterType size)
        {
            string sizeKey = size.ToString();
            StockBucket inBucket = FindBucket(family, sizeKey);

            var adjacent = new List<StockBucket>();
            int rank = NaturalSizeRank(size);
            if (rank >= 0)
            {
                if (rank - 1 >= 0)
                {
                    StockBucket below = FindBucket(family, _orderedSizes[rank - 1].ToString());
                    if (below != null)
                    {
                        adjacent.Add(below);
                    }
                }
                if (rank + 1 < _orderedSizes.Length)
                {
                    StockBucket above = FindBucket(family, _orderedSizes[rank + 1].ToString());
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
                fallback.Sort((a, b) => SizeDistance(a, size).CompareTo(SizeDistance(b, size)));
            }

            return new BucketResolution(family, size, inBucket, adjacent, fallback);
        }

        private static readonly MetaAssemblySizeFilterType[] _orderedSizes =
        {
            MetaAssemblySizeFilterType.XSMINUS,
            MetaAssemblySizeFilterType.XS,
            MetaAssemblySizeFilterType.XSPLUS,
            MetaAssemblySizeFilterType.S,
            MetaAssemblySizeFilterType.SPLUS,
            MetaAssemblySizeFilterType.M,
            MetaAssemblySizeFilterType.MPLUS,
            MetaAssemblySizeFilterType.L,
            MetaAssemblySizeFilterType.LPLUS,
            MetaAssemblySizeFilterType.XL,
            MetaAssemblySizeFilterType.XLPLUS,
            MetaAssemblySizeFilterType.XXL,
            MetaAssemblySizeFilterType.XXXL,
            MetaAssemblySizeFilterType.XXXXL,
            MetaAssemblySizeFilterType.XXXXXL,
            MetaAssemblySizeFilterType.XXXXXXL
        };

        private static int NaturalSizeRank(MetaAssemblySizeFilterType size)
        {
            for (int i = 0; i < _orderedSizes.Length; i++)
            {
                if (_orderedSizes[i] == size)
                {
                    return i;
                }
            }
            return -1;
        }

        private static int SizeDistance(StockBucket bucket, MetaAssemblySizeFilterType target)
        {
            int targetRank = NaturalSizeRank(target);
            if (targetRank < 0 || bucket == null)
            {
                return int.MaxValue;
            }
            if (Enum.TryParse(bucket.SizeCategory, ignoreCase: false, out MetaAssemblySizeFilterType actual))
            {
                int actualRank = NaturalSizeRank(actual);
                if (actualRank >= 0)
                {
                    return Math.Abs(actualRank - targetRank);
                }
            }
            return int.MaxValue;
        }
    }

    /// <summary>One stats bucket: all stock parts that share a (family, sizeCategory) key.</summary>
    [Serializable]
    public sealed class StockBucket
    {
        /// <summary>The PartData.family value, e.g. "0100-Methalox".</summary>
        public string Family;

        /// <summary>The PartData.sizeCategory value, e.g. "S".</summary>
        public string SizeCategory;

        /// <summary>Per-tracked-field aggregate stats across the bucket's parts.</summary>
        public List<StockField> Fields = new();

        /// <summary>One entry per stock part in the bucket, with the values it contributed.</summary>
        public List<StockPartRef> ContributingParts = new();

        /// <summary>Returns the field with the given canonical name, or null if not tracked here.</summary>
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
        public string Name;
        public float Value;
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
