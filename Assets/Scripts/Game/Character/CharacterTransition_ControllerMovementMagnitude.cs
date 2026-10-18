using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003d2; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("ControllerMovementMagnitudeCheck")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterTransition_ControllerMovementMagnitude : FSMTransition
	{
		// Original nested 020003d3.
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Serializable]
		protected class JSONCtorArgs
		{
			public float MagnitudeThreshold;

			public ComparisonType ComparisonType;

			public bool UseRawInput;

			// 0600171a (1.10.1 RVA 0x790020)
			public JSONCtorArgs()
			{
			}
		}

		private readonly float m_magnitudeThreshold;

		private readonly ComparisonType m_comparisonType;

		private readonly bool m_useRawInput;

		// 06001717 (1.10.1 RVA 0x78FE90)
		protected CharacterTransition_ControllerMovementMagnitude(FiniteStateMachine fsm, FSMIdentifier transitionId, JSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_magnitudeThreshold = ctorArgs.MagnitudeThreshold;
			m_comparisonType = ctorArgs.ComparisonType;
			m_useRawInput = ctorArgs.UseRawInput;
		}

		// 06001718 (1.10.1 RVA 0x78FED0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_ControllerMovementMagnitude(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 06001719 (1.10.1 RVA 0x78FF80)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
			{
				return true;
			}
			Character character = user.GetAs<Character>();
			float magnitude = m_useRawInput ? character.RawControllerMovementMagnitude : character.ControllerMovementMagnitude;
			return m_comparisonType.Evaluate(magnitude, m_magnitudeThreshold);
		}
	}
}
