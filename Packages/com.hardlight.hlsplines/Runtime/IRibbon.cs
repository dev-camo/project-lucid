using System;
using UnityEngine;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x02000065: all twenty-eight own abstract contracts.
    public interface IRibbon : ISurface
    {
        LightweightTransform Transform { get; }
        int KnotCount { get; }
        IRibbonKnot GetBackKnot(RibbonAlignment alignment);
        IRibbonKnot GetHeadKnot(RibbonAlignment alignment);
        IRibbonKnot GetKnot(int knotIndex, RibbonAlignment alignment);
        SurfaceKnotMetadata GetKnotMetadataFromDistance(float distance);
        SurfaceKnotMetadata GetKnotMetadataFromKnotT(KnotT knotT);
        SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(LinearRatio linearRatio);
        RibbonLocation FindNearestRibbonLocation(Vector3 worldPosition, RibbonAlignment alignment);
        RibbonLocation FindAdjacentRibbonLocation(Vector3 worldPosition, Vector3 forward, RibbonAlignment alignment);
        RibbonLocation GetRibbonLocationFromDistance(float distance, RibbonAlignment alignment);
        LinearRatio GetLinearRatioFromKnotT(KnotT knotT, RibbonAlignment alignment);
        KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment);
        PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT, RibbonAlignment alignment);
        LightweightTransform GetLocalTransformFromKnotT(KnotT knotT, RibbonAlignment alignment);
        Vector3 GetLocalPosFromKnotT(KnotT knotT, RibbonAlignment alignment);
        PositionAndTangent GetLocalPosAndTangentFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment);
        LightweightTransform GetLocalTransformFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment);
        Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio, RibbonAlignment alignment);
        LightweightTransform GetWorldTransformFromDistance(float distance);
        LightweightTransform GetWorldTransformFromRibbonT(float ribbonT);
        void KnotsUpdated();
        void UpdateRibbonType(IRibbon newRibbon);
        bool TryGetRaycastHit(Ray ray, float distance, out RaycastHit raycastHit);
        event Action OnRibbonKnotsUpdated;
        event Action<IRibbon> OnRibbonTypeChanged;
    }
}
