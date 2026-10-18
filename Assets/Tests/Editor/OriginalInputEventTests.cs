using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.Utils;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.GameInputDeclarations;

namespace ProjectLucid.Tests
{
    // InputEvent (020006a5), InputEventSelectedOnly (020006a6), RawInput (020006b3) and
    // GameInputDisabledListener (020006a1), against their 1.10.1 x86-64 bodies. Components sit
    // on inactive owned GameObjects; registered systems are owned and released by the scope.
    // Not covered here: InputEventSelectedOnly's selection comparison (0x589D02..0x589D7F, needs a
    // UINavigationManager) and RawInput.Update (0x58C380 reads the engine's Input.anyKeyDown).
    [TestFixture]
    public sealed class OriginalInputEventTests
    {
        private static readonly GameInput OwnedInput = (GameInput)(int.MinValue + 0x4E11);
        private static readonly GameInput OwnedOther = (GameInput)(int.MinValue + 0x4E12);

        // Answers the UIManager queries InputEvent.OnInput makes and records them in order.
        private sealed class RecordingContainerManager : UIContainerManager
        {
            public readonly List<string> Calls = new List<string>();
            public bool Exclusive = true;
            public UIContainerIdentifier Open;
            public IReadOnlyList<UIContainerIdentifier> ExclusiveArgument;

            public override bool IsOpen(UIContainerIdentifier identifier)
            {
                Calls.Add("open:" + identifier.name);
                return ReferenceEquals(identifier, Open);
            }

            public override bool AreExclusivelyOpen(IReadOnlyList<UIContainerIdentifier> identifiers)
            {
                Calls.Add("exclusive");
                ExclusiveArgument = identifiers;
                return Exclusive;
            }

            public override UIContainer OpenContainer(UIContainer containerPrefab) => throw new NotSupportedException();
            public override void CloseAll() => throw new NotSupportedException();
            public override void Close(UIContainerIdentifier identifier) => throw new NotSupportedException();
            public override bool IsAnyOpen() => throw new NotSupportedException();
            public override bool TryGet(UIContainerIdentifier identifier, out UIContainer container) => throw new NotSupportedException();
            public override void CloseAllExcept(IReadOnlyList<UIContainerIdentifier> exceptFor) => throw new NotSupportedException();
        }

        // An inactive UIManager whose only container manager is the recorder, registered in its row.
        private static RecordingContainerManager RegisterUIManager(GameInputScope scope)
        {
            scope.Row<UIManager>();
            var uiManager = scope.Inactive("Lucid owned UI manager").AddComponent<UIManager>();
            var recorder = new RecordingContainerManager();
            ((IDictionary)ModernUITestSupport.Get(uiManager, "m_containerManagers")).Add(UIContainerBehaviour.Free, recorder);
            scope.Register(uiManager);
            return recorder;
        }

        private static UIContainerIdentifier Identifier(GameInputScope scope, string name)
        {
            UIContainerIdentifier identifier = scope.Own(ScriptableObject.CreateInstance<UIContainerIdentifier>());
            identifier.name = name;
            return identifier;
        }

        private static T Event<T>(GameInputScope scope, List<string> log) where T : InputEvent
        {
            var inputEvent = scope.Inactive("Lucid owned " + typeof(T).Name).AddComponent<T>();
            var unityEvent = new UnityEvent();
            unityEvent.AddListener(() => log.Add("event"));
            ModernUITestSupport.Set(inputEvent, "m_event", unityEvent);
            return inputEvent;
        }

        private static void OnInput(InputEvent inputEvent, float amount) =>
            ModernUITestSupport.Call(typeof(InputEvent), inputEvent, "OnInput", amount);

        // ---- InputEvent ----

