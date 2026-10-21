using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000464; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "CameraAbilityOverrideDefinition", menuName = "HardlightProject/DefinitionData/Definitions/CameraAbilityOverrideDefinition")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CameraAbilityOverrideDefinition : CameraAbilityDefinition
	{
		[SerializeField]
		private Vector3 m_axisAlignment = Vector3.forward;

		[SerializeField]
		private bool m_reverseByDirection;

		// 060019b4 (1.10.1 RVA 0x7A8C50)
		public Vector3 AxisAlignment => m_axisAlignment;

		// 060019b5 (1.10.1 RVA 0x7A8C60)
		public bool ReverseByDirection => m_reverseByDirection;

		// 060019b6 (1.10.1 RVA 0x7A8C70)
		public CameraAbilityOverrideDefinition()
		{
		}
	}
}
