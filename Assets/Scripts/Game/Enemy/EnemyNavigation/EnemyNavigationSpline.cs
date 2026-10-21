using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000660; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class EnemyNavigationSpline : EnemyNavigation
	{
		[SerializeField]
		private Spline m_spline;

		[Range(0f, 1f)]
		[SerializeField]
		private float m_startPosition;

		[Min(0f)]
		[SerializeField]
		private float m_followDistance = 3f;

		[SerializeField]
		[Min(0f)]
		private float m_trackerStepSize = 1f;

		[SerializeField]
		private bool m_startReversed;

		[SerializeField]
		private bool m_loop;

		private bool m_reversing;

		private float m_followDistanceSqr;

		private Tracker2D m_tracker2D;

		private readonly Dictionary<ISurface, ISurface[]> m_trackableSurfaces = new Dictionary<ISurface, ISurface[]>();

		private readonly List<ISurface> m_trackableSurfacesList = new List<ISurface>();

		private Vector3 m_nearestSplineLocationWorld;

		private Vector3 TrackerPosition => m_tracker2D.Location.m_worldPosition;

		private Vector3 TrackerForward => m_tracker2D.Location.m_worldRotation * Vector3.forward;

		private bool TrackerAtEnd => m_tracker2D.Location.m_positionBoundsInfo.AtEndEdge;

		// 06002254 (1.10.1 RVA 0x578B30): without a spline the navigation stays uninitialised.
		protected override void SetUp()
		{
			bool initialised = false;
			if (m_spline != null)
			{
				m_reversing = m_startReversed;
				m_followDistanceSqr = m_followDistance * m_followDistance;
				ISpline spline = m_spline.GetSpline();
				Vector3 startPosition = spline.GetWorldTransformFromSplineT(m_startPosition).Location;
				m_trackableSurfaces.Clear();
				m_trackableSurfacesList.Clear();
				m_trackableSurfaces.Add(spline, Array.Empty<ISurface>());
				m_trackableSurfacesList.Add(spline);
				if (m_tracker2D == null)
				{
					m_tracker2D = new Tracker2D(new CollisionResolver2D());
				}
				m_tracker2D.SetWorldInformation(m_trackableSurfacesList, m_trackableSurfaces);
				m_tracker2D.Teleport(startPosition);
				initialised = true;
			}
			IsInitialised = initialised;
		}

		// 06002255 (1.10.1 RVA 0x578EA0)
		public override void StartNavigation()
		{
			if (IsInitialised)
			{
				m_tracker2D.Teleport(transform.position);
			}
		}

		// 06002256 (1.10.1 RVA 0x578EE0)
		public override void InterruptNavigation()
		{
		}

		// 06002257 (1.10.1 RVA 0x578EF0): advances the tracker along the spline until it is the follow distance ahead
		// of the enemy. At the end of an open spline the walk turns round; on a loop it wraps to the other end,
		// carrying over the step left unused.
		public override Vector3 GetDestination()
		{
			bool initialised = IsInitialised;
			Vector3 position = transform.position;
			if (!initialised)
			{
				return position;
			}

			float stepSize = m_reversing ? -m_trackerStepSize : m_trackerStepSize;
			Vector3 step = Vector3.forward * stepSize;
			while ((position - TrackerPosition).sqrMagnitude < m_followDistanceSqr)
			{
				Vector3 previousPosition = TrackerPosition;
				m_tracker2D.MoveLocal(step);
				if (!TrackerAtEnd)
				{
					continue;
				}

				if (!m_loop)
				{
					m_reversing = !m_reversing;
					step = -step;
					continue;
				}

				float splineT = m_reversing ? 1f : 0f;
				Vector3 wrappedPosition = m_spline.GetSpline().GetWorldTransformFromSplineT(splineT).Location;
				float remainingStep = m_trackerStepSize - Vector3.Distance(previousPosition, TrackerPosition);
				if (remainingStep > 0f)
				{
					wrappedPosition += TrackerForward * (m_reversing ? -remainingStep : remainingStep);
				}
				m_tracker2D.Teleport(wrappedPosition);
			}
			return TrackerPosition;
		}

		// 06002258 (1.10.1 RVA 0x579360)
		public override Vector3 ConstrainPosition(Vector3 position)
		{
			if (!IsInitialised)
			{
				return position;
			}
			if (CanReachPosition(position))
			{
				return position;
			}
			switch (ConstrainMethod)
			{
				case ConstrainType.Volume:
					return ConstrainToVolume(ConstrainCollider, position);
				case ConstrainType.Distance:
					return ConstrainToPoint(position, ConstrainDistance, m_nearestSplineLocationWorld);
				default:
					return position;
			}
		}

		// 06002259 (1.10.1 RVA 0x579430)
		public override bool CanReachPosition(Vector3 position)
		{
			if (!IsInitialised || !Constrain)
			{
				return true;
			}
			switch (ConstrainMethod)
			{
				case ConstrainType.Distance:
					return CalculateLine(position);
				case ConstrainType.Volume:
					return CanReachPoint(ConstrainCollider, position);
				default:
					return false;
			}
		}

		// 0600225a (1.10.1 RVA 0x5794C0): the constrained line runs along the spline's tangent at the point nearest to
		// the position, in world space.
		private bool CalculateLine(Vector3 position)
		{
			ISpline spline = m_spline.GetSpline();
			Transform splineTransform = m_spline.transform;
			LightweightTransform nearestLocal = spline.FindNearestSplineLocation(position).GetLocalTransform();
			m_nearestSplineLocationWorld = splineTransform.localToWorldMatrix.MultiplyPoint3x4(nearestLocal.Location);
			Quaternion worldRotation = splineTransform.rotation * nearestLocal.Orientation;
			return CanReachLine(position, ConstrainDistance, m_nearestSplineLocationWorld, worldRotation);
		}

		// 0600225b (1.10.1 RVA 0x579820)
		public Quaternion GetLocationWorldRotation()
		{
			if (IsInitialised)
			{
				return m_tracker2D.Location.m_worldRotation;
			}
			return Quaternion.identity;
		}

		// 0600225c (1.10.1 RVA 0x579880)
		public bool GetReversing()
		{
			return m_reversing;
		}

		// 0600225d (1.10.1 RVA 0x579890)
		private void OnDrawGizmos()
		{
			Color color = m_reversing ? Color.yellow : Color.green;
			if (m_loop)
			{
				color = m_reversing ? Color.cyan : Color.blue;
			}
			Gizmos.color = color;
			Gizmos.DrawWireSphere(transform.position, m_followDistance);
			if (m_tracker2D != null)
			{
				Gizmos.DrawSphere(TrackerPosition, m_trackerStepSize);
			}
		}

		// 0600225e (1.10.1 RVA 0x579940)
		public EnemyNavigationSpline()
		{
		}
	}
}
