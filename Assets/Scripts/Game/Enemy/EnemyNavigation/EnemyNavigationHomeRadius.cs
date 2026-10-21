using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200065f; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class EnemyNavigationHomeRadius : EnemyNavigation
	{
		[Tooltip("Max distance to choose to move to from current home position.")]
		[Min(0f)]
		[SerializeField]
		private float m_homeRadius = 10f;

		[Min(0f)]
		[Tooltip("Minimum distance to choose a new spot to move to.")]
		public float m_minNewDestinationDistance = 5f;

		[Tooltip("If ticked, the \"home\" position is reset to the enemy's current location whenever navigation is interrupted.")]
		[SerializeField]
		private bool m_resetHomeOnInterrupt;

		private Vector3 m_home;

		private float m_homeRadiusSqr;

		private Vector3 m_destination;

		private bool m_wasInterrupted;

		private float m_minNewDestinationDistanceSquared;

		private const int LoopLimit = 20;

		// 06002248 (1.10.1 RVA 0x5783F0)
		protected override void SetUp()
		{
			m_home = transform.position;
			m_homeRadiusSqr = m_homeRadius * m_homeRadius;
			m_minNewDestinationDistanceSquared = m_minNewDestinationDistance * m_minNewDestinationDistance;
			IsInitialised = true;
		}

		// 06002249 (1.10.1 RVA 0x578440): picks a random spot around home on the plane the enemy stands on, trying up
		// to LoopLimit times for one that is inside the home radius and far enough from where the enemy is now. The
		// last spot tried is kept even when none qualified.
		public override void StartNavigation()
		{
			Transform enemyTransform = transform;
			Vector3 position = enemyTransform.position;
			if (m_resetHomeOnInterrupt && m_wasInterrupted)
			{
				m_home = position;
				m_wasInterrupted = false;
			}
			Vector3 down = -enemyTransform.up;
			Quaternion planeRotation = Quaternion.FromToRotation(Vector3.down, down);
			Plane plane = new Plane(down, m_home);
			Vector3 positionOnPlane = plane.ClosestPointOnPlane(position);
			Vector3 destination = default;
			for (int attempt = 0; attempt < LoopLimit; attempt++)
			{
				float x = Random.Range(-m_homeRadius, m_homeRadius);
				float z = Random.Range(-m_homeRadius, m_homeRadius);
				destination = planeRotation * new Vector3(x, 0f, z) + m_home;
				if ((m_home - destination).sqrMagnitude <= m_homeRadiusSqr
					&& (positionOnPlane - destination).sqrMagnitude >= m_minNewDestinationDistanceSquared)
				{
					break;
				}
			}
			m_destination = destination;
		}

		// 0600224a (1.10.1 RVA 0x5787C0)
		public override void InterruptNavigation()
		{
			m_wasInterrupted = true;
		}

		// 0600224b (1.10.1 RVA 0x5787D0)
		public override Vector3 GetDestination()
		{
			return m_destination;
		}

		// 0600224c (1.10.1 RVA 0x5787E0)
		public override Vector3 ConstrainPosition(Vector3 position)
		{
			if (CanReachPosition(position))
			{
				return position;
			}
			switch (ConstrainMethod)
			{
				case ConstrainType.Volume:
					return ConstrainToVolume(ConstrainCollider, position);
				case ConstrainType.Distance:
					return ConstrainToPoint(position, ConstrainDistance, m_home);
				default:
					return position;
			}
		}

		// 0600224d (1.10.1 RVA 0x5788A0)
		public override bool CanReachPosition(Vector3 position)
		{
			if (!Constrain)
			{
				return true;
			}
			switch (ConstrainMethod)
			{
				case ConstrainType.Distance:
					return CanReachPoint(position, ConstrainDistance, m_home);
				case ConstrainType.Volume:
					return CanReachPoint(ConstrainCollider, position);
				default:
					return false;
			}
		}

		// 0600224e (1.10.1 RVA 0x578980)
		public void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.red;
			Gizmos.DrawWireSphere(Application.isPlaying ? m_home : transform.position, m_homeRadius);
		}

		// 0600224f (1.10.1 RVA 0x578A20)
		private void OnValidate()
		{
			if (gameObject.CanValidate(false) && m_minNewDestinationDistance > 0f && m_minNewDestinationDistance >= m_homeRadius)
			{
				m_minNewDestinationDistance = 0f;
			}
		}

		// 06002250 (1.10.1 RVA 0x578A70)
		public EnemyNavigationHomeRadius()
		{
		}
	}
}
