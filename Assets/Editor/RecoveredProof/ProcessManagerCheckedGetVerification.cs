using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    public static class ProcessManagerCheckedGetVerification
    {
        private sealed class First : ISystem { public readonly string Name; public First(string name) { Name = name; } }
        private sealed class Second : ISystem { }
        private static int checks;
        private static void Check(bool condition, string message) { if (!condition) throw new Exception("ProcessManager checked-get verification: " + message); checks++; }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        private static IDictionary Registry => (IDictionary)Field(typeof(ProcessManager), "s_systemDictionary").GetValue(null);
        private static List<DictionaryEntry> CopyRows(IDictionary registry) { var rows = new List<DictionaryEntry>(); foreach (DictionaryEntry row in registry) rows.Add(row); return rows; }
        private static void RestoreRows(IDictionary registry, List<DictionaryEntry> rows) { registry.Clear(); foreach (var row in rows) registry.Add(row.Key, row.Value); }
        private static TError Throws<TError>(Action action, string message) where TError : Exception
        {
            try { action(); } catch (TError error) { checks++; return error; }
            throw new Exception("ProcessManager checked-get expected " + typeof(TError).Name + ": " + message);
        }
        public static int RunManaged()
        {
            checks = 0;
            CheckDeclarations(); CheckManagedLookup();
            return checks;
        }
        public static void Run()
        {
            RunManaged(); CheckEngine();
            Debug.Log("PASS original ProcessManager checked-get bounded checks=" + checks + "; registered host fixtures executed; no authored startup claim.");
        }
        private static void CheckDeclarations()
        {
            MethodInfo method = typeof(ProcessManager).GetMethods(BindingFlags.Public | BindingFlags.Static).Single(m => m.Name == "GetSystem" && m.IsGenericMethodDefinition);
            Check((int)method.Attributes == 150 && (int)method.GetMethodImplementationFlags() == 0, "original public static HideBySig method flags");
            Check(method.GetCustomAttributes(false).Length == 0, "original method has no custom attributes");
            Type generic = method.GetGenericArguments().Single();
            Check(generic.Name == "T" && generic.GenericParameterPosition == 0 && generic.GenericParameterAttributes == GenericParameterAttributes.ReferenceTypeConstraint, "original one class-constrained T");
            Check(generic.GetGenericParameterConstraints().SequenceEqual(new[] { typeof(ISystem) }) && method.ReturnType == generic, "genuine ISystem constraint and generic return");
            ParameterInfo[] parameters = method.GetParameters();
            Check(parameters.Select(p => p.Name).SequenceEqual(new[] { "systemName", "autoRegister" }) && parameters.Select(p => p.ParameterType).SequenceEqual(new[] { typeof(string), typeof(bool) }), "original two parameter names/types/order");
            Check(parameters.All(p => (int)p.Attributes == 4112), "original Optional and HasDefault parameter flags");
            Check(parameters[0].DefaultValue == null && Equals(parameters[1].DefaultValue, true), "original null and true defaults");
            Check(parameters.All(p => p.GetCustomAttributes(false).All(attribute => attribute is System.Runtime.InteropServices.OptionalAttribute)), "reflection exposes only flag-derived Optional pseudo attributes; actual custom rows checked separately");
        }
        private static void CheckManagedLookup()
        {
            Check(HLUnityCore.IsNull(), "managed fixture starts without Unity logging host");
            IDictionary registry = Registry; var rows = CopyRows(registry);
            try
            {
                registry.Clear(); var first = new First("first"); var second = new First("replacement");
                string defaultName = typeof(First).ToString();
                SystemRef original = ProcessManager.RegisterSystem(first);
                Check(original.SystemName() == defaultName && registry.Count == 1, "genuine default-name registry setup");
                Check(ReferenceEquals(ProcessManager.GetSystem<First>(), first), "null name uses original Type.ToString default");
                Check(ReferenceEquals(ProcessManager.GetSystem<First>(defaultName, false), first), "false auto-register still retrieves existing captured row");
                Check(ReferenceEquals(ProcessManager.GetSystemRef(defaultName), original), "checked read preserves row reference identity");
                Check(ProcessManager.GetSystem<Second>(defaultName) == null && original.IsValid(), "non-null incompatible requested type returns null without diagnostic");
                var named = ProcessManager.RegisterSystem(first, "literal key");
                Check(ReferenceEquals(ProcessManager.GetSystem<First>("literal key"), first), "supplied key remains literal");
                Check(ReferenceEquals(ProcessManager.GetSystemRef("literal key"), named), "same system alias retains separate reference");
                var error = Throws<NullReferenceException>(() => ProcessManager.GetSystem<First>("missing"), "absent registered row");
                Check(error.Message == "System 'missing' is null", "original first diagnostic exact pinned message");
                Check(registry.Contains("missing"), "default auto-register retains null row before diagnostic throws");
                var missing = ProcessManager.GetSystemRef("missing", false);
                Check(missing.IsNull() && missing.SystemName() == "missing", "retained empty reference identity/name");
                Check(Throws<NullReferenceException>(() => ProcessManager.GetSystem<First>("missing"), "repeat null row").Message == error.Message, "repeat checked read keeps original diagnosis");
                Check(ReferenceEquals(ProcessManager.GetSystemRef("missing", false), missing), "repeat failure does not replace captured row");
                int count = registry.Count;
                Check(Throws<NullReferenceException>(() => ProcessManager.GetSystem<First>("detached", false), "absent detached row").Message == "System 'detached' is null", "detached first diagnosis preserves supplied key");
                Check(registry.Count == count && !registry.Contains("detached"), "disabled auto-register does not add detached reference");
                Check(Throws<NullReferenceException>(() => ProcessManager.GetSystem<First>(string.Empty), "empty key").Message == "System '' is null", "empty string is not default-name substitution");
                Check(registry.Contains(string.Empty), "raw empty key remains a retained row");
                ProcessManager.UnregisterSystem("literal key");
                Check(named.IsNull() && ReferenceEquals(ProcessManager.GetSystemRef("literal key", false), named), "original unregistration retains captured row");
                Check(Throws<NullReferenceException>(() => ProcessManager.GetSystem<First>("literal key"), "revoked row").Message == "System 'literal key' is null", "revoked row diagnoses under original name");
                Check(ReferenceEquals(ProcessManager.RegisterSystem(second, "literal key"), named), "original replacement fills same retained row");
                Check(ReferenceEquals(ProcessManager.GetSystem<First>("literal key"), second), "checked lookup observes replaced captured reference");
                ProcessManager.UnregisterSystem(defaultName);
                Check(Throws<NullReferenceException>(() => ProcessManager.GetSystem<First>(), "revoked default").Message == "System '" + defaultName + "' is null", "null-name missing diagnostic uses full original default name");
            }
            finally { RestoreRows(registry, rows); }
        }
        private static void CheckEngine()
        {
            Check(HLUnityCore.IsNull(), "engine fixture starts without logging singleton");
            IDictionary registry = Registry; var rows = CopyRows(registry);
            var singleton = typeof(MonoSingleton<HLUnityCore>).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            object previousSingleton = singleton.GetValue(null);
            var config = ScriptableObject.CreateInstance<HLUnityCoreConfigurationAsset>();
            var gameObject = new GameObject("Lucid original ProcessManager checked-get fixture"); gameObject.SetActive(false);
            var host = gameObject.AddComponent<HLUnityCore>();
            const string hostName = "ProjectLucid.ProcessManager.GenuineHostFixture";
            const string prefix = "ProjectLucid.ProcessManager.CheckedGet.";
            var messages = new List<string>(); var stacks = new List<string>(); var codes = new List<int>();
            Action<string, string, int> capture = (message, stack, code) => { messages.Add(message); stacks.Add(stack); codes.Add(code); };
            try
            {
                // Genuine inactive engine component; manually arranged singleton/config
                // isolates lookup callbacks and does not assert Awake startup behavior.
                singleton.SetValue(null, host); Field(typeof(HLUnityCore), "m_unityCoreConfigurationAsset").SetValue(host, config);
                ProcessManager.RegisterSystem(host, hostName);
                Check(ReferenceEquals(HLUnityCore.Instance, host) && ReferenceEquals(ProcessManager.GetSystem<HLUnityCore>(hostName), host), "genuine host and named registry row");
                host.AddLogExceptionHandler(capture);
                Field(typeof(HLUnityCoreConfigurationAsset), "m_logInPlaceOfException").SetValue(config, false);
                Check(Throws<NullReferenceException>(() => ProcessManager.GetSystem<First>(prefix + "throw"), "configured throw").Message == "System '" + prefix + "throw' is null" && messages.Count == 0, "false policy throws before transport or inner getter");
                Field(typeof(HLUnityCoreConfigurationAsset), "m_logInPlaceOfException").SetValue(config, true);
                Check(ProcessManager.GetSystem<First>(prefix + "twice") == null && messages.Count == 2, "unchanged empty captured reference reports caller and inner getter diagnostics");
                Check(messages.All(message => message == "System '" + prefix + "twice' is null") && stacks.All(stack => stack == string.Empty) && codes.All(code => code == 0), "both exact native diagnostic payloads");
                messages.Clear(); stacks.Clear(); codes.Clear();
                string sameName = prefix + "same"; var captured = ProcessManager.GetSystemRef(sameName); var replacement = new First("callback");
                Action<string, string, int> same = (message, stack, code) => ProcessManager.RegisterSystem(replacement, sameName);
                host.AddLogExceptionHandler(same);
                try { Check(ReferenceEquals(ProcessManager.GetSystem<First>(sameName), replacement) && messages.Count == 1, "callback fills same captured reference before inner predicate"); }
                finally { host.RemoveLogExceptionHandler(same); }
                Check(ReferenceEquals(ProcessManager.GetSystemRef(sameName), captured) && ReferenceEquals(captured.GetSafe(), replacement), "same-row mutation preserves captured identity");
                messages.Clear(); string detachedName = prefix + "detached";
                Action<string, string, int> detached = (message, stack, code) => { if (messages.Count == 1) ProcessManager.RegisterSystem(replacement, detachedName); };
                host.AddLogExceptionHandler(detached);
                try { Check(ProcessManager.GetSystem<First>(detachedName, false) == null && messages.Count == 2, "detached captured reference stays null after callback creates new registry row"); }
                finally { host.RemoveLogExceptionHandler(detached); }
                Check(ReferenceEquals(ProcessManager.GetSystemRef(detachedName).GetSafe(), replacement), "new registry row is valid while detached checked result stayed null");
                messages.Clear(); string changedName = prefix + "changed"; var old = ProcessManager.GetSystemRef(changedName);
                Action<string, string, int> changed = (message, stack, code) => { if (messages.Count == 1) { registry.Remove(changedName); ProcessManager.RegisterSystem(replacement, changedName); } };
                host.AddLogExceptionHandler(changed);
                try { Check(ProcessManager.GetSystem<First>(changedName) == null && messages.Count == 2, "removed/recreated dictionary row is not re-queried after diagnostic callback"); }
                finally { host.RemoveLogExceptionHandler(changed); }
                Check(old.IsNull() && !ReferenceEquals(ProcessManager.GetSystemRef(changedName), old), "original captured reference remains detached from replacement dictionary row");
                Check(ReferenceEquals(ProcessManager.GetSystemRef(changedName).GetSafe(), replacement), "replacement row retains valid system despite null checked return");
                messages.Clear(); string policyName = prefix + "policy";
                Action<string, string, int> policy = (message, stack, code) => Field(typeof(HLUnityCoreConfigurationAsset), "m_logInPlaceOfException").SetValue(config, false);
                host.AddLogExceptionHandler(policy);
                try { Check(Throws<NullReferenceException>(() => ProcessManager.GetSystem<First>(policyName), "inner policy change").Message == "System '" + policyName + "' is null" && messages.Count == 1, "inner getter re-reads host policy after first callback"); }
                finally { host.RemoveLogExceptionHandler(policy); }
                Field(typeof(HLUnityCoreConfigurationAsset), "m_logInPlaceOfException").SetValue(config, true);
                messages.Clear(); var fault = new InvalidOperationException("registered callback fault");
                Action<string, string, int> thrower = (message, stack, code) => { throw fault; };
                host.AddLogExceptionHandler(thrower);
                try { Check(ReferenceEquals(Throws<InvalidOperationException>(() => ProcessManager.GetSystem<First>(prefix + "fault"), "handler fault"), fault) && messages.Count == 1, "callback fault propagates before inner getter and second notification"); }
                finally { host.RemoveLogExceptionHandler(thrower); }
                Check(ProcessManager.GetSystemRef(prefix + "fault").IsNull(), "callback fault retains preceding null-row registration");
                messages.Clear(); ProcessManager.RegisterSystem(new Second(), prefix + "incompatible");
                Check(ProcessManager.GetSystem<First>(prefix + "incompatible") == null && messages.Count == 0, "valid incompatible system is silent under real host");
            }
            finally
            {
                try { host.RemoveLogExceptionHandler(capture); }
                finally
                {
                    try { ProcessManager.UnregisterSystem(hostName); }
                    finally
                    {
                        try { singleton.SetValue(null, previousSingleton); }
                        finally
                        {
                            try { RestoreRows(registry, rows); }
                            finally
                            {
                                try { UnityEngine.Object.DestroyImmediate(gameObject); }
                                finally { UnityEngine.Object.DestroyImmediate(config); }
                            }
                        }
                    }
                }
            }
        }
    }
}
