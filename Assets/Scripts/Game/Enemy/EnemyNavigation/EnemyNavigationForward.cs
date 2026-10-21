using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200065e; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class EnemyNavigationForward : EnemyNavigation
	{
		[SerializeField]
		[Min(1f)]
		[Tooltip("How far ahead to set the target. Make sure this is larger than the enemy's 'Move Within Distance'.")]
		private float m_lookAheadDistance = 10f;

		// 06002240 (1.10.1 RVA 0x5782B0)
		protected override void SetUp()
		{
			IsInitialised = true;
		}

		// 06002241 (1.10.1 RVA 0x5782C0)
		public override void StartNavigation()
		{
		}

		// 06002242 (1.10.1 RVA 0x5782D0)
		public override void InterruptNavigation()
		{
		}

		// 06002243 (1.10.1 RVA 0x5782E0)
		public override Vector3 GetDestination()
		{
			return transform.position + transform.forward * m_lookAheadDistance;
		}

		// 06002244 (1.10.1 RVA 0x578340)
		public override Vector3 ConstrainPosition(Vector3 position)
		{
			return position;
		}

		// 06002245 (1.10.1 RVA 0x578350)
		public override bool CanReachPosition(Vector3 position)
		{
			return true;
		}

		// 06002246 (1.10.1 RVA 0x578360)
		private void OnDrawGizmos()
		{
			Gizmos.color = Color.red;
			Gizmos.DrawLine(transform.position, GetDestination());
		}

		// 06002247 (1.10.1 RVA 0x5783D0)
		public EnemyNavigationForward()
		{
		}
	}
}
