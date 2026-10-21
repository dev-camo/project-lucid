using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CharacterAbilityDefinition_CatchRelease (TypeDef 0x020004c4, x86-64
    // 0x542B40..0x542BF3), CharacterAbility_CatchRelease (0x020002c2, 0x743690..0x744F10),
    // CatchReleaseTrigger (0x02000ac0, 0x6E5500..0x6E5EC9), CharacterState_CatchRelease (0x02000364,
    // 0x775D20..0x776090) and CharacterTransition_CatchReleaseInputTriggered (0x020003cd,
    // 0x78F850..0x78F9F0). The character is a real component with a real Rigidbody on owned scene
    // objects (a probe supplies the gravity vector the shipped override reads from a component Edit
    // Mode does not build); the ability is built by its real constructor and given its definition
    // through the real Initialise, so only the character's link is assigned. The next-frame host of
    // CoroutineUtils is absent in Edit Mode, which makes scheduling the last statement of an exit or
    // reset: those cases run with the static host cleared and read everything before the fault. Not covered:
    // the release transform an exit applies (SetCharacterReleaseTransform 0x7442C0, its multiplier, world
    // speed and fixed-point offsets, and the fake-parent placement before it). DoTriggerExit applies it only
    // when AreModifiersBlocked is false, which needs Actor.Initialise to have set m_initialised, and that
    // cannot run here (its TimeManager subscription needs an awakened TimeManager and its FSM an FSMManager).
    public sealed class CharacterCatchReleaseTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // Far from anything a scene may hold, so rays and bounds only meet the fixture.
        private static readonly Vector3 Origin = new Vector3(200f, 200f, 200f);

        private static string Name() => "Lucid-catch-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);

        private sealed class CatchScene : IDisposable
        {
            private readonly List<IDisposable> m_leases = new List<IDisposable>();
            public readonly OwnedWorldObjects Objects = new OwnedWorldObjects("lucid-catch-release");
            public readonly CharacterGravityVectorProbe Character;
            public readonly Rigidbody Body;
            public readonly FSMStorage Storage = new FSMStorage(0);
            public readonly BoxCollider Trigger;
            public readonly CharacterAbilityDefinition_CatchRelease Definition;
            public readonly CharacterAbility_CatchRelease Ability;

            public CatchReleaseDefinition Data => Definition.Data;

            // The character's field initialiser leases the GameplayIslandManager reference and the ability's
            // the App reference; both rows must be absent so the fixture owns them.
            public CatchScene()
            {
                try
                {
                    Row(ProcessManager.GetDefaultName<GameplayIslandManager>());
                    Row(ProcessManager.GetDefaultName<App>());
                    GameObject host = Objects.Create("character", Origin, Quaternion.identity);
                    Character = host.AddComponent<CharacterGravityVectorProbe>();
                    Body = host.AddComponent<Rigidbody>();
                    Set(Character, "m_body", Body);
                    SetActorProperty(Character, "Storage", Storage);
                    Trigger = Objects.Create("trigger", Origin, Quaternion.identity).AddComponent<BoxCollider>();
                    Definition = Objects.Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_CatchRelease>());
                    Definition.Data = new CatchReleaseDefinition();
                    Ability = new CharacterAbility_CatchRelease();
                    Ability.Initialise(null, Definition);
                    Set(Ability, "m_character", Character);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            private void Row(string name) => m_leases.Add(new VerificationFixtureOwnership.LifecycleRow(name));

            public void Lease(IDisposable lease) => m_leases.Add(lease);

            // The character keeps the velocity in its body; the magnitude and direction caches follow.
            public void Move(Vector3 velocity) => Character.SetWorldVelocity(velocity);

            public void AddToCharacter(ActorAbilityType type, ActorAbility ability) =>
                Get<IDictionary>(Character, "m_abilities").Add(type, ability);

            // Back to an idle ability and an enabled trigger.
            public void Reset()
            {
                Ability.Leave();
                Trigger.enabled = true;
                Storage.RemoveValue<bool>(ActorFSMKeys.CatchReleaseActive);
                Storage.RemoveValue<float>(ActorFSMKeys.StateRemainTimer);
            }

            public bool Active => Storage.GetCollection().ContainsKey(ActorFSMKeys.CatchReleaseActive);

            public void Dispose()
            {
                Objects.Dispose();
                for (int i = m_leases.Count - 1; i >= 0; i--) m_leases[i].Dispose();
                m_leases.Clear();
            }
        }

        private static List<float> Record(UnityEvent<float> unityEvent)
        {
            var values = new List<float>();
            unityEvent.AddListener(values.Add);
            return values;
        }

        // ---- CharacterAbilityDefinition_CatchRelease ---------------------------------------------

        // The constructor 0x542BE0 stores 1.0 (0x3f800000) at +0x50, the shared GravityMultiplier initialiser,
        // before chaining to the ability-definition base.
        [Test]
        public void DefinitionStartsWithAUnitGravityMultiplier()
        {
            var definition = ScriptableObject.CreateInstance<CharacterAbilityDefinition_CatchRelease>();
            try
            {
                Assert.That(definition.GravityMultiplier, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        // CalculateCachedValues 0x542BA0 runs the base, then stores cosf(AlignmentAngleTolerance * 0.017453292f)
        // (constant 0x29a6c18) into Data.Attach+0x48. A different tolerance gives a different cosine, so a
        // stale or constant store fails.
        [Test]
        public void DefinitionCachesTheCosineOfTheAttachAlignmentTolerance()
        {
            var definition = ScriptableObject.CreateInstance<CharacterAbilityDefinition_CatchRelease>();
            try
            {
                definition.Data = new CatchReleaseDefinition();
                foreach ((float degrees, float cosine) in new[] { (0f, 1f), (60f, 0.5f), (90f, 0f), (180f, -1f) })
                {
                    definition.Data.Attach.AlignmentAngleTolerance = degrees;
                    definition.CalculateCachedValues();
                    Assert.That(definition.Data.Attach.AlignmentCosAngleTolerance, Is.EqualTo(cosine).Within(1e-6f), "tolerance " + degrees);
                }
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        // The native body reads Data (+0xD0) and its Attach (+0x28) without a guard, after the base call.
        [Test]
        public void DefinitionCachingFaultsWithoutData()
        {
            var definition = ScriptableObject.CreateInstance<CharacterAbilityDefinition_CatchRelease>();
            try
            {
                definition.Data = null;
                Assert.Throws<NullReferenceException>(() => definition.CalculateCachedValues());
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        // Declaration-shape self-check (not a behaviour test): get_ScriptType 0x542B40 loads the ability's type handle.
        [Test]
        public void DeclarationShapeDefinitionNamesItsAbilityScript()
        {
            var definition = ScriptableObject.CreateInstance<CharacterAbilityDefinition_CatchRelease>();
            try
            {
                Assert.That(definition.ScriptType, Is.EqualTo(typeof(CharacterAbility_CatchRelease)));
                Assert.That(typeof(CharacterAbilityDefinition_CatchRelease).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition)));
                Assert.That(typeof(CharacterAbility_CatchRelease).BaseType, Is.EqualTo(typeof(CharacterAbility<CharacterAbilityDefinition_CatchRelease>)));
                Assert.That(typeof(CharacterAbility_CatchRelease).GetInterfaces(), Does.Contain(typeof(IAbilityTrigger)));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        // ---- CharacterTransition_CatchReleaseInputTriggered --------------------------------------

        // The class factory finds the transition by its static ConstructInstance and its argument type by the
        // attribute on the nested class, which keeps the original name.
        [Test]
        public void TransitionIsRegisteredWithItsArgumentType()
        {
            Assert.That(FSMClassFactory.Transitions.TryGetValue(nameof(CharacterTransition_CatchReleaseInputTriggered), out JSONFactoryClass factory), Is.True);
            Assert.That(factory.ClassType, Is.EqualTo(typeof(CharacterTransition_CatchReleaseInputTriggered)));
            Assert.That(factory.JSONCtorArgsType.Name, Is.EqualTo("CatchPointInputReleaseJSONCtorArgs"));
            Assert.That(factory.JSONCtorArgsType.DeclaringType, Is.EqualTo(typeof(CharacterTransition_CatchReleaseInputTriggered)));
            Assert.That(typeof(CharacterTransition_CatchReleaseInputTriggered).GetCustomAttribute<GraphNodeDefaultNameAttribute>(false).DefaultName,
                Is.EqualTo("CatchReleaseInputTriggered"));
            Assert.That(typeof(CharacterTransition_CatchReleaseInputTriggered).GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format,
                Is.EqualTo("Character/{0}"));
        }

        private static FSMTransition NewTransition(CatchInputReleaseType type, bool inverse) =>
            (FSMTransition)FSMClassFactory.ConstructTransition(Machine(), nameof(CharacterTransition_CatchReleaseInputTriggered), new FSMIdentifier(Name()),
                "{\"InputReleaseType\":" + (int)type + ",\"InverseCheck\":" + (inverse ? "true" : "false") + "}");

        // The constructor 0x78F850 copies the two argument fields to +0x1C (release type) and +0x20 (inverse flag).
        [Test]
        public void TransitionReadsTheReleaseTypeAndInverseFlagFromItsJson()
        {
            FSMTransition transition = NewTransition(CatchInputReleaseType.ToJump, true);
            Assert.That(Get<CatchInputReleaseType>(transition, "m_inputReleaseType"), Is.EqualTo(CatchInputReleaseType.ToJump));
            Assert.That(Get<bool>(transition, "m_inverseCheck"), Is.True);
            transition = NewTransition(CatchInputReleaseType.ToAir, false);
            Assert.That(Get<CatchInputReleaseType>(transition, "m_inputReleaseType"), Is.EqualTo(CatchInputReleaseType.ToAir));
            Assert.That(Get<bool>(transition, "m_inverseCheck"), Is.False);
        }

        // DoUpdate 0x78F920: base update first (false here), then Actor.TryGetAbility for ability type 0x98d7dbf2.
        // Without the ability it returns false (0x78F9C7) even when inverted; with it, the result is
        // HasInputReleaseType != inverse (0x78F9BC..0x78F9C5), and an ability without data reports no match.
        [Test]
        public void TransitionComparesTheAbilitysReleaseTypeAndInvertsOnlyWhenTheAbilityExists()
        {
            using (var scene = new CatchScene())
            {
                foreach (bool inverse in new[] { false, true })
                    Assert.That(NewTransition(CatchInputReleaseType.ToAir, inverse).Update(scene.Character, default), Is.False, "no ability, inverse " + inverse);

                scene.AddToCharacter(ActorAbilityType.Character_CatchRelease, scene.Ability);
                Assert.That(NewTransition(CatchInputReleaseType.ToAir, false).Update(scene.Character, default), Is.False, "no data");
                Assert.That(NewTransition(CatchInputReleaseType.ToAir, true).Update(scene.Character, default), Is.True, "no data, inverted");

                scene.Data.Release.InputType = CatchInputReleaseType.ToAir;
                Set(scene.Ability, "m_data", scene.Data);
                foreach ((CatchInputReleaseType wanted, bool inverse, bool expected) in new[]
                {
                    (CatchInputReleaseType.ToAir, false, true), (CatchInputReleaseType.ToJump, false, false),
                    (CatchInputReleaseType.ToAir, true, false), (CatchInputReleaseType.ToJump, true, true),
                    (CatchInputReleaseType.None, false, false), (CatchInputReleaseType.None, true, true),
                })
                    Assert.That(NewTransition(wanted, inverse).Update(scene.Character, default), Is.EqualTo(expected), wanted + " inverse " + inverse);
            }
        }

        // ---- CharacterAbility_CatchRelease -------------------------------------------------------

        // HasInputReleaseType 0x743D90: a null data (+0xC0) gives false before anything is read; otherwise
        // Release.InputType (+0x10) equals the argument.
        [Test]
        public void AbilityMatchesTheReleaseInputTypeOfItsDataOnly()
        {
            using (var scene = new CatchScene())
            {
                Assert.That(scene.Ability.HasInputReleaseType(CatchInputReleaseType.None), Is.False, "no data");
                scene.Data.Release.InputType = CatchInputReleaseType.ToJump;
                Set(scene.Ability, "m_data", scene.Data);
                Assert.That(scene.Ability.HasInputReleaseType(CatchInputReleaseType.ToJump), Is.True);
                Assert.That(scene.Ability.HasInputReleaseType(CatchInputReleaseType.ToAir), Is.False);
                Assert.That(scene.Ability.HasInputReleaseType(CatchInputReleaseType.None), Is.False);
            }
        }

        // DoOnEnter 0x7436E0 clears the triggered flag and the attach time (+0xD4); DoOnUpdate 0x743730 adds
        // the delta to the attach time only while triggered (0x743762 cmp byte [+0xB8]); DoOnLeave 0x743790
        // clears the flag. Both overrides also refresh the gravity multiplier from the definition.
        [Test]
        public void AttachTimeCountsOnlyWhileTriggered()
        {
            using (var scene = new CatchScene())
            {
                scene.Definition.GravityMultiplier = 3f;
                SetProperty(scene.Ability, "IsTriggered", true);
                Set(scene.Ability, "m_attachTime", 9f);
                scene.Ability.Enter();
                Assert.That(scene.Ability.IsTriggered, Is.False);
                Assert.That(Get<float>(scene.Ability, "m_attachTime"), Is.EqualTo(0f));
                Assert.That(scene.Ability.GravityMultiplier, Is.EqualTo(3f));

                Call(scene.Ability, "DoOnUpdate", (object)null, 0.25f);
                Assert.That(Get<float>(scene.Ability, "m_attachTime"), Is.EqualTo(0f), "not triggered");

                SetProperty(scene.Ability, "IsTriggered", true);
                Call(scene.Ability, "DoOnUpdate", (object)null, 0.25f);
                Call(scene.Ability, "DoOnUpdate", (object)null, 0.5f);
                Assert.That(Get<float>(scene.Ability, "m_attachTime"), Is.EqualTo(0.75f));

                scene.Ability.Leave();
                Assert.That(scene.Ability.IsTriggered, Is.False);
            }
        }

        private static void SetProperty(object target, string property, object value) =>
            target.GetType().GetProperty(property, Members).GetSetMethod(true).Invoke(target, new[] { value });

        private static bool TryEnter(CatchScene scene, Vector3 velocity)
        {
            scene.Move(velocity);
            scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
            bool entered = scene.Ability.IsTriggered;
            Assert.That(scene.Active, Is.EqualTo(entered), "the active flag follows the entry");
            Assert.That(scene.Trigger.enabled, Is.EqualTo(!entered), "an entry disables the trigger collider");
            if (entered) scene.Reset();
            return entered;
        }

        // DoTriggerEnter 0x7437B0 takes the speed along the trigger's forward (|v . dir|, 0x743863..0x7438c4)
        // unless UseSpeedMagnitude (+0x1A) is set, which takes the character's speed (+0xD0); a speed below
        // SpeedMin (+0x1C) returns before the ability is entered (ja 0x743cf9).
        [Test]
        public void EntryNeedsTheAlignedSpeedOrTheFullSpeedWhenTheMagnitudeIsUsed()
        {
            using (var scene = new CatchScene())
            {
                scene.Data.Attach.SpeedMin = 5f;
                Assert.That(TryEnter(scene, new Vector3(6f, 0f, 0f)), Is.False, "fast but sideways: no speed along the forward");
                Assert.That(TryEnter(scene, new Vector3(0f, 0f, 4f)), Is.False, "aligned but slow");
                Assert.That(TryEnter(scene, new Vector3(0f, 0f, 6f)), Is.True, "aligned and fast");
                Assert.That(TryEnter(scene, new Vector3(0f, 0f, -6f)), Is.True, "the aligned speed is absolute");

                scene.Data.Attach.UseSpeedMagnitude = true;
                Assert.That(TryEnter(scene, new Vector3(6f, 0f, 0f)), Is.True, "the full speed counts");
                Assert.That(TryEnter(scene, new Vector3(3f, 0f, 0f)), Is.False, "slow");
            }
        }

        // With an alignment tolerance (+0x2C > 0) the direction is the trigger's rotation applied to
        // AlignmentDirection and the normalised velocity must lie within the cosine cached from the tolerance
        // (0x7438d2..0x74390e: reject when cos > dot). A zero tolerance skips the check.
        [Test]
        public void EntryChecksTheAlignmentConeOnlyWhenAToleranceIsSet()
        {
            using (var scene = new CatchScene())
            {
                scene.Data.Attach.AlignmentDirection = Vector3.forward;
                scene.Data.Attach.AlignmentAngleTolerance = 30f;
                scene.Definition.CalculateCachedValues();
                Vector3 tenDegrees = Quaternion.Euler(0f, 10f, 0f) * Vector3.forward * 8f;
                Vector3 sixtyDegrees = Quaternion.Euler(0f, 60f, 0f) * Vector3.forward * 8f;
                Assert.That(TryEnter(scene, tenDegrees), Is.True, "inside the cone");
                Assert.That(TryEnter(scene, sixtyDegrees), Is.False, "outside the cone");

                scene.Data.Attach.AlignmentAngleTolerance = 0f;
                scene.Definition.CalculateCachedValues();
                Assert.That(TryEnter(scene, sixtyDegrees), Is.True, "no tolerance, no alignment check");
            }
        }

        // DoTriggerEnter rejects only when SpeedMin > speed (ucomiss + ja, 0x7438b0/0x7438c9) and only when
        // cos > dot (0x743906..0x74390e), so a speed equal to SpeedMin and a heading exactly on the cached
        // cosine still attach. The cosine is cached at tolerance 0 (cos 0 = 1) and the tolerance raised afterwards
        // without a new cache, so a heading along the direction (dot 1) sits exactly on the boundary.
        [Test]
        public void EntryAtTheSpeedMinimumAndOnTheConeBoundaryStillAttaches()
        {
            using (var scene = new CatchScene())
            {
                scene.Data.Attach.SpeedMin = 5f;
                Assert.That(TryEnter(scene, new Vector3(0f, 0f, 4.999f)), Is.False, "just under the minimum");
                Assert.That(TryEnter(scene, new Vector3(0f, 0f, 5f)), Is.True, "exactly the minimum");

                scene.Data.Attach.SpeedMin = 0f;
                scene.Data.Attach.AlignmentDirection = Vector3.forward;
                scene.Data.Attach.AlignmentAngleTolerance = 0f;
                scene.Definition.CalculateCachedValues();
                Assert.That(scene.Data.Attach.AlignmentCosAngleTolerance, Is.EqualTo(1f));
                scene.Data.Attach.AlignmentAngleTolerance = 30f;
                Assert.That(TryEnter(scene, new Vector3(0f, 0f, 8f)), Is.True, "dot equals the cached cosine");
                Assert.That(TryEnter(scene, Quaternion.Euler(0f, 10f, 0f) * Vector3.forward * 8f), Is.False, "dot below the cached cosine");
            }
        }

        // The tolerance also chooses the direction: a trigger turned 90 degrees makes AlignmentDirection a
        // sideways axis, while without a tolerance the trigger's own forward is used (0x743835 versus 0x743851).
        [Test]
        public void EntryUsesTheTriggersForwardWithoutAToleranceAndItsRotatedAlignmentDirectionWithOne()
        {
            using (var scene = new CatchScene())
            {
                scene.Trigger.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                scene.Data.Attach.SpeedMin = 5f;
                Assert.That(TryEnter(scene, new Vector3(6f, 0f, 0f)), Is.True, "forward is +x");
                Assert.That(TryEnter(scene, new Vector3(0f, 0f, 6f)), Is.False);

                scene.Data.Attach.AlignmentDirection = Vector3.forward;
                scene.Data.Attach.AlignmentAngleTolerance = 89f;
                scene.Definition.CalculateCachedValues();
                Assert.That(TryEnter(scene, new Vector3(6f, 0f, 0f)), Is.True, "the rotated alignment direction is +x");
                Assert.That(TryEnter(scene, new Vector3(0f, 0f, 6f)), Is.False);
            }
        }

        // An ability that is already triggered ignores a second entry before reading the definition (0x7437FE).
        [Test]
        public void EntryIsIgnoredWhileTriggered()
        {
            using (var scene = new CatchScene())
            {
                scene.Move(new Vector3(0f, 0f, 6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(scene.Ability.IsTriggered, Is.True);
                var other = new CatchReleaseDefinition();
                var otherTrigger = scene.Objects.Create("other trigger").AddComponent<BoxCollider>();
                scene.Ability.DoTriggerEnter(other, otherTrigger);
                Assert.That(Get<CatchReleaseDefinition>(scene.Ability, "m_data"), Is.SameAs(scene.Data));
                Assert.That(Get<Collider>(scene.Ability, "m_collider"), Is.SameAs(scene.Trigger));
                Assert.That(otherTrigger.enabled, Is.True);
            }
        }

        // 0x743ad1..0x743ae3: the catch is reversed when ReverseOnDirection (+0x14) is set and the direction
        // dot (the velocity's, since OrientateFromVelocity defaults to true) is below zero. The enter event
        // of the chosen point receives the attach-curve speed over SpeedNormalised (0x743b4d), negated for a
        // negative dot (cmpltss + sign mask, 0x743b70..0x743b7b), or 1 when SpeedNormalised is not positive
        // (0x743b53, constant 1.0 at 0x29a6be0).
        [Test]
        public void ReversedEntryRaisesTheReversePointsEventWithANegatedNormalisedSpeed()
        {
            using (var scene = new CatchScene())
            {
                List<float> forward = Record(scene.Data.Catch.PointForward.Events.m_onEnter);
                List<float> reverse = Record(scene.Data.Catch.PointReverse.Events.m_onEnter);
                scene.Data.Catch.ReverseOnDirection = true;
                scene.Data.Attach.SpeedNormalised = 2f;

                scene.Move(new Vector3(0f, 0f, 6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(Get<bool>(scene.Ability, "m_isReversed"), Is.False);
                Assert.That(forward, Is.EqualTo(new[] { 3f }), "linear curve: speed 6 over 2");
                Assert.That(reverse, Is.Empty);
                scene.Reset();

                scene.Move(new Vector3(0f, 0f, -6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(Get<bool>(scene.Ability, "m_isReversed"), Is.True);
                Assert.That(forward, Is.EqualTo(new[] { 3f }));
                Assert.That(reverse, Is.EqualTo(new[] { -3f }));
                scene.Reset();

                scene.Data.Attach.SpeedNormalised = 0f;
                scene.Move(new Vector3(0f, 0f, -6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(reverse, Is.EqualTo(new[] { -3f, -1f }), "without a normalised speed the event carries one");
                scene.Reset();

                scene.Data.Catch.ReverseOnDirection = false;
                scene.Move(new Vector3(0f, 0f, -6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(Get<bool>(scene.Ability, "m_isReversed"), Is.False, "reversal needs the flag");
                Assert.That(forward, Is.EqualTo(new[] { 3f, -1f }), "the sign still follows the dot");
            }
        }

        // The enter event takes the attach curve (Attach+0x38, 0x743b1b..0x743b30) and the release speed the
        // release curve (Release+0x30), so two different curves tell a swap apart: attach speed 2 x 6 = 12 over a
        // normalised 2 gives 6 where the release identity curve would give 3, and the release speed stays 6.
        [Test]
        public void TheAttachCurveFeedsTheEnterEventAndTheReleaseCurveTheReleaseSpeed()
        {
            using (var scene = new CatchScene())
            {
                List<float> forward = Record(scene.Data.Catch.PointForward.Events.m_onEnter);
                scene.Data.Attach.SpeedCurve = AnimationCurve.Linear(0f, 0f, 10f, 20f);
                scene.Data.Attach.SpeedNormalised = 2f;
                scene.Move(new Vector3(0f, 0f, 6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(forward, Is.EqualTo(new[] { 6f }));
                Assert.That(Get<float>(scene.Ability, "m_releaseSpeed"), Is.EqualTo(6f).Within(1e-4f));
            }
        }

        // 0x743a2c: OrientateFromVelocity (+0x19) clear measures the direction dot against the character's
        // forward instead of its velocity, which decides the reversal.
        [Test]
        public void ReversalFollowsTheCharactersForwardWhenNotOrientatedFromVelocity()
        {
            using (var scene = new CatchScene())
            {
                scene.Data.Catch.ReverseOnDirection = true;
                scene.Data.Attach.OrientateFromVelocity = false;
                scene.Character.SetWorldRotation(Quaternion.Euler(0f, 180f, 0f));
                scene.Move(new Vector3(0f, 0f, 6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(Get<bool>(scene.Ability, "m_isReversed"), Is.True, "the character faces against the trigger");
                scene.Reset();

                scene.Data.Attach.OrientateFromVelocity = true;
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(Get<bool>(scene.Ability, "m_isReversed"), Is.False, "the velocity runs with the trigger");
            }
        }

        // 0x743b04 / 0x743b0f..0x743b14: the release speed is the release speed curve at the entry speed, the
        // trigger collider is disabled and the ability marked triggered; 0x743c59..0x743c8a store the catch
        // RemainTime (+0x10) as the state timer only when it is not zero.
        [Test]
        public void EntryStoresTheReleaseSpeedAndTheRemainTimerOnlyForANonZeroRemainTime()
        {
            using (var scene = new CatchScene())
            {
                scene.Data.Release.SpeedCurve = AnimationCurve.Linear(0f, 0f, 10f, 40f);
                scene.Move(new Vector3(0f, 0f, 5f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(Get<float>(scene.Ability, "m_releaseSpeed"), Is.EqualTo(20f).Within(1e-4f));
                Assert.That(scene.Storage.GetCollection().ContainsKey(ActorFSMKeys.StateRemainTimer), Is.False, "zero remain time");
                scene.Reset();

                scene.Data.Catch.RemainTime = 2.5f;
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(scene.Storage.GetValueOnly<float>(ActorFSMKeys.StateRemainTimer), Is.EqualTo(2.5f));
            }
        }

        // 0x743be6..0x743c55: the catch position and normalised velocity are remembered; SnapToAttachPoint
        // (+0x43) moves the character to the catch position and stops it. SnapToAttachPosition 0x743D50 puts
        // the collider back at the remembered position.
        [Test]
        public void EntryRemembersTheCatchPointAndSnapsTheCharacterToItWhenAsked()
        {
            using (var scene = new CatchScene())
            {
                scene.Trigger.transform.position = Origin + new Vector3(0f, 2f, 0f);
                scene.Move(new Vector3(0f, 0f, 6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(Get<Vector3>(scene.Ability, "m_attachPosition"), Is.EqualTo(Origin + new Vector3(0f, 2f, 0f)));
                Assert.That(Get<Vector3>(scene.Ability, "m_attachDirection"), Is.EqualTo(Vector3.forward));
                Assert.That(scene.Character.WorldPosition, Is.EqualTo(Vector3.zero), "no snap by default");
                scene.Reset();

                scene.Data.Attach.SnapToAttachPoint = true;
                scene.Move(new Vector3(0f, 0f, 6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(scene.Character.WorldPosition, Is.EqualTo(Origin + new Vector3(0f, 2f, 0f)));
                Assert.That(scene.Character.WorldVelocity, Is.EqualTo(Vector3.zero));

                scene.Trigger.transform.position = Origin;
                scene.Ability.SnapToAttachPosition();
                Assert.That(scene.Trigger.transform.position, Is.EqualTo(Origin + new Vector3(0f, 2f, 0f)));
            }
        }

        // 0x743aa8..0x743ac0: a dynamic pivot resets the collider's local position to zero on entry.
        [Test]
        public void DynamicEntryResetsTheTriggersLocalPosition()
        {
            using (var scene = new CatchScene())
            {
                Transform parent = scene.Objects.Create("parent", Origin + new Vector3(10f, 0f, 0f), Quaternion.identity).transform;
                scene.Trigger.transform.SetParent(parent, true);
                scene.Trigger.transform.localPosition = new Vector3(0f, 3f, 0f);
                scene.Move(new Vector3(0f, 0f, 6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(scene.Trigger.transform.localPosition, Is.EqualTo(new Vector3(0f, 3f, 0f)));
                scene.Reset();

                scene.Data.Attach.IsDynamic = true;
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(scene.Trigger.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(Get<Vector3>(scene.Ability, "m_attachPosition"), Is.EqualTo(Origin + new Vector3(10f, 0f, 0f)));
            }
        }

        // OnUpdate 0x744250: velocity = (position - last position) / delta time, then the position is remembered.
        // OnAttach 0x743DB0 stops the character and remembers its position (+0xFC).
        [Test]
        public void UpdateDerivesTheVelocityFromTheMovementSinceTheLastUpdate()
        {
            using (var scene = new CatchScene())
            {
                Set(scene.Ability, "m_characterPosition", new Vector3(1f, 2f, 3f));
                scene.Character.SetWorldPosition(new Vector3(3f, 2f, 7f));
                scene.Ability.OnUpdate(0.5f);
                Assert.That(scene.Character.WorldVelocity.x, Is.EqualTo(4f).Within(1e-5f));
                Assert.That(scene.Character.WorldVelocity.y, Is.EqualTo(0f).Within(1e-5f));
                Assert.That(scene.Character.WorldVelocity.z, Is.EqualTo(8f).Within(1e-5f));
                Assert.That(Get<Vector3>(scene.Ability, "m_characterPosition"), Is.EqualTo(new Vector3(3f, 2f, 7f)));

                scene.Ability.OnUpdate(0.5f);
                Assert.That(scene.Character.WorldVelocity.sqrMagnitude, Is.EqualTo(0f).Within(1e-9f), "no movement, no velocity");
            }
        }

        private static TransformFakeParent AddFakeParent(CatchScene scene)
        {
            var fake = scene.Character.gameObject.AddComponent<TransformFakeParent>();
            Set(scene.Character, "m_fakeParent", fake);
            var capsule = scene.Character.gameObject.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            Set(scene.Character, "m_capsuleColliderCollision", capsule);
            return fake;
        }

        // SetupParentConstraints 0x743E30 (through OnAttach): the attach position in the catch point's frame
        // (the inverse of its angle offset), lowered by the capsule height 2 when OffsetToTopOfCharacter
        // (+0x44, 0x743fa8), minus the point's position offset, becomes the fake parent's translation about the
        // trigger transform. The character is stopped (0x743e06).
        [Test]
        public void AttachParentsTheCharacterToTheTriggerAtTheOffsetBelowTheCatchPoint()
        {
            using (var scene = new CatchScene())
            {
                TransformFakeParent fake = AddFakeParent(scene);
                scene.Trigger.transform.position = Origin + new Vector3(0f, 3f, 0f);
                scene.Data.Catch.PointForward.PositionOffset = new Vector3(0.5f, 0f, 0f);
                scene.Move(new Vector3(0f, 0f, 6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                scene.Ability.OnAttach();

                // local attach position (0,0,0) in the trigger's frame; minus the capsule height 2; minus (0.5,0,0).
                Assert.That(fake.Evaluate(out Vector3 position, out Quaternion rotation), Is.True);
                Assert.That(position.x, Is.EqualTo(Origin.x - 0.5f).Within(1e-3f));
                Assert.That(position.y, Is.EqualTo(Origin.y + 3f - 2f).Within(1e-3f));
                Assert.That(position.z, Is.EqualTo(Origin.z).Within(1e-3f));
                Assert.That(Quaternion.Angle(rotation, Quaternion.identity), Is.LessThan(0.01f));
                Assert.That(Get<Vector3>(scene.Ability, "m_attachOffset").y, Is.EqualTo(0f).Within(1e-4f), "the stored offset is not lowered");
                Assert.That(scene.Character.WorldVelocity.sqrMagnitude, Is.EqualTo(0f).Within(1e-9f));
                Assert.That(Get<Vector3>(scene.Ability, "m_characterPosition"), Is.EqualTo(scene.Character.WorldPosition));
            }
        }

        // 0x743f95..0x743fbc: OffsetToTopOfCharacter lowers the parenting offset by the capsule height 2. A
        // dynamic pivot (+0x18, 0x743fdb) then moves the trigger to the character, extends the stored offset upward by
        // the distance from the catch position to the character (less the capsule height only when OffsetToTop,
        // 0x744062..0x744072) and raises the update-constraints event with that distance (0x744123). The parenting
        // offset is the one computed before the extension. Run once with and once without OffsetToTop.
        [TestCase(false)]
        [TestCase(true)]
        public void DynamicAttachMovesTheTriggerToTheCharacterAndRaisesTheConstraintEvent(bool offsetToTop)
        {
            using (var scene = new CatchScene())
            {
                TransformFakeParent fake = AddFakeParent(scene);
                List<float> updates = Record(scene.Data.Catch.PointForward.Events.m_onUpdateConstraints);
                scene.Data.Attach.IsDynamic = true;
                scene.Data.Attach.OffsetToTopOfCharacter = offsetToTop;
                // The entry puts a dynamic trigger back to its parent, so the catch point is the parent's.
                Transform parent = scene.Objects.Create("pivot", Origin + new Vector3(0f, 3f, 0f), Quaternion.identity).transform;
                scene.Trigger.transform.SetParent(parent, false);
                scene.Trigger.transform.localPosition = Vector3.zero;
                scene.Character.SetWorldPosition(Origin);
                scene.Move(new Vector3(0f, 0f, 6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(Get<Vector3>(scene.Ability, "m_attachPosition").y, Is.EqualTo(Origin.y + 3f).Within(1e-3f));
                scene.Ability.OnAttach();

                float distance = offsetToTop ? 3f - 2f : 3f;
                float lowered = offsetToTop ? -2f : 0f;
                Assert.That(updates, Has.Count.EqualTo(1));
                Assert.That(updates[0], Is.EqualTo(distance).Within(1e-3f), "distance from the catch position to the character");
                Assert.That(scene.Trigger.transform.position.y, Is.EqualTo(Origin.y).Within(1e-3f), "the pivot follows the character");
                Assert.That(Get<Vector3>(scene.Ability, "m_attachOffset").y, Is.EqualTo(distance).Within(1e-3f), "stored offset plus up times the distance");
                Assert.That(fake.Evaluate(out Vector3 position, out Quaternion _), Is.True);
                Assert.That(position.y, Is.EqualTo(Origin.y + lowered).Within(1e-3f), "the parenting offset is the one before the extension");
            }
        }

        private static void WithoutCoroutineHost(Action body)
        {
            FieldInfo host = typeof(Hardlight.Utils.CoroutineUtils).GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            object prior = host.GetValue(null);
            host.SetValue(null, null);
            try
            {
                body();
            }
            finally
            {
                host.SetValue(null, prior);
            }
        }

        private static void ExpectNextFrameFault(Action exit)
        {
            WithoutCoroutineHost(() =>
            {
                NullReferenceException fault = Assert.Throws<NullReferenceException>(() => exit());
                Assert.That(fault.StackTrace, Does.Contain("CoroutineUtils.OnNextFrame"), "the fault comes from scheduling the exit event");
            });
        }

        // DoTriggerExit 0x744630 returns at its first test (0x7446c4) while the ability is not triggered: no
        // release, no leave, nothing scheduled, so no host is needed.
        [Test]
        public void ExitDoesNothingWhileNotTriggered()
        {
            using (var scene = new CatchScene())
            {
                scene.Ability.Enter();
                WithoutCoroutineHost(() => Assert.DoesNotThrow(() => scene.Ability.DoTriggerExit()));
                Assert.That(scene.Ability.Enabled, Is.True, "not left");
                Assert.That(scene.Storage.GetCollection().ContainsKey(ActorFSMKeys.AirDecelerationDisabled), Is.False);
            }
        }

        // AreModifiersBlocked (0x744745) true skips the release transform entirely (jne 0x74488c): an
        // uninitialised character keeps its velocity, yet the ability still leaves and the exit is scheduled.
        [Test]
        public void ExitWithBlockedModifiersLeavesTheAbilityWithoutReleasingTheCharacter()
        {
            using (var scene = new CatchScene())
            {
                AddFakeParent(scene);
                scene.Move(new Vector3(0f, 0f, 6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(scene.Character.Initialised, Is.False);

                ExpectNextFrameFault(() => scene.Ability.DoTriggerExit());

                Assert.That(scene.Ability.IsTriggered, Is.False);
                Assert.That(scene.Character.WorldVelocity, Is.EqualTo(new Vector3(0f, 0f, 6f)));
                Assert.That(scene.Storage.GetCollection().ContainsKey(ActorFSMKeys.AirDecelerationDisabled), Is.False);
            }
        }

        // OnRelease 0x744A40 clears a connector's pivots (when one is set) before it exits like DoTriggerExit.
        [Test]
        public void ReleaseClearsTheConnectorBeforeExiting()
        {
            using (var scene = new CatchScene())
            {
                AddFakeParent(scene);
                // The connector's field initialiser takes the TimeManager reference; its row must be absent.
                scene.Lease(new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<TimeManager>()));
                var connector = scene.Objects.Create("connector").AddComponent<CatchConnector>();
                scene.Data.Catch.Connector = connector;
                connector.gameObject.SetActive(false);
                Set(connector, "m_alwaysActive", false);
                Set(connector, "m_catchTransform", scene.Trigger.transform);
                scene.Move(new Vector3(0f, 0f, 6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);

                ExpectNextFrameFault(() => scene.Ability.OnRelease());

                Assert.That(Get<Transform>(connector, "m_catchTransform"), Is.Null, "pivots cleared");
                Assert.That(scene.Ability.IsTriggered, Is.False);
            }
        }

        // GetFormType 0x744AE0 takes the data's form unless there is no data or the form is None (0xb6fa1284),
        // then the definition's form; GetAnimationDefinition 0x744B10 takes the data's animation unless data or
        // animation is missing, then the definition's speed-based one.
        [Test]
        public void FormAndAnimationComeFromTheDataWhenItSetsThem()
        {
            using (var scene = new CatchScene())
            {
                var animation = scene.Objects.Own(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                var fallback = scene.Objects.Own(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                scene.Definition.FormType = ActorFormType.Ball;
                scene.Definition.AnimationDefinition = fallback;
                Assert.That(scene.Ability.GetFormType(), Is.EqualTo(ActorFormType.Ball), "no data");
                Assert.That(scene.Ability.GetAnimationDefinition(), Is.SameAs(fallback), "no data");

                Set(scene.Ability, "m_data", scene.Data);
                scene.Data.FormType = ActorFormType.None;
                scene.Data.AnimationDefinition = null;
                Assert.That(scene.Ability.GetFormType(), Is.EqualTo(ActorFormType.Ball), "data form is None");
                Assert.That(scene.Ability.GetAnimationDefinition(), Is.SameAs(fallback), "no data animation");

                scene.Data.FormType = ActorFormType.CatchReleaseBall;
                scene.Data.AnimationDefinition = animation;
                Assert.That(scene.Ability.GetFormType(), Is.EqualTo(ActorFormType.CatchReleaseBall));
                Assert.That(scene.Ability.GetAnimationDefinition(), Is.SameAs(animation));
            }
        }

        // ---- CatchReleaseTrigger -----------------------------------------------------------------

        private static CatchReleaseTrigger NewTrigger(CatchScene scene)
        {
            // The field initialiser takes the LevelManager reference; its row must be absent.
            scene.Lease(new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<LevelManager>()));
            GameObject host = scene.Objects.Create("catch release trigger", Origin, Quaternion.identity);
            var box = host.AddComponent<BoxCollider>();
            var trigger = host.AddComponent<CatchReleaseTrigger>();
            Set(trigger, "m_collider", box);
            return trigger;
        }

        // The constructor 0x6E5A40 builds the data, the level-reset event, the fixed-update wait and the level
        // manager reference before the base constructor, each per instance.
        [Test]
        public void TriggerBuildsItsOwnDataAndResetEvent()
        {
            using (var scene = new CatchScene())
            {
                CatchReleaseTrigger first = NewTrigger(scene);
                var second = scene.Objects.Create("second trigger").AddComponent<BoxCollider>().gameObject.AddComponent<CatchReleaseTrigger>();
                Assert.That(Get<CatchReleaseDefinition>(first, "m_data"), Is.Not.Null.And.Not.SameAs(Get<CatchReleaseDefinition>(second, "m_data")));
                Assert.That(Get<UnityEvent>(first, "m_onLevelReset"), Is.Not.Null.And.Not.SameAs(Get<UnityEvent>(second, "m_onLevelReset")));
                Assert.That(Get<WaitForFixedUpdate>(first, "m_waitForFixedUpdate"), Is.Not.Null);
                Assert.That(Get<SystemRef<LevelManager>>(first, "m_levelManagerRef"), Is.Not.Null);
                Assert.That(Get<CharacterAbility_CatchRelease>(first, "m_ability"), Is.Null);
            }
        }

        // OnLevelActivated 0x6E5680 resets the ability if one is held, drops it, and always raises the event;
        // OnReset 0x6E56D0 does the first two without the event.
        [Test]
        public void LevelActivationRaisesTheResetEventWhileAResetDoesNot()
        {
            using (var scene = new CatchScene())
            {
                CatchReleaseTrigger trigger = NewTrigger(scene);
                int raised = 0;
                Get<UnityEvent>(trigger, "m_onLevelReset").AddListener(() => raised++);

                Call(trigger, "OnLevelActivated", (object)null);
                Assert.That(raised, Is.EqualTo(1));
                trigger.OnReset();
                Assert.That(raised, Is.EqualTo(1), "a plain reset raises nothing");
                Assert.That(Get<CharacterAbility_CatchRelease>(trigger, "m_ability"), Is.Null);
            }
        }

        // OnEndReached 0x6E59E0, DoTriggerExit 0x6E59A0 and SnapToAttachPosition 0x6E5A20 test the ability for
        // null before using it; with an untriggered ability the exit is a no-op and the snap moves the collider.
        [Test]
        public void ExitEndAndSnapIgnoreAMissingAbilityAndForwardToAnExistingOne()
        {
            using (var scene = new CatchScene())
            {
                CatchReleaseTrigger trigger = NewTrigger(scene);
                Assert.DoesNotThrow(() => trigger.OnEndReached());
                Assert.DoesNotThrow(() => Call(trigger, "DoTriggerExit", (object)scene.Character));
                Assert.DoesNotThrow(() => trigger.SnapToAttachPosition());

                Set(trigger, "m_ability", scene.Ability);
                scene.Move(new Vector3(0f, 0f, 6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Set(scene.Ability, "m_attachPosition", Origin + new Vector3(0f, 7f, 0f));
                trigger.SnapToAttachPosition();
                Assert.That(scene.Trigger.transform.position, Is.EqualTo(Origin + new Vector3(0f, 7f, 0f)), "forwarded to the ability");
            }
        }

        // DoTriggerEnter 0x6E5750: a character that is not active (here one whose body was kinematic) is refused before the
        // ability is looked up; an active one has its ability fetched into m_ability and, when no input is
        // required (Attach +0x10 clear), entered at once with the trigger's data and collider.
        [Test]
        public void EntryTriggersTheCharactersAbilityDirectlyWhenNoInputIsRequired()
        {
            using (var scene = new CatchScene())
            {
                CatchReleaseTrigger trigger = NewTrigger(scene);
                scene.AddToCharacter(ActorAbilityType.Character_CatchRelease, scene.Ability);

                Set(scene.Character, "m_bodyWasKinematic", true);
                Call(trigger, "DoTriggerEnter", (object)scene.Character);
                Assert.That(Get<CharacterAbility_CatchRelease>(trigger, "m_ability"), Is.Null, "an inactive character is refused");
                Assert.That(scene.Ability.IsTriggered, Is.False);

                Set(scene.Character, "m_bodyWasKinematic", false);
                Call(trigger, "DoTriggerEnter", (object)scene.Character);
                Assert.That(Get<CharacterAbility_CatchRelease>(trigger, "m_ability"), Is.SameAs(scene.Ability));
                Assert.That(scene.Ability.IsTriggered, Is.True);
                Assert.That(Get<CatchReleaseDefinition>(scene.Ability, "m_data"), Is.SameAs(Get<CatchReleaseDefinition>(trigger, "m_data")));
                Assert.That(Get<Collider>(scene.Ability, "m_collider"), Is.SameAs(Get<Collider>(trigger, "m_collider")));
            }
        }

        // An ability that is already triggered is kept in m_ability but not entered again (0x6E57CB cmp byte [+0xB8]).
        [Test]
        public void EntryKeepsAnAlreadyTriggeredAbilityWithoutEnteringItAgain()
        {
            using (var scene = new CatchScene())
            {
                CatchReleaseTrigger trigger = NewTrigger(scene);
                scene.AddToCharacter(ActorAbilityType.Character_CatchRelease, scene.Ability);
                scene.Move(new Vector3(0f, 0f, 6f));
                scene.Ability.DoTriggerEnter(scene.Data, scene.Trigger);
                Assert.That(scene.Ability.IsTriggered, Is.True);

                Call(trigger, "DoTriggerEnter", (object)scene.Character);
                Assert.That(Get<CharacterAbility_CatchRelease>(trigger, "m_ability"), Is.SameAs(scene.Ability));
                Assert.That(Get<CatchReleaseDefinition>(scene.Ability, "m_data"), Is.SameAs(scene.Data), "not re-entered with the trigger's data");
            }
        }

        // ProcessStayEvents (iterator 0x6E5B80) yields exactly WaitNumberOfFramesBeforeTrigger = 2 null frames,
        // then reads the brain's attach input (0x6E5C7A GetBrainAppliedState). A vanished character ends the wait
        // after the two frames without releasing.
        [Test]
        public void StayWaitYieldsTwoFramesBeforeItReadsTheAttachInput()
        {
            using (var scene = new CatchScene())
            {
                CatchReleaseTrigger trigger = NewTrigger(scene);
                var ended = (IEnumerator)Call(trigger, "ProcessStayEvents", (object)null);
                Assert.That(ended.MoveNext(), Is.True);
                Assert.That(ended.Current, Is.Null);
                Assert.That(ended.MoveNext(), Is.True);
                Assert.That(ended.Current, Is.Null);
                Assert.That(ended.MoveNext(), Is.False, "no character: the wait ends");

                var waiting = (IEnumerator)Call(trigger, "ProcessStayEvents", (object)scene.Character);
                Assert.That(waiting.MoveNext(), Is.True);
                Assert.That(waiting.MoveNext(), Is.True);
                NullReferenceException fault = Assert.Throws<NullReferenceException>(() => waiting.MoveNext());
                Assert.That(fault.StackTrace, Does.Contain("HardlightProject.Character.GetBrainAppliedState"), "the input is first read on the third step");
            }
        }

        private sealed class HeldBrain : CharacterBrain
        {
            public override void Close()
            {
            }
        }

        // A brain whose applied state for the attach action (CharacterHomingAttack by default) can be toggled.
        private static HeldBrain GiveBrain(Character character, bool held)
        {
            CharacterActionFlags.Initialise();
            var brain = new HeldBrain();
            Set(character, "m_brain", brain);
            Hold(brain, held);
            return brain;
        }

        private static void Hold(CharacterBrain brain, bool held)
        {
            var flags = new CharacterActionFlags();
            flags.SetValue(GameAction.CharacterHomingAttack, held);
            brain.ApplyActions(flags, 1f);
        }

        private static IEnumerator StayOf(CatchReleaseTrigger trigger, Character character, CatchScene scene)
        {
            Set(trigger, "m_ability", scene.Ability);
            var stay = (IEnumerator)Call(trigger, "ProcessStayEvents", (object)character);
            Assert.That(stay.MoveNext(), Is.True);
            Assert.That(stay.MoveNext(), Is.True);
            return stay;
        }

        // MoveNext 0x6E5C87..0x6E5E1E: once the input is applied the ability is entered and the trigger's own
        // fixed-update wait is yielded; a non-continuous input (Attach+0x11 clear) then stops without a release.
        [Test]
        public void StayAttachesOnTheInputAndStopsWithoutReleasingForANonContinuousInput()
        {
            using (var scene = new CatchScene())
            {
                CatchReleaseTrigger trigger = NewTrigger(scene);
                Get<CatchReleaseDefinition>(trigger, "m_data").Attach.InputRequiredIsContinuous = false;
                GiveBrain(scene.Character, true);
                IEnumerator stay = StayOf(trigger, scene.Character, scene);
                Assert.That(scene.Ability.IsTriggered, Is.False, "nothing happens in the two waiting frames");

                Assert.That(stay.MoveNext(), Is.True);
                Assert.That(stay.Current, Is.SameAs(Get<WaitForFixedUpdate>(trigger, "m_waitForFixedUpdate")));
                Assert.That(scene.Ability.IsTriggered, Is.True);
                Assert.That(stay.MoveNext(), Is.False);
                Assert.That(scene.Ability.IsTriggered, Is.True, "no release for an instant input");
            }
        }

        // A continuous input keeps waiting while it is held and releases the ability (DoTriggerExit) when it ends.
        [Test]
        public void StayReleasesTheAbilityWhenAContinuousHoldEnds()
        {
            using (var scene = new CatchScene())
            {
                AddFakeParent(scene);
                CatchReleaseTrigger trigger = NewTrigger(scene);
                HeldBrain brain = GiveBrain(scene.Character, true);
                IEnumerator stay = StayOf(trigger, scene.Character, scene);
                Assert.That(stay.MoveNext(), Is.True, "attached");
                Assert.That(stay.MoveNext(), Is.True, "still held");
                Assert.That(scene.Ability.IsTriggered, Is.True);

                Hold(brain, false);
                ExpectNextFrameFault(() => stay.MoveNext());

                Assert.That(scene.Ability.IsTriggered, Is.False);
            }
        }

        // A character that disappears during a continuous hold ends the wait and the ability is released.
        [Test]
        public void StayReleasesTheAbilityWhenTheCharacterVanishesDuringAContinuousHold()
        {
            using (var scene = new CatchScene())
            {
                AddFakeParent(scene);
                CatchReleaseTrigger trigger = NewTrigger(scene);
                var watched = scene.Objects.Create("watched character", Origin, Quaternion.identity).AddComponent<CharacterGravityVectorProbe>();
                GiveBrain(watched, true);
                IEnumerator stay = StayOf(trigger, watched, scene);
                Assert.That(stay.MoveNext(), Is.True, "attached");
                Assert.That(stay.MoveNext(), Is.True, "still held");
                Assert.That(scene.Ability.IsTriggered, Is.True);

                Object.DestroyImmediate(watched.gameObject);
                ExpectNextFrameFault(() => stay.MoveNext());

                Assert.That(scene.Ability.IsTriggered, Is.False);
            }
        }

        // DoTriggerStay 0x6E5920..0x6E5934 does not re-enter while the input wait runs (m_stayCoroutine set).
        [Test]
        public void StayInTheVolumeDoesNotReenterWhileTheInputWaitIsRunning()
        {
            using (var scene = new CatchScene())
            {
                CatchReleaseTrigger trigger = NewTrigger(scene);
                scene.AddToCharacter(ActorAbilityType.Character_CatchRelease, scene.Ability);
                Coroutine running = trigger.StartCoroutine(PendingFrame());
                Assert.That(running, Is.Not.Null);
                Set(trigger, "m_stayCoroutine", running);

                Call(trigger, "DoTriggerStay", (object)scene.Character);
                Assert.That(scene.Ability.IsTriggered, Is.False);
                Assert.That(Get<CharacterAbility_CatchRelease>(trigger, "m_ability"), Is.Null);

                Set(trigger, "m_stayCoroutine", null);
                Call(trigger, "DoTriggerStay", (object)scene.Character);
                Assert.That(scene.Ability.IsTriggered, Is.True);
            }
        }

        private static IEnumerator PendingFrame()
        {
            yield return null;
        }

        // ---- CharacterState_CatchRelease ---------------------------------------------------------

        private static List<string> Calls(Type type, string method) =>
            OriginalILCallSequence.Names(OriginalILCallSequence.Calls(type.GetMethod(method, Members | BindingFlags.DeclaredOnly))).ToList();

        // The registered node: graph name and menu, and the shared ability argument type.
        [Test]
        public void StateIsRegisteredAsAGraphNodeWithTheAbilityArguments()
        {
            Assert.That(FSMClassFactory.States.TryGetValue(nameof(CharacterState_CatchRelease), out JSONFactoryClass factory), Is.True);
            Assert.That(factory.ClassType, Is.EqualTo(typeof(CharacterState_CatchRelease)));
            Assert.That(factory.ConstructInstanceMethod.DeclaringType, Is.EqualTo(typeof(CharacterState_CatchRelease)));
            Assert.That(factory.JSONCtorArgsType.Name, Is.EqualTo("AbilityJSONCtorArgs"));
            Assert.That(factory.JSONCtorArgsType.DeclaringType, Is.EqualTo(typeof(ActorState_Ability)));
            Assert.That(typeof(CharacterState_CatchRelease).GetCustomAttribute<GraphNodeDefaultNameAttribute>(false).DefaultName, Is.EqualTo("CatchRelease"));
            Assert.That(typeof(CharacterState_CatchRelease).GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format, Is.EqualTo("Character/{0}"));
            IFSMState state = FSMClassFactory.ConstructState(Machine(), nameof(CharacterState_CatchRelease), new FSMIdentifier(Name()), "{}");
            Assert.That(state, Is.TypeOf<CharacterState_CatchRelease>());
        }

        private static void AssertOrder(List<string> calls, params string[] expected)
        {
            int[] indexes = expected.Select(call => calls.IndexOf(call)).ToArray();
            Assert.That(indexes, Has.All.GreaterThanOrEqualTo(0), string.Join(", ", calls));
            Assert.That(indexes, Is.Ordered.Ascending.And.Unique, string.Join(", ", calls));
        }

        // Declaration-shape witness on the compiled bodies, not a behaviour test (a full ability stack is not
        // buildable here). Call order of 0x775E50, 0x775F10 and 0x775FE0: entry runs the ability base entry, then
        // attaches, then turns the visual proxy's smoothing off, the burst-attack flag off and refreshes the
        // connection tracking; the update runs the base, advances the ability and counts the state timer down;
        // leaving runs the base, turns smoothing back on and only then releases the ability.
        [Test]
        public void StateBodiesCallInTheNativeOrder()
        {
            AssertOrder(Calls(typeof(CharacterState_CatchRelease), "DoOnEnter"), "CharacterState_Ability`1.DoOnEnter", "CharacterAbility_CatchRelease.OnAttach",
                "SmoothedTransformRotator.ToggleSmoothing", "Character.TriggerBurstAttackAir", "Character.UpdateConnectionTrackingValues");
            AssertOrder(Calls(typeof(CharacterState_CatchRelease), "DoUpdate"), "CharacterState_Ability`1.DoUpdate", "CharacterAbility_CatchRelease.OnUpdate",
                "CharacterState_Ability`1.UpdateRemainTimer");
            AssertOrder(Calls(typeof(CharacterState_CatchRelease), "DoOnLeave"), "CharacterState_Ability`1.DoOnLeave", "SmoothedTransformRotator.ToggleSmoothing",
                "CharacterAbility_CatchRelease.OnRelease");
        }
    }
}
