using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200094f; bodies translated from the 1.10.1 x86-64 binary.
    // Waypoint markers on the screen: a marker outside an ellipse around its centre is pulled onto the ellipse edge.
    // The ellipse's height and radius depend on whether the last input type is one that shows the on-screen UI.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerWaypointMarkersScreenSpace : UIContainerWaypointMarkersBase
    {
        [SerializeField] private RectTransform m_ellipseCentre;
        [SerializeField] private float m_ellipseHeightForUIOn;
        [SerializeField] private Vector2 m_ellipseRadiusForUIOn;
        [SerializeField] private float m_ellipseHeightForUIOff;
        [SerializeField] private Vector2 m_ellipseRadiusForUIOff;
        [SerializeField] private List<UIInputType> m_uiInputTypesWhichToggleOn = new List<UIInputType>();
        private Vector2 m_ellipseRadius;

        // 0x06003584 (0x683110): the ellipse is set from the bridge's current input type before listening for changes.
        private void OnEnable()
        {
            OnInputTypeChanged(ProcessManager.GetSystemSafe<UIRuntimeConfiguration>(null, true).InputBridge.GetLastInputType());
            InputBridge.OnUpdateLastInputType += OnInputTypeChanged;
        }

        // 0x06003585 (0x683360)
        private void OnDisable()
        {
            InputBridge.OnUpdateLastInputType -= OnInputTypeChanged;
        }

        // 0x06003586 (0x6832C0): the radius and the centre's bottom offset follow the input type.
        private void OnInputTypeChanged(UIInputType inputType)
        {
            bool uiOn = m_uiInputTypesWhichToggleOn.Contains(inputType);
            m_ellipseRadius = uiOn ? m_ellipseRadiusForUIOn : m_ellipseRadiusForUIOff;
            Vector2 offsetMin = m_ellipseCentre.offsetMin;
            offsetMin.y = uiOn ? m_ellipseHeightForUIOn : m_ellipseHeightForUIOff;
            m_ellipseCentre.offsetMin = offsetMin;
        }

        // 0x06003587 (0x683430)
        protected override void DoUpdate(Character character, Camera mainCamera)
        {
            foreach (UIWidgetSDTWaypointMarker waypointMarker in m_waypointMarkers)
            {
                if (!waypointMarker.gameObject.activeInHierarchy)
                    continue;
                waypointMarker.OnUpdate(character, mainCamera, m_canvas, m_smoothTimeSeconds, false);
                if (waypointMarker.IsVisible.Value)
                    ClampMarkerToEllipse(waypointMarker);
            }
        }

        // 0x06003588 (0x6835D0): a marker behind the camera is mirrored through the centre onto the ellipse edge, with only
        // the arrow shown. Otherwise a marker outside the ellipse is pulled to the edge (arrow shown) and one inside keeps its
        // position (distance indicator shown unless the marker disables it). The position's depth is always 0.
        private void ClampMarkerToEllipse(UIWidgetSDTWaypointMarker waypointMarker)
        {
            Vector2 offset = waypointMarker.RectTransform.position - m_ellipseCentre.position;
            bool showDistanceIndicator;
            bool showArrowIndicator;
            if (waypointMarker.IsBehindCamera.Value)
            {
                Vector3 edgePoint = GetClosestPointOnEllipseEdge(-offset);
                waypointMarker.RectTransform.position = (Vector2)m_ellipseCentre.position + (Vector2)edgePoint; // depth reset to 0
                showDistanceIndicator = false;
                showArrowIndicator = true;
            }
            else
            {
                Vector3 edgePoint = GetClosestPointOnEllipseEdge(offset);
                bool clampedX = MathUtilities.TryClamp(ref offset.x, -Mathf.Abs(edgePoint.x), Mathf.Abs(edgePoint.x));
                bool clampedY = MathUtilities.TryClamp(ref offset.y, -Mathf.Abs(edgePoint.y), Mathf.Abs(edgePoint.y));
                waypointMarker.RectTransform.position = (Vector2)m_ellipseCentre.position + offset; // depth reset to 0
                if (clampedX | clampedY)
                {
                    showDistanceIndicator = false;
                    showArrowIndicator = true;
                }
                else
                {
                    showArrowIndicator = false;
                    showDistanceIndicator = !waypointMarker.DistanceIndicatorDisabled();
                }
            }
            waypointMarker.ShowDistanceIndicator.Value = showDistanceIndicator;
            waypointMarker.ShowArrowIndicator.Value = showArrowIndicator;
        }

        // 0x06003589 (0x683940): the point is scaled into the unit circle (the ellipse radius is in screen fractions), normalised,
        // and scaled back out onto the ellipse edge.
        private Vector3 GetClosestPointOnEllipseEdge(Vector3 point)
        {
            Vector2 radius = m_ellipseRadius * new Vector2(Screen.width, Screen.height);
            Vector3 direction = new Vector3(point.x / radius.x, point.y / radius.y, point.z).normalized;
            return new Vector3(radius.x * direction.x, radius.y * direction.y, direction.z);
        }

        // 0x0600358a (0x683A70)
        public UIContainerWaypointMarkersScreenSpace() { }
    }
}
