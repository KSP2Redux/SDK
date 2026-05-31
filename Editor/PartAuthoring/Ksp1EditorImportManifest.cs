using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring
{
    internal sealed class Ksp1EditorImportManifest
    {
        public List<string> GeneratedPaths = new();

        public static Ksp1EditorImportManifest Load(string path)
        {
            if (!File.Exists(path))
            {
                return new Ksp1EditorImportManifest();
            }

            try
            {
                return JsonConvert.DeserializeObject<Ksp1EditorImportManifest>(File.ReadAllText(path)) ??
                       new Ksp1EditorImportManifest();
            }
            catch
            {
                return new Ksp1EditorImportManifest();
            }
        }

        public bool IsGenerated(string path)
        {
            string normalized = Normalize(path);
            return GeneratedPaths.Any(item => string.Equals(Normalize(item), normalized, StringComparison.OrdinalIgnoreCase));
        }

        public void MarkGenerated(string path)
        {
            path = Normalize(path);
            if (!GeneratedPaths.Any(item => string.Equals(Normalize(item), path, StringComparison.OrdinalIgnoreCase)))
            {
                GeneratedPaths.Add(path);
            }
        }

        public void Save(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
            AssetDatabase.ImportAsset(path);
        }

        private static string Normalize(string path)
        {
            return (path ?? "").Replace('\\', '/');
        }
    }
}
