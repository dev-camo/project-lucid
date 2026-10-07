using System;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Bounded real API/null and natural comparer proof. No fabricated ribbon owner.
    public static class RibbonSharedRuntimeVerification
    {
        private static int count;
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            ++count;
        }
        private static readonly Type Runtime = typeof(IRibbon).Assembly.GetType("Hardlight.RibbonRuntimeComponent", true);
        private static MethodInfo Method(string name, params Type[] types) => Runtime.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, types, null);
        private static void Fault(string name, object[] arguments, params Type[] types)
        {
            try { Method(name, types).Invoke(null, arguments); throw new Exception("Expected null receiver fault: " + name); }
            catch (TargetInvocationException e) { Check(e.InnerException is NullReferenceException, name + " faults through the original null receiver"); }
        }
        public static int RunManaged()
        {
            count = 0;
            MethodInfo edge = Method("IsEndEdge", typeof(IRibbonRuntimeHandle), typeof(KnotT));
            float[] values = { float.NaN, float.NegativeInfinity, -1f, -0f, 0f, .001f, .0010001f, .5f, .999f, 1f, float.PositiveInfinity };
            foreach (float value in values)
                Check((bool)edge.Invoke(null, new object[] { null, new KnotT(0, value) }) == (value <= .001f), "first-knot threshold keeps the original ordered predicate");
            foreach (int index in new[] { -1, 1, 2, int.MinValue, int.MaxValue })
                foreach (float value in new[] { float.NaN, 0f, 1f })
                    Fault("IsEndEdge", new object[] { null, new KnotT(index, value) }, typeof(IRibbonRuntimeHandle), typeof(KnotT));
            Type cacheType = Runtime.GetNestedType("<>c", BindingFlags.NonPublic);
            object cache = cacheType.GetField("<>9", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            MethodInfo comparer = cacheType.GetMethod("<SortKnotBounds>b__10_0", BindingFlags.NonPublic | BindingFlags.Instance);
            Check(cache != null && comparer != null, "natural original callback/cache identity remains exact without ordinal padding");
            foreach (float left in values)
                foreach (float right in values)
                {
                    int actual = (int)comparer.Invoke(cache, new object[] { new SortKnots { KnotIndex = 7, SqrDistance = left }, new SortKnots { KnotIndex = -8, SqrDistance = right } });
                    Check(actual == left.CompareTo(right), "native Single.CompareTo ordering includes unordered and signed zero");
                }
            foreach (int left in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
                foreach (int right in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
                    Check((int)comparer.Invoke(cache, new object[] { new SortKnots { KnotIndex = left, SqrDistance = 4f }, new SortKnots { KnotIndex = right, SqrDistance = 4f } }) == 0, "equal distances do not use a fabricated knot-index tie breaker");
            Fault("FillKnotBounds", new object[] { null, null }, typeof(IRibbonRuntimeHandle), typeof(System.Collections.Generic.List<Bounds>));
            Fault("GetRibbonLocationFromDistance", new object[] { null, null, 0f, RibbonAlignment.Center }, typeof(IRibbon), typeof(ISplineRuntimeHandle), typeof(float), typeof(RibbonAlignment));
            Fault("FindNearestBlockKnotPositionFromCentre", new object[] { null, Vector3.zero, 0, null, null }, typeof(IRibbonRuntimeHandle), typeof(Vector3), typeof(int), typeof(Vector3).MakeByRefType(), typeof(PositionBoundsInfo).MakeByRefType());
            Fault("FindAdjacentRibbonPosition", new object[] { null, Vector3.zero, Vector3.forward, null, null }, typeof(IRibbonRuntimeHandle), typeof(Vector3), typeof(Vector3), typeof(Vector3).MakeByRefType(), typeof(int).MakeByRefType());
            return count;
        }
        public static void Run()
        {
            int checks = RunManaged();
            Debug.Log("Original ribbon helper bounded checks: " + checks + "; concrete provider/component geometry remains pending.");
        }
    }
}
