using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Uses the genuine original two-method helper and fresh transient curves only.
    // Fixed linear and constant witnesses do not call a second integration routine.
    public static class OriginalAnimationCurveVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        public static int RunDeclarations()
        {
            int checks = 0;
            Type type = typeof(AnimationCurveExtensions);
            Require(type.FullName == "Hardlight.AnimationCurveExtensions" && type.Assembly.GetName().Name == "HLUnityCore.Runtime" && (int)type.Attributes == 1048961, "complete original utility identity and flags", ref checks);
            Require(type.GetFields(Own).Length == 0 && type.GetConstructors(Own).Length == 0, "original fieldless static class", ref checks);
            MethodInfo[] methods = type.GetMethods(Own);
            Require(methods.Length == 2 && methods.Select(x => x.Name).OrderBy(x => x).SequenceEqual(new[] { "Area", "TotalTime" }), "all two original declarations", ref checks);
            Require(type.GetCustomAttributesData().Count == 1 && type.IsDefined(typeof(ExtensionAttribute), false), "natural original extension type attribute", ref checks);
            foreach (MethodInfo method in methods)
            {
                Require((int)method.Attributes == 150 && method.ReturnType == typeof(float) && !method.IsGenericMethod, method.Name + " exact access and return type", ref checks);
                Require(method.GetCustomAttributesData().Count == 1 && method.IsDefined(typeof(ExtensionAttribute), false), method.Name + " original extension attribute", ref checks);
            }
            ParameterInfo[] total = type.GetMethod("TotalTime").GetParameters();
            Require(total.Length == 1 && total[0].Name == "animCurve" && total[0].ParameterType == typeof(AnimationCurve) && (int)total[0].Attributes == 0 && !total[0].HasDefaultValue, "TotalTime original parameter", ref checks);
            ParameterInfo[] area = type.GetMethod("Area").GetParameters();
            string[] names = { "animCurve", "timeStart", "timeEnd", "timeStep" };
            Require(area.Length == 4 && area[0].Name == names[0] && area[0].ParameterType == typeof(AnimationCurve) && (int)area[0].Attributes == 0 && !area[0].HasDefaultValue, "Area original curve parameter", ref checks);
            for (int i = 1; i < 4; i++)
                Require(area[i].Name == names[i] && area[i].ParameterType == typeof(float) && (int)area[i].Attributes == 4112 && area[i].HasDefaultValue, "Area original optional parameter " + names[i], ref checks);
            Require((float)area[1].DefaultValue == 0f, "original timeStart default", ref checks);
            Require((float)area[2].DefaultValue == 0f, "original timeEnd default", ref checks);
            Require((float)area[3].DefaultValue == 0.1f, "original timeStep default", ref checks);
            return checks;
        }

        public static int RunNullBoundaries()
        {
            int checks = 0;
            Throws<NullReferenceException>(() => AnimationCurveExtensions.TotalTime(null), "TotalTime null length access", ref checks);
            Throws<NullReferenceException>(() => AnimationCurveExtensions.Area(null, 2f, 1f, -1f), "Area reads TotalTime before end and step decisions", ref checks);
            return checks;
        }

        public static int RunFinalKeyTimes()
        {
            int checks = 0;
            Require(new AnimationCurve().TotalTime() == 0f, "empty curve returns zero", ref checks);
            Require(new AnimationCurve(new Keyframe(-2f, 8f)).TotalTime() == -2f, "single negative key time is retained", ref checks);
            var unsorted = new AnimationCurve(new Keyframe(3f, 0f), new Keyframe(-4f, 1f), new Keyframe(1f, 2f));
            Require(unsorted[0].time == -4f && unsorted[2].time == 3f, "real Unity curve sorts the supplied key times", ref checks);
            Require(unsorted.TotalTime() == 3f, "final sorted key time", ref checks);
            Require(new AnimationCurve(new Keyframe(2f, 0f), new Keyframe(3f, 1f)).TotalTime() == 3f, "total time is final time rather than duration", ref checks);
            Require(new AnimationCurve(new Keyframe(-4f, 0f), new Keyframe(-2f, 1f)).TotalTime() == -2f, "negative final time", ref checks);
            Require(new AnimationCurve(new Keyframe(0f, 9f)).TotalTime() == 0f, "single zero-time key", ref checks);
            unsorted.keys = new[] { new Keyframe(7f, 0f), new Keyframe(1f, 1f) };
            Require(unsorted.TotalTime() == 7f, "reads current keys after replacement", ref checks);
            return checks;
        }

        public static int RunAreaArithmetic()
        {
            int checks = 0;
            var linear = new AnimationCurve(new Keyframe(0f, 0f, 1f, 1f), new Keyframe(1f, 1f, 1f, 1f));
            Near(linear.Evaluate(0f), 0f, "real linear curve origin", ref checks);
            Near(linear.Evaluate(0.25f), 0.25f, "real linear curve interior", ref checks);
            Near(linear.Evaluate(1f), 1f, "real linear curve endpoint", ref checks);
            Near(linear.Area(0f, 0f, 0.5f), 0.5f, "two full intervals", ref checks);
            // Original scalar instructions give 9/50 + 22/75 = 71/150 here.
            Near(linear.Area(0f, 0f, 0.6f), 71f / 150f, "clipped final interval retains full-step slope divisor", ref checks);
            Near(linear.Area(0f, 0f, 2f), 0.25f, "one oversized step retains full-step divisor", ref checks);
            Near(linear.Area(0f, 0.5f, 1f), 0.0625f, "requested end clamps the first interval", ref checks);
            Near(linear.Area(0.25f, 0.75f, 0.5f), 0.25f, "nonzero start retains the intercept term", ref checks);
            Near(linear.Area(0.75f, 0.5f, 0.25f), 0.21875f, "end before start falls back to final key time", ref checks);
            Near(linear.Area(0f, 2f, 0.5f), 0.5f, "end beyond the final key falls back", ref checks);
            Near(linear.Area(0.5f, 0.5f, 0.5f), 0.375f, "equal start and end falls back", ref checks);
            Near(linear.Area(0f, float.NaN, 0.5f), 0.5f, "unordered requested end falls back", ref checks);
            var constant = new AnimationCurve(new Keyframe(0f, 2f, 0f, 0f), new Keyframe(1f, 2f, 0f, 0f));
            Near(constant.Area(), 2f, "all original optional defaults", ref checks);
            var negative = new AnimationCurve(new Keyframe(0f, -2f, 0f, 0f), new Keyframe(1f, -2f, 0f, 0f));
            Near(negative.Area(0f, 0f, 0.5f), -2f, "signed area", ref checks);
            Near(linear.Area(0f, 0f, float.PositiveInfinity), 0f, "infinite step retains zero slope", ref checks);
            Require(float.IsNaN(linear.Area(0f, 0f, float.NaN)), "unordered step propagates through one interval", ref checks);
            Near(new AnimationCurve().Area(), 0f, "empty curve still evaluates before a suppressed loop", ref checks);
            Near(linear.Area(1f, 0f, 0f), 0f, "zero step is accepted when no interval is entered", ref checks);
            Near(linear.Area(1f, 0f, -1f), 0f, "negative step is accepted when no interval is entered", ref checks);
            Near(linear.Area(float.NaN, 0f, 0.5f), 0f, "unordered start suppresses the interval loop", ref checks);
            // Zero and negative steps with an active interval can run indefinitely in
            // the original and are deliberately outside these bounded engine checks.
            return checks;
        }

        private static void Near(float actual, float expected, string label, ref int checks)
        { Require(!float.IsNaN(actual) && Math.Abs(actual - expected) <= 0.00001f, label + " expected " + expected + ", got " + actual, ref checks); }
        private static void Throws<T>(Action action, string label, ref int checks) where T : Exception
        {
            try { action(); }
            catch (T) { Require(true, label, ref checks); return; }
            throw new InvalidOperationException(label + " did not throw " + typeof(T).Name);
        }
        private static void Require(bool condition, string label, ref int checks)
        { if (!condition) throw new InvalidOperationException(label); checks++; }
    }
}
