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
    // Truth tables for four Character FSM transitions, with expected values from the 1.10.1
    // x86-64 bodies cited per test. The Character comes from its real constructor through
    // AddComponent; in EditMode its Awake and startup never run. The fixture supplies only what
    // the bodies read: the FSMStorage(0) that Actor.Awake creates, the genuine modifier store
    // from InitialiseModifiers, and a CharacterCollider from its real constructor.
    // FSMTransition.DoUpdate always returns false, so the base-exit branch cannot be reached here.
    // GetValue(key, default, true) stores the default on a miss, so a key that stays absent
    // after an update shows the body never read it.
    public sealed class OriginalCharacterExitTransitionTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // 06001703 (0x78F430): a null cached ability exits (0x78F561 je to the epilogue with
        // BL=1); otherwise the result is AirActive == 0 (0x78F566 cmp / 0x78F56D sete).
        [Test]
        public void AirAbilityExitLeavesWhenTheCachedAbilityIsMissingOrInactive()
        {
            WithCharacter((character, storage) =>
            {
                FSMTransition exit = Transition(CharacterTransition_AirAbilityExit.ConstructInstance);
                Assert.IsTrue(exit.Update(character, default), "missing ability exits");
                Assert.IsTrue(storage.GetCollection().ContainsKey(ActorFSMKeys.AirAbilityActive), "the miss stores its default");
                Assert.IsNull(storage.GetValueOnly<CharacterAbility_Air>(ActorFSMKeys.AirAbilityActive));
                Assert.IsTrue(exit.Update(character, default), "stored null ability exits");

                var ability = new CharacterAbility_Air();
                storage.SetValue(ActorFSMKeys.AirAbilityActive, ability);
                Assert.IsTrue(exit.Update(character, default), "inactive ability exits");
                SetProperty(ability, nameof(CharacterAbility_Air.AirActive), true);
                Assert.IsFalse(exit.Update(character, default), "active ability stays");
            });
        }

        // 06001731 (0x790D40): IsDirectionOverrideActive (0x790DC0) jumps to the epilogue with
        // R13B=1 before any storage read. HalfPipeColliderActive set returns 0 (0x790E96) without
        // reading HalfPipeTransferActive; otherwise the result is !HalfPipeTransferActive (0x790F64).
        [Test]
        public void HalfPipeAirExitLeavesOnDirectionOverrideBeforeReadingStorage()
        {
            WithCharacter((character, storage) =>
            {
                FSMTransition exit = Transition(CharacterTransition_HalfPipeAirExit.ConstructInstance);

                storage.SetValue(ActorFSMKeys.HalfPipeColliderActive, true);
                Assert.IsFalse(exit.Update(character, default), "active collider stays");
                Assert.IsFalse(storage.GetCollection().ContainsKey(ActorFSMKeys.HalfPipeTransferActive), "transfer is not read");

                storage.SetValue(ActorFSMKeys.HalfPipeColliderActive, false);
                storage.SetValue(ActorFSMKeys.HalfPipeTransferActive, true);
                Assert.IsFalse(exit.Update(character, default), "active transfer stays");
                storage.SetValue(ActorFSMKeys.HalfPipeTransferActive, false);
                Assert.IsTrue(exit.Update(character, default), "neither flag exits");

                storage.Clear();
                Assert.IsTrue(exit.Update(character, default), "missing flags read as false and exit");
                AssertStoredFalse(storage, ActorFSMKeys.HalfPipeColliderActive);
                AssertStoredFalse(storage, ActorFSMKeys.HalfPipeTransferActive);

                storage.Clear();
                AddDirectionOverride(character);
                Assert.IsTrue(exit.Update(character, default), "direction override exits");
                Assert.IsFalse(storage.GetCollection().ContainsKey(ActorFSMKeys.HalfPipeColliderActive), "collider is not read");
                Assert.IsFalse(storage.GetCollection().ContainsKey(ActorFSMKeys.HalfPipeTransferActive), "transfer is not read");
                storage.SetValue(ActorFSMKeys.HalfPipeColliderActive, true);
                storage.SetValue(ActorFSMKeys.HalfPipeTransferActive, true);
                Assert.IsTrue(exit.Update(character, default), "direction override exits over both flags");
            });
        }

        // 06001737 (0x7911E0): Collider.HasTrackContact (0x791260) and then
        // IsDirectionOverrideActive (0x791272) each jump to the epilogue with BL=1; only then is
        // the result !HalfPipeTransferActive (0x791343).
        // Contact is raised through the collider's genuine OnColliderContactIgnored handler.
        [Test]
        public void HalfPipeTransferExitLeavesOnContactOrDirectionOverrideBeforeReadingTransfer()
        {
            WithCharacter((character, storage) =>
            {
                FSMTransition exit = Transition(CharacterTransition_HalfPipeTransferExit.ConstructInstance);

                storage.SetValue(ActorFSMKeys.HalfPipeTransferActive, true);
                Assert.IsFalse(exit.Update(character, default), "active transfer stays");
                storage.SetValue(ActorFSMKeys.HalfPipeTransferActive, false);
                Assert.IsTrue(exit.Update(character, default), "inactive transfer exits");
                storage.Clear();
                Assert.IsTrue(exit.Update(character, default), "missing transfer reads as false and exits");
                AssertStoredFalse(storage, ActorFSMKeys.HalfPipeTransferActive);

                storage.Clear();
                AddDirectionOverride(character);
                Assert.IsTrue(exit.Update(character, default), "direction override exits");
                Assert.IsFalse(storage.GetCollection().ContainsKey(ActorFSMKeys.HalfPipeTransferActive), "transfer is not read");
            });

            WithCharacter((character, storage) =>
            {
                FSMTransition exit = Transition(CharacterTransition_HalfPipeTransferExit.ConstructInstance);
                typeof(CharacterCollider).GetMethod("OnColliderContactIgnored", Instance).Invoke(character.Collider, null);
                Assert.IsTrue(character.Collider.HasTrackContact());
                Assert.IsFalse(character.IsDirectionOverrideActive());
                Assert.IsTrue(exit.Update(character, default), "track contact exits");
                Assert.IsFalse(storage.GetCollection().ContainsKey(ActorFSMKeys.HalfPipeTransferActive), "transfer is not read");
                storage.SetValue(ActorFSMKeys.HalfPipeTransferActive, true);
                Assert.IsTrue(exit.Update(character, default), "track contact exits over an active transfer");
            });
        }

        // 0600179c (0x794590): IsOutOfBounds is read (0x79469E) with no branch on its result,
        // then InHardFailureState (0x79474E), and the two are ORed (0x794751). A short-circuit
        // would skip storing the missing InHardFailureState default when out of bounds.
        [Test]
        public void ShouldRespawnReadsBothFlagsBeforeCombiningThem()
        {
            WithCharacter((character, storage) =>
            {
                FSMTransition respawn = Transition(CharacterTransition_ShouldRespawn.ConstructInstance);

                storage.SetValue(ActorFSMKeys.IsOutOfBounds, true);
                Assert.IsTrue(respawn.Update(character, default), "out of bounds respawns");
                AssertStoredFalse(storage, ActorFSMKeys.InHardFailureState); // hard failure is still read

                storage.Clear();
                Assert.IsFalse(respawn.Update(character, default), "missing flags read as false");
                AssertStoredFalse(storage, ActorFSMKeys.IsOutOfBounds);
                AssertStoredFalse(storage, ActorFSMKeys.InHardFailureState);
                storage.SetValue(ActorFSMKeys.InHardFailureState, true);
                Assert.IsTrue(respawn.Update(character, default), "hard failure respawns");
                storage.SetValue(ActorFSMKeys.IsOutOfBounds, true);
                Assert.IsTrue(respawn.Update(character, default), "both flags respawn");
            });
        }

        private static FSMTransition Transition(Func<FiniteStateMachine, FSMIdentifier, string, IFSMTransition> construct)
        {
            string name = "Lucid-exit-" + Guid.NewGuid().ToString("N");
            return (FSMTransition)construct(new FiniteStateMachine(name, skipAddToManager: true), name, null);
        }

        private static void AssertStoredFalse(FSMStorage storage, GraphStorageKey key)
        {
            Assert.IsTrue(storage.GetCollection().ContainsKey(key), "the miss stores its default");
            Assert.IsFalse(storage.GetValueOnly<bool>(key));
        }

        private static void AddDirectionOverride(Character character)
        {
            Assert.IsFalse(character.IsDirectionOverrideActive(), "the base override is zero");
            character.AddModifierOverride((int)DirectionModifierType.Override, Vector3.forward);
            Assert.IsTrue(character.IsDirectionOverrideActive());
        }

        private static IDictionary Registry =>
            (IDictionary)typeof(ProcessManager).GetField("s_systemDictionary", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        // Character's field initializer creates the empty GameplayIslandManager row and
        // CharacterAbility's creates the empty App row. Both must be absent beforehand so the
        // fixture owns them; cleanup removes them only while they are still empty.
        private static void WithCharacter(Action<Character, FSMStorage> body)
        {
            IDictionary registry = Registry;
            var owned = new[] { ProcessManager.GetDefaultName<GameplayIslandManager>(), ProcessManager.GetDefaultName<App>() };
            foreach (string name in owned) VerificationFixtureOwnership.RequireAbsent(registry, name);
            var before = new HashSet<string>();
            foreach (string key in registry.Keys) before.Add(key);
            var gameObject = new GameObject("Lucid-exit-character");
            Exception primary = null;
            try
            {
                Character character = gameObject.AddComponent<Character>();
                var storage = new FSMStorage(0);
                SetProperty(character, nameof(Actor.Storage), storage);
                typeof(Character).GetMethod("InitialiseModifiers", Instance).Invoke(character, null);
                SetProperty(character, nameof(Character.Settings), new CharacterSettings { Collider = new CharacterSettings.ColliderSettings() });
                SetProperty(character, nameof(Character.Collider), new CharacterCollider(character, null, null));
                body(character, storage);
                foreach (string key in registry.Keys)
                    if (!before.Contains(key) && Array.IndexOf(owned, key) < 0)
                        Assert.Fail("Fixture created an unowned registry row: " + key);
            }
            catch (Exception failure) { primary = failure; throw; }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                // Every owned row is released even when an earlier one refuses, so a refused
                // GameplayIslandManager row cannot leave the App row behind for later fixtures.
                var cleanupFailures = new List<Exception>();
                foreach (string name in owned)
                {
                    if (!registry.Contains(name)) continue;
                    try { VerificationFixtureOwnership.ReleaseEmptyRow(registry, name, registry[name]); }
                    catch (Exception cleanup) { cleanupFailures.Add(cleanup); }
                }
                if (cleanupFailures.Count > 0)
                {
                    var cleanupFailure = new AggregateException("Exit transition fixture cleanup failed", cleanupFailures);
                    if (primary == null) throw cleanupFailure;
                    TestContext.Error.WriteLine("Exit transition fixture cleanup failed after a body failure: " + cleanupFailure);
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
