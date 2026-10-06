using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ClientDataAPI;
using UnityEngine;

namespace ProjectLucid
{
    // Bounded original codec/pool checks. Full loading and engine lifecycle are separate.
    // Empty shipped interfaces are genuine; concrete method flags are restored narrowly.
    public static class ClientDataAPIVerification
    {
        private static int checks;
        private static readonly BindingFlags AllOwn = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static void Check(bool condition, string message) { ++checks; if (!condition) throw new InvalidOperationException(message); }
        private static void Bytes(byte[] actual, byte[] expected, string label) { Check(actual.SequenceEqual(expected), label + ": " + BitConverter.ToString(actual)); }
        private static void Throws<T>(Action action, string label) where T : Exception
        {
            try { action(); } catch (Exception error) { if (error is TargetInvocationException && error.InnerException != null) error = error.InnerException; Check(error is T, label + ": " + error.GetType()); return; }
            throw new InvalidOperationException(label + ": no exception");
        }
        private static FieldInfo Field(Type type, string name) => type.GetField(name, AllOwn);
        private static object Call(Type type, string name, params object[] args) => type.GetMethod(name, AllOwn).Invoke(null, args);
        private static bool Acquired(object value) => (bool)Field(value.GetType(), "m_poolAcquired").GetValue(value);

        // Preserve original static references, values, cached object contents and per-entry
        // flags so verification can run twice without altering a shared codec registry.
        private sealed class State : IDisposable
        {
            private readonly List<KeyValuePair<FieldInfo, object>> statics = new List<KeyValuePair<FieldInfo, object>>();
            private readonly Dictionary<object, List<KeyValuePair<FieldInfo, object>>> instances = new Dictionary<object, List<KeyValuePair<FieldInfo, object>>>();
            public State(Type[] types)
            {
                foreach (Type type in types)
                {
                    foreach (FieldInfo field in type.GetFields(AllOwn).Where(f => f.IsStatic && !f.IsLiteral && !f.IsInitOnly))
                    {
                        object value = field.GetValue(null);
                        statics.Add(new KeyValuePair<FieldInfo, object>(field, value));
                        if (value != null && value.GetType() == type) Capture(value);
                        if (value is Array array) foreach (object entry in array) if (entry != null && entry.GetType() == type) Capture(entry);
                    }
                }
            }
            private void Capture(object value)
            {
                if (instances.ContainsKey(value)) return;
                instances.Add(value, value.GetType().GetFields(AllOwn).Where(f => !f.IsStatic).Select(f => new KeyValuePair<FieldInfo, object>(f, f.GetValue(value))).ToList());
            }
            public void Dispose()
            {
                foreach (var pair in instances) foreach (var field in pair.Value) field.Key.SetValue(pair.Key, field.Value);
                foreach (var pair in statics) pair.Key.SetValue(null, pair.Value);
            }
        }

        public static int RunBundledData()
        {
            string directory = System.IO.Path.Combine(Application.streamingAssetsPath, "LanguageStrings");
            string[] files = System.IO.Directory.GetFiles(directory, "*.bytes").OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (files.Length == 0) throw new InvalidOperationException("No extracted language data is present.");
            Type[] types = { typeof(LocalisedString), typeof(ClientDataAPI.StringTable), typeof(SupportedLanguage), typeof(LocalisationDefinitions) };
            using (new State(types))
            {
                foreach (Type type in types)
                {
                    // Fresh arrays keep the shared pool entries untouched; State
                    // restores the original references and counters even on failure.
                    Field(type, "s_poolSize").SetValue(null, 20);
                    Field(type, "s_poolNumAcquired").SetValue(null, 0);
                    Field(type, "s_poolInstances").SetValue(null, Array.CreateInstance(type, 20));
                }
                int strings = 0;
                foreach (string file in files)
                {
                    byte[] input = System.IO.File.ReadAllBytes(file);
                    var data = LocalisationDefinitions.decode(input);
                    if (data == null || data.StringTable == null || data.StringTable.Strings == null || data.StringTable.Strings.Length == 0)
                        throw new InvalidOperationException("Bundled language table is incomplete: " + System.IO.Path.GetFileName(file));
                    if (!input.SequenceEqual(data.encode()))
                        throw new InvalidOperationException("Bundled language bytes do not roundtrip: " + System.IO.Path.GetFileName(file));
                    if (!input.SequenceEqual(System.IO.File.ReadAllBytes(file)))
                        throw new InvalidOperationException("Bundled language input changed during verification.");
                    strings += data.StringTable.Strings.Length;
                }
                Debug.Log("Project Lucid decoded and re-encoded " + files.Length + " bundled languages / " + strings + " localized entries without changing their bytes.");
                return files.Length;
            }
        }

