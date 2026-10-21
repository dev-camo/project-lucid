using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.CharacterSelectScene;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.DreamPowerSlotRigs;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerCharacterSkinSelection and its parameters (Game.Runtime 02000890 / 02000891, x86-64 0x624000..0x6246A0) as a
    // real component on an owned inactive host over the character select scene. Wearing a skin saves the choice, and the save
    // request is refused once by the fixture's coroutine host with an engine error. The character definition is assigned
    // directly where Setup would take it. Setup's selection of the current skin's toggle (0x62429C..0x6242B6) needs the live
    // UINavigationManager that the scene cannot build in Edit Mode, so Setup runs here for a character whose current skin is
    // not one of its listed skins; the zone colours (a selected level) and the removal of toggles left from an earlier
    // setup (Object.Destroy is refused in Edit Mode) are not covered.
    public sealed class UIContainerCharacterSkinSelectionTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        private sealed class Rig
        {
            public UIContainerCharacterSkinSelection Container;
            public CharacterDefinition Character;
            public CharacterSkinDefinition SkinA, SkinB;
            public Sprite ThumbnailA, ThumbnailB;
            public CountingEvent Selected;
            public Animation Animation;
            public AnimationClip Clip;
        }

        private static CharacterSkinDefinition NewSkin(Scene scene, string guid, Strings name, bool unlockedByDefault, out Sprite thumbnail)
        {
            var skin = scene.Owned.Track(ScriptableObject.CreateInstance<CharacterSkinDefinition>());
            Set(skin, "m_guid", guid); // Reflection: authored GUID of a serialized definition
            Set(skin, "m_name", name); // Reflection: authored serialized field
            Set(skin, "m_unlockedByDefault", unlockedByDefault); // Reflection: authored serialized field
            thumbnail = NewSprite(scene.Owned);
            Set(skin.Thumbnail, "m_loadedAsset", thumbnail); // Reflection: the sprite an earlier Addressables load would hold
            return skin;
        }

        private static void Run(bool skinAByDefault, bool skinBByDefault, Action<Scene, Rig> body) => CharacterSelectScene.Run(scene =>
        {
            var rig = new Rig();
            rig.Container = scene.Owned.Host("Lucid skin selection", false).AddComponent<UIContainerCharacterSkinSelection>();
            rig.Character = AddCharacter(scene, CharacterId.Tails);
            rig.SkinA = NewSkin(scene, "lucid-skin-a", Strings.MISSING, skinAByDefault, out rig.ThumbnailA);
            rig.SkinB = NewSkin(scene, "lucid-skin-b", Strings.NONE, skinBByDefault, out rig.ThumbnailB);
            Set(rig.Character, "m_characterSkins", new[] { rig.SkinA, rig.SkinB }); // Reflection: authored serialized field
            Set(rig.Container, "m_characterDefinition", rig.Character); // Reflection: Setup assigns it from its parameters
            rig.Selected = Author(rig.Container, "m_onSelectCharacter"); // Reflection: the authored event, which the constructor leaves null
            GameObject animationHost = scene.Owned.Host("Lucid skin selection animation", false);
            rig.Animation = animationHost.AddComponent<Animation>();
            rig.Animation.playAutomatically = false;
            rig.Clip = AddClip(scene.Owned, rig.Animation, "skin selected");
            rig.Animation.clip = rig.Clip;
            animationHost.SetActive(true);
            Set(rig.Container, "m_skinSelectedAnimation", rig.Animation); // Reflection: serialized reference the editor assigns
            body(scene, rig);
        });

        private static void ExpectSaveRequests(int count)
        {
            for (int i = 0; i < count; i++) LogAssert.Expect(LogType.Error, RefusedCoroutine);
        }

        // AttemptSetCharacterSkin 0x624560: a skin the save has unlocked is not locked (0x6245CB) and is worn by the character
        // (0x624611); a changed skin invokes the selection event (0x624625) and plays the selection animation (0x624634).
        // The binders then show its thumbnail and name (UpdateCharacterBinders 0x624370).
        [Test]
        public void AnUnlockedSkinIsWornAndShown()
        {
            Run(false, false, (scene, rig) =>
            {
                scene.Save.CurrentSave.GetOrCreateSkinData("lucid-skin-a").Unlocked = true;
                ExpectSaveRequests(1);

                rig.Container.SetCharacterSkin(rig.SkinA);

                Assert.That(rig.Container.IsLocked.Value, Is.False);
                Assert.That(rig.Character.CurrentSkin, Is.SameAs(rig.SkinA));
                Assert.That(rig.Selected.Count, Is.EqualTo(1));
                Assert.That(rig.Animation.IsPlaying(rig.Clip.name), Is.True);
                Assert.That(rig.Container.Render.Value, Is.SameAs(rig.ThumbnailA));
                Assert.That(rig.Container.SkinName.Value, Is.EqualTo(Strings.MISSING));
            });
        }

        // A skin that is neither saved as unlocked nor unlocked by default is shown locked and not worn (0x6245D3..0x6245FF returns):
        // no event, no animation, and the character keeps its skin. The binders still show it.
        [Test]
        public void ALockedSkinIsShownButNotWorn()
        {
            Run(false, false, (scene, rig) =>
            {
                rig.Container.SetCharacterSkin(rig.SkinB);

                Assert.That(rig.Container.IsLocked.Value, Is.True);
                Assert.That(rig.Character.CurrentSkin == null, Is.True);
                Assert.That(rig.Selected.Count, Is.Zero);
                Assert.That(rig.Animation.IsPlaying(rig.Clip.name), Is.False);
                Assert.That(rig.Container.Render.Value, Is.SameAs(rig.ThumbnailB));
                Assert.That(rig.Container.SkinName.Value, Is.EqualTo(Strings.NONE));
            });
        }

        // A skin unlocked by default is not locked and is worn even though the save has not unlocked it (0x6245D7 reads skin +0x60).
        [Test]
        public void ASkinUnlockedByDefaultIsWornWithoutASavedUnlock()
        {
            Run(true, false, (scene, rig) =>
            {
                ExpectSaveRequests(1);

                rig.Container.SetCharacterSkin(rig.SkinA);

                Assert.That(rig.Container.IsLocked.Value, Is.False);
                Assert.That(rig.Character.CurrentSkin, Is.SameAs(rig.SkinA));
                Assert.That(rig.Selected.Count, Is.EqualTo(1));
            });
        }

        // SetCharacterSkin 0x6243F0 returns at once for the skin already shown (0x62440C); a different skin first releases the
        // previous thumbnail (0x624464), and the selection event fires only for a change the character accepted.
        [Test]
        public void ChangingSkinReleasesThePreviousThumbnailAndRepeatingDoesNothing()
        {
            Run(true, true, (scene, rig) =>
            {
                ExpectSaveRequests(1);
                rig.Container.SetCharacterSkin(rig.SkinA);
                Assert.That(Get<int>(rig.SkinA.Thumbnail, "m_refCount"), Is.EqualTo(1));

                rig.Container.SetCharacterSkin(rig.SkinA);
                Assert.That(Get<int>(rig.SkinA.Thumbnail, "m_refCount"), Is.EqualTo(1), "the same skin is not loaded again");
                Assert.That(rig.Selected.Count, Is.EqualTo(1));

                ExpectSaveRequests(1);
                rig.Container.SetCharacterSkin(rig.SkinB);
                Assert.That(Get<int>(rig.SkinA.Thumbnail, "m_refCount"), Is.Zero);
                Assert.That(Get<int>(rig.SkinB.Thumbnail, "m_refCount"), Is.EqualTo(1));
                Assert.That(rig.Container.Render.Value, Is.SameAs(rig.ThumbnailB));
                Assert.That(rig.Character.CurrentSkin, Is.SameAs(rig.SkinB));
                Assert.That(rig.Selected.Count, Is.EqualTo(2));
            });
        }

        // UnloadRender 0x624500 and OnDestroy 0x624640 release the shown skin's thumbnail and do nothing without one.
        [Test]
        public void UnloadAndDestroyReleaseTheShownThumbnail()
        {
            Run(true, true, (scene, rig) =>
            {
                Assert.DoesNotThrow(() => Call(rig.Container, "UnloadRender"));
                Assert.DoesNotThrow(() => Call(rig.Container, "OnDestroy"));
                ExpectSaveRequests(1);
                rig.Container.SetCharacterSkin(rig.SkinA);
                Assert.That(Get<int>(rig.SkinA.Thumbnail, "m_refCount"), Is.EqualTo(1));

                Call(rig.Container, "UnloadRender");
                Assert.That(Get<int>(rig.SkinA.Thumbnail, "m_refCount"), Is.Zero);

                Call(rig.Container, "OnDestroy");
                Assert.That(Get<int>(rig.SkinA.Thumbnail, "m_refCount"), Is.Zero, "released at zero stays at zero");
            });
        }

        // Setup 0x624000 takes the character from its parameters (0x6240A0), makes one toggle from the prefab under the toggle
        // parent for each listed skin (0x6241F3), set up with that skin, the container's SetCharacterSkin and the toggle group
        // (0x62424E), and shows the displayed skin's thumbnail and name (0x624322..0x62436A). A skin that is not the character's
        // current one is not selected.
        [Test]
        public void SetupMakesOneSkinToggleForEachListedSkin()
        {
            Run(true, true, (scene, rig) =>
            {
                Owned owned = scene.Owned;
                // Edit Mode does not run Awake, so the toggle prefab has no transition state; queuing one finds none and does nothing.
                GameObject prefabHost = owned.Host("Lucid skin toggle prefab", false);
                var prefabToggle = prefabHost.AddComponent<UIToggle>();
                var element = prefabHost.AddComponent<UIToggleGroupElement>();
                Set(prefabToggle, "m_groupElement", element); // Reflection: serialized reference the editor assigns
                Set(element, "m_toggle", prefabToggle);
                var prefab = prefabHost.AddComponent<UIWidgetSDTSkinToggle>();
                Call(prefab, "Reset");
                var animation = prefabHost.AddComponent<Animation>();
                var transition = prefabHost.AddComponent<UITransitionAnimation>();
                Set(transition, "m_animation", animation);
                Set(transition, "m_uiInteractable", prefabToggle);
                Set(transition, "m_settings", new List<UITransitionAnimationSetting>());
                Set(prefab, "m_uiTransitionAnimation", transition);
                Transform parent = owned.Host("Lucid skin toggle parent", true).transform;
                UIToggleGroup group = owned.Host("Lucid skin toggle group", true).AddComponent<UIToggleGroup>();
                Set(rig.Container, "m_toggleParent", parent); // Reflection: serialized references the editor assigns
                Set(rig.Container, "m_skinTogglePrefab", prefab);
                Set(rig.Container, "m_skinToggleGroup", group);
                // The displayed skin is shown last by Setup, so one is needed first (the character wears none of its listed skins).
                ExpectSaveRequests(1);
                rig.Container.SetCharacterSkin(rig.SkinB);
                var start = new Color(0.1f, 0.2f, 0.3f);
                rig.Container.ZoneBackgroundStartColour.Value = start;
                var other = owned.Track(ScriptableObject.CreateInstance<CharacterDefinition>());
                Set(other, "m_characterSkins", new[] { rig.SkinA, rig.SkinB }); // Reflection: authored serialized field

                rig.Container.Setup(new UIContainerCharacterSkinSelection.UIContainerCharacterSkinSelectionParameters(other));

                Assert.That(Get<CharacterDefinition>(rig.Container, "m_characterDefinition"), Is.SameAs(other));
                Assert.That(parent.childCount, Is.EqualTo(2));
                var skins = new List<CharacterSkinDefinition>();
                foreach (Transform child in parent)
                {
                    var toggle = child.GetComponent<UIWidgetSDTSkinToggle>();
                    var parameters = Get<UIWidgetSDTSkinToggle.UIWidgetSDTSkinToggleParameters>(toggle, "m_parameters");
                    skins.Add(parameters.CharacterSkinDefinition);
                    Assert.That(parameters.ToggleGroup, Is.SameAs(group));
                    Assert.That(parameters.SetCharacterSkin.Target, Is.SameAs(rig.Container));
                    Assert.That(toggle.GetComponent<UIToggle>().IsOn, Is.False, "the character's current skin is not one of these");
                    Assert.That(toggle.Thumbnail.Value, Is.Not.Null);
                }
                Assert.That(skins, Is.EqualTo(new[] { rig.SkinA, rig.SkinB }));
                Assert.That(rig.Container.Render.Value, Is.SameAs(rig.ThumbnailB));
                Assert.That(rig.Container.SkinName.Value, Is.EqualTo(Strings.NONE));
                Assert.That(rig.Container.ZoneBackgroundStartColour.Value, Is.EqualTo(start), "no selected level leaves the colours alone");
            });
        }

        // The parameters constructor 0x6231B0 stores the character definition.
        [Test]
        public void TheParametersKeepTheirCharacterDefinition()
        {
            Run(true, true, (scene, rig) =>
            {
                var parameters = new UIContainerCharacterSkinSelection.UIContainerCharacterSkinSelectionParameters(rig.Character);

                Assert.That(parameters.CharacterDefinition, Is.SameAs(rig.Character));
            });
        }
    }
}
