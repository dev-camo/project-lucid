using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // HLUnityCore.Runtime ConditionalOutput 0x06000168..0x06000182 and its settings
    // types 0x06000183..0x06000193, against the x86-64 retail bodies. The
    // [Conditional] members are invoked by reflection because call sites to them
    // compile away here exactly as they did in the shipped player.
    [TestFixture]
    public sealed class ConditionalOutputTests
    {
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static readonly Type LogDataType = typeof(ConditionalOutput).GetNestedType("LogData", BindingFlags.NonPublic);

        private sealed class OwnedLogger : IConditionalLogger { }
        private abstract class AbstractOwnedLogger : IConditionalLogger { }
        private static class OuterA { public sealed class Same { } }
        private static class OuterB { public sealed class Same { } }

        private sealed class OwnedPreInitialiseSettings : ConditionalOutputPreInitialiseSettings
        {
            public readonly List<string> Reads = new List<string>();
            public readonly List<ConditionalOutputSettings.LogStateOverride> Overrides = new List<ConditionalOutputSettings.LogStateOverride>();
            protected override bool DefaultLogState { get { Reads.Add("DefaultLogState"); return false; } }
            protected override ConditionalOutputSettings.UnhandledLogBehaviour PreInitialiseLogBehaviour { get { Reads.Add("PreInitialiseLogBehaviour"); return ConditionalOutputSettings.UnhandledLogBehaviour.Queue; } }
            protected override List<ConditionalOutputSettings.LogStateOverride> DefaultLogStateOverrides { get { Reads.Add("DefaultLogStateOverrides"); return Overrides; } }
            // Counts scan-made instances; reset by the test that reads it.
            public static int Created;
            public OwnedPreInitialiseSettings() { Created++; }
        }

        // Records every string conversion, so formatting is visible.
        private sealed class RecordingMessage
        {
            private readonly List<string> m_log;
            private readonly string m_name;
            public RecordingMessage(List<string> log, string name) { m_log = log; m_name = name; }
            public override string ToString() { m_log.Add(m_name); return m_name; }
        }

        // Runs an action at the moment the message is converted.
        private sealed class CallbackMessage
        {
            private readonly string m_name;
            private readonly Action m_onConvert;
            public CallbackMessage(string name, Action onConvert) { m_name = name; m_onConvert = onConvert; }
            public override string ToString() { m_onConvert(); return m_name; }
        }

        private bool m_savedInitialised;
        private object m_savedSettings;
        private List<KeyValuePair<Type, bool>> m_savedLookup;
        private List<object> m_savedQueue;
        private bool m_savedPrefExists;
        private int m_savedPref;

        [SetUp]
        public void SaveSharedState()
        {
            m_savedInitialised = (bool)Field("s_initialised").GetValue(null);
            m_savedSettings = Field("s_conditionalOutputSettings").GetValue(null);
            m_savedLookup = new List<KeyValuePair<Type, bool>>(Lookup);
            m_savedQueue = new List<object>();
            foreach (object entry in LogQueue) m_savedQueue.Add(entry);
            // Only the test-owned logger's full-name key is written.
            m_savedPrefExists = PlayerPrefs.HasKey(PrefKey);
            m_savedPref = PlayerPrefs.GetInt(PrefKey);
            PlayerPrefs.DeleteKey(PrefKey);
        }

        [TearDown]
        public void RestoreSharedState()
        {
            Field("s_initialised").SetValue(null, m_savedInitialised);
            Field("s_conditionalOutputSettings").SetValue(null, m_savedSettings);
            Lookup.Clear();
            foreach (KeyValuePair<Type, bool> pair in m_savedLookup) Lookup.Add(pair.Key, pair.Value);
            ClearQueue();
            MethodInfo enqueue = LogQueue.GetType().GetMethod("Enqueue");
            foreach (object entry in m_savedQueue) enqueue.Invoke(LogQueue, new[] { entry });
            if (m_savedPrefExists) PlayerPrefs.SetInt(PrefKey, m_savedPref);
            else PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }

        // 0x1aa1ccb..0x1aa1d79: Queue stores the raw arguments in LogData field order
        // before the condition is tested, without reading the lookup or the message.
        [Test]
        public void QueueBeforeInitialiseStoresRawArgumentsWithoutReadingLookupOrCondition()
        {
            var conversions = new List<string>();
            var message = new RecordingMessage(conversions, "queued");
            var context = new GameObject("owned-conditional-output-context");
            try
            {
                Prepare(initialised: false, ConditionalOutputSettings.UnhandledLogBehaviour.Queue);
                int before = QueueCount;
                Internal(typeof(OwnedLogger), false, message, context, "red");
                Assert.That(QueueCount, Is.EqualTo(before + 1));
                object entry = QueueLast();
                Assert.That(Get(entry, "Type"), Is.SameAs(typeof(OwnedLogger)));
                Assert.That(Get(entry, "Condition"), Is.EqualTo(false));
                Assert.That(Get(entry, "Message"), Is.SameAs(message));
                Assert.That(Get(entry, "Context"), Is.SameAs(context));
                Assert.That(Get(entry, "Colour"), Is.EqualTo("red"));
                Assert.That(conversions, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(context);
            }
        }

        // 0x1aa1d9c skips the lookup for ForceLog; 0x1aa1bc0 reads it once initialised.
        // Formatting (0x1aa1c09) converts the message only when enabled and conditioned.
        [Test]
        public void LookupGateAppliesOnlyAfterInitialiseAndFormatsOnlyEnabledConditionedCalls()
        {
            var conversions = new List<string>();
            Prepare(initialised: false, ConditionalOutputSettings.UnhandledLogBehaviour.ForceLog);
            int before = QueueCount;
            Internal(typeof(OwnedLogger), true, new RecordingMessage(conversions, "forced"), null, null);
            Internal(typeof(OwnedLogger), false, new RecordingMessage(conversions, "forced-false"), null, null);
            Assert.That(QueueCount, Is.EqualTo(before));
            CollectionAssert.AreEqual(new[] { "forced" }, conversions);

            // Initialised ignores the pre-initialise behaviour, even Queue.
            Prepare(initialised: true, ConditionalOutputSettings.UnhandledLogBehaviour.Queue);
            Assert.Throws<KeyNotFoundException>(() => Internal(typeof(OwnedLogger), true, new RecordingMessage(conversions, "missing"), null, null));
            Lookup[typeof(OwnedLogger)] = false;
            Internal(typeof(OwnedLogger), true, new RecordingMessage(conversions, "disabled"), null, null);
            Lookup[typeof(OwnedLogger)] = true;
            Internal(typeof(OwnedLogger), false, new RecordingMessage(conversions, "enabled-false"), null, null);
            Internal(typeof(OwnedLogger), true, new RecordingMessage(conversions, "enabled"), null, null);
            Assert.That(QueueCount, Is.EqualTo(before));
            CollectionAssert.AreEqual(new[] { "forced", "enabled" }, conversions);
        }

        // 0x1aa1da8..0x1aa1e13: an unknown behaviour reports through HLOutput.LogError
        // and returns, leaving the queue and the message untouched. The known
        // behaviours report nothing.
        [Test]
        public void UnhandledBehaviourReportsAndNeitherQueuesNorFormatsTheMessage()
        {
            var conversions = new List<string>();
            using (var log = new OwnedCoreErrorLog())
            {
                Prepare(initialised: false, ConditionalOutputSettings.UnhandledLogBehaviour.ForceLog);
                Internal(typeof(OwnedLogger), false, "forced", null, null);
                Prepare(initialised: false, ConditionalOutputSettings.UnhandledLogBehaviour.Queue);
                Internal(typeof(OwnedLogger), false, "queued", null, null);
                Assert.That(log.Errors, Is.Empty);

                Prepare(initialised: false, (ConditionalOutputSettings.UnhandledLogBehaviour)2);
                int before = QueueCount;
                Internal(typeof(OwnedLogger), true, new RecordingMessage(conversions, "unhandled"), null, null);
                Assert.That(QueueCount, Is.EqualTo(before));
                Assert.That(conversions, Is.Empty);
                CollectionAssert.AreEqual(new[] { "Unhandled PreInitialiseLogBehaviour value '2' in ConditionalLogSettings.:0" }, log.Errors);
            }
        }

        // 0x1aa2010: FIFO drain; each message is converted once by the "(Delayed log)"
        // format before the gate, so even a false condition is converted.
        [Test]
        public void DumpLogQueueDrainsInOrderFormattingEachMessageOnce()
        {
            var conversions = new List<string>();
            Prepare(initialised: false, ConditionalOutputSettings.UnhandledLogBehaviour.Queue);
            ClearQueue();
            Internal(typeof(OwnedLogger), true, new RecordingMessage(conversions, "first"), null, null);
            Internal(typeof(OwnedLogger), false, new RecordingMessage(conversions, "second"), null, null);
            Internal(typeof(OwnedLogger), true, new RecordingMessage(conversions, "third"), null, null);
            Assert.That(conversions, Is.Empty);

            Prepare(initialised: false, ConditionalOutputSettings.UnhandledLogBehaviour.ForceLog);
            Invoke("DumpLogQueue");
            Assert.That(QueueCount, Is.EqualTo(0));
            CollectionAssert.AreEqual(new[] { "first", "second", "third" }, conversions);
        }

        // 0x1aa1e40: null inverts the entry, values are taken as given, the full name
        // keys PlayerPrefs as 0/1, and the event follows each write.
        [Test]
        public void ToggleLogInvertsOnNullPersistsByFullNameAndRaisesAfterwards()
        {
            Type type = typeof(OwnedLogger);
            int raised = 0;
            Action handler = () =>
            {
                raised++;
                Assert.That(PlayerPrefs.HasKey(type.FullName), Is.True);
            };
            Lookup[type] = true;
            ConditionalOutput.OnConditionalLoggerToggled += handler;
            try
            {
                Toggle(type, null);
                Assert.That(Lookup[type], Is.False);
                Assert.That(PlayerPrefs.GetInt(type.FullName, -1), Is.EqualTo(0));
                Assert.That(PlayerPrefs.HasKey(type.Name), Is.False);
                Assert.That(raised, Is.EqualTo(1));

                Toggle(type, true);
                Toggle(type, true);
                Assert.That(Lookup[type], Is.True);
                Assert.That(PlayerPrefs.GetInt(type.FullName, -1), Is.EqualTo(1));
                Assert.That(raised, Is.EqualTo(3));

                Toggle(type, null);
                Assert.That(Lookup[type], Is.False);
                Assert.That(raised, Is.EqualTo(4));
            }
            finally
            {
                ConditionalOutput.OnConditionalLoggerToggled -= handler;
            }
        }

        // 0x1aa1fd2: a null request on an unknown type fails before any write or event.
        [Test]
        public void ToggleLogNullOnUnknownTypeThrowsBeforeWriting()
        {
            Type type = typeof(OwnedLogger);
            Lookup.Remove(type);
            int raised = 0;
            Action handler = () => raised++;
            ConditionalOutput.OnConditionalLoggerToggled += handler;
            try
            {
                Assert.Throws<KeyNotFoundException>(() => Toggle(type, null));
                Assert.That(Lookup.ContainsKey(type), Is.False);
                Assert.That(PlayerPrefs.HasKey(type.FullName), Is.False);
                Assert.That(raised, Is.EqualTo(0));
            }
            finally
            {
                ConditionalOutput.OnConditionalLoggerToggled -= handler;
            }
        }

        // 0x8f15b0 forwards every argument to ConditionalLogInternal with the type
        // from a constrained call on the logger, not typeof(T): instantiated over the
        // interface, the queued type is still the runtime class.
        [Test]
        public void ConditionalLogWithConditionForwardsTheLoggerRuntimeType()
        {
            Prepare(initialised: false, ConditionalOutputSettings.UnhandledLogBehaviour.Queue);
            var logger = new OwnedLogger();
            var context = new GameObject("owned-conditional-output-context");
            try
            {
                int before = QueueCount;
                Generic("ConditionalLog", 5, typeof(OwnedLogger)).Invoke(null, new object[] { logger, false, "long", context, "green" });
                Assert.That(QueueCount, Is.EqualTo(before + 1));
                object entry = QueueLast();
                Assert.That(Get(entry, "Type"), Is.SameAs(typeof(OwnedLogger)));
                Assert.That(Get(entry, "Condition"), Is.EqualTo(false));
                Assert.That(Get(entry, "Message"), Is.EqualTo("long"));
                Assert.That(Get(entry, "Context"), Is.SameAs(context));
                Assert.That(Get(entry, "Colour"), Is.EqualTo("green"));

                Generic("ConditionalLog", 5, typeof(IConditionalLogger)).Invoke(null, new object[] { logger, true, "boxed", null, null });
                Assert.That(QueueCount, Is.EqualTo(before + 2));
                Assert.That(Get(QueueLast(), "Type"), Is.SameAs(typeof(OwnedLogger)));
            }
            finally
            {
                Object.DestroyImmediate(context);
            }
        }

        // Retail-stripping oracle: 0x8f15a0 (4-argument ConditionalLog<T>) and
        // 0x8f1700 (ToggleLog<T>) are a bare RET in the shipped player.
        [Test]
        public void RetailStrippedGenericBodiesAreNoOps()
        {
            Prepare(initialised: false, ConditionalOutputSettings.UnhandledLogBehaviour.Queue);
            Lookup[typeof(OwnedLogger)] = true;
            int before = QueueCount;
            Generic("ConditionalLog", 4, typeof(OwnedLogger)).Invoke(null, new object[] { new OwnedLogger(), "short", null, "blue" });
            Assert.That(QueueCount, Is.EqualTo(before));

            Generic("ToggleLog", 1, typeof(OwnedLogger)).Invoke(null, new object[] { (bool?)false });
            Assert.That(Lookup[typeof(OwnedLogger)], Is.True);
            Assert.That(PlayerPrefs.HasKey(typeof(OwnedLogger).FullName), Is.False);
        }

        // Retail-stripping oracle: 0x1aa12a0, the shipped Initialise, only latches
        // the flag; its development-only setup calls are compiled out.
        [Test]
        public void RetailStrippedInitialiseOnlyLatchesTheInitialisedFlag()
        {
            var settings = new ConditionalOutputSettings();
            Field("s_conditionalOutputSettings").SetValue(null, settings);
            Field("s_initialised").SetValue(null, false);
            int lookupCount = Lookup.Count;
            int queueCount = QueueCount;
            Invoke("Initialise");
            Invoke("Initialise");
            Assert.That(Field("s_initialised").GetValue(null), Is.EqualTo(true));
            Assert.That(Field("s_conditionalOutputSettings").GetValue(null), Is.SameAs(settings));
            Assert.That(Lookup.Count, Is.EqualTo(lookupCount));
            Assert.That(QueueCount, Is.EqualTo(queueCount));
        }

        // 0x1aa2010..0x1aa209d: each replay passes the dequeued Type, Condition,
        // Context and Colour with the message formatted as "(Delayed log) {0}". The
        // first conversion switches back to Queue, so its replay is queued again and
        // the second conversion captures it before the drain finishes.
        [Test]
        public void DumpLogQueueReplaysEveryFieldWithTheDelayedPrefix()
        {
            var context = new GameObject("owned-conditional-output-context");
            try
            {
                Prepare(initialised: false, ConditionalOutputSettings.UnhandledLogBehaviour.Queue);
                ClearQueue();
                object requeued = null;
                var first = new CallbackMessage("first", () => Prepare(initialised: false, ConditionalOutputSettings.UnhandledLogBehaviour.Queue));
                var second = new CallbackMessage("second", () =>
                {
                    requeued = QueueLast();
                    Prepare(initialised: false, ConditionalOutputSettings.UnhandledLogBehaviour.ForceLog);
                });
                Internal(typeof(OwnedLogger), false, first, context, "red");
                Internal(typeof(OuterA.Same), true, second, null, "blue");

                Prepare(initialised: false, ConditionalOutputSettings.UnhandledLogBehaviour.ForceLog);
                Invoke("DumpLogQueue");
                Assert.That(QueueCount, Is.EqualTo(0));
                Assert.That(requeued, Is.Not.Null);
                Assert.That(Get(requeued, "Type"), Is.SameAs(typeof(OwnedLogger)));
                Assert.That(Get(requeued, "Condition"), Is.EqualTo(false));
                Assert.That(Get(requeued, "Message"), Is.EqualTo("(Delayed log) first"));
                Assert.That(Get(requeued, "Context"), Is.SameAs(context));
                Assert.That(Get(requeued, "Colour"), Is.EqualTo("red"));
            }
            finally
            {
                Object.DestroyImmediate(context);
            }
        }

        // 0x1aa1730: each non-abstract IConditionalLogger implementation is added
        // with PlayerPrefs[FullName] == 1, defaulting to the settings' state for the
        // type. A same-named interface from another namespace does not match, and a
        // second scan throws on the first duplicate Add.
        [Test]
        public void InitialiseLoggerLookupReadsPlayerPrefsOverTheDefaultAndRejectsDuplicates()
        {
            var overrides = new List<ConditionalOutputSettings.LogStateOverride>();
            Field("s_conditionalOutputSettings").SetValue(null, new ConditionalOutputSettings(false,
                ConditionalOutputSettings.UnhandledLogBehaviour.ForceLog, overrides));

            // Absent key: the default state decides, through the full-name override.
            Assert.That(ScanLookup(), Is.False);
            overrides.Add(new ConditionalOutputSettings.LogStateOverride { TypeName = PrefKey, Enabled = true });
            Assert.That(ScanLookup(), Is.True);
            Assert.That(Lookup.ContainsKey(typeof(AbstractOwnedLogger)), Is.False);
            Assert.That(Lookup.ContainsKey(typeof(ConditionalOutputDecoy.DecoyLogger)), Is.False);
            Assert.That(Lookup.ContainsKey(typeof(ProcessManager).GetNestedType("ProcessManagerLogger", BindingFlags.NonPublic)), Is.True);

            // A stored key replaces the default; only 1 enables.
            PlayerPrefs.SetInt(PrefKey, 0);
            Assert.That(ScanLookup(), Is.False);
            PlayerPrefs.SetInt(PrefKey, 2);
            Assert.That(ScanLookup(), Is.False);
            overrides[0].Enabled = false;
            PlayerPrefs.SetInt(PrefKey, 1);
            Assert.That(ScanLookup(), Is.True);

            Assert.Throws<ArgumentException>(() => Invoke("InitialiseLoggerLookup"));
        }

        // 0x1aa1660: without a Resources ConditionalOutputConfiguration the current
        // settings reference is kept.
        [Test]
        public void InitialiseSettingsWithoutConfigurationKeepsTheCurrentSettings()
        {
            Assert.That(Resources.Load<ConditionalOutputConfiguration>(ConditionalOutputConfiguration.FileName), Is.Null,
                "precondition: this project ships no Resources configuration");
            var settings = new ConditionalOutputSettings();
            Field("s_conditionalOutputSettings").SetValue(null, settings);
            Invoke("InitialiseSettings");
            Assert.That(Field("s_conditionalOutputSettings").GetValue(null), Is.SameAs(settings));
        }

        // 0x1aa1340: the scan creates at most one provider and stores its fresh
        // settings. The concrete base is itself a candidate, so when
        // HLUnityCore.Runtime is enumerated before this assembly the base wins over
        // the later test-owned subclass.
        [Test]
        public void CreatePreInitialiseSettingsUsesTheFirstEnumeratedProviderOnly()
        {
            var previous = new ConditionalOutputSettings();
            Field("s_conditionalOutputSettings").SetValue(null, previous);
            OwnedPreInitialiseSettings.Created = 0;
            Invoke("CreatePreInitialiseSettings");
            var created = (ConditionalOutputSettings)Field("s_conditionalOutputSettings").GetValue(null);
            Assert.That(created, Is.Not.Null);
            Assert.That(created, Is.Not.SameAs(previous));
            Assert.That(OwnedPreInitialiseSettings.Created, Is.LessThanOrEqualTo(1));
            if (OwnedPreInitialiseSettings.Created == 1)
            {
                Assert.That(created.PreInitialiseLogBehaviour, Is.EqualTo(ConditionalOutputSettings.UnhandledLogBehaviour.Queue));
                Assert.That(created.GetDefaultLogState(typeof(OwnedLogger)), Is.False);
            }
            else
            {
                Assert.That(created.PreInitialiseLogBehaviour, Is.EqualTo(ConditionalOutputSettings.UnhandledLogBehaviour.ForceLog));
                Assert.That(created.GetDefaultLogState(typeof(OwnedLogger)), Is.True);
            }

            List<Assembly> assemblies = new List<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
            if (assemblies.IndexOf(typeof(ConditionalOutput).Assembly) < assemblies.IndexOf(typeof(ConditionalOutputTests).Assembly))
                Assert.That(OwnedPreInitialiseSettings.Created, Is.EqualTo(0));
        }

        // 0x1aa2120 forwards unconditionally; 0x1aa2180 writes only once initialised.
        [Test]
        public void StaticDefaultStateReadsAlwaysButWritesOnlyAfterInitialise()
        {
            var settings = new ConditionalOutputSettings(true, ConditionalOutputSettings.UnhandledLogBehaviour.ForceLog,
                new List<ConditionalOutputSettings.LogStateOverride>());
            Field("s_conditionalOutputSettings").SetValue(null, settings);
            Field("s_initialised").SetValue(null, false);
            ConditionalOutput.SetDefaultLogState(typeof(OwnedLogger), false);
            Assert.That(ConditionalOutput.GetDefaultLogState(typeof(OwnedLogger)), Is.True);
            Assert.That(Overrides(settings), Is.Empty);

            Field("s_initialised").SetValue(null, true);
            ConditionalOutput.SetDefaultLogState(typeof(OwnedLogger), false);
            Assert.That(ConditionalOutput.GetDefaultLogState(typeof(OwnedLogger)), Is.False);
            Assert.That(settings.GetDefaultLogState(typeof(OwnedLogger)), Is.False);
        }

        // 0x1aa2640 initialises m_defaultLogState to true; 0x1aa1a40 and 0x1aa2240 match
        // overrides by full name, update the first match in place or append.
        [Test]
        public void SettingsDefaultTrueAndOverridesKeyedByFullName()
        {
            var settings = new ConditionalOutputSettings();
            Assert.That(settings.PreInitialiseLogBehaviour, Is.EqualTo(ConditionalOutputSettings.UnhandledLogBehaviour.ForceLog));
            Assert.That(settings.GetDefaultLogState(typeof(OuterA.Same)), Is.True);
            Assert.That(Overrides(settings), Is.Empty);

            settings.SetDefaultLogState(typeof(OuterA.Same), false);
            Assert.That(settings.GetDefaultLogState(typeof(OuterA.Same)), Is.False);
            Assert.That(settings.GetDefaultLogState(typeof(OuterB.Same)), Is.True);
            List<ConditionalOutputSettings.LogStateOverride> overrides = Overrides(settings);
            Assert.That(overrides.Count, Is.EqualTo(1));
            ConditionalOutputSettings.LogStateOverride first = overrides[0];
            Assert.That(first.TypeName, Is.EqualTo(typeof(OuterA.Same).FullName));
            Assert.That(first.Enabled, Is.False);

            settings.SetDefaultLogState(typeof(OuterA.Same), true);
            Assert.That(Overrides(settings).Count, Is.EqualTo(1));
            Assert.That(first.Enabled, Is.True);

            // A short-name override does not apply.
            Overrides(settings).Add(new ConditionalOutputSettings.LogStateOverride { TypeName = typeof(OuterB.Same).Name, Enabled = false });
            Assert.That(settings.GetDefaultLogState(typeof(OuterB.Same)), Is.True);

            var authored = new ConditionalOutputSettings(false, ConditionalOutputSettings.UnhandledLogBehaviour.Queue, overrides);
            Assert.That(authored.PreInitialiseLogBehaviour, Is.EqualTo(ConditionalOutputSettings.UnhandledLogBehaviour.Queue));
            Assert.That(authored.GetDefaultLogState(typeof(OwnedLogger)), Is.False);
            Assert.That(Overrides(authored), Is.SameAs(overrides));
        }

        // 0x1aa2500/10/20 defaults and 0x1aa15b0's virtual reads in declaration order.
        [Test]
        public void PreInitialiseSettingsDefaultsAndVirtualReadOrder()
        {
            var baseProvider = new ConditionalOutputPreInitialiseSettings();
            ConditionalOutputSettings first = baseProvider.CreateConditionalOutputSettings();
            ConditionalOutputSettings second = baseProvider.CreateConditionalOutputSettings();
            Assert.That(first.GetDefaultLogState(typeof(OwnedLogger)), Is.True);
            Assert.That(first.PreInitialiseLogBehaviour, Is.EqualTo(ConditionalOutputSettings.UnhandledLogBehaviour.ForceLog));
            Assert.That(Overrides(first), Is.Empty);
            Assert.That(Overrides(first), Is.Not.SameAs(Overrides(second)));

            var owned = new OwnedPreInitialiseSettings();
            ConditionalOutputSettings created = owned.CreateConditionalOutputSettings();
            CollectionAssert.AreEqual(new[] { "DefaultLogState", "PreInitialiseLogBehaviour", "DefaultLogStateOverrides" }, owned.Reads);
            Assert.That(created.GetDefaultLogState(typeof(OwnedLogger)), Is.False);
            Assert.That(created.PreInitialiseLogBehaviour, Is.EqualTo(ConditionalOutputSettings.UnhandledLogBehaviour.Queue));
            Assert.That(Overrides(created), Is.SameAs(owned.Overrides));
        }

        private static FieldInfo Field(string name)
        {
            FieldInfo field = typeof(ConditionalOutput).GetField(name, StaticMembers);
            Assert.That(field, Is.Not.Null, name);
            return field;
        }

        private static Dictionary<Type, bool> Lookup => (Dictionary<Type, bool>)Field("s_conditionalLoggerLookup").GetValue(null);

        private static string PrefKey => typeof(OwnedLogger).FullName;

        private static ICollection LogQueue => (ICollection)Field("s_logQueue").GetValue(null);

        private static int QueueCount => LogQueue.Count;

        private static void ClearQueue() => LogQueue.GetType().GetMethod("Clear").Invoke(LogQueue, null);

        private static object QueueLast()
        {
            object last = null;
            foreach (object entry in LogQueue) last = entry;
            return last;
        }

        private static object Get(object logData, string property)
        {
            Assert.That(LogDataType, Is.Not.Null);
            return LogDataType.GetProperty(property, BindingFlags.Instance | BindingFlags.Public).GetValue(logData);
        }

        private static List<ConditionalOutputSettings.LogStateOverride> Overrides(ConditionalOutputSettings settings)
        {
            FieldInfo field = typeof(ConditionalOutputSettings).GetField("m_defaultLogStateOverrides", BindingFlags.Instance | BindingFlags.NonPublic);
            return (List<ConditionalOutputSettings.LogStateOverride>)field.GetValue(settings);
        }

        // Runs InitialiseLoggerLookup on an empty lookup and returns the owned entry.
        private static bool ScanLookup()
        {
            Lookup.Clear();
            Invoke("InitialiseLoggerLookup");
            return Lookup[typeof(OwnedLogger)];
        }

        private static void Prepare(bool initialised, ConditionalOutputSettings.UnhandledLogBehaviour behaviour)
        {
            Field("s_initialised").SetValue(null, initialised);
            Field("s_conditionalOutputSettings").SetValue(null, new ConditionalOutputSettings(true, behaviour,
                new List<ConditionalOutputSettings.LogStateOverride>()));
        }

        private static void Internal(Type type, bool condition, object message, Object context, string colour)
        {
            MethodInfo method = typeof(ConditionalOutput).GetMethod("ConditionalLogInternal", StaticMembers, null,
                new[] { typeof(Type), typeof(bool), typeof(object), typeof(Object), typeof(string) }, null);
            Call(method, type, condition, message, context, colour);
        }

        private static void Toggle(Type type, bool? enabled)
        {
            MethodInfo method = typeof(ConditionalOutput).GetMethod("ToggleLog", StaticMembers, null,
                new[] { typeof(Type), typeof(bool?) }, null);
            Call(method, type, enabled);
        }

        private static void Invoke(string name)
        {
            Call(typeof(ConditionalOutput).GetMethod(name, StaticMembers, null, Type.EmptyTypes, null));
        }

        private static MethodInfo Generic(string name, int parameterCount, Type argument)
        {
            foreach (MethodInfo method in typeof(ConditionalOutput).GetMethods(StaticMembers))
                if (method.Name == name && method.IsGenericMethodDefinition && method.GetParameters().Length == parameterCount)
                    return method.MakeGenericMethod(argument);
            Assert.Fail(name + "/" + parameterCount);
            return null;
        }

        private static void Call(MethodInfo method, params object[] arguments)
        {
            Assert.That(method, Is.Not.Null);
            try
            {
                method.Invoke(null, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }
    }
}

namespace ProjectLucid.Tests.ConditionalOutputDecoy
{
    // Shares IConditionalLogger's simple name only; InitialiseLoggerLookup's
    // identity check after GetInterface(Name) must reject it.
    internal interface IConditionalLogger { }

    internal sealed class DecoyLogger : IConditionalLogger { }
}
