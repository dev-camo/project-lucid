using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Analytics;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.Utils;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200072d; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class NotificationManager : MonoBehaviour, ISystem
	{
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[Serializable]
		private class SDTNotification
		{
			[HashEnum(null)]
			public Strings NotificationMessageID;

			public NotificationType Notification;

			public NotificationTimerType NotificationTimer;

			public string NotificationCategory;

			[NonSerialized]
			public bool RequestedPermission;

			// 0600287c (1.10.1 RVA 0x5C8120)
			public SDTNotification()
			{
			}
		}

		private const int SessionCountNotificationsValid = 2;

		[SerializeField]
		private SDTNotification[] m_notifications;

		private SystemRef<ChallengeManager> m_challengeManagerRef;

		private ChallengeManager m_challengeManager;

		private SystemRef<SaveManager> m_saveManagerRef;

		private SaveManager m_saveManager;

		private bool m_userToldAppCanNotify;

		private bool m_saveManagerReady;

		private bool m_challengeManagerReady;

		// 06002868 (1.10.1 RVA 0x5C76C0)
		private bool m_systemReady => m_challengeManagerReady && m_saveManagerReady;

		// 06002869 (1.10.1 RVA 0x5C76E0)
		public bool NotificationPermissionFullyGranted => m_userToldAppCanNotify && HLNotifications.IsPermissionGranted();

		// 0600286a (1.10.1 RVA 0x5C7700)
		private void Start()
		{
			m_challengeManagerRef = ProcessManager.GetSystemRef<ChallengeManager>();
			m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>();
			m_challengeManagerRef.InvokeOnValid(OnChallengeManagerValid);
			m_saveManagerRef.InvokeOnValid(OnSaveManagerValid);
			ProcessManager.RegisterSystem(this);
		}

		// 0600286b (1.10.1 RVA 0x5C7890)
		private void OnSaveManagerValid(SaveManager saveManager)
		{
			m_saveManager = saveManager;
			LoadFromSaveData();
		}

		// 0600286c (1.10.1 RVA 0x5C7950)
		private void OnChallengeManagerValid(ChallengeManager challengeManager)
		{
			m_challengeManager = challengeManager;
			m_challengeManagerReady = true;
		}

		// 0600286d (1.10.1 RVA 0x5C7980); MoveNext 0600287f (RVA 0x5C8140)
		private IEnumerator WaitForNotificationsToLoad()
		{
			while (!m_saveManager.HasSaveDataNotifications())
			{
				yield return null;
			}
			SyncSaveData();
		}

		// 0600286e (1.10.1 RVA 0x5C78C0): the original gate tests HasSaveDataAnalytics, not the
		// notification data that SyncSaveData reads; the coroutine waits on the notification data.
		public void LoadFromSaveData()
		{
			if (m_saveManager.HasSaveDataAnalytics())
			{
				SyncSaveData();
			}
			else
			{
				CoroutineUtils.RunCoroutine(WaitForNotificationsToLoad());
			}
		}

		// 0600286f (1.10.1 RVA 0x5C7A10): copies each saved item's permission request flag onto the
		// configured notification of the same type.
		private void SyncSaveData()
		{
			m_saveManagerReady = true;
			SDTNotification[] notifications = m_notifications;
			SaveDataNotifications saveDataNotifications = m_saveManager.GetSaveDataNotifications();
			for (int i = 0; i < notifications.Length; i++)
			{
				SDTNotification notification = notifications[i];
				foreach (SaveDataNotificationItem item in saveDataNotifications.NotificationSaveData)
				{
					if (notification.Notification == item.Notification)
					{
						notification.RequestedPermission = item.RequestedPermission;
					}
				}
			}
			m_userToldAppCanNotify = saveDataNotifications.NotificationsPermitted;
		}

		// 06002870 (1.10.1 RVA 0x5C7BC0): the challenge refresh reminder is held back until challenges unlock.
		public void SetAllUnlockedNotifications()
		{
			SDTNotification[] notifications = m_notifications;
			for (int i = 0; i < notifications.Length; i++)
			{
				SDTNotification notification = notifications[i];
				if (notification.Notification == NotificationType.ChallengeRefresh && !AreChallengesUnlocked())
				{
					continue;
				}
				ScheduleNotification(notification);
			}
		}

		// 06002871 (1.10.1 RVA 0x5C7C40)
		private void ScheduleNotification(SDTNotification notification)
		{
			DateTime fireDate = CalculateTimeForType(notification.NotificationTimer);
			HLNotifications.ScheduleLocalNotification(StringTable.GetString(notification.NotificationMessageID), fireDate, notification.Notification.GetString(), notification.NotificationCategory, null);
		}

		// 06002872 (1.10.1 RVA 0x5C7DE0): once both managers are ready, leaving the app with permission granted
		// schedules everything; any other focus change clears the schedule.
		private void OnApplicationFocus(bool hasFocus)
		{
			if (!m_systemReady)
			{
				return;
			}
			if (!hasFocus && m_userToldAppCanNotify && HLNotifications.IsPermissionGranted())
			{
				SetAllUnlockedNotifications();
			}
			else
			{
				CancelAllNotifications();
			}
		}

		// 06002873 (1.10.1 RVA 0x5C7E70)
		private void CancelAllNotifications()
		{
			HLNotifications.CancelAllNotifications();
		}

		// 06002874 (1.10.1 RVA 0x5C7D00)
		private DateTime CalculateTimeForType(NotificationTimerType notificationTimerType)
		{
			DateTime now = DateTime.Now;
			switch (notificationTimerType)
			{
				case NotificationTimerType.ThirtyDaysFromNow:
					return now.AddDays(30.0);
				case NotificationTimerType.OnChallengeRefresh:
					return now + m_challengeManager.GetChallengePeriodTimeRemaining();
				default:
					return now;
			}
		}

		// 06002875 (1.10.1 RVA 0x5C7C20)
		private bool AreChallengesUnlocked()
		{
			return m_challengeManager.FeatureState != ChallengeManager.ChallengeFeatureState.Locked;
		}

		// 06002876 (1.10.1 RVA 0x5C7E80): a configured notification whose request flag is clear can be
		// requested from the second session on, unless the user allowed notifications and the system
		// permission is granted.
		public bool CanRequestNotification(NotificationType notificationType)
		{
			foreach (SDTNotification notification in m_notifications)
			{
				if (notification.Notification == notificationType)
				{
					if (notification.RequestedPermission)
					{
						return false;
					}
					if (ProjectLucid.Offline.OfflineAdapters.Active) // Offline hook H-069: the original read the session number from the session manager, which offline is not registered; we read the same persisted value from the analytics save and keep the comparison (the original comparison stays in the else branch)
					{
						if (ProjectLucid.Offline.OfflineAnalyticsConsumers.SessionNumber(m_saveManager) < SessionCountNotificationsValid)
						{
							return false;
						}
					}
					else if (ProcessManager.GetSystem<AnalyticsSessionManager>().SessionNumber < SessionCountNotificationsValid)
					{
						return false;
					}
					return !m_userToldAppCanNotify || !HLNotifications.IsPermissionGranted();
				}
			}
			return false;
		}

		// 06002877 (1.10.1 RVA 0x5C7F50): records the request, on every configured notification of the type,
		// in the save data and saves.
		public void IsRequestingNotification(NotificationType notificationType)
		{
			SDTNotification[] notifications = m_notifications;
			SaveDataNotifications saveDataNotifications = m_saveManager.GetSaveDataNotifications();
			for (int i = 0; i < notifications.Length; i++)
			{
				SDTNotification notification = notifications[i];
				if (notification.Notification == notificationType)
				{
					notification.RequestedPermission = true;
					saveDataNotifications.TryGetOrNew(notificationType).RequestedPermission = notification.RequestedPermission;
				}
			}
			m_saveManager.RequestSaveNotifications();
		}

		// 06002878 (1.10.1 RVA 0x5C7FF0)
		public void GrantPermission()
		{
			SaveDataNotifications saveDataNotifications = m_saveManager.GetSaveDataNotifications();
			m_userToldAppCanNotify = true;
			saveDataNotifications.NotificationsPermitted = true;
			m_saveManager.RequestSaveNotifications();
			HLNotifications.RegisterForNotifications();
		}

		// 06002879 (1.10.1 RVA 0x5C8030)
		public void RetractPermission()
		{
			SaveDataNotifications saveDataNotifications = m_saveManager.GetSaveDataNotifications();
			m_userToldAppCanNotify = false;
			saveDataNotifications.NotificationsPermitted = false;
			m_saveManager.RequestSaveNotifications();
			HLNotifications.CancelAllNotifications();
		}

		// 0600287a (1.10.1 RVA 0x5C8070)
		public void Debug_TestNotificationInOneMinute()
		{
			HLNotifications.ScheduleLocalNotification("Your Test Notification is Ready", DateTime.UtcNow.AddMinutes(1.0), "SDT_Test", "SDT_Test", null);
		}

		// 0600287b (1.10.1 RVA 0x5C8110)
		public NotificationManager()
		{
		}
	}
}
