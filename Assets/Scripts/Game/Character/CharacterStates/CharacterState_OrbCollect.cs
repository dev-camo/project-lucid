using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003b4; bodies translated from the 1.10.1 x86-64 binary. The orb collection effect plays
	// the state animation, or its ghosted variant for the replay outros.
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeDefaultName("OrbCollect")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterState_OrbCollect : ActorState_Effect
	{
		// Original nested 020003b5.
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		[Serializable]
		protected class OrbCollectJSONCtorArgs : BaseJSONCtorArgs
		{
			[GraphUnityObject(typeof(ActorAnimationDefinition))]
			public string GhostedOrbCollectionAnimationDefinition;

			// 06001677 (1.10.1 RVA 0x7867B0)
			public OrbCollectJSONCtorArgs()
			{
			}
		}

		private readonly ActorAnimationDefinition m_ghostedOrbAnimationDefinition;

		// 06001671 (1.10.1 RVA 0x786260)
		protected CharacterState_OrbCollect(FiniteStateMachine fsm, FSMIdentifier stateId, OrbCollectJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			string ghostedDefinitionName = ctorArgs.GhostedOrbCollectionAnimationDefinition;
			if (string.IsNullOrEmpty(ghostedDefinitionName))
			{
				return;
			}
			ProcessManager.GetSystemRef<App>(null, true).Get().DataManager.ActorStateAnimationDefinitions.TryGetValue(ghostedDefinitionName, out m_ghostedOrbAnimationDefinition);
		}

		// 06001672 (1.10.1 RVA 0x786370)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_OrbCollect(fsm, transitionId, JsonUtility.FromJson<OrbCollectJSONCtorArgs>(jsonCtorArgs));
		}

		// 06001673 (1.10.1 RVA 0x786400). The base entry is not called; the state effect is activated directly, and the
		// bool animation is set while the character is off the ground.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			Character character = user.GetAs<Character>();
			ActorAnimationDefinition animationDefinition = GetAnimationDefinition(character);
			ActivateStateEffect(character, animationDefinition);
			if (!character.IsOnGround(CharacterTrackingType.Active))
			{
				character.Animator.TrySetFromAnimationType(animationDefinition, ActorAnimationType.BoolSet, true);
			}
		}

		// 06001674 (1.10.1 RVA 0x7864A0)
		private ActorAnimationDefinition GetAnimationDefinition(IGraphUser user)
		{
			switch (user.Storage.GetValue(ActorFSMKeys.OutroSequenceType, OutroSequenceIdentifier.Default, true))
			{
				case OutroSequenceIdentifier.Default:
				case OutroSequenceIdentifier.Moon:
					return m_stateAnimationDefinition;
				case OutroSequenceIdentifier.MoonReplay:
				case OutroSequenceIdentifier.Replay:
					return m_ghostedOrbAnimationDefinition;
				default:
					return m_stateAnimationDefinition;
			}
		}

		// 06001675 (1.10.1 RVA 0x786650)
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
		}

		// 06001676 (1.10.1 RVA 0x786660). The base leave is not called either.
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			Character character = user.GetAs<Character>();
			ActorAnimationDefinition animationDefinition = GetAnimationDefinition(character);
			DeactivateStateEffect(character, animationDefinition);
			character.Animator.TrySetFromAnimationType(animationDefinition, ActorAnimationType.BoolSet, false);
			character.Storage.RemoveValue<OutroSequenceIdentifier>(ActorFSMKeys.OutroSequenceType);
		}
	}
}
