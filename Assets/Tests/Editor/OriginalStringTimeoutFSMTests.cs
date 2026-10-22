using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ProjectLucid.Tests
{
    // Rebuilt managed/Unity behaviour of the HLUnityCore string, int-range,
    // random-timeout, move, scriptable-object and graph-serialisation nodes.
    // Addresses are the original x86-64 bodies. Machines skip the FSMManager
    // (except the one case that owns a manager row), names are unique, and Random,
    // Unity objects, files and the HLOutput error route are owned and restored by each case.
    public class OriginalStringTimeoutFSMTests : OwnedGraphNamesFixture
    {
        public OriginalStringTimeoutFSMTests() : base("Lucid-strtimeout-") { }

        private static string Name() => "Lucid-strtimeout-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);
        private static FSMUser User() => new FSMUser(new FSMStorage(0));

        // FSMTransitionCheckIntInRange.DoUpdate 0x06000486, 0x1ac9d50: FillValueOnly<int>
        // (0x1ac9e1c, no insertion), then signed jl Low / setle High (0x1ac9e24..0x1ac9e2c).
        // Catches exclusive bounds, unsigned compares, swapped bounds or default insertion.
        [Test]
        public void CheckIntInRangeKeepsInclusiveSignedBoundsWithoutInsertingMissingValues()
        {
            var machine = Machine();
            var user = User();
            var key = new GraphStorageKey(Name());
            var range = new FSMTransitionCheckIntInRange(machine, Name(), key, -2, 3);
            Assert.That(range.Update(user, default), Is.True, "missing reads zero");
            Assert.That(user.Storage.HasValue(key), Is.False);
            foreach (int inside in new[] { -2, 0, 3 })
            {
                user.Storage.SetValue(key, inside);
                Assert.That(range.Update(user, default), Is.True, inside.ToString());
            }
            foreach (int outside in new[] { -3, 4, int.MinValue, int.MaxValue })
            {
                user.Storage.SetValue(key, outside);
                Assert.That(range.Update(user, default), Is.False, outside.ToString());
            }
            user.Storage.SetValue(key, 1);
            Assert.That(new FSMTransitionCheckIntInRange(machine, Name(), key, 3, -2).Update(user, default), Is.False, "inverted bounds stay raw");
            Assert.That(machine.Transitions[range.TransitionId], Is.SameAs(range));
        }

        // FSMTransitionCheckString.DoUpdate 0x0600048d, 0x1aca420: GetValue<string>
        // with storeDefault true (0x1aca555), static string.Equals (0x1aca584) or
        // Equals(..., OrdinalIgnoreCase=5) (0x1aca590), and false otherwise (0x1aca5aa).
        [Test]
        public void CheckStringUsesStaticOrdinalEqualityAndStoresMissingValueAsNull()
        {
            var machine = Machine();
            var user = User();
            var key = new GraphStorageKey(Name());
            var sensitive = new FSMTransitionCheckString(machine, Name(), key, "Ready");
            Assert.That(sensitive.Compare, Is.EqualTo(FSMTransitionCheckString.Comparison.EqualCaseSensitive));
            Assert.That(sensitive.Update(user, default), Is.False);
            Assert.That(user.Storage.HasValue(key), Is.True, "missing string is inserted");
            Assert.That(user.Storage.GetCollection()[key], Is.Null);
            Assert.That(new FSMTransitionCheckString(machine, Name(), key, null).Update(user, default), Is.True, "null trigger equals stored null");
            var insensitive = new FSMTransitionCheckString(machine, Name(), key, "Ready", FSMTransitionCheckString.Comparison.EqualCaseInsensitive);
            user.Storage.SetValue(key, "READY");
            Assert.That(sensitive.Update(user, default), Is.False);
            Assert.That(insensitive.Update(user, default), Is.True);
            user.Storage.SetValue(key, "Ready");
            Assert.That(sensitive.Update(user, default), Is.True);
            Assert.That(new FSMTransitionCheckString(machine, Name(), key, "Ready", (FSMTransitionCheckString.Comparison)7).Update(user, default), Is.False);
            Assert.That(Enum.GetNames(typeof(FSMTransitionCheckString.Comparison)), Is.EqualTo(new[] { "EqualCaseSensitive", "EqualCaseInsensitive" }));
        }

        // CreateDefaultIdentifier 0x0600048a (0x1ac9f70, compare boxed before the
        // trigger text) and 0x06000483 (0x1ac9950); ConstructInstance 0x0600048b
        // parses ignoring case (edx=1, 0x1aca1bc) before allocation (0x1aca20a).
        [Test]
        public void StringAndRangeFactoriesKeepOriginalIdentifiersEnumParsingAndJson()
        {
            var machine = Machine();
            string variable = Name();
            var bare = new GraphStorageKey(variable);
            string keyText = "null_null_" + variable;
            var shortcut = new FSMTransitionCheckString(machine, bare, "go", FSMTransitionCheckString.Comparison.EqualCaseInsensitive);
            Assert.That(FSMTransition.LookupNameUsingId(shortcut.TransitionId), Is.EqualTo("CheckString_" + keyText + "_EqualCaseInsensitive_go"));
            var range = new FSMTransitionCheckIntInRange(machine, bare, -3, 4);
            Assert.That(FSMTransition.LookupNameUsingId(range.TransitionId),
                Is.EqualTo("CheckIntInRange_" + keyText + "_" + (-3).ToString(CultureInfo.CurrentCulture) + "_4"));

            string name = Name(), node = Name(), graph = Name();
            string common = "\"Name\":\"" + name + "\",\"Node\":\"" + node + "\",\"FSM\":\"" + graph + "\"";
            var key = new GraphStorageKey(name, node, graph);
            var check = (FSMTransitionCheckString)FSMTransitionCheckString.ConstructInstance(machine, Name(),
                "{" + common + ",\"TriggerOn\":\"Done\",\"Compare\":\"eQuAlCaSeInSeNsItIvE\"}");
            Assert.That(check.StorageKey, Is.EqualTo(key));
            Assert.That(check.TriggerOn, Is.EqualTo("Done"));
            Assert.That(check.Compare, Is.EqualTo(FSMTransitionCheckString.Comparison.EqualCaseInsensitive));
            var cj = JsonUtility.FromJson<KeyObservation>(check.SerialiseRuntimeToJSON());
            AssertKey(cj, name, node, graph);
            Assert.That(cj.TriggerOn, Is.EqualTo("Done"));
            Assert.That(cj.Compare, Is.EqualTo("EqualCaseInsensitive"));
            int count = machine.Transitions.Count;
            Assert.Throws<ArgumentException>(() => FSMTransitionCheckString.ConstructInstance(machine, Name(),
                "{" + common + ",\"TriggerOn\":\"Done\",\"Compare\":\"Equal\"}"));
            Assert.That(machine.Transitions.Count, Is.EqualTo(count), "enum fault precedes registration");

            var parsed = (FSMTransitionCheckIntInRange)FSMTransitionCheckIntInRange.ConstructInstance(machine, Name(), "{" + common + ",\"Low\":-5,\"High\":9}");
            Assert.That(parsed.StorageKey, Is.EqualTo(key));
            Assert.That(parsed.Low, Is.EqualTo(-5));
            Assert.That(parsed.High, Is.EqualTo(9));
            var rj = JsonUtility.FromJson<KeyObservation>(parsed.SerialiseRuntimeToJSON());
            AssertKey(rj, name, node, graph);
            Assert.That(rj.Low, Is.EqualTo(-5));
            Assert.That(rj.High, Is.EqualTo(9));
        }

        // FSMTransitionTimeoutRandom.DoOnEnter 0x060004aa, 0x1acc430: Random.Range(Min, Max)
        // into SetTimeoutSeconds, then SetElapsedSeconds(0). DoOnLeave 0x060004ab removes
        // timeout then elapsed. The constructor (0x1acbc40) keeps the raw, unordered range.
        [Test]
        public void TimeoutRandomRollsRawRangeThenResetsElapsedAndRemovesBothOnLeave()
        {
            Random.State saved = Random.state;
            try
            {
                var machine = Machine();
                var user = User();
                var timeout = new FSMTransitionTimeoutRandom(machine, Name(), 3f, 1f);
                var elapsedKey = new GraphStorageKey("elapsedSeconds", timeout.TransitionId, machine.FSMId);
                var timeoutKey = new GraphStorageKey("timeoutSeconds", timeout.TransitionId, machine.FSMId);
                Assert.That(timeout.ElapsedVariableName, Is.EqualTo(elapsedKey));
                Assert.That(timeout.TimeoutVariableName, Is.EqualTo(timeoutKey));
                Random.InitState(4021);
                float expected = Random.Range(3f, 1f);
                float expectedNext = Random.value;
                user.Storage.SetValue(elapsedKey, 8f);
                Random.InitState(4021);
                timeout.OnEnter(user, null);
                Assert.That(Random.value, Is.EqualTo(expectedNext), "exactly one range draw");
                Assert.That(user.Storage.GetValueOnly<float>(timeoutKey), Is.EqualTo(expected));
                Assert.That(user.Storage.GetValueOnly<float>(elapsedKey, -1f), Is.EqualTo(0f));
                timeout.OnLeave(user, null);
                Assert.That(user.Storage.HasValue(timeoutKey), Is.False);
                Assert.That(user.Storage.HasValue(elapsedKey), Is.False);
            }
            finally
            {
                Random.state = saved;
            }
        }

        // DoUpdate 0x060004ac, 0x1acc6c0: AdjustValue(elapsed, delta) first (0x1acc776),
        // then GetValue<float>(timeout, 0, storeDefault true) (0x1acc2ca), compared with
        // setae (NaN false). GetElapsedSeconds itself writes back even a zero adjustment.
        [Test]
        public void TimeoutRandomUpdateAdvancesBeforeReadingAndStoresMissingTimeoutAsZero()
        {
            var machine = Machine();
            var user = User();
            var timeout = new FSMTransitionTimeoutRandom(machine, Name(), 1f, 2f);
            Assert.That(timeout.Update(user, new FSMUpdateContext(0.25f)), Is.True, "missing timeout reads zero");
            Assert.That(user.Storage.GetValueOnly<float>(timeout.TimeoutVariableName, -1f), Is.EqualTo(0f));
            Assert.That(user.Storage.GetValueOnly<float>(timeout.ElapsedVariableName), Is.EqualTo(0.25f));
            timeout.SetTimeoutSeconds(user, 1f);
            timeout.SetElapsedSeconds(user, 0f);
            Assert.That(timeout.Update(user, new FSMUpdateContext(0.5f, FSMUpdateType.FixedUpdate)), Is.False);
            Assert.That(timeout.Update(user, new FSMUpdateContext(0.5f, FSMUpdateType.Manual)), Is.True, "boundary is inclusive");
            timeout.SetTimeoutSeconds(user, float.NaN);
            Assert.That(timeout.Update(user, new FSMUpdateContext(0f)), Is.False);
            Assert.That(float.IsNaN(timeout.GetTimeoutSeconds(user)), Is.True);
            var fresh = User();
            Assert.That(timeout.GetElapsedSeconds(fresh), Is.EqualTo(0f));
            Assert.That(fresh.Storage.HasValue(timeout.ElapsedVariableName), Is.True, "zero adjustment is written");
        }

        // Constructor 0x060004a3 substitutes only null/empty names (0x1acbd1f/0x1acbd5f)
        // and scopes both keys to the transition and machine; Serialise 0x060004a5 writes
        // only the name IDs (0x1acbec1/0x1acbf14); ConstructInstance 0x060004a4.
        [Test]
        public void TimeoutRandomScopesNamesToTheTransitionAndRoundTripsJson()
        {
            var machine = Machine();
            string elapsed = Name(), limit = Name();
            var defaults = new FSMTransitionTimeoutRandom(machine, Name(), 1f, 2f, "", null);
            Assert.That(defaults.ElapsedVariableName, Is.EqualTo(new GraphStorageKey("elapsedSeconds", defaults.TransitionId, machine.FSMId)));
            Assert.That(defaults.TimeoutVariableName, Is.EqualTo(new GraphStorageKey("timeoutSeconds", defaults.TransitionId, machine.FSMId)));
            var named = new FSMTransitionTimeoutRandom(machine, Name(), -1.5f, 4.25f, elapsed, limit);
            var json = JsonUtility.FromJson<TimeoutObservation>(named.SerialiseRuntimeToJSON());
            Assert.That(json.MinTimeoutSeconds, Is.EqualTo(-1.5f));
            Assert.That(json.MaxTimeoutSeconds, Is.EqualTo(4.25f));
            Assert.That(json.ElapsedVariableName, Is.EqualTo(elapsed));
            Assert.That(json.TimeoutVariableName, Is.EqualTo(limit));
            var copy = (FSMTransitionTimeoutRandom)FSMTransitionTimeoutRandom.ConstructInstance(machine, Name(), named.SerialiseRuntimeToJSON());
            Assert.That(copy.MinTimeoutSeconds, Is.EqualTo(-1.5f));
            Assert.That(copy.MaxTimeoutSeconds, Is.EqualTo(4.25f));
            Assert.That(copy.ElapsedVariableName, Is.EqualTo(new GraphStorageKey(elapsed, copy.TransitionId, machine.FSMId)));
            Assert.That(copy.TimeoutVariableName, Is.EqualTo(new GraphStorageKey(limit, copy.TransitionId, machine.FSMId)));
            Assert.That(copy.ElapsedVariableName, Is.Not.EqualTo(named.ElapsedVariableName), "scope follows the new node");
        }

        // FSMStateSetString .ctor 0x06000454 (0x1ac6800): base registration (0x1ac6817)
        // precedes the args reads; DoOnEnter 0x06000456 writes SetValue<string>. There is
        // no SerialiseRuntimeToJSON override, so the base empty string remains.
        [Test]
        public void SetStringRegistersBeforeArgsAndWritesCapturedValueOnEnter()
        {
            var machine = Machine();
            var user = User();
            var args = new FSMStateSetString.JSONCtorArgs { Name = Name(), Node = Name(), FSM = Name(), Value = "first" };
            var key = new GraphStorageKey(args.Name, args.Node, args.FSM);
            var set = new FSMStateSetString(machine, Name(), args);
            args.Value = "changed";
            set.OnEnter(user, null);
            Assert.That(user.Storage.GetValueOnly<string>(key), Is.EqualTo("first"));
            Assert.That(set.SerialiseRuntimeToJSON(), Is.EqualTo(string.Empty));
            var parsed = (FSMStateSetString)FSMStateSetString.ConstructInstance(machine, Name(),
                "{\"Name\":\"" + args.Name + "\",\"Node\":\"" + args.Node + "\",\"FSM\":\"" + args.FSM + "\",\"Value\":\"json\"}");
            parsed.OnEnter(user, null);
            Assert.That(user.Storage.GetValueOnly<string>(key), Is.EqualTo("json"));
            FSMIdentifier id = Name();
            int count = machine.States.Count;
            Assert.Throws<NullReferenceException>(() => new FSMStateSetString(machine, id, null));
            Assert.That(machine.States.Count, Is.EqualTo(count + 1));
            Assert.That(machine.States[id.Id], Is.TypeOf<FSMStateSetString>());
        }

        // FSMStateMoveGameObject.DoUpdate 0x06000446, 0x1ac5bd0: GetAs<FSMUserBehaviourGameObject>
        // cast, Transform.Translate(translation * delta) (0x1ac5c57, self space) and then
        // Transform.Rotate(rotation * delta) (0x1ac5c7d). A pre-rotated object separates the
        // order: rotating first would move along a different world direction.
        [Test]
        public void MoveGameObjectTranslatesInSelfSpaceBeforeRotatingByDelta()
        {
            var host = new GameObject("Owned FSM move user");
            try
            {
                var user = host.AddComponent<FSMUserBehaviourGameObject>();
                Quaternion start = Quaternion.Euler(0f, 90f, 0f);
                host.transform.SetPositionAndRotation(Vector3.zero, start);
                var machine = Machine();
                var move = new FSMStateMoveGameObject(machine, Name(), new Vector3(2f, 0f, 0f), new Vector3(0f, 0f, 60f));
                move.Update(user, new FSMUpdateContext(0.5f, FSMUpdateType.LateUpdate));
                Assert.That(Vector3.Distance(host.transform.position, new Vector3(0f, 0f, -1f)), Is.LessThan(1e-4f));
                Assert.That(Quaternion.Angle(host.transform.rotation, start * Quaternion.Euler(0f, 0f, 30f)), Is.LessThan(1e-3f));
                var copy = (FSMStateMoveGameObject)FSMStateMoveGameObject.ConstructInstance(machine, Name(), move.SerialiseRuntimeToJSON());
                var json = JsonUtility.FromJson<MoveObservation>(copy.SerialiseRuntimeToJSON());
                Assert.That(json.Translation, Is.EqualTo(new Vector3(2f, 0f, 0f)));
                Assert.That(json.Rotation, Is.EqualTo(new Vector3(0f, 0f, 60f)));
                Assert.Throws<InvalidCastException>(() => move.Update(User(), new FSMUpdateContext(1f)));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // FSMStateUsesScriptableObject.FindByName 0x0600045b, 0x1ac6b70: empty names return
        // null silently (0x1ac6bcc); otherwise a loaded ScriptableObject whose name is equal
        // (0x1ac6c17) is taken, and a Unity-null result logs the formatted miss with the state
        // (0x1ac6c6a/0x1ac6c91). Serialise 0x0600045a uses Unity truthiness.
        [Test]
        public void UsesScriptableObjectResolvesLoadedObjectsByNameAndLogsOnlyRealMisses()
        {
            var owned = new List<Object>();
            using (var capture = new OwnedHLOutputErrorCapture())
            {
                try
                {
                    string nameA = Name(), nameB = Name(), missing = Name(), stateName = Name();
                    var a = ScriptableObject.CreateInstance<HLProfilerGroupDefinition>();
                    a.name = nameA;
                    owned.Add(a);
                    var b = ScriptableObject.CreateInstance<HLProfilerGroupDefinition>();
                    b.name = nameB;
                    owned.Add(b);
                    var machine = Machine();
                    var uses = new FSMStateUsesScriptableObject(machine, stateName, nameA, nameB);
                    Assert.That(machine.States[FSMState.ConvertNameToId(stateName)], Is.SameAs(uses));
                    Assert.IsEmpty(capture.Errors);
                    var json = JsonUtility.FromJson<ScriptableObservation>(uses.SerialiseRuntimeToJSON());
                    Assert.That(json.ScriptableObjectNameA, Is.EqualTo(nameA));
                    Assert.That(json.ScriptableObjectNameB, Is.EqualTo(nameB));

                    string missState = Name();
                    var miss = FSMStateUsesScriptableObject.ConstructInstance(machine, missState,
                        "{\"ScriptableObjectNameA\":\"" + missing + "\",\"ScriptableObjectNameB\":\"\"}");
                    CollectionAssert.AreEqual(new[] { "Referenced scriptable object '" + missing + "' not found. Used by FSM state '" + missState +
                        "'. Add to FSM's referenced scriptable object array|0" }, capture.Errors);
                    var none = JsonUtility.FromJson<ScriptableObservation>(miss.SerialiseRuntimeToJSON());
                    Assert.That(none.ScriptableObjectNameA, Is.Null.Or.Empty);
                    Assert.That(none.ScriptableObjectNameB, Is.Null.Or.Empty);

                    capture.Errors.Clear();
                    Object.DestroyImmediate(b);
                    owned.Remove(b);
                    var destroyed = JsonUtility.FromJson<ScriptableObservation>(uses.SerialiseRuntimeToJSON());
                    Assert.That(destroyed.ScriptableObjectNameA, Is.EqualTo(nameA));
                    Assert.That(destroyed.ScriptableObjectNameB, Is.Null.Or.Empty);
                    Assert.IsEmpty(capture.Errors);
                }
                finally
                {
                    foreach (Object item in owned)
                    {
                        if (item != null) Object.DestroyImmediate(item);
                    }
                }
            }
        }

        // FindByName 0x0600045b breaks on the first string op_Equality hit (0x1ac6c1e), so of
        // two loaded objects sharing a name the one earlier in FindObjectsOfTypeAll order is
        // kept. Only the state's Unity truthiness is observable (Serialise 0x0600045a), so
        // destroying the later twin must keep the name and destroying the earlier one must
        // clear it. A last-wins or non-breaking loop keeps the later twin and fails.
        [Test]
        public void UsesScriptableObjectKeepsTheFirstLoadedObjectWithAnEqualName()
        {
            var owned = new List<Object>();
            using (var capture = new OwnedHLOutputErrorCapture())
            {
                try
                {
                    string shared = Name();
                    var one = ScriptableObject.CreateInstance<HLProfilerGroupDefinition>();
                    one.name = shared;
                    owned.Add(one);
                    var two = ScriptableObject.CreateInstance<HLProfilerGroupDefinition>();
                    two.name = shared;
                    owned.Add(two);
                    // The expected winner comes from the engine's own enumeration, not creation order.
                    List<ScriptableObject> twins = Resources.FindObjectsOfTypeAll<ScriptableObject>()
                        .Where(item => item.name == shared).ToList();
                    Assert.That(twins.Count, Is.EqualTo(2), "both owned twins are loaded");
                    ScriptableObject earlier = twins[0];
                    ScriptableObject later = twins[1];

                    var uses = new FSMStateUsesScriptableObject(Machine(), Name(), shared, "");
                    Assert.IsEmpty(capture.Errors);
                    Object.DestroyImmediate(later);
                    var afterLater = JsonUtility.FromJson<ScriptableObservation>(uses.SerialiseRuntimeToJSON());
                    Assert.That(afterLater.ScriptableObjectNameA, Is.EqualTo(shared), "the earlier twin was kept");
                    Object.DestroyImmediate(earlier);
                    var afterEarlier = JsonUtility.FromJson<ScriptableObservation>(uses.SerialiseRuntimeToJSON());
                    Assert.That(afterEarlier.ScriptableObjectNameA, Is.Null.Or.Empty, "the kept reference was the earlier twin");
                    Assert.IsEmpty(capture.Errors);
                }
                finally
                {
                    foreach (Object item in owned)
                    {
                        if (item != null) Object.DestroyImmediate(item);
                    }
                }
            }
        }

        // FSMClassFactory.GenerateJSONFiniteStateMachineClass 0x060003af, 0x1abe010: name,
        // flags and default state, then states (Name, GetType() then virtual slot 8, inferred
        // MemberInfo.get_Name; node serialiser, links) and transitions. No node metadata is produced.
        [Test]
        public void GenerateFiniteStateMachineClassWritesShortClassNamesArgumentsAndLinks()
        {
            var machine = Machine();
            machine.DoMultipleTransitions = false;
            machine.UpdateTransientStates = true;
            string fromName = Name(), toName = Name(), checkName = Name();
            var from = new FSMStateSetString(machine, fromName, new FSMStateSetString.JSONCtorArgs { Name = Name(), Node = Name(), FSM = Name(), Value = "v" });
            var to = new FSMStateMoveGameObject(machine, toName, Vector3.one, Vector3.up);
            var check = new FSMTransitionCheckString(machine, checkName, new GraphStorageKey(Name()), "x");
            from.AddTransition(check, to);
            machine.DefaultState = to;

            FSMClassFactory.JSONFiniteStateMachineClass json = FSMClassFactory.GenerateJSONFiniteStateMachineClass(machine);
            Assert.That(json.Name, Is.EqualTo(GraphNameLookup.LookupNameUsingId(machine.FSMId)));
            Assert.That(json.DefaultState, Is.EqualTo(toName));
            Assert.That(json.DoMultipleTransitions, Is.False);
            Assert.That(json.UpdateTransientStates, Is.True);
            Assert.That(json.NodeMetadata, Is.Null);
            Assert.That(json.States.Count, Is.EqualTo(2));
            Assert.That(json.States[0].Name, Is.EqualTo(fromName));
            Assert.That(json.States[0].Class, Is.EqualTo("FSMStateSetString"));
            Assert.That(json.States[0].CtorArgs, Is.EqualTo(string.Empty));
            Assert.That(json.States[0].StateTransitions.Count, Is.EqualTo(1));
            Assert.That(json.States[0].StateTransitions[0].Transition, Is.EqualTo(checkName));
            Assert.That(json.States[0].StateTransitions[0].ToState, Is.EqualTo(toName));
            Assert.That(json.States[0].StateTransitions[0].NodeMetadata, Is.Null);
            Assert.That(json.States[1].Class, Is.EqualTo("FSMStateMoveGameObject"));
            Assert.That(json.States[1].CtorArgs, Is.EqualTo(to.SerialiseRuntimeToJSON()));
            Assert.That(json.States[1].StateTransitions, Is.Empty);
            Assert.That(json.States[1].NodeMetadata, Is.Null);
            Assert.That(json.Transitions.Count, Is.EqualTo(1));
            Assert.That(json.Transitions[0].Name, Is.EqualTo(checkName));
            Assert.That(json.Transitions[0].Class, Is.EqualTo("FSMTransitionCheckString"));
            Assert.That(json.Transitions[0].CtorArgs, Is.EqualTo(check.SerialiseRuntimeToJSON()));

            var empty = FSMClassFactory.GenerateJSONFiniteStateMachineClass(Machine());
            Assert.That(empty.DefaultState, Is.Null, "no default state leaves the field unset");
            Assert.That(empty.DoMultipleTransitions, Is.True);
            Assert.That(empty.States, Is.Empty);
            Assert.That(empty.Transitions, Is.Empty);
        }

        // GenerateJSONMultipleFSMsClass 0x060003ae, 0x1abda00: reuse a supplied container
        // (allocate only for null, 0x1abdaea) and always replace FSMs (0x1abdb33). Includes
        // are untouched. Dependencies and the null-fsm branch have their own cases below.
        [Test]
        public void GenerateMultipleFSMsReusesContainerAndReplacesOnlyTheMachineList()
        {
            var machine = Machine();
            var includes = new List<string> { "include.json" };
            var oldList = new List<FSMClassFactory.JSONFiniteStateMachineClass> { new FSMClassFactory.JSONFiniteStateMachineClass() };
            var existing = new FSMClassFactory.JSONMultipleFSMs { Includes = includes, FSMs = oldList };
            FSMClassFactory.JSONMultipleFSMs result = FSMClassFactory.GenerateJSONMultipleFSMsClass(machine, existing);
            Assert.That(result, Is.SameAs(existing));
            Assert.That(result.Includes, Is.SameAs(includes));
            Assert.That(result.FSMs, Is.Not.SameAs(oldList));
            Assert.That(oldList.Count, Is.EqualTo(1));
            Assert.That(result.FSMs.Count, Is.EqualTo(1));
            Assert.That(result.FSMs[0].Name, Is.EqualTo(GraphNameLookup.LookupNameUsingId(machine.FSMId)));
            var created = FSMClassFactory.GenerateJSONMultipleFSMsClass(machine, null);
            Assert.That(created, Is.Not.Null);
            Assert.That(created.Includes, Is.Null);
            Assert.That(created.FSMs.Count, Is.EqualTo(1));
        }

        // 0x1abdb6d writes the machine first, then 0x1abdbd5 calls GetDependencies
        // (0x06000377) and its List.Enumerator loop adds each entry in order without
        // deduplication. Two sub-FSM states share one dependency to expose duplicates.
        [Test]
        public void GenerateMultipleFSMsWritesTheMachineThenEachDependencyInOrderWithDuplicates()
        {
            var machine = Machine();
            var first = Machine();
            var second = Machine();
            new FSMStateSubFSM(machine, Name(), first);
            new FSMStateSubFSM(machine, Name(), second);
            new FSMStateSubFSM(machine, Name(), first);
            FSMClassFactory.JSONMultipleFSMs json = FSMClassFactory.GenerateJSONMultipleFSMsClass(machine, null);
            var expected = new[] { machine, first, second, first }.Select(item => GraphNameLookup.LookupNameUsingId(item.FSMId)).ToArray();
            Assert.That(json.FSMs.Select(item => item.Name).ToArray(), Is.EqualTo(expected));
            Assert.That(json.FSMs[0].States.Count, Is.EqualTo(3));
            Assert.That(json.FSMs[1].States, Is.Empty);
        }

        // Null fsm (0x1abdd1f): FSMManager.GetManager(null).FSMs is walked through
        // IEnumerable<KeyValuePair<int, FiniteStateMachine>> and each value is written.
        // The default manager row is borrowed or owned through FSMManagerVerification's row
        // scope. Both manager tables get their original layout back, dropping the machine, in the
        // body's own finally, so the restore never depends on the row still being live at cleanup.
        [Test]
        public void GenerateMultipleFSMsWithoutMachineWritesEveryManagerRegisteredFSM()
        {
            FSMManagerVerification.WithDefaultManagerRow(() =>
            {
                FSMManager manager = FSMManager.GetManager();
                var tables = new List<KeyValuePair<IDictionary, object[]>>();
                try
                {
                    foreach (string field in new[] { "m_fsmRefs", "m_fsmLookUp" })
                    {
                        var table = (IDictionary)typeof(FSMManager).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager);
                        tables.Add(new KeyValuePair<IDictionary, object[]>(table, FSMManagerVerification.CaptureLayout(table)));
                    }
                    var machine = Machine();
                    manager.AddFSM(machine);
                    List<string> registered = manager.FSMs.Select(entry => GraphNameLookup.LookupNameUsingId(entry.Value.FSMId)).ToList();
                    Assert.That(registered, Does.Contain(GraphNameLookup.LookupNameUsingId(machine.FSMId)));

                    FSMClassFactory.JSONMultipleFSMs json = FSMClassFactory.GenerateJSONMultipleFSMsClass(null, null);
                    Assert.That(json.FSMs.Select(item => item.Name).ToList(), Is.EqualTo(registered));
                }
                finally
                {
                    foreach (KeyValuePair<IDictionary, object[]> table in tables)
                        FSMManagerVerification.RestoreLayout(table.Key, table.Value);
                }
            }, manager => { });
        }

        // SerialiseFSMsToFile 0x060003b0, 0x1abf110: pretty ToJson (esi=1, 0x1abf1e7), then
        // whitespace-path and extension errors return (0x1abf2d9). An existing file without
        // replace logs (0x1abf314) yet falls through to the directory and StreamWriter path.
        [Test]
        public void SerialiseToFileRejectsBadPathsAndStillOverwritesAfterLoggingExistingFile()
        {
            string root = Path.Combine(Path.GetTempPath(), Name());
            using (var capture = new OwnedHLOutputErrorCapture())
            {
                try
                {
                    var machine = Machine();
                    string machineName = GraphNameLookup.LookupNameUsingId(machine.FSMId);
                    FSMClassFactory.SerialiseFSMsToFile(machine, "   ");
                    CollectionAssert.AreEqual(new[] { "Unable to serialise FSM, path is empty or whitespace|0" }, capture.Errors);
                    capture.Errors.Clear();
                    string wrong = Path.Combine(root, "graph.txt");
                    FSMClassFactory.SerialiseFSMsToFile(machine, wrong);
                    CollectionAssert.AreEqual(new[] { "Unable to serialise FSM, file extension is not '.json'|0" }, capture.Errors);
                    Assert.That(Directory.Exists(root), Is.False);

                    capture.Errors.Clear();
                    string path = Path.Combine(root, "nested", "graph.json");
                    FSMClassFactory.SerialiseFSMsToFile(machine, path);
                    Assert.IsEmpty(capture.Errors);
                    string text = File.ReadAllText(path);
                    Assert.That(text, Does.Contain("\n"), "pretty printed");
                    var parsed = JsonUtility.FromJson<FSMClassFactory.JSONMultipleFSMs>(text);
                    Assert.That(parsed.FSMs.Count, Is.EqualTo(1));
                    Assert.That(parsed.FSMs[0].Name, Is.EqualTo(machineName));

                    File.WriteAllText(path, "sentinel");
                    FSMClassFactory.SerialiseFSMsToFile(machine, path);
                    CollectionAssert.AreEqual(new[] { "Unable to serialise FSM, file exists at " + path + "|0" }, capture.Errors);
                    Assert.That(File.ReadAllText(path), Is.EqualTo(text), "logged existing file is still overwritten");

                    capture.Errors.Clear();
                    File.WriteAllText(path, "sentinel");
                    FSMClassFactory.SerialiseFSMsToFile(machine, path, true);
                    Assert.IsEmpty(capture.Errors);
                    Assert.That(File.ReadAllText(path), Is.EqualTo(text));
                }
                finally
                {
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                }
            }
        }

        [Serializable]
        private sealed class KeyObservation
        {
            public string Name, Node, FSM, TriggerOn, Compare;
            public int Low, High;
        }
        [Serializable]
        private sealed class TimeoutObservation
        {
            public float MinTimeoutSeconds, MaxTimeoutSeconds;
            public string ElapsedVariableName, TimeoutVariableName;
        }
        [Serializable]
        private sealed class MoveObservation { public Vector3 Translation, Rotation; }
        [Serializable]
        private sealed class ScriptableObservation { public string ScriptableObjectNameA, ScriptableObjectNameB; }
        private static void AssertKey(KeyObservation value, string name, string node, string graph)
        {
            Assert.That(value.Name, Is.EqualTo(name));
            Assert.That(value.Node, Is.EqualTo(node));
            Assert.That(value.FSM, Is.EqualTo(graph));
        }
    }
}
