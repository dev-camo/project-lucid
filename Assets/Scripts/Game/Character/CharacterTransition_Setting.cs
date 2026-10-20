using System;
using Unity.IL2CPP.CompilerServices;
using Hardlight;

namespace HardlightProject
{
	// Original Game.Runtime 02000402; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeMenuFormat("Character/{0}")]
	public abstract class CharacterTransition_Setting : FSMTransition
	{
		// Original nested 02000403.
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		protected class JSONCtorArgs
		{
			public ButtonMappingType ButtonMapping;

			// 06001796 (1.10.1 RVA 0x794430)
			public JSONCtorArgs()
			{
			}
		}

		private readonly ButtonMappingType m_buttonMapping;

		// 06001793 (1.10.1 RVA 0x794370)
		protected CharacterTransition_Setting(FiniteStateMachine fsm, FSMIdentifier transitionId, JSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_buttonMapping = ctorArgs.ButtonMapping;
		}

		// 06001794 (1.10.1 RVA 0x7943A0); true while the player's saved mapping for the action is the configured one.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
			{
				return true;
			}
			return GetButtonMapping(user.GetAs<Character>().SaveDataSettings) == m_buttonMapping;
		}

		// 06001795
		protected abstract ButtonMappingType GetButtonMapping(SaveDataSettings saveDataSettings);
	}
}
