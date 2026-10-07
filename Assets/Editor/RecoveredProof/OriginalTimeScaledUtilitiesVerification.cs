using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine.TestTools;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Editor
{
public static class OriginalTimeScaledUtilitiesVerification
{
    private static int RunManagedCore()
    {
        int checks = 0;
        Action<bool, string> check = (condition, name) =>
        {
            if (!condition) throw new Exception(name);
            checks++;
        };
        Func<Action, Type, bool> throws = (action, expected) =>
        {
            try { action(); }
            catch (Exception error) { return error.GetType() == expected; }
            return false;
        };
        Type type = typeof(TimeScaledUtilities);
        check(type.IsPublic && type.IsAbstract && type.IsSealed, "original static owner");
        check((type.Attributes & TypeAttributes.BeforeFieldInit) != 0, "original BeforeFieldInit");
        FieldInfo[] fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Static);
        check(fields.Length == 2, "two original owner fields");
        check(fields[0].Name == "s_timeManagerRef" && fields[0].FieldType == typeof(SystemRef<TimeManager>), "first real reference field");
        check(fields[1].Name == "s_waitForFixedUpdate" && fields[1].FieldType == typeof(WaitForFixedUpdate), "second real yield field");
        foreach (FieldInfo field in fields) check(field.IsPrivate && field.IsStatic && field.IsInitOnly, "original field modifiers");
        check(type.TypeInitializer != null && type.TypeInitializer.IsPrivate, "real initializer");
        MethodInfo[] methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        check(methods.Length == 4, "four ordinary owner methods");
        foreach (string name in new[] { "WaitForFixedSeconds", "WaitForSeconds" })
        {
            MethodInfo method = type.GetMethod(name);
            check(method.IsPublic && method.IsStatic && method.ReturnType == typeof(IEnumerator), name + " signature");
            ParameterInfo[] parameters = method.GetParameters();
            check(parameters.Length == 3 && parameters[0].Name == "waitSeconds" && parameters[0].ParameterType == typeof(float), "duration parameter");
            check(parameters[1].Name == "timeCategory" && parameters[1].ParameterType == typeof(TimeCategoryObject), "category parameter");
            check(parameters[2].Name == "earlyOut" && parameters[2].ParameterType == typeof(Func<bool>), "predicate parameter");
            check(parameters[2].IsOptional && parameters[2].HasDefaultValue && parameters[2].DefaultValue == null, "optional original null");
        }
        MethodInfo delay = type.GetMethod("DelayFixedSecondsCoroutine", BindingFlags.NonPublic | BindingFlags.Static);
        check(delay.IsPrivate && delay.ReturnType == typeof(IEnumerator), "private original delay iterator");
        check(type.GetMethod("DelayFixedSeconds").ReturnType == typeof(Coroutine), "original host wrapper signature");
        check(type.GetNestedTypes(BindingFlags.NonPublic).Length == 3, "three emitted genuine coroutine records, binding unclaimed");

        // A real empty ProcessManager registry and the real cached SystemRef are
        // sufficient to exercise the absent-manager path. No replacement manager
        // or engine object is constructed and no Unity time getter is called.
        ProcessManager.UnregisterSystem<TimeManager>();
        float[] durations = { float.NaN, float.NegativeInfinity, float.PositiveInfinity, -1f, -0f, 0f, 0.5f };
        foreach (bool fixedWait in new[] { false, true })
        foreach (float duration in durations)
        {
            int predicateCalls = 0;
            Func<bool> earlyOut = () => { predicateCalls++; throw new Exception("must not run without manager"); };
            IEnumerator iterator = fixedWait ? TimeScaledUtilities.WaitForFixedSeconds(duration, null, earlyOut)
                : TimeScaledUtilities.WaitForSeconds(duration, null, earlyOut);
            check(predicateCalls == 0, "construction does not run predicate");
            check(iterator.Current == null, "initial current null");
            check(!iterator.MoveNext(), "absent manager ends all durations");
            check(predicateCalls == 0, "TryGet precedes duration and predicate");
            check(iterator.Current == null, "no yield on absent manager");
            check(!iterator.MoveNext(), "terminal iterator remains terminal");
            check(throws(() => iterator.Reset(), typeof(NotSupportedException)), "genuine reset exception");
            ((IDisposable)iterator).Dispose();
            check(predicateCalls == 0 && !iterator.MoveNext(), "dispose has no callback");
        }
        foreach (float duration in durations)
        {
            int callbackCalls = 0;
            IEnumerator outer = null;
            bool reentryResult = true;
            outer = (IEnumerator)delay.Invoke(null, new object[] { duration, null, (Action)(() =>
            {
                callbackCalls++;
                reentryResult = outer.MoveNext();
            }) });
            check(callbackCalls == 0, "delay construction is deferred");
            check(outer.MoveNext(), "delay always yields once");
            IEnumerator inner = outer.Current as IEnumerator;
            check(inner != null, "delay yields genuine nested wait");
            check(!inner.MoveNext(), "nested wait sees absent manager");
            check(callbackCalls == 0, "nested completion alone does not invoke callback");
            check(!outer.MoveNext(), "delay resumes then completes");
            check(callbackCalls == 1 && !reentryResult, "terminal before callback reentry");
            check(!outer.MoveNext() && callbackCalls == 1, "callback exactly once");
            check(throws(() => outer.Reset(), typeof(NotSupportedException)), "outer original reset");
        }
        IEnumerator fault = (IEnumerator)delay.Invoke(null, new object[] { 1f, null, (Action)null });
        check(fault.MoveNext(), "null callback still yields first");
        check(throws(() => fault.MoveNext(), typeof(NullReferenceException)), "unguarded null callback");
        check(!fault.MoveNext(), "null callback fault leaves terminal");
        var expected = new InvalidOperationException("callback evidence");
        IEnumerator failure = (IEnumerator)delay.Invoke(null, new object[] { 1f, null, (Action)(() => { throw expected; }) });
        check(failure.MoveNext(), "throwing callback yields first");
        Exception observed = null;
        try { failure.MoveNext(); } catch (Exception error) { observed = error; }
        check(ReferenceEquals(observed, expected), "callback fault passes through unchanged");
        check(!failure.MoveNext(), "throwing callback remains terminal");
        IEnumerator disposed = (IEnumerator)delay.Invoke(null, new object[] { 1f, null, (Action)(() => { throw new Exception("not requested"); }) });
        check(disposed.MoveNext(), "dispose case yields first");
        ((IDisposable)disposed).Dispose();
        check(disposed.Current is IEnumerator, "original empty Dispose retains Current");
        check(throws(() => disposed.MoveNext(), typeof(Exception)), "empty Dispose does not cancel pending callback");
        check(!disposed.MoveNext(), "callback fault after empty Dispose remains terminal");
        return checks;
    }
    private static readonly object FixtureLock = new object();

    public static int RunOriginalManagedBoundaries()
    {
        lock (FixtureLock)
        using (var registry = new RegistryScope())
            return RunManagedCore();
    }

    private sealed class Checks
    {
        public int Count;
        public void That(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
            ++Count;
        }
        public void Throws<T>(Action action, string reason) where T : Exception
        {
            Exception error = Observe(action);
            That(error != null && error.GetType() == typeof(T), reason);
        }
    }

    private static Exception Observe(Action action)
    {
        try { action(); return null; }
        catch (Exception error) { return error; }
    }

    private static FieldInfo Field(Type type, string name)
    {
        for (Type cursor = type; cursor != null; cursor = cursor.BaseType)
        {
            FieldInfo field = cursor.GetField(name, BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(type.FullName, name);
    }

    private static object Read(object target, string name) => Field(target.GetType(), name).GetValue(target);
    private static void Write(object target, string name, object value)
    {
        FieldInfo field = Field(target.GetType(), name);
        Require(!field.IsInitOnly && !field.IsLiteral, "Fixture never writes an original readonly or literal field");
        field.SetValue(target, value);
    }
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException(reason); }

    private static void Invoke(object target, string name)
    {
        try
        {
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, null);
        }
        catch (TargetInvocationException error)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    private static IEnumerator Delay(float duration, TimeCategoryObject category, Action action) =>
        (IEnumerator)typeof(TimeScaledUtilities).GetMethod("DelayFixedSecondsCoroutine",
            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { duration, category, action });

    private static object SharedFixedYield => Field(typeof(TimeScaledUtilities), "s_waitForFixedUpdate").GetValue(null);

    // Preserve existing dictionary objects and their entries. This is fixture
    // isolation, not a replacement runtime registry or provider.
    private sealed class DictionarySnapshot
    {
        private readonly IDictionary dictionary;
        private readonly List<DictionaryEntry> entries = new List<DictionaryEntry>();
        public DictionarySnapshot(IDictionary dictionary)
        {
            this.dictionary = dictionary;
            foreach (DictionaryEntry entry in dictionary) entries.Add(entry);
        }
        public void Restore()
        {
            dictionary.Clear();
            foreach (DictionaryEntry entry in entries) dictionary.Add(entry.Key, entry.Value);
        }
        public bool Matches()
        {
            if (dictionary.Count != entries.Count) return false;
            foreach (DictionaryEntry entry in entries)
                if (!dictionary.Contains(entry.Key) || !ReferenceEquals(dictionary[entry.Key], entry.Value)) return false;
            return true;
        }
    }

    private sealed class SavedMutableField
    {
        private readonly object target;
        private readonly FieldInfo field;
        private readonly object value;
        public SavedMutableField(object target, string name)
        {
            this.target = target;
            field = Field(target.GetType(), name);
            Require(!field.IsInitOnly, "Only mutable registry callback fields are isolated");
            value = field.GetValue(target);
            field.SetValue(target, null);
        }
        public void Restore() { field.SetValue(target, value); }
        public bool Matches() => ReferenceEquals(field.GetValue(target), value);
    }

    private sealed class RegistryScope : IDisposable
    {
        private readonly string timeName = ProcessManager.GetDefaultName<TimeManager>();
        private readonly string configName = ProcessManager.GetDefaultName<SystemConfiguration>();
        private readonly ISystem priorTime, priorConfig;
        private readonly object originalTimeReference;
        private readonly DictionarySnapshot systems, actions;
        private readonly List<DictionarySnapshot> typedCaches = new List<DictionarySnapshot>();
        private readonly List<DictionarySnapshot> actionRows = new List<DictionarySnapshot>();
        private readonly List<SavedMutableField> callbacks = new List<SavedMutableField>();
        private bool disposed;

        public RegistryScope()
        {
            // Prime genuine static/typed caches before the fixture snapshot. Never
            // ForceReset or replace TimeScaledUtilities' readonly cached reference.
            RuntimeHelpers.RunClassConstructor(typeof(TimeScaledUtilities).TypeHandle);
            ProcessManager.GetSystemSafe<TimeManager>();
            ProcessManager.GetSystemSafe<SystemConfiguration>();
            originalTimeReference = Field(typeof(TimeScaledUtilities), "s_timeManagerRef").GetValue(null);
            priorTime = ProcessManager.GetSystemRef(timeName).GetSafe();
            priorConfig = ProcessManager.GetSystemRef(configName).GetSafe();
            IDictionary rows = (IDictionary)Field(typeof(ProcessManager), "s_systemDictionary").GetValue(null);
            systems = new DictionarySnapshot(rows);
            foreach (DictionaryEntry entry in rows)
            {
                IDictionary cache = (IDictionary)Read(entry.Value, "SystemRefDictionary");
                typedCaches.Add(new DictionarySnapshot(cache));
                if ((string)entry.Key != timeName && (string)entry.Key != configName) continue;
                var references = new List<object> { Read(entry.Value, "SystemRef") };
                foreach (object reference in cache.Values) references.Add(reference);
                foreach (object reference in references)
                {
                    callbacks.Add(new SavedMutableField(reference, "OnSystemStartup"));
                    callbacks.Add(new SavedMutableField(reference, "OnSystemShutdown"));
                    callbacks.Add(new SavedMutableField(reference, "m_actionOnSystemValid"));
                }
            }
            // Real unregistration unsubscribes the prior system's action entries;
            // preserve each live row and restore it after the real registration.
            IDictionary lookup = (IDictionary)Field(typeof(ProcessManager), "s_systemActionLookup").GetValue(null);
            actions = new DictionarySnapshot(lookup);
            foreach (IDictionary row in lookup.Values) actionRows.Add(new DictionarySnapshot(row));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                ProcessManager.UnregisterSystem(timeName);
                ProcessManager.UnregisterSystem(configName);
                if (priorTime != null) ProcessManager.RegisterSystem(priorTime, timeName, canReplace: true);
                if (priorConfig != null) ProcessManager.RegisterSystem(priorConfig, configName, canReplace: true);
            }
            finally
            {
                foreach (DictionarySnapshot row in actionRows) row.Restore();
                actions.Restore();
                foreach (DictionarySnapshot cache in typedCaches) cache.Restore();
                systems.Restore();
                foreach (SavedMutableField callback in callbacks) callback.Restore();
            }
            Require(systems.Matches() && actions.Matches(), "Registry and action row identities are restored");
            foreach (DictionarySnapshot row in typedCaches) Require(row.Matches(), "Original typed reference caches are restored");
            foreach (DictionarySnapshot row in actionRows) Require(row.Matches(), "Original action entries are restored");
            foreach (SavedMutableField callback in callbacks) Require(callback.Matches(), "Prior startup/shutdown/deferred callbacks are restored without invocation");
            Require(ReferenceEquals(Field(typeof(TimeScaledUtilities), "s_timeManagerRef").GetValue(null), originalTimeReference),
                "Original readonly cached reference identity survives isolation");
            Require(ReferenceEquals(ProcessManager.GetSystemRef(timeName).GetSafe(), priorTime) &&
                ReferenceEquals(ProcessManager.GetSystemRef(configName).GetSafe(), priorConfig), "Prior real systems are restored");
        }
    }

    private sealed class EngineScope : IDisposable
    {
        private readonly RegistryScope registry = new RegistryScope();
        private readonly float priorTimeScale = Time.timeScale, priorFixedDelta = Time.fixedDeltaTime;
        private readonly FieldInfo settingCache = Field(typeof(TimeManager), "s_cachedTimeSettingResult");
        private readonly object priorSettingCache;
        private readonly IDictionary multiplies;
        private readonly bool hadMultiply;
        private readonly object priorMultiply;
        private readonly SystemConfiguration configurationSystem = new SystemConfiguration();
        private StackableDataHandle scaleHandle;
        private GameObject host;
        private TimeCategoryConfiguration configuration;
        private bool initialised, disposed;
        public TimeManager Manager { get; private set; }
        public TimeCategoryObject Category { get; private set; }
        public TimeCategoryObject MissingCategory { get; private set; }
        private TimeCategoryObject globalCategory;

        public EngineScope()
        {
            priorSettingCache = settingCache.GetValue(null);
            multiplies = (IDictionary)Field(typeof(StackableData), "Multiplies").GetValue(null);
            hadMultiply = multiplies.Contains(typeof(TimeSetting));
            priorMultiply = multiplies[typeof(TimeSetting)];
            try
            {
                ProcessManager.UnregisterSystem<TimeManager>();
                ProcessManager.RegisterSystem(configurationSystem, canReplace: true);
                Category = ScriptableObject.CreateInstance<TimeCategoryObject>();
                MissingCategory = ScriptableObject.CreateInstance<TimeCategoryObject>();
                globalCategory = ScriptableObject.CreateInstance<TimeCategoryObject>();
                Write(Category, "m_guid", "original-time-fixture-category");
                Write(MissingCategory, "m_guid", "original-time-fixture-missing");
                Write(globalCategory, "m_guid", "original-time-fixture-global");
                Category.name = "Original timing category";
                globalCategory.name = "Original timing global";
                configuration = ScriptableObject.CreateInstance<TimeCategoryConfiguration>();
                Write(configuration, "m_categoriesPrioritised", new List<TimeCategoryObject> { Category });
                Write(configuration, "m_unityGlobalTime", globalCategory);
                SystemConfiguration.AddConfig<SystemConfigurationAsset>(configuration);
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
                host = new GameObject("OriginalTimeScaledUtilitiesFixture");
                host.SetActive(false);
                // The real Editor AddComponent invokes the original Reset before
                // Awake, when its configuration is still null. Preserve that fault.
                int resetCount = 0;
                string resetStack = null;
                Application.LogCallback log = (condition, stack, type) =>
                {
                    if (type == LogType.Exception && condition == "NullReferenceException: Object reference not set to an instance of an object")
                    { ++resetCount; resetStack = stack; }
                };
                LogAssert.Expect(LogType.Exception, new Regex("^NullReferenceException: Object reference not set to an instance of an object$"));
                Application.logMessageReceived += log;
                try { Manager = host.AddComponent<TimeManager>(); }
                finally { Application.logMessageReceived -= log; }
                Require(resetCount == 1 && resetStack != null && resetStack.Contains("Hardlight.TimeManager.Reset"),
                    "Actual Editor original Reset-before-Awake boundary must occur exactly once");
                if (Read(Manager, "m_timeCategoryConfiguration") == null) Invoke(Manager, "Awake");
                initialised = true;
                Require(ReferenceEquals(ProcessManager.GetSystemSafe<TimeManager>(), Manager), "Real Awake registers the owned TimeManager");
                Require(Manager.GetTimescale(Category) == 1f && Manager.GetTimescale(globalCategory) == 1f,
                    "Real configuration and stack initialise category scales");
            }
            catch { Dispose(); throw; }
        }

        public void Scale(float scale)
        {
            var setting = new TimeSetting();
            setting.SetCategory(Category, scale);
            if (scaleHandle == null) scaleHandle = Manager.ApplyTimeSetting(setting);
            else Manager.UpdateTimeSetting(scaleHandle, setting);
            float observed = Manager.GetTimescale(Category);
            Require(float.IsNaN(scale) ? float.IsNaN(observed) : observed == scale, "Real TimeSetting stack supplies requested category scale");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (Manager != null && scaleHandle != null) Manager.RemoveTimeSetting(scaleHandle);
                if (Manager != null && (initialised || Read(Manager, "m_timeCategoryConfiguration") != null)) Invoke(Manager, "OnDestroy");
            }
            finally
            {
                try
                {
                    // Keep the owned GameObject inactive throughout this fixture.
                    // Invoke its original teardown before owned object disposal, as
                    // in the maintained real-TimeManager Editor verification.
                    if (host != null) UnityEngine.Object.DestroyImmediate(host);
                    foreach (UnityEngine.Object owned in new UnityEngine.Object[] { configuration, Category, MissingCategory, globalCategory })
                        if (owned != null) UnityEngine.Object.DestroyImmediate(owned);
                }
                finally
                {
                    settingCache.SetValue(null, priorSettingCache);
                    if (hadMultiply) multiplies[typeof(TimeSetting)] = priorMultiply;
                    else multiplies.Remove(typeof(TimeSetting));
                    Time.timeScale = priorTimeScale;
                    Time.fixedDeltaTime = priorFixedDelta;
                    registry.Dispose();
                }
            }
            Require(ReferenceEquals(settingCache.GetValue(null), priorSettingCache), "Shared mutable cache identity restored");
            Require(Time.timeScale == priorTimeScale && Time.fixedDeltaTime == priorFixedDelta, "Unity global time state restored");
            Require(multiplies.Contains(typeof(TimeSetting)) == hadMultiply && (!hadMultiply || ReferenceEquals(multiplies[typeof(TimeSetting)], priorMultiply)),
                "Original multiply delegate entry restored");
            Require(host == null && Category == null && MissingCategory == null && globalCategory == null && configuration == null,
                "Every owned Unity object is destroyed");
        }
    }

    // These checks manually drive the original iterators with real Unity providers.
    // They do not assert a scheduled host coroutine or playable-game acceptance.
    public static int RunOriginalDurationBoundaries()
    {
        lock (FixtureLock)
        using (var scope = new EngineScope())
        {
            var checks = new Checks();
            foreach (bool fixedWait in new[] { false, true })
            foreach (float duration in new[] { float.NaN, float.NegativeInfinity, -1f, -0f, 0f })
            {
                int predicates = 0;
                Func<bool> earlyOut = () => { ++predicates; throw new InvalidOperationException("must not run for inactive duration"); };
                IEnumerator iterator = fixedWait ? TimeScaledUtilities.WaitForFixedSeconds(duration, scope.MissingCategory, earlyOut)
                    : TimeScaledUtilities.WaitForSeconds(duration, scope.MissingCategory, earlyOut);
                checks.That(!iterator.MoveNext(), "Present-manager nonpositive/unordered duration stops before predicate/category lookup");
                checks.That(predicates == 0, "Duration comparison precedes early-out callback");
                checks.That(iterator.Current == null, "Inactive duration never publishes a yield");
                checks.That(!iterator.MoveNext(), "Inactive duration remains terminal");
            }
            return checks.Count;
        }
    }

    public static int RunOriginalFixedTimeBoundaries()
    {
        lock (FixtureLock)
        using (var scope = new EngineScope())
        {
            var checks = new Checks();
            scope.Scale(2f);
            int predicates = 0;
            IEnumerator overshoot = TimeScaledUtilities.WaitForFixedSeconds(0.03f, scope.Category, () => { ++predicates; return false; });
            checks.That(overshoot.Current == null && predicates == 0, "Fixed wait construction is deferred");
            checks.That(overshoot.MoveNext(), "Overshooting step still yields once");
            checks.That(ReferenceEquals(overshoot.Current, SharedFixedYield) && overshoot.Current is WaitForFixedUpdate, "Fixed Current is genuine shared engine yield");
            checks.That(predicates == 1 && !overshoot.MoveNext(), "Advance occurs before first yield; next comparison completes");
            checks.That(ReferenceEquals(overshoot.Current, SharedFixedYield), "Terminal Current is retained");
            checks.Throws<NotSupportedException>(() => overshoot.Reset(), "Fixed Reset preserves original unsupported operation");

            scope.Scale(1f);
            IEnumerator retained = TimeScaledUtilities.WaitForFixedSeconds(0.05f, scope.Category);
            checks.That(retained.MoveNext(), "Captured manager first step");
            ProcessManager.UnregisterSystem<TimeManager>();
            checks.That(ProcessManager.GetSystemSafe<TimeManager>() == null, "Actual registry now lacks manager");
            checks.That(retained.MoveNext(), "Existing iterator uses captured manager after unregister");
            checks.That(!TimeScaledUtilities.WaitForFixedSeconds(0.05f, scope.Category).MoveNext(), "New iterator resolves missing manager independently");
            ((IDisposable)retained).Dispose();
            checks.That(retained.MoveNext(), "Original empty Dispose does not cancel existing wait");
            checks.That(!retained.MoveNext(), "Three real fixed steps complete original duration");
            ProcessManager.RegisterSystem(scope.Manager, canReplace: true);

            int dynamicPredicates = 0;
            IEnumerator dynamic = TimeScaledUtilities.WaitForFixedSeconds(0.05f, scope.Category, () => { ++dynamicPredicates; return false; });
            checks.That(dynamic.MoveNext(), "Dynamic category first original step");
            scope.Scale(3f);
            checks.That(dynamic.MoveNext(), "Next step re-reads actual category scale and still yields");
            checks.That(!dynamic.MoveNext() && dynamicPredicates == 2, "Updated scale ends on next comparison without another predicate");

            foreach (float scale in new[] { 0f, -1f, float.NegativeInfinity })
            {
                scope.Scale(scale);
                int calls = 0;
                IEnumerator bounded = TimeScaledUtilities.WaitForFixedSeconds(0.01f, scope.Category, () => ++calls == 3);
                checks.That(bounded.MoveNext(), "Zero/negative scale first step yields");
                checks.That(bounded.MoveNext(), "Zero/negative scale does not invent duration progress");
                checks.That(!bounded.MoveNext() && calls == 3, "Original predicate bounds nonterminating scale");
            }
            foreach (float scale in new[] { float.NaN, float.PositiveInfinity })
            {
                scope.Scale(scale);
                int calls = 0;
                IEnumerator bounded = TimeScaledUtilities.WaitForFixedSeconds(0.01f, scope.Category, () => { ++calls; return false; });
                checks.That(bounded.MoveNext(), "Unordered/infinite timer step publishes one yield");
                checks.That(!bounded.MoveNext() && calls == 1, "Unordered/infinite timer stops before next predicate");
            }
            return checks.Count;
        }
    }

    public static int RunOriginalFrameTimeBoundaries()
    {
        lock (FixtureLock)
        using (var scope = new EngineScope())
        {
            var checks = new Checks();
            scope.Scale(1f);
            float delta = Time.deltaTime;
            checks.That(!float.IsNaN(delta) && !float.IsInfinity(delta) && delta >= 0f, "Observe actual engine frame delta");
            int calls = 0;
            IEnumerator frame = TimeScaledUtilities.WaitForSeconds(delta > 0f ? delta * 1.5f : 0.5f,
                scope.Category, () => ++calls == 3);
            checks.That(frame.Current == null && calls == 0, "Frame wait construction is deferred");
            checks.That(frame.MoveNext() && frame.Current == null && calls == 1, "Frame wait yields null after actual first delta");
            checks.That(frame.MoveNext() && frame.Current == null && calls == 2, "Second frame step publishes null even after overshoot");
            checks.That(!frame.MoveNext() && calls == (delta > 0f ? 2 : 3), "Actual positive delta or original early-out bounds frame completion");
            checks.That(frame.Current == null, "Frame terminal Current remains null");
            checks.Throws<NotSupportedException>(() => frame.Reset(), "Frame Reset retains original exception");
            ((IDisposable)frame).Dispose();
            checks.That(!frame.MoveNext(), "Terminal frame remains terminal after empty Dispose");
            scope.Scale(float.NaN);
            int unorderedCalls = 0;
            IEnumerator unordered = TimeScaledUtilities.WaitForSeconds(0.01f, scope.Category, () => { ++unorderedCalls; return false; });
            checks.That(unordered.MoveNext() && unordered.Current == null, "Actual delta times NaN scale yields once");
            checks.That(!unordered.MoveNext() && unorderedCalls == 1, "NaN frame timer terminates before another predicate");
            return checks.Count;
        }
    }

    public static int RunOriginalFaultAndNestedDelayBoundaries()
    {
        lock (FixtureLock)
        using (var scope = new EngineScope())
        {
            var checks = new Checks();
            foreach (bool fixedWait in new[] { false, true })
            {
                Func<float, TimeCategoryObject, Func<bool>, IEnumerator> wait = (duration, category, predicate) =>
                    fixedWait ? TimeScaledUtilities.WaitForFixedSeconds(duration, category, predicate)
                        : TimeScaledUtilities.WaitForSeconds(duration, category, predicate);
                int calls = 0;
                IEnumerator early = wait(1f, scope.MissingCategory, () => { ++calls; return true; });
                checks.That(!early.MoveNext() && calls == 1, "Early-out stops before missing-category failure");
                checks.That(early.Current == null, "Early-out publishes no yield");
                var expected = new ApplicationException("original predicate identity");
                IEnumerator fault = wait(1f, scope.Category, () => { throw expected; });
                checks.That(ReferenceEquals(Observe(() => fault.MoveNext()), expected), "Original predicate exception identity passes through");
                checks.That(!fault.MoveNext() && fault.Current == null, "Predicate fault leaves terminal iterator");
                int missingCalls = 0;
                IEnumerator missing = wait(1f, scope.MissingCategory, () => { ++missingCalls; return false; });
                checks.Throws<System.Collections.Generic.KeyNotFoundException>(() => missing.MoveNext(), "Actual missing timescale key fault remains");
                checks.That(missingCalls == 1 && !missing.MoveNext(), "Predicate precedes category fault and terminal state survives");
                IEnumerator reentry = null;
                bool nestedResult = true;
                int reentryCalls = 0;
                reentry = wait(float.PositiveInfinity, scope.Category, () =>
                {
                    ++reentryCalls;
                    if (reentryCalls == 1) { nestedResult = reentry.MoveNext(); return false; }
                    return true;
                });
                checks.That(reentry.MoveNext() && !nestedResult && reentryCalls == 1, "Predicate observes terminal state during reentry before outer yield");
                checks.That(!reentry.MoveNext() && reentryCalls == 2, "Next predicate ends bounded reentry case");
            }
            int callbacks = 0;
            IEnumerator outer = null;
            bool reentrant = true;
            outer = Delay(0f, scope.Category, () => { ++callbacks; reentrant = outer.MoveNext(); });
            checks.That(outer.Current == null && callbacks == 0, "Nested delay construction is deferred");
            checks.That(outer.MoveNext(), "Zero-duration delay still yields nested iterator once");
            IEnumerator inner = outer.Current as IEnumerator;
            checks.That(inner != null && !inner.MoveNext(), "Real nested wait resolves present manager then zero duration");
            checks.That(callbacks == 0, "Nested completion alone does not call outer action");
            checks.That(!outer.MoveNext() && callbacks == 1 && !reentrant, "Outer becomes terminal before original action reentry");
            checks.That(!outer.MoveNext() && callbacks == 1 && ReferenceEquals(outer.Current, inner), "Action runs once and Current remains retained");
            IEnumerator nullAction = Delay(0f, scope.Category, null);
            checks.That(nullAction.MoveNext(), "Null original action still yields first");
            checks.Throws<NullReferenceException>(() => nullAction.MoveNext(), "Original unguarded null action fault");
            checks.That(!nullAction.MoveNext(), "Null action failure is terminal");
            var actionFailure = new InvalidOperationException("original delayed action identity");
            IEnumerator failed = Delay(0f, scope.Category, () => { throw actionFailure; });
            checks.That(failed.MoveNext(), "Throwing action still yields first");
            checks.That(ReferenceEquals(Observe(() => failed.MoveNext()), actionFailure), "Original action fault identity propagates");
            checks.That(!failed.MoveNext(), "Throwing action is terminal");
            int disposedCalls = 0;
            IEnumerator disposed = Delay(0f, scope.Category, () => ++disposedCalls);
            ((IDisposable)disposed).Dispose();
            checks.That(disposed.MoveNext(), "Dispose before start does not cancel delay");
            object current = disposed.Current;
            ((IDisposable)disposed).Dispose();
            checks.That(ReferenceEquals(current, disposed.Current), "Empty Dispose after yield retains nested Current");
            checks.Throws<NotSupportedException>(() => disposed.Reset(), "Original reset fault does not rewrite pending flow");
            checks.That(!disposed.MoveNext() && disposedCalls == 1, "Empty Dispose and failed Reset leave pending action executable");

            FieldInfo hostField = Field(typeof(Hardlight.Utils.CoroutineUtils), "s_instance");
            object priorHost = hostField.GetValue(null);
            try
            {
                Require(!hostField.IsInitOnly, "Only mutable host singleton is isolated");
                hostField.SetValue(null, null);
                int hostCallbacks = 0;
                checks.Throws<NullReferenceException>(() => TimeScaledUtilities.DelayFixedSeconds(0f, scope.Category, () => ++hostCallbacks),
                    "Public wrapper preserves absent real CoroutineUtils host failure");
                checks.That(hostCallbacks == 0, "Missing host never invokes deferred action");
            }
            finally { hostField.SetValue(null, priorHost); }
            return checks.Count;
        }
    }
}
}
