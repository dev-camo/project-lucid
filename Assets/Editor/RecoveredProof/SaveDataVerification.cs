// Native-derived Game.Runtime SaveData checks against maintained implementations.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HardlightProject;
using Hardlight.Analytics;
using UnityEngine;

namespace ProjectLucid
{
    public static class SaveDataVerification
    {
        static int checks;
        static void Check(bool value, string label)
        {
            ++checks;
            if (!value)
                throw new Exception(label);
        }

        static void Throws<T>(Action action, string label)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                ++checks;
                return;
            }

            throw new Exception(label);
        }

        static FieldInfo Field(object o, string n)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null)
                    return f;
            }

            throw new Exception(n);
        }

        static T Get<T>(object o, string n) => (T)Field(o, n).GetValue(o);
        static void Set(object o, string n, object v) => Field(o, n).SetValue(o, v);
        sealed class Node : SaveDataItem
        {
            public List<SaveDataItem> Children = new List<SaveDataItem>();
            public int Visits, Inits;
            public Action Before, After;
            protected override void IterateChildren(Action<SaveDataItem> action)
            {
                ++Visits;
                foreach (var child in Children)
                {
                    Before?.Invoke();
                    action(child);
                    After?.Invoke();
                }
            }

            public override void Initialise()
            {
                ++Inits;
                base.Initialise();
            }

            public static void ToDict<TK, TV>(List<TV> l, Dictionary<TK, TV> d, Func<TV, TK> k) => ListToDictionary(l, d, k);
            public static void ToList<TK, TV>(Dictionary<TK, TV> d, ref List<TV> l) => DictionaryToList(d, ref l);
        }

        static void Base()
        {
            var a = new Node();
            Check(Get<bool>(a, "m_savingEnabled"), "base enabled");
            Check(!Get<bool>(a, "m_dirty"), "base clean");
            Check(!a.HasChangesToSave(), "clean");
            Check(a.Visits == 1, "clean traversed");
            a.MarkDirty();
            Check(a.HasChangesToSave(), "dirty");
            Check(a.Visits == 1, "dirty skips");
            var b = new Node();
            var c = new Node();
            a.Children.Add(b);
            a.Children.Add(c);
            a.MarkSaved();
            Check(!a.HasChangesToSave(), "saved");
            b.MarkDirty();
            c.MarkDirty();
            int bv = b.Visits, cv = c.Visits;
            Check(a.HasChangesToSave(), "child dirty");
            Check(Get<bool>(a, "m_dirty"), "parent caches");
            Check(b.Visits == bv && c.Visits == cv, "each dirty fast path");
            a.MarkSaved();
            Check(!b.HasChangesToSave() && !c.HasChangesToSave(), "children cleared");
            a.Initialise();
            Check(b.Inits == 1 && c.Inits == 1, "init children");
            a.MarkDirty();
            b.MarkDirty();
            a.DisableSaving();
            a.MarkSaved();
            Check(!a.HasChangesToSave(), "disabled false");
            Check(Get<bool>(a, "m_dirty") && b.HasChangesToSave(), "disabled retained");
            var nullNode = new Node();
            nullNode.Children.Add(null);
            Throws<NullReferenceException>(() => nullNode.MarkSaved(), "null mark");
            Throws<NullReferenceException>(() => nullNode.Initialise(), "null init");
            Throws<NullReferenceException>(() => nullNode.HasChangesToSave(), "null changes");
            var p = new Node();
            p.Children.Add(new Node());
            p.After = p.MarkDirty;
            Check(!p.HasChangesToSave() && !Get<bool>(p, "m_dirty"), "cache overwrites callback dirty");
            p.MarkDirty();
            p.Before = () => Check(!Get<bool>(p, "m_dirty"), "saved before callbacks");
            p.After = null;
            p.MarkSaved();
            var mut = new Node();
            mut.Children.Add(new Node());
            mut.After = () => mut.Children.Add(new Node());
            Throws<InvalidOperationException>(() => mut.MarkSaved(), "live list mutation");
            var d = new Dictionary<string, string>
            {
                {
                    "old",
                    "old"
                }
            };
            Node.ToDict(new List<string> { "a", "a", "b" }, d, x => x);
            Check(d.Count == 2 && !d.ContainsKey("old"), "dict rebuild");
            Throws<InvalidOperationException>(() =>
            {
                var l = new List<string>
                {
                    "a"
                };
                Node.ToDict(l, d, x =>
                {
                    l.Add("b");
                    return x;
                });
            }, "key callback mutates");
            Check(d.Count == 1 && d["a"] == "a", "partial dictionary");
            Throws<NullReferenceException>(() => Node.ToDict<string, string>(null, d, x => x), "null list");
            Check(d.Count == 0, "clear before null list");
            List<string> result = null;
            Node.ToList(new Dictionary<string, string> { { "a", "v1" }, { "b", "v2" } }, ref result);
            Check(result.SequenceEqual(new[] { "v1", "v2" }), "dict values order");
            var saved = result;
            Node.ToList(new Dictionary<string, string> { { "c", null } }, ref result);
            Check(ReferenceEquals(saved, result) && result.Count == 1 && result[0] == null, "reuse and null value");
        }

        static void Settings()
        {
            var s = new SaveDataSettings();
            Check(s.HasChangesToSave(), "settings start dirty");
            Check(s.MasterVolume == 1 && s.MusicVolume == 1 && s.SfxVolume == 1 && s.VoiceVolume == 1, "audio defaults");
            Check(s.CameraRecenterHeadingType == CameraRecenterHeadingType.Medium && !s.CameraInvertedControls, "recenter defaults");
            Check(s.CameraConfiguration == CameraConfigurationType.Default && s.CameraSensitivity == CameraSensitivityType.Medium, "camera defaults");
            Check(s.CameraSecondarySensitivity == CameraSensitivityType.Medium && s.ControlStickSensitivity == InputModifierType.Medium, "control sensitivity defaults");
            Check(!s.CameraSnapBehindCharacterButtonVisible && !s.DynamicMovementStick && !s.OnScreenControlsFlipped && s.AirAbilityHold, "control flags");
            Check(s.HomingAttackMapping == ButtonMappingType.Secondary && s.AirStompAttackMapping == ButtonMappingType.Secondary, "mapping defaults");
            Check(s.GameSpeed == 1 && s.FailState == AccessibilityFailState.Default && !s.GameSpeedToggleEnabled && s.SubtitlesEnabled, "accessibility");
            Check(s.RenderScale == -1 && s.PerformanceProfileName == "" && s.LastWhatsNewVersion == "" && !s.SkipFTUE, "quality/version defaults");
            Check(Get<SaveDataDebug>(s, "m_debug") == null && s.EditableUIComponents.Count == 0, "lazy/default list");
            s.MarkSaved();
            s.MusicVolume = s.MusicVolume;
            Check(s.HasChangesToSave(), "settings equal setter dirty");
            s.MarkSaved();
            s.SetAudioDefaults(false);
            Check(s.HasChangesToSave(), "false audio dirty");
            foreach (Action reset in new Action[]
            {
                () => s.SetControlsDefaults(false),
                () => s.SetAccessibilityDefaults(false),
                () => s.SetQualityDefaults(false),
                () => s.SetCameraDefaults(false)
            }

            )
            {
                s.MarkSaved();
                reset();
                Check(!s.HasChangesToSave(), "false nonaudio default clean");
            }

            int hits = 0;
            Action<CameraConfigurationType> cb = v =>
            {
                ++hits;
                Check(s.CameraConfiguration == v, "callback field assigned");
            };
            s.InvokeOnConfigurationChanged(cb);
            Check(hits == 1 && !s.HasChangesToSave(), "register immediate no dirty");
            s.CameraConfiguration = s.CameraConfiguration;
            Check(hits == 2 && s.HasChangesToSave(), "same callback setter");
            s.RemoveConfigurationChangedAction(cb);
            s.MarkSaved();
            s.CameraConfiguration = CameraConfigurationType.Far;
            Check(hits == 2, "removed");
            s.MarkSaved();
            Action<CameraConfigurationType> fail = v => throw new InvalidOperationException();
            Throws<InvalidOperationException>(() => s.InvokeOnConfigurationChanged(fail), "subscribe retained on throw");
            Throws<InvalidOperationException>(() => s.CameraConfiguration = CameraConfigurationType.Near, "setter callback throws");
            Check(s.CameraConfiguration == CameraConfigurationType.Near && !Get<bool>(s, "m_dirty"), "setter partial before dirty");
            s.RemoveConfigurationChangedAction(fail);
            Throws<NullReferenceException>(() => s.InvokeOnConfigurationChanged(null), "null callback direct invoke");
            s.MarkSaved();
            var debug = s.Debug;
            Check(debug.GameCenterDebugIsUnderage && debug.HasChangesToSave() && s.HasChangesToSave(), "debug lazy defaults");
            s.MarkSaved();
            Check(debug.HasChangesToSave() && !s.HasChangesToSave(), "debug omitted from children");
            var ui = s.GetOrCreateEditableUIComponentData("Stick");
            Check(ui.ID == "Stick" && ui.Position.x == 0 && ui.Position.y == 0 && ui.Scale == 0 && !ui.UserSet, "ui defaults");
            Check(ReferenceEquals(ui, s.GetOrCreateEditableUIComponentData("sTiCk")), "id ignorecase lookup");
            s.MarkSaved();
            ui.Scale = 2;
            Check(s.HasChangesToSave(), "ui child dirty");
            s.MarkSaved();
            ui.Position = new Vector2(float.NaN, 0);
            s.MarkSaved();
            ui.Position = ui.Position;
            Check(ui.HasChangesToSave(), "nan vector compares unequal");
            var a = new SaveDataSettingsEditableUIComponent("id");
            var b = new SaveDataSettingsEditableUIComponent("ID")
            {
                UserSet = true,
                Scale = 2
            };
            var c = new SaveDataSettingsEditableUIComponent("Id")
            {
                Scale = 3
            };
            s.EditableUIComponents.Clear();
            s.EditableUIComponents.AddRange(new[] { a, null, new SaveDataSettingsEditableUIComponent("  "), b, c });
            s.MarkDirty();
            s.OnAfterDeserialize();
            Check(s.EditableUIComponents.Count == 1 && ReferenceEquals(s.EditableUIComponents[0], b), "dedupe user winner");
            Check(b.ID == "ID", "id not trimmed/case rewritten");
            Set(s, "m_editableUIComponents", null);
            Throws<NullReferenceException>(() => s.OnAfterDeserialize(), "settings callback no repair");
            s.Initialise();
            Check(s.EditableUIComponents.Count == 0, "settings init repair");
            var old = new SaveDataSettings();
            var newer = new SaveDataSettings();
            old.MusicVolume = float.NaN;
            newer.MusicVolume = .25f;
            old.VoiceVolume = .75f;
            newer.VoiceVolume = float.NaN;
            old.DynamicMovementStick = true;
            newer.DynamicMovementStick = false;
            old.LastWhatsNewVersion = "old";
            newer.LastWhatsNewVersion = "new";
            old.SkipFTUE = true;
            newer.SkipFTUE = false;
            var oldDebug = old.Debug;
            old.MarkSaved();
            old.ResolveNewData(newer);
            Check(old.MusicVolume == .25f && float.IsNaN(old.VoiceVolume), "native minimum NaN operand direction");
            Check(old.DynamicMovementStick && old.LastWhatsNewVersion == "old" && old.SkipFTUE, "merge OR and omitted fields");
            Check(!ReferenceEquals(Get<SaveDataDebug>(old, "m_debug"), oldDebug) && !old.HasChangesToSave(), "merge resets debug without dirty parent");
            old.CameraSecondarySensitivity = CameraSensitivityType.High;
            old.AirStompAttackMapping = ButtonMappingType.Primary;
            old.SkipFTUE = true;
            old.Debug = oldDebug;
            old.CopyFrom(newer);
            Check(old.CameraSecondarySensitivity == CameraSensitivityType.High && old.AirStompAttackMapping == ButtonMappingType.Primary && old.SkipFTUE && ReferenceEquals(old.Debug, oldDebug), "copy omitted fields");
            newer.EditableUIComponents.Add(new SaveDataSettingsEditableUIComponent("new") { Scale = 3 });
            old.CopyFrom(newer);
            Check(old.EditableUIComponents.Count == 1 && old.EditableUIComponents[0].Scale == 3 && !ReferenceEquals(old.EditableUIComponents[0], newer.EditableUIComponents[0]), "copy clones ui");
            old.MarkSaved();
            old.CopyFrom(old);
            Check(!old.HasChangesToSave(), "self copy no mutation");
            var nullId = new SaveDataSettings();
            nullId.GetOrCreateEditableUIComponentData(null);
            Throws<NullReferenceException>(() => nullId.GetOrCreateEditableUIComponentData(null), "lookup null ID instance Equals");
        }

        static void Analytics()
        {
            var a = new SaveDataAnalytics();
            Check(!a.HasChangesToSave() && a.SessionID == null && a.InstallDate == null && a.SessionNumber == 0, "analytics defaults");
            Check(a.SessionStartTimestamp == 0 && a.SessionSuspendedTimestamp == 0 && a.CurrentSessionLengthSeconds == 0 && a.ConsentStates.Count == 0, "analytics scalar/list defaults");
            a.SessionNumber = 0;
            Check(!a.HasChangesToSave(), "equal scalar no dirty");
            a.SessionID = "id";
            a.MarkSaved();
            a.SessionID = new string (new[] { 'i', 'd' });
            Check(!a.HasChangesToSave(), "string value comparison");
            a.CurrentSessionLengthSeconds = float.NaN;
            a.MarkSaved();
            a.CurrentSessionLengthSeconds = float.NaN;
            Check(a.HasChangesToSave(), "nan analytics dirties");
            a.MarkSaved();
            long beforeConsent = Hardlight.TimeUtils.ToUnixTimeMs(DateTime.UtcNow);
            var c = a.GetOrCreateConsentData("one");
            long afterConsent = Hardlight.TimeUtils.ToUnixTimeMs(DateTime.UtcNow);
            Check(c.ConsentState == AnalyticsConsentState.None && !c.HasChangesToSave() && a.HasChangesToSave(), "new consent defaults");
            Check(c.ConsentUpdatedTimestamp >= beforeConsent && c.ConsentUpdatedTimestamp <= afterConsent, "consent timestamp uses UTC milliseconds");
            a.MarkSaved();
            c.ConsentState = AnalyticsConsentState.Consented;
            Check(c.HasChangesToSave() && !a.HasChangesToSave(), "consent omitted child");
            a.OnBeforeSerialize();
            Check(a.ConsentStates.Count == 1 && ReferenceEquals(a.ConsentStates[0], c), "dictionary to list");
            beforeConsent = Hardlight.TimeUtils.ToUnixTimeMs(DateTime.UtcNow);
            a.SetConsentData("two", AnalyticsConsentState.Rejected, 123L);
            afterConsent = Hardlight.TimeUtils.ToUnixTimeMs(DateTime.UtcNow);
            a.OnBeforeSerialize();
            long createdTimestamp = a.ConsentStates.Single(x => x.GameCenterAccountID == "two").ConsentUpdatedTimestamp;
            Check(createdTimestamp >= beforeConsent && createdTimestamp <= afterConsent, "new entry ignores provided timestamp and uses UTC milliseconds");
            a.SetConsentData("two", AnalyticsConsentState.Rejected, 123L);
            a.OnBeforeSerialize();
            Check(a.ConsentStates.Single(x => x.GameCenterAccountID == "two").ConsentUpdatedTimestamp == 123L, "existing entry uses positive timestamp");
            var first = new SaveDataAnalyticsConsentState("same", AnalyticsConsentState.Consented);
            var last = new SaveDataAnalyticsConsentState("same", AnalyticsConsentState.Rejected);
            Set(a, "m_consentStatesList", new List<SaveDataAnalyticsConsentState> { first, last });
            a.OnAfterDeserialize();
            Check(ReferenceEquals(a.GetOrCreateConsentData("same"), last), "last consent duplicate wins");
            Set(a, "m_consentStatesList", null);
            Throws<NullReferenceException>(() => a.OnAfterDeserialize(), "null consent list not repaired");
            Check(Get<Dictionary<string, SaveDataAnalyticsConsentState>>(a, "m_consentStates").Count == 0, "dict cleared before null list");
            a.OnBeforeSerialize();
            Check(a.ConsentStates.Count == 0, "before serialize list repair");
            var p = new SaveDataAnalyticsConsentState("id", AnalyticsConsentState.Consented);
            var q = new SaveDataAnalyticsConsentState("id", AnalyticsConsentState.Rejected);
            p.ConsentUpdatedTimestamp = 1;
            q.ConsentUpdatedTimestamp = 2;
            p.MarkSaved();
            p.ResolveNewData(q);
            Check(p.ConsentState == AnalyticsConsentState.Rejected && p.ConsentUpdatedTimestamp == 2 && !p.HasChangesToSave(), "consent newer merge direct clean");
            p.ConsentState = AnalyticsConsentState.Consented;
            p.ConsentUpdatedTimestamp = 0;
            q.ConsentUpdatedTimestamp = 0;
            p.MarkSaved();
            p.ResolveNewData(q);
            Check(p.ConsentState == AnalyticsConsentState.None && !p.HasChangesToSave(), "zero-time conflict none");
            p.ConsentState = AnalyticsConsentState.Consented;
            p.ConsentUpdatedTimestamp = 3;
            q.ConsentUpdatedTimestamp = 3;
            p.ResolveNewData(q);
            Check(p.ConsentState == AnalyticsConsentState.Consented, "equal positive preserve");
            q.GameCenterAccountID = "different";
            q.ConsentUpdatedTimestamp = 99;
            p.ResolveNewData(q);
            Check(p.ConsentUpdatedTimestamp == 3, "id mismatch ignored");
            var lhs = new SaveDataAnalytics();
            var rhs = new SaveDataAnalytics();
            lhs.SessionNumber = 3;
            rhs.SessionNumber = 9;
            var incoming = rhs.GetOrCreateConsentData("incoming");
            lhs.MarkSaved();
            lhs.ResolveNewData(rhs);
            Check(lhs.SessionNumber == 3 && ReferenceEquals(lhs.GetOrCreateConsentData("incoming"), incoming) && !lhs.HasChangesToSave(), "analytics merge reference and no session copy/dirty");
        }

        static void Notifications()
        {
            var n = new SaveDataNotifications();
            Check(!n.NotificationsPermitted && n.NotificationSaveData.Count == 0 && !n.HasChangesToSave(), "notification defaults");
            n.NotificationsPermitted = false;
            Check(!n.HasChangesToSave(), "same permit clean");
            var i = n.TryGetOrNew(NotificationType.LapsedPlayer);
            Check(i.Notification == NotificationType.LapsedPlayer && !i.RequestedPermission && i.HasChangesToSave() && n.HasChangesToSave(), "new notification dirty");
            Check(ReferenceEquals(i, n.TryGetOrNew(NotificationType.LapsedPlayer)), "notification lookup first");
            n.MarkSaved();
            Check(!i.HasChangesToSave() && !n.HasChangesToSave(), "notification child traversal");
            i.RequestedPermission = true;
            Check(n.HasChangesToSave(), "notification child bubbles");
            Set(n, "m_savedNotificationData", null);
            Throws<NullReferenceException>(() => n.OnAfterDeserialize(), "notification deserialize no null repair");
            var lhs = new SaveDataNotifications();
            var rhs = new SaveDataNotifications();
            var own = lhs.TryGetOrNew(NotificationType.LapsedPlayer);
            rhs.TryGetOrNew(NotificationType.LapsedPlayer).RequestedPermission = true;
            var added = rhs.TryGetOrNew(NotificationType.ChallengeRefresh);
            rhs.NotificationsPermitted = true;
            lhs.MarkSaved();
            rhs.MarkSaved();
            lhs.ResolveNewData(rhs);
            Check(own.RequestedPermission && lhs.NotificationsPermitted && ReferenceEquals(lhs.NotificationSaveData[1], added), "notification merge copies flag/appends ref");
            Check(!lhs.HasChangesToSave(), "merge no dirties");
            var dupe = new SaveDataNotificationItem
            {
                Notification = NotificationType.LapsedPlayer,
                RequestedPermission = false
            };
            rhs.NotificationSaveData.Add(dupe);
            lhs.ResolveNewData(rhs);
            Check(!own.RequestedPermission, "all matching incoming entries last flag wins");
            var debug = new SaveDataDebug();
            Check(!debug.UnlockLevels && !debug.UnlockDreamPowers && !debug.GameCenterDebugIsLoggedIn && debug.GameCenterDebugIsUnderage && debug.GameCenterDebugAccountIDIndex == 0 && debug.HasChangesToSave(), "debug constructor exact");
            debug.MarkSaved();
            debug.UnlockLevels = false;
            Check(debug.HasChangesToSave(), "debug same setter dirty");
        }

        static void Schema()
        {
            var serialTypes = new[]
            {
                typeof(SaveDataSettings),
                typeof(SaveDataAnalytics),
                typeof(SaveDataAnalyticsConsentState),
                typeof(SaveDataNotifications),
                typeof(SaveDataNotificationItem),
                typeof(SaveDataDebug),
                typeof(SaveDataSettingsEditableUIComponent)
            };
            foreach (Type t in serialTypes)
                Check(t.IsDefined(typeof(SerializableAttribute), false), t.Name + " serializable");
            Check(!typeof(SaveDataItem).IsDefined(typeof(SerializableAttribute), false), "base nonserializable");
            var types = serialTypes.Concat(new[] { typeof(SaveDataItem), typeof(SaveDataResolvableItem<>) });
            foreach (Type t in types)
                Check(t.GetCustomAttributes<Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute>().Count() == 2, t.Name + " performance attrs");
            Check(typeof(SaveDataSettings).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Count(f => f.IsDefined(typeof(SerializeField), false)) == 26, "settings serial fields");
            Check(typeof(SaveDataAnalytics).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Count(f => f.IsDefined(typeof(SerializeField), false)) == 7, "analytics serial fields");
            foreach (string name in new[]
            {
                "m_dirty",
                "m_savingEnabled"
            }

            )
                Check(!typeof(SaveDataItem).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).IsDefined(typeof(SerializeField), false), "base flags private not serialized");
        }

        static void ExactSchema()
        {
            FieldSchema(typeof(HardlightProject.SaveDataAnalytics), new[] { "m_lastKnownSessionID", "m_lastKnownSessionNumber", "m_sessionStartTimestamp", "m_sessionSuspendedTimestamp", "m_currentSessionLengthSeconds", "m_installDate", "m_consentStatesList", "m_consentStates" }, new[] { "m_lastKnownSessionID", "m_lastKnownSessionNumber", "m_sessionStartTimestamp", "m_sessionSuspendedTimestamp", "m_currentSessionLengthSeconds", "m_installDate", "m_consentStatesList" });
            OptionSchema(typeof(HardlightProject.SaveDataAnalytics), new[] { 2, 1 });
            FieldSchema(typeof(HardlightProject.SaveDataAnalyticsConsentState), new[] { "m_gameCenterAccountID", "m_consentState", "m_consentUpdatedTimestamp" }, new[] { "m_gameCenterAccountID", "m_consentState", "m_consentUpdatedTimestamp" });
            OptionSchema(typeof(HardlightProject.SaveDataAnalyticsConsentState), new[] { 1, 2 });
            FieldSchema(typeof(HardlightProject.SaveDataDebug), new[] { "m_unlockLevels", "m_unlockDreamPowers", "m_gameCenterDebugIsLoggedIn", "m_gameCenterDebugIsUnderage", "m_gameCenterDebugAccountIDIndex" }, new[] { "m_unlockLevels", "m_unlockDreamPowers", "m_gameCenterDebugIsLoggedIn", "m_gameCenterDebugIsUnderage", "m_gameCenterDebugAccountIDIndex" });
            OptionSchema(typeof(HardlightProject.SaveDataDebug), new[] { 1, 2 });
            FieldSchema(typeof(HardlightProject.SaveDataItem), new[] { "m_dirty", "m_savingEnabled" }, new string[0]);
            OptionSchema(typeof(HardlightProject.SaveDataItem), new[] { 2, 1 });
            FieldSchema(typeof(HardlightProject.SaveDataNotificationItem), new[] { "m_notification", "m_requestedPermission" }, new[] { "m_notification", "m_requestedPermission" });
            OptionSchema(typeof(HardlightProject.SaveDataNotificationItem), new[] { 1, 2 });
            FieldSchema(typeof(HardlightProject.SaveDataNotifications), new[] { "m_notificationsPermitted", "m_savedNotificationData" }, new[] { "m_notificationsPermitted", "m_savedNotificationData" });
            OptionSchema(typeof(HardlightProject.SaveDataNotifications), new[] { 2, 1 });
            FieldSchema(typeof(HardlightProject.SaveDataResolvableItem<>), new string[0], new string[0]);
            OptionSchema(typeof(HardlightProject.SaveDataResolvableItem<>), new[] { 1, 2 });
            FieldSchema(typeof(HardlightProject.SaveDataSettings), new[] { "m_masterVolume", "m_musicVolume", "m_sfxVolume", "m_voiceVolume", "m_cameraRecenterHeadingType", "m_cameraInvertedControls", "m_cameraConfiguration", "m_cameraSensitivity", "m_cameraSnapBehindCharacterButtonVisible", "m_dynamicMovementStick", "m_controlStickSensitivity", "m_cameraSecondarySensitivity", "m_airAbilityHold", "m_editableUIComponents", "m_onScreenControlsFlipped", "m_homingAttackMapping", "m_airStompAttackMapping", "m_gameSpeed", "m_failState", "m_gameSpeedToggleEnabled", "m_subtitlesEnabled", "m_debug", "m_performanceProfileName", "m_renderScale", "m_lastWhatsNewVersion", "m_skipFTUE", "m_onCameraConfigurationChanged", "m_onCameraSensitivityChanged", "m_onCameraSecondarySensitivityChanged", "EditableUIComponentsIDComparison", "s_editableUIComponentsTempDictionary" }, new[] { "m_masterVolume", "m_musicVolume", "m_sfxVolume", "m_voiceVolume", "m_cameraRecenterHeadingType", "m_cameraInvertedControls", "m_cameraConfiguration", "m_cameraSensitivity", "m_cameraSnapBehindCharacterButtonVisible", "m_dynamicMovementStick", "m_controlStickSensitivity", "m_cameraSecondarySensitivity", "m_airAbilityHold", "m_editableUIComponents", "m_onScreenControlsFlipped", "m_homingAttackMapping", "m_airStompAttackMapping", "m_gameSpeed", "m_failState", "m_gameSpeedToggleEnabled", "m_subtitlesEnabled", "m_debug", "m_performanceProfileName", "m_renderScale", "m_lastWhatsNewVersion", "m_skipFTUE" });
            OptionSchema(typeof(HardlightProject.SaveDataSettings), new[] { 1, 2 });
            FieldSchema(typeof(HardlightProject.SaveDataSettingsEditableUIComponent), new[] { "m_id", "m_position", "m_scale", "m_userSet" }, new[] { "m_id", "m_position", "m_scale", "m_userSet" });
            OptionSchema(typeof(HardlightProject.SaveDataSettingsEditableUIComponent), new[] { 1, 2 });
            EnumSchema(typeof(Hardlight.Analytics.AnalyticsConsentState), new[] { "None", "Consented", "Rejected", "Underage" }, new[] { 0, -324521923, -1147781194, 1042743376 });
            EnumSchema(typeof(HardlightProject.AccessibilityFailState), new[] { "Default", "Invincible", "NoFail" }, new[] { 602145082, -819389673, 809165625 });
            EnumSchema(typeof(HardlightProject.ButtonMappingType), new[] { "None", "All", "Primary", "Secondary" }, new[] { 0, 1379978408, -671093917, -1603365627 });
            EnumSchema(typeof(HardlightProject.CameraConfigurationType), new[] { "None", "Default", "Far", "Near" }, new[] { 0, 602145082, 414095619, -1088024998 });
            EnumSchema(typeof(HardlightProject.CameraRecenterHeadingType), new[] { "None", "Medium" }, new[] { 0, -915112498 });
            EnumSchema(typeof(HardlightProject.CameraSensitivityType), new[] { "None", "High", "Low", "Medium", "VeryHigh", "VeryLow" }, new[] { 0, 946861598, -70203948, -915112498, -1925819726, 1924880489 });
            EnumSchema(typeof(HardlightProject.InputModifierType), new[] { "High", "Low", "Medium" }, new[] { 946861598, -70203948, -915112498 });
            EnumSchema(typeof(HardlightProject.NotificationType), new[] { "ChallengeRefresh", "LapsedPlayer" }, new[] { -517856104, 1549041558 });
        }

        static void FieldSchema(Type type, string[] names, string[] serialized)
        {
            Check(type.Assembly.GetName().Name == "Game.Runtime", type.Name + " original assembly");
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
            Check(fields.Select(f => f.Name).SequenceEqual(names), type.Name + " original field order");
            foreach (var field in fields)
                Check(field.IsDefined(typeof(SerializeField), false) == serialized.Contains(field.Name), type.Name + "." + field.Name + " original SerializeField");
        }

        static void OptionSchema(Type type, int[] options)
        {
            var attributes = type.GetCustomAttributes<Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute>().ToArray();
            Check(attributes.Select(a => (int)a.Option).SequenceEqual(options), type.Name + " original option order");
            Check(attributes.All(a => a.Value is bool && !(bool)a.Value), type.Name + " disabled native checks");
        }

        static void EnumSchema(Type type, string[] names, int[] values)
        {
            Check(type.Assembly.GetName().Name == "HLAutoGenerated", type.Name + " original enum assembly");
            Check(Enum.GetUnderlyingType(type) == typeof(int), type.Name + " Int32 enum");
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
            Check(fields.Select(f => f.Name).SequenceEqual(names), type.Name + " enum declaration order");
            Check(fields.Select(f => (int)f.GetRawConstantValue()).SequenceEqual(values), type.Name + " original numeric values");
        }

        public static void Run()
        {
            checks = 0;
            Base();
            Settings();
            Analytics();
            Notifications();
            Schema();
            ExactSchema();
            UnityEngine.Debug.Log("Original SaveData foundation verification passed: " + checks + " checks; full save-slot integration remains unverified.");
        }
    }
}
