using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Static data-flow check on CharacterMovementUtilities.ProcessAirControl<T>. Running it needs a
    // fully started Character (tracking, camera, sticky controls, FSM storage), which a test cannot
    // build without fakes. Expectations come from the 1.10.1 x86-64 gshared body
    // _CharacterMovementUtilities_ProcessAirControl_TisRuntimeObject_..._gshared (0x8e9970..0x8ea2b0),
    // not from the reconstructed source. Only call order and which IL local feeds each use are checked.
    public sealed class ProcessAirControlDirectionFlowTests
    {
        // Native data flow: 0x8e9cf6..0x8e9d01 copies the direction (-0x60/-0x40) into a separate local
        // (-0xb0/-0xa8); 0x8e9d1f passes &-0xb0 to the virtual UpdateForwardSpeed (vtable 0x2f8, call
        // 0x8e9d36); 0x8e9d41/0x8e9d54 read x/z of that ref-updated local for the velocity; the
        // GroundInputDirection SetValue<Vector3> (key static offset 0x7f0, call 0x8e9e1e) reloads the
        // untouched -0x60/-0x40 at 0x8e9e13/0x8e9e17. CharacterAbility_MovementAir rewrites the ref
        // direction whenever it accelerates, so sharing one local would publish the updated direction.
        [Test]
        public void GroundInputDirectionStoresThePreUpdateDirectionWhileTheVelocityUsesTheUpdatedOne()
        {
            MethodInfo method = typeof(CharacterMovementUtilities).GetMethod(nameof(CharacterMovementUtilities.ProcessAirControl));
            Assert.That(method, Is.Not.Null);
            IList<LocalVariableInfo> locals = method.GetMethodBody().LocalVariables;
            List<Instruction> il = Decode(method);

            int effective = Single(il, i => IsCall(i) && i.Member.Name == "CalculateEffectiveInputMagnitude");
            int update = Single(il, i => IsCall(i) && i.Member.Name == "UpdateForwardSpeed");
            var updateMethod = (MethodInfo)il[update].Member;
            Assert.That(updateMethod.DeclaringType.GetGenericTypeDefinition(), Is.EqualTo(typeof(CharacterAbility_Movement<>)));
            Assert.That(updateMethod.GetParameters().Length, Is.EqualTo(7), "the public seven-argument overload");

            FieldInfo key = typeof(ActorFSMKeys).GetField(nameof(ActorFSMKeys.GroundInputDirection));
            int keyLoad = Single(il, i => i.Op == OpCodes.Ldsfld && Same(i.Member, key));
            int setValue = il.FindIndex(keyLoad, i => IsCall(i) && i.Member.Name == nameof(IGraphStorage.SetValue));
            Assert.That(setValue, Is.GreaterThan(keyLoad));
            var setMethod = (MethodInfo)il[setValue].Member;
            Assert.That(setMethod.DeclaringType, Is.EqualTo(typeof(IGraphStorage)));
            Assert.That(setMethod.GetGenericArguments(), Is.EqualTo(new[] { typeof(Vector3) }));

            MethodInfo rotate = typeof(Quaternion).GetMethod("op_Multiply", new[] { typeof(Quaternion), typeof(Vector3) });
            int multiply = il.FindIndex(setValue, i => IsCall(i) && Same(i.Member, rotate));
            int clamp = il.FindIndex(update, i => IsCall(i) && i.Member.Name == "UpdateClampedVelocity");
            MethodInfo setWorldVelocity = typeof(Actor).GetMethod(nameof(Actor.SetWorldVelocity), new[] { typeof(Vector3) });
            int velocityStore = il.FindIndex(update, i => IsCall(i) && i.Member is MethodInfo m &&
                Same(m.GetBaseDefinition(), setWorldVelocity));
            Assert.That(new[] { effective, update, keyLoad, setValue, multiply, clamp, velocityStore },
                Is.Ordered.Ascending.And.Unique,
                "UpdateForwardSpeed, SetValue(GroundInputDirection), WorldRotation * velocity, UpdateClampedVelocity, SetWorldVelocity");

            // The ref direction argument: the only Vector3 address taken while the call's arguments are built.
            List<int> refArguments = Range(il, effective + 1, update)
                .Where(i => il[i].Kind == LocalKind.Address && locals[il[i].Local].LocalType == typeof(Vector3))
                .Select(i => il[i].Local).ToList();
            Assert.That(refArguments.Count, Is.EqualTo(1), "one Vector3 passed by ref to UpdateForwardSpeed");
            int updatedLocal = refArguments[0];

            // SetValue's value argument is the last thing pushed before the call.
            Instruction stored = il[setValue - 1];
            Assert.That(stored.Kind, Is.EqualTo(LocalKind.Load), "SetValue receives a Vector3 local by value");
            Assert.That(locals[stored.Local].LocalType, Is.EqualTo(typeof(Vector3)));

            Assert.That(stored.Local, Is.Not.EqualTo(updatedLocal),
                "GroundInputDirection must not be read from the local UpdateForwardSpeed rewrote");
            Assert.That(Range(il, effective + 1, update).Any(i => il[i].Kind == LocalKind.Store &&
                il[i].Local == updatedLocal && il[i - 1].Kind == LocalKind.Load && il[i - 1].Local == stored.Local),
                Is.True, "the ref local is a copy of the stored direction made before the call");
            Assert.That(Range(il, update, setValue).Any(i => il[i].Local == stored.Local &&
                il[i].Kind != LocalKind.Load), Is.False, "the stored direction is not written between the call and SetValue");

            // Velocity x/z come from the updated local (0x8e9d41/0x8e9d54), never the stored one.
            FieldInfo x = typeof(Vector3).GetField(nameof(Vector3.x));
            FieldInfo z = typeof(Vector3).GetField(nameof(Vector3.z));
            List<int> velocitySources = Range(il, update + 1, multiply)
                .Where(i => il[i].Op == OpCodes.Ldfld && (Same(il[i].Member, x) || Same(il[i].Member, z)) &&
                    il[i - 1].Kind != LocalKind.None && locals[il[i - 1].Local].LocalType == typeof(Vector3))
                .Select(i => il[i - 1].Local).ToList();
            Assert.That(velocitySources, Is.EqualTo(new[] { updatedLocal, updatedLocal }),
                "velocity x then z are read from the ref-updated direction");
        }

        private enum LocalKind { None, Load, Address, Store }

        private struct Instruction
        {
            public OpCode Op;
            public MemberInfo Member;
            public LocalKind Kind;
            public int Local;
        }

        private static bool IsCall(Instruction i) => (i.Op == OpCodes.Call || i.Op == OpCodes.Callvirt) && i.Member != null;

        private static bool Same(MemberInfo actual, MemberInfo expected)
        {
            Assert.That(expected, Is.Not.Null, "expected member exists");
            return actual != null && actual.Module == expected.Module && actual.MetadataToken == expected.MetadataToken;
        }

        private static int Single(List<Instruction> il, Predicate<Instruction> match)
        {
            List<int> hits = Range(il, 0, il.Count).Where(i => match(il[i])).ToList();
            Assert.That(hits.Count, Is.EqualTo(1), "exactly one matching instruction");
            return hits[0];
        }

        private static IEnumerable<int> Range(List<Instruction> il, int start, int end) =>
            Enumerable.Range(start, Math.Max(0, end - start));

        private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null))
            .ToDictionary(op => op.Value);

        // Decodes the body, resolving member operands in the generic method's context and
        // recording which local each ldloc/ldloca/stloc form touches.
        private static List<Instruction> Decode(MethodInfo method)
        {
            byte[] bytes = method.GetMethodBody().GetILAsByteArray();
            Type[] methodArguments = method.IsGenericMethodDefinition ? method.GetGenericArguments() : null;
            var result = new List<Instruction>();
            for (int i = 0; i < bytes.Length;)
            {
                short value = bytes[i] == 0xFE ? (short)(0xFE00 | bytes[i + 1]) : bytes[i];
                Assert.That(OpCodesByValue.TryGetValue(value, out OpCode op), Is.True, "known opcode at IL_" + i.ToString("x4"));
                i += op.Size;
                var instruction = new Instruction { Op = op, Kind = LocalKind.None, Local = -1 };
                if (op == OpCodes.Ldloc_0 || op == OpCodes.Ldloc_1 || op == OpCodes.Ldloc_2 || op == OpCodes.Ldloc_3)
                    SetLocal(ref instruction, LocalKind.Load, op.Value - OpCodes.Ldloc_0.Value);
                else if (op == OpCodes.Stloc_0 || op == OpCodes.Stloc_1 || op == OpCodes.Stloc_2 || op == OpCodes.Stloc_3)
                    SetLocal(ref instruction, LocalKind.Store, op.Value - OpCodes.Stloc_0.Value);
                switch (op.OperandType)
                {
                    case OperandType.InlineMethod:
                    case OperandType.InlineField:
                        instruction.Member = method.Module.ResolveMember(BitConverter.ToInt32(bytes, i), null, methodArguments);
                        i += 4;
                        break;
                    case OperandType.ShortInlineVar:
                        if (op == OpCodes.Ldloc_S) SetLocal(ref instruction, LocalKind.Load, bytes[i]);
                        else if (op == OpCodes.Ldloca_S) SetLocal(ref instruction, LocalKind.Address, bytes[i]);
                        else if (op == OpCodes.Stloc_S) SetLocal(ref instruction, LocalKind.Store, bytes[i]);
                        i += 1;
                        break;
                    case OperandType.InlineVar:
                        int index = BitConverter.ToUInt16(bytes, i);
                        if (op == OpCodes.Ldloc) SetLocal(ref instruction, LocalKind.Load, index);
                        else if (op == OpCodes.Ldloca) SetLocal(ref instruction, LocalKind.Address, index);
                        else if (op == OpCodes.Stloc) SetLocal(ref instruction, LocalKind.Store, index);
                        i += 2;
                        break;
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI: i += 1; break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR: i += 8; break;
                    case OperandType.InlineSwitch: i += 4 + 4 * BitConverter.ToInt32(bytes, i); break;
                    default: i += 4; break;
                }
                result.Add(instruction);
            }
            return result;
        }

        private static void SetLocal(ref Instruction instruction, LocalKind kind, int local)
        {
            instruction.Kind = kind;
            instruction.Local = local;
        }
    }
}
