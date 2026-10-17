using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class CharacterAbilityRollingTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Original 060011c5 (x86-64 0x74B370) calls ActorAbility.DoOnLeave, then
        // stores one zero word at +0xE8, clearing IsRollingTriggered (+0xE8) and
        // IsRollingExiting (+0xE9) together. Fails if either reset is dropped or
        // if leaving is rewritten as UpdateTrigger(false), which would keep
        // IsRollingExiting set after a triggered roll.
        [Test]
        public void LeaveClearsTriggeredAndExitingFlags()
        {
            WithOwnedAppRow(ability =>
            {
                foreach (bool triggered in new[] { true, false })
                {
                    Set(ability, "Enabled", true);
                    Set(ability, nameof(CharacterAbility_Rolling.IsRollingTriggered), triggered);
                    Set(ability, nameof(CharacterAbility_Rolling.IsRollingExiting), true);
                    ability.Leave();
                    Assert.IsFalse(ability.Enabled);
                    Assert.IsFalse(ability.IsRollingTriggered, "Leave clears the trigger (triggered=" + triggered + ").");
                    Assert.IsFalse(ability.IsRollingExiting, "Leave clears exiting (triggered=" + triggered + ").");
                    Assert.IsFalse(ability.IsTriggered);
                }
            });
        }

        // Original 060011c1 (x86-64 0x74B100) returns true when
        // IsRollingTriggered (+0xE8) is set, otherwise IsRollingExiting (+0xE9).
        // The flags are set directly here; in the original, UpdateTrigger
        // (0x74B1A0) produces (false, true) when a triggered roll is released.
        // That state is the negative control: a getter that returns only
        // IsRollingTriggered reports false there. The IAbilityTrigger read
        // checks interface dispatch only, not ActorTransition_AbilityTriggered.
        [Test]
        public void IsTriggeredReportsTriggeredOrExiting()
        {
            WithOwnedAppRow(ability =>
            {
                foreach (bool triggered in new[] { false, true })
                foreach (bool exiting in new[] { false, true })
                {
                    Set(ability, nameof(CharacterAbility_Rolling.IsRollingTriggered), triggered);
                    Set(ability, nameof(CharacterAbility_Rolling.IsRollingExiting), exiting);
                    string state = "(triggered=" + triggered + ", exiting=" + exiting + ")";
                    Assert.AreEqual(triggered || exiting, ability.IsTriggered, "Direct getter " + state);
                    Assert.AreEqual(triggered || exiting, ((IAbilityTrigger)ability).IsTriggered, "IAbilityTrigger getter " + state);
                }
            });
        }

        // Original base UpdateForwardSpeed (06000ff4, x86-64 0x739500) keeps the
        // modifier-adjusted SpeedMin at [rbp-0x3c] and the input-scaled minimum in
        // xmm4 (0x7395c6..0x7395f3, maxss with SpeedMin only when ClampToSpeedMin).
        // Positive acceleration compares and caps against [rbp-0x3c] (0x7396cb,
        // 0x7396e2); the curve ratio (0x739609), direction fallback (0x7396f2) and
        // deceleration floor (0x73975a) use xmm4. CharacterAbility_Rolling
        // inherits the base body. Every case uses deltaTime 1 and zero SpeedCap.

        // fs 0, SpeedMin 10, SpeedMax 20, input 0.5: the effective minimum is 5 but
        // acceleration 10 reaches the configured 10. Capping at the effective
        // minimum gives 5.
        [Test]
        public void ForwardSpeedAcceleratesToConfiguredMinimumUnderPartialInput()
        {
            WithOwnedAppRow(ability =>
            {
                var motion = ForwardMotion(clampToSpeedMin: false, acceleration: 10f);
                Assert.AreEqual(10f, UpdateForwardSpeed(ability, motion, 0f, 0.5f).Speed, 1e-5f);
            });
        }

        // A modifier lowering speedMin to 8 sets the cap: effective 4, configured
        // 8, motion.SpeedMin 10. Expect 8, not 4 (effective) or 10 (unmodified).
        [Test]
        public void ForwardSpeedCapIsTheModifierAdjustedMinimum()
        {
            WithOwnedAppRow(ability =>
            {
                ability.m_modifierSpeed = (ref float acceleration, ref float speedMin, ref float speedMax,
                    ref float deceleration) => speedMin = 8f;
                var motion = ForwardMotion(clampToSpeedMin: false, acceleration: 10f);
                Assert.AreEqual(8f, UpdateForwardSpeed(ability, motion, 0f, 0.5f).Speed, 1e-5f);
            });
        }

        // fs 5, input 0.5: the curve t => t is sampled at 5 / effective 5 = 1, so
        // acceleration 4 gives min(5 + 4, 10) = 9. A configured ratio (0.5) gives
        // 7; an effective cap gives 5.
        [Test]
        public void ForwardSpeedCurveRatioUsesTheEffectiveMinimum()
        {
            WithOwnedAppRow(ability =>
            {
                var motion = ForwardMotion(clampToSpeedMin: false, acceleration: 4f);
                var curve = UnityEngine.AnimationCurve.Linear(0f, 0f, 2f, 2f);
                Assert.AreEqual(9f, UpdateForwardSpeed(ability, motion, 5f, 0.5f, curve).Speed, 1e-5f);
            });
        }

        // ClampToSpeedMin with input 2: effective max(20, 10) = 20. Acceleration
        // 15 stops at the configured 10 (an effective cap gives 15), and 10 < 20
        // still applies the zero-direction fallback. The direction assertion
        // guards against comparing with the configured minimum instead.
        [Test]
        public void ClampedForwardSpeedStopsAtConfiguredMinimumAndFallsBackBelowTheEffectiveOne()
        {
            WithOwnedAppRow(ability =>
            {
                var motion = ForwardMotion(clampToSpeedMin: true, acceleration: 15f);
                var result = UpdateForwardSpeed(ability, motion, 0f, 2f);
                Assert.AreEqual(10f, result.Speed, 1e-5f);
                Assert.AreEqual(UnityEngine.Vector3.forward, result.Direction);
            });
        }

        // Negative control for the floor: with zero acceleration, deceleration 10
        // and input 0.5, fs 15 falls to 5 but stops at the effective 7.5. It
        // guards against flooring at the configured 10; a body that uses the
        // effective minimum everywhere also passes it.
        [Test]
        public void ForwardSpeedDecelerationFloorIsTheEffectiveMinimum()
        {
            WithOwnedAppRow(ability =>
            {
                var motion = ForwardMotion(clampToSpeedMin: false, acceleration: 0f);
                motion.DecelerationCurve = UnityEngine.AnimationCurve.Constant(0f, 1f, 10f);
                Assert.AreEqual(7.5f, UpdateForwardSpeed(ability, motion, 15f, 0.5f).Speed, 1e-5f);
            });
        }

        private static CharacterAbilityDefinition.Motion ForwardMotion(bool clampToSpeedMin, float acceleration) =>
            new CharacterAbilityDefinition.Motion
            {
                ClampToSpeedMin = clampToSpeedMin,
                SpeedMin = 10f,
                SpeedMax = 20f,
                Acceleration = acceleration,
            };

        private static (float Speed, UnityEngine.Vector3 Direction) UpdateForwardSpeed(CharacterAbility ability,
            CharacterAbilityDefinition.Motion motion, float forwardSpeed, float effectiveInputMagnitude,
            UnityEngine.AnimationCurve accelerationCurve = null)
        {
            MethodInfo method = typeof(CharacterAbility).GetMethod("UpdateForwardSpeed", Instance);
            // The original Rolling and MovementGround<T> declare no override, so
            // Invoke must dispatch to the base body these cases specify.
            MethodInfo resolved = ability.GetType().GetMethod("UpdateForwardSpeed", Instance, null,
                Array.ConvertAll(method.GetParameters(), parameter => parameter.ParameterType), null);
            Assert.AreEqual(typeof(CharacterAbility), resolved.DeclaringType);
            var args = new object[]
            {
                forwardSpeed, UnityEngine.Vector3.zero, 1f, motion, 1f, effectiveInputMagnitude,
                accelerationCurve, true,
            };
            method.Invoke(ability, args);
            return ((float)args[0], (UnityEngine.Vector3)args[1]);
        }

        // Owned failure controls for the App row cleanup; they make no
        // original-behavior claim. Each failure is raised by the fixture itself
        // after the real constructor has created the App reference.
        [Test]
        public void OwnedFailureAfterAppReferenceCreationReleasesTheEmptyRowAndKeepsThePrimary()
        {
            var primary = new InvalidProgramException("owned failure after App reference creation");
            var reports = new List<string>();
            Assert.AreSame(primary, Assert.Throws<InvalidProgramException>(() => WithOwnedAppRow(_ => throw primary, reports.Add)));
            Assert.IsFalse(Registry.Contains(ProcessManager.GetDefaultName<App>()), "The empty owned row is released.");
            Assert.IsEmpty(reports);
        }

        [Test]
        public void ObservedAppReferenceIsRetainedAndReportedWithoutReplacingThePrimary()
        {
            string appName = ProcessManager.GetDefaultName<App>();
            var primary = new InvalidProgramException("owned failure with an observed App reference");
            var reports = new List<string>();
            SystemRef<App> typed = null;
            int calls = 0;
            Action<App> observer = _ => calls++;
            try
            {
                Assert.AreSame(primary, Assert.Throws<InvalidProgramException>(() => WithOwnedAppRow(_ =>
                {
                    // Stands in for foreign work on the row: cleanup must refuse
                    // to remove it rather than clear the observer.
                    typed = ProcessManager.GetSystemRef<App>();
                    typed.OnSystemShutdown += observer;
                    throw primary;
                }, reports.Add)));
                Assert.AreEqual(1, reports.Count, "The refused release is reported separately.");
                StringAssert.Contains("retains state", reports[0]);
                Assert.IsTrue(Registry.Contains(appName), "The observed row is preserved.");
                Assert.AreEqual(0, calls);
            }
            finally
            {
                // Remove only this control's observer, then its now-empty row.
                if (typed != null) typed.OnSystemShutdown -= observer;
                object row = Registry[appName];
                if (row != null) VerificationFixtureOwnership.ReleaseEmptyRow(Registry, appName, row);
            }
            Assert.IsFalse(Registry.Contains(appName));
        }

        private static IDictionary Registry =>
            (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        // The real constructor's CharacterAbility initializer creates the App
        // row; only an absent row can be owned and removed afterwards. A body
        // failure stays the result, and a cleanup failure after it goes to the
        // reporter instead.
        private static void WithOwnedAppRow(Action<CharacterAbility_Rolling> body, Action<string> report = null)
        {
            IDictionary registry = Registry;
            string appName = ProcessManager.GetDefaultName<App>();
            VerificationFixtureOwnership.RequireAbsent(registry, appName);
            object ownedRow = null;
            Exception primary = null;
            try
            {
                var ability = new CharacterAbility_Rolling();
                ownedRow = registry[appName];
                Assert.IsNotNull(ownedRow, "Construction creates the App reference row.");
                body(ability);
            }
            catch (Exception failure) { primary = failure; throw; }
            finally
            {
                // A constructor that throws after its initializer created the
                // App row still leaves that row to this owner: it was absent
                // just before construction and nothing else ran in between.
                // The lookup never creates a row; ReleaseEmptyRow still refuses
                // any row that gained a system or observer.
                if (ownedRow == null) ownedRow = registry[appName];
                if (ownedRow != null && ReferenceEquals(registry[appName], ownedRow))
                {
                    try { VerificationFixtureOwnership.ReleaseEmptyRow(registry, appName, ownedRow); }
                    catch (Exception cleanup) when (primary != null)
                    {
                        string message = "Rolling fixture cleanup failed after a body failure: " + cleanup;
                        if (report != null) report(message);
                        else TestContext.Error.WriteLine(message);
                    }
                }
            }
        }

        // Private setters are read from the declaring type (ActorAbility owns Enabled).
        private static void Set(object target, string property, bool value)
        {
            Type owner = typeof(CharacterAbility_Rolling).GetProperty(property, Instance).DeclaringType;
            owner.GetProperty(property, Instance).GetSetMethod(true).Invoke(target, new object[] { value });
        }
    }
}
