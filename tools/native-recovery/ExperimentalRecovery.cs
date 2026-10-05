using Assembly = System.Reflection.Assembly;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Builder;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.Builder;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Cpp2IL.Core;
using Cpp2IL.Core.Extensions;
using Cpp2IL.Core.ISIL;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Utils;

namespace ProjectLucid.NativeRecovery;

// Original HLUnityCore.Runtime:Hardlight.TimeUtils:0x06001006 is a call-free,
// signed Int64 threshold comparison in both shipped Mach-O architectures.
// This bounded emission experiment never adds generated code to the project.
internal static class ExperimentalRecovery
{
    private const string Symbol = "_TimeUtils_HasTimePassedThresholdSinceLastCheck_mA1AF9ACB8472CCE7736F80B11BB3ECBA9344A729";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    internal static void ValidateSelection(MethodAnalysisContext method)
    {
        if (method.DeclaringType?.DeclaringAssembly.DefaultName != "HLUnityCore.Runtime" ||
            method.DeclaringType.DefaultFullName != "Hardlight.TimeUtils" || method.Token != 0x06001006 ||
            method.DefaultName != "HasTimePassedThresholdSinceLastCheck" || !method.IsStatic ||
            method.ReturnType.FullName != "System.Boolean" || method.Parameters.Count != 3 ||
            method.Parameters.Any(p => p.ParameterType.FullName != "System.Int64"))
            throw new ArgumentException("Experimental recovery supports only the original static TimeUtils threshold method (HLUnityCore.Runtime:0x06001006).");
    }

