#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    // Native-derived storage/condition boundaries on synthetic fixtures.
    // Passing does not establish the authored application graph or game startup.
    public static class FSMStorageConditionVerification
    {
        private static int checks;
        private static string Name() => "Lucid-condition-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);
        public static void Run()
        {
            checks = 0;
            VerifyStorage(); VerifyBooleanCheck(); VerifyExistsAndSet(); VerifyTimeout(); VerifyMetadata(); VerifyJSON();
            Debug.Log("Original FSM storage/Boolean/timeout source-based verification passed: " + checks + " checks. Authored startup remains unresolved.");
        }
        private sealed class RecordingStorage : IGraphStorage
        {
            public readonly FSMStorage Inner = new FSMStorage();
            public int Collections, Gets, Sets, Removes;
            public bool LastStoreDefault; public Type LastType; public GraphStorageKey LastKey;
            public object LastDefault, LastValue; public Action BeforeGet;
            public void Initialise() => Inner.Initialise(); public void Clear() => Inner.Clear();
            public IReadOnlyDictionary<GraphStorageKey, object> GetCollection() { ++Collections; return Inner.GetCollection(); }
            public T GetValue<T>(GraphStorageKey key, T defaultValue = default(T), bool storeDefault = true)
            {
                ++Gets; LastKey = key; LastType = typeof(T); LastStoreDefault = storeDefault; LastDefault = defaultValue;
                BeforeGet?.Invoke(); return Inner.GetValue(key, defaultValue, storeDefault);
            }
            public void SetValue<T>(GraphStorageKey key, T value)
            { ++Sets; LastKey = key; LastType = typeof(T); LastValue = value; Inner.SetValue(key, value); }
            public void RemoveValue<T>(GraphStorageKey key) { ++Removes; LastKey = key; LastType = typeof(T); Inner.RemoveValue<T>(key); }
            public void Raw(GraphStorageKey key, object value) => ((IDictionary<GraphStorageKey, object>)Inner.GetCollection())[key] = value;
        }
        private sealed class User : IGraphUser
        {
            public int Reads; public IGraphStorage First, Next;
            public IGraphStorage Storage { get { ++Reads; return Reads == 1 || Next == null ? First : Next; } }
            public User(IGraphStorage value) { First = value; }
            public void DestroyUser() { Storage.Clear(); }
        }
        private static void VerifyStorage()
        {
            var storage = new RecordingStorage(); var key = new GraphStorageKey(Name());
            Check(storage.GetValueOnly(key, 19) == 19 && !storage.LastStoreDefault && !storage.Inner.GetCollection().ContainsKey(key), "GetValueOnly forwards fallback without insertion");
            storage.FillValueOnly(key, out bool boolean, true);
            Check(boolean && !storage.LastStoreDefault && Equals(storage.LastDefault, true) && !storage.Inner.GetCollection().ContainsKey(key), "FillValueOnly forwards fallback without insertion");
            Check(!storage.TryGetValue<int>(key, out int integer) && integer == 0 && storage.Sets == 0, "missing typed retrieval assigns default without insertion");
            storage.Raw(key, new IGraphStorage.Var<bool>(false));
            Check(storage.HasValue<bool>(key) && storage.TryGetValue(key, out boolean) && !boolean, "typed presence means successful conversion, not truth");
            storage.Raw(key, true); Check(storage.TryGetValue(key, out boolean) && boolean, "raw boxed Boolean conversion");
            storage.Raw(key, null); Check(storage.HasValue(key) && storage.HasValue<string>(key) && storage.TryGetValue(key, out string text) && text == null, "present null reference remains present");
            boolean = true;
            Throws<NullReferenceException>(() => storage.TryGetValue(key, out boolean), "null unboxing to Boolean propagates");
            Check(boolean, "failed conversion leaves prior out slot unchanged");
            storage.Raw(key, "wrong type");
            Throws<InvalidCastException>(() => storage.HasValue<bool>(key), "typed presence propagates invalid cast");
            Check(storage.HasValue(key), "untyped presence never coerces a wrong type");
            Throws<NullReferenceException>(() => ((IGraphStorage)null).HasValue(key), "null storage has no presence repair");
            storage.Raw(key, new IGraphStorage.Var<float>(1.25f)); int sets = storage.Sets;
            Check(storage.AdjustValue(key, -0.5f, 8f) == 0.75f && storage.Sets == sets + 1 && !storage.LastStoreDefault && Equals(storage.LastDefault, 8f), "Single adjustment reads without insertion then writes result");
            storage.Inner.RemoveValue<float>(key); Check(storage.AdjustValue(key, 2f, 3f) == 5f && storage.Inner.GetValue<float>(key) == 5f, "Single adjustment stores fallback plus adjustment");
            storage.Raw(key, 7f);
            Throws<InvalidCastException>(() => storage.AdjustValue(key, 1f), "readable boxed Single fails the original wrapped-value write contract");
            Check(Equals(storage.Inner.GetCollection()[key], 7f), "failed adjustment write retains preceding raw Single");
            storage.BeforeGet = () => throw new InvalidOperationException("storage fixture"); sets = storage.Sets;
            Throws<InvalidOperationException>(() => storage.AdjustValue(key, 1f), "read errors escape adjustment");
            Check(storage.Sets == sets, "failed adjustment read does not write");
        }
        private static void VerifyBooleanCheck()
        {
            var machine = Machine(); var storage = new RecordingStorage(); var user = new User(storage); var key = new GraphStorageKey(Name());
            var truth = new FSMTransitionCheckBool(machine, Name(), key);
            Check(!truth.Update(user, default) && storage.Gets == 1 && !storage.LastStoreDefault && !storage.Inner.GetCollection().ContainsKey(key), "missing default Boolean is false without insertion");
            var falsehood = new FSMTransitionCheckBool(machine, Name(), key, FSMTransitionCheckBool.Comparison.FalseCausesTransition);
            Check(falsehood.Update(user, default), "missing false causes transition when presence is optional");
            var required = new FSMTransitionCheckBool(machine, Name(), key, FSMTransitionCheckBool.Comparison.FalseCausesTransition, onlyIfExists: true);
            int gets = storage.Gets; user.Reads = 0;
            Check(!required.Update(user, default) && user.Reads == 1 && storage.Gets == gets, "required missing value returns before fill and second getter");
            storage.Raw(key, new IGraphStorage.Var<bool>(false)); user.Reads = 0;
            Check(required.Update(user, new FSMUpdateContext(float.NaN, FSMUpdateType.LateUpdate)) && user.Reads == 2, "required present false converts then fills independently of context");
            var next = new RecordingStorage(); next.Raw(key, true); user.Reads = 0; user.Next = next;
            Check(!required.Update(user, default) && next.Gets == 1, "later fill re-reads the user's current storage");
            user.Next = null; storage.Raw(key, "wrong"); user.Reads = 0;
            Throws<InvalidCastException>(() => required.Update(user, default), "required wrong type fails during typed presence");
            Check(user.Reads == 1, "typed presence failure precedes second storage getter");
            var invalid = new FSMTransitionCheckBool(machine, Name(), key, (FSMTransitionCheckBool.Comparison)77);
            Throws<InvalidCastException>(() => invalid.Update(user, default), "invalid comparison still retrieves/converts first");
            storage.Raw(key, true); Check(!invalid.Update(user, default), "invalid comparison returns false after successful fill");
            truth.OnLeave(null, null); truth.OnFire(null, default, null); Check(true, "false removal flags avoid all user/storage access");
            var remove = new FSMTransitionCheckBool(machine, Name(), key, removeValueOnLeave: true, removeValueOnFire: true);
            remove.OnLeave(user, null); Check(storage.Removes == 1 && storage.LastType == typeof(bool) && !storage.Inner.GetCollection().ContainsKey(key), "leave removes Boolean key");
            storage.Raw(key, true); remove.OnFire(user, default, null);
            Check(storage.Removes == 2 && storage.LastType == typeof(bool) && !storage.Inner.GetCollection().ContainsKey(key), "fire removes Boolean key");
            Check(ReferenceEquals(machine.Transitions[remove.TransitionId], remove), "Boolean constructor registers owner and identity");
        }
        private static void VerifyExistsAndSet()
        {
            var machine = Machine(); var storage = new RecordingStorage(); var user = new User(storage); var key = new GraphStorageKey(Name(), Name(), Name());
            var exists = new FSMTransitionCheckExists(machine, Name(), key, false);
            var absent = new FSMTransitionCheckExists(machine, Name(), key, true);
            Check(!exists.Update(user, default) && absent.Update(user, default), "untyped existence/inversion on missing key");
            storage.Raw(key, null); Check(exists.Update(user, default) && !absent.Update(user, default), "untyped existence accepts present null");
            storage.Raw(key, 9); Check(exists.Update(user, default) && storage.Gets == 0, "untyped existence avoids value reads/conversion");
            var args = new FSMStateSetBool.JSONCtorArgs { Name = Name(), Node = Name(), FSM = Name(), Value = true };
            var state = new FSMStateSetBool(machine, Name(), args);
            GraphStorageKey expected = new GraphStorageKey(args.Name, args.Node, args.FSM);
            state.OnEnter(user, null);
            Check(storage.LastType == typeof(bool) && storage.LastKey.Equals(expected) && storage.Inner.GetValue<bool>(expected), "state entry writes retained authored key/Boolean");
            args.Name = Name(); args.Value = false; state.OnEnter(user, null);
            Check(storage.LastKey.Equals(expected) && storage.Inner.GetValue<bool>(expected), "constructor retains key/value independently of later DTO changes");
            int count = machine.States.Count;
            Throws<NullReferenceException>(() => new FSMStateSetBool(machine, Name(), null), "null direct DTO propagates after base construction");
            Check(machine.States.Count == count + 1, "SetBool null-DTO failure retains original state registration");
        }
        private static void VerifyTimeout()
        {
            var machine = Machine(); var storage = new RecordingStorage(); var user = new User(storage);
            var timeout = new FSMTransitionTimeout(machine, Name(), 2f);
            Check(timeout.VariableName.NameId == GraphNameLookup.ConvertNameToId("elapsedSeconds") && timeout.VariableName.NodeId == timeout.TransitionId && timeout.VariableName.GraphId == machine.FSMId, "timeout default variable uses exact transition/graph scope");
            foreach (string name in new[] { null, "" }) Check(new FSMTransitionTimeout(machine, Name(), 1f, name).VariableName.NameId == timeout.VariableName.NameId, "null/empty timeout variable uses original default");
            Check(new FSMTransitionTimeout(machine, Name(), 1f, " ").VariableName.NameId == GraphNameLookup.ConvertNameToId(" "), "whitespace timeout variable is retained");
            Check(timeout.GetElapsedSeconds(user) == 0f && storage.Sets == 1 && storage.LastKey.Equals(timeout.VariableName), "elapsed getter with zero adjustment still stores missing zero");
            timeout.SetElapsedSeconds(user, 3f); Check(timeout.GetElapsedSeconds(user, -2f) == 1f, "elapsed setter/adjustment retains raw negative values");
            Check(!timeout.Update(user, new FSMUpdateContext(0.5f, FSMUpdateType.FixedUpdate)) && timeout.Update(user, new FSMUpdateContext(0.5f, FSMUpdateType.LateUpdate)), "timeout >= exact threshold advances for every update type");
            Check(timeout.GetElapsedSeconds(user) == 2f, "timeout keeps elapsed value after firing decision");
            timeout.OnEnter(user, null); Check(timeout.GetElapsedSeconds(user) == 0f, "entry resets elapsed to zero");
            timeout.OnLeave(user, null); Check(storage.LastType == typeof(float) && !storage.Inner.GetCollection().ContainsKey(timeout.VariableName), "leave removes Single key");
            var negative = new FSMTransitionTimeout(machine, Name(), -1f);
            Check(negative.Update(user, default) && !negative.Update(user, new FSMUpdateContext(-2f)), "raw negative timeout/delta remain unclamped");
            var nan = new FSMTransitionTimeout(machine, Name(), float.NaN);
            Check(!nan.Update(user, new FSMUpdateContext(4f)) && nan.GetElapsedSeconds(user) == 4f, "NaN timeout is false after storing elapsed advance");
            timeout.SetElapsedSeconds(user, float.NaN);
            Check(!timeout.Update(user, new FSMUpdateContext(1f)) && float.IsNaN(timeout.GetElapsedSeconds(user)), "NaN elapsed remains stored and compares false");
        }
        private static void VerifyMetadata()
        {
            foreach (Type type in new[] { typeof(FSMStateSetBool), typeof(FSMTransitionCheckBool), typeof(FSMTransitionCheckExists), typeof(FSMTransitionTimeout) })
            {
                Check(type.GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format == "Core/{0}", "original storage condition menu format");
                Option[] expected = type == typeof(FSMStateSetBool) || type == typeof(FSMTransitionTimeout)
                    ? new[] { Option.ArrayBoundsChecks, Option.NullChecks } : new[] { Option.NullChecks, Option.ArrayBoundsChecks };
                var options = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
                Check(options.Select(x => x.Option).SequenceEqual(expected) && options.All(x => Equals(x.Value, false)), "original storage condition compiler options");
            }
            Type boolean = typeof(FSMTransitionCheckBool).GetNestedType("JSONCtorArgs", BindingFlags.NonPublic);
            Check(boolean.GetField("Compare").GetCustomAttribute<GraphEnumPopupAttribute>().EnumType == typeof(FSMTransitionCheckBool.Comparison)
                && boolean.GetCustomAttribute<GraphNodeDefaultNameAttribute>().DefaultName == "CheckBool", "original comparison authoring metadata");
            Check(typeof(FSMStateSetBool.JSONCtorArgs).GetCustomAttribute<JSONCtorArgsAttribute>() != null, "public SetBool DTO carries original factory marker");
        }
        [Serializable] private class BoolArgs { public string Name, Node, FSM, Compare; public bool RemoveValueOnLeave, OnlyIfExists, RemoveValueOnFire; }
        [Serializable] private class ExistsArgs { public string Name, Node, FSM; public bool Invert; }
        [Serializable] private class TimeoutArgs { public float TimeoutSeconds; public string VariableName; }
        private static void VerifyJSON()
        {
            var machine = Machine(); var args = new BoolArgs { Name = Name(), Node = Name(), FSM = Name(), Compare = "falsecausestransition", RemoveValueOnLeave = true, OnlyIfExists = true, RemoveValueOnFire = true };
            var node = (FSMTransitionCheckBool)FSMTransitionCheckBool.ConstructInstance(machine, Name(), JsonUtility.ToJson(args));
            Check(node.StorageKey.Equals(new GraphStorageKey(args.Name, args.Node, args.FSM)) && node.Compare == FSMTransitionCheckBool.Comparison.FalseCausesTransition && node.RemoveValueOnLeave && node.OnlyIfExists && node.RemoveValueOnFire, "Boolean JSON factory retains key/options and parses case-insensitively");
            var roundtrip = JsonUtility.FromJson<BoolArgs>(node.SerialiseRuntimeToJSON());
            Check(roundtrip.Name == args.Name && roundtrip.Node == args.Node && roundtrip.FSM == args.FSM && roundtrip.Compare == "FalseCausesTransition" && roundtrip.RemoveValueOnLeave && roundtrip.OnlyIfExists && roundtrip.RemoveValueOnFire, "Boolean JSON serializer uses name lookup/canonical enum text");
            args.Compare = "77"; node = (FSMTransitionCheckBool)FSMTransitionCheckBool.ConstructInstance(machine, Name(), JsonUtility.ToJson(args));
            Check((int)node.Compare == 77 && JsonUtility.FromJson<BoolArgs>(node.SerialiseRuntimeToJSON()).Compare == "77", "numeric unnamed comparison survives factory/serializer");
            int count = machine.Transitions.Count; args.Compare = "not-valid";
            Throws<ArgumentException>(() => FSMTransitionCheckBool.ConstructInstance(machine, Name(), JsonUtility.ToJson(args)), "invalid authored comparison propagates");
            Check(machine.Transitions.Count == count, "failed enum parse precedes registration");
            Throws<ArgumentException>(() => FSMTransitionCheckBool.ConstructInstance(machine, Name(), "{}"), "missing comparison has no fallback");
            Check(machine.Transitions.Count == count, "missing comparison fails before registration");
            var existsArgs = new ExistsArgs { Name = args.Name, Node = args.Node, FSM = args.FSM, Invert = true };
            var exists = (FSMTransitionCheckExists)FSMTransitionCheckExists.ConstructInstance(machine, Name(), JsonUtility.ToJson(existsArgs));
            var existsRoundtrip = JsonUtility.FromJson<ExistsArgs>(exists.SerialiseRuntimeToJSON());
            Check(exists.StorageKey.Equals(node.StorageKey) && exists.Invert && existsRoundtrip.Name == args.Name && existsRoundtrip.Node == args.Node && existsRoundtrip.FSM == args.FSM && existsRoundtrip.Invert, "Exists factory/string-key serializer");
            var timeout = (FSMTransitionTimeout)FSMTransitionTimeout.ConstructInstance(machine, Name(), "{}");
            var timeoutRoundtrip = JsonUtility.FromJson<TimeoutArgs>(timeout.SerialiseRuntimeToJSON());
            Check(timeout.TimeoutSeconds == 0f && timeoutRoundtrip.TimeoutSeconds == 0f && timeoutRoundtrip.VariableName == "elapsedSeconds", "omitted timeout fields retain zero/default variable");
            var set = (FSMStateSetBool)FSMStateSetBool.ConstructInstance(machine, Name(), "{}");
            var user = new FSMUser(); set.OnEnter(user, null); Check(user.Storage.GetValue<bool>(default(GraphStorageKey)) == false, "omitted SetBool fields retain global empty key/false");
            count = machine.States.Count;
            Throws<NullReferenceException>(() => FSMStateSetBool.ConstructInstance(machine, Name(), ""), "empty SetBool JSON retains null DTO constructor path");
            Check(machine.States.Count == count + 1, "empty SetBool JSON registers before DTO failure");
            count = machine.Transitions.Count;
            Throws<NullReferenceException>(() => FSMTransitionTimeout.ConstructInstance(machine, Name(), ""), "empty timeout JSON has no DTO repair");
            Check(machine.Transitions.Count == count, "empty timeout JSON fails before constructor registration");
        }
        private static void Check(bool condition, string reason) { ++checks; if (!condition) throw new InvalidOperationException("FSM storage condition verification failed: " + reason); }
        private static void Throws<T>(Action action, string reason) where T : Exception { try { action(); } catch (T) { Check(true, reason); return; } throw new InvalidOperationException("FSM storage condition verification failed: " + reason); }
    }
}
#endif
