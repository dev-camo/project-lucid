using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003f7; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeDefaultName("LightspeedDashExit")]
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterTransition_LightspeedDashExit : Transition_Ability
	{
		// 06001773 (1.10.1 RVA 0x792F50)
		protected CharacterTransition_LightspeedDashExit(FiniteStateMachine fsm, FSMIdentifier transitionId, AbilityJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 06001774 (1.10.1 RVA 0x792F80)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_LightspeedDashExit(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 06001775 (1.10.1 RVA 0x793020); the dash target is read as an ISpline here, an ISurface by the entering transition.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			return base.DoUpdate(user, updateContext) || !user.GetAs<Character>().Storage.HasValue<ISpline>(ActorFSMKeys.LightspeedDashTarget);
		}
	}
}
