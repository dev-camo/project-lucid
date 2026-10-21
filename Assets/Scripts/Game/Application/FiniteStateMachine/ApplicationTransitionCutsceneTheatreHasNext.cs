using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000168; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ApplicationTransitionCutsceneTheatreHasNext : FSMTransition
	{
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Serializable]
		private class JSONCtorArgs
		{
			public bool TrueCausesTransition;

			// 0600083a (1.10.1 RVA 0x662AB0)
			public JSONCtorArgs()
			{
			}
		}

		private readonly bool m_trueCausesTransition;

		// 06000837 (1.10.1 RVA 0x662860)
		private ApplicationTransitionCutsceneTheatreHasNext(FiniteStateMachine fsm, FSMIdentifier transitionId, bool trueCausesTransition) : base(fsm, transitionId)
		{
			m_trueCausesTransition = trueCausesTransition;
		}

		// 06000838 (1.10.1 RVA 0x662880)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationTransitionCutsceneTheatreHasNext(fsm, transitionId, args.TrueCausesTransition);
		}

		// 06000839 (1.10.1 RVA 0x662920): true when the theatre queue is empty, flipped by the flag
		// so that TrueCausesTransition fires on a non-empty queue.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return user.Storage.GetValue<CutsceneTheatre>(AppFSMKeys.CutsceneTheatre, null, true).IsEmpty() ^ m_trueCausesTransition;
		}
	}
}
