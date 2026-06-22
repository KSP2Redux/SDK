using System;
using System.Collections.Generic;
using KSP;
using KSP.Modules;
using KSP.OAB;
using KSP.Sim.Definitions;
using KSP.Sim.ResourceSystem;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Wizards
{
    /// <summary>
    /// Convenience base for archetypes - implements <see cref="IPartArchetype" /> with abstract
    /// property hooks and protected helpers for the most common seeding patterns.
    /// </summary>
    /// <remarks>
    /// Concrete archetypes are free to implement <see cref="IPartArchetype" /> directly if they
    /// prefer, but inheriting cuts the boilerplate of finding a usable bucket, reading per-field
    /// medians, and walking <c>part.Core.modules</c> to find a <c>Data_*</c> instance.
    /// </remarks>
    public abstract class PartArchetypeBase : IPartArchetype
    {
        /// <inheritdoc />
        public abstract string Category { get; }

        /// <inheritdoc />
        public abstract string Family { get; }

        /// <inheritdoc />
        public abstract string DisplayName { get; }

        /// <inheritdoc />
        public abstract string Description { get; }

        /// <inheritdoc />
        public abstract MetaAssemblySizeFilterType DefaultSize { get; }

        /// <inheritdoc />
        public abstract IReadOnlyList<Type> DefaultModules { get; }

        /// <inheritdoc />
        public abstract IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes { get; }

        /// <inheritdoc />
        public abstract void SeedDefaults(CorePartData part, BucketResolution bucket);

        /// <summary>
        /// Returns the first bucket that should drive seeded defaults: the exact in-bucket match if
        /// present, otherwise the closest family fallback, otherwise null.
        /// </summary>
        protected static StockBucket? FindFirstUsableBucket(BucketResolution bucket)
        {
            if (bucket == null)
            {
                return null;
            }
            if (bucket.InBucket != null)
            {
                return bucket.InBucket;
            }
            return bucket.FamilyFallback is { Count: > 0 } ? bucket.FamilyFallback[0] : null;
        }

        /// <summary>
        /// Reads the median for <paramref name="fieldName" /> from <paramref name="source" /> and
        /// passes it to <paramref name="apply" /> when present. No-op when the field is missing
        /// or the bucket has no contributing parts for it.
        /// </summary>
        protected static void TrySeedScalar(StockBucket source, string fieldName, Action<float> apply)
        {
            if (source == null || apply == null)
            {
                return;
            }
            StockField field = source.FindField(fieldName);
            if (field != null && field.Count > 0)
            {
                apply(field.Median);
            }
        }

        /// <summary>
        /// Integer variant of <see cref="TrySeedScalar" />, rounds the median to nearest int.
        /// </summary>
        protected static void TrySeedScalarInt(StockBucket source, string fieldName, Action<int> apply)
        {
            if (source == null || apply == null)
            {
                return;
            }
            StockField field = source.FindField(fieldName);
            if (field != null && field.Count > 0)
            {
                apply((int)Math.Round(field.Median));
            }
        }

        /// <summary>
        /// Finds the <typeparamref name="T" /> module-data instance on the part's
        /// <see cref="PartBehaviourModule" /> components, or null if no module owns one.
        /// </summary>
        /// <remarks>
        /// Walks every <see cref="PartBehaviourModule" /> on the part's GameObject and inspects
        /// its <c>DataModules</c> dictionary. <c>PartCore.modules</c> is only populated by the
        /// runtime JSON loader; at scaffold time it is empty.
        /// </remarks>
        protected static T FindModuleData<T>(CorePartData part) where T : class
        {
            if (part == null || part.gameObject == null) return null;
            foreach (var module in part.gameObject.GetComponents<PartBehaviourModule>())
            {
                if (module == null || module.DataModules == null) continue;
                foreach (var data in module.DataModules.Values)
                {
                    if (data is T match) return match;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds the ElectricCharge entry in <paramref name="list" /> and updates its rate,
        /// or appends a new entry if none exists. No-op when <paramref name="list" /> is null.
        /// </summary>
        protected static void UpsertElectricChargeRate(List<PartModuleResourceSetting> list, float rate)
        {
            if (list == null)
            {
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].ResourceName == "ElectricCharge")
                {
                    PartModuleResourceSetting updated = list[i];
                    updated.Rate = rate;
                    list[i] = updated;
                    return;
                }
            }
            list.Add(new PartModuleResourceSetting
            {
                ResourceName = "ElectricCharge",
                Rate = rate
            });
        }

        /// <summary>
        /// Seeds the four PartData scalars every tank archetype tunes (mass, cost, crashTolerance,
        /// maxTemp) from the bucket's medians and appends a single resource container.
        /// </summary>
        /// <remarks>
        /// Standard shape used by Methalox, Monoprop, Hydrogen, Xenon, and similar fuel tanks. The
        /// container's capacity comes from the bucket's <c>tank.capacity.{resourceName}</c> field
        /// when present, otherwise <paramref name="defaultCapacity" />. No-op when the part has no
        /// PartData or the bucket has no usable source.
        /// </remarks>
        protected static void SeedTankDefaults(
            CorePartData part,
            BucketResolution bucket,
            string resourceName,
            float defaultCapacity)
        {
            if (part?.Data == null) return;
            var source = FindFirstUsableBucket(bucket);
            if (source == null) return;

            var data = part.Data;
            TrySeedScalar(source, StockFieldNames.Mass, v => data.mass = v);
            TrySeedScalarInt(source, StockFieldNames.Cost, v => data.cost = v);
            TrySeedScalar(source, StockFieldNames.CrashTolerance, v => data.crashTolerance = v);
            TrySeedScalar(source, StockFieldNames.MaxTemp, v => data.maxTemp = v);

            AddResourceContainer(data, source, resourceName, defaultCapacity,
                capacityFieldName: StockFieldNames.TankCapacity + "." + resourceName);
        }

        /// <summary>
        /// Appends a <see cref="ContainedResourceDefinition" /> for <paramref name="resourceName" />
        /// to <paramref name="data" />'s <c>resourceContainers</c>, pulling the capacity from the
        /// bucket's <paramref name="capacityFieldName" /> field when present.
        /// </summary>
        protected static void AddResourceContainer(
            PartData data,
            StockBucket source,
            string resourceName,
            float defaultCapacity,
            string capacityFieldName)
        {
            if (data?.resourceContainers == null)
            {
                return;
            }
            for (int i = 0; i < data.resourceContainers.Count; i++)
            {
                if (data.resourceContainers[i]?.name == resourceName)
                {
                    return;
                }
            }
            float capacity = defaultCapacity;
            StockField field = source?.FindField(capacityFieldName);
            if (field != null && field.Count > 0)
            {
                capacity = field.Median;
            }
            data.resourceContainers.Add(new ContainedResourceDefinition(capacity, resourceName));
        }

        /// <summary>
        /// Seeds an engine mode's ISP curve with two keyframes pulled from the bucket's
        /// <c>engine.isp_vac.{propellant}</c> and <c>engine.isp_sl.{propellant}</c> fields. No-op
        /// when neither bucket field exists or the engine has no atmosphereCurve.
        /// </summary>
        protected static void SeedEngineModeIsp(Data_Engine.EngineMode mode, StockBucket source, string propellant)
        {
            if (mode?.atmosphereCurve?.Curve == null || source == null || string.IsNullOrEmpty(propellant))
            {
                return;
            }
            StockField vac = source.FindField(StockFieldNames.EngineIspVac + "." + propellant);
            StockField sl = source.FindField(StockFieldNames.EngineIspSl + "." + propellant);
            bool hasVac = vac != null && vac.Count > 0;
            bool hasSl = sl != null && sl.Count > 0;
            if (!hasVac && !hasSl)
            {
                return;
            }
            float ispVac = hasVac ? vac.Median : 0f;
            float ispSl = hasSl ? sl.Median : 0f;
            mode.atmosphereCurve.Curve.keys = new[]
            {
                new Keyframe(0f, ispVac),
                new Keyframe(1f, ispSl)
            };
        }

        /// <summary>
        /// Seeds a Module_Generator's <c>ResourceSetting</c> with EC output rate from the bucket's
        /// <c>generator.output.ElectricCharge</c> field. No-op when the bucket has no data for it.
        /// </summary>
        protected static void SeedAlternatorOutput(Data_ModuleGenerator alt, StockBucket source)
        {
            if (alt == null || source == null)
            {
                return;
            }
            StockField field = source.FindField(StockFieldNames.GeneratorOutput + ".ElectricCharge");
            if (field == null || field.Count == 0)
            {
                return;
            }
            PartModuleResourceSetting rs = alt.ResourceSetting;
            rs.ResourceName = "ElectricCharge";
            rs.Rate = field.Median;
            alt.ResourceSetting = rs;
        }

        /// <summary>
        /// Collects per-experiment property fields from <paramref name="source" /> (prefix
        /// <c>science.experiment.{id}.{property}</c>) and appends fully-configured
        /// <see cref="ExperimentConfiguration" /> entries to <paramref name="data" />.Experiments.
        /// Skips experiments already on the list.
        /// </summary>
        /// <remarks>
        /// Each tracked experiment in the bucket contributes three fields: timeToComplete,
        /// crewRequired, ecRate. The archetype reads the bucket's median for each, builds an
        /// ExperimentConfiguration, and adds it. Experiments with no stock parts in the bucket
        /// have <c>Count = 0</c> for all three fields and are skipped.
        /// </remarks>
        protected static void SeedScienceExperimentsFromBucket(Data_ScienceExperiment data, StockBucket source)
        {
            if (data?.Experiments == null || source?.Fields == null)
            {
                return;
            }
            string prefix = StockFieldNames.ScienceExperiment + ".";
            var byExpId = new Dictionary<string, (StockField Time, StockField Crew, StockField Ec)>();
            foreach (StockField field in source.Fields)
            {
                if (field?.Name == null || !field.Name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }
                if (field.Count == 0)
                {
                    continue;
                }
                string tail = field.Name.Substring(prefix.Length);
                int dotIdx = tail.IndexOf('.');
                if (dotIdx <= 0 || dotIdx >= tail.Length - 1)
                {
                    continue;
                }
                string expId = tail.Substring(0, dotIdx);
                string prop = tail.Substring(dotIdx + 1);
                byExpId.TryGetValue(expId, out var tuple);
                switch (prop)
                {
                    case "timeToComplete": tuple.Time = field; break;
                    case "crewRequired": tuple.Crew = field; break;
                    case "ecRate": tuple.Ec = field; break;
                }
                byExpId[expId] = tuple;
            }

            foreach (var kvp in byExpId)
            {
                string expId = kvp.Key;
                bool already = false;
                for (int i = 0; i < data.Experiments.Count; i++)
                {
                    if (string.Equals(data.Experiments[i].ExperimentDefinitionID, expId, StringComparison.Ordinal))
                    {
                        already = true;
                        break;
                    }
                }
                if (already)
                {
                    continue;
                }
                var (time, crew, ec) = kvp.Value;
                var config = new ExperimentConfiguration { ExperimentDefinitionID = expId };
                if (time != null)
                {
                    config.TimeToComplete = time.Median;
                }
                if (crew != null)
                {
                    config.CrewRequired = (int)Math.Round(crew.Median);
                }
                if (ec != null && ec.Median > 0f)
                {
                    config.ResourcesCost = new List<PartModuleResourceSetting>
                    {
                        new PartModuleResourceSetting { ResourceName = "ElectricCharge", Rate = ec.Median }
                    };
                }
                data.Experiments.Add(config);
            }
        }
    }
}
