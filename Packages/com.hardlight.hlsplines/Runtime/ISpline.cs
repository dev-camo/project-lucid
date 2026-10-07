using System;
using UnityEngine;

namespace Hardlight
{
    public interface ISpline : ISurface
    {
        LightweightTransform Transform { get; }
        SplineType SplineType { get; }
        int KnotCount { get; }
        ISplineKnot GetBackKnot();
        ISplineKnot GetHeadKnot();
        ISplineKnot GetKnot(int knotIndex);
        SurfaceKnotMetadata GetKnotMetadataFromDistance(float distance);
        SurfaceKnotMetadata GetKnotMetadataFromKnotT(KnotT knotT);
        SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(LinearRatio linearRatio);
        SplineLocation FindNearestSplineLocation(Vector3 worldPosition);
        SplineLocation FindAdjacentSplineLocation(Vector3 worldPosition, Vector3 forward);
        SplineLocation GetSplineLocationFromDistance(float distance);
        LinearRatio GetLinearRatioFromKnotT(KnotT knotT);
        KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio);
        PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT);
        LightweightTransform GetLocalTransformFromKnotT(KnotT knotT);
        Vector3 GetLocalPosFromKnotT(KnotT knotT);
        PositionAndTangent GetLocalPosAndTangentFromLinearRatio(LinearRatio linearRatio);
        LightweightTransform GetLocalTransformFromLinearRatio(LinearRatio linearRatio);
        Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio);
        LightweightTransform GetWorldTransformFromDistance(float distance);
        LightweightTransform GetWorldTransformFromSplineT(float splineT);
        void KnotsUpdated();
        void UpdateSplineType(ISpline newSpline);
        event Action OnSplineKnotsUpdated;
        event Action<ISpline> OnSplineTypeChanged;
    }
}
