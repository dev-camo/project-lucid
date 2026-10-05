using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight.Utils;
using UnityEngine;
using GameVersion = Hardlight.Utils.Version;

namespace ProjectLucid
{
    // Bounded native-derived Version20 proof. Test fixtures model only caller data;
    // no registered logging route, original asset or loading pipeline is substituted.
    public static class VersionVerification
    {
        private static int s_checks;
        private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static readonly Type Type = typeof(GameVersion);
        private static void Check(bool value, string message) { ++s_checks; if (!value) throw new InvalidOperationException(message); }
        private static T Read<T>(GameVersion value, string field) => (T)Type.GetField(field, Fields).GetValue(value);
        private static GameVersion Raw(string text, uint[] array = null, bool parsed = false)
        {
            object boxed = default(GameVersion);
            Type.GetField("m_version", Fields).SetValue(boxed, text);
            Type.GetField("m_numericIdentifiers", Fields).SetValue(boxed, array);
            Type.GetField("m_isParsed", Fields).SetValue(boxed, parsed);
            return (GameVersion)boxed;
        }
        private static void Throws<T>(Action action, string message) where T : Exception
        {
            bool matched = false;
            try { action(); } catch (Exception e) { matched = (e is TargetInvocationException ? e.InnerException : e) is T; }
            Check(matched, message);
        }
        private static MethodInfo Method(string name, params Type[] arguments) => Type.GetMethod(name, Fields, null, arguments, null);
        private static string Generate(IReadOnlyList<uint> values) => (string)Method("GenerateStringFromNumericIdentifiers", typeof(IReadOnlyList<uint>)).Invoke(default(GameVersion), new object[] { values });

