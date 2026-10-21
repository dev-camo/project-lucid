using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200094d; bodies translated from the 1.10.1 x86-64 binary.
    // Waypoint markers along a compass strip: markers outside the strip light the left or right arrow and can be clamped to its sides.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerWaypointMarkersCompass : UIContainerWaypointMarkersBase
    {
        private enum ScreenDirection
        {
            None = 0,
            Right = 1,
            Left = 2,
        }

        [SerializeField] private bool m_clampMarkersToSides;
        [ShowIf("m_clampMarkersToSides", (string)null)] [SerializeField] private float m_clampToSidesPadding;
        public readonly Bindable<bool> ShowLeftMarkerArrow = new Bindable<bool>();
        public readonly Bindable<bool> ShowRightMarkerArrow = new Bindable<bool>();

        // 0x06003580 (0x682BD0): the smooth damp time only applies to a marker that is outside the strip. The native body
        // inlines DetermineMarkerOutOfBoundsDirection and ClampMarkerToSide.
        protected override void DoUpdate(Character character, Camera mainCamera)
        {
            bool showLeftArrow = false;
            bool showRightArrow = false;
            foreach (UIWidgetSDTWaypointMarker waypointMarker in m_waypointMarkers)
            {
                if (!waypointMarker.gameObject.activeInHierarchy)
                    continue;
                ScreenDirection screenDirection = ScreenDirection.None;
                float smoothTimeSeconds = 0f;
                if (waypointMarker.IsVisible.Value)
                {
                    screenDirection = DetermineMarkerOutOfBoundsDirection(waypointMarker);
                    if (screenDirection != ScreenDirection.None)
                        smoothTimeSeconds = m_smoothTimeSeconds;
                }
                waypointMarker.OnUpdate(character, mainCamera, m_canvas, smoothTimeSeconds, true);
                if (screenDirection == ScreenDirection.Right)
                    showRightArrow = true;
                else if (screenDirection == ScreenDirection.Left)
                    showLeftArrow = true;
                if (m_clampMarkersToSides)
                    ClampMarkerToSide(waypointMarker, screenDirection, m_clampToSidesPadding);
            }
            ShowLeftMarkerArrow.Value = showLeftArrow;
            ShowRightMarkerArrow.Value = showRightArrow;
        }

        // 0x06003581 (0x682F20): a marker behind the camera goes to the side it is on, mirrored about the strip's centre.
        private ScreenDirection DetermineMarkerOutOfBoundsDirection(UIWidgetSDTWaypointMarker waypointMarker)
        {
            Vector2 anchoredPosition = waypointMarker.RectTransform.anchoredPosition;
            Rect rect = m_rectTransform.rect;
            if (waypointMarker.IsBehindCamera.Value)
                return rect.center.x - anchoredPosition.x < 0f ? ScreenDirection.Left : ScreenDirection.Right;
            if (rect.xMin > anchoredPosition.x)
                return ScreenDirection.Left;
            return anchoredPosition.x > rect.xMax ? ScreenDirection.Right : ScreenDirection.None;
        }

        // 0x06003582 (0x682FC0): moves a marker that is outside the strip onto the strip's edge, inset by the padding.
        private void ClampMarkerToSide(UIWidgetSDTWaypointMarker waypointMarker, ScreenDirection screenDirection, float padding)
        {
            Vector3 anchoredPosition = waypointMarker.RectTransform.anchoredPosition3D;
            switch (screenDirection)
            {
                case ScreenDirection.Left:
                    anchoredPosition.x = m_rectTransform.rect.xMin + padding;
                    break;
                case ScreenDirection.Right:
                    anchoredPosition.x = m_rectTransform.rect.xMax - padding;
                    break;
                default:
                    return;
            }
            waypointMarker.RectTransform.anchoredPosition3D = anchoredPosition;
        }

        // 0x06003583 (0x683060)
        public UIContainerWaypointMarkersCompass() { }
    }
}
