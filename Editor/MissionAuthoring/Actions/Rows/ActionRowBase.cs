using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using KSP.Game.Missions;
using KSP.Messages;
using Ksp2UnityTools.Editor.MissionAuthoring.Widgets;
using Ksp2UnityTools.Editor.Widgets;
using Newtonsoft.Json;
using Redux.Missions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Actions.Rows
{
    /// <summary>
    /// Shared chrome for every action row.
    /// </summary>
    /// <remarks>
    /// Holds the row's backing <see cref="IMissionAction" /> and the callbacks the row needs to mutate its place in the list. <see cref="Replace" /> swaps the action with another or null. <see cref="NotifyChanged" /> asks the parent section to refresh derived UI state. <see cref="MoveUp" /> and <see cref="MoveDown" /> reorder within the list.
    /// </remarks>
    public abstract class ActionRowBase : VisualElement
    {
        private const int MAX_RECURSION_DEPTH = 2;

        /// <summary>
        /// Gets the mission asset the action belongs to.
        /// </summary>
        public Mission Mission { get; }

        /// <summary>
        /// Gets the action instance this row edits.
        /// </summary>
        public IMissionAction Action { get; }

        /// <summary>
        /// Gets the callback that swaps <see cref="Action" /> with another instance or removes it when passed null.
        /// </summary>
        protected System.Action<IMissionAction> Replace { get; }

        /// <summary>
        /// Gets the callback that asks the parent section to refresh derived UI state after a field change.
        /// </summary>
        protected System.Action NotifyChanged { get; }

        /// <summary>
        /// Gets the optional callback that reorders this row up within its parent list. Null when the row is already at the top.
        /// </summary>
        protected System.Action MoveUp { get; }

        /// <summary>
        /// Gets the optional callback that reorders this row down within its parent list. Null when the row is already at the bottom.
        /// </summary>
        protected System.Action MoveDown { get; }

        /// <summary>
        /// Creates a new <see cref="ActionRowBase" /> bound to the supplied action and callbacks.
        /// </summary>
        /// <param name="mission">The mission asset that owns the action.</param>
        /// <param name="action">The action instance to edit.</param>
        /// <param name="replace">Callback to swap the action with another or null.</param>
        /// <param name="notifyChanged">Callback fired when the action's state changes.</param>
        /// <param name="moveUp">Optional callback to reorder this row up within its parent list.</param>
        /// <param name="moveDown">Optional callback to reorder this row down within its parent list.</param>
        protected ActionRowBase(Mission mission, IMissionAction action, System.Action<IMissionAction> replace, System.Action notifyChanged, System.Action moveUp = null, System.Action moveDown = null)
        {
            Mission = mission;
            Action = action;
            Replace = replace;
            NotifyChanged = notifyChanged;
            MoveUp = moveUp;
            MoveDown = moveDown;
            AddToClassList("action-row");
        }

        /// <summary>
        /// Builds the card chrome (title plus reorder plus delete header plus body container) and returns the body element so the subclass can append its field widgets.
        /// </summary>
        /// <returns>The empty body container the subclass should append field widgets to.</returns>
        protected VisualElement BuildCard()
        {
            AddToClassList("condition-row-property-card");

            var header = new VisualElement();
            header.AddToClassList("condition-row-card-header");

            var title = new Label(ResolveTitleText());
            title.AddToClassList("condition-row-card-title");
            header.Add(title);

            var spacer = new VisualElement();
            spacer.AddToClassList("condition-row-header-spacer");
            header.Add(spacer);

            BuildHeaderReorderButtons(header);
            BuildHeaderDeleteButton(header);

            Add(header);

            var body = new VisualElement();
            body.AddToClassList("condition-row-property-body");
            Add(body);

            return body;
        }

        /// <summary>
        /// Adds a widget for a serialized field of <see cref="Action" /> to <paramref name="parent" />.
        /// </summary>
        /// <remarks>
        /// Picks the right widget based on the field's type, reads <c>[Tooltip]</c>, <c>[InspectorLabel]</c>, and <c>[InspectorTypeFilter]</c> from the field, and wires Undo, SetDirty, and <see cref="NotifyChanged" /> on changes.
        /// </remarks>
        /// <param name="parent">The container that receives the new widget.</param>
        /// <param name="field">The serialized field on <see cref="Action" /> to render.</param>
        /// <returns>The widget that was added, or null if the field type is unsupported and a placeholder hint was added instead.</returns>
        protected VisualElement BuildScalarField(VisualElement parent, FieldInfo field)
        {
            return BuildFieldOnOwner(parent, field, Action, 0);
        }

        /// <summary>
        /// Walks <paramref name="fields" /> in order, inserting a bold section header label whenever a field's <see cref="InspectorSection" /> value differs from the previous field's.
        /// </summary>
        /// <remarks>
        /// Fields without the attribute render without a header. Header labels use the <c>.action-row-section-header</c> USS class.
        /// </remarks>
        /// <param name="body">The container that receives the field widgets and any section headers.</param>
        /// <param name="fields">The serialized fields to render in declared order.</param>
        protected void BuildFieldsBySection(VisualElement body, IEnumerable<FieldInfo> fields)
        {
            BuildFieldsBySectionOnOwner(body, fields, Action, 0);
        }

        /// <summary>
        /// Adds the inherited <see cref="MissionActionBase.StageEvent" /> field as the last field in <paramref name="body" />.
        /// </summary>
        /// <remarks>
        /// Custom rows call this at the end of their body build so authors keep access to the audio-event hook on every action.
        /// </remarks>
        /// <param name="body">The container that receives the StageEvent widget.</param>
        protected void BuildStageEventField(VisualElement body)
        {
            if (Action == null) return;
            var stageEventField = typeof(MissionActionBase).GetField(nameof(MissionActionBase.StageEvent), BindingFlags.Public | BindingFlags.Instance);
            if (stageEventField == null) return;
            BuildScalarField(body, stageEventField);
        }

        /// <summary>
        /// Writes a new value to <paramref name="field" /> on <see cref="Action" /> with Undo, SetDirty, and <see cref="NotifyChanged" /> wired up.
        /// </summary>
        /// <remarks>
        /// Used by every field-change callback.
        /// </remarks>
        /// <param name="field">The serialized field on <see cref="Action" /> to write.</param>
        /// <param name="newValue">The new value to assign.</param>
        protected void CommitFieldChange(FieldInfo field, object newValue)
        {
            CommitFieldChangeOnOwner(field, Action, newValue);
        }

        /// <summary>
        /// Returns the title text for the card header.
        /// </summary>
        /// <remarks>
        /// Reads from <see cref="ActionTypeCatalog" />'s resolved display name, falling back to the C# type name when the action is null or untyped.
        /// </remarks>
        /// <returns>The resolved display name, or a fallback when the action is null.</returns>
        protected string ResolveTitleText()
        {
            if (Action == null) return "Unknown Action";
            var entry = ActionTypeCatalog.FindByType(Action.GetType());
            return entry?.DisplayName ?? Action.GetType().Name;
        }

        /// <summary>
        /// Converts a C# field name to a human-friendly label.
        /// </summary>
        /// <remarks>
        /// Splits on underscores and at camel/Pascal-case word boundaries, then capitalizes each token.
        /// </remarks>
        /// <param name="raw">The raw field name.</param>
        /// <returns>The normalized, space-separated label, or the original string when null or empty.</returns>
        public static string NormalizeFieldName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            var sb = new StringBuilder(raw.Length + 4);
            bool prevLower = false;
            bool prevDigit = false;
            foreach (var c in raw)
            {
                if (c == '_')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                    prevLower = false;
                    prevDigit = false;
                    continue;
                }
                bool isUpper = char.IsUpper(c);
                bool isDigit = char.IsDigit(c);
                if (isUpper && (prevLower || prevDigit) && sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                if (sb.Length == 0 || sb[sb.Length - 1] == ' ') sb.Append(char.ToUpperInvariant(c));
                else sb.Append(c);
                prevLower = char.IsLower(c);
                prevDigit = isDigit;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Adds the reorder buttons (up and down) to the supplied header.
        /// </summary>
        /// <remarks>
        /// Subclasses call this before <see cref="BuildHeaderDeleteButton" />.
        /// </remarks>
        /// <param name="header">The header element that receives the buttons.</param>
        protected void BuildHeaderReorderButtons(VisualElement header)
        {
            if (MoveUp == null && MoveDown == null) return;
            var upBtn = new Button(MoveUp ?? (() => { })) { text = "▲", tooltip = "Move up" };
            upBtn.AddToClassList("condition-row-reorder-btn");
            upBtn.SetEnabled(MoveUp != null);
            header.Add(upBtn);

            var downBtn = new Button(MoveDown ?? (() => { })) { text = "▼", tooltip = "Move down" };
            downBtn.AddToClassList("condition-row-reorder-btn");
            downBtn.SetEnabled(MoveDown != null);
            header.Add(downBtn);
        }

        /// <summary>
        /// Adds the delete (X) button as the rightmost element of the header.
        /// </summary>
        /// <param name="header">The header element that receives the delete button.</param>
        protected void BuildHeaderDeleteButton(VisualElement header)
        {
            var deleteBtn = new Button(() => Replace?.Invoke(null)) { text = "X", tooltip = "Delete action" };
            deleteBtn.AddToClassList("condition-row-delete-btn");
            header.Add(deleteBtn);
        }

        #region Field rendering (internal recursion path)

        private void BuildFieldsBySectionOnOwner(VisualElement body, IEnumerable<FieldInfo> fields, object owner, int depth)
        {
            string currentSection = null;
            foreach (var field in fields)
            {
                var section = field.GetCustomAttribute<InspectorSection>()?.Section;
                if (section != currentSection)
                {
                    currentSection = section;
                    if (!string.IsNullOrEmpty(section))
                    {
                        var header = new Label(section);
                        header.AddToClassList("action-row-section-header");
                        body.Add(header);
                    }
                }
                BuildFieldOnOwner(body, field, owner, depth);
            }
        }

        private VisualElement BuildFieldOnOwner(VisualElement parent, FieldInfo field, object owner, int depth)
        {
            if (field == null || owner == null) return null;
            string label = field.GetCustomAttribute<InspectorLabel>()?.Label;
            if (string.IsNullOrEmpty(label)) label = NormalizeFieldName(field.Name);
            string tooltip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip;
            var ft = field.FieldType;

            if (ft == typeof(Type))
            {
                return BuildTypeField(parent, field, owner, label, tooltip);
            }
            if (IsListType(ft))
            {
                return BuildListField(parent, field, owner, label, tooltip, depth);
            }
            if (IsNestedSerializableClass(ft))
            {
                return BuildNestedObjectField(parent, field, owner, label, tooltip, depth);
            }

            return BuildPrimitiveField(parent, field, owner, label, tooltip, ft);
        }

        private VisualElement BuildPrimitiveField(VisualElement parent, FieldInfo field, object owner, string label, string tooltip, Type ft)
        {
            VisualElement widget;
            if (ft == typeof(string))
            {
                var f = new TextField(label) { value = (field.GetValue(owner) as string) ?? string.Empty, isDelayed = true };
                f.RegisterValueChangedCallback(e => CommitFieldChangeOnOwner(field, owner, e.newValue ?? string.Empty));
                widget = f;
            }
            else if (ft == typeof(bool))
            {
                var f = new Toggle(label) { value = (bool)field.GetValue(owner) };
                f.RegisterValueChangedCallback(e => CommitFieldChangeOnOwner(field, owner, e.newValue));
                widget = f;
            }
            else if (ft == typeof(int))
            {
                var f = new IntegerField(label) { value = (int)field.GetValue(owner), isDelayed = true };
                f.RegisterValueChangedCallback(e => CommitFieldChangeOnOwner(field, owner, e.newValue));
                widget = f;
            }
            else if (ft == typeof(float))
            {
                var f = new FloatField(label) { value = (float)field.GetValue(owner), isDelayed = true };
                f.RegisterValueChangedCallback(e => CommitFieldChangeOnOwner(field, owner, e.newValue));
                widget = f;
            }
            else if (ft == typeof(double))
            {
                var f = new DoubleField(label) { value = (double)field.GetValue(owner), isDelayed = true };
                f.RegisterValueChangedCallback(e => CommitFieldChangeOnOwner(field, owner, e.newValue));
                widget = f;
            }
            else if (ft.IsEnum)
            {
                var current = (Enum)field.GetValue(owner);
                var f = new EnumField(label, current);
                f.RegisterValueChangedCallback(e => CommitFieldChangeOnOwner(field, owner, e.newValue));
                widget = f;
            }
            else if (ft == typeof(Vector2))
            {
                var f = new Vector2Field(label) { value = (Vector2)field.GetValue(owner) };
                f.RegisterValueChangedCallback(e => CommitFieldChangeOnOwner(field, owner, e.newValue));
                widget = f;
            }
            else if (ft == typeof(Vector3))
            {
                var f = new Vector3Field(label) { value = (Vector3)field.GetValue(owner) };
                f.RegisterValueChangedCallback(e => CommitFieldChangeOnOwner(field, owner, e.newValue));
                widget = f;
            }
            else
            {
                var placeholder = new Label($"{label}: (complex field - use raw inspector for now)");
                placeholder.AddToClassList("action-row-complex-field-hint");
                if (!string.IsNullOrEmpty(tooltip)) placeholder.tooltip = tooltip;
                parent.Add(placeholder);
                return null;
            }

            StyleFieldWidget(widget);
            if (!string.IsNullOrEmpty(tooltip)) widget.tooltip = tooltip;
            parent.Add(widget);
            return widget;
        }

        private VisualElement BuildTypeField(VisualElement parent, FieldInfo field, object owner, string label, string tooltip)
        {
            var filter = field.GetCustomAttribute<InspectorTypeFilter>();
            if (filter?.FilterType != null && typeof(MessageCenterMessage).IsAssignableFrom(filter.FilterType))
            {
                var current = field.GetValue(owner) as Type;
                var widget = new MessageEventTypeField(label, current, t => CommitFieldChangeOnOwner(field, owner, t));
                if (!string.IsNullOrEmpty(tooltip)) widget.tooltip = tooltip;
                parent.Add(widget);
                return widget;
            }

            var placeholder = new Label($"{label}: (Type field needs [InspectorTypeFilter] decoration)");
            placeholder.AddToClassList("action-row-complex-field-hint");
            if (!string.IsNullOrEmpty(tooltip)) placeholder.tooltip = tooltip;
            parent.Add(placeholder);
            return null;
        }

        private VisualElement BuildNestedObjectField(VisualElement parent, FieldInfo field, object owner, string label, string tooltip, int depth)
        {
            if (depth >= MAX_RECURSION_DEPTH)
            {
                var placeholder = new Label($"{label}: (nested too deep - use raw inspector)");
                placeholder.AddToClassList("action-row-complex-field-hint");
                if (!string.IsNullOrEmpty(tooltip)) placeholder.tooltip = tooltip;
                parent.Add(placeholder);
                return null;
            }

            var nested = field.GetValue(owner);
            if (nested == null)
            {
                try { nested = Activator.CreateInstance(field.FieldType); field.SetValue(owner, nested); }
                catch
                {
                    var placeholder = new Label($"{label}: (cannot instantiate nested object)");
                    placeholder.AddToClassList("action-row-complex-field-hint");
                    parent.Add(placeholder);
                    return null;
                }
            }

            var container = new VisualElement();
            container.AddToClassList("action-row-nested-section");

            var header = new Label(label);
            header.AddToClassList("action-row-section-header");
            if (!string.IsNullOrEmpty(tooltip)) header.tooltip = tooltip;
            container.Add(header);

            BuildFieldsBySectionOnOwner(container, GetSerializedFields(field.FieldType), nested, depth + 1);
            parent.Add(container);
            return container;
        }

        private VisualElement BuildListField(VisualElement parent, FieldInfo field, object owner, string label, string tooltip, int depth)
        {
            var listType = field.FieldType;
            var elementType = listType.GetGenericArguments()[0];

            var listValue = field.GetValue(owner);
            if (listValue == null)
            {
                try { listValue = Activator.CreateInstance(listType); field.SetValue(owner, listValue); }
                catch
                {
                    var ph = new Label($"{label}: (cannot instantiate list)");
                    ph.AddToClassList("action-row-complex-field-hint");
                    parent.Add(ph);
                    return null;
                }
            }
            var list = (IList)listValue;

            CardListSection.ListHandle handle = null;
            handle = CardListSection.BuildFromObjectList(
                () => list,
                new CardListSection.ObjectListConfig
                {
                    Title = label,
                    AddButtonTooltip = $"Add {label} entry",
                    EmptyHintText = "(none)",
                    OnAddClicked = () =>
                    {
                        Undo.RegisterCompleteObjectUndo(Mission, $"Add {label} entry");
                        list.Add(CreateDefaultForType(elementType));
                        EditorUtility.SetDirty(Mission);
                        NotifyChanged?.Invoke();
                        handle?.Rebuild?.Invoke();
                    },
                    BuildCard = (entry, idx) => BuildListEntryCard(list, idx, elementType, depth, () => handle?.Rebuild?.Invoke()),
                });

            if (!string.IsNullOrEmpty(tooltip)) handle.Root.tooltip = tooltip;
            parent.Add(handle.Root);
            return handle.Root;
        }

        private VisualElement BuildListEntryCard(IList list, int idx, Type elementType, int depth, System.Action rebuild)
        {
            int count = list.Count;

            if (IsNestedSerializableClass(elementType))
            {
                return BuildClassListEntry(list, idx, elementType, depth, count, rebuild);
            }
            return BuildPrimitiveListEntry(list, idx, elementType, count, rebuild);
        }

        private VisualElement BuildPrimitiveListEntry(IList list, int idx, Type elementType, int count, System.Action rebuild)
        {
            var row = new VisualElement();
            row.AddToClassList("action-row-list-primitive-row");

            var widget = BuildPrimitiveValueWidget(elementType, $"[{idx}]", list[idx], v =>
            {
                Undo.RecordObject(Mission, "Edit list entry");
                list[idx] = v;
                EditorUtility.SetDirty(Mission);
                NotifyChanged?.Invoke();
            });
            StyleFieldWidget(widget);
            row.Add(widget);

            AddListEntryButtons(row, list, idx, count, rebuild);
            return row;
        }

        private VisualElement BuildClassListEntry(IList list, int idx, Type elementType, int depth, int count, System.Action rebuild)
        {
            if (depth >= MAX_RECURSION_DEPTH)
            {
                var placeholder = new Label($"[{idx}]: (nested too deep)");
                placeholder.AddToClassList("action-row-complex-field-hint");
                return placeholder;
            }

            object value = list[idx];
            if (value == null)
            {
                try { value = Activator.CreateInstance(elementType); list[idx] = value; }
                catch { }
            }

            var card = CardShell.Build(out var slots);

            var title = new Label($"[{idx}]");
            title.AddToClassList("data-editor-card-name-field");
            slots.Header.Add(title);

            AddListEntryButtons(slots.Header, list, idx, count, rebuild);

            if (value != null)
            {
                BuildFieldsBySectionOnOwner(slots.Body, GetSerializedFields(elementType), value, depth + 1);
            }
            return card;
        }

        private void AddListEntryButtons(VisualElement parent, IList list, int idx, int count, System.Action rebuild)
        {
            var upBtn = new Button(() =>
            {
                if (idx <= 0) return;
                Undo.RegisterCompleteObjectUndo(Mission, "Reorder list entry");
                var item = list[idx];
                list.RemoveAt(idx);
                list.Insert(idx - 1, item);
                EditorUtility.SetDirty(Mission);
                NotifyChanged?.Invoke();
                rebuild?.Invoke();
            }) { text = "▲", tooltip = "Move up" };
            upBtn.AddToClassList("condition-row-reorder-btn");
            upBtn.SetEnabled(idx > 0);
            parent.Add(upBtn);

            var downBtn = new Button(() =>
            {
                if (idx >= list.Count - 1) return;
                Undo.RegisterCompleteObjectUndo(Mission, "Reorder list entry");
                var item = list[idx];
                list.RemoveAt(idx);
                list.Insert(idx + 1, item);
                EditorUtility.SetDirty(Mission);
                NotifyChanged?.Invoke();
                rebuild?.Invoke();
            }) { text = "▼", tooltip = "Move down" };
            downBtn.AddToClassList("condition-row-reorder-btn");
            downBtn.SetEnabled(idx < count - 1);
            parent.Add(downBtn);

            var deleteBtn = new Button(() =>
            {
                if (idx < 0 || idx >= list.Count) return;
                Undo.RegisterCompleteObjectUndo(Mission, "Delete list entry");
                list.RemoveAt(idx);
                EditorUtility.SetDirty(Mission);
                NotifyChanged?.Invoke();
                rebuild?.Invoke();
            }) { text = "X", tooltip = "Delete entry" };
            deleteBtn.AddToClassList("condition-row-delete-btn");
            parent.Add(deleteBtn);
        }

        private static VisualElement BuildPrimitiveValueWidget(Type t, string label, object initial, System.Action<object> onChanged)
        {
            if (t == typeof(string))
            {
                var f = new TextField(label) { value = (initial as string) ?? string.Empty, isDelayed = true };
                f.RegisterValueChangedCallback(e => onChanged(e.newValue ?? string.Empty));
                return f;
            }
            if (t == typeof(bool))
            {
                var f = new Toggle(label) { value = (bool)(initial ?? false) };
                f.RegisterValueChangedCallback(e => onChanged(e.newValue));
                return f;
            }
            if (t == typeof(int))
            {
                var f = new IntegerField(label) { value = (int)(initial ?? 0), isDelayed = true };
                f.RegisterValueChangedCallback(e => onChanged(e.newValue));
                return f;
            }
            if (t == typeof(float))
            {
                var f = new FloatField(label) { value = (float)(initial ?? 0f), isDelayed = true };
                f.RegisterValueChangedCallback(e => onChanged(e.newValue));
                return f;
            }
            if (t == typeof(double))
            {
                var f = new DoubleField(label) { value = (double)(initial ?? 0.0), isDelayed = true };
                f.RegisterValueChangedCallback(e => onChanged(e.newValue));
                return f;
            }
            if (t.IsEnum)
            {
                var current = (Enum)(initial ?? Activator.CreateInstance(t));
                var f = new EnumField(label, current);
                f.RegisterValueChangedCallback(e => onChanged(e.newValue));
                return f;
            }
            if (t == typeof(Vector2))
            {
                var f = new Vector2Field(label) { value = (Vector2)(initial ?? Vector2.zero) };
                f.RegisterValueChangedCallback(e => onChanged(e.newValue));
                return f;
            }
            if (t == typeof(Vector3))
            {
                var f = new Vector3Field(label) { value = (Vector3)(initial ?? Vector3.zero) };
                f.RegisterValueChangedCallback(e => onChanged(e.newValue));
                return f;
            }
            return new Label($"{label}: (unsupported)");
        }

        private static object CreateDefaultForType(Type t)
        {
            if (t == typeof(string)) return string.Empty;
            if (t.IsValueType) return Activator.CreateInstance(t);
            try { return Activator.CreateInstance(t); }
            catch { return null; }
        }

        private static bool IsListType(Type t)
        {
            if (!t.IsGenericType) return false;
            return t.GetGenericTypeDefinition() == typeof(List<>);
        }

        private static bool IsNestedSerializableClass(Type t)
        {
            if (t.IsPrimitive || t.IsEnum) return false;
            if (t == typeof(string) || t == typeof(Type)) return false;
            if (t == typeof(Vector2) || t == typeof(Vector3) || t == typeof(Vector4)) return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(t)) return false;
            if (t.IsClass && t.GetCustomAttribute<SerializableAttribute>() != null) return true;
            return false;
        }

        #endregion

        /// <summary>
        /// Returns the public instance fields of <paramref name="t" /> that should be surfaced in the editor.
        /// </summary>
        /// <remarks>
        /// Matches the runtime's Newtonsoft serialization convention. Public fields by default, excluding any with <c>[JsonIgnore]</c> or <c>[NonSerialized]</c>. Note that <c>[JsonProperty]</c> is NOT required so plain public fields on nested types (DialogEntries.Entries, SlideData.HeaderText, etc.) are picked up.
        /// </remarks>
        /// <param name="t">The type to enumerate serialized fields from.</param>
        /// <returns>The fields that should be surfaced in the editor, in reflection declaration order.</returns>
        protected static IEnumerable<FieldInfo> GetSerializedFields(Type t)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            foreach (var field in t.GetFields(flags))
            {
                if (field.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
                if (field.GetCustomAttribute<NonSerializedAttribute>() != null) continue;
                yield return field;
            }
        }

        private void CommitFieldChangeOnOwner(FieldInfo field, object owner, object newValue)
        {
            Undo.RecordObject(Mission, $"Edit action {field.Name}");
            field.SetValue(owner, newValue);
            EditorUtility.SetDirty(Mission);
            NotifyChanged?.Invoke();
        }

        /// <summary>
        /// Applies the shared row field USS classes to <paramref name="widget" /> so it lines up with the aligned label column.
        /// </summary>
        /// <param name="widget">The widget to style.</param>
        protected static void StyleFieldWidget(VisualElement widget)
        {
            widget.AddToClassList("condition-row-field");
            widget.AddToClassList("unity-base-field__aligned");
        }

        /// <summary>
        /// Copies the field's <c>[Tooltip]</c> text to <paramref name="widget" /> when one is declared.
        /// </summary>
        /// <param name="widget">The widget that receives the tooltip.</param>
        /// <param name="field">The serialized field whose <c>[Tooltip]</c> attribute is read.</param>
        protected static void ApplyTooltip(VisualElement widget, FieldInfo field)
        {
            var tip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip;
            if (!string.IsNullOrEmpty(tip)) widget.tooltip = tip;
        }
    }
}
