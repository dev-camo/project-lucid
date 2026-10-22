using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.CharacterSelectScene;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.DreamPowerSlotRigs;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerCharacterSelectEquipDreamPower (Game.Runtime 0200088e, x86-64 0x621430..0x621FD0) as a real component on an owned
    // inactive host over the character select scene, a real DreamPowerStoreManager over the scene's open save, real image slot
    // widgets and an active mission context built by its shipped constructor. The call to action prompt (a real PromptTrigger on
    // Start, 0x621628) is not covered, nor is the store's own unlock check (IsAvailable with a failing requirement).
    public sealed class UIContainerCharacterSelectEquipDreamPowerTests
    {
        private const string PowerGuid = "lucid-equip-power", OtherGuid = "lucid-equip-other-power";

        private sealed class Rig
        {
            public UIContainerCharacterSelectEquipDreamPower Container;
            public UIWidgetButton Overlay;
            public Button OverlayButton;
            public GameObject ControllerGroup;
            public ImageSlot[] Slots;
            public DreamPowerStoreManager Store;
            public DreamPowerDefinition Power, OtherPower;
            public Sprite PowerIcon, OtherIcon, Silhouette;
            public CharacterArchetypeDefinition Archetype;
        }

        private static void Run(Action<Scene, Rig> body) => CharacterSelectScene.Run(scene =>
        {
            Owned owned = scene.Owned;
            owned.LeaseRow<ChallengeManager>(); // The store's and the mission context's field initialisers cache this row
            var rig = new Rig();
            rig.Store = owned.Register(owned.Host("Lucid equip store", false).AddComponent<DreamPowerStoreManager>());
            Set(rig.Store, "m_saveManager", scene.Save); // Reflection: the reference the store's start-up chain takes from the SaveManager row
            Set(rig.Store, "m_unlockRequirements", new RequirementGameplayLevelBase[0]); // Reflection: authored serialized field
            // The open save holds the settings the store's debug check reads once they are loaded.
            Set(scene.Save, "m_saveSettings", new SaveDataSettings()); // Reflection: the settings the SaveManager holds after loading

            rig.Container = owned.Host("Lucid equip dream power", false).AddComponent<UIContainerCharacterSelectEquipDreamPower>();
            rig.Slots = new[] { NewImageSlot(owned), NewImageSlot(owned) };
            Set(rig.Container, "m_slots", Array.ConvertAll(rig.Slots, slot => slot.Widget)); // Reflection: serialized references the editor assigns
            GameObject overlayHost = owned.Host("Lucid equip overlay", false);
            rig.OverlayButton = overlayHost.AddComponent<Button>();
            rig.Overlay = overlayHost.AddComponent<UIWidgetButton>();
            Set(rig.Overlay, "m_uiButton", rig.OverlayButton); // Reflection: serialized m_uiButton the editor assigns
            Set(rig.Container, "m_buttonOverlay", rig.Overlay); // Reflection: serialized m_buttonOverlay the editor assigns
            rig.ControllerGroup = owned.Host("Lucid equip controller group", false);
            Set(rig.Container, "m_controllerGroup", rig.ControllerGroup.AddComponent<RectTransform>()); // Reflection: serialized m_controllerGroup the editor assigns

            rig.PowerIcon = NewSprite(owned);
            rig.OtherIcon = NewSprite(owned);
            rig.Silhouette = NewSprite(owned);
            rig.Power = NewDefinition(owned, PowerGuid, Hardlight.Enums.Strings.CULTURE_NUMBER_MILLIONS, rig.PowerIcon);
            rig.OtherPower = NewDefinition(owned, OtherGuid, Hardlight.Enums.Strings.MISSING, rig.OtherIcon);
            scene.Data.DreamPowerDefinitions[PowerGuid] = rig.Power;
            scene.Data.DreamPowerDefinitions[OtherGuid] = rig.OtherPower;
            rig.Archetype = owned.Track(ScriptableObject.CreateInstance<CharacterArchetypeDefinition>());
            Set(rig.Archetype.SilhouetteImage, "m_loadedAsset", rig.Silhouette); // Reflection: the sprite an earlier Addressables load would hold
            scene.Data.CharacterArchetypes[CharacterArchetype.Climbing] = rig.Archetype;
            body(scene, rig);
        });

        // The scene's active mission: a shipped regular context over a definition for the given archetype, published through the
        // manager's public SetActiveMissionContext (which loads the mission's state); it is cleared again before the scene is released.
        private static void ActivateMission(Scene scene, CharacterArchetype archetype, bool dreamPowersEnabled)
        {
            var mission = scene.Owned.Track(ScriptableObject.CreateInstance<MissionDefinition>());
            Set(mission, "m_guid", "lucid-equip-mission"); // Reflection: authored GUID of a serialized definition
            Set(mission, "m_characterArchetype", archetype); // Reflection: authored mission data
            Set(mission, "m_enableDreamPowers", dreamPowersEnabled); // Reflection: authored mission data
            Set(mission, "m_subMissions", new System.Collections.Generic.List<MissionDefinition>()); // Reflection: authored (empty) list, which LoadMission reads
            var level = scene.Owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
            Set(level, "m_guid", "lucid-equip-level"); // Reflection: authored GUID of a serialized definition
            scene.Missions.SetActiveMissionContext(new MissionContextRegular(mission, level, false));
            scene.Owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => scene.Missions.SetActiveMissionContext(null)));
        }

        private static void Save(Scene scene, CharacterArchetype archetype, int slot, string guid) =>
            scene.Save.CurrentSave.GetOrCreateDreamPowerLoadout(archetype).Set(slot, guid);

        private static bool Playing(ImageSlot slot, AnimationClip clip) => slot.Animation.IsPlaying(clip.name);

        // Start 0x621430 with a dream power mission: the store is available (empty requirements, 0x6214A5), the new flag follows
        // the saved store state (0x6214E6), powers are enabled and the overlay and controller group shown (0x6215B0..0x6215DC), and
        // the archetype's silhouette and saved loadout are shown (SetupPowers 0x621670). A power the archetype may not use is shown
        // disabled and recorded for later refreshes; an empty slot is shown unequipped and not recorded.
        [Test]
        public void StartShowsTheArchetypeAndTheSavedLoadoutOfADreamPowerMission()
        {
            Run((scene, rig) =>
            {
                ActivateMission(scene, CharacterArchetype.Climbing, true);
                Save(scene, CharacterArchetype.Climbing, 0, PowerGuid);
                rig.OverlayButton.interactable = false;

                Call(rig.Container, "Start");

                Assert.That(rig.Container.Available.Value, Is.True);
                Assert.That(rig.Container.ShowNewFlag.Value, Is.False);
                Assert.That(rig.Container.PowersEnabled.Value, Is.True);
                Assert.That(rig.OverlayButton.interactable, Is.True);
                Assert.That(rig.ControllerGroup.activeSelf, Is.True);
                Assert.That(rig.Container.ShowArchetype.Value, Is.True);
                Assert.That(rig.Container.ArchetypeImage.Value, Is.SameAs(rig.Silhouette));
                Assert.That(Get<CharacterArchetypeDefinition>(rig.Container, "m_archetypeDefinition"), Is.SameAs(rig.Archetype));
                Assert.That(rig.Slots[0].Widget.Icon.Value, Is.SameAs(rig.PowerIcon));
                Assert.That(Playing(rig.Slots[0], rig.Slots[0].Disabled), Is.True, "the climbing archetype lists no powers");
                Assert.That(rig.Slots[1].Widget.Icon.Value, Is.Null);
                Assert.That(Playing(rig.Slots[1], rig.Slots[1].Unequip), Is.True);
                var recorded = Get<Dictionary<UIWidgetImageDreamPowerSlot, DreamPowerDefinition>>(rig.Container, "m_powersBySlotWidget");
                Assert.That(recorded.Count, Is.EqualTo(1));
                Assert.That(recorded[rig.Slots[0].Widget], Is.SameAs(rig.Power));
            });
        }

        // The unrestricted archetype (None) allows every power, so the equipped slot is not disabled (the Disable call at 0x62191B is
        // skipped), the archetype image is not shown (0x621713 setne), and the new flag is true while the store is flagged new.
        [Test]
        public void AnUnrestrictedMissionShowsNoArchetypeAndKeepsPowersEnabled()
        {
            Run((scene, rig) =>
            {
                ActivateMission(scene, CharacterArchetype.None, true);
                Save(scene, CharacterArchetype.None, 1, OtherGuid);
                scene.Save.CurrentSave.DreamPowerStore.IsNewState = DreamPowerStoreIsNewState.IsNew;

                Call(rig.Container, "Start");

                Assert.That(rig.Container.ShowNewFlag.Value, Is.True);
                Assert.That(rig.Container.ShowArchetype.Value, Is.False);
                Assert.That(rig.Container.ArchetypeImage.Value, Is.Null);
                Assert.That(Get<CharacterArchetypeDefinition>(rig.Container, "m_archetypeDefinition") == null, Is.True);
                Assert.That(rig.Slots[1].Widget.Icon.Value, Is.SameAs(rig.OtherIcon));
                Assert.That(Playing(rig.Slots[1], rig.Slots[1].Disabled), Is.False);
                Assert.That(Playing(rig.Slots[1], rig.Slots[1].Equip), Is.True);
                Assert.That(Playing(rig.Slots[0], rig.Slots[0].Unequip), Is.True);
            });
        }

        // A mission without dream powers (0x62158E returns false) clears the enabled flag, makes the overlay not interactable
        // and hides the controller group (0x621637..0x62165E); no slot is touched.
        [Test]
        public void AMissionWithoutDreamPowersHidesTheLoadout()
        {
            Run((scene, rig) =>
            {
                ActivateMission(scene, CharacterArchetype.Climbing, false);
                Save(scene, CharacterArchetype.Climbing, 0, PowerGuid);
                rig.Container.PowersEnabled.Value = true;
                rig.ControllerGroup.SetActive(true);

                Call(rig.Container, "Start");

                Assert.That(rig.Container.PowersEnabled.Value, Is.False);
                Assert.That(rig.OverlayButton.interactable, Is.False);
                Assert.That(rig.ControllerGroup.activeSelf, Is.False);
                Assert.That(rig.Slots[0].Widget.Icon.Value, Is.Null);
                Assert.That(rig.Container.ShowArchetype.Value, Is.False);
            });
        }

        // RefreshPowers 0x621DC0 does nothing while powers are disabled (0x621E62); otherwise every recorded slot is enabled when
        // the archetype allows its power and disabled when it does not.
        [Test]
        public void RefreshingEnablesOrDisablesEachRecordedSlotByTheArchetype()
        {
            Run((scene, rig) =>
            {
                ActivateMission(scene, CharacterArchetype.None, true);
                Save(scene, CharacterArchetype.None, 0, PowerGuid);
                Call(rig.Container, "Start");
                rig.Container.PowersEnabled.Value = false;

                rig.Container.RefreshPowers(CharacterArchetype.Climbing);
                Assert.That(Playing(rig.Slots[0], rig.Slots[0].Disabled), Is.False, "disabled powers refresh nothing");

                rig.Container.PowersEnabled.Value = true;
                rig.Container.RefreshPowers(CharacterArchetype.Climbing);
                Assert.That(Playing(rig.Slots[0], rig.Slots[0].Disabled), Is.True);

                rig.Container.RefreshPowers(CharacterArchetype.None);
                Assert.That(Playing(rig.Slots[0], rig.Slots[0].Enabled), Is.True);
            });
        }

        // ArchetypeAllowsPower 0x621D60 asks the store, which lets the unrestricted archetype use any power and refuses an archetype
        // with no collated powers.
        [Test]
        public void TheContainerAsksTheStoreWhichArchetypeMayUseAPower()
        {
            Run((scene, rig) =>
            {
                Assert.That(Call(rig.Container, "ArchetypeAllowsPower", CharacterArchetype.None, rig.Power), Is.EqualTo(true));
                Assert.That(Call(rig.Container, "ArchetypeAllowsPower", CharacterArchetype.Climbing, rig.Power), Is.EqualTo(false));
            });
        }

        // IsAnyPowerEquipped 0x621950 is true as soon as any archetype's saved loadout holds a slot (0x621BFB), whichever archetype.
        [Test]
        public void ApowerIsEquippedWhenAnyArchetypesLoadoutHoldsASlot()
        {
            Run((scene, rig) =>
            {
                Assert.That(Call(rig.Container, "IsAnyPowerEquipped"), Is.EqualTo(false), "no loadouts");

                scene.Save.CurrentSave.GetOrCreateDreamPowerLoadout(CharacterArchetype.Dashing);
                Assert.That(Call(rig.Container, "IsAnyPowerEquipped"), Is.EqualTo(false), "an empty loadout");

                Save(scene, CharacterArchetype.Flying, 2, PowerGuid);
                Assert.That(Call(rig.Container, "IsAnyPowerEquipped"), Is.EqualTo(true));
            });
        }

        // OnDestroy 0x621D00 releases the archetype image Start retained, if there is one.
        [Test]
        public void DestroyReleasesTheArchetypeImage()
        {
            Run((scene, rig) =>
            {
                Assert.DoesNotThrow(() => Call(rig.Container, "OnDestroy"), "nothing was retained");
                ActivateMission(scene, CharacterArchetype.Climbing, true);
                Call(rig.Container, "Start");
                Assert.That(Get<int>(rig.Archetype.SilhouetteImage, "m_refCount"), Is.EqualTo(1));

                Call(rig.Container, "OnDestroy");

                Assert.That(Get<int>(rig.Archetype.SilhouetteImage, "m_refCount"), Is.Zero);
            });
        }
    }
}
