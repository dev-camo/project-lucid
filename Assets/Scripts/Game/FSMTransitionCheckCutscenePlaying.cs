using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200069b; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Application/{0}")]
	public class FSMTransitionCheckCutscenePlaying : FSMTransition
	{
		public enum Comparison
		{
			TrueCausesTransition = 0,
			FalseCausesTransition = 1,
			EitherCausesTransition = 2
		}

		[Serializable]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		private class JSONCtorArgs
		{
			[GraphEnumPopup(typeof(Comparison))]
			public string Compare;

			// 0600236f (1.10.1 RVA 0x587890)
			public JSONCtorArgs()
			{;}
		}

		private readonly Comparison m_compare;

		// The system reference is a field initializer, so it is acquired before base registration.
		private readonly SystemRef<CutsceneManager> m_cutsceneManagerRef = ProcessManager.GetSystemRef<CutsceneManager>();

		// 0600236b (1.10.1 RVA 0x5874D0)
		public FSMTransitionCheckCutscenePlaying(FiniteStateMachine fsm, FSMIdentifier transitionId) : base(fsm, transitionId)
		{
		}

		// 0600236c (1.10.1 RVA 0x587570); the comparison is stored after base registration.
		public FSMTransitionCheckCutscenePlaying(FiniteStateMachine fsm, FSMIdentifier transitionId, Comparison compare = Comparison.TrueCausesTransition) : base(fsm, transitionId)
		{
			m_compare = compare;
		}

		// 0600236d (1.10.1 RVA 0x587620); a case-insensitive, throwing Enum.Parse with no default.
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			Comparison compare = (Comparison)Enum.Parse(typeof(Comparison), args.Compare, true);
			return new FSMTransitionCheckCutscenePlaying(fsm, transitionId, compare);
		}

		// 0600236e (1.10.1 RVA 0x5877F0); nothing fires while a cutscene is loading, and the
		// manager is not null-checked. Unknown comparisons never fire.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			CutsceneManager cutsceneManager = m_cutsceneManagerRef.Get();
			if (cutsceneManager.CutsceneIsLoading)
			{
				return false;
			}
			switch (m_compare)
			{
				case Comparison.TrueCausesTransition:
					return cutsceneManager.IsCutscenePlaying;
				case Comparison.FalseCausesTransition:
					return !cutsceneManager.IsCutscenePlaying;
				case Comparison.EitherCausesTransition:
					return true;
				default:
					return false;
			}
		}
	}
}
