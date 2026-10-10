using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime SaveDataGame: complete 60 owner declarations and
    // eight deserialize callbacks (10 natural declarations). Private candidate;
    // complete genuine leaf providers and compiled native fidelity remain open.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataGame : SaveDataItem, ISerializationCallbackReceiver
    {
        [SerializeField]
        private long m_lastLevelVisitedAt;

        [SerializeField]
        private long m_lastRatingRequestTimestampSeconds;

        [SerializeField]
        private bool m_cutsceneSeenSaveMaintenancePerformed;

        [SerializeField]
        private string m_gameVersion;

        [SerializeField]
        private SaveDataChallengeRewardTrack m_adventureRewardTrack = new SaveDataChallengeRewardTrack(RewardTrackType.Adventure);

        [SerializeField]
        private List<SaveDataLevel> m_levels = new List<SaveDataLevel>();

        private Dictionary<string, SaveDataLevel> m_levelsByGuid = new Dictionary<string, SaveDataLevel>();

        [SerializeField]
        private List<SaveDataOrnament> m_ornaments = new List<SaveDataOrnament>();

        private Dictionary<string, SaveDataOrnament> m_ornamentsByGuid = new Dictionary<string, SaveDataOrnament>();

        [SerializeField]
        private List<SaveDataMusicTrack> m_musicTracks = new List<SaveDataMusicTrack>();

        private Dictionary<string, SaveDataMusicTrack> m_musicTracksByGUID = new Dictionary<string, SaveDataMusicTrack>();

        [SerializeField]
        private string m_lastLevelVisitedGUID;

        [SerializeField]
        private List<string> m_seenPromptGuids = new List<string>();

        [SerializeField]
        private List<string> m_seenCutsceneGuids = new List<string>();

        [SerializeField]
        private List<string> m_cutsceneIsNewGuids = new List<string>();

        [SerializeField]
        private List<string> m_zoneUnlockSeenGuids = new List<string>();

        [SerializeField]
        private List<SaveDataCharacterArchetype> m_characterArchetypes = new List<SaveDataCharacterArchetype>();

        private Dictionary<CharacterArchetype, SaveDataCharacterArchetype> m_saveDataByCharacterArchetype = new Dictionary<CharacterArchetype, SaveDataCharacterArchetype>(HardlightEnumComparers.CharacterArchetypeComparer);

        [SerializeField]
        private SaveDataChallenges m_challenges = new SaveDataChallenges();

        [SerializeField]
        private long m_lastSavedTimestampMs;

        [SerializeField]
        private string m_lastLevelSelectedGUID;

        [SerializeField]
        private List<SaveDataPlayerStat> m_playerStats = new List<SaveDataPlayerStat>();

        private Dictionary<SaveDataPlayerStat.Type, SaveDataPlayerStat> m_playerStatsByType = new Dictionary<SaveDataPlayerStat.Type, SaveDataPlayerStat>();

        [SerializeField]
        private SaveDataBonusZone1 m_bonusZone1;

        [SerializeField]
        private SaveDataCollectable m_blueCoins;

        [SerializeField]
        private SaveDataDreamPowerStore m_dreamPowerStore;

        [SerializeField]
        private List<SaveDataDreamPowerLoadout> m_dreamPowerLoadouts;

        private Dictionary<CharacterArchetype, SaveDataDreamPowerLoadout> m_dreamPowerLoadoutByArchetype = new Dictionary<CharacterArchetype, SaveDataDreamPowerLoadout>();

        [SerializeField]
        private List<SaveDataCharacterSkin> m_characterSkins = new List<SaveDataCharacterSkin>();

        private Dictionary<string, SaveDataCharacterSkin> m_characterSkinsByGUID = new Dictionary<string, SaveDataCharacterSkin>();

        [SerializeField]
        private List<SaveDataCharacterCustomisation> m_characterCustomisations = new List<SaveDataCharacterCustomisation>();

        private Dictionary<CharacterId, SaveDataCharacterCustomisation> m_customisationsByCharacter = new Dictionary<CharacterId, SaveDataCharacterCustomisation>();

        [SerializeField]
        private long m_lastDeletionTimestamp;

        // Game.Runtime.dll 0x06002bed.
        public IReadOnlyList<SaveDataLevel> Levels
        {
            get { return m_levels; }
        }

        // Game.Runtime.dll 0x06002bee.
        public IReadOnlyDictionary<string, SaveDataOrnament> OrnamentsByGuid
        {
            get { return m_ornamentsByGuid; }
        }

        // Game.Runtime.dll 0x06002bef.
        public string LastLevelVisitedGUID
        {
            get { return m_lastLevelVisitedGUID; }
        }

        // Game.Runtime.dll 0x06002bf0.
        public IReadOnlyList<string> SeenPromptGuids
        {
            get { return m_seenPromptGuids; }
        }

        // Game.Runtime.dll 0x06002bf1.
        public IReadOnlyList<string> SeenCutsceneGuids
        {
            get { return m_seenCutsceneGuids; }
        }

        // Game.Runtime.dll 0x06002bf2.
        public IReadOnlyList<string> CutsceneIsNewGuids
        {
            get { return m_cutsceneIsNewGuids; }
        }

        // Game.Runtime.dll 0x06002bf3.
        public IReadOnlyList<string> ZoneUnlockSeenGuids
        {
            get { return m_zoneUnlockSeenGuids; }
        }

        // Game.Runtime.dll 0x06002bf4.
        public SaveDataChallenges SaveDataChallenges
        {
            get { return m_challenges; }
        }

        // Game.Runtime.dll 0x06002bf5.
        public long LastSavedTimestampMs
        {
            get { return m_lastSavedTimestampMs; }
        }

        // Game.Runtime.dll 0x06002bf6.
        public string LastLevelSelectedGUID
        {
            get { return m_lastLevelSelectedGUID; }
        }

        // Game.Runtime.dll 0x06002bf7.
        public SaveDataBonusZone1 BonusZone1
        {
            get { return m_bonusZone1; }
        }

        // Game.Runtime.dll 0x06002bf8.
        public SaveDataCollectable BlueCoins
        {
            get { return m_blueCoins; }
        }

        // Game.Runtime.dll 0x06002bf9.
        public SaveDataDreamPowerStore DreamPowerStore
        {
            get { return m_dreamPowerStore; }
        }

        // Game.Runtime.dll 0x06002bfa.
        public IReadOnlyDictionary<CharacterArchetype, SaveDataDreamPowerLoadout> DreamPowerLoadoutByArchetype
        {
            get { return m_dreamPowerLoadoutByArchetype; }
        }

        // Game.Runtime.dll 0x06002bfb.
        public IReadOnlyDictionary<string, SaveDataCharacterSkin> CharacterSkinsByGUID
        {
            get { return m_characterSkinsByGUID; }
        }

        // Game.Runtime.dll 0x06002bfc.
        public IReadOnlyDictionary<CharacterId, SaveDataCharacterCustomisation> CustomisationsByCharacter
        {
            get { return m_customisationsByCharacter; }
        }

        // Game.Runtime.dll 0x06002bfd.
        public long LastRatingRequestTimestampSeconds
        {
            get { return m_lastRatingRequestTimestampSeconds; }
            // Game.Runtime.dll 0x06002bfe.
            set
            {
                m_lastRatingRequestTimestampSeconds = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002bff.
        public bool CutsceneSeenSaveMaintenance
        {
            get { return m_cutsceneSeenSaveMaintenancePerformed; }
            // Game.Runtime.dll 0x06002c00.
            set
            {
                m_cutsceneSeenSaveMaintenancePerformed = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c01.
        public string GameVersion
        {
            get { return m_gameVersion; }
            // Game.Runtime.dll 0x06002c02.
            set
            {
                m_gameVersion = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c03.
        public long LastDeletionTimestamp
        {
            get { return m_lastDeletionTimestamp; }
            // Game.Runtime.dll 0x06002c04.
            set
            {
                m_lastDeletionTimestamp = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c28.
        public SaveDataGame()
        {
        }

        // Game.Runtime.dll 0x06002c05.
        public override void Initialise()
        {
            if (m_levels == null) m_levels = new List<SaveDataLevel>();
            if (m_levelsByGuid == null) m_levelsByGuid = new Dictionary<string,SaveDataLevel>();
            if (m_ornaments == null) m_ornaments = new List<SaveDataOrnament>();
            if (m_ornamentsByGuid == null) m_ornamentsByGuid = new Dictionary<string,SaveDataOrnament>();
            if (m_musicTracks == null) m_musicTracks = new List<SaveDataMusicTrack>();
            if (m_musicTracksByGUID == null) m_musicTracksByGUID = new Dictionary<string,SaveDataMusicTrack>();
            if (m_characterArchetypes == null) m_characterArchetypes = new List<SaveDataCharacterArchetype>();
            if (m_saveDataByCharacterArchetype == null) m_saveDataByCharacterArchetype = new Dictionary<CharacterArchetype,SaveDataCharacterArchetype>(HardlightEnumComparers.CharacterArchetypeComparer);
            if (m_seenPromptGuids == null) m_seenPromptGuids = new List<string>();
            if (m_seenCutsceneGuids == null) m_seenCutsceneGuids = new List<string>();
            if (m_cutsceneIsNewGuids == null) m_cutsceneIsNewGuids = new List<string>();
            if (m_zoneUnlockSeenGuids == null) m_zoneUnlockSeenGuids = new List<string>();
            if (m_challenges == null) m_challenges = new SaveDataChallenges();
            if (m_adventureRewardTrack == null) m_adventureRewardTrack = new SaveDataChallengeRewardTrack(RewardTrackType.Adventure);
            if (m_bonusZone1 == null) m_bonusZone1 = new SaveDataBonusZone1();
            if (m_blueCoins == null) m_blueCoins = new SaveDataCollectable();
            if (m_dreamPowerStore == null) m_dreamPowerStore = new SaveDataDreamPowerStore();
            if (m_dreamPowerLoadouts == null) m_dreamPowerLoadouts = new List<SaveDataDreamPowerLoadout>();
            if (m_dreamPowerLoadoutByArchetype == null) m_dreamPowerLoadoutByArchetype = new Dictionary<CharacterArchetype,SaveDataDreamPowerLoadout>(HardlightEnumComparers.CharacterArchetypeComparer);
            if (m_playerStats == null) m_playerStats = new List<SaveDataPlayerStat>();
            if (m_playerStatsByType == null) m_playerStatsByType = new Dictionary<SaveDataPlayerStat.Type,SaveDataPlayerStat>();
            if (m_characterSkins == null) m_characterSkins = new List<SaveDataCharacterSkin>();
            if (m_characterSkinsByGUID == null) m_characterSkinsByGUID = new Dictionary<string,SaveDataCharacterSkin>();
            if (m_characterCustomisations == null) m_characterCustomisations = new List<SaveDataCharacterCustomisation>();
            if (m_customisationsByCharacter == null) m_customisationsByCharacter = new Dictionary<CharacterId,SaveDataCharacterCustomisation>(HardlightEnumComparers.CharacterIdComparer);
            base.Initialise();
        }

        // Game.Runtime.dll 0x06002c06.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            foreach (var pair in m_levelsByGuid) action(pair.Value);
            action(m_challenges);
            action(m_adventureRewardTrack);
            action(m_bonusZone1);
            action(m_blueCoins);
            action(m_dreamPowerStore);
            foreach (var pair in m_ornamentsByGuid) action(pair.Value);
            foreach (var pair in m_musicTracksByGUID) action(pair.Value);
            foreach (var pair in m_saveDataByCharacterArchetype) action(pair.Value);
            foreach (var pair in m_playerStatsByType) action(pair.Value);
            foreach (var pair in m_dreamPowerLoadoutByArchetype) action(pair.Value);
            foreach (var pair in m_characterSkinsByGUID) action(pair.Value);
            foreach (var pair in m_customisationsByCharacter) action(pair.Value);
        }

        // 0x06002c07. Incoming values are reused by reference on missing keys.
        // The original does not merge character customisations or rating time.
        // Parent dirty state is not set by this merge; child merge behavior is
        // provided by each genuine original leaf, never by property setters.
        public void ResolveNewData(SaveDataGame newSaveDataGame, bool cloudIsLatest)
        {
            foreach (KeyValuePair<string, SaveDataLevel> pair in newSaveDataGame.m_levelsByGuid)
                if (m_levelsByGuid.TryGetValue(pair.Key, out SaveDataLevel old)) old.ResolveNewData(pair.Value);
                else m_levelsByGuid[pair.Key] = pair.Value;
            foreach (KeyValuePair<string, SaveDataOrnament> pair in newSaveDataGame.m_ornamentsByGuid)
                if (m_ornamentsByGuid.TryGetValue(pair.Key, out SaveDataOrnament old)) old.ResolveNewData(pair.Value);
                else m_ornamentsByGuid[pair.Key] = pair.Value;
            foreach (KeyValuePair<string, SaveDataMusicTrack> pair in newSaveDataGame.m_musicTracksByGUID)
                if (m_musicTracksByGUID.TryGetValue(pair.Key, out SaveDataMusicTrack old)) old.ResolveNewData(pair.Value);
                else m_musicTracksByGUID[pair.Key] = pair.Value;
            foreach (KeyValuePair<CharacterArchetype, SaveDataCharacterArchetype> pair in newSaveDataGame.m_saveDataByCharacterArchetype)
                if (m_saveDataByCharacterArchetype.TryGetValue(pair.Key, out SaveDataCharacterArchetype old)) old.ResolveNewData(pair.Value);
                else m_saveDataByCharacterArchetype[pair.Key] = pair.Value;
            foreach (KeyValuePair<SaveDataPlayerStat.Type, SaveDataPlayerStat> pair in newSaveDataGame.m_playerStatsByType)
                if (m_playerStatsByType.TryGetValue(pair.Key, out SaveDataPlayerStat old)) old.ResolveNewData(pair.Value);
                else m_playerStatsByType[pair.Key] = pair.Value;
            foreach (KeyValuePair<CharacterArchetype, SaveDataDreamPowerLoadout> pair in newSaveDataGame.m_dreamPowerLoadoutByArchetype)
                if (m_dreamPowerLoadoutByArchetype.TryGetValue(pair.Key, out SaveDataDreamPowerLoadout old)) old.ResolveNewData(pair.Value, cloudIsLatest);
                else m_dreamPowerLoadoutByArchetype[pair.Key] = pair.Value;
            foreach (KeyValuePair<string, SaveDataCharacterSkin> pair in newSaveDataGame.m_characterSkinsByGUID)
                if (m_characterSkinsByGUID.TryGetValue(pair.Key, out SaveDataCharacterSkin old)) old.ResolveNewData(pair.Value);
                else m_characterSkinsByGUID[pair.Key] = pair.Value;

            if (string.IsNullOrWhiteSpace(m_gameVersion)) m_gameVersion = newSaveDataGame.m_gameVersion;
            else if (!string.IsNullOrWhiteSpace(newSaveDataGame.m_gameVersion))
            {
                var localVersion = new Version(m_gameVersion);
                var incomingVersion = new Version(newSaveDataGame.m_gameVersion);
                m_gameVersion = (incomingVersion > localVersion ? incomingVersion : localVersion).ToString();
            }
            m_lastLevelVisitedGUID = m_lastLevelVisitedAt > newSaveDataGame.m_lastLevelVisitedAt
                ? m_lastLevelVisitedGUID : newSaveDataGame.m_lastLevelVisitedGUID;
            m_lastLevelSelectedGUID = m_lastSavedTimestampMs > newSaveDataGame.m_lastSavedTimestampMs
                ? m_lastLevelSelectedGUID : newSaveDataGame.m_lastLevelSelectedGUID;
            m_lastLevelVisitedAt = Math.Max(m_lastLevelVisitedAt, newSaveDataGame.m_lastLevelVisitedAt);
            m_seenPromptGuids.AddUniqueFromRange(newSaveDataGame.m_seenPromptGuids);
            m_seenCutsceneGuids.AddUniqueFromRange(newSaveDataGame.m_seenCutsceneGuids);
            m_cutsceneIsNewGuids.AddUniqueFromRange(newSaveDataGame.m_cutsceneIsNewGuids);
            m_zoneUnlockSeenGuids.AddUniqueFromRange(newSaveDataGame.m_zoneUnlockSeenGuids);
            m_challenges.ResolveNewData(newSaveDataGame.m_challenges);
            m_adventureRewardTrack.ResolveNewData(newSaveDataGame.m_adventureRewardTrack);
            m_bonusZone1.ResolveNewData(newSaveDataGame.m_bonusZone1);
            m_blueCoins.ResolveNewData(newSaveDataGame.m_blueCoins);
            m_dreamPowerStore.ResolveNewData(newSaveDataGame.m_dreamPowerStore);
            m_lastSavedTimestampMs = Math.Max(m_lastSavedTimestampMs, newSaveDataGame.m_lastSavedTimestampMs);
            m_cutsceneSeenSaveMaintenancePerformed = m_cutsceneSeenSaveMaintenancePerformed && newSaveDataGame.m_cutsceneSeenSaveMaintenancePerformed;
            m_lastDeletionTimestamp = Math.Max(m_lastDeletionTimestamp, newSaveDataGame.m_lastDeletionTimestamp);
        }

        // 0x06002c08: unlike the other getters, an existing null value is returned.
        public SaveDataLevel GetOrCreateLevelData(string levelGuid)
        {
            if (!m_levelsByGuid.TryGetValue(levelGuid, out SaveDataLevel saveData))
            {
                saveData = new SaveDataLevel(levelGuid);
                m_levelsByGuid[levelGuid] = saveData;
                MarkDirty();
            }
            return saveData;
        }

        // 0x06002c09.
        public SaveDataOrnament GetOrCreateOrnamentData(string ornamentGuid)
        {
            if (!m_ornamentsByGuid.TryGetValue(ornamentGuid, out SaveDataOrnament saveData) || saveData == null)
            {
                saveData = new SaveDataOrnament(ornamentGuid);
                m_ornamentsByGuid[ornamentGuid] = saveData;
                MarkDirty();
            }
            return saveData;
        }

        // 0x06002c0a.
        public SaveDataCharacterArchetype GetOrCreateCharacterArchetypeData(CharacterArchetype characterArchetype)
        {
            if (!m_saveDataByCharacterArchetype.TryGetValue(characterArchetype, out SaveDataCharacterArchetype saveData) || saveData == null)
            {
                saveData = new SaveDataCharacterArchetype(characterArchetype);
                m_saveDataByCharacterArchetype[characterArchetype] = saveData;
                MarkDirty();
            }
            return saveData;
        }

        // 0x06002c0b.
        public SaveDataPlayerStat GetOrCreatePlayerStatData(SaveDataPlayerStat.Type statType)
        {
            if (!m_playerStatsByType.TryGetValue(statType, out SaveDataPlayerStat saveData) || saveData == null)
            {
                saveData = new SaveDataPlayerStat(statType);
                m_playerStatsByType[statType] = saveData;
                MarkDirty();
            }
            return saveData;
        }

        // 0x06002c0c: the adventure data has its separate original storage field.
        public SaveDataChallengeRewardTrack GetOrCreateRewardTrackData(RewardTrackType type) =>
            type == RewardTrackType.Adventure ? m_adventureRewardTrack : m_challenges.GetOrCreateRewardTrackData(type);

        // 0x06002c0d.
        public SaveDataMusicTrack GetOrCreateMusicTrackData(string musicTrackGUID)
        {
            if (!m_musicTracksByGUID.TryGetValue(musicTrackGUID, out SaveDataMusicTrack saveData) || saveData == null)
            {
                saveData = new SaveDataMusicTrack(musicTrackGUID);
                m_musicTracksByGUID[musicTrackGUID] = saveData;
                MarkDirty();
            }
            return saveData;
        }

        // 0x06002c0e.
        public SaveDataDreamPowerLoadout GetOrCreateDreamPowerLoadout(CharacterArchetype characterArchetype)
        {
            if (!m_dreamPowerLoadoutByArchetype.TryGetValue(characterArchetype, out SaveDataDreamPowerLoadout saveData) || saveData == null)
            {
                saveData = new SaveDataDreamPowerLoadout(characterArchetype);
                m_dreamPowerLoadoutByArchetype[characterArchetype] = saveData;
                MarkDirty();
            }
            return saveData;
        }

        // 0x06002c0f: creation deliberately does not dirty this parent save.
        public SaveDataCharacterSkin GetOrCreateSkinData(string characterSkinGUID)
        {
            if (!m_characterSkinsByGUID.TryGetValue(characterSkinGUID, out SaveDataCharacterSkin saveData) || saveData == null)
            {
                saveData = new SaveDataCharacterSkin(characterSkinGUID);
                m_characterSkinsByGUID[characterSkinGUID] = saveData;
            }
            return saveData;
        }

        // 0x06002c10: creation deliberately does not dirty this parent save.
        public SaveDataCharacterCustomisation GetOrCreateCharacterCustomisation(CharacterId characterId)
        {
            if (!m_customisationsByCharacter.TryGetValue(characterId, out SaveDataCharacterCustomisation saveData) || saveData == null)
            {
                saveData = new SaveDataCharacterCustomisation(characterId);
                m_customisationsByCharacter[characterId] = saveData;
            }
            return saveData;
        }

        // 0x06002c11.
        public void CreateRewardTrackData(RewardTrackType type, string rewardTrackGUID, string rewardGUID) =>
            GetOrCreateRewardTrackData(type).CreateRewardData(rewardTrackGUID, rewardGUID);

        // 0x06002c12/13/14: adventure lookup precedes the challenges provider.
        public SaveDataChallengeReward GetRewardDataByTrackGUIDUnsafe(string trackGUID) =>
            m_adventureRewardTrack.TryGetRewardByTrackGUID(trackGUID, out SaveDataChallengeReward reward)
                ? reward : m_challenges.GetRewardDataByTrackGUIDUnsafe(trackGUID);
        public SaveDataChallengeReward GetRewardDataByRewardGUIDUnsafe(string rewardGUID) =>
            m_adventureRewardTrack.TryGetRewardByRewardGUID(rewardGUID, out SaveDataChallengeReward reward)
                ? reward : m_challenges.GetRewardDataByRewardGUIDUnsafe(rewardGUID);
        public bool TryGetRewardDataByRewardGUID(string rewardGUID, out SaveDataChallengeReward reward) =>
            m_adventureRewardTrack.TryGetRewardByRewardGUID(rewardGUID, out reward) ||
            m_challenges.TryGetRewardDataByRewardGUID(rewardGUID, out reward);

        // 0x06002c15: all enum entries, even Adventure, query the challenges store.
        public IReadOnlyList<SaveDataChallengeReward> DebugGetAdventureRewards()
        {
            var rewards = new List<SaveDataChallengeReward>(m_adventureRewardTrack.Rewards);
            foreach (RewardTrackType type in EnumUtilities.GetValues<RewardTrackType>())
                rewards.AddRange(m_challenges.GetOrCreateRewardTrackData(type).Rewards);
            return rewards;
        }

        // 0x06002c16..1c: mutations dirty the parent even when membership is unchanged.
        public void AddSeenPromptGuid(string promptGuid)
        {
            m_seenPromptGuids.AddUnique(promptGuid);
            MarkDirty();
        }
        public void ClearAllSeenPromptGuids()
        {
            m_seenPromptGuids.Clear();
            MarkDirty();
        }
        public void AddSeenCutsceneGuid(string seenCutsceneGUID)
        {
            m_seenCutsceneGuids.AddUnique(seenCutsceneGUID);
            MarkDirty();
        }
        public void RemoveSeenCutsceneGuid(string seenCutsceneGUID)
        {
            m_seenCutsceneGuids.Remove(seenCutsceneGUID);
            MarkDirty();
        }
        public void ClearAllSeenCutsceneGuids()
        {
            m_seenCutsceneGuids.Clear();
            MarkDirty();
        }
        public void AddIsNewCutsceneGuid(string cutsceneGuid)
        {
            m_cutsceneIsNewGuids.AddUnique(cutsceneGuid);
            MarkDirty();
        }
        public void AddZoneUnlockSeenGuid(string zoneGuid)
        {
            m_zoneUnlockSeenGuids.AddUnique(zoneGuid);
            MarkDirty();
        }

        // 0x06002c1d: always refresh visit time, then dirty only on GUID change.
        public void SetLastLevelVisited(GameplayLevelDefinition level)
        {
            string guid = level != null ? level.GetGUID() : null;
            SetLevelVisitedTime();
            if (guid == m_lastLevelVisitedGUID) return;
            m_lastLevelVisitedGUID = guid;
            MarkDirty();
        }
        // 0x06002c1e.
        public void SetLastLevelSelected(GameplayLevelDefinition level)
        {
            string guid = level != null ? level.GetGUID() : null;
            if (guid == m_lastLevelSelectedGUID) return;
            m_lastLevelSelectedGUID = guid;
            MarkDirty();
        }
        // 0x06002c1f: this helper has no MarkDirty call.
        private void SetLevelVisitedTime() => m_lastLevelVisitedAt = TimeUtils.ToUnixTimeMs(DateTime.UtcNow);

        // 0x06002c20: list clearing precedes dictionary clearing.
        public void ClearAllUnlockedOrnamentGuids()
        {
            m_ornaments.Clear();
            m_ornamentsByGuid.Clear();
            MarkDirty();
        }
        // 0x06002c21: an existing null value is deliberately dereferenced.
        public void UnlockCharacterArchetype(CharacterArchetype characterArchetype)
        {
            if (m_saveDataByCharacterArchetype.TryGetValue(characterArchetype, out SaveDataCharacterArchetype saveData))
            {
                if (saveData.Unlocked) return;
                saveData.Unlocked = true;
            }
            else
            {
                saveData = new SaveDataCharacterArchetype(characterArchetype);
                saveData.Unlocked = true;
                m_saveDataByCharacterArchetype[characterArchetype] = saveData;
            }
            MarkDirty();
        }
        // 0x06002c22: absent keys leave dirty state unchanged.
        public void RemoveUnlockedOrnamentGuid(string unlockedOrnamentGUID)
        {
            if (!m_ornamentsByGuid.TryGetValue(unlockedOrnamentGUID, out SaveDataOrnament ornament)) return;
            m_ornaments.Remove(ornament);
            m_ornamentsByGuid.Remove(unlockedOrnamentGUID);
            MarkDirty();
        }
        // 0x06002c25: returns serialized-list Count without recaching it.
        public int CollectedOrnamentsCount() => m_ornaments.Count;

        // Game.Runtime.dll 0x06002c23.
        public void OnBeforeSerialize()
        {
            DictionaryToList(m_levelsByGuid, ref m_levels);
            DictionaryToList(m_ornamentsByGuid, ref m_ornaments);
            DictionaryToList(m_musicTracksByGUID, ref m_musicTracks);
            DictionaryToList(m_saveDataByCharacterArchetype, ref m_characterArchetypes);
            DictionaryToList(m_playerStatsByType, ref m_playerStats);
            DictionaryToList(m_dreamPowerLoadoutByArchetype, ref m_dreamPowerLoadouts);
            DictionaryToList(m_characterSkinsByGUID, ref m_characterSkins);
            DictionaryToList(m_customisationsByCharacter, ref m_characterCustomisations);
        }

        // Game.Runtime.dll 0x06002c24.
        public void OnAfterDeserialize()
        {
            Initialise();
            ListToDictionary(m_levels, m_levelsByGuid, level => level.GUID);
            ListToDictionary(m_ornaments, m_ornamentsByGuid, ornament => ornament.GUID);
            ListToDictionary(m_musicTracks, m_musicTracksByGUID, musicTrack => musicTrack.GUID);
            ListToDictionary(m_characterArchetypes, m_saveDataByCharacterArchetype, saveData => saveData.CharacterArchetype);
            ListToDictionary(m_playerStats, m_playerStatsByType, playerStat => playerStat.StatType);
            ListToDictionary(m_dreamPowerLoadouts, m_dreamPowerLoadoutByArchetype, loadout => loadout.Archetype);
            ListToDictionary(m_characterSkins, m_characterSkinsByGUID, skin => skin.GUID);
            ListToDictionary(m_characterCustomisations, m_customisationsByCharacter, characterCustomisation => characterCustomisation.CharacterId);
        }

        // Game.Runtime.dll 0x06002c26.
        public void SetLastSavedTime(long timestampMs)
        {
            m_lastSavedTimestampMs = timestampMs;
            MarkDirty();
        }

        // Game.Runtime.dll 0x06002c27.
        public bool IsSaveEmpty()
        {
            return string.IsNullOrWhiteSpace(m_lastLevelVisitedGUID);
        }

    }
}
