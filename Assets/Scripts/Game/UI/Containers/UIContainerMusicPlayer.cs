using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Game.Runtime 020008ec. The twelve compiler-generated APIs remain
    // separately subject to original iterator, lambda and field binding.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerMusicPlayer : UIContainer
    {
        [SerializeField] private List<UIWidgetButtonMusic> m_playerButtons;
        [SerializeField] private List<UIWidgetToggleMusicPlayer> m_playerToggles;
        [SerializeField] private UIWidgetToggleMusicPlayer m_playToggle;
        [SerializeField] private UIWidgetToggleMusicPlayer m_repeatToggle;
        [SerializeField] private UIWidgetToggleMusicPlayer m_shuffleToggle;
        [SerializeField] private Animation[] m_playingAnimations;
        [SerializeField] private RewardUnlockStringPicker m_rewardUnlockStringPicker;
        public readonly Bindable<Sprite> TrackImage = new Bindable<Sprite>();
        public readonly Bindable<string> TrackName = new Bindable<string>();
        public readonly Bindable<Strings> TrackLockedZone = new Bindable<Strings>();
        public readonly Bindable<Strings> TrackLockedAct = new Bindable<Strings>();
        public readonly Bindable<string> TrackCurrentTimeFormatted = new Bindable<string>();
        public readonly Bindable<string> TrackTotalTimeFormatted = new Bindable<string>();
        public readonly Bindable<float> TrackTotalTime = new Bindable<float>();
        public readonly Bindable<float> TrackSliderValue = new Bindable<float>();
        public readonly Bindable<bool> ShowTrackAnimation = new Bindable<bool>();
        public readonly Bindable<bool> TrackLocked = new Bindable<bool>();
        public readonly Bindable<bool> TrackLockedByChallenge = new Bindable<bool>();
        // Original 06003386/87 and cache 060033a4..a6: atomic event accessors
        // and the original empty initial subscriber.
        public event Action<MusicTrackEndEvent> OnMusicStopped = _ => { };
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
        private readonly SystemRef<AudioManager> m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>(null, true);
        private const HLAudioSourceIdentifier DefaultAudioSource = HLAudioSourceIdentifier.Jukebox;
        private const HLAudioSourceIdentifier BackgroundAudioSource = HLAudioSourceIdentifier.UiBackground;
        private HLAudioClipIdentifier m_currentlyPlayingClip;
        private UIContainerMusicPlayerParameters m_parameters;
        private HLAudioSourceData m_audioSourceData;
        private bool m_isPlayingMusic;
        private bool m_isRepeating;
        private bool m_isShuffling;
        private bool m_wasIdleStart;
        private bool m_trackSkipRequested;
        private float m_clipTime;
        private float m_initialBackgroundSourceVolume;
        private float m_timeListeningSeconds;

        // Original 06003388; coroutine publication is unconditional.
        private void Start() { StartCoroutine(ListenForOnClipEnded()); }

        // Original 06003389: capture one manager, background volume before fade,
        // then publish the jukebox source before Pause and SetLooping.
        private void OnEnable()
        {
            var audioManager = m_audioManagerRef.Get();
            if (audioManager.TryGetAudioSourceData(BackgroundAudioSource, out var backgroundSource))
            {
                m_initialBackgroundSourceVolume = backgroundSource.GetVolume();
                audioManager.FadeOutSource(BackgroundAudioSource);
            }
            if (audioManager.TryGetAudioSourceData(DefaultAudioSource, out var audioSource))
            {
                m_audioSourceData = audioSource;
                m_audioSourceData.Pause();
                m_audioSourceData.SetLooping(false);
            }
        }

        // Original 0600338a: all cleanup, including toggle enumeration, is
        // inside the original successful lookup and non-destroyed source gate.
        private void OnDisable()
        {
            if (m_audioManagerRef.TryGet(out var audioManager)
                && audioManager.TryGetAudioSourceData(DefaultAudioSource, out var audioSource)
                && !audioSource.IsDestroyed())
            {
                Pause(MusicStoppedReason.Menu_Exit);
                RestoreAudio();
                m_currentlyPlayingClip = HLAudioClipIdentifier.None;
                m_isPlayingMusic = false;
                m_isRepeating = false;
                m_isShuffling = false;
                foreach (var toggle in m_playerToggles) toggle.Toggle.IsOn = false;
            }
        }

        // Original 0600338b: preserve the custom definition equality, original
        // UNKNOWN string keys, captured track definition and fresh callback fields.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            m_parameters = parameters.GetAs<UIContainerMusicPlayerParameters>();
            bool locked = m_parameters.Locked;
            bool lockedByChallenge = m_parameters.LevelDefinition == null;
            var levelManager = m_levelManagerRef.Get();
            bool unlocked = !locked || lockedByChallenge;
            Strings zoneName = Strings.COMMON_MENU_UNKNOWN;
            Strings actName = Strings.COMMON_MENU_UNKNOWN;
            if (unlocked)
            {
                if (lockedByChallenge && m_rewardUnlockStringPicker != null)
                    m_rewardUnlockStringPicker.UpdateStringFromRewardGUID(m_parameters.MusicTrackDefinition.GetGUID());
            }
            else
            {
                var levelDefinition = m_parameters.LevelDefinition;
                if (levelManager.GameLevels.TryGetZoneForLevel(levelDefinition, out var zone))
                {
                    zoneName = zone.Title;
                    actName = levelDefinition.ActName;
                }
            }
            foreach (var button in m_playerButtons)
            {
                if (locked) button.Lock();
                else button.Unlock();
            }
            foreach (var toggle in m_playerToggles)
            {
                if (locked) toggle.Lock();
                else toggle.Unlock();
            }
            var trackDefinition = m_parameters.MusicTrackDefinition;
            string trackName = StringTable.GetString(trackDefinition.Name);
            TrackName.Value = string.Format("{0}. {1}", m_parameters.Number, trackName);
            TrackLockedZone.Value = zoneName;
            TrackLockedAct.Value = actName;
            TrackLocked.Value = !unlocked;
            TrackLockedByChallenge.Value = lockedByChallenge;
            ShowTrackAnimation.Value = !locked;
            trackDefinition.ImageAsset.LoadAsync(sprite => TrackImage.Value = sprite);
            ResetTrackValues();
            ResetPlayingAnimations();
            m_audioManagerRef.Get().GetClip(trackDefinition.ClipIdentifier, SetupAudioBindings);
            if (m_isRepeating)
            {
                m_repeatToggle.Toggle.IsOn = true;
                m_repeatToggle.Toggle.Transition.QueueTransitionToState(UITransitionState.Selected);
            }
            if (m_isShuffling)
            {
                m_shuffleToggle.Toggle.IsOn = true;
                m_shuffleToggle.Toggle.Transition.QueueTransitionToState(UITransitionState.Selected);
            }
            if (!locked && m_isPlayingMusic)
            {
                m_playToggle.Toggle.IsOn = true;
                m_playToggle.Toggle.Transition.QueueTransitionToState(UITransitionState.Selected);
                if (m_audioSourceData.IsPlaying()
                    && m_parameters.MusicTrackDefinition.ClipIdentifier != m_currentlyPlayingClip)
                    RegisterTrackEnd(m_trackSkipRequested ? MusicStoppedReason.Skipped : MusicStoppedReason.Deselected);
                Play();
            }
            m_trackSkipRequested = false;
        }

        // Original 0600338c: the minutes component wraps each hour. The slider
        // receives elapsed seconds; callback faults prevent the listening-time update.
        private void Update()
        {
            if (m_isPlayingMusic)
            {
                float trackTime = m_audioSourceData.GetTime();
                var timeSpan = TimeSpan.FromSeconds(trackTime);
                TrackCurrentTimeFormatted.Value = string.Format("{0:00}:{1:00}", timeSpan.Minutes, timeSpan.Seconds);
                TrackSliderValue.Value = trackTime;
                m_timeListeningSeconds += Time.deltaTime;
            }
        }

        // Original 0600338d.
        public void Action_TogglePlay(bool play) { if (play) Play(); else Pause(); }
        // Original 0600338e: retain the field reread after the audio call.
        public void Action_ToggleRepeat(bool repeat)
        {
            m_isRepeating = repeat;
            m_audioSourceData.SetLooping(repeat);
            if (m_isRepeating) m_shuffleToggle.Toggle.IsOn = false;
        }
        // Original 0600338f.
        public void Action_StartSeek() { m_audioSourceData.Mute(); }
        // Original 06003390.
        public void Action_Seek(float trackTime) { m_audioSourceData.Seek(trackTime); m_clipTime = trackTime; }
        // Original 06003391: the supplied trackTime is unused.
        public void Action_FinishSeek(float trackTime) { m_audioSourceData.Unmute(); }
        // Original 06003392: shuffle does not set the skip flag.
        public void Action_PlayNext()
        {
            if (m_isShuffling) RandomlyChooseNextSong();
            else { m_trackSkipRequested = true; m_parameters.PlayNext(); }
        }
        // Original 06003393: publish the skip flag before the mandatory callback.
        public void Action_PlayPrevious() { m_trackSkipRequested = true; m_parameters.PlayPrevious(); }
        // Original 06003394.
        public void Action_ToggleShuffle(bool shuffle)
        {
            m_isShuffling = shuffle;
            if (shuffle) m_repeatToggle.Toggle.IsOn = false;
        }
        // Original 06003395: reacquire the original manager.
        private void RestoreAudio() { m_audioManagerRef.Get().FadeInSource(BackgroundAudioSource, m_initialBackgroundSourceVolume); }

        // Original 06003396: false state precedes playback; animation and seek
        // faults retain it. The old clip field is reread after animation playback.
        private void Play()
        {
            var clipIdentifier = m_parameters.MusicTrackDefinition.ClipIdentifier;
            if (m_isPlayingMusic && clipIdentifier == m_currentlyPlayingClip) return;
            m_isPlayingMusic = false;
            m_audioManagerRef.Get().PlayClipAtSource(clipIdentifier, DefaultAudioSource, false, null);
            PlayPlayingAnimations();
            Seek(m_currentlyPlayingClip == HLAudioClipIdentifier.None || m_currentlyPlayingClip == clipIdentifier ? m_clipTime : 0);
            m_isPlayingMusic = true;
            m_currentlyPlayingClip = clipIdentifier;
            m_audioSourceData.SetLooping(m_isRepeating);
        }

        // Original 06003397: callback faults stop the later time capture, Pause,
        // state clear and animation reset. The original optional default is Paused.
        private void Pause(MusicStoppedReason musicStoppedReason = MusicStoppedReason.Paused)
        {
            if (m_audioSourceData.IsPlaying()) RegisterTrackEnd(musicStoppedReason);
            m_clipTime = m_audioSourceData.GetTime();
            m_audioSourceData.Pause();
            m_isPlayingMusic = false;
            ResetPlayingAnimations();
        }
        // Original 06003398: the time field is stored after the source call.
        private void Seek(float trackTime) { m_audioSourceData.Seek(trackTime); m_clipTime = trackTime; }

        // Original 06003399: the clip is captured once but length is read twice,
        // on opposite sides of the first bindable callback.
        private void SetupAudioBindings(HLAudioClipHandler.LoadedAudioParameters audioParameters)
        {
            var clip = audioParameters.Clip;
            TrackTotalTime.Value = clip.length;
            var timeSpan = TimeSpan.FromSeconds(clip.length);
            TrackTotalTimeFormatted.Value = string.Format("{0:00}:{1:00}", timeSpan.Minutes, timeSpan.Seconds);
        }
        // Original 0600339a.
        private void ResetTrackValues() { TrackCurrentTimeFormatted.Value = "00:00"; TrackSliderValue.Value = 0; }

        // Original 0600339b, instance predicates 060033a2/3 and iterator a7..ac.
        // No source hoisted local beyond the original captured instance is needed.
        private IEnumerator ListenForOnClipEnded()
        {
            while (true)
            {
                yield return new WaitUntil(() => m_isPlayingMusic);
                yield return new WaitUntil(() => !m_audioSourceData.IsPlaying() && m_audioSourceData.IsAtEnd());
                RegisterTrackEnd(MusicStoppedReason.Completed);
                m_clipTime = 0;
                if (m_isShuffling) RandomlyChooseNextSong();
                else if (!m_isRepeating)
                {
                    ResetPlayingAnimations();
                    ResetTrackValues();
                    Seek(0);
                    m_parameters.PlayNext();
                }
            }
        }

        // Original 0600339c: capture the event delegate before argument getters,
        // invoke it before publishing idle state and clearing listening seconds.
        private void RegisterTrackEnd(MusicStoppedReason reason)
        {
            OnMusicStopped(new MusicTrackEndEvent(reason, TrackTotalTime.Value, m_timeListeningSeconds, m_wasIdleStart));
            m_wasIdleStart = reason == MusicStoppedReason.Completed;
            m_timeListeningSeconds = 0;
        }
        // Original 0600339d; the original callback is mandatory.
        private void RandomlyChooseNextSong() { m_parameters.Shuffle(); }
        // Original 0600339e.
        private void PlayPlayingAnimations() { foreach (var animation in m_playingAnimations) animation.Play(); }
        // Original 0600339f: retain captured-array order Rewind, Sample, Stop.
        public void ResetPlayingAnimations()
        {
            foreach (var animation in m_playingAnimations) { animation.Rewind(); animation.Sample(); animation.Stop(); }
        }
        // Original 060033a0: the ordered field initializers precede UIContainer construction.
        public UIContainerMusicPlayer() { }
    }
}
