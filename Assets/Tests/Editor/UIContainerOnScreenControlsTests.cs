using System.Collections.Generic;
using System.Linq;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.OnScreenControlTestServices;
using static ProjectLucid.Tests.TriggerConditionTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerOnScreenControls (020008f3) against its 1.10.1 x86-64 bodies (0x66BE80..0x66C6E0). The container is a
    // real component on an owned inactive host (no Awake runs, so each case calls the original methods itself),
    // talking to a real MessageManager, CharacterManager and SaveManager (OnScreenControlTestServices). A character
    // is current for the container once OnCharacterChange has been called with it, which is what the
    // CharacterManager does when the character becomes valid.
    public sealed class UIContainerOnScreenControlsTests
    {
        private sealed class PresentAbility : ActorAbility
        {
            public override AbilityDefinition DefinitionBase => null;
        }

        private static UIContainerOnScreenControls NewContainer(Services services) =>
            services.Owned.Host("Lucid owned on-screen controls", false).AddComponent<UIContainerOnScreenControls>();

        private static IEnumerable<ScreenControlsGroup> Groups(List<ScreenControlGroupMessageData> received) => received.Select(message => message.GroupToEnable);

        // Awake 0x66BE80 registers OnCharacterChange with the CharacterManager reference (not invoked at once, as no
        // character is valid), then publishes the Default group (esi = 0x23E4013A) under the container with an empty
        // completion; the published message object is kept for GetLastControlGroupMessageData (0x66C6D0).
        [Test]
        public void AwakeAnnouncesTheDefaultGroupAndListensForCharacterChanges()
        {
            With(services =>
            {
                UIContainerOnScreenControls container = NewContainer(services);
                Assert.That(container.GetLastControlGroupMessageData(), Is.Null);
                List<ScreenControlGroupMessageData> received = Capture<ScreenControlGroupMessageData>(services.Messages, container);

                Call(container, "Awake");

                Assert.That(Groups(received), Is.EqualTo(new[] { ScreenControlsGroup.Default }));
                Assert.That(container.GetLastControlGroupMessageData(), Is.SameAs(received[0]));
                Assert.That(CharacterChangeTargets(services.Characters), Is.EqualTo(new object[] { container }));
            });
        }

        // OnDestroy 0x66C2B0 unsubscribes from the character's messages, then (reference valid) releases the character
        // change handler.
        [Test]
        public void DestroyingStopsListeningToTheCharacterAndTheCharacterManager()
        {
            With(services =>
            {
                UIContainerOnScreenControls container = NewContainer(services);
                Set(services.Save, "m_saveSettings", new SaveDataSettings()); // loaded by the save flow in the original
                List<ScreenControlGroupMessageData> received = Capture<ScreenControlGroupMessageData>(services.Messages, container);
                Call(container, "Awake");
                Call(container, "OnCharacterChange", services.Character);
                Assert.That(Request(services.Messages, services.Character, ScreenControlsGroup.InAir), Is.EqualTo(1));
                Assert.That(Groups(received), Is.EqualTo(new[] { ScreenControlsGroup.Default, ScreenControlsGroup.InAir }));

                Call(container, "OnDestroy");

                Assert.That(CharacterChangeTargets(services.Characters), Is.Empty);
                Request(services.Messages, services.Character, ScreenControlsGroup.Default);
                Assert.That(received.Count, Is.EqualTo(2), "the request is no longer heard");
            });
        }

        // OnRequestScreenControlGroup 0x66C4A0: group 0 (None) is announced as Default; any other group is announced as
        // requested, except an InAir request (0xC2733EB7) is dropped, without completing, while the current character's
        // teleport is in progress (TeleportIsInProgress, 0x66C513).
        [Test]
        public void RequestsAreAnnouncedAndAnInAirRequestWaitsOutATeleport()
        {
            With(services =>
            {
                UIContainerOnScreenControls container = NewContainer(services);
                Set(services.Save, "m_saveSettings", new SaveDataSettings()); // loaded by the save flow in the original
                List<ScreenControlGroupMessageData> received = Capture<ScreenControlGroupMessageData>(services.Messages, container);
                Call(container, "Awake");
                Call(container, "OnCharacterChange", services.Character);
                received.Clear();

                Assert.That(Request(services.Messages, services.Character, ScreenControlsGroup.None), Is.EqualTo(1));
                Assert.That(Request(services.Messages, services.Character, ScreenControlsGroup.InAir), Is.EqualTo(1));
                Assert.That(Request(services.Messages, services.Character, ScreenControlsGroup.Default), Is.EqualTo(1));
                Assert.That(Groups(received), Is.EqualTo(new[] { ScreenControlsGroup.Default, ScreenControlsGroup.InAir, ScreenControlsGroup.Default }));

                services.Character.Storage.SetValue(ActorFSMKeys.RespawnTeleportToAir, true);
                received.Clear();
                Assert.That(Request(services.Messages, services.Character, ScreenControlsGroup.InAir), Is.EqualTo(0), "dropped without completing");
                Assert.That(received, Is.Empty);
                Assert.That(Request(services.Messages, services.Character, ScreenControlsGroup.Default), Is.EqualTo(1), "only InAir waits");
                Assert.That(Request(services.Messages, services.Character, ScreenControlsGroup.None), Is.EqualTo(1));
                Assert.That(Groups(received), Is.EqualTo(new[] { ScreenControlsGroup.Default, ScreenControlsGroup.Default }));
            });
        }

        // OnCharacterChange 0x66C550 first unsubscribes the previous character (0x66C380), so only the new character's
        // requests are heard afterwards.
        [Test]
        public void ChangingCharacterMovesTheRequestSubscription()
        {
            With(services =>
            {
                UIContainerOnScreenControls container = NewContainer(services);
                Set(services.Save, "m_saveSettings", new SaveDataSettings()); // loaded by the save flow in the original
                Character second = NewCharacter(services.Owned, new FSMStorage(0));
                List<ScreenControlGroupMessageData> received = Capture<ScreenControlGroupMessageData>(services.Messages, container);
                Call(container, "Awake");
                received.Clear();

                Call(container, "OnCharacterChange", services.Character);
                Request(services.Messages, services.Character, ScreenControlsGroup.InAir);
                Request(services.Messages, second, ScreenControlsGroup.Default);
                Assert.That(Groups(received), Is.EqualTo(new[] { ScreenControlsGroup.InAir }));

                Call(container, "OnCharacterChange", second);
                received.Clear();
                Request(services.Messages, services.Character, ScreenControlsGroup.InAir);
                Assert.That(received, Is.Empty, "the previous character is no longer heard");
                Request(services.Messages, second, ScreenControlsGroup.InAir);
                Assert.That(Groups(received), Is.EqualTo(new[] { ScreenControlsGroup.InAir }));
            });
        }

        // OnSaveManagerValid 0x66C150 (run by OnCharacterChange for the valid SaveManager): the stick and snap flags are
        // copied from the settings (+0x35, +0x34); both tertiary buttons need the character to have the AirStompAttack
        // (0x4DBE014F) or SpinDashCharge (0x21DFB747) ability, the left one for button mapping All (0x5240CCA8) or
        // Primary (0xD7FFEB63) and the right one for All or Secondary (0xA06E9505). An absent character counts as
        // having neither ability.
        [Test]
        public void SettingsDriveTheStickSnapAndTertiaryButtons()
        {
            With(services =>
            {
                UIContainerOnScreenControls container = NewContainer(services);
                var settings = new SaveDataSettings
                {
                    DynamicMovementStick = true,
                    CameraSnapBehindCharacterButtonVisible = false,
                };
                Set(services.Save, "m_saveSettings", settings); // loaded by the save flow in the original
                Call(container, "Awake");
                var abilities = Get<Dictionary<ActorAbilityType, ActorAbility>>(services.Character, "m_abilities"); // built from the FSM definitions in the original

                settings.AirStompAttackMapping = ButtonMappingType.All;
                Call(container, "OnSaveManagerValid", services.Save);
                Assert.That((container.DynamicStick.Value, container.CameraSnapBehindCharacterButtonVisible.Value), Is.EqualTo((true, false)));
                Assert.That((container.TertiaryButtonLeftVisible.Value, container.TertiaryButtonRightVisible.Value), Is.EqualTo((false, false)), "no character yet");

                settings.DynamicMovementStick = false;
                settings.CameraSnapBehindCharacterButtonVisible = true;
                Call(container, "OnCharacterChange", services.Character);
                Assert.That((container.DynamicStick.Value, container.CameraSnapBehindCharacterButtonVisible.Value), Is.EqualTo((false, true)));
                Assert.That((container.TertiaryButtonLeftVisible.Value, container.TertiaryButtonRightVisible.Value), Is.EqualTo((false, false)), "no ability");

                foreach (ActorAbilityType[] have in new[]
                {
                    new[] { ActorAbilityType.Character_AirStompAttack },
                    new[] { ActorAbilityType.Character_SpinDashCharge },
                    new[] { ActorAbilityType.Character_AirStompAttack, ActorAbilityType.Character_SpinDashCharge },
                })
                {
                    abilities.Clear();
                    foreach (ActorAbilityType type in have) abilities[type] = new PresentAbility();
                    foreach ((ButtonMappingType mapping, bool left, bool right) in new[]
                    {
                        (ButtonMappingType.None, false, false), (ButtonMappingType.All, true, true),
                        (ButtonMappingType.Primary, true, false), (ButtonMappingType.Secondary, false, true),
                    })
                    {
                        settings.AirStompAttackMapping = mapping;
                        Call(container, "OnSaveManagerValid", services.Save);
                        string label = string.Join("+", have) + " " + mapping;
                        Assert.That((container.TertiaryButtonLeftVisible.Value, container.TertiaryButtonRightVisible.Value), Is.EqualTo((left, right)), label);
                    }
                }

                abilities.Clear();
                settings.AirStompAttackMapping = ButtonMappingType.All;
                Call(container, "OnSaveManagerValid", services.Save);
                Assert.That((container.TertiaryButtonLeftVisible.Value, container.TertiaryButtonRightVisible.Value), Is.EqualTo((false, false)));
            });
        }
    }
}
