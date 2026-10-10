using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Game02000570: complete original seventeen-method owner, nineteen fields,
    // two nested enums and the complete nine-method serialized seeded record.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "ChallengeDefinition", menuName = "HardlightProject/DefinitionData/Definitions/ChallengeDefinition")]
    public sealed class ChallengeDefinition : ScriptableObjectWithGuid
    {
        [Header("Core challenge info")]
        [SerializeField] private MissionDefinition m_mission;
        [SerializeField] private GameplayLevelDefinition m_level;
        [Tooltip("Use {0} in the string content to insert the time limit, and {1} to insert the objective count. If left empty, mission name will be used by default.")]
        [SerializeField]
        [HashEnum(typeof(Strings))] private Strings m_description;
        [SerializeField] private ChallengeRankDefinition m_rankDefinition;
        [Header("Seed override")]
        [SerializeField]
        [Tooltip("Tick to manually provide a seed used to generate randomised data. Otherwise the seed will be based on the cycle position this challenge appears in and could change.")]
        private bool m_overrideSeed;
        [ShowIf("m_overrideSeed", null)]
        [SerializeField] private int m_seed;
        [SerializeField] private ChallengeSeededData m_seededData;
        [Tooltip("Seeded random selection of starting points in the level. If empty, the start position in the mission definition is used.")]
        [SerializeField] private LevelStartPositionDefinition[] m_startPositions;
        [SerializeField]
        [Header("Objectives")] private bool m_randomiseObjectives;
        [Min(1f)]
        [SerializeField]
        [ShowIf("m_randomiseObjectives", null)] private int m_objectivesToChoose = 1;
        [SerializeField]
        [Header("Time and score")]
        [Min(1f)] private int m_timeLimitSeconds;
        [Tooltip("Score (Y) that should be given when completing the challenge with (X) seconds remaining. Fractional score will be rounded up, and score is clamped to always be >= 1.")]
        [SerializeField] private AnimationCurve m_timeRemainingScoreCurve;
        [SerializeField]
        [Min(1f)] private int m_xpReward;
        [SerializeField]
        [Header("Character selection")] private SelectionType m_characterSelectionType;
        [SerializeField] private CharacterRestrictionType m_characterRestriction;
        [SerializeField] private CharacterId[] m_characterSelection;
        [SerializeField] private CharacterArchetype[] m_characterArchetypes;
        [SerializeField] private PerformanceAttribute m_performanceAttribute;
        [InspectorReadOnly]
        [SerializeField] private int m_randomisingDataHash;

        public enum CharacterRestrictionType { CharacterId = 0, CharacterArchetype = 1 }
        public enum SelectionType { SeededRandom = 0, PlayerChoice = 1 }

        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public sealed class ChallengeSeededData
        {
            [SerializeField] private List<CharacterId> m_characterList;
            [SerializeField] private List<int> m_objectiveIndices;
            [SerializeField] private LevelStartPositionDefinition m_startPosition;
            [SerializeField] private int m_seedUsed = int.MinValue;
            [SerializeField] private int m_definitionHash;

            //06001d6d/6e: each list is allocated lazily and the stored identity is
            //returned. The original implicit ctor06001d75 sets only SeedUsed.
            public List<CharacterId> CharacterList
            {
                get
                {
                    if (m_characterList == null) m_characterList = new List<CharacterId>();
                    return m_characterList;
                }
            }
            public List<int> ObjectiveIndices
            {
                get
                {
                    if (m_objectiveIndices == null) m_objectiveIndices = new List<int>();
                    return m_objectiveIndices;
                }
            }
            //06001d6f/70,71/72,73/74: original named fields, no backing fields.
            public LevelStartPositionDefinition StartPosition { get => m_startPosition; set => m_startPosition = value; }
            public int SeedUsed { get => m_seedUsed; set => m_seedUsed = value; }
            public int DefinitionHash { get => m_definitionHash; set => m_definitionHash = value; }
        }

        //06001d5c/5d/5e.
        public MissionDefinition MissionDefinition => m_mission;
        public GameplayLevelDefinition GameplayLevelDefinition => m_level;
        public ChallengeRankDefinition RankDefinition => m_rankDefinition;
        //06001d5f: mission count is read before the randomisation flag.
        public int ObjectiveCount
        {
            get
            {
                int total = m_mission.TotalObjectiveCount;
                return m_randomiseObjectives ? Math.Min(total, m_objectivesToChoose) : total;
            }
        }
        //06001d60: the native test is literal zero; actual Strings.NONE is
        //-1225125244. Preserve the original zero sentinel without normalising it.
        public Strings Description => m_description != (Strings)0 ? m_description : m_mission.MissionName;
        //06001d61/62/63.
        public int TimeLimitSeconds => m_timeLimitSeconds;
        public int XPReward => m_xpReward;
        public PerformanceAttribute PerformanceAttribute => m_performanceAttribute;

        //06001d64: non-overridden requests are fresh. The overridden cache tests
        //only the seed; a changed definition hash does not invalidate it.
        public ChallengeSeededData GetSeededData(int seed = 0)
        {
            if (!m_overrideSeed) return GenerateChallengeSeededData(seed);
            if (m_seededData == null || m_seededData.SeedUsed != m_seed)
                m_seededData = GenerateChallengeSeededData(m_seed);
            return m_seededData;
        }

        //06001d65: retain random construction, seeded record writes, character
        //generation, objective generation, then start-position selection.
        private ChallengeSeededData GenerateChallengeSeededData(int seed)
        {
            var seededRandom = new System.Random(seed);
            var challengeSeededData = new ChallengeSeededData
            {
                SeedUsed = seed,
                DefinitionHash = m_randomisingDataHash
            };
            GenerateCharacterList(seededRandom, challengeSeededData);
            GenerateMissionObjectiveIndices(seededRandom, challengeSeededData);
            challengeSeededData.StartPosition = GenerateStartPosition(seededRandom);
            return challengeSeededData;
        }

        //06001d66: explicit regeneration does not test m_overrideSeed.
        private void RegenerateCachedSeededData() => m_seededData = GenerateChallengeSeededData(m_seed);

        //06001d67: the by-reference provider receives a local list variable. The
        //original never writes a replaced local back into the seeded record.
        private void GenerateCharacterList(System.Random seededRandom, ChallengeSeededData challengeSeededData)
        {
            List<CharacterId> characters = challengeSeededData.CharacterList;
            characters.Clear();
            if (m_characterRestriction == CharacterRestrictionType.CharacterId)
            {
                if (m_characterSelectionType == SelectionType.PlayerChoice)
                    characters.AddRange(m_characterSelection);
                else if (m_characterSelection.Length > 0)
                    characters.Add(m_characterSelection[seededRandom.Next(m_characterSelection.Length)]);
            }
            else if (m_characterSelectionType == SelectionType.PlayerChoice)
            {
                foreach (CharacterArchetype archetype in m_characterArchetypes)
                    CharacterManager.GetCharacterIdsForArchetype(archetype, ref characters, false);
            }
            else if (m_characterArchetypes.Length > 0)
            {
                CharacterManager.GetCharacterIdsForArchetype(
                    m_characterArchetypes[seededRandom.Next(m_characterArchetypes.Length)], ref characters, false);
            }
        }

        //06001d68: a single start position consumes no random draw. A null
        //array retains its original failure; only an empty array uses the mission.
        private LevelStartPositionDefinition GenerateStartPosition(System.Random seededRandom)
        {
            if (m_startPositions.Length == 0) return m_mission.CustomStartPosition;
            if (m_startPositions.Length == 1) return m_startPositions[0];
            return m_startPositions[seededRandom.Next(m_startPositions.Length)];
        }

        //06001d69: the list stays empty in ordinary/all-objectives mode. Only a
        //randomised proper subset is populated, shuffled, shortened, then sorted.
        //The original does not validate a negative objective count.
        private void GenerateMissionObjectiveIndices(System.Random seededRandom, ChallengeSeededData challengeSeededData)
        {
            List<int> indices = challengeSeededData.ObjectiveIndices;
            indices.Clear();
            int total = m_mission.TotalObjectiveCount;
            int selected = ObjectiveCount;
            int removed = unchecked(total - selected);
            if (m_randomiseObjectives && removed != 0)
            {
                for (int i = 0; i < total; ++i) indices.Add(i);
                indices.Shuffle(seededRandom);
                indices.RemoveRange(selected, removed);
                indices.Sort();
            }
        }

        //06001d6a: description lookup precedes time/count evaluation.
        public string GetDescription() => MissionStringsUtil.GetMissionDescription(Description, m_timeLimitSeconds, ObjectiveCount);

        //06001d6b: use the actual Unity clamp/evaluate/ceil/max operations. Their
        //exceptional-float native conversion behavior is architecture dependent;
        //no native equivalence is claimed for NaN, infinities or out-of-range casts.
        public int CalculateScore(float timeElapsedSeconds)
        {
            float totalTime = m_timeLimitSeconds + m_mission.ObjectivesAdditionalTotalTimeSeconds;
            float remaining = totalTime - Mathf.Clamp(timeElapsedSeconds, 0f, totalTime);
            return Mathf.Max(1, Mathf.CeilToInt(m_timeRemainingScoreCurve.Evaluate(remaining)));
        }

        //Implicit ctor06001d6c: only m_objectivesToChoose = 1 precedes base ctor.
    }
}
