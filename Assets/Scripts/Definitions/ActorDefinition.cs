using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ActorDefinition : ScriptableObjectWithGuid
    {
        [Tooltip("State machine representing actor's physical state.")]
        public FiniteStateMachineScriptableObject FSMMovement;
        public CharacterSettings Settings;
        [Tooltip("Lookups are applied in order - items in later lists will override existing items with the same effect type.")]
        public List<ActorAudioDefinition> AudioDefinitions;
        [Tooltip("Lookups are applied in order - items in later lists will override existing items with the same effect type.")]
        public List<ActorParticleEffectLookup> PFXLookups;
        [HashEnum(null)] [SerializeField] protected Strings m_name;
        public Strings Name => m_name; // 0x06001994; dictionary getter follows at 0x06001995.
        public Dictionary<ActorParticleTriggerType, ParticleEffectType> PFXDefinitionsDictionary { get; } =
            new Dictionary<ActorParticleTriggerType, ParticleEffectType>(HardlightEnumComparers.ActorParticleTriggerTypeComparer);
        public readonly ActorAudioLookup ActorAudioLookup = new ActorAudioLookup();
        protected virtual void Awake() { UpdateCachedValues(); } // 0x06001996.
        // 0x06001997: refresh before the base validation callback.
        protected override void OnValidate() { UpdateCachedValues(); base.OnValidate(); }
        // 0x06001998: ordered non-atomic refresh. Later definitions replace earlier
        // keys; a failure retains the caches and entries already written.
        protected virtual void UpdateCachedValues()
        {
            Settings.UpdateCachedValues();
            PFXDefinitionsDictionary.Clear();
            foreach (ActorParticleEffectLookup lookup in PFXLookups)
            {
                lookup.UpdateCachedValues();
                foreach (KeyValuePair<ActorParticleTriggerType, ParticleEffectType> entry in lookup.Lookup)
                {
                    entry.Deconstruct(out ActorParticleTriggerType trigger, out ParticleEffectType effect);
                    PFXDefinitionsDictionary[trigger] = effect;
                }
            }
            ActorAudioLookup.AudioDictionary.Clear();
            foreach (ActorAudioDefinition definition in AudioDefinitions)
                ActorAudioLookup.CompileLookup(definition.ActorAudioReferences);
        }
        // 0x06001999: dictionary then audio lookup initializers before base ctor.
    }
}
