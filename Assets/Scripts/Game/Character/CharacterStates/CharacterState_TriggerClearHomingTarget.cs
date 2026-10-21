using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000397; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeMenuFormat("Character/Trigger/{0}")]
	[GraphNodeDefaultName("TriggerClearHomingTarget")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterState_TriggerClearHomingTarget : FSMState_Trigger
	{
		// The original nested type carries no [Serializable] of its own.
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		protected class JSONCtorArgs : TriggerJSONCtorArgs
		{
			public bool StartGracePeriod;

			// 06001619 (1.10.1 RVA 0x7823B0)
			public JSONCtorArgs()
			{
			}
		}

		private readonly bool m_startGracePeriod;

		// 06001616 (1.10.1 RVA 0x782280)
		protected CharacterState_TriggerClearHomingTarget(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			m_startGracePeriod = ctorArgs.StartGracePeriod;
		}

		// 06001617 (1.10.1 RVA 0x7822B0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_TriggerClearHomingTarget(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 06001618 (1.10.1 RVA 0x782350)
		protected override void InvokeTriggerStart(IGraphUser user, FSMStateChangeAction action)
		{
			user.GetAs<Character>().HomingPool.ClearTarget(m_startGracePeriod);
		}
	}
}
