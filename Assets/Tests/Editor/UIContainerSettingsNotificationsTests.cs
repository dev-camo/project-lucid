using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine.UI;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerSettingsNotifications (Game.Runtime 0x0200093b, Start 0x67DC80, Action_SetNotificationPermission 0x67DD00,
    // constructor 0x67DDB0) as a real component beside a real Toggle on an owned inactive host. The NotificationManager is
    // the real one over the real SaveManager of OwnedCharacterManagers, holding the notification save data the original
    // assigns while opening a save. The platform bridge is the shipped stub (permission always granted). The HLNotifications
    // singleton that Grant/RetractPermission reach is an owned component on an inactive host, installed as the singleton
    // (its Awake would register a system row) and removed afterwards.
    public sealed class UIContainerSettingsNotificationsTests
    {
        // Reflection: MonoSingleton<T>.Instance has a private setter that the component's Awake calls; its Awake would register a system row.
        private static readonly MethodInfo SetNotificationsInstance =
            typeof(MonoSingleton<HLNotifications>).GetProperty("Instance").GetSetMethod(true);

        private sealed class Rig
        {
            public UIContainerSettingsNotifications Container;
            public NotificationManager Manager;
            public SaveDataNotifications Saved;
            public Toggle Toggle;
            public List<bool> Changes = new List<bool>();
        }

        private static void Run(bool permitted, Action<Owned, Rig> body)
        {
            SettingsPageTestSupport.InitialiseAnalytics(); // the collector's type initialiser runs over owned rows here, not in whichever case reports first
            Owned.Run(owned =>
            {
                // Taken first, so it is released last: it owns the SaveManager, App and DataManager rows.
                owned.Lease(new OwnedCharacterManagers());
                if (HLNotifications.Instance != null) throw new InvalidOperationException("Fixture requires no live HLNotifications instance");
                var rig = new Rig();
                SaveManager save = ProcessManager.GetSystem<SaveManager>();
                // The original assigns both while opening a save from the property store.
                // Reflection: SaveManager.OnPropertyStoreLoad (0x06002d22) is private and needs a loaded property store; it assigns both.
                Set(save, "m_saveAnalytics", new SaveDataAnalytics());
                rig.Saved = new SaveDataNotifications();
                rig.Saved.Initialise();
                rig.Saved.NotificationsPermitted = permitted;
                Set(save, "m_saveDataNotifications", rig.Saved); // Reflection: SaveManager.OnPropertyStoreLoad (0x06002d22) assigns it; see the m_saveAnalytics write above

                var notifications = owned.Host("Lucid notifications singleton", false).AddComponent<HLNotifications>();
                SetNotificationsInstance.Invoke(null, new object[] { notifications }); // Reflection: private singleton setter, see SetNotificationsInstance
                owned.Lease(new ReleaseAction(() => SetNotificationsInstance.Invoke(null, new object[] { null })));

                rig.Manager = owned.Host("Lucid notification manager", false).AddComponent<NotificationManager>();
                Type notification = typeof(NotificationManager).GetNestedType("SDTNotification", BindingFlags.NonPublic);
                Set(rig.Manager, "m_notifications", Array.CreateInstance(notification, 0)); // serialized in the original
                owned.Register(rig.Manager);
                Call(rig.Manager, "OnSaveManagerValid", save); // the manager reads the saved answer

                rig.Container = owned.Host("Lucid notifications page", false).AddComponent<UIContainerSettingsNotifications>();
                rig.Toggle = rig.Container.gameObject.AddComponent<Toggle>();
                rig.Toggle.onValueChanged.AddListener(rig.Changes.Add);
                Set(rig.Container, "m_notificationsPermission", rig.Toggle); // serialized in the original
                body(owned, rig);
            });
        }

        // Start 0x67DC80 shows the manager's NotificationPermissionFullyGranted (0x5C76E0) on the toggle without notifying.
        [Test]
        public void StartShowsAGrantedPermissionWithoutNotifying()
        {
            Run(true, (owned, rig) =>
            {
                Assert.That(rig.Toggle.isOn, Is.False);

                Call(rig.Container, "Start");

                Assert.That(rig.Toggle.isOn, Is.True);
                Assert.That(rig.Changes, Is.Empty);
            });
        }

        [Test]
        public void StartShowsARefusedPermissionWithoutNotifying()
        {
            Run(false, (owned, rig) =>
            {
                rig.Toggle.SetIsOnWithoutNotify(true);

                Call(rig.Container, "Start");

                Assert.That(rig.Toggle.isOn, Is.False);
                Assert.That(rig.Changes, Is.Empty);
            });
        }

        // Action_SetNotificationPermission(true) grants (0x67DD5E calls 0x5C7FF0): the user's answer and the saved flag turn on.
        [Test]
        public void TurningThePermissionOnGrantsIt()
        {
            Run(false, (owned, rig) =>
            {
                Assert.That(rig.Manager.NotificationPermissionFullyGranted, Is.False);

                rig.Container.Action_SetNotificationPermission(true);

                Assert.That(rig.Manager.NotificationPermissionFullyGranted, Is.True);
                Assert.That(rig.Saved.NotificationsPermitted, Is.True);
            });
        }

        // Action_SetNotificationPermission(false) retracts (0x67DD8D calls 0x5C8030).
        [Test]
        public void TurningThePermissionOffRetractsIt()
        {
            Run(true, (owned, rig) =>
            {
                Assert.That(rig.Manager.NotificationPermissionFullyGranted, Is.True);

                rig.Container.Action_SetNotificationPermission(false);

                Assert.That(rig.Manager.NotificationPermissionFullyGranted, Is.False);
                Assert.That(rig.Saved.NotificationsPermitted, Is.False);
            });
        }
    }
}
