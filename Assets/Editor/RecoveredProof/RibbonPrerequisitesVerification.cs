using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Complete original value/interface prerequisite scope. No fixture implements IRibbon.
    public static class RibbonPrerequisitesVerification
    {
        private static int checks;
        private static void Check(bool value, string label)
        {
            ++checks;
            if (!value) throw new InvalidOperationException("RibbonPrerequisites: " + label);
        }
        private static void Vec(Vector3 actual, Vector3 expected, string label)
        {
            Check(actual.x.Equals(expected.x), label + ":x");
            Check(actual.y.Equals(expected.y), label + ":y");
            Check(actual.z.Equals(expected.z), label + ":z");
        }
        private static void Quat(Quaternion actual, Quaternion expected, string label)
        {
            Check(actual.x.Equals(expected.x), label + ":x");
            Check(actual.y.Equals(expected.y), label + ":y");
            Check(actual.z.Equals(expected.z), label + ":z");
            Check(actual.w.Equals(expected.w), label + ":w");
        }
        private static void ExpectNull(Action action, string label)
        {
            try { action(); }
            catch (NullReferenceException) { Check(true, label); return; }
            Check(false, label);
        }

        public static int RunManaged()
        {
            checks = 0;
            var kt = new KnotT(-4, -2.5f);
            Check(kt.KnotIndex == -4 && kt.T == -2.5f, "KnotT constructor does not clamp");
            kt.KnotIndex = int.MinValue; kt.T = float.NaN;
            Check(kt.KnotIndex == int.MinValue && float.IsNaN(kt.T), "KnotT setters preserve extremes");
            kt.T = -0f;
            Check(BitConverter.SingleToInt32Bits(kt.T) == unchecked((int)0x80000000), "KnotT setter keeps negative zero");
            Check(new KnotT(2, float.PositiveInfinity).T == float.PositiveInfinity, "KnotT constructor keeps infinity");
            Check(default(KnotT).KnotIndex == 0 && default(KnotT).T == 0f, "KnotT default is zero");
            var lr = new LinearRatio(int.MaxValue, 3f);
            Check(lr.KnotIndex == int.MaxValue && lr.T == 3f, "LinearRatio constructor no clamp");
            lr.KnotIndex = -9; lr.T = float.NegativeInfinity;
            Check(lr.KnotIndex == -9 && float.IsNegativeInfinity(lr.T), "LinearRatio mutable raw values");
            Check(float.IsNaN(new LinearRatio(-1, float.NaN).T), "LinearRatio constructor retains NaN");
            lr.T = -0f;
            Check(BitConverter.SingleToInt32Bits(lr.T) == unchecked((int)0x80000000), "LinearRatio setter negative zero");
            Check(default(LinearRatio).KnotIndex == 0 && default(LinearRatio).T == 0f, "LinearRatio default zero");

            var pos = new Vector3(2f, -3f, 5f); var tangent = new Vector3(7f, 11f, -13f);
            var pt = new PositionAndTangent(pos, tangent);
            Vec(pt.Position, pos, "position ctor stores"); Vec(pt.Tangent, tangent, "tangent remains unnormalized");
            pt.Position = tangent; pt.Tangent = pos;
            Vec(pt.Position, tangent, "position setter"); Vec(pt.Tangent, pos, "tangent setter");
            Check(!typeof(KnotT).IsSerializable && !typeof(LinearRatio).IsSerializable && !typeof(PositionAndTangent).IsSerializable,
                "three original nonserializable value shapes");
            Check(typeof(KnotT).GetField("<KnotIndex>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic) != null,
                "natural original KnotT backing field");
            Check(typeof(LinearRatio).GetProperty("T").SetMethod.IsPublic, "original public LinearRatio setter");
            Check(typeof(PositionAndTangent).GetProperty("Position").GetMethod.GetCustomAttributes(false)
                .Any(a => a.GetType().FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute"), "natural readonly getter marker");

            Check((int)RibbonAlignment.Center == 0 && (int)RibbonAlignment.Left == -1 && (int)RibbonAlignment.Right == 1,
                "exact original enum signs");
            Check(typeof(IRibbon).GetInterfaces().SequenceEqual(new[] { typeof(ISurface) }), "real inherited surface interface");
            Check(typeof(IRibbon).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length == 28,
                "all twenty-eight genuine own ribbon contracts");
            Check(typeof(IRibbonKnot).GetMethods().Length == 10 && typeof(IRibbonKnot).GetInterfaces().Length == 0,
                "ten actual own knot contracts");
            Check(typeof(IRibbon).GetEvents().Length == 2 && typeof(IRibbon).GetFields().Length == 0,
                "original fieldless interface event graph");
            Check(typeof(IRibbon).GetMethod("TryGetRaycastHit").GetParameters()[2].IsOut, "original raycast out contract");

            Quat(new LightweightTransform(pos, new Quaternion(1f, 2f, 3f, 4f)).Orientation, new Quaternion(1f, 2f, 3f, 4f), "raw constructor does not normalize quaternion");
            var rotation = new Quaternion(0f, 1f, 0f, 0f);
            var transform = new LightweightTransform(pos, rotation);
            Vec(transform.Location, pos, "transform ctor raw location"); Quat(transform.Orientation, rotation, "transform ctor raw orientation");
            Vec(transform.Forwards, new Vector3(0f, 0f, -1f), "forwards quaternion basis");
            Vec(transform.Right, new Vector3(-1f, 0f, 0f), "right quaternion basis");
            Vec(transform.Up, new Vector3(0f, 1f, 0f), "up quaternion basis");
            transform.Location = tangent; transform.Orientation = new Quaternion(0f, 2f, 0f, 0f);
            Vec(transform.Location, tangent, "transform location setter");
            Vec(transform.Forwards, new Vector3(0f, 0f, -7f), "basis keeps nonunit quaternion quirk");
            Check(typeof(LightweightTransform).IsSerializable, "original serializable type flag");
            var fields = typeof(LightweightTransform).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Check(fields.Select(f => f.Name).SequenceEqual(new[] { "m_location", "m_orientation" }), "two original ordered private fields");
            Check(fields.All(f => f.GetCustomAttributes(typeof(SerializeField), false).Length == 1), "original two SerializeField markers");

            var target = new LightweightTransform(new Vector3(10f, 0f, 0f), new Quaternion(1f, 2f, 3f, 4f));
            var moving = new LightweightTransform(Vector3.zero, rotation);
            moving.SmoothTowards(target, 2f, 0f, false);
            Vec(moving.Location, new Vector3(2f, 0f, 0f), "position uses absolute MoveTowards distance");
            Quat(moving.Orientation, rotation, "zero rotation cap keeps orientation");
            moving.SmoothTowards(target, 20f, -1f, false);
            Vec(moving.Location, target.Location, "position overshoot lands at target");
            Quat(moving.Orientation, rotation, "negative rotation cap unchanged");
            moving = new LightweightTransform(Vector3.zero, rotation);
            moving.SmoothTowards(target, -2f, float.NaN, false);
            Vec(moving.Location, new Vector3(-2f, 0f, 0f), "negative position limit moves away");
            Quat(moving.Orientation, rotation, "ordered NaN rotation cap skips");
            moving.SmoothTowards(target, 0f, float.NaN, true);
            Quat(moving.Orientation, target.Orientation, "snap ignores rotation cap and normalization");
            moving = new LightweightTransform(target.Location, rotation);
            moving.SmoothTowards(target, -2f, 0f, false);
            Vec(moving.Location, target.Location, "zero displacement keeps target even negative limit");

            var location = default(RibbonLocation);
            Check(location.m_ribbon == null && location.m_alignment == RibbonAlignment.Center, "original default ribbon fields");
            Check(location.m_knotLinearRatio.KnotIndex == 0 && location.m_metadata.Surface == null, "original default value fields");
            ExpectNull(() => { float value = location.KnotDistance; }, "KnotDistance original null fault before zero guard");
            ExpectNull(() => { float value = location.RibbonDistance; }, "RibbonDistance original null fault before zero guard");
            ExpectNull(() => location.GetLocalTransform(), "GetLocalTransform does not fabricate null ribbon");
            Check(typeof(RibbonLocation).GetFields().Select(f => f.Name).SequenceEqual(new[] { "m_ribbon", "m_alignment", "m_knotLinearRatio", "m_localOffset", "m_positionBoundsInfo", "m_metadata" }), "all six public ordered original fields");
            var parameter = typeof(RibbonLocation).GetMethod("GetLocalTransform").GetParameters()[0];
            Check(parameter.Name == "ignoreOffset" && parameter.IsOptional && parameter.DefaultValue.Equals(false), "exact original optional false");
            return checks;
        }

        // Genuine Unity lifecycle/shared-native Quaternion fixtures, not a stand-in Transform.
        public static int RunEngine()
        {
            checks = 0;
            var obj = new GameObject("Project Lucid original ribbon transform proof");
            try
            {
                obj.transform.position = new Vector3(2f, 3f, 5f);
                obj.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                var captured = new LightweightTransform(obj.transform);
                Vec(captured.Location, obj.transform.position, "genuine Transform constructor position");
                Quat(captured.Orientation, obj.transform.rotation, "genuine Transform constructor rotation");
                var original = new LightweightTransform(Vector3.zero, Quaternion.identity);
                var target = new LightweightTransform(new Vector3(10f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
                var half = LightweightTransform.Lerp(original, target, 0.5f);
                Vec(half.Location, new Vector3(5f, 0f, 0f), "original Vector3.Lerp position");
                Check(Quaternion.Angle(half.Orientation, Quaternion.Euler(0f, 45f, 0f)) < 0.001f, "genuine native Quaternion.Lerp");
                var clamped = LightweightTransform.Lerp(original, target, 2f);
                Vec(clamped.Location, target.Location, "Lerp clamps position upper factor");
                Check(Quaternion.Angle(clamped.Orientation, target.Orientation) < 0.001f, "shared Quaternion.Lerp upper factor");
                original.SmoothTowards(target, 2f, 15f, false);
                Vec(original.Location, new Vector3(2f, 0f, 0f), "genuine engine positive rotation preserves position order");
                Check(Math.Abs(Quaternion.Angle(Quaternion.identity, original.Orientation) - 15f) < 0.001f, "genuine shared RotateTowards cap");
                original.ApplyTo(obj.transform);
                Vec(obj.transform.position, original.Location, "genuine ordered position setter");
                Check(Quaternion.Angle(obj.transform.rotation, original.Orientation) < 0.001f, "genuine ordered rotation setter");
                var fresh = new LightweightTransform(obj.transform);
                Vec(fresh.Location, original.Location, "genuine Transform roundtrip");
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); }
            return checks;
        }
    }
}
