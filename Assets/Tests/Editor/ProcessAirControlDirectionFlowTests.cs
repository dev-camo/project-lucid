using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests;
using UnityEngine;
using Instruction = System.Collections.Generic.KeyValuePair<System.Reflection.Emit.OpCode, object>;

namespace ProjectLucid.Editor
{
    // Static data-flow check on CharacterMovementUtilities.ProcessAirControl<T>. Running it needs a
    // fully started Character (tracking, camera, sticky controls, FSM storage), which a test cannot
    // build without fakes. Expectations come from the 1.10.1 x86-64 gshared body
    // _CharacterMovementUtilities_ProcessAirControl_TisRuntimeObject_..._gshared (0x8e9970..0x8ea2b0),
    // not from the reconstructed source. Only call order and which IL local feeds each use are checked.
    // The second case shows the two directions really diverge for the air override.
    public sealed class ProcessAirControlDirectionFlowTests
    {
        private enum LocalKind { None, Load, Address, Store }

        // Native data flow: 0x8e9cf6..0x8e9d01 copies the direction (-0x60/-0x40) into a separate local
        // (-0xb0/-0xa8) after CalculateEffectiveInputMagnitude (0x8e9cde); 0x8e9d1f passes &-0xb0 to the
        // virtual UpdateForwardSpeed (vtable 0x2f8, call 0x8e9d36); 0x8e9d41/0x8e9d54 multiply x/z of that
        // ref-updated local by the speed before the storage is read (0x8e9d61); the GroundInputDirection
        // SetValue<Vector3> (key static offset 0x7f0, call 0x8e9e1e) reloads the untouched -0x60/-0x40 at
        // 0x8e9e13/0x8e9e17; WorldRotation (0x54/0x5c) and the rotation multiply (0x8e9e43) follow it.
        // Which of the two locals is the copy is a slot assignment, so either copy direction is accepted.
        [Test]
        public void GroundInputDirectionStoresThePreUpdateDirectionWhileTheVelocityUsesTheUpdatedOne()
        {
            MethodInfo method = typeof(CharacterMovementUtilities).GetMethod(nameof(CharacterMovementUtilities.ProcessAirControl));
            Assert.That(method, Is.Not.Null);
            IList<LocalVariableInfo> locals = method.GetMethodBody().LocalVariables;
            List<Instruction> il = OriginalMethodBodyReader.Instructions(method);

            int effective = Single(il, i => Called(i)?.Name == "CalculateEffectiveInputMagnitude");
            int update = Single(il, i => Called(i)?.Name == "UpdateForwardSpeed");
            var updateMethod = (MethodInfo)il[update].Value;
            Assert.That(updateMethod.DeclaringType.GetGenericTypeDefinition(), Is.EqualTo(typeof(CharacterAbility_Movement<>)));
            Assert.That(updateMethod.GetParameters().Length, Is.EqualTo(7), "the public seven-argument overload");

            FieldInfo key = typeof(ActorFSMKeys).GetField(nameof(ActorFSMKeys.GroundInputDirection));
            int keyLoad = Single(il, i => i.Key == OpCodes.Ldsfld && Same(i.Value as MemberInfo, key));
            int setValue = il.FindIndex(keyLoad, i => Called(i)?.Name == nameof(IGraphStorage.SetValue));
            Assert.That(setValue, Is.GreaterThan(keyLoad));
            var setMethod = (MethodInfo)il[setValue].Value;
            Assert.That(setMethod.DeclaringType, Is.EqualTo(typeof(IGraphStorage)));
            Assert.That(setMethod.GetGenericArguments(), Is.EqualTo(new[] { typeof(Vector3) }));

            MethodInfo rotate = typeof(Quaternion).GetMethod("op_Multiply", new[] { typeof(Quaternion), typeof(Vector3) });
            int multiply = il.FindIndex(setValue, i => Same(Called(i), rotate));
            int clamp = il.FindIndex(update, i => Called(i)?.Name == "UpdateClampedVelocity");
            MethodInfo setWorldVelocity = typeof(Actor).GetMethod(nameof(Actor.SetWorldVelocity), new[] { typeof(Vector3) });
            int velocityStore = il.FindIndex(update, i => Called(i) is MethodInfo m && Same(m.GetBaseDefinition(), setWorldVelocity));
            Assert.That(new[] { effective, update, keyLoad, setValue, multiply, clamp, velocityStore },
                Is.Ordered.Ascending.And.Unique,
                "UpdateForwardSpeed, SetValue(GroundInputDirection), WorldRotation * velocity, UpdateClampedVelocity, SetWorldVelocity");

            // The ref direction argument: the only Vector3 address taken while the call's arguments are built.
            List<int> refArguments = Range(effective + 1, update)
                .Where(i => Kind(il[i]) == LocalKind.Address && IsVector3(locals, il[i]))
                .Select(i => OriginalMethodBodyReader.LocalIndex(il[i])).ToList();
            Assert.That(refArguments.Count, Is.EqualTo(1), "one Vector3 passed by ref to UpdateForwardSpeed");
            int updatedLocal = refArguments[0];

            // SetValue's value argument is the last thing pushed before the call.
            Instruction stored = il[setValue - 1];
            Assert.That(Kind(stored), Is.EqualTo(LocalKind.Load), "SetValue receives a Vector3 local by value");
            Assert.That(IsVector3(locals, stored), Is.True);
            int storedLocal = OriginalMethodBodyReader.LocalIndex(stored);

            Assert.That(storedLocal, Is.Not.EqualTo(updatedLocal),
                "GroundInputDirection must not be read from the local UpdateForwardSpeed rewrote");
            Assert.That(Range(effective + 1, update).Any(i => Kind(il[i]) == LocalKind.Store && Kind(il[i - 1]) == LocalKind.Load &&
                    IsCopyBetween(OriginalMethodBodyReader.LocalIndex(il[i - 1]), OriginalMethodBodyReader.LocalIndex(il[i]), storedLocal, updatedLocal)),
                Is.True, "one direction local is copied into the other before the call");
            Assert.That(Range(update, setValue).Any(i => OriginalMethodBodyReader.LocalIndex(il[i]) == storedLocal &&
                Kind(il[i]) != LocalKind.Load), Is.False, "the stored direction is not written between the call and SetValue");

            // Velocity x/z come from the updated local (0x8e9d41/0x8e9d54), never the stored one, and are
            // read before the storage write.
            FieldInfo x = typeof(Vector3).GetField(nameof(Vector3.x));
            FieldInfo z = typeof(Vector3).GetField(nameof(Vector3.z));
            List<int> componentReads = Range(update + 1, velocityStore)
                .Where(i => il[i].Key == OpCodes.Ldfld && (Same(il[i].Value as MemberInfo, x) || Same(il[i].Value as MemberInfo, z)) &&
                    Kind(il[i - 1]) != LocalKind.None && IsVector3(locals, il[i - 1]))
                .ToList();
            Assert.That(componentReads.Select(i => OriginalMethodBodyReader.LocalIndex(il[i - 1])), Is.EqualTo(new[] { updatedLocal, updatedLocal }),
                "velocity x then z are read from the ref-updated direction");
            Assert.That(componentReads.All(i => i < keyLoad), Is.True, "x/z * speed are formed before SetValue(GroundInputDirection)");
        }

