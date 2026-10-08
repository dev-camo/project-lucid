using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;
using UObj = UnityEngine.Object;

namespace ProjectLucid.Verification
{
    // Original Core35 preservation observer. No Apply, Unity quality/time writes,
    // registry registration, ProcessManager dispatch or provider substitute.
    public static class OriginalVisualQualityPreservationVerification
    {
        private static FieldInfo Field(Type owner, string name) =>
            owner.GetField(name, BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic) ?? throw new InvalidOperationException(name);

        private static void Check(ref int count, bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            count++;
        }

        private static void Fault<T>(ref int count, Action action, string description) where T : Exception
        {
            try { action(); }
            catch (T) { count++; return; }
            throw new InvalidOperationException(description);
        }

        private static void Quality(VisualQualityConfiguration configuration, PerformanceProfile.QualityLevel value) =>
            Field(typeof(VisualQualityConfiguration), "m_qualityLevel").SetValue(configuration, value);

        private static T Value<T>(Type type, string name, object instance) =>
            (T)Field(type, name).GetValue(instance);

        private static Func<VisualQualityManager, T[], PerformanceProfile.QualityLevel, T> Selection<T>()
            where T : VisualQualityConfiguration
        {
            MethodInfo method = typeof(VisualQualityManager).GetMethod("GetConfiguration",
                BindingFlags.Instance | BindingFlags.NonPublic).MakeGenericMethod(typeof(T));
            return (Func<VisualQualityManager, T[], PerformanceProfile.QualityLevel, T>)
                Delegate.CreateDelegate(typeof(Func<VisualQualityManager, T[], PerformanceProfile.QualityLevel, T>), method);
        }

        public static int RunConstructorDefaults18()
        {
            int count = 0;
            var frame = new FramerateConfiguration();
            var scale = new ResolutionScaleConfiguration();
            var unity = new UnityQualityConfiguration();
            Check(ref count, Value<int>(typeof(FramerateConfiguration), "m_targetFramerate", frame) == 60, "frame default60");
            Check(ref count, Value<float>(typeof(ResolutionScaleConfiguration), "m_resolutionScale", scale) == 1f, "scale default1");
            Check(ref count, Value<string>(typeof(UnityQualityConfiguration), "m_unityQualitySetting", unity) == "Medium", "Unity default Medium");
            Check(ref count, frame.QualityLevel == PerformanceProfile.QualityLevel.Medium, "frame default quality");
            Check(ref count, scale.QualityLevel == PerformanceProfile.QualityLevel.Medium, "scale default quality");
            Check(ref count, unity.QualityLevel == PerformanceProfile.QualityLevel.Medium, "Unity default quality");
            var frame2 = new FramerateConfiguration();
            var scale2 = new ResolutionScaleConfiguration();
            var unity2 = new UnityQualityConfiguration();
            Check(ref count, !ReferenceEquals(frame, frame2), "frame distinct objects");
            Check(ref count, !ReferenceEquals(scale, scale2), "scale distinct objects");
            Check(ref count, !ReferenceEquals(unity, unity2), "Unity distinct objects");
            Field(typeof(FramerateConfiguration), "m_targetFramerate").SetValue(frame, 24);
            Field(typeof(ResolutionScaleConfiguration), "m_resolutionScale").SetValue(scale, .25f);
            Field(typeof(UnityQualityConfiguration), "m_unityQualitySetting").SetValue(unity, "Owned authored choice");
            Check(ref count, Value<int>(typeof(FramerateConfiguration), "m_targetFramerate", frame) == 24, "authored frame field");
            Check(ref count, Value<float>(typeof(ResolutionScaleConfiguration), "m_resolutionScale", scale) == .25f, "authored scale field");
            Check(ref count, Value<string>(typeof(UnityQualityConfiguration), "m_unityQualitySetting", unity) == "Owned authored choice", "authored Unity field");
            Check(ref count, Value<int>(typeof(FramerateConfiguration), "m_targetFramerate", frame2) == 60, "frame defaults independent");
            Check(ref count, Value<float>(typeof(ResolutionScaleConfiguration), "m_resolutionScale", scale2) == 1f, "scale defaults independent");
            Check(ref count, Value<string>(typeof(UnityQualityConfiguration), "m_unityQualitySetting", unity2) == "Medium", "Unity defaults independent");
            Quality(frame, PerformanceProfile.QualityLevel.Low);
            Quality(scale, PerformanceProfile.QualityLevel.Low);
            Quality(unity, PerformanceProfile.QualityLevel.Low);
            Check(ref count, frame.QualityLevel == PerformanceProfile.QualityLevel.Low, "frame quality direct read");
            Check(ref count, scale.QualityLevel == PerformanceProfile.QualityLevel.Low, "scale quality direct read");
            Check(ref count, unity.QualityLevel == PerformanceProfile.QualityLevel.Low, "Unity quality direct read");
            return count;
        }

