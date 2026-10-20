using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000392; bodies translated from the 1.10.1 x86-64 binary. For the duration of the ability's
	// jump curve the character moves toward the tracker position while keeping its start velocity; afterwards the stored
	// neighbour is cleared and the character resumes at the stored speed.
	[GraphNodeDefaultName("RailSwitch")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterState_RailSwitch : CharacterState_Rail<CharacterAbility_RailSwitch>
	{
		// 060015fa (1.10.1 RVA 0x77E370)
		protected CharacterState_RailSwitch(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 060015fb (1.10.1 RVA 0x77E3E0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_RailSwitch(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 060015fc (1.10.1 RVA 0x77E4A0). Without a published target nothing happens. Otherwise the elapsed time, the
		// start position, the velocity component along the target's forward direction (as the start velocity) and the
		// speed to resume with are stored, and the tracker is teleported by the lateral distance to the target.
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			Character character = user.GetAs<Character>();
			CharacterAbility_RailSwitch ability = GetAbility(character);
			if (!ability.GetFSMTarget(out CharacterTargetObjectData? targetObject, out float _) || !targetObject.HasValue)
			{
				return;
			}
			Vector3 startPosition = character.WorldPosition;
			Vector3 targetPosition = targetObject.Value.WorldPosition;
			float resumeSpeed = character.WorldVelocityMagnitude;
			Vector3 targetForward = targetObject.Value.WorldRotation * Vector3.forward;
			float speedAlongTarget = Vector3.Dot(character.WorldVelocity, targetForward);
			character.Storage.SetValue(ActorFSMKeys.RailSwitchTimeElapsed, 0f);
			character.Storage.SetValue(ActorFSMKeys.RailSwitchStartPosition, startPosition);
			character.Storage.SetValue(ActorFSMKeys.RailSwitchStartVelocity, targetForward * speedAlongTarget);
			character.Storage.SetValue(ActorFSMKeys.RailSwitchResumeSpeed, resumeSpeed);
			float lateralDistance = Vector3.Dot(targetPosition - character.WorldPosition, character.RightDirection);
			character.Tracker.OnTeleport(character.WorldPosition + character.RightDirection * lateralDistance, lateralDistance, ability.RailDefinition.MetadataGroupKey);
		}

		// 060015fd (1.10.1 RVA 0x77ED50). While the jump curve runs, the velocity is the one that reaches the point between
		// the start position and the tracker (lifted along the up direction by the curve) in this step, with its
		// component along the start velocity replaced by the start velocity itself; the tracker then advances by the
		// start velocity's step. Once the curve has run out the neighbour is cleared and, when the tracker looks ahead
		// onto a surface, the character continues forward at the resume speed.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			CharacterAbilityDefinition_RailSwitch definition = GetAbility(character).Definition;
			AnimationCurve jumpCurve = definition.JumpCurve;
			float duration = jumpCurve[jumpCurve.length - 1].time;
			float deltaTime = updateContext.DeltaTime;
			float elapsed = character.Storage.AdjustValue(ActorFSMKeys.RailSwitchTimeElapsed, deltaTime, 0f);
			Vector3 startPosition = character.Storage.GetValue(ActorFSMKeys.RailSwitchStartPosition, Vector3.zero, true);
			if (duration > elapsed)
			{
				Vector3 trackerPosition = character.Tracker.TrackerPosition;
				float height = definition.JumpCurve.Evaluate(elapsed);
				Vector3 targetPosition = Vector3.LerpUnclamped(startPosition, trackerPosition, elapsed / duration) + character.UpDirection * height;
				Vector3 startVelocity = character.Storage.GetValue(ActorFSMKeys.RailSwitchStartVelocity, Vector3.zero, true);
				Vector3 startDirection = startVelocity.normalized;
				Vector3 velocity = (targetPosition - character.WorldPosition) / deltaTime;
				character.SetWorldVelocity(velocity + (startVelocity - startDirection * Vector3.Dot(startDirection, velocity)));
				character.Tracker.MoveTrackerByDistance((startVelocity * deltaTime).magnitude);
			}
			else
			{
				character.Storage.SetValue<ISurface>(ActorFSMKeys.RailSwitchNeighbour, null);
				character.EndJumpImpulses();
				bool onTracker = character.Tracker.LookAheadIsOnTracker();
				character.Storage.SetValue(ActorFSMKeys.RailActive, onTracker);
				if (onTracker)
				{
					character.SetWorldVelocity(character.Storage.GetValue(ActorFSMKeys.RailSwitchResumeSpeed, 0f, true) * character.ForwardDirection);
				}
			}
		}

		// 060015fe (1.10.1 RVA 0x77FA80)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			Character character = user.GetAs<Character>();
			GetAbility(character).Reset(true);
			character.Storage.RemoveValue<float>(ActorFSMKeys.RailSwitchTimeElapsed);
			character.Storage.RemoveValue<Vector3>(ActorFSMKeys.RailSwitchStartPosition);
			character.Storage.RemoveValue<float>(ActorFSMKeys.RailSwitchResumeSpeed);
		}
	}
}
