// Original HLUnityCore.Runtime.dll, supplied Sonic Dream Team 1.10.1.
// Hardlight.FSMStateUsesScriptableObject; TypeDef 0x020000c3.
// 0x06000458 .ctor: x86_64 0x1ac6ae0.
// 0x06000459 ConstructInstance: x86_64 0x1ac6cb0.
// 0x0600045a SerialiseRuntimeToJSON: x86_64 0x1ac6e00.
// 0x0600045b FindByName: x86_64 0x1ac6b70.
// Reconstructed from shipping metadata and the native implementation.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Core/{0}")]
    public class FSMStateUsesScriptableObject : FSMState
    {
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [GraphNodeDefaultName("UsesScriptableObject")]
        private class JSONCtorArgs
        {
            [GraphUnityObject(typeof(ScriptableObject))]
            public string ScriptableObjectNameA;
            [GraphUnityObjectPopup(typeof(ScriptableObject), true)]
            public string ScriptableObjectNameB;
        }
        private readonly ScriptableObject m_scriptableObjectA;
        private readonly ScriptableObject m_scriptableObjectB;

        // The state is identified by name (an implicit FSMIdentifier) and is
        // registered before either lookup runs.
        public FSMStateUsesScriptableObject(FiniteStateMachine fsm, string stateName, string scriptableObjectNameA, string scriptableObjectNameB)
            : base(fsm, stateName)
        {
            m_scriptableObjectA = FindByName(scriptableObjectNameA);
            m_scriptableObjectB = FindByName(scriptableObjectNameB);
        }
        // The identifier becomes its name (lazily resolved from the ID) before the args are read.
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            return new FSMStateUsesScriptableObject(fsm, stateId, args.ScriptableObjectNameA, args.ScriptableObjectNameB);
        }
        // Unity-null references serialise as null names.
        public override string SerialiseRuntimeToJSON() => JsonUtility.ToJson(new JSONCtorArgs
        {
            ScriptableObjectNameA = m_scriptableObjectA ? m_scriptableObjectA.name : null,
            ScriptableObjectNameB = m_scriptableObjectB ? m_scriptableObjectB.name : null
        });
        // Empty names skip the search silently. Otherwise the first loaded
        // ScriptableObject with an exactly equal name wins; a miss is logged.
        private ScriptableObject FindByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            ScriptableObject found = null;
            foreach (ScriptableObject scriptableObject in Resources.FindObjectsOfTypeAll<ScriptableObject>())
            {
                if (scriptableObject.name == name)
                {
                    found = scriptableObject;
                    break;
                }
            }
            if (found == null)
                HLOutput.LogError(string.Format("Referenced scriptable object '{0}' not found. Used by FSM state '{1}'. Add to FSM's referenced scriptable object array", name, this));
            return found;
        }
    }
}
