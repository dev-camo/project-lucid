using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003a6, its iterators <TriggerRespawn>d__9 (020003aa) and <SnapCameras>d__13 (020003a9)
	// and the lambda closure <>c__DisplayClass9_0 (020003a8); bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("TriggerRespawn")]
	[GraphNodeMenuFormat("Character/Trigger/{0}")]
	public class CharacterState_TriggerRespawn : ActorState_Effect
	{
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		[Serializable]
		protected class RespawnJSONCtorArgs : BaseJSONCtorArgs
		{
			public float SecondsInvulnerableAfterOutOfBounds;

			[GraphUnityObject(typeof(UIContainerIdentifier))]
			public string TransitionOverrideContainer;

			public bool DoFullscreenTransition = true;

			// 0600164b (1.10.1 RVA 0x784910)
			public RespawnJSONCtorArgs()
			{
			}
		}

		private readonly float m_secondsInvulnerableAfterOutOfBounds;

		private readonly UIContainerIdentifier m_transitionOverrideContainer;

		private readonly bool m_doFullscreenTransition;

		// 06001641 (1.10.1 RVA 0x783D80)
		protected CharacterState_TriggerRespawn(FiniteStateMachine fsm, FSMIdentifier stateId, RespawnJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			m_secondsInvulnerableAfterOutOfBounds = ctorArgs.SecondsInvulnerableAfterOutOfBounds;
			m_transitionOverrideContainer = string.IsNullOrEmpty(ctorArgs.TransitionOverrideContainer) ? null : UIContainerIdentifier.FindByName(ctorArgs.TransitionOverrideContainer);
			m_doFullscreenTransition = ctorArgs.DoFullscreenTransition;
		}

		// 06001642 (1.10.1 RVA 0x783DF0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_TriggerRespawn(fsm, transitionId, JsonUtility.FromJson<RespawnJSONCtorArgs>(jsonCtorArgs));
		}

		// 06001643 (1.10.1 RVA 0x783ED0)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			CoroutineUtils.RunCoroutine(TriggerRespawn(user.GetAs<Character>(), m_secondsInvulnerableAfterOutOfBounds));
		}

		// 06001644 (1.10.1 RVA 0x784040)
		protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnLeave(user, action);
			user.GetAs<Character>().Storage.RemoveValue<bool>(ActorFSMKeys.RespawnRetainVelocity);
		}

		// 06001645 (1.10.1 RVA 0x784170); a transition that queued the teleport leaves it to this update to perform.
		protected override void DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			base.DoUpdate(user, updateContext);
			Character character = user.GetAs<Character>();
			if (character.Storage.GetValue(ActorFSMKeys.RespawnTeleportRequired, false, true))
			{
				RespawnCharacter(character);
			}
		}

		// 06001646 (1.10.1 RVA 0x783FB0); the screen transition, when there is one, runs the setup at its midpoint and the
		// finish at its end, and the teleport happens in DoUpdate in between. Without it everything happens at once.
		private IEnumerator TriggerRespawn(Character character, float secondsInvulnerableAfterOutOfBounds)
		{
			ProcessManager.GetSystem<CameraYAxisProxyManager>().enabled = false;
			character.MovementStop(false);
			yield return new WaitForFixedUpdate();
			character.Storage.SetValue(ActorFSMKeys.RespawnInvulnerableDuration, secondsInvulnerableAfterOutOfBounds);
			if (m_doFullscreenTransition && !ProcessManager.GetSystem<App>().Storage.GetValue(AppFSMKeys.RestartInProgress, false, true))
			{
				ProcessManager.GetSystem<UIScreenTransitionManager>().QueueTransition(m_transitionOverrideContainer, () => SetupRespawn(character), () => FinishRespawn(character));
			}
			else
			{
				SetupRespawn(character);
				RespawnCharacter(character);
				FinishRespawn(character);
			}
			ProcessManager.GetSystemSafe<MissionManager>()?.IncrementActiveMissionDeathCount();
		}

		// 06001647 (1.10.1 RVA 0x7844C0); any invulnerability still running is ended and restarted for the stored duration.
		private static void SetupRespawn(Character character)
		{
			character.Storage.SetValue(ActorFSMKeys.RespawnTeleportRequired, true);
			character.MovementResume(character.WorldPosition, Vector3.zero);
			ProcessManager.GetSystem<CinemachineCameraManager>().RequestCharacterCameraChange(CameraType.None);
			if (character.TryGetAbility(ActorAbilityType.Character_Invulnerability, out CharacterAbility_Invulnerability invulnerability))
			{
				invulnerability.Finish();
				invulnerability.TriggerForDuration(character.Storage.GetValue(ActorFSMKeys.RespawnInvulnerableDuration, 0f, true));
			}
		}

		// 06001648 (1.10.1 RVA 0x7847A0)
		private static void FinishRespawn(Character character)
		{
			character.Storage.SetValue(ActorFSMKeys.RespawnEffectsComplete, true);
		}

		// 06001649 (1.10.1 RVA 0x7842D0)
		private static void RespawnCharacter(Character character)
		{
			character.TeleportWithStatusKey(ActorFSMKeys.RespawnTeleportRequired);
			CharacterManager characterManager = ProcessManager.GetSystem<CharacterManager>();
			character.Storage.RemoveValue<bool>(ActorFSMKeys.RespawnTeleportRequired);
			character.AllowCollisions(true);
			characterManager.FireOnCharacterRespawned();
			CoroutineUtils.RunCoroutine(SnapCameras());
		}

		// 0600164a (1.10.1 RVA 0x7848A0); the cameras are snapped to the respawned character two physics steps later.
		private static IEnumerator SnapCameras()
		{
			yield return new WaitForFixedUpdate();
			yield return new WaitForFixedUpdate();
			CameraYAxisProxyManager yAxisProxyManager = ProcessManager.GetSystemSafe<CameraYAxisProxyManager>();
			if (yAxisProxyManager != null)
			{
				yAxisProxyManager.SnapToRotation();
				yAxisProxyManager.enabled = true;
			}
			CinemachineCameraManager cameraManager = ProcessManager.GetSystemSafe<CinemachineCameraManager>();
			if (cameraManager != null)
			{
				cameraManager.SnapToCharacter(true, false);
				cameraManager.ResetYAxisRecenteringOnAllCameras();
			}
		}
	}
}
