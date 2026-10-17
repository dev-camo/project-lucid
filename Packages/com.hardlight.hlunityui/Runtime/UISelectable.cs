// Original HLUnityUI.Runtime Hardlight.UISelectable 0200007a (MethodDefs
// 0x0600038e..d2) with nested UIEventInfo 0200007b, GraphicAndMaterialPair
// 0200007c, SelectionState 0200007d and <WaitForRootObjects>d__74 0200007e,
// translated from the x86-64 bodies. It is the event-table counterpart of
// HLSelectable: UI events, sounds and haptics come from m_events rather than
// per-event UnityEvent fields, and the selectables register themselves in the
// static per-scene tables used by UISelectableController.FindSelectable.
//
// Member order fixes the generated iterator name: WaitForRootObjects is member
// ordinal 74 only with two nested types ahead of it (auto-property backing
// fields count as members). The nested TypeDef order (UIEventInfo,
// GraphicAndMaterialPair, SelectionState) therefore places SelectionState
// after WaitForRootObjects; only its exact position after it is unrecoverable.
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hardlight
{
    [AddComponentMenu("Hardlight UI/UISelectable", 70)]
    [ExecuteInEditMode]
    [SelectionBase]
    [DisallowMultipleComponent]
    public class UISelectable : SelectableBase
    {
        [Serializable]
        public class UIEventInfo
        {
            [SerializeField]
            public UIEventType m_type;

            [HashEnum(typeof(HLAudioTypes))]
            [SerializeField]
            public HLAudioTypes m_sfxId;

            [SerializeField]
            [HashEnum(typeof(VibrationType))]
            public VibrationType m_haptic;

            [SerializeField]
            public UnityEvent m_onEvent;
        }

        protected struct GraphicAndMaterialPair
        {
            public Graphic cachedGraphic;
            public Material cachedStartMaterial;

            public GraphicAndMaterialPair(Graphic graphic, Material material)
            {
                cachedGraphic = graphic;
                cachedStartMaterial = material;
            }
        }

        public static Dictionary<UIEventType, FastAction<HLAudioTypes>> OnSfx = new Dictionary<UIEventType, FastAction<HLAudioTypes>>();

        [SerializeField]
        private List<UIEventInfo> m_events;

        [SerializeField]
        private Transform m_targetTransform;

        [SerializeField]
        private GameObject m_selectedAndHighlightObject;

        [SerializeField]
        private Transform m_highlightParentTransform;

        [SerializeField]
        private HLButtonHighlightCustomisations m_customisations;

        [SerializeField]
        private UINavigation m_navigation;

        [SerializeField]
        [Tooltip("Can the selectable be interacted with?")]
        private bool m_interactable = true;

        [Tooltip("Set this on objects like the FTUE interaction blocker, or anything else you don't want the highlight system to select.")]
        [SerializeField]
        private bool m_neverSelectable;

        [SerializeField]
        private bool m_autoSelectOnEnable;

        [SerializeField]
        private Material m_nonInteractiveMaterial;

        [SerializeField]
        private Graphic[] m_graphicsToMaterialSwapWithInteractability;

        [SerializeField]
        private UISelectableInputListener m_inputListener;

        protected static FastAction<bool> m_onShouldHighlightChanged;
        protected bool m_highlightEnabled = true;
        protected readonly List<CanvasGroup> m_canvasGroupCache = new List<CanvasGroup>();
        protected bool m_groupsAllowInteraction = true;
        protected SelectionState m_currentSelectionState;
        protected SelectionState m_previousSelectionState;
        protected List<GraphicAndMaterialPair> m_graphicAndMaterialPairs;
        protected string m_sceneName;
        // Unlike HLSelectable.Awake, no body of this type assigns the controller.
        protected UISelectableController m_selectableController;
        protected InputBridge m_inputBridge;
        private UIRuntimeConfiguration m_uiRuntimeConfiguration;
        private SystemRef<VibrationManager> m_vibrationManager;
        public static FastAction<FastAction<bool>, Action<bool>> InitShouldHighlight;
        private static readonly Dictionary<string, List<UISelectable>> s_selectables = new Dictionary<string, List<UISelectable>>();
        private static readonly List<UISelectable> s_allSelectables = new List<UISelectable>();

        public HLButtonHighlightCustomisations Customisations
        {
            get { return m_customisations; }
        }

        public UISelectableInputListener InputListener
        {
            get { return m_inputListener; }
        }

        public bool NeverSelectable
        {
            get { return m_neverSelectable; }
        }

        public string SceneName
        {
            get { return m_sceneName; }
        }

        protected UISelectable() { }

        public Transform TargetTransform
        {
            get { return m_targetTransform; }
            set
            {
                if (UIUtils.SetClass(ref m_targetTransform, value))
                {
                    OnSetProperty();
                }
            }
        }

        public Transform HighlightParentTransform
        {
            get { return m_highlightParentTransform != null ? m_highlightParentTransform : transform; }
            set
            {
                if (UIUtils.SetClass(ref m_highlightParentTransform, value))
                {
                    OnSetProperty();
                }
            }
        }

        public UINavigation Navigation
        {
            get { return m_navigation; }
            set
            {
                if (UIUtils.SetStruct(ref m_navigation, value))
                {
                    OnSetProperty();
                }
            }
        }

        public bool Interactable
        {
            get { return m_interactable; }
            set
            {
                if (m_interactable != value && InteractionOverride())
                {
                    m_interactable = value;
                    OnInteractableChanged();
                    OnSetProperty();
                }
            }
        }

        // Every selectable joins the global list once; a named scene list
        // accepts duplicates (0x1B54940).
        public static void Add(UISelectable selectable, string sceneName = null)
        {
            s_allSelectables.AddUnique(selectable);
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            if (!s_selectables.ContainsKey(sceneName))
            {
                s_selectables[sceneName] = new List<UISelectable>();
            }

            s_selectables[sceneName].Add(selectable);
        }

        public static void Remove(UISelectable selectable, string sceneName = null)
        {
            s_allSelectables.Remove(selectable);
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            if (s_selectables.ContainsKey(sceneName))
            {
                s_selectables[sceneName].Remove(selectable);
            }
        }

        // A blank name returns the global list; an unknown scene gets an empty
        // list added to the table (0x1B54CF0).
        public static IReadOnlyCollection<UISelectable> GetSelectables(string sceneName = null)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return s_allSelectables;
            }

            if (!s_selectables.ContainsKey(sceneName))
            {
                s_selectables[sceneName] = new List<UISelectable>();
            }

            return s_selectables[sceneName];
        }

        protected virtual bool InteractionOverride()
        {
            return true;
        }

        protected bool IsPointerInside { get; set; }

        protected bool IsPointerDown { get; set; }

        protected SelectionState CurrentSelectionState
        {
            get { return m_currentSelectionState; }
        }

        protected SelectionState PreviousSelectionState
        {
            get { return m_previousSelectionState; }
        }

        protected bool IsSelected()
        {
            if (m_uiRuntimeConfiguration == null)
            {
                if (ProcessManager.IsSystemNull<UIRuntimeConfiguration>())
                {
                    return false;
                }

                m_uiRuntimeConfiguration = ProcessManager.GetSystem<UIRuntimeConfiguration>();
            }

            if (m_uiRuntimeConfiguration.ShouldHighlightSelection)
            {
                return m_selectableController != null && m_selectableController.IsCurrentSelection(this);
            }

            return EventSystem.current != null
                && m_inputBridge != null
                && m_inputBridge.InputModuleExists()
                && m_inputBridge.IsObjectSelected(gameObject);
        }

        // UIBehaviour.Awake is not called.
        protected override void Awake()
        {
            if (m_targetTransform == null)
            {
                m_targetTransform = transform;
            }

            if (m_inputListener == null)
            {
                m_inputListener = GetComponent<UISelectableInputListener>();
            }

            if (ProcessManager.IsSystemValid<UIRuntimeConfiguration>())
            {
                m_uiRuntimeConfiguration = ProcessManager.GetSystem<UIRuntimeConfiguration>();
                m_inputBridge = m_uiRuntimeConfiguration.InputBridge;
                if (m_uiRuntimeConfiguration.ShouldAutoNavigateUISelection && Application.isPlaying
                    && m_navigation.Mode != UINavigation.NavMode.Explicit)
                {
                    m_navigation.Mode = UINavigation.NavMode.Automatic;
                }
            }

            CacheGraphicAndMaterialPairs();
            m_vibrationManager = ProcessManager.GetSystemRef<VibrationManager>();
        }

        protected void CacheGraphicAndMaterialPairs()
        {
            if (m_graphicsToMaterialSwapWithInteractability == null || m_graphicsToMaterialSwapWithInteractability.Length == 0)
            {
                return;
            }

            m_graphicAndMaterialPairs = new List<GraphicAndMaterialPair>(m_graphicsToMaterialSwapWithInteractability.Length);
            for (int i = 0; i < m_graphicsToMaterialSwapWithInteractability.Length; i++)
            {
                Graphic graphic = m_graphicsToMaterialSwapWithInteractability[i];
                m_graphicAndMaterialPairs.Add(new GraphicAndMaterialPair(graphic, graphic.material));
            }
        }

        protected override void OnCanvasGroupChanged()
        {
            bool groupsAllowInteraction = true;
            Transform current = transform;
            while (current != null)
            {
                current.GetComponents(m_canvasGroupCache);
                bool stop = false;
                for (int i = 0; i < m_canvasGroupCache.Count; i++)
                {
                    if (!m_canvasGroupCache[i].interactable)
                    {
                        groupsAllowInteraction = false;
                        stop = true;
                    }

                    if (m_canvasGroupCache[i].ignoreParentGroups)
                    {
                        stop = true;
                    }
                }

                if (stop)
                {
                    break;
                }

                current = current.parent;
            }

            if (groupsAllowInteraction != m_groupsAllowInteraction)
            {
                m_groupsAllowInteraction = groupsAllowInteraction;
                OnSetProperty();
            }
        }

        public override bool IsInteractable()
        {
            return m_groupsAllowInteraction && m_interactable;
        }

        protected override void OnDidApplyAnimationProperties()
        {
            OnSetProperty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            m_currentSelectionState = IsSelected() ? SelectionState.Highlighted : SelectionState.Normal;
            m_previousSelectionState = SelectionState.Normal;
            InternalEvaluateAndTransitionToSelectionState(true);
            m_onShouldHighlightChanged += ShouldHighlight;
            InitShouldHighlight.Invoke(m_onShouldHighlightChanged, ShouldHighlight);
            m_sceneName = gameObject.scene.name;
            Add(this, m_sceneName);
            // The retail body ends by testing m_uiRuntimeConfiguration != null
            // (0x1B527AC) and does nothing with the result; the statement it
            // guarded is not in the build.
        }

        // No body in this type starts it.
        private IEnumerator WaitForRootObjects()
        {
            yield return null;
            m_sceneName = gameObject.scene.name;
            Add(this, m_sceneName);
        }

        private void OnSetProperty()
        {
            InternalEvaluateAndTransitionToSelectionState(false);
        }

        protected virtual void OnInteractableChanged()
        {
            InvokeEvent(m_interactable ? UIEventType.OnInteractable : UIEventType.OnUninteractable);
        }

        protected override void OnDisable()
        {
            Remove(this, m_sceneName);
            InstantClearState();
            m_onShouldHighlightChanged -= ShouldHighlight;
            base.OnDisable();
        }

        protected enum SelectionState
        {
            Normal = 0,
            Highlighted = 1,
            Pressed = 2,
            Disabled = 3
        }

        protected virtual void InstantClearState()
        {
            IsPointerInside = false;
            IsPointerDown = false;
            Deselect();
        }

        protected virtual void DoStateTransition(SelectionState state, bool instant)
        {
            if (m_graphicAndMaterialPairs == null || m_graphicAndMaterialPairs.Count == 0)
            {
                return;
            }

            if (state == SelectionState.Disabled)
            {
                for (int i = 0; i < m_graphicAndMaterialPairs.Count; i++)
                {
                    m_graphicAndMaterialPairs[i].cachedGraphic.material = m_nonInteractiveMaterial;
                }
            }
            else
            {
                for (int i = 0; i < m_graphicAndMaterialPairs.Count; i++)
                {
                    m_graphicAndMaterialPairs[i].cachedGraphic.material = m_graphicAndMaterialPairs[i].cachedStartMaterial;
                }
            }
        }

        public UISelectable FindSelectable(Vector3 dir)
        {
            return UISelectableController.FindSelectable(dir, transform, this, m_sceneName);
        }

        public static Vector3 GetPointOnRectEdge(RectTransform rect, Vector2 dir)
        {
            if (rect == null)
            {
                return Vector3.zero;
            }

            if (dir != Vector2.zero)
            {
                dir /= Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
            }

            return rect.rect.center + Vector2.Scale(rect.rect.size, dir * 0.5f);
        }

        private void Navigate(AxisEventData eventData, UISelectable selectedObject)
        {
            if (selectedObject == null || !selectedObject.IsActive())
            {
                return;
            }

            // The retail body ends after the IsActive call (0x1B55BC0); nothing
            // that followed it is in the build.
        }

        public virtual UISelectable FindSelectableOnLeft()
        {
            return FindSelectable_Internal(MoveDirection.Left);
        }

        public virtual UISelectable FindSelectableOnRight()
        {
            return FindSelectable_Internal(MoveDirection.Right);
        }

        public virtual UISelectable FindSelectableOnUp()
        {
            return FindSelectable_Internal(MoveDirection.Up);
        }

        public virtual UISelectable FindSelectableOnDown()
        {
            return FindSelectable_Internal(MoveDirection.Down);
        }

        // Up and MoveDirection.None both search upwards on the vertical axis
        // (0x1B55CC0).
        public virtual UISelectable FindSelectable_Internal(MoveDirection moveDirection)
        {
            Vector3 dir = Vector3.up;
            UISelectable explicitSelection = m_navigation.SelectOnUp;
            UINavigation.NavMode axis = UINavigation.NavMode.Vertical;
            switch (moveDirection)
            {
                case MoveDirection.Left:
                    dir = Vector3.left;
                    explicitSelection = m_navigation.SelectOnLeft;
                    axis = UINavigation.NavMode.Horizontal;
                    break;
                case MoveDirection.Right:
                    dir = Vector3.right;
                    explicitSelection = m_navigation.SelectOnRight;
                    axis = UINavigation.NavMode.Horizontal;
                    break;
                case MoveDirection.Down:
                    dir = Vector3.down;
                    explicitSelection = m_navigation.SelectOnDown;
                    break;
            }

            if (m_navigation.Mode == UINavigation.NavMode.Explicit)
            {
                if (m_uiRuntimeConfiguration.ShouldHighlightSelection
                    && m_navigation.AutoNavigateIfExplicitSelectionIsNotValid
                    && !UISelectableController.IsValidSelectableForNavigation(explicitSelection))
                {
                    return FindSelectable(transform.rotation * dir);
                }

                return explicitSelection;
            }

            if ((m_uiRuntimeConfiguration != null && !m_uiRuntimeConfiguration.ShouldHighlightSelection)
                || (m_navigation.Mode & axis) == 0)
            {
                return null;
            }

            return FindSelectable(transform.rotation * dir);
        }

        public override void OnMove(AxisEventData eventData)
        {
            Navigate(eventData, FindSelectable_Internal(eventData.moveDir));
        }

        protected virtual void UpdateHighlight()
        {
            bool show = m_highlightEnabled && (m_currentSelectionState == SelectionState.Highlighted || IsSelected());
            if (m_uiRuntimeConfiguration != null && m_uiRuntimeConfiguration.ShouldHighlightSelection)
            {
                if (show && m_selectableController != null)
                {
                    m_selectableController.HighlightThis(this);
                }
            }
            else
            {
                UIUtils.SetActive(m_selectedAndHighlightObject, show);
            }
        }

        protected virtual void ShouldHighlight(bool highlightEnabled)
        {
            m_highlightEnabled = highlightEnabled;
            UpdateHighlight();
        }

        // Only pointer events consult the configuration, which must then
        // exist; other events report the pointer-inside flag (0x1B56430).
        protected bool IsHighlighted(BaseEventData eventData)
        {
            if (!IsActive())
            {
                return false;
            }

            PointerEventData pointerData = eventData as PointerEventData;
            if (pointerData == null)
            {
                return IsPointerInside;
            }

            bool highlighted = (IsPointerDown && !IsPointerInside && pointerData.pointerPress == gameObject)
                | (!IsPointerDown && IsPointerInside && pointerData.pointerPress == gameObject)
                | (!IsPointerDown && IsPointerInside && pointerData.pointerPress == null);
            if (m_uiRuntimeConfiguration.ShouldHighlightSelection)
            {
                return highlighted & UISelectableController.IsValidSelectableForNavigation(this);
            }

            return highlighted;
        }

        [Obsolete("Is Pressed no longer requires eventData", false)]
        protected bool IsPressed(BaseEventData eventData)
        {
            return IsPressed();
        }

        protected bool IsPressed()
        {
            return IsActive() && IsPointerInside && IsPointerDown;
        }

        protected void UpdateSelectionState(BaseEventData eventData)
        {
            m_previousSelectionState = m_currentSelectionState;
            if (IsPressed())
            {
                m_currentSelectionState = SelectionState.Pressed;
            }
            else if (IsHighlighted(eventData))
            {
                m_currentSelectionState = SelectionState.Highlighted;
            }
            else
            {
                m_currentSelectionState = IsSelected() ? SelectionState.Highlighted : SelectionState.Normal;
            }
        }

        protected void EvaluateAndTransitionToSelectionState(BaseEventData eventData)
        {
            if (!IsActive())
            {
                return;
            }

            UpdateSelectionState(eventData);
            InternalEvaluateAndTransitionToSelectionState(false);
        }

        // Disabled is entered only while active and non-interactable; otherwise
        // a Disabled state returns to Normal. The transition
        // fires whenever the stored states differ, even when this call changed
        // neither of them (0x1B557A3).
        private void InternalEvaluateAndTransitionToSelectionState(bool instant)
        {
            if (IsActive() && !IsInteractable())
            {
                m_previousSelectionState = m_currentSelectionState;
                m_currentSelectionState = SelectionState.Disabled;
            }
            else if (m_currentSelectionState == SelectionState.Disabled)
            {
                m_previousSelectionState = m_currentSelectionState;
                m_currentSelectionState = SelectionState.Normal;
            }

            UpdateHighlight();
            if (m_previousSelectionState != m_currentSelectionState)
            {
                DoStateTransition(m_currentSelectionState, instant);
            }
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            IsPointerDown = true;
            if (IsInteractable())
            {
                m_inputBridge.SetSelectedGameObjectWithMemory(gameObject, eventData);
            }

            EvaluateAndTransitionToSelectionState(eventData);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            IsPointerDown = false;
            EvaluateAndTransitionToSelectionState(eventData);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            if (!m_inputBridge.GetHardwareCursorVisibility())
            {
                return;
            }

            IsPointerInside = true;
            EvaluateAndTransitionToSelectionState(eventData);
            if (m_navigation.PointerEnterShouldTriggerNavigationEvent)
            {
                InvokeEvent(UIEventType.OnNavigateToward);
            }
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            // Read and discarded, unlike OnPointerEnter (0x1B51FD3).
            _ = EventSystem.current;
            if (!m_inputBridge.GetHardwareCursorVisibility())
            {
                return;
            }

            IsPointerInside = false;
            EvaluateAndTransitionToSelectionState(eventData);
            if (m_navigation.PointerExitShouldTriggerNavigationEvent)
            {
                InvokeEvent(UIEventType.OnNavigateAway);
            }
        }

        public override void OnSelect(BaseEventData eventData)
        {
            EvaluateAndTransitionToSelectionState(eventData);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            EvaluateAndTransitionToSelectionState(eventData);
            InvokeEvent(UIEventType.OnDeselect);
        }

        // Both branches read GetWorldCorners; isFTUEButton maps them with
        // ScreenToViewportPoint instead of WorldToViewportPoint before testing
        // camera.rect.
        public bool AnyCornersVisible(bool isFTUEButton)
        {
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform == null)
            {
                return true;
            }

            Camera camera = ProcessManager.IsSystemValid<GUICameraManager>() ? GUICameraManager.Instance.MenuCamera : Camera.main;
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            if (isFTUEButton)
            {
                for (int i = 0; i < corners.Length; i++)
                {
                    corners[i] = camera.ScreenToViewportPoint(corners[i]);
                    if (camera.rect.Contains(corners[i]))
                    {
                        return true;
                    }
                }
            }
            else
            {
                for (int i = 0; i < corners.Length; i++)
                {
                    corners[i] = camera.WorldToViewportPoint(corners[i]);
                    if (camera.rect.Contains(corners[i]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // EventSystem.current is dereferenced without a null check here, but
        // not in Deselect.
        public virtual void Select()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem.alreadySelecting || m_inputBridge == null)
            {
                return;
            }

            m_inputBridge.SetSelectedGameObjectWithMemory(gameObject, null);
        }

        public virtual void Deselect()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.alreadySelecting || m_inputBridge == null)
            {
                return;
            }

            m_inputBridge.DeselectObjectIfCurrentlySelected(gameObject);
        }

        public void SetNavigationAction(UnityAction addToEvent)
        {
            UIEventInfo eventInfo = GetEventInfo(UIEventType.OnNavigateToward, true);
            if (eventInfo.m_onEvent == null)
            {
                eventInfo.m_onEvent = new UnityEvent();
            }

            eventInfo.m_onEvent.AddListener(addToEvent);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            Remove(this, m_sceneName);
            if (m_uiRuntimeConfiguration != null && m_uiRuntimeConfiguration.ShouldHighlightSelection && IsSelected())
            {
                m_selectableController.RemoveHighlighterFromScene();
            }
        }

        public void Action_Select()
        {
            if (m_selectableController == null)
            {
                return;
            }

            m_selectableController.SelectThis(this);
        }

        // A missing entry is created and stored only on request; otherwise a
        // detached entry carrying just the type is returned (0x1B513D0).
        protected UIEventInfo GetEventInfo(UIEventType type, bool createIfMissing = false)
        {
            for (int i = 0; i < m_events.Count; i++)
            {
                if (m_events[i].m_type == type)
                {
                    return m_events[i];
                }
            }

            if (createIfMissing)
            {
                return SetEventHandler(type, null);
            }

            return new UIEventInfo { m_type = type };
        }

        protected UIEventInfo SetEventHandler(UIEventType type, UnityEvent eventHandler)
        {
            for (int i = 0; i < m_events.Count; i++)
            {
                UIEventInfo existing = m_events[i];
                if (existing.m_type == type)
                {
                    existing.m_onEvent = eventHandler;
                    return existing;
                }
            }

            UIEventInfo eventInfo = new UIEventInfo { m_type = type, m_onEvent = eventHandler };
            m_events.Add(eventInfo);
            return eventInfo;
        }

        // Haptic, then the entry's UnityEvent, then the per-type sound action
        // with the entry's sound id.
        protected void InvokeEvent(UIEventType type)
        {
            UIEventInfo eventInfo = GetEventInfo(type);
            m_vibrationManager.GetSafe()?.Vibrate(eventInfo.m_haptic);
            eventInfo.m_onEvent?.Invoke();
            if (OnSfx.TryGetValue(type, out FastAction<HLAudioTypes> sfx))
            {
                sfx.Invoke(eventInfo.m_sfxId);
            }
        }
    }
}
