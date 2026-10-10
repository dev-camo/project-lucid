using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Hardlight;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Owned managed/Unity checks of the preserved four-method leaf. These do not
    // execute the supplied native player or qualify native layout/fault parity.
    public static class OriginalConeVolumeVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        private static readonly object FixtureGate = new object();

        // Original dual-CPU literal table, expressed as bits instead of recomputing
        // sin/cos. The small residuals and asymmetric quadrant values are retained.
        private static readonly uint[] OriginalAngleBits =
        {
            0x3f800000u, 0x00000000u, 0x3f6c835eu, 0x3ec3ef16u,
            0x3f3504f3u, 0x3f3504f3u, 0x3ec3ef15u, 0x3f6c835eu,
            0xb33bbd2eu, 0x3f800000u, 0xbec3ef18u, 0x3f6c835eu,
            0xbf3504f3u, 0x3f3504f3u, 0xbf6c8360u, 0x3ec3ef10u,
            0xbf800000u, 0xb3bbbd2eu, 0xbf6c835eu, 0xbec3ef15u,
            0xbf3504f1u, 0xbf3504f5u, 0xbec3ef0bu, 0xbf6c8361u,
            0x324cde2eu, 0xbf800000u, 0x3ec3ef1bu, 0xbf6c835du,
            0x3f3504f7u, 0xbf3504efu, 0x3f6c835fu, 0xbec3ef15u
        };

        private static void RequireCurrentProvidersAndEmission()
        {
            // Deliberately missing from this source-author packet. Root must
            // supply a separately reviewed witness of actual complete current PE
            // declarations/CIL/scoped providers; no fabricated emitted facts.
            ConeVolumeCurrentFacts.RequireWholeCurrentEmission();
            Require(typeof(ConeVolume).FullName == "HardlightProject.ConeVolume" &&
                typeof(ConeVolume).Assembly.GetName().Name == "Game.Runtime" &&
                (int)typeof(ConeVolume).Attributes == 1048961, "original whole static owner");
            Require(typeof(ConeVolume.ConeDescription).FullName == "HardlightProject.ConeVolume+ConeDescription" &&
                (int)typeof(ConeVolume.ConeDescription).Attributes == 1048842 &&
                typeof(ConeVolume.ConeDescription).GetMethods(Own).Length == 0 &&
                typeof(ConeVolume.ConeDescription).GetConstructors(Own).Length == 0,
                "original ordinary nested value owner has no explicit constructor or methods");
            Require(typeof(ConeVolume).GetMethods(Own).Length == 4 &&
                typeof(ConeVolume).GetFields(Own).Length == 2 &&
                typeof(ConeVolume).GetProperties(Own).Length == 1 &&
                typeof(ConeVolume).GetNestedTypes(Own).Length == 1,
                "complete original owner and nested declaration counts");
            foreach (Type provider in new[] { typeof(Vector2), typeof(Vector3), typeof(Quaternion), typeof(Bounds), typeof(Mathf) })
                Require(provider.Assembly.GetName().Name == "UnityEngine.CoreModule", "genuine Unity provider " + provider.FullName);
            Require(typeof(Vector3Extensions).Assembly.GetName().Name == "HLUnityCore.Runtime",
                "genuine complete Core vector extension provider");
        }

        public static void RunOrdinaryDescriptionValues()
        {
            RequireCurrentProvidersAndEmission();
            // Ordinary zero initialization, not an uninitialized-object bypass.
            var value = new ConeVolume.ConeDescription();
            Vector(value.Origin, Vector3.zero, "default origin");
            QuaternionBits(value.Rotation, new Quaternion(0f, 0f, 0f, 0f), "default rotation is the zero quaternion");
            Require(Bits(value.Distance) == 0u && Bits(value.Step) == 0u &&
                Bits(value.Tangents.x) == 0u && Bits(value.Tangents.y) == 0u, "ordinary value fields initialize to positive zero");
            value.Origin = new Vector3(10f, -20f, 30f);
            value.Rotation = new Quaternion(0f, 1f, 0f, 0f);
            value.Distance = 8f; value.Step = 3f; value.Tangents = new Vector2(0.5f, 0.25f);
            ConeVolume.ConeDescription copy = value;
            copy.Origin.x = -6f; copy.Distance = -8f; copy.Step = 99f; copy.Tangents.y = 2f;
            Vector(value.Origin, new Vector3(10f, -20f, 30f), "description copy does not alias origin");
            QuaternionBits(value.Rotation, new Quaternion(0f, 1f, 0f, 0f), "ordinary rotation field remains unchanged");
            Require(value.Distance == 8f && value.Step == 3f && value.Tangents.y == 0.25f &&
                copy.Origin.x == -6f && copy.Distance == -8f && copy.Step == 99f && copy.Tangents.y == 2f,
                "nested description retains ordinary value-copy semantics");
        }

        public static void RunEllipseBoundaryDepthAndTranslation()
        {
            RequireCurrentProvidersAndEmission();
            Vector3 origin = new Vector3(8f, -4f, 16f);
            Vector2 tangents = new Vector2(2f, 1f);
            // Radii 4 and 2 give exact dyadic inside/edge/outside results.
            Inside(origin, origin, tangents, 2f, Quaternion.identity, true, "ellipse center");
            Inside(origin + new Vector3(2f, 1f, 0f), origin, tangents, 2f, Quaternion.identity, true, "strict interior");
            Inside(origin + new Vector3(4f, 0f, 0f), origin, tangents, 2f, Quaternion.identity, true, "inclusive positive X boundary");
            Inside(origin + new Vector3(-4f, 0f, 0f), origin, tangents, 2f, Quaternion.identity, true, "inclusive negative X boundary");
            Inside(origin + new Vector3(0f, 2f, 0f), origin, tangents, 2f, Quaternion.identity, true, "inclusive Y boundary");
            Inside(origin + new Vector3(4f, 2f, 0f), origin, tangents, 2f, Quaternion.identity, false, "rectangle corner lies outside ellipse");
            Inside(new Vector3(FromBits(0x40800001u), 0f, 0f), Vector3.zero, tangents, 2f, Quaternion.identity, false, "one float above X edge");
            Inside(new Vector3(FromBits(0x407fffffu), 0f, 0f), Vector3.zero, tangents, 2f, Quaternion.identity, true, "one float below X edge");
            foreach (float depth in new[] { -128f, 0f, 128f })
            {
                Inside(origin + new Vector3(2f, 1f, depth), origin, tangents, 2f, Quaternion.identity, true, "local depth is deliberately ignored");
                Inside(origin + new Vector3(4f, 2f, depth), origin, tangents, 2f, Quaternion.identity, false, "ignored depth cannot admit an outside XY point");
            }
            Quaternion halfYaw = new Quaternion(0f, 1f, 0f, 0f);
            Inside(origin + new Vector3(-4f, 0f, 128f), origin, tangents, 2f, halfYaw, true, "genuine half yaw preserves exact transformed boundary");
            Inside(origin + new Vector3(-4f, 2f, -128f), origin, tangents, 2f, halfYaw, false, "transformed outside point remains outside");
            Inside(origin + new Vector3(4f, 0f, 0f), origin, tangents, -2f, Quaternion.identity, true, "negative distance retains squared-radius membership");
        }

        public static void RunExactAngleTableAndMutableAlias()
        {
            RequireCurrentProvidersAndEmission();
            using (var lease = new CacheLease())
            {
                lease.Install(null);
                Vector2[] angles = lease.ReadAndOwnGenerated();
                Require(angles.Length == 16, "exact table size");
                Table(angles, "fresh original literal table");
                Require(ReferenceEquals(angles, ConeVolume.ConeSegmentAngles), "repeated getter returns the retained array identity");
                angles[4] = new Vector2(7f, -3f);
                Vector2[] same = ConeVolume.ConeSegmentAngles;
                Require(ReferenceEquals(angles, same) && same[4].x == 7f && same[4].y == -3f,
                    "caller mutation survives the correctly sized cache guard");
                for (int i = 0; i < angles.Length; i++)
                    if (i != 4) Require(Bits(angles[i].x) == OriginalAngleBits[2 * i] && Bits(angles[i].y) == OriginalAngleBits[2 * i + 1], "mutation does not alter neighboring table entries");
            }
        }

        public static void RunCacheSizeReplacementAndSizedRetention()
        {
            RequireCurrentProvidersAndEmission();
            using (var lease = new CacheLease())
            {
                foreach (int length in new[] { 0, 15, 17 })
                {
                    var wrong = new Vector2[length];
                    if (length != 0) wrong[0] = new Vector2(7f, -3f);
                    uint[] before = Snapshot(wrong);
                    lease.Install(wrong);
                    Vector2[] replacement = lease.ReadAndOwnGenerated();
                    Require(!ReferenceEquals(wrong, replacement) && replacement.Length == 16, "wrong size creates a distinct 16-entry array");
                    Table(replacement, "each replacement receives the exact original table");
                    EqualSnapshot(wrong, before, "replacement does not mutate the rejected array");
                }
                var sized = new Vector2[16];
                sized[0] = new Vector2(7f, -3f); sized[15] = new Vector2(-9f, 11f);
                uint[] retained = Snapshot(sized);
                lease.Install(sized);
                Require(ReferenceEquals(sized, ConeVolume.ConeSegmentAngles), "arbitrary correctly sized array is retained");
                EqualSnapshot(sized, retained, "length check does not refill caller-supplied contents");
            }
        }

        public static void RunBoundsAxisHalfTurnNegativeDistanceAndStep()
        {
            RequireCurrentProvidersAndEmission();
            using (var lease = new CacheLease())
            {
                lease.Install(null);
                lease.ReadAndOwnGenerated();
                var cone = Description(Quaternion.identity, 8f);
                Bounds first = lease.CalculateBounds(cone);
                BoundsValues(first, new Vector3(6f, -22f, 30f), new Vector3(14f, -18f, 38f), "axis-aligned ellipse rim and apex");
                cone.Step = 999f;
                Bounds second = lease.CalculateBounds(cone);
                BoundsSame(first, second, "Step is preserved data and deliberately unused by the bounds calculation");
                cone.Rotation = new Quaternion(0f, 1f, 0f, 0f);
                BoundsValues(lease.CalculateBounds(cone), new Vector3(6f, -22f, 22f), new Vector3(14f, -18f, 30f), "half yaw reverses axial direction");
                cone.Rotation = Quaternion.identity; cone.Distance = -8f;
                BoundsValues(lease.CalculateBounds(cone), new Vector3(6f, -22f, 22f), new Vector3(14f, -18f, 30f), "negative distance is retained without a clamp");
            }
        }

        public static void RunBoundsConsumeMutableCachedAngles()
        {
            RequireCurrentProvidersAndEmission();
            using (var lease = new CacheLease())
            {
                var angles = new Vector2[16];
                lease.Install(angles);
                var cone = Description(Quaternion.identity, 8f);
                BoundsValues(lease.CalculateBounds(cone), new Vector3(10f, -20f, 30f), new Vector3(10f, -20f, 38f), "zero cached rim collapses to the apex/end segment");
                angles[0] = new Vector2(3f, -4f);
                BoundsValues(lease.CalculateBounds(cone), new Vector3(10f, -28f, 30f), new Vector3(22f, -20f, 38f), "caller-edited cached point changes the actual bounds");
                Require(ReferenceEquals(angles, ConeVolume.ConeSegmentAngles), "bounds does not replace the correctly sized mutable cache");
                angles[0] = Vector2.zero;
                BoundsValues(lease.CalculateBounds(cone), new Vector3(10f, -20f, 30f), new Vector3(10f, -20f, 38f), "restored owned point restores the segment bounds");
            }
        }

        public static void RunBoundsRetainNonunitQuaternion()
        {
            RequireCurrentProvidersAndEmission();
            using (var lease = new CacheLease())
            {
                lease.Install(null);
                lease.ReadAndOwnGenerated();
                // This genuine Quaternion has squared length two. Its exact
                // algebra maps forward to (2,0,-1); no normalization is allowed.
                var cone = Description(new Quaternion(0f, 1f, 0f, 1f), 8f);
                BoundsValues(lease.CalculateBounds(cone), new Vector3(10f, -22f, 14f), new Vector3(30f, -18f, 30f), "nonunit quaternion preserves scaled/sheared provider result");
            }
        }

        private static ConeVolume.ConeDescription Description(Quaternion rotation, float distance)
        {
            return new ConeVolume.ConeDescription
            {
                Origin = new Vector3(10f, -20f, 30f), Rotation = rotation,
                Distance = distance, Step = 3f, Tangents = new Vector2(0.5f, 0.25f)
            };
        }
        private static void Inside(Vector3 position, Vector3 origin, Vector2 tangents, float distance, Quaternion rotation, bool expected, string label)
        { Require(ConeVolume.IsPositionInsideCone(position, origin, tangents, distance, rotation) == expected, label); }
        private static void BoundsValues(Bounds bounds, Vector3 min, Vector3 max, string label)
        {
            Vector(bounds.min, min, label + " min"); Vector(bounds.max, max, label + " max");
            Vector(bounds.center, (min + max) * 0.5f, label + " center");
            Vector(bounds.size, max - min, label + " size");
        }
        private static void BoundsSame(Bounds a, Bounds b, string label)
        { Vector(a.center, b.center, label + " center"); Vector(a.size, b.size, label + " size"); }
        private static void Vector(Vector3 value, Vector3 expected, string label)
        { Require(value.x == expected.x && value.y == expected.y && value.z == expected.z, label); }
        private static void QuaternionBits(Quaternion value, Quaternion expected, string label)
        { Require(Bits(value.x) == Bits(expected.x) && Bits(value.y) == Bits(expected.y) && Bits(value.z) == Bits(expected.z) && Bits(value.w) == Bits(expected.w), label); }
        private static uint Bits(float value) { return BitConverter.ToUInt32(BitConverter.GetBytes(value), 0); }
        private static float FromBits(uint value) { return BitConverter.ToSingle(BitConverter.GetBytes(value), 0); }
        private static uint[] Snapshot(Vector2[] values)
        {
            if (values == null) return null;
            var bits = new uint[values.Length * 2];
            for (int i = 0; i < values.Length; i++) { bits[2 * i] = Bits(values[i].x); bits[2 * i + 1] = Bits(values[i].y); }
            return bits;
        }
        private static void EqualSnapshot(Vector2[] values, uint[] expected, string label)
        {
            Require((values == null) == (expected == null), label + " null identity");
            if (values == null) return;
            Require(values.Length * 2 == expected.Length, label + " length");
            for (int i = 0; i < values.Length; i++) Require(Bits(values[i].x) == expected[2 * i] && Bits(values[i].y) == expected[2 * i + 1], label + " bits " + i);
        }
        private static void Table(Vector2[] values, string label) { EqualSnapshot(values, OriginalAngleBits, label); }
        private static void Require(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); }

        // Only this original mutable field is reflected. No readonly field or
        // constructor is bypassed. Every installed/generated array is owned;
        // the pre-existing array identity and all its bits remain untouched.
        private sealed class CacheLease : IDisposable
        {
            private readonly FieldInfo field;
            private readonly Vector2[] saved;
            private readonly uint[] savedBits;
            private readonly List<Vector2[]> introduced = new List<Vector2[]>();
            private Vector2[] active;
            private bool disposed;

            public CacheLease()
            {
                Require(Monitor.TryEnter(FixtureGate), "another owned cache fixture is active");
                try
                {
                    field = typeof(ConeVolume).GetField("s_coneSegmentAngles", Own);
                    Require(field != null && field.DeclaringType == typeof(ConeVolume) &&
                        field.FieldType == typeof(Vector2[]) && (int)field.Attributes == 17 &&
                        field.IsStatic && !field.IsInitOnly && !field.IsLiteral, "exact original writable static cache field");
                    saved = (Vector2[])field.GetValue(null); savedBits = Snapshot(saved); active = saved;
                }
                catch { Monitor.Exit(FixtureGate); throw; }
            }
            public void Install(Vector2[] values)
            {
                Require(!disposed && ReferenceEquals(field.GetValue(null), active), "cache identity changed before owned install");
                EqualSnapshot(saved, savedBits, "pre-existing cache remains untouched before install");
                Require(values == null || !ReferenceEquals(values, saved), "only newly owned arrays may be installed");
                introduced.Add(values); field.SetValue(null, values); active = values;
                Require(ReferenceEquals(field.GetValue(null), active), "owned cache installation is exact");
            }
            public Vector2[] ReadAndOwnGenerated()
            {
                Require(!disposed && ReferenceEquals(field.GetValue(null), active), "cache identity changed before getter");
                bool replacementRequired = active == null || active.Length != 16;
                Vector2[] returned = ConeVolume.ConeSegmentAngles;
                // Prove getter-return provenance before adopting the field
                // identity; an unmatched foreign array must remain unowned.
                Vector2[] actual = (Vector2[])field.GetValue(null);
                Require(ReferenceEquals(returned, actual), "getter return matches the actual cache before ownership");
                if (!ReferenceEquals(actual, active)) { introduced.Add(actual); active = actual; }
                Require(ReferenceEquals(returned, active) && returned != null && returned.Length == 16,
                    "getter returns its actual 16-entry cache");
                Require(!replacementRequired || !ReferenceEquals(returned, saved), "getter replacement is newly owned");
                return returned;
            }
            public Bounds CalculateBounds(ConeVolume.ConeDescription cone)
            {
                Require(!disposed && ReferenceEquals(field.GetValue(null), active) && active != null && active.Length == 16,
                    "owned correctly sized cache before bounds");
                uint[] before = Snapshot(active);
                Bounds result = ConeVolume.CalculateConeBounds(cone);
                Require(ReferenceEquals(field.GetValue(null), active), "bounds retains the active array identity");
                EqualSnapshot(active, before, "bounds reads without mutating the owned array");
                return result;
            }
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                try
                {
                    Vector2[] actual = (Vector2[])field.GetValue(null);
                    bool owned = ReferenceEquals(actual, saved) || introduced.Exists(x => ReferenceEquals(x, actual));
                    // A foreign identity is never overwritten to manufacture a
                    // clean test. Root must hold/isolate the process if detected.
                    Require(owned, "foreign cache identity prevents safe restoration");
                    field.SetValue(null, saved);
                    Require(ReferenceEquals(field.GetValue(null), saved), "original cache identity restored");
                    EqualSnapshot(saved, savedBits, "original cache bits restored without element writes");
                }
                finally { Monitor.Exit(FixtureGate); }
            }
        }
    }
}
