using System.Collections.Generic;
using System.IO;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.CustomEditors
{
    [CustomEditor(typeof(BiomeLookupUtility))]
    public class BiomeLookupUtilityEditor : UnityEditor.Editor
    {
        private static PersistentDictionary _hashMapNames;
        private static PersistentDictionary _lutNames;

        private static PersistentDictionary HashMapNames =>
            _hashMapNames ??= KSP2UnityToolsManager.GetDictionary("HashMapNames");

        private static PersistentDictionary LutNames => _lutNames ??= KSP2UnityToolsManager.GetDictionary("LUTNames");
        private BiomeLookupUtility Target => target as BiomeLookupUtility;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            GUILayout.Label("Hash Map Saving", EditorStyles.boldLabel);
            string hashMapName = HashMapNames.TryGetValue(Target.name, out string newHashMapName)
                ? newHashMapName
                : $"{Target.name} Hashmap";
            HashMapNames[Target.name] = hashMapName = EditorGUILayout.TextField("Hash Map Name", hashMapName);
            string lutName = LutNames.TryGetValue(Target.name, out string newLutName)
                ? newLutName
                : $"{Target.name} LUT";
            LutNames[Target.name] = lutName = EditorGUILayout.TextField("LUT Name", lutName);
            if (GUILayout.Button("Bake"))
            {
                try
                {
                    string path = AssetDatabase.GetAssetPath(Target);
                    if (path == "")
                    {
                        path = "Assets";
                    }
                    else if (Path.GetExtension(path) != "")
                    {
                        path = path.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");
                    }

                    var lut = CreateInstance<BiomeTextureColorLookupTable>();
                    lut.BiomeLookupPairs = new List<BiomeLookupEditorPair>();
                    var hashmap = CreateInstance<BiomeLookupHashTable>();

                    for (int i = 0; i < Target.colorMapping.Count; i++)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Baking Biome Map",
                            $"Building Color LUT {i + 1}/{Target.colorMapping.Count}",
                            (i + 1) / (float)Target.colorMapping.Count
                        );
                        lut.BiomeLookupPairs.Add(
                            new BiomeLookupEditorPair
                            {
                                name = Target.colorMapping[i].name,
                                color = Target.colorMapping[i].color
                            }
                        );
                    }

                    if (File.Exists(path + $"/{lutName}.asset"))
                    {
                        AssetDatabase.DeleteAsset(path + $"/{lutName}.asset");
                    }

                    AssetDatabase.CreateAsset(lut, path + $"/{lutName}.asset");
                    for (int y = 0; y < 256; y++)
                    {
                        int baseIndex = y * 256;

                        for (int x = 0; x < 256; x++)
                        {
                            int index = baseIndex + x;
                            EditorUtility.DisplayProgressBar(
                                "Baking Biome Map",
                                $"Building Hashmap (x = {x}/255, y = {y}/255)",
                                index / 65535.0f
                            );
                            hashmap.Cells[index] = new BiomeLookupHashCell
                            {
                                BiomeChunks = Target.GetCell(x, y)
                            };
                        }
                    }

                    if (File.Exists(path + $"/{hashMapName}.asset"))
                    {
                        AssetDatabase.DeleteAsset(path + $"/{hashMapName}.asset");
                    }

                    AssetDatabase.CreateAsset(hashmap, path + $"/{hashMapName}.asset");
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                    AssetDatabase.Refresh();
                }
            }
        }
    }
}