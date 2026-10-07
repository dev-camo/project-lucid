// Original HLUnityCore.Runtime AnimationCurveExtensions (0x02000058), both declared methods.
// Retains the original integration formula, step handling and endpoint comparisons.
using UnityEngine;

namespace Hardlight
{
    public static class AnimationCurveExtensions
    {
        // Original 0x0600023b: an empty curve has no final key; null fails on length access.
        public static float TotalTime(this AnimationCurve animCurve)
        {
            if (animCurve.length == 0) return 0f;
            return animCurve[animCurve.length - 1].time;
        }

        // Original 0x0600023c. The final interval clamps its sampled endpoint, while the
        // slope still divides by timeStep. Keep this arithmetic and the unclamped advance.
        // The original accepts every timeStep; zero or negative steps can fail to terminate.
        public static float Area(this AnimationCurve animCurve, float timeStart = 0f,
            float timeEnd = 0f, float timeStep = 0.1f)
        {
            float totalTime = animCurve.TotalTime();
            timeEnd = timeEnd < totalTime && timeEnd > timeStart ? timeEnd : totalTime;
            float value = animCurve.Evaluate(timeStart);
            float area = 0f;
            while (timeStart < timeEnd)
            {
                float nextTime = Mathf.Min(timeStart + timeStep, timeEnd);
                float nextValue = animCurve.Evaluate(nextTime);
                float gradient = (nextValue - value) / timeStep;
                float intercept = value - timeStart * gradient;
                area += (nextTime * nextTime - timeStart * timeStart) * (gradient * 0.5f)
                    + (nextTime - timeStart) * intercept;
                timeStart += timeStep;
                value = nextValue;
            }
            return area;
        }
    }
}
