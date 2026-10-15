using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Actual engine creation and destruction of the original HLPhysics.Runtime singleton. The original
    // constructor (x86-64 0x1a4cde0) only tail-calls MonoSingleton<HLPhysics>'s constructor, so
    // construction alone publishes nothing; Unity's Awake (MonoSingleton) registers the system and sets
    // Instance, and OnDestroy unregisters it and clears Instance. The type is reached by name, so this
    // assembly needs no HLPhysics.Runtime reference.
    // Ownership: borrowed state is refused before any object exists; the raw registry key is leased while
    // still absent; the row is captured immediately after activation, before any assertion, and only when
    // it holds this fixture's component or nothing, so a foreign replacement is never adopted. Every
    // cleanup step runs independently through the lease, the first body failure stays primary, and later
    // cleanup failures are reported in order without hiding it.
    public sealed class OriginalHLPhysicsLifecycleTests
    {
        private static readonly Type Physics = Type.GetType("Hardlight.HLPhysics, HLPhysics.Runtime", true);

        // Read-only access to the public static MonoSingleton<HLPhysics>.Instance getter.
        private static object Instance() =>
            typeof(MonoSingleton<>).MakeGenericType(Physics).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

        private sealed class Outcome
        {
            internal Exception BodyFailure;
            internal IReadOnlyList<LifecycleSystemRowLease.FailureRecord> CleanupFailures;
            internal readonly List<string> Notes = new List<string>();
            // Observed after construction and before activation; the original constructor publishes nothing.
            internal bool InstanceBeforeActivation;
            internal bool RowBeforeActivation;
        }

        // Creates the owned HLPhysics through the engine, runs body, then always releases owned state.
        // injectedCleanupFailure runs as an extra cleanup step before the owned object is destroyed.
        private static Outcome RunOwnedLifecycle(Action<Component> body, Action injectedCleanupFailure = null)
        {
            IDictionary registry = LifecycleSystemRowLease.SharedRegistry;
            string name = Physics.ToString();
            Assert.That(ReferenceEquals(Instance(), null), "The fixture refuses a borrowed HLPhysics singleton.");
            var lease = new LifecycleSystemRowLease(registry, name);
            var outcome = new Outcome();
            GameObject owner = null;
            Component physics = null;
            SystemRef row = null;
            try
            {
                owner = new GameObject("Lucid owned HLPhysics");
                owner.SetActive(false);
                physics = owner.AddComponent(Physics);
                outcome.InstanceBeforeActivation = !ReferenceEquals(Instance(), null);
                outcome.RowBeforeActivation = registry.Contains(name);
                owner.SetActive(true);
                // Capture before any assertion, by a non-creating read of the raw row, and only a row that
                // holds this component or nothing.
                if (registry.Contains(name))
                {
                    object raw = registry[name];
                    row = (SystemRef)raw.GetType().GetField("SystemRef").GetValue(raw);
                    object current = row.GetSafe();
                    if (current == null || ReferenceEquals(current, physics)) lease.CaptureCreatedRow(row);
                    else { outcome.Notes.Add("foreign system in the leased row; not captured"); row = null; }
                }
                body(physics);
            }
            catch (Exception failure)
            {
                outcome.BodyFailure = failure;
            }
            finally
            {
                if (injectedCleanupFailure != null) lease.RunCleanup("injected-cleanup", injectedCleanupFailure);
                GameObject ownedObject = owner;
                lease.RunCleanup("destroy-owned-object", () => { if (ownedObject != null) Object.DestroyImmediate(ownedObject); });
                // If OnDestroy did not unregister (for example Awake registered without publishing
                // Instance), release the system only when the captured row still holds this fixture's
                // component, through the public API. A foreign or replaced system is left alone.
                Component ownedComponent = physics;
                SystemRef capturedRow = row;
                lease.RunCleanup("unregister-owned-system", () =>
                {
                    if (capturedRow != null && !ReferenceEquals(ownedComponent, null) && ReferenceEquals(capturedRow.GetSafe(), ownedComponent))
                    {
                        outcome.Notes.Add("owned system still registered after destruction; unregistered by the fixture");
                        ProcessManager.UnregisterSystem((ISystem)ownedComponent);
                    }
                });
                lease.Dispose();
                outcome.CleanupFailures = lease.CleanupFailures;
            }
            return outcome;
        }

        private static Dictionary<object, object> Snapshot(IDictionary registry)
        {
            var rows = new Dictionary<object, object>();
            foreach (DictionaryEntry entry in registry) rows.Add(entry.Key, entry.Value);
            return rows;
        }

        private static void RequireRestored(Dictionary<object, object> foreignRows)
        {
            IDictionary registry = LifecycleSystemRowLease.SharedRegistry;
            Assert.That(ReferenceEquals(Instance(), null), Is.True, "Instance is cleared.");
            Assert.That(registry.Contains(Physics.ToString()), Is.False, "the owned cleared row is removed");
            Assert.That(registry.Count, Is.EqualTo(foreignRows.Count));
            foreach (KeyValuePair<object, object> entry in foreignRows)
                Assert.That(registry[entry.Key], Is.SameAs(entry.Value), "Foreign row " + entry.Key + " is unchanged.");
        }

        // Catches a throwing or side-effecting constructor, a missing Awake registration and an OnDestroy
        // that leaves the system or the singleton behind.
        [Test]
        public void EngineCreationRegistersOnAwakeAndDestructionReleasesIt()
        {
            string name = Physics.ToString();
            Dictionary<object, object> foreignRows = Snapshot(LifecycleSystemRowLease.SharedRegistry);
            Outcome outcome = RunOwnedLifecycle(physics =>
            {
                Assert.That(physics, Is.Not.Null, "the original constructor completes");
                Assert.That(Instance(), Is.SameAs(physics), "Unity's Awake publishes the singleton");
                // Non-creating lookup: a missing registration must fail here without adding a row.
                Assert.That(ProcessManager.GetSystemRef(name, false).GetSafe(), Is.SameAs(physics), "Awake registered this system");
                Object.DestroyImmediate(physics.gameObject);
                Assert.That(ReferenceEquals(Instance(), null), Is.True, "OnDestroy clears Instance");
                Assert.That(ProcessManager.IsSystemNull(name), Is.True, "OnDestroy unregisters the system");
            });
            // Construction alone (original ctor 0x1a4cde0 only tail-calls the base) publishes no Instance
            // and creates no row; checked before any body failure is rethrown.
            Assert.That(outcome.InstanceBeforeActivation, Is.False, "construction alone publishes no Instance");
            Assert.That(outcome.RowBeforeActivation, Is.False, "construction alone registers nothing");
            if (outcome.BodyFailure != null)
            {
                // Keep the body failure primary; a failing diagnostic writer cannot replace it.
                foreach (LifecycleSystemRowLease.FailureRecord record in outcome.CleanupFailures)
                    try { TestContext.Error.WriteLine("HLPhysics cleanup also failed at " + record.Step + ": " + record.Failure); }
                    catch (Exception) { }
                ExceptionDispatchInfo.Capture(outcome.BodyFailure).Throw();
            }
            Assert.That(outcome.Notes, Is.Empty, "no foreign row appeared and OnDestroy unregistered");
            Assert.That(outcome.CleanupFailures, Is.Empty);
            RequireRestored(foreignRows);
        }

        // Same-process isolation control: a body failure after Awake, plus an injected cleanup failure,
        // must keep the body failure primary, report the cleanup failure, still destroy the owned object,
        // remove the owned row and leave foreign rows untouched, so later tests see restored state.
        [Test]
        public void FailureAfterAwakeStillReleasesOwnedStateAndKeepsTheFirstFailure()
        {
            Dictionary<object, object> foreignRows = Snapshot(LifecycleSystemRowLease.SharedRegistry);
            var bodyFailure = new InvalidOperationException("lucid-injected-body-failure");
            var cleanupFailure = new InvalidOperationException("lucid-injected-cleanup-failure");
            Component created = null;
            Outcome outcome = RunOwnedLifecycle(physics =>
            {
                created = physics;
                Assert.That(Instance(), Is.SameAs(physics), "registered before the injected failure");
                throw bodyFailure;
            }, () => throw cleanupFailure);
            Assert.That(outcome.BodyFailure, Is.SameAs(bodyFailure), "the first body failure stays primary");
            Assert.That(outcome.CleanupFailures, Has.Count.EqualTo(1));
            Assert.That(outcome.CleanupFailures[0].Step, Is.EqualTo("injected-cleanup"));
            Assert.That(outcome.CleanupFailures[0].Failure, Is.SameAs(cleanupFailure));
            Assert.That(created == null, Is.True, "the owned object was still destroyed after the cleanup failure");
            RequireRestored(foreignRows);
        }
    }
}
