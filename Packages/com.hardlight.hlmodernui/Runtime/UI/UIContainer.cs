using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [DisallowMultipleComponent]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class UIContainer : MonoBehaviour
    {
        [SerializeField] protected UIContainerIdentifier m_identifier;
        [SerializeField] private UIContainerBehaviour m_behaviour;
        [SerializeField] protected Canvas m_canvas;

        // HLModernUI.Runtime06000020/21: direct original fields.
        public UIContainerIdentifier Identifier => m_identifier;
        public UIContainerBehaviour Behaviour => m_behaviour;

        // Original06000022 is a genuine RET; derived containers own their setup.
        public virtual void Setup(IUIContainerParameters parameters) { }

        // Original06000023: Unity null/destroyed test, then canvas field reload.
        public void AssignCamera(Camera guiCamera)
        {
            if (m_canvas == null) return;
            m_canvas.worldCamera = guiCamera;
        }

        // Original06000024 controls Canvas.enabled rather than GameObject activity.
        public virtual void SetEnabled(bool enabled)
        {
            if (m_canvas == null) return;
            m_canvas.enabled = enabled;
        }

        // Original06000025: an assigned live canvas wins; only this component's
        // GameObject is queried, and a failed query leaves the original field.
        protected virtual void OnValidate()
        {
            Canvas canvas = null;
            if (m_canvas != null) return;
            if (TryGetComponent<Canvas>(out canvas)) m_canvas = canvas;
        }
        // Original06000026: implicit protected MonoBehaviour base constructor.
    }
}