        [Test]
        public void InputEventDeclarationsKeepOriginalFlagsAndAttributeRows()
        {
            Type type = typeof(InputEvent);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            SetOptions(type, 2, 1);
            Fields(type, ("m_input", typeof(GameInput), 1), ("m_triggerImmediately", typeof(bool), 1), ("m_event", typeof(UnityEvent), 1),
                ("m_exclusiveContainers", typeof(List<UIContainerIdentifier>), 1), ("m_ignoreIfContainerIsOpen", typeof(List<UIContainerIdentifier>), 1),
                ("m_audioSourceIdentifier", typeof(HLAudioSourceIdentifier), 1), ("m_audioClipIdentifier", typeof(HLAudioClipIdentifier), 1),
                ("m_audioManagerRef", typeof(SystemRef<AudioManager>), 0x21), ("m_isSuspended", typeof(bool), 1));
            AttributeRows(type, "m_input", typeof(SerializeField), typeof(TooltipAttribute));
            AttributeRows(type, "m_triggerImmediately", typeof(SerializeField), typeof(TooltipAttribute));
            AttributeRows(type, "m_event", typeof(TooltipAttribute), typeof(SerializeField));
            AttributeRows(type, "m_exclusiveContainers", typeof(SerializeField), typeof(TooltipAttribute));
            AttributeRows(type, "m_ignoreIfContainerIsOpen", typeof(SerializeField));
            AttributeRows(type, "m_audioSourceIdentifier", typeof(SerializeField));
            AttributeRows(type, "m_audioClipIdentifier", typeof(SerializeField), typeof(HashEnumAttribute));
            AttributeRows(type, "m_audioManagerRef");
            Assert.That(type.GetField("m_audioClipIdentifier", Declared).GetCustomAttributesData()[1].ConstructorArguments.Single().Value, Is.Null);
            Assert.That(Tooltip(type, "m_input"), Is.EqualTo("The input required to trigger the events."));
            Assert.That(Tooltip(type, "m_triggerImmediately"), Is.EqualTo("If not triggered immediately, will wait until next frame."));
            Assert.That(Tooltip(type, "m_event"), Is.EqualTo("The events to be triggered on input."));
            Assert.That(Tooltip(type, "m_exclusiveContainers"), Is.EqualTo("Set if this should only trigger if only this list of containers are open."));
            Methods(type, ("OnEnable", 0x81), ("OnDisable", 0x81), ("OnInput", 0x1c4), ("InvokeEvent", 0x81),
                ("Action_PublishUIMessage", 0x86), ("Action_Suspend", 0x86), ("Action_Unsuspend", 0x86), (".ctor", 0x1886));
        }

        // .ctor 0x589B80 writes m_input = 0xb6fa1284 (GameInput.None) and m_triggerImmediately = 1,
        // allocates only m_ignoreIfContainerIsOpen, and caches GetSystemRef<AudioManager>(null, true).
        [Test]
        public void InputEventConstructorSeedsNoneImmediateAndTheAudioManagerReference()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<AudioManager>();
                var inputEvent = Event<InputEvent>(scope, new List<string>());
                Assert.That(ModernUITestSupport.Get(inputEvent, "m_input"), Is.EqualTo(GameInput.None));
                Assert.That((int)GameInput.None, Is.EqualTo(unchecked((int)0xb6fa1284)));
                Assert.That(ModernUITestSupport.Get(inputEvent, "m_triggerImmediately"), Is.True);
                Assert.That(ModernUITestSupport.Get(inputEvent, "m_ignoreIfContainerIsOpen"), Is.Not.Null);
                Assert.That(ModernUITestSupport.Get(inputEvent, "m_isSuspended"), Is.False);
                Assert.That(ModernUITestSupport.Get(inputEvent, "m_audioManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<AudioManager>()));
            });
        }

