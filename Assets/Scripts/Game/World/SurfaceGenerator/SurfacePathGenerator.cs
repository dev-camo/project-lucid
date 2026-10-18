using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000aad; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public abstract class SurfacePathGenerator
    {
        public enum AxisAlignment
        {
            XZ = 0,
            XY = 1,
            YX = 2,
            YZ = 3,
            ZX = 4,
            ZY = 5
        }

        [SerializeField]
        private AxisAlignment m_axisAlignment;

        public abstract void GenerateKnots(List<LightweightTransform> knots);

        // 06003d87 (0x6DB380): a path continues from the last knot already generated.
        protected void GetOrigin(out Vector3 originPosition, out Quaternion originRotation, List<LightweightTransform> knots)
        {
            if (knots.Count > 0)
            {
                LightweightTransform lastKnot = knots[knots.Count - 1];
                originPosition = lastKnot.Location;
                originRotation = lastKnot.Orientation;
            }
            else
            {
                originPosition = Vector3.zero;
                originRotation = Quaternion.identity;
            }
        }

        // 06003d88 (0x6DB600): v0 is negated for clockwise paths before it is placed on the
        // aligned axes; an undefined alignment uses the XZ placement.
        protected Vector3 GetAxis(float v0, float v1, bool clockwise)
        {
            if (clockwise)
                v0 = -v0;
            switch (m_axisAlignment)
            {
                case AxisAlignment.XY: return new Vector3(v0, v1, 0f);
                case AxisAlignment.YX: return new Vector3(v1, v0, 0f);
                case AxisAlignment.YZ: return new Vector3(0f, v0, v1);
                case AxisAlignment.ZX: return new Vector3(v1, 0f, v0);
                case AxisAlignment.ZY: return new Vector3(0f, v1, v0);
                default: return new Vector3(v0, 0f, v1);
            }
        }
    }
}
