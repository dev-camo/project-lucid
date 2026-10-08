using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Verification
{
    // Original HLNotifications callback 0600000f and direct field getters.
    // Managed wrappers exercise only CLR fields and rebuilt FastAction dispatch:
    // no singleton startup, bridge, platform service or Unity lifecycle is called.
    public static class OriginalNotificationsVerification
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool value, ref int count, string message)
        { if (!value) throw new InvalidOperationException(message); count++; }

        public static int RunManagedCallbacks()
        {
            int count = 0;
            var owner = (HLNotifications)FormatterServices.GetUninitializedObject(typeof(HLNotifications));
            Check(owner.CachedPushToken == null, ref count, "Managed wrapper starts without a token");
            Check(owner.OnReceivedPushToken == null, ref count, "Managed wrapper starts without listeners");
            owner.Native_ReceivedPushToken("alpha");
            Check(owner.CachedPushToken == "alpha", ref count, "Null listener still stores token");
            Check(owner.OnReceivedPushToken == null, ref count, "Null listener remains null");
            owner.Native_ReceivedPushToken(null);
            Check(owner.CachedPushToken == null, ref count, "Null token is retained");
            owner.Native_ReceivedPushToken(string.Empty);
            Check(owner.CachedPushToken == string.Empty, ref count, "Empty token is retained");
            string alias = new string(new[] { 't', 'o', 'k', 'e', 'n' });
            owner.Native_ReceivedPushToken(alias);
            Check(ReferenceEquals(owner.CachedPushToken, alias), ref count, "Token alias is retained");
            typeof(HLNotifications).GetField("m_notificationSoundiOS", Fields).SetValue(owner, alias);
            Check(ReferenceEquals(owner.NotificationSoundiOS, alias), ref count, "Sound getter returns original field alias");

            var trace = new List<string>();
            string expectedNested = "nested";
            FastAction<string> replacement = null;
            replacement += token =>
            {
                trace.Add("replacement:" + token);
                Check(token == expectedNested, ref count, "Fresh callback receives current token argument");
                Check(owner.CachedPushToken == expectedNested, ref count, "Fresh callback observes token already stored");
            };
            FastAction<string> original = null;
            original += token =>
            {
                trace.Add("first:" + token);
                Check(token == "outer", ref count, "First callback receives outer argument");
                Check(owner.CachedPushToken == "outer", ref count, "Store precedes first callback");
                owner.OnReceivedPushToken = replacement;
                owner.Native_ReceivedPushToken("nested");
                Check(owner.CachedPushToken == "nested", ref count, "Outer return does not restore nested token");
            };
            original += token =>
            {
                trace.Add("second:" + token);
                Check(token == "outer", ref count, "Captured old dispatch retains outer argument");
                Check(owner.CachedPushToken == "nested", ref count, "Old later callback sees nested field write");
                Check(ReferenceEquals(owner.OnReceivedPushToken, replacement), ref count, "Callback field replacement survives old dispatch");
            };
            owner.OnReceivedPushToken = original;
            owner.Native_ReceivedPushToken("outer");
            Check(string.Join(",", trace) == "first:outer,replacement:nested,second:outer", ref count, "Nested dispatch uses replacement while outer keeps captured action");
            Check(original.GetInvocationListCount() == 2, ref count, "Original dispatch retains its two listeners");
            Check(replacement.GetInvocationListCount() == 1, ref count, "Replacement dispatch retains its listener");
            Check(ReferenceEquals(owner.OnReceivedPushToken, replacement), ref count, "Outer completion retains replacement action");
            expectedNested = "next";
            owner.Native_ReceivedPushToken("next");
            Check(owner.CachedPushToken == "next", ref count, "Next call stores its own token");

            var fault = new InvalidOperationException("listener sentinel");
            string observedBeforeFault = null;
            FastAction<string> failing = null;
            failing += token => { observedBeforeFault = owner.CachedPushToken; trace.Add("fault:" + token); throw fault; };
            failing += token => { trace.Add("unreachable:" + token); };
            owner.OnReceivedPushToken = failing;
            Exception caught = null;
            try { owner.Native_ReceivedPushToken("fault-token"); }
            catch (Exception error) { caught = error; }
            Check(ReferenceEquals(caught, fault), ref count, "Original listener exception propagates without wrapping");
            Check(owner.CachedPushToken == "fault-token", ref count, "Listener fault retains stored token");
            Check(observedBeforeFault == "fault-token", ref count, "Faulting listener observes new token");
            Check(string.Join(",", trace) == "first:outer,replacement:nested,second:outer,replacement:next,fault:fault-token", ref count, "Fault prevents later listener");
            Check(ReferenceEquals(owner.OnReceivedPushToken, failing), ref count, "Fault leaves callback reference intact");
            owner.OnReceivedPushToken = null;
            owner.Native_ReceivedPushToken("after-fault");
            Check(owner.CachedPushToken == "after-fault", ref count, "Fresh null action permits a later token write");
            Check(owner.OnReceivedPushToken == null, ref count, "Later null action remains null");
            bool nullOwnerFault = false;
            try { ((HLNotifications)null).Native_ReceivedPushToken("unused"); }
            catch (NullReferenceException) { nullOwnerFault = true; }
            Check(nullOwnerFault, ref count, "Null owner faults at its field store");
            return count;
        }

        // These checks require a real Editor-owned ScriptableObject. They exercise
        // authored constructor defaults and direct getters, excluding Validate's
        // filesystem/logging route and all platform configuration export.
        public static int RunConfigurationEngine()
        {
            int count = 0;
            HLNotificationsConfiguration config = ScriptableObject.CreateInstance<HLNotificationsConfiguration>();
            try
            {
                Check(config.PathToGradleTemplateFile == string.Empty, ref count, "Original template override default");
                Check(config.PathToGradleFile == string.Empty, ref count, "Original output override default");
                Check(config.DefaultGradleTemplatePath == "/NativeAndroid~/build.2019gradle", ref count, "Notifications retains original 2019 template");
                Check(((IGradleFileConfig)config).DefaultGradlePath == "/NativeAndroid~/build.gradle", ref count, "Genuine inherited output default");
                string template = new string(new[] { 't', 'e', 'm', 'p' });
                string output = new string(new[] { 'o', 'u', 't' });
                FieldInfo templateField = typeof(HLNotificationsConfiguration).GetField("m_gradleTemplateFileOverride", Fields);
                FieldInfo outputField = typeof(HLNotificationsConfiguration).GetField("m_gradleFileOverride", Fields);
                templateField.SetValue(config, template);
                outputField.SetValue(config, output);
                Check(ReferenceEquals(config.PathToGradleTemplateFile, template), ref count, "Template alias retained");
                Check(ReferenceEquals(config.PathToGradleFile, output), ref count, "Output alias retained");
                Check(config.DefaultGradleTemplatePath == "/NativeAndroid~/build.2019gradle", ref count, "Overrides do not replace template default");
                Check(((IGradleFileConfig)config).DefaultGradlePath == "/NativeAndroid~/build.gradle", ref count, "Overrides do not replace inherited output default");
                templateField.SetValue(config, null);
                outputField.SetValue(config, null);
                Check(config.PathToGradleTemplateFile == null, ref count, "Null template is not normalized");
                Check(config.PathToGradleFile == null, ref count, "Null output is not normalized");
                return count;
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }
    }
}
