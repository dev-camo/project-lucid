using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of Hardlight.HLLogHandler (HLUnityCore.Runtime 0x06000a44..59)
    // and the dependencies it reads, against the shipped x86-64 bodies. Each
    // case owns its component, ProcessManager row, log callback subscription,
    // shared StringBuilder contents and HLLogConsole.AllLogTypes value.
    [TestFixture]
    public sealed class OriginalHLLogHandlerTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const SupportedLogTypes Everything = (SupportedLogTypes)0x3E;

        sealed class Owned : IDisposable
        {
            readonly VerificationFixtureOwnership.RegistryRow row;
            readonly GameObject owner;
            public readonly HLLogHandler Handler;

            // Awake (0x1b00a60) registers under the default name, Type.ToString.
            public Owned(HLLogHandlerSettingScriptableObject asset = null)
            {
                row = new VerificationFixtureOwnership.RegistryRow("Hardlight.HLLogHandler");
                owner = new GameObject("HLLogHandler fixture") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    Handler = owner.AddComponent<HLLogHandler>();
                    if (asset != null) Set(Handler, "m_settingsAsset", asset);
                    row.RegisterUsing(Handler, () => Call(Handler, "Awake"));
                }
                catch { Dispose(); throw; }
            }

            public void Dispose()
            {
                try { row.Dispose(); }
                finally { Object.DestroyImmediate(owner); }
            }
        }

        static object Get(object target, string name) => target.GetType().GetField(name, Any).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Any).SetValue(target, value);

        static object Call(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, Any).Invoke(target, args); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        static HLLogHandlerSetting Setting(string platform = "osx", ExportFormats format = 0, SupportedLogTypes types = 0,
            SupportedLogTypes stacks = 0, int maxMB = 0, ExportStages stage = 0, string exportPath = "Logs")
        {
            var setting = new HLLogHandlerSetting();
            Set(setting, "m_platform", platform);
            Set(setting, "m_exportPath", exportPath);
            Set(setting, "m_logExportFormat", format);
            Set(setting, "m_logTypeSupported", types);
            Set(setting, "m_stackTraceExtractionPerLogType", stacks);
            Set(setting, "m_maxLogSizeInMB", maxMB);
            Set(setting, "m_logExportStage", stage);
            return setting;
        }

        static List<HLLogMessage> Messages(HLLogHandler handler) => (List<HLLogMessage>)Get(handler, "m_logMessages");
        static void Receive(HLLogHandler handler, string message, string stackTrace, LogType type) =>
            Call(handler, "LogMessageReceiveThreaded", message, stackTrace, type);
        static string Describe(HLLogMessage message) =>
            (int)message.Type + "|" + message.Message + "|" + (message.StackTrace ?? "<null>");
        static string[] Drain(HLLogHandler handler)
        {
            Call(handler, "LogQueueUpdate");
            string[] result = Messages(handler).Select(Describe).ToArray();
            handler.LogFlush();
            return result;
        }

        // Restores the shared s_tempStringBuilder (0x1b02130) text after a case.
        static T WithSharedBuilder<T>(Func<T> body)
        {
            var builder = (StringBuilder)typeof(HLLogHandler).GetField("s_tempStringBuilder", Any).GetValue(null);
            string before = builder.ToString();
            try { return body(); }
            finally { builder.Length = 0; builder.Append(before); }
        }

        // Constructor 0x1b020b0 creates only the queue. Awake 0x1b00a60 with no
        // asset stores a new default setting ("Logs", Txt, OnApplicationQuit,
        // 0 MB), Array.Empty<ExclusionList>() and a 10000-entry list
        // (DefaultLogSizeInMB * AverageLogMessagesPerMB, mov r14d,0x2710).
        // Mutations caught: creating the list in the constructor, a fresh
        // empty array, auto-export without a size, or a different capacity.
        [Test]
        public void ConstructorCreatesOnlyTheQueueAndAwakeFallsBackToDefaults()
        {
            var owner = new GameObject("HLLogHandler constructor") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var fresh = owner.AddComponent<HLLogHandler>();
                Assert.IsNull(fresh.LogMessages, "no list before Awake");
                Assert.IsNotNull(Get(fresh, "m_logQueue"));
                Assert.IsNull(Get(fresh, "m_currentSetting"));
            }
            finally { Object.DestroyImmediate(owner); }

            using (var owned = new Owned())
            {
                HLLogHandler handler = owned.Handler;
                var setting = (HLLogHandlerSetting)Get(handler, "m_currentSetting");
                Assert.AreEqual("Logs", setting.ExportPath);
                Assert.AreEqual(ExportFormats.Txt, setting.LogExportFormat);
                Assert.AreEqual(ExportStages.OnApplicationQuit, setting.LogExportStage);
                Assert.AreEqual(0, setting.MaxLogSizeInMB);
                Assert.IsNull(setting.Platform);
                Assert.AreSame(Array.Empty<ExclusionList>(), Get(handler, "m_exclusionLists"));
                Assert.AreEqual(false, Get(handler, "m_isAutoExportEnabled"));
                Assert.AreEqual(10000, handler.GetEstimatedLogMessagesCount());
                Assert.AreEqual(10000, Messages(handler).Capacity);
                Assert.AreSame(handler.LogMessages, Messages(handler), "LogMessages exposes the live list (0x1b00a50)");
                Assert.AreSame(handler, ProcessManager.GetSystemRef("Hardlight.HLLogHandler", false).GetSafe());
            }
            LogAssert.NoUnexpectedReceived();
        }

        // Awake 0x1b00a60 inlines GetPlatformSetting (0x1b00d80): string.Equals
        // with OrdinalIgnoreCase (edx=5) against the active identifier ("osx"
        // in the shipped macOS PlatformUtils), first match wins; then
        // MaxLogSizeInMB * 1000 sizes the list and a positive size enables
        // auto-export. A missing row or exclusion array falls back.
        // Mutations caught: ordinal comparison, last match, copying the
        // asset's arrays, or keeping the default capacity with an asset.
        [Test]
        public void AwakeSelectsTheFirstCaseInsensitivePlatformRowAndSizesTheList()
        {
            var created = new List<Object>();
            try
            {
                var asset = ScriptableObject.CreateInstance<HLLogHandlerSettingScriptableObject>();
                created.Add(asset);
                HLLogHandlerSetting chosen = Setting("OSX", maxMB: 3);
                var exclusions = new[] { new ExclusionList { Name = "a", Exclusions = new string[0] } };
                Set(asset, "m_platformSettings", new[] { Setting("windows", maxMB: 1), chosen, Setting("osx", maxMB: 7) });
                Set(asset, "m_logExclusionList", exclusions);
                using (var owned = new Owned(asset))
                {
                    Assert.AreSame(chosen, Get(owned.Handler, "m_currentSetting"));
                    Assert.AreSame(exclusions, Get(owned.Handler, "m_exclusionLists"));
                    Assert.AreEqual(true, Get(owned.Handler, "m_isAutoExportEnabled"));
                    Assert.AreEqual(3000, owned.Handler.GetEstimatedLogMessagesCount());
                    Assert.AreEqual(3000, Messages(owned.Handler).Capacity);
                }

                var unmatched = ScriptableObject.CreateInstance<HLLogHandlerSettingScriptableObject>();
                created.Add(unmatched);
                Set(unmatched, "m_platformSettings", new[] { Setting("ios", maxMB: 5) });
                Set(unmatched, "m_logExclusionList", null);
                Assert.IsNull(unmatched.GetPlatformSetting());
                using (var owned = new Owned(unmatched))
                {
                    var setting = (HLLogHandlerSetting)Get(owned.Handler, "m_currentSetting");
                    Assert.IsNull(setting.Platform, "a new default setting replaces the missing row");
                    Assert.AreEqual(0, setting.MaxLogSizeInMB);
                    Assert.AreSame(Array.Empty<ExclusionList>(), Get(owned.Handler, "m_exclusionLists"));
                    Assert.AreEqual(10000, Messages(owned.Handler).Capacity);
                }
            }
            finally { foreach (Object value in created) Object.DestroyImmediate(value); }
            LogAssert.NoUnexpectedReceived();
        }

        // LogMessageReceiveThreaded 0x1b013d0: a mask equal to the current
        // HLLogConsole.AllLogTypes accepts everything (cmp r14d,[rax] before the
        // jump table); otherwise the type is bounded to 0..4 (cmp 4; ja return)
        // and tested against its flag. The stack trace uses the second mask the
        // same way (lookup table 0x29a8370: 8,16,4,2,32) or becomes null.
        // Mutations caught: Enum.HasFlag-style "all bits" logic, accepting
        // unmapped types under a partial mask, a cached 0x3e constant, or
        // keeping stack traces for unselected types.
        [Test]
        public void MasksFilterTypesAndOnlyTheCurrentAllLogTypesValueAcceptsUnmappedTypes()
        {
            SupportedLogTypes original = HLLogConsole.AllLogTypes;
            Assert.AreEqual(Everything, original, "fixture requires the shipped AllLogTypes value");
            var types = new[] { LogType.Error, LogType.Assert, LogType.Warning, LogType.Log, LogType.Exception, (LogType)5 };
            try
            {
                using (var owned = new Owned())
                {
                    HLLogHandler handler = owned.Handler;
                    string[] Run(SupportedLogTypes accepted, SupportedLogTypes stacks)
                    {
                        Set(handler, "m_currentSetting", Setting(types: accepted, stacks: stacks));
                        foreach (LogType type in types) Receive(handler, "m" + (int)type, "s" + (int)type, type);
                        return Drain(handler);
                    }

                    Assert.That(Run(SupportedLogTypes.Log | SupportedLogTypes.Error, SupportedLogTypes.Error),
                        Is.EqualTo(new[] { "0|m0|s0", "3|m3|<null>" }));
                    Assert.That(Run(SupportedLogTypes.Assert | SupportedLogTypes.Warning | SupportedLogTypes.Exception, SupportedLogTypes.Exception),
                        Is.EqualTo(new[] { "1|m1|<null>", "2|m2|<null>", "4|m4|s4" }));
                    Assert.That(Run(Everything, SupportedLogTypes.Error),
                        Is.EqualTo(new[] { "0|m0|s0", "1|m1|<null>", "2|m2|<null>", "3|m3|<null>", "4|m4|<null>", "5|m5|<null>" }));
                    Assert.That(Run(Everything, Everything),
                        Is.EqualTo(new[] { "0|m0|s0", "1|m1|s1", "2|m2|s2", "3|m3|s3", "4|m4|s4", "5|m5|s5" }));
                    Assert.That(Run(Everything | (SupportedLogTypes)0x40, Everything | (SupportedLogTypes)0x40),
                        Is.EqualTo(new[] { "0|m0|s0", "1|m1|s1", "2|m2|s2", "3|m3|s3", "4|m4|s4" }), "a superset mask is not the AllLogTypes value");

                    HLLogConsole.AllLogTypes = SupportedLogTypes.Warning;
                    Assert.That(Run(SupportedLogTypes.Warning, SupportedLogTypes.Error),
                        Is.EqualTo(new[] { "0|m0|s0", "1|m1|<null>", "2|m2|<null>", "3|m3|<null>", "4|m4|<null>", "5|m5|<null>" }),
                        "the field is read at call time");
                }
            }
            finally { HLLogConsole.AllLogTypes = original; }
            LogAssert.NoUnexpectedReceived();
        }

        // 0x1b013d0 inlines ExclusionList.IsExcluded for every list on the raw
        // message before enqueueing. EnableExclusionList 0x1b003b0 changes only
        // the first list whose name is not unequal (op_Inequality) and stops.
        // Mutations caught: excluding after the stack decision, matching the
        // stack trace, toggling every same-named list, or a missing-name throw.
        [Test]
        public void ExclusionListsSkipMatchesAndEnableExclusionListChangesTheFirstNamedList()
        {
            using (var owned = new Owned())
            {
                HLLogHandler handler = owned.Handler;
                Set(handler, "m_currentSetting", Setting(types: SupportedLogTypes.Log, stacks: SupportedLogTypes.Log));
                var first = new ExclusionList { Name = "noise", Exclusions = new[] { "spam" } };
                var second = new ExclusionList { Name = "noise", Exclusions = new[] { "junk" } };
                Set(handler, "m_exclusionLists", new[] { first, second });

                void Send()
                {
                    Receive(handler, "a spam b", "t", LogType.Log);
                    Receive(handler, "c junk", "t", LogType.Log);
                    Receive(handler, "keep", "spam junk", LogType.Log);
                }

                Send();
                Assert.That(Drain(handler), Is.EqualTo(new[] { "3|keep|spam junk" }));
                handler.EnableExclusionList("missing", false);
                Send();
                Assert.That(Drain(handler), Is.EqualTo(new[] { "3|keep|spam junk" }));
                handler.EnableExclusionList("noise", false);
                Send();
                Assert.That(Drain(handler), Is.EqualTo(new[] { "3|a spam b|t", "3|keep|spam junk" }), "only the first list is disabled");
                handler.EnableExclusionList("noise", true);
                Send();
                Assert.That(Drain(handler), Is.EqualTo(new[] { "3|keep|spam junk" }));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // LogQueueUpdate 0x1b00fd0 adds the stack trace length only when
        // IsNullOrEmpty is false, then the message length, appends the message
        // and only then invokes OnLogMessageReceived ([rbx+0x58]).
        // Mutations caught: counting characters twice, raising before Add,
        // or invoking after the handler was removed.
        [Test]
        public void QueueUpdateCountsStackThenMessageLengthsAndRaisesAfterListing()
        {
            using (var owned = new Owned())
            {
                HLLogHandler handler = owned.Handler;
                Set(handler, "m_currentSetting", Setting(types: SupportedLogTypes.Log, stacks: SupportedLogTypes.Log));
                var seen = new List<string>();
                Action<HLLogMessage> observer = message => seen.Add(message.Message + "@" + handler.LogMessages.Count);
                handler.OnLogMessageReceived += observer;
                Receive(handler, "abc", "de", LogType.Log);
                Receive(handler, "f", "", LogType.Log);
                Call(handler, "LogQueueUpdate");
                Assert.That(seen, Is.EqualTo(new[] { "abc@1", "f@2" }));
                Assert.AreEqual(6L, Get(handler, "m_currentLogByteSize"));

                handler.OnLogMessageReceived -= observer;
                Receive(handler, "g", null, LogType.Log);
                Call(handler, "LogQueueUpdate");
                Assert.AreEqual(2, seen.Count);
                Assert.AreEqual(7L, Get(handler, "m_currentLogByteSize"));
                Assert.AreEqual(3, handler.LogMessages.Count);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // Update 0x1b00f40 inlines the threshold check (0x1b01160):
        // imul eax,[rax+0x2c],0xf4240 is a 32-bit product, sign-extended, and
        // the flush runs at >=. 2148 MB wraps negative, so even an empty log
        // flushes. Disabled auto-export never flushes. Export format 0 keeps
        // LogExport from writing (test 0xe at 0x1afb8fa).
        // Mutations caught: a long product, a strict comparison, or checking
        // the size without the auto-export flag.
        [Test]
        public void UpdateFlushesAtTheWrappedIntThresholdOnlyWithAutoExport()
        {
            using (var owned = new Owned())
            {
                HLLogHandler handler = owned.Handler;
                bool Flushes(int maxMB, bool enabled, long size)
                {
                    Set(handler, "m_currentSetting", Setting(maxMB: maxMB));
                    Set(handler, "m_isAutoExportEnabled", enabled);
                    Messages(handler).Add(new HLLogMessage { Message = "x" });
                    Set(handler, "m_currentLogByteSize", size);
                    Call(handler, "Update");
                    bool flushed = Messages(handler).Count == 0;
                    Assert.AreEqual(flushed ? 0L : size, Get(handler, "m_currentLogByteSize"));
                    handler.LogFlush();
                    return flushed;
                }

                Assert.IsFalse(Flushes(1, true, 999999L));
                Assert.IsTrue(Flushes(1, true, 1000000L));
                Assert.IsTrue(Flushes(2148, true, 0L), "2148 * 1000000 wraps to a negative int threshold");
                Assert.IsFalse(Flushes(2147, true, 2146999999L));
                Assert.IsFalse(Flushes(1, false, 5000000L));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // OnApplicationPause 0x1b012f0 tests only stage flag 4 and never reads
        // its argument; OnApplicationQuit 0x1b01360 tests flag 2. Both flush.
        // Mutations caught: honouring the pause argument or swapped flags.
        [Test]
        public void PauseAndQuitFlushByConfiguredStageIgnoringThePauseArgument()
        {
            using (var owned = new Owned())
            {
                HLLogHandler handler = owned.Handler;
                bool Flushes(ExportStages stage, string method, params object[] args)
                {
                    Set(handler, "m_currentSetting", Setting(stage: stage));
                    Messages(handler).Add(new HLLogMessage { Message = "x" });
                    Set(handler, "m_currentLogByteSize", 9L);
                    Call(handler, method, args);
                    bool flushed = Messages(handler).Count == 0 && (long)Get(handler, "m_currentLogByteSize") == 0L;
                    handler.LogFlush();
                    return flushed;
                }

                Assert.IsTrue(Flushes(ExportStages.OnApplicationPause, "OnApplicationPause", false));
                Assert.IsTrue(Flushes(ExportStages.OnApplicationPause, "OnApplicationPause", true));
                Assert.IsFalse(Flushes(ExportStages.OnApplicationPause, "OnApplicationQuit"));
                Assert.IsTrue(Flushes(ExportStages.OnApplicationQuit, "OnApplicationQuit"));
                Assert.IsFalse(Flushes(ExportStages.OnApplicationQuit, "OnApplicationPause", true));
                Assert.IsFalse(Flushes(0, "OnApplicationQuit"));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // Text 0x1b01720 and Markdown 0x1b01bf0 literals, per-type CSS rows from
        // .cctor 0x1b02130, and the Markdown trace edit: Replace("\n","\n+ ")
        // then Remove(Length - 3, 2) (add esi,-3; mov edx,2), which throws for
        // a trace shorter than three characters. Json 0x1b01a60 appends
        // JsonUtility.ToJson(message, true) and "\n" per message.
        // Mutations caught: changed separators, the Error style for Exception,
        // trimming the trace end differently, or compact JSON.
        [Test]
        public void TextMarkdownAndJsonFormatsUseTheNativeLiterals()
        {
            using (var owned = new Owned())
            {
                HLLogHandler handler = owned.Handler;
                var messages = new[]
                {
                    new HLLogMessage { Type = LogType.Log, Message = "hello" },
                    new HLLogMessage { Type = LogType.Error, Message = "bad", StackTrace = "at A\nat B\n" },
                    new HLLogMessage { Type = LogType.Warning, Message = "w", StackTrace = "" },
                    new HLLogMessage { Type = LogType.Exception, Message = "ex", StackTrace = "x\n" },
                    new HLLogMessage { Type = LogType.Assert, Message = "as", StackTrace = "abc" },
                };
                Messages(handler).AddRange(messages);
                WithSharedBuilder(() =>
                {
                    Assert.AreEqual(
                        "Log: hello\n" +
                        "Error: bad\nStack trace:\nat A\nat B\n\n" +
                        "Warning: w\n" +
                        "Exception: ex\nStack trace:\nx\n\n" +
                        "Assert: as\nStack trace:\nabc\n",
                        handler.LogGetInTextFormat());
                    Assert.AreEqual(
                        "**Log**: hello\n*****\n" +
                        "**<span style=\"color:red; font-weight:bold\">Error</span>**: bad\n\n**Stack trace**:\n+ at A\n+ at B \n*****\n" +
                        "**Warning**: w\n*****\n" +
                        "**<span style=\"color:darkcyan; font-weight:bold\">Exception</span>**: ex\n\n**Stack trace**:\n+ x \n*****\n" +
                        "**<span style=\"color:red; font-weight:bold\">Assert</span>**: as\n\n**Stack trace**:\n+ c\n*****\n",
                        handler.LogGetInMarkdownFormat());
                    string json = handler.LogGetInJsonFormat();
                    Assert.AreEqual(string.Concat(messages.Select(m => JsonUtility.ToJson(m, true) + "\n")), json);
                    Assert.AreNotEqual(string.Concat(messages.Select(m => JsonUtility.ToJson(m, false) + "\n")), json, "pretty printing is on");

                    handler.LogFlush();
                    Messages(handler).Add(new HLLogMessage { Type = LogType.Warning, Message = "w", StackTrace = "a\n" });
                    Assert.AreEqual("**<span style=\"color:orange; font-weight:bold\">Warning</span>**: w\n\n**Stack trace**:\n+ a \n*****\n", handler.LogGetInMarkdownFormat());
                    handler.LogFlush();
                    Messages(handler).Add(new HLLogMessage { Type = LogType.Log, Message = "l", StackTrace = "a\n" });
                    Assert.AreEqual("**<span style=\"font-weight:bold\">Log</span>**: l\n\n**Stack trace**:\n+ a \n*****\n", handler.LogGetInMarkdownFormat());
                    handler.LogFlush();
                    Messages(handler).Add(new HLLogMessage { Type = LogType.Log, Message = "l", StackTrace = "ab" });
                    Assert.Throws<ArgumentOutOfRangeException>(() => handler.LogGetInMarkdownFormat());
                    Assert.AreEqual("Log: l\nStack trace:\nab\n", handler.LogGetInTextFormat(), "the builder is reset before each format");
                    return 0;
                });
            }
            LogAssert.NoUnexpectedReceived();
        }

        // LogExport 0x1afb850: "<persistentDataPath>/<ExportPath>" is created,
        // then "/" + date('/'->'-') + "_" + RemoveWhitespace(time(':'->'-')) is
        // appended. OpString + appends in place (0x1a99ac0), so the Json and
        // Markdown names extend the Txt name. A catch(Exception) handler swallows
        // failures. Mutations caught: fresh names per extension, a missing
        // format mask, or letting a bad path throw.
        [Test]
        public void ExportWritesChainedExtensionNamesAndSwallowsFailures()
        {
            string root = "ProjectLucidHLLogHandlerTests-" + Guid.NewGuid().ToString("N");
            string rootPath = Application.persistentDataPath + "/" + root;
            try
            {
                using (var owned = new Owned())
                {
                    HLLogHandler handler = owned.Handler;
                    Messages(handler).Add(new HLLogMessage { Type = LogType.Error, Message = "bad", StackTrace = "at A\n" });
                    WithSharedBuilder(() =>
                    {
                        Set(handler, "m_currentSetting", Setting(format: ExportFormats.All, exportPath: root + "/all"));
                        handler.LogExport();
                        string[] files = Directory.GetFiles(rootPath + "/all").Select(file => Path.GetFileName(file)).OrderBy(name => name.Length).ToArray();
                        Assert.AreEqual(3, files.Length, string.Join(", ", files));
                        StringAssert.EndsWith(".txt", files[0]);
                        string stem = files[0].Substring(0, files[0].Length - ".txt".Length);
                        Assert.That(files, Is.EqualTo(new[] { stem + ".txt", stem + ".txt.json", stem + ".txt.json.md" }));
                        StringAssert.Contains("_", stem);
                        Assert.IsFalse(stem.Any(c => c == ':' || c == '/'), stem);
                        Assert.IsFalse(stem.Substring(stem.LastIndexOf('_')).Any(char.IsWhiteSpace), "the time part is RemoveWhitespace'd: " + stem);
                        string directory = rootPath + "/all/";
                        Assert.AreEqual(handler.LogGetInTextFormat(), File.ReadAllText(directory + files[0]));
                        Assert.AreEqual(handler.LogGetInJsonFormat(), File.ReadAllText(directory + files[1]));
                        Assert.AreEqual(handler.LogGetInMarkdownFormat(), File.ReadAllText(directory + files[2]));

                        Set(handler, "m_currentSetting", Setting(format: ExportFormats.Markdown, exportPath: root + "/md"));
                        handler.LogExport();
                        string[] markdown = Directory.GetFiles(rootPath + "/md").Select(file => Path.GetFileName(file)).ToArray();
                        Assert.AreEqual(1, markdown.Length);
                        StringAssert.EndsWith(".md", markdown[0]);
                        StringAssert.DoesNotContain(".txt", markdown[0]);

                        Set(handler, "m_currentSetting", Setting(format: 0, exportPath: root + "/none"));
                        handler.LogExport();
                        Assert.IsFalse(Directory.Exists(rootPath + "/none"), "no directory without a format");

                        Set(handler, "m_currentSetting", Setting(format: ExportFormats.Txt, exportPath: root + "/bad\0name"));
                        Assert.DoesNotThrow(() => handler.LogExport());
                        Assert.AreEqual(1, Messages(handler).Count, "export does not flush");
                        return 0;
                    });
                }
            }
            finally { if (Directory.Exists(rootPath)) Directory.Delete(rootPath, true); }
            LogAssert.NoUnexpectedReceived();
        }

        // OnEnable 0x1b00eb0 / OnDisable 0x1b01260 add and remove a LogCallback
        // for this instance on Application.logMessageReceivedThreaded.
        // Mutations caught: subscribing logMessageReceived or not removing.
        [Test]
        public void OnEnableReceivesThreadedLogMessagesUntilOnDisable()
        {
            using (var owned = new Owned())
            {
                HLLogHandler handler = owned.Handler;
                Set(handler, "m_currentSetting", Setting(types: SupportedLogTypes.Log));
                string first = "HLLogHandler fixture " + Guid.NewGuid().ToString("N");
                string second = "HLLogHandler fixture " + Guid.NewGuid().ToString("N");
                Call(handler, "OnEnable");
                try { Debug.Log(first); }
                finally { Call(handler, "OnDisable"); }
                Debug.Log(second);
                Call(handler, "LogQueueUpdate");
                Assert.That(handler.LogMessages.Where(m => m.Message == first).Select(Describe), Is.EqualTo(new[] { "3|" + first + "|<null>" }));
                Assert.IsFalse(handler.LogMessages.Any(m => m.Message == second));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // HLLogConsole .cctor 0x1b007a0 stores 0x3e; HLLogMessage.HasStackTrace
        // 0x1b011e0 is !IsNullOrEmpty; StringExtensions.RemoveWhitespace
        // 0x1ab4810 joins Split((string[])null, RemoveEmptyEntries) with "".
        // HLLogHandlerSettingScriptableObject.OnValidate 0x1b02370 only compares.
        [Test]
        public void DependenciesKeepTheirShippedBehaviour()
        {
            Assert.AreEqual(Everything, HLLogConsole.AllLogTypes);
            Assert.IsFalse(new HLLogMessage().HasStackTrace);
            Assert.IsFalse(new HLLogMessage { StackTrace = "" }.HasStackTrace);
            Assert.IsTrue(new HLLogMessage { StackTrace = " " }.HasStackTrace);
            Assert.AreEqual("abcde", " a b\tc\r\n d e ".RemoveWhitespace());
            Assert.AreEqual("a​b", "a​b".RemoveWhitespace(), "zero-width space is not whitespace");
            Assert.Throws<NullReferenceException>(() => ((string)null).RemoveWhitespace());

            var asset = ScriptableObject.CreateInstance<HLLogHandlerSettingScriptableObject>();
            try
            {
                Set(asset, "m_platformSettings", new[] { Setting("osx"), Setting("osx") });
                Assert.DoesNotThrow(() => Call(asset, "OnValidate"));
                Assert.AreEqual("osx", asset.GetPlatformSetting().Platform);
            }
            finally { Object.DestroyImmediate(asset); }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
