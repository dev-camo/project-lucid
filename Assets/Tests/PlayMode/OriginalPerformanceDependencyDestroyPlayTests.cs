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
    public sealed class OriginalPerformanceDependencyDestroyPlayTests
    {
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

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
        }
    }
}
