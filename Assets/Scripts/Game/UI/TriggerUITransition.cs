using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x0200044d; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class TriggerUITransition : MonoBehaviour
	{
		[SerializeField]
		private UIContainerIdentifier m_transitionContainer;

		// 0600195a (1.10.1 RVA 0x7A65D0): holds the transition at its midpoint through a wait
		// handle kept in the App storage, as App.AddWaitTransitionHandle does, then queues it.
		protected virtual void OnEnable()
		{
			App app = ProcessManager.GetSystem<App>(null, true);
			UIScreenTransitionManager transitionManager = ProcessManager.GetSystem<UIScreenTransitionManager>(null, true);
			List<StackableDataHandle> handles = app.Storage.GetValue<List<StackableDataHandle>>(AppFSMKeys.TransitionMidpointWaitHandles, null, false);
			if (handles == null)
			{
				handles = new List<StackableDataHandle>(1);
				app.Storage.SetValue(AppFSMKeys.TransitionMidpointWaitHandles, handles);
			}
			handles.Add(transitionManager.AddWaitForMidpointOverride());
			transitionManager.QueueTransition(m_transitionContainer);
		}
	}
}
