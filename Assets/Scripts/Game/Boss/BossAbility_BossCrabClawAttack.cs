using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020001dc; bodies translated from the 1.10.1 x86-64 binary (0x6F8020..0x6F8FD0).
    // The crab boss's claw attack. It picks the claw to attack with, telegraphs it, and strikes after the wind-up (the
    // attack with the configured index winds up for longer), spawning the claw's hazard projectile at the strike.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BossAbility_BossCrabClawAttack : ActorAbility, IAbilityTrigger, IOffensiveAbility
    {
        public BossAbilityDefinition_BossCrabClawAttack Definition;

        private bool m_abilityActivated;
        private bool m_canTriggerAttackAnim;
        private bool m_attackAnimStarted;
        private bool m_startWindup;
        private bool m_isWindingUp;
        private bool m_isWoundUp;
        private Boss m_boss;
        private float m_attackTimeoutTimer;
        private float m_attackAnimationTimer;
        private float m_extraWindUpTimer;
        private float m_currentFarRangeSquared;
        private float m_currentTelegraphRangeSquared;
        private float m_extraRange;
        private int m_thisAttackIndex;
        private readonly GraphStorageKey m_bodySlamCountFSMVar = new GraphStorageKey("clawAttackCount", 0, 0);

        public override AbilityDefinition DefinitionBase => Definition; // 06000a48
        public bool IsTriggered => m_canTriggerAttackAnim; // 06000a49
        public bool StartWindUp => m_startWindup; // 06000a4a
        public bool WoundUp => m_isWoundUp; // 06000a4b

        // 06000a4c: both casts are `as`; a null boss or definition stores null.
        public override void Initialise(Actor boss, AbilityDefinition definition)
        {
            base.Initialise(boss, definition);
            m_boss = boss as Boss;
            Definition = definition as BossAbilityDefinition_BossCrabClawAttack;
        }

        // 06000a4d: the attack index is the stored count of claw attacks so far.
        protected override void DoOnEnter()
        {
            base.DoOnEnter();
            m_thisAttackIndex = m_boss.Storage.GetValue(m_bodySlamCountFSMVar, 0, true);
        }

        // 06000a4e
        protected override void DoOnLeave()
        {
            base.DoOnLeave();
            AbilityFinished = false;
            m_attackTimeoutTimer = 0f;
            m_attackAnimationTimer = 0f;
            m_extraWindUpTimer = 0f;
            m_extraRange = 0f;
            m_abilityActivated = false;
            m_canTriggerAttackAnim = false;
            m_attackAnimStarted = false;
            m_startWindup = false;
            m_isWindingUp = false;
            m_isWoundUp = false;
        }

        // 06000a4f: the strike only runs once the wind-up is wound up; its end finishes the ability.
        protected override void DoOnUpdate(float deltaTime)
        {
            base.DoOnUpdate(deltaTime);
            if (m_abilityActivated && !WindingUp(deltaTime))
            {
                if (!Attacking(deltaTime))
                    AbilityFinished = true;
            }
        }

        // 06000a50: the valid ranges grow while no target is in range.
        private void IncreaseValidAttackRange(float deltaTime)
        {
            m_extraRange = deltaTime * Definition.TargetGrowthPerSecond + m_extraRange;
            float far = m_extraRange + Definition.TargetValidRangeFar;
            float telegraph = m_extraRange + Definition.TelegraphClawChoiceRange;
            m_currentFarRangeSquared = far * far;
            m_currentTelegraphRangeSquared = telegraph * telegraph;
        }

        // 06000a51: waits out the post-wind-up timeout while there is no character or it is inside the near range, then
        // plays the strike animation and spawns the projectile once it has run its length. True while still attacking.
        private bool Attacking(float deltaTime)
        {
            Character character;
            bool characterFound = ProcessManager.GetSystem<CharacterManager>().TryGetCurrentCharacter(out character);
            bool waitForCharacter = !characterFound
                || Definition.TargetValidRangeNearSquared > (m_boss.WorldPosition - character.WorldPosition).sqrMagnitude;
            if (waitForCharacter && Definition.PostWindUpAttackTimeOut > m_attackTimeoutTimer)
            {
                m_attackTimeoutTimer = m_attackTimeoutTimer + deltaTime;
                return true;
            }
            if (!m_canTriggerAttackAnim)
                m_canTriggerAttackAnim = true;
            if (!m_attackAnimStarted)
                return true;
            if (Definition.AttackAnimationLength > m_attackAnimationTimer)
            {
                m_attackAnimationTimer = m_attackAnimationTimer + deltaTime;
                return true;
            }
            TriggerAttackStrikeEffects();
            FireProjectileFromBoss();
            m_attackAnimStarted = false;
            m_attackTimeoutTimer = 0f;
            m_attackAnimationTimer = 0f;
            return false;
        }

        // 06000a52
        public void TriggerAttackStrikeEffects()
        {
            m_boss.Storage.SetValue(BossFSMKeys.BossEffectType, BossEffectTriggerTypes.AttackStrike);
            m_boss.TriggerEffects();
        }

        // 06000a53: the hazard spawns at the attack's claw point; without a claw attack type nothing is attached (zero).
        private void FireProjectileFromBoss()
        {
            BossAttackTypes attack = m_boss.Storage.GetValue(BossFSMKeys.BossAttackType, BossAttackTypes.None, true);
            ActorAttachPointType attachPoint = attack == BossAttackTypes.BossCrab_RightClaw
                ? ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint
                : attack == BossAttackTypes.BossCrab_LeftClaw
                    ? ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint
                    : ActorAttachPointType.None;
            ProcessManager.GetSystem<BossManager>().SpawnObjectFromBoss<Hazard>(Definition.Projectile, m_boss, attachPoint, Definition.ProjectileLifetime);
        }

        public void SetAttackFlow(bool abilityActive) // 06000a54
        {
            m_abilityActivated = abilityActive;
            m_startWindup = true;
        }

        public void SetAttackWindingUp() => m_isWindingUp = true; // 06000a55
        public void SetAttackWoundUp() => m_isWoundUp = true; // 06000a56
        public void SetAttackAnimStarted() => m_attackAnimStarted = true; // 06000a57

        // 06000a58: skips the wind-up and applies the claw choice the storage override holds.
        public void SkipAttackPreparation(ActorAnimationDefinition actorAnimationDefinition)
        {
            m_isWindingUp = true;
            m_isWoundUp = true;
            ProcessAttackSettings(m_boss.Storage.GetValue(BossFSMKeys.BossAttackTypeOverride, BossAttackTypes.None, true), actorAnimationDefinition);
        }

        public void ResetProjectileGroupCounter() { } // 06000a59, original empty body

        private bool AbilityActivated() => m_abilityActivated; // 06000a5a

        // 06000a5b: true while the wind-up is still running. The attack with the long wind-up index waits out the extra
        // wind-up seconds after the wind-up. It also drops the start request once winding up or wound up.
        private bool WindingUp(float deltaTime)
        {
            if (m_isWindingUp)
                m_startWindup = false;
            if (!m_isWoundUp)
                return true;
            m_startWindup = false;
            if (m_thisAttackIndex != Definition.LongWindUpAttackComparison)
                return false;
            m_extraWindUpTimer = deltaTime + m_extraWindUpTimer;
            return Definition.ExtraWindUpSeconds > m_extraWindUpTimer;
        }

        // 06000a5c: a target is valid inside the near-to-far range and the valid attack angle, or inside the telegraph range.
        // Both range tests are negated comparisons, so a NaN distance passes them. A valid target alternates the claw
        // (or picks the side the character is on when neither claw is deflated); an invalid one clears the choice and
        // widens the valid range.
        public bool CanAttack(Boss boss, Character character, ActorAnimationDefinition actorAnimationDefinition, float deltaTime)
        {
            Vector3 toBoss = boss.WorldPosition - character.WorldPosition;
            float angle = Vector3.SignedAngle(character.ForwardDirection, toBoss, boss.UpDirection);
            float squaredDistance = toBoss.sqrMagnitude;
            bool targetValid = (!(Definition.TargetValidRangeNearSquared > squaredDistance) && !(squaredDistance > m_currentFarRangeSquared)
                    && Definition.ValidAttackAngle > Mathf.Abs(angle))
                || m_currentTelegraphRangeSquared > squaredDistance;
            if (!targetValid)
            {
                ProcessAttackSettings(BossAttackTypes.None, actorAnimationDefinition);
                IncreaseValidAttackRange(deltaTime);
                return false;
            }

            BossAttackTypes current = m_boss.Storage.GetValue(BossFSMKeys.BossAttackType, BossAttackTypes.None, true);
            if (boss.IsComponentDestroyed(ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint)
                || boss.IsComponentDestroyed(ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint))
            {
                ProcessAttackSettings(current == BossAttackTypes.BossCrab_LeftClaw
                    ? BossAttackTypes.BossCrab_RightClaw
                    : BossAttackTypes.BossCrab_LeftClaw, actorAnimationDefinition);
                return true;
            }
            ProcessAttackSettings(0f > angle ? BossAttackTypes.BossCrab_LeftClaw : BossAttackTypes.BossCrab_RightClaw, actorAnimationDefinition);
            return true;
        }

        // 06000a5d: stores the claw choice and sets the telegraph, left and right animation flags and claw hazards to match.
        private void ProcessAttackSettings(BossAttackTypes bossAttack, ActorAnimationDefinition actorAnimationDefinition)
        {
            m_boss.Storage.SetValue(BossFSMKeys.BossAttackType, bossAttack);
            bool left = bossAttack == BossAttackTypes.BossCrab_LeftClaw;
            bool right = bossAttack == BossAttackTypes.BossCrab_RightClaw;
            m_boss.Animator.TrySetFromAnimationType(actorAnimationDefinition, ActorAnimationType.Boss_Telegraph_Choice, left || right);
            m_boss.Animator.TrySetFromAnimationType(actorAnimationDefinition, ActorAnimationType.Boss_Use_Left, left);
            m_boss.Animator.TrySetFromAnimationType(actorAnimationDefinition, ActorAnimationType.Boss_Use_Right, right);
            m_boss.SetHazard(ActorAttachPointType.Boss_Crab_LeftClaw_VulnerablePoint, left);
            m_boss.SetHazard(ActorAttachPointType.Boss_Crab_RightClaw_VulnerablePoint, right);
        }

        // 06000a5e: unused by the retail code. True when the character is at or beyond the near range (also for NaN).
        private bool CharacterInAttackRange()
        {
            Character character;
            bool inRange = false;
            if (ProcessManager.GetSystem<CharacterManager>().TryGetCurrentCharacter(out character))
                inRange = !(Definition.TargetValidRangeNearSquared > (m_boss.WorldPosition - character.WorldPosition).sqrMagnitude);
            return inRange;
        }
    }
}
