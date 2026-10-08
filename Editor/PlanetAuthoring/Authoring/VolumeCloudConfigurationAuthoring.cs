using System.Collections.Generic;
using Ksp2UnityTools.Editor.PlanetAuthoring.Clouds;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Editor-only state for a <see cref="KSP.VolumeCloud.VolumeCloudConfiguration" />.
    /// </summary>
    public class VolumeCloudConfigurationAuthoring : ScriptableObject
    {
        /// <summary>
        /// The scaled cloud configuration whose layers mirror this volumetric configuration.
        /// </summary>
        public ScaledCloudConfiguration ScaledConfiguration;

        /// <summary>
        /// The configuration the Low cloud quality tier loads: a mirror of this one that draws scaled clouds only, as
        /// stock's Low tiers do.
        /// </summary>
        public KSP.VolumeCloud.VolumeCloudConfiguration LowConfiguration;

        /// <summary>
        /// The settings each layer's distribution map was last generated from, matched by layer name.
        /// </summary>
        public List<CloudDistributionSettings> Distributions = new();

        /// <summary>
        /// Gets the distribution settings for a layer, adding defaults when the layer has none yet.
        /// </summary>
        /// <param name="layerName">The layer's name.</param>
        /// <returns>The layer's settings.</returns>
        public CloudDistributionSettings GetDistribution(string layerName)
        {
            foreach (CloudDistributionSettings settings in Distributions)
            {
                if (settings != null && settings.LayerName == layerName)
                    return settings;
            }

            var created = new CloudDistributionSettings { LayerName = layerName };
            Distributions.Add(created);
            return created;
        }
    }
}
