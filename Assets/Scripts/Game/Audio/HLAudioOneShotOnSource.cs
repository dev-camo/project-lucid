using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
using Random = UnityEngine.Random;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001b5. Clips are played straight on the
    // serialized AudioSource once every requested clip has reported back.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HLAudioOneShotOnSource : MonoBehaviour
    {
        private enum PlayMode
        {
            All,
            Random
        }

        [SerializeField] private AudioSource m_audioSource;
        [SerializeField]
        [HashEnum(typeof(HLAudioClipIdentifier))]
        private HLAudioClipIdentifier[] m_oneShotIds;
        [SerializeField] private PlayMode m_playMode;
        [SerializeField] private bool m_preloadAudio = true;
        private readonly SystemRef<AudioManager> m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>();
        private readonly List<AudioClip> m_loadedClips = new List<AudioClip>();
        private bool m_loadClipsCalled;
        private Coroutine m_waitForLoadRoutine;

        private void Awake()
        {
            if (m_preloadAudio) LoadClips();
        }

        // Every loaded clip is appended, a null one included.
        private void LoadClips()
        {
            if (m_loadClipsCalled) return;
            AudioManager audioManager = m_audioManagerRef.Get();
            foreach (HLAudioClipIdentifier oneShotId in m_oneShotIds)
                audioManager.GetClip(oneShotId, loadedAudioParameters => m_loadedClips.Add(loadedAudioParameters.Clip));
            m_loadClipsCalled = true;
        }

        public void PlayOneShots()
        {
            if (m_waitForLoadRoutine != null) return;
            if (ClipsAreLoaded())
            {
                Internal_PlayOneShots();
                return;
            }
            if (!m_preloadAudio) LoadClips();
            m_waitForLoadRoutine = StartCoroutine(WaitForClipsToLoadThenPlay());
        }

        private IEnumerator WaitForClipsToLoadThenPlay()
        {
            yield return new WaitUntil(ClipsAreLoaded);
            Internal_PlayOneShots();
            m_waitForLoadRoutine = null;
        }

        private void Internal_PlayOneShots()
        {
            switch (m_playMode)
            {
                case PlayMode.All:
                    foreach (AudioClip clip in m_loadedClips)
                        m_audioSource.PlayOneShot(clip);
                    break;
                case PlayMode.Random:
                    m_audioSource.PlayOneShot(m_loadedClips[Random.Range(0, m_loadedClips.Count)]);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // Counts reported clips, so repeated identifiers each need their own report.
        private bool ClipsAreLoaded() => m_loadedClips.Count == m_oneShotIds.Length;

        private void Reset()
        {
            m_audioSource = GetComponent<AudioSource>();
        }
    }
}
