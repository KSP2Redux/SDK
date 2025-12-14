using System;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.Plumes.Utility
{
    public static class ShaderUtility
    {
        public static string[] GetShaderPropertyNames(Shader shader)
        {
            if (shader == null)
            {
                return Array.Empty<string>();
            }

            int propertyCount = ShaderUtil.GetPropertyCount(shader);
            string[] propertyNames = new string[propertyCount];

            for (int i = 0; i < propertyCount; i++)
            {
                string propertyName = ShaderUtil.GetPropertyName(shader, i);
                propertyNames[i] = propertyName;
            }

            return propertyNames;
        }
    }
}