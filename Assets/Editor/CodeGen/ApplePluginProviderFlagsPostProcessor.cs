using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace ProjectLucid.CodeGen
{
    // Original Hardlight.SourceApplePluginControllerProvider (HLAppleInput.Runtime 0x02000016)
    // emits get_KeySource (0x0600003f) and get_AxisSource (0x06000040) as Final|Virtual|NewSlot
    // (0x09e6, slots 7 and 8) because they implement members of IBaseSourceProvider<...>.
    // That interface is genuinely empty in the shipped metadata, so no member is invented for
    // the C# compiler to implement. Restore only those two 16-bit MethodDef flag cells.
    // No PE or symbol re-serialization; sibling of InputMonitorMethodFlagsPostProcessor.
    public sealed class ApplePluginProviderFlagsPostProcessor : ILPostProcessor
    {
        public override ILPostProcessor GetInstance() => new ApplePluginProviderFlagsPostProcessor();
        public override bool WillProcess(ICompiledAssembly assembly) => assembly.Name == "HLAppleInput.Runtime";
        public override ILPostProcessResult Process(ICompiledAssembly assembly)
        {
            if (!WillProcess(assembly)) return null;
            try
            {
                byte[] restored = ApplePluginProviderFlags.Restore(assembly.InMemoryAssembly.PeData);
                return new ILPostProcessResult(new InMemoryAssembly(restored, assembly.InMemoryAssembly.PdbData));
            }
            catch (Exception error) when (error is InvalidDataException || error is IOException || error is InvalidOperationException || error is BadImageFormatException || error is OverflowException || error is ArgumentException || error is IndexOutOfRangeException || error is AssemblyResolutionException)
            {
                // Never return partially patched bytes; the Error diagnostic stops compilation.
                return new ILPostProcessResult(assembly.InMemoryAssembly, new List<DiagnosticMessage> {
                    new DiagnosticMessage { DiagnosticType = DiagnosticType.Error, MessageData = "Apple provider metadata restoration refused: " + error.Message }
                });
            }
        }
    }

    internal static class ApplePluginProviderFlags
    {
        private const ushort AddedFlags = 0x0160; // Final | Virtual | NewSlot
        private const string Owner = "Hardlight.SourceApplePluginControllerProvider";
        private const string LocalAssembly = "HLAppleInput.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
        private const string InputAssembly = "HLInput.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
        private const string CoreAssembly = "HLUnityCore.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
        private const string Il2CppCtor = "System.Void Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute::.ctor(Unity.IL2CPP.CompilerServices.Option,System.Object)";
        private const string Key = "Hardlight.IApplePluginControllerInputKeySource", Axis = "Hardlight.IApplePluginControllerInputAxisSource", Name = "Apple.GameController.Controller.GCControllerInputName";

        // Original type attributes in metadata order: ArrayBoundsChecks (2) then NullChecks (1), both false.
        private static readonly string[] TypeAttributes = {
            "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|" + Il2CppCtor + "|01000200000002000000",
            "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute|" + Il2CppCtor + "|01000100000002000000",
        };
        private static readonly string[] Interfaces = {
            "Hardlight.ISourceApplePluginControllerProvider",
            "Hardlight.IBaseSourceProvider`4<" + Key + "," + Name + "," + Axis + "," + Name + ">",
        };
        private const string FieldContract = "m_sourceApplePluginController|1|Hardlight.SourceApplePluginController";
        // name|compiled flags|return type|parameter. The two targets are the first two entries; the
        // original flags of the targets are the compiled flags plus AddedFlags (2182 -> 2534).
        private static readonly string[] Methods = {
            "get_KeySource|2182|" + Key + "|",
            "get_AxisSource|2182|" + Axis + "|",
            "get_GlyphSource|2182|Hardlight.IApplePluginControllerGlyphSource|",
            "Initialise|198|System.Void|baseControllerProvider@Hardlight.IBaseControllerProvider",
            "Shutdown|198|System.Void|",
            "Update|198|System.Void|",
            ".ctor|6278|System.Void|",
        };
        // property|type|getter index
        private static readonly string[] Properties = {
            "KeySource|" + Key + "|0",
            "AxisSource|" + Axis + "|1",
            "GlyphSource|Hardlight.IApplePluginControllerGlyphSource|2",
        };

        internal static byte[] Restore(byte[] original)
        {
            Require(original != null, "No PE image.");
            using (var input = new MemoryStream(original, false))
            using (var module = ModuleDefinition.ReadModule(input, new ReaderParameters { ReadingMode = ReadingMode.Deferred }))
            {
                Require(module.Assembly != null && module.Assembly.Name.FullName == LocalAssembly && !module.Assembly.Name.HasPublicKey && module.Name == "HLAppleInput.Runtime.dll", "Unexpected assembly identity/signing.");
                var matches = AllTypes(module.Types).Where(t => t.FullName == Owner).ToArray();
                Require(matches.Length == 1 && matches[0].DeclaringType == null, "Missing/duplicate/nested provider owner.");
                var type = matches[0];
                Require((int)type.Attributes == 1048833 && !type.HasGenericParameters && !type.HasNestedTypes && !type.HasSecurityDeclarations && type.PackingSize == -1 && type.ClassSize == -1 && type.Events.Count == 0, "Changed provider type flags/shape.");
                Require(type.BaseType != null && type.BaseType.FullName == "Hardlight.BaseSourceProvider" && Scope(type.BaseType) == InputAssembly, "Changed provider base.");
                Require(type.CustomAttributes.Count == TypeAttributes.Length, "Changed provider attribute count.");
                for (int i = 0; i < TypeAttributes.Length; ++i)
                {
                    var attribute = type.CustomAttributes[i];
                    Require(Scope(attribute.AttributeType) == CoreAssembly && attribute.AttributeType.FullName + "|" + attribute.Constructor.FullName + "|" + BitConverter.ToString(attribute.GetBlob()).Replace("-", "") == TypeAttributes[i], "Changed provider attribute order/payload.");
                }
                Require(type.Interfaces.Count == Interfaces.Length, "Changed provider interface count.");
                for (int i = 0; i < Interfaces.Length; ++i)
                {
                    var entry = type.Interfaces[i];
                    Require(entry.CustomAttributes.Count == 0 && entry.InterfaceType.FullName == Interfaces[i], "Changed provider interface identity/order.");
                    // The generic marker lives in HLInput.Runtime and stays empty; the local one is untouched.
                    Require(Scope(entry.InterfaceType) == (i == 0 ? LocalAssembly : InputAssembly), "Changed provider interface scope.");
                }
                Require(type.Fields.Count == 1, "Changed provider field count.");
                var field = type.Fields[0];
                Require(field.Name + "|" + (int)field.Attributes + "|" + field.FieldType.FullName == FieldContract && Scope(field.FieldType) == LocalAssembly && !field.HasConstant && !field.HasMarshalInfo && field.Offset == -1 && field.InitialValue.Length == 0 && field.CustomAttributes.Count == 0, "Changed provider field.");
                Require(type.Methods.Count == Methods.Length && type.Properties.Count == Properties.Length, "Changed provider member inventory.");

                var getters = new HashSet<MethodDefinition>();
                for (int i = 0; i < Properties.Length; ++i)
                {
                    string[] key = Properties[i].Split('|'); var property = type.Properties[i]; var getter = type.Methods[int.Parse(key[2])];
                    Require(property.Name == key[0] && property.PropertyType.FullName == key[1] && Scope(property.PropertyType) == LocalAssembly && property.Attributes == 0 && !property.HasConstant && property.CustomAttributes.Count == 0 && property.Parameters.Count == 0 && property.HasThis && property.GetMethod == getter && property.SetMethod == null && property.OtherMethods.Count == 0 && getter.Name == "get_" + key[0], "Changed provider property.");
                    getters.Add(getter);
                }
                bool? restored = null; var targets = new List<MethodDefinition>();
                for (int i = 0; i < Methods.Length; ++i)
                {
                    string[] key = Methods[i].Split('|'); var method = type.Methods[i]; ushort compiled = ushort.Parse(key[1]), actual = (ushort)method.Attributes;
                    if (i < 2)
                    {
                        bool done = actual == (compiled | AddedFlags);
                        Require(actual == compiled || done, "Unexpected target MethodDef flags.");
                        Require(!restored.HasValue || restored.Value == done, "Partially restored target flags.");
                        restored = done; targets.Add(method);
                    }
                    else Require(actual == compiled, "Changed provider method flags " + method.Name + ".");
                    Require(method.Name == key[0] && method.ReturnType.FullName == key[2] && method.Parameters.Count == (key[3].Length == 0 ? 0 : 1) && method.ImplAttributes == 0 && method.HasBody && method.HasThis && !method.ExplicitThis && method.CallingConvention == MethodCallingConvention.Default && !method.HasGenericParameters && method.Overrides.Count == 0 && !method.HasSecurityDeclarations && method.CustomAttributes.Count == 0, "Changed provider method shape " + method.Name + ".");
                    Require(method.SemanticsAttributes == (getters.Contains(method) ? MethodSemanticsAttributes.Getter : 0), "Changed provider method/property semantics.");
                    Require(method.MethodReturnType.Attributes == 0 && !method.MethodReturnType.HasConstant && !method.MethodReturnType.HasMarshalInfo && method.MethodReturnType.CustomAttributes.Count == 0, "Changed provider return metadata.");
                    if (key[2] != "System.Void") Require(Scope(method.ReturnType) == LocalAssembly, "Changed provider return scope.");
                    foreach (var parameter in method.Parameters)
                        Require(parameter.Name + "@" + parameter.ParameterType.FullName == key[3] && Scope(parameter.ParameterType) == InputAssembly && parameter.Attributes == 0 && !parameter.HasConstant && !parameter.HasMarshalInfo && parameter.CustomAttributes.Count == 0, "Changed provider parameter metadata.");
                }
                Require(targets.Count == 2 && targets[0].Name == "get_KeySource" && targets[1].Name == "get_AxisSource", "Expected exact two target methods.");
                // Targets must still be the original plain field read (the bodies are never touched).
                foreach (var target in targets)
                {
                    var instructions = target.Body.Instructions;
                    Require(!target.Body.InitLocals && target.Body.Variables.Count == 0 && target.Body.ExceptionHandlers.Count == 0 && instructions.Count == 3
                        && instructions[0].OpCode.Code == Code.Ldarg_0 && instructions[1].OpCode.Code == Code.Ldfld && ReferenceEquals(instructions[1].Operand, field) && instructions[2].OpCode.Code == Code.Ret, "Changed target getter body.");
                }

                var copy = (byte[])original.Clone(); var table = new MethodTable(copy);
                int[] offsets = targets.Select(target => table.FlagsOffset(target.MetadataToken.RID)).ToArray();
                Require(offsets.Distinct().Count() == 2, "Duplicate target MethodDef cell.");
                foreach (var target in targets)
                {
                    Require(target.MetadataToken.TokenType == TokenType.Method, "Target is not a MethodDef.");
                    int offset = table.FlagsOffset(target.MetadataToken.RID);
                    ushort old = table.U16(offset);
                    Require(old == (ushort)target.Attributes, "PE/Cecil MethodDef disagreement.");
                    table.SetU16(offset, (ushort)(old | AddedFlags));
                }
                int changes = 0;
                for (int i = 0; i < copy.Length; ++i)
                    if (copy[i] != original[i])
                    {
                        Require(offsets.Any(offset => i == offset || i == offset + 1), "Write outside the two target cells.");
                        ++changes;
                    }
                Require(changes == (restored.Value ? 0 : 4), "Unexpected two-target byte delta.");
                return copy;
            }
        }

        private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> roots)
        {
            foreach (var t in roots) { yield return t; foreach (var n in AllTypes(t.NestedTypes)) yield return n; }
        }
        // Full name of the assembly that declares the type (generic arguments are covered by the FullName checks).
        private static string Scope(TypeReference type)
        {
            while (type is TypeSpecification specification) type = specification.ElementType;
            if (type.Scope is ModuleDefinition module) return module.Assembly.Name.FullName;
            var scope = type.Scope as AssemblyNameReference;
            Require(scope != null, "Unsupported type scope.");
            return scope.FullName;
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }

        // Locate MethodDef flag cells without reserializing the PE or PDB.
        // Every read is bounds checked. The only writes are two 16-bit flags.
        private sealed class MethodTable
        {
            private readonly byte[] image;
            private readonly int start, stride;
            private readonly uint count;
            internal MethodTable(byte[] image)
            {
                this.image = image;
                Require(U16(0) == 0x5a4d, "Missing DOS header.");
                int pe = checked((int)U32(0x3c));
                Require(U32(pe) == 0x00004550, "Missing PE header.");
                int sections = U16(pe + 6), optional = pe + 24, sectionTable = checked(optional + U16(pe + 20));
                ushort magic = U16(optional);
                Require(magic == 0x10b || magic == 0x20b, "Unsupported PE magic.");
                int directories = optional + (magic == 0x10b ? 96 : 112);
                Func<uint, int> rva = address => {
                    for (int i = 0; i < sections; ++i) {
                        int row = checked(sectionTable + i * 40); uint va = U32(row + 12), raw = U32(row + 16), ptr = U32(row + 20);
                        if (address >= va && (ulong)address - va < raw) return checked((int)(ptr + address - va));
                    }
                    throw new InvalidDataException("RVA outside file-backed sections.");
                };
                int cli = rva(U32(directories + 14 * 8)); int metadata = rva(U32(cli + 8));
                Require(U32(metadata) == 0x424a5342, "Missing CLR metadata.");
                int versionEnd = checked(metadata + 16 + (int)U32(metadata + 12));
                int header = checked((versionEnd + 3) & ~3); int streams = U16(header + 2), cursor = header + 4, tables = -1;
                for (int i = 0; i < streams; ++i) {
                    uint offset = U32(cursor); int name = cursor + 8, end = name;
                    while (Byte(end) != 0) ++end;
                    string label = System.Text.Encoding.ASCII.GetString(image, name, end - name);
                    if (label == "#~" || label == "#-") { Require(tables < 0, "Duplicate table stream."); tables = checked(metadata + (int)offset); }
                    cursor = checked((end + 4) & ~3);
                }
                Require(tables >= 0, "Missing table stream.");
                byte heaps = Byte(tables + 6); ulong valid = U64(tables + 8); cursor = tables + 24;
                var rows = new uint[64];
                for (int i = 0; i < 64; ++i) if ((valid & (1UL << i)) != 0) { rows[i] = U32(cursor); cursor += 4; }
                Func<int, int> index = id => rows[id] >= 65536 ? 4 : 2;
                Func<int, int[], int> coded = (bits, ids) => ids.Max(id => rows[id]) >= (1U << (16 - bits)) ? 4 : 2;
                int str = (heaps & 1) == 0 ? 2 : 4, guid = (heaps & 2) == 0 ? 2 : 4, blob = (heaps & 4) == 0 ? 2 : 4;
                int[] widths = { 2 + str + guid * 3, coded(2, new[] { 0, 26, 35, 1 }) + str * 2, 4 + str * 2 + coded(2, new[] { 2, 1, 27 }) + index(4) + index(6), index(4), 2 + str + blob, index(6) };
                for (int i = 0; i < 6; ++i) cursor = checked(cursor + (int)rows[i] * widths[i]);
                start = cursor; count = rows[6]; stride = 8 + str + blob + index(8);
                Bounds(start, checked((int)count * stride));
            }
            internal int FlagsOffset(uint rid) { Require(rid > 0 && rid <= count, "MethodDef RID outside table."); return checked(start + ((int)rid - 1) * stride + 6); }
            private void Bounds(int offset, int size) { Require(offset >= 0 && size >= 0 && (long)offset + size <= image.Length, "PE read outside image."); }
            private byte Byte(int offset) { Bounds(offset, 1); return image[offset]; }
            internal ushort U16(int offset) { Bounds(offset, 2); return (ushort)(image[offset] | image[offset + 1] << 8); }
            private uint U32(int offset) { Bounds(offset, 4); return (uint)(image[offset] | image[offset + 1] << 8 | image[offset + 2] << 16 | image[offset + 3] << 24); }
            private ulong U64(int offset) => U32(offset) | ((ulong)U32(offset + 4) << 32);
            internal void SetU16(int offset, ushort value) { Bounds(offset, 2); image[offset] = (byte)value; image[offset + 1] = (byte)(value >> 8); }
        }
    }
}
