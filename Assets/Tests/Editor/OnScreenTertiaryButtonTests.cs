using System;
using System.Collections.Generic;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.OnScreenControlTestServices;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // OnScreenTertiaryButton (0200097b) against its 1.10.1 x86-64 bodies (0x6924E0..0x692970), with the real
    // CharacterManager, SaveManager and MessageManager of OnScreenControlTestServices and a real
    // UIContainerOnScreenControls whose announcements the button follows. The player character gets abilities
    // through the dictionary the FSM definitions fill in the original. The air stomp icon (+0x20) and the spin dash
    // icon (+0x28) are read through their active flag.
    public sealed class OnScreenTertiaryButtonTests
    {
        private sealed class PresentAbility : ActorAbility
        {
            public override AbilityDefinition DefinitionBase => null;
        }

        private sealed class Rig
        {
            public Services Services;
            public OnScreenTertiaryButton Button;
            public UIContainerOnScreenControls Container;
            public GameObject AirStompIcon;
            public GameObject SpinDashIcon;
            public Dictionary<ActorAbilityType, ActorAbility> Abilities;

            public (bool airStomp, bool spinDash) Icons => (AirStompIcon.activeSelf, SpinDashIcon.activeSelf);

            public void HaveAbilities(bool airStomp, bool spinDash)
            {
                Abilities.Clear();
                if (airStomp) Abilities[ActorAbilityType.Character_AirStompAttack] = new PresentAbility();
                if (spinDash) Abilities[ActorAbilityType.Character_SpinDashCharge] = new PresentAbility();
            }

            public void Announce(ScreenControlsGroup group) => Request(Services.Messages, Services.Character, group);
        }

        private static void WithButton(bool announced, Action<Rig> body) => With(services =>
        {
            var rig = new Rig { Services = services };
            rig.AirStompIcon = services.Owned.Track(new GameObject("Lucid owned air stomp icon"));
            rig.SpinDashIcon = services.Owned.Track(new GameObject("Lucid owned spin dash icon"));
            rig.Container = services.Owned.Host("Lucid owned on-screen controls", false).AddComponent<UIContainerOnScreenControls>();
            Set(services.Save, "m_saveSettings", new SaveDataSettings()); // loaded by the save flow in the original
            if (announced)
            {
                Call(rig.Container, "Awake");
                Call(rig.Container, "OnCharacterChange", services.Character);
            }
            rig.Abilities = Get<Dictionary<ActorAbilityType, ActorAbility>>(services.Character, "m_abilities"); // built from the FSM definitions in the original
            rig.Button = services.Owned.Host("Lucid owned tertiary button", false).AddComponent<OnScreenTertiaryButton>();
            Set(rig.Button, "m_airStompIcon", rig.AirStompIcon); // serialized in the original
            Set(rig.Button, "m_spinDashIcon", rig.SpinDashIcon); // serialized in the original
            Set(rig.Button, "m_uiContainerOnScreenControls", rig.Container); // serialized in the original
            body(rig);
        });

        // RefreshIcons 0x6925E0 (inlined in Awake and OnCharacterChange): a character with one of the abilities shows its
        // icon; with both, the air stomp icon shows while the last group is InAir (0xC2733EB7) and the spin dash icon
        // otherwise.
        [Test]
        public void ACharacterWithOneAbilityShowsItsIconAndOneWithBothShowsTheIconOfTheGroup()
        {
            WithButton(false, rig =>
            {
                foreach ((bool airStomp, bool spinDash, (bool airStomp, bool spinDash) icons) in new[]
                {
                    (false, false, (false, false)), (true, false, (true, false)), (false, true, (false, true)), (true, true, (false, true)),
                })
                {
                    rig.AirStompIcon.SetActive(true);
                    rig.SpinDashIcon.SetActive(true);
                    rig.HaveAbilities(airStomp, spinDash);
                    Call(rig.Button, "OnCharacterChange", rig.Services.Character);
                    Assert.That(rig.Icons, Is.EqualTo(icons), "air stomp " + airStomp + " spin dash " + spinDash);
                }
            });
        }

        // DoUpdate 0x692660 follows the last group the container announced (None before any announcement); only a
        // change of group refreshes the icons, and OnEnable and Update both run it.
        [Test]
        public void TheIconsFollowTheAnnouncedGroupOnlyWhenItChanges()
        {
            WithButton(true, rig =>
            {
                rig.HaveAbilities(true, true);
                Call(rig.Button, "OnCharacterChange", rig.Services.Character);
                Assert.That(rig.Icons, Is.EqualTo((false, true)), "no group seen yet");

                Call(rig.Button, "Update");
                Assert.That(rig.Icons, Is.EqualTo((false, true)), "Default");

                rig.Announce(ScreenControlsGroup.InAir);
                Call(rig.Button, "OnEnable");
                Assert.That(rig.Icons, Is.EqualTo((true, false)), "InAir");

                rig.AirStompIcon.SetActive(false);
                rig.SpinDashIcon.SetActive(true);
                Call(rig.Button, "Update");
                Assert.That(rig.Icons, Is.EqualTo((false, true)), "the same group refreshes nothing");

                rig.AirStompIcon.SetActive(true);
                rig.Announce(ScreenControlsGroup.Default);
                Call(rig.Button, "Update");
                Assert.That(rig.Icons, Is.EqualTo((false, true)), "back to Default");
                rig.SpinDashIcon.SetActive(false);
                rig.Announce(ScreenControlsGroup.InAir);
                Call(rig.Button, "Update");
                Assert.That(rig.Icons, Is.EqualTo((true, false)), "InAir again");
            });
        }

        // Without a container announcement the group is None (xor eax, eax at 0x692684): the spin dash icon wins.
        [Test]
        public void WithoutAnAnnouncementTheGroupIsNone()
        {
            WithButton(false, rig =>
            {
                rig.HaveAbilities(true, true);
                Call(rig.Button, "OnCharacterChange", rig.Services.Character);
                Call(rig.Button, "Update");
                Assert.That(rig.Icons, Is.EqualTo((false, true)));
                Assert.That(rig.Container.GetLastControlGroupMessageData(), Is.Null);
            });
        }

        // OnCharacterChange 0x6927D0 stores the character and stops there when it is destroyed or null.
        [Test]
        public void ANullCharacterLeavesTheIconsAsTheyWere()
        {
            WithButton(false, rig =>
            {
                rig.HaveAbilities(true, true);
                rig.AirStompIcon.SetActive(true);
                rig.SpinDashIcon.SetActive(true);
                Assert.DoesNotThrow(() => Call(rig.Button, "OnCharacterChange", new object[] { null }));
                Assert.That(rig.Icons, Is.EqualTo((true, true)));
            });
        }

        // Awake 0x6924E0 registers OnCharacterChange with the CharacterManager (not invoked at once with no valid character) and
        // shows no icon for a character with no ability; OnDestroy 0x692720 releases the handler.
        [Test]
        public void AwakeListensForCharacterChangesAndDestroyingStops()
        {
            WithButton(false, rig =>
            {
                rig.AirStompIcon.SetActive(true);
                rig.SpinDashIcon.SetActive(true);
                Call(rig.Button, "Awake");
                Assert.That(CharacterChangeTargets(rig.Services.Characters), Is.EqualTo(new object[] { rig.Button }));
                Assert.That(rig.Icons, Is.EqualTo((false, false)), "no abilities yet");

                Call(rig.Button, "OnDestroy");
                Assert.That(CharacterChangeTargets(rig.Services.Characters), Is.Empty);
            });
        }

        // OnValidate 0x6928D0 only asks CanValidate, outside play mode (Application.isPlaying is false here).
        [Test]
        public void ValidatingChangesNothing()
        {
            WithButton(false, rig =>
            {
                rig.AirStompIcon.SetActive(true);
                Assert.DoesNotThrow(() => Call(rig.Button, "OnValidate"));
                Assert.That(rig.Icons, Is.EqualTo((true, true)));
            });
        }
    }
}
