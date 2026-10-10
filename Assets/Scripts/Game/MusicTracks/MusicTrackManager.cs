using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime owner 02000727; all methods and natural contexts are preserved.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class MusicTrackManager : ISystem, ISaveGameListener
    {
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
        private readonly SystemRef<AudioManager> m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>(null, true);
        private readonly SystemRef<ChallengeManager> m_challengeManagerRef = ProcessManager.GetSystemRef<ChallengeManager>(null, true);
        private readonly List<MusicTrackDefinition> m_unlockedMusicTracks = new List<MusicTrackDefinition>();
        private readonly List<MusicTrackDefinition> m_shuffledMusicTracks = new List<MusicTrackDefinition>();
        private const HLAudioSourceIdentifier DefaultAudioSource = HLAudioSourceIdentifier.Background;
        public event Action<MusicTrackDefinition> OnMusicTrackChanged = delegate { };
        public bool ShuffleActive { get; private set; }
        private Coroutine m_musicPlaybackPolling;

        // 06002842/2858: dependent-save callback, persistent level startup subscription, then mission validity callback.
        public MusicTrackManager()
        {
            m_saveManagerRef.InvokeOnValid(saveManager => saveManager.AddDependantListener(this, true));
            m_levelManagerRef.OnSystemStartup += OnLevelManagerStartup;
            if (m_levelManagerRef.IsValid()) OnLevelManagerStartup(m_levelManagerRef.Get());
            m_missionManagerRef.InvokeOnValid(OnMissionManagerStartup);
        }

        // 06002843: combine with the existing callback; no duplicate suppression or unsubscribe.
        private void OnMissionManagerStartup(MissionManager missionManager)
        {
            missionManager.OnMissionCompleted += OnMissionCompleted;
        }

        // 06002844: the original GUID-aware operator is selected by the genuine definition type.
        private void OnMissionCompleted(MissionState missionState)
        {
            if (missionState.Definition.MusicTrack != null) CheckForUnlockedTracks();
        }

        // 06002845: exit handler first, then start handler on intro completion.
        private void OnLevelManagerStartup(LevelManager levelManager)
        {
            levelManager.OnLevelExit += OnLevelExit;
            levelManager.OnIntroSequenceComplete += OnLevelStart;
        }

        // 06002846: current level track is added only after successful source lookup.
        private void OnLevelStart()
        {
            if (m_audioManagerRef.Get().TryGetAudioSourceData(DefaultAudioSource, out HLAudioSourceData audioSourceData))
            {
                AddCurrentLevelMusic();
                SetupTrackEndBehaviour(audioSourceData);
            }
        }

        // 06002847: stop first; partial failures do not perform the subsequent refresh.
        private void OnLevelExit()
        {
            CoroutineUtils.StopUtilCoroutine(ref m_musicPlaybackPolling);
            CheckForUnlockedTracks();
        }

        // 06002848: publish the new handle only after RunCoroutine returns.
        public void SetupTrackEndBehaviour(HLAudioSourceData audioSourceData)
        {
            CoroutineUtils.StopUtilCoroutine(ref m_musicPlaybackPolling);
            m_musicPlaybackPolling = CoroutineUtils.RunCoroutine(WaitForMusicEnd(audioSourceData));
        }

        // 06002849,2862..2867/285c..285d: two distinct waits, original method group and short-circuit predicate.
        private IEnumerator WaitForMusicEnd(HLAudioSourceData audioSourceData)
        {
            yield return new WaitUntil(audioSourceData.IsPlaying);
            yield return new WaitUntil(() => !audioSourceData.IsPlaying() && audioSourceData.IsAtEnd());
            MusicTrackDefinition nextTrack = GetNext(audioSourceData.CurrentAudio);
            m_audioManagerRef.Get().PlayClipAtSource(nextTrack.ClipIdentifier, DefaultAudioSource, false, null);
            OnMusicTrackChanged(nextTrack);
            SetupTrackEndBehaviour(audioSourceData);
        }

        // 0600284a: original BCL IReadOnlyDictionary overload, without a supplied default value.
        public MusicTrackDefinition GetDefinition(HLAudioClipIdentifier audioClipIdentifier)
        {
            return m_dataManagerRef.Get().MusicTrackDefinitions.GetValueOrDefault(audioClipIdentifier);
        }

        // 0600284b/284c: signed one-step navigation.
        public MusicTrackDefinition GetNext(HLAudioClipIdentifier currentTrackIdentifier) => GetTrack(currentTrackIdentifier, 1);
        public MusicTrackDefinition GetPrevious(HLAudioClipIdentifier currentTrackIdentifier) => GetTrack(currentTrackIdentifier, -1);

        // 0600284d: false retains the previous shuffled list; true mutates incrementally with original engine RNG.
        public void SetShuffle(bool active)
        {
            ShuffleActive = active;
            if (active)
            {
                m_shuffledMusicTracks.Clear();
                List<MusicTrackDefinition> remainingTracks = new List<MusicTrackDefinition>(m_unlockedMusicTracks);
                for (int remaining = remainingTracks.Count; remaining > 0; --remaining)
                {
                    int index = UnityEngine.Random.Range(0, remainingTracks.Count);
                    m_shuffledMusicTracks.Add(remainingTracks[index]);
                    remainingTracks.RemoveAt(index);
                }
            }
        }

        // 0600284e: intentionally one-based when found; absent returns the dictionary count, including zero.
        public int GetTrackIndex(MusicTrackDefinition trackDefinition)
        {
            int index = 0;
            foreach (var (_, definition) in m_dataManagerRef.Get().MusicTrackDefinitions)
            {
                ++index;
                if (definition == trackDefinition) break;
            }
            return index;
        }

        // 0600284f/285e..285f: list selection follows definition lookup; no empty-list guard.
        private MusicTrackDefinition GetTrack(HLAudioClipIdentifier currentTrackIdentifier, int indexChange)
        {
            MusicTrackDefinition currentTrack = GetDefinition(currentTrackIdentifier);
            List<MusicTrackDefinition> tracks = ShuffleActive ? m_shuffledMusicTracks : m_unlockedMusicTracks;
            int index = tracks.FindIndex(track => track == currentTrack);
            return tracks[WrapIndex(unchecked(index + indexChange), tracks.Count)];
        }

        // 06002850: single wrap, not modulo; original signed and empty-count behavior is retained.
        private static int WrapIndex(int index, int count)
        {
            if (index < 0) return unchecked(count - 1);
            if (index >= count) return 0;
            return index;
        }

        // 06002851/2860..2861: clear before provider reads; strict cast in predicate and safe cast of Find result.
        private void CheckForUnlockedTracks()
        {
            m_unlockedMusicTracks.Clear();
            MissionManager missionManager = m_missionManagerRef.Get();
            DataManager dataManager = m_dataManagerRef.Get();
            SaveManager saveManager = m_saveManagerRef.Get();
            ChallengeManager challengeManager = m_challengeManagerRef.Get();
            foreach (var (_, musicTrackDefinition) in dataManager.MusicTrackDefinitions)
            {
                if (musicTrackDefinition.AlwaysUnlocked)
                {
                    m_unlockedMusicTracks.AddUnique(musicTrackDefinition);
                    continue;
                }
                if (musicTrackDefinition.HiddenInJukebox) continue;
                var (missionDefinition, levelDefinition) = FindMissionDefinitionForTrack(musicTrackDefinition);
                if (missionDefinition != null && levelDefinition != null)
                {
                    if (missionManager.GetSaveDataForMission(missionDefinition.GetGUID(), levelDefinition.GetGUID(), null).Complete)
                        m_unlockedMusicTracks.AddUnique(musicTrackDefinition);
                }
                else
                {
                    ChallengeRewardMusicTrack reward = challengeManager.ChallengeRewardsByType[ChallengeReward.ChallengeRewardType.MusicTrack]
                        .Find(reward => ((ChallengeRewardMusicTrack)reward).MusicTrack == musicTrackDefinition) as ChallengeRewardMusicTrack;
                    if (reward != null && saveManager.CurrentSave.TryGetRewardDataByRewardGUID(reward.GetRewardGUID(), out SaveDataChallengeReward rewardData) && rewardData.Collected)
                        m_unlockedMusicTracks.AddUnique(musicTrackDefinition);
                }
            }
        }

        // 06002852: dereference level definition before testing the mission context; zero override retains level audio.
        private void AddCurrentLevelMusic()
        {
            LevelManagerLevel currentLevel = m_levelManagerRef.Get().GetCurrentLevelUnsafe();
            IMissionContext activeMissionContext = m_missionManagerRef.Get().ActiveMissionContext;
            HLAudioClipIdentifier clipIdentifier = currentLevel.LevelDefinition.BackgroundAudioClip;
            if (activeMissionContext != null)
            {
                HLAudioClipIdentifier missionClip = activeMissionContext.MissionDefinition.MusicOverrideIdentifier;
                if (missionClip != HLAudioClipIdentifier.None) clipIdentifier = missionClip;
            }
            m_unlockedMusicTracks.AddUnique(GetDefinition(clipIdentifier));
        }

        // 06002853: save-record creation is intentional; no RequestSave is added to this count query.
        public int GetNewTracksCount()
        {
            SaveDataGame saveDataGame = m_saveManagerRef.Get().CurrentSave;
            int count = 0;
            foreach (MusicTrackDefinition track in m_unlockedMusicTracks)
                if (!track.AlwaysUnlocked && !saveDataGame.GetOrCreateMusicTrackData(track.GetGUID()).Seen) ++count;
            return count;
        }

        // 06002854: first matching dictionary entry is returned; every exit disposes the real enumerator.
        private KeyValuePair<MissionDefinition, GameplayLevelDefinition> FindMissionDefinitionForTrack(MusicTrackDefinition musicTrackDefinition)
        {
            foreach (KeyValuePair<MissionDefinition, GameplayLevelDefinition> entry in m_dataManagerRef.Get().MissionDefinitionLevelLookup)
                if (entry.Key.MusicTrack == musicTrackDefinition) return entry;
            return default;
        }

        // 06002855/2856: opening refreshes; closing is the genuine empty implementation.
        public void OnSaveGameOpen(SaveDataGame saveDataGame) => CheckForUnlockedTracks();
        public void OnSaveGameClose(SaveDataGame saveDataGame) { }

        // 06002857: mark existing reward, request save, and only then optionally refresh; no record creation.
        public void CollectTrack(MusicTrackDefinition definition, bool refreshUnlockedTracks = true)
        {
            SaveManager saveManager = m_saveManagerRef.Get();
            saveManager.CurrentSave.GetRewardDataByRewardGUIDUnsafe(definition.GetGUID()).Collected = true;
            saveManager.RequestSave();
            if (refreshUnlockedTracks) CheckForUnlockedTracks();
        }
    }
}
