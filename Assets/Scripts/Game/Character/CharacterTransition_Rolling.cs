using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003fe; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("ToRolling")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterTransition_Rolling : Transition_Ability
	{
		// Original nested 020003ff; the original carries neither [Serializable] nor the JSON constructor marker.
		protected class JSONCtorArgs : AbilityJSONCtorArgs
		{
			public bool Inverse;

			public bool ResetVelocity;

			// 0600178e (1.10.1 RVA 0x7941E0)
			public JSONCtorArgs()
			{
			}
		}

		private readonly bool m_inverseCheck;

		private readonly bool m_resetVelocity;

		// 0600178a (1.10.1 RVA 0x793F50)
		protected CharacterTransition_Rolling(FiniteStateMachine fsm, FSMIdentifier transitionId, JSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
			m_inverseCheck = ctorArgs.Inverse;
			m_resetVelocity = ctorArgs.ResetVelocity;
		}

		// 0600178b (1.10.1 RVA 0x793F90)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_Rolling(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 0600178c (1.10.1 RVA 0x794040); true while the rolling ability's trigger matches the inverse flag.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
			{
				return true;
			}
			if (!user.GetAs<Character>().TryGetAbility(m_abilityType, out CharacterAbility_Rolling ability))
			{
				return false;
			}
			return ability.IsRollingTriggered != m_inverseCheck;
		}

		// 0600178d (1.10.1 RVA 0x794100); the velocity is only cleared when this transition fired.
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			if (m_resetVelocity && action != null && action.TransitionThatFired == this)
			{
				user.GetAs<Character>().SetWorldVelocity(Vector3.zero);
			}
		}
	}
}
