using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KSP.Modules;
using KSP.Sim;
using KSP.Sim.Definitions;
using Redux.Modules.Attributes;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// Custom editor for <see cref="Data_Deployable" /> with edit-mode animation preview controls.
    /// </summary>
    [DataEditor(typeof(Data_Deployable))]
    public sealed class DeployableDataEditor : IDataEditor
    {
        private const BindingFlags FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private const string USS_PATH = "/Assets/Windows/PartAuthoring/Inspectors/DataEditors/DataEditors.uss";

        private static readonly FieldInfo DataDeployableField = typeof(Module_Deployable).GetField(
            "dataDeployable",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private PartBehaviourModule _module;
        private Transform _partRoot;
        private Data_Deployable _data;
        private PreviewClipSet _clips;

        private VisualElement _root;
        private Slider _positionSlider;
        private HelpBox _statusBox;
        private Button _playDeployButton;
        private Button _playRetractButton;
        private Button _clearButton;

        private AnimationClip _playClip;
        private double _playStartTime;
        private float _playStartPosition;
        private float _playDirection = 1f;
        private bool _isPlaying;
        private bool _startedAnimationMode;
        private bool _registeredPlayModeCallback;

        /// <inheritdoc />
        public VisualElement Build(SerializedProperty dataProp, PartBehaviourModule module)
        {
            _module = module;
            _partRoot = module == null ? null : module.gameObject.transform;
            _data = GetDeployableData(module);
            _clips = PreviewClipSet.Resolve(module as Module_Deployable, _data);

            _root = new VisualElement();
            _root.style.flexDirection = FlexDirection.Column;
            _root.RegisterCallback<DetachFromPanelEvent>(_ => CleanupPreview(revertPose: true));
            RegisterPlayModeCallback();

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + USS_PATH);
            if (sheet != null)
            {
                _root.styleSheets.Add(sheet);
            }

            _root.Add(BuildPreviewSection());
            _root.Add(BuildDefinitionFields(dataProp));
            return _root;
        }

        private VisualElement BuildPreviewSection()
        {
            var section = new VisualElement();
            section.AddToClassList("drag-tools-box");

            var header = new Label("Animation Preview");
            header.AddToClassList("data-editor-subsection-header");
            section.Add(header);

            if (!CanPreviewAnimations())
            {
                section.Add(new HelpBox(
                    "Preview tools are disabled while Unity is entering, running, or leaving Play Mode.",
                    HelpBoxMessageType.Info));
                return section;
            }

            var moduleDeployable = _module as Module_Deployable;
            var animator = PreviewClipSet.GetAnimator(moduleDeployable);
            if (moduleDeployable == null)
            {
                section.Add(new HelpBox("Preview tools require a Module_Deployable component.", HelpBoxMessageType.Warning));
                return section;
            }
            if (animator == null)
            {
                section.Add(new HelpBox("No Animator was found. Assign Module_Deployable.animator or add an Animator under this part.", HelpBoxMessageType.Warning));
                return section;
            }
            if (!_clips.HasAnyClip)
            {
                section.Add(new HelpBox("No animation clips were found on the deployable Animator controller.", HelpBoxMessageType.Warning));
                return section;
            }

            section.Add(BuildObjectRow("Animator", animator, () => SelectAndPing(animator)));
            if (animator.runtimeAnimatorController != null)
            {
                section.Add(BuildObjectRow("Controller", animator.runtimeAnimatorController, () => SelectAndPing(animator.runtimeAnimatorController)));
            }

            var clipSummary = string.Join(
                ", ",
                new[]
                {
                    _clips.OpenClip == null ? null : "open: " + _clips.OpenClip.name,
                    _clips.CloseClip == null
                        ? (_clips.OpenClip == null ? null : "close: open clip reversed")
                        : "close: " + _clips.CloseClip.name,
                    _clips.ClosedClip == null ? null : "closed: " + _clips.ClosedClip.name,
                    _clips.OpenedClip == null ? null : "opened: " + _clips.OpenedClip.name,
                }.Where(s => !string.IsNullOrEmpty(s)));
            var clipLabel = new Label(string.IsNullOrEmpty(clipSummary) ? "Using controller animation clips." : clipSummary);
            clipLabel.style.whiteSpace = WhiteSpace.Normal;
            clipLabel.style.marginTop = 2f;
            clipLabel.style.marginBottom = 4f;
            section.Add(clipLabel);

            _positionSlider = new Slider("Preview Position", 0f, 1f)
            {
                value = 0f,
                showInputField = true,
            };
            _positionSlider.AddToClassList("unity-base-field__aligned");
            _positionSlider.RegisterValueChangedCallback(evt =>
            {
                StopPlaybackOnly();
                SampleDeployFraction(evt.newValue, "Previewing deploy position.");
            });
            section.Add(_positionSlider);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.flexWrap = Wrap.Wrap;
            buttonRow.style.marginTop = 4f;

            var retractedButton = BuildPreviewButton("Retracted", () => SampleEndpoint(extended: false));
            var extendedButton = BuildPreviewButton("Extended", () => SampleEndpoint(extended: true));
            _playDeployButton = BuildPreviewButton("Play Deploy", () => StartPlayback(forward: true));
            _playRetractButton = BuildPreviewButton("Play Retract", () => StartPlayback(forward: false));
            _clearButton = BuildPreviewButton("Clear Preview", () => StopPreview(revertPose: true));

            buttonRow.Add(retractedButton);
            buttonRow.Add(extendedButton);
            buttonRow.Add(_playDeployButton);
            buttonRow.Add(_playRetractButton);
            buttonRow.Add(_clearButton);
            section.Add(buttonRow);

            _statusBox = new HelpBox(
                "Use these controls to inspect deploy/retract motion in the Scene view. Clear Preview returns the prefab to its authored pose.",
                HelpBoxMessageType.Info);
            _statusBox.style.marginTop = 4f;
            section.Add(_statusBox);

            UpdateButtonState();
            return section;
        }

        private static Button BuildPreviewButton(string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.style.minWidth = 96f;
            button.style.flexGrow = 1f;
            button.style.marginRight = 4f;
            button.style.marginBottom = 4f;
            return button;
        }

        private static VisualElement BuildObjectRow(string label, UnityEngine.Object value, Action select)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 2f;

            var field = new ObjectField(label)
            {
                objectType = value == null ? typeof(UnityEngine.Object) : value.GetType(),
                value = value,
            };
            field.AddToClassList("unity-base-field__aligned");
            field.SetEnabled(false);
            field.style.flexGrow = 1f;
            row.Add(field);

            var selectButton = new Button(select) { text = "Select" };
            selectButton.style.marginLeft = 4f;
            selectButton.style.flexShrink = 0f;
            row.Add(selectButton);
            return row;
        }

        private VisualElement BuildDefinitionFields(SerializedProperty dataProp)
        {
            var container = new VisualElement();
            var iterator = dataProp.Copy();
            var end = iterator.GetEndProperty();

            var first = true;
            while (iterator.NextVisible(first))
            {
                first = false;
                if (SerializedProperty.EqualContents(iterator, end))
                {
                    break;
                }

                var field = FindField(typeof(Data_Deployable), iterator.name);
                if (!ShouldRender(field))
                {
                    continue;
                }

                var row = ReflectionModuleEditor.BuildFieldRowForCustomEditor(iterator.Copy(), field, _partRoot);
                if (row != null)
                {
                    container.Add(row);
                }
            }
            return container;
        }

        private void SampleEndpoint(bool extended)
        {
            StopPlaybackOnly();
            if (extended && _clips.OpenedClip != null)
            {
                SampleClip(_clips.OpenedClip, 0f, 1f, "Previewing opened pose.");
                _positionSlider?.SetValueWithoutNotify(1f);
                return;
            }
            if (!extended && _clips.ClosedClip != null)
            {
                SampleClip(_clips.ClosedClip, 0f, 0f, "Previewing closed pose.");
                _positionSlider?.SetValueWithoutNotify(0f);
                return;
            }

            var normalized = extended ? 1f : 0f;
            SampleDeployFraction(normalized, extended ? "Previewing extended pose." : "Previewing retracted pose.");
            _positionSlider?.SetValueWithoutNotify(normalized);
        }

        private void SampleDeployFraction(float normalized, string status)
        {
            if (!CanPreviewAnimations())
            {
                StopPreview(revertPose: true);
                SetStatus("Preview stopped because Unity is entering or leaving Play Mode.", HelpBoxMessageType.Info);
                return;
            }

            var deployFraction = Mathf.Clamp01(normalized);
            var clip = _clips.OpenClip ?? _clips.CloseClip;
            if (clip == null)
            {
                SetStatus("No deploy animation clip is available to sample.", HelpBoxMessageType.Warning);
                return;
            }

            var sampleTime = deployFraction;
            if (_clips.CloseClip == null && _clips.OpenClip != null)
            {
                sampleTime = deployFraction;
            }
            else if (_clips.OpenClip == null && _clips.CloseClip != null)
            {
                sampleTime = 1f - deployFraction;
            }

            SampleClip(clip, sampleTime, deployFraction, status);
        }

        private void SampleClip(AnimationClip clip, float normalizedTime, float sliderValue, string status)
        {
            if (!CanPreviewAnimations())
            {
                StopPreview(revertPose: true);
                SetStatus("Preview stopped because Unity is entering or leaving Play Mode.", HelpBoxMessageType.Info);
                return;
            }

            var sampleRoot = _clips.SampleRoot;
            if (sampleRoot == null || clip == null)
            {
                SetStatus("No valid animation root or clip was found.", HelpBoxMessageType.Warning);
                return;
            }

            EnsureAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(sampleRoot, clip, Mathf.Clamp01(normalizedTime) * clip.length);
            AnimationMode.EndSampling();
            _positionSlider?.SetValueWithoutNotify(Mathf.Clamp01(sliderValue));
            SceneView.RepaintAll();
            SetStatus(status, HelpBoxMessageType.Info);
        }

        private void StartPlayback(bool forward)
        {
            if (!CanPreviewAnimations())
            {
                SetStatus("Preview tools are disabled while Unity is entering or leaving Play Mode.", HelpBoxMessageType.Info);
                return;
            }

            StopPlaybackOnly();
            var clip = _clips.OpenClip ?? _clips.CloseClip;
            if (clip == null)
            {
                SetStatus(forward ? "No deploy animation clip is available." : "No retract animation clip is available.", HelpBoxMessageType.Warning);
                return;
            }

            _playClip = clip;
            _playDirection = forward ? 1f : -1f;
            _playStartPosition = _positionSlider == null ? (forward ? 0f : 1f) : Mathf.Clamp01(_positionSlider.value);
            if (forward && _playStartPosition >= 1f)
            {
                _playStartPosition = 0f;
            }
            else if (!forward && _playStartPosition <= 0f)
            {
                _playStartPosition = 1f;
            }
            _playStartTime = EditorApplication.timeSinceStartup;
            _isPlaying = true;
            EditorApplication.update += TickPlayback;
            UpdateButtonState();
            SetStatus(
                forward
                    ? $"Playing deploy preview with '{clip.name}'."
                    : $"Playing retract preview with '{clip.name}' backwards.",
                HelpBoxMessageType.Info);
        }

        private void TickPlayback()
        {
            if (!CanPreviewAnimations())
            {
                StopPreview(revertPose: true);
                return;
            }

            if (!_isPlaying || _playClip == null)
            {
                StopPlaybackOnly();
                return;
            }

            var duration = Mathf.Max(0.001f, _playClip.length);
            var elapsed = (float)(EditorApplication.timeSinceStartup - _playStartTime);
            var previewPosition = Mathf.Clamp01(_playStartPosition + _playDirection * (elapsed / duration));

            if (_playDirection >= 0f)
            {
                SampleDeployFraction(previewPosition, "Playing deploy preview.");
            }
            else
            {
                SampleDeployFraction(previewPosition, "Playing retract preview.");
            }

            if (previewPosition <= 0f || previewPosition >= 1f)
            {
                StopPlaybackOnly();
                SetStatus(previewPosition >= 1f ? "Deploy preview reached the extended pose." : "Retract preview reached the retracted pose.", HelpBoxMessageType.Info);
            }
        }

        private void EnsureAnimationMode()
        {
            if (AnimationMode.InAnimationMode())
            {
                return;
            }
            AnimationMode.StartAnimationMode();
            _startedAnimationMode = true;
        }

        private void StopPlaybackOnly()
        {
            if (!_isPlaying)
            {
                return;
            }
            EditorApplication.update -= TickPlayback;
            _isPlaying = false;
            _playClip = null;
            UpdateButtonState();
        }

        private void StopPreview(bool revertPose)
        {
            StopPlaybackOnly();
            if (revertPose && _startedAnimationMode && AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }
            _startedAnimationMode = false;
            _positionSlider?.SetValueWithoutNotify(0f);
            SetStatus("Preview cleared.", HelpBoxMessageType.Info);
        }

        private void CleanupPreview(bool revertPose)
        {
            StopPreview(revertPose);
            if (_registeredPlayModeCallback)
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                _registeredPlayModeCallback = false;
            }
        }

        private void RegisterPlayModeCallback()
        {
            if (_registeredPlayModeCallback)
            {
                return;
            }
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            _registeredPlayModeCallback = true;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state is PlayModeStateChange.ExitingEditMode or PlayModeStateChange.EnteredPlayMode or PlayModeStateChange.ExitingPlayMode)
            {
                CleanupPreview(revertPose: true);
            }
        }

        private static bool CanPreviewAnimations()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode &&
                   !EditorApplication.isCompiling &&
                   !EditorApplication.isUpdating;
        }

        private void UpdateButtonState()
        {
            if (_playDeployButton != null)
            {
                _playDeployButton.SetEnabled(!_isPlaying && (_clips.OpenClip != null || _clips.CloseClip != null));
            }
            if (_playRetractButton != null)
            {
                _playRetractButton.SetEnabled(!_isPlaying && (_clips.CloseClip != null || _clips.OpenClip != null));
            }
            if (_clearButton != null)
            {
                _clearButton.SetEnabled(_startedAnimationMode || _isPlaying);
            }
        }

        private void SetStatus(string message, HelpBoxMessageType type)
        {
            if (_statusBox == null)
            {
                return;
            }
            _statusBox.text = message;
            _statusBox.messageType = type;
        }

        private static void SelectAndPing(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private static Data_Deployable GetDeployableData(PartBehaviourModule module)
        {
            return module is Module_Deployable deployable
                ? DataDeployableField?.GetValue(deployable) as Data_Deployable
                : null;
        }

        private static bool ShouldRender(FieldInfo field)
        {
            if (field == null)
            {
                return false;
            }
            if (field.IsDefined(typeof(KSPStateAttribute), inherit: true))
            {
                return false;
            }
            if (field.IsDefined(typeof(HideInInspector), inherit: true))
            {
                return false;
            }
            return field.IsDefined(typeof(KSPDefinitionAttribute), inherit: true);
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null && type != typeof(object))
            {
                var field = type.GetField(name, FIELD_FLAGS);
                if (field != null)
                {
                    return field;
                }
                type = type.BaseType;
            }
            return null;
        }

        private readonly struct PreviewClipSet
        {
            public readonly GameObject SampleRoot;
            public readonly AnimationClip OpenClip;
            public readonly AnimationClip CloseClip;
            public readonly AnimationClip ClosedClip;
            public readonly AnimationClip OpenedClip;

            public bool HasAnyClip => OpenClip != null || CloseClip != null || ClosedClip != null || OpenedClip != null;

            private PreviewClipSet(
                GameObject sampleRoot,
                AnimationClip openClip,
                AnimationClip closeClip,
                AnimationClip closedClip,
                AnimationClip openedClip)
            {
                SampleRoot = sampleRoot;
                OpenClip = openClip;
                CloseClip = closeClip;
                ClosedClip = closedClip;
                OpenedClip = openedClip;
            }

            public static PreviewClipSet Resolve(Module_Deployable deployable, Data_Deployable data)
            {
                var animator = GetAnimator(deployable);
                var controller = animator == null ? null : animator.runtimeAnimatorController;
                var clips = controller == null ? Array.Empty<AnimationClip>() : controller.animationClips.Where(c => c != null).Distinct().ToArray();
                if (clips.Length == 0)
                {
                    return new PreviewClipSet(animator == null ? null : animator.gameObject, null, null, null, null);
                }

                var open = FindAnimatedControllerStateClip(controller, "OPEN") ??
                           FindAnimatedControllerStateClip(controller, "EXTEND") ??
                           FindAnimatedNamedClip(clips, data?.animationName) ??
                           FindAnimatedClipByToken(clips, "open", reject: "opened") ??
                           FindAnimatedClipByToken(clips, "deploy", reject: null) ??
                           LongestNonStaticClip(clips);

                var close = FindAnimatedControllerStateClip(controller, "CLOSE") ??
                            FindAnimatedControllerStateClip(controller, "RETRACT") ??
                            FindAnimatedClipByToken(clips, "retract", reject: null) ??
                            FindAnimatedClipByToken(clips, "close", reject: "closed");
                if (close == open)
                {
                    close = null;
                }

                var closed = FindControllerStateClip(controller, "CLOSED") ??
                             FindClipByToken(clips, "closed", reject: null);

                var opened = FindControllerStateClip(controller, "OPENED") ??
                             FindClipByToken(clips, "opened", reject: null);

                return new PreviewClipSet(animator == null ? null : animator.gameObject, open, close, closed, opened);
            }

            public static Animator GetAnimator(Module_Deployable deployable)
            {
                if (deployable == null)
                {
                    return null;
                }
                if (deployable.animator != null)
                {
                    return deployable.animator;
                }
                return deployable.GetComponentInChildren<Animator>(true);
            }

            private static AnimationClip FindControllerStateClip(RuntimeAnimatorController controller, string stateName)
            {
                if (controller is AnimatorOverrideController overrideController)
                {
                    controller = overrideController.runtimeAnimatorController;
                }
                if (controller is not AnimatorController animatorController)
                {
                    return null;
                }

                foreach (var layer in animatorController.layers)
                {
                    var clip = FindStateClip(layer.stateMachine, stateName);
                    if (clip != null)
                    {
                        return clip;
                    }
                }
                return null;
            }

            private static AnimationClip FindAnimatedControllerStateClip(
                RuntimeAnimatorController controller,
                string stateName)
            {
                var clip = FindControllerStateClip(controller, stateName);
                return IsAnimatedClip(clip) ? clip : null;
            }

            private static AnimationClip FindStateClip(AnimatorStateMachine stateMachine, string stateName)
            {
                if (stateMachine == null)
                {
                    return null;
                }

                foreach (ChildAnimatorState childState in stateMachine.states)
                {
                    if (string.Equals(childState.state.name, stateName, StringComparison.OrdinalIgnoreCase) &&
                        childState.state.motion is AnimationClip clip)
                    {
                        return clip;
                    }
                }

                foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
                {
                    var clip = FindStateClip(childMachine.stateMachine, stateName);
                    if (clip != null)
                    {
                        return clip;
                    }
                }
                return null;
            }

            private static AnimationClip FindNamedClip(AnimationClip[] clips, string animationName)
            {
                if (string.IsNullOrWhiteSpace(animationName))
                {
                    return null;
                }
                return clips.FirstOrDefault(c => string.Equals(c.name, animationName, StringComparison.OrdinalIgnoreCase)) ??
                       clips.FirstOrDefault(c => c.name.IndexOf(animationName, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            private static AnimationClip FindAnimatedNamedClip(AnimationClip[] clips, string animationName)
            {
                var clip = FindNamedClip(clips, animationName);
                return IsAnimatedClip(clip) ? clip : null;
            }

            private static AnimationClip FindClipByToken(AnimationClip[] clips, string token, string reject)
            {
                return clips.FirstOrDefault(c =>
                    c.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (string.IsNullOrEmpty(reject) || c.name.IndexOf(reject, StringComparison.OrdinalIgnoreCase) < 0));
            }

            private static AnimationClip FindAnimatedClipByToken(AnimationClip[] clips, string token, string reject)
            {
                return clips.FirstOrDefault(c =>
                    IsAnimatedClip(c) &&
                    c.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    (string.IsNullOrEmpty(reject) || c.name.IndexOf(reject, StringComparison.OrdinalIgnoreCase) < 0));
            }

            private static AnimationClip LongestNonStaticClip(AnimationClip[] clips)
            {
                return clips
                    .Where(IsAnimatedClip)
                    .OrderByDescending(c => Mathf.Max(c.length, EstimateCurveSpan(c)))
                    .FirstOrDefault();
            }

            private static bool IsAnimatedClip(AnimationClip clip)
            {
                if (clip == null)
                {
                    return false;
                }
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (CurveChangesValue(curve))
                    {
                        return true;
                    }
                }

                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    if (ObjectReferenceCurveChangesValue(keys))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool CurveChangesValue(AnimationCurve curve)
            {
                if (curve == null || curve.keys.Length < 2)
                {
                    return false;
                }

                var firstValue = curve.keys[0].value;
                for (var i = 1; i < curve.keys.Length; i++)
                {
                    if (Mathf.Abs(curve.keys[i].value - firstValue) > 0.0001f)
                    {
                        return true;
                    }
                }
                return false;
            }

            private static bool ObjectReferenceCurveChangesValue(ObjectReferenceKeyframe[] keys)
            {
                if (keys == null || keys.Length < 2)
                {
                    return false;
                }

                var firstValue = keys[0].value;
                for (var i = 1; i < keys.Length; i++)
                {
                    if (keys[i].value != firstValue)
                    {
                        return true;
                    }
                }
                return false;
            }

            private static float EstimateCurveSpan(AnimationClip clip)
            {
                var maxTime = 0f;
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null || curve.keys.Length == 0)
                    {
                        continue;
                    }
                    maxTime = Mathf.Max(maxTime, curve.keys[curve.keys.Length - 1].time);
                }
                return maxTime;
            }
        }
    }
}
