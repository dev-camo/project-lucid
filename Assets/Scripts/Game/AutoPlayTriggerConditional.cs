using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight.AutoPlay
{
	// Original Game.Runtime 0200001a; bodies translated from the 1.10.1 x86-64 binary.
	[RequireComponent(typeof(BoxCollider))]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class AutoPlayTriggerConditional : AutoPlayTrigger
	{
		[SerializeField]
		[Tooltip("If this is an intended destination then tick the box, otherwise this will count as a test failure.")]
		private bool m_testSuccessLocation;

		// 0600004b
		public bool IsPassPoint => m_testSuccessLocation;
	}
}
