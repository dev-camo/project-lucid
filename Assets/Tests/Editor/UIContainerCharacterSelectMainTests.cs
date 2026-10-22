using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static ProjectLucid.Tests.CharacterSelectScene;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.DreamPowerSlotRigs;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerCharacterSelectMain (Game.Runtime 0200088f, x86-64 0x622270..0x623C60) as a real component on an owned
    // inactive host over the character select scene, with real toggle widgets, continue button and input event. The
    // equip container is a real one with its powers disabled. The screen's Awake takes no meta game selection from the
    // scene, so the zone colours and the reward counter (which need a selected level) are not covered, nor is Start's
    // call to action prompt (a real PromptTrigger) or the swap of the character manager. The UINavigationManager row is empty,
    // so the navigation selection after a toggle is chosen (0x623663) is not covered either. The equip container is built with a
    // store and slots only by the case that checks the refresh at the end of SetCharacter.
    public sealed class UIContainerCharacterSelectMainTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");
        private const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic;

        private sealed class Rig
        {
            public UIContainerCharacterSelectMain Container;
            public UIWidgetButton Continue;
            public Button ContinueButton;
            public InputEvent ContinueInput;
            public UIToggleGroup Group;
            public UIContainerCharacterSelectEquipDreamPower Equip;
            public UIModernEvent OpenSkins;
            public readonly Dictionary<CharacterId, UIWidgetToggle> Toggles = new Dictionary<CharacterId, UIWidgetToggle>();
        }

        // The toggles are added in the given order, which is the order the screen visits them.
        private static void Run(Action<Scene, Rig> body, params CharacterId[] toggles) => CharacterSelectScene.Run(scene =>
        {
            Owned owned = scene.Owned;
            owned.LeaseRow<UINavigationManager>(); // The screen's field initialiser caches this row
            var rig = new Rig();
            rig.Container = owned.Host("Lucid character select main", false).AddComponent<UIContainerCharacterSelectMain>();
            GameObject buttonHost = owned.Host("Lucid character select continue", false);
            rig.ContinueButton = buttonHost.AddComponent<Button>();
            rig.Continue = buttonHost.AddComponent<UIWidgetButton>();
            Set(rig.Continue, "m_uiButton", rig.ContinueButton); // Reflection: serialized reference the editor assigns
            rig.ContinueInput = owned.Host("Lucid character select continue input", false).AddComponent<InputEvent>();
            rig.Group = owned.Host("Lucid character select group", false).AddComponent<UIToggleGroup>();
            rig.Group.AllowSwitchOff = true;
            rig.Equip = owned.Host("Lucid character select equip", false).AddComponent<UIContainerCharacterSelectEquipDreamPower>();
            rig.OpenSkins = owned.Track(ScriptableObject.CreateInstance<UIModernEvent>());
            Set(rig.Container, "m_charactersToggleGroup", rig.Group); // Reflection: serialized references the editor assigns
            Set(rig.Container, "m_continueButton", rig.Continue);
            Set(rig.Container, "m_continueInputEvent", rig.ContinueInput);
            Set(rig.Container, "m_equipDreamPowerContainer", rig.Equip); // Reflection: serialized m_equipDreamPowerContainer the editor assigns
            Set(rig.Container, "m_openSkinSelectionEvent", rig.OpenSkins); // Reflection: serialized m_openSkinSelectionEvent the editor assigns
            var dictionary = Get<SerializableDictionary<CharacterId, UIWidgetToggle>>(rig.Container, "m_characterIdToGameObject");
            foreach (CharacterId id in toggles)
            {
                GameObject host = owned.Host("Lucid character toggle " + id, false);
                host.AddComponent<UIToggle>();
                var widget = host.AddComponent<UIWidgetToggle>();
                Call(widget, "Reset"); // The editor message that assigns the sibling toggle
                rig.Toggles[id] = widget;
                dictionary[id] = widget;
            }
            // Edit Mode does not send OnDestroy to a component that never woke, so the static input type subscription is removed here.
            VerificationFixtureOwnership.RunThenCleanUp(() => body(scene, rig), () => Call(rig.Container, "OnDestroy"));
        });

        private static bool Subscribed(UIContainerCharacterSelectMain container)
        {
            var handlers = (Delegate)typeof(ControlMapping).GetField("OnUpdateLastInputType", Statics).GetValue(null);
            return handlers.GetInvocationList().Any(handler => ReferenceEquals(handler.Target, container));
        }

        private static CharacterId[] Selected(Rig rig) => rig.Toggles.Where(pair => pair.Value.IsSelected()).Select(pair => pair.Key).ToArray();

        private static void Prefer(Scene scene, params CharacterId[] ids) =>
            scene.App.Storage.SetValue(AppFSMKeys.CharacterSelectionPreference, new List<CharacterId>(ids));

        private static List<CharacterId> Preferences(Scene scene) =>
            scene.App.Storage.TryGetValue(AppFSMKeys.CharacterSelectionPreference, out List<CharacterId> ids) ? ids : null;

        // Awake 0x622270 disables continuing (0x62233E, 0x62234B), subscribes to input type changes (0x622437), reads the
        // challenge flag from the active mission context (a missing one is not a challenge) and whether the skin requirements are met
        // (none are required, so they are). UpdateCharacters then makes only the mission's characters interactable and selects
        // the first of them.
        [Test]
        public void AwakeDisablesContinuingAndSelectsTheFirstAllowedCharacter()
        {
            Run((scene, rig) =>
            {
                AddCharacter(scene, CharacterId.Sonic);
                AddCharacter(scene, CharacterId.Tails);

                Call(rig.Container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not

                Assert.That(Get<App>(rig.Container, "m_app"), Is.SameAs(scene.App));
                Assert.That(rig.ContinueButton.interactable, Is.False);
                Assert.That(rig.ContinueInput.enabled, Is.False);
                Assert.That(rig.Container.IsChallengeMode.Value, Is.False);
                Assert.That(rig.Container.SkinsSelectionActive.Value, Is.True);
                Assert.That(Subscribed(rig.Container), Is.True);
                Assert.That(rig.Toggles[CharacterId.Amy].IsSelected(), Is.False, "the character the mission does not list comes first but is skipped");
                Assert.That(rig.Toggles[CharacterId.Amy].GetComponent<UIToggle>().IsInteractable(), Is.False);
                Assert.That(rig.Toggles[CharacterId.Sonic].GetComponent<UIToggle>().IsInteractable(), Is.True);
                Assert.That(rig.Toggles[CharacterId.Tails].GetComponent<UIToggle>().IsInteractable(), Is.True);
                Assert.That(Selected(rig), Is.EqualTo(new[] { CharacterId.Sonic }));
            }, CharacterId.Amy, CharacterId.Sonic, CharacterId.Tails);
        }

        // GetPreferredCharacter 0x6233E0 takes the first remembered character that the mission lists (0x623562): a
        // remembered character the mission does not list is skipped, and the preferred one is selected instead of the first.
        [Test]
        public void TheRememberedCharacterIsSelectedWhenTheMissionAllowsIt()
        {
            Run((scene, rig) =>
            {
                AddCharacter(scene, CharacterId.Sonic);
                AddCharacter(scene, CharacterId.Tails);
                Prefer(scene, CharacterId.Amy, CharacterId.Tails, CharacterId.Sonic);

                Call(rig.Container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not

                Assert.That(Selected(rig), Is.EqualTo(new[] { CharacterId.Tails }));
            }, CharacterId.Sonic, CharacterId.Tails);
        }

        // With nothing remembered (0x623570 returns None) the first allowed character is selected, and with a
        // remembered list that holds only unlisted characters the answer is None as well.
        [Test]
        public void NoUsableMemoryFallsBackToTheFirstAllowedCharacter()
        {
            Run((scene, rig) =>
            {
                AddCharacter(scene, CharacterId.Sonic);
                AddCharacter(scene, CharacterId.Tails);
                Prefer(scene, CharacterId.Amy);

                Call(rig.Container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not

                Assert.That(Call(rig.Container, "GetPreferredCharacter"), Is.EqualTo(CharacterId.None)); // Reflection: private UIContainerCharacterSelectMain.GetPreferredCharacter
                Assert.That(Selected(rig), Is.EqualTo(new[] { CharacterId.Sonic }));
            }, CharacterId.Sonic, CharacterId.Tails);
        }

        // OnDestroy 0x622BD0 removes the input type subscription made in Awake.
        [Test]
        public void DestroyingTheScreenRemovesItsInputSubscription()
        {
            Run((scene, rig) =>
            {
                Call(rig.Container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                Assert.That(Subscribed(rig.Container), Is.True);

                Call(rig.Container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not

                Assert.That(Subscribed(rig.Container), Is.False);
            });
        }

        // SetPreferredCharacter 0x6231E0: the character is removed from the remembered list (0x6232F5) and inserted first
        // (0x62330D); a list is created when none is stored, and the list is stored again (0x6233C9).
        [Test]
        public void ChoosingACharacterMovesItToTheFrontOfTheRememberedList()
        {
            Run((scene, rig) =>
            {
                Set(rig.Container, "m_app", scene.App); // Reflection: Awake takes it from the registry
                Call(rig.Container, "SetPreferredCharacter", CharacterId.Tails);
                Assert.That(Preferences(scene), Is.EqualTo(new[] { CharacterId.Tails }), "no list stored yet");

                Prefer(scene, CharacterId.Sonic, CharacterId.Tails, CharacterId.Amy);
                Call(rig.Container, "SetPreferredCharacter", CharacterId.Tails); // Reflection: private UIContainerCharacterSelectMain.SetPreferredCharacter
                Assert.That(Preferences(scene), Is.EqualTo(new[] { CharacterId.Tails, CharacterId.Sonic, CharacterId.Amy }), "moved, not duplicated");

                Call(rig.Container, "SetPreferredCharacter", CharacterId.Knuckles); // Reflection: private UIContainerCharacterSelectMain.SetPreferredCharacter
                Assert.That(Preferences(scene), Is.EqualTo(new[] { CharacterId.Knuckles, CharacterId.Tails, CharacterId.Sonic, CharacterId.Amy }));
            });
        }

        // SetCharacter 0x622C70 remembers the choice, stores it as the current and the mission character (0x622D79,
        // 0x622E3A), turns off switching off in the toggle group (0x622E41) and enables continuing (0x622E50, 0x622E60).
        // Action_OnCharacterSelected 0x622C60 is a tail call to it.
        [Test]
        public void ChoosingACharacterStoresItAndEnablesContinuing()
        {
            Run((scene, rig) =>
            {
                Set(rig.Container, "m_app", scene.App); // Reflection: Awake takes it from the registry
                CharacterDefinition tails = AddCharacter(scene, CharacterId.Tails);
                CharacterDefinition sonic = AddCharacter(scene, CharacterId.Sonic);
                rig.ContinueButton.interactable = false;
                rig.ContinueInput.enabled = false;
                Assert.That(rig.Group.AllowSwitchOff, Is.True);

                rig.Container.SetCharacter(tails);

                Assert.That(Preferences(scene), Is.EqualTo(new[] { CharacterId.Tails }));
                Assert.That(scene.App.Storage.GetValueOnly(AppFSMKeys.CurrentCharacterId, CharacterId.None), Is.EqualTo(CharacterId.Tails));
                Assert.That(scene.App.Storage.GetValueOnly(AppFSMKeys.MissionCharacterId, CharacterId.None), Is.EqualTo(CharacterId.Tails));
                Assert.That(rig.Group.AllowSwitchOff, Is.False);
                Assert.That(rig.ContinueButton.interactable, Is.True);
                Assert.That(rig.ContinueInput.enabled, Is.True);

                rig.Container.Action_OnCharacterSelected(sonic);

                Assert.That(Preferences(scene), Is.EqualTo(new[] { CharacterId.Sonic, CharacterId.Tails }));
                Assert.That(scene.App.Storage.GetValueOnly(AppFSMKeys.CurrentCharacterId, CharacterId.None), Is.EqualTo(CharacterId.Sonic));
            });
        }

        // The scene's active mission context, published through the manager's public SetActiveMissionContext (which loads the
        // mission's state); it is cleared again before the scene is released.
        private static void ActivateContext(Scene scene, IMissionContext context)
        {
            scene.Missions.SetActiveMissionContext(context);
            scene.Owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => scene.Missions.SetActiveMissionContext(null)));
        }

        private static MissionDefinition NewMission(Scene scene)
        {
            var mission = scene.Owned.Track(ScriptableObject.CreateInstance<MissionDefinition>());
            Set(mission, "m_guid", "lucid-select-main-mission"); // Reflection: authored GUID of a serialized definition
            Set(mission, "m_subMissions", new List<MissionDefinition>()); // Reflection: authored serialized list
            return mission;
        }

        // Awake 0x622270 tests the active context's class (0x62246F..0x62248F) and asks the skin requirements (0x6224B9).
        // A challenge context is a challenge; a regular one and none at all are not; one unmet requirement turns the skin entry off.
        [Test]
        public void ARegularContextIsNotChallengeMode()
        {
            Run((scene, rig) =>
            {
                scene.Owned.LeaseRow<ChallengeManager>(); // MissionContextRegular's field initialiser caches this row
                var level = scene.Owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
                Set(level, "m_guid", "lucid-select-main-level"); // Reflection: authored GUID of a serialized definition
                ActivateContext(scene, new MissionContextRegular(NewMission(scene), level, false));

                Call(rig.Container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not

                Assert.That(rig.Container.IsChallengeMode.Value, Is.False);
            });
        }

        [Test]
        public void AChallengeContextMakesTheScreenChallengeMode()
        {
            Run((scene, rig) =>
            {
                var definition = scene.Owned.Track(ScriptableObject.CreateInstance<ChallengeDefinition>());
                Set(definition, "m_mission", NewMission(scene)); // Reflection: serialized m_mission the editor assigns
                var challengeLevel = scene.Owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
                Set(challengeLevel, "m_guid", "lucid-select-main-challenge-level"); // Reflection: authored GUID, which LoadMission keys the level's save data by
                Set(definition, "m_level", challengeLevel);
                Set(definition, "m_characterSelection", new CharacterId[0]);
                Set(definition, "m_startPositions", new LevelStartPositionDefinition[0]); // Reflection: serialized m_startPositions the editor assigns
                var state = new ChallengeState(0, definition, 0, null, 0);
                OwnedStringTable.Run(new Dictionary<Hardlight.Enums.Strings, string> { { (Hardlight.Enums.Strings)0, "Lucid challenge" } },
                    () => ActivateContext(scene, new MissionContextChallenge(state, false)));

                Call(rig.Container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not

                Assert.That(rig.Container.IsChallengeMode.Value, Is.True);
            });
        }

        [Test]
        public void AnUnmetSkinRequirementTurnsTheSkinEntryOff()
        {
            Run((scene, rig) =>
            {
                var unmet = scene.Owned.Track(ScriptableObject.CreateInstance<RequirementLevelAlwaysLocked>());
                Set(rig.Container, "m_characterSkinUnlockRequirements", new RequirementGameplayLevelBase[] { unmet }); // Reflection: authored serialized field

                Call(rig.Container, "Awake");

                Assert.That(rig.Container.SkinsSelectionActive.Value, Is.False);
            });
        }

        // SetCharacter 0x622C70 ends with RefreshPowers(characterDefinition.Archetype) on the equip container (0x622E65..0x622E7E).
        // Over a powers-enabled container with one saved power, Climbing lists no powers (disabled) and None allows every power (enabled).
        [Test]
        public void ChoosingACharacterRefreshesTheEquippedPowersForItsArchetype()
        {
            Run((scene, rig) =>
            {
                Owned owned = scene.Owned;
                owned.LeaseRow<ChallengeManager>(); // The store's and the mission context's field initialisers cache this row
                Set(rig.Container, "m_app", scene.App); // Reflection: Awake takes it from the registry
                var store = owned.Register(owned.Host("Lucid select main store", false).AddComponent<DreamPowerStoreManager>());
                Set(store, "m_saveManager", scene.Save); // Reflection: the reference the store's start-up chain takes from the SaveManager row
                Set(store, "m_unlockRequirements", new RequirementGameplayLevelBase[0]); // Reflection: authored serialized field
                Set(scene.Save, "m_saveSettings", new SaveDataSettings()); // Reflection: the settings the SaveManager holds after loading
                ImageSlot[] slots = { NewImageSlot(owned), NewImageSlot(owned) };
                Set(rig.Equip, "m_slots", Array.ConvertAll(slots, slot => slot.Widget)); // Reflection: serialized references the editor assigns
                GameObject overlayHost = owned.Host("Lucid select main equip overlay", false);
                var overlayButton = overlayHost.AddComponent<Button>();
                var overlay = overlayHost.AddComponent<UIWidgetButton>();
                Set(overlay, "m_uiButton", overlayButton); // Reflection: serialized m_uiButton the editor assigns
                Set(rig.Equip, "m_buttonOverlay", overlay); // Reflection: serialized m_buttonOverlay the editor assigns
                Set(rig.Equip, "m_controllerGroup", owned.Host("Lucid select main equip group", false).AddComponent<RectTransform>()); // Reflection: serialized m_controllerGroup the editor assigns
                DreamPowerDefinition power = NewDefinition(owned, "lucid-select-main-power", Hardlight.Enums.Strings.MISSING, NewSprite(owned));
                scene.Data.DreamPowerDefinitions["lucid-select-main-power"] = power;
                scene.Save.CurrentSave.GetOrCreateDreamPowerLoadout(CharacterArchetype.None).Set(0, "lucid-select-main-power");
                var mission = NewMission(scene);
                Set(mission, "m_enableDreamPowers", true); // Reflection: authored mission data
                var level = owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
                Set(level, "m_guid", "lucid-select-main-level"); // Reflection: authored GUID of a serialized definition
                ActivateContext(scene, new MissionContextRegular(mission, level, false));
                Call(rig.Equip, "Start");
                Assert.That(rig.Equip.PowersEnabled.Value, Is.True);
                CharacterDefinition climber = AddCharacter(scene, CharacterId.Tails, CharacterArchetype.Climbing);
                CharacterDefinition unrestricted = AddCharacter(scene, CharacterId.Sonic);

                rig.Container.SetCharacter(climber);
                Assert.That(slots[0].Animation.IsPlaying(slots[0].Disabled.name), Is.True, "climbing lists no powers");

                rig.Container.SetCharacter(unrestricted);
                Assert.That(slots[0].Animation.IsPlaying(slots[0].Enabled.name), Is.True, "the unrestricted archetype allows every power");
                Assert.That(slots[0].Animation.IsPlaying(slots[0].Disabled.name), Is.False);
            });
        }

        // Action_OpenSkinSelection 0x622E90 passes the current character's definition to the skin selection screen as the
        // container parameters (0x62310A) and publishes the open event under OnClick (0x623191).
        [Test]
        public void OpeningSkinSelectionHandsOverTheCurrentCharacterAndPublishesTheEvent()
        {
            Run((scene, rig) =>
            {
                Set(rig.Container, "m_app", scene.App); // Reflection: Awake takes it from the registry
                CharacterDefinition tails = AddCharacter(scene, CharacterId.Tails);
                AddCharacter(scene, CharacterId.Sonic);
                scene.App.Storage.SetValue(AppFSMKeys.CurrentCharacterId, CharacterId.Tails);
                string name = ProcessManager.GetDefaultName<MessageExchangeBoundCallbackArg<UIModernMessage>>();
                var row = scene.Owned.Lease(new VerificationFixtureOwnership.RegistryRow(name));
                var exchange = new MessageExchangeBoundCallbackArg<UIModernMessage>();
                row.Register(exchange);
                var received = new List<(UIModernEventType, UIModernEvent)>();
                var unsubscribe = new List<Action>();
                foreach (UIModernEventType type in Enum.GetValues(typeof(UIModernEventType)))
                {
                    UIModernEventType subscribed = type;
                    var message = new UIModernMessage(type);
                    MessageCallback<UIModernEvent> callback = (in UIModernEvent value) => received.Add((subscribed, value));
                    exchange.SubscribeToMessage(in message, callback);
                    unsubscribe.Add(() => exchange.UnsubscribeFromMessage(in message, callback));
                }
                try
                {
                    rig.Container.Action_OpenSkinSelection();
                }
                finally
                {
                    foreach (Action step in unsubscribe) step();
                }

                var parameters = scene.App.Storage.GetValueOnly<IUIContainerParameters>(AppFSMKeys.UIContainerParameters);
                Assert.That(parameters, Is.InstanceOf<UIContainerCharacterSkinSelection.UIContainerCharacterSkinSelectionParameters>());
                Assert.That(((UIContainerCharacterSkinSelection.UIContainerCharacterSkinSelectionParameters)parameters).CharacterDefinition, Is.SameAs(tails));
                Assert.That(received.Count, Is.EqualTo(1));
                Assert.That(received[0].Item1, Is.EqualTo(UIModernEventType.OnClick));
                Assert.That(received[0].Item2, Is.SameAs(rig.OpenSkins));
            });
        }

        // OnInputTypeChanged 0x623690 ignores the three pointer and key input types (0x623715..0x623733). For a controller type it
        // selects the first mission character (0x623942) only when no toggle is selected (0x623880), and leaves a selection alone.
        [Test]
        public void ControllerInputSelectsTheFirstMissionCharacterOnlyWhenNothingIsSelected()
        {
            Run((scene, rig) =>
            {
                AddCharacter(scene, CharacterId.Sonic);
                AddCharacter(scene, CharacterId.Tails);
                Call(rig.Container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                Assert.That(Selected(rig), Is.EqualTo(new[] { CharacterId.Sonic }));
                rig.Toggles[CharacterId.Sonic].SetSelected(false);

                foreach (InputType pointerOrKey in new[] { InputType.Mouse, InputType.Keyboard, InputType.Touch })
                {
                    Call(rig.Container, "OnInputTypeChanged", pointerOrKey); // Reflection: private UIContainerCharacterSelectMain.OnInputTypeChanged
                    Assert.That(Selected(rig), Is.Empty, pointerOrKey.ToString());
                }

                Call(rig.Container, "OnInputTypeChanged", InputType.DefaultController); // Reflection: private UIContainerCharacterSelectMain.OnInputTypeChanged
                Assert.That(Selected(rig), Is.EqualTo(new[] { CharacterId.Sonic }), "nothing was selected");

                rig.Toggles[CharacterId.Sonic].SetSelected(false);
                rig.Toggles[CharacterId.Tails].SetSelected(true);
                Call(rig.Container, "OnInputTypeChanged", InputType.DefaultController); // Reflection: private UIContainerCharacterSelectMain.OnInputTypeChanged
                Assert.That(Selected(rig), Is.EqualTo(new[] { CharacterId.Tails }), "a selection is kept");
            }, CharacterId.Sonic, CharacterId.Tails);
        }

        // OnInputTypeChanged selects the toggle of m_characterIds[0] (0x623944, key compare 0x623A8A), whatever the toggle order.
        [Test]
        public void ControllerInputSelectsTheToggleOfTheFirstListedCharacterNotTheFirstToggle()
        {
            Run((scene, rig) =>
            {
                AddCharacter(scene, CharacterId.Sonic);
                AddCharacter(scene, CharacterId.Tails);
                Call(rig.Container, "Awake"); // Reflection: Unity sends Awake; Edit Mode does not
                rig.Toggles[CharacterId.Sonic].SetSelected(false);
                // Reflection: the list GetCharactersForActiveMission fills, written here with a different order than the toggles
                Set(rig.Container, "m_characterIds", new List<CharacterId> { CharacterId.Tails, CharacterId.Sonic });

                Call(rig.Container, "OnInputTypeChanged", InputType.DefaultController); // Reflection: private UIContainerCharacterSelectMain.OnInputTypeChanged

                Assert.That(Selected(rig), Is.EqualTo(new[] { CharacterId.Tails }));
            }, CharacterId.Sonic, CharacterId.Tails);
        }

        // IsAnyNonDefaultCharacterSkinSelected 0x6229E0: a character with no current skin or one unlocked by default does not
        // count (0x622B0E, 0x622B1E); the first one wearing another skin ends the scan with true.
        [Test]
        public void OnlyANonDefaultCurrentSkinCounts()
        {
            Run((scene, rig) =>
            {
                CharacterDefinition none = AddCharacter(scene, CharacterId.Amy);
                CharacterDefinition basic = AddCharacter(scene, CharacterId.Sonic);
                CharacterDefinition styled = AddCharacter(scene, CharacterId.Tails);
                Assert.That(Call(rig.Container, "IsAnyNonDefaultCharacterSkinSelected"), Is.EqualTo(false), "no skins at all"); // Reflection: private UIContainerCharacterSelectMain.IsAnyNonDefaultCharacterSkinSelected

                // Wearing a skin saves the choice, and the save request is refused once by the fixture's coroutine host.
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                basic.SetCharacterSkin(NewSkin(scene, "lucid-basic-skin", true));
                Assert.That(Call(rig.Container, "IsAnyNonDefaultCharacterSkinSelected"), Is.EqualTo(false), "only a default skin"); // Reflection: private UIContainerCharacterSelectMain.IsAnyNonDefaultCharacterSkinSelected

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                styled.SetCharacterSkin(NewSkin(scene, "lucid-styled-skin", false));
                Assert.That(Call(rig.Container, "IsAnyNonDefaultCharacterSkinSelected"), Is.EqualTo(true), "one character wears a skin that is not a default"); // Reflection: private UIContainerCharacterSelectMain.IsAnyNonDefaultCharacterSkinSelected
                Assert.That(none.CurrentSkin == null, Is.True);
            });
        }

        private static CharacterSkinDefinition NewSkin(Scene scene, string guid, bool unlockedByDefault)
        {
            var skin = scene.Owned.Track(ScriptableObject.CreateInstance<CharacterSkinDefinition>());
            Set(skin, "m_guid", guid); // Reflection: authored GUID of a serialized definition
            Set(skin, "m_unlockedByDefault", unlockedByDefault); // Reflection: authored serialized field
            return skin;
        }
    }
}