        public static int RunConfigurationSelection20()
        {
            int count = 0;
            var first = new FramerateConfiguration();
            var match = new FramerateConfiguration();
            var duplicate = new FramerateConfiguration();
            Quality(first, PerformanceProfile.QualityLevel.Low);
            Quality(match, PerformanceProfile.QualityLevel.High);
            Quality(duplicate, PerformanceProfile.QualityLevel.High);
            var select = Selection<FramerateConfiguration>();
            var array = new[] { first, match, duplicate };
            // Only the original managed body is invoked. Its actual PE does not read
            // this; an open delegate with an explicit null receiver avoids creating
            // an invalid Unity wrapper. This is not a Unity instance/lifecycle proof.
            Check(ref count, ReferenceEquals(select(null, array, PerformanceProfile.QualityLevel.High), match), "first matching quality");
            Check(ref count, ReferenceEquals(select(null, array, PerformanceProfile.QualityLevel.Low), first), "first authored match");
            Check(ref count, ReferenceEquals(select(null, array, PerformanceProfile.QualityLevel.VeryLow), first), "unmatched first fallback");
            Check(ref count, ReferenceEquals(select(null, array, (PerformanceProfile.QualityLevel)777), first), "unknown quality first fallback");
            Check(ref count, ReferenceEquals(select(null, new[] { duplicate, match }, PerformanceProfile.QualityLevel.High), duplicate), "duplicate first order");
            Check(ref count, ReferenceEquals(select(null, new[] { match }, PerformanceProfile.QualityLevel.Low), match), "single unmatched fallback");
            Fault<NullReferenceException>(ref count, () => select(null, null, PerformanceProfile.QualityLevel.Medium), "null array faults before search");
            Fault<IndexOutOfRangeException>(ref count, () => select(null, new FramerateConfiguration[0], PerformanceProfile.QualityLevel.Medium), "empty array index0 before search");
            Fault<NullReferenceException>(ref count, () => select(null, new[] { null, match }, PerformanceProfile.QualityLevel.High), "null first entry is not filtered");
            Fault<NullReferenceException>(ref count, () => select(null, new[] { first, null, match }, PerformanceProfile.QualityLevel.High), "null middle before later match");
            Check(ref count, ReferenceEquals(select(null, new[] { match, null }, PerformanceProfile.QualityLevel.High), match), "successful prefix skips trailing null");
            Check(ref count, ReferenceEquals(select(null, new[] { first, null }, PerformanceProfile.QualityLevel.Low), first), "first match skips trailing null");
            Fault<NullReferenceException>(ref count, () => select(null, new[] { first, null }, (PerformanceProfile.QualityLevel)777), "unmatched scans trailing null");
            Fault<NullReferenceException>(ref count, () => select(null, new FramerateConfiguration[] { null }, PerformanceProfile.QualityLevel.Medium), "single null faults");
            Fault<NullReferenceException>(ref count, () => select(null, new FramerateConfiguration[] { null, null }, PerformanceProfile.QualityLevel.Medium), "all null faults");
            Quality(match, PerformanceProfile.QualityLevel.Low);
            Quality(first, PerformanceProfile.QualityLevel.VeryLow);
            Check(ref count, ReferenceEquals(select(null, array, PerformanceProfile.QualityLevel.Low), match), "live field change affects search");
            array[0] = duplicate;
            Check(ref count, ReferenceEquals(select(null, array, (PerformanceProfile.QualityLevel)777), duplicate), "live array first fallback");
            var scale1 = new ResolutionScaleConfiguration(); var scale2 = new ResolutionScaleConfiguration();
            Quality(scale2, PerformanceProfile.QualityLevel.High);
            Check(ref count, ReferenceEquals(Selection<ResolutionScaleConfiguration>()(null, new[] { scale1, scale2 }, PerformanceProfile.QualityLevel.High), scale2), "genuine resolution generic context");
            var unity1 = new UnityQualityConfiguration(); var unity2 = new UnityQualityConfiguration();
            Check(ref count, ReferenceEquals(Selection<UnityQualityConfiguration>()(null, new[] { unity1, unity2 }, PerformanceProfile.QualityLevel.VeryHigh), unity1), "genuine Unity quality generic fallback");
            Quality(first, PerformanceProfile.QualityLevel.VeryHigh);
            Check(ref count, ReferenceEquals(select(null, new[] { first, match }, PerformanceProfile.QualityLevel.VeryHigh), first), "subsequent direct quality read");
            return count;
        }

