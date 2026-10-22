using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using ProjectLucid.Tests.WorldSurfaceShared;
using UnityEngine;
using static ProjectLucid.Tests.CharacterAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The transporter proximity ability and definition, the two transporter character states, and the gravity, homing
    // target, game play control, respawn, camera proxy, sticky controls and zero velocity trigger states. Expected values
    // come from the 1.10.1 x86-64 bodies cited per test. The declaration rows are the original DummyDll ECMA-335 tables.
    // The states are built from their JSON through the node factory and run against a real Character on an owned inactive
    // host; the effect states get the animation storage the started Actor would have created.
    // Not covered by any test, because they need a started Character, a live tracker, camera or UI manager, or the data
    // manager: the TransporterMovement enter, update and leave bodies and its UpdateVelocity, OrientateToTracker (the
    // 1e-4 squared speed gate) and OrientateOnExit helpers beyond the call-order witnesses below, the
    // TransporterHomingProximity enter, update and leave bodies beyond those witnesses (their ability state base chain
    // sets camera overrides and reads the input modifier), the ProximityCheckToTargetedObject search through the track
    // manager's transporter octrees (TryGetTransporterHit) and its distance gate, the TriggerRespawn enter, SetupRespawn,
    // RespawnCharacter and the TriggerRespawn iterator, the GamePlayControl and SetCameraProxySettings and StickyControls
    // trigger starts, and the construction of the two states that look their data up by name.
    public sealed class CharacterTransporterAndTriggerStateTests : OwnedGraphNamesFixture
    {
        public CharacterTransporterAndTriggerStateTests() : base("Lucid-transporter-trigger-") { }

        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static string Name() => "Lucid-transporter-trigger-" + Guid.NewGuid().ToString("N");

        private static IFSMState NewState(string node, string json) =>
            FSMClassFactory.ConstructState(new FiniteStateMachine(Name(), skipAddToManager: true), node, new FSMIdentifier(Name()), json);

        private static string AbilityJson(ActorAbilityType type) => "{\"AbilityType\":" + (int)type + "}";

        private static FSMUpdateContext Context() => new FSMUpdateContext(0.02f, FSMUpdateType.FixedUpdate);

        private static void WithStateCharacter(Action<Owned, CharacterGravityVectorProbe> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            body(owned, NewCharacter(owned));
        });

        // The two dictionaries the effect states read from the character's storage; a started Actor creates them.
        private static void PrimeEffectStorage(Character character)
        {
            character.Storage.SetValue(ActorFSMKeys.ActiveAnimationLookup, new Dictionary<string, ActorAnimationDefinition>());
            character.Storage.SetValue(ActorFSMKeys.FullscreenEffectDictionary, new Dictionary<int, List<FullscreenShaderManager.ParametersHandle>>());
        }

        // TargetingHoming's field initialiser creates the camera manager row and the homing pool the message manager row.
        private static void WithTransporterAbility(Action<Owned, CharacterGravityVectorProbe, CharacterAbility_TransporterProximity, CharacterAbilityDefinition_TransporterProximity> body) =>
            WithAbility<CharacterAbility_TransporterProximity, CharacterAbilityDefinition_TransporterProximity>(owned =>
            {
                owned.LeaseRow<MessageManager>();
                owned.LeaseRow<CinemachineCameraManager>();
            }, (owned, character, ability, definition) =>
            {
                Set(character, "m_trackManagerRef", new SystemRef<TrackManager>("TrackManager", null));
                SetProperty(character, "HomingPool", new CharacterHomingPool(character, new Dictionary<HomingTargetType, TargetingTypePriorityDefinition>()));
                body(owned, character, ability, definition);
            });

        private static Type Nested(Type owner, string name) => owner.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);

        private static string[] AttributeNames(Type type) => type.GetCustomAttributesData()
            .Where(data => data.AttributeType != typeof(SerializableAttribute))
            .Select(data => data.AttributeType.Name == "Il2CppSetOptionAttribute"
                ? "Il2CppSetOption:" + Convert.ToInt32(data.ConstructorArguments[0].Value)
                : data.AttributeType.Name.Replace("Attribute", "")).ToArray();

        private static void AssertShape(Type type, int typeFlags, Type baseType, string[] attributes,
            (string name, int flags)[] fields, (string name, int flags)[] methods)
        {
            AssertMembers(type, typeFlags, fields, methods);
            Type actualBase = type.BaseType.IsGenericType ? type.BaseType.GetGenericTypeDefinition() : type.BaseType;
            Assert.That(actualBase, Is.EqualTo(baseType), type.FullName + " base");
            Assert.That(AttributeNames(type), Is.EqualTo(attributes), type.FullName + " attributes");
        }

        // ---- Construction from JSON ----

        // Defaults come from the argument constructors: GravityEnabled is set to one at 0x783884, TimeCategory to
        // 0x67b16ba4 (PlayerPhysics) at 0x7831E4 and DoFullscreenTransition to one at 0x784914; every other field keeps
        // its zero. The constructors copy the fields (0x783365, 0x7867D5, 0x783D9B..0x783DD3); an empty container name
        // is not looked up (String.IsNullOrEmpty at 0x783DAC).
        [Test]
        public void TriggerStatesReadTheirArgumentsFromJson()
        {
            IFSMState gravity = NewState(nameof(CharacterState_TriggerGravity), "{}");
            Assert.That(gravity.GetType(), Is.EqualTo(typeof(CharacterState_TriggerGravity)));
            Assert.That(Get<bool>(gravity, "m_gravityEnabled"), Is.True, "gravity is on unless the node says otherwise");
            Assert.That(Get<bool>(NewState(nameof(CharacterState_TriggerGravity), "{\"GravityEnabled\":false}"), "m_gravityEnabled"), Is.False);

            Assert.That(Get<bool>(NewState(nameof(CharacterState_TriggerClearHomingTarget), "{}"), "m_startGracePeriod"), Is.False);
            Assert.That(Get<bool>(NewState(nameof(CharacterState_TriggerClearHomingTarget), "{\"StartGracePeriod\":true}"), "m_startGracePeriod"), Is.True);

            Assert.That(Get<bool>(NewState(nameof(CharacterState_ZeroVelocity), "{}"), "m_onUpdate"), Is.False);
            Assert.That(Get<bool>(NewState(nameof(CharacterState_ZeroVelocity), "{\"MaintainZeroVelocityInUpdate\":true}"), "m_onUpdate"), Is.True);

            IFSMState control = NewState(nameof(CharacterState_TriggerGamePlayControl), "{}");
            Assert.That(Get<float>(control, "m_controlLockTime"), Is.EqualTo(0f));
            Assert.That(Get<TimeCategory>(control, "m_timeCategory"), Is.EqualTo(TimeCategory.PlayerPhysics));
            control = NewState(nameof(CharacterState_TriggerGamePlayControl), "{\"ControlLockTime\":1.5,\"TimeCategory\":" + (int)TimeCategory.PlayerMovement + "}");
            Assert.That(Get<float>(control, "m_controlLockTime"), Is.EqualTo(1.5f));
            Assert.That(Get<TimeCategory>(control, "m_timeCategory"), Is.EqualTo(TimeCategory.PlayerMovement));

            IFSMState respawn = NewState(nameof(CharacterState_TriggerRespawn), "{}");
            Assert.That(Get<float>(respawn, "m_secondsInvulnerableAfterOutOfBounds"), Is.EqualTo(0f));
            Assert.That(Get<bool>(respawn, "m_doFullscreenTransition"), Is.True, "the screen transition is on unless the node says otherwise");
            Assert.That(Get<UIContainerIdentifier>(respawn, "m_transitionOverrideContainer"), Is.Null);
            respawn = NewState(nameof(CharacterState_TriggerRespawn),
                "{\"SecondsInvulnerableAfterOutOfBounds\":2.5,\"DoFullscreenTransition\":false,\"TransitionOverrideContainer\":\"\"}");
            Assert.That(Get<float>(respawn, "m_secondsInvulnerableAfterOutOfBounds"), Is.EqualTo(2.5f));
            Assert.That(Get<bool>(respawn, "m_doFullscreenTransition"), Is.False);
            Assert.That(Get<UIContainerIdentifier>(respawn, "m_transitionOverrideContainer"), Is.Null);
        }

        // 0x77FD50 and 0x7808C0 pass the plain ability arguments to the rail and ability state bases, and the factory
        // builds each through its ConstructInstance. The ability types are deliberately unrelated to the states, to show
        // the value comes from the JSON.
        [Test]
        public void TransporterStatesReadTheirAbilityTypeFromJson()
        {
            foreach ((Type type, ActorAbilityType ability) in new[]
            {
                (typeof(CharacterState_TransporterHomingProximity), ActorAbilityType.Character_Jump),
                (typeof(CharacterState_TransporterMovement), ActorAbilityType.Character_MovementAir),
            })
            {
                IFSMState state = NewState(type.Name, AbilityJson(ability));
                Assert.That(state.GetType(), Is.EqualTo(type));
                Assert.That(typeof(ActorState_Ability).GetProperty("m_abilityType", Instance).GetValue(state), Is.EqualTo(ability), type.Name);
            }
        }

        // ---- Declaration shape ----

        private const string Il2CppNull = "Il2CppSetOption:1";
        private const string Il2CppBounds = "Il2CppSetOption:2";

        [Test]
        public void TriggerStatesKeepTheOriginalShape()
        {
            Type trigger = typeof(FSMState_Trigger);
            (string, int)[] triggerStart = { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("InvokeTriggerStart", 0x00c4) };
            AssertShape(typeof(CharacterState_TriggerGravity), 0x00100001, trigger,
                new[] { Il2CppNull, "GraphNodeMenuFormat", "GraphNodeDefaultName", Il2CppBounds },
                new[] { ("m_gravityEnabled", 0x0021) }, triggerStart);
            AssertShape(typeof(CharacterState_TriggerClearHomingTarget), 0x00100001, trigger,
                new[] { Il2CppNull, "GraphNodeMenuFormat", "GraphNodeDefaultName", Il2CppBounds },
                new[] { ("m_startGracePeriod", 0x0021) }, triggerStart);
            AssertShape(typeof(CharacterState_TriggerGamePlayControl), 0x00100001, trigger,
                new[] { "GraphNodeMenuFormat", "GraphNodeDefaultName", Il2CppBounds, Il2CppNull },
                new[] { ("m_controlLockTime", 0x0021), ("m_timeCategory", 0x0021) },
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("InvokeTriggerStart", 0x00c4), ("ControlLock", 0x0081) });
            AssertShape(typeof(CharacterState_TriggerSetCameraProxySettings), 0x00100001, trigger,
                new[] { Il2CppNull, Il2CppBounds, "GraphNodeDefaultName", "GraphNodeMenuFormat" },
                new[] { ("m_targetSettingsData", 0x0021) }, triggerStart);
            AssertShape(typeof(CharacterState_TriggerStickyControls), 0x00100001, trigger,
                new[] { Il2CppBounds, "GraphNodeDefaultName", "GraphNodeMenuFormat", Il2CppNull },
                new[] { ("m_stickyControlsDefinition", 0x0021) },
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("InvokeTriggerStart", 0x00c4), ("InvokeTriggerEnd", 0x00c4) });
        }

        [Test]
        public void EffectAndAbilityStatesKeepTheOriginalShape()
        {
            AssertShape(typeof(CharacterState_TriggerRespawn), 0x00100001, typeof(ActorState_Effect),
                new[] { Il2CppNull, Il2CppBounds, "GraphNodeDefaultName", "GraphNodeMenuFormat" },
                new[] { ("m_secondsInvulnerableAfterOutOfBounds", 0x0021), ("m_transitionOverrideContainer", 0x0021), ("m_doFullscreenTransition", 0x0021) },
                new[]
                {
                    (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00c4), ("DoOnLeave", 0x00c4), ("DoUpdate", 0x00c4),
                    ("TriggerRespawn", 0x0081), ("SetupRespawn", 0x0091), ("FinishRespawn", 0x0091), ("RespawnCharacter", 0x0091), ("SnapCameras", 0x0091),
                });
            AssertShape(typeof(CharacterState_ZeroVelocity), 0x00100001, typeof(ActorState_Effect),
                new[] { "GraphNodeMenuFormat", "GraphNodeDefaultName", Il2CppNull, Il2CppBounds },
                new[] { ("m_onUpdate", 0x0001) },
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00c4), ("DoUpdate", 0x00c4) });
            AssertShape(typeof(CharacterState_TransporterHomingProximity), 0x00100001, typeof(CharacterState_Rail<>),
                new[] { "GraphNodeMenuFormat", "GraphNodeDefaultName", Il2CppBounds, Il2CppNull },
                new (string, int)[0],
                new[] { (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00c4), ("DoUpdate", 0x00c4), ("DoOnLeave", 0x00c4) });
            AssertShape(typeof(CharacterState_TransporterMovement), 0x00100001, typeof(CharacterState_Ability<>),
                new[] { Il2CppBounds, Il2CppNull, "GraphNodeMenuFormat", "GraphNodeDefaultName" },
                new (string, int)[0],
                new[]
                {
                    (".ctor", 0x1884), ("ConstructInstance", 0x0096), ("DoOnEnter", 0x00c4), ("OnSurfaceChanged", 0x0081), ("DoUpdate", 0x00c4),
                    ("DoOnLeave", 0x00c4), ("UpdateVelocity", 0x0081), ("OrientateToTracker", 0x0081), ("OrientateOnExit", 0x0081),
                    ("OverrideProxy", 0x0081), ("ResetProxy", 0x0081),
                });
            Assert.That(typeof(CharacterState_TransporterHomingProximity).BaseType, Is.EqualTo(typeof(CharacterState_Rail<CharacterAbility_TransporterProximity>)));
            Assert.That(typeof(CharacterState_TransporterMovement).BaseType, Is.EqualTo(typeof(CharacterState_Ability<CharacterAbility_MovementTransporter>)));
        }

        // The argument classes keep their own names; Serializable is the type flag 0x2000 (so the Clear homing target
        // arguments, which lack it, have 0x00100004), and only the respawn arguments carry the factory attribute.
        [Test]
        public void ArgumentClassesKeepTheOriginalShape()
        {
            Type triggerArgs = Nested(typeof(FSMState_Trigger), "TriggerJSONCtorArgs");
            Type baseArgs = Nested(typeof(ActorState), "BaseJSONCtorArgs");
            (string, int)[] constructor = { (".ctor", 0x1886) };
            AssertShape(Nested(typeof(CharacterState_TriggerGravity), "JSONCtorArgs"), 0x00102004, triggerArgs,
                new[] { Il2CppBounds, Il2CppNull }, new[] { ("GravityEnabled", 0x0006) }, constructor);
            AssertShape(Nested(typeof(CharacterState_TriggerClearHomingTarget), "JSONCtorArgs"), 0x00100004, triggerArgs,
                new[] { Il2CppBounds, Il2CppNull }, new[] { ("StartGracePeriod", 0x0006) }, constructor);
            AssertShape(Nested(typeof(CharacterState_TriggerGamePlayControl), "JSONCtorArgs"), 0x00102004, triggerArgs,
                new[] { Il2CppNull, Il2CppBounds }, new[] { ("ControlLockTime", 0x0006), ("TimeCategory", 0x0006) }, constructor);
            AssertShape(Nested(typeof(CharacterState_TriggerSetCameraProxySettings), "JSONCtorArgs"), 0x00102004, triggerArgs,
                new[] { Il2CppBounds, Il2CppNull }, new[] { ("TargetSettingsData", 0x0006) }, constructor);
            AssertShape(Nested(typeof(CharacterState_TriggerStickyControls), "JSONCtorArgs"), 0x00102004, triggerArgs,
                new[] { Il2CppBounds, Il2CppNull }, new[] { ("StickyControls", 0x0006) }, constructor);
            AssertShape(Nested(typeof(CharacterState_ZeroVelocity), "JSONCtorArgs"), 0x00102004, baseArgs,
                new[] { Il2CppBounds, Il2CppNull }, new[] { ("MaintainZeroVelocityInUpdate", 0x0006) }, constructor);
            AssertShape(Nested(typeof(CharacterState_TriggerRespawn), "RespawnJSONCtorArgs"), 0x00102004, baseArgs,
                new[] { Il2CppNull, Il2CppBounds, "FiniteStateMachineJsonCtorArgs" },
                new[] { ("SecondsInvulnerableAfterOutOfBounds", 0x0006), ("TransitionOverrideContainer", 0x0006), ("DoFullscreenTransition", 0x0006) },
                constructor);
        }

        [Test]
        public void AbilityAndDefinitionKeepTheOriginalShape()
        {
            AssertShape(typeof(CharacterAbility_TransporterProximity), 0x00100001, typeof(CharacterAbility_TargetingHoming<>),
                new[] { Il2CppNull, Il2CppBounds }, new (string, int)[0],
                new[] { ("DoOnUpdate", 0x00c4), ("ProximityCheckToTargetedObject", 0x0081), (".ctor", 0x1886) });
            Assert.That(typeof(CharacterAbility_TransporterProximity).BaseType, Is.EqualTo(typeof(CharacterAbility_TargetingHoming<CharacterAbilityDefinition_TransporterProximity>)));
            Assert.That(typeof(CharacterAbility_TransporterProximity).GetInterfaces().Select(i => i.Name), Does.Contain("IAbilityTrigger"));
            AssertShape(typeof(CharacterAbilityDefinition_TransporterProximity), 0x00100001, typeof(CharacterAbilityDefinition_RailsHoming),
                new[] { "CreateAssetMenu", Il2CppBounds, Il2CppNull }, new (string, int)[0],
                new[] { ("get_ScriptType", 0x08c6), (".ctor", 0x1886) });
        }

        [Test]
        public void StatesKeepTheirGraphNodeNames()
        {
            foreach ((Type type, string name, string menu) in new[]
            {
                (typeof(CharacterState_TriggerGravity), "TriggerGravity", "Character/Trigger/{0}"),
                (typeof(CharacterState_TriggerClearHomingTarget), "TriggerClearHomingTarget", "Character/Trigger/{0}"),
                (typeof(CharacterState_TriggerGamePlayControl), "TriggerGamePlayControl", "Character/Trigger/{0}"),
                (typeof(CharacterState_TriggerRespawn), "TriggerRespawn", "Character/Trigger/{0}"),
                (typeof(CharacterState_TriggerSetCameraProxySettings), "TriggerSetCameraProxySettings", "Character/Trigger/{0}"),
                (typeof(CharacterState_TriggerStickyControls), "TriggerStickyControls", "Character/Trigger/{0}"),
                (typeof(CharacterState_ZeroVelocity), "ZeroVelocity", "Character/{0}"),
                (typeof(CharacterState_TransporterHomingProximity), "TransporterHomingProximity", "Character/{0}"),
                (typeof(CharacterState_TransporterMovement), "TransporterMovement", "Character/{0}"),
            })
            {
                Assert.That(type.GetCustomAttributes(typeof(GraphNodeDefaultNameAttribute), false).Cast<GraphNodeDefaultNameAttribute>().Single().DefaultName, Is.EqualTo(name), type.Name);
                Assert.That(type.GetCustomAttributes(typeof(GraphNodeMenuFormatAttribute), false).Cast<GraphNodeMenuFormatAttribute>().Single().Format, Is.EqualTo(menu), type.Name);
            }
        }

        // The data fields name the asset type the graph editor offers (0x04000cd* field attributes of the metadata).
        [Test]
        public void ArgumentFieldsKeepTheirGraphObjectTypes()
        {
            foreach ((Type owner, string field, Type asset) in new[]
            {
                (Nested(typeof(CharacterState_TriggerSetCameraProxySettings), "JSONCtorArgs"), "TargetSettingsData", typeof(CameraProxyTargetSettingsData)),
                (Nested(typeof(CharacterState_TriggerStickyControls), "JSONCtorArgs"), "StickyControls", typeof(CharacterStickyControlsDefinition)),
                (Nested(typeof(CharacterState_TriggerRespawn), "RespawnJSONCtorArgs"), "TransitionOverrideContainer", typeof(UIContainerIdentifier)),
            })
            {
                var attributes = owner.GetField(field).GetCustomAttributes(typeof(GraphUnityObjectAttribute), false).Cast<GraphUnityObjectAttribute>().ToArray();
                Assert.That(attributes, Has.Length.EqualTo(1), field);
                Assert.That(attributes[0].UnityObjectType, Is.EqualTo(asset), field);
                Assert.That(owner.GetField(field).FieldType, Is.EqualTo(typeof(string)), field);
            }
        }

        // ---- Definition ----

        // 0x545290 returns the ability script type; the definition adds no field of its own, so the rails homing
        // defaults (HomingRotationalVelocity 180 at 0x544BA4, HomingDistance one) stand.
        [Test]
        public void DefinitionNamesItsAbilityScriptAndKeepsTheRailsHomingDefaults()
        {
            Owned.Run(owned =>
            {
                var definition = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityDefinition_TransporterProximity>());
                Assert.That(definition.ScriptType, Is.EqualTo(typeof(CharacterAbility_TransporterProximity)));
                Assert.That(definition.HomingRotationalVelocity, Is.EqualTo(180f));
                Assert.That(definition.HomingDistance, Is.EqualTo(1f));
                var menu = (CreateAssetMenuAttribute)typeof(CharacterAbilityDefinition_TransporterProximity).GetCustomAttributes(typeof(CreateAssetMenuAttribute), false).Single();
                Assert.That(menu.fileName, Is.EqualTo("CharacterAbilityDefinition_TransporterProximity"));
                Assert.That(menu.menuName, Is.EqualTo("HardlightProject/DefinitionData/AbilityDef/TransporterProximity"));
                Assert.That(menu.order, Is.EqualTo(1));
            });
        }

        // ---- Ability ----

        // 0x754894 returns false for exactly zero (ucomiss with zero, jne/jnp) before anything else is read; any other
        // distance, negative included, reads the track manager and then the stored auto attach surface (0x7548AD..0x754935).
        // The stored read inserts a default, so it is visible in the storage. An unstarted track manager has no transporters
        // octree: TryGetTransporterHit loads it (0x6DFD63) and TryGetSurfaceHit calls GetColliding on it (0x6DF7F4) with null
        // checks disabled, so the search that follows the read faults with a NullReferenceException.
        [Test]
        public void ProximityCheckReadsTheStoredSurfaceOnlyForANonZeroDistance()
        {
            WithTransporterAbility((owned, character, ability, definition) =>
            {
                TrackManager tracks = owned.Host("Lucid proximity tracks", false).AddComponent<TrackManager>();
                Set(character, "m_trackManagerRef", new SystemRef<TrackManager>("TrackManager", tracks));
                Assert.That((bool)Call(ability, "ProximityCheckToTargetedObject", 0f), Is.False);
                Assert.That(character.Storage.HasValue(ActorFSMKeys.TransporterAutoAttach), Is.False, "zero distance returns first");
                foreach (float distance in new[] { 1f, -1f })
                {
                    character.Storage.RemoveValue<ISurface>(ActorFSMKeys.TransporterAutoAttach);
                    Assert.Throws<NullReferenceException>(() => Call(ability, "ProximityCheckToTargetedObject", distance), "distance " + distance);
                    Assert.That(character.Storage.HasValue(ActorFSMKeys.TransporterAutoAttach), Is.True, "distance " + distance);
                }
            });
        }

        // 0x7548CF: a missing track manager (UnityEngine.Object.op_Equality) returns false before the storage is read.
        [Test]
        public void ProximityCheckWithoutATrackManagerReturnsBeforeReadingTheStorage()
        {
            WithTransporterAbility((owned, character, ability, definition) =>
            {
                Assert.That((bool)Call(ability, "ProximityCheckToTargetedObject", 1f), Is.False);
                Assert.That(character.Storage.HasValue(ActorFSMKeys.TransporterAutoAttach), Is.False);
            });
        }

        // 0x754B52: a stored auto attach surface skips the HomingDistanceSqr test (test r14,r14; jne 0x754BBA), so it is
        // trusted at any distance (HomingDistanceSqr stays zero here, the surface is fifty units away). The surface must be
        // a Component (isinst at 0x754B10); its game object becomes the target, typed Transporter (0xd2ca8fc3, 0x754C0E).
        [Test]
        public void ProximityCheckTrustsAStoredSurfaceAtAnyDistance()
        {
            WithTransporterAbility((owned, character, ability, definition) =>
            {
                Set(character, "m_trackManagerRef", new SystemRef<TrackManager>("TrackManager", owned.Host("Lucid proximity tracks", false).AddComponent<TrackManager>()));
                var surface = owned.Host("Lucid auto attach surface", false).AddComponent<FixedNearestSurfaceProbe>();
                surface.NearestPosition = new Vector3(0f, 0f, 50f);
                character.Storage.SetValue<ISurface>(ActorFSMKeys.TransporterAutoAttach, surface);
                Assert.That(definition.HomingDistanceSqr, Is.EqualTo(0f));

                Assert.That((bool)Call(ability, "ProximityCheckToTargetedObject", 1f), Is.True);
                var target = Get<CharacterTargetObjectData>(ability, "m_targetedObject");
                Assert.That(target.Target, Is.SameAs(surface.gameObject));
                Assert.That(target.Type, Is.EqualTo(HomingTargetType.Transporter));
                Assert.That(target.WorldPosition, Is.EqualTo(new Vector3(0f, 0f, 50f)));
            });
        }

        // 0x754B0E -> 0x754B3A: a stored surface that locates no surface of its own fails the Component test, and no target is set.
        [Test]
        public void ProximityCheckFailsWhenTheStoredSurfaceLocatesNoComponent()
        {
            WithTransporterAbility((owned, character, ability, definition) =>
            {
                Set(character, "m_trackManagerRef", new SystemRef<TrackManager>("TrackManager", owned.Host("Lucid proximity tracks", false).AddComponent<TrackManager>()));
                var surface = owned.Host("Lucid auto attach surface", false).AddComponent<FixedNearestSurfaceProbe>();
                surface.ReturnsItself = false;
                surface.NearestPosition = new Vector3(0f, 0f, 50f);
                character.Storage.SetValue<ISurface>(ActorFSMKeys.TransporterAutoAttach, surface);

                Assert.That((bool)Call(ability, "ProximityCheckToTargetedObject", 1f), Is.False);
                Assert.That(Get<CharacterTargetObjectData>(ability, "m_targetedObject").Target, Is.Null);
            });
        }

        private static InertSpline StoreSurface(Owned owned, Character character)
        {
            InertSpline surface = owned.Host("Lucid proximity surface", false).AddComponent<InertSpline>();
            character.Storage.SetValue<ISurface>(ActorFSMKeys.TransporterAutoAttach, surface);
            return surface;
        }

        private static void Update(CharacterAbility_TransporterProximity ability) => Call(ability, "DoOnUpdate", null, 0.02f);

        // 0x75460A: an already triggered ability returns after the targeting base update, without reading the stored surface.
        [Test]
        public void UpdateLeavesATriggeredAbilityAlone()
        {
            WithTransporterAbility((owned, character, ability, definition) =>
            {
                SuppressTargetingRefresh(ability, definition);
                StoreSurface(owned, character);
                ability.IsTriggered = true;
                Update(ability);
                Assert.That(ability.IsTriggered, Is.True);
                Assert.That(Get<bool>(ability, "m_targetRequiresRefresh"), Is.False, "the stored surface was not read");
            });
        }

        // 0x754625..0x7546F4: with the refresh flag down the stored surface is read, and the flag becomes whether one is
        // stored (setne); with the flag up the read is skipped (jne 0x754701), so the flag stays up even with nothing stored.
        // The proximity check then fails without a track manager, so the ability never triggers.
        [Test]
        public void UpdateTurnsTheRefreshFlagIntoWhetherASurfaceIsStored()
        {
            WithTransporterAbility((owned, character, ability, definition) =>
            {
                // The targeting interval cannot elapse, so the update leaves the refresh flag to the ability alone.
                SuppressTargetingRefresh(ability, definition);
                Update(ability);
                Assert.That(Get<bool>(ability, "m_targetRequiresRefresh"), Is.False, "nothing stored");
                StoreSurface(owned, character);
                Update(ability);
                Assert.That(Get<bool>(ability, "m_targetRequiresRefresh"), Is.True, "a surface is stored");
                Assert.That(ability.IsTriggered, Is.False, "without a track manager the check fails");
            });
        }

        [Test]
        public void UpdateSkipsTheStoredSurfaceReadWhileTheRefreshFlagIsUp()
        {
            WithTransporterAbility((owned, character, ability, definition) =>
            {
                // Not suppressed: the first update starts the targeting interval, which raises the flag (0xE69233) before the
                // ability looks at the stored surface.
                Update(ability);
                Assert.That(Get<bool>(ability, "m_targetRequiresRefresh"), Is.True);
                Assert.That(character.Storage.HasValue(ActorFSMKeys.TransporterAutoAttach), Is.False, "the stored surface was not read");
            });
        }

        // ---- Trigger states ----

        // 0x783420: the earlier handle is removed from the character and the storage first (0x7835E1..0x7836FA), then a
        // gravity disabling override (modifier 0x64bd855e, value false) is added and its handle stored (0x783739..0x78387B)
        // unless gravity is to stay on (cmp byte [+0x28], 0 at 0x78371D).
        [Test]
        public void GravityStateDisablesGravityThroughAStoredHandle()
        {
            WithStateCharacter((owned, character) =>
            {
                Assert.That(character.GetModifierValue<bool>((int)GameplayModifierType.ApplyGravity), Is.True);
                Call(NewState(nameof(CharacterState_TriggerGravity), "{\"GravityEnabled\":false}"), "InvokeTriggerStart", character, null);
                Assert.That(character.GetModifierValue<bool>((int)GameplayModifierType.ApplyGravity), Is.False);
                Assert.That(character.Storage.GetValue<StackableDataHandle>(ActorFSMKeys.GravityDisabledModifierHandle, null, false), Is.Not.Null);
            });
        }

        [Test]
        public void GravityStateLeavingGravityOnAddsNothing()
        {
            WithStateCharacter((owned, character) =>
            {
                Call(NewState(nameof(CharacterState_TriggerGravity), "{}"), "InvokeTriggerStart", character, null);
                Assert.That(character.GetModifierValue<bool>((int)GameplayModifierType.ApplyGravity), Is.True);
                Assert.That(character.Storage.GetValue<StackableDataHandle>(ActorFSMKeys.GravityDisabledModifierHandle, null, false), Is.Null);
            });
        }

        [Test]
        public void GravityStateEnablingRemovesTheEarlierDisablingOverride()
        {
            WithStateCharacter((owned, character) =>
            {
                Call(NewState(nameof(CharacterState_TriggerGravity), "{\"GravityEnabled\":false}"), "InvokeTriggerStart", character, null);
                Call(NewState(nameof(CharacterState_TriggerGravity), "{\"GravityEnabled\":true}"), "InvokeTriggerStart", character, null);
                Assert.That(character.GetModifierValue<bool>((int)GameplayModifierType.ApplyGravity), Is.True);
                Assert.That(character.Storage.HasValue(ActorFSMKeys.GravityDisabledModifierHandle), Is.False, "the handle is removed from the storage");
            });
        }

        // A second disabling state replaces the first one's override instead of stacking a second: one enabling state
        // afterwards restores gravity.
        [Test]
        public void GravityStateReplacesItsEarlierOverrideInsteadOfStackingIt()
        {
            WithStateCharacter((owned, character) =>
            {
                IFSMState disable = NewState(nameof(CharacterState_TriggerGravity), "{\"GravityEnabled\":false}");
                Call(disable, "InvokeTriggerStart", character, null);
                StackableDataHandle first = character.Storage.GetValue<StackableDataHandle>(ActorFSMKeys.GravityDisabledModifierHandle, null, false);
                Call(disable, "InvokeTriggerStart", character, null);
                StackableDataHandle second = character.Storage.GetValue<StackableDataHandle>(ActorFSMKeys.GravityDisabledModifierHandle, null, false);
                Assert.That(second, Is.Not.SameAs(first));
                Call(NewState(nameof(CharacterState_TriggerGravity), "{}"), "InvokeTriggerStart", character, null);
                Assert.That(character.GetModifierValue<bool>((int)GameplayModifierType.ApplyGravity), Is.True);
            });
        }

        private static GlobalConstantDefinition_Character GraceConstants(Owned owned, Character character)
        {
            var constants = owned.Track(ScriptableObject.CreateInstance<GlobalConstantDefinition_Character>());
            constants.HomingTargetGracePeriodSeconds = 2.5f;
            SetProperty(character, "Constants", constants);
            return constants;
        }

        // 0x782350 hands the node's flag to CharacterHomingPool.ClearTarget: without it the grace period ends (timer zero),
        // the active target is dropped and its ability is no longer triggered.
        [Test]
        public void ClearHomingTargetEndsTheGracePeriodAndReleasesTheActiveTarget()
        {
            WithTransporterAbility((owned, character, ability, definition) =>
            {
                GraceConstants(owned, character);
                CharacterHomingPool pool = character.HomingPool;
                ability.IsTriggered = true;
                pool.ClearTarget(true);
                Assert.That(Get<float>(pool, "m_targetGracePeriodTimer"), Is.EqualTo(2.5f), "precondition: the grace period runs");
                pool.SetTargetImmediately(ability, new CharacterTargetObjectData(), Vector3.zero, 5f);
                Assert.That(pool.IsTargetActive(), Is.True);
                Call(NewState(nameof(CharacterState_TriggerClearHomingTarget), "{}"), "InvokeTriggerStart", character, null);
                Assert.That(Get<float>(pool, "m_targetGracePeriodTimer"), Is.EqualTo(0f));
                Assert.That(pool.IsTargetActive(), Is.False);
                Assert.That(ability.IsTriggered, Is.False);
            });
        }

        [Test]
        public void ClearHomingTargetCanStartTheGracePeriodInstead()
        {
            WithTransporterAbility((owned, character, ability, definition) =>
            {
                GraceConstants(owned, character);
                CharacterHomingPool pool = character.HomingPool;
                pool.SetTargetImmediately(ability, new CharacterTargetObjectData(), Vector3.zero, 5f);
                Call(NewState(nameof(CharacterState_TriggerClearHomingTarget), "{\"StartGracePeriod\":true}"), "InvokeTriggerStart", character, null);
                Assert.That(Get<float>(pool, "m_targetGracePeriodTimer"), Is.EqualTo(2.5f));
                Assert.That(pool.IsTargetActive(), Is.False);
            });
        }

        // 0x783140 and its iterator 0x783210: the controls are disabled (modifier 0xc22df46d, false) when the iterator first
        // runs, it then yields a fixed seconds wait of the node's lock time and category (0x7832AC..0x7832BA), and on the
        // next step the override is removed and the controls come back (0x783266).
        [Test]
        public void ControlLockDisablesTheControlsForTheLockTimeAndThenRestoresThem()
        {
            WithStateCharacter((owned, character) =>
            {
                IFSMState state = NewState(nameof(CharacterState_TriggerGamePlayControl),
                    "{\"ControlLockTime\":1.5,\"TimeCategory\":" + (int)TimeCategory.PlayerMovement + "}");
                var steps = (IEnumerator)Call(state, "ControlLock", character);
                Assert.That(character.GetModifierValue<bool>((int)GameplayModifierType.ControlsEnabled), Is.True, "nothing runs before the first step");
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(character.GetModifierValue<bool>((int)GameplayModifierType.ControlsEnabled), Is.False);
                (float seconds, object category) = WaitArguments(steps.Current);
                Assert.That(seconds, Is.EqualTo(1.5f));
                Assert.That(category, Is.EqualTo(TimeCategory.PlayerMovement));
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(character.GetModifierValue<bool>((int)GameplayModifierType.ControlsEnabled), Is.True);
            });
        }

        // 0x7868C5 and 0x786998: both the enter and the update call the effect base first, then set the velocity to
        // Vector3.zero; the update does so only when the node asked for it (cmp byte [+0x38], 0 at 0x78697F).
        [Test]
        public void ZeroVelocityStopsTheCharacterOnEnterAndOnUpdateOnlyWhenAsked()
        {
            WithStateCharacter((owned, character) =>
            {
                PrimeEffectStorage(character);
                character.SetWorldVelocity(new Vector3(3f, 4f, 5f));
                Call(NewState(nameof(CharacterState_ZeroVelocity), "{}"), "DoOnEnter", character, null);
                Assert.That(character.WorldVelocity, Is.EqualTo(Vector3.zero));

                character.SetWorldVelocity(new Vector3(1f, 2f, 3f));
                Call(NewState(nameof(CharacterState_ZeroVelocity), "{}"), "DoUpdate", character, Context());
                Assert.That(character.WorldVelocity, Is.EqualTo(new Vector3(1f, 2f, 3f)), "the update leaves the velocity unless asked");
                Call(NewState(nameof(CharacterState_ZeroVelocity), "{\"MaintainZeroVelocityInUpdate\":true}"), "DoUpdate", character, Context());
                Assert.That(character.WorldVelocity, Is.EqualTo(Vector3.zero));
            });
        }

        // 0x784040: after the effect base the retained velocity flag is removed from the character's storage.
        [Test]
        public void RespawnLeavingRemovesTheRetainedVelocityFlag()
        {
            WithStateCharacter((owned, character) =>
            {
                PrimeEffectStorage(character);
                character.Storage.SetValue(ActorFSMKeys.RespawnRetainVelocity, true);
                Call(NewState(nameof(CharacterState_TriggerRespawn), "{}"), "DoOnLeave", character, null);
                Assert.That(character.Storage.HasValue(ActorFSMKeys.RespawnRetainVelocity), Is.False);
            });
        }

        // 0x7847A0 marks the respawn effects as complete.
        [Test]
        public void RespawnFinishMarksTheEffectsComplete()
        {
            WithStateCharacter((owned, character) =>
            {
                typeof(CharacterState_TriggerRespawn).GetMethod("FinishRespawn", AnyStatic).Invoke(null, new object[] { character });
                Assert.That(character.Storage.GetValue(ActorFSMKeys.RespawnEffectsComplete, false, false), Is.True);
            });
        }

        // 0x7841E0..0x78429E reads the teleport flag with the store-default argument set, so the update without a pending
        // teleport leaves a stored false; it performs no teleport.
        [Test]
        public void RespawnUpdateWithoutAPendingTeleportOnlyReadsTheFlag()
        {
            WithStateCharacter((owned, character) =>
            {
                PrimeEffectStorage(character);
                Call(NewState(nameof(CharacterState_TriggerRespawn), "{}"), "DoUpdate", character, Context());
                Assert.That(character.Storage.HasValue(ActorFSMKeys.RespawnTeleportRequired), Is.True);
                Assert.That(character.Storage.GetValue(ActorFSMKeys.RespawnTeleportRequired, true, false), Is.False);
            });
        }

        // 0x784960: the static iterator waits two fixed updates before it looks for the camera managers; with neither
        // registered it then ends.
        [Test]
        public void SnapCamerasWaitsTwoFixedUpdatesAndEndsWithoutCameraManagers()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CameraYAxisProxyManager>();
                owned.LeaseRow<CinemachineCameraManager>();
                var steps = (IEnumerator)typeof(CharacterState_TriggerRespawn).GetMethod("SnapCameras", AnyStatic).Invoke(null, null);
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(steps.Current, Is.TypeOf<WaitForFixedUpdate>());
                Assert.That(steps.MoveNext(), Is.True);
                Assert.That(steps.Current, Is.TypeOf<WaitForFixedUpdate>());
                Assert.That(steps.MoveNext(), Is.False);
            });
        }

        // 0x77D5A6 in the sibling dash is the same shape: 0x7817B0 makes the visual proxy follow the character's rotation
        // followed by a quarter turn about right (AngleAxis 90 about Vector3.right, character rotation first), and 0x781C10
        // releases it. A character turned 90 degrees about up gives a proxy whose forward is down and whose up is the
        // character's right; the swapped product would give a different pose.
        [Test]
        public void MovementTipsTheProxyAQuarterTurnAndLaterReleasesIt()
        {
            WithStateCharacter((owned, character) =>
            {
                var proxy = owned.Host("Lucid transporter proxy").AddComponent<SmoothedTransformProxy>();
                Call(proxy, "Awake"); // Edit Mode does not run Awake, which caches the transform the proxy rotates
                SetActorProperty(character, "VisualProxy", proxy); // Reflection: Character.Awake assigns it from the component lookup, which Edit Mode does not run
                character.SetWorldRotation(Quaternion.Euler(0f, 90f, 0f));
                IFSMState state = NewState(nameof(CharacterState_TransporterMovement), AbilityJson(ActorAbilityType.Character_MovementAir));

                Call(state, "OverrideProxy", character);
                Quaternion rotation = proxy.transform.rotation;
                Assert.That(Get<bool>(proxy, "m_rotationOverriden"), Is.True);
                Assert.That(Vector3.Distance(rotation * Vector3.forward, new Vector3(0f, -1f, 0f)), Is.LessThan(1e-5f));
                Assert.That(Vector3.Distance(rotation * Vector3.up, new Vector3(1f, 0f, 0f)), Is.LessThan(1e-5f));

                Call(state, "ResetProxy", character);
                Assert.That(Get<bool>(proxy, "m_rotationOverriden"), Is.False);
                Assert.That(proxy.transform.rotation, Is.EqualTo(rotation), "the pose is left as it was");
            });
        }

        // ---- Call order witnesses (emission-only: separate from the behaviour oracles above) ----

        // 0x780D94..0x780DD0: the transporter exists check on the new surface precedes the ability notification.
        [Test]
        public void CallOrderMovementSurfaceChangeChecksTheTransporterBeforeInformingTheAbility()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_TransporterMovement), "OnSurfaceChanged");
            int exists = IndexOf(calls, "TrackManager", "TrackableTransporterExists");
            IndexOf(calls, "CharacterAbility_MovementTracker", "OnSurfaceChanged", exists + 1);
        }

        // 0x7810F9..0x7812DB: velocity, orientation, proxy override, then the vertical velocity and the tracker update.
        [Test]
        public void CallOrderMovementUpdatesVelocityThenOrientationThenTheTracker()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_TransporterMovement), "DoUpdate");
            int velocity = IndexOf(calls, "CharacterState_TransporterMovement", "UpdateVelocity");
            int orientate = IndexOf(calls, "CharacterState_TransporterMovement", "OrientateToTracker", velocity + 1);
            int proxy = IndexOf(calls, "CharacterState_TransporterMovement", "OverrideProxy", orientate + 1);
            int vertical = IndexOf(calls, "CharacterTracker", "ApplyVerticalVelocity", proxy + 1);
            IndexOf(calls, "CharacterTracker", "UpdateOnTrackerSpline", vertical + 1);
        }

        // 0x7819B9..0x781C05.
        [Test]
        public void CallOrderMovementLeavesByDetachingThenStandingUpright()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_TransporterMovement), "DoOnLeave");
            int velocity = IndexOf(calls, "CharacterState_TransporterMovement", "UpdateVelocity");
            int lockTime = IndexOf(calls, "Character", "SetAirControlsLockTime", velocity + 1);
            int surface = IndexOf(calls, "CharacterAbility_MovementTracker", "OnSurfaceChanged", lockTime + 1);
            int tracking = IndexOf(calls, "Character", "DetectTracking", surface + 1);
            int proxy = IndexOf(calls, "CharacterState_TransporterMovement", "ResetProxy", tracking + 1);
            IndexOf(calls, "CharacterState_TransporterMovement", "OrientateOnExit", proxy + 1);
        }

        // 0x7806D8..0x78089A: a held target places the character, restores its velocity and teleports the tracker before
        // the target is cleared. The placement and velocity are the Actor virtual slots 35 and 37 (0x780709, 0x780728),
        // so the IL names their Actor declarations.
        [Test]
        public void CallOrderHomingLeavePlacesTheCharacterOnTheTargetBeforeClearingIt()
        {
            List<MethodBase> calls = Calls(typeof(CharacterState_TransporterHomingProximity), "DoOnLeave");
            int position = IndexOf(calls, "Actor", "SetWorldPosition");
            int velocity = IndexOf(calls, "Actor", "SetWorldVelocity", position + 1);
            int teleport = IndexOf(calls, "CharacterTracker", "OnTeleport", velocity + 1);
            IndexOf(calls, "CharacterHomingPool", "ClearTarget", teleport + 1);
        }
    }
}
