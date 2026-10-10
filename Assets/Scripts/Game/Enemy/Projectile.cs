using System;
using System.Diagnostics;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Complete original abstract owner02000670: 17 concrete APIs and four
    //abstract APIs. Runtime, compiler binding and engine acceptance are pending.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class Projectile : TimeScaledComponent_SDT
    {
        [SerializeField, Tooltip("When projectile is spawned but not yet fired into motion.")]
        private UnityEvent m_onWarmingUp;
        [SerializeField, Tooltip("When projectile motion begins.")]
        private UnityEvent m_onFired;
        [SerializeField, Tooltip("When projectile lifetime ends or it has collided, and movement stops. EndCooldown should be called from here to clean up the projectile.")]
        private UnityEvent m_onCoolingDown;
        [SerializeField] protected Collider m_collider;
        protected ProjectileDefinition m_definition;
        protected float m_lifeRemainingSeconds;
        protected ProjectileState m_state = ProjectileState.Expired;
        private Collider m_ownerCollider;
        private readonly RaycastHit[] m_raycastHits = new RaycastHit[4];
        protected CharacterManager m_characterManager;
        protected HazardDefinition m_hazardDefinition;
        public Action<Projectile> OnExpired;

        public ProjectileType ProjectileType => m_definition.ProjectileType;

        //060022ca: base registration precedes deactivation and state publication.
        protected override void Awake()
        {
            base.Awake();
            gameObject.SetActive(false);
            m_state = ProjectileState.Expired;
        }

        //060022cb: retain publication and callback order, including faults.
        public void Setup(Vector3 focus, Collider ownerCollider)
        {
            m_characterManager = ProcessManager.GetSystem<CharacterManager>();
            m_state = ProjectileState.WarmingUp;
            m_ownerCollider = ownerCollider;
            UpdateFocus(focus);
            gameObject.SetActive(true);
            m_onWarmingUp.Invoke();
        }

        //060022cc retains Conditional(BUILD_DEVELOPMENT). Both shipping bodies
        //only read activeInHierarchy; no state comparison or assertion survives.
        [Conditional("BUILD_DEVELOPMENT")]
        private void AssertState(ProjectileState state, bool gameObjectActive)
        {
            _ = gameObject.activeInHierarchy;
        }

        public abstract void UpdateFocus(Vector3 target);
        protected abstract Vector3 DoMovement(float adjustedDeltaTime);

        public virtual void Fire()
        {
            m_lifeRemainingSeconds = m_definition.MaxLifespanSeconds;
            m_state = ProjectileState.Fired;
            m_onFired.Invoke();
        }

        public void SetLifeRemaining(float timeSeconds)
        {
            m_lifeRemainingSeconds = timeSeconds;
        }

        //060022d1: movement and collisions still run when lifetime expires;
        //a collision takes priority over the expired-lifetime cooldown path.
        protected override void InternalUpdate(float deltaTime)
        {
            if (m_state != ProjectileState.Fired) return;
            bool expired = DoLifetime(ref deltaTime);
            Vector3 movement = DoMovement(deltaTime);
            if (DoCollisions(movement, out RaycastHit hitInfo))
            {
                if (ShouldTeleportToHitPoint()) transform.position = hitInfo.point;
                Hit(hitInfo.collider);
            }
            else if (expired)
            {
                EnterCooldown();
            }
        }

        //060022d2: publish remaining time first, then shorten the caller's step
        //only for ordered nonpositive time. NaN skips adjustment and returns false.
        protected virtual bool DoLifetime(ref float deltaTime)
        {
            m_lifeRemainingSeconds -= deltaTime;
            if (m_lifeRemainingSeconds <= 0f) deltaTime += m_lifeRemainingSeconds;
            return m_lifeRemainingSeconds <= 0f;
        }

        //060022d3: examine returned hits in reverse order, with no sorting.
        //The out value is published before owner/virtual filtering; false can
        //leave the last ignored hit in hitInfo rather than resetting it.
        protected virtual bool DoCollisions(Vector3 movement, out RaycastHit hitInfo)
        {
            hitInfo = default;
            int count = m_collider.PhysicsCastFromCollider(movement,
                m_definition.CollisionLayerMask, m_raycastHits, QueryTriggerInteraction.Ignore);
            for (int i = count - 1; i >= 0; --i)
            {
                hitInfo = m_raycastHits[i];
                if (hitInfo.collider == m_ownerCollider) continue;
                if (ShouldIgnoreCollider(hitInfo.collider)) continue;
                return true;
            }
            return false;
        }

        protected virtual void Hit(Collider hitCollider)
        {
            if (IsColliderPlayer(hitCollider, out Character currentCharacter))
                currentCharacter.TryTakeDamage(m_hazardDefinition, null);
            EnterCooldown();
        }

        //060022d5: original Unity equality and inherited ColliderCollision API;
        //a failed lookup or mismatch always clears the supplied out reference.
        protected bool IsColliderPlayer(Collider hitCollider, out Character currentCharacter)
        {
            if (m_characterManager.TryGetCurrentCharacter(out currentCharacter)
                && hitCollider == currentCharacter.ColliderCollision)
                return true;
            currentCharacter = null;
            return false;
        }

        protected abstract bool ShouldIgnoreCollider(Collider collider);
        protected abstract bool ShouldTeleportToHitPoint();

        protected void EnterCooldown()
        {
            m_state = ProjectileState.CoolingDown;
            m_onCoolingDown.Invoke();
        }

        //060022d9: the shipping implementation ignores allowExceptions and
        //returns only for CoolingDown or Expired; no exception is manufactured.
        public virtual void ForceEnterCoolDown(bool allowExceptions)
        {
            if (m_state == ProjectileState.CoolingDown || m_state == ProjectileState.Expired)
                return;
            EnterCooldown();
        }

        public void EndCooldown()
        {
            Kill();
        }

        //060022db: the callback follows state publication and optional
        //deactivation, also when either Unity object compares equal to null.
        //Repeated calls retain the original repeated callback behavior.
        public void Kill()
        {
            m_state = ProjectileState.Expired;
            if (this != null && gameObject != null) gameObject.SetActive(false);
            OnExpired?.Invoke(this);
        }

        //060022dc: definition publication precedes system/dictionary access.
        //The genuine dictionary indexer retains missing-key and null faults.
        public void Initialise(ProjectileDefinition definition)
        {
            m_definition = definition;
            m_hazardDefinition = ProcessManager.GetSystem<DataManager>()
                .HazardDefinitions[m_definition.HazardType];
        }

        //060022dd: state and four-hit buffer field initializers precede base ctor.
        protected Projectile() { }

        protected enum ProjectileState
        {
            WarmingUp = 0,
            Fired = 1,
            CoolingDown = 2,
            Expired = 3
        }
    }
}