        private static int HandlerCount(PerformanceProfile profile, ScalablePerformance target)
        {
            Delegate handlers = Value<Delegate>(typeof(PerformanceProfile), "OnProfileUpdated", profile);
            int count = 0;
            if (handlers != null) foreach (Delegate handler in handlers.GetInvocationList())
                if (ReferenceEquals(handler.Target, target)) count++;
            return count;
        }

        private static int OuterHandlerCount(ScalablePerformance target) =>
            Value<Delegate>(typeof(ScalablePerformance), "OnProfileUpdated", target)?.GetInvocationList().Length ?? 0;

        private static void Edit(PerformanceProfile profile)
        {
            MethodInfo method = typeof(PerformanceProfile).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
            var edit = (Action<PerformanceProfile>)Delegate.CreateDelegate(typeof(Action<PerformanceProfile>), method);
            edit(profile);
        }

        public static int RunProfileIdentityEngine28()
        {
            int count = 0;
            using (var owned = new OwnedProfiles())
            {
                var manager = owned.Manager; var a = owned.A; var equal = owned.EqualA; var b = owned.B;
                Check(ref count, !owned.GameObject.activeSelf, "owned manager stays inactive");
                Check(ref count, ReferenceEquals(manager.ActiveProfile, null), "initial active CLR null");
                Check(ref count, ReferenceEquals(manager.PerformanceProfiles, null), "original list initially null");
                var profiles = new List<PerformanceProfile> { a };
                Field(typeof(ScalablePerformance), "m_performanceProfiles").SetValue(manager, profiles);
                Check(ref count, ReferenceEquals(manager.PerformanceProfiles, profiles), "live list identity");
                manager.AddPerformanceProfile(equal);
                Check(ref count, profiles.Count == 2, "append original list");
                Check(ref count, ReferenceEquals(profiles[1], equal), "append exact object");
                var notices = new List<PerformanceProfile>();
                manager.OnProfileUpdated += value => notices.Add(value);
                manager.SetPerformanceProfile(a);
                Check(ref count, ReferenceEquals(manager.ActiveProfile, a), "store active A");
                Check(ref count, notices.Count == 1, "A one event");
                Check(ref count, ReferenceEquals(notices[0], a), "A exact event argument");
                Check(ref count, HandlerCount(a, manager) == 1, "A one original edit subscriber");
                // The two live ScriptableObjects are distinct but have the same
                // authentic GUID field; original identity equality retains A.
                if ((UObj)a == (UObj)equal || a != equal) throw new InvalidOperationException("owned GUID equality setup");
                manager.SetPerformanceProfile(equal);
                Check(ref count, ReferenceEquals(manager.ActiveProfile, a), "equal GUID keeps first object");
                Check(ref count, notices.Count == 1, "equal GUID no event");
                Check(ref count, HandlerCount(equal, manager) == 0, "equal object never subscribed");
                manager.SetPerformanceProfile(b);
                Check(ref count, ReferenceEquals(manager.ActiveProfile, b), "store B");
                Check(ref count, notices.Count == 2, "B one additional event");
                Check(ref count, ReferenceEquals(notices[1], b), "B exact argument");
                Check(ref count, HandlerCount(a, manager) == 0, "unsubscribe old A");
                Check(ref count, HandlerCount(b, manager) == 1, "subscribe current B");
                Edit(b);
                Check(ref count, notices.Count == 2, "edit hits equal-profile early return");
                Check(ref count, ReferenceEquals(manager.ActiveProfile, b), "edit keeps current B");
                Check(ref count, ReferenceEquals(manager.GetPerformanceProfile("OwnedA"), a), "original name lookup");
                Check(ref count, ReferenceEquals(manager.GetPerformanceProfile("absent"), null), "missing name returns CLR null");
                profiles.Add(b);
                Check(ref count, manager.PerformanceProfiles.Count == 3, "external alias changes live list");
                Check(ref count, ReferenceEquals(manager.GetPerformanceProfile("OwnedB"), b), "lookup sees aliased append");
                manager.SetPerformanceProfile("OwnedA");
                Check(ref count, ReferenceEquals(manager.ActiveProfile, a), "name overload selects A");
                Check(ref count, notices.Count == 3, "name overload dispatches once");
                Check(ref count, ReferenceEquals(notices[2], a), "name exact event argument");
                Check(ref count, HandlerCount(b, manager) == 0, "name transition removes B subscriber");
            }
            return count;
        }

