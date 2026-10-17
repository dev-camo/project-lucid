using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Hashed enum identifiers passed or compared by three Game.Runtime bodies. Running them needs
    // a started Character (brain from OnStart, registered abilities) or a registered AudioManager,
    // which a test cannot build without fakes, so the compiled IL is read instead. Expected values
    // are the 1.10.1 x86-64 immediates cited per test; the enum member names are the dump.cs
    // declarations at the cited lines.
    public sealed class OriginalHashedIdentifierArgumentTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic;

        // 060011c2 (RVA 0x74B120): after the base DoOnEnter, 0x74B15C loads
        // movl $0xf1c70db0 as the action for Character_GetBrainAppliedState (0x74B163).
        // dump.cs:554466 declares it as GameAction.CharacterRoll = -238613072. An undeclared
        // value misses CharacterActionFlags' mapping, so GetValue would throw.
        [Test]
        public void RollingEntryQueriesTheCharacterRollAction()
        {
            MethodInfo body = typeof(CharacterAbility_Rolling).GetMethod("DoOnEnter", Declared, null, Type.EmptyTypes, null);
            int action = ImmediateFor(body, m => m.DeclaringType == typeof(Character) && m.Name == "GetBrainAppliedState");
            Assert.That(action, Is.EqualTo(unchecked((int)0xf1c70db0)));
            Assert.That(Enum.GetName(typeof(GameAction), action), Is.EqualTo("CharacterRoll"));
        }

        // 06001764 (RVA 0x7925F0): 0x79264C loads movl $0xbae93dc5 as the key for
        // Actor_TryGetAbility<CharacterAbility_Knockback> (0x792651). A hit then reads the
        // triggered flag (+0xB8, 0x79265E) and tail-calls MinDurationExceeded (0x792669).
        // dump.cs:552741 declares the key as ActorAbilityType.Character_Knockback = -1159119419.
        [Test]
        public void KnockbackTransitionLooksUpTheCharacterKnockbackAbility()
        {
            MethodInfo body = typeof(CharacterTransition_KnockbackMinDurationExceeded).GetMethod("DoUpdate", Declared);
            Predicate<MethodBase> lookup = m => m.DeclaringType == typeof(Actor) && m.Name == "TryGetAbility";
            int key = ImmediateFor(body, lookup);
            Assert.That(key, Is.EqualTo(unchecked((int)0xbae93dc5)));
            Assert.That(Enum.GetName(typeof(ActorAbilityType), key), Is.EqualTo("Character_Knockback"));

            string[] calls = Calls(body).Select(m => m.Name).ToArray();
            Assert.That(calls.SkipWhile(name => name != "TryGetAbility").ToArray(),
                Is.EqualTo(new[] { "TryGetAbility", "get_IsTriggered", "MinDurationExceeded" }));
            MethodBase tryGet = Calls(body).Single(m => lookup(m));
            Assert.That(tryGet.GetGenericArguments(), Is.EqualTo(new[] { typeof(CharacterAbility_Knockback) }));
        }

        // 06000635 (RVA 0x53BDC0): GetMixer receives movl $0x7642df2f (0x53BE22). The source at
        // +0x24 is compared with cmpl $0xadd78741 (0x53BE31, je to SetSfxVolume 0x53E130), then with
        // cmpl $0x83abe32d (0x53BE39, equal falls through to SetAmbientVolume 0x53E370); anything
        // else reaches SetMusicVolume (0x53E0A0). dump.cs:555834 declares Sfx = -1378384063.
        [Test]
        public void AudioSourceEntryRoutesSfxThenAmbientAndOtherwiseMusic()
        {
            MethodInfo body = typeof(ApplicationStateAdjustAudioSource).GetMethod("DoOnEnter", Declared);
            int mixer = ImmediateFor(body, m => m.DeclaringType == typeof(AudioManager) && m.Name == "GetMixer");
            Assert.That(mixer, Is.EqualTo(0x7642df2f));

            List<KeyValuePair<OpCode, object>> instructions = OriginalMethodBodyReader.Instructions(body);
            var route = new List<string>();
            for (int i = 0; i < instructions.Count; i++)
            {
                object operand = instructions[i].Value;
                if (operand is int value && value != 0x7642df2f)
                {
                    Assert.That(IsEqualityGuard(instructions, i), Is.True, "0x" + value.ToString("x8") + " guards on equality");
                    route.Add("0x" + value.ToString("x8"));
                }
                else if (operand is MethodBase method && method.DeclaringType == typeof(HLAudioMixerDefinition))
                {
                    route.Add(method.Name);
                }
            }
            Assert.That(route, Is.EqualTo(new[] { "0xadd78741", "SetSfxVolume", "0x83abe32d", "SetAmbientVolume", "SetMusicVolume" }));
            Assert.That(Enum.GetName(typeof(HLAudioSourceIdentifier), unchecked((int)0xadd78741)), Is.EqualTo("Sfx"));
            Assert.That(Enum.GetName(typeof(HLAudioSourceIdentifier), unchecked((int)0x83abe32d)), Is.EqualTo("Ambient"));
        }

        private static IEnumerable<MethodBase> Calls(MethodBase body) =>
            OriginalMethodBodyReader.Instructions(body).Select(instruction => instruction.Value).OfType<MethodBase>();

        // The last ldc.i4 immediate before the single matching call, with no other call between.
        private static int ImmediateFor(MethodBase body, Predicate<MethodBase> target)
        {
            Assert.That(body, Is.Not.Null, "body exists");
            List<KeyValuePair<OpCode, object>> instructions = OriginalMethodBodyReader.Instructions(body);
            int call = instructions.FindIndex(instruction => instruction.Value is MethodBase method && target(method));
            Assert.That(call, Is.GreaterThanOrEqualTo(0), "call is present");
            Assert.That(instructions.FindLastIndex(instruction => instruction.Value is MethodBase method && target(method)), Is.EqualTo(call),
                "call appears once");
            for (int i = call - 1; i >= 0; i--)
            {
                object operand = instructions[i].Value;
                if (operand is int value) return value;
                Assert.That(operand, Is.Not.InstanceOf<MethodBase>(), "no other call between the immediate and its call");
            }
            Assert.Fail("no immediate before the call");
            return 0;
        }

        // Release IL branches away with bne.un when unequal; Debug IL stores ceq and tests it
        // with brfalse. Either way the code after the branch runs only on equality.
        private static bool IsEqualityGuard(List<KeyValuePair<OpCode, object>> instructions, int constant)
        {
            int i = constant + 1;
            OpCode code = instructions[i].Key;
            if (code == OpCodes.Bne_Un || code == OpCodes.Bne_Un_S) return true;
            if (code != OpCodes.Ceq) return false;
            for (i++; i < instructions.Count; i++)
            {
                code = instructions[i].Key;
                if (code.Name.StartsWith("stloc", StringComparison.Ordinal) || code.Name.StartsWith("ldloc", StringComparison.Ordinal)) continue;
                return code == OpCodes.Brfalse || code == OpCodes.Brfalse_S;
            }
            return false;
        }
    }
}
