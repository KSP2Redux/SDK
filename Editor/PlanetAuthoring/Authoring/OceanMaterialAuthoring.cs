using System;
using System.Collections.Generic;
using Ksp2UnityTools.Editor.PlanetAuthoring.Ocean;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Editor-only state for an ocean material: the settings each of its generated textures was last made from.
    /// </summary>
    /// <remarks>
    /// The defaults follow the character of stock's textures: stretched ripples for the detail normals, a faint
    /// undulation for the large ones, and a finer near foam over a broader far foam.
    /// </remarks>
    public class OceanMaterialAuthoring : ScriptableObject
    {
        /// <summary>
        /// The settings of the caustics texture.
        /// </summary>
        public OceanCausticsSettings Caustics = new();

        /// <summary>
        /// The settings of the foam shape seen up close.
        /// </summary>
        public OceanFoamSettings Foam = new();

        /// <summary>
        /// The settings of the foam shape seen from further away.
        /// </summary>
        public OceanFoamSettings FarFoam = new() { Seed = 2, Cells = 5, Coverage = 0.62f, Contrast = 1.4f };

        /// <summary>
        /// The settings of the detail normal map.
        /// </summary>
        public OceanNormalSettings DetailNormal = new() { CellsX = 3, CellsY = 12, Bumpiness = 0.26f };

        /// <summary>
        /// The settings of the large normal map, used for both of the material's large normal layers.
        /// </summary>
        public OceanNormalSettings LargeNormal = new() { Seed = 3, CellsX = 6, CellsY = 6, Bumpiness = 0.07f, Resolution = 1024 };

        /// <summary>
        /// The sea's colour from orbit, painted into the body's scaled albedo wherever there is sea.
        /// </summary>
        /// <remarks>
        /// The water fades into the scaled albedo with altitude, so this is also what the ocean looks like from high up.
        /// Stock Kerbin paints its sea one flat colour, sRGB (34, 56, 77), which new oceans start from. The ocean
        /// material's own colours are lit-water parameters and do not give it.
        /// </remarks>
        [Tooltip("The sea's colour from orbit, painted into the scaled albedo wherever there is sea. The water fades into it with altitude. Rebake Body Surface with Scaled Textures ticked to apply it.")]
        public Color ScaledOceanColor = OceanPresets.KERBIN_SCALED_OCEAN_COLOR;

        /// <summary>
        /// The fingerprint of the terrain and sea level the shoreline mask was last made from, compared against the
        /// current ones to report a stale mask.
        /// </summary>
        public string ShorelineFingerprint;

        /// <summary>
        /// The fade-in opacities of scaled-space maps the body has not baked yet, held at zero on the material until
        /// the map exists.
        /// </summary>
        public List<HeldFade> HeldScaledFades = new();

        /// <summary>
        /// A scaled-space map's fade-in opacities, kept while the material's are held at zero.
        /// </summary>
        [Serializable]
        public class HeldFade
        {
            /// <summary>
            /// The property prefix the opacities share, such as <c>_ScaleAlbedo</c>.
            /// </summary>
            public string Prefix;

            /// <summary>
            /// The opacity where the fade starts.
            /// </summary>
            public float Start;

            /// <summary>
            /// The opacity where the fade ends.
            /// </summary>
            public float End;
        }
    }
}
