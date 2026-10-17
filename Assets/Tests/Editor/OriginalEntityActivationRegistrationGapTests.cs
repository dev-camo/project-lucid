using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // The genuine manager, definitions and instances over test-owned entities;
    // only the logic lifecycle is fixture-recorded. The manager's constructor
    // subscribes to Update, which each test unsubscribes again.
    public sealed class OriginalEntityActivationRegistrationGapTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

        private sealed class Entity : IEntityActivatable
        {
            public Entity(EntityActivationDefinition definition, Transform transform)
            {
                Definition = definition;
                Transform = transform;
            }
            public EntityActivationDefinition Definition { get; }
            public Transform Transform { get; }
        }

        // EntityActivationManager.Register 0x06003bae, x86-64 0x6bf590:
        // TryRegisterEntity 0x6bf5bf on m_inactive (+0x18) with !active, then
        // 0x6bf5d7 on m_active (+0x10) with active, both passing isActive, so
        // the instance's first-time callback runs after the inactive row
        // exists. TryDeregisterEntity 0x06003bb1 (0x6c2ef0) calls RemoveAt
        // 0x6c3145 before the Transform Unity-null test 0x6c31cf and Shutdown
        // 0x6c31db. Creating the active row first, a non-first-time activation,
        // or a Transform check before removal fails here.
        [Test]
        public void RegisterCreatesBothRowsInactiveFirstAndDeregisterRemovesBeforeShutdown()
        {
            Owned.Run(owned =>
            {
                EntityActivationManager manager = owned.Manager();
                EntityActivationDefinition definition = owned.Definition(EntityActivationType.Collectable, out List<string> trace);
                var active = new Entity(definition, owned.Transform());
                bool inactiveRowSeen = false;
                manager.Register(active, true, () =>
                {
                    inactiveRowSeen = Rows(manager, "m_inactive").TryGetValue(EntityActivationType.Collectable,
                        out List<EntityActivationInstance> inactive) && inactive.Count == 0;
                    trace.Add("activated");
                }, () => trace.Add("deactivated"));
                Assert.That(inactiveRowSeen, Is.True, "the empty inactive row precedes the active registration");
                Assert.That(trace, Is.EqualTo(new[] { "activated", "start deactivate" }), "first-time activation skips ending");
                Assert.That(Entities(manager, "m_active"), Is.EqualTo(new[] { active }));
                Assert.That(Entities(manager, "m_inactive"), Is.Empty);

                trace.Clear();
                var detached = new Entity(definition, null);
                manager.Register(detached, false, () => trace.Add("activated"), () => trace.Add("deactivated"));
                Assert.That(trace, Is.EqualTo(new[] { "deactivated", "start activate" }));
                Assert.That(Entities(manager, "m_inactive"), Is.EqualTo(new[] { detached }));

                trace.Clear();
                manager.Deregister(detached);
                Assert.That(Entities(manager, "m_inactive"), Is.Empty, "removal precedes the Transform check");
                Assert.That(trace, Is.Empty, "a null Transform skips Shutdown");

                manager.Deregister(null);
                manager.Deregister(active);
                Assert.That(Entities(manager, "m_active"), Is.Empty);
                Assert.That(trace, Is.EqualTo(new[] { "shutdown activate", "shutdown deactivate" }));
            });
        }

        // Activate 0x06003bb2 (x86-64 0x6bf5f0) reads m_inactive through the
        // Dictionary indexer 0x6bf72a; Deactivate 0x06003bb3 (0x6bf830) uses
        // TryGetValue 0x6bf97e on m_active. Each RemoveAt (0x6bf79b/0x6bf9fa)
        // and destination Add precede the instance callback (OnDeactivate
        // 0x6bfa71), with no rollback. A TryGetValue in Activate, an indexer in
        // Deactivate, or restoring the lists after a throwing callback fails here.
        [Test]
        public void ActivateIndexesTheSourceRowWhileDeactivateProbesItAndNeitherRollsBack()
        {
            Owned.Run(owned =>
            {
                EntityActivationManager manager = owned.Manager();
                EntityActivationDefinition unregistered = owned.Definition(EntityActivationType.CollectableNoRespawn, out _);
                var stranger = new Entity(unregistered, owned.Transform());
                Assert.Throws<KeyNotFoundException>(() => manager.Activate(stranger));
                Assert.DoesNotThrow(() => manager.Deactivate(stranger));

                EntityActivationDefinition definition = owned.Definition(EntityActivationType.Collectable, out List<string> trace);
                var entity = new Entity(definition, owned.Transform());
                bool throwOnActivate = false, throwOnDeactivate = false;
                manager.Register(entity, false, () =>
                {
                    if (throwOnActivate) throw new InvalidOperationException("lucid-activate");
                }, () =>
                {
                    if (throwOnDeactivate) throw new InvalidOperationException("lucid-deactivate");
                });

                throwOnActivate = true;
                trace.Clear();
                Assert.Throws<InvalidOperationException>(() => manager.Activate(entity));
                Assert.That(Entities(manager, "m_inactive"), Is.Empty);
                Assert.That(Entities(manager, "m_active"), Is.EqualTo(new[] { entity }), "the move is published before the callback");
                Assert.That(trace, Is.EqualTo(new[] { "end activate" }), "the failed callback stops the opposite start");

                throwOnDeactivate = true;
                trace.Clear();
                Assert.Throws<InvalidOperationException>(() => manager.Deactivate(entity));
                Assert.That(Entities(manager, "m_active"), Is.Empty);
                Assert.That(Entities(manager, "m_inactive"), Is.EqualTo(new[] { entity }));
                Assert.That(trace, Is.EqualTo(new[] { "end deactivate" }));
            });
        }

        private static Dictionary<EntityActivationType, List<EntityActivationInstance>> Rows(EntityActivationManager manager, string field) =>
            (Dictionary<EntityActivationType, List<EntityActivationInstance>>)typeof(EntityActivationManager)
                .GetField(field, Instance).GetValue(manager);

        private static List<IEntityActivatable> Entities(EntityActivationManager manager, string field) =>
            Rows(manager, field)[EntityActivationType.Collectable].ConvertAll(instance => instance.Entity);

        private sealed class Owned
        {
            private Owned() { }

            private readonly List<Object> m_objects = new List<Object>();
            // Taken before the manager's constructor can create the Update row.
            private readonly VerificationFixtureOwnership.ActionRows m_actionRows = new VerificationFixtureOwnership.ActionRows(SystemAction.Update);
            private EntityActivationManager m_manager;

            public EntityActivationManager Manager() => m_manager = new EntityActivationManager();

            public Transform Transform()
            {
                var host = new GameObject("Lucid owned activatable");
                m_objects.Add(host);
                return host.transform;
            }

            public EntityActivationDefinition Definition(EntityActivationType type, out List<string> trace)
            {
                var activate = ScriptableObject.CreateInstance<ProjectLucidEntityActivationProbeDefinition>();
                var deactivate = ScriptableObject.CreateInstance<ProjectLucidEntityActivationProbeDefinition>();
                var definition = ScriptableObject.CreateInstance<EntityActivationDefinition>();
                m_objects.Add(activate);
                m_objects.Add(deactivate);
                m_objects.Add(definition);
                activate.Label = "activate";
                deactivate.Label = "deactivate";
                trace = activate.Trace;
                // Both logics report into one ordered trace.
                deactivate.Trace = trace;
                Set(definition, "m_type", type);
                Set(definition, "m_activateLogicDefinitions", new List<EntityActivationLogicDefinition> { activate });
                Set(definition, "m_deactivateLogicDefinitions", new List<EntityActivationLogicDefinition> { deactivate });
                return definition;
            }

            private static void Set(EntityActivationDefinition definition, string name, object value) =>
                typeof(EntityActivationDefinition).GetField(name, Instance).SetValue(definition, value);

            // Runs body on a fresh fixture, then unsubscribes the manager, destroys
            // the owned objects and releases the Update row last. Each step runs
            // even after an earlier failure, and the first failure stays the result.
            public static void Run(Action<Owned> body)
            {
                var owned = new Owned();
                VerificationFixtureOwnership.RunThenCleanUp(() => body(owned),
                    () => { if (owned.m_manager != null) owned.m_manager.UnsubscribeFromAction(SystemAction.Update); },
                    () => { foreach (Object value in owned.m_objects) if (value != null) Object.DestroyImmediate(value); },
                    owned.m_actionRows.Dispose);
            }
        }
    }
}
