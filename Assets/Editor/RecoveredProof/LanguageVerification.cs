using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    // Bounded original metadata/managed field and callback proof. Uninitialized
    // genuine original objects are proof fixtures only; no Unity engine instance,
    // fake runtime class, source body substitute, disk IO or authored content load.
    public static class LanguageVerification
    {
        private static int checks;
        private static readonly BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static readonly Type T = typeof(Language);
        private static FieldInfo Field(Type type, string name) => type.GetField(name, Declared);
        private static MethodInfo Method(string name) => T.GetMethod(name, Declared);
        private static void Check(bool condition, string label)
        {
            ++checks;
            if (!condition) throw new InvalidOperationException(label);
        }
        private static void Set(Language language, string name, object value) => Field(T, name).SetValue(language, value);
        private static V Get<V>(Language language, string name) => (V)Field(T, name).GetValue(language);
        private static Language Fixture(Languages current, Languages fallback = Languages.German, bool loaded = false, bool overridden = false)
        {
            var language = (Language)FormatterServices.GetUninitializedObject(T);
            Set(language, "m_currentLanguage", current); Set(language, "m_defaultLanguage", fallback);
            Set(language, "m_languageLoaded", loaded); Set(language, "m_overrideLanguage", overridden);
            Set(language, "m_osLanguageString", "isolated-proof-value");
            return language;
        }
        private static object Invoke(Language language, string name, params object[] arguments)
        {
            try { return Method(name).Invoke(language, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        private static HLPropertyList Properties(string overrideValue, string languageValue = null)
        {
            var values = new HLPropertyList();
            if (overrideValue != null) values.AddProperty("language_override", overrideValue);
            if (languageValue != null) values.AddProperty("language_current", languageValue);
            return values;
        }
        private static void Load(Language language, HLPropertyList values, bool newFile = false) => Invoke(language, "OnPropertyStoreLoad", values, newFile);
        private static bool Throws<E>(Action action) where E : Exception
        {
            try { action(); return false; } catch (E) { return true; }
        }
        private static IEnumerable<(OpCode op, object value)> Instructions(MethodBase method)
        {
            var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(OpCode))
                .Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Value);
            byte[] bytes = method.GetMethodBody().GetILAsByteArray();
            for (int position = 0; position < bytes.Length;)
            {
                short key = bytes[position++]; if (key == 0xfe) key = (short)(0xfe00 | bytes[position++]);
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
                    default: throw new InvalidOperationException("Unrecognized bounded language IL operand: " + code);
                }
                yield return (code, operand);
            }
        }
        private static void Metadata()
        {
            Check(T.Assembly.GetName().Name == "HLLocalisation.Runtime" && T.IsPublic && !T.IsSealed && !T.IsAbstract && T.BaseType == typeof(MonoSingleton<Language>), "original type/assembly/genuine singleton base");
            var options = T.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
            Check(options.Length == 2 && T.GetCustomAttributesData().Count == 2 && options[0].Option == Option.ArrayBoundsChecks && options[1].Option == Option.NullChecks && options.All(o => Equals(o.Value, false)), "original ordered boxed options");
            var fields = T.GetFields(Declared).OrderBy(f => f.MetadataToken).ToArray();
            string[] names = { "m_defaultLanguage", "m_forceDefaultLanguage", "m_languageLoaded", "m_overrideLanguage", "m_osLanguageString", "m_currentLanguage", "OnLanguageChanged", "languageOverrideSaveKey", "languageCurrentSaveKey" };
            Type[] types = { typeof(Languages), typeof(bool), typeof(bool), typeof(bool), typeof(string), typeof(Languages), typeof(Language.LanguageChangedHandler), typeof(string), typeof(string) };
            Check(fields.Select(f => f.Name).SequenceEqual(names), "all nine original fields/order, genuine event backing name");
            for (int i = 0; i < fields.Length; ++i)
            {
                Check(fields[i].FieldType == types[i] && fields[i].IsPrivate && fields[i].IsStatic == (i >= 6) && fields[i].IsLiteral == (i >= 7), names[i] + " original type/visibility/static/constant flags");
                Check(fields[i].GetCustomAttributesData().Select(a => a.AttributeType).SequenceEqual(i < 2 ? new[] { typeof(SerializeField) } : i == 6 ? new[] { typeof(CompilerGeneratedAttribute) } : Type.EmptyTypes), names[i] + " exact field attributes");
            }
            Check(Equals(fields[7].GetRawConstantValue(), "language_override") && Equals(fields[8].GetRawConstantValue(), "language_current"), "original private literal save keys");
            string[] methods = { "get_LanguageLoaded", "get_OSLanguageString", "get_CurrentLanguage", "get_DefaultLanguage", "GetLanguageAsInt", "GetLanguage", "IsLanguageOverriden", "add_OnLanguageChanged", "remove_OnLanguageChanged", "OverrideLanguage", "ClearOverrideLanguage", "Start", "OnDestroy", "OnPropertyStoreSave", "OnPropertyStoreLoad", "CalculateSystemLanguage", "ConvertLanguage" };
            Check(T.GetMethods(Declared).OrderBy(m => m.MetadataToken).Select(m => m.Name).SequenceEqual(methods) && T.GetConstructors(Declared).Length == 1, "complete original eighteen-method API/order, no padding");
            Check(T.GetProperties(Declared).Select(p => p.Name).SequenceEqual(new[] { "LanguageLoaded", "OSLanguageString", "CurrentLanguage", "DefaultLanguage" }) && T.GetProperties(Declared).All(p => p.GetMethod.IsPublic && p.SetMethod == null), "four original getter-only properties");
            foreach (string name in new[] { "add_OnLanguageChanged", "remove_OnLanguageChanged" })
            {
                var method = Method(name); var il = Instructions(method).ToArray();
                Check(method.IsPublic && method.IsStatic && method.IsSpecialName && method.GetCustomAttributesData().Select(a => a.AttributeType).SequenceEqual(new[] { typeof(CompilerGeneratedAttribute) }), name + " original event accessor attributes");
                Check(il.Any(i => i.value is MethodBase m && m.DeclaringType == typeof(System.Threading.Interlocked) && m.Name == "CompareExchange"), name + " native atomic delegate retry mechanism");
            }
            Type handler = typeof(Language.LanguageChangedHandler);
            Check(handler.IsNestedPublic && handler.IsSealed && handler.BaseType == typeof(MulticastDelegate) && handler.GetFields(Declared).Length == 0 && handler.GetCustomAttributesData().Count == 0, "original fieldless nested delegate contract");
            Check(handler.GetMethods(Declared).Select(m => m.Name).SequenceEqual(new[] { "Invoke", "BeginInvoke", "EndInvoke" }) && handler.GetConstructors(Declared).Length == 1 && handler.GetMethod("Invoke").GetParameters().Single().ParameterType == typeof(Languages), "four original runtime delegate contracts, count zero own bodies");
            foreach (string name in new[] { "Start", "OnPropertyStoreSave", "OnPropertyStoreLoad", "CalculateSystemLanguage", "ConvertLanguage" }) Check(Method(name).IsPrivate && !Method(name).IsVirtual && !Method(name).IsStatic, name + " original private instance contract");
            Check(Method("OnDestroy").IsFamily && Method("OnDestroy").IsVirtual && Method("OnDestroy").GetBaseDefinition().DeclaringType == typeof(MonoSingleton<Language>), "original protected singleton lifecycle override");
            foreach (string name in new[] { "Start", "OnDestroy" })
            {
                var calls = Instructions(Method(name)).Where(i => i.value is MethodBase && (i.op == OpCodes.Call || i.op == OpCodes.Callvirt)).Select(i => (MethodBase)i.value).ToArray();
                Check(calls.Select(m => m.Name).SequenceEqual(name == "Start" ? new[] { "AddLoadHandler", "AddSaveHandler", "CalculateSystemLanguage" } : new[] { "RemoveLoadHandler", "RemoveSaveHandler", "OnDestroy" }), name + " exact native handler/callee order");
            }
            var save = Instructions(Method("OnPropertyStoreSave")).Where(i => i.value is MethodInfo m && m.IsGenericMethod).Select(i => (MethodInfo)i.value).ToArray();
            Check(save.Length == 2 && save.All(m => m.DeclaringType == typeof(HLPropertyList) && m.Name == "AddProperty") && save.Select(m => m.GetGenericArguments().Single()).SequenceEqual(new[] { typeof(bool), typeof(string) }), "native save generic argument identities bool then string");
            var load = Instructions(Method("OnPropertyStoreLoad")).Where(i => i.value is MethodInfo m && m.IsGenericMethod).Select(i => (MethodInfo)i.value).ToArray();
            Check(load.Length == 1 && load[0].DeclaringType == typeof(EnumUtilities) && load[0].Name == "SafeParse" && load[0].GetGenericArguments().Single() == typeof(Languages), "native SafeParse<Languages>, no invented enum reader");
            var ctor = Instructions(T.GetConstructor(Type.EmptyTypes)).ToArray();
            Check(ctor.Where(i => i.op == OpCodes.Stfld).Select(i => ((FieldInfo)i.value).Name).SequenceEqual(new[] { "m_defaultLanguage", "m_osLanguageString", "m_currentLanguage" }), "three original constructor initializer stores/order");
            Check(ctor.Where(i => i.op == OpCodes.Ldc_I4).Select(i => (int)i.value).SequenceEqual(new[] { -1884417163, -1884417163 }) && ctor.Any(i => i.op == OpCodes.Ldsfld && i.value is FieldInfo f && f.DeclaringType == typeof(string) && f.Name == "Empty"), "native EnglishUS/current and String.Empty defaults");
            Check(Array.FindIndex(ctor, i => i.value is ConstructorInfo c && c.DeclaringType == typeof(MonoSingleton<Language>)) > Array.FindLastIndex(ctor, i => i.op == OpCodes.Stfld), "original field defaults precede genuine base constructor");
            Check(T.GetMethods(Declared).All(m => !Instructions(m).Any(i => i.value is FieldInfo f && f.Name == "m_forceDefaultLanguage")), "serialized force-default field is not consulted by original player bodies");
            var calculate = Instructions(Method("CalculateSystemLanguage")).Where(i => i.value is MethodBase && (i.op == OpCodes.Call || i.op == OpCodes.Callvirt)).Select(i => (MethodBase)i.value).ToArray();
            Check(calculate.Select(m => m.Name).SequenceEqual(new[] { "get_systemLanguage", "ToString", "GetDeviceISO2CountryCode", "ToLower", "ConvertLanguage" }) && calculate[3].DeclaringType == typeof(string) && calculate[3].GetParameters().Length == 0, "original Application/enum/country/culture-sensitive lower/conversion order");
        }

        private static void SwitchCases()
        {
            // Independently decoded native42-byte jump table and ARM mov/movk
            // return constants; zero marks the original m_defaultLanguage branch.
            int[] expected = { 1119569783, 1446156718, 0, 0, 1719143143, 1019848328, -1090567719, 1463574514, 1383479723, -1600431373, -1884417163, -660139554, 0, 1741521722, -1603078617, -1389135656, -1470421023, 453055515, 1690931536, 0, 212472324, -1761384406, 861610717, -1638731334, 25459628, 152854774, -884526987, -553119796, 186665011, -1790037404, 735899792, -1587011195, -99717768, -1474628719, -190647087, 1939505869, 37741342, 1901479564, 217030958, -1268270255, -1090567719, 1392694936 };
            var language = Fixture(Languages.Japanese, Languages.Hindi);
            foreach (bool force in new[] { false, true })
            {
                Set(language, "m_forceDefaultLanguage", force);
                for (int i = 0; i < expected.Length; ++i)
                    Check((int)(Languages)Invoke(language, "ConvertLanguage", (SystemLanguage)i, "zz") == (expected[i] == 0 ? (int)Languages.Hindi : expected[i]), "native system-language switch " + i + ", force=" + force);
            }
            foreach (int unsupported in new[] { -1, 42, 43, int.MinValue, int.MaxValue }) Check((Languages)Invoke(language, "ConvertLanguage", (SystemLanguage)unsupported, "gb") == Languages.Hindi, "native unsigned switch range/fallback " + unsupported);
            foreach (var pair in new[] { ("gb", Languages.EnglishUK), ("au", Languages.EnglishAus), ("GB", Languages.EnglishUS), ("AU", Languages.EnglishUS), ("ca", Languages.EnglishUS), ("", Languages.EnglishUS), ((string)null, Languages.EnglishUS) })
                Check((Languages)Invoke(language, "ConvertLanguage", SystemLanguage.English, pair.Item1) == pair.Item2, "exact native English country equality " + (pair.Item1 ?? "null"));
            Check((Languages)Invoke(language, "ConvertLanguage", SystemLanguage.French, "ca") == Languages.French && (Languages)Invoke(language, "ConvertLanguage", SystemLanguage.Portuguese, "pt") == Languages.PortugueseBrazil && (Languages)Invoke(language, "ConvertLanguage", SystemLanguage.SerboCroatian, null) == Languages.SerbianLatin, "native country is ignored for non-English and retains shipped regional choices");
        }

        private static void SavedState()
        {
            var eventField = Field(T, "OnLanguageChanged"); object previousEvent = eventField.GetValue(null);
            try
            {
                var language = Fixture(Languages.Japanese); var notifications = new List<string>();
                Language.LanguageChangedHandler observer = value => notifications.Add(value + ":" + language.LanguageLoaded + ":" + Get<bool>(language, "m_overrideLanguage"));
                eventField.SetValue(null, null); Language.OnLanguageChanged += observer;
                Load(language, Properties(null), true);
                Check(notifications.SequenceEqual(new[] { "Japanese:False:False" }) && language.LanguageLoaded && language.CurrentLanguage == Languages.Japanese, "first nonoverride load notifies retained current before loaded, ignores isNewFile");
                notifications.Clear(); Set(language, "m_overrideLanguage", true); Load(language, Properties(null));
                Check(notifications.Count == 0 && !Get<bool>(language, "m_overrideLanguage") && language.LanguageLoaded, "repeat nonoverride load clears override without calculating language or notifying");
                Load(language, Properties("True", "French"));
                Check(language.CurrentLanguage == Languages.French && Get<bool>(language, "m_overrideLanguage") && notifications.SequenceEqual(new[] { "French:True:True" }), "changed saved override stores before notification on already loaded instance");
                notifications.Clear(); Load(language, Properties("True", "French"), true);
                Check(notifications.Count == 0 && language.CurrentLanguage == Languages.French, "equal already loaded override suppresses notification");
                Set(language, "m_languageLoaded", false); Load(language, Properties("True", "French"));
                Check(notifications.SequenceEqual(new[] { "French:False:True" }) && language.LanguageLoaded, "equal first override load still notifies before loaded");
                foreach (string saved in new[] { "french", "invalid", "", null })
                {
                    Set(language, "m_currentLanguage", Languages.Japanese); notifications.Clear(); Load(language, Properties("True", saved));
                    Check(language.CurrentLanguage == Languages.German && notifications.Count == 1, "native case-sensitive parse failure uses configured default: " + (saved ?? "missing"));
                }
                Load(language, Properties("True", "123456789")); Check((int)language.CurrentLanguage == 123456789, "native parsing accepts unnamed decimal enum values");
                var nullValue = Properties("True"); nullValue.Properties.Add(new HLPropertyList.Property("language_current", HLPropertyStore.GetCRC("language_current"), null));
                Load(language, nullValue); Check(language.CurrentLanguage == Languages.German, "present null saved enum uses fallback");
                var duplicate = Properties("False", "Korean"); duplicate.Properties.Add(new HLPropertyList.Property("language_override", HLPropertyStore.GetCRC("language_override"), "True"));
                Set(language, "m_currentLanguage", Languages.Italian); Load(language, duplicate);
                Check(language.CurrentLanguage == Languages.Italian && !Get<bool>(language, "m_overrideLanguage"), "original first-CRC-match duplicate override wins");
                Set(language, "m_languageLoaded", false); Set(language, "m_overrideLanguage", true); notifications.Clear();
                Check(Throws<FormatException>(() => Load(language, Properties("malformed", "French"))) && !language.LanguageLoaded && Get<bool>(language, "m_overrideLanguage") && language.CurrentLanguage == Languages.Italian && notifications.Count == 0, "malformed bool preserves flags/current and propagates before notification");
                Language.OnLanguageChanged -= observer;
                Language.LanguageChangedHandler throwing = value => throw new ApplicationException("isolated-language-callback");
                int later = 0; Language.LanguageChangedHandler after = value => ++later;
                Language.OnLanguageChanged += throwing; Language.OnLanguageChanged += after;
                Check(Throws<ApplicationException>(() => Load(language, Properties("True", "Korean"))) && language.CurrentLanguage == Languages.Korean && Get<bool>(language, "m_overrideLanguage") && !language.LanguageLoaded && later == 0, "callback throw keeps changed fields, unloaded state, and stops later subscribers");
                Language.OnLanguageChanged -= throwing; Load(language, Properties("True", "Korean"));
                Check(later == 1 && language.LanguageLoaded, "retry equal load notifies because prior callback never marked loaded");
                eventField.SetValue(null, null); var trace = new List<Languages>();
                Language.LanguageChangedHandler nested = value => { trace.Add(value); if (value == Languages.Japanese) Load(language, Properties("True", "German")); };
                Language.OnLanguageChanged += nested; Load(language, Properties("True", "Japanese"));
                Check(trace.SequenceEqual(new[] { Languages.Japanese, Languages.German }) && language.CurrentLanguage == Languages.German && language.LanguageLoaded, "original reentrant callback retains inner language and completes loaded state");
                eventField.SetValue(null, null); int duplicates = 0; Language.LanguageChangedHandler repeated = value => ++duplicates;
                Language.OnLanguageChanged += repeated; Language.OnLanguageChanged += repeated; Language.OnLanguageChanged -= repeated;
                Load(language, Properties("True", "Italian")); Check(duplicates == 1, "native delegate removal removes last matching occurrence");
                Language.OnLanguageChanged -= repeated; Load(language, Properties("True", "French")); Check(duplicates == 1, "native event removal restores no subscribers");
                var values = new HLPropertyList(); Invoke(language, "OnPropertyStoreSave", values);
                Check(values.Properties.Select(p => p.m_name).SequenceEqual(new[] { "language_override", "language_current" }) && values.AsBool("language_override") && values.AsString("language_current") == "French", "original bool-first/save language uses real generated name registry");
                Invoke(language, "OnPropertyStoreSave", values); Check(values.Properties.Count == 2, "original repeated save updates first CRC property without appending");
            }
            finally { eventField.SetValue(null, previousEvent); }
        }

        private static void StaticState()
        {
            FieldInfo instance = Field(typeof(MonoSingleton<Language>), "<Instance>k__BackingField");
            FieldInfo eventField = Field(T, "OnLanguageChanged"); object oldInstance = instance.GetValue(null), oldEvent = eventField.GetValue(null);
            try
            {
                eventField.SetValue(null, null); instance.SetValue(null, null);
                Check(Language.GetLanguage() == Languages.EnglishUS && Language.GetLanguageAsInt() == (int)Languages.EnglishUS, "original absent singleton getter fallbacks using genuine CLR-null Unity comparison");
                Check(Throws<NullReferenceException>(() => Language.IsLanguageOverriden()), "managed CLR null override accessor lacks invented guard; original optimized native null-fault parity unclaimed");
                var language = Fixture(Languages.Japanese); instance.SetValue(null, language);
                int calls = 0; Language.LanguageChangedHandler handler = value => { ++calls; Check(value == language.CurrentLanguage && Get<bool>(language, "m_overrideLanguage"), "override callback observes original updated fields"); };
                Language.OnLanguageChanged += handler;
                Check(!Language.OverrideLanguage(Languages.Japanese) && calls == 0 && !Language.IsLanguageOverriden(), "equal override request does not set flag or notify");
                Check(Language.OverrideLanguage(Languages.French) && calls == 1 && Language.IsLanguageOverriden() && language.CurrentLanguage == Languages.French && !language.LanguageLoaded, "changed override sets flag/current without marking loaded");
                Language.OnLanguageChanged -= handler;
                Language.LanguageChangedHandler revert = value => Set(language, "m_currentLanguage", Languages.French);
                Language.OnLanguageChanged += revert; Check(Language.OverrideLanguage(Languages.Japanese) && language.CurrentLanguage == Languages.French, "return comparison uses previous value despite reentrant current restoration");
                Language.OnLanguageChanged -= revert;
                Language.LanguageChangedHandler fail = value => throw new ApplicationException("isolated-override"); Language.OnLanguageChanged += fail;
                Check(Throws<ApplicationException>(() => Language.OverrideLanguage(Languages.German)) && language.CurrentLanguage == Languages.German && Language.IsLanguageOverriden(), "override callback throw retains updated current/flag");
                Language.OnLanguageChanged -= fail;
            }
            finally { instance.SetValue(null, oldInstance); eventField.SetValue(null, oldEvent); }
        }

        private static void ImmediateStart()
        {
            FieldInfo storeInstance = Field(typeof(HLPropertyStore), "s_internalInstance"), loadHandlers = Field(typeof(HLPropertyStore), "LoadHandlers"), saveHandlers = Field(typeof(HLPropertyStore), "SaveHandlers"), eventField = Field(T, "OnLanguageChanged");
            object oldStore = storeInstance.GetValue(null), oldLoads = loadHandlers.GetValue(null), oldSaves = saveHandlers.GetValue(null), oldEvent = eventField.GetValue(null);
            try
            {
                var store = (HLPropertyStore)FormatterServices.GetUninitializedObject(typeof(HLPropertyStore));
                Field(typeof(HLPropertyStore), "ActiveProperties").SetValue(store, Properties("True", "Korean"));
                Field(typeof(HLPropertyStore), "m_isLoaded").SetValue(store, true); storeInstance.SetValue(null, store);
                loadHandlers.SetValue(null, null); saveHandlers.SetValue(null, null); eventField.SetValue(null, null);
                var language = Fixture(Languages.Japanese); int notifications = 0;
                Language.LanguageChangedHandler observer = value => { ++notifications; Check(!HLPropertyStore.IsThereAnySaveHandler && value == Languages.Korean && !language.LanguageLoaded, "immediate load callback precedes save subscription and loaded marking"); };
                Language.OnLanguageChanged += observer; Invoke(language, "Start");
                Check(language.CurrentLanguage == Languages.Korean && language.LanguageLoaded && Get<bool>(language, "m_overrideLanguage") && notifications == 1, "original Start respects synchronously loaded saved override instead of calculating system language");
                Check(((Delegate)loadHandlers.GetValue(null)).GetInvocationList().Length == 1 && ((Delegate)saveHandlers.GetValue(null)).GetInvocationList().Length == 1, "original Start adds one real load/save method delegate");
                var values = new HLPropertyList(); ((HLPropertyStore.SaveHandler)saveHandlers.GetValue(null))(values);
                Check(values.AsBool("language_override") && values.AsString("language_current") == "Korean", "actual subscribed original save callback transports both values");
                Language.OnLanguageChanged -= observer; loadHandlers.SetValue(null, null); saveHandlers.SetValue(null, null);
                var failedLanguage = Fixture(Languages.Italian);
                Language.LanguageChangedHandler fail = value => throw new ApplicationException("isolated-immediate-load"); Language.OnLanguageChanged += fail;
                Check(Throws<ApplicationException>(() => Invoke(failedLanguage, "Start")) && HLPropertyStore.IsThereAnyLoadHandler && !HLPropertyStore.IsThereAnySaveHandler && !failedLanguage.LanguageLoaded && failedLanguage.CurrentLanguage == Languages.Korean, "throwing immediate load retains load subscription and aborts before save subscription");
                Language.OnLanguageChanged -= fail;
            }
            finally { storeInstance.SetValue(null, oldStore); loadHandlers.SetValue(null, oldLoads); saveHandlers.SetValue(null, oldSaves); eventField.SetValue(null, oldEvent); }
        }

        public static int RunManaged()
        {
            checks = 0; Metadata(); SwitchCases(); SavedState(); StaticState(); ImmediateStart();
            return checks;
        }

        private static void EngineComponent()
        {
            // Actual engine component/serialization proof with explicit invocation
            // of original lifecycle methods. Automatic Awake/Start/OnDestroy is a
            // separate live PlayMode fixture. No author assets or disk are loaded.
            Check(ProcessManager.IsSystemNull<Language>() && ReferenceEquals(MonoSingleton<Language>.Instance, null), "isolated engine fixture has no existing original language singleton");
            var storeInstance = Field(typeof(HLPropertyStore), "s_internalInstance");
            var loads = Field(typeof(HLPropertyStore), "LoadHandlers"); var saves = Field(typeof(HLPropertyStore), "SaveHandlers");
            var eventField = Field(T, "OnLanguageChanged"); var bridge = Field(typeof(HLUnityCore), "s_unityCoreNativeBridge");
            object oldStore = storeInstance.GetValue(null), oldLoads = loads.GetValue(null), oldSaves = saves.GetValue(null), oldEvent = eventField.GetValue(null), oldBridge = bridge.GetValue(null);
            GameObject owner = null; Language language = null; bool registered = false;
            SystemRef<Language> system = ProcessManager.GetSystemRef<Language>();
            Action<Language> shutdown = null;
            try
            {
                owner = new GameObject("Lucid original language component proof"); owner.SetActive(false);
                language = owner.AddComponent<Language>();
                Check(language.DefaultLanguage == Languages.EnglishUS && language.CurrentLanguage == Languages.EnglishUS && language.OSLanguageString == string.Empty && !language.LanguageLoaded && !Get<bool>(language, "m_overrideLanguage") && !Get<bool>(language, "m_forceDefaultLanguage"), "actual component executes all original constructor defaults");
                string originalJson = JsonUtility.ToJson(language);
                Check(originalJson.Contains("\"m_defaultLanguage\":-1884417163") && originalJson.Contains("\"m_forceDefaultLanguage\":false"), "actual Unity serializes both original configuration fields");
                Check(new[] { "m_languageLoaded", "m_overrideLanguage", "m_osLanguageString", "m_currentLanguage", "OnLanguageChanged", "languageOverrideSaveKey", "languageCurrentSaveKey" }.All(n => !originalJson.Contains("\"" + n + "\"")), "actual Unity excludes remaining seven original private/static/literal fields");
                JsonUtility.FromJsonOverwrite("{\"m_defaultLanguage\":-1389135656,\"m_forceDefaultLanguage\":true}", language);
                Check(language.DefaultLanguage == Languages.German && Get<bool>(language, "m_forceDefaultLanguage") && language.CurrentLanguage == Languages.EnglishUS && !language.LanguageLoaded, "actual JSON overwrite restores configuration without changing runtime language fields");
                Check((Languages)Invoke(language, "ConvertLanguage", SystemLanguage.Unknown, "gb") == Languages.German, "actual serialized default controls unsupported-language fallback");
                var store = (HLPropertyStore)FormatterServices.GetUninitializedObject(typeof(HLPropertyStore));
                Field(typeof(HLPropertyStore), "m_isLoaded").SetValue(store, true);
                Field(typeof(HLPropertyStore), "ActiveProperties").SetValue(store, Properties("True", "Korean"));
                storeInstance.SetValue(null, store); loads.SetValue(null, null); saves.SetValue(null, null); eventField.SetValue(null, null);
                typeof(MonoSingleton<Language>).GetMethod("Awake", Declared).Invoke(language, null); registered = true;
                Check(ReferenceEquals(MonoSingleton<Language>.Instance, language) && ReferenceEquals(system.GetSafe(), language) && !ProcessManager.IsSystemNull<Language>(), "actual genuine component registers through original singleton/process graph");
                Check(Language.GetLanguage() == Languages.EnglishUS && Language.GetLanguageAsInt() == (int)Languages.EnglishUS, "actual Unity nonnull getters read original current singleton");
                int notifications = 0;
                Language.LanguageChangedHandler observer = value => { ++notifications; Check(value == Languages.Korean && !language.LanguageLoaded && !HLPropertyStore.IsThereAnySaveHandler, "actual component immediate-load callback precedes loaded/save subscription"); };
                Language.OnLanguageChanged += observer; Invoke(language, "Start");
                Check(language.LanguageLoaded && language.CurrentLanguage == Languages.Korean && Language.IsLanguageOverriden() && notifications == 1, "original Start consumes genuine already loaded store properties");
                Language.OnLanguageChanged -= observer;
                var saved = new HLPropertyList(); ((HLPropertyStore.SaveHandler)saves.GetValue(null))(saved);
                Check(saved.AsBool("language_override") && saved.AsString("language_current") == "Korean", "actual component registered save callback exports original keys and values");
                bridge.SetValue(null, new HLUnityCoreNativeBridge());
                Set(language, "m_currentLanguage", (Languages)int.MinValue);
                Languages calculated = (Languages)Invoke(language, "CalculateSystemLanguage");
                Check(language.OSLanguageString == Application.systemLanguage.ToString() && language.CurrentLanguage == (Languages)int.MinValue, "actual Application language records OS string without changing current field");
                string country = HLUnityCore.GetDeviceISO2CountryCode().ToLower();
                Check(calculated == (Languages)Invoke(language, "ConvertLanguage", Application.systemLanguage, country), "actual calculation consumes genuine maintained native-bridge country boundary, not a fake provider");
                Language.LanguageChangedHandler cleared = value => { ++notifications; Check(!Language.IsLanguageOverriden() && value == calculated && language.LanguageLoaded, "actual clear callback sees cleared flag/calculated language/retained loaded state"); };
                Language.OnLanguageChanged += cleared; Language.ClearOverrideLanguage();
                Check(language.CurrentLanguage == calculated && notifications == 2 && !Language.IsLanguageOverriden() && language.LanguageLoaded, "actual clear updates current and notifies once after calculation");
                Language.ClearOverrideLanguage(); Check(notifications == 2, "equal calculated language suppresses subsequent clear notification");
                Language.OnLanguageChanged -= cleared;
                bool observedCleanup = false;
                shutdown = value => { observedCleanup = true; Check(!HLPropertyStore.IsThereAnyLoadHandler && !HLPropertyStore.IsThereAnySaveHandler && ReferenceEquals(MonoSingleton<Language>.Instance, language), "original destruction removes property callbacks before base unregistration clears Instance"); };
                system.OnSystemShutdown += shutdown;
                Language.OnLanguageChanged += cleared;
                Invoke(language, "OnDestroy"); registered = false;
                Check(observedCleanup && ProcessManager.IsSystemNull<Language>() && ReferenceEquals(MonoSingleton<Language>.Instance, null), "actual original base cleanup unregisters/clears singleton");
                Check(eventField.GetValue(null) != null, "original destruction retains static language event rather than inventing event cleanup");
            }
            finally
            {
                if (shutdown != null) system.OnSystemShutdown -= shutdown;
                if (registered && language != null) Invoke(language, "OnDestroy");
                if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
                storeInstance.SetValue(null, oldStore); loads.SetValue(null, oldLoads); saves.SetValue(null, oldSaves); eventField.SetValue(null, oldEvent); bridge.SetValue(null, oldBridge);
            }
        }

        public static void Run()
        {
            int managed = RunManaged(); EngineComponent();
            Console.WriteLine("PASS original language checks=" + checks + "; managed=" + managed + "; actual engine=" + (checks - managed));
        }
    }
}
