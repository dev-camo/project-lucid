using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UObj = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Original PerformanceDependency 02000209 on owned inactive components.
    // Private lifecycle bodies are invoked explicitly; Edit Mode runs no
    // automatic Awake/OnDestroy for this component.
    public sealed class OriginalPerformanceDependencyGapTests
    {
        private const BindingFlags Own = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // OnPerformanceProfileChanged 0x06000d72 x86-64 0x1b0fba0: the Hide case
        // calls GameObject.SetActive(valid) (0x1b0fc82), so a valid profile
        // reactivates the object; the Destroy case tests valid before
        // Object.Destroy (0x1b0fc87/0x1b0fcba). Fails if Hide only deactivates,
        // inverts validity, or Destroy runs for a valid profile (Edit Mode
        // Destroy would also log an unexpected error).
        [Test]
        public void HideFollowsValidityAndDestroyKeepsAValidObject()
        {
            using (var owned = new OwnedDependency())
            {
                GameObject host = owned.Host;
                Assert.That(owned.Get("m_onDependencyNotMet"), Is.EqualTo(PerformanceDependency.Behaviour.Hide), "Hide is the original default.");
                Assert.That(host.activeSelf, Is.False);

                owned.Required.Level = PerformanceProfile.QualityLevel.High;
                owned.Change();
                Assert.That(host.activeSelf, Is.True, "A valid profile activates a hidden dependency.");

                owned.Required.Level = PerformanceProfile.QualityLevel.VeryHigh;
                owned.Change();
                Assert.That(host.activeSelf, Is.False, "An invalid profile hides it.");
                owned.Change();
                Assert.That(host.activeSelf, Is.False);

                owned.Required.Level = PerformanceProfile.QualityLevel.Medium;
                owned.Change();
                Assert.That(host.activeSelf, Is.True);

                owned.Set("m_onDependencyNotMet", PerformanceDependency.Behaviour.Destroy);
                owned.Required.Level = PerformanceProfile.QualityLevel.High;
                owned.Change();
                Assert.That(host != null && owned.Dependency != null, Is.True, "A valid profile never destroys.");
                Assert.That(host.activeSelf, Is.True, "Destroy behaviour does not change activity.");
                LogAssert.NoUnexpectedReceived();
            }
        }

        // OnDestroy 0x06000d73 x86-64 0x1b10050: when the cached reference is
        // valid, remove this component's handler from the live manager event
        // (0x1b1019a), then its startup (0x1b101f9) and shutdown (0x1b1024d)
        // registry handlers. Fails if any removal is skipped, if a foreign
        // listener is removed instead, or if a repeated OnDestroy disturbs the
        // remaining listeners.
        [Test]
        public void DestroyRemovesOnlyItsOwnProfileAndRegistryHandlers()
        {
            RuntimeHelpers.RunClassConstructor(typeof(PerformanceDependency).TypeHandle);
            var reference = (SystemRef<ScalablePerformance>)typeof(PerformanceDependency)
                .GetField("s_scalablePerformanceRef", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            string name = ProcessManager.GetDefaultName<ScalablePerformance>();
            object row = Registry()[name];
            List<object> rowReferences = RowReferences(row);
            Assert.That(rowReferences, Has.Member(reference));
            foreach (object rowReference in rowReferences)
                Assert.That(Idle(rowReference), Is.True, "Every cached ScalablePerformance reference must be unowned and unobserved.");

            var trace = new List<string>();
            Action<PerformanceProfile> prior = profile => trace.Add("prior");
            Action<ScalablePerformance> foreignStartup = system => trace.Add("foreign-startup");
            GameObject managerHost = null;
            ScalablePerformance manager = null;
            Delegate ownStartup = null, ownShutdown = null, ownProfile = null;
            try
            {
                managerHost = new GameObject("Owned original performance manager");
                managerHost.SetActive(false);
                manager = managerHost.AddComponent<ScalablePerformance>();
                ProcessManager.RegisterSystem(manager);
                Assert.That(reference.GetSafe(), Is.SameAs(manager));
                Assert.That(ReferenceEquals(Registry()[name], row), Is.True, "Registration reuses the existing row.");
                manager.OnProfileUpdated += prior;
                reference.OnSystemStartup += foreignStartup;

                using (var owned = new OwnedDependency())
                {
                    PerformanceDependency dependency = owned.Dependency;
                    ownStartup = Bound(typeof(Action<ScalablePerformance>), dependency, "ScalablePerformanceStartup");
                    ownShutdown = Bound(typeof(Action<ScalablePerformance>), dependency, "OnScalablePerformanceShutdown");
                    ownProfile = Bound(typeof(Action<PerformanceProfile>), dependency, "OnPerformanceProfileChanged");
                    try
                    {
                        owned.Call("Awake");
                        Assert.That(Handlers(manager, typeof(ScalablePerformance), "OnProfileUpdated"), Is.EqualTo(new[] { (Delegate)prior, ownProfile }));
                        Assert.That(Handlers(reference, SystemRefType(reference), "OnSystemStartup"), Is.EqualTo(new[] { (Delegate)foreignStartup, ownStartup }));
                        Assert.That(Handlers(reference, SystemRefType(reference), "OnSystemShutdown"), Is.EqualTo(new[] { ownShutdown }));

                        owned.Call("OnDestroy");
                        Assert.That(Handlers(manager, typeof(ScalablePerformance), "OnProfileUpdated"), Is.EqualTo(new[] { (Delegate)prior }));
                        Assert.That(Handlers(reference, SystemRefType(reference), "OnSystemStartup"), Is.EqualTo(new[] { (Delegate)foreignStartup }));
                        Assert.That(Handlers(reference, SystemRefType(reference), "OnSystemShutdown"), Is.Empty);

                        owned.Call("OnDestroy");
                        Assert.That(Handlers(manager, typeof(ScalablePerformance), "OnProfileUpdated"), Is.EqualTo(new[] { (Delegate)prior }));
                        Assert.That(Handlers(reference, SystemRefType(reference), "OnSystemStartup"), Is.EqualTo(new[] { (Delegate)foreignStartup }));
                        Assert.That(trace, Is.Empty, "No owned listener ran.");
                    }
                    finally
                    {
                        // Public removals only; each is a no-op when OnDestroy already ran.
                        reference.OnSystemStartup -= (Action<ScalablePerformance>)ownStartup;
                        reference.OnSystemShutdown -= (Action<ScalablePerformance>)ownShutdown;
                        manager.OnProfileUpdated -= (Action<PerformanceProfile>)ownProfile;
                    }
                }
            }
            finally
            {
                reference.OnSystemStartup -= foreignStartup;
                if ((UObj)manager != null)
                {
                    manager.OnProfileUpdated -= prior;
                    if (ReferenceEquals(reference.GetSafe(), manager)) ProcessManager.UnregisterSystem(manager);
                }
                if (managerHost != null) UObj.DestroyImmediate(managerHost);
            }
            Assert.That(ReferenceEquals(Registry()[name], row), Is.True);
            foreach (object rowReference in RowReferences(row))
                Assert.That(Idle(rowReference), Is.True, "The shared row returns to its unowned state.");
        }

        private static IDictionary Registry() => (IDictionary)typeof(ProcessManager)
            .GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        private static List<object> RowReferences(object row)
        {
            var references = new List<object> { row.GetType().GetField("SystemRef", Own).GetValue(row) };
            foreach (DictionaryEntry entry in (IDictionary)row.GetType().GetField("SystemRefDictionary", Own).GetValue(row))
                references.Add(entry.Value);
            return references;
        }

        private static Type SystemRefType(object reference)
        {
            Type type = reference.GetType();
            while (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(SystemRef<>)) type = type.BaseType;
            return type;
        }

        // Read-only ownership check of a cached reference.
        private static bool Idle(object reference)
        {
            Type type = SystemRefType(reference);
            foreach (string field in new[] { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
                if (type.GetField(field, Own).GetValue(reference) != null) return false;
            return true;
        }

        private static Delegate[] Handlers(object owner, Type type, string field)
        {
            var value = (Delegate)type.GetField(field, Own).GetValue(owner);
            return value == null ? new Delegate[0] : value.GetInvocationList();
        }

        private static Delegate Bound(Type delegateType, object target, string method) =>
            Delegate.CreateDelegate(delegateType, target, target.GetType().GetMethod(method, Own));

        // An owned inactive dependency with an authored High profile attribute.
        private sealed class OwnedDependency : IDisposable
        {
            public readonly GameObject Host;
            public readonly PerformanceDependency Dependency;
            public readonly PerformanceProfile Profile;
            public readonly ScalableFeature Feature;
            public readonly PerformanceAttribute Required;

            public OwnedDependency()
            {
                RuntimeHelpers.RunClassConstructor(typeof(PerformanceDependency).TypeHandle);
                try
                {
                    Host = new GameObject("Owned original performance dependency");
                    Host.SetActive(false);
                    Dependency = Host.AddComponent<PerformanceDependency>();
                    Profile = ScriptableObject.CreateInstance<PerformanceProfile>();
                    Feature = ScriptableObject.CreateInstance<ScalableFeature>();
                    Feature.ID = "owned-performance-dependency-gap";
                    ((List<PerformanceAttribute>)typeof(PerformanceProfile).GetField("m_attributes", Own).GetValue(Profile))
                        .Add(new PerformanceAttribute { Feature = Feature, Level = PerformanceProfile.QualityLevel.High });
                    Required = new PerformanceAttribute { Feature = Feature, Level = PerformanceProfile.QualityLevel.High };
                    Set("m_requiredPerformanceAttribute", Required);
                }
                catch { Dispose(); throw; }
            }

            public object Get(string field) => typeof(PerformanceDependency).GetField(field, Own).GetValue(Dependency);
            public void Set(string field, object value) => typeof(PerformanceDependency).GetField(field, Own).SetValue(Dependency, value);
            public void Change() => Call("OnPerformanceProfileChanged", Profile);

            public void Call(string method, params object[] arguments)
            {
                try { typeof(PerformanceDependency).GetMethod(method, Own).Invoke(Dependency, arguments); }
                catch (TargetInvocationException error) { throw error.InnerException; }
            }

            public void Dispose()
            {
                if (Host != null) UObj.DestroyImmediate(Host);
                if ((UObj)Feature != null) UObj.DestroyImmediate(Feature);
                if ((UObj)Profile != null) UObj.DestroyImmediate(Profile);
            }
        }
    }
}
