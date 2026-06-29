#if REDUX
using System.Collections.Generic;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats
{
    /// <summary>Static list of every field extractor the bake runs.</summary>
    /// <remarks>
    /// Adding a tracked field means adding one entry here. The baker iterates this list per
    /// part, so order here determines column order in <see cref="StockBucket.Fields" />.
    /// Reflection-based auto-discovery is deliberately not used so the list stays explicit
    /// and reviewable.
    /// </remarks>
    internal static class StockStatsExtractorRegistry
    {
        /// <summary>
        /// Builds the ordered list of extractors the bake runs against each part.
        /// </summary>
        /// <returns>A fresh list of extractor instances in invocation order.</returns>
        public static List<IStockFieldExtractor> Create()
        {
            return new List<IStockFieldExtractor>
            {
                // PartData scalars
                new ScalarFieldExtractor(StockFieldNames.Mass, d => d.mass),
                new ScalarFieldExtractor(StockFieldNames.Cost, d => d.cost),
                new ScalarFieldExtractor(StockFieldNames.CrashTolerance, d => d.crashTolerance),
                new ScalarFieldExtractor(StockFieldNames.BreakingForce, d => d.breakingForce),
                new ScalarFieldExtractor(StockFieldNames.BreakingTorque, d => d.breakingTorque),
                new ScalarFieldExtractor(StockFieldNames.ExplosionPotential, d => d.explosionPotential),
                new ScalarFieldExtractor(StockFieldNames.MaxTemp, d => d.maxTemp),
                new ScalarFieldExtractor(StockFieldNames.CrewCapacity, d => d.crewCapacity),
                new ScalarFieldExtractor(StockFieldNames.HeatConductivity, d => d.heatConductivity),
                new ScalarFieldExtractor(StockFieldNames.SkinMaxTemp, d => d.skinMaxTemp),
                new ScalarFieldExtractor(StockFieldNames.MaxLength, d => d.maxLength),
                new ScalarFieldExtractor(StockFieldNames.Buoyancy, d => d.buoyancy),
                new ScalarFieldExtractor(StockFieldNames.AngularDrag, d => d.angularDrag),
                new ScalarFieldExtractor(StockFieldNames.CoMassOffsetX, d => Component(d.coMassOffset, 0)),
                new ScalarFieldExtractor(StockFieldNames.CoMassOffsetY, d => Component(d.coMassOffset, 1)),
                new ScalarFieldExtractor(StockFieldNames.CoMassOffsetZ, d => Component(d.coMassOffset, 2)),
                new ScalarFieldExtractor(StockFieldNames.CoLiftOffsetX, d => Component(d.coLiftOffset, 0)),
                new ScalarFieldExtractor(StockFieldNames.CoLiftOffsetY, d => Component(d.coLiftOffset, 1)),
                new ScalarFieldExtractor(StockFieldNames.CoLiftOffsetZ, d => Component(d.coLiftOffset, 2)),
                new ScalarFieldExtractor(StockFieldNames.CoPressureOffsetX, d => Component(d.coPressureOffset, 0)),
                new ScalarFieldExtractor(StockFieldNames.CoPressureOffsetY, d => Component(d.coPressureOffset, 1)),
                new ScalarFieldExtractor(StockFieldNames.CoPressureOffsetZ, d => Component(d.coPressureOffset, 2)),

                // Engine + paired gimbal
                new EngineMaxThrustExtractor(),
                new EngineIspVacExtractor(),
                new EngineIspSlExtractor(),
                new EngineFuelFlowExtractor(),
                new GimbalExtractor(),

                // Reaction wheel
                new ReactionWheelExtractor(),

                // Command (pod / probe core EC requirement)
                new CommandExtractor(),

                // Science experiments (per-experiment presence flag)
                new ScienceExperimentExtractor(),

                // Resource intake (jet engine air intake)
                new ResourceIntakeExtractor(),

                // Lifting surface (wings, tail fins)
                new LiftingSurfaceExtractor(),

                // Heatshield
                new HeatshieldExtractor(),

                // Active radiator
                new ActiveRadiatorExtractor(),

                // Cargo bay
                new CargoBayExtractor(),

                // Decoupler
                new DecoupleExtractor(),

                // Tank
                new TankCapacityExtractor(),
                new TankResourcePercentExtractor(),

                // RCS
                new RcsThrustExtractor(),

                // Solar
                new SolarPanelExtractor(),

                // Antenna
                new AntennaExtractor(),

                // Parachute
                new ParachuteExtractor(),

                // Generator / converter
                new GeneratorExtractor(),
                new ConverterExtractor(),

                // Wheel
                new WheelBrakeTorqueExtractor(),
                new WheelSuspensionExtractor(),

                // Docking
                new DockingNodeExtractor(),

                // Control surface
                new ControlSurfaceExtractor(),
            };
        }

        private static float Component(Vector3Mirror vector, int axis)
        {
            if (vector == null)
            {
                return 0f;
            }
            return axis switch
            {
                0 => vector.x,
                1 => vector.y,
                _ => vector.z
            };
        }
    }
}
#endif
