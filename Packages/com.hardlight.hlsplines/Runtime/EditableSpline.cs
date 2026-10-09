using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLSplines.Runtime 0x02000092, complete 53-method owner including its callback.
    // Bodies inferred from complete ARM64/x86 captures; authored text and native fault scheduling remain qualified.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [ExecuteInEditMode]
    public class EditableSpline : SplineMonoBase, ISpline, ISurface, ISplineRuntimeHandle
    {
        [SerializeField] private DateTime m_timestamp;
        [SerializeField] protected List<SplineKnot> m_knots = new List<SplineKnot>();
        [SerializeField] protected int m_lutCount = 10;
        private List<ISplineRuntimeHandle.KnotBounds> m_knotBoundsList;

        // 0x060003a2..3a7. The indexer does not use GetKnot's empty-list guard.
        public LightweightTransform Transform => new LightweightTransform(transform.localPosition, transform.localRotation);
        public List<SplineKnot> Knots => m_knots;
        public SplineKnot this[int knotIndex] => m_knots[knotIndex];
        public int KnotCount => m_knots == null ? 0 : m_knots.Count;
        public int LUTCount => m_lutCount;
        public float LengthInverse => 1f / m_length;

        // 0x060003a8..3ab: ordinary atomic field-like delegate combine/remove.
        public event Action OnSplineKnotsUpdated;
        public event Action<ISpline> OnSplineTypeChanged;

        // 0x060003ac..3b3. Ownership and list reads remain live, including the missing-knot after path.
        public ISplineKnot GetHeadKnot() => GetKnot(0);
        public ISplineKnot GetBackKnot() => GetKnot(m_knots.Count - 1);
        public ISplineKnot GetKnot(int knotIndex)
        {
            if (m_knots == null || m_knots.Count < 1) return null;
            return m_knots[knotIndex];
        }
        private ISplineKnotRuntimeHandle GetKnotRuntimeHandle(int knotIndex) => GetKnot(knotIndex) as ISplineKnotRuntimeHandle;
        public int GetKnotIndex(SplineKnot knot)
        {
            if (knot.EditableSpline != this)
            {
                HLOutput.LogError(string.Format("Knot {0} is part of spline {1} not us {2}", knot, knot.EditableSpline, ToString()), null);
                return -1;
            }
            return SplineRuntimeComponent.GetKnotIndex(this, knot);
        }
        public bool ContainsKnot(SplineKnot knot)
        {
            if (knot.EditableSpline != this) return false;
            return SplineRuntimeComponent.GetKnotIndex(this, knot) >= 0;
        }
        public SplineKnot GetKnotBefore(SplineKnot knot)
        {
            int knotIndex = GetKnotIndex(knot) - 1;
            if (knotIndex < 0) return null;
            return m_knots[knotIndex];
        }
        public SplineKnot GetKnotAfter(SplineKnot knot)
        {
            int knotIndex = GetKnotIndex(knot) + 1;
            if (knotIndex >= m_knots.Count) return null;
            return m_knots[knotIndex];
        }

        // 0x060003b4..3ba: original helper forwarding and callback capture.
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
        public SplineLocation GetSplineLocationFromDistance(float distance)
        {
            SplineRuntimeComponent.GetSplineLocationFromDistance(this, distance, out LinearRatio linearRatio);
            SurfaceKnotMetadata metadata = GetKnotMetadataFromLinearRatio(linearRatio);
            return new SplineLocation { m_spline = this, m_knotLinearRatio = linearRatio, m_metadata = metadata };
        }

        // 0x060003bb: cache validity is determined by count alone.
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
        public LinearRatio GetLinearRatioFromKnotT(KnotT knotT) => SplineRuntimeComponent.GetLinearRatioFromKnotT(GetKnotRuntimeHandle(knotT.KnotIndex), knotT);
        public KnotT GetKnotTFromLinearRatio(LinearRatio linearRatio) => SplineRuntimeComponent.GetKnotTFromLinearRatio(this, linearRatio);

        // 0x060003bf..3c4: original controls copy precedes point/tangent evaluation.
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

        // 0x060003c5: invalidate first, publish length, then recompute bounds.
        public void RecalculateLength(bool force = false)
        {
            m_knotBoundsList = null;
            m_length = SplineRuntimeComponent.RecalculateKnotsLength(this, force);
            m_boundingBox = SplineRuntimeComponent.RecalculateBounds(this);
        }
        public LightweightTransform GetWorldTransformFromDistance(float distance)
        {
            LightweightTransform localTransform = GetSplineLocationFromDistance(distance).GetLocalTransform();
            Vector3 position = transform.localToWorldMatrix.MultiplyPoint3x4(localTransform.Location);
            Quaternion rotation = transform.rotation * localTransform.Orientation;
            return new LightweightTransform(position, rotation);
        }
        public LightweightTransform GetWorldTransformFromSplineT(float splineT) => GetWorldTransformFromDistance(m_length * splineT);
        public SurfaceLocation FindNearestSurfaceLocationFromWorld(Vector3 worldPosition) => SplineRuntimeComponent.GetSurfaceLocation(FindNearestSplineLocation(worldPosition), transform);
        // 0x060003c9: both CPUs use the local vector's z as a distance; this is a shipping quirk.
        public SurfaceLocation FindNearestSurfaceLocationFromLocal(Vector3 localPosition) => SplineRuntimeComponent.GetSurfaceLocation(GetSplineLocationFromDistance(localPosition.z), transform);

        // 0x060003ca: inactive returns before list reads; list count remains live in the rotation loop.
        public void KnotsUpdated()
        {
            if (!gameObject.activeInHierarchy) return;
            if (m_knots.Count >= 2)
            {
                for (int i = 0; i < m_knots.Count; i++)
                {
                    KnotT knotT = i < m_knots.Count - 1 ? new KnotT(i, 0f) : new KnotT(i - 1, 1f);
                    Quaternion rotation = GetLocalTransformFromKnotT(knotT).Orientation;
                    Transform knotTransform = m_knots[i].transform;
                    if (knotTransform.localRotation != rotation) knotTransform.localRotation = rotation;
                }
            }
            RecalculateLength(true);
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

        // 0x060003cf..3d1: explicit runtime contract, including the original as-cast and null fault.
        ISplineKnotRuntimeHandle ISplineRuntimeHandle.GetKnotRuntimeHandle(int knotIndex)
        {
            if (KnotCount < 1) return null;
            return m_knots[knotIndex];
        }
        int ISplineRuntimeHandle.GetKnotRuntimeHandleIndex(ISplineKnotRuntimeHandle knotHandle) => GetKnotIndex(knotHandle as SplineKnot);
        void ISplineRuntimeHandle.FindNearestKnotPosition(Vector3 localPosition, int knotIndex, out Vector3 knotPosition, out KnotT knotT)
            => SplineRuntimeComponent.FindNearestKnotPositionOnSpline(this, localPosition, knotIndex, out knotPosition, out knotT);
        public void Initialise()
        {
            for (int i = 0; i < m_knots.Count; i++) m_knots[i].Initialise(m_lutCount);
        }
        public DateTime GetTimestamp() => m_timestamp;
        // 0x060003d4: count is captured once, but indexed list reads remain live.
        private bool AreKnotsLengthDirty()
        {
            int count = KnotCount;
            for (int i = 0; i < count; i++) if (m_knots[i].IsLengthDirty()) return true;
            return false;
        }
        public EditableSpline() { }
    }
}
