using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.CompilerServices;
using Hardlight;
using Hardlight.UI.Binding;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static ProjectLucid.Tests.GameInputDeclarations;

namespace ProjectLucid.Tests
{
    // GameInputButtonClick (02000965) and GameActionButtonClick (02000962), against their 1.10.1
    // x86-64 bodies. Components sit on inactive owned GameObjects.
    // Not covered here, as each needs an engine coroutine or a current Character from a
    // CharacterManager: SetAction's start from a null handle (0x6892B2..0x689312) and stop of a
    // live one (0x689319..0x689337), and UpdateCoroutine's MoveNext (0x689660) past state 0.
    [TestFixture]
    public sealed class OriginalGameButtonClickTests
    {
        private static readonly GameInput Serialized = (GameInput)(int.MinValue + 0x4E21);
        private static readonly GameInput Override = (GameInput)(int.MinValue + 0x4E22);

        // A brain whose movement aggregate the test feeds through the original AddMovement.
        private sealed class MovementBrain : CharacterBrain
        {
            public void Move(float x, float y) => AddMovement(new Vector2(x, y), 1f);
            public override void Close() => throw new NotSupportedException();
        }

        // ---- GameInputButtonClick ----

        [Test]
        public void InputButtonDeclarationsKeepOriginalFlagsAndFieldOrder()
        {
            Type type = typeof(GameInputButtonClick);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            SetOptions(type, 2, 1);
            Fields(type, ("m_buttonToClick", typeof(Button), 1), ("m_inputToListenFor", typeof(GameInput), 1),
                ("m_inputTrigger", typeof(InputTrigger), 1), ("Input", typeof(Bindable<GameInput>), 0x26), ("m_inputOverridden", typeof(bool), 1));
            AttributeRows(type, "m_buttonToClick", typeof(SerializeField));
            AttributeRows(type, "m_inputToListenFor", typeof(SerializeField));
            AttributeRows(type, "m_inputTrigger", typeof(SerializeField));
            AttributeRows(type, "Input");
            Methods(type, ("OnEnable", 0x81), ("OnDisable", 0x81), ("OnInput", 0x81), ("SetInput", 0x86), (".ctor", 0x1886));
        }

        // .ctor 0x689EE0 writes m_inputTrigger = 1 (Down) and allocates Input with Bindable's
        // parameterless constructor.
        [Test]
        public void InputButtonConstructorSeedsDownAndAnEmptyBindable()
        {
            GameInputScope.Run(scope =>
            {
                var click = scope.Inactive("Lucid owned game input button click").AddComponent<GameInputButtonClick>();
                Assert.That(ModernUITestSupport.Get(click, "m_inputTrigger"), Is.EqualTo(InputTrigger.Down));
                Assert.That(click.Input, Is.Not.Null);
                Assert.That((int)click.Input.Value, Is.Zero);
                Assert.That(ModernUITestSupport.Get(click, "m_inputOverridden"), Is.False);
            });
        }

        private static GameInputButtonClick InputButton(GameInputScope scope, InputTrigger trigger)
        {
            var click = scope.Inactive("Lucid owned game input button click").AddComponent<GameInputButtonClick>();
            ModernUITestSupport.Set(click, "m_inputToListenFor", Serialized);
            ModernUITestSupport.Set(click, "m_inputTrigger", trigger);
            return click;
        }

        // OnEnable 0x689BC0 copies m_inputToListenFor into Input only without an override (+0x38)
        // and subscribes OnInput for Input.Value with m_inputTrigger; OnDisable 0x689CA0 removes it.
        [Test]
        public void InputButtonEnableSubscribesTheSerializedInputWithItsTrigger()
        {
            GameInputScope.Run(scope =>
            {
                GameInputButtonClick held = InputButton(scope, InputTrigger.Held);
                GameInputButtonClick up = InputButton(scope, InputTrigger.Up);
                OwnedControlInputs.Run(() =>
                {
                    ModernUITestSupport.Call(held, "OnEnable");
                    Assert.That(held.Input.Value, Is.EqualTo(Serialized));
                    Assert.That(ControlTables.Subscriptions(Serialized, held), Is.EqualTo(ControlTables.Expected(InputTrigger.Held, "GameInputButtonClick.OnInput")));
                    ModernUITestSupport.Call(up, "OnEnable");
                    Assert.That(ControlTables.Subscriptions(Serialized, up), Is.EqualTo(ControlTables.Expected(InputTrigger.Up, "GameInputButtonClick.OnInput")));

                    ModernUITestSupport.Call(held, "OnDisable");
                    ModernUITestSupport.Call(up, "OnDisable");
                    Assert.That(ControlTables.Subscriptions(Serialized, held), Is.Empty);
                    Assert.That(ControlTables.Subscriptions(Serialized, up), Is.Empty);
                }, Serialized);
            });
        }

