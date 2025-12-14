using Redux.VFX.Plume;
using Redux.VFX.Plume.Configs;
using Newtonsoft.Json.Linq;
using Redux.VFX.Plume.Services;
using UnityEngine;

namespace Ksp2UnityTools.Editor.Plumes.Utility
{
    public static class PlumeExtensions
    {
        private static IPlumeLogger Logger => ServiceProvider.GetService<IPlumeLogger>();
        private static IAssetManager AssetManager => ServiceProvider.GetService<IAssetManager>();

        public static Material GetEditorMaterial(this PlumeComponentConfig config)
        {
            if (AssetManager.GetShader(config.ShaderSettings.ShaderName) is not { } shader)
            {
                Logger.LogError($"Couldn't find shader {config.ShaderSettings.ShaderName}");
                return null;
            }

            var material = new Material(shader);

            foreach ((string param, object value) in config.ShaderSettings.ShaderParams)
            {
                if (value is JObject jobject)
                {
                    SetJObjectParam(jobject, material, param);
                }
                else
                {
                    SetGeneralParam(value, material, param);
                }
            }

            return material;
        }

        private static void SetJObjectParam(JObject jobject, Material material, string param)
        {
            if (jobject.ContainsKey("r"))
            {
                var color = new Color(
                    jobject["r"].ToObject<float>(),
                    jobject["g"].ToObject<float>(),
                    jobject["b"].ToObject<float>(),
                    jobject["a"].ToObject<float>()
                );
                material.SetColor(param, color);
            }
            else if (jobject.ContainsKey("x"))
            {
                Vector4 vector = Vector4.zero;

                vector.x = jobject["x"].ToObject<float>();
                vector.y = jobject["y"].ToObject<float>();

                if (jobject.ContainsKey("z"))
                {
                    vector.z = jobject["z"].ToObject<float>();
                }

                if (jobject.ContainsKey("w"))
                {
                    vector.w = jobject["w"].ToObject<float>();
                }

                material.SetVector(param, vector);
            }
        }

        private static void SetGeneralParam(object value, Material material, string param)
        {
            switch (value)
            {
                case Color color:
                    material.SetColor(param, color);
                    break;
                case Vector2 vector2:
                    material.SetVector(param, vector2);
                    break;
                case Vector3 vector3:
                    material.SetVector(param, vector3);
                    break;
                case Vector4 vector4:
                    material.SetVector(param, vector4);
                    break;
                case float number:
                    material.SetFloat(param, number);
                    break;
                case double dnumber:
                    material.SetFloat(param, (float)dnumber);
                    break;
                case int integer:
                    material.SetFloat(param, integer);
                    break;
                case string textureName:
                    material.SetTexture(param, AssetManager.GetAsset<Texture>(textureName));
                    break;
            }
        }
    }
}