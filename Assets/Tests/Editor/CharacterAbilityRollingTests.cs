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