        public static int RunManaged()
        {
            s_checks = 0;
            Check(Type.Assembly.GetName().Name == "HLUnityCore.Runtime", "original assembly identity");
            Check(Type.IsValueType && Type.IsSerializable && (uint)Type.Attributes == 0x102109, "exact original struct flags");
            Check(Type.GetInterfaces().Contains(typeof(IEquatable<GameVersion>)) && Type.GetInterfaces().Contains(typeof(IComparable<GameVersion>)), "original two typed contracts");
            FieldInfo[] fields = Type.GetFields(Fields).OrderBy(f => f.MetadataToken).ToArray();
            Check(string.Join("|", fields.Select(f => f.Name)) == "m_version|Default|DefaultString|Separator|NumberStyleNone|s_numberFormatInfoInvariant|s_stringBuilder|m_numericIdentifiers|m_isParsed", "nine exact original fields/order");
            Check(fields[0].IsPrivate && fields[0].FieldType == typeof(string) && fields[0].IsDefined(typeof(SerializeField), false), "original serialized text field");
            Check(fields[0].GetCustomAttribute<TooltipAttribute>().tooltip == "Insert a version in the format of 0.0.0, you can use as many numeric identifiers as you wish.", "original tooltip literal");
            Check(fields[1].IsLiteral && (uint)fields[1].GetRawConstantValue() == 0 && fields[1].IsPrivate, "uint Default0");
            Check(fields[2].IsPublic && fields[2].IsStatic && fields[2].IsInitOnly && (string)fields[2].GetValue(null) == "0", "default string readonly0");
            Check(fields[3].IsLiteral && (string)fields[3].GetRawConstantValue() == ".", "separator literal");
            Check(fields[4].IsLiteral && (int)fields[4].GetRawConstantValue() == 0, "NumberStyles.None constant");
            Check(ReferenceEquals(fields[5].GetValue(null), NumberFormatInfo.InvariantInfo), "actual invariant number provider");
            Check(fields[6].IsPrivate && fields[6].IsStatic && !fields[6].IsInitOnly && fields[6].FieldType == typeof(System.Text.StringBuilder), "original mutable shared builder");
            Check(fields[7].IsPrivate && !fields[7].IsStatic && fields[7].FieldType == typeof(uint[]) && fields[8].FieldType == typeof(bool), "original parsed caches");
            Check(Type.GetMethods(Fields).Length + Type.GetConstructors(Fields).Length == 20, "complete own twenty methods including static ctor");
            Check(Type.GetConstructor(new[] { typeof(uint[]) }).GetParameters()[0].IsDefined(typeof(ParamArrayAttribute), false), "original params numeric ctor");
            Check(GameVersion.DefaultString == "0", "original formatted static default");

            string[] valid = { "0", "1", "000", "01.002.0003", "1.0.0", "4294967295", "0.4294967295" };
            string[] canonical = { "0", "1", "0", "1.2.3", "1.0.0", "4294967295", "0.4294967295" };
            for (int i = 0; i < valid.Length; ++i)
            {
                Check(GameVersion.IsVersionStringValid(valid[i], out string error) && error == string.Empty && GameVersion.IsVersionStringValid(valid[i]), "valid exact unsigned numeric format" + i);
                var value = new GameVersion(valid[i]);
                Check(value.ToString() == canonical[i] && Read<bool>(value, "m_isParsed"), "constructor canonical text/cache" + i);
            }
            string[] invalid = { null, "", ".", "1.", ".1", "1..2", "-1", "+1", " 1", "1 ", "1. 2", "0x1", "4294967296", "1.2e3", "1.\n2" };
            string[] errors = {
                "The version is null or empty", "The version is null or empty",
                Empty(1, "."), Empty(2, "1."), Empty(1, ".1"), Empty(2, "1..2"),
                Bad(1, "-1", "-1"), Bad(1, "+1", "+1"), Bad(1, " 1", " 1"), Bad(1, "1 ", "1 "), Bad(2, "1. 2", " 2"), Bad(1, "0x1", "0x1"), Bad(1, "4294967296", "4294967296"), Bad(2, "1.2e3", "2e3"), Bad(2, "1.\n2", "\n2")
            };
            for (int i = 0; i < invalid.Length; ++i)
            {
                Check(!GameVersion.IsVersionStringValid(invalid[i], out string error), "reject original invalid format" + i);
                Check(error == errors[i], "exact first failure/out diagnostic" + i);
                Check(!GameVersion.IsVersionStringValid(invalid[i]), "simple overload rejects" + i);
            }
            Check(new GameVersion((uint[])null).ToString() == "0" && new GameVersion(Array.Empty<uint>()).ToString() == "0", "null/empty params produces actual one0");
            uint[] given = { 1u, 2u }; var alias = new GameVersion(given);
            Check(ReferenceEquals(Read<uint[]>(alias, "m_numericIdentifiers"), given) && alias.GetHashCode() == given.GetHashCode(), "caller array retained and identity hashed");
            given[1] = 3;
            Check(alias.ToString() == "1.2" && alias.CompareTo(new GameVersion(1u, 3u)) == 0, "alias mutation affects numeric compare but cached text remains");
            Check(alias.GetHashCode() == given.GetHashCode(), "array identity hash survives content change");
            var separately = new GameVersion(1u, 3u);
            Check(!ReferenceEquals(Read<uint[]>(alias, "m_numericIdentifiers"), Read<uint[]>(separately, "m_numericIdentifiers")) && separately.GetHashCode() == Read<uint[]>(separately, "m_numericIdentifiers").GetHashCode(), "equal distinct arrays use own object hashes without value hash assumptions");

            (uint[] A, uint[] B, int Expected)[] comparisons = {
                (new uint[]{1}, new uint[]{1,0,0}, 0), (new uint[]{1,0,1}, new uint[]{1}, 1), (new uint[]{1}, new uint[]{1,0,1}, -1),
                (new uint[]{0,100}, new uint[]{1}, -1), (new uint[]{1,2,3}, new uint[]{1,2,4}, -1),
                (new uint[]{1,3}, new uint[]{1,2,uint.MaxValue}, 1), (new uint[]{uint.MaxValue}, new uint[]{0}, 1), (new uint[]{2147483648}, new uint[]{2147483647}, 1)
            };
            foreach (var row in comparisons)
            {
                var a = new GameVersion(row.A); var b = new GameVersion(row.B);
                Check(a.CompareTo(b) == row.Expected && b.CompareTo(a) == -row.Expected, "unsigned padded lexicographic comparison");
                Check(a.Equals(b) == (row.Expected == 0) && a.Equals((object)b) == (row.Expected == 0), "both equals routes numeric equivalence");
                Check((a == b) == (row.Expected == 0) && (a != b) == (row.Expected != 0) && (a > b) == (row.Expected == 1) && (a < b) == (row.Expected == -1) && (a >= b) == (row.Expected >= 0) && (a <= b) == (row.Expected <= 0), "all six original operator conditions");
            }
            var untouched = default(GameVersion);
            Check(!untouched.Equals((object)null) && !untouched.Equals((object)"0") && Read<string>(untouched, "m_version") == null && Read<uint[]>(untouched, "m_numericIdentifiers") == null, "wrong/null object equals does not parse receiver");
            var aDefault = default(GameVersion); var bDefault = default(GameVersion);
            Check(aDefault == bDefault && Read<string>(aDefault, "m_version") == null && Read<string>(bDefault, "m_version") == null, "operator parses only value copies");
            Check(aDefault.CompareTo(bDefault) == 0 && Read<bool>(aDefault, "m_isParsed") && !Read<bool>(bDefault, "m_isParsed"), "CompareTo receiver mutates, by-value other caller stays default");
            object boxedOther = default(GameVersion); var receiver = default(GameVersion);
            Check(receiver.Equals(boxedOther) && Read<bool>(receiver, "m_isParsed") && !Read<bool>((GameVersion)boxedOther, "m_isParsed"), "Equals object copies original boxed argument before parse");
            var lazy = Raw("01.002");
            Check(lazy.ToString() == "1.2" && Read<bool>(lazy, "m_isParsed"), "legitimate serialized text lazy parse/canonicalization");

            Sub(new uint[]{1}, new uint[]{1,2}, 1, true); Sub(new uint[]{1}, new uint[]{1,2}, 2, true); Sub(new uint[]{1}, new uint[]{1,2}, 3, false); Sub(new uint[]{1}, new uint[]{1,2}, 0, false);
            Sub(new uint[]{0}, new uint[]{1}, 0, false); Sub(new uint[]{0}, new uint[]{1}, 2, false);
            Sub(new uint[]{1,2}, new uint[]{1,3}, 2, true); Sub(new uint[]{1,3}, new uint[]{1,2}, 2, false); Sub(new uint[]{1,2}, new uint[]{1,3}, 3, false);
            Sub(new uint[]{1,2,3}, new uint[]{1,2,4}, 3, true); Sub(new uint[]{1,2,5}, new uint[]{1,2,4}, 3, false);
            Sub(new uint[]{1,2,3,99}, new uint[]{1,2,3,0}, 3, true); Sub(new uint[]{1,2,3,99}, new uint[]{1,2,3,0}, 4, false);
            Sub(new uint[]{1}, new uint[]{1,0,0}, 0, true); Sub(new uint[]{1,2}, new uint[]{1,2}, uint.MaxValue, true); Sub(new uint[]{1,2}, new uint[]{1,3}, uint.MaxValue, false);
            var unparsedOther = Raw("1"); var parsed = new GameVersion(1u);
            Throws<NullReferenceException>(() => parsed.SubversionOf(unparsedOther, 1), "Subversion never parses genuine by-value other");
            var lazyReceiver = Raw("01.2");
            Check(lazyReceiver.SubversionOf(new GameVersion(1u,2u),2) && Read<bool>(lazyReceiver,"m_isParsed"), "Subversion parses receiver itself");
            var emptyParsed = Raw("", Array.Empty<uint>(), true);
            Throws<IndexOutOfRangeException>(() => emptyParsed.SubversionOf(unparsedOther, 1), "receiver first index fails before other null array");
            Check(emptyParsed.CompareTo(Raw("", Array.Empty<uint>(), true)) == 0, "CompareTo zero-length parsed arrays equal without major indexing");
            var nullParsed = Raw(null, null, true);
            Check(nullParsed.GetHashCode() == 0 && nullParsed.ToString() == null, "parsed flag respects cached-null hash/string branch without logging");
            Throws<NullReferenceException>(() => nullParsed.CompareTo(new GameVersion(0u)), "CompareTo cachednull array dereference preserved");

            var zero = new Range(0, false); Check(Generate(zero) == null && zero.CountCalls == 1 && zero.ReadCalls == 0, "format zeroCount returnsnull before builder lock");
            var negative = new Range(-1, false); Check(Generate(negative) == "" && negative.CountCalls == 1 && negative.ReadCalls == 0, "negative custom count passeszero guard then locked empty result");
            var good = new Range(2, false); Check(Generate(good) == "1.2" && good.CountCalls == 1 && good.ReadCalls == 2, "format snapshots Count and indexes no enumerator");
            Throws<NullReferenceException>(() => Generate(null), "format null count dereference");
            Throws<InvalidOperationException>(() => Generate(new Range(1, true)), "format index fault propagates");
            Check(!System.Threading.Monitor.IsEntered(fields[6].GetValue(null)) && Generate(new Range(2, false)) == "1.2", "original shared lock released after index fault");
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Check(new GameVersion(1u,2000u).ToString() == "1.2000" && GameVersion.IsVersionStringValid("1.2000") && !GameVersion.IsVersionStringValid("1,2000"), "invariant unsigned parser and actual numeric formatting under alternate culture");
            }
            finally { CultureInfo.CurrentCulture = culture; }

