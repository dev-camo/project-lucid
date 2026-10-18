using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000aaa; the shipped GenerateKnots (06003d7d, 0x6DAB30) returns
    // without adding knots.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class SurfacePathAnimCurveGenerator : SurfacePathGenerator
    {
        public override void GenerateKnots(List<LightweightTransform> knots)
        {
        }
    }
}
