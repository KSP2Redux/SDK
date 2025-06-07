using UnityEngine;
using UnityEngine.SceneManagement;

namespace Redux.VFX.Plumes.Editor.Noise.Save
{
    public class Save3D : MonoBehaviour
    {
        private const int ThreadGroupSize = 32;
        public ComputeShader Slicer;

        public void Save(RenderTexture volumeTexture, string saveName)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            saveName = sceneName + "_" + saveName;
            int resolution = volumeTexture.width;
            var slices = new Texture2D[resolution];

            Slicer.SetInt("resolution", resolution);
            Slicer.SetTexture(0, "volumeTexture", volumeTexture);

            for (int layer = 0; layer < resolution; layer++)
            {
                var slice = new RenderTexture(resolution, resolution, 0)
                {
                    dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                    enableRandomWrite = true
                };
                slice.Create();

                Slicer.SetTexture(0, "slice", slice);
                Slicer.SetInt("layer", layer);
                int numThreadGroups = Mathf.CeilToInt(resolution / (float)ThreadGroupSize);
                Slicer.Dispatch(0, numThreadGroups, numThreadGroups, 1);

                slices[layer] = ConvertFromRenderTexture(slice);
            }

            var x = Tex3DFromTex2DArray(slices, resolution);
            UnityEditor.AssetDatabase.CreateAsset(x, "Assets/Resources/" + saveName + ".asset");
        }

        private static Texture3D Tex3DFromTex2DArray(Texture2D[] slices, int resolution)
        {
            var tex3D = new Texture3D(resolution, resolution, resolution, TextureFormat.ARGB32, false)
            {
                filterMode = FilterMode.Trilinear
            };
            Color[] outputPixels = tex3D.GetPixels();

            for (int z = 0; z < resolution; z++)
            {
                Color c = slices[z].GetPixel(0, 0);
                Color[] layerPixels = slices[z].GetPixels();
                for (int x = 0; x < resolution; x++)
                for (int y = 0; y < resolution; y++)
                {
                    outputPixels[x + resolution * (y + z * resolution)] = layerPixels[x + y * resolution];
                }
            }

            tex3D.SetPixels(outputPixels);
            tex3D.Apply();

            return tex3D;
        }

        private static Texture2D ConvertFromRenderTexture(RenderTexture rt)
        {
            var output = new Texture2D(rt.width, rt.height);
            RenderTexture.active = rt;
            output.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            output.Apply();
            return output;
        }
    }
}