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
    // Static checks on Game.Runtime bodies, plus one value check on an engine operator a gate
    // binds to. Running the bodies needs a fully started Character (brain from OnStart, App, FSM,
    // physics), which a test cannot build without fakes.
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

        // 06001510 (RVA 0x773590): the blend-gravity refresh is gated by Unity's inlined Vector3 !=.
        // The squared difference is compared with 9.99999944E-11f (0x2edbe6fe at VA 0x29a6e8c) by
        // ucomiss/ja (0x773941-0x77394C). The engine operator's values are checked separately by
        // EngineVectorInequalityMatchesTheNativeAirGravityGate. The refresh then calls EnableGravity
        // (0x773963), OrientateToGravity (0x773992) and UpdateCachedValues (0x7739A6) in that order.
        [Test]
        public void AirAbilityUpdateRefreshesGravityThroughVectorInequality()
        {
            List<MemberInfo> refs = References(Method(typeof(CharacterState_AirAbility), "DoUpdate"));
            MethodInfo inequality = typeof(Vector3).GetMethod("op_Inequality", new[] { typeof(Vector3), typeof(Vector3) });
            MethodInfo sqrMagnitude = Getter(typeof(Vector3), "sqrMagnitude");
            int normalised = Index(refs, m => Same(m, Getter(typeof(Vector3), "normalized")));
            int compare = Index(refs, m => Same(m, inequality));
            int enable = Index(refs, m => m.Name == "EnableGravity");
            int orientate = Index(refs, m => m.Name == "OrientateToGravity");
            int cache = Index(refs, m => m.Name == "UpdateCachedValues");
            Assert.That(refs.Count(m => Same(m, inequality)), Is.EqualTo(1));
            Assert.That(new[] { normalised, compare, enable, orientate, cache }, Is.Ordered.Ascending.And.Unique,
                "gravity direction, the != gate, then EnableGravity, OrientateToGravity and UpdateCachedValues");
            Assert.That(refs.Take(enable).Count(m => Same(m, sqrMagnitude)), Is.EqualTo(0),
                "no hand-written sqrMagnitude threshold before the refresh");
        }

        // Value check on the engine operator the DoUpdate gate binds to. Native 0x773941-0x77394C skips
        // the refresh only when 9.99999944E-11f (0x2edbe6fe) is greater than the squared difference
        // (ucomiss/ja is false when unordered), so a difference of exactly that value refreshes, a NaN
        // difference refreshes, and anything below it does not.
        [Test]
        public void EngineVectorInequalityMatchesTheNativeAirGravityGate()
        {
            const int nativeThresholdBits = 0x2edbe6fe;
            float epsilon = 1e-5f;
            var atThreshold = new Vector3(epsilon, 1f, 0f);
            Assert.That(Bits((atThreshold - Vector3.up).sqrMagnitude), Is.EqualTo(nativeThresholdBits),
                "the witness difference squares to the native threshold");
            Assert.That(atThreshold != Vector3.up, Is.True, "a squared difference equal to the threshold refreshes");

            // One float below 1e-5f squares to 0x2edbe6fb, below the threshold.
            float belowEpsilon = BitConverter.ToSingle(BitConverter.GetBytes(Bits(epsilon) - 1), 0);
            var belowThreshold = new Vector3(belowEpsilon, 1f, 0f);
            Assert.That(Bits((belowThreshold - Vector3.up).sqrMagnitude), Is.EqualTo(0x2edbe6fb));
            Assert.That(belowThreshold != Vector3.up, Is.False, "a squared difference below the threshold skips");

            Assert.That(new Vector3(float.NaN, 1f, 0f) != Vector3.up, Is.True, "an unordered NaN compare refreshes");
            Assert.That(Vector3.up != Vector3.up, Is.False, "identical vectors skip");
            Assert.That(Bits(1e-10f), Is.EqualTo(nativeThresholdBits + 1), "the literal 1e-10f is one ULP above the native threshold");
        }

        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);

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

        // 06001038 (RVA 0xE5D960): loads this.m_character, calls Character.get_SaveDataSettings (0xE5D96A)
        // and returns cmp byte [rax+0x40], 0 / sete. In dump.cs +0x40 is m_airAbilityHold and
        // m_gameSpeedToggleEnabled is +0x64. The override reads neither Definition nor Input.
        [Test]
        public void ChaosControlContinuousCheckNegatesOnlyTheAirAbilityHoldSetting()
        {
            List<MemberInfo> refs = References(Method(typeof(CharacterAbility_AirChaos<>), "IsControlContinuous"));
            MethodInfo saveDataSettings = Getter(typeof(Character), "SaveDataSettings");
            MethodInfo airAbilityHold = Getter(typeof(SaveDataSettings), "AirAbilityHold");
            Assert.That(refs.Count(m => Same(m, airAbilityHold)), Is.EqualTo(1));
            Assert.That(refs.Count(m => Same(m, Getter(typeof(SaveDataSettings), "GameSpeedToggleEnabled"))), Is.EqualTo(0),
                "+0x40 is not the game speed toggle");
            Assert.That(refs.Count(m => m.Name == "get_Definition" || m.Name == "get_CharacterAbilityDefinition" || m.Name == "Input"),
                Is.EqualTo(0), "the definition input type is not consulted");
            Assert.That(Index(refs, m => Same(m, saveDataSettings)), Is.LessThan(Index(refs, m => Same(m, airAbilityHold))));
        }

        // 06001024 (RVA 0x73C560): get_Definition (0x73C592), then Input (+0xCC) compared with
        // Continuous (0x73C597). Only on a match does it call get_SaveDataSettings (0x73C5A9) and
        // return the negated +0x40 (m_airAbilityHold) byte; otherwise it returns false.
        [Test]
        public void AirContinuousCheckReadsTheDefinitionInputBeforeTheAirAbilityHoldSetting()
        {
            List<MemberInfo> refs = References(Method(typeof(CharacterAbility_Air), "IsControlContinuous"));
            MethodInfo definition = Getter(typeof(CharacterAbility<CharacterAbilityDefinition_Air>), "Definition");
            FieldInfo input = Field(typeof(CharacterAbilityDefinition_Air), "Input");
            MethodInfo saveDataSettings = Getter(typeof(Character), "SaveDataSettings");
            MethodInfo airAbilityHold = Getter(typeof(SaveDataSettings), "AirAbilityHold");
            Assert.That(refs.Count(m => Same(m, airAbilityHold)), Is.EqualTo(1));
            Assert.That(refs.Count(m => Same(m, Getter(typeof(SaveDataSettings), "GameSpeedToggleEnabled"))), Is.EqualTo(0));
            Assert.That(new[] { Index(refs, m => Same(m, definition)), Index(refs, m => Same(m, input)),
                    Index(refs, m => Same(m, saveDataSettings)), Index(refs, m => Same(m, airAbilityHold)) },
                Is.Ordered.Ascending.And.Unique);
        }

        // Method-group delegates, never invoked, for the calls and moves these tests claim, in
        // their expected order and ending with EndAirActiveImpulse. Each is ldftn or ldvirtftn
        // followed by a newobj of the delegate type.
        private static class DelegateOnly
        {
            public static object Sink;

            public static void Body(Actor actor, Character character)
            {
                Sink = new Func<Vector3, Vector3, float>(Vector3.Dot);
                Sink = new Func<float, float>(Mathf.Abs);
                Sink = new Action<Vector3>(actor.SetWorldPosition);
                Sink = new Func<Vector3, Vector3, float, int, QueryTriggerInteraction, bool>(Physics.Raycast);
                Sink = new Action(character.EndAirActiveImpulse);
            }
        }

        // A body that only loads the targets must give no SetWorldPosition move, no Raycast and no
        // EndAirActiveImpulse call, so the one-move, one-raycast and final-call claims above fail on it.
        [Test]
        public void DelegateOnlyBodyIsNotACallOrMoveWitness()
        {
            MethodInfo body = typeof(DelegateOnly).GetMethod(nameof(DelegateOnly.Body));
            MethodInfo setPosition = typeof(Actor).GetMethod("SetWorldPosition", new[] { typeof(Vector3) });
            MethodInfo raycast = typeof(Physics).GetMethod("Raycast",
                new[] { typeof(Vector3), typeof(Vector3), typeof(float), typeof(int), typeof(QueryTriggerInteraction) });
            MethodInfo endActive = Method(typeof(Character), "EndAirActiveImpulse");
            Predicate<MemberInfo> isSetPosition = m => m is MethodInfo mi && Same(mi.GetBaseDefinition(), setPosition);

            List<KeyValuePair<OpCode, MemberInfo>> operands = Operands(body);
            List<KeyValuePair<OpCode, MemberInfo>> loads = operands
                .Where(operand => operand.Key == OpCodes.Ldftn || operand.Key == OpCodes.Ldvirtftn).ToList();
            Assert.That(loads.Count, Is.EqualTo(5), "the fixture loads each target once");
            Assert.That(loads.Count(operand => isSetPosition(operand.Value)), Is.EqualTo(1));
            Assert.That(loads.Count(operand => Same(operand.Value, raycast)), Is.EqualTo(1));
            Assert.That(Same(loads.Last().Value, endActive), Is.True, "EndAirActiveImpulse is the last loaded method");

            List<MemberInfo> refs = References(body);
            Assert.That(refs.Count(m => isSetPosition(m)), Is.EqualTo(0), "no move");
            Assert.That(refs.Count(m => Same(m, raycast)), Is.EqualTo(0), "no raycast");
            Assert.That(refs.Count(m => Same(m, endActive)), Is.EqualTo(0), "no EndAirActiveImpulse call");
            Assert.That(refs.OfType<MethodBase>().Select(m => m.Name), Is.All.EqualTo(".ctor"), "only the delegate constructors are calls");
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

        // Field operands and invoked methods in IL order. ldftn and ldvirtftn only load a method
        // pointer (ECMA-335 III.3.41, III.4.18), so a method-group delegate is not a call and is
        // left out; the call and move claims in these tests depend on that.
        private static List<MemberInfo> References(MethodBase method) =>
            Operands(method).Where(operand => operand.Value is FieldInfo || ProjectLucid.Tests.OriginalILCallSequence.IsInvocation(operand.Key))
                .Select(operand => operand.Value).ToList();

        // Every method and field operand in IL order with its opcode, resolved in the declaring
        // type's generic context.
        private static List<KeyValuePair<OpCode, MemberInfo>> Operands(MethodBase method)
        {
            MethodBody body = method.GetMethodBody();
            Assert.That(body, Is.Not.Null, method.Name + " has an IL body");
            byte[] il = body.GetILAsByteArray();
            Type[] typeArguments = method.DeclaringType.IsGenericTypeDefinition ? method.DeclaringType.GetGenericArguments() : null;
            var result = new List<KeyValuePair<OpCode, MemberInfo>>();
            for (int i = 0; i < il.Length;)
            {
                short value = il[i] == 0xFE ? (short)(0xFE00 | il[i + 1]) : il[i];
                Assert.That(OpCodesByValue.TryGetValue(value, out OpCode op), Is.True, "known opcode at IL_" + i.ToString("x4"));
                i += op.Size;
                switch (op.OperandType)
                {
                    case OperandType.InlineMethod:
                    case OperandType.InlineField:
                        result.Add(new KeyValuePair<OpCode, MemberInfo>(op, method.Module.ResolveMember(BitConverter.ToInt32(il, i), typeArguments, null)));
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
