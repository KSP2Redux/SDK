using System.Globalization;
using System.Text;
using KSP;
using KSP.Sim;
using Ksp2UnityTools.Editor.Modding;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Editor-only orbit for a body, and the Patch Manager patch that adds the body to a galaxy with it.
    /// </summary>
    /// <remarks>
    /// A body's orbit is not in its JSON. The game reads it from the galaxy definition, which a body joins through a
    /// patch. In a mod this sidecar is a <see cref="TextAssetGenerator" />, so the build writes the patch into the mod,
    /// where SpaceWarp runs it as a mod script. In a project without a mod the planet inspector writes the same patch
    /// beside the body as a checked-in Lua file.
    /// </remarks>
    public class CelestialBodyOrbitAuthoring : TextAssetGenerator
    {
        /// <summary>
        /// The Scaled prefab of the body this orbit belongs to.
        /// </summary>
        public GameObject ScaledPrefab;

        /// <summary>
        /// The key of the galaxy definition the body is added to.
        /// </summary>
        public string GalaxyDefinitionKey = SerializedSavedGame.DEFAULT_GALAXY_DEFINITION_KEY;

        /// <summary>
        /// The name of the body this one orbits, as the galaxy knows it.
        /// </summary>
        public string ParentBody = string.Empty;

        /// <summary>
        /// The semi-major axis in meters.
        /// </summary>
        public double SemiMajorAxis = 1_000_000.0;

        /// <summary>
        /// The eccentricity, from zero for a circle up to but not including one.
        /// </summary>
        public double Eccentricity;

        /// <summary>
        /// The inclination in degrees.
        /// </summary>
        public double Inclination;

        /// <summary>
        /// The longitude of the ascending node in degrees.
        /// </summary>
        public double LongitudeOfAscendingNode;

        /// <summary>
        /// The argument of periapsis in degrees.
        /// </summary>
        public double ArgumentOfPeriapsis;

        /// <summary>
        /// The mean anomaly at the epoch in radians.
        /// </summary>
        public double MeanAnomalyAtEpoch;

        /// <summary>
        /// The epoch in universal seconds.
        /// </summary>
        public double Epoch;

        /// <summary>
        /// The colour of the orbit line in map view.
        /// </summary>
        public Color OrbitColor = new(0.86f, 0.38f, 0.18f, 0.03f);

        /// <summary>
        /// The colour of the orbit's nodes in map view.
        /// </summary>
        public Color NodeColor = new(0.86f, 0.38f, 0.18f, 0.05f);

        /// <summary>
        /// The camera distance, as a multiple of the semi-major axis, below which the orbit line fades.
        /// </summary>
        public float LowerCamVsSmaRatio;

        /// <summary>
        /// The camera distance, as a multiple of the semi-major axis, above which the orbit line fades.
        /// </summary>
        public float UpperCamVsSmaRatio;

        /// <summary>
        /// Whether the orbit line's texture offset is worked out automatically.
        /// </summary>
        public bool AutoTextureOffset;

        /// <summary>
        /// The orbit line's texture offset, when it is not automatic.
        /// </summary>
        public float TextureOffset;

        /// <summary>
        /// Gets the name of the body this orbit belongs to, or null when its prefab is missing.
        /// </summary>
        public string BodyName
        {
            get
            {
                CoreCelestialBodyData body = ScaledPrefab != null ? ScaledPrefab.GetComponent<CoreCelestialBodyData>() : null;
                string bodyName = body != null ? body.Core?.data?.bodyName : null;
                return string.IsNullOrEmpty(bodyName) ? null : bodyName;
            }
        }

        /// <inheritdoc />
        public override bool ShouldGenerate => BodyName != null && !string.IsNullOrEmpty(ParentBody);

        /// <inheritdoc />
        public override string PathInMod => $"patches/{PatchFileName}";

        /// <summary>
        /// Gets the file name of the patch, such as AddDrast.lua.
        /// </summary>
        public string PatchFileName => $"Add{BodyName}.lua";

        /// <inheritdoc />
        /// <remarks>
        /// The patch runs in the early pass and first bucket, so every other patch to the galaxy, a mod's own
        /// included, already sees the body.
        /// </remarks>
        public override string Generate()
        {
            string bodyName = BodyName;
            CoreCelestialBodyData body = ScaledPrefab.GetComponent<CoreCelestialBodyData>();
            string prefabKey = body.Core.data.assetKeySimulation ?? string.Empty;
            if (prefabKey.EndsWith(".prefab"))
            {
                prefabKey = prefabKey.Substring(0, prefabKey.Length - ".prefab".Length);
            }

            // The stock galaxy keeps its own patch call, so patches written before galaxies could be chosen stay current
            bool isDefaultGalaxy = string.IsNullOrEmpty(GalaxyDefinitionKey) ||
                GalaxyDefinitionKey == SerializedSavedGame.DEFAULT_GALAXY_DEFINITION_KEY;
            string galaxyDescription = isDefaultGalaxy ? "the default galaxy" : GalaxyDefinitionKey;
            string patchCall = isDefaultGalaxy
                ? $"PM.Planets:PatchDefaultGalaxy(\"Add{bodyName}\")"
                : $"PM.Planets:PatchGalaxy(\"{GalaxyDefinitionKey}\", \"Add{bodyName}\")";

            var lua = new StringBuilder();
            lua.AppendLine($"-- Adds {bodyName} to {galaxyDescription}. Written by the planet inspector from {name}, so edit the orbit there.");
            lua.AppendLine($"{patchCall}:Early():First():Do(function(galaxy)");
            lua.AppendLine($"    galaxy:Add(\"{bodyName}\", function(body)");
            lua.AppendLine($"        body.PrefabKey = \"{prefabKey}\"");
            lua.AppendLine($"        body.referenceBodyGuid = \"{ParentBody}\"");
            lua.AppendLine("        body.OrbitProperties = {");
            lua.AppendLine($"            referenceBodyGuid = \"{ParentBody}\",");
            lua.AppendLine($"            inclination = {Number(Inclination)},");
            lua.AppendLine($"            eccentricity = {Number(Eccentricity)},");
            lua.AppendLine($"            semiMajorAxis = {Number(SemiMajorAxis)},");
            lua.AppendLine($"            longitudeOfAscendingNode = {Number(LongitudeOfAscendingNode)},");
            lua.AppendLine($"            argumentOfPeriapsis = {Number(ArgumentOfPeriapsis)},");
            lua.AppendLine($"            meanAnomalyAtEpoch = {Number(MeanAnomalyAtEpoch)},");
            lua.AppendLine($"            epoch = {Number(Epoch)},");
            lua.AppendLine("        }");
            lua.AppendLine("        body.OrbiterProperties = {");
            lua.AppendLine($"            orbitColor = {LuaColor(OrbitColor)},");
            lua.AppendLine($"            nodeColor = {LuaColor(NodeColor)},");
            lua.AppendLine($"            lowerCamVsSmaRatio = {Number(LowerCamVsSmaRatio)},");
            lua.AppendLine($"            upperCamVsSmaRatio = {Number(UpperCamVsSmaRatio)},");
            lua.AppendLine($"            autoTextureOffset = {(AutoTextureOffset ? "true" : "false")},");
            lua.AppendLine($"            textureOffset = {Number(TextureOffset)},");
            lua.AppendLine("        }");
            lua.AppendLine("    end)");
            lua.AppendLine("end)");
            return lua.ToString();
        }

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        // A float widened to double prints its binary error, 0.03 as 0.029999999329447746, so floats print as floats.
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string LuaColor(Color color) =>
            $"{{ r = {Number(color.r)}, g = {Number(color.g)}, b = {Number(color.b)}, a = {Number(color.a)} }}";
    }
}
