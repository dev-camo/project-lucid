// Original HLUnityUI.Runtime Hardlight.UINavigation 02000078 (MethodDefs
// 0x0600037f..8d) and its nested NavMode 02000079, translated from the x86-64
// bodies. Unlike HLNavigation, the explicit-fallback and pointer navigation
// flags live in the struct, and Equals compares them after the four targets.
using System;
using UnityEngine;

namespace Hardlight
{
    [Serializable]
    public struct UINavigation : IEquatable<UINavigation>
    {
        [Flags]
        public enum NavMode
        {
            None = 0,
            Horizontal = 1,
            Vertical = 2,
            Automatic = 3,
            Explicit = 4
        }

        [SerializeField]
        private NavMode m_mode;

        [SerializeField]
        private UISelectable m_selectOnUp;

        [SerializeField]
        private UISelectable m_selectOnDown;

        [SerializeField]
        private UISelectable m_selectOnLeft;

        [SerializeField]
        private UISelectable m_selectOnRight;

        [SerializeField]
        [Tooltip("If you explicitly define navigation, but the object is off or null this will automatically find another.")]
        private bool m_autoNavigateIfExplicitSelectionIsNotValid;

        [SerializeField]
        private bool m_pointerEnterShouldTriggerNavigationEvent;

        [SerializeField]
        private bool m_pointerExitShouldTriggerNavigationEvent;

        public NavMode Mode
        {
            get { return m_mode; }
            set { m_mode = value; }
        }

        public bool AutoNavigateIfExplicitSelectionIsNotValid
        {
            get { return m_autoNavigateIfExplicitSelectionIsNotValid; }
        }

        public bool PointerEnterShouldTriggerNavigationEvent
        {
            get { return m_pointerEnterShouldTriggerNavigationEvent; }
        }

        public bool PointerExitShouldTriggerNavigationEvent
        {
            get { return m_pointerExitShouldTriggerNavigationEvent; }
        }

        public UISelectable SelectOnUp
        {
            get { return m_selectOnUp; }
            set { m_selectOnUp = value; }
        }

        public UISelectable SelectOnDown
        {
            get { return m_selectOnDown; }
            set { m_selectOnDown = value; }
        }

        public UISelectable SelectOnLeft
        {
            get { return m_selectOnLeft; }
            set { m_selectOnLeft = value; }
        }

        public UISelectable SelectOnRight
        {
            get { return m_selectOnRight; }
            set { m_selectOnRight = value; }
        }

        // Automatic with no targets and every flag clear (0x1B54230).
        public static UINavigation DefaultNavigation
        {
            get
            {
                UINavigation navigation = default(UINavigation);
                navigation.m_mode = NavMode.Automatic;
                return navigation;
            }
        }

        // The targets use UnityEngine.Object equality, so a destroyed target
        // equals null (0x1B54250).
        public bool Equals(UINavigation other)
        {
            return m_mode == other.m_mode
                && m_selectOnUp == other.m_selectOnUp
                && m_selectOnDown == other.m_selectOnDown
                && m_selectOnLeft == other.m_selectOnLeft
                && m_selectOnRight == other.m_selectOnRight
                && m_autoNavigateIfExplicitSelectionIsNotValid == other.m_autoNavigateIfExplicitSelectionIsNotValid
                && m_pointerEnterShouldTriggerNavigationEvent == other.m_pointerEnterShouldTriggerNavigationEvent
                && m_pointerExitShouldTriggerNavigationEvent == other.m_pointerExitShouldTriggerNavigationEvent;
        }
    }
}
