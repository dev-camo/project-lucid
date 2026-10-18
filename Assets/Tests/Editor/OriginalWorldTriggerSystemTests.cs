using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.WorldTriggersShared;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime EventCollisionTrigger (0x02000764), AutoRegisterHashedComponentGroup
    // (0x020006b4) and DeactivateWhileContainersAreOpen (0x02000953) against the 1.10.1 x86-64
    // bodies, over the genuine GameplayIslandManager, GameplaySection, LevelManager and
    // UIManager. Systems are registered into absent rows before any component caches a typed
    // reference; rows that constructors create are leased. The rejecting filter branches run
    // on FilteredEventCollisionTriggerProbe. Not covered: OnCollisionEnter/Stay/Exit, which need
    // a physics Collision, and Awake's respawn subscription, whose CharacterManager would need
    // a live SaveManager.
    public sealed class OriginalWorldTriggerSystemTests
    {
        private static Collider NewCollider(Owned owned, string name) => owned.Host(name, false).AddComponent<BoxCollider>();

        private static UnityEvent<Collider> Recorded(object trigger, string field, List<string> log, string label)
        {
            var unityEvent = new UnityEvent<Collider>();
            unityEvent.AddListener(collider => log.Add(label + ":" + collider.name));
            Set(trigger, field, unityEvent);
            return unityEvent;
        }

        private static FilteredEventCollisionTriggerProbe Probe(Owned owned, List<string> log)
        {
            owned.LeaseRow<GameplayIslandManager>();
            var trigger = owned.Host("Lucid event collision trigger", false).AddComponent<FilteredEventCollisionTriggerProbe>();
            Recorded(trigger, "m_onEnter", log, "enter");
            Recorded(trigger, "m_onStay", log, "stay");
            Recorded(trigger, "m_onExit", log, "exit");
            return trigger;
        }

        private static HashSet<Collider> Active(EventCollisionTrigger trigger) => Get<HashSet<Collider>>(trigger, "m_collisionsActive");

        // An unawakened LevelManager whose constructor rows are leased.
        private static LevelManager Level(Owned owned, bool register)
        {
            owned.LeaseRow<MissionManager>();
            owned.LeaseRow<AudioManager>();
            owned.LeaseRow<SaveManager>();
            var levelManager = owned.Host("Lucid level manager", false).AddComponent<LevelManager>();
            typeof(LevelManager).GetProperty("CurrentLevelValid").SetValue(levelManager, true);
            if (register) owned.Register(levelManager);
            else owned.LeaseRow<LevelManager>();
            return levelManager;
        }

        private static GameplayIslandManager Islands(Owned owned)
        {
            owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
            owned.LeaseRow<LevelManager>();
            owned.LeaseRow<CharacterManager>();
            return owned.Register(new GameplayIslandManager());
        }

        private static Delegate[] LevelActivated(LevelManager levelManager) =>
            Get<Action<LevelManagerLevel>>(levelManager, "m_actionOnLevelActivated")?.GetInvocationList() ?? new Delegate[0];

        // TypeDef 0x02000764 metadata order: the four events are protected, the lambdas
        // <Awake>b__8_0..2 follow the constructor. The constructor 0x5CEC60 caches
        // GetSystemRef<GameplayIslandManager>(null, true) and allocates the empty set.
        [Test]
        public void EventCollisionTriggerKeepsItsDeclarationsAndCachesTheIslandReference()
        {
            AssertMembers(typeof(EventCollisionTrigger), 0x00100001,
                new[] { ("m_onEnter", 0x0004), ("m_onStay", 0x0004), ("m_onExit", 0x0004), ("m_onReset", 0x0004),
                        ("m_enableMultipleCollisionTrigger", 0x0001), ("m_gameplayIslandManagerRef", 0x0021), ("m_collisionsActive", 0x0021) },
                new[] { ("OnEnable", 0x01c4), ("Awake", 0x01c4), ("OnDestroy", 0x0081), ("OnDisable", 0x0081), ("OnTriggerEnter", 0x01c4),
                        ("OnTriggerStay", 0x01c4), ("OnTriggerExit", 0x01c4), ("OnCollisionEnter", 0x0081), ("OnCollisionStay", 0x0081),
                        ("OnCollisionExit", 0x0081), ("FilterCollision", 0x01c4), ("TryTrigger", 0x0081), ("OnLevelActivated", 0x0081),
                        ("ResetGameplayElement", 0x01e6), ("GetPosition", 0x01e6), ("IsDestroyed", 0x01e6), ("TryTriggerStay", 0x0084),
                        ("SetActiveCollision", 0x0081), (".ctor", 0x1886), ("<Awake>b__8_0", 0x0081), ("<Awake>b__8_1", 0x0081),
                        ("<Awake>b__8_2", 0x0081) });
            AssertIl2CppOptions(typeof(EventCollisionTrigger), 1, 2);
            Assert.That(typeof(EventCollisionTrigger).GetInterfaces(), Is.EqualTo(new[] { typeof(IResetCapableGameplayElement) }));

            Owned.Run(owned =>
            {
                owned.LeaseRow<GameplayIslandManager>();
                var trigger = owned.Host("Lucid event collision trigger", false).AddComponent<EventCollisionTrigger>();
                Assert.That(Get<SystemRef<GameplayIslandManager>>(trigger, "m_gameplayIslandManagerRef"),
                    Is.SameAs(ProcessManager.GetSystemRef<GameplayIslandManager>()));
                Assert.That(Active(trigger), Is.Empty);
                Assert.That((bool)Call(trigger, "FilterCollision", NewCollider(owned, "any")), Is.True, "0x5CF624 mov al, 1");
            });
        }

        // TryTrigger 0x06002960 (0x5CF4A0): FilterCollision (vtable slot 12), then the inlined
        // SetActiveCollision adds a passing collider and invokes the event, or removes a
        // failing one without invoking. OnTriggerExit 0x5CE6A0 uses the same body with
        // m_onExit, so a passing exit marks the collider active.
        [Test]
        public void EnterAndExitInvokeOnlyForAPassingColliderAndMarkItActive()
        {
            Owned.Run(owned =>
            {
                var log = new List<string>();
                FilteredEventCollisionTriggerProbe trigger = Probe(owned, log);
                Collider other = NewCollider(owned, "other");
                bool passes = true;
                trigger.Filter = collider => passes;

                Call(trigger, "OnTriggerEnter", other);
                Assert.That(log, Is.EqualTo(new[] { "enter:other" }));
                Assert.That(Active(trigger), Is.EquivalentTo(new[] { other }));
                passes = false;
                Call(trigger, "OnTriggerEnter", other);
                Assert.That(log, Has.Count.EqualTo(1), "a rejected enter is not raised");
                Assert.That(Active(trigger), Is.Empty);

                passes = true;
                Call(trigger, "OnTriggerExit", other);
                Assert.That(log, Is.EqualTo(new[] { "enter:other", "exit:other" }));
                Assert.That(Active(trigger), Is.EquivalentTo(new[] { other }), "a passing exit adds the collider");
                passes = false;
                Call(trigger, "OnTriggerExit", other);
                Assert.That(log, Has.Count.EqualTo(2));
                Assert.That(Active(trigger), Is.Empty);
            });
        }

        // TryTriggerStay 0x06002965 (0x5CEA00): Contains before the filter; on pass, enter only
        // when multiple triggering is on and the collider was inactive (0x5ceaac..0x5ceab7),
        // then stay; on failure, exit only when multiple triggering is on and it was active
        // (0x5ceb0f..0x5ceb1e). OnTriggerStay 0x5CE660 jumps straight here.
        [TestCase(true, false, true, "enter:other,stay:other", true)]
        [TestCase(true, true, true, "stay:other", true)]
        [TestCase(false, false, true, "stay:other", true)]
        [TestCase(true, true, false, "exit:other", false)]
        [TestCase(true, false, false, "", false)]
        [TestCase(false, true, false, "", false)]
        public void StayRaisesEnterOrExitOnlyForAChangeWithMultipleTriggering(bool multiple, bool wasActive, bool passes, string events, bool active)
        {
            Owned.Run(owned =>
            {
                var log = new List<string>();
                FilteredEventCollisionTriggerProbe trigger = Probe(owned, log);
                Collider other = NewCollider(owned, "other");
                Set(trigger, "m_enableMultipleCollisionTrigger", multiple);
                if (wasActive) Active(trigger).Add(other);
                trigger.Filter = collider => passes;
                Call(trigger, "OnTriggerStay", other);
                Assert.That(string.Join(",", log), Is.EqualTo(events));
                Assert.That(Active(trigger).Contains(other), Is.EqualTo(active));
            });
        }

        // The set changes before the event runs: TryTrigger Add 0x5cf525 then Invoke 0x5cf544;
        // TryTriggerStay Add 0x5ceaa7 before the enter and stay Invokes (0x5ceaca, 0x5ceb3e),
        // and Remove 0x5ceb0a before the exit Invoke. Each listener records membership.
        [Test]
        public void TriggersUpdateTheActiveSetBeforeRaisingTheirEvent()
        {
            Owned.Run(owned =>
            {
                var log = new List<string>();
                FilteredEventCollisionTriggerProbe trigger = Probe(owned, log);
                foreach ((string field, string label) in new[] { ("m_onEnter", "enter"), ("m_onStay", "stay"), ("m_onExit", "exit") })
                {
                    var unityEvent = new UnityEvent<Collider>();
                    unityEvent.AddListener(collider => log.Add(label + ":" + Active(trigger).Contains(collider)));
                    Set(trigger, field, unityEvent);
                }
                Collider other = NewCollider(owned, "other");
                Set(trigger, "m_enableMultipleCollisionTrigger", true);
                bool passes = true;
                trigger.Filter = collider => passes;

                Call(trigger, "OnTriggerEnter", other);
                Call(trigger, "OnTriggerStay", other);
                Call(trigger, "OnTriggerExit", other);
                Assert.That(log, Is.EqualTo(new[] { "enter:True", "stay:True", "exit:True" }));

                log.Clear();
                Active(trigger).Remove(other);
                Call(trigger, "OnTriggerStay", other);
                Assert.That(log, Is.EqualTo(new[] { "enter:True", "stay:True" }), "a stay that enters");

                log.Clear();
                passes = false;
                Call(trigger, "OnTriggerStay", other);
                Assert.That(log, Is.EqualTo(new[] { "exit:False" }), "a stay that exits");
            });
        }

        // TryTriggerStay reads Contains (call 0x5cea49) before the FilterCollision vcall at
        // 0x5cea61, so a filter that marks the collider itself still sees the enter raised.
        [Test]
        public void StayReadsTheActiveSetBeforeTheFilterRuns()
        {
            Owned.Run(owned =>
            {
                var log = new List<string>();
                FilteredEventCollisionTriggerProbe trigger = Probe(owned, log);
                Collider other = NewCollider(owned, "other");
                Set(trigger, "m_enableMultipleCollisionTrigger", true);
                trigger.Filter = collider =>
                {
                    Active(trigger).Add(collider);
                    return true;
                };
                Call(trigger, "OnTriggerStay", other);
                Assert.That(string.Join(",", log), Is.EqualTo("enter:other,stay:other"));
            });
        }

        // OnEnable 0x06002955 clears the set; OnDisable 0x06002958 raises exit for every live
        // active collider (op_Equality null skip at 0x5cf3dd), then clears.
        [Test]
        public void DisableExitsEachLiveActiveColliderAndEnableForgetsThem()
        {
            Owned.Run(owned =>
            {
                var log = new List<string>();
                FilteredEventCollisionTriggerProbe trigger = Probe(owned, log);
                Collider live = NewCollider(owned, "live");
                Collider destroyed = NewCollider(owned, "destroyed");
                Active(trigger).Add(live);
                Active(trigger).Add(destroyed);
                Object.DestroyImmediate(destroyed.gameObject);
                Call(trigger, "OnDisable");
                Assert.That(log, Is.EqualTo(new[] { "exit:live" }));
                Assert.That(Active(trigger), Is.Empty);

                Active(trigger).Add(live);
                Call(trigger, "OnEnable");
                Assert.That(Active(trigger), Is.Empty);
                Assert.That(log, Has.Count.EqualTo(1), "enable raises nothing");
            });
        }

        // ResetGameplayElement 0x06002962 and OnLevelActivated 0x06002961 both invoke m_onReset;
        // GetPosition 0x06002963 is transform.position; IsDestroyed 0x06002964 is this == null.
        [Test]
        public void ResetHooksInvokeTheResetEventAndReportTheTriggerState()
        {
            Owned.Run(owned =>
            {
                FilteredEventCollisionTriggerProbe trigger = Probe(owned, new List<string>());
                int resets = 0;
                var reset = new UnityEvent();
                reset.AddListener(() => ++resets);
                Set(trigger, "m_onReset", reset);
                trigger.ResetGameplayElement();
                Call(trigger, "OnLevelActivated", (LevelManagerLevel)null);
                Assert.That(resets, Is.EqualTo(2));
                trigger.transform.position = new Vector3(1f, 2f, 3f);
                Assert.That(trigger.GetPosition(), Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(trigger.IsDestroyed(), Is.False);
                Object.DestroyImmediate(trigger);
                Assert.That(trigger.IsDestroyed(), Is.True);
            });
        }

        // Awake 0x06002956 (0x5CEF30): InvokeOnValid(<Awake>b__8_0) registers with the island
        // manager (pending here, no island holds the position). The level and respawn wiring
        // runs only when m_onReset has persistent listeners (jle 0x5cf030), so with none the
        // LevelManager and CharacterManager references gain no deferred work.
        [Test]
        public void AwakeRegistersAsResetCapableAndWiresNothingWithoutResetListeners()
        {
            Owned.Run(owned =>
            {
                GameplayIslandManager islands = Islands(owned);
                var trigger = owned.Host("Lucid event collision trigger", false).AddComponent<EventCollisionTrigger>();
                Set(trigger, "m_onReset", new UnityEvent());
                Call(trigger, "Awake");
                Assert.That(Get<List<IResetCapableGameplayElement>>(islands, "m_pendingResetCapableGameplayElements"), Is.EqualTo(new[] { trigger }));
                Assert.That(Get<object>(ProcessManager.GetSystemRef<LevelManager>(), "m_actionOnSystemValid"), Is.Null);
                Assert.That(Get<object>(ProcessManager.GetSystemRef<CharacterManager>(), "m_actionOnSystemValid"), Is.Null);
            });
        }

        // With a persistent m_onReset listener (GetPersistentEventCount 0x5cf029 > 0), Awake
        // passes <Awake>b__8_1 to GetSystemRef<LevelManager>().InvokeOnValid (0x5cf0a0) and
        // <Awake>b__8_2 to GetSystemRef<CharacterManager>().InvokeOnValid (0x5cf101). Both rows
        // are absent, so each reference defers exactly that callback; the fixture clears the
        // deferred work it caused before the rows are released. m_onEnter has no persistent
        // listener, so only the m_onReset count can open the branch.
        [Test]
        public void AwakeDefersTheLevelAndRespawnWiringWhenResetHasPersistentListeners()
        {
            Owned.Run(owned =>
            {
                GameplayIslandManager islands = Islands(owned);
                var trigger = owned.Host("Lucid event collision trigger", false).AddComponent<EventCollisionTrigger>();
                var reset = new UnityEvent();
                var target = owned.Host("Lucid reset target", false).AddComponent<CycleGameObjectList>();
                UnityEventTools.AddPersistentListener(reset, target.Action_Next);
                Set(trigger, "m_onReset", reset);
                Set(trigger, "m_onEnter", new UnityEvent<Collider>());
                SystemRef<LevelManager> levelRef = ProcessManager.GetSystemRef<LevelManager>();
                SystemRef<CharacterManager> characterRef = ProcessManager.GetSystemRef<CharacterManager>();
                try
                {
                    Call(trigger, "Awake");
                    Assert.That(Get<List<IResetCapableGameplayElement>>(islands, "m_pendingResetCapableGameplayElements"), Is.EqualTo(new[] { trigger }));
                    List<Action<LevelManager>> level = Get<FastAction<LevelManager>>(levelRef, "m_actionOnSystemValid").GetInvocationList();
                    Assert.That(level.Select(action => (action.Target, action.Method.Name)), Is.EqualTo(new[] { ((object)trigger, "<Awake>b__8_1") }));
                    List<Action<CharacterManager>> character = Get<FastAction<CharacterManager>>(characterRef, "m_actionOnSystemValid").GetInvocationList();
                    Assert.That(character.Select(action => (action.Target, action.Method.Name)), Is.EqualTo(new[] { ((object)trigger, "<Awake>b__8_2") }));
                }
                finally
                {
                    Set(levelRef, "m_actionOnSystemValid", null);
                    Set(characterRef, "m_actionOnSystemValid", null);
                }
            });
        }

        // <Awake>b__8_1 0x06002969 (0x5CF760) subscribes OnLevelActivated with
        // triggerImmediately = false (edx 0 at 0x5cf7c7), so a valid current level does not
        // reset at once. OnDestroy 0x06002957 (0x5CF120) removes that subscription through
        // GetSystemRef<LevelManager>().TryGet only when the persistent count is non-zero.
        [Test]
        public void LevelSubscriptionWaitsForTheNextActivationAndOnDestroyRemovesItWithResetListeners()
        {
            Owned.Run(owned =>
            {
                LevelManager levelManager = Level(owned, true);
                owned.LeaseRow<GameplayIslandManager>();
                owned.LeaseRow<CharacterManager>();
                var trigger = owned.Host("Lucid event collision trigger", false).AddComponent<EventCollisionTrigger>();
                int resets = 0;
                var reset = new UnityEvent();
                reset.AddListener(() => ++resets);
                Set(trigger, "m_onReset", reset);

                Call(trigger, "<Awake>b__8_1", levelManager);
                Assert.That(resets, Is.Zero, "not triggered immediately");
                Delegate subscription = LevelActivated(levelManager).Single();
                Assert.That(subscription.Target, Is.SameAs(trigger));
                subscription.DynamicInvoke(new object[] { null });
                Assert.That(resets, Is.EqualTo(1));

                Call(trigger, "OnDestroy");
                Assert.That(LevelActivated(levelManager), Has.Length.EqualTo(1), "no persistent listener: nothing is removed");
                var target = owned.Host("Lucid reset target", false).AddComponent<CycleGameObjectList>();
                UnityEventTools.AddPersistentListener(reset, target.Action_Next);
                Call(trigger, "OnDestroy");
                Assert.That(LevelActivated(levelManager), Is.Empty);
            });
        }

        // TypeDef 0x020006b4. The constructor 0x58CC10 caches the island reference; OnDisable
        // 0x060023ff (0x58C5F0) tests the list for null before enumerating.
        [Test]
        public void AutoRegisterGroupCachesTheIslandReferenceAndToleratesNoSections()
        {
            AssertMembers(typeof(AutoRegisterHashedComponentGroup), 0x00100001,
                new[] { ("m_gameplayIslandManagerRef", 0x0021), ("m_registeredGameplaySections", 0x0001) },
                new[] { ("OnEnable", 0x0081), ("OnDisable", 0x0081), ("OnGameplayIslandValid", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(AutoRegisterHashedComponentGroup), 2, 1);
            Assert.That(typeof(AutoRegisterHashedComponentGroup).BaseType, Is.EqualTo(typeof(HashedComponentGroup)));

            Owned.Run(owned =>
            {
                owned.LeaseRow<GameplayIslandManager>();
                var group = owned.Host("Lucid auto group", false).AddComponent<AutoRegisterHashedComponentGroup>();
                Assert.That(Get<SystemRef<GameplayIslandManager>>(group, "m_gameplayIslandManagerRef"),
                    Is.SameAs(ProcessManager.GetSystemRef<GameplayIslandManager>()));
                Assert.DoesNotThrow(() => Call(group, "OnDisable"));
            });
        }

        // OnEnable 0x060023fe InvokeOnValid(OnGameplayIslandValid); OnGameplayIslandValid
        // 0x06002400 (0x58C730) stores TryGetAllGameplaySections(transform.position) into the
        // field and registers with each section; OnDisable unregisters from that kept list.
        [Test]
        public void AutoRegisterGroupJoinsEverySectionContainingItAndLeavesThemOnDisable()
        {
            Owned.Run(owned =>
            {
                GameplayIslandManager islands = Islands(owned);
                owned.LeaseRow<TrackManager>();
                var island = owned.Track(ScriptableObject.CreateInstance<GameplayIslandDefinition>());
                GameplaySection Section(string name, Vector3 centre)
                {
                    GameObject volumeHost = owned.Host(name + " volume", false);
                    // AddComponent sends CharacterTrigger.Reset, which stores this collider; GetBounds returns
                    // an enabled collider's bounds (empty at the origin on an inactive host) and the m_bounds
                    // snapshot otherwise, so the collider is disabled for the snapshot to place the section.
                    volumeHost.AddComponent<BoxCollider>().enabled = false;
                    var volume = volumeHost.AddComponent<CharacterEventTrigger>();
                    Set(volume, "m_bounds", new Bounds(centre, Vector3.one * 2f));
                    var section = owned.Host(name, false).AddComponent<GameplaySection>();
                    Set(section, "m_definition", island);
                    Set(section, "m_islandVolumes", new[] { volume });
                    islands.RegisterGameplaySection(section);
                    return section;
                }
                GameplaySection first = Section("first", Vector3.zero);
                GameplaySection outside = Section("outside", new Vector3(10f, 0f, 0f));
                GameplaySection second = Section("second", new Vector3(0.5f, 0f, 0f));
                List<HashedComponentGroup> Groups(GameplaySection section) => Get<List<HashedComponentGroup>>(section, "m_componentGroups");

                var group = owned.Host("Lucid auto group", false).AddComponent<AutoRegisterHashedComponentGroup>();
                Call(group, "OnEnable");
                var registered = Get<List<GameplaySection>>(group, "m_registeredGameplaySections");
                Assert.That(registered, Is.EqualTo(new[] { first, second }));
                Assert.That(Groups(first), Is.EqualTo(new[] { group }));
                Assert.That(Groups(second), Is.EqualTo(new[] { group }));
                Assert.That(Groups(outside), Is.Empty);

                Call(group, "OnDisable");
                Assert.That(Groups(first).Concat(Groups(second)), Is.Empty);
                Assert.That(Get<List<GameplaySection>>(group, "m_registeredGameplaySections"), Is.SameAs(registered), "the list is kept");

                group.transform.position = new Vector3(10f, 0f, 0f);
                Call(group, "OnEnable");
                Assert.That(Get<List<GameplaySection>>(group, "m_registeredGameplaySections"), Is.EqualTo(new[] { outside }).And.Not.SameAs(registered));
                Assert.That(Groups(outside), Is.EqualTo(new[] { group }));
            });
        }

        // TypeDef 0x02000953 (sealed). Start 0x0600359c caches GetSystemRef<UIManager>(null, true)
        // and InvokeOnValid(ListenForContainerOpen), which combines OnContainerOpen into the
        // public OnContainerCreated field (0x684950). OnContainerOpen 0x0600359f (0x684A30) sets
        // each object active exactly when the container's identifier is not listed; OnDisable
        // 0x0600359d removes the handler through Get().
        [Test]
        public void ContainerWatcherHidesItsObjectsOnlyForAListedContainer()
        {
            AssertMembers(typeof(DeactivateWhileContainersAreOpen), 0x00100101,
                new[] { ("m_objectsToDeactivate", 0x0001), ("m_containers", 0x0001), ("m_uiManagerRef", 0x0001) },
                new[] { ("Start", 0x0081), ("OnDisable", 0x0081), ("ListenForContainerOpen", 0x0081), ("OnContainerOpen", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(DeactivateWhileContainersAreOpen), 2, 1);

            Owned.Run(owned =>
            {
                var uiManager = owned.Register(owned.Host("Lucid UI manager", false).AddComponent<UIManager>());
                var listed = owned.Track(ScriptableObject.CreateInstance<UIContainerIdentifier>());
                var unlisted = owned.Track(ScriptableObject.CreateInstance<UIContainerIdentifier>());
                // List.Contains uses ScriptableObjectWithGuid.Equals (HLUnityCore 0x06000e5f), which compares m_guid;
                // two bare CreateInstance identifiers share the empty guid and would both count as listed.
                Set(listed, "m_guid", "lucid-container-watcher-listed");
                Set(unlisted, "m_guid", "lucid-container-watcher-unlisted");
                UIContainer Container(UIContainerIdentifier identifier)
                {
                    var container = owned.Host("Lucid container", false).AddComponent<UIContainerSimple>();
                    Set(container, "m_identifier", identifier);
                    return container;
                }
                GameObject[] objects = { owned.Host("one"), owned.Host("two") };
                var watcher = owned.Host("Lucid container watcher", false).AddComponent<DeactivateWhileContainersAreOpen>();
                Set(watcher, "m_objectsToDeactivate", objects.ToList());
                Set(watcher, "m_containers", new List<UIContainerIdentifier> { listed });

                Call(watcher, "Start");
                Assert.That(Get<SystemRef<UIManager>>(watcher, "m_uiManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<UIManager>()));
                Delegate handler = uiManager.OnContainerCreated.GetInvocationList().Single();
                Assert.That(handler.Target, Is.SameAs(watcher));
                Assert.That(handler.Method.Name, Is.EqualTo("OnContainerOpen"));

                uiManager.OnContainerCreated(Container(listed));
                Assert.That(objects.Select(gameObject => gameObject.activeSelf), Is.EqualTo(new[] { false, false }));
                uiManager.OnContainerCreated(Container(unlisted));
                Assert.That(objects.Select(gameObject => gameObject.activeSelf), Is.EqualTo(new[] { true, true }), "an unlisted container reactivates them");

                Call(watcher, "OnDisable");
                Assert.That(uiManager.OnContainerCreated, Is.Null);
            });
        }
    }
}