        // SetInput 0x689D80 returns for the current value; otherwise it sets the override flag to
        // input != 0, substitutes m_inputToListenFor for 0, and moves the subscription (unsubscribe
        // the old Input.Value, store, subscribe the new one) whether or not the component is enabled.
        [Test]
        public void InputButtonSetInputMovesTheSubscriptionAndZeroRestoresTheSerializedInput()
        {
            GameInputScope.Run(scope =>
            {
                GameInputButtonClick click = InputButton(scope, InputTrigger.Down);
                string listener = "GameInputButtonClick.OnInput";
                OwnedControlInputs.Run(() =>
                {
                    click.SetInput(Override);
                    Assert.That(click.Input.Value, Is.EqualTo(Override));
                    Assert.That(ModernUITestSupport.Get(click, "m_inputOverridden"), Is.True);
                    Assert.That(ControlTables.Subscriptions(Override, click), Is.EqualTo(ControlTables.Expected(InputTrigger.Down, listener)), "subscribed while disabled");

                    ModernUITestSupport.Call(click, "OnEnable");
                    Assert.That(click.Input.Value, Is.EqualTo(Override), "the override survives enabling");
                    Assert.That(ControlTables.Subscriptions(Override, click), Is.EqualTo(ControlTables.Expected(InputTrigger.Down, listener)), "AddUnique keeps one entry");
                    Assert.That(ControlTables.Subscriptions(Serialized, click), Is.Empty);

                    ModernUITestSupport.Set(click, "m_inputTrigger", InputTrigger.Up);
                    click.SetInput(Override);
                    Assert.That(ControlTables.Subscriptions(Override, click), Is.EqualTo(ControlTables.Expected(InputTrigger.Down, listener)), "the same input changes nothing");

                    click.SetInput((GameInput)0);
                    Assert.That(click.Input.Value, Is.EqualTo(Serialized));
                    Assert.That(ModernUITestSupport.Get(click, "m_inputOverridden"), Is.False);
                    Assert.That(ControlTables.Subscriptions(Override, click), Is.Empty);
                    Assert.That(ControlTables.Subscriptions(Serialized, click), Is.EqualTo(ControlTables.Expected(InputTrigger.Up, listener)));

                    ModernUITestSupport.Call(click, "OnDisable");
                    Assert.That(ControlTables.Subscriptions(Serialized, click), Is.Empty);
                }, Serialized, Override);
            });
        }

        // OnInput 0x689D60 invokes m_buttonToClick.onClick (+0x100) whatever the value.
        [Test]
        public void InputButtonOnInputClicksTheButton()
        {
            GameInputScope.Run(scope =>
            {
                GameInputButtonClick click = InputButton(scope, InputTrigger.Down);
                var button = scope.Inactive("Lucid owned button").AddComponent<Button>();
                int clicks = 0;
                button.onClick.AddListener(() => clicks++);
                ModernUITestSupport.Set(click, "m_buttonToClick", button);
                ModernUITestSupport.Call(click, "OnInput", 0f);
                ModernUITestSupport.Call(click, "OnInput", 1f);
                Assert.That(clicks, Is.EqualTo(2));
            });
        }

        // ---- GameActionButtonClick ----

