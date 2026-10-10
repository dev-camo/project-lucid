using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Analytics;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerCollectionsMusic : UIContainer
    {
        [SerializeField] private Animation m_animation;
        [SerializeField] private AnimationClip m_unlockAnimationClip;
        [SerializeField] private AnimationClip m_lockAnimationClip;
        [SerializeField] private RectTransform m_tracksRoot;
        [SerializeField] private UIWidgetToggleMusicTrack m_trackWidgetPrefab;
        [SerializeField] private UIContainerMusicPlayer m_musicPlayer;
        [SerializeField] private UIToggleGroup m_trackWidgetsToggleGroup;
        [SerializeField] private HLScrollRectSelection m_scrollRectSelection;
        [SerializeField]
        [Header("Info panel")]
        private GameObject m_infoPanel;
        [SerializeField] private UIWidgetButtonImageBase m_infoButton;
        [SerializeField] private GameObject m_infoGlyph;

        public readonly Bindable<Strings> Title = new Bindable<Strings>();
        public readonly Bindable<int> TotalAmount = new Bindable<int>();
        public readonly Bindable<int> CollectedAmount = new Bindable<int>();
        public readonly Bindable<string> DisplayFormattedString = new Bindable<string>();
        public readonly Bindable<string> InfoText = new Bindable<string>();

        // Game.Runtime060031ad/31ae: ordinary atomic event accessors; original
        //060031c5 supplies the empty initial callback, not a nullable fallback.
        public event Action OnNewMusicTracksCountChanged = () => { };

        private int m_activeTrackIndex;
        private DataManager m_dataManager;
        private MissionManager m_missionManager;
        private ProgressionManager m_progressionManager;
        private SaveManager m_saveManager;
        private ChallengeManager m_challengeManager;
        private LevelManager m_levelManager;
        private readonly List<MusicTrackCache> m_allTracksCache = new List<MusicTrackCache>();
        private readonly List<UIWidgetToggleMusicTrack> m_trackWidgets = new List<UIWidgetToggleMusicTrack>();

        // Game.Runtime060031af.
        public UIContainerMusicPlayer MusicPlayer => m_musicPlayer;

        // Game.Runtime060031b0: all six registrations precede cache and row
        // creation. The initial selection precedes the stop subscription.
        private void Start()
        {
            m_dataManager = ProcessManager.GetSystem<DataManager>();
            m_missionManager = ProcessManager.GetSystem<MissionManager>();
            m_progressionManager = ProcessManager.GetSystem<ProgressionManager>();
            m_saveManager = ProcessManager.GetSystem<SaveManager>();
            m_challengeManager = ProcessManager.GetSystem<ChallengeManager>();
            m_levelManager = ProcessManager.GetSystem<LevelManager>();
            CacheMusicData();
            UpdateCollectableCounter();
            SetupRows();
            MusicTrackCache musicTrackCache = m_allTracksCache[0];
            OnTrackValueChanged(true, 0, musicTrackCache.Number, musicTrackCache);
            m_musicPlayer.OnMusicStopped += OnMusicStopped;
        }

        // Game.Runtime060031b1: no Unity-null or partial-startup guard.
        private void OnDestroy()
        {
            m_musicPlayer.OnMusicStopped -= OnMusicStopped;
        }

        // Game.Runtime060031b2: enrich the value argument using the current
        // active cache before the authentic original analytics call.
        private void OnMusicStopped(MusicTrackEndEvent musicTrackEndEvent)
        {
            musicTrackEndEvent.TrackCache = m_allTracksCache[m_activeTrackIndex];
            AnalyticsEventCollector.JukeboxTrackEndEvent(musicTrackEndEvent);
        }

        // Game.Runtime060031b3; original literal0xe3dc00e8.
        private void OnEnable()
        {
            Title.Value = Strings.MENU_JUKEBOX_TITLE;
            m_infoButton.SetInteractable(false);
            m_infoGlyph.SetActive(false);
        }

        // Game.Runtime060031b4 and natural060031c6/31c7: each iteration has
        // its own captured cache/index/number, and setup precedes list append.
        private void SetupRows()
        {
            for (int i = 0; i < m_allTracksCache.Count; i++)
            {
                MusicTrackCache musicTrackCache = m_allTracksCache[i];
                int trackIndex = i;
                int trackNumber = musicTrackCache.Number;
                MusicTrackDefinition musicTrackDefinition = musicTrackCache.MusicTrackDefinition;
                bool locked = IsMusicTrackLocked(musicTrackCache);
                bool isNew = musicTrackCache.IsNew;
                bool selected = i == m_activeTrackIndex;
                UIWidgetToggleMusicTrack widget = UnityEngine.Object.Instantiate(m_trackWidgetPrefab, m_tracksRoot);
                widget.Setup(new UIWidgetToggleMusicTrackParameters(trackNumber,
                    musicTrackDefinition.ImageAsset, musicTrackDefinition.Name,
                    musicTrackDefinition.Composer, selected, locked, isNew,
                    m_trackWidgetsToggleGroup,
                    isOn => OnTrackValueChanged(isOn, trackIndex, trackNumber, musicTrackCache),
                    OnMusicTrackHighlighted));
                m_trackWidgets.Add(widget);
            }
        }

        // Game.Runtime060031b5; original bools and null callback are required.
        private void OnMusicTrackHighlighted(RectTransform musicTrackTransform)
        {
            m_scrollRectSelection.Select(musicTrackTransform, true, false, null);
        }

        // Game.Runtime060031b6 and natural060031c8/31c9. The selected record
        // becomes seen and the list changes before notification. The captured
        // value stays unchanged; the callback can fault before the index write.
        private void OnTrackValueChanged(bool isOn, int musicTrackIndex,
            int musicTrackNumber, MusicTrackCache musicTrackCache)
        {
            if (!isOn) return;
            if (musicTrackCache.IsNew)
            {
                m_saveManager.CurrentSave.GetOrCreateMusicTrackData(
                    musicTrackCache.MusicTrackDefinition.GetGUID()).Seen = true;
                m_saveManager.RequestSave();
                int trackIndex = m_allTracksCache.FindIndex(track =>
                    track.MusicTrackDefinition == musicTrackCache.MusicTrackDefinition);
                m_allTracksCache[trackIndex] = new MusicTrackCache(musicTrackCache.Number,
                    musicTrackCache.LevelDefinition, musicTrackCache.MusicTrackDefinition,
                    musicTrackCache.Unlocked, false);
                OnNewMusicTracksCountChanged();
            }
            m_activeTrackIndex = musicTrackIndex;
            // The original passes musicTrackNumber into the parameter whose
            // shipped name is musicTrackIndex. This distinction is retained.
            UpdateMusicPlayer(musicTrackNumber, musicTrackCache);
        }

        // Game.Runtime060031b7: animation then player setup; info status is
        // recalculated after setup, allowing original callbacks to mutate it.
        private void UpdateMusicPlayer(int musicTrackIndex, MusicTrackCache musicTrackCache)
        {
            bool locked = IsMusicTrackLocked(musicTrackCache);
            m_animation.Play((locked ? m_lockAnimationClip : m_unlockAnimationClip).name);
            m_musicPlayer.Setup(new UIContainerMusicPlayerParameters(
                musicTrackCache.MusicTrackDefinition, musicTrackCache.LevelDefinition,
                musicTrackIndex, locked, OnPlayNextTrack, OnPlayPreviousTrack, OnShuffleTrack));
            SetInfoPanelStatus(musicTrackCache);
        }

        // Game.Runtime060031b8 and natural060031ca..31cd: original dictionary
        // order, full foreach disposal and first-match replacement are retained.
        // There is no cache clear or missing-match guard; a failed later phase
        // retains the prefix of records already appended or replaced.
        private void CacheMusicData()
        {
            SaveDataGame saveDataGame = m_saveManager.CurrentSave;
            int number = 1;
            foreach (MusicTrackDefinition musicTrackDefinition in m_dataManager.MusicTrackDefinitions.Values)
            {
                if (musicTrackDefinition.HiddenInJukebox) continue;
                bool isNew = true;
                if (musicTrackDefinition.AlwaysUnlocked)
                    isNew = !saveDataGame.GetOrCreateMusicTrackData(musicTrackDefinition.GetGUID()).Seen;
                m_allTracksCache.Add(new MusicTrackCache(number, null,
                    musicTrackDefinition, true, isNew));
                number = unchecked(number + 1);
            }
            foreach (GameplayLevelDefinition levelDefinition in m_levelManager.GameLevels.GetLevels())
            {
                if (!levelDefinition.ShowInLevelSelect || levelDefinition.MissionList == null) continue;
                foreach (MissionDefinition missionDefinition in levelDefinition.MissionList.GetMissionsOfType(MissionType.Jukebox))
                {
                    SaveDataLevelMission saveDataMission = m_missionManager.GetSaveDataForMission(
                        missionDefinition.GetGUID(), levelDefinition.GetGUID(), null);
                    MusicTrackDefinition missionMusicTrackDefinition = missionDefinition.MusicTrack;
                    if (missionMusicTrackDefinition == null) continue;
                    int trackIndex = m_allTracksCache.FindIndex(track =>
                        track.MusicTrackDefinition == missionMusicTrackDefinition);
                    MusicTrackCache musicTrackCache = m_allTracksCache[trackIndex];
                    bool isNew = saveDataMission.Complete &&
                        !saveDataGame.GetOrCreateMusicTrackData(musicTrackCache.MusicTrackDefinition.GetGUID()).Seen;
                    m_allTracksCache[trackIndex] = new MusicTrackCache(musicTrackCache.Number,
                        levelDefinition, missionMusicTrackDefinition, saveDataMission.Complete, isNew);
                }
            }
            foreach (var (_, rewardTrackState) in m_challengeManager.RewardTrackStates)
            {
                foreach (ChallengeReward reward in rewardTrackState.Definition.Rewards)
                {
                    if (!(reward is ChallengeRewardMusicTrack musicTrackReward)) continue;
                    MusicTrackDefinition musicTrackDefinition = musicTrackReward.MusicTrack;
                    int trackIndex = m_allTracksCache.FindIndex(track =>
                        track.MusicTrackDefinition == musicTrackDefinition);
                    MusicTrackCache musicTrackCache = m_allTracksCache[trackIndex];
                    SaveDataChallengeReward saveDataReward = saveDataGame.GetRewardDataByRewardGUIDUnsafe(
                        musicTrackDefinition.GetGUID());
                    bool unlocked = saveDataReward != null && saveDataReward.Collected;
                    bool isNew = unlocked &&
                        !saveDataGame.GetOrCreateMusicTrackData(musicTrackCache.MusicTrackDefinition.GetGUID()).Seen;
                    m_allTracksCache[trackIndex] = new MusicTrackCache(musicTrackCache.Number,
                        null, musicTrackDefinition, unlocked, isNew);
                }
            }
        }

        // Game.Runtime060031b9: publish the format then collected/total. Each
        // always-unlocked record independently increments both bindables.
        private void UpdateCollectableCounter()
        {
            CollectableDefinition definition = m_dataManager.CollectableDefinitions[CollectableType.MusicTrack];
            DisplayFormattedString.Value = definition.DisplayFormattedString;
            ProgressionCollectable progress = m_progressionManager.GetTotalProgress(CollectableType.MusicTrack);
            CollectedAmount.Value = progress.Collected;
            TotalAmount.Value = progress.Total;
            foreach (MusicTrackCache musicTrackCache in m_allTracksCache)
            {
                if (!musicTrackCache.MusicTrackDefinition.AlwaysUnlocked) continue;
                CollectedAmount.Value = unchecked(CollectedAmount.Value + 1);
                TotalAmount.Value = unchecked(TotalAmount.Value + 1);
            }
        }

        // Game.Runtime060031ba.
        private static bool IsMusicTrackLocked(MusicTrackCache musicTrackCache)
        {
            return !musicTrackCache.MusicTrackDefinition.AlwaysUnlocked && !musicTrackCache.Unlocked;
        }

        // Game.Runtime060031bb/31bc retain original wrappers whose native bodies
        // inline the complete directional loop.
        private void OnPlayNextTrack() { IterateSelectTrack(true); }
        private void OnPlayPreviousTrack() { IterateSelectTrack(false); }

        // Game.Runtime060031bd: no empty/all-locked termination guard. Both
        // shipping architectures continue while locked OR the index is -1.
        private void IterateSelectTrack(bool forward)
        {
            int trackIndex = m_activeTrackIndex;
            int step = forward ? 1 : -1;
            MusicTrackCache musicTrackCache;
            do
            {
                trackIndex = unchecked(trackIndex + step);
                int count = m_allTracksCache.Count;
                if (trackIndex < 0) trackIndex = count - 1;
                else if (trackIndex >= count) trackIndex = 0;
                musicTrackCache = m_allTracksCache[trackIndex];
            }
            while (IsMusicTrackLocked(musicTrackCache) || trackIndex == -1);
            ForceReselectTrack(trackIndex);
        }

        // Game.Runtime060031be: one collected track selects literal index0.
        // Otherwise keep retrying random unlocked indices until it differs from
        // the live active index; empty lists and a sole active candidate retain
        // the shipping fault/nontermination behavior.
        private void OnShuffleTrack()
        {
            if (CollectedAmount.Value == 1)
            {
                ForceReselectTrack(0);
                return;
            }
            List<int> trackIndices = new List<int>();
            for (int i = 0; i < m_allTracksCache.Count; i++)
                if (!IsMusicTrackLocked(m_allTracksCache[i])) trackIndices.Add(i);
            int trackIndex;
            do { trackIndex = trackIndices[UnityEngine.Random.Range(0, trackIndices.Count)]; }
            while (trackIndex == m_activeTrackIndex);
            ForceReselectTrack(trackIndex);
        }

        // Game.Runtime060031bf: mutation precedes the row lookup, selection
        // callback precedes the scroll receiver reload, and the cast is explicit.
        private void ForceReselectTrack(int trackIndex)
        {
            m_activeTrackIndex = trackIndex;
            UIWidgetToggleMusicTrack widget = m_trackWidgets[trackIndex];
            widget.SetSelected(true);
            m_scrollRectSelection.Select((RectTransform)widget.transform, true, false, null);
        }

        // Game.Runtime060031c0; original literal0xf9ca4263. InfoText receiver is
        // captured before translation through the authentic StringTable API.
        private void SetInfoPanelStatus(MusicTrackCache musicTrackCache)
        {
            m_infoPanel.SetActive(IsMusicTrackLocked(musicTrackCache));
            InfoText.Value = StringTable.GetString(Strings.MENU_JUKEBOX_INFO);
        }

        // Game.Runtime060031c1: own field initializers run in declaration order
        // before the real UIContainer base. The active index remains zero.
        public UIContainerCollectionsMusic() { }

        public readonly struct MusicTrackCache
        {
            public readonly int Number;
            public readonly GameplayLevelDefinition LevelDefinition;
            public readonly MusicTrackDefinition MusicTrackDefinition;
            public readonly bool Unlocked;
            public readonly bool IsNew;

            // Game.Runtime060031c2; no validation or normalization.
            public MusicTrackCache(int number, GameplayLevelDefinition levelDefinition,
                MusicTrackDefinition musicTrackDefinition, bool unlocked, bool isNew)
            {
                Number = number;
                LevelDefinition = levelDefinition;
                MusicTrackDefinition = musicTrackDefinition;
                Unlocked = unlocked;
                IsNew = isNew;
            }
        }
    }
}