        // CharacterAbility_MovementAir<object>.UpdateForwardSpeed (0xE643A0) with no deceleration curve
        // (EvaluateDeceleration 0x540d10 returns 0), no modifier and no acceleration curve (constant 1.0f at
        // 0x29a6be0): speed = max(1 - 0 * 1, 0) = 1 (0xe644b7); velocity = dir * 1 + AngleAxis(90, up) *
        // (dir * (1 * 1)) = (1, 0, 1); its magnitude is positive, so the ref direction becomes (1, 0, 1) / sqrt(2)
        // (0xe645e8/0xe645ec). Speed 1 is above SpeedMin 0 and below the magnitude, so it is kept (0xe645fb..0xe64605).
        [Test]
        public void AirUpdateForwardSpeedTurnsTheRefDirectionForFiniteSteering()
        {
            using (var appRow = new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<App>()))
            {
                // The CharacterAbility field initializer looks up the App reference; own that row.
                appRow.Register(null);
                var ability = new AirSteeringProbe();
                var motion = new CharacterAbilityDefinition.Motion { SpeedMin = 0f, SpeedMax = 10f, Acceleration = 1f };
                Vector3 before = new Vector3(0f, 0f, 1f);
                Vector3 direction = before;
                float speed = 1f;

                ability.UpdateAirForwardSpeed(ref speed, ref direction, 1f, motion, 90f, 1f, null, true);

                Assert.That(speed, Is.EqualTo(1f).Within(1e-6f));
                Assert.That(direction.x, Is.EqualTo(0.70710677f).Within(1e-5f));
                Assert.That(direction.y, Is.EqualTo(0f).Within(1e-5f));
                Assert.That(direction.z, Is.EqualTo(0.70710677f).Within(1e-5f));
                Assert.That(Vector3.Distance(direction, before), Is.GreaterThan(0.5f),
                    "the ref-updated direction differs from the pre-update copy ProcessAirControl stores");
            }
        }

