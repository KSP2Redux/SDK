using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    public static class LinkedAddressablePlayerBuilder
    {
        public const string BootstrapScenePath =
            "Assets/KSP2UnityToolsGenerated/LinkedAddressablesBootstrap.unity";

        public static BuildReport Build(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("A player output path is required.", nameof(outputPath));

            var absoluteOutputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutputPath));

            var previousBootstrap =
                LinkedAddressablePlayerBuildOptions.UseTranslatedSceneBootstrap;
            LinkedAddressablePlayerBuildOptions.UseTranslatedSceneBootstrap = true;
            BuildReport report;
            try
            {
                LinkedAddressableTranslatedContentBuilder.Build(true);
                var manifest =
                    LinkedAddressableRuntimeManifestBuilder.RebuildForPlayer(false);
                LinkedAddressablePlayerBuildProcessor.ValidateExternalSource(manifest);
                LinkedAddressablePlayerBuildProcessor.ValidateTranslatedContent(manifest);
                CreateBootstrapScene();
                AssetDatabase.SaveAssets();

                report = BuildPipeline.BuildPlayer(
                    new BuildPlayerOptions
                    {
                        scenes = new[] { BootstrapScenePath },
                        locationPathName = absoluteOutputPath,
                        target = BuildTarget.StandaloneWindows64,
                        options = BuildOptions.CleanBuildCache
                    }
                );
            }
            finally
            {
                LinkedAddressablePlayerBuildOptions.UseTranslatedSceneBootstrap =
                    previousBootstrap;
            }
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Linked Addressables player build ended with "
                        + $"'{report.summary.result}' and "
                        + $"{report.summary.totalErrors} error(s)."
                );
            }

            Debug.Log(
                $"[KSP2UnityTools.LinkedAddressables.Build] Built linked Addressables player "
                    + $"at '{absoluteOutputPath}' ({report.summary.totalSize} bytes)."
            );
            return report;
        }

        private static void CreateBootstrapScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BootstrapScenePath));
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive
            );
            try
            {
                if (!EditorSceneManager.SaveScene(scene, BootstrapScenePath, true))
                {
                    throw new InvalidOperationException(
                        $"Unity could not save bootstrap scene '{BootstrapScenePath}'."
                    );
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            AssetDatabase.ImportAsset(
                BootstrapScenePath,
                ImportAssetOptions.ForceSynchronousImport
            );
        }
    }
}
