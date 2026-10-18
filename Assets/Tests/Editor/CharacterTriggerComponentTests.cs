using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.TriggerConditionTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CatchReleaseDefinition (TypeDef 0x020004a6, x86-64 0x540270..0x540470),
    // CharacterAnimationParameterTrigger (0x02000734, 0x5C9B20..0x5C9CDF), CharacterAutoRailTrigger
    // (0x02000735, 0x5C9CE0..0x5C9E0F), CharacterBoostTrigger (0x02000736, 0x5C9E10..0x5C9F42),
    // CharacterColliderSingleSided (0x02000741, 0x5CB840..0x5CB91F) and CharacterAnimationTrigger
    // (0x0200076b, 0x5D2CB0..0x5D2E22). Components are real, on owned hosts that never wake (the
    // Editor sends no Awake); a CharacterManager is built by its real constructor (OwnedCharacterManagers)
    // and registered into its absent row before a component caches its reference. A Character has no body
    // here, so only members that need none run.
    public sealed class CharacterTriggerComponentTests
    {
        private static T Add<T>(Owned owned, string name) where T : Component
        {
            GameObject host = owned.Host(name, false);
            // CharacterTrigger and CharacterColliderModifier require a Collider, which is abstract.
            host.AddComponent<BoxCollider>();
            return host.AddComponent<T>();
        }

        private static void AssertType(Type type, int typeFlags, Type baseType, int[] options,
            (string name, int flags)[] fields, (string name, int flags)[] methods)
        {
            AssertMembers(type, typeFlags, fields, methods);
            AssertIl2CppOptions(type, options);
            Assert.That(type.BaseType, Is.EqualTo(baseType), type.FullName);
        }

        private static string[] AttributeNames(MemberInfo member) =>
            member.GetCustomAttributesData().Select(data => data.AttributeType.Name).ToArray();

        // The declarations come from the original DummyDll metadata of each type.
        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsMethodsAndAttributes()
        {
            AssertType(typeof(CatchReleaseDefinition), 0x00102001, typeof(object), new[] { 2, 1 },
                new[]
                {
                    ("FormType", 0x0006), ("AnimationDefinition", 0x0006), ("TimeCategory", 0x0006),
                    ("Attach", 0x0006), ("Catch", 0x0006), ("Release", 0x0006)
                },
                new[] { ("CalculateCachedValues", 0x0086), (".ctor", 0x1886) });

            AssertType(typeof(CharacterAnimationParameterTrigger), 0x00100001, typeof(CharacterTrigger), new[] { 1, 2 },
                new[]
                {
                    ("m_onEnterTriggerParameter", 0x0001), ("m_onStayTriggerParameter", 0x0001),
                    ("m_onExitTriggerParameter", 0x0001), ("m_boolParameter", 0x0001)
                },
                new[]
                {
                    ("DoTriggerEnter", 0x00c4), ("DoTriggerStay", 0x00c4), ("DoTriggerExit", 0x00c4),
                    ("PlayTrigger", 0x0081), ("SetBool", 0x0081), (".ctor", 0x1886)
                });
            FieldInfo boolParameter = Field(typeof(CharacterAnimationParameterTrigger), "m_boolParameter");
            Assert.That(AttributeTypes(boolParameter), Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(Tooltip(boolParameter), Is.EqualTo("Set true on enter and false on exit."));

            AssertType(typeof(CharacterAnimationTrigger), 0x00100001, typeof(ActorAnimationTrigger<Character>), new[] { 1, 2 },
                new[] { ("m_currentCharacterOnly", 0x0001), ("m_characterManagerRef", 0x0021) },
                new[] { ("FilterActor", 0x00c4), (".ctor", 0x1886) });
            Assert.That(AttributeTypes(Field(typeof(CharacterAnimationTrigger), "m_currentCharacterOnly")),
                Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));

            AssertType(typeof(CharacterAutoRailTrigger), 0x00100001, typeof(CharacterTrigger), new[] { 1, 2 },
                new[] { ("m_overrideHandle", 0x0001) },
                new[] { ("DoTriggerEnter", 0x00c4), ("DoTriggerExit", 0x00c4), ("RemoveOverride", 0x0081), (".ctor", 0x1886) });

            AssertType(typeof(CharacterBoostTrigger), 0x00100001, typeof(CharacterTrigger), new[] { 1, 2 },
                new[] { ("m_triggerType", 0x0001), ("m_continuous", 0x0001), ("m_cooldownTime", 0x0001) },
                new[] { ("DoTriggerEnter", 0x00c4), ("DoTriggerStay", 0x00c4), ("DoTrigger", 0x0081), (".ctor", 0x1886) });
            Type triggerType = typeof(CharacterBoostTrigger).GetNestedType("TriggerType", BindingFlags.NonPublic);
            AssertMembers(triggerType, 0x00000103, new[] { ("value__", 0x0606), ("Reset", 0x8056), ("Cancel", 0x8056) },
                new (string, int)[0]);
            Assert.That(AttributeTypes(Field(typeof(CharacterBoostTrigger), "m_triggerType")),
                Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(AttributeTypes(Field(typeof(CharacterBoostTrigger), "m_continuous")),
                Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            FieldInfo cooldown = Field(typeof(CharacterBoostTrigger), "m_cooldownTime");
            Assert.That(AttributeTypes(cooldown),
                Is.EqualTo(new[] { typeof(MinAttribute), typeof(ShowIfAttribute), typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(Tooltip(cooldown), Is.EqualTo("Period of time that boost is disabled."));

            AssertType(typeof(CharacterColliderSingleSided), 0x00100001, typeof(CharacterColliderModifier), new[] { 1, 2 },
                new (string, int)[0],
                new[] { ("DoEvaluateConditions", 0x00c6), (".ctor", 0x1886) });
            Assert.That(AttributeNames(typeof(CharacterColliderSingleSided)),
                Is.EqualTo(new[] { "Il2CppSetOptionAttribute", "Il2CppSetOptionAttribute", "RequireComponent" }));
        }

        // ---- CatchReleaseDefinition --------------------------------------------------------------

        // The constructor 0x5402F0 writes None (0xb6fa1284) to FormType and Environment (0x34dab335) to
        // TimeCategory, leaves AnimationDefinition null and builds the three parts, whose own
        // constructors it inlines: Attach (+0x11, +0x14, +0x19, +0x44, curve 0..100), Catch and Release
        // (+0x10 = 0xe2bf0ae1, return speed 0x41200000).
        [Test]
        public void ConstructorWritesTheNativeDefaultsAndBuildsTheThreeParts()
        {
            var definition = new CatchReleaseDefinition();

            Assert.That((int)definition.FormType, Is.EqualTo(unchecked((int)0xb6fa1284)));
            Assert.That((int)definition.TimeCategory, Is.EqualTo(0x34dab335));
            Assert.That(definition.AnimationDefinition, Is.Null);
            Assert.That(definition.Attach, Is.Not.Null);
            Assert.That(definition.Catch, Is.Not.Null);
            Assert.That(definition.Release, Is.Not.Null);
            Assert.That(definition.Attach.InputRequiredIsContinuous, Is.True);
            Assert.That((int)definition.Attach.InputGameAction, Is.EqualTo(0x36a31b5c));
            Assert.That(definition.Attach.OrientateFromVelocity, Is.True);
            Assert.That(definition.Attach.OffsetToTopOfCharacter, Is.True);
            Assert.That(definition.Attach.SpeedCurve.Evaluate(50f), Is.EqualTo(50f).Within(1e-4f));
            Assert.That((int)definition.Release.InputType, Is.EqualTo(unchecked((int)0xe2bf0ae1)));
            Assert.That(definition.Release.ReturnSpeed, Is.EqualTo(BitConverter.Int32BitsToSingle(0x41200000)));
            Assert.That(definition.Release.SpeedCurve.Evaluate(50f), Is.EqualTo(50f).Within(1e-4f));
            Assert.That(new CatchReleaseDefinition().Attach, Is.Not.SameAs(definition.Attach), "each definition builds its own parts");
        }

        // CalculateCachedValues 0x540270 multiplies the Attach alignment tolerance by the single-precision
        // degrees-to-radians constant (0x3c8efa35, 0x540277) and stores cosf in Attach+0x48
        // (AlignmentCosAngleTolerance), and nothing else. Expected bits are cosf of the single-precision
        // product (computed as float32 and rounded once).
        [Test]
        public void CachedValuesStoreTheCosineOfTheAlignmentToleranceInSinglePrecision()
        {
            var definition = new CatchReleaseDefinition();
            (float degrees, int bits)[] table =
            {
                (0f, 0x3F800000),     // cos 0
                (30f, 0x3F5DB3D7),    // 0.8660254
                (60f, 0x3EFFFFFF),    // 0.49999997: the product rounds just below pi/3
                (90f, unchecked((int)0xB33BBD2E)),   // -4.371139e-08: pi/2 is not exact in single precision
                (180f, unchecked((int)0xBF800000)),  // cos pi
            };
            foreach ((float degrees, int bits) in table)
            {
                definition.Attach.AlignmentAngleTolerance = degrees;
                definition.CalculateCachedValues();
                Assert.That(BitConverter.SingleToInt32Bits(definition.Attach.AlignmentCosAngleTolerance), Is.EqualTo(bits), degrees + " degrees");
            }
        }

        // ---- CharacterAnimationParameterTrigger --------------------------------------------------

        private static CharacterAnimationParameterTrigger NewParameterTrigger(Owned owned)
        {
            var trigger = Add<CharacterAnimationParameterTrigger>(owned, "Lucid animation parameter trigger");
            Set(trigger, "m_onEnterTriggerParameter", Parameter("EnterTrigger"));
            Set(trigger, "m_onStayTriggerParameter", Parameter("StayTrigger"));
            Set(trigger, "m_onExitTriggerParameter", Parameter("ExitTrigger"));
            Set(trigger, "m_boolParameter", Parameter("Inside"));
            return trigger;
        }

        private static readonly string[] ParameterTriggers = { "EnterTrigger", "StayTrigger", "ExitTrigger" };

        private static bool[] TriggerStates(Animator animator) => ParameterTriggers.Select(animator.GetBool).ToArray();

        // DoTriggerEnter 0x5C9B20 sets the enter trigger (SetAnimationTrigger 0x5C9B54) and the bool true
        // (0x5C9B7A, edx=1); DoTriggerStay 0x5C9C20 sets only the stay trigger; DoTriggerExit 0x5C9C60 sets
        // the exit trigger and the bool false (0x5C9CBC, edx=0). None of them touches another parameter.
        [Test]
        public void EachCallbackSetsOnlyItsOwnTriggerAndTheBoolFollowsEnterAndExit()
        {
            WithCharacter((owned, character) =>
            {
                Animator animator = AttachAnimator(owned, character, ParameterTriggers, new[] { "Inside" });
                CharacterAnimationParameterTrigger trigger = NewParameterTrigger(owned);

                Call(trigger, "DoTriggerEnter", character);
                Assert.That(TriggerStates(animator), Is.EqualTo(new[] { true, false, false }), "enter");
                Assert.That(animator.GetBool("Inside"), Is.True);

                animator.ResetTrigger("EnterTrigger");
                animator.SetBool("Inside", false);
                Call(trigger, "DoTriggerStay", character);
                Assert.That(TriggerStates(animator), Is.EqualTo(new[] { false, true, false }), "stay");
                Assert.That(animator.GetBool("Inside"), Is.False, "stay leaves the bool alone");

                animator.ResetTrigger("StayTrigger");
                animator.SetBool("Inside", true);
                Call(trigger, "DoTriggerExit", character);
                Assert.That(TriggerStates(animator), Is.EqualTo(new[] { false, false, true }), "exit");
                Assert.That(animator.GetBool("Inside"), Is.False, "exit writes false");
            });
        }

        // TryGetAnimationHash (0x6A6700) fails for an empty name, and each use is guarded by its result
        // (test al,al; je at 0x5C9B47 and 0x5C9B6F), so unnamed parameters change nothing.
        [Test]
        public void UnnamedParametersAreSkipped()
        {
            WithCharacter((owned, character) =>
            {
                Animator animator = AttachAnimator(owned, character, ParameterTriggers, new[] { "Inside" });
                CharacterAnimationParameterTrigger trigger = NewParameterTrigger(owned);
                Set(trigger, "m_onEnterTriggerParameter", Parameter(""));
                Set(trigger, "m_boolParameter", Parameter(null));
                animator.SetBool("Inside", true);

                Call(trigger, "DoTriggerEnter", character);
                Assert.That(TriggerStates(animator), Is.EqualTo(new[] { false, false, false }));
                Assert.That(animator.GetBool("Inside"), Is.True, "an unnamed bool parameter is not written");

                Call(trigger, "DoTriggerExit", character);
                Assert.That(TriggerStates(animator), Is.EqualTo(new[] { false, false, true }), "the named exit trigger still fires");
                Assert.That(animator.GetBool("Inside"), Is.True);
            });
        }

        // ---- CharacterAnimationTrigger -----------------------------------------------------------

        // FilterActor 0x5D2CB0: with m_currentCharacterOnly clear (+0x41, 0x5d2cfd) the actor passes; otherwise
        // the CharacterManager reference must be valid (IsValid, 0x5d2d12), the manager must report a current
        // character (TryGetCurrentCharacter, 0x5d2d3a, whose flag alone decides) and the actor must be it
        // (Object.op_Equality, 0x5d2d67).
        [Test]
        public void FilterPassesEveryoneUnlessOnlyTheCurrentCharacterIsWanted()
        {
            WithManager((owned, manager, first) =>
            {
                Character second = NewCharacter(owned, new FSMStorage(0));
                var trigger = Add<CharacterAnimationTrigger>(owned, "Lucid animation trigger");

                Assert.That(Call(trigger, "FilterActor", first), Is.True, "filter off: any actor");
                Assert.That(Call(trigger, "FilterActor", second), Is.True);

                Set(trigger, "m_currentCharacterOnly", true);
                SetCurrent(manager, first, false);
                Assert.That(Call(trigger, "FilterActor", first), Is.False, "no current character");
                SetCurrent(manager, first, true);
                Assert.That(Call(trigger, "FilterActor", first), Is.True, "the current character");
                Assert.That(Call(trigger, "FilterActor", second), Is.False, "another character");
                SetCurrent(manager, second, true);
                Assert.That(Call(trigger, "FilterActor", first), Is.False);
                Assert.That(Call(trigger, "FilterActor", second), Is.True);
            });
        }

        // The IsValid test at 0x5d2d12 comes first: a reference to a row that holds no CharacterManager
        // rejects the actor without a lookup (je 0x5d2d76 returns false).
        [Test]
        public void FilterRejectsEveryActorWhileNoCharacterManagerIsRegistered()
        {
            WithCharacter((owned, character) =>
            {
                owned.LeaseRow<CharacterManager>();
                var trigger = Add<CharacterAnimationTrigger>(owned, "Lucid animation trigger without manager");
                Assert.That(Call(trigger, "FilterActor", character), Is.True, "filter off: any actor");
                Set(trigger, "m_currentCharacterOnly", true);
                Assert.That(Call(trigger, "FilterActor", character), Is.False);
            });
        }

        // ---- CharacterAutoRailTrigger ------------------------------------------------------------

        private static int AutoRail => (int)GameplayModifierType.AutoRail;

        // DoTriggerEnter 0x5C9CE0 first drops an override left by an earlier entry (0x5c9d28), then adds a
        // new true AutoRail override (AddModifierOverride<bool>, esi=0xd3678a5, edx=1, 0x5c9d55) and keeps its
        // handle. DoTriggerExit 0x5C9DB0 removes the held override and clears the handle (0x5c9dd6, 0x5c9ddb).
        [Test]
        public void AutoRailHoldsOneTrueOverrideWhileInsideAndReleasesItOnExit()
        {
            WithCharacter((owned, character) =>
            {
                var trigger = Add<CharacterAutoRailTrigger>(owned, "Lucid auto rail trigger");
                StackableData modifiers = Modifiers(character);
                Assert.That(modifiers.HasData(AutoRail), Is.False);

                Call(trigger, "DoTriggerEnter", character);
                StackableDataHandle first = Get<StackableDataHandle>(trigger, "m_overrideHandle");
                Assert.That(first, Is.Not.Null);
                Assert.That(modifiers.HasData(AutoRail), Is.True);
                Assert.That(modifiers.Get(AutoRail, true, false), Is.True, "the override value is true");
                Assert.That(modifiers.GetStack<bool>(AutoRail, new List<bool>()), Is.EqualTo(new[] { true }));

                Call(trigger, "DoTriggerEnter", character);
                StackableDataHandle second = Get<StackableDataHandle>(trigger, "m_overrideHandle");
                Assert.That(second, Is.Not.SameAs(first), "a new override replaces the held one");
                Assert.That(modifiers.GetStack<bool>(AutoRail, new List<bool>()).Count, Is.EqualTo(1), "the earlier override was removed");

                Call(trigger, "DoTriggerExit", character);
                Assert.That(Get<StackableDataHandle>(trigger, "m_overrideHandle"), Is.Null);
                Assert.That(modifiers.HasData(AutoRail), Is.False);

                Call(trigger, "DoTriggerExit", character);
                Assert.That(Get<StackableDataHandle>(trigger, "m_overrideHandle"), Is.Null, "leaving without an override changes nothing");
            });
        }

        // ---- CharacterBoostTrigger ---------------------------------------------------------------

        private static void WithBoostStamina(Action<Owned, Character, CharacterBoostStamina> body) =>
            WithManager((owned, manager, character) =>
            {
                // Built by its real constructor into its absent row before the stamina caches the reference.
                owned.Register(new CollectableManager());
                var stamina = new CharacterBoostStamina(character);
                typeof(Character).GetProperty("BoostStamina").GetSetMethod(true).Invoke(character, new object[] { stamina });
                try
                {
                    body(owned, character, stamina);
                }
                finally
                {
                    stamina.Close();
                }
            });

        private static readonly Type BoostTriggerType = typeof(CharacterBoostTrigger).GetNestedType("TriggerType", BindingFlags.NonPublic);

        // DoTrigger 0x5C9E40: type 0 (Reset) restores the boost stamina (CharacterStamina.Reset, jmp 0x5c9e6a
        // with esi=0, so no immediate reset) and leaves the cooldown alone; type 1 (Cancel) copies the
        // trigger's cooldown time (+0x54) into the stamina (0x5c9e7b) and leaves the stamina alone.
        [Test]
        public void ResetRestoresTheStaminaAndCancelStartsTheCooldown()
        {
            WithBoostStamina((owned, character, stamina) =>
            {
                var trigger = Add<CharacterBoostTrigger>(owned, "Lucid boost trigger");
                // Reset 0x76FB00 writes the full stamina, so the set-up drains it after the immediate reset.
                stamina.Reset(true);
                stamina.AddStamina(-0.75f);
                stamina.ActivateCooldown(3f);
                Assert.That(stamina.Stamina, Is.EqualTo(0.25f));
                Assert.That(stamina.ImmediateResetApplied, Is.True);

                Call(trigger, "DoTriggerEnter", character);
                Assert.That(stamina.Stamina, Is.EqualTo(1f), "reset restores the stamina");
                Assert.That(stamina.ImmediateResetApplied, Is.False, "without an immediate reset");
                Assert.That(Get<float>(stamina, "m_cooldownTime"), Is.EqualTo(3f), "reset leaves the cooldown");

                Set(trigger, "m_triggerType", Enum.ToObject(BoostTriggerType, 1));
                Set(trigger, "m_cooldownTime", 2.5f);
                stamina.AddStamina(-0.5f);
                Call(trigger, "DoTriggerEnter", character);
                Assert.That(Get<float>(stamina, "m_cooldownTime"), Is.EqualTo(2.5f), "cancel starts the trigger's cooldown");
                Assert.That(stamina.Stamina, Is.EqualTo(0.5f), "cancel leaves the stamina");
            });
        }

        // The cooldown time defaults to 1 (0x5C9F34, movss 0x3f800000); DoTriggerEnter always fires, while
        // DoTriggerStay 0x5C9F00 fires only when the continuous flag (+0x50, 0x5c9f14) is set.
        [Test]
        public void StayFiresOnlyWhenContinuousAndTheCooldownDefaultsToOne()
        {
            WithBoostStamina((owned, character, stamina) =>
            {
                var trigger = Add<CharacterBoostTrigger>(owned, "Lucid continuous boost trigger");
                Assert.That(Get<float>(trigger, "m_cooldownTime"), Is.EqualTo(1f));
                Set(trigger, "m_triggerType", Enum.ToObject(BoostTriggerType, 1));
                stamina.ActivateCooldown(0f);

                Call(trigger, "DoTriggerStay", character);
                Assert.That(Get<float>(stamina, "m_cooldownTime"), Is.EqualTo(0f), "not continuous");
                Set(trigger, "m_continuous", true);
                Call(trigger, "DoTriggerStay", character);
                Assert.That(Get<float>(stamina, "m_cooldownTime"), Is.EqualTo(1f), "continuous");

                stamina.ActivateCooldown(0f);
                Set(trigger, "m_continuous", false);
                Call(trigger, "DoTriggerEnter", character);
                Assert.That(Get<float>(stamina, "m_cooldownTime"), Is.EqualTo(1f), "enter ignores the flag");
            });
        }

        // The default of the type switch 0x5c9e89 raises ArgumentOutOfRangeException("TriggerType", value, null)
        // for a value that is neither 0 nor 1 (0x5c9ed8), before any use of the character.
        [Test]
        public void AnUndeclaredTriggerTypeRaisesArgumentOutOfRange()
        {
            WithCharacter((owned, character) =>
            {
                var trigger = Add<CharacterBoostTrigger>(owned, "Lucid invalid boost trigger");
                object invalid = Enum.ToObject(BoostTriggerType, 7);
                Set(trigger, "m_triggerType", invalid);

                var failure = Assert.Throws<ArgumentOutOfRangeException>(() => Call(trigger, "DoTrigger", new object[] { null }));
                Assert.That(failure.ParamName, Is.EqualTo("TriggerType"));
                Assert.That(failure.ActualValue, Is.EqualTo(invalid));
            });
        }

        // ---- CharacterColliderSingleSided --------------------------------------------------------

        // DoEvaluateConditions 0x5CB840: a character slower than 1e-4 (ucomiss 0x29a6dfc, ja 0x5cb855) always
        // passes; otherwise the heading must not point along the character's up axis: the dot of the up
        // direction (+0x98) and the normalised velocity (+0xd4) must be <= 0 (setae 0x5cb88c, xmm1 = 0 against
        // the dot), so zero passes and NaN does not. The z products (+0xa0 and +0xdc) are part of the dot.
        [Test]
        public void SingleSidedPassesAStationaryCharacterOrOneNotMovingAlongUp()
        {
            WithCharacter((owned, character) =>
            {
                owned.LeaseRow<CharacterManager>();
                var collider = Add<CharacterColliderSingleSided>(owned, "Lucid single sided collider");
                bool Evaluate(float speed, Vector3 up, Vector3 heading)
                {
                    SetActorProperty(character, "WorldVelocityMagnitude", speed);
                    SetActorProperty(character, "UpDirection", up);
                    SetActorProperty(character, "WorldVelocityNormalised", heading);
                    return collider.DoEvaluateConditions(character);
                }

                Assert.That(Evaluate(0f, Vector3.up, Vector3.up), Is.True, "stationary");
                Assert.That(Evaluate(0.00005f, Vector3.up, Vector3.up), Is.True, "slower than the threshold");
                Assert.That(Evaluate(0.0001f, Vector3.up, Vector3.up), Is.False, "exactly the threshold is evaluated");
                Assert.That(Evaluate(5f, Vector3.up, Vector3.up), Is.False, "along up");
                Assert.That(Evaluate(5f, Vector3.up, Vector3.down), Is.True, "against up");
                Assert.That(Evaluate(5f, Vector3.up, Vector3.right), Is.True, "perpendicular to up");
                Assert.That(Evaluate(5f, Vector3.forward, new Vector3(0f, 0f, 1f)), Is.False, "z components count");
                Assert.That(Evaluate(5f, Vector3.forward, new Vector3(0f, 0f, -1f)), Is.True);
                Assert.That(Evaluate(5f, Vector3.up, new Vector3(float.NaN, 0f, 0f)), Is.False, "an unordered dot fails");
                // ucomiss 0x5cb84e is unordered for NaN, so ja is not taken and the dot decides.
                Assert.That(Evaluate(float.NaN, Vector3.up, Vector3.up), Is.False, "a NaN speed falls through to the dot");
                Assert.That(Evaluate(float.NaN, Vector3.up, Vector3.down), Is.True, "a NaN speed against up");
            });
        }
    }
}
