using KSP.VFX;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KSP.Editor
{
    [CustomEditor(typeof(ThrottleVFXManager))]
    public class ThrottleVFXManagerEditor : UnityEditor.Editor
    {
        private ThrottleVFXManager _target;

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

        private void OnEnable()
        {
            _target = (ThrottleVFXManager)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            DrawGatherEffectsTools();
            EditorGUILayout.Space();
            ThrottleVFXPreviewBridge.DrawPreviewControls(_target, showHeader: true);
        }

        private void DrawGatherEffectsTools()
        {
            EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
            if (GUILayout.Button("Gather Effects From Children"))
            {
                int gatheredCount = GatherEffectsFromChildren(_target);
                ThrottleVFXPreviewBridge.ActivateAndRefresh(_target);
                Debug.Log($"Gathered {gatheredCount} engine VFX effect references for {_target.name}.", _target);
            }
        }

        private static int GatherEffectsFromChildren(ThrottleVFXManager manager)
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
            return manager.FXModeActionEvents.Sum(modeEvent => modeEvent.ActionEvents.Sum(actionEvent => actionEvent.EngineEffects.Length));
        }

        private static List<EffectAssignment> GetAssignments(
            MonoBehaviour behaviour,
            Dictionary<GameObject, List<EffectAssignment>> existingAssignments
        )
        {
            if (behaviour is IEngineFXDataModeHint { HasEngineFXModeHint: true } modeHint)
            {
                return new List<EffectAssignment>
                {
                    new(Mathf.Max(0, modeHint.EngineFXEngineModeIndex), modeHint.EngineFXModeEvent)
                };
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
