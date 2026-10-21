using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200097f/02000980; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class OrbDetectorUI : TimeScaledComponent_SDT
    {
        [SerializeField]
        private UIContainerGameplayIsland m_gameplayIsland;
        [Header("Orb detector pings")]
        [SerializeField]
        [Tooltip("Audio source to use to play ping noises.")]
        private HLAudioSourceIdentifier m_audioSource;
        [SerializeField]
        private List<OrbDetectorLevel> m_levels;
        [Header("Background music distortion")]
        [Tooltip("Audio source that low-pass filtering will be applied to.")]
        [SerializeField]
        private HLAudioSourceIdentifier m_gameplayMusicAudioSource;
        [Tooltip("Y axis: low-pass cutoff frequency to apply. X axis: orb proximity, where 0 = farthest, 1 = closest.")]
        [SerializeField]
        private AnimationCurve m_orbProximityMusicLowPassCutoff;
        [Tooltip("Curve to remove the low-pass filter effect after orb collection. Y axis: low-pass cutoff frequency. X axis: time in seconds since orb collection.")]
        [SerializeField]
        private AnimationCurve m_orbCollectionLowPassRestore;
        private const int NoLevel = -1;
        private float m_timeSinceAudio;
        private int m_currentLevel;
        private float m_lastWeightedProximity;
        private AudioManager m_audioManager;
        private AudioLowPassFilter m_musicLowPassFilter;
        private bool m_noLowPassFilterAvailable;
        private bool m_restoringMusicAfterOrbCollection;
        private float m_timeSinceOrbCollection;

        // 060036e9 (1.10.1 RVA 0x693080)
        protected override void Awake()
        {
            base.Awake();
            InternalValidate();
            ProcessManager.GetSystemRef<AudioManager>(null, true).InvokeOnValid(OnAudioManagerValid);
        }

        // 060036ea (1.10.1 RVA 0x693290)
        private void OnAudioManagerValid(AudioManager audioManager)
        {
            m_audioManager = audioManager;
        }

        // 060036eb (1.10.1 RVA 0x6932B0): the cached filter is dropped so the next
        // SetMusicEffects looks it up again.
        protected override void OnEnable()
        {
            base.OnEnable();
            m_lastWeightedProximity = 0f;
            m_timeSinceAudio = 0f;
            m_noLowPassFilterAvailable = false;
            m_musicLowPassFilter = null;
            m_restoringMusicAfterOrbCollection = false;
            m_timeSinceOrbCollection = 0f;
            SetMusicEffects(0f);
            SetLevel(NoLevel);
        }

        // 060036ec (1.10.1 RVA 0x6935D0)
        protected override void OnDisable()
        {
            base.OnDisable();
            SetMusicEffects(0f);
        }

        // 060036ed (1.10.1 RVA 0x693330): the filter is enabled only while the restore
        // curve is not running, and while restoring the lower of the two cutoffs wins.
        private void SetMusicEffects(float proximity)
        {
            if (m_gameplayMusicAudioSource == HLAudioSourceIdentifier.None || m_noLowPassFilterAvailable)
                return;
            if (m_musicLowPassFilter == null)
            {
                if (m_audioManager == null)
                    return;
                if (!m_audioManager.TryGetAudioSourceData(m_gameplayMusicAudioSource, out HLAudioSourceData audioSourceData))
                    return;
                m_musicLowPassFilter = audioSourceData.LowPassFilter;
                if (m_musicLowPassFilter == null)
                    return;
                if (!audioSourceData.HasLowPassFilter)
                {
                    m_noLowPassFilterAvailable = true;
                    return;
                }
            }
            float cutoffFrequency = m_orbProximityMusicLowPassCutoff.Evaluate(Mathf.Clamp01(proximity));
            if (m_restoringMusicAfterOrbCollection)
                cutoffFrequency = Mathf.Min(m_orbCollectionLowPassRestore.Evaluate(m_timeSinceOrbCollection), cutoffFrequency);
            m_musicLowPassFilter.enabled = !m_restoringMusicAfterOrbCollection;
            m_musicLowPassFilter.cutoffFrequency = cutoffFrequency;
        }

        // 060036ee (1.10.1 RVA 0x693600)
        protected override void InternalUpdate(float deltaTime)
        {
            float weightedProximity;
            if (IsCollectingOrb())
            {
                m_timeSinceOrbCollection = 0f;
                m_restoringMusicAfterOrbCollection = true;
                SetMusicEffects(1f);
                weightedProximity = 0f;
            }
            else
            {
                if (m_restoringMusicAfterOrbCollection)
                {
                    m_timeSinceOrbCollection += deltaTime;
                    if (m_timeSinceOrbCollection >= m_orbCollectionLowPassRestore[m_orbCollectionLowPassRestore.length - 1].time)
                        m_restoringMusicAfterOrbCollection = false;
                }
                SetMusicEffects(m_gameplayIsland.DistanceProximity.Value);
                weightedProximity = m_gameplayIsland.WeightedProximity.Value;
            }

            int level = m_currentLevel;
            if (!Mathf.Approximately(m_lastWeightedProximity, weightedProximity))
            {
                m_lastWeightedProximity = weightedProximity;
                level = NoLevel;
                // ucomiss/jbe 0x69382d keeps climbing unless the next minimum is above the proximity, so NaN climbs.
                while (level < m_levels.Count - 1 && !(m_levels[level + 1].MinProximity > weightedProximity))
                    level++;
                if (m_currentLevel != level)
                {
                    SetLevel(level);
                    return;
                }
            }

            if (level == NoLevel)
                return;
            m_timeSinceAudio += deltaTime;
            OrbDetectorLevel orbDetectorLevel = m_levels[level];
            if (m_timeSinceAudio >= orbDetectorLevel.AudioRepeatFrequency)
            {
                PlayAudio(orbDetectorLevel);
                while (m_timeSinceAudio >= orbDetectorLevel.AudioRepeatFrequency)
                    m_timeSinceAudio -= orbDetectorLevel.AudioRepeatFrequency;
            }
        }

        // 060036ef (1.10.1 RVA 0x6934F0): every level up to and including the new one is
        // shown; the last of those pings once on entry.
        private void SetLevel(int level)
        {
            OrbDetectorLevel reachedLevel = null;
            for (int i = 0; i < m_levels.Count; i++)
            {
                OrbDetectorLevel orbDetectorLevel = m_levels[i];
                bool active = i <= level;
                if (active)
                    reachedLevel = orbDetectorLevel;
                SetGameObjectsActive(orbDetectorLevel.EnabledWhileActive, active);
            }
            if (m_currentLevel != level)
            {
                m_currentLevel = level;
                if (reachedLevel != null)
                {
                    PlayAudio(reachedLevel);
                    m_timeSinceAudio = 0f;
                }
            }
        }

        // 060036f0 (1.10.1 RVA 0x6939B0)
        private void SetGameObjectsActive(IEnumerable<GameObject> gameObjects, bool active)
        {
            foreach (GameObject gameObject in gameObjects)
                gameObject.SetActive(active);
        }

        // 060036f1 (1.10.1 RVA 0x6938F0)
        private void PlayAudio(OrbDetectorLevel level)
        {
            if (m_audioManager == null || m_audioSource == HLAudioSourceIdentifier.None
                || level.AudioClip == HLAudioClipIdentifier.None || IsCollectingOrb())
                return;
            m_audioManager.PlayOneShotAtSource(level.AudioClip, m_audioSource, 0f, 1f);
        }

        // 060036f2 (1.10.1 RVA 0x6938D0)
        private bool IsCollectingOrb()
        {
            return m_gameplayIsland.CollectionInProgress.Value;
        }

        // 060036f3 (1.10.1 RVA 0x693170): the shipping body only walks m_levels; whatever
        // validation the loop once did is compiled out of the release build.
        private void InternalValidate()
        {
            foreach (OrbDetectorLevel level in m_levels)
            {
            }
        }

        // 060036f4 (1.10.1 RVA 0x693C60)
        protected override void OnValidate()
        {
            base.OnValidate();
            if (gameObject.CanValidate())
                InternalValidate();
        }

        // 060036f5 (1.10.1 RVA 0x693CA0)
        public OrbDetectorUI()
        {
        }

        [System.Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class OrbDetectorLevel
        {
            [Range(0f, 1f)]
            [SerializeField]
            private float m_minProximity;
            [SerializeField]
            private List<GameObject> m_enabledWhileActive;
            [SerializeField]
            private HLAudioClipIdentifier m_audioClip;
            [Range(0.1f, 10f)]
            [SerializeField]
            private float m_audioRepeatFrequency;

            // 060036f6..060036f9: direct field reads.
            public float MinProximity => m_minProximity;
            public List<GameObject> EnabledWhileActive => m_enabledWhileActive;
            public HLAudioClipIdentifier AudioClip => m_audioClip;
            public float AudioRepeatFrequency => m_audioRepeatFrequency;

            // 060036fa (1.10.1 RVA 0x693CF0)
            public OrbDetectorLevel()
            {
            }
        }
    }
}
