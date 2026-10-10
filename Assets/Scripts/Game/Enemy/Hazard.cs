using System;
using Hardlight;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Complete original Game.Runtime 02000668, eight methods. These source
    // statements preserve the common managed behavior inferred from both CPUs;
    // native unchecked memory/fault scheduling is not a CLR equivalence claim.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class Hazard : MonoBehaviour
    {
        [SerializeField] private HazardType m_hazardType;
        [SerializeField] private string m_requiredCollisionTag;
        [SerializeField] private UnityEvent<Collider> m_onDamageEvent;
        [SerializeField] private RigidbodyForceTrigger m_knockbackForceTrigger;
        private HazardDefinition m_hazardDefinition;
        protected bool m_playerDamageableSet;
        protected IPlayerDamageable m_playerDamageable;
        protected bool m_initialised;

        // Original 060022a0: true virtual new-slot lifecycle method. Registration
        // retains the original optional name null and no-exception true arguments.
        protected virtual void Start()
        {
            ProcessManager.GetSystemRef<DataManager>(null, true).InvokeOnValid(Initialise);
        }

        // Original 060022a1: the lookup result is discarded. A missing key writes
        // the out field and still marks this component initialised.
        private void Initialise(DataManager dataManager)
        {
            dataManager.HazardDefinitions.TryGetValue(m_hazardType, out m_hazardDefinition);
            m_initialised = true;
        }

        // Original 060022a2: enabled is queried before the initialised flag.
        private void OnTriggerEnter(Collider other)
        {
            if (!enabled || !m_initialised)
                return;
            TryDamageCollider(other);
        }

        // Original 060022a3: both gates precede collision.collider retrieval.
        private void OnCollisionEnter(Collision collision)
        {
            if (!enabled || !m_initialised)
                return;
            TryDamageCollider(collision.collider);
        }

        // Original 060022a4: instance tag.Equals retains its original null fault.
        // IDamageable is an interface, so its null test is a managed reference
        // comparison. The definition is evaluated before delegate construction.
        private void TryDamageCollider(Collider other)
        {
            if (!string.IsNullOrEmpty(m_requiredCollisionTag) &&
                !other.tag.Equals(m_requiredCollisionTag))
                return;

            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null)
                return;
            damageable.TryTakeDamage(m_hazardDefinition, OnProcessDamageAction);
        }

        // Original 060022a5: parent lookup occurs once, including a null result,
        // before dispatch. Live fields are reread after callbacks; exceptions stop
        // later calls. ReturnDamage has no damage-event notification.
        private void OnProcessDamageAction(DamageAction damageAction, Collider damagedCollider)
        {
            if (!m_playerDamageableSet)
            {
                m_playerDamageable = GetComponentInParent<IPlayerDamageable>();
                m_playerDamageableSet = true;
            }

            switch (damageAction)
            {
                case DamageAction.None:
                    return;
                case DamageAction.HardFailure:
                case DamageAction.Knockback:
                    if (!m_hazardDefinition.InstantDeath && m_knockbackForceTrigger != null)
                        m_knockbackForceTrigger.TryTrigger(damagedCollider);
                    m_playerDamageable?.RegisterHitPlayer(damageAction, m_hazardType);
                    m_onDamageEvent.Invoke(damagedCollider);
                    return;
                case DamageAction.Stumble:
                    m_playerDamageable?.RegisterHitPlayer(DamageAction.Stumble, m_hazardType);
                    m_onDamageEvent.Invoke(damagedCollider);
                    return;
                case DamageAction.ReturnDamage:
                    m_playerDamageable?.ReceiveHit(damagedCollider);
                    return;
                default:
                    throw new ArgumentOutOfRangeException("damageAction", damageAction, null);
            }
        }

        // Original 060022a6: null leaves both previous fields unchanged.
        public void RegisterPlayerDamageableInterface(IPlayerDamageable playerDamageable)
        {
            if (playerDamageable == null)
                return;
            m_playerDamageable = playerDamageable;
            m_playerDamageableSet = true;
        }

        // Original 060022a7: implicit constructor calls only MonoBehaviour base.
    }
}
