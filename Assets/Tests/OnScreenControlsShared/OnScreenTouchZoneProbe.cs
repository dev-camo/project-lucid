using System.Collections.Generic;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only concrete OnScreenTouchZone. It records what the abstract and virtual hooks receive, in order,
    // and runs every inherited body; Order holds "process", "input", "held", "pinch" and "release" rows.
    public sealed class OnScreenTouchZoneProbe : OnScreenTouchZone
    {
        public readonly List<string> Order = new List<string>();
        public readonly List<(Vector2 position, Vector2 delta, float pinch, float initial)> Processed =
            new List<(Vector2 position, Vector2 delta, float pinch, float initial)>();
        public readonly List<(Vector2 position, Vector2 delta)> Inputs = new List<(Vector2 position, Vector2 delta)>();
        public readonly List<Vector2> HeldInputs = new List<Vector2>();
        // The pinch hook's arguments and, beside them, the values of the protected pinch properties at that moment.
        public readonly List<(float pinch, float initial, float pinchProperty, float initialProperty)> Pinches =
            new List<(float pinch, float initial, float pinchProperty, float initialProperty)>();

        public int Initialised;
        public readonly List<bool> Shutdowns = new List<bool>();

        public Vector2 InputResult;
        public bool? ActiveOverride;
        public float? ScoreOverride;

        public override void Initialise()
        {
            Initialised++;
            base.Initialise();
        }

        public override void Shutdown(bool usingValidInputType)
        {
            Shutdowns.Add(usingValidInputType);
            base.Shutdown(usingValidInputType);
        }

        protected override void ProcessTouch(Vector2 touchPosition, Vector2 touchDelta, float pinchDistance, float initialPinchDistance)
        {
            Order.Add("process");
            Processed.Add((touchPosition, touchDelta, pinchDistance, initialPinchDistance));
            base.ProcessTouch(touchPosition, touchDelta, pinchDistance, initialPinchDistance);
        }

        protected override void OnTouchRelease()
        {
            Order.Add("release");
            base.OnTouchRelease();
        }

        protected override Vector2 OnInput(Vector2 touchPosition, Vector2 touchDelta)
        {
            Order.Add("input");
            Inputs.Add((touchPosition, touchDelta));
            return InputResult;
        }

        protected override void SupplyHeldGameInput(Vector2 resultantMovement)
        {
            Order.Add("held");
            HeldInputs.Add(resultantMovement);
        }

        protected override void SupplyPinchInput(float pinchDistance, float initialPinchDistance)
        {
            Order.Add("pinch");
            Pinches.Add((pinchDistance, initialPinchDistance, PinchDistance, InitialPinchDistance));
        }

        public override bool IsActive() => ActiveOverride ?? base.IsActive();

        protected override float GetScoreForTouchPosition(Vector2 position) => ScoreOverride ?? base.GetScoreForTouchPosition(position);

        public bool Pinching => IsPinching;
        public float PinchNow => PinchDistance;
        public float InitialPinchNow => InitialPinchDistance;
        public bool WasDown => PointerWasDown;

        public bool MultiTouch
        {
            get => MultiTouchEnabled;
            set => MultiTouchEnabled = value;
        }

        public void ProcessNow(Vector2 position, Vector2 delta, float pinch, float initialPinch) => ProcessTouch(position, delta, pinch, initialPinch);

        public void ReleaseNow() => OnTouchRelease();

        public bool ValidFor(List<ActorAbilityType> ignoreAbilities) => TouchValidForCharacter(ignoreAbilities);
    }
}
