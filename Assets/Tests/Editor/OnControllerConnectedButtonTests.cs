using System;
using System.Collections.Generic;
using Apple.GameController.Controller;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // OnControllerConnectedButton (Game.Runtime 020006b2, bodies 0x58BA50..0x58C380) against the 1.10.1
    // x86-64 bodies cited per case.
    //
    // Not covered: Start and the enabled branch of OnDestroy. They run only after Start finds an
    // ApplePluginControllerProvider behind the current EventSystem's HLInputModule, which needs a running
    // event system. The enabled branches of TryGetActiveControllerType and SimulateButtonPress read only
    // their own fields, so they are driven below with an enabled button and a real provider.
    public sealed class OnControllerConnectedButtonTests
    {
        // The InputType literals are the 32-bit words of the native table at 0x29A79B4, in GCControllerType order.
        private const uint MfiController = 0xDC966E01;
        private const uint XboneController = 0x89BC61FF;
        private const uint PS4Controller = 0x8EB02DBD;
        private const uint PS5Controller = 0x4F3EF27D;
        private const uint Unsupported = 0x480DD923;

        private static int Raw(uint word) => unchecked((int)word);

        private static OnControllerConnectedButton NewButton(Owned owned) =>
            owned.Host("Lucid controller connected button", false).AddComponent<OnControllerConnectedButton>();

        private static ControllerConnectedEventArgs Connected(string productCategory) =>
            new ControllerConnectedEventArgs(new GCController(new GCControllerHandle { ProductCategory = productCategory }));

        // Constructor 0x58C360 stores GameInput 0x06BFA431 and InputTrigger 1 (Down); the rest is zero.
        [Test]
        public void ConstructionSuppliesTheControllerConnectedInputAsADownPress()
        {
            Owned.Run(owned =>
            {
                OnControllerConnectedButton button = NewButton(owned);
                Assert.That((int)Get<GameInput>(button, "m_inputToSupply"), Is.EqualTo(0x06BFA431));
                Assert.That((int)Get<InputTrigger>(button, "m_inputTrigger"), Is.EqualTo(1));
                Assert.That((int)Get<InputType>(button, "m_inputType"), Is.Zero);
                Assert.That(Get<bool>(button, "m_systemEnabled"), Is.False);
            });
        }

        // ConvertGCControllerTypeToInputType 0x58C290: values 0..4 index the table and anything else
        // (unsigned compare above 4) is Unsupported. Siri Remote (4) maps to Unsupported in the table.
        [Test]
        public void EveryControllerTypeMapsToItsNativeInputType()
        {
            Owned.Run(owned =>
            {
                OnControllerConnectedButton button = NewButton(owned);
                var expected = new (int controllerType, uint inputType)[]
                {
                    (0, MfiController), (1, XboneController), (2, PS4Controller), (3, PS5Controller), (4, Unsupported),
                    (5, Unsupported), (-1, Unsupported), (100, Unsupported),
                };
                foreach ((int controllerType, uint inputType) in expected)
                {
                    var actual = (InputType)Call(button, "ConvertGCControllerTypeToInputType", (GCControllerType)controllerType);
                    Assert.That((int)actual, Is.EqualTo(Raw(inputType)), "controller type " + controllerType);
                }
            });
        }

        // SetActiveControllerType 0x58C220 reads the handle's type twice (GetControllerType, 0x58C241 and
        // 0x58C25A): a Siri Remote (4) leaves the input type alone, any other type is converted. A product
        // category the handle does not recognise is type 0 (Unknown), which converts to MfiController.
        [Test]
        public void AConnectedControllerSetsTheInputTypeUnlessItIsASiriRemote()
        {
            Owned.Run(owned =>
            {
                OnControllerConnectedButton button = NewButton(owned);
                Call(button, "SetActiveControllerType", null, Connected("DualShock 4"));
                Assert.That((int)Get<InputType>(button, "m_inputType"), Is.EqualTo(Raw(PS4Controller)));

                Call(button, "SetActiveControllerType", null, Connected("Siri Remote"));
                Assert.That((int)Get<InputType>(button, "m_inputType"), Is.EqualTo(Raw(PS4Controller)), "the Siri Remote is ignored");

                Call(button, "SetActiveControllerType", null, Connected("DualSense"));
                Assert.That((int)Get<InputType>(button, "m_inputType"), Is.EqualTo(Raw(PS5Controller)));

                Call(button, "SetActiveControllerType", null, Connected("Xbox One"));
                Assert.That((int)Get<InputType>(button, "m_inputType"), Is.EqualTo(Raw(XboneController)));

                Call(button, "SetActiveControllerType", null, Connected("Generic Gamepad"));
                Assert.That((int)Get<InputType>(button, "m_inputType"), Is.EqualTo(Raw(MfiController)));
            });
        }

        // TryGetActiveControllerType 0x58BFE0 and SimulateButtonPress 0x58C180 both test m_systemEnabled
        // first (0x58C016, 0x58C186), so a button that never started touches no provider and presses nothing.
        [Test]
        public void ADisabledButtonDoesNothing()
        {
            Owned.Run(owned =>
            {
                OnControllerConnectedButton button = NewButton(owned);
                int presses = 0;
                button.ButtonHandlers += (joystick, input, value, type) => presses++;
                button.ButtonDownHandlers += (joystick, input, value, type) => presses++;
                button.ButtonUpHandlers += (joystick, input, value, type) => presses++;

                Assert.DoesNotThrow(() => Call(button, "TryGetActiveControllerType"));
                Assert.DoesNotThrow(() => Call(button, "SimulateButtonPress"));
                Assert.That(presses, Is.Zero);
                Assert.That((int)Get<InputType>(button, "m_inputType"), Is.Zero);
            });
        }

        private static ApplePluginControllerProvider ProviderWith(params string[] productCategories)
        {
            var provider = new ApplePluginControllerProvider(1f);
            var list = Get<List<GCController>>(provider, "m_applePluginControllers");
            foreach (string category in productCategories)
            {
                list.Add(new GCController(new GCControllerHandle { ProductCategory = category }));
            }
            return provider;
        }

        private static int ActiveTypeFor(OnControllerConnectedButton button, params string[] productCategories)
        {
            Set(button, "m_systemEnabled", true);
            Set(button, "m_applePluginControllerProvider", ProviderWith(productCategories));
            Set(button, "m_inputType", (InputType)0);
            Call(button, "TryGetActiveControllerType");
            return (int)Get<InputType>(button, "m_inputType");
        }

        // TryGetActiveControllerType 0x58BFE0 walks the list from Count-1 down to 0 (0x58C0A0 dec/js) and
        // skips type 4 (0x58C124 je), so the last non-Siri controller wins; a forward scan would give PS4.
        [Test]
        public void TheLastControllerThatIsNotASiriRemoteDecidesTheInputType()
        {
            Owned.Run(owned =>
            {
                OnControllerConnectedButton button = NewButton(owned);
                Assert.That(ActiveTypeFor(button, "DualShock 4", "Siri Remote", "Xbox One", "Siri Remote"), Is.EqualTo(Raw(XboneController)));
                Assert.That(ActiveTypeFor(button, "Xbox One", "DualShock 4", "Siri Remote"), Is.EqualTo(Raw(PS4Controller)));
            });
        }

        [Test]
        public void AnEmptyOrSiriRemoteOnlyProviderLeavesTheInputTypeAlone()
        {
            Owned.Run(owned =>
            {
                OnControllerConnectedButton button = NewButton(owned);
                Assert.That(ActiveTypeFor(button), Is.Zero);
                Assert.That(ActiveTypeFor(button, "Siri Remote", "Siri Remote"), Is.Zero);
            });
        }

        // SimulateButtonPress 0x58C180 switches on m_inputTrigger (0x58C19D/0x58C1D6/0x58C1A8 -> 0x58C1B1):
        // 0 fires ButtonHandlers, 1 ButtonDownHandlers, 2 ButtonUpHandlers, each with (0, GameInput
        // 0x06BFA431, 1.0f [constant 0x29A6BE0], the input type).
        [TestCase(0, "held")]
        [TestCase(1, "down")]
        [TestCase(2, "up")]
        public void AnEnabledButtonFiresOnlyTheHandlerOfItsTrigger(int trigger, string expected)
        {
            Owned.Run(owned =>
            {
                OnControllerConnectedButton button = NewButton(owned);
                Set(button, "m_systemEnabled", true);
                Set(button, "m_inputTrigger", (InputTrigger)trigger);
                Set(button, "m_inputType", (InputType)Raw(XboneController));
                var fired = new List<string>();
                button.ButtonHandlers += (joystick, input, value, type) => fired.Add($"held {joystick} {(int)input} {value} {(int)type}");
                button.ButtonDownHandlers += (joystick, input, value, type) => fired.Add($"down {joystick} {(int)input} {value} {(int)type}");
                button.ButtonUpHandlers += (joystick, input, value, type) => fired.Add($"up {joystick} {(int)input} {value} {(int)type}");

                Call(button, "SimulateButtonPress");

                Assert.That(fired, Is.EqualTo(new[] { $"{expected} 0 {0x06BFA431} 1 {Raw(XboneController)}" }));
            });
        }

        [Test]
        public void AnUnknownTriggerThrowsAndAMissingHandlerIsSkipped()
        {
            Owned.Run(owned =>
            {
                OnControllerConnectedButton button = NewButton(owned);
                Set(button, "m_systemEnabled", true);
                Set(button, "m_inputTrigger", (InputTrigger)3);
                Assert.Throws<ArgumentOutOfRangeException>(() => Call(button, "SimulateButtonPress")); // 0x58C1E6

                foreach (int trigger in new[] { 0, 1, 2 })
                {
                    Set(button, "m_inputTrigger", (InputTrigger)trigger);
                    Assert.DoesNotThrow(() => Call(button, "SimulateButtonPress"), "trigger " + trigger);
                }
            });
        }

        // OnDestroy 0x58C2B0 unregisters from the control mapping first (0x58C2FC), which a button that
        // never started does not have.
        [Test]
        public void OnDestroyBeforeStartFaultsOnTheMissingControlMapping()
        {
            Owned.Run(owned =>
            {
                OnControllerConnectedButton button = NewButton(owned);
                Assert.Throws<NullReferenceException>(() => Call(button, "OnDestroy"));
            });
        }
    }
}