        public static void Run() => Debug.Log("Project Lucid ClientDataAPI bounded codec/pool checks: " + RunManaged() + "; authored language loading remains unresolved.");
        public static int RunManaged()
        {
            checks = 0;
            Type[] types = { typeof(LocalisedString), typeof(ClientDataAPI.StringTable), typeof(SupportedLanguage), typeof(LocalisationDefinitions) };
            using (new State(types))
            {
                Metadata();
                foreach (Type type in types) Pools(type);
                Scalars(); Tables(); Definitions(); FailureOrder(); Ownership();
            }
            return checks;
        }

        private static void Metadata()
        {
            Type[] types = { typeof(LocalisedString), typeof(ClientDataAPI.StringTable), typeof(SupportedLanguage), typeof(LocalisationDefinitions) };
            string[][] fields = {
                new[] { "ID", "NUM_ARGS_DEFAULT", "m_id", "m_content", "m_numArgs", "s_encodeInstance", "m_poolAcquired", "s_poolNumAcquired", "s_poolSize", "s_poolInstances" },
                new[] { "ID", "LANGUAGE_DEFAULT", "m_language", "m_strings", "m_hash", "s_encodeInstance", "m_poolAcquired", "s_poolNumAcquired", "s_poolSize", "s_poolInstances" },
                new[] { "ID", "LANGUAGE_DEFAULT", "LOCALISED_NAME_DEFAULT", "m_language", "m_localisedName", "m_isRTL", "s_encodeInstance", "m_poolAcquired", "s_poolNumAcquired", "s_poolSize", "s_poolInstances" },
                new[] { "ID", "m_supportedLanguages", "m_stringTable", "s_encodeInstance", "m_poolAcquired", "s_poolNumAcquired", "s_poolSize", "s_poolInstances" }
            };
            int[] ids = { -1700661691, 146798109, -1632469498, 1007235840 };
            for (int i = 0; i < types.Length; ++i)
            {
                Type type = types[i];
                Check(type.Assembly.GetName().Name == "HLAutoGenerated", "original model assembly");
                Check(type.IsSealed && type.IsPublic && type.BaseType == typeof(object), "original sealed Object base");
                Check((type.Attributes & TypeAttributes.BeforeFieldInit) != 0, "original beforefieldinit");
                Check(type.GetInterfaces().OrderBy(t => t.Name).SequenceEqual(new[] { typeof(DecodableMessage), typeof(EncodableMessage) }), "original two interfaces");
                FieldInfo[] own = type.GetFields(AllOwn).OrderBy(f => f.MetadataToken).ToArray();
                Check(own.Select(f => f.Name).SequenceEqual(fields[i]), "exact ordered complete fields " + type.Name);
                foreach (FieldInfo field in own) Check(field.GetCustomAttributesData().Count == 0, "original no field attrs " + field.Name);
                Check(Field(type, "ID").IsLiteral && (int)Field(type, "ID").GetRawConstantValue() == ids[i], "original constant message id");
                Check((int)type.GetProperty("MessageId").GetValue(Activator.CreateInstance(type)) == ids[i], "message id accessor");
                foreach (FieldInfo field in own.Where(f => f.Name.EndsWith("_DEFAULT"))) Check(field.IsStatic && field.IsInitOnly && (int)field.GetValue(null) == 0, "original readonly zero default");
                Check(type.GetConstructors(AllOwn).Count(c => !c.IsStatic) == 1 && type.TypeInitializer != null, "one instance/static constructor");
                Check(type.GetMethods(AllOwn).Length == (i == 3 ? 20 : 22), "complete own method count excludes two constructors");
                Check(type.GetMethod("encode", AllOwn, null, new[] { typeof(NetworkBuffer).MakeByRefType(), typeof(int) }, null).IsPrivate, "original length overload private");
                var attrs = type.GetCustomAttributesData();
                Check(attrs.Count == 2, "only two original IL2CPP attributes");
                int[] order = i == 3 ? new[] { 2, 1 } : new[] { 1, 2 };
                for (int a = 0; a < 2; ++a)
                {
                    Check(attrs[a].AttributeType.FullName == "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute", "original option type");
                    Check(Convert.ToInt32(attrs[a].ConstructorArguments[0].Value) == order[a] && Equals(attrs[a].ConstructorArguments[1].Value, false), "original option/order/value");
                }
                // This deliberately verifies an explicit limitation, not native metadata parity.
                foreach (MethodInfo method in type.GetMethods(AllOwn).Where(m => m.Name == "get_MessageId" || m.Name == "encodedLength" || m.Name == "decodeInto" || (m.Name == "encode" && m.IsPublic)))
                    Check(method.IsVirtual && method.IsFinal && method.GetBaseDefinition() == method && (int)method.Attributes == (method.IsSpecialName ? 0x09e6 : 0x01e6), "original final virtual newslot flags and concrete base definition");
                Default(Activator.CreateInstance(type));
            }
            foreach (Type contract in new[] { typeof(EncodableMessage), typeof(DecodableMessage) })
            {
                Check(contract.IsPublic && contract.IsInterface && contract.Assembly.GetName().Name == "HLNetworking.Runtime", "real original interface identity");
                Check(contract.GetMethods(AllOwn).Length == 0 && contract.GetFields(AllOwn).Length == 0 && contract.GetInterfaces().Length == 0 && contract.GetCustomAttributesData().Count == 0, "raw original interface graph has zero members/bases/attrs");
            }
        }