        // OnEnable 0x589690 subscribes a delegate to the virtual OnInput (vtable +0x180) for
        // m_input with InputTrigger.Down (1) and joystick -1; OnDisable 0x589730 removes it.
        [Test]
        public void InputEventEnableSubscribesItsVirtualOnInputOnButtonDown()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<AudioManager>();
                scope.Row<UINavigationManager>();
                var plain = Event<InputEvent>(scope, new List<string>());
                var selectedOnly = Event<InputEventSelectedOnly>(scope, new List<string>());
                ModernUITestSupport.Set(plain, "m_input", OwnedInput);
                ModernUITestSupport.Set(selectedOnly, "m_input", OwnedOther);
                OwnedControlInputs.Run(() =>
                {
                    ModernUITestSupport.Call(typeof(InputEvent), plain, "OnEnable");
                    ModernUITestSupport.Call(typeof(InputEvent), selectedOnly, "OnEnable");
                    Assert.That(ControlTables.Subscriptions(OwnedInput, plain), Is.EqualTo(ControlTables.Expected(InputTrigger.Down, "InputEvent.OnInput")));
                    Assert.That(ControlTables.Subscriptions(OwnedOther, selectedOnly),
                        Is.EqualTo(ControlTables.Expected(InputTrigger.Down, "InputEventSelectedOnly.OnInput")));

                    ModernUITestSupport.Call(typeof(InputEvent), plain, "OnDisable");
                    ModernUITestSupport.Call(typeof(InputEvent), selectedOnly, "OnDisable");
                    Assert.That(ControlTables.Subscriptions(OwnedInput, plain), Is.Empty);
                    Assert.That(ControlTables.Subscriptions(OwnedOther, selectedOnly), Is.Empty);
                }, OwnedInput, OwnedOther);
            });
        }

        // OnInput 0x5897D0 returns while suspended (+0x50); Action_Suspend 0x589B60 and
        // Action_Unsuspend 0x589B70 only write that byte. With no container lists the event
        // fires immediately and the UIManager is never queried.
        [Test]
        public void SuspendedInputEventDoesNothingUntilUnsuspended()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<AudioManager>();
                RecordingContainerManager manager = RegisterUIManager(scope);
                var log = new List<string>();
                var inputEvent = Event<InputEvent>(scope, log);
                ModernUITestSupport.Set(inputEvent, "m_exclusiveContainers", null);

                inputEvent.Action_Suspend();
                Assert.That(ModernUITestSupport.Get(inputEvent, "m_isSuspended"), Is.True);
                OnInput(inputEvent, 1f);
                Assert.That(log, Is.Empty);

                inputEvent.Action_Unsuspend();
                Assert.That(ModernUITestSupport.Get(inputEvent, "m_isSuspended"), Is.False);
                OnInput(inputEvent, 0f);
                Assert.That(log, Is.EqualTo(new[] { "event" }), "the amount is not read");
                Assert.That(manager.Calls, Is.Empty);
            });
        }

        // A non-empty m_exclusiveContainers (+0x30) must pass UIManager.AreExclusivelyOpen, given
        // the list itself, before m_ignoreIfContainerIsOpen (+0x38) is read; an empty list is skipped.
        [Test]
        public void InputEventExclusiveContainersMustBeExclusivelyOpen()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<AudioManager>();
                RecordingContainerManager manager = RegisterUIManager(scope);
                var log = new List<string>();
                var inputEvent = Event<InputEvent>(scope, log);
                var exclusive = new List<UIContainerIdentifier> { Identifier(scope, "exclusive") };
                ModernUITestSupport.Set(inputEvent, "m_exclusiveContainers", exclusive);
                ModernUITestSupport.Set(inputEvent, "m_ignoreIfContainerIsOpen", new List<UIContainerIdentifier> { Identifier(scope, "ignored") });

                manager.Exclusive = false;
                OnInput(inputEvent, 1f);
                Assert.That(log, Is.Empty);
                Assert.That(manager.Calls, Is.EqualTo(new[] { "exclusive" }), "the ignore list is not read");
                Assert.That(manager.ExclusiveArgument, Is.SameAs(exclusive));

                manager.Calls.Clear();
                manager.Exclusive = true;
                OnInput(inputEvent, 1f);
                Assert.That(log, Is.EqualTo(new[] { "event" }));
                Assert.That(manager.Calls, Is.EqualTo(new[] { "exclusive", "open:ignored" }));

                manager.Calls.Clear();
                manager.Exclusive = false;
                exclusive.Clear();
                OnInput(inputEvent, 1f);
                Assert.That(log, Is.EqualTo(new[] { "event", "event" }));
                Assert.That(manager.Calls, Is.EqualTo(new[] { "open:ignored" }), "an empty exclusive list is not queried");
            });
        }

        // The foreach over m_ignoreIfContainerIsOpen returns at the first open container.
        [Test]
        public void InputEventStopsAtTheFirstOpenIgnoredContainer()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<AudioManager>();
                RecordingContainerManager manager = RegisterUIManager(scope);
                var log = new List<string>();
                var inputEvent = Event<InputEvent>(scope, log);
                UIContainerIdentifier first = Identifier(scope, "first"), second = Identifier(scope, "second");
                ModernUITestSupport.Set(inputEvent, "m_exclusiveContainers", null);
                ModernUITestSupport.Set(inputEvent, "m_ignoreIfContainerIsOpen", new List<UIContainerIdentifier> { first, second });

                manager.Open = first;
                OnInput(inputEvent, 1f);
                Assert.That(manager.Calls, Is.EqualTo(new[] { "open:first" }));
                manager.Open = second;
                OnInput(inputEvent, 1f);
                Assert.That(manager.Calls, Is.EqualTo(new[] { "open:first", "open:first", "open:second" }));
                Assert.That(log, Is.Empty);

                manager.Open = null;
                OnInput(inputEvent, 1f);
                Assert.That(log, Is.EqualTo(new[] { "event" }));
            });
        }

        // With m_triggerImmediately (+0x24) clear, InvokeEvent goes to CoroutineUtils.OnNextFrame,
        // which faults on the missing host; the event is not invoked in the same call.
        [Test]
        public void InputEventDefersToTheNextFrameWhenNotImmediate()
        {
            GameInputScope.RequireNoCoroutineHost();
            GameInputScope.Run(scope =>
            {
                scope.Row<AudioManager>();
                RegisterUIManager(scope);
                var log = new List<string>();
                var inputEvent = Event<InputEvent>(scope, log);
                ModernUITestSupport.Set(inputEvent, "m_exclusiveContainers", null);
                ModernUITestSupport.Set(inputEvent, "m_triggerImmediately", false);
                Assert.Throws<NullReferenceException>(() => OnInput(inputEvent, 1f));
                Assert.That(log, Is.Empty);
            });
        }

        // InvokeEvent 0x589A20 invokes m_event first, then plays only with a clip (+0x44), a
        // source (+0x40) and a non-null manager reference. The unawakened manager's null source
        // handler makes reaching PlayOneShotAtSource observable.
        [Test]
        public void InvokeEventPlaysOnlyWithAClipASourceAndAManager()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<AudioManager>();
                var log = new List<string>();
                var inputEvent = Event<InputEvent>(scope, log);
                Action invoke = () => ModernUITestSupport.Call(typeof(InputEvent), inputEvent, "InvokeEvent");
                void Audio(HLAudioClipIdentifier clip, HLAudioSourceIdentifier source)
                {
                    ModernUITestSupport.Set(inputEvent, "m_audioClipIdentifier", clip);
                    ModernUITestSupport.Set(inputEvent, "m_audioSourceIdentifier", source);
                }

                Audio(HLAudioClipIdentifier.AmbientBossCrabOcean, HLAudioSourceIdentifier.Ambient);
                Assert.DoesNotThrow(() => invoke(), "no AudioManager");

                var audioManager = scope.Inactive("Lucid owned audio manager").AddComponent<AudioManager>();
                ModernUITestSupport.Set(audioManager, "m_audioSourceHandler", null);
                scope.Register(audioManager);
                Audio(HLAudioClipIdentifier.None, HLAudioSourceIdentifier.Ambient);
                Assert.DoesNotThrow(() => invoke(), "no clip");
                Audio(HLAudioClipIdentifier.AmbientBossCrabOcean, HLAudioSourceIdentifier.None);
                Assert.DoesNotThrow(() => invoke(), "no source");
                Assert.That(log, Is.EqualTo(new[] { "event", "event", "event" }));

                Audio(HLAudioClipIdentifier.AmbientBossCrabOcean, HLAudioSourceIdentifier.Ambient);
                Assert.Throws<NullReferenceException>(() => invoke());
                Assert.That(log.Count, Is.EqualTo(4), "the event precedes the audio");
            });
        }

        // Action_PublishUIMessage 0x589AC0 publishes eventId under UIModernMessage(8, OnClick) on the
        // auto-created exchange with broadcast 3 (Both).
        [Test]
        public void InputEventPublishesTheUIEventUnderOnClick()
        {
            string name = ProcessManager.GetDefaultName<MessageExchangeBoundCallbackArg<UIModernMessage>>();
            var actionRows = new VerificationFixtureOwnership.ActionRows(SystemAction.Shutdown);
            VerificationFixtureOwnership.RegistryRow row = null;
            VerificationFixtureOwnership.RunThenCleanUp(() =>
            {
                row = new VerificationFixtureOwnership.RegistryRow(name);
                var exchange = new MessageExchangeBoundCallbackArg<UIModernMessage>();
                row.Register(exchange);
                GameInputScope.Run(scope =>
                {
                    scope.Row<AudioManager>();
                    var inputEvent = Event<InputEvent>(scope, new List<string>());
                    UIModernEvent eventId = scope.Own(ScriptableObject.CreateInstance<UIModernEvent>());
                    var received = new List<(UIModernEventType, UIModernEvent)>();
                    var unsubscribe = new List<Action>();
                    try
                    {
                        foreach (UIModernEventType type in Enum.GetValues(typeof(UIModernEventType)))
                        {
                            UIModernEventType subscribed = type;
                            var message = new UIModernMessage(type);
                            MessageCallback<UIModernEvent> callback = (in UIModernEvent value) => received.Add((subscribed, value));
                            exchange.SubscribeToMessage(in message, callback);
                            unsubscribe.Add(() => exchange.UnsubscribeFromMessage(in message, callback));
                        }
                        inputEvent.Action_PublishUIMessage(eventId);
                        Assert.That(received.Select(r => r.Item1), Is.EqualTo(new[] { UIModernEventType.OnClick }));
                        Assert.That(received[0].Item2, Is.SameAs(eventId));
                    }
                    finally { foreach (Action step in unsubscribe) step(); }
                });
            }, () => row?.Dispose(), actionRows.Dispose);
        }

        // ---- InputEventSelectedOnly ----

        [Test]
        public void SelectedOnlyDeclarationsKeepOriginalFlagsAndAttributeRows()
        {
            Type type = typeof(InputEventSelectedOnly);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(InputEvent)));
            SetOptions(type, 1, 2);
            Fields(type, ("m_interactable", typeof(UIInteractable), 1), ("m_navigationManagerRef", typeof(SystemRef<UINavigationManager>), 0x21));
            AttributeRows(type, "m_interactable", typeof(TooltipAttribute), typeof(SerializeField));
            Assert.That(Tooltip(type, "m_interactable"), Is.EqualTo("Optional UI component that will block input if not interactable."));
            Methods(type, ("OnInput", 0xc4), (".ctor", 0x1886));
        }

        // OnInput 0x589C60 returns for a non-null m_interactable (+0x58) whose virtual Interactable
        // is false, and while the UINavigationManager reference IsNull, before InputEvent.OnInput;
        // the plain InputEvent on the same setup fires as the control.
        [Test]
        public void SelectedOnlyRefusesWhenNotInteractableOrWithoutNavigation()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<AudioManager>();
                VerificationFixtureOwnership.LifecycleRow navigation = scope.Row<UINavigationManager>();
                Assert.That(navigation.BorrowsLiveSystem, Is.False);
                RecordingContainerManager manager = RegisterUIManager(scope);
                var log = new List<string>();
                var control = Event<InputEvent>(scope, log);
                var selectedOnly = Event<InputEventSelectedOnly>(scope, log);
                ModernUITestSupport.Set(control, "m_exclusiveContainers", null);
                ModernUITestSupport.Set(selectedOnly, "m_exclusiveContainers", null);
                Assert.That(ModernUITestSupport.Get(selectedOnly, "m_navigationManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<UINavigationManager>()));

                OnInput(control, 1f);
                Assert.That(log, Is.EqualTo(new[] { "event" }));

                var toggle = scope.Inactive("Lucid owned interactable", typeof(RectTransform)).AddComponent<UIToggle>();
                ModernUITestSupport.Set(selectedOnly, "m_interactable", toggle);
                ModernUITestSupport.Set(toggle, "m_interactable", false);
                OnInput(selectedOnly, 1f);
                ModernUITestSupport.Set(toggle, "m_interactable", true);
                OnInput(selectedOnly, 1f);
                ModernUITestSupport.Set(selectedOnly, "m_interactable", null);
                OnInput(selectedOnly, 1f);
                Assert.That(log, Is.EqualTo(new[] { "event" }));
                Assert.That(manager.Calls, Is.Empty);
            });
        }

        // ---- RawInput ----

        [Test]
        public void RawInputDeclarationsKeepOriginalFlags()
        {
            Type type = typeof(RawInput);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100101));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            SetOptions(type, 2, 1);
            Fields(type, ("m_onControllerInput", typeof(UnityEvent), 1), ("m_fired", typeof(bool), 1));
            AttributeRows(type, "m_onControllerInput", typeof(SerializeField));
            AttributeRows(type, "m_fired");
            Methods(type, ("Update", 0x81), ("FireOnController", 0x81), ("OnEnable", 0x81), ("OnDisable", 0x81), ("OnInput", 0x81), (".ctor", 0x1886));
        }

        // OnInput 0x58C520: ucomiss against 0 with jne/jnp, so only an exact zero (not NaN) is
        // ignored; m_fired (+0x28) then blocks it. FireOnController 0x58C3C0 sets the flag and
        // invokes a non-null event without testing the flag.
        [Test]
        public void RawInputFiresOnceForNonZeroInputButFireAlwaysInvokes()
        {
            GameInputScope.Run(scope =>
            {
                var raw = scope.Inactive("Lucid owned raw input").AddComponent<RawInput>();
                int fired = 0;
                var onInput = new UnityEvent();
                onInput.AddListener(() => fired++);
                ModernUITestSupport.Set(raw, "m_onControllerInput", onInput);

                ModernUITestSupport.Call(raw, "OnInput", 0f);
                ModernUITestSupport.Call(raw, "OnInput", -0f);
                Assert.That(fired, Is.Zero);
                Assert.That(ModernUITestSupport.Get(raw, "m_fired"), Is.False);
                ModernUITestSupport.Call(raw, "OnInput", float.NaN);
                Assert.That(fired, Is.EqualTo(1));
                Assert.That(ModernUITestSupport.Get(raw, "m_fired"), Is.True);
                ModernUITestSupport.Call(raw, "OnInput", 1f);
                Assert.That(fired, Is.EqualTo(1));

                ModernUITestSupport.Call(raw, "FireOnController");
                Assert.That(fired, Is.EqualTo(2));

                var other = scope.Inactive("Lucid owned raw input").AddComponent<RawInput>();
                ModernUITestSupport.Set(other, "m_onControllerInput", onInput);
                ModernUITestSupport.Call(other, "OnInput", -0.25f);
                Assert.That(fired, Is.EqualTo(3), "negative input fires");

                ModernUITestSupport.Set(other, "m_onControllerInput", null);
                Assert.DoesNotThrow(() => ModernUITestSupport.Call(other, "FireOnController"));
            });
        }

        // OnEnable 0x58C3E0 subscribes OnInput for GameInput 0x29dcb137 (SkipTheSplash) with
        // InputTrigger.Down and joystick -1; OnDisable 0x58C480 removes it.
        [Test]
        public void RawInputListensForSkipTheSplashPresses()
        {
            Assert.That((int)GameInput.SkipTheSplash, Is.EqualTo(0x29dcb137));
            GameInputScope.Run(scope =>
            {
                var raw = scope.Inactive("Lucid owned raw input").AddComponent<RawInput>();
                ControlTableLease.Run(() =>
                {
                    ModernUITestSupport.Call(raw, "OnEnable");
                    Assert.That(ControlTables.Subscriptions(GameInput.SkipTheSplash, raw), Is.EqualTo(ControlTables.Expected(InputTrigger.Down, "RawInput.OnInput")));
                    ModernUITestSupport.Call(raw, "OnDisable");
                    Assert.That(ControlTables.Subscriptions(GameInput.SkipTheSplash, raw), Is.Empty);
                }, GameInput.SkipTheSplash);
            });
        }

        // ---- GameInputDisabledListener ----

        [Test]
        public void DisabledListenerDeclarationsKeepOriginalFlagsAndAttributeRows()
        {
            Type type = typeof(GameInputDisabledListener);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            SetOptions(type, 1, 2);
            Fields(type, ("m_gameInputs", typeof(GameInput[]), 1), ("m_onInputsDisabled", typeof(UnityEvent), 1),
                ("m_onInputsEnabled", typeof(UnityEvent), 1), ("m_controlMapping", typeof(ControlMapping), 1), ("m_inputsAllDisabled", typeof(bool), 1));
            AttributeRows(type, "m_gameInputs", typeof(SerializeField), typeof(TooltipAttribute));
            AttributeRows(type, "m_onInputsDisabled", typeof(SerializeField), typeof(TooltipAttribute));
            AttributeRows(type, "m_onInputsEnabled", typeof(TooltipAttribute), typeof(SerializeField));
            Assert.That(Tooltip(type, "m_gameInputs"), Is.EqualTo("GameInputs to listen for the disabling of."));
            Assert.That(Tooltip(type, "m_onInputsDisabled"), Is.EqualTo(
                "Triggered when all the inputs on the above list become disabled, and if the inputs are already all disabled when this object is enabled."));
            Assert.That(Tooltip(type, "m_onInputsEnabled"), Is.EqualTo(
                "Triggered when all the inputs on the above list were disabled and one becomes enabled, and if any of the inputs are already enabled when this object is enabled."));
            Methods(type, ("Awake", 0x81), ("OnEnable", 0x81), ("OnDisable", 0x81), ("AllInputsDisabled", 0x81), ("OnInputDisabled", 0x81), (".ctor", 0x1886));
        }

        // A listener whose events log "disabled"/"enabled" with the stored flag seen at the call,
        // over a mapping whose disabled-input data covers only the owned inputs and forwards
        // updates as InitialiseStackableData (0x06000071) wires them.
        private static GameInputDisabledListener Listener(GameInputScope scope, ControlMapping mapping, List<string> log, params GameInput[] inputs)
        {
            var listener = scope.Inactive("Lucid owned input disabled listener").AddComponent<GameInputDisabledListener>();
            ModernUITestSupport.Set(listener, "m_gameInputs", inputs);
            ModernUITestSupport.Set(listener, "m_controlMapping", mapping);
            foreach (string field in new[] { "m_onInputsDisabled", "m_onInputsEnabled" })
            {
                var unityEvent = new UnityEvent();
                string name = field == "m_onInputsDisabled" ? "disabled" : "enabled";
                unityEvent.AddListener(() => log.Add(name + "@" + ModernUITestSupport.Get(listener, "m_inputsAllDisabled")));
                ModernUITestSupport.Set(listener, field, unityEvent);
            }
            return listener;
        }

        private static ControlMapping Mapping(GameInputScope scope, params GameInput[] inputs)
        {
            ControlMapping mapping = scope.Own(ScriptableObject.CreateInstance<ControlMapping>());
            var data = new StackableData();
            foreach (GameInput input in inputs)
            {
                data.SetBaseValue((int)input, false);
                data.SetRetrievalOperation((int)input, StackableData.RetrievalOperation.LogicalOr, false);
            }
            ModernUITestSupport.Set(mapping, "m_gameInputsDisabled", data);
            MethodInfo updated = typeof(ControlMap).GetMethod("OnInputsDisabledUpdated", BindingFlags.Instance | BindingFlags.NonPublic);
            data.OnDataUpdated += (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), mapping, updated);
            return mapping;
        }

        private static IDictionary DisabledCallbacks(ControlMapping mapping) =>
            (IDictionary)typeof(ControlMap).GetField("m_gameInputsDisabledCallbacks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mapping);

        // Awake 0x5884A0 stores the registered InputSystem's ControlMapping (+0x20) at +0x38.
        [Test]
        public void DisabledListenerAwakeTakesTheSystemMapping()
        {
            GameInputScope.Run(scope =>
            {
                ControlMapping mapping = scope.RegisterInputSystem();
                var listener = scope.Inactive("Lucid owned input disabled listener").AddComponent<GameInputDisabledListener>();
                ModernUITestSupport.Call(listener, "Awake");
                Assert.That(ModernUITestSupport.Get(listener, "m_controlMapping"), Is.SameAs(mapping));
            });
        }

        // OnEnable 0x588520 subscribes OnInputDisabled per input with callImmediately false, then
        // stores AllInputsDisabled (true for an empty list) before invoking the matching event.
        // OnDisable 0x588690 unsubscribes, invokes the enabled event only if all were disabled,
        // and clears the flag.
        [Test]
        public void DisabledListenerEnableReportsTheCurrentStateAndDisableRestoresEnabled()
        {
            GameInputScope.Run(scope =>
            {
                ControlMapping mapping = Mapping(scope, OwnedInput, OwnedOther);
                var log = new List<string>();
                GameInputDisabledListener listener = Listener(scope, mapping, log, OwnedInput, OwnedOther);

                ModernUITestSupport.Call(listener, "OnEnable");
                Assert.That(log, Is.EqualTo(new[] { "enabled@False" }), "one event and no immediate callbacks");
                IDictionary callbacks = DisabledCallbacks(mapping);
                foreach (GameInput input in new[] { OwnedInput, OwnedOther })
                {
                    var callback = (Delegate)callbacks[input];
                    Assert.That(callback.GetInvocationList().Single().Target, Is.SameAs(listener));
                    Assert.That(callback.Method.Name, Is.EqualTo("OnInputDisabled"));
                }
                ModernUITestSupport.Call(listener, "OnDisable");
                Assert.That(callbacks.Contains(OwnedInput) || callbacks.Contains(OwnedOther), Is.False);
                Assert.That(log.Count, Is.EqualTo(1), "nothing was disabled");

                mapping.AddGameInputDisabled(OwnedInput);
                mapping.AddGameInputDisabled(OwnedOther);
                ModernUITestSupport.Call(listener, "OnEnable");
                Assert.That(log.Last(), Is.EqualTo("disabled@True"), "stored before the event");
                ModernUITestSupport.Call(listener, "OnDisable");
                Assert.That(log.Last(), Is.EqualTo("enabled@True"), "invoked before the flag is cleared");
                Assert.That(ModernUITestSupport.Get(listener, "m_inputsAllDisabled"), Is.False);
                Assert.That(log.Count, Is.EqualTo(3));

                var empty = Listener(scope, mapping, log);
                ModernUITestSupport.Call(empty, "OnEnable");
                Assert.That(log.Last(), Is.EqualTo("disabled@True"), "an empty list is all disabled");
                ModernUITestSupport.Call(empty, "OnDisable");
            });
        }

        // OnInputDisabled 0x588760 ignores its arguments and re-reads every input; each event fires
        // only on a change of the stored flag (+0x40), before the flag is written.
        [Test]
        public void DisabledListenerFiresOnlyWhenTheWholeListChanges()
        {
            GameInputScope.Run(scope =>
            {
                ControlMapping mapping = Mapping(scope, OwnedInput, OwnedOther);
                var log = new List<string>();
                GameInputDisabledListener listener = Listener(scope, mapping, log, OwnedInput, OwnedOther);
                ModernUITestSupport.Call(listener, "OnEnable");
                log.Clear();

                StackableDataHandle first = mapping.AddGameInputDisabled(OwnedInput);
                Assert.That(log, Is.Empty, "one of two disabled");
                StackableDataHandle second = mapping.AddGameInputDisabled(OwnedOther);
                Assert.That(log, Is.EqualTo(new[] { "disabled@False" }));
                StackableDataHandle again = mapping.AddGameInputDisabled(OwnedOther);
                ModernUITestSupport.Call(listener, "OnInputDisabled", OwnedOther, false);
                Assert.That(log.Count, Is.EqualTo(1), "still all disabled, whatever the arguments say");

                mapping.RemoveGameInputDisabled(first);
                Assert.That(log, Is.EqualTo(new[] { "disabled@False", "enabled@True" }));
                mapping.RemoveGameInputDisabled(second);
                mapping.RemoveGameInputDisabled(again);
                Assert.That(log.Count, Is.EqualTo(2));
                Assert.That(ModernUITestSupport.Get(listener, "m_inputsAllDisabled"), Is.False);
                ModernUITestSupport.Call(listener, "OnDisable");
            });
            LogAssert.NoUnexpectedReceived();
        }
    }
}