        private sealed class AirSteeringProbe : CharacterAbility_MovementAir<CharacterAbilityDefinition_MovementAir>
        {
            public void UpdateAirForwardSpeed(ref float forwardSpeed, ref Vector3 forwardDirection, float deltaTime,
                CharacterAbilityDefinition.Motion motion, float intendedTurnDelta, float effectiveInputMagnitude,
                AnimationCurve accelerationCurve, bool applyDeceleration) =>
                UpdateForwardSpeed(ref forwardSpeed, ref forwardDirection, deltaTime, motion, intendedTurnDelta,
                    effectiveInputMagnitude, accelerationCurve, applyDeceleration);
        }

        private static MethodBase Called(Instruction i) =>
            i.Key == OpCodes.Call || i.Key == OpCodes.Callvirt ? i.Value as MethodBase : null;

        private static LocalKind Kind(Instruction i)
        {
            if (OriginalMethodBodyReader.LocalIndex(i) < 0) return LocalKind.None;
            if (i.Key == OpCodes.Ldloca_S || i.Key == OpCodes.Ldloca) return LocalKind.Address;
            if (i.Key == OpCodes.Stloc_S || i.Key == OpCodes.Stloc || (i.Key.Value >= OpCodes.Stloc_0.Value && i.Key.Value <= OpCodes.Stloc_3.Value))
                return LocalKind.Store;
            return LocalKind.Load;
        }

        private static bool IsVector3(IList<LocalVariableInfo> locals, Instruction i) =>
            OriginalMethodBodyReader.LocalIndex(i) is int index && index >= 0 && locals[index].LocalType == typeof(Vector3);

        private static bool IsCopyBetween(int from, int to, int a, int b) => (from == a && to == b) || (from == b && to == a);

        private static bool Same(MemberInfo actual, MemberInfo expected)
        {
            Assert.That(expected, Is.Not.Null, "expected member exists");
            return actual != null && actual.Module == expected.Module && actual.MetadataToken == expected.MetadataToken;
        }

        private static int Single(List<Instruction> il, Predicate<Instruction> match)
        {
            List<int> hits = Range(0, il.Count).Where(i => match(il[i])).ToList();
            Assert.That(hits.Count, Is.EqualTo(1), "exactly one matching instruction");
            return hits[0];
        }

        private static IEnumerable<int> Range(int start, int end) => Enumerable.Range(start, Math.Max(0, end - start));
    }
}
