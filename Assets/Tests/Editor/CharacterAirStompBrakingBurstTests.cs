using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Air stomp, braking and burst attack: the definitions and abilities, the nine concrete FSM
    // nodes and two abstract generic burst states that drive them, and the node-factory registration.
    // Expected values come from the 1.10.1 x86-64 bodies cited per test. Bodies that need a
    // fully started Character (ground contact, stamina, physics) cannot run here without
    // fakes, so those are checked as call sequences read from the compiled IL; each such test
    // says which native order or omission it witnesses.
    public sealed class CharacterAirStompBrakingBurstTests : OwnedGraphNamesFixture
    {
        public CharacterAirStompBrakingBurstTests() : base("Lucid-burst-") { }

        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Declared = Members | BindingFlags.DeclaredOnly;

        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        private T Own<T>(T item) where T : Object
        {
            owned.Add(item);
            return item;
        }

        // ---- CharacterAbilityDefinition_AirStompAttack.GetSettingsForSlope (06001add, 0x5422F0) ----

        [Serializable]
        public sealed class SlopeSettingsHolder
        {
            public List<CharacterAbilityDefinition_AirStompAttack.AirStompSlopeSetting> m_airStompSlopeSettings;
        }

        private static CharacterAbilityDefinition_AirStompAttack.AirStompSlopeSetting Slope(float min, float max, bool orient, bool boost, float speed) =>
            new CharacterAbilityDefinition_AirStompAttack.AirStompSlopeSetting
            {
                MinAngleInclusive = min, MaxAngleExclusive = max, OrientDownhill = orient, BoostIfDownhill = boost, BoostSpeed = speed,
            };

        // The settings list is a private [SerializeField]; it is filled through the engine's
        // own deserializer by serialized field name.
        private CharacterAbilityDefinition_AirStompAttack StompDefinition(params CharacterAbilityDefinition_AirStompAttack.AirStompSlopeSetting[] slopes)
        {
            var definition = Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_AirStompAttack>());
            var holder = new SlopeSettingsHolder { m_airStompSlopeSettings = slopes.ToList() };
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(holder), definition);
            return definition;
        }

        private static void AssertSlope(CharacterAbilityDefinition_AirStompAttack definition, float angle, bool orient, float boost)
        {
            definition.GetSettingsForSlope(angle, out bool actualOrient, out float actualBoost);
            Assert.That((actualOrient, actualBoost), Is.EqualTo((orient, boost)), "angle " + angle);
        }

        // The comparisons at 0x5423B4-0x5423C8 skip a setting when Min > angle or angle >= Max, so
        // Min is inclusive and Max exclusive. BoostSpeed is reported only with BoostIfDownhill (0x5423D1).
        // Fails if either bound turns inclusive or exclusive the other way, or if the
        // boost flag is ignored (the 60 degree row stores 33 but has BoostIfDownhill clear).
        [Test]
        public void SlopeSettingsAreMinimumInclusiveMaximumExclusiveAndBoostOnlyWhenFlagged()
        {
            CharacterAbilityDefinition_AirStompAttack definition = StompDefinition(
                Slope(0f, 30f, false, false, 11f), Slope(30f, 60f, true, true, 22f), Slope(60f, 90f, true, false, 33f));
            AssertSlope(definition, 0f, false, 0f);
            AssertSlope(definition, 29.99f, false, 0f);
            AssertSlope(definition, 30f, true, 22f);
            AssertSlope(definition, 59.99f, true, 22f);
            AssertSlope(definition, 60f, true, 0f);
            AssertSlope(definition, 89.99f, true, 0f);
            AssertSlope(definition, 90f, false, 0f);
            AssertSlope(definition, -1f, false, 0f);
        }

        // The loop leaves through 0x5423E5 right after the first match, so a later overlapping range never wins.
        [Test]
        public void SlopeSettingsFirstMatchingRangeWinsWhenRangesOverlap()
        {
            CharacterAbilityDefinition_AirStompAttack definition = StompDefinition(
                Slope(0f, 50f, true, true, 5f), Slope(20f, 60f, false, true, 9f));
            AssertSlope(definition, 25f, true, 5f);
            AssertSlope(definition, 55f, false, 9f);
        }

        // 0x5423B9 ucomiss/ja and 0x5423C4 ucomiss/jae skip only on an ordered comparison, so an
        // unordered NaN angle matches the first setting. Fails against skip-on-not-in-range forms.
        [Test]
        public void SlopeSettingsMatchTheFirstEntryForANaNAngle()
        {
            CharacterAbilityDefinition_AirStompAttack definition = StompDefinition(
                Slope(40f, 50f, true, true, 7f), Slope(0f, 90f, false, false, 1f));
            AssertSlope(definition, float.NaN, true, 7f);
        }

        // No setting matches: both out values keep the entry defaults (0x54235C, 0x542360).
        [Test]
        public void SlopeSettingsReportNothingForAnEmptyList()
        {
            AssertSlope(StompDefinition(), 45f, false, 0f);
        }

        // ---- CharacterAbilityDefinition_Braking (06001ae0-06001ae8, 0x542480-0x5426B0) ----

        // Constructor 0x5426B0 stores 0.5, 160, 30, 15 at +0x128..+0x134 (constants at VA 0x29a76d0),
        // 1 and 0.5 at +0x138/+0x13C (VA 0x29a7480) and 0.5 at +0x140.
        [Test]
        public void BrakingDefinitionStartsWithTheNativeThresholds()
        {
            var definition = Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_Braking>());
            Assert.That(definition.BufferedIntentSmoothTimeSeconds, Is.EqualTo(0.5f));
            Assert.That(definition.MinimumIntendedTriggerAngle, Is.EqualTo(160f));
            Assert.That(definition.ExitIntendedAngle, Is.EqualTo(30f));
            Assert.That(definition.MinimumTriggerXZSpeed, Is.EqualTo(15f));
            Assert.That(definition.EndAtXZSpeed, Is.EqualTo(1f));
            Assert.That(definition.MinimumInputMagnitude, Is.EqualTo(0.5f));
            Assert.That(definition.LandingBufferTimeSeconds, Is.EqualTo(0.5f));
            Assert.That(definition.LandingTime, Is.EqualTo(0.2f), "the ground base default is kept");
        }

        // 0x5425D1-0x5425F9 square MinimumInputMagnitude into +0x144, MinimumTriggerXZSpeed into +0x148
        // and EndAtXZSpeed into +0x14C. Distinct inputs separate the three outputs.
        // The ledge cosines are computed first, by the ground base inlined at 0x542555-0x5425CC.
        [Test]
        public void BrakingCachedValuesSquareEachThresholdIntoItsOwnProperty()
        {
            var definition = Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_Braking>());
            definition.LedgeBrakeParameters = new LedgeBrakeParameters();
            definition.MinimumInputMagnitude = 3f;
            definition.MinimumTriggerXZSpeed = 4f;
            definition.EndAtXZSpeed = 5f;
            definition.CalculateCachedValues();
            Assert.That(definition.MinimumInputMagnitudeSqr, Is.EqualTo(9f));
            Assert.That(definition.MinimumTriggerXZSpeedSqr, Is.EqualTo(16f));
            Assert.That(definition.EndAtXZSpeedSqr, Is.EqualTo(25f));
            Assert.That(definition.LedgeBrakeParameters.MaximumCosAngleToPlane, Is.EqualTo(0.5f).Within(1e-6f));
        }

        // Shared by the ground base: a missing ledge parameter object faults after the base cache ran,
        // leaving the squared properties unset.
        [Test]
        public void BrakingCachedValuesFaultWithoutLedgeParametersBeforeSquaring()
        {
            var definition = Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_Braking>());
            definition.LedgeBrakeParameters = null;
            Assert.Throws<NullReferenceException>(() => definition.CalculateCachedValues());
            Assert.That(definition.MinimumInputMagnitudeSqr, Is.EqualTo(0f));
        }

        // Declaration-shape self-check (not a behaviour test): the four get_ScriptType bodies load the
        // matching ability type handle.
        [Test]
        public void DeclarationShapeDefinitionsNameTheirOwnAbilityScript()
        {
            Assert.That(Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_AirStompAttack>()).ScriptType, Is.EqualTo(typeof(CharacterAbility_AirStompAttack)));
            Assert.That(Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_Braking>()).ScriptType, Is.EqualTo(typeof(CharacterAbility_Braking)));
            Assert.That(Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_BurstAttackGround>()).ScriptType, Is.EqualTo(typeof(CharacterAbility_BurstAttackGround)));
            Assert.That(Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_BurstAttackChaosGround>()).ScriptType, Is.EqualTo(typeof(CharacterAbility_BurstAttackChaosGround)));
        }

        // Declaration-shape self-check (not a behaviour test). Metadata: the Ground leaves sit directly under the shared burst definitions and the abilities
        // under the generic burst ability bases (dump.cs base types of TypeDefs 020004c2/020004c3,
        // 020002c0/020002c1, 020002b7); the braking ability implements IAbilityTrigger (020002b8).
        [Test]
        public void DeclarationShapeBurstGroundLeavesKeepTheirOriginalBases()
        {
            Assert.That(typeof(CharacterAbilityDefinition_BurstAttackGround).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_BurstAttack)));
            Assert.That(typeof(CharacterAbilityDefinition_BurstAttackChaosGround).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_BurstAttackChaos)));
            Assert.That(typeof(CharacterAbility_BurstAttackGround).BaseType, Is.EqualTo(typeof(CharacterAbility_BurstAttack<CharacterAbilityDefinition_BurstAttackGround>)));
            Assert.That(typeof(CharacterAbility_BurstAttackChaosGround).BaseType, Is.EqualTo(typeof(CharacterAbility_BurstAttackChaos<CharacterAbilityDefinition_BurstAttackChaosGround>)));
            Assert.That(typeof(CharacterAbility_AirStompAttack).BaseType, Is.EqualTo(typeof(CharacterAbility_Air)));
            Assert.That(typeof(CharacterAbility_Braking).GetInterfaces(), Does.Contain(typeof(IAbilityTrigger)));
        }

        // ---- CharacterAbility_Braking (06001071-0600107a) ----

        private static IDictionary Registry =>
            (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        // The real constructor's CharacterAbility initializer creates the App reference row;
        // only an absent row can be owned, and the empty row is released afterwards.
        private static void WithOwnedAppRow(Action<CharacterAbility_Braking> body)
        {
            IDictionary registry = Registry;
            string appName = ProcessManager.GetDefaultName<App>();
            VerificationFixtureOwnership.RequireAbsent(registry, appName);
            try
            {
                body(new CharacterAbility_Braking());
            }
            finally
            {
                object row = registry[appName];
                if (row != null) VerificationFixtureOwnership.ReleaseEmptyRow(registry, appName, row);
            }
        }

        private static void SetBraking(CharacterAbility_Braking ability, bool value) =>
            typeof(CharacterAbility_Braking).GetProperty("IsBraking", Declared).GetSetMethod(true).Invoke(ability, new object[] { value });

        // Both getters (0x742C80, 0x742C90) read the same byte at +0xE8.
        [Test]
        public void BrakingTriggerMirrorsTheBrakingFlag()
        {
            WithOwnedAppRow(ability =>
            {
                foreach (bool braking in new[] { false, true, false })
                {
                    SetBraking(ability, braking);
                    Assert.That(ability.IsBraking, Is.EqualTo(braking));
                    Assert.That(ability.IsTriggered, Is.EqualTo(braking));
                    Assert.That(((IAbilityTrigger)ability).IsTriggered, Is.EqualTo(braking));
                }
            });
        }

        private CharacterAbilityDefinition_Braking BrakingDefinition()
        {
            var definition = Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_Braking>());
            definition.LedgeBrakeParameters = new LedgeBrakeParameters();
            definition.ForwardMotion = new CharacterAbilityDefinition.Motion
            {
                SpeedMin = 2f, SpeedMax = 4f, Acceleration = 1f, DecelerationCurve = AnimationCurve.Constant(0f, 10f, 5f),
            };
            definition.BrakePowerCurve = AnimationCurve.Constant(0f, 10f, 2f);
            return definition;
        }

        // 0x743180-0x743227: only while IsBraking and applyDeceleration, the speed goes through
        // ProcessDecelerationCurve (brake power 2 x deceleration 5 x dt 0.5 = 5 off 10). Otherwise the
        // speed is untouched; the ground base, which would clamp -3 up to 0, is not consulted.
        [Test]
        public void BrakingDeceleratesOnlyWhileBrakingAndWhenDecelerationIsRequested()
        {
            WithOwnedAppRow(ability =>
            {
                ability.Initialise(null, BrakingDefinition());
                var direction = Vector3.forward;

                SetBraking(ability, true);
                float speed = 10f;
                ability.UpdateForwardSpeed(ref speed, ref direction, 0.5f);
                Assert.That(speed, Is.EqualTo(5f).Within(1e-5f));

                speed = 10f;
                ability.UpdateForwardSpeed(ref speed, ref direction, 0.5f, applyDeceleration: false);
                Assert.That(speed, Is.EqualTo(10f));

                SetBraking(ability, false);
                speed = 10f;
                ability.UpdateForwardSpeed(ref speed, ref direction, 0.5f);
                Assert.That(speed, Is.EqualTo(10f));
                speed = -3f;
                ability.UpdateForwardSpeed(ref speed, ref direction, 0.5f);
                Assert.That(speed, Is.EqualTo(-3f), "no ground-base clamp to zero");
                Assert.That(direction, Is.EqualTo(Vector3.forward));
            });
        }

        // The multiplier reaches the curve helper (0x743211-0x74321B moves xmm2 unchanged): a speed of 3
        // sits above the base minimum 2 (it would decelerate to 0) but below the multiplied minimum 4,
        // so it accelerates by dt x Acceleration to 3.5.
        [Test]
        public void BrakingMotionMultiplierScalesTheMinimumSpeed()
        {
            WithOwnedAppRow(ability =>
            {
                ability.Initialise(null, BrakingDefinition());
                SetBraking(ability, true);
                var direction = Vector3.forward;
                float speed = 3f;
                ability.UpdateForwardSpeed(ref speed, ref direction, 0.5f, 2f);
                Assert.That(speed, Is.EqualTo(3.5f).Within(1e-5f));
            });
        }

        // 0x743202 passes m_brakeElapsedTime (+0xEC) as the curve progress and 0x743211 passes deltaTime in the
        // next argument. A linear brake power curve (power = time) at elapsed 2 gives 2 x deceleration 5 x dt 0.5
        // = 5 off 10; a progress of dt would give 8.75 and a progress of 0 would give 10.
        [Test]
        public void BrakingDecelerationReadsTheBrakeCurveAtTheElapsedBrakeTime()
        {
            WithOwnedAppRow(ability =>
            {
                CharacterAbilityDefinition_Braking definition = BrakingDefinition();
                definition.BrakePowerCurve = AnimationCurve.Linear(0f, 0f, 10f, 10f);
                ability.Initialise(null, definition);
                typeof(CharacterAbility_Braking).GetField("m_brakeElapsedTime", Declared).SetValue(ability, 2f);
                SetBraking(ability, true);
                var direction = Vector3.forward;
                float speed = 10f;
                ability.UpdateForwardSpeed(ref speed, ref direction, 0.5f);
                Assert.That(speed, Is.EqualTo(5f).Within(1e-5f));
            });
        }

        // Metadata optional-parameter defaults of the override (1, 1, null, true) and its base slot.
        [Test]
        public void BrakingForwardSpeedOverrideKeepsTheOriginalOptionalDefaults()
        {
            MethodInfo method = typeof(CharacterAbility_Braking).GetMethods(Declared).Single(m => m.Name == "UpdateForwardSpeed");
            ParameterInfo[] parameters = method.GetParameters();
            Assert.That(parameters.Select(p => p.Name), Is.EqualTo(new[]
            {
                "forwardSpeed", "forwardDirection", "deltaTime", "motionMultiplier", "effectiveInputMagnitude",
                "turnTraitsAccelerationMultiplier", "applyDeceleration",
            }));
            Assert.That(parameters[3].DefaultValue, Is.EqualTo(1f));
            Assert.That(parameters[4].DefaultValue, Is.EqualTo(1f));
            Assert.That(parameters[5].HasDefaultValue, Is.True);
            Assert.That(parameters[5].DefaultValue, Is.Null);
            Assert.That(parameters[6].DefaultValue, Is.EqualTo(true));
            Assert.That(method.GetBaseDefinition().DeclaringType.GetGenericTypeDefinition(), Is.EqualTo(typeof(CharacterAbility_Movement<>)));
        }

        // ---- IL witnesses for bodies that need a started Character ----
        // These witnesses pin call order and counts only. Branch polarity and NaN behaviour of the Braking
        // DoOnUpdate predicates, AirStomp IsBrainImpulseActive and TryRotateDownhillOfCollision, the burst
        // ActivateStamina skip and the air transition branches have no truth-table coverage yet (they need
        // a started Character with its save, stamina and brain graph); do not read these as behavioural proof.

        private static List<MethodBase> Calls(Type owner, string name)
        {
            MethodBase method = owner.GetMethods(Declared).Single(m => m.Name == name);
            return OriginalILCallSequence.Calls(method).Select(call => call.Target).ToList();
        }

        private static int IndexOf(List<MethodBase> calls, string typeName, string name, int from = 0)
        {
            int index = calls.FindIndex(from, m => DeclaredBy(m, typeName) && m.Name == name);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), typeName + "." + name + " is called" + (from > 0 ? " after index " + from : string.Empty));
            return index;
        }

        private static int Count(List<MethodBase> calls, string typeName, string name) =>
            calls.Count(m => DeclaredBy(m, typeName) && m.Name == name);

        // Exact declaring type; a generic type definition is named with its arity suffix (Name`N).
        private static bool DeclaredBy(MethodBase method, string typeName) =>
            method.DeclaringType.Name == typeName || method.DeclaringType.Name.StartsWith(typeName + "`", StringComparison.Ordinal);

        // 06001075 (0x742F80): the SmoothDamp of the buffered intent comes after the movement base update,
        // and the result feeds EvaluateInput (0x742D20); the elapsed time is written without any ground-base call.
        [Test]
        public void BrakingUpdateSmoothsTheBufferedIntentBeforeEvaluatingInput()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_Braking), "DoOnUpdate");
            int baseUpdate = IndexOf(calls, "CharacterAbility_Movement", "DoOnUpdate");
            int fixedTime = IndexOf(calls, "Actor", "GetTotalFixedTime");
            int smooth = IndexOf(calls, "Vector2", "SmoothDamp");
            int evaluate = IndexOf(calls, "CharacterAbility_Braking", "EvaluateInput");
            Assert.That(new[] { baseUpdate, fixedTime, smooth, evaluate }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(calls, "CharacterAbility_Braking", "EvaluateInput"), Is.EqualTo(1));
        }

        // 06001079 (0x742D20): the forward-to-intent angle is measured twice (braking exit test at 0x742DF7 and
        // landing-buffer entry at 0x742E29) and the buffered-versus-raw intent once, through Vector2.Angle inlined at 0x742E78.
        [Test]
        public void BrakingEvaluateInputMeasuresTheThreeNativeAngles()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_Braking), "EvaluateInput");
            Assert.That(Count(calls, "CharacterMovementUtilities", "GetIntendedToCurrentForwardAngle"), Is.EqualTo(2));
            Assert.That(Count(calls, "Vector2", "Angle"), Is.EqualTo(1));
            Assert.That(Count(calls, "Vector3Extensions", "xz"), Is.EqualTo(1));
        }

        // Character's field initialiser creates the empty GameplayIslandManager row and CharacterAbility's the
        // empty App row. Both are admitted absent and released while still empty.
        private static void WithBrakingOnCharacter(Action<CharacterAbility_Braking, Character> body)
        {
            IDictionary registry = Registry;
            string[] rows = { ProcessManager.GetDefaultName<GameplayIslandManager>(), ProcessManager.GetDefaultName<App>() };
            foreach (string name in rows) VerificationFixtureOwnership.RequireAbsent(registry, name);
            var host = new GameObject("Lucid-braking-character");
            host.SetActive(false);
            VerificationFixtureOwnership.RunThenCleanUp(
                () => body(new CharacterAbility_Braking(), host.AddComponent<Character>()),
                () => Object.DestroyImmediate(host),
                () =>
                {
                    foreach (string name in rows)
                        if (registry.Contains(name)) VerificationFixtureOwnership.ReleaseEmptyRow(registry, name, registry[name]);
                });
        }

        // Private setters are taken from the declaring type.
        private static void SetProperty(object target, string property, object value)
        {
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type owner = target.GetType().GetProperty(property, instance).DeclaringType;
            owner.GetProperty(property, instance | BindingFlags.DeclaredOnly).GetSetMethod(true).Invoke(target, new[] { value });
        }

        private static bool EvaluateInput(CharacterAbility_Braking ability, bool evaluateForwardToIntent) =>
            (bool)typeof(CharacterAbility_Braking).GetMethod("EvaluateInput", Declared).Invoke(ability, new object[] { evaluateForwardToIntent });

        // 06001079 (0x742D20) with IsBraking clear and the forward-to-intent flag clear. Gate 1 is 0x742D85
        // ucomiss/jae: sqr(ControllerMovement) >= MinimumInputMagnitudeSqr (+0x144), so equality passes and NaN
        // does not. Gate 2 compares the XZ speed squared with +0x148 (MinimumTriggerXZSpeedSqr) while not braking
        // (0x742DD1 picks +0x14C only when braking), at 0x742DDD ucomiss/jb, again equality passing. The result
        // is 0x742F63 setae: Vector2.Angle(buffered, raw) >= MinimumIntendedTriggerAngle (+0x12C).
        // Defaults give 0.25, 225, 1 and 160 degrees.
        [Test]
        public void BrakingEvaluateInputGatesOnInputSpeedAndAngleWithNativePolarity()
        {
            WithBrakingOnCharacter((ability, character) =>
            {
                ability.Initialise(null, BrakingDefinition());
                typeof(CharacterAbility).GetField("m_character", Members).SetValue(ability, character);
                typeof(CharacterAbility_Braking).GetField("m_bufferedControllerMovement", Declared).SetValue(ability, new Vector2(0f, 1f));
                SetProperty(character, "RawControllerMovement", new Vector2(0f, -1f));
                SetProperty(character, "ControllerMovement", new Vector2(0.5f, 0f));
                SetProperty(character, "LocalVelocity", new Vector3(0f, 0f, 15f));
                Assert.That(EvaluateInput(ability, false), Is.True, "input 0.25 and speed 225 sit exactly on both thresholds and the angle is 180");

                SetProperty(character, "LocalVelocity", new Vector3(0f, 0f, 10f));
                Assert.That(EvaluateInput(ability, false), Is.False, "speed 100 is below the trigger threshold 225, not the end threshold 1");

                SetProperty(character, "LocalVelocity", new Vector3(0f, 0f, 15f));
                SetProperty(character, "ControllerMovement", new Vector2(float.NaN, 0f));
                Assert.That(EvaluateInput(ability, false), Is.False, "a NaN input magnitude fails the gate");

                SetProperty(character, "ControllerMovement", new Vector2(0.5f, 0f));
                SetProperty(character, "RawControllerMovement", new Vector2(0f, 1f));
                Assert.That(EvaluateInput(ability, false), Is.False, "an angle of 0 is below 160");
            });
        }

        // 06001077 (0x743180) ends in a tail call to ProcessDecelerationCurve; it never reaches the movement
        // base UpdateForwardSpeed.
        [Test]
        public void BrakingForwardSpeedNeverCallsTheMovementBase()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_Braking), "UpdateForwardSpeed");
            Assert.That(Count(calls, "CharacterMovementUtilities", "ProcessDecelerationCurve"), Is.EqualTo(1));
            Assert.That(Count(calls, "CharacterAbility_Movement", "UpdateForwardSpeed"), Is.EqualTo(0));
        }

        // 0600106b (0x7426D0): the queued jump and boost flags are ORed from the brain after the air update,
        // then the downhill rotation runs. 0600106d (0x742BB0) reads only the air-stomp action, never the air-active one;
        // 0600106e (0x742C00) ends only the air-stomp impulse.
        [Test]
        public void AirStompAbilityReadsTheStompActionAndEndsTheStompImpulse()
        {
            List<MethodBase> update = Calls(typeof(CharacterAbility_AirStompAttack), "DoOnUpdate");
            int baseUpdate = IndexOf(update, "CharacterAbility_Air", "DoOnUpdate");
            int jump = IndexOf(update, "CharacterBrain", "get_Jump");
            int setJump = IndexOf(update, "CharacterQueuedActions", "set_Jump");
            int boost = IndexOf(update, "CharacterBrain", "get_Boost");
            int setBoost = IndexOf(update, "CharacterQueuedActions", "set_Boost");
            int rotate = IndexOf(update, "CharacterAbility_AirStompAttack", "TryRotateDownhillOfCollision");
            Assert.That(new[] { baseUpdate, jump, setJump, boost, setBoost, rotate }, Is.Ordered.Ascending.And.Unique);

            List<MethodBase> impulse = Calls(typeof(CharacterAbility_AirStompAttack), "IsBrainImpulseActive");
            Assert.That(Count(impulse, "CharacterBrain", "get_AirStompAttack"), Is.GreaterThanOrEqualTo(1));
            Assert.That(Count(impulse, "CharacterBrain", "get_AirActive"), Is.EqualTo(0));
            Assert.That(Count(impulse, "CharacterAbility_Air", "IsControlContinuous"), Is.EqualTo(1));

            List<MethodBase> end = Calls(typeof(CharacterAbility_AirStompAttack), "EndBrainImpulse");
            Assert.That(end.Select(m => m.Name), Is.EqualTo(new[] { "EndAirStompAttackImpulse" }));
        }

        // 06001069 (0x742580) stores the typed definition only after a successful type test; 0600106f (0x742C10)
        // resets the air base, then (when interrupted) triggers the air burst, rotates downhill and sets the
        // local velocity to the boost speed.
        [Test]
        public void AirStompResetRotatesDownhillAfterTheAirBase()
        {
            List<MethodBase> reset = Calls(typeof(CharacterAbility_AirStompAttack), "Reset");
            int baseReset = IndexOf(reset, "CharacterAbility_Air", "Reset");
            int burst = IndexOf(reset, "Character", "TriggerBurstAttackAir");
            int rotate = IndexOf(reset, "CharacterAbility_AirStompAttack", "TryRotateDownhillOfCollision");
            int velocity = IndexOf(reset, "Actor", "SetLocalVelocity");
            Assert.That(new[] { baseReset, burst, rotate, velocity }, Is.Ordered.Ascending.And.Unique);
        }

        // 0600106c (0x742770): the ground hit position is applied once (only when only the projected hit exists);
        // the slope angle is Vector3.Angle against negated gravity, the downhill is the normalised
        // plane projection of gravity, then the settings lookup and either the look rotation or the forward dot.
        // SetWorldPosition and SetWorldRotation are Actor virtuals (here vcall slot 35 at 0x7427EE and slot 36 at
        // 0x742B60), so the IL call targets are declared by Actor even though the receivers are Characters.
        [Test]
        public void AirStompDownhillRotationCallsTheNativeMathInOrder()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_AirStompAttack), "TryRotateDownhillOfCollision");
            int position = IndexOf(calls, "Actor", "SetWorldPosition");
            int negate = IndexOf(calls, "Vector3", "op_UnaryNegation");
            int angle = IndexOf(calls, "Vector3", "Angle");
            int project = IndexOf(calls, "Vector3", "ProjectOnPlane");
            int forward = IndexOf(calls, "Actor", "get_ForwardDirection");
            int settings = IndexOf(calls, "CharacterAbilityDefinition_AirStompAttack", "GetSettingsForSlope");
            int look = IndexOf(calls, "Quaternion", "LookRotation");
            int rotate = IndexOf(calls, "Actor", "SetWorldRotation");
            int dot = IndexOf(calls, "Vector3", "Dot");
            Assert.That(new[] { position, negate, angle, project, forward, settings, look, rotate, dot }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(calls, "Actor", "SetWorldPosition"), Is.EqualTo(1));
        }

        // ---- The 11 FSM nodes ----

        private static string Name() => "Lucid-burst-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);

        private static void AssertNode(Type node, string defaultName, string menu)
        {
            GraphNodeDefaultNameAttribute name = node.GetCustomAttribute<GraphNodeDefaultNameAttribute>(false);
            if (defaultName == null)
                Assert.That(name, Is.Null, node.Name);
            else
                Assert.That(name.DefaultName, Is.EqualTo(defaultName), node.Name);
            Assert.That(node.GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format, Is.EqualTo(menu), node.Name);
        }

        // CAD blobs of the node classes: default graph names, menu formats and (for the trigger state)
        // the red graph colour.
        [Test]
        public void NodesCarryTheirOriginalGraphAuthoringAttributes()
        {
            AssertNode(typeof(ActorState_TriggerAbility), "TriggerAbility", "Actor/{0}");
            AssertNode(typeof(ActorState_TriggerDisableAbilities), "TriggerDisableAbilities", "Actor/{0}");
            AssertNode(typeof(ActorTransition_AbilityFinished), "AbilityFinished", "Actor/{0}");
            AssertNode(typeof(CharacterState_AirLedgeBuffer), "AirLedgeBuffer", "Character/{0}");
            AssertNode(typeof(CharacterState_BurstAttackAir), "BurstAttackAir", "Character/{0}");
            AssertNode(typeof(CharacterState_BurstAttackGround), "BurstAttackGround", "Character/{0}");
            AssertNode(typeof(CharacterState_BurstAttackChaosAir), "BurstAttackChaosAir", "Character/{0}");
            AssertNode(typeof(CharacterState_BurstAttackChaosGround), "BurstAttackChaosGround", "Character/{0}");
            AssertNode(typeof(CharacterTransition_AirAbility), null, "Character/{0}");
            Assert.That(typeof(ActorState_TriggerAbility).GetCustomAttribute<GraphNodeColourAttribute>(false).Colour, Is.EqualTo(new Color(1f, 0f, 0f, 1f)));
        }

        // Type-level shape of the generic burst states: abstract, two constrained parameters, the shared
        // overlap buffer, and a protected abstract argument type that defaults MaxTargets to 10 (0xE4BFA4).
        [Test]
        public void BurstStatesKeepTheirGenericShape()
        {
            Type burst = typeof(CharacterState_BurstAttack<,>);
            Assert.That(burst.IsAbstract, Is.True);
            Assert.That(burst.GetGenericArguments().Select(a => a.Name), Is.EqualTo(new[] { "T", "TDef" }));
            Assert.That(burst.GetField("m_temporaryOverlapResults", Declared).FieldType, Is.EqualTo(typeof(Collider[])));
            Type chaos = typeof(CharacterState_BurstAttackChaos<,>);
            Assert.That(chaos.IsAbstract, Is.True);
            Assert.That(chaos.GetGenericArguments().Select(a => a.Name), Is.EqualTo(new[] { "TAbility", "TAbilityDef" }));
            Assert.That(chaos.BaseType.GetGenericTypeDefinition(), Is.EqualTo(burst));
            Type args = burst.GetNestedType("BurstAttackJSONCtorArgs", BindingFlags.NonPublic);
            Assert.That(args.IsAbstract && args.IsNestedFamily, Is.True);
            Assert.That(args.GetField("MaxTargets").FieldType, Is.EqualTo(typeof(int)));
        }

        private static void AssertRegistered(IReadOnlyDictionary<string, JSONFactoryClass> lookup, Type node, string argsName, Type argsOwner)
        {
            Assert.That(lookup.TryGetValue(node.Name, out JSONFactoryClass factory), Is.True, node.Name);
            Assert.That(factory.ClassType, Is.EqualTo(node), node.Name);
            Assert.That(factory.ConstructInstanceMethod.DeclaringType, Is.EqualTo(node), node.Name);
            Assert.That(factory.JSONCtorArgsType.Name, Is.EqualTo(argsName), node.Name);
            Assert.That(factory.JSONCtorArgsType.DeclaringType, Is.EqualTo(argsOwner), node.Name);
        }

        // The class factory discovers nodes by their static ConstructInstance and finds the argument type as
        // the nested type named JSONCtorArgs or carrying the FSM argument attribute. The trigger states'
        // nested type must keep the bare JSONCtorArgs name (it has no attribute), the burst leaves their own
        // attributed argument classes, and the ability nodes the shared Ability argument types.
        [Test]
        public void ClassFactoryRegistersTheNodesWithTheirArgumentTypes()
        {
            AssertRegistered(FSMClassFactory.States, typeof(ActorState_TriggerAbility), "JSONCtorArgs", typeof(ActorState_TriggerAbility));
            AssertRegistered(FSMClassFactory.States, typeof(ActorState_TriggerDisableAbilities), "JSONCtorArgs", typeof(ActorState_TriggerDisableAbilities));
            AssertRegistered(FSMClassFactory.States, typeof(CharacterState_AirLedgeBuffer), "AbilityJSONCtorArgs", typeof(ActorState_Ability));
            AssertRegistered(FSMClassFactory.States, typeof(CharacterState_BurstAttackAir), "BurstAttackAirJSONCtorArgs", typeof(CharacterState_BurstAttackAir));
            AssertRegistered(FSMClassFactory.States, typeof(CharacterState_BurstAttackGround), "BurstAttackGroundJSONCtorArgs", typeof(CharacterState_BurstAttackGround));
            AssertRegistered(FSMClassFactory.States, typeof(CharacterState_BurstAttackChaosAir), "BurstAttackChaosAirJSONCtorArgs", typeof(CharacterState_BurstAttackChaosAir));
            AssertRegistered(FSMClassFactory.States, typeof(CharacterState_BurstAttackChaosGround), "BurstAttackChaosGroundJSONCtorArgs", typeof(CharacterState_BurstAttackChaosGround));
            AssertRegistered(FSMClassFactory.Transitions, typeof(ActorTransition_AbilityFinished), "AbilityJSONCtorArgs", typeof(Transition_Ability));
            AssertRegistered(FSMClassFactory.Transitions, typeof(CharacterTransition_AirAbility), "AbilityJSONCtorArgs", typeof(Transition_Ability));
            Assert.That(FSMClassFactory.States.ContainsKey(typeof(CharacterState_BurstAttack<,>).Name), Is.False, "abstract generics are not factory nodes");
        }

        private static int OverlapCapacity(object state)
        {
            for (Type type = state.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField("m_temporaryOverlapResults", Declared);
                if (field != null) return ((Array)field.GetValue(state)).Length;
            }
            throw new AssertionException("no overlap buffer on " + state.GetType());
        }

        // 0xE6BD5F/0xE4BFA4: the buffer holds MaxTargets colliders, with 10 when the JSON omits it.
        // Fails if the default is dropped to zero (JsonUtility keeps constructor defaults) or the
        // authored value is ignored.
        [Test]
        public void BurstStatesSizeTheOverlapBufferFromMaxTargets()
        {
            FiniteStateMachine machine = Machine();
            foreach (string node in new[]
            {
                nameof(CharacterState_BurstAttackAir), nameof(CharacterState_BurstAttackGround),
                nameof(CharacterState_BurstAttackChaosAir), nameof(CharacterState_BurstAttackChaosGround),
            })
            {
                IFSMState authored = FSMClassFactory.ConstructState(machine, node, new FSMIdentifier(Name()), "{\"MaxTargets\":4}");
                Assert.That(authored.GetType().Name, Is.EqualTo(node));
                Assert.That(OverlapCapacity(authored), Is.EqualTo(4), node);
                IFSMState defaulted = FSMClassFactory.ConstructState(machine, node, new FSMIdentifier(Name()), "{}");
                Assert.That(OverlapCapacity(defaulted), Is.EqualTo(10), node);
            }
        }

        private static bool Prevents(object state) =>
            (bool)state.GetType().GetMethod("PreventDeactivation", Members).Invoke(state, null);

        // 0x7759F0 returns true for the air chaos state; the base (0xE6BCF0) and the ground leaf return false.
        [Test]
        public void OnlyTheAirChaosStatePreventsDeactivation()
        {
            FiniteStateMachine machine = Machine();
            IFSMState air = FSMClassFactory.ConstructState(machine, nameof(CharacterState_BurstAttackChaosAir), new FSMIdentifier(Name()), "{}");
            IFSMState ground = FSMClassFactory.ConstructState(machine, nameof(CharacterState_BurstAttackChaosGround), new FSMIdentifier(Name()), "{}");
            Assert.That(Prevents(air), Is.True);
            Assert.That(Prevents(ground), Is.False);
            Assert.That(typeof(CharacterState_BurstAttackChaosAir).GetMethod("PreventDeactivation", Members).DeclaringType, Is.EqualTo(typeof(CharacterState_BurstAttackChaosAir)));
            Assert.That(typeof(CharacterState_BurstAttackChaosGround).GetMethod("PreventDeactivation", Members).DeclaringType, Is.Not.EqualTo(typeof(CharacterState_BurstAttackChaosGround)));
        }

        // 0x775740 is a bare RET: the ledge-buffer state never counts the boost suspension down. A null character
        // and ability would fault in the air state's implementation (0x775740 vs. the base body that reads both).
        [Test]
        public void LedgeBufferStateSuppressesTheBoostSuspendCountdown()
        {
            IFSMState state = FSMClassFactory.ConstructState(Machine(), nameof(CharacterState_AirLedgeBuffer), new FSMIdentifier(Name()), "{}");
            MethodInfo method = state.GetType().GetMethod("UpdateBoostSuspendTime", Members);
            Assert.That(method.DeclaringType, Is.EqualTo(typeof(CharacterState_AirLedgeBuffer)));
            Assert.DoesNotThrow(() => method.Invoke(state, new object[] { null, null, 1f }));
            // Control: a plain air state runs the base body, which reads the character and faults on null.
            IFSMState air = FSMClassFactory.ConstructState(Machine(), nameof(CharacterState_Air), new FSMIdentifier(Name()), "{}");
            MethodInfo airMethod = air.GetType().GetMethod("UpdateBoostSuspendTime", Members);
            Assert.That(airMethod.DeclaringType, Is.EqualTo(typeof(CharacterState_Air)));
            Assert.That(Assert.Throws<TargetInvocationException>(() => airMethod.Invoke(air, new object[] { null, null, 1f })).InnerException,
                Is.TypeOf<NullReferenceException>());
        }

        // 0x529620 and 0x78EAF0 build the transition from the JSON ability type and register it with the machine.
        [Test]
        public void AbilityTransitionsReadTheAbilityTypeFromTheirJson()
        {
            FieldInfo abilityType = typeof(Transition_Ability).GetField("m_abilityType", Members);
            foreach (string node in new[] { nameof(ActorTransition_AbilityFinished), nameof(CharacterTransition_AirAbility) })
            {
                FiniteStateMachine machine = Machine();
                string name = Name();
                IFSMTransition transition = FSMClassFactory.ConstructTransition(machine, node, new FSMIdentifier(name),
                    "{\"AbilityType\":" + (int)ActorAbilityType.Character_AirStompAttack + "}");
                Assert.That(transition.GetType().Name, Is.EqualTo(node));
                Assert.That(abilityType.GetValue(transition), Is.EqualTo(ActorAbilityType.Character_AirStompAttack), node);
                Assert.That(machine.TransitionExists(new FSMIdentifier(name), out IFSMTransition registered), Is.True, node);
                Assert.That(registered, Is.SameAs(transition), node);
            }
        }

        // 0x528280 stores Enable = true before chaining; the trigger-event type stays at its enum default.
        [Test]
        public void TriggerAbilityArgumentsDefaultToEnabled()
        {
            Type args = typeof(ActorState_TriggerAbility).GetNestedType("JSONCtorArgs", BindingFlags.NonPublic);
            FieldInfo triggerEvent = args.GetField("ActiveEventType");
            Assert.That(Convert.ToInt32(triggerEvent.GetValue(Activator.CreateInstance(args))), Is.EqualTo(0));
            Assert.That(args.GetField("Enable").GetValue(Activator.CreateInstance(args)), Is.EqualTo(true));
            object parsed = JsonUtility.FromJson("{}", args);
            Assert.That(Convert.ToInt32(triggerEvent.GetValue(parsed)), Is.EqualTo(0));
            Assert.That(args.GetField("Enable").GetValue(parsed), Is.EqualTo(true), "the constructor default survives JSON that omits the field");
            parsed = JsonUtility.FromJson("{\"Enable\":false,\"AbilityDefinitionName\":\"x\"}", args);
            Assert.That(args.GetField("Enable").GetValue(parsed), Is.EqualTo(false));
            Assert.That(args.GetField("AbilityDefinitionName").GetValue(parsed), Is.EqualTo("x"));
            Assert.That(args.GetField("AbilityDefinitionName").GetCustomAttribute<GraphUnityObjectAttribute>().UnityObjectType, Is.EqualTo(typeof(AbilityDefinition)));

            Type disable = typeof(ActorState_TriggerDisableAbilities).GetNestedType("JSONCtorArgs", BindingFlags.NonPublic);
            Assert.That(disable.GetField("ExceptionList").GetCustomAttribute<GraphUnityObjectAttribute>().UnityObjectType, Is.EqualTo(typeof(AbilityDefinition)));
        }

        private sealed class ProbeActor : Actor
        {
            public override float BrainMovementMagnitude => 0f;
            protected override void DoOnUpdate(float deltaTime) { }
            public override TimeCategory GetTimeCategory() => default;
            protected override void UpdateBodyPosition(float deltaTime, out Vector3 position, out Quaternion rotation, out Vector3 velocity)
            {
                position = default;
                rotation = default;
                velocity = default;
            }
            public override void MovementStop(bool detectCollisions = false) { }
            public override void MovementResume(Vector3 position, Vector3 velocity) { }
            protected override void DoClose(bool isBeingDestroyed) { }
            public void AddAbility(ActorAbilityType type, ActorAbility ability) => m_abilities.Add(type, ability);
        }

        private sealed class ProbeAbility : ActorAbility
        {
            public readonly List<string> Log = new List<string>();
            public override AbilityDefinition DefinitionBase => null;
            protected override void DoOnEnter() => Log.Add("Enter");
            protected override void DoOnLeave() => Log.Add("Leave");
            public void Finish(bool value) => AbilityFinished = value;
        }

        private sealed class ProbeAbilityDefinition : AbilityDefinition
        {
            public override Type ScriptType => typeof(ProbeAbility);
        }

        private sealed class ProbeTriggerState : ActorState_TriggerAbility
        {
            private readonly Actor m_target;

            private ProbeTriggerState(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs, Actor target) : base(fsm, stateId, ctorArgs)
            {
                m_target = target;
            }

            public static ProbeTriggerState Create(FiniteStateMachine fsm, FSMIdentifier stateId, string json, Actor target) =>
                new ProbeTriggerState(fsm, stateId, JsonUtility.FromJson<JSONCtorArgs>(json), target);

            protected override Actor GetActor(IGraphUser user) => m_target;
            public void Start() => InvokeTriggerStart(null, null);
            public void End() => InvokeTriggerEnd(null, null);
        }

        private ProbeActor NewProbeActor()
        {
            var host = Own(new GameObject("Lucid-trigger-actor"));
            host.SetActive(false);
            return host.AddComponent<ProbeActor>();
        }

        // 0600043a (0x528100) applies m_enable, 0600043b (0x5281D0) the opposite: 0x52821F enters or leaves by
        // the flag left in the argument register. Enter always runs its hook; Leave only acts on an enabled ability.
        [Test]
        public void TriggerAbilityStateAppliesTheConfiguredChangeOnStartAndTheOppositeOnEnd()
        {
            using (var characters = new OwnedCharacterManagers())
            {
                var definition = Own(ScriptableObject.CreateInstance<ProbeAbilityDefinition>());
                definition.AbilityType = ActorAbilityType.Character_AirStompAttack;
                string key = "Lucid-ability-" + Guid.NewGuid().ToString("N");
                Dictionary<string, AbilityDefinition> definitions = ProcessManager.GetSystemRef<App>().Get().DataManager.AbilityDefinitions;
                definitions.Add(key, definition);
                try
                {
                    foreach (bool enable in new[] { false, true })
                    {
                        ProbeActor actor = NewProbeActor();
                        var ability = new ProbeAbility();
                        ability.Enter();
                        actor.AddAbility(definition.AbilityType, ability);
                        ProbeTriggerState state = ProbeTriggerState.Create(Machine(), new FSMIdentifier(Name()),
                            "{\"AbilityDefinitionName\":\"" + key + "\",\"Enable\":" + (enable ? "true" : "false") + "}", actor);
                        ability.Log.Clear();
                        state.Start();
                        string[] started = ability.Log.ToArray();
                        state.End();
                        Assert.That(started, Is.EqualTo(new[] { enable ? "Enter" : "Leave" }), "start, enable " + enable);
                        Assert.That(ability.Log.Skip(started.Length), Is.EqualTo(new[] { enable ? "Leave" : "Enter" }), "end, enable " + enable);
                    }
                }
                finally
                {
                    definitions.Remove(key);
                }
            }
        }

        // 06000458 (0x5296B0): no ability of the type gives false (0x52972E xor eax,eax); otherwise the flag is returned.
        [Test]
        public void AbilityFinishedTransitionIsFalseForAMissingAbilityAndFollowsTheFinishedFlag()
        {
            IFSMTransition transition = FSMClassFactory.ConstructTransition(Machine(), nameof(ActorTransition_AbilityFinished), new FSMIdentifier(Name()),
                "{\"AbilityType\":" + (int)ActorAbilityType.Character_AirStompAttack + "}");
            MethodInfo doUpdate = typeof(ActorTransition_AbilityFinished).GetMethod("DoUpdate", Declared);
            ProbeActor actor = NewProbeActor();
            bool Run() => (bool)doUpdate.Invoke(transition, new object[] { actor, new FSMUpdateContext(0.02f, FSMUpdateType.FixedUpdate) });

            Assert.That(Run(), Is.False, "no ability of the type");
            var ability = new ProbeAbility();
            actor.AddAbility(ActorAbilityType.Character_AirStompAttack, ability);
            Assert.That(Run(), Is.False, "ability not finished");
            ability.Finish(true);
            Assert.That(Run(), Is.True, "ability finished");
        }

        // 06000458 (0x5296B0): the transition fires on ActorAbility.AbilityFinished (+0x11) alone. It neither
        // consults the base transition update nor reads Enabled (+0x10).
        [Test]
        public void AbilityFinishedTransitionReadsOnlyTheFinishedFlag()
        {
            List<MethodBase> calls = Calls(typeof(ActorTransition_AbilityFinished), "DoUpdate");
            Assert.That(Count(calls, "ActorAbility", "get_AbilityFinished"), Is.EqualTo(1));
            Assert.That(Count(calls, "ActorAbility", "get_Enabled"), Is.EqualTo(0));
            Assert.That(Count(calls, "Actor", "TryGetAbility"), Is.EqualTo(1));
            Assert.That(Count(calls, "FSMTransition", "DoUpdate"), Is.EqualTo(0));
        }

        // 060016f6 (0x78EB90): base update first (true short-circuits), then the lookup, the
        // primary/secondary test with a non-interrupting reset, and finally the air-active flag.
        [Test]
        public void AirAbilityTransitionResetsALosingSecondaryBeforeReadingAirActive()
        {
            List<MethodBase> calls = Calls(typeof(CharacterTransition_AirAbility), "DoUpdate");
            int baseUpdate = IndexOf(calls, "FSMTransition", "DoUpdate");
            int lookup = IndexOf(calls, "Actor", "TryGetAbility");
            int secondary = IndexOf(calls, "CharacterAbility_Air", "IsSecondaryAndPrimaryActive");
            int reset = IndexOf(calls, "CharacterAbility_Air", "Reset");
            int active = IndexOf(calls, "CharacterAbility_Air", "get_AirActive");
            Assert.That(new[] { baseUpdate, lookup, secondary, reset, active }, Is.Ordered.Ascending.And.Unique);

            // 0x78EC36 (xor r15d,r15d) returns false straight after the reset, whose two arguments are false.
            MethodInfo method = typeof(CharacterTransition_AirAbility).GetMethods(Declared).Single(m => m.Name == "DoUpdate");
            var instructions = OriginalILCallSequence.Instructions(method);
            int resetCall = instructions.FindIndex(i => i.Offset == OriginalILCallSequence.Calls(method).Single(c => c.Target.Name == "Reset").Offset);
            Assert.That(instructions[resetCall - 1].Code, Is.EqualTo(OpCodes.Ldc_I4_0), "resetActiveCount");
            Assert.That(instructions[resetCall - 2].Code, Is.EqualTo(OpCodes.Ldc_I4_0), "interrupted");
            int next = resetCall + 1;
            while (instructions[next].Code == OpCodes.Nop) next++;
            Assert.That(instructions[next].Code, Is.EqualTo(OpCodes.Ldc_I4_0), "the losing secondary reports false instead of reading AirActive");
        }

        // 06001524 (0x7752A0): the air base update, then the buffered air control (only while the timer is
        // positive) and the timer write; 06001525 (0x775560) removes the rail flag before the timer.
        [Test]
        public void LedgeBufferStateRunsAirControlBetweenTheBaseUpdateAndTheTimerWrite()
        {
            List<MethodBase> update = Calls(typeof(CharacterState_AirLedgeBuffer), "DoUpdate");
            int baseUpdate = IndexOf(update, "CharacterState_Air", "DoUpdate");
            int read = IndexOf(update, "IGraphStorage", "GetValue");
            int control = IndexOf(update, "CharacterMovementUtilities", "ProcessAirControl");
            int write = IndexOf(update, "IGraphStorage", "SetValue");
            Assert.That(new[] { baseUpdate, read, control, write }, Is.Ordered.Ascending.And.Unique);

            List<MethodBase> leave = Calls(typeof(CharacterState_AirLedgeBuffer), "DoOnLeave");
            Assert.That(IndexOf(leave, "CharacterState_Air", "DoOnLeave"), Is.LessThan(IndexOf(leave, "IGraphStorage", "RemoveValue")));
            Assert.That(Count(leave, "IGraphStorage", "RemoveValue"), Is.EqualTo(2));
        }

        // 0600152a (0xE6C0A0): the effect base update, the sphere query, damage to every overlapped
        // IPlayerDamageable, the duration timer, and only then the grounded velocity strip and orientation.
        [Test]
        public void BurstUpdateHitsOverlapsBeforeStrippingGroundVelocity()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_BurstAttack<,>), "DoUpdate");
            int baseUpdate = IndexOf(calls, "ActorState_Effect", "DoUpdate");
            int target = IndexOf(calls, "CharacterAbility_BurstAttack", "GetTargetPosition");
            int sphere = IndexOf(calls, "Physics", "OverlapSphereNonAlloc");
            int component = IndexOf(calls, "Component", "TryGetComponent");
            int hit = IndexOf(calls, "IPlayerDamageable", "ReceiveHit");
            int timer = IndexOf(calls, "IGraphStorageExtensions", "AdjustValue");
            int grounded = IndexOf(calls, "CharacterTracking", "IsOnGround");
            int normal = IndexOf(calls, "CharacterTracking", "GetContactNormal");
            int velocity = IndexOf(calls, "Actor", "SetWorldVelocity");
            int orientate = IndexOf(calls, "Character", "OrientateToPlane");
            Assert.That(new[] { baseUpdate, target, sphere, component, hit, timer, grounded, normal, velocity, orientate }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(calls, "Character", "get_ActiveTracking"), Is.EqualTo(2), "read again for the normal (0xE6C3BF, 0xE6C3DE)");
        }

        // 06001528 (0xE6BDA0): after the effect entry the horizontal velocity is kept, the timer restarts, and the
        // boost then chaos stamina are activated; 0600152b (0xE6C480) starts the cooldown before clearing the timer.
        [Test]
        public void BurstEntryKeepsHorizontalVelocityThenSpendsBoostThenChaosStamina()
        {
            List<MethodBase> enter = Calls(typeof(CharacterState_BurstAttack<,>), "DoOnEnter");
            int baseEnter = IndexOf(enter, "ActorState_Ability", "DoOnEnter");
            int strip = IndexOf(enter, "Actor", "SetLocalVelocity");
            int timer = IndexOf(enter, "IGraphStorage", "SetValue");
            int boost = IndexOf(enter, "Character", "get_BoostStamina");
            int chaos = IndexOf(enter, "Character", "get_ChaosStamina");
            Assert.That(new[] { baseEnter, strip, timer, boost, chaos }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(enter, "CharacterState_BurstAttack", "ActivateStamina"), Is.EqualTo(2));

            List<MethodBase> stamina = Calls(typeof(CharacterState_BurstAttack<,>), "ActivateStamina");
            Assert.That(IndexOf(stamina, "CharacterStamina", "Activate"), Is.LessThan(IndexOf(stamina, "CharacterStamina", "ExternalConsumeStaminaCost")));

            List<MethodBase> leave = Calls(typeof(CharacterState_BurstAttack<,>), "DoOnLeave");
            Assert.That(IndexOf(leave, "CharacterAbility_BurstAttack", "TryStartCooldown"), Is.LessThan(IndexOf(leave, "IGraphStorage", "RemoveValue")));
        }

        // 06001531 (0xE6BA40): the chaos stamina is judged before the base entry spends it; deactivation is
        // checked afterwards, both callbacks sweep the chaos abilities, and the time dilation starts last.
        // The two lambdas test IsTimeDilationActive (0xB05210) and TryStopTimeDilation (0xB05140) respectively.
        [Test]
        public void ChaosEntryJudgesStaminaBeforeTheBaseEntryAndStartsDilationLast()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_BurstAttackChaos<,>), "DoOnEnter");
            int stamina = IndexOf(calls, "CharacterAbility_BurstAttack", "EnoughChaosStamina");
            int baseEnter = IndexOf(calls, "CharacterState_BurstAttack", "DoOnEnter");
            int prevent = IndexOf(calls, "CharacterState_BurstAttackChaos", "PreventDeactivation");
            int first = IndexOf(calls, "Actor", "GetAbilitiesWithInterface");
            int second = IndexOf(calls, "Actor", "GetAbilitiesWithInterface", first + 1);
            int trigger = IndexOf(calls, "CharacterAbility_BurstAttackChaos", "TriggerTimeDilation");
            Assert.That(new[] { stamina, baseEnter, prevent, first, second, trigger }, Is.Ordered.Ascending.And.Unique);

            List<List<MethodBase>> lambdas = typeof(CharacterState_BurstAttackChaos<,>).GetNestedTypes(BindingFlags.NonPublic)
                .SelectMany(nested => nested.GetMethods(Declared))
                .Where(method => method.Name.StartsWith("<DoOnEnter>", StringComparison.Ordinal))
                .Select(method => OriginalILCallSequence.Calls(method).Select(call => call.Target).ToList())
                .ToList();
            Assert.That(lambdas, Has.Count.EqualTo(2));
            Assert.That(lambdas.Count(l => Count(l, "IAbilityBurstAttackChaos", "IsTimeDilationActive") == 1), Is.EqualTo(1));
            Assert.That(lambdas.Count(l => Count(l, "IAbilityBurstAttackChaos", "TryStopTimeDilation") == 1), Is.EqualTo(1));
        }

        // 0600043f (0x528290) keeps only the exception names DataManager.AbilityDefinitions resolves.
        // 06000441 (0x528620) and its lambda 06000442 (0x5286D0) walk the actor's abilities: a passive
        // definition (+0x30, tested at 0x528767) or one whose ScriptType an exception shares (List.Exists,
        // 0x5287AA) is kept; everything else is left (ActorAbility.Leave, tail call at 0x5287C9).
        [Test]
        public void DisableAbilitiesStateLeavesEveryNonPassiveNonExceptedAbility()
        {
            using (var characters = new OwnedCharacterManagers())
            {
                var plain = Own(ScriptableObject.CreateInstance<ProbeAbilityDefinition>());
                var passive = Own(ScriptableObject.CreateInstance<ProbeAbilityDefinition>());
                passive.Passive = true;
                var excepted = Own(ScriptableObject.CreateInstance<OtherProbeAbilityDefinition>());
                // The exception is a different asset: only its ScriptType has to match.
                var exception = Own(ScriptableObject.CreateInstance<OtherProbeAbilityDefinition>());
                string key = "Lucid-ability-" + Guid.NewGuid().ToString("N");
                Dictionary<string, AbilityDefinition> definitions = ProcessManager.GetSystemRef<App>().Get().DataManager.AbilityDefinitions;
                definitions.Add(key, exception);
                try
                {
                    ProbeActor actor = NewProbeActor();
                    DefinedProbeAbility Add(ActorAbilityType type, AbilityDefinition definition)
                    {
                        var ability = new DefinedProbeAbility(definition);
                        ability.Enter();
                        actor.AddAbility(type, ability);
                        return ability;
                    }
                    DefinedProbeAbility left = Add(ActorAbilityType.Character_AirStompAttack, plain);
                    DefinedProbeAbility kept = Add(ActorAbilityType.Character_BrakingRunning, passive);
                    DefinedProbeAbility spared = Add(ActorAbilityType.Character_BurstAttackGround, excepted);

                    // A name with no registered definition is skipped by the constructor, not stored as null.
                    IFSMState state = ActorState_TriggerDisableAbilities.ConstructInstance(Machine(), new FSMIdentifier(Name()),
                        "{\"ExceptionList\":[\"" + key + "\",\"Lucid-missing-" + Guid.NewGuid().ToString("N") + "\"]}");
                    Assert.That(typeof(ActorState_TriggerDisableAbilities).GetField("m_exceptionList", Declared).GetValue(state), Is.EqualTo(new AbilityDefinition[] { exception }));

                    typeof(ActorState_TriggerDisableAbilities).GetMethod("InvokeTriggerStart", Declared).Invoke(state, new object[] { actor, null });

                    Assert.That(left.Enabled, Is.False, "a non-passive ability outside the exceptions is left");
                    Assert.That(left.Log, Is.EqualTo(new[] { "Enter", "Leave" }));
                    Assert.That(kept.Enabled, Is.True, "a passive ability is kept");
                    Assert.That(kept.Log, Is.EqualTo(new[] { "Enter" }));
                    Assert.That(spared.Enabled, Is.True, "an ability sharing an exception's ScriptType is kept");
                    Assert.That(spared.Log, Is.EqualTo(new[] { "Enter" }));
                }
                finally
                {
                    definitions.Remove(key);
                }
            }
        }

        private sealed class DefinedProbeAbility : ActorAbility
        {
            private readonly AbilityDefinition m_definition;
            public readonly List<string> Log = new List<string>();
            public DefinedProbeAbility(AbilityDefinition definition) => m_definition = definition;
            public override AbilityDefinition DefinitionBase => m_definition;
            protected override void DoOnEnter() => Log.Add("Enter");
            protected override void DoOnLeave() => Log.Add("Leave");
        }

        private sealed class OtherProbeAbilityDefinition : AbilityDefinition
        {
            public override Type ScriptType => typeof(DefinedProbeAbility);
        }
    }
}
