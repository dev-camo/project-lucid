// Original HLUnityUI.Runtime Hardlight.AnimatableEvent 020000d7, from the x86-64 bodies
// (0x060005cd..0x060005d0, 0x1B6B2D0..0x1B6B510) and metadata.
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [AddComponentMenu("Hardlight UI/Events/Animatable Event")]
    public sealed class AnimatableEvent : MonoBehaviour
    {
        [SerializeField]
        private NamedUnityEvent[] m_namedEvents;
        private Dictionary<string, UnityEvent> m_namedEventDictionary = new Dictionary<string, UnityEvent>();

        private void Awake()
        {
            PopulateDictionary();
        }

        // The first event with a given name wins; each later duplicate only logs.
        private void PopulateDictionary()
        {
            m_namedEventDictionary.Clear();
            for (int i = 0; i < m_namedEvents.Length; i++)
            {
                NamedUnityEvent namedEvent = m_namedEvents[i];
                if (m_namedEventDictionary.ContainsKey(namedEvent.m_eventName))
                    HLOutput.LogError(name + ":: AnimatableEvent Contains more than 1 event named: " + namedEvent.m_eventName);
                else
                    m_namedEventDictionary.Add(namedEvent.m_eventName, namedEvent.m_event);
            }
        }

        // Null, empty and unknown names are ignored.
        public void TriggerEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
                return;

            if (m_namedEventDictionary.TryGetValue(eventName, out UnityEvent namedEvent))
                namedEvent.Invoke();
        }
    }
}
