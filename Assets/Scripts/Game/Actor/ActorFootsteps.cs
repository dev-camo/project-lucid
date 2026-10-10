using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [RequireComponent(typeof(Animator))]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ActorFootsteps : TimeScaledComponent_SDT
    {
        [SerializeField] private float m_minTimeBetweenFootsteps = 0.1f;
        protected ActorAudioTypes m_footstepAudioType = ActorAudioTypes.Footsteps_Dirt;
        protected ActorAudioTypes m_footstepAudioTypeDefault = ActorAudioTypes.Footsteps_Dirt;
        protected ActorAudioTypes m_footstepAudioTypeClimbing = ActorAudioTypes.Footsteps_ClimbingWall;
        protected ActorAudioTypes m_footstepAudioTypeClimbingDefault = ActorAudioTypes.Footsteps_ClimbingWall;
        protected Dictionary<string, ActorParticleTriggerType> m_footstepParticleLookup = s_particleLookup;
        protected Dictionary<string, ActorParticleTriggerType> m_footstepParticleLookupDefault = s_particleLookup;
        private LevelManager m_levelManager;
        private Action<AnimationEvent> m_bufferedOneShotFunc;
        private AnimationEvent m_bufferedAnimationEvent;
        private Actor m_actor;
        private bool m_initialised;
        private float m_timeSinceLastFootstep;
        protected static readonly Dictionary<TerrainEffectType, ActorAudioTypes> s_terrainAudioLookup =
            new Dictionary<TerrainEffectType, ActorAudioTypes>(HardlightEnumComparers.TerrainEffectTypeComparer)
            {
                { TerrainEffectType.Dirt, ActorAudioTypes.Footsteps_Dirt },
                { TerrainEffectType.Grass, ActorAudioTypes.Footsteps_Dirt },
                { TerrainEffectType.Sand, ActorAudioTypes.Footsteps_Dirt },
                { TerrainEffectType.ClimbingWall, ActorAudioTypes.Footsteps_ClimbingWall },
                { TerrainEffectType.Metal, ActorAudioTypes.Footsteps_Dirt },
                { TerrainEffectType.BouncyCastle, ActorAudioTypes.Footsteps_Dirt },
                { TerrainEffectType.Glass, ActorAudioTypes.Footsteps_Dirt }
            };
        private static readonly Dictionary<string, ActorParticleTriggerType> s_particleLookup =
            new Dictionary<string, ActorParticleTriggerType>
            {
                { ActorAttachPointType.LeftHand.GetString(), ActorParticleTriggerType.ClimbingImpactLeftHand },
                { ActorAttachPointType.RightHand.GetString(), ActorParticleTriggerType.ClimbingImpactRightHand },
                { ActorAttachPointType.BossGuardian_Tentacle_BackL.GetString(), ActorParticleTriggerType.BossZ3GroundImpact_BackL },
                { ActorAttachPointType.BossGuardian_Tentacle_BackR.GetString(), ActorParticleTriggerType.BossZ3GroundImpact_BackR },
                { ActorAttachPointType.BossGuardian_Tentacle_FrontL.GetString(), ActorParticleTriggerType.BossZ3GroundImpact_FrontL },
                { ActorAttachPointType.BossGuardian_Tentacle_FrontR.GetString(), ActorParticleTriggerType.BossZ3GroundImpact_FrontR }
            };
        protected static readonly Dictionary<TerrainEffectType, Dictionary<string, ActorParticleTriggerType>> s_terrainParticleLookup =
            new Dictionary<TerrainEffectType, Dictionary<string, ActorParticleTriggerType>>(HardlightEnumComparers.TerrainEffectTypeComparer)
            {
                { TerrainEffectType.Dirt, null },
                { TerrainEffectType.Grass, null },
                { TerrainEffectType.Sand, null },
                { TerrainEffectType.ClimbingWall, s_particleLookup }
            };

        protected virtual void Start() // 060003cd
        {
            ProcessManager.GetSystemRef<LevelManager>(null, true).InvokeOnValid(OnLevelManagerRegistered);
        }

        public override void OnDestroy() // 060003ce
        {
            InitialiseActor(null);
            if (m_levelManager != null) m_levelManager.RemoveLevelActivatedAction(OnLevelActivated);
            base.OnDestroy();
        }

        public virtual void InitialiseActor(Actor actor) // 060003cf
        {
            m_actor = actor;
            m_initialised = true;
        }

        private void OnLevelManagerRegistered(LevelManager levelManager) // 060003d0
        {
            m_levelManager = levelManager;
            m_levelManager.InvokeOnLevelActivated(OnLevelActivated, true);
        }

        private void OnLevelActivated(LevelManagerLevel levelManagerLevel) // 060003d1
        {
            m_levelManager.RemoveLevelActivatedAction(OnLevelActivated);
            LevelSetupTypes setupType = levelManagerLevel.LevelDefinition.LevelSetupType;
            LevelSetupDefinition setup = ProcessManager.GetSystem<DataManager>(null, true).LevelSetupDefinitions[setupType];
            if (s_terrainAudioLookup.TryGetValue(setup.DefaultTerrainEffectTypeGround, out ActorAudioTypes audio))
            {
                m_footstepAudioType = audio;
                m_footstepAudioTypeDefault = audio;
            }
            if (s_terrainAudioLookup.TryGetValue(setup.DefaultTerrainEffectTypeClimbingWall, out audio))
            {
                m_footstepAudioTypeClimbing = audio;
                m_footstepAudioTypeClimbingDefault = audio;
            }
            if (s_terrainParticleLookup.TryGetValue(setup.DefaultTerrainEffectTypeClimbingWall,
                out Dictionary<string, ActorParticleTriggerType> particles))
            {
                m_footstepParticleLookup = particles;
                m_footstepParticleLookupDefault = particles;
            }
        }

        public void Action_PlayOneShot(AnimationEvent animationEvent) // 060003d2
        {
            if (m_initialised) BufferOneShot(BufferedPlayOneShot, animationEvent);
        }

        public void Action_PlayOneShotWithPfx(AnimationEvent animationEvent) // 060003d3
        {
            if (m_initialised) BufferOneShot(BufferedPlayOneShotWithPfx, animationEvent);
        }

        private void BufferOneShot(Action<AnimationEvent> oneShotFunc, AnimationEvent animationEvent) // 060003d4
        {
            // Equal and unordered clip weights retain the earlier event.
            if (m_bufferedAnimationEvent == null ||
                m_bufferedAnimationEvent.animatorClipInfo.weight < animationEvent.animatorClipInfo.weight)
            {
                m_bufferedOneShotFunc = oneShotFunc;
                m_bufferedAnimationEvent = animationEvent;
            }
        }

        private void BufferedPlayOneShot(AnimationEvent animationEvent) // 060003d5
        {
            // Both architectures enter on unordered values; retain the original negated guard.
            if (!(m_timeSinceLastFootstep <= m_minTimeBetweenFootsteps))
            {
                m_actor.Audio.PlayOneShot(m_footstepAudioType);
                m_timeSinceLastFootstep = 0f;
            }
        }

        private void BufferedPlayOneShotWithPfx(AnimationEvent animationEvent) // 060003d6
        {
            if (!(m_timeSinceLastFootstep <= m_minTimeBetweenFootsteps))
            {
                m_actor.Audio.PlayOneShot(m_footstepAudioTypeClimbing);
                m_timeSinceLastFootstep = 0f;
            }
            TrySpawnParticle(m_footstepParticleLookup, animationEvent.stringParameter);
        }

        private void TryPlayOneShot(ActorAudioTypes audioType) // 060003d7
        {
            if (!(m_timeSinceLastFootstep <= m_minTimeBetweenFootsteps))
            {
                m_actor.Audio.PlayOneShot(audioType);
                m_timeSinceLastFootstep = 0f;
            }
        }

        private void TrySpawnParticle(IReadOnlyDictionary<string, ActorParticleTriggerType> footstepParticleLookup,
            string identifier) // 060003d8
        {
            if (footstepParticleLookup != null && !string.IsNullOrWhiteSpace(identifier) &&
                footstepParticleLookup.TryGetValue(identifier, out ActorParticleTriggerType trigger))
                m_actor.PFXController.PlayActorPFX(trigger);
        }

        protected override void InternalUpdate(float deltaTime) // 060003d9
        {
            Action<AnimationEvent> callback = m_bufferedOneShotFunc;
            m_timeSinceLastFootstep += deltaTime;
            if (callback != null) callback(m_bufferedAnimationEvent);
            // These stores occur after the callback, including when it buffered another event.
            m_bufferedAnimationEvent = null;
            m_bufferedOneShotFunc = null;
        }

        public ActorFootsteps() { } // 060003da; all original instance initializers precede base.
        // 060003db is generated by the three complete original static field initializers.
    }
}
