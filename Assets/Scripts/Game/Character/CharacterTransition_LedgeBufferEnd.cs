using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003f3; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("LedgeBufferEnd")]
	public class CharacterTransition_LedgeBufferEnd : FSMTransition
	{
		// Original nested 020003f4.
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		[Serializable]
		private class JSONCtorArgs
		{
			public float DurationSeconds;

			// 0600176c (1.10.1 RVA 0x792B10)
			public JSONCtorArgs()
			{
			}
		}

		private float m_durationSeconds;

		// 06001769 (1.10.1 RVA 0x7928C0)
		private CharacterTransition_LedgeBufferEnd(FiniteStateMachine fsm, FSMIdentifier transitionId, JSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_durationSeconds = ctorArgs.DurationSeconds;
		}

		// 0600176a (1.10.1 RVA 0x7928F0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_LedgeBufferEnd(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 0600176b (1.10.1 RVA 0x792990); ends once the air ledge buffer timer reaches the duration (an unordered NaN does not).
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
			{
				return true;
			}
			Character character = user.GetAs<Character>();
			if (!character.GetAbility<CharacterAbility_MovementAir>(ActorAbilityType.Character_MovementAir).UpdateEnabled)
			{
				return false;
			}
			return character.Storage.GetValue(ActorFSMKeys.AirLedgeBufferTimer, 0f, true) >= m_durationSeconds;
		}
	}
}
