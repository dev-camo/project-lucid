using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x060003dd..3e2. LUT getter calls stay separate.
    internal static class InertSplineRuntimeComponent
    {
        private static readonly FloatComparerFast s_comparer = new FloatComparerFast();

        private static LinearRatio GetLinearRatioFromLerpT(IInertSplineRuntimeHandle splineHandle,
            KnotT knotT, int lutIndex)
        {
            ISplineKnotRuntimeHandle knotHandle = splineHandle.GetKnotRuntimeHandle(knotT.KnotIndex);
            int lutCount = knotHandle.TLUT.Length;
            float t0 = knotHandle.GetLUTValue(lutIndex);
            float t1 = knotHandle.GetLUTValue(lutIndex + 1);
            return new LinearRatio(knotT.KnotIndex,
                (lutIndex + (knotT.T - t0) / (t1 - t0)) / lutCount);
        }

        internal static LinearRatio GetLinearRatioFromKnotT(IInertSplineRuntimeHandle splineHandle,
            KnotT knotT)
        {
            ISplineKnotRuntimeHandle knotHandle = splineHandle.GetKnotRuntimeHandle(knotT.KnotIndex);
            float t = knotT.T;
            if (knotHandle.GetLUTValue(0) <= t && knotHandle.GetLUTValue(1) > t)
                return GetLinearRatioFromLerpT(splineHandle, knotT, 0);

            int lutIndex = Array.BinarySearch(knotHandle.TLUT, t, s_comparer);
            int lutCount = knotHandle.TLUT.Length;
            if (lutIndex >= 0)
            {
                if (lutIndex < lutCount)
                    return new LinearRatio(knotT.KnotIndex, (float)lutIndex / lutCount);
            }
            else
            {
                lutIndex = ~lutIndex - 1;
                if (lutIndex < lutCount)
                    return GetLinearRatioFromLerpT(splineHandle, knotT, lutIndex);
            }
            return new LinearRatio(knotT.KnotIndex, 1f);
        }

        internal static KnotT GetKnotTFromLinearRatio(IInertSplineRuntimeHandle splineHandle,
            LinearRatio linearRatio)
        {
            ISplineKnotRuntimeHandle knotHandle = splineHandle.GetKnotRuntimeHandle(linearRatio.KnotIndex);
            int lutCount = knotHandle.TLUT.Length;
            float scaledRatio = linearRatio.T * lutCount;
            int lutIndex = (int)scaledRatio;
            float t0 = knotHandle.GetLUTValue(lutIndex);
            float t1 = knotHandle.GetLUTValue(lutIndex + 1);
            return new KnotT(linearRatio.KnotIndex, Mathf.Lerp(t0, t1, scaledRatio - lutIndex));
        }

        private class FloatComparerFast : IComparer<float>
        {
            public int Compare(float a, float b)
            {
                if (a == b) return 0;
                return a < b ? -1 : 1;
            }
        }
    }
}
