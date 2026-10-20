using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The ground boost ability, the movement climbing state and the ground and minimum jump time transitions.
    // Expected values come from the 1.10.1 x86-64 bodies cited per test; each transition is built from its JSON
    // through the node factory and updated against a real Character on an owned inactive host.
    public sealed class CharacterGroundBoostTransitionTests
    {
        private static string Name() => "Lucid-ground-" + Guid.NewGuid().ToString("N");

        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);

        private static IFSMTransition BuildTransition(string node, string json) =>
            FSMClassFactory.ConstructTransition(Machine(), node, new FSMIdentifier(Name()), json);

        private static bool Update(IFSMTransition transition, Character character) =>
            transition.Update(character, new FSMUpdateContext(0.02f, FSMUpdateType.FixedUpdate));

        // A tracking that reports a scripted ground state for a chosen tracking type and refuses everything else.
        private sealed class GroundTracking : CharacterTracking
        {
            private readonly CharacterTrackingType m_type;
            public bool OnGround;

            public GroundTracking(Character character, CharacterTrackingType type) : base(character) => m_type = type;

            public override CharacterTrackingType Type => m_type;
            public override bool IsOnGround() => OnGround;
            public override bool WasOnGround() => throw new NotSupportedException();
            public override Vector3 GetContactNormal() => throw new NotSupportedException();
            public override bool GetProjectedLandingPosition(float maximumDistance, out Quaternion hitRotation, out float hitTime) => throw new NotSupportedException();
            public override bool RaycastOnTrack(Ray ray, float distance, out RaycastHit raycastHit) => throw new NotSupportedException();
            public override bool RaycastOnCollision(Ray ray, float distance, out RaycastHit raycastHit) => throw new NotSupportedException();
            public override void ProcessMovement(CharacterAbilityDefinition_MovementGround abilityDef, float deltaTime) => throw new NotSupportedException();
            public override void OrientateToGround() => throw new NotSupportedException();
            public override void GetSteeringInput(CharacterAbilityDefinition_MovementFree abilityDef, float deltaTime, out Vector3 inputForward, out float inputTurn) =>
                throw new NotSupportedException();
        }

        // ---- CharacterAbility_MovementGroundBoost (020002d9) ----

        // 0x749100: the boost can trigger while the one-shot boost's storage flag is not set to true (the flag read is
        // the Actor boolean probe, so a stored false counts as absent), then the result is negated (xor al, 1).
        [Test]
        public void GroundBoostCanTriggerUnlessTheOneShotFlagHolds()
        {
            WithAbility<CharacterAbility_MovementGroundBoost, CharacterAbilityDefinition_MovementGroundBoost>((owned, character, ability, definition) =>
            {
                Assert.That((bool)Call(ability, "CanTrigger"), Is.True, "no flag");
                character.Storage.SetValue(ActorFSMKeys.OneShotCanTriggerBoost, false);
                Assert.That((bool)Call(ability, "CanTrigger"), Is.True, "a stored false");
                character.Storage.SetValue(ActorFSMKeys.OneShotCanTriggerBoost, true);
                Assert.That((bool)Call(ability, "CanTrigger"), Is.False, "the one-shot boost owns the trigger");
            });
        }

        // 0x749050: the ability base initialises first, then the boost stamina is updated from the ability's type and
        // boost definition, passing false for the impulse override (the fourth argument register ecx is
        // zeroed at 0x7490DF). Initialise needs a started Character, so the
        // order is read from the compiled IL.
        [Test]
        public void GroundBoostInitialiseUpdatesTheBoostStaminaAfterTheBase()
        {
            MethodBase initialise = typeof(CharacterAbility_MovementGroundBoost).GetMethods(DeclaredMembers).Single(m => m.Name == "Initialise");
            List<OriginalILCallSequence.Call> calls = OriginalILCallSequence.Calls(initialise);
            string[] names = OriginalILCallSequence.Names(calls);
            Assert.That(names.Take(4), Is.EqualTo(new[]
            {
                "CharacterAbility`1.Initialise", "Character.get_BoostStamina", "CharacterAbility`1.get_Definition", "CharacterAbility`1.get_Definition",
            }));
            Assert.That(names.Length, Is.EqualTo(5));
            Assert.That(names[4], Does.EndWith(".UpdateFromAbility"));
            List<(int Offset, OpCode Code)> il = OriginalILCallSequence.Instructions(initialise);
            int update = il.FindIndex(i => i.Offset == calls[4].Offset);
            Assert.That(il[update - 1].Code, Is.EqualTo(OpCodes.Ldc_I4_0), "the impulse override is false");
        }

        // ---- CharacterTransition_Ground (020003d9) ----

        private static string GroundJson(CharacterTrackingType type) => "{\"TrackingType\":" + (int)type + "}";

        private static void WithGroundTracking(CharacterTrackingType active, bool onGround, Action<Owned, Character, GroundTracking, CharacterCollider> body) =>
            Owned.Run(owned =>
            {
                owned.LeaseRow<GameplayIslandManager>();
                CharacterGravityVectorProbe character = NewCharacter(owned);
                var tracking = new GroundTracking(character, active) { OnGround = onGround };
                SetProperty(character, "ActiveTracking", tracking);
                body(owned, character, tracking, character.Collider);
            });

        // 0x790750: the base transition first; then the character must be on the ground for the configured tracking type
        // (Character.IsOnGround, 0x71F8E0: the Active wildcard, or the active tracking's own type, and it being on the
        // ground), and not resting on a climbable surface: the linked collision data at +0x20 is read only while the
        // collider has linked data (+0x30) and is climbable at +0x5B.
        [Test]
        public void GroundTransitionNeedsGroundTrackingAndNoClimbableContact()
        {
            WithGroundTracking(CharacterTrackingType.SurfaceTracker, true, (owned, character, tracking, collider) =>
            {
                IFSMTransition active = BuildTransition(nameof(CharacterTransition_Ground), GroundJson(CharacterTrackingType.Active));
                IFSMTransition surface = BuildTransition(nameof(CharacterTransition_Ground), GroundJson(CharacterTrackingType.SurfaceTracker));
                IFSMTransition other = BuildTransition(nameof(CharacterTransition_Ground), GroundJson(CharacterTrackingType.ColliderTracker));

                Assert.That(Update(active, character), Is.True, "the wildcard accepts the active tracking");
                Assert.That(Update(surface, character), Is.True, "the active tracking has this type");
                Assert.That(Update(other, character), Is.False, "another tracking type");
                tracking.OnGround = false;
                Assert.That(Update(active, character), Is.False, "not on the ground");
                Assert.That(Update(surface, character), Is.False);
            });
        }

        [Test]
        public void GroundTransitionRefusesOnlyAClimbableLinkedContact()
        {
            WithGroundTracking(CharacterTrackingType.SurfaceTracker, true, (owned, character, tracking, collider) =>
            {
                IFSMTransition transition = BuildTransition(nameof(CharacterTransition_Ground), GroundJson(CharacterTrackingType.Active));
                // ColliderData requires a Collider (RequireComponent), which Unity cannot add for the abstract type.
                GameObject dataHost = owned.Host("Lucid collision data", false);
                dataHost.AddComponent<BoxCollider>();
                var data = dataHost.AddComponent<CharacterCollisionData>();
                SetProperty(collider, "LinkedCollisionData", data);

                foreach ((bool linked, bool climbable, bool expected) in new[]
                {
                    (false, false, true), (false, true, true), (true, false, true), (true, true, false),
                })
                {
                    SetProperty(collider, "HasLinkedCollisionData", linked);
                    SetProperty(data, "IsClimbable", climbable);
                    Assert.That(Update(transition, character), Is.EqualTo(expected), "linked " + linked + ", climbable " + climbable);
                }
            });
        }

        // The JSON argument class has no behaviour of its own; the constructor stores its tracking type and the
        // factory finds it as the attributed nested class.
        [Test]
        public void GroundTransitionKeepsTheTrackingTypeOfItsJson()
        {
            IFSMTransition transition = BuildTransition(nameof(CharacterTransition_Ground), GroundJson(CharacterTrackingType.ColliderFree));
            Assert.That(transition.GetType(), Is.EqualTo(typeof(CharacterTransition_Ground)));
            Assert.That(Get<CharacterTrackingType>(transition, "TrackingType"), Is.EqualTo(CharacterTrackingType.ColliderFree));
            Assert.That(Get<CharacterTrackingType>(BuildTransition(nameof(CharacterTransition_Ground), "{}"), "TrackingType"), Is.EqualTo((CharacterTrackingType)0),
                "an omitted field is zero");
        }

        // ---- CharacterTransition_MinimumJumpTime (020003f8) ----

        private static string JumpJson => "{\"AbilityType\":" + (int)ActorAbilityType.Character_Jump + "}";

        private static CharacterAbility_MovementJump RegisterJump(Owned owned, Character character, float minimumJumpTime, ActorAbilityType type = ActorAbilityType.Character_Jump)
        {
            var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_Jump>());
            definition.MinimumJumpTimeSeconds = minimumJumpTime;
            var jump = new CharacterAbility_MovementJump();
            Set(jump, "m_definition", definition);
            Get<Dictionary<ActorAbilityType, ActorAbility>>(character, "m_abilities")[type] = jump;
            return jump;
        }

        private static void WithJumpCharacter(Action<Owned, Character> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<App>();
            body(owned, NewCharacter(owned));
        });

        // 0x7931C0: no jump ability of the configured type is false; otherwise the stored jump elapsed time (default zero,
        // stored on first read) must have reached the definition's minimum jump time: ucomiss / setae, so equality
        // passes and an unordered NaN fails.
        [Test]
        public void MinimumJumpTimeFiresOnceTheJumpHasLastedItsMinimum()
        {
            WithJumpCharacter((owned, character) =>
            {
                IFSMTransition transition = BuildTransition(nameof(CharacterTransition_MinimumJumpTime), JumpJson);
                Assert.That(Update(transition, character), Is.False, "no jump ability");

                RegisterJump(owned, character, 0.2f);
                foreach ((float elapsed, bool expected) in new[] { (0f, false), (0.19f, false), (0.2f, true), (0.25f, true), (float.NaN, false) })
                {
                    character.Storage.SetValue(ActorFSMKeys.JumpElapsedTime, elapsed);
                    Assert.That(Update(transition, character), Is.EqualTo(expected), "elapsed " + elapsed);
                }
            });
        }

        [Test]
        public void MinimumJumpTimeReadsAMissingElapsedTimeAsZeroAndStoresIt()
        {
            WithJumpCharacter((owned, character) =>
            {
                IFSMTransition transition = BuildTransition(nameof(CharacterTransition_MinimumJumpTime), JumpJson);
                CharacterAbility_MovementJump jump = RegisterJump(owned, character, 0.2f);
                Assert.That(Update(transition, character), Is.False);
                Assert.That(character.Storage.TryGetValue(ActorFSMKeys.JumpElapsedTime, out float stored) && stored == 0f, Is.True, "the default is stored");

                character.Storage.RemoveValue<float>(ActorFSMKeys.JumpElapsedTime);
                jump.Definition.MinimumJumpTimeSeconds = 0f;
                Assert.That(Update(transition, character), Is.True, "zero reaches a zero minimum");
            });
        }

        // The ability is found under the configured ability type only.
        [Test]
        public void MinimumJumpTimeLooksTheJumpUpByItsConfiguredType()
        {
            WithJumpCharacter((owned, character) =>
            {
                RegisterJump(owned, character, 0f, ActorAbilityType.Character_JumpOneShot);
                Assert.That(Update(BuildTransition(nameof(CharacterTransition_MinimumJumpTime), JumpJson), character), Is.False, "registered under another type");
                string oneShot = "{\"AbilityType\":" + (int)ActorAbilityType.Character_JumpOneShot + "}";
                Assert.That(Update(BuildTransition(nameof(CharacterTransition_MinimumJumpTime), oneShot), character), Is.True);
            });
        }

        // ---- CharacterState_MovementClimbing (02000386) ----

        // 0x77DA60: the factory builds the state from the ability JSON and the base keeps the ability type.
        [Test]
        public void MovementClimbingStateReadsItsAbilityTypeFromItsJson()
        {
            IFSMState state = FSMClassFactory.ConstructState(Machine(), nameof(CharacterState_MovementClimbing), new FSMIdentifier(Name()),
                "{\"AbilityType\":" + (int)ActorAbilityType.Character_MovementClimbing + "}");
            Assert.That(state.GetType(), Is.EqualTo(typeof(CharacterState_MovementClimbing)));
            Assert.That(typeof(ActorState_Ability).GetProperty("m_abilityType", Instance).GetValue(state), Is.EqualTo(ActorAbilityType.Character_MovementClimbing));
        }

        // ---- Registration and graph authoring metadata (declaration shape, not behaviour) ----

        private static void AssertNode(Type node, string defaultName, string menu)
        {
            Assert.That(node.GetCustomAttribute<GraphNodeDefaultNameAttribute>(false).DefaultName, Is.EqualTo(defaultName), node.Name);
            Assert.That(node.GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format, Is.EqualTo(menu), node.Name);
        }

        [Test]
        public void DeclarationShapeNodesCarryTheirOriginalGraphAuthoringAttributesInOrder()
        {
            AssertNode(typeof(CharacterTransition_Ground), "ToGround", "Character/{0}");
            AssertNode(typeof(CharacterTransition_MinimumJumpTime), "MinimumJumpTime", "Character/{0}");
            AssertNode(typeof(CharacterState_MovementClimbing), "MovementClimbing", "Character/{0}");
            string[] Order(Type type) => type.GetCustomAttributesData().Select(data => data.AttributeType.Name.Replace("Attribute", "")
                + (data.AttributeType.Name == "Il2CppSetOptionAttribute" ? Convert.ToInt32(data.ConstructorArguments[0].Value).ToString() : "")).ToArray();
            Assert.That(Order(typeof(CharacterTransition_Ground)), Is.EqualTo(new[] { "Il2CppSetOption2", "GraphNodeMenuFormat", "GraphNodeDefaultName", "Il2CppSetOption1" }));
            Assert.That(Order(typeof(CharacterTransition_MinimumJumpTime)), Is.EqualTo(new[] { "Il2CppSetOption1", "Il2CppSetOption2", "GraphNodeDefaultName", "GraphNodeMenuFormat" }));
            Assert.That(Order(typeof(CharacterState_MovementClimbing)), Is.EqualTo(new[] { "GraphNodeMenuFormat", "GraphNodeDefaultName", "Il2CppSetOption1", "Il2CppSetOption2" }));
        }

        private static void AssertRegistered(IReadOnlyDictionary<string, JSONFactoryClass> lookup, Type node, string argsName, Type argsOwner)
        {
            Assert.That(lookup.TryGetValue(node.Name, out JSONFactoryClass factory), Is.True, node.Name);
            Assert.That(factory.ClassType, Is.EqualTo(node), node.Name);
            Assert.That(factory.ConstructInstanceMethod.DeclaringType, Is.EqualTo(node), node.Name);
            Assert.That(factory.JSONCtorArgsType.Name, Is.EqualTo(argsName), node.Name);
            Assert.That(factory.JSONCtorArgsType.DeclaringType, Is.EqualTo(argsOwner), node.Name);
        }

        [Test]
        public void DeclarationShapeClassFactoryRegistersTheNodesWithTheirArgumentTypes()
        {
            AssertRegistered(FSMClassFactory.Transitions, typeof(CharacterTransition_Ground), "JSONCtorArgs", typeof(CharacterTransition_Ground));
            AssertRegistered(FSMClassFactory.Transitions, typeof(CharacterTransition_MinimumJumpTime), "AbilityJSONCtorArgs", typeof(Transition_Ability));
            AssertRegistered(FSMClassFactory.States, typeof(CharacterState_MovementClimbing), "AbilityJSONCtorArgs", typeof(ActorState_Ability));
        }

        [Test]
        public void DeclarationShapeTransitionsAndAbilityKeepTheirMembers()
        {
            const BindingFlags declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            Assert.That(typeof(CharacterTransition_Ground).BaseType, Is.EqualTo(typeof(FSMTransition)));
            FieldInfo tracking = typeof(CharacterTransition_Ground).GetFields(declared).Single();
            Assert.That((tracking.Name, tracking.IsPrivate, tracking.IsInitOnly), Is.EqualTo(("TrackingType", true, true)));
            ConstructorInfo constructor = typeof(CharacterTransition_Ground).GetConstructors(declared).Single();
            Assert.That(constructor.IsPrivate, Is.True, "only the factory builds it");
            Type args = typeof(CharacterTransition_Ground).GetNestedType("JSONCtorArgs", BindingFlags.NonPublic);
            Assert.That(args.IsNestedPrivate && args.IsSerializable, Is.True);
            Assert.That(args.GetCustomAttribute<FiniteStateMachineJsonCtorArgsAttribute>(false), Is.Not.Null);
            AssertIl2CppOptions(args, 1, 2);
            Assert.That(args.GetFields(declared).Select(f => f.Name).ToArray(), Is.EqualTo(new[] { "TrackingType" }));

            Assert.That(typeof(CharacterTransition_MinimumJumpTime).BaseType, Is.EqualTo(typeof(Transition_Ability)));
            Assert.That(typeof(CharacterTransition_MinimumJumpTime).GetFields(declared), Is.Empty);
            Assert.That(typeof(CharacterState_MovementClimbing).BaseType,
                Is.EqualTo(typeof(CharacterState_Climbing<CharacterAbility_MovementClimbing, CharacterAbilityDefinition_MovementClimbing>)));
            Assert.That(typeof(CharacterAbility_MovementGroundBoost).BaseType,
                Is.EqualTo(typeof(CharacterAbility_MovementGroundBoostBase<CharacterAbilityDefinition_MovementGroundBoost>)));
            AssertIl2CppOptions(typeof(CharacterAbility_MovementGroundBoost), 2, 1);
        }
    }
}
