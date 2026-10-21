using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008de; bodies translated from the 1.10.1 x86-64 binary.
    // The jukebox controls of the mission screen, driving the background audio source.
    // The compiler-generated WaitForMusic iterator (<WaitForMusic>d__25) follows member order: 22 fields, then 3 methods.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerMissionMusicPlayer : UIContainer
    {
        [SerializeField] private List<UIWidgetButtonMusic> m_playerButtons;
        [SerializeField] private List<UIWidgetToggleMusicPlayer> m_playerToggles;
        [SerializeField] private UIWidgetToggleMusicPlayer m_playToggle;
        [SerializeField] private UIWidgetToggleMusicPlayer m_repeatToggle;
        [SerializeField] private UIWidgetToggleMusicPlayer m_shuffleToggle;
        [SerializeField] private Animation[] m_playingAnimations;
        public readonly Bindable<string> TrackName = new Bindable<string>();
        public readonly Bindable<string> TrackCurrentTimeFormatted = new Bindable<string>();
        public readonly Bindable<string> TrackTotalTimeFormatted = new Bindable<string>();
        public readonly Bindable<float> TrackTotalTime = new Bindable<float>();
        public readonly Bindable<float> TrackSliderValue = new Bindable<float>();
        public readonly Bindable<bool> TrackSliderInteractable = new Bindable<bool>();
        public readonly Bindable<bool> ShowTrackAnimation = new Bindable<bool>();
        private readonly SystemRef<AudioManager> m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>(null, true);
        private MusicTrackManager m_musicTrackManager;
        private const HLAudioSourceIdentifier DefaultAudioSource = HLAudioSourceIdentifier.Background;
        private HLAudioClipIdentifier m_currentlyPlayingClip;
        private MusicTrackDefinition m_currentTrackDefinition;
        private HLAudioSourceData m_audioSourceData;
        private bool m_isRepeating;
        private float m_initialBackgroundSourceVolume;
        private Coroutine m_waitForMusicCoroutine;

        // 06003321 (0x63D690)
        private void Awake()
        {
            m_musicTrackManager = ProcessManager.GetSystem<MusicTrackManager>(null, true);
            m_musicTrackManager.OnMusicTrackChanged += OnMusicTrackChanged;
        }

        // 06003322 (0x63D770)
        private void OnDestroy()
        {
            if (m_musicTrackManager != null)
                m_musicTrackManager.OnMusicTrackChanged -= OnMusicTrackChanged;
        }

        // 06003323 (0x63D800): with the background source available every music control is unlocked and the
        // current track is picked up from the source once it is known.
        private void OnEnable()
        {
            if (m_audioManagerRef.Get().TryGetAudioSourceData(DefaultAudioSource, out HLAudioSourceData audioSourceData))
            {
                foreach (UIWidgetButtonMusic playerButton in m_playerButtons)
                    playerButton.Unlock();
                foreach (UIWidgetToggleMusicPlayer playerToggle in m_playerToggles)
                    playerToggle.Unlock();
                m_audioSourceData = audioSourceData;
                m_waitForMusicCoroutine = StartCoroutine(WaitForMusic());
            }
        }

        // 06003324 (0x63DAF0), state machine MoveNext at 0x63E620: state 1 re-enters the same body, so the lookup
        // repeats every frame until the clip playing on the source is a known track.
        private IEnumerator WaitForMusic()
        {
            while (true)
            {
                m_currentlyPlayingClip = m_audioSourceData.CurrentAudio;
                m_isRepeating = m_audioSourceData.IsRepeating;
                MusicTrackDefinition musicTrackDefinition = m_musicTrackManager.GetDefinition(m_currentlyPlayingClip);
                if (musicTrackDefinition != null)
                {
                    SelectTrack(musicTrackDefinition);
                    yield break;
                }
                yield return null;
            }
        }

        // 06003325 (0x63DB80)
        private void OnDisable()
        {
            if (m_waitForMusicCoroutine != null)
            {
                StopCoroutine(m_waitForMusicCoroutine);
                m_waitForMusicCoroutine = null;
            }
        }

        // 06003326 (0x63DBD0): a track hidden in the jukebox shows its bare name, others are numbered by their index.
        public void SelectTrack(MusicTrackDefinition musicTrackDefinition)
        {
            m_currentTrackDefinition = musicTrackDefinition;
            string trackName = StringTable.GetString(musicTrackDefinition.Name);
            string numberedTrackName = string.Format("{0}. {1}", m_musicTrackManager.GetTrackIndex(musicTrackDefinition), trackName);
            if (!m_currentTrackDefinition.HiddenInJukebox)
                trackName = numberedTrackName;
            TrackName.Value = trackName;
            ShowTrackAnimation.Value = true;
            ResetPlayingAnimations();
            m_audioManagerRef.Get().GetClip(musicTrackDefinition.ClipIdentifier, SetupAudioBindings);
            if (m_isRepeating)
            {
                m_repeatToggle.Toggle.IsOn = true;
                m_repeatToggle.Toggle.Transition.QueueTransitionToState(UITransitionState.Selected);
            }
            if (m_musicTrackManager.ShuffleActive)
            {
                m_shuffleToggle.Toggle.IsOn = true;
                m_shuffleToggle.Toggle.Transition.QueueTransitionToState(UITransitionState.Selected);
            }
            Play();
        }

        // 06003327 (0x63DF80)
        private void Update()
        {
            float trackTime = m_audioSourceData.GetTime();
            TimeSpan timeSpan = TimeSpan.FromSeconds(trackTime);
            TrackCurrentTimeFormatted.Value = string.Format("{0:00}:{1:00}", timeSpan.Minutes, timeSpan.Seconds);
            TrackSliderValue.Value = trackTime;
        }

        // 06003328 (0x63E0B0): switching play off does nothing.
        public void Action_TogglePlay(bool play)
        {
            if (play)
                Play();
        }

        // 06003329 (0x63E0C0)
        public void Action_ToggleRepeat(bool repeat)
        {
            m_isRepeating = repeat;
            m_audioSourceData.SetLooping(repeat);
            if (m_isRepeating)
                m_shuffleToggle.Toggle.IsOn = false;
        }

        // 0600332a (0x63E110)
        public void Action_StartSeek()
        {
            m_audioSourceData.Mute();
        }

        // 0600332b (0x63E130)
        public void Action_Seek(float trackTime)
        {
            m_audioSourceData.Seek(trackTime);
        }

        // 0600332c (0x63E170): the supplied time is unused.
        public void Action_FinishSeek(float trackTime)
        {
            m_audioSourceData.Unmute();
        }

        // 0600332d (0x63E190)
        public void Action_PlayNext()
        {
            SelectTrack(m_musicTrackManager.GetNext(m_audioSourceData.CurrentAudio));
        }

        // 0600332e (0x63E1D0)
        public void Action_PlayPrevious()
        {
            SelectTrack(m_musicTrackManager.GetPrevious(m_audioSourceData.CurrentAudio));
        }

        // 0600332f (0x63E210)
        public void Action_ToggleShuffle(bool shuffleActive)
        {
            m_musicTrackManager.SetShuffle(shuffleActive);
            if (shuffleActive)
                m_repeatToggle.Toggle.IsOn = false;
        }

        // 06003330 (0x63DE80): a clip that is not already the current one restarts the source at the beginning.
        private void Play()
        {
            HLAudioClipIdentifier clipIdentifier = m_currentTrackDefinition.ClipIdentifier;
            if (clipIdentifier != m_currentlyPlayingClip)
            {
                m_audioSourceData.Seek(0f);
                m_audioSourceData.Pause();
                m_audioManagerRef.Get().PlayClipAtSource(clipIdentifier, DefaultAudioSource, false, null);
            }
            TrackSliderInteractable.Value = true;
            PlayPlayingAnimations();
            m_currentlyPlayingClip = clipIdentifier;
            m_audioSourceData.SetLooping(m_isRepeating);
        }

        // 06003331 (0x63E150)
        private void Seek(float trackTime)
        {
            m_audioSourceData.Seek(trackTime);
        }

        // 06003332 (0x63E290): the clip length is read twice, around the first bindable callback.
        private void SetupAudioBindings(HLAudioClipHandler.LoadedAudioParameters audioParameters)
        {
            AudioClip clip = audioParameters.Clip;
            TrackTotalTime.Value = clip.length;
            TimeSpan timeSpan = TimeSpan.FromSeconds(clip.length);
            TrackTotalTimeFormatted.Value = string.Format("{0:00}:{1:00}", timeSpan.Minutes, timeSpan.Seconds);
        }

        // 06003333 (0x63E3C0)
        private void OnMusicTrackChanged(MusicTrackDefinition newTrack)
        {
            m_currentlyPlayingClip = m_audioSourceData.CurrentAudio;
            SelectTrack(newTrack);
        }

        // 06003334 (0x63E250)
        private void PlayPlayingAnimations()
        {
            foreach (Animation playingAnimation in m_playingAnimations)
                playingAnimation.Play();
        }

        // 06003335 (0x63DE20)
        public void ResetPlayingAnimations()
        {
            foreach (Animation playingAnimation in m_playingAnimations)
            {
                playingAnimation.Rewind();
                playingAnimation.Sample();
                playingAnimation.Stop();
            }
        }

        // 06003336
        public UIContainerMissionMusicPlayer() { }
    }
}
