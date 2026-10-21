using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200097c; bodies translated from the 1.10.1 x86-64 binary.
    // A screen region that follows the touches inside it. OnScreenControlInputHandler hands touches to the
    // zones; each frame the zone turns what it tracks into held input and, for several touches, a pinch.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class OnScreenTouchZone : OnScreenInputButton
    {
        [SerializeField] protected RectTransform m_validTouchTransform;
        [Tooltip("During multi-touch functionality, pinch distance between two points will be clamped to this as a minimum")]
        [SerializeField] private float m_minimumPinchDistance = 50f;
        [SerializeField]
        [Tooltip("If any specified abilities are active on the character then ignore touch input while in use.")]
        private List<ActorAbilityType> m_touchIgnoreAbilities = new List<ActorAbilityType>();
        [SerializeField] private bool m_restrictToSingleTouch;
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();

        // 060036b8..060036be (1.10.1 RVA 0x692970, 0x690520, 0x690500, 0x692980, 0x68E230, 0x692990, 0x6929A0):
        // the pinch distances read 0 unless a pinch is in progress.
        protected bool IsPinching => m_isPinching;
        protected float InitialPinchDistance => m_isPinching ? m_initialPinchDistance : 0f;
        protected float PinchDistance => m_isPinching ? m_pinchDistance : 0f;
        protected bool PointerWasDown => m_pointerWasDown;
        public bool PointerIsDown => m_activeTouches.Count > 0;
        protected bool MultiTouchEnabled { get; set; }

        private readonly List<Touch> m_activeTouches = new List<Touch>();
        private bool m_changedTouches;
        private Vector2 m_touchPositionPrevious;
        private bool m_isPinching;
        private float m_initialPinchDistance;
        private float m_pinchDistance;
        private int m_peakTouches;
        private bool m_pointerWasDown;

        // 060036bf (RVA 0x690920)
        public virtual void Initialise()
        {
            ClearAllTouches();
        }

        // 060036c0 (RVA 0x690A30): the zone reports the empty state once, unless the input type is invalid.
        public virtual void Shutdown(bool usingValidInputType)
        {
            ClearAllTouches();
            if (usingValidInputType) SupplyTouchData();
        }

        // 060036c1 (RVA 0x690A90)
        protected virtual void OnTouchRelease()
        {
            SupplyHeldGameInput(Vector2.zero);
        }

        // 060036c2 (RVA 0x690CC0): a pinch distance of 0 or less ends the pinch; NaN counts as a pinch.
        protected virtual void ProcessTouch(Vector2 touchPosition, Vector2 touchDelta, float pinchDistance, float initialPinchDistance)
        {
            SupplyHeldGameInput(OnInput(touchPosition, touchDelta));
            if (pinchDistance <= 0f)
            {
                m_isPinching = false;
            }
            else
            {
                m_isPinching = true;
                SupplyPinchInput(pinchDistance, initialPinchDistance);
            }
        }

        // 060036c3 (RVA 0x68E7B0): the score of a position outside the zone stays at the worst value.
        public bool TouchPositionValid(Vector2 position, out float score)
        {
            score = float.MaxValue;
            bool valid = RectTransformUtility.RectangleContainsScreenPoint(m_validTouchTransform, position);
            if (valid) score = GetScoreForTouchPosition(position);
            return valid;
        }

        // 060036c4 (RVA 0x6929B0): lower scores win when zones overlap.
        protected virtual float GetScoreForTouchPosition(Vector2 position)
        {
            return 0f;
        }

        // 060036c5
        protected abstract void SupplyHeldGameInput(Vector2 resultantMovement);

        // 060036c6 (RVA 0x6929C0)
        protected virtual void SupplyPinchInput(float pinchDistance, float initialPinchDistance)
        {
        }

        // 060036c7
        protected abstract Vector2 OnInput(Vector2 touchPosition, Vector2 touchDelta);

        // 060036c8 (RVA 0x6929D0)
        public virtual bool IsActive()
        {
            return gameObject.activeInHierarchy;
        }

        // 060036c9 (RVA 0x6929F0)
        protected virtual bool IsSingleTouch()
        {
            return m_restrictToSingleTouch;
        }

        // 060036ca (RVA 0x68E740)
        public bool CanAcceptNewTouch()
        {
            return IsActive() && (m_activeTouches.Count <= 0 || MultiTouchEnabled);
        }

        // 060036cb (RVA 0x692A00): turning multi-touch off drops every tracked touch.
        public void SetMultiTouchEnabled(bool enable)
        {
            if (MultiTouchEnabled == enable) return;
            MultiTouchEnabled = enable;
            if (!enable) ClearAllTouches();
        }

        // 060036cc (RVA 0x68E1E0)
        public void ClearAllTouches()
        {
            m_activeTouches.Clear();
            m_changedTouches = true;
            m_peakTouches = 0;
        }

        // 060036cd (RVA 0x68E270)
        public bool IsTrackingTouch(Touch touch)
        {
            return TryGetTouchIndex(touch, out int _);
        }

        // 060036ce (RVA 0x68E3B0)
        public void StopTrackingTouch(Touch touch)
        {
            if (TryGetTouchIndex(touch, out int index))
            {
                m_activeTouches.RemoveAt(index);
                m_changedTouches = true;
            }
            if (m_activeTouches.Count == 0) m_peakTouches = 0;
        }

        // 060036cf (RVA 0x68E850): a zone for several touches only takes one while it is still at its peak.
        public void StartTrackingTouch(Touch touch)
        {
            if (IsSingleTouch() || m_peakTouches == m_activeTouches.Count)
            {
                m_activeTouches.Add(touch);
                m_changedTouches = true;
                m_peakTouches = Mathf.Max(m_peakTouches, m_activeTouches.Count);
            }
        }

        // 060036d0 (RVA 0x68E550)
        public void UpdateTouch(Touch touch)
        {
            if (IsSingleTouch() || m_peakTouches == m_activeTouches.Count)
            {
                if (TryGetTouchIndex(touch, out int index)) m_activeTouches[index] = touch;
            }
        }

        // 060036d1 (RVA 0x692A60): index ends at the touch count when the finger is not tracked.
        private bool TryGetTouchIndex(Touch touch, out int index)
        {
            for (index = 0; index < m_activeTouches.Count; index++)
            {
                if (m_activeTouches[index].fingerId == touch.fingerId) return true;
            }
            return false;
        }

        // 060036d2 (RVA 0x691640): the touch is valid unless a listed ability is in use on the current character.
        protected bool TouchValidForCharacter(List<ActorAbilityType> ignoreAbilities)
        {
            bool valid = true;
            if (ignoreAbilities.Count != 0 &&
                m_characterManagerRef.TryGet(out CharacterManager characterManager) &&
                characterManager.TryGetCurrentCharacter(out Character character))
            {
                valid = !ActorAbilityUtilities.AnyAbilityInUse(character, ignoreAbilities, 0.2f);
            }
            return valid;
        }

        // 060036d3 (RVA 0x68E9B0): with several touches the zone works from the bounds of all of them; their
        // centre is the position and the diagonal of the bounds the pinch distance.
        public void SupplyTouchData()
        {
            bool pointerIsDown = PointerIsDown && TouchValidForCharacter(m_touchIgnoreAbilities);
            if (IsSingleTouch())
            {
                SupplySingleTouch(pointerIsDown);
                return;
            }
            if (pointerIsDown)
            {
                if (m_peakTouches != m_activeTouches.Count)
                {
                    // A touch has been lost since the peak: hold the last position with no movement.
                    ProcessTouch(m_touchPositionPrevious, Vector2.zero, 0f, 0f);
                }
                else
                {
                    Rect bounds = new Rect(m_activeTouches[0].position, Vector2.zero);
                    for (int i = 1; i < m_activeTouches.Count; i++)
                    {
                        Touch touch = m_activeTouches[i];
                        bounds.xMin = Mathf.Min(bounds.xMin, touch.position.x);
                        bounds.xMax = Mathf.Max(bounds.xMax, touch.position.x);
                        bounds.yMin = Mathf.Min(bounds.yMin, touch.position.y);
                        bounds.yMax = Mathf.Max(bounds.yMax, touch.position.y);
                    }
                    Vector2 center = bounds.center;
                    float pinchDistance = 0f;
                    if (m_activeTouches.Count >= 2) pinchDistance = Mathf.Max(m_minimumPinchDistance, bounds.size.magnitude);
                    m_pinchDistance = pinchDistance;
                    Vector2 delta;
                    if (m_changedTouches)
                    {
                        delta = Vector2.zero;
                        m_initialPinchDistance = pinchDistance;
                    }
                    else
                    {
                        delta = center - m_touchPositionPrevious;
                    }
                    m_touchPositionPrevious = center;
                    ProcessTouch(center, delta, pinchDistance, m_initialPinchDistance);
                }
            }
            else if (m_pointerWasDown)
            {
                OnTouchRelease();
            }
            m_pointerWasDown = pointerIsDown;
            m_changedTouches = false;
        }

        // 060036d4 (RVA 0x692B70)
        private void SupplySingleTouch(bool pointerIsDown)
        {
            if (pointerIsDown)
            {
                Vector2 position = m_activeTouches[0].position;
                m_pinchDistance = 0f;
                Vector2 delta = position - m_touchPositionPrevious;
                m_touchPositionPrevious = position;
                ProcessTouch(position, delta, 0f, m_initialPinchDistance);
            }
            else if (m_pointerWasDown)
            {
                OnTouchRelease();
            }
            m_pointerWasDown = pointerIsDown;
            m_changedTouches = false;
        }

        // 060036d5 (RVA 0x6910E0)
        protected OnScreenTouchZone()
        {
        }
    }
}
