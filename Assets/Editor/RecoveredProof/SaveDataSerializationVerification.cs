using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hardlight;
using Hardlight.Analytics;
using HardlightProject;
using UnityEngine;

namespace ProjectLucid
{
    // Actual Unity JSON and durable property-store proof for the native-derived
    // record classes. Original SaveManager slot orchestration remains separate.
    public static class SaveDataSerializationVerification
    {
        private static int checks;
        private static readonly string[] StoreStatics = { "s_internalInstance", "SaveHandlers", "ModifySaveHandlers",
            "ResolveConflictHandlers", "SaveSuccessHandlers", "AfterConflictsResolvedHandlers", "LoadHandlers" };

        public static void Run()
        {
            checks = 0;
            VerifySettings();
            VerifyAnalytics();
            VerifyNotifications();
            VerifyPropertyStore();
            Debug.Log("[Project Lucid] Save-data Unity JSON/storage checks passed: " + checks);
        }

        private static SaveDataSettings Settings()
        {
            var settings = new SaveDataSettings { MasterVolume = 0.75f, MusicVolume = 0.25f,
                PerformanceProfileName = "fixture-profile", RenderScale = 0.875f,
                LastWhatsNewVersion = "1.10.1", SkipFTUE = true };
            var component = settings.GetOrCreateEditableUIComponentData("ControlStick");
            component.Position = new Vector2(10.5f, -3.25f); component.Scale = 1.75f; component.UserSet = true;
            settings.Debug.UnlockLevels = true;
            return settings;
        }

        private static void VerifySettings()
        {
            SaveDataSettings settings = Settings();
            string json = JsonUtility.ToJson(settings);
            Require(json.Contains("\"m_masterVolume\":0.75") && json.Contains("\"m_musicVolume\":0.25"), "original private settings fields serialize");
            Require(json.Contains("\"m_editableUIComponents\"") && json.Contains("\"m_debug\""), "nested original records serialize");
            Require(!json.Contains("m_dirty") && !json.Contains("m_savingEnabled") && !json.Contains("m_onCamera"), "runtime dirty/enabled/delegates are excluded");
            SaveDataSettings restored = JsonUtility.FromJson<SaveDataSettings>(json);
            Require(restored.MasterVolume == 0.75f && restored.MusicVolume == 0.25f && restored.RenderScale == 0.875f && restored.PerformanceProfileName == "fixture-profile", "settings scalars survive JSON");
            Require(restored.LastWhatsNewVersion == "1.10.1" && restored.SkipFTUE, "version and FTUE fields survive JSON");
            Require(restored.EditableUIComponents.Count == 1 && restored.EditableUIComponents[0].ID == "ControlStick", "editable list and ID survive JSON");
            SaveDataSettingsEditableUIComponent component = restored.EditableUIComponents[0];
            Require(component.Position.x == 10.5f && component.Position.y == -3.25f && component.Scale == 1.75f && component.UserSet, "editable layout survives JSON");
            Require(restored.Debug.UnlockLevels, "nested debug field survives JSON");
            Require(SavingEnabled(restored) && Dirty(restored), "matching Unity restores the root settings constructor's runtime defaults");
            Require(!SavingEnabled(component) && !Dirty(component), "parameterized-only editable child retains zero runtime flags after Unity deserialization");
            restored.MarkSaved(); component.Scale = 2.25f;
            Require(Dirty(component) && !component.HasChangesToSave() && !restored.HasChangesToSave(), "original disabled child does not bubble changes through a clean parent");
            restored.MarkSaved();
            Require(Dirty(component), "original MarkSaved leaves disabled child dirty state intact");
            Debug.Log("[Project Lucid] JSON settings runtime flags: enabled=" + SavingEnabled(restored) + ", dirty=" + Dirty(restored) + ", childEnabled=" + SavingEnabled(component));

            string duplicates = "{\"m_editableUIComponents\":[{\"m_id\":\"Stick\",\"m_scale\":1},{\"m_id\":\"sTICK\",\"m_scale\":2,\"m_userSet\":true},{\"m_id\":\"STICK\",\"m_scale\":3},{\"m_id\":\"   \"}]}";
            SaveDataSettings normalized = JsonUtility.FromJson<SaveDataSettings>(duplicates);
            Require(normalized.EditableUIComponents.Count == 1 && normalized.EditableUIComponents[0].ID == "sTICK" && normalized.EditableUIComponents[0].Scale == 2,
                "Unity invokes the original case-insensitive user-priority deduplication callback");
            Require(JsonUtility.FromJson<SaveDataSettings>(null) == null && JsonUtility.FromJson<SaveDataSettings>("") == null, "empty JSON keeps original null-result boundary");
            RequireThrows<ArgumentException>(() => JsonUtility.FromJson<SaveDataSettings>("not-json"), "malformed JSON rejects rather than resets data");
        }

