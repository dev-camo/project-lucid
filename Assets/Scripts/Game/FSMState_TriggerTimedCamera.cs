using Unity.IL2CPP.CompilerServices;
using System.Collections;
using Hardlight;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000694; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Generic/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("TriggerTimedCamera")]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class FSMState_TriggerTimedCamera : FSMState_Trigger
	{
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		protected class JSONCtorArgs : TriggerJSONCtorArgs
		{
			public CameraType CameraType;

			public float ShowForSeconds;

			public TimeCategory TimeCategory;

			// 06002357 (1.10.1 RVA 0x586760)
			public JSONCtorArgs()
			{
			}
		}

		private CameraType m_cameraType;

		private float m_showForSeconds;

		private TimeCategory m_timeCategory;

		// 06002353 (1.10.1 RVA 0x586480)
		protected FSMState_TriggerTimedCamera(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
			m_cameraType = ctorArgs.CameraType;
			m_showForSeconds = ctorArgs.ShowForSeconds;
			m_timeCategory = ctorArgs.TimeCategory;
		}

		// 06002354 (1.10.1 RVA 0x5864C0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new FSMState_TriggerTimedCamera(fsm, transitionId, args);
		}

		// 06002355 (1.10.1 RVA 0x586570): applies the camera type override and schedules its removal.
		protected override void InvokeTriggerStart(IGraphUser user, FSMStateChangeAction action)
		{
			SystemRef<CinemachineCameraManager> cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>();
			if (cameraManagerRef.IsNull())
			{
				return;
			}
			StackableDataHandle handle = cameraManagerRef.Get().ApplyCameraTypeOverride(m_cameraType);
			CoroutineUtils.RunCoroutine(RemoveOverrideRoutine(handle, m_showForSeconds, m_timeCategory));
		}

		// 06002356 (1.10.1 RVA 0x5866B0); MoveNext 0600235a (RVA 0x586780). The manager is looked up again
		// after the wait, and the override is only removed while it is still valid.
		private IEnumerator RemoveOverrideRoutine(StackableDataHandle handle, float showForSeconds, TimeCategory timeCategory)
		{
			yield return TimeScaledUtilities_SDT.WaitForFixedSeconds(showForSeconds, timeCategory);
			SystemRef<CinemachineCameraManager> cameraManagerRef = ProcessManager.GetSystemRef<CinemachineCameraManager>();
			if (cameraManagerRef.IsValid())
			{
				cameraManagerRef.Get().RemoveCameraSettingOverride(handle);
			}
		}
	}
}
