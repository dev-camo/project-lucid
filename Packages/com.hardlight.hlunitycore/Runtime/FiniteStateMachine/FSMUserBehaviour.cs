using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FSMUserBehaviour : MonoBehaviour
    {
        [Tooltip("The FSM the user will use when 'Update' is called"), SerializeField]
        private FiniteStateMachineBehaviour m_finiteStateMachine;
        [Tooltip("If true the FSM user will be initialised - setting its initial state within the FSM")]
        [Header("Advanced Settings"), SerializeField]
        private bool m_initialiseUserOnEnable = true;
        [Tooltip("If true the FSM user will be initialised on enable, but only if the user does not already have an active state")]
        [SerializeField]
        private bool m_initialiseUserOnEnableIfNoActiveState = true;
        [Tooltip("If true the FSM user will be cleared from the FSM - any data used by the FSM will be removed, and the currently active state will be cleared")]
        [SerializeField]
        private bool m_clearUserOnDisable = true;
        protected IGraphUser m_user;

        // HLUnityCore.Runtime.dll:0x060004dd; arm64 0x1ad1a1c.
        protected virtual void Awake() { m_user = AcquireUserInstance(); }
        // Original token 0x060004de; arm64 0x1ad1a50. Unity object-null
        // semantics guard the target component; raw user/flags pass through.
        protected virtual void OnEnable()
        {
            if (m_finiteStateMachine == null) return;
            m_finiteStateMachine.AddUser(m_user, m_initialiseUserOnEnable,
                m_initialiseUserOnEnableIfNoActiveState);
        }
        // Original token 0x060004df; arm64 0x1ad1b08.
        protected virtual void OnDisable()
        {
            if (m_finiteStateMachine == null) return;
            m_finiteStateMachine.RemoveUser(m_user, m_clearUserOnDisable);
        }
        // Original token 0x060004e0; arm64 0x1ad1c18: virtual release.
        protected virtual void OnDestroy() { ReleaseUserInstance(); }
        // Original token 0x060004e1; arm64 0x1ad1c24. Fresh user/storage
        // with original default history length five.
        protected virtual IGraphUser AcquireUserInstance() => new FSMUser();
        // Original token 0x060004e2; arm64 0x1ad1cd4. Null-safe interface
        // call; retain m_user even after its storage has been destroyed.
        protected virtual void ReleaseUserInstance() { m_user?.DestroyUser(); }
        // Original token 0x060004e3; arm64 0x1ad1d90. All three flags
        // initialize true before the MonoBehaviour constructor.
        public FSMUserBehaviour() { }
    }
}
