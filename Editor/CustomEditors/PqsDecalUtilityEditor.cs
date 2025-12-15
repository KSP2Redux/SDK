using System.IO;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UniLinq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.CustomEditors
{
    [CustomEditor(typeof(PqsDecalUtility))]
    public class PqsDecalUtilityEditor : UnityEditor.Editor
    {
        private static PersistentDictionary _pqsDecalDataNames;

        private static PersistentDictionary PqsDecalNames =>
            _pqsDecalDataNames ??= KSP2UnityToolsManager.GetDictionary("PqsDecalNames");

        private PqsDecalUtility Target => target as PqsDecalUtility;

        private static (ushort[] data, int width, int height) GetDataFrom(Texture2D texture16)
        {
            NativeArray<ushort> rawData = texture16.GetRawTextureData<ushort>();
            ushort[] data = new ushort[texture16.width * texture16.height];
            rawData.CopyTo(data);
            return (data, texture16.width, texture16.height);
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            GUILayout.Label("PQS Decal Data Baking", EditorStyles.boldLabel);
            string dataName = PqsDecalNames.TryGetValue(Target.name, out string newDataName)
                ? newDataName
                : "NewPQSData";
            PqsDecalNames[Target.name] = dataName = EditorGUILayout.TextField("Data Asset Name", dataName);
            if (GUILayout.Button("Bake"))
            {
                var decalData = CreateInstance<PQSDecalData>();
                decalData.BakedPqsDecalList = Target.decals;
                decalData.BakedPqsDecalIDList = Target.decals.Select(x => x.DecalID).ToList();
                decalData.DiffuseTextureArray = Target.diffuse;
                decalData.NormalTextureArray = Target.normal;
                decalData.AlphaMaskTextureArray = Target.alphaMask;
                decalData.PeakTextureArray = Target.peak;
                decalData.SlopeTextureArray = Target.slope;
                decalData.Count = Target.decals.Count;
                (decalData.HeightData, decalData.HeightWidth, decalData.HeightHeight) = GetDataFrom(Target.heightData);
                (decalData.AlphaData, decalData.AlphaWidth, decalData.AlphaHeight) = GetDataFrom(Target.alphaData);
                string path = AssetDatabase.GetAssetPath(Target);
                if (path == "")
                {
                    path = "Assets";
                }
                else if (Path.GetExtension(path) != "")
                {
                    path = path.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");
                }

                if (File.Exists($"{path}/{dataName}.asset"))
                {
                    AssetDatabase.DeleteAsset($"{path}/{dataName}.asset");
                }

                AssetDatabase.CreateAsset(decalData, $"{path}/{dataName}.asset");
                AssetDatabase.Refresh();
            }
        }
    }
}