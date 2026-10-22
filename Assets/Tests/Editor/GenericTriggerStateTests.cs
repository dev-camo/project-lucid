using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.WorldTriggersShared;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.CameraManagerClientSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.TriggerConditionTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using CameraType = HardlightProject.CameraType;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime generic FSM trigger states: FSMState_TriggerTimedCamera (TypeDef 0x02000694, x86-64
    // 0x586480..0x5868a2), FSMState_TriggerTimeScale (0x02000697, 0x586910..0x587372), FSMState_TriggerFullscreenShader,
    // FSMStateOverrideGamePlayControl (0x0200067d, 0x583D50..0x5842c8) and ApplicationFSMBoolEventTrigger (0x020000ac).
    // The states are built by their ConstructInstance factories from node JSON, entered through IFSMState, and act on real
    // collaborators: a CinemachineCameraManager with mixing cameras (camera priority shows the active override), a TimeManager
    // and a Character. The coroutine host OwnedCharacterManagers installs refuses coroutines, each logging the engine error
    // expected here, so the routines the states start are stepped directly. Not covered: starting a new fullscreen shader
    // override (it needs a FullscreenShaderManager with a shader material). The tests of the camera, character camera,
    // cutscene and screen controls trigger states, the crash report and load-time systems states and the post-level-load
    // reset state follow with the contributor's restorations of those types.
    public sealed class GenericTriggerStateTests : OwnedGraphNamesFixture
    {
        public GenericTriggerStateTests() : base("Lucid-trigger-node-") { }

        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");
        private const int DefaultStart = 10, RailStart = 20;

        private static string Name() => "Lucid-trigger-node-" + Guid.NewGuid().ToString("N");

        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);

        private static FSMUser User() => new FSMUser(new FSMStorage(0));

        // ActiveEventType: 0 triggers on entry, 2 on entry and the end on leaving.
        private static string Json(int eventType, string fields = "") =>
            "{\"ActiveEventType\":" + eventType + (fields.Length == 0 ? "" : "," + fields) + "}";

        private sealed class Cameras
        {
            internal Rig Rig;
            internal CinemachineMixingCamera Default;
            internal CinemachineMixingCamera Rail;

            internal void AssertPriorities(int expectedDefault, int expectedRail, string label) =>
                Assert.That((Default.Priority, Rail.Priority), Is.EqualTo((expectedDefault, expectedRail)), label);
        }

        private static void WithCameras(Action<Owned, Cameras> body, bool coroutineHost = false) => Owned.Run(owned =>
        {
            // Taken first, so it is released last: it owns the SaveManager row and the refusing coroutine host.
            if (coroutineHost) owned.Lease(new OwnedCharacterManagers());
            var cameras = new Cameras { Rig = NewRig(owned, !coroutineHost, true) };
            cameras.Default = AddCamera(owned, cameras.Rig, CameraType.Default, DefaultStart);
            cameras.Rail = AddCamera(owned, cameras.Rig, CameraType.Rail, RailStart);
            cameras.AssertPriorities(-1, -1, "registered with nothing on the camera stack");
            body(owned, cameras);
        });

        // A TriggerTimedCamera entered while no CinemachineCameraManager is registered does nothing.
        [Test]
        public void CameraTriggersDoNothingWithoutACameraManager()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CinemachineCameraManager>();
                FSMUser user = User();
                IFSMState timed = FSMState_TriggerTimedCamera.ConstructInstance(Machine(), Name(),
                    Json(0, "\"CameraType\":" + (int)CameraType.Rail + ",\"ShowForSeconds\":1.5,\"TimeCategory\":" + (int)TimeCategory.PlayerMovement));

                timed.OnEnter(user, null);

                Assert.That(user.Storage.GetCollection(), Is.Empty);
            });
        }

        private static string TimedJson(CameraType type, float seconds, TimeCategory category) =>
            Json(0, "\"CameraType\":" + (int)type + ",\"ShowForSeconds\":" + seconds.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                ",\"TimeCategory\":" + (int)category);

        // TriggerTimedCamera InvokeTriggerStart 0x586570 applies the camera type override (0x586629) and starts the removal
        // routine, which the refusing coroutine host rejects with one engine error here.
        [Test]
        public void TriggerTimedCameraAppliesItsOverrideOnEntry()
        {
            WithCameras((owned, cameras) =>
            {
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                IFSMState state = FSMState_TriggerTimedCamera.ConstructInstance(Machine(), Name(), TimedJson(CameraType.Rail, 1.5f, TimeCategory.PlayerMovement));

                state.OnEnter(User(), null);

                cameras.AssertPriorities(-1, RailStart, "rail applied");
            }, coroutineHost: true);
        }

        // RemoveOverrideRoutine (MoveNext 0x586780): the first step yields a fixed-seconds wait with the node's seconds and
        // category (WaitForFixedSeconds, 0x586875, after the state change at 0x586862) and removes nothing; the next step
        // looks the camera manager up again (GetSystemRef, 0x586814; IsValid, 0x586832) and removes the handle's override.
        [Test]
        public void TheTimedCameraRoutineWaitsThenRemovesTheOverride()
        {
            WithCameras((owned, cameras) =>
            {
                IFSMState state = FSMState_TriggerTimedCamera.ConstructInstance(Machine(), Name(), TimedJson(CameraType.Rail, 1.5f, TimeCategory.PlayerMovement));
                StackableDataHandle handle = cameras.Rig.Manager.ApplyCameraTypeOverride(CameraType.Rail);
                cameras.AssertPriorities(-1, RailStart, "rail applied");

                var steps = (IEnumerator)Call(state, "RemoveOverrideRoutine", handle, 1.5f, TimeCategory.PlayerMovement);
                cameras.AssertPriorities(-1, RailStart, "nothing runs before the first step");
                Assert.That(steps.MoveNext(), Is.True);
                (float seconds, object category) = WaitArguments(steps.Current);
                Assert.That(seconds, Is.EqualTo(1.5f));
                Assert.That(category, Is.EqualTo(TimeCategory.PlayerMovement));
                cameras.AssertPriorities(-1, RailStart, "still applied while waiting");
                Assert.That(steps.MoveNext(), Is.False);
                cameras.AssertPriorities(-1, -1, "removed after the wait");
            });
        }

        [Test]
        public void TheTimedCameraRoutineLeavesAGoneCameraManagerAlone()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CinemachineCameraManager>();
                IFSMState state = FSMState_TriggerTimedCamera.ConstructInstance(Machine(), Name(), TimedJson(CameraType.Rail, 0.5f, TimeCategory.PlayerMovement));

                var steps = (IEnumerator)Call(state, "RemoveOverrideRoutine", new StackableDataHandle(), 0.5f, TimeCategory.PlayerMovement);

                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(steps.MoveNext(), Is.False, "no manager, nothing to remove and no fault");
            });
        }

        private sealed class TimeScales
        {
            internal TimeScaleDefinition Definition;
            internal string DefinitionName;
            internal TimeManager Manager;
        }

        private static void WithTimeScale(float lastKeyTime, Action<Owned, TimeScales> body) => Owned.Run(owned =>
        {
            // Taken first, so it is released last: it owns the App, DataManager and SaveManager rows and the coroutine host.
            owned.Lease(new OwnedCharacterManagers());
            TimeManager timeManager = NewTimeManager(owned, TimeCategory.PlayerPhysics, 1f);
            owned.Register(timeManager);
            // TimeSetting.GetDefault (the routine's first step) reads the TimeCategoryConfiguration from the registered
            // SystemConfiguration and adds its global time category to the prioritised list.
            SystemConfiguration configuration = owned.Register(new SystemConfiguration());
            var categories = owned.Track(ScriptableObject.CreateInstance<TimeCategoryConfiguration>());
            var globalTime = owned.Track(ScriptableObject.CreateInstance<TimeCategoryObject>());
            Set(globalTime, "m_guid", "lucid-trigger-global-time"); // Reflection: serialized identifier with no setter, as NewTimeManager does.
            var serializedCategories = new SerializedObject(categories);
            serializedCategories.FindProperty("m_unityGlobalTime").objectReferenceValue = globalTime;
            serializedCategories.ApplyModifiedPropertiesWithoutUndo();
            configuration.StackableData.AddOverride(HLCRC32.GenerateInt(nameof(TimeCategoryConfiguration)), (SystemConfigurationAsset)categories);
            var scales = new TimeScales { DefinitionName = Name(), Manager = timeManager };
            scales.Definition = owned.Track(ScriptableObject.CreateInstance<TimeScaleDefinition>());
            scales.Definition.TimeCategory = TimeCategory.PlayerPhysics;
            scales.Definition.TimeScale = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(lastKeyTime, 0.25f));
            ProcessManager.GetSystem<DataManager>().TimeScaleDefinitions[scales.DefinitionName] = scales.Definition;
            body(owned, scales);
        });

        private static IFSMState TimeScaleState(TimeScales scales, int eventType) =>
            FSMState_TriggerTimeScale.ConstructInstance(Machine(), Name(), Json(eventType, "\"TimeScaleDefinitionName\":\"" + scales.DefinitionName + "\""));

        // Constructor 0x586910 resolves the named definition through DataManagerUtilities.TryGetTimeScaleDefinition
        // (0x5869a0); InvokeTriggerStart 0x586AC0 reads the user's active-time-scale table (GetValue, 0x586c4e), creates and
        // stores a new one only when absent (0x586cd8..0x586e19) and starts the routine either way (0x586ccc).
        [Test]
        public void TriggerTimeScaleCreatesTheActiveTableOnceAndStartsItsRoutine()
        {
            WithTimeScale(10f, (owned, scales) =>
            {
                IFSMState state = TimeScaleState(scales, 0);
                Assert.That(Get<TimeScaleDefinition>(state, "m_timeScaleDefinition"), Is.SameAs(scales.Definition));
                FSMUser user = User();
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);

                state.OnEnter(user, null);
                Assert.That(user.Storage.TryGetValue(FSMStorageKeys.TimeScalesActive, out Dictionary<TimeScaleDefinition, bool> table), Is.True);
                Assert.That(table, Is.Empty);

                state.OnEnter(user, null);
                Assert.That(user.Storage.GetValueOnly<Dictionary<TimeScaleDefinition, bool>>(FSMStorageKeys.TimeScalesActive), Is.SameAs(table));
            });
        }

        // InvokeTriggerEnd 0x586ED0 sets the definition's entry in the stored table to false (Dictionary set_Item, 0x587062).
        [Test]
        public void TriggerTimeScaleEndClearsItsActiveFlag()
        {
            WithTimeScale(10f, (owned, scales) =>
            {
                IFSMState state = TimeScaleState(scales, 2);
                FSMUser user = User();
                var table = new Dictionary<TimeScaleDefinition, bool> { { scales.Definition, true } };
                user.Storage.SetValue(FSMStorageKeys.TimeScalesActive, table);

                state.OnLeave(user, null);

                Assert.That(table[scales.Definition], Is.False);
            });
        }

        // UpdateTimeSettingRoutine (MoveNext 0x587090): the definition is flagged active (0x5871f0); while the last curve key's
        // time is greater than the accumulated time (ucomiss ja, 0x587255/0x587268) and the flag stays set (0x587296) the
        // routine applies a step and yields a WaitForFixedUpdate (0x58731d); otherwise it removes its time setting and
        // clears the flag (0x587337..0x58735c).
        [Test]
        public void TheTimeScaleRoutineRunsWhileTheCurveHasTimeLeftAndTheFlagIsSet()
        {
            WithTimeScale(10f, (owned, scales) =>
            {
                IFSMState state = TimeScaleState(scales, 0);
                var table = new Dictionary<TimeScaleDefinition, bool>();

                var steps = (IEnumerator)Call(state, "UpdateTimeSettingRoutine", table);
                Assert.That(table, Is.Empty, "nothing runs before the first step");
                // The expected scales are the curve (0,1)..(10,0.25) with zero tangents (a cubic Hermite, 1 - 0.75 * (3s^2 - 2s^3)
                // for s = t / 10) at t = one and two project fixed steps of 0.016667, worked out by hand.
                Assert.That(Time.fixedUnscaledDeltaTime, Is.EqualTo(0.016667f).Within(1e-6f), "the literals below assume the project fixed step");
                Assert.That(scales.Manager.GetTimescale(TimeCategory.PlayerPhysics), Is.EqualTo(1f), "the base scale before the routine runs");
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(steps.Current, Is.TypeOf<WaitForFixedUpdate>());
                Assert.That(table[scales.Definition], Is.True);
                Assert.That(scales.Manager.GetTimescale(TimeCategory.PlayerPhysics), Is.EqualTo(0.99999376f).Within(2e-7f),
                    "timer += fixedUnscaledDeltaTime (0x5872af) precedes the Evaluate and UpdateTimeSetting (0x5872e2..0x587307)");

                Assert.That(steps.MoveNext(), Is.True, "still running while flagged");
                Assert.That(scales.Manager.GetTimescale(TimeCategory.PlayerPhysics), Is.EqualTo(0.99997505f).Within(2e-7f), "the timer accumulates");

                table[scales.Definition] = false;
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(table[scales.Definition], Is.False);
                Assert.That(scales.Manager.GetTimescale(TimeCategory.PlayerPhysics), Is.EqualTo(1f), "RemoveTimeSetting (0x587337) lifts the slowdown");
            });
        }

        // A curve whose last key is at time zero has no time left (0 > 0 is false), so the first step finishes at once.
        [Test]
        public void TheTimeScaleRoutineFinishesAtOnceForAnEmptyCurve()
        {
            WithTimeScale(0f, (owned, scales) =>
            {
                var table = new Dictionary<TimeScaleDefinition, bool>();

                var steps = (IEnumerator)Call(TimeScaleState(scales, 0), "UpdateTimeSettingRoutine", table);

                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(table[scales.Definition], Is.False, "flagged active, then cleared");
            });
        }

        private static IFSMState OverrideControl(bool setOverride) =>
            FSMStateOverrideGamePlayControl.ConstructInstance(Machine(), Name(), "{\"Override\":" + (setOverride ? "true" : "false") + "}");

        private static bool ControlsEnabled(Character character) =>
            character.GetModifierValue<bool>((int)GameplayModifierType.ControlsEnabled);

        // DoOnEnter 0x583E20, on the CharacterManager's current character (GetSystem, 0x583ee0; character +0x38): without
        // an existing InputOverrideModifierHandle (HasValue, 0x583f2e) the override adds a controls-disabled override
        // (modifier 0xc22df46d, false, 0x584094) and stores its handle (0x5840d0..0x5841c5); with one it adjusts that handle
        // (0x58418c). Without the override a stored handle is removed (0x584217) together with its storage key (0x5842c8).
        [Test]
        public void OverrideGamePlayControlDisablesAndRestoresTheCurrentCharactersControls()
        {
            WithManager((owned, manager, character) =>
            {
                // Awake builds the modifier store (ControlsEnabled based true); the inactive host never runs it.
                Call(character, "InitialiseModifiers");
                SetCurrent(manager, character, true);
                FSMUser user = User();
                IFSMState on = OverrideControl(true);
                IFSMState off = OverrideControl(false);
                Assert.That(ControlsEnabled(character), Is.True);

                off.OnEnter(user, null);
                Assert.That(ControlsEnabled(character), Is.True, "nothing to restore");

                on.OnEnter(user, null);
                Assert.That(ControlsEnabled(character), Is.False);
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.InputOverrideModifierHandle, out StackableDataHandle handle), Is.True);

                on.OnEnter(user, null);
                Assert.That(ControlsEnabled(character), Is.False);
                Assert.That(character.Storage.GetValueOnly<StackableDataHandle>(ActorFSMKeys.InputOverrideModifierHandle), Is.SameAs(handle),
                    "the stored handle is adjusted, not replaced");

                off.OnEnter(user, null);
                Assert.That(ControlsEnabled(character), Is.True);
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.InputOverrideModifierHandle, out StackableDataHandle _), Is.False);
            });
        }

        // The user (the entering FSM user) is not read: the override applies to the manager's current character.
        [Test]
        public void OverrideGamePlayControlUsesTheManagersCharacterNotTheUser()
        {
            WithManager((owned, manager, character) =>
            {
                SetCurrent(manager, character, true);
                FSMUser user = User();

                OverrideControl(true).OnEnter(user, null);

                Assert.That(ControlsEnabled(character), Is.False);
                Assert.That(user.Storage.GetCollection(), Is.Empty);
            });
        }

        // A FullscreenShaderManager without a material, registered as the shipped system. AddComponent sends the editor Reset
        // and OnValidate, which read the TimeScaledComponent_SDT s_config lookup and would register a SystemConfiguration row
        // (GetConfig); an owned empty lookup stands in while it is added.
        private static FullscreenShaderManager NewShaderManager(Owned owned)
        {
            FieldInfo config = typeof(TimeScaledComponent_SDT).GetField("s_config_Internal", BindingFlags.Static | BindingFlags.NonPublic);
            object prior = config.GetValue(null);
            config.SetValue(null, owned.Track(ScriptableObject.CreateInstance<TimeCategoryLookup>()));
            FullscreenShaderManager manager;
            try { manager = owned.Host("Lucid fullscreen shader manager", false).AddComponent<FullscreenShaderManager>(); }
            finally { config.SetValue(null, prior); }
            return owned.Register(manager);
        }

        // FullscreenShader InvokeTriggerStart 0x585D70 looks the manager up first (0x585d9f; GetSystem faults on an absent
        // system, so one is registered) but, when the user's storage already holds a ShaderOverride handle (HasValue, 0x585f19;
        // jne 0x5860b0), never touches it and leaves the handle in place.
        [Test]
        public void TriggerFullscreenShaderKeepsAnExistingOverrideWithoutStartingAnother()
        {
            Owned.Run(owned =>
            {
                NewShaderManager(owned);
                FSMUser user = User();
                user.Storage.SetValue(FSMStorageKeys.ShaderOverride, new FullscreenShaderManager.ParametersHandle(null, 77));
                IFSMState state = FSMState_TriggerFullscreenShader.ConstructInstance(Machine(), Name(),
                    Json(0, "\"ShaderType\":\"" + FullscreenShaderParametersType.ChaosShift + "\",\"ShaderDuration\":0.75"));

                Assert.DoesNotThrow(() => state.OnEnter(user, null), "no override is started when one is held");

                Assert.That(user.Storage.GetValueOnly<FullscreenShaderManager.ParametersHandle>(FSMStorageKeys.ShaderOverride).InstanceID, Is.EqualTo(77));
            });
        }

        // Without a held handle InvokeTriggerStart 0x585D70 calls StartEffectOverride on the looked-up manager with no null
        // check. With no FullscreenShaderManager registered the lookup (GetSystem, 0x585d9f) already raises the engine's
        // NullReferenceException for the absent system, before the call, and no handle is stored.
        [Test]
        public void TriggerFullscreenShaderWithNeitherAHandleNorAManagerThrows()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<FullscreenShaderManager>(); // required absent or idle on entry, restored on release
                Assert.That(ProcessManager.GetSystemRef<FullscreenShaderManager>().IsNull(), Is.True);
                FSMUser user = User();
                IFSMState state = FSMState_TriggerFullscreenShader.ConstructInstance(Machine(), Name(),
                    Json(0, "\"ShaderType\":\"" + FullscreenShaderParametersType.ChaosShift + "\",\"ShaderDuration\":0.75"));

                Assert.Throws<NullReferenceException>(() => state.OnEnter(user, null));

                Assert.That(user.Storage.HasValue<FullscreenShaderManager.ParametersHandle>(FSMStorageKeys.ShaderOverride), Is.False);
            });
        }

        // Adds a void persistent call of `method` on `target` to the serialized UnityEvent (EditorAndRuntime, because a
        // RuntimeOnly call does not fire outside Play Mode).
        private static void AddPersistentCall(SerializedProperty unityEvent, Object target, string method)
        {
            SerializedProperty calls = unityEvent.FindPropertyRelative("m_PersistentCalls.m_Calls");
            calls.arraySize++;
            SerializedProperty call = calls.GetArrayElementAtIndex(calls.arraySize - 1);
            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = target.GetType().AssemblyQualifiedName;
            call.FindPropertyRelative("m_MethodName").stringValue = method;
            call.FindPropertyRelative("m_Mode").intValue = (int)PersistentListenerMode.Void;
            call.FindPropertyRelative("m_CallState").intValue = (int)UnityEventCallState.EditorAndRuntime;
        }

        // Constructors: each factory parses its node JSON into the fields the original constructor stores.
        [Test]
        public void TheFactoriesStoreTheNodeSettings()
        {
            IFSMState shader = FSMState_TriggerFullscreenShader.ConstructInstance(Machine(), Name(),
                Json(0, "\"ShaderType\":\"" + FullscreenShaderParametersType.ChaosShift + "\",\"ShaderDuration\":0.75"));
            Assert.That(Get<FullscreenShaderParametersType>(shader, "m_shaderType"), Is.EqualTo(FullscreenShaderParametersType.ChaosShift));
            Assert.That(Get<float>(shader, "m_shaderDuration"), Is.EqualTo(0.75f));

            IFSMState timed = FSMState_TriggerTimedCamera.ConstructInstance(Machine(), Name(), TimedJson(CameraType.Rail, 2.5f, TimeCategory.PlayerPhysics));
            Assert.That(Get<CameraType>(timed, "m_cameraType"), Is.EqualTo(CameraType.Rail));
            Assert.That(Get<float>(timed, "m_showForSeconds"), Is.EqualTo(2.5f));
            Assert.That(Get<TimeCategory>(timed, "m_timeCategory"), Is.EqualTo(TimeCategory.PlayerPhysics));
        }

        // ApplicationFSMBoolEventTrigger.Awake 0x534E40 reads the bool stored under the named key in the App's storage
        // (TryGetValue<bool>, 0x534f06) and invokes the matching UnityEvent (0x534f20); a missing key invokes neither.
        [Test]
        public void TheBoolEventTriggerRunsTheEventMatchingTheStoredApplicationValue()
        {
            foreach ((bool? stored, int expectedTrue, int expectedFalse) in new (bool?, int, int)[]
            {
                (true, 1, 0), (false, 0, 1), (null, 0, 0),
            })
            {
                Owned.Run(owned =>
                {
                    owned.Lease(new OwnedCharacterManagers());
                    App app = ProcessManager.GetSystem<App>();
                    if (stored.HasValue) app.Storage.SetValue(AppFSMKeys.LevelPlayed, stored.Value);
                    var trigger = owned.Host("Lucid bool event trigger", false).AddComponent<ApplicationFSMBoolEventTrigger>();
                    var onTrueProbe = owned.Host("Lucid true listener").AddComponent<EventInvocationTargetProbe>();
                    var onFalseProbe = owned.Host("Lucid false listener").AddComponent<EventInvocationTargetProbe>();
                    // Authored, serialized fields: the key and one persistent listener on each event.
                    var serialized = new SerializedObject(trigger);
                    serialized.FindProperty("m_applicationFSMKey").stringValue = "LevelPlayed";
                    AddPersistentCall(serialized.FindProperty("m_eventOnTrue"), onTrueProbe, nameof(EventInvocationTargetProbe.NoArguments));
                    AddPersistentCall(serialized.FindProperty("m_eventOnFalse"), onFalseProbe, nameof(EventInvocationTargetProbe.NoArguments));
                    serialized.ApplyModifiedPropertiesWithoutUndo();

                    Call(trigger, "Awake");

                    Assert.That((onTrueProbe.Calls.Count, onFalseProbe.Calls.Count), Is.EqualTo((expectedTrue, expectedFalse)), "stored " + (stored?.ToString() ?? "nothing"));
                });
            }
        }

    }
}
