using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.GameInputDeclarations;

namespace ProjectLucid.Tests
{
    // The IInputButton providers of Game.Runtime: InputHandler (020006a8), GameInputSupplier
    // (020006a3) and GameInputCharacterTriggerVolume (020001d2), against their 1.10.1 x86-64
    // bodies. Components sit on inactive owned GameObjects, so Unity sends them no messages.
    // Not covered here, as each needs a live Character: SupplyInput past its CharacterManager
    // guard (0x589080), and DoTriggerStay's bounds test and m_delayComplete write (0x6F36B0).
    // The held-button entry is observed up to its SubscribeToTimeManager (0x6F3A90), which faults
    // without a TimeManager. InitialPressInput (0x6F3590) is observed only up to its first
    // OnNextFrame schedule, which faults without a coroutine host, so later inputs and the
    // deferred releases' frame order are not.
    [TestFixture]
    public sealed class OriginalGameInputProviderTests
    {
        private static readonly GameInput InputA = (GameInput)(int.MinValue + 0x4E01);
        private static readonly GameInput InputB = (GameInput)(int.MinValue + 0x4E02);
        private static readonly GameInput InputC = (GameInput)(int.MinValue + 0x4E03);
        private const InputType DefaultController = InputType.DefaultController;

        private sealed class ProbeHandler : InputHandler { }

        private static string Held(GameInput input, float value, InputType type) => ButtonCallbackLog.Row("held", 0, input, value, type);
        private static string Down(GameInput input, float value, InputType type) => ButtonCallbackLog.Row("down", 0, input, value, type);
        private static string Up(GameInput input, float value, InputType type) => ButtonCallbackLog.Row("up", 0, input, value, type);

        private static List<IInputButton> Providers(ControlMapping mapping) =>
            (List<IInputButton>)ModernUITestSupport.Get(mapping, "m_buttonProviders");

        // The mapping methods a provider's three events forward to, as "event:Method".
        private static string[] Wiring(object provider, Type declaring, ControlMapping mapping)
        {
            var rows = new List<string>();
            foreach (string name in new[] { "ButtonHandlers", "ButtonDownHandlers", "ButtonUpHandlers" })
            {
                var handlers = (Delegate)declaring.GetField(name, Declared).GetValue(provider);
                if (handlers == null) continue;
                foreach (Delegate handler in handlers.GetInvocationList())
                    if (ReferenceEquals(handler.Target, mapping)) rows.Add(name + ":" + handler.Method.Name);
            }
            return rows.ToArray();
        }

        private static readonly string[] MappingWiring =
            { "ButtonHandlers:OnButtonHeld", "ButtonDownHandlers:OnButtonDown", "ButtonUpHandlers:OnButtonUp" };

        // ---- InputHandler ----

