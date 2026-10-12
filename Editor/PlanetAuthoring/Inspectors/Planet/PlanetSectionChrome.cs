using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// Puts a section's actions in its foldout header, and wires the header's preview draw toggles.
    /// </summary>
    public static class PlanetSectionChrome
    {
        private const string ACTIONS_CLASS = "planet-section-actions";
        private const string ACTIVE_CLASS = "is-active";

        /// <summary>
        /// Moves every planet-section-actions row under <paramref name="tab" /> into the header of the foldout it
        /// sits in.
        /// </summary>
        /// <remarks>
        /// The header is the foldout's toggle, so a click on an action would also fold the section. The actions stop
        /// their pointer events from reaching it.
        /// </remarks>
        /// <param name="tab">The tab content.</param>
        public static void MoveActionsIntoHeaders(VisualElement tab)
        {
            tab.Query(className: ACTIONS_CLASS).ForEach(actions =>
            {
                var foldout = actions.GetFirstAncestorOfType<Foldout>();
                VisualElement header = foldout?.Q<Toggle>()?.Q(className: Toggle.inputUssClassName);
                if (header == null)
                    return;

                header.Add(actions);
                actions.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                actions.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
                actions.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            });
        }

        /// <summary>
        /// Wires a draw toggle to a preview driver: it draws the feature while active, and is disabled without a
        /// preview that has the driver booted.
        /// </summary>
        /// <param name="button">The toggle button.</param>
        /// <param name="selectDriver">Picks the driver from the running session.</param>
        /// <returns>The refresh to run when preview state may have changed.</returns>
        public static Action WireDrawToggle(Button button, Func<PlanetAuthoringSession, IPreviewDriver> selectDriver)
        {
            if (button == null)
                return () => { };

            button.clicked += () =>
            {
                IPreviewDriver driver = CurrentDriver(selectDriver);
                if (driver == null)
                    return;

                driver.Enabled = !driver.Enabled;
                button.EnableInClassList(ACTIVE_CLASS, driver.Enabled);
                SceneView.RepaintAll();
            };

            return () =>
            {
                IPreviewDriver driver = CurrentDriver(selectDriver);
                bool usable = driver != null && driver.Booted;
                button.SetEnabled(usable);
                button.EnableInClassList(ACTIVE_CLASS, usable && driver.Enabled);
                button.tooltip = usable
                    ? "Draw this in the running preview."
                    : driver == null ? "Start the preview to draw this." : driver.Status;
            };
        }

        private static IPreviewDriver CurrentDriver(Func<PlanetAuthoringSession, IPreviewDriver> selectDriver)
        {
            PlanetAuthoringSession session = PlanetAuthoringSession.Active;
            return session != null ? selectDriver(session) : null;
        }
    }
}
