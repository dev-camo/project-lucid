using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ProjectLucid.CodeGen
{
    // HLSplines.Runtime original getters 06000285/287/28a/28c/28f/291.
    // Roslyn emits IsReadOnly before CompilerGenerated for these six auto getters.
    // Restore only their CustomAttribute row order; PE bodies, tokens, heaps, MVID
    // and portable PDB remain byte-identical. No declared-scope alias is applied.
    internal static class RibbonAccessorAttributes
    {
        private const string Core = "netstandard, Version=2.1.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51";
        private const string Engine = "UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
        private const string Generated = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";
        private const string ReadOnly = "System.Runtime.CompilerServices.IsReadOnlyAttribute";
        private static void Require(bool value, string description) { if (!value) throw new InvalidDataException(description); }
        private static void Type(TypeReference type, string name)
        {
            Require(type != null && type.FullName == name && !(type is TypeSpecification), "Changed accessor type.");
            Require(type.Scope is AssemblyNameReference scope && scope.FullName == (name == "UnityEngine.Vector3" ? Engine : Core), "Changed accessor declared scope.");
        }
        private static void Marker(CustomAttribute attribute, string name)
        {
            Require(attribute.AttributeType.FullName == name && attribute.AttributeType.Scope is AssemblyNameReference scope && scope.FullName == Core,
                "Changed accessor marker identity/scope.");
            Require(attribute.Constructor.FullName == "System.Void " + name + "::.ctor()" && attribute.Constructor.HasThis && attribute.Constructor.Parameters.Count == 0 && !attribute.Constructor.ExplicitThis && attribute.Constructor.CallingConvention == MethodCallingConvention.Default,
                "Changed accessor marker constructor.");
            Type(attribute.Constructor.ReturnType, "System.Void");
            Require(attribute.GetBlob().SequenceEqual(new byte[] { 1, 0, 0, 0 }), "Changed accessor marker blob.");
        }
        private static void Body(MethodDefinition method, string[] opcodes, object[] operands)
        {
            Require(method.HasBody && method.ImplAttributes == 0 && method.GenericParameters.Count == 0 && method.Overrides.Count == 0 && !method.Body.HasVariables && method.Body.ExceptionHandlers.Count == 0,
                "Changed accessor method body contract.");
            var instructions = method.Body.Instructions.Where(i => i.OpCode != OpCodes.Nop).ToArray();
            Require(instructions.Length == opcodes.Length, "Changed accessor instruction count.");
            for (int i = 0; i < instructions.Length; ++i)
                Require(instructions[i].OpCode.Name == opcodes[i] && (operands[i] == null ? instructions[i].Operand == null : ReferenceEquals(instructions[i].Operand, operands[i])),
                    "Changed accessor instruction/member linkage.");
        }
        private static List<MethodDefinition> Validate(ModuleDefinition module, out bool restored)
        {
            Require(module.Assembly.Name.FullName == "HLSplines.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", "Wrong ribbon assembly identity.");
            var methods = new List<MethodDefinition>(); bool? state = null;
            foreach (string name in new[] { "KnotT", "LinearRatio", "PositionAndTangent" })
            {
                var type = module.Types.SingleOrDefault(t => t.FullName == "Hardlight." + name);
                Require(type != null && (int)type.Attributes == 1048841 && !type.HasGenericParameters && !type.HasNestedTypes && type.Interfaces.Count == 0 && type.CustomAttributes.Count == 0 && type.Events.Count == 0,
                    "Changed ribbon accessor type graph.");
                Type(type.BaseType, "System.ValueType");
                Require(!type.HasLayoutInfo, "Changed explicit ribbon layout record.");
                string[] properties = name == "PositionAndTangent" ? new[] { "Position", "Tangent" } : new[] { "KnotIndex", "T" };
                string[] types = name == "PositionAndTangent" ? new[] { "UnityEngine.Vector3", "UnityEngine.Vector3" } : new[] { "System.Int32", "System.Single" };
                string[] parameters = name == "PositionAndTangent" ? new[] { "position", "tangent" } : new[] { "knotIndex", "t" };
                Require(type.Fields.Count == 2 && type.Methods.Count == 5 && type.Properties.Count == 2, "Changed complete ribbon accessor population.");
                foreach (var method in type.Methods)
                    Require(!method.ExplicitThis && method.CallingConvention == MethodCallingConvention.Default && method.MethodReturnType.Attributes == 0 && !method.MethodReturnType.HasConstant && !method.MethodReturnType.HasMarshalInfo && method.MethodReturnType.CustomAttributes.Count == 0,
                        "Changed ribbon calling convention/return metadata.");
                var constructor = type.Methods[0];
                Require(constructor.Name == ".ctor" && (int)constructor.Attributes == 6278 && constructor.HasThis && constructor.CustomAttributes.Count == 0 && constructor.Parameters.Count == 2,
                    "Changed ribbon constructor declaration.");
                Type(constructor.ReturnType, "System.Void");
                for (int i = 0; i < 2; ++i)
                {
                    var field = type.Fields[i]; var property = type.Properties[i]; var getter = type.Methods[1 + i * 2]; var setter = type.Methods[2 + i * 2];
                    Require(field.Name == "<" + properties[i] + ">k__BackingField" && (int)field.Attributes == 1 && !field.HasConstant && !field.HasMarshalInfo && !field.HasLayoutInfo && field.RVA == 0 && (field.InitialValue == null || field.InitialValue.Length == 0) && field.CustomAttributes.Count == 1,
                        "Changed ribbon backing field.");
                    Type(field.FieldType, types[i]); Marker(field.CustomAttributes[0], Generated);
                    Require(property.Name == properties[i] && property.Attributes == 0 && property.Parameters.Count == 0 && property.CustomAttributes.Count == 0 && property.OtherMethods.Count == 0 && property.GetMethod == getter && property.SetMethod == setter,
                        "Changed ribbon property linkage/order.");
                    Type(property.PropertyType, types[i]);
                    Require(getter.Name == "get_" + properties[i] && setter.Name == "set_" + properties[i] && (int)getter.Attributes == 2182 && (int)setter.Attributes == 2182 && getter.HasThis && setter.HasThis && getter.Parameters.Count == 0 && setter.Parameters.Count == 1,
                        "Changed ribbon accessor flags/signatures.");
                    Type(getter.ReturnType, types[i]); Type(setter.ReturnType, "System.Void");
                    var argument = setter.Parameters[0];
                    Require(argument.Name == "value" && argument.Attributes == 0 && !argument.HasConstant && argument.CustomAttributes.Count == 0 && !argument.HasMarshalInfo,
                        "Changed ribbon setter parameter.");
                    Type(argument.ParameterType, types[i]);
                    var parameter = constructor.Parameters[i];
                    Require(parameter.Name == parameters[i] && parameter.Attributes == 0 && !parameter.HasConstant && parameter.CustomAttributes.Count == 0 && !parameter.HasMarshalInfo,
                        "Changed ribbon constructor parameter.");
                    Type(parameter.ParameterType, types[i]);
                    Require(getter.CustomAttributes.Count == 2 && setter.CustomAttributes.Count == 1, "Changed ribbon accessor attribute count.");
                    bool originalOrder = getter.CustomAttributes[0].AttributeType.FullName == Generated;
                    Marker(getter.CustomAttributes[originalOrder ? 0 : 1], Generated);
                    Marker(getter.CustomAttributes[originalOrder ? 1 : 0], ReadOnly);
                    Marker(setter.CustomAttributes[0], Generated);
                    Require(!state.HasValue || state.Value == originalOrder, "Partially restored ribbon attribute order."); state = originalOrder;
                    Body(getter, new[] { "ldarg.0", "ldfld", "ret" }, new object[] { null, field, null });
                    Body(setter, new[] { "ldarg.0", "ldarg.1", "stfld", "ret" }, new object[] { null, null, field, null });
                    methods.Add(getter);
                }
                Body(constructor, new[] { "ldarg.0", "ldarg.1", "call", "ldarg.0", "ldarg.2", "call", "ret" }, new object[] { null, null, type.Methods[2], null, null, type.Methods[4], null });
            }
            Require(methods.Count == 6 && state.HasValue, "Expected exactly six original ribbon getters."); restored = state.Value; return methods;
        }
        internal static byte[] Restore(byte[] original)
        {
            Require(original != null, "No ribbon PE image.");
            using (var stream = new MemoryStream(original, false))
            using (var module = ModuleDefinition.ReadModule(stream, new ReaderParameters { ReadingMode = ReadingMode.Deferred, AssemblyResolver = new RejectResolver() }))
            {
                bool restored; var methods = Validate(module, out restored); var copy = (byte[])original.Clone();
                var table = new AttributeTable(copy);
                foreach (var method in methods) table.Swap(method.MetadataToken.RID, !restored);
                using (var afterStream = new MemoryStream(copy, false))
                using (var after = ModuleDefinition.ReadModule(afterStream, new ReaderParameters { ReadingMode = ReadingMode.Deferred, AssemblyResolver = new RejectResolver() }))
                { bool final; Validate(after, out final); Require(final, "Restored ribbon attribute order did not round-trip."); }
                return copy;
            }
        }
        private sealed class RejectResolver : IAssemblyResolver
        {
            public AssemblyDefinition Resolve(AssemblyNameReference name) => throw new InvalidDataException("Unexpected ribbon dependency resolution.");
            public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) => Resolve(name);
            public void Dispose() { }
        }
        // CustomAttribute table 0x0c: swap only the two rows of the same MethodDef.
        private sealed class AttributeTable
        {
            private readonly byte[] image; private readonly int start, stride, parentWidth; private readonly uint count;
            internal AttributeTable(byte[] image)
            {
                this.image = image; Require(U16(0) == 0x5a4d, "Missing DOS header.");
                int pe = checked((int)U32(0x3c)); Require(U32(pe) == 0x00004550, "Missing PE header.");
                int sections = U16(pe + 6), optional = pe + 24, sectionTable = checked(optional + U16(pe + 20)); ushort magic = U16(optional);
                Require(magic == 0x10b || magic == 0x20b, "Unsupported PE magic."); int directories = optional + (magic == 0x10b ? 96 : 112);
                Func<uint, int> rva = address => {
                    for (int i = 0; i < sections; ++i) { int row = checked(sectionTable + i * 40); uint va = U32(row + 12), raw = U32(row + 16), ptr = U32(row + 20);
                        if (address >= va && (ulong)address - va < raw) return checked((int)(ptr + address - va)); }
                    throw new InvalidDataException("RVA outside file-backed sections."); };
                int cli = rva(U32(directories + 14 * 8)), metadata = rva(U32(cli + 8)); Require(U32(metadata) == 0x424a5342, "Missing CLR metadata.");
                int versionEnd = checked(metadata + 16 + (int)U32(metadata + 12)), header = checked((versionEnd + 3) & ~3), streams = U16(header + 2), cursor = header + 4, tables = -1;
                for (int i = 0; i < streams; ++i) { uint offset = U32(cursor); int name = cursor + 8, end = name; while (Byte(end) != 0) ++end;
                    string label = System.Text.Encoding.ASCII.GetString(image, name, end - name); if (label == "#~" || label == "#-") { Require(tables < 0, "Duplicate table stream."); tables = checked(metadata + (int)offset); }
                    cursor = checked((end + 4) & ~3); }
                Require(tables >= 0, "Missing table stream."); byte heaps = Byte(tables + 6); ulong valid = U64(tables + 8); cursor = tables + 24; var rows = new uint[64];
                for (int i = 0; i < 64; ++i) if ((valid & (1UL << i)) != 0) { rows[i] = U32(cursor); cursor += 4; }
                Func<int, int> index = id => rows[id] >= 65536 ? 4 : 2;
                Func<int, int[], int> coded = (bits, ids) => ids.Max(id => rows[id]) >= (1U << (16 - bits)) ? 4 : 2;
                int str = (heaps & 1) == 0 ? 2 : 4, guid = (heaps & 2) == 0 ? 2 : 4, blob = (heaps & 4) == 0 ? 2 : 4;
                int defRef = coded(2, new[] { 2, 1, 27 });
                int[] widths = { 2 + str + guid * 3, coded(2,new[] {0,26,35,1}) + str * 2, 4 + str * 2 + defRef + index(4) + index(6), index(4), 2 + str + blob, index(6), 8 + str + blob + index(8), index(8), 4 + str, index(2) + defRef, coded(3,new[] {2,1,26,6,27}) + str + blob, 2 + coded(2,new[] {4,8,23}) + blob };
                for (int i = 0; i < 12; ++i) cursor = checked(cursor + (int)rows[i] * widths[i]);
                start = cursor; count = rows[12]; parentWidth = coded(5,new[] {6,4,1,2,8,9,10,0,14,23,20,17,26,27,32,35,38,39,40,42,44,43});
                stride = parentWidth + coded(3,new[] {6,10}) + blob; Bounds(start, checked((int)count * stride));
            }
            internal void Swap(uint methodRid, bool change)
            {
                Require(methodRid > 0 && methodRid <= uint.MaxValue >> 5, "Invalid getter RID."); uint parent = methodRid << 5;
                var offsets = new List<int>(); for (int i = 0; i < count; ++i) { int offset = checked(start + i * stride); if ((parentWidth == 2 ? U16(offset) : U32(offset)) == parent) offsets.Add(offset); }
                Require(offsets.Count == 2 && offsets[1] == offsets[0] + stride, "Getter must have exactly two consecutive attribute rows.");
                if (!change) return;
                for (int j = parentWidth; j < stride; ++j) { byte old = image[offsets[0] + j]; image[offsets[0] + j] = image[offsets[1] + j]; image[offsets[1] + j] = old; }
            }
            private void Bounds(int offset, int size) { Require(offset >= 0 && size >= 0 && (long)offset + size <= image.Length, "PE read outside image."); }
            private byte Byte(int offset) { Bounds(offset, 1); return image[offset]; }
            private ushort U16(int offset) { Bounds(offset, 2); return (ushort)(image[offset] | image[offset + 1] << 8); }
            private uint U32(int offset) { Bounds(offset, 4); return (uint)(image[offset] | image[offset + 1] << 8 | image[offset + 2] << 16 | image[offset + 3] << 24); }
            private ulong U64(int offset) => U32(offset) | ((ulong)U32(offset + 4) << 32);
        }
    }
}
