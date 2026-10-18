using System;
using System.Collections.Generic;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime EntityActivationEvent (0x02000a52), EntityActivationLevelVisibilityEntity
    // (0x02000a54), EntityActivationMissionVisibilityEntity (0x02000a65) and EntityActivator
    // (0x02000a66) against the 1.10.1 x86-64 bodies, over the genuine EntityActivationManager,
    // definitions and probe logics. Managers are registered into absent rows before any
    // component caches its typed reference; LevelManager is an unawakened component whose
    // constructor rows are leased. Edit Mode sends no Unity messages, so hooks are called directly.
    public sealed class OriginalEntityActivationEntityTests
    {
        private static EntityActivationDefinition Definition(Owned owned, bool activate, bool deactivate, out List<string> trace)
        {
            var definition = owned.Track(ScriptableObject.CreateInstance<EntityActivationDefinition>());
            var activateLogic = owned.Track(ScriptableObject.CreateInstance<ProjectLucidEntityActivationProbeDefinition>());
            var deactivateLogic = owned.Track(ScriptableObject.CreateInstance<ProjectLucidEntityActivationProbeDefinition>());
            activateLogic.Label = "activate";
            deactivateLogic.Label = "deactivate";
            trace = activateLogic.Trace;
            deactivateLogic.Trace = trace;
            Set(definition, "m_type", EntityActivationType.Collectable);
            Set(definition, "m_activateLogicDefinitions", activate ? new List<EntityActivationLogicDefinition> { activateLogic } : new List<EntityActivationLogicDefinition>());
            Set(definition, "m_deactivateLogicDefinitions", deactivate ? new List<EntityActivationLogicDefinition> { deactivateLogic } : new List<EntityActivationLogicDefinition>());
            return definition;
        }

        private static EntityActivationManager Manager(Owned owned)
        {
            owned.LeaseActions(SystemAction.Update);
            return owned.Register(new EntityActivationManager());
        }

        // An unawakened LevelManager; its three constructor rows are leased first.
        private static LevelManager Level(Owned owned, bool register, bool currentLevelValid)
        {
            owned.LeaseRow<MissionManager>();
            owned.LeaseRow<AudioManager>();
            owned.LeaseRow<SaveManager>();
            var levelManager = owned.Host("Lucid level manager", false).AddComponent<LevelManager>();
            typeof(LevelManager).GetProperty("CurrentLevelValid").SetValue(levelManager, currentLevelValid);
            if (register) owned.Register(levelManager);
            else owned.LeaseRow<LevelManager>();
            return levelManager;
        }

        private static List<IEntityActivatable> Entities(EntityActivationManager manager, string field) =>
            Get<Dictionary<EntityActivationType, List<EntityActivationInstance>>>(manager, field)
                .TryGetValue(EntityActivationType.Collectable, out List<EntityActivationInstance> instances)
                ? instances.ConvertAll(instance => instance.Entity) : new List<IEntityActivatable>();

        private static EntityActivationInstance Instance(EntityActivationManager manager, string field, IEntityActivatable entity) =>
            Get<Dictionary<EntityActivationType, List<EntityActivationInstance>>>(manager, field)[EntityActivationType.Collectable]
                .Single(instance => instance.Entity == entity);

        private static Delegate[] LevelActivated(LevelManager levelManager) =>
            Get<Action<LevelManagerLevel>>(levelManager, "m_actionOnLevelActivated")?.GetInvocationList() ?? new Delegate[0];

        private static UnityEvent Counted(object target, string field, List<string> log, string label)
        {
            var unityEvent = new UnityEvent();
            unityEvent.AddListener(() => log.Add(label));
            Set(target, field, unityEvent);
            return unityEvent;
        }

        // TypeDef 0x02000a52 is sealed (0x00100101). The constructor 0x6BFAF0 allocates both
        // events. OnActivate/OnDeactivate (0x6BFAD0/0x6BFAE0) only invoke their event: neither
        // calls the base override, so the inherited m_registeredAsActive stays false.
        [Test]
        public void EntityActivationEventOverridesInvokeOnlyTheirOwnEvent()
        {
            AssertMembers(typeof(EntityActivationEvent), 0x00100101,
                new[] { ("m_onActivate", 0x0001), ("m_onDeactivate", 0x0001) },
                new[] { ("OnActivate", 0x00c4), ("OnDeactivate", 0x00c4), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(EntityActivationEvent), 1, 2);

            Owned.Run(owned =>
            {
                var entity = owned.Host("Lucid activation event", false).AddComponent<EntityActivationEvent>();
                Assert.That(Get<UnityEvent>(entity, "m_onActivate"), Is.Not.Null.And.Not.SameAs(Get<UnityEvent>(entity, "m_onDeactivate")));
                var log = new List<string>();
                Counted(entity, "m_onActivate", log, "activate");
                Counted(entity, "m_onDeactivate", log, "deactivate");

                Call(entity, "OnActivate");
                Assert.That(log, Is.EqualTo(new[] { "activate" }));
                Assert.That(Get<bool>(entity, "m_registeredAsActive"), Is.False, "the base flag is not set");
                Call(entity, "OnDeactivate");
                Assert.That(log, Is.EqualTo(new[] { "activate", "deactivate" }));
            });
        }

        // Start 0x06003bc5 caches GetSystemRef<EntityActivationManager>(null, true) then
        // InvokeOnValid(Initialise). Initialise 0x06003bc6 (0x6C4170) registers through the
        // reference's Get() with activeInHierarchy and both callbacks (the inlined Register:
        // inactive row with !active first, then the active row). OnActivateEvent and
        // OnDeactivateEvent call Activate/Deactivate; OnDestroy 0x6C4290 deregisters through
        // ?.GetSafe()?, so a never-started activator is ignored.
        [Test]
        public void EntityActivatorRegistersWithItsHierarchyStateAndForwardsEvents()
        {
            AssertMembers(typeof(EntityActivator), 0x00100001,
                new[] { ("m_definition", 0x0001), ("m_onActivation", 0x0001), ("m_onDeactivation", 0x0001), ("m_entityActivationManagerRef", 0x0001) },
                new[] { ("get_Definition", 0x09e6), ("get_Transform", 0x09e6), ("Start", 0x0081), ("Initialise", 0x0081), ("OnDestroy", 0x0081),
                        ("OnActivateEvent", 0x0086), ("OnDeactivateEvent", 0x0086), ("OnActivateCallback", 0x0081), ("OnDeactivateCallback", 0x0081),
                        (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(EntityActivator), 2, 1);

            Owned.Run(owned =>
            {
                EntityActivationManager manager = Manager(owned);
                EntityActivationDefinition definition = Definition(owned, true, true, out List<string> trace);
                GameObject host = owned.Host("Lucid activator");
                var activator = host.AddComponent<EntityActivator>();
                Set(activator, "m_definition", definition);
                var log = new List<string>();
                Counted(activator, "m_onActivation", log, "activation");
                Counted(activator, "m_onDeactivation", log, "deactivation");
                Assert.That(activator.Definition, Is.SameAs(definition));
                Assert.That(activator.Transform, Is.SameAs(host.transform));
                Assert.DoesNotThrow(() => Call(activator, "OnDestroy"), "no reference before Start");

                Call(activator, "Start");
                Assert.That(Get<SystemRef<EntityActivationManager>>(activator, "m_entityActivationManagerRef"),
                    Is.SameAs(ProcessManager.GetSystemRef<EntityActivationManager>()));
                Assert.That(Entities(manager, "m_active"), Is.EqualTo(new[] { activator }));
                Assert.That(log, Is.EqualTo(new[] { "activation" }));
                Assert.That(trace, Is.EqualTo(new[] { "start deactivate" }), "first-time activation");

                activator.OnDeactivateEvent();
                Assert.That(Entities(manager, "m_inactive"), Is.EqualTo(new[] { activator }));
                Assert.That(log, Is.EqualTo(new[] { "activation", "deactivation" }));
                activator.OnActivateEvent();
                Assert.That(Entities(manager, "m_active"), Is.EqualTo(new[] { activator }));

                trace.Clear();
                Call(activator, "OnDestroy");
                Assert.That(Entities(manager, "m_active").Concat(Entities(manager, "m_inactive")), Is.Empty);
                Assert.That(trace, Is.EqualTo(new[] { "shutdown activate", "shutdown deactivate" }));

                var inactive = owned.Host("Lucid inactive activator", false).AddComponent<EntityActivator>();
                Set(inactive, "m_definition", definition);
                log.Clear();
                Counted(inactive, "m_onActivation", log, "activation");
                Counted(inactive, "m_onDeactivation", log, "deactivation");
                Call(inactive, "Start");
                Assert.That(Entities(manager, "m_inactive"), Is.EqualTo(new[] { inactive }));
                Assert.That(log, Is.EqualTo(new[] { "deactivation" }));
            });
        }

        // TypeDef 0x02000a54 metadata order, and the constructor 0x6C0D90 storing
        // GetSystemRef<LevelManager>(null, true) then GetSystemRef<EntityActivationManager>,
        // the rows' cached typed references. SetDefinition 0x06003b67 stores the field the
        // Definition getter 0x06003b5d returns.
        [Test]
        public void LevelVisibilityEntityCachesBothRowReferences()
        {
            AssertMembers(typeof(EntityActivationLevelVisibilityEntity), 0x00100001,
                new[] { ("m_entityActivationDefinition", 0x0001), ("m_onEnabled", 0x0001), ("m_onDisabled", 0x0001), ("m_enabledOnAwake", 0x0001),
                        ("m_hasRegistered", 0x0001), ("m_deactivationInProgress", 0x0001), ("m_levelManagerRef", 0x0021),
                        ("m_entityActivationManagerRef", 0x0024) },
                new[] { ("get_Definition", 0x09e6), ("get_Transform", 0x09e6), ("Awake", 0x01c4), ("OnDisable", 0x0081), ("OnDestroy", 0x01c4),
                        ("OnLevelManagerValid", 0x0081), ("OnLevelActivated", 0x0081), ("OnEntityManagerValid", 0x0081), ("OnEntityEnable", 0x0081),
                        ("OnEntityDisable", 0x0081), ("SetDefinition", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(EntityActivationLevelVisibilityEntity), 1, 2);
            Assert.That(AttributeTypes(Field(typeof(EntityActivationLevelVisibilityEntity), "m_onEnabled")),
                Is.EqualTo(new[] { typeof(FormerlySerializedAsAttribute), typeof(TooltipAttribute), typeof(SerializeField) }));

            Owned.Run(owned =>
            {
                owned.LeaseRow<LevelManager>();
                owned.LeaseRow<EntityActivationManager>();
                GameObject host = owned.Host("Lucid level visibility", false);
                var entity = host.AddComponent<EntityActivationLevelVisibilityEntity>();
                Assert.That(Get<SystemRef<LevelManager>>(entity, "m_levelManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<LevelManager>()));
                Assert.That(Get<SystemRef<EntityActivationManager>>(entity, "m_entityActivationManagerRef"),
                    Is.SameAs(ProcessManager.GetSystemRef<EntityActivationManager>()));
                var definition = owned.Track(ScriptableObject.CreateInstance<EntityActivationDefinition>());
                entity.SetDefinition(definition);
                Assert.That(entity.Definition, Is.SameAs(definition));
                Assert.That(entity.Transform, Is.SameAs(host.transform));
            });
        }

        // Awake 0x06003b5f stores activeInHierarchy, then InvokeOnValid(OnLevelManagerValid);
        // OnLevelManagerValid 0x6C08C0 passes triggerImmediately = true (edx 1 at 0x6c0927), so
        // a valid current level runs OnLevelActivated at once. OnLevelActivated 0x6C0940 then
        // InvokeOnValid(OnEntityManagerValid). Without a current level only the subscription
        // remains until the level manager raises it.
        [Test]
        public void AwakeSubscribesToLevelActivationWhichRegistersTheEntity()
        {
            Owned.Run(owned =>
            {
                EntityActivationManager manager = Manager(owned);
                LevelManager levelManager = Level(owned, true, false);
                EntityActivationDefinition definition = Definition(owned, true, true, out _);
                var waiting = owned.Host("Lucid waiting visibility").AddComponent<EntityActivationLevelVisibilityEntity>();
                waiting.SetDefinition(definition);

                Call(waiting, "Awake");
                Assert.That(Get<bool>(waiting, "m_enabledOnAwake"), Is.True);
                Delegate subscription = LevelActivated(levelManager).Single();
                Assert.That(subscription.Target, Is.SameAs(waiting));
                Assert.That(subscription.Method.Name, Is.EqualTo("OnLevelActivated"));
                Assert.That(Get<bool>(waiting, "m_hasRegistered"), Is.False);
                Assert.That(Entities(manager, "m_active"), Is.Empty);

                subscription.DynamicInvoke(new object[] { null });
                Assert.That(Entities(manager, "m_active"), Is.EqualTo(new[] { waiting }));
                Assert.That(Get<bool>(waiting, "m_hasRegistered"), Is.True);

                typeof(LevelManager).GetProperty("CurrentLevelValid").SetValue(levelManager, true);
                var immediate = owned.Host("Lucid immediate visibility").AddComponent<EntityActivationLevelVisibilityEntity>();
                immediate.SetDefinition(definition);
                Call(immediate, "Awake");
                Assert.That(Entities(manager, "m_active"), Is.EqualTo(new IEntityActivatable[] { waiting, immediate }));
            });
        }

        // OnEntityManagerValid 0x06003b64 (0x6C0A80): inlined Deregister, SetActive(m_enabledOnAwake)
        // when activeInHierarchy differs, an OnEntityEnable callback only when CanActivate and an
        // OnEntityDisable callback only when CanDeactivate, the inlined Register at
        // m_enabledOnAwake, then m_hasRegistered. A second call replaces the first entry.
        [Test]
        public void EntityManagerValidReregistersAtTheAwakeStateWithOnlyTheDefinedCallbacks()
        {
            Owned.Run(owned =>
            {
                EntityActivationManager manager = Manager(owned);
                owned.LeaseRow<LevelManager>();
                EntityActivationDefinition activateOnly = Definition(owned, true, false, out List<string> trace);
                GameObject host = owned.Host("Lucid visibility", false);
                var entity = host.AddComponent<EntityActivationLevelVisibilityEntity>();
                entity.SetDefinition(activateOnly);
                var log = new List<string>();
                Counted(entity, "m_onEnabled", log, "enabled");
                Counted(entity, "m_onDisabled", log, "disabled");
                Set(entity, "m_enabledOnAwake", true);

                Call(entity, "OnEntityManagerValid", manager);
                Assert.That(host.activeSelf, Is.True, "the awake visibility is restored");
                Assert.That(Entities(manager, "m_active"), Is.EqualTo(new[] { entity }));
                EntityActivationInstance instance = Instance(manager, "m_active", entity);
                var onActivate = Get<Action>(instance, "m_onActivateCallback");
                Assert.That(onActivate.Target, Is.SameAs(entity));
                Assert.That(onActivate.Method.Name, Is.EqualTo("OnEntityEnable"));
                Assert.That(Get<Action>(instance, "m_onDeactivateCallback"), Is.Null, "no deactivation logic");
                Assert.That(log, Is.EqualTo(new[] { "enabled" }));
                Assert.That(Get<bool>(entity, "m_hasRegistered"), Is.True);

                trace.Clear();
                Call(entity, "OnEntityManagerValid", manager);
                Assert.That(Entities(manager, "m_active"), Is.EqualTo(new[] { entity }), "deregistered before registering again");
                Assert.That(trace, Is.EqualTo(new[] { "shutdown activate" }));

                EntityActivationDefinition deactivateOnly = Definition(owned, false, true, out _);
                GameObject otherHost = owned.Host("Lucid hidden visibility");
                var hidden = otherHost.AddComponent<EntityActivationLevelVisibilityEntity>();
                hidden.SetDefinition(deactivateOnly);
                log.Clear();
                Counted(hidden, "m_onEnabled", log, "enabled");
                Counted(hidden, "m_onDisabled", log, "disabled");
                Call(hidden, "OnEntityManagerValid", manager);
                Assert.That(otherHost.activeSelf, Is.False);
                Assert.That(Entities(manager, "m_inactive"), Is.EqualTo(new[] { hidden }));
                Assert.That(Get<Action>(Instance(manager, "m_inactive", hidden), "m_onActivateCallback"), Is.Null);
                Assert.That(log, Is.EqualTo(new[] { "disabled" }));
                Assert.That(Get<bool>(hidden, "m_deactivationInProgress"), Is.False);
            });
        }

        // OnEntityEnable/OnEntityDisable 0x06003b65/0x06003b66: Unity-null tests of this and the
        // game object return early; otherwise SetActive, then the event through ?.Invoke.
        [Test]
        public void EntityEnableAndDisableSkipADestroyedEntityAndANullEvent()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<LevelManager>();
                owned.LeaseRow<EntityActivationManager>();
                GameObject host = owned.Host("Lucid visibility", false);
                var entity = host.AddComponent<EntityActivationLevelVisibilityEntity>();
                Set(entity, "m_onEnabled", null);
                Call(entity, "OnEntityEnable");
                Assert.That(host.activeSelf, Is.True);
                var log = new List<string>();
                Counted(entity, "m_onDisabled", log, "disabled");
                Call(entity, "OnEntityDisable");
                Assert.That(host.activeSelf, Is.False);
                Assert.That(log, Is.EqualTo(new[] { "disabled" }));
                Assert.That(Get<bool>(entity, "m_deactivationInProgress"), Is.False);

                Counted(entity, "m_onEnabled", log, "enabled");
                Object.DestroyImmediate(entity);
                Assert.DoesNotThrow(() => Call(entity, "OnEntityEnable"));
                Assert.That(host.activeSelf, Is.False);
                Assert.That(log, Is.EqualTo(new[] { "disabled" }));
            });
        }

        // OnDisable 0x06003b60 (0x6C0720): Deactivate through ?.GetSafe()? only when registered,
        // the object itself is inactive and no OnEntityDisable is in progress.
        [Test]
        public void OnDisableForwardsOnlyAnExternalDeactivationOfARegisteredEntity()
        {
            Owned.Run(owned =>
            {
                EntityActivationManager manager = Manager(owned);
                owned.LeaseRow<LevelManager>();
                GameObject host = owned.Host("Lucid visibility");
                var entity = host.AddComponent<EntityActivationLevelVisibilityEntity>();
                entity.SetDefinition(Definition(owned, true, true, out _));
                var log = new List<string>();
                Counted(entity, "m_onDisabled", log, "disabled");
                Set(entity, "m_enabledOnAwake", true);
                Call(entity, "OnEntityManagerValid", manager);

                Call(entity, "OnDisable");
                Assert.That(Entities(manager, "m_active"), Is.EqualTo(new[] { entity }), "the object is still active");
                host.SetActive(false);
                Set(entity, "m_hasRegistered", false);
                Call(entity, "OnDisable");
                Assert.That(Entities(manager, "m_active"), Is.EqualTo(new[] { entity }), "not registered");
                Set(entity, "m_hasRegistered", true);
                Set(entity, "m_deactivationInProgress", true);
                Call(entity, "OnDisable");
                Assert.That(Entities(manager, "m_active"), Is.EqualTo(new[] { entity }), "its own deactivation");
                Assert.That(log, Is.Empty);

                Set(entity, "m_deactivationInProgress", false);
                Call(entity, "OnDisable");
                Assert.That(Entities(manager, "m_inactive"), Is.EqualTo(new[] { entity }));
                Assert.That(log, Is.EqualTo(new[] { "disabled" }));
            });
        }

        // OnDestroy 0x06003b61 (0x6C0790): the inlined Deregister needs a valid manager reference
        // and m_hasRegistered; RemoveLevelActivatedAction needs only a valid level reference.
        // OnLevelActivated 0x6C0940 on a destroyed entity removes its own subscription through
        // m_levelManagerRef.Get() and registers nothing.
        [Test]
        public void OnDestroyAndADestroyedLevelActivationUnsubscribe()
        {
            Owned.Run(owned =>
            {
                EntityActivationManager manager = Manager(owned);
                LevelManager levelManager = Level(owned, true, false);
                EntityActivationDefinition definition = Definition(owned, true, true, out _);
                var entity = owned.Host("Lucid visibility").AddComponent<EntityActivationLevelVisibilityEntity>();
                entity.SetDefinition(definition);
                Set(entity, "m_enabledOnAwake", true);
                Call(entity, "OnEntityManagerValid", manager);
                Call(entity, "OnLevelManagerValid", levelManager);
                Assert.That(LevelActivated(levelManager), Has.Length.EqualTo(1));

                Set(entity, "m_hasRegistered", false);
                Call(entity, "OnDestroy");
                Assert.That(Entities(manager, "m_active"), Is.EqualTo(new[] { entity }), "an unregistered entity is not deregistered");
                Assert.That(LevelActivated(levelManager), Is.Empty);

                Set(entity, "m_hasRegistered", true);
                Call(entity, "OnDestroy");
                Assert.That(Entities(manager, "m_active"), Is.Empty);

                var destroyed = owned.Host("Lucid destroyed visibility").AddComponent<EntityActivationLevelVisibilityEntity>();
                destroyed.SetDefinition(definition);
                Call(destroyed, "OnLevelManagerValid", levelManager);
                Object.DestroyImmediate(destroyed);
                Call(destroyed, "OnLevelActivated", (LevelManagerLevel)null);
                Assert.That(LevelActivated(levelManager), Is.Empty);
                Assert.That(Entities(manager, "m_active").Concat(Entities(manager, "m_inactive")), Is.Empty);
            });
        }

        // TypeDef 0x02000a65: the constructor 0x6C3FB0 allocates both lists; AddMission
        // 0x06003bc1 picks [0x50 + 8 * !enable] and adds only a missing entry. Awake 0x6C3DE0
        // runs the base Awake, then stores activeSelf (the base stores activeInHierarchy).
        [Test]
        public void MissionVisibilityEntityKeepsDistinctMissionListsAndItsOwnAwakeState()
        {
            Type type = typeof(EntityActivationMissionVisibilityEntity);
            AssertMembers(type, 0x00100001,
                new[] { ("m_missionsToEnable", 0x0001), ("m_missionsToDisable", 0x0001), ("m_entityActiveOnAwake", 0x0001) },
                new[] { ("get_EntitiesToEnable", 0x09e6), ("get_EntitiesToDisable", 0x09e6), ("Awake", 0x00c4), ("OnLevelActivated", 0x01e6),
                        ("AddMission", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(type, 2, 1);
            Assert.That(type.GetInterfaces(), Does.Contain(typeof(IEntityActivatable<MissionDefinition>)));

            Owned.Run(owned =>
            {
                LevelManager levelManager = Level(owned, true, false);
                owned.LeaseRow<EntityActivationManager>();
                GameObject parent = owned.Host("Lucid hidden parent", false);
                GameObject host = owned.Host("Lucid mission visibility", true, parent.transform);
                var entity = host.AddComponent<EntityActivationMissionVisibilityEntity>();
                Assert.That(entity.EntitiesToEnable, Is.Empty.And.SameAs(Get<List<MissionDefinition>>(entity, "m_missionsToEnable")));
                Assert.That(entity.EntitiesToDisable, Is.Empty.And.Not.SameAs(entity.EntitiesToEnable));

                entity.AddMission(null, true);
                entity.AddMission(null, true);
                Assert.That(entity.EntitiesToEnable, Has.Count.EqualTo(1), "a contained mission is not added again");
                Assert.That(entity.EntitiesToDisable, Is.Empty);
                entity.AddMission(null, false);
                Assert.That(entity.EntitiesToDisable, Has.Count.EqualTo(1));

                Call(entity, "Awake");
                Assert.That(Get<bool>(entity, "m_enabledOnAwake"), Is.False, "base: activeInHierarchy");
                Assert.That(Get<bool>(entity, "m_entityActiveOnAwake"), Is.True, "own: activeSelf");
                Assert.That(LevelActivated(levelManager).Single().Target, Is.SameAs(entity));
            });
        }

        // OnLevelActivated 0x06003bc0 (0x6C3E10): nothing for a destroyed entity or an unchanged
        // activeSelf; otherwise the awake state picks Activate or Deactivate through Get().
        [Test]
        public void MissionVisibilityLevelActivationRestoresOnlyAChangedVisibility()
        {
            Owned.Run(owned =>
            {
                EntityActivationManager manager = Manager(owned);
                owned.LeaseRow<LevelManager>();
                GameObject host = owned.Host("Lucid mission visibility");
                var entity = host.AddComponent<EntityActivationMissionVisibilityEntity>();
                entity.SetDefinition(Definition(owned, true, true, out _));
                Set(entity, "m_entityActiveOnAwake", true);
                Call(entity, "OnEntityManagerValid", manager);
                Assert.That(Entities(manager, "m_inactive"), Is.EqualTo(new[] { entity }), "registered at the base awake state");
                Assert.That(host.activeSelf, Is.False, "the base awake state (inactive) was restored");

                host.SetActive(true);
                entity.OnLevelActivated();
                Assert.That(Entities(manager, "m_inactive"), Is.EqualTo(new[] { entity }), "activeSelf already matches");

                host.SetActive(false);
                entity.OnLevelActivated();
                Assert.That(Entities(manager, "m_active"), Is.EqualTo(new[] { entity }));
                Assert.That(host.activeSelf, Is.True);

                Set(entity, "m_entityActiveOnAwake", false);
                entity.OnLevelActivated();
                Assert.That(Entities(manager, "m_inactive"), Is.EqualTo(new[] { entity }));

                host.SetActive(true);
                Object.DestroyImmediate(entity);
                Assert.DoesNotThrow(() => entity.OnLevelActivated());
                Assert.That(Entities(manager, "m_inactive"), Is.EqualTo(new[] { entity }));
            });
        }
    }
}
