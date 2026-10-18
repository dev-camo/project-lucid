using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original Game.Runtime 02000013; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Core/{0}")]
	public sealed class FSMTransitionIsTvOs : FSMTransition
	{
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Serializable]
		private class JSONCtorArgs
		{
			public bool TrueCausesTransition;

			// 06000030 (1.10.1 RVA 0x507C10)
			public JSONCtorArgs()
			{;}
		}

		private readonly bool m_trueCausesTransition;

		// 0600002d (1.10.1 RVA 0x507B40); the base registration precedes the field store.
		private FSMTransitionIsTvOs(FiniteStateMachine fsm, FSMIdentifier transitionId, bool trueCausesTransition) : base(fsm, transitionId)
		{
			m_trueCausesTransition = trueCausesTransition;
		}

		// 0600002e (1.10.1 RVA 0x507B60); the flag is read before the instance is allocated.
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new FSMTransitionIsTvOs(fsm, transitionId, args.TrueCausesTransition);
		}

		// 0600002f (1.10.1 RVA 0x507C00). The platform test is folded to false in this
		// build, so the transition fires only when TrueCausesTransition is false.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return !m_trueCausesTransition;
		}
	}
}
