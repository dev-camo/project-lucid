using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using ProjectLucid.CodeGen;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace ProjectLucid
{
    // Exercise the actual complete compiler input,
    // never on a rewritten substitute for the positive preservation test.
    internal static class InputMonitorPostProcessorVerification
    {
        private sealed class Compilation : ICompiledAssembly
        {
            public InMemoryAssembly InMemoryAssembly { get; set; }
            public string Name { get; set; }
            public string[] References { get; set; }
            public string[] Defines => new[] { "UNITY_2022_3_54", "UNITY_EDITOR" };
        }
        private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> roots)
        {
            foreach (var t in roots) { yield return t; foreach (var n in AllTypes(t.NestedTypes)) yield return n; }
        }
        private static TypeDefinition Owner(ModuleDefinition module) => AllTypes(module.Types).Single(t => t.FullName == "Hardlight.InputMonitor");
        private static TypeDefinition Provider(ModuleDefinition module) => AllTypes(module.Types).Single(t => t.FullName == "Hardlight.SourceProvider");
        private static readonly string[] AddedTargetKeys = new string[] { "Hardlight.BaseActiveBindings`2|get_CurrentInputBindings", "Hardlight.BaseActiveBindings`2|add_OnCurrentInputBindingsUpdate", "Hardlight.BaseActiveBindings`2|remove_OnCurrentInputBindingsUpdate", "Hardlight.BaseActiveBindings`2|get_CurrentBaseInputBindings", "Hardlight.BaseActiveBindings`2|add_OnCurrentBaseInputBindingsUpdate", "Hardlight.BaseActiveBindings`2|remove_OnCurrentBaseInputBindingsUpdate", "Hardlight.BaseInputAxis`2|add_AxisStartHandlers", "Hardlight.BaseInputAxis`2|remove_AxisStartHandlers", "Hardlight.BaseInputAxis`2|add_AxisHandlers", "Hardlight.BaseInputAxis`2|remove_AxisHandlers", "Hardlight.BaseInputAxis`2|add_AxisEndHandlers", "Hardlight.BaseInputAxis`2|remove_AxisEndHandlers", "Hardlight.BaseInputBinding`1|get_BindingIdentifiers", "Hardlight.BaseInputBinding`1|get_Priority", "Hardlight.BaseInputBinding`1|get_AllowRemapping", "Hardlight.BaseInputBinding`1|get_ForcedBinding", "Hardlight.BaseInputBinding`1|get_FallbackInputType" };
        private static TypeDefinition ExtraOwner(ModuleDefinition module, string name) => AllTypes(module.Types).Single(t => t.FullName == name);
        private static MethodDefinition AddedTarget(ModuleDefinition module, string key)
        {
            string[] parts = key.Split('|');
            return ExtraOwner(module, parts[0]).Methods.Single(t => t.Name == parts[1]);
        }
        private static MethodDefinition[] Targets(ModuleDefinition module) => new[] { Owner(module).Methods[1], Owner(module).Methods[8], Provider(module).Methods[0], Provider(module).Methods[1], Provider(module).Methods[2], Provider(module).Methods[4] }.Concat(AddedTargetKeys.Select(key => AddedTarget(module, key))).OrderBy(m => m.MetadataToken.RID).ToArray();
        private sealed class ExactReferences : IAssemblyResolver
        {
            private readonly Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly Dictionary<string, AssemblyDefinition> opened = new Dictionary<string, AssemblyDefinition>(StringComparer.Ordinal);
            internal ExactReferences(IEnumerable<string> references)
            {
                foreach (string path in references)
                    using (var m = ModuleDefinition.ReadModule(path))
                    {
                        string name = m.Assembly.Name.FullName;
                        if (paths.ContainsKey(name)) throw new InvalidDataException("Duplicate literal fixture reference identity: " + name);
                        paths.Add(name, Path.GetFullPath(path));
                    }
            }
            public AssemblyDefinition Resolve(AssemblyNameReference name) => Resolve(name, new ReaderParameters());
            public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
            {
                if (!paths.ContainsKey(name.FullName)) throw new AssemblyResolutionException(name);
                if (!opened.ContainsKey(name.FullName)) opened.Add(name.FullName, AssemblyDefinition.ReadAssembly(paths[name.FullName], new ReaderParameters { AssemblyResolver = this, ReadingMode = ReadingMode.Deferred }));
                return opened[name.FullName];
            }
            public void Dispose() { foreach (var a in opened.Values) a.Dispose(); opened.Clear(); }
        }
        internal static int Run(string rawPath, string output, string[] references)
        {
            int checks = 0;
            Action<bool, string> check = (value, label) => { if (!value) throw new InvalidOperationException(label); ++checks; };
            byte[] original = File.ReadAllBytes(rawPath), unchanged = (byte[])original.Clone();
            byte[] pdb = File.ReadAllBytes(Path.ChangeExtension(rawPath, ".pdb"));
            var processor = new InputMonitorMethodFlagsPostProcessor();
            Func<byte[], string, Compilation> input = (pe, name) => new Compilation { Name = name, References = references, InMemoryAssembly = new InMemoryAssembly(pe, pdb) };
            check(!ReferenceEquals(processor.GetInstance(), processor), "Fresh processor instance");
            var ignored = input(original, "Game.Runtime");
            check(!processor.WillProcess(ignored) && processor.Process(ignored) == null, "Other assembly ignored");
            var result = processor.Process(input(original, "HLInput.Runtime"));
            check(result != null && result.Diagnostics.Count == 0, "Complete actual source contract accepted");
            byte[] patched = result.InMemoryAssembly.PeData;
            check(original.SequenceEqual(unchanged) && !ReferenceEquals(original, patched), "Original PE never mutated");
            check(ReferenceEquals(pdb, result.InMemoryAssembly.PdbData), "PDB object and bytes retained");
            check(patched.Length == original.Length, "Whole PE length retained");
            int[] differences = Enumerable.Range(0, original.Length).Where(i => original[i] != patched[i]).ToArray();
            check(differences.Length == 46 && Enumerable.Range(0, 23).All(i => differences[2 * i + 1] == differences[2 * i] + 1), "Exactly twenty-three sixteen-bit cells changed");
            for (int i = 0; i < 46; i += 2)
            {
                int offset = differences[i];
                ushort before = (ushort)(original[offset] | original[offset + 1] << 8);
                ushort after = (ushort)(patched[offset] | patched[offset + 1] << 8);
                check((before == 2182 || before == 134) && after == (before | 0x160), "Exact original flag-bit delta");
            }
            using (var before = ModuleDefinition.ReadModule(new MemoryStream(original, false)))
            using (var after = ModuleDefinition.ReadModule(new MemoryStream(patched, false)))
            {
                check(before.Mvid == after.Mvid, "MVID retained");
                var aa = AllTypes(before.Types).SelectMany(t => t.Methods).ToArray();
                var bb = AllTypes(after.Types).SelectMany(t => t.Methods).ToArray();
                check(aa.Length == bb.Length, "All recursive MethodDef rows retained");
                var changed = new List<string>();
                for (int i = 0; i < aa.Length; ++i)
                {
                    check(aa[i].MetadataToken == bb[i].MetadataToken && aa[i].RVA == bb[i].RVA && aa[i].FullName == bb[i].FullName && aa[i].ImplAttributes == bb[i].ImplAttributes, "Method identity/RVA/implementation retained " + i);
                    if (aa[i].Attributes != bb[i].Attributes)
                    {
                        changed.Add(aa[i].FullName);
                        check((ushort)bb[i].Attributes == ((ushort)aa[i].Attributes | 0x160), "Only target bits restored");
                    }
                }
                check(changed.SequenceEqual(Targets(before).Select(m => m.FullName)), "Exact twenty-three target identities");
                check(Targets(after).Length == 23 && Targets(after).All(m => (ushort)m.Attributes == (m.Name == "SetPaused" ? 486 : 2534)), "Original exact twenty-three flags restored");
                check((ushort)Provider(before).Methods[3].Attributes == 2534 && Provider(before).Methods[3].Attributes == Provider(after).Methods[3].Attributes, "Already-correct TouchSource flags untouched");
                check((ushort)ExtraOwner(before, "Hardlight.BaseBindingData").Methods.Single(m => m.Name == "get_GameInput").Attributes == 2534 && ExtraOwner(before, "Hardlight.BaseBindingData").Methods.Single(m => m.Name == "get_GameInput").Attributes == ExtraOwner(after, "Hardlight.BaseBindingData").Methods.Single(m => m.Name == "get_GameInput").Attributes, "Already-correct unselected GameInput getter flags untouched");
            }
            // Forty-six flag bytes are the entire PE delta: body headers, CIL, locals,
            // EH, resources, heaps, debug directory and generator rows all retain
            // their physical bytes. No Cecil serialization is used above.
            var again = processor.Process(input(patched, "HLInput.Runtime"));
            check(again != null && again.Diagnostics.Count == 0 && again.InMemoryAssembly.PeData.SequenceEqual(patched) && ReferenceEquals(again.InMemoryAssembly.PdbData, pdb), "Whole-image idempotence");
            Action<string, byte[]> reject = (label, bytes) => {
                var supplied = input(bytes, "HLInput.Runtime");
                var snapshot = (byte[])bytes.Clone(); var refused = processor.Process(supplied);
                check(refused != null && refused.Diagnostics.Count == 1 && refused.Diagnostics[0].DiagnosticType == DiagnosticType.Error, label + " refuses compilation");
                check(ReferenceEquals(refused.InMemoryAssembly, supplied.InMemoryAssembly) && bytes.SequenceEqual(snapshot) && ReferenceEquals(refused.InMemoryAssembly.PdbData, pdb), label + " preserves input/symbols");
            };
            Action<string, Action<ModuleDefinition>, Action<ModuleDefinition>> rejectMutation = (label, change, witness) => {
                // Rewriting is confined to negative inputs. The positive input
                // and production byte-only restoration above never use Write.
                // Construction, serialization, reopening and witness failures
                // propagate; none may count as a production refusal.
                using (var resolver = new ExactReferences(references))
                {
                    byte[] serialized;
                    using (var m = ModuleDefinition.ReadModule(new MemoryStream(original, false), new ReaderParameters { AssemblyResolver = resolver, ReadingMode = ReadingMode.Deferred }))
                    using (var stream = new MemoryStream())
                    {
                        change(m);
                        m.Write(stream);
                        serialized = stream.ToArray();
                    }
                    // This module is read from the finished bytes, not the graph
                    // passed to change. Prove the intended persisted contract
                    // before submitting that exact image to the processor.
                    using (var reopenedStream = new MemoryStream(serialized, false))
                    using (var reopened = ModuleDefinition.ReadModule(reopenedStream, new ReaderParameters { AssemblyResolver = resolver, ReadingMode = ReadingMode.Deferred }))
                        witness(reopened);
                    reject(label, serialized);
                }
            };
            reject("Empty PE", new byte[0]); reject("Truncated PE", original.Take(128).ToArray());
            rejectMutation("Wrong assembly", m => m.Assembly.Name.Name = "Other",
                m => check(m.Assembly.Name.Name == "Other", "Wrong assembly persisted in reopened bytes"));
            rejectMutation("Wrong version", m => m.Assembly.Name.Version = new Version(1,0,0,0),
                m => check(m.Assembly.Name.Version == new Version(1,0,0,0), "Wrong version persisted in reopened bytes"));
            rejectMutation("Missing owner with markers", m => Owner(m).Name = "MissingInputMonitor",
                m => check(AllTypes(m.Types).Count(t => t.FullName == "Hardlight.InputMonitor") == 0 && AllTypes(m.Types).Count(t => t.FullName == "Hardlight.MissingInputMonitor") == 1 && AllTypes(m.Types).Count(t => t.FullName == "Hardlight.IInputMonitor") == 1 && AllTypes(m.Types).Count(t => t.FullName == "Hardlight.IBaseInputMonitor") == 1, "Renamed owner and retained markers persisted in reopened bytes"));
            rejectMutation("Duplicate owner", m => m.Types.Add(new TypeDefinition("Hardlight", "InputMonitor", Mono.Cecil.TypeAttributes.Public)),
                m => check(m.Types.Count(t => t.FullName == "Hardlight.InputMonitor") == 2 && m.Types.Count(t => t.FullName == "Hardlight.InputMonitor" && t.Attributes == Mono.Cecil.TypeAttributes.Public && t.Fields.Count == 0 && t.Methods.Count == 0) == 1, "Added duplicate top-level owner persisted in reopened bytes"));
            rejectMutation("Type flags", m => Owner(m).IsSealed = false,
                m => check((int)Owner(m).Attributes == (1057025 & ~(int)Mono.Cecil.TypeAttributes.Sealed), "Exact cleared Sealed type bit persisted in reopened bytes"));
            rejectMutation("Invented generic", m => Owner(m).GenericParameters.Add(new GenericParameter("T", Owner(m))),
                m => check(Owner(m).GenericParameters.Count == 1 && Owner(m).GenericParameters[0].Name == "T" && Owner(m).GenericParameters[0].Position == 0 && ReferenceEquals(Owner(m).GenericParameters[0].Owner, Owner(m)), "Added owned generic T persisted in reopened bytes"));
            rejectMutation("Extra field", m => Owner(m).Fields.Add(new FieldDefinition("extra", Mono.Cecil.FieldAttributes.Private, m.TypeSystem.Int32)),
                m => check(Owner(m).Fields.Count == 22 && Owner(m).Fields[21].Name == "extra" && Owner(m).Fields[21].Attributes == Mono.Cecil.FieldAttributes.Private && Owner(m).Fields[21].FieldType.FullName == "System.Int32", "Added ordered private Int32 field persisted in reopened bytes"));
            rejectMutation("Field identity", m => Owner(m).Fields[0].Name = "other",
                m => check(Owner(m).Fields.Count == 21 && Owner(m).Fields[0].Name == "other" && Owner(m).Fields[0].FieldType.FullName == "Hardlight.InputTouch" && !Owner(m).Fields.Any(f => f.Name == "m_touch"), "Renamed first InputTouch field persisted in reopened bytes"));
            rejectMutation("Missing method", m => Owner(m).Methods.RemoveAt(17),
                m => check(Owner(m).Methods.Count == 17 && !Owner(m).Methods.Any(method => method.Name == ".ctor"), "Removed original constructor persisted in reopened bytes"));
            rejectMutation("Duplicate target", m => Owner(m).Methods.Add(new MethodDefinition("SetPaused", Mono.Cecil.MethodAttributes.Public, m.TypeSystem.Void)),
                m => check(Owner(m).Methods.Count == 19 && Owner(m).Methods.Count(method => method.Name == "SetPaused") == 2 && Owner(m).Methods[18].Name == "SetPaused" && Owner(m).Methods[18].Parameters.Count == 0 && Owner(m).Methods[18].ReturnType.FullName == "System.Void" && Owner(m).Methods[18].Attributes == Mono.Cecil.MethodAttributes.Public, "Added no-parameter SetPaused duplicate name persisted in reopened bytes"));
            rejectMutation("Target parameter name", m => Owner(m).Methods[8].Parameters[0].Name = "other",
                m => check(Owner(m).Methods[8].Name == "SetPaused" && Owner(m).Methods[8].Parameters.Count == 1 && Owner(m).Methods[8].Parameters[0].Name == "other", "Renamed target parameter persisted in reopened bytes"));
            rejectMutation("Target parameter type", m => Owner(m).Methods[8].Parameters[0].ParameterType = m.TypeSystem.Int32,
                m => check(Owner(m).Methods[8].Name == "SetPaused" && Owner(m).Methods[8].Parameters.Count == 1 && Owner(m).Methods[8].Parameters[0].ParameterType.FullName == "System.Int32" && Owner(m).Methods[8].Parameters[0].ParameterType.IsValueType, "Int32 target parameter persisted in reopened bytes"));
            rejectMutation("Target parameter default", m => Owner(m).Methods[8].Parameters[0].Constant = false,
                m => { var parameter = Owner(m).Methods[8].Parameters.Single(); check(Owner(m).Methods[8].Name == "SetPaused" && parameter.HasConstant && parameter.Constant is bool && !(bool)parameter.Constant, "False target parameter constant persisted in reopened bytes"); });
            rejectMutation("Unexpected target flags", m => Owner(m).Methods[1].IsVirtual = true,
                m => check(Owner(m).Methods[1].Name == "get_SourceProvider" && (ushort)Owner(m).Methods[1].Attributes == (2182 | (ushort)Mono.Cecil.MethodAttributes.Virtual), "Exact unexpected target Virtual bit persisted in reopened bytes"));
            rejectMutation("Partial restoration", m => Owner(m).Methods[1].Attributes |= (Mono.Cecil.MethodAttributes)0x160,
                m => check(Owner(m).Methods[1].Name == "get_SourceProvider" && (ushort)Owner(m).Methods[1].Attributes == 2534 && Owner(m).Methods[8].Name == "SetPaused" && (ushort)Owner(m).Methods[8].Attributes == 134, "Only getter restoration persisted in reopened bytes"));
            rejectMutation("Unrelated flags", m => Owner(m).Methods[0].IsVirtual = true,
                m => check(Owner(m).Methods[0].Name == "get_TouchCount" && (ushort)Owner(m).Methods[0].Attributes == (2182 | (ushort)Mono.Cecil.MethodAttributes.Virtual), "Exact unrelated getter Virtual bit persisted in reopened bytes"));
            rejectMutation("Impl flags", m => Owner(m).Methods[8].ImplAttributes |= MethodImplAttributes.NoInlining,
                m => check(Owner(m).Methods[8].Name == "SetPaused" && Owner(m).Methods[8].ImplAttributes == MethodImplAttributes.NoInlining, "Exact target NoInlining implementation bit persisted in reopened bytes"));
            rejectMutation("Changed getter field", m => Owner(m).Methods[1].Body.Instructions[1].Operand = Owner(m).Fields.Single(f => f.Name == "m_paused"),
                m => { var method = Owner(m).Methods[1]; var field = Owner(m).Fields.Single(f => f.Name == "m_paused"); check(method.Name == "get_SourceProvider" && method.Body.Instructions.Count == 3 && method.Body.Instructions[1].OpCode.Code == Code.Ldfld && ReferenceEquals(method.Body.Instructions[1].Operand, field), "Getter now loads paused field in reopened bytes"); });
            rejectMutation("Changed setter body", m => Owner(m).Methods[8].Body.Instructions.Insert(2, Instruction.Create(OpCodes.Ldc_I4_0)),
                m => { var method = Owner(m).Methods[8]; var body = method.Body.Instructions; check(method.Name == "SetPaused" && body.Count == 5 && body[0].OpCode.Code == Code.Ldarg_0 && body[1].OpCode.Code == Code.Ldarg_1 && body[2].OpCode.Code == Code.Ldc_I4_0 && body[3].OpCode.Code == Code.Stfld && ReferenceEquals(body[3].Operand, Owner(m).Fields.Single(f => f.Name == "m_paused")) && body[4].OpCode.Code == Code.Ret, "Exact extra setter stack instruction persisted in reopened bytes"); });
            rejectMutation("Property link", m => { var property = Owner(m).Properties[1]; var originalGetter = property.GetMethod; if (originalGetter == null) throw new InvalidOperationException("Original property getter missing before mutation"); property.GetMethod = Owner(m).Methods[0]; },
                m => check(Owner(m).Properties[1].Name == "SourceProvider" && Owner(m).Properties[1].PropertyType.FullName == "Hardlight.ISourceProvider" && ReferenceEquals(Owner(m).Properties[1].GetMethod, Owner(m).Methods[0]) && Owner(m).Properties[1].GetMethod.Name == "get_TouchCount", "SourceProvider property now links TouchCount getter in reopened bytes"));
            rejectMutation("Interface order", m => { var t = Owner(m); var old = t.Interfaces[0]; t.Interfaces.RemoveAt(0); t.Interfaces.Add(old); },
                m => check(Owner(m).Interfaces.Select(i => i.InterfaceType.FullName).SequenceEqual(new[] { "Hardlight.IBaseInputMonitor", "Hardlight.IInputMonitor" }), "Exact reversed marker order persisted in reopened bytes"));
            rejectMutation("Invented marker method", m => AllTypes(m.Types).Single(t => t.FullName == "Hardlight.IInputMonitor").Methods.Add(new MethodDefinition("Invented", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Virtual | Mono.Cecil.MethodAttributes.NewSlot | Mono.Cecil.MethodAttributes.Abstract, m.TypeSystem.Void)),
                m => { var marker = AllTypes(m.Types).Single(t => t.FullName == "Hardlight.IInputMonitor"); check(marker.Methods.Count == 1 && marker.Methods[0].Name == "Invented" && marker.Methods[0].ReturnType.FullName == "System.Void" && marker.Methods[0].Parameters.Count == 0 && marker.Methods[0].Attributes == (Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Virtual | Mono.Cecil.MethodAttributes.NewSlot | Mono.Cecil.MethodAttributes.Abstract), "Exact invented abstract marker member persisted in reopened bytes"); });
            rejectMutation("Missing marker inheritance", m => AllTypes(m.Types).Single(t => t.FullName == "Hardlight.IInputMonitor").Interfaces.Clear(),
                m => check(AllTypes(m.Types).Single(t => t.FullName == "Hardlight.IInputMonitor").Interfaces.Count == 0 && Owner(m).Interfaces.Count == 2, "Cleared marker inheritance with concrete markers retained in reopened bytes"));
            rejectMutation("Changed declared scope", m => m.AssemblyReferences.Single(a => a.Name == "netstandard").Version = new Version(9,0,0,0),
                m => check(m.AssemblyReferences.Count(a => a.Name == "netstandard") == 1 && m.AssemblyReferences.Single(a => a.Name == "netstandard").FullName == "netstandard, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51" && ((AssemblyNameReference)Owner(m).Methods[8].Parameters[0].ParameterType.Scope).FullName == "netstandard, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51", "Changed assembly and parameter provider scope persisted in reopened bytes"));
            rejectMutation("Missing IL2CPP attribute", m => Owner(m).CustomAttributes.RemoveAt(0),
                m => check(Owner(m).CustomAttributes.Count == 1 && Owner(m).CustomAttributes[0].AttributeType.FullName == "Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute" && BitConverter.ToString(Owner(m).CustomAttributes[0].GetBlob()).Replace("-", "") == "01000200000002000000", "Removed first IL2CPP attribute and retained second payload in reopened bytes"));
            // Each new negative proves its intended state after serialization.
            // No provider body or original/native service is invoked.
            foreach (int targetIndex in new[] { 0, 1, 2, 4 })
            {
                int selected = targetIndex;
                rejectMutation("Partial provider target " + selected, m => Provider(m).Methods[selected].Attributes |= (Mono.Cecil.MethodAttributes)0x160,
                    m => check((ushort)Provider(m).Methods[selected].Attributes == 2534 && new[] { 0, 1, 2, 4 }.Where(i => i != selected).All(i => (ushort)Provider(m).Methods[i].Attributes == 2182) && (ushort)Owner(m).Methods[1].Attributes == 2182 && (ushort)Owner(m).Methods[8].Attributes == 134, "Only selected provider target persisted in reopened bytes"));
            }
            rejectMutation("Only monitor pair restored", m => { Owner(m).Methods[1].Attributes |= (Mono.Cecil.MethodAttributes)0x160; Owner(m).Methods[8].Attributes |= (Mono.Cecil.MethodAttributes)0x160; },
                m => check((ushort)Owner(m).Methods[1].Attributes == 2534 && (ushort)Owner(m).Methods[8].Attributes == 486 && new[] { 0, 1, 2, 4 }.All(i => (ushort)Provider(m).Methods[i].Attributes == 2182), "Only monitor pair restored in reopened bytes"));
            rejectMutation("Only provider quartet restored", m => { foreach (int i in new[] { 0, 1, 2, 4 }) Provider(m).Methods[i].Attributes |= (Mono.Cecil.MethodAttributes)0x160; },
                m => check((ushort)Owner(m).Methods[1].Attributes == 2182 && (ushort)Owner(m).Methods[8].Attributes == 134 && new[] { 0, 1, 2, 4 }.All(i => (ushort)Provider(m).Methods[i].Attributes == 2534), "Only provider quartet restored in reopened bytes"));
            rejectMutation("Provider owner missing", m => Provider(m).Name = "MissingSourceProvider",
                m => check(!AllTypes(m.Types).Any(t => t.FullName == "Hardlight.SourceProvider") && AllTypes(m.Types).Count(t => t.FullName == "Hardlight.MissingSourceProvider") == 1 && AllTypes(m.Types).Count(t => t.FullName == "Hardlight.ISourceProvider") == 1, "Provider renamed while original interface remains in reopened bytes"));
            rejectMutation("Provider owner duplicate", m => m.Types.Add(new TypeDefinition("Hardlight", "SourceProvider", Mono.Cecil.TypeAttributes.Public)),
                m => check(m.Types.Count(t => t.FullName == "Hardlight.SourceProvider") == 2, "Duplicate provider owner persisted in reopened bytes"));
            rejectMutation("Provider type flags", m => Provider(m).IsSealed = false,
                m => check((int)Provider(m).Attributes == (1048833 & ~(int)Mono.Cecil.TypeAttributes.Sealed), "Exact provider Sealed bit removed in reopened bytes"));
            rejectMutation("Provider field type", m => Provider(m).Fields[0].FieldType = AllTypes(m.Types).Single(t => t.FullName == "Hardlight.SourceMouse"),
                m => check(Provider(m).Fields[0].Name == "m_sourceController" && Provider(m).Fields[0].FieldType.FullName == "Hardlight.SourceMouse", "Controller field now declares genuine mouse provider in reopened bytes"));
            rejectMutation("Provider field name", m => Provider(m).Fields[1].Name = "otherMouse",
                m => check(Provider(m).Fields[1].Name == "otherMouse" && Provider(m).Fields[1].FieldType.FullName == "Hardlight.SourceMouse", "Mouse field rename persisted in reopened bytes"));
            rejectMutation("Provider field order", m => { var first = Provider(m).Fields[0]; Provider(m).Fields.RemoveAt(0); Provider(m).Fields.Add(first); },
                m => check(Provider(m).Fields.Select(f => f.Name).SequenceEqual(new[] { "m_sourceMouse", "m_sourceTouchScreen", "m_sourceController" }), "Exact rotated provider fields persisted in reopened bytes"));
            rejectMutation("Provider interface order", m => { var first = Provider(m).Interfaces[0]; Provider(m).Interfaces.RemoveAt(0); Provider(m).Interfaces.Add(first); },
                m => check(Provider(m).Interfaces.Count == 2 && Provider(m).Interfaces[1].InterfaceType.FullName == "Hardlight.ISourceProvider" && Provider(m).Interfaces[0].InterfaceType.FullName == "Hardlight.IBaseSourceProvider`4<Hardlight.IInputKeySource,UnityEngine.KeyCode,Hardlight.IInputAxisSource,System.String>", "Provider interfaces rotated in reopened bytes"));
            rejectMutation("Provider missing method", m =>
            {
                var owner = Provider(m); var removed = owner.Methods[10];
                if (removed.Name != "CreateTouchSource" || removed.Parameters.Count != 0 || removed.ReturnType.FullName != "System.Void" || !removed.HasThis || removed.ExplicitThis || removed.HasGenericParameters)
                    throw new InvalidOperationException("Unexpected original CreateTouchSource declaration before mutation");
                // Materialize all local IL before detaching the MethodDefinition.
                // The negative keeps a local same-signature MemberRef for the
                // genuine Initialise call, so the absent-MethodDef mutant can serialize.
                var affected = AllTypes(m.Types).SelectMany(type => type.Methods)
                    .Where(method => method.HasBody)
                    .SelectMany(method => method.Body.Instructions.Select(instruction => new { method, instruction }))
                    .Where(pair => ReferenceEquals(pair.instruction.Operand, removed)).ToArray();
                if (affected.Length != 1 || !ReferenceEquals(affected[0].method.DeclaringType, owner) || affected[0].method.Name != "Initialise" || affected[0].instruction.OpCode.Code != Code.Call)
                    throw new InvalidOperationException("Unexpected original CreateTouchSource local caller before mutation");
                foreach (var pair in affected)
                    pair.instruction.Operand = new MethodReference(removed.Name, removed.ReturnType, owner)
                    { HasThis = removed.HasThis, ExplicitThis = removed.ExplicitThis, CallingConvention = removed.CallingConvention };
                owner.Methods.RemoveAt(10);
            },
                m => check(Provider(m).Methods.Count == 11 && !Provider(m).Methods.Any(method => method.Name == "CreateTouchSource"), "Genuine CreateTouchSource removed in reopened bytes"));
            rejectMutation("Provider target flags", m => Provider(m).Methods[0].IsVirtual = true,
                m => check(Provider(m).Methods[0].Name == "get_KeySource" && (ushort)Provider(m).Methods[0].Attributes == (2182 | (ushort)Mono.Cecil.MethodAttributes.Virtual), "Unexpected KeySource Virtual bit persisted in reopened bytes"));
            rejectMutation("Already-correct TouchSource flags", m => Provider(m).Methods[3].IsFinal = false,
                m => check(Provider(m).Methods[3].Name == "get_TouchSource" && (ushort)Provider(m).Methods[3].Attributes == (2534 & ~(ushort)Mono.Cecil.MethodAttributes.Final), "TouchSource Final bit removed in reopened bytes"));
            rejectMutation("Provider target implementation", m => Provider(m).Methods[2].ImplAttributes |= MethodImplAttributes.NoInlining,
                m => check(Provider(m).Methods[2].Name == "get_PointerSource" && Provider(m).Methods[2].ImplAttributes == MethodImplAttributes.NoInlining, "PointerSource NoInlining persisted in reopened bytes"));
            rejectMutation("Provider getter field", m => Provider(m).Methods[0].Body.Instructions[1].Operand = Provider(m).Fields[1],
                m => check(Provider(m).Methods[0].Body.Instructions.Count == 3 && Provider(m).Methods[0].Body.Instructions[1].OpCode.Code == Code.Ldfld && ReferenceEquals(Provider(m).Methods[0].Body.Instructions[1].Operand, Provider(m).Fields[1]), "KeySource loads mouse field in reopened bytes"));
            rejectMutation("Provider getter body", m => Provider(m).Methods[4].Body.Instructions.Insert(1, Instruction.Create(OpCodes.Nop)),
                m => check(Provider(m).Methods[4].Name == "get_GestureSource" && Provider(m).Methods[4].Body.Instructions.Count == 4 && Provider(m).Methods[4].Body.Instructions[1].OpCode.Code == Code.Nop, "GestureSource extra Nop persisted in reopened bytes"));
            rejectMutation("Provider property link", m =>
            {
                var owner = Provider(m);
                // Finish lazy owner accessor and MethodSemantics reads before changing a link.
                foreach (var observedProperty in owner.Properties)
                {
                    _ = observedProperty.GetMethod;
                    _ = observedProperty.SetMethod;
                    _ = observedProperty.OtherMethods.Count;
                }
                foreach (var observedMethod in owner.Methods)
                    _ = observedMethod.SemanticsAttributes;
                var property = owner.Properties[0];
                var originalGetter = property.GetMethod;
                if (originalGetter == null) throw new InvalidOperationException("Original provider getter missing before mutation");
                var donor = owner.Properties[1];
                var replacementGetter = owner.Methods[1];
                if (donor.Name != "AxisSource" || replacementGetter.Name != "get_AxisSource" || !ReferenceEquals(donor.GetMethod, replacementGetter))
                    throw new InvalidOperationException("Unexpected original AxisSource donor getter before mutation");
                donor.GetMethod = null;
                property.GetMethod = replacementGetter;
            },
                m => check(Provider(m).Properties[0].Name == "KeySource" && Provider(m).Properties[0].PropertyType.FullName == "Hardlight.IInputKeySource" && ReferenceEquals(Provider(m).Properties[0].GetMethod, Provider(m).Methods[1]) && Provider(m).Properties[0].GetMethod.Name == "get_AxisSource" && Provider(m).Properties[1].Name == "AxisSource" && Provider(m).Properties[1].GetMethod == null, "KeySource property links AxisSource getter in reopened bytes"));
            rejectMutation("Provider property name", m => Provider(m).Properties[4].Name = "OtherGesture",
                m => check(Provider(m).Properties[4].Name == "OtherGesture" && Provider(m).Properties[4].GetMethod.Name == "get_GestureSource", "Gesture property rename persisted in reopened bytes"));
            rejectMutation("Provider parameter name", m => Provider(m).Methods[5].Parameters[0].Name = "other",
                m => check(Provider(m).Methods[5].Name == "Initialise" && Provider(m).Methods[5].Parameters[0].Name == "other", "Initialise parameter name changed in reopened bytes"));
            rejectMutation("Provider attribute order", m => { var first = Provider(m).CustomAttributes[0]; Provider(m).CustomAttributes.RemoveAt(0); Provider(m).CustomAttributes.Add(first); },
                m => check(Provider(m).CustomAttributes.Count == 2 && BitConverter.ToString(Provider(m).CustomAttributes[0].GetBlob()).Replace("-", "") == "01000100000002000000" && BitConverter.ToString(Provider(m).CustomAttributes[1].GetBlob()).Replace("-", "") == "01000200000002000000", "Exact reversed provider IL2CPP payloads persisted in reopened bytes"));
            rejectMutation("Genuine controller owner flags", m => AllTypes(m.Types).Single(t => t.FullName == "Hardlight.SourceController").IsSealed = true,
                m => check((int)AllTypes(m.Types).Single(t => t.FullName == "Hardlight.SourceController").Attributes == (1048577 | (int)Mono.Cecil.TypeAttributes.Sealed), "Controller Sealed bit added in reopened bytes"));
            rejectMutation("Genuine mouse constant", m => AllTypes(m.Types).Single(t => t.FullName == "Hardlight.SourceMouse").Fields[0].Constant = 3,
                m => { var field = AllTypes(m.Types).Single(t => t.FullName == "Hardlight.SourceMouse").Fields[0]; check(field.Name == "NumberOfMouseButtonTouches" && field.HasConstant && field.Constant is int && (int)field.Constant == 3, "Mouse touch-count constant changed in reopened bytes"); });
            rejectMutation("Genuine Touch declared scope", m => AllTypes(m.Types).Single(t => t.FullName == "Hardlight.SourceMouse").Fields[3].FieldType = new ArrayType(new TypeReference("UnityEngine", "Touch", m, m.AssemblyReferences.Single(a => a.Name == "UnityEngine.CoreModule"), true)),
                m => { var field = AllTypes(m.Types).Single(t => t.FullName == "Hardlight.SourceMouse").Fields[3]; var element = ((ArrayType)field.FieldType).ElementType; check(field.Name == "m_trackedMouseTouches" && element.FullName == "UnityEngine.Touch" && element.Scope.Name == "UnityEngine.CoreModule", "Touch now has incorrect genuine CoreModule scope in reopened bytes"); });
            rejectMutation("Stripped generic constraint", m => AllTypes(m.Types).Single(t => t.FullName == "Hardlight.IBaseSourceProvider`4").GenericParameters[0].Constraints.Clear(),
                m => check(AllTypes(m.Types).Single(t => t.FullName == "Hardlight.IBaseSourceProvider`4").GenericParameters[0].Name == "TKeySource" && AllTypes(m.Types).Single(t => t.FullName == "Hardlight.IBaseSourceProvider`4").GenericParameters[0].Constraints.Count == 0, "Original key-source constraint removed in reopened bytes"));
            rejectMutation("Invented stripped API", m => AllTypes(m.Types).Single(t => t.FullName == "Hardlight.IBaseSourceProvider`4").Methods.Add(new MethodDefinition("Invented", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Virtual | Mono.Cecil.MethodAttributes.NewSlot | Mono.Cecil.MethodAttributes.Abstract, m.TypeSystem.Void)),
                m => { var marker = AllTypes(m.Types).Single(t => t.FullName == "Hardlight.IBaseSourceProvider`4"); check(marker.Methods.Count == 1 && marker.Methods[0].Name == "Invented" && marker.Methods[0].IsAbstract, "Invented stripped-interface member persisted in reopened bytes"); });
            // Keep all fifty-three prior actions/witnesses above byte-identical.
            // Every added action has an explicit witness on the finished bytes.
            foreach (string key in AddedTargetKeys)
            {
                string selectedKey = key;
                rejectMutation("New partial target " + selectedKey,
                    m => AddedTarget(m, selectedKey).Attributes |= (Mono.Cecil.MethodAttributes)0x160,
                    m => check((ushort)AddedTarget(m, selectedKey).Attributes == 2534 && Targets(m).Count(t => (ushort)t.Attributes == (t.Name == "SetPaused" ? 486 : 2534)) == 1, "Exactly selected new target restored in reopened bytes " + selectedKey));
                rejectMutation("New unexpected target bits " + selectedKey,
                    m => AddedTarget(m, selectedKey).IsVirtual = true,
                    m => check((ushort)AddedTarget(m, selectedKey).Attributes == (2182 | (ushort)Mono.Cecil.MethodAttributes.Virtual), "Only selected Virtual bit persisted in reopened bytes " + selectedKey));
                rejectMutation("New target body " + selectedKey,
                    m => {
                        var t = AddedTarget(m, selectedKey);
                        if (t.Name.StartsWith("get_", StringComparison.Ordinal))
                            t.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Nop));
                        else t.Body.Instructions[18].OpCode = OpCodes.Beq_S;
                    },
                    m => {
                        var t = AddedTarget(m, selectedKey);
                        check(t.Name.StartsWith("get_", StringComparison.Ordinal)
                            ? t.Body.Instructions.Count == 4 && t.Body.Instructions[0].OpCode.Code == Code.Nop && t.Body.Instructions[1].OpCode.Code == Code.Ldarg_0
                            : t.Body.Instructions.Count == 20 && t.Body.Instructions[18].OpCode.Code == Code.Beq_S && ReferenceEquals(t.Body.Instructions[18].Operand, t.Body.Instructions[3]), "Exact new target body mutation persisted in reopened bytes " + selectedKey);
                    });
            }
            rejectMutation("Axis complete owner flags", m => ExtraOwner(m, "Hardlight.BaseInputAxis`2").IsSealed = true,
                m => check((int)ExtraOwner(m, "Hardlight.BaseInputAxis`2").Attributes == (1056897 | (int)Mono.Cecil.TypeAttributes.Sealed), "Axis exact added Sealed bit persisted"));
            rejectMutation("Axis event name", m => ExtraOwner(m, "Hardlight.BaseInputAxis`2").Events[0].Name = "OtherAxisStart",
                m => check(ExtraOwner(m, "Hardlight.BaseInputAxis`2").Events[0].Name == "OtherAxisStart" && ExtraOwner(m, "Hardlight.BaseInputAxis`2").Events[0].AddMethod.Name == "add_AxisStartHandlers", "Axis renamed event with original add accessor persisted"));
            rejectMutation("Axis delegate parameter name", m => AddedTarget(m, "Hardlight.BaseInputAxis`2|add_AxisStartHandlers").Parameters[0].Name = "other",
                m => check(AddedTarget(m, "Hardlight.BaseInputAxis`2|add_AxisStartHandlers").Parameters[0].Name == "other", "Axis delegate value parameter rename persisted"));
            rejectMutation("Axis generic constraint", m => ExtraOwner(m, "Hardlight.BaseInputAxis`2").GenericParameters[1].Constraints.Clear(),
                m => check(ExtraOwner(m, "Hardlight.BaseInputAxis`2").GenericParameters[1].Name == "TAxisBinding" && ExtraOwner(m, "Hardlight.BaseInputAxis`2").GenericParameters[1].Constraints.Count == 0, "Axis exact constraint removal persisted"));
            rejectMutation("Axis invented stripped API", m => ExtraOwner(m, "Hardlight.IInputAxis").Methods.Add(new MethodDefinition("Invented", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Abstract | Mono.Cecil.MethodAttributes.Virtual | Mono.Cecil.MethodAttributes.NewSlot, m.TypeSystem.Void)),
                m => check(ExtraOwner(m, "Hardlight.IInputAxis").Methods.Count == 1 && ExtraOwner(m, "Hardlight.IInputAxis").Methods[0].Name == "Invented" && ExtraOwner(m, "Hardlight.IInputAxis").Methods[0].IsAbstract && ExtraOwner(m, "Hardlight.IInputAxis").NestedTypes.Count == 3, "Invented own Axis method and retained genuine nested delegates persisted"));
            rejectMutation("Axis nested delegate owner", m => ExtraOwner(m, "Hardlight.IInputAxis/OnAxisHandler").Name = "OtherAxisHandler",
                m => check(AllTypes(m.Types).Count(t => t.FullName == "Hardlight.IInputAxis/OnAxisHandler") == 0 && AllTypes(m.Types).Count(t => t.FullName == "Hardlight.IInputAxis/OtherAxisHandler") == 1, "Axis nested delegate rename persisted"));
            rejectMutation("CAS retry destination", m => AddedTarget(m, "Hardlight.BaseInputAxis`2|add_AxisStartHandlers").Body.Instructions[18].Operand = AddedTarget(m, "Hardlight.BaseInputAxis`2|add_AxisStartHandlers").Body.Instructions[0],
                m => { var b = AddedTarget(m, "Hardlight.BaseInputAxis`2|add_AxisStartHandlers").Body.Instructions; check(b[18].OpCode.Code == Code.Bne_Un_S && ReferenceEquals(b[18].Operand, b[0]), "CAS wrong exact retry destination persisted"); });
            rejectMutation("CAS specialized delegate", m => ((GenericInstanceMethod)AddedTarget(m, "Hardlight.BaseInputAxis`2|add_AxisStartHandlers").Body.Instructions[14].Operand).GenericArguments[0] = m.TypeSystem.Object,
                m => { var operand = (GenericInstanceMethod)AddedTarget(m, "Hardlight.BaseInputAxis`2|add_AxisStartHandlers").Body.Instructions[14].Operand; check(operand.GenericArguments.Count == 1 && operand.GenericArguments[0].FullName == "System.Object" && operand.GenericArguments[0].Scope.Name == "netstandard", "CAS generic delegate replaced by exact Object specialization in reopened bytes"); });
            rejectMutation("CAS local type", m => AddedTarget(m, "Hardlight.BaseInputAxis`2|add_AxisStartHandlers").Body.Variables[0].VariableType = m.TypeSystem.Object,
                m => check(AddedTarget(m, "Hardlight.BaseInputAxis`2|add_AxisStartHandlers").Body.Variables.Count == 3 && AddedTarget(m, "Hardlight.BaseInputAxis`2|add_AxisStartHandlers").Body.Variables[0].VariableType.FullName == "System.Object", "CAS first local Object persisted"));
            rejectMutation("BaseActive event name", m => ExtraOwner(m, "Hardlight.BaseActiveBindings`2").Events[0].Name = "OtherCurrentInputUpdate",
                m => check(ExtraOwner(m, "Hardlight.BaseActiveBindings`2").Events[0].Name == "OtherCurrentInputUpdate" && ExtraOwner(m, "Hardlight.BaseActiveBindings`2").Events[0].AddMethod.Name == "add_OnCurrentInputBindingsUpdate", "BaseActive exact event rename persisted"));
            rejectMutation("BaseActive generic constraint", m => ExtraOwner(m, "Hardlight.BaseActiveBindings`2").GenericParameters[0].Constraints.Clear(),
                m => check(ExtraOwner(m, "Hardlight.BaseActiveBindings`2").GenericParameters[0].Name == "TInputBindingProvider" && ExtraOwner(m, "Hardlight.BaseActiveBindings`2").GenericParameters[0].Constraints.Count == 0, "BaseActive exact constraint removal persisted"));
            rejectMutation("BaseActive true Core base scope", m => ExtraOwner(m, "Hardlight.BaseActiveBindings`2").BaseType = new TypeReference("Hardlight", "SingleScriptableObject", m, m.AssemblyReferences.Single(a => a.Name == "UnityEngine.CoreModule")),
                m => check(ExtraOwner(m, "Hardlight.BaseActiveBindings`2").BaseType.FullName == "Hardlight.SingleScriptableObject" && ExtraOwner(m, "Hardlight.BaseActiveBindings`2").BaseType.Scope.Name == "UnityEngine.CoreModule", "BaseActive incorrect exact Core provider scope persisted"));
            rejectMutation("BaseActive generated captured field", m => ExtraOwner(m, "Hardlight.BaseActiveBindings`2/<>c__DisplayClass20_0").Fields[0].Name = "identifier",
                m => check(ExtraOwner(m, "Hardlight.BaseActiveBindings`2/<>c__DisplayClass20_0").Fields.Count == 1 && ExtraOwner(m, "Hardlight.BaseActiveBindings`2/<>c__DisplayClass20_0").Fields[0].Name == "identifier", "Captured field regressed to identifier in reopened bytes"));
            rejectMutation("BaseActive cache identity", m => ExtraOwner(m, "Hardlight.BaseActiveBindings`2/<>c").Fields[1].Name = "<>9__21_0",
                m => check(ExtraOwner(m, "Hardlight.BaseActiveBindings`2/<>c").Fields[1].Name == "<>9__21_0" && !ExtraOwner(m, "Hardlight.BaseActiveBindings`2/<>c").Fields.Any(f => f.Name == "<>9__20_0"), "Exact wrong generated cache ordinal persisted"));
            rejectMutation("BaseActive property name", m => ExtraOwner(m, "Hardlight.BaseActiveBindings`2").Properties[0].Name = "OtherCurrentInputBindings",
                m => check(ExtraOwner(m, "Hardlight.BaseActiveBindings`2").Properties[0].Name == "OtherCurrentInputBindings" && ExtraOwner(m, "Hardlight.BaseActiveBindings`2").Properties[0].GetMethod.Name == "get_CurrentInputBindings", "BaseActive property rename with original getter persisted"));
            rejectMutation("Invented CurrentBase stripped API", m => ExtraOwner(m, "Hardlight.ICurrentBaseInputBindingsProvider").Methods.Add(new MethodDefinition("Invented", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Abstract | Mono.Cecil.MethodAttributes.Virtual | Mono.Cecil.MethodAttributes.NewSlot, m.TypeSystem.Void)),
                m => check(ExtraOwner(m, "Hardlight.ICurrentBaseInputBindingsProvider").Methods.Count == 1 && ExtraOwner(m, "Hardlight.ICurrentBaseInputBindingsProvider").Methods[0].Name == "Invented" && ExtraOwner(m, "Hardlight.ICurrentBaseInputBindingsProvider").Methods[0].IsAbstract, "Exact invented CurrentBase API persisted"));
            rejectMutation("Binding complete field order", m => { var fields = ExtraOwner(m, "Hardlight.BaseInputBinding`1").Fields; var first = fields[0]; fields.RemoveAt(0); fields.Insert(1, first); },
                m => check(ExtraOwner(m, "Hardlight.BaseInputBinding`1").Fields.Count == 10 && ExtraOwner(m, "Hardlight.BaseInputBinding`1").Fields[0].Name == "m_allowRemapping" && ExtraOwner(m, "Hardlight.BaseInputBinding`1").Fields[1].Name == "m_forcedBinding", "Binding exact first-two field swap persisted"));
            rejectMutation("Binding original accessor order", m => { var methods = ExtraOwner(m, "Hardlight.BaseInputBinding`1").Methods; var first = methods[0]; methods.RemoveAt(0); methods.Insert(4, first); },
                m => check(ExtraOwner(m, "Hardlight.BaseInputBinding`1").Methods.Take(5).Select(t => t.Name).SequenceEqual(new[] { "get_JoystickIndex", "set_JoystickIndex", "get_Priority", "set_Priority", "get_BindingIdentifiers" }), "Binding wrong complete five-accessor prefix persisted"));
            rejectMutation("Binding generic constraint", m => ExtraOwner(m, "Hardlight.BaseInputBinding`1").GenericParameters[0].Constraints.Clear(),
                m => check(ExtraOwner(m, "Hardlight.BaseInputBinding`1").GenericParameters[0].Name == "TBindingData" && ExtraOwner(m, "Hardlight.BaseInputBinding`1").GenericParameters[0].Constraints.Count == 0, "Binding exact constraint removal persisted"));
            rejectMutation("Binding property name", m => ExtraOwner(m, "Hardlight.BaseInputBinding`1").Properties[0].Name = "OtherBindingIdentifiers",
                m => check(ExtraOwner(m, "Hardlight.BaseInputBinding`1").Properties[0].Name == "OtherBindingIdentifiers" && ExtraOwner(m, "Hardlight.BaseInputBinding`1").Properties[0].GetMethod.Name == "get_BindingIdentifiers", "Binding property rename with original getter persisted"));
            rejectMutation("Binding parameter metadata", m => ExtraOwner(m, "Hardlight.BaseInputBinding`1").Methods.Single(t => t.Name == "Setup").Parameters[0].Name = "other",
                m => check(ExtraOwner(m, "Hardlight.BaseInputBinding`1").Methods.Single(t => t.Name == "Setup").Parameters[0].Name == "other", "Binding Setup exact parameter rename persisted"));
            rejectMutation("Binding field annotations", m => ExtraOwner(m, "Hardlight.BaseInputBinding`1").Fields[0].CustomAttributes.RemoveAt(0),
                m => check(ExtraOwner(m, "Hardlight.BaseInputBinding`1").Fields[0].CustomAttributes.Count == 1 && ExtraOwner(m, "Hardlight.BaseInputBinding`1").Fields[0].CustomAttributes[0].AttributeType.FullName == "UnityEngine.TooltipAttribute", "Binding SerializeField removal and retained Tooltip persisted"));
            rejectMutation("Binding duplicate owner", m => m.Types.Add(new TypeDefinition("Hardlight", "BaseInputBinding`1", Mono.Cecil.TypeAttributes.Public)),
                m => check(AllTypes(m.Types).Count(t => t.FullName == "Hardlight.BaseInputBinding`1") == 2 && m.Types.Count(t => t.FullName == "Hardlight.BaseInputBinding`1" && t.Methods.Count == 0) == 1, "Exact duplicate binding owner persisted"));
            rejectMutation("Untouched GameInput getter flags", m => ExtraOwner(m, "Hardlight.BaseBindingData").Methods.Single(t => t.Name == "get_GameInput").Attributes = (Mono.Cecil.MethodAttributes)2182,
                m => check((ushort)ExtraOwner(m, "Hardlight.BaseBindingData").Methods.Single(t => t.Name == "get_GameInput").Attributes == 2182, "Already-correct unselected GameInput getter flags damaged in reopened bytes"));
            // Separate null-constant regression: witness the finished serialized
            // parameter contract before requiring a normal guarded refusal.
            rejectMutation("Binding Setup null identifier constant",
                m => {
                    var setup = ExtraOwner(m, "Hardlight.BaseInputBinding`1").Methods.Single(t => t.Name == "Setup");
                    var parameter = setup.Parameters[2];
                    if (setup.FullName != "System.Void Hardlight.BaseInputBinding`1::Setup(System.Int32,System.Int32,System.String)" || setup.Parameters.Count != 3 || parameter.Index != 2 || parameter.Name != "identifier" || parameter.ParameterType.FullName != "System.String" || parameter.ParameterType.Scope.Name != "netstandard" || parameter.ParameterType.IsValueType || (int)parameter.Attributes != 0 || parameter.HasConstant || parameter.HasMarshalInfo || parameter.CustomAttributes.Count != 0)
                        throw new InvalidOperationException("Original Setup identifier parameter contract changed before null-constant mutation.");
                    parameter.Attributes = Mono.Cecil.ParameterAttributes.HasDefault;
                    parameter.Constant = null;
                },
                m => {
                    var setup = ExtraOwner(m, "Hardlight.BaseInputBinding`1").Methods.Single(t => t.Name == "Setup");
                    var parameter = setup.Parameters[2];
                    check(setup.FullName == "System.Void Hardlight.BaseInputBinding`1::Setup(System.Int32,System.Int32,System.String)" && setup.Parameters.Count == 3 && parameter.Index == 2 && parameter.Name == "identifier" && parameter.ParameterType.FullName == "System.String" && parameter.ParameterType.Scope.Name == "netstandard" && !parameter.ParameterType.IsValueType && parameter.Attributes == Mono.Cecil.ParameterAttributes.HasDefault && (int)parameter.Attributes == 4096 && parameter.HasConstant && parameter.Constant == null && !parameter.HasMarshalInfo && parameter.CustomAttributes.Count == 0, "Binding Setup exact null identifier Constant and HasDefault contract persisted in reopened bytes");
                });
            File.WriteAllBytes(output, patched); File.WriteAllBytes(Path.ChangeExtension(output, ".pdb"), pdb);
            return checks;
        }
        // Consume a genuine separately compiled maintained package. The root
        // caller supplies its actual compiler-plan source count and records that
        // receipt; no hardcoded historical12/current13/prospective15 label.
        internal static int CheckActualSmallPackage(string path, string[] references, int actualSourceCount)
        {
            if (actualSourceCount <= 0) throw new InvalidOperationException("Missing actual maintained source-count receipt.");
            byte[] pe = File.ReadAllBytes(path), pdb = File.ReadAllBytes(Path.ChangeExtension(path, ".pdb"));
            using (var m = ModuleDefinition.ReadModule(new MemoryStream(pe, false)))
            {
                string[] names = new string[] { "Hardlight.InputMonitor", "Hardlight.IInputMonitor", "Hardlight.IBaseInputMonitor", "Hardlight.SourceProvider", "Hardlight.ISourceProvider", "Hardlight.IBaseSourceProvider`4", "Hardlight.BaseSourceProvider", "Hardlight.SourceMouse", "Hardlight.SourceController", "Hardlight.SourceTouchScreen", "Hardlight.IInputAxisSource", "Hardlight.IInputKeySource", "Hardlight.IControllerProvider`1", "Hardlight.BaseInputAxis`2", "Hardlight.BaseActiveBindings`2", "Hardlight.BaseInputBinding`1", "Hardlight.IInputAxis", "Hardlight.IInputAxis/OnAxisStartHandler", "Hardlight.IInputAxis/OnAxisHandler", "Hardlight.IInputAxis/OnAxisEndHandler", "Hardlight.ICurrentBaseInputBindingsProvider", "Hardlight.IBaseInputBindingProvider", "Hardlight.BaseBindingData", "Hardlight.ActiveGameInputBindings", "Hardlight.BaseActiveBindings`2/<>c", "Hardlight.BaseActiveBindings`2/<>c__DisplayClass20_0" };
                if (AllTypes(m.Types).Any(t => names.Contains(t.FullName, StringComparer.Ordinal)))
                    throw new InvalidOperationException("Expected actual maintained owner-absence graph.");
            }
            var supplied = new Compilation { Name = "HLInput.Runtime", References = references, InMemoryAssembly = new InMemoryAssembly(pe, pdb) };
            var result = new InputMonitorMethodFlagsPostProcessor().Process(supplied);
            if (result == null || result.Diagnostics.Count != 0 || !ReferenceEquals(result.InMemoryAssembly.PeData, pe) || !ReferenceEquals(result.InMemoryAssembly.PdbData, pdb))
                throw new InvalidOperationException("Actual owner-absence package changed.");
            return actualSourceCount;
        }
    }
}