        private static SaveDataAnalytics Analytics()
        {
            var analytics = new SaveDataAnalytics { SessionID = "local-session", SessionNumber = 7,
                SessionStartTimestamp = 1234567890123L, SessionSuspendedTimestamp = 1234567890999L,
                CurrentSessionLengthSeconds = 12.5f, InstallDate = "2026-10-05" };
            analytics.SetConsentData("local-account", AnalyticsConsentState.Consented, 1234567890222L);
            // Existing entries accept the supplied timestamp; creation intentionally
            // uses original current UTC time regardless of that optional argument.
            analytics.SetConsentData("local-account", AnalyticsConsentState.Consented, 1234567890222L);
            return analytics;
        }

        private static void VerifyAnalytics()
        {
            SaveDataAnalytics analytics = Analytics();
            Require(analytics.ConsentStates.Count == 0, "consent list remains stale before the serializer callback");
            string json = JsonUtility.ToJson(analytics);
            Require(analytics.ConsentStates.Count == 1 && json.Contains("\"m_consentStatesList\""), "Unity invokes dictionary-to-list before serialization");
            Require(!json.Contains("\"m_consentStates\"") && !json.Contains("m_dirty"), "runtime dictionary and flags are excluded");
            SaveDataAnalytics restored = JsonUtility.FromJson<SaveDataAnalytics>(json);
            Require(restored.SessionID == "local-session" && restored.SessionNumber == 7 && restored.CurrentSessionLengthSeconds == 12.5f, "analytics scalars survive JSON");
            Require(restored.SessionStartTimestamp == 1234567890123L && restored.SessionSuspendedTimestamp == 1234567890999L && restored.InstallDate == "2026-10-05", "analytics timestamp units and install date survive JSON");
            SaveDataAnalyticsConsentState consent = restored.GetOrCreateConsentData("local-account");
            Require(restored.ConsentStates.Count == 1 && ReferenceEquals(consent, restored.ConsentStates[0]), "after-deserialize restores the original dictionary with existing list identities");
            Require(consent.ConsentState == AnalyticsConsentState.Consented && consent.ConsentUpdatedTimestamp == 1234567890222L, "consent enum and Int64 timestamp survive JSON");
            Require(SavingEnabled(restored) && !SavingEnabled(consent), "matching Unity retains root/default-constructor and nested/parameterized-constructor flag distinction");
            var duplicates = JsonUtility.FromJson<SaveDataAnalytics>("{\"m_consentStatesList\":[{\"m_gameCenterAccountID\":\"same\",\"m_consentUpdatedTimestamp\":1},{\"m_gameCenterAccountID\":\"same\",\"m_consentUpdatedTimestamp\":2}]}");
            Require(duplicates.GetOrCreateConsentData("same").ConsentUpdatedTimestamp == 2, "original dictionary callback keeps last duplicate account");
        }

        private static SaveDataNotifications Notifications()
        {
            var notifications = new SaveDataNotifications { NotificationsPermitted = true };
            notifications.TryGetOrNew(NotificationType.LapsedPlayer).RequestedPermission = true;
            notifications.TryGetOrNew(NotificationType.ChallengeRefresh);
            return notifications;
        }

        private static void VerifyNotifications()
        {
            SaveDataNotifications notifications = Notifications();
            string json = JsonUtility.ToJson(notifications);
            Require(json.Contains("\"m_savedNotificationData\"") && !json.Contains("m_dirty"), "original notification list serializes without runtime flags");
            SaveDataNotifications restored = JsonUtility.FromJson<SaveDataNotifications>(json);
            Require(restored.NotificationsPermitted && restored.NotificationSaveData.Count == 2, "notification root fields survive JSON");
            Require(restored.TryGetOrNew(NotificationType.LapsedPlayer).RequestedPermission && !restored.TryGetOrNew(NotificationType.ChallengeRefresh).RequestedPermission, "original notification enum identities and permission flags survive JSON");
            Require(SavingEnabled(restored) && SavingEnabled(restored.NotificationSaveData[0]), "default-constructor notification children retain saving-enabled initialization");
            restored.MarkSaved(); restored.NotificationSaveData[0].RequestedPermission = false;
            Require(restored.HasChangesToSave(), "enabled notification child changes still bubble after JSON restoration");
            Debug.Log("[Project Lucid] JSON notification runtime flags: enabled=" + SavingEnabled(restored) + ", childEnabled=" + SavingEnabled(restored.NotificationSaveData[0]));
        }

