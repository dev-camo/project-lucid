using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000844; bodies translated from the 1.10.1 x86-64 binary.
    // Publishes a UI event (and an optional click sound) whenever a game input is pressed.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIEventFromInput : MonoBehaviour
    {
        [SerializeField] private UIModernEvent m_eventToFire;
        [SerializeField] private GameInput m_input;

        [Tooltip("Set if this should only trigger if only this list of containers are open.")]
        [SerializeField] private List<UIContainerIdentifier> m_exclusiveContainers;

        [Tooltip("Add any platforms that this input is not relevant to.")]
        [SerializeField] private RuntimePlatform[] m_ignoreOnPlatforms = Array.Empty<RuntimePlatform>();

        [SerializeField] private HLAudioSourceIdentifier m_audioSourceIdentifier;
        [SerializeField] [HashEnum(null)] private HLAudioClipIdentifier m_audioClipIdentifier;
        private readonly SystemRef<AudioManager> m_audioManagerRef = ProcessManager.GetSystemRef<AudioManager>(null, true);

        // 0x06002fee: subscribes to the input going down.
        private void OnEnable()
        {
            ControlMapping.Subscribe(m_input, OnInputTriggered, InputTrigger.Down, -1);
        }

        // 0x06002fef
        private void OnDisable()
        {
            ControlMapping.Unsubscribe(m_input, OnInputTriggered, -1);
        }

        // 0x06002ff0: ignored on the listed platforms; with exclusive containers set, only while
        // exactly those are open. The input value itself is not inspected.
        private void OnInputTriggered(float inputValue)
        {
            if (m_ignoreOnPlatforms.Contains(Application.platform))
                return;
            if (!m_exclusiveContainers.IsNullOrEmpty()
                && !ProcessManager.GetSystem<UIManager>(null, true).AreExclusivelyOpen(m_exclusiveContainers))
                return;
            ProcessManager.GetSystem<App>(null, true).AddUniqueUIModernEvent(m_eventToFire);
            PlayOneShotClip();
        }

        // 0x06002ff1: needs both identifiers set and a live audio manager.
        private void PlayOneShotClip()
        {
            if (m_audioClipIdentifier == HLAudioClipIdentifier.None || m_audioSourceIdentifier == HLAudioSourceIdentifier.None
                || m_audioManagerRef.IsNull())
                return;
            m_audioManagerRef.Get().PlayOneShotAtSource(m_audioClipIdentifier, m_audioSourceIdentifier, 0f, 1f);
        }

        // 0x06002ff2: the field initialisers precede the MonoBehaviour constructor.
        public UIEventFromInput() { }
    }
}
