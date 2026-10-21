using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using ProjectLucid.Tests.UISelectionShared;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // OnScreenButton (0200096c) and OnScreenControlGroupListenerButton (02000837) against their 1.10.1 x86-64 bodies
    // (0x68BB10..0x68C8B9, 0x6085A0..0x608718). The button is built on an owned object next to a real UIButton
    // probe (the button the inspector would assign). Edit Mode runs no Awake or OnDestroy for these components, so
    // the cases call them, and take the button off the owned ControlMapping again after Awake: the inputs it
    // supplies are recorded here and never reach the shared ControlMapping tables. The held-button cases use an
    // active owner and a CoroutineUtils host that starts real coroutines. Not covered: the frame-by-frame timing of
    // the held coroutine, which needs a running player loop; the cases step the iterator by hand instead (a fresh
    // one takes the same path as a resumed one: both states 0 and 1 run the loop of 0x68C770).
    public sealed class OnScreenUiButtonTests
    {
        private static readonly GameInput InputA = (GameInput)(int.MinValue + 0x4E21);
        private static readonly GameInput InputB = (GameInput)(int.MinValue + 0x4E22);

        private static readonly FieldInfo CoroutineHost =
            typeof(CoroutineUtils).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);

        private static string Row(string kind, GameInput input) => ButtonCallbackLog.Row(kind, 0, input, 1f, InputType.Touch);

        private sealed class Rig
        {
            public GameObject Owner;
            public OnScreenButton Button;
            public UIButtonProbe Ui;
            public ButtonCallbackLog Log;
            public ControlMapping Mapping;
        }

        // A button wired to a UIButton probe with fresh UnityEvents, supplying the given inputs on the given trigger.
        private static Rig NewRig(GameInputScope scope, InputTrigger trigger, params GameInput[] inputs)
        {
            var rig = new Rig { Mapping = scope.RegisterInputSystem(), Log = new ButtonCallbackLog() };
            rig.Ui = scope.Inactive("Lucid owned on-screen ui button").AddComponent<UIButtonProbe>();
            ModernUITestSupport.Set(rig.Ui, "m_events", new List<UISelectable.UIEventInfo>()); // serialized in the original
            // Interactable's change event vibrates through the reference that UISelectable.Awake caches.
            scope.Row<VibrationManager>();
            ModernUITestSupport.Call(typeof(UISelectable), rig.Ui, "Awake");
            rig.Ui.OnButtonDown = new UnityEvent();
            rig.Ui.OnButtonUp = new UnityEvent();
            rig.Ui.OnButtonExit = new UnityEvent();
            rig.Owner = scope.Inactive("Lucid owned on-screen button");
            rig.Button = rig.Owner.AddComponent<OnScreenButton>();
            ModernUITestSupport.Set(rig.Button, "m_button", rig.Ui); // serialized in the original
            ModernUITestSupport.Set(rig.Button, "m_inputToSupply", inputs); // serialized in the original
            ModernUITestSupport.Set(rig.Button, "m_inputTrigger", trigger); // serialized in the original
            return rig;
        }

        private static object Call(Rig rig, string name, params object[] arguments) =>
            ModernUITestSupport.Call(typeof(OnScreenButton), rig.Button, name, arguments);

        // Activates the owner and runs Awake as Unity does in play, then takes the provider registration back so the
        // supplied inputs stay local. OnDestroy, which also withdraws the input type listener, is deferred to the end.
        private static void Activate(GameInputScope scope, Rig rig)
        {
            rig.Owner.SetActive(true);
            Call(rig, "Awake");
            scope.Defer(() => Call(rig, "OnDestroy"));
            rig.Mapping.UnregisterButtonProvider(rig.Button);
            rig.Log.Attach(rig.Button);
        }

        private static int LastInputTypeSubscriptions(object target)
        {
            var handlers = (Action<InputType>)typeof(ControlMapping).GetField("OnUpdateLastInputType", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            return handlers.GetInvocationList().Count(handler => ReferenceEquals(handler.Target, target));
        }

        private static List<IInputButton> Providers(ControlMapping mapping) =>
            (List<IInputButton>)typeof(ControlMapping).GetField("m_buttonProviders", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mapping);

        // Reset 0x68BB10 takes the UIButton on the same object (GetComponent), null when there is none.
        [Test]
        public void ResetTakesTheUiButtonOnTheSameObject()
        {
            GameInputScope.Run(scope =>
            {
                GameObject owner = scope.Inactive("Lucid owned on-screen button");
                var ui = owner.AddComponent<UIButtonProbe>();
                var button = owner.AddComponent<OnScreenButton>();
                ModernUITestSupport.Set(button, "m_button", null);
                ModernUITestSupport.Call(typeof(OnScreenButton), button, "Reset");
                Assert.That(ModernUITestSupport.Get(button, "m_button"), Is.SameAs(ui));

                var alone = scope.Inactive("Lucid owned lone on-screen button").AddComponent<OnScreenButton>();
                ModernUITestSupport.Set(alone, "m_button", ui);
                ModernUITestSupport.Call(typeof(OnScreenButton), alone, "Reset");
                Assert.That(ModernUITestSupport.Get(alone, "m_button") == null, Is.True);
            });
        }

        // Awake 0x68BB60 registers the button as a provider with the input mapping, listens for the last input type, and
        // hooks the UIButton events its trigger needs: Held (0) down to begin the hold, up to end it and exit; Up (2)
        // up; Down (1) down; any other trigger throws ArgumentOutOfRangeException. OnDestroy 0x68BE70 withdraws the
        // registration, the type listener and the down and up listeners only. The press and release below happen on an inactive owner, so a hold
        // ends at once (0x68C1F1 tests activeInHierarchy).
        [Test]
        public void AwakeHooksTheEventsOfTheTriggerAndDestroyingWithdrawsTheRegistration()
        {
            foreach ((InputTrigger trigger, string[] onDown, string[] onUp) in new[]
            {
                (InputTrigger.Held, new[] { "down", "up" }, new string[0]),
                (InputTrigger.Down, new[] { "down" }, new string[0]),
                (InputTrigger.Up, new string[0], new[] { "up" }),
            })
            {
                GameInputScope.Run(scope =>
                {
                    Rig rig = NewRig(scope, trigger, InputA);
                    try
                    {
                        Call(rig, "Awake");
                        Assert.That(Providers(rig.Mapping), Is.EqualTo(new IInputButton[] { rig.Button }));
                        Assert.That(LastInputTypeSubscriptions(rig.Button), Is.EqualTo(1));
                        rig.Mapping.UnregisterButtonProvider(rig.Button);
                        rig.Log.Attach(rig.Button);

                        rig.Ui.OnButtonDown.Invoke();
                        Assert.That(rig.Log.Calls, Is.EqualTo(onDown.Select(kind => Row(kind, InputA)).ToArray()), trigger + " down");
                        rig.Log.Calls.Clear();
                        rig.Ui.OnButtonUp.Invoke();
                        Assert.That(rig.Log.Calls, Is.EqualTo(onUp.Select(kind => Row(kind, InputA)).ToArray()), trigger + " up");
                        rig.Log.Calls.Clear();
                        rig.Ui.OnButtonExit.Invoke();
                        Assert.That(rig.Log.Calls, Is.Empty, "the exit input is None");
                    }
                    finally
                    {
                        Call(rig, "OnDestroy");
                    }
                    Assert.That(Providers(rig.Mapping), Is.Empty);
                    Assert.That(LastInputTypeSubscriptions(rig.Button), Is.EqualTo(0));

                    // 0x68BF98 and 0x68BFE0 withdraw only the OnButtonDown and OnButtonUp listeners, so the hold
                    // listeners the Held trigger added stay on the UIButton: its press still begins (and, on this
                    // inactive owner, ends) a hold, while the Down and Up triggers have nothing left to hear.
                    rig.Log.Calls.Clear();
                    rig.Ui.OnButtonDown.Invoke();
                    rig.Ui.OnButtonUp.Invoke();
                    Assert.That(rig.Log.Calls, Is.EqualTo(trigger == InputTrigger.Held ? new[] { Row("down", InputA), Row("up", InputA) } : new string[0]), trigger + " after destroy");
                });
            }

            GameInputScope.Run(scope =>
            {
                Rig rig = NewRig(scope, (InputTrigger)3, InputA);
                try
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => Call(rig, "Awake"));
                }
                finally
                {
                    Call(rig, "OnDestroy");
                }
            });
        }

        // The Held trigger also hooks the UIButton exit event: OnButtonExit 0x68C490 starts the exit input as a
        // press sequence (SupplyGameInputSequence, a coroutine on this button, which an inactive host refuses with
        // the engine error) unless it is None (0xB6FA1284) or the button is not interactable.
        [Test]
        public void ExitingTheButtonStartsTheExitInputUnlessItIsNoneOrTheButtonIsNotInteractable()
        {
            GameInputScope.Run(scope =>
            {
                Rig rig = NewRig(scope, InputTrigger.Held, InputA);
                rig.Log.Attach(rig.Button);
                Call(rig, "OnButtonExit");
                Assert.That(rig.Log.Calls, Is.Empty, "the default exit input is None");

                ModernUITestSupport.Set(rig.Button, "m_inputOnExit", InputB); // serialized in the original
                rig.Ui.Interactable = false;
                Call(rig, "OnButtonExit");

                rig.Ui.Interactable = true;
                LogAssert.Expect(LogType.Error, new Regex("Coroutine couldn't be started"));
                Call(rig, "OnButtonExit");
                Assert.That(rig.Log.Calls, Is.Empty, "the sequence is deferred to the coroutine");
            });
        }

        // OnButtonDown 0x68C2C0 and OnButtonUp 0x68C3E0 supply every input in array order with value 1 and the touch
        // input type, but only while the UI button is interactable (the byte at +0x78 of the UIButton).
        [Test]
        public void DownAndUpSupplyEveryInputInOrderOnlyWhileTheButtonIsInteractable()
        {
            GameInputScope.Run(scope =>
            {
                Rig rig = NewRig(scope, InputTrigger.Down, InputA, InputB);
                rig.Log.Attach(rig.Button);

                Call(rig, "OnButtonDown");
                Call(rig, "OnButtonUp");
                Assert.That(rig.Log.Calls, Is.EqualTo(new[] { Row("down", InputA), Row("down", InputB), Row("up", InputA), Row("up", InputB) }));

                rig.Log.Calls.Clear();
                rig.Ui.Interactable = false;
                Call(rig, "OnButtonDown");
                Call(rig, "OnButtonUp");
                Call(rig, "OnButtonHoldBegin");
                Assert.That(rig.Log.Calls, Is.Empty);
                Assert.That(Call(rig, "IsInteractable"), Is.False);
                rig.Ui.Interactable = true;
                Assert.That(Call(rig, "IsInteractable"), Is.True);
            });
        }

        // OnButtonHoldBegin 0x68C130 on an inactive owner: the inputs go down, then EndButtonHold 0x68C010 releases them
        // (up, in order) and runs no callback; a button that is not interactable has nothing to release.
        [Test]
        public void BeginningAHoldOnAnInactiveButtonPressesAndReleasesAtOnce()
        {
            GameInputScope.Run(scope =>
            {
                Rig rig = NewRig(scope, InputTrigger.Held, InputA, InputB);
                rig.Log.Attach(rig.Button);
                Call(rig, "OnButtonHoldBegin");
                Assert.That(rig.Log.Calls, Is.EqualTo(new[] { Row("down", InputA), Row("down", InputB), Row("up", InputA), Row("up", InputB) }));

                rig.Log.Calls.Clear();
                rig.Ui.Interactable = false;
                Call(rig, "EndButtonHold");
                Assert.That(rig.Log.Calls, Is.Empty);
            });
        }

        // DisableWhenReady 0x68C0E0 calls back at once unless a hold is in progress; with no callback it does nothing.
        [Test]
        public void DisablingAnIdleButtonCallsBackAtOnce()
        {
            GameInputScope.Run(scope =>
            {
                Rig rig = NewRig(scope, InputTrigger.Held, InputA);
                int calls = 0;
                rig.Button.DisableWhenReady(() => calls++);
                Assert.That(calls, Is.EqualTo(1));
                Assert.DoesNotThrow(() => rig.Button.DisableWhenReady(null));
            });
        }

        // OnInputTypeChanged 0x68C5F0 shows the graphic exactly when the type is in the serialized list, and does
        // nothing without a graphic.
        [Test]
        public void TheGraphicIsShownForTheListedInputTypesOnly()
        {
            GameInputScope.Run(scope =>
            {
                Rig rig = NewRig(scope, InputTrigger.Held, InputA);
                GameObject graphic = scope.Inactive("Lucid owned on-screen graphic");
                Assert.DoesNotThrow(() => Call(rig, "OnInputTypeChanged", InputType.Touch), "no graphic");
                ModernUITestSupport.Set(rig.Button, "m_buttonGraphic", graphic); // serialized in the original
                ModernUITestSupport.Set(rig.Button, "m_showOnInputTypes", new List<InputType> { InputType.Touch, InputType.Mouse }); // serialized in the original

                Call(rig, "OnInputTypeChanged", InputType.Touch);
                Assert.That(graphic.activeSelf, Is.True);
                Call(rig, "OnInputTypeChanged", InputType.Keyboard);
                Assert.That(graphic.activeSelf, Is.False);
                Call(rig, "OnInputTypeChanged", InputType.Mouse);
                Assert.That(graphic.activeSelf, Is.True);
            });
        }

        // With an active owner and a real CoroutineUtils host, a press starts the hold coroutine (OnButtonHoldBegin
        // sets the flag at 0x68C1F9 and runs SupplyHeldButton). DisableWhenReady then waits; the next pass of the
        // iterator sees the flag cleared, releases the inputs (up, in order), calls back and stops the coroutine.
        [Test]
        public void ADisabledHeldButtonCallsBackOnlyWhenItsHoldHasBeenReleased()
        {
            WithHost(scope =>
            {
                Rig rig = NewRig(scope, InputTrigger.Held, InputA);
                Activate(scope, rig);

                rig.Ui.OnButtonDown.Invoke();
                Assert.That(rig.Log.Calls[0], Is.EqualTo(Row("down", InputA)));
                Assert.That(rig.Log.Calls.Any(call => call.StartsWith("up")), Is.False, "still held");

                int calls = 0;
                rig.Button.DisableWhenReady(() => calls++);
                Assert.That(calls, Is.EqualTo(0), "waits for the release");

                var next = (IEnumerator)Call(rig, "SupplyHeldButton");
                Assert.That(next.MoveNext(), Is.False);
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(rig.Log.Calls[rig.Log.Calls.Count - 1], Is.EqualTo(Row("up", InputA)));
                Assert.That(next.MoveNext(), Is.False);
                Assert.That(calls, Is.EqualTo(1), "the callback runs once");
            });
        }

        // MoveNext 0x68C740 while the hold lasts (0x68C76B..0x68C7EE, 0x68C80A): every input is held with value 1 (0x29A6BE0)
        // as a touch, in array order, and the iterator yields. Deactivating the owner ends the loop on the next pass,
        // which clears the flag and releases the inputs. A fresh iterator takes the same path as a resumed one.
        [Test]
        public void AHeldButtonHoldsItsInputsEachPassUntilItsObjectIsDeactivated()
        {
            WithHost(scope =>
            {
                Rig rig = NewRig(scope, InputTrigger.Held, InputA, InputB);
                Activate(scope, rig);
                rig.Ui.OnButtonDown.Invoke();
                rig.Log.Calls.Clear();

                var next = (IEnumerator)Call(rig, "SupplyHeldButton");
                Assert.That(next.MoveNext(), Is.True);
                Assert.That(rig.Log.Calls, Is.EqualTo(new[] { ButtonCallbackLog.Row("held", 0, InputA, 1f, InputType.Touch), ButtonCallbackLog.Row("held", 0, InputB, 1f, InputType.Touch) }));
                Assert.That((bool)ModernUITestSupport.Get(rig.Button, "m_buttonHeld"), Is.True);

                rig.Log.Calls.Clear();
                rig.Owner.SetActive(false);
                Assert.That(next.MoveNext(), Is.False);
                Assert.That(rig.Log.Calls, Is.EqualTo(new[] { Row("up", InputA), Row("up", InputB) }));
                Assert.That((bool)ModernUITestSupport.Get(rig.Button, "m_buttonHeld"), Is.False);
            });
        }

        // Releasing the UIButton (OnButtonHoldEnd 0x68C0D0 clears the flag) lets the next pass release the inputs.
        [Test]
        public void ReleasingTheUiButtonEndsTheHoldOnTheNextPass()
        {
            WithHost(scope =>
            {
                Rig rig = NewRig(scope, InputTrigger.Held, InputA, InputB);
                Activate(scope, rig);

                rig.Ui.OnButtonDown.Invoke();
                rig.Ui.OnButtonUp.Invoke();
                int downs = rig.Log.Calls.Count(call => call.StartsWith("down"));
                Assert.That(downs, Is.EqualTo(2));
                Assert.That(rig.Log.Calls.Any(call => call.StartsWith("up")), Is.False);

                var next = (IEnumerator)Call(rig, "SupplyHeldButton");
                Assert.That(next.MoveNext(), Is.False);
                Assert.That(rig.Log.Calls.Skip(rig.Log.Calls.Count - 2).ToArray(), Is.EqualTo(new[] { Row("up", InputA), Row("up", InputB) }));
            });
        }

        // OnDestroy 0x68BE70 releases a hold that is still in progress before withdrawing its registrations.
        [Test]
        public void DestroyingAHeldButtonReleasesItsInputs()
        {
            WithHost(scope =>
            {
                Rig rig = NewRig(scope, InputTrigger.Held, InputA);
                Activate(scope, rig);
                rig.Ui.OnButtonDown.Invoke();
                Assert.That(rig.Log.Calls.Any(call => call.StartsWith("up")), Is.False);

                Call(rig, "OnDestroy");
                Assert.That(rig.Log.Calls[rig.Log.Calls.Count - 1], Is.EqualTo(Row("up", InputA)));
                Assert.That(LastInputTypeSubscriptions(rig.Button), Is.EqualTo(0));
            });
        }

        // Hide 0x6085A0 waits for the button: an idle button runs the base hide at once (the hide event, then the
        // completion), a held one only after its release.
        [Test]
        public void HidingTheListenerWaitsForAHeldButtonToBeReleased()
        {
            foreach (bool held in new[] { false, true })
            {
                WithHost(scope =>
                {
                    Rig rig = NewRig(scope, InputTrigger.Held, InputA);
                    Activate(scope, rig);
                    var order = new List<string>();
                    var hideEvent = new UnityEvent();
                    hideEvent.AddListener(() => order.Add("hide"));
                    var listener = scope.Inactive("Lucid owned listener").AddComponent<OnScreenControlGroupListenerButtonProbe>();
                    ModernUITestSupport.Set(listener, "m_onScreenButton", rig.Button); // serialized in the original
                    ModernUITestSupport.Set(listener, "m_onHideEvent", hideEvent); // serialized in the original

                    if (held) rig.Ui.OnButtonDown.Invoke();
                    listener.HideNow(() => order.Add("completed"));
                    Assert.That(order, Is.EqualTo(held ? new string[0] : new[] { "hide", "completed" }), "held " + held);

                    if (!held) return;
                    var next = (IEnumerator)Call(rig, "SupplyHeldButton");
                    Assert.That(next.MoveNext(), Is.False);
                    Assert.That(order, Is.EqualTo(new[] { "hide", "completed" }), "after the release");
                });
            }
        }

        // A CoroutineUtils host on an active owned object, so that coroutines really start (CoroutineUtils.RunCoroutine
        // 0x06001172 uses the static host). Edit Mode runs no Awake, so the host publishes nothing itself: the static
        // is set here and restored.
        private static void WithHost(Action<GameInputScope> body) => GameInputScope.Run(scope =>
        {
            object prior = CoroutineHost.GetValue(null);
            var host = new GameObject("Lucid owned coroutine host");
            scope.Defer(() => Object.DestroyImmediate(host));
            scope.Defer(() => CoroutineHost.SetValue(null, prior));
            CoroutineHost.SetValue(null, host.AddComponent<CoroutineUtils>());
            body(scope);
        });
    }
}
