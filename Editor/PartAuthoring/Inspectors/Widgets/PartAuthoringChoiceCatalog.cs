using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KSP.OAB;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Static stock/reference values used by part-authoring autocomplete fields.
    /// </summary>
    /// <remarks>
    /// This is for stable, hardcoded authoring choices. Autocomplete values that come from the
    /// currently edited part, scene hierarchy, reflection, or other live context should stay near
    /// the editor that owns that context.
    /// </remarks>
    public static class PartAuthoringChoiceCatalog
    {
        private static readonly List<string> Families = new()
        {
            "0000-Pod",
            "0010-Probe",
            "0020-Cockpit",
            "0025-Station",
            "0030-Rover",
            "0040-Methalox",
            "0050-Methane",
            "0060-Monopropellant",
            "0070-Xenon",
            "0072-Argon",
            "0074-Lithium",
            "0080-Hydrogen",
            "0085-Ore",
            "0090-Fuel Line",
            "0100-Methalox",
            "0110-Solid Fuel Booster",
            "0120-Jet Engine",
            "0130-Monopropellant",
            "0140-Xenon",
            "0142-Argon",
            "0144-Lithium",
            "0150-Hydrogen",
            "0160-Strut",
            "0170-Clamp",
            "0180-Engine Mount",
            "0190-Adapter",
            "0200-Beam",
            "0210-Body",
            "0220-Panel",
            "0230-Hub",
            "0240-Truss",
            "0250-Truss Adapter",
            "0260-Truss Resizer",
            "0270-Tube",
            "0280-Stack Decoupler",
            "0290-Stack Separator",
            "0300-Radial Decoupler",
            "0310-Docking Port",
            "0320-Fairing",
            "0330-Cargo Bay",
            "0340-Crew Cabin",
            "0350-Truss",
            "0360-Nose Cone",
            "0370-Intake",
            "0380-Wing",
            "0390-Stabilizer",
            "0400-Control Surface",
            "0410-Tail Section",
            "0420-Landing Leg",
            "0430-Landing Gear",
            "0440-Wheel",
            "0450-Heat Shield",
            "0460-Radiator",
            "0470-Battery",
            "0480-Solar Array",
            "0490-Generator",
            "0500-Antenna",
            "0505-LifeSupport",
            "0510-Parachute",
            "0520-RCS",
            "0530-Stabilizer",
            "0540-Light",
            "0550-Ladder",
            "0560-Resource Gathering",
            "0560-Science Collector",
            "Camera",
            "Experiment",
            "Factory",
            "Scanner",
            "Service Bay",
            "UniversalContainer",
        };

        private static readonly string[] StagingIconAddresses =
        {
            "staging_booster.png",
            "staging_decoupler.png",
            "staging_fairing.png",
            "staging_icons_temp/staging_fairing.png",
            "staging_parachute.png",
            "staging_rcs.png",
            "staging_separation.png",
            "staging_solid-booster.png",
            "staging_truss01.png",
            "staging_truss02.png",
            "Staging-Engines/Staging-ICO-Jet-Airs.png",
            "Staging-Engines/Staging-ICO-Metallic.png",
            "Staging-Engines/Staging-ICO-Methalox.png",
            "Staging-Engines/Staging-ICO-Monoprop.png",
            "Staging-Engines/Staging-ICO-Xenons.png",
            "Staging-ICO-Antimatter.png",
            "Staging-ICO-Booster.png",
            "Staging-ICO-Fusion.png",
            "Staging-ICO-Chute.png",
            "Staging-ICO-Jet-Airs.png",
            "Staging-ICO-Liquid.png",
            "Staging-ICO-Metallic.png",
            "Staging-ICO-Methalox.png",
            "Staging-ICO-Monoprop.png",
            "Staging-ICO-Nuclear-Pulse.png",
            "Staging-ICO-Nuclear-Saltwater.png",
            "Staging-ICO-Port.png",
            "Staging-ICO-Radial.png",
            "Staging-ICO-Solid-Fuels.png",
            "Staging-ICO-Structure01.png",
            "Staging-ICO-Xenons.png",
            "Staging-Icon-Inline.png",
            "Staging-Icon-Thruster.png",
        };

        private static readonly string[] StockAttachNodeIds =
        {
            "angle",
            "back",
            "bottom",
            "docking",
            "front",
            "left",
            "right",
            "srf",
            "srfAttach",
            "surface",
            "top",
        };

        /// <summary>
        /// Returns the canonical list of known families.
        /// </summary>
        /// <remarks>
        /// Stable order, safe to enumerate repeatedly.
        /// </remarks>
        /// <returns>The canonical family list.</returns>
        public static IReadOnlyList<string> GetKnownFamilies() => Families;

        /// <summary>
        /// Returns stock staging icon address strings used by part staging data.
        /// </summary>
        public static IReadOnlyList<string> GetStagingIconAddresses() => StagingIconAddresses;

        /// <summary>
        /// Returns known part and attach-node size keys. Authors may still type custom keys.
        /// </summary>
        public static IReadOnlyList<string> GetKnownSizeKeys() => PartSizeRegistry.Definitions
            .Select(definition => definition.Key)
            .ToList();

        /// <summary>
        /// Returns a compact diameter label for a known size key, or an empty string for custom keys.
        /// </summary>
        public static string GetKnownSizeKeyDetail(string sizeKey)
        {
            return PartSizeRegistry.TryGet(sizeKey, out PartSizeDefinition definition)
                ? string.Format(CultureInfo.InvariantCulture, "{0:0.####} m diameter", definition.Diameter)
                : string.Empty;
        }

        /// <summary>
        /// Returns common stock attach-node IDs without numeric per-part suffix variants.
        /// </summary>
        public static IReadOnlyList<string> GetStockAttachNodeIds() => StockAttachNodeIds;
    }
}