        private static void Default(object value)
        {
            foreach (FieldInfo field in value.GetType().GetFields(AllOwn).Where(f => !f.IsStatic && f.Name != "m_poolAcquired"))
                Check(Equals(field.GetValue(value), field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null), "reset/default " + value.GetType().Name + "." + field.Name);
        }
        private static void Dirty(object value)
        {
            foreach (FieldInfo field in value.GetType().GetFields(AllOwn).Where(f => !f.IsStatic && f.Name != "m_poolAcquired"))
            {
                object dirty = field.FieldType == typeof(int) ? (object)13 : field.FieldType == typeof(bool) ? true : field.FieldType == typeof(string) ? "stale" : field.FieldType.IsArray ? Array.CreateInstance(field.FieldType.GetElementType(), 0) : Activator.CreateInstance(field.FieldType);
                field.SetValue(value, dirty);
            }
        }
        private static void Pools(Type type)
        {
            Check((int)Field(type, "s_poolSize").GetValue(null) == 20 && ((Array)Field(type, "s_poolInstances").GetValue(null)).Length == 20, "native original initial pool size");
            Call(type, "PoolResize", 3);
            object a = Call(type, "PoolAcquire"), b = Call(type, "PoolAcquire");
            Check(Acquired(a) && Acquired(b) && (int)Call(type, "PoolNumAcquired") == 2, "pooled two acquire");
            Dirty(a); type.GetMethod("PoolRelease").Invoke(a, null);
            Check(!Acquired(a) && (int)Call(type, "PoolNumAcquired") == 1, "release changes flag/count only");
            Check(type.GetFields(AllOwn).Where(f => !f.IsStatic && f.Name != "m_poolAcquired").Any(f => !Equals(f.GetValue(a), f.FieldType.IsValueType ? Activator.CreateInstance(f.FieldType) : null)), "release retains stale payload");
            object c = Call(type, "PoolAcquire");
            Check(!ReferenceEquals(a, c) && Acquired(c), "first scan starts at acquired count, chooses high null slot before lower free slot");
            object again = Call(type, "PoolAcquire");
            Check(ReferenceEquals(a, again) && Acquired(a) && (int)Call(type, "PoolNumAcquired") == 3, "wrap scan finds lower released slot"); Default(a);
            object fallback = Call(type, "PoolAcquire");
            Check(!Acquired(fallback) && (int)Call(type, "PoolNumAcquired") == 3 && !new[] { a, b, c }.Contains(fallback), "full pool returns unpooled object");
            type.GetMethod("PoolRelease").Invoke(fallback, null);
            Check((int)Call(type, "PoolNumAcquired") == 3, "unpooled release no counter effect");
            type.GetMethod("PoolRelease").Invoke(a, null); type.GetMethod("PoolRelease").Invoke(a, null);
            Check((int)Call(type, "PoolNumAcquired") == 2, "duplicate release idempotent");
            object cached = Call(type, "GetEncodingInstance"); Dirty(cached);
            Check(ReferenceEquals(cached, Call(type, "GetEncodingInstance")), "same encoding singleton"); Default(cached);
            Call(type, "PoolResize", 2);
            Check(!Acquired(b) && !Acquired(c) && (int)Call(type, "PoolNumAcquired") == 0, "resize unflags every old pooled reference");
            Check(!((Array)Field(type, "s_poolInstances").GetValue(null)).Cast<object>().Any(x => x != null), "resize fresh null slots");
            object held = Call(type, "PoolAcquire"); Dirty(held);
            Array before = (Array)Field(type, "s_poolInstances").GetValue(null);
            Throws<OverflowException>(() => Call(type, "PoolResize", -1), "negative resize allocation failure");
            Check(!Acquired(held) && (int)Field(type, "s_poolSize").GetValue(null) == -1 && ReferenceEquals(before, Field(type, "s_poolInstances").GetValue(null)) && (int)Call(type, "PoolNumAcquired") == 1, "resize failure keeps original ordered partial state");
            Check(!Acquired(Call(type, "PoolAcquire")) && (int)Call(type, "PoolNumAcquired") == 1, "negative-size failed resize subsequent unpooled fallback");
            type.GetMethod("PoolRelease").Invoke(held, null);
            Check((int)Call(type, "PoolNumAcquired") == 1, "unflagged prior held object cannot repair retained count");
            Call(type, "PoolResize", 20);
        }

