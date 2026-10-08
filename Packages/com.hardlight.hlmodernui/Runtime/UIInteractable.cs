using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLModernUI.Runtime 02000021, all six own APIs.
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Hardlight/HLModernUI/UIToggle")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class UIInteractable : UIBehaviour
    {
        [Tooltip("Can the Interactable be interacted with?")]
        [SerializeField] private bool m_interactable = true;
        [SerializeField] private UnityEvent<bool> m_onInteractableValueChanged;
        private bool m_canvasGroupAllowInteraction = true;
        private readonly List<CanvasGroup> m_canvasGroupCache = new List<CanvasGroup>();

        // Original 060000b0/b1: assignment is unconditional; selection clearing precedes the nullable event.
        public virtual bool Interactable
        {
            get => IsInteractable();
            set
            {
                m_interactable = value;
                if (!value && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
                m_onInteractableValueChanged?.Invoke(IsInteractable());
            }
        }

        // Original 060000b2: all groups on a transform are visited before stopping parent traversal.
        protected override void OnCanvasGroupChanged()
        {
            bool allowInteraction = true;
            Transform current = transform;
            while (current != null)
            {
                current.GetComponents(m_canvasGroupCache);
                bool stopAtThisTransform = false;
                foreach (var canvasGroup in m_canvasGroupCache)
                {
                    if (canvasGroup.enabled && !canvasGroup.interactable)
                    {
                        allowInteraction = false;
                        stopAtThisTransform = true;
                    }
                    if (canvasGroup.ignoreParentGroups) stopAtThisTransform = true;
                }
                if (stopAtThisTransform) break;
                current = current.parent;
            }
            m_canvasGroupAllowInteraction = allowInteraction;
        }

        // Original 060000b3, nonvirtual and independent of a subclass's Interactable getter.
        public bool IsInteractable() => m_canvasGroupAllowInteraction && m_interactable;
        // Original 060000b4, inherited callback first and virtual group callback second.
        protected override void OnTransformParentChanged() { base.OnTransformParentChanged(); OnCanvasGroupChanged(); }
        // Original 060000b5, the two true flags and list allocation precede the inherited constructor.
        protected UIInteractable() { }
    }
}
