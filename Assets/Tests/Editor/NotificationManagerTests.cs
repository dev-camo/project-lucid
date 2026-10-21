using System;
using Hardlight;
using Hardlight.Analytics;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime NotificationManager (TypeDef 0x0200072d, x86-64 0x5C76C0..0x5C8130) and
    // ApplicationTransitionRequestNotifications (0x0200018f, 0x666500..0x666810). The manager is a component on an owned
    // inactive host with its authored notification list; its SaveManager is the real one OwnedCharacterManagers registers,
    // given the analytics and notification save data the original assigns while opening a save (private, no setter), and
    // its session count comes from a real AnalyticsSessionManager in its absent row. The platform notification bridge is
    // the shipped stub (permission always granted). Not covered: scheduling (ScheduleNotification, SetAllUnlockedNotifications,
    // OnApplicationFocus), which goes through the HLNotifications singleton, the challenge-refresh timer and
    // AreChallengesUnlocked, which need a live ChallengeManager, and Grant/RetractPermission, which register with or cancel
    // through that singleton.
    public sealed class NotificationManagerTests
    {
        private static readonly FSMUpdateContext s_context = new FSMUpdateContext(0.25f, FSMUpdateType.Manual);

        private sealed class Scene
        {
            public Owned Owned;
            public SaveManager Save;
            public SaveDataNotifications Saved;
            public AnalyticsSessionManager Sessions;
            public NotificationManager Manager;
        }

        private static void SetSerialized(Object target, string field, Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(target);
            assign(serialized.FindProperty(field));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // The manager has read the save data (OnSaveManagerValid, which Start passes to InvokeOnValid and which runs at
        // once for a live SaveManager); `before` runs first, to prepare the save data it reads.
        private static void WithManager(bool permitted, int sessionNumber, NotificationType[] configured,
            Action<Scene> body, Action<Scene> before = null) => Owned.Run(owned =>
        {
            // Taken first, so it is released last: it owns the SaveManager, App and DataManager rows.
            owned.Lease(new OwnedCharacterManagers());
            var scene = new Scene { Owned = owned, Save = ProcessManager.GetSystem<SaveManager>() };
            // The original assigns both while opening a save from the property store.
            var analytics = new SaveDataAnalytics { SessionNumber = sessionNumber };
            Set(scene.Save, "m_saveAnalytics", analytics);
            scene.Saved = new SaveDataNotifications();
            scene.Saved.Initialise();
            scene.Saved.NotificationsPermitted = permitted;
            Set(scene.Save, "m_saveDataNotifications", scene.Saved);

            owned.LeaseActions(SystemAction.AppInitialise, SystemAction.Update, SystemAction.AppShutdown);
            scene.Sessions = new AnalyticsSessionManager();
            owned.Lease(new ReleaseAction(() => ProcessManager.UnsubscribeFromAllActions(scene.Sessions)));
            owned.Register(scene.Sessions);
            // The session number is read from the saved analytics data, as it is when the save manager becomes valid.
            scene.Sessions.LoadFromSaveData(scene.Save);

            scene.Manager = owned.Host("Lucid notification manager", false).AddComponent<NotificationManager>();
            // An empty authored list is an empty array, which a single resize of the unset (null) field does not create.
            if (configured.Length == 0)
                SetSerialized(scene.Manager, "m_notifications", property => property.arraySize = 1);
            SetSerialized(scene.Manager, "m_notifications", property =>
            {
                property.arraySize = configured.Length;
                for (int index = 0; index < configured.Length; index++)
                {
                    SerializedProperty element = property.GetArrayElementAtIndex(index);
                    element.FindPropertyRelative("Notification").intValue = (int)configured[index];
                    element.FindPropertyRelative("NotificationCategory").stringValue = "lucid-category";
                }
            });
            before?.Invoke(scene);
            owned.Register(scene.Manager);
            Call(scene.Manager, "OnSaveManagerValid", scene.Save);
            body(scene);
        });

        // CanRequestNotification 0x5C7E80: an unconfigured type fails (loop exit, 0x5c7eeb); a configured one fails once its
        // RequestedPermission flag is set (cmp byte [+0x28], 0x5c7ee5), then needs the session count to reach two
        // (cmp [+0x28], 2; jl at 0x5c7f27), then asks again unless the user already allowed notifications and the system
        // permission is granted (0x5c7f2d..0x5c7f3d).
        [Test]
        public void ARequestNeedsAConfiguredTypeASecondSessionAndNoGrantedPermission()
        {
            WithManager(false, 2, new[] { NotificationType.ChallengeRefresh }, scene =>
            {
                Assert.That(scene.Manager.CanRequestNotification(NotificationType.ChallengeRefresh), Is.True);
                Assert.That(scene.Manager.CanRequestNotification(NotificationType.LapsedPlayer), Is.False, "not configured");
            });
            WithManager(false, 1, new[] { NotificationType.ChallengeRefresh }, scene =>
                Assert.That(scene.Manager.CanRequestNotification(NotificationType.ChallengeRefresh), Is.False, "first session"));
            WithManager(false, 0, new[] { NotificationType.ChallengeRefresh }, scene =>
                Assert.That(scene.Manager.CanRequestNotification(NotificationType.ChallengeRefresh), Is.False, "no session"));
            WithManager(false, 7, new[] { NotificationType.ChallengeRefresh }, scene =>
                Assert.That(scene.Manager.CanRequestNotification(NotificationType.ChallengeRefresh), Is.True, "a later session"));
            WithManager(true, 2, new[] { NotificationType.ChallengeRefresh }, scene =>
                Assert.That(scene.Manager.CanRequestNotification(NotificationType.ChallengeRefresh), Is.False, "already allowed and granted"));
        }

        // SyncSaveData 0x5C7A10 copies each saved item's RequestedPermission (+0x18) onto the configured notification of
        // the same type (+0x28, compare at 0x5c7b03) and takes the permitted flag (+0x12) as the user's answer (0x5c7b8c).
        [Test]
        public void LoadingTheSaveRestoresWhichNotificationsWereAlreadyRequested()
        {
            WithManager(false, 3, new[] { NotificationType.ChallengeRefresh, NotificationType.LapsedPlayer }, scene =>
            {
                Assert.That(scene.Manager.CanRequestNotification(NotificationType.ChallengeRefresh), Is.False, "saved as requested");
                Assert.That(scene.Manager.CanRequestNotification(NotificationType.LapsedPlayer), Is.True, "not in the save");
            }, scene => scene.Saved.TryGetOrNew(NotificationType.ChallengeRefresh).RequestedPermission = true);
        }

        // NotificationPermissionFullyGranted 0x5C76E0: the user's answer (+0x48) gates the platform permission query
        // (IsPermissionGranted, 0x5c76ed), which the shipped bridge answers true.
        [Test]
        public void PermissionIsFullyGrantedOnlyAfterTheUserAllowedNotifications()
        {
            WithManager(false, 3, new NotificationType[0], scene =>
                Assert.That(scene.Manager.NotificationPermissionFullyGranted, Is.False));
            WithManager(true, 3, new NotificationType[0], scene =>
                Assert.That(scene.Manager.NotificationPermissionFullyGranted, Is.True));
        }

        // IsRequestingNotification 0x5C7F50 sets RequestedPermission on the configured notification of the type (0x5c7fa6),
        // records it through SaveDataNotifications.TryGetOrNew and the item's RequestedPermission setter (0x5c7fb2,
        // 0x5c7fc0) and finally requests a save (tail call 0x5c7fe4). The other types are untouched.
        [Test]
        public void RequestingANotificationRecordsItInTheManagerAndTheSave()
        {
            WithManager(false, 3, new[] { NotificationType.ChallengeRefresh, NotificationType.LapsedPlayer }, scene =>
            {
                Assert.That(scene.Manager.CanRequestNotification(NotificationType.ChallengeRefresh), Is.True);
                Assert.That(scene.Saved.NotificationSaveData, Is.Empty);
                scene.Saved.MarkDirty();
                Assert.That(scene.Saved.HasChangesToSave(), Is.True);

                scene.Manager.IsRequestingNotification(NotificationType.ChallengeRefresh);

                Assert.That(scene.Manager.CanRequestNotification(NotificationType.ChallengeRefresh), Is.False);
                Assert.That(scene.Manager.CanRequestNotification(NotificationType.LapsedPlayer), Is.True);
                Assert.That(scene.Saved.NotificationSaveData.Count, Is.EqualTo(1));
                Assert.That(scene.Saved.NotificationSaveData[0].Notification, Is.EqualTo(NotificationType.ChallengeRefresh));
                Assert.That(scene.Saved.NotificationSaveData[0].RequestedPermission, Is.True);
                Assert.That(scene.Saved.HasChangesToSave(), Is.False, "RequestSaveNotifications marks the save data saved");
            });
        }

        // IsRequestingNotification 0x5C7F50 reaches TryGetOrNew only inside the loop, for a configured notification of the
        // requested type (jne skip at 0x5c7fa0, call at 0x5c7fb2), so a type nobody configured leaves the save untouched.
        [Test]
        public void RequestingAnUnconfiguredNotificationLeavesTheSaveAndTheConfiguredOnesUntouched()
        {
            WithManager(false, 3, new[] { NotificationType.ChallengeRefresh }, scene =>
            {
                scene.Manager.IsRequestingNotification(NotificationType.LapsedPlayer);

                Assert.That(scene.Saved.NotificationSaveData, Is.Empty);
                Assert.That(scene.Manager.CanRequestNotification(NotificationType.ChallengeRefresh), Is.True);
            });
        }

        // LoadFromSaveData 0x5C78C0 gates on SaveManager.HasSaveDataAnalytics (call at 0x5c78d0), not on the notification data
        // SyncSaveData reads: with no analytics data it defers to the WaitForNotificationsToLoad coroutine (the refusing
        // coroutine host logs the engine error), so nothing is synced yet. That coroutine (MoveNext 0x5C8140) waits on
        // HasSaveDataNotifications (0x5c816b), which is present, so its first step syncs and finishes.
        [Test]
        public void WithoutAnalyticsDataTheSaveIsOnlySyncedByTheWaitingCoroutine()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Coroutine couldn't be started"));
            WithManager(false, 3, new[] { NotificationType.ChallengeRefresh }, scene =>
            {
                Assert.That(scene.Manager.CanRequestNotification(NotificationType.ChallengeRefresh), Is.True, "not synced by the load itself");

                var steps = (System.Collections.IEnumerator)Call(scene.Manager, "WaitForNotificationsToLoad");
                Assert.That(steps.MoveNext(), Is.False, "the notification data is present, so it syncs at once");

                Assert.That(scene.Manager.CanRequestNotification(NotificationType.ChallengeRefresh), Is.False, "saved as requested");
            }, scene =>
            {
                Set(scene.Save, "m_saveAnalytics", null);
                scene.Saved.TryGetOrNew(NotificationType.ChallengeRefresh).RequestedPermission = true;
            });
        }

        // CalculateTimeForType 0x5C7D00: the thirty-day timer adds the constant 30.0 days to DateTime.Now (AddDays at
        // 0x5c7dc3, double constant at 0x29a7a08); an unknown timer returns DateTime.Now unchanged (0x5c7dcb). The
        // challenge-refresh timer needs a ChallengeManager and is not covered.
        [Test]
        public void TheThirtyDayTimerFiresThirtyDaysFromNow()
        {
            WithManager(false, 3, new NotificationType[0], scene =>
            {
                DateTime before = DateTime.Now;
                var thirty = (DateTime)Call(scene.Manager, "CalculateTimeForType", NotificationTimerType.ThirtyDaysFromNow);
                var unknown = (DateTime)Call(scene.Manager, "CalculateTimeForType", (NotificationTimerType)12345);
                DateTime after = DateTime.Now;

                Assert.That(thirty, Is.GreaterThanOrEqualTo(before.AddDays(30)).And.LessThanOrEqualTo(after.AddDays(30)));
                Assert.That(unknown, Is.GreaterThanOrEqualTo(before).And.LessThanOrEqualTo(after));
            });
        }

        private static IFSMTransition RequestTransition(bool transitionIfTrue, NotificationType notification) =>
            ApplicationTransitionRequestNotifications.ConstructInstance(
                new FiniteStateMachine("Lucid-notification-node-" + Guid.NewGuid().ToString("N"), skipAddToManager: true),
                "Lucid-notification-node-" + Guid.NewGuid().ToString("N"),
                "{\"TransitionIfTrue\":" + (transitionIfTrue ? "true" : "false") + ",\"Notification\":" + (int)notification + "}");

        // DoUpdate 0x6665D0: with TransitionIfTrue, CanRequestNotification (0x66667c) decides; a request fires the transition
        // and publishes UIContainerNotificationRequestParameters under UIContainerParameters (0x66677b..0x6667f9) and
        // returns true (0x6667fc), a refusal returns false and publishes nothing. Without TransitionIfTrue the result is
        // the negation (xor 1 at 0x6666fd) and nothing is published.
        [Test]
        public void TheRequestTransitionFollowsTheManagerAndPublishesTheContainerParametersOnlyWhenArmed()
        {
            foreach ((bool armed, int session, bool fires, bool publishes) in new[]
            {
                (true, 3, true, true),
                (true, 1, false, false),
                (false, 3, false, false),
                (false, 1, true, false),
            })
            {
                WithManager(false, session, new[] { NotificationType.ChallengeRefresh }, scene =>
                {
                    var user = new FSMUser(new FSMStorage(0));
                    string label = "armed " + armed + ", session " + session;

                    Assert.That(RequestTransition(armed, NotificationType.ChallengeRefresh).Update(user, s_context), Is.EqualTo(fires), label);

                    bool stored = user.Storage.TryGetValue(AppFSMKeys.UIContainerParameters, out IUIContainerParameters parameters);
                    Assert.That(stored, Is.EqualTo(publishes), label);
                    if (publishes)
                    {
                        Assert.That(parameters, Is.TypeOf<UIContainerNotificationRequestParameters>(), label);
                        Assert.That(((UIContainerNotificationRequestParameters)parameters).Notification, Is.EqualTo(NotificationType.ChallengeRefresh), label);
                    }
                });
            }
        }
    }
}
