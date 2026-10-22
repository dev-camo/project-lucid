using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace ProjectLucid.CodeGen
{
    // A Unity development player compiles its scripts in Debug mode (the Bee
    // "PDevDbg" graph; BuildOptions.ForceOptimizeScriptCompilation is obsolete and
    // ignored in 2022.3.54f1, and neither the Editor's code-optimisation mode nor
    // -releaseCodeOptimization changes the player compile). Roslyn then adds
    // [DebuggerBrowsable(DebuggerBrowsableState.Never)] to every auto-property
    // backing field. The shipped release metadata has no such attribute, so the
    // restoring post-processors must keep rejecting it everywhere except this one
    // compiler-generated case. The allowance is exact: only a Debug compile
    // (DebuggableAttribute with DisableOptimizations), only a field named
    // <Name>k__BackingField (or the field of a field-like event) that also carries
    // CompilerGeneratedAttribute, and only
    // the one attribute with the exact constructor and Never blob. A release or
    // Editor compile, or any other extra attribute, is returned unchanged.
    public static class DebugBackingFieldAttribute
    {
        private const string Debuggable = "System.Diagnostics.DebuggableAttribute";
        private const string Modes = "System.Diagnostics.DebuggableAttribute/DebuggingModes";
        private const string Browsable = "System.Diagnostics.DebuggerBrowsableAttribute";
        private const string BrowsableConstructor = "System.Void System.Diagnostics.DebuggerBrowsableAttribute::.ctor(System.Diagnostics.DebuggerBrowsableState)";
        private const string Generated = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";
        private const int DisableOptimizations = 256;
        private static readonly byte[] NeverBlob = { 1, 0, 0, 0, 0, 0, 0, 0 };  // prolog, int32 Never, no named arguments

        public static bool IsDebugCompile(ModuleDefinition module)
        {
            if (module == null || module.Assembly == null) return false;
            foreach (var attribute in module.Assembly.CustomAttributes)
            {
                // Read the blob, not ConstructorArguments: decoding the enum would
                // resolve a dependency, which the post-processors' resolvers refuse.
                if (attribute.AttributeType.FullName != Debuggable || attribute.Constructor.Parameters.Count != 1 ||
                    attribute.Constructor.Parameters[0].ParameterType.FullName != Modes) continue;
                var blob = attribute.GetBlob();
                // prolog, int32 DebuggingModes, no named arguments.
                return blob.Length == 8 && (BitConverter.ToInt32(blob, 2) & DisableOptimizations) != 0;
            }
            return false;
        }

        // The field's custom attributes as the release compile would have emitted them.
        public static IList<CustomAttribute> Original(FieldDefinition field)
        {
            IList<CustomAttribute> attributes = field.CustomAttributes;
            if (!IsCompilerBackingField(field) || !IsDebugCompile(field.Module) || !attributes.Any(a => a.AttributeType.FullName == Generated))
                return attributes;
            var found = attributes.Where(IsNever).ToArray();
            return found.Length == 1 ? attributes.Where(a => !ReferenceEquals(a, found[0])).ToList() : attributes;
        }

        // An auto-property's <Name>k__BackingField, or the field behind a field-like
        // event (a field named after an event of the same type).
        private static bool IsCompilerBackingField(FieldDefinition field)
        {
            if (field.Name.StartsWith("<", StringComparison.Ordinal) && field.Name.EndsWith(">k__BackingField", StringComparison.Ordinal)) return true;
            return field.DeclaringType != null && field.DeclaringType.Events.Any(e => e.Name == field.Name);
        }

        private static bool IsNever(CustomAttribute attribute)
        {
            return attribute.AttributeType.FullName == Browsable && attribute.Constructor.FullName == BrowsableConstructor &&
                attribute.GetBlob().SequenceEqual(NeverBlob);
        }
    }

    // The other Debug-only difference InputMonitor's checks meet: an explicit
    // `get { return m_field; }` compiles in Debug to
    //   nop; ldarg.0; ldfld; stloc.0; br.s next; ldloc.0; ret
    // with one local of the return type (offsets 0,1,2,7,8,10,11; maxstack 1, init locals),
    // where a release compile emits `ldarg.0; ldfld; ret`. Only that exact body,
    // only in a Debug compile, is recognised; the caller still checks the field.
    public static class DebugAccessorBody
    {
        public static bool IsReturnField(MethodDefinition method, ModuleDefinition module)
        {
            if (!DebugBackingFieldAttribute.IsDebugCompile(module) || !method.HasBody) return false;
            var body = method.Body; var instructions = body.Instructions;
            if (body.MaxStackSize != 1 || !body.InitLocals || body.Variables.Count != 1 || body.ExceptionHandlers.Count != 0 ||
                body.Variables[0].IsPinned || body.Variables[0].VariableType.FullName != method.ReturnType.FullName || instructions.Count != 7) return false;
            string[] codes = { "nop", "ldarg.0", "ldfld", "stloc.0", "br.s", "ldloc.0", "ret" };
            int[] offsets = { 0, 1, 2, 7, 8, 10, 11 };
            for (int i = 0; i < codes.Length; ++i)
                if (instructions[i].OpCode.Name != codes[i] || instructions[i].Offset != offsets[i]) return false;
            return ReferenceEquals(instructions[4].Operand, instructions[5]) && instructions[2].Operand is FieldReference;
        }
    }
}
