using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200010d; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class ApplicationStatePlayOutroSequences : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		protected class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventId;

			// 0600072a (1.10.1 RVA 0x651D50)
			public JSONCtorArgs()
			{
			}
		}

		private readonly ApplicationStateEvent m_stateEvent;

		private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>();

		// 06000723 (1.10.1 RVA 0x6515A0)
		private ApplicationStatePlayOutroSequences(FiniteStateMachine fsm, string stateName, ApplicationStateEvent stateEvent) : base(fsm, stateName)
		{
			m_stateEvent = stateEvent;
		}

		// 06000724 (1.10.1 RVA 0x651660)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			ApplicationStateEvent eventId = ApplicationStateEvent.FindByName(args.EventId);
			return new ApplicationStatePlayOutroSequences(fsm, (string)stateId, eventId);
		}

		// 06000725 (1.10.1 RVA 0x651700)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			if (ProcessManager.GetSystem<LevelManager>().TryGetCurrentLevel(out LevelManagerLevel level))
			{
				level.Data.PrepareOutroSequence(GetOutroSequenceIdentifier(), user, OnPlayOutroSequence);
			}
		}

		// 06000726 (1.10.1 RVA 0x651810)
		private OutroSequenceIdentifier GetOutroSequenceIdentifier()
		{
			MissionManager missionManager = m_missionManagerRef.Get();
			CollectableType rewardType = missionManager.ActiveMissionContext.MissionDefinition.RewardType;
			bool replay = missionManager.ActiveMissionPreviouslyCompleted();
			if (rewardType == CollectableType.Moon)
			{
				return replay ? OutroSequenceIdentifier.MoonReplay : OutroSequenceIdentifier.Moon;
			}
			if (rewardType == CollectableType.Orb && replay)
			{
				return OutroSequenceIdentifier.Replay;
			}
			return OutroSequenceIdentifier.Default;
		}

		// 06000727 (1.10.1 RVA 0x651910)
		private void OnPlayOutroSequence(IGraphUser user, GameObject outroSequenceInstance, Character character)
		{
			TargetedEffect effectData = new TargetedEffect
			{
				Target = character.transform,
				Offset = Vector3.zero
			};
			Quaternion.LookRotation(character.CameraForward, character.UpDirection);
			character.MovementStop();
			BeginEffectSequence(user, outroSequenceInstance.GetComponent<OutroSequence>(), effectData);
		}

		// 06000728 (1.10.1 RVA 0x651A50)
		private void BeginEffectSequence(IGraphUser user, OutroSequence outroSequence, TargetedEffect effectData)
		{
			outroSequence.Play(effectData);
			PublishMessage(user);
		}

		// 06000729 (1.10.1 RVA 0x651A80)
		private void PublishMessage(IGraphUser user)
		{
			user.Storage.RemoveValue<bool>(AppFSMKeys.RestartInProgress);
			user.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_stateEvent);
		}
	}
}
