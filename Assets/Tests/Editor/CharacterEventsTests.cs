using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CharacterEvents (TypeDef 0x02000329, x86-64 0x760EF0..0x763500) with its
    // compiler-generated closures (0x0200032a..0x0200032c). The component is real, on an owned host that never
    // wakes; Awake is called by the case, because CharacterTrigger resolves its CharacterManager there. A
    // CharacterManager is built by its real constructor and registered, and the character is a real component
    // whose gravity is supplied. Members that need a live body, the camera, the track manager or physics are
    // witnessed on the compiled bodies instead: the order of their calls and the constants they load.
    public sealed class CharacterEventsTests
    {
        private static void WithEvents(Action<Owned, CharacterManager, CharacterGravityProbe, CharacterEvents> body) =>
            CharacterTriggerTestSupport.WithManager((owned, manager, unused) =>
            {
                CharacterGravityProbe character = NewGravityCharacter(owned, Vector3.down);
                GameObject host = owned.Host("Lucid character events", false);
                host.AddComponent<BoxCollider>();
                var events = host.AddComponent<CharacterEvents>();
                // Awake reads these two before it wires anything; the Editor does not build serialized events here.
                Set(events, "m_onTriggerLevelActivatedEvents", new UnityEvent());
                Set(events, "m_onTriggerTakeDamageEvents", new UnityEvent());
                Call(events, "Awake");
                SetCurrent(manager, character, true);
                // CharacterTrigger.TryGetCharacter also asks whether the character is active.
                events.SetTriggerWhenInactive(true);
                body(owned, manager, character, events);
            });

        private static void AssertType(Type type, int typeFlags, Type baseType, int[] options,
            (string name, int flags)[] fields, (string name, int flags)[] methods)
        {
            AssertMembers(type, typeFlags, fields, methods);
            AssertIl2CppOptions(type, options);
            Assert.That(type.BaseType, Is.EqualTo(baseType), type.FullName);
        }

        private static MethodInfo Method(Type type, string name, int parameters = -1) =>
            type.GetMethods(Declared).Single(method => method.Name == name && (parameters < 0 || method.GetParameters().Length == parameters));

        private static string[] CallNames(MethodInfo method) => OriginalILCallSequence.Names(OriginalILCallSequence.Calls(method));

        // The opcodes of the instructions directly before the single call to callee.
        private static OpCode[] LoadsBefore(MethodInfo method, string callee, int count)
        {
            var instructions = OriginalILCallSequence.Instructions(method);
            var call = OriginalILCallSequence.Calls(method).Single(candidate => candidate.Target.Name == callee);
            int index = instructions.FindIndex(instruction => instruction.Offset == call.Offset);
            return instructions.Skip(index - count).Take(count).Select(instruction => instruction.Code).ToArray();
        }

        private sealed class ProbeHomingAbility : ActorAbility, IHomingAbility
        {
            public int Resets;
            public override AbilityDefinition DefinitionBase => null;
            protected override void DoOnEnter() { }
            protected override void DoOnLeave() { }
            public void Reset() => Resets++;
            public bool CanActivate => false;
            public bool IsTriggered { get; set; }
            public bool OverridesTargeting => false;
            public bool ShouldProjectFromCamera() => false;
            public float GetAdditionalSpeed(float time) => 0f;
            public CharacterAbilityDefinition_Targeting TargetingDefinition => null;
        }

        private sealed class ProbeAbilityDefinition : CharacterAbilityDefinition
        {
            public int CachedValueCalls;
            public override Type ScriptType => typeof(ActorAbility);
            public override void CalculateCachedValues() => CachedValueCalls++;
        }

        // The declarations come from the original DummyDll metadata, nested compiler types included: their
        // names follow from the 23 serialized fields and the methods declared before each lambda.
        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsMethodsAndClosures()
        {
            AssertType(typeof(CharacterEvents), 0x00100001, typeof(CharacterTrigger), new[] { 2, 1 },
                new[]
                {
                    ("m_onTriggerEnableEvents", 0x0001), ("m_onTriggerDisableEvents", 0x0001), ("m_onTriggerEnterEvents", 0x0001),
                    ("m_onTriggerStayEvents", 0x0001), ("m_onTriggerStayDelayTime", 0x0001), ("m_onTriggerStayDelayedEvents", 0x0001),
                    ("m_onTriggerExitEvents", 0x0001), ("m_onTriggerTakeDamageEvents", 0x0001), ("m_onTriggerLevelActivatedEvents", 0x0001),
                    ("m_speedCurve", 0x0001), ("m_onTriggerSpeedEvents", 0x0001), ("m_targetTransform", 0x0001),
                    ("m_targetUseCameraForward", 0x0001), ("m_targetUseClosestPoint", 0x0001), ("m_targetRaycastDistance", 0x0001),
                    ("m_targetRaycastAngle", 0x0001), ("m_targetUseRibbonOrientation", 0x0001), ("m_abilityTypes", 0x0001),
                    ("m_abilityTypeGroup", 0x0001), ("m_levelManagerRef", 0x0001), ("m_controlsHandle", 0x0001),
                    ("m_targetingHandle", 0x0001), ("m_onTriggerStayDelayCoroutine", 0x0001)
                },
                new[]
                {
                    ("Awake", 0x00c4), ("OnDestroy", 0x0081), ("OnEnable", 0x0081), ("OnDisable", 0x0081), ("DoTriggerEnter", 0x00c4),
                    ("DoTriggerStay", 0x00c4), ("DoTriggerStayDelayed", 0x0081), ("DoTriggerExit", 0x00c4), ("OnLevelActivated", 0x0081),
                    ("OnCharacterTakeDamage", 0x0081), ("AirAbilitiesExit", 0x0086), ("AirAbilitiesExit", 0x0086),
                    ("AirAbilitiesReset", 0x0086), ("AirAbilitiesReset", 0x0086), ("AirAbilitiesRecharge", 0x0086),
                    ("AirAbilitiesRecharge", 0x0086), ("ResetSpinDashCharge", 0x0086), ("ResetSpinDashCharge", 0x0086),
                    ("ResetCharacterForm", 0x0086), ("TryResetCharacterForm", 0x0086), ("SetControlsEnabled", 0x0086),
                    ("SetTargetingEnabled", 0x0086), ("SetTargetingInnerVolumeOnly", 0x0086), ("SetAbilityDefinitionOverride", 0x0086),
                    ("LevelExit", 0x0086), ("MissionPauseTimers", 0x0086), ("TriggerCharacterAnimationEnter", 0x0086),
                    ("TriggerCharacterAnimationExit", 0x0086), ("TriggerCharacterSpeedEvents", 0x0086), ("ResetTransform", 0x0086),
                    ("CalculateTarget", 0x0081), ("SetTransformPosition", 0x0086), ("SetTransformRotation", 0x0086),
                    ("SetTransform", 0x0086), ("SetTransformRotationOnPlane", 0x0086), ("OverrideEnabledAbilities", 0x0086),
                    ("SetAbilityUpdateEnabled", 0x0091), ("EndImpulses", 0x0086), (".ctor", 0x1886),
                    ("<Awake>b__23_0", 0x0081), ("<Awake>b__23_1", 0x0081)
                });
            Type closures = typeof(CharacterEvents).GetNestedType("<>c", BindingFlags.NonPublic);
            AssertMembers(closures, 0x00102103,
                new[] { ("<>9", 0x0036), ("<>9__36_0", 0x0016), ("<>9__36_1", 0x0016) },
                new[] { (".cctor", 0x1891), (".ctor", 0x1886), ("<AirAbilitiesReset>b__36_0", 0x0083), ("<AirAbilitiesReset>b__36_1", 0x0083) });
            AssertMembers(typeof(CharacterEvents).GetNestedType("<>c__DisplayClass28_0", BindingFlags.NonPublic), 0x00100103,
                new[] { ("<>4__this", 0x0006), ("character", 0x0006) },
                new[] { (".ctor", 0x1886), ("<DoTriggerStay>b__0", 0x0083) });
            AssertMembers(typeof(CharacterEvents).GetNestedType("<>c__DisplayClass38_0", BindingFlags.NonPublic), 0x00100103,
                new[] { ("isExit", 0x0006) },
                new[] { (".ctor", 0x1886), ("<AirAbilitiesRecharge>b__0", 0x0083) });

            FieldInfo targetPoint = Field(typeof(CharacterEvents), "m_targetUseClosestPoint");
            Assert.That(AttributeTypes(targetPoint), Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(Tooltip(targetPoint), Is.EqualTo("Sets target location to closest point on ."));
            Assert.That(AttributeTypes(Field(typeof(CharacterEvents), "m_onTriggerEnableEvents")),
                Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(AttributeTypes(Field(typeof(CharacterEvents), "m_onTriggerDisableEvents")),
                Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(Tooltip(Field(typeof(CharacterEvents), "m_targetTransform")),
                Is.EqualTo("Optional transform to target.  If not set target will be this game object's transform."));
        }

        // The constructor 0x7630B0 builds a linear speed curve from 0 to 100 (AnimationCurve.Linear, 0x7630f5),
        // stores the raycast distance 100 and angle 45 with one eight-byte write (0x763118, constant 0x29a7d20)
        // and an empty ability type list; every other field keeps its zero value.
        [Test]
        public void ConstructorWritesTheNativeDefaults()
        {
            WithEvents((owned, manager, character, events) =>
            {
                var curve = Get<AnimationCurve>(events, "m_speedCurve");
                Assert.That(curve.keys.Length, Is.EqualTo(2));
                Assert.That(curve.Evaluate(0f), Is.EqualTo(0f));
                Assert.That(curve.Evaluate(50f), Is.EqualTo(50f).Within(1e-4f));
                Assert.That(curve.Evaluate(100f), Is.EqualTo(100f));
                Assert.That(Get<float>(events, "m_targetRaycastDistance"), Is.EqualTo(100f));
                Assert.That(Get<float>(events, "m_targetRaycastAngle"), Is.EqualTo(45f));
                Assert.That(Get<List<ActorAbilityType>>(events, "m_abilityTypes"), Is.Empty);
                Assert.That(Get<float>(events, "m_onTriggerStayDelayTime"), Is.EqualTo(0f));
                Assert.That(Get<bool>(events, "m_targetUseCameraForward"), Is.False);
                Assert.That(Get<bool>(events, "m_targetUseClosestPoint"), Is.False);
                Assert.That(Get<bool>(events, "m_targetUseRibbonOrientation"), Is.False);
                Assert.That(Get<CharacterAbilityTypeGroup>(events, "m_abilityTypeGroup"), Is.Null);
            });
        }

        // Awake 0x760EF0 runs CharacterTrigger.Awake (0x760f72), which resolves the CharacterManager reference, and
        // only assigns the LevelManager reference inside the level-event branch (0x760fbb), so without
        // persistent level listeners the field stays unset.
        [Test]
        public void AwakeResolvesTheManagerThroughTheBaseAndWiresNothingWithoutListeners()
        {
            WithEvents((owned, manager, character, events) =>
            {
                Assert.That(Get<SystemRef<CharacterManager>>(events, "m_characterManagerRef").Get(), Is.SameAs(manager));
                Assert.That(Get<object>(events, "m_levelManagerRef"), Is.Null);
            });
        }

        // The wiring happens only for events with persistent listeners (GetPersistentEventCount, 0x760f80 and 0x76101f).
        // The level lambda <Awake>b__23_0 (0x763170) calls LevelManager.InvokeOnLevelActivated with the level callback and
        // true (edx = 1, 0x7631d7); <Awake>b__23_1 (0x7631f0) adds OnCharacterTakeDamage to the manager's event.
        [Test]
        public void AwakeAndDestroyWireTheLevelAndDamageEventsThroughTheirLambdas()
        {
            Assert.That(CallNames(Method(typeof(CharacterEvents), "Awake")), Is.EqualTo(new[]
            {
                "CharacterTrigger.Awake", "UnityEventBase.GetPersistentEventCount", "ProcessManager.GetSystemRef", "Action`1..ctor",
                "SystemRef`1.InvokeOnValid", "UnityEventBase.GetPersistentEventCount", "Action`1..ctor", "SystemRef`1.InvokeOnValid",
            }));

            MethodInfo level = Method(typeof(CharacterEvents), "<Awake>b__23_0");
            Assert.That(CallNames(level), Is.EqualTo(new[] { "Action`1..ctor", "LevelManager.InvokeOnLevelActivated" }));
            Assert.That(LoadsBefore(level, "InvokeOnLevelActivated", 1), Is.EqualTo(new[] { OpCodes.Ldc_I4_1 }));
            Assert.That(CallNames(Method(typeof(CharacterEvents), "<Awake>b__23_1")),
                Is.EqualTo(new[] { "Action..ctor", "CharacterManager.add_OnCharacterTakeDamage" }));

            Assert.That(CallNames(Method(typeof(CharacterEvents), "OnDestroy")), Is.EqualTo(new[]
            {
                "UnityEventBase.GetPersistentEventCount", "SystemRef`1.IsValid", "SystemRef`1.Get", "Action`1..ctor", "LevelManager.RemoveLevelActivatedAction",
                "UnityEventBase.GetPersistentEventCount", "SystemRef`1.IsValid", "SystemRef`1.Get", "Action..ctor", "CharacterManager.remove_OnCharacterTakeDamage",
            }));
        }

        // OnEnable 0x761300, OnDisable 0x761320, OnLevelActivated 0x7615C0 and OnCharacterTakeDamage 0x7615E0 each test
        // their event for null and invoke it; the enter, stay and exit callbacks 0x761340, 0x7613A0 and 0x761540 do the
        // same with the character, after the base callback.
        [Test]
        public void CallbacksInvokeTheirEventsAndTolerateUnsetOnes()
        {
            WithEvents((owned, manager, character, events) =>
            {
                foreach (string name in new[] { "m_onTriggerEnableEvents", "m_onTriggerDisableEvents", "m_onTriggerLevelActivatedEvents",
                             "m_onTriggerTakeDamageEvents", "m_onTriggerEnterEvents", "m_onTriggerStayEvents", "m_onTriggerExitEvents",
                             "m_onTriggerStayDelayedEvents" })
                    Set(events, name, null);
                Call(events, "OnEnable");
                Call(events, "OnDisable");
                Call(events, "OnLevelActivated", new object[] { null });
                Call(events, "OnCharacterTakeDamage");
                Call(events, "DoTriggerEnter", character);
                Call(events, "DoTriggerStay", character);
                Call(events, "DoTriggerStayDelayed", character);
                Call(events, "DoTriggerExit", character);

                var log = new List<string>();
                UnityEvent Plain(string name)
                {
                    var plain = new UnityEvent();
                    plain.AddListener(() => log.Add(name));
                    Set(events, name, plain);
                    return plain;
                }
                UnityEvent<Character> Typed(string name)
                {
                    var withCharacter = new UnityEvent<Character>();
                    withCharacter.AddListener(argument => log.Add(name + ":" + (argument == character)));
                    Set(events, name, withCharacter);
                    return withCharacter;
                }
                Plain("m_onTriggerEnableEvents");
                Plain("m_onTriggerDisableEvents");
                Plain("m_onTriggerLevelActivatedEvents");
                Plain("m_onTriggerTakeDamageEvents");
                Typed("m_onTriggerEnterEvents");
                Typed("m_onTriggerStayEvents");
                Typed("m_onTriggerExitEvents");
                Typed("m_onTriggerStayDelayedEvents");

                Call(events, "OnEnable");
                Call(events, "OnDisable");
                Call(events, "OnLevelActivated", new object[] { null });
                Call(events, "OnCharacterTakeDamage");
                Call(events, "DoTriggerEnter", character);
                Call(events, "DoTriggerStay", character);
                Call(events, "DoTriggerStayDelayed", character);
                Call(events, "DoTriggerExit", character);

                Assert.That(log, Is.EqualTo(new[]
                {
                    "m_onTriggerEnableEvents", "m_onTriggerDisableEvents", "m_onTriggerLevelActivatedEvents", "m_onTriggerTakeDamageEvents",
                    "m_onTriggerEnterEvents:True", "m_onTriggerStayEvents:True", "m_onTriggerStayDelayedEvents:True", "m_onTriggerExitEvents:True",
                }));
            });
        }

        // DoTriggerStay 0x7613A0 raises the stay events, then returns when the delay time is zero (ucomiss, 0x761462) or a
        // delayed call is already pending (+0xe8, 0x761469); otherwise it schedules DoTriggerStayDelayed after the
        // delay time (CoroutineUtils.Delay, 0x7614b6) and keeps the coroutine. The coroutine host here is inactive, so
        // the engine refuses it and logs the error each request produces.
        [Test]
        public void StayRequestsTheDelayedCallOnlyWithADelayTime()
        {
            WithEvents((owned, manager, character, events) =>
            {
                int stays = 0;
                var stay = new UnityEvent<Character>();
                stay.AddListener(argument => stays++);
                Set(events, "m_onTriggerStayEvents", stay);

                Call(events, "DoTriggerStay", character);
                Assert.That(stays, Is.EqualTo(1));
                Assert.That(Get<object>(events, "m_onTriggerStayDelayCoroutine"), Is.Null, "no delay time: nothing is scheduled");

                Set(events, "m_onTriggerStayDelayTime", 0.5f);
                LogAssert.Expect(LogType.Error, new Regex("Coroutine couldn't be started"));
                Call(events, "DoTriggerStay", character);
                Assert.That(stays, Is.EqualTo(2), "the stay events come first");
                Assert.That(Get<object>(events, "m_onTriggerStayDelayCoroutine"), Is.Null, "the refused coroutine leaves nothing pending");

                var exited = new UnityEvent<Character>();
                Set(events, "m_onTriggerExitEvents", exited);
                Call(events, "DoTriggerExit", character);
                Assert.That(Get<object>(events, "m_onTriggerStayDelayCoroutine"), Is.Null);
            });
        }

        // The closure <DoTriggerStay>b__0 (0x7634a0) captures the character and the component and raises the delayed events
        // (+0x78) with that character.
        [Test]
        public void TheDelayedCallRaisesTheDelayedEventsWithTheCapturedCharacter()
        {
            WithEvents((owned, manager, character, events) =>
            {
                var received = new List<Character>();
                var delayed = new UnityEvent<Character>();
                delayed.AddListener(received.Add);
                Set(events, "m_onTriggerStayDelayedEvents", delayed);
                Type closure = typeof(CharacterEvents).GetNestedType("<>c__DisplayClass28_0", BindingFlags.NonPublic);
                object instance = Activator.CreateInstance(closure);
                closure.GetField("<>4__this").SetValue(instance, events);
                closure.GetField("character").SetValue(instance, character);

                closure.GetMethod("<DoTriggerStay>b__0", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(instance, null);

                Assert.That(received, Is.EqualTo(new[] { character }));
            });
        }

        private static void SeedBrain(CharacterGravityProbe character, out CharacterBrain brain)
        {
            brain = new CharacterBrain_Replay();
            Set(character, "m_brain", brain);
        }

        private static bool Queued(CharacterBrain brain, GameAction action) => brain.ActionStateQueued.GetValue(action);

        // AirAbilitiesExit 0x761600 and 0x761620 end the character's air-activate impulse only (EndAirActiveImpulse,
        // 0x735060), for the current character or the one passed.
        [Test]
        public void AirAbilitiesExitEndsOnlyTheAirActivateImpulse()
        {
            WithEvents((owned, manager, character, events) =>
            {
                SeedBrain(character, out CharacterBrain brain);
                void Start()
                {
                    Call(brain, "SetState", GameAction.CharacterAirActivate, true);
                    Call(brain, "SetState", GameAction.CharacterJump, true);
                    Assert.That(Queued(brain, GameAction.CharacterAirActivate), Is.True);
                }

                Start();
                events.AirAbilitiesExit();
                Assert.That(Queued(brain, GameAction.CharacterAirActivate), Is.False, "the current character");
                Assert.That(Queued(brain, GameAction.CharacterJump), Is.True, "other impulses stay");

                Call(brain, "SetState", GameAction.CharacterAirActivate, true);
                var other = NewGravityCharacter(owned, Vector3.down);
                SeedBrain(other, out CharacterBrain otherBrain);
                Call(otherBrain, "SetState", GameAction.CharacterAirActivate, true);
                events.AirAbilitiesExit(other);
                Assert.That(Queued(otherBrain, GameAction.CharacterAirActivate), Is.False, "the character passed");
                Assert.That(Queued(brain, GameAction.CharacterAirActivate), Is.True, "not the current one");
            });
        }

        // ResetSpinDashCharge 0x761A40/0x761A60 call CharacterSpinDashRoll.DisableCharge: roll and spin-dash-charge impulses
        // end, the rolling flag is removed (the form is not locked) and the charge time is stored as zero.
        [Test]
        public void ResetSpinDashChargeDisablesTheCharge()
        {
            WithEvents((owned, manager, character, events) =>
            {
                SeedBrain(character, out CharacterBrain brain);
                typeof(Character).GetProperty("SpinDashRoll").GetSetMethod(true).Invoke(character, new object[] { new CharacterSpinDashRoll(character) });
                void Prepare()
                {
                    Call(brain, "SetState", GameAction.CharacterRoll, true);
                    Call(brain, "SetState", GameAction.CharacterSpinDashCharge, true);
                    Call(brain, "SetState", GameAction.CharacterJump, true);
                    character.Storage.SetValue<bool>(ActorFSMKeys.RollingActive, true);
                    character.Storage.SetValue<float>(ActorFSMKeys.SpinDashChargeTime, 5f);
                }

                foreach (bool withCharacter in new[] { false, true })
                {
                    Prepare();
                    if (withCharacter) events.ResetSpinDashCharge(character);
                    else events.ResetSpinDashCharge();
                    Assert.That(Queued(brain, GameAction.CharacterRoll), Is.False, "roll ended " + withCharacter);
                    Assert.That(Queued(brain, GameAction.CharacterSpinDashCharge), Is.False, "charge ended " + withCharacter);
                    Assert.That(Queued(brain, GameAction.CharacterJump), Is.True, "jump stays " + withCharacter);
                    Assert.That(character.Storage.TryGetValue(ActorFSMKeys.RollingActive, out bool rolling), Is.False, "rolling removed " + withCharacter);
                    Assert.That(character.Storage.TryGetValue(ActorFSMKeys.SpinDashChargeTime, out float charge), Is.True);
                    Assert.That(charge, Is.EqualTo(0f), "charge time " + withCharacter);
                }
            });
        }

        // AirAbilitiesReset 0x761650 returns for a destroyed or absent character (op_Equality, 0x761705); otherwise it
        // calls Leave on every CharacterAbility_Air (lambda 0x7633e0), Reset on every IHomingAbility (lambda 0x763400,
        // interface slot 0), disables the spin dash charge (0x761881) and stores true under AirAbilitiesReset
        // (SetValue<bool>, ecx = 1, 0x761937).
        [Test]
        public void AirAbilitiesResetResetsHomingAbilitiesAndPublishesTheFlag()
        {
            WithEvents((owned, manager, character, events) =>
            {
                SeedBrain(character, out CharacterBrain brain);
                typeof(Character).GetProperty("SpinDashRoll").GetSetMethod(true).Invoke(character, new object[] { new CharacterSpinDashRoll(character) });
                var homing = new ProbeHomingAbility();
                Get<Dictionary<ActorAbilityType, ActorAbility>>(character, "m_abilities").Add(ActorAbilityType.Character_AirStompAttack, homing);
                character.Storage.SetValue<float>(ActorFSMKeys.SpinDashChargeTime, 5f);

                events.AirAbilitiesReset(null);
                Assert.That(homing.Resets, Is.EqualTo(0), "an absent character does nothing");
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.AirAbilitiesReset, out bool flag), Is.False);

                events.AirAbilitiesReset(character);
                Assert.That(homing.Resets, Is.EqualTo(1));
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.AirAbilitiesReset, out flag), Is.True);
                Assert.That(flag, Is.True);
                Assert.That(character.Storage.GetValue<float>(ActorFSMKeys.SpinDashChargeTime, -1f, false), Is.EqualTo(0f), "the charge was disabled");

                events.AirAbilitiesReset();
                Assert.That(homing.Resets, Is.EqualTo(2), "the current character");
            });
        }

        // The lambdas of AirAbilitiesReset and AirAbilitiesRecharge call ActorAbility.Leave, IHomingAbility.Reset and
        // CharacterAbility_Air.Recharge(isExit) with the captured flag (0x763500: movzx eax, [rdi + 0x10]); the overloads
        // without a character fetch the current one first.
        [Test]
        public void AbilityLambdasAndOverloadsCallTheOriginalTargets()
        {
            Type closures = typeof(CharacterEvents).GetNestedType("<>c", BindingFlags.NonPublic);
            Assert.That(CallNames(Method(closures, "<AirAbilitiesReset>b__36_0")), Is.EqualTo(new[] { "ActorAbility.Leave" }));
            Assert.That(CallNames(Method(closures, "<AirAbilitiesReset>b__36_1")), Is.EqualTo(new[] { "IHomingAbility.Reset" }));
            Type recharge = typeof(CharacterEvents).GetNestedType("<>c__DisplayClass38_0", BindingFlags.NonPublic);
            Assert.That(CallNames(Method(recharge, "<AirAbilitiesRecharge>b__0")), Is.EqualTo(new[] { "CharacterAbility_Air.Recharge" }));

            Assert.That(CallNames(Method(typeof(CharacterEvents), "AirAbilitiesExit", 0)),
                Is.EqualTo(new[] { "CharacterTrigger.get_Character", "Character.EndAirActiveImpulse" }));
            Assert.That(CallNames(Method(typeof(CharacterEvents), "AirAbilitiesExit", 1)), Is.EqualTo(new[] { "Character.EndAirActiveImpulse" }));
            Assert.That(CallNames(Method(typeof(CharacterEvents), "AirAbilitiesReset", 0)),
                Is.EqualTo(new[] { "CharacterTrigger.get_Character", "CharacterEvents.AirAbilitiesReset" }));
            Assert.That(CallNames(Method(typeof(CharacterEvents), "AirAbilitiesRecharge", 1)),
                Is.EqualTo(new[] { "CharacterTrigger.get_Character", "CharacterEvents.AirAbilitiesRecharge" }));
            Assert.That(CallNames(Method(typeof(CharacterEvents), "AirAbilitiesRecharge", 2)).Last(), Is.EqualTo("Actor.GetAbilitiesOfType"));
            Assert.That(CallNames(Method(typeof(CharacterEvents), "ResetSpinDashCharge", 0)),
                Is.EqualTo(new[] { "CharacterTrigger.get_Character", "Character.get_SpinDashRoll", "CharacterSpinDashRoll.DisableCharge" }));
        }

        // ResetCharacterForm 0x761A80 switches to the default form (esi = 0x23e4013a) without locking and clearing the lock
        // (edx = 0, ecx = 1); TryResetCharacterForm 0x761AB0 passes false for both (ecx = 0).
        [Test]
        public void FormResetsSwitchToTheDefaultFormWithTheNativeLockArguments()
        {
            Assert.That((int)ActorFormType.Default, Is.EqualTo(0x23e4013a));
            MethodInfo reset = Method(typeof(CharacterEvents), "ResetCharacterForm");
            MethodInfo tryReset = Method(typeof(CharacterEvents), "TryResetCharacterForm");
            Assert.That(CallNames(reset), Is.EqualTo(new[] { "CharacterTrigger.get_Character", "Character.SwitchToForm" }));
            Assert.That(CallNames(tryReset), Is.EqualTo(new[] { "CharacterTrigger.get_Character", "Character.SwitchToForm" }));
            Assert.That(LoadsBefore(reset, "SwitchToForm", 3), Is.EqualTo(new[] { OpCodes.Ldc_I4, OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_1 }));
            Assert.That(LoadsBefore(tryReset, "SwitchToForm", 3), Is.EqualTo(new[] { OpCodes.Ldc_I4, OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_0 }));
        }

        private static int ControlsEnabled => (int)GameplayModifierType.ControlsEnabled;
        private static int TargetingAvailable => (int)GameplayModifierType.TargetingAvailable;

        // SetControlsEnabled 0x761AD0: disabling adds a false ControlsEnabled override (esi = 0xc22df46d, edx = 0, 0x761b4d)
        // and keeps its handle; enabling removes the held override and clears the handle (0x761b1b, 0x761b20) without a
        // null test, so enabling with nothing held reaches RemoveModifierOverrides with a null handle.
        [Test]
        public void ControlsAreDisabledByAFalseOverrideAndEnabledByRemovingIt()
        {
            WithEvents((owned, manager, character, events) =>
            {
                StackableData modifiers = Modifiers(character);
                Assert.Throws<ArgumentNullException>(() => events.SetControlsEnabled(true), "nothing held");

                events.SetControlsEnabled(false);
                Assert.That(Get<StackableDataHandle>(events, "m_controlsHandle"), Is.Not.Null);
                Assert.That(modifiers.GetStack<bool>(ControlsEnabled, new List<bool>()), Is.EqualTo(new[] { false }));

                events.SetControlsEnabled(true);
                Assert.That(Get<StackableDataHandle>(events, "m_controlsHandle"), Is.Null);
                Assert.That(modifiers.HasData(ControlsEnabled), Is.False);
            });
        }

        // SetTargetingEnabled 0x761B70: enabling releases the held TargetingAvailable override (0x761bb0) and clears the handle;
        // disabling adds a false override (esi = 0x9d7e35f5, 0x761bfe) only when none is held (0x761bd6), and enabling with
        // nothing held changes nothing.
        [Test]
        public void TargetingHoldsAtMostOneFalseOverride()
        {
            WithEvents((owned, manager, character, events) =>
            {
                StackableData modifiers = Modifiers(character);
                events.SetTargetingEnabled(true);
                Assert.That(modifiers.HasData(TargetingAvailable), Is.False, "nothing to release");
                Assert.That(Get<StackableDataHandle>(events, "m_targetingHandle"), Is.Null);

                events.SetTargetingEnabled(false);
                StackableDataHandle held = Get<StackableDataHandle>(events, "m_targetingHandle");
                Assert.That(held, Is.Not.Null);
                Assert.That(modifiers.GetStack<bool>(TargetingAvailable, new List<bool>()), Is.EqualTo(new[] { false }));

                events.SetTargetingEnabled(false);
                Assert.That(Get<StackableDataHandle>(events, "m_targetingHandle"), Is.SameAs(held), "a second disable keeps the first override");
                Assert.That(modifiers.GetStack<bool>(TargetingAvailable, new List<bool>()).Count, Is.EqualTo(1));

                events.SetTargetingEnabled(true);
                Assert.That(Get<StackableDataHandle>(events, "m_targetingHandle"), Is.Null);
                Assert.That(modifiers.HasData(TargetingAvailable), Is.False);
            });
        }

        // SetTargetingInnerVolumeOnly 0x761C20 stores true under HomingTargetInnerVolumeOnly (SetValue<bool>, ecx = 1) or
        // removes the key (RemoveValue<bool>).
        [Test]
        public void InnerVolumeTargetingIsAStoredFlag()
        {
            WithEvents((owned, manager, character, events) =>
            {
                events.SetTargetingInnerVolumeOnly(true);
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.HomingTargetInnerVolumeOnly, out bool value), Is.True);
                Assert.That(value, Is.True);
                events.SetTargetingInnerVolumeOnly(false);
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.HomingTargetInnerVolumeOnly, out value), Is.False);
            });
        }

        // SetAbilityDefinitionOverride 0x761E00 returns for an absent definition (op_Equality, 0x761e67); otherwise it recalculates
        // the definition's cached values through its virtual slot 5 (0x761e8c) and stores it under AbilityDefinitionOverride.
        [Test]
        public void AnAbilityDefinitionOverrideIsRecalculatedAndStored()
        {
            WithEvents((owned, manager, character, events) =>
            {
                var definition = owned.Track(ScriptableObject.CreateInstance<ProbeAbilityDefinition>());
                definition.CachedValueCalls = 0;

                events.SetAbilityDefinitionOverride(null);
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.AbilityDefinitionOverride, out CharacterAbilityDefinition stored), Is.False);

                events.SetAbilityDefinitionOverride(definition);
                Assert.That(definition.CachedValueCalls, Is.EqualTo(1));
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.AbilityDefinitionOverride, out stored), Is.True);
                Assert.That(stored, Is.SameAs(definition));
            });
        }

        // TriggerCharacterSpeedEvents 0x762150 evaluates the speed curve at the character's world speed (+0xd0) and sends
        // the result to the speed events.
        [Test]
        public void SpeedEventsReceiveTheCurveValueAtTheCharactersSpeed()
        {
            WithEvents((owned, manager, character, events) =>
            {
                var received = new List<float>();
                var speed = new UnityEvent<float>();
                speed.AddListener(received.Add);
                Set(events, "m_onTriggerSpeedEvents", speed);
                Set(events, "m_speedCurve", AnimationCurve.Linear(0f, 0f, 50f, 1f));
                SetActorProperty(character, "WorldVelocityMagnitude", 25f);

                events.TriggerCharacterSpeedEvents();

                Assert.That(received, Is.EqualTo(new[] { 0.5f }).Within(1e-5f));
            });
        }

        // ResetTransform 0x7621C0 zeroes the local position and the local rotation of the target transform, which is the
        // optional one when set (op_Inequality, 0x762211) and this component's own otherwise.
        [Test]
        public void ResetTransformReturnsTheTargetToItsParent()
        {
            WithEvents((owned, manager, character, events) =>
            {
                Transform own = events.transform;
                own.localPosition = new Vector3(1f, 2f, 3f);
                own.localRotation = Quaternion.Euler(10f, 20f, 30f);
                events.ResetTransform();
                Assert.That(own.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(Quaternion.Angle(own.localRotation, Quaternion.identity), Is.EqualTo(0f).Within(1e-4f));

                Transform target = owned.Host("Lucid events target", false).transform;
                target.localPosition = new Vector3(4f, 5f, 6f);
                target.localRotation = Quaternion.Euler(40f, 50f, 60f);
                own.localPosition = new Vector3(1f, 2f, 3f);
                Set(events, "m_targetTransform", target);
                events.ResetTransform();
                Assert.That(target.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(Quaternion.Angle(target.localRotation, Quaternion.identity), Is.EqualTo(0f).Within(1e-4f));
                Assert.That(own.localPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)), "the component's own transform is untouched");
            });
        }

        private static (Vector3 worldForward, Vector3 targetForward, Transform targetTransform) Target(
            CharacterEvents events, Character character)
        {
            object[] arguments = { character, null, null, null };
            Call(events, "CalculateTarget", arguments);
            return ((Vector3)arguments[1], (Vector3)arguments[2], (Transform)arguments[3]);
        }

        // CalculateTarget 0x7622C0: the world forward is the camera's (+0x2c0, selected by cmovne, 0x76237d) or the
        // character's own forward direction (+0xa4); the target forward is that vector flattened onto the plane of the
        // character's up axis (WorldUp, 0x7623a2) and normalised, zero when nothing is left (0x7624fc). The target
        // transform is the optional one or this component's own (0x762324).
        [Test]
        public void TargetIsTheFlattenedForwardOfTheCameraOrTheCharacter()
        {
            WithEvents((owned, manager, character, events) =>
            {
                SetActorProperty(character, "ForwardDirection", new Vector3(0.6f, 0.8f, 0f));
                Set(character, "m_cameraForward", new Vector3(0f, 0f, -2f));

                var own = Target(events, character);
                Assert.That(own.worldForward, Is.EqualTo(new Vector3(0.6f, 0.8f, 0f)));
                Assert.That(own.targetForward.x, Is.EqualTo(1f).Within(1e-6f));
                Assert.That(own.targetForward.y, Is.EqualTo(0f).Within(1e-6f));
                Assert.That(own.targetForward.z, Is.EqualTo(0f).Within(1e-6f));
                Assert.That(own.targetTransform, Is.SameAs(events.transform));

                Set(events, "m_targetUseCameraForward", true);
                var camera = Target(events, character);
                Assert.That(camera.worldForward, Is.EqualTo(new Vector3(0f, 0f, -2f)), "the camera forward is passed on unnormalised");
                Assert.That(camera.targetForward.z, Is.EqualTo(-1f).Within(1e-6f));
                Assert.That(camera.targetForward.x, Is.EqualTo(0f).Within(1e-6f));

                SetActorProperty(character, "ForwardDirection", Vector3.up);
                Set(events, "m_targetUseCameraForward", false);
                Transform target = owned.Host("Lucid events calculated target", false).transform;
                Set(events, "m_targetTransform", target);
                var degenerate = Target(events, character);
                Assert.That(degenerate.worldForward, Is.EqualTo(Vector3.up));
                Assert.That(degenerate.targetForward, Is.EqualTo(Vector3.zero), "a forward along up has nothing left on the plane");
                Assert.That(degenerate.targetTransform, Is.SameAs(target));
            });
        }

        // SetTransformRotationOnPlane 0x762C10 takes the plane transform's up and the target's forward flattened onto it
        // (the vector stays when the plane normal is shorter than Epsilon, 0x762d06) and looks along the result with
        // that up.
        [Test]
        public void RotationOnAPlaneLooksAlongTheFlattenedForwardWithThePlaneUp()
        {
            WithEvents((owned, manager, character, events) =>
            {
                Transform plane = owned.Host("Lucid events plane", false).transform;
                plane.rotation = Quaternion.Euler(0f, 0f, -90f);
                events.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

                events.SetTransformRotationOnPlane(plane);

                Vector3 forward = events.transform.forward;
                Vector3 up = events.transform.up;
                Assert.That(forward.x, Is.EqualTo(0f).Within(1e-5f));
                Assert.That(forward.y, Is.EqualTo(0f).Within(1e-5f));
                Assert.That(forward.z, Is.EqualTo(1f).Within(1e-5f), "the forward was flattened onto the plane");
                Assert.That(up.x, Is.EqualTo(1f).Within(1e-5f));
                Assert.That(up.y, Is.EqualTo(0f).Within(1e-5f));
                Assert.That(up.z, Is.EqualTo(0f).Within(1e-5f), "the plane's up");

                Transform target = owned.Host("Lucid events plane target", false).transform;
                Set(events, "m_targetTransform", target);
                target.rotation = Quaternion.identity;
                events.transform.rotation = Quaternion.identity;
                events.SetTransformRotationOnPlane(plane);
                Assert.That(target.up.x, Is.EqualTo(1f).Within(1e-5f), "the optional target is turned");
                Assert.That(events.transform.up.y, Is.EqualTo(1f).Within(1e-5f), "the component's own transform is left as it was");
            });
        }

        // OverrideEnabledAbilities 0x762D70 does nothing without a current character; with one it sets the update flag
        // (ActorAbility +0x12, 0x762eb1 and 0x762f75) of every listed ability type, then of every type in the optional
        // group, skipping types the character has no ability for.
        [Test]
        public void AbilityUpdatesAreSwitchedForTheListedTypesAndTheGroup()
        {
            WithEvents((owned, manager, character, events) =>
            {
                var abilities = Get<Dictionary<ActorAbilityType, ActorAbility>>(character, "m_abilities");
                ProbeHomingAbility listed = new ProbeHomingAbility(), grouped = new ProbeHomingAbility(), other = new ProbeHomingAbility();
                foreach (ProbeHomingAbility ability in new[] { listed, grouped, other }) ability.UpdateEnabled = true;
                abilities.Add(ActorAbilityType.Character_AirStompAttack, listed);
                abilities.Add(ActorAbilityType.Character_BrakingRunning, grouped);
                abilities.Add(ActorAbilityType.Character_BurstAttackGround, other);
                Get<List<ActorAbilityType>>(events, "m_abilityTypes").Add(ActorAbilityType.Character_AirStompAttack);
                Get<List<ActorAbilityType>>(events, "m_abilityTypes").Add(ActorAbilityType.Character_Jump);

                SetCurrent(manager, character, false);
                events.OverrideEnabledAbilities(false);
                Assert.That(listed.UpdateEnabled, Is.True, "no current character");

                SetCurrent(manager, character, true);
                events.OverrideEnabledAbilities(false);
                Assert.That(listed.UpdateEnabled, Is.False);
                Assert.That(grouped.UpdateEnabled, Is.True, "no group yet");
                Assert.That(other.UpdateEnabled, Is.True);

                var group = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityTypeGroup>());
                var definition = new CharacterAbilityTypeGroup.AbilityTypeDefinition();
                Set(definition, "m_type", ActorAbilityType.Character_BrakingRunning);
                group.Abilities.Add(definition);
                Set(events, "m_abilityTypeGroup", group);
                events.OverrideEnabledAbilities(false);
                Assert.That(grouped.UpdateEnabled, Is.False);
                Assert.That(other.UpdateEnabled, Is.True, "an unlisted type is untouched");

                events.OverrideEnabledAbilities(true);
                Assert.That(listed.UpdateEnabled, Is.True);
                Assert.That(grouped.UpdateEnabled, Is.True);
                Assert.That(other.UpdateEnabled, Is.True);
            });
        }

        // The remaining actions call, in order: SetWorldVelocity(zero) then ExitLevel(false, false) for LevelExit (0x761fe1,
        // 0x762024); SetTimersPaused for MissionPauseTimers (0x76209c); the animation trigger's Action_OnEnter/OnLeave with
        // the current character (0x7620f7, 0x762147); Character.EndImpulses for the found character (0x7630a5). The
        // camera, ribbon and physics members need engine state, so the calls they make are witnessed: one raycast and two
        // closest-point queries for the position, and one ribbon lookup, one flattening and two look rotations for the rotation.
        [Test]
        public void ActionBodiesMakeTheNativeCalls()
        {
            Assert.That(CallNames(Method(typeof(CharacterEvents), "LevelExit")), Is.EqualTo(new[]
            {
                "CharacterTrigger.get_Character", "Vector3.get_zero", "Actor.SetWorldVelocity", "ProcessManager.GetSystem", "LevelManager.ExitLevel",
            }));
            Assert.That(LoadsBefore(Method(typeof(CharacterEvents), "LevelExit"), "ExitLevel", 2), Is.EqualTo(new[] { OpCodes.Ldc_I4_0, OpCodes.Ldc_I4_0 }));
            Assert.That(CallNames(Method(typeof(CharacterEvents), "MissionPauseTimers")),
                Is.EqualTo(new[] { "ProcessManager.GetSystem", "MissionManager.SetTimersPaused" }));
            Assert.That(CallNames(Method(typeof(CharacterEvents), "EndImpulses")),
                Is.EqualTo(new[] { "CharacterTrigger.TryGetCharacter", "Character.EndImpulses" }));
            foreach ((string name, string target) in new[] { ("TriggerCharacterAnimationEnter", "Action_OnEnter"), ("TriggerCharacterAnimationExit", "Action_OnLeave") })
            {
                var calls = OriginalILCallSequence.Calls(Method(typeof(CharacterEvents), name));
                Assert.That(OriginalILCallSequence.Names(calls), Is.EqualTo(new[] { "CharacterTrigger.get_Character", "ActorAnimationTrigger`1." + target }), name);
                Assert.That(calls[1].Target.GetParameters().Single().ParameterType, Is.EqualTo(typeof(Character)), "the actor overload");
            }

            string[] position = CallNames(Method(typeof(CharacterEvents), "SetTransformPosition"));
            Assert.That(position.Count(name => name == "Collider.Raycast"), Is.EqualTo(1));
            Assert.That(position.Count(name => name == "Collider.ClosestPoint"), Is.EqualTo(2));
            Assert.That(position.Count(name => name == "Quaternion.AngleAxis"), Is.EqualTo(1));
            Assert.That(position.Count(name => name == "Transform.set_position"), Is.EqualTo(1));
            Assert.That(position.Count(name => name == "CharacterEvents.CalculateTarget"), Is.EqualTo(1));

            string[] rotation = CallNames(Method(typeof(CharacterEvents), "SetTransformRotation"));
            Assert.That(rotation.Count(name => name == "Component.TryGetComponent"), Is.EqualTo(1));
            Assert.That(rotation.Count(name => name == "IRibbon.FindNearestRibbonLocation"), Is.EqualTo(1));
            Assert.That(rotation.Count(name => name == "RibbonLocation.GetLocalTransform"), Is.EqualTo(1));
            Assert.That(rotation.Count(name => name == "Vector3.ProjectOnPlane"), Is.EqualTo(1));
            Assert.That(rotation.Count(name => name == "Quaternion.LookRotation"), Is.EqualTo(2));
            Assert.That(rotation.Count(name => name == "Transform.set_rotation"), Is.EqualTo(2));

            Assert.That(CallNames(Method(typeof(CharacterEvents), "SetTransform")),
                Is.EqualTo(new[] { "CharacterEvents.SetTransformPosition", "CharacterEvents.SetTransformRotation" }));
        }
    }
}
