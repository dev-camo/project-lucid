using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000abc; Break 0x6E2E70.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class BreakableObject : MonoBehaviour
    {
        [SerializeField]
        [Header("Optional components")]
        private HLAudioSfxComponent m_audioSfxComponent;
        [SerializeField]
        private PrefabSpawner m_prefabSpawner;
        [SerializeField]
        private GameObject[] m_objectsToHide;
        [SerializeField]
        private UnityEvent m_onBreakEvent;

        // The optional components are skipped while Unity-null; the hidden objects
        // are deactivated in array order before the break event is raised.
        public void Break()
        {
            if (m_audioSfxComponent != null)
                m_audioSfxComponent.PlayOneShotAtSource();
            if (m_prefabSpawner != null)
                m_prefabSpawner.Spawn();
            for (int i = 0; i < m_objectsToHide.Length; i++)
                m_objectsToHide[i].SetActive(false);
            m_onBreakEvent.Invoke();
        }
    }
}
