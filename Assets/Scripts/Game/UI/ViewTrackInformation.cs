using System.Collections.Generic;
using System.Text;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000608; bodies translated from the 1.10.1 x86-64 binary, 0x564BE0..0x564FB0.
    // Debug overlay of the track manager: cycles the visible info panel with the DebugNextInfo input and publishes
    // the instance prefab pool texts.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ViewTrackInformation : MonoBehaviour
    {
        [Tooltip("List of info panels to cycle visibility.")]
        [SerializeField]
        private List<GameObject> m_infoList = new List<GameObject>();
        public readonly Bindable<string> InstancePrefabPoolsInfo = new Bindable<string>();
        private int m_infoIndex;
        private StringBuilder m_stringInfo = new StringBuilder();
        private readonly SystemRef<TrackManager> m_trackManagerRef = ProcessManager.GetSystemRef<TrackManager>(null, true);

        // 06002062 (1.10.1 RVA 0x564BE0)
        protected virtual void OnEnable()
        {
            ControlMapping.Subscribe(GameInput.DebugNextInfo, OnDebugNextInfo, InputTrigger.Down);
            EnableInfoList();
        }

        // 06002063 (1.10.1 RVA 0x564D90)
        protected virtual void OnDisable()
        {
            ControlMapping.Unsubscribe(GameInput.DebugNextInfo, OnDebugNextInfo);
        }

        // 06002064 (1.10.1 RVA 0x564E30)
        private void OnDebugNextInfo(float amount)
        {
            m_infoIndex = (m_infoIndex + 1) % m_infoList.Count;
            EnableInfoList();
        }

        // 06002065 (1.10.1 RVA 0x564D00): only the panel at the current index is visible.
        private void EnableInfoList()
        {
            for (int i = 0; i < m_infoList.Count; i++)
                m_infoList[i].SetActive(i == m_infoIndex);
        }

        // 06002066 (1.10.1 RVA 0x564EF0)
        protected virtual void Update()
        {
            if (m_trackManagerRef.IsValid())
            {
                TrackManager trackManager = m_trackManagerRef.Get();
                m_stringInfo.Length = 0;
                trackManager.DebugGetInstancePrefabPoolsInfo(m_stringInfo);
                InstancePrefabPoolsInfo.Value = m_stringInfo.ToString();
            }
        }

        // 06002067 (1.10.1 RVA 0x564FB0): the field initialisers are the whole constructor.
        public ViewTrackInformation()
        {
        }
    }
}
