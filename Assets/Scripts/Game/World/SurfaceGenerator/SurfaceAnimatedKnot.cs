using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a9b; bodies translated from the 1.10.1 x86-64 binary.
    // Moves one knot, or the whole spline along one axis, of an inert spline to follow a transform.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Serializable]
    public class SurfaceAnimatedKnot
    {
        private enum Axis
        {
            X = 0,
            Y = 1,
            Z = 2
        }

        [Tooltip("Spline that will be animated.  NB: This must be set as the inert variant.")]
        [SerializeField]
        private Spline m_spline;

        [Tooltip("Whether to set transform to all knots or a single index.")]
        [SerializeField]
        private bool m_allKnots;

        [ShowIf("m_allKnots", false)]
        [Min(0f)]
        [SerializeField]
        [Tooltip("The single knot index that is animated.")]
        private int m_index;

        [SerializeField]
        [ShowIf("m_allKnots", (string)null)]
        [Tooltip("The axis to align spline to.")]
        private Axis m_axis;

        [Tooltip("The transform we are aligning the animation to.")]
        [SerializeField]
        private Transform m_transform;

        private bool m_isValid;
        private IInertSplineRuntimeHandle m_inertSpline;
        private IInertSplineKnotRuntimeHandle m_inertKnot;

        // 06003d0f (0x6D23F0): only an inert spline, and for a single knot an inert knot handle,
        // leave the knot valid.
        public void CalculateCachedValues()
        {
            m_inertSpline = m_spline.GetSpline() as IInertSplineRuntimeHandle;
            m_isValid = m_inertSpline != null;
            if (m_isValid && !m_allKnots)
            {
                m_inertKnot = m_inertSpline.GetKnotRuntimeHandle(m_index) as IInertSplineKnotRuntimeHandle;
                m_isValid = m_inertKnot != null;
            }
        }

        public void DoUpdate(float deltaTime)
        {
            bool transformed = m_allKnots ? TransformSpline() : TransformKnot();
            if (transformed)
                m_inertSpline.KnotsUpdated();
        }

        // 06003d11 (0x6D2620): the knot takes the target pose in spline space unless its location
        // is already within the default tolerance.
        private bool TransformKnot()
        {
            if (!m_isValid)
                return false;
            Transform splineTransform = m_spline.transform;
            Vector3 localPosition = splineTransform.InverseTransformPoint(m_transform.position);
            if (m_inertKnot.Transform.Location.WithinTolerance(localPosition))
                return false;
            Quaternion localRotation = Quaternion.Inverse(splineTransform.rotation) * m_transform.rotation;
            m_inertKnot.SetTransform(localPosition, localRotation);
            return true;
        }

        // 06003d12 (0x6D28A0): the spline transform moves so that, along the chosen axis, it lines
        // up with the target. An undefined axis value uses Vector3.zero and never moves the
        // spline; an unordered (NaN) offset still moves it (ucomiss/ja at 0x6D29C5).
        private bool TransformSpline()
        {
            if (!m_isValid)
                return false;
            Transform splineTransform = m_spline.transform;
            Vector3 axis;
            switch (m_axis)
            {
                case Axis.X: axis = splineTransform.right; break;
                case Axis.Y: axis = splineTransform.up; break;
                case Axis.Z: axis = splineTransform.forward; break;
                default: axis = Vector3.zero; break;
            }
            Vector3 offset = axis * Vector3.Dot(splineTransform.position - m_transform.position, axis);
            if (offset.sqrMagnitude < 0.0001f)
                return false;
            splineTransform.position = splineTransform.position - offset;
            return true;
        }
    }
}
