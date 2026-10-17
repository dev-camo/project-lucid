using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200012f; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Application/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class ApplicationStateUnloadUnusedAssets : FSMState
	{
		[Serializable]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class JSONCtorArgs
		{
			[GraphUnityObject(typeof(ApplicationStateEvent))]
			public string EventId;

			public int FrameDelayCount;

			// 0600077b (1.10.1 RVA 0x656230)
			public JSONCtorArgs()
			{
			}
		}

		private readonly ApplicationStateEvent m_stateEvent;

		private readonly int m_frameDelayCount;

		// 06000776 (1.10.1 RVA 0x655E60)
		protected ApplicationStateUnloadUnusedAssets(FiniteStateMachine fsm, FSMIdentifier stateId, ApplicationStateEvent stateEvent, int frameDelayCount) : base(fsm, stateId)
		{
			m_stateEvent = stateEvent;
			m_frameDelayCount = frameDelayCount;
		}

		// 06000777 (1.10.1 RVA 0x655EA0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationStateUnloadUnusedAssets(fsm, stateId, ApplicationStateEvent.FindByName(args.EventId), args.FrameDelayCount);
		}

		// 06000778 (1.10.1 RVA 0x655F60)
		protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
		{
			base.DoOnEnter(user, action);
			CoroutineUtils.RunCoroutine(ReleaseUnusedAssets(user));
		}

		// 06000779 (1.10.1 RVA 0x655FF0); MoveNext 0600077e (RVA 0x656250)
		private IEnumerator ReleaseUnusedAssets(IGraphUser user)
		{
			for (int i = 0; i < m_frameDelayCount; i++)
			{
				yield return null;
			}
			AsyncOperation operation = Resources.UnloadUnusedAssets();
			operation.priority = 0;
			yield return operation;
			PublishMessage(user);
		}

		// 0600077a (1.10.1 RVA 0x656090)
		private void PublishMessage(IGraphUser user)
		{
			user.Storage.GetValue<List<ApplicationStateEvent>>(AppFSMKeys.StateEvents, null, true).AddUnique(m_stateEvent);
		}
	}
}
