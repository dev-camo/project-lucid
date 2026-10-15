using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Editor
{
    // Static checks on three Game.Runtime bodies. Running them needs a fully started Character
    // (brain from OnStart, App, FSM, physics), which a test cannot build without fakes.
    // The expected call sets and order come from the 1.10.1 x86-64 bodies cited per test, not
    // from the reconstructed source. Only member references and their relative order are checked.
    // Nops, locals and branch layout depend on the compiler mode and are not checked.
    public sealed class OriginalCharacterAbilityBodyOrderTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;

        // 0600150f (RVA 0x773030): the velocity test (0x773124-0x77315A) comes first. Then, guarded by
        // NudgeCharacterUpOnEnter (+0x15C) and |Dot(up, gravity)| <= MaxDotProduct (+0x160), the
        // position moves by up * Distance (+0x164) through the virtual SetWorldPosition (0x7731ED).
        // Only after that is the forward or velocity direction read for OrientateToGravity (0x7731F3).
        [Test]
        public void AirAbilityEntryNudgeRunsBetweenTheVelocityTestAndTheForwardRead()
        {
            List<MemberInfo> refs = References(Method(typeof(CharacterState_AirAbility), "DoOnEnter"));
            MethodInfo setPosition = typeof(Actor).GetMethod("SetWorldPosition", new[] { typeof(Vector3) });
            Predicate<MemberInfo> isSetPosition = m => m is MethodInfo mi && Same(mi.GetBaseDefinition(), setPosition);
            Assert.That(refs.Count(m => isSetPosition(m)), Is.EqualTo(1), "one entry nudge through the virtual SetWorldPosition");

            FieldInfo maintain = Field(typeof(CharacterAbilityDefinition_Air), "MaintainOrientationFromVelocity");
            FieldInfo nudge = Field(typeof(CharacterAbilityDefinition_Air), "NudgeCharacterUpOnEnter");
            FieldInfo maxDot = Field(typeof(CharacterAbilityDefinition_Air), "NudgeCharacterUpOnEnterMaxDotProduct");
            FieldInfo distance = Field(typeof(CharacterAbilityDefinition_Air), "NudgeCharacterUpOnEnterDistance");
            foreach (FieldInfo field in new[] { nudge, maxDot, distance })
                Assert.That(refs.Count(m => Same(m, field)), Is.EqualTo(1), field.Name + " is read once");

            int velocityTest = Index(refs, m => Same(m, Getter(typeof(Actor), "LocalVelocity")));
            int guard = Index(refs, m => Same(m, nudge));
            int dot = Index(refs, m => Same(m, typeof(Vector3).GetMethod("Dot", new[] { typeof(Vector3), typeof(Vector3) })));
            int abs = Index(refs, m => Same(m, typeof(Mathf).GetMethod("Abs", new[] { typeof(float) })));
            int limit = Index(refs, m => Same(m, maxDot));
            int offset = Index(refs, m => Same(m, distance));
            int move = Index(refs, isSetPosition);
            int forward = Index(refs, m => Same(m, Getter(typeof(Actor), "ForwardDirection")));
            int velocityForward = Index(refs, m => Same(m, Getter(typeof(Actor), "WorldVelocityNormalised")));
            int orientate = Index(refs, m => m.Name == "OrientateToGravity");
            Assert.That(Index(refs, m => Same(m, maintain)), Is.LessThan(velocityTest));
            Assert.That(new[] { velocityTest, guard, dot, abs, limit, offset, move }, Is.Ordered.Ascending.And.Unique,
                "velocity test, guard, |Dot|, limit, distance, then the move");
            Assert.That(forward, Is.GreaterThan(move), "forward is read after the nudge");
            Assert.That(velocityForward, Is.GreaterThan(move), "velocity direction is read after the nudge");
            Assert.That(orientate, Is.GreaterThan(Math.Max(forward, velocityForward)));
        }

        // 060010f1 (RVA 0xE60CF0): the early air-active branch stores AirActive = false
        // (0xE60ECA) and tail-jumps to Character_EndAirActiveImpulse (0xE60EE1). The body
        // never ends the air-stomp impulse.
        [Test]
        public void HalfPipeEarlyAirSuppressionEndsOnlyTheAirActiveImpulse()
        {
            List<MemberInfo> refs = References(Method(typeof(CharacterAbility_HalfPipe<>), "DoOnUpdate"));
            MethodInfo endActive = Method(typeof(Character), "EndAirActiveImpulse");
            MethodInfo endStomp = Method(typeof(Character), "EndAirStompAttackImpulse");
            Assert.That(refs.Count(m => Same(m, endStomp)), Is.EqualTo(0), "the stomp impulse is not ended here");
            Assert.That(refs.Count(m => Same(m, endActive)), Is.EqualTo(1));
            int lastStore = refs.FindLastIndex(m => m.Name == "set_AirActive");
            Assert.That(lastStore, Is.GreaterThanOrEqualTo(0));
            Assert.That(refs.FindIndex(m => Same(m, endActive)), Is.EqualTo(refs.Count - 1).And.GreaterThan(lastStore),
                "ending the air-active impulse follows the AirActive store and is the final call");
        }

        // 0600129e (RVA 0xE692B0): one sqrtss gives the distance (0xE6938C). The divps/divss by it
        // follow directly (0xE69394-0xE69397), with no compare before them. Unity's Vector3.normalized
        // returns zero at magnitude <= 1e-5, so it would add a compare that the original lacks.
        // The distance is passed as maxDistance to the 5-argument Raycast (0xE693A4).
        [Test]
        public void TargetedLocationRaycastDividesByOneMagnitudeWithoutNormalizing()
        {
            List<MemberInfo> refs = References(Method(typeof(CharacterAbility_Targeting<>), "TargetedLocationIsValid"));
            MethodInfo magnitude = Getter(typeof(Vector3), "magnitude");
            MethodInfo division = typeof(Vector3).GetMethod("op_Division", new[] { typeof(Vector3), typeof(float) });
            MethodInfo raycast = typeof(Physics).GetMethod("Raycast",
                new[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(int), typeof(QueryTriggerInteraction) });
            Assert.That(refs.Count(m => Same(m, Getter(typeof(Vector3), "normalized"))), Is.EqualTo(0), "no epsilon normalization");
            Assert.That(refs.Count(m => Same(m, magnitude)), Is.EqualTo(1), "one square root");
            Assert.That(refs.Count(m => Same(m, division)), Is.EqualTo(1));
            Assert.That(refs.Count(m => Same(m, raycast)), Is.EqualTo(1));
            Assert.That(new[] { Index(refs, m => Same(m, magnitude)), Index(refs, m => Same(m, division)), Index(refs, m => Same(m, raycast)) },
                Is.Ordered.Ascending.And.Unique);
        }

        private static readonly Dictionary<short, OpCode> OpCodesByValue = BuildOpCodes();

        private static Dictionary<short, OpCode> BuildOpCodes()
        {
            var table = new Dictionary<short, OpCode>();
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var op = (OpCode)field.GetValue(null);
                table[op.Value] = op;
            }
            return table;
        }

        // Every method and field operand in IL order, resolved in the declaring type's generic context.
        private static List<MemberInfo> References(MethodBase method)
        {
            MethodBody body = method.GetMethodBody();
            Assert.That(body, Is.Not.Null, method.Name + " has an IL body");
            byte[] il = body.GetILAsByteArray();
            Type[] typeArguments = method.DeclaringType.IsGenericTypeDefinition ? method.DeclaringType.GetGenericArguments() : null;
            var result = new List<MemberInfo>();
            for (int i = 0; i < il.Length;)
            {
                short value = il[i] == 0xFE ? (short)(0xFE00 | il[i + 1]) : il[i];
                Assert.That(OpCodesByValue.TryGetValue(value, out OpCode op), Is.True, "known opcode at IL_" + i.ToString("x4"));
                i += op.Size;
                switch (op.OperandType)
                {
                    case OperandType.InlineMethod:
                    case OperandType.InlineField:
                        result.Add(method.Module.ResolveMember(BitConverter.ToInt32(il, i), typeArguments, null));
                        i += 4;
                        break;
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar: i += 1; break;
                    case OperandType.InlineVar: i += 2; break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR: i += 8; break;
                    case OperandType.InlineSwitch: i += 4 + 4 * BitConverter.ToInt32(il, i); break;
                    default: i += 4; break;
                }
            }
            return result;
        }

        private static bool Same(MemberInfo actual, MemberInfo expected)
        {
            Assert.That(expected, Is.Not.Null, "expected member exists");
            return actual.Module == expected.Module && actual.MetadataToken == expected.MetadataToken;
        }

        private static int Index(List<MemberInfo> refs, Predicate<MemberInfo> match)
        {
            int index = refs.FindIndex(match);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "expected member reference is present");
            return index;
        }

        private static MethodInfo Method(Type owner, string name) => owner.GetMethods(Declared).Single(m => m.Name == name);
        private static FieldInfo Field(Type owner, string name) => owner.GetField(name, Declared);
        private static MethodInfo Getter(Type owner, string name) => owner.GetProperty(name, Declared).GetGetMethod(true);
    }
}
