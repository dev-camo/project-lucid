using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // HLInput.Runtime 0x02000071: all twenty-two owner native methods and
    // TapDetectionData's original constructor, from both shipped architectures.
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class InputTouch
    {
        public delegate void OnTouchHandler(GameInput gameInput, Vector2 point);
        public delegate void OnTouchReleaseHandler(GameInput gameInput, Vector2 point);
        public delegate void OnMultiTouchHandler(GameInput gameInput, List<Vector2> point);
        public delegate void OnMultiTouchReleaseHandler(GameInput gameInput, Vector2 point);
        public delegate void OnTapHandler(GameInput gameInput, Vector2 point);
        public delegate void OnDoubleTapHandler(GameInput gameInput, Vector2 deltaBetweenTaps);
        public delegate void OnHoldHandler(GameInput gameInput);
        public delegate void OnHoldPressureHandler(GameInput gameInput, float pressure);

        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class TapDetectionData // 0x0200007a
        {
            public Vector2 m_fingerDownPoint;
            public float m_fingerDownTime;
            public int m_fingerID;
            public bool m_tapInvalid;
            public bool m_tapFinished;
            public bool m_tapHold;
            public float m_tapPressure;
            public TapDetectionData() { } // 0x06000254: only Object..ctor
        }

        // Original 0x0600022e..0x06000237 event add/remove pairs.
        public event OnTouchHandler TouchHandlers;
        public event OnTouchReleaseHandler TouchReleaseHandlers;
        public event OnMultiTouchHandler MultiTouchHandlers;
        public event OnMultiTouchReleaseHandler MultiTouchReleaseHandlers;
        public event OnTapHandler TapHandlers;
        // These three exact CompilerGenerated private fields remain in shipped
        // metadata, but their event accessors have no original declarations or
        // native bodies. Event-record/source emission remains an explicit gap.
        [CompilerGenerated] private OnDoubleTapHandler DoubleTapHandlers;
        [CompilerGenerated] private OnHoldHandler HoldHandlers;
        [CompilerGenerated] private OnHoldPressureHandler HoldPressureHandlers;
        public Vector2 TapPosition { get; private set; } // 0x06000238/0x06000239
        public Vector2 PreviousTapPosition { get; private set; } // 0x0600023a/0x0600023b
        [SerializeField] private float m_doubleTapTimeLimit = 0.5f;
        [SerializeField] private float m_gestureActivationLengthInMM = 10f;
        private Vector2 m_lastTouchPosition;
        private const int MaxTapTouchesToProcess = 5;
        private List<TapDetectionData> m_tapData = new List<TapDetectionData>(5);
        private bool m_tap;
        private bool m_doubleTap;
        private float m_doubleTapTimer;
        private bool m_hold;
        private float m_tapDeactivationLengthInPixels;
        private float m_tapDeactivationTimeInSeconds = 0.25f;
        private IInputTouchSource m_inputTouchSource;
        private IReadOnlyList<GameInputBinding> m_gameInputBindings;
        private List<Vector2> m_touchPositions = new List<Vector2>(5);

        public int TouchCount => m_touchPositions.Count; // 0x0600023c

        // 0x0600023d: glyph callbacks run after provider stores and before DPI.
        public void Initialise(ref Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>> gameInputGlyphMap,
            Func<FixedBindingsGlyphMap.FixedBindingsData, InputType, int, Texture2D> getGlyphForKeyAndInputType,
            IInputTouchSource inputTouchSource, IReadOnlyList<GameInputBinding> gameInputBindings)
        {
            m_inputTouchSource = inputTouchSource;
            m_gameInputBindings = gameInputBindings;
            PopulateToGlyphMap(ref gameInputGlyphMap, getGlyphForKeyAndInputType, gameInputBindings);
            float dpi = ScreenExtensions.DPI();
            m_tapDeactivationLengthInPixels =
                (dpi * (m_gestureActivationLengthInMM * (1f / 25.4f))) * 0.9f;
        }

        // 0x0600023e: timers, records, positions and handlers are retained.
        public void Shutdown()
        {
            m_inputTouchSource = null;
            m_gameInputBindings = null;
        }

        // 0x0600023f: records are finished before raw sampling, so disappeared
        // fingers can produce taps. Only the first finished valid tap and first
        // held record produce their corresponding callbacks in an Update.
        private void HandleTapDetection()
        {
            m_doubleTapTimer -= Time.deltaTime;
            for (int i = 0; i < m_tapData.Count; ++i)
                m_tapData[i].m_tapFinished = true;
            int touchCount = m_inputTouchSource.GetTouchCount();
            for (int i = 0; i < touchCount; ++i)
                ProcessTapForTouch(m_inputTouchSource.GetTouch(i));

            int tapIndex = 0;
            while (tapIndex < m_tapData.Count)
            {
                TapDetectionData tapData = m_tapData[tapIndex];
                if (tapData.m_tapFinished)
                {
                    if (!m_tap && !tapData.m_tapInvalid)
                    {
                        m_tap = true;
                        PreviousTapPosition = TapPosition;
                        TapPosition = tapData.m_fingerDownPoint;
                        m_doubleTap = m_doubleTapTimer > 0f;
                        if (!m_doubleTap) m_doubleTapTimer = m_doubleTapTimeLimit;
                        Vector2 deltaBetweenTaps = TapPosition - PreviousTapPosition;
                        for (int i = 0; i < m_gameInputBindings.Count; ++i)
                        {
                            IReadOnlyList<BindingData> bindingData = m_gameInputBindings[i].BindingData;
                            for (int j = 0; j < bindingData.Count; ++j)
                            {
                                BindingData data = bindingData[j];
                                if (data == null) continue;
                                if (data.FixedBindings.BindToTap && TapHandlers != null)
                                    TapHandlers(data.GameInput, TapPosition);
                                if (m_doubleTap && data.FixedBindings.BindToDoubleTap && DoubleTapHandlers != null)
                                {
                                    HLOutput.LogError("Double Taps support not fully implemented!");
                                    DoubleTapHandlers(data.GameInput, deltaBetweenTaps);
                                }
                            }
                        }
                    }
                    m_tapData.RemoveAt(tapIndex);
                }
                else
                {
                    if (!m_hold && tapData.m_tapHold)
                    {
                        m_hold = true;
                        for (int i = 0; i < m_gameInputBindings.Count; ++i)
                        {
                            IReadOnlyList<BindingData> bindingData = m_gameInputBindings[i].BindingData;
                            for (int j = 0; j < bindingData.Count; ++j)
                            {
                                BindingData data = bindingData[j];
                                if (data == null) continue;
                                if (data.FixedBindings.BindToHold && HoldHandlers != null)
                                {
                                    HLOutput.LogError("Hold support not fully implemented!");
                                    HoldHandlers(data.GameInput);
                                }
                                if (data.FixedBindings.BindToHoldPressure && HoldPressureHandlers != null)
                                {
                                    HLOutput.LogError("Hold pressure support not fully implemented!");
                                    HoldPressureHandlers(data.GameInput, tapData.m_tapPressure);
                                }
                            }
                        }
                    }
                    ++tapIndex;
                }
            }

            // Raw source count is resampled after tap/hold callbacks. The old
            // position count and no-active-touch decision are then captured.
            touchCount = m_inputTouchSource.GetTouchCount();
            int previousTouchCount = m_touchPositions.Count;
            m_touchPositions.Clear();
            if (touchCount > 0)
            {
                int count = Mathf.Min(touchCount, MaxTapTouchesToProcess);
                for (int i = 0; i < count; ++i)
                {
                    Touch touch = m_inputTouchSource.GetTouch(i);
                    if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
                        m_touchPositions.Add(touch.position);
                }
            }
            bool noActiveTouch = m_touchPositions.Count < 1;
            if (!noActiveTouch) m_lastTouchPosition = m_touchPositions[0];
            for (int i = 0; i < m_gameInputBindings.Count; ++i)
            {
                IReadOnlyList<BindingData> bindingData = m_gameInputBindings[i].BindingData;
                for (int j = 0; j < bindingData.Count; ++j)
                {
                    BindingData data = bindingData[j];
                    if (data == null) continue;
                    if (noActiveTouch)
                    {
                        if (previousTouchCount <= 0) continue;
                        if (data.FixedBindings.BindToTouchRelease && TouchReleaseHandlers != null)
                            TouchReleaseHandlers(data.GameInput, m_lastTouchPosition);
                        if (data.FixedBindings.BindToMultiTouchRelease && MultiTouchReleaseHandlers != null)
                            MultiTouchReleaseHandlers(data.GameInput, m_lastTouchPosition);
                    }
                    else
                    {
                        if (data.FixedBindings.BindToTouch && TouchHandlers != null)
                            TouchHandlers(data.GameInput, m_lastTouchPosition);
                        if (data.FixedBindings.BindToMultiTouch && MultiTouchHandlers != null)
                            MultiTouchHandlers(data.GameInput, m_touchPositions);
                    }
                }
            }
        }

        // 0x06000240: new records return immediately, even for ended/canceled
        // raw touches. Existing canceled touches take the ordinary active path.
        private void ProcessTapForTouch(Touch touch)
        {
            TapDetectionData tapData = null;
            for (int i = 0; i < m_tapData.Count; ++i)
            {
                TapDetectionData current = m_tapData[i];
                if (current.m_fingerID == touch.fingerId)
                {
                    tapData = current;
                    break;
                }
            }
            if (tapData == null)
            {
                if (m_tapData.Count < MaxTapTouchesToProcess)
                {
                    tapData = new TapDetectionData();
                    tapData.m_fingerDownPoint = touch.position;
                    tapData.m_fingerDownTime = 0f;
                    tapData.m_fingerID = touch.fingerId;
                    tapData.m_tapInvalid = false;
                    tapData.m_tapFinished = false;
                    tapData.m_tapHold = false;
                    tapData.m_tapPressure = 1f;
                    m_tapData.Add(tapData);
                }
                return;
            }
            if (touch.phase == TouchPhase.Ended)
            {
                tapData.m_tapFinished = true;
                return;
            }
            tapData.m_tapFinished = false;
            tapData.m_fingerDownTime += Time.deltaTime;
            if (!tapData.m_tapInvalid)
            {
                Vector2 point = touch.position;
                Vector2 start = tapData.m_fingerDownPoint;
                float time = tapData.m_fingerDownTime;
                if ((point - start).magnitude > m_tapDeactivationLengthInPixels)
                    tapData.m_tapInvalid = true;
                if (time > m_tapDeactivationTimeInSeconds)
                {
                    tapData.m_tapHold = true;
                    tapData.m_tapPressure = touch.pressure;
                }
            }
        }

        // 0x06000241: flags clear before any timer/source work or its faults.
        public void Update()
        {
            m_tap = false;
            m_doubleTap = false;
            m_hold = false;
            HandleTapDetection();
        }

        // 0x06000242: copies all ten fixed flags, including pointer and scroll;
        // glyph callback precedes all map reads, and each map insert rereads ref.
        private void PopulateToGlyphMap(ref Dictionary<GameInput, Dictionary<InputType, List<Texture2D>>> gameInputMap,
            Func<FixedBindingsGlyphMap.FixedBindingsData, InputType, int, Texture2D> getGlyphForKeyAndInputType,
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
                    FixedBindings fixedBindings = data.FixedBindings;
                    if (!fixedBindings.BindToPointer && !fixedBindings.BindToScrollWheel &&
                        !fixedBindings.BindToTap && !fixedBindings.BindToDoubleTap &&
                        !fixedBindings.BindToTouch && !fixedBindings.BindToTouchRelease &&
                        !fixedBindings.BindToMultiTouch && !fixedBindings.BindToMultiTouchRelease &&
                        !fixedBindings.BindToHold && !fixedBindings.BindToHoldPressure) continue;
                    FixedBindingsGlyphMap.FixedBindingsData fixedData = new FixedBindingsGlyphMap.FixedBindingsData
                    {
                        BindToPointer = fixedBindings.BindToPointer,
                        BindToScrollWheel = fixedBindings.BindToScrollWheel,
                        BindToTap = fixedBindings.BindToTap,
                        BindToDoubleTap = fixedBindings.BindToDoubleTap,
                        BindToTouch = fixedBindings.BindToTouch,
                        BindToTouchRelease = fixedBindings.BindToTouchRelease,
                        BindToMultiTouch = fixedBindings.BindToMultiTouch,
                        BindToMultiTouchRelease = fixedBindings.BindToMultiTouchRelease,
                        BindToHold = fixedBindings.BindToHold,
                        BindToHoldPressure = fixedBindings.BindToHoldPressure
                    };
                    Texture2D glyph = getGlyphForKeyAndInputType(fixedData, inputType, joystickIndex);
                    if (!gameInputMap.TryGetValue(gameInput, out Dictionary<InputType, List<Texture2D>> map))
                    {
                        map = new Dictionary<InputType, List<Texture2D>>(HardlightInputEnumComparers.InputTypeComparer);
                        map.Add(inputType, new List<Texture2D>());
                        gameInputMap.Add(gameInput, map);
                    }
                    if (!gameInputMap[gameInput].TryGetValue(inputType, out List<Texture2D> glyphs))
                    {
                        glyphs = new List<Texture2D>();
                        gameInputMap[gameInput].Add(inputType, glyphs);
                    }
                    glyphs.Add(glyph);
                }
            }
        }

        public InputTouch() { } // 0x06000243: defaults/list capacities before Object..ctor
    }
}