        [Test]
        public void ActionButtonDeclarationsKeepOriginalFlagsAndIteratorNames()
        {
            Type type = typeof(GameActionButtonClick);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            SetOptions(type, 2, 1);
            Fields(type, ("m_buttonToClick", typeof(Button), 1), ("m_actionToListenFor", typeof(GameAction), 1),
                ("m_actionComboToListenFor", typeof(GameAction), 1), ("m_minimumTime", typeof(float), 1), ("m_inputLockTime", typeof(float), 1),
                ("Action", typeof(Bindable<GameAction>), 0x26), ("ActionCombo", typeof(Bindable<GameAction>), 0x26),
                ("MinimumTime", typeof(Bindable<float>), 0x26), ("m_actionOverridden", typeof(bool), 1), ("m_updateCoroutine", typeof(Coroutine), 1),
                ("m_characterManagerRef", typeof(SystemRef<CharacterManager>), 0x21));
            AttributeRows(type, "m_minimumTime", typeof(SerializeField));
            AttributeRows(type, "m_inputLockTime", typeof(TooltipAttribute), typeof(SerializeField));
            AttributeRows(type, "Action");
            Assert.That(Tooltip(type, "m_inputLockTime"), Is.EqualTo(
                "When game action is triggered, inputs are locked for this period of time to ensure game logic also receives them."));
            Methods(type, ("OnEnable", 0x81), ("OnDisable", 0x81), ("SetAction", 0x86), ("SetActionCombo", 0x86), ("SetMinimumTime", 0x86),
                ("StartUpdateCoroutine", 0x81), ("UpdateCoroutine", 0x81), ("GetBrainState", 0x81), ("StopUpdateCoroutine", 0x81), (".ctor", 0x1886));

            Type iterator = Nested(type, "<UpdateCoroutine>d__17");
            Assert.That(type.GetMethod("UpdateCoroutine", Declared).GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType, Is.EqualTo(iterator));
            Assert.That((int)iterator.Attributes, Is.EqualTo(0x00100103));
            Fields(iterator, ("<>1__state", typeof(int), 1), ("<>2__current", typeof(object), 1),
                ("<>4__this", type, 6), ("<character>5__2", typeof(Character), 1));
        }

        // .ctor 0x6894F0 writes m_inputLockTime = 0x3dcccccd (0.1f), builds Action and ActionCombo
        // with Bindable(T) given 0 (None) and MinimumTime with Bindable(), then caches the
        // CharacterManager reference.
        [Test]
        public void ActionButtonConstructorSeedsTheLockTimeAndThreeBindables()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<CharacterManager>();
                var click = scope.Inactive("Lucid owned game action button click").AddComponent<GameActionButtonClick>();
                Assert.That(BitConverter.SingleToInt32Bits((float)ModernUITestSupport.Get(click, "m_inputLockTime")), Is.EqualTo(0x3dcccccd));
                Assert.That(click.Action.Value, Is.EqualTo(GameAction.None));
                Assert.That(click.ActionCombo.Value, Is.EqualTo(GameAction.None));
                Assert.That(click.MinimumTime.Value, Is.Zero);
                Assert.That(click.Action, Is.Not.SameAs(click.ActionCombo));
                Assert.That(ModernUITestSupport.Get(click, "m_characterManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<CharacterManager>()));
            });
        }

        // OnEnable 0x689020 copies all three serialized values into the bindables unless the
        // action was overridden (+0x50), and starts the coroutine only for a non-None action.
        [Test]
        public void ActionButtonEnableCopiesTheSerializedValuesUnlessOverridden()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<CharacterManager>();
                var click = scope.Inactive("Lucid owned game action button click").AddComponent<GameActionButtonClick>();
                ModernUITestSupport.Set(click, "m_actionComboToListenFor", GameAction.CharacterAirActivate);
                ModernUITestSupport.Set(click, "m_minimumTime", 0.75f);
                ModernUITestSupport.Call(click, "OnEnable");
                Assert.That(click.Action.Value, Is.EqualTo(GameAction.None));
                Assert.That(click.ActionCombo.Value, Is.EqualTo(GameAction.CharacterAirActivate));
                Assert.That(click.MinimumTime.Value, Is.EqualTo(0.75f));
                Assert.That(ModernUITestSupport.Get(click, "m_updateCoroutine"), Is.Null, "no coroutine for None");

