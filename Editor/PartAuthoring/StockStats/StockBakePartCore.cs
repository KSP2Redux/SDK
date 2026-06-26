#if REDUX
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats
{
    /// <summary>JSON mirror of the part .bytes envelope read by the bake.</summary>
    /// <remarks>
    /// Only the fields the bake needs are modelled. Newtonsoft silently drops unknown JSON keys
    /// and leaves missing fields at default, so the mirror stays loose against schema churn in
    /// the source dump. Lowercase field names match the JSON keys verbatim.
    /// </remarks>
    internal sealed class StockBakePartCore
    {
        [JsonProperty("data")] public PartDataMirror Data;
    }

    internal sealed class PartDataMirror
    {
        public string partName;
        public string family;
        public string sizeKey;
        public string sizeCategory;
        public float mass;
        public float cost;
        public float crashTolerance;
        public float breakingForce;
        public float breakingTorque;
        public float explosionPotential;
        public float maxTemp;
        public float crewCapacity;
        public float heatConductivity;
        public float skinMaxTemp;
        public float maxLength;
        public float buoyancy;
        public List<ResourceContainerMirror> resourceContainers;
        public List<ModuleEnvelopeMirror> serializedPartModules;
    }

    internal sealed class ResourceContainerMirror
    {
        public string name;
        public float capacityUnits;
    }

    internal sealed class ModuleEnvelopeMirror
    {
        public string Name;
        public List<ModuleDataMirror> ModuleData;
    }

    internal sealed class ModuleDataMirror
    {
        [JsonConverter(typeof(DataObjectMirrorConverter))]
        public DataObjectMirror DataObject;
    }

    /// <summary>Abstract base for module-specific DataObject shapes. Concrete subclasses below.</summary>
    /// <remarks>
    /// The bake reads <c>$type</c> on each DataObject and routes to the matching subclass via
    /// <see cref="DataObjectMirrorConverter" />. Unknown module shapes resolve to
    /// <see cref="UnknownDataObjectMirror" /> so deserialisation stays total.
    /// </remarks>
    internal abstract class DataObjectMirror
    {
    }

    /// <summary>Data_Engine fields the bake reads. Engine parts have one of these per Engine module.</summary>
    [DataObjectMirror("KSP.Modules.Data_Engine")]
    internal sealed class EngineDataObjectMirror : DataObjectMirror
    {
        public List<EngineModeMirror> engineModes;
    }

    /// <summary>Data_RCS fields the bake reads. Effective thrust is <c>maxThrust * thrustPercentage.storedValue / 100</c>.</summary>
    [DataObjectMirror("KSP.Modules.Data_RCS")]
    internal sealed class RcsDataObjectMirror : DataObjectMirror
    {
        public float maxThrust;
        public ContextWrappedFloat thrustPercentage;
        public PropellantMirror propellant;
    }

    /// <summary>Catch-all for module DataObject types the bake does not track (Gimbal, Generator, Drag, etc.).</summary>
    /// <remarks>
    /// The <c>$type</c> string is preserved so verbose-log output can list which module shapes
    /// were skipped, useful when adding a new field would require modelling a new module type.
    /// </remarks>
    internal sealed class UnknownDataObjectMirror : DataObjectMirror
    {
        public string TypeDiscriminator;
    }

    internal sealed class EngineModeMirror
    {
        public float maxThrust;
        public CurveMirror atmosphereCurve;
        public PropellantMirror propellant;
    }

    /// <summary>Mirror of <c>PropellantDefinition</c>. Only the mixture name is needed for keying.</summary>
    internal sealed class PropellantMirror
    {
        public string mixtureName;
    }

    /// <summary>Mirror of <c>PartModuleResourceSetting</c>. Drives per-resource rates for generator and solar modules.</summary>
    internal sealed class PartModuleResourceSettingMirror
    {
        public string ResourceName;
        public float Rate;
    }

    [DataObjectMirror("KSP.Modules.Data_Gimbal")]
    internal sealed class GimbalDataObjectMirror : DataObjectMirror
    {
        public float gimbalRange;
        public float gimbalResponseSpeed;
    }

    [DataObjectMirror("KSP.Modules.Data_ReactionWheel")]
    internal sealed class ReactionWheelDataObjectMirror : DataObjectMirror
    {
        public float PitchTorque;
        public float YawTorque;
        public float RollTorque;
        public List<PartModuleResourceSettingMirror> RequiredResources;
    }

    [DataObjectMirror("KSP.Modules.Data_Command")]
    internal sealed class CommandDataObjectMirror : DataObjectMirror
    {
        public int minimumCrew;
        public bool hasHibernation;
        public bool requiresCommNet;
        public List<PartModuleResourceSettingMirror> requiredResources;
    }

    [DataObjectMirror("KSP.Modules.Data_ScienceExperiment")]
    internal sealed class ScienceExperimentDataObjectMirror : DataObjectMirror
    {
        public List<ExperimentConfigurationMirror> Experiments;
    }

    [DataObjectMirror("KSP.Modules.Data_ResourceIntake")]
    internal sealed class ResourceIntakeDataObjectMirror : DataObjectMirror
    {
        public float area;
        public double intakeSpeed;
    }

    [DataObjectMirror("KSP.Modules.Data_LiftingSurface")]
    internal sealed class LiftingSurfaceDataObjectMirror : DataObjectMirror
    {
        public float deflectionLiftCoeff;
    }

    [DataObjectMirror("KSP.Modules.Data_Heatshield")]
    internal sealed class HeatshieldDataObjectMirror : DataObjectMirror
    {
        public double AblationTempThreshold;
        public double AblationMaximumOverThreshold;
        public double PyrolysisLossFactor;
        public float ShieldingScale;
    }

    [DataObjectMirror("KSP.Modules.Data_ActiveRadiator")]
    internal sealed class ActiveRadiatorDataObjectMirror : DataObjectMirror
    {
        public float ProceduralRadiatorFluxPerAreaUnit;
    }

    [DataObjectMirror("KSP.Modules.Data_CargoBay")]
    internal sealed class CargoBayDataObjectMirror : DataObjectMirror
    {
        public float lookUpRadius;
        public float BayInternalLength;
    }

    /// <summary>
    /// Mirror of <c>ExperimentConfiguration</c>. Carries the per-experiment fields the bake
    /// tracks (time, crew, EC cost) so archetypes can seed a fully-configured experiment
    /// rather than just an empty entry.
    /// </summary>
    internal sealed class ExperimentConfigurationMirror
    {
        public string ExperimentDefinitionID;
        public float TimeToComplete;
        public int CrewRequired;
        public List<PartModuleResourceSettingMirror> ResourcesCost;
    }

    [DataObjectMirror("KSP.Modules.Data_Decouple")]
    internal sealed class DecoupleDataObjectMirror : DataObjectMirror
    {
        public float ejectionForce;
    }

    [DataObjectMirror("KSP.Modules.Data_SolarPanel")]
    internal sealed class SolarPanelDataObjectMirror : DataObjectMirror
    {
        public float EfficiencyMultiplier;
        public PartModuleResourceSettingMirror ResourceSettings;
    }

    [DataObjectMirror("KSP.Modules.Data_Transmitter")]
    internal sealed class TransmitterDataObjectMirror : DataObjectMirror
    {
        public double CommunicationRange;
        public float DataPacketSize;
        public float DataTransmissionInterval;
    }

    [DataObjectMirror("KSP.Modules.Data_Parachute")]
    internal sealed class ParachuteDataObjectMirror : DataObjectMirror
    {
        public float defaultDeployAltitude;
        public float defaultMinAirPressureToOpen;
        public double areaDeployed;
        public double chuteMaxTemp;
    }

    [DataObjectMirror("KSP.Modules.Data_ModuleGenerator")]
    internal sealed class GeneratorDataObjectMirror : DataObjectMirror
    {
        public PartModuleResourceSettingMirror ResourceSetting;
    }

    [DataObjectMirror("KSP.Modules.Data_ResourceConverter")]
    internal sealed class ConverterDataObjectMirror : DataObjectMirror
    {
        public ContextWrappedFloat conversionRate;
    }

    [DataObjectMirror("KSP.Modules.Data_WheelBrakes")]
    internal sealed class WheelBrakesDataObjectMirror : DataObjectMirror
    {
        public float MaxBrakeTorque;
    }

    [DataObjectMirror("KSP.Modules.Data_WheelSuspension")]
    internal sealed class WheelSuspensionDataObjectMirror : DataObjectMirror
    {
        public float suspensionDistance;
    }

    [DataObjectMirror("KSP.Modules.Data_DockingNode")]
    internal sealed class DockingNodeDataObjectMirror : DataObjectMirror
    {
        public float AcquireRange;
        public float AcquireForce;
        public float CaptureRange;
    }

    [DataObjectMirror("KSP.Modules.Data_ControlSurface")]
    internal sealed class ControlSurfaceDataObjectMirror : DataObjectMirror
    {
        public float CtrlSurfaceRange;
        public float CtrlSurfaceArea;
        public float ActuatorSpeedNormalScale;
    }

    internal sealed class CurveMirror
    {
        public FCurveMirror fCurve;
    }

    internal sealed class FCurveMirror
    {
        public List<KeyMirror> keys;
    }

    internal sealed class KeyMirror
    {
        public float time;
        public float value;
    }

    /// <summary>Mirror of the runtime context-key wrapper used for authored fields with subscribable storage.</summary>
    internal sealed class ContextWrappedFloat
    {
        public string ContextKey;
        public float storedValue;
    }
}
#endif
