using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using ProjectLucid.CodeGen;

namespace ProjectLucid
{
    // Synthetic Cecil modules (compilation-pipeline fixtures, not game content) that
    // pin the exact scope of the Debug-only allowances and show that optimised
    // (release) compiles stay as strict as before.
    public static class DebugBackingFieldVerification
    {
        private const string Never = "System.Diagnostics.DebuggerBrowsableAttribute";
        private static AssemblyNameReference core;

        // Facts from the real PDevDbg (Unity Debug player) HLInput.Runtime.dll, dumped with
        // Cecil: assembly DebuggableAttribute blob 01-00-07-01-00-00-00-00 (modes 263);
        // Hardlight.BaseActiveBindings`2::get_CurrentInputBindings (also get_CurrentBaseInputBindings,
        // BaseBindingData::get_VectorisedBindings) is `get { return m_field; }` with
        // CodeSize 12, MaxStackSize 1, InitLocals, one local, instructions
        //   nop ldarg.0 ldfld stloc.0 br.s ldloc.0 ret
        // The release compile of the same getter is `ldarg.0 ldfld ret` (CodeSize 7, maxstack 8).
        // The IL bytes below are that body (ldfld token abridged to a placeholder);
        // the fixture decodes them, so offsets come from the real encoding, not from the helper.
        private static readonly byte[] RealDebugGetter = { 0x00, 0x02, 0x7B, 0x01, 0x00, 0x00, 0x04, 0x0A, 0x2B, 0x00, 0x06, 0x2A };

        private static TypeReference Reference(ModuleDefinition module, string ns, string name, bool value = false)
        {
            return new TypeReference(ns, name, module, core, value);
        }

        private static CustomAttribute Marker(ModuleDefinition module, string ns, string name, byte[] blob)
        {
            var type = Reference(module, ns, name);
            var constructor = new MethodReference(".ctor", module.TypeSystem.Void, type) { HasThis = true };
            return new CustomAttribute(constructor, blob);
        }

        private static CustomAttribute Generated(ModuleDefinition module)
        {
            return Marker(module, "System.Runtime.CompilerServices", "CompilerGeneratedAttribute", new byte[] { 1, 0, 0, 0 });
        }

        private static CustomAttribute Browsable(ModuleDefinition module, byte state, bool intConstructor = false)
        {
            var type = Reference(module, "System.Diagnostics", "DebuggerBrowsableAttribute");
            var constructor = new MethodReference(".ctor", module.TypeSystem.Void, type) { HasThis = true };
            constructor.Parameters.Add(new ParameterDefinition(intConstructor ? module.TypeSystem.Int32 : Reference(module, "System.Diagnostics", "DebuggerBrowsableState", true)));
            return new CustomAttribute(constructor, new byte[] { 1, 0, state, 0, 0, 0, 0, 0 });
        }

