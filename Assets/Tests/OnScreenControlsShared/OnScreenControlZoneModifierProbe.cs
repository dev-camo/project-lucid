using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Tests.OnScreenControlsShared
{
    // Test-only InputModifier: scales the vector it is given, and counts and records what the zone asks of it.
    public sealed class OnScreenControlZoneModifierProbe : InputModifier
    {
        public Vector2 Scale = Vector2.one;
        public int Resets;
        public readonly List<Vector3> Received = new List<Vector3>();

        public override float Modify(float value, float deltaTime) => value;

        public override Vector3 Modify(Vector3 value, float deltaTime)
        {
            Received.Add(value);
            return new Vector3(value.x * Scale.x, value.y * Scale.y, value.z);
        }

        public override void Reset() => Resets++;
    }
}