        public static int RunProfileReentryEngine24()
        {
            int count = 0;
            using (var owned = new OwnedProfiles())
            {
                var manager = owned.Manager; var a = owned.A; var b = owned.B;
                var trace = new List<string>(); bool entered = false;
                PerformanceProfile secondArgument = null, thirdArgument = null;
                Action<PerformanceProfile> second = value => { secondArgument = value; trace.Add("second:" + value.name); };
                Action<PerformanceProfile> third = value => { thirdArgument = value; trace.Add("third:" + value.name); };
                Action<PerformanceProfile> first = value =>
                {
                    trace.Add("first:" + value.name);
                    if (ReferenceEquals(value, a) && !entered)
                    {
                        entered = true;
                        manager.OnProfileUpdated -= second;
                        manager.OnProfileUpdated += third;
                        manager.SetPerformanceProfile(b);
                    }
                };
                manager.OnProfileUpdated += first; manager.OnProfileUpdated += second;
                manager.SetPerformanceProfile(a);
                Check(ref count, trace.Count == 4, "nested dispatch four callbacks");
                Check(ref count, trace[0] == "first:OwnedA", "outer first prefix");
                Check(ref count, trace[1] == "first:OwnedB", "nested first");
                Check(ref count, trace[2] == "third:OwnedB", "nested changed invocation list");
                Check(ref count, trace[3] == "second:OwnedA", "outer captured removed callback");
                Check(ref count, ReferenceEquals(manager.ActiveProfile, b), "reentry leaves active B");
                Check(ref count, HandlerCount(a, manager) == 0, "reentry removed A subscriber");
                Check(ref count, HandlerCount(b, manager) == 1, "reentry B one subscriber");
                Check(ref count, OuterHandlerCount(manager) == 2, "outer live handlers first and third");
                Check(ref count, ReferenceEquals(secondArgument, a), "outer argument stays captured A");
                Check(ref count, ReferenceEquals(thirdArgument, b), "nested argument B");
                manager.SetPerformanceProfile(b);
                Check(ref count, trace.Count == 4, "same B no event");
                Edit(b);
                Check(ref count, trace.Count == 4, "current edit no redispatch");
                Field(typeof(ScriptableObjectWithGuid), "m_guid").SetValue(a, "owned-B");
                manager.SetPerformanceProfile(a);
                Check(ref count, ReferenceEquals(manager.ActiveProfile, b), "live GUID change equal retains B");
                Check(ref count, trace.Count == 4, "live equal GUID no event");
                Field(typeof(ScriptableObjectWithGuid), "m_guid").SetValue(a, "owned-A");
                manager.SetPerformanceProfile(a);
                Check(ref count, trace.Count == 6, "later transition two current callbacks");
                Check(ref count, trace[4] == "first:OwnedA", "later first");
                Check(ref count, trace[5] == "third:OwnedA", "later third");
                Check(ref count, ReferenceEquals(manager.ActiveProfile, a), "later active A");
                Check(ref count, HandlerCount(a, manager) == 1, "later A one subscriber");
                Check(ref count, HandlerCount(b, manager) == 0, "later B subscriber removed");
                Check(ref count, ReferenceEquals(Value<Delegate>(typeof(ScalablePerformance), "OnProfileUpdated", manager).GetInvocationList()[0], first), "first handler identity preserved");
                Edit(a);
                Check(ref count, trace.Count == 6, "A edit equality no event");
                Check(ref count, !owned.GameObject.activeSelf, "reentry never activated GameObject");
            }
            return count;
        }

