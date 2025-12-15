using System;
using UnityEngine;

namespace Ksp2UnityTools.Editor.Plumes.Noise
{
    public abstract class NoiseSettings : ScriptableObject
    {
        public event Action OnValueChanged;

        public abstract Array GetDataArray();

        public abstract int Stride { get; }

        private void OnValidate()
        {
            OnValueChanged?.Invoke();
        }
    }
}