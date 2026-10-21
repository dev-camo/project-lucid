using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000661; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class EnemyNavigationStaticAndReturnAfterChase : EnemyNavigation
	{
		private Vector3 m_home;

		// 0600225f (1.10.1 RVA 0x579A20): unlike its siblings, this setup never marks the navigation initialised.
		protected override void SetUp()
		{
			m_home = transform.position;
		}

		// 06002260 (1.10.1 RVA 0x579A50)
		public override void StartNavigation()
		{
		}

		// 06002261 (1.10.1 RVA 0x579A60)
		public override void InterruptNavigation()
		{
		}

		// 06002262 (1.10.1 RVA 0x579A70)
		public override Vector3 GetDestination()
		{
			return m_home;
		}

		// 06002263 (1.10.1 RVA 0x579A80)
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

		// 06002264 (1.10.1 RVA 0x579B40)
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

		// 06002265 (1.10.1 RVA 0x579C20)
		public void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.red;
			Gizmos.DrawWireSphere(Application.isPlaying ? m_home : transform.position, 0f);
		}

		// 06002266 (1.10.1 RVA 0x579CC0)
		public EnemyNavigationStaticAndReturnAfterChase()
		{
		}
	}
}
