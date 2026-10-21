using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Level widgets and zone pages for the level select fixtures. A level widget is a real UIWidgetSDTLevel on an owned inactive
    // host with its toggle and navigation; the toggle's transition has an empty settings list, so changing its state queues
    // nothing. The widget is not set up (its Setup also reads the progression, the data manager and the mission list): the lock
    // state the zone pages read is written to its binding and its parameters, the two things Setup would derive from the level.
    internal static class LevelSelectZoneTestSupport
    {
        internal sealed class LevelWidget
        {
            internal UIWidgetSDTLevel Widget;
            internal UIToggle Toggle;
            internal UIWidgetNavigation Navigation;
            internal GameplayLevelDefinition Level;
            internal UIWidgetForGameplayLevelDefinition Entry;
        }

        // The rows the widgets' and zone pages' field initialisers cache references from, other than the data manager. Taken once per case.
        internal static void LeaseRows(Owned owned)
        {
            owned.LeaseRow<UINavigationManager>();
            owned.LeaseRow<ProgressionManager>();
        }

        internal static LevelWidget NewLevelWidget(Owned owned, string guid, bool locked)
        {
            var result = new LevelWidget();
            GameObject host = owned.Host("Lucid level widget " + guid, false);
            result.Widget = host.AddComponent<UIWidgetSDTLevel>();
            result.Toggle = host.AddComponent<UIToggle>();
            result.Navigation = host.AddComponent<UIWidgetNavigation>();
            var transition = host.AddComponent<UITransitionAnimation>();
            Set(transition, "m_settings", new List<UITransitionAnimationSetting>()); // Reflection: authored (empty) transition settings
            Set(result.Toggle, "m_transition", transition); // Reflection: serialized references
            Set(result.Widget, "m_toggle", result.Toggle);
            Set(result.Widget, "m_uiWidgetNavigation", result.Navigation);
            result.Level = owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
            Set(result.Level, "m_guid", guid); // Reflection: authored GUID of a serialized definition
            result.Widget.IsLocked.Value = locked;
            // Reflection: what UIWidgetSDTLevel.Setup stores; ToggleWidgetNavigation reads only its Locked flag.
            Set(result.Widget, "m_parameters", new UIWidgetSDTLevelParameters(result.Level, null, null, false, locked, true, false, 1, false));
            result.Entry = new UIWidgetForGameplayLevelDefinition();
            Set(result.Entry, "m_gameplayLevelDefinition", result.Level); // Reflection: serialized references
            Set(result.Entry, "m_widgetSDTLevel", result.Widget);
            result.Navigation.enabled = false;
            return result;
        }

        // A zone page over these widgets, with the preselected level the zone's parameters name (the page is not set up, since its
        // Setup follows the progression and save managers; the parameters it would store are written instead).
        internal static UIContainerLevelSelectZone NewZonePage(Owned owned, GameplayLevelDefinition preselected, params LevelWidget[] widgets)
        {
            var page = owned.Host("Lucid level select zone", false).AddComponent<UIContainerLevelSelectZone>();
            var entries = new UIWidgetForGameplayLevelDefinition[widgets.Length];
            for (int i = 0; i < widgets.Length; i++) entries[i] = widgets[i].Entry;
            Set(page, "m_widgetForGameplayLevelDefinitions", entries); // Reflection: serialized array
            Set(page, "m_parameters", new UIContainerLevelSelectZoneParameters(preselected, null)); // Reflection: stored by Setup
            return page;
        }

    }
}