            // Exact native diagnostics present but never routed through a fixture logger.
            Check(Calls(Type.GetConstructor(new[]{typeof(uint[])})).Count(m => m.DeclaringType == typeof(Hardlight.HLOutput) && m.Name == "LogError") == 1, "params ctor retains registered error call");
            Check(Calls(Type.GetConstructor(new[]{typeof(string)})).Count(m => m.DeclaringType == typeof(Hardlight.HLOutput) && m.Name == "LogError") == 1, "text ctor retains registered error call");
            Check(Calls(Method("ParseVersionString")).Count(m => m.DeclaringType == typeof(Hardlight.HLOutput) && m.Name == "LogError") == 1, "lazy parser retains registered error call");
            Check(Calls(Method("SubversionOf",Type,typeof(uint))).Count(m => m.DeclaringType == Type && m.Name == "ParseVersionString") == 1 && Calls(Method("CompareTo", Type)).Count(m => m.DeclaringType == Type && m.Name == "ParseVersionString") == 2, "one versus two native parse-call ordering boundaries");
            return s_checks;
        }

        // Actual Unity JSON serializer assertions remain unrun until root-owned engine execution.
        public static void Run()
        {
            RunManaged();
            var value = JsonUtility.FromJson<GameVersion>("{\"m_version\":\"001.002\"}");
            Check(value.ToString() == "1.2", "actual Unity serialized original private text/canonical parse");
            string encoded = JsonUtility.ToJson(value);
            Check(encoded == "{\"m_version\":\"1.2\"}", "actual Unity onlyoriginal serialized field, nonserialized caches absent");
            var restored = JsonUtility.FromJson<GameVersion>(encoded);
            Check(Read<uint[]>(restored,"m_numericIdentifiers") == null && !Read<bool>(restored,"m_isParsed"), "actual deserialize does not serialize runtime caches");
            Check(restored.CompareTo(new GameVersion(1u,2u,0u)) == 0 && restored.ToString() == "1.2", "actual deserialize numeric cache regenerated before padded compare");
            var omitted = JsonUtility.FromJson<GameVersion>("{}");
            Check(omitted.ToString() == "0", "actual omitted original field uses lazy default rather than invented DTOctor");
            Debug.Log("VersionVerification passed " + s_checks + " checks; registered invalid-version diagnostics and original definition loads remain unverified.");
        }
        private static string Empty(int i,string v) => "The numeric identifier in location " + i + " of version '" + v + "' split using '.' as separator is null or empty.";
        private static string Bad(int i,string v,string n) => "The numeric identifier in location " + i + " of version '" + v + "' split using '.' as separator, which is '" + n + "', is not an unsigned digit only number.";
        private static void Sub(uint[] a,uint[] b,uint ordinal,bool expected) => Check(new GameVersion(a).SubversionOf(new GameVersion(b),ordinal) == expected, "native Subversion ordinal/padded prefix decision" + ordinal);
        private sealed class Range : IReadOnlyList<uint>
        {
            private readonly int m_count; private readonly bool m_fault; public int CountCalls; public int ReadCalls;
            public Range(int count,bool fault) { m_count=count; m_fault=fault; }
            public int Count { get { ++CountCalls; return m_count; } }
            public uint this[int index] { get { ++ReadCalls; if(m_fault)throw new InvalidOperationException("caller index fault");return (uint)index+1; } }
            public IEnumerator<uint> GetEnumerator() => throw new InvalidOperationException("native uses indexing");
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        private static IEnumerable<MethodBase> Calls(MethodBase method)
        {
            byte[] il = method.GetMethodBody().GetILAsByteArray(); var codes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>(ushort)o.Value);
            for(int p=0;p<il.Length;)
            {
                ushort code=il[p++];if(code==0xfe)code=(ushort)(0xfe00|il[p++]);OpCode op=codes[code];int size;
                switch(op.OperandType)
                {
                    case OperandType.InlineNone:size=0;break;
                    case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:size=1;break;
                    case OperandType.InlineVar:size=2;break;
                    case OperandType.InlineI8:case OperandType.InlineR:size=8;break;
                    case OperandType.InlineSwitch:size=4+BitConverter.ToInt32(il,p)*4;break;
                    default:size=4;break;
                }
                if(op.OperandType==OperandType.InlineMethod)yield return method.Module.ResolveMethod(BitConverter.ToInt32(il,p),method.DeclaringType.GetGenericArguments(),method.IsGenericMethod?method.GetGenericArguments():null);
                p+=size;
            }
        }
    }
}
