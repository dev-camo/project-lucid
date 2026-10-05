using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using System.Text;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace ProjectLucid
{
    public static class LoggingHostVerification
    {
        private static int checks;
        private static void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        private static void Set(object value, string name, object fieldValue) => Field(value.GetType(), name).SetValue(value, fieldValue);
        private static Exception Capture(Action action)
        {
            try { action(); return null; } catch (Exception exception) { return exception; }
        }
        private static HLUnityCore CreateCLRFixture()
        {
            // A genuine original type with its native-derived callback field
            // fixture; no alternate host/provider/component types are created.
            // Engine construction/Awake/native initialization remain unrun here.
            var core = (HLUnityCore)FormatterServices.GetUninitializedObject(typeof(HLUnityCore));
            Set(core, "m_logErrorHandler", new FastAction<string, int>());
            Set(core, "m_logExceptionHandler", new FastAction<string, string, int>());
            Set(core, "m_devBreadcrumbHandler", new FastAction<string>());
            Set(core, "m_breadcrumbHandler", new FastAction<string>());
            Set(core, "m_customKeyStringHandler", new FastAction<string, string>());
            Set(core, "m_customKeyIntHandler", new FastAction<string, int>());
            Set(core, "m_customKeyFloatHandler", new FastAction<string, float>());
            Set(core, "m_customKeyBoolHandler", new FastAction<string, bool>());
            Set(core, "m_trackingAuthorisationHandler", new FastAction<int>());
            Set(core, "m_lastLogTime", float.MinValue);
            Set(core, "m_breadcrumbHandlersEnabled", true);
            Set(core, "m_customKeyHandlersEnabled", true);
            return core;
        }
        private sealed class Message : object
        {
            public int Reads;
            public Func<string> Value;
            public override string ToString() { Reads++; return Value(); }
        }

        public static void Run()
        {
            int managed = RunManaged();
            int engine = RunEngine();
            Console.WriteLine("PASS original logging host managed checks=" + managed + "; actual engine manual-lifecycle checks=" + engine);
        }

        // This bounded Editor helper creates the genuine original component and
        // assets. It invokes native-derived Awake/OnDestroy explicitly on an
        // inactive object; the separate live helper verifies Unity dispatch.
        // No scene/catalog/authored App boot or studio native parity is implied.
        public static int RunEngine()
        {
            checks = 0;
            Check(HLUnityCore.IsNull() && ProcessManager.IsSystemNull<HLUnityCore>(), "engine proof starts without a live original host");
            var config = UnityEngine.ScriptableObject.CreateInstance<HLUnityCoreConfigurationAsset>();
            Set(config.LowMemoryConfig, "m_enabled", false);
            Set(config.LowMemoryConfig, "m_triggerResourcesUnloadAssets", false);
            StackableDataHandle handle = null;
            UnityEngine.GameObject gameObject = null;
            HLUnityCore core = null;
            Type type = typeof(HLUnityCore);
            object previousBridge = Field(type, "s_unityCoreNativeBridge").GetValue(null);
            object previousTracking = Field(type, "s_trackingAuthorisationNativeBridge").GetValue(null);
            bool priorConfiguration = !ProcessManager.IsSystemNull<SystemConfiguration>();
            SystemConfiguration createdConfiguration = null;
            UnityEngine.Application.LogCallback callback = null;
            bool awake = false;
            try
            {
                // Original runtime configuration enumeration registers the base asset type.
                handle = SystemConfiguration.AddConfig<SystemConfigurationAsset>(config);
                if (!priorConfiguration) createdConfiguration = ProcessManager.GetSystem<SystemConfiguration>();
                Check(ReferenceEquals(SystemConfiguration.GetConfig<HLUnityCoreConfigurationAsset>(), config), "actual original configuration override supplies genuine config");
                gameObject = new UnityEngine.GameObject("Lucid original logging manual lifecycle proof");
                gameObject.SetActive(false);
                core = gameObject.AddComponent<HLUnityCore>();
                Check(core != null && ReferenceEquals(core.gameObject, gameObject), "actual engine creates original component on real GameObject");
                foreach (string name in new[] { "m_logErrorHandler", "m_logExceptionHandler", "m_devBreadcrumbHandler", "m_breadcrumbHandler", "m_customKeyStringHandler", "m_customKeyIntHandler", "m_customKeyFloatHandler", "m_customKeyBoolHandler", "m_trackingAuthorisationHandler" })
                    Check(Field(type, name).GetValue(core) != null, "actual original component constructor initializes " + name);
                Check((float)Field(type, "m_lastLogTime").GetValue(core) == float.MinValue && (bool)Field(type, "m_breadcrumbHandlersEnabled").GetValue(core) && (bool)Field(type, "m_customKeyHandlersEnabled").GetValue(core), "actual native-derived constructor defaults");
                Check(Field(type, "m_unityCoreConfigurationAsset").GetValue(core) == null, "constructor leaves configuration null until lifecycle");
                Check(HLUnityCore.IsNull() && ProcessManager.IsSystemNull<HLUnityCore>(), "inactive component has not been registered by this manual Editor fixture");
                Field(type, "s_unityCoreNativeBridge").SetValue(null, null);
                Field(type, "s_trackingAuthorisationNativeBridge").SetValue(null, null);
                MethodInfo onLog = type.GetMethod("OnHandleLog", BindingFlags.NonPublic | BindingFlags.Instance);
                callback = (UnityEngine.Application.LogCallback)Delegate.CreateDelegate(typeof(UnityEngine.Application.LogCallback), core, onLog);
                awake = true;
                type.GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(core, null);
                Check(ReferenceEquals(HLUnityCore.Instance, core) && ReferenceEquals(ProcessManager.GetSystem<HLUnityCore>(), core), "original base Awake registers real host and singleton");
                Check(ReferenceEquals(Field(type, "m_unityCoreConfigurationAsset").GetValue(core), config), "original Awake resolves genuine configuration override");
                object bridge = Field(type, "s_unityCoreNativeBridge").GetValue(null);
                object tracking = Field(type, "s_trackingAuthorisationNativeBridge").GetValue(null);
                Check(bridge != null && bridge.GetType() == typeof(HLUnityCoreNativeBridge), "original EnsureInitialised creates actual original native bridge type");
                Check(tracking != null && tracking.GetType() == typeof(HLTrackingAuthorisationNativeBridgeStub), "original retail factory creates actual original tracking stub type");
                type.GetMethod("EnsureInitialised", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(core, null);
                Check(ReferenceEquals(Field(type, "s_unityCoreNativeBridge").GetValue(null), bridge) && ReferenceEquals(Field(type, "s_trackingAuthorisationNativeBridge").GetValue(null), tracking), "original existing bridge prevents reinitialization/replacement");
                Check(HLUnityCore.GetDeviceOS() == UnityEngine.SystemInfo.operatingSystem, "actual original OS route uses Unity SystemInfo");
                Check(HLUnityCore.GetDeviceID() == string.Empty && HLUnityCore.GetScreenUnusableHeaderHeightInPixels() == 0 && HLUnityCore.GetScreenUnusableFooterHeightInPixels() == 0 && !HLUnityCore.DoesThisDeviceHaveANotch(), "original retail Mac constants, separate from native adapters");
                Check(HLUnityCore.CalculateApplicationChecksum("lucid-engine-proof") == HLCRC32.GenerateInt("lucid-engine-proof"), "original native Mac checksum0 combines with genuine CRC");
                var safe = UnityEngine.Screen.safeArea;
                Check(HLUnityCore.DoesThisDeviceHaveATopNotch() == (safe.y > 0f), "actual native-named top notch uses y directly");
                Check(HLUnityCore.DoesThisDeviceHaveABottomNotch() == (safe.yMax < UnityEngine.Screen.height), "actual native-named bottom notch uses yMax boundary");
                Check(HLUnityCore.DoesThisDeviceHaveALeftNotch() == (safe.x > 0f) && HLUnityCore.DoesThisDeviceHaveARightNotch() == (safe.xMax < UnityEngine.Screen.width), "actual side notch screen comparisons");
                Check(HLUnityCore.DoesThisDeviceHaveASafeArea() == (safe.y > 0f || safe.yMax < UnityEngine.Screen.height || safe.x > 0f || safe.xMax < UnityEngine.Screen.width), "actual safe area combines all four boundaries");
                Check(HLUnityCore.GetBottomSafeAreaInsetInPixels() == (int)safe.yMax && HLUnityCore.GetTopSafeAreaInsetInPixels() == (int)safe.y, "actual original default interface methods use yMax/y, without screen-height subtraction");
                Check(!HLUnityCore.CanDeviceRequestTrackingAuthorisation() && HLUnityCore.RequestTrackingAuthorisationStatus() == -1, "original retail tracking constants through real interface");
                var errors = new List<string>();
                var exceptions = new List<string>();
                var breadcrumbs = new List<string>();
                core.AddLogErrorHandler((text, code) => errors.Add(text + ":" + code));
                core.AddLogExceptionHandler((text, stack, code) => exceptions.Add(text + ":" + code));
                core.AddBreadcrumbHandler(breadcrumbs.Add);
                HLOutput.LogError("engine-route");
                Check(errors.SequenceEqual(new[] { "engine-route:0" }), "actual original registry delivers HLOutput error callback");
                errors.Clear();
                HLUnityCore.RequestTrackingAuthorisation();
                Check(errors.SequenceEqual(new[] { "This device does not support tracking authorisation requests:0" }), "original tracking diagnostic now traverses genuine registered host");
                Set(config, "m_logInPlaceOfException", true);
                HLUnityCore.LogOrThrowException("handled");
                Check(exceptions.SequenceEqual(new[] { "handled:0" }), "configured original exception path reports callback instead of throwing");
                errors.Clear(); exceptions.Clear();
                var supplied = new InvalidOperationException("supplied");
                HLUnityCore.OnHandleFirstChanceException(null, new FirstChanceExceptionEventArgs(supplied));
                Check(exceptions.SequenceEqual(new[] { "supplied:" + supplied.HResult }) && errors.Single() == "First Chance Exception " + supplied + ":0", "positive original first-chance route forwards HResult then formatted error");
                errors.Clear(); exceptions.Clear();
                HLUnityCore.OnHandleUnresolvedException(null, new UnhandledExceptionEventArgs(supplied, false));
                Check(exceptions.SequenceEqual(new[] { "supplied:" + supplied.HResult }) && errors.Single() == "Unhandled Exception " + supplied + ":0", "positive original unhandled route forwards HResult then formatted error");
                Set(config.LowMemoryConfig, "m_breadcrumbMessage", "engine-memory");
                Set(config.LowMemoryConfig, "m_minimumLogIntervalSeconds", 0f);
                type.GetMethod("OnLowMemory", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(core, null);
                Check(breadcrumbs.SequenceEqual(new[] { "engine-memory" }), "actual direct low-memory callback transports configured breadcrumb despite disabled registration");
                Check((float)Field(type, "m_lastLogTime").GetValue(core) == UnityEngine.Time.unscaledTime, "original low-memory callback records actual unscaled time");
                Set(config.LowMemoryConfig, "m_minimumLogIntervalSeconds", float.MaxValue);
                type.GetMethod("OnLowMemory", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(core, null);
                Check(breadcrumbs.Count == 1, "native interval gate suppresses immediate repeated breadcrumb");
                Set(config.LowMemoryConfig, "m_breadcrumbMessage", string.Empty);
                Set(core, "m_lastLogTime", float.MinValue);
                type.GetMethod("OnLowMemory", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(core, null);
                Check(breadcrumbs.Count == 1 && (float)Field(type, "m_lastLogTime").GetValue(core) == float.MinValue, "empty message does not change last-log state");
                type.GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(core, null);
                awake = false;
                Check(HLUnityCore.IsNull() && ProcessManager.IsSystemNull<HLUnityCore>(), "original explicit OnDestroy unregisters actual singleton/registry");
                Check(ReferenceEquals(Field(type, "s_unityCoreNativeBridge").GetValue(null), bridge) && ReferenceEquals(Field(type, "s_trackingAuthorisationNativeBridge").GetValue(null), tracking), "original destruction retains both static bridges");
                UnityEngine.Object.DestroyImmediate(gameObject);
                Check(core == null, "actual Unity destruction invalidates original component wrapper");
            }
            finally
            {
                if (awake && core != null) type.GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(core, null);
                if (callback != null) UnityEngine.Application.logMessageReceived -= callback; // Probe cleanup; original OnDestroy deliberately does not do this.
                if (gameObject != null) UnityEngine.Object.DestroyImmediate(gameObject);
                Field(type, "s_unityCoreNativeBridge").SetValue(null, previousBridge);
                Field(type, "s_trackingAuthorisationNativeBridge").SetValue(null, previousTracking);
                if (handle != null) ProcessManager.GetSystem<SystemConfiguration>().StackableData.RemoveOverrides(handle);
                if (createdConfiguration != null) ProcessManager.UnregisterSystem(createdConfiguration);
                if (config != null) UnityEngine.Object.DestroyImmediate(config);
            }
            return checks;
        }

        // This portion uses complete genuine original Core/registry/callback
        // types. It does not construct engine components, run lifecycle hooks,
        // query engine safe-area/OS properties, or exercise studio native parity.
        public static int RunManaged()
        {
            checks = 0;
            CheckMetadata();
            CheckTransports();
            CheckRegisteredRoutes();
            CheckNativeValueMethods();
            return checks;
        }

        private static void CheckMetadata()
        {
            Type host = typeof(HLUnityCore);
            Check(host.Assembly.GetName().Name == "HLUnityCore.Runtime" && host.FullName == "Hardlight.HLUnityCore", "original host identity");
            Check(host.IsPublic && !host.IsAbstract && !host.IsSealed && (host.Attributes & TypeAttributes.BeforeFieldInit) != 0, "original host class visibility/flags");
            Check(host.BaseType == typeof(MonoSingleton<HLUnityCore>), "genuine original singleton base identity");
            FieldInfo[] fields = host.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
            string[] names = { "s_unityCoreNativeBridge", "s_trackingAuthorisationNativeBridge", "m_logErrorHandler", "m_logExceptionHandler", "m_devBreadcrumbHandler", "m_breadcrumbHandler", "m_customKeyStringHandler", "m_customKeyIntHandler", "m_customKeyFloatHandler", "m_customKeyBoolHandler", "m_trackingAuthorisationHandler", "m_unityCoreConfigurationAsset", "m_lastLogTime", "m_breadcrumbHandlersEnabled", "m_customKeyHandlersEnabled" };
            Check(fields.Select(f => f.Name).SequenceEqual(names), "all15 original fields/order, no additions");
            for (int i = 0; i < fields.Length; i++)
            {
                Check(fields[i].IsPrivate && fields[i].IsStatic == (i < 2) && !fields[i].IsInitOnly, "original field visibility/static/readonly flags " + fields[i].Name);
                Check(!fields[i].IsDefined(typeof(UnityEngine.SerializeField), false), "original host field has no added serialization attribute");
            }
            Check(fields[0].FieldType == typeof(IHLUnityCoreNativeBridge) && fields[1].FieldType == typeof(IHLTrackingAuthorisationNativeBridge), "real native bridge interfaces");
            Check(fields[2].FieldType == typeof(FastAction<string, int>) && fields[3].FieldType == typeof(FastAction<string, string, int>) && fields[11].FieldType == typeof(HLUnityCoreConfigurationAsset), "real original callback/config field graph");
            Check(host.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 62 && host.GetConstructors().Length == 1, "all63 original host methods including ctor");
            Check(typeof(HLOutput).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 6, "all six original output fields");
            FieldInfo[] outputFields = typeof(HLOutput).GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(field => field.MetadataToken).ToArray();
            Check(outputFields.Select(field => field.Name).SequenceEqual(new[] { "s_hlOutputPlugin", "s_bugInfoProvider", "s_specificBugInfoProvider", "s_stringBuilderPool", "s_errorScopedStringBuilderDisposeCallback", "s_quitting" }) && outputFields.Take(5).All(field => field.Attributes == (FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly)) && outputFields[5].Attributes == (FieldAttributes.Private | FieldAttributes.Static), "all original output fields/order/readonly flags");
            Check(typeof(HLOutput).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 29 && typeof(HLOutput).TypeInitializer != null, "all30 original output methods including cctor");
            Check(typeof(HLUnityCoreNativeBridge).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 0, "original zero-field native bridge");
            Check(typeof(HLUnityCoreNativeBridge).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 18, "all original bridge method signatures including explicit import adaptations");
            Check(typeof(IHLUnityCoreNativeBridge).GetMethods().Count(m => m.IsAbstract) == 13 && typeof(IHLUnityCoreNativeBridge).GetMethods().Count(m => !m.IsAbstract) == 2, "original13 contracts/two default interface bodies");
            Check(typeof(IHLTrackingAuthorisationNativeBridge).GetMethods().Length == 3 && typeof(IHLTrackingAuthorisationNativeBridge).GetMethods().All(m => m.IsAbstract), "three original tracking contracts have no body credit");
            Check(typeof(HLOutputPlugin).IsAbstract && typeof(HLOutputPlugin).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).All(m => m.IsAbstract), "original native logging abstract contracts");
            Check(typeof(HLOutputPluginMacOS).BaseType == typeof(HLOutputPlugin) && typeof(HLOutputPluginUnsupported).BaseType == typeof(HLOutputPlugin), "genuine original platform plugin hierarchy");
            CheckOptions(host, Option.ArrayBoundsChecks, Option.NullChecks);
            CheckOptions(typeof(HLOutput), Option.ArrayBoundsChecks, Option.NullChecks);
            CheckOptions(typeof(HLUnityCoreNativeBridge), Option.NullChecks, Option.ArrayBoundsChecks);
            CheckOptions(typeof(HLOutputPlugin), Option.ArrayBoundsChecks, Option.NullChecks);
            CheckOptions(typeof(HLOutputPluginMacOS), Option.ArrayBoundsChecks, Option.NullChecks);
            CheckOptions(typeof(HLOutputPluginUnsupported), Option.NullChecks, Option.ArrayBoundsChecks);
            Check((typeof(HLOutput).Attributes & TypeAttributes.BeforeFieldInit) == 0, "original explicit output initializer is not BeforeFieldInit");
            Check(host.GetMethod("OnHandleUnresolvedException").IsDefined(typeof(HandleProcessCorruptedStateExceptionsAttribute), false), "original corrupted-state exception handler attribute");
            MethodInfo runtimeLoad = typeof(HLOutput).GetMethod("OnRuntimeMethodLoad", BindingFlags.NonPublic | BindingFlags.Static);
            var runtimeAttribute = runtimeLoad.GetCustomAttribute<UnityEngine.RuntimeInitializeOnLoadMethodAttribute>();
            Check(runtimeAttribute != null && runtimeAttribute.loadType == UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad, "original parameterless runtime initialization attribute/default load stage");
            MethodInfo[] developmentMethods = typeof(HLOutput).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => method.Name == "Log" || method.Name == "LogFormat" || method.Name == "LogWarning").ToArray();
            Check(developmentMethods.Length == 10 && developmentMethods.All(method => method.GetCustomAttributes<System.Diagnostics.ConditionalAttribute>().Single().ConditionString == "BUILD_DEVELOPMENT"), "all ten original development conditional attributes");
            foreach (Type nativeType in new[] { typeof(HLUnityCoreNativeBridge), typeof(HLOutputPluginMacOS) })
            {
                MethodInfo[] imports = nativeType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
                Check(imports.Length == (nativeType == typeof(HLUnityCoreNativeBridge) ? 5 : 2), "seven original import signatures represented explicitly");
                Check(imports.All(method => (method.GetMethodImplementationFlags() & MethodImplAttributes.PreserveSig) != 0 && (method.Attributes & MethodAttributes.PinvokeImpl) == 0 && method.GetMethodBody() != null), "explicit adapter retains PreserveSig but deliberately removes original P/Invoke flag");
            }
        }

        private static void CheckOptions(Type type, params Option[] options)
        {
            var attributes = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
            Check(attributes.Select(attribute => attribute.Option).SequenceEqual(options) && attributes.All(attribute => Equals(attribute.Value, false)), "exact original IL2CPP type options/order " + type.FullName);
        }

        private static void CheckTransports()
        {
            var core = CreateCLRFixture();
            List<string> trace = new List<string>();
            Action<string, int> error = (text, code) => trace.Add(text + ":" + code);
            core.AddLogErrorHandler(error); core.AddLogErrorHandler(error);
            core.LogError(null, -7);
            Check(string.Join(",", trace) == ":-7,:-7", "error callback duplicates and null text preserved");
            core.RemoveLogErrorHandler(error); trace.Clear(); core.LogError("message");
            Check(string.Join(",", trace) == "message:0", "error removal removes one duplicate/default code zero");
            core.RemoveLogErrorHandler(null); trace.Clear(); core.LogError("discarded", 5);
            Check(trace.Count == 0 && Field(typeof(HLUnityCore), "m_logErrorHandler").GetValue(core) == null, "original null removal clears field via subtraction operator");
            core.AddLogErrorHandler(error); core.LogError("created", 3);
            Check(trace.Single() == "created:3", "subsequent add creates genuine callback receiver");

            Action<string, string, int> exception = (message, stack, code) => trace.Add(message + "|" + stack + "|" + code);
            core.AddLogExceptionHandler(exception); core.AddLogExceptionHandler(exception);
            trace.Clear(); core.LogException(null, null, int.MinValue);
            Check(trace.SequenceEqual(new[] { "||-2147483648", "||-2147483648" }), "exception three arguments/duplicates transport unchanged");
            core.RemoveLogExceptionHandler(exception); trace.Clear(); core.LogException("error", "stack");
            Check(trace.Single() == "error|stack|0", "exception default code/removal");
            core.RemoveLogExceptionHandler(null); trace.Clear(); core.LogException("ignored", "ignored");
            Check(trace.Count == 0, "exception null removal preserves original null field semantics");
            core.AddLogExceptionHandler(exception);
            var onLog = typeof(HLUnityCore).GetMethod("OnHandleLog", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (UnityEngine.LogType type in Enum.GetValues(typeof(UnityEngine.LogType)))
            {
                trace.Clear(); onLog.Invoke(core, new object[] { "log", "stack", type });
                Check(trace.Count == (type == UnityEngine.LogType.Exception ? 1 : 0), "only Unity exception type dispatches callback: " + type);
            }

            Action<string> bread = message => trace.Add("b:" + message);
            Action<string> dev = message => trace.Add("d:" + message);
            core.AddBreadcrumbHandler(bread); core.AddDevBreadcrumbHandler(dev);
            trace.Clear(); core.Breadcrumb(null); core.DevBreadcrumb("dev");
            Check(trace.SequenceEqual(new[] { "b:", "d:dev" }), "both original breadcrumb transports/default-enabled fields");
            core.ToggleBreadcrumbHandlers(); trace.Clear(); core.Breadcrumb("skip"); core.DevBreadcrumb("skip");
            Check(trace.Count == 0, "no-argument breadcrumb toggle disables both fields");
            core.ToggleBreadcrumbHandlers(true); core.RemoveBreadcrumbHandler(bread); core.DevBreadcrumb("kept");
            Check(trace.Single() == "d:kept", "explicit toggle and independent handler removal");
            core.RemoveDevBreadcrumbHandler(dev); trace.Clear(); core.DevBreadcrumb("removed");
            Check(trace.Count == 0, "development breadcrumb remove route");

            Action<string, string> str = (key, value) => trace.Add("s:" + key + ":" + value);
            Action<string, int> integer = (key, value) => trace.Add("i:" + key + ":" + value);
            Action<string, float> real = (key, value) => { Check(key == null && float.IsNaN(value), "float callback retains native argument values"); trace.Add("f"); };
            Action<string, bool> boolean = (key, value) => trace.Add("b:" + key + ":" + value);
            core.AddCustomKeyStringHandler(str); core.AddCustomKeyIntHandler(integer); core.AddCustomKeyFloatHandler(real); core.AddCustomKeyBoolHandler(boolean);
            core.CustomKeyString(null, null); core.CustomKeyInt("k", -4); core.CustomKeyFloat(null, float.NaN); core.CustomKeyBool("k", true);
            Check(trace.SequenceEqual(new[] { "s::", "i:k:-4", "f", "b:k:True" }), "all four original typed custom-key channels/order");
            core.ToggleCustomKeyHandler(); trace.Clear(); core.CustomKeyString("", ""); core.CustomKeyInt("", 0); core.CustomKeyFloat(null, float.NaN); core.CustomKeyBool("", false);
            Check(trace.Count == 0, "custom-key no-argument toggle gates all four channels");
            core.ToggleCustomKeyHandler(true); core.RemoveCustomKeyStringHandler(str); core.RemoveCustomKeyIntHandler(integer); core.RemoveCustomKeyFloatHandler(real); core.RemoveCustomKeyBoolHandler(boolean);
            core.CustomKeyString("", ""); core.CustomKeyInt("", 0); core.CustomKeyFloat(null, float.NaN); core.CustomKeyBool("", false);
            Check(trace.Count == 0, "all four genuine removal routes");

            List<int> statuses = new List<int>(); Action<int> status = value => statuses.Add(value);
            core.AddTrackingAuthorisationHandler(status);
            foreach (string input in new[] { "-1", " 12 ", null, "invalid", "2147483648" }) core.Native_TrackingAuthorisationStatus(input);
            Check(statuses.SequenceEqual(new[] { -1, 12, 0, 0, 0 }), "native tracking callback ignores parse success and forwards out value");
            core.RemoveTrackingAuthorisationHandler(status); core.Native_TrackingAuthorisationStatus("3");
            Check(statuses.Count == 5, "tracking callback removal");
        }

        private static void CheckRegisteredRoutes()
        {
            const string name = "Hardlight.HLUnityCore";
            IDictionary registry = (IDictionary)Field(typeof(ProcessManager), "s_systemDictionary").GetValue(null);
            bool existed = registry.Contains(name); object prior = existed ? registry[name] : null;
            Check(ProcessManager.IsSystemNull<HLUnityCore>(), "isolated proof requires no live original registered host");
            var core = CreateCLRFixture(); var replacement = CreateCLRFixture();
            List<string> trace = new List<string>();
            List<Func<string>> global = (List<Func<string>>)Field(typeof(HLOutput), "s_bugInfoProvider").GetValue(null);
            Dictionary<BugInfo, List<Func<string>>> specific = (Dictionary<BugInfo, List<Func<string>>>)Field(typeof(HLOutput), "s_specificBugInfoProvider").GetValue(null);
            Stack<StringBuilder> pool = (Stack<StringBuilder>)Field(typeof(HLOutput), "s_stringBuilderPool").GetValue(null);
            Func<string>[] oldGlobal = global.ToArray(); var oldSpecific = specific.ToArray(); StringBuilder[] oldPool = pool.ToArray();
            bool oldQuitting = (bool)Field(typeof(HLOutput), "s_quitting").GetValue(null);
            try
            {
                global.Clear(); specific.Clear(); pool.Clear(); Field(typeof(HLOutput), "s_quitting").SetValue(null, false);
                var message = new Message { Value = () => "text" };
                HLOutput.LogError(message); HLOutput.LogError(null);
                Check(message.Reads == 0, "unregistered error route does not evaluate message/null");
                ProcessManager.RegisterSystem(core);
                core.AddLogErrorHandler((text, code) => trace.Add("old:" + text + ":" + code));
                replacement.AddLogErrorHandler((text, code) => trace.Add("new:" + text + ":" + code));
                HLOutput.LogError(message);
                Check(message.Reads == 1 && trace.Single() == "old:text:0", "genuine registry routes registered error through original host/callback");
                trace.Clear(); HLOutput.LogError(false, message);
                Check(message.Reads == 1 && trace.Count == 0, "false condition avoids message evaluation");
                HLOutput.LogError(true, message);
                Check(message.Reads == 2 && trace.Single() == "old:text:0", "true condition forwards registered route");
                Check(Capture(() => HLOutput.LogError(null)) is NullReferenceException, "registered null message throws after registry lookup");
                message.Value = () => { ProcessManager.UnregisterSystem(core); ProcessManager.RegisterSystem(replacement); return "changed"; };
                trace.Clear(); HLOutput.LogError(message);
                Check(trace.Single() == "old:changed:0" && ReferenceEquals(ProcessManager.GetSystem<HLUnityCore>(), replacement), "host retrieval precedes side-effecting virtual message conversion");
                ProcessManager.UnregisterSystem(replacement); ProcessManager.RegisterSystem(core);
                Action<string> breadcrumb = text => trace.Add("bread:" + text); core.AddBreadcrumbHandler(breadcrumb); core.AddDevBreadcrumbHandler(text => trace.Add("dev:" + text));
                trace.Clear(); HLOutput.Breadcrumb(false, "skip"); HLOutput.DevBreadcrumb(false, "skip"); HLOutput.Breadcrumb(true, null); HLOutput.DevBreadcrumb(true, "present");
                Check(trace.SequenceEqual(new[] { "bread:", "dev:present" }), "registered breadcrumb conditions and original separate channels");
                var key = new BugInfo("key", "display");
                Func<string> provider = () => "global"; Func<string> child = () => "specific";
                HLOutput.AddBugInfoProvider(provider); HLOutput.AddBugInfoProvider(provider); HLOutput.AddBugInfoProvider(key, child);
                Check(global.Count == 2 && specific[key].Count == 1, "bug providers retain duplicates and actual keyed list");
                trace.Clear(); HLOutput.LogBugInfo(key, "detail");
                Check(trace.Single() == "old:Bug Info: key - display - Message: detail - Quitting: False - Extra Info: global - global - specific:0", "exact native bug header/provider order/one suffix removal");
                Check(HLOutput.RemoveBugInfoProvider(provider) && global.Count == 1, "global removal removes one duplicate");
                Check(HLOutput.RemoveBugInfoProvider(key, child) && !specific.ContainsKey(key), "last specific removal removes dictionary row");
                Check(!HLOutput.RemoveBugInfoProvider(key, child), "missing specific row returns false");
                global.Clear(); trace.Clear(); HLOutput.LogBugInfo(key, " ");
                Check(trace.Single() == "old:Bug Info: key - display -  Quitting: False:0", "whitespace optional message retained without Message prefix");
                Field(typeof(HLOutput), "s_quitting").SetValue(null, true); trace.Clear(); HLOutput.LogError("during quit"); HLOutput.LogBugInfo(key);
                Check(trace.SequenceEqual(new[] { "old:during quit:0", "old:Bug Info: key - display - Quitting: True:0" }), "quitting flag labels bug reports but does not gate original errors");
                HLOutput.AddBugInfoProvider((Func<string>)null);
                Check(Capture(() => HLOutput.LogBugInfo(key)) is NullReferenceException, "native providers have no invented null guard");
                global.Clear(); Exception sentinel = new Exception("provider"); HLOutput.AddBugInfoProvider(() => { throw sentinel; });
                Check(ReferenceEquals(Capture(() => HLOutput.LogBugInfo(key)), sentinel), "provider exception propagates unchanged"); global.Clear();

                Check(HLOutput.GetLogScopedStringBuilder().Length == 0 && HLOutput.GetWarningScopedStringBuilder().Length == 0, "original retail default development scopes");
                trace.Clear(); var scope = HLOutput.GetErrorScopedStringBuilder(); scope.Append("scoped"); scope.Dispose();
                Check(trace.Single() == "old:scoped:0" && pool.Count == 1, "error scope reports then returns builder to pool");
                StringBuilder recycled = pool.Peek(); var second = HLOutput.GetErrorScopedStringBuilder();
                Check(pool.Count == 0 && second.Length == 0 && recycled.Length == 0, "reused builder cleared before scope exposure");
                trace.Clear(); second.Dispose();
                Check(trace.Count == 0 && pool.Count == 1, "empty scope still returns builder without reporting");
                second.Dispose(); Check(pool.Count == 2 && ReferenceEquals(pool.ToArray()[0], pool.ToArray()[1]), "original repeated disposal permits duplicate pool entries");
                pool.Clear(); var failed = HLOutput.GetErrorScopedStringBuilder(); failed.Append("callback failure");
                Action<string, int> thrower = (text, code) => { throw sentinel; }; core.AddLogErrorHandler(thrower);
                Check(ReferenceEquals(Capture(() => failed.Dispose()), sentinel) && pool.Count == 0, "error callback failure prevents pool return, no finally");
                core.RemoveLogErrorHandler(thrower);

                trace.Clear(); HLUnityCore.OnHandleFirstChanceException(null, null); HLUnityCore.OnHandleUnresolvedException(null, null); HLUnityCore.OnHandleUnresolvedException(null, new UnhandledExceptionEventArgs("object", false));
                Check(trace.Count == 0, "original exception notification invalid/null guards");
            }
            finally
            {
                ProcessManager.UnregisterSystem(core); ProcessManager.UnregisterSystem(replacement);
                if (existed) registry[name] = prior; else registry.Remove(name);
                global.Clear(); global.AddRange(oldGlobal); specific.Clear(); foreach (var entry in oldSpecific) specific.Add(entry.Key, entry.Value);
                pool.Clear(); for (int i = oldPool.Length - 1; i >= 0; i--) pool.Push(oldPool[i]);
                Field(typeof(HLOutput), "s_quitting").SetValue(null, oldQuitting);
            }
        }

        private static void CheckNativeValueMethods()
        {
            var bridge = new HLUnityCoreNativeBridge();
            Check(bridge.GetDeviceID() == string.Empty && bridge.GetScreenUnusableHeaderHeightInPixels() == 0 && bridge.GetScreenUnusableFooterHeightInPixels() == 0 && !bridge.DoesThisDeviceHaveANotch() && bridge.CalculateApplicationChecksum() == 0, "original retail bridge constants");
            bridge.LogToDeviceConsole(null); Check(true, "original retail console body RET");
            uint[] bits = { 0x00000000, 0x80000000, 0x3f800000, 0xbf800000, 0x3fe00000, 0xbfe00000, 0x4effffff, 0x4f000000, 0x4f000001, 0xceffffff, 0xcf000000, 0xcf000001, 0x7f7fffff, 0xff7fffff, 0x7f800000, 0xff800000, 0x7fc00000, 0xffc00000, 0x7f800001, 0xff800001, 0x00000001, 0x80000001 };
            int[] expected = { 0, 0, 1, -1, 1, -1, 2147483520, int.MaxValue, int.MaxValue, -2147483520, int.MinValue, int.MinValue, int.MaxValue, int.MinValue, int.MinValue, int.MinValue, 0, 0, 0, 0, 0, 0 };
            for (int i = 0; i < bits.Length; i++) Check(bridge.GetPixelsFromNativeUnitDistance(BitConverter.ToSingle(BitConverter.GetBytes(bits[i]), 0)) == expected[i], "exact original ARM64 leaf conversion " + bits[i].ToString("x8"));
            Check(HLTrackingAuthorisationNativeBridge.Initialise(null).GetType() == typeof(HLTrackingAuthorisationNativeBridgeStub), "original retail tracking factory ignores owner/selects genuine original Stub");
            var tracking = new HLTrackingAuthorisationNativeBridgeStub();
            Check(!tracking.CanDeviceRequestTrackingAuthorisation() && tracking.RequestTrackingAuthorisationStatus() == -1, "original retail tracking capabilities/status");
            Check(((string)null).TrimEndString(null) == null, "original null suffix bypasses null receiver");
            Check(Capture(() => ((string)null).TrimEndString("suffix")) is NullReferenceException, "original nonnull suffix dereferences null receiver");
            Check("abcabc".TrimEndString("abc") == "abc", "original suffix removed once only");
            Check("abc".TrimEndString("") == "abc" && "abc".TrimEndString("none") == "abc", "empty/nonmatching suffix results");
            string value = new string(new[] { 'a', 'b', 'c' }); Check(ReferenceEquals(value, value.TrimEndString(null)) && ReferenceEquals(value, value.TrimEndString("x")), "bypass/nonmatch preserve actual reference");
        }
    }
}
