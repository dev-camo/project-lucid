using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // Hashed enum identifiers passed or compared by three Game.Runtime bodies. Running the bodies
    // themselves needs a started Character (brain from OnStart, registered abilities) or a
    // registered AudioManager, which this EditMode fixture does not build, so their compiled IL is
    // read instead; executing them stays with the Character and audio PlayMode gates. Expected
    // values are the 1.10.1 x86-64 immediates cited per test; the enum member names are the
    // dump.cs declarations at the cited lines. The CharacterActionFlags lookup the rolling
    // argument feeds runs on its own, so that boundary is exercised directly.
    public sealed class OriginalHashedIdentifierArgumentTests
    {
        private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic;

        // 060011c2 (RVA 0x74B120): after the base DoOnEnter, 0x74B15C loads
        // movl $0xf1c70db0 as the action for Character_GetBrainAppliedState (0x74B163).
        // dump.cs:554466 declares it as GameAction.CharacterRoll = -238613072.
        [Test]
        public void RollingEntryQueriesTheCharacterRollAction()
        {
            MethodInfo body = typeof(CharacterAbility_Rolling).GetMethod("DoOnEnter", Declared, null, Type.EmptyTypes, null);
            int action = ImmediateFor(body, m => m.DeclaringType == typeof(Character) && m.Name == "GetBrainAppliedState");
            Assert.That(action, Is.EqualTo(unchecked((int)0xf1c70db0)));
            Assert.That(Enum.GetName(typeof(GameAction), action), Is.EqualTo("CharacterRoll"));
        }

        // 06001764 (RVA 0x7925F0): 0x79264C loads movl $0xbae93dc5 as the key for
        // Actor_TryGetAbility<CharacterAbility_Knockback> (0x792651). A hit then reads
        // m_isKnockedBack (+0xB8, 0x79265E) through the inlined IsTriggered getter (06001111), so
        // the managed get_IsTriggered call is the matching source form, then calls
        // MinDurationExceeded (0x792669) and returns its result (0x792674).
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

        // 06000635 (RVA 0x53BDC0): GetMixer receives movl $0x7642df2f (0x53BE22), declared as
        // HLAudioMixerIdentifier.Main = 1984093999 (dump.cs:555800). The source at
        // +0x24 is compared with cmpl $0xadd78741 (0x53BE31, je to SetSfxVolume 0x53E130), then with
        // cmpl $0x83abe32d (0x53BE39, equal falls through to SetAmbientVolume 0x53E370); anything
        // else reaches SetMusicVolume (0x53E0A0). dump.cs:555834 declares Sfx = -1378384063.
        [Test]
        public void AudioSourceEntryRoutesSfxThenAmbientAndOtherwiseMusic()
        {
            MethodInfo body = typeof(ApplicationStateAdjustAudioSource).GetMethod("DoOnEnter", Declared);
            int mixer = ImmediateFor(body, m => m.DeclaringType == typeof(AudioManager) && m.Name == "GetMixer");
            Assert.That(mixer, Is.EqualTo(0x7642df2f));
            Assert.That(Enum.GetName(typeof(HLAudioMixerIdentifier), mixer), Is.EqualTo("Main"));

            List<KeyValuePair<OpCode, object>> instructions = OriginalMethodBodyReader.Instructions(body);
            var route = new List<string>();
            for (int i = 0; i < instructions.Count; i++)
            {
                object operand = instructions[i].Value;
                if (instructions[i].Key == OpCodes.Ldc_I4 && operand is int value && value != 0x7642df2f)
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

        // 060012d8 GetValue (RVA 0x755CD0) looks the action up with Dictionary.TryGetValue
        // (0x755D4B) in the mapping Initialise (060012d6, RVA 0x755980) fills from
        // Enum.GetValues(GameAction); a miss constructs and raises ArgumentOutOfRangeException
        // (0x755DC1, 0x755DD8). The rolling query's value is accepted; the undeclared value the
        // body passed before (0xf1c711b0) is not.
        [Test]
        public void CharacterActionFlagsAcceptsCharacterRollAndRejectsAnUndeclaredAction()
        {
            const BindingFlags own = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo indices = typeof(CharacterActionFlags).GetField("m_actionIndices", own);
            FieldInfo count = typeof(CharacterActionFlags).GetField("<ActionCount>k__BackingField", own);
            var map = (Dictionary<GameAction, int>)typeof(CharacterActionFlags).GetField("m_actionMapping", own).GetValue(null);
            object oldIndices = indices.GetValue(null), oldCount = count.GetValue(null);
            var oldMap = new Dictionary<GameAction, int>(map);
            try
            {
                CharacterActionFlags.Initialise();
                var flags = new CharacterActionFlags();
                Assert.That(flags.GetValue(GameAction.CharacterRoll), Is.False);
                flags.SetValue(GameAction.CharacterRoll, true);
                Assert.That(flags.GetValue(GameAction.CharacterRoll), Is.True);

                var undeclared = (GameAction)unchecked((int)0xf1c711b0);
                Assert.That(Enum.IsDefined(typeof(GameAction), undeclared), Is.False);
                Assert.Throws<ArgumentOutOfRangeException>(() => flags.GetValue(undeclared));
            }
            finally
            {
                map.Clear();
                foreach (KeyValuePair<GameAction, int> row in oldMap) map.Add(row.Key, row.Value);
                indices.SetValue(null, oldIndices);
                count.SetValue(null, oldCount);
            }
        }

        private static IEnumerable<MethodBase> Calls(MethodBase body) =>
            OriginalMethodBodyReader.Instructions(body).Select(instruction => instruction.Value).OfType<MethodBase>();

        // The last ldc.i4 immediate before the single matching call, with no other call between.
        // Only 32-bit ldc.i4 operands are boxed by the reader, so ldc.i4.s and ldc.i4.N short
        // forms are not seen; every hashed identifier checked here needs the 32-bit form.
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
                if (instructions[i].Key == OpCodes.Ldc_I4 && operand is int value) return value;
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
