using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Hardlight;
using Hardlight.Utils;
using HLCloud.Plugin;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Checks preserved original managed behavior using real Unity/package types.
    // Mac native imports, GameObject.Find callbacks and Apple storage are unexecuted.
    public static class OriginalCloudVerification
    {
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        private static void Check(bool value, string description, ref int checks)
        {
            if (!value) throw new InvalidOperationException("Original cloud: " + description);
            ++checks;
        }
        private static void ThrowsExact<T>(Action action, string description, ref int checks) where T : Exception
        {
            try { action(); }
            catch (T exception)
            {
                if (exception.GetType() != typeof(T)) throw;
                ++checks;
                return;
            }
            throw new InvalidOperationException("Original cloud: " + description);
        }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, Own) ?? throw new InvalidOperationException("Missing original field: " + type + "." + name);
        private static T Get<T>(object value, string name) => (T)Field(value.GetType(), name).GetValue(value);
        private static void Set(object value, string name, object data) => Field(value.GetType(), name).SetValue(value, data);
        private static void Receiver(Cloud value, ICloud receiver) => Field(typeof(Cloud), "cloudObject").SetValue(value, receiver);

        public static int RunEditorConstants()
        {
            int checks = 0;
            var editor = new Cloud_Editor();
            var fields = typeof(Cloud_Editor).GetFields(Own);
            Check(fields.Select(f => f.Name).SequenceEqual(new[] { "m_cloudProperties", "m_allCloudKeys" }), "two original ordered fields", ref checks);
            Check(fields.All(f => f.IsPrivate && f.IsInitOnly), "original private readonly collections", ref checks);
            Check(typeof(Cloud_Editor).GetMethods(Own).Length == 13 && typeof(Cloud_Editor).GetConstructors(Own).Length == 1, "complete original editor declaration population", ref checks);
            var properties = Get<Dictionary<string, string>>(editor, "m_cloudProperties");
            var keys = Get<HashSet<string>>(editor, "m_allCloudKeys");
            Check(properties.Count == 0 && keys.Count == 0, "real constructor initializes empty collections", ref checks);
            Check(properties.Comparer.Equals(EqualityComparer<string>.Default) && keys.Comparer.Equals(EqualityComparer<string>.Default), "original default comparers", ref checks);
            Check(editor.Synchronize(), "original synchronization constant true", ref checks);
            Check(editor.FloatForKey(null) == 0f && editor.IntForKey(null) == 0 && editor.BoolForKey(null), "original getters ignore null keys and return constants", ref checks);
            editor.SetFloatForKey(float.NaN, null); editor.SetIntForKey(int.MinValue, null); editor.SetBoolForKey(false, null);
            Check(properties.Count == 0, "original numeric and bool writes ignore null keys", ref checks);
            editor.SetStringForKey("text", "key");
            editor.SetFloatForKey(7f, "key"); editor.SetIntForKey(8, "key"); editor.SetBoolForKey(false, "key");
            Check(editor.StringForKey("key") == "text" && properties.Count == 1, "discarded numeric writes do not overwrite strings", ref checks);
            Check(editor.FloatForKey("key") == 0f && editor.IntForKey("key") == 0 && editor.BoolForKey("key"), "numeric reads do not parse stored text", ref checks);
            editor.InitializeWithGameObjectName(null, true); editor.InitializeWithGameObjectName("unused", false);
            editor.SetSynchronisationCooldown(float.PositiveInfinity);
            Check(editor.StringForKey("key") == "text" && properties.Count == 1, "RET initialization and base cooldown preserve state", ref checks);
            Check(editor.StringForKey("missing") == string.Empty, "missing string is empty", ref checks);
            editor.SetStringForKey(null, "null"); editor.SetStringForKey(string.Empty, "empty");
            Check(editor.StringForKey("null") == null && editor.StringForKey("empty") == string.Empty, "dictionary keeps null and empty string values", ref checks);
            ThrowsExact<ArgumentNullException>(() => editor.StringForKey(null), "null string lookup propagates", ref checks);
            ThrowsExact<ArgumentNullException>(() => editor.SetStringForKey("x", null), "null string assignment propagates", ref checks);
            ThrowsExact<ArgumentNullException>(() => editor.RemoveForKey(null), "null string removal propagates", ref checks);
            return checks;
        }

        public static int RunLiveKeysAndCaptures()
        {
            int checks = 0;
            var editor = new Cloud_Editor();
            editor.SetStringForKey("A", "one"); editor.SetStringForKey("B", "One");
            var original = Get<HashSet<string>>(editor, "m_allCloudKeys");
            HashSet<string> first = editor.RetrieveAllCloudKeys(null);
            Check(ReferenceEquals(first, original) && first.SetEquals(new[] { "one", "One" }), "return original live set with case-sensitive keys", ref checks);
            var list = new List<string>(first);
            first.Add("caller-only");
            Check(first.Contains("caller-only") && editor.StringForKey("caller-only") == string.Empty, "caller set mutation does not publish dictionary value", ref checks);
            editor.SetStringForKey("C", "later");
            Check(!first.Contains("later"), "setter does not eagerly update captured key set", ref checks);
            HashSet<string> second = editor.RetrieveAllCloudKeys("ignored|separator");
            Check(ReferenceEquals(first, second) && first.SetEquals(new[] { "one", "One", "later" }), "retrieval rebuilds the same captured live set and ignores separator", ref checks);
            Check(!first.Contains("caller-only") && list.Count == 2 && list.Contains("one") && list.Contains("One"), "rebuild clears caller mutation while prior list snapshot stays stale", ref checks);
            editor.RemoveForKey("one");
            Check(first.Contains("one") && editor.StringForKey("one") == string.Empty, "removal leaves prior key capture stale", ref checks);
            editor.RetrieveAllCloudKeys(string.Empty);
            Check(!first.Contains("one") && first.SetEquals(new[] { "One", "later" }), "next retrieval updates previous alias", ref checks);
            Check(list.Count == 2 && list.Contains("one"), "copied list retains removed key", ref checks);
            first.Clear();
            Check(editor.StringForKey("One") == "B" && editor.StringForKey("later") == "C", "clearing live key set leaves original values", ref checks);
            Check(ReferenceEquals(first, editor.RetrieveAllCloudKeys(null)) && first.Count == 2, "later retrieval restores keys after caller clear", ref checks);
            editor.RemoveForKey("missing");
            Check(editor.RetrieveAllCloudKeys(null).Count == 2, "missing removal does not mutate values", ref checks);
            return checks;
        }

        public static int RunOriginalNotifications()
        {
            int checks = 0;
            const string message = "{\"NSUbiquitousKeyValueStoreChangedKeysKey\":[\"one\",\"One\",\"one\",\"\"],\"NSUbiquitousKeyValueStoreChangeReasonKey\":3,\"ignored\":42}";
            var receiver = new RecordingReceiver();
            var editor = new Cloud_Editor(); Receiver(editor, receiver);
            editor.SetStringForKey("unchanged", "one");
            editor.CloudDidChange(message);
            Check(receiver.Calls == 1 && receiver.NativeCalls == 0, "editor JSON route calls OnCloudChange only", ref checks);
            Check(receiver.Keys.SequenceEqual(new[] { "one", "One", "one", "" }) && receiver.Reason == ChangeReason.AccountChange, "real UserInfo JSON preserves key order/duplicates and enum reason", ref checks);
            string[] captured = receiver.Keys;
            editor.CloudDidChange("{\"NSUbiquitousKeyValueStoreChangedKeysKey\":[\"next\"],\"NSUbiquitousKeyValueStoreChangeReasonKey\":1}");
            Check(receiver.Calls == 2 && receiver.Reason == ChangeReason.InitialSyncChange && receiver.Keys.SequenceEqual(new[] { "next" }), "subsequent original notification is delivered independently", ref checks);
            Check(!ReferenceEquals(captured, receiver.Keys) && captured.SequenceEqual(new[] { "one", "One", "one", "" }), "prior JSON array capture is not reused or cleared", ref checks);
            editor.CloudDidChange("{}");
            Check(receiver.Calls == 3 && receiver.Keys == null && receiver.Reason == ChangeReason.ServerChange, "original editor forwards missing keys/null and default reason without guard", ref checks);
            Check(editor.StringForKey("one") == "unchanged", "notification does not update in-memory cloud values", ref checks);
            var failure = new InvalidOperationException("receiver fixture"); receiver.Failure = failure;
            Exception caught = null; try { editor.CloudDidChange(message); } catch (Exception exception) { caught = exception; }
            Check(ReferenceEquals(caught, failure) && receiver.Calls == 4, "original receiver exception propagates unchanged", ref checks);
            ThrowsExact<NullReferenceException>(() => new Cloud_Editor().CloudDidChange(message), "original editor does not guard absent receiver", ref checks);
            using (var state = new OriginalState())
            {
                var mac = new Cloud_MacOS(); var macReceiver = new RecordingReceiver(); Receiver(mac, macReceiver);
                mac.CloudDidChange(message);
                Check(macReceiver.Calls == 1 && macReceiver.NativeCalls == 0 && macReceiver.Keys.SequenceEqual(new[] { "one", "One", "one", "" }) && macReceiver.Reason == ChangeReason.AccountChange, "native-free original Mac managed JSON route", ref checks);
                ThrowsExact<NullReferenceException>(() => mac.CloudDidChange("{}"), "original Mac discarded key loop faults for null keys", ref checks);
                Check(macReceiver.Calls == 1, "Mac null-key loop fails before receiver notification", ref checks);
            }
            return checks;
        }

        public static int RunConnectDelegateOrder()
        {
            int checks = 0;
            using (var state = new OriginalState())
            {
                var calls = new List<string>();
                string delivered = "sentinel";
                Action<string> a = text => { delivered = text; calls.Add("A:" + text); }; Action<string> b = text => calls.Add("B:" + text);
                Cloud.SubscribeOnConnect(a); Cloud.SubscribeOnConnect(b); Cloud.SubscribeOnConnect(a);
                Check(calls.Count == 0, "subscription does not replay notification", ref checks);
                var invoke = (Action<IntPtr, IntPtr, IntPtr>)Delegate.CreateDelegate(typeof(Action<IntPtr, IntPtr, IntPtr>), typeof(Cloud_MacOS).GetMethod("OnConnectCallback", Own));
                Cloud_MacOS.UseFindGameObject = false;
                IntPtr data = Marshal.StringToHGlobalAuto("payload");
                try
                {
                    invoke(IntPtr.Zero, IntPtr.Zero, data);
                    Check(calls.SequenceEqual(new[] { "A:payload", "B:payload", "A:payload" }), "data-only original callback preserves combined duplicate order", ref checks);
                    calls.Clear(); Cloud.UnsubscribeOnConnect(a); invoke(IntPtr.Zero, IntPtr.Zero, data);
                    Check(calls.SequenceEqual(new[] { "A:payload", "B:payload" }), "Delegate.Remove removes last matching subscription", ref checks);
                    calls.Clear(); delivered = "sentinel"; invoke(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    Check(calls.SequenceEqual(new[] { "A:", "B:" }) && delivered == null, "zero data pointer is delivered as null to callbacks", ref checks);
                    Cloud.UnsubscribeOnConnect(a); Cloud.UnsubscribeOnConnect(b); Cloud.UnsubscribeOnConnect(a);
                    Check(((Action<string>)Field(typeof(Cloud), "m_onConnect").GetValue(null)).GetInvocationList().Length == 1, "removal retains fixture baseline callback", ref checks);
                    Cloud.SubscribeOnConnect(null); Cloud.UnsubscribeOnConnect(null);
                    Check(((Action<string>)Field(typeof(Cloud), "m_onConnect").GetValue(null)).GetInvocationList().Length == 1, "null combine/remove leave baseline unchanged", ref checks);
                    Field(typeof(Cloud), "m_onConnect").SetValue(null, null);
                    ThrowsExact<NullReferenceException>(() => invoke(IntPtr.Zero, IntPtr.Zero, data), "original callback does not null-guard base delegate", ref checks);
                }
                finally { Marshal.FreeHGlobal(data); }
            }
            return checks;
        }

        public static int RunDefaultOfflineFactory()
        {
            int checks = 0;
            using (var state = new OriginalState())
            {
                var receiver = new RecordingReceiver();
                Cloud_MacOS.UseFindGameObject = true;
                var registry = (IDictionary)Field(typeof(ProcessManager), "s_systemDictionary").GetValue(null);
                Cloud first = Cloud.NativePluginInstance(receiver, false);
                Check(first.GetType() == typeof(ProjectLucid.Offline.LocalCloud), "default original boundary chooses separate offline class", ref checks);
                Check(Cloud_MacOS.UseFindGameObject && registry.Count == 0, "default factory never constructs original Mac dependency or changes its static flag", ref checks);
                Check(ReferenceEquals(Field(typeof(Cloud), "cloudObject").GetValue(first), receiver), "offline selection retains supplied receiver", ref checks);
                Check(((ProjectLucid.Offline.LocalCloud)first).SavePath == Path.GetFullPath(ProjectLucid.Offline.LocalCloud.DefaultSavePath), "default selection uses Project Lucid local path", ref checks);
                Cloud second = Cloud.NativePluginInstance(null, true);
                Check(second.GetType() == typeof(ProjectLucid.Offline.LocalCloud) && !ReferenceEquals(first, second) && Field(typeof(Cloud), "cloudObject").GetValue(second) == null, "factory creates independent offline instances and preserves null receiver", ref checks);
                Check(registry.Count == 0 && receiver.Calls == 0 && receiver.NativeCalls == 0, "factory emits no notification or original registry work", ref checks);
            }
            return checks;
        }

        public static int RunMacCooldownAndWaitIterator()
        {
            int checks = 0;
            using (var state = new OriginalState())
            {
                GameObject item = null;
                try
                {
                    var mac = new Cloud_MacOS();
                    WaitForSeconds wait = Get<WaitForSeconds>(mac, "m_waitForCooldown");
                    FieldInfo duration = Field(typeof(WaitForSeconds), "m_Seconds");
                    Check(wait != null && (float)duration.GetValue(wait) == 0f, "real original constructor retains initially zero WaitForSeconds", ref checks);
                    Check(Get<float>(mac, "m_synchronisationCooldownSeconds") == 0f && !Get<bool>(mac, "m_resynchronisationRequested") && Get<Coroutine>(mac, "m_synchronisationCoroutine") == null, "original cooldown defaults", ref checks);
                    Check(Get<SystemRef<CoroutineUtils>>(mac, "m_coroutineUtils") != null, "genuine managed SystemRef dependency", ref checks);
                    mac.SetSynchronisationCooldown(7f);
                    Check(Get<float>(mac, "m_synchronisationCooldownSeconds") == 7f && ReferenceEquals(wait, Get<WaitForSeconds>(mac, "m_waitForCooldown")) && (float)duration.GetValue(wait) == 0f, "setter updates scalar but does not replace or retime original wait", ref checks);
                    // No Synchronize call is made. Completion has no pending request,
                    // so its original managed branch cannot reach a native import.
                    var complete = (Action)Delegate.CreateDelegate(typeof(Action), mac, typeof(Cloud_MacOS).GetMethod("OnSynchronisationCooldownComplete", Own));
                    complete();
                    Check(!Get<bool>(mac, "m_resynchronisationRequested") && Get<Coroutine>(mac, "m_synchronisationCoroutine") == null, "no-request completion clears original state without resynchronizing", ref checks);
                    item = new GameObject("Original cloud wait iterator fixture"); item.SetActive(false);
                    CoroutineUtils host = item.AddComponent<CoroutineUtils>();
                    MethodInfo factory = typeof(CoroutineUtils).GetMethod("DelayCoroutine", Own, null, new[] { typeof(Action), typeof(WaitForSeconds) }, null);
                    Check(factory != null && factory.ReturnType == typeof(IEnumerator), "genuine supplied-wait iterator overload", ref checks);
                    Func<Action, WaitForSeconds, IEnumerator> create = (action, supplied) => (IEnumerator)factory.Invoke(host, new object[] { action, supplied });
                    int calls = 0; IEnumerator iterator = create(() => ++calls, wait);
                    Check(calls == 0 && iterator.Current == null, "iterator factory defers callback and starts with null Current", ref checks);
                    Check(iterator.MoveNext() && ReferenceEquals(iterator.Current, wait) && calls == 0, "first move yields exact Mac wait object before callback", ref checks);
                    Check(ReferenceEquals(((IEnumerator<object>)iterator).Current, wait), "generic Current preserves exact supplied object", ref checks);
                    ((IDisposable)iterator).Dispose();
                    Check(!iterator.MoveNext() && calls == 1, "original no-op Dispose leaves pending callback", ref checks);
                    Check(!iterator.MoveNext() && calls == 1, "completion invokes callback only once", ref checks);
                    ThrowsExact<NotSupportedException>(() => iterator.Reset(), "original iterator Reset rejects restart", ref checks);
                    var supplied = new WaitForSeconds(3.5f); IEnumerator second = create(() => ++calls, supplied);
                    Check(second.MoveNext() && ReferenceEquals(second.Current, supplied) && (float)duration.GetValue(supplied) == 3.5f && calls == 1, "real nonzero supplied wait object is neither replaced nor retimed", ref checks);
                    Check(!second.MoveNext() && calls == 2, "supplied-wait continuation invokes action after yield", ref checks);
                    var failure = new InvalidOperationException("wait action fixture"); IEnumerator throwing = create(() => { throw failure; }, wait);
                    Check(throwing.MoveNext() && ReferenceEquals(throwing.Current, wait), "throwing callback still yields original wait first", ref checks);
                    Exception caught = null; try { throwing.MoveNext(); } catch (Exception exception) { caught = exception; }
                    Check(ReferenceEquals(caught, failure) && !throwing.MoveNext(), "callback failure propagates with iterator already terminal", ref checks);
                    IEnumerator missing = create(null, wait);
                    Check(missing.MoveNext() && ReferenceEquals(missing.Current, wait), "null action is not rejected before wait yield", ref checks);
                    ThrowsExact<NullReferenceException>(() => missing.MoveNext(), "original action invocation remains unguarded", ref checks);
                    Check(!missing.MoveNext(), "null action failure does not repeat", ref checks);
                }
                finally { if (item != null) UnityEngine.Object.DestroyImmediate(item); }
            }
            return checks;
        }

        private sealed class RecordingReceiver : ICloud
        {
            public int Calls;
            public int NativeCalls;
            public string[] Keys;
            public ChangeReason Reason;
            public Exception Failure;
            public void Native_CloudDidChange(string message) { ++NativeCalls; }
            public void OnCloudChange(string[] keys, ChangeReason reason)
            {
                ++Calls; Keys = keys; Reason = reason;
                if (Failure != null) throw Failure;
            }
        }

        private sealed class OriginalState : IDisposable
        {
            private readonly FieldInfo notification = Field(typeof(Cloud), "m_onConnect");
            private readonly FieldInfo registry = Field(typeof(ProcessManager), "s_systemDictionary");
            private readonly object priorNotification;
            private readonly object priorRegistry;
            private readonly bool priorFind;
            private readonly object priorCoroutineHost;
            public OriginalState()
            {
                priorNotification = notification.GetValue(null);
                priorRegistry = registry.GetValue(null);
                priorFind = Cloud_MacOS.UseFindGameObject;
                priorCoroutineHost = Field(typeof(CoroutineUtils), "s_instance").GetValue(null);
                // A fixture baseline isolates combine/remove and callback tests
                // from subscribers owned by other tests or the running project.
                notification.SetValue(null, new Action<string>(message => { }));
                registry.SetValue(null, Activator.CreateInstance(registry.FieldType));
            }
            public void Dispose()
            {
                try { registry.SetValue(null, priorRegistry); }
                finally
                {
                    try { notification.SetValue(null, priorNotification); }
                    finally
                    {
                        try { Cloud_MacOS.UseFindGameObject = priorFind; }
                        finally { Field(typeof(CoroutineUtils), "s_instance").SetValue(null, priorCoroutineHost); }
                    }
                }
            }
        }
    }
}
