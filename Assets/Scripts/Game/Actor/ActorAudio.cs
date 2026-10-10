using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.Audio;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Full original ActorAudio schema and native-derived bodies. This private source
    // remains unaccepted until its genuine AudioManager and LevelManager graph closes.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ActorAudio : TimeScaledComponent_SDT
    {
        [Tooltip("Defines the available sources on which to play audio simultaneously.")]
        [SerializeField] private List<AudioSource> m_audioSources = new List<AudioSource>();
        [SerializeField]
        [Tooltip("Plays audio immediately and does not cancel clips that are already being played by AudioSource.PlayOneShot and AudioSource.Play")]
        private AudioSource m_oneShotAudioSource;
        [Tooltip("Plays VO audio immediately and does not cancel clips that are already being played by AudioSource.PlayOneShot and AudioSource.Play")]
        [SerializeField] private AudioSource m_oneShotAudioVOSource;
        [Tooltip("Defines the available sources on which to play raw audio simultaneously.")]
        [SerializeField] private List<AudioSource> m_audioRawSources = new List<AudioSource>();
        [SerializeField]
        [Tooltip("Plays raw audio immediately and does not cancel clips that are already being played by AudioSource.PlayOneShot and AudioSource.Play")]
        private AudioSource m_oneShotAudioRawSource;
        [SerializeField] private AudioMixerSnapshot m_mixerSnapshotDefault;
        [SerializeField] private float m_mixerSnapshotDefaultTransitionTime = 1f;
        [SerializeField] private AudioMixerSnapshot m_mixerSnapshotFilterChaosAir;
        [SerializeField] private AudioMixerSnapshot m_mixerSnapshotFilterChaosGround;
        [SerializeField] private float m_mixerSnapshotFilterTransitionTime = 1f;
        private ActorAudioLookup m_lookup;
        private AudioManager m_audioManager;
        private readonly List<ActorAudioTypes> m_pendingClips = new List<ActorAudioTypes>();
        private readonly List<DelayedAudio> m_delayedAudios = new List<DelayedAudio>();
        private bool m_isPaused;
        private LevelManager m_levelManager;
        private LevelSetupTypes m_setupType;
        private int m_slotIndex;
        private int m_slotCount;
        private int m_slotRawIndex;
        private int m_slotRawCount;
        private readonly Dictionary<ActorAudioTypes, int> m_audioSourceSlot = new Dictionary<ActorAudioTypes, int>(HardlightEnumComparers.ActorAudioTypesComparer);
        private readonly Dictionary<ActorAudioTypes, int> m_audioRawSourceSlot = new Dictionary<ActorAudioTypes, int>(HardlightEnumComparers.ActorAudioTypesComparer);

        private struct DelayedAudio
        {
            public float ElapsedSeconds;
            public readonly ActorAnimationDefinition.AudioParameter AudioParameter;
            public DelayedAudio(ActorAnimationDefinition.AudioParameter audioParameter)
            {
                ElapsedSeconds = 0f;
                AudioParameter = audioParameter;
            }
        }

        // 0600037f publishes lookup before resolving the registered genuine manager.
        public void Initialise(ActorAudioLookup lookup)
        {
            m_lookup = lookup;
            m_audioManager = ProcessManager.GetSystem<AudioManager>(null, true);
            m_slotCount = m_audioSources.Count;
            m_slotRawCount = m_audioRawSources.Count;
        }

        public void ProcessOnEnterActions(ActorAnimationDefinition animationDefinition)
        {
            if (animationDefinition == null) return;
            foreach (ActorAnimationDefinition.AudioParameter audioParameter in animationDefinition.Audio)
                ProcessClipAction(audioParameter.onEnter, audioParameter);
        }

        public void Play(ActorAnimationDefinition animationDefinition) => ProcessOnEnterActions(animationDefinition);
        public void Stop(ActorAnimationDefinition animationDefinition) => ProcessOnLeaveActions(animationDefinition);

        private void ProcessClipAction(ActorAnimationDefinition.AudioParameter.ClipAction action, ActorAnimationDefinition.AudioParameter audioParameter)
        {
            switch (action)
            {
                case ActorAnimationDefinition.AudioParameter.ClipAction.None: return;
                case ActorAnimationDefinition.AudioParameter.ClipAction.Play:
                    // Original ARM b.le sends unordered delays through immediate play.
                    if (audioParameter.DelaySeconds > 0f)
                        m_delayedAudios.Add(new DelayedAudio(audioParameter));
                    else
                        PlayClip(audioParameter.Clip, audioParameter.BehaviourOn);
                    return;
                case ActorAnimationDefinition.AudioParameter.ClipAction.Stop:
                    StopClip(audioParameter.Clip);
                    return;
                default:
                    HLOutput.LogError(string.Format("Enum does not handle {0}, please extend.", action), this);
                    return;
            }
        }

        public void ProcessOnLeaveActions(ActorAnimationDefinition animationDefinition)
        {
            if (animationDefinition == null) return;
            foreach (ActorAnimationDefinition.AudioParameter audioParameter in animationDefinition.Audio)
                ProcessClipAction(audioParameter.onLeave, audioParameter);
        }

        public void PlayOneShot(ActorAudioTypes actorAudioType) => PlayClip(actorAudioType, ActorAnimationDefinition.AudioParameter.BehaviourOfClip.OneShot);

        private void PlayClip(ActorAudioTypes actorAudioType, ActorAnimationDefinition.AudioParameter.BehaviourOfClip behaviour)
        {
            if (actorAudioType == 0 || !m_lookup.TryGet(actorAudioType, out HLAudioClipIdentifier? clip, m_setupType) || !clip.HasValue || clip.Value == 0)
                return;
            m_pendingClips.Add(actorAudioType);
            // Original callback removes one pending occurrence before evaluating Unity
            // clip equality, even when removal is false; no cancellation generation exists.
            m_audioManager.GetClip(clip.Value, loadedAudioParameters =>
            {
                AudioClip audioClip = loadedAudioParameters.Clip;
                bool pending = m_pendingClips.Remove(actorAudioType);
                bool missingClip = audioClip == null;
                if (!pending || missingClip) return;
                if (behaviour == ActorAnimationDefinition.AudioParameter.BehaviourOfClip.OneShot)
                    PlayOneShot(audioClip, m_oneShotAudioSource, m_audioSources, m_slotIndex, m_slotCount);
                if (behaviour == ActorAnimationDefinition.AudioParameter.BehaviourOfClip.OneShotRaw)
                    PlayOneShot(audioClip, m_oneShotAudioRawSource, m_audioRawSources, m_slotRawIndex, m_slotRawCount);
                if (behaviour == ActorAnimationDefinition.AudioParameter.BehaviourOfClip.OneShotVO && m_oneShotAudioVOSource != null)
                    m_oneShotAudioVOSource.PlayOneShot(audioClip);
                if (behaviour == ActorAnimationDefinition.AudioParameter.BehaviourOfClip.Loop && !m_audioSources.Exists(source => source.clip == audioClip))
                    m_slotIndex = SetAudioSlot(actorAudioType, audioClip, behaviour, m_audioSources, m_slotIndex, m_slotCount, m_audioSourceSlot);
                if (behaviour == ActorAnimationDefinition.AudioParameter.BehaviourOfClip.LoopRaw && !m_audioRawSources.Exists(source => source.clip == audioClip))
                    m_slotRawIndex = SetAudioSlot(actorAudioType, audioClip, behaviour, m_audioRawSources, m_slotRawIndex, m_slotRawCount, m_audioRawSourceSlot);
            });
        }

        private static bool PlayOneShot(AudioClip audioClip, AudioSource oneShotAudioSource, List<AudioSource> audioSources, int slotIndex, int slotCount)
        {
            AudioSource source;
            if (oneShotAudioSource != null) source = oneShotAudioSource;
            else
            {
                if (audioSources == null || slotCount < 1) return false;
                source = audioSources[slotIndex];
            }
            source.PlayOneShot(audioClip);
            return true;
        }

        private int SetAudioSlot(ActorAudioTypes actorAudioType, AudioClip audioClip, ActorAnimationDefinition.AudioParameter.BehaviourOfClip behaviour, List<AudioSource> audioSources, int slotIndex, int slotCount, Dictionary<ActorAudioTypes, int> audioSourceSlot)
        {
            AudioSource source = audioSources[slotIndex];
            source.clip = audioClip;
            // Raw-loop mode does not set the loop flag in this original shared helper.
            source.loop = behaviour == ActorAnimationDefinition.AudioParameter.BehaviourOfClip.Loop;
            audioSourceSlot[actorAudioType] = slotIndex;
            if (!m_isPaused) source.Play();
            int next = slotIndex;
            do
            {
                next = unchecked(next + 1) % slotCount;
                AudioSource nextSource = audioSources[next];
                if (nextSource != null)
                {
                    if (nextSource.clip == null) break;
                    // The original still reads loop when it has circled to slotIndex.
                    bool looping = nextSource.loop;
                    if (next == slotIndex || !looping) break;
                }
            } while (next != slotIndex);
            return next;
        }

        public void StopClip(ActorAudioTypes actorAudioClip)
        {
            if (actorAudioClip == 0 || !m_lookup.TryGet(actorAudioClip, out HLAudioClipIdentifier? clip, m_setupType) || !clip.HasValue) return;
            m_pendingClips.Remove(actorAudioClip);
            if (m_audioSourceSlot.Remove(actorAudioClip, out int slotIndex))
            {
                AudioSource source = m_audioSources[slotIndex];
                if (source != null) { source.Stop(); source.clip = null; }
            }
            if (m_audioRawSourceSlot.Remove(actorAudioClip, out int slotRawIndex))
            {
                AudioSource source = m_audioRawSources[slotRawIndex];
                if (source != null) { source.Stop(); source.clip = null; }
            }
        }

        public void ClearDelayedAudio() => m_delayedAudios.Clear();

        private static void StopAudio(List<AudioSource> audioSources)
        {
            foreach (AudioSource source in audioSources) { source.Stop(); source.clip = null; }
        }

        public void StopAnyAudio()
        {
            StopAudio(m_audioSources);
            m_audioSourceSlot.Clear();
            m_slotIndex = 0;
            StopAudio(m_audioRawSources);
            m_audioRawSourceSlot.Clear();
            m_slotRawIndex = 0;
        }

        protected override void InternalUpdate(float deltaTime)
        {
            for (int index = m_delayedAudios.Count - 1; index >= 0; --index)
            {
                DelayedAudio delayed = m_delayedAudios[index];
                delayed.ElapsedSeconds += deltaTime;
                // Ordered >= preserves ARM b.lt's writeback path for NaN inputs.
                if (delayed.ElapsedSeconds >= delayed.AudioParameter.DelaySeconds)
                {
                    PlayClip(delayed.AudioParameter.Clip, delayed.AudioParameter.BehaviourOn);
                    m_delayedAudios.RemoveAt(index);
                }
                else m_delayedAudios[index] = delayed;
            }
        }

        public void SetActive(bool active)
        {
            SetActive(active, m_audioSources);
            SetActive(active, m_audioRawSources);
            if (!active)
            {
                if (m_oneShotAudioSource != null) m_oneShotAudioSource.Stop();
                if (m_oneShotAudioRawSource != null) m_oneShotAudioRawSource.Stop();
                if (m_oneShotAudioVOSource != null) m_oneShotAudioVOSource.Stop();
            }
            m_isPaused = !active;
        }

        private static void SetActive(bool active, List<AudioSource> audioSources)
        {
            foreach (AudioSource source in audioSources)
                if (source.clip != null)
                {
                    if (active) source.Play();
                    else source.Pause();
                }
        }

        protected virtual void Start() => ProcessManager.GetSystemRef<LevelManager>(null, true).InvokeOnValid(OnLevelManagerRegistered);

        public override void OnDestroy()
        {
            if (m_levelManager != null) m_levelManager.RemoveLevelActivatedAction(OnLevelActivated);
            base.OnDestroy();
        }

        private void OnLevelManagerRegistered(LevelManager levelManager)
        {
            m_levelManager = levelManager;
            m_levelManager.InvokeOnLevelActivated(OnLevelActivated, true);
        }

        private void OnLevelActivated(LevelManagerLevel levelManagerLevel)
        {
            m_levelManager.RemoveLevelActivatedAction(OnLevelActivated);
            m_setupType = levelManagerLevel.LevelDefinition.LevelSetupType;
        }

        public void ActivateFilter(bool enable, bool isAir)
        {
            if (enable)
            {
                AudioMixerSnapshot snapshot = isAir ? m_mixerSnapshotFilterChaosAir : m_mixerSnapshotFilterChaosGround;
                if (snapshot != null) snapshot.TransitionTo(m_mixerSnapshotFilterTransitionTime);
            }
            else if (m_mixerSnapshotDefault != null) m_mixerSnapshotDefault.TransitionTo(m_mixerSnapshotDefaultTransitionTime);
        }

        // 06000395 field initializers above publish lists, the two 1f transition
        // times and both original-comparer dictionaries before the genuine base.
        public ActorAudio() { }


    }
}
