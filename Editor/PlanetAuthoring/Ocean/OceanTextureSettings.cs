using System;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Ocean
{
    /// <summary>
    /// The settings an ocean material's caustics texture is generated from.
    /// </summary>
    [Serializable]
    public class OceanCausticsSettings
    {
        /// <summary>
        /// Picks a different pattern with the same character.
        /// </summary>
        public int Seed = 1;

        /// <summary>
        /// How many light cells fit across the texture.
        /// </summary>
        [Tooltip("How many light cells fit across the texture.")]
        [Range(1, 32)] public int Cells = 5;

        /// <summary>
        /// How far the cells are bent out of shape.
        /// </summary>
        [Tooltip("How far the cells are bent out of shape.")]
        [Range(0f, 2f)] public float Warp = 0.35f;

        /// <summary>
        /// How thin and bright the lines of focused light are.
        /// </summary>
        [Tooltip("How thin and bright the lines of focused light are.")]
        [Range(1f, 40f)] public float Sharpness = 9f;

        /// <summary>
        /// How far the red and blue lines split from the green, as a share of the texture.
        /// </summary>
        [Tooltip("How far the red and blue lines split from the green, as a share of the texture.")]
        [Range(0f, 0.02f)] public float Fringe = 0.004f;

        /// <summary>
        /// The edge length of the texture, in texels.
        /// </summary>
        public int Resolution = 512;
    }

    /// <summary>
    /// The settings an ocean material's foam shape texture is generated from.
    /// </summary>
    [Serializable]
    public class OceanFoamSettings
    {
        /// <summary>
        /// Picks a different pattern with the same character.
        /// </summary>
        public int Seed = 1;

        /// <summary>
        /// How many broad patches fit across the texture. Bubble clumps are finer.
        /// </summary>
        [Tooltip("How many broad patches fit across the texture. Bubble clumps are finer.")]
        [Range(1, 32)] public int Cells = 8;

        /// <summary>
        /// How many octaves of finer mottling are layered on.
        /// </summary>
        [Range(1, 8)] public int Octaves = 5;

        /// <summary>
        /// How strongly each finer octave shows.
        /// </summary>
        [Range(0.1f, 0.9f)] public float Roughness = 0.55f;

        /// <summary>
        /// How far the patches are bent out of shape.
        /// </summary>
        [Range(0f, 2f)] public float Warp = 0.5f;

        /// <summary>
        /// The texture's average brightness, how much of a foam patch reads as foam.
        /// </summary>
        [Tooltip("The texture's average brightness, how much of a foam patch reads as foam. Stock's is about 0.6 to 0.7.")]
        [Range(0f, 1f)] public float Coverage = 0.7f;

        /// <summary>
        /// How sharply bright foam separates from the gaps in it.
        /// </summary>
        [Range(0.1f, 4f)] public float Contrast = 1.6f;

        /// <summary>
        /// The edge length of the texture, in texels.
        /// </summary>
        public int Resolution = 2048;
    }

    /// <summary>
    /// The settings an ocean material's normal map is generated from.
    /// </summary>
    [Serializable]
    public class OceanNormalSettings
    {
        /// <summary>
        /// Picks a different pattern with the same character.
        /// </summary>
        public int Seed = 1;

        /// <summary>
        /// How many waves fit across the texture horizontally.
        /// </summary>
        [Tooltip("How many waves fit across the texture horizontally. Fewer than vertically stretches them into ripples.")]
        [Range(1, 64)] public int CellsX = 4;

        /// <summary>
        /// How many waves fit across the texture vertically.
        /// </summary>
        [Tooltip("How many waves fit across the texture vertically.")]
        [Range(1, 64)] public int CellsY = 4;

        /// <summary>
        /// How many octaves of finer waves are layered on.
        /// </summary>
        [Range(1, 8)] public int Octaves = 5;

        /// <summary>
        /// How strongly each finer octave shows.
        /// </summary>
        [Range(0.1f, 0.9f)] public float Roughness = 0.5f;

        /// <summary>
        /// How far the waves are bent out of shape.
        /// </summary>
        [Range(0f, 2f)] public float Warp = 0.3f;

        /// <summary>
        /// How far the normals tilt on average, as the spread of their sideways components.
        /// </summary>
        [Tooltip("How far the normals tilt on average. Stock's detail normals spread about 0.26, its large normals about 0.07.")]
        [Range(0.01f, 1f)] public float Bumpiness = 0.25f;

        /// <summary>
        /// The edge length of the texture, in texels.
        /// </summary>
        public int Resolution = 512;
    }
}
