using System;
using System.Collections.Generic;

namespace Hardlight
{
    // Original HLUnityCore.Runtime.dll type. Named arm64 native functions
    // establish reference counts, overwrite behavior and registry interaction.
    public class FSMManager : ISystem
    {
        private struct FSMRef
        {
            public int RefCount;
            public FiniteStateMachine FSM;
            // 0x060003f5: direct field stores.
            public FSMRef(FiniteStateMachine fsm, int refCount = 1)
            {
                RefCount = refCount;
                FSM = fsm;
            }
        }

        private readonly Dictionary<int, FSMRef> m_fsmRefs;
        private readonly Dictionary<int, FiniteStateMachine> m_fsmLookUp;
        // 0x060003ea; arm64 0x1ac5cfc.
        public IReadOnlyDictionary<int, FiniteStateMachine> FSMs => m_fsmLookUp;

        // 0x060003f4; arm64 0x1ac5d04.
        public FSMManager()
        {
            m_fsmRefs = new Dictionary<int, FSMRef>();
            m_fsmLookUp = new Dictionary<int, FiniteStateMachine>();
        }

        // 0x060003eb; arm64 0x1abbe0c. The original existence check uses
        // the default name even when managerName was supplied.
        public static FSMManager GetManager(string managerName = null)
        {
            if (ProcessManager.IsSystemNull<FSMManager>())
                ProcessManager.RegisterSystem(new FSMManager(), managerName);
            return ProcessManager.GetSystem<FSMManager>(managerName);
        }

        // 0x060003ec; arm64 0x1abbf44..0x1abc168. Overwriting increments
        // the existing reference count and replaces the looked-up instance.
        public void AddFSM(FiniteStateMachine fsm, bool isOverwriteOk = false)
        {
            if (m_fsmRefs.TryGetValue(fsm.FSMId, out FSMRef reference))
            {
                string name = GraphNameLookup.LookupNameUsingId(fsm.FSMId);
                if (!isOverwriteOk)
                    throw new Exception("FSM with the following id already exists in the FSMManager: '" + name + "'");
                reference.RefCount = unchecked(reference.RefCount + 1);
                reference.FSM = fsm;
                m_fsmLookUp[fsm.FSMId] = fsm;
                m_fsmRefs[fsm.FSMId] = reference;
            }
            else
            {
                m_fsmLookUp[fsm.FSMId] = fsm;
                m_fsmRefs[fsm.FSMId] = new FSMRef(fsm);
            }
        }

        // 0x060003ed; arm64 0x1ac5de4: lookup cleared before reference table.
        public void ClearAllFSMs()
        {
            m_fsmLookUp.Clear();
            m_fsmRefs.Clear();
        }

        // 0x060003ee; arm64 0x1ac5e64.
        public bool FSMExists(FSMIdentifier fsmId) => m_fsmLookUp.ContainsKey(fsmId.Id);
        // 0x060003ef; arm64 0x1ac5f34: lookup does not acquire a reference.
        public bool TryGetFSM(FSMIdentifier fsmId, out FiniteStateMachine fsm) => m_fsmLookUp.TryGetValue(fsmId.Id, out fsm);

        // 0x060003f0; arm64 0x1ac5280.
        public bool TryAcquireFSM(FSMIdentifier fsmId, out FiniteStateMachine fsm)
        {
            if (m_fsmRefs.TryGetValue(fsmId.Id, out FSMRef reference))
            {
                reference.RefCount = unchecked(reference.RefCount + 1);
                m_fsmRefs[fsmId.Id] = reference;
                fsm = reference.FSM;
                return true;
            }
            fsm = null;
            return false;
        }

        // 0x060003f1; arm64 0x1ac6014. True means the last reference was
        // removed; a successful decrement with references remaining returns false.
        public bool ReleaseFSM(FSMIdentifier fsmId)
        {
            if (!m_fsmRefs.TryGetValue(fsmId.Id, out FSMRef reference)) return false;
            reference.RefCount = unchecked(reference.RefCount - 1);
            if (reference.RefCount > 0)
            {
                m_fsmRefs[fsmId.Id] = reference;
                return false;
            }
            m_fsmRefs.Remove(fsmId.Id);
            m_fsmLookUp.Remove(fsmId.Id);
            return true;
        }

        // 0x060003f2; arm64 0x1ac62b4: uses the ID, not object identity.
        public bool ReleaseFSM(FiniteStateMachine fsm) => ReleaseFSM(new FSMIdentifier(fsm.FSMId));

        // 0x060003f3; arm64 0x1abf2e4. Caller dictionary remains intact.
        public void ReleaseFSMs(Dictionary<string, FiniteStateMachine> fsmDictionary)
        {
            foreach (KeyValuePair<string, FiniteStateMachine> entry in fsmDictionary)
                ReleaseFSM(new FSMIdentifier(entry.Value.FSMId));
        }
    }
}
