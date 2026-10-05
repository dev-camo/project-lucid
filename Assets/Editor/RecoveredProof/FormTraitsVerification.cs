using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HardlightProject;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace ProjectLucid
{
    public static class FormTraitsVerification
    {
        private static int checks;
        private static void Require(bool condition, string description)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(description);
        }
        private static List<(OpCode opcode, object operand)> ReadIL(MethodBase method)
        {
            var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null))
                .ToDictionary(c => unchecked((ushort)c.Value));
            byte[] bytes = method.GetMethodBody().GetILAsByteArray();
            var result = new List<(OpCode, object)>();
            for (int i = 0; i < bytes.Length;)
            {
                ushort value = bytes[i++];
                if (value == 0xfe) value = (ushort)(0xfe00 | bytes[i++]);
                OpCode code = codes[value];
                object operand = null;
                switch (code.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineI: operand = (sbyte)bytes[i]; i++; break;
                    case OperandType.ShortInlineVar: operand = bytes[i]; i++; break;
                    case OperandType.ShortInlineBrTarget: operand = i + 1 + (sbyte)bytes[i]; i++; break;
                    case OperandType.InlineVar: operand = BitConverter.ToUInt16(bytes, i); i += 2; break;
                    case OperandType.InlineBrTarget: operand = i + 4 + BitConverter.ToInt32(bytes, i); i += 4; break;
                    case OperandType.InlineI: operand = BitConverter.ToInt32(bytes, i); i += 4; break;
                    case OperandType.InlineI8: operand = BitConverter.ToInt64(bytes, i); i += 8; break;
                    case OperandType.ShortInlineR: operand = BitConverter.ToSingle(bytes, i); i += 4; break;
                    case OperandType.InlineR: operand = BitConverter.ToDouble(bytes, i); i += 8; break;
                    case OperandType.InlineMethod: operand = method.Module.ResolveMethod(BitConverter.ToInt32(bytes, i)); i += 4; break;
                    case OperandType.InlineField: operand = method.Module.ResolveField(BitConverter.ToInt32(bytes, i)); i += 4; break;
                    case OperandType.InlineType: operand = method.Module.ResolveType(BitConverter.ToInt32(bytes, i)); i += 4; break;
                    case OperandType.InlineTok: operand = method.Module.ResolveMember(BitConverter.ToInt32(bytes, i)); i += 4; break;
                    case OperandType.InlineString: operand = method.Module.ResolveString(BitConverter.ToInt32(bytes, i)); i += 4; break;
                    case OperandType.InlineSig: operand = method.Module.ResolveSignature(BitConverter.ToInt32(bytes, i)); i += 4; break;
                    case OperandType.InlineSwitch:
                        int count = BitConverter.ToInt32(bytes, i); i += 4;
                        operand = Enumerable.Range(0, count).Select(n => i + 4 * count + BitConverter.ToInt32(bytes, i + 4 * n)).ToArray(); i += 4 * count;
                        break;
                    default: throw new InvalidOperationException("Unsupported genuine IL operand " + code.OperandType);
                }
                result.Add((code, operand));
            }
            return result;
        }
        private static string CallName(MethodBase method) => method.DeclaringType.FullName + "." + method.Name;

        public static int RunManaged()
        {
            checks = 0;
            Type type = typeof(FormTraits);
            Require(type.FullName == "HardlightProject.FormTraits" && type.Assembly.GetName().Name == "Game.Runtime", "original form type/assembly identity");
            Require(type.IsPublic && !type.IsSealed && !type.IsAbstract && type.BaseType == typeof(object), "original public concrete nonsealed form type");
            Require(type.IsSerializable && (type.Attributes & TypeAttributes.BeforeFieldInit) != 0 && type.TypeInitializer == null, "original Serializable/BeforeFieldInit flags");
            var options = type.GetCustomAttributes<Il2CppSetOptionAttribute>(false).ToArray();
            Require(options.Length == 2 && options[0].Option == Option.ArrayBoundsChecks && options[1].Option == Option.NullChecks, "original ordered IL2CPP options");
            Require(options.All(a => a.Value is bool && !(bool)a.Value), "original false option values");
            Require(type.GetCustomAttributesData().All(a => a.AttributeType == typeof(Il2CppSetOptionAttribute) || a.AttributeType == typeof(SerializableAttribute)), "original stored options and reflected Serializable pseudo-attribute only");
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
            Require(fields.Select(f => f.Name).SequenceEqual(new[] { "FormType", "ColliderRadius", "ColliderHeight" }), "complete ordered original own fields");
            Require(fields[0].FieldType == typeof(ActorFormType) && fields[0].FieldType.Assembly.GetName().Name == "HLAutoGenerated", "genuine original game enum dependency");
            Require(fields.Skip(1).All(f => f.FieldType == typeof(float)), "original Single dimensions");
            foreach (var field in fields)
            {
                Require(field.IsPublic && !field.IsStatic && !field.IsLiteral && !field.IsInitOnly, "original mutable public field " + field.Name);
                Require(field.GetCustomAttributesData().Count == 0, "original attribute-free field " + field.Name);
            }
            Require(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 0, "no invented own form methods");
            ConstructorInfo ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Single();
            Require(ctor.IsPublic && !ctor.IsStatic && ctor.IsHideBySig && ctor.IsSpecialName && (ctor.Attributes & MethodAttributes.RTSpecialName) != 0, "original public constructor flags");
            Require(ctor.GetParameters().Length == 0 && ctor.GetCustomAttributesData().Count == 0, "original parameterless attribute-free constructor");
            var il = ReadIL(ctor);
            Require(il.Where(x => x.operand is FieldInfo).Select(x => ((FieldInfo)x.operand).Name).SequenceEqual(new[] { "FormType", "ColliderRadius", "ColliderHeight" }), "native initializer field-store order");
            Require(il.Where(x => x.operand is MethodBase).Select(x => CallName((MethodBase)x.operand)).SequenceEqual(new[] { "System.Object..ctor" }), "native genuine Object constructor only");
            int baseCall = il.FindIndex(x => x.operand is MethodBase);
            Require(il.FindLastIndex(x => x.opcode == OpCodes.Stfld) < baseCall, "field initializers precede base constructor");
            Require(il.Any(x => x.opcode == OpCodes.Ldc_I4 && Equals(x.operand, 602145082)) && il.Any(x => x.opcode == OpCodes.Ldc_R4 && Equals(x.operand, 0.5f)) && il.Any(x => x.opcode == OpCodes.Ldc_R4 && Equals(x.operand, 1.2f)), "original exact native default constants");
            Require((int)ActorFormType.Default == 602145082, "original Default enum literal");
            FormTraits first = new FormTraits();
            Require(first.FormType == ActorFormType.Default, "native default form");
            Require(first.ColliderRadius == 0.5f && BitConverter.ToInt32(BitConverter.GetBytes(first.ColliderRadius), 0) == 0x3f000000, "native packed radius Single");
            Require(first.ColliderHeight == 1.2f && BitConverter.ToInt32(BitConverter.GetBytes(first.ColliderHeight), 0) == 0x3f99999a, "native packed height Single");
            first.FormType = ActorFormType.Ball; first.ColliderRadius = -2f; first.ColliderHeight = -3f;
            Require(first.FormType == ActorFormType.Ball && first.ColliderRadius == -2f && first.ColliderHeight == -3f, "original mutable geometry fields have no invented validation");
            FormTraits second = new FormTraits();
            Require(!ReferenceEquals(first, second) && second.FormType == ActorFormType.Default && second.ColliderRadius == 0.5f && second.ColliderHeight == 1.2f, "instance defaults remain independent");
            return checks;
        }

        // Genuine Unity serialization is reserved for the root's actual Editor
        // test window. It does not touch original assets or save containers.
        public static int Run()
        {
            RunManaged();
            var input = new FormTraits { FormType = ActorFormType.Ball, ColliderRadius = -2.5f, ColliderHeight = 7.25f };
            string json = JsonUtility.ToJson(input);
            Require(json.Contains("\"FormType\"") && json.Contains("\"ColliderRadius\"") && json.Contains("\"ColliderHeight\""), "actual engine serializes all original fields");
            var output = JsonUtility.FromJson<FormTraits>(json);
            Require(output != null && !ReferenceEquals(input, output), "actual engine reconstructs an independent original form object");
            Require(output.FormType == ActorFormType.Ball, "actual engine preserves original enum value");
            Require(output.ColliderRadius == -2.5f, "actual engine preserves signed radius without clamp");
            Require(output.ColliderHeight == 7.25f, "actual engine preserves height");
            var existing = new FormTraits();
            JsonUtility.FromJsonOverwrite(json, existing);
            Require(existing.FormType == ActorFormType.Ball && existing.ColliderRadius == -2.5f && existing.ColliderHeight == 7.25f, "actual engine overwrites original fields");
            Require(input.FormType == ActorFormType.Ball && input.ColliderRadius == -2.5f && input.ColliderHeight == 7.25f, "actual engine roundtrip preserves source object");
            return checks;
        }
    }
}
