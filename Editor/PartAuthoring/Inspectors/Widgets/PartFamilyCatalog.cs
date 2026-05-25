using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Static list of known part-family values in their canonical sort order.
    /// </summary>
    /// <remarks>
    /// Mirrors the family taxonomy KSP2's Parts Manager uses. The four-digit numeric prefix drives sort order in the parts picker. Authors should pick from this list rather than invent new families to keep the picker grouping coherent. Adding a new family is a small edit here followed by sharing the new value with anyone else authoring parts. Entries with the same prefix (for example <c>0560-Resource Gathering</c> and <c>0560-Science Collector</c>) are intentional siblings sharing a sort bucket.
    /// </remarks>
    public static class PartFamilyCatalog
    {
        private static readonly List<string> _families = new()
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
        };

        /// <summary>
        /// Returns the canonical list of known families.
        /// </summary>
        /// <remarks>
        /// Stable order, safe to enumerate repeatedly.
        /// </remarks>
        /// <returns>The canonical family list.</returns>
        public static IReadOnlyList<string> GetKnownFamilies() => _families;
    }
}
