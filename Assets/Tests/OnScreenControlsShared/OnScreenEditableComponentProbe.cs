using System.Collections.Generic;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only user-editable UI component: the fixture supplies the touch and pinch readings and the
    // rectangle, and the component records the calls the controller makes on it.
    public sealed class OnScreenEditableComponentProbe : MonoBehaviour, IUserEditableUIComponent
    {
        public RectTransform Rect;
        public bool HasTouch;
        public Vector2 Touch;
        public bool HasPinch;
        public float PinchDistance;
        public float InitialPinchDistance;
        public int TransformUpdatedCount;
        public readonly List<bool> EditingCalls = new List<bool>();

        public void TransformUpdated() => TransformUpdatedCount++;

        public RectTransform GetRectTransform() => Rect;

        public bool GetTouchPosition(out Vector2 touch)
        {
            touch = Touch;
            return HasTouch;
        }

        public bool GetPinchInfo(out float pinchDistance, out float initialPinchDistance)
        {
            pinchDistance = PinchDistance;
            initialPinchDistance = InitialPinchDistance;
            return HasPinch;
        }

        public void SetEditing(bool editing) => EditingCalls.Add(editing);
    }
}
