using System.Collections.Generic;
using System.Text;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000602; bodies translated from the 1.10.1 x86-64 binary, 0x5633E0..0x563B20.
    // Debug overlay of an actor: cycles the visible info panel with the DebugNextInfo input and publishes the
    // information texts as bindables. The development-only calls that fill the actor, state, FSM, collision and animator
    // texts (Actor.DebugGet...) are compiled out of the retail build, so only the entity activation text is ever filled.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ViewActorInformation : MonoBehaviour
    {
        [Tooltip("List of info panels to cycle visibility.")]
        [SerializeField] private List<GameObject> m_infoList = new List<GameObject>();
        public readonly Bindable<string> ActorInfo = new Bindable<string>();
        public readonly Bindable<string> StateInfo = new Bindable<string>();
        public readonly Bindable<string> TransformInfo = new Bindable<string>();
        public readonly Bindable<string> FsmInfo = new Bindable<string>();
        public readonly Bindable<string> CollisionInfo = new Bindable<string>();
        public readonly Bindable<string> EntityActivationInfo = new Bindable<string>();
        public readonly Bindable<string> AnimatorColumn0Info = new Bindable<string>();
        public readonly Bindable<string> AnimatorColumn1Info = new Bindable<string>();
        private int m_infoIndex;
        protected Actor m_actor;
        protected StringBuilder m_stringInfo = new StringBuilder();
        private SystemRef<EntityActivationManager> m_entityActivationManagerRef;

        // 0600204c (1.10.1 RVA 0x5633E0)
        private void Awake()
        {
            m_entityActivationManagerRef = ProcessManager.GetSystemRef<EntityActivationManager>(null, true);
        }

        // 0600204d (1.10.1 RVA 0x563460)
        protected virtual void OnEnable()
        {
            ControlMapping.Subscribe(GameInput.DebugNextInfo, OnDebugNextInfo, InputTrigger.Down);
            ControlMapping.Subscribe(GameInput.DebugClearFsmInfo, OnDebugClearFsmInfo, InputTrigger.Down);
            EnableInfoList();
        }

        // 0600204e (1.10.1 RVA 0x563650): the clear-FSM-info subscription is not removed again.
        protected virtual void OnDisable()
        {
            ControlMapping.Unsubscribe(GameInput.DebugNextInfo, OnDebugNextInfo);
            m_actor = null;
        }

        // 0600204f (1.10.1 RVA 0x563700)
        private void OnDebugNextInfo(float amount)
        {
            m_infoIndex = (m_infoIndex + 1) % m_infoList.Count;
            EnableInfoList();
        }

        // 06002050 (1.10.1 RVA 0x5637C0): the development-only Actor.DebugClearFsmInfo call that follows the check is
        // compiled out of the retail build.
        private void OnDebugClearFsmInfo(float amount)
        {
            if (m_actor == null)
                return;
        }

        // 06002051 (1.10.1 RVA 0x5635C0): only the panel at the current index is visible.
        private void EnableInfoList()
        {
            for (int i = 0; i < m_infoList.Count; ++i)
                m_infoList[i].SetActive(i == m_infoIndex);
        }

        // 06002052 (1.10.1 RVA 0x563820): every text is republished each frame; the animator text is split into two
        // columns, the first holding the lines up to and including the middle one.
        protected virtual void Update()
        {
            if (m_actor == null)
                return;
            m_stringInfo.Length = 0;
            ActorInfo.Value = m_stringInfo.ToString();
            m_stringInfo.Length = 0;
            StateInfo.Value = m_stringInfo.ToString();
            m_stringInfo.Length = 0;
            FsmInfo.Value = m_stringInfo.ToString();
            m_stringInfo.Length = 0;
            CollisionInfo.Value = m_stringInfo.ToString();
            m_stringInfo.Length = 0;
            m_entityActivationManagerRef.Get().GetDebugInfo(m_stringInfo);
            EntityActivationInfo.Value = m_stringInfo.ToString();
            m_stringInfo.Length = 0;
            string[] lines = m_stringInfo.ToString().Split("\n");
            m_stringInfo.Length = 0;
            int line = 0;
            for (; line <= lines.Length / 2; ++line)
                m_stringInfo.AppendLine(lines[line]);
            AnimatorColumn0Info.Value = m_stringInfo.ToString();
            m_stringInfo.Length = 0;
            for (; line < lines.Length; ++line)
                m_stringInfo.AppendLine(lines[line]);
            AnimatorColumn1Info.Value = m_stringInfo.ToString();
        }

        // 06002053 (1.10.1 RVA 0x563B20): the field initialisers are the whole constructor.
        public ViewActorInformation()
        {
        }
    }
}
