using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace ProjectLucid.Tests
{
    // Reads the call, callvirt and newobj targets of a compiled method body in
    // linear IL order (ECMA-335 III). Used only where an original order has no
    // other observable effect; it inspects metadata and never invokes the method.
    // Linear order does not prove that every runtime path executes each call.
    // ldftn and ldvirtftn also carry a method token but only load a pointer
    // (III.3.41, III.4.18), so they add no record. jmp (III.3.37) and calli are
    // not recorded either; C# never emits jmp, and calli has no method token.
    internal static class OriginalILCallSequence
    {
        internal struct Call
        {
            public int Offset;
            public MethodBase Target;
        }

        private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (OpCode)field.GetValue(null))
            .ToDictionary(code => code.Value);

        // call, callvirt and newobj invoke their method token; ldftn and ldvirtftn only load it.
        // Every EditMode call witness uses this one predicate.
        internal static bool IsInvocation(OpCode code) => code == OpCodes.Call || code == OpCodes.Callvirt || code == OpCodes.Newobj;

        internal static List<Call> Calls(MethodBase method)
        {
            byte[] il = method.GetMethodBody().GetILAsByteArray();
            Type[] typeArguments = method.DeclaringType.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
            Type[] methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
            var calls = new List<Call>();
            int position = 0;
            while (position < il.Length)
            {
                int start = position;
                short value = il[position] == 0xFE ? unchecked((short)(0xFE00 | il[position + 1])) : il[position];
                position += il[position] == 0xFE ? 2 : 1;
                OpCode code = Codes[value];
                switch (code.OperandType)
                {
                    case OperandType.InlineNone:
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar:
                        position += 1;
                        break;
                    case OperandType.InlineVar:
                        position += 2;
                        break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                        position += 8;
                        break;
                    case OperandType.InlineSwitch:
                        position += 4 + 4 * BitConverter.ToInt32(il, position);
                        break;
                    case OperandType.InlineMethod:
                        if (IsInvocation(code))
                        {
                            calls.Add(new Call
                            {
                                Offset = start,
                                Target = method.Module.ResolveMethod(BitConverter.ToInt32(il, position), typeArguments, methodArguments),
                            });
                        }
                        position += 4;
                        break;
                    default:
                        position += 4;
                        break;
                }
            }
            return calls;
        }

        // "Type.Name" for each target, in IL order.
        internal static string[] Names(IEnumerable<Call> calls) =>
            calls.Select(call => call.Target.DeclaringType.Name + "." + call.Target.Name).ToArray();
    }
}
