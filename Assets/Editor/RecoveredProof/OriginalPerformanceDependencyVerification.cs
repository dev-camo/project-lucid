using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Hardlight;
using UnityEngine;
using UnityEngine.Events;
using UObj = UnityEngine.Object;

namespace ProjectLucid.Verification
{
    // Original PerformanceDependency 06000d6b..06000d75. The first two groups
    // exercise CLR fields/delegates on uninitialized managed objects only.
    // The remaining groups require real Unity objects; private lifecycle methods
    // are invoked explicitly, with no Awake, registry replacement or ForceReset.
    public static class OriginalPerformanceDependencyVerification
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = type.GetField(name, Instance | BindingFlags.Static);
            if (field == null) throw new InvalidOperationException("Missing original field " + type + "." + name);
            return field;
        }
        private static object Get(Type type, string name, object owner) { return Field(type, name).GetValue(owner); }
        private static void Set(Type type, string name, object owner, object value) { Field(type, name).SetValue(owner, value); }
        private static void DependencySet(PerformanceDependency owner, string name, object value)
        { Set(typeof(PerformanceDependency), name, owner, value); }
        private static object DependencyGet(PerformanceDependency owner, string name)
        { return Get(typeof(PerformanceDependency), name, owner); }
        private static void Call(object owner, string name, params object[] arguments)
        {
            MethodInfo method = owner.GetType().GetMethod(name, Instance);
            if (method == null) throw new InvalidOperationException("Missing original method " + name);
            try { method.Invoke(owner, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        private static void Change(PerformanceDependency owner, PerformanceProfile profile)
        { Call(owner, "OnPerformanceProfileChanged", profile); }
        private static void Check(bool condition, ref int count, string message)
        { if (!condition) throw new InvalidOperationException(message); count++; }
        private static bool Throws<T>(Action action) where T : Exception
        { try { action(); return false; } catch (T) { return true; } }
        private static string Trace(List<string> values) { return string.Join(",", values); }
        private static Action<bool> Callback(PerformanceDependency owner)
        { return (Action<bool>)DependencyGet(owner, "ValidateDependencyCallback"); }
        private static int Length(Delegate value) { return value == null ? 0 : value.GetInvocationList().Length; }
        private static PerformanceDependency ManagedObject()
        { return (PerformanceDependency)FormatterServices.GetUninitializedObject(typeof(PerformanceDependency)); }
        private static void InitialiseOriginalReference()
        {
            // The authored static initializer may create its one genuine cached
            // registry reference. Observe state after this initialization; never
            // remove its row or replace the readonly reference for a test.
            RuntimeHelpers.RunClassConstructor(typeof(PerformanceDependency).TypeHandle);
        }

        public static int RunCopyManaged20()
        {
            InitialiseOriginalReference();
            var registry = new RegistryObservation();
            int count = 0;
            try
            {
                PerformanceDependency source = ManagedObject(), target = ManagedObject();
                var attribute = new PerformanceAttribute { Level = PerformanceProfile.QualityLevel.High };
                object sourceEvent = FormatterServices.GetUninitializedObject(typeof(UnityEvent));
                object targetEvent = FormatterServices.GetUninitializedObject(typeof(UnityEvent));
                Action<bool> sourceCallback = value => { }, targetCallback = value => { };
                DependencySet(source, "m_requiredPerformanceAttribute", attribute);
                DependencySet(source, "m_comparison", PerformanceDependency.Comparison.Exactly);
                DependencySet(source, "m_onDependencyNotMet", PerformanceDependency.Behaviour.Callback);
                DependencySet(source, "m_triggeredEvent", sourceEvent);
                DependencySet(target, "m_triggeredEvent", targetEvent);
                source.ValidateDependencyCallback += sourceCallback;
                target.ValidateDependencyCallback += targetCallback;
                target.Copy(source);
                Check(ReferenceEquals(DependencyGet(target, "m_requiredPerformanceAttribute"), attribute), ref count, "Copy must retain the attribute alias");
                Check((PerformanceDependency.Comparison)DependencyGet(target, "m_comparison") == PerformanceDependency.Comparison.Exactly, ref count, "Copy comparison");
                Check((PerformanceDependency.Behaviour)DependencyGet(target, "m_onDependencyNotMet") == PerformanceDependency.Behaviour.Callback, ref count, "Copy behavior");
                Check(ReferenceEquals(DependencyGet(target, "m_triggeredEvent"), targetEvent), ref count, "Copy must retain destination event");
                Check(ReferenceEquals(Callback(target), targetCallback), ref count, "Copy must retain destination callback");
                Check(ReferenceEquals(DependencyGet(source, "m_requiredPerformanceAttribute"), attribute), ref count, "Source alias unchanged");
                Check((PerformanceDependency.Comparison)DependencyGet(source, "m_comparison") == PerformanceDependency.Comparison.Exactly, ref count, "Source comparison unchanged");
                Check((PerformanceDependency.Behaviour)DependencyGet(source, "m_onDependencyNotMet") == PerformanceDependency.Behaviour.Callback, ref count, "Source behavior unchanged");
                Check(ReferenceEquals(DependencyGet(source, "m_triggeredEvent"), sourceEvent), ref count, "Source event unchanged");
                Check(ReferenceEquals(Callback(source), sourceCallback), ref count, "Source callback unchanged");
                attribute.Level = PerformanceProfile.QualityLevel.Low;
                Check(((PerformanceAttribute)DependencyGet(target, "m_requiredPerformanceAttribute")).Level == PerformanceProfile.QualityLevel.Low, ref count, "Aliased later attribute edit is visible");
                target.Copy(target);
                Check(ReferenceEquals(DependencyGet(target, "m_requiredPerformanceAttribute"), attribute), ref count, "Self copy retains attribute");
                Check(ReferenceEquals(DependencyGet(target, "m_triggeredEvent"), targetEvent), ref count, "Self copy retains event");
                Check(ReferenceEquals(Callback(target), targetCallback), ref count, "Self copy retains callback");
                Check(Throws<NullReferenceException>(() => target.Copy(null)), ref count, "Null source faults before first assignment");
                Check(ReferenceEquals(DependencyGet(target, "m_requiredPerformanceAttribute"), attribute), ref count, "Null source retains destination attribute");
                Check((PerformanceDependency.Comparison)DependencyGet(target, "m_comparison") == PerformanceDependency.Comparison.Exactly, ref count, "Null source retains comparison");
                Check((PerformanceDependency.Behaviour)DependencyGet(target, "m_onDependencyNotMet") == PerformanceDependency.Behaviour.Callback, ref count, "Null source retains behavior");
                Check(ReferenceEquals(DependencyGet(target, "m_triggeredEvent"), targetEvent), ref count, "Null source retains event");
                Check(ReferenceEquals(Callback(target), targetCallback), ref count, "Null source retains callback");
                return count;
            }
            finally { registry.AssertUnchanged(); }
        }

        public static int RunDelegateAccessorsManaged18()
        {
            InitialiseOriginalReference();
            var registry = new RegistryObservation();
            int count = 0;
            try
            {
                PerformanceDependency owner = ManagedObject();
                var trace = new List<string>();
                Action<bool> a = value => trace.Add("A" + value), b = value => trace.Add("B" + value);
                Action<bool> c = value => trace.Add("C" + value), absent = value => { };
                Check(Callback(owner) == null, ref count, "Initial delegate null");
                owner.ValidateDependencyCallback += null;
                Check(Callback(owner) == null, ref count, "Adding null retains null");
                owner.ValidateDependencyCallback += a; owner.ValidateDependencyCallback += b; owner.ValidateDependencyCallback += a;
                Check(Length(Callback(owner)) == 3, ref count, "Duplicate callbacks are retained");
                Callback(owner)(false);
                Check(Trace(trace) == "AFalse,BFalse,AFalse", ref count, "Combine order");
                owner.ValidateDependencyCallback -= a;
                Check(Length(Callback(owner)) == 2, ref count, "Remove only the last equal callback");
                trace.Clear(); Callback(owner)(true);
                Check(Trace(trace) == "ATrue,BTrue", ref count, "Earlier duplicate survives");
                Action<bool> before = Callback(owner);
                owner.ValidateDependencyCallback -= absent;
                Check(ReferenceEquals(Callback(owner), before), ref count, "Absent remove retains delegate identity");
                owner.ValidateDependencyCallback -= null;
                Check(ReferenceEquals(Callback(owner), before), ref count, "Null remove retains delegate identity");
                owner.ValidateDependencyCallback += b;
                trace.Clear(); Callback(owner)(false);
                Check(Trace(trace) == "AFalse,BFalse,BFalse", ref count, "Second duplicate is appended");
                Check(Length(Callback(owner)) == 3, ref count, "Three callbacks after append");
                Action<bool> captured = Callback(owner);
                owner.ValidateDependencyCallback += c;
                trace.Clear(); captured(true);
                Check(Trace(trace) == "ATrue,BTrue,BTrue", ref count, "Captured invocation retains old list");
                trace.Clear(); Callback(owner)(true);
                Check(Trace(trace) == "ATrue,BTrue,BTrue,CTrue", ref count, "Fresh invocation sees appended callback");
                DependencySet(owner, "ValidateDependencyCallback", null);
                Action<bool> fault = value => { trace.Add("fault"); throw new InvalidOperationException("Owned delegate fault"); };
                owner.ValidateDependencyCallback += fault; owner.ValidateDependencyCallback += b;
                trace.Clear();
                Check(Throws<InvalidOperationException>(() => Callback(owner)(true)), ref count, "Callback exception propagates");
                Check(Trace(trace) == "fault", ref count, "Fault stops the remaining multicast prefix");
                owner.ValidateDependencyCallback -= fault;
                Check(ReferenceEquals(Callback(owner), b), ref count, "Remove fault restores surviving callback");
                trace.Clear(); Callback(owner)(false);
                Check(Trace(trace) == "BFalse", ref count, "Survivor still runs");
                owner.ValidateDependencyCallback -= b;
                Check(Callback(owner) == null, ref count, "Last remove returns null");
                Change(owner, null);
                Check(Callback(owner) == null, ref count, "Null profile returns before callback state");
                return count;
            }
            finally { registry.AssertUnchanged(); }
        }

        public static int RunComparisonsEngine44()
        {
            int count = 0;
            using (var owned = new OwnedObjects())
            {
                PerformanceDependency owner = owned.Dependency;
                Check(DependencyGet(owner, "m_requiredPerformanceAttribute") == null, ref count, "Original required attribute default");
                Check((PerformanceDependency.Comparison)DependencyGet(owner, "m_comparison") == PerformanceDependency.Comparison.GreaterThanOrEqual, ref count, "Original comparison default");
                Check((PerformanceDependency.Behaviour)DependencyGet(owner, "m_onDependencyNotMet") == PerformanceDependency.Behaviour.Hide, ref count, "Original Hide default");
                Check(Callback(owner) == null, ref count, "Original callback default");
                DependencySet(owner, "m_requiredPerformanceAttribute", owned.Required);
                DependencySet(owner, "m_onDependencyNotMet", PerformanceDependency.Behaviour.Callback);
                int calls = 0; bool actual = false;
                owner.ValidateDependencyCallback += value => { calls++; actual = value; };
                var levels = new[] { PerformanceProfile.QualityLevel.Low, PerformanceProfile.QualityLevel.Medium,
                    PerformanceProfile.QualityLevel.High, PerformanceProfile.QualityLevel.VeryHigh };
                bool[][] expected = { new[] { true, true, true, false }, new[] { false, false, false, true }, new[] { false, false, true, false } };
                for (int comparison = 0; comparison < 3; comparison++)
                    for (int level = 0; level < 4; level++)
                    {
                        owned.Required.Level = levels[level]; calls = 0;
                        DependencySet(owner, "m_comparison", (PerformanceDependency.Comparison)comparison);
                        Change(owner, owned.Profile);
                        Check(calls == 1, ref count, "Comparison must deliver one callback");
                        Check(actual == expected[comparison][level], ref count, "Authored High profile threshold/equality result");
                    }
                // A missing feature has the original Medium fallback: IsSupported
                // returns required > Medium, while IsExactlySupported returns == Medium.
                owned.Attributes.Clear();
                bool[][] missing = { new[] { false, true }, new[] { true, false }, new[] { true, false } };
                for (int comparison = 0; comparison < 3; comparison++)
                    for (int level = 0; level < 2; level++)
                    {
                        owned.Required.Level = level == 0 ? PerformanceProfile.QualityLevel.Medium : PerformanceProfile.QualityLevel.High;
                        calls = 0; DependencySet(owner, "m_comparison", (PerformanceDependency.Comparison)comparison);
                        Change(owner, owned.Profile);
                        Check(calls == 1, ref count, "Missing feature must still deliver a callback");
                        Check(actual == missing[comparison][level], ref count, "Original missing-feature fallback");
                    }
                calls = 0; Change(owner, null);
                Check(calls == 0, ref count, "Null profile is silent");
                DependencySet(owner, "m_requiredPerformanceAttribute", null); Change(owner, owned.Profile);
                Check(calls == 0, ref count, "Null required attribute is silent");
                DependencySet(owner, "m_requiredPerformanceAttribute", owned.Required);
                DependencySet(owner, "m_comparison", (PerformanceDependency.Comparison)99); Change(owner, owned.Profile);
                Check(calls == 0, ref count, "Unknown comparison is silent");
                DependencySet(owner, "m_comparison", PerformanceDependency.Comparison.GreaterThanOrEqual);
                DependencySet(owner, "m_onDependencyNotMet", (PerformanceDependency.Behaviour)99); Change(owner, owned.Profile);
                Check(calls == 0, ref count, "Unknown behavior is silent");
            }
            return count;
        }

        public static int RunCallbacksAndEventsEngine22()
        {
            int count = 0;
            using (var owned = new OwnedObjects())
            {
                PerformanceDependency owner = owned.Dependency;
                DependencySet(owner, "m_requiredPerformanceAttribute", owned.Required);
                var trace = new List<string>();
                Action<bool> a = value => trace.Add("A" + value), b = value => trace.Add("B" + value);
                DependencySet(owner, "m_onDependencyNotMet", PerformanceDependency.Behaviour.Callback);
                owner.ValidateDependencyCallback += a; owner.ValidateDependencyCallback += b;
                Change(owner, owned.Profile);
                Check(Trace(trace) == "ATrue,BTrue", ref count, "Profile callback order");
                owner.ValidateDependencyCallback -= a; owned.Required.Level = PerformanceProfile.QualityLevel.VeryHigh;
                trace.Clear(); Change(owner, owned.Profile);
                Check(Trace(trace) == "BFalse", ref count, "False validity is still dispatched");
                owner.ValidateDependencyCallback += b; trace.Clear(); Change(owner, owned.Profile);
                Check(Trace(trace) == "BFalse,BFalse", ref count, "Profile dispatch retains duplicate callbacks");
                DependencySet(owner, "ValidateDependencyCallback", null);
                Action<bool> fault = value => { trace.Add("fault"); throw new InvalidOperationException("Owned profile callback fault"); };
                owner.ValidateDependencyCallback += fault; owner.ValidateDependencyCallback += b; trace.Clear();
                Check(Throws<InvalidOperationException>(() => Change(owner, owned.Profile)), ref count, "Profile callback fault propagates");
                Check(Trace(trace) == "fault", ref count, "Profile callback fault prefix");
                Check((PerformanceDependency.Behaviour)DependencyGet(owner, "m_onDependencyNotMet") == PerformanceDependency.Behaviour.Callback, ref count, "Fault retains behavior");
                Check(ReferenceEquals(DependencyGet(owner, "m_requiredPerformanceAttribute"), owned.Required), ref count, "Fault retains required attribute");
                DependencySet(owner, "ValidateDependencyCallback", null); owned.Required.Level = PerformanceProfile.QualityLevel.High;
                bool nested = false;
                Action<bool> c = value => trace.Add("C" + value);
                Action<bool> reenter = value =>
                {
                    trace.Add("A" + value);
                    if (!nested)
                    {
                        nested = true; owner.ValidateDependencyCallback -= b; owner.ValidateDependencyCallback += c;
                        owned.Required.Level = PerformanceProfile.QualityLevel.VeryHigh; Change(owner, owned.Profile);
                    }
                };
                owner.ValidateDependencyCallback += reenter; owner.ValidateDependencyCallback += b;
                trace.Clear(); Change(owner, owned.Profile);
                Check(Trace(trace) == "ATrue,AFalse,CFalse,BTrue", ref count, "Reentry sees fresh callbacks; outer dispatch retains captured callbacks and validity");
                Check(Length(Callback(owner)) == 2, ref count, "Reentry changes future callback list");
                trace.Clear(); Change(owner, owned.Profile);
                Check(Trace(trace) == "AFalse,CFalse", ref count, "Later dispatch uses changed callback list");
                DependencySet(owner, "ValidateDependencyCallback", null);
                owner.ValidateDependencyCallback += value => { trace.Add("change"); DependencySet(owner, "m_onDependencyNotMet", PerformanceDependency.Behaviour.TriggerEvent); };
                owner.ValidateDependencyCallback += b;
                trace.Clear(); Change(owner, owned.Profile);
                Check(Trace(trace) == "change,BFalse", ref count, "Behavior mutation during callback leaves remaining captured callback");
                Check((PerformanceDependency.Behaviour)DependencyGet(owner, "m_onDependencyNotMet") == PerformanceDependency.Behaviour.TriggerEvent, ref count, "Changed behavior retained");
                int events = 0; var signal = new UnityEvent(); signal.AddListener(() => events++);
                DependencySet(owner, "m_triggeredEvent", signal); owned.Required.Level = PerformanceProfile.QualityLevel.High;
                Change(owner, owned.Profile);
                Check(events == 0, ref count, "Valid profile does not trigger UnityEvent");
                owned.Required.Level = PerformanceProfile.QualityLevel.VeryHigh; Change(owner, owned.Profile);
                Check(events == 1, ref count, "Invalid profile triggers UnityEvent");
                signal = new UnityEvent(); signal.AddListener(() => { trace.Add("event-fault"); throw new InvalidOperationException("Owned UnityEvent fault"); });
                signal.AddListener(() => trace.Add("event-after")); DependencySet(owner, "m_triggeredEvent", signal); trace.Clear();
                Check(Throws<InvalidOperationException>(() => Change(owner, owned.Profile)), ref count, "UnityEvent fault propagates");
                Check(Trace(trace) == "event-fault", ref count, "UnityEvent fault prefix stops later listener");
                DependencySet(owner, "m_triggeredEvent", null); Change(owner, owned.Profile);
                Check(Trace(trace) == "event-fault", ref count, "Null UnityEvent is silent");
                DependencySet(owner, "m_onDependencyNotMet", PerformanceDependency.Behaviour.Callback);
                DependencySet(owner, "ValidateDependencyCallback", b); trace.Clear(); owned.Required.Feature = null;
                Check(Throws<NullReferenceException>(() => Change(owner, owned.Profile)), ref count, "Genuine predicate faults on missing required feature");
                Check(trace.Count == 0, ref count, "Predicate fault precedes callback");
                Check((PerformanceDependency.Behaviour)DependencyGet(owner, "m_onDependencyNotMet") == PerformanceDependency.Behaviour.Callback, ref count, "Predicate fault retains behavior");
                Check(ReferenceEquals(DependencyGet(owner, "m_requiredPerformanceAttribute"), owned.Required), ref count, "Predicate fault retains attribute alias");
                Check(ReferenceEquals(Get(typeof(PerformanceProfile), "m_attributes", owned.Profile), owned.Attributes), ref count, "Predicate fault retains profile collection");
            }
            return count;
        }

        public static int RunProfileSubscriptionsEngine18()
        {
            int count = 0;
            using (var owned = new OwnedObjects())
            {
                PerformanceDependency owner = owned.Dependency; ScalablePerformance manager = owned.Manager;
                DependencySet(owner, "m_requiredPerformanceAttribute", owned.Required);
                var trace = new List<string>();
                Check(Get(typeof(ScalablePerformance), "OnProfileUpdated", manager) == null, ref count, "Owned inactive manager starts without listeners");
                Action<PerformanceProfile> prior = profile => trace.Add("prior");
                manager.OnProfileUpdated += prior;
                Set(typeof(ScalablePerformance), "m_activeProfile", manager, owned.Profile);
                DependencySet(owner, "m_onDependencyNotMet", PerformanceDependency.Behaviour.Callback);
                owner.ValidateDependencyCallback += value => trace.Add("D" + value);
                Call(owner, "ScalablePerformanceStartup", manager);
                Check(Trace(trace) == "DTrue", ref count, "Startup immediately validates active profile");
                Delegate listeners = (Delegate)Get(typeof(ScalablePerformance), "OnProfileUpdated", manager);
                Check(Length(listeners) == 2, ref count, "Startup appends profile listener");
                Check(ReferenceEquals(listeners.GetInvocationList()[0], prior), ref count, "Startup preserves earlier listener order");
                trace.Clear(); ((Action<PerformanceProfile>)Get(typeof(ScalablePerformance), "OnProfileUpdated", manager))(owned.Profile);
                Check(Trace(trace) == "prior,DTrue", ref count, "Captured real manager event dispatch includes dependency");
                trace.Clear(); Call(owner, "ScalablePerformanceStartup", manager);
                Check(Trace(trace) == "DTrue", ref count, "Repeated startup validates again");
                Check(Length((Delegate)Get(typeof(ScalablePerformance), "OnProfileUpdated", manager)) == 3, ref count, "Repeated startup retains duplicate subscription");
                Call(owner, "OnScalablePerformanceShutdown", manager);
                Check(Length((Delegate)Get(typeof(ScalablePerformance), "OnProfileUpdated", manager)) == 2, ref count, "Shutdown removes one matching subscription");
                trace.Clear(); ((Action<PerformanceProfile>)Get(typeof(ScalablePerformance), "OnProfileUpdated", manager))(owned.Profile);
                Check(Trace(trace) == "prior,DTrue", ref count, "One repeated-startup subscription remains");
                Call(owner, "OnScalablePerformanceShutdown", manager);
                Check(Length((Delegate)Get(typeof(ScalablePerformance), "OnProfileUpdated", manager)) == 1, ref count, "Second shutdown removes remaining subscription");
                Check(ReferenceEquals(Get(typeof(ScalablePerformance), "OnProfileUpdated", manager), prior), ref count, "Shutdown retains exact earlier delegate");
                Check(Throws<NullReferenceException>(() => Call(owner, "ScalablePerformanceStartup", (ScalablePerformance)null)), ref count, "Null startup faults at manager subscription");
                Check(ReferenceEquals(Get(typeof(ScalablePerformance), "OnProfileUpdated", manager), prior), ref count, "Null startup leaves real manager listeners untouched");
                DependencySet(owner, "ValidateDependencyCallback", (Action<bool>)(value => { trace.Add("fault"); throw new InvalidOperationException("Owned startup fault"); }));
                trace.Clear();
                Check(Throws<InvalidOperationException>(() => Call(owner, "ScalablePerformanceStartup", manager)), ref count, "Startup immediate callback fault propagates");
                Check(Trace(trace) == "fault", ref count, "Startup fault callback prefix");
                Check(Length((Delegate)Get(typeof(ScalablePerformance), "OnProfileUpdated", manager)) == 2, ref count, "Subscription remains after immediate validation fault");
                Call(owner, "OnScalablePerformanceShutdown", manager);
                Check(ReferenceEquals(Get(typeof(ScalablePerformance), "OnProfileUpdated", manager), prior), ref count, "Explicit shutdown removes fault-retained subscription");
                Check(ReferenceEquals(Get(typeof(ScalablePerformance), "m_activeProfile", manager), owned.Profile), ref count, "Startup/shutdown retain active profile reference");
            }
            return count;
        }

        private sealed class OwnedObjects : IDisposable
        {
            public GameObject GameObject;
            public PerformanceDependency Dependency;
            public ScalablePerformance Manager;
            public PerformanceProfile Profile;
            public ScalableFeature Feature;
            public PerformanceAttribute Required;
            public List<PerformanceAttribute> Attributes;
            private RegistryObservation registry;
            private Delegate priorManagerCallback;
            private object priorManagerProfiles;
            private Delegate priorProfileCallback;

            public OwnedObjects()
            {
                InitialiseOriginalReference();
                registry = new RegistryObservation();
                try
                {
                    GameObject = new GameObject("Owned original performance preservation");
                    GameObject.SetActive(false);
                    Dependency = GameObject.AddComponent<PerformanceDependency>();
                    Manager = GameObject.AddComponent<ScalablePerformance>();
                    priorManagerCallback = (Delegate)Get(typeof(ScalablePerformance), "OnProfileUpdated", Manager);
                    priorManagerProfiles = Get(typeof(ScalablePerformance), "m_performanceProfiles", Manager);
                    Profile = ScriptableObject.CreateInstance<PerformanceProfile>();
                    priorProfileCallback = (Delegate)Get(typeof(PerformanceProfile), "OnProfileUpdated", Profile);
                    Set(typeof(ScriptableObjectWithGuid), "m_guid", Profile, "owned-performance-preservation");
                    Feature = ScriptableObject.CreateInstance<ScalableFeature>(); Feature.ID = "owned-feature";
                    Attributes = (List<PerformanceAttribute>)Get(typeof(PerformanceProfile), "m_attributes", Profile);
                    Attributes.Add(new PerformanceAttribute { Feature = Feature, Level = PerformanceProfile.QualityLevel.High });
                    Required = new PerformanceAttribute { Feature = Feature, Level = PerformanceProfile.QualityLevel.High };
                    // Set by each test after the constructor-default observations.
                }
                catch { Dispose(); throw; }
            }

            public void Dispose()
            {
                if ((UObj)Dependency != null)
                {
                    DependencySet(Dependency, "ValidateDependencyCallback", null);
                    DependencySet(Dependency, "m_triggeredEvent", null);
                    DependencySet(Dependency, "m_requiredPerformanceAttribute", null);
                }
                if ((UObj)Manager != null)
                {
                    Set(typeof(ScalablePerformance), "OnProfileUpdated", Manager, priorManagerCallback);
                    Set(typeof(ScalablePerformance), "m_activeProfile", Manager, null);
                    Set(typeof(ScalablePerformance), "m_performanceProfiles", Manager, priorManagerProfiles);
                }
                if ((UObj)Profile != null) Set(typeof(PerformanceProfile), "OnProfileUpdated", Profile, priorProfileCallback);
                if (GameObject != null) UObj.DestroyImmediate(GameObject);
                if ((UObj)Feature != null) UObj.DestroyImmediate(Feature);
                if ((UObj)Profile != null) UObj.DestroyImmediate(Profile);
                if (registry != null) registry.AssertUnchanged();
            }
        }

        // Exact row/delegate/typed-reference identities are observed after the
        // original one-time static initialization. No registry state is reset.
        private sealed class RegistryObservation
        {
            private readonly IDictionary systems = (IDictionary)Field(typeof(ProcessManager), "s_systemDictionary").GetValue(null);
            private readonly IDictionary actions = (IDictionary)Field(typeof(ProcessManager), "s_systemActionLookup").GetValue(null);
            private readonly IList dispatch = (IList)Field(typeof(ProcessManager), "s_actionList").GetValue(null);
            private readonly object inProgress = Field(typeof(ProcessManager), "s_systemActionInProgress").GetValue(null);
            private readonly List<KeyValuePair<IDictionary, Dictionary<object, object>>> dictionaries = new List<KeyValuePair<IDictionary, Dictionary<object, object>>>();
            private readonly List<KeyValuePair<object, Dictionary<FieldInfo, object>>> references = new List<KeyValuePair<object, Dictionary<FieldInfo, object>>>();
            private readonly object[] priorDispatch;
            public RegistryObservation()
            {
                if ((bool)inProgress) throw new InvalidOperationException("Performance preservation cannot start inside ProcessManager dispatch");
                Snapshot(systems); Snapshot(actions);
                foreach (DictionaryEntry row in systems)
                {
                    object info = row.Value;
                    IDictionary typed = (IDictionary)info.GetType().GetField("SystemRefDictionary").GetValue(info);
                    Snapshot(typed); SnapshotReference(info.GetType().GetField("SystemRef").GetValue(info));
                    foreach (DictionaryEntry cached in typed) SnapshotReference(cached.Value);
                }
                foreach (DictionaryEntry row in actions) Snapshot((IDictionary)row.Value);
                priorDispatch = new object[dispatch.Count]; dispatch.CopyTo(priorDispatch, 0);
            }
            private void Snapshot(IDictionary dictionary)
            {
                var prior = new Dictionary<object, object>();
                foreach (DictionaryEntry row in dictionary) prior.Add(row.Key, row.Value);
                dictionaries.Add(new KeyValuePair<IDictionary, Dictionary<object, object>>(dictionary, prior));
            }
            private void SnapshotReference(object reference)
            {
                var prior = new Dictionary<FieldInfo, object>();
                for (Type type = reference.GetType(); type != null; type = type.BaseType)
                    foreach (FieldInfo field in type.GetFields(Instance | BindingFlags.DeclaredOnly)) prior.Add(field, field.GetValue(reference));
                references.Add(new KeyValuePair<object, Dictionary<FieldInfo, object>>(reference, prior));
            }
            public void AssertUnchanged()
            {
                if (!ReferenceEquals(systems, Field(typeof(ProcessManager), "s_systemDictionary").GetValue(null)) ||
                    !ReferenceEquals(actions, Field(typeof(ProcessManager), "s_systemActionLookup").GetValue(null)) ||
                    !ReferenceEquals(dispatch, Field(typeof(ProcessManager), "s_actionList").GetValue(null)) ||
                    !Equals(inProgress, Field(typeof(ProcessManager), "s_systemActionInProgress").GetValue(null)))
                    throw new InvalidOperationException("Performance preservation changed registry containers/dispatch flag");
                foreach (var pair in dictionaries)
                {
                    if (pair.Key.Count != pair.Value.Count) throw new InvalidOperationException("Performance preservation changed registry row count");
                    foreach (var row in pair.Value) if (!pair.Key.Contains(row.Key) || !ReferenceEquals(pair.Key[row.Key], row.Value))
                        throw new InvalidOperationException("Performance preservation changed registry row identity");
                }
                foreach (var reference in references) foreach (var field in reference.Value)
                    if (!ReferenceEquals(field.Value, field.Key.GetValue(reference.Key)) && !(field.Key.FieldType.IsValueType && Equals(field.Value, field.Key.GetValue(reference.Key))))
                        throw new InvalidOperationException("Performance preservation changed cached reference/delegate state");
                if (dispatch.Count != priorDispatch.Length) throw new InvalidOperationException("Performance preservation changed dispatch list");
                for (int i = 0; i < priorDispatch.Length; i++) if (!ReferenceEquals(dispatch[i], priorDispatch[i]))
                    throw new InvalidOperationException("Performance preservation changed dispatch callback identity");
            }
        }
    }
}
