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
        public IEnumerator OriginalFSMComponents_EmbeddedStartDispatchAndLifecycle()
        {
            string name = "Lucid-live-fsm-" + Guid.NewGuid().ToString("N");
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
                Assert.IsTrue(FSMManager.GetManager().FSMExists(name));
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
                Assert.IsFalse(FSMManager.GetManager().FSMExists(name), "The final component release revokes manager ownership.");
                Assert.AreSame(machine, asset.FSM, "Original asset release retains its selected machine reference.");
            }
            finally
            {
                if (userObject != null) UnityEngine.Object.DestroyImmediate(userObject);
                if (machineObject != null) UnityEngine.Object.DestroyImmediate(machineObject);
                var manager = FSMManager.GetManager(); while (manager.FSMExists(name)) manager.ReleaseFSM(name);
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            }
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
