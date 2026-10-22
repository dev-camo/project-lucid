using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.MissionTrackerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerMissionRanks (Game.Runtime 0x020008e3; OnDestroy 0x63ECB0, Setup 0x63EE00, ShowChallengeRanks 0x63F290,
    // ShowRank 0x63F610, SetIcon 0x63F820) over a real DataManager whose rank definitions hold already loaded icons. The timer
    // format is "{0}|{1}|{2}". Not covered: the title and rank times of a mission whose completed-mission override is absent
    // (the original dereferences it for the failure flag).
    public sealed class UIContainerMissionRanksTests
    {
        private static readonly RankType[] RankTypes = { RankType.S, RankType.A, RankType.B, RankType.C, RankType.D };

        private static readonly Dictionary<Strings, string> OwnedStrings = new Dictionary<Strings, string>
        {
            { Strings.MISSION_TIMER_FORMAT_DESCRIPTION, "{0}|{1}|{2}" },
        };

        private sealed class Rig
        {
            internal Owned Owned;
            internal DataManager Data;
            internal UIContainerMissionRanks Container;
            // The three loaded icons (base, background, foreground) of each rank type, and the definition holding them.
            internal readonly Dictionary<RankType, Sprite[]> Icons = new Dictionary<RankType, Sprite[]>();
            internal readonly Dictionary<RankType, RankDefinition> Definitions = new Dictionary<RankType, RankDefinition>();
        }

        private static void Run(Action<Rig> body) => WithWorld(world =>
        {
            Owned owned = world.Owned;
            var rig = new Rig { Owned = owned };
            rig.Data = owned.Register(owned.Host("Lucid data manager", false).AddComponent<DataManager>());
            foreach (RankType type in RankTypes) AddRankDefinition(rig, type);
            AddRankDefinition(rig, RankType.None); // the type of a rank that fills no field
            rig.Container = owned.Host("Lucid mission ranks", false).AddComponent<UIContainerMissionRanks>();
            OwnedStringTable.Run(OwnedStrings, () => body(rig));
        });

        private static void AddRankDefinition(Rig rig, RankType type)
        {
            RankDefinition definition = Create<RankDefinition>(rig.Owned, "{\"m_type\":" + (int)type + "}");
            Call(definition, "OnEnable"); // the wrappers Unity's own OnEnable builds, built again so they are certain to exist
            rig.Icons[type] = new[] { NewSprite(rig.Owned), NewSprite(rig.Owned), NewSprite(rig.Owned) };
            Cache(definition.BaseAddressable, rig.Icons[type][0]);
            Cache(definition.BackgroundAddressable, rig.Icons[type][1]);
            Cache(definition.ForegroundAddressable, rig.Icons[type][2]);
            rig.Definitions[type] = definition;
            rig.Data.RankDefinitions[type] = definition;
        }

        private static MissionRank NewMissionRank(RankType type, int seconds)
        {
            var rank = new MissionRank();
            JsonUtility.FromJsonOverwrite("{\"m_rankType\":" + (int)type + ",\"m_seconds\":" + seconds + "}", rank);
            rank.Validate();
            return rank;
        }

        // A time-trial mission: a 90 second limit plus 5 seconds for the objectives, ranked S 30 s, A 45 s, B 60 s, C 75 s
        // and D 30 s, with a completed-mission override that does or does not fail on the time limit.
        private static MissionDefinition NewTimeTrial(Rig rig, bool failOnTimeLimit, bool sequenced)
        {
            var ranks = rig.Owned.Track(ScriptableObject.CreateInstance<MissionRankDefinition>());
            int[] seconds = { 30, 45, 60, 75, 30 };
            for (int i = 0; i < RankTypes.Length; i++) ranks.Ranks.Add(RankTypes[i], NewMissionRank(RankTypes[i], seconds[i]));
            var mission = Create<MissionDefinition>(rig.Owned,
                "{\"m_timeLimitSeconds\":90,\"m_objectivesAdditionalTotalTimeSeconds\":5,\"m_isInstancedSequencedTracker\":" + (sequenced ? "true" : "false") + "}");
            var overrides = Create<MissionDefinition>(rig.Owned, "{\"m_timeLimitSeconds\":90,\"m_failOnTimeLimitReached\":" + (failOnTimeLimit ? "true" : "false") + "}");
            Set(mission, "m_ranks", ranks); // Reflection: serialized references and the flag UpdateOverrides sets for a completed mission with overrides
            Set(mission, "m_completedMissionOverrides", overrides);
            Set(mission, "m_isBeingOverridden", true);
            return mission;
        }

        private static string[] RankTexts(UIContainerMissionRanks container) => new[]
        {
            container.SRank.Value, container.ARank.Value, container.BRank.Value, container.CRank.Value, container.DRank.Value,
        };

        private static Sprite[][] ShownIcons(UIContainerMissionRanks container) => new[]
        {
            new[] { container.SRankIconBase.Value, container.SRankIconBackground.Value, container.SRankIconForeground.Value },
            new[] { container.ARankIconBase.Value, container.ARankIconBackground.Value, container.ARankIconForeground.Value },
            new[] { container.BRankIconBase.Value, container.BRankIconBackground.Value, container.BRankIconForeground.Value },
            new[] { container.CRankIconBase.Value, container.CRankIconBackground.Value, container.CRankIconForeground.Value },
            new[] { container.DRankIconBase.Value, container.DRankIconBackground.Value, container.DRankIconForeground.Value },
        };

        // Setup 0x63EE00..0x63F110: each rank shows its time, counted down from the total time (limit plus objective time)
        // when the override fails on the time limit (subss at 0x63F0F1), except the D rank, which keeps a time of zero
        // (cmp r14d, 0x5C4C95FB at 0x63F0D7). The D rank here is 30 s, so the exception is visible. The title shows for a
        // sequenced tracker only (cmp byte [rax+0xCD] at 0x63F1D1); the ranks show either way.
        [Test]
        public void TheRankTimesCountDownFromTheTotalTimeWhenTheMissionFailsOnTheTimeLimit()
        {
            foreach ((bool fail, bool sequenced, string[] texts) in new[]
            {
                (false, false, new[] { "0|30|00", "0|45|00", "1|00|00", "1|15|00", "-|--|--" }),
                (true, true, new[] { "1|05|00", "0|50|00", "0|35|00", "0|20|00", "-|--|--" }),
            })
            {
                Run(rig =>
                {
                    string row = "fail " + fail + ", sequenced " + sequenced;
                    MissionDefinition mission = NewTimeTrial(rig, fail, sequenced);

                    rig.Container.Setup(new UIContainerRanksParameters(mission, null));

                    Assert.That(RankTexts(rig.Container), Is.EqualTo(texts), row);
                    Assert.That(rig.Container.Show.Value, Is.True, row);
                    Assert.That(rig.Container.ShowTitle.Value, Is.EqualTo(sequenced), row);
                });
            }
        }

        // ShowRank 0x63F610: each rank type fills its own text and its three icons (base, background, foreground), loaded
        // from the DataManager's definition of that type and kept until OnDestroy 0x63ECB0 releases them.
        [Test]
        public void EachRankFillsItsOwnTextAndIconsWhichTheContainerReleasesWhenDestroyed()
        {
            Run(rig =>
            {
                rig.Container.Setup(new UIContainerRanksParameters(NewTimeTrial(rig, false, false), null));

                Sprite[][] shown = ShownIcons(rig.Container);
                for (int i = 0; i < RankTypes.Length; i++)
                    Assert.That(shown[i], Is.EqualTo(rig.Icons[RankTypes[i]]), RankTypes[i] + " icons");
                foreach (RankType type in RankTypes)
                    Assert.That(Count(rig.Definitions[type].BaseAddressable), Is.EqualTo(1), type + " base loaded once");

                Call(rig.Container, "OnDestroy"); // Reflection: Unity sends OnDestroy; Edit Mode does not

                foreach (RankType type in RankTypes)
                {
                    Assert.That(Count(rig.Definitions[type].BaseAddressable), Is.EqualTo(0), type + " base released");
                    Assert.That(Count(rig.Definitions[type].ForegroundAddressable), Is.EqualTo(0), type + " foreground released");
                }
            });
        }

        // A rank of a type with no field of its own loads its icons (they are fetched before the type is switched on) but
        // fills nothing and keeps nothing.
        [Test]
        public void ARankOfNoDisplayedTypeFillsNothing()
        {
            Run(rig =>
            {
                MissionRank rank = NewMissionRank(RankType.None, 10);

                Call(rig.Container, "ShowRank", rank, RankType.None, "ignored"); // Reflection: private UIContainerMissionRanks.ShowRank

                Assert.That(RankTexts(rig.Container), Is.EqualTo(new string[5]));
                foreach (Sprite[] icons in ShownIcons(rig.Container)) Assert.That(icons, Is.All.Null);
                Assert.That(Get<List<ManagedAddressableAsset<Sprite>>>(rig.Container, "m_managedAddressableSprites"), Is.Empty);
            });
        }

        // Setup 0x63EE00: a challenge is shown before the mission is looked at, with each rank's score as its text (ShowChallengeRanks
        // 0x63F290, Int32.ToString at 0x63F4D1) and no title. Only the challenge has ranks here.
        [Test]
        public void AChallengeShowsItsScoresWhateverTheMission()
        {
            Run(rig =>
            {
                var ranks = rig.Owned.Track(ScriptableObject.CreateInstance<ChallengeRankDefinition>());
                int[] scores = { 5000, 3000, 2000, 1000, 0 };
                for (int i = 0; i < RankTypes.Length; i++)
                {
                    var rank = new ChallengeRank();
                    JsonUtility.FromJsonOverwrite("{\"m_rankType\":" + (int)RankTypes[i] + ",\"m_score\":" + scores[i] + "}", rank);
                    ranks.Ranks.Add(RankTypes[i], rank);
                }
                var challenge = rig.Owned.Track(ScriptableObject.CreateInstance<ChallengeDefinition>());
                Set(challenge, "m_rankDefinition", ranks); // Reflection: serialized reference

                rig.Container.Setup(new UIContainerRanksParameters(NewTimeTrial(rig, false, true), challenge));

                Assert.That(RankTexts(rig.Container), Is.EqualTo(new[] { "5000", "3000", "2000", "1000", "0" }));
                Assert.That(rig.Container.Show.Value, Is.True);
                Assert.That(rig.Container.ShowTitle.Value, Is.False, "the title is a mission setting");
                Assert.That(ShownIcons(rig.Container)[0], Is.EqualTo(rig.Icons[RankType.S]));
            });
        }

        // Nothing to show hides the ranks: no mission, a mission that is not a time trial, a mission without rank definitions
        // (0x63EEE6..0x63EF38, then 0x63F11A) and a challenge without rank definitions (0x63F32C..0x63F358). The first write
        // to a binding always notifies, so the false is seen.
        [Test]
        public void WithoutRanksToShowTheContainerHidesThem()
        {
            foreach (string kind in new[] { "no mission", "not a time trial", "no rank definition", "challenge without ranks" })
            {
                Run(rig =>
                {
                    MissionDefinition mission = null;
                    ChallengeDefinition challenge = null;
                    if (kind == "not a time trial" || kind == "no rank definition")
                    {
                        mission = NewTimeTrial(rig, false, false);
                        if (kind == "not a time trial") Set(mission, "m_isBeingOverridden", false); // Reflection: set by UpdateOverrides
                        else Set(mission, "m_ranks", null);
                    }
                    if (kind == "challenge without ranks") challenge = rig.Owned.Track(ScriptableObject.CreateInstance<ChallengeDefinition>());
                    int notifications = 0;
                    rig.Container.Show.AddListener(() => notifications++);

                    rig.Container.Setup(new UIContainerRanksParameters(mission, challenge));

                    Assert.That(rig.Container.Show.Value, Is.False, kind);
                    Assert.That(notifications, Is.EqualTo(1), kind);
                    Assert.That(RankTexts(rig.Container), Is.EqualTo(new string[5]), kind);
                });
            }
        }
    }
}
