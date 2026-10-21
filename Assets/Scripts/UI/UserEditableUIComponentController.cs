using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 02000995; bodies translated from the 1.10.1 x86-64 binary.
    // Positions are normalised over the free space left inside the movement bounds
    // (0 = bounds minimum, 1 = bounds maximum); all geometry is in world space.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UserEditableUIComponentController : UserEditableUIBase
    {
        [Tooltip("ID for saving the settings for this component into the save file.")]
        [SerializeField]
        [Header("Editable component setup")]
        private string m_saveId;
        [SerializeField]
        [Tooltip("Bounds the component can be moved around in by the user in edit mode. Component must be fully inside the bounds.")]
        private RectTransform m_movementBoundsRectTransform;
        [SerializeField]
        [Tooltip("The component to be moved by the user. Must implement IUserEditableUIComponent.")]
        private MonoBehaviour m_editableUIComponent;
        [Range(0, 1)]
        [Tooltip("Minimum component to movement area width ratio the user can set.")]
        [SerializeField]
        private float m_scaleMin = 0.05f;
        [Range(0, 1)]
        [Tooltip("Maximum component to movement area width ratio the user can set.")]
        [SerializeField]
        private float m_scaleMax = 0.5f;
        [SerializeField]
        [Tooltip("Flips to flip horizontally if control sides are swapped.")]
        private Flip[] m_flips;
        [SerializeField]
        [Header("Unity events")]
        private UnityEvent m_onEditStart;
        [SerializeField]
        private UnityEvent m_onEditEnd;
        private SaveDataSettingsEditableUIComponent m_saveDataComponent;
        private SaveDataSettingsEditableUIComponent m_saveDataComponentBackupBeforeEdit;
        private Vector2 m_defaultPositionNormalised;
        private float m_defaultScale;
        private Vector2 m_currentPositionNormalised;
        private float m_currentScale;
        private float m_scaleAtPinchBegin;
        private bool m_pinchActive;
        private readonly Vector3[] m_temporaryCornersArray = new Vector3[4];
        private Rect m_movementRect;
        private Rect m_componentRect;
        private float m_movableWidth;
        private float m_movableHeight;
        private RectTransform m_componentRectTransform;
        private IUserEditableUIComponent m_editableUIComponentInterface;
        private Camera m_menuCamera;

        // 06003767 (1.10.1 RVA 0x697AA0)
        protected override void OnSaveManagerValid(SaveManager saveManager)
        {
            base.OnSaveManagerValid(saveManager);
            m_editableUIComponentInterface = m_editableUIComponent as IUserEditableUIComponent;
            m_menuCamera = GUICameraManager.Instance.MenuCamera;
            m_saveDataComponent = m_saveDataSettings.GetOrCreateEditableUIComponentData(m_saveId);
        }

        // 06003768 (RVA 0x697BC0): the base check and an identical one of its own both ship.
        protected override void OnValidate()
        {
            base.OnValidate();
            gameObject.CanValidate();
        }

        // 06003769 (RVA 0x697C00): drags the component with the touch and scales it with a pinch.
        protected override void DoUpdate()
        {
            Vector2 touch;
            if (!m_editableUIComponentInterface.GetTouchPosition(out touch)) return;

            // Component origin (touch minus half the component size) relative to the bounds, over the free space.
            Vector2 offset = touch - m_movementRect.position + m_componentRect.size * new Vector2(-0.5f, -0.5f);
            Vector2 position = new Vector2(offset.x / m_movableWidth, offset.y / m_movableHeight);
            float scale = m_currentScale;
            float pinchDistance;
            float initialPinchDistance;
            bool pinching = m_editableUIComponentInterface.GetPinchInfo(out pinchDistance, out initialPinchDistance);
            if (pinching)
            {
                float scaleAtPinchBegin;
                if (m_pinchActive)
                {
                    scaleAtPinchBegin = m_scaleAtPinchBegin;
                }
                else
                {
                    scaleAtPinchBegin = m_currentScale;
                    m_scaleAtPinchBegin = scaleAtPinchBegin;
                }
                scale = (float)(pinchDistance / initialPinchDistance) * scaleAtPinchBegin;
            }
            m_pinchActive = pinching;
            SetComponentNormalisedPositionAndScale(position, scale);
            m_saveDataComponent.UserSet = true;
            UpdateSavedSettings();
        }

        // 0600376a (RVA 0x6980B0): a flipped layout counts as user-set.
        private void UpdateSavedSettings()
        {
            m_saveDataComponent.Position = m_currentPositionNormalised;
            m_saveDataComponent.Scale = m_currentScale;
            if (m_isFlipped) m_saveDataComponent.UserSet = true;
        }

        // 0600376b (RVA 0x698110): the default placement is whatever the prefab currently shows, unless the
        // device layout configuration lists this component.
        protected override void DoInitialise()
        {
            RefreshRects();
            m_defaultScale = m_movementRect.width != 0f ? m_componentRect.width / m_movementRect.width : 0f;
            m_defaultPositionNormalised = new Vector2(
                m_movableWidth != 0f ? (float)(m_componentRect.x - m_movementRect.x) / m_movableWidth : 0f,
                m_movableHeight != 0f ? (float)(m_componentRect.y - m_movementRect.y) / m_movableHeight : 0f);
            TouchLayoutConfiguration.TouchControlDefault touchDefault;
            if (ProcessManager.GetSystem<DataManager>().TouchLayoutConfiguration.TryGetTouchControlDefault(m_saveId, out touchDefault))
            {
                m_defaultScale = touchDefault.Scale;
                m_defaultPositionNormalised = touchDefault.Position;
            }
            m_currentScale = m_defaultScale;
            if (m_saveDataComponent.UserSet)
                SetComponentNormalisedPositionAndScale(m_saveDataComponent.Position, m_saveDataComponent.Scale);
            else
                SetComponentNormalisedPositionAndScale(m_defaultPositionNormalised, m_defaultScale);
            UpdateFlipComponents();
        }

        // 0600376c (RVA 0x6982E0)
        private void RefreshRects()
        {
            m_componentRectTransform = m_editableUIComponentInterface.GetRectTransform();
            m_componentRect = RectTransformToRect(m_componentRectTransform);
            m_movementRect = RectTransformToRect(m_movementBoundsRectTransform);
            UpdateMovableSpace();
        }

        // 0600376d (RVA 0x6984B0): free space is the bounds minus the component, never negative.
        private void UpdateMovableSpace()
        {
            Vector2 space = m_movementRect.size - m_componentRect.size;
            m_movableWidth = Mathf.Max(space.x, 0f);
            m_movableHeight = Mathf.Max(space.y, 0f);
        }

        // 0600376e (RVA 0x698470): world-space rectangle from the bottom-left and top-right corners.
        private Rect RectTransformToRect(RectTransform rectTransform)
        {
            rectTransform.GetWorldCorners(m_temporaryCornersArray);
            return new Rect(m_temporaryCornersArray[0], m_temporaryCornersArray[2] - m_temporaryCornersArray[0]);
        }

        // 0600376f (RVA 0x6984E0): snapshots the saved settings so that Cancel can restore them.
        protected override void OnEditStart(in UIModernEvent uiModernEvent)
        {
            base.OnEditStart(in uiModernEvent);
            if (m_saveDataComponentBackupBeforeEdit != null)
                m_saveDataComponentBackupBeforeEdit.CopyFrom(m_saveDataComponent);
            else
                m_saveDataComponentBackupBeforeEdit = m_saveDataComponent.Clone();
            m_editableUIComponentInterface.SetEditing(true);
            m_onEditStart?.Invoke();
        }

        // 06003770 (RVA 0x6985F0)
        protected override void OnEditCancel(in UIModernEvent uiModernEvent)
        {
            m_saveDataComponent.CopyFrom(m_saveDataComponentBackupBeforeEdit);
            if (m_saveDataComponent.UserSet)
                SetComponentNormalisedPositionAndScale(m_saveDataComponent.Position, m_saveDataComponent.Scale);
            else
                SetComponentNormalisedPositionAndScale(m_defaultPositionNormalised, m_defaultScale);
            ExitEditMode();
        }

        // 06003771 (RVA 0x698410)
        private void UpdateFlipComponents()
        {
            if (m_flips == null) return;
            foreach (Flip flip in m_flips)
                flip.SetHorizontalFlip(m_isFlipped);
        }

        // 06003772 (RVA 0x698670)
        protected override void OnEditReset(in UIModernEvent uiMessage)
        {
            base.OnEditReset(in uiMessage);
            m_saveDataComponent.UserSet = false;
            UpdateFlipComponents();
            SetComponentNormalisedPositionAndScale(m_defaultPositionNormalised, m_defaultScale);
        }

        // 06003773 (RVA 0x6986F0): mirrors the horizontal position; the flip components follow.
        protected override void OnEditFlip(in UIModernEvent uiModernEvent)
        {
            base.OnEditFlip(in uiModernEvent);
            m_currentPositionNormalised.x = 1f - m_currentPositionNormalised.x;
            UpdateFlipComponents();
        }

        // 06003774 (RVA 0x698760)
        protected override void ExitEditMode()
        {
            base.ExitEditMode();
            m_editableUIComponentInterface.SetEditing(false);
            m_onEditEnd?.Invoke();
        }

        // 06003775 (RVA 0x697E30): clamps the scale to its limits and the position to the unit square,
        // then moves the component so that its bottom-left corner lands on the normalised position.
        private void SetComponentNormalisedPositionAndScale(Vector2 position, float scale)
        {
            RefreshRects();
            scale = Mathf.Clamp(scale, m_scaleMin, m_scaleMax);
            if (!Mathf.Approximately(m_currentScale, scale))
            {
                // Target width relative to the movement bounds, expressed as a multiplier of the current width.
                float ratio = (float)(m_movementRect.width * scale) / m_componentRect.width;
                m_componentRectTransform.localScale *= ratio;
                m_componentRect = RectTransformToRect(m_componentRectTransform);
                UpdateMovableSpace();
                m_currentScale = scale;
            }
            m_currentPositionNormalised = new Vector2(Mathf.Clamp01(position.x), Mathf.Clamp01(position.y));
            Vector2 movable = new Vector2(m_movableWidth, m_movableHeight);
            Vector2 bottomLeft = new Vector2(
                (float)(m_currentPositionNormalised.x * movable.x) + m_movementRect.x,
                (float)(m_currentPositionNormalised.y * movable.y) + m_movementRect.y);
            Vector3 worldPosition = m_componentRectTransform.position;
            Vector2 pivotOffset = new Vector2(
                m_componentRectTransform.pivot.x * m_componentRect.width,
                m_componentRectTransform.pivot.y * m_componentRect.height);
            m_componentRectTransform.position = new Vector3(pivotOffset.x + bottomLeft.x, pivotOffset.y + bottomLeft.y, worldPosition.z);
            m_editableUIComponentInterface.TransformUpdated();
        }

        // 06003776 (RVA 0x698830)
        public void UpdateAfterParentFlipped()
        {
            SetComponentNormalisedPositionAndScale(m_currentPositionNormalised, m_currentScale);
            UpdateSavedSettings();
        }

        // 06003777 (RVA 0x68CDE0)
        public void GetPositionInfo(ref StringBuilder stringBuilder)
        {
            stringBuilder.AppendLine(string.Format("{0} pos: {{{1}, {2}}} scale {3}",
                m_saveId, m_currentPositionNormalised.x, m_currentPositionNormalised.y, m_currentScale));
        }

        // 06003778 (RVA 0x6988A0)
        public UserEditableUIComponentController()
        {
        }
    }
}
