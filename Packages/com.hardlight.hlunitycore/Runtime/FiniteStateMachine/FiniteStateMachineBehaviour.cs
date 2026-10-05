using System.Collections;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FiniteStateMachineBehaviour : MonoBehaviour
    {
        private enum UpdateType { Update = 0, FixedUpdate = 1, All = 2 }

        [SerializeField, Tooltip("Select the scriptable object that has the correct FSM")]
        private FiniteStateMachineScriptableObject m_fsmAsset;
        [SerializeField, Tooltip("Override the default FSM used by the scriptable object - leave blank to use the default")]
        private string m_overrideFSMName;
        [SerializeField, Tooltip("Determines if FSM updates from Unity's Update or FixedUpdate")]
        private UpdateType m_updateType;
        private FiniteStateMachine m_fsm;
        private readonly List<IGraphUser> m_fsmUsers = new List<IGraphUser>();
        private readonly List<IGraphUser> m_fsmUsersPendingInitialise = new List<IGraphUser>();

        // HLUnityCore.Runtime.dll:0x0600037b; arm64 0x1abdc4c wrapper,
        // generated MoveNext 0x06000386 at 0x1abe740. The yielded child
        // acquires the asset before the current component fields are selected.
        private IEnumerator Start()
        {
            if (m_fsmAsset == null)
            {
                HLOutput.LogError("You must specify a FSM asset to use. Behaviour: " + name);
                yield break;
            }
            yield return m_fsmAsset.AcquireFSM();
            OneTimeInitialisation();
        }

        // Original token 0x0600037c; arm64 0x1abdcf0. Release even if
        // Start never acquired; no clearing of users or selected FSM occurs.
        private void OnDestroy()
        {
            if (m_fsmAsset != null) m_fsmAsset.ReleaseFSM();
        }

        // Original token 0x0600037d; arm64 0x1abde20. Enumerate the live
        // list; current FSM and engine delta time are read for each user.
        private void Update()
        {
            if (!UpdatesOnType(UpdateType.Update) || m_fsm == null) return;
            foreach (IGraphUser user in m_fsmUsers)
                m_fsm.Update(user, new FSMUpdateContext(Time.deltaTime));
        }

        // Original token 0x0600037e; arm64 0x1abdf94. Native context packs
        // fixedDeltaTime with original FSMUpdateType.FixedUpdate value one.
        private void FixedUpdate()
        {
            if (!UpdatesOnType(UpdateType.FixedUpdate) || m_fsm == null) return;
            foreach (IGraphUser user in m_fsmUsers)
                m_fsm.Update(user, new FSMUpdateContext(Time.fixedDeltaTime, FSMUpdateType.FixedUpdate));
        }

        // Original token 0x0600037f; arm64 0x1abe0f8. Append first, without
        // null/duplicate checks. Pending entries retain only the user, not flags.
        public void AddUser(IGraphUser user, bool initialiseUser = true,
            bool initialiseUserIfNoActiveState = true)
        {
            m_fsmUsers.Add(user);
            if (!initialiseUser && !initialiseUserIfNoActiveState) return;
            if (m_fsm == null) m_fsmUsersPendingInitialise.Add(user);
            else if (initialiseUser || m_fsm.GetActiveState(user.Storage) == null)
                m_fsm.InitialiseUser(user);
        }

        // Original token 0x06000380; arm64 0x1abe31c. Remove only one
        // active-list occurrence; pending rows are deliberately untouched.
        public void RemoveUser(IGraphUser user, bool clearUser = true)
        {
            m_fsmUsers.Remove(user);
            if (clearUser && m_fsm != null) m_fsm.ClearUser(user);
        }

        // Original token 0x06000381; arm64 0x1abe3b0. Empty override uses
        // the asset default; whitespace is a real lookup. Pending initialization
        // is unconditional and clears only after successful live enumeration.
        private void OneTimeInitialisation()
        {
            m_fsm = string.IsNullOrEmpty(m_overrideFSMName)
                ? m_fsmAsset.FSM : m_fsmAsset.GetFSMByName(m_overrideFSMName);
            if (m_fsm == null) return;
            foreach (IGraphUser user in m_fsmUsersPendingInitialise)
                m_fsm.InitialiseUser(user);
            m_fsmUsersPendingInitialise.Clear();
        }

        // Original token 0x06000382; arm64 0x1abdf80. All or exact match.
        private bool UpdatesOnType(UpdateType type) => m_updateType == UpdateType.All || m_updateType == type;

        // Original token 0x06000383; arm64 0x1abe68c. Both retention lists
        // are allocated before the MonoBehaviour base constructor.
        public FiniteStateMachineBehaviour() { }
    }
}
