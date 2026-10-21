using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000abb; 0x6E22E0..0x6E2D40.
    [RequireComponent(typeof(Collider))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class BossHomingTarget : HomingTarget
    {
        [SerializeField] private bool m_useAnimatedTargetIndicator = true;
        [SerializeField, ShowIf("m_useAnimatedTargetIndicator", (string)null)]
        private AnimatedTargetIndicator m_animatedTargetIndicator;
        [SerializeField] private CharacterAbilityTypeGroup m_characterTriggerTargetAbilities;
        [SerializeField] private bool m_alwaysTargetableWhenActive;
        private AnimatedTargetIndicator m_instantiatedTargetIndicator;
        private bool m_instantiated;
        private CharacterManager m_characterManager;

        public AnimatedTargetIndicator AnimatedTargetIndicator => m_instantiatedTargetIndicator; // 06003ddb

        // 06003ddc: the character manager is only resolved when an indicator is used.
        protected override void Awake()
        {
            base.Awake();
            if (!m_useAnimatedTargetIndicator) return;
            m_instantiatedTargetIndicator = Instantiate(m_animatedTargetIndicator);
            m_instantiatedTargetIndicator.Setup(new AnimatedTargetIndicatorParameters
            {
                WorldspaceTarget = transform.position,
                OriginTarget = gameObject
            });
            m_instantiated = true;
            m_characterManager = ProcessManager.GetSystem<CharacterManager>(null, true);
        }

        // 06003ddd: with a trigger ability group, one of its abilities must be in use first.
        // The ability loop is the base DoOnCharacterHit, inlined natively.
        protected override void OnTriggerEnter(Collider otherCollider)
        {
            if (!m_characterManager.TryGetCurrentCharacter(out Character character)) return;
            if (m_characterTriggerTargetAbilities != null)
            {
                bool abilityInUse = false;
                foreach (CharacterAbilityTypeGroup.AbilityTypeDefinition ability in m_characterTriggerTargetAbilities.Abilities)
                {
                    if (ActorAbilityUtilities.IsAbilityInUse(character, ability.Type, ability.GracePeriod))
                    {
                        abilityInUse = true;
                        break;
                    }
                }
                if (!abilityInUse) return;
            }
            bool collision = m_characterManager.IsCurrentCharacterColliderCollision(otherCollider);
            bool trigger = m_characterManager.IsCurrentCharacterColliderTrigger(otherCollider);
            if (!collision && !trigger) return;
            ActiveHomingTarget target = TryGetHomingAttackTarget(character);
            if (target == null) return;
            foreach (IHomingAbility ability in target.Abilities)
                if (ability is IHomingAttack homingAttackAbility)
                    DoOnCharacterHit(homingAttackAbility);
        }

        // 06003dde..de0 all run the same update.
        public override void OnUpdate(float deltaTime) => RunUpdate();
        public override void OnLateUpdate(float deltaTime) => RunUpdate();
        public override void OnFixedUpdate(float deltaTime) => RunUpdate();

        // 06003de1: a raycast hit by something other than this target hides the indicator and
        // makes the target untargetable, unless it is always targetable while active.
        private void RunUpdate()
        {
            if (!m_instantiated || IsPaused) return;
            m_instantiatedTargetIndicator.SetTargetPosition(transform.position);
            if (m_alwaysTargetableWhenActive)
            {
                IsTargetable = true;
                return;
            }
            if (m_instantiatedTargetIndicator.CheckIfVisible(out RaycastHit hit, transform.position))
            {
                if (hit.colliderInstanceID == 0) return;
                IsTargetable = hit.collider.gameObject == gameObject;
                m_instantiatedTargetIndicator.SetHiddenBehindObject(!IsTargetable);
            }
            else
            {
                IsTargetable = true;
                m_instantiatedTargetIndicator.SetHiddenBehindObject(false);
            }
        }

        // 06003de2.
        protected override void OnDisable()
        {
            base.OnDisable();
            if (m_instantiated && m_instantiatedTargetIndicator != null)
                m_instantiatedTargetIndicator.gameObject.SetActive(false);
        }

        // 06003de3.
        protected override void OnEnable()
        {
            base.OnEnable();
            if (m_instantiated) m_instantiatedTargetIndicator.gameObject.SetActive(true);
        }
    }
}