        private static LocalisedString Text(string id = "a", string content = "b", int args = 0) => new LocalisedString { Id = id, Content = content, NumArgs = args };
        private static ClientDataAPI.StringTable EmptyTable() => new ClientDataAPI.StringTable { Strings = new LocalisedString[0], Hash = "" };
        private static void Scalars()
        {
            Bytes(Text().encode(), new byte[] { 1, 2, 97, 2, 2, 98 }, "independent text wire vector");
            Bytes(Text("", "").encode(), new byte[] { 1, 1, 2, 1 }, "empty mandatory strings");
            Bytes(Text(args:128).encode(), new byte[] { 1, 2, 97, 2, 2, 98, 3, 128, 1 }, "optional args varint");
            Bytes(Text(args:-1).encode(), new byte[] { 1, 2, 97, 2, 2, 98, 3, 255, 255, 255, 255, 15 }, "signed args uses unsigned bits");
            LocalisedString text = LocalisedString.decode(new byte[] { 1, 2, 97, 2, 2, 98, 3, 128, 1 });
            Check(text.Id == "a" && text.Content == "b" && text.NumArgs == 128 && !Acquired(text), "scalar golden decode uses new nonpool object");
            Throws<NullReferenceException>(() => new LocalisedString().encodedLength(), "missing required id");
            Throws<NullReferenceException>(() => Text("a", null).encode(), "missing required content");
            Bytes(new SupportedLanguage().encode(), new byte[0], "all optional supported-language fields omitted");
            Bytes(new SupportedLanguage { IsRTL = true }.encode(), new byte[] { 2 }, "RTL true is tag presence only");
            Bytes(new SupportedLanguage { Language = 1, LocalisedName = 2, IsRTL = true }.encode(), new byte[] { 0, 1, 1, 2, 2 }, "supported-language golden vector");
            SupportedLanguage language = SupportedLanguage.decode(new byte[] { 0, 1, 1, 2, 2 });
            Check(language.Language == 1 && language.LocalisedName == 2 && language.IsRTL && !Acquired(language), "supported golden decode");
            foreach (int scalar in new[] { int.MinValue, -1, 0, 1, 127, 128, 16384, int.MaxValue })
            {
                LocalisedString item = Text("ひ", "é", scalar); byte[] data = item.encode(); Check(data.Length == item.encodedLength(), "text boundary encoded length");
                LocalisedString round = LocalisedString.decode(data); Check(round.Id == item.Id && round.Content == item.Content && round.NumArgs == scalar, "text UTF8/scalar boundary roundtrip");
                SupportedLanguage item2 = new SupportedLanguage { Language = scalar, LocalisedName = scalar, IsRTL = true }; byte[] data2 = item2.encode(); Check(data2.Length == item2.encodedLength(), "language boundary encoded length");
                SupportedLanguage round2 = SupportedLanguage.decode(data2); Check(round2.Language == scalar && round2.LocalisedName == scalar && round2.IsRTL, "language scalar boundary roundtrip");
            }
        }
        private static void Tables()
        {
            Bytes(EmptyTable().encode(), new byte[] { 2, 0, 3, 1 }, "empty nonnull strings array/hash");
            ClientDataAPI.StringTable table = new ClientDataAPI.StringTable { Language = 1, Strings = new[] { null, Text() }, Hash = "" };
            byte[] expected = { 1, 1, 2, 2, 0, 7, 1, 2, 97, 2, 2, 98, 3, 1 };
            Bytes(table.encode(), expected, "nullable array entries use zero or child length plus one");
            var round = ClientDataAPI.StringTable.decode(expected);
            Check(round.Language == 1 && round.Hash == "" && round.Strings.Length == 2 && round.Strings[0] == null && round.Strings[1].Content == "b", "table golden decode");
            Check(!Acquired(round) && !Acquired(round.Strings[1]), "nested decode does not acquire pools");
            Throws<NullReferenceException>(() => new ClientDataAPI.StringTable().encodedLength(), "table mandatory array");
            Throws<NullReferenceException>(() => new ClientDataAPI.StringTable { Strings = new LocalisedString[0] }.encodedLength(), "table mandatory hash");
            Throws<NullReferenceException>(() => new ClientDataAPI.StringTable { Strings = new[] { new LocalisedString() }, Hash = "h" }.encodedLength(), "nested mandatory string validation");
            var emptyChild = ClientDataAPI.StringTable.decode(new byte[] { 2, 2, 0, 1 });
            Check(emptyChild.Strings[0] == null && emptyChild.Strings[1] != null && emptyChild.Strings[1].Id == null, "null marker distinct from present zero-length child");
            var payload = table.encode(); byte[] framed = new byte[payload.Length + 4]; Array.Copy(payload, 0, framed, 2, payload.Length);
            var offset = ClientDataAPI.StringTable.decode(framed, 2, payload.Length); Check(offset.Strings[1].Id == "a", "byte-array explicit offset/length overload");
            NetworkBuffer buffer = new NetworkBuffer(framed, 0, framed.Length); buffer.skip(2);
            var slice = ClientDataAPI.StringTable.decode(ref buffer, payload.Length);
            Check(slice.Language == 1 && buffer.Offset == payload.Length + 2 && buffer.Remaining == 2, "ref length advances only declared parent slice");
        }
        private static void Definitions()
        {
            LocalisationDefinitions defs = new LocalisationDefinitions { SupportedLanguages = new[] { null, new SupportedLanguage { IsRTL = true }, new SupportedLanguage() }, StringTable = EmptyTable() };
            byte[] expected = { 1, 3, 0, 2, 2, 1, 2, 4, 2, 0, 3, 1 };
            Bytes(defs.encode(), expected, "definition golden framing: nullable language children plus required raw-length table");
            var round = LocalisationDefinitions.decode(expected);
            Check(round.SupportedLanguages.Length == 3 && round.SupportedLanguages[0] == null && round.SupportedLanguages[1].IsRTL && round.SupportedLanguages[2] != null, "definition optional elements decoded");
            Check(round.StringTable.Strings.Length == 0 && round.StringTable.Hash == "", "required nested table decode");
            Check(!Acquired(round) && !Acquired(round.SupportedLanguages[1]) && !Acquired(round.StringTable), "definition nested ordinary allocations");
            Throws<NullReferenceException>(() => new LocalisationDefinitions().encodedLength(), "definition mandatory supported-languages array");
            Throws<NullReferenceException>(() => new LocalisationDefinitions { SupportedLanguages = new SupportedLanguage[0] }.encodedLength(), "definition mandatory table");
            var incomplete = LocalisationDefinitions.decode(new byte[] { 2, 0 }); Check(incomplete.StringTable != null && incomplete.StringTable.Strings == null && incomplete.SupportedLanguages == null, "decoder does not validate required presence");
            Throws<NullReferenceException>(() => incomplete.encode(), "incomplete decoded graph cannot encode mandatory data");
        }
        private static void FailureOrder()
        {
            LocalisedString target = Text("old", "old", 9);
            NetworkBuffer duplicate = new NetworkBuffer(new byte[] { 1, 2, 97, 1, 2, 98 }, 0, 6);
            target.decodeInto(ref duplicate); Check(target.Id == "b" && target.Content == "old" && target.NumArgs == 9 && duplicate.Offset == 6, "duplicate equal tags overwrite without resetting unspecified fields");
            NetworkBuffer descending = new NetworkBuffer(new byte[] { 2, 2, 98, 1, 2, 97 }, 0, 6);
            try { target.decodeInto(ref descending); throw new InvalidOperationException("descending no exception"); } catch (IndexOutOfRangeException) { Check(descending.Offset == 4 && target.Content == "b" && target.Id == "b", "descending error occurs after tag before its payload"); }
            NetworkBuffer unknown = new NetworkBuffer(new byte[] { 4, 255, 128, 0 }, 0, 4); target.decodeInto(ref unknown);
            Check(unknown.Offset == 4 && target.NumArgs == 9, "unknown higher tag skips whole remaining message");
            NetworkBuffer lower = new NetworkBuffer(new byte[] { 0, 17 }, 0, 2);
            try { target.decodeInto(ref lower); throw new InvalidOperationException("lower no exception"); } catch (IndexOutOfRangeException) { Check(lower.Offset == 1, "unknown lower tag error before payload"); }
            NetworkBuffer failedChild = new NetworkBuffer(new byte[] { 1 }, 0, 1);
            try { LocalisedString.decode(ref failedChild, 1); throw new InvalidOperationException("child no exception"); } catch (IndexOutOfRangeException) { Check(failedChild.Offset == 1, "parent skip precedes failing child decode"); }
            NetworkBuffer outOfRange = new NetworkBuffer(new byte[] { 1 }, 0, 1);
            try { LocalisedString.decode(ref outOfRange, 2); throw new InvalidOperationException("range no exception"); } catch (IndexOutOfRangeException) { Check(outOfRange.Offset == 0, "failed SubRange leaves parent cursor unchanged"); }
            LocalisationDefinitions defs = new LocalisationDefinitions(); NetworkBuffer partial = new NetworkBuffer(new byte[] { 1, 1, 3, 0, 128 }, 0, 5);
            try { defs.decodeInto(ref partial); throw new InvalidOperationException("partial no exception"); } catch (IndexOutOfRangeException) { Check(partial.Offset == 5 && defs.SupportedLanguages.Length == 1 && defs.SupportedLanguages[0] == null, "array assigned before child decode, parent skipped before error, entry assigned only after success"); }
            SupportedLanguage rtl = new SupportedLanguage { IsRTL = true, Language = 7 }; NetworkBuffer empty = new NetworkBuffer(new byte[0], 0, 0); rtl.decodeInto(ref empty);
            Check(rtl.IsRTL && rtl.Language == 7, "empty decodeInto retains prior values");
            NetworkBuffer writes = new NetworkBuffer(new byte[20], 0, 20); Text().encode(ref writes); Check(writes.Offset == 6, "public encode(ref) writes computed length");
            byte[] manual = new byte[20]; NetworkBuffer privateBuffer = new NetworkBuffer(manual, 0, 20); object[] args = { privateBuffer, 99 };
            MethodInfo lengthOverload = typeof(LocalisedString).GetMethod("encode", AllOwn, null, new[] { typeof(NetworkBuffer).MakeByRefType(), typeof(int) }, null);
            Throws<IndexOutOfRangeException>(() => lengthOverload.Invoke(Text(), args), "length mismatch check after body");
            Bytes(manual.Take(6).ToArray(), new byte[] { 1, 2, 97, 2, 2, 98 }, "mismatch preserves previously written bytes");
        }
        private static void Ownership()
        {
            LocalisedString.PoolResize(2); SupportedLanguage.PoolResize(2); ClientDataAPI.StringTable.PoolResize(2); LocalisationDefinitions.PoolResize(2);
            LocalisedString child = LocalisedString.PoolAcquire(); SupportedLanguage language = SupportedLanguage.PoolAcquire(); var table = ClientDataAPI.StringTable.PoolAcquire(); var definitions = LocalisationDefinitions.PoolAcquire();
            table.Strings = new[] { child }; table.Hash = "h"; definitions.SupportedLanguages = new[] { language }; definitions.StringTable = table;
            definitions.PoolRelease(); Check(definitions.StringTable == table && definitions.SupportedLanguages[0] == language && Acquired(table) && Acquired(language), "parent release retains child payload/ownership");
            definitions.Reset(); Check(definitions.StringTable == null && definitions.SupportedLanguages == null && LocalisedString.PoolNumAcquired() == 1 && SupportedLanguage.PoolNumAcquired() == 1 && ClientDataAPI.StringTable.PoolNumAcquired() == 1, "parent reset does not release nested pools");
            table.Reset(); Check(table.Strings == null && LocalisedString.PoolNumAcquired() == 1, "table reset only drops references");
            child.PoolRelease(); language.PoolRelease(); table.PoolRelease(); Check(LocalisedString.PoolNumAcquired() == 0 && SupportedLanguage.PoolNumAcquired() == 0 && ClientDataAPI.StringTable.PoolNumAcquired() == 0 && LocalisationDefinitions.PoolNumAcquired() == 0, "only explicit nested releases update their counters");
        }
    }
}
