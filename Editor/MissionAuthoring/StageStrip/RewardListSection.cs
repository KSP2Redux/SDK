using System;
using System.Collections.Generic;
using System.Reflection;
using KSP.Game.Missions;
using KSP.Game.Missions.Definitions;
using Ksp2UnityTools.Editor.Widgets;
using Redux.Missions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.StageStrip
{
    /// <summary>
    /// Per-stage list of <see cref="MissionRewardDefinition" /> entries. Backed by
    /// <see cref="CardListSection.BuildFromList{T}" /> so chrome matches the rest of the
    /// SDK's card-list surfaces. Each entry renders as a CardShell card with three
    /// reward fields (Type / Amount / Key) in its body.
    /// </summary>
    public class RewardListSection : VisualElement
    {
        /// <summary>
        /// Gets the mission that owns the backing reward list.
        /// </summary>
        public Mission Mission { get; }

        /// <summary>
        /// Resolves to the current backing reward list on every access. Returns null if the resolver yields null.
        /// </summary>
        public IList<MissionRewardDefinition> Rewards => _rewardsResolver?.Invoke();

        private readonly Func<IList<MissionRewardDefinition>> _rewardsResolver;
        private readonly CardListSection.ListHandle _handle;

        /// <summary>
        /// Creates a new reward list section bound to the given mission and backing list.
        /// </summary>
        /// <param name="mission">The mission that owns the backing reward list.</param>
        /// <param name="rewardsResolver">Accessor returning the current backing list, re-evaluated on every access.</param>
        /// <param name="title">The header title shown above the card container.</param>
        public RewardListSection(Mission mission, Func<IList<MissionRewardDefinition>> rewardsResolver, string title)
        {
            Mission = mission;
            _rewardsResolver = rewardsResolver;

            AddToClassList("reward-list-section");

            _handle = CardListSection.BuildFromList<MissionRewardDefinition>(rewardsResolver, new CardListSection.ListConfig<MissionRewardDefinition>
            {
                Title = title,
                AddButtonTooltip = "Add reward",
                EmptyHintText = "(none)",
                OnAddClicked = OnAddClicked,
                BuildCard = BuildRewardCard,
            });

            Add(_handle.Root);
        }

        /// <summary>
        /// Rebuilds the card container from the current backing list. Called by the strip view's
        /// undo cascade after a Ctrl+Z restores the mission state.
        /// </summary>
        public void Reconcile() => _handle?.Rebuild?.Invoke();

        private void OnAddClicked()
        {
            var list = Rewards;
            if (list == null) return;
            Undo.RegisterCompleteObjectUndo(Mission, "Add reward");
            list.Add(new MissionRewardDefinition());
            EditorUtility.SetDirty(Mission);
            _handle?.Rebuild?.Invoke();
        }

        private VisualElement BuildRewardCard(MissionRewardDefinition entry, int index)
        {
            var list = Rewards;
            int count = list?.Count ?? 0;

            var card = CardShell.Build(out var slots);

            var title = new Label($"[{index}]");
            title.AddToClassList("data-editor-card-name-field");
            slots.Header.Add(title);

            AddReorderAndDeleteButtons(slots.Header, index, count);

            BuildRewardFields(slots.Body, entry);
            return card;
        }

        private void BuildRewardFields(VisualElement body, MissionRewardDefinition entry)
        {
            var typeFieldInfo = typeof(MissionRewardDefinition).GetField(nameof(MissionRewardDefinition.MissionRewardType));
            var amountFieldInfo = typeof(MissionRewardDefinition).GetField(nameof(MissionRewardDefinition.RewardAmount));
            var keyFieldInfo = typeof(MissionRewardDefinition).GetField(nameof(MissionRewardDefinition.RewardKey));

            body.Add(BuildEnumField(typeFieldInfo, entry));
            body.Add(BuildFloatField(amountFieldInfo, entry));
            body.Add(BuildStringField(keyFieldInfo, entry));
        }

        private VisualElement BuildEnumField(FieldInfo field, MissionRewardDefinition entry)
        {
            var label = field.GetCustomAttribute<InspectorLabel>()?.Label ?? field.Name;
            var tooltip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip;
            var current = (Enum)field.GetValue(entry);
            var f = new EnumField(label, current);
            f.AddToClassList("condition-row-field");
            f.AddToClassList("unity-base-field__aligned");
            if (!string.IsNullOrEmpty(tooltip)) f.tooltip = tooltip;
            f.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(Mission, "Edit reward type");
                field.SetValue(entry, e.newValue);
                EditorUtility.SetDirty(Mission);
            });
            return f;
        }

        private VisualElement BuildFloatField(FieldInfo field, MissionRewardDefinition entry)
        {
            var label = field.GetCustomAttribute<InspectorLabel>()?.Label ?? field.Name;
            var tooltip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip;
            var f = new FloatField(label) { value = (float)field.GetValue(entry), isDelayed = true };
            f.AddToClassList("condition-row-field");
            f.AddToClassList("unity-base-field__aligned");
            if (!string.IsNullOrEmpty(tooltip)) f.tooltip = tooltip;
            f.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(Mission, "Edit reward amount");
                field.SetValue(entry, e.newValue);
                EditorUtility.SetDirty(Mission);
            });
            return f;
        }

        private VisualElement BuildStringField(FieldInfo field, MissionRewardDefinition entry)
        {
            var label = field.GetCustomAttribute<InspectorLabel>()?.Label ?? field.Name;
            var tooltip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip;
            var f = new TextField(label) { value = (string)field.GetValue(entry) ?? string.Empty, isDelayed = true };
            f.AddToClassList("condition-row-field");
            f.AddToClassList("unity-base-field__aligned");
            if (!string.IsNullOrEmpty(tooltip)) f.tooltip = tooltip;
            f.RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(Mission, "Edit reward key");
                field.SetValue(entry, e.newValue ?? string.Empty);
                EditorUtility.SetDirty(Mission);
            });
            return f;
        }

        private void AddReorderAndDeleteButtons(VisualElement header, int idx, int count)
        {
            var upBtn = new Button(() => MoveReward(idx, -1)) { text = "▲", tooltip = "Move up" };
            upBtn.AddToClassList("condition-row-reorder-btn");
            upBtn.SetEnabled(idx > 0);
            header.Add(upBtn);

            var downBtn = new Button(() => MoveReward(idx, +1)) { text = "▼", tooltip = "Move down" };
            downBtn.AddToClassList("condition-row-reorder-btn");
            downBtn.SetEnabled(idx < count - 1);
            header.Add(downBtn);

            var deleteBtn = new Button(() => DeleteReward(idx)) { text = "X", tooltip = "Delete reward" };
            deleteBtn.AddToClassList("condition-row-delete-btn");
            header.Add(deleteBtn);
        }

        private void MoveReward(int index, int delta)
        {
            var list = Rewards;
            if (list == null) return;
            int target = index + delta;
            if (target < 0 || target >= list.Count) return;
            Undo.RegisterCompleteObjectUndo(Mission, "Reorder reward");
            var item = list[index];
            list.RemoveAt(index);
            list.Insert(target, item);
            EditorUtility.SetDirty(Mission);
            _handle?.Rebuild?.Invoke();
        }

        private void DeleteReward(int index)
        {
            var list = Rewards;
            if (list == null || index < 0 || index >= list.Count) return;
            Undo.RegisterCompleteObjectUndo(Mission, "Delete reward");
            list.RemoveAt(index);
            EditorUtility.SetDirty(Mission);
            _handle?.Rebuild?.Invoke();
        }
    }
}