        // The DebuggableAttribute as Roslyn encodes it; the variants build the near misses.
        private static ModuleDefinition Module(int? debuggingModes, bool boolConstructor = false, bool namedArgument = false)
        {
            core = new AssemblyNameReference("netstandard", new Version(2, 1, 0, 0));
            var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("Fixture", new Version(0, 0, 0, 0)), "Fixture.dll", ModuleKind.Dll);
            var module = assembly.MainModule;
            module.AssemblyReferences.Add(core);
            if (debuggingModes.HasValue)
            {
                var debuggable = Reference(module, "System.Diagnostics", "DebuggableAttribute");
                var constructor = new MethodReference(".ctor", module.TypeSystem.Void, debuggable) { HasThis = true };
                byte[] blob;
                if (boolConstructor)
                {
                    constructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Boolean));
                    constructor.Parameters.Add(new ParameterDefinition(module.TypeSystem.Boolean));
                    blob = new byte[] { 1, 0, 0, 1, 0, 0 };
                }
                else
                {
                    constructor.Parameters.Add(new ParameterDefinition(new TypeReference("", "DebuggingModes", module, core, true) { DeclaringType = debuggable }));
                    blob = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 };
                    BitConverter.GetBytes(debuggingModes.Value).CopyTo(blob, 2);
                    if (namedArgument) blob = blob.Concat(new byte[] { 1, 0 }).ToArray();
                }
                assembly.CustomAttributes.Add(new CustomAttribute(constructor, blob));
            }
            return module;
        }

        private static FieldDefinition Field(ModuleDefinition module, string name, params CustomAttribute[] attributes)
        {
            var type = new TypeDefinition("Fixture", "Owner" + module.Types.Count, TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
            module.Types.Add(type);
            var field = new FieldDefinition(name, FieldAttributes.Private, module.TypeSystem.Int32);
            foreach (var attribute in attributes) field.CustomAttributes.Add(attribute);
            type.Fields.Add(field);
            return field;
        }

        // Decodes the recorded real Debug getter into a method; `mutate` makes one near miss.
        private static MethodDefinition DebugGetter(ModuleDefinition module, Action<MethodDefinition, FieldDefinition> mutate = null)
        {
            var type = new TypeDefinition("Fixture", "Holder" + module.Types.Count, TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
            module.Types.Add(type);
            var field = new FieldDefinition("m_value", FieldAttributes.Private, module.TypeSystem.Int32);
            type.Fields.Add(field);
            var method = new MethodDefinition("get_Value", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, module.TypeSystem.Int32);
            type.Methods.Add(method);
            var body = method.Body; var il = body.GetILProcessor();
            var starts = new System.Collections.Generic.Dictionary<int, Instruction>();
            Instruction branch = null; int branchEnd = 0;
            for (int at = 0; at < RealDebugGetter.Length;)
            {
                int start = at; Instruction instruction;
                switch (RealDebugGetter[at++])
                {
                    case 0x00: instruction = Instruction.Create(OpCodes.Nop); break;
                    case 0x02: instruction = Instruction.Create(OpCodes.Ldarg_0); break;
                    case 0x7B: instruction = Instruction.Create(OpCodes.Ldfld, field); at += 4; break;
                    case 0x0A: instruction = Instruction.Create(OpCodes.Stloc_0); break;
                    case 0x2B: instruction = Instruction.Create(OpCodes.Br_S, Instruction.Create(OpCodes.Nop)); branch = instruction; at += 1; branchEnd = at; break;
                    case 0x06: instruction = Instruction.Create(OpCodes.Ldloc_0); break;
                    case 0x2A: instruction = Instruction.Create(OpCodes.Ret); break;
                    default: throw new InvalidOperationException("Unexpected byte in the recorded Debug getter.");
                }
                instruction.Offset = start; starts[start] = instruction; il.Append(instruction);
            }
            branch.Operand = starts[branchEnd + (sbyte)RealDebugGetter[branchEnd - 1]];
            body.Variables.Add(new VariableDefinition(module.TypeSystem.Int32));
            body.InitLocals = true;
            body.MaxStackSize = 1;
            if (mutate != null) mutate(method, field);
            return method;
        }

        // Swap one opcode in place, keeping the recorded offset so only the opcode differs.
        private static void Replace(MethodDefinition method, int index, OpCode code)
        {
            var instruction = Instruction.Create(code); instruction.Offset = method.Body.Instructions[index].Offset;
            method.Body.Instructions[index] = instruction;
        }

        private static bool Accepts(ModuleDefinition module, Action<MethodDefinition, FieldDefinition> mutate = null)
        {
            return DebugAccessorBody.IsReturnField(DebugGetter(module, mutate), module);
        }

        public static int Run()
        {
            int checks = 0;
            Action<bool, string> check = (value, description) => {
                if (!value) throw new InvalidOperationException(description);
                ++checks;
            };
            const string backing = "<Position>k__BackingField";

            var debug = Module(263);
            var release = Module(2);
            var plain = Module(null);
            check(DebugBackingFieldAttribute.IsDebugCompile(debug), "The exact Unity Debug player modes must count as Debug.");
            check(!DebugBackingFieldAttribute.IsDebugCompile(release), "An optimised compile must not count as Debug.");
            check(!DebugBackingFieldAttribute.IsDebugCompile(plain), "A compile without DebuggableAttribute must not count as Debug.");
            check(!DebugBackingFieldAttribute.IsDebugCompile(null), "A missing module must not count as Debug.");
            check(!DebugBackingFieldAttribute.IsDebugCompile(Module(256)), "Another value with DisableOptimizations must not count as Debug.");
            check(!DebugBackingFieldAttribute.IsDebugCompile(Module(262)), "A neighbouring modes value must not count as Debug.");
            check(!DebugBackingFieldAttribute.IsDebugCompile(Module(263, boolConstructor: true)), "The (bool,bool) constructor must not count as Debug.");
            check(!DebugBackingFieldAttribute.IsDebugCompile(Module(263, namedArgument: true)), "A blob with named arguments must not count as Debug.");

            // The one allowed attribute case: a Debug compile, a compiler-generated backing field, Never.
            var allowed = Field(debug, backing, Generated(debug), Browsable(debug, 0));
            var original = DebugBackingFieldAttribute.Original(allowed);
            check(original.Count == 1 && original[0].AttributeType.Name == "CompilerGeneratedAttribute", "Debug backing field must reduce to CompilerGenerated only.");
            check(allowed.CustomAttributes.Count == 2, "Filtering must not edit the field.");
            var reversed = Field(debug, backing, Browsable(debug, 0), Generated(debug));
            original = DebugBackingFieldAttribute.Original(reversed);
            check(original.Count == 1 && original[0].AttributeType.Name == "CompilerGeneratedAttribute", "Attribute position must not matter inside the allowance.");

            // Optimised compiles stay as strict as before: the extra attribute is visible.
            foreach (var module in new[] { release, plain })
            {
                var field = Field(module, backing, Generated(module), Browsable(module, 0));
                check(DebugBackingFieldAttribute.Original(field).Count == 2, "A non-Debug compile must keep the extra attribute.");
            }

            // Everything else inside a Debug compile is still refused.
            check(DebugBackingFieldAttribute.Original(Field(debug, "m_position", Generated(debug), Browsable(debug, 0))).Count == 2, "A field that is not a backing field must keep the attribute.");
            var unmarked = DebugBackingFieldAttribute.Original(Field(debug, backing, Browsable(debug, 0)));
            check(unmarked.Count == 1 && unmarked[0].AttributeType.FullName == Never, "A backing field without CompilerGenerated must keep the attribute.");
            check(DebugBackingFieldAttribute.Original(Field(debug, backing, Generated(debug), Browsable(debug, 2))).Count == 2, "Only the Never state may be removed.");
            check(DebugBackingFieldAttribute.Original(Field(debug, backing, Generated(debug), Browsable(debug, 0, true))).Count == 2, "A different constructor signature with the Never blob must not be removed.");
            check(DebugBackingFieldAttribute.Original(Field(debug, backing, Generated(debug), Browsable(debug, 0), Browsable(debug, 0))).Count == 3, "A duplicated attribute must not be removed.");
            // The field behind a field-like event is a compiler backing field too.
            var eventField = Field(debug, "Changed", Generated(debug), Browsable(debug, 0));
            eventField.DeclaringType.Events.Add(new EventDefinition("Changed", EventAttributes.None, debug.TypeSystem.Int32));
            check(DebugBackingFieldAttribute.Original(eventField).Count == 1, "The field of a field-like event must lose the Debug attribute.");
            var mistyped = Field(debug, "Changed", Generated(debug), Browsable(debug, 0));
            mistyped.DeclaringType.Events.Add(new EventDefinition("Changed", EventAttributes.None, debug.TypeSystem.Object));
            check(DebugBackingFieldAttribute.Original(mistyped).Count == 2, "An event of another type must not unlock the attribute.");
            var unrelated = Field(debug, "Changed", Generated(debug), Browsable(debug, 0));
            check(DebugBackingFieldAttribute.Original(unrelated).Count == 2, "A field with no matching event must keep the attribute.");
            var other = Field(debug, backing, Generated(debug), Marker(debug, "System", "ObsoleteAttribute", new byte[] { 1, 0, 0, 0 }), Browsable(debug, 0));
            original = DebugBackingFieldAttribute.Original(other);
            check(original.Count == 2 && original[1].AttributeType.Name == "ObsoleteAttribute", "Another attribute must survive the filter.");

            // Second allowance: the real Debug body of `get { return m_field; }`, and one
            // negative per condition the helper checks.
            check(Accepts(debug), "The recorded real Debug getter body must be recognised in a Debug compile.");
            check(!Accepts(release), "The Debug getter body must be refused in a release compile.");
            check(!Accepts(plain), "The Debug getter body must be refused without DebuggableAttribute.");
            check(!Accepts(debug, (m, f) => m.Body.Variables[0] = new VariableDefinition(debug.TypeSystem.Single)), "The local must have the return type.");
            check(!Accepts(debug, (m, f) => m.Body.MaxStackSize = 2), "Maxstack must be 1.");
            check(!Accepts(debug, (m, f) => m.Body.InitLocals = false), "Locals must be initialised.");
            check(!Accepts(debug, (m, f) => m.Body.Variables.Add(new VariableDefinition(debug.TypeSystem.Int32))), "Exactly one local is allowed.");
            check(!Accepts(debug, (m, f) => m.Body.Variables[0] = new VariableDefinition(new PinnedType(debug.TypeSystem.Int32))), "A pinned local is refused (the pinned type also differs by name).");
            check(!Accepts(debug, (m, f) => m.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally) {
                TryStart = m.Body.Instructions[0], TryEnd = m.Body.Instructions[1], HandlerStart = m.Body.Instructions[1], HandlerEnd = m.Body.Instructions[2] })), "An exception handler is refused.");
            check(!Accepts(debug, (m, f) => m.Body.Instructions[4].Operand = m.Body.Instructions[6]), "The branch must target ldloc.0.");
            check(!Accepts(debug, (m, f) => m.Body.Instructions[5].Offset += 1), "A shifted offset is refused.");
            check(!Accepts(debug, (m, f) => Replace(m, 1, OpCodes.Ldarg_1)), "ldarg.1 is not ldarg.0.");
            check(!Accepts(debug, (m, f) => Replace(m, 0, OpCodes.Ldarg_0)), "The leading nop is required.");
            check(!Accepts(debug, (m, f) => m.Body.Instructions[2].Operand = new MethodReference("M", debug.TypeSystem.Void, m.DeclaringType)), "The load must reference a field.");
            check(!Accepts(debug, (m, f) => m.Body.Instructions.RemoveAt(0)), "A body missing an instruction is refused.");
            check(!Accepts(debug, (m, f) => m.Body.Instructions.Add(Instruction.Create(OpCodes.Nop))), "A body with an extra instruction is refused.");
            return checks;
        }
    }
}
