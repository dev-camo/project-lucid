using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020001cb; complete native-derived candidate.
    // AudioManager and the original following/character graph remain unclosed.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class InGameAudioListener : MonoBehaviour
    {
        private const int InGameAudioListenerStackableID = 0;
        private const float DefaultTransitionTime = 0.3f;
        [SerializeField] private FollowPlayerCharacterPosition m_followPlayerCharacter;
        [SerializeField] private LowPassFilterSettings[] m_lowPassFilterSettings;
        [Range(10f, 22000f)]
        [SerializeField] private float m_defaultFrequencyCutoff = 5000f;
        private Coroutine m_runningCoroutine;
        private readonly Dictionary<LowFilterType, LowPassFilterSettings> m_settings =
            new Dictionary<LowFilterType, LowPassFilterSettings>();
        private readonly StackableData m_activeSettings = new StackableData();
        private readonly List<LowPassFilterSettings> m_listOfSettings = new List<LowPassFilterSettings>();
        private readonly SystemRef<AudioManager> m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>();
        private readonly Dictionary<HLAudioSourceIdentifier, float> m_lerpFromCutoffValues =
            new Dictionary<HLAudioSourceIdentifier, float>(HardlightEnumComparers.HLAudioSourceIdentifierComparer);

        public enum LowFilterType
        {
            None = -1,
            PauseMenu = 0,
            Transporter = 1,
            ScoreAttack = 2
        }

        [Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        private class LowPassFilterSettings
        {
            public LowFilterType Type;
            public float CutoffFrequency;
            public float TransitionTime = DefaultTransitionTime;
            [NonSerialized] public StackableDataHandle DataHandle;

            // Original 0x060009b3; TransitionTime initializer precedes Object base.
            public LowPassFilterSettings() { }
        }

        private void Start()
        {
            m_audioManagerRef.Get().RegisterAudioListener(this);
            foreach (LowPassFilterSettings settings in m_lowPassFilterSettings)
            {
                // The supplied release retains this ContainsKey call, discards
                // its result, and then overwrites any duplicate configured key.
                m_settings.ContainsKey(settings.Type);
                m_settings[settings.Type] = settings;
            }
        }

        public void SetShouldFollow(bool shouldFollow) => m_followPlayerCharacter.SetShouldFollow(shouldFollow);

        public void ActivateLowPass(LowFilterType type)
        {
            foreach (LowPassFilterSettings settings in m_listOfSettings)
                if (settings.Type == type) return;
            this.SafeStopCoroutine(ref m_runningCoroutine);
            LowPassFilterSettings setting = m_settings[type];
            m_runningCoroutine = StartCoroutine(SetLowPassFilter(setting.CutoffFrequency, setting.TransitionTime));
            setting.DataHandle = m_activeSettings.AddOverride(InGameAudioListenerStackableID, setting);
            m_activeSettings.GetStack(InGameAudioListenerStackableID, m_listOfSettings);
        }

        public void RemoveLowPassFilter(LowFilterType type)
        {
            bool found = false;
            foreach (LowPassFilterSettings settings in m_listOfSettings)
                if (settings.Type == type) { found = true; break; }
            if (!found) return;
            this.SafeStopCoroutine(ref m_runningCoroutine);
            m_activeSettings.RemoveOverrides(m_settings[type].DataHandle);
            if (m_activeSettings.HasData(InGameAudioListenerStackableID))
            {
                LowPassFilterSettings settings = m_activeSettings.Get<LowPassFilterSettings>(InGameAudioListenerStackableID);
                m_runningCoroutine = StartCoroutine(SetLowPassFilter(settings.CutoffFrequency, settings.TransitionTime));
            }
            else
                m_runningCoroutine = StartCoroutine(SetLowPassFilter(m_defaultFrequencyCutoff, DefaultTransitionTime));
            // The removed setting retains its DataHandle until ClearLowPassFilters.
            m_activeSettings.GetStack(InGameAudioListenerStackableID, m_listOfSettings);
        }

        private void OnDestroy()
        {
            ClearLowPassFilters();
            if (m_audioManagerRef.IsValid()) m_audioManagerRef.Get().RemoveAudioListener(this);
        }

        // Original 0x060009b0; natural <SetLowPassFilter>d__18. The initial map
        // is retained across transitions. Newly added sources use the configured
        // default cutoff when absent from that map. Completion only toggles the
        // filter enabled state; it does not force a final cutoff or clear handle.
        private IEnumerator SetLowPassFilter(float targetFrequency, float time)
        {
            float timer = 0f;
            AudioManager audioManager = m_audioManagerRef.Get();
            foreach (HLAudioSourceData audioSource in audioManager.GetAllAudioSources())
            {
                if (!audioSource.HasLowPassFilter) continue;
                m_lerpFromCutoffValues[audioSource.Identifier] = audioSource.LowPassFilter.cutoffFrequency;
                audioSource.LowPassFilter.enabled = true;
            }
            while (timer < time)
            {
                timer += Time.unscaledDeltaTime;
                // Preserve collection/enumerator acquisition before evaluating
                // the native upper saturation and per-source interpolation.
                using (IEnumerator<HLAudioSourceData> sources = audioManager.GetAllAudioSources().GetEnumerator())
                {
                    float blend = Mathf.Min(timer / time, 1f);
                    while (sources.MoveNext())
                    {
                        HLAudioSourceData audioSource = sources.Current;
                        if (!audioSource.HasLowPassFilter) continue;
                        if (!m_lerpFromCutoffValues.TryGetValue(audioSource.Identifier, out float initialCutoff))
                            initialCutoff = m_defaultFrequencyCutoff;
                        audioSource.LowPassFilter.cutoffFrequency = Mathf.Lerp(initialCutoff, targetFrequency, blend);
                    }
                }
                yield return null;
            }
            foreach (HLAudioSourceData audioSource in audioManager.GetAllAudioSources())
                if (audioSource.HasLowPassFilter)
                    audioSource.LowPassFilter.enabled = targetFrequency < m_defaultFrequencyCutoff;
        }

        private void ClearLowPassFilters()
        {
            this.SafeStopCoroutine(ref m_runningCoroutine);
            foreach (LowPassFilterSettings settings in m_lowPassFilterSettings)
            {
                if (settings.DataHandle == null) continue;
                m_activeSettings.RemoveOverrides(settings.DataHandle);
                settings.DataHandle = null;
            }
            m_listOfSettings.Clear();
            m_lerpFromCutoffValues.Clear();
            if (!m_audioManagerRef.IsValid()) return;
            foreach (HLAudioSourceData audioSource in m_audioManagerRef.Get().GetAllAudioSources())
            {
                if (!audioSource.HasLowPassFilter) continue;
                if (audioSource.LowPassFilter == null) continue;
                audioSource.LowPassFilter.cutoffFrequency = m_defaultFrequencyCutoff;
                audioSource.LowPassFilter.enabled = false;
            }
        }

        // Original 0x060009b2; all six instance initializers precede MonoBehaviour base.
        public InGameAudioListener() { }
    }
}
