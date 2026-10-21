using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200016a; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ApplicationTransitionCutsceneTheatreIsPlaying : FSMTransition
	{
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Serializable]
		private class JSONCtorArgs
		{
			public bool TrueCausesTransition;

			// 0600083e (1.10.1 RVA 0x662D00)
			public JSONCtorArgs()
			{
			}
		}

		private readonly bool m_trueCausesTransition;

		// 0600083b (1.10.1 RVA 0x662AC0)
		private ApplicationTransitionCutsceneTheatreIsPlaying(FiniteStateMachine fsm, FSMIdentifier transitionId, bool trueCausesTransition) : base(fsm, transitionId)
		{
			m_trueCausesTransition = trueCausesTransition;
		}

		// 0600083c (1.10.1 RVA 0x662AE0)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationTransitionCutsceneTheatreIsPlaying(fsm, transitionId, args.TrueCausesTransition);
		}

		// 0600083d (1.10.1 RVA 0x662B80): the theatre's playing flag compared with TrueCausesTransition.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			return user.Storage.GetValue<CutsceneTheatre>(AppFSMKeys.CutsceneTheatre, null, true).IsPlaying == m_trueCausesTransition;
		}
	}
}
