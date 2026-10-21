using System;
using System.Linq;
using System.Reflection;
using System.Text;
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
    // The rail boost, rail standing and rail switch abilities, their definitions and the rail switch transition. Expected
    // values come from the 1.10.1 x86-64 bodies and constructors cited per test: the packed field defaults are the
    // constants of the binary's data section (VA 0x29a7770, 0x29a7780, 0x29a7790, 0x29a77d0, 0x29a7720), never read
    // back from the implementation. Abilities are built by their real constructors and given the character and the
    // definition the way ActorAbility.Initialise would, on a real Character on an owned inactive host. Initialise,
    // DoOnEnter, ProjectToTargetedObject, GetProjectedRailTarget, GetTargetSectionLocation and UpdateJumpRequirement
    // are not covered by any test: they read the tracker and track manager of the character, and no tracker setup
    // for this ability has been verified here (UpdateJumpRequirement: ucomiss 0.1 > sqr, jbe, at 0x7523ED). Of
    // DoOnUpdate only the trigger ability gate (0x7527C5..0x7527CC) is covered.
    public sealed class CharacterRailAbilityTests
    {
        private const float Tolerance = 1e-5f;

        private static string Name() => "Lucid-rail-" + Guid.NewGuid().ToString("N");

        private static void WithRailSwitch(Action<Owned, CharacterGravityVectorProbe, CharacterAbility_RailSwitch, CharacterAbilityDefinition_RailSwitch> body) =>
            WithAbility<CharacterAbility_RailSwitch, CharacterAbilityDefinition_RailSwitch>(body);

        private static CharacterTargetObjectData Target(HomingTargetType type) => new CharacterTargetObjectData { Type = type };

        // ---- Definitions ----

        // 0x545040: the constructor stores the packed floats of 0x29a77d0 (45, 10, 0.5, 1) over the four fields at +0xD4,
        // 0.1f (0x3dcccccd) at +0xF0, a new list at +0xF8 and 0.5f (0x3f000000) at +0x100. Its inlined bases store the
        // pair 0x29a7720 (0.1, 0.1) at +0xCC and the gravity multiplier 1.0f at +0x50. The curve and the time scaling flag
        // stay unset.
        [Test]
        public void RailSwitchDefinitionStoresTheNativeDefaults()
        {
            Owned.Run(owned =>
            {
                var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_RailSwitch>());
                Assert.That((definition.RailProjectAngle, definition.RailProjectDistance, definition.ActivationInputThreshold, definition.ActivationInputTime),
                    Is.EqualTo((45f, 10f, 0.5f, 1f)));
                Assert.That((definition.TurnAroundCooldown, definition.TriggerGracePeriod), Is.EqualTo((0.1f, 0.5f)));
                Assert.That((definition.TargetingIntervalSeconds, definition.CooldownTimeSeconds, definition.GravityMultiplier), Is.EqualTo((0.1f, 0.1f, 1f)));
                Assert.That(definition.TriggerAbilities, Is.Not.Null.And.Empty);
                Assert.That(definition.JumpCurve, Is.Null);
                Assert.That(definition.UseTimeScaling, Is.False);
                Assert.That(definition.RailProjectDistanceSqr, Is.EqualTo(0f), "the cached square is only set by CalculateCachedValues");
            });
        }

        // 0x545010: the base call, then mulss of the distance with itself stored at +0xF4 (so a negative distance squares
        // to a positive one and an unordered one stays unordered).
        [Test]
        public void RailSwitchDefinitionCachesTheSquareOfTheProjectDistance()
        {
            Owned.Run(owned =>
            {
                var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_RailSwitch>());
                foreach ((float distance, float square) in new[] { (3f, 9f), (0f, 0f), (-2f, 4f), (0.5f, 0.25f) })
                {
                    definition.RailProjectDistance = distance;
                    definition.CalculateCachedValues();
                    Assert.That(definition.RailProjectDistanceSqr, Is.EqualTo(square), "distance " + distance);
                }
                definition.RailProjectDistance = float.NaN;
                definition.CalculateCachedValues();
                Assert.That(float.IsNaN(definition.RailProjectDistanceSqr), Is.True);
            });
        }

        // 0x5447A0 and 0x544970: both constructors store the same packed rail defaults of their inlined base: 0x29a7770
        // (35, 0.5, 1, 50) at +0xE4, 0x29a7780 (1, 45) at +0xF4 and 0x29a7790 (4, 35) at +0x108.
        [Test]
        public void RailBoostAndStandingDefinitionsStoreTheNativeRailDefaults()
        {
            Owned.Run(owned =>
            {
                foreach (Type type in new[] { typeof(CharacterAbilityDefinition_MovementRailBoost), typeof(CharacterAbilityDefinition_MovementRailStanding) })
                {
                    var definition = (CharacterAbilityDefinition_MovementRail)owned.Track(ScriptableObject.CreateInstance(type));
                    Assert.That((definition.AttachSpeedMin, definition.TurnAroundInputThreshold, definition.TurnAroundInputTime, definition.TurnAroundDeceleration),
                        Is.EqualTo((35f, 0.5f, 1f, 50f)), type.Name);
                    Assert.That((definition.TurnAroundSpeedThreshold, definition.JumpAngleOffGravityThreshold), Is.EqualTo((1f, 45f)), type.Name);
                    Assert.That((definition.JumpDistanceMax, definition.JumpOnRailAngleMax), Is.EqualTo((4f, 35f)), type.Name);
                    Assert.That(definition.StateTimeMin, Is.EqualTo(0f), type.Name);
                }
            });
        }

        // 0x544740, 0x544910 and 0x544F90 load the runtime type of their ability.
        [Test]
        public void DefinitionsNameTheirAbilityTypes()
        {
            Owned.Run(owned =>
            {
                foreach ((Type definition, Type ability) in new[]
                {
                    (typeof(CharacterAbilityDefinition_MovementRailBoost), typeof(CharacterAbility_MovementRailBoost)),
                    (typeof(CharacterAbilityDefinition_MovementRailStanding), typeof(CharacterAbility_MovementRailStanding)),
                    (typeof(CharacterAbilityDefinition_RailSwitch), typeof(CharacterAbility_RailSwitch)),
                })
                {
                    var instance = (AbilityDefinition)owned.Track(ScriptableObject.CreateInstance(definition));
                    Assert.That(instance.ScriptType, Is.EqualTo(ability), definition.Name);
                }
            });
        }

        // ---- CharacterAbility_MovementRailBoost and CharacterAbility_MovementRailStanding ----

        // 0x74A8C0: the boost may trigger unless the one-shot boost flag reads true (Actor.TryGetValue, then xor 1); an
        // absent key and a stored false both leave it open.
        [Test]
        public void RailBoostCanTriggerUnlessTheOneShotFlagIsSet()
        {
            WithAbility<CharacterAbility_MovementRailBoost, CharacterAbilityDefinition_MovementRailBoost>((owned, character, ability, definition) =>
            {
                Assert.That((bool)Call(ability, "CanTrigger"), Is.True, "no flag");
                character.Storage.SetValue(ActorFSMKeys.OneShotCanTriggerBoost, true);
                Assert.That((bool)Call(ability, "CanTrigger"), Is.False, "the one-shot may trigger");
                character.Storage.SetValue(ActorFSMKeys.OneShotCanTriggerBoost, false);
                Assert.That((bool)Call(ability, "CanTrigger"), Is.True, "the flag is false");
            });
        }

        // 0x74AE90 stores the argument into the triggered flag of the rail ability (+0x13A) and nothing else.
        [Test]
        public void RailStandingSetActiveStoresTheTriggeredFlag()
        {
            WithAbility<CharacterAbility_MovementRailStanding, CharacterAbilityDefinition_MovementRailStanding>((owned, character, ability, definition) =>
            {
                Assert.That(ability.IsTriggered, Is.False);
                ability.SetActive(true);
                Assert.That(ability.IsTriggered, Is.True);
                ability.SetActive(false);
                Assert.That(ability.IsTriggered, Is.False);
            });
        }

        // ---- CharacterAbility_RailSwitch ----

        // The snap tolerance literal (field 04000b29) is 0.1f, the constant at 0x29a6bfc (0x3dcccccd) that every squared
        // distance test of the ability compares against.
        [Test]
        public void RailSwitchKeepsTheNativeSnapTolerance()
        {
            FieldInfo field = typeof(CharacterAbility_RailSwitch).GetField("SnapToRailTolerance", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field.IsLiteral, Is.True);
            Assert.That(field.GetRawConstantValue(), Is.EqualTo(0.1f));
            Assert.That(typeof(CharacterAbility_RailSwitch).BaseType, Is.EqualTo(typeof(CharacterAbility_Targeting<CharacterAbilityDefinition_RailSwitch>)));
        }

        // 0x754270, 0x7542C0 and 0x754310 read the interval (+0xCC), the cooldown (+0xD0) and the time scaling flag
        // (+0x104) of the definition.
        [Test]
        public void RailSwitchTargetingSettingsComeFromTheDefinition()
        {
            WithRailSwitch((owned, character, ability, definition) =>
            {
                foreach ((float interval, float cooldown, bool scaling) in new[] { (0.3f, 0.7f, true), (2f, 4f, false) })
                {
                    definition.TargetingIntervalSeconds = interval;
                    definition.CooldownTimeSeconds = cooldown;
                    definition.UseTimeScaling = scaling;
                    Assert.That(typeof(CharacterAbility_RailSwitch).GetProperty("TargetingIntervalSeconds", Instance).GetValue(ability), Is.EqualTo(interval));
                    Assert.That(typeof(CharacterAbility_RailSwitch).GetProperty("CooldownTimeSeconds", Instance).GetValue(ability), Is.EqualTo(cooldown));
                    Assert.That(typeof(CharacterAbility_RailSwitch).GetProperty("UseTimeScaling", Instance).GetValue(ability), Is.EqualTo(scaling));
                }
            });
        }

        // 0x752520: the input, the published target and the targeted object and surface are cleared; the cooldown is
        // written only when asked for and positive (ucomiss against zero then jbe at 0x752613, so zero, negative and
        // unordered cooldowns write nothing).
        [Test]
        public void ResetClearsTheInputTheTargetAndTheTargetedLocation()
        {
            WithRailSwitch((owned, character, ability, definition) =>
            {
                InertSpline spline = owned.Host(Name(), false).AddComponent<InertSpline>();
                GameObject targetObject = owned.Host(Name(), false);
                Call(ability, "SetFSMTarget", Target(HomingTargetType.Rail), 9f);
                Set(ability, "m_inputMovement", 0.75f);
                CharacterTargetObjectData targeted = Get<CharacterTargetObjectData>(ability, "m_targetedObject");
                targeted.Target = targetObject;
                Set(ability, "m_targetedObject", targeted);
                SurfaceLocation location = Get<SurfaceLocation>(ability, "m_targetedLocation");
                location.m_surface = spline;
                Set(ability, "m_targetedLocation", location);

                ability.Reset();

                Assert.That(Get<float>(ability, "m_inputMovement"), Is.EqualTo(0f));
                Assert.That(Get<CharacterTargetObjectData>(ability, "m_targetedObject").Target, Is.Null);
                Assert.That(Get<SurfaceLocation>(ability, "m_targetedLocation").m_surface, Is.Null);
                Assert.That(ability.GetFSMTarget(out CharacterTargetObjectData? published, out float speed), Is.False);
                Assert.That((published.HasValue, speed), Is.EqualTo((false, 0f)));
            });
        }

        [Test]
        public void ResetWritesTheTurnAroundCooldownOnlyWhenAskedAndPositive()
        {
            WithRailSwitch((owned, character, ability, definition) =>
            {
                foreach ((bool activate, float cooldown, bool written) in new[]
                {
                    (false, 0.1f, false), (true, 0.1f, true), (true, 2.5f, true), (true, 0f, false), (true, -1f, false), (true, float.NaN, false),
                })
                {
                    character.Storage.RemoveValue<float>(ActorFSMKeys.RailTurnAroundCooldown);
                    definition.TurnAroundCooldown = cooldown;
                    ability.Reset(activate);
                    bool stored = character.Storage.TryGetValue(ActorFSMKeys.RailTurnAroundCooldown, out float value);
                    Assert.That(stored, Is.EqualTo(written), "activate " + activate + ", cooldown " + cooldown);
                    if (written) Assert.That(value, Is.EqualTo(cooldown));
                }
            });
        }

        // 0x753D60: the character's forward turned about its own up axis by the projection angle: positive for input at or
        // above zero, negative below it. The select compares 0 <= input (cmpnless 0, input at 0x753DFD), so an unordered
        // input takes the negative angle and negative zero the positive one.
        [Test]
        public void ProjectedDirectionTurnsTheForwardToTheSideOfTheInput()
        {
            WithRailSwitch((owned, character, ability, definition) =>
            {
                definition.RailProjectAngle = 30f;
                float half = 0.5f;
                float cosine = Mathf.Cos(30f * Mathf.Deg2Rad);
                foreach ((float input, float expectedX, string why) in new[]
                {
                    (0.2f, half, "positive input"), (0f, half, "zero input"), (-0f, half, "negative zero"),
                    (-0.2f, -half, "negative input"), (float.NaN, -half, "unordered input"),
                })
                {
                    Set(ability, "m_inputMovement", input);
                    var direction = (Vector3)Call(ability, "GetProjectedDirection");
                    Assert.That(direction.x, Is.EqualTo(expectedX).Within(Tolerance), why);
                    Assert.That(direction.y, Is.EqualTo(0f).Within(Tolerance), why);
                    Assert.That(direction.z, Is.EqualTo(cosine).Within(Tolerance), why);
                }
            });
        }

        // 0x753E5A: the turned forward is then multiplied by the character's rotation (quaternion at +0x54).
        [Test]
        public void ProjectedDirectionIsRotatedByTheCharacter()
        {
            WithRailSwitch((owned, character, ability, definition) =>
            {
                definition.RailProjectAngle = 30f;
                SetActorProperty(character, "WorldRotation", Quaternion.Euler(0f, 90f, 0f));
                Set(ability, "m_inputMovement", 1f);
                var direction = (Vector3)Call(ability, "GetProjectedDirection");
                Assert.That(direction.x, Is.EqualTo(Mathf.Cos(30f * Mathf.Deg2Rad)).Within(Tolerance));
                Assert.That(direction.y, Is.EqualTo(0f).Within(Tolerance));
                Assert.That(direction.z, Is.EqualTo(-0.5f).Within(Tolerance));
            });
        }

        // 0x753E80: a switch is in progress while a neighbour rail is stored and the published target is a rail
        // (HasValue, then the type compared with 0x9036f9ca); every other combination is false.
        [Test]
        public void SwitchInProgressNeedsANeighbourAndARailTarget()
        {
            WithRailSwitch((owned, character, ability, definition) =>
            {
                InertSpline spline = owned.Host(Name(), false).AddComponent<InertSpline>();
                foreach (bool neighbour in new[] { false, true })
                    foreach (HomingTargetType? type in new HomingTargetType?[] { null, HomingTargetType.Rail, HomingTargetType.Enemy })
                    {
                        character.Storage.RemoveValue<ISurface>(ActorFSMKeys.RailSwitchNeighbour);
                        if (neighbour) character.Storage.SetValue<ISurface>(ActorFSMKeys.RailSwitchNeighbour, spline);
                        if (type.HasValue) Call(ability, "SetFSMTarget", Target(type.Value), 5f);
                        else Call(ability, "SetFSMTarget", null, 0f);
                        Assert.That(ability.SwitchInProgress(), Is.EqualTo(neighbour && type == HomingTargetType.Rail), "neighbour " + neighbour + ", target " + type);
                    }
            });
        }

        // 0x7527C5..0x7527CC: with no trigger ability in use (an empty list here) the update returns before the base
        // update, the input read or the reset, so the input and the published target stay as they were. The character
        // is on a rail, so the later rail gate (0x752942) would not return.
        [Test]
        public void UpdateWithoutAnActiveTriggerAbilityChangesNothing()
        {
            WithRailSwitch((owned, character, ability, definition) =>
            {
                character.Storage.SetValue(ActorFSMKeys.RailActive, true);
                Call(ability, "SetFSMTarget", Target(HomingTargetType.Rail), 9f);
                Set(ability, "m_inputMovement", 0.75f);

                Call(ability, "DoOnUpdate", null, 0.1f);

                Assert.That(Get<float>(ability, "m_inputMovement"), Is.EqualTo(0.75f));
                Assert.That(ability.GetFSMTarget(out CharacterTargetObjectData? target, out float speed), Is.True);
                Assert.That((target.Value.Type, speed), Is.EqualTo((HomingTargetType.Rail, 9f)));
            });
        }

        // 0x753310 and 0x754000 store and read the target and its speed under their own keys.
        [Test]
        public void TheFsmTargetRoundTripsThroughTheStorage()
        {
            WithRailSwitch((owned, character, ability, definition) =>
            {
                Assert.That(ability.GetFSMTarget(out CharacterTargetObjectData? empty, out float emptySpeed), Is.False, "nothing published");
                Assert.That((empty.HasValue, emptySpeed), Is.EqualTo((false, 0f)));
                Call(ability, "SetFSMTarget", Target(HomingTargetType.Rail), 7.5f);
                Assert.That(ability.GetFSMTarget(out CharacterTargetObjectData? target, out float speed), Is.True);
                Assert.That(target.Value.Type, Is.EqualTo(HomingTargetType.Rail));
                Assert.That(speed, Is.EqualTo(7.5f));
                Assert.That(character.Storage.GetValue(ActorFSMKeys.HomingRailSwitchSpeed, -1f, false), Is.EqualTo(7.5f));
            });
        }

        // 0x754360: the targeting base writes the target line, then the movement is formatted, and a targeted surface that
        // is a live component names its game object.
        [Test]
        public void DebugInfoNamesTheMovementAndTheTargetedSurface()
        {
            WithRailSwitch((owned, character, ability, definition) =>
            {
                string hostName = Name();
                InertSpline spline = owned.Host(hostName, false).AddComponent<InertSpline>();
                var info = new StringBuilder();
                Set(ability, "m_inputMovement", 2f);
                Call(ability, "GetDebugExtraInfo", info);
                Assert.That(info.ToString(), Is.EqualTo("Target = None Movement = 2 "), "no surface");

                SurfaceLocation location = Get<SurfaceLocation>(ability, "m_targetedLocation");
                location.m_surface = spline;
                Set(ability, "m_targetedLocation", location);
                info.Clear();
                Call(ability, "GetDebugExtraInfo", info);
                Assert.That(info.ToString(), Is.EqualTo("Target = None Movement = 2 Target = " + hostName + " "), "a component surface");

                UnityEngine.Object.DestroyImmediate(spline);
                info.Clear();
                Call(ability, "GetDebugExtraInfo", info);
                Assert.That(info.ToString(), Is.EqualTo("Target = None Movement = 2 "), "a destroyed component is not named");
            });
        }

        // ---- CharacterTransition_RailSwitch ----

        private static IFSMTransition Build(string json) =>
            FSMClassFactory.ConstructTransition(new FiniteStateMachine(Name(), skipAddToManager: true), nameof(CharacterTransition_RailSwitch), new FSMIdentifier(Name()), json);

        private static bool Update(IFSMTransition transition, Character character) =>
            transition.Update(character, new FSMUpdateContext(0.02f, FSMUpdateType.FixedUpdate));

        // 0x793720: the transition fires while the rail switch ability reports a switch in progress; without the ability
        // (0xf417852b) it does not.
        [Test]
        public void TransitionFiresOnlyWhileTheAbilityHasASwitchInProgress()
        {
            WithRailSwitch((owned, character, ability, definition) =>
            {
                IFSMTransition transition = Build("{}");
                Assert.That(transition.GetType(), Is.EqualTo(typeof(CharacterTransition_RailSwitch)));
                var abilities = Get<System.Collections.Generic.Dictionary<ActorAbilityType, ActorAbility>>(character, "m_abilities");
                InertSpline spline = owned.Host(Name(), false).AddComponent<InertSpline>();
                character.Storage.SetValue<ISurface>(ActorFSMKeys.RailSwitchNeighbour, spline);
                Call(ability, "SetFSMTarget", Target(HomingTargetType.Rail), 5f);

                Assert.That(Update(transition, character), Is.False, "no ability registered");
                abilities[ActorAbilityType.Character_RailSwitch] = ability;
                Assert.That(Update(transition, character), Is.True, "a switch is in progress");
                character.Storage.RemoveValue<ISurface>(ActorFSMKeys.RailSwitchNeighbour);
                Assert.That(Update(transition, character), Is.False, "the neighbour was released");
            });
        }

        // The transition declares the node names the graph editor shows.
        [Test]
        public void TransitionKeepsItsGraphNodeAttributes()
        {
            Type type = typeof(CharacterTransition_RailSwitch);
            Assert.That(type.GetCustomAttributes(typeof(GraphNodeDefaultNameAttribute), false).Cast<GraphNodeDefaultNameAttribute>().Single().DefaultName, Is.EqualTo("ToRailSwitch"));
            Assert.That(type.GetCustomAttributes(typeof(GraphNodeMenuFormatAttribute), false), Has.Length.EqualTo(1));
            Assert.That(type.BaseType, Is.EqualTo(typeof(FSMTransition)));
        }
    }
}
