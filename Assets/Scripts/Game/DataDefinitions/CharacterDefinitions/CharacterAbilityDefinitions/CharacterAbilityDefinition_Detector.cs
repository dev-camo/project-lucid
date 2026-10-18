using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200050a; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbilityDefinition_Detector : CharacterAbilityDefinition
	{
		[Tooltip("Period of time between detector refreshes.")]
		public float RefreshInterval = 1f;

		[Tooltip("Distance to activate detection. 0 defaults to always.")]
		public float ProximityDistance;

		// 06001ba7/06001ba8; compiler-generated property, private setter.
		public float ProximityDistanceSqr { get; private set; }

		// 06001ba9 (1.10.1 RVA 0x545E30)
		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			ProximityDistanceSqr = ProximityDistance * ProximityDistance;
		}

		// 06001baa (1.10.1 RVA 0x545E60)
		protected CharacterAbilityDefinition_Detector()
		{
		}
	}
}
