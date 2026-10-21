using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009cc; bodies translated from the 1.10.1 x86-64 binary.
    // An on screen marker for a waypoint target: icon, distance, height indicator and visibility.
    [Il2CppSetOption(Option.NullChecks, false)]
    [RequireComponent(typeof(RectTransform))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIWidgetSDTWaypointMarker : UIWidget
    {
        public readonly Bindable<Sprite> Icon = new Bindable<Sprite>();
        public readonly Bindable<float> DistanceToTarget = new Bindable<float>();
        public readonly Bindable<bool> ShowHeightIndicator = new Bindable<bool>();
        public readonly Bindable<bool> HeightIndicatorIsAbove = new Bindable<bool>();
        public readonly Bindable<bool> IsVisible = new Bindable<bool>();
        public readonly Bindable<bool> ShowDistanceIndicator = new Bindable<bool>();
        public readonly Bindable<bool> ShowArrowIndicator = new Bindable<bool>();
        public readonly Bindable<bool> IsBehindCamera = new Bindable<bool>();
        [SerializeField] private UnityEvent<Vector3> m_screenSpacePositionUpdated;
        public RectTransform RectTransform { get; private set; }
        private Transform m_target;
        private bool m_targetAssigned;
        private WaypointDefinition m_waypointDefinition;
        private Vector3 m_positionDampVelocity;
        private WaypointSettings m_settings;

        // 0x0600386e: the cast throws if this object is not on a RectTransform.
        private void Awake()
        {
            RectTransform = (RectTransform)transform;
        }

        // 0x0600386f: the settings for the container type are looked up in the definition (a
        // missing entry leaves them unset).
        public override void Setup(IUIWidgetParameters parameters)
        {
            base.Setup(parameters);
            UIWidgetSDTWaypointMarkerParameters data = parameters.GetAs<UIWidgetSDTWaypointMarkerParameters>();
            m_waypointDefinition = data.Definition;
            WaypointTarget target = data.Target;
            m_waypointDefinition.TryGetSettingsByContainerType(data.ContainerType, out m_settings);
            Icon.Value = m_settings.Icon;
            m_target = target.transform;
            m_targetAssigned = m_target != null;
            ShowDistanceIndicator.Value = m_settings.ShowDistanceIndicator;
        }

        // 0x06003870: forgets the target and resets the published state.
        public void Close()
        {
            Icon.Value = null;
            m_target = null;
            m_targetAssigned = false;
            IsVisible.Value = false;
            ShowHeightIndicator.Value = false;
            DistanceToTarget.Value = 0f;
            m_positionDampVelocity = Vector3.zero;
        }

        // 0x06003871: without a target the result is zero. Otherwise the target's screen point
        // gives IsBehindCamera (negative depth); a screen space camera canvas converts it back to
        // world space through the canvas camera. The converted position is published to the
        // screen space event, then optionally takes this marker's current Y.
        private Vector3 GetTargetPosition(Camera mainCamera, Canvas canvas, bool lockYAxis)
        {
            if (!m_targetAssigned)
                return Vector3.zero;
            Vector3 screenPoint = mainCamera.WorldToScreenPoint(m_target.position);
            IsBehindCamera.Value = screenPoint.z < 0f;
            Vector3 position = canvas.renderMode == RenderMode.ScreenSpaceCamera
                ? canvas.worldCamera.ScreenToWorldPoint(screenPoint)
                : screenPoint;
            m_screenSpacePositionUpdated.Invoke(position);
            if (lockYAxis)
                position.y = RectTransform.position.y;
            return position;
        }

        // 0x06003872: moves the marker (smoothed when smoothTimeSeconds is positive), flattens its
        // anchored depth while the target is in front, then publishes the distance, the visibility
        // window and the height indicator relative to the character's gravity.
        public void OnUpdate(Character character, Camera mainCamera, Canvas canvas, float smoothTimeSeconds, bool lockYAxis)
        {
            Vector3 targetPosition = GetTargetPosition(mainCamera, canvas, lockYAxis);
            RectTransform rectTransform = RectTransform;
            if (smoothTimeSeconds > 0f)
                targetPosition = Vector3.SmoothDamp(rectTransform.position, targetPosition, ref m_positionDampVelocity, smoothTimeSeconds);
            rectTransform.position = targetPosition;
            if (!IsBehindCamera.Value)
            {
                Vector3 anchoredPosition = RectTransform.anchoredPosition3D;
                anchoredPosition.z = 0f;
                RectTransform.anchoredPosition3D = anchoredPosition;
            }
            Vector3 offset = character.WorldPosition - m_target.position;
            DistanceToTarget.Value = offset.magnitude;
            bool visible = DistanceToTarget.Value >= m_settings.MinDrawDistance;
            if (visible && m_settings.MaxDrawDistance > 0f)
                visible = DistanceToTarget.Value <= m_settings.MaxDrawDistance;
            IsVisible.Value = visible;
            float height = Vector3.Dot(offset, character.GravityNormalised);
            ShowHeightIndicator.Value = Mathf.Abs(height) >= m_settings.ShowHeightIndicatorThreshold;
            if (ShowHeightIndicator.Value)
                HeightIndicatorIsAbove.Value = height > 0f;
        }

        // 0x06003873
        public bool DistanceIndicatorDisabled()
        {
            return !m_settings.ShowDistanceIndicator;
        }

        // 0x06003874: the field initialisers precede the UIWidget constructor.
        public UIWidgetSDTWaypointMarker() { }
    }
}
