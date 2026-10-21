using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003b6; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("ZeroVelocity")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterState_ZeroVelocity : ActorState_Effect
	{
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		protected class JSONCtorArgs : BaseJSONCtorArgs
		{
			public bool MaintainZeroVelocityInUpdate;

			// 0600167c (1.10.1 RVA 0x786A00)
			public JSONCtorArgs()
			{
			}
		}

		private bool m_onUpdate;

		// 06001678 (1.10.1 RVA 0x7867C0)
		protected CharacterState_ZeroVelocity(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			m_onUpdate = ctorArgs.MaintainZeroVelocityInUpdate;
		}

		// 06001679 (1.10.1 RVA 0x7867F0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_ZeroVelocity(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 0600167a (1.10.1 RVA 0x786890)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			user.GetAs<Character>().SetWorldVelocity(Vector3.zero);
		}

		// 0600167b (1.10.1 RVA 0x786940); the velocity is held at zero every update only when the node asks for it.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			if (m_onUpdate)
			{
				user.GetAs<Character>().SetWorldVelocity(Vector3.zero);
			}
		}
	}
}
