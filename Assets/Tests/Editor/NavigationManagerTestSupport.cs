using HardlightProject;
using Hardlight;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // A real UINavigationManager for the container cases whose original code asks the registry for it. It is built as the
    // constructor expects: the navigation configuration in the system configuration and a UI
    // runtime configuration holding the input bridge. The probe bridge answers whether the input type requires selection as the
    // case sets it when the manager is constructed (so the manager is active or not), and afterwards records the objects the
    // navigation selects instead of reaching an event system; it asks for none after construction, so no highlight is spawned.
    // Each registration, the shutdown subscription and the static input-type listener the constructor adds are released with
    // the case.
    internal static class NavigationManagerTestSupport
    {
        internal sealed class Navigation
        {
            internal UINavigationManager Manager;
            internal NavigationSelectionBridgeProbe Bridge;
        }

        // The world must already lease the Shutdown row before any manager subscribes (MissionTrackerTestSupport does); a second
        // lease taken here would be released while an earlier-subscribed manager has already left the row.
        internal static Navigation Add(Owned owned, bool active = true)
        {
            FastAction<UIInputType> priorListeners = InputBridge.OnUpdateLastInputType;
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => InputBridge.OnUpdateLastInputType = priorListeners));
            var systemConfiguration = owned.Register(new SystemConfiguration());
            var configuration = owned.Track(ScriptableObject.CreateInstance<UINavigationConfiguration>());
            // The override SystemConfiguration.AddConfig makes, into this case's own configuration system.
            systemConfiguration.StackableData.AddOverride(HLCRC32.GenerateInt(nameof(UINavigationConfiguration)), configuration);
            var bridge = owned.Track(ScriptableObject.CreateInstance<NavigationSelectionBridgeProbe>());
            bridge.RequiresSelection = active;
            var runtimeConfiguration = owned.Host("Lucid navigation ui configuration", false).AddComponent<UIRuntimeConfiguration>();
            Set(runtimeConfiguration, "m_inputBridge", bridge); // Reflection: serialized reference the editor assigns
            owned.Register(runtimeConfiguration);
            var manager = new UINavigationManager();
            owned.Register(manager);
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() =>
            {
                ProcessManager.ProcessSystemAction(manager, SystemAction.Shutdown);
                ProcessManager.UnsubscribeFromAllActions(manager);
            }));
            Assert.That(manager.IsActive, Is.EqualTo(active), "the bridge answered for the input type while the manager was constructed");
            bridge.RequiresSelection = false;
            return new Navigation { Manager = manager, Bridge = bridge };
        }
    }
}
