using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000124; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ApplicationStateStartUITransition : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		private class JSONCtorArgs
		{
			[GraphUnityObject(typeof(UIContainerIdentifier))]
			public string TransitionContainer;

			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventOnMidpoint;

			public bool WaitAtMidpoint;

			public bool QueueIfTransitionInProgress = true;

			// 06000760 (1.10.1 RVA 0x654C70)
			public JSONCtorArgs()
			{;}
		}

		private readonly ApplicationStateEvent m_eventOnMidpoint;

		private readonly UIContainerIdentifier m_transitionContainerId;

		private readonly bool m_waitAtMidpoint;

		private readonly bool m_queueIfTransitionInProgress;

		// 0600075b (1.10.1 RVA 0x6546D0)
		private ApplicationStateStartUITransition(FiniteStateMachine fsm, string stateName, ApplicationStateEvent eventOnMidpoint, UIContainerIdentifier transitionContainerId, bool waitAtMidpoint, bool queueIfTransitionInProgress) : base(fsm, stateName)
		{
			m_transitionContainerId = transitionContainerId;
			m_eventOnMidpoint = eventOnMidpoint;
			m_waitAtMidpoint = waitAtMidpoint;
			m_queueIfTransitionInProgress = queueIfTransitionInProgress;
		}

		// 0600075c (1.10.1 RVA 0x654750)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			ApplicationStateEvent eventOnMidpoint = ApplicationStateEvent.FindByName(args.EventOnMidpoint);
			UIContainerIdentifier transitionContainer = string.IsNullOrEmpty(args.TransitionContainer) ? null : UIContainerIdentifier.FindByName(args.TransitionContainer);
			bool waitAtMidpoint = args.WaitAtMidpoint;
			bool queueIfTransitionInProgress = args.QueueIfTransitionInProgress;
			return new ApplicationStateStartUITransition(fsm, stateId, eventOnMidpoint, transitionContainer, waitAtMidpoint, queueIfTransitionInProgress);
		}

		// 0600075d (1.10.1 RVA 0x654860). The lambdas compile to <>c__DisplayClass7_0:
		// .ctor 06000761 (0x654A50), b__0 06000762 (0x654C80), b__1 06000763 (0x654CA0).
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			App app = user.GetAs<App>();
			UIScreenTransitionManager transitionManager = ProcessManager.GetSystem<UIScreenTransitionManager>();
			if (!transitionManager.IsTransitionActive() || m_queueIfTransitionInProgress)
			{
				if (m_waitAtMidpoint)
				{
					app.AddWaitTransitionHandle();
				}
				transitionManager.QueueTransition(m_transitionContainerId, () => PublishMessage(user), null, () => OnTransitionTimeout(user));
			}
			else if (transitionManager.HasReachedMidPoint())
			{
				PublishMessage(user);
			}
		}

		// 0600075e (1.10.1 RVA 0x654A60)
		private void PublishMessage(IGraphUser user)
		{
			user.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_eventOnMidpoint);
		}

		// 0600075f (1.10.1 RVA 0x654C00)
		private void OnTransitionTimeout(IGraphUser user)
		{
			HLOutput.LogError("Failed to complete UI transition: attempting to continue");
			PublishMessage(user);
		}
	}
}