    internal static void Recover(MethodAnalysisContext method, string binary, string output, Dictionary<string, object?> report)
    {
        report["recovery_stages"] = new { declaration_selected = true, il_emitted = false, executable = false, behavior_verified = false };
        report["counts"] = new { declarations = 1, il_emitted = 0, executable = 0, bounded_behavior_verified = 0, maintained_implementations = 0 };
        uint cpu;
        using (var reader = new BinaryReader(File.OpenRead(binary))) { reader.ReadUInt32(); cpu = reader.ReadUInt32(); }
        string expectedHash = cpu switch
        {
            0x01000007 => "58275e261cf2c9bde993c15a82dc3e41d0e0fc9b7d9dbe498303e575a5e6fbcb",
            0x0100000c => "3411df1222f5da945c4fdbdfd68dd6959e16ec98a6392cf761e1eab456d7e510",
            _ => throw new InvalidDataException("Unsupported experimental architecture.")
        };
        string nativeHash = Hash(method.RawBytes.ToArray());
        if (nativeHash != expectedHash)
            throw new InvalidDataException("Selected native body differs from the independently corroborated threshold profile.");
        var native = VerifySymbol(binary, method.UnderlyingPointer, method.RawBytes.ToArray());
        report["native_symbol"] = native;
        report["native_addresses_verified"] = true;
        report["verification_scope"] = "One native threshold body; signed Int64 boundary cases and deterministic samples. No camera, save, startup, gameplay or whole-game claim.";
        report["execution_runtime"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        report["original_native_body_executed"] = false;
        report["behavior_reference"] = new
        {
            contract = "previousCheck < threshold && threshold <= currentTime (signed Int64)",
            evidence = "Exact named x86_64 cmp/setl/setle/and and ARM64 cmp/ccmp/cset body profiles.",
            boundary_cases = 343, deterministic_samples = 10000, random_seed = 12345,
            native_execution_claim = false, whole_game_verification = false
        };
        report["managed_semantics_recovered"] = false;
        report["generated_runtime_installation"] = false;

        var raw = method.AppContext.InstructionSet.GetIsilFromMethod(method);
        if (raw.Any(i => i.OpCode is OpCode.Invalid or OpCode.NotImplemented))
            throw new InvalidDataException("Raw graph contains invalid or unsupported instructions.");
        // Serial analysis of this exact 16/32-byte body avoids the whole-assembly
        // parallel emission route. It does not correct the upstream analyzer.
        _ = method.AppContext.GetOrCreateKeyFunctionAddresses();
        report["analysis_pipeline_invoked"] = true;
        method.Analyze();
        var graph = method.ControlFlowGraph ?? throw new InvalidDataException("Analysis produced no graph.");
        var instructions = graph.Instructions.ToArray();
        GuardAcyclic(graph.EntryBlock, new HashSet<Cpp2IL.Core.Graphs.Block>(), new HashSet<Cpp2IL.Core.Graphs.Block>());
        GuardGraph(instructions);
        File.WriteAllLines(Path.Combine(output, "analyzed-isil.txt"), instructions.Select(i => i.ToString()));
        var variants = new List<Dictionary<string, object?>>();
        report["variants"] = variants;
        variants.Add(EmitAndCheck(method, output, "baseline", false));
        variants.Add(EmitAndCheck(method, output, "maxstack-only", true));

        int integers = PropagateInt64Subtractions(instructions, method.AppContext.SystemTypes.SystemInt64Type);
        int booleans = PropagateBooleans(instructions, method.AppContext.SystemTypes.SystemBooleanType);
        var used = instructions.SelectMany(i => i.Operands).OfType<LocalVariable>().Distinct().ToArray();
        if (used.Any(l => l.Type?.FullName is not ("System.Boolean" or "System.Int64")))
            throw new InvalidDataException("Used local has unknown or unsupported primitive type after bounded propagation.");
        var zero = new LocalVariable("int64ComparisonZero", new Register(null, "int64ComparisonZero"), method.AppContext.SystemTypes.SystemInt64Type);
        int zeros = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.OpCode is not (OpCode.CheckEqual or OpCode.CheckLess or OpCode.CheckNotEqual)) continue;
            if (!instruction.Operands.Skip(1).Any(o => o is LocalVariable { Type.FullName: "System.Int64" })) continue;
            for (int i = 1; i < instruction.Operands.Count; i++)
                if (instruction.Operands[i] is Immediate { Value: 0 }) { instruction.SetOperand(i, zero); zeros++; }
        }
        if (zeros > 0) method.Locals.Add(zero);
        GuardTypedGraph(instructions);
        report["emission_corrections"] = new
        {
            computed_maxstack = true, boolean_temporaries = booleans, int64_subtraction_temporaries = integers,
            int64_comparison_zero_operands = zeros, initialized_typed_zero = true,
            upstream_source_modified = false,
            boolean_rule = "Not with Boolean source; And/Or/Xor with two Boolean sources. Existing conflicting types are rejected.",
            int64_rule = "Subtract with two known Int64 sources; zero comparison operand normalized to initialized Int64 local."
        };
        File.WriteAllLines(Path.Combine(output, "corrected-isil.txt"), instructions.Select(i => i.ToString()));
        variants.Add(EmitAndCheck(method, output, "corrected", true));
        report["variants"] = variants;
        var corrected = variants[^1];
        bool passed = (string?)corrected["execution_status"] == "passed";
        report["recovery_stages"] = new { declaration_selected = true, il_emitted = true, executable = corrected["executable"], behavior_verified = passed };
        report["counts"] = new { declarations = 1, il_emitted = 1, executable = (bool)corrected["executable"]! ? 1 : 0, bounded_behavior_verified = passed ? 1 : 0, maintained_implementations = 0 };
        // The report never represents an automatically promoted implementation.
        if (!passed) throw new InvalidDataException("Corrected artifact did not pass its bounded threshold execution proof.");
    }

    private static void GuardGraph(Instruction[] instructions)
    {
        if (instructions.Length is 0 or > 100) throw new InvalidDataException("Experimental graph exceeds its instruction bound.");
        foreach (var instruction in instructions)
        {
            if (instruction.OpCode is not (OpCode.Subtract or OpCode.Xor or OpCode.And or OpCode.Or or OpCode.Not or
                OpCode.CheckLess or OpCode.CheckEqual or OpCode.CheckNotEqual or OpCode.Move or OpCode.ConditionalJump or OpCode.Jump or OpCode.Return))
                throw new InvalidDataException("Unsupported experimental graph opcode: " + instruction.OpCode);
            foreach (var operand in instruction.Operands)
                if (operand is not (LocalVariable or Immediate or Cpp2IL.Core.Graphs.Block))
                    throw new InvalidDataException("Unsupported experimental graph operand: " + operand.GetType().Name);
            int expected = instruction.OpCode switch
            {
                OpCode.Return or OpCode.Jump => 1,
                OpCode.Not or OpCode.Move or OpCode.ConditionalJump => 2,
                _ => 3
            };
            if (instruction.Operands.Count != expected || instruction.Operands.OfType<Immediate>().Any(i => i.Value != 0))
                throw new InvalidDataException("Unsupported experimental instruction shape or constant.");
            if (instruction.OpCode is OpCode.Jump or OpCode.ConditionalJump)
            {
                if (instruction.Operands[0] is not Cpp2IL.Core.Graphs.Block)
                    throw new InvalidDataException("Branch target is not an analyzed block.");
            }
            else if (instruction.Operands[0] is not LocalVariable || instruction.Operands.Any(o => o is Cpp2IL.Core.Graphs.Block))
                throw new InvalidDataException("Experimental instruction requires primitive local operands.");
        }
    }

    private static void GuardAcyclic(Cpp2IL.Core.Graphs.Block block, HashSet<Cpp2IL.Core.Graphs.Block> active, HashSet<Cpp2IL.Core.Graphs.Block> done)
    {
        if (done.Contains(block)) return;
        if (done.Count + active.Count > 100 || !active.Add(block)) throw new InvalidDataException("Experimental graph contains a loop or exceeds its block bound.");
        foreach (var successor in block.Successors) GuardAcyclic(successor, active, done);
        active.Remove(block); done.Add(block);
    }

    private static void GuardTypedGraph(Instruction[] instructions)
    {
        static string? Type(IOperand operand) => (operand as LocalVariable)?.Type?.FullName;
        foreach (var instruction in instructions)
        {
            var operands = instruction.Operands;
            bool valid = instruction.OpCode switch
            {
                OpCode.Jump => true,
                OpCode.Return => Type(operands[0]) == "System.Boolean",
                OpCode.ConditionalJump => Type(operands[1]) == "System.Boolean",
                OpCode.Move => Type(operands[0]) == "System.Boolean" && operands[1] is Immediate { Value: 0 },
                OpCode.Not => Type(operands[0]) == "System.Boolean" && Type(operands[1]) == "System.Boolean",
                OpCode.Subtract => operands.All(o => Type(o) == "System.Int64"),
                OpCode.And or OpCode.Or or OpCode.Xor => Type(operands[0]) is "System.Int64" or "System.Boolean" &&
                    Type(operands[0]) == Type(operands[1]) && Type(operands[1]) == Type(operands[2]),
                OpCode.CheckLess => Type(operands[0]) == "System.Boolean" && Type(operands[1]) == "System.Int64" && Type(operands[2]) == "System.Int64",
                OpCode.CheckEqual or OpCode.CheckNotEqual => Type(operands[0]) == "System.Boolean" &&
                    Type(operands[1]) is "System.Int64" or "System.Boolean" && Type(operands[1]) == Type(operands[2]),
                _ => false
            };
            if (!valid) throw new InvalidDataException("Unsupported or conflicting typed instruction: " + instruction.OpCode);
        }
    }

    private static void SetInferredType(LocalVariable destination, TypeAnalysisContext type)
    {
        if (destination.Type is not null && destination.Type.FullName != type.FullName)
            throw new InvalidDataException("Conflicting inferred primitive type: " + destination.Name);
        destination.Type = type;
    }

    private static int PropagateInt64Subtractions(Instruction[] instructions, TypeAnalysisContext int64)
    {
        int assigned = 0;
        foreach (var instruction in instructions)
            if (instruction.OpCode == OpCode.Subtract && instruction.Operands.Count == 3 &&
                instruction.Operands[0] is LocalVariable destination &&
                instruction.Operands[1] is LocalVariable { Type.FullName: "System.Int64" } &&
                instruction.Operands[2] is LocalVariable { Type.FullName: "System.Int64" })
            {
                if (destination.Type is null) assigned++;
                SetInferredType(destination, int64);
            }
        return assigned;
    }

    private static int PropagateBooleans(Instruction[] instructions, TypeAnalysisContext boolean)
    {
        int assigned = 0;
        bool changed;
        do
        {
            changed = false;
            foreach (var instruction in instructions)
            {
                bool justified = instruction.OpCode == OpCode.Not && instruction.Operands.Count == 2 &&
                    instruction.Operands[1] is LocalVariable { Type.FullName: "System.Boolean" };
                justified |= instruction.OpCode is OpCode.And or OpCode.Or or OpCode.Xor && instruction.Operands.Count == 3 &&
                    instruction.Operands[1] is LocalVariable { Type.FullName: "System.Boolean" } &&
                    instruction.Operands[2] is LocalVariable { Type.FullName: "System.Boolean" };
                if (!justified || instruction.Operands[0] is not LocalVariable destination) continue;
                if (destination.Type is null) { assigned++; changed = true; }
                SetInferredType(destination, boolean);
            }
        } while (changed);
        return assigned;
    }

    private static Dictionary<string, object?> EmitAndCheck(MethodAnalysisContext context, string output, string variant, bool computeMaxstack)
    {
        string directory = Path.Combine(output, variant);
        Directory.CreateDirectory(directory);
        // Provide only the primitive identities used by the isolated emitter.
        var corlib = new AssemblyDefinition("mscorlib", new Version(4, 0, 0, 0));
        var corlibModule = new ModuleDefinition("mscorlib.dll"); corlib.Modules.Add(corlibModule);
        foreach (var type in context.Locals.Select(l => l.Type).Where(t => t is not null).Distinct())
        {
            var definition = new TypeDefinition(type!.Namespace, type.Name, TypeAttributes.Public);
            corlibModule.TopLevelTypes.Add(definition); type.PutExtraData("AsmResolverType", definition);
        }
        string name = "ProjectLucid.ExperimentalThreshold." + variant;
        var assembly = new AssemblyDefinition(name, new Version(1, 0, 0, 0));
        var module = new ModuleDefinition(name + ".dll", new AssemblyReference("mscorlib", new Version(4, 0, 0, 0)));
        assembly.Modules.Add(module);
        var owner = new TypeDefinition("Hardlight", "TimeUtils", TypeAttributes.Public) { BaseType = module.CorLibTypeFactory.Object.Type };
        module.TopLevelTypes.Add(owner);
        var method = new MethodDefinition(context.DefaultName, MethodAttributes.Public | MethodAttributes.Static,
            MethodSignature.CreateStatic(module.CorLibTypeFactory.Boolean,
                [module.CorLibTypeFactory.Int64, module.CorLibTypeFactory.Int64, module.CorLibTypeFactory.Int64]));
        owner.Methods.Add(method);
        for (int i = 0; i < 3; i++) method.ParameterDefinitions.Add(new ParameterDefinition((ushort)(i + 1), context.Parameters[i].Name, 0));
        IlGenerator.GenerateIl(context, method);
        var body = method.CilMethodBody ?? throw new InvalidDataException("Emitter produced no body.");
        body.ComputeMaxStackOnBuild = computeMaxstack;
        body.InitializeLocals = true;
        foreach (var local in body.LocalVariables)
            if (local.VariableType.FullName == "System.Int64") local.VariableType = module.CorLibTypeFactory.Int64;
            else if (local.VariableType.FullName == "System.Boolean") local.VariableType = module.CorLibTypeFactory.Boolean;
        File.WriteAllLines(Path.Combine(directory, "il.txt"), body.Instructions.Select(i => i.ToString()));
        File.WriteAllLines(Path.Combine(directory, "locals.txt"), body.LocalVariables.Select(i => i.VariableType.FullName));
        string file = Path.Combine(directory, name + ".dll");
        new ManagedPEFileBuilder().CreateFile(module.ToPEImage(new ManagedPEImageBuilder())).Write(file);
        var receipt = new Dictionary<string, object?>
        {
            ["variant"] = variant, ["assembly"] = file, ["assembly_sha256"] = Hash(File.ReadAllBytes(file)),
            ["il_emitted"] = true, ["executable"] = false, ["execution_status"] = "failed", ["checks"] = 0,
            ["computed_maxstack"] = computeMaxstack, ["runtime"] = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
        };
        int checks = 0;
        try
        {
            var loaded = Assembly.LoadFile(file).GetType("Hardlight.TimeUtils")!.GetMethod(context.DefaultName)!;
            receipt["max_stack"] = loaded.GetMethodBody()!.MaxStackSize;
            receipt["initialized_locals"] = loaded.GetMethodBody()!.InitLocals;
            if (!loaded.GetMethodBody()!.InitLocals) throw new InvalidDataException("Emitted method does not initialize its typed zero local.");
            var function = (Func<long, long, long, bool>)loaded.CreateDelegate(typeof(Func<long, long, long, bool>));
            void Check(long previous, long threshold, long current)
            {
                bool actual = function(previous, threshold, current);
                receipt["executable"] = true;
                bool expected = previous < threshold && threshold <= current;
                if (actual != expected)
                {
                    receipt["counterexample"] = new { previous, threshold, current, actual, expected };
                    throw new InvalidDataException("Threshold result differs from the independent native signed-comparison contract.");
                }
                checks++;
            }
            long[] values = [long.MinValue, long.MinValue + 1, -1, 0, 1, long.MaxValue - 1, long.MaxValue];
            foreach (long previous in values) foreach (long threshold in values) foreach (long current in values) Check(previous, threshold, current);
            var random = new Random(12345); byte[] bytes = new byte[24];
            for (int i = 0; i < 10000; i++)
            {
                random.NextBytes(bytes);
                Check(BitConverter.ToInt64(bytes, 0), BitConverter.ToInt64(bytes, 8), BitConverter.ToInt64(bytes, 16));
            }
            receipt["execution_status"] = "passed";
        }
        catch (Exception error) { receipt["error"] = error.Message; receipt["exception_type"] = error.GetType().FullName; }
        receipt["checks"] = checks;
        File.WriteAllText(Path.Combine(directory, "execution.json"), JsonSerializer.Serialize(receipt, Json) + "\n");
        return receipt;
    }

    private static object VerifySymbol(string binary, ulong address, byte[] body)
    {
        using var file = File.OpenRead(binary); using var reader = new BinaryReader(file);
        file.Position = 16; uint commands = reader.ReadUInt32(), commandBytes = reader.ReadUInt32();
        if (commands > 1000 || 32L + commandBytes > file.Length) throw new InvalidDataException("Malformed Mach-O command bounds.");
        uint symbols = 0, symbolCount = 0, strings = 0, stringSize = 0;
        var segments = new List<(ulong Address, ulong Size, ulong Offset, ulong FileSize)>();
        long command = 32;
        for (int i = 0; i < commands; i++)
        {
            file.Position = command; uint kind = reader.ReadUInt32(), size = reader.ReadUInt32();
            if (size < 8 || command + size > 32L + commandBytes) throw new InvalidDataException("Malformed Mach-O load command.");
            if (kind == 2)
            {
                if (size < 24) throw new InvalidDataException("Truncated Mach-O symbol command.");
                symbols = reader.ReadUInt32(); symbolCount = reader.ReadUInt32(); strings = reader.ReadUInt32(); stringSize = reader.ReadUInt32();
            }
            if (kind == 0x19)
            {
                if (size < 72) throw new InvalidDataException("Truncated Mach-O segment command.");
                file.Position = command + 24;
                segments.Add((reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64()));
            }
            command += size;
        }
        if (symbolCount > 2000000 || symbols + symbolCount * 16UL > (ulong)file.Length || strings + (ulong)stringSize > (ulong)file.Length || stringSize > 256 * 1024 * 1024)
            throw new InvalidDataException("Malformed or absent Mach-O symbol table.");
        file.Position = strings; byte[] names = reader.ReadBytes((int)stringSize);
        var matches = new List<ulong>();
        for (uint i = 0; i < symbolCount; i++)
        {
            file.Position = symbols + i * 16L; uint index = reader.ReadUInt32(); byte kind = reader.ReadByte();
            reader.ReadByte(); reader.ReadUInt16(); ulong value = reader.ReadUInt64();
            if ((kind & 0xe0) != 0 || (kind & 0x0e) != 0x0e || index >= names.Length) continue;
            int end = Array.IndexOf(names, (byte)0, (int)index);
            if (end < 0) throw new InvalidDataException("Unterminated Mach-O symbol name.");
            if (Encoding.UTF8.GetString(names, (int)index, end - (int)index) == Symbol) matches.Add(value);
        }
        if (matches.Count != 1 || matches[0] != address) throw new InvalidDataException("Cpp2IL method pointer does not match the exact original Mach-O symbol.");
        var mapped = segments.Where(s => address >= s.Address && address - s.Address <= s.FileSize && (ulong)body.Length <= s.FileSize - (address - s.Address)).ToArray();
        if (mapped.Length != 1) throw new InvalidDataException("Native body does not map to one file-backed Mach-O segment.");
        ulong offset = mapped[0].Offset + address - mapped[0].Address;
        if (offset + (ulong)body.Length > (ulong)file.Length) throw new InvalidDataException("Native body exceeds file bounds.");
        file.Position = (long)offset;
        if (!reader.ReadBytes(body.Length).SequenceEqual(body)) throw new InvalidDataException("Cpp2IL native bytes differ from the symbol-mapped file bytes.");
        return new { name = Symbol, address = "0x" + address.ToString("x"), file_offset = offset, native_sha256 = Hash(body), bytes = body.Length };
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
