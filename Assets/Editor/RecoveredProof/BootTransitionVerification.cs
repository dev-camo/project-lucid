using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Verification
{
    // Controlled reconstruction fixtures. Original classes and declarations remain untouched.
    // These cases are future Unity Editor-only checks, not substitutes for original boot flow.
    public static partial class BootTransitionVerification
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Boot transition verification: " + message);
        }
        sealed class Owned
        {
            public FSMStorage Storage; public FSMUser User; public FiniteStateMachine Machine;
            public readonly HashSet<IFSMTransition> KnownTransitions = new HashSet<IFSMTransition>();
            public readonly int Id;
            public Owned(int id) { Id = id; }
            public void Acquire()
            {
                Storage = new FSMStorage(0);
                Require(Storage.GetCollection().Count == 0 && Storage.StateHistoryMaxCount == 0 && Storage.StateHistory == null, "real zero-history storage is empty");
                User = new FSMUser(Storage); Require(ReferenceEquals(User.Storage, Storage), "genuine user retains supplied storage");
                // Empty string resolves to genuine ID zero; nonzero transition IDs below never request Name.
                // ProfilerMarker acquisition belongs to the actual future Unity host; no global manager registration.
                Machine = new FiniteStateMachine(new FSMIdentifier(string.Empty), isOverwriteOk: false, skipAddToManager: true);
                Require(Machine.FSMId == 0 && Machine.States.Count == 0 && Machine.Transitions.Count == 0 && Machine.DefaultState == null && Machine.DoMultipleTransitions && !Machine.UpdateTransientStates, "genuine unregistered empty machine defaults");
            }
            public IFSMTransition Remember(IFSMTransition transition, int id)
            {
                Require(transition != null && (transition.GetType() == typeof(ApplicationTransitionGameSaveLoaded) || transition.GetType() == typeof(ApplicationTransitionIsUnityEditor)), "only exact original owned transitions");
                FSMTransition original = (FSMTransition)transition;
                Require(ReferenceEquals(original.FSM, Machine) && transition.FSMId == 0 && transition.TransitionId == id && Machine.TransitionExists(new FSMIdentifier(id), out IFSMTransition registered) && ReferenceEquals(registered, transition), "original base registered exact object and identifier");
                KnownTransitions.Add(transition); return transition;
            }
            public IFSMTransition Save(int id, string json) => Remember(ApplicationTransitionGameSaveLoaded.ConstructInstance(Machine, new FSMIdentifier(id), json), id);
            public IFSMTransition Editor(int id, string json) => Remember(ApplicationTransitionIsUnityEditor.ConstructInstance(Machine, new FSMIdentifier(id), json), id);
            public void CheckOwnedMachine()
            {
                if (Machine == null) return;
                Require(Machine.FSMId == 0 && Machine.States.Count == 0 && Machine.DefaultState == null, "owned machine has no states/default state");
                foreach (KeyValuePair<int, IFSMTransition> entry in Machine.Transitions)
                    Require(KnownTransitions.Contains(entry.Value) && entry.Key == entry.Value.TransitionId && ReferenceEquals(((FSMTransition)entry.Value).FSM, Machine), "no foreign transition registry entry");
            }
        }
        static void CleanupAttempt(List<Exception> failures, Action action)
        {
            try { action(); } catch (Exception exception) { failures.Add(exception); }
        }
        static void RunOwned(int id, Action<Owned> body)
        {
            CheckCompleteCurrentDeclarationsAndBodies();
            var owned = new Owned(id); var failures = new List<Exception>();
            try { owned.Acquire(); body(owned); }
            catch (Exception exception) { failures.Add(exception); }
            finally
            {
                // Each cleanup/verification is independent. A body or DestroyUser failure cannot skip storage cleanup.
                CleanupAttempt(failures, () => owned.CheckOwnedMachine());
                CleanupAttempt(failures, () => { if (owned.User != null) owned.User.DestroyUser(); });
                CleanupAttempt(failures, () => { if (owned.Storage != null) owned.Storage.Clear(); });
                CleanupAttempt(failures, () => { if (owned.Storage != null) Require(owned.Storage.GetCollection().Count == 0 && owned.Storage.StateHistoryMaxCount == 0 && owned.Storage.StateHistory == null, "owned storage independently empty after cleanup"); });
                CleanupAttempt(failures, () => { if (owned.User != null) Require(ReferenceEquals(owned.User.Storage, owned.Storage), "DestroyUser retains genuine owned storage"); });
                // Machine is an unregistered plain original object with no disposal API. Its private dictionaries
                // are neither reset nor assigned; dropping only these local references ends fixture ownership.
                owned.User = null; owned.Storage = null; owned.Machine = null;
            }
            if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException("Boot transition body and independent cleanup failures", failures);
        }
        static void ExpectExactly<T>(Action action) where T : Exception
        {
            Exception caught = null;
            try { action(); } catch (Exception exception) { caught = exception; }
            Require(caught != null && caught.GetType() == typeof(T), "expected exact managed " + typeof(T).FullName);
        }
        static FSMUpdateContext[] Contexts() => new[] {
            new FSMUpdateContext(0f), new FSMUpdateContext(-2f, FSMUpdateType.FixedUpdate),
            new FSMUpdateContext(float.NaN, FSMUpdateType.Manual), new FSMUpdateContext(float.PositiveInfinity, (FSMUpdateType)int.MaxValue)
        };
        static KeyValuePair<GraphStorageKey, object>[] Snapshot(FSMStorage storage) => storage.GetCollection().ToArray();
        static void SameStorage(FSMStorage storage, KeyValuePair<GraphStorageKey, object>[] before)
        {
            IReadOnlyDictionary<GraphStorageKey, object> actual = storage.GetCollection();
            Require(actual.Count == before.Length, "storage count unchanged by original predicate");
            foreach (KeyValuePair<GraphStorageKey, object> entry in before)
                Require(actual.TryGetValue(entry.Key, out object value) && ReferenceEquals(value, entry.Value), "every owned storage key/object identity retained");
        }
        static void CheckPair(Owned owned, IFSMTransition normal, IFSMTransition inverse, bool expected)
        {
            KeyValuePair<GraphStorageKey, object>[] before = Snapshot(owned.Storage);
            foreach (FSMUpdateContext context in Contexts())
            {
                Require(normal.Update(owned.User, context) == expected && inverse.Update(owned.User, context) == !expected, "normal/inverse exact Boolean truth table across ignored contexts");
                SameStorage(owned.Storage, before);
            }
        }
        public static void VerifyJsonDefaultsAndOriginalFactories()
        {
            RunOwned(0x4c550101, owned =>
            {
                var direct = new ApplicationTransitionGameSaveLoaded.JSONCtorArgs(); Require(!direct.Inverse, "public original DTO constructor defaults false");
                foreach (string json in new[] { "{}", "{\"Inverse\":false}", "{\"Inverse\":true}" })
                {
                    bool expected = json == "{\"Inverse\":true}";
                    var dto = JsonUtility.FromJson<ApplicationTransitionGameSaveLoaded.JSONCtorArgs>(json);
                    Require(dto != null && dto.GetType() == typeof(ApplicationTransitionGameSaveLoaded.JSONCtorArgs) && dto.Inverse == expected, "real Unity JSON public original field");
                    IFSMTransition transition = owned.Save(owned.Id, json);
                    Require(transition.Update(owned.User, new FSMUpdateContext(0f)) == expected && owned.Machine.Transitions.Count == 1 && owned.Storage.GetCollection().Count == 0, "original factory uses parsed inversion and overwrites same owned ID without inserting default");
                }
            });
        }
        public static void VerifySaveLoadedTruthTableAndNoDefaultInsertion()
        {
            RunOwned(0x4c550102, owned =>
            {
                IFSMTransition normal = owned.Save(owned.Id, "{}"), inverse = owned.Save(owned.Id + 1, "{\"Inverse\":true}");
                Require(owned.Machine.Transitions.Count == 2, "two genuine distinct registered transitions");
                CheckPair(owned, normal, inverse, false); Require(owned.Storage.GetCollection().Count == 0, "missing default never inserted");
                object stable = null;
                foreach (bool value in new[] { false, true, false })
                {
                    owned.Storage.SetValue<bool>(AppFSMKeys.GameSaveLoaded, value);
                    Require(owned.Storage.GetCollection().Count == 1 && owned.Storage.GetCollection().TryGetValue(AppFSMKeys.GameSaveLoaded, out object raw) && raw is IGraphStorage.Var<bool>, "genuine value-type wrapper at exact original key");
                    object current = owned.Storage.GetCollection()[AppFSMKeys.GameSaveLoaded];
                    if (stable == null) stable = current;
                    Require(ReferenceEquals(stable, current) && ((IGraphStorage.Var<bool>)current).Value == value, "same genuine Boolean Var identity mutates");
                    CheckPair(owned, normal, inverse, value); Require(((IGraphStorage.Var<bool>)current).Value == value, "predicates retain Boolean value");
                }
                owned.Storage.RemoveValue<bool>(AppFSMKeys.GameSaveLoaded);
                CheckPair(owned, normal, inverse, false); Require(owned.Storage.GetCollection().Count == 0, "removed key returns to absent default without insertion");
            });
        }
        public static void VerifyUserAndTypedNullStorageFaults()
        {
            RunOwned(0x4c550103, owned =>
            {
                IFSMTransition normal = owned.Save(owned.Id, "{}"), inverse = owned.Save(owned.Id + 1, "{\"Inverse\":true}");
                foreach (IFSMTransition transition in new[] { normal, inverse })
                {
                    KeyValuePair<GraphStorageKey, object>[] before = Snapshot(owned.Storage);
                    ExpectExactly<NullReferenceException>(() => transition.Update(null, new FSMUpdateContext(float.NaN)));
                    SameStorage(owned.Storage, before);
                }
                // Real reference-type SetValue stores null directly. Boolean unboxing faults with managed NRE;
                // the genuine conversion catches only InvalidCastException, so this path dispatches no HLOutput log.
                owned.Storage.SetValue<string>(AppFSMKeys.GameSaveLoaded, null);
                Require(owned.Storage.GetCollection().Count == 1 && owned.Storage.GetCollection().ContainsKey(AppFSMKeys.GameSaveLoaded) && owned.Storage.GetCollection()[AppFSMKeys.GameSaveLoaded] == null, "genuine typed raw-null slot");
                foreach (IFSMTransition transition in new[] { normal, inverse })
                {
                    KeyValuePair<GraphStorageKey, object>[] before = Snapshot(owned.Storage);
                    ExpectExactly<NullReferenceException>(() => transition.Update(owned.User, new FSMUpdateContext(-1f, FSMUpdateType.FixedUpdate)));
                    SameStorage(owned.Storage, before);
                }
                Require(ReferenceEquals(owned.User.Storage, owned.Storage) && owned.Machine.Transitions.Count == 2, "faults retain owned user/registration");
                owned.Storage.RemoveValue<string>(AppFSMKeys.GameSaveLoaded); CheckPair(owned, normal, inverse, false);
            });
        }
        public static void VerifyConstructorRegistrationBeforeJsonFault()
        {
            RunOwned(0x4c550104, owned =>
            {
                Require(JsonUtility.FromJson<ApplicationTransitionGameSaveLoaded.JSONCtorArgs>(null) == null, "actual Unity FromJson null result is prerequisite to this managed partial-fault case");
                IFSMTransition predecessor = owned.Editor(owned.Id, "ignored");
                ExpectExactly<NullReferenceException>(() => ApplicationTransitionGameSaveLoaded.ConstructInstance(owned.Machine, new FSMIdentifier(owned.Id), null));
                Require(owned.Machine.TransitionExists(new FSMIdentifier(owned.Id), out IFSMTransition partial) && partial != null && partial.GetType() == typeof(ApplicationTransitionGameSaveLoaded) && !ReferenceEquals(partial, predecessor), "genuine base registration precedes DTO dereference fault");
                owned.Remember(partial, owned.Id); Require(owned.Machine.Transitions.Count == 1, "partial original transition replaces predecessor at same ID");
                Require(!partial.Update(owned.User, new FSMUpdateContext(0f)) && owned.Storage.GetCollection().Count == 0, "partial object retains zero-initialized inversion and absent default");
                owned.Storage.SetValue<bool>(AppFSMKeys.GameSaveLoaded, true);
                Require(partial.Update(owned.User, new FSMUpdateContext(float.PositiveInfinity)) && owned.Storage.GetCollection().Count == 1, "partial object's original predicate reads present true");
                owned.Storage.RemoveValue<bool>(AppFSMKeys.GameSaveLoaded);
                IFSMTransition replacement = owned.Save(owned.Id, "{\"Inverse\":true}");
                Require(!ReferenceEquals(replacement, partial) && !ReferenceEquals(replacement, predecessor) && replacement.Update(owned.User, new FSMUpdateContext(0f)) && owned.Machine.Transitions.Count == 1 && owned.Storage.GetCollection().Count == 0, "valid original factory overwrites partial object and preserves inverted missing predicate");
            });
        }
        public static void VerifyNullMachineFactoryFaults()
        {
            RunOwned(0x4c550105, owned =>
            {
                IFSMTransition kept = owned.Editor(owned.Id, null); KeyValuePair<GraphStorageKey, object>[] before = Snapshot(owned.Storage);
                ExpectExactly<NullReferenceException>(() => ApplicationTransitionGameSaveLoaded.ConstructInstance(null, new FSMIdentifier(owned.Id + 1), "{}"));
                ExpectExactly<NullReferenceException>(() => ApplicationTransitionIsUnityEditor.ConstructInstance(null, new FSMIdentifier(owned.Id + 2), "not JSON"));
                Require(owned.Machine.Transitions.Count == 1 && owned.Machine.TransitionExists(new FSMIdentifier(owned.Id), out IFSMTransition actual) && ReferenceEquals(actual, kept) && owned.Machine.States.Count == 0, "null foreign factory owner has no fallback or owned registry mutation");
                SameStorage(owned.Storage, before); Require(ReferenceEquals(owned.User.Storage, owned.Storage), "null-owner faults retain genuine owned user");
            });
        }
        public static void VerifyShippingEditorPredicateAndIgnoredJson()
        {
            RunOwned(0x4c550106, owned =>
            {
                IFSMTransition prior = null;
                foreach (string json in new[] { null, string.Empty, "{\"Inverse\":true}", "this is not JSON" })
                {
                    IFSMTransition editor = owned.Editor(owned.Id, json);
                    Require(!ReferenceEquals(prior, editor) && owned.Machine.Transitions.Count == 1, "ignored JSON original editor factory creates and overwrites exact ID");
                    prior = editor;
                    foreach (FSMUpdateContext context in Contexts())
                    {
                        Require(!editor.Update(null, context) && !editor.Update(owned.User, context), "shipping false predicate ignores null user and all contexts");
                        Require(owned.Storage.GetCollection().Count == 0, "shipping predicate does not read or insert storage");
                    }
                }
                object stableBoolean = null;
                foreach (bool value in new[] { false, true })
                {
                    owned.Storage.SetValue<bool>(AppFSMKeys.GameSaveLoaded, value);
                    Require(owned.Storage.GetCollection().Count == 1 && owned.Storage.GetCollection().TryGetValue(AppFSMKeys.GameSaveLoaded, out object rawBoolean) && rawBoolean is IGraphStorage.Var<bool>, "editor case uses genuine Boolean wrapper at original key");
                    object currentBoolean = owned.Storage.GetCollection()[AppFSMKeys.GameSaveLoaded];
                    if (stableBoolean == null) stableBoolean = currentBoolean;
                    Require(ReferenceEquals(stableBoolean, currentBoolean) && ((IGraphStorage.Var<bool>)currentBoolean).Value == value, "editor false/true writes retain original Boolean wrapper identity and value");
                    KeyValuePair<GraphStorageKey, object>[] booleanBefore = Snapshot(owned.Storage);
                    foreach (FSMUpdateContext context in Contexts())
                    {
                        Require(!prior.Update(null, context) && !prior.Update(owned.User, context), "shipping editor predicate remains false for genuine false/true flags and both users");
                        SameStorage(owned.Storage, booleanBefore);
                        Require(ReferenceEquals(owned.Storage.GetCollection()[AppFSMKeys.GameSaveLoaded], stableBoolean) && ((IGraphStorage.Var<bool>)currentBoolean).Value == value, "editor predicate retains complete Boolean slot wrapper and value");
                    }
                }
                owned.Storage.RemoveValue<bool>(AppFSMKeys.GameSaveLoaded);
                Require(owned.Storage.GetCollection().Count == 0, "editor Boolean coverage removes only owned flag before raw-null case");
                owned.Storage.SetValue<string>(AppFSMKeys.GameSaveLoaded, null); KeyValuePair<GraphStorageKey, object>[] before = Snapshot(owned.Storage);
                foreach (FSMUpdateContext context in Contexts()) Require(!prior.Update(null, context) && !prior.Update(owned.User, context), "editor predicate ignores genuine typed-null slot");
                SameStorage(owned.Storage, before);
            });
        }
    }
}
