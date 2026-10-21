using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only concrete OnScreenControlZone whose input is the touch movement itself, with entry points
    // for the protected members.
    public sealed class OnScreenControlZoneProbe : OnScreenControlZone
    {
        protected override Vector2 OnInput(Vector2 touchPosition, Vector2 touchDelta) => touchDelta;

        public void SupplyNow(Vector2 movement) => SupplyHeldGameInput(movement);

        public void ReleaseNow() => OnTouchRelease();

        public Vector2 ApplyNow(Vector2 movement)
        {
            ApplyModifiers(ref movement);
            return movement;
        }

        public Vector2 ControllerInput => m_controllerInput;

        public List<InputType> ShowOnInputTypes => m_showOnInputTypes;
    }
}
