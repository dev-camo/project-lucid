using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace ProjectLucid.Tests
{
    // Minimal IL walker for declaration cases that check a compiled body's call or
    // initializer order without executing it.
    internal static class OriginalMethodBodyReader
    {
        private static readonly Dictionary<short, OpCode> Codes = CreateCodes();

        private static Dictionary<short, OpCode> CreateCodes()
        {
            var codes = new Dictionary<short, OpCode>();
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var code = (OpCode)field.GetValue(null);
                codes[code.Value] = code;
            }
            return codes;
        }

        // Every instruction in order, with method/field/token operands resolved,
        // 32-bit immediates (ldc.i4) boxed as int and variable operands (ldloc.s,
        // stloc, ldarg.s, ...) reported as their index.
        public static List<KeyValuePair<OpCode, object>> Instructions(MethodBase method)
        {
            byte[] il = method.GetMethodBody().GetILAsByteArray();
            Type[] typeArguments = method.DeclaringType.IsGenericType ? method.DeclaringType.GetGenericArguments() : null;
            Type[] methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
            var instructions = new List<KeyValuePair<OpCode, object>>();
            for (int i = 0; i < il.Length;)
            {
                OpCode code = il[i] == 0xFE ? Codes[unchecked((short)(0xFE00 | il[i + 1]))] : Codes[il[i]];
                i += code.Size;
                object operand = null;
                switch (code.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI: i += 1; break;
                    case OperandType.ShortInlineVar: operand = (int)il[i]; i += 1; break;
                    case OperandType.InlineVar: operand = (int)BitConverter.ToUInt16(il, i); i += 2; break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR: i += 8; break;
                    case OperandType.InlineSwitch: i += 4 + 4 * BitConverter.ToInt32(il, i); break;
                    case OperandType.InlineI: operand = BitConverter.ToInt32(il, i); i += 4; break;
                    case OperandType.InlineMethod:
                        operand = method.Module.ResolveMethod(BitConverter.ToInt32(il, i), typeArguments, methodArguments); i += 4; break;
                    case OperandType.InlineField:
                        operand = method.Module.ResolveField(BitConverter.ToInt32(il, i), typeArguments, methodArguments); i += 4; break;
                    case OperandType.InlineTok:
                        operand = method.Module.ResolveMember(BitConverter.ToInt32(il, i), typeArguments, methodArguments); i += 4; break;
                    default: i += 4; break;
                }
                instructions.Add(new KeyValuePair<OpCode, object>(code, operand));
            }
            return instructions;
        }

        // The local an ldloc, ldloca or stloc form names, or -1 for any other instruction.
        public static int LocalIndex(KeyValuePair<OpCode, object> instruction)
        {
            short value = instruction.Key.Value;
            if (value >= OpCodes.Ldloc_0.Value && value <= OpCodes.Ldloc_3.Value) return value - OpCodes.Ldloc_0.Value;
            if (value >= OpCodes.Stloc_0.Value && value <= OpCodes.Stloc_3.Value) return value - OpCodes.Stloc_0.Value;
            OpCode code = instruction.Key;
            if (code == OpCodes.Ldloc_S || code == OpCodes.Ldloca_S || code == OpCodes.Stloc_S ||
                code == OpCodes.Ldloc || code == OpCodes.Ldloca || code == OpCodes.Stloc)
                return (int)instruction.Value;
            return -1;
        }
    }
}
