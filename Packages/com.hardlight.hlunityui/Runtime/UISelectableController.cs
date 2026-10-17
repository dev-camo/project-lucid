// Original HLUnityUI.Runtime Hardlight.UISelectableController 0200007f
// (MethodDefs 0x060003db..fb) with its cached-lambda class <>c 02000080 and
// iterator <MoveToNewParent>d__50 02000081, translated from the x86-64 bodies.
// It moves the shared button highlighter between UISelectables and finds
// navigation targets in the static UISelectable scene tables. Member order
// keeps the generated names: MoveToNewParent is member 50,
// TryGetCachedSelectable 53, AppReloading 54 and the constructor that owns the
// field-initialiser lambdas 61.
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UISelectableController : SelectableControllerBase
    {
        private const int MaxTrackedSubmittedSelectables = 5;

        [SerializeField]
        private HLButtonHighlighter m_buttonHighlightObject;

        [SerializeField]
        private string m_buttonHighlightTransitionLayer;

        [SerializeField]
        private float m_buttonHighlightTransitionTime = 0.2f;

        [SerializeField]
        [Tooltip("When set to auto, the nav system will prefer buttons within the same scene, what weight should be given to same scene objects?")]
        private float m_sameSceneButtonWeighting = 3f;

        [HashEnum(typeof(EaseUtilities.EaseType))]
        [SerializeField]
        private EaseUtilities.EaseType m_movementEasingType;

        [Tooltip("If nothing is selected and you want to spawn the highlighter, where should it appear from?")]
        [SerializeField]
        private Transform m_selectableOriginPoint;

        [SerializeField]
        private bool m_forceInstantSelection;

        private Coroutine m_coroutine;
        private bool m_isButtonHighlightActive;
        private UISelectable m_currentlySelected;
        private HLButtonHighlighter m_cachedButtonHighlight;
        private InputBridge m_inputBridge;
        private static readonly List<string> s_exclusiveScenes = new List<string>();
        private bool m_systemActive;
        private bool m_systemEnabled;
        private UIInputType m_cachedLastInputType = UIInputType.Unsupported;
        private readonly List<UISelectable> m_recentlySubmittedSelectables = new List<UISelectable>();
        private readonly List<UISelectable> m_overrideList = new List<UISelectable>();
        private UISelectable m_lastKnownSelected;
        private bool m_setInstantTransition;
        private Vector3 m_recoveryScreenOrigin;
        public FastAction<UISelectable> OnSelect;
        public FastAction<UISelectable> OnDeselect;
        public Func<bool> BlockFindSelectable = () => false;
        public Func<bool> UseOverrideList = () => false;

        public float ButtonTransitionTime
        {
            get { return m_buttonHighlightTransitionTime; }
        }

        public List<UISelectable> OverrideList
        {
            get { return m_overrideList; }
        }

        public UISelectable CurrentlySelected
        {
            get { return m_currentlySelected; }
        }

        public float SameSceneButtonWeighting
        {
            get { return m_sameSceneButtonWeighting; }
        }

        public override bool ShouldBlockFindSelectable()
        {
            return BlockFindSelectable();
        }

        public override float GetButtonTransitionTime()
        {
            return m_buttonHighlightTransitionTime;
        }

        private void Start()
        {
            ProcessManager.RegisterSystem(this, null, true, false);
            m_inputBridge = ProcessManager.GetSystemSafe<UIRuntimeConfiguration>().InputBridge;
            InputBridge.OnUpdateLastInputType += OnUpdateLastInputType;
            m_recoveryScreenOrigin = new Vector3(0f, Screen.height, 0f);
            ActivateSystem();
        }

        private void OnDestroy()
        {
            ProcessManager.UnregisterSystem(this);
            InputBridge.OnUpdateLastInputType -= OnUpdateLastInputType;
            DeactivateSystem(true);
        }

        private void OnDisable()
        {
            m_systemEnabled = false;
        }

        private void SpawnSelectedIndicator()
        {
            if (m_isButtonHighlightActive)
            {
                return;
            }

            if (m_cachedButtonHighlight == null)
            {
                if (m_buttonHighlightObject != null)
                {
                    m_cachedButtonHighlight = Instantiate(m_buttonHighlightObject, m_selectableOriginPoint);
                }
            }
            else
            {
                m_cachedButtonHighlight.transform.SetParent(m_selectableOriginPoint, false);
                m_cachedButtonHighlight.gameObject.SetActive(true);
            }

            // Set even when there was no highlighter prefab to spawn.
            m_setInstantTransition = true;
            m_isButtonHighlightActive = true;
        }

        public override void RegisterExclusiveScene(string sceneName)
        {
            s_exclusiveScenes.AddUnique(sceneName);
        }

        public override void DeregisterExclusiveScene(string sceneName)
        {
            if (s_exclusiveScenes.Contains(sceneName))
            {
                s_exclusiveScenes.Remove(sceneName);
            }
        }

        // The edge point uses the direction in the origin's local space; the
        // search itself keeps the world direction.
        public static UISelectable FindSelectable(Vector3 dir, Transform origin, UISelectable caller, string sceneName)
        {
            Vector3 position = origin.TransformPoint(UISelectable.GetPointOnRectEdge(origin as RectTransform, Quaternion.Inverse(origin.rotation) * dir));
            return FindSelectable(dir, position, caller, sceneName);
        }

        // Scores are dot / sqrMagnitude, and only candidates strictly ahead
        // count. A caller outside an exclusive scene scales each score by the
        // same-scene weighting when the scenes match; the best score is stored
        // after that scaling (0x1B57720).
        public static UISelectable FindSelectable(Vector3 dir, Vector3 position, UISelectable caller, string sceneName)
        {
            IReadOnlyCollection<UISelectable> selectables = null;
            bool useOverrideList = false;
            float sameSceneWeighting = 1f;
            if (ProcessManager.IsSystemValid<UISelectableController>())
            {
                UISelectableController controller = ProcessManager.GetSystem<UISelectableController>();
                useOverrideList = controller.UseOverrideList();
                if (controller.BlockFindSelectable() && !useOverrideList)
                {
                    return null;
                }

                // Taken even when the override list is not used.
                selectables = controller.m_overrideList.AsReadOnly();
                sameSceneWeighting = controller.m_sameSceneButtonWeighting;
            }

            bool isExclusiveScene = false;
            string exclusiveScene = null;
            if (!string.IsNullOrEmpty(sceneName))
            {
                isExclusiveScene = s_exclusiveScenes.Contains(sceneName);
                if (isExclusiveScene)
                {
                    exclusiveScene = sceneName;
                }
            }

            if (!useOverrideList)
            {
                selectables = UISelectable.GetSelectables(exclusiveScene);
            }

            UISelectable bestPick = null;
            float maxScore = float.NegativeInfinity;
            foreach (UISelectable selectable in selectables)
            {
                if (selectable == caller || selectable == null || !IsValidSelectableForNavigation(selectable))
                {
                    continue;
                }

                RectTransform selectableRect = selectable.transform as RectTransform;
                Vector3 selectableCenter = selectableRect != null ? (Vector3)selectableRect.rect.center : Vector3.zero;
                Vector3 toSelectable = selectable.transform.TransformPoint(selectableCenter) - position;
                float dot = Vector3.Dot(dir, toSelectable);
                if (dot <= 0f)
                {
                    continue;
                }

                float score = dot / toSelectable.sqrMagnitude;
                if (caller != null && !isExclusiveScene)
                {
                    score *= caller.SceneName == selectable.SceneName ? sameSceneWeighting : 1f;
                }

                if (score > maxScore)
                {
                    maxScore = score;
                    bestPick = selectable;
                }
            }

            return bestPick;
        }

        public static bool IsValidSelectableForNavigation(UISelectable sel)
        {
            if (sel == null || !sel.IsInteractable() || sel.Navigation.Mode == UINavigation.NavMode.None
                || !sel.gameObject.activeInHierarchy || !sel.gameObject.activeSelf)
            {
                return false;
            }

            // The block delegate's result is passed as AnyCornersVisible's isFTUEButton.
            if (ProcessManager.IsSystemValid<UISelectableController>()
                && !sel.AnyCornersVisible(ProcessManager.GetSystem<UISelectableController>().BlockFindSelectable()))
            {
                return false;
            }

            return !sel.NeverSelectable && sel.enabled;
        }

        public bool IsCurrentSelection(UISelectable selectable)
        {
            return m_currentlySelected == selectable;
        }

        public void DeselectCurrentSelection()
        {
            m_lastKnownSelected = m_currentlySelected;
            if (m_currentlySelected == null)
            {
                return;
            }

            OnDeselect?.Invoke(m_currentlySelected);
            m_currentlySelected = null;
            m_inputBridge.DeselectObjectIfCurrentlySelected(m_lastKnownSelected.gameObject);
        }

        // While the system is disabled the selection is recorded without
        // validation; a non-UISelectable is stored as null (0x1B57F00).
        public override void SelectThis(SelectableBase selectable, bool instantTransition = false)
        {
            UISelectable uiSelectable = selectable as UISelectable;
            if (!m_systemEnabled)
            {
                m_inputBridge.SetSelectedGameObjectWithMemory(selectable.gameObject);
                m_currentlySelected = uiSelectable;
                return;
            }

            if (m_currentlySelected == selectable)
            {
                HighlightThis(uiSelectable);
                return;
            }

            if (!IsValidSelectableForNavigation(uiSelectable))
            {
                return;
            }

            DeselectCurrentSelection();
            m_currentlySelected = uiSelectable;
            m_setInstantTransition |= instantTransition | m_forceInstantSelection;
            OnSelect.Invoke(m_currentlySelected);
            m_inputBridge.SetSelectedGameObjectWithMemory(m_currentlySelected.gameObject);
        }

        public void HighlightThis(UISelectable selectable)
        {
            if (!m_systemEnabled || !m_isButtonHighlightActive || !IsValidSelectableForNavigation(selectable))
            {
                return;
            }

            this.SafeStopCoroutine(ref m_coroutine);
            m_coroutine = StartCoroutine(MoveToNewParent(selectable));
        }

        // Keeps the newest submissions last; m_lastKnownSelected becomes the
        // current selection, not the submitted one.
        public void TrackRecentlySubmittedSelectable(UISelectable selectable)
        {
            m_lastKnownSelected = m_currentlySelected;
            if (m_recentlySubmittedSelectables.Contains(selectable))
            {
                m_recentlySubmittedSelectables.Remove(selectable);
            }

            m_recentlySubmittedSelectables.Add(selectable);
            if (m_recentlySubmittedSelectables.Count > MaxTrackedSubmittedSelectables)
            {
                m_recentlySubmittedSelectables.RemoveAt(0);
            }
        }

        private IEnumerator MoveToNewParent(UISelectable selectable)
        {
            if (!m_systemEnabled || m_cachedButtonHighlight == null)
            {
                yield break;
            }

            m_cachedButtonHighlight.transform.SetParent(selectable.HighlightParentTransform, true);
            m_cachedButtonHighlight.SetButtonCanvasLayer(m_buttonHighlightTransitionLayer);
            float timer = 0f;
            Transform selectedIndicatorTransform = m_cachedButtonHighlight.transform;
            RectTransform cachedRect = selectedIndicatorTransform as RectTransform;
            if (m_setInstantTransition || m_lastKnownSelected == null
                || (m_lastKnownSelected != null && !IsValidSelectableForNavigation(m_lastKnownSelected)))
            {
                selectedIndicatorTransform.localScale = m_setInstantTransition ? Vector3.one : Vector3.zero;
                selectedIndicatorTransform.localRotation = Quaternion.identity;
                cachedRect.offsetMin = Vector2.zero;
                cachedRect.offsetMax = Vector2.zero;
                m_cachedButtonHighlight.ResetCanvasLayer();
            }

            Vector3 cachedScale = selectedIndicatorTransform.localScale;
            Quaternion cachedRot = selectedIndicatorTransform.localRotation;
            Vector2 cachedOffsetMin = cachedRect.offsetMin;
            Vector2 cachedOffsetMax = cachedRect.offsetMax;
            Vector3 cachedAnchoredPosition3D = cachedRect.anchoredPosition3D;
            m_cachedButtonHighlight.SetButtonState(selectable.Customisations, false);
            while (timer < m_buttonHighlightTransitionTime)
            {
                timer += Time.unscaledDeltaTime;
                float t = EaseUtilities.Ease(m_movementEasingType, timer / m_buttonHighlightTransitionTime);
                selectedIndicatorTransform.localScale = Vector3.Lerp(cachedScale, Vector3.one, t);
                selectedIndicatorTransform.localRotation = Quaternion.Slerp(cachedRot, Quaternion.identity, t);
                cachedRect.offsetMin = Vector2.Lerp(cachedOffsetMin, Vector2.zero, t);
                cachedRect.offsetMax = Vector2.Lerp(cachedOffsetMax, Vector2.zero, t);
                // Follow the live x and y while easing; z keeps its cached start.
                Vector3 currentPosition = cachedRect.anchoredPosition3D;
                cachedAnchoredPosition3D.x = currentPosition.x;
                cachedAnchoredPosition3D.y = currentPosition.y;
                cachedRect.anchoredPosition3D = Vector3.Lerp(cachedAnchoredPosition3D, Vector3.zero, t);
                yield return null;
            }

            m_setInstantTransition = false;
            m_cachedButtonHighlight.ResetCanvasLayer();
        }

        public override void RemoveHighlighterFromScene()
        {
            if (!m_systemEnabled || !m_isButtonHighlightActive)
            {
                return;
            }

            if (m_currentlySelected != null && IsValidSelectableForNavigation(m_currentlySelected))
            {
                return;
            }

            if (m_cachedButtonHighlight == null)
            {
                return;
            }

            m_cachedButtonHighlight.transform.SetParent(transform, true);
            m_cachedButtonHighlight.transform.localScale = Vector3.zero;
            this.SafeStopCoroutine(ref m_coroutine);
            m_cachedButtonHighlight.ReactivateBehaviours();
            FindAndSelectNewSelectable();
        }

        public void FindAndSelectNewSelectable()
        {
            SelectableBase selectable = TryGetCachedSelectable();
            m_setInstantTransition = true;
            if (selectable == null)
            {
                return;
            }

            SelectThis(selectable, true);
        }

        // Last known selection, then the newest valid submission, then a
        // downward search from the top-left of the screen (0x1B58680).
        public override SelectableBase TryGetCachedSelectable()
        {
            if (m_lastKnownSelected != null && IsValidSelectableForNavigation(m_lastKnownSelected))
            {
                return m_lastKnownSelected;
            }

            m_recentlySubmittedSelectables.RemoveAll(item => item == null);
            for (int i = m_recentlySubmittedSelectables.Count - 1; i >= 0; i--)
            {
                if (IsValidSelectableForNavigation(m_recentlySubmittedSelectables[i]))
                {
                    return m_recentlySubmittedSelectables[i];
                }
            }

            return FindSelectable(Vector3.down, m_recoveryScreenOrigin, null, null);
        }

        public void AppReloading()
        {
            this.SafeStopCoroutine(ref m_coroutine);
            BlockFindSelectable = () => false;
            DisableSystem();
        }

        private void OnUpdateLastInputType(UIInputType lastInputType)
        {
            if (!m_systemActive || m_cachedLastInputType == lastInputType)
            {
                return;
            }

            m_cachedLastInputType = lastInputType;
            if (!EnableSystemIfInputTypeIsValid(lastInputType) && m_systemEnabled)
            {
                DisableSystem();
            }
        }

        public override void ActivateSystem()
        {
            m_systemActive = true;
            EnableSystemIfInputTypeIsValid(m_inputBridge.GetLastInputType());
        }

        public override void DeactivateSystem(bool isBeingDestroyed)
        {
            if (!m_systemActive)
            {
                return;
            }

            DisableSystem(isBeingDestroyed);
            m_systemActive = false;
        }

        private void DisableSystem(bool isBeingDestroyed = false)
        {
            m_systemEnabled = false;
            this.SafeStopCoroutine(ref m_coroutine);
            if (m_cachedButtonHighlight != null && !isBeingDestroyed)
            {
                m_cachedButtonHighlight.gameObject.SetActive(false);
                m_cachedButtonHighlight.transform.SetParent(transform, true);
                m_isButtonHighlightActive = false;
            }

            m_inputBridge.SetHardwareCursorVisibility(true);
            if (m_currentlySelected == null)
            {
                return;
            }

            DeselectCurrentSelection();
        }

        private void EnableSystem()
        {
            m_systemEnabled = true;
            if (!m_isButtonHighlightActive)
            {
                SpawnSelectedIndicator();
            }

            FindAndSelectNewSelectable();
            m_inputBridge.SetHardwareCursorVisibility(false);
        }

        private bool EnableSystemIfInputTypeIsValid(UIInputType type)
        {
            bool requiresSelection = m_inputBridge.DoesInputTypeRequireSelection(type);
            if (requiresSelection && !m_systemEnabled)
            {
                EnableSystem();
            }

            return requiresSelection;
        }

        public UISelectableController() { }
    }
}
