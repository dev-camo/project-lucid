using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Pool;
using PFXParameterBase = HardlightProject.ActorAnimationDefinition.PFXParameterBase;
using PFXParameterCondition = HardlightProject.ActorAnimationDefinition.PFXParameterCondition;
using ParticleBindingParameters = HardlightProject.ParticleEffectWrapper.ParticleBindingParameters;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ActorPFXController : TimeScaledComponent_SDT
    {
        [SerializeField] private TerrainEffectsMetadataKeyLookupDefinition m_terrainEffectsMetadataKeyLookup;
        private ActorAttachPoints m_actorAttachPoints;
        private readonly Dictionary<ActorParticleTriggerType, ActorParticleEffectDefinition> m_pfxDefinitions =
            new Dictionary<ActorParticleTriggerType, ActorParticleEffectDefinition>(HardlightEnumComparers.ActorParticleTriggerTypeComparer);
        private Dictionary<ActorParticleTriggerType, ParticleEffectType> m_pfxLookup;
        private readonly List<ParticleEffectWrapper> m_activePFX = new List<ParticleEffectWrapper>();
        private readonly HashSet<CharacterCollisionData> m_currentCollisionData = new HashSet<CharacterCollisionData>();
        private readonly Dictionary<ActorTerrainEffectsDefinition, int> m_currentTerrainEffects = new Dictionary<ActorTerrainEffectsDefinition, int>();
        private DataManager m_dataManager;
        private ParticleEffectManager m_particleEffectManager;
        private bool m_initialised;
        private readonly List<ContactPoint> m_contactPoints = new List<ContactPoint>();
        private Actor m_actor;
        private readonly List<DelayedParticleRemoval> m_delayedParticleRemovals = new List<DelayedParticleRemoval>();
        private readonly ObjectPool<DelayedParticleRemoval> m_delayedParticleRemovalsPool =
            new ObjectPool<DelayedParticleRemoval>(() => new DelayedParticleRemoval(), null, null, null, true, 10, 10000);
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class DelayedParticleRemoval
        {
            public PFXParameterBase PFXParameter { get; private set; } // 06000405/406
            public float TimeRemainingSeconds { get; set; } // 06000407/408
            public void Initialise(PFXParameterBase pfxParameter) // 06000409
            {
                PFXParameter = pfxParameter;
                TimeRemainingSeconds = pfxParameter.RemoveDelaySeconds;
            }
            public DelayedParticleRemoval() { } // 0600040a
        }

        public void Initialise(Actor actor, ActorAttachPoints actorAttachPoints,
            Dictionary<ActorParticleTriggerType, ParticleEffectType> pfxLookup) // 060003eb
        {
            m_actor = actor;
            m_actorAttachPoints = actorAttachPoints;
            m_pfxLookup = pfxLookup;
            m_dataManager = ProcessManager.GetSystem<DataManager>(null, true);
            m_particleEffectManager = ProcessManager.GetSystem<ParticleEffectManager>(null, true);
            // The original evaluates Unity equality here and ignores its result.
            _ = m_terrainEffectsMetadataKeyLookup == null;
            Dictionary<ParticleEffectType, ParticleEffectDefinition> definitions = m_dataManager.ParticleEffectDefinitions;
            foreach (var (trigger, effectType) in m_pfxLookup)
            {
                if (definitions.TryGetValue(effectType, out ParticleEffectDefinition definition))
                {
                    ActorParticleEffectDefinition actorDefinition = definition as ActorParticleEffectDefinition;
                    if (actorDefinition != null) m_pfxDefinitions[trigger] = actorDefinition;
                }
            }
            foreach (var (trigger, definition) in m_pfxDefinitions)
                m_particleEffectManager.RegisterInterest(definition, this);
            if (m_actor is Character) ShowEffectsOnIntroComplete();
            m_initialised = true;
        }

        private void ShowEffectsOnIntroComplete() // 060003ec; original member ordinal 17
        {
            m_levelManagerRef.InvokeOnValid(levelManager =>
            {
                if (levelManager.TryGetCurrentLevel(out LevelManagerLevel level))
                    level.Data.RegisterForIntroSequenceComplete(() =>
                    {
                        if (m_actor is Character character && ClearPreviousCollisionData(character, out CharacterCollisionData collisionData))
                        {
                            m_currentCollisionData.Clear();
                            m_currentTerrainEffects.Clear();
                            OnCollisionDataEnter(collisionData, null);
                        }
                    });
            });
        }

        private bool ClearPreviousCollisionData(Character character, out CharacterCollisionData collisionData) // 060003ed
        {
            collisionData = character.Collider.LinkedCollisionData;
            if (collisionData == null || !collisionData.HasEffects ||
                !collisionData.EffectMetadata.TryGetValue(m_terrainEffectsMetadataKeyLookup.TerrainEffectTypeMetadataKey,
                    out TerrainEffectType terrainType)) return false;
            m_currentCollisionData.Add(collisionData);
            EndTerrainEffects(m_dataManager.ActorTerrainEffectsDefinitions[terrainType]);
            return true;
        }

        public void Deinitialise() // 060003ee
        {
            if (m_particleEffectManager != null)
            {
                foreach (var (trigger, definition) in m_pfxDefinitions)
                    if (definition != null) m_particleEffectManager.UnregisterInterest(definition, this);
                StopAllPFX();
            }
            m_pfxDefinitions.Clear();
            m_currentCollisionData.Clear();
            m_currentTerrainEffects.Clear();
            m_actor = null;
            m_actorAttachPoints = null;
            m_pfxLookup = null;
            m_dataManager = null;
            m_particleEffectManager = null;
            m_initialised = false;
        }

        public void StopAllPFX() // 060003ef
        {
            for (int i = m_activePFX.Count - 1; i >= 0; --i)
            {
                ParticleEffectWrapper pfx = m_activePFX[i];
                m_particleEffectManager.StopPFX(pfx, false);
                OnActorPFXInstanceComplete(pfx);
            }
            foreach (DelayedParticleRemoval delayedRemoval in m_delayedParticleRemovals)
                m_delayedParticleRemovalsPool.Release(delayedRemoval);
            m_delayedParticleRemovals.Clear();
        }

        public void PlayActorPFX(IEnumerable<PFXParameterBase> pfxParameters) // 060003f0
        {
            if (!m_particleEffectManager.ActorPFXEnabled || m_actorAttachPoints == null) return;
            ParticleBindingParameters parameters = CreateDefaultParticleBindingParameters();
            foreach (PFXParameterBase pfxParameter in pfxParameters)
            {
                bool startEffect = EvaluatePFXConditions(pfxParameter.Conditions, parameters);
                bool continuous = pfxParameter.EvaluateContinuously;
                if (startEffect | continuous) PlayActorPFX(pfxParameter.PFXTrigger, parameters, startEffect);
            }
        }

        public void UpdateActorPFX(IEnumerable<PFXParameterBase> pfxParameters) // 060003f1
        {
            if (!m_particleEffectManager.ActorPFXEnabled || m_actorAttachPoints == null) return;
            foreach (PFXParameterBase parameter in pfxParameters)
            {
                if (!parameter.EvaluateContinuously || !m_pfxLookup.TryGetValue(parameter.PFXTrigger, out ParticleEffectType type)) continue;
                foreach (ParticleEffectWrapper pfx in m_activePFX)
                {
                    if (pfx.ParticleEffectDefinition.PFXType != type) continue;
                    bool shouldStart = EvaluatePFXConditions(parameter.Conditions, pfx.BindingParameters);
                    // A matching effect already in the requested state ends this inner search.
                    if (shouldStart == (pfx.Status == ParticleEffectWrapper.ParticleEffectWrapperStatus.Started)) break;
                    if (shouldStart) pfx.StartEffect();
                    else pfx.StopEffect(parameter.RemoveEmittedParticles ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting, false);
                }
            }
        }

        private static bool EvaluatePFXConditions(IReadOnlyCollection<PFXParameterCondition> conditions,
            ParticleBindingParameters parameters) // 060003f2
        {
            if (conditions == null || conditions.Count == 0) return true;
            foreach (PFXParameterCondition condition in conditions)
            {
                float value;
                switch (condition.ConditionType)
                {
                    case PFXParameterCondition.PFXParameterConditionType.ActorXZVelocity: value = parameters.ActorXZVelocity; break;
                    case PFXParameterCondition.PFXParameterConditionType.ActorImpactVelocity: value = parameters.ActorImpactVelocity; break;
                    case PFXParameterCondition.PFXParameterConditionType.BrainMovement: value = parameters.BrainMovement; break;
                    default: return false;
                }
                if (!condition.ComparisonType.Evaluate(value, condition.Criteria)) return false;
            }
            return true;
        }

        private ParticleBindingParameters CreateDefaultParticleBindingParameters() // 060003f3
        {
            float xzVelocity = m_actor.LocalVelocity.xz().magnitude;
            float brainMovement = m_actor.BrainMovementMagnitude;
            float spinDashCharge = GetSpinDashChargeValue(m_actor);
            return new ParticleBindingParameters { ActorImpactVelocity = 0f, ActorXZVelocity = xzVelocity,
                ActorVisibility = true, BrainMovement = brainMovement, SpinDashCharge = spinDashCharge };
        }

        private float GetSpinDashChargeValue(Actor actor) // 060003f4
        {
            // The original ignores its parameter and uses the current component actor.
            if (!(m_actor is Character character) || character.SpinDashRoll == null) return 0f;
            float chargeTime = character.SpinDashRoll.ChargeTime;
            if (!(chargeTime > 0f)) return 0f;
            return character.GetAbility<CharacterAbility_MovementGroundSpinDashCharge>(ActorAbilityType.Character_SpinDashCharge).GetLaunchProgress(chargeTime);
        }

        public void PlayActorPFX(ActorParticleTriggerType particleTriggerType) // 060003f5
        {
            ParticleBindingParameters parameters = CreateDefaultParticleBindingParameters();
            PlayActorPFX(particleTriggerType, parameters, true);
        }

        public void PlayActorPFX(ActorParticleTriggerType particleTriggerType,
            ParticleBindingParameters particleBindingParameters, bool startEffect) // 060003f6
        {
            if (m_pfxDefinitions.TryGetValue(particleTriggerType, out ActorParticleEffectDefinition definition))
                m_particleEffectManager.AcquirePFX(definition, OnActorPFXInstanceStart, OnActorPFXInstanceComplete,
                    particleBindingParameters, startEffect);
        }

        private void OnActorPFXInstanceStart(ParticleEffectWrapper pfx, ActorParticleEffectDefinition definition, bool startEffect) // 060003f7
        {
            ActorAttachPoints attachPoints;
            m_actorAttachPoints.Attach(m_actor, definition.Location, pfx.transform, definition.SyncOrientationWithActorVelocity);
            if (startEffect) pfx.StartEffect();
            m_activePFX.Add(pfx);
            if (definition.DetachImmediately)
            {
                attachPoints = m_actorAttachPoints;
                CoroutineUtils.OnNextFrame(() => attachPoints.Detach(definition.Location, pfx.transform, definition.KeepPosition, definition.KeepRotation));
            }
        }

        private void OnActorPFXInstanceComplete(ParticleEffectWrapper pfx) // 060003f8
        {
            if (!m_activePFX.Remove(pfx) || !m_initialised || m_actorAttachPoints == null) return;
            ActorParticleEffectDefinition definition = pfx.ParticleEffectDefinition as ActorParticleEffectDefinition;
            m_actorAttachPoints.Detach(definition.Location, pfx.transform, false, false);
        }

        public void StopPFX(IEnumerable<PFXParameterBase> pfxParameters) // 060003f9
        {
            foreach (PFXParameterBase parameter in pfxParameters)
            {
                if (!parameter.EndOnLeaveState) continue;
                if (!(parameter.RemoveDelaySeconds <= 0f))
                {
                    DelayedParticleRemoval removal = m_delayedParticleRemovalsPool.Get();
                    removal.Initialise(parameter);
                    m_delayedParticleRemovals.Add(removal);
                }
                else StopPFX(parameter);
            }
        }

        private void StopPFX(PFXParameterBase pfxParameter) // 060003fa
        {
            if (!m_pfxLookup.TryGetValue(pfxParameter.PFXTrigger, out ParticleEffectType type)) return;
            foreach (ParticleEffectWrapper pfx in m_activePFX)
            {
                if (pfx.ParticleEffectDefinition.PFXType != type) continue;
                if (!pfxParameter.EvaluateContinuously && pfx.Status != ParticleEffectWrapper.ParticleEffectWrapperStatus.Started) continue;
                pfx.StopEffect(pfxParameter.RemoveEmittedParticles ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting, false);
                break;
            }
        }

        public void OnCollisionDataEnter(CharacterCollisionData collisionData, Collision collision) // 060003fb
        {
            if (m_currentCollisionData.Contains(collisionData) || !collisionData.HasEffects ||
                !collisionData.EffectMetadata.TryGetValue(m_terrainEffectsMetadataKeyLookup.TerrainEffectTypeMetadataKey,
                    out TerrainEffectType terrainType)) return;
            m_currentCollisionData.Add(collisionData);
            ActorTerrainEffectsDefinition definition = m_dataManager.ActorTerrainEffectsDefinitions[terrainType];
            StartTerrainEffects(definition);
            if (collision != null) SpawnImpactEffects(definition, collision);
        }

        public void OnCollisionDataExit(CharacterCollisionData collisionData, Collision collision) // 060003fc
        {
            if (m_currentCollisionData.Remove(collisionData) &&
                collisionData.EffectMetadata.TryGetValue(m_terrainEffectsMetadataKeyLookup.TerrainEffectTypeMetadataKey,
                    out TerrainEffectType terrainType)) EndTerrainEffects(m_dataManager.ActorTerrainEffectsDefinitions[terrainType]);
        }

        private void StartTerrainEffects(ActorTerrainEffectsDefinition terrainEffects) // 060003fd
        {
            if (terrainEffects.ActorAnimationDefinition == null) return;
            int count = m_currentTerrainEffects.TryGetWithDefault(terrainEffects, 0);
            if (count == 0) PlayActorPFX(terrainEffects.ActorAnimationDefinition.PFXParameters);
            m_currentTerrainEffects[terrainEffects] = count + 1;
        }

        private void EndTerrainEffects(ActorTerrainEffectsDefinition terrainEffects) // 060003fe
        {
            if (terrainEffects.ActorAnimationDefinition == null) return;
            int count = m_currentTerrainEffects.TryGetWithDefault(terrainEffects, 0) - 1;
            if (count == 0) StopPFX(terrainEffects.ActorAnimationDefinition.PFXParameters);
            m_currentTerrainEffects[terrainEffects] = count;
        }

        private void SpawnImpactEffects(ActorTerrainEffectsDefinition terrainEffects, Collision collision) // 060003ff
        {
            if (!m_particleEffectManager.ActorPFXEnabled || collision.contactCount == 0 ||
                terrainEffects.ImpactAnimationDefinition == null || terrainEffects.ImpactAnimationDefinition.PFXParameters.Count == 0) return;
            Vector3 relativeVelocity = collision.relativeVelocity;
            if (relativeVelocity.sqrMagnitude < terrainEffects.MinimumImpactSpeedSqr) return;
            Vector3 normal = Vector3.zero;
            Vector3 point = Vector3.zero;
            int count = collision.GetContacts(m_contactPoints);
            for (int i = 0; i < count; ++i)
            {
                ContactPoint contact = m_contactPoints[i];
                normal += contact.normal;
                point += contact.point;
            }
            normal /= count;
            relativeVelocity *= Vector3.Dot(normal, relativeVelocity.normalized);
            if (relativeVelocity.sqrMagnitude < terrainEffects.MinimumImpactSpeedSqr) return;
            point /= count;
            float impactVelocity = relativeVelocity.magnitude;
            float xzVelocity = m_actor.LocalVelocity.xz().magnitude;
            float brainMovement = m_actor.BrainMovementMagnitude;
            float spinDashCharge = GetSpinDashChargeValue(m_actor);
            foreach (PFXParameterBase parameter in terrainEffects.ImpactAnimationDefinition.PFXParameters)
            {
                if (!m_pfxDefinitions.TryGetValue(parameter.PFXTrigger, out ActorParticleEffectDefinition definition)) continue;
                ParticleBindingParameters parameters = new ParticleBindingParameters { ActorImpactVelocity = impactVelocity,
                    ActorXZVelocity = xzVelocity, ActorVisibility = true, BrainMovement = brainMovement, SpinDashCharge = spinDashCharge };
                if (!EvaluatePFXConditions(parameter.Conditions, parameters)) continue;
                if (m_particleEffectManager.AcquirePFX(definition, OnImpactPFXInstanceComplete, parameters, out ParticleEffectWrapper pfx))
                {
                    pfx.transform.SetPositionAndRotation(point, Quaternion.LookRotation(-normal));
                    pfx.StartEffect();
                    m_activePFX.Add(pfx);
                }
            }
        }

        private void OnImpactPFXInstanceComplete(ParticleEffectWrapper pfx) // 06000400
        {
            m_activePFX.Remove(pfx);
        }

        protected override void InternalUpdate(float deltaTime) // 06000401
        {
            if (m_activePFX.Count == 0) return;
            for (int i = m_delayedParticleRemovals.Count - 1; i >= 0; --i)
            {
                DelayedParticleRemoval removal = m_delayedParticleRemovals[i];
                removal.TimeRemainingSeconds -= deltaTime;
                if (removal.TimeRemainingSeconds > 0f) continue;
                m_delayedParticleRemovals.RemoveAt(i);
                StopPFX(removal.PFXParameter);
                m_delayedParticleRemovalsPool.Release(removal);
            }
            float xzVelocity = m_actor.LocalVelocity.xz().magnitude;
            float brainMovement = m_actor.BrainMovementMagnitude;
            bool visibility = m_actor.FormRenderersOn;
            float spinDashCharge = GetSpinDashChargeValue(m_actor);
            foreach (ParticleEffectWrapper pfx in m_activePFX)
            {
                ParticleBindingParameters parameters = pfx.BindingParameters;
                parameters.ActorXZVelocity = xzVelocity;
                parameters.ActorVisibility = visibility;
                parameters.BrainMovement = brainMovement;
                parameters.SpinDashCharge = spinDashCharge;
                pfx.UpdateParameters(parameters);
            }
        }

        public ActorPFXController() { } // 06000402; original field initialization order precedes base.
        // Natural own callbacks 06000403/404 and cache/display contexts 0600040b..40f arise from original lambdas above.
    }
}
