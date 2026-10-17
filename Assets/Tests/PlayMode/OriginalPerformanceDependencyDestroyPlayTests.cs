using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Original PerformanceDependency Destroy behaviour on an owned inactive
    // object. Its components never awake, so neither Awake nor OnDestroy
    // touches the shared ScalablePerformance reference.
    //
    // The type initializer (06000d75) stores
    // GetSystemRef<ScalablePerformance>(null, true) in a static readonly
    // field, which adds the "Hardlight.ScalablePerformance" row when it is
    // absent. Removing that row would detach the static reference for the
    // rest of the domain, so it stays (allowlisted by its exact changes). A
    // row that existed before the case must be left exactly as it was.
    public sealed class OriginalPerformanceDependencyDestroyPlayTests
    {
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags OwnStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // OnPerformanceProfileChanged 0x06000d72 x86-64 0x1b0fba0: for an
        // invalid profile the Destroy case passes Component.gameObject
        // (0x1b0fc91) to Object.Destroy (0x1b0fcba). Fails if only the
        // component is destroyed, if the destroy waits for a valid profile,
        // or if it is skipped.
        [UnityTest]
        public IEnumerator InvalidProfileDestroysTheWholeGameObject()
        {
            string name = ProcessManager.GetDefaultName<ScalablePerformance>();
            RowSnapshot borrowed = Registry().Contains(name) ? new RowSnapshot(Registry()[name]) : null;
            RuntimeHelpers.RunClassConstructor(typeof(PerformanceDependency).TypeHandle);
            GameObject host = null;
            PerformanceProfile profile = null;
            ScalableFeature feature = null;
            try
            {
                host = new GameObject("Owned original performance dependency destroy");
                host.SetActive(false);
                PerformanceDependency dependency = host.AddComponent<PerformanceDependency>();
                profile = ScriptableObject.CreateInstance<PerformanceProfile>();
                feature = ScriptableObject.CreateInstance<ScalableFeature>();
                feature.ID = "owned-performance-dependency-destroy";
                ((List<PerformanceAttribute>)typeof(PerformanceProfile).GetField("m_attributes", Own).GetValue(profile))
                    .Add(new PerformanceAttribute { Feature = feature, Level = PerformanceProfile.QualityLevel.High });
                var required = new PerformanceAttribute { Feature = feature, Level = PerformanceProfile.QualityLevel.VeryHigh };
                FieldInfo behaviour = typeof(PerformanceDependency).GetField("m_onDependencyNotMet", Own);
                typeof(PerformanceDependency).GetField("m_requiredPerformanceAttribute", Own).SetValue(dependency, required);
                behaviour.SetValue(dependency, PerformanceDependency.Behaviour.Destroy);
                MethodInfo change = typeof(PerformanceDependency).GetMethod("OnPerformanceProfileChanged", Own);

                // High does not reach VeryHigh, so the GreaterThanOrEqual default is invalid.
                change.Invoke(dependency, new object[] { profile });
                Assert.That(host != null, Is.True, "Object.Destroy is deferred to the end of the frame.");
                Assert.That(host.activeSelf, Is.False, "Destroy behaviour does not change activity.");
                yield return null;
                Assert.That(host == null, Is.True, "The whole GameObject is destroyed, not only the component.");
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
                if ((Object)feature != null) Object.DestroyImmediate(feature);
                if ((Object)profile != null) Object.DestroyImmediate(profile);
            }
            // A row still absent means the initializer ran earlier and its row was removed; nothing changed here.
            if (borrowed != null) borrowed.AssertUnchanged(Registry(), name);
            else if (Registry().Contains(name)) AssertOnlyTheTypeInitializerRow(Registry()[name], name);
        }

        private static IDictionary Registry() => (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", OwnStatic).GetValue(null);

        private static IDictionary TypedCache(object row) => (IDictionary)row.GetType().GetField("SystemRefDictionary", Own).GetValue(row);

        private static object UntypedReference(object row) => row.GetType().GetField("SystemRef", Own).GetValue(row);

        private static readonly string[] OwnershipFields = { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" };

        // Read ownership only; never clear events, deferred actions or values.
        private static object[] Ownership(object reference)
        {
            System.Type type = reference.GetType();
            while (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(SystemRef<>)) type = type.BaseType;
            var values = new object[OwnershipFields.Length];
            for (int i = 0; i < values.Length; i++) values[i] = type.GetField(OwnershipFields[i], Own).GetValue(reference);
            return values;
        }

        // A row this case's type initializer created holds only the
        // initializer's typed reference, and nothing owns or observes it.
        private static void AssertOnlyTheTypeInitializerRow(object row, string name)
        {
            object initializerReference = typeof(PerformanceDependency).GetField("s_scalablePerformanceRef", OwnStatic).GetValue(null);
            IDictionary typedCache = TypedCache(row);
            Assert.That(typedCache.Count, Is.EqualTo(1), "The type initializer adds one typed reference.");
            Assert.That(typedCache[name], Is.SameAs(initializerReference), "The row's typed entry is the type initializer's reference.");
            foreach (object reference in new[] { UntypedReference(row), typedCache[name] })
            {
                object[] ownership = Ownership(reference);
                for (int i = 0; i < ownership.Length; i++)
                    Assert.That(ownership[i], Is.Null, "The ScalablePerformance row stays idle: " + OwnershipFields[i]);
            }
        }

        // A row that existed before the case: the same row, references,
        // typed entries, owners, observers and deferred work afterwards.
        private sealed class RowSnapshot
        {
            private readonly object row;
            private readonly object untyped;
            private readonly object[] untypedOwnership;
            private readonly List<KeyValuePair<object, object>> typed = new List<KeyValuePair<object, object>>();
            private readonly List<object[]> typedOwnership = new List<object[]>();

            internal RowSnapshot(object row)
            {
                this.row = row;
                untyped = UntypedReference(row);
                untypedOwnership = Ownership(untyped);
                foreach (DictionaryEntry entry in TypedCache(row))
                {
                    typed.Add(new KeyValuePair<object, object>(entry.Key, entry.Value));
                    typedOwnership.Add(Ownership(entry.Value));
                }
            }

            internal void AssertUnchanged(IDictionary registry, string name)
            {
                Assert.That(registry.Contains(name) && ReferenceEquals(registry[name], row), Is.True, "The borrowed ScalablePerformance row stays registered.");
                Assert.That(UntypedReference(row), Is.SameAs(untyped), "The borrowed row keeps its untyped reference.");
                AssertSameOwnership(untypedOwnership, Ownership(untyped));
                IDictionary typedCache = TypedCache(row);
                Assert.That(typedCache.Count, Is.EqualTo(typed.Count), "The borrowed row keeps its typed entries.");
                for (int i = 0; i < typed.Count; i++)
                {
                    Assert.That(typedCache.Contains(typed[i].Key) && ReferenceEquals(typedCache[typed[i].Key], typed[i].Value), Is.True,
                        "The borrowed row keeps its typed entry " + typed[i].Key);
                    AssertSameOwnership(typedOwnership[i], Ownership(typed[i].Value));
                }
            }

            private static void AssertSameOwnership(object[] before, object[] after)
            {
                for (int i = 0; i < before.Length; i++)
                    Assert.That(after[i], Is.SameAs(before[i]), "The borrowed row keeps " + OwnershipFields[i]);
            }
        }
    }
}
