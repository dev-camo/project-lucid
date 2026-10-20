using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003f6; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeDefaultName("ToLightspeedDash")]
	public class CharacterTransition_LightspeedDash : Transition_Ability
	{
		// 06001770 (1.10.1 RVA 0x792D30)
		protected CharacterTransition_LightspeedDash(FiniteStateMachine fsm, FSMIdentifier transitionId, AbilityJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 06001771 (1.10.1 RVA 0x792D60)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_LightspeedDash(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 06001772 (1.10.1 RVA 0x792E00); needs the dash ability and a live track manager, then a stored dash target surface.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
			{
				return true;
			}
			Character character = user.GetAs<Character>();
			bool hasAbility = character.TryGetAbility(m_abilityType, out CharacterAbility_LightspeedDash _);
			bool hasTrackManager = !(character.TrackManager == null);
			if (!(hasAbility & hasTrackManager))
			{
				return false;
			}
			return character.Storage.HasValue<ISurface>(ActorFSMKeys.LightspeedDashTarget);
		}
	}
}
