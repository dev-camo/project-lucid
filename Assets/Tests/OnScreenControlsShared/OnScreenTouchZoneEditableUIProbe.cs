using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only OnScreenTouchZoneEditableUI with entry points for its protected hooks.
    public sealed class OnScreenTouchZoneEditableUIProbe : OnScreenTouchZoneEditableUI
    {
        public Vector2 InputNow(Vector2 position, Vector2 delta) => OnInput(position, delta);

        public void SupplyNow(Vector2 movement) => SupplyHeldGameInput(movement);

        public float ScoreNow(Vector2 position) => GetScoreForTouchPosition(position);
    }
}