        [Test]
        public void InputHandlerDeclarationsKeepOriginalFlagsAndMemberOrder()
        {
            Type type = typeof(InputHandler);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100081));
            Assert.That(type.BaseType, Is.EqualTo(typeof(object)));
            Assert.That(type.GetInterfaces(), Is.EqualTo(new[] { typeof(IInputButton) }));
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("Game.Runtime"));
            SetOptions(type, 2, 1);
            Fields(type, ("ButtonHandlers", typeof(ButtonHandler.OnHeld), 1), ("ButtonDownHandlers", typeof(ButtonHandler.OnDown), 1),
                ("ButtonUpHandlers", typeof(ButtonHandler.OnUp), 1), ("m_registered", typeof(bool), 1),
                ("m_inputSystemRef", typeof(SystemRef<InputSystem>), 0x21));
            foreach (string field in new[] { "ButtonHandlers", "ButtonDownHandlers", "ButtonUpHandlers" })
                AttributeRows(type, field, typeof(CompilerGeneratedAttribute));
            Methods(type, ("add_ButtonHandlers", 0x9e6), ("remove_ButtonHandlers", 0x9e6), ("add_ButtonDownHandlers", 0x9e6),
                ("remove_ButtonDownHandlers", 0x9e6), ("add_ButtonUpHandlers", 0x9e6), ("remove_ButtonUpHandlers", 0x9e6),
                ("Register", 0x86), ("Unregister", 0x86), ("OnButtonHeld", 0x86), ("OnButtonDown", 0x86), ("OnButtonUp", 0x86), (".ctor", 0x1884));
            Assert.That(type.GetNestedTypes(Declared), Is.Empty);
        }

        // 0x58A1E0/0x58A660/0x58A690 test m_registered (+0x28), then null-check only the
        // matching event and call it with joystick index 0 (xor esi, esi).
        [Test]
        public void InputHandlerForwardsOnlyWhileRegisteredWithJoystickZero()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<InputSystem>();
                var handler = new ProbeHandler();
                handler.OnButtonDown(InputA, 1f, DefaultController);
                Assert.DoesNotThrow(() => handler.OnButtonHeld(InputA, 1f, DefaultController), "no handlers");

                var log = new ButtonCallbackLog();
                log.Attach(handler);
                handler.OnButtonHeld(InputA, 0.5f, InputType.Mouse);
                handler.OnButtonDown(InputB, 1f, DefaultController);
                handler.OnButtonUp(InputC, 0f, InputType.Touch);
                Assert.That(log.Calls, Is.Empty, "unregistered handlers are not called");

                ModernUITestSupport.Set(handler, "m_registered", true);
                handler.OnButtonHeld(InputA, 0.5f, InputType.Mouse);
                handler.OnButtonDown(InputB, 1f, DefaultController);
                handler.OnButtonUp(InputC, 0f, InputType.Touch);
                Assert.That(log.Calls, Is.EqualTo(new[] { Held(InputA, 0.5f, InputType.Mouse), Down(InputB, 1f, DefaultController), Up(InputC, 0f, InputType.Touch) }));

                var bare = new ProbeHandler();
                ModernUITestSupport.Set(bare, "m_registered", true);
                Assert.DoesNotThrow(() => { bare.OnButtonHeld(InputA, 1f, DefaultController); bare.OnButtonDown(InputA, 1f, DefaultController); bare.OnButtonUp(InputA, 1f, DefaultController); });
            });
        }

        // Register 0x58A570 returns while +0x28 is set or TryGet fails, else registers with the
        // system's ControlMapping (+0x20) and only then sets the flag; Unregister 0x58A5F0 mirrors it.
        [Test]
        public void InputHandlerRegisterNeedsAnInputSystemAndRunsOnce()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<InputSystem>();
                var handler = new ProbeHandler();
                handler.Register();
                Assert.That(ModernUITestSupport.Get(handler, "m_registered"), Is.False, "no InputSystem");

                ControlMapping mapping = scope.RegisterInputSystem();
                handler.Unregister();
                Assert.That(Providers(mapping), Is.Empty, "Unregister does nothing before Register");

                handler.Register();
                Assert.That(ModernUITestSupport.Get(handler, "m_registered"), Is.True);
                Assert.That(Providers(mapping), Is.EqualTo(new IInputButton[] { handler }));
                Assert.That(Wiring(handler, typeof(InputHandler), mapping), Is.EquivalentTo(MappingWiring));

                handler.Register();
                Assert.That(Providers(mapping).Count, Is.EqualTo(1), "a registered handler is not added twice");
                Assert.That(Wiring(handler, typeof(InputHandler), mapping).Length, Is.EqualTo(3));

                // A failed TryGet jumps past both the unregistration and the flag write (je 0x58A654).
                var inputSystem = ProcessManager.GetSystem<InputSystem>();
                scope.Unregister(inputSystem);
                handler.Unregister();
                Assert.That(ModernUITestSupport.Get(handler, "m_registered"), Is.True, "no InputSystem");
                Assert.That(Providers(mapping), Is.EqualTo(new IInputButton[] { handler }));
                Assert.That(Wiring(handler, typeof(InputHandler), mapping), Is.EquivalentTo(MappingWiring));
                scope.Register(inputSystem);

                handler.Unregister();
                Assert.That(ModernUITestSupport.Get(handler, "m_registered"), Is.False);
                Assert.That(Providers(mapping), Is.Empty);
                Assert.That(Wiring(handler, typeof(InputHandler), mapping), Is.Empty);
            });
        }

        // ---- GameInputSupplier ----

        [Test]
        public void SupplierDeclarationsKeepOriginalFlagsAndIteratorName()
        {
            Type type = typeof(GameInputSupplier);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(type.GetInterfaces(), Is.EqualTo(new[] { typeof(IInputButton) }));
            SetOptions(type, 1, 2);
            Fields(type, ("ButtonHandlers", typeof(ButtonHandler.OnHeld), 1), ("ButtonDownHandlers", typeof(ButtonHandler.OnDown), 1),
                ("ButtonUpHandlers", typeof(ButtonHandler.OnUp), 1), ("m_input", typeof(GameInput), 1), ("m_repeatInputForSeconds", typeof(float), 1),
                ("m_controlMapping", typeof(ControlMapping), 1), ("m_repeatedInput", typeof(Coroutine), 1),
                ("m_characterManagerRef", typeof(SystemRef<CharacterManager>), 0x21));
            AttributeRows(type, "m_input", typeof(SerializeField));
            AttributeRows(type, "m_repeatInputForSeconds", typeof(SerializeField));
            AttributeRows(type, "m_controlMapping");
            Methods(type, ("add_ButtonHandlers", 0x9e6), ("remove_ButtonHandlers", 0x9e6), ("add_ButtonDownHandlers", 0x9e6),
                ("remove_ButtonDownHandlers", 0x9e6), ("add_ButtonUpHandlers", 0x9e6), ("remove_ButtonUpHandlers", 0x9e6),
                ("OnEnable", 0x86), ("SupplyInput", 0x86), ("SupplyHeldInput", 0x81), ("OnDisable", 0x81), (".ctor", 0x1886));
            Assert.That(type.GetMethod("SupplyHeldInput", Declared).GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType,
                Is.EqualTo(Nested(type, "<SupplyHeldInput>d__16")));

            Type iterator = Nested(type, "<SupplyHeldInput>d__16");
            Assert.That((int)iterator.Attributes, Is.EqualTo(0x00100103));
            Fields(iterator, ("<>1__state", typeof(int), 1), ("<>2__current", typeof(object), 1),
                ("<>4__this", typeof(GameInputSupplier), 6), ("durationSeconds", typeof(float), 6));
        }

        // OnEnable 0x588FF0 stores GetSystem<InputSystem>().ControlMapping (+0x40) and registers;
        // OnDisable 0x589310 stops the coroutine and unregisters unless the mapping is Unity-null.
        [Test]
        public void SupplierEnableRegistersWithTheSystemMappingAndDisableUnregisters()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<CharacterManager>();
                ControlMapping mapping = scope.RegisterInputSystem();
                var supplier = scope.Inactive("Lucid owned game input supplier").AddComponent<GameInputSupplier>();
                Assert.DoesNotThrow(() => ModernUITestSupport.Call(supplier, "OnDisable"), "a never-enabled supplier has no mapping");

                supplier.OnEnable();
                Assert.That(ModernUITestSupport.Get(supplier, "m_controlMapping"), Is.SameAs(mapping));
                Assert.That(Providers(mapping), Is.EqualTo(new IInputButton[] { supplier }));
                Assert.That(Wiring(supplier, typeof(GameInputSupplier), mapping), Is.EquivalentTo(MappingWiring));

                ModernUITestSupport.Call(supplier, "OnDisable");
                Assert.That(Providers(mapping), Is.Empty);
                Assert.That(Wiring(supplier, typeof(GameInputSupplier), mapping), Is.Empty);
                Assert.That(ModernUITestSupport.Get(supplier, "m_repeatedInput"), Is.Null);
            });
        }

        // OnDisable 0x589310 tests m_controlMapping with UnityEngine.Object.op_Equality (0x589367),
        // so a destroyed mapping whose managed list still holds the supplier is skipped; a
        // reference null test would unregister from it.
        [Test]
        public void SupplierDisableSkipsADestroyedMapping()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<CharacterManager>();
                ControlMapping mapping = scope.RegisterInputSystem();
                var supplier = scope.Inactive("Lucid owned game input supplier").AddComponent<GameInputSupplier>();
                supplier.OnEnable();
                Assert.That(Providers(mapping), Is.EqualTo(new IInputButton[] { supplier }));

                UnityEngine.Object.DestroyImmediate(mapping);
                Assert.That(mapping == null, Is.True, "Unity-null");
                Assert.That(ModernUITestSupport.Get(supplier, "m_controlMapping") is null, Is.False, "the managed reference survives");
                ModernUITestSupport.Call(supplier, "OnDisable");
                Assert.That(Providers(mapping), Is.EqualTo(new IInputButton[] { supplier }));
                Assert.That(Wiring(supplier, typeof(GameInputSupplier), mapping), Is.EquivalentTo(MappingWiring));
            });
        }

        // SupplyInput 0x589080 returns when the CharacterManager reference IsNull, before any
        // callback, impulse retention or coroutine.
        [Test]
        public void SupplyInputWithoutACharacterManagerSuppliesNothing()
        {
            GameInputScope.Run(scope =>
            {
                VerificationFixtureOwnership.LifecycleRow row = scope.Row<CharacterManager>();
                Assert.That(row.BorrowsLiveSystem, Is.False);
                var supplier = scope.Inactive("Lucid owned game input supplier").AddComponent<GameInputSupplier>();
                ModernUITestSupport.Set(supplier, "m_input", InputA);
                var log = new ButtonCallbackLog();
                log.Attach(supplier);
                supplier.SupplyInput();
                Assert.That(log.Calls, Is.Empty);
                Assert.That(ModernUITestSupport.Get(supplier, "m_repeatedInput"), Is.Null);
            });
        }

        // MoveNext 0x589420: states 0 and 1 share the `durationSeconds > 0` test (jbe, so NaN
        // ends); each pass raises the held event (+0x20) with 1f and the shared last input type,
        // then subtracts Time.deltaTime and yields null. The end raises the up event (+0x30) with 0f.
        [Test]
        public void SupplyHeldInputHoldsWhileTimeRemainsThenReleasesWithZero()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<CharacterManager>();
                var supplier = scope.Inactive("Lucid owned game input supplier").AddComponent<GameInputSupplier>();
                ModernUITestSupport.Set(supplier, "m_input", InputB);
                var log = new ButtonCallbackLog();
                log.Attach(supplier);
                InputType last = ControlMapping.LastInputType;

                foreach (float ended in new[] { 0f, -1f, float.NaN })
                {
                    log.Calls.Clear();
                    var finished = (IEnumerator)ModernUITestSupport.Call(supplier, "SupplyHeldInput", ended);
                    Assert.That(finished.MoveNext(), Is.False, ended.ToString());
                    Assert.That(log.Calls, Is.EqualTo(new[] { Up(InputB, 0f, last) }), ended.ToString());
                }

                log.Calls.Clear();
                var held = (IEnumerator)ModernUITestSupport.Call(supplier, "SupplyHeldInput", 1000f);
                float deltaTime = Time.deltaTime;
                Assert.That(held.MoveNext(), Is.True);
                Assert.That(held.Current, Is.Null);
                Assert.That(log.Calls, Is.EqualTo(new[] { Held(InputB, 1f, last) }));
                Assert.That(ModernUITestSupport.Get(held, "durationSeconds"), Is.EqualTo(1000f - deltaTime));

                ModernUITestSupport.Set(held, "durationSeconds", 0f);
                Assert.That(held.MoveNext(), Is.False);
                Assert.That(log.Calls, Is.EqualTo(new[] { Held(InputB, 1f, last), Up(InputB, 0f, last) }));
                Assert.That(held.MoveNext(), Is.False, "a finished iterator raises nothing more");
                Assert.That(log.Calls.Count, Is.EqualTo(2));
                Assert.Throws<NotSupportedException>(held.Reset);

                var bare = scope.Inactive("Lucid owned bare game input supplier").AddComponent<GameInputSupplier>();
                Assert.DoesNotThrow(() => ((IEnumerator)ModernUITestSupport.Call(bare, "SupplyHeldInput", 0f)).MoveNext(), "events are null-checked");
            });
        }

        // ---- GameInputCharacterTriggerVolume ----

        [Test]
        public void TriggerVolumeDeclarationsKeepOriginalFlagsNestedTypesAndClosureName()
        {
            Type type = typeof(GameInputCharacterTriggerVolume);
            Assert.That((int)type.Attributes, Is.EqualTo(0x00100001));
            Assert.That(type.BaseType, Is.EqualTo(typeof(CharacterTrigger)));
            Assert.That(type.GetInterfaces().Except(typeof(CharacterTrigger).GetInterfaces()), Is.EquivalentTo(new[] { typeof(IInputButton), typeof(ITimeScaled) }));
            SetOptions(type, 1, 2);
            Type simulation = Nested(type, "InputTypeSimulation");
            Fields(type, ("ButtonHandlers", typeof(ButtonHandler.OnHeld), 1), ("ButtonDownHandlers", typeof(ButtonHandler.OnDown), 1),
                ("ButtonUpHandlers", typeof(ButtonHandler.OnUp), 1), ("InputType", typeof(InputType), 0x8051),
                ("m_inputsToProvide", typeof(GameInput[]), 1), ("m_typeSimulation", simulation, 1),
                ("m_delayInputUntilWholeColliderInside", typeof(bool), 1), ("m_frameCount", typeof(float), 1),
                ("m_delayComplete", typeof(bool), 1), ("m_controlMapping", typeof(ControlMapping), 1), ("<IsPaused>k__BackingField", typeof(bool), 1));
            Assert.That(type.GetField("InputType", Declared).GetRawConstantValue(), Is.EqualTo(897719803));
            AttributeRows(type, "m_delayInputUntilWholeColliderInside", typeof(TooltipAttribute), typeof(SerializeField));
            Assert.That(Tooltip(type, "m_delayInputUntilWholeColliderInside"),
                Is.EqualTo("Triggering immediately can cause issues with form changes, tick this box if you expect to enter ball form."));
            Methods(type, ("add_ButtonHandlers", 0x9e6), ("remove_ButtonHandlers", 0x9e6), ("add_ButtonDownHandlers", 0x9e6),
                ("remove_ButtonDownHandlers", 0x9e6), ("add_ButtonUpHandlers", 0x9e6), ("remove_ButtonUpHandlers", 0x9e6),
                ("Awake", 0xc4), ("DoTriggerEnter", 0xc4), ("DoTriggerStay", 0xc4), ("OnUpdate", 0x1e6), ("DoTriggerExit", 0xc4),
                ("InitialPressInput", 0x81), ("InitialHoldInput", 0x81), ("ContinuousHoldInput", 0x81), ("EndHoldInput", 0x81),
                ("SubscribeToTimeManager", 0x81), ("UnsubscribeFromTimeManager", 0x81), ("get_IsPaused", 0x9e6), ("set_IsPaused", 0x9e6), (".ctor", 0x1886));

            Assert.That((int)simulation.Attributes, Is.EqualTo(0x103));
            Assert.That(Enum.GetNames(simulation), Is.EqualTo(new[] { "HeldButton", "SingleFrameButtonOnEntry", "SingleFrameButtonOnExit" }));
            Assert.That(Enum.GetValues(simulation).Cast<object>().Select(value => Convert.ToInt32(value)).ToArray(), Is.EqualTo(new[] { 0, 1, 2 }));

            Type closure = Nested(type, "<>c__DisplayClass22_0");
            Assert.That((int)closure.Attributes, Is.EqualTo(0x00100103));
            Fields(closure, ("j", typeof(int), 6), ("<>4__this", type, 6));
            Methods(closure, (".ctor", 0x1886), ("<InitialPressInput>b__0", 0x83));
        }

        private static GameInputCharacterTriggerVolume Volume(GameInputScope scope, int simulation, bool delay, params GameInput[] inputs)
        {
            GameObject owner = scope.Inactive("Lucid owned game input trigger volume", typeof(BoxCollider));
            var volume = owner.AddComponent<GameInputCharacterTriggerVolume>();
            Type type = typeof(GameInputCharacterTriggerVolume);
            ModernUITestSupport.Set(volume, "m_typeSimulation", Enum.ToObject(Nested(type, "InputTypeSimulation"), simulation));
            ModernUITestSupport.Set(volume, "m_delayInputUntilWholeColliderInside", delay);
            ModernUITestSupport.Set(volume, "m_inputsToProvide", inputs);
            return volume;
        }

        private static void Invoke(GameInputCharacterTriggerVolume volume, string method, params object[] arguments) =>
            ModernUITestSupport.Call(typeof(GameInputCharacterTriggerVolume), volume, method, arguments);

        private static float FrameCount(GameInputCharacterTriggerVolume volume) => (float)ModernUITestSupport.Get(volume, "m_frameCount");

        // Awake 0x6F3380 runs CharacterTrigger.Awake (collider, CharacterManager reference), then
        // stores GetSystem<InputSystem>().ControlMapping (+0x80) and registers as a provider.
        [Test]
        public void TriggerVolumeAwakeRegistersWithTheSystemMapping()
        {
            GameInputScope.Run(scope =>
            {
                scope.Row<CharacterManager>();
                ControlMapping mapping = scope.RegisterInputSystem();
                GameInputCharacterTriggerVolume volume = Volume(scope, 0, false, InputA);
                Invoke(volume, "Awake");
                Assert.That(ModernUITestSupport.Get(volume, "m_collider"), Is.SameAs(volume.GetComponent<BoxCollider>()));
                Assert.That(ModernUITestSupport.Get(volume, "m_characterManagerRef"), Is.Not.Null);
                Assert.That(ModernUITestSupport.Get(volume, "m_controlMapping"), Is.SameAs(mapping));
                Assert.That(Providers(mapping), Is.EqualTo(new IInputButton[] { volume }));
                Assert.That(Wiring(volume, typeof(GameInputCharacterTriggerVolume), mapping), Is.EquivalentTo(MappingWiring));
                mapping.UnregisterButtonProvider(volume);
            });
        }

        // DoTriggerEnter 0x6F3420 switches on m_typeSimulation (+0x70): 1 presses, 0 holds unless
        // delayed (+0x74), 2 does nothing. InitialPressInput 0x6F3590 sets m_frameCount to 1 and,
        // per input, raises the down event (joystick 0, value 1, DefaultController) before
        // scheduling the release through CoroutineUtils.OnNextFrame, whose missing host faults.
        [Test]
        public void TriggerVolumeEnterPressesOnlyForItsSimulation()
        {
            GameInputScope.Run(scope =>
            {
                scope.WithoutCoroutineHost();
                var log = new ButtonCallbackLog();
                GameInputCharacterTriggerVolume onExit = Volume(scope, 2, false, InputA, InputB);
                GameInputCharacterTriggerVolume delayed = Volume(scope, 0, true, InputA, InputB);
                GameInputCharacterTriggerVolume onEntry = Volume(scope, 1, false, InputA, InputB);
                log.Attach(onExit);
                log.Attach(delayed);
                log.Attach(onEntry);

                Invoke(onExit, "DoTriggerEnter", (object)null);
                Invoke(delayed, "DoTriggerEnter", (object)null);
                Assert.That(log.Calls, Is.Empty);
                Assert.That(FrameCount(onExit), Is.Zero);
                Assert.That(FrameCount(delayed), Is.Zero);

                Assert.Throws<NullReferenceException>(() => Invoke(onEntry, "DoTriggerEnter", (object)null));
                Assert.That(log.Calls, Is.EqualTo(new[] { Down(InputA, 1f, DefaultController) }), "the first press precedes the schedule");
                Assert.That(FrameCount(onEntry), Is.EqualTo(1f));
            });
        }

        // DoTriggerEnter 0x6F3420 with HeldButton and no delay inlines InitialHoldInput: m_frameCount
        // = 1 (0x6F3454), then per input the down event (+0x58) and then the held event (+0x50),
        // each with joystick 0, value 1 and DefaultController (0x6F347C..0x6F34D6), and only then
        // the SubscribeToTimeManager tail call (0x6F34DF). Its GetSystem<TimeManager>(null, true)
        // faults on the absent system without a logging host, after every callback has run.
        [Test]
        public void TriggerVolumeHeldEntryPressesThenHoldsEachInputBeforeSubscribing()
        {
            GameInputScope.Run(scope =>
            {
                VerificationFixtureOwnership.LifecycleRow timeRow = scope.Row<TimeManager>();
                Assert.That(timeRow.BorrowsLiveSystem, Is.False);
                Assert.That(HLUnityCore.IsNull(), Is.True, "a live logging host would log instead of throwing");
                var log = new ButtonCallbackLog();
                GameInputCharacterTriggerVolume held = Volume(scope, 0, false, InputA, InputB);
                log.Attach(held);

                NullReferenceException error = Assert.Throws<NullReferenceException>(() => Invoke(held, "DoTriggerEnter", (object)null));
                Assert.That(error.Message, Is.EqualTo("System '" + ProcessManager.GetDefaultName<TimeManager>() + "' is null"));
                Assert.That(log.Calls, Is.EqualTo(new[]
                {
                    Down(InputA, 1f, DefaultController), Held(InputA, 1f, DefaultController),
                    Down(InputB, 1f, DefaultController), Held(InputB, 1f, DefaultController),
                }));
                Assert.That(FrameCount(held), Is.EqualTo(1f));
            });
        }

        // <InitialPressInput>b__0 0x6F3C20 reads the up event (+0x60) and m_inputsToProvide[j]
        // when it runs (bounds-checked), and reports value 1 (0x3f800000) with DefaultController.
        [Test]
        public void TriggerVolumeDeferredReleaseReadsItsInputWhenItRuns()
        {
            GameInputScope.Run(scope =>
            {
                GameInputCharacterTriggerVolume volume = Volume(scope, 1, false, InputA, InputB);
                Type closureType = Nested(typeof(GameInputCharacterTriggerVolume), "<>c__DisplayClass22_0");
                object closure = Activator.CreateInstance(closureType);
                closureType.GetField("<>4__this").SetValue(closure, volume);
                closureType.GetField("j").SetValue(closure, 1);
                Action release = () => ModernUITestSupport.Call(closureType, closure, "<InitialPressInput>b__0");

                Assert.DoesNotThrow(() => release(), "a null up event is skipped");
                var log = new ButtonCallbackLog();
                log.Attach(volume);
                release();
                ModernUITestSupport.Set(volume, "m_inputsToProvide", new[] { InputA, InputC });
                release();
                Assert.That(log.Calls, Is.EqualTo(new[] { Up(InputB, 1f, DefaultController), Up(InputC, 1f, DefaultController) }));

                ModernUITestSupport.Set(volume, "m_inputsToProvide", new[] { InputA });
                Assert.Throws<IndexOutOfRangeException>(() => release());
            });
        }

        // OnUpdate 0x6F3820 is ContinuousHoldInput 0x6F38B0 inlined: m_frameCount += 1, then the
        // held event per input with value 1; deltaTime is never read.
        [Test]
        public void TriggerVolumeUpdateHoldsEveryInputAndIgnoresDeltaTime()
        {
            GameInputScope.Run(scope =>
            {
                GameInputCharacterTriggerVolume volume = Volume(scope, 0, false, InputA, InputB);
                var log = new ButtonCallbackLog();
                log.Attach(volume);
                ModernUITestSupport.Set(volume, "m_frameCount", 1f);
                volume.OnUpdate(123f);
                volume.OnUpdate(-5f);
                Assert.That(FrameCount(volume), Is.EqualTo(3f));
                string held0 = Held(InputA, 1f, DefaultController), held1 = Held(InputB, 1f, DefaultController);
                Assert.That(log.Calls, Is.EqualTo(new[] { held0, held1, held0, held1 }));

                ModernUITestSupport.Set(volume, "m_inputsToProvide", new GameInput[0]);
                volume.OnUpdate(0f);
                Assert.That(FrameCount(volume), Is.EqualTo(4f), "the count advances with no inputs");
                Assert.That(log.Calls.Count, Is.EqualTo(4));
            });
        }

        // DoTriggerExit 0x6F3940: 2 presses, 0 ends the hold, 1 does nothing. EndHoldInput
        // 0x6F39F0 adds 1 to m_frameCount, raises the up event per input with value 1, then
        // UnsubscribeFromTimeManager 0x6F3B30 skips a Unity-null GetSystemSafe<TimeManager>().
        [Test]
        public void TriggerVolumeHeldExitReleasesWithValueOneWithoutATimeManager()
        {
            GameInputScope.Run(scope =>
            {
                VerificationFixtureOwnership.LifecycleRow timeRow = scope.Row<TimeManager>();
                Assert.That(timeRow.BorrowsLiveSystem, Is.False);
                var log = new ButtonCallbackLog();
                GameInputCharacterTriggerVolume onEntry = Volume(scope, 1, false, InputA, InputB);
                GameInputCharacterTriggerVolume held = Volume(scope, 0, false, InputA, InputB);
                log.Attach(onEntry);
                log.Attach(held);

                Invoke(onEntry, "DoTriggerExit", (object)null);
                Assert.That(log.Calls, Is.Empty);
                Assert.That(FrameCount(onEntry), Is.Zero);

                Invoke(held, "DoTriggerExit", (object)null);
                Assert.That(log.Calls, Is.EqualTo(new[] { Up(InputA, 1f, DefaultController), Up(InputB, 1f, DefaultController) }));
                Assert.That(FrameCount(held), Is.EqualTo(1f));
            });
        }

        // DoTriggerExit with SingleFrameButtonOnExit runs the same InitialPressInput as entry.
        [Test]
        public void TriggerVolumeExitPressesForTheExitSimulation()
        {
            GameInputScope.Run(scope =>
            {
                scope.WithoutCoroutineHost();
                var log = new ButtonCallbackLog();
                GameInputCharacterTriggerVolume onExit = Volume(scope, 2, false, InputC);
                log.Attach(onExit);
                Assert.Throws<NullReferenceException>(() => Invoke(onExit, "DoTriggerExit", (object)null));
                Assert.That(log.Calls, Is.EqualTo(new[] { Down(InputC, 1f, DefaultController) }));
                Assert.That(FrameCount(onExit), Is.EqualTo(1f));
            });
        }

        // DoTriggerStay 0x6F36B0 returns unless delayed (+0x74) and not yet complete (+0x7C);
        // only then does it read the collider bounds (here a null m_collider, as Awake never ran).
        [Test]
        public void TriggerVolumeStayReadsBoundsOnlyForAnUnfinishedDelay()
        {
            GameInputScope.Run(scope =>
            {
                var log = new ButtonCallbackLog();
                GameInputCharacterTriggerVolume immediate = Volume(scope, 0, false, InputA);
                GameInputCharacterTriggerVolume complete = Volume(scope, 0, true, InputA);
                GameInputCharacterTriggerVolume waiting = Volume(scope, 0, true, InputA);
                ModernUITestSupport.Set(complete, "m_delayComplete", true);
                log.Attach(immediate);
                log.Attach(complete);
                log.Attach(waiting);

                Assert.DoesNotThrow(() => Invoke(immediate, "DoTriggerStay", (object)null));
                Assert.DoesNotThrow(() => Invoke(complete, "DoTriggerStay", (object)null));
                Assert.Throws<NullReferenceException>(() => Invoke(waiting, "DoTriggerStay", (object)null));
                Assert.That(log.Calls, Is.Empty);
                Assert.That(ModernUITestSupport.Get(waiting, "m_delayComplete"), Is.False);
            });
        }

        // get/set_IsPaused 0x6F3BF0/0x6F3C00 read and write the backing byte at +0x88.
        [Test]
        public void TriggerVolumeIsPausedStoresItsValue()
        {
            GameInputScope.Run(scope =>
            {
                GameInputCharacterTriggerVolume volume = Volume(scope, 0, false);
                Assert.That(volume.IsPaused, Is.False);
                ((ITimeScaled)volume).IsPaused = true;
                Assert.That(ModernUITestSupport.Get(volume, "<IsPaused>k__BackingField"), Is.True);
                volume.IsPaused = false;
                Assert.That(((ITimeScaled)volume).IsPaused, Is.False);
            });
            LogAssert.NoUnexpectedReceived();
        }
    }
}
