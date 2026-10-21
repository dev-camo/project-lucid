using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.UiWidgetImageFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerChallenge (Game.Runtime 0x0200087e, x86-64 0x61B1E0..0x61C3E0, parameters 0x0200087f) as a real component on an
    // inactive owned host over real challenge states, mission and challenge definitions, an owned string table and a DataManager
    // registered before the container caches its reference. Challenge states are built by their constructor and given their best
    // score and time through UpdateScoreInfo. The serialized fields are authored data.
    public sealed class UIContainerChallengeTests
    {
        private static readonly Regex RefusedDestroy = new Regex("^Destroy may not be called from edit mode");

        private const Strings TimeLabel = Strings.MENU_TAILS_CHALLENGES_LABEL_REWARD;
        private const Strings ScoreLabel = Strings.MENU_TAILS_CHALLENGES_LABEL_REWARD_XP;
        private const Strings RankXpFormat = Strings.MENU_TAILS_CHALLENGES_LABEL_COLLECT;
        private const Strings Description = Strings.MENU_TAILS_CHALLENGES_INFO_HEADER;

        private static readonly Dictionary<Strings, string> Texts = new Dictionary<Strings, string>
        {
            { TimeLabel, "Best time" },
            { ScoreLabel, "Best score" },
            { Strings.MENU_TAILS_CHALLENGES_XP, "{0} XP" },
            { RankXpFormat, "Rank +{0}" },
            { Strings.MISSION_TIMER_FORMAT_DESCRIPTION, "T{0}-{1}-{2}" },
            { Description, "Run: {0}/{1}" },
        };

        private sealed class Rig
        {
            public DataManager Data;
            public UIContainerChallenge Container;
            public UIWidgetCharacterHeadsSpawner Heads;
            public UIWidgetArchetypeImageSpawner Archetypes;
            public GameObject ArchetypeParent;
            public CharacterDefinition Amy, Cream, Sonic;
            public ChallengeRankDefinition Ranks;
            public Sprite[] RankSprites;

            public UIWidgetImageArchetype SpawnedArchetype => Get<UIWidgetImageArchetype>(Archetypes, "m_spawnedInstance");
            public List<UIWidgetButtonImageCharacter> HeadInstances => Get<List<UIWidgetButtonImageCharacter>>(Heads, "m_characterHeadsInstances");
        }

        // The DataManager is registered before the container (and the spawners) cache their references.
        private static void Run(Action<Owned, Rig> body, bool spawners = false) => OwnedStringTable.Run(Texts, () => Owned.Run(owned =>
        {
            var rig = new Rig();
            rig.Data = owned.Host("Lucid challenge data manager", false).AddComponent<DataManager>();
            owned.Register(rig.Data);
            owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
            rig.Container = owned.Host("Lucid challenge container", false).AddComponent<UIContainerChallenge>();
            Set(rig.Container, "m_bestTimeLabel", TimeLabel); // Reflection: serialized labels
            Set(rig.Container, "m_bestScoreLabel", ScoreLabel);
            Set(rig.Container, "m_xpFormatForRank", RankXpFormat);
            AddRanks(owned, rig);
            if (spawners) AddSpawners(owned, rig);
            body(owned, rig);
        }));

        // Ranks S (from 100 points) and A (from 50) over rank definitions whose three icon wrappers are loaded.
        private static void AddRanks(Owned owned, Rig rig)
        {
            rig.Ranks = Create<ChallengeRankDefinition>(owned);
            Set(rig.Ranks, "m_sRankBonus", 40); // Reflection: serialized bonus
            rig.RankSprites = new Sprite[6];
            int index = 0;
            foreach ((RankType type, int score) in new[] { (RankType.S, 100), (RankType.A, 50) })
            {
                var rank = new ChallengeRank();
                Set(rank, "m_rankType", type); // Reflection: serialized fields of the authored rank
                Set(rank, "m_score", score);
                rig.Ranks.Ranks[type] = rank;
                var definition = Create<RankDefinition>(owned, "{\"m_type\":" + (int)type + "}");
                Cache(definition.BaseAddressable, rig.RankSprites[index++] = NewSprite(owned));
                Cache(definition.BackgroundAddressable, rig.RankSprites[index++] = NewSprite(owned));
                Cache(definition.ForegroundAddressable, rig.RankSprites[index++] = NewSprite(owned));
                rig.Data.RankDefinitions[type] = definition;
            }
            Call(rig.Ranks, "OnEnable"); // the enable callback rebuilds the sorted list from the authored ranks
        }

        // Amy and Sonic are dashing characters and Cream a flying one; the data manager holds them in that order.
        private static void AddSpawners(Owned owned, Rig rig)
        {
            rig.Amy = NewCharacterDefinition(owned, CharacterId.Amy, out _, out _, CharacterArchetype.Dashing);
            rig.Cream = NewCharacterDefinition(owned, CharacterId.Cream, out _, out _, CharacterArchetype.Flying);
            rig.Sonic = NewCharacterDefinition(owned, CharacterId.Sonic, out _, out _, CharacterArchetype.Dashing);
            rig.Data.Characters[CharacterId.Amy] = rig.Amy;
            rig.Data.Characters[CharacterId.Cream] = rig.Cream;
            rig.Data.Characters[CharacterId.Sonic] = rig.Sonic;
            rig.Data.CharacterArchetypes[CharacterArchetype.Dashing] =
                NewArchetypeDefinition(owned, CharacterArchetype.Dashing, out _, out _, out _);

            GameObject headPrefabHost = owned.Host("Lucid challenge head prefab", false);
            var headPrefab = headPrefabHost.AddComponent<UIWidgetButtonImageCharacter>();
            Set(headPrefab, "m_swapIcon", owned.Host("Lucid challenge head swap icon", true, headPrefabHost.transform));
            rig.Heads = owned.Host("Lucid challenge heads spawner", false).AddComponent<UIWidgetCharacterHeadsSpawner>();
            Set(rig.Heads, "m_spawnParent", owned.Host("Lucid challenge heads parent", false).transform);
            Set(rig.Heads, "m_characterHeadPrefab", headPrefab);
            rig.Heads.Awake();

            rig.ArchetypeParent = owned.Host("Lucid challenge archetype parent", false);
            rig.Archetypes = owned.Host("Lucid challenge archetype spawner", false).AddComponent<UIWidgetArchetypeImageSpawner>();
            Set(rig.Archetypes, "m_spawnParent", rig.ArchetypeParent.transform);
            Set(rig.Archetypes, "m_prefab", owned.Host("Lucid challenge archetype prefab", false).AddComponent<UIWidgetImageArchetype>());

            Set(rig.Container, "m_characterHeadsSpawner", rig.Heads); // Reflection: serialized references
            Set(rig.Container, "m_archetypeSpawner", rig.Archetypes);
        }

        // A challenge over a mission with the given flags and a definition worth `xp`, whose best score and time are the given ones.
        private static ChallengeState NewState(Owned owned, Rig rig, int score, float time, bool timed = true, bool scored = true,
            int xp = 120, CharacterId[] characters = null, bool withRanks = false)
        {
            var mission = Create<MissionDefinition>(owned, "{\"m_isTimed\":" + (timed ? "true" : "false") + ",\"m_isScored\":" + (scored ? "true" : "false") + "}");
            var definition = Create<ChallengeDefinition>(owned,
                "{\"m_xpReward\":" + xp + ",\"m_timeLimitSeconds\":90,\"m_description\":" + (int)Description + "}");
            Set(definition, "m_mission", mission); // Reflection: serialized references and lists
            Set(definition, "m_characterSelection", new CharacterId[0]);
            Set(definition, "m_startPositions", new LevelStartPositionDefinition[0]);
            if (withRanks) Set(definition, "m_rankDefinition", rig.Ranks);
            var state = new ChallengeState(0, definition, 0, null, 0);
            if (characters != null)
            {
                state.ChallengeSeededData.CharacterList.AddRange(characters); // the seeded record's own list
            }
            state.OnAwardXP = (challenge, first, ringXp) => { };
            state.OnUpdateScore = challenge => { };
            state.UpdateScoreInfo(score, time, 0, true);
            state.OnUpdateScore = null;
            return state;
        }

        private static UIContainerChallenge.UIContainerChallengeParameters Parameters(ChallengeState state) =>
            new UIContainerChallenge.UIContainerChallengeParameters(state);

        private static void Show(Rig rig, ChallengeState state) => rig.Container.Setup(Parameters(state));

        // dump.cs TypeDefIndex 2174/2173 and the DummyDll rows (fields 0x04002111..0x0400212d, methods 0x0600311e..0x06003126).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            var serialized = new[] { "m_characterHeadsSpawner", "m_archetypeSpawner", "m_bestTimeLabel", "m_bestScoreLabel", "m_useXpPostfix",
                "m_xpFormat", "m_useXpPostfixForRank", "m_xpFormatForRank" };
            var bindables = new[] { "Description", "Completed", "XPReward", "IsTimed", "IsScored", "HighScore", "BestTimeString", "MissionIcon",
                "BestTimeLabel", "BestScoreLabel", "FormattedXP", "ShowRank", "FormattedRankXP", "RankBase", "RankBackground", "RankLetter",
                "SRankAchieved" };
            AssertMembers(typeof(UIContainerChallenge), 0x00100101,
                serialized.Select(name => (name, 0x0001))
                    .Concat(bindables.Select(name => (name, 0x0026)))
                    .Concat(new (string, int)[] { ("m_challengeState", 0x0001), ("m_managedAddressableSprites", 0x0021), ("m_dataManagerRef", 0x0021) })
                    .ToArray(),
                new (string, int)[]
                {
                    ("Setup", 0x00c6), ("OnChallengeStateUpdate", 0x0086), ("GetFormattedXpString", 0x0081), ("TryGetArchetype", 0x0081),
                    ("SetIcon", 0x0081), ("OnDestroy", 0x0081), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UIContainerChallenge), 2, 1);
            Assert.That(typeof(UIContainerChallenge).BaseType, Is.EqualTo(typeof(UIContainer)));
            AssertMembers(typeof(UIContainerChallenge.UIContainerChallengeParameters), 0x00100102,
                new (string, int)[] { ("<ChallengeState>k__BackingField", 0x0021) },
                new (string, int)[] { ("get_ChallengeState", 0x0886), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(UIContainerChallenge.UIContainerChallengeParameters), 2, 1);
            Assert.That(typeof(UIContainerChallenge.UIContainerChallengeParameters).GetInterfaces(), Does.Contain(typeof(IUIContainerParameters)));
            var format = typeof(UIContainerChallenge).GetField("m_xpFormat", Declared);
            Assert.That(Tooltip(format), Is.EqualTo("Format for XP display. Parameters: {0} XP reward amount."));
            Assert.That(Tooltip(typeof(UIContainerChallenge).GetField("m_xpFormatForRank", Declared)),
                Is.EqualTo("Format for rank XP display. Parameters: {0} XP reward amount."));
            var xpString = typeof(UIContainerChallenge).GetMethod("GetFormattedXpString", Declared).GetParameters();
            Assert.That(xpString[1].HasDefaultValue, Is.True);
            Assert.That(xpString[1].DefaultValue, Is.False);
        }

        // The constructor 0x61BF10 stores the XP format defaults (0x30e0029c, MENU_TAILS_CHALLENGES_XP) at +0x54 and +0x5C, creates the
        // seventeen bindables, an empty icon list and takes the DataManager reference.
        [Test]
        public void ConstructionSetsTheXpFormatDefaultsAndCreatesTheBindables()
        {
            Run((owned, rig) =>
            {
                var container = rig.Container;
                Assert.That(Get<Strings>(container, "m_xpFormat"), Is.EqualTo(Strings.MENU_TAILS_CHALLENGES_XP));
                Assert.That(Get<Strings>(container, "m_xpFormatForRank"), Is.EqualTo(RankXpFormat), "set by the case; the default is checked on a new component below");
                var fresh = owned.Host("Lucid challenge fresh container", false).AddComponent<UIContainerChallenge>();
                Assert.That(Get<Strings>(fresh, "m_xpFormat"), Is.EqualTo(Strings.MENU_TAILS_CHALLENGES_XP));
                Assert.That(Get<Strings>(fresh, "m_xpFormatForRank"), Is.EqualTo(Strings.MENU_TAILS_CHALLENGES_XP));
                Assert.That(Get<List<ManagedAddressableAsset<Sprite>>>(container, "m_managedAddressableSprites"), Is.Empty);
                Assert.That(Get<SystemRef<DataManager>>(container, "m_dataManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<DataManager>()));
                var bindables = new object[]
                {
                    container.Description, container.Completed, container.XPReward, container.IsTimed, container.IsScored, container.HighScore,
                    container.BestTimeString, container.MissionIcon, container.BestTimeLabel, container.BestScoreLabel, container.FormattedXP,
                    container.ShowRank, container.FormattedRankXP, container.RankBase, container.RankBackground, container.RankLetter,
                    container.SRankAchieved,
                };
                Assert.That(bindables, Has.All.Not.Null);
                Assert.That(bindables.Distinct().Count(), Is.EqualTo(17), "one bindable per field");
                Assert.That(container.Completed.Value, Is.False);
                Assert.That(container.HighScore.Value, Is.Zero);
            });
        }

        // Setup 0x61B1E0: the base setup, then both labels are localised (score label first, 0x61B26E, then time label 0x61B290),
        // then the container follows the challenge (its update handler joins OnUpdateScore) and shows it at once.
        [Test]
        public void SetupLocalisesTheLabelsAndFollowsTheChallenge()
        {
            Run((owned, rig) =>
            {
                ChallengeState state = NewState(owned, rig, 80, 83.25f);

                Show(rig, state);

                Assert.That(rig.Container.BestTimeLabel.Value, Is.EqualTo("Best time"));
                Assert.That(rig.Container.BestScoreLabel.Value, Is.EqualTo("Best score"));
                Assert.That(Get<ChallengeState>(rig.Container, "m_challengeState"), Is.SameAs(state));
                Assert.That(state.OnUpdateScore.GetInvocationList().Select(handler => handler.Target), Is.EqualTo(new object[] { rig.Container }));
                Assert.That(rig.Container.HighScore.Value, Is.EqualTo(80), "shown as soon as it is set");
            });
        }

        // A later score update reaches the shown challenge through the subscribed handler.
        [Test]
        public void AScoreUpdateOfTheShownChallengeRefreshesTheBindables()
        {
            Run((owned, rig) =>
            {
                ChallengeState state = NewState(owned, rig, 80, 83.25f);
                Show(rig, state);

                state.UpdateScoreInfo(130, 70f, 0, false);

                Assert.That(rig.Container.HighScore.Value, Is.EqualTo(130));
                Assert.That(rig.Container.BestTimeString.Value, Is.EqualTo("T1-10-00"));
            });
        }

        // Following another challenge first leaves the previous one (0x61B2C3..0x61B36F removes the handler before the new state is
        // stored), so its updates no longer reach the container; setting up with no challenge leaves the old one and follows nothing.
        [Test]
        public void SettingUpAgainUnfollowsThePreviousChallenge()
        {
            Run((owned, rig) =>
            {
                ChallengeState first = NewState(owned, rig, 10, 5f);
                ChallengeState second = NewState(owned, rig, 20, 6f);
                Show(rig, first);

                Show(rig, second);

                Assert.That(first.OnUpdateScore, Is.Null);
                Assert.That(second.OnUpdateScore.GetInvocationList().Select(handler => handler.Target), Is.EqualTo(new object[] { rig.Container }));
                Assert.That(rig.Container.HighScore.Value, Is.EqualTo(20));

                Show(rig, null);

                Assert.That(second.OnUpdateScore, Is.Null);
                Assert.That(Get<ChallengeState>(rig.Container, "m_challengeState"), Is.Null);
                Assert.That(rig.Container.HighScore.Value, Is.EqualTo(20), "nothing is shown for no challenge");
            });
        }

        // The parameters are cast after the labels are set (0x61B2AB..0x61B2BD): a foreign object faults, but the labels are already in.
        [Test]
        public void AForeignParametersObjectFaultsAfterTheLabelsAreSet()
        {
            Run((owned, rig) =>
            {
                Assert.Throws<InvalidCastException>(() => rig.Container.Setup(new UIContainerChallengeBonus.UIContainerChallengeBonusParameters(1, 2, true)));

                Assert.That(rig.Container.BestScoreLabel.Value, Is.EqualTo("Best score"));
                Assert.That(rig.Container.BestTimeLabel.Value, Is.EqualTo("Best time"));
                Assert.Throws<NullReferenceException>(() => rig.Container.Setup(null), "a null object passes the cast, then its challenge is read");
            });
        }

        // 0x61B4A2: an update for a challenge other than the one shown is ignored.
        [Test]
        public void AnUpdateForAnotherChallengeIsIgnored()
        {
            Run((owned, rig) =>
            {
                ChallengeState shown = NewState(owned, rig, 10, 5f);
                ChallengeState other = NewState(owned, rig, 99, 7f, xp: 500);
                Show(rig, shown);

                rig.Container.OnChallengeStateUpdate(other);

                Assert.That(rig.Container.HighScore.Value, Is.EqualTo(10));
                Assert.That(rig.Container.XPReward.Value, Is.EqualTo(120));
            });
        }

        // 0x61B4B3..0x61B5CE: the flags come from the mission (+0x7C, +0x7D), the XP from the definition (+0x70), the score and time from
        // the state; the mission icon is the mission's objective icon and the description the definition's formatted description.
        [Test]
        public void TheBindablesReadTheChallengeStateAndItsDefinitions()
        {
            Run((owned, rig) =>
            {
                ChallengeState state = NewState(owned, rig, 80, 83.25f, timed: true, scored: false, xp: 120);
                Sprite icon = NewSprite(owned);
                Set(state.ChallengeDefinition.MissionDefinition, "m_missionObjectiveIcon", icon); // Reflection: serialized icon

                Show(rig, state);

                Assert.That(rig.Container.IsTimed.Value, Is.True);
                Assert.That(rig.Container.IsScored.Value, Is.False);
                Assert.That(rig.Container.XPReward.Value, Is.EqualTo(120));
                Assert.That(rig.Container.HighScore.Value, Is.EqualTo(80));
                Assert.That(rig.Container.BestTimeString.Value, Is.EqualTo("T1-23-25"));
                Assert.That(rig.Container.MissionIcon.Value, Is.SameAs(icon));
                Assert.That(rig.Container.Description.Value, Is.EqualTo("Run: T1-30-00/"));
                Assert.That(rig.Container.FormattedXP.Value, Is.EqualTo("120"), "the postfix is off by default");
                Assert.That(rig.Container.ShowRank.Value, Is.False, "no rank definition is authored");

                ChallengeState untimed = NewState(owned, rig, 3, 0f, timed: false, scored: true);
                Show(rig, untimed);
                Assert.That(rig.Container.IsTimed.Value, Is.False);
                Assert.That(rig.Container.IsScored.Value, Is.True);
            });
        }

        // 0x61B4F2..0x61B503: completed is the strict test best time > 0 (ucomiss, seta), so a zero, negative or NaN time is not completed
        // even when a score was achieved.
        [TestCase(83.25f, true, TestName = "APositiveBestTimeIsCompleted")]
        [TestCase(0f, false, TestName = "AZeroBestTimeIsNotCompleted")]
        [TestCase(-1f, false, TestName = "ANegativeBestTimeIsNotCompleted")]
        [TestCase(float.NaN, false, TestName = "ANaNBestTimeIsNotCompleted")]
        [TestCase(float.Epsilon, true, TestName = "ATinyPositiveBestTimeIsCompleted")]
        public void CompletedIsAPositiveBestTime(float bestTime, bool expected)
        {
            Run((owned, rig) =>
            {
                ChallengeState state = NewState(owned, rig, 50, bestTime);

                Show(rig, state);

                Assert.That(rig.Container.Completed.Value, Is.EqualTo(expected));
            });
        }

        // GetFormattedXpString 0x61B8D0 (inlined for the XP at 0x61B5D4 and for the rank bonus at 0x61B7FE): the number alone while the
        // postfix option of that kind is off, otherwise the number inside the localised format. The two options are independent and
        // each selects its own format string (+0x50/+0x54, +0x58/+0x5C).
        [TestCase(false, false, false, "25", TestName = "TheXpIsPlainWithBothOptionsOff")]
        [TestCase(true, false, false, "25 XP", TestName = "TheXpUsesItsFormatWhenItsOptionIsOn")]
        [TestCase(false, true, false, "25", TestName = "TheRankOptionDoesNotFormatTheXp")]
        [TestCase(false, true, true, "Rank +25", TestName = "TheRankXpUsesTheRankFormatWhenItsOptionIsOn")]
        [TestCase(true, false, true, "25", TestName = "TheXpOptionDoesNotFormatTheRankXp")]
        public void TheFormattedXpFollowsItsOwnOption(bool useXpPostfix, bool useRankPostfix, bool forRank, string expected)
        {
            Run((owned, rig) =>
            {
                Set(rig.Container, "m_useXpPostfix", useXpPostfix); // Reflection: serialized options
                Set(rig.Container, "m_useXpPostfixForRank", useRankPostfix);

                Assert.That(Call(rig.Container, "GetFormattedXpString", 25, forRank), Is.EqualTo(expected));
            });
        }

        // The XP bindable is formatted by the XP option when the challenge is shown.
        [Test]
        public void TheFormattedXpBindableUsesTheXpFormatWhenEnabled()
        {
            Run((owned, rig) =>
            {
                Set(rig.Container, "m_useXpPostfix", true);

                Show(rig, NewState(owned, rig, 1, 1f, xp: 75));

                Assert.That(rig.Container.FormattedXP.Value, Is.EqualTo("75 XP"));
                Assert.That(rig.Container.XPReward.Value, Is.EqualTo(75), "the number bindable stays a number");
            });
        }

        // TryGetArchetype 0x61B960: an empty list is false with the none archetype; otherwise the first character's archetype is
        // written (0x61BAC8) before the rest are compared one by one, and the first mismatch ends it with false, leaving that
        // first archetype in the output (0x61BAD0 precedes the loop at 0x61BB0B).
        [TestCase(new CharacterId[0], false, CharacterArchetype.None, TestName = "NoCharactersGiveNoArchetype")]
        [TestCase(new[] { CharacterId.Amy }, true, CharacterArchetype.Dashing, TestName = "OneCharacterGivesItsArchetype")]
        [TestCase(new[] { CharacterId.Amy, CharacterId.Sonic }, true, CharacterArchetype.Dashing, TestName = "CharactersOfOneArchetypeGiveIt")]
        [TestCase(new[] { CharacterId.Amy, CharacterId.Cream }, false, CharacterArchetype.Dashing, TestName = "AMismatchAtTheSecondCharacterKeepsTheFirstArchetype")]
        [TestCase(new[] { CharacterId.Cream, CharacterId.Amy }, false, CharacterArchetype.Flying, TestName = "AMismatchReportsTheFirstCharactersArchetype")]
        [TestCase(new[] { CharacterId.Amy, CharacterId.Sonic, CharacterId.Cream }, false, CharacterArchetype.Dashing, TestName = "ALateMismatchIsStillFound")]
        public void AnArchetypeIsFoundOnlyWhenEveryCharacterSharesIt(CharacterId[] characters, bool expected, CharacterArchetype archetype)
        {
            Run((owned, rig) =>
            {
                var arguments = new object[] { characters, CharacterArchetype.Climbing };

                Assert.That(Call(rig.Container, "TryGetArchetype", arguments), Is.EqualTo(expected));
                Assert.That(arguments[1], Is.EqualTo(archetype));
            }, spawners: true);
        }

        // A character the data manager does not hold faults on the dictionary lookup, as the original indexer does.
        [Test]
        public void AnUnknownCharacterThrows()
        {
            Run((owned, rig) =>
                Assert.Throws<KeyNotFoundException>(() =>
                    Call(rig.Container, "TryGetArchetype", new object[] { new[] { CharacterId.Tails }, CharacterArchetype.None })), spawners: true);
        }

        // 0x61B65E..0x61B6FF with a spawner assigned: characters of one archetype clear the heads and show that archetype's image; a mixed
        // group (or none) shows no archetype image (the none archetype hides its parent) and the heads of its characters, in the data
        // manager's order. Going back to a uniform group clears the two heads again (each refused Destroy is logged in Edit Mode).
        [Test]
        public void TheCharactersAreShownAsOneArchetypeOrAsTheirOwnHeads()
        {
            Run((owned, rig) =>
            {
                rig.ArchetypeParent.SetActive(true);
                Show(rig, NewState(owned, rig, 1, 1f, characters: new[] { CharacterId.Cream, CharacterId.Amy }));

                Assert.That(rig.ArchetypeParent.activeSelf, Is.False);
                Assert.That(rig.SpawnedArchetype, Is.Null);
                Assert.That(rig.HeadInstances.Select(head => head.CharacterId).ToArray(), Is.EqualTo(new[] { CharacterId.Amy, CharacterId.Cream }));

                LogAssert.Expect(LogType.Error, RefusedDestroy);
                LogAssert.Expect(LogType.Error, RefusedDestroy);
                Show(rig, NewState(owned, rig, 1, 1f, characters: new[] { CharacterId.Amy, CharacterId.Sonic }));

                Assert.That(rig.ArchetypeParent.activeSelf, Is.True);
                Assert.That(rig.SpawnedArchetype, Is.Not.Null);
                Assert.That(rig.SpawnedArchetype.Archetype, Is.EqualTo(CharacterArchetype.Dashing));
                Assert.That(rig.HeadInstances, Is.Empty);
            }, spawners: true);
        }

        // With no characters the archetype image is hidden and no heads are spawned (an empty request spawns nothing).
        [Test]
        public void NoCharactersShowNeitherAnArchetypeNorHeads()
        {
            Run((owned, rig) =>
            {
                rig.ArchetypeParent.SetActive(true);

                Show(rig, NewState(owned, rig, 1, 1f, characters: new CharacterId[0]));

                Assert.That(rig.ArchetypeParent.activeSelf, Is.False);
                Assert.That(rig.SpawnedArchetype, Is.Null);
                Assert.That(rig.HeadInstances, Is.Empty);
            }, spawners: true);
        }

        // 0x61B65E: the whole character section is skipped without a heads spawner, even with an archetype spawner assigned.
        [Test]
        public void WithoutAHeadsSpawnerTheArchetypeSpawnerIsLeftAlone()
        {
            Run((owned, rig) =>
            {
                Set(rig.Container, "m_characterHeadsSpawner", null);
                rig.ArchetypeParent.SetActive(true);

                Show(rig, NewState(owned, rig, 1, 1f, characters: new[] { CharacterId.Amy, CharacterId.Sonic }));

                Assert.That(rig.ArchetypeParent.activeSelf, Is.True);
                Assert.That(rig.SpawnedArchetype, Is.Null);
            }, spawners: true);
        }

        // 0x61B6FF..0x61B8BE: the rank is hidden at first; a never-played challenge (no time and no score) keeps it hidden, and so does a
        // challenge without a rank definition. Both leave the rank bindables untouched.
        [Test]
        public void ANeverPlayedChallengeOrOneWithoutRanksShowsNoRank()
        {
            Run((owned, rig) =>
            {
                rig.Container.ShowRank.Value = true;
                Show(rig, NewState(owned, rig, 0, 0f, withRanks: true));
                Assert.That(rig.Container.ShowRank.Value, Is.False);
                Assert.That(rig.Container.RankBase.Value, Is.Null);

                rig.Container.ShowRank.Value = true;
                Show(rig, NewState(owned, rig, 500, 0f, withRanks: false));
                Assert.That(rig.Container.ShowRank.Value, Is.False, "played, but no rank definition");
                Assert.That(rig.Container.RankBase.Value, Is.Null);
            });
        }

        // The rank is chosen by the best score (TryGetRankForScore, 0x61B772): S from 100, A from 50, and below every threshold the
        // definition has no fallback rank here, so none is shown. The three images are the rank definition's base, background and
        // foreground sprites (RankBase, RankBackground, RankLetter), the rank bonus is formatted with the rank format and the S flag is the
        // rank type test (0xdf9f103c is RankType.S). A score of exactly the threshold earns the rank.
        [TestCase(120, true, 0, true, TestName = "AScoreAboveTheSThresholdEarnsTheSRank")]
        [TestCase(100, true, 0, true, TestName = "AScoreAtTheSThresholdEarnsTheSRank")]
        [TestCase(99, true, 3, false, TestName = "AScoreJustBelowTheSThresholdEarnsTheARank")]
        [TestCase(50, true, 3, false, TestName = "AScoreAtTheAThresholdEarnsTheARank")]
        [TestCase(49, false, -1, false, TestName = "AScoreBelowEveryThresholdEarnsNoRank")]
        public void TheRankFollowsTheBestScore(int score, bool shown, int spriteIndex, bool sRank)
        {
            Run((owned, rig) =>
            {
                Set(rig.Container, "m_useXpPostfixForRank", true);
                rig.Container.SRankAchieved.Value = !sRank;

                Show(rig, NewState(owned, rig, score, 0f, withRanks: true));

                Assert.That(rig.Container.ShowRank.Value, Is.EqualTo(shown));
                if (!shown)
                {
                    Assert.That(rig.Container.RankBase.Value, Is.Null);
                    Assert.That(rig.Container.FormattedRankXP.Value, Is.Null);
                    Assert.That(rig.Container.SRankAchieved.Value, Is.EqualTo(!sRank), "not written for a hidden rank");
                    return;
                }
                Assert.That(rig.Container.RankBase.Value, Is.SameAs(rig.RankSprites[spriteIndex]));
                Assert.That(rig.Container.RankBackground.Value, Is.SameAs(rig.RankSprites[spriteIndex + 1]));
                Assert.That(rig.Container.RankLetter.Value, Is.SameAs(rig.RankSprites[spriteIndex + 2]));
                Assert.That(rig.Container.FormattedRankXP.Value, Is.EqualTo("Rank +40"));
                Assert.That(rig.Container.SRankAchieved.Value, Is.EqualTo(sRank));
            });
        }

        // SetIcon 0x61BC00 loads each icon once per update and keeps the wrapper, and OnDestroy 0x61BCE0 unsubscribes from the shown
        // challenge and unloads every wrapper kept (the list is cleared afterwards).
        [Test]
        public void DestroyingUnloadsTheLoadedIconsAndUnfollowsTheChallenge()
        {
            Run((owned, rig) =>
            {
                ChallengeState state = NewState(owned, rig, 120, 0f, withRanks: true);
                Show(rig, state);
                var kept = Get<List<ManagedAddressableAsset<Sprite>>>(rig.Container, "m_managedAddressableSprites");
                Assert.That(kept.Count, Is.EqualTo(3));
                Assert.That(kept.Select(Count).ToArray(), Is.EqualTo(new[] { 1, 1, 1 }));

                Call(rig.Container, "OnDestroy");

                Assert.That(kept, Is.Empty);
                Assert.That(state.OnUpdateScore, Is.Null);
                Assert.That(rig.Container.RankBase.Value, Is.SameAs(rig.RankSprites[0]), "the bindables keep the sprites");
            });
        }

        // Each update loads the icons again and keeps one more wrapper per icon, so one OnDestroy releases every load.
        [Test]
        public void EveryUpdateKeepsAnotherLoadOfEachIcon()
        {
            Run((owned, rig) =>
            {
                ChallengeState state = NewState(owned, rig, 120, 0f, withRanks: true);
                Show(rig, state);

                rig.Container.OnChallengeStateUpdate(state);

                var kept = Get<List<ManagedAddressableAsset<Sprite>>>(rig.Container, "m_managedAddressableSprites");
                Assert.That(kept.Count, Is.EqualTo(6));
                Call(rig.Container, "OnDestroy");
                Assert.That(kept, Is.Empty);
            });
        }

        // OnDestroy without a challenge touches only the icon list.
        [Test]
        public void DestroyingWithoutAChallengeDoesNothingElse()
        {
            Run((owned, rig) => Assert.DoesNotThrow(() => Call(rig.Container, "OnDestroy")));
        }
    }
}
