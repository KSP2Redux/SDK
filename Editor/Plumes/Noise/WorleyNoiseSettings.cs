using UnityEngine;

namespace Ksp2UnityTools.Editor.Plumes.Noise
{
    [CreateAssetMenu]
    public class WorleyNoiseSettings : ScriptableObject
    {
        public int Seed;
        [Range(1, 50)] public int NumDivisionsA = 5;
        [Range(1, 50)] public int NumDivisionsB = 10;
        [Range(1, 50)] public int NumDivisionsC = 15;

        public float Persistence = .5f;
        public int Tile = 1;
        public bool Invert = true;
    }
}