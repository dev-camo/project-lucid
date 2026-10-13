using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using Hardlight;
using Hardlight.Enums;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Table = Hardlight.Localisation.StringTable;
using Entry = Hardlight.Localisation.StringTable.StringEntry;
using Definition = ClientDataAPI.LocalisationDefinitions;
using BufferTable = ClientDataAPI.StringTable;

namespace ProjectLucid
{
    // Bounded source/metadata/callback proof. Managed fixtures use genuine
    // original objects without Unity lifecycle; engine and supplied-content
    // loading require their own actual tests and do not follow from this proof.
    public static class StringTableVerification
    {
        private static int checks;
        private static readonly BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static readonly Type T = typeof(Table);
        private static FieldInfo Field(Type t, string name) => t.GetField(name, Declared);
        private static MethodInfo Method(string name) => T.GetMethod(name, Declared);
        private static void Check(bool value, string message) { ++checks; if (!value) throw new InvalidOperationException(message); }
        private static object Invoke(object owner, string name, params object[] arguments)
        {
            try { return Method(name).Invoke(owner, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        private static bool Throws<E>(Action action) where E : Exception
        { try { action(); return false; } catch (E) { return true; } }
        private static Table Fixture() => (Table)FormatterServices.GetUninitializedObject(T);
        private static ClientDataAPI.LocalisedString Value(string id, string content, int args = 0) => new ClientDataAPI.LocalisedString { Id = id, Content = content, NumArgs = args };
        private static BufferTable Buffer(string hash, params ClientDataAPI.LocalisedString[] values) => new BufferTable { Hash = hash, Strings = values };
        private static Definition DefinitionFor(Languages language) => new Definition { SupportedLanguages = new ClientDataAPI.SupportedLanguage[0], StringTable = new BufferTable { Language = (int)language, Hash = "bounded-callback", Strings = new ClientDataAPI.LocalisedString[0] } };

        private static FieldInfo[] DataStatics() => T.GetFields(Declared).Where(f => f.IsStatic && !f.IsLiteral &&
            f.Name != "ShouldInvokeStringHandlers" && f.Name != "OnRequestStringTable" && f.Name != "StringsTableHandlers").ToArray();

        private static void WithCleanup(Action body, params Action[] cleanup)
        {
            Exception primary = null;
            var failures = new List<Exception>();
            try { body(); }
            catch (Exception failure) { primary = failure; }
            foreach (Action release in cleanup)
                try { release(); }
                catch (Exception failure) { failures.Add(failure); }
            if (primary != null)
            {
                if (failures.Count != 0) primary.Data["StringTableFixtureCleanupFailures"] = new AggregateException(failures);
                ExceptionDispatchInfo.Capture(primary).Throw();
            }
            if (failures.Count != 0) throw new AggregateException(failures);
        }

        private sealed class CallbackSubscriptions : IDisposable
        {
            private readonly Func<bool> initializer;
            private Func<bool> readiness;
            private readonly List<Action> requests = new List<Action>();
            private readonly List<Action> handlers = new List<Action>();
            private readonly List<FastAction> releasedHandlers = new List<FastAction>();
            private FastAction ownedHandlers;

            public CallbackSubscriptions()
            {
                initializer = (Func<bool>)Field(T, "ShouldInvokeStringHandlers").GetValue(null);
                RequireOriginalReadiness(initializer);
                Table.ShouldInvokeStringHandlers -= initializer;
            }

            public void SetReadiness(Func<bool> callback)
            {
                if (readiness != null) Table.ShouldInvokeStringHandlers -= readiness;
                readiness = null;
                if (callback != null)
                {
                    readiness = callback;
                    Table.ShouldInvokeStringHandlers += callback;
                }
            }

            public void AddRequest(Action callback) { requests.Add(callback); Table.OnRequestStringTable += callback; }
            public void RemoveRequest(Action callback) { Table.OnRequestStringTable -= callback; requests.Remove(callback); }
            public void ClearRequests()
            {
                foreach (Action callback in requests) Table.OnRequestStringTable -= callback;
                requests.Clear();
            }

            public void AddHandler(Action callback)
            {
                if (!ReferenceEquals(Table.StringsTableHandlers, ownedHandlers))
                    throw new InvalidOperationException("StringTable handler owner changed during fixture.");
                handlers.Add(callback);
                Table.StringsTableHandlers += callback;
                ownedHandlers = Table.StringsTableHandlers;
            }

            public void ClearHandlers()
            {
                if (ownedHandlers == null) return;
                if (!ReferenceEquals(Table.StringsTableHandlers, ownedHandlers))
                    throw new InvalidOperationException("StringTable handler owner changed during cleanup.");
                Type storage = ownedHandlers.GetType().BaseType;
                IEnumerable<Action>[] lists = {
                    ownedHandlers.GetInvocationList(),
                    (IEnumerable<Action>)storage.GetField("m_addedList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ownedHandlers),
                    (IEnumerable<Action>)storage.GetField("m_removedList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ownedHandlers) };
                if (lists.Any(list => list.Any(callback => !handlers.Any(owned => ReferenceEquals(callback, owned)))))
                    throw new InvalidOperationException("StringTable owned handler object contains a foreign callback.");
                foreach (Action callback in handlers) Table.StringsTableHandlers -= callback;
                // Keep the old object and its fault state for this scope. Public
                // unsubscription may queue removals; do not repair/flush them.
                releasedHandlers.Add(ownedHandlers);
                Table.StringsTableHandlers = null;
                ownedHandlers = null;
                handlers.Clear();
            }

            public void Dispose()
            {
                WithCleanup(() => { }, ClearHandlers, ClearRequests, () => SetReadiness(null), () => Table.ShouldInvokeStringHandlers += initializer);
            }
        }

        private static IEnumerable<(int offset, OpCode op, object value)> IL(MethodBase method)
        {
            var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Value);
            byte[] bytes = method.GetMethodBody().GetILAsByteArray();
            for (int position = 0; position < bytes.Length;)
            {
                int start = position; short key = bytes[position++]; if (key == 0xfe) key = (short)(0xfe00 | bytes[position++]);
                OpCode code = codes[key]; object operand = null;
                switch (code.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineI: operand = (sbyte)bytes[position++]; break;
                    case OperandType.ShortInlineVar: operand = bytes[position++]; break;
                    case OperandType.InlineVar: operand = BitConverter.ToUInt16(bytes, position); position += 2; break;
                    case OperandType.InlineI: operand = BitConverter.ToInt32(bytes, position); position += 4; break;
                    case OperandType.ShortInlineR: operand = BitConverter.ToSingle(bytes, position); position += 4; break;
                    case OperandType.InlineR: operand = BitConverter.ToDouble(bytes, position); position += 8; break;
                    case OperandType.ShortInlineBrTarget: operand = (sbyte)bytes[position++] + position; break;
                    case OperandType.InlineBrTarget: operand = BitConverter.ToInt32(bytes, position) + position + 4; position += 4; break;
                    case OperandType.InlineString: operand = method.Module.ResolveString(BitConverter.ToInt32(bytes, position)); position += 4; break;
                    case OperandType.InlineType: operand = method.Module.ResolveType(BitConverter.ToInt32(bytes, position)); position += 4; break;
                    case OperandType.InlineTok: operand = method.Module.ResolveMember(BitConverter.ToInt32(bytes, position)); position += 4; break;
                    case OperandType.InlineMethod: operand = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, position)); position += 4; break;
                    case OperandType.InlineField: operand = method.Module.ResolveField(BitConverter.ToInt32(bytes, position)); position += 4; break;
                    case OperandType.InlineSwitch: int count = BitConverter.ToInt32(bytes, position); position += 4 + count * 4; operand = count; break;
                    default: throw new InvalidOperationException("Unrecognized bounded StringTable IL operand " + code);
                }
                yield return (start, code, operand);
            }
        }
        private static MethodBase[] Calls(MethodBase method) => IL(method).Where(i => i.value is MethodBase && (i.op == OpCodes.Call || i.op == OpCodes.Callvirt)).Select(i => (MethodBase)i.value).ToArray();
        private static void Metadata()
        {
            Check(T.Assembly.GetName().Name == "HLLocalisation.Runtime" && T.IsPublic && !T.IsSealed && !T.IsAbstract && T.BaseType == typeof(MonoSingleton<Table>), "original public concrete singleton type and assembly");
            var options = T.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
            Check(options.Length == 2 && T.GetCustomAttributesData().Count == 2 && options[0].Option == Option.NullChecks && options[1].Option == Option.ArrayBoundsChecks && options.All(o => Equals(o.Value, false)), "exact ordered native class options");
            string[] fields = { "StringsTableHandlers", "ShouldInvokeStringHandlers", "<Hash>k__BackingField", "<StringsLanguage>k__BackingField", "PlusOrMinusNoDecimalFormat", "PlusOrMinusOneDecimalFormat", "PlusOrMinusTwoDecimalFormat", "PlusOrMinusThreeDecimalFormat", "m_coroutineDelayingStringHandlerInvocation", "<NumberFormat>k__BackingField", "OnRequestStringTable", "m_supportedLanguages", "m_strings", "m_cachedAtTime", "StringTableCachedTimeSaveProperty", "CacheFilePrefix", "CacheVersionPostfix", "m_config" };
            Type[] types = { typeof(FastAction), typeof(Func<bool>), typeof(string), typeof(Languages), typeof(string), typeof(string), typeof(string), typeof(string), typeof(Coroutine), typeof(NumberFormatInfo), typeof(Action), typeof(ClientDataAPI.SupportedLanguage[]), typeof(Dictionary<int, Entry>), typeof(long), typeof(string), typeof(string), typeof(string), typeof(HLLocalisationConfigurationAsset) };
            int[] attributes = { 22,17,17,17,32854,32854,32854,32854,1,1,17,1,1,1,32849,32849,32849,1 };
            FieldInfo[] own = T.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            Check(own.Select(f => f.Name).SequenceEqual(fields), "all eighteen original fields in declaration order");
            for (int i = 0; i < own.Length; ++i) Check(own[i].FieldType == types[i] && (int)own[i].Attributes == attributes[i], fields[i] + " exact type and attributes");
            Check(own.All(f => !f.IsDefined(typeof(SerializeField), false)), "original outer fields have no invented Unity SerializeField attribute");
            Check(Table.PlusOrMinusNoDecimalFormat == "{0:+0;-0;0}" && Table.PlusOrMinusOneDecimalFormat == "{0:+0.#;-0.#;0}" && Table.PlusOrMinusTwoDecimalFormat == "{0:+0.##;-0.##;0}" && Table.PlusOrMinusThreeDecimalFormat == "{0:+0.###;-0.###;0}", "all four original formatting literals");
            Check((string)Field(T,"StringTableCachedTimeSaveProperty").GetRawConstantValue() == "StringTableCachedTime" && (string)Field(T,"CacheFilePrefix").GetRawConstantValue() == "Text-" && (string)Field(T,"CacheVersionPostfix").GetRawConstantValue() == "-Version", "exact save/cache key literals");
            Check((int)Strings.NONE == -1225125244 && (int)Strings.NUMBER_SEPARATOR == 2113991425 && (int)Strings.DECIMAL_SEPARATOR == -2013010610, "native sentinel and separator identities, no guessed lookup names");
            Check(T.GetProperties(Declared).Select(p => p.Name).SequenceEqual(new[] { "Hash", "StringsLanguage", "NumberFormat" }) && T.GetProperties(Declared).All(p => p.GetMethod.IsPublic && p.GetSetMethod(true).IsPrivate), "three original private-set properties");
            Check(T.GetEvents(Declared).Select(e => e.Name).SequenceEqual(new[] { "ShouldInvokeStringHandlers", "OnRequestStringTable" }), "two original static events");
            Type[] nested = T.GetNestedTypes(Declared).OrderBy(n => n.MetadataToken).ToArray();
            string[] nestedNames = { "StringEntry", "<>c", "<DelayedStringLoad>d__54", "<LoadLocalisationDefinitions>d__62", "<WaitForStrings>d__57", "<WaitOnProjectAssets>d__52" };
            int[] fieldCounts = { 3,1,3,7,2,4 }, methodCounts = { 1,3,6,7,6,6 };
            Check(nested.Select(n => n.Name).SequenceEqual(nestedNames), "all original generated names and natural declaration order");
            for (int i = 0; i < nested.Length; ++i) Check(nested[i].GetFields(Declared).Length == fieldCounts[i] && nested[i].GetMethods(Declared).Length + nested[i].GetConstructors(Declared).Length == methodCounts[i], nestedNames[i] + " exact field/method counts");
            Check(nested.Single(n => n.Name == "<>c").GetMethods(Declared).Single(m => !m.IsSpecialName).Name == "<.cctor>b__69_0", "original natural static readiness closure ordinal");
            Check(typeof(Entry).IsValueType && typeof(Entry).IsNestedPublic && typeof(Entry).GetFields(Declared).Select(f => f.Name).SequenceEqual(new[] { "id", "content", "numArgs" }) && typeof(Entry).GetFields(Declared).All(f => f.IsPublic && f.IsInitOnly && !f.IsStatic), "complete original readonly entry struct");
            Check(T.GetMethods(Declared).Length + T.GetConstructors(Declared).Length == 48 && nested.Sum(n => n.GetMethods(Declared).Length + n.GetConstructors(Declared).Length) == 29, "exact original forty-eight outer and twenty-nine nested declarations");
            Check(Method("IsDirty").IsFamily && Method("IsDirty").IsVirtual && Method("DelayedStringLoad").IsFamily && Method("DelayedStringLoad").IsVirtual && Method("GetClientVersion").IsFamily && Method("GetClientVersion").IsVirtual, "all three original protected virtual boundaries");
            var load = Method("LoadLanguageDefinition"); var callback = load.GetParameters().Last();
            Check(callback.IsOptional && callback.DefaultValue == null && callback.ParameterType == typeof(Action<Definition>), "exact original optional callback default");
        }
        private static void NativeOrderingContracts()
        {
            var sync = Method("LoadLanguageDefinition"); var clauses = sync.GetMethodBody().ExceptionHandlingClauses;
            Check(clauses.Count(c => c.Flags == ExceptionHandlingClauseOptions.Clause && c.CatchType == typeof(Exception)) == 2 && clauses.All(c => c.Flags != ExceptionHandlingClauseOptions.Clause || c.CatchType == typeof(Exception)), "native two independent System.Exception catches");
            foreach (string name in new[] { "decode", "CustomKeyString" })
            {
                var call = IL(sync).Single(i => i.value is MethodInfo m && m.Name == name);
                Check(!clauses.Any(c => c.Flags == ExceptionHandlingClauseOptions.Clause && call.offset >= c.TryOffset && call.offset < c.TryOffset + c.TryLength), name + " remains outside native fallback catches");
            }
            Check(IL(sync).Count(i => i.op == OpCodes.Newobj && i.value is ConstructorInfo c && c.DeclaringType == typeof(System.IO.BinaryReader)) == 1 && !Calls(sync).Any(c => c.DeclaringType == typeof(System.IO.BinaryReader) && c.Name == "Dispose"), "native only FileStream owns explicit disposal; no added BinaryReader disposal");
            Check(!Calls(sync).Any(c => c.DeclaringType == typeof(Action<Definition>) && c.Name == "Invoke"), "original synchronous route forwards fallback callback without invoking it");
            var objectUpdate = T.GetMethod("UpdateLocalisationDefinition", Declared, null, new[] { typeof(Definition) }, null);
            Check(Calls(objectUpdate).Where(c => c.DeclaringType == typeof(Definition)).Select(c => c.Name).SequenceEqual(new[] { "GetEncodingInstance", "encode" }), "native object-update uses reset encoding singleton rather than supplied argument");
            foreach (var method in T.GetMethods(Declared).Where(m => m.Name == "UpdateLocalisationDefinition").Concat(new[] { Method("SaveLocalisationDefinitionToCache") }))
                Check(method.GetMethodBody().ExceptionHandlingClauses.Count(c => c.Flags == ExceptionHandlingClauseOptions.Clause && c.CatchType == typeof(Exception)) == 1, method + " exact silent System.Exception boundary");
            Check(Method("LoadLocalisationDefinitionFromCache").GetMethodBody().ExceptionHandlingClauses.Count == 0, "native cache reader itself does not catch exceptions");
            Check(Calls(Method("LoadLocalisationDefinitionFromCache")).Take(3).Select(c => c.Name).SequenceEqual(new[] { "get_Instance", "IsDirty", "GetString" }), "native cache virtual gate precedes language registry/path/IO");
            var cacheReader = Method("LoadLocalisationDefinitionFromCache");
            string[] cacheCallNames = { "get_Instance", "IsDirty", "GetString", "GetCachePathVersion", "Exists", "ReadAllBytes", "get_ASCII", "GetString", "IsNullOrEmpty", "get_Instance", "GetClientVersion", "IsVersionUpToDate", "GetCachePath", "Exists", "ReadAllBytes" };
            Type[] cacheCallOwners = { typeof(MonoSingleton<Table>), T, typeof(EnumExtentions), T, typeof(System.IO.File), typeof(System.IO.File), typeof(System.Text.Encoding), typeof(System.Text.Encoding), typeof(string), typeof(MonoSingleton<Table>), T, typeof(VersionChecker), T, typeof(System.IO.File), typeof(System.IO.File) };
            Check(Calls(cacheReader).Select(c => (c.Name, c.DeclaringType)).SequenceEqual(cacheCallNames.Select((name, i) => (name, cacheCallOwners[i]))), "native complete cache-read call graph reads version bytes before ASCII/GetString, then virtual client-version validation before data-path existence/read");
            Check(IL(cacheReader).Where(i => i.value is MethodInfo m && m.DeclaringType == T && (m.Name == "IsDirty" || m.Name == "GetClientVersion")).All(i => i.op == OpCodes.Callvirt), "native cache dirty/client-version calls retain virtual dispatch inside uncaught cache-reader boundary");
            var asyncLoad = T.GetNestedType("<LoadLocalisationDefinitions>d__62", Declared).GetMethod("MoveNext", Declared);
            var asyncIL = IL(asyncLoad).ToArray();
            int decodeAt = Array.FindIndex(asyncIL, i => i.value is MethodInfo m && m.DeclaringType == typeof(Definition) && m.Name == "decode");
            int callbackLoadAt = Array.FindIndex(asyncIL, decodeAt + 1, i => i.op == OpCodes.Ldfld && i.value is FieldInfo f && f.DeclaringType == asyncLoad.DeclaringType && f.Name == "callback");
            int callbackInvokeAt = Array.FindIndex(asyncIL, i => i.value is MethodInfo m && m.DeclaringType == typeof(Action<Definition>) && m.Name == "Invoke");
            Check(decodeAt >= 0 && decodeAt < callbackLoadAt && callbackLoadAt < callbackInvokeAt && !asyncIL.Take(decodeAt).Any(i => i.op == OpCodes.Ldfld && i.value is FieldInfo f && f.DeclaringType == asyncLoad.DeclaringType && f.Name == "callback"), "native asynchronous decoder completes before captured callback reload/invocation; callback schedule preserves original seven-field closure");
            Check(Calls(Method("OnDestroy")).Select(c => c.Name).SequenceEqual(new[] { "RemoveLoadHandler", "RemoveSaveHandler", "remove_OnLanguageChanged", "OnDestroy" }), "original destruction order and no invented deferred coroutine stop");
            Check(Calls(Method("Start")).Select(c => c.Name).SequenceEqual(new[] { "GetConfig", "AddLoadHandler", "AddSaveHandler", "add_OnLanguageChanged", "get_Instance", "get_LanguageLoaded", "GetLanguage", "OnLanguageChanged" }), "original config/load/save/language subscription order");
            Check(Calls(Method("InternalSetStrings")).Select(c => c.Name).SequenceEqual(new[] { "get_InvariantCulture", "get_NumberFormat", "Clone", "set_NumberFormat", "get_NumberFormat", "GetString", "set_NumberGroupSeparator", "get_NumberFormat", "GetString", "set_NumberDecimalSeparator" }), "native dictionary/number-format/separator assignment order");
            Check(Calls(Method("OnLanguageDefinitionLoaded")).Select(c => c.Name).Take(6).SequenceEqual(new[] { "get_SupportedLanguages", "get_StringTable", "ReadStringsFromBuffer", "InternalSetStrings", "Invoke", "InvokeStringTableHandlers" }), "native supported-language mutation precedes table/hash and readiness callback");
            Check(Calls(Method("InvokeStringTableHandlers")).Select(c => c.Name).SequenceEqual(new[] { "get_StringTable", "get_Language", "set_StringsLanguage", "Invoke", "RequestStringTable" }), "native language/handler/request order");
            Check(Calls(Method("UpdateLocalisationDefinitionInternal")).Select(c => c.Name).SequenceEqual(new[] { "OnLanguageDefinitionLoaded", "get_StringTable", "get_Language", "SaveLocalisationDefinitionToCache" }), "native language re-read after callbacks before cache write");
            Check(Calls(Method("SaveLocalisationDefinitionToCache")).Select(c => c.Name).SequenceEqual(new[] { "GetString", "GetCachePath", "WriteAllBytes", "GetCachePathVersion", "GetClientVersion", "get_ASCII", "GetBytes", "WriteAllBytes" }), "native writes data then calls live client-version before ASCII receiver evaluation and version write");
            var tableRead = IL(Method("ReadStringsFromBuffer")).ToArray();
            Check(Array.FindIndex(tableRead, i => i.value is MethodInfo m && m.Name == "set_Hash") < Array.FindIndex(tableRead, i => i.op == OpCodes.Ldlen), "native global hash assignment precedes array-length validation");
            Check(Calls(Method("ReadStringsFromBuffer")).Count(c => c.Name == "get_Strings") == 2 && Calls(Method("ReadStringsFromBuffer")).Count(c => c.Name == "get_Id") == 2, "native captured length with live per-element array and id re-reads");
            var delayed = T.GetNestedType("<DelayedStringLoad>d__54", Declared).GetMethod("MoveNext", Declared);
            Check(Calls(delayed).Select(c => c.Name).SequenceEqual(new[] { "GetLanguage", "OnLanguageChanged" }), "native delayed iterator performs actual language load before null yield");
        }
        private static void EntriesAndHash()
        {
            var entry = new Entry(null, "value", int.MinValue);
            Check(entry.id == null && entry.content == "value" && entry.numArgs == int.MinValue, "native entry constructor preserves nullable id and signed args");
            Check(Table.GetStringID(null) == 0 && Table.GetStringID(string.Empty) == 0, "original CRC null and empty lookup identity");
            Check(Table.GetStringID("mixedCase") == Table.GetStringID("MIXEDCASE") && Table.GetStringID("mixedCase") == HLCRC32.GenerateInt("mixedCase"), "original one-argument upper-case CRC convention");
            string hash = new string(new[] { 'h','a','s','h' });
            var source = Buffer(hash, Value("same", "old", 1), Value("SAME", "new", -2), Value(null, null, int.MaxValue), Value("", "zero replacement", int.MinValue), Value("emoji", "\ud83d\ude80", 3));
            var result = Table.ReadStringsFromBuffer(source);
            Check(ReferenceEquals(Table.Hash, hash) && result.Count == 3, "native retains hash reference and overwrites duplicate/colliding keys");
            Check(result[Table.GetStringID("same")].id == "SAME" && result[Table.GetStringID("same")].content == "new" && result[Table.GetStringID("same")].numArgs == -2, "native last case-colliding entry wins including its own id and args");
            Check(result[0].id == "" && result[0].content == "zero replacement" && result[0].numArgs == int.MinValue, "native null/empty CRC collision is retained without sanitization");
            Check(result[Table.GetStringID("emoji")].content == "\ud83d\ude80", "native arbitrary content retained");
            source.Strings[1].Content = "later mutation"; source.Strings = new ClientDataAPI.LocalisedString[0];
            Check(result[Table.GetStringID("same")].content == "new" && result.Count == 3, "readonly value entry snapshots fields, not source array or codec object");
            Check(Table.ReadStringsFromBuffer(Buffer(null)).Count == 0 && Table.Hash == null, "native empty table accepts null hash");
            var invalid = new BufferTable { Hash = "null-array", Strings = null };
            Check(Throws<NullReferenceException>(() => Table.ReadStringsFromBuffer(invalid)) && Table.Hash == "null-array", "native null-array failure leaves newly assigned hash");
            invalid = Buffer("null-child", Value("first", "retained"), null);
            Check(Throws<NullReferenceException>(() => Table.ReadStringsFromBuffer(invalid)) && Table.Hash == "null-child", "native later null entry does not roll hash back");
            Check(Throws<NullReferenceException>(() => Table.ReadStringsFromBuffer(null)) && Table.Hash == "null-child", "native null table fails before hash mutation");
            result = Table.ReadStringsFromBuffer(Buffer("nullable-content", Value("id", null, -1)));
            Check(result[Table.GetStringID("id")].content == null && result[Table.GetStringID("id")].numArgs == -1, "native nullable content and negative format count preserved");
        }
        private static void StoreAndFailurePaths()
        {
            Table owner = Fixture(); var cached = Field(T,"m_cachedAtTime");
            foreach (long value in new[] { 0L, -1L, long.MinValue, long.MaxValue, 12345678901234L })
            {
                cached.SetValue(owner, value); var properties = new HLPropertyList(); Invoke(owner,"OnPropertyStoreSave",properties);
                Check(properties.Properties.Count == 1 && properties.Properties[0].m_name == "StringTableCachedTime" && properties.AsLong("StringTableCachedTime") == value, "original signed Int64 cache save identity " + value);
                cached.SetValue(owner, 17L); Invoke(owner,"OnPropertyStoreLoad",properties,false); Check((long)cached.GetValue(owner) == value, "original cache load preserves signed Int64 " + value);
                cached.SetValue(owner, 18L); Invoke(owner,"OnPropertyStoreLoad",properties,true); Check((long)cached.GetValue(owner) == value, "native isNewFile does not alter cache load " + value);
            }
            var missing = new HLPropertyList(); cached.SetValue(owner,long.MaxValue); Invoke(owner,"OnPropertyStoreLoad",missing,true);
            Check((long)cached.GetValue(owner) == 0, "original missing saved cache time defaults to zero");
            missing.AddProperty("StringTableCachedTime","not-an-integer"); cached.SetValue(owner,7L);
            Check(Throws<FormatException>(() => Invoke(owner,"OnPropertyStoreLoad",missing,false)) && (long)cached.GetValue(owner) == 7L, "native malformed saved Int64 propagates conversion failure and retains old cache time");
            Check((bool)Invoke(owner,"IsDirty") == false && (string)Invoke(owner,"GetClientVersion") == "0", "native false cache gate and client-version default");
            var singleton = Field(typeof(MonoSingleton<Table>),"<Instance>k__BackingField"); object before = singleton.GetValue(null);
            try
            {
                singleton.SetValue(null,owner);
                var supported = new[] { new ClientDataAPI.SupportedLanguage { Language = (int)Languages.EnglishUS } };
                Invoke(owner,"InternalSetSupportedLanguages",(object)supported);
                Check(ReferenceEquals(Table.GetSupportedLanguages(),supported), "original returns owned supported-language array");
                supported[0] = null; Check(Table.GetSupportedLanguages()[0] == null, "caller-owned array mutation remains visible");
                Invoke(owner,"InternalSetSupportedLanguages",new object[] { null }); Check(Table.GetSupportedLanguages() == null, "original owned array accepts null without fallback");
            }
            finally { singleton.SetValue(null,before); }
            var originalLanguages = new[] { new ClientDataAPI.SupportedLanguage() }; Field(T,"m_supportedLanguages").SetValue(owner,originalLanguages);
            owner.UpdateLocalisationDefinition((byte[])null);
            Check(ReferenceEquals(Field(T,"m_supportedLanguages").GetValue(owner),originalLanguages), "native null byte update exits before any owned state mutation");
            owner.UpdateLocalisationDefinition(new byte[] { 0x80 });
            Check(ReferenceEquals(Field(T,"m_supportedLanguages").GetValue(owner),originalLanguages), "truncated varint decode exception swallowed before applying a definition");
            owner.UpdateLocalisationDefinition(new byte[0]);
            Check(Field(T,"m_supportedLanguages").GetValue(owner) == null, "native incomplete definition mutates supported array before caught null-table failure");
            Field(T,"m_supportedLanguages").SetValue(owner,originalLanguages); Field(T,"<Hash>k__BackingField").SetValue(null,"before-incomplete-table");
            owner.UpdateLocalisationDefinition(new byte[] { 1,0,2,0 });
            Check(((ClientDataAPI.SupportedLanguage[])Field(T,"m_supportedLanguages").GetValue(owner)).Length == 0 && Table.Hash == null, "native incomplete nested table preserves array and hash partial mutations before array validation");
            var definition = DefinitionFor(Languages.Japanese); var encodingField = Field(typeof(Definition),"s_encodeInstance");
            var encoding = (Definition)encodingField.GetValue(null); var oldLanguages = encoding.SupportedLanguages; var oldTable = encoding.StringTable;
            try
            {
                encoding.SupportedLanguages = definition.SupportedLanguages; encoding.StringTable = definition.StringTable;
                Field(T,"m_supportedLanguages").SetValue(owner,originalLanguages); Field(T,"<Hash>k__BackingField").SetValue(null,"untouched-object-update");
                owner.UpdateLocalisationDefinition(definition);
                Check(encoding.SupportedLanguages == null && encoding.StringTable == null, "native object overload resets real encoding singleton");
                Check(ReferenceEquals(Field(T,"m_supportedLanguages").GetValue(owner),originalLanguages) && Table.Hash == "untouched-object-update", "native reset singleton encode failure is swallowed before applying supplied definition");
                Check(definition.StringTable.Language == (int)Languages.Japanese && definition.SupportedLanguages.Length == 0, "native supplied definition is not reset or encoded");
                owner.UpdateLocalisationDefinition((Definition)null); Check(ReferenceEquals(Field(T,"m_supportedLanguages").GetValue(owner),originalLanguages), "native null definition update has no side effects");
            }
            finally { encoding.SupportedLanguages = oldLanguages; encoding.StringTable = oldTable; }
        }
        private static void CallbackOrdering()
        {
            Table owner = Fixture(); Definition definition = DefinitionFor(Languages.Japanese);
            var callbacks = new CallbackSubscriptions();
            WithCleanup(() =>
            {
                MethodInfo initializer = T.GetNestedType("<>c", Declared).GetMethods(Declared).Single(m => m.Name == "<.cctor>b__69_0");
                Check(IL(initializer).Where(i => i.op != OpCodes.Nop).Select(i => i.op).SequenceEqual(new[] { OpCodes.Ldc_I4_1, OpCodes.Ret }), "readiness initializer source pattern returns true without invoking current subscribers");
                var trace = new List<string>(); Action oldRequest = () => trace.Add("stale request"), newRequest = () => trace.Add("live request");
                callbacks.AddRequest(oldRequest);
                Action first = () => { trace.Add("handler first"); Check(Table.StringsLanguage == Languages.Japanese,"native language published before first callback"); callbacks.RemoveRequest(oldRequest); callbacks.AddRequest(newRequest); definition.StringTable.Language = (int)Languages.German; };
                Action second = () => { trace.Add("handler second"); Check(Table.StringsLanguage == Languages.Japanese,"native published language remains captured despite handler changing definition"); };
                callbacks.AddHandler(first); callbacks.AddHandler(second);
                Invoke(owner,"InvokeStringTableHandlers",definition);
                Check(trace.SequenceEqual(new[] { "handler first","handler second","live request" }), "original live request event re-read after handlers mutate registration");
                Check(definition.StringTable.Language == (int)Languages.German && Table.StringsLanguage == Languages.Japanese,"handler mutation retained without additional language publication");
                trace.Clear(); callbacks.ClearHandlers(); callbacks.ClearRequests(); callbacks.AddRequest(() => trace.Add("request with null handlers"));
                Invoke(owner,"InvokeStringTableHandlers",definition);
                Check(Table.StringsLanguage == Languages.German && trace.SequenceEqual(new[] { "request with null handlers" }), "native genuine FastAction null invokes no handlers then request");
                trace.Clear(); callbacks.ClearHandlers(); callbacks.ClearRequests();
                callbacks.AddHandler(() => { trace.Add("throwing handler"); throw new ApplicationException("bounded handler failure"); });
                callbacks.AddRequest(() => trace.Add("must not request")); definition.StringTable.Language = (int)Languages.French;
                Check(Throws<ApplicationException>(() => Invoke(owner,"InvokeStringTableHandlers",definition)) && trace.SequenceEqual(new[] { "throwing handler" }) && Table.StringsLanguage == Languages.French, "native handler throw keeps published language and prevents request");
                trace.Clear(); callbacks.ClearHandlers(); callbacks.ClearRequests(); callbacks.AddRequest(() => { trace.Add("throwing request"); throw new ApplicationException("bounded request failure"); });
                Check(Throws<ApplicationException>(() => Invoke(owner,"RequestStringTable")) && trace.SequenceEqual(new[] { "throwing request" }), "native request event does not swallow exception");
                callbacks.ClearRequests(); Invoke(owner,"RequestStringTable"); Check(Field(T,"OnRequestStringTable").GetValue(null) == null,"native absent request event is a no-op");
                int probes = 0, callbackCount = 0;
                Func<bool> delayed = () => { ++probes; return false; }; callbacks.SetReadiness(delayed);
                callbacks.AddRequest(() => ++callbackCount);
                IEnumerator iterator = (IEnumerator)Invoke(owner,"WaitOnProjectAssets",definition);
                Check(probes == 0 && callbackCount == 0,"native iterator construction has no readiness callback");
                Check(iterator.MoveNext() && iterator.Current == null && probes == 1 && callbackCount == 0,"native false readiness yields null once");
                callbacks.SetReadiness(() => { ++probes; return true; });
                Check(!iterator.MoveNext() && probes == 2 && callbackCount == 1 && Table.StringsLanguage == Languages.French,"native deferred path consults live replaced readiness and invokes once");
                Check(!iterator.MoveNext() && callbackCount == 1,"native deferred completed state prevents callback repetition");
                callbacks.SetReadiness(delayed); iterator = (IEnumerator)Invoke(owner,"WaitOnProjectAssets",definition); Check(iterator.MoveNext(),"paused deferred fixture begins");
                ((IDisposable)iterator).Dispose(); callbacks.SetReadiness(() => true);
                Check(!iterator.MoveNext() && callbackCount == 2,"original empty deferred Dispose leaves paused state resumable");
                callbacks.SetReadiness(null); iterator = (IEnumerator)Invoke(owner,"WaitOnProjectAssets",definition);
                Check(Throws<NullReferenceException>(() => iterator.MoveNext()),"native null readiness event is unguarded");
                callbacks.SetReadiness(() => true); Check(!iterator.MoveNext() && callbackCount == 2,"native readiness throw terminates iterator before later re-entry");
                callbacks.SetReadiness(() => true); iterator = (IEnumerator)Invoke(owner,"WaitOnProjectAssets",definition);
                callbacks.ClearRequests(); callbacks.AddRequest(() => throw new ApplicationException("bounded completion failure"));
                Check(Throws<ApplicationException>(() => iterator.MoveNext()),"native completion callback exception propagates");
                callbacks.ClearRequests(); callbacks.AddRequest(() => ++callbackCount); Check(!iterator.MoveNext() && callbackCount == 2,"native terminal state retained after completion callback throws");
                Check(Throws<NotSupportedException>(() => iterator.Reset()),"native iterator Reset is not supported");
            }, callbacks.Dispose);
        }
        private static void NullSingletonBoundaries()
        {
            var singleton = Field(typeof(MonoSingleton<Table>),"<Instance>k__BackingField"); object old = singleton.GetValue(null);
            try
            {
                singleton.SetValue(null,null);
                Check(Table.GetString(Strings.NONE) == string.Empty,"native NONE lookup exits before singleton access");
                Check(Table.GetString(Strings.NUMBER_SEPARATOR) == string.Empty && !Table.StringExists(Strings.NONE) && !Table.AreStringsLoaded(),"native absent singleton lookup and readiness fallbacks");
                string content = "previous"; int args = 99;
                Check(!Table.GetStringAndNumArgs(Strings.NONE,out content,out args) && content == string.Empty && args == 0,"native absent enum lookup clears out values");
                Check(!Table.GetStringAndNumArgs((string)null,out content,out args) && content == string.Empty && args == 0,"native null string CRC lookup falls back through enum overload");
                Check(Throws<NullReferenceException>(() => Table.GetSupportedLanguages()),"native supported-language accessor has no singleton guard");
                IEnumerator iterator = Table.WaitForStrings(); Check(iterator.MoveNext() && iterator.Current == null,"native no-singleton waiter yields null");
                ((IDisposable)iterator).Dispose(); Check(iterator.MoveNext() && iterator.Current == null,"original empty string waiter Dispose leaves paused state resumable");
                Check(Throws<NotSupportedException>(() => iterator.Reset()),"native string waiter Reset unsupported");
            }
            finally { singleton.SetValue(null,old); }
        }
        public static int RunManaged()
        {
            RequireColdManagedState();
            checks = 0; FieldInfo[] statics = DataStatics(); object[] previous = statics.Select(f => f.GetValue(null)).ToArray();
            WithCleanup(() => { Metadata(); NativeOrderingContracts(); EntriesAndHash(); StoreAndFailurePaths(); CallbackOrdering(); NullSingletonBoundaries(); },
                () => { for (int i = 0; i < statics.Length; ++i) statics[i].SetValue(null,previous[i]); });
            return checks;
        }

        private static void RequireColdManagedState()
        {
            var registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            RequireColdState(registry, MonoSingleton<Table>.Instance, MonoSingleton<Hardlight.Localisation.Language>.Instance,
                Table.StringsTableHandlers, Field(T, "OnRequestStringTable").GetValue(null));
            RequireOriginalReadiness((Func<bool>)Field(T, "ShouldInvokeStringHandlers").GetValue(null));
        }

        public static void RequireColdState(IDictionary registry, object tableSingleton, object languageSingleton, object handlers, object request)
        {
            VerificationFixtureOwnership.RequireAbsent(registry, ProcessManager.GetDefaultName<Table>(), tableSingleton, languageSingleton, handlers, request);
            VerificationFixtureOwnership.RequireAbsent(registry, ProcessManager.GetDefaultName<Hardlight.Localisation.Language>());
        }

        public static void RequireOriginalReadiness(Func<bool> readiness)
        {
            Type closure = T.GetNestedType("<>c", Declared);
            MethodInfo initializer = closure.GetMethods(Declared).Single(m => m.Name == "<.cctor>b__69_0");
            object target = Field(closure, "<>9").GetValue(null);
            Delegate[] handlers = readiness?.GetInvocationList();
            // This is the expected generated source pattern, not a fresh
            // original/compiler identity receipt. Never invoke borrowed code.
            if (handlers == null || handlers.Length != 1 ||
                !handlers[0].Method.Equals(initializer) || !ReferenceEquals(handlers[0].Target, target))
                throw new InvalidOperationException("StringTable fixture requires only the original readiness initializer.");
        }
        private static void EngineComponentAndFiles()
        {
            // Actual Editor execution only. Files below are generated from the
            // genuine codec in a new temporary directory, never supplied assets
            // or save containers. Automatic lifecycle is a separate Play test.
            RequireColdManagedState();
            Check(!Application.isPlaying, "isolated synchronous loader proof runs in actual EditMode");
            Check(ProcessManager.IsSystemNull<Table>() && ReferenceEquals(MonoSingleton<Table>.Instance,null), "no existing original string-table singleton");
            Check(ProcessManager.IsSystemNull<Hardlight.Localisation.Language>() && ReferenceEquals(MonoSingleton<Hardlight.Localisation.Language>.Instance,null), "no existing original language singleton");
            FieldInfo[] statics = DataStatics(); object[] oldStatics = statics.Select(f => f.GetValue(null)).ToArray();
            var tableRegistration = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<Table>());
            var languageRegistration = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<Hardlight.Localisation.Language>());
            GameObject owner = null, languageOwner = null; Table table = null; Hardlight.Localisation.Language language = null;
            string temporary = System.IO.Path.Combine(System.IO.Path.GetTempPath(),"project-lucid-string-table-"+Guid.NewGuid().ToString("N"));
            var subscriptions = new CallbackSubscriptions();
            WithCleanup(() =>
            {
                System.IO.Directory.CreateDirectory(temporary);
                owner = new GameObject("Lucid original StringTable proof"); owner.SetActive(false); table = owner.AddComponent<Table>();
                Check(Field(T,"m_supportedLanguages").GetValue(table) == null && Field(T,"m_strings").GetValue(table) == null && (long)Field(T,"m_cachedAtTime").GetValue(table) == 0 && table.NumberFormat == null, "actual original component constructor leaves all table runtime data unset");
                string json = JsonUtility.ToJson(table);
                Check(T.GetFields(Declared).All(f => !json.Contains("\""+f.Name+"\"")), "actual Unity serializes no original outer runtime/static/constant field");
                tableRegistration.RegisterUsing(table, () => typeof(MonoSingleton<Table>).GetMethod("Awake",Declared).Invoke(table,null));
                Check(ReferenceEquals(MonoSingleton<Table>.Instance,table) && ReferenceEquals(ProcessManager.GetSystemRef<Table>().GetSafe(),table), "actual component registers through genuine singleton/process graph");
                Check(!Table.AreStringsLoaded() && !Table.StringExists(Strings.NONE) && Table.GetString(Strings.NONE) == string.Empty, "actual registered empty table and NONE lookup readiness");
                Check(Invoke(null,"LoadLocalisationDefinitionFromCache",(Languages)int.MinValue) == null, "actual original false dirty gate exits before unknown enum/path/cache IO");
                Check(Table.GetCachePath("EnglishUS") == Application.persistentDataPath+"/Text-EnglishUS" && Table.GetCachePathVersion("EnglishUS") == Application.persistentDataPath+"/Text-EnglishUS-Version", "actual engine cache paths retain exact original literal slash/key suffix");
                Invoke(table,"SaveLocalisationDefinitionToCache",(Languages)int.MinValue,null);
                Check((long)Field(T,"m_cachedAtTime").GetValue(table) == 0, "actual null cache-save buffer exits without time mutation or IO");
                var dictionary = new Dictionary<int,Entry> { [(int)Strings.NUMBER_SEPARATOR] = new Entry("NUMBER_SEPARATOR","_",0), [(int)Strings.DECIMAL_SEPARATOR] = new Entry("DECIMAL_SEPARATOR",",",0), [(int)Strings.NONE] = new Entry("NONE","stored-none",4), [1234567] = new Entry("arbitrary",null,-8) };
                Invoke(table,"InternalSetStrings",dictionary);
                Check(Table.AreStringsLoaded() && ReferenceEquals(Field(T,"m_strings").GetValue(table),dictionary), "actual original dictionary ownership and readiness");
                Check(table.NumberFormat != null && !table.NumberFormat.IsReadOnly && !ReferenceEquals(table.NumberFormat,CultureInfo.InvariantCulture.NumberFormat), "actual original invariant number format clone is mutable and owned");
                Check(table.NumberFormat.NumberGroupSeparator == "_" && table.NumberFormat.NumberDecimalSeparator == "," && CultureInfo.InvariantCulture.NumberFormat.NumberGroupSeparator == "," && CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator == ".", "actual separators applied without changing invariant source format");
                Check(Table.StringExists(Strings.NONE) && Table.GetString(Strings.NONE) == string.Empty, "actual stored NONE exists but GetString still returns sentinel empty");
                string content; int args;
                Check(Table.GetStringAndNumArgs(Strings.NONE,out content,out args) && content == "stored-none" && args == 4, "actual NONE num-args overload lacks GetString sentinel branch");
                Check(Table.GetString((Strings)1234567) == null && Table.GetStringAndNumArgs((Strings)1234567,out content,out args) && content == null && args == -8, "actual nullable stored content and signed args preserved");
                Check(Table.GetString((Strings)7654321) == string.Empty && !Table.GetStringAndNumArgs((Strings)7654321,out content,out args) && content == string.Empty && args == 0, "actual absent entry empty/out fallback");
                languageOwner = new GameObject("Lucid original Language table proof"); languageOwner.SetActive(false); language = languageOwner.AddComponent<Hardlight.Localisation.Language>();
                languageRegistration.RegisterUsing(language, () => typeof(MonoSingleton<Hardlight.Localisation.Language>).GetMethod("Awake",Declared).Invoke(language,null));
                Field(typeof(Hardlight.Localisation.Language),"m_currentLanguage").SetValue(language,Languages.Japanese);
                var supported = new[] { new ClientDataAPI.SupportedLanguage { Language = (int)Languages.EnglishUS },new ClientDataAPI.SupportedLanguage { Language = (int)Languages.Japanese } };
                Invoke(table,"InternalSetSupportedLanguages",(object)supported);
                Check(ReferenceEquals(Table.GetSupportedLanguages(),supported) && ReferenceEquals(Table.GetCurrentLanguageData(),supported[1]), "actual current language lookup returns original matching array element");
                Field(typeof(Hardlight.Localisation.Language),"m_currentLanguage").SetValue(language,Languages.German);
                Check(Table.GetCurrentLanguageData() == null, "actual unmatched current language returns null");
                supported[0] = null;
                Check(Throws<NullReferenceException>(() => Table.GetCurrentLanguageData()), "actual native current-language loop does not sanitize null entries");
                supported[0] = new ClientDataAPI.SupportedLanguage { Language = (int)Languages.EnglishUS };
                var definition = new Definition { SupportedLanguages = supported, StringTable = Buffer("actual-definition",Value("NUMBER_SEPARATOR"," "),Value("DECIMAL_SEPARATOR","."),Value("title","bounded original loader",2)) };
                definition.StringTable.Language = (int)Languages.Japanese;
                Check(Table.GetStringID("NUMBER_SEPARATOR") == (int)Strings.NUMBER_SEPARATOR && Table.GetStringID("DECIMAL_SEPARATOR") == (int)Strings.DECIMAL_SEPARATOR, "actual buffer keys use genuine original CRC identities");
                var trace = new List<string>();
                subscriptions.SetReadiness(() => { trace.Add("ready"); Check(ReferenceEquals(Table.GetSupportedLanguages(),supported) && Table.Hash == "actual-definition" && Table.GetString("title") == "bounded original loader" && table.NumberFormat.NumberGroupSeparator == " ", "actual readiness callback observes already applied support/hash/dictionary/format"); return true; });
                subscriptions.AddHandler(() => { trace.Add("handlers"); Check(Table.StringsLanguage == Languages.Japanese,"actual handlers observe published original definition language"); });
                subscriptions.AddRequest(() => trace.Add("request")); Invoke(table,"OnLanguageDefinitionLoaded",definition);
                Check(trace.SequenceEqual(new[] { "ready","handlers","request" }), "actual native readiness/handler/request sequence");
                var incomplete = new Definition { SupportedLanguages = new ClientDataAPI.SupportedLanguage[0], StringTable = Buffer("missing-decimal",Value("NUMBER_SEPARATOR",";")) };
                Check(Throws<ArgumentException>(() => Invoke(table,"OnLanguageDefinitionLoaded",incomplete)), "actual missing decimal separator propagates real BCL setter exception");
                Check(ReferenceEquals(Table.GetSupportedLanguages(),incomplete.SupportedLanguages) && Table.Hash == "missing-decimal" && table.NumberFormat.NumberGroupSeparator == ";" && table.NumberFormat.NumberDecimalSeparator == ".", "actual failed definition keeps new support/hash/dictionary and partly changed format");
                trace.Clear(); subscriptions.SetReadiness(null);
                Check(Throws<NullReferenceException>(() => Invoke(table,"OnLanguageDefinitionLoaded",definition)) && trace.Count == 0 && Table.Hash == "actual-definition" && table.NumberFormat.NumberDecimalSeparator == ".", "actual null readiness failure occurs after data/format mutation and before handlers");
                subscriptions.SetReadiness(() => { throw new ApplicationException("bounded ready failure"); });
                byte[] encoded = definition.encode(); table.UpdateLocalisationDefinition(encoded);
                Check(Table.Hash == "actual-definition" && Table.GetString("title") == "bounded original loader" && trace.Count == 0, "actual byte update applies decoded definition then swallows readiness exception before cache write");
                string path = System.IO.Path.Combine(temporary,"EnglishUS.bytes"); definition.StringTable.Language = (int)Languages.EnglishUS; byte[] english = definition.encode(); System.IO.File.WriteAllBytes(path,english);
                int callbacks = 0; Action<Definition> callback = value => ++callbacks;
                var loaded = Table.LoadLanguageDefinition(Languages.EnglishUS,temporary,callback);
                Check(loaded.StringTable.Language == (int)Languages.EnglishUS && loaded.StringTable.Hash == "actual-definition" && loaded.StringTable.Strings.Length == 3 && loaded.SupportedLanguages.Length == 2, "actual original synchronous loader reads genuine generated codec file from rooted directory");
                Check(callbacks == 0, "actual synchronous successful load returns model without callback invocation");
                loaded = Table.LoadLanguageDefinition(Languages.Japanese,temporary,callback);
                Check(loaded.StringTable.Language == (int)Languages.EnglishUS && callbacks == 0, "actual missing nondefault file recurses to original default with same directory without callback");
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(temporary,"Japanese.bytes"),new byte[] { 0x80 });
                Check(Throws<IndexOutOfRangeException>(() => Table.LoadLanguageDefinition(Languages.Japanese,temporary,callback)) && callbacks == 0, "actual malformed nondefault codec escapes fallback catch instead of loading valid default");
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(temporary,"Japanese.bytes"),new byte[0]);
                loaded = Table.LoadLanguageDefinition(Languages.Japanese,temporary,callback);
                Check(loaded.SupportedLanguages == null && loaded.StringTable == null && callbacks == 0, "actual empty file returns original incomplete codec model without default fallback");
                Check((long)Field(T,"m_cachedAtTime").GetValue(table) == 0, "native table/read/update/handlers never invent cached-at timestamp changes");
            }, () =>
            {
                // Pair each explicit Awake with its original OnDestroy, then
                // release only our acquired rows and exact subscriptions.
                if (owner != null)
                {
                    try { Method("OnDestroy").Invoke(table, null); }
                    finally { UnityEngine.Object.DestroyImmediate(owner); }
                }
            }, () =>
            {
                if (languageOwner != null)
                {
                    try { typeof(Hardlight.Localisation.Language).GetMethod("OnDestroy", Declared).Invoke(language, null); }
                    finally { UnityEngine.Object.DestroyImmediate(languageOwner); }
                }
            }, subscriptions.Dispose, tableRegistration.Dispose, languageRegistration.Dispose,
                () => { for (int i = 0; i < statics.Length; ++i) statics[i].SetValue(null,oldStatics[i]); },
                () => { if (System.IO.Directory.Exists(temporary)) System.IO.Directory.Delete(temporary,true); });
            Check(ProcessManager.IsSystemNull<Table>() && ReferenceEquals(MonoSingleton<Table>.Instance,null), "actual table destruction unregisters and clears original singleton");
            Check(ProcessManager.IsSystemNull<Hardlight.Localisation.Language>() && ReferenceEquals(MonoSingleton<Hardlight.Localisation.Language>.Instance,null), "actual language fixture destruction unregisters and clears original singleton");
        }
        public static void Run()
        {
            RequireColdManagedState();
            using (ClientDataAPIVerification.IsolatePools())
            {
                int managed = RunManaged();
                EngineComponentAndFiles();
                Console.WriteLine("PASS original StringTable checks="+checks+"; managed="+managed+"; actual engine="+(checks-managed)+"; supplied-content loading=0");
            }
        }
    }
}
