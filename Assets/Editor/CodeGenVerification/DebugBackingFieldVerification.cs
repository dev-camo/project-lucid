using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using ProjectLucid.CodeGen;

namespace ProjectLucid
{
    // Synthetic Cecil modules (compilation-pipeline fixtures, not game content) that
    // pin the exact scope of the Debug-only DebuggerBrowsable(Never) allowance and
    // show that release and Editor compiles stay as strict as before.
    public static class DebugBackingFieldVerification
    {
        private const string Never = "System.Diagnostics.DebuggerBrowsableAttribute";
        private static AssemblyNameReference core;

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

        private static CustomAttribute Browsable(ModuleDefinition module, byte state)
        {
            var type = Reference(module, "System.Diagnostics", "DebuggerBrowsableAttribute");
            var constructor = new MethodReference(".ctor", module.TypeSystem.Void, type) { HasThis = true };
            constructor.Parameters.Add(new ParameterDefinition(Reference(module, "System.Diagnostics", "DebuggerBrowsableState", true)));
            return new CustomAttribute(constructor, new byte[] { 1, 0, state, 0, 0, 0, 0, 0 });
        }

        private static ModuleDefinition Module(int? debuggingModes)
        {
            core = new AssemblyNameReference("netstandard", new Version(2, 1, 0, 0));
            var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("Fixture", new Version(0, 0, 0, 0)), "Fixture.dll", ModuleKind.Dll);
            var module = assembly.MainModule;
            module.AssemblyReferences.Add(core);
            if (debuggingModes.HasValue)
            {
                var debuggable = Reference(module, "System.Diagnostics", "DebuggableAttribute");
                var modes = new TypeReference("", "DebuggingModes", module, core, true) { DeclaringType = debuggable };
                var constructor = new MethodReference(".ctor", module.TypeSystem.Void, debuggable) { HasThis = true };
                constructor.Parameters.Add(new ParameterDefinition(modes));
                var blob = new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 };
                BitConverter.GetBytes(debuggingModes.Value).CopyTo(blob, 2);
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

        // The body shapes the two compiles give `get { return m_field; }`.
        private static MethodDefinition Getter(ModuleDefinition module, bool debugShape, TypeReference local)
        {
            var type = new TypeDefinition("Fixture", "Holder" + module.Types.Count, TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
            module.Types.Add(type);
            var field = new FieldDefinition("m_value", FieldAttributes.Private, module.TypeSystem.Int32);
            type.Fields.Add(field);
            var method = new MethodDefinition("get_Value", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, module.TypeSystem.Int32);
            type.Methods.Add(method);
            var il = method.Body.GetILProcessor();
            if (debugShape)
            {
                var load = Instruction.Create(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Nop); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Stloc_0);
                il.Emit(OpCodes.Br_S, load); il.Append(load); il.Emit(OpCodes.Ret);
                // Cecil leaves offsets at zero on bodies built in memory; give the fixture the
                // 1+1+5+1+2+1+1 byte layout a read module reports.
                int[] offsets = { 0, 1, 2, 7, 8, 10, 11 };
                for (int i = 0; i < offsets.Length; ++i) method.Body.Instructions[i].Offset = offsets[i];
                method.Body.Variables.Add(new VariableDefinition(local));
                method.Body.InitLocals = true;
                method.Body.MaxStackSize = 1;
            }
            else
            {
                il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Ret);
                method.Body.MaxStackSize = 8;
            }
            return method;
        }

        public static int Run()
        {
            int checks = 0;
            Action<bool, string> check = (value, description) => {
                if (!value) throw new InvalidOperationException(description);
                ++checks;
            };
            const string backing = "<Position>k__BackingField";

            var debug = Module(256 | 2);
            var release = Module(2);
            var plain = Module(null);
            check(DebugBackingFieldAttribute.IsDebugCompile(debug), "A DisableOptimizations compile must count as Debug.");
            check(!DebugBackingFieldAttribute.IsDebugCompile(release), "An optimised compile must not count as Debug.");
            check(!DebugBackingFieldAttribute.IsDebugCompile(plain), "A compile without DebuggableAttribute must not count as Debug.");
            check(!DebugBackingFieldAttribute.IsDebugCompile(null), "A missing module must not count as Debug.");

            // The one allowed case: a Debug compile, a compiler-generated backing field, Never.
            var allowed = Field(debug, backing, Generated(debug), Browsable(debug, 0));
            var original = DebugBackingFieldAttribute.Original(allowed);
            check(original.Count == 1 && original[0].AttributeType.Name == "CompilerGeneratedAttribute", "Debug backing field must reduce to CompilerGenerated only.");
            check(allowed.CustomAttributes.Count == 2, "Filtering must not edit the field.");
            var reversed = Field(debug, backing, Browsable(debug, 0), Generated(debug));
            original = DebugBackingFieldAttribute.Original(reversed);
            check(original.Count == 1 && original[0].AttributeType.Name == "CompilerGeneratedAttribute", "Attribute position must not matter inside the allowance.");

            // Release and Editor compiles stay as strict as before: the extra attribute is visible.
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
            check(DebugBackingFieldAttribute.Original(Field(debug, backing, Generated(debug), Browsable(debug, 0), Browsable(debug, 0))).Count == 3, "A duplicated attribute must not be removed.");
            // The field behind a field-like event is a compiler backing field too.
            var eventField = Field(debug, "Changed", Generated(debug), Browsable(debug, 0));
            eventField.DeclaringType.Events.Add(new EventDefinition("Changed", EventAttributes.None, debug.TypeSystem.Object));
            check(DebugBackingFieldAttribute.Original(eventField).Count == 1, "The field of a field-like event must lose the Debug attribute.");
            var unrelated = Field(debug, "Changed", Generated(debug), Browsable(debug, 0));
            check(DebugBackingFieldAttribute.Original(unrelated).Count == 2, "A field with no matching event must keep the attribute.");
            var other = Field(debug, backing, Generated(debug), Marker(debug, "System", "ObsoleteAttribute", new byte[] { 1, 0, 0, 0 }), Browsable(debug, 0));
            original = DebugBackingFieldAttribute.Original(other);
            check(original.Count == 2 && original[1].AttributeType.Name == "ObsoleteAttribute", "Another attribute must survive the filter.");

            // Debug compiles also give `get { return m_field; }` a nop and a local; that exact shape,
            // and only in a Debug compile, is the second allowance.
            check(DebugAccessorBody.IsReturnField(Getter(debug, true, debug.TypeSystem.Int32), debug), "The exact Debug getter body must be recognised in a Debug compile.");
            check(!DebugAccessorBody.IsReturnField(Getter(release, true, release.TypeSystem.Int32), release), "The Debug getter body must be refused in a release compile.");
            check(!DebugAccessorBody.IsReturnField(Getter(debug, true, debug.TypeSystem.Single), debug), "The Debug getter body must keep a local of the return type.");
            check(!DebugAccessorBody.IsReturnField(Getter(debug, false, null), debug), "The release getter body is not the Debug shape.");
            return checks;
        }
    }
}
