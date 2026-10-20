using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
    // The six character transitions of the jump, ledge and lightspeed dash cluster: has ability type, the jump pair
    // with their shared base, ledge buffer end, and the lightspeed dash entry and exit. Expected values come from the
    // 1.10.1 x86-64 bodies cited per test; each transition is built from its JSON through the node factory and
    // updated against a real Character on an owned inactive host.
    public sealed class CharacterJumpLedgeTransitionTests
    {
        private static string Name() => "Lucid-transition-" + Guid.NewGuid().ToString("N");

        private static IFSMTransition Build(string node, string json) =>
            FSMClassFactory.ConstructTransition(new FiniteStateMachine(Name(), skipAddToManager: true), node, new FSMIdentifier(Name()), json);

        private static bool Update(IFSMTransition transition, Character character) =>
            transition.Update(character, new FSMUpdateContext(0.02f, FSMUpdateType.FixedUpdate));

        private static void WithCharacter(Action<Owned, CharacterGravityVectorProbe> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            body(owned, NewCharacter(owned));
        });

        private static Dictionary<ActorAbilityType, ActorAbility> Abilities(Character character) =>
            (Dictionary<ActorAbilityType, ActorAbility>)Get<object>(character, "m_abilities");

        private static string Bool(bool value) => value ? "true" : "false";

        private static string AbilityJson(ActorAbilityType type) => "{\"AbilityType\":" + (int)type + "}";

        private sealed class ProbeAbility : ActorAbility
        {
            private readonly AbilityDefinition m_definition;
            public ProbeAbility(AbilityDefinition definition) { m_definition = definition; }
            public override AbilityDefinition DefinitionBase => m_definition;
        }

        private sealed class ProbeDefinition : AbilityDefinition
        {
            public override Type ScriptType => typeof(ProbeAbility);
        }

        // A tracking with a scripted contact normal; every other query is refused.
        private sealed class ContactTrackingProbe : CharacterTracking
        {
            public Vector3 ContactNormal;

            public ContactTrackingProbe(Character character) : base(character) { }

            public override CharacterTrackingType Type => CharacterTrackingType.ColliderFree;
            public override Vector3 GetContactNormal() => ContactNormal;

            public override void GetSteeringInput(CharacterAbilityDefinition_MovementFree abilityDef, float deltaTime, out Vector3 inputForward, out float inputTurn) =>
                throw new NotSupportedException();
            public override bool IsOnGround() => throw new NotSupportedException();
            public override bool WasOnGround() => throw new NotSupportedException();
            public override bool GetProjectedLandingPosition(float maximumDistance, out Quaternion hitRotation, out float hitTime) =>
                throw new NotSupportedException();
            public override bool RaycastOnTrack(Ray ray, float distance, out RaycastHit raycastHit) => throw new NotSupportedException();
            public override bool RaycastOnCollision(Ray ray, float distance, out RaycastHit raycastHit) => throw new NotSupportedException();
            public override void ProcessMovement(CharacterAbilityDefinition_MovementGround abilityDef, float deltaTime) => throw new NotSupportedException();
            public override void OrientateToGround() => throw new NotSupportedException();
        }

        private enum Flag { None, CatchRelease, JumpOnRail, LedgeFromRail }

        // ---- CharacterTransition_HasAbilityType (020003df) ----

        // 0x791430 with the base result false: Actor.HasAbilityType of the configured type against the inverse flag
        // (0x791498 cmp / setne, then xor with the lookup result): present and not inverted, or absent and inverted.
        // A dictionary key that holds a null ability still counts as present (Actor.HasAbilityType is ContainsKey).
        [Test]
        public void HasAbilityTypeIsTrueWhenThePresenceDiffersFromTheInverseFlag()
        {
            WithCharacter((owned, character) =>
            {
                ActorAbilityType type = ActorAbilityType.Character_Jump;
                var probe = new ProbeAbility(owned.Track(ScriptableObject.CreateInstance<ProbeDefinition>()));
                foreach (bool inverse in new[] { false, true })
                {
                    IFSMTransition transition = Build(nameof(CharacterTransition_HasAbilityType), "{\"AbilityType\":" + (int)type + ",\"InverseCheck\":" + Bool(inverse) + "}");
                    Abilities(character).Remove(type);
                    Assert.That(Update(transition, character), Is.EqualTo(inverse), "absent, inverse " + inverse);
                    Abilities(character)[type] = probe;
                    Assert.That(Update(transition, character), Is.EqualTo(!inverse), "present, inverse " + inverse);
                    Abilities(character)[type] = null;
                    Assert.That(Update(transition, character), Is.EqualTo(!inverse), "a null entry is present, inverse " + inverse);
                }
                Abilities(character)[type] = probe;
                Assert.That(Update(Build(nameof(CharacterTransition_HasAbilityType), AbilityJson(type)), character), Is.True, "an omitted inverse flag is false");
            });
        }

        // ---- CharacterTransition_LedgeBufferEnd (020003f3) ----

        private static string LedgeBufferJson(float seconds) =>
            "{\"DurationSeconds\":" + seconds.ToString("R", CultureInfo.InvariantCulture) + "}";

        // 0x792990: false without an update-enabled air movement ability (byte +0x12 of the ability at 0xB79C3514);
        // otherwise the stored air ledge buffer timer against the configured duration with ucomiss / setae, so an
        // equal timer ends the buffer and an unordered NaN does not.
        [Test]
        public void LedgeBufferEndNeedsAnUpdatingAirAbilityAndATimerThatReachedTheDuration()
        {
            WithAbility<CharacterAbility_MovementAir, CharacterAbilityDefinition_MovementAir>((owned, character, ability, definition) =>
            {
                Abilities(character)[ActorAbilityType.Character_MovementAir] = ability;
                IFSMTransition transition = Build(nameof(CharacterTransition_LedgeBufferEnd), LedgeBufferJson(0.5f));

                ability.UpdateEnabled = false;
                character.Storage.SetValue(ActorFSMKeys.AirLedgeBufferTimer, 9f);
                Assert.That(Update(transition, character), Is.False, "the air ability is not updating");

                ability.UpdateEnabled = true;
                foreach ((float timer, bool expected) in new[] { (0f, false), (0.25f, false), (0.5f, true), (0.75f, true), (float.NaN, false) })
                {
                    character.Storage.SetValue(ActorFSMKeys.AirLedgeBufferTimer, timer);
                    Assert.That(Update(transition, character), Is.EqualTo(expected), "timer " + timer);
                }

                character.Storage.RemoveValue<float>(ActorFSMKeys.AirLedgeBufferTimer);
                Assert.That(Update(Build(nameof(CharacterTransition_LedgeBufferEnd), LedgeBufferJson(0f)), character), Is.True, "a missing timer reads zero, which reaches a zero duration");
            });
        }

        // ---- CharacterTransition_JumpBase / Jump / JumpOneShot (020003eb, 020003e9, 020003ee) ----

        private static string JumpJson(ActorAbilityType type, bool faceVelocityDirection) =>
            "{\"AbilityType\":" + (int)type + ",\"FaceVelocityDirection\":" + Bool(faceVelocityDirection) + "}";

        private static void WithJump(Action<Owned, CharacterGravityVectorProbe, CharacterAbility_MovementJump> body) =>
            WithAbility<CharacterAbility_MovementJump, CharacterAbilityDefinition_Jump>((owned, character, ability, definition) =>
            {
                Abilities(character)[ActorAbilityType.Character_Jump] = ability;
                body(owned, character, ability);
            });

        // 0xE79050: nothing fires while the ability is not jumping (byte +0xE8, 0xE79134); a running state timer
        // (ucomiss / jbe at 0xE79208: strictly positive) refuses; an unordered NaN timer does not. A jump that has
        // already run (JumpElapsedTime strictly positive, 0xE792E8 ja) fires without choosing a new direction.
        [Test]
        public void JumpFiresOnlyWhileTheAbilityJumpsAndNoStateTimerRuns()
        {
            WithJump((owned, character, ability) =>
            {
                IFSMTransition transition = Build(nameof(CharacterTransition_Jump), JumpJson(ActorAbilityType.Character_Jump, false));
                character.Storage.SetValue(ActorFSMKeys.CatchReleaseActive, true);

                SetProperty(ability, "Jump", false);
                Assert.That(Update(transition, character), Is.False, "the ability is not jumping");

                SetProperty(ability, "Jump", true);
                foreach ((float timer, bool expected) in new[] { (0f, true), (-1f, true), (0.5f, false), (float.NaN, true) })
                {
                    character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, timer);
                    Assert.That(Update(transition, character), Is.EqualTo(expected), "state timer " + timer);
                }

                character.Storage.SetValue(ActorFSMKeys.StateRemainTimer, 0f);
                foreach ((float elapsed, bool directionChosen) in new[] { (0f, true), (0.1f, false), (float.NaN, true) })
                {
                    character.Storage.RemoveValue<Vector3>(ActorFSMKeys.JumpDirection);
                    character.Storage.SetValue(ActorFSMKeys.JumpElapsedTime, elapsed);
                    Assert.That(Update(transition, character), Is.True, "elapsed " + elapsed);
                    Assert.That(character.Storage.HasValue(ActorFSMKeys.JumpDirection), Is.EqualTo(directionChosen), "elapsed " + elapsed);
                }
            });
        }

        // 0xE79050: the jump direction is the character's world up while a catch release, a rail jump or an air ledge
        // buffer from a rail is on (the three flags are all read, 0xE79315-0xE7950E, and any one selects up),
        // otherwise the active tracking's contact normal.
        [Test]
        public void TheJumpDirectionIsWorldUpOnRailsAndCatchReleaseAndTheContactNormalOtherwise()
        {
            WithJump((owned, character, ability) =>
            {
                IFSMTransition transition = Build(nameof(CharacterTransition_Jump), JumpJson(ActorAbilityType.Character_Jump, false));
                SetProperty(ability, "Jump", true);
                var tracking = new ContactTrackingProbe(character) { ContactNormal = new Vector3(0.6f, 0.8f, 0f) };
                SetProperty(character, "ActiveTracking", tracking);

                foreach (Flag flag in new[] { Flag.None, Flag.CatchRelease, Flag.JumpOnRail, Flag.LedgeFromRail })
                {
                    character.Storage.SetValue(ActorFSMKeys.CatchReleaseActive, flag == Flag.CatchRelease);
                    character.Storage.SetValue(ActorFSMKeys.JumpOnRailTracker, flag == Flag.JumpOnRail);
                    character.Storage.SetValue(ActorFSMKeys.AirLedgeBufferFromRail, flag == Flag.LedgeFromRail);
                    Assert.That(Update(transition, character), Is.True, flag.ToString());
                    Vector3 expected = flag == Flag.None ? new Vector3(0.6f, 0.8f, 0f) : Vector3.up;
                    Assert.That(character.Storage.GetValue(ActorFSMKeys.JumpDirection, Vector3.zero, false), Is.EqualTo(expected), flag.ToString());
                }
            });
        }

        // 0xE79050 0xE79611-0xE79664: a non zero extra angle (anything but exactly zero, so NaN too) rotates the direction
        // towards the character's gravity by minus the angle in radians (0x29A73AC is -pi/180), that is away from gravity
        // and never past the direction opposite to it. With gravity along (1, -1, 0) and a direction of up that opposite
        // direction is (-1, 1, 0) / sqrt 2, reached after 45 degrees.
        [Test]
        public void ANonZeroExtraAngleRotatesTheDirectionAwayFromGravity()
        {
            WithJump((owned, character, ability) =>
            {
                IFSMTransition transition = Build(nameof(CharacterTransition_Jump), JumpJson(ActorAbilityType.Character_Jump, false));
                SetProperty(ability, "Jump", true);
                character.GravityVector = new Vector3(20f, -20f, 0f);
                SetProperty(character, "ActiveTracking", new ContactTrackingProbe(character) { ContactNormal = Vector3.up });
                float root = Mathf.Sqrt(0.5f);
                foreach ((float angle, Vector3 expected) in new[]
                {
                    (0f, Vector3.up),
                    (30f, new Vector3(-0.5f, Mathf.Sqrt(3f) / 2f, 0f)),
                    (45f, new Vector3(-root, root, 0f)),
                    (90f, new Vector3(-root, root, 0f)),
                })
                {
                    character.Storage.SetValue(ActorFSMKeys.JumpAngleExtra, angle);
                    Assert.That(Update(transition, character), Is.True);
                    Vector3 direction = character.Storage.GetValue(ActorFSMKeys.JumpDirection, Vector3.zero, false);
                    Assert.That(Vector3.Distance(direction, expected), Is.LessThan(1e-4f), "angle " + angle + " gave " + direction);
                }
            });
        }

        // 0xE78E50: the result is recorded under JumpFaceVelocityDirection only for a transition configured to face the
        // velocity direction (byte +0x20, 0xE78EBE); 0xE78CC0 removes the record when the transition is entered.
        [Test]
        public void TheFaceVelocityDirectionRecordFollowsTheConfiguredFlag()
        {
            WithJump((owned, character, ability) =>
            {
                character.Storage.SetValue(ActorFSMKeys.CatchReleaseActive, true);
                IFSMTransition recording = Build(nameof(CharacterTransition_Jump), JumpJson(ActorAbilityType.Character_Jump, true));
                IFSMTransition silent = Build(nameof(CharacterTransition_Jump), JumpJson(ActorAbilityType.Character_Jump, false));

                foreach (bool jumping in new[] { true, false })
                {
                    SetProperty(ability, "Jump", jumping);
                    character.Storage.RemoveValue<bool>(ActorFSMKeys.JumpFaceVelocityDirection);
                    Assert.That(Update(silent, character), Is.EqualTo(jumping));
                    Assert.That(character.Storage.HasValue(ActorFSMKeys.JumpFaceVelocityDirection), Is.False, "not configured, jumping " + jumping);
                    Assert.That(Update(recording, character), Is.EqualTo(jumping));
                    Assert.That(character.Storage.GetValue(ActorFSMKeys.JumpFaceVelocityDirection, !jumping, false), Is.EqualTo(jumping), "configured, jumping " + jumping);
                }

                foreach (IFSMTransition transition in new[] { recording, silent })
                {
                    character.Storage.SetValue(ActorFSMKeys.JumpFaceVelocityDirection, true);
                    transition.OnEnter(character, null);
                    Assert.That(character.Storage.HasValue(ActorFSMKeys.JumpFaceVelocityDirection), Is.False, "entering removes the record");
                }
            });
        }

        // The jump transitions fetch the ability class of their own generic arguments (0xE78FF0 TryGetAbility<TAbility>):
        // an ability of the other jump class registered under their type is not found, whatever it is doing.
        [Test]
        public void EachJumpTransitionLooksForItsOwnAbilityClass()
        {
            WithJump((owned, character, jump) =>
            {
                var oneShot = new CharacterAbility_MovementJumpOneShot();
                Set(oneShot, "m_character", character);
                Set(oneShot, "m_definition", owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_JumpOneShot>()));
                SetProperty(jump, "Jump", true);
                SetProperty(oneShot, "Jump", true);
                character.Storage.SetValue(ActorFSMKeys.CatchReleaseActive, true);

                IFSMTransition jumpTransition = Build(nameof(CharacterTransition_Jump), JumpJson(ActorAbilityType.Character_Jump, false));
                IFSMTransition oneShotTransition = Build(nameof(CharacterTransition_JumpOneShot), JumpJson(ActorAbilityType.Character_JumpOneShot, false));

                Abilities(character)[ActorAbilityType.Character_Jump] = oneShot;
                Abilities(character)[ActorAbilityType.Character_JumpOneShot] = jump;
                Assert.That(Update(jumpTransition, character), Is.False, "a one shot ability under the jump type");
                Assert.That(Update(oneShotTransition, character), Is.False, "a jump ability under the one shot type");

                Abilities(character)[ActorAbilityType.Character_Jump] = jump;
                Abilities(character)[ActorAbilityType.Character_JumpOneShot] = oneShot;
                Assert.That(Update(jumpTransition, character), Is.True);
                Assert.That(Update(oneShotTransition, character), Is.True);

                Abilities(character).Remove(ActorAbilityType.Character_JumpOneShot);
                Assert.That(Update(oneShotTransition, character), Is.False, "no ability of the type");
            });
        }

        // ---- CharacterTransition_LightspeedDash and LightspeedDashExit (020003f6, 020003f7) ----

        private static void WithDash(Action<Owned, CharacterGravityVectorProbe, CharacterAbility_LightspeedDash, TrackManager, InertSpline> body) =>
            WithAbility<CharacterAbility_LightspeedDash, CharacterAbilityDefinition_LightspeedDash>((owned, character, ability, definition) =>
            {
                TrackManager trackManager = owned.Host("Lucid track manager", false).AddComponent<TrackManager>();
                InertSpline spline = owned.Host("Lucid dash spline", false).AddComponent<InertSpline>();
                body(owned, character, ability, trackManager, spline);
            });

        // 0x792E00: the dash ability of the configured type and a live track manager (UnityEngine.Object.op_Equality against
        // null, 0x792EE6) are both read before the result is decided with a non short-circuit and; with both, the transition
        // fires when an ISurface is stored as the dash target (0x792F2E HasValue<ISurface>), otherwise it is false.
        [Test]
        public void LightspeedDashNeedsTheAbilityATrackManagerAndAStoredTarget()
        {
            WithDash((owned, character, ability, trackManager, spline) =>
            {
                ActorAbilityType type = ActorAbilityType.Character_LightspeedDash;
                IFSMTransition transition = Build(nameof(CharacterTransition_LightspeedDash), AbilityJson(type));
                foreach (bool hasAbility in new[] { false, true })
                    foreach (bool hasTrackManager in new[] { false, true })
                        foreach (bool hasTarget in new[] { false, true })
                        {
                            Abilities(character).Remove(type);
                            if (hasAbility) Abilities(character)[type] = ability;
                            Set(character, "m_trackManagerRef", new SystemRef<TrackManager>("TrackManager", hasTrackManager ? trackManager : null));
                            character.Storage.RemoveValue<ISurface>(ActorFSMKeys.LightspeedDashTarget);
                            if (hasTarget) character.Storage.SetValue<ISurface>(ActorFSMKeys.LightspeedDashTarget, spline);
                            Assert.That(Update(transition, character), Is.EqualTo(hasAbility && hasTrackManager && hasTarget),
                                "ability " + hasAbility + ", track manager " + hasTrackManager + ", target " + hasTarget);
                        }
            });
        }

        // 0x793020: the exit is the base result or the absence of a stored dash target read as an ISpline (0x7930CC
        // HasValue<ISpline>, then xor 1); it does not consult the ability or the track manager.
        [Test]
        public void LightspeedDashExitsWhenNoTargetSplineIsStored()
        {
            WithDash((owned, character, ability, trackManager, spline) =>
            {
                IFSMTransition transition = Build(nameof(CharacterTransition_LightspeedDashExit), AbilityJson(ActorAbilityType.Character_LightspeedDash));
                Assert.That(Update(transition, character), Is.True, "no target stored");
                character.Storage.SetValue<ISpline>(ActorFSMKeys.LightspeedDashTarget, spline);
                Assert.That(Update(transition, character), Is.False, "a target spline is stored");
                character.Storage.RemoveValue<ISpline>(ActorFSMKeys.LightspeedDashTarget);
                Assert.That(Update(transition, character), Is.True);
            });
        }

        // The two dash transitions read the one target key as different types (0x792F27 ISurface, 0x7930C5 ISpline).
        [Test]
        public void TheDashTransitionsReadTheTargetAsDifferentTypes()
        {
            Assert.That(GenericArguments(Calls(typeof(CharacterTransition_LightspeedDash), "DoUpdate"), "HasValue"), Is.EqualTo(new[] { typeof(ISurface) }));
            Assert.That(GenericArguments(Calls(typeof(CharacterTransition_LightspeedDashExit), "DoUpdate"), "HasValue"), Is.EqualTo(new[] { typeof(ISpline) }));
        }

        private static Type[] GenericArguments(List<MethodBase> calls, string name) =>
            calls.Where(call => call.Name == name).SelectMany(call => ((MethodInfo)call).GetGenericArguments()).ToArray();

        // ---- IL witnesses ----

        // 0xE79050 and 0xE78E50: the base update is the first thing the shared jump update does, the ability is tried
        // before any timer is read, the extra angle is only read once the direction is chosen, and the record is the
        // last call of the public update.
        [Test]
        public void TheJumpUpdateFollowsTheNativeOrder()
        {
            List<MethodBase> calls = Calls(typeof(CharacterTransition_JumpBase<,>), "InternalDoUpdate");
            int baseUpdate = IndexOf(calls, "FSMTransition", "DoUpdate");
            int ability = IndexOf(calls, "CharacterTransition_JumpBase", "AbilityJumpTriggered");
            int remain = IndexOf(calls, "IGraphStorage", "GetValue");
            int rotate = IndexOf(calls, "Vector3", "RotateTowards");
            int direction = IndexOf(calls, "IGraphStorage", "SetValue");
            Assert.That(new[] { baseUpdate, ability, remain, rotate, direction }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(calls, "IGraphStorage", "GetValue"), Is.EqualTo(6), "state timer, elapsed time, three flags, extra angle");
            Assert.That(Count(calls, "IGraphStorage", "SetValue"), Is.EqualTo(1));
            Assert.That(Count(calls, "Actor", "get_WorldUp"), Is.EqualTo(1));
            Assert.That(Count(calls, "CharacterTracking", "GetContactNormal"), Is.EqualTo(1));

            List<MethodBase> update = Calls(typeof(CharacterTransition_JumpBase<,>), "DoUpdate");
            Assert.That(IndexOf(update, "CharacterTransition_JumpBase", "InternalDoUpdate"), Is.LessThan(IndexOf(update, "IGraphStorage", "SetValue")));
            List<MethodBase> enter = Calls(typeof(CharacterTransition_JumpBase<,>), "DoOnEnter");
            Assert.That(IndexOf(enter, "FSMTransition", "DoOnEnter"), Is.LessThan(IndexOf(enter, "IGraphStorage", "RemoveValue")));
        }

        // ---- Registration and graph authoring metadata ----

        private static void AssertNode(Type node, string defaultName)
        {
            Assert.That(node.GetCustomAttribute<GraphNodeDefaultNameAttribute>(false).DefaultName, Is.EqualTo(defaultName), node.Name);
            Assert.That(node.GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format, Is.EqualTo("Character/{0}"), node.Name);
        }

        // The default graph names and the Character menu of the six transitions; the abstract jump base carries neither.
        [Test]
        public void NodesCarryTheirOriginalGraphAuthoringAttributes()
        {
            AssertNode(typeof(CharacterTransition_HasAbilityType), "HasAbilityType");
            AssertNode(typeof(CharacterTransition_Jump), "ToJump");
            AssertNode(typeof(CharacterTransition_JumpOneShot), "ToJumpOneShot");
            AssertNode(typeof(CharacterTransition_LedgeBufferEnd), "LedgeBufferEnd");
            AssertNode(typeof(CharacterTransition_LightspeedDash), "ToLightspeedDash");
            AssertNode(typeof(CharacterTransition_LightspeedDashExit), "LightspeedDashExit");
            Assert.That(typeof(CharacterTransition_JumpBase<,>).GetCustomAttribute<GraphNodeDefaultNameAttribute>(false), Is.Null);
            Assert.That(typeof(CharacterTransition_JumpBase<,>).GetCustomAttribute<GraphNodeMenuFormatAttribute>(false), Is.Null);
        }

        private static void AssertRegistered(Type node, string argsName, Type argsOwner)
        {
            Assert.That(FSMClassFactory.Transitions.TryGetValue(node.Name, out JSONFactoryClass factory), Is.True, node.Name);
            Assert.That(factory.ClassType, Is.EqualTo(node), node.Name);
            Assert.That(factory.ConstructInstanceMethod.DeclaringType, Is.EqualTo(node), node.Name);
            Assert.That(factory.JSONCtorArgsType.Name, Is.EqualTo(argsName), node.Name);
            Assert.That(factory.JSONCtorArgsType.DeclaringType, Is.EqualTo(argsOwner), node.Name);
        }

        // The factory finds each node by its static ConstructInstance and its argument type as the nested class carrying
        // the FSM argument attribute (or named JSONCtorArgs); the dash transitions share the ability argument class of
        // Transition_Ability, and the abstract jump base is never registered.
        [Test]
        public void ClassFactoryRegistersTheNodesWithTheirArgumentTypes()
        {
            AssertRegistered(typeof(CharacterTransition_HasAbilityType), "HasAbilityTypeJSONCtorArgs", typeof(CharacterTransition_HasAbilityType));
            AssertRegistered(typeof(CharacterTransition_Jump), "JumpTransitionJSONCtorArgs", typeof(CharacterTransition_Jump));
            AssertRegistered(typeof(CharacterTransition_JumpOneShot), "JumpOneShotTransitionJSONCtorArgs", typeof(CharacterTransition_JumpOneShot));
            AssertRegistered(typeof(CharacterTransition_LedgeBufferEnd), "JSONCtorArgs", typeof(CharacterTransition_LedgeBufferEnd));
            AssertRegistered(typeof(CharacterTransition_LightspeedDash), "AbilityJSONCtorArgs", typeof(Transition_Ability));
            AssertRegistered(typeof(CharacterTransition_LightspeedDashExit), "AbilityJSONCtorArgs", typeof(Transition_Ability));
            Assert.That(FSMClassFactory.Transitions.Keys.Any(key => key.StartsWith("CharacterTransition_JumpBase", StringComparison.Ordinal)), Is.False);
        }

        // Field order, the readonly flags and the argument classes: has ability type keeps one readonly flag, ledge buffer end
        // one writable duration, the jump base one readonly flag, and the shared jump argument class adds one serialised flag
        // to the ability argument class.
        [Test]
        public void DeclarationShapeTransitionsKeepTheirFieldsBasesAndArgumentClasses()
        {
            const BindingFlags declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo[] FieldsOf(Type type) => type.GetFields(declared).OrderBy(field => field.MetadataToken).ToArray();

            FieldInfo[] inverse = FieldsOf(typeof(CharacterTransition_HasAbilityType));
            Assert.That(inverse.Select(f => f.Name), Is.EqualTo(new[] { "m_inverseCheck" }));
            Assert.That(inverse[0].IsInitOnly && inverse[0].IsPrivate, Is.True);
            FieldInfo[] duration = FieldsOf(typeof(CharacterTransition_LedgeBufferEnd));
            Assert.That(duration.Select(f => f.Name), Is.EqualTo(new[] { "m_durationSeconds" }));
            Assert.That(duration[0].IsInitOnly, Is.False);
            FieldInfo[] face = FieldsOf(typeof(CharacterTransition_JumpBase<,>));
            Assert.That(face.Select(f => f.Name), Is.EqualTo(new[] { "m_faceVelocityDirection" }));
            Assert.That(face[0].IsInitOnly && face[0].IsPrivate, Is.True);

            Assert.That(typeof(CharacterTransition_HasAbilityType).BaseType, Is.EqualTo(typeof(Transition_Ability)));
            Assert.That(typeof(CharacterTransition_LightspeedDash).BaseType, Is.EqualTo(typeof(Transition_Ability)));
            Assert.That(typeof(CharacterTransition_LightspeedDashExit).BaseType, Is.EqualTo(typeof(Transition_Ability)));
            Assert.That(typeof(CharacterTransition_LedgeBufferEnd).BaseType, Is.EqualTo(typeof(FSMTransition)));
            Assert.That(typeof(CharacterTransition_Jump).BaseType, Is.EqualTo(typeof(CharacterTransition_JumpBase<CharacterAbility_MovementJump, CharacterAbilityDefinition_Jump>)));
            Assert.That(typeof(CharacterTransition_JumpOneShot).BaseType,
                Is.EqualTo(typeof(CharacterTransition_JumpBase<CharacterAbility_MovementJumpOneShot, CharacterAbilityDefinition_JumpOneShot>)));
            Assert.That(typeof(CharacterTransition_JumpBase<,>).IsAbstract, Is.True);
            Type[] constraints = typeof(CharacterTransition_JumpBase<,>).GetGenericArguments().Select(argument => argument.GetGenericParameterConstraints().Single()).ToArray();
            Assert.That(constraints[0].GetGenericTypeDefinition(), Is.EqualTo(typeof(CharacterAbility_MovementJumpBase<>)));
            Assert.That(constraints[1], Is.EqualTo(typeof(CharacterAbilityDefinition_Jump)));

            Type baseArgs = typeof(CharacterTransition_JumpBase<,>).GetNestedType("JumpTransitionBaseJSONCtorArgs", BindingFlags.NonPublic);
            Assert.That(baseArgs.IsNestedFamily && baseArgs.IsSerializable, Is.True);
            Assert.That(baseArgs.GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "FaceVelocityDirection" }));
            Assert.That(baseArgs.BaseType, Is.EqualTo(typeof(Transition_Ability).GetNestedType("AbilityJSONCtorArgs", BindingFlags.NonPublic)));
            Type inverseArgs = typeof(CharacterTransition_HasAbilityType).GetNestedType("HasAbilityTypeJSONCtorArgs", BindingFlags.NonPublic);
            Assert.That(inverseArgs.GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "InverseCheck" }));
            Type durationArgs = typeof(CharacterTransition_LedgeBufferEnd).GetNestedType("JSONCtorArgs", BindingFlags.NonPublic);
            Assert.That(durationArgs.IsNestedPrivate && durationArgs.IsSerializable, Is.True);
            Assert.That(durationArgs.GetFields(declared).Select(f => f.Name), Is.EqualTo(new[] { "DurationSeconds" }));
        }
    }
}
