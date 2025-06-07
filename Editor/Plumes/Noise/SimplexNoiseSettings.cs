using System;
using UnityEngine;

namespace Redux.VFX.Plumes.Editor.Noise
{
    [CreateAssetMenu]
    public class SimplexNoiseSettings : NoiseSettings
    {
        public int Seed;
        [Range(1, 6)] public int NumLayers = 1;
        public float Scale = 1;
        public float Lacunarity = 2;
        public float Persistence = .5f;
        public Vector2 Offset;

        public override Array GetDataArray()
        {
            var data = new DataStruct
            {
                Seed = Seed,
                NumLayers = Mathf.Max(1, NumLayers),
                Scale = Scale,
                Lacunarity = Lacunarity,
                Persistence = Persistence,
                Offset = Offset
            };

            return new[] { data };
        }

        public struct DataStruct
        {
            public int Seed;
            public int NumLayers;
            public float Scale;
            public float Lacunarity;
            public float Persistence;
            public Vector2 Offset;
        }

        public override int Stride => sizeof(float) * 7;
    }
}