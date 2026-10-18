using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000409; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class Transition_Ability : FSMTransition
	{
		// AbilityJSONCtorArgs: its implicit constructor is 060017a9 (1.10.1 RVA 0x7914C0).
		[Serializable]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		protected class AbilityJSONCtorArgs
		{
			public ActorAbilityType AbilityType;
		}

		protected ActorAbilityType m_abilityType;

		// 060017a8 (1.10.1 RVA 0x78EAC0)
		protected Transition_Ability(FiniteStateMachine fsm, FSMIdentifier transitionId, AbilityJSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_abilityType = ctorArgs.AbilityType;
		}
	}
}
