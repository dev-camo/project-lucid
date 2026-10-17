// Original HLUnityUI.Runtime research reconstruction.
// Source/native preservation only: provider algorithms, Unity/menu/bootstrap/native
// faults and full gameplay remain unqualified. Unity-compiler emission is unconfirmed;
// the comment on SelectionState covers the d__77 iterator ordinal.
// 0x020000f2 Hardlight.HLSelectable+ClickableEvent
// 0x060006eb System.Void .ctor() | arm64 0x1b79bbc..0x1b79bc4; x86_64 0x1b71530..0x1b71540
// 0x020000f3 Hardlight.HLSelectable+UnclickableEvent
// 0x060006ec System.Void .ctor() | arm64 0x1b79bc4..0x1b79bcc; x86_64 0x1b71540..0x1b71550
// 0x020000f4 Hardlight.HLSelectable+GraphicAndMaterialPair
// 0x060006ed System.Void .ctor(UnityEngine.UI.Graphic graphic, UnityEngine.Material material) | arm64 0x1b7a538..0x1b7a56c; x86_64 0x1b71e70..0x1b71ea0
// 0x020000f5 Hardlight.HLSelectable+SelectionState
// 0x020000f6 Hardlight.HLSelectable+<WaitForRootObjects>d__77
// 0x060006ee System.Void .ctor(System.Int32 <>1__state) | arm64 0x1b7a99c..0x1b7a9c8; x86_64 0x1b72320..0x1b72340
// 0x060006ef System.Void System.IDisposable.Dispose() | arm64 0x1b7c03c..0x1b7c040; x86_64 0x1b73790..0x1b737a0
// 0x060006f0 System.Boolean MoveNext() | arm64 0x1b7c040..0x1b7c144; x86_64 0x1b737a0..0x1b738a0
// 0x060006f1 System.Object System.Collections.Generic.IEnumerator<System.Object>.get_Current() | arm64 0x1b7c144..0x1b7c14c; x86_64 0x1b738a0..0x1b738b0
// 0x060006f2 System.Void System.Collections.IEnumerator.Reset() | arm64 0x1b7c14c..0x1b7c18c; x86_64 0x1b738b0..0x1b738f0
// 0x060006f3 System.Object System.Collections.IEnumerator.get_Current() | arm64 0x1b7c18c..0x1b7c194; x86_64 0x1b738f0..0x1b73900
// 0x020000f1 Hardlight.HLSelectable
// 0x060006ac System.Void .ctor() | arm64 0x1b77768..0x1b77904; x86_64 0x1b6ed60..0x1b6ef00
// 0x060006ad HLButtonHighlightCustomisations get_Customisations() | arm64 0x1b79bd4..0x1b79bdc; x86_64 0x1b71560..0x1b71570
// 0x060006ae Hardlight.HLSelectableInputListener get_InputListener() | arm64 0x1b79bdc..0x1b79be4; x86_64 0x1b71570..0x1b71580
// 0x060006af System.Boolean get_NeverSelectable() | arm64 0x1b79be4..0x1b79bec; x86_64 0x1b71580..0x1b71590
// 0x060006b0 System.String get_SceneName() | arm64 0x1b79bec..0x1b79bf4; x86_64 0x1b71590..0x1b715a0
// 0x060006b1 UnityEngine.Transform get_TargetTransform() | arm64 0x1b79bf4..0x1b79bfc; x86_64 0x1b715a0..0x1b715b0
// 0x060006b2 System.Void set_TargetTransform(UnityEngine.Transform value) | arm64 0x1b79bfc..0x1b79cf8; x86_64 0x1b715b0..0x1b716a0
// 0x060006b3 UnityEngine.Transform get_HighlightParentTransform() | arm64 0x1b79da4..0x1b79e58; x86_64 0x1b71750..0x1b717c0
// 0x060006b4 System.Void set_HighlightParentTransform(UnityEngine.Transform value) | arm64 0x1b79e58..0x1b79f54; x86_64 0x1b717c0..0x1b718b0
// 0x060006b5 Hardlight.HLNavigation get_Navigation() | arm64 0x1b79f54..0x1b79f68; x86_64 0x1b718b0..0x1b718d0
// 0x060006b6 System.Void set_Navigation(Hardlight.HLNavigation value) | arm64 0x1b79f68..0x1b7a070; x86_64 0x1b718d0..0x1b719e0
// 0x060006b7 System.Boolean get_Interactable() | arm64 0x1b7a070..0x1b7a078; x86_64 0x1b719e0..0x1b719f0
// 0x060006b8 System.Void set_Interactable(System.Boolean value) | arm64 0x1b751d4..0x1b752c0; x86_64 0x1b6c960..0x1b6ca50
// 0x060006b9 System.Boolean InteractionOverride() | arm64 0x1b7a078..0x1b7a080; x86_64 0x1b719f0..0x1b71a00
// 0x060006ba System.Boolean get_IsPointerInside() | arm64 0x1b7a080..0x1b7a088; x86_64 0x1b71a00..0x1b71a10
// 0x060006bb System.Void set_IsPointerInside(System.Boolean value) | arm64 0x1b7a088..0x1b7a090; x86_64 0x1b71a10..0x1b71a20
// 0x060006bc System.Boolean get_IsPointerDown() | arm64 0x1b7a090..0x1b7a098; x86_64 0x1b71a20..0x1b71a30
// 0x060006bd System.Void set_IsPointerDown(System.Boolean value) | arm64 0x1b7a098..0x1b7a0a0; x86_64 0x1b71a30..0x1b71a40
// 0x060006be Hardlight.HLSelectable+SelectionState get_CurrentSelectionState() | arm64 0x1b7a0a0..0x1b7a0a8; x86_64 0x1b71a40..0x1b71a50
// 0x060006bf Hardlight.HLSelectable+SelectionState get_PreviousSelectionState() | arm64 0x1b7a0a8..0x1b7a0b0; x86_64 0x1b71a50..0x1b71a60
// 0x060006c0 System.Boolean IsSelected() | arm64 0x1b7a0b0..0x1b7a388; x86_64 0x1b71a60..0x1b71ca0
// 0x060006c1 System.Void Awake() | arm64 0x1b763dc..0x1b7669c; x86_64 0x1b6db10..0x1b6dd60
// 0x060006c2 System.Void CacheGraphicAndMaterialPairs() | arm64 0x1b7a388..0x1b7a538; x86_64 0x1b71ca0..0x1b71e70
// 0x060006c3 System.Void OnCanvasGroupChanged() | arm64 0x1b7a56c..0x1b7a7a8; x86_64 0x1b71ea0..0x1b72130
// 0x060006c4 System.Boolean IsInteractable() | arm64 0x1b7a7a8..0x1b7a7c8; x86_64 0x1b72130..0x1b72150
// 0x060006c5 System.Void OnDidApplyAnimationProperties() | arm64 0x1b7a7c8..0x1b7a874; x86_64 0x1b72150..0x1b72200
// 0x060006c6 System.Void OnEnable() | arm64 0x1b7669c..0x1b76970; x86_64 0x1b6dd60..0x1b6e040
// 0x060006c7 System.Collections.IEnumerator WaitForRootObjects() | arm64 0x1b7a924..0x1b7a99c; x86_64 0x1b722b0..0x1b72320
// 0x060006c8 System.Void OnSetProperty() | arm64 0x1b79cf8..0x1b79da4; x86_64 0x1b716a0..0x1b71750
// 0x060006c9 System.Void OnInteractableChanged() | arm64 0x1b7a9c8..0x1b7a9f8; x86_64 0x1b72340..0x1b72370
// 0x060006ca System.Void OnDisable() | arm64 0x1b76970..0x1b76a90; x86_64 0x1b6e040..0x1b6e150
// 0x060006cb System.Void InstantClearState() | arm64 0x1b7a9f8..0x1b7aa0c; x86_64 0x1b72370..0x1b723a0
// 0x060006cc System.Void DoStateTransition(Hardlight.HLSelectable+SelectionState state, System.Boolean instant) | arm64 0x1b77138..0x1b77254; x86_64 0x1b6e7d0..0x1b6e910
// 0x060006cd Hardlight.HLSelectable FindSelectable(UnityEngine.Vector3 dir) | arm64 0x1b7aa0c..0x1b7aab4; x86_64 0x1b723a0..0x1b72430
// 0x060006ce UnityEngine.Vector3 GetPointOnRectEdge(UnityEngine.RectTransform rect, UnityEngine.Vector2 dir) | arm64 0x1b7aab4..0x1b7ac64; x86_64 0x1b72430..0x1b72590
// 0x060006cf System.Void Navigate(UnityEngine.EventSystems.AxisEventData eventData, Hardlight.HLSelectable selectedObject) | arm64 0x1b7ac64..0x1b7ae4c; x86_64 0x1b72590..0x1b72760
// 0x060006d0 System.Void SetCallToAction(System.Boolean active) | arm64 0x1b7ae4c..0x1b7af0c; x86_64 0x1b72760..0x1b727f0
// 0x060006d1 Hardlight.HLSelectable FindSelectableOnLeft() | arm64 0x1b7af0c..0x1b7af20; x86_64 0x1b727f0..0x1b72810
// 0x060006d2 Hardlight.HLSelectable FindSelectableOnRight() | arm64 0x1b7af20..0x1b7af34; x86_64 0x1b72810..0x1b72830
// 0x060006d3 Hardlight.HLSelectable FindSelectableOnUp() | arm64 0x1b7af34..0x1b7af48; x86_64 0x1b72830..0x1b72850
// 0x060006d4 Hardlight.HLSelectable FindSelectableOnDown() | arm64 0x1b7af48..0x1b7af5c; x86_64 0x1b72850..0x1b72870
// 0x060006d5 Hardlight.HLSelectable FindSelectable_Internal(UnityEngine.EventSystems.MoveDirection moveDirection) | arm64 0x1b7af5c..0x1b7b2f4; x86_64 0x1b72870..0x1b72ba0
// 0x060006d6 System.Void OnMove(UnityEngine.EventSystems.AxisEventData eventData) | arm64 0x1b7b2f4..0x1b7b33c; x86_64 0x1b72ba0..0x1b72be0
// 0x060006d7 System.Void UpdateHighlight() | arm64 0x1b7b33c..0x1b7b514; x86_64 0x1b72be0..0x1b72d30
// 0x060006d8 System.Void ShouldHighlight(System.Boolean highlightEnabled) | arm64 0x1b7b514..0x1b7b528; x86_64 0x1b72d30..0x1b72d50
// 0x060006d9 System.Boolean IsHighlighted(UnityEngine.EventSystems.BaseEventData eventData) | arm64 0x1b7b528..0x1b7b73c; x86_64 0x1b72d50..0x1b72f60
// 0x060006da System.Boolean IsPressed(UnityEngine.EventSystems.BaseEventData eventData) | arm64 0x1b7b73c..0x1b7b78c; x86_64 0x1b72f60..0x1b72fa0
// 0x060006db System.Boolean IsPressed() | arm64 0x1b7b78c..0x1b7b7dc; x86_64 0x1b72fa0..0x1b72fe0
// 0x060006dc System.Void UpdateSelectionState(UnityEngine.EventSystems.BaseEventData eventData) | arm64 0x1b7b7dc..0x1b7b868; x86_64 0x1b72fe0..0x1b73050
// 0x060006dd System.Void EvaluateAndTransitionToSelectionState(UnityEngine.EventSystems.BaseEventData eventData) | arm64 0x1b76c4c..0x1b76d94; x86_64 0x1b6e310..0x1b6e430
// 0x060006de System.Void InternalEvaluateAndTransitionToSelectionState(System.Boolean instant) | arm64 0x1b7a874..0x1b7a924; x86_64 0x1b72200..0x1b722b0
// 0x060006df System.Void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData) | arm64 0x1b75bc4..0x1b75c5c; x86_64 0x1b6d380..0x1b6d410
// 0x060006e0 System.Void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData) | arm64 0x1b75cd0..0x1b75cfc; x86_64 0x1b6d490..0x1b6d4c0
// 0x060006e1 System.Void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData) | arm64 0x1b75ea0..0x1b75f7c; x86_64 0x1b6d5f0..0x1b6d6a0
// 0x060006e2 System.Void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) | arm64 0x1b75fd4..0x1b760ac; x86_64 0x1b6d6f0..0x1b6d7a0
// 0x060006e3 System.Void OnSelect(UnityEngine.EventSystems.BaseEventData eventData) | arm64 0x1b7b868..0x1b7b86c; x86_64 0x1b73050..0x1b73060
// 0x060006e4 System.Void OnDeselect(UnityEngine.EventSystems.BaseEventData eventData) | arm64 0x1b7b86c..0x1b7b89c; x86_64 0x1b73060..0x1b73090
// 0x060006e5 System.Boolean AnyCornersVisible(System.Boolean isFTUEButton) | arm64 0x1b7b89c..0x1b7bbcc; x86_64 0x1b73090..0x1b73400
// 0x060006e6 System.Void Select() | arm64 0x1b7bbcc..0x1b7bce4; x86_64 0x1b73400..0x1b734f0
// 0x060006e7 System.Void Deselect() | arm64 0x1b7bce4..0x1b7be48; x86_64 0x1b734f0..0x1b735f0
// 0x060006e8 System.Void SetNavigationAction(UnityEngine.Events.UnityAction addToEvent) | arm64 0x1b7be48..0x1b7be68; x86_64 0x1b735f0..0x1b73610
// 0x060006e9 System.Void OnDestroy() | arm64 0x1b7be68..0x1b7bf78; x86_64 0x1b73610..0x1b73700
// 0x060006ea System.Void Action_Select() | arm64 0x1b7bf78..0x1b7c03c; x86_64 0x1b73700..0x1b73790
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Hardlight
{
    [SelectionBase]
    [ExecuteInEditMode]
    [AddComponentMenu("Hardlight UI/HLSelectable", 70)]
    [DisallowMultipleComponent]
    public class HLSelectable : SelectableBase
    {
        [Serializable]
        public class ClickableEvent : UnityEvent
        {
            public ClickableEvent() { }
        }

        [Serializable]
        public class UnclickableEvent : UnityEvent
        {
            public UnclickableEvent() { }
        }

        public struct GraphicAndMaterialPair
        {
            public Graphic cachedGraphic;
            public Material cachedStartMaterial;

            public GraphicAndMaterialPair(Graphic graphic, Material material)
            {
                cachedGraphic = graphic;
                cachedStartMaterial = material;
            }
        }

        public static FastAction<HLAudioTypes> OnNavigateTowardsSfx;
        [SerializeField] protected Transform m_targetTransform;
        [SerializeField] protected GameObject m_selectedAndHighlightObject;
        [SerializeField] protected Transform m_highlightParentTransform;
        [SerializeField] private HLButtonHighlightCustomisations m_customisations;
        [SerializeField] protected HLNavigation m_navigation;
        [SerializeField]
        [Tooltip("If you explicitly define navigation, but the object is off or null this will automatically find another.")]
        protected bool m_autoNavigateIfExplicitSelectionIsNotValid;
        [Tooltip("Can the Selectable be interacted with?")]
        [SerializeField] protected bool m_interactable = true;
        [SerializeField]
        [Tooltip("This event is invoked when the button becomes interactable.")]
        protected ClickableEvent m_onInteractable = new ClickableEvent();
        [SerializeField]
        [Tooltip("This event is invoked when the button becomes uninteractable.")]
        protected UnclickableEvent m_onUninteractable = new UnclickableEvent();
        [SerializeField]
        [FormerlySerializedAs("m_onNavigationEvent")]
        [Tooltip("This event is invoked when the button is selected by navigation.")]
        protected UnityEvent m_onNavigateTowardEvent = new UnityEvent();
        [SerializeField]
        [Tooltip("This event is invoked when the the button is navigated away from - fires BEFORE deselection.")]
        protected UnityEvent m_onNavigateAwayEvent = new UnityEvent();
        [SerializeField]
        [Tooltip("This event is invoked when the the button is deselected.")]
        protected UnityEvent m_onDeselectEvent = new UnityEvent();
        [Tooltip("Set this on objects like the FTUE interaction blocker, or anything else you don't want the highlight system to select.")]
        [SerializeField] protected bool m_neverSelectable;
        [SerializeField] protected bool m_autoSelectOnEnable;
        [SerializeField] protected Material m_nonInteractiveMaterial;
        [SerializeField] protected Graphic[] m_graphicsToMaterialSwapWithInteractability;
        [SerializeField] protected HLSelectableInputListener m_inputListener;
        [SerializeField] protected bool m_pointerEnterShouldTriggerNavigationEvent;
        [SerializeField] protected bool m_pointerExitShouldTriggerNavigationEvent;
        [SerializeField] protected GameObject m_callToAction;
        public static FastAction<FastAction<bool>, Action<bool>> InitShouldHighlight;
        protected static FastAction<bool> m_onShouldHighlightChanged;
        protected bool m_highlightEnabled = true;
        protected readonly List<CanvasGroup> m_CanvasGroupCache = new List<CanvasGroup>();
        protected bool m_groupsAllowInteraction = true;
        protected SelectionState m_currentSelectionState;
        protected SelectionState m_previousSelectionState;
        protected List<GraphicAndMaterialPair> m_graphicAndMaterialPairs;
        protected string m_sceneName;
        protected HLSelectableController m_hlSelectableController;
        protected InputBridge m_inputBridge;
        private UIRuntimeConfiguration m_uiRuntimeConfiguration;

        protected HLSelectable() { }

        public HLButtonHighlightCustomisations Customisations { get { return m_customisations; } }
        public HLSelectableInputListener InputListener { get { return m_inputListener; } }
        public bool NeverSelectable { get { return m_neverSelectable; } }
        public string SceneName { get { return m_sceneName; } }
        public Transform TargetTransform
        {
            get { return m_targetTransform; }
            set { if (UIUtils.SetClass(ref m_targetTransform, value)) OnSetProperty(); }
        }
        public Transform HighlightParentTransform
        {
            get { return m_highlightParentTransform != null ? m_highlightParentTransform : transform; }
            set { if (UIUtils.SetClass(ref m_highlightParentTransform, value)) OnSetProperty(); }
        }
        public HLNavigation Navigation
        {
            get { return m_navigation; }
            set { if (UIUtils.SetStruct(ref m_navigation, value)) OnSetProperty(); }
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

        protected virtual bool InteractionOverride() { return true; }
        protected bool IsPointerInside { get; set; }
        protected bool IsPointerDown { get; set; }
        protected SelectionState CurrentSelectionState { get { return m_currentSelectionState; } }
        protected SelectionState PreviousSelectionState { get { return m_previousSelectionState; } }

        protected bool IsSelected()
        {
            if (m_uiRuntimeConfiguration == null)
            {
                if (ProcessManager.IsSystemNull<UIRuntimeConfiguration>()) return false;
                m_uiRuntimeConfiguration = ProcessManager.GetSystem<UIRuntimeConfiguration>();
            }
            if (m_uiRuntimeConfiguration.ShouldHighlightSelection)
                return m_hlSelectableController != null && m_hlSelectableController.IsCurrentSelection(this);
            return EventSystem.current != null && m_inputBridge != null &&
                m_inputBridge.InputModuleExists() && m_inputBridge.IsObjectSelected(gameObject);
        }

        protected override void Awake()
        {
            if (m_targetTransform == null) m_targetTransform = transform;
            if (m_inputListener == null) m_inputListener = GetComponent<HLSelectableInputListener>();
            if (ProcessManager.IsSystemValid<UIRuntimeConfiguration>())
            {
                m_uiRuntimeConfiguration = ProcessManager.GetSystem<UIRuntimeConfiguration>();
                m_inputBridge = m_uiRuntimeConfiguration.InputBridge;
                if (m_uiRuntimeConfiguration.ShouldHighlightSelection)
                    m_hlSelectableController = ProcessManager.GetSystem<HLSelectableController>();
                if (m_uiRuntimeConfiguration.ShouldAutoNavigateUISelection && Application.isPlaying &&
                    m_navigation.Mode != HLNavigation.NavMode.Explicit)
                    m_navigation.Mode = HLNavigation.NavMode.Automatic;
            }
            CacheGraphicAndMaterialPairs();
        }

        protected void CacheGraphicAndMaterialPairs()
        {
            if (m_graphicsToMaterialSwapWithInteractability != null &&
                m_graphicsToMaterialSwapWithInteractability.Length > 0)
            {
                m_graphicAndMaterialPairs = new List<GraphicAndMaterialPair>(m_graphicsToMaterialSwapWithInteractability.Length);
                for (int i = 0; i < m_graphicsToMaterialSwapWithInteractability.Length; i++)
                {
                    Graphic graphic = m_graphicsToMaterialSwapWithInteractability[i];
                    m_graphicAndMaterialPairs.Add(new GraphicAndMaterialPair(graphic, graphic.material));
                }
            }
        }

        protected override void OnCanvasGroupChanged()
        {
            bool groupsAllowInteraction = true;
            Transform current = transform;
            while (current != null)
            {
                current.GetComponents(m_CanvasGroupCache);
                bool stop = false;
                for (int i = 0; i < m_CanvasGroupCache.Count; i++)
                {
                    if (!m_CanvasGroupCache[i].interactable)
                    {
                        groupsAllowInteraction = false;
                        stop = true;
                    }
                    if (m_CanvasGroupCache[i].ignoreParentGroups) stop = true;
                }
                if (stop) break;
                current = current.parent;
            }
            if (m_groupsAllowInteraction != groupsAllowInteraction)
            {
                m_groupsAllowInteraction = groupsAllowInteraction;
                OnSetProperty();
            }
        }

        public override bool IsInteractable() { return m_groupsAllowInteraction && m_interactable; }
        protected override void OnDidApplyAnimationProperties() { InternalEvaluateAndTransitionToSelectionState(false); }

        protected override void OnEnable()
        {
            base.OnEnable();
            m_currentSelectionState = IsSelected() ? SelectionState.Highlighted : SelectionState.Normal;
            m_previousSelectionState = SelectionState.Normal;
            InternalEvaluateAndTransitionToSelectionState(true);
            m_onShouldHighlightChanged += ShouldHighlight;
            InitShouldHighlight.Invoke(m_onShouldHighlightChanged, ShouldHighlight);
            m_sceneName = gameObject.scene.name;
            ActiveSelectables.Add(this, m_sceneName);
            if (m_uiRuntimeConfiguration != null && m_uiRuntimeConfiguration.ShouldHighlightSelection && m_autoSelectOnEnable)
                m_hlSelectableController.SelectThis(this, false);
        }

        private IEnumerator WaitForRootObjects()
        {
            yield return null;
            m_sceneName = gameObject.scene.name;
            ActiveSelectables.Add(this, m_sceneName);
        }

        private void OnSetProperty() { InternalEvaluateAndTransitionToSelectionState(false); }
        protected virtual void OnInteractableChanged()
        {
            if (m_interactable) m_onInteractable.Invoke();
            else m_onUninteractable.Invoke();
        }

        protected override void OnDisable()
        {
            ActiveSelectables.Remove(this, m_sceneName);
            InstantClearState();
            m_onShouldHighlightChanged -= ShouldHighlight;
            base.OnDisable();
        }

        // Ordinal 77 (<WaitForRootObjects>d__77, 0x020000f6) needs one nested type
        // after WaitForRootObjects. In nested TypeDef order SelectionState (0x020000f5)
        // is last, so it is that one; its exact slot after the iterator is inferred.
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
            InternalEvaluateAndTransitionToSelectionState(false);
        }

        protected virtual void DoStateTransition(SelectionState state, bool instant)
        {
            if (m_graphicAndMaterialPairs != null && m_graphicAndMaterialPairs.Count != 0)
            {
                if (state == SelectionState.Disabled)
                {
                    for (int i = 0; i < m_graphicAndMaterialPairs.Count; i++)
                        m_graphicAndMaterialPairs[i].cachedGraphic.material = m_nonInteractiveMaterial;
                }
                else
                {
                    for (int i = 0; i < m_graphicAndMaterialPairs.Count; i++)
                        m_graphicAndMaterialPairs[i].cachedGraphic.material = m_graphicAndMaterialPairs[i].cachedStartMaterial;
                }
            }
        }

        public HLSelectable FindSelectable(Vector3 dir)
        {
            return HLSelectableController.FindSelectable(dir, transform, this, m_sceneName);
        }

        public static Vector3 GetPointOnRectEdge(RectTransform rect, Vector2 dir)
        {
            if (rect == null) return Vector3.zero;
            if (dir != Vector2.zero) dir /= Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
            return rect.rect.center + Vector2.Scale(rect.rect.size, dir * 0.5f);
        }

        private void Navigate(AxisEventData eventData, HLSelectable selectedObject)
        {
            if (selectedObject == null || !selectedObject.IsActive()) return;
            if (m_uiRuntimeConfiguration.ShouldHighlightSelection)
            {
                selectedObject.m_onNavigateTowardEvent.Invoke();
                OnNavigateTowardsSfx.Invoke(ProcessManager.GetSystem<UIRuntimeConfiguration>().NavigateTowardsSound);
                m_onNavigateAwayEvent.Invoke();
                m_hlSelectableController.SelectThis(selectedObject, false);
            }
            else m_inputBridge.SetSelectedGameObjectWithMemory(selectedObject.gameObject, eventData);
        }

        public void SetCallToAction(bool active)
        {
            if (m_callToAction == null) return;
            m_callToAction.SetActive(active);
        }

        public virtual HLSelectable FindSelectableOnLeft() { return FindSelectable_Internal(MoveDirection.Left); }
        public virtual HLSelectable FindSelectableOnRight() { return FindSelectable_Internal(MoveDirection.Right); }
        public virtual HLSelectable FindSelectableOnUp() { return FindSelectable_Internal(MoveDirection.Up); }
        public virtual HLSelectable FindSelectableOnDown() { return FindSelectable_Internal(MoveDirection.Down); }

        public virtual HLSelectable FindSelectable_Internal(MoveDirection moveDirection)
        {
            Vector3 dir = Vector3.up;
            HLSelectable selectedObject = m_navigation.SelectOnUp;
            HLNavigation.NavMode mode = HLNavigation.NavMode.Vertical;
            switch (moveDirection)
            {
                case MoveDirection.Left:
                    dir = Vector3.left; selectedObject = m_navigation.SelectOnLeft; mode = HLNavigation.NavMode.Horizontal; break;
                case MoveDirection.Right:
                    dir = Vector3.right; selectedObject = m_navigation.SelectOnRight; mode = HLNavigation.NavMode.Horizontal; break;
                case MoveDirection.Down:
                    dir = Vector3.down; selectedObject = m_navigation.SelectOnDown; break;
            }
            if (m_navigation.Mode == HLNavigation.NavMode.Explicit)
            {
                if (m_uiRuntimeConfiguration.ShouldHighlightSelection && m_autoNavigateIfExplicitSelectionIsNotValid &&
                    !HLSelectableController.IsValidSelectableForNavigation(selectedObject))
                    return FindSelectable(transform.rotation * dir);
                return selectedObject;
            }
            if ((m_uiRuntimeConfiguration != null && !m_uiRuntimeConfiguration.ShouldHighlightSelection) ||
                (m_navigation.Mode & mode) == 0) return null;
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
                if (show && m_hlSelectableController != null) m_hlSelectableController.HighlightThis(this);
            }
            else UIUtils.SetActive(m_selectedAndHighlightObject, show);
        }

        protected virtual void ShouldHighlight(bool highlightEnabled)
        {
            m_highlightEnabled = highlightEnabled;
            UpdateHighlight();
        }

        protected bool IsHighlighted(BaseEventData eventData)
        {
            if (!IsActive()) return false;
            PointerEventData pointer = eventData as PointerEventData;
            if (pointer == null) return IsPointerInside;
            bool downOutside = IsPointerDown && !IsPointerInside && pointer.pointerPress == gameObject;
            bool upInsideOwned = !IsPointerDown && IsPointerInside && pointer.pointerPress == gameObject;
            bool upInsideUnpressed = !IsPointerDown && IsPointerInside && pointer.pointerPress == null;
            bool highlighted = downOutside | upInsideOwned | upInsideUnpressed;
            if (m_uiRuntimeConfiguration.ShouldHighlightSelection)
                return highlighted & HLSelectableController.IsValidSelectableForNavigation(this);
            return highlighted;
        }

        [Obsolete("Is Pressed no longer requires eventData", false)]
        protected bool IsPressed(BaseEventData eventData) { return IsActive() && IsPointerInside && IsPointerDown; }
        protected bool IsPressed() { return IsActive() && IsPointerInside && IsPointerDown; }

        protected void UpdateSelectionState(BaseEventData eventData)
        {
            m_previousSelectionState = m_currentSelectionState;
            if (IsPressed()) m_currentSelectionState = SelectionState.Pressed;
            else if (IsHighlighted(eventData)) m_currentSelectionState = SelectionState.Highlighted;
            else m_currentSelectionState = IsSelected() ? SelectionState.Highlighted : SelectionState.Normal;
        }

        protected void EvaluateAndTransitionToSelectionState(BaseEventData eventData)
        {
            if (!IsActive()) return;
            UpdateSelectionState(eventData);
            InternalEvaluateAndTransitionToSelectionState(false);
        }

        private void InternalEvaluateAndTransitionToSelectionState(bool instant)
        {
            if (!IsActive() || IsInteractable())
            {
                if (m_currentSelectionState == SelectionState.Disabled)
                {
                    m_previousSelectionState = m_currentSelectionState;
                    m_currentSelectionState = SelectionState.Normal;
                }
            }
            else
            {
                m_previousSelectionState = m_currentSelectionState;
                m_currentSelectionState = SelectionState.Disabled;
            }
            UpdateHighlight();
            if (m_currentSelectionState != m_previousSelectionState)
                DoStateTransition(m_currentSelectionState, instant);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            IsPointerDown = true;
            if (IsInteractable()) m_inputBridge.SetSelectedGameObjectWithMemory(gameObject, eventData);
            EvaluateAndTransitionToSelectionState(eventData);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            IsPointerDown = false;
            EvaluateAndTransitionToSelectionState(eventData);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            EventSystem eventSystem = EventSystem.current;
            if (m_inputBridge.GetHardwareCursorVisibility())
            {
                IsPointerInside = true;
                EvaluateAndTransitionToSelectionState(eventData);
                if (m_pointerEnterShouldTriggerNavigationEvent) m_onNavigateTowardEvent.Invoke();
            }
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            EventSystem eventSystem = EventSystem.current;
            if (m_inputBridge.GetHardwareCursorVisibility())
            {
                IsPointerInside = false;
                EvaluateAndTransitionToSelectionState(eventData);
                if (m_pointerExitShouldTriggerNavigationEvent) m_onNavigateAwayEvent.Invoke();
            }
        }

        public override void OnSelect(BaseEventData eventData) { EvaluateAndTransitionToSelectionState(eventData); }
        public override void OnDeselect(BaseEventData eventData)
        {
            EvaluateAndTransitionToSelectionState(eventData);
            m_onDeselectEvent.Invoke();
        }

        public bool AnyCornersVisible(bool isFTUEButton)
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null) return true;
            Camera camera = ProcessManager.IsSystemValid<GUICameraManager>() ? GUICameraManager.Instance.MenuCamera : Camera.main;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            if (isFTUEButton)
            {
                for (int i = 0; i < corners.Length; i++)
                {
                    corners[i] = camera.ScreenToViewportPoint(corners[i]);
                    if (camera.rect.Contains(corners[i])) return true;
                }
            }
            else
            {
                for (int i = 0; i < corners.Length; i++)
                {
                    corners[i] = camera.WorldToViewportPoint(corners[i]);
                    if (camera.rect.Contains(corners[i])) return true;
                }
            }
            return false;
        }

        public virtual void Select()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem.alreadySelecting || m_inputBridge == null) return;
            m_inputBridge.SetSelectedGameObjectWithMemory(gameObject, null);
        }

        public virtual void Deselect()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.alreadySelecting || m_inputBridge == null) return;
            m_inputBridge.DeselectObjectIfCurrentlySelected(gameObject);
        }

        public void SetNavigationAction(UnityAction addToEvent) { m_onNavigateTowardEvent.AddListener(addToEvent); }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ActiveSelectables.Remove(this, m_sceneName);
            if (m_uiRuntimeConfiguration == null) return;
            if (m_uiRuntimeConfiguration.ShouldHighlightSelection && IsSelected())
                m_hlSelectableController.DeselectCurrentSelection();
        }

        public void Action_Select()
        {
            if (m_hlSelectableController == null) return;
            m_hlSelectableController.SelectThis(this, false);
        }
    }
}
