using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // The original declares no concrete CharacterAbility_HalfPipe<T> or definition subclass,
    // so these test-only probes close the generic. They override only what the abstract bases require.
    public sealed class HalfPipeLeanProbeDefinition : CharacterAbilityDefinition_HalfPipe
    {
        // Required by AbilityDefinition; the fixture never calls CreateAbility.
        public override Type ScriptType => typeof(HalfPipeLeanProbe);
    }

    internal sealed class HalfPipeLeanProbe : CharacterAbility_HalfPipe<HalfPipeLeanProbeDefinition>
    {
    }

    // Truth table for CharacterAbility_HalfPipe<T>.LeanValue (060010f5), with expected
    // values read from the 1.10.1 x86-64 shared body at 0xE61290..0xE61370, not from the
    // maintained formula. Evidence is x86-64 only; no ARM or cross-platform parity is claimed.
    //
    // With m_leanStart (+0xC4) equal to zero or unordered (0xE612AD ucomiss / 0xE612B4 je, no
    // jp, so ZF from a NaN compare also takes the branch), the body loads
    // ExitLeanDeadzone (+0xCC) into xmm3 and |input| into xmm4, then 0xE61333 ucomiss xmm4,xmm3
    // and 0xE61339 jae return the zeroed xmm0 only for an ordered deadzone >= |input|. An
    // unordered compare sets CF and falls through to (|input| - deadzone), negated when
    // !(0 <= input) (0xE61349 cmpnless), minus deadzone (0xE6135B), so NaN propagates.
    // The two NaN cases separate this from an ordered |input| > deadzone gate, which returns 0,
    // and the NaN m_leanStart case separates the je gate from an ordered m_leanStart == 0 test,
    // which would store the input and return 0.
    //
    // The Character comes from its real constructor through AddComponent; in EditMode its
    // Awake never runs. Only ControllerMovement (+0x2A8) and the definition are supplied.
    public sealed class OriginalHalfPipeLeanValueTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // 0.5 against 0.1: 0.5 - 0.1 = 0.4, sign kept, minus 0.1 gives about 0.3.
        [Test]
        public void PositiveInputAboveTheDeadzoneKeepsItsSignAndSubtractsTheDeadzoneTwice()
        {
            WithProbe((probe, character, definition) =>
                Assert.AreEqual(0.3f, Lean(probe, character, definition, 0.5f, 0.1f), 1e-6f));
        }

        // -0.5 against 0.1: !(0 <= input) selects -(0.4), minus 0.1 gives about -0.5.
        [Test]
        public void NegativeInputAboveTheDeadzoneNegatesBeforeSubtractingTheDeadzone()
        {
            WithProbe((probe, character, definition) =>
                Assert.AreEqual(-0.5f, Lean(probe, character, definition, -0.5f, 0.1f), 1e-6f));
        }

        // |input| equal to the deadzone: ordered 0.1 >= 0.1 takes jae to the zero return.
        [Test]
        public void InputEqualToTheDeadzoneReturnsZero()
        {
            WithProbe((probe, character, definition) =>
                Assert.AreEqual(0f, Lean(probe, character, definition, 0.1f, 0.1f)));
        }

        [Test]
        public void ZeroInputInsideTheDeadzoneReturnsZero()
        {
            WithProbe((probe, character, definition) =>
                Assert.AreEqual(0f, Lean(probe, character, definition, 0f, 0.1f)));
        }

        // NaN input: unordered compare, no jae, the arithmetic returns NaN.
        [Test]
        public void NaNInputFallsThroughToTheArithmetic()
        {
            WithProbe((probe, character, definition) =>
                Assert.IsTrue(float.IsNaN(Lean(probe, character, definition, float.NaN, 0.1f)), "unordered input"));
        }

        // NaN deadzone: unordered compare, no jae, the arithmetic returns NaN.
        [Test]
        public void NaNDeadzoneFallsThroughToTheArithmetic()
        {
            WithProbe((probe, character, definition) =>
                Assert.IsTrue(float.IsNaN(Lean(probe, character, definition, 0.5f, float.NaN)), "unordered deadzone"));
        }

        // A finite non-zero m_leanStart takes the fall-through at 0xE612B6: it stores the
        // input at +0xC4 and returns zero without reading the deadzone.
        [Test]
        public void NonZeroLeanStartStoresTheInputAndReturnsZero()
        {
            WithProbe((probe, character, definition) =>
            {
                LeanStart.SetValue(probe, 0.25f);
                Assert.AreEqual(0f, Lean(probe, character, definition, 0.5f, 0.1f, resetLeanStart: false));
                Assert.AreEqual(0.5f, (float)LeanStart.GetValue(probe));
            });
        }

        // NaN m_leanStart: the unordered ucomiss sets ZF, je at 0xE612B4 is taken, so 0.5 against
        // 0.1 gives about 0.3 and the store at 0xE612B6 is skipped, leaving m_leanStart NaN.
        [Test]
        public void NaNLeanStartTakesTheDeadzonePathAndIsNotOverwritten()
        {
            WithProbe((probe, character, definition) =>
            {
                LeanStart.SetValue(probe, float.NaN);
                Assert.AreEqual(0.3f, Lean(probe, character, definition, 0.5f, 0.1f, resetLeanStart: false), 1e-6f);
                Assert.IsTrue(float.IsNaN((float)LeanStart.GetValue(probe)), "m_leanStart is not overwritten");
            });
        }

        private static readonly FieldInfo LeanStart =
            typeof(CharacterAbility_HalfPipe<HalfPipeLeanProbeDefinition>).GetField("m_leanStart", Instance);

        private static float Lean(HalfPipeLeanProbe probe, Character character, HalfPipeLeanProbeDefinition definition,
            float input, float deadzone, bool resetLeanStart = true)
        {
            if (resetLeanStart) LeanStart.SetValue(probe, 0f);
            definition.ExitLeanDeadzone = deadzone;
            SetProperty(character, nameof(Character.ControllerMovement), new Vector2(input, 0f));
            float result = (float)typeof(CharacterAbility_HalfPipe<HalfPipeLeanProbeDefinition>)
                .GetMethod("LeanValue", Instance).Invoke(probe, null);
            // The zero-start path never stores the input.
            if (resetLeanStart) Assert.AreEqual(0f, (float)LeanStart.GetValue(probe), "m_leanStart is unchanged");
            return result;
        }

        private static IDictionary Registry =>
            (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        // Character's field initializer creates the empty GameplayIslandManager row and
        // CharacterAbility's creates the empty App row. Both must be absent beforehand so the
        // fixture owns them; cleanup removes them only while they are still empty.
        private static void WithProbe(Action<HalfPipeLeanProbe, Character, HalfPipeLeanProbeDefinition> body)
        {
            IDictionary registry = Registry;
            var owned = new[] { ProcessManager.GetDefaultName<GameplayIslandManager>(), ProcessManager.GetDefaultName<App>() };
            foreach (string name in owned) VerificationFixtureOwnership.RequireAbsent(registry, name);
            var before = new HashSet<string>();
            foreach (string key in registry.Keys) before.Add(key);
            var gameObject = new GameObject("Lucid-halfpipe-lean-character");
            HalfPipeLeanProbeDefinition definition = null;
            Exception primary = null;
            try
            {
                Character character = gameObject.AddComponent<Character>();
                definition = ScriptableObject.CreateInstance<HalfPipeLeanProbeDefinition>();
                var probe = new HalfPipeLeanProbe();
                typeof(CharacterAbility).GetField("m_character", Instance).SetValue(probe, character);
                typeof(CharacterAbility<HalfPipeLeanProbeDefinition>).GetField("m_definition", Instance).SetValue(probe, definition);
                Assert.AreSame(definition, probe.Definition, "no override is set");
                body(probe, character, definition);
            }
            catch (Exception failure) { primary = failure; throw; }
            finally
            {
                // Unowned rows are reported on every path; they are never removed here.
                var unowned = new List<string>();
                foreach (string key in registry.Keys)
                    if (!before.Contains(key) && Array.IndexOf(owned, key) < 0) unowned.Add(key);
                if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(gameObject);
                // Every owned row is released even when an earlier one refuses.
                var cleanupFailures = new List<Exception>();
                foreach (string name in owned)
                {
                    if (!registry.Contains(name)) continue;
                    try { VerificationFixtureOwnership.ReleaseEmptyRow(registry, name, registry[name]); }
                    catch (Exception cleanup) { cleanupFailures.Add(cleanup); }
                }
                if (unowned.Count > 0)
                {
                    string message = "Fixture created unowned registry rows: " + string.Join(", ", unowned);
                    if (primary == null && cleanupFailures.Count == 0) Assert.Fail(message);
                    TestContext.Error.WriteLine(message);
                }
                if (cleanupFailures.Count > 0)
                {
                    var cleanupFailure = new AggregateException("HalfPipe lean fixture cleanup failed", cleanupFailures);
                    if (primary == null) throw cleanupFailure;
                    TestContext.Error.WriteLine("HalfPipe lean fixture cleanup failed after a body failure: " + cleanupFailure);
                }
            }
        }

        // Private setters are taken from the declaring type.
        private static void SetProperty(object target, string property, object value)
        {
            Type owner = target.GetType().GetProperty(property, Instance).DeclaringType;
            owner.GetProperty(property, Instance).GetSetMethod(true).Invoke(target, new[] { value });
        }
    }
}
