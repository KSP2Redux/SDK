using KSP.VFX;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using KSP.Modules;
using Redux.VFX.Plume.Volumetric;

namespace KSP.Editor
{
    [CustomEditor(typeof(ThrottleVFXManager))]
    public class ThrottleVFXManagerEditor : UnityEditor.Editor
    {
        private readonly struct EffectAssignment
        {
            public readonly int EngineModeIndex;
            public readonly ThrottleVFXManager.FXmodeEvent ModeEvent;

            public EffectAssignment(int engineModeIndex, ThrottleVFXManager.FXmodeEvent modeEvent)
            {
                EngineModeIndex = engineModeIndex;
                ModeEvent = modeEvent;
            }
        }

        /// <inheritdoc />
        public override VisualElement CreateInspectorGUI()
        {
            var manager = (ThrottleVFXManager)target;
            var root = new VisualElement();
            root.Add(ThrottleVFXPreviewBridge.CreatePreviewControls(manager));
            root.Add(CreateMeshControls(manager));
            root.Add(CreateVolumeControls(manager));

            var bindings = new Foldout { text = "Engine events and effect bindings", value = false };
            InspectorElement.FillDefaultInspector(bindings, serializedObject, this);
            bindings.RegisterCallback<SerializedPropertyChangeEvent>(_ => ThrottleVFXPreviewBridge.RefreshIfActive(manager));
            bindings.Add(new Button(() =>
            {
                GatherEffectsFromChildren(manager);
                ThrottleVFXPreviewBridge.RefreshIfActive(manager);
            }) { text = "Gather effects from children" });
            root.Add(bindings);
            return root;
        }

        private static VisualElement CreateMeshControls(ThrottleVFXManager manager)
        {
            var low = new Foldout { text = "Low - Mesh and particle effects", value = false };
            low.Add(new HelpBox("These are this engine's existing effects. Bell glow, heat distortion and unreplaced effects can also remain active at higher quality.", HelpBoxMessageType.Info));
            foreach (MonoBehaviour effect in manager.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (effect is not IEngineFXData || effect is VolumetricPlumeSource)
                    continue;

                string path = AnimationUtility.CalculateTransformPath(effect.transform, manager.transform);
                var layer = new Foldout
                {
                    text = $"{(path.Length > 0 ? path : manager.name)} ({effect.GetType().Name})",
                    value = false
                };
                layer.Add(new Button(() => Selection.activeObject = effect) { text = "Edit curves, gradients and presets" });
                low.Add(layer);
                if (effect is not ThrottleBlendshapeData)
                    continue;

                var settings = new SerializedObject(effect);
                layer.Add(new PropertyField(settings.FindProperty("UseAmbientPressure")));
                layer.Add(new PropertyField(settings.FindProperty("UseEngineThrottle"), "Use engine thrust"));
                layer.Add(new PropertyField(settings.FindProperty("OverexpandedBlendshapeIndex"), "Overexpansion shape"));
                layer.Add(new PropertyField(settings.FindProperty("FullOverexpansionPressure"), "Full compression (atm)"));
                layer.RegisterCallback<SerializedPropertyChangeEvent>(_ => ThrottleVFXPreviewBridge.RefreshIfActive(effect));
                // Rebind after the enclosing inspector binds its manager object.
                layer.RegisterCallback<AttachToPanelEvent>(_ => layer.Bind(settings));
            }
            return low;
        }

