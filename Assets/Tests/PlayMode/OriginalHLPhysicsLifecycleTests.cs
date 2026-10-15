using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    // assembly needs no HLPhysics.Runtime reference. Borrowed state is refused before any object exists,
    // and only the owned, cleared registry row is removed.
    public sealed class OriginalHLPhysicsLifecycleTests
    {
        private static readonly Type Physics = Type.GetType("Hardlight.HLPhysics, HLPhysics.Runtime", true);

        // Read-only access to the public static MonoSingleton<HLPhysics>.Instance getter.
        private static object Instance() =>
            typeof(MonoSingleton<>).MakeGenericType(Physics).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

        // Catches a throwing or side-effecting constructor, a missing Awake registration and an OnDestroy
        // that leaves the system or the singleton behind.
        [Test]
        public void EngineCreationRegistersOnAwakeAndDestructionReleasesIt()
        {
            IDictionary registry = LifecycleSystemRowLease.SharedRegistry;
            string name = Physics.ToString();
            Assert.That(ReferenceEquals(Instance(), null), "The fixture refuses a borrowed HLPhysics singleton.");
            var foreignRows = new Dictionary<object, object>();
            foreach (DictionaryEntry entry in registry) foreignRows.Add(entry.Key, entry.Value);
            var lease = new LifecycleSystemRowLease(registry, name);
            var owner = new GameObject("Lucid owned HLPhysics");
            owner.SetActive(false);
            try
            {
                Component physics = owner.AddComponent(Physics);
                Assert.That(physics, Is.Not.Null, "the original constructor completes");
                Assert.That(registry.Contains(name), Is.False, "construction alone registers nothing");
                Assert.That(ReferenceEquals(Instance(), null), Is.True, "construction alone publishes no Instance");

                owner.SetActive(true);
                Assert.That(Instance(), Is.SameAs(physics), "Unity's Awake publishes the singleton");
                SystemRef row = ProcessManager.GetSystemRef(name);
                lease.CaptureCreatedRow(row);
                Assert.That(row.GetSafe(), Is.SameAs(physics), "Awake registered this system");

                Object.DestroyImmediate(owner);
                owner = null;
                Assert.That(ReferenceEquals(Instance(), null), Is.True, "OnDestroy clears Instance");
                Assert.That(ProcessManager.IsSystemNull(name), Is.True, "OnDestroy unregisters the system");
            }
            finally
            {
                if (owner != null) Object.DestroyImmediate(owner);
                lease.Dispose();
            }
            Assert.That(lease.CleanupFailures, Is.Empty);
            Assert.That(registry.Contains(name), Is.False, "the owned cleared row is removed");
            Assert.That(registry.Count, Is.EqualTo(foreignRows.Count));
            foreach (KeyValuePair<object, object> entry in foreignRows)
                Assert.That(registry[entry.Key], Is.SameAs(entry.Value), "Foreign row " + entry.Key + " is unchanged.");
        }
    }
}