        public static int RunProfileFaultPrefixesEngine16()
        {
            int count = 0;
            using (var owned = new OwnedProfiles())
            {
                var manager = owned.Manager; var a = owned.A; var b = owned.B;
                manager.SetPerformanceProfile((PerformanceProfile)null);
                Check(ref count, ReferenceEquals(manager.ActiveProfile, null), "initial null equality return");
                Check(ref count, OuterHandlerCount(manager) == 0, "initial null no subscribers");
                Fault<NullReferenceException>(ref count, () => manager.AddPerformanceProfile(null), "original null list add fault");
                Check(ref count, ReferenceEquals(manager.PerformanceProfiles, null), "failed append keeps list null");
                Fault<NullReferenceException>(ref count, () => manager.GetPerformanceProfile("x"), "original null list search fault");
                int firstCount = 0, laterCount = 0;
                var sentinel = new InvalidOperationException("Owned callback sentinel");
                Action<PerformanceProfile> first = value => { firstCount++; throw sentinel; };
                Action<PerformanceProfile> later = value => laterCount++;
                manager.OnProfileUpdated += first; manager.OnProfileUpdated += later;
                Exception observed = null;
                try { manager.SetPerformanceProfile(a); } catch (Exception error) { observed = error; }
                Check(ref count, ReferenceEquals(observed, sentinel), "propagates exact callback exception");
                Check(ref count, ReferenceEquals(manager.ActiveProfile, a), "callback fault retains preceding active store");
                Check(ref count, HandlerCount(a, manager) == 1, "callback fault retains original edit subscription");
                Check(ref count, firstCount == 1, "first callback once");
                Check(ref count, laterCount == 0, "callback fault stops later callback");
                manager.OnProfileUpdated -= first; manager.OnProfileUpdated -= later;
                Fault<NullReferenceException>(ref count, () => manager.SetPerformanceProfile((PerformanceProfile)null), "null transition faults after store and removal");
                Check(ref count, ReferenceEquals(manager.ActiveProfile, null), "null fault leaves CLR null active field");
                Check(ref count, HandlerCount(a, manager) == 0, "null fault already removed A handler");
                manager.SetPerformanceProfile((PerformanceProfile)null);
                Check(ref count, ReferenceEquals(manager.ActiveProfile, null), "repeated null early return");
                manager.SetPerformanceProfile(b);
                Check(ref count, ReferenceEquals(manager.ActiveProfile, b), "subsequent B transition works");
                Check(ref count, HandlerCount(b, manager) == 1, "subsequent B subscription exactly once");
            }
            return count;
        }

        // Only owned inactive GameObjects and owned profiles are touched. Scalable
        // has no SystemRef initializer; Awake is never invoked. Registry rows,
        // cached references and delegates must remain identical after destruction.
        private sealed class OwnedProfiles : IDisposable
        {
            public GameObject GameObject;
            public ScalablePerformance Manager;
            public PerformanceProfile A, EqualA, B;
            private readonly List<PerformanceProfile> profiles = new List<PerformanceProfile>();
            private readonly List<Delegate> originalProfileDelegates = new List<Delegate>();
            private readonly RegistryObservation registry = new RegistryObservation();
            private Delegate originalManagerDelegate;
            private object originalManagerList;

            public OwnedProfiles()
            {
                try
                {
                    GameObject = new GameObject("Owned Core35 preservation");
                    GameObject.SetActive(false);
                    Manager = GameObject.AddComponent<ScalablePerformance>();
                    originalManagerDelegate = Value<Delegate>(typeof(ScalablePerformance), "OnProfileUpdated", Manager);
                    originalManagerList = Field(typeof(ScalablePerformance), "m_performanceProfiles").GetValue(Manager);
                    A = NewProfile("OwnedA", "owned-A");
                    EqualA = NewProfile("OwnedEqualA", "owned-A");
                    B = NewProfile("OwnedB", "owned-B");
                }
                catch { Dispose(); throw; }
            }

            private PerformanceProfile NewProfile(string name, string guid)
            {
                PerformanceProfile profile = ScriptableObject.CreateInstance<PerformanceProfile>();
                profiles.Add(profile);
                originalProfileDelegates.Add(Value<Delegate>(typeof(PerformanceProfile), "OnProfileUpdated", profile));
                Field(typeof(ScriptableObjectWithGuid), "m_guid").SetValue(profile, guid);
                profile.name = name;
                return profile;
            }

            public void Dispose()
            {
                if ((UObj)Manager != null)
                {
                    // Detach every owned callback before Unity may call OnDestroy.
                    // No callback on a preexisting/global provider was subscribed.
                    Field(typeof(ScalablePerformance), "OnProfileUpdated").SetValue(Manager, originalManagerDelegate);
                    Field(typeof(ScalablePerformance), "m_activeProfile").SetValue(Manager, null);
                    Field(typeof(ScalablePerformance), "m_performanceProfiles").SetValue(Manager, originalManagerList);
                }
                for (int i = 0; i < profiles.Count; i++) if ((UObj)profiles[i] != null)
                    Field(typeof(PerformanceProfile), "OnProfileUpdated").SetValue(profiles[i], originalProfileDelegates[i]);
                if (GameObject != null) UObj.DestroyImmediate(GameObject);
                for (int i = profiles.Count - 1; i >= 0; i--) if ((UObj)profiles[i] != null) UObj.DestroyImmediate(profiles[i]);
                registry.AssertUnchanged();
            }
        }

