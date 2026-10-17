using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Behaviour of Hardlight.HLLogConsole (HLUnityCore.Runtime 0x060009ec..0x06000a27)
    // and HLLogConsoleScriptableObject (0x06000a28..0x06000a43) against the
    // shipped x86-64 bodies. Only state logic is exercised: IMGUI drawing needs
    // an OnGUI event and is not covered here. Each case owns its GameObject,
    // settings asset, skin and textures, and restores the shared
    // ConsoleLogMessage.s_lastIdStamp and HLLogConsole.AllLogTypes values.
    [TestFixture]
    public sealed class OriginalHLLogConsoleTests
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const SupportedLogTypes Everything = (SupportedLogTypes)0x3E;

        static readonly Type MessageType = typeof(HLLogConsole).GetNestedType("ConsoleLogMessage", BindingFlags.NonPublic);
        static readonly FieldInfo LastIdStamp = MessageType.GetField("s_lastIdStamp", Any);

        long savedIdStamp;
        SupportedLogTypes savedAllLogTypes;

        [SetUp]
        public void SaveSharedState()
        {
            savedIdStamp = (long)LastIdStamp.GetValue(null);
            savedAllLogTypes = HLLogConsole.AllLogTypes;
        }

        [TearDown]
        public void RestoreSharedState()
        {
            LastIdStamp.SetValue(null, savedIdStamp);
            HLLogConsole.AllLogTypes = savedAllLogTypes;
        }

        sealed class Owned : IDisposable
        {
            readonly List<Object> owned = new List<Object>();
            public readonly HLLogConsole Console;
            public readonly HLLogHandler Handler;
            public readonly HLLogConsoleScriptableObject Asset;
            public readonly GUISkin Skin;

            // RequireComponent adds the HLLogHandler; neither component is
            // awakened by AddComponent in edit mode.
            public Owned()
            {
                try
                {
                    var owner = new GameObject("HLLogConsole fixture") { hideFlags = HideFlags.HideAndDontSave };
                    owned.Add(owner);
                    Console = owner.AddComponent<HLLogConsole>();
                    Handler = owner.GetComponent<HLLogHandler>();
                    Asset = Own(ScriptableObject.CreateInstance<HLLogConsoleScriptableObject>());
                    Skin = Own(ScriptableObject.CreateInstance<GUISkin>());
                }
                catch { Dispose(); throw; }
            }

            public T Own<T>(T item) where T : Object { owned.Add(item); return item; }

            public Texture2D Icon(string name) => Own(new Texture2D(1, 1) { name = name, hideFlags = HideFlags.HideAndDontSave });

            // Assigns the asset and skin with every custom style the state logic
            // reads, then runs Awake (0x1af8be0) and OnEnable (0x1af9340).
            // customStyles is assigned first: every GUISkin setter calls
            // Apply, which logs "custom styles is null" while it is unset.
            public Owned Awaken(bool enable = true)
            {
                Skin.customStyles = new[]
                {
                    new GUIStyle { name = "headerlabel", fontSize = 20 },
                    new GUIStyle { name = "logmessage" },
                    new GUIStyle { name = "logmessagedarker" },
                    new GUIStyle { name = "logmessageselected" },
                    new GUIStyle { name = "importantmessagewindow" },
                };
                Skin.label = new GUIStyle { fontSize = 14 };
                Skin.window = new GUIStyle();
                Set(Asset, "m_consoleSkin", Skin);
                Set(Console, "m_logConsoleAsset", Asset);
                Call(Console, "Awake");
                if (enable) Call(Console, "OnEnable");
                return this;
            }

            public void Dispose()
            {
                for (int i = owned.Count - 1; i >= 0; i--)
                    if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            }
        }

        static object Get(object target, string name) => target.GetType().GetField(name, Any).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Any).SetValue(target, value);
        static object Prop(object target, string name) => target.GetType().GetProperty(name, Any).GetValue(target);

        static object Call(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, Any).Invoke(target, args); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        static object CallStatic(string name, params object[] args)
        {
            try { return typeof(HLLogConsole).GetMethod(name, Any).Invoke(null, args); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }

        static void Receive(HLLogConsole console, LogType type, string message) =>
            Call(console, "LogMessageReceived", new HLLogMessage { Type = type, Message = message });

        static IList TypeList(HLLogConsole console, LogType type) => (IList)((IDictionary)Get(console, "m_logMessagesPerType"))[type];
        static string Text(object consoleMessage) => ((HLLogMessage)Prop(consoleMessage, "LogMessage")).Message;
        static long Stamp(object consoleMessage) => (long)Prop(consoleMessage, "IdStamp");
        static int Times(object consoleMessage) => (int)Prop(consoleMessage, "NumberOfTimesLogged");
        static string[] Texts(IEnumerable consoleMessages) => consoleMessages.Cast<object>().Select(Text).ToArray();
        static string[] TypeTexts(HLLogConsole console, LogType type) => Texts(TypeList(console, type));
        static string[] Sorted(HLLogConsole console) => Texts(((IDictionary)Get(console, "m_currentSortedFilteredLogMessageList")).Values);
        static string[] Queued(HLLogConsole console) => Texts((IEnumerable)Get(console, "m_importantMessageQueue"));
        static int Capacity(object collection) => (int)collection.GetType().GetProperty("Capacity").GetValue(collection);

        // HLLogConsole .ctor 0x1b004b0: m_titleBarRect from the 16-byte constant
        // 0x29a8340 (0, 0, 10000, 20), both positions from 0x29a8350 (20, 20),
        // m_lastSelectedMessageIndex -1, and the three masks copied from the
        // current AllLogTypes. Settings .ctor 0x1b00a00: 10 pages, then
        // (100, 10, 100, 10) at 0x34..0x40, 30 at 0x44 and white colours.
        // Mutations caught: a 0x3e literal instead of reading AllLogTypes, the
        // pagination default swapped with the page minimum, or a missing colour.
        [Test]
        public void ConstructorsUseTheShippedInitialisers()
        {
            Assert.AreEqual(Everything, HLLogConsole.AllLogTypes, "fixture requires the shipped AllLogTypes value");
            using (var owned = new Owned())
            {
                HLLogConsole console = owned.Console;
                Assert.AreEqual(new Rect(0f, 0f, 10000f, 20f), Get(console, "m_titleBarRect"));
                Assert.AreEqual(20f, Get(console, "m_consolePositionX"));
                Assert.AreEqual(20f, Get(console, "m_consolePositionY"));
                Assert.AreEqual(-1, Get(console, "m_lastSelectedMessageIndex"));
                Assert.AreEqual(0, Get(console, "m_logPageElementCountLimit"));
                foreach (string mask in new[] { "m_logTypeToDisplay", "m_logTypeToExtractFromHandler", "m_temporaryLogTypeToExtractFromHandler" })
                    Assert.AreEqual(Everything, Get(console, mask), mask);
                Assert.AreEqual(0, ((ICollection)Get(console, "m_importantMessageQueue")).Count);
                Assert.AreEqual(0, ((ICollection)Get(console, "m_dynamicLogExclusionList")).Count);
                string[] contents = { "m_consoleWindowTitleContent", "m_exportButtonContent", "m_hideButtonContent", "m_settingButtonContent", "m_settingApplyFilterButtonContent", "m_settingPreviewContent", "m_genericContent" };
                Assert.AreEqual(contents.Length, contents.Select(name => Get(console, name)).Where(c => c != null).Distinct().Count());
                Assert.IsNull(Get(console, "m_logMessagesPerType"), "OnEnable creates the lists");
                Assert.IsNull(Get(console, "m_customStyles"), "GUIStyleInitialize creates the styles");
                Assert.AreEqual(false, Get(console, "m_initialized"));
                Assert.IsFalse(console.IsConsoleVisible);

                SettingsDefaultsMatch(owned.Asset);
            }

            HLLogConsole.AllLogTypes = SupportedLogTypes.Warning;
            using (var owned = new Owned())
                Assert.AreEqual(SupportedLogTypes.Warning, Get(owned.Console, "m_temporaryLogTypeToExtractFromHandler"), "the initialiser reads the static field");
            LogAssert.NoUnexpectedReceived();
        }

        static void SettingsDefaultsMatch(HLLogConsoleScriptableObject asset)
        {
            Assert.AreEqual(10, asset.LogPaginationMaxCount);
            Assert.AreEqual(100, asset.LogPageElementCountDefaultLimit);
            Assert.AreEqual(10, asset.LogPageElementMinCount);
            Assert.AreEqual(100, asset.LogPageElementMaxCount);
            Assert.AreEqual(10, asset.FontSizeMin);
            Assert.AreEqual(30, asset.FontSizeMax);
            Assert.AreEqual(new[] { Color.white, Color.white, Color.white, Color.white, Color.white },
                new[] { asset.ErrorTextColor, asset.WarningTextColor, asset.InfoTextColor, asset.AssertTextColor, asset.ExceptionTextColor });
            Assert.AreEqual((SupportedLogTypes)0, asset.ImportantLogTypesToDisplay);
            Assert.AreEqual((SupportedLogTypes)0, asset.DefaultLogTypesToDisplay);
            Assert.IsFalse(asset.ShowDetailedViewOnImportantLogType);
            Assert.IsFalse(asset.AlwaysOpenOnMostRecentPage);
            Assert.IsNull(asset.ConsoleSkin);
            Assert.IsNull(asset.InfoIcon);
        }

        // ConsoleConfigAndSkinCheck 0x1af8cd0 disables the component when the
        // asset or its skin is missing, and Awake 0x1af8be0 then returns before
        // clearing the event fields; OnEnable 0x1af9340 creates nothing.
        // Mutations caught: clearing the events first, checking only the asset,
        // or OnEnable building lists without a configuration.
        [Test]
        public void ConfigurationCheckDisablesWithoutAssetOrSkin()
        {
            using (var owned = new Owned())
            {
                HLLogConsole console = owned.Console;
                Action listener = () => { };
                console.OnConsoleOpen += listener;
                Call(console, "Awake");
                Assert.IsFalse(console.enabled, "no asset");
                Assert.AreSame(owned.Handler, Get(console, "m_logHolderComponent"), "GetComponent runs before the check");
                Assert.AreSame(listener, Get(console, "OnConsoleOpen"), "the failed check returns before the events are cleared");

                console.enabled = true;
                Set(console, "m_logConsoleAsset", owned.Asset);
                Call(console, "Awake");
                Assert.IsFalse(console.enabled, "asset without a skin");
                Assert.AreEqual(0, Get(console, "m_logPageElementCountLimit"));

                console.enabled = true;
                Call(console, "OnEnable");
                Assert.IsFalse(console.enabled);
                Assert.IsNull(Get(console, "m_logMessagesPerType"));
                Assert.IsNull(Get(owned.Handler, "OnLogMessageReceived"));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // Awake 0x1af8be0 with a valid asset copies the default page limit and
        // display mask, runs GUIStyleInitialize 0x1af8da0 and clears both event
        // fields. GUIStyleInitialize adds the icons Log, Warning, Error, Assert,
        // Exception (esi 3, 2, 0, 1, 4; insertion order only, since Dictionary
        // enumeration order is not observable behaviour), indexes the custom
        // styles by name, stores
        // the header/label font size difference and sets the button contents;
        // the apply-filter button takes the warning icon (0xa8) and the preview
        // the info icon (0xa0). Mutations caught: icon slots swapped, the
        // difference reversed, or the tooltip on a different content.
        [Test]
        public void AwakeCopiesAssetDefaultsAndInitialisesStylesAndContents()
        {
            using (var owned = new Owned())
            {
                HLLogConsoleScriptableObject asset = owned.Asset;
                string[] icons = { "m_logConsoleIcon", "m_infoIcon", "m_warningIcon", "m_errorIcon", "m_assertIcon", "m_exceptionIcon", "m_clearIcon", "m_settingIcon", "m_exportIcon", "m_showConsoleIcon", "m_hideConsoleIcon" };
                foreach (string icon in icons)
                    Set(asset, icon, owned.Icon(icon));
                Set(asset, "m_logPageElementCountDefaultLimit", 37);
                Set(asset, "m_defaultLogTypesToDisplay", SupportedLogTypes.Warning | SupportedLogTypes.Exception);
                HLLogConsole console = owned.Console;
                console.OnConsoleOpen += () => { };
                console.OnConsoleClose += () => { };
                owned.Awaken(enable: false);

                Assert.IsTrue(console.enabled);
                Assert.AreEqual(37, Get(console, "m_logPageElementCountLimit"));
                Assert.AreEqual(SupportedLogTypes.Warning | SupportedLogTypes.Exception, Get(console, "m_logTypeToDisplay"));
                Assert.AreEqual(Everything, Get(console, "m_logTypeToExtractFromHandler"), "only the display mask comes from the asset");
                Assert.IsNull(Get(console, "OnConsoleOpen"));
                Assert.IsNull(Get(console, "OnConsoleClose"));

                var iconPerType = (Dictionary<LogType, Texture2D>)Get(console, "m_logIconPerType");
                Assert.AreEqual(5, iconPerType.Count);
                Assert.AreSame(asset.InfoIcon, iconPerType[LogType.Log]);
                Assert.AreSame(asset.WarningIcon, iconPerType[LogType.Warning]);
                Assert.AreSame(asset.ErrorIcon, iconPerType[LogType.Error]);
                Assert.AreSame(asset.AssertIcon, iconPerType[LogType.Assert]);
                Assert.AreSame(asset.ExceptionIcon, iconPerType[LogType.Exception]);

                var styles = (Dictionary<string, GUIStyle>)Get(console, "m_customStyles");
                Assert.AreEqual(owned.Skin.customStyles.Length, styles.Count);
                foreach (GUIStyle style in owned.Skin.customStyles)
                    Assert.AreSame(style, styles[style.name], style.name);
                Assert.AreEqual(6, Get(console, "m_headerLabelFontSizeDifference"), "header 20 minus label 14");

                void Content(string field, string text, Texture2D image, string tooltip = "")
                {
                    var content = (GUIContent)Get(console, field);
                    Assert.AreEqual(text, content.text, field);
                    Assert.AreSame(image, content.image, field);
                    Assert.AreEqual(tooltip, content.tooltip, field);
                }
                Content("m_exportButtonContent", "Export log", asset.ExportIcon);
                Content("m_consoleWindowTitleContent", "HL Log Console", asset.LogConsoleIcon);
                Content("m_settingButtonContent", "Settings", asset.SettingIcon);
                Content("m_hideButtonContent", "Hide", asset.HideConsoleIcon);
                Content("m_settingApplyFilterButtonContent", "Apply filter", asset.WarningIcon,
                    "Only active log types will be processed by the console. Apply will flush all logs for disabled types.");
                Content("m_settingPreviewContent",
                    "Assembly for Assembly Definition File 'Assets/Plugins/HLNetworking/Tests/Editor/HLNetworking.Editor.Tests.asmdef' will not be compiled, because it has no scripts associated with it.",
                    asset.InfoIcon);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // OnEnable 0x1af9340 sizes five lists (Log, Warning, Error, Assert,
        // Exception) from GetEstimatedLogMessagesCount and the sorted list from
        // s_logTypeCount (Enum.GetValues(LogType).Length, 0x1af88e0) times it,
        // once; every enable subscribes again and OnDisable 0x1aff7e0 removes one.
        // Mutations caught: rebuilding on re-enable, a fixed capacity, or a
        // subscription guarded by m_initialized.
        [Test]
        public void OnEnableSizesListsOnceAndSubscribesOnEveryEnable()
        {
            using (var owned = new Owned())
            {
                var setting = new HLLogHandlerSetting();
                Set(setting, "m_maxLogSizeInMB", 3);
                Set(owned.Handler, "m_currentSetting", setting);
                Set(owned.Handler, "m_isAutoExportEnabled", true);
                Assert.AreEqual(3000, owned.Handler.GetEstimatedLogMessagesCount());
                HLLogConsole console = owned.Awaken().Console;

                Assert.AreEqual(5, typeof(HLLogConsole).GetProperty("s_logTypeCount", Any).GetValue(null));
                var perType = (IDictionary)Get(console, "m_logMessagesPerType");
                CollectionAssert.AreEquivalent(new[] { LogType.Log, LogType.Warning, LogType.Error, LogType.Assert, LogType.Exception }, perType.Keys.Cast<LogType>().ToArray());
                foreach (object list in perType.Values)
                    Assert.AreEqual(3000, Capacity(list));
                object sorted = Get(console, "m_currentSortedFilteredLogMessageList");
                Assert.AreEqual(15000, Capacity(sorted));
                Assert.AreEqual(true, Get(console, "m_initialized"));

                int Subscriptions() => ((Delegate)Get(owned.Handler, "OnLogMessageReceived"))?.GetInvocationList().Count(d => d.Target == (object)console) ?? 0;
                Assert.AreEqual(1, Subscriptions());
                Set(owned.Handler, "m_isAutoExportEnabled", false);
                Call(console, "OnEnable");
                Assert.AreSame(perType, Get(console, "m_logMessagesPerType"), "built once");
                Assert.AreSame(sorted, Get(console, "m_currentSortedFilteredLogMessageList"));
                Assert.AreEqual(2, Subscriptions());
                Call(console, "OnDisable");
                Assert.AreEqual(1, Subscriptions());
                Call(console, "OnDisable");
                Assert.AreEqual(0, Subscriptions());
            }
            LogAssert.NoUnexpectedReceived();
        }

        // LogMessageReceived 0x1affa10: the important mask (asset 0x20) creates
        // and queues the message when ShowDetailedViewOnImportantLogType is set
        // (0x24), else only latches m_hasReceivedImportantMessage; the extract
        // mask then lists it (creating it only if needed) and the display mask
        // adds it to the sorted list. Values outside the five LogTypes pass no
        // switch (jump tables 0x1b00068, 0x1b0007c), whatever the masks.
        // Mutations caught: creating the message before the masks (stamps
        // consumed by dropped messages), queueing only extracted messages, or
        // an all-bits mask accepting unmapped types.
        [Test]
        public void ReceivedMessagesRouteThroughImportantExtractAndDisplayMasks()
        {
            using (var owned = new Owned())
            {
                HLLogConsole console = owned.Awaken().Console;
                Set(owned.Asset, "m_importantLogTypesToDisplay", SupportedLogTypes.Error | SupportedLogTypes.Assert);
                Set(owned.Asset, "m_showDetailedViewOnImportantLogType", true);
                Set(console, "m_logTypeToExtractFromHandler", SupportedLogTypes.Log | SupportedLogTypes.Warning | SupportedLogTypes.Error);
                Set(console, "m_logTypeToDisplay", SupportedLogTypes.Log | SupportedLogTypes.Error);
                LastIdStamp.SetValue(null, 40L);

                Receive(console, LogType.Log, "a");
                Receive(console, LogType.Exception, "dropped");
                Receive(console, (LogType)7, "unmapped");
                Receive(console, LogType.Warning, "b");
                Receive(console, LogType.Error, "c");
                Receive(console, LogType.Assert, "d");

                Assert.AreEqual(new[] { "a" }, TypeTexts(console, LogType.Log));
                Assert.AreEqual(new[] { "b" }, TypeTexts(console, LogType.Warning));
                Assert.AreEqual(new[] { "c" }, TypeTexts(console, LogType.Error));
                Assert.IsEmpty(TypeTexts(console, LogType.Assert), "important but not extracted");
                Assert.IsEmpty(TypeTexts(console, LogType.Exception));
                Assert.AreEqual(new[] { "a", "c" }, Sorted(console));
                Assert.AreEqual(new[] { "c", "d" }, Queued(console));
                Assert.AreSame(TypeList(console, LogType.Error)[0], ((IEnumerable)Get(console, "m_importantMessageQueue")).Cast<object>().First(), "one instance");
                Assert.AreEqual(new[] { 41L, 42L, 43L }, new[] { LogType.Log, LogType.Warning, LogType.Error }.Select(t => Stamp(TypeList(console, t)[0])).ToArray());
                Assert.AreEqual(44L, LastIdStamp.GetValue(null), "only the four routed messages took a stamp");
                Assert.AreEqual(false, Get(console, "m_hasReceivedImportantMessage"));

                Set(owned.Asset, "m_importantLogTypesToDisplay", Everything);
                Set(console, "m_logTypeToExtractFromHandler", Everything);
                Set(console, "m_logTypeToDisplay", Everything);
                Receive(console, (LogType)5, "still unmapped");
                Assert.AreEqual(44L, LastIdStamp.GetValue(null));
                Assert.AreEqual(2, Queued(console).Length);

                Set(owned.Asset, "m_showDetailedViewOnImportantLogType", false);
                Receive(console, LogType.Warning, "e");
                Assert.AreEqual(true, Get(console, "m_hasReceivedImportantMessage"));
                Assert.AreEqual(new[] { "c", "d" }, Queued(console), "not queued without the detailed view");
                Assert.AreEqual(new[] { "b", "e" }, TypeTexts(console, LogType.Warning));
                Assert.AreEqual(new[] { "a", "c", "e" }, Sorted(console));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // LogMessageReceived 0x1affa10 compares the text with each dynamic
        // exclusion first and only bumps that entry; CollapseExcludedLogMessages
        // 0x1afbde0 counts the message once plus once per other copy removed
        // from its own type's list (IdStamp kept); IsExcludedLogMessage
        // 0x1b00110 matches text; GetNextImportantLogMessage 0x1b001d0 drops
        // excluded entries. Mutations caught: matching by type as well, removing
        // the message itself, or returning an excluded important message.
        [Test]
        public void DynamicExclusionsCountRepeatsAndCollapseOtherCopies()
        {
            using (var owned = new Owned())
            {
                HLLogConsole console = owned.Awaken().Console;
                Receive(console, LogType.Log, "x");
                Receive(console, LogType.Log, "y");
                Receive(console, LogType.Log, "x");
                Receive(console, LogType.Warning, "x");
                Receive(console, LogType.Log, "x");
                object first = TypeList(console, LogType.Log)[0];

                var exclusions = (IList)Get(console, "m_dynamicLogExclusionList");
                exclusions.Add(first);
                Call(console, "CollapseExcludedLogMessages", first);
                Assert.AreEqual(3, Times(first), "itself plus two removed copies");
                Assert.AreEqual(new[] { "x", "y" }, TypeTexts(console, LogType.Log));
                Assert.AreSame(first, TypeList(console, LogType.Log)[0]);
                Assert.AreEqual(new[] { "x" }, TypeTexts(console, LogType.Warning), "other types keep their copies");

                long stamp = (long)LastIdStamp.GetValue(null);
                Receive(console, LogType.Error, "x");
                Assert.AreEqual(4, Times(first));
                Assert.IsEmpty(TypeTexts(console, LogType.Error));
                Assert.AreEqual(stamp, LastIdStamp.GetValue(null));

                Assert.AreEqual(true, Call(console, "IsExcludedLogMessage", new HLLogMessage { Type = LogType.Exception, Message = "x" }));
                Assert.AreEqual(false, Call(console, "IsExcludedLogMessage", new HLLogMessage { Type = LogType.Log, Message = "y" }));

                Set(owned.Asset, "m_importantLogTypesToDisplay", SupportedLogTypes.Assert);
                Set(owned.Asset, "m_showDetailedViewOnImportantLogType", true);
                Receive(console, LogType.Assert, "p");
                Receive(console, LogType.Assert, "q");
                exclusions.Add(((IEnumerable)Get(console, "m_importantMessageQueue")).Cast<object>().First());
                Assert.AreEqual("q", Text(Call(console, "GetNextImportantLogMessage")));
                Assert.IsEmpty(Queued(console), "the excluded entry was dequeued and dropped");
                Assert.IsNull(Call(console, "GetNextImportantLogMessage"));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // SortedLogListUpdate 0x1afbef0 clears the selection and rebuilds from
        // the displayed types in id order; ClearLogTypes 0x1afd400 and
        // ClearAllLogTypes 0x1afc4c0 clear lists then rebuild; LogMessagePerTypeTrim
        // 0x1afef50 clears the types no longer extracted and rebuilds only when
        // it cleared one. Mutations caught: rebuilding in type order instead of
        // id order, clearing unflagged types, or an unconditional rebuild.
        [Test]
        public void SortedRebuildClearAndTrimFollowTheirMasks()
        {
            using (var owned = new Owned())
            {
                HLLogConsole console = owned.Awaken().Console;
                Receive(console, LogType.Error, "e1");
                Receive(console, LogType.Log, "l1");
                Receive(console, LogType.Warning, "w1");
                Receive(console, LogType.Error, "e2");
                Receive(console, LogType.Assert, "a1");

                Set(console, "m_logTypeToDisplay", SupportedLogTypes.Error | SupportedLogTypes.Log);
                Set(console, "m_lastSelectedMessageIndex", 3);
                Call(console, "SortedLogListUpdate");
                Assert.AreEqual(new[] { "e1", "l1", "e2" }, Sorted(console));
                Assert.AreEqual(-1, Get(console, "m_lastSelectedMessageIndex"));

                Set(console, "m_logTypeToDisplay", Everything);
                console.ClearLogTypes(SupportedLogTypes.Warning | SupportedLogTypes.Exception);
                Assert.IsEmpty(TypeTexts(console, LogType.Warning));
                Assert.AreEqual(new[] { "e1", "l1", "e2", "a1" }, Sorted(console));

                Set(console, "m_lastSelectedMessageIndex", 5);
                Call(console, "LogMessagePerTypeTrim");
                Assert.AreEqual(5, Get(console, "m_lastSelectedMessageIndex"), "nothing trimmed, no rebuild");
                Set(console, "m_logTypeToExtractFromHandler", SupportedLogTypes.Error);
                Call(console, "LogMessagePerTypeTrim");
                Assert.AreEqual(new[] { "e1", "e2" }, TypeTexts(console, LogType.Error));
                Assert.IsEmpty(TypeTexts(console, LogType.Log));
                Assert.IsEmpty(TypeTexts(console, LogType.Assert));
                Assert.AreEqual(new[] { "e1", "e2" }, Sorted(console));
                Assert.AreEqual(-1, Get(console, "m_lastSelectedMessageIndex"));

                console.ClearAllLogTypes();
                Assert.IsEmpty(TypeTexts(console, LogType.Error));
                Assert.IsEmpty(Sorted(console));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // ConsoleShow 0x1afbd30 sets visible, closes settings, keeps the detail
        // panel only for an important message and, with AlwaysOpenOnMostRecentPage
        // (0x30), opens the last page of GetTotalPageCount 0x1afdce0 (a partial
        // page counts) and its pagination index; ConsoleHide 0x1afb7c0 mirrors
        // it; both raise their event. An empty list opens page -1 in pagination
        // 0 (integer division). Mutations caught: rounding the page count down,
        // closing an important detail panel, or a missing event.
        [Test]
        public void ShowHideAndPageCountFollowTheShippedState()
        {
            using (var owned = new Owned())
            {
                HLLogConsole console = owned.Awaken().Console;
                Set(console, "m_logTypeToDisplay", Everything);
                Set(console, "m_logPageElementCountLimit", 10);
                Set(owned.Asset, "m_maxPageToDisplay", 2);
                Set(owned.Asset, "m_alwaysOpenOnMostRecentPage", true);
                int opened = 0, closed = 0;
                console.OnConsoleOpen += () => opened++;
                console.OnConsoleClose += () => closed++;

                Assert.AreEqual(0, Call(console, "GetTotalPageCount"));
                console.Action_ConsoleShow();
                Assert.AreEqual(-1, Get(console, "m_currentLogPageIndex"));
                Assert.AreEqual(0, Get(console, "m_currentPaginationIndex"));

                for (int i = 0; i < 10; i++) Receive(console, LogType.Log, "m" + i);
                Assert.AreEqual(1, Call(console, "GetTotalPageCount"));
                for (int i = 10; i < 21; i++) Receive(console, LogType.Log, "m" + i);
                Assert.AreEqual(3, Call(console, "GetTotalPageCount"));

                Set(console, "m_isSettingPanelOpen", true);
                Set(console, "m_isDetailsPanelOpen", true);
                console.Action_ConsoleShow();
                Assert.IsTrue(console.IsConsoleVisible);
                Assert.AreEqual(false, Get(console, "m_isSettingPanelOpen"));
                Assert.AreEqual(false, Get(console, "m_isDetailsPanelOpen"));
                Assert.AreEqual(2, Get(console, "m_currentLogPageIndex"));
                Assert.AreEqual(1, Get(console, "m_currentPaginationIndex"));
                Assert.AreEqual(2, opened);

                Set(console, "m_isLastMessageIsImportant", true);
                Set(console, "m_isDetailsPanelOpen", true);
                Set(console, "m_isSettingPanelOpen", true);
                console.Action_ConsoleToggle();
                Assert.IsFalse(console.IsConsoleVisible);
                Assert.AreEqual(false, Get(console, "m_isSettingPanelOpen"));
                Assert.AreEqual(true, Get(console, "m_isDetailsPanelOpen"), "an important message keeps its panel");
                Assert.AreEqual(1, closed);

                Set(owned.Asset, "m_alwaysOpenOnMostRecentPage", false);
                Set(console, "m_currentLogPageIndex", 0);
                console.Action_ConsoleToggle();
                Assert.IsTrue(console.IsConsoleVisible);
                Assert.AreEqual(true, Get(console, "m_isDetailsPanelOpen"));
                Assert.AreEqual(0, Get(console, "m_currentLogPageIndex"), "no page jump without the asset flag");
                Assert.AreEqual(3, opened);

                Set(console, "m_isLastMessageIsImportant", false);
                console.Action_ConsoleHide();
                Assert.AreEqual(false, Get(console, "m_isDetailsPanelOpen"));
                Assert.AreEqual(2, closed);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // ImportantMessageCheck 0x1af9a00 with a queued message and no open
        // panel caches the next important message, marks it important, takes
        // the "importantmessagewindow" style and shows the console; an open
        // panel blocks it; an all-excluded queue hides the panel but still
        // shows the console; without the detailed view a received important
        // message clears both flags (word store at 0x12a) and shows it.
        // The fallback branch reads GUI.skin and needs OnGUI, so it is not run.
        [Test]
        public void ImportantMessageCheckOpensTheNextImportantMessage()
        {
            using (var owned = new Owned())
            {
                HLLogConsole console = owned.Awaken().Console;
                Set(owned.Asset, "m_importantLogTypesToDisplay", SupportedLogTypes.Error);
                Set(owned.Asset, "m_showDetailedViewOnImportantLogType", true);
                int opened = 0;
                console.OnConsoleOpen += () => opened++;

                Receive(console, LogType.Error, "e1");
                Call(console, "ImportantMessageCheck");
                Assert.AreEqual("e1", Text(Get(console, "m_detailPanelCachedMessage")));
                Assert.AreEqual(true, Get(console, "m_isLastMessageIsImportant"));
                Assert.AreEqual(true, Get(console, "m_isDetailsPanelOpen"));
                Assert.AreSame(owned.Skin.customStyles[4], Get(console, "m_detailPanelCurrentStyle"));
                Assert.IsTrue(console.IsConsoleVisible);
                Assert.AreEqual(1, opened);

                Receive(console, LogType.Error, "e2");
                Call(console, "ImportantMessageCheck");
                Assert.AreEqual("e1", Text(Get(console, "m_detailPanelCachedMessage")), "an open panel waits");
                Assert.AreEqual(new[] { "e2" }, Queued(console));
                Assert.AreEqual(1, opened);

                ((IList)Get(console, "m_dynamicLogExclusionList")).Add(TypeList(console, LogType.Error)[1]);
                Set(console, "m_isDetailsPanelOpen", false);
                console.Action_ConsoleHide();
                Call(console, "ImportantMessageCheck");
                Assert.IsNull(Get(console, "m_detailPanelCachedMessage"));
                Assert.AreEqual(false, Get(console, "m_isDetailsPanelOpen"));
                Assert.IsTrue(console.IsConsoleVisible, "shown even though nothing was left");
                Assert.AreEqual(2, opened);

                Set(owned.Asset, "m_showDetailedViewOnImportantLogType", false);
                Receive(console, LogType.Error, "e3");
                Assert.AreEqual(true, Get(console, "m_hasReceivedImportantMessage"));
                Call(console, "ImportantMessageCheck");
                Assert.AreEqual(false, Get(console, "m_hasReceivedImportantMessage"));
                Assert.AreEqual(false, Get(console, "m_isLastMessageIsImportant"));
                Assert.AreEqual(3, opened);
            }
            LogAssert.NoUnexpectedReceived();
        }

        // ConsoleWindowRectCompute 0x1af9c00 insets the safe area by 2 *
        // WindowMargin (0x29a8330 = -40, -40) on the window style and rect, and
        // clamps each position with Mathf.Max(position, safe-area origin, 20)
        // (0x41a00000). Mutations caught: a single margin, or a lower clamp that
        // ignores WindowMargin. EditMode reports a safe-area origin of (0, 0), so
        // the safe-area term of the clamp is not covered here.
        [Test]
        public void WindowRectInsetsTheSafeAreaAndClampsThePosition()
        {
            using (var owned = new Owned())
            {
                HLLogConsole console = owned.Awaken().Console;
                Set(console, "m_consolePositionX", 5f);
                Set(console, "m_consolePositionY", 500f);
                Call(console, "ConsoleWindowRectCompute");
                Rect safe = Screen.safeArea;
                Assume.That(safe.x == 0f && safe.y == 0f, "the literal expectations assume a (0, 0) safe-area origin");
                Assert.AreEqual(new Rect(20f, 500f, safe.width - 40f, safe.height - 40f), Get(console, "m_consoleWindowRect"));
                Assert.AreEqual(safe.width - 40f, owned.Skin.window.fixedWidth);
                Assert.AreEqual(safe.height - 40f, owned.Skin.window.fixedHeight);
                Assert.AreEqual(safe.width, Get(console, "m_consoleWidth"));
                Assert.AreEqual(20f, Get(console, "m_consolePositionX"));
                Assert.AreEqual(500f, Get(console, "m_consolePositionY"));
            }
            LogAssert.NoUnexpectedReceived();
        }

        // DetailModeActionButtonGetText 0x1afbc40, LabelTextTruncate 0x1afb800
        // (0x3fff compare, Substring(0, 0x3ffe)), HasFlagValue 0x1afd020 and
        // EnableExclusionList 0x1b00340, which inlines the handler's loop.
        // Mutations caught: an off-by-one truncation limit, "Next" without the
        // queue count, or a console-side exclusion copy.
        [Test]
        public void HelpersUseTheNativeLiteralsAndLimits()
        {
            using (var owned = new Owned())
            {
                HLLogConsole console = owned.Awaken().Console;
                Assert.AreEqual("Exit", Call(console, "DetailModeActionButtonGetText", true));
                Assert.AreEqual("Back", Call(console, "DetailModeActionButtonGetText", false));
                Set(owned.Asset, "m_importantLogTypesToDisplay", SupportedLogTypes.Log);
                Set(owned.Asset, "m_showDetailedViewOnImportantLogType", true);
                Receive(console, LogType.Log, "i1");
                Receive(console, LogType.Log, "i2");
                Assert.AreEqual("<b>Next (2 important log remaining)</b>", Call(console, "DetailModeActionButtonGetText", true));

                var exclusion = new ExclusionList { Name = "n", Exclusions = new[] { "z" } };
                Set(owned.Handler, "m_exclusionLists", new[] { exclusion });
                console.EnableExclusionList("n", false);
                Assert.IsFalse(exclusion.IsExcluded("z"));
                console.EnableExclusionList("n", true);
                Assert.IsTrue(exclusion.IsExcluded("z"));
            }

            string limit = new string('a', 16382);
            Assert.AreSame(limit, CallStatic("LabelTextTruncate", limit));
            string longer = limit + "b";
            Assert.AreEqual(limit, CallStatic("LabelTextTruncate", longer));
            Assert.IsNull(CallStatic("LabelTextTruncate", new object[] { null }));
            Assert.AreEqual("", CallStatic("LabelTextTruncate", ""));
            Assert.AreEqual(true, CallStatic("HasFlagValue", SupportedLogTypes.Log | SupportedLogTypes.Error, SupportedLogTypes.Error | SupportedLogTypes.Assert));
            Assert.AreEqual(false, CallStatic("HasFlagValue", SupportedLogTypes.Log, SupportedLogTypes.Error));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
