using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Owned synchronous formatting cases. No activation, frame yield or service startup.
    public sealed class MissionStringsFormattingTests
    {
        private const Strings Timer = Strings.MISSION_TIMER_FORMAT_DESCRIPTION;
        private const Strings Description = Strings.NUMBER_SEPARATOR;

        private const BindingFlags StaticFields = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.DeclaredOnly;
        private const BindingFlags InstanceFields = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.DeclaredOnly;

        private static FieldInfo Field(Type type, string name, BindingFlags flags)
        {
            FieldInfo field = type.GetField(name, flags);
            Assert.IsNotNull(field, type.FullName + "." + name);
            return field;
        }

        private static bool SameField(FieldInfo left, FieldInfo right) =>
            left.Module == right.Module && left.MetadataToken == right.MetadataToken && left.DeclaringType == right.DeclaringType;

        private static void RestoreStatics(List<FieldState> fields, int index = 0)
        {
            if (index == fields.Count) return;
            try
            {
                Assert.IsFalse(fields[index].Field.IsInitOnly, "never write readonly provider fields");
                fields[index].Field.SetValue(null, fields[index].Value);
            }
            finally { RestoreStatics(fields, index + 1); }
        }

        private static void SameValue(object expected, object actual, string label)
        {
            if (expected != null && expected.GetType().IsValueType)
                Assert.AreEqual(expected, actual, label);
            else
                Assert.AreSame(expected, actual, label);
        }

        private sealed class FieldState
        {
            internal readonly FieldInfo Field;
            internal readonly object Target;
            internal readonly object Value;
            internal FieldState(FieldInfo field, object target)
            { Field = field; Target = target; Value = field.GetValue(target); }
            internal void Check() => SameValue(Value, Field.GetValue(Target), Field.DeclaringType.FullName + "." + Field.Name);
        }

        private sealed class DictionaryState
        {
            private readonly System.Collections.IDictionary dictionary;
            private readonly List<System.Collections.DictionaryEntry> entries = new List<System.Collections.DictionaryEntry>();
            private readonly System.Collections.IDictionaryEnumerator originalEnumerator;
            internal DictionaryState(System.Collections.IDictionary value)
            {
                dictionary = value;
                originalEnumerator = value.GetEnumerator();
                foreach (System.Collections.DictionaryEntry entry in value) entries.Add(entry);
            }
            internal void Check()
            {
                // A genuine retained enumerator also rejects ordinary version-changing edits.
                originalEnumerator.Reset();
                Assert.AreEqual(entries.Count, dictionary.Count, "provider dictionary count");
                int index = 0;
                foreach (System.Collections.DictionaryEntry entry in dictionary)
                {
                    SameValue(entries[index].Key, entry.Key, "provider dictionary key/order");
                    SameValue(entries[index].Value, entry.Value, "provider dictionary value/order");
                    index++;
                }
            }
        }

        private sealed class ListState
        {
            private readonly System.Collections.IList list;
            private readonly object[] entries;
            private readonly System.Collections.IEnumerator originalEnumerator;
            internal ListState(System.Collections.IList value)
            { list = value; originalEnumerator = value.GetEnumerator(); entries = new object[value.Count]; value.CopyTo(entries, 0); }
            internal void Check()
            {
                originalEnumerator.Reset();
                Assert.AreEqual(entries.Length, list.Count, "provider list count");
                for (int i = 0; i < entries.Length; i++) SameValue(entries[i], list[i], "provider list identity/order");
            }
        }

        // This captures only the real providers touched by these lifecycle methods.
        // Registry/queues are observed, never reset, pruned or replaced on failure.
        private sealed class ProviderState
        {
            private readonly List<FieldState> fields = new List<FieldState>();
            private readonly List<DictionaryState> dictionaries = new List<DictionaryState>();
            private readonly List<ListState> lists = new List<ListState>();
            internal readonly FieldInfo Load = Field(typeof(HLPropertyStore), "LoadHandlers", StaticFields);
            internal readonly FieldInfo Save = Field(typeof(HLPropertyStore), "SaveHandlers", StaticFields);
            internal readonly FieldInfo LanguageChanged = Field(typeof(Language), "OnLanguageChanged", StaticFields);

            internal ProviderState()
            {
                Capture(typeof(ProcessManager), null, StaticFields);
                Capture(typeof(HLPropertyStore), null, StaticFields);
                Capture(typeof(Language), null, StaticFields);
                Capture(typeof(MonoSingleton<Language>), null, StaticFields);
                Assert.IsNull(Field(typeof(HLPropertyStore), "s_internalInstance", StaticFields).GetValue(null), "foreign property store must be absent");
                Assert.IsNull(Field(typeof(MonoSingleton<Language>), "<Instance>k__BackingField", StaticFields).GetValue(null), "foreign language singleton must be absent");
                Assert.IsFalse((bool)Field(typeof(ProcessManager), "s_systemActionInProgress", StaticFields).GetValue(null), "active provider dispatch is unsuitable");
                var actions = (System.Collections.IList)Field(typeof(ProcessManager), "s_actionList", StaticFields).GetValue(null);
                Assert.AreEqual(0, actions.Count, "pending provider action snapshot is unsuitable");
                lists.Add(new ListState(actions));
                var registry = (System.Collections.IDictionary)Field(typeof(ProcessManager), "s_systemDictionary", StaticFields).GetValue(null);
                dictionaries.Add(new DictionaryState(registry));
                foreach (System.Collections.DictionaryEntry entry in registry)
                {
                    object info = entry.Value;
                    Capture(info.GetType(), info, InstanceFields);
                    ObserveReference(Field(info.GetType(), "SystemRef", InstanceFields).GetValue(info));
                    var typed = (System.Collections.IDictionary)Field(info.GetType(), "SystemRefDictionary", InstanceFields).GetValue(info);
                    dictionaries.Add(new DictionaryState(typed));
                    foreach (System.Collections.DictionaryEntry reference in typed) ObserveReference(reference.Value);
                }
                var lookup = (System.Collections.IDictionary)Field(typeof(ProcessManager), "s_systemActionLookup", StaticFields).GetValue(null);
                dictionaries.Add(new DictionaryState(lookup));
                foreach (System.Collections.DictionaryEntry entry in lookup)
                    dictionaries.Add(new DictionaryState((System.Collections.IDictionary)entry.Value));
                ObserveFastAction(Field(typeof(StringTable), "StringsTableHandlers", StaticFields).GetValue(null));
            }

            private void Capture(Type type, object target, BindingFlags flags)
            {
                foreach (FieldInfo field in type.GetFields(flags))
                    if (!field.IsLiteral) fields.Add(new FieldState(field, target));
            }

            private void ObserveReference(object reference)
            {
                Assert.IsNotNull(reference);
                Type type = reference.GetType();
                Assert.IsTrue(type == typeof(SystemRef) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SystemRef<>)), "unknown registry reference is unsuitable");
                while (type != null && (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(SystemRef<>))) type = type.BaseType;
                Assert.IsNotNull(type);
                Capture(type, reference, InstanceFields);
                Assert.IsNull(Field(type, "m_system", InstanceFields).GetValue(reference), "foreign live typed system is unsuitable");
                Assert.IsNull(Field(type, "m_isystem", InstanceFields).GetValue(reference), "foreign live system is unsuitable");
                Assert.IsNull(Field(type, "OnSystemStartup", InstanceFields).GetValue(reference), "foreign startup callback is unsuitable");
                Assert.IsNull(Field(type, "OnSystemShutdown", InstanceFields).GetValue(reference), "foreign shutdown callback is unsuitable");
                ObserveFastAction(Field(type, "m_actionOnSystemValid", InstanceFields).GetValue(reference));
            }

            private void ObserveFastAction(object action)
            {
                if (action == null) return;
                Type type = action.GetType();
                Assert.IsTrue(type == typeof(FastAction) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(FastAction<>)), "unknown provider action is unsuitable");
                while (type != null && (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(FastActionBase<,>))) type = type.BaseType;
                Assert.IsNotNull(type);
                Capture(type, action, InstanceFields);
                Assert.IsFalse((bool)Field(type, "m_invocationActive", InstanceFields).GetValue(action), "active provider invocation is unsuitable");
                foreach (string name in new[] { "m_invocationList", "m_removedList", "m_addedList" })
                {
                    var list = (System.Collections.IList)Field(type, name, InstanceFields).GetValue(action);
                    Assert.AreEqual(0, list.Count, "foreign/pending provider callbacks are unsuitable");
                    lists.Add(new ListState(list));
                }
            }

            internal void Check(bool sentinelsInstalled)
            {
                foreach (FieldState state in fields)
                    if (!sentinelsInstalled || (!SameField(state.Field, Load) && !SameField(state.Field, Save) && !SameField(state.Field, LanguageChanged))) state.Check();
                foreach (DictionaryState state in dictionaries) state.Check();
                foreach (ListState state in lists) state.Check();
            }
        }

        private sealed class HandlerSentinels
        {
            private readonly FieldInfo[] fields;
            private readonly object[] original = new object[3];
            private readonly object[] seeded = new object[3];
            private readonly Delegate[][] seededMembers = new Delegate[3][];
            private int installed;
            internal HandlerSentinels(ProviderState providers, StringTable table)
            {
                fields = new[] { providers.Load, providers.Save, providers.LanguageChanged };
                string[] names = { "OnPropertyStoreLoad", "OnPropertyStoreSave", "OnLanguageChanged" };
                for (int i = 0; i < fields.Length; i++)
                {
                    original[i] = fields[i].GetValue(null);
                    MethodInfo method = typeof(StringTable).GetMethod(names[i], BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    Assert.IsNotNull(method);
                    // Bind a genuine owned method; never invoke a lifecycle/service callback.
                    Delegate owned = Delegate.CreateDelegate(fields[i].FieldType, table, method);
                    seeded[i] = Delegate.Combine((Delegate)original[i], owned);
                    seededMembers[i] = ((Delegate)seeded[i]).GetInvocationList();
                }
            }
            internal void Install()
            {
                for (int i = 0; i < fields.Length; i++)
                { fields[i].SetValue(null, seeded[i]); installed++; }
            }
            internal void Check()
            {
                for (int i = 0; i < installed; i++)
                {
                    SameValue(seeded[i], fields[i].GetValue(null), "owned lifecycle sentinel membership/order");
                    Delegate[] actual = ((Delegate)fields[i].GetValue(null)).GetInvocationList();
                    Assert.AreEqual(seededMembers[i].Length, actual.Length);
                    for (int j = 0; j < actual.Length; j++)
                    {
                        Assert.AreSame(seededMembers[i][j].Target, actual[j].Target);
                        Assert.AreEqual(seededMembers[i][j].Method, actual[j].Method);
                    }
                }
            }
            internal void Restore()
            {
                // Each deliberate delegate lease restores even if another reflection restore fails.
                try { if (installed > 0) fields[0].SetValue(null, original[0]); }
                finally
                {
                    try { if (installed > 1) fields[1].SetValue(null, original[1]); }
                    finally { if (installed > 2) fields[2].SetValue(null, original[2]); }
                }
            }
        }

        private static void WithTable(Action<Dictionary<int, StringTable.StringEntry>> assertion)
        {
            FieldInfo singleton = Field(typeof(MonoSingleton<StringTable>), "<Instance>k__BackingField", StaticFields);
            FieldInfo strings = Field(typeof(StringTable), "m_strings", InstanceFields);
            object savedSingleton = singleton.GetValue(null);
            Assert.IsNull(savedSingleton, "foreign StringTable singleton is unsuitable");
            var savedStatics = new List<FieldState>();
            foreach (FieldInfo field in typeof(StringTable).GetFields(StaticFields))
                if (!field.IsLiteral) savedStatics.Add(new FieldState(field, null));
            var providers = new ProviderState();
            CultureInfo savedCulture = CultureInfo.CurrentCulture;
            CultureInfo savedUICulture = CultureInfo.CurrentUICulture;
            GameObject owner = null;
            StringTable table = null;
            object savedStrings = null;
            HandlerSentinels sentinels = null;
            var ownedFields = new List<FieldState>();
            try
            {
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
                Assert.IsFalse(UnityEngine.TestTools.LogAssert.ignoreFailingMessages);
                providers.Check(false);
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                owner = new GameObject("Lucid owned inactive mission strings");
                owner.SetActive(false);
                Assert.IsFalse(owner.activeSelf);
                Assert.IsFalse(owner.activeInHierarchy);
                table = owner.AddComponent<StringTable>();
                Assert.IsFalse(owner.activeSelf);
                Assert.IsFalse(owner.activeInHierarchy);
                Assert.AreSame(savedSingleton, singleton.GetValue(null), "inactive creation must not dispatch singleton Awake");
                providers.Check(false);
                foreach (FieldState state in savedStatics) state.Check();
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
                savedStrings = strings.GetValue(table);
                foreach (FieldInfo field in typeof(StringTable).GetFields(InstanceFields))
                    if (!SameField(field, strings)) ownedFields.Add(new FieldState(field, table));
                sentinels = new HandlerSentinels(providers, table);
                sentinels.Install();
                var entries = new Dictionary<int, StringTable.StringEntry>
                {
                    { (int)Timer, new StringTable.StringEntry("owned-timer", "{0}:{1}:{2}", 3) },
                    { (int)Description, new StringTable.StringEntry("owned-description", "T[{0}] C[{1}]", 2) }
                };
                strings.SetValue(table, entries);
                singleton.SetValue(null, table);
                assertion(entries);
                sentinels.Check();
                providers.Check(true);
                foreach (FieldState state in ownedFields) state.Check();
                foreach (FieldState state in savedStatics) state.Check();
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                try
                {
                    try
                    {
                        try { if (table != null) strings.SetValue(table, savedStrings); }
                        finally { singleton.SetValue(null, savedSingleton); }
                    }
                    finally
                    {
                        // Sentinels stay installed through genuine inactive destruction.
                        // Membership/state equality is bounded evidence, not universal call tracing.
                        try
                        {
                            if (owner != null)
                            {
                                Assert.IsFalse(owner.activeSelf);
                                Assert.IsFalse(owner.activeInHierarchy);
                            }
                        }
                        finally { if (owner != null) Object.DestroyImmediate(owner); }
                    }
                }
                finally
                {
                    try
                    {
                        if (sentinels != null) sentinels.Check();
                        providers.Check(sentinels != null);
                        UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
                    }
                    finally
                    {
                        try { if (sentinels != null) sentinels.Restore(); }
                        finally
                        {
                            try
                            {
                                try { singleton.SetValue(null, savedSingleton); }
                                finally { RestoreStatics(savedStatics); }
                            }
                            finally
                            {
                                try { CultureInfo.CurrentCulture = savedCulture; }
                                finally { CultureInfo.CurrentUICulture = savedUICulture; }
                            }
                        }
                    }
                }
            }
            providers.Check(false);
            Assert.AreSame(savedSingleton, singleton.GetValue(null));
            Assert.AreSame(savedCulture, CultureInfo.CurrentCulture);
            Assert.AreSame(savedUICulture, CultureInfo.CurrentUICulture);
        }

        [Test]
        public void MissionTimer_NonpositiveMissingAndCustomFormats()
        {
            WithTable(entries =>
            {
                Assert.AreEqual("-:--:--", MissionStringsUtil.GetMissionTimerFormat(0f));
                Assert.AreEqual("-:--:--", MissionStringsUtil.GetMissionTimerFormat(-2f));
                entries[(int)Description] = new StringTable.StringEntry("owned-custom", "{2}/{1}/{0}", 3);
                Assert.AreEqual("12/01/0", MissionStringsUtil.GetMissionTimerFormat(1.125f, Description));
                Assert.AreEqual(string.Empty, MissionStringsUtil.GetMissionTimerFormat(1f, Strings.DECIMAL_SEPARATOR));
                entries[(int)Strings.NONE] = new StringTable.StringEntry("owned-none", "{", 0);
                Assert.AreEqual(string.Empty, MissionStringsUtil.GetMissionTimerFormat(1f, Strings.NONE));
            });
        }

        [Test]
        public void MissionTimer_FiniteFractionalFloorAndTruncation()
        {
            WithTable(entries =>
            {
                Assert.AreEqual("0:59:87", MissionStringsUtil.GetMissionTimerFormat(59.875f));
                Assert.AreEqual("1:01:12", MissionStringsUtil.GetMissionTimerFormat(61.125f));
                Assert.AreEqual("2:00:01", MissionStringsUtil.GetMissionTimerFormat(120.015625f));
            });
        }

        [Test]
        public void MissionDescription_PositiveArgumentsAndNaNDistinction()
        {
            WithTable(entries =>
            {
                Assert.AreEqual("T[0:09:50] C[4]", MissionStringsUtil.GetMissionDescription(Description, 9.5f, 4));
                Assert.AreEqual("T[] C[4]", MissionStringsUtil.GetMissionDescription(Description, 0f, 4));
                Assert.AreEqual("T[0:01:00] C[]", MissionStringsUtil.GetMissionDescription(Description, 1f, -1));
                Assert.AreEqual("T[] C[3]", MissionStringsUtil.GetMissionDescription(Description, float.NaN, 3));
                string unorderedTimer = MissionStringsUtil.GetMissionTimerFormat(float.NaN);
                Assert.AreNotEqual("-:--:--", unorderedTimer, "unordered timer follows numeric formatting; no hardware conversion value is assumed");
                Assert.AreEqual(3, unorderedTimer.Split(':').Length);
            });
        }

        [Test]
        public void MissionFormats_OrdinaryFaultsAndPositiveTimerEvaluationOrder()
        {
            WithTable(entries =>
            {
                entries[(int)Timer] = new StringTable.StringEntry("owned-bad-timer", "{", 0);
                Assert.Throws<FormatException>(() => MissionStringsUtil.GetMissionTimerFormat(0f));
                entries[(int)Description] = new StringTable.StringEntry("owned-null-description", null, 0);
                Assert.Throws<FormatException>(() => MissionStringsUtil.GetMissionDescription(Description, 1f));
                Assert.Throws<ArgumentNullException>(() => MissionStringsUtil.GetMissionDescription(Description));
                entries[(int)Timer] = new StringTable.StringEntry("owned-null-timer", null, 0);
                Assert.Throws<ArgumentNullException>(() => MissionStringsUtil.GetMissionTimerFormat(0f));
                entries[(int)Description] = new StringTable.StringEntry("owned-bad-description", "{", 0);
                Assert.Throws<FormatException>(() => MissionStringsUtil.GetMissionDescription(Description));
            });
        }
    }
}
