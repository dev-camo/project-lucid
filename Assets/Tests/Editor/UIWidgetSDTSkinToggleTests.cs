using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.DreamPowerSlotRigs;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetSDTSkinToggle and its parameters (Game.Runtime 020009c7 / 020009c8, x86-64 0x6A1FB0..0x6A2400) as a real
    // component beside a real UIToggle, group element and transition on an owned host, over a real SaveManager that holds
    // an open save. The skin's thumbnail wrapper already holds its sprite, so no Addressables call is made.
    public sealed class UIWidgetSDTSkinToggleTests
    {
        private const string SkinGuid = "lucid-skin-toggle-skin";

        private sealed class Rig
        {
            public UIWidgetSDTSkinToggle Widget;
            public UIToggle Toggle;
            public UIToggleGroupElement Element;
            public UIToggleGroup Group;
            public Transitions Animations;
            public CharacterSkinDefinition Skin;
            public Sprite Thumbnail;
            public SaveDataGame Game;
            public readonly List<CharacterSkinDefinition> Applied = new List<CharacterSkinDefinition>();

            public UIWidgetSDTSkinToggle.UIWidgetSDTSkinToggleParameters Parameters() =>
                new UIWidgetSDTSkinToggle.UIWidgetSDTSkinToggleParameters(Skin, Applied.Add, Group);

            public string PlayingState()
            {
                foreach (string state in new[] { "state Normal", "state Selected", "state Disabled" })
                    if (Animations.Animation.IsPlaying(Animations.Clips[state].name)) return state;
                return null;
            }
        }

        private static void Run(Action<Owned, Rig> body)
        {
            using (var characters = new OwnedCharacterManagers())
            {
                Owned.Run(owned =>
                {
                    var rig = new Rig { Game = ProcessManager.GetSystem<SaveManager>().CurrentSave };
                    GameObject host = owned.Host("Lucid skin toggle", false);
                    rig.Toggle = host.AddComponent<UIToggle>();
                    rig.Element = host.AddComponent<UIToggleGroupElement>();
                    Set(rig.Toggle, "m_groupElement", rig.Element); // Reflection: serialized reference the editor assigns
                    Set(rig.Element, "m_toggle", rig.Toggle); // Reflection: serialized reference the editor assigns
                    rig.Widget = host.AddComponent<UIWidgetSDTSkinToggle>();
                    Call(rig.Widget, "Reset"); // The editor message that assigns the sibling toggle
                    rig.Animations = AddTransitions(owned, host, rig.Toggle,
                        new[] { UITransitionState.Normal, UITransitionState.Selected, UITransitionState.Disabled }, new string[0]);
                    Set(rig.Widget, "m_uiTransitionAnimation", rig.Animations.Transition); // Reflection: serialized reference the editor assigns
                    rig.Group = owned.Host("Lucid skin toggle group", true).AddComponent<UIToggleGroup>();
                    rig.Group.AllowSwitchOff = true;
                    rig.Skin = owned.Track(ScriptableObject.CreateInstance<CharacterSkinDefinition>());
                    Set(rig.Skin, "m_guid", SkinGuid); // Reflection: authored GUID of a serialized definition
                    rig.Thumbnail = NewSprite(owned);
                    Set(rig.Skin.Thumbnail, "m_loadedAsset", rig.Thumbnail); // Reflection: the sprite an earlier Addressables load would hold
                    body(owned, rig);
                });
            }
        }

        private static void SetSaved(Rig rig, bool unlocked, bool seen, bool unlockedByDefault)
        {
            SaveDataCharacterSkin data = rig.Game.GetOrCreateSkinData(SkinGuid);
            data.Unlocked = unlocked;
            data.UnlockSeen = seen;
            Set(rig.Skin, "m_unlockedByDefault", unlockedByDefault); // Reflection: authored serialized field
        }

        // Setup 0x6A1FB0 assigns the group to the toggle's group element (0x6A2014), loads the skin's thumbnail
        // (0x6A2033), and refreshes the binders (0x6A2055). It does not run the base toggle setup, so no label is taken.
        [Test]
        public void SetupJoinsTheGroupShowsTheThumbnailAndRetainsIt()
        {
            Run((owned, rig) =>
            {
                rig.Widget.Label.Value = "kept";

                rig.Widget.Setup(rig.Parameters());

                Assert.That(rig.Element.Group, Is.SameAs(rig.Group));
                Assert.That(rig.Widget.Thumbnail.Value, Is.SameAs(rig.Thumbnail));
                Assert.That(Get<int>(rig.Skin.Thumbnail, "m_refCount"), Is.EqualTo(1));
                Assert.That(rig.Widget.Label.Value, Is.EqualTo("kept"));
            });
        }

        // UpdateBinders 0x6A2060 (0x6A20D7..0x6A2121): locked = not unlocked and not unlocked by default; new = unlocked, not yet
        // seen and not unlocked by default. The eight rows separate each flag's contribution.
        [Test]
        public void LockedAndNewFollowTheSavedFlagsAndTheDefaultFlag()
        {
            Run((owned, rig) =>
            {
                rig.Widget.Setup(rig.Parameters());
                // (unlocked, seen, unlocked by default) -> (locked, new)
                var table = new (bool unlocked, bool seen, bool byDefault, bool locked, bool isNew)[]
                {
                    (false, false, false, true, false),
                    (false, false, true, false, false),
                    (false, true, false, true, false),
                    (false, true, true, false, false),
                    (true, false, false, false, true),
                    (true, false, true, false, false),
                    (true, true, false, false, false),
                    (true, true, true, false, false),
                };
                foreach (var row in table)
                {
                    SetSaved(rig, row.unlocked, row.seen, row.byDefault);
                    Call(rig.Widget, "UpdateBinders");
                    string label = row.unlocked + "/" + row.seen + "/" + row.byDefault;
                    Assert.That(rig.Widget.IsLocked.Value, Is.EqualTo(row.locked), "locked " + label);
                    Assert.That(rig.Widget.IsNew.Value, Is.EqualTo(row.isNew), "new " + label);
                }
            });
        }

        // 0x6A2160..0x6A217D: the transition goes to Selected (3) when the toggle is among the group's active toggles,
        // else to Disabled (4) for a locked skin and Normal (0) otherwise.
        [Test]
        public void TheTransitionShowsSelectedThenDisabledThenNormal()
        {
            Run((owned, rig) =>
            {
                rig.Widget.Setup(rig.Parameters());

                SetSaved(rig, false, false, false);
                Call(rig.Widget, "UpdateBinders");
                Assert.That(rig.PlayingState(), Is.EqualTo("state Disabled"), "locked and not selected");

                SetSaved(rig, true, true, false);
                Call(rig.Widget, "UpdateBinders");
                Assert.That(rig.PlayingState(), Is.EqualTo("state Normal"), "unlocked and not selected");

                rig.Toggle.IsOn = true;
                SetSaved(rig, false, false, false);
                Call(rig.Widget, "UpdateBinders");
                Assert.That(rig.PlayingState(), Is.EqualTo("state Selected"), "selected wins over locked");
            });
        }

        // 0x6A2190 / 0x6A2210 / 0x6A21D0: each of the three entry points hands the skin to the callback (0x6A21B2) and only then
        // refreshes the binders (tail call at 0x6A21BE): a callback that unlocks the skin is already seen by the refresh.
        [Test]
        public void EverySelectionEntryPointAppliesTheSkinAndRefreshesTheBinders()
        {
            Run((owned, rig) =>
            {
                var applied = new List<CharacterSkinDefinition>();
                var parameters = new UIWidgetSDTSkinToggle.UIWidgetSDTSkinToggleParameters(rig.Skin,
                    skin => { applied.Add(skin); rig.Game.GetOrCreateSkinData(SkinGuid).Unlocked = true; }, rig.Group);
                rig.Widget.Setup(parameters);
                Action[] entryPoints =
                {
                    () => rig.Widget.Action_OnSkinSelected(),
                    () => rig.Widget.Action_OnSkinSelected(false), // the flag is ignored
                    () => Call(rig.Widget, "SetCharacterSkin"),
                };

                for (int i = 0; i < entryPoints.Length; i++)
                {
                    SetSaved(rig, false, false, false);
                    Call(rig.Widget, "UpdateBinders");
                    Assert.That(rig.Widget.IsLocked.Value, Is.True, "locked before entry point " + i);

                    entryPoints[i]();

                    Assert.That(applied.Count, Is.EqualTo(i + 1));
                    Assert.That(applied[i], Is.SameAs(rig.Skin));
                    Assert.That(rig.Widget.IsLocked.Value, Is.False, "binders refreshed after the callback, entry point " + i);
                    Assert.That(rig.Widget.IsNew.Value, Is.True, "entry point " + i);
                }
            });
        }

        // The entry points test the parameters first (0x6A2199): a widget that was never set up does nothing.
        [Test]
        public void AWidgetThatWasNeverSetUpIgnoresSelectionAndDestroy()
        {
            Run((owned, rig) =>
            {
                Assert.DoesNotThrow(() => rig.Widget.Action_OnSkinSelected());
                Assert.DoesNotThrow(() => rig.Widget.Action_OnSkinSelected(true));
                Assert.DoesNotThrow(() => Call(rig.Widget, "SetCharacterSkin"));
                Assert.DoesNotThrow(() => Call(rig.Widget, "UpdateBinders"));
                Assert.DoesNotThrow(() => Call(rig.Widget, "OnDestroy"));
                Assert.That(rig.Applied, Is.Empty);
            });
        }

        // OnDestroy 0x6A2250 releases the thumbnail the setup retained.
        [Test]
        public void DestroyReleasesTheThumbnail()
        {
            Run((owned, rig) =>
            {
                rig.Widget.Setup(rig.Parameters());

                Call(rig.Widget, "OnDestroy");

                Assert.That(Get<int>(rig.Skin.Thumbnail, "m_refCount"), Is.Zero);
                Assert.That(Get<Sprite>(rig.Skin.Thumbnail, "m_loadedAsset"), Is.Null);
            });
        }

        // The parameters constructor 0x6A2400 stores its three arguments in order.
        [Test]
        public void TheParametersKeepTheirArguments()
        {
            Run((owned, rig) =>
            {
                Action<CharacterSkinDefinition> callback = rig.Applied.Add;

                var parameters = new UIWidgetSDTSkinToggle.UIWidgetSDTSkinToggleParameters(rig.Skin, callback, rig.Group);

                Assert.That(parameters.CharacterSkinDefinition, Is.SameAs(rig.Skin));
                Assert.That(parameters.SetCharacterSkin, Is.SameAs(callback));
                Assert.That(parameters.ToggleGroup, Is.SameAs(rig.Group));
            });
        }
    }
}
