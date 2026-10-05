#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    // Isolated original component lifecycle checks. Authored scene wiring,
    // registered diagnostics and full game startup are separate requirements.
    public static class FSMBehaviourVerification
    {
        private static readonly List<GameObject> Objects = new List<GameObject>();
        private static readonly List<FiniteStateMachineScriptableObject> Assets = new List<FiniteStateMachineScriptableObject>();
        private static int checks;
        public static void Run()
        {
            checks = 0;
            try
            {
                VerifyCompilerAttributes();
                VerifyFieldMetadata();
                VerifyPendingUsers();
                VerifySelectedUsers();
                VerifyUpdates();
                VerifyStartAndRelease();
                VerifyUserLifecycle();
            }
            finally
            {
                foreach (GameObject gameObject in Objects) if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
                Objects.Clear();
                foreach (var asset in Assets) if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
                Assets.Clear();
            }
            Debug.Log("Original FSM component lifecycle source-based verification passed: " + checks + " checks. Authored scene startup remains unresolved.");
        }
        private static T Component<T>() where T : Component
        {
            var gameObject = new GameObject("Lucid FSM component proof");
            gameObject.SetActive(false); Objects.Add(gameObject); return gameObject.AddComponent<T>();
        }
        private static FiniteStateMachine Machine() => new FiniteStateMachine("Lucid-component-" + Guid.NewGuid().ToString("N"), skipAddToManager: true);
        private static FiniteStateMachineScriptableObject Asset(FiniteStateMachine machine = null)
        {
            var asset = ScriptableObject.CreateInstance<FiniteStateMachineScriptableObject>();
            asset.name = "Lucid component asset proof"; Assets.Add(asset);
            Set(asset, "m_fsm", machine); Set(asset, "m_releaseOnDestroy", false);
            return asset;
        }
        private sealed class State : FSMState
        {
            public int Enters, Leaves, Updates;
            public Action EnterAction, UpdateAction;
            public IGraphUser LastUser; public FSMUpdateContext LastContext;
            public State(FiniteStateMachine machine) : base(machine, "component-state-" + Guid.NewGuid().ToString("N")) { machine.DefaultState = this; }
            protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action) { ++Enters; LastUser = user; EnterAction?.Invoke(); }
            protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action) { ++Leaves; LastUser = user; }
            protected override void DoUpdate(IGraphUser user, FSMUpdateContext context) { ++Updates; LastUser = user; LastContext = context; UpdateAction?.Invoke(); }
        }
        private static List<IGraphUser> Users(FiniteStateMachineBehaviour component) => Get<List<IGraphUser>>(component, "m_fsmUsers");
        private static List<IGraphUser> Pending(FiniteStateMachineBehaviour component) => Get<List<IGraphUser>>(component, "m_fsmUsersPendingInitialise");
        private static void VerifyCompilerAttributes()
        {
            var value = new object(); var attribute = new Il2CppSetOptionAttribute((Option)77, value);
            Check((int)attribute.Option == 77 && ReferenceEquals(attribute.Value, value), "option/raw value retained");
            typeof(Il2CppSetOptionAttribute).GetProperty("Option").GetSetMethod(true).Invoke(attribute, new object[] { Option.ArrayBoundsChecks });
            typeof(Il2CppSetOptionAttribute).GetProperty("Value").GetSetMethod(true).Invoke(attribute, new object[] { null });
            Check(attribute.Option == Option.ArrayBoundsChecks && attribute.Value == null, "private option/value setters");
            var usage = typeof(Il2CppSetOptionAttribute).GetCustomAttribute<AttributeUsageAttribute>();
            Check(usage.ValidOn == (AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property) && usage.AllowMultiple && !usage.Inherited, "original compiler attribute usage");
            foreach (Type type in new[] { typeof(FiniteStateMachineBehaviour), typeof(FSMUserBehaviour), typeof(FSMUserBehaviourGameObject) })
            {
                var options = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
                Check(options.Length == 2 && options[0].Option == Option.NullChecks && options[1].Option == Option.ArrayBoundsChecks && options.All(a => Equals(a.Value, false)), "original component compiler flags");
            }
        }
        private static void VerifyPendingUsers()
        {
            var component = Component<FiniteStateMachineBehaviour>();
            Check(Users(component).Count == 0 && Pending(component).Count == 0 && !ReferenceEquals(Users(component), Pending(component)), "independent empty user lists");
            Check(Get<int>(component, "m_updateType") == 0 && Get<FiniteStateMachine>(component, "m_fsm") == null, "default Update and no selected FSM");
            var user = new FSMUser(); component.AddUser(user); component.AddUser(user, false, true); component.AddUser(null, false, false);
            Check(Users(component).SequenceEqual(new IGraphUser[] { user, user, null }) && Pending(component).SequenceEqual(new IGraphUser[] { user, user }), "append duplicates/null before initialisation flags");
            component.RemoveUser(user);
            Check(Users(component).SequenceEqual(new IGraphUser[] { user, null }) && Pending(component).Count == 2, "single removal retains pending rows");
            var asset = Asset(); Set(component, "m_fsmAsset", asset); Invoke(component, "OneTimeInitialisation");
            Check(Pending(component).Count == 2 && Get<FiniteStateMachine>(component, "m_fsm") == null, "null asset selection retains pending entries");
            var machine = Machine(); var state = new State(machine); Set(asset, "m_fsm", machine);
            Invoke(component, "OneTimeInitialisation");
            Check(state.Enters == 2 && state.Leaves == 1 && Pending(component).Count == 0 && ReferenceEquals(machine.GetActiveState(user), state), "pending duplicates initialize unconditionally then clear");
            component.RemoveUser(user, false); component.RemoveUser(null, false);
            Check(Users(component).Count == 0 && ReferenceEquals(machine.GetActiveState(user), state), "false clear retains user storage");
            component.RemoveUser(user);
            Check(machine.GetActiveState(user) == null && state.Leaves == 2, "clear runs even when no active-list occurrence exists");

            var failure = Component<FiniteStateMachineBehaviour>(); var failureMachine = Machine(); var throwing = new State(failureMachine);
            var first = new FSMUser(); var second = new FSMUser(); failure.AddUser(first); failure.AddUser(second);
            Set(failure, "m_fsmAsset", Asset(failureMachine)); throwing.EnterAction = () => throw new InvalidOperationException("native component fixture");
            Throws<InvalidOperationException>(() => Invoke(failure, "OneTimeInitialisation"), "pending callback errors propagate");
            Check(Pending(failure).Count == 2 && throwing.Enters == 1 && ReferenceEquals(Get<FiniteStateMachine>(failure, "m_fsm"), failureMachine), "callback failure retains pending and selected FSM");
            throwing.EnterAction = null; Invoke(failure, "OneTimeInitialisation");
            Check(Pending(failure).Count == 0 && throwing.Enters == 3, "retry traverses complete retained pending list");
        }
        private static void VerifyFieldMetadata()
        {
            Type type = typeof(FiniteStateMachineBehaviour);
            FieldInfo[] fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fields.Select(f => f.Name).SequenceEqual(new[] { "m_fsmAsset", "m_overrideFSMName", "m_updateType", "m_fsm", "m_fsmUsers", "m_fsmUsersPendingInitialise" }), "original machine component field order");
            Check(fields[0].FieldType == typeof(FiniteStateMachineScriptableObject) && fields[1].FieldType == typeof(string)
                && fields[3].FieldType == typeof(FiniteStateMachine) && fields.Skip(4).All(f => f.IsInitOnly && f.FieldType == typeof(List<IGraphUser>)), "original machine component field types/retention");
            Check(fields.Take(3).All(f => f.GetCustomAttribute<SerializeField>() != null) && fields.Skip(3).All(f => f.GetCustomAttribute<SerializeField>() == null), "original machine component serialization boundary");
            Check(fields[0].GetCustomAttribute<TooltipAttribute>().tooltip == "Select the scriptable object that has the correct FSM"
                && fields[1].GetCustomAttribute<TooltipAttribute>().tooltip == "Override the default FSM used by the scriptable object - leave blank to use the default"
                && fields[2].GetCustomAttribute<TooltipAttribute>().tooltip == "Determines if FSM updates from Unity's Update or FixedUpdate", "original machine component authoring strings");
            Type update = fields[2].FieldType;
            Check(update.IsNestedPrivate && Enum.GetNames(update).SequenceEqual(new[] { "Update", "FixedUpdate", "All" })
                && Enum.GetValues(update).Cast<object>().Select(Convert.ToInt32).SequenceEqual(new[] { 0, 1, 2 }), "original private UpdateType values");
            fields = typeof(FSMUserBehaviour).GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fields.Select(f => f.Name).SequenceEqual(new[] { "m_finiteStateMachine", "m_initialiseUserOnEnable", "m_initialiseUserOnEnableIfNoActiveState", "m_clearUserOnDisable", "m_user" }), "original user component field order");
            Check(fields.Take(4).All(f => f.GetCustomAttribute<SerializeField>() != null) && fields[4].IsFamily && fields[4].FieldType == typeof(IGraphUser)
                && fields[4].GetCustomAttribute<SerializeField>() == null, "original user component serialized flags/protected user");
            Check(fields[1].GetCustomAttribute<HeaderAttribute>().header == "Advanced Settings"
                && fields[0].GetCustomAttribute<TooltipAttribute>().tooltip == "The FSM the user will use when 'Update' is called"
                && fields[1].GetCustomAttribute<TooltipAttribute>().tooltip == "If true the FSM user will be initialised - setting its initial state within the FSM"
                && fields[2].GetCustomAttribute<TooltipAttribute>().tooltip == "If true the FSM user will be initialised on enable, but only if the user does not already have an active state"
                && fields[3].GetCustomAttribute<TooltipAttribute>().tooltip == "If true the FSM user will be cleared from the FSM - any data used by the FSM will be removed, and the currently active state will be cleared", "original user component authoring strings");
            fields = typeof(FSMUserBehaviourGameObject).GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fields.Length == 1 && fields[0].Name == "<Storage>k__BackingField" && fields[0].FieldType == typeof(IGraphStorage)
                && fields[0].IsInitOnly && fields[0].GetCustomAttribute<SerializeField>() == null, "original self-user storage field contract");
        }
        private static void VerifySelectedUsers()
        {
            var component = Component<FiniteStateMachineBehaviour>(); var machine = Machine(); var state = new State(machine);
            Set(component, "m_fsm", machine); var user = new FSMUser();
            component.AddUser(user, false, true); Check(state.Enters == 1, "conditional initialize missing active state");
            component.AddUser(user, false, true); Check(state.Enters == 1 && Users(component).Count == 2, "existing active state skips conditional initialize");
            component.AddUser(user, true, false); Check(state.Enters == 2 && state.Leaves == 1, "forced initialize wins independently of conditional flag");
            component.AddUser(null, false, false); Check(Users(component).Last() == null, "no initialization avoids null user storage");
            Throws<NullReferenceException>(() => component.AddUser(null, false, true), "conditional null user storage fails after append");
            Check(Users(component).Count == 5, "failed initialization does not roll back list append");
            var asset = Asset(machine); var other = Machine(); new State(other);
            Set(asset, "m_fsmDictionary", new Dictionary<string, FiniteStateMachine> { { " ", other } });
            Set(component, "m_fsmAsset", asset); Set(component, "m_overrideFSMName", " "); Invoke(component, "OneTimeInitialisation");
            Check(ReferenceEquals(Get<FiniteStateMachine>(component, "m_fsm"), other), "whitespace override is a real name");
            Set(component, "m_overrideFSMName", ""); Invoke(component, "OneTimeInitialisation");
            Check(ReferenceEquals(Get<FiniteStateMachine>(component, "m_fsm"), machine), "empty override chooses asset default");
            Set(component, "m_overrideFSMName", "absent"); Invoke(component, "OneTimeInitialisation");
            Check(Get<FiniteStateMachine>(component, "m_fsm") == null, "missing override clears previous selected machine");
        }
        private static void VerifyUpdates()
        {
            var component = Component<FiniteStateMachineBehaviour>(); var first = Machine(); var firstState = new State(first);
            var second = Machine(); var secondState = new State(second); var a = new FSMUser(); var b = new FSMUser();
            first.InitialiseUser(a); first.InitialiseUser(b); second.InitialiseUser(a); second.InitialiseUser(b);
            component.AddUser(a, false, false); component.AddUser(b, false, false); Set(component, "m_fsm", first);
            firstState.UpdateAction = () => Set(component, "m_fsm", second); Invoke(component, "Update");
            Check(firstState.Updates == 1 && secondState.Updates == 1 && ReferenceEquals(secondState.LastUser, b), "current machine reread for every user");
            Check(firstState.LastContext.UpdateType == FSMUpdateType.Update && firstState.LastContext.DeltaTime == Time.deltaTime, "Update passes engine context");
            Invoke(component, "FixedUpdate"); Check(secondState.Updates == 1, "default type suppresses FixedUpdate");
            SetEnum(component, "m_updateType", 1); Invoke(component, "Update"); Check(secondState.Updates == 1, "FixedUpdate type suppresses Update");
            Invoke(component, "FixedUpdate");
            Check(secondState.Updates == 3 && secondState.LastContext.UpdateType == FSMUpdateType.FixedUpdate && secondState.LastContext.DeltaTime == Time.fixedDeltaTime, "FixedUpdate uses fixed engine context");
            SetEnum(component, "m_updateType", 2); Invoke(component, "Update"); Invoke(component, "FixedUpdate"); Check(secondState.Updates == 7, "All enables both callbacks");
            SetEnum(component, "m_updateType", 99); Invoke(component, "Update"); Invoke(component, "FixedUpdate"); Check(secondState.Updates == 7, "invalid authored update type suppresses callbacks");
            SetEnum(component, "m_updateType", 0); secondState.UpdateAction = () => component.AddUser(new FSMUser(), false, false);
            Throws<InvalidOperationException>(() => Invoke(component, "Update"), "live user-list mutation invalidates enumeration");
            Check(secondState.Updates == 8 && Users(component).Count == 3, "mutation stops before next user without rollback");
            secondState.UpdateAction = null; Set(component, "m_fsm", null); Invoke(component, "Update"); Check(secondState.Updates == 8, "no selected machine suppresses updates");
        }
        private static void VerifyStartAndRelease()
        {
            var component = Component<FiniteStateMachineBehaviour>(); var machine = Machine(); new State(machine); var asset = Asset(machine);
            Set(asset, "m_refCount", 1); Set(component, "m_fsmAsset", asset); var user = new FSMUser(); component.AddUser(user);
            var start = (IEnumerator)Invoke(component, "Start");
            Check(start.Current == null && Get<int>(asset, "m_refCount") == 1, "Start wrapper defers acquisition");
            Check(start.MoveNext() && start.Current is IEnumerator && Get<int>(asset, "m_refCount") == 1, "Start yields original acquisition iterator without enumerating it");
            var child = (IEnumerator)start.Current; Check(!child.MoveNext() && Get<int>(asset, "m_refCount") == 2, "nested acquisition executes independently");
            ((IDisposable)start).Dispose(); Check(!start.MoveNext() && ReferenceEquals(machine.GetActiveState(user), machine.DefaultState), "native no-op Dispose permits resumed selection/initialization");
            Check(ReferenceEquals(start.Current, child), "completed iterator retains yielded child");
            Throws<NotSupportedException>(() => start.Reset(), "iterator reset rejects restart");
            Invoke(component, "OnDestroy"); Check(Get<int>(asset, "m_refCount") == 1 && ReferenceEquals(Get<FiniteStateMachine>(component, "m_fsm"), machine), "destroy release retains selected machine");
            var swapped = Component<FiniteStateMachineBehaviour>(); var before = Asset(machine); Set(before, "m_refCount", 1);
            var otherMachine = Machine(); new State(otherMachine); var after = Asset(otherMachine);
            Set(swapped, "m_fsmAsset", before); var changed = (IEnumerator)Invoke(swapped, "Start"); Check(changed.MoveNext(), "replacement fixture first yield");
            Set(swapped, "m_fsmAsset", after); Check(!((IEnumerator)changed.Current).MoveNext(), "yielded child retains original asset");
            Check(!changed.MoveNext() && ReferenceEquals(Get<FiniteStateMachine>(swapped, "m_fsm"), otherMachine) && Get<int>(before, "m_refCount") == 2 && Get<int>(after, "m_refCount") == 0, "resume selects current asset after original acquisition");
            Invoke(swapped, "OnDestroy"); Check(Get<int>(before, "m_refCount") == 2 && Get<int>(after, "m_refCount") == -1, "destroy releases current asset independently of acquisition");
            var neverStarted = Component<FiniteStateMachineBehaviour>(); var empty = Asset(); Set(neverStarted, "m_fsmAsset", empty);
            Invoke(neverStarted, "OnDestroy"); Check(Get<int>(empty, "m_refCount") == -1, "destroy without acquisition preserves original underflow");
            var absent = Component<FiniteStateMachineBehaviour>(); var missing = (IEnumerator)Invoke(absent, "Start");
            Check(!missing.MoveNext() && missing.Current == null && !missing.MoveNext(), "missing asset completes diagnostic branch");
            Throws<NullReferenceException>(() => Invoke(absent, "OneTimeInitialisation"), "direct selection does not repair absent asset");
        }
        private static void VerifyUserLifecycle()
        {
            var component = Component<FSMUserBehaviour>();
            Check(Get<bool>(component, "m_initialiseUserOnEnable") && Get<bool>(component, "m_initialiseUserOnEnableIfNoActiveState") && Get<bool>(component, "m_clearUserOnDisable"), "original user component flags default true");
            var first = (IGraphUser)Invoke(component, "AcquireUserInstance"); var second = (IGraphUser)Invoke(component, "AcquireUserInstance");
            Check(!ReferenceEquals(first, second) && !ReferenceEquals(first.Storage, second.Storage) && ((FSMStorage)first.Storage).StateHistoryMaxCount == 5, "fresh default user and storage per acquisition");
            Invoke(component, "Awake"); var retained = Get<IGraphUser>(component, "m_user"); var key = new GraphStorageKey("Lucid component lifecycle value");
            retained.Storage.SetValue(key, 4); Invoke(component, "ReleaseUserInstance");
            Check(ReferenceEquals(Get<IGraphUser>(component, "m_user"), retained) && retained.Storage.GetCollection().Count == 0, "release destroys storage without clearing user field");
            Set(component, "m_user", null); Invoke(component, "ReleaseUserInstance"); Invoke(component, "OnEnable"); Invoke(component, "OnDisable"); Check(Get<IGraphUser>(component, "m_user") == null, "unset target/user lifecycle is safe");
            var target = Component<FiniteStateMachineBehaviour>(); Set(component, "m_finiteStateMachine", target); Set(component, "m_user", retained);
            Set(component, "m_initialiseUserOnEnable", false); Set(component, "m_initialiseUserOnEnableIfNoActiveState", false);
            Invoke(component, "OnEnable"); Check(Users(target).Count == 1 && ReferenceEquals(Users(target)[0], retained) && Pending(target).Count == 0, "enable forwards retained user and both flags");
            Invoke(component, "OnDisable"); Check(Users(target).Count == 0, "disable forwards removal");
            UnityEngine.Object.DestroyImmediate(target.gameObject); Invoke(component, "OnEnable"); Invoke(component, "OnDisable"); Check(true, "destroyed target uses Unity-null guard");
            var self = Component<FSMUserBehaviourGameObject>(); Invoke(self, "Awake");
            Check(ReferenceEquals(Get<IGraphUser>(self, "m_user"), self) && ((FSMStorage)self.Storage).StateHistoryMaxCount == 5, "game-object user acquires itself and original default storage");
            self.Storage.SetValue(key, 9); self.DestroyUser(); Check(self.Storage.GetCollection().Count == 0, "self user clears retained storage");
            self.Storage.SetValue(key, 10); Invoke(self, "OnDestroy"); Check(self.Storage.GetCollection().Count == 0 && ReferenceEquals(Get<IGraphUser>(self, "m_user"), self), "virtual destroy routes through retained self user");
        }
        private static FieldInfo Field(object owner, string name)
        {
            for (Type type = owner.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            throw new MissingFieldException(owner.GetType().FullName, name);
        }
        private static T Get<T>(object owner, string name)
        {
            object value = Field(owner, name).GetValue(owner);
            if (typeof(T) == typeof(int) && value != null && value.GetType().IsEnum) return (T)(object)Convert.ToInt32(value);
            return (T)value;
        }
        private static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
        private static void SetEnum(object owner, string name, int value) => Set(owner, name, Enum.ToObject(Field(owner, name).FieldType, value));
        private static object Invoke(object owner, string name)
        {
            MethodInfo method = null;
            for (Type type = owner.GetType(); type != null && method == null; type = type.BaseType)
                method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            try { return method.Invoke(owner, null); }
            catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        }
        private static void Check(bool condition, string message) { ++checks; if (!condition) throw new InvalidOperationException("FSM component verification failed: " + message); }
        private static void Throws<T>(Action action, string message) where T : Exception { try { action(); } catch (T) { Check(true, message); return; } throw new InvalidOperationException("FSM component verification failed: " + message); }
    }
}
#endif
