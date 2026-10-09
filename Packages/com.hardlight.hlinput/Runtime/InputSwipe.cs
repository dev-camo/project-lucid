using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // HLInput.Runtime 0x0200006b: all sixteen owner and four SwipeData native
    // methods, recovered from both shipped Mac architectures. This candidate
    // retains the real stripped-binding constructor and engine-call frontiers.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class InputSwipe
    {
        public enum Direction { None = 0, Down = 1, Up = 2, Left = 3, Right = 4 }
        public delegate void OnSwipeHandler(int joystickIndex, GameInput gameInput, float value);
        public enum State { Null = 0, Idle = 1, Tracking = 2, WaitingForNoTouch = 3 }

        // Original nested 0x0200006f, including its ordered IL2CPP options.
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class SwipeData
        {
            public Vector2 m_position;
            public Vector2 m_delta;
            public State m_currentState;
            public State m_nextState;
            public float m_stateTimer;
            public Vector2 m_swipeStartPosition;
            public Vector2 m_swipeStartPositionDelta;
            public float m_swipeTime;
            public Direction m_lastSwipeDirection;
            public Vector3 m_lastSwipeLength;
            public bool m_swipeLeft;
            public bool m_swipeRight;
            public bool m_swipeUp;
            public bool m_swipeDown;
            public TouchPhase m_phase;

            // 0x06000224..0x06000227: ResetGesture deliberately leaves flags,
            // last direction and last length intact and writes Canceled.
            public bool SwipeDetected() => m_swipeLeft || m_swipeRight || m_swipeUp || m_swipeDown;
            public void ResetFlags()
            {
                m_swipeLeft = false;
                m_swipeRight = false;
                m_swipeUp = false;
                m_swipeDown = false;
            }
            public void ResetGesture()
            {
                m_swipeStartPosition = m_position;
                m_swipeStartPositionDelta = m_delta;
                m_swipeTime = 0f;
                m_phase = TouchPhase.Canceled;
            }
            public SwipeData() { }
        }

        public event OnSwipeHandler SwipeHandlers; // 0x06000212/0x06000213
        [SerializeField] private float m_gestureActivationTouchScreenLengthInMM = 10f;
        [SerializeField] private float m_gestureActivationTouchPadLengthInMM = 15f;
        [Range(0f, 1f), SerializeField] private float m_verticalGestureActivationDecimalOfHorizontalLength = 0.8f;
        [SerializeField, Range(0f, 1f)] private float m_gestureActivationScreenPercentage = 0.1f;
        [SerializeField] private float m_gestureTimeoutDefault = 1f;
        [SerializeField] private float m_gestureTimeoutTvOS = 0.25f;
        [SerializeField] private bool m_dontSwapTouches;
        [Tooltip("Whether swipes wait for input to end or are evaluated during movement."), SerializeField]
        private bool m_waitForRelease = true;
        private float m_gestureTimeout;
        private int m_fingerIdForCurrentSwipe = -1;
        private Dictionary<int, SwipeData> m_swipeData = new Dictionary<int, SwipeData>();
        private Dictionary<int, SwipeData> m_swipeDataPrev = new Dictionary<int, SwipeData>();
        private float m_gestureActivationLengthInPixelsHorizontal;
        private float m_gestureActivationLengthInPixelsVertical;
        private IInputTouchSource m_inputTouchSource;
        private IReadOnlyList<GameInputBinding> m_gameInputBindings;

        // 0x06000214: the stores and glyph callback work precede timeout/reset.
        public void Initialise(ref Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>> gameInputGlyphMap,
            Func<Direction, InputType, int, Texture2D> getGlyphForKeyAndInputType,
            IInputTouchSource inputTouchSource, IReadOnlyList<GameInputBinding> gameInputBindings)
        {
            m_inputTouchSource = inputTouchSource;
            m_gameInputBindings = gameInputBindings;
            PopulateToGlyphMap(ref gameInputGlyphMap, getGlyphForKeyAndInputType, m_gameInputBindings);
            m_gestureTimeout = m_gestureTimeoutDefault;
            Reset();
            SetSwipeDistances();
        }

        // 0x06000215: handlers, dictionaries and selected finger survive shutdown.
        public void Shutdown()
        {
            m_inputTouchSource = null;
            m_gameInputBindings = null;
        }

        // 0x06000216: the finger store precedes both potentially faulting clears.
        public void Reset()
        {
            m_fingerIdForCurrentSwipe = -1;
            m_swipeData.Clear();
            m_swipeDataPrev.Clear();
        }

        // 0x06000217: both axes share the screen distance on the shipped Mac
        // paths. The touch-pad/vertical-ratio fields are not read on these paths.
        private void SetSwipeDistances()
        {
            float dpi = ScreenExtensions.DPI();
            m_gestureActivationLengthInPixelsHorizontal =
                dpi * (m_gestureActivationTouchScreenLengthInMM * (1f / 25.4f));
            m_gestureActivationLengthInPixelsVertical = m_gestureActivationLengthInPixelsHorizontal;
            if (m_gestureActivationLengthInPixelsHorizontal < 1f)
            {
                int width = Screen.width;
                m_gestureActivationLengthInPixelsHorizontal = width * m_gestureActivationScreenPercentage;
                m_gestureActivationLengthInPixelsVertical = m_gestureActivationLengthInPixelsHorizontal;
            }
        }

        // 0x06000218: both real Dictionary enumerators retain disposal/fault
        // behavior. Every tracked touch is handled; selected finger is not a gate.
        public void Update()
        {
            foreach (KeyValuePair<int, SwipeData> pair in m_swipeData)
            {
                SwipeData data = pair.Value;
                data.ResetFlags();
                if (data.m_currentState != data.m_nextState)
                {
                    data.m_currentState = data.m_nextState;
                    data.m_stateTimer = 0f;
                }
                else
                    data.m_stateTimer += Time.deltaTime;
            }
            ReadRawInput();
            foreach (KeyValuePair<int, SwipeData> pair in m_swipeData)
            {
                SwipeData swipeData = pair.Value;
                switch (swipeData.m_currentState)
                {
                    case State.Idle:
                        swipeData.m_nextState = HandleIdleState(ref swipeData);
                        break;
                    case State.Tracking:
                    case State.WaitingForNoTouch:
                        swipeData.m_nextState = HandleActiveState(ref swipeData);
                        break;
                }
            }
        }

        // 0x06000219: touch count is sampled once. Providers are reread on each
        // query, dictionaries are swapped, and duplicate finger ids still fault.
        private void ReadRawInput()
        {
            int touchCount = m_inputTouchSource.GetTouchCount();
            if (touchCount <= 0)
            {
                m_swipeData.Clear();
                m_swipeDataPrev.Clear();
                return;
            }
            Dictionary<int, SwipeData> previous = m_swipeDataPrev;
            m_swipeDataPrev = m_swipeData;
            m_swipeData = previous;
            m_swipeData.Clear();
            for (int i = 0; i < touchCount; ++i)
            {
                Touch touch = m_inputTouchSource.GetTouch(i);
                if (!m_swipeDataPrev.TryGetValue(touch.fingerId, out SwipeData swipeData))
                {
                    swipeData = new SwipeData();
                    swipeData.m_currentState = State.Idle;
                    swipeData.m_nextState = State.Idle;
                    swipeData.m_stateTimer = 0f;
                    swipeData.m_lastSwipeDirection = Direction.None;
                    swipeData.ResetFlags();
                }
                swipeData.m_position = touch.position;
                swipeData.m_delta = touch.deltaPosition;
                swipeData.m_phase = touch.phase;
                m_swipeData.Add(touch.fingerId, swipeData);
            }
            int touchIndex = -1;
            if (m_dontSwapTouches)
            {
                for (int i = 0; i < touchCount; ++i)
                {
                    Touch touch = m_inputTouchSource.GetTouch(i);
                    if (touch.fingerId == m_fingerIdForCurrentSwipe)
                    {
                        touchIndex = i;
                        break;
                    }
                }
            }
            if (touchIndex < 0)
            {
                float greatestLength = 0f;
                for (int i = 0; i < touchCount; ++i)
                {
                    Touch touch = m_inputTouchSource.GetTouch(i);
                    float length = touch.deltaPosition.sqrMagnitude;
                    if (length > greatestLength || touchIndex < 0)
                    {
                        touchIndex = i;
                        greatestLength = length;
                    }
                }
            }
            Touch selected = m_inputTouchSource.GetTouch(touchIndex);
            if (selected.fingerId != m_fingerIdForCurrentSwipe)
                m_fingerIdForCurrentSwipe = selected.fingerId;
        }

        // 0x0600021a: genuine nested helper, not a rewritten state initializer.
        private void BeginGesture(ref SwipeData swipeData) => swipeData.ResetGesture();

        // 0x0600021b: strict ordered comparisons give horizontal ties priority.
        // An expired gesture stores its length but does not increment its timer.
        private void UpdateGestures(ref SwipeData swipeData)
        {
            Vector2 gestureLength = swipeData.m_swipeStartPositionDelta +
                (swipeData.m_position - swipeData.m_swipeStartPosition);
            if (swipeData.m_swipeTime > m_gestureTimeout)
            {
                swipeData.m_lastSwipeDirection = Direction.None;
                swipeData.m_lastSwipeLength = gestureLength;
                return;
            }
            float longestLength = 0f;
            Direction gestureDirection = Direction.None;
            if (gestureLength.x > longestLength)
            {
                longestLength = gestureLength.x;
                gestureDirection = Direction.Right;
            }
            if (gestureLength.x < -longestLength)
            {
                longestLength = -gestureLength.x;
                gestureDirection = Direction.Left;
            }
            if (gestureLength.y > longestLength)
            {
                longestLength = gestureLength.y;
                gestureDirection = Direction.Up;
            }
            if (gestureLength.y < -longestLength)
            {
                longestLength = -gestureLength.y;
                gestureDirection = Direction.Down;
            }
            if (longestLength > Mathf.Min(m_gestureActivationLengthInPixelsHorizontal,
                    m_gestureActivationLengthInPixelsVertical) &&
                swipeData.m_phase == (m_waitForRelease ? TouchPhase.Ended : TouchPhase.Moved) &&
                gestureDirection != Direction.None && swipeData.m_lastSwipeDirection == Direction.None)
            {
                for (int i = 0; i < m_gameInputBindings.Count; ++i)
                {
                    GameInputBinding binding = m_gameInputBindings[i];
                    IReadOnlyList<BindingData> bindingData = binding.BindingData;
                    for (int j = 0; j < bindingData.Count; ++j)
                    {
                        BindingData data = bindingData[j];
                        IReadOnlyList<SwipeBinding> swipeBindings = data.SwipeBindings;
                        for (int k = 0; k < swipeBindings.Count; ++k)
                        {
                            SwipeBinding swipeBinding = swipeBindings[k];
                            ProcessSwipeDirection(swipeData, gestureDirection, longestLength,
                                data.GameInput, swipeBinding, binding.JoystickIndex);
                        }
                    }
                }
            }
            swipeData.m_swipeTime += Time.deltaTime;
            if (swipeData.SwipeDetected())
            {
                swipeData.m_lastSwipeDirection = gestureDirection;
                swipeData.m_lastSwipeLength = gestureLength;
            }
        }

        // 0x0600021c: direction flags precede modifiers and the unguarded event
        // invocation. A zero/suppressed value leaves the corresponding flag set.
        private void ProcessSwipeDirection(SwipeData swipeData, Direction gestureDirection,
            float longestLength, GameInput gameInput, SwipeBinding swipeBinding, int joystickIndex)
        {
            if (swipeBinding.Direction != gestureDirection ||
                !swipeBinding.ValidSwipePosition(swipeData.m_swipeStartPosition))
                return;
            float value = 1f;
            switch (gestureDirection)
            {
                case Direction.Down:
                    if (!(longestLength > m_gestureActivationLengthInPixelsVertical)) return;
                    swipeData.m_swipeDown = true;
                    break;
                case Direction.Up:
                    if (!(longestLength > m_gestureActivationLengthInPixelsVertical)) return;
                    swipeData.m_swipeUp = true;
                    break;
                case Direction.Left:
                    if (!(longestLength > m_gestureActivationLengthInPixelsHorizontal)) return;
                    swipeData.m_swipeLeft = true;
                    break;
                case Direction.Right:
                    if (!(longestLength > m_gestureActivationLengthInPixelsHorizontal)) return;
                    swipeData.m_swipeRight = true;
                    break;
                default:
                    return;
            }
            ApplyModifiers(swipeBinding, ref value, gameInput);
            if (!MathUtilities.WithinTolerance(value, 0f, 0.0001f))
                SwipeHandlers(joystickIndex, gameInput, value);
        }

        // 0x0600021d: captured modifier list, live Count/index, no null skip.
        private void ApplyModifiers(SwipeBinding swipeBinding, ref float value, GameInput gameInput)
        {
            IReadOnlyList<InputModifier> modifiers = swipeBinding.Modifiers;
            for (int i = 0; i < modifiers.Count; ++i)
                value = modifiers[i].Modify(value, Time.deltaTime, gameInput);
        }

        // 0x0600021e/0x0600021f: ResetGesture writes Canceled, so a new idle
        // gesture does not immediately use the raw touch's old phase.
        private State HandleIdleState(ref SwipeData swipeData)
        {
            BeginGesture(ref swipeData);
            UpdateGestures(ref swipeData);
            return swipeData.SwipeDetected() ? State.WaitingForNoTouch : State.Tracking;
        }
        private State HandleActiveState(ref SwipeData swipeData)
        {
            UpdateGestures(ref swipeData);
            if (swipeData.m_phase == TouchPhase.Ended)
            {
                BeginGesture(ref swipeData);
                swipeData.ResetFlags();
                swipeData.m_lastSwipeDirection = Direction.None;
                return State.Idle;
            }
            if (swipeData.SwipeDetected() || swipeData.m_swipeTime > m_gestureTimeout)
                return State.WaitingForNoTouch;
            return swipeData.m_currentState;
        }

        // 0x06000220: glyph callbacks precede map reads. The map ref and its
        // nested entry are reread after callbacks and on each subsequent insert.
        private void PopulateToGlyphMap(ref Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>> gameInputGlyphMap,
            Func<Direction, InputType, int, Texture2D> getGlyphForKeyAndInputType,
            IReadOnlyList<GameInputBinding> gameInputBindings)
        {
            for (int i = 0; i < gameInputBindings.Count; ++i)
            {
                GameInputBinding binding = gameInputBindings[i];
                InputType inputType = binding.BindingInputType;
                IReadOnlyList<BindingData> bindingData = binding.BindingData;
                int joystickIndex = binding.JoystickIndex;
                for (int j = 0; j < bindingData.Count; ++j)
                {
                    BindingData data = bindingData[j];
                    GameInput gameInput = data.GameInput;
                    IReadOnlyList<SwipeBinding> swipeBindings = data.SwipeBindings;
                    for (int k = 0; k < swipeBindings.Count; ++k)
                    {
                        Texture2D glyph = getGlyphForKeyAndInputType(swipeBindings[k].Direction, inputType, joystickIndex);
                        if (!gameInputGlyphMap.TryGetValue(gameInput, out Dictionary<InputType, List<Texture2D>> map))
                        {
                            map = new Dictionary<InputType, List<Texture2D>>(HardlightInputEnumComparers.InputTypeComparer);
                            map.Add(inputType, new List<Texture2D>());
                            gameInputGlyphMap.Add(gameInput, map);
                        }
                        if (!gameInputGlyphMap[gameInput].TryGetValue(inputType, out List<Texture2D> glyphs))
                        {
                            glyphs = new List<Texture2D>();
                            gameInputGlyphMap[gameInput].Add(inputType, glyphs);
                        }
                        glyphs.Add(glyph);
                    }
                }
            }
        }

        // 0x06000221: nonzero authored defaults and both dictionaries are field
        // initializers in original store order, followed only by Object..ctor.
        public InputSwipe() { }
    }
}
