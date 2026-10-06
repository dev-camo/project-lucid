using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace Hardlight
{
    [DisallowMultipleComponent]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIVisibilityGroupMember : MonoBehaviour
    {
        [SerializeField] private UIVisibilityGroupDefinition m_visibilityGroupDefinition;
        [Tooltip("If true, the game object this is attached to will be set inactive when invisible, and active when visible.")]
        [SerializeField] private bool m_setGameObjectActive = true;
        [SerializeField]
        [Tooltip("Events triggered when the member becomes visible, including on initialisation if the member is initially visible.")]
        private UnityEvent m_onBecomeVisible;
        [Tooltip("Events triggered when the member becomes invisible, including on initialisation if the member is initially invisible.")]
        [SerializeField] private UnityEvent m_onBecomeInvisible;
        private SystemRef<UIManager> m_uiManagerSystemRef;
        private GameObject m_gameObject;
        private bool m_isVisible;
        private bool m_initialVisibilitySet;

        // HLModernUI.Runtime06000005; caches the owner before obtaining the real
        // registry row. Invalid hosts apply initial visibility before subscribing.
        private void Awake()
        {
            m_gameObject = gameObject;
            m_uiManagerSystemRef = ProcessManager.GetSystemRef<UIManager>();
            if (m_uiManagerSystemRef.IsValid())
                m_uiManagerSystemRef.Get().RegisterUIVisibilityGroupMember(m_visibilityGroupDefinition, this);
            else
            {
                OnVisibilityChanged(m_visibilityGroupDefinition.InitialVisibility);
                m_uiManagerSystemRef.InvokeOnValid(OnUIManagerValid);
            }
        }

        //06000006: validity is rechecked; the original does not unsubscribe a
        // waiting InvokeOnValid callback when no UIManager exists yet.
        private void OnDestroy()
        {
            if (m_uiManagerSystemRef.IsValid())
                m_uiManagerSystemRef.Get().UnregisterUIVisibilityGroupMember(m_visibilityGroupDefinition, this);
        }

        //06000007: callback consumes its supplied manager directly.
        private void OnUIManagerValid(UIManager uiManager)
        {
            uiManager.RegisterUIVisibilityGroupMember(m_visibilityGroupDefinition, this);
        }

        //06000008: publish both state flags before callbacks. Callbacks can change
        // the activation flag/game object; those fields are read again afterward.
        public void OnVisibilityChanged(bool visible)
        {
            if (m_isVisible == visible && m_initialVisibilitySet) return;
            m_isVisible = visible;
            m_initialVisibilitySet = true;
            if (visible) m_onBecomeVisible?.Invoke();
            else m_onBecomeInvisible?.Invoke();
            if (m_setGameObjectActive) m_gameObject.SetActive(visible);
        }
        //06000009: the sole explicit initial value is m_setGameObjectActive=true.
    }
}
