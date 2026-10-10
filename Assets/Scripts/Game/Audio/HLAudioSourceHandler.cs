using System.Collections;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001c4. Full native-derived source candidate;
    // registration still requires the genuine AudioManager startup graph.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HLAudioSourceHandler : MonoBehaviour
    {
        private Dictionary<HLAudioSourceIdentifier, HLAudioSourceData> m_audioSourcesData;
        private Transform m_parent;
        private readonly Dictionary<HLAudioSourceIdentifier, int> m_playingConcurrentlyCount =
            new Dictionary<HLAudioSourceIdentifier, int>(HardlightEnumComparers.HLAudioSourceIdentifierComparer);

        // Original 0x06000972. Reinitialization leaves concurrent shot counts intact.
        public void Initialise(Transform parent)
        {
            m_parent = parent;
            m_audioSourcesData = new Dictionary<HLAudioSourceIdentifier, HLAudioSourceData>(
                HardlightEnumComparers.HLAudioSourceIdentifierComparer);
        }

        // Original 0x06000973; Instantiate(original, parent), with default worldPositionStays.
        public void InstantiateSource(HLAudioSourceRegisterComponent audioSourcePrefab)
        {
            if (m_audioSourcesData.ContainsKey(audioSourcePrefab.AudioSourceIdentifier)) return;
            Object.Instantiate(audioSourcePrefab, m_parent);
        }

        public void Add(HLAudioSourceData hlAudioSourceData)
        {
            HLAudioSourceIdentifier identifier = hlAudioSourceData.Identifier;
            if (identifier == 0)
            {
                HLOutput.LogError("Adding an audio source for identifier " +
                    HardlightEnumExtensions.GetString((HLAudioSourceIdentifier)0) + " is invalid.");
                return;
            }
            if (m_audioSourcesData.ContainsKey(identifier)) return;
            m_audioSourcesData.Add(hlAudioSourceData.Identifier, hlAudioSourceData);
        }

        // Original 0x06000975. An explicit None overrides a configured Fade.
        // InstantSwap does not cancel an existing fade.
        public void Play(HLAudioSourceIdentifier identifier, AudioClip audioClip,
            HLAudioClipIdentifier audioClipIdentifier, bool startAtRandomTime,
            IReadOnlyList<float> startTimes, HLAudioSourceTransition? transition = null)
        {
            if (m_audioSourcesData.TryGetValue(identifier, out HLAudioSourceData sourceData))
            {
                if ((transition ?? sourceData.Transition) == HLAudioSourceTransition.Fade)
                    sourceData.StartFade(FadeBetween(sourceData, audioClip, audioClipIdentifier,
                        startAtRandomTime, startTimes));
                else
                    InstantSwap(sourceData, audioClip, audioClipIdentifier, startAtRandomTime, startTimes);
                return;
            }
            HLOutput.LogError(string.Concat(new[] { "Cannot play audio clip ",
                HardlightEnumExtensions.GetString(audioClipIdentifier), ", source ",
                HardlightEnumExtensions.GetString(identifier), " does not exist." }));
        }

        public void FadeOut(HLAudioSourceIdentifier identifier)
        {
            if (m_audioSourcesData.TryGetValue(identifier, out HLAudioSourceData sourceData))
                sourceData.StartFade(FadeOut(sourceData));
            else
                HLOutput.LogError("Cannot fade out source " +
                    HardlightEnumExtensions.GetString(identifier) + ", it does not exist.");
        }

        public void FadeIn(HLAudioSourceIdentifier identifier, float restoreVolume)
        {
            if (m_audioSourcesData.TryGetValue(identifier, out HLAudioSourceData sourceData))
                sourceData.StartFade(FadeIn(sourceData, restoreVolume));
            else
                HLOutput.LogError("Cannot fade in source " +
                    HardlightEnumExtensions.GetString(identifier) + ", it does not exist.");
        }

        public void LoadingOneShotClipForSource(HLAudioSourceIdentifier identifier)
        {
            if (m_audioSourcesData.TryGetValue(identifier, out HLAudioSourceData sourceData))
                sourceData.SetOneShotClipLoading(true);
            else
                HLOutput.LogError("Cannot load clip, the source " +
                    HardlightEnumExtensions.GetString(identifier) + " does not exist.");
        }

        // Original 0x06000979. Preserve repeated count/list reads and unchecked
        // counter arithmetic. Clear does not cancel these completion timers.
        public void PlayOneShot(HLAudioSourceIdentifier identifier, AudioClip audioClip,
            bool dampenVolume, IReadOnlyList<float> oneShotVolumeScale,
            float pan = 0f, float desiredVolumeScale = 1f)
        {
            if (identifier == 0)
            {
                HLOutput.LogError("Cannot play one shot, audio source with identifier " +
                    HardlightEnumExtensions.GetString((HLAudioSourceIdentifier)0) + " cannot be used.");
                return;
            }
            if (!m_audioSourcesData.TryGetValue(identifier, out HLAudioSourceData sourceData))
            {
                HLOutput.LogError(string.Concat(new[] { "Cannot play one shot of audio clip ",
                    audioClip.name, ", the source ", HardlightEnumExtensions.GetString(identifier),
                    " does not exist." }));
                return;
            }
            if (dampenVolume)
            {
                m_playingConcurrentlyCount.TryAdd(identifier, 0);
                if (m_playingConcurrentlyCount[identifier] > oneShotVolumeScale.Count)
                    desiredVolumeScale *= oneShotVolumeScale[oneShotVolumeScale.Count - 1];
                else if (m_playingConcurrentlyCount[identifier] != 0)
                    desiredVolumeScale *= oneShotVolumeScale[m_playingConcurrentlyCount[identifier] - 1];
                m_playingConcurrentlyCount[identifier]++;
                StartCoroutine(DecrementWhenComplete(identifier, audioClip.length));
            }
            sourceData.SetPan(pan);
            sourceData.PlayOneShot(audioClip, desiredVolumeScale);
            sourceData.SetOneShotClipLoading(false);
        }

        // Original 0x0600097a; natural <DecrementWhenComplete>d__11.
        private IEnumerator DecrementWhenComplete(HLAudioSourceIdentifier identifier, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            m_playingConcurrentlyCount[identifier]--;
        }

        // Original 0x0600097b. Snapshot keys; Destroy precedes dictionary reload/remove.
        public void Clear(bool gameplayLevelSourcesOnly = false)
        {
            HLAudioSourceIdentifier[] identifiers = new HLAudioSourceIdentifier[m_audioSourcesData.Count];
            m_audioSourcesData.Keys.CopyTo(identifiers, 0);
            foreach (HLAudioSourceIdentifier identifier in identifiers)
            {
                HLAudioSourceData sourceData = m_audioSourcesData[identifier];
                if (!gameplayLevelSourcesOnly || sourceData.IsGameplayLevelSource)
                {
                    sourceData.Destroy();
                    m_audioSourcesData.Remove(identifier);
                }
            }
        }

        public bool HasSource(HLAudioSourceIdentifier identifier) => m_audioSourcesData.ContainsKey(identifier);
        public bool TryGetAudioSourceData(HLAudioSourceIdentifier audioSourceIdentifier,
            out HLAudioSourceData audioSourceData) =>
            m_audioSourcesData.TryGetValue(audioSourceIdentifier, out audioSourceData);

        public bool IsPlayingAtSource(HLAudioSourceIdentifier sourceIdentifier, AudioClip clip)
        {
            if (sourceIdentifier == 0)
            {
                HLOutput.LogError("Cannot check if audio is playing, audio source with identifier " +
                    HardlightEnumExtensions.GetString((HLAudioSourceIdentifier)0) + " cannot be used.");
                return false;
            }
            if (m_audioSourcesData.TryGetValue(sourceIdentifier, out HLAudioSourceData sourceData))
                return sourceData.IsPlayingClip(clip);
            HLOutput.LogError("Cannot check if audio is playing, the source " +
                HardlightEnumExtensions.GetString(sourceIdentifier) + " does not exist.");
            return false;
        }

        public bool IsPlayingOneShot(HLAudioSourceIdentifier sourceIdentifier)
        {
            if (sourceIdentifier == 0)
            {
                HLOutput.LogError("Cannot check if audio is playing, audio source with identifier " +
                    HardlightEnumExtensions.GetString((HLAudioSourceIdentifier)0) + " cannot be used.");
                return false;
            }
            if (m_audioSourcesData.TryGetValue(sourceIdentifier, out HLAudioSourceData sourceData))
                return sourceData.IsPlayingOrLoadingOneShot();
            HLOutput.LogError("Cannot check if audio is playing, the source " +
                HardlightEnumExtensions.GetString(sourceIdentifier) + " does not exist.");
            return false;
        }

        public void MoveTo(HLAudioSourceIdentifier identifier, Vector3 position)
        {
            if (identifier == 0)
            {
                HLOutput.LogError("Cannot move audio source, audio source with identifier " +
                    HardlightEnumExtensions.GetString((HLAudioSourceIdentifier)0) + " cannot be used.");
                return;
            }
            if (m_audioSourcesData.TryGetValue(identifier, out HLAudioSourceData sourceData))
                sourceData.MoveTo(position);
            else
                HLOutput.LogError("Cannot move audio source, the source " +
                    HardlightEnumExtensions.GetString(identifier) + " does not exist.");
        }

        // Original 0x06000981; natural <FadeBetween>d__18. Nested iterators are
        // yielded, with no disposal/failure restoration inserted into the flow.
        private static IEnumerator FadeBetween(HLAudioSourceData sourceData, AudioClip newAudioClip,
            HLAudioClipIdentifier newAudioClipIdentifier, bool startAtRandomTime, IReadOnlyList<float> startTimes)
        {
            AudioFadeTransitionSettings fadeTransitionSettings = sourceData.FadeTransitionSettings;
            float initialVolume = sourceData.GetVolume();
            if (sourceData.CurrentAudio != 0)
                yield return Fade(sourceData, fadeTransitionSettings.FadeOutDuration, 0f);
            else
                sourceData.SetVolume(0f);
            InstantSwap(sourceData, newAudioClip, newAudioClipIdentifier, startAtRandomTime, startTimes);
            if (newAudioClipIdentifier != 0)
                yield return Fade(sourceData, fadeTransitionSettings.FadeInDuration, initialVolume);
            else
                sourceData.SetVolume(initialVolume);
        }

        private static IEnumerator FadeOut(HLAudioSourceData sourceData)
        {
            if (sourceData.CurrentAudio != 0)
                yield return Fade(sourceData, sourceData.FadeTransitionSettings.FadeOutDuration, 0f);
            else
                sourceData.SetVolume(0f);
        }

        private static IEnumerator FadeIn(HLAudioSourceData sourceData, float restoreVolume)
        {
            if (sourceData.CurrentAudio != 0)
                yield return Fade(sourceData, sourceData.FadeTransitionSettings.FadeInDuration, restoreVolume);
            else
                sourceData.SetVolume(restoreVolume);
        }

        private static void InstantSwap(HLAudioSourceData sourceData, AudioClip newAudioClip,
            HLAudioClipIdentifier identifier, bool startAtRandomTime, IReadOnlyList<float> startTimes)
        {
            sourceData.SetAudioClip(newAudioClip, identifier, startAtRandomTime, startTimes);
            if (identifier != 0) sourceData.Play();
            else sourceData.Stop();
        }

        // Original 0x06000985; natural <Fade>d__22. Read the initial volume even
        // for zero/negative/NaN duration; no final target-volume write is authored.
        private static IEnumerator Fade(HLAudioSourceData audioSource, float duration, float targetVolume)
        {
            float currentTime = 0f;
            float start = audioSource.GetVolume();
            while (currentTime < duration)
            {
                currentTime += Time.deltaTime;
                if (!audioSource.IsValid()) yield break;
                audioSource.SetVolume(Mathf.Lerp(start, targetVolume, currentTime / duration));
                yield return null;
            }
        }

        public void CancelAnyActiveFade(HLAudioSourceIdentifier identifier) => m_audioSourcesData[identifier].StopFade();
        public IReadOnlyCollection<HLAudioSourceData> GetAllAudioSources() => m_audioSourcesData.Values;

        // Original 0x06000988. The concurrent dictionary initializer precedes base.
        public HLAudioSourceHandler() { }
    }
}
