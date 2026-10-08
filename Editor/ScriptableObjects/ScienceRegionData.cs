using System;
using System.Collections.Generic;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.API;
using Newtonsoft.Json;
using UniLinq;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.ScriptableObjects
{
    public class ScienceRegionData : ScriptableObject
    {
        [MenuItem("Assets/Redux SDK/Planet Authoring/Science Region Data", priority = KSP2UnityTools.MenuPriority)]
        public static void CreateScienceRegionData()
        {
            KSP2UnityTools.CreateKsp2UnityToolsAssetAtSelectedPath<ScienceRegionData>("New Science Region");
        }


        public Texture2D scienceRegionMap;
        public ScienceRegionDataInformation information = new();
        public List<CelestialBodyDiscoverablePosition> discoverables = new();

        private byte ConvertToIndex(Color col)
        {
            int bestIndex = 0;
            float closestDistance = float.MaxValue;
            foreach (ExtendedScienceRegionDefinition region in information.ScienceRegionDefinitions)
            {
                Color indexColor = region.RegionColor;
                float distanceSquared = (col.r - indexColor.r) * (col.r - indexColor.r) +
                    (col.g - indexColor.g) * (col.g - indexColor.g) +
                    (col.b - indexColor.b) * (col.b - indexColor.b);
                if (!(distanceSquared < closestDistance))
                {
                    continue;
                }

                closestDistance = distanceSquared;
                bestIndex = region.MapId;
            }

            return (byte)bestIndex;
        }

        public byte[] GetIndices()
        {
            return !scienceRegionMap.isReadable
                ? new byte[scienceRegionMap.width * scienceRegionMap.height]
                : scienceRegionMap.GetPixels().Select(ConvertToIndex).ToArray();
        }


        [Serializable]
        public class ScienceRegionDataInformation
        {
            public string Version;
            public string BodyName;
            public CBSituationData SituationData;

            [JsonProperty(PropertyName = "Regions")]
            public ExtendedScienceRegionDefinition[] ScienceRegionDefinitions;
        }

        [Serializable]
        public class ExtendedScienceRegionDefinition : ScienceRegionDefinition
        {
            [JsonIgnore] public Color RegionColor;
        }
    }
}