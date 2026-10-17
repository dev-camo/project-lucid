using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Live scheduling of a synthetic graph through the original component and
    // reflection factory. Authored scenes and full-game acceptance are separate.
    public sealed class FSMComponentTests
    {
        [UnityTest]
        public IEnumerator OriginalFSMComponents_EmbeddedStartDispatchAndLifecycle() => EmbeddedStartDispatchAndLifecycle(null, null);

        // afterAcquisition runs once the original acquisition has registered the machine, so a
        // case can fail the flow there; admitted receives the manager row lease, if any.
        private static IEnumerator EmbeddedStartDispatchAndLifecycle(Action afterAcquisition, Action<LifecycleSystemRowLease> admitted)
        {
            string name = "Lucid-live-fsm-" + Guid.NewGuid().ToString("N");
            // FSMManager.GetManager (0x060003eb) registers a default manager whenever that row is
            // null. A live manager is borrowed in place and loses only this test's machine; an
            // absent or idle row is leased, so the manager the acquisition registers is the
            // test's own and its row is released afterwards. A null row that still carries
            // observers or deferred work is refused before anything runs, leaving it untouched:
            // unlike the EditMode fixtures this test does not set such a row aside, so a session
            // that leaked one fails here rather than hiding the leak.
            string managerName = ProcessManager.GetDefaultName<FSMManager>();
            LifecycleSystemRowLease managerLease = ProcessManager.IsSystemNull(managerName)
                ? LifecycleSystemRowLease.AdmitAbsentOrIdle(LifecycleSystemRowLease.SharedRegistry, managerName)
                : null;
            admitted?.Invoke(managerLease);
            GameObject machineObject = null, userObject = null;
            FiniteStateMachineScriptableObject asset = null;
            try
            {
                asset = ScriptableObject.CreateInstance<FiniteStateMachineScriptableObject>();
                Set(asset, "m_name", name); Set(asset, "m_embeddedJSON", GraphJSON(name));
                machineObject = new GameObject("Lucid live FSM component proof"); machineObject.SetActive(false);
                var component = machineObject.AddComponent<FiniteStateMachineBehaviour>();
                Set(component, "m_fsmAsset", asset); SetUpdateType(component, 99);
                userObject = new GameObject("Lucid live self-user proof"); userObject.SetActive(false);
                var user = userObject.AddComponent<FSMUserBehaviourGameObject>();
                Set(user, "m_finiteStateMachine", component);
                int completions = 0; bool selectionDeferred = false;
                asset.OnInitialisationComplete = value =>
                {
                    ++completions;
                    selectionDeferred = ReferenceEquals(value, asset) && value.FSM != null
                        && Get<FiniteStateMachine>(component, "m_fsm") == null
                        && Get<List<IGraphUser>>(component, "m_fsmUsersPendingInitialise").Count == 1;
                };

                userObject.SetActive(true);
                Assert.AreSame(user, Get<IGraphUser>(user, "m_user"), "Awake acquires the component itself before OnEnable.");
                Assert.AreEqual(5, ((FSMStorage)user.Storage).StateHistoryMaxCount);
                Assert.AreEqual(1, Get<List<IGraphUser>>(component, "m_fsmUsersPendingInitialise").Count);
                Assert.IsNull(asset.FSM, "OnEnable registers a pending user before the machine's Start.");
                machineObject.SetActive(true);
                for (int frame = 0; frame < 12 && Get<FiniteStateMachine>(component, "m_fsm") == null; ++frame) yield return null;
                FiniteStateMachine machine = Get<FiniteStateMachine>(component, "m_fsm");
                Assert.IsNotNull(machine, "Unity enumerates the original nested acquisition and construction iterators.");
                Assert.AreSame(asset.FSM, machine);
                Assert.AreEqual(1, completions); Assert.IsTrue(selectionDeferred, "Asset completion precedes component selection and pending-user initialization.");
                Assert.AreEqual(1, Get<int>(asset, "m_refCount"));
                Assert.IsEmpty(Get<List<IGraphUser>>(component, "m_fsmUsersPendingInitialise"));
                Assert.IsTrue(RegisteredManager().FSMExists(name));
                afterAcquisition?.Invoke();
                Assert.AreEqual("idle", machine.GetActiveState(user).ToString());

                SetUpdateType(component, 0);
                for (int frame = 0; frame < 12 && !(machine.GetActiveState(user) is FSMStateFinished); ++frame) yield return null;
                var finished = machine.GetActiveState(user) as FSMStateFinished;
                Assert.IsNotNull(finished, "The discovered original Always transition advances the JSON-created machine on Update.");
                Assert.IsTrue(finished.HasFinished(user)); Assert.IsFalse(finished.IsEndState());
                var storage = user.Storage; var key = new GraphStorageKey("Lucid live user retained value");
                storage.SetValue(key, 17);
                SetUpdateType(component, 99); userObject.SetActive(false);
                Assert.IsNull(machine.GetActiveState(user), "Default OnDisable clears the active state.");
                Assert.IsEmpty(Get<List<IGraphUser>>(component, "m_fsmUsers"));
                Assert.AreEqual(17, storage.GetValue<int>(key), "Clearing the FSM user does not destroy unrelated storage.");
                userObject.SetActive(true);
                Assert.AreSame(storage, user.Storage); Assert.AreEqual("idle", machine.GetActiveState(user).ToString());
                Assert.AreEqual(1, Get<int>(asset, "m_refCount"), "Re-enabling a user does not reacquire the FSM asset.");

                var probe = new ProbeState(machine); machine.DefaultState = probe; machine.InitialiseUser(user);
                SetUpdateType(component, 0);
                for (int frame = 0; frame < 3; ++frame) yield return null;
                Assert.IsNotEmpty(probe.Updates); Assert.IsTrue(probe.Updates.All(x => x.Type == FSMUpdateType.Update));
                Assert.IsTrue(probe.Updates.All(x => ReferenceEquals(x.User, user) && x.DeltaTime == x.EngineDeltaTime));
                SetUpdateType(component, 1); probe.Updates.Clear();
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Assert.IsNotEmpty(probe.Updates); Assert.IsTrue(probe.Updates.All(x => x.Type == FSMUpdateType.FixedUpdate));
                Assert.IsTrue(probe.Updates.All(x => ReferenceEquals(x.User, user) && x.DeltaTime == Time.fixedDeltaTime));
                SetUpdateType(component, 2); probe.Updates.Clear();
                for (int frame = 0; frame < 3; ++frame) yield return null;
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Assert.IsTrue(probe.Updates.Any(x => x.Type == FSMUpdateType.Update));
                Assert.IsTrue(probe.Updates.Any(x => x.Type == FSMUpdateType.FixedUpdate));
                SetUpdateType(component, 99); probe.Updates.Clear();
                yield return null; yield return new WaitForFixedUpdate(); yield return null;
                Assert.IsEmpty(probe.Updates, "An invalid serialized UpdateType suppresses both engine callbacks.");

                int enters = probe.Enters;
                Set(user, "m_clearUserOnDisable", false); Set(user, "m_initialiseUserOnEnable", false);
                userObject.SetActive(false); Assert.AreSame(probe, machine.GetActiveState(user));
                userObject.SetActive(true); Assert.AreEqual(enters, probe.Enters, "Conditional re-enable preserves an existing active state.");
                Set(user, "m_clearUserOnDisable", true); userObject.SetActive(false); Assert.IsNull(machine.GetActiveState(user));
                userObject.SetActive(true); Assert.AreEqual(enters + 1, probe.Enters, "Conditional re-enable initializes after a successful clear.");
                UnityEngine.Object.Destroy(userObject); yield return null;
                Assert.IsTrue(user == null); Assert.IsEmpty(storage.GetCollection(), "OnDestroy releases the retained self user and its storage.");
                Assert.IsEmpty(Get<List<IGraphUser>>(component, "m_fsmUsers"));
                Assert.AreEqual(1, Get<int>(asset, "m_refCount"));
                UnityEngine.Object.Destroy(machineObject); yield return null;
                Assert.AreEqual(0, Get<int>(asset, "m_refCount"));
                Assert.IsFalse(RegisteredManager().FSMExists(name), "The final component release revokes manager ownership.");
                Assert.AreSame(machine, asset.FSM, "Original asset release retains its selected machine reference.");
            }
            finally
            {
                if (userObject != null) UnityEngine.Object.DestroyImmediate(userObject);
                if (machineObject != null) UnityEngine.Object.DestroyImmediate(machineObject);
                // Look the manager up without registering one.
                Action releaseMachine = () =>
                {
                    if (ProcessManager.IsSystemNull(managerName)) return;
                    var manager = ProcessManager.GetSystem<FSMManager>(managerName);
                    while (manager.FSMExists(name)) manager.ReleaseFSM(name);
                };
                if (managerLease == null) releaseMachine();
                else
                {
                    managerLease.RunCleanup("release-fsm", releaseMachine);
                    managerLease.RunCleanup("unregister-fsm-manager", () =>
                    {
                        if (!LifecycleSystemRowLease.SharedRegistry.Contains(managerName)) return;
                        if (!managerLease.IsBorrowed) managerLease.CaptureCreatedRow(ProcessManager.GetSystemRef(managerName));
                        ProcessManager.UnregisterSystem(managerName);
                    });
                    managerLease.Dispose();
                }
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            }
            if (managerLease != null)
            {
                Assert.IsEmpty(managerLease.CleanupFailures, "Fixture cleanup failed; retained failures were reported separately.");
                Assert.IsEmpty(managerLease.DiagnosticFailures, "Fixture cleanup reporting failed; diagnostic failures remain retained.");
            }
        }

        // With no default manager row, the manager the live acquisition registers is the
        // test's own and no row is left behind.
        [UnityTest]
        public IEnumerator OriginalFSMComponents_ColdRegistryLeavesNoDefaultManagerRow()
        {
            var registry = new DefaultManagerRowSnapshot();
            try
            {
                IEnumerator run = OriginalFSMComponents_EmbeddedStartDispatchAndLifecycle();
                try { while (run.MoveNext()) yield return run.Current; }
                finally { (run as IDisposable)?.Dispose(); }
                Assert.IsFalse(LifecycleSystemRowLease.SharedRegistry.Contains(DefaultManagerRowSnapshot.Name), "The test left a default FSM manager row.");
            }
            finally { registry.Restore(); }
        }

        // A failure after the acquisition registered the test's manager still runs the leased
        // cleanup: the machine and the owned row are released and no cleanup step fails.
        [UnityTest]
        public IEnumerator OriginalFSMComponents_ColdRegistryFailureAfterAcquisitionLeavesNoDefaultManagerRow()
        {
            var registry = new DefaultManagerRowSnapshot();
            try
            {
                var injected = new InvalidOperationException("Lucid failure after the manager acquisition");
                LifecycleSystemRowLease lease = null;
                bool acquired = false;
                IEnumerator run = EmbeddedStartDispatchAndLifecycle(() =>
                {
                    acquired = ProcessManager.IsSystemValid<FSMManager>();
                    throw injected;
                }, admitted => lease = admitted);
                Exception caught = null;
                try
                {
                    while (true)
                    {
                        try { if (!run.MoveNext()) break; }
                        catch (Exception failure) { caught = failure; break; }
                        yield return run.Current;
                    }
                }
                finally { (run as IDisposable)?.Dispose(); }
                Assert.AreSame(injected, caught);
                Assert.IsTrue(acquired, "The failure ran after GetManager registered the test's manager.");
                Assert.IsNotNull(lease, "A cold registry is leased.");
                Assert.IsFalse(lease.IsBorrowed);
                Assert.IsEmpty(lease.CleanupFailures, "Fixture cleanup failed after the injected failure.");
                Assert.IsEmpty(lease.DiagnosticFailures);
                Assert.IsFalse(LifecycleSystemRowLease.SharedRegistry.Contains(DefaultManagerRowSnapshot.Name), "The failed test left a default FSM manager row.");
            }
            finally { registry.Restore(); }
        }

        // A null default row whose references carry observers is refused before the test does
        // anything: no observer runs, and the row, its references and the registry order stay.
        [UnityTest]
        public IEnumerator OriginalFSMComponents_ObservedNullRowIsRefusedUntouched()
        {
            var registry = new DefaultManagerRowSnapshot();
            IDictionary rows = LifecycleSystemRowLease.SharedRegistry;
            string later = "Lucid-fsm-row-order-" + Guid.NewGuid().ToString("N");
            SystemRef untyped = ProcessManager.GetSystemRef(DefaultManagerRowSnapshot.Name);
            SystemRef<FSMManager> typed = ProcessManager.GetSystemRef<FSMManager>();
            int calls = 0;
            Action<ISystem> untypedObserver = system => calls++;
            Action<FSMManager> typedObserver = system => calls++;
            try
            {
                object row = rows[DefaultManagerRowSnapshot.Name];
                // A later row makes the observed row's position visible.
                ProcessManager.GetSystemRef(later);
                List<string> order = rows.Keys.Cast<string>().ToList();
                untyped.OnSystemStartup += untypedObserver; untyped.OnSystemShutdown += untypedObserver;
                typed.OnSystemStartup += typedObserver; typed.OnSystemShutdown += typedObserver;
                IEnumerator run = EmbeddedStartDispatchAndLifecycle(null, null);
                try { Assert.Throws<InvalidOperationException>(() => run.MoveNext()); }
                finally { (run as IDisposable)?.Dispose(); }
                Assert.AreEqual(0, calls, "The refused row's observers ran.");
                Assert.AreSame(row, rows[DefaultManagerRowSnapshot.Name]);
                Assert.AreSame(untyped, ProcessManager.GetSystemRef(DefaultManagerRowSnapshot.Name));
                Assert.AreSame(typed, ProcessManager.GetSystemRef<FSMManager>());
                Assert.IsTrue(untyped.IsNull() && typed.IsNull());
                CollectionAssert.AreEqual(order, rows.Keys.Cast<string>().ToList(), "Registry order");
                yield return null;
            }
            finally
            {
                untyped.OnSystemStartup -= untypedObserver; untyped.OnSystemShutdown -= untypedObserver;
                typed.OnSystemStartup -= typedObserver; typed.OnSystemShutdown -= typedObserver;
                rows.Remove(later);
                registry.Restore();
            }
        }

        // A live default manager is borrowed in place: it stays registered and keeps only the
        // machines it already held.
        [UnityTest]
        public IEnumerator OriginalFSMComponents_BorrowedLiveManagerKeepsOnlyItsOwnMachines()
        {
            var registry = new DefaultManagerRowSnapshot();
            try
            {
                var live = new FSMManager();
                ProcessManager.RegisterSystem(live, DefaultManagerRowSnapshot.Name);
                object row = LifecycleSystemRowLease.SharedRegistry[DefaultManagerRowSnapshot.Name];
                var foreign = new FiniteStateMachine("Lucid-live-borrowed-" + Guid.NewGuid().ToString("N"), skipAddToManager: true);
                live.AddFSM(foreign);
                IEnumerator run = OriginalFSMComponents_EmbeddedStartDispatchAndLifecycle();
                try { while (run.MoveNext()) yield return run.Current; }
                finally { (run as IDisposable)?.Dispose(); }
                Assert.AreSame(row, LifecycleSystemRowLease.SharedRegistry[DefaultManagerRowSnapshot.Name]);
                Assert.AreSame(live, ProcessManager.GetSystem<FSMManager>());
                CollectionAssert.AreEquivalent(new[] { foreign.FSMId }, live.FSMs.Keys.ToArray(), "The test left machines in the borrowed manager.");
            }
            finally { registry.Restore(); }
        }

        // An idle empty default row is borrowed: the test's manager registers into it and is
        // revoked again, and the row object and its cached references stay.
        [UnityTest]
        public IEnumerator OriginalFSMComponents_BorrowedIdleRowStaysRegisteredAndEmpty()
        {
            var registry = new DefaultManagerRowSnapshot();
            try
            {
                SystemRef untyped = ProcessManager.GetSystemRef(DefaultManagerRowSnapshot.Name);
                object row = LifecycleSystemRowLease.SharedRegistry[DefaultManagerRowSnapshot.Name];
                IEnumerator run = OriginalFSMComponents_EmbeddedStartDispatchAndLifecycle();
                try { while (run.MoveNext()) yield return run.Current; }
                finally { (run as IDisposable)?.Dispose(); }
                Assert.AreSame(row, LifecycleSystemRowLease.SharedRegistry[DefaultManagerRowSnapshot.Name]);
                Assert.AreSame(untyped, ProcessManager.GetSystemRef(DefaultManagerRowSnapshot.Name));
                Assert.IsTrue(untyped.IsNull(), "The borrowed idle row kept the test's manager.");
            }
            finally { registry.Restore(); }
        }

        // Sets the default manager row aside for one case and puts the registry back afterwards:
        // the case's own row is unregistered and removed, the set-aside row returns, and the
        // dictionary's complete state, free slots included, is restored once it holds exactly
        // its original rows again. This repeats FSMManagerVerification's layout helpers because
        // that class lives in the Editor-only assembly, which this player test assembly cannot
        // reference. Release is also simpler than its audited ReleaseOwnedRows teardown: the
        // case's row holds only the test's own manager, already released through the lease, or
        // a row the case built itself, so the original UnregisterSystem and a plain removal
        // suffice. Failure ordering matches: the row is always removed, the set-aside row put
        // back and the layout restored, and the unregister failure then propagates.
        private sealed class DefaultManagerRowSnapshot
        {
            internal static readonly string Name = ProcessManager.GetDefaultName<FSMManager>();
            private readonly IDictionary rows = LifecycleSystemRowLease.SharedRegistry;
            private readonly List<DictionaryEntry> original = new List<DictionaryEntry>();
            private readonly FieldInfo[] fields;
            private readonly object[] layout;
            private readonly object setAside;

            internal DefaultManagerRowSnapshot()
            {
                foreach (DictionaryEntry row in rows) original.Add(row);
                fields = rows.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                layout = fields.Select(field => field.GetValue(rows) is Array array ? array.Clone() : field.GetValue(rows)).ToArray();
                if (rows.Contains(Name)) setAside = rows[Name];
                rows.Remove(Name);
            }

            internal void Restore()
            {
                try
                {
                    if (rows.Contains(Name)) ProcessManager.UnregisterSystem(Name);
                }
                finally
                {
                    rows.Remove(Name);
                    if (setAside != null) rows[Name] = setAside;
                    if (HoldsOriginalRows())
                        for (int i = 0; i < fields.Length; i++)
                            fields[i].SetValue(rows, layout[i] is Array array ? array.Clone() : layout[i]);
                }
            }

            private bool HoldsOriginalRows()
            {
                if (rows.Count != original.Count) return false;
                foreach (DictionaryEntry row in original)
                    if (!rows.Contains(row.Key) || !ReferenceEquals(rows[row.Key], row.Value)) return false;
                return true;
            }
        }

        // Reads the registered default manager; unlike GetManager it never registers one.
        private static FSMManager RegisteredManager()
        {
            Assert.IsFalse(ProcessManager.IsSystemNull<FSMManager>(), "The original acquisition registered the default FSM manager.");
            return ProcessManager.GetSystem<FSMManager>();
        }

        private static string GraphJSON(string name) => JsonUtility.ToJson(new FSMClassFactory.JSONMultipleFSMs
        {
            FSMs = new List<FSMClassFactory.JSONFiniteStateMachineClass>
            {
                new FSMClassFactory.JSONFiniteStateMachineClass
                {
                    Name = name, DefaultState = "idle",
                    States = new List<FSMClassFactory.JSONStateClass>
                    {
                        new FSMClassFactory.JSONStateClass
                        {
                            Name = "idle", Class = nameof(FSMState),
                            StateTransitions = new List<FSMClassFactory.JSONStateTransition>
                            { new FSMClassFactory.JSONStateTransition { Transition = "finish", ToState = "finished" } }
                        },
                        new FSMClassFactory.JSONStateClass { Name = "finished", Class = nameof(FSMStateFinished) }
                    },
                    Transitions = new List<FSMClassFactory.JSONTransitionClass>
                    { new FSMClassFactory.JSONTransitionClass { Name = "finish", Class = nameof(FSMTransitionAlways) } }
                }
            }
        });
        private sealed class ProbeState : FSMState
        {
            public readonly List<UpdateRecord> Updates = new List<UpdateRecord>(); public int Enters;
            public ProbeState(FiniteStateMachine machine) : base(machine, "probe") { }
            protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action) { ++Enters; }
            protected override void DoUpdate(IGraphUser user, FSMUpdateContext context) => Updates.Add(new UpdateRecord
            {
                User = user, Type = context.UpdateType, DeltaTime = context.DeltaTime,
                EngineDeltaTime = context.UpdateType == FSMUpdateType.FixedUpdate ? Time.fixedDeltaTime : Time.deltaTime
            });
        }
        private sealed class UpdateRecord { public IGraphUser User; public FSMUpdateType Type; public float DeltaTime, EngineDeltaTime; }
        private static FieldInfo Field(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(target.GetType().FullName, name);
        }
        private static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
        private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        private static void SetUpdateType(FiniteStateMachineBehaviour target, int value) => Set(target, "m_updateType", Enum.ToObject(Field(target, "m_updateType").FieldType, value));
    }
}
