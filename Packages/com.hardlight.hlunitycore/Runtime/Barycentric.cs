using UnityEngine;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0x0200017e, methods 0x06000a9c..0x06000aa6.
    // Coordinates of aP relative to triangle (aV1, aV2, aV3): u weights aV1,
    // v weights aV2 and w = 1 - u - v weights aV3. A degenerate triangle
    // divides by zero and keeps the resulting infinities/NaNs.
    public class Barycentric
    {
        public float u;
        public float v;
        public float w;

        // 0x06000a9c.
        public Barycentric(float aU, float aV, float aW)
        {
            u = aU;
            v = aV;
            w = aW;
        }

        // 0x06000a9d (x86-64 0x1b03ff0).
        public Barycentric(Vector2 aV1, Vector2 aV2, Vector2 aV3, Vector2 aP)
        {
            Vector2 a = aV2 - aV3, b = aV1 - aV3, c = aP - aV3;
            float dotAA = Vector2.Dot(a, a);
            float dotBB = Vector2.Dot(b, b);
            float dotAB = Vector2.Dot(a, b);
            float dotAC = Vector2.Dot(a, c);
            float dotBC = Vector2.Dot(b, c);
            float d = dotAA * dotBB - dotAB * dotAB;
            u = (dotAA * dotBC - dotAB * dotAC) / d;
            v = (dotBB * dotAC - dotAB * dotBC) / d;
            w = 1.0f - u - v;
        }

        // 0x06000a9e (x86-64 0x1b040e0).
        public Barycentric(Vector3 aV1, Vector3 aV2, Vector3 aV3, Vector3 aP)
        {
            Vector3 a = aV2 - aV3, b = aV1 - aV3, c = aP - aV3;
            float dotAA = Vector3.Dot(a, a);
            float dotBB = Vector3.Dot(b, b);
            float dotAB = Vector3.Dot(a, b);
            float dotAC = Vector3.Dot(a, c);
            float dotBC = Vector3.Dot(b, c);
            float d = dotAA * dotBB - dotAB * dotAB;
            u = (dotAA * dotBC - dotAB * dotAC) / d;
            v = (dotBB * dotAC - dotAB * dotBC) / d;
            w = 1.0f - u - v;
        }

        // 0x06000a9f (x86-64 0x1b04250).
        public Barycentric(Vector4 aV1, Vector4 aV2, Vector4 aV3, Vector4 aP)
        {
            Vector4 a = aV2 - aV3, b = aV1 - aV3, c = aP - aV3;
            float dotAA = Vector4.Dot(a, a);
            float dotBB = Vector4.Dot(b, b);
            float dotAB = Vector4.Dot(a, b);
            float dotAC = Vector4.Dot(a, c);
            float dotBC = Vector4.Dot(b, c);
            float d = dotAA * dotBB - dotAB * dotAB;
            u = (dotAA * dotBC - dotAB * dotAC) / d;
            v = (dotBB * dotAC - dotAB * dotBC) / d;
            w = 1.0f - u - v;
        }

        // 0x06000aa0 (x86-64 0x1b04400).
        public Barycentric(Color aV1, Color aV2, Color aV3, Color aP)
        {
            Color a = aV2 - aV3, b = aV1 - aV3, c = aP - aV3;
            // Color has no Dot, so the products are written out in the same order.
            float dotAA = a.r * a.r + a.g * a.g + a.b * a.b + a.a * a.a;
            float dotBB = b.r * b.r + b.g * b.g + b.b * b.b + b.a * b.a;
            float dotAB = a.r * b.r + a.g * b.g + a.b * b.b + a.a * b.a;
            float dotAC = a.r * c.r + a.g * c.g + a.b * c.b + a.a * c.a;
            float dotBC = b.r * c.r + b.g * c.g + b.b * c.b + b.a * c.a;
            float d = dotAA * dotBB - dotAB * dotAB;
            u = (dotAA * dotBC - dotAB * dotAC) / d;
            v = (dotBB * dotAC - dotAB * dotBC) / d;
            w = 1.0f - u - v;
        }

        // 0x06000aa1: all three weights in the open range, with tolerance.
        public bool IsInside => IsNormalisedRange(u) && IsNormalisedRange(v) && IsNormalisedRange(w);

        // 0x06000aa2: strict comparisons against -0.0001f and 1.0001f (native
        // constants 0xb8d1b717 and 0x3f800347), so NaN is outside.
        private bool IsNormalisedRange(float t) => t > -0.0001f && t < 1.0001f;

        // 0x06000aa3..0x06000aa6.
        public Vector2 Interpolate(Vector2 v1, Vector2 v2, Vector2 v3) => v1 * u + v2 * v + v3 * w;
        public Vector3 Interpolate(Vector3 v1, Vector3 v2, Vector3 v3) => v1 * u + v2 * v + v3 * w;
        public Vector4 Interpolate(Vector4 v1, Vector4 v2, Vector4 v3) => v1 * u + v2 * v + v3 * w;
        public Color Interpolate(Color v1, Color v2, Color v3) => v1 * u + v2 * v + v3 * w;
    }
}
