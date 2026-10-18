using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000756; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class TriggerCondition_PersistentLevelObjectBoolean : TriggerCondition
	{
		[SerializeField]
		private PersistentLevelObjectBoolean m_persistentLevelObject;

		// 06002924 (1.10.1 RVA 0x5CDC10)
		public PersistentLevelObjectBoolean PersistentLevelObject => m_persistentLevelObject;

		// 06002925 (1.10.1 RVA 0x5CDC20)
		protected override bool DoEvaluate()
		{
			return m_persistentLevelObject.GetCachedValue();
		}

		// 06002926 (1.10.1 RVA 0x5CDC40): the character is ignored and the parameterless
		// Evaluate, which already applies m_invert, answers, so Evaluate(Character) applies
		// the inversion twice.
		protected override bool DoEvaluate(Character _)
		{
			return Evaluate();
		}
	}
}
