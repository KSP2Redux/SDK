using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.Definitions;
using KSP.Sim.impl;
using KSP.Sim.ResourceSystem;
using Redux.Ksp1Import;
using Redux.Ksp1Import.Config;
using Redux.Ksp1Import.Modules;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring
{
    internal static class Ksp1EditorNativeDeployableConverter
    {
        private const string ClosedState = "CLOSED";
        private const string OpenState = "OPEN";
        private const string OpenedState = "OPENED";
        private const string CloseState = "CLOSE";
        private const string Ksp1DefaultDeployablePivot = "sunPivot";

        public static void Convert(
            GameObject prefab,
            PartCore core,
            Ksp1ConfigNode partNode,
            string partFolder,
            Ksp1ImportReport report
        )
        {
            if (prefab == null || core?.data?.serializedPartModules == null || partNode == null)
            {
                return;
            }

            bool convertedNativeDeployable = false;
            bool convertedSolar = TryConvertSolarPanel(prefab, core, partNode, partFolder, report, out bool solarHadAnimation);
            bool convertedRadiator = TryConvertRadiator(prefab, core, partNode, partFolder, report, out bool radiatorHadAnimation);
            bool convertedWheel = TryConvertWheel(prefab, core, partNode, partFolder, report, out bool wheelHadAnimation);

            convertedNativeDeployable |= convertedSolar && solarHadAnimation;
            convertedNativeDeployable |= convertedRadiator && radiatorHadAnimation;
            convertedNativeDeployable |= convertedWheel && wheelHadAnimation;

            if (!convertedSolar && !convertedRadiator && !convertedWheel)
            {
                convertedNativeDeployable = TryConvertGenericDeployable(prefab, core, partNode, partFolder, report);
            }

            if (convertedNativeDeployable)
            {
                RemoveKsp1DeployableFallback(prefab, core);
            }
        }

        private static bool TryConvertSolarPanel(
            GameObject prefab,
            PartCore core,
            Ksp1ConfigNode partNode,
            string partFolder,
            Ksp1ImportReport report,
            out bool hadAnimation
        )
        {
            hadAnimation = false;
            Ksp1ConfigNode solarNode = FindModule(partNode, "ModuleDeployableSolarPanel");
            if (solarNode == null)
            {
                return false;
            }

            Data_Deployable deployable = BuildDeployableData(solarNode, defaultTracking: true, defaultRetractable: true);
            AnimationBakeResult bake = TryBakeDeployableAnimation(prefab, solarNode, partFolder, core.data.partName, "solar", report);
            if (bake.Success)
            {
                hadAnimation = true;
                deployable.extendable = true;
                deployable.animationName = string.Empty;
            }
            else
            {
                deployable.extendable = false;
                deployable.retractable = false;
                deployable.CurrentDeployState.SetValue(Data_Deployable.DeployState.Extended);
                deployable.toggleExtend.SetValue(true);
                deployable.AnimationNormalizedTime.SetValue(1f);
            }

            deployable.DefaultActionGroup = KSPActionGroup.SolarPanels;
            deployable.OneTimeExtendActionName = "PartModules/Deployable/ExtendOnlyPanels";
            deployable.DeployToggleActionName = "PartModules/Deployable/TogglePanels";
            deployable.ActionGroupToggleName = "PartModules/Deployable/ToggleExtended/TogglePanels";
            deployable.ActionGroupExtendName = "PartModules/Deployable/ToggleExtended/ExtendPanels";
            deployable.ActionGroupRetractName = "PartModules/Deployable/ToggleExtended/RetractPanels";

            string raycastTransformName = solarNode.GetValue("raycastTransformName");
            string secondaryTransformName = solarNode.GetValue("secondaryTransformName", raycastTransformName);
            if (!string.IsNullOrWhiteSpace(secondaryTransformName))
            {
                deployable.secondaryTransform = secondaryTransformName;
            }

            Data_SolarPanel solar = new()
            {
                ResourceSettings = ResourceSetting(
                    solarNode.GetValue("resourceName", "ElectricCharge"),
                    GetFloat(solarNode, "chargeRate", 0f)
                ),
                RaycastOffset = GetFloat(solarNode, "raycastOffset", 0.25f),
                UseRaycastForTrackingDot = GetBool(solarNode, "useRaycastForTrackingDot", false),
                RaycastTransformName = string.IsNullOrWhiteSpace(raycastTransformName) ? secondaryTransformName : raycastTransformName,
                PanelIncidenceDirection = GetSolarPanelIncidenceDirection(solarNode, prefab, deployable.pivotName)
            };

            RemoveMatchingSolarGenerator(core, solar.ResourceSettings);
            ReplaceOrAddModule(core, CreateSerialized(typeof(Module_SolarPanel), deployable, solar), "Module_SolarPanel");
            report.Important($"Part '{core.data.partName}' converted KSP1 solar panel to native KSP2 Module_SolarPanel.");
            return true;
        }

        private static bool TryConvertRadiator(
            GameObject prefab,
            PartCore core,
            Ksp1ConfigNode partNode,
            string partFolder,
            Ksp1ImportReport report,
            out bool hadAnimation
        )
        {
            hadAnimation = false;
            Ksp1ConfigNode activeNode = FindModule(partNode, "ModuleActiveRadiator");
            Ksp1ConfigNode deployNode = FindModule(partNode, "ModuleDeployableRadiator");
            if (activeNode == null && deployNode == null)
            {
                return false;
            }

            if (activeNode != null)
            {
                Data_Cooler cooler = new()
                {
                    fluxRemoved = GetDouble(activeNode, "maxEnergyTransfer", 0.0),
                    requiredResources = ReadResourceSettings(activeNode).ToList(),
                    emissiveMaterialNames = new List<string>(),
                    InputResourcesIDs = new List<ResourceDefinitionID>()
                };
                if (cooler.requiredResources.Count == 0)
                {
                    cooler.requiredResources.Add(ResourceSetting("ElectricCharge", 0f));
                }

                Data_ActiveRadiator radiator = new();
                ReplaceOrAddModule(core, CreateSerialized(typeof(Module_ActiveRadiator), cooler, radiator), "Module_ActiveRadiator");
            }

            if (deployNode != null)
            {
                Data_Deployable deployable = BuildDeployableData(deployNode, defaultTracking: false, defaultRetractable: true);
                deployable.DefaultActionGroup = KSPActionGroup.RadiatorPanels;
                AnimationBakeResult bake =
                    TryBakeDeployableAnimation(prefab, deployNode, partFolder, core.data.partName, "radiator", report);
                if (bake.Success)
                {
                    hadAnimation = true;
                    deployable.animationName = string.Empty;
                    ReplaceOrAddModule(core, CreateSerialized(typeof(Module_Deployable), deployable), "Module_Deployable");
                }
                else
                {
                    report.Warn(
                        $"Part '{core.data.partName}' has a deployable radiator module but no native deploy animation could be baked."
                    );
                }
            }

            report.Important($"Part '{core.data.partName}' converted KSP1 radiator data to native KSP2 radiator module(s).");
            return true;
        }

        private static bool TryConvertWheel(
            GameObject prefab,
            PartCore core,
            Ksp1ConfigNode partNode,
            string partFolder,
            Ksp1ImportReport report,
            out bool hadAnimation
        )
        {
            hadAnimation = false;
            Ksp1ConfigNode wheelBaseNode = FindModule(partNode, "ModuleWheelBase");
            Ksp1ConfigNode deploymentNode = FindModule(partNode, "ModuleWheelDeployment");
            if (wheelBaseNode == null && deploymentNode == null)
            {
                return false;
            }

            if (deploymentNode != null)
            {
                Data_Deployable deployable = BuildDeployableData(deploymentNode, defaultTracking: false, defaultRetractable: true);
                deployable.DisableAnimatorWhenInactive = true;
                deployable.DefaultActionGroup = KSPActionGroup.Gear;
                AnimationBakeResult bake =
                    TryBakeDeployableAnimation(prefab, deploymentNode, partFolder, core.data.partName, "wheel", report);
                if (bake.Success)
                {
                    hadAnimation = true;
                    deployable.animationName = string.Empty;
                    ReplaceOrAddModule(core, CreateSerialized(typeof(Module_Deployable), deployable), "Module_Deployable");
                }
                else
                {
                    report.Warn(
                        $"Part '{core.data.partName}' has KSP1 wheel deployment data but no native deploy animation could be baked."
                    );
                }
            }

            if (wheelBaseNode != null)
            {
                Data_WheelBase wheelBaseData = BuildWheelBaseData(wheelBaseNode);
                if (deploymentNode != null)
                {
                    wheelBaseData.UseStandinCollider = GetBool(deploymentNode, "useStandInCollider", wheelBaseData.UseStandinCollider);
                    wheelBaseData.DeploymentSubsystemNormalized =
                        GetFloat(deploymentNode, "TsubSys", wheelBaseData.DeploymentSubsystemNormalized);
                }

                ReplaceOrAddModule(core, CreateSerialized(typeof(Module_WheelBase), wheelBaseData), "Module_WheelBase");
            }

            Ksp1ConfigNode suspensionNode = FindModule(partNode, "ModuleWheelSuspension");
            if (suspensionNode != null)
            {
                ReplaceOrAddModule(
                    core,
                    CreateSerialized(typeof(Module_WheelSuspension), BuildWheelSuspensionData(suspensionNode)),
                    "Module_WheelSuspension"
                );
            }

            Ksp1ConfigNode brakesNode = FindModule(partNode, "ModuleWheelBrakes");
            if (brakesNode != null)
            {
                ReplaceOrAddModule(core, CreateSerialized(typeof(Module_WheelBrakes), BuildWheelBrakesData(brakesNode)), "Module_WheelBrakes");
            }

            Ksp1ConfigNode steeringNode = FindModule(partNode, "ModuleWheelSteering");
            if (steeringNode != null)
            {
                ReplaceOrAddModule(core, CreateSerialized(typeof(Module_WheelSteering), BuildWheelSteeringData(steeringNode)), "Module_WheelSteering");
            }

            Ksp1ConfigNode lockNode = FindModule(partNode, "ModuleWheelLock");
            if (lockNode != null)
            {
                ReplaceOrAddModule(core, CreateSerialized(typeof(Module_WheelLock), BuildWheelLockData(lockNode)), "Module_WheelLock");
            }

            Ksp1ConfigNode bogeyNode = FindModule(partNode, "ModuleWheelBogey");
            if (bogeyNode != null)
            {
                ReplaceOrAddModule(core, CreateSerialized(typeof(Module_WheelBogey), BuildWheelBogeyData(bogeyNode)), "Module_WheelBogey");
            }

            Ksp1ConfigNode damageNode = FindModule(partNode, "ModuleWheelDamage");
            if (damageNode != null)
            {
                ReplaceOrAddModule(core, CreateSerialized(typeof(Module_WheelDamage), BuildWheelDamageData(damageNode)), "Module_WheelDamage");
            }

            EnsureWheelColliderComponent(prefab, wheelBaseNode, report, core.data.partName);
            report.Important($"Part '{core.data.partName}' converted KSP1 wheel/leg module data to native KSP2 wheel module(s).");
            return true;
        }

        private static bool TryConvertGenericDeployable(
            GameObject prefab,
            PartCore core,
            Ksp1ConfigNode partNode,
            string partFolder,
            Ksp1ImportReport report
        )
        {
            List<Ksp1ConfigNode> genericNodes = partNode
                .GetNodes("MODULE")
                .Where(node => string.Equals(node.GetValue("name"), "ModuleAnimateGeneric", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (genericNodes.Count == 0)
            {
                return false;
            }

            if (genericNodes.Count > 1)
            {
                report.Warn(
                    $"Part '{core.data.partName}' has {genericNodes.Count} ModuleAnimateGeneric entries. Native KSP2 conversion supports one deployable animator per part, so the KSP1 deployable fallback was kept."
                );
                return false;
            }

            Ksp1ConfigNode node = genericNodes[0];
            AnimationBakeResult bake = TryBakeDeployableAnimation(prefab, node, partFolder, core.data.partName, "generic", report);
            if (!bake.Success)
            {
                return false;
            }

            Data_Deployable deployable = BuildDeployableData(node, defaultTracking: false, defaultRetractable: true);
            deployable.animationName = string.Empty;
            deployable.DefaultActionGroup = ReadActionGroup(node.GetValue("defaultActionGroup"), KSPActionGroup.None);
            ReplaceOrAddModule(core, CreateSerialized(typeof(Module_Deployable), deployable), "Module_Deployable");
            report.Important($"Part '{core.data.partName}' converted KSP1 ModuleAnimateGeneric to native KSP2 Module_Deployable.");
            return true;
        }

        private static Data_Deployable BuildDeployableData(
            Ksp1ConfigNode moduleNode,
            bool defaultTracking,
            bool defaultRetractable
        )
        {
            bool startsExtended = GetBool(moduleNode, "startDeployed", false);
            bool isOneShot = GetBool(moduleNode, "isOneShot", false);
            Data_Deployable data = new()
            {
                showStatus = GetBool(moduleNode, "showStatus", true),
                isTracking = GetBool(moduleNode, "isTracking", defaultTracking),
                extendable = true,
                retractable = GetBool(moduleNode, "retractable", defaultRetractable) && !isOneShot,
                applyShielding = GetBool(moduleNode, "applyShielding", false),
                applyShieldingExtend = GetBool(moduleNode, "applyShieldingExtend", false),
                pivotName = FirstNonEmpty(moduleNode.GetValue("pivotName"), Ksp1DefaultDeployablePivot),
                secondaryTransform = moduleNode.GetValue("secondaryTransformName", string.Empty),
                alignType = moduleNode.GetValue("alignType", string.Empty),
                trackingSpeed = GetFloat(moduleNode, "trackingSpeed", 0.25f),
                TrackingAlignmentOffset = GetFloat(moduleNode, "TrackingAlignmentOffset", 0f),
                trackingMode = ReadDeployableTrackingMode(moduleNode, defaultTracking),
                rotationMode = Data_Deployable.RotationMode.YAW,
                EditorAnimSpeedMul = GetFloat(moduleNode, "editorAnimationSpeedMult", 10f),
                DeployToggleActionName = FirstNonEmpty(
                    moduleNode.GetValue("actionGUIName"),
                    moduleNode.GetValue("extendpanelsActionName"),
                    "PartModules/Deployable/Toggle"
                ),
                ActionGroupToggleName = FirstNonEmpty(
                    moduleNode.GetValue("actionGUIName"),
                    moduleNode.GetValue("extendpanelsActionName"),
                    "PartModules/Deployable/ToggleExtended/Toggle"
                ),
                ActionGroupExtendName = FirstNonEmpty(
                    moduleNode.GetValue("startEventGUIName"),
                    moduleNode.GetValue("extendActionName"),
                    "PartModules/Deployable/ToggleExtended/Extend"
                ),
                ActionGroupRetractName = FirstNonEmpty(
                    moduleNode.GetValue("endEventGUIName"),
                    moduleNode.GetValue("retractActionName"),
                    "PartModules/Deployable/ToggleExtended/Retract"
                ),
                DefaultActionGroup = ReadActionGroup(moduleNode.GetValue("defaultActionGroup"), KSPActionGroup.None),
                deployCrashTolerance = GetDouble(moduleNode, "impactResistance", 10.0),
                AeroStressCanBreak = GetBool(moduleNode, "isBreakable", true)
            };

            if (data.trackingMode == Data_Deployable.TrackingMode.None)
            {
                data.isTracking = false;
            }
            else if (defaultTracking && !HasValue(moduleNode, "isTracking"))
            {
                data.isTracking = true;
            }

            data.CurrentDeployState.SetValue(startsExtended ? Data_Deployable.DeployState.Extended : Data_Deployable.DeployState.Retracted);
            data.toggleExtend.SetValue(startsExtended);
            data.AnimationNormalizedTime.SetValue(startsExtended ? 1f : 0f);
            data.DefaultDeployState = startsExtended ? Data_Deployable.DeployState.Extended : Data_Deployable.DeployState.Retracted;
            return data;
        }

        private static Data_WheelBase BuildWheelBaseData(Ksp1ConfigNode node)
        {
            Data_WheelBase data = new()
            {
                WheelType = ReadWheelType(node.GetValue("wheelType")),
                FitWheelColliderToMesh = GetBool(node, "FitWheelColliderToMesh", false),
                Radius = GetFloat(node, "radius", 0.2f),
                Center = node.GetVector3("center", Vector3.zero),
                Mass = GetFloat(node, "mass", 0.04f),
                FrictionSharpness = GetFloat(node, "frictionSharpness", 0f),
                WheelDamping = GetFloat(node, "wheelDamping", 0.05f),
                WheelMaxSpeed = GetFloat(node, "wheelMaxSpeed", 1000f),
                ClipObject = node.GetValue("clipObject", string.Empty),
                AdherentStart = GetFloat(node, "adherentStart", 0.5f),
                FrictionAdherent = GetFloat(node, "frictionAdherent", 0.25f),
                PeakStart = GetFloat(node, "peakStart", 4f),
                FrictionPeak = GetFloat(node, "frictionPeak", 1.45f),
                LimitStart = GetFloat(node, "limitStart", 7f),
                FrictionLimit = GetFloat(node, "frictionLimit", 1.1f),
                GeeBias = GetFloat(node, "geeBias", 1.6f),
                GroundHeightOffset = GetFloat(node, "groundHeightOffset", 0f),
                InctiveSubsteps = GetInt(node, "inactiveSubsteps", 4),
                ActiveSubsteps = GetInt(node, "activeSubsteps", 8),
                TireForceSharpness = GetFloat(node, "tireForceSharpness", 10f),
                SuspensionForceSharpness = GetFloat(node, "suspensionForceSharpness", 10f),
                WheelColliderTransformName = node.GetValue("wheelColliderTransformName", "wheelCol"),
                WheelTransformName = node.GetValue("wheelTransformName", string.Empty),
                SpringSlerpRate = GetFloat(node, "springSlerpRate", 0.02f),
                MinimumDownforce = GetFloat(node, "minimumDownforce", 0.5f),
                UseNewFrictionModel = GetBool(node, "useNewFrictionModel", true)
            };
            data.AutoFriction.SetValue(GetBool(node, "autoFriction", true));
            data.FrictionMultiplier.SetValue(GetFloat(node, "frictionMultiplier", 1f));
            data.AlignVisualAndCollider = data.WheelType != WheelType.LEG;
            return data;
        }

        private static Data_WheelSuspension BuildWheelSuspensionData(Ksp1ConfigNode node)
        {
            Data_WheelSuspension data = new()
            {
                suspensionTransformName = node.GetValue("suspensionTransformName", string.Empty),
                suspensionColliderName = node.GetValue("suspensionColliderName", string.Empty),
                suspensionDistance = GetFloat(node, "suspensionDistance", 0f),
                suspensionOffset = GetFloat(node, "suspensionOffset", 0f),
                maximumLoad = GetFloat(node, "maximumLoad", 1f),
                targetPosition = GetFloat(node, "targetPosition", 1f),
                springRatio = GetFloat(node, "springRatio", 50f),
                damperRatio = GetFloat(node, "damperRatio", 1f),
                boostRatio = GetFloat(node, "boostRatio", 0f),
                useDistributedMass = GetBool(node, "useDistributedMass", true),
                useAutoBoost = GetBool(node, "useAutoBoost", true),
                adjustForHighGee = GetBool(node, "adjustForHighGee", true),
                highGeeThreshold = GetFloat(node, "highGeeThreshold", 1.5f),
                highGeeSpringTweakable = GetFloat(node, "highGeeSpringTweakable", 3f),
                highGeeDamperTweakable = GetFloat(node, "highGeeDamperTweakable", 1.2f)
            };
            data.autoSpringDamper.SetValue(GetBool(node, "autoSpringDamper", true));
            data.springTweakable.SetValue(GetFloat(node, "springTweakable", 1f));
            data.damperTweakable.SetValue(GetFloat(node, "damperTweakable", 1f));
            return data;
        }

        private static Data_WheelBrakes BuildWheelBrakesData(Ksp1ConfigNode node)
        {
            Data_WheelBrakes data = new()
            {
                MaxBrakeTorque = GetFloat(node, "maxBrakeTorque", 2000f),
                BrakeResponse = GetFloat(node, "brakeResponse", 10f)
            };
            data.BrakeTweakable.SetValue(GetFloat(node, "brakeTweakable", 100f));
            return data;
        }

        private static Data_WheelSteering BuildWheelSteeringData(Ksp1ConfigNode node)
        {
            Data_WheelSteering data = new()
            {
                CaliperTransformName = node.GetValue("caliperTransformName", string.Empty),
                SteeringResponse = GetFloat(node, "steeringResponse", 8f),
                SteeringCurve = ReadCurve(node, "steeringCurve", new Keyframe(0f, GetFloat(node, "steeringRange", 30f))),
                SteeringMaxAngleCurve = ReadCurve(node, "steeringMaxAngleCurve", new Keyframe(0f, 1f), new Keyframe(100f, 1f))
            };
            data.SteeringEnabled.SetValue(GetBool(node, "steeringEnabled", true));
            data.SteeringInvert.SetValue(GetBool(node, "steeringInvert", false));
            return data;
        }

        private static Data_WheelLock BuildWheelLockData(Ksp1ConfigNode node)
        {
            return new Data_WheelLock
            {
                MaxTorque = GetFloat(node, "maxTorque", 1000f)
            };
        }

        private static Data_WheelBogey BuildWheelBogeyData(Ksp1ConfigNode node)
        {
            return new Data_WheelBogey
            {
                wheelTransformRefName = node.GetValue("wheelTransformRefName", string.Empty),
                wheelTransformBaseName = node.GetValue("wheelTransformBaseName", string.Empty),
                bogeyTransformName = node.GetValue("bogeyTransformName", string.Empty),
                bogeyRefTransformName = node.GetValue("bogeyRefTransformName", string.Empty),
                maxPitch = GetFloat(node, "maxPitch", 25f),
                minPitch = GetFloat(node, "minPitch", -25f),
                restPitch = GetFloat(node, "restPitch", -25f),
                pitchResponse = GetFloat(node, "pitchResponse", 8f),
                bogeyAxis = node.GetVector3("bogeyAxis", Vector3.right),
                bogeyUpAxis = node.GetVector3("bogeyUpAxis", Vector3.up)
            };
        }

        private static Data_WheelDamage BuildWheelDamageData(Ksp1ConfigNode node)
        {
            return new Data_WheelDamage
            {
                stressTolerance = GetFloat(node, "stressTolerance", 10f),
                impactTolerance = GetFloat(node, "impactTolerance", 40f),
                deflectionMagnitude = GetFloat(node, "deflectionMagnitude", 1f),
                slipMagnitude = GetFloat(node, "slipMagnitude", 1f),
                deflectionSharpness = GetFloat(node, "deflectionSharpness", 1f),
                slipSharpness = GetFloat(node, "slipSharpness", 2f),
                damagedTransformName = node.GetValue("damagedTransformName", string.Empty),
                undamagedTransformName = node.GetValue("undamagedTransformName", string.Empty),
                isRepairable = GetBool(node, "isRepairable", true),
                explodeMultiplier = GetFloat(node, "explodeMultiplier", 5f),
                impactDamageVelocity = GetFloat(node, "impactDamageVelocity", 0f),
                impactDamageColliderName = node.GetValue("impactDamageColliderName", string.Empty)
            };
        }

        private static AnimationBakeResult TryBakeDeployableAnimation(
            GameObject prefab,
            Ksp1ConfigNode moduleNode,
            string partFolder,
            string partName,
            string suffix,
            Ksp1ImportReport report
        )
        {
            string animationName = FirstNonEmpty(moduleNode.GetValue("animationName"), moduleNode.GetValue("animationStateName"));
            if (string.IsNullOrWhiteSpace(animationName))
            {
                return AnimationBakeResult.Missing;
            }

            Animation animation = FindAnimation(prefab, animationName, moduleNode.GetValue("animationTrfName"));
            AnimationClip sourceClip = animation == null ? null : animation.GetClip(animationName);
            if (animation == null || sourceClip == null)
            {
                report.Warn($"Part '{partName}' could not find KSP1 animation clip '{animationName}' for native deployable conversion.");
                return AnimationBakeResult.Missing;
            }

            string animationFolder = Ksp1EditorAssetUtility.EnsureFolder(partFolder, "Animations");
            string baseName = Ksp1EditorAssetUtility.SanitizePathSegment($"{partName}_{suffix}_{animationName}");
            AnimationClip deployClip = CloneAnimationClip(sourceClip, $"{baseName}_open", false);
            AnimationClip retractClip = ReverseAnimationClip(sourceClip, $"{baseName}_close");
            AnimationClip closedClip = CreateStaticPoseClip(sourceClip, $"{baseName}_closed", 0f);
            AnimationClip openedClip = CreateStaticPoseClip(sourceClip, $"{baseName}_opened", sourceClip.length);

            string closedPath = AssetPath(animationFolder, $"{baseName}_closed.anim");
            string openPath = AssetPath(animationFolder, $"{baseName}_open.anim");
            string openedPath = AssetPath(animationFolder, $"{baseName}_opened.anim");
            string closePath = AssetPath(animationFolder, $"{baseName}_close.anim");
            CreateOrReplaceAsset(closedClip, closedPath);
            CreateOrReplaceAsset(deployClip, openPath);
            CreateOrReplaceAsset(openedClip, openedPath);
            CreateOrReplaceAsset(retractClip, closePath);

            string controllerPath = AssetPath(animationFolder, $"{baseName}.controller");
            AnimatorController controller =
                BuildDeployableController(controllerPath, closedClip, deployClip, openedClip, retractClip);

            Animator animator = GetOrAddAnimator(animation.gameObject);
            if (animator == null)
            {
                report.Warn($"Part '{partName}' could not add an Animator to '{animation.gameObject.name}' for native deployable conversion.");
                return AnimationBakeResult.Missing;
            }
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            return AnimationBakeResult.Baked;
        }

        private static Animator GetOrAddAnimator(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            Animator animator = target.GetComponent<Animator>();
            if (animator == null)
            {
                animator = target.AddComponent<Animator>();
            }
            return animator == null ? null : animator;
        }

        private static AnimatorController BuildDeployableController(
            string path,
            Motion closedClip,
            Motion deployClip,
            Motion openedClip,
            Motion retractClip
        )
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("isDeployed", AnimatorControllerParameterType.Bool);
            controller.AddParameter("playbackMul", AnimatorControllerParameterType.Float);
            controller.AddParameter("reverseAnimStateChange", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState closed = stateMachine.AddState(ClosedState);
            AnimatorState open = stateMachine.AddState(OpenState);
            AnimatorState opened = stateMachine.AddState(OpenedState);
            AnimatorState close = stateMachine.AddState(CloseState);
            stateMachine.defaultState = closed;

            closed.motion = closedClip;
            open.motion = deployClip;
            opened.motion = openedClip;
            close.motion = retractClip;
            open.speedParameterActive = true;
            open.speedParameter = "playbackMul";
            close.speedParameterActive = true;
            close.speedParameter = "playbackMul";
            closed.AddStateMachineBehaviour<Module_AnimStateInformer>();
            opened.AddStateMachineBehaviour<Module_AnimStateInformer>();

            AddBoolTransition(closed, open, true);
            AddExitTransition(open, opened);
            AddTriggerTransition(open, closed);
            AddBoolTransition(opened, close, false);
            AddExitTransition(close, closed);
            AddTriggerTransition(close, opened);
            EditorUtility.SetDirty(controller);
            AssetDatabase.ImportAsset(path);
            return controller;
        }

        private static void AddBoolTransition(AnimatorState from, AnimatorState to, bool value)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, "isDeployed");
        }

        private static void AddTriggerTransition(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.AddCondition(AnimatorConditionMode.If, 0f, "reverseAnimStateChange");
        }

        private static void AddExitTransition(AnimatorState from, AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = 0.99f;
            transition.duration = 0f;
        }

        private static AnimationClip CloneAnimationClip(AnimationClip source, string name, bool legacy)
        {
            AnimationClip clip = new()
            {
                name = name,
                legacy = legacy,
                wrapMode = WrapMode.ClampForever,
                frameRate = source.frameRate
            };

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                clip.SetCurve(binding.path, binding.type, binding.propertyName, AnimationUtility.GetEditorCurve(source, binding));
            }

            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
            {
                AnimationUtility.SetObjectReferenceCurve(clip, binding, AnimationUtility.GetObjectReferenceCurve(source, binding));
            }

            AnimationUtility.SetAnimationEvents(clip, AnimationUtility.GetAnimationEvents(source));
            return clip;
        }

        private static AnimationClip ReverseAnimationClip(AnimationClip source, string name)
        {
            AnimationClip clip = new()
            {
                name = name,
                legacy = false,
                wrapMode = WrapMode.ClampForever,
                frameRate = source.frameRate
            };
            float length = Mathf.Max(source.length, 1f / Mathf.Max(source.frameRate, 1f));

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(source, binding);
                AnimationCurve reversed = new();
                for (int i = sourceCurve.length - 1; i >= 0; i--)
                {
                    Keyframe key = sourceCurve.keys[i];
                    reversed.AddKey(new Keyframe(length - key.time, key.value, -key.outTangent, -key.inTangent, key.outWeight, key.inWeight));
                }

                clip.SetCurve(binding.path, binding.type, binding.propertyName, reversed);
            }

            return clip;
        }

        private static AnimationClip CreateStaticPoseClip(AnimationClip source, string name, float sampleTime)
        {
            AnimationClip clip = new()
            {
                name = name,
                legacy = false,
                wrapMode = WrapMode.ClampForever,
                frameRate = source.frameRate
            };

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(source, binding);
                float value = sourceCurve.Evaluate(sampleTime);
                clip.SetCurve(binding.path, binding.type, binding.propertyName, AnimationCurve.Constant(0f, 1f / 60f, value));
            }

            return clip;
        }

        private static void CreateOrReplaceAsset(UnityEngine.Object asset, string path)
        {
            UnityEngine.Object existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.ImportAsset(path);
        }

        private static Animation FindAnimation(GameObject prefab, string clipName, string animationTransformName)
        {
            Animation[] animations = prefab.GetComponentsInChildren<Animation>(true);
            if (!string.IsNullOrWhiteSpace(animationTransformName))
            {
                foreach (Animation animation in animations)
                {
                    if (animation != null &&
                        animation.name.Equals(animationTransformName, StringComparison.OrdinalIgnoreCase) &&
                        animation.GetClip(clipName) != null)
                    {
                        return animation;
                    }
                }
            }

            return animations.FirstOrDefault(animation => animation != null && animation.GetClip(clipName) != null);
        }

        private static void RemoveKsp1DeployableFallback(GameObject prefab, PartCore core)
        {
            core.data.serializedPartModules.RemoveAll(module =>
                module.BehaviourType == typeof(Ksp1Module_Deployable) ||
                module.ComponentType == typeof(Ksp1PartComponentModule_Deployable) ||
                string.Equals(module.BehaviourType?.Name, "Ksp1Module_Deployable", StringComparison.Ordinal));

            foreach (Ksp1DeployableAnimationRuntime runtime in prefab.GetComponentsInChildren<Ksp1DeployableAnimationRuntime>(true))
            {
                UnityEngine.Object.DestroyImmediate(runtime);
            }
        }

        private static void RemoveMatchingSolarGenerator(PartCore core, PartModuleResourceSetting solarResource)
        {
            int index = core.data.serializedPartModules.FindIndex(module =>
                module.BehaviourType == typeof(Module_Generator) &&
                module.ModuleData != null &&
                module.ModuleData.Any(data =>
                    data.DataObject is Data_ModuleGenerator generator &&
                    string.Equals(generator.ResourceSetting.ResourceName, solarResource.ResourceName, StringComparison.OrdinalIgnoreCase) &&
                    Mathf.Approximately(generator.ResourceSetting.Rate, solarResource.Rate)));

            if (index >= 0)
            {
                core.data.serializedPartModules.RemoveAt(index);
            }
        }

        private static void ReplaceOrAddModule(PartCore core, SerializedPartModule module, string behaviourName)
        {
            int existingIndex = core.data.serializedPartModules.FindIndex(existing =>
                string.Equals(existing.BehaviourType?.Name, behaviourName, StringComparison.Ordinal) ||
                existing.BehaviourType == module.BehaviourType ||
                existing.ComponentType == module.ComponentType);

            if (existingIndex >= 0)
            {
                core.data.serializedPartModules[existingIndex] = module;
            }
            else
            {
                core.data.serializedPartModules.Add(module);
            }
        }

        private static SerializedPartModule CreateSerialized(Type behaviourType, params ModuleData[] dataObjects)
        {
            Type componentType = FindPartComponentModuleType(behaviourType);
            return new SerializedPartModule
            {
                Name = componentType.Name,
                BehaviourType = behaviourType,
                ComponentType = componentType,
                ModuleData = dataObjects.Select(data => new SerializedModuleData(data)).ToList()
            };
        }

        private static Type FindPartComponentModuleType(Type behaviourType)
        {
            string componentTypeName = "KSP.Sim.impl.PartComponent" + behaviourType.Name;
            return behaviourType.Assembly.GetType(componentTypeName) ?? behaviourType;
        }

        private static IEnumerable<PartModuleResourceSetting> ReadResourceSettings(Ksp1ConfigNode node)
        {
            foreach (Ksp1ConfigNode resourceNode in node.GetNodes("RESOURCE"))
            {
                string name = resourceNode.GetValue("name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                yield return ResourceSetting(name, GetFloat(resourceNode, "rate", 0f));
            }
        }

        private static PartModuleResourceSetting ResourceSetting(string name, float rate)
        {
            return new PartModuleResourceSetting
            {
                ResourceName = Ksp1ResourceMapper.MapResourceName(name),
                Rate = rate,
                AcceptanceThreshold = 0.0
            };
        }

        private static FloatCurve ReadCurve(Ksp1ConfigNode node, string curveName, params Keyframe[] defaults)
        {
            Ksp1ConfigNode curveNode = node.GetNode(curveName);
            FloatCurve curve = new()
            {
                Curve = defaults != null && defaults.Length > 0 ? new AnimationCurve(defaults) : new AnimationCurve()
            };

            if (curveNode == null)
            {
                return curve;
            }

            AnimationCurve animationCurve = new();
            foreach (string keyValue in curveNode.GetValues("key"))
            {
                string[] parts = keyValue.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 ||
                    !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float time) ||
                    !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    continue;
                }

                float inTangent = parts.Length > 2 && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedIn)
                    ? parsedIn
                    : 0f;
                float outTangent = parts.Length > 3 && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedOut)
                    ? parsedOut
                    : inTangent;
                animationCurve.AddKey(new Keyframe(time, value, inTangent, outTangent));
            }

            if (animationCurve.length > 0)
            {
                curve.Curve = animationCurve;
            }

            return curve;
        }

        private static void EnsureWheelColliderComponent(
            GameObject prefab,
            Ksp1ConfigNode wheelBaseNode,
            Ksp1ImportReport report,
            string partName
        )
        {
            string colliderTransformName = wheelBaseNode?.GetValue("wheelColliderTransformName");
            if (string.IsNullOrWhiteSpace(colliderTransformName))
            {
                return;
            }

            Transform colliderTransform = FindChildByName(prefab.transform, colliderTransformName);
            if (colliderTransform == null)
            {
                report.Warn($"Part '{partName}' references wheel collider transform '{colliderTransformName}', but it was not found.");
                return;
            }

            if (colliderTransform.GetComponent<WheelCollider>() == null)
            {
                WheelCollider collider = colliderTransform.gameObject.AddComponent<WheelCollider>();
                collider.enabled = false;
            }
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (parent.name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return parent;
            }

            foreach (Transform child in parent)
            {
                Transform match = FindChildByName(child, name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Vector3 EstimatePanelIncidenceDirection(GameObject prefab, string pivotName)
        {
            Transform pivot = FindChildByName(prefab.transform, pivotName);
            if (pivot == null)
            {
                return Vector3.right;
            }

            Bounds bounds = new(pivot.position, Vector3.zero);
            bool hasBounds = false;
            foreach (Renderer renderer in pivot.GetComponentsInChildren<Renderer>(true))
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                return Vector3.right;
            }

            Vector3 localSize = pivot.InverseTransformVector(bounds.size);
            localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            if (localSize.x <= localSize.y && localSize.x <= localSize.z)
            {
                return Vector3.right;
            }

            if (localSize.y <= localSize.x && localSize.y <= localSize.z)
            {
                return Vector3.up;
            }

            return Vector3.forward;
        }

        private static Vector3 GetSolarPanelIncidenceDirection(Ksp1ConfigNode node, GameObject prefab, string pivotName)
        {
            string panelType = node.GetValue("type", "FLAT");
            if (string.Equals(panelType, "FLAT", StringComparison.OrdinalIgnoreCase))
            {
                // KSP1 flat solar panels calculate exposure from trackingDotTransform.forward.
                return Vector3.forward;
            }

            return EstimatePanelIncidenceDirection(prefab, pivotName);
        }

        private static Ksp1ConfigNode FindModule(Ksp1ConfigNode partNode, string moduleName)
        {
            return partNode.GetNodes("MODULE")
                .FirstOrDefault(module => string.Equals(module.GetValue("name"), moduleName, StringComparison.OrdinalIgnoreCase));
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static bool HasValue(Ksp1ConfigNode node, string name)
        {
            return node?.GetValue(name) != null;
        }

        private static string AssetPath(string folder, string fileName)
        {
            return $"{folder}/{Ksp1EditorAssetUtility.SanitizePathSegment(Path.GetFileNameWithoutExtension(fileName))}{Path.GetExtension(fileName)}";
        }

        private static Data_Deployable.TrackingMode ReadTrackingMode(string raw, Data_Deployable.TrackingMode fallback)
        {
            return Enum.TryParse(raw, true, out Data_Deployable.TrackingMode mode) ? mode : fallback;
        }

        private static Data_Deployable.TrackingMode ReadDeployableTrackingMode(Ksp1ConfigNode node, bool defaultTracking)
        {
            if (node != null && node.TryGetBool("sunTracking", out bool sunTracking))
            {
                return sunTracking ? Data_Deployable.TrackingMode.Sun : Data_Deployable.TrackingMode.None;
            }

            return ReadTrackingMode(
                node?.GetValue("trackingMode"),
                defaultTracking ? Data_Deployable.TrackingMode.Sun : Data_Deployable.TrackingMode.None
            );
        }

        private static KSPActionGroup ReadActionGroup(string raw, KSPActionGroup fallback)
        {
            return Enum.TryParse(raw, true, out KSPActionGroup group) ? group : fallback;
        }

        private static WheelType ReadWheelType(string raw)
        {
            return Enum.TryParse(raw, true, out WheelType type) ? type : WheelType.FREE;
        }

        private static float GetFloat(Ksp1ConfigNode node, string name, float fallback)
        {
            node.TryGetFloat(name, out float value, fallback);
            return value;
        }

        private static double GetDouble(Ksp1ConfigNode node, string name, double fallback)
        {
            node.TryGetDouble(name, out double value, fallback);
            return value;
        }

        private static int GetInt(Ksp1ConfigNode node, string name, int fallback)
        {
            node.TryGetInt(name, out int value, fallback);
            return value;
        }

        private static bool GetBool(Ksp1ConfigNode node, string name, bool fallback)
        {
            node.TryGetBool(name, out bool value, fallback);
            return value;
        }

        private readonly struct AnimationBakeResult
        {
            public static readonly AnimationBakeResult Baked = new(true);
            public static readonly AnimationBakeResult Missing = new(false);

            private AnimationBakeResult(bool success)
            {
                Success = success;
            }

            public bool Success { get; }
        }
    }
}