        private static VisualElement CreateVolumeControls(ThrottleVFXManager manager)
        {
            var volume = new Foldout { text = "Medium and High - Volumetric appearance", value = true };
            void PopulateVolumes()
            {
                volume.Clear();
                if (manager == null)
                    return;

                VolumetricPlumeSource[] sources = manager.GetComponentsInChildren<VolumetricPlumeSource>(true);
                foreach (VolumetricPlumeSource source in sources)
                {
                    VisualElement controls = VolumetricPlumeSourceEditor.CreateProfileControls(new SerializedObject(source));
                    if (sources.Length == 1)
                    {
                        volume.Add(controls);
                        continue;
                    }

                    string path = AnimationUtility.CalculateTransformPath(source.transform, manager.transform);
                    var group = new Foldout { text = path.Length > 0 ? path : manager.name };
                    group.Add(controls);
                    volume.Add(group);
                }
                if (sources.Length > 0)
                    return;

                volume.Add(new HelpBox("This engine currently uses its mesh effects at every quality. Add a source to author its volumetric appearance.", HelpBoxMessageType.Info));
                volume.Add(new Button(() =>
                {
                    var source = Undo.AddComponent<VolumetricPlumeSource>(manager.gameObject);
                    var module = manager.GetComponentInParent<Module_Engine>(true);
                    Data_Engine.EngineMode mode = module != null ? module.DataEngine?.currentEngineModeData : null;
                    if (mode != null)
                    {
                        using var settings = new SerializedObject(source);
                        settings.FindProperty("_previewEngineType").intValue = (int)mode.engineType;
                        settings.FindProperty("_previewMaximumThrust").floatValue = mode.maxThrust;
                        settings.ApplyModifiedProperties();
                    }
                    PopulateVolumes();
                    ThrottleVFXPreviewBridge.RefreshIfActive(manager);
                }) { text = "Add volumetric source" });
            }
            PopulateVolumes();
            volume.RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += PopulateVolumes);
            volume.RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= PopulateVolumes);
            return volume;
        }

        private static void GatherEffectsFromChildren(ThrottleVFXManager manager)
        {
            Undo.RecordObject(manager, "Gather Throttle VFX Effects");

            Dictionary<int, Dictionary<ThrottleVFXManager.FXmodeEvent, List<GameObject>>> effectsByMode = new();
            Dictionary<GameObject, List<EffectAssignment>> existingAssignments = GetExistingAssignments(manager);
            MonoBehaviour[] behaviours = manager.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null || behaviour is not IEngineFXData)
                {
                    continue;
                }

                // Child systems that play along with their parent effect stay out of the manager, as in stock engines.
                if (behaviour is IEngineFXDataModeHint { IsExcludedFromEngineFXManager: true })
                {
                    continue;
                }

                List<EffectAssignment> assignments = GetAssignments(behaviour, existingAssignments);
                foreach (EffectAssignment assignment in assignments)
                {
                    if (!effectsByMode.TryGetValue(
                            assignment.EngineModeIndex,
                            out Dictionary<ThrottleVFXManager.FXmodeEvent, List<GameObject>> effectsByEvent
                        ))
                    {
                        effectsByEvent = new Dictionary<ThrottleVFXManager.FXmodeEvent, List<GameObject>>();
                        effectsByMode.Add(assignment.EngineModeIndex, effectsByEvent);
                    }

                    if (!effectsByEvent.TryGetValue(assignment.ModeEvent, out List<GameObject> effectReferences))
                    {
                        effectReferences = new List<GameObject>();
                        effectsByEvent.Add(assignment.ModeEvent, effectReferences);
                    }

                    if (!effectReferences.Contains(behaviour.gameObject))
                    {
                        effectReferences.Add(behaviour.gameObject);
                    }
                }
            }

            manager.FXModeActionEvents = effectsByMode
                .OrderBy(pair => pair.Key)
                .Select(pair => new ThrottleVFXManager.FXModeActionEvent
                {
                    EngineModeIndex = pair.Key,
                    ActionEvents = pair.Value
                        .OrderBy(actionPair => (int)actionPair.Key)
                        .Select(actionPair => new ThrottleVFXManager.FXActionEvent
                        {
                            ModeEvent = actionPair.Key,
                            EngineEffects = actionPair.Value
                                .Select(effectReference => new ThrottleVFXManager.EngineEffect
                                {
                                    EffectReference = effectReference
                                })
                                .ToArray()
                        })
                        .ToArray()
                })
                .ToArray();

            EditorUtility.SetDirty(manager);
            PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        }

        private static List<EffectAssignment> GetAssignments(
            MonoBehaviour behaviour,
            Dictionary<GameObject, List<EffectAssignment>> existingAssignments
        )
        {
            if (behaviour is IEngineFXDataModeHint { HasEngineFXModeHint: true } modeHint)
            {
                var hinted = new List<EffectAssignment>
                {
                    new(Mathf.Max(0, modeHint.EngineFXEngineModeIndex), modeHint.EngineFXModeEvent)
                };
                // An effect shared by several modes or events, such as a flame used by both modes of an afterburning
                // jet, lists the others here.
                if (modeHint.EngineFXAdditionalAssignments != null)
                {
                    foreach (EngineFXModeAssignment additional in modeHint.EngineFXAdditionalAssignments)
                    {
                        hinted.Add(new EffectAssignment(Mathf.Max(0, additional.EngineModeIndex), additional.ModeEvent));
                    }
                }

                return hinted;
            }

            if (existingAssignments.TryGetValue(behaviour.gameObject, out List<EffectAssignment> assignments))
            {
                return assignments;
            }

            return new List<EffectAssignment>
            {
                new(0, ThrottleVFXManager.FXmodeEvent.FXModeRunning)
            };
        }

        private static Dictionary<GameObject, List<EffectAssignment>> GetExistingAssignments(ThrottleVFXManager manager)
        {
            Dictionary<GameObject, List<EffectAssignment>> assignmentsByEffect = new();
            if (manager.FXModeActionEvents == null)
            {
                return assignmentsByEffect;
            }

            foreach (ThrottleVFXManager.FXModeActionEvent modeActionEvent in manager.FXModeActionEvents)
            {
                if (modeActionEvent?.ActionEvents == null)
                {
                    continue;
                }

                foreach (ThrottleVFXManager.FXActionEvent actionEvent in modeActionEvent.ActionEvents)
                {
                    if (actionEvent?.EngineEffects == null)
                    {
                        continue;
                    }

                    foreach (ThrottleVFXManager.EngineEffect effect in actionEvent.EngineEffects)
                    {
                        if (effect?.EffectReference == null)
                        {
                            continue;
                        }

                        if (!assignmentsByEffect.TryGetValue(effect.EffectReference, out List<EffectAssignment> assignments))
                        {
                            assignments = new List<EffectAssignment>();
                            assignmentsByEffect.Add(effect.EffectReference, assignments);
                        }

                        assignments.Add(new EffectAssignment(modeActionEvent.EngineModeIndex, actionEvent.ModeEvent));
                    }
                }
            }

            return assignmentsByEffect;
        }
    }
}
