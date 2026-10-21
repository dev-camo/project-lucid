using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008f8; bodies translated from the 1.10.1 x86-64 binary.
    // The ornament gallery: category tabs, the paged ornament grid, the info panel and the
    // spinning 3D ornament preview. Member order is kept: the compiler-generated iterator and
    // lambda names (d__53, d__55, d__56, b__61_0) derive from it.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerOrnamentGallery : UIContainer
    {
        [SerializeField] private UIContainerNavigation m_containerNavigation;
        [Header("Ornament Model Display")]
        [SerializeField] private AssetReferenceT<GameObject> m_lockedOrnament;
        [SerializeField] private float m_scaleDurationSeconds;
        [SerializeField] private AnimationCurve m_scaleCurve;
        [SerializeField] private float m_yRotationSpeed;
        [SerializeField] private float m_yRotationAtTouchExtent;
        [SerializeField] private float m_holdToSpinThreshold;
        [SerializeField] private OnScreenTouchZoneOrnament m_onScreenControlZone;
        [HashEnum(typeof(Strings))]
        [Header("Ornament Selection UI")]
        [SerializeField] private Strings m_nameLocked;
        [SerializeField] private int m_ornamentsPerRowNotchedDevice = 4;
        [SerializeField] private int m_ornamentsPerRowNoNotchDevice = 3;
        [SerializeField] private HLScrollRectSelection m_scrollRectSelection;
        [SerializeField] private float m_widescreenOffset = 0.35f;
        [SerializeField] private UIWidgetOrnamentEntryRowComposer m_ornamentEntryRowComposer;
        [SerializeField] private UIToggleGroup m_toggleGroup;
        [SerializeField] private UIWidgetCategoryToggle m_storyTab;
        [SerializeField] private UIWidgetCategoryToggle m_challengeTab;
        [SerializeField] private UIWidgetCategoryToggle m_achieverTab;
        [Header("Additional Info Panel")]
        [SerializeField] private GameObject m_infoPanel;
        [SerializeField] private UIWidgetButtonImageBase m_infoButton;
        [SerializeField] private GameObject m_infoGlyph;
        [SerializeField] private RewardUnlockStringPicker m_rewardTrackUnlockStringPicker;
        public readonly Bindable<Strings> Title = new Bindable<Strings>();
        public readonly Bindable<Strings> OrnamentName = new Bindable<Strings>();
        public readonly Bindable<string> InfoText = new Bindable<string>();
        public event Action OnNewOrnamentsCountChanged = delegate { };
        private readonly Dictionary<string, AsyncOperationHandle<GameObject>> m_ornamentHandleByGuid =
            new Dictionary<string, AsyncOperationHandle<GameObject>>();
        private OrnamentCategory m_currentCategory = OrnamentCategory.Achievement;
        private OrnamentDefinition m_currentOrnamentDefinition;
        private Ornament m_currentOrnament;
        private OrnamentManager m_ornamentManager;
        private AddressableManager m_addressableManager;
        private TimeManager m_timeManager;
        private Coroutine m_ornamentWaitHandleCoroutine;
        private Coroutine m_ornamentLoadingCoroutine;
        private int m_ornamentsPerRow;
        private float m_ornamentXOffset;
        private float m_rotationFromButtonInput;
        private DataManager m_dataManager;
        private SaveManager m_saveManager;

        // 0x060033d5 (0x66D670)
        private void Start()
        {
            m_infoButton.AddPointerUpListener(Action_ToggleInfoPanel);
        }

        // 0x060033d6 (0x66D6F0): wide screens (above 1.75) offset the ornament; above 1.9 the
        // notched-device row length applies.
        private void OnEnable()
        {
            m_timeManager = ProcessManager.GetSystem<TimeManager>(null, true);
            m_ornamentManager = ProcessManager.GetSystem<OrnamentManager>(null, true);
            m_addressableManager = ProcessManager.GetSystem<AddressableManager>(null, true);
            float aspect = (float)Screen.width / (float)Screen.height;
            m_ornamentsPerRow = aspect > 1.9f ? m_ornamentsPerRowNotchedDevice : m_ornamentsPerRowNoNotchDevice;
            m_ornamentXOffset = aspect > 1.75f ? m_widescreenOffset : 0f;
            m_dataManager = ProcessManager.GetSystem<DataManager>(null, true);
            m_saveManager = ProcessManager.GetSystem<SaveManager>(null, true);
            PopulateUI(m_currentCategory);
            m_onScreenControlZone.Initialise();
            Title.Value = Strings.GLOSSARY_24;
        }

        // 0x060033d7 (0x66E080): button input becomes an impulse on the ornament. While a drag is
        // active (DragAmount not NaN) and the rotator's impulse speed is below the hold-to-spin
        // threshold, the ornament is turned directly by the drag. Otherwise automation resumes and
        // any pending release impulse is applied.
        private void LateUpdate()
        {
            if (m_currentOrnament == null)
                return;
            if (m_rotationFromButtonInput > 0f)
            {
                m_currentOrnament.TransformRotator.CurrentImpulseSpeed = m_rotationFromButtonInput;
                m_rotationFromButtonInput = 0f;
                return;
            }
            if (!float.IsNaN(m_onScreenControlZone.DragAmount)
                && m_holdToSpinThreshold > m_currentOrnament.TransformRotator.CurrentImpulseSpeed)
            {
                SmoothedTransformRotator rotator = m_currentOrnament.TransformRotator;
                rotator.ToggleAutomation(false);
                float angle = m_yRotationAtTouchExtent * 2f * -m_onScreenControlZone.DragAmount;
                Transform ornamentTransform = m_currentOrnament.TransformRotator.CachedTransform;
                Quaternion rotation = ornamentTransform.rotation * Quaternion.AngleAxis(angle, Vector3.up);
                ornamentTransform.rotation = rotation;
                float yaw = rotation.eulerAngles.y;
                if (m_currentOrnament.TransformRotator.PingPongWhenIdle
                    && Mathf.Abs(yaw) > m_currentOrnament.TransformRotator.PingPongToDegrees)
                    return;
                m_currentOrnament.TransformRotator.UpdateRotation(
                    Quaternion.Euler(Vector3.up * yaw), m_timeManager.GetDeltaTime(TimeCategory.System));
            }
            else
            {
                m_currentOrnament.TransformRotator.ToggleAutomation(true);
                float impulse = m_onScreenControlZone.ConsumeDragReleaseImpulse();
                if (impulse != 0f)
                    m_currentOrnament.TransformRotator.ApplyRotationImpulse(impulse);
            }
        }

        // 0x060033d8 (0x66E3F0)
        public void Action_RotateOrnament(float speed)
        {
            m_rotationFromButtonInput = speed;
        }

        // 0x060033d9 (0x66E400): destroys the preview, releases every cached ornament handle and
        // requests a save.
        private void OnDestroy()
        {
            if (m_currentOrnament != null)
            {
                Destroy(m_currentOrnament.gameObject);
                m_currentOrnament = null;
            }
            foreach (var (_, handle) in m_ornamentHandleByGuid)
                m_addressableManager.ReleaseHandle(handle);
            m_saveManager.RequestSave();
        }

        // 0x060033da (0x66D900): groups the category's ornaments into rows of m_ornamentsPerRow. With
        // no current ornament, the first one becomes current; the current ornament is the default
        // selected toggle and fills the info panel.
        private void PopulateUI(OrnamentCategory activeCategory)
        {
            m_ornamentEntryRowComposer.Clear();
            m_currentCategory = activeCategory;
            List<UIWidgetOrnamentEntryRowParameters> rowData = new List<UIWidgetOrnamentEntryRowParameters>();
            List<UIWidgetOrnamentToggleParameters> toggleData = new List<UIWidgetOrnamentToggleParameters>();
            bool hasCurrentOrnament = m_currentOrnamentDefinition != null;
            int column = 0;
            foreach (var (identifier, status) in m_ornamentManager.OrnamentCollections[activeCategory])
            {
                OrnamentDefinition definition = m_dataManager.OrnamentDefinitions[identifier];
                if (column == m_ornamentsPerRow)
                {
                    rowData.Add(new UIWidgetOrnamentEntryRowParameters(toggleData));
                    toggleData.Clear();
                    column = 0;
                }
                if (!hasCurrentOrnament)
                {
                    m_currentOrnamentDefinition = definition;
                    hasCurrentOrnament = true;
                }
                bool isCurrent = m_currentOrnamentDefinition == definition;
                if (isCurrent)
                    SetInfoPanelStatus(definition, status.Unlocked);
                toggleData.Add(new UIWidgetOrnamentToggleParameters(
                    definition, status, OrnamentSelectionChanged, OrnamentHoverChanged, m_toggleGroup, isCurrent));
                column++;
            }
            if (toggleData.Count > 0)
            {
                rowData.Add(new UIWidgetOrnamentEntryRowParameters(toggleData));
                toggleData.Clear();
            }
            SetupCollectionTab(m_storyTab, activeCategory, OrnamentCategory.Story);
            SetupCollectionTab(m_challengeTab, activeCategory, OrnamentCategory.Challenge);
            SetupCollectionTab(m_achieverTab, activeCategory, OrnamentCategory.Achievement);
            m_ornamentEntryRowComposer.Setup(new UIWidgetOrnamentEntryRowComposerParameters(rowData));
            m_containerNavigation.UpdateChildWidgets();
        }

        // 0x060033db (0x66E8F0): counts the category's total and available ornaments.
        private void SetupCollectionTab(UIWidgetCategoryToggle tab, OrnamentCategory activeCategory, OrnamentCategory setupCategory)
        {
            int total = m_ornamentManager.OrnamentCollections[setupCategory].Count;
            int collected = 0;
            foreach (var (_, status) in m_ornamentManager.OrnamentCollections[setupCategory])
            {
                if (status.IsAvailable())
                    collected++;
            }
            tab.Setup(new UIWidgetCategoryToggleParameters(activeCategory == setupCategory, collected, total, setupCategory));
        }

        // 0x060033dc (0x66EC20): selecting an available, new ornament marks it seen in the save and
        // in the manager (a challenge reward that is not yet collected stays uncollected), then
        // refreshes the tabs' new-unlock markers.
        private void OrnamentSelectionChanged(OrnamentDefinition ornamentDefinition, bool available, bool isNew)
        {
            m_currentOrnamentDefinition = ornamentDefinition;
            if (available && isNew)
            {
                SaveDataGame saveData = m_saveManager.CurrentSave;
                SaveDataOrnament ornamentData = saveData.GetOrCreateOrnamentData(ornamentDefinition.GetGUID());
                bool collected = !saveData.TryGetRewardDataByRewardGUID(ornamentDefinition.GetGUID(), out SaveDataChallengeReward reward)
                    || reward.Collected;
                ornamentData.UnlockSeen = true;
                m_ornamentManager.OrnamentCollections[ornamentDefinition.OrnamentCategory][ornamentDefinition.OrnamentIdentifier] =
                    new OrnamentManager.OrnamentStatus(true, true, collected);
                m_storyTab.HasAnyNew(ornamentDefinition.OrnamentCategory);
                m_challengeTab.HasAnyNew(ornamentDefinition.OrnamentCategory);
                m_achieverTab.HasAnyNew(ornamentDefinition.OrnamentCategory);
                OnNewOrnamentsCountChanged();
            }
            SetInfoPanelStatus(ornamentDefinition, available);
            LoadNewOrnament(ornamentDefinition, available);
        }

        // 0x060033dd (0x66F100)
        private void OrnamentHoverChanged(RectTransform widgetTransform)
        {
            m_scrollRectSelection.Select(widgetTransform, true);
        }

        // 0x060033de (0x66E6D0): a locked ornament shows the locked name and the info panel; an
        // unlocked one shows its name, lore and the info glyph. The locked tip depends on the
        // category: a story act number, the challenge reward track's string, or the plain tip.
        private void SetInfoPanelStatus(OrnamentDefinition ornamentDefinition, bool unlocked)
        {
            OrnamentName.Value = unlocked ? ornamentDefinition.LocalisedName : m_nameLocked;
            m_infoPanel.SetActive(!unlocked);
            m_infoButton.SetInteractable(unlocked);
            m_infoGlyph.SetActive(unlocked);
            Strings text;
            if (!unlocked)
            {
                text = ornamentDefinition.LocalisedLockedTip;
                switch (ornamentDefinition.OrnamentCategory)
                {
                    case OrnamentCategory.Challenge:
                        if (m_rewardTrackUnlockStringPicker != null)
                        {
                            m_rewardTrackUnlockStringPicker.UpdateStringFromRewardGUID(ornamentDefinition.GetGUID());
                            text = m_rewardTrackUnlockStringPicker.StringID.Value;
                        }
                        break;
                    case OrnamentCategory.Story:
                        InfoText.Value = string.Format(StringTable.GetString(ornamentDefinition.LocalisedLockedTip), ornamentDefinition.ActNumber);
                        return;
                }
            }
            else
            {
                text = ornamentDefinition.LocalisedUnlockedLore;
            }
            InfoText.Value = StringTable.GetString(text);
        }

        // 0x060033df (0x66EE30): ornament prefabs are cached by the definition's GUID, locked or not.
        // A cached handle still loading is waited on; a finished one shows its prefab at once.
        private void LoadNewOrnament(OrnamentDefinition ornamentDefinition, bool unlocked)
        {
            AddressableManager addressableManager = ProcessManager.GetSystem<AddressableManager>(null, true);
            AssetReferenceT<GameObject> ornamentReference = unlocked ? ornamentDefinition.Ornament : m_lockedOrnament;
            if (m_ornamentHandleByGuid.TryGetValue(ornamentDefinition.GetGUID(), out AsyncOperationHandle<GameObject> handle))
            {
                if (handle.IsDone && handle.IsValid())
                {
                    OnOrnamentLoaded(handle.Result);
                }
                else
                {
                    this.SafeStopCoroutine(ref m_ornamentWaitHandleCoroutine);
                    if (gameObject.activeInHierarchy)
                        m_ornamentWaitHandleCoroutine = StartCoroutine(WaitForOrnamentHandleLoadOrnament(handle));
                }
            }
            else
            {
                m_ornamentHandleByGuid[ornamentDefinition.GetGUID()] =
                    addressableManager.LoadAsset<GameObject>(ornamentReference, OnOrnamentLoaded);
            }
        }

        // 0x060033e0 (0x66F200)
        private IEnumerator WaitForOrnamentHandleLoadOrnament(AsyncOperationHandle<GameObject> handle)
        {
            yield return handle;
            OnOrnamentLoaded(handle.Result);
            m_ornamentWaitHandleCoroutine = null;
        }

        // 0x060033e1 (0x66F120)
        private void OnOrnamentLoaded(GameObject prefab)
        {
            this.SafeStopCoroutine(ref m_ornamentLoadingCoroutine);
            if (gameObject.activeInHierarchy)
                m_ornamentLoadingCoroutine = StartCoroutine(InstantiateOrnament(prefab));
        }

        // 0x060033e2 (0x66F2B0): shrinks away the previous ornament, then places the new one
        // hidden at zero scale and scales it up.
        private IEnumerator InstantiateOrnament(GameObject prefab)
        {
            if (m_currentOrnament != null)
            {
                yield return ScaleWithCurve(false);
                Destroy(m_currentOrnament.gameObject);
                m_currentOrnament = null;
            }
            m_currentOrnament = Instantiate(prefab).GetComponent<Ornament>();
            Transform ornamentTransform = m_currentOrnament.transform;
            ornamentTransform.position = new Vector3(m_ornamentXOffset, 0f, 0f);
            float yRotation = m_currentOrnament.TransformRotator.PingPongWhenIdle ? 0f : m_currentOrnament.YRotationOffset;
            ornamentTransform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
            ornamentTransform.localScale = Vector3.zero;
            m_currentOrnament.TransformRotator.enabled = true;
            m_currentOrnament.TransformRotator.SetIdleRotationSpeed(new Vector3(0f, m_yRotationSpeed, 0f));
            m_currentOrnament.gameObject.SetActive(false);
            Camera[] cameras = m_currentOrnament.gameObject.GetComponentsInChildren<Camera>();
            for (int i = 0; i < cameras.Length; i++)
                cameras[i].enabled = false;
            StartCoroutine(ScaleWithCurve(true));
            m_ornamentLoadingCoroutine = null;
        }

        // 0x060033e3 (0x66F350): scales the current ornament along the curve; scaling down
        // starts from the ornament's present x scale.
        private IEnumerator ScaleWithCurve(bool scaleUp)
        {
            m_currentOrnament.gameObject.SetActive(true);
            float timer = 0f;
            Transform ornamentTransform = m_currentOrnament.transform;
            float upperScaleBound = scaleUp ? 1f : ornamentTransform.localScale.x;
            while (timer < m_scaleDurationSeconds)
            {
                float progress = timer / m_scaleDurationSeconds;
                float curveValue = m_scaleCurve.Evaluate(scaleUp ? progress : 1f - progress);
                ornamentTransform.localScale = Vector3.one * (curveValue * upperScaleBound);
                timer += m_timeManager.GetDeltaTime(TimeCategory.System);
                yield return null;
            }
            ornamentTransform.localScale = Vector3.one * (scaleUp ? 1f : 0f);
        }

        // 0x060033e4 (0x66F3F0)
        public void Action_ToggleInfoPanel()
        {
            if (m_infoButton.IsInteractable)
                m_infoPanel.SetActive(!m_infoPanel.activeSelf);
        }

        // 0x060033e5 (0x66F440)
        public void Action_TabStory(bool active)
        {
            if (!active)
                return;
            m_currentOrnamentDefinition = null;
            PopulateUI(OrnamentCategory.Story);
        }

        // 0x060033e6 (0x66F480)
        public void Action_TabChallenges(bool active)
        {
            if (!active)
                return;
            m_currentOrnamentDefinition = null;
            PopulateUI(OrnamentCategory.Challenge);
        }

        // 0x060033e7 (0x66F4C0)
        public void Action_TabAchiever(bool active)
        {
            if (!active)
                return;
            m_currentOrnamentDefinition = null;
            PopulateUI(OrnamentCategory.Achievement);
        }

        // 0x060033e8 (0x66F500)
        public UIContainerOrnamentGallery() { }
    }
}
