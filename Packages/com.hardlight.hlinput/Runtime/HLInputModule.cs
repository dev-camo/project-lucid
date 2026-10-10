using System;
using System.Collections.Generic;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace Hardlight
{
    // Original HLInput.Runtime 0x02000027; original PointerInputModule base is provided by Unity UI.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [AddComponentMenu("Event/HL Input Module")]
    public class HLInputModule : PointerInputModule, ILastInputTypeUpdateHandler
    {
        [SerializeField] private float m_InputActionsPerSecond = 10f;
        [SerializeField] private float m_RepeatDelay = 0.5f;
        [SerializeField] private bool m_releaseMouseOnLoseFocus;
        [SerializeField, FormerlySerializedAs("m_AllowActivationOnMobileDevice")] private bool m_ForceModuleActive;
        [SerializeField] private bool m_enableNavigationHold;
        [HashEnum(typeof(GameInput)), SerializeField] private GameInput m_submitNavigationInput;
        [SerializeField, HashEnum(typeof(GameInput))] private GameInput m_backNavigationInput;
        [SerializeField, HashEnum(typeof(GameInput))] private GameInput m_leftNavigationInput;
        [SerializeField, HashEnum(typeof(GameInput))] private GameInput m_rightNavigationInput;
        [SerializeField, HashEnum(typeof(GameInput))] private GameInput m_upNavigationInput;
        [HashEnum(typeof(GameInput)), SerializeField] private GameInput m_downNavigationInput;
        [HashEnum(typeof(GameInput)), SerializeField] private GameInput m_scrollUpInput;
        [HashEnum(typeof(GameInput)), SerializeField] private GameInput m_scrollDownInput;
        [SerializeField, HashEnum(typeof(GameInput))] private GameInput m_tabNextInput;
        [HashEnum(typeof(GameInput)), SerializeField] private GameInput m_tabPreviousInput;
        [HashEnum(typeof(GameInput)), SerializeField] private GameInput m_tooltipToggleInput;
        [SerializeField] private CursorBase m_cursor;
        [SerializeField] private ControlMap m_controlMap;
        [SerializeField] private float m_controllerConnectionPollingRateInSeconds = 3f;
        [SerializeField] private BaseInputMonitor[] m_inputMonitors;
        [FormerlySerializedAs("m_inputTypesThatRequireSelection"), SerializeField] private InputType[] m_inputTypesThatEnforceSelectionControl;
        [SerializeField] private bool useEditorOverrideList;
        [SerializeField] private InputType[] m_editorOverrideInputTypesThatEnforceSelectionControl;
        [SerializeField] private ScriptableControllerProvider[] m_baseControllerProviders;

        // Original 0x060000ca..0xce. The editor override fields are retained but the shipped getter
        // reads only m_inputTypesThatEnforceSelectionControl on both architectures.
        public IReadOnlyList<ICurrentBaseInputBindingsProvider> CurrentBaseInputBindingsProviders
        { get { return GetCachedCurrentBaseInputBindingsProviders(); } }
        public IBaseControllerProvider BaseControllerProvider { get { return m_baseControllerProvider; } }
        public GlyphLookupSystem GlyphLookupSystem { get; private set; }
        private InputType[] InputTypesThatEnforceSelectionControl { get { return m_inputTypesThatEnforceSelectionControl; } }

        // Original public static field has no initializer, readonly flag, or retained static ctor.
        public static FastAction OnShutdown;
        private bool m_cursorValid;
        private GameObject m_lastKnownSelectedGameObject;
        private GameObject m_CurrentFocusedGameObject;
        private PointerEventData m_InputPointerEvent;
        private BaseControllerProvider m_baseControllerProvider;
        private List<ICurrentBaseInputBindingsProvider> m_currentBaseInputBindingsProviders;
        private bool m_doesLastInputTypeRequireSelection;
        private Vector2 m_lastNavigationDirection = Vector2.zero;
        private bool m_hardwareCursorIsVisible = true;

        // Original 0x060000cf. Controller iteration captures its array; monitor iteration rereads
        // the live field. No replacement controller or null guards are added to the original path.
        protected override void Awake()
        {
            base.Awake();
            if (InputTypesThatEnforceSelectionControl != null && InputTypesThatEnforceSelectionControl.Length != 0)
                ControlMapping.RegisterEventHandler(gameObject);
#if PROJECT_LUCID_ORIGINAL_APPLE_INPUT
            foreach (ScriptableControllerProvider provider in m_baseControllerProviders)
            {
                if (provider.IsActiveProvider())
                {
                    m_baseControllerProvider = provider.GetControllerNameProvider(m_controllerConnectionPollingRateInSeconds);
                    break;
                }
            }
#else
            // Separately authored portable selection; original providers remain preserved above.
            m_baseControllerProvider = ProjectLucid.Offline.PortableControllerSelection.Create(m_controllerConnectionPollingRateInSeconds);
#endif
            m_baseControllerProvider.Initialise();
            GlyphLookupSystem = new GlyphLookupSystem();
            for (int i = 0; i < m_inputMonitors.Length; ++i)
                m_inputMonitors[i].Initialise(m_baseControllerProvider, m_controlMap, GlyphLookupSystem);
        }

        // Original 0x060000d0. Shutdown notifications precede monitor/controller teardown.
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (OnShutdown != null) OnShutdown.Invoke();
            for (int i = 0; i < m_inputMonitors.Length; ++i) m_inputMonitors[i].Shutdown();
            GlyphLookupSystem = null;
            m_baseControllerProvider.Shutdown();
            m_baseControllerProvider = null;
            if (InputTypesThatEnforceSelectionControl != null && InputTypesThatEnforceSelectionControl.Length != 0)
                ControlMapping.UnregisterEventHandler(gameObject);
        }

        // Original 0x060000d1. Delegates are formed before each helper can skip numeric-zero input.
        protected override void OnEnable()
        {
            base.OnEnable();
            m_cursorValid = m_cursor != null;
            SubscribeIfValid(m_submitNavigationInput, OnSubmit);
            SubscribeIfValid(m_backNavigationInput, OnBack);
            SubscribeIfValid(m_leftNavigationInput, OnLeft);
            SubscribeIfValid(m_rightNavigationInput, OnRight);
            SubscribeIfValid(m_upNavigationInput, OnUp);
            SubscribeIfValid(m_downNavigationInput, OnDown);
            if (m_enableNavigationHold)
            {
                SubscribeIfValid(m_leftNavigationInput, OnLeftHold, InputTrigger.Held);
                SubscribeIfValid(m_rightNavigationInput, OnRightHold, InputTrigger.Held);
                SubscribeIfValid(m_upNavigationInput, OnUpHold, InputTrigger.Held);
                SubscribeIfValid(m_downNavigationInput, OnDownHold, InputTrigger.Held);
            }
            SubscribeIfValid(m_scrollUpInput, OnScrollUp);
            SubscribeIfValid(m_scrollDownInput, OnScrollDown);
            SubscribeIfValid(m_tabNextInput, OnTabNext);
            SubscribeIfValid(m_tabPreviousInput, OnTabPrevious);
            SubscribeIfValid(m_tooltipToggleInput, OnTooltipToggle);
        }

        // Original 0x060000d2 removes hold callbacks unconditionally, including when hold is disabled.
        protected override void OnDisable()
        {
            base.OnDisable();
            UnsubscribeIfValid(m_submitNavigationInput, OnSubmit);
            UnsubscribeIfValid(m_backNavigationInput, OnBack);
            UnsubscribeIfValid(m_leftNavigationInput, OnLeft);
            UnsubscribeIfValid(m_rightNavigationInput, OnRight);
            UnsubscribeIfValid(m_upNavigationInput, OnUp);
            UnsubscribeIfValid(m_downNavigationInput, OnDown);
            UnsubscribeIfValid(m_leftNavigationInput, OnLeftHold);
            UnsubscribeIfValid(m_rightNavigationInput, OnRightHold);
            UnsubscribeIfValid(m_upNavigationInput, OnUpHold);
            UnsubscribeIfValid(m_downNavigationInput, OnDownHold);
            UnsubscribeIfValid(m_scrollUpInput, OnScrollUp);
            UnsubscribeIfValid(m_scrollDownInput, OnScrollDown);
            UnsubscribeIfValid(m_tabNextInput, OnTabNext);
            UnsubscribeIfValid(m_tabPreviousInput, OnTabPrevious);
            UnsubscribeIfValid(m_tooltipToggleInput, OnTooltipToggle);
        }

        // Original 0x060000d3..0xd6. Numeric zero is the actual skipped value; joystick id is -1.
        private void SubscribeIfValid(GameInput gameInput, Action<float> callback, InputTrigger inputTrigger = InputTrigger.Down)
        { if ((int)gameInput != 0) ControlMapping.Subscribe(gameInput, callback, inputTrigger, -1); }
        private void SubscribeIfValid(GameInput gameInput, Action<Vector2> callback)
        { if ((int)gameInput != 0) ControlMapping.Subscribe(gameInput, callback, -1); }
        private void UnsubscribeIfValid(GameInput gameInput, Action<float> callback)
        { if ((int)gameInput != 0) ControlMapping.Unsubscribe(gameInput, callback, -1); }
        private void UnsubscribeIfValid(GameInput gameInput, Action<Vector2> callback)
        { if ((int)gameInput != 0) ControlMapping.Unsubscribe(gameInput, callback, -1); }

        // Original 0x060000d7 captures navigation/selection before consulting the live cursor.
        // The selected-object availability test is a managed reference test in the shipped code.
        private bool ValidForNavigationEvent()
        {
            bool navigation = eventSystem.sendNavigationEvents;
            GameObject selected = eventSystem.currentSelectedGameObject;
            GameObject remembered = m_lastKnownSelectedGameObject;
            bool blocked = m_cursorValid && m_cursor.CursorVisible() && m_cursor.IsInputBlocked();
            return navigation && ((object)selected != null || (object)remembered != null) && !blocked;
        }

        // Original 0x060000d8. Submit captures its selected target before requesting event data.
        private void OnSubmit(float value)
        {
            if (!ValidForNavigationEvent()) return;
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected == null) selected = m_lastKnownSelectedGameObject;
            BaseEventData data = GetBaseEventData();
            ExecuteEvents.Execute(selected, data, ExecuteEvents.submitHandler);
        }
        // Original 0x060000d9. Back requests data first and has no remembered-selection fallback.
        private void OnBack(float value)
        {
            if (!ValidForNavigationEvent()) return;
            BaseEventData data = GetBaseEventData();
            ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, data, ExecuteEvents.cancelHandler);
        }
        // Original 0x060000da/0xdb use dead zone zero and request axis data before selecting target.
        private void OnHorizontalNavigationEvent(float value)
        {
            if (!ValidForNavigationEvent()) return;
            AxisEventData data = GetAxisEventData(value, 0f, 0f);
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected == null) selected = m_lastKnownSelectedGameObject;
            ExecuteEvents.Execute(selected, data, ExecuteEvents.moveHandler);
        }
        private void OnVerticalNavigationEvent(float value)
        {
            if (!ValidForNavigationEvent()) return;
            AxisEventData data = GetAxisEventData(0f, value, 0f);
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected == null) selected = m_lastKnownSelectedGameObject;
            ExecuteEvents.Execute(selected, data, ExecuteEvents.moveHandler);
        }

        // Original 0x060000dc..0xe3. A down callback updates remembered direction even when
        // navigation is blocked; hold callbacks only repeat the matching Unity Vector2 direction.
        private void OnLeft(float value) { m_lastNavigationDirection = Vector2.left; OnHorizontalNavigationEvent(-Mathf.Abs(value)); }
        private void OnLeftHold(float value) { if (m_lastNavigationDirection == Vector2.left) OnHorizontalNavigationEvent(-Mathf.Abs(value)); }
        private void OnRight(float value) { m_lastNavigationDirection = Vector2.right; OnHorizontalNavigationEvent(Mathf.Abs(value)); }
        private void OnRightHold(float value) { if (m_lastNavigationDirection == Vector2.right) OnHorizontalNavigationEvent(Mathf.Abs(value)); }
        private void OnUp(float value) { m_lastNavigationDirection = Vector2.up; OnVerticalNavigationEvent(Mathf.Abs(value)); }
        private void OnUpHold(float value) { if (m_lastNavigationDirection == Vector2.up) OnVerticalNavigationEvent(Mathf.Abs(value)); }
        private void OnDown(float value) { m_lastNavigationDirection = Vector2.down; OnVerticalNavigationEvent(-Mathf.Abs(value)); }
        private void OnDownHold(float value) { if (m_lastNavigationDirection == Vector2.down) OnVerticalNavigationEvent(-Mathf.Abs(value)); }

        // Original 0x060000e4/0xe5: values are passed through without changing their sign.
        // These cursor events use visibility and their own event data, without navigation gating.
        private void OnScrollUp(float value)
        {
            if (m_cursorValid && m_cursor.CursorVisible())
            {
                ScrollEventData data = new ScrollEventData(eventSystem, value);
                ExecuteEvents.ExecuteHierarchy(m_cursor.GetRaycastResult().gameObject, data, EventHandlers.ScrollUpHandler);
            }
        }
        private void OnScrollDown(float value)
        {
            if (m_cursorValid && m_cursor.CursorVisible())
            {
                ScrollEventData data = new ScrollEventData(eventSystem, value);
                ExecuteEvents.ExecuteHierarchy(m_cursor.GetRaycastResult().gameObject, data, EventHandlers.ScrollDownHandler);
            }
        }
        // Original 0x060000e6..0xe8 request base data before rereading the cursor raycast target.
        private void OnTabNext(float value)
        {
            if (m_cursorValid && m_cursor.CursorVisible())
            {
                BaseEventData data = GetBaseEventData();
                ExecuteEvents.ExecuteHierarchy(m_cursor.GetRaycastResult().gameObject, data, EventHandlers.TabNextHandler);
            }
        }
        private void OnTabPrevious(float value)
        {
            if (m_cursorValid && m_cursor.CursorVisible())
            {
                BaseEventData data = GetBaseEventData();
                ExecuteEvents.ExecuteHierarchy(m_cursor.GetRaycastResult().gameObject, data, EventHandlers.TabPreviousHandler);
            }
        }
        private void OnTooltipToggle(float value)
        {
            if (m_cursorValid && m_cursor.CursorVisible())
            {
                BaseEventData data = GetBaseEventData();
                ExecuteEvents.ExecuteHierarchy(m_cursor.GetRaycastResult().gameObject, data, EventHandlers.TooltipToggleHandler);
            }
        }

        // Original 0x060000e9 retains a valid remembered selection before using EventSystem.current.
        public void SetSelectedGameObjectWithMemory(GameObject newSelection = null, BaseEventData eventData = null)
        {
            if (newSelection != null) m_lastKnownSelectedGameObject = newSelection;
            EventSystem.current.SetSelectedGameObject(newSelection, eventData);
        }
        // Original 0x060000ea/0xeb compare the ISelectHandler owner with remembered selection
        // using managed identity; deselection does not clear that remembered field.
        public void DeselectObjectIfCurrentlySelected(GameObject objectToDeselect = null, BaseEventData eventData = null)
        {
            if (IsObjectSelected(objectToDeselect) && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null, eventData);
        }
        public bool IsObjectSelected(GameObject prospectiveObject)
        { return (object)ExecuteEvents.GetEventHandler<ISelectHandler>(prospectiveObject) == (object)m_lastKnownSelectedGameObject; }

        // Original 0x060000ec..0xef. Values are retained directly without clamping.
        public float inputActionsPerSecond { get { return m_InputActionsPerSecond; } set { m_InputActionsPerSecond = value; } }
        public float repeatDelay { get { return m_RepeatDelay; } set { m_RepeatDelay = value; } }
        // Original 0x060000f0 queries SystemInfo and returns false in both shipped macOS slices.
        // This is shipping-body recovery; no unseen platform-conditional branch is invented.
        private bool ShouldIgnoreEventsOnNoFocus()
        { OperatingSystemFamily family = SystemInfo.operatingSystemFamily; return false; }
        // Original 0x060000f1..0xf4 retain focus query order and real PointerInputModule lifecycle.
        public override void UpdateModule()
        { if (!eventSystem.isFocused && ShouldIgnoreEventsOnNoFocus()) return; }
        public override bool IsModuleSupported()
        { return m_ForceModuleActive || Input.mousePresent || Input.touchSupported; }
        public override void ActivateModule()
        {
            if (!eventSystem.isFocused && ShouldIgnoreEventsOnNoFocus()) return;
            base.ActivateModule();
            GameObject selected = eventSystem.currentSelectedGameObject;
            if ((object)selected == null) selected = eventSystem.firstSelectedGameObject;
            eventSystem.SetSelectedGameObject(selected, GetBaseEventData());
        }
        public override void DeactivateModule() { base.DeactivateModule(); ClearSelection(); }

        // Original 0x060000f5 processes native touch/mouse first, then live monitor updates.
        // Cursor processing runs last, so monitor faults preserve prior pointer side effects.
        public override void Process()
        {
            if (!ProcessTouchEvents() && !(m_cursorValid && m_cursor.CursorVisible())) ProcessMouseEvent();
            for (int i = 0; i < m_inputMonitors.Length; ++i) m_inputMonitors[i].Update();
            if (!eventSystem.isFocused && ShouldIgnoreEventsOnNoFocus()) return;
            SendUpdateEventToSelectedObject();
            if (m_cursorValid && m_cursor.CursorVisible())
            {
                m_cursor.Process();
                ProcessCursorEvent();
                m_cursor.PostProcess();
            }
        }

        // Original 0x060000f6 uses the custom cursor's genuine left-button state.
        protected void ProcessCursorEvent()
        {
            MouseButtonEventData left = m_cursor.GetCursorEventData().GetButtonState(PointerEventData.InputButton.Left).eventData;
            m_CurrentFocusedGameObject = left.buttonData.pointerCurrentRaycast.gameObject;
            ProcessMousePress(left);
            ProcessMove(left.buttonData);
            ProcessDrag(left.buttonData);
            if (!Mathf.Approximately(left.buttonData.scrollDelta.sqrMagnitude, 0f))
            {
                GameObject scrollHandler = ExecuteEvents.GetEventHandler<IScrollHandler>(left.buttonData.pointerCurrentRaycast.gameObject);
                ExecuteEvents.ExecuteHierarchy(scrollHandler, left.buttonData, ExecuteEvents.scrollHandler);
            }
        }

        // Original 0x060000f7 rereads Input.touchCount for iteration and final return; indirect
        // touches are skipped, but a positive count still suppresses native mouse processing.
        private bool ProcessTouchEvents()
        {
            for (int i = 0; i < Input.touchCount; ++i)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.type == TouchType.Indirect) continue;
                bool pressed, released;
                PointerEventData pointer = GetTouchPointerEventData(touch, out pressed, out released);
                ProcessTouchPress(pointer, pressed, released);
                if (!released) { ProcessMove(pointer); ProcessDrag(pointer); }
                else RemovePointerData(pointer);
            }
            return Input.touchCount > 0;
        }

        // Original 0x060000f8. Managed-reference comparisons, captured raycast target and
        // callback/write ordering differ from newer Unity input modules and are preserved.
        protected void ProcessTouchPress(PointerEventData pointerEvent, bool pressed, bool released)
        {
            GameObject currentOverGo = pointerEvent.pointerCurrentRaycast.gameObject;
            if (pressed)
            {
                pointerEvent.eligibleForClick = true;
                pointerEvent.delta = Vector2.zero;
                pointerEvent.dragging = false;
                pointerEvent.useDragThreshold = true;
                pointerEvent.pressPosition = pointerEvent.position;
                pointerEvent.pointerPressRaycast = pointerEvent.pointerCurrentRaycast;
                DeselectIfSelectionChanged(currentOverGo, pointerEvent);
                if ((object)pointerEvent.pointerEnter != (object)currentOverGo)
                {
                    HandlePointerExitAndEnter(pointerEvent, currentOverGo);
                    pointerEvent.pointerEnter = currentOverGo;
                }
                GameObject newPressed = ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerEvent, ExecuteEvents.pointerDownHandler);
                if ((object)newPressed == null) newPressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);
                float time = Time.unscaledTime;
                if ((object)newPressed == (object)pointerEvent.lastPress)
                {
                    float diffTime = time - pointerEvent.clickTime;
                    // Original widened-float native threshold includes exactly 0.3f.
                    if (diffTime <= 0.3f) ++pointerEvent.clickCount;
                    else pointerEvent.clickCount = 1;
                    pointerEvent.clickTime = time;
                }
                else pointerEvent.clickCount = 1;
                pointerEvent.pointerPress = newPressed;
                pointerEvent.rawPointerPress = currentOverGo;
                pointerEvent.clickTime = time;
                pointerEvent.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(currentOverGo);
                if ((object)pointerEvent.pointerDrag != null)
                    ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.initializePotentialDrag);
                m_InputPointerEvent = pointerEvent;
            }
            if (released)
            {
                ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);
                GameObject pointerUpHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);
                if ((object)pointerEvent.pointerPress == (object)pointerUpHandler && pointerEvent.eligibleForClick)
                    ExecuteEvents.Execute(pointerUpHandler, pointerEvent, ExecuteEvents.pointerClickHandler);
                else if ((object)pointerEvent.pointerDrag != null && pointerEvent.dragging)
                    ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerEvent, ExecuteEvents.dropHandler);
                pointerEvent.eligibleForClick = false;
                pointerEvent.pointerPress = null;
                pointerEvent.rawPointerPress = null;
                if ((object)pointerEvent.pointerDrag != null && pointerEvent.dragging)
                    ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.endDragHandler);
                pointerEvent.dragging = false;
                pointerEvent.pointerDrag = null;
                ExecuteEvents.ExecuteHierarchy(pointerEvent.pointerEnter, pointerEvent, ExecuteEvents.pointerExitHandler);
                pointerEvent.pointerEnter = null;
                m_InputPointerEvent = pointerEvent;
            }
        }

        // Original 0x060000f9/0xfa. Right/middle states are fetched separately for press and drag.
        protected void ProcessMouseEvent() { ProcessMouseEvent(0); }
        protected void ProcessMouseEvent(int id)
        {
            MouseState mouseData = GetMousePointerEventData(id);
            MouseButtonEventData left = mouseData.GetButtonState(PointerEventData.InputButton.Left).eventData;
            m_CurrentFocusedGameObject = left.buttonData.pointerCurrentRaycast.gameObject;
            ProcessMousePress(left);
            ProcessMove(left.buttonData);
            ProcessDrag(left.buttonData);
            ProcessMousePress(mouseData.GetButtonState(PointerEventData.InputButton.Right).eventData);
            ProcessDrag(mouseData.GetButtonState(PointerEventData.InputButton.Right).eventData.buttonData);
            ProcessMousePress(mouseData.GetButtonState(PointerEventData.InputButton.Middle).eventData);
            ProcessDrag(mouseData.GetButtonState(PointerEventData.InputButton.Middle).eventData.buttonData);
            if (!Mathf.Approximately(left.buttonData.scrollDelta.sqrMagnitude, 0f))
            {
                GameObject scrollHandler = ExecuteEvents.GetEventHandler<IScrollHandler>(left.buttonData.pointerCurrentRaycast.gameObject);
                ExecuteEvents.ExecuteHierarchy(scrollHandler, left.buttonData, ExecuteEvents.scrollHandler);
            }
        }

        // Original 0x060000fb clears only cached mouse drag state on lost focus, without sending
        // end-drag callbacks, before forwarding focus to every live monitor.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (m_releaseMouseOnLoseFocus && !hasFocus)
            {
                PointerEventData pointer;
                if (m_PointerData.TryGetValue(-1, out pointer)) { pointer.pointerDrag = null; pointer.dragging = false; }
                if (m_PointerData.TryGetValue(-2, out pointer)) { pointer.pointerDrag = null; pointer.dragging = false; }
                if (m_PointerData.TryGetValue(-3, out pointer)) { pointer.pointerDrag = null; pointer.dragging = false; }
            }
            for (int i = 0; i < m_inputMonitors.Length; ++i) m_inputMonitors[i].ApplicationFocus(hasFocus);
        }

        // Original 0x060000fc uses a managed availability check and rereads selection after data.
        protected bool SendUpdateEventToSelectedObject()
        {
            if ((object)eventSystem.currentSelectedGameObject == null) return false;
            BaseEventData data = GetBaseEventData();
            ExecuteEvents.Execute(eventSystem.currentSelectedGameObject, data, ExecuteEvents.updateSelectedHandler);
            return data.used;
        }

        // Original 0x060000fd has a separate press/release path for mouse/cursor data. There is no
        // touch-style pointerEnter assignment on press; release refreshes enter/exit on target change.
        protected void ProcessMousePress(MouseButtonEventData data)
        {
            PointerEventData pointerEvent = data.buttonData;
            GameObject currentOverGo = pointerEvent.pointerCurrentRaycast.gameObject;
            if (data.PressedThisFrame())
            {
                pointerEvent.eligibleForClick = true;
                pointerEvent.delta = Vector2.zero;
                pointerEvent.dragging = false;
                pointerEvent.useDragThreshold = true;
                pointerEvent.pressPosition = pointerEvent.position;
                pointerEvent.pointerPressRaycast = pointerEvent.pointerCurrentRaycast;
                DeselectIfSelectionChanged(currentOverGo, pointerEvent);
                GameObject newPressed = ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerEvent, ExecuteEvents.pointerDownHandler);
                if ((object)newPressed == null) newPressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);
                float time = Time.unscaledTime;
                if ((object)newPressed == (object)pointerEvent.lastPress)
                {
                    float diffTime = time - pointerEvent.clickTime;
                    // Original widened-float native threshold includes exactly 0.3f.
                    if (diffTime <= 0.3f) ++pointerEvent.clickCount;
                    else pointerEvent.clickCount = 1;
                    pointerEvent.clickTime = time;
                }
                else pointerEvent.clickCount = 1;
                pointerEvent.pointerPress = newPressed;
                pointerEvent.rawPointerPress = currentOverGo;
                pointerEvent.clickTime = time;
                pointerEvent.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(currentOverGo);
                if ((object)pointerEvent.pointerDrag != null)
                    ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.initializePotentialDrag);
                m_InputPointerEvent = pointerEvent;
            }
            if (data.ReleasedThisFrame())
            {
                ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);
                GameObject pointerUpHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(currentOverGo);
                if ((object)pointerEvent.pointerPress == (object)pointerUpHandler && pointerEvent.eligibleForClick)
                    ExecuteEvents.Execute(pointerUpHandler, pointerEvent, ExecuteEvents.pointerClickHandler);
                else if ((object)pointerEvent.pointerDrag != null && pointerEvent.dragging)
                    ExecuteEvents.ExecuteHierarchy(currentOverGo, pointerEvent, ExecuteEvents.dropHandler);
                pointerEvent.eligibleForClick = false;
                pointerEvent.pointerPress = null;
                pointerEvent.rawPointerPress = null;
                if ((object)pointerEvent.pointerDrag != null && pointerEvent.dragging)
                    ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.endDragHandler);
                pointerEvent.dragging = false;
                pointerEvent.pointerDrag = null;
                if ((object)currentOverGo != (object)pointerEvent.pointerEnter)
                {
                    HandlePointerExitAndEnter(pointerEvent, null);
                    HandlePointerExitAndEnter(pointerEvent, currentOverGo);
                }
                m_InputPointerEvent = pointerEvent;
            }
        }

        // Original 0x060000fe; most recent native/custom cursor raycast target.
        protected GameObject GetCurrentFocusedGameObject() { return m_CurrentFocusedGameObject; }
        // Original 0x060000ff retains selection on required input types and clears the actual
        // selection only when transitioning from required to non-required input.
        void ILastInputTypeUpdateHandler.OnLastInputTypeUpdate(BaseEventData eventData)
        {
            LastInputTypeUpdateEventData update = eventData as LastInputTypeUpdateEventData;
            if (DoesInputTypeRequireSelection((InputType)update.NewInputType)) m_doesLastInputTypeRequireSelection = true;
            else
            {
                bool previous = m_doesLastInputTypeRequireSelection;
                m_doesLastInputTypeRequireSelection = false;
                if (previous) eventSystem.SetSelectedGameObject(null);
            }
        }
        // Original 0x06000100: enum value membership in the actual configured array; null faults.
        public bool DoesInputTypeRequireSelection(InputType inputType)
        { return Array.IndexOf(InputTypesThatEnforceSelectionControl, inputType) >= 0; }
        // Original 0x06000101/0x102 publish cached visibility only after Unity's cursor setter.
        public void SetHardwareCursorVisibility(bool on) { Cursor.visible = on; m_hardwareCursorIsVisible = on; }
        public bool GetHardwareCursorVisibility() { return m_hardwareCursorIsVisible; }
        // Original 0x06000103 generic constraint is the genuine marker interface, not BaseInputMonitor.
        public TInputMonitor GetInputMonitor<TInputMonitor>() where TInputMonitor : class, IBaseInputMonitor
        {
            for (int i = 0; i < m_inputMonitors.Length; ++i)
            {
                TInputMonitor monitor = m_inputMonitors[i] as TInputMonitor;
                if ((object)monitor != null) return monitor;
            }
            return null;
        }
        // Original 0x06000104 publishes the live list before getter callbacks and retains it after
        // partial failures. Array length is captured once; each monitor comes from the live field.
        private IReadOnlyList<ICurrentBaseInputBindingsProvider> GetCachedCurrentBaseInputBindingsProviders()
        {
            if (m_currentBaseInputBindingsProviders == null)
            {
                int count = m_inputMonitors.Length;
                m_currentBaseInputBindingsProviders = new List<ICurrentBaseInputBindingsProvider>(count);
                for (int i = 0; i < count; ++i)
                    m_currentBaseInputBindingsProviders.Add(m_inputMonitors[i].CurrentBaseInputBindingsProvider);
            }
            return m_currentBaseInputBindingsProviders;
        }
        // Original 0x06000105 implicit public ctor. Both original data slices prove 10f/0.5f;
        // polling3f, Vector2.zero and hardware visibility true precede PointerInputModule's ctor.
    }
}
