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
    // absent. That row cannot be removed afterwards: a later registration
    // would make a new row, and the static reference would stay detached for
    // the rest of the domain. So when this case is the first to use the type,
    // the row is its one allowlisted shared-state change, and the case checks
    // that the row holds only that idle reference.
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
            AssertOnlyTheTypeInitializerRowRemains();
        }

        // The type initializer's reference is the row's typed entry, and every
        // reference in the row holds no system, observer or deferred work.
        private static void AssertOnlyTheTypeInitializerRowRemains()
        {
            object initializerReference = typeof(PerformanceDependency).GetField("s_scalablePerformanceRef", OwnStatic).GetValue(null);
            var registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", OwnStatic).GetValue(null);
            string name = ProcessManager.GetDefaultName<ScalablePerformance>();
            Assert.That(registry.Contains(name), Is.True, "The type initializer registers the ScalablePerformance row.");
            object row = registry[name];
            var typedCache = (IDictionary)row.GetType().GetField("SystemRefDictionary", Own).GetValue(row);
            Assert.That(typedCache[name], Is.SameAs(initializerReference), "The row's typed entry is the type initializer's reference.");
            var references = new List<object> { row.GetType().GetField("SystemRef", Own).GetValue(row) };
            foreach (DictionaryEntry entry in typedCache) references.Add(entry.Value);
            foreach (object reference in references)
            {
                System.Type type = reference.GetType();
                while (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(SystemRef<>)) type = type.BaseType;
                // Read ownership only; never clear events, deferred actions or values.
                foreach (string field in new[] { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
                    Assert.That(type.GetField(field, Own).GetValue(reference), Is.Null, "The ScalablePerformance row stays idle: " + field);
            }
        }
    }
}
