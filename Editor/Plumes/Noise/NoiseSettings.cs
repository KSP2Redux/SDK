using System;
using UnityEngine;

namespace Redux.VFX.Plumes.Editor.Noise
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