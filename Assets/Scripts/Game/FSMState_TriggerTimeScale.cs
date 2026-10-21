using Unity.IL2CPP.CompilerServices;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000697; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("TriggerTimeScale")]
	[GraphNodeMenuFormat("Generic/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class FSMState_TriggerTimeScale : FSMState_Trigger
	{
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class JSONCtorArgs : TriggerJSONCtorArgs
		{
			[GraphUnityObject(typeof(TimeScaleDefinition))]
			public string TimeScaleDefinitionName;

			// 06002363 (1.10.1 RVA 0x587070)
			public JSONCtorArgs()
			{
			}
		}

		private readonly TimeScaleDefinition m_timeScaleDefinition;

		// 0600235e (1.10.1 RVA 0x586910)
		protected FSMState_TriggerTimeScale(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			DataManagerUtilities.TryGetTimeScaleDefinition(ctorArgs.TimeScaleDefinitionName, out m_timeScaleDefinition, "State " + stateId.Name);
		}

		// 0600235f (1.10.1 RVA 0x5869C0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new FSMState_TriggerTimeScale(fsm, transitionId, args);
		}

		// 06002360 (1.10.1 RVA 0x586AC0): the user's active-time-scale table is created on first use.
		protected override void InvokeTriggerStart(IGraphUser user, FSMStateChangeAction action)
		{
			Dictionary<TimeScaleDefinition, bool> timeScalesActive = user.Storage.GetValue<Dictionary<TimeScaleDefinition, bool>>(FSMStorageKeys.TimeScalesActive, null, true);
			if (timeScalesActive == null)
			{
				timeScalesActive = new Dictionary<TimeScaleDefinition, bool>();
				user.Storage.SetValue(FSMStorageKeys.TimeScalesActive, timeScalesActive);
			}
			CoroutineUtils.RunCoroutine(UpdateTimeSettingRoutine(timeScalesActive));
		}

		// 06002361 (1.10.1 RVA 0x586E30); MoveNext 06002366 (RVA 0x587090). Plays the definition's curve over
		// unscaled fixed time while the definition stays flagged active, then removes the time setting.
		private IEnumerator UpdateTimeSettingRoutine(Dictionary<TimeScaleDefinition, bool> timeScalesActive)
		{
			WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();
			TimeManager timeManager = ProcessManager.GetSystem<TimeManager>();
			TimeSetting timeSetting = TimeSetting.GetDefault(1f);
			StackableDataHandle timeHandle = timeManager.ApplyTimeSetting(timeSetting);
			timeScalesActive[m_timeScaleDefinition] = true;
			float timer = 0f;
			float timeTotal = m_timeScaleDefinition.TimeScale[m_timeScaleDefinition.TimeScale.length - 1].time;
			while (timeTotal > timer && timeScalesActive[m_timeScaleDefinition])
			{
				timer += Time.fixedUnscaledDeltaTime;
				timeSetting.SetCategory(m_timeScaleDefinition.TimeCategory, m_timeScaleDefinition.TimeScale.Evaluate(timer));
				timeManager.UpdateTimeSetting(timeHandle, timeSetting);
				yield return waitForFixedUpdate;
			}
			timeManager.RemoveTimeSetting(timeHandle);
			timeScalesActive[m_timeScaleDefinition] = false;
		}

		// 06002362 (1.10.1 RVA 0x586ED0): ending only clears the active flag; the routine unwinds itself.
		protected override void InvokeTriggerEnd(IGraphUser user, FSMStateChangeAction action)
		{
			user.Storage.GetValue<Dictionary<TimeScaleDefinition, bool>>(FSMStorageKeys.TimeScalesActive, null, true)[m_timeScaleDefinition] = false;
		}
	}
}
