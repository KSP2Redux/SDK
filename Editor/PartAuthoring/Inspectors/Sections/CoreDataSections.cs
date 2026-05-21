using KSP;
using Ksp2UnityTools.Editor.PartAuthoring.Tools;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// Factory methods for the Core tab's PartData-field sections.
    /// </summary>
    /// <remarks>
    /// Each method returns a UI Toolkit Foldout with <c>sdk-section</c> chrome and a stack of
    /// PropertyFields bound to the supplied SerializedObject. Sections that need extra UI -
    /// Attachment's auto-detect button, Resources' future-tab notice - are special-cased inline.
    /// </remarks>
    internal static class CoreDataSections
    {
        /// <summary>
        /// Builds the Identity section (partName, author, category, family, sizeKey, sizeCategory, tags, isCompound).
        /// </summary>
        /// <param name="so">The CorePartData's SerializedObject.</param>
        /// <returns>A bound Foldout with the section's PropertyFields.</returns>
        public static VisualElement BuildIdentity(SerializedObject so)
        {
            var foldout = MakeSectionFoldout("Identity");
            AddField(foldout, so, "core.data.partName");
            AddField(foldout, so, "core.data.author");
            AddField(foldout, so, "core.data.category");
            AddField(foldout, so, "core.data.family");
            AddField(foldout, so, "core.data.sizeKey");
            AddField(foldout, so, "core.data.sizeCategory");
            AddField(foldout, so, "core.data.tags");
            AddField(foldout, so, "core.data.isCompound");
            return Bound(foldout, so);
        }

        /// <summary>
        /// Builds the Mass and Cost and Crew section (mass, cost, crewCapacity).
        /// </summary>
        /// <param name="so">The CorePartData's SerializedObject.</param>
        /// <returns>A bound Foldout with the section's PropertyFields.</returns>
        public static VisualElement BuildMassCostCrew(SerializedObject so)
        {
            var foldout = MakeSectionFoldout("Mass & Cost & Crew");
            AddField(foldout, so, "core.data.mass");
            AddField(foldout, so, "core.data.cost");
            AddField(foldout, so, "core.data.crewCapacity");
            return Bound(foldout, so);
        }

        /// <summary>
        /// Builds the Breakage and Thermal section (crashTolerance, breaking force/torque, thermal mass, radiator, etc.).
        /// </summary>
        /// <param name="so">The CorePartData's SerializedObject.</param>
        /// <returns>A bound Foldout with the section's PropertyFields.</returns>
        public static VisualElement BuildBreakageThermal(SerializedObject so)
        {
            var foldout = MakeSectionFoldout("Breakage & Thermal");
            AddField(foldout, so, "core.data.crashTolerance");
            AddField(foldout, so, "core.data.breakingForce");
            AddField(foldout, so, "core.data.breakingTorque");
            AddField(foldout, so, "core.data.explosionPotential");
            AddField(foldout, so, "core.data.maxTemp");
            AddField(foldout, so, "core.data.skinMaxTemp");
            AddField(foldout, so, "core.data.emissiveConstant");
            AddField(foldout, so, "core.data.heatConductivity");
            AddField(foldout, so, "core.data.thermalMassModifier");
            AddField(foldout, so, "core.data.skinMassPerArea");
            AddField(foldout, so, "core.data.skinInternalConductionMult");
            AddField(foldout, so, "core.data.radiatorHeadroom");
            AddField(foldout, so, "core.data.radiatorMax");
            return Bound(foldout, so);
        }

        /// <summary>
        /// Builds the Aerodynamics and Physics section (drag, body lift, physics mode, etc.).
        /// </summary>
        /// <param name="so">The CorePartData's SerializedObject.</param>
        /// <returns>A bound Foldout with the section's PropertyFields.</returns>
        public static VisualElement BuildAerodynamicsPhysics(SerializedObject so)
        {
            var foldout = MakeSectionFoldout("Aerodynamics & Physics");
            AddField(foldout, so, "core.data.angularDrag");
            AddField(foldout, so, "core.data.maximumDrag");
            AddField(foldout, so, "core.data.minimumDrag");
            AddField(foldout, so, "core.data.bodyLiftOnlyUnattachedLift");
            AddField(foldout, so, "core.data.bodyLiftOnlyAttachName");
            AddField(foldout, so, "core.data.physicsMode");
            AddField(foldout, so, "core.data.PartSizeDiameter");
            AddField(foldout, so, "core.data.maxLength");
            AddField(foldout, so, "core.data.AllowKinematicPhysicsIfIntersectTerrain");
            AddField(foldout, so, "core.data.collisionVolumeBoundsScale");
            return Bound(foldout, so);
        }

        /// <summary>
        /// Builds the Attachment section (attachRules, attachNodes list, and the Auto-detect from GO button below it).
        /// </summary>
        /// <param name="so">The CorePartData's SerializedObject.</param>
        /// <param name="target">The CorePartData instance, used by the auto-detect button.</param>
        /// <returns>A bound Foldout with the section's content.</returns>
        public static VisualElement BuildAttachment(SerializedObject so, CorePartData target)
        {
            var foldout = MakeSectionFoldout("Attachment");
            foldout.Add(BuildAttachRulesField(so));

            var listBuilder = new AttachNodesListBuilder(so);
            foldout.Add(listBuilder.Build());

            var autoBtn = new Button(() =>
            {
                AttachNodeAutoGenerator.RegenerateFromHierarchy(target);
                so.Update();
                listBuilder.Refresh();
            })
            {
                text = "Auto-detect from GO",
                tooltip = "Replace the attach-node list with one entry per AttachmentNode component found in the prefab hierarchy.",
            };
            foldout.Add(autoBtn);

            AddField(foldout, so, "core.data.fuelCrossFeed");
            return Bound(foldout, so);
        }

        private static VisualElement BuildAttachRulesField(SerializedObject so)
        {
            var container = new VisualElement();
            container.AddToClassList("attach-rules-container");

            var header = new Label("Attach Rules");
            header.AddToClassList("attach-rules-header");
            container.Add(header);

            var grid = new VisualElement();
            grid.AddToClassList("attach-rules-grid");

            var rulesProp = so.FindProperty("core.data.attachRules");
            if (rulesProp != null)
            {
                AddRuleCell(grid, rulesProp.FindPropertyRelative("stack"));
                AddRuleCell(grid, rulesProp.FindPropertyRelative("srfAttach"));
                AddRuleCell(grid, rulesProp.FindPropertyRelative("allowStack"));
                AddRuleCell(grid, rulesProp.FindPropertyRelative("allowSrfAttach"));
                AddRuleCell(grid, rulesProp.FindPropertyRelative("allowCollision"));
                AddRuleCell(grid, rulesProp.FindPropertyRelative("allowDock"));
                AddRuleCell(grid, rulesProp.FindPropertyRelative("allowRotate"));
                AddRuleCell(grid, rulesProp.FindPropertyRelative("allowRoot"));
            }

            container.Add(grid);
            return container;
        }

        private static void AddRuleCell(VisualElement grid, SerializedProperty prop)
        {
            if (prop == null)
            {
                return;
            }
            var toggle = new Toggle(prop.displayName);
            toggle.BindProperty(prop);
            toggle.AddToClassList("attach-rules-cell");
            grid.Add(toggle);
        }

        /// <summary>
        /// Builds the Staging section (stageOffset, childStageOffset, stageType, inverseStageCarryover, stagingIconAssetAddress).
        /// </summary>
        /// <param name="so">The CorePartData's SerializedObject.</param>
        /// <returns>A bound Foldout with the section's PropertyFields.</returns>
        public static VisualElement BuildStaging(SerializedObject so)
        {
            var foldout = MakeSectionFoldout("Staging");
            AddField(foldout, so, "core.data.stageOffset");
            AddField(foldout, so, "core.data.childStageOffset");
            AddField(foldout, so, "core.data.stageType");
            AddField(foldout, so, "core.data.inverseStageCarryover");
            AddField(foldout, so, "core.data.stagingIconAssetAddress");
            return Bound(foldout, so);
        }

        /// <summary>
        /// Builds the Centers and Buoyancy section (CoM / CoL / CoP / buoyancy / displacement offsets and toggles).
        /// </summary>
        /// <param name="so">The CorePartData's SerializedObject.</param>
        /// <returns>A bound Foldout with the section's PropertyFields.</returns>
        public static VisualElement BuildCentersBuoyancy(SerializedObject so)
        {
            var foldout = MakeSectionFoldout("Centers & Buoyancy");
            AddField(foldout, so, "core.data.coMassOffset");
            AddField(foldout, so, "core.data.coLiftOffset");
            AddField(foldout, so, "core.data.coPressureOffset");
            AddField(foldout, so, "core.data.coBuoyancy");
            AddField(foldout, so, "core.data.coDisplacement");
            AddField(foldout, so, "core.data.buoyancy");
            AddField(foldout, so, "core.data.buoyancyUseSine");
            AddField(foldout, so, "core.data.buoyancyUseCubeNamed");
            return Bound(foldout, so);
        }

        /// <summary>
        /// Builds the Resources section. Fields here are slated to move into the future Resources tab.
        /// </summary>
        /// <param name="so">The CorePartData's SerializedObject.</param>
        /// <returns>A bound Foldout with the section's PropertyFields and a future-tab notice HelpBox.</returns>
        public static VisualElement BuildResources(SerializedObject so)
        {
            var foldout = MakeSectionFoldout("Resources");
            foldout.Add(new HelpBox(
                "These fields move to the Resources tab when that tab is built.",
                HelpBoxMessageType.Info));
            AddField(foldout, so, "core.data.resourceContainers");
            AddField(foldout, so, "core.data.resourceCosts");
            AddField(foldout, so, "core.data.HasReportStorage");
            return Bound(foldout, so);
        }

        /// <summary>
        /// Builds the OAB and Editor section (category and hide-mode flags for the part assembly editor).
        /// </summary>
        /// <param name="so">The CorePartData's SerializedObject.</param>
        /// <returns>A bound Foldout with the section's PropertyFields.</returns>
        public static VisualElement BuildOabEditor(SerializedObject so)
        {
            var foldout = MakeSectionFoldout("OAB & Editor");
            AddField(foldout, so, "core.data.oabEditorCategory");
            AddField(foldout, so, "core.data.partType");
            AddField(foldout, so, "core.data.partHideMode");
            AddField(foldout, so, "core.data.PreferredOrientation");
            AddField(foldout, so, "core.data.MirrorTechnique");
            AddField(foldout, so, "core.data.CanSuggestOrientation");
            AddField(foldout, so, "core.data.PickUpPointOffset");
            AddField(foldout, so, "core.data.PickupRotationPointOffset");
            AddField(foldout, so, "core.data.hideFromFlightPartsManager");
            AddField(foldout, so, "core.data.hideFromOABPartsManager");
            return Bound(foldout, so);
        }

        /// <summary>
        /// Builds the PAM Overrides section (Parts Action Menu sort and display overrides).
        /// </summary>
        /// <param name="so">The CorePartData's SerializedObject.</param>
        /// <returns>A bound Foldout with the section's PropertyFields.</returns>
        public static VisualElement BuildPamOverrides(SerializedObject so)
        {
            var foldout = MakeSectionFoldout("PAM Overrides");
            foldout.Add(new HelpBox(
                "These overrides move to the per-module editors (and the Variants editor for the variants module) when those are built.",
                HelpBoxMessageType.Info));
            AddField(foldout, so, "core.data.PAMModuleSortOverride");
            AddField(foldout, so, "core.data.PAMModuleVisualsOverride");
            return Bound(foldout, so);
        }

        private static Foldout MakeSectionFoldout(string title)
        {
            var foldout = new Foldout { text = title, value = true };
            foldout.AddToClassList("sdk-section");
            return foldout;
        }

        private static void AddField(VisualElement parent, SerializedObject so, string path)
        {
            var prop = so.FindProperty(path);
            if (prop != null)
            {
                parent.Add(new PropertyField(prop));
            }
        }

        private static VisualElement Bound(VisualElement element, SerializedObject so)
        {
            element.Bind(so);
            return element;
        }
    }
}
