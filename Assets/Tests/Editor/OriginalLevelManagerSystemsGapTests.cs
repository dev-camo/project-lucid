using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class OriginalLevelManagerSystemsGapTests
    {
        // A test-only ISystem under its own default registry name. Each
        // instance records its construction and Initialise dispatch.
        private sealed class OrderedSystem : ISystem
        {
            public static List<string> Trace;
            private static int s_created;
            public readonly int Number;

            public OrderedSystem()
            {
                Number = ++s_created;
                Trace?.Add("construct " + Number);
                this.SubscribeToAction(SystemAction.Initialise, _ => Trace?.Add("initialise " + Number));
                this.SubscribeToAction(SystemAction.Shutdown, _ => Trace?.Add("shutdown " + Number));
            }
        }

        // LevelManagerSystems.AddManagedSystem<T>(bool) 0x0600249c, shared
        // x86-64 0x95bf80: GetSystemRef<T> 0x95bfd8, SystemRef.IsValid 0x95bff0,
        // the canReplace byte test 0x95bff9 (false returns), Get 0x95c009 and
        // RemoveManagedSystem 0x95c01c, and only then Activator.CreateInstance
        // 0x95c029 before the instance overload 0x95c046. Always replacing, or
        // constructing the new system before the old one's shutdown, fails here.
        [Test]
        public void ConstructorReplacementShutsTheOldSystemDownBeforeCreatingTheNewOne()
        {
            string name = ProcessManager.GetDefaultName<OrderedSystem>();
            var registry = (IDictionary)typeof(ProcessManager)
                .GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            VerificationFixtureOwnership.RequireAbsent(registry, name, OrderedSystem.Trace);
            var trace = new List<string>();
            var created = new List<OrderedSystem>();
            OrderedSystem.Trace = trace;
            // Each OrderedSystem constructor subscribes to Initialise and Shutdown, and
            // SubscribeToAction creates a missing action row that unsubscribing keeps;
            // own those rows and release them last, after every system unsubscribed.
            var actionRows = new VerificationFixtureOwnership.ActionRows(SystemAction.Initialise, SystemAction.Shutdown);
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                var owner = new LevelManagerSystems(null);
                var first = new OrderedSystem();
                created.Add(first);
                owner.AddManagedSystem(first);
                Assert.That(trace, Is.EqualTo(new[] { "construct " + first.Number, "initialise " + first.Number }));

                trace.Clear();
                owner.AddManagedSystem<OrderedSystem>();
                Assert.That(trace, Is.Empty, "a valid registered system is kept without replacement");
                Assert.That(ProcessManager.GetSystemSafe<OrderedSystem>(), Is.SameAs(first));

                owner.AddManagedSystem<OrderedSystem>(true);
                OrderedSystem second = ProcessManager.GetSystemSafe<OrderedSystem>();
                created.Add(second);
                Assert.That(second, Is.Not.SameAs(first));
                Assert.That(trace, Is.EqualTo(new[]
                {
                    "shutdown " + first.Number,
                    "construct " + second.Number,
                    "initialise " + second.Number,
                }));
                Assert.That(owner.HasSystemOfType<OrderedSystem>(), Is.True);

                // A present but empty row is not valid, so it is filled without a shutdown.
                ProcessManager.UnregisterSystem(second);
                trace.Clear();
                owner.AddManagedSystem<OrderedSystem>();
                OrderedSystem third = ProcessManager.GetSystemSafe<OrderedSystem>();
                created.Add(third);
                Assert.That(trace, Is.EqualTo(new[] { "construct " + third.Number, "initialise " + third.Number }));
            },
            () =>
            {
                OrderedSystem.Trace = null;
                foreach (OrderedSystem system in created)
                    if (system != null) ProcessManager.UnsubscribeFromAllActions(system);
            },
            () =>
            {
                if (registry.Contains(name))
                {
                    ProcessManager.UnregisterSystem(name);
                    registry.Remove(name);
                }
            },
            actionRows.Dispose);
        }
    }
}