        private static void VerifyPropertyStore()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ProjectLucidRecordProof-" + Guid.NewGuid().ToString("N"));
            object[] previous = StoreStatics.Select(name => StoreField(name).GetValue(null)).ToArray();
            try
            {
                SaveDataSettings settings = Settings(); SaveDataAnalytics analytics = Analytics(); SaveDataNotifications notifications = Notifications();
                HLPropertyStore store = NewStore(directory);
                store.LoadFromIdentifier("HL_Default_Save_ID");
                HLPropertyStore.AddSaveHandler(properties =>
                {
                    properties.AddProperty("GameSave_Settings", JsonUtility.ToJson(settings));
                    properties.AddProperty("GameSave_Analytics", JsonUtility.ToJson(analytics));
                    properties.AddProperty("GameSave_Notifications", JsonUtility.ToJson(notifications));
                });
                Require(HLPropertyStore.SaveImmediate(false), "actual record JSON saves through native framing and local atomic adapter");
                string originalJson = store.GetPropertyValue("GameSave_Settings");
                settings.MusicVolume = 0.5f;
                Require(HLPropertyStore.SaveImmediate(false), "second actual record write succeeds");
                string lastJson = store.GetPropertyValue("GameSave_Settings");
                Require(originalJson != lastJson, "changed settings produce a new stored payload");

                store = NewStore(directory); store.LoadFromIdentifier("HL_Default_Save_ID");
                SaveDataSettings loadedSettings = JsonUtility.FromJson<SaveDataSettings>(store.GetPropertyValue("GameSave_Settings"));
                SaveDataAnalytics loadedAnalytics = JsonUtility.FromJson<SaveDataAnalytics>(store.GetPropertyValue("GameSave_Analytics"));
                SaveDataNotifications loadedNotifications = JsonUtility.FromJson<SaveDataNotifications>(store.GetPropertyValue("GameSave_Notifications"));
                Require(store.IsLoaded && loadedSettings.MusicVolume == 0.5f && loadedSettings.EditableUIComponents[0].Scale == 1.75f, "fresh store instance restores settings and nested layout");
                Require(loadedAnalytics.GetOrCreateConsentData("local-account").ConsentUpdatedTimestamp == 1234567890222L && loadedNotifications.NotificationSaveData.Count == 2, "fresh store restores analytics callbacks and notification data");
                Require(store.GetPropertyValue("GameSave_Settings") == lastJson, "storage preserves complete JSON bytes");

                // Both writes retain a last-known-good backup. Damage only this
                // disposable proof's primary file to exercise real recovery.
                string primary = Path.Combine(directory, "PrimaryHL_Default_Save_ID");
                Require(File.Exists(primary), "expected original filename convention is retained");
                File.WriteAllText(primary, "damaged-fixture");
                store = NewStore(directory); store.LoadFromIdentifier("HL_Default_Save_ID");
                Require(store.IsLoaded && JsonUtility.FromJson<SaveDataSettings>(store.GetPropertyValue("GameSave_Settings")).MusicVolume == 0.5f,
                    "damaged primary restores verified DTO payload from backup");
            }
            finally
            {
                for (int i = 0; i < StoreStatics.Length; ++i) StoreField(StoreStatics[i]).SetValue(null, previous[i]);
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static HLPropertyStore NewStore(string directory)
        {
            foreach (string name in StoreStatics) StoreField(name).SetValue(null, null);
            var store = new HLPropertyStore("fixture-only", 0, "Primary");
            typeof(HLPropertyStore).GetField("m_propertyFileStorage", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(store, new ProjectLucid.Offline.LocalPropertySave("fixture-only", "Primary", directory));
            return store;
        }
        private static FieldInfo StoreField(string name) => typeof(HLPropertyStore).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
        private static FieldInfo ItemField(string name) => typeof(SaveDataItem).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static bool SavingEnabled(SaveDataItem item) => (bool)ItemField("m_savingEnabled").GetValue(item);
        private static bool Dirty(SaveDataItem item) => (bool)ItemField("m_dirty").GetValue(item);
        private static void Require(bool result, string label) { ++checks; if (!result) throw new InvalidOperationException(label); }
        private static void RequireThrows<T>(Action action, string label) where T : Exception
        {
            try { action(); } catch (T) { ++checks; return; }
            throw new InvalidOperationException(label);
        }
    }
}
