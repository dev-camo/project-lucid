using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    public static class SystemRefVerification
    {
        private sealed class First : ISystem { public readonly string Name; public First(string name) { Name = name; } }
        private sealed class Second : ISystem { }
        private static int checks;
        private static void Check(bool condition, string text) { if (!condition) throw new Exception("SystemRef verification: " + text); checks++; }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static void Set<T>(SystemRef<T> owner, string name, object value) where T : class, ISystem => Field(typeof(SystemRef<T>), name).SetValue(owner, value);
        private static object Pending<T>(SystemRef<T> owner) where T : class, ISystem => Field(typeof(SystemRef<T>), "m_actionOnSystemValid").GetValue(owner);
        private static void Replace<T>(SystemRef<T> owner, ISystem value) where T : class, ISystem => Invoke(owner, "Hardlight.ISystemRef.InternalReplaceSystem", new object[] { value });
        private static void Revoke<T>(SystemRef<T> owner) where T : class, ISystem => Invoke(owner, "Hardlight.ISystemRef.InternalRevokeSystem", null);
        private static void Invoke<T>(SystemRef<T> owner, string name, object[] arguments) where T : class, ISystem
        {
            try { typeof(SystemRef<T>).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, arguments); }
            catch (TargetInvocationException exception) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
        }
        private static TError Throws<TError>(Action action, string text) where TError : Exception
        {
            try { action(); } catch (TError error) { checks++; return error; }
            throw new Exception("SystemRef verification expected " + typeof(TError).Name + ": " + text);
        }
        public static int RunManaged()
        {
            checks = 0;
            CheckDeclarations(); CheckReads(); CheckCallbacks(); CheckIterator(); CheckConcurrentEvents();
            return checks;
        }
        public static void Run()
        {
            RunManaged(); CheckEngine();
            Debug.Log("PASS original SystemRef bounded checks=" + checks + "; host and Unity-reference fixtures executed; full startup remains outside proof.");
        }
        private static void CheckDeclarations()
        {
            Type type = typeof(SystemRef<>);
            FieldInfo[] fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(field => field.MetadataToken).ToArray();
            Check(fields.Select(field => field.Name).SequenceEqual(new[] { "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid", "m_systemName", "m_system", "m_isystem" }), "all six original field names and declaration order");
            Check(fields.All(field => field.IsPrivate && !field.IsStatic) && fields.Select(field => field.IsInitOnly).SequenceEqual(new[] { false, false, false, true, false, false }), "original field visibility and sole readonly name");
            Check(fields.Take(2).All(field => field.IsDefined(typeof(CompilerGeneratedAttribute), false)) && fields.Skip(2).All(field => !field.IsDefined(typeof(CompilerGeneratedAttribute), false)), "original event backing-field attributes only");
            Check(fields.Take(2).All(field => field.FieldType.GetGenericTypeDefinition() == typeof(Action<>)) && fields[2].FieldType.GetGenericTypeDefinition() == typeof(FastAction<>), "genuine callback and deferred field types");
            Check(fields[3].FieldType == typeof(string) && fields[4].FieldType == type.GetGenericArguments()[0] && fields[5].FieldType == typeof(ISystem), "original name and two independent system field types");
            Check(type.GetCustomAttributes(false).Length == 0 && (type.Attributes & TypeAttributes.BeforeFieldInit) != 0, "no invented type attributes or initializer");
            Check(type.GetGenericArguments()[0].GenericParameterAttributes == GenericParameterAttributes.ReferenceTypeConstraint && type.GetGenericArguments()[0].GetGenericParameterConstraints().SequenceEqual(new[] { typeof(ISystem) }), "original class and ISystem generic constraint");
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(method => method.MetadataToken).ToArray();
            Check(methods.Select(method => method.Name).SequenceEqual(new[] { "add_OnSystemStartup", "remove_OnSystemStartup", "add_OnSystemShutdown", "remove_OnSystemShutdown", "IsNull", "IsValid", "SystemName", "WaitOnSystem", "Get", "GetSafe", "TryGet", "TryGet", "Get", "GetSafe", "InvokeOnValid", "Hardlight.ISystemRef.InternalReplaceSystem", "Hardlight.ISystemRef.InternalRevokeSystem" }), "all original outer method order excluding separately reflected ctor");
            Check(methods.Take(4).All(method => method.IsDefined(typeof(CompilerGeneratedAttribute), false)), "four original compiler-generated event accessors");
            Check(type.GetConstructors().Length == 1 && type.GetConstructors()[0].GetParameters().Select(parameter => parameter.Name).SequenceEqual(new[] { "systemName", "system" }), "original constructor contract");
            foreach (var method in methods.Where(method => method.IsGenericMethodDefinition))
                Check(method.GetGenericArguments()[0].GenericParameterAttributes == GenericParameterAttributes.ReferenceTypeConstraint && method.GetGenericArguments()[0].GetGenericParameterConstraints().SequenceEqual(new[] { typeof(ISystem) }), "original requested-type class and ISystem constraints " + method.Name);
            var state = type.GetMethod("WaitOnSystem").GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
            Check(state.Name == "<WaitOnSystem>d__14" && state.DeclaringType == type, "natural original iterator identity without ordinal padding");
            Check(state.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(field => field.MetadataToken).Select(field => field.Name).SequenceEqual(new[] { "<>1__state", "<>2__current", "<>4__this" }), "all original three iterator fields");
            Check(state.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == 5 && state.GetConstructors().Length == 1, "original six generated iterator methods");
            Check(state.IsDefined(typeof(CompilerGeneratedAttribute), false) && state.GetInterfaces().Length == 3, "original generated attribute and three iterator interfaces");
            Check(typeof(SystemRef).BaseType == typeof(SystemRef<ISystem>) && typeof(SystemRef).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length == 0, "original zero-field derived SystemRef identity");
        }
        private static void CheckReads()
        {
            var a = new First("A"); var owner = new SystemRef<First>("literal name", a);
            Check(owner.IsValid() && !owner.IsNull() && owner.SystemName() == "literal name", "constructor raw identity and untyped validity");
            Check(ReferenceEquals(owner.Get(), a) && ReferenceEquals(owner.Get<First>(), a), "both checked getters return original reference");
            Check(owner.Get<Second>() == null, "valid untyped owner incompatible requested type returns null without logging");
            Check(ReferenceEquals(owner.GetSafe(), a) && ReferenceEquals(owner.GetSafe<First>(), a) && owner.GetSafe<Second>() == null, "safe conversions independently preserve typed reference");
            Check(owner.TryGet(out First found) && ReferenceEquals(found, a), "nongeneric TryGet fills out reference");
            Check(owner.TryGet<First>(out found) && ReferenceEquals(found, a), "generic TryGet preserves original reference");
            Check(!owner.TryGet<Second>(out Second other) && other == null, "incompatible TryGet clears out result");
            Set(owner, "m_system", null);
            Check(owner.IsValid() && owner.Get() == null && owner.Get<First>() == null, "valid untyped reference with null typed reference is silent");
            Check(!owner.TryGet(out found) && found == null && !owner.TryGet<First>(out found), "TryGet uses typed reference rather than untyped validity");
            Set(owner, "m_system", a); Set(owner, "m_isystem", null);
            Check(owner.IsNull() && ReferenceEquals(owner.GetSafe(), a) && owner.TryGet(out found), "safe getters permit deliberately inconsistent typed/untyped fixture");
            // No host is created on CLR runs; Unity's null/null comparison is managed.
            Check(Throws<NullReferenceException>(() => owner.Get(), "missing host checked get").Message == "System 'literal name' is null", "original nongeneric missing-host diagnostic message");
            Check(Throws<NullReferenceException>(() => owner.Get<First>(), "missing host generic checked get").Message == "System 'literal name' is null", "generic diagnosis follows untyped null even with a valid cast");
            var empty = new SystemRef<First>(null, null);
            Check(empty.SystemName() == null && empty.IsNull() && !empty.IsValid(), "null name preserved without sanitization");
            Check(Throws<NullReferenceException>(() => empty.Get(), "null name diagnosis").Message == "System '' is null", "original Concat null-name behavior");
            empty.InvokeOnValid(null); Check(Pending(empty) == null, "null deferred listener preserves null FastAction field");
            Replace(empty, new Second());
            Check(empty.IsValid() && empty.Get() == null && empty.Get<Second>() == null, "incompatible replacement keeps untyped validity while typed field is null");
            First argument = a; empty.InvokeOnValid(value => argument = value);
            Check(argument == null, "immediate callback casts untyped reference to original T");
            Throws<NullReferenceException>(() => empty.InvokeOnValid(null), "valid owner null callback propagates");
        }
        private static void CheckCallbacks()
        {
            var a = new First("A"); var b = new First("B"); var owner = new SystemRef<First>("callback", null); var trace = new List<string>();
            Action<First> startup = value => trace.Add("start:" + (value == null ? "null" : value.Name));
            owner.OnSystemStartup += startup; owner.OnSystemStartup += startup; owner.OnSystemStartup -= startup;
            owner.OnSystemShutdown += value => trace.Add("stop:" + value.Name);
            owner.InvokeOnValid(value => trace.Add("valid:" + value.Name));
            Replace(owner, a);
            Check(trace.SequenceEqual(new[] { "start:A", "valid:A" }), "startup before pending callback and duplicate event removal");
            Check(Pending(owner) == null, "successful replacement clears deferred field");
            trace.Clear(); Replace(owner, b);
            Check(trace.SequenceEqual(new[] { "start:B" }), "replacement has no shutdown event or repeated pending callback");
            Revoke(owner); Check(trace.SequenceEqual(new[] { "start:B", "stop:B" }) && owner.IsNull() && owner.GetSafe() == null, "shutdown receives old refs before both are cleared");
            Revoke(owner); Check(trace.Count == 2, "already revoked reference does not notify again");
            var reentrant = new SystemRef<First>("reentrant", null); trace.Clear();
            reentrant.InvokeOnValid(value => trace.Add("old:" + (value == null ? "null" : value.Name)));
            reentrant.OnSystemShutdown += value => trace.Add("shutdown:" + value.Name);
            reentrant.OnSystemStartup += value => { trace.Add("startup:" + value.Name); Revoke(reentrant); reentrant.InvokeOnValid(next => trace.Add("new:" + (next == null ? "null" : next.Name))); };
            Replace(reentrant, a);
            Check(trace.SequenceEqual(new[] { "startup:A", "shutdown:A", "old:null", "new:null" }), "post-startup re-read uses revoked typed field and callback additions");
            Check(reentrant.IsNull() && Pending(reentrant) == null, "successful outer replacement clears live pending field after reentrant revoke");
            var marker = new InvalidOperationException("marker"); var exceptional = new SystemRef<First>("throw", null); int calls = 0;
            exceptional.InvokeOnValid(value => { Check(ReferenceEquals(value, b), "retained pending callback sees later replacement"); calls++; });
            Action<First> fault = value => { throw marker; }; exceptional.OnSystemStartup += fault;
            Check(ReferenceEquals(Throws<InvalidOperationException>(() => Replace(exceptional, a), "startup exception"), marker), "startup exception identity propagated");
            Check(exceptional.IsValid() && ReferenceEquals(exceptional.GetSafe(), a) && Pending(exceptional) != null && calls == 0, "startup fault leaves replaced refs and pending callbacks intact");
            exceptional.OnSystemStartup -= fault; Replace(exceptional, b);
            Check(calls == 1 && Pending(exceptional) == null, "later successful replacement invokes retained callback");
            exceptional.OnSystemShutdown += fault;
            Check(ReferenceEquals(Throws<InvalidOperationException>(() => Revoke(exceptional), "shutdown exception"), marker), "shutdown callback exception identity propagated");
            Check(exceptional.IsValid() && ReferenceEquals(exceptional.GetSafe(), b), "shutdown fault prevents both reference clears");
            exceptional.OnSystemShutdown -= fault; Revoke(exceptional);
            var deferredFault = new SystemRef<First>("deferred fault", null); deferredFault.InvokeOnValid(value => { throw marker; });
            Check(ReferenceEquals(Throws<InvalidOperationException>(() => Replace(deferredFault, a), "deferred fault"), marker), "deferred exception propagates unchanged");
            Check(Pending(deferredFault) != null && deferredFault.IsValid(), "deferred fault prevents final pending clear");
            var incompatible = new SystemRef<First>("other", null); First received = a;
            incompatible.OnSystemStartup += value => received = value; Replace(incompatible, new Second());
            Check(received == null && incompatible.IsValid(), "startup gets null for incompatible untyped replacement");
            Set(incompatible, "m_system", a); Set(incompatible, "m_isystem", null); int shutdown = 0; incompatible.OnSystemShutdown += value => shutdown++;
            Revoke(incompatible); Check(shutdown == 0 && incompatible.GetSafe() == null, "revoke predicate is untyped field, not typed reference");
        }
        private static void CheckIterator()
        {
            var owner = new SystemRef<First>("wait", null); var iterator = owner.WaitOnSystem();
            Check(iterator.GetType().Name == "<WaitOnSystem>d__14", "constructed iterator uses original generated identity");
            Check(iterator.MoveNext() && iterator.Current == null && iterator.MoveNext() && iterator.Current == null, "wait repeatedly yields null while untyped system missing");
            ((IDisposable)iterator).Dispose(); Check(iterator.MoveNext(), "original empty Dispose does not stop suspended wait");
            Set(owner, "m_system", new First("typed only")); Check(iterator.MoveNext(), "waiting ignores inconsistent typed field");
            Replace(owner, new Second()); Check(!iterator.MoveNext(), "any non-null untyped replacement ends wait even when typed field is null");
            Revoke(owner); Check(!iterator.MoveNext(), "completed iterator does not restart after revoke");
            Throws<NotSupportedException>(() => iterator.Reset(), "original generated Reset");
            var valid = new SystemRef<First>("ready", new First("ready")); Check(!valid.WaitOnSystem().MoveNext(), "ready system produces no initial yield");
        }
        private static void CheckConcurrentEvents()
        {
            var owner = new SystemRef<First>("concurrency", null); int invocations = 0; Action<First> handler = value => Interlocked.Increment(ref invocations);
            const int workers = 4, repetitions = 100; var threads = new Thread[workers];
            for (int i = 0; i < workers; i++) { threads[i] = new Thread(() => { for (int j = 0; j < repetitions; j++) owner.OnSystemStartup += handler; }); threads[i].Start(); }
            foreach (var thread in threads) thread.Join();
            Replace(owner, new First("concurrent")); Check(invocations == workers * repetitions, "original event CAS preserves concurrent duplicate subscriptions");
            for (int i = 0; i < workers; i++) { threads[i] = new Thread(() => { for (int j = 0; j < repetitions; j++) owner.OnSystemStartup -= handler; }); threads[i].Start(); }
            foreach (var thread in threads) thread.Join();
            Replace(owner, new First("removed")); Check(invocations == workers * repetitions, "original event CAS preserves concurrent removals");
        }
        private static void CheckEngine()
        {
            Check(HLUnityCore.IsNull() && ProcessManager.IsSystemNull<HLUnityCore>(), "engine fixture begins without original logging host");
            var config = ScriptableObject.CreateInstance<HLUnityCoreConfigurationAsset>();
            var gameObject = new GameObject("Lucid original SystemRef logging fixture"); gameObject.SetActive(false);
            var host = gameObject.AddComponent<HLUnityCore>();
            var singleton = typeof(MonoSingleton<HLUnityCore>).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            object previousSingleton = singleton.GetValue(null);
            const string proofName = "ProjectLucid.SystemRef.GenuineHostFixture";
            var registry = (IDictionary)Field(typeof(ProcessManager), "s_systemDictionary").GetValue(null);
            var previousRows = new List<DictionaryEntry>(); foreach (DictionaryEntry row in registry) previousRows.Add(row);
            try
            {
                // Actual original engine component and registry, manually arranged to
                // isolate Get diagnostics. This does not assert its Awake lifecycle.
                singleton.SetValue(null, host); Field(typeof(HLUnityCore), "m_unityCoreConfigurationAsset").SetValue(host, config);
                ProcessManager.RegisterSystem(host, proofName);
                Check(ReferenceEquals(HLUnityCore.Instance, host) && ReferenceEquals(ProcessManager.GetSystem<HLUnityCore>(proofName), host), "genuine singleton and registry point to actual Unity component");
                var owner = new SystemRef<First>("engine name", null); int logs = 0; string message = null; string stack = null; int code = -1;
                Action<string, string, int> capture = (text, trace, value) => { logs++; message = text; stack = trace; code = value; };
                host.AddLogExceptionHandler(capture);
                Field(typeof(HLUnityCoreConfigurationAsset), "m_logInPlaceOfException").SetValue(config, false);
                Check(Throws<NullReferenceException>(() => owner.Get(), "configured throw").Message == "System 'engine name' is null" && logs == 0, "original disabled logging policy throws without transport");
                Field(typeof(HLUnityCoreConfigurationAsset), "m_logInPlaceOfException").SetValue(config, true);
                Check(owner.Get() == null && logs == 1 && message == "System 'engine name' is null" && stack == string.Empty && code == 0, "original real host receives nongeneric diagnostic payload and permits null return");
                Check(owner.Get<First>() == null && logs == 2, "generic diagnostic uses same actual host transport");
                host.RemoveLogExceptionHandler(capture);
                var replacement = new First("callback replacement");
                Action<string, string, int> replace = (text, trace, value) => Replace(owner, replacement);
                host.AddLogExceptionHandler(replace);
                Check(ReferenceEquals(owner.Get(), replacement), "nongeneric checked getter reloads typed field after registered diagnostic callback");
                Revoke(owner); Check(ReferenceEquals(owner.Get<First>(), replacement), "generic checked getter reloads typed field after registered diagnostic callback");
                host.RemoveLogExceptionHandler(replace); Revoke(owner);
                var fault = new InvalidOperationException("engine callback fault"); Action<string, string, int> thrower = (text, trace, value) => { throw fault; }; host.AddLogExceptionHandler(thrower);
                Check(ReferenceEquals(Throws<InvalidOperationException>(() => owner.Get(), "registered callback fault"), fault), "logging handler failure propagates through checked getter");
                host.RemoveLogExceptionHandler(thrower);
                var unityOwner = new SystemRef<HLUnityCore>("destroyed wrapper", host);
                ProcessManager.UnregisterSystem(proofName);
                singleton.SetValue(null, previousSingleton);
                UnityEngine.Object.DestroyImmediate(gameObject); gameObject = null;
                Check(host == null && !ReferenceEquals(host, null), "actual destroyed Unity wrapper differs from managed null");
                Check(unityOwner.IsValid() && !unityOwner.IsNull(), "original interface validity retains destroyed Unity wrapper");
                Check(ReferenceEquals(unityOwner.Get(), host) && ReferenceEquals(unityOwner.Get<HLUnityCore>(), host), "checked getters preserve destroyed wrapper without invoking diagnostic");
                Check(unityOwner.TryGet(out HLUnityCore result) && ReferenceEquals(result, host), "generic reference constraint preserves CLR TryGet semantics on destroyed wrapper");
            }
            finally
            {
                try { ProcessManager.UnregisterSystem(proofName); }
                finally
                {
                    try { singleton.SetValue(null, previousSingleton); registry.Clear(); foreach (var row in previousRows) registry.Add(row.Key, row.Value); }
                    finally
                    {
                        try { if (!ReferenceEquals(gameObject, null)) UnityEngine.Object.DestroyImmediate(gameObject); }
                        finally { UnityEngine.Object.DestroyImmediate(config); }
                    }
                }
            }
        }
    }
}
