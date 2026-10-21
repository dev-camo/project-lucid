using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.UIContainerShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.EnemyTestSupport;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The enemy, frame, track and character debug overlays (Game.Runtime 02000606 ViewEnemyInformation, 02000607 ViewFrameInformation,
    // 02000608 ViewTrackInformation, 02000605 ViewCharacterInformation; x86-64 0x564500..0x5645C0, 0x5645D0..0x564AC0,
    // 0x564BE0..0x564FB0 and 0x563E50..0x564330) and the frame counter container (020008ba UIContainerFPSDebug,
    // 0x631730..0x6317DF). The views are real components on owned inactive hosts, so Edit Mode sends them no
    // Awake, OnEnable or Update and the cases call those directly, as ActorInformationViewTests does for the base view. The debug
    // inputs they bind are leased through ControlTableLease. The character view runs over a real CharacterManager and Character.
    // Not covered: ViewCharacterInformation.Update with an actor (it asks the character for its transform and ability tables, which
    // read the rigid body and gravity that Edit Mode never builds), ViewCharacterInformation.OnDisable with an invalid manager
    // reference, and the frame rate number of ViewFrameInformation.Update (the frame delta comes from the engine's clock).
    public sealed class DebugInformationViewTests
    {
        private static void WithInputs(Action<Owned> body, params GameInput[] inputs) =>
            ControlTableLease.Run(() => Owned.Run(body), inputs);

        private static void WithActorInputs(Action<Owned> body) =>
            WithInputs(body, GameInput.DebugNextInfo, GameInput.DebugClearFsmInfo);

        private static T NewView<T>(Owned owned) where T : Component => owned.Host("Lucid debug information view", false).AddComponent<T>();

        private static List<GameObject> NewPanels(Owned owned, object view, int count)
        {
            var panels = new List<GameObject>();
            for (int i = 0; i < count; i++)
            {
                GameObject panel = owned.Host("Lucid debug information panel " + i);
                panels.Add(panel);
                Get<List<GameObject>>(view, "m_infoList").Add(panel);
            }
            return panels;
        }

        private static bool[] Visible(List<GameObject> panels) => panels.ConvertAll(panel => panel.activeSelf).ToArray();

        // ---- ViewEnemyInformation ----

        // dump.cs TypeDefIndex 1541 and the DummyDll rows (field 0x0400155e, methods 0x0600205c..0x0600205e).
        [Test]
        public void TheEnemyViewDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(ViewEnemyInformation), 0x00100001,
                new (string, int)[] { ("m_enemy", 0x0001) },
                new (string, int)[] { ("OnEnable", 0x00c4), ("Update", 0x00c4), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(ViewEnemyInformation), 2, 1);
            Assert.That(typeof(ViewEnemyInformation).BaseType, Is.EqualTo(typeof(ViewActorInformation)));
            Assert.That(AttributeTypes(Field(typeof(ViewEnemyInformation), "m_enemy")), Is.EqualTo(new[] { typeof(SerializeField) }));
        }

        // 0x564500: the authored enemy becomes the actor (the store at +0x70 comes from +0x88), then the base binds both debug inputs.
        [Test]
        public void EnablingTakesTheAuthoredEnemyBeforeTheBaseBinds()
        {
            WithActorInputs(owned =>
            {
                Enemy enemy = NewEnemy(owned);
                ViewEnemyInformation view = NewView<ViewEnemyInformation>(owned);
                Set(view, "m_enemy", enemy); // Reflection: serialized reference

                Call(view, "OnEnable");

                Assert.That(Get<Actor>(view, "m_actor"), Is.SameAs(enemy));
                Assert.That(ControlTables.Subscriptions(GameInput.DebugNextInfo, view),
                    Is.EqualTo(ControlTables.Expected(InputTrigger.Down, "ViewActorInformation.OnDebugNextInfo")), "the base ran");
                Call(view, "OnDisable");
                Assert.That(Get<Actor>(view, "m_actor"), Is.Null, "the base disable forgets the actor");
            });
        }

        // 0x564530..0x56457E: without an actor the update returns before any text is touched.
        [Test]
        public void TheEnemyUpdateWithoutAnActorLeavesTheTextsAlone()
        {
            WithActorInputs(owned =>
            {
                ViewEnemyInformation view = NewView<ViewEnemyInformation>(owned);
                Get<StringBuilder>(view, "m_stringInfo").Append("builder");
                view.StateInfo.Value = "stale";

                Call(view, "Update");

                Assert.That(view.StateInfo.Value, Is.EqualTo("stale"));
            });
        }

        // 0x564587..0x5645BA: with an actor the state text takes the builder as it stands (nothing clears it, and the base update,
        // which would blank every other text and read the activation manager, is not run).
        [Test]
        public void TheEnemyUpdatePublishesOnlyTheStateTextFromTheUnclearedBuilder()
        {
            WithActorInputs(owned =>
            {
                ViewEnemyInformation view = NewView<ViewEnemyInformation>(owned);
                Set(view, "m_enemy", NewEnemy(owned)); // Reflection: serialized reference
                Call(view, "OnEnable");
                var builder = Get<StringBuilder>(view, "m_stringInfo");
                builder.Append("enemy state");
                view.ActorInfo.Value = "stale";
                view.EntityActivationInfo.Value = "stale";
                view.StateInfo.Value = "stale";

                Call(view, "Update");
                Assert.That(view.StateInfo.Value, Is.EqualTo("enemy state"));
                builder.Append(" and more");
                Call(view, "Update");

                Assert.That(view.StateInfo.Value, Is.EqualTo("enemy state and more"), "the builder is never cleared");
                Assert.That(view.ActorInfo.Value, Is.EqualTo("stale"));
                Assert.That(view.EntityActivationInfo.Value, Is.EqualTo("stale"));
                Call(view, "OnDisable");
            });
        }

        // ---- ViewFrameInformation ----

        // dump.cs TypeDefIndex 1542 and the DummyDll rows (fields 0x0400155f..0x04001562, methods 0x0600205f..0x06002061); the class
        // carries no Il2CppSetOption attributes.
        [Test]
        public void TheFrameViewDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(ViewFrameInformation), 0x00100001,
                new (string, int)[] { ("Framerate", 0x0026), ("FramerateCPU", 0x0026), ("FramerateGPU", 0x0026), ("m_framerateSamplesMs", 0x0021) },
                new (string, int)[] { ("Update", 0x0081), ("GetAverageFramerateMs", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(ViewFrameInformation));
            Assert.That(typeof(ViewFrameInformation).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
        }

        private static float Average(ViewFrameInformation view, float sample) => (float)Call(view, "GetAverageFramerateMs", sample);

        // 0x564940: each call queues the sample and returns the sum of the queue over its count (float arithmetic).
        [Test]
        public void TheAverageIsOverTheQueuedSamples()
        {
            Owned.Run(owned =>
            {
                ViewFrameInformation view = NewView<ViewFrameInformation>(owned);

                Assert.That(Average(view, 0.5f), Is.EqualTo(0.5f));
                Assert.That(Average(view, 1.5f), Is.EqualTo(1f));
                Assert.That(Average(view, 2.5f), Is.EqualTo(1.5f));
                Assert.That(Get<Queue<float>>(view, "m_framerateSamplesMs").ToArray(), Is.EqualTo(new[] { 0.5f, 1.5f, 2.5f }));
            });
        }

        // 0x5649EC: a sample that brings the queue to 0x3D (61) entries drops the oldest, so the queue holds at most sixty. Sixty
        // samples of one are kept whole (a window of 59 would already have dropped one); the sixty-first, 61, replaces the first one
        // and the average is (59 + 61) / 60.
        [Test]
        public void TheWindowKeepsTheNewestSixtySamples()
        {
            Owned.Run(owned =>
            {
                ViewFrameInformation view = NewView<ViewFrameInformation>(owned);
                var samples = Get<Queue<float>>(view, "m_framerateSamplesMs");
                for (int i = 0; i < 60; i++)
                    Assert.That(Average(view, 1f), Is.EqualTo(1f));
                Assert.That(samples.Count, Is.EqualTo(60));

                Assert.That(Average(view, 61f), Is.EqualTo(2f));
                Assert.That(samples.Count, Is.EqualTo(60));
                Assert.That(samples.Last(), Is.EqualTo(61f));
                Assert.That(Average(view, 1f), Is.EqualTo(2f), "the oldest one goes again");
                Assert.That(samples.Count, Is.EqualTo(60));
            });
        }

        // 0x5645D0: the update publishes the CPU text, the GPU text and the frame rate text in the original formats and queues one
        // delta time sample. The CPU and GPU texts show the timer frequencies (0x564649..0x5646E5, 0x564709..0x564783: Math.Round to
        // three decimals of the converted GetCpuTimerFrequency/GetGpuTimerFrequency, never a captured frame time). Both are whole
        // platform constants, so the rounding is the identity and the text is the ulong's own decimal text. The frame rate comes
        // from the engine's clock, so only its format is checked.
        [Test]
        public void TheFrameUpdatePublishesAllThreeTextsInTheirFormats()
        {
            Owned.Run(owned =>
            {
                ViewFrameInformation view = NewView<ViewFrameInformation>(owned);

                Call(view, "Update");

                Assert.That(view.FramerateCPU.Value, Is.EqualTo("CPU: " + FrameTimingManager.GetCpuTimerFrequency().ToString() + "ms"));
                Assert.That(view.FramerateGPU.Value, Is.EqualTo("GPU: " + FrameTimingManager.GetGpuTimerFrequency().ToString() + "ms"));
                Assert.That(view.Framerate.Value, Does.StartWith("Framerate: ").And.EndsWith(" FPS"));
                Assert.That(Get<Queue<float>>(view, "m_framerateSamplesMs").Count, Is.EqualTo(1));
            });
        }

        // ---- ViewTrackInformation ----

        // dump.cs TypeDefIndex 1543 and the DummyDll rows (fields 0x04001563..0x04001567, methods 0x06002062..0x06002067).
        [Test]
        public void TheTrackViewDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(ViewTrackInformation), 0x00100001,
                new (string, int)[]
                {
                    ("m_infoList", 0x0001), ("InstancePrefabPoolsInfo", 0x0026), ("m_infoIndex", 0x0001), ("m_stringInfo", 0x0001),
                    ("m_trackManagerRef", 0x0021),
                },
                new (string, int)[]
                {
                    ("OnEnable", 0x01c4), ("OnDisable", 0x01c4), ("OnDebugNextInfo", 0x0081), ("EnableInfoList", 0x0081), ("Update", 0x01c4),
                    (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(ViewTrackInformation), 1, 2);
            Assert.That(typeof(ViewTrackInformation).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(AttributeTypes(Field(typeof(ViewTrackInformation), "m_infoList")), Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(Tooltip(Field(typeof(ViewTrackInformation), "m_infoList")), Is.EqualTo("List of info panels to cycle visibility."));
        }

        private static void WithTrackView(Action<Owned, ViewTrackInformation> body) =>
            WithInputs(owned =>
            {
                owned.LeaseRow<TrackManager>(); // the field initialiser's reference creates the row
                body(owned, NewView<ViewTrackInformation>(owned));
            }, GameInput.DebugNextInfo);

        // 0x564BE0: the view binds the next-info input alone, as a Down trigger for any joystick (edx = 1, ecx = -1), and shows only
        // the panel at index zero; 0x564D90 removes the subscription again.
        [Test]
        public void TheTrackViewBindsTheNextInfoInputAndShowsOnlyTheFirstPanel()
        {
            WithTrackView((owned, view) =>
            {
                List<GameObject> panels = NewPanels(owned, view, 3);

                Call(view, "OnEnable");

                Assert.That(ControlTables.Subscriptions(GameInput.DebugNextInfo, view),
                    Is.EqualTo(ControlTables.Expected(InputTrigger.Down, "ViewTrackInformation.OnDebugNextInfo")));
                Assert.That(Visible(panels), Is.EqualTo(new[] { true, false, false }));

                Call(view, "OnDisable");
                Assert.That(ControlTables.Subscriptions(GameInput.DebugNextInfo, view), Is.Empty);
            });
        }

        // 0x564E30: each next-info input advances the index around the list and shows only that panel.
        [Test]
        public void TheTrackViewNextInfoCyclesThroughThePanels()
        {
            WithTrackView((owned, view) =>
            {
                List<GameObject> panels = NewPanels(owned, view, 3);
                Call(view, "OnEnable");

                var seen = new List<bool[]>();
                for (int i = 0; i < 4; i++)
                {
                    Call(view, "OnDebugNextInfo", 1f);
                    seen.Add(Visible(panels));
                }

                Assert.That(seen[0], Is.EqualTo(new[] { false, true, false }));
                Assert.That(seen[1], Is.EqualTo(new[] { false, false, true }));
                Assert.That(seen[2], Is.EqualTo(new[] { true, false, false }), "it wraps around");
                Assert.That(seen[3], Is.EqualTo(new[] { false, true, false }));
                Call(view, "OnDisable");
            });
        }

        // 0x564F22..0x564F35: without a valid track manager the update changes nothing.
        [Test]
        public void TheTrackUpdateWithoutATrackManagerLeavesTheTextAlone()
        {
            WithTrackView((owned, view) =>
            {
                Get<StringBuilder>(view, "m_stringInfo").Append("builder");
                view.InstancePrefabPoolsInfo.Value = "stale";

                Call(view, "Update");

                Assert.That(view.InstancePrefabPoolsInfo.Value, Is.EqualTo("stale"));
                Assert.That(Get<StringBuilder>(view, "m_stringInfo").ToString(), Is.EqualTo("builder"));
            });
        }

        // 0x564F39..0x564FA6: with a valid track manager the builder is cleared (length zero), the manager's pool info is appended
        // (virtual call at vtable slot 4) and the text takes the result. A manager without track groups appends nothing.
        [Test]
        public void TheTrackUpdateClearsTheBuilderAndPublishesTheManagersPoolInfo()
        {
            ControlTableLease.Run(() => WithWorld(world =>
            {
                ViewTrackInformation view = NewView<ViewTrackInformation>(world.Owned);
                Get<StringBuilder>(view, "m_stringInfo").Append("old text");
                view.InstancePrefabPoolsInfo.Value = "stale";

                Call(view, "Update");

                Assert.That(view.InstancePrefabPoolsInfo.Value, Is.Empty);
                Assert.That(Get<StringBuilder>(view, "m_stringInfo").Length, Is.Zero);
            }), GameInput.DebugNextInfo);
        }

        // 0x564F5C..0x564F6D: the update calls the manager's virtual DebugGetInstancePrefabPoolsInfo (slot 4), so a manager that
        // appends text reaches the published value, and the stale builder text is cleared first.
        [Test]
        public void TheTrackUpdatePublishesWhatTheManagersVirtualPoolInfoAppends()
        {
            ControlTableLease.Run(() => Owned.Run(owned =>
            {
                owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
                var manager = owned.Host("Lucid track manager probe", false).AddComponent<TrackManagerPoolInfoProbe>();
                owned.Register<TrackManager>(manager);
                ViewTrackInformation view = NewView<ViewTrackInformation>(owned);
                Get<StringBuilder>(view, "m_stringInfo").Append("old text");
                view.InstancePrefabPoolsInfo.Value = "stale";

                Call(view, "Update");

                Assert.That(manager.Calls, Is.EqualTo(1));
                Assert.That(view.InstancePrefabPoolsInfo.Value, Is.EqualTo(TrackManagerPoolInfoProbe.Marker));
            }), GameInput.DebugNextInfo);
        }

        // ---- ViewCharacterInformation ----

        // dump.cs TypeDefIndex 1540 and the DummyDll rows (fields 0x04001554..0x0400155d, methods 0x06002057..0x0600205b).
        [Test]
        public void TheCharacterViewDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(ViewCharacterInformation), 0x00100001,
                new (string, int)[]
                {
                    ("AbilityNameInfo", 0x0026), ("AbilityEnabledInfo", 0x0026), ("AbilityUpdateInfo", 0x0026), ("AbilityExtraInfo", 0x0026),
                    ("m_stringAbilityNameInfo", 0x0021), ("m_stringAbilityEnabledInfo", 0x0021), ("m_stringAbilityUpdateInfo", 0x0021),
                    ("m_stringAbilityExtraInfo", 0x0021), ("m_characterManagerRef", 0x0001), ("m_character", 0x0001),
                },
                new (string, int)[]
                {
                    ("OnEnable", 0x00c4), ("OnDisable", 0x00c4), ("OnCharacterChange", 0x0081), ("Update", 0x00c4), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(ViewCharacterInformation), 1, 2);
            Assert.That(typeof(ViewCharacterInformation).BaseType, Is.EqualTo(typeof(ViewActorInformation)));
        }

        // 0x564330: four distinct bindables, four empty builders and no manager reference or character yet.
        [Test]
        public void TheCharacterViewStartsWithFourTextsAndNoCharacter()
        {
            Owned.Run(owned =>
            {
                ViewCharacterInformation view = NewView<ViewCharacterInformation>(owned);
                var texts = new[] { view.AbilityNameInfo, view.AbilityEnabledInfo, view.AbilityUpdateInfo, view.AbilityExtraInfo };
                Assert.That(texts.All(text => text != null && text.Value == null), Is.True);
                Assert.That(texts.Distinct().Count(), Is.EqualTo(4));
                foreach (string name in new[] { "m_stringAbilityNameInfo", "m_stringAbilityEnabledInfo", "m_stringAbilityUpdateInfo", "m_stringAbilityExtraInfo" })
                {
                    Assert.That(Get<StringBuilder>(view, name).Length, Is.Zero, name);
                    Assert.That(Get<StringBuilder>(view, name).Capacity, Is.EqualTo(new StringBuilder().Capacity), name);
                }
                Assert.That(Get<object>(view, "m_characterManagerRef"), Is.Null);
                Assert.That(Get<Character>(view, "m_character"), Is.Null);
            });
        }

        // The debug menu announces the character info toggle through a static action; the case watches it and puts it back.
        private static void WithToggleRecorder(Action<List<bool>> body)
        {
            Action<bool> prior = DebugMenu.OnCharacterInfoToggle;
            var toggles = new List<bool>();
            try
            {
                DebugMenu.OnCharacterInfoToggle = toggles.Add;
                body(toggles);
            }
            finally
            {
                DebugMenu.OnCharacterInfoToggle = prior;
            }
        }

        private static void WithCharacterView(Action<Owned, CharacterManager, Character, ViewCharacterInformation, List<bool>> body) =>
            WithToggleRecorder(toggles => ControlTableLease.Run(() => WithManager((owned, manager, character) =>
                body(owned, manager, character, NewView<ViewCharacterInformation>(owned), toggles)),
                GameInput.DebugNextInfo, GameInput.DebugClearFsmInfo));

        private static Delegate[] Callbacks(CharacterManager manager) =>
            Get<Action<Character>>(manager, "m_onCharacterChange")?.GetInvocationList() ?? new Delegate[0];

        // 0x563E50: the base enables first (it binds the inputs), then the view takes the character manager reference and registers
        // OnCharacterChange with an immediate call if a character is valid (edx = 1), and the debug menu hears the toggle with true.
        // No character is valid here, so the actor stays empty.
        [Test]
        public void EnablingRegistersForCharacterChangesAndTellsTheDebugMenu()
        {
            WithCharacterView((owned, manager, character, view, toggles) =>
            {
                Call(view, "OnEnable");

                Assert.That(ControlTables.Subscriptions(GameInput.DebugNextInfo, view),
                    Is.EqualTo(ControlTables.Expected(InputTrigger.Down, "ViewActorInformation.OnDebugNextInfo")), "the base ran");
                Assert.That(Callbacks(manager).Select(callback => (callback.Target, callback.Method.Name)).ToArray(),
                    Is.EqualTo(new (object, string)[] { (view, "OnCharacterChange") }));
                Assert.That(Get<SystemRef<CharacterManager>>(view, "m_characterManagerRef").Get(), Is.SameAs(manager));
                Assert.That(toggles, Is.EqualTo(new[] { true }));
                Assert.That(Get<Actor>(view, "m_actor"), Is.Null);
                Call(view, "OnDisable");
            });
        }

        // The registration calls back at once for a valid character, which becomes both the actor and the character.
        [Test]
        public void EnablingWithAValidCharacterTakesItAtOnce()
        {
            WithCharacterView((owned, manager, character, view, toggles) =>
            {
                SetCurrent(manager, character, true);

                Call(view, "OnEnable");

                Assert.That(Get<Actor>(view, "m_actor"), Is.SameAs(character));
                Assert.That(Get<Character>(view, "m_character"), Is.SameAs(character));
                Call(view, "OnDisable");
            });
        }

        // 0x564100: a later character change stores the character as the actor (+0x70) and as the view's own character (+0xD0).
        [Test]
        public void ACharacterChangeStoresTheCharacterTwice()
        {
            WithCharacterView((owned, manager, character, view, toggles) =>
            {
                Call(view, "OnEnable");

                Get<Action<Character>>(manager, "m_onCharacterChange")(character);

                Assert.That(Get<Actor>(view, "m_actor"), Is.SameAs(character));
                Assert.That(Get<Character>(view, "m_character"), Is.SameAs(character));
                Call(view, "OnDisable");
            });
        }

        // 0x563FC0: with a valid manager reference the callback is released, the debug menu hears the toggle with false and the base
        // disables (it removes the next-info subscription and forgets the actor, but the view keeps its own character).
        [Test]
        public void DisablingReleasesTheCallbackAndTellsTheDebugMenu()
        {
            WithCharacterView((owned, manager, character, view, toggles) =>
            {
                SetCurrent(manager, character, true);
                Call(view, "OnEnable");
                toggles.Clear();

                Call(view, "OnDisable");

                Assert.That(Callbacks(manager), Is.Empty);
                Assert.That(toggles, Is.EqualTo(new[] { false }));
                Assert.That(ControlTables.Subscriptions(GameInput.DebugNextInfo, view), Is.Empty);
                Assert.That(Get<Actor>(view, "m_actor"), Is.Null);
                Assert.That(Get<Character>(view, "m_character"), Is.SameAs(character));
            });
        }

        // 0x564140..0x564196: without an actor the update returns before any text is touched.
        [Test]
        public void TheCharacterUpdateWithoutAnActorLeavesTheTextsAlone()
        {
            WithCharacterView((owned, manager, character, view, toggles) =>
            {
                view.TransformInfo.Value = "stale";
                view.AbilityNameInfo.Value = "stale";
                view.ActorInfo.Value = "stale";

                Call(view, "Update");

                Assert.That(view.TransformInfo.Value, Is.EqualTo("stale"));
                Assert.That(view.AbilityNameInfo.Value, Is.EqualTo("stale"));
                Assert.That(view.ActorInfo.Value, Is.EqualTo("stale"));
            });
        }

        // ---- UIContainerFPSDebug ----

        [Test]
        public void TheFrameCounterDeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIContainerFPSDebug), 0x00100101,
                new (string, int)[] { ("FPSCount", 0x0026) },
                new (string, int)[] { ("Update", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerFPSDebug), 2, 1);
            Assert.That(typeof(UIContainerFPSDebug).BaseType, Is.EqualTo(typeof(UIContainer)));
        }

        // 0x631730: every update publishes VisualQualityManager_SDT.FramesPerSecond (the shared "N0" text of the static count). The
        // count is a private static the manager's own update fills, so the case sets it and puts it back.
        [Test]
        public void TheFrameCounterPublishesTheStaticFrameRateEachUpdate()
        {
            FieldInfo frames = typeof(VisualQualityManager_SDT).GetField("s_framesPerSecond", BindingFlags.Static | BindingFlags.NonPublic);
            float prior = (float)frames.GetValue(null);
            try
            {
                Owned.Run(owned =>
                {
                    var counter = owned.Host("Lucid frame counter", false).AddComponent<UIContainerFPSDebug>();
                    frames.SetValue(null, 59.6f);
                    Call(counter, "Update");
                    Assert.That(counter.FPSCount.Value, Is.EqualTo("60"));
                    frames.SetValue(null, 144.4f);
                    Call(counter, "Update");
                    Assert.That(counter.FPSCount.Value, Is.EqualTo("144"));
                });
            }
            finally
            {
                frames.SetValue(null, prior);
            }
        }
    }
}
