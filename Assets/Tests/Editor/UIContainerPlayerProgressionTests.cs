using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.ProgressionContainerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerPlayerProgression (Game.Runtime 02000904, x86-64 0x672170..0x673440) as a real component on an owned inactive
    // host over the world of LevelRequirementWorld (real App, DataManager, SaveManager, MissionManager and LevelManager), a
    // real AudioManager over a real source handler (no sources, so a request for the empty clip is reported through the
    // fixture's HLUnityCore), an owned string table, real UIWidgetProgression widgets and recording unlocks. The unlocks are
    // the test's own implementations of IMetaGameUnlock, which lets a case choose the progression type, the widget and the
    // exit flags; the restored unlock classes have their own tests.
    //
    // Edit Mode refuses two engine calls the screen makes, and each is expected where it happens: the next-frame activation of
    // a shown widget (the process-wide coroutine host of OwnedCharacterManagers is inactive; its callback, 06003432, is
    // then called directly) and the destruction of the previous widget (Object.Destroy; the clone stays under the owned root).
    //
    // Not covered: the zone of a MetaGameUnlockMissionGroup (it needs a level whose loading image is a valid Addressables
    // key, otherwise its texture callback faults), the level skip of the debug unlock-all switch (AreAllLevelsUnlocked is a
    // constant false in the retail build, shown here by two Level unlocks both being displayed), and the jingle that is kept
    // because the same type is still playing (it needs a playing audio source).
    public sealed class UIContainerPlayerProgressionTests
    {
        private const string NoClipAtSfx = "Will not play the given audio clip at source Sfx, no clip was specified.:0";
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");
        private static readonly Regex RefusedDestroy = new Regex("^Destroy may not be called from edit mode");

        private static readonly Dictionary<Strings, string> Texts = new Dictionary<Strings, string>
        {
            { Strings.MISSING, "Title A" },
            { Strings.MISSIONS, "Body A" },
            { Strings.ORNAMENT_CAT_STORY, "Title B" },
            { Strings.ORNAMENT_CAT_ACHIEVER, "Body B" },
        };

        // An unlock that records what the screen asks of it, in order, into a list shared by the case.
        private sealed class Unlock : IMetaGameUnlock
        {
            private readonly string m_name;
            private readonly PlayerProgressionTypes m_type;
            private readonly List<string> m_calls;
            public UIWidgetProgression Widget;
            public Texture Texture;
            public Strings Title, Body;
            public bool CanExit, UsesAlternativeExit;

            public Unlock(string name, PlayerProgressionTypes type, List<string> calls, UIWidgetProgression widget = null)
            {
                m_name = name;
                m_type = type;
                m_calls = calls;
                Widget = widget;
            }

            public string GetId() => m_name;
            public PlayerProgressionTypes GetProgressionType() => m_type;

            public UIWidgetProgression GetWidget()
            {
                m_calls.Add(m_name + ":GetWidget");
                return Widget;
            }

            public Strings GetTitle() => Title;
            public Strings GetBody() => Body;
            public HLAudioClipIdentifier GetAudio() => HLAudioClipIdentifier.None;
            public void Save(SaveManager saveManager) => m_calls.Add(m_name + ":Save");

            public void GetCustomTexture(Action<Texture> onLoaded)
            {
                m_calls.Add(m_name + ":Texture");
                onLoaded(Texture);
            }

            public void GetCustomSprite(Action<Sprite> onLoaded) { }
            public void ReleaseAssets() => m_calls.Add(m_name + ":Release");
            public bool CanExitToMenu() => CanExit;
            public bool UsesAlternativeExitToMenuEvent() => UsesAlternativeExit;
        }

        private sealed class Rig
        {
            public Owned Owned;
            public LevelRequirementWorld World;
            public App App;
            public OwnedCoreErrorLog Log;
            public UIContainerPlayerProgression Container;
            public Transform Root;
            public UIModernEvent Exit, ExitToMenu, AlternativeExitToMenu;
            public readonly List<string> Calls = new List<string>();
        }

        private static void WithRig(Action<Rig> body, int maxOrnaments = 3) => LevelRequirementWorld.Run((owned, world) =>
        {
            // Enum-string tables the audio manager's diagnostic fills, put back afterwards, and the HLUnityCore the
            // diagnostic is reported through.
            owned.Lease(new VerificationFixtureOwnership.StaticDictionaryLease(typeof(HardlightProject.HardlightEnumExtensions),
                "HLAudioClipIdentifierEnumToString", "HLAudioSourceIdentifierEnumToString"));
            var rig = new Rig { Owned = owned, World = world, App = ProcessManager.GetSystem<App>(), Log = owned.Lease(new OwnedCoreErrorLog()) };
            rig.App.Storage.SetValue(AppFSMKeys.UIModernEvents, new List<UIModernEvent>());

            // A real AudioManager with a real source handler and no sources, registered into the row the world admitted.
            AudioManager audio = owned.Host("Lucid progression audio", false).AddComponent<AudioManager>();
            HLAudioSourceHandler sources = audio.gameObject.AddComponent<HLAudioSourceHandler>();
            sources.Initialise(audio.transform);
            // Reflection: serialized reference that the manager's startup would fill.
            Set(audio, "m_audioSourceHandler", sources);
            owned.Register(audio);
            HLAudioSfxComponent sfx = owned.Host("Lucid progression sfx", false).AddComponent<HLAudioSfxComponent>();
            Set(sfx, "m_audioSourceIdentifier", HLAudioSourceIdentifier.Sfx);

            rig.Root = owned.Host("Lucid progression widget root", true).transform;
            rig.Exit = NewEvent(owned);
            rig.ExitToMenu = NewEvent(owned);
            rig.AlternativeExitToMenu = NewEvent(owned);
            rig.Container = NewContainer<UIContainerPlayerProgression>(owned);
            // Serialized authored references.
            Set(rig.Container, "m_widgetRoot", rig.Root);
            Set(rig.Container, "m_exitEvent", rig.Exit);
            Set(rig.Container, "m_exitToMenuEvent", rig.ExitToMenu);
            Set(rig.Container, "m_alternativeExitToMenuEvent", rig.AlternativeExitToMenu);
            Set(rig.Container, "m_audioSfx", sfx);
            Set(rig.Container, "m_firstLevel", world.NewLevel("lucid-progression-first-level", false));
            Set(rig.Container, "m_maxOrnamentsToShow", maxOrnaments);
            OwnedStringTable.Run(Texts, () => body(rig));
        });

        // An active source widget, so that a clone starts active and the screen's own deactivation is visible.
        private static UIWidgetProgression NewWidget(Owned owned, string name)
        {
            GameObject host = owned.Host("Lucid progression widget " + name, true);
            UIWidgetProgression widget = host.AddComponent<UIWidgetProgression>();
            Animation animation = host.AddComponent<Animation>();
            AnimationClip incoming = NewClip(owned, "Lucid " + name + " in");
            AnimationClip outgoing = NewClip(owned, "Lucid " + name + " out");
            animation.AddClip(incoming, incoming.name);
            animation.AddClip(outgoing, outgoing.name);
            // Serialized authored references.
            Set(widget, "m_animation", animation);
            Set(widget, "m_animateInClip", incoming);
            Set(widget, "m_animateOutClip", outgoing);
            return widget;
        }

        private static UIContainerPlayerProgressionParameters Pending(params IMetaGameUnlock[] unlocks) =>
            new UIContainerPlayerProgressionParameters(new Queue<IMetaGameUnlock>(unlocks));

        private static UIWidgetProgression LastClone(Rig rig) =>
            rig.Root.GetChild(rig.Root.childCount - 1).GetComponent<UIWidgetProgression>();

        // Fires the animation-complete event the engine would raise at the end of the out clip.
        private static void FinishAnimateOut(Rig rig) => LastClone(rig).Action_AnimateOutComplete();

        // dump.cs TypeDefIndex 2307 and the DummyDll rows (fields 0x040024e1..0x040024f7, methods 0x06003428..0x06003433).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerPlayerProgression), 0x00100101,
                new (string, int)[]
                {
                    ("m_widgetRoot", 0x0001), ("m_exitEvent", 0x0001), ("m_exitToMenuEvent", 0x0001),
                    ("m_alternativeExitToMenuEvent", 0x0001), ("m_audioSfx", 0x0001), ("m_firstLevel", 0x0001),
                    ("m_maxOrnamentsToShow", 0x0001), ("m_zoneAddressableSprite", 0x0001),
                    ("Title", 0x0026), ("Body", 0x0026), ("ShowAriemImage", 0x0026), ("ShowZone", 0x0026),
                    ("ZoneTitle", 0x0026), ("ShowMultipleContinueOptions", 0x0026),
                    ("m_app", 0x0001), ("m_saveManager", 0x0001), ("m_levelManager", 0x0001), ("m_progressionsQueue", 0x0001),
                    ("m_previousProgression", 0x0001), ("m_currentProgression", 0x0001), ("m_currentWidget", 0x0001),
                    ("m_unlockTypes", 0x0021), ("m_ornamentUnlocksShown", 0x0001),
                },
                new (string, int)[]
                {
                    ("Setup", 0x00c6), ("PlayNextProgression", 0x0081), ("UpdateProgressionStringBindable", 0x0081),
                    ("Action_AnimateOut", 0x0086), ("Action_AnimateToMenu", 0x0086), ("Continue", 0x0081),
                    ("SaveProgression", 0x0081), ("Exit", 0x0081), (".ctor", 0x1886),
                    ("<PlayNextProgression>b__24_0", 0x0081), ("<PlayNextProgression>b__24_1", 0x0081),
                    ("<Action_AnimateToMenu>b__27_0", 0x0081),
                });
            AssertIl2CppOptions(typeof(UIContainerPlayerProgression), 2, 1);
            Assert.That(typeof(UIContainerPlayerProgression).BaseType, Is.EqualTo(typeof(UIContainer)));
        }

        // Setup 0x672170 shows the character image when the first level has a completed mission (HasAnyCompleteMissions
        // 0x672316 on the level's mission list, +0x30, with the level itself as the argument).
        [Test]
        public void TheCharacterImageFollowsTheCompletedMissionsOfTheFirstLevel()
        {
            WithRig(rig =>
            {
                MissionDefinition mission = rig.World.NewMission("lucid-progression-mission");
                GameplayLevelDefinition level = rig.World.NewLevel("lucid-progression-level", false,
                    rig.World.NewGroup("lucid-progression-group", mission));
                Set(rig.Container, "m_firstLevel", level);

                rig.World.SetComplete(level, mission, false);
                rig.Container.Setup(Pending());
                Assert.That(rig.Container.ShowAriemImage.Value, Is.False);

                rig.World.SetComplete(level, mission, true);
                rig.Container.Setup(Pending());
                Assert.That(rig.Container.ShowAriemImage.Value, Is.True);
            });
        }

        // PlayNextProgression 0x672350 publishes the exit event (AddUniqueUIModernEvent 0x530D60) when the queue is empty
        // (cmp [queue+0x20], 0; jle 0x67255e), so an empty Setup exits at once, once. Action_AnimateOut 0x672DF0 does the same
        // when no widget is shown (0x672e62), even when it was published before.
        [Test]
        public void AnEmptyQueueExitsAtOnceAndAnimatingOutWithoutAWidgetExitsAgain()
        {
            WithRig(rig =>
            {
                rig.Container.Setup(Pending());
                Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { rig.Exit }));
                Assert.That(rig.Root.childCount, Is.Zero);

                rig.App.GetUIModernEvents().Clear();
                rig.Container.Action_AnimateOut();
                Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { rig.Exit }));
            });
        }

        // The ornament cap 0x672636..0x672647: every ornament counts (inc), and one is skipped when the count is greater than
        // m_maxOrnamentsToShow (setg, a signed compare). A skipped unlock is saved and released (SaveProgression 0x672B60) before
        // the next is shown, and is never asked for its widget; an ornament without a widget is not saved. The count restarts at
        // every Setup (0x672331).
        [Test]
        public void OrnamentsBeyondTheCapAreSavedWithoutBeingShown()
        {
            var table = new[]
            {
                (max: 2, calls: new[] { "o1:GetWidget", "o2:GetWidget", "o3:Save", "o3:Release", "o4:Save", "o4:Release" }),
                (max: 0, calls: new[] { "o1:Save", "o1:Release", "o2:Save", "o2:Release", "o3:Save", "o3:Release", "o4:Save", "o4:Release" }),
                (max: -1, calls: new[] { "o1:Save", "o1:Release", "o2:Save", "o2:Release", "o3:Save", "o3:Release", "o4:Save", "o4:Release" }),
                (max: 4, calls: new[] { "o1:GetWidget", "o2:GetWidget", "o3:GetWidget", "o4:GetWidget" }),
            };
            foreach (var row in table)
                WithRig(rig =>
                {
                    Set(rig.Container, "m_maxOrnamentsToShow", row.max);
                    var ornaments = new IMetaGameUnlock[4];
                    for (int index = 0; index < ornaments.Length; ++index)
                        ornaments[index] = new Unlock("o" + (index + 1), PlayerProgressionTypes.Ornament, rig.Calls);
                    rig.Container.Setup(Pending(ornaments));
                    Assert.That(rig.Calls, Is.EqualTo(row.calls), "max " + row.max);
                    Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { rig.Exit }), "max " + row.max);

                    // A new Setup counts from zero again.
                    rig.Calls.Clear();
                    rig.Container.Setup(Pending(new Unlock("again", PlayerProgressionTypes.Ornament, rig.Calls)));
                    Assert.That(rig.Calls, Is.EqualTo(row.max >= 1 ? new[] { "again:GetWidget" } : new[] { "again:Save", "again:Release" }), "max " + row.max);
                });
        }

        // An unlock without a widget (0x6726e1 test, then the recursion at 0x67271a) is passed over without being saved, and
        // the following unlocks are still processed in order.
        [Test]
        public void UnlocksWithoutAWidgetArePassedOverUnsaved()
        {
            WithRig(rig =>
            {
                rig.Container.Setup(Pending(
                    new Unlock("x1", PlayerProgressionTypes.MusicTrack, rig.Calls),
                    new Unlock("x2", PlayerProgressionTypes.MusicTrack, rig.Calls)));
                Assert.That(rig.Calls, Is.EqualTo(new[] { "x1:GetWidget", "x2:GetWidget" }));
                Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { rig.Exit }));
            });
        }

        // A mission group is shown only once per Setup: after one is displayed its type is in the set (HashSet.Add 0x672733)
        // and a later one is skipped, saved and released (0x67262a, Contains; 0x672666). The level skip (0x6725ca) needs
        // AreAllLevelsUnlocked, a constant false here, so a second Level unlock is displayed like any other type.
        [Test]
        public void ASecondMissionGroupIsSkippedButASecondLevelIsShown()
        {
            WithRig(rig =>
            {
                var first = new Unlock("g1", PlayerProgressionTypes.MissionGroup, rig.Calls, NewWidget(rig.Owned, "g1"));
                var second = new Unlock("g2", PlayerProgressionTypes.MissionGroup, rig.Calls, NewWidget(rig.Owned, "g2"));
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                rig.Container.Setup(Pending(first, second));
                Assert.That(rig.Calls, Is.EqualTo(new[] { "g1:GetWidget", "g1:Texture" }));
                Assert.That(rig.Container.ShowZone.Value, Is.False, "only a MetaGameUnlockMissionGroup selects a zone");

                LogAssert.Expect(LogType.Error, RefusedDestroy);
                rig.Container.Action_AnimateOut();
                FinishAnimateOut(rig);
                Assert.That(rig.Calls, Is.EqualTo(new[]
                {
                    "g1:GetWidget", "g1:Texture", "g1:Save", "g1:Release", "g2:Save", "g2:Release",
                }), "the second group is saved and released without being asked for its widget");
                Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { rig.Exit }));
            });
            WithRig(rig =>
            {
                var first = new Unlock("l1", PlayerProgressionTypes.Level, rig.Calls, NewWidget(rig.Owned, "l1"));
                var second = new Unlock("l2", PlayerProgressionTypes.Level, rig.Calls, NewWidget(rig.Owned, "l2"));
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                rig.Container.Setup(Pending(first, second));
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                rig.Container.Action_AnimateOut();
                FinishAnimateOut(rig);
                Assert.That(rig.Calls, Is.EqualTo(new[]
                {
                    "l1:GetWidget", "l1:Texture", "l1:Save", "l1:Release", "l2:GetWidget", "l2:Texture",
                }));
                Assert.That(rig.App.GetUIModernEvents(), Is.Empty, "the second level is on screen");
            });
        }

        // The whole display path of PlayNextProgression 0x672350 for two unlocks, in order: the widget is instantiated under the
        // root (Instantiate 0x672764), the unlock is asked for its texture, which reaches the widget's masked image (06003431),
        // the widget is hidden (SetActive false 0x67286b) and its animation started (AnimateIn 0x672879), the activation is
        // deferred one frame (06003432), title and body are published (0x672C70), the exit-to-menu flag follows CanExitToMenu
        // (slot 10) and the unlock's audio is requested at the Sfx source. Continuing (Continue 0x673030) removes the callback,
        // saves and releases the unlock and shows the next one; continuing the last one exits.
        [Test]
        public void UnlocksAreShownOneAtATimeAndSavedWhenContinued()
        {
            WithRig(rig =>
            {
                Texture texture = rig.Owned.Track(new Texture2D(2, 2));
                var first = new Unlock("a", PlayerProgressionTypes.MusicTrack, rig.Calls, NewWidget(rig.Owned, "a"))
                    { Texture = texture, Title = Strings.MISSING, Body = Strings.MISSIONS, CanExit = true };
                var second = new Unlock("b", PlayerProgressionTypes.Level, rig.Calls, NewWidget(rig.Owned, "b"))
                    { Title = Strings.ORNAMENT_CAT_STORY, Body = Strings.ORNAMENT_CAT_ACHIEVER, CanExit = false };

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                rig.Container.Setup(Pending(first, second));
                Assert.That(rig.Calls, Is.EqualTo(new[] { "a:GetWidget", "a:Texture" }));
                Assert.That(rig.Root.childCount, Is.EqualTo(1));
                UIWidgetProgression shown = LastClone(rig);
                Assert.That(shown, Is.Not.SameAs(first.Widget), "an instance of the widget, not the widget itself");
                Assert.That(shown.MaskedTexture.Value, Is.SameAs(texture));
                Assert.That(shown.gameObject.activeSelf, Is.False, "hidden until the next frame");
                Call(rig.Container, "<PlayNextProgression>b__24_1");
                Assert.That(shown.gameObject.activeSelf, Is.True, "the deferred callback shows it");
                Assert.That(rig.Container.Title.Value, Is.EqualTo("Title A"));
                Assert.That(rig.Container.Body.Value, Is.EqualTo("Body A"));
                Assert.That(rig.Container.ShowMultipleContinueOptions.Value, Is.True);
                Assert.That(rig.Container.ShowZone.Value, Is.False);
                Assert.That(rig.Log.Errors, Is.EqualTo(new[] { NoClipAtSfx }), "the first unlock's audio was requested");

                LogAssert.Expect(LogType.Error, RefusedDestroy);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                rig.Container.Action_AnimateOut();
                FinishAnimateOut(rig);
                Assert.That(rig.Calls, Is.EqualTo(new[]
                {
                    "a:GetWidget", "a:Texture", "a:Save", "a:Release", "b:GetWidget", "b:Texture",
                }));
                Assert.That(rig.Root.childCount, Is.EqualTo(2), "the destroy of the first clone was refused");
                Assert.That(rig.Container.Title.Value, Is.EqualTo("Title B"));
                Assert.That(rig.Container.Body.Value, Is.EqualTo("Body B"));
                Assert.That(rig.Container.ShowMultipleContinueOptions.Value, Is.False);
                Assert.That(rig.Log.Errors, Is.EqualTo(new[] { NoClipAtSfx, NoClipAtSfx }), "another type restarts the audio");
                Assert.That(rig.App.GetUIModernEvents(), Is.Empty);

                rig.Container.Action_AnimateOut();
                FinishAnimateOut(rig);
                Assert.That(rig.Calls[rig.Calls.Count - 2], Is.EqualTo("b:Save"));
                Assert.That(rig.Calls[rig.Calls.Count - 1], Is.EqualTo("b:Release"));
                Assert.That(rig.App.GetUIModernEvents(), Is.EqualTo(new[] { rig.Exit }), "the last unlock continues to the exit");
            });
        }

        // Action_AnimateToMenu 0x672F40 and its callback 06003433 (0x673420): when the shown widget finishes animating out,
        // the shown unlock is saved and released, the rest of the queue is dropped unseen (Queue.Clear 0x6734ad) and the exit
        // event is the alternative one when the unlock asks for it (UsesAlternativeExitToMenuEvent, slot 11, indexing +0x48
        // or +0x50 by the result), otherwise the exit-to-menu event.
        [Test]
        public void AnimatingToTheMenuSavesTheShownUnlockDropsTheRestAndPicksTheMenuEvent()
        {
            foreach (bool alternative in new[] { false, true })
                WithRig(rig =>
                {
                    var first = new Unlock("a", PlayerProgressionTypes.MusicTrack, rig.Calls, NewWidget(rig.Owned, "a"))
                        { UsesAlternativeExit = alternative };
                    var second = new Unlock("b", PlayerProgressionTypes.Level, rig.Calls, NewWidget(rig.Owned, "b"));
                    LogAssert.Expect(LogType.Error, RefusedCoroutine);
                    rig.Container.Setup(Pending(first, second));

                    rig.Container.Action_AnimateToMenu();
                    Assert.That(rig.Calls, Is.EqualTo(new[] { "a:GetWidget", "a:Texture" }), "nothing is saved before the animation ends");
                    FinishAnimateOut(rig);
                    Assert.That(rig.Calls, Is.EqualTo(new[] { "a:GetWidget", "a:Texture", "a:Save", "a:Release" }), "the second unlock is never seen");
                    Assert.That(rig.App.GetUIModernEvents(),
                        Is.EqualTo(new[] { alternative ? rig.AlternativeExitToMenu : rig.ExitToMenu }), "alternative " + alternative);
                });
        }
    }
}
