using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000400; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeDefaultName("RollingToRunning")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterTransition_RollingToRunning : Transition_Ability
	{
		// Original nested 02000401; the original carries neither [Serializable] nor the JSON constructor marker.
		protected class JSONCtorArgs : AbilityJSONCtorArgs
		{
			public bool Inverse;

			// 06001792 (1.10.1 RVA 0x794360)
			public JSONCtorArgs()
			{
			}
		}

		private bool m_inverseCheck;

		// 0600178f (1.10.1 RVA 0x7941F0)
		protected CharacterTransition_RollingToRunning(FiniteStateMachine fsm, FSMIdentifier transitionId, JSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
			m_inverseCheck = ctorArgs.Inverse;
		}

		// 06001790 (1.10.1 RVA 0x794220)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_RollingToRunning(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 06001791 (1.10.1 RVA 0x7942C0); true while the rolling ability is exiting (or not, when inverted). An
		// absent ability is an unchecked read in the original.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
			{
				return true;
			}
			return user.GetAs<Character>().GetAbility<CharacterAbility_Rolling>(m_abilityType).IsRollingExiting != m_inverseCheck;
		}
	}
}
