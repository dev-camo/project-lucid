using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x0200009d, complete 56-method owner including its callback.
    // Bodies inferred from complete ARM64/x86 captures; authored text and native fault scheduling remain qualified.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [ExecuteInEditMode]
    public class InertSpline : SplineMonoBase, ISpline, ISurface, IInertSplineRuntimeHandle, ISplineRuntimeHandle
    {
        [SerializeField] private DateTime m_timestamp;
        [SerializeField] private LightweightTransform m_transform;
        [SerializeField] private InertSplineKnot[] m_knots;
        [SerializeField] private float m_lengthInverse;
        private List<ISplineRuntimeHandle.KnotBounds> m_knotBoundsList;

        public LightweightTransform Transform => m_transform;
        public InertSplineKnot[] Knots => m_knots;
        public InertSplineKnot this[int knotIndex] => m_knots[knotIndex];
        public int KnotCount => m_knots == null ? 0 : m_knots.Length;
        public float LengthInverse => m_lengthInverse;
        public int LUTCount { get; private set; }

        public event Action OnSplineKnotsUpdated;
        public event Action<ISpline> OnSplineTypeChanged;

        public ISplineKnot GetHeadKnot() => GetKnot(0);
        public ISplineKnot GetBackKnot() => GetKnot(m_knots.Length - 1);
        public ISplineKnot GetKnot(int knotIndex)
        {
            if (m_knots == null || m_knots.Length < 1) return null;
            return m_knots[knotIndex];
        }
        public int GetKnotIndex(InertSplineKnot knot) => SplineRuntimeComponent.GetKnotIndex(this, knot);
        public bool ContainsKnot(InertSplineKnot knot) => SplineRuntimeComponent.GetKnotIndex(this, knot) >= 0;
        public InertSplineKnot GetKnotBefore(InertSplineKnot knot)
        {
            int knotIndex = GetKnotIndex(knot) - 1;
            if (knotIndex < 0) return null;
            return m_knots[knotIndex];
        }
        public InertSplineKnot GetKnotAfter(InertSplineKnot knot)
        {
            int knotIndex = GetKnotIndex(knot) + 1;
            if (knotIndex >= m_knots.Length) return null;
            return m_knots[knotIndex];
        }

        public MetadataGroups GetSurfaceMetadata() => m_metadata;
        public SurfaceKnotMetadata GetKnotMetadataFromDistance(float distance) => SplineRuntimeComponent.GetKnotMetadataFromDistance(this, distance);
        public SurfaceKnotMetadata GetKnotMetadataFromKnotT(KnotT knotT) => SplineRuntimeComponent.GetKnotMetadataFromKnotT(this, knotT);
        public SurfaceKnotMetadata GetKnotMetadataFromLinearRatio(LinearRatio linearRatio) => SplineRuntimeComponent.GetKnotMetadataFromLinearRatio(this, linearRatio);
        public SplineLocation FindAdjacentSplineLocation(Vector3 worldPosition, Vector3 forward)
        {
            return SplineRuntimeComponent.FindAdjacentSplineLocation(this, this, transform, worldPosition, forward,
                (position, forwardVector, i) => SplineRuntimeComponent.FindAdjacentKnotPosition(this, position, forwardVector, i));
        }
        public SplineLocation FindNearestSplineLocation(Vector3 worldPosition)
        {
            Vector3 localPosition = transform.worldToLocalMatrix.MultiplyPoint3x4(worldPosition);
            SplineRuntimeComponent.FindNearestSplinePosition(this, localPosition, out Vector3 splinePosition, out LinearRatio linearRatio);
            SurfaceKnotMetadata metadata = GetKnotMetadataFromLinearRatio(linearRatio);
            return new SplineLocation { m_spline = this, m_knotLinearRatio = linearRatio, m_metadata = metadata };
        }
        private void FindNearestKnotPosition(Vector3 position, int knotIndex, out Vector3 knotPosition, out KnotT knotT)
            => SplineRuntimeComponent.FindNearestKnotPositionOnSpline(this, position, knotIndex, out knotPosition, out knotT);
        public SplineLocation GetSplineLocationFromDistance(float distance)
        {
            SplineRuntimeComponent.GetSplineLocationFromDistance(this, distance, out LinearRatio linearRatio);
            SurfaceKnotMetadata metadata = GetKnotMetadataFromLinearRatio(linearRatio);
            return new SplineLocation { m_spline = this, m_knotLinearRatio = linearRatio, m_metadata = metadata };
        }

        public List<ISplineRuntimeHandle.KnotBounds> KnotBoundsList
        {
            get
            {
                if (m_knotBoundsList == null || m_knotBoundsList.Count != KnotCount - 1)
                    m_knotBoundsList = SplineRuntimeComponent.CreateKnotBounds(this);
                return m_knotBoundsList;
            }
        }
        public SplineControlPoints GetControlsForKnots(int knot0Index, int knot1Index) => SplineRuntimeComponent.GetControlsForKnots(this, knot0Index, knot1Index);
        public LinearRatio GetLinearRatioFromKnotT(KnotT knotT) => InertSplineRuntimeComponent.GetLinearRatioFromKnotT(this, knotT);
        public KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio) => InertSplineRuntimeComponent.GetKnotTFromLinearRatio(this, linearRatio);

        public PositionAndTangent GetLocalPosAndTangentFromKnotT(KnotT knotT)
        {
            SplineControlPoints controls = GetControlsForKnots(knotT.KnotIndex, knotT.KnotIndex + 1);
            float tangentMagnitude = 0f;
            return SplineRuntimeComponent.PointAndTangentAlongSpline(this, ref tangentMagnitude, knotT.T, ref controls);
        }
        public LightweightTransform GetLocalTransformFromKnotT(KnotT knotT) => SplineRuntimeComponent.PointAndRotationAlongSpline(this, knotT);
        public Vector3 GetLocalPosFromKnotT(KnotT knotT)
        {
            SplineControlPoints controls = GetControlsForKnots(knotT.KnotIndex, knotT.KnotIndex + 1);
            return SplineRuntimeComponent.PointAlongSpline(this, knotT.T, ref controls);
        }
        public PositionAndTangent GetLocalPosAndTangentFromLinearRatio(LinearRatio linearRatio) => GetLocalPosAndTangentFromKnotT(GetKnotTFromLinearRatio(linearRatio));
        public LightweightTransform GetLocalTransformFromLinearRatio(LinearRatio linearRatio) => GetLocalTransformFromKnotT(GetKnotTFromLinearRatio(linearRatio));
        public Vector3 GetLocalPosFromLinearRatio(LinearRatio linearRatio) => GetLocalPosFromKnotT(GetKnotTFromLinearRatio(linearRatio));

        public LightweightTransform GetWorldTransformFromDistance(float distance)
        {
            LightweightTransform localTransform = GetSplineLocationFromDistance(distance).GetLocalTransform();
            Vector3 position = transform.localToWorldMatrix.MultiplyPoint3x4(localTransform.Location);
            Quaternion rotation = transform.rotation * localTransform.Orientation;
            return new LightweightTransform(position, rotation);
        }
        public LightweightTransform GetWorldTransformFromSplineT(float ribbonT) => GetWorldTransformFromDistance(m_length * ribbonT);
        public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition) => SplineRuntimeComponent.GetSurfaceLocation(FindNearestSplineLocation(worldPosition), transform);
        public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition) => SplineRuntimeComponent.GetSurfaceLocation(GetSplineLocationFromDistance(localPosition.z), transform);

        public void KnotsUpdated()
        {
            m_timestamp = DateTime.Now;
            OnSplineKnotsUpdated?.Invoke();
        }
        public void UpdateSplineType(ISpline newSpline)
        {
            m_timestamp = DateTime.Now;
            OnSplineTypeChanged?.Invoke(newSpline);
        }
        public Vector3 TransformPoint(Vector3 point) => transform.TransformPoint(point);
        public Vector3 InverseTransformPoint(Vector3 point) => transform.InverseTransformPoint(point);
        public bool IsActive() => gameObject.activeInHierarchy;

        public DateTime GetTimestamp() => m_timestamp;

        // 0x06000495..49c: the separate explicit contract retains original helpers and throw body.
        SplineControlPoints ISplineRuntimeHandle.GetControlsForKnots(int knot0Index, int knot1Index)
            => SplineRuntimeComponent.GetControlsForKnots(this, knot0Index, knot1Index);
        LinearRatio ISplineRuntimeHandle.GetLinearRatioFromKnotT(KnotT knotT)
            => InertSplineRuntimeComponent.GetLinearRatioFromKnotT(this, knotT);
        KnotT ISplineRuntimeHandle.GetKnotTFromLinearRatio(LinearRatio linearRatio)
            => InertSplineRuntimeComponent.GetKnotTFromLinearRatio(this, linearRatio);
        PositionAndTangent ISplineRuntimeHandle.GetLocalPosAndTangentFromKnotT(KnotT knotT)
        {
            SplineControlPoints controls = SplineRuntimeComponent.GetControlsForKnots(this, knotT.KnotIndex, knotT.KnotIndex + 1);
            float tangentMagnitude = 0f;
            return SplineRuntimeComponent.PointAndTangentAlongSpline(this, ref tangentMagnitude, knotT.T, ref controls);
        }
        void ISplineRuntimeHandle.RecalculateLength(bool force) => throw new NotImplementedException();
        ISplineKnotRuntimeHandle ISplineRuntimeHandle.GetKnotRuntimeHandle(int knotIndex)
        {
            if (KnotCount < 1) return null;
            return m_knots[knotIndex];
        }
        int ISplineRuntimeHandle.GetKnotRuntimeHandleIndex(ISplineKnotRuntimeHandle knotHandle) => GetKnotIndex(knotHandle as InertSplineKnot);
        void ISplineRuntimeHandle.FindNearestKnotPosition(Vector3 localPosition, int knotIndex, out Vector3 knotPosition, out KnotT knotT)
            => SplineRuntimeComponent.FindNearestKnotPositionOnSpline(this, localPosition, knotIndex, out knotPosition, out knotT);
        // 0x0600049d has no derived field initializer; base constructor performs its original initialization.
        public InertSpline() { }
    }
}
