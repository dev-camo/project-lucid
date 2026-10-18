using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003d0; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("ControllerAxisCheck")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterTransition_ControllerAxisCheck : FSMTransition
	{
		// Original nested 020003d1.
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Serializable]
		protected class JSONCtorArgs
		{
			public float XAxisValue;

			public ComparisonType XAxisComparisonType;

			public bool AbsoluteXValue;

			public float YAxisValue;

			public ComparisonType YAxisComparisonType;

			public bool AbsoluteYValue;

			public bool UseRawInput;

			// 06001716 (1.10.1 RVA 0x78FE80)
			public JSONCtorArgs()
			{
			}
		}

		private readonly float m_xAxisValue;

		private readonly ComparisonType m_xAxisComparisonType;

		private readonly bool m_absoluteXValue;

		private readonly float m_yAxisValue;

		private readonly ComparisonType m_yAxisComparisonType;

		private readonly bool m_absoluteYValue;

		private readonly bool m_useRawInput;

		// 06001713 (1.10.1 RVA 0x78FC70)
		protected CharacterTransition_ControllerAxisCheck(FiniteStateMachine fsm, FSMIdentifier transitionId, JSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_xAxisValue = ctorArgs.XAxisValue;
			m_xAxisComparisonType = ctorArgs.XAxisComparisonType;
			m_absoluteXValue = ctorArgs.AbsoluteXValue;
			m_yAxisValue = ctorArgs.YAxisValue;
			m_yAxisComparisonType = ctorArgs.YAxisComparisonType;
			m_absoluteYValue = ctorArgs.AbsoluteYValue;
			m_useRawInput = ctorArgs.UseRawInput;
		}

		// 06001714 (1.10.1 RVA 0x78FCD0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_ControllerAxisCheck(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 06001715 (1.10.1 RVA 0x78FDB0); both axes are always evaluated.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
			{
				return true;
			}
			Character character = user.GetAs<Character>();
			Vector2 input = m_useRawInput ? character.RawControllerMovement : character.ControllerMovement;
			float x = m_absoluteXValue ? Mathf.Abs(input.x) : input.x;
			float y = m_absoluteYValue ? Mathf.Abs(input.y) : input.y;
			return m_xAxisComparisonType.Evaluate(x, m_xAxisValue) & m_yAxisComparisonType.Evaluate(y, m_yAxisValue);
		}
	}
}
