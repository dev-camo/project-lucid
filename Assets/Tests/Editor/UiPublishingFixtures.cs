using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // A container manager whose open set, exclusivity answer and close side effect are the fixture's. Every open and
    // close is recorded by identifier name.
    internal sealed class ScriptedExclusivityContainerManager : UIContainerManager
    {
        public readonly List<string> Log = new List<string>();
        public readonly HashSet<UIContainerIdentifier> Open = new HashSet<UIContainerIdentifier>();

        // What AreExclusivelyOpen answers, and the identifiers it was last asked about.
        public bool Exclusive;
        public IReadOnlyList<UIContainerIdentifier> ExclusiveQuery;

        // Runs inside Close, before the container leaves the open set.
        public Action OnClose;

        public override UIContainer OpenContainer(UIContainer containerPrefab)
        {
            Log.Add("open:" + containerPrefab.Identifier.Name);
            Open.Add(containerPrefab.Identifier);
            return containerPrefab;
        }

        public override void CloseAll() { }

        public override void Close(UIContainerIdentifier identifier)
        {
            Log.Add("close:" + identifier.Name);
            OnClose?.Invoke();
            Open.Remove(identifier);
        }

        public override bool IsOpen(UIContainerIdentifier identifier) => Open.Contains(identifier);

        public override bool IsAnyOpen() => Open.Count != 0;

        public override bool AreExclusivelyOpen(IReadOnlyList<UIContainerIdentifier> identifiers)
        {
            ExclusiveQuery = identifiers;
            return Exclusive;
        }

        public override bool TryGet(UIContainerIdentifier identifier, out UIContainer container)
        {
            container = null;
            return false;
        }

        public override void CloseAllExcept(IReadOnlyList<UIContainerIdentifier> exceptFor) { }
    }

    // Real App and UIManager rows for the components that publish UI events or open and close containers. Nothing here
    // is faked: the App is the genuine class built by its own initialisers on an inactive host (Awake does not run) and the
    // UIManager is the genuine class whose only container manager is the scripted one.
    internal static class UiPublishingFixtures
    {
        // The App keeps its published UI events in its storage; the Awake that would create the list does not run.
        // The SaveManager and UIScreenTransitionManager rows are the ones its field initialisers cache references from.
        internal static App NewApp(Owned owned)
        {
            owned.LeaseRow<SaveManager>();
            owned.LeaseRow<UIScreenTransitionManager>();
            App app = owned.Register(owned.Host("Lucid ui publishing app", false).AddComponent<App>());
            app.Storage.SetValue(AppFSMKeys.UIModernEvents, new List<UIModernEvent>());
            return app;
        }

        // The Free container manager is replaced by the scripted one (the manager's Awake, which builds the real ones,
        // does not run).
        internal static UIManager NewUiManager(Owned owned, out ScriptedExclusivityContainerManager containers)
        {
            UIManager manager = owned.Host("Lucid ui publishing manager", false).AddComponent<UIManager>();
            containers = new ScriptedExclusivityContainerManager();
            Get<Dictionary<UIContainerBehaviour, UIContainerManager>>(manager, "m_containerManagers")[UIContainerBehaviour.Free] = containers;
            return owned.Register(manager);
        }

        // Events compare by GUID, so each one is given a GUID of its own.
        internal static UIModernEvent NewEvent(Owned owned, string name) =>
            Create<UIModernEvent>(owned, "{\"m_guid\":\"lucid-event-" + name + "\"}");

        // Identifiers compare, hash and key the manager's open set by their GUID.
        internal static UIContainerIdentifier NewIdentifier(Owned owned, string name) =>
            Create<UIContainerIdentifier>(owned, "{\"m_name\":\"" + name + "\",\"m_guid\":\"lucid-container-" + name + "\"}");

        // A container the manager can open: its identifier holds it as the loaded prefab.
        internal static T NewContainer<T>(Owned owned, UIContainerIdentifier identifier) where T : UIContainer
        {
            var container = owned.Host("Lucid container " + identifier.Name, false).AddComponent<T>();
            identifier.RegisterContainer(container);
            Set(container, "m_identifier", identifier); // serialized
            Set(container, "m_behaviour", UIContainerBehaviour.Free); // serialized
            return container;
        }
    }
}
