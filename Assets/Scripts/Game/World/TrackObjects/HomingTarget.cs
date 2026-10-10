using System;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime02000ac6, complete25 methods and18 fields.
    [RequireComponent(typeof(Collider))]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HomingTarget : TimeScaledComponent_SDT
    {
        [SerializeField] private UnityEvent m_onAwake;
        [SerializeField] private UnityEvent m_onActive;
        [SerializeField] private UnityEvent<Collider> m_onDisable;
        [SerializeField] private UnityEvent m_onTargeted;
        [SerializeField] private UnityEvent m_onCancelled;
        [SerializeField] private UnityEvent m_onHit;
        [SerializeField] private bool m_startDisabled;
        [SerializeField] private bool m_destroyOnComplete;
        [SerializeField] private TimeScaledComponent_SDT m_visualComponent;
        [SerializeField] private RigidbodyForceTrigger m_forceTrigger;
        [SerializeField] private HomingTargetType m_targetType;
        [SerializeField] private bool m_targetableFromGround = true;
        public bool IsTargetable { get; protected set; } = true;
        public Action<HomingTarget> ActionOnCharacterHit;
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
        private Collider m_collider;
        private EffectSequence m_effectSequence;
        private bool m_characterColliding;

        // Original06003e23..29; position is collider-bounds centre, rotation uses the component transform.
        public Vector3 WorldPosition => m_collider.bounds.center;
        public Quaternion WorldRotation => transform.rotation;
        public HomingTargetType TargetType => m_targetType;
        public Collider Collider => m_collider;
        public bool TargetableFromGround => m_targetableFromGround;

        // Original06003e2a; authored events are not allocated or null-guarded here.
        protected override void Awake()
        {
            base.Awake();
            CacheComponents();
            m_onAwake.Invoke();
            if (m_startDisabled) m_onDisable.Invoke(null);
            if (m_forceTrigger != null) m_forceTrigger.SetAsManualTrigger();
        }
        // Original06003e2b.
        protected override void OnValidate()
        {
            base.OnValidate();
            if (gameObject.CanValidate(false)) CacheComponents();
        }
        // Original06003e2c; base shutdown first, then clear collision flag.
        protected override void OnDisable() { base.OnDisable(); m_characterColliding = false; }
        // Original06003e2d is a shipped RET body.
        protected override void InternalUpdate(float deltaTime) { }
        // Original06003e2e uses managed reference null checks, including destroyed Unity wrappers.
        private void CacheComponents()
        {
            m_collider ??= GetComponent<Collider>();
            m_effectSequence ??= GetComponent<EffectSequence>();
        }
        // Original06003e2f; genuine visual virtual slot28 is ResetSynchronise, then current-target abilities.
        public void DoOnActive(bool enableForceTrigger, Character character)
        {
            m_onActive.Invoke();
            if (m_visualComponent != null) m_visualComponent.ResetSynchronise();
            if (!m_characterColliding) return;
            ActiveHomingTarget target = TryGetHomingAttackTarget(character);
            if (target == null) return;
            foreach (IHomingAbility ability in target.Abilities)
                if (ability is IHomingAttack homingAttackAbility)
                    OnCharacterHit(character, enableForceTrigger, homingAttackAbility);
        }
        // Original06003e30..33.
        public void DoOnDisable(Collider otherCollider) { m_onDisable.Invoke(otherCollider); }
        public void DoOnDisable() { m_onDisable.Invoke(null); }
        public void DoOnTargeted() { m_onTargeted.Invoke(); }
        public void DoOnCancelled() { m_onCancelled.Invoke(); }
        // Original06003e34.
        public void DoOnCharacterHit(IHomingAttack homingAttackAbility)
        {
            if (m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character))
            {
                m_characterColliding = true;
                OnCharacterHit(character, true, homingAttackAbility);
            }
        }
        // Original06003e35: both collider tests run, even when the first already succeeds.
        protected virtual void OnTriggerEnter(Collider otherCollider)
        {
            CharacterManager characterManager = m_characterManagerRef.Get();
            if (!characterManager.TryGetCurrentCharacter(out Character character)) return;
            bool collision = characterManager.IsCurrentCharacterColliderCollision(otherCollider);
            bool trigger = characterManager.IsCurrentCharacterColliderTrigger(otherCollider);
            if (!collision && !trigger) return;
            m_characterColliding = collision;
            ActiveHomingTarget target = TryGetHomingAttackTarget(character);
            if (target == null) return;
            foreach (IHomingAbility ability in target.Abilities)
                if (ability is IHomingAttack homingAttackAbility)
                    OnCharacterHit(character, collision, homingAttackAbility);
        }
        // Original06003e36; only the genuine collision collider clears this flag.
        private void OnTriggerExit(Collider otherCollider)
        {
            if (m_characterManagerRef.Get().IsCurrentCharacterColliderCollision(otherCollider))
                m_characterColliding = false;
        }
        // Original06003e37; active pool first, then actual Unity GameObject identity comparison.
        protected ActiveHomingTarget TryGetHomingAttackTarget(Character character)
        {
            if (!character.HomingPool.IsTargetActive()) return null;
            ActiveHomingTarget target = character.HomingPool.ActiveTarget;
            return gameObject != target.Object.Target ? null : target;
        }
        // Original06003e38: callback/order retained; current effect data is only the target transform.
        private void OnCharacterHit(Character character, bool enableForceTrigger, IHomingAttack homingAttackAbility)
        {
            m_onHit?.Invoke();
            if (enableForceTrigger && m_forceTrigger != null) m_forceTrigger.TryTrigger(character.ColliderCollision);
            homingAttackAbility?.OnHomingTargetHit(this);
            ActionOnCharacterHit?.Invoke(this);
            if (m_effectSequence == null)
            {
                CoroutineUtils.OnFixedUpdate(OnComplete);
                return;
            }
            IEffectData effectData = new TargetedEffect { Target = transform };
            MessageExchangeWithCompletion<Component> exchange = ProcessManager.GetSystem<MessageManager>().ComponentMessagesWithCompletion;
            Component key = this;
            exchange.PublishMessage(in key, in effectData, OnComplete, -1);
        }
        // Original06003e39; no authored event is raised here.
        protected virtual void OnComplete() { if (m_destroyOnComplete) Destroy(gameObject); }
        // Original06003e3a; safe manager resolution before request creation/publication, base teardown last.
        public override void OnDestroy()
        {
            MessageManager manager = ProcessManager.GetSystemSafe<MessageManager>();
            HomingTargetRequestMessage request = new HomingTargetRequestMessage { Target = this };
            if (manager != null)
            {
                Component key = this;
                manager.ComponentMessagesWithoutCompletion.PublishMessage(in key, in request);
            }
            base.OnDestroy();
        }
        // Implicit original public06003e3b; true/true then genuine SystemRef initializer precedes base ctor.
    }
}
