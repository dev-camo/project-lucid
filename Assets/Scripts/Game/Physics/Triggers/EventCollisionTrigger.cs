using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000764; bodies translated from the 1.10.1 x86-64 binary.
    // Awake's three lambdas keep their original <Awake>b__8_0..2 order.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class EventCollisionTrigger : MonoBehaviour, IResetCapableGameplayElement
    {
        [SerializeField] protected UnityEvent<Collider> m_onEnter;
        [SerializeField] protected UnityEvent<Collider> m_onStay;
        [SerializeField] protected UnityEvent<Collider> m_onExit;
        [SerializeField] protected UnityEvent m_onReset;
        [Tooltip("If true, then the OnEnter and OnExit events will be called during OnStay if the collision filter conditions become met or unmet.")]
        [SerializeField]
        private bool m_enableMultipleCollisionTrigger;
        private readonly SystemRef<GameplayIslandManager> m_gameplayIslandManagerRef = ProcessManager.GetSystemRef<GameplayIslandManager>();
        private readonly HashSet<Collider> m_collisionsActive = new HashSet<Collider>();

        // 0x06002955
        protected virtual void OnEnable()
        {
            m_collisionsActive.Clear();
        }

        // 0x06002956 (0x5CEF30): level and respawn resets are wired only when the reset
        // event has persistent listeners.
        protected virtual void Awake()
        {
            m_gameplayIslandManagerRef.InvokeOnValid(islandManager => islandManager.RegisterResetCapableBehaviour(this));
            if (m_onReset.GetPersistentEventCount() > 0)
            {
                ProcessManager.GetSystemRef<LevelManager>().InvokeOnValid(levelManager => levelManager.InvokeOnLevelActivated(OnLevelActivated, false));
                ProcessManager.GetSystemRef<CharacterManager>().InvokeOnValid(characterManager => characterManager.OnCharacterRespawn += ResetGameplayElement);
            }
        }

        // 0x06002957 (0x5CF120): the matching teardown tests the count against zero.
        private void OnDestroy()
        {
            if (m_onReset.GetPersistentEventCount() != 0)
            {
                if (ProcessManager.GetSystemRef<LevelManager>().TryGet(out LevelManager levelManager))
                    levelManager.RemoveLevelActivatedAction(OnLevelActivated);
                if (ProcessManager.GetSystemRef<CharacterManager>().TryGet(out CharacterManager characterManager))
                    characterManager.OnCharacterRespawn -= ResetGameplayElement;
            }
        }

        // 0x06002958: every live active collider exits before the set is cleared.
        private void OnDisable()
        {
            foreach (Collider collider in m_collisionsActive)
            {
                if (collider == null) continue;
                m_onExit.Invoke(collider);
            }
            m_collisionsActive.Clear();
        }

        // 0x06002959..0x0600295b
        protected virtual void OnTriggerEnter(Collider other)
        {
            TryTrigger(m_onEnter, other);
        }

        protected virtual void OnTriggerStay(Collider other)
        {
            TryTriggerStay(other);
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            TryTrigger(m_onExit, other);
        }

        // 0x0600295c..0x0600295e
        private void OnCollisionEnter(Collision other)
        {
            TryTrigger(m_onEnter, other.collider);
        }

        private void OnCollisionStay(Collision other)
        {
            TryTriggerStay(other.collider);
        }

        private void OnCollisionExit(Collision other)
        {
            TryTrigger(m_onExit, other.collider);
        }

        // 0x0600295f
        protected virtual bool FilterCollision(Collider other)
        {
            return true;
        }

        // 0x06002960 (0x5CF4A0): a passing collider is marked active even for the exit
        // event; a failing one is only unmarked.
        private void TryTrigger(UnityEvent<Collider> trigger, Collider other)
        {
            bool passes = FilterCollision(other);
            SetActiveCollision(passes, other);
            if (passes)
                trigger.Invoke(other);
        }

        // 0x06002961/0x06002962
        private void OnLevelActivated(LevelManagerLevel levelManagerLevel)
        {
            m_onReset.Invoke();
        }

        public void ResetGameplayElement()
        {
            m_onReset.Invoke();
        }

        // 0x06002963/0x06002964
        public Vector3 GetPosition()
        {
            return transform.position;
        }

        public bool IsDestroyed()
        {
            return this == null;
        }

        // 0x06002965 (0x5CEA00): with multiple triggering enabled, a filter change while
        // staying raises enter or exit; stay is raised only while the filter passes.
        protected void TryTriggerStay(Collider other)
        {
            bool wasActive = m_collisionsActive.Contains(other);
            bool passes = FilterCollision(other);
            SetActiveCollision(passes, other);
            if (passes)
            {
                if (m_enableMultipleCollisionTrigger && !wasActive)
                    m_onEnter.Invoke(other);
                m_onStay.Invoke(other);
            }
            else if (m_enableMultipleCollisionTrigger && wasActive)
            {
                m_onExit.Invoke(other);
            }
        }

        // 0x06002966
        private void SetActiveCollision(bool active, Collider collider)
        {
            if (active)
                m_collisionsActive.Add(collider);
            else
                m_collisionsActive.Remove(collider);
        }
    }
}
