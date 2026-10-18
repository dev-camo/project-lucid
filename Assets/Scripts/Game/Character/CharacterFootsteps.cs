using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 0200032e. Chooses the footstep audio and particle
    // set from the terrain effect of the collision data the character touches.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterFootsteps : ActorFootsteps
    {
        [SerializeField] protected TerrainEffectsMetadataKeyLookupDefinition m_terrainEffectsMetadataKeyLookup;
        private readonly HashSet<CharacterCollisionData> m_activeSurfaces = new HashSet<CharacterCollisionData>();
        private Character m_character;

        // 060013e9; moves the collision-data subscriptions to the new character.
        public override void InitialiseActor(Actor actor)
        {
            base.InitialiseActor(actor);
            Character character = actor as Character;
            if (m_character == character) return;
            if (m_character != null)
            {
                m_character.OnCollisionDataEnterEvent -= OnCharacterCollisionDataEnter;
                m_character.OnCollisionDataExitEvent -= OnCharacterCollisionDataExit;
            }
            m_character = character;
            if (m_character != null)
            {
                m_character.OnCollisionDataEnterEvent += OnCharacterCollisionDataEnter;
                m_character.OnCollisionDataExitEvent += OnCharacterCollisionDataExit;
            }
        }

        // 060013ea; only surfaces carrying a terrain effect are tracked.
        private void OnCharacterCollisionDataEnter(CharacterCollisionData collisionData, Collision collision)
        {
            if (collisionData.HasEffects &&
                collisionData.EffectMetadata.TryGetValue(m_terrainEffectsMetadataKeyLookup.TerrainEffectTypeMetadataKey,
                    out TerrainEffectType _) &&
                m_activeSurfaces.Add(collisionData))
                RecalculateFootstepAudio();
        }

        // 060013eb
        private void OnCharacterCollisionDataExit(CharacterCollisionData collisionData, Collision collision)
        {
            if (m_activeSurfaces.Remove(collisionData)) RecalculateFootstepAudio();
        }

        // 060013ec; reset to the defaults, then the first active surface with a
        // terrain effect decides (the original loop ends after it).
        private void RecalculateFootstepAudio()
        {
            m_footstepAudioType = m_footstepAudioTypeDefault;
            m_footstepAudioTypeClimbing = m_footstepAudioTypeClimbingDefault;
            m_footstepParticleLookup = m_footstepParticleLookupDefault;
            MetadataKeyType key = m_terrainEffectsMetadataKeyLookup.TerrainEffectTypeMetadataKey;
            foreach (CharacterCollisionData surface in m_activeSurfaces)
            {
                if (!surface.EffectMetadata.TryGetValue(key, out TerrainEffectType effectType)) continue;
                if (s_terrainAudioLookup.TryGetValue(effectType, out ActorAudioTypes audioType))
                {
                    m_footstepAudioType = audioType;
                    m_footstepAudioTypeClimbing = audioType;
                }
                if (s_terrainParticleLookup.TryGetValue(effectType,
                    out Dictionary<string, ActorParticleTriggerType> particleLookup))
                    m_footstepParticleLookup = particleLookup;
                break;
            }
        }

        // 060013ed
        public CharacterFootsteps() { }
    }
}
