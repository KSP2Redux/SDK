using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Widgets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Modding
{
    /// <summary>
    /// Features and Excluded lists that edit the feature labels of an authored thing's content assets together, such
    /// as a part's JSON or a body's JSON, science regions and resources.
    /// </summary>
    /// <remarks>
    /// Only assets carrying a content label are read and written, since the feature gate filters loads by those
    /// labels. Prefabs, textures and the like load because gated content names them, so they need no labels.
    /// </remarks>
    public static class AssetFeaturesSection
    {
        // Long enough that a pause mid-word does not declare a half-typed label
        private const long SAVE_DELAY_MS = 800;

        /// <summary>
        /// Builds the section. It shows every feature the content assets require or exclude between them, and writes
        /// the lists to all of them.
        /// </summary>
        /// <param name="assetPaths">Gets the candidate assets' project paths, asked again on every save.</param>
        /// <param name="owner">The mod the assets belong to, or null for Redux's own content.</param>
        /// <returns>The section.</returns>
        public static VisualElement Build(Func<IReadOnlyList<string>> assetPaths, Mod owner)
        {
            string ownerId = owner != null ? owner.id : FeatureCatalog.REDUX_MOD_ID;
            var root = new VisualElement();
            root.AddToClassList("data-editor-inline-block");

            List<AddressableAssetEntry> ContentEntries() =>
                assetPaths()
                    .Select(AssetFeatureLabels.EntryOf)
                    .Where(entry => entry != null && AssetFeatureLabels.IsGatedContent(entry.labels))
                    .ToList();

            (List<string> features, List<string> noFeatures) =
                AssetFeatureLabels.ReadAll(ContentEntries().Select(entry => entry.labels), ownerId);

            IVisualElementScheduledItem pendingSave = null;
            void Save() =>
                AssetFeatureLabels.Apply(ContentEntries().Select(entry => entry.AssetPath), ownerId, features, noFeatures);
            void SaveSoon()
            {
                pendingSave?.Pause();
                pendingSave = root.schedule.Execute(Save).StartingIn(SAVE_DELAY_MS);
            }

            root.Add(BuildList("Features", features, owner, Save, SaveSoon));
            root.Add(BuildList("Excluded Features", noFeatures, owner, Save, SaveSoon));
            return root;
        }

        private static VisualElement BuildList(string title, List<string> ids, Mod owner, Action save, Action saveSoon)
        {
            CardListSection.ListHandle handle = null;
            handle = CardListSection.BuildFromList(() => ids, new CardListSection.ListConfig<string>
            {
                Title = title,
                AddButtonText = "+ Add",
                OnAddClicked = () =>
                {
                    ids.Add(string.Empty);
                    handle.Rebuild();
                },
                BuildCard = (id, index) => BuildRow(ids, index, owner, saveSoon, () =>
                {
                    ids.RemoveAt(index);
                    save();
                    handle.Rebuild();
                })
            });
            return handle.Root;
        }

        private static VisualElement BuildRow(List<string> ids, int index, Mod owner, Action saveSoon, Action remove)
        {
            var row = new VisualElement();
            row.AddToClassList("data-editor-inline-row");

            var field = new FeatureField(ids[index], null, owner, value =>
            {
                ids[index] = value;
                saveSoon();
            });
            field.AddToClassList("data-editor-inline-row__grow");
            row.Add(field);

            var removeButton = new Button(remove) { text = "X" };
            removeButton.AddToClassList("data-editor-card-remove-btn");
            row.Add(removeButton);
            return row;
        }
    }
}
