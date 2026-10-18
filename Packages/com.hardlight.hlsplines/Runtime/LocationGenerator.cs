using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Random = System.Random;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x02000023; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class LocationGenerator
    {
        public enum BiasType
        {
            WidthRatio,
            WorldUnit
        }

        public enum BiasDensity
        {
            Even,
            Left,
            Right,
            Edge,
            Center
        }

        private const float ZoneMult = 0.125f;
        private const float Zone1 = 0f;
        private const float Zone2 = 0.125f;
        private const float Zone3 = 0.25f;
        private const float Zone4 = 0.375f;
        private const float Zone5 = 0.5f;
        private const float Zone6 = 0.625f;
        private const float Zone7 = 0.75f;
        private const float Zone8 = 0.875f;

        // Weighted zone tables: each entry is the start of one of eight equal
        // width zones, repeated in proportion to how often that zone is picked.
        private static readonly float[] LeftBias =
        {
            Zone1, Zone1, Zone1, Zone1, Zone1, Zone1, Zone1, Zone1, Zone1, Zone1, Zone1, Zone1, Zone1, Zone1, Zone1,
            Zone2, Zone2, Zone2, Zone2, Zone2, Zone2, Zone2, Zone2, Zone2, Zone2, Zone2,
            Zone3, Zone3, Zone3, Zone3, Zone3, Zone3, Zone3,
            Zone4, Zone4, Zone4, Zone4, Zone4,
            Zone5, Zone5, Zone5,
            Zone6, Zone6,
            Zone7,
            Zone8
        };

        private static readonly float[] RightBias =
        {
            Zone1,
            Zone2,
            Zone3, Zone3,
            Zone4, Zone4, Zone4,
            Zone5, Zone5, Zone5, Zone5, Zone5,
            Zone6, Zone6, Zone6, Zone6, Zone6, Zone6, Zone6,
            Zone7, Zone7, Zone7, Zone7, Zone7, Zone7, Zone7, Zone7, Zone7, Zone7, Zone7,
            Zone8, Zone8, Zone8, Zone8, Zone8, Zone8, Zone8, Zone8, Zone8, Zone8, Zone8, Zone8, Zone8, Zone8, Zone8
        };

        private static readonly float[] EdgeBias =
        {
            Zone1, Zone1, Zone1, Zone1, Zone1, Zone1, Zone1, Zone1, Zone1, Zone1, Zone1, Zone1,
            Zone2, Zone2, Zone2, Zone2, Zone2,
            Zone3, Zone3,
            Zone4,
            Zone5,
            Zone6, Zone6,
            Zone7, Zone7, Zone7, Zone7, Zone7,
            Zone8, Zone8, Zone8, Zone8, Zone8, Zone8, Zone8, Zone8, Zone8, Zone8, Zone8, Zone8
        };

        private static readonly float[] CenterBias =
        {
            Zone1,
            Zone2, Zone2,
            Zone3, Zone3, Zone3, Zone3, Zone3,
            Zone4, Zone4, Zone4, Zone4, Zone4, Zone4, Zone4, Zone4, Zone4, Zone4, Zone4, Zone4,
            Zone5, Zone5, Zone5, Zone5, Zone5, Zone5, Zone5, Zone5, Zone5, Zone5, Zone5, Zone5,
            Zone6, Zone6, Zone6, Zone6, Zone6,
            Zone7, Zone7,
            Zone8
        };

        // Negative biases are measured against the left half-width, others
        // against the right; a missing half gives the centre line.
        private static float ConvertBiasToRatio(float bias, float leftMagnitude, float rightMagnitude)
        {
            if (bias < 0f)
                return leftMagnitude > 0f ? Mathf.Max(-1f, bias / leftMagnitude) : 0f;
            return rightMagnitude > 0f ? Mathf.Min(1f, bias / rightMagnitude) : 0f;
        }

        private static float ConvertBiasToRatio(float bias, float leftMagnitude, float rightMagnitude, out float worldUnitBias)
        {
            float ratio = ConvertBiasToRatio(bias, leftMagnitude, rightMagnitude);
            worldUnitBias = ratio * (bias < 0f ? leftMagnitude : rightMagnitude);
            return ratio;
        }

        // A biased density picks a weighted zone, then spreads evenly inside it.
        private static float GetTBias(Random rand, float biasMin, float biasRange, BiasDensity biasDensity)
        {
            float t = (float)rand.NextDouble();
            float[] zones;
            switch (biasDensity)
            {
                case BiasDensity.Even:
                    return biasMin + t * biasRange;
                case BiasDensity.Left:
                    zones = LeftBias;
                    break;
                case BiasDensity.Right:
                    zones = RightBias;
                    break;
                case BiasDensity.Edge:
                    zones = EdgeBias;
                    break;
                case BiasDensity.Center:
                    zones = CenterBias;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(biasDensity), biasDensity, null);
            }
            float scaled = t * zones.Length;
            int zone = (int)scaled;
            return biasMin + zones[zone] * biasRange + biasRange * ZoneMult * (scaled - (int)scaled);
        }

        public static List<LightweightTransform> RandomPositions(Random rand, IRibbon ribbon, int numberToGenerate, float biasMin = -1f, float biasMax = 1f, BiasType biasType = BiasType.WidthRatio, BiasDensity biasDensity = BiasDensity.Even, bool inverseBias = false, int startKnot = 0, int endKnot = -1)
        {
            var positions = new List<LightweightTransform>(numberToGenerate);
            if (endKnot == -1)
                endKnot = ribbon.KnotCount - 1;
            float knotStep = 1f / (numberToGenerate / (float)(endKnot - startKnot));

            if (biasType == BiasType.WorldUnit)
            {
                for (int i = 0; i < numberToGenerate; i++)
                {
                    var linearRatio = new LinearRatio((int)Math.Floor(i * knotStep) + startKnot, (float)rand.NextDouble());
                    LightweightTransform centre = ribbon.GetLocalTransformFromLinearRatio(linearRatio, RibbonAlignment.Center);
                    LightweightTransform left = ribbon.GetLocalTransformFromLinearRatio(linearRatio, RibbonAlignment.Left);
                    LightweightTransform right = ribbon.GetLocalTransformFromLinearRatio(linearRatio, RibbonAlignment.Right);
                    float leftMagnitude = (centre.Location - left.Location).magnitude;
                    float rightMagnitude = (centre.Location - right.Location).magnitude;
                    float minRatio = ConvertBiasToRatio(biasMin, leftMagnitude, rightMagnitude, out float minWorldUnitBias);
                    float maxRatio = ConvertBiasToRatio(biasMax, leftMagnitude, rightMagnitude, out float maxWorldUnitBias);
                    // Skipped when both limits, converted to ratios and scaled back by the
                    // chosen half-width, exceed the requested ones (limits past the left
                    // half-width, or a one-ulp rounding overshoot).
                    if (minWorldUnitBias > biasMin && maxWorldUnitBias > biasMax)
                        continue;

                    float ratioRange = maxRatio - minRatio;
                    float t;
                    if (inverseBias)
                    {
                        // Sample the excluded width, then shift past the kept band.
                        t = GetTBias(rand, -1f, 2f - ratioRange, biasDensity);
                        if (t > minRatio)
                            t = t - minRatio + maxRatio;
                    }
                    else
                    {
                        t = GetTBias(rand, minRatio, ratioRange, biasDensity);
                    }

                    LightweightTransform side = right;
                    if (t < 0f)
                    {
                        t = -t;
                        side = left;
                    }
                    positions.Add(new LightweightTransform(Vector3.LerpUnclamped(centre.Location, side.Location, t),
                        Quaternion.Slerp(centre.Orientation, side.Orientation, t)));
                }
            }
            else
            {
                float biasRange = biasMax - biasMin;
                for (int i = 0; i < numberToGenerate; i++)
                {
                    var linearRatio = new LinearRatio((int)Math.Floor(i * knotStep) + startKnot, (float)rand.NextDouble());
                    LightweightTransform centre = ribbon.GetLocalTransformFromLinearRatio(linearRatio, RibbonAlignment.Center);
                    float t;
                    if (inverseBias)
                    {
                        t = GetTBias(rand, -1f, 2f - biasRange, biasDensity);
                        if (t > biasMin)
                            t = t - biasMin + biasMax;
                    }
                    else
                    {
                        t = GetTBias(rand, biasMin, biasRange, biasDensity);
                    }

                    int sign = Math.Sign(t);
                    float sideRatio = t * sign;
                    LightweightTransform side = ribbon.GetLocalTransformFromLinearRatio(linearRatio, (RibbonAlignment)sign);
                    positions.Add(new LightweightTransform(Vector3.LerpUnclamped(centre.Location, side.Location, sideRatio),
                        Quaternion.Slerp(centre.Orientation, side.Orientation, sideRatio)));
                }
            }
            return positions;
        }

        public static List<LightweightTransform> RandomPositions(Random rand, ISpline spline, int numberToGenerate, int startKnot = 0, int endKnot = -1)
        {
            var positions = new List<LightweightTransform>(numberToGenerate);
            if (endKnot == -1)
                endKnot = spline.KnotCount - 1;
            float knotStep = 1f / (numberToGenerate / (float)(endKnot - startKnot));
            for (int i = 0; i < numberToGenerate; i++)
            {
                var linearRatio = new LinearRatio((int)Math.Floor(i * knotStep) + startKnot, (float)rand.NextDouble());
                LightweightTransform location = spline.GetLocalTransformFromLinearRatio(linearRatio);
                positions.Add(new LightweightTransform(location.Location, location.Orientation));
            }
            return positions;
        }

        // Rows run along each knot every strideZ metres, carrying the overshoot
        // into the next knot; knots before startKnot only advance the carry.
        public static List<LightweightTransform> GridPositions(IRibbon ribbon, float strideZ, float strideX, float biasMin = -1f, float biasMax = 1f, BiasType biasType = BiasType.WidthRatio, int startKnot = 0, int endKnot = -1)
        {
            var positions = new List<LightweightTransform>();
            if (endKnot == -1)
                endKnot = ribbon.KnotCount - 1;
            bool useStrideX = strideX >= float.Epsilon;
            bool includeCentre = biasMin <= 0f && biasMax >= 0f;
            if (!useStrideX && !includeCentre)
                return positions;

            float distance = strideZ * 0.5f;
            for (int knotIndex = 0; knotIndex < endKnot; knotIndex++)
            {
                float knotLength = ribbon.GetKnot(knotIndex, RibbonAlignment.Center).Length;
                float strideRatio = strideZ / knotLength;
                float t = distance / knotLength;
                if (knotIndex < startKnot)
                {
                    t += Mathf.Ceil((1f - t) / strideRatio) * strideRatio;
                }
                else
                {
                    for (; t <= 1f; t += strideRatio)
                    {
                        var linearRatio = new LinearRatio(knotIndex, t);
                        LightweightTransform centre = ribbon.GetLocalTransformFromLinearRatio(linearRatio, RibbonAlignment.Center);
                        if (includeCentre)
                            positions.Add(new LightweightTransform(centre.Location, centre.Orientation));
                        if (!useStrideX)
                            continue;

                        LightweightTransform right = ribbon.GetLocalTransformFromLinearRatio(linearRatio, RibbonAlignment.Right);
                        Vector3 toRight = right.Location - centre.Location;
                        float rightWidth = toRight.magnitude;
                        LightweightTransform left = ribbon.GetLocalTransformFromLinearRatio(linearRatio, RibbonAlignment.Left);
                        Vector3 toLeft = left.Location - centre.Location;
                        float leftWidth = toLeft.magnitude;

                        float minRatio = biasMin;
                        float maxRatio = biasMax;
                        if (biasType == BiasType.WorldUnit)
                        {
                            minRatio = ConvertBiasToRatio(biasMin, leftWidth, rightWidth);
                            maxRatio = ConvertBiasToRatio(biasMax, leftWidth, rightWidth);
                        }

                        if (rightWidth > 0f)
                        {
                            float stepRatio = strideX / rightWidth;
                            for (float x = stepRatio; x <= maxRatio; x += stepRatio)
                            {
                                if (x >= minRatio)
                                    positions.Add(new LightweightTransform(centre.Location + toRight * x,
                                        Quaternion.Slerp(centre.Orientation, right.Orientation, x)));
                            }
                        }
                        if (leftWidth > 0f)
                        {
                            float stepRatio = strideX / leftWidth;
                            for (float x = stepRatio; x <= -minRatio; x += stepRatio)
                            {
                                if (x >= -maxRatio)
                                    positions.Add(new LightweightTransform(centre.Location + toLeft * x,
                                        Quaternion.Slerp(centre.Orientation, left.Orientation, x)));
                            }
                        }
                    }
                }
                distance = (t - 1f) * knotLength;
            }
            return positions;
        }

        public static List<LightweightTransform> GridPositions(ISpline spline, float strideZ, int startKnot = 0, int endKnot = -1)
        {
            var positions = new List<LightweightTransform>();
            if (endKnot == -1)
                endKnot = spline.KnotCount - 1;
            float distance = strideZ * 0.5f;
            for (int knotIndex = 0; knotIndex < endKnot; knotIndex++)
            {
                float knotLength = spline.GetKnot(knotIndex).Length;
                float strideRatio = strideZ / knotLength;
                float t = distance / knotLength;
                if (knotIndex < startKnot)
                {
                    t += Mathf.Ceil((1f - t) / strideRatio) * strideRatio;
                }
                else
                {
                    for (; t <= 1f; t += strideRatio)
                    {
                        LightweightTransform location = spline.GetLocalTransformFromLinearRatio(new LinearRatio(knotIndex, t));
                        positions.Add(new LightweightTransform(location.Location, location.Orientation));
                    }
                }
                distance = (t - 1f) * knotLength;
            }
            return positions;
        }

        // Area-weighted triangle pick, then a uniform point inside it facing
        // along the triangle normal.
        public static List<LightweightTransform> RandomPositions(Random rand, Mesh mesh, int numberToGenerate)
        {
            var positions = new List<LightweightTransform>(numberToGenerate);
            float[] triSizes = GetTriSizes(mesh.triangles, mesh.vertices);
            float[] cumulativeSizes = new float[triSizes.Length];
            float totalSize = 0f;
            for (int i = 0; i < triSizes.Length; i++)
            {
                totalSize += triSizes[i];
                cumulativeSizes[i] = totalSize;
            }

            for (int i = 0; i < numberToGenerate; i++)
            {
                float target = (float)(rand.NextDouble() * totalSize);
                int low = 0;
                int high = cumulativeSizes.Length - 1;
                int tri = (low + high) / 2;
                while (low <= high)
                {
                    if (cumulativeSizes[tri] > target)
                        high = tri - 1;
                    else
                        low = tri + 1;
                    tri = (low + high) / 2;
                }
                if (target > cumulativeSizes[tri])
                    tri++;

                Vector3 a = mesh.vertices[mesh.triangles[tri * 3]];
                Vector3 b = mesh.vertices[mesh.triangles[tri * 3 + 1]];
                Vector3 c = mesh.vertices[mesh.triangles[tri * 3 + 2]];
                float u = (float)rand.NextDouble();
                float v = (float)rand.NextDouble();
                // Fold samples from the far half of the parallelogram back inside.
                if (u + v >= 1f)
                {
                    u = 1f - u;
                    v = 1f - v;
                }
                Vector3 ab = b - a;
                Vector3 ac = c - a;
                Vector3 normal = Vector3.Cross(ab, ac).normalized;
                Vector3 position = a + ab * u + ac * v;
                positions.Add(new LightweightTransform(position, Quaternion.FromToRotation(Vector3.up, normal)));
            }
            return positions;
        }

        private static float[] GetTriSizes(IReadOnlyList<int> tris, IReadOnlyList<Vector3> verts)
        {
            int triCount = tris.Count / 3;
            float[] sizes = new float[triCount];
            for (int i = 0; i < triCount; i++)
            {
                sizes[i] = Vector3.Cross(verts[tris[i * 3 + 1]] - verts[tris[i * 3]],
                    verts[tris[i * 3 + 2]] - verts[tris[i * 3]]).magnitude * 0.5f;
            }
            return sizes;
        }
    }
}
