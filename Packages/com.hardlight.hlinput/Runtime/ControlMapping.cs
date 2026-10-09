using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLInput.Runtime02000021: all56 owner APIs, GameInputKey2, and four natural lambda methods.
    // Source preserves original shared state, callback ordering, faults, and dictionary quirks.
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Control Mapping")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ControlMapping : ControlMap
    {
        public static event Action<InputType> OnUpdateLastInputType = _ => { };
        public static event Action<int> OnUpdateLastJoystickIndex = _ => { };
        [SerializeField] private bool m_debugMode;
        [SerializeField] private bool m_editorIgnoreMouse;
        private static readonly Dictionary<GameInputKey, FastAction<Vector2>> s_onPointer = new Dictionary<GameInputKey, FastAction<Vector2>>();
        private static readonly Dictionary<GameInputKey, FastAction<float>> s_onScrollWheel = new Dictionary<GameInputKey, FastAction<float>>();
        private static readonly Dictionary<GameInputKey, FastAction<Vector2>> s_onTap = new Dictionary<GameInputKey, FastAction<Vector2>>();
        private static readonly Dictionary<GameInputKey, FastAction<Vector2>> s_onTouch = new Dictionary<GameInputKey, FastAction<Vector2>>();
        private static readonly Dictionary<GameInputKey, FastAction<Vector2>> s_onTouchRelease = new Dictionary<GameInputKey, FastAction<Vector2>>();
        private static readonly Dictionary<GameInputKey, FastAction<List<Vector2>>> s_onMultiTouch = new Dictionary<GameInputKey, FastAction<List<Vector2>>>();
        private static readonly Dictionary<GameInputKey, FastAction<Vector2>> s_onMultiTouchRelease = new Dictionary<GameInputKey, FastAction<Vector2>>();
        private static readonly Dictionary<GameInputKey, FastAction<Vector2>> s_onVectorisedGameInput = new Dictionary<GameInputKey, FastAction<Vector2>>();
        private static readonly Dictionary<GameInputKey, FastAction<float>> s_onButtonsHeld = new Dictionary<GameInputKey, FastAction<float>>();
        private static readonly Dictionary<GameInputKey, FastAction<float>> s_onButtonsDown = new Dictionary<GameInputKey, FastAction<float>>();
        private static readonly Dictionary<GameInputKey, FastAction<float>> s_onButtonsUp = new Dictionary<GameInputKey, FastAction<float>>();
        private static readonly Dictionary<GameInputKey, FastAction<float>> s_onAxis = new Dictionary<GameInputKey, FastAction<float>>();
        private static readonly Dictionary<GameInputKey, FastAction<float>> s_onAxisStart = new Dictionary<GameInputKey, FastAction<float>>();
        private static readonly Dictionary<GameInputKey, FastAction<float>> s_onAxisEnd = new Dictionary<GameInputKey, FastAction<float>>();
        private static readonly Dictionary<GameInputKey, FastAction<float>> s_onSwipe = new Dictionary<GameInputKey, FastAction<float>>();
        private readonly List<IInputButton> m_buttonProviders = new List<IInputButton>();
        private readonly List<GameInput> m_frameHandledGameInputs = new List<GameInput>();
        private static InputType s_lastInputType = InputType.Unsupported;
        private static int s_lastJoystickIndex = -1;
        private static readonly List<GameObject> s_eventHandlers = new List<GameObject>();
        private static readonly HashSet<GameInput> s_exclusiveGameInputs = new HashSet<GameInput>();

        // Original02000022 implements IEqualityComparer, although the15 dictionaries use their parameterless constructors.
        private struct GameInputKey : IEqualityComparer<GameInputKey>
        {
            public GameInput GameInput;
            public int JoystickIndex;
            // Original060000b5.
            public bool Equals(GameInputKey x, GameInputKey y) => x.GameInput == y.GameInput && x.JoystickIndex == y.JoystickIndex;
            // Original060000b6 uses unchecked32-bit arithmetic.
            public int GetHashCode(GameInputKey gameInputKey) => unchecked((int)gameInputKey.GameInput * 10 + gameInputKey.JoystickIndex + 1);
        }
        // Original06000081.
        public static InputType LastInputType => s_lastInputType;
        // Original06000082. AddUnique uses the genuine FastAction provider.
        private static void SubscribeInternal<T>(Dictionary<GameInputKey, FastAction<T>> dictionary, Action<T> callback, GameInput gameInput, int joystickIndex)
        {
            dictionary.TryGetOrNew(new GameInputKey { GameInput = gameInput, JoystickIndex = joystickIndex }).AddUnique(callback);
        }
        // Original06000083. The operator's return is intentionally discarded; the dictionary entry remains present.
        private static void UnsubscribeInternal<T>(Dictionary<GameInputKey, FastAction<T>> dictionary, Action<T> callback, GameInput gameInput, int joystickIndex)
        {
            if (dictionary.TryGetValue(new GameInputKey { GameInput = gameInput, JoystickIndex = joystickIndex }, out FastAction<T> action))
                action -= callback;
        }
        // Original06000084.
        public static void Subscribe(GameInput gameInput, Action<Vector2> callback, int joystickIndex = -1)
        {
            SubscribeInternal(s_onPointer, callback, gameInput, joystickIndex);
            SubscribeInternal(s_onTap, callback, gameInput, joystickIndex);
            SubscribeInternal(s_onTouch, callback, gameInput, joystickIndex);
            SubscribeInternal(s_onTouchRelease, callback, gameInput, joystickIndex);
            SubscribeInternal(s_onMultiTouchRelease, callback, gameInput, joystickIndex);
            SubscribeInternal(s_onVectorisedGameInput, callback, gameInput, joystickIndex);
        }
        // Original06000085.
        public static void Unsubscribe(GameInput gameInput, Action<Vector2> callback, int joystickIndex = -1)
        {
            UnsubscribeInternal(s_onPointer, callback, gameInput, joystickIndex);
            UnsubscribeInternal(s_onTap, callback, gameInput, joystickIndex);
            UnsubscribeInternal(s_onTouch, callback, gameInput, joystickIndex);
            UnsubscribeInternal(s_onTouchRelease, callback, gameInput, joystickIndex);
            UnsubscribeInternal(s_onMultiTouchRelease, callback, gameInput, joystickIndex);
            UnsubscribeInternal(s_onVectorisedGameInput, callback, gameInput, joystickIndex);
        }
        // Original06000086.
        public static void Subscribe(GameInput gameInput, Action<List<Vector2>> callback, int joystickIndex = -1)
        {
            SubscribeInternal(s_onMultiTouch, callback, gameInput, joystickIndex);
        }
        // Original06000087.
        public static void Unsubscribe(GameInput gameInput, Action<List<Vector2>> callback, int joystickIndex = -1)
        {
            UnsubscribeInternal(s_onMultiTouch, callback, gameInput, joystickIndex);
        }
        // Original06000088: all trigger paths, including invalid triggers, proceed to swipe and scroll subscriptions.
        public static void Subscribe(GameInput gameInput, Action<float> callback, InputTrigger inputTrigger = InputTrigger.Down, int joystickIndex = -1)
        {
            switch (inputTrigger)
            {
                case InputTrigger.Held:
                    SubscribeInternal(s_onButtonsHeld, callback, gameInput, joystickIndex);
                    SubscribeInternal(s_onAxis, callback, gameInput, joystickIndex);
                    break;
                case InputTrigger.Down:
                    SubscribeInternal(s_onButtonsDown, callback, gameInput, joystickIndex);
                    SubscribeInternal(s_onAxisStart, callback, gameInput, joystickIndex);
                    break;
                case InputTrigger.Up:
                    SubscribeInternal(s_onButtonsUp, callback, gameInput, joystickIndex);
                    SubscribeInternal(s_onAxisEnd, callback, gameInput, joystickIndex);
                    break;
                default:
                    HLOutput.LogError(string.Format("No subscription set for input trigger '{0}'.", inputTrigger), null);
                    break;
            }
            SubscribeInternal(s_onSwipe, callback, gameInput, joystickIndex);
            SubscribeInternal(s_onScrollWheel, callback, gameInput, joystickIndex);
        }
        // Original06000089.
        public static void Unsubscribe(GameInput gameInput, Action<float> callback, int joystickIndex = -1)
        {
            UnsubscribeInternal(s_onAxis, callback, gameInput, joystickIndex);
            UnsubscribeInternal(s_onAxisEnd, callback, gameInput, joystickIndex);
            UnsubscribeInternal(s_onAxisStart, callback, gameInput, joystickIndex);
            UnsubscribeInternal(s_onButtonsDown, callback, gameInput, joystickIndex);
            UnsubscribeInternal(s_onButtonsUp, callback, gameInput, joystickIndex);
            UnsubscribeInternal(s_onButtonsHeld, callback, gameInput, joystickIndex);
            UnsubscribeInternal(s_onSwipe, callback, gameInput, joystickIndex);
            UnsubscribeInternal(s_onScrollWheel, callback, gameInput, joystickIndex);
        }
        // Original0600008a/8b: duplicate and null registrations are retained by the real list.
        public static void RegisterEventHandler(GameObject gameObject) { s_eventHandlers.Add(gameObject); }
        public static void UnregisterEventHandler(GameObject gameObject) { s_eventHandlers.Remove(gameObject); }
        // Original0600008c: direct subscriptions precede registration, which subscribes the same callbacks again.
        public override void Initialise<TKey, TButtonBinding>(BaseInputButton<TKey, TButtonBinding> inputButton)
        {
            inputButton.ButtonUpHandlers += OnButtonUp;
            inputButton.ButtonDownHandlers += OnButtonDown;
            inputButton.ButtonHandlers += OnButtonHeld;
            RegisterButtonProvider(inputButton);
        }
        // Original0600008d.
        public override void Initialise<TAxis, TAxisBinding>(BaseInputAxis<TAxis, TAxisBinding> inputAxis)
        {
            inputAxis.AxisHandlers += OnAxis;
            inputAxis.AxisEndHandlers += OnAxisEnd;
            inputAxis.AxisStartHandlers += OnAxisStart;
        }
        // Original0600008e.
        public override void Initialise<TBindingType, TBindingData>(BaseInputVectorisedGameInput<TBindingType, TBindingData> inputVectorisedGameInput)
        {
            inputVectorisedGameInput.VectorisedGameInputHandlers += OnVectorisedGameInput;
        }
        // Original0600008f.
        public override void Initialise(InputSwipe inputSwipe, InputTouch inputTouch, InputPointer inputPointer)
        {
            UpdateLastInputType(InputType.Mouse);
            inputTouch.TapHandlers += OnTap;
            inputTouch.TouchHandlers += OnTouch;
            inputTouch.TouchReleaseHandlers += OnTouchRelease;
            inputTouch.MultiTouchHandlers += OnMultiTouch;
            inputTouch.MultiTouchReleaseHandlers += OnMultiTouchRelease;
            inputSwipe.SwipeHandlers += OnSwipe;
            inputPointer.PointerHandlers += OnPointer;
            inputPointer.ScrollWheelHandlers += OnScrollWheel;
        }
        // Original06000090 removes only the direct subscription set; provider cleanup occurs separately.
        public override void Shutdown<TKey, TButtonBinding>(BaseInputButton<TKey, TButtonBinding> inputButton)
        {
            inputButton.ButtonUpHandlers -= OnButtonUp;
            inputButton.ButtonDownHandlers -= OnButtonDown;
            inputButton.ButtonHandlers -= OnButtonHeld;
        }
        // Original06000091.
        public override void Shutdown<TAxis, TAxisBinding>(BaseInputAxis<TAxis, TAxisBinding> inputAxis)
        {
            inputAxis.AxisHandlers -= OnAxis;
            inputAxis.AxisEndHandlers -= OnAxisEnd;
            inputAxis.AxisStartHandlers -= OnAxisStart;
        }
        // Original06000092.
        public override void Shutdown<TBindingType, TBindingData>(BaseInputVectorisedGameInput<TBindingType, TBindingData> inputVectorisedGameInput)
        {
            inputVectorisedGameInput.VectorisedGameInputHandlers -= OnVectorisedGameInput;
        }
        // Original06000093 reverses the eight subscriptions, cleans providers, then clears14 dictionaries.
        public override void Shutdown(InputSwipe inputSwipe, InputTouch inputTouch, InputPointer inputPointer)
        {
            inputPointer.ScrollWheelHandlers -= OnScrollWheel;
            inputPointer.PointerHandlers -= OnPointer;
            inputSwipe.SwipeHandlers -= OnSwipe;
            inputTouch.MultiTouchReleaseHandlers -= OnMultiTouchRelease;
            inputTouch.MultiTouchHandlers -= OnMultiTouch;
            inputTouch.TouchReleaseHandlers -= OnTouchRelease;
            inputTouch.TouchHandlers -= OnTouch;
            inputTouch.TapHandlers -= OnTap;
            UnregisterButtonProviders();
            s_onPointer.Clear();
            s_onTap.Clear();
            s_onTouch.Clear();
            s_onTouchRelease.Clear();
            s_onMultiTouch.Clear();
            s_onMultiTouchRelease.Clear();
            s_onVectorisedGameInput.Clear();
            s_onButtonsHeld.Clear();
            s_onButtonsDown.Clear();
            s_onButtonsUp.Clear();
            s_onAxis.Clear();
            s_onAxisStart.Clear();
            s_onAxisEnd.Clear();
            s_onSwipe.Clear();
            // Scroll wheel callbacks are not cleared by either shipped implementation.
        }
        // Original06000094: store first, then subscribe down, up, and held.
        public void RegisterButtonProvider(IInputButton buttonProvider)
        {
            m_buttonProviders.Add(buttonProvider);
            buttonProvider.ButtonDownHandlers += OnButtonDown;
            buttonProvider.ButtonUpHandlers += OnButtonUp;
            buttonProvider.ButtonHandlers += OnButtonHeld;
        }
        // Original06000095: unsubscribe before removing a single list occurrence.
        public void UnregisterButtonProvider(IInputButton buttonProvider)
        {
            buttonProvider.ButtonDownHandlers -= OnButtonDown;
            buttonProvider.ButtonUpHandlers -= OnButtonUp;
            buttonProvider.ButtonHandlers -= OnButtonHeld;
            m_buttonProviders.Remove(buttonProvider);
        }
        // Original06000096: capture the initial final index, visit the live provider list in reverse.
        private void UnregisterButtonProviders()
        {
            for (int i = m_buttonProviders.Count - 1; i >= 0; --i)
                UnregisterButtonProvider(m_buttonProviders[i]);
        }
        // Original06000097..9a. ClearExclusiveInputs intentionally ignores its argument.
        public static void AddExclusiveInput(GameInput gameInput) { s_exclusiveGameInputs.Add(gameInput); }
        public static void AddExclusiveInput(IReadOnlyCollection<GameInput> gameInputs) { s_exclusiveGameInputs.AddRange(gameInputs); }
        public static void RemoveExclusiveInput(GameInput gameInput) { s_exclusiveGameInputs.Remove(gameInput); }
        public static void ClearExclusiveInputs(IReadOnlyCollection<GameInput> gameInputs) { s_exclusiveGameInputs.Clear(); }
        // Original0600009b: the returned totals are discarded and scroll wheel callbacks are omitted.
        private void ValidateShutdown()
        {
            CountSubscribers<float, FastAction<float>>(s_onAxis, "m_onAxis");
            CountSubscribers<float, FastAction<float>>(s_onAxisEnd, "m_onAxisEnd");
            CountSubscribers<float, FastAction<float>>(s_onAxisStart, "m_onAxisStart");
            CountSubscribers<float, FastAction<float>>(s_onButtonsDown, "m_onButtonsDown");
            CountSubscribers<float, FastAction<float>>(s_onButtonsHeld, "m_onButtonsHeld");
            CountSubscribers<float, FastAction<float>>(s_onButtonsUp, "m_onButtonsUp");
            CountSubscribers<List<Vector2>, FastAction<List<Vector2>>>(s_onMultiTouch, "m_onMultiTouch");
            CountSubscribers<Vector2, FastAction<Vector2>>(s_onMultiTouchRelease, "m_onMultiTouchRelease");
            CountSubscribers<Vector2, FastAction<Vector2>>(s_onPointer, "m_onPointer");
            CountSubscribers<float, FastAction<float>>(s_onSwipe, "m_onSwipe");
            CountSubscribers<Vector2, FastAction<Vector2>>(s_onTap, "m_onTap");
            CountSubscribers<Vector2, FastAction<Vector2>>(s_onTouch, "m_onTouch");
            CountSubscribers<Vector2, FastAction<Vector2>>(s_onTouchRelease, "m_onTouchRelease");
            CountSubscribers<Vector2, FastAction<Vector2>>(s_onVectorisedGameInput, "m_onVectorisedGameInput");
        }
        // Original0600009c retains the unused message and unused builder, including callback reflection/formatting side effects.
        private static int CountSubscribers<T, T1>(Dictionary<GameInputKey, T1> dictionary, string message) where T1 : FastAction<T>
        {
            StringBuilder builder = new StringBuilder();
            int count = 0;
            foreach (KeyValuePair<GameInputKey, T1> pair in dictionary)
            {
                count = unchecked(count + pair.Value.GetInvocationListCount());
                if (count > 0)
                {
                    List<Action<T>> callbacks = pair.Value.GetInvocationList();
                    for (int i = 0; i < callbacks.Count; ++i)
                    {
                        Action<T> callback = callbacks[i];
                        builder.Append("Callback Info:\n");
                        builder.Append(string.Format("{0}\n", callback.Method));
                        builder.Append(string.Format("{0}\n", callback.Target));
                    }
                }
            }
            return count;
        }
        // Original0600009d: frame consumption precedes blocked-type filtering, current shared last type is tested after both callbacks.
        private void HandleInput<T>(Dictionary<GameInputKey, FastAction<T>> dictionary, GameInput gameInput, T value,
            int joystickIndex = -1, InputType? inputType = null, bool skipCheckHandledInputThisFrame = false, bool skipUpdateLastProperties = false)
        {
            if (IsGameInputDisabled(gameInput)) return;
            if (s_exclusiveGameInputs.Count > 0 && !s_exclusiveGameInputs.Contains(gameInput)) return;
            if (!skipCheckHandledInputThisFrame && CheckHandledInputThisFrame(gameInput)) return;
            if (inputType.HasValue && IsInputTypeBlocked(inputType.Value)) return;
            if (!skipUpdateLastProperties)
            {
                if (!inputType.HasValue) inputType = InputTypeResolver.ResolveTouchInput();
                UpdateLastInputType(inputType.Value);
                UpdateLastJoystickIndex(joystickIndex);
            }
            if (s_lastInputType == InputType.Unsupported) return;
            GameInputKey key = new GameInputKey { GameInput = gameInput, JoystickIndex = joystickIndex };
            if (joystickIndex == -1 || !dictionary.ContainsKey(key)) key.JoystickIndex = -1;
            if (dictionary.TryGetValue(key, out FastAction<T> action)) action.Invoke(value);
        }
        // Original0600009e.
        private void OnTap(GameInput gameInput, Vector2 point) { HandleInput(s_onTap, gameInput, point); }
        // Original0600009f.
        private void OnTouch(GameInput gameInput, Vector2 point) { HandleInput(s_onTouch, gameInput, point); }
        // Original060000a0.
        private void OnTouchRelease(GameInput gameInput, Vector2 point) { HandleInput(s_onTouchRelease, gameInput, point); }
        // Original060000a1.
        private void OnMultiTouch(GameInput gameInput, List<Vector2> points) { HandleInput(s_onMultiTouch, gameInput, points); }
        // Original060000a2.
        private void OnMultiTouchRelease(GameInput gameInput, Vector2 point) { HandleInput(s_onMultiTouchRelease, gameInput, point); }
        // Original060000a3.
        private void OnSwipe(int joystickIndex, GameInput gameInput, float value) { HandleInput(s_onSwipe, gameInput, value, joystickIndex); }
        // Original060000a4.
        private void OnButtonDown(int joystickIndex, GameInput gameInput, float value, InputType inputType) { HandleInput(s_onButtonsDown, gameInput, value, joystickIndex, inputType); }
        // Original060000a5.
        private void OnButtonUp(int joystickIndex, GameInput gameInput, float value, InputType inputType) { HandleInput(s_onButtonsUp, gameInput, value, joystickIndex, inputType); }
        // Original060000a6.
        private void OnButtonHeld(int joystickIndex, GameInput gameInput, float value, InputType inputType) { HandleInput(s_onButtonsHeld, gameInput, value, joystickIndex, inputType); }
        // Original060000a7.
        private void OnAxis(int joystickIndex, GameInput gameInput, float value, InputType inputType) { HandleInput(s_onAxis, gameInput, value, joystickIndex, inputType); }
        // Original060000a8.
        private void OnAxisEnd(int joystickIndex, GameInput gameInput, float value, InputType inputType) { HandleInput(s_onAxisEnd, gameInput, value, joystickIndex, inputType); }
        // Original060000a9.
        private void OnAxisStart(int joystickIndex, GameInput gameInput, float value, InputType inputType) { HandleInput(s_onAxisStart, gameInput, value, joystickIndex, inputType); }
        // Original060000aa drops z and permits repeated pointer input within a frame.
        private void OnPointer(GameInput gameInput, Vector3 position) { HandleInput(s_onPointer, gameInput, (Vector2)position, -1, null, true); }
        // Original060000ab explicitly reports Mouse.
        private void OnScrollWheel(GameInput gameInput, float value) { HandleInput(s_onScrollWheel, gameInput, value, -1, InputType.Mouse); }
        // Original060000ac drops z and retains the previously stored last-input properties.
        private void OnVectorisedGameInput(GameInput gameInput, Vector3 value) { HandleInput(s_onVectorisedGameInput, gameInput, (Vector2)value, -1, null, false, true); }
        // Original060000ad.
        private bool CheckHandledInputThisFrame(GameInput gameInput)
        {
            bool handled = m_frameHandledGameInputs.Contains(gameInput);
            if (!handled) m_frameHandledGameInputs.Add(gameInput);
            return handled;
        }
        // Original060000ae.
        public override void ClearFrameInputs() { m_frameHandledGameInputs.Clear(); }
        // Original060000af: equality returns before the blocked query; shared type is stored before event-data allocation/callbacks.
        private void UpdateLastInputType(InputType inputType)
        {
            if (inputType == s_lastInputType) return;
            inputType = IsInputTypeBlocked(inputType) ? InputType.Unsupported : inputType;
            s_lastInputType = inputType;
            LastInputTypeUpdateEventData eventData = new LastInputTypeUpdateEventData(EventSystem.current, (int)inputType);
            int count = s_eventHandlers.Count;
            for (int i = 0; i < count; ++i)
                ExecuteEvents.ExecuteHierarchy(s_eventHandlers[i], eventData, EventHandlers.LastInputTypeUpdateHandler);
            OnUpdateLastInputType(inputType);
        }
        // Original060000b0.
        private void UpdateLastJoystickIndex(int joystickIndex)
        {
            if (joystickIndex == s_lastJoystickIndex) return;
            s_lastJoystickIndex = joystickIndex;
            OnUpdateLastJoystickIndex(joystickIndex);
        }
        // Original060000b1: disabling invokes up/end zeros before the authentic base callback.
        protected override void NotifyInputDisabledUpdated(GameInput gameInput, bool disabled)
        {
            if (disabled)
            {
                InvokeCallbacksForInput(s_onButtonsUp, gameInput, 0f);
                InvokeCallbacksForInput(s_onAxisEnd, gameInput, 0f);
            }
            base.NotifyInputDisabledUpdated(gameInput, disabled);
        }
        // Original060000b2 visits the live dictionary and every matching joystick key, preserving enumeration faults.
        private void InvokeCallbacksForInput<T>(Dictionary<GameInputKey, FastAction<T>> dictionary, GameInput gameInput, T value)
        {
            foreach (KeyValuePair<GameInputKey, FastAction<T>> pair in dictionary)
                if (pair.Key.GameInput == gameInput) pair.Value.Invoke(value);
        }
        // Implicit060000b3 allocates the two instance lists before the ControlMap constructor.
        // Implicit060000b4 retains BeforeFieldInit; the two no-op event lambdas generate the natural060000b7..ba group.
    }
}