                click.ActionCombo.Value = GameAction.None;
                click.MinimumTime.Value = 2f;
                ModernUITestSupport.Set(click, "m_actionOverridden", true);
                ModernUITestSupport.Call(click, "OnEnable");
                Assert.That(click.ActionCombo.Value, Is.EqualTo(GameAction.None), "an override keeps every bindable");
                Assert.That(click.MinimumTime.Value, Is.EqualTo(2f));
                Assert.DoesNotThrow(() => ModernUITestSupport.Call(click, "OnDisable"), "no coroutine to stop");
            });
        }

        // SetAction 0x689240 with None: the override flag is cleared and the serialized action
        // (None here) stored; a None result stops the (absent) coroutine. SetActionCombo 0x689350
        // returns for the current value, substitutes the serialized combo for None and never
        // touches the override flag. SetMinimumTime 0x6893A0 stores its value as given.
        [Test]
        public void ActionButtonSettersStoreValuesAndNoneRestoresTheSerializedCombo()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<CharacterManager>();
                var click = scope.Inactive("Lucid owned game action button click").AddComponent<GameActionButtonClick>();
                ModernUITestSupport.Set(click, "m_actionComboToListenFor", GameAction.CharacterAirActivate);
                ModernUITestSupport.Set(click, "m_actionOverridden", true);

                click.SetAction(GameAction.None);
                Assert.That(ModernUITestSupport.Get(click, "m_actionOverridden"), Is.True, "an unchanged action leaves the flag");
                click.Action.Value = GameAction.CharacterAirStompAttack;
                click.SetAction(GameAction.None);
                Assert.That(ModernUITestSupport.Get(click, "m_actionOverridden"), Is.False);
                Assert.That(click.Action.Value, Is.EqualTo(GameAction.None));
                Assert.That(ModernUITestSupport.Get(click, "m_updateCoroutine"), Is.Null);

                click.SetActionCombo(GameAction.CharacterJump);
                Assert.That(click.ActionCombo.Value, Is.EqualTo(GameAction.CharacterJump));
                click.SetActionCombo(GameAction.None);
                Assert.That(click.ActionCombo.Value, Is.EqualTo(GameAction.CharacterAirActivate));
                ModernUITestSupport.Set(click, "m_actionComboToListenFor", GameAction.CharacterRoll);
                click.SetActionCombo(GameAction.None);
                Assert.That(click.ActionCombo.Value, Is.EqualTo(GameAction.CharacterRoll), "the serialized combo is read at the call");
                Assert.That(ModernUITestSupport.Get(click, "m_actionOverridden"), Is.False);

                click.SetMinimumTime(-1.5f);
                Assert.That(click.MinimumTime.Value, Is.EqualTo(-1.5f));
                click.SetMinimumTime(float.NaN);
                Assert.That(click.MinimumTime.Value, Is.NaN);
            });
            LogAssert.NoUnexpectedReceived();
        }

        // SetAction 0x689240 with a non-None action marks the override (al = 1 at 0x689266) and
        // stores it; the inlined StartUpdateCoroutine then returns at 0x6892AC while m_updateCoroutine
        // (+0x58) is set, so no second coroutine starts and the live handle is kept. A StartCoroutine
        // on the inactive owner would log an error and replace the handle with null.
        [Test]
        public void ActionButtonSetActionKeepsALiveCoroutine()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<CharacterManager>();
                var click = scope.Inactive("Lucid owned game action button click").AddComponent<GameActionButtonClick>();
                ModernUITestSupport.Set(click, "m_actionToListenFor", GameAction.CharacterRoll);
                var live = (Coroutine)FormatterServices.GetUninitializedObject(typeof(Coroutine));
                // The handle has no engine coroutine behind it; its engine finalizer must not run.
                GC.SuppressFinalize(live);
                ModernUITestSupport.Set(click, "m_updateCoroutine", live);
                scope.Defer(() => ModernUITestSupport.Set(click, "m_updateCoroutine", null));

                click.SetAction(GameAction.CharacterJump);
                Assert.That(ModernUITestSupport.Get(click, "m_actionOverridden"), Is.True);
                Assert.That(click.Action.Value, Is.EqualTo(GameAction.CharacterJump), "the given action, not the serialized one");
                Assert.That(ModernUITestSupport.Get(click, "m_updateCoroutine"), Is.SameAs(live));

                click.SetAction(GameAction.CharacterJump);
                Assert.That(click.Action.Value, Is.EqualTo(GameAction.CharacterJump));
                Assert.That(ModernUITestSupport.Get(click, "m_updateCoroutine"), Is.SameAs(live), "an unchanged action still only checks the handle");
            });
            LogAssert.NoUnexpectedReceived();
        }

        // GetBrainState 0x689450 calls CacheBrainMovement first, then r15b = (ActionCombo == 0) and
        // actionHeld = GetBrainRawState(Action); a set combo with the action held tail-calls
        // GetBrainRawState(ActionCombo) (0x6894D4), otherwise it returns r15b & actionHeld
        // (0x6894D9). GetBrainRawState 0x735150 passes the 0.5 threshold (0x3f000000 at 0x29A6C08).
        // Each call's own CacheMovement consumes the aggregate fed just before it, so skipping it,
        // or caching after the reads, returns the wrong row; (0,1) with a combo separates
        // `hasCombo ? comboHeld : actionHeld` and `actionHeld || comboHeld` from the original.
        [Test]
        public void ActionButtonBrainStateNeedsTheActionAndAnySetCombo()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<CharacterManager>();
                // Character's m_gameplayIslandManagerRef initializer creates this row.
                scope.Row<GameplayIslandManager>();
                GameObject owner = scope.Inactive("Lucid owned game action button click");
                var click = owner.AddComponent<GameActionButtonClick>();
                Character character = owner.AddComponent<Character>();
                var brain = new MovementBrain();
                ModernUITestSupport.Set(character, "m_brain", brain);
                click.Action.Value = GameAction.CharacterMovementRight;

                bool State(float x, float y)
                {
                    brain.Move(x, y);
                    return (bool)ModernUITestSupport.Call(click, "GetBrainState", character);
                }

                Assert.That(State(1f, 0f), Is.True, "no combo, action held");
                Assert.That((bool)ModernUITestSupport.Call(click, "GetBrainState", character), Is.False, "the previous call consumed the aggregate");
                Assert.That(State(0f, 0f), Is.False, "no combo, nothing held");
                Assert.That(State(0f, 1f), Is.False, "no combo, only up held");

                click.ActionCombo.Value = GameAction.CharacterMovementUp;
                Assert.That(State(1f, 0f), Is.False, "combo set, only the action held");
                Assert.That(State(0f, 1f), Is.False, "combo set, only the combo held");
                Assert.That(State(1f, 1f), Is.True, "both held; (1,1) normalises to (0.707,0.707)");
                Assert.That(State(0f, 0f), Is.False, "combo set, nothing held");
            });
        }

        // MoveNext 0x689660 state 0 writes state -1 (0x689867), then calls m_characterManagerRef.Get()
        // (0x68988F) with no IsNull guard, unlike SupplyInput's at 0x5890DC. Without a system or a
        // logging host, SystemRef.Get raises through LogOrThrowException before any yield or click.
        [Test]
        public void ActionButtonCoroutineFaultsWithoutACharacterManager()
        {
            GameInputScope.Run(scope =>
            {
                VerificationFixtureOwnership.LifecycleRow row = scope.Row<CharacterManager>();
                Assert.That(row.BorrowsLiveSystem, Is.False);
                Assert.That(HLUnityCore.IsNull(), Is.True, "a live logging host would log instead of throwing");
                var click = scope.Inactive("Lucid owned game action button click").AddComponent<GameActionButtonClick>();
                var button = scope.Inactive("Lucid owned button").AddComponent<Button>();
                int clicks = 0;
                button.onClick.AddListener(() => clicks++);
                ModernUITestSupport.Set(click, "m_buttonToClick", button);

                var iterator = (IEnumerator)ModernUITestSupport.Call(click, "UpdateCoroutine");
                NullReferenceException error = Assert.Throws<NullReferenceException>(() => iterator.MoveNext());
                Assert.That(error.Message, Is.EqualTo("System '" + ProcessManager.GetDefaultName<CharacterManager>() + "' is null"));
                Assert.That(iterator.MoveNext(), Is.False, "the state was already -1");
                Assert.That(clicks, Is.Zero);
                Assert.That(ModernUITestSupport.Get(click, "m_updateCoroutine"), Is.Null);
            });
            LogAssert.NoUnexpectedReceived();
        }
    }
}
