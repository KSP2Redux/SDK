using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace Ksp2UnityTools.LinkedAddressables
{
    /// <summary>
    /// Opt-in standalone verification harness. It is inert unless the player is
    /// launched with --ksp2ut-verify-output=&lt;directory&gt;.
    /// </summary>
    internal sealed class LinkedAddressableRuntimeVerifier : MonoBehaviour
    {
        private const string OutputArgument = "--ksp2ut-verify-output=";
        private const float RuntimeTimeoutSeconds = 60f;
        private const float VideoTimeoutSeconds = 15f;
        private const float ScreenshotTimeoutSeconds = 10f;
        private const string MatrixTypeName =
            "KSP2UnityTools.LinkedAddressableTests.LinkedAddressableReferenceMatrix";
        private const string RawImageTypeName = "UnityEngine.UI.RawImage";

        private static bool verifierStarted;
        private string outputDirectory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetVerifier()
        {
            verifierStarted = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfRequested()
        {
            if (verifierStarted)
                return;

            var argument = Environment
                .GetCommandLineArgs()
                .FirstOrDefault(
                    value =>
                        value.StartsWith(OutputArgument, StringComparison.OrdinalIgnoreCase)
                );
            if (argument == null)
                return;

            verifierStarted = true;
            var outputDirectory = argument.Substring(OutputArgument.Length).Trim('"');
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                Debug.LogError(
                    "[ReduxSDK.LinkedAddressables.Verifier] The verification output "
                        + "argument did not contain a directory."
                );
                Application.Quit(2);
                return;
            }

            var runner = new GameObject("Redux SDK Linked Addressables Verifier")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(runner);
            runner.AddComponent<LinkedAddressableRuntimeVerifier>().outputDirectory =
                Path.GetFullPath(outputDirectory);
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(outputDirectory);
            var deadline = Time.realtimeSinceStartup + RuntimeTimeoutSeconds;
            while (
                !LinkedAddressableRuntime.IsReady
                && string.IsNullOrWhiteSpace(LinkedAddressableRuntime.RuntimeFailure)
                && Time.realtimeSinceStartup < deadline
            )
            {
                yield return null;
            }

            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            var report = BuildReport();
            var video = GetMatrixField<VideoClip>(report.MatrixAsset, "Video");
            if (video != null)
                yield return VerifyVideo(video, report);

            var screenshotPath = Path.Combine(outputDirectory, "linked-addressables.png");
            ScreenCapture.CaptureScreenshot(screenshotPath, 1);
            var screenshotDeadline =
                Time.realtimeSinceStartup + ScreenshotTimeoutSeconds;
            while (
                (!File.Exists(screenshotPath) || new FileInfo(screenshotPath).Length == 0)
                && Time.realtimeSinceStartup < screenshotDeadline
            )
            {
                yield return null;
            }

            report.ScreenshotPath = screenshotPath.Replace('\\', '/');
            report.ScreenshotWritten =
                File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0;
            report.Success =
                report.ManifestFound
                && report.RuntimeReady
                && report.TranslatedSceneLoaded
                && report.SourceModeValid
                && string.IsNullOrWhiteSpace(report.RuntimeFailure)
                && report.Roots.Length > 0
                && report.Roots.All(root => root.Loaded && string.IsNullOrEmpty(root.Failure))
                && report.MatrixFound
                && report.Fields.Length == 18
                && report.Fields.All(field => field.Valid)
                && report.MissingSceneComponentCount == 0
                && report.RawImageUsesMatrixTexture
                && report.AudioPcmValid
                && report.VideoPrepared
                && report.VideoProducedFrame
                && report.ScreenshotWritten;

            report.MatrixAsset = null;
            var reportPath = Path.Combine(outputDirectory, "verification.json");
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log(
                $"[ReduxSDK.LinkedAddressables.Verifier] Verification "
                    + $"{(report.Success ? "passed" : "failed")}. Report: '{reportPath}'."
            );
            Application.Quit(report.Success ? 0 : 3);
        }

        private static LinkedAddressableVerificationReport BuildReport()
        {
            var manifest = Resources.Load<LinkedAddressableRuntimeManifest>(
                LinkedAddressableRuntimeManifest.ResourcePath
            );
            var report = new LinkedAddressableVerificationReport
            {
                UnityVersion = Application.unityVersion,
                StreamingAssetsPath = Application.streamingAssetsPath.Replace('\\', '/'),
                ManifestFound = manifest != null,
                ConfiguredSourceRoot = manifest?.SourceRoot,
                SourceRoot = LinkedAddressableRuntime.ResolvedSourceRoot,
                SourceCopiedToPlayer = manifest?.CopySourceToPlayer ?? false,
                RuntimeReady = LinkedAddressableRuntime.IsReady,
                TranslatedSceneLoaded = LinkedAddressableRuntime.TranslatedSceneLoaded,
                RuntimeFailure = LinkedAddressableRuntime.RuntimeFailure,
                Roots = BuildRootReport(manifest),
                Fields = Array.Empty<LinkedAddressableVerificationField>()
            };
            report.SourceModeValid = IsExpectedSourceMode(manifest, report.SourceRoot);

            var targetBundle = LinkedAddressableRuntime.TargetAssetBundle;
            report.TargetAssetBundleLoaded = targetBundle != null;
            if (targetBundle == null)
                return report;

            report.MatrixAsset = targetBundle
                .LoadAllAssets()
                .FirstOrDefault(asset => asset != null && asset.GetType().FullName == MatrixTypeName);
            report.MatrixFound = report.MatrixAsset != null;
            if (!report.MatrixFound)
                return report;

            var values = GetMatrixValues(report.MatrixAsset);
            report.Fields = values
                .Select(pair => ValidateField(pair.Key, pair.Value, values, report))
                .ToArray();

            var matrixTexture = values["Texture"] as Texture;
            var rawImageTextures = FindRawImageTextures();
            report.RawImageConsumerCount = rawImageTextures.Count;
            report.RawImageUsesMatrixTexture =
                matrixTexture != null
                && rawImageTextures.Any(texture => texture == matrixTexture);
            report.MissingSceneComponentCount = CountMissingSceneComponents();
            return report;
        }

        private static bool IsExpectedSourceMode(
            LinkedAddressableRuntimeManifest manifest,
            string resolvedSourceRoot
        )
        {
            if (manifest == null || string.IsNullOrWhiteSpace(resolvedSourceRoot))
                return false;

            var expected = manifest.CopySourceToPlayer
                ? Path.Combine(
                    Application.streamingAssetsPath,
                    manifest.CopiedSourceRelativePath ?? string.Empty
                )
                : manifest.SourceRoot;
            if (string.IsNullOrWhiteSpace(expected))
                return false;

            return string.Equals(
                Path.GetFullPath(expected)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar
                    ),
                Path.GetFullPath(resolvedSourceRoot)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar
                    ),
                StringComparison.OrdinalIgnoreCase
            );
        }

        private static int CountMissingSceneComponents()
        {
            var missing = 0;
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                    continue;

                foreach (var root in scene.GetRootGameObjects())
                    missing += CountMissingComponents(root.transform);
            }

            return missing;
        }

        private static int CountMissingComponents(Transform transform)
        {
            var missing = transform.gameObject
                .GetComponents<Component>()
                .Count(component => component == null);
            for (var index = 0; index < transform.childCount; index++)
                missing += CountMissingComponents(transform.GetChild(index));
            return missing;
        }

        private static LinkedAddressableVerificationRoot[] BuildRootReport(
            LinkedAddressableRuntimeManifest manifest
        )
        {
            if (manifest?.Entries == null)
                return Array.Empty<LinkedAddressableVerificationRoot>();

            return manifest
                .Entries.Select(
                    entry =>
                    {
                        var descriptor = entry?.Descriptor;
                        LinkedAddressableRuntime.TryGetFailure(
                            descriptor?.StableId,
                            out var failure
                        );
                        LinkedAddressableRuntime.TryGetLoadedRoot(
                            descriptor?.StableId,
                            out var root
                        );
                        return new LinkedAddressableVerificationRoot
                        {
                            StableId = descriptor?.StableId,
                            Address = descriptor?.Address,
                            DeclaredType = descriptor?.AssetType,
                            Loaded = root != null,
                            LoadedType = root?.GetType().AssemblyQualifiedName,
                            Failure = failure
                        };
                    }
                )
                .ToArray();
        }

        private static Dictionary<string, UnityEngine.Object> GetMatrixValues(
            UnityEngine.Object matrix
        )
        {
            return matrix
                .GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                .ToDictionary(
                    field => field.Name,
                    field => field.GetValue(matrix) as UnityEngine.Object,
                    StringComparer.Ordinal
                );
        }

        private static LinkedAddressableVerificationField ValidateField(
            string name,
            UnityEngine.Object value,
            IReadOnlyDictionary<string, UnityEngine.Object> values,
            LinkedAddressableVerificationReport report
        )
        {
            var field = new LinkedAddressableVerificationField
            {
                Name = name,
                ObjectName = value?.name,
                RuntimeType = value?.GetType().AssemblyQualifiedName,
                Valid = value != null
            };
            if (value == null)
            {
                field.Details = "null";
                return field;
            }

            switch (name)
            {
                case "Texture":
                    var texture = value as Texture2D;
                    field.Valid = texture != null && texture.width == 512 && texture.height == 256;
                    field.Details = texture == null
                        ? "wrong type"
                        : $"{texture.width}x{texture.height}";
                    break;
                case "Text":
                    var text = value as TextAsset;
                    field.Valid =
                        text != null
                        && !string.IsNullOrWhiteSpace(text.text)
                        && text.text.IndexOf("lorem", StringComparison.OrdinalIgnoreCase) >= 0;
                    field.Details = text == null ? "wrong type" : $"{text.text.Length} characters";
                    break;
                case "Prefab":
                    field.Valid = value is GameObject;
                    field.Details = value is GameObject prefab
                        ? $"{prefab.transform.childCount} child object(s)"
                        : "wrong type";
                    break;
                case "NestedTransform":
                    var nested = value as Transform;
                    var prefabRoot = values["Prefab"] as GameObject;
                    field.Valid =
                        nested != null
                        && prefabRoot != null
                        && nested.IsChildOf(prefabRoot.transform);
                    field.Details = nested == null ? "wrong type" : GetTransformPath(nested);
                    break;
                case "Data":
                    field.Valid =
                        value.GetType().FullName
                        == "AddressablesSource.Fixtures.LinkedAddressableSampleData";
                    field.Details = DescribePublicValues(value);
                    break;
                case "Component":
                    var component = value as Component;
                    field.Valid =
                        component != null
                        && component.GetType().FullName
                            == "AddressablesSource.Fixtures.LinkedAddressableSampleComponent"
                        && component.gameObject == values["Prefab"];
                    field.Details = component == null
                        ? "wrong type"
                        : $"{component.GetType().FullName} on {component.gameObject.name}";
                    break;
                case "Material":
                    var material = value as Material;
                    field.Valid =
                        material != null
                        && material.mainTexture == values["Texture"];
                    field.Details = DescribeMaterial(material);
                    break;
                case "Mesh":
                case "PrivateMesh":
                    var mesh = value as Mesh;
                    field.Valid = mesh != null && mesh.vertexCount == 3;
                    field.Details = mesh == null ? "wrong type" : $"{mesh.vertexCount} vertices";
                    break;
                case "Animation":
                case "PrivateAnimation":
                    var animation = value as AnimationClip;
                    field.Valid = animation != null && animation.length > 0.9f;
                    field.Details = animation == null
                        ? "wrong type"
                        : $"{animation.length:F3} seconds";
                    break;
                case "Audio":
                case "PrivateAudio":
                    var audio = value as AudioClip;
                    var audioValid = ValidateAudioPcm(audio, out var audioDetails);
                    report.AudioPcmValid &= audioValid;
                    field.Valid = audioValid;
                    field.Details = audioDetails;
                    break;
                case "Sprite":
                    var sprite = value as Sprite;
                    field.Valid =
                        sprite != null
                        && sprite.texture == values["Texture"];
                    field.Details = sprite == null
                        ? "wrong type"
                        : $"{sprite.rect.width}x{sprite.rect.height}";
                    break;
                case "Video":
                    var video = value as VideoClip;
                    field.Valid =
                        video != null
                        && video.width == 1920
                        && video.height == 1080
                        && video.frameCount == 5286;
                    field.Details = video == null
                        ? "wrong type"
                        : $"{video.width}x{video.height}, {video.frameCount} frames, "
                            + $"{video.length:F2} seconds";
                    break;
                case "PrivateMaterial":
                    var privateMaterial = value as Material;
                    field.Valid =
                        privateMaterial != null
                        && privateMaterial.mainTexture == values["PrivateTexture"]
                        && privateMaterial.shader == values["PrivateShader"];
                    field.Details = DescribeMaterial(privateMaterial);
                    break;
                case "PrivateTexture":
                    var privateTexture = value as Texture2D;
                    field.Valid =
                        privateTexture != null
                        && privateTexture.width > 2
                        && privateTexture.height > 2;
                    field.Details = privateTexture == null
                        ? "wrong type"
                        : $"{privateTexture.width}x{privateTexture.height}";
                    break;
                case "PrivateShader":
                    var shader = value as Shader;
                    field.Valid = shader != null && !string.IsNullOrWhiteSpace(shader.name);
                    field.Details = shader?.name ?? "wrong type";
                    break;
            }

            return field;
        }

        private static bool ValidateAudioPcm(AudioClip audio, out string details)
        {
            if (audio == null || audio.samples <= 0)
            {
                details = "missing or empty";
                return false;
            }

            var samples = new float[Math.Min(audio.samples * audio.channels, 4096)];
            var read = audio.GetData(samples, 0);
            var nonZero = read && samples.Any(sample => Math.Abs(sample) > 0.00001f);
            details =
                $"{audio.samples} samples, {audio.channels} channel(s), "
                + $"PCM read={read}, nonzero={nonZero}";
            return audio.samples == 4410 && nonZero;
        }

        private static IEnumerator VerifyVideo(
            VideoClip clip,
            LinkedAddressableVerificationReport report
        )
        {
            var playerObject = new GameObject("Redux SDK Video Verification")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(playerObject);
            var player = playerObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.renderMode = VideoRenderMode.APIOnly;
            player.clip = clip;
            player.Prepare();

            var deadline = Time.realtimeSinceStartup + VideoTimeoutSeconds;
            while (!player.isPrepared && Time.realtimeSinceStartup < deadline)
                yield return null;

            report.VideoPrepared = player.isPrepared;
            if (player.isPrepared)
            {
                player.Play();
                deadline = Time.realtimeSinceStartup + VideoTimeoutSeconds;
                while (
                    player.frame < 0
                    && player.texture == null
                    && Time.realtimeSinceStartup < deadline
                )
                {
                    yield return null;
                }

                report.VideoProducedFrame = player.frame >= 0 || player.texture != null;
                report.VideoFrame = player.frame;
                report.VideoTextureWidth = player.texture?.width ?? 0;
                report.VideoTextureHeight = player.texture?.height ?? 0;
                player.Stop();
            }

            Destroy(playerObject);
        }

        private static T GetMatrixField<T>(UnityEngine.Object matrix, string name)
            where T : UnityEngine.Object
        {
            return matrix
                ?.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(matrix) as T;
        }

        private static List<Texture> FindRawImageTextures()
        {
            var textures = new List<Texture>();
            foreach (
                var component in Resources.FindObjectsOfTypeAll(typeof(Component))
                    .OfType<Component>()
            )
            {
                if (
                    component == null
                    || component.GetType().FullName != RawImageTypeName
                    || !component.gameObject.scene.IsValid()
                    || !component.gameObject.scene.isLoaded
                )
                {
                    continue;
                }

                var property = component
                    .GetType()
                    .GetProperty("texture", BindingFlags.Instance | BindingFlags.Public);
                if (property?.GetValue(component) is Texture texture)
                    textures.Add(texture);
            }

            return textures;
        }

        private static string GetTransformPath(Transform transform)
        {
            var names = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }

        private static string DescribeMaterial(Material material)
        {
            return material == null
                ? "wrong type"
                : $"shader={material.shader?.name}; texture={material.mainTexture?.name}";
        }

        private static string DescribePublicValues(UnityEngine.Object asset)
        {
            var values = asset
                .GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                .Select(field => $"{field.Name}={field.GetValue(asset)}")
                .ToArray();
            return values.Length == 0 ? asset.name : string.Join(", ", values);
        }
    }

    [Serializable]
    internal sealed class LinkedAddressableVerificationReport
    {
        public bool Success;
        public string UnityVersion;
        public string StreamingAssetsPath;
        public bool ManifestFound;
        public string ConfiguredSourceRoot;
        public string SourceRoot;
        public bool SourceCopiedToPlayer;
        public bool SourceModeValid;
        public bool RuntimeReady;
        public bool TranslatedSceneLoaded;
        public string RuntimeFailure;
        public LinkedAddressableVerificationRoot[] Roots;
        public bool TargetAssetBundleLoaded;
        public bool MatrixFound;
        [NonSerialized]
        public UnityEngine.Object MatrixAsset;
        public LinkedAddressableVerificationField[] Fields;
        public int MissingSceneComponentCount;
        public int RawImageConsumerCount;
        public bool RawImageUsesMatrixTexture;
        public bool AudioPcmValid = true;
        public bool VideoPrepared;
        public bool VideoProducedFrame;
        public long VideoFrame;
        public int VideoTextureWidth;
        public int VideoTextureHeight;
        public string ScreenshotPath;
        public bool ScreenshotWritten;
    }

    [Serializable]
    internal sealed class LinkedAddressableVerificationRoot
    {
        public string StableId;
        public string Address;
        public string DeclaredType;
        public bool Loaded;
        public string LoadedType;
        public string Failure;
    }

    [Serializable]
    internal sealed class LinkedAddressableVerificationField
    {
        public string Name;
        public bool Valid;
        public string ObjectName;
        public string RuntimeType;
        public string Details;
    }
}
