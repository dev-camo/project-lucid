using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;

namespace ProjectLucid.Editor
{
    // These checks exercise scalar records, the native-derived comparer, and null faults.
    // Non-null original spline handles require their complete genuine owning classes.
    public static class InertSplinePrerequisiteVerification
    {
        private static int checks;

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Inert spline prerequisite proof: " + message);
            ++checks;
        }

        private static void NullFault(Func<object> action, string message)
        {
            try { action(); }
            catch (TargetInvocationException ex) when (ex.InnerException is NullReferenceException)
            { Check(true, message); return; }
            catch (NullReferenceException) { Check(true, message); return; }
            throw new InvalidOperationException("Inert spline prerequisite proof: " + message);
        }

        public static int RunManaged()
        {
            checks = 0;
            Assembly assembly = typeof(SplineLocation).Assembly;
            Type component = assembly.GetType("Hardlight.InertSplineRuntimeComponent", true);
            Type comparerType = component.GetNestedType("FloatComparerFast", BindingFlags.NonPublic);
            Check(comparerType != null, "genuine nested comparer");
            var comparer = (IComparer<float>)Activator.CreateInstance(comparerType);
            float[] values = { float.NegativeInfinity, -2f, -0f, 0f, 2f, float.PositiveInfinity, float.NaN };
            foreach (float a in values)
                foreach (float b in values)
                {
                    int expected;
                    if (float.IsNaN(a) || float.IsNaN(b)) expected = 1;
                    else if (a == b) expected = 0;
                    else expected = a < b ? -1 : 1;
                    Check(comparer.Compare(a, b) == expected, "native comparison including unordered operands");
                }
            Check(comparer.Compare(float.NaN, float.NaN) == 1, "NaN is not equal to itself");
            Check(comparer.Compare(-0f, 0f) == 0, "signed zero equality");
            float[] lut = { 0f, 0.25f, 0.75f, 1f };
            Check(Array.BinarySearch(lut, 0.25f, comparer) == 1, "exact LUT match");
            Check(Array.BinarySearch(lut, 0.5f, comparer) == ~2, "interior insertion point");
            Check(Array.BinarySearch(lut, -1f, comparer) == ~0, "insertion before beginning");
            Check(Array.BinarySearch(lut, 2f, comparer) == ~4, "insertion beyond end");
            Check(Array.BinarySearch(lut, float.NaN, comparer) == ~0, "unordered search uses original comparer");

            FieldInfo comparerField = component.GetField("s_comparer", BindingFlags.NonPublic | BindingFlags.Static);
            Check(comparerField != null && comparerField.IsInitOnly, "static readonly comparer field");
            Check(comparerField.FieldType == comparerType, "concrete original field type");
            Check(comparerField.GetValue(null) is IComparer<float>, "genuine field initializer");

            SplineLocation location = default;
            Check(location.m_spline == null, "default record has null genuine owner");
            Check(location.m_knotLinearRatio.KnotIndex == 0 && location.m_knotLinearRatio.T == 0f,
                "default linear coordinates");
            Check(location.m_metadata.Equals(default(SurfaceKnotMetadata)), "default original metadata record");
            NullFault(() => location.KnotDistance, "KnotDistance null owner fault");
            NullFault(() => location.SplineDistance, "SplineDistance null owner fault");
            NullFault(() => location.GetLocalTransform(), "GetLocalTransform null owner fault");
            NullFault(() => component.GetMethod("GetLinearRatioFromKnotT", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { null, new KnotT(-2, 0.5f) }), "inert ratio null owner fault");
            NullFault(() => component.GetMethod("GetKnotTFromLinearRatio", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { null, new LinearRatio(-2, 0.5f) }), "inert parameter null owner fault");
            NullFault(() => component.GetMethod("GetLinearRatioFromLerpT", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { null, new KnotT(-2, 0.5f), -1 }), "private interpolation null owner fault");

            Check(typeof(ISpline).GetInterfaces().Length == 1 && typeof(ISpline).GetInterfaces()[0] == typeof(ISurface),
                "real inherited surface contract");
            Check(typeof(IInertSplineRuntimeHandle).GetInterfaces().Length == 1 &&
                typeof(IInertSplineRuntimeHandle).GetInterfaces()[0] == typeof(ISplineRuntimeHandle),
                "real inert runtime inheritance");
            Check(typeof(ISpline).GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length == 2,
                "two genuine spline events");
            Check(typeof(ISpline).GetEvent("OnSplineKnotsUpdated").EventHandlerType == typeof(Action),
                "knot callback type");
            Check(typeof(ISpline).GetEvent("OnSplineTypeChanged").EventHandlerType == typeof(Action<ISpline>),
                "type-change callback type");
            Type drawer = assembly.GetType("Hardlight.ISplineDrawerHandle", true);
            Check(drawer.IsInterface && drawer.GetMethods().Length == 0, "genuine empty drawer marker");
            Check(drawer.GetInterfaces().Length == 0, "no fabricated drawer base contracts");
            Check((int)SplineType.Bezier == 0 && (int)SplineType.CatmullRom == 1, "original enum literals");
            Check(Enum.GetNames(typeof(SplineType)).Length == 2, "no invented spline variants");
            Check(default(SortKnots).KnotIndex == 0 && default(SortKnots).SqrDistance == 0f, "sorting record zero state");
            Check(default(FindAdjacentResult).KnotT.KnotIndex == 0 && !default(FindAdjacentResult).HitOnKnot,
                "adjacency record zero state");
            Check(default(ISplineRuntimeHandle.KnotBounds).KnotIndex == 0 &&
                default(ISplineRuntimeHandle.KnotBounds).SqrDistanceToBounds == 0f, "bounds record zero state");
            return checks;
        }
    }
}
