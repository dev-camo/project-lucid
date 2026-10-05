using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Hardlight;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    // The runtime graph uses the genuine original definition groups and definitions.
    // TrackingRange/Sink are proof-only CLR callback fixtures, not game providers or recovered bodies.
    public static class DefinitionDataVerification
    {
        private static int checks;
        private const BindingFlags Own = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static void Require(bool value, string description) { checks++; if (!value) throw new InvalidOperationException(description); }
        private static FieldInfo[] Fields(Type type) => type.GetFields(Own).OrderBy(f => f.MetadataToken).ToArray();
        private static MethodInfo[] Methods(Type type) => type.GetMethods(Own).OrderBy(m => m.MetadataToken).ToArray();
        private static void Expect<T>(Action action, string description) where T : Exception
        {
            try { action(); } catch (T) { Require(true, description); return; }
            Require(false, description);
        }
        private static void Options(Type type, params Option[] values)
        {
            var attrs = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
            Require(attrs.Select(a => a.Option).SequenceEqual(values) && attrs.All(a => Equals(a.Value, false)), type.Name + " complete ordered native options");
        }
        private static void BaseOnly(Type type, Type parent)
        {
            var method = type.GetConstructors(Own).Single(); var il = ReadIL(method).ToArray();
            Require(il.Length == 3 && il[0].op == OpCodes.Ldarg_0 && il[1].op == OpCodes.Call && ((MethodBase)il[1].value).DeclaringType == parent && ((MethodBase)il[1].value).IsConstructor && il[2].op == OpCodes.Ret, type.Name + " native base-only constructor without invented defaults");
        }
        private static MethodBase[] Calls(MethodBase method) => ReadIL(method).Where(x => x.op == OpCodes.Call || x.op == OpCodes.Callvirt || x.op == OpCodes.Newobj).Select(x => (MethodBase)x.value).ToArray();

        public static int RunManaged()
        {
            checks = 0;
            var plain = typeof(DefinitionDataType); var generic = typeof(DefinitionDataType<,>); var element = typeof(DefinitionDataType<RankType, RankDefinition>.DefinitionElement<RankDefinition>);
            Require(typeof(DataDefinitions).Assembly.GetName().Name == "Game.Runtime" && plain.Assembly == generic.Assembly && element.Assembly == plain.Assembly, "original complete Game.Runtime assembly graph");
            Require(typeof(Hardlight.CollectionExtensions).Assembly.GetName().Name == "HLUnityCore.Runtime", "original collection dependency assembly identity");
            Require(plain.IsPublic && plain.IsAbstract && plain.BaseType == typeof(ScriptableObject) && Fields(plain).Length == 0 && Methods(plain).Length == 0, "full genuine fieldless abstract ScriptableObject base");
            Options(plain, Option.NullChecks, Option.ArrayBoundsChecks); BaseOnly(plain, typeof(ScriptableObject));
            var baseElement = typeof(DefinitionDataType.DefinitionElement);
            Require(baseElement.IsNestedPublic && baseElement.IsSerializable && baseElement.BaseType == typeof(object) && Fields(baseElement).Length == 0 && Methods(baseElement).Length == 0, "full original serializable fieldless element base");
            Options(baseElement, Option.NullChecks, Option.ArrayBoundsChecks); BaseOnly(baseElement, typeof(object));
            Require(generic.IsPublic && generic.IsAbstract && generic.BaseType == plain, "full original generic inheritance");
            var parameters = generic.GetGenericArguments();
            Require(parameters.Length == 2 && parameters.Select(p => p.Name).SequenceEqual(new[] { "TKey", "TData" }) && parameters.All(p => p.GenericParameterAttributes == GenericParameterAttributes.None), "all original generic names/flags");
            Require(parameters[0].GetGenericParameterConstraints().Length == 0 && parameters[1].GetGenericParameterConstraints().SequenceEqual(new[] { typeof(ScriptableObject) }), "exact original TData constraint, no invented TKey constraint");
            Options(generic, Option.NullChecks, Option.ArrayBoundsChecks); BaseOnly(generic, plain);
            var genericFields = Fields(generic); Require(genericFields.Length == 1 && genericFields[0].Name == "m_elements" && genericFields[0].IsPublic && genericFields[0].IsDefined(typeof(SerializeField), false) && genericFields[0].FieldType.IsArray, "complete generic serialized array field");
            Require(genericFields[0].FieldType.GetElementType().GetGenericTypeDefinition().FullName == "HardlightProject.DefinitionDataType`2+DefinitionElement`1", "exact original nested three-argument element identity");
            var genericMethods = Methods(generic); Require(genericMethods.Select(m => m.Name).SequenceEqual(new[] { "GetData", "GetElementKey", "GetKeyComparer" }), "complete original generic three-method API order");
            Require(!genericMethods[0].IsVirtual && genericMethods[0].IsPublic && genericMethods.Skip(1).All(m => m.IsAbstract && m.IsVirtual && m.IsFamily && (m.Attributes & MethodAttributes.NewSlot) != 0), "genuine protected abstract contracts, no substitute bodies");
            var getDataCalls = Calls(genericMethods[0]);
            Require(getDataCalls.Length == 4 && getDataCalls[0].Name == "GetKeyComparer" && getDataCalls[1].IsConstructor && getDataCalls[1].DeclaringType.GetGenericTypeDefinition() == typeof(Dictionary<,>) && getDataCalls[2].Name == "GetElementKey" && getDataCalls[3].Name == "Add", "native comparer/constructor/key/Add call order without cache or null repair");
            Require(ReadIL(genericMethods[0]).Count(i => i.op == OpCodes.Ldfld && ((FieldInfo)i.value).Name == "Data") == 2 && ReadIL(genericMethods[0]).Count(i => i.op == OpCodes.Ldfld && ((FieldInfo)i.value).Name == "m_elements") == 1 && genericMethods[0].GetMethodBody().ExceptionHandlingClauses.Count == 0, "array foreach snapshots and exact two Data reads without exception suppression");
            Require(element.BaseType == baseElement && element.IsNestedPublic && element.IsSerializable && typeof(ISerializationCallbackReceiver).IsAssignableFrom(element), "complete original element base and serialization interface");
            Options(element, Option.NullChecks, Option.ArrayBoundsChecks);
            var ef = Fields(element); Require(ef.Select(f => f.Name).SequenceEqual(new[] { "Name", "Data" }) && ef[0].FieldType == typeof(string) && ef[1].FieldType == typeof(RankDefinition), "complete original ordered Name/Data graph");
            Require(ef.All(f => f.IsPublic && f.IsDefined(typeof(SerializeField), false) && !f.IsNotSerialized) && ef[0].IsDefined(typeof(HideInInspector), false) && !ef[1].IsDefined(typeof(HideInInspector), false), "exact native element field visibility/serialization attributes");
            Require(Methods(element).Select(m => m.Name).SequenceEqual(new[] { "OnBeforeSerialize", "GenerateName", "OnAfterDeserialize" }), "all original nested element methods");
            var before = element.GetMethod("OnBeforeSerialize", Own); var beforeCalls = Calls(before);
            Require(beforeCalls.Length == 1 && beforeCalls[0].Name == "GenerateName" && ReadIL(before).Select(i => i.op).SequenceEqual(new[] { OpCodes.Ldarg_0, OpCodes.Call, OpCodes.Ret }), "original OnBeforeSerialize tailcall target is GenerateName, not an empty callback");
            Require(ReadIL(element.GetMethod("OnAfterDeserialize", Own)).Select(i => i.op).SequenceEqual(new[] { OpCodes.Ret }), "OnAfterDeserialize genuine player RET body");
            var naming = element.GetMethod("GenerateName", Own); var nameCalls = Calls(naming);
            Require(naming.IsPrivate && nameCalls.Select(m => m.Name).SequenceEqual(new[] { "op_Equality", "get_name" }) && nameCalls.All(m => m.DeclaringType == typeof(UnityEngine.Object)), "original Unity destroyed/null comparison then real object name");
            Require(ReadIL(naming).Any(i => i.op == OpCodes.Ldstr && Equals(i.value, "NONE")), "native NONE literal, not empty-string normalization");
            var construction = element.GetConstructors().Single(); var constructorIL = ReadIL(construction).ToArray(); var constructorCalls = Calls(construction);
            Require(constructorCalls.Length == 2 && constructorCalls[0].DeclaringType == baseElement && constructorCalls[0].IsConstructor && constructorCalls[1].Name == "GenerateName", "original element base/store/GenerateName construction path");
            Require(Array.FindIndex(constructorIL, i => i.op == OpCodes.Stfld && ((FieldInfo)i.value).Name == "Data") < Array.FindLastIndex(constructorIL, i => i.op == OpCodes.Call), "Data assigned before constructor naming");
            var data = typeof(DataDefinitions); Require(data.IsPublic && !data.IsAbstract && data.BaseType == typeof(ScriptableObject), "original root definition hierarchy");
            Options(data, Option.ArrayBoundsChecks, Option.NullChecks); BaseOnly(data, typeof(ScriptableObject));
            var df = Fields(data); Require(df.Select(f => f.Name).SequenceEqual(new[] { "AssetMenu", "DefinitionsMenu", "GroupsMenu", "DataDefinitionsArray" }), "complete original four-field/constant order");
            Require((string)df[0].GetRawConstantValue() == "HardlightProject/DefinitionData/" && (string)df[1].GetRawConstantValue() == "HardlightProject/DefinitionData/Definitions/" && (string)df[2].GetRawConstantValue() == "HardlightProject/DefinitionData/Groups/" && df.Take(3).All(f => f.IsPublic && f.IsLiteral), "exact original three constant values/flags");
            Require(df[3].IsPublic && df[3].FieldType == typeof(DefinitionDataType[]) && !df[3].IsDefined(typeof(SerializeField), false), "authored public array without fabricated serialization attribute");
            var dmethods = Methods(data); Require(dmethods.Select(m => m.Name).SequenceEqual(new[] { "Get", "GetGroup" }), "complete original root method API");
            Require(dmethods[0].GetGenericArguments().Select(p => p.Name).SequenceEqual(new[] { "TKey", "TData" }) && dmethods[0].GetGenericArguments()[0].GetGenericParameterConstraints().Length == 0 && dmethods[0].GetGenericArguments()[1].GetGenericParameterConstraints().SequenceEqual(new[] { typeof(ScriptableObject) }), "root Get exact original generic constraints");
            Require(dmethods[1].GetGenericArguments().Single().GetGenericParameterConstraints().SequenceEqual(new[] { typeof(ScriptableObject) }), "root GetGroup exact original generic constraint");
            var rootCalls = Calls(dmethods[0]); Require(rootCalls.Length == 2 && rootCalls[0].Name == "GetData" && rootCalls[1].Name == "AddRange" && rootCalls[1].DeclaringType == typeof(Hardlight.CollectionExtensions), "native all-group merge through original helper, no copied dictionary replacement");
            Require(Calls(dmethods[1]).Length == 0 && ReadIL(dmethods[1]).Any(i => i.op == OpCodes.Isinst) && dmethods.All(m => ReadIL(m).Count(i => i.op == OpCodes.Ldfld && ((FieldInfo)i.value).Name == "DataDefinitionsArray") == 1), "GetGroup original CLR type checks, no Unity equality calls");
            var menu = data.GetCustomAttribute<CreateAssetMenuAttribute>(); Require(menu.fileName == "DataDefinitions" && menu.menuName == "HardlightProject/DefinitionData/Groups/All Definitions", "exact original root authoring menu");
            foreach (var group in new[] { typeof(MusicTrackDefinitionGroup), typeof(OrnamentDefinitionGroup), typeof(RankDefinitionGroup) })
            {
                var key = group.BaseType.GetGenericArguments()[0]; var value = group.BaseType.GetGenericArguments()[1];
                Require(group.IsPublic && !group.IsSealed && !group.IsAbstract && group.BaseType.GetGenericTypeDefinition() == generic && Fields(group).Length == 0, group.Name + " complete original concrete generic group hierarchy");
                Options(group, group == typeof(MusicTrackDefinitionGroup) ? new[] { Option.NullChecks, Option.ArrayBoundsChecks } : new[] { Option.ArrayBoundsChecks, Option.NullChecks });
                var ordered = group.GetCustomAttributesData().Select(a => a.AttributeType).ToArray();
                Require(ordered.SequenceEqual(group == typeof(MusicTrackDefinitionGroup) ? new[] { typeof(Il2CppSetOptionAttribute), typeof(Il2CppSetOptionAttribute), typeof(CreateAssetMenuAttribute) } : group == typeof(OrnamentDefinitionGroup) ? new[] { typeof(CreateAssetMenuAttribute), typeof(Il2CppSetOptionAttribute), typeof(Il2CppSetOptionAttribute) } : new[] { typeof(Il2CppSetOptionAttribute), typeof(CreateAssetMenuAttribute), typeof(Il2CppSetOptionAttribute) }), group.Name + " complete ordered original type attributes");
                var ownMethods = Methods(group); Require(ownMethods.Select(m => m.Name).SequenceEqual(new[] { "GetElementKey", "GetKeyComparer" }) && ownMethods.All(m => m.IsFamily && m.IsVirtual && !m.IsAbstract && (m.Attributes & MethodAttributes.NewSlot) == 0), group.Name + " original protected overrides only");
                var keyCalls = Calls(ownMethods[0]); Require(keyCalls.Length == 1 && keyCalls[0].DeclaringType == value && ownMethods[0].ReturnType == key, group.Name + " native direct key getter dependency");
                var comparer = ReadIL(ownMethods[1]).ToArray(); Require(comparer.Length == 2 && comparer[0].op == OpCodes.Ldsfld && ((FieldInfo)comparer[0].value).Name == key.Name + "Comparer" && ((FieldInfo)comparer[0].value).DeclaringType == typeof(HardlightEnumComparers) && comparer[1].op == OpCodes.Ret, group.Name + " exact maintained enum comparer");
                BaseOnly(group, group.BaseType);
                var gm = group.GetCustomAttribute<CreateAssetMenuAttribute>(); Require(gm.fileName == group.Name && gm.menuName == "HardlightProject/DefinitionData/Groups/" + group.Name, group.Name + " exact original authoring menu");
            }
            var collection = typeof(Hardlight.CollectionExtensions); Require(collection.IsPublic && collection.IsAbstract && collection.IsSealed && Fields(collection).Length == 0 && Methods(collection).Select(m => m.Name).SequenceEqual(new[] { "IsIndexValid", "AddRange" }), "maintained fieldless collection dependency subset");
            Require(collection.IsDefined(typeof(ExtensionAttribute), false) && Methods(collection).All(m => m.IsDefined(typeof(ExtensionAttribute), false) && m.IsPublic && m.IsStatic), "maintained original extension attributes and API");
            var add = collection.GetMethod("AddRange"); Require(ReadIL(add).Any(i => i.op == OpCodes.Ldstr && Equals(i.value, "Provided range to add is null.")) && Calls(add).Any(m => m.Name == "LogError" && m.DeclaringType == typeof(HLOutput)), "exact original null-range diagnostic; registered HLOutput route remains unexecuted");
            var trace = new List<string>(); var range = new TrackingRange(trace, new[] { 4, 9 }); var sink = new Sink(trace);
            sink.AddRange(range); Require(sink.Values.SequenceEqual(new[] { 4, 9 }) && trace.SequenceEqual(new[] { "enumerator", "move", "current", "add:4", "move", "current", "add:9", "move", "dispose" }), "native range traversal callback order/disposal without Count reads");
            trace.Clear(); range = new TrackingRange(trace, Array.Empty<int>()); Hardlight.CollectionExtensions.AddRange<int>(null, range); Require(trace.SequenceEqual(new[] { "enumerator", "move", "dispose" }), "native empty range does not dereference null target");
            trace.Clear(); range = new TrackingRange(trace, new[] { 4 }); Expect<NullReferenceException>(() => Hardlight.CollectionExtensions.AddRange<int>(null, range), "null target fails only for a delivered item"); Require(trace.SequenceEqual(new[] { "enumerator", "move", "current", "dispose" }), "Current precedes null target failure and enumeration is disposed");
            foreach (var point in new[] { "move", "current", "add" })
            {
                trace.Clear(); range = new TrackingRange(trace, new[] { 4 }) { ThrowAt = point }; sink = new Sink(trace) { ThrowAt = point };
                Expect<ProofException>(() => sink.AddRange(range), point + " callback failure is rethrown"); Require(trace.Last() == "dispose" && trace.Count(x => x == "dispose") == 1, point + " callback failure invokes original foreach cleanup exactly once");
            }
            var count = new CountProbe(); Require(!Hardlight.CollectionExtensions.IsIndexValid<int>(null, 0), "index null collection returns false"); Require(!count.IsIndexValid(-1) && count.Reads == 0, "negative index short-circuits Count"); Require(count.IsIndexValid(2) && count.Reads == 1, "native signed index below count"); Require(!count.IsIndexValid(3) && count.Reads == 2, "native equality at count is invalid");
            return checks;
        }

        public static void Run()
        {
            RunManaged();
            var owned = new List<UnityEngine.Object>();
            T New<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); owned.Add(value); return value; }
            try
            {
                var root = New<DataDefinitions>(); var first = New<MusicTrackDefinitionGroup>(); var second = New<MusicTrackDefinitionGroup>(); var other = New<OrnamentDefinitionGroup>(); var ranks = New<RankDefinitionGroup>();
                Require(root.DataDefinitionsArray == null && first.m_elements == null && second.m_elements == null && other.m_elements == null && ranks.m_elements == null, "actual original root/group constructors preserve null authored arrays");
                Expect<NullReferenceException>(() => root.GetGroup<MusicTrackDefinitionGroup>(), "null original root array fails");
                Expect<NullReferenceException>(() => first.GetData(), "null original element array fails without automatic repair");
                var a = New<MusicTrackDefinition>(); var b = New<MusicTrackDefinition>(); a.name = "Original A"; b.name = "Original B";
                var values = Enum.GetValues(typeof(HLAudioClipIdentifier)).Cast<HLAudioClipIdentifier>().Distinct().Take(2).ToArray();
                var identifier = typeof(MusicTrackDefinition).GetField("m_identifier", Own); identifier.SetValue(a, values[0]); identifier.SetValue(b, values[1]);
                var e = new DefinitionDataType<HLAudioClipIdentifier, MusicTrackDefinition>.DefinitionElement<MusicTrackDefinition>(a);
                Require(ReferenceEquals(e.Data, a) && e.Name == "Original A", "actual original constructor stores Data before naming");
                a.name = "Changed A"; e.OnBeforeSerialize(); e.OnAfterDeserialize(); Require(e.Name == "Changed A", "native BeforeSerialize regenerates current Unity name; AfterDeserialize leaves it untouched");
                var empty = new DefinitionDataType<HLAudioClipIdentifier, MusicTrackDefinition>.DefinitionElement<MusicTrackDefinition>(null); Require(empty.Name == "NONE" && empty.Data == null, "actual native null element literal");
                first.m_elements = new[] { e }; second.m_elements = new[] { new DefinitionDataType<HLAudioClipIdentifier, MusicTrackDefinition>.DefinitionElement<MusicTrackDefinition>(b) };
                string groupJson = JsonUtility.ToJson(first); var groupCopy = New<MusicTrackDefinitionGroup>();
                Require(groupJson.Contains("\"m_elements\"") && groupJson.Contains("Changed A") && groupJson.Contains("\"Data\""), "actual original inherited generic element field graph serializes current generated name");
                JsonUtility.FromJsonOverwrite(groupJson, groupCopy);
                Require(groupCopy.m_elements.Length == 1 && groupCopy.m_elements[0].Name == "Changed A" && ReferenceEquals(groupCopy.m_elements[0].Data, a) && !ReferenceEquals(groupCopy.m_elements[0], e), "actual inline generic element roundtrip preserves value and Unity reference, not inline object alias");
                var direct = first.GetData(); var repeated = first.GetData(); Require(!ReferenceEquals(direct, repeated) && ReferenceEquals(direct.Comparer, HardlightEnumComparers.HLAudioClipIdentifierComparer) && ReferenceEquals(direct[values[0]], a), "actual group creates fresh dictionary using original comparer and reference");
                root.DataDefinitionsArray = new DefinitionDataType[] { null, other, first, ranks, second };
                string rootJson = JsonUtility.ToJson(root); var rootCopy = New<DataDefinitions>();
                Require(rootJson.Contains("\"DataDefinitionsArray\""), "actual authored public root array serializes without fabricated attributes");
                JsonUtility.FromJsonOverwrite(rootJson, rootCopy);
                Require(rootCopy.DataDefinitionsArray.Length == 5 && rootCopy.DataDefinitionsArray[0] == null && ReferenceEquals(rootCopy.DataDefinitionsArray[1], other) && ReferenceEquals(rootCopy.DataDefinitionsArray[2], first) && ReferenceEquals(rootCopy.DataDefinitionsArray[3], ranks) && ReferenceEquals(rootCopy.DataDefinitionsArray[4], second), "actual original root-array order and Unity reference roundtrip");
                Require(ReferenceEquals(root.GetGroup<MusicTrackDefinitionGroup>(), first), "actual first matching original group, unrelated/null entries skipped");
                var combined = root.Get<HLAudioClipIdentifier, MusicTrackDefinition>(); Require(combined.Count == 2 && ReferenceEquals(combined.Comparer, direct.Comparer) && ReferenceEquals(combined[values[0]], a) && ReferenceEquals(combined[values[1]], b), "actual original typed groups merge all definitions with first comparer");
                root.DataDefinitionsArray = new DefinitionDataType[] { null, ranks }; Require(root.Get<HLAudioClipIdentifier, MusicTrackDefinition>() == null && root.GetGroup<MusicTrackDefinitionGroup>() == null, "actual no matching group returns null rather than empty substitutes");
                first.m_elements = Array.Empty<DefinitionDataType<HLAudioClipIdentifier, MusicTrackDefinition>.DefinitionElement<MusicTrackDefinition>>(); Require(first.GetData().Count == 0, "genuine authored empty array yields empty comparer-backed dictionary");
                first.m_elements = new[] { e, e }; Expect<ArgumentException>(() => first.GetData(), "native duplicate keys fail within a group");
                first.m_elements = new[] { e }; second.m_elements = new[] { e }; root.DataDefinitionsArray = new DefinitionDataType[] { first, second }; Expect<ArgumentException>(() => root.Get<HLAudioClipIdentifier, MusicTrackDefinition>(), "native duplicate keys fail while merging groups");
                first.m_elements = new DefinitionDataType<HLAudioClipIdentifier, MusicTrackDefinition>.DefinitionElement<MusicTrackDefinition>[] { null }; Expect<NullReferenceException>(() => first.GetData(), "native null element remains invalid");
                first.m_elements = new[] { empty }; Expect<NullReferenceException>(() => first.GetData(), "native concrete group null Data remains invalid");
                var dead = New<MusicTrackDefinitionGroup>(); root.DataDefinitionsArray = new DefinitionDataType[] { dead, second }; UnityEngine.Object.DestroyImmediate(dead);
                Require(dead == null && ReferenceEquals(root.GetGroup<MusicTrackDefinitionGroup>(), dead), "original CLR type matching returns destroyed first group, distinct from Unity null test");
                var deadData = New<MusicTrackDefinition>(); UnityEngine.Object.DestroyImmediate(deadData);
                var deadElement = new DefinitionDataType<HLAudioClipIdentifier, MusicTrackDefinition>.DefinitionElement<MusicTrackDefinition>(deadData); Require(deadElement.Name == "NONE" && ReferenceEquals(deadElement.Data, deadData), "original Unity null naming accepts destroyed Data without clearing reference");
            }
            finally { foreach (var value in owned) if (value != null) UnityEngine.Object.DestroyImmediate(value); }
            Debug.Log("Project Lucid original definition data proof: PASS checks=" + checks);
        }

        private sealed class ProofException : Exception { }
        private sealed class CountProbe : IReadOnlyCollection<int>
        {
            public int Reads; public int Count { get { Reads++; return 3; } }
            public IEnumerator<int> GetEnumerator() => throw new InvalidOperationException("unneeded enumeration");
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        private sealed class TrackingRange : IReadOnlyCollection<int>
        {
            private readonly List<string> trace; private readonly int[] values; public string ThrowAt;
            public TrackingRange(List<string> trace, int[] values) { this.trace = trace; this.values = values; }
            public int Count => throw new InvalidOperationException("Native AddRange does not read Count");
            public IEnumerator<int> GetEnumerator() { trace.Add("enumerator"); return new Enumerator(this); }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            private sealed class Enumerator : IEnumerator<int>
            {
                private readonly TrackingRange owner; private int index = -1;
                public Enumerator(TrackingRange owner) { this.owner = owner; }
                public bool MoveNext() { owner.trace.Add("move"); if (owner.ThrowAt == "move") throw new ProofException(); return ++index < owner.values.Length; }
                public int Current { get { owner.trace.Add("current"); if (owner.ThrowAt == "current") throw new ProofException(); return owner.values[index]; } }
                object IEnumerator.Current => Current;
                public void Dispose() => owner.trace.Add("dispose");
                public void Reset() => throw new NotSupportedException();
            }
        }
        private sealed class Sink : ICollection<int>
        {
            private readonly List<string> trace; public readonly List<int> Values = new List<int>(); public string ThrowAt;
            public Sink(List<string> trace) { this.trace = trace; }
            public void Add(int value) { trace.Add("add:" + value); if (ThrowAt == "add") throw new ProofException(); Values.Add(value); }
            public int Count => Values.Count; public bool IsReadOnly => false;
            public void Clear() => Values.Clear(); public bool Contains(int value) => Values.Contains(value);
            public void CopyTo(int[] array, int index) => Values.CopyTo(array, index); public bool Remove(int value) => Values.Remove(value);
            public IEnumerator<int> GetEnumerator() => Values.GetEnumerator(); IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        private static IEnumerable<(OpCode op, object value)> ReadIL(MethodBase method)
        {
            var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Value);
            byte[] il = method.GetMethodBody().GetILAsByteArray(); var ta = method.DeclaringType.GetGenericArguments(); var ma = method.IsGenericMethod ? method.GetGenericArguments() : null;
            for (int p = 0; p < il.Length;)
            {
                short key = il[p++]; if (key == 0xfe) key = (short)(0xfe00 | il[p++]); var op = codes[key]; object value = null; int n;
                switch (op.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.InlineField: value = method.Module.ResolveField(BitConverter.ToInt32(il, p), ta, ma); p += 4; break;
                    case OperandType.InlineMethod: value = method.Module.ResolveMethod(BitConverter.ToInt32(il, p), ta, ma); p += 4; break;
                    case OperandType.InlineType: value = method.Module.ResolveType(BitConverter.ToInt32(il, p), ta, ma); p += 4; break;
                    case OperandType.InlineString: value = method.Module.ResolveString(BitConverter.ToInt32(il, p)); p += 4; break;
                    case OperandType.InlineBrTarget: case OperandType.InlineI: value = BitConverter.ToInt32(il, p); p += 4; break;
                    case OperandType.InlineI8: value = BitConverter.ToInt64(il, p); p += 8; break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: value = (sbyte)il[p++]; break;
                    case OperandType.ShortInlineVar: value = il[p++]; break;
                    case OperandType.InlineVar: value = BitConverter.ToUInt16(il, p); p += 2; break;
                    case OperandType.InlineSwitch: n = BitConverter.ToInt32(il, p); p += 4 + 4 * n; break;
                    default: throw new InvalidOperationException("Unexpected native-matched IL: " + op);
                }
                yield return (op, value);
            }
        }
    }
}
