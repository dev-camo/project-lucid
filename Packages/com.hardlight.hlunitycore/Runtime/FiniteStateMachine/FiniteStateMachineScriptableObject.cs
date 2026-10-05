using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Hardlight
{
    [CreateAssetMenu(fileName = "FSM", menuName = "Hardlight/HLUnityCore/Finite State Machine", order = 0)]
    public class FiniteStateMachineScriptableObject : ScriptableObject
    {
        [SerializeField, Tooltip("The FSM JSON can contain multiple FSMs, specify which one should be used")]
        private string m_name;
        [Header("Advanced Settings"), SerializeField, Tooltip("If true then it's ok for existing FSMs to be overwritten with the new FSMs where there's a name clash")]
        private bool m_isOverwriteOk;
        [SerializeField, Tooltip("If true then existing FSMs will be reused when there's a name clash. The loaded FSM will be discarded")]
        private bool m_reuseExisting;
        [Tooltip("If true then the loaded FSMs will be released from the FSMManager when ReleaseFSM is called"), SerializeField]
        private bool m_releaseOnDestroy = true;
        [Tooltip("Reference scriptable objects used by the FSM's states and transitions to ensure they're loaded."), SerializeField]
        private List<ScriptableObject> m_referencedScriptableObjects = new List<ScriptableObject>();
        [SerializeField, Header("JSON"), Tooltip("Path is relative to the StreamingAssets directory")]
        private string m_relativePathToJSON;
        [SerializeField, Tooltip("JSON data for the FSM. If specified 'Relative Path To JSON' is ignored"), TextArea(5, 30)]
        private string m_embeddedJSON;
        private Dictionary<string, FiniteStateMachine> m_fsmDictionary;
        private FiniteStateMachine m_fsm;
        private int m_refCount;
        public Action<FiniteStateMachineScriptableObject> OnInitialisationComplete;

        // HLUnityCore.Runtime.dll:Hardlight.FiniteStateMachineScriptableObject:
        // 0x06000392; arm64 0x1abf0a4.
        public FiniteStateMachine FSM => m_fsm;
        // Original token 0x06000393; arm64 0x1abe58c. A missing dictionary
        // is silent; null lookup keys still reach Dictionary.TryGetValue.
        public FiniteStateMachine GetFSMByName(string fsmName)
        {
            if (m_fsmDictionary == null) return null;
            if (!m_fsmDictionary.TryGetValue(fsmName, out FiniteStateMachine fsm))
                HLOutput.LogError("Failed to find FSM with name '" + fsmName + "'. ScriptableObject: " + name);
            return fsm;
        }
        // Original token 0x06000394 wrapper 0x1abe900; iterator MoveNext
        // 0x0600039b at 0x1abf7ac. Count increments only on first enumeration.
        // Disposal/failed child/callback does not roll back the acquired count.
        public IEnumerator AcquireFSM()
        {
            ++m_refCount;
            if (m_refCount >= 2 && m_fsm != null) yield break;
            if (string.IsNullOrEmpty(m_embeddedJSON))
                yield return FSMClassFactory.ConstructFSMsFromFile(m_relativePathToJSON, ProcessFSM, m_reuseExisting, m_isOverwriteOk);
            else yield return FSMClassFactory.ConstructFSMs(m_embeddedJSON, ProcessFSM, null, m_reuseExisting, m_isOverwriteOk);
            OnInitialisationComplete?.Invoke(this);
        }
        // Original token 0x06000395; arm64 0x1abf0d8. GetJSON treats
        // whitespace differently from AcquireFSM and uses direct StreamReader.
        public string GetJSON()
        {
            if (!string.IsNullOrWhiteSpace(m_embeddedJSON)) return m_embeddedJSON;
            if (string.IsNullOrWhiteSpace(m_relativePathToJSON)) return null;
            using (var reader = new StreamReader(Path.Combine(Application.streamingAssetsPath, m_relativePathToJSON)))
            {
                string json = reader.ReadToEnd();
                reader.Close();
                return json;
            }
        }
        // Original token 0x06000396; arm64 0x1abddcc. No underflow guard,
        // reference clearing or dictionary clearing accompanies the final release.
        public void ReleaseFSM()
        {
            --m_refCount;
            if (m_refCount == 0 && m_fsmDictionary != null && m_releaseOnDestroy)
                FSMManager.GetManager().ReleaseFSMs(m_fsmDictionary);
        }
        // Original token 0x06000397; arm64 0x1abf444. Error lists preserve
        // old selection; other branches assign the dictionary before inspecting
        // it. Missing keys clear m_fsm through the original out argument.
        private void ProcessFSM(Dictionary<string, FiniteStateMachine> fsmDictionary, List<string> errors)
        {
            string file = string.IsNullOrEmpty(m_relativePathToJSON) ? "Embedded JSON" : m_relativePathToJSON;
            if (errors != null && errors.Count > 0)
            {
                HLOutput.LogError("Errors occurred when creating FSM. ScriptableObject: " + name + ", File: " + file);
                return;
            }
            m_fsmDictionary = fsmDictionary;
            if (m_fsmDictionary.Count == 0)
            {
                HLOutput.LogError("Loaded FSM JSON contains no FSMs. ScriptableObject: " + name + ", File: " + file);
                return;
            }
            if (string.IsNullOrEmpty(m_name))
            {
                HLOutput.LogError("FSM name has not been specified. ScriptableObject: " + name + ", File: " + file);
                return;
            }
            if (!m_fsmDictionary.TryGetValue(m_name, out m_fsm))
                HLOutput.LogError("Failed to find FSM with name '" + m_name + "'. ScriptableObject: " + name + ", File: " + file);
        }
        // Original token 0x06000398; arm64 0x1abf714. Initializers above
        // allocate retention list and enable release before ScriptableObject ctor.
        public FiniteStateMachineScriptableObject() { }
    }
}
