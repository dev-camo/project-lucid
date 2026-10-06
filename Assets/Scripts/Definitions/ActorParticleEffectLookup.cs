using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
namespace HardlightProject
{
    [CreateAssetMenu(fileName = "ActorParticleEffectLookup", menuName = "HardlightProject/DefinitionData/Groups/ActorParticleEffectLookup")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ActorParticleEffectLookup : ScriptableObject
    {
        [SerializeField] private List<ActorParticleEffectLookupElement> m_elements = new List<ActorParticleEffectLookupElement>();
        public Dictionary<ActorParticleTriggerType, ParticleEffectType> Lookup { get; } =
            new Dictionary<ActorParticleTriggerType, ParticleEffectType>(HardlightEnumComparers.ActorParticleTriggerTypeComparer);
        // 0x0600199a getter, 0x0600199b: refresh in order; last authored entry wins.
        public void UpdateCachedValues()
        {
            Lookup.Clear();
            foreach (ActorParticleEffectLookupElement element in m_elements)
            {
                Lookup[element.Trigger] = element.Effect;
            }
        }
        // 0x0600199c: list followed by comparer-backed dictionary initializer.
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class ActorParticleEffectLookupElement : ISerializationCallbackReceiver
        {
            [SerializeField] [HashEnum(typeof(ActorParticleTriggerType))] private ActorParticleTriggerType m_trigger;
            [HashEnum(typeof(ParticleEffectType))] [SerializeField] private ParticleEffectType m_effect;
            [SerializeField] [HideInInspector] public string Name;
            public ActorParticleTriggerType Trigger => m_trigger; // 0x0600199d
            public ParticleEffectType Effect => m_effect; // 0x0600199e
            // 0x0600199f: zero uses the original NONE literal, not the enum registry.
            public void OnBeforeSerialize()
            {
                string trigger = (int)m_trigger == 0 ? "NONE" : m_trigger.GetString();
                string effect = (int)m_effect == 0 ? "NONE" : m_effect.GetString();
                Name = string.Concat(trigger, " -> ", effect);
            }
            public void OnAfterDeserialize() { } // 0x060019a0: genuine RET.
            // 0x060019a1: base-only.
        }
    }
}
