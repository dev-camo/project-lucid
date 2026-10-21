using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.OrnamentGalleryFixtures;
using static ProjectLucid.Tests.UiWidgetImageFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The ornament gallery's widgets (Game.Runtime 0x020009b7 .. 0x020009c1, x86-64 0x69E220..0x69FA10): the music seeker, the ornament
    // toggle, entry row and row composer, and the character select toggle. Each is a real component on an inactive owned host; Start,
    // OnDestroy and the compiler-generated lambda are called directly because Edit Mode sends no engine messages. Serialized
    // references are the authored wiring. Edit Mode refuses Object.Destroy with an error, so a destroy is checked by that logged
    // request. Not covered: the navigation manager selection (SelectFirst and the composer's first row need a live
    // UINavigationManager), and the CoroutineUtils next-frame scheduling, which is replaced by a refusing host.
    public sealed class OrnamentGalleryWidgetTests
    {
        private static readonly Regex RefusedDestroy = new Regex("^Destroy may not be called from edit mode");
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");
        private const string NoClipAtSfx = "Will not play the given audio clip at source Sfx, no clip was specified.:0";
        private const string ValidKey = "0123456789abcdef0123456789abcdef";

        private static UIWidgetOrnamentToggleParameters ToggleParameters(OrnamentDefinition definition, OrnamentManager.OrnamentStatus status,
            Action<OrnamentDefinition, bool, bool> select = null, Action<RectTransform> hover = null, UIToggleGroup group = null,
            bool defaultSelected = false) =>
            new UIWidgetOrnamentToggleParameters(definition, status, select ?? ((d, a, n) => { }), hover, group, defaultSelected);

        // dump.cs TypeDefIndex 2486..2496 and the DummyDll rows.
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetMusicSeeker), 0x00100101,
                new (string, int)[] { ("m_slider", 0x0001), ("m_onValueChanged", 0x0001), ("m_onInputDown", 0x0001), ("m_onInputUp", 0x0001) },
                new (string, int)[] { ("Action_OnValueChanged", 0x0086), ("OnPointerDown", 0x01e6), ("OnPointerUp", 0x01e6), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIWidgetMusicSeeker), 1, 2);
            Assert.That(typeof(UIWidgetMusicSeeker).BaseType, Is.EqualTo(typeof(UIWidget)));
            Assert.That(typeof(UIWidgetMusicSeeker).GetInterfaces(), Is.EquivalentTo(new[] { typeof(IPointerDownHandler), typeof(IPointerUpHandler), typeof(IEventSystemHandler) }));

            AssertMembers(typeof(UIWidgetOrnamentEntryRowComposerParameters), 0x00100101,
                new (string, int)[] { ("RowData", 0x0026) }, new (string, int)[] { (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIWidgetOrnamentEntryRowComposerParameters), 1, 2);
            Assert.That(typeof(UIWidgetOrnamentEntryRowComposerParameters).GetInterfaces(), Is.EqualTo(new[] { typeof(IUIWidgetParameters) }));

            AssertMembers(typeof(UIWidgetOrnamentEntryRowComposer), 0x00100101,
                new (string, int)[] { ("m_rowPrefab", 0x0001) },
                new (string, int)[] { ("Setup", 0x00c6), ("Clear", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIWidgetOrnamentEntryRowComposer), 1, 2);
            Assert.That(typeof(UIWidgetOrnamentEntryRowComposer).BaseType, Is.EqualTo(typeof(UIWidget)));

            AssertMembers(typeof(UIWidgetOrnamentEntryRow), 0x00100001,
                new (string, int)[] { ("m_thumbnailPrefab", 0x0001), ("m_uiNavigationManagerRef", 0x0021), ("m_entriesInThisRow", 0x0021) },
                new (string, int)[] { ("Setup", 0x00c6), ("SelectFirst", 0x0086), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIWidgetOrnamentEntryRow), 2, 1);
            Assert.That(typeof(UIWidgetOrnamentEntryRow).BaseType, Is.EqualTo(typeof(UIWidget)));

            AssertMembers(typeof(UIWidgetOrnamentToggle), 0x00100001,
                new (string, int)[]
                {
                    ("m_backgroundImage", 0x0001), ("m_backgroundSpriteLocked", 0x0001), ("m_backgroundSpriteUnlocked", 0x0001),
                    ("m_ornamentImage", 0x0001), ("m_ornamentImageLockedColour", 0x0001), ("m_borderImage", 0x0001),
                    ("m_borderSpriteLocked", 0x0001), ("m_borderSpriteUnlocked", 0x0001), ("m_selectedOutline", 0x0001),
                    ("m_groupElement", 0x0001), ("m_newUnlockIcon", 0x0001), ("m_sfxComponent", 0x0001), ("m_onHover", 0x0001),
                    ("m_isNew", 0x0001), ("m_definition", 0x0001),
                },
                new (string, int)[] { ("Setup", 0x00c6), ("OnToggleValueChanged", 0x0081), ("Action_OnSelected", 0x0086), ("OnDestroy", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIWidgetOrnamentToggle), 2, 1);
            Assert.That(typeof(UIWidgetOrnamentToggle).BaseType, Is.EqualTo(typeof(UIWidgetToggle)));
            Assert.That(typeof(UIWidgetOrnamentToggle).GetMethod("OnToggleValueChanged", Declared).GetParameters()[2].DefaultValue, Is.EqualTo(false));
            // The closure of the Setup lambda (member ordinal 15) is generated under the original name.
            Assert.That(typeof(UIWidgetOrnamentToggle).GetNestedType("<>c__DisplayClass15_0", System.Reflection.BindingFlags.NonPublic), Is.Not.Null);

            AssertMembers(typeof(UIWidgetSDTCharacterSelect), 0x00100001,
                new (string, int)[]
                {
                    ("m_characterDefinition", 0x0001), ("m_onCharacterSelected", 0x0001), ("m_highlight", 0x0001), ("m_highlightContainer", 0x0001),
                    ("CharacterRender", 0x0026), ("CompanionRender", 0x0026), ("CharacterName", 0x0026),
                },
                new (string, int)[]
                {
                    ("Start", 0x0081), ("OnDestroy", 0x0081), ("Action_OnCharacterSelected", 0x0086), (".ctor", 0x1886),
                    ("<Action_OnCharacterSelected>b__9_0", 0x0081),
                });
            AssertIl2CppOptions(typeof(UIWidgetSDTCharacterSelect), 2, 1);
            Assert.That(typeof(UIWidgetSDTCharacterSelect).BaseType, Is.EqualTo(typeof(UIWidgetToggle)));
        }

        // ---- UIWidgetOrnamentEntryRowComposerParameters (0x69EE30)

        // The constructor stores the list itself (no copy).
        [Test]
        public void TheComposerParametersKeepTheRowDataList()
        {
            var rows = new List<UIWidgetOrnamentEntryRowParameters>();
            Assert.That(new UIWidgetOrnamentEntryRowComposerParameters(rows).RowData, Is.SameAs(rows));
        }

        // ---- UIWidgetMusicSeeker (0x69E220..0x69E340)

        private sealed class SeekerRig
        {
            public UIWidgetMusicSeeker Seeker;
            public readonly List<float> Changed = new List<float>(), Down = new List<float>(), Up = new List<float>();
        }

        private static void WithSeeker(Action<SeekerRig> body) => Owned.Run(owned =>
        {
            var rig = new SeekerRig { Seeker = owned.Host("Lucid music seeker", false).AddComponent<UIWidgetMusicSeeker>() };
            var slider = owned.Host("Lucid music slider", false).AddComponent<Slider>();
            slider.SetValueWithoutNotify(0.25f);
            Set(rig.Seeker, "m_slider", slider);
            var changed = new UnityEvent<float>();
            changed.AddListener(rig.Changed.Add);
            var down = new UnityEvent<float>();
            down.AddListener(rig.Down.Add);
            var up = new UnityEvent<float>();
            up.AddListener(rig.Up.Add);
            Set(rig.Seeker, "m_onValueChanged", changed);
            Set(rig.Seeker, "m_onInputDown", down);
            Set(rig.Seeker, "m_onInputUp", up);
            body(rig);
        });

        // Action_OnValueChanged 0x69E220 invokes the value-changed event with the value it was given.
        [Test]
        public void TheSeekerForwardsAChangedValueToItsEventOnly()
        {
            WithSeeker(rig =>
            {
                rig.Seeker.Action_OnValueChanged(0.75f);
                Assert.That(rig.Changed, Is.EqualTo(new[] { 0.75f }));
                Assert.That(rig.Down, Is.Empty);
                Assert.That(rig.Up, Is.Empty);
            });
        }

        // OnPointerDown 0x69E280 and OnPointerUp 0x69E2E0 report the slider's current value (0.25), not an argument, each to its own event.
        [Test]
        public void ThePointerEventsReportTheSliderValueToTheirOwnEvents()
        {
            WithSeeker(rig =>
            {
                rig.Seeker.OnPointerDown(null);
                Assert.That(rig.Down, Is.EqualTo(new[] { 0.25f }));
                Assert.That(rig.Up, Is.Empty);

                rig.Seeker.OnPointerUp(null);
                Assert.That(rig.Up, Is.EqualTo(new[] { 0.25f }));
                Assert.That(rig.Down, Is.EqualTo(new[] { 0.25f }));
                Assert.That(rig.Changed, Is.Empty);
            });
        }

        // 0x69E253, 0x69E2AA, 0x69E30A: an absent event is skipped before anything else; the slider is not even read (it is also absent here).
        [Test]
        public void AbsentEventsAreSkippedWithoutReadingTheSlider()
        {
            WithSeeker(rig =>
            {
                Set(rig.Seeker, "m_slider", null);
                Set(rig.Seeker, "m_onValueChanged", null);
                Set(rig.Seeker, "m_onInputDown", null);
                Set(rig.Seeker, "m_onInputUp", null);
                Assert.DoesNotThrow(() => rig.Seeker.Action_OnValueChanged(1f));
                Assert.DoesNotThrow(() => rig.Seeker.OnPointerDown(null));
                Assert.DoesNotThrow(() => rig.Seeker.OnPointerUp(null));
            });
        }

        // ---- UIWidgetOrnamentToggle (0x69EE60..0x69F360)

        // Setup 0x69EE60: an available ornament shows its unlocked art in white; one that is not available shows the locked art in the
        // authored locked colour. The label is the parameters' (the localised name), and the toggle takes the definition's thumbnail.
        [TestCase(true, true, TestName = "AnAvailableOrnamentShowsItsUnlockedArt")]
        [TestCase(false, false, TestName = "AnUnavailableOrnamentShowsItsLockedArt")]
        [TestCase(true, false, TestName = "AnUnlockedButUncollectedOrnamentStillShowsTheLockedArt")]
        public void SetupShowsTheArtOfTheOrnamentsAvailability(bool unlocked, bool collected)
        {
            bool available = unlocked && collected;
            OwnedStringTable.Run(new Dictionary<Strings, string> { { (Strings)9101, "Master Emerald" } }, () => Owned.Run(owned =>
            {
                OrnamentToggleRig rig = NewToggleRig(owned, null);
                OrnamentDefinition definition = NewDefinition(owned, "lucid-ornament-a", OrnamentCategory.Story, Identifier(0), out Texture2D texture, (Strings)9101);

                rig.Toggle.Setup(ToggleParameters(definition, Status(unlocked, true, collected)));

                Assert.That(rig.Background.sprite, Is.SameAs(available ? rig.BackgroundUnlocked : rig.BackgroundLocked));
                Assert.That(rig.Border.sprite, Is.SameAs(available ? rig.BorderUnlocked : rig.BorderLocked));
                Assert.That(rig.Ornament.color, Is.EqualTo(available ? Color.white : LockedColour));
                Assert.That(rig.Ornament.texture, Is.SameAs(texture));
                Assert.That(rig.Toggle.Label.Value, Is.EqualTo("Master Emerald"));
                Assert.That(rig.Outline.enabled, Is.False);
                Assert.That(Count(definition.ThumbnailAsset), Is.EqualTo(1), "the thumbnail is loaded once");
            }));
        }

        // 0x69F06B..0x69F076: the new-unlock marker needs both an available ornament and one not yet seen. The rows separate that from a
        // marker that ignores availability or the seen flag.
        [TestCase(true, false, true, true, TestName = "AnAvailableUnseenOrnamentShowsTheNewMarker")]
        [TestCase(true, true, true, false, TestName = "ASeenOrnamentHidesTheNewMarker")]
        [TestCase(false, false, false, false, TestName = "AnUnavailableUnseenOrnamentHidesTheNewMarker")]
        [TestCase(true, false, false, false, TestName = "AnUncollectedUnseenOrnamentHidesTheNewMarker")]
        public void TheNewMarkerNeedsAnAvailableUnseenOrnament(bool unlocked, bool seen, bool collected, bool marker)
        {
            Owned.Run(owned =>
            {
                OrnamentToggleRig rig = NewToggleRig(owned, null);
                OrnamentDefinition definition = NewDefinition(owned, "lucid-ornament-a", OrnamentCategory.Story, Identifier(0), out _);

                rig.Toggle.Setup(ToggleParameters(definition, Status(unlocked, seen, collected)));

                Assert.That(rig.NewIcon.activeSelf, Is.EqualTo(marker));
                Assert.That(Get<bool>(rig.Toggle, "m_isNew"), Is.EqualTo(marker));
            });
        }

        // The toggle's group element takes the parameters' group (0x69F0F5).
        [Test]
        public void SetupAssignsTheToggleGroup()
        {
            Owned.Run(owned =>
            {
                OrnamentToggleRig rig = NewToggleRig(owned, null);
                OrnamentDefinition definition = NewDefinition(owned, "lucid-ornament-a", OrnamentCategory.Story, Identifier(0), out _);
                var group = owned.Host("Lucid ornament toggle group", false).AddComponent<UIToggleGroup>();

                rig.Toggle.Setup(ToggleParameters(definition, Status(true, true, true), group: group));

                Assert.That(rig.Element.Group, Is.SameAs(group));
            });
        }

        // OnToggleValueChanged 0x69F1B0: selecting reports (definition, available, new marker) to the select callback, outlines the
        // toggle, makes it non-interactive, clears the new marker (parameters and widget) and plays the select sound. The callback
        // registered by Setup is replaced by the next Setup (RemoveAllListeners 0x69F099), so only the latest parameters answer.
        [Test]
        public void SelectingReportsTheOrnamentLocksTheToggleAndPlaysTheSound()
        {
            using (var audio = new OwnedAudioServices())
            {
                audio.RegisterAudioManager();
                HLAudioSfxComponent sound = audio.Add<HLAudioSfxComponent>();
                Set(sound, "m_audioSourceIdentifier", HLAudioSourceIdentifier.Sfx);
                Owned.Run(owned =>
                {
                    OrnamentToggleRig rig = NewToggleRig(owned, sound);
                    OrnamentDefinition first = NewDefinition(owned, "lucid-ornament-a", OrnamentCategory.Story, Identifier(0), out _);
                    OrnamentDefinition second = NewDefinition(owned, "lucid-ornament-b", OrnamentCategory.Story, Identifier(1), out _);
                    var firstCalls = new List<OrnamentDefinition>();
                    var secondCalls = new List<(OrnamentDefinition definition, bool available, bool isNew)>();
                    rig.Toggle.Setup(ToggleParameters(first, Status(true, true, true), (d, a, n) => firstCalls.Add(d)));
                    UIWidgetOrnamentToggleParameters parameters = ToggleParameters(second, Status(true, false, true),
                        (d, a, n) => secondCalls.Add((d, a, n)));
                    rig.Toggle.Setup(parameters);
                    Assert.That(rig.NewIcon.activeSelf, Is.True);

                    rig.UiToggle.OnValueChanged.Invoke(true);

                    Assert.That(firstCalls, Is.Empty, "the earlier listener was removed");
                    Assert.That(secondCalls.Count, Is.EqualTo(1));
                    Assert.That(secondCalls[0].definition, Is.SameAs(second));
                    Assert.That(secondCalls[0].available, Is.True);
                    Assert.That(secondCalls[0].isNew, Is.True, "reported before the marker is cleared");
                    Assert.That(rig.Outline.enabled, Is.True);
                    Assert.That(rig.UiToggle.IsInteractable(), Is.False);
                    Assert.That(rig.NewIcon.activeSelf, Is.False);
                    Assert.That(parameters.IsNew, Is.False);
                    Assert.That(Get<bool>(rig.Toggle, "m_isNew"), Is.False);
                    Assert.That(audio.Log.Errors, Is.EqualTo(new[] { NoClipAtSfx }), "the select sound was requested once");
                });
            }
        }

        // Deselecting 0x69F23A outlines nothing and makes the toggle interactive again; it reports nothing and plays no sound.
        [Test]
        public void DeselectingRestoresTheToggleSilently()
        {
            using (var audio = new OwnedAudioServices())
            {
                audio.RegisterAudioManager();
                HLAudioSfxComponent sound = audio.Add<HLAudioSfxComponent>();
                Set(sound, "m_audioSourceIdentifier", HLAudioSourceIdentifier.Sfx);
                Owned.Run(owned =>
                {
                    OrnamentToggleRig rig = NewToggleRig(owned, sound);
                    OrnamentDefinition definition = NewDefinition(owned, "lucid-ornament-a", OrnamentCategory.Story, Identifier(0), out _);
                    int calls = 0;
                    rig.Toggle.Setup(ToggleParameters(definition, Status(true, true, true), (d, a, n) => calls++));
                    rig.UiToggle.OnValueChanged.Invoke(true);
                    audio.Log.Errors.Clear();
                    calls = 0;

                    rig.UiToggle.OnValueChanged.Invoke(false);

                    Assert.That(rig.Outline.enabled, Is.False);
                    Assert.That(rig.UiToggle.IsInteractable(), Is.True);
                    Assert.That(calls, Is.EqualTo(0));
                    Assert.That(audio.Log.Errors, Is.Empty);
                });
            }
        }

        // 0x69F10E..0x69F18B: a default-selected toggle runs the selected path at once, without the sound, and switches itself on without
        // notifying again.
        [Test]
        public void ADefaultSelectedToggleSelectsItselfWithoutTheSound()
        {
            using (var audio = new OwnedAudioServices())
            {
                audio.RegisterAudioManager();
                HLAudioSfxComponent sound = audio.Add<HLAudioSfxComponent>();
                Owned.Run(owned =>
                {
                    OrnamentToggleRig rig = NewToggleRig(owned, sound);
                    OrnamentDefinition definition = NewDefinition(owned, "lucid-ornament-a", OrnamentCategory.Story, Identifier(0), out _);
                    var calls = new List<(bool available, bool isNew)>();
                    UIWidgetOrnamentToggleParameters parameters = ToggleParameters(definition, Status(true, false, true),
                        (d, a, n) => calls.Add((a, n)), defaultSelected: true);

                    rig.Toggle.Setup(parameters);

                    Assert.That(calls, Is.EqualTo(new[] { (true, true) }), "reported once, not again by the silent switch-on");
                    Assert.That(rig.UiToggle.IsOn, Is.True);
                    Assert.That(rig.Outline.enabled, Is.True);
                    Assert.That(rig.UiToggle.IsInteractable(), Is.False);
                    Assert.That(rig.NewIcon.activeSelf, Is.False);
                    Assert.That(parameters.IsNew, Is.False);
                    Assert.That(audio.Log.Errors, Is.Empty, "no sound for the default selection");
                });
            }
        }

        // 0x69EF1A..0x69EF7B: a toggle handed a different ornament (a different GUID) is reset first: switched off silently and made
        // interactive again. The same instance, or another instance with the same GUID (the definition operators compare GUIDs), keeps
        // its selection.
        [TestCase(false, false, TestName = "AToggleReusedForAnotherOrnamentIsReset")]
        [TestCase(true, true, TestName = "AToggleReusedForTheSameDefinitionKeepsItsSelection")]
        [TestCase(false, true, TestName = "AToggleReusedForADefinitionWithTheSameGuidKeepsItsSelection")]
        public void SetupResetsTheSelectionOnlyForAnotherOrnament(bool sameInstance, bool keepsSelection)
        {
            bool sameGuid = keepsSelection;
            Owned.Run(owned =>
            {
                OrnamentToggleRig rig = NewToggleRig(owned, null);
                OrnamentDefinition first = NewDefinition(owned, "lucid-ornament-a", OrnamentCategory.Story, Identifier(0), out _);
                OrnamentDefinition next = sameInstance ? first
                    : NewDefinition(owned, sameGuid ? "lucid-ornament-a" : "lucid-ornament-b", OrnamentCategory.Story, Identifier(1), out _);
                rig.Toggle.Setup(ToggleParameters(first, Status(true, true, true), defaultSelected: true));
                Assert.That(rig.UiToggle.IsOn, Is.True);
                Assert.That(rig.UiToggle.IsInteractable(), Is.False);

                rig.Toggle.Setup(ToggleParameters(next, Status(true, true, true)));

                Assert.That(rig.UiToggle.IsOn, Is.EqualTo(keepsSelection));
                Assert.That(rig.UiToggle.IsInteractable(), Is.EqualTo(!keepsSelection));
                Assert.That(Get<OrnamentDefinition>(rig.Toggle, "m_definition"), Is.SameAs(next));
            });
        }

        // Action_OnSelected 0x69F290 hands the hover callback this widget's own RectTransform.
        [Test]
        public void SelectingTheWidgetReportsItsTransformToTheHoverCallback()
        {
            Owned.Run(owned =>
            {
                OrnamentToggleRig rig = NewToggleRig(owned, null);
                OrnamentDefinition definition = NewDefinition(owned, "lucid-ornament-a", OrnamentCategory.Story, Identifier(0), out _);
                var hovered = new List<RectTransform>();
                rig.Toggle.Setup(ToggleParameters(definition, Status(true, true, true), hover: hovered.Add));

                rig.Toggle.Action_OnSelected();

                Assert.That(hovered.Count, Is.EqualTo(1));
                Assert.That(hovered[0], Is.SameAs((RectTransform)rig.Toggle.transform));
            });
        }

        // OnDestroy 0x69F300 unloads the definition's thumbnail once.
        [Test]
        public void DestroyingTheToggleUnloadsTheThumbnail()
        {
            Owned.Run(owned =>
            {
                OrnamentToggleRig rig = NewToggleRig(owned, null);
                OrnamentDefinition definition = NewDefinition(owned, "lucid-ornament-a", OrnamentCategory.Story, Identifier(0), out _);
                rig.Toggle.Setup(ToggleParameters(definition, Status(true, true, true)));
                Assert.That(Count(definition.ThumbnailAsset), Is.EqualTo(1));

                Call(rig.Toggle, "OnDestroy");

                Assert.That(Count(definition.ThumbnailAsset), Is.EqualTo(0));
                Assert.That(IsCached(definition.ThumbnailAsset), Is.False);
            });
        }

        // ---- UIWidgetOrnamentEntryRow (0x69E350..0x69E780) and UIWidgetOrnamentEntryRowComposer (0x69E8E0..0x69EE20)

        private sealed class RowRig
        {
            public OrnamentToggleRig Prefab;
            public readonly List<OrnamentDefinition> Definitions = new List<OrnamentDefinition>();
        }

        // A row prefab over a toggle prefab. The rows' constructor caches the UINavigationManager reference, so its row is leased first.
        private static void WithRows(int definitionCount, Action<Owned, RowRig> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<UINavigationManager>();
            var rig = new RowRig { Prefab = NewToggleRig(owned, null) };
            for (int i = 0; i < definitionCount; i++)
                rig.Definitions.Add(NewDefinition(owned, "lucid-row-ornament-" + i, OrnamentCategory.Story, Identifier(i), out _));
            body(owned, rig);
        });

        private static UIWidgetOrnamentEntryRow NewRow(Owned owned, RowRig rig)
        {
            UIWidgetOrnamentEntryRow row = owned.Host("Lucid ornament row", false).AddComponent<UIWidgetOrnamentEntryRow>();
            Set(row, "m_thumbnailPrefab", rig.Prefab.Toggle);
            return row;
        }

        private static UIWidgetOrnamentEntryRowParameters RowOf(RowRig rig, params int[] indices)
        {
            var toggles = new List<UIWidgetOrnamentToggleParameters>();
            foreach (int index in indices)
                toggles.Add(ToggleParameters(rig.Definitions[index], Status(true, true, true)));
            return new UIWidgetOrnamentEntryRowParameters(toggles);
        }

        private static List<UIWidgetOrnamentToggle> Entries(UIWidgetOrnamentEntryRow row) => Get<List<UIWidgetOrnamentToggle>>(row, "m_entriesInThisRow");

        // Setup 0x69E350 adds a toggle (a clone of the thumbnail prefab under the row) for each entry and sets each up with its own
        // parameters, in order.
        [Test]
        public void ARowHoldsOneSetUpToggleForEachEntry()
        {
            WithRows(3, (owned, rig) =>
            {
                UIWidgetOrnamentEntryRow row = NewRow(owned, rig);

                row.Setup(RowOf(rig, 2, 0, 1));

                Assert.That(row.transform.childCount, Is.EqualTo(3));
                List<UIWidgetOrnamentToggle> entries = Entries(row);
                Assert.That(entries.Count, Is.EqualTo(3));
                Assert.That(entries.TrueForAll(entry => entry != rig.Prefab.Toggle && entry.transform.parent == row.transform), Is.True, "clones under the row");
                Assert.That(Get<OrnamentDefinition>(entries[0], "m_definition"), Is.SameAs(rig.Definitions[2]));
                Assert.That(Get<OrnamentDefinition>(entries[1], "m_definition"), Is.SameAs(rig.Definitions[0]));
                Assert.That(Get<OrnamentDefinition>(entries[2], "m_definition"), Is.SameAs(rig.Definitions[1]));
            });
        }

        // A row set up again keeps its toggles (the same instances, set up with the new entries) and adds only the missing ones.
        [Test]
        public void ARowSetUpAgainReusesItsTogglesAndGrows()
        {
            WithRows(4, (owned, rig) =>
            {
                UIWidgetOrnamentEntryRow row = NewRow(owned, rig);
                row.Setup(RowOf(rig, 0, 1));
                UIWidgetOrnamentToggle first = Entries(row)[0], second = Entries(row)[1];

                row.Setup(RowOf(rig, 3, 2, 1, 0));

                List<UIWidgetOrnamentToggle> entries = Entries(row);
                Assert.That(entries.Count, Is.EqualTo(4));
                Assert.That(entries[0], Is.SameAs(first));
                Assert.That(entries[1], Is.SameAs(second));
                Assert.That(row.transform.childCount, Is.EqualTo(4));
                for (int i = 0; i < 4; i++)
                    Assert.That(Get<OrnamentDefinition>(entries[i], "m_definition"), Is.SameAs(rig.Definitions[3 - i]), "toggle " + i);
            });
        }

        // Surplus toggles are removed from the end, one destroy request each (Edit Mode refuses and logs it), and the rest are set up again.
        [Test]
        public void ARowSetUpWithFewerEntriesDestroysTheLastToggles()
        {
            WithRows(3, (owned, rig) =>
            {
                UIWidgetOrnamentEntryRow row = NewRow(owned, rig);
                row.Setup(RowOf(rig, 0, 1, 2));
                UIWidgetOrnamentToggle first = Entries(row)[0];

                LogAssert.Expect(LogType.Error, RefusedDestroy);
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                row.Setup(RowOf(rig, 2));

                List<UIWidgetOrnamentToggle> entries = Entries(row);
                Assert.That(entries.Count, Is.EqualTo(1));
                Assert.That(entries[0], Is.SameAs(first));
                Assert.That(Get<OrnamentDefinition>(first, "m_definition"), Is.SameAs(rig.Definitions[2]));
            });
        }

        // SelectFirst 0x69E6F0 looks the navigation manager up before touching the entries: with none registered it does nothing, even for a
        // row that has no toggle at all (reading entry zero first would fault).
        [Test]
        public void SelectFirstDoesNothingWithoutANavigationManager()
        {
            WithRows(0, (owned, rig) =>
            {
                UIWidgetOrnamentEntryRow row = NewRow(owned, rig);
                Assert.DoesNotThrow(() => row.SelectFirst());
            });
        }

        // Composer Setup 0x69E8E0 spawns one row clone under itself per row of data and sets each up.
        [Test]
        public void TheComposerSpawnsAndSetsUpOneRowPerRowOfData()
        {
            WithRows(3, (owned, rig) =>
            {
                UIWidgetOrnamentEntryRowComposer composer = owned.Host("Lucid ornament composer", false).AddComponent<UIWidgetOrnamentEntryRowComposer>();
                Set(composer, "m_rowPrefab", NewRow(owned, rig));

                composer.Setup(new UIWidgetOrnamentEntryRowComposerParameters(new List<UIWidgetOrnamentEntryRowParameters>
                {
                    RowOf(rig, 0, 1), RowOf(rig, 2),
                }));

                Assert.That(composer.transform.childCount, Is.EqualTo(2));
                UIWidgetOrnamentEntryRow first = composer.transform.GetChild(0).GetComponent<UIWidgetOrnamentEntryRow>();
                UIWidgetOrnamentEntryRow second = composer.transform.GetChild(1).GetComponent<UIWidgetOrnamentEntryRow>();
                Assert.That(Entries(first).Count, Is.EqualTo(2));
                Assert.That(Entries(second).Count, Is.EqualTo(1));
                Assert.That(Get<OrnamentDefinition>(Entries(first)[1], "m_definition"), Is.SameAs(rig.Definitions[1]));
                Assert.That(Get<OrnamentDefinition>(Entries(second)[0], "m_definition"), Is.SameAs(rig.Definitions[2]));
            });
        }

        // Clear 0x69EB40 asks to destroy each direct child (the rows), not their descendants; Edit Mode refuses and logs each request.
        [Test]
        public void ClearingTheComposerDestroysEachChildOnce()
        {
            WithRows(3, (owned, rig) =>
            {
                UIWidgetOrnamentEntryRowComposer composer = owned.Host("Lucid ornament composer", false).AddComponent<UIWidgetOrnamentEntryRowComposer>();
                Set(composer, "m_rowPrefab", NewRow(owned, rig));
                composer.Setup(new UIWidgetOrnamentEntryRowComposerParameters(new List<UIWidgetOrnamentEntryRowParameters>
                {
                    RowOf(rig, 0, 1), RowOf(rig, 2),
                }));

                LogAssert.Expect(LogType.Error, RefusedDestroy);
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                composer.Clear();
            });
        }

        // ---- UIWidgetSDTCharacterSelect (0x69F740..0x69FA10)

        private static CharacterDefinition NewCharacter(Owned owned, out Sprite render)
        {
            CharacterDefinition character = NewCharacterDefinition(owned, CharacterId.Sonic, out _, out render);
            JsonUtility.FromJsonOverwrite("{\"m_name\":" + (int)(Strings)9201 + "}", character); // Unity's serializer sets the authored name
            return character;
        }

        // Start 0x69F740 publishes the character's render (loading it), the companion render only when its key is valid, and the name.
        [Test]
        public void StartPublishesTheRenderTheNameAndAValidCompanion()
        {
            Owned.Run(owned =>
            {
                CharacterDefinition character = NewCharacter(owned, out Sprite render);
                var companionWrapper = new ManagedAddressableAsset<Sprite>(new AssetReferenceT<Sprite>(ValidKey));
                Sprite companion = NewSprite(owned);
                Cache(companionWrapper, companion);
                character.RenderCompanion = companionWrapper;
                var widget = owned.Host("Lucid character select", false).AddComponent<UIWidgetSDTCharacterSelect>();
                Set(widget, "m_characterDefinition", character);

                Call(widget, "Start");

                Assert.That(widget.CharacterRender.Value, Is.SameAs(render));
                Assert.That(widget.CompanionRender.Value, Is.SameAs(companion));
                Assert.That(widget.CharacterName.Value, Is.EqualTo((Strings)9201));
                Assert.That(Count(character.Render), Is.EqualTo(1));
                Assert.That(Count(companionWrapper), Is.EqualTo(1));
            });
        }

        // 0x69F7BE: a companion wrapper without a valid key is neither loaded nor published.
        [Test]
        public void StartLeavesTheCompanionAloneWithoutAValidKey()
        {
            Owned.Run(owned =>
            {
                CharacterDefinition character = NewCharacter(owned, out Sprite render);
                var companionWrapper = new ManagedAddressableAsset<Sprite>(null);
                character.RenderCompanion = companionWrapper;
                var widget = owned.Host("Lucid character select", false).AddComponent<UIWidgetSDTCharacterSelect>();
                Set(widget, "m_characterDefinition", character);

                Call(widget, "Start");

                Assert.That(widget.CharacterRender.Value, Is.SameAs(render));
                Assert.That(widget.CompanionRender.Value, Is.Null);
                Assert.That(Count(companionWrapper), Is.EqualTo(0));
            });
        }

        // OnDestroy 0x69F820 unloads the render and the companion render.
        [Test]
        public void DestroyingTheWidgetUnloadsBothRenders()
        {
            Owned.Run(owned =>
            {
                CharacterDefinition character = NewCharacter(owned, out _);
                var companionWrapper = new ManagedAddressableAsset<Sprite>(new AssetReferenceT<Sprite>(ValidKey));
                Cache(companionWrapper, NewSprite(owned));
                character.RenderCompanion = companionWrapper;
                var widget = owned.Host("Lucid character select", false).AddComponent<UIWidgetSDTCharacterSelect>();
                Set(widget, "m_characterDefinition", character);
                Call(widget, "Start");
                Assert.That(Count(character.Render), Is.EqualTo(1));
                Assert.That(Count(companionWrapper), Is.EqualTo(1));

                Call(widget, "OnDestroy");

                Assert.That(Count(character.Render), Is.EqualTo(0));
                Assert.That(Count(companionWrapper), Is.EqualTo(0));
            });
        }

        // Action_OnCharacterSelected 0x69F880: deselecting does nothing; selecting reports the character to the event, then schedules the
        // highlight move for the next frame. The next-frame host here refuses coroutines (its error is the scheduling request).
        [Test]
        public void SelectingTheCharacterRaisesTheEventAndSchedulesTheHighlight()
        {
            using (var audio = new OwnedAudioServices())
            {
                audio.RefusingCoroutineHost();
                Owned.Run(owned =>
                {
                    CharacterDefinition character = NewCharacter(owned, out _);
                    var widget = owned.Host("Lucid character select", false).AddComponent<UIWidgetSDTCharacterSelect>();
                    Set(widget, "m_characterDefinition", character);
                    var selected = new List<CharacterDefinition>();
                    var selectedEvent = new UnityEvent<CharacterDefinition>();
                    selectedEvent.AddListener(selected.Add);
                    Set(widget, "m_onCharacterSelected", selectedEvent);

                    widget.Action_OnCharacterSelected(false);
                    Assert.That(selected, Is.Empty);

                    LogAssert.Expect(LogType.Error, RefusedCoroutine);
                    widget.Action_OnCharacterSelected(true);
                    Assert.That(selected.Count, Is.EqualTo(1));
                    Assert.That(selected[0], Is.SameAs(character));
                });
            }
        }

        // 0x69F8CA..0x69F8CD: an absent event is skipped, and the highlight is still scheduled.
        [Test]
        public void SelectingWithoutAnEventStillSchedulesTheHighlight()
        {
            using (var audio = new OwnedAudioServices())
            {
                audio.RefusingCoroutineHost();
                Owned.Run(owned =>
                {
                    var widget = owned.Host("Lucid character select", false).AddComponent<UIWidgetSDTCharacterSelect>();
                    Set(widget, "m_onCharacterSelected", null);

                    LogAssert.Expect(LogType.Error, RefusedCoroutine);
                    Assert.DoesNotThrow(() => widget.Action_OnCharacterSelected(true));
                });
            }
        }

        // The next-frame action 0x69FA10 reparents the highlight under the container, keeping its world position (SetParent without the
        // bool, 0x26DAAE0); with either object missing it does nothing.
        [Test]
        public void TheHighlightMovesUnderItsContainerKeepingItsWorldPosition()
        {
            Owned.Run(owned =>
            {
                var widget = owned.Host("Lucid character select", false).AddComponent<UIWidgetSDTCharacterSelect>();
                GameObject container = owned.Host("Lucid highlight container", false);
                container.transform.position = new Vector3(10f, 0f, 0f);
                GameObject highlight = owned.Host("Lucid highlight", false);
                highlight.transform.position = new Vector3(5f, 0f, 0f);
                Set(widget, "m_highlight", highlight);
                Set(widget, "m_highlightContainer", container);

                Call(widget, "<Action_OnCharacterSelected>b__9_0");

                Assert.That(highlight.transform.parent, Is.SameAs(container.transform));
                Assert.That(highlight.transform.position, Is.EqualTo(new Vector3(5f, 0f, 0f)));
                Assert.That(highlight.transform.localPosition, Is.EqualTo(new Vector3(-5f, 0f, 0f)));
            });
        }

        [TestCase(true, false, TestName = "TheHighlightStaysPutWithoutAContainer")]
        [TestCase(false, true, TestName = "AMissingHighlightIsLeftAlone")]
        public void TheHighlightMoveNeedsBothObjects(bool hasHighlight, bool hasContainer)
        {
            Owned.Run(owned =>
            {
                var widget = owned.Host("Lucid character select", false).AddComponent<UIWidgetSDTCharacterSelect>();
                GameObject highlight = hasHighlight ? owned.Host("Lucid highlight", false) : null;
                GameObject container = hasContainer ? owned.Host("Lucid highlight container", false) : null;
                Set(widget, "m_highlight", highlight);
                Set(widget, "m_highlightContainer", container);

                Assert.DoesNotThrow(() => Call(widget, "<Action_OnCharacterSelected>b__9_0"));

                if (hasHighlight) Assert.That(highlight.transform.parent, Is.Null);
            });
        }
    }
}
