using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SourceMouse : IInputTouchSource, IInputPointerSource
    {
        private const int NumberOfMouseButtonTouches = 2;
        private const int MouseButton0Touch = 0;
        private const int MouseButton1Touch = 1;
        private Touch[] m_trackedMouseTouches;
        private List<Touch> m_currentActiveTouches;

        // Original06000299 allocates only after Object base construction.
        public SourceMouse()
        {
            m_trackedMouseTouches = new Touch[NumberOfMouseButtonTouches];
            m_currentActiveTouches = new List<Touch>(NumberOfMouseButtonTouches);
            m_trackedMouseTouches[MouseButton0Touch].fingerId = 1;
            m_trackedMouseTouches[MouseButton0Touch].pressure = 1f;
            m_trackedMouseTouches[MouseButton1Touch].fingerId = 2;
            m_trackedMouseTouches[MouseButton1Touch].pressure = 1f;
        }

        public int GetTouchCount() => m_currentActiveTouches.Count; // Original0600029a.
        public Touch GetTouch(int touchIndex) // Original0600029b: signed upper check only; negative index still faults in List.
        {
            if (touchIndex < m_currentActiveTouches.Count)
                return m_currentActiveTouches[touchIndex];
            Touch touch = new Touch();
            touch.fingerId = unchecked(touchIndex + 1);
            touch.phase = TouchPhase.Began;
            touch.pressure = 1f;
            touch.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            touch.deltaPosition = Vector2.zero;
            return touch;
        }
        public Vector3 GetTilt() // Original0600029c: no zero-width guard or clamp.
        {
            float halfWidth = Screen.width * 0.5f;
            return new Vector3((Input.mousePosition.x - halfWidth) / halfWidth, 0f, 0f);
        }
        public Vector3 GetPosition() => Input.mousePosition; // Original0600029d.
        public float GetScrollWheelDelta() => Input.mouseScrollDelta.y; // Original0600029e.
        public void Update() { UpdateTouchIntenal(MouseButton0Touch); UpdateTouchIntenal(MouseButton1Touch); } // Original0600029f, true spelling.

        private void UpdateTouchIntenal(int index) // Original060002a0, exact original typo.
        {
            Touch touch = m_trackedMouseTouches[index];
            bool pressed = Input.GetMouseButton(index);
            Vector2 previousPosition = touch.position;
            Vector2 position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            Vector2 deltaPosition = position - previousPosition;
            TouchPhase phase = TouchPhase.Began;
            bool add = false;
            bool remove = false;
            switch (touch.phase)
            {
                case TouchPhase.Began:
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    // Both native branches choose Stationary for unordered squared magnitude.
                    phase = pressed ? (deltaPosition.sqrMagnitude >= float.Epsilon ? TouchPhase.Moved : TouchPhase.Stationary)
                        : TouchPhase.Ended;
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    bool tracked = TouchIsTracked(touch.fingerId);
                    add = pressed && !tracked;
                    remove = !pressed && tracked;
                    phase = pressed ? TouchPhase.Began : touch.phase;
                    deltaPosition = Vector2.zero;
                    break;
            }
            touch.position = position;
            touch.deltaPosition = deltaPosition;
            touch.phase = phase;
            m_trackedMouseTouches[index] = touch;
            if (add)
                m_currentActiveTouches.Add(touch);
            else if (remove)
                RemoveTrackedTouch(touch.fingerId);
            else
                UpdateTrackedTouch(touch);
        }

        private bool TouchIsTracked(int fingerID) // Original060002a1: first ascending match and live Count.
        {
            for (int i = 0; i < m_currentActiveTouches.Count; i++)
                if (m_currentActiveTouches[i].fingerId == fingerID) return true;
            return false;
        }
        private void RemoveTrackedTouch(int fingerID) // Original060002a2 removes only last matching entry.
        {
            for (int i = m_currentActiveTouches.Count - 1; i >= 0; i--)
                if (m_currentActiveTouches[i].fingerId == fingerID)
                {
                    m_currentActiveTouches.RemoveAt(i);
                    return;
                }
        }
        private void UpdateTrackedTouch(Touch touch) // Original060002a3 updates only first matching entry.
        {
            for (int i = 0; i < m_currentActiveTouches.Count; i++)
                if (touch.fingerId == m_currentActiveTouches[i].fingerId)
                {
                    m_currentActiveTouches[i] = touch;
                    return;
                }
        }
    }
}
