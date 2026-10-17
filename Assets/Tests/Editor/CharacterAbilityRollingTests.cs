using System;
using System.Collections;
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
            var registry = (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            string appName = ProcessManager.GetDefaultName<App>();
            // The real constructor's CharacterAbility initializer creates this
            // App row; only an absent row can be owned and removed afterwards.
            VerificationFixtureOwnership.RequireAbsent(registry, appName);
            object ownedRow = null;
            Exception primary = null;
            try
            {
                var ability = new CharacterAbility_Rolling();
                ownedRow = registry[appName];
                Assert.IsNotNull(ownedRow, "Construction creates the App reference row.");

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
            }
            catch (Exception failure) { primary = failure; throw; }
            finally
            {
                // A constructor that throws after its initializer created the
                // App row still leaves that row to this owner: it was absent
                // just before construction and nothing else ran in between.
                // The lookup never creates a row; ReleaseNullRow still refuses
                // any row that gained a system or observer.
                if (ownedRow == null) ownedRow = registry[appName];
                if (ownedRow != null && ReferenceEquals(registry[appName], ownedRow))
                {
                    try { ReleaseNullRow(registry, appName, ownedRow); }
                    catch (Exception cleanup) when (primary != null)
                    {
                        // Keep the body failure as the result; report cleanup separately.
                        TestContext.Error.WriteLine("Rolling fixture cleanup failed after a body failure: " + cleanup);
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

        // Remove the fixture-created row only while every reference in it is
        // still empty; a row that gained a system or observer is foreign state.
        private static void ReleaseNullRow(IDictionary registry, string name, object row)
        {
            var untyped = (SystemRef)row.GetType().GetField("SystemRef").GetValue(row);
            var cache = (IDictionary)row.GetType().GetField("SystemRefDictionary").GetValue(row);
            if (!untyped.IsNull()) throw new InvalidOperationException("Fixture App row gained a system: " + name);
            foreach (DictionaryEntry entry in cache)
            {
                Type type = entry.Value.GetType();
                while (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(SystemRef<>)) type = type.BaseType;
                foreach (string field in new[] { "m_system", "m_isystem", "OnSystemStartup", "OnSystemShutdown", "m_actionOnSystemValid" })
                    if (type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly).GetValue(entry.Value) != null)
                        throw new InvalidOperationException("Fixture App row reference retains state: " + field);
            }
            registry.Remove(name);
        }
    }
}
