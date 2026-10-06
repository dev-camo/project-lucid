using System.Collections;
using System.Collections.Generic;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001c1. This is the original source wrapper,
    // including its untracked one-shot delay and retained start-time list.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLAudioSourceData
    {
        public HLAudioSourceIdentifier Identifier { get; }
        public AudioLowPassFilter LowPassFilter { get; }
        public bool HasLowPassFilter { get; }
        public bool IsGameplayLevelSource { get; }
        public HLAudioSourceTransition Transition { get; }
        public AudioFadeTransitionSettings FadeTransitionSettings { get; }
        public HLAudioClipIdentifier CurrentAudio { get; private set; }

        private IReadOnlyList<float> m_startTimes;
        private bool m_startTimesSet;
        private bool m_playingOneShot;
        private bool m_oneShotClipLoading;
        private float m_endTime;
        private Coroutine m_oneShotIsPlayingRoutine;

        public bool IsRepeating => m_audioSource.loop;
        private AudioSource m_audioSource { get; }
        private readonly AudioFade m_currentFade = new AudioFade();

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class AudioFade
        {
            public float StartingVolume;
            public Coroutine FadeCoroutine;

            // Original 0x0600096b; base-only constructor.
            public AudioFade() { }
        }

        // Original 0x0600094e / arm64 0x6c8968. The fade object initializer
        // precedes the Object base call; the low-pass presence uses Unity null.
        public HLAudioSourceData(HLAudioSourceIdentifier identifier, AudioSource audioSource,
            bool isGameplayLevelSource, HLAudioSourceTransition transition,
            AudioFadeTransitionSettings fadeTransitionSettings)
        {
            Identifier = identifier;
            Transition = transition;
            FadeTransitionSettings = fadeTransitionSettings;
            m_audioSource = audioSource;
            LowPassFilter = audioSource.GetComponent<AudioLowPassFilter>();
            HasLowPassFilter = LowPassFilter != null;
            IsGameplayLevelSource = isGameplayLevelSource;
            CurrentAudio = 0;
        }

        // Original 0x0600094f. Reads gameObject again after the null comparison.
        public void Destroy()
        {
            if (m_audioSource == null) return;
            if (m_audioSource.gameObject == null) return;
            Object.Destroy(m_audioSource.gameObject);
        }

        public bool IsValid() => m_audioSource != null && m_audioSource.gameObject != null;

        // Original 0x06000951. Disabling random starts retains the old list.
        public void SetAudioClip(AudioClip audioClip, HLAudioClipIdentifier identifier,
            bool startAtRandomTime, IReadOnlyList<float> startTimes = null)
        {
            m_audioSource.clip = audioClip;
            CurrentAudio = identifier;
            if (startAtRandomTime) m_startTimes = startTimes;
            m_startTimesSet = startAtRandomTime;
        }

        // Original 0x06000952. Capture the source receiver before list access,
        // then reload it for Play. Empty/null authored lists have no fallback.
        public void Play()
        {
            if (m_startTimesSet)
            {
                AudioSource source = m_audioSource;
                IReadOnlyList<float> startTimes = m_startTimes;
                source.time = startTimes[Random.Range(0, startTimes.Count)];
            }
            m_audioSource.Play();
        }

        public void SetPan(float pan) => m_audioSource.panStereo = pan;
        public void SetOneShotClipLoading(bool loading) => m_oneShotClipLoading = loading;

        // Original 0x06000955 and natural lambda 0x0600096a. The returned delay
        // handle is discarded. Earlier overlapping callbacks may clear this flag
        // while a later one-shot is still playing; Stop/Pause do not cancel it.
        public void PlayOneShot(AudioClip audioClip, float volumeScale)
        {
            m_audioSource.PlayOneShot(audioClip, volumeScale);
            m_playingOneShot = true;
            CoroutineUtils.Delay(() => m_playingOneShot = false, audioClip.length);
        }

        public bool IsMuted() => m_audioSource.mute;
        public void Mute() => m_audioSource.mute = true;
        public void Unmute() => m_audioSource.mute = false;
        public float GetVolume() => m_audioSource.volume;
        public void SetVolume(float volume) => m_audioSource.volume = volume;
        public void Seek(float trackTime) => m_audioSource.time = trackTime;
        public float GetTime() => m_audioSource.time;
        public bool IsPlaying() => m_audioSource.isPlaying;
        public bool IsPlayingOrLoadingOneShot() => m_playingOneShot || m_oneShotClipLoading;
        public bool IsPlayingClip(AudioClip clip) => m_audioSource.isPlaying && m_audioSource.clip == clip;
        public bool IsPlayingClip(HLAudioClipIdentifier clip) => m_audioSource.isPlaying && CurrentAudio == clip;
        public void SetLooping(bool loop) => m_audioSource.loop = loop;
        public void MoveTo(Vector3 position) => m_audioSource.gameObject.transform.position = position;
        public void Stop() => m_audioSource.Stop();
        public void Pause() => m_audioSource.Pause();

        // Original 0x06000965. The current fade receiver is captured before
        // reading volume and before RunCoroutine can synchronously advance it.
        public void StartFade(IEnumerator fade)
        {
            AudioFade currentFade = m_currentFade;
            if (currentFade.FadeCoroutine != null)
            {
                CoroutineUtils.StopUtilCoroutine(ref currentFade.FadeCoroutine);
                m_audioSource.volume = m_currentFade.StartingVolume;
                currentFade = m_currentFade;
            }
            currentFade.StartingVolume = m_audioSource.volume;
            m_currentFade.FadeCoroutine = CoroutineUtils.RunCoroutine(PerformFade(fade));
        }

        // Original 0x06000966; original natural iterator <PerformFade>d__59.
        // Yield the nested enumerator itself, then clear the handle on normal
        // completion only. Dispose does not execute a cleanup/finally block.
        private IEnumerator PerformFade(IEnumerator fade)
        {
            yield return fade;
            m_currentFade.FadeCoroutine = null;
        }

        public void StopFade()
        {
            AudioFade currentFade = m_currentFade;
            if (currentFade.FadeCoroutine == null) return;
            CoroutineUtils.StopUtilCoroutine(ref currentFade.FadeCoroutine);
            m_audioSource.volume = m_currentFade.StartingVolume;
        }

        // Original 0x06000968. A missing source returns false, but an existing
        // source with no clip still dereferences clip.length; no loop special case.
        public bool IsAtEnd()
        {
            if (m_audioSource == null) return false;
            return m_audioSource.time >= m_audioSource.clip.length;
        }
        public bool IsDestroyed() => m_audioSource == null;
    }
}
