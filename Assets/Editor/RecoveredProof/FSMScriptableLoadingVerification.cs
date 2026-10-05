#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    // Synthetic graph fixtures exercise recovered original loading boundaries.
    // These checks do not establish all authored states or game startup behavior.
    public static class FSMScriptableLoadingVerification
    {
        private static readonly List<FiniteStateMachineScriptableObject> Objects = new List<FiniteStateMachineScriptableObject>();
        private static readonly HashSet<string> Names = new HashSet<string>();
        public static void Run()
        {
            string directory = Path.Combine(Application.temporaryCachePath, "Lucid-fsm-scriptable-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                VerifyDefaultsAndJSON(directory);
                VerifyProcessRetention();
                VerifyAcquireAndRelease();
                VerifyLoaderOrdering();
                VerifyLoaderErrorsAndReentry();
            }
            finally
            {
                foreach (var item in Objects) if (item != null) UnityEngine.Object.DestroyImmediate(item);
                Objects.Clear();
                var manager = FSMManager.GetManager();
                foreach (string name in Names) while (manager.FSMExists(name)) manager.ReleaseFSM(name);
                Names.Clear(); Directory.Delete(directory, true);
            }
            Debug.Log("Original FSM ScriptableObject selection/acquisition and authored-loader boundary source-based verification passed. Concrete authored states and full game startup remain unresolved.");
        }
        private static FiniteStateMachineScriptableObject Asset()
        {
            var asset = ScriptableObject.CreateInstance<FiniteStateMachineScriptableObject>();
            asset.name = "Lucid FSM loading proof"; Objects.Add(asset); return asset;
        }
        private static string NewName() { string name = "Lucid-scriptable-" + Guid.NewGuid().ToString("N"); Names.Add(name); return name; }
        private static FiniteStateMachine Machine() => new FiniteStateMachine(NewName());
        private static void Configure(FiniteStateMachineScriptableObject asset)
        {
            string name = NewName();
            Set(asset, "m_name", name);
            Set(asset, "m_embeddedJSON", JsonUtility.ToJson(new FSMClassFactory.JSONMultipleFSMs
            {
                FSMs = new List<FSMClassFactory.JSONFiniteStateMachineClass> { new FSMClassFactory.JSONFiniteStateMachineClass
                {
                    Name = name, DefaultState = "idle",
                    States = new List<FSMClassFactory.JSONStateClass> { new FSMClassFactory.JSONStateClass { Class = "FSMState", Name = "idle" } },
                    Transitions = new List<FSMClassFactory.JSONTransitionClass>()
                } }
            }));
        }
        private static void VerifyDefaultsAndJSON(string directory)
        {
            var asset = Asset();
            Check(Get<bool>(asset, "m_releaseOnDestroy") && Get<List<ScriptableObject>>(asset, "m_referencedScriptableObjects").Count == 0 && Get<int>(asset, "m_refCount") == 0 && asset.FSM == null, "original constructor defaults");
            Check(asset.GetJSON() == null && asset.GetFSMByName(null) == null, "unset JSON and dictionary return null");
            Set(asset, "m_embeddedJSON", "  data  "); Check(asset.GetJSON() == "  data  ", "embedded content retains whitespace");
            Set(asset, "m_embeddedJSON", " \t "); Set(asset, "m_relativePathToJSON", " \r ");
            Check(asset.GetJSON() == null, "GetJSON ignores whitespace-only sources");
            string path = Path.Combine(directory, "direct.json"); File.WriteAllText(path, "file content"); Set(asset, "m_relativePathToJSON", path);
            Check(asset.GetJSON() == "file content", "direct reader uses original Path.Combine absolute path behavior");
            Set(asset, "m_relativePathToJSON", Path.Combine(directory, "absent.json"));
            Throws<FileNotFoundException>(() => asset.GetJSON(), "direct reader error propagates");
        }
        private static void VerifyProcessRetention()
        {
            var asset = Asset(); var old = Machine(); var next = Machine();
            var oldMap = new Dictionary<string, FiniteStateMachine> { { "old", old } };
            Set(asset, "m_fsm", old); Set(asset, "m_fsmDictionary", oldMap); Set(asset, "m_name", "next");
            var newMap = new Dictionary<string, FiniteStateMachine> { { "next", next } };
            Process(asset, newMap, new List<string> { "original-error-presence" });
            Check(ReferenceEquals(Get<Dictionary<string, FiniteStateMachine>>(asset, "m_fsmDictionary"), oldMap) && ReferenceEquals(asset.FSM, old), "error presence retains prior dictionary and selection");
            var empty = new Dictionary<string, FiniteStateMachine>(); Process(asset, empty, null);
            Check(ReferenceEquals(Get<Dictionary<string, FiniteStateMachine>>(asset, "m_fsmDictionary"), empty) && ReferenceEquals(asset.FSM, old), "empty successful dictionary retains old selection");
            Set(asset, "m_name", ""); Process(asset, newMap, new List<string>());
            Check(ReferenceEquals(Get<Dictionary<string, FiniteStateMachine>>(asset, "m_fsmDictionary"), newMap) && ReferenceEquals(asset.FSM, old), "empty name accepts dictionary but retains selection");
            Set(asset, "m_name", "missing"); Process(asset, newMap, null);
            Check(asset.FSM == null && ReferenceEquals(asset.GetFSMByName("next"), next) && asset.GetFSMByName("absent") == null, "missing selected key clears selection while raw dictionary lookup works");
            Throws<ArgumentNullException>(() => asset.GetFSMByName(null), "null key reaches populated dictionary");
            Set(asset, "m_name", "next"); Process(asset, newMap, null);
            Check(ReferenceEquals(asset.FSM, next), "selected machine retrieved from accepted dictionary");
            Throws<NullReferenceException>(() => Process(asset, null, null), "null success dictionary throws after assignment");
            Check(Get<Dictionary<string, FiniteStateMachine>>(asset, "m_fsmDictionary") == null && ReferenceEquals(asset.FSM, next), "null dictionary exception retains preceding dictionary assignment and old selected FSM");
        }
        private static void VerifyAcquireAndRelease()
        {
            var asset = Asset(); Configure(asset); int callbacks = 0;
            asset.OnInitialisationComplete = value => { Check(ReferenceEquals(value, asset), "callback owner"); ++callbacks; };
            IEnumerator acquisition = asset.AcquireFSM();
            Check(Get<int>(asset, "m_refCount") == 0, "acquisition deferred until first MoveNext");
            Drain(acquisition);
            Check(callbacks == 1 && Get<int>(asset, "m_refCount") == 1 && asset.FSM != null && asset.FSM.DefaultState != null, "embedded graph creates machine and then invokes initialization callback");
            FiniteStateMachine original = asset.FSM;
            Drain(asset.AcquireFSM());
            Check(callbacks == 1 && Get<int>(asset, "m_refCount") == 2 && ReferenceEquals(asset.FSM, original), "later valid acquire skips callback and construction");
            asset.ReleaseFSM(); Check(FSMManager.GetManager().FSMExists(original.FSMId) && Get<int>(asset, "m_refCount") == 1, "non-final release retains manager machine");
            asset.ReleaseFSM(); Check(!FSMManager.GetManager().FSMExists(original.FSMId) && Get<int>(asset, "m_refCount") == 0 && ReferenceEquals(asset.FSM, original), "final release removes manager entry but retains selected FSM and dictionary");
            Drain(asset.AcquireFSM()); Check(callbacks == 2 && !ReferenceEquals(asset.FSM, original), "count-one acquisition reloads retained selection after release");
            Set(asset, "m_releaseOnDestroy", false); FiniteStateMachine retained = asset.FSM;
            asset.ReleaseFSM(); Check(FSMManager.GetManager().FSMExists(retained.FSMId) && Get<int>(asset, "m_refCount") == 0, "release flag disables manager release only");
            asset.ReleaseFSM(); Check(Get<int>(asset, "m_refCount") == -1, "release count underflows without guard");

            var errors = Asset(); Set(errors, "m_embeddedJSON", "{not-json"); int failures = 0;
            errors.OnInitialisationComplete = _ => ++failures; Drain(errors.AcquireFSM()); Drain(errors.AcquireFSM());
            Check(failures == 2 && errors.FSM == null && Get<int>(errors, "m_refCount") == 2, "failed acquisition still calls completion and later retries when selection is null");
            Set(errors, "m_embeddedJSON", " \t "); Check(errors.GetJSON() == null, "GetJSON whitespace precondition");
            IEnumerator whitespace = errors.AcquireFSM(); Check(whitespace.MoveNext() && whitespace.Current is IEnumerator, "Acquire chooses nonempty whitespace embedded data rather than GetJSON filtering");
            int observed = 0; errors.OnInitialisationComplete = _ => ++observed;
            ((IDisposable)whitespace).Dispose();
            Check(!whitespace.MoveNext() && observed == 1 && Get<int>(errors, "m_refCount") == 3, "original no-op iterator Dispose retains resumed callback and count");
            IEnumerator throwing = errors.AcquireFSM(); Check(throwing.MoveNext(), "throwing callback first yield");
            errors.OnInitialisationComplete = _ => throw new InvalidOperationException("callback fixture");
            Throws<InvalidOperationException>(() => throwing.MoveNext(), "completion callback error propagates");
            Check(!throwing.MoveNext() && Get<int>(errors, "m_refCount") == 4, "callback error leaves iterator completed and reference count incremented");
        }
        private static void VerifyLoaderOrdering()
        {
            var first = Asset(); var second = Asset(); Configure(first); Configure(second);
            int enumerations = 0, sourceDisposed = 0; var trace = new List<string>();
            IEnumerable<FiniteStateMachineScriptableObject> Input()
            {
                ++enumerations;
                try { yield return first; yield return first; yield return second; }
                finally { ++sourceDisposed; }
            }
            var loader = new FSMStateLoader(Input(), () => trace.Add("all"));
            first.OnInitialisationComplete = _ => trace.Add("first"); second.OnInitialisationComplete = _ => trace.Add("second");
            Check(enumerations == 0 && loader.LoadedStateMachines.Count == 0, "loader constructor defers input enumeration");
            IEnumerator iterator = LoadIterator(loader); Check(iterator.MoveNext(), "loader yields first acquisition");
            Check(enumerations == 1 && sourceDisposed == 1 && Get<List<FiniteStateMachineScriptableObject>>(loader, "m_loadingStateMachines").Count == 2, "loader gathers unique unresolved entries and disposes source before load yield");
            Drain((IEnumerator)iterator.Current); while (iterator.MoveNext()) if (iterator.Current is IEnumerator nested) Drain(nested);
            Check(trace.SequenceEqual(new[] { "second", "first", "all" }) && loader.LoadedStateMachines.SequenceEqual(new[] { second, first }), "original loader loads unique entries in reverse order then completes");
            Check(Get<List<FiniteStateMachineScriptableObject>>(loader, "m_loadingStateMachines").Count == 0, "completion drains pending list");
            var alreadyLoaded = new FSMStateLoader(new[] { first, second }, () => trace.Add("already")); Drain(LoadIterator(alreadyLoaded));
            Check(alreadyLoaded.LoadedStateMachines.Count == 0 && trace.Last() == "already" && Get<int>(first, "m_refCount") == 1 && Get<int>(second, "m_refCount") == 1, "already selected input is skipped without loaded-list entry or acquire");
        }
        private static void VerifyLoaderErrorsAndReentry()
        {
            int emptyCallbacks = 0; var empty = new FSMStateLoader(new FiniteStateMachineScriptableObject[0], () => ++emptyCallbacks); Drain(LoadIterator(empty));
            Check(emptyCallbacks == 1, "empty loader immediately completes");
            Throws<NullReferenceException>(() => Drain(LoadIterator(new FSMStateLoader(null, null))), "null source enumeration propagates");
            Throws<NullReferenceException>(() => Drain(LoadIterator(new FSMStateLoader(new FiniteStateMachineScriptableObject[] { null }, null))), "null source entry propagates");
            var failed = Asset(); Set(failed, "m_embeddedJSON", "{not-json"); int callbacks = 0;
            var loader = new FSMStateLoader(new[] { failed }, () => ++callbacks); Drain(LoadIterator(loader));
            Check(callbacks == 1 && failed.FSM == null && loader.LoadedStateMachines.Count == 1 && ReferenceEquals(loader.LoadedStateMachines[0], failed), "factory errors still follow original completion and loaded-list callback route");
            Drain(LoadIterator(loader));
            Check(callbacks == 2 && loader.LoadedStateMachines.Count == 1 && Get<int>(failed, "m_refCount") == 2, "reentry retries unresolved asset while loaded list stays unique");
            var manual = Asset(); var immediate = new FSMStateLoader(new FiniteStateMachineScriptableObject[0], () => ++callbacks);
            Loaded(immediate, manual); Loaded(immediate, manual);
            Check(immediate.LoadedStateMachines.Count == 1 && callbacks == 4, "direct completion repeats zero-pending callback but retains unique loaded entry");
        }
        private static void Set(object value, string field, object assigned) => value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, assigned);
        private static T Get<T>(object value, string member)
        {
            FieldInfo field = value.GetType().GetField(member, BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? (T)field.GetValue(value) : (T)value.GetType().GetProperty(member, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        }
        private static void Process(FiniteStateMachineScriptableObject asset, Dictionary<string, FiniteStateMachine> dictionary, List<string> errors) => Invoke(asset, "ProcessFSM", dictionary, errors);
        private static void Loaded(FSMStateLoader loader, FiniteStateMachineScriptableObject asset) => Invoke(loader, "OnStateMachineLoaded", asset);
        private static IEnumerator LoadIterator(FSMStateLoader loader) => (IEnumerator)Invoke(loader, "LoadStatesCoroutine");
        private static object Invoke(object value, string method, params object[] args)
        {
            try { return value.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args); }
            catch (TargetInvocationException exception) { throw exception.InnerException; }
        }
        private static void Drain(IEnumerator iterator) { try { while (iterator.MoveNext()) if (iterator.Current is IEnumerator nested) Drain(nested); } finally { (iterator as IDisposable)?.Dispose(); } }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("FSM ScriptableObject verification failed: " + message); }
        private static void Throws<T>(Action action, string message) where T : Exception { try { action(); } catch (T) { return; } throw new InvalidOperationException("FSM ScriptableObject verification failed: " + message); }
    }
}
#endif
