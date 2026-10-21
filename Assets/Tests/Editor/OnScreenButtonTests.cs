using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // OnScreenInputButton (02000976) and OnScreenBurstAttackButton (0200096b) against their 1.10.1 x86-64 bodies.
    // Components sit on inactive owned GameObjects, so Unity sends them no messages. Not covered: the
    // frame-by-frame timing of the supplied input, which needs a running player loop.
    public sealed class OnScreenButtonTests
    {
        private static readonly GameInput InputA = (GameInput)(int.MinValue + 0x4E11);
        private static readonly GameInput InputB = (GameInput)(int.MinValue + 0x4E12);

        private static string Row(string kind, GameInput input, float value, InputType type) => ButtonCallbackLog.Row(kind, 0, input, value, type);

        // ---- OnScreenInputButton ----

        // OnButtonDown 0x68C3B0, OnButtonHeld 0x68C830 and OnButtonUp 0x68C460 test only their own event field
        // (+0x28, +0x20, +0x30) and call it with joystick index 0 (xor esi, esi) and the arguments unchanged.
        [Test]
        public void EachRaiseCallsOnlyItsOwnEventWithJoystickZero()
        {
            GameInputScope.Run(scope =>
            {
                var button = scope.Inactive("Lucid owned on-screen input button").AddComponent<OnScreenInputButtonProbe>();
                Assert.DoesNotThrow(() => { button.RaiseDown(InputA, 1f, InputType.Touch); button.RaiseHeld(InputA, 1f, InputType.Touch); button.RaiseUp(InputA, 1f, InputType.Touch); },
                    "events are null-checked");
                var log = new ButtonCallbackLog();
                log.Attach(button);

                button.RaiseHeld(InputA, 0.5f, InputType.Mouse);
                Assert.That(log.Calls, Is.EqualTo(new[] { Row("held", InputA, 0.5f, InputType.Mouse) }));
                button.RaiseDown(InputB, 1f, InputType.Touch);
                Assert.That(log.Calls, Is.EqualTo(new[] { Row("held", InputA, 0.5f, InputType.Mouse), Row("down", InputB, 1f, InputType.Touch) }));
                button.RaiseUp(InputA, 0f, InputType.DefaultController);
                Assert.That(log.Calls.Count, Is.EqualTo(3));
                Assert.That(log.Calls[2], Is.EqualTo(Row("up", InputA, 0f, InputType.DefaultController)));
            });
        }

        // The compiler-generated accessors 0x6918D0..0x691BA0 combine and remove delegates.
        [Test]
        public void RemovedHandlersAreNoLongerCalled()
        {
            GameInputScope.Run(scope =>
            {
                var button = scope.Inactive("Lucid owned on-screen input button").AddComponent<OnScreenInputButtonProbe>();
                var first = new ButtonCallbackLog();
                var second = new ButtonCallbackLog();
                first.Attach(button);
                second.Attach(button);
                button.ButtonDownHandlers -= first.Down;
                button.RaiseDown(InputA, 1f, InputType.Touch);
                Assert.That(first.Calls, Is.Empty);
                Assert.That(second.Calls, Is.EqualTo(new[] { Row("down", InputA, 1f, InputType.Touch) }));
            });
        }

        // MoveNext 0x691CE0: state 0 raises down (+0x28) and yields null, state 1 raises held (+0x20) and yields
        // null, state 2 raises up (+0x30) and ends; each with value 1f (0x29A6BE0 holds 0x3F800000).
        [Test]
        public void SupplyCoroutineRaisesDownHeldAndUpOneFrameApart()
        {
            GameInputScope.Run(scope =>
            {
                var button = scope.Inactive("Lucid owned on-screen input button").AddComponent<OnScreenInputButtonProbe>();
                var log = new ButtonCallbackLog();
                log.Attach(button);

                var supply = (IEnumerator)ModernUITestSupport.Call(typeof(OnScreenInputButton), button, "SupplyGameInputCoroutine", InputB, InputType.Touch);
                Assert.That(log.Calls, Is.Empty, "nothing runs before the first MoveNext");
                Assert.That(supply.MoveNext(), Is.True);
                Assert.That(supply.Current, Is.Null);
                Assert.That(log.Calls, Is.EqualTo(new[] { Row("down", InputB, 1f, InputType.Touch) }));
                Assert.That(supply.MoveNext(), Is.True);
                Assert.That(supply.Current, Is.Null);
                Assert.That(log.Calls, Is.EqualTo(new[] { Row("down", InputB, 1f, InputType.Touch), Row("held", InputB, 1f, InputType.Touch) }));
                Assert.That(supply.MoveNext(), Is.False);
                Assert.That(log.Calls, Is.EqualTo(new[]
                {
                    Row("down", InputB, 1f, InputType.Touch), Row("held", InputB, 1f, InputType.Touch), Row("up", InputB, 1f, InputType.Touch),
                }), "the release reports value 1, not 0");
            });
        }

        // 0x68C540 starts the iterator on this button (MonoBehaviour.StartCoroutine); an inactive host refuses it
        // with the engine's own error, which is the observable proof that the start was attempted.
        [Test]
        public void SupplySequenceStartsTheCoroutineOnTheButton()
        {
            GameInputScope.Run(scope =>
            {
                var button = scope.Inactive("Lucid owned on-screen input button").AddComponent<OnScreenInputButtonProbe>();
                var log = new ButtonCallbackLog();
                log.Attach(button);
                LogAssert.Expect(LogType.Error, new Regex("Coroutine couldn't be started"));
                button.Supply(InputA, InputType.Touch);
                Assert.That(log.Calls, Is.Empty, "the sequence is deferred to the coroutine");
            });
        }

        // ---- OnScreenBurstAttackButton ----

        // SetTargetingActive 0x68B9D0 reads the three bytes of the message in field order (+0, +1, +2) into
        // CanTrigger (+0x20), IsChaosControl (+0x28) and IsActive (+0x30): each value reaches its own Bindable.
        [Test]
        public void BurstAttackMessageFlagsReachTheirOwnBindables()
        {
            GameInputScope.Run(scope =>
            {
                var button = scope.Inactive("Lucid owned burst attack button").AddComponent<OnScreenBurstAttackButton>();
                Assert.That((button.CanTrigger.Value, button.IsChaosControl.Value, button.IsActive.Value), Is.EqualTo((false, false, false)));
                foreach ((bool canTrigger, bool chaos, bool active) in new[]
                {
                    (true, false, false), (false, true, false), (false, false, true), (true, true, false), (true, false, true), (false, true, true), (true, true, true), (false, false, false),
                })
                {
                    button.SetTargetingActive(new BurstAttackUpdateMessage { CanTrigger = canTrigger, IsChaosControl = chaos, IsActive = active });
                    Assert.That((button.CanTrigger.Value, button.IsChaosControl.Value, button.IsActive.Value), Is.EqualTo((canTrigger, chaos, active)));
                }
            });
        }

        // Each Bindable is a distinct object (three allocations in the constructor, 0x68BA40).
        [Test]
        public void BurstAttackBindablesAreThreeDistinctObjects()
        {
            GameInputScope.Run(scope =>
            {
                var button = scope.Inactive("Lucid owned burst attack button").AddComponent<OnScreenBurstAttackButton>();
                Assert.That(button.CanTrigger, Is.Not.SameAs(button.IsChaosControl));
                Assert.That(button.IsChaosControl, Is.Not.SameAs(button.IsActive));
                Assert.That(button.CanTrigger, Is.Not.SameAs(button.IsActive));
            });
        }
    }
}
