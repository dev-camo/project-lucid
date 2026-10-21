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
    // UIContainerChallengeResults (Game.Runtime 0x02000885; Setup 0x61CF10, SetIcon 0x61D420, OnDisable 0x61D500, constructor
    // 0x61D670) and its parameters (0x02000886, constructor 0x61DA10) over a real challenge definition. The challenge scores
    // ceil(10 * seconds remaining + 0.5) out of a 100 second limit, so a run of 40 s scores 601, 70 s scores 301, 20 s scores 801.
    // The formats are "{0} XP", "Earned {0}", "Challenge {0}" and "{0}:{1}:{2}". Not covered: rank icons that are not already
    // loaded (the wrapper's uncached path starts an Addressables load).
    public sealed class UIContainerChallengeResultsTests
    {
        private static readonly Dictionary<Strings, string> OwnedStrings = new Dictionary<Strings, string>
        {
            { Strings.MENU_TAILS_CHALLENGES_XP, "{0} XP" },
            { Strings.CHALLENGES_RESULT_XP_DESCRIPTION, "Earned {0}" },
            { Strings.CHALLENGES_RESULT_TITLE_CHALLENGE, "Challenge {0}" },
            { Strings.CHALLENGE_BEST_TIME_FORMAT, "{0}:{1}:{2}" },
        };

        private sealed class Rig
        {
            internal Owned Owned;
            internal DataManager Data;
            internal ChallengeDefinition Challenge;
            internal UIContainerChallengeResults Container;
        }

        private static void Run(Action<Rig> body) => WithWorld(world =>
        {
            Owned owned = world.Owned;
            var rig = new Rig { Owned = owned };
            rig.Data = owned.Register(owned.Host("Lucid data manager", false).AddComponent<DataManager>());
            rig.Challenge = Create<ChallengeDefinition>(owned, "{\"m_timeLimitSeconds\":100,\"m_xpReward\":25}");
            Set(rig.Challenge, "m_mission", Create<MissionDefinition>(owned)); // Reflection: serialized reference; no extra objective time
            Set(rig.Challenge, "m_timeRemainingScoreCurve", AnimationCurve.Linear(0f, 0.5f, 100f, 1000.5f)); // Reflection: authored curve
            rig.Container = owned.Host("Lucid challenge results", false).AddComponent<UIContainerChallengeResults>();
            OwnedStringTable.Run(OwnedStrings, () => body(rig));
        });

        private static UIContainerChallengeResults.UIContainerChallengeResultsParameters NewParameters(Rig rig, float previousBest, float time, int bonusScore) =>
            new UIContainerChallengeResults.UIContainerChallengeResultsParameters
            {
                ChallengeDefinition = rig.Challenge,
                PreviousBestTimeSeconds = previousBest,
                IsTimed = true,
                TimeSeconds = time,
                IsScored = true,
                Score = bonusScore,
                ChallengeIndex = 3,
            };

        // The constructor stores true at +0xB0 and the doublewords at +0xB4 from the read-only data at 0x29A7AC0, which hold the
        // strings 0x30E0029C, 0x84B9950F, 0xDEB5EE97 and 0xE141A380.
        [Test]
        public void DeclarationKeepsTheAuthoredDefaults()
        {
            Run(rig =>
            {
                Assert.That(Get<bool>(rig.Container, "m_showBestInsteadOfPrevious"), Is.True);
                Assert.That((int)Get<Strings>(rig.Container, "m_xpStringFormat"), Is.EqualTo(819987100));
                Assert.That((int)Get<Strings>(rig.Container, "m_xpStringFormatLong"), Is.EqualTo(-2068212465));
                Assert.That((int)Get<Strings>(rig.Container, "m_missionTimerFormat"), Is.EqualTo(-558502249));
                Assert.That((int)Get<Strings>(rig.Container, "m_challengeTitleFormat"), Is.EqualTo(-2136784415));
                Assert.That(Get<List<ManagedAddressableAsset<Sprite>>>(rig.Container, "m_managedAddressableSprites"), Is.Empty);
            });
        }

        // Setup 0x61CF10: with no previous best (a previous best time of zero, 0x61D002 setbe) the run is a new time and score
        // record, the XP is shown, and with m_showBestInsteadOfPrevious (0x61D1D0) the previous bests repeat the new values.
        [Test]
        public void AFirstCompletionIsARecordThatAwardsXpAndRepeatsTheNewValuesAsThePreviousBest()
        {
            Run(rig =>
            {
                var container = rig.Container;

                container.Setup(NewParameters(rig, 0f, 40f, 5));

                Assert.That(container.IsScored.Value, Is.True);
                Assert.That(container.IsTimed.Value, Is.True);
                Assert.That(container.IsXPAwarded.Value, Is.True);
                Assert.That(container.XPAwardedString.Value, Is.EqualTo("25 XP"));
                Assert.That(container.XPAwardedLongString.Value, Is.EqualTo("Earned 25"));
                Assert.That(container.ChallengeTitle.Value, Is.EqualTo("Challenge 3"));
                Assert.That(container.TimeString.Value, Is.EqualTo("0:40:00"));
                Assert.That(container.PreviousBestTimeString.Value, Is.EqualTo("0:40:00"));
                Assert.That(container.ScoreString.Value, Is.EqualTo("606"));
                Assert.That(container.PreviousBestScoreString.Value, Is.EqualTo("606"));
                Assert.That(container.IsNewRecord.Value, Is.True);
                Assert.That(container.ShowRanks.Value, Is.False, "the challenge has no rank definition");
            });
        }

        // Without showing the best, the previous best of a first completion is the formatted time of zero ("-", "--", "--") and
        // the score zero (0x61D22D stores zero before the score is computed).
        [Test]
        public void AFirstCompletionKeepsZeroAsThePreviousBestWhenTheBestIsNotShown()
        {
            Run(rig =>
            {
                Set(rig.Container, "m_showBestInsteadOfPrevious", false); // Reflection: serialized option

                rig.Container.Setup(NewParameters(rig, 0f, 40f, 5));

                Assert.That(rig.Container.PreviousBestTimeString.Value, Is.EqualTo("-:--:--"));
                Assert.That(rig.Container.PreviousBestScoreString.Value, Is.EqualTo("0"));
                Assert.That(rig.Container.IsNewRecord.Value, Is.True);
            });
        }

        // A slower run is no record: the XP is not awarded (the strings are empty, 0x61D06F), the previous best time is the
        // formatted previous time, and the previous best score is the score of the previous time alone (0x61D250: the bonus
        // score is added to the current run only), here 601 where adding the bonus would give 606.
        [Test]
        public void ASlowerRunIsNoRecordAndTheOldScoreCarriesNoBonus()
        {
            Run(rig =>
            {
                rig.Container.Setup(NewParameters(rig, 40f, 70f, 5));

                Assert.That(rig.Container.IsXPAwarded.Value, Is.False);
                Assert.That(rig.Container.XPAwardedString.Value, Is.EqualTo(string.Empty));
                Assert.That(rig.Container.XPAwardedLongString.Value, Is.EqualTo(string.Empty));
                Assert.That(rig.Container.TimeString.Value, Is.EqualTo("1:10:00"));
                Assert.That(rig.Container.PreviousBestTimeString.Value, Is.EqualTo("0:40:00"));
                Assert.That(rig.Container.ScoreString.Value, Is.EqualTo("306"));
                Assert.That(rig.Container.PreviousBestScoreString.Value, Is.EqualTo("601"));
                Assert.That(rig.Container.IsNewRecord.Value, Is.False);
            });
        }

        // The time record and the score record are decided separately and either one makes a new record (or at 0x61D305): a faster
        // run with a large penalty is a new time but not a new score, and the previous best time repeats the new time while the
        // previous best score stays the old one.
        [Test]
        public void ATimeRecordWithoutAScoreRecordIsStillANewRecord()
        {
            Run(rig =>
            {
                rig.Container.Setup(NewParameters(rig, 40f, 20f, -1000));

                Assert.That(rig.Container.TimeString.Value, Is.EqualTo("0:20:00"));
                Assert.That(rig.Container.PreviousBestTimeString.Value, Is.EqualTo("0:20:00"));
                Assert.That(rig.Container.ScoreString.Value, Is.EqualTo("-199"));
                Assert.That(rig.Container.PreviousBestScoreString.Value, Is.EqualTo("601"));
                Assert.That(rig.Container.IsNewRecord.Value, Is.True);
            });
        }

        // Both records need a strict improvement (ja at 0x61D1A0 and setg at 0x61D285): repeating the previous time and score is no
        // record.
        [Test]
        public void RepeatingThePreviousTimeAndScoreIsNoRecord()
        {
            Run(rig =>
            {
                rig.Container.Setup(NewParameters(rig, 40f, 40f, 0));

                Assert.That(rig.Container.IsNewRecord.Value, Is.False);
                Assert.That(rig.Container.ScoreString.Value, Is.EqualTo("601"));
                Assert.That(rig.Container.PreviousBestScoreString.Value, Is.EqualTo("601"));
                Assert.That(rig.Container.PreviousBestTimeString.Value, Is.EqualTo("0:40:00"));
            });
        }

        // The rank is looked up for the new score (0x61D376, 606 here): the best rank whose score it reaches is shown, each of its
        // three icons loaded once from the data manager's definition of that rank type (SetIcon 0x61D420) and released, with
        // the list cleared, when the container is disabled (OnDisable 0x61D500).
        [Test]
        public void TheRankForTheNewScoreLoadsItsIconsWhichTheContainerReleasesWhenDisabled()
        {
            Run(rig =>
            {
                var ranks = rig.Owned.Track(ScriptableObject.CreateInstance<ChallengeRankDefinition>());
                RankType[] types = { RankType.S, RankType.A, RankType.D };
                int[] scores = { 700, 400, 0 };
                var definitions = new Dictionary<RankType, RankDefinition>();
                var icons = new Dictionary<RankType, Sprite[]>();
                for (int i = 0; i < types.Length; i++)
                {
                    var rank = new ChallengeRank();
                    JsonUtility.FromJsonOverwrite("{\"m_rankType\":" + (int)types[i] + ",\"m_score\":" + scores[i] + "}", rank);
                    ranks.Ranks.Add(types[i], rank);
                    RankDefinition definition = Create<RankDefinition>(rig.Owned, "{\"m_type\":" + (int)types[i] + "}");
                    Call(definition, "OnEnable"); // the wrappers Unity's own OnEnable builds, built again so they are certain to exist
                    icons[types[i]] = new[] { NewSprite(rig.Owned), NewSprite(rig.Owned), NewSprite(rig.Owned) };
                    Cache(definition.BaseAddressable, icons[types[i]][0]);
                    Cache(definition.BackgroundAddressable, icons[types[i]][1]);
                    Cache(definition.ForegroundAddressable, icons[types[i]][2]);
                    definitions[types[i]] = definition;
                    rig.Data.RankDefinitions[types[i]] = definition;
                }
                Call(ranks, "OnEnable"); // rebuilds the sorted rank cache from the ranks added above
                Set(rig.Challenge, "m_rankDefinition", ranks); // Reflection: serialized reference

                rig.Container.Setup(NewParameters(rig, 0f, 40f, 5));

                Assert.That(rig.Container.ShowRanks.Value, Is.True);
                Assert.That(rig.Container.RankBase.Value, Is.SameAs(icons[RankType.A][0]));
                Assert.That(rig.Container.RankBadge.Value, Is.SameAs(icons[RankType.A][1]), "the badge is the rank's background icon");
                Assert.That(rig.Container.RankLetter.Value, Is.SameAs(icons[RankType.A][2]), "the letter is the rank's foreground icon");
                Assert.That(Count(definitions[RankType.A].BaseAddressable), Is.EqualTo(1));
                Assert.That(Count(definitions[RankType.S].BaseAddressable), Is.EqualTo(0));
                Assert.That(Get<List<ManagedAddressableAsset<Sprite>>>(rig.Container, "m_managedAddressableSprites"), Has.Count.EqualTo(3));

                Call(rig.Container, "OnDisable");

                Assert.That(Count(definitions[RankType.A].BaseAddressable), Is.EqualTo(0));
                Assert.That(Count(definitions[RankType.A].BackgroundAddressable), Is.EqualTo(0));
                Assert.That(Count(definitions[RankType.A].ForegroundAddressable), Is.EqualTo(0));
                Assert.That(Get<List<ManagedAddressableAsset<Sprite>>>(rig.Container, "m_managedAddressableSprites"), Is.Empty);
            });
        }

        // A rank definition that has no rank for the score (no ranks at all) hides the ranks and loads nothing.
        [Test]
        public void AScoreWithoutARankHidesTheRanks()
        {
            Run(rig =>
            {
                var ranks = rig.Owned.Track(ScriptableObject.CreateInstance<ChallengeRankDefinition>());
                Call(ranks, "OnEnable");
                Set(rig.Challenge, "m_rankDefinition", ranks); // Reflection: serialized reference
                rig.Container.ShowRanks.Value = true;

                rig.Container.Setup(NewParameters(rig, 0f, 40f, 5));

                Assert.That(rig.Container.ShowRanks.Value, Is.False);
                Assert.That(rig.Container.RankBase.Value, Is.Null);
                Assert.That(Get<List<ManagedAddressableAsset<Sprite>>>(rig.Container, "m_managedAddressableSprites"), Is.Empty);
            });
        }
    }
}
