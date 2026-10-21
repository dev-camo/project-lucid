using Unity.IL2CPP.CompilerServices;
using System;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200018f; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Application/{0}")]
	public class ApplicationTransitionRequestNotifications : FSMTransition
	{
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Serializable]
		public class JSONCtorArgs
		{
			public bool TransitionIfTrue;

			public NotificationType Notification;

			// 06000893 (1.10.1 RVA 0x666820)
			public JSONCtorArgs()
			{
			}
		}

		private bool m_transitionIfTrue;

		private NotificationType m_notification;

		// 06000890 (1.10.1 RVA 0x666500)
		private ApplicationTransitionRequestNotifications(FiniteStateMachine fsm, FSMIdentifier transitionId, JSONCtorArgs ctorArgs) : base(fsm, transitionId)
		{
			m_transitionIfTrue = ctorArgs.TransitionIfTrue;
			m_notification = ctorArgs.Notification;
		}

		// 06000891 (1.10.1 RVA 0x666530)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
			return new ApplicationTransitionRequestNotifications(fsm, transitionId, args);
		}

		// 06000892 (1.10.1 RVA 0x6665D0): with TransitionIfTrue the transition fires, and publishes the
		// notification-request container parameters, only when the notification can be requested;
		// otherwise it fires exactly when it cannot, without publishing.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (m_transitionIfTrue)
			{
				if (!ProcessManager.GetSystem<NotificationManager>().CanRequestNotification(m_notification))
				{
					return false;
				}
				user.Storage.SetValue<IUIContainerParameters>(AppFSMKeys.UIContainerParameters, new UIContainerNotificationRequestParameters(m_notification));
				return true;
			}
			return !ProcessManager.GetSystem<NotificationManager>().CanRequestNotification(m_notification);
		}
	}
}
