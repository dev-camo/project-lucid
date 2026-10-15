using System;
using System.Reflection;
using Hardlight.Analytics;
using Hardlight.JSON;
using Hardlight.Pooling;
using NUnit.Framework;
using ProjectLucid.Offline;
using UnityEngine;

namespace ProjectLucid.Editor.Tests
{
    // These exercise the genuine original facade, event contract, and owned
    // configuration assets. The original AnalyticsSystem is never constructed
    // or invoked. The observer is an explicitly owned abstract-event extension.
    public sealed class OfflineAnalyticsTests
    {
        // Facade 0x06000003..0b forwards through IAnalytics interface slots (x86-64 e.g.
        // Initialise 0x199da00 slot 1, ProcessManualQueue 0x199dba0 slot 3, ApplicationPause
        // 0x199df00 slot 7, ClearAnalyticsEventsCached 0x199e0a0 slot 9). An owned settings
        // identity survives every forwarder except Shutdown, so routing any of them to the
        // Shutdown or Initialise slot fails; LocalAnalytics itself is offline policy.
        [Test]
        public void DefaultFacadeUsesOfflineProviderAndCompletesLifecycleLocally()
        {
            Assert.That(Analytics.IsSupported, Is.False);
            FieldInfo provider = typeof(Analytics).GetField("s_analytics", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.That(provider, Is.Not.Null);
            Assert.That(provider.IsInitOnly, Is.True);
            Assert.That(provider.FieldType, Is.EqualTo(typeof(IAnalytics)));
            Assert.That(provider.GetValue(null).GetType(), Is.EqualTo(typeof(LocalAnalytics)));
            IAnalyticsSettings priorSettings = Analytics.GetAnalyticsSettings();
            try
            {
                Analytics.Initialise(null);
                Assert.That(Analytics.GetAnalyticsSettings(), Is.Null);
                foreach (int category in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
                {
                    Analytics.PushAnalyticsEventToManualQueue(null, category);
                    Analytics.ProcessManualQueue(category);
                }
                Analytics.ApplicationPause(true);
                Analytics.ApplicationPause(false);
                Analytics.ClearAnalyticsEventsCached();
                Analytics.ResetEventIndex();
                Assert.That(Analytics.GetEventIndex(), Is.EqualTo(-1));
                Assert.That(Analytics.GetEventIndex(), Is.EqualTo(-1));
                Analytics.Shutdown();
                Assert.That(Analytics.IsSupported, Is.False);
                Assert.That(Analytics.GetAnalyticsSettings(), Is.Null);

                var owned = new OwnedSettings();
                Analytics.Initialise(owned);
                Assert.That(Analytics.GetAnalyticsSettings(), Is.SameAs(owned));
                Analytics.ResetEventIndex();
                Analytics.ProcessManualQueue(0);
                Analytics.PushAnalyticsEventToManualQueue(null, 0);
                Analytics.ApplicationPause(true);
                Analytics.ClearAnalyticsEventsCached();
                Assert.That(Analytics.GetEventIndex(), Is.EqualTo(-1));
                Assert.That(Analytics.GetAnalyticsSettings(), Is.SameAs(owned));
                Analytics.Shutdown();
                Assert.That(Analytics.GetAnalyticsSettings(), Is.Null);
            }
            finally
            {
                Analytics.Shutdown();
                Analytics.Initialise(priorSettings);
            }
        }

        // Identity-only settings: the offline provider must never read them.
        private sealed class OwnedSettings : IAnalyticsSettings
        {
            private static Exception Read() => new InvalidOperationException("owned analytics settings must not be read");
            public string GameID => throw Read();
            public string Environment => throw Read();
            public Uri ServerBaseURL => throw Read();
            public int MaxQueueLength => throw Read();
            public int MaxSendAttempts => throw Read();
            public bool IsAnalyticsEventsSendingAllowed => throw Read();
            public bool IsAnalyticsEventsCollectionAllowed => throw Read();
            public bool PersistentEventIndex => throw Read();
            public bool IsAnalyticsEventToManualQueueCollectionEnabled(int category) => throw Read();
        }

        [Test]
        public void DiscardReleasesOriginalEventCallbacksWithoutCollectingLateData()
        {
            Assert.That(Analytics.IsSupported, Is.False);
            var readinessFault = new InvalidOperationException("owned readiness callback must remain uncalled");
            var lateDataFault = new InvalidOperationException("owned late-data callback must remain uncalled");
            OwnedEvent value = AnalyticsEvent<OwnedEvent>.Create(() => { throw readinessFault; }, _ => { throw lateDataFault; });
            IAnalyticsEvent contract = value;
            try
            {
                foreach (int category in new[] { int.MinValue, 0, 1, int.MaxValue })
                {
                    value = AnalyticsEvent<OwnedEvent>.Create(() => { throw readinessFault; }, _ => { throw lateDataFault; });
                    Assert.That(ReferenceEquals(value, contract), Is.True);
                    Analytics.PushAnalyticsEventToManualQueue(contract, category);
                    Assert.That(contract.IsLateDataReady, Is.True, "original ReleaseEvent cleared its readiness callback");
                    contract.SetLateData();
                    Assert.That(contract.CreateData(), Is.Null);
                }
                Assert.That(value.InitialiseCalls, Is.EqualTo(0));
                Assert.That(value.FillDataCalls, Is.EqualTo(0));
                Assert.That(ObjectPool<OwnedEvent>.IsInitialised(), Is.False);
                Assert.That(ObjectPool<OwnedEvent>.UsedObjectCount, Is.EqualTo(0));
            }
            finally { contract.ReleaseEvent(); }
        }

        [Test]
        public void UnsupportedOriginalEventReusesItsStubWithoutInitialisingThePool()
        {
            Assert.That(Analytics.IsSupported, Is.False);
            OwnedEvent first = AnalyticsEvent<OwnedEvent>.Create();
            OwnedEvent second = AnalyticsEvent<OwnedEvent>.Create();
            try
            {
                Assert.That(ReferenceEquals(first, second), Is.True);
                Assert.That(((IAnalyticsEvent)first).IsLateDataReady, Is.True);
                ((IAnalyticsEvent)first).SetLateData();
                Assert.That(((IAnalyticsEvent)first).CreateData(), Is.Null);
                Assert.That(first.InitialiseCalls, Is.EqualTo(0));
                Assert.That(first.FillDataCalls, Is.EqualTo(0));
                Assert.That(ObjectPool<OwnedEvent>.IsInitialised(), Is.False);
                ((IAnalyticsEvent)first).ReleaseEvent();
                Assert.That(ObjectPool<OwnedEvent>.UsedObjectCount, Is.EqualTo(0));
                Assert.That(ReferenceEquals(first, AnalyticsEvent<OwnedEvent>.Create()), Is.True);
            }
            finally { ((IAnalyticsEvent)first).ReleaseEvent(); }
        }

        [Test]
        public void GenuineAnalyticsConfigurationRetainsItsAuthoredInspectorDefault()
        {
            HLAnalyticsConfigurationAsset first = null, second = null;
            try
            {
                first = ScriptableObject.CreateInstance<HLAnalyticsConfigurationAsset>();
                second = ScriptableObject.CreateInstance<HLAnalyticsConfigurationAsset>();
                Assert.That(first != null && second != null, Is.True);
                first.hideFlags = second.hideFlags = HideFlags.HideAndDontSave;
                Assert.That(ReferenceEquals(first, second), Is.False);
                Assert.That(first.EnableEditorDataGeneration, Is.True);
                Assert.That(second.EnableEditorDataGeneration, Is.True);
                first.Validate();
                Assert.That(first.EnableEditorDataGeneration, Is.True);
                FieldInfo field = typeof(HLAnalyticsConfigurationAsset).GetField("m_enableEditorDataGeneration", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                Assert.That(field, Is.Not.Null);
                Assert.That(field.IsStatic || field.IsInitOnly, Is.False);
                Assert.That(field.GetCustomAttribute<SerializeField>(), Is.Not.Null);
                field.SetValue(first, false);
                first.Validate();
                Assert.That(first.EnableEditorDataGeneration, Is.False, "original Validate is empty");
                Assert.That(second.EnableEditorDataGeneration, Is.True);
                Assert.That(JsonUtility.ToJson(first), Does.Contain("\"m_enableEditorDataGeneration\":false"));
            }
            finally
            {
                if (second != null) UnityEngine.Object.DestroyImmediate(second);
                if (first != null) UnityEngine.Object.DestroyImmediate(first);
            }
        }

        public sealed class OwnedEvent : AnalyticsEvent<OwnedEvent>
        {
            public int InitialiseCalls { get; private set; }
            public int FillDataCalls { get; private set; }
            public OwnedEvent() { }
            protected override void Initialise() { ++InitialiseCalls; }
            protected override void FillData(JSONHashtable data) { ++FillDataCalls; }
        }
    }
}
