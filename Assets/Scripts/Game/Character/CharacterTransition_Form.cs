using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003d6; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("FormIsActive")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterTransition_Form : FSMTransition
	{
		// Original nested 020003d7.
		[FiniteStateMachineJsonCtorArgs]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		protected class JSONCtorArgs
		{
			public ActorFormType ActiveFormType;

			public bool Inverse;

			// 06001724 (1.10.1 RVA 0x790570)
			public JSONCtorArgs()
			{
			}
		}

		private readonly ActorFormType m_activeFormType;

		private readonly bool m_inverse;

		// 06001721 (1.10.1 RVA 0x790410)
		protected CharacterTransition_Form(FiniteStateMachine fsm, FSMIdentifier transitionId, JSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_activeFormType = ctorArgs.ActiveFormType;
			m_inverse = ctorArgs.Inverse;
		}

		// 06001722 (1.10.1 RVA 0x790440)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_Form(fsm, transitionId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
		}

		// 06001723 (1.10.1 RVA 0x7904E0)
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
			{
				return true;
			}
			Character character = user.GetAs<Character>();
			return m_inverse ? character.FormType != m_activeFormType : character.FormType == m_activeFormType;
		}
	}
}
