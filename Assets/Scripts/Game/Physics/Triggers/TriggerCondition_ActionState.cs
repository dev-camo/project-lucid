using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000748; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class TriggerCondition_ActionState : TriggerCondition
	{
		[SerializeField]
		private GameAction m_action;

		// 060028f7 (1.10.1 RVA 0x5CCBF0)
		protected override bool DoEvaluate(Character character)
		{
			return character.GetBrainAppliedState(m_action);
		}
	}
}
