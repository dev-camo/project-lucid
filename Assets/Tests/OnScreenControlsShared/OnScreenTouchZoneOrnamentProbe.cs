using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only OnScreenTouchZoneOrnament with entry points for its protected hooks.
    public sealed class OnScreenTouchZoneOrnamentProbe : OnScreenTouchZoneOrnament
    {
        public Vector2 InputNow(Vector2 position, Vector2 delta) => OnInput(position, delta);

        public void SupplyNow(Vector2 movement) => SupplyHeldGameInput(movement);
    }
}