        private sealed class RegistryObservation
        {
            private readonly IDictionary systems = (IDictionary)Field(typeof(ProcessManager), "s_systemDictionary").GetValue(null);
            private readonly IDictionary actions = (IDictionary)Field(typeof(ProcessManager), "s_systemActionLookup").GetValue(null);
            private readonly IList dispatch = (IList)Field(typeof(ProcessManager), "s_actionList").GetValue(null);
            private readonly object inProgress = Field(typeof(ProcessManager), "s_systemActionInProgress").GetValue(null);
            private readonly List<KeyValuePair<IDictionary, Dictionary<object, object>>> dictionaries =
                new List<KeyValuePair<IDictionary, Dictionary<object, object>>>();
            private readonly List<KeyValuePair<object, Dictionary<FieldInfo, object>>> referenceFields =
                new List<KeyValuePair<object, Dictionary<FieldInfo, object>>>();
            private readonly object[] dispatchBefore;

            public RegistryObservation()
            {
                if ((bool)inProgress) throw new InvalidOperationException("Core35 fixture cannot start inside ProcessManager dispatch");
                Snapshot(systems); Snapshot(actions);
                foreach (DictionaryEntry row in systems)
                {
                    object info = row.Value;
                    IDictionary typed = (IDictionary)info.GetType().GetField("SystemRefDictionary").GetValue(info);
                    Snapshot(typed);
                    SnapshotReference(info.GetType().GetField("SystemRef").GetValue(info));
                    foreach (DictionaryEntry cached in typed) SnapshotReference(cached.Value);
                }
                foreach (DictionaryEntry row in actions) Snapshot((IDictionary)row.Value);
                dispatchBefore = new object[dispatch.Count]; dispatch.CopyTo(dispatchBefore, 0);
            }

            private void Snapshot(IDictionary dictionary)
            {
                var before = new Dictionary<object, object>();
                foreach (DictionaryEntry row in dictionary) before.Add(row.Key, row.Value);
                dictionaries.Add(new KeyValuePair<IDictionary, Dictionary<object, object>>(dictionary, before));
            }

            private void SnapshotReference(object reference)
            {
                var before = new Dictionary<FieldInfo, object>();
                for (Type type = reference.GetType(); type != null; type = type.BaseType)
                    foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) before.Add(field, field.GetValue(reference));
                referenceFields.Add(new KeyValuePair<object, Dictionary<FieldInfo, object>>(reference, before));
            }

            public void AssertUnchanged()
            {
                if (!ReferenceEquals(systems, Field(typeof(ProcessManager), "s_systemDictionary").GetValue(null)) ||
                    !ReferenceEquals(actions, Field(typeof(ProcessManager), "s_systemActionLookup").GetValue(null)) ||
                    !ReferenceEquals(dispatch, Field(typeof(ProcessManager), "s_actionList").GetValue(null)) ||
                    !Equals(inProgress, Field(typeof(ProcessManager), "s_systemActionInProgress").GetValue(null)))
                    throw new InvalidOperationException("Core35 fixture changed registry containers or dispatch flag");
                foreach (var pair in dictionaries)
                {
                    if (pair.Key.Count != pair.Value.Count) throw new InvalidOperationException("Core35 fixture changed registry row count");
                    foreach (var row in pair.Value) if (!pair.Key.Contains(row.Key) || !ReferenceEquals(pair.Key[row.Key], row.Value))
                        throw new InvalidOperationException("Core35 fixture changed registry row identity");
                }
                foreach (var reference in referenceFields) foreach (var field in reference.Value)
                    if (!ReferenceEquals(field.Value, field.Key.GetValue(reference.Key)) &&
                        !(field.Key.FieldType.IsValueType && Equals(field.Value, field.Key.GetValue(reference.Key))))
                        throw new InvalidOperationException("Core35 fixture changed an existing SystemRef field");
                if (dispatch.Count != dispatchBefore.Length) throw new InvalidOperationException("Core35 fixture changed dispatch list");
                for (int i = 0; i < dispatchBefore.Length; i++) if (!ReferenceEquals(dispatch[i], dispatchBefore[i]))
                    throw new InvalidOperationException("Core35 fixture changed dispatch delegate identity");
            }
        }
    }
}
