using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.WorldTriggersShared;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.TriggerConditionTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CharacterFootsteps (TypeDef 0x0200032e, x86-64 0x7639C0..0x76417F),
    // CharacterJumpTrackerTrigger (0x020001d1, 0x6F2CD0..0x6F3016), CharacterMagnetismModifier
    // (0x02000743, 0x5CB9B0..0x5CC01D), CharacterPause (0x02000ac2, 0x6E5ED0..0x6E64DF),
    // CharacterProxyAssignTrigger (0x02000340, 0x76A890..0x76A9AA) and CharacterSplineAttachTrigger
    // (0x0200073c, 0x5CAC60..0x5CB0BF). Components are real, on owned hosts that never wake (the
    // Editor sends Reset but no Awake); a CharacterManager is built by its real constructor
    // (OwnedCharacterManagers) and registered into its absent row. A Character has no Rigidbody here,
    // so only the branches that do not move it are run; the rest need a live Character body.
    //
    // Not run in EditMode, with the native oracle for a later PlayMode case:
    // - CharacterPause.Start (0x6E5ED0) needs TimeSetting.GetDefault, which reads the runtime
    //   TimeCategoryConfiguration. A non-empty list takes GetDefault(1) (constant 1.0 at 0x6e5f68) and
    //   sets each listed category to 0 (0x6e5fd6); an empty or null list takes GetDefault(0) (0x6e5fe5)
    //   and sets System (0xc64a29f2) and UnityGlobal (0xa1cd01c4) to 1 (0x6e6008, 0x6e6018).
    //   PauseGame/ResumeGame past their guards need a live Character body.
    // - CharacterJumpTrackerTrigger.FixedUpdate (0x6F2D00): until a jump has started it re-samples the
    //   start position and velocity every tick (0x6f2f21-0x6f2f50); it completes only when IsOnGround
    //   (CharacterTracking slot 5) and not IsJumping (xor al,1 at 0x6f2f93), and only completion stores
    //   m_endPosition.
    // - CharacterMagnetismModifier.OnTriggerStay (0x5CBC90) with forward (0,0,1), right (1,0,0), up
    //   (0,1,0), the character at the trigger + (2,3,5) and constant-1 curves: acceleration (-1,-1,-1),
    //   velocity += acceleration * fixedDeltaTime. The lateral term is negated at 0x5cbe33; forward and
    //   normal are subtracted at 0x5cbed7 and 0x5cbee3 (a positive lateral term would give (1,-1,-1)).
    // - CharacterSplineAttachTrigger.DoTriggerEnter non-legacy branch (0x5CAD58).
    public sealed class OriginalCharacterComponentTests
    {
        // CharacterTrigger and ColliderData require a Collider, so AddComponent fails (returns null) on a bare host.
        private static GameObject NewColliderHost(Owned owned, string name)
        {
            GameObject host = owned.Host(name, false);
            host.AddComponent<BoxCollider>();
            return host;
        }

        // A character with a real FSM storage and the GameplayIslandManager row its constructor needs.
        private static void WithCharacter(Action<Owned, Character, FSMStorage> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<GameplayIslandManager>();
            var storage = new FSMStorage(0);
            body(owned, NewCharacter(owned, storage), storage);
        });

        // A CharacterManager built by its real constructor and registered under its row, with a
        // character that can be made current. The component under test is created by the body, after
        // the registration, because its CharacterManager reference is resolved when it is built.
        private static void WithManager(Action<Owned, CharacterManager, Character> body) => Owned.Run(owned =>
        {
            // Taken first, so it is released last: the manager constructor's rows outlive the case's.
            OwnedCharacterManagers characters = owned.Lease(new OwnedCharacterManagers());
            owned.LeaseRow<GameplayIslandManager>();
            owned.LeaseRow<LevelManager>();
            // An absent row the case registers its manager into, before any component caches a reference.
            CharacterManager manager = owned.Register(characters.NewManager());
            body(owned, manager, NewCharacter(owned, new FSMStorage(0)));
        });

        // CharacterValid has a private setter; the case calls it as the manager's own code does.
        private static void SetCurrent(CharacterManager manager, Character character, bool valid)
        {
            Set(manager, "m_currentCharacter", character);
            typeof(CharacterManager).GetProperty("CharacterValid").GetSetMethod(true).Invoke(manager, new object[] { valid });
        }

        private static string[] HandlerNames(Delegate handlers) =>
            handlers == null ? new string[0] : handlers.GetInvocationList().Select(handler => handler.Method.Name).ToArray();

        private static object[] HandlerTargets(Delegate handlers) =>
            handlers == null ? new object[0] : handlers.GetInvocationList().Select(handler => handler.Target).ToArray();

        private static void AssertType(Type type, int typeFlags, Type baseType, int[] options,
            (string name, int flags)[] fields, (string name, int flags)[] methods)
        {
            AssertMembers(type, typeFlags, fields, methods);
            AssertIl2CppOptions(type, options);
            Assert.That(type.BaseType, Is.EqualTo(baseType), type.FullName);
        }

        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsMethodsAndAttributes()
        {
            AssertType(typeof(CharacterFootsteps), 0x00100001, typeof(ActorFootsteps), new[] { 2, 1 },
                new[] { ("m_terrainEffectsMetadataKeyLookup", 0x0004), ("m_activeSurfaces", 0x0021), ("m_character", 0x0001) },
                new[]
                {
                    ("InitialiseActor", 0x00c6), ("OnCharacterCollisionDataEnter", 0x0081), ("OnCharacterCollisionDataExit", 0x0081),
                    ("RecalculateFootstepAudio", 0x0081), (".ctor", 0x1886)
                });
            Assert.That(AttributeTypes(Field(typeof(CharacterFootsteps), "m_terrainEffectsMetadataKeyLookup")),
                Is.EqualTo(new[] { typeof(SerializeField) }));

            AssertType(typeof(CharacterJumpTrackerTrigger), 0x00100001, typeof(CharacterTrigger), new[] { 2, 1 },
                new[]
                {
                    ("m_character", 0x0001), ("m_hasStartedJump", 0x0001), ("m_jumpComplete", 0x0001), ("m_hasLoggedVelocity", 0x0001),
                    ("m_startPosition", 0x0001), ("m_startVelocity", 0x0001), ("m_endPosition", 0x0001)
                },
                new[] { ("DoTriggerEnter", 0x00c4), ("FixedUpdate", 0x0081), (".ctor", 0x1886) });

            AssertType(typeof(CharacterMagnetismModifier), 0x00100001, typeof(CharacterCollisionModifier), new[] { 2, 1 },
                new[]
                {
                    ("m_magnetismByDistanceForward", 0x0001), ("m_magnetismByDistanceLateral", 0x0001),
                    ("m_magnetismByDistanceOnNormal", 0x0001), ("m_abilityTypeGroup", 0x0001),
                    ("m_worldUp", 0x0001), ("m_worldForward", 0x0001), ("m_worldRight", 0x0001)
                },
                new[] { ("OnTriggerEnter", 0x0081), ("OnTriggerStay", 0x0081), (".ctor", 0x1886) });
            var curveTooltips = new[]
            {
                ("m_magnetismByDistanceForward", "Magnetism acceleration by distance on forward from origin."),
                ("m_magnetismByDistanceLateral", "Magnetism acceleration by distance on lateral from origin."),
                ("m_magnetismByDistanceOnNormal", "Magnetism acceleration by distance on normal from origin.")
            };
            foreach ((string name, string tooltip) in curveTooltips)
            {
                FieldInfo curve = Field(typeof(CharacterMagnetismModifier), name);
                Assert.That(AttributeTypes(curve), Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }), name);
                Assert.That(Tooltip(curve), Is.EqualTo(tooltip), name);
            }
            FieldInfo group = Field(typeof(CharacterMagnetismModifier), "m_abilityTypeGroup");
            Assert.That(AttributeTypes(group), Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(Tooltip(group), Is.EqualTo("Scriptable object for the abilities valid for magnetism to act on the character."));

            AssertType(typeof(CharacterPause), 0x00100001, typeof(MonoBehaviour), new[] { 2, 1 },
                new[]
                {
                    ("m_timeCategories", 0x0001), ("m_characterManagerRef", 0x0001), ("m_pauseTimeSetting", 0x0001),
                    ("m_pauseTimeOverride", 0x0001), ("m_pausePosition", 0x0001), ("m_pauseVelocity", 0x0001)
                },
                new[]
                {
                    ("Start", 0x0081), ("PauseGame", 0x0086), ("ResumeGame", 0x0086), ("RemoveTimeOverride", 0x0081),
                    ("OnDisable", 0x0081), (".ctor", 0x1886)
                });
            FieldInfo categories = Field(typeof(CharacterPause), "m_timeCategories");
            Assert.That(AttributeTypes(categories), Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(Tooltip(categories), Is.EqualTo("The time categories to be paused."));

            AssertType(typeof(CharacterProxyAssignTrigger), 0x00100001, typeof(TransformAssignEventTrigger), new[] { 1, 2 },
                new[] { ("m_characterManagerRef", 0x0021) },
                new[] { ("get_SetNullOnExit", 0x08c4), ("GetTargetTransform", 0x00c4), (".ctor", 0x1886) });

            AssertType(typeof(CharacterSplineAttachTrigger), 0x00100081, typeof(CharacterTrigger), new[] { 1, 2 },
                new[] { ("m_spline", 0x0001), ("m_biDirectional", 0x0001), ("m_useLegacyLogic", 0x0001) },
                new[]
                {
                    ("Awake", 0x00c4), ("DoTriggerEnter", 0x00c4), ("DoTriggerExit", 0x00c4), ("GetFSMKey", 0x05c4),
                    ("ActivateSpline", 0x0086), (".ctor", 0x1884)
                });
            FieldInfo spline = Field(typeof(CharacterSplineAttachTrigger), "m_spline");
            Assert.That(AttributeTypes(spline), Is.EqualTo(new[] { typeof(SerializeField), typeof(TooltipAttribute) }));
            Assert.That(Tooltip(spline), Is.EqualTo("The target spline to attach to."));
            FieldInfo biDirectional = Field(typeof(CharacterSplineAttachTrigger), "m_biDirectional");
            Assert.That(AttributeTypes(biDirectional), Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(Tooltip(biDirectional), Is.EqualTo(
                "If false, only triggers when the incoming direction is within 180 degrees of this transform's forward."));
            FieldInfo legacy = Field(typeof(CharacterSplineAttachTrigger), "m_useLegacyLogic");
            Assert.That(AttributeTypes(legacy), Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(Tooltip(legacy), Is.EqualTo(
                "Flag to use deprecated legacy logic for scenes that have not been corrected to use their orientation."));
        }

        // ---- CharacterJumpTrackerTrigger -------------------------------------------------------

        // DoTriggerEnter 0x6F2CD0 calls the base (an immediate return) and stores the character at +0x50.
        [Test]
        public void JumpTrackerStoresTheEnteringCharacterAndStartsNothing()
        {
            WithCharacter((owned, character, storage) =>
            {
                var trigger = NewColliderHost(owned, "Lucid jump tracker").AddComponent<CharacterJumpTrackerTrigger>();
                Assert.That(Get<Character>(trigger, "m_character"), Is.Null);

                Call(trigger, "DoTriggerEnter", character);

                Assert.That(Get<Character>(trigger, "m_character"), Is.SameAs(character));
                Assert.That(Get<bool>(trigger, "m_hasStartedJump"), Is.False);
                Assert.That(Get<bool>(trigger, "m_jumpComplete"), Is.False);
                Assert.That(Get<bool>(trigger, "m_hasLoggedVelocity"), Is.False);
            });
        }

        // FixedUpdate 0x6F2D00 returns at once for a completed jump (+0x59) or a destroyed or absent
        // character (op_Equality at 0x6f2d72). Past either guard the first step reads IsJumping (from 0x6f2e60),
        // then the position, then the velocity (0x6f2f46), which faults here because this Character has no
        // body.
        [Test]
        public void JumpTrackerFixedUpdateDoesNothingForACompletedJumpOrWithoutACharacter()
        {
            WithCharacter((owned, character, storage) =>
            {
                var trigger = NewColliderHost(owned, "Lucid jump tracker guard").AddComponent<CharacterJumpTrackerTrigger>();

                Call(trigger, "FixedUpdate");
                Assert.That(Get<bool>(trigger, "m_hasStartedJump"), Is.False, "no character");

                Call(trigger, "DoTriggerEnter", character);
                Set(trigger, "m_jumpComplete", true);
                Call(trigger, "FixedUpdate");
                Assert.That(Get<bool>(trigger, "m_hasStartedJump"), Is.False, "a completed jump is not tracked again");
                Assert.That(Get<Vector3>(trigger, "m_startPosition"), Is.EqualTo(Vector3.zero));
                Assert.That(Get<bool>(trigger, "m_hasLoggedVelocity"), Is.False);
            });
        }

        // ---- CharacterFootsteps ----------------------------------------------------------------

        private static void WithFootsteps(Action<Owned, CharacterFootsteps, Character> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<TimeManager>();
            owned.LeaseRow<GameplayIslandManager>();
            using (var time = new OwnedTimeScaledObjects("lucid-character-footsteps"))
            {
                var footsteps = time.Add<CharacterFootsteps>("footsteps");
                body(owned, footsteps, NewCharacter(owned, new FSMStorage(0)));
            }
        });

        // InitialiseActor 0x7639C0 runs the base, then (when the Character changes) removes the enter
        // and exit handlers from the old one and adds them to the new one; stay is never used.
        [Test]
        public void FootstepsFollowTheInitialisedCharactersCollisionEvents()
        {
            WithFootsteps((owned, footsteps, first) =>
            {
                Character second = NewCharacter(owned, new FSMStorage(0));

                footsteps.InitialiseActor(first);
                Assert.That(Get<bool>(footsteps, "m_initialised"), Is.True, "the base ran");
                Assert.That(Get<Actor>(footsteps, "m_actor"), Is.SameAs(first));
                Assert.That(HandlerNames(first.OnCollisionDataEnterEvent), Is.EqualTo(new[] { "OnCharacterCollisionDataEnter" }));
                Assert.That(HandlerNames(first.OnCollisionDataExitEvent), Is.EqualTo(new[] { "OnCharacterCollisionDataExit" }));
                Assert.That(HandlerTargets(first.OnCollisionDataEnterEvent), Is.EqualTo(new object[] { footsteps }));
                Assert.That(first.OnCollisionDataStayEvent, Is.Null);

                footsteps.InitialiseActor(first);
                Assert.That(HandlerNames(first.OnCollisionDataEnterEvent).Length, Is.EqualTo(1), "the same character is not subscribed twice");
                Assert.That(HandlerNames(first.OnCollisionDataExitEvent).Length, Is.EqualTo(1));

                footsteps.InitialiseActor(second);
                Assert.That(HandlerNames(first.OnCollisionDataEnterEvent), Is.Empty);
                Assert.That(HandlerNames(first.OnCollisionDataExitEvent), Is.Empty);
                Assert.That(HandlerNames(second.OnCollisionDataEnterEvent), Is.EqualTo(new[] { "OnCharacterCollisionDataEnter" }));
                Assert.That(HandlerNames(second.OnCollisionDataExitEvent), Is.EqualTo(new[] { "OnCharacterCollisionDataExit" }));

                footsteps.InitialiseActor(null);
                Assert.That(HandlerNames(second.OnCollisionDataEnterEvent), Is.Empty);
                Assert.That(HandlerNames(second.OnCollisionDataExitEvent), Is.Empty);
                Assert.That(Get<Character>(footsteps, "m_character"), Is.Null);
            });
        }

        // RecalculateFootstepAudio 0x763E70 first copies the three defaults over the current audio,
        // climbing audio and particle lookup, then lets the first tracked surface with a terrain
        // effect override them. With no tracked surface only the copy remains.
        [Test]
        public void FootstepsWithoutTrackedSurfacesRestoreTheDefaults()
        {
            WithFootsteps((owned, footsteps, character) =>
            {
                Set(footsteps, "m_terrainEffectsMetadataKeyLookup",
                    owned.Track(ScriptableObject.CreateInstance<TerrainEffectsMetadataKeyLookupDefinition>()));
                var defaultLookup = new System.Collections.Generic.Dictionary<string, ActorParticleTriggerType>();
                Set(footsteps, "m_footstepAudioTypeDefault", ActorAudioTypes.Footsteps_ClimbingWall);
                Set(footsteps, "m_footstepAudioTypeClimbingDefault", ActorAudioTypes.Footsteps_Dirt);
                Set(footsteps, "m_footstepParticleLookupDefault", defaultLookup);
                Set(footsteps, "m_footstepAudioType", ActorAudioTypes.Footsteps_Dirt);
                Set(footsteps, "m_footstepAudioTypeClimbing", ActorAudioTypes.Footsteps_ClimbingWall);
                Set(footsteps, "m_footstepParticleLookup", null);

                Call(footsteps, "RecalculateFootstepAudio");

                Assert.That(Get<ActorAudioTypes>(footsteps, "m_footstepAudioType"), Is.EqualTo(ActorAudioTypes.Footsteps_ClimbingWall));
                Assert.That(Get<ActorAudioTypes>(footsteps, "m_footstepAudioTypeClimbing"), Is.EqualTo(ActorAudioTypes.Footsteps_Dirt));
                Assert.That(Get<object>(footsteps, "m_footstepParticleLookup"), Is.SameAs(defaultLookup));
            });
        }

        // OnCharacterCollisionDataEnter 0x763DD0 tracks a surface only when it has effects with a
        // terrain type (HasEffects at +0x5c), and OnCharacterCollisionDataExit 0x7640E0
        // recalculates only when the surface was tracked.
        [Test]
        public void FootstepsIgnoreSurfacesWithoutEffectsAndRecalculateOnlyWhenATrackedSurfaceLeaves()
        {
            WithFootsteps((owned, footsteps, character) =>
            {
                Set(footsteps, "m_terrainEffectsMetadataKeyLookup",
                    owned.Track(ScriptableObject.CreateInstance<TerrainEffectsMetadataKeyLookupDefinition>()));
                var surface = NewColliderHost(owned, "Lucid footsteps surface").AddComponent<CharacterCollisionData>();
                var tracked = Get<System.Collections.Generic.HashSet<CharacterCollisionData>>(footsteps, "m_activeSurfaces");
                Assert.That(surface.HasEffects, Is.False);

                Call(footsteps, "OnCharacterCollisionDataEnter", surface, null);
                Assert.That(tracked, Is.Empty, "a surface without effects is not tracked");

                Set(footsteps, "m_footstepAudioType", ActorAudioTypes.Footsteps_ClimbingWall);
                Call(footsteps, "OnCharacterCollisionDataExit", surface, null);
                Assert.That(Get<ActorAudioTypes>(footsteps, "m_footstepAudioType"), Is.EqualTo(ActorAudioTypes.Footsteps_ClimbingWall),
                    "an untracked surface leaving does not recalculate");

                tracked.Add(surface);
                Call(footsteps, "OnCharacterCollisionDataExit", surface, null);
                Assert.That(tracked, Is.Empty);
                Assert.That(Get<ActorAudioTypes>(footsteps, "m_footstepAudioType"), Is.EqualTo(ActorAudioTypes.Footsteps_Dirt),
                    "a tracked surface leaving restores the default audio");
            });
        }

        // Private setters of auto-properties are called as the owning class's own code does; the lookup
        // goes through the declaring type because a derived PropertyInfo hides an inherited private setter.
        private static void SetPrivate(object target, string property, object value) =>
            target.GetType().GetProperty(property).DeclaringType.GetProperty(property).GetSetMethod(true).Invoke(target, new[] { value });

        // A surface whose effect metadata holds one TerrainEffectType row (or none) under the key.
        private static CharacterCollisionData NewEffectSurface(Owned owned, MetadataKeyType key, string effect)
        {
            var surface = NewColliderHost(owned, "Lucid footsteps effect surface").AddComponent<CharacterCollisionData>();
            var group = owned.Track(ScriptableObject.CreateInstance<MetadataFixedGroup>());
            if (effect != null)
                Get<System.Collections.Generic.List<MetadataFixed>>(Get<object>(group, "m_data"), "m_metadata")
                    .Add(new MetadataFixed(key, effect));
            SetPrivate(surface, "EffectMetadata", group);
            SetPrivate(surface, "HasEffects", true);
            return surface;
        }

        // OnCharacterCollisionDataEnter 0x763DD0 tracks a HasEffects surface only when its metadata has
        // the TerrainEffectType key (TryGetValue at 0x763e34) and then recalculates.
        // RecalculateFootstepAudio 0x763E70 stores the looked-up audio to both the ground (+0x54) and
        // climbing (+0x5c) fields (0x763ffc-0x764006), stores the looked-up particle table
        // unconditionally once its TryGetValue succeeds (0x76403e-0x76404a, null for Dirt), and ends
        // the loop after the first surface with a terrain type (both outcomes jump to 0x764052).
        [Test]
        public void FootstepsTheFirstTrackedTerrainEffectOverridesTheDefaults()
        {
            WithFootsteps((owned, footsteps, character) =>
            {
                var key = owned.Track(ScriptableObject.CreateInstance<MetadataKeyType>());
                var definition = owned.Track(ScriptableObject.CreateInstance<TerrainEffectsMetadataKeyLookupDefinition>());
                definition.TerrainEffectTypeMetadataKey = key;
                Set(footsteps, "m_terrainEffectsMetadataKeyLookup", definition);
                var defaultLookup = new System.Collections.Generic.Dictionary<string, ActorParticleTriggerType>();
                var climbingLookup = typeof(ActorFootsteps).GetField("s_particleLookup", BindingFlags.Static | BindingFlags.NonPublic)
                    .GetValue(null);
                Set(footsteps, "m_footstepParticleLookupDefault", defaultLookup);
                var tracked = Get<System.Collections.Generic.HashSet<CharacterCollisionData>>(footsteps, "m_activeSurfaces");

                void Defaults(ActorAudioTypes audio, ActorAudioTypes climbing)
                {
                    Set(footsteps, "m_footstepAudioTypeDefault", audio);
                    Set(footsteps, "m_footstepAudioTypeClimbingDefault", climbing);
                }

                // ClimbingWall: both audio fields and the shared particle table.
                Defaults(ActorAudioTypes.Footsteps_Dirt, ActorAudioTypes.Footsteps_Dirt);
                CharacterCollisionData wall = NewEffectSurface(owned, key, "ClimbingWall");
                Call(footsteps, "OnCharacterCollisionDataEnter", wall, null);
                Assert.That(tracked, Is.EquivalentTo(new[] { wall }));
                Assert.That(Get<ActorAudioTypes>(footsteps, "m_footstepAudioType"), Is.EqualTo(ActorAudioTypes.Footsteps_ClimbingWall));
                Assert.That(Get<ActorAudioTypes>(footsteps, "m_footstepAudioTypeClimbing"), Is.EqualTo(ActorAudioTypes.Footsteps_ClimbingWall));
                Assert.That(Get<object>(footsteps, "m_footstepParticleLookup"), Is.SameAs(climbingLookup));
                Call(footsteps, "OnCharacterCollisionDataExit", wall, null);
                Assert.That(Get<object>(footsteps, "m_footstepParticleLookup"), Is.SameAs(defaultLookup));

                // Dirt: both audio fields, and its null particle table replaces the default.
                Defaults(ActorAudioTypes.Footsteps_ClimbingWall, ActorAudioTypes.Footsteps_ClimbingWall);
                CharacterCollisionData dirt = NewEffectSurface(owned, key, "Dirt");
                Call(footsteps, "OnCharacterCollisionDataEnter", dirt, null);
                Assert.That(Get<ActorAudioTypes>(footsteps, "m_footstepAudioType"), Is.EqualTo(ActorAudioTypes.Footsteps_Dirt));
                Assert.That(Get<ActorAudioTypes>(footsteps, "m_footstepAudioTypeClimbing"), Is.EqualTo(ActorAudioTypes.Footsteps_Dirt));
                Assert.That(Get<object>(footsteps, "m_footstepParticleLookup"), Is.Null, "Dirt maps to a null particle table");

                // The first tracked surface decides: a later ClimbingWall does not override Dirt.
                Call(footsteps, "OnCharacterCollisionDataEnter", wall, null);
                Assert.That(tracked.Count, Is.EqualTo(2));
                Assert.That(Get<ActorAudioTypes>(footsteps, "m_footstepAudioType"), Is.EqualTo(ActorAudioTypes.Footsteps_Dirt));
                Assert.That(Get<object>(footsteps, "m_footstepParticleLookup"), Is.Null);
                Call(footsteps, "OnCharacterCollisionDataExit", dirt, null);
                Call(footsteps, "OnCharacterCollisionDataExit", wall, null);

                // Metal has an audio entry but no particle entry, which keeps the default table.
                Defaults(ActorAudioTypes.Footsteps_ClimbingWall, ActorAudioTypes.Footsteps_ClimbingWall);
                CharacterCollisionData metal = NewEffectSurface(owned, key, "Metal");
                Call(footsteps, "OnCharacterCollisionDataEnter", metal, null);
                Assert.That(Get<ActorAudioTypes>(footsteps, "m_footstepAudioType"), Is.EqualTo(ActorAudioTypes.Footsteps_Dirt));
                Assert.That(Get<ActorAudioTypes>(footsteps, "m_footstepAudioTypeClimbing"), Is.EqualTo(ActorAudioTypes.Footsteps_Dirt));
                Assert.That(Get<object>(footsteps, "m_footstepParticleLookup"), Is.SameAs(defaultLookup));
                Call(footsteps, "OnCharacterCollisionDataExit", metal, null);

                // A HasEffects surface whose metadata lacks the key is not tracked.
                CharacterCollisionData keyless = NewEffectSurface(owned, key, null);
                Call(footsteps, "OnCharacterCollisionDataEnter", keyless, null);
                Assert.That(tracked, Is.Empty);
            });
        }

        // ---- CharacterProxyAssignTrigger -------------------------------------------------------

        // get_SetNullOnExit 0x76A890 is a constant true that ignores the serialised flag.
        [Test]
        public void ProxyAssignAlwaysClearsTheTargetOnExit()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<LevelManager>();
                owned.LeaseRow<CharacterManager>();
                var trigger = owned.Host("Lucid proxy assign", false).AddComponent<CharacterProxyAssignTrigger>();

                Assert.That(Get<bool>(trigger, "m_setNullOnExit"), Is.False);
                Assert.That(Call(trigger, "get_SetNullOnExit"), Is.True);
            });
        }

        // GetTargetTransform 0x76A8A0 answers the current character's transform, not the collider's,
        // and null while no valid current character exists (TryGet then TryGetCurrentCharacter).
        [Test]
        public void ProxyAssignTargetsTheCurrentCharactersTransformOnlyWhileOneIsValid()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<LevelManager>();
                owned.LeaseRow<CharacterManager>();
                var absent = owned.Host("Lucid proxy assign no manager", false).AddComponent<CharacterProxyAssignTrigger>();
                Assert.That(Call(absent, "GetTargetTransform", new object[] { null }), Is.Null, "no CharacterManager registered");
            });

            WithManager((owned, manager, character) =>
            {
                var trigger = owned.Host("Lucid proxy assign", false).AddComponent<CharacterProxyAssignTrigger>();
                Collider other = owned.Host("Lucid proxy assign other", false).AddComponent<BoxCollider>();

                Assert.That(Call(trigger, "GetTargetTransform", other), Is.Null, "a manager without a valid character");

                SetCurrent(manager, character, true);
                Assert.That(Call(trigger, "GetTargetTransform", other), Is.SameAs(character.transform));
                Assert.That(character.transform, Is.Not.SameAs(other.transform));

                SetCurrent(manager, character, false);
                Assert.That(Call(trigger, "GetTargetTransform", other), Is.Null, "the validity flag decides");
            });
        }

        // ---- CharacterSplineAttachTrigger ------------------------------------------------------

        private sealed class SplineFixture
        {
            public CharacterSplineAttachTriggerProbe Trigger;
            public Spline Facade;
            public InertSpline Inert;
        }

        // A trigger at the given position with an optional Spline facade over an InertSpline. The
        // spline hangs under an inactive parent, so activating it wakes nothing.
        private static SplineFixture NewSplineTrigger(Owned owned, Vector3 position, bool withSpline)
        {
            var fixture = new SplineFixture();
            GameObject host = NewColliderHost(owned, "Lucid spline attach");
            host.transform.position = position;
            fixture.Trigger = host.AddComponent<CharacterSplineAttachTriggerProbe>();
            if (withSpline)
            {
                GameObject parent = owned.Host("Lucid spline attach parent", false);
                GameObject splineObject = owned.Host("Lucid spline attach spline", true, parent.transform);
                fixture.Inert = splineObject.AddComponent<InertSpline>();
                fixture.Facade = splineObject.AddComponent<Spline>();
                Set(fixture.Facade, "m_inertSpline", fixture.Inert);
                Set(fixture.Facade, "m_activeSplineType", SplineVariantType.Inert);
                Set(fixture.Trigger, "m_spline", fixture.Facade);
            }
            return fixture;
        }

        // .ctor 0x5CAEF0 stores true in m_useLegacyLogic (+0x59) only.
        [Test]
        public void SplineAttachDefaultsToLegacyLogicAndOneDirection()
        {
            WithCharacter((owned, character, storage) =>
            {
                SplineFixture fixture = NewSplineTrigger(owned, Vector3.zero, false);

                Assert.That(Get<bool>(fixture.Trigger, "m_useLegacyLogic"), Is.True);
                Assert.That(Get<bool>(fixture.Trigger, "m_biDirectional"), Is.False);
                Assert.That(Get<Spline>(fixture.Trigger, "m_spline"), Is.Null);
            });
        }

        // DoTriggerEnter 0x5CAC60 and DoTriggerExit 0x5CAF10 test m_spline right after the base call, so
        // without a spline the virtual GetFSMKey is not asked and the storage is not touched.
        [Test]
        public void SplineAttachWithoutASplineNeitherNamesAKeyNorTouchesTheStorage()
        {
            WithCharacter((owned, character, storage) =>
            {
                SplineFixture fixture = NewSplineTrigger(owned, Vector3.zero, false);
                var stored = new Inert();
                storage.SetValue<object>(CharacterSplineAttachTriggerProbe.Key, stored);

                Call(fixture.Trigger, "DoTriggerEnter", character);
                Call(fixture.Trigger, "DoTriggerExit", character);

                Assert.That(fixture.Trigger.KeyRequests, Is.EqualTo(0));
                Assert.That(storage.HasValue(CharacterSplineAttachTriggerProbe.Key), Is.True, "exit did not remove the existing value");
            });
        }

        private sealed class Inert { }

        // Enter stores the spline under the key the descendant names; a one-directional trigger
        // ignores a character whose offset to the trigger has a negative dot with the trigger's forward
        // (ucomiss 0 > dot at 0x5cad4d), so a zero dot still attaches.
        [Test]
        public void SplineAttachStoresTheSplineUnlessAOneDirectionalTriggerIsBehindTheCharacter()
        {
            WithCharacter((owned, character, storage) =>
            {
                SplineFixture ahead = NewSplineTrigger(owned, new Vector3(0f, 0f, 5f), true);
                Call(ahead.Trigger, "DoTriggerEnter", character);
                Assert.That(ahead.Trigger.KeyRequests, Is.EqualTo(1));
                Assert.That(storage.GetValue<ISpline>(CharacterSplineAttachTriggerProbe.Key, null, true), Is.SameAs(ahead.Inert));
                storage.RemoveValue<ISpline>(CharacterSplineAttachTriggerProbe.Key);

                SplineFixture behind = NewSplineTrigger(owned, new Vector3(0f, 0f, -5f), true);
                Call(behind.Trigger, "DoTriggerEnter", character);
                Assert.That(behind.Trigger.KeyRequests, Is.EqualTo(0));
                Assert.That(storage.HasValue(CharacterSplineAttachTriggerProbe.Key), Is.False, "behind a one-directional trigger");

                Set(behind.Trigger, "m_biDirectional", true);
                Call(behind.Trigger, "DoTriggerEnter", character);
                Assert.That(storage.GetValue<ISpline>(CharacterSplineAttachTriggerProbe.Key, null, true), Is.SameAs(behind.Inert),
                    "a bidirectional trigger attaches from behind");
                storage.RemoveValue<ISpline>(CharacterSplineAttachTriggerProbe.Key);

                SplineFixture level = NewSplineTrigger(owned, Vector3.zero, true);
                Call(level.Trigger, "DoTriggerEnter", character);
                Assert.That(storage.GetValue<ISpline>(CharacterSplineAttachTriggerProbe.Key, null, true), Is.SameAs(level.Inert),
                    "a zero dot is not behind");
            });
        }

        [Test]
        public void SplineAttachExitRemovesTheStoredValueOnlyWhenItHasASpline()
        {
            WithCharacter((owned, character, storage) =>
            {
                SplineFixture fixture = NewSplineTrigger(owned, Vector3.zero, true);
                storage.SetValue<ISpline>(CharacterSplineAttachTriggerProbe.Key, fixture.Inert);

                Call(fixture.Trigger, "DoTriggerExit", character);

                Assert.That(fixture.Trigger.KeyRequests, Is.EqualTo(1));
                Assert.That(storage.HasValue(CharacterSplineAttachTriggerProbe.Key), Is.False);
            });
        }

        // Records the generic argument of each storage call, which FSMStorage ignores.
        private sealed class RecordingStorage : IGraphStorage
        {
            private readonly FSMStorage m_inner = new FSMStorage(0);
            public readonly System.Collections.Generic.List<(string call, Type type)> Calls =
                new System.Collections.Generic.List<(string, Type)>();

            public void Initialise() => m_inner.Initialise();
            public void Clear() => m_inner.Clear();
            public System.Collections.Generic.IReadOnlyDictionary<GraphStorageKey, object> GetCollection() => m_inner.GetCollection();

            public void SetValue<T>(GraphStorageKey storageKey, T value)
            {
                Calls.Add(("SetValue", typeof(T)));
                m_inner.SetValue(storageKey, value);
            }

            public T GetValue<T>(GraphStorageKey storageKey, T defaultValue = default, bool storeDefault = true) =>
                m_inner.GetValue(storageKey, defaultValue, storeDefault);

            public void RemoveValue<T>(GraphStorageKey storageKey)
            {
                Calls.Add(("RemoveValue", typeof(T)));
                m_inner.RemoveValue<T>(storageKey);
            }
        }

        // Enter stores the value as ISpline  and exit removes it as ISurface
        // (IGraphStorage.RemoveValue<ISurface> at 0x5cafa8); FSMStorage ignores the type argument, so
        // only a recording storage tells the two apart.
        [Test]
        public void SplineAttachStoresAsISplineAndRemovesAsISurface()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<GameplayIslandManager>();
                var storage = new RecordingStorage();
                Character character = NewCharacter(owned, storage);
                SplineFixture fixture = NewSplineTrigger(owned, Vector3.zero, true);

                Call(fixture.Trigger, "DoTriggerEnter", character);
                Call(fixture.Trigger, "DoTriggerExit", character);

                Assert.That(storage.Calls, Is.EqualTo(new[] { ("SetValue", typeof(ISpline)), ("RemoveValue", typeof(ISurface)) }));
            });
        }

        // ActivateSpline 0x5CB040 does nothing without a spline and otherwise sets the active state
        // of the spline's GameObject.
        [Test]
        public void SplineAttachActivateSplineSetsTheSplinesActiveState()
        {
            WithCharacter((owned, character, storage) =>
            {
                SplineFixture without = NewSplineTrigger(owned, Vector3.zero, false);
                Assert.DoesNotThrow(() => without.Trigger.ActivateSpline(true));

                SplineFixture fixture = NewSplineTrigger(owned, Vector3.zero, true);
                GameObject splineObject = fixture.Facade.gameObject;
                Assert.That(splineObject.activeSelf, Is.True);
                fixture.Trigger.ActivateSpline(false);
                Assert.That(splineObject.activeSelf, Is.False);
                fixture.Trigger.ActivateSpline(true);
                Assert.That(splineObject.activeSelf, Is.True);
            });
        }

        // ---- CharacterMagnetismModifier --------------------------------------------------------

        // The constructor 0x5CBF30 builds each curve with AnimationCurve.Constant(0, 1, 1).
        [Test]
        public void MagnetismStartsWithThreeSeparateFlatCurvesOfOne()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CharacterManager>();
                var modifier = owned.Host("Lucid magnetism curves", false).AddComponent<CharacterMagnetismModifier>();
                string[] names = { "m_magnetismByDistanceForward", "m_magnetismByDistanceLateral", "m_magnetismByDistanceOnNormal" };
                AnimationCurve[] curves = names.Select(name => Get<AnimationCurve>(modifier, name)).ToArray();

                foreach (AnimationCurve curve in curves)
                    foreach (float distance in new[] { -3f, 0f, 0.5f, 1f, 9f })
                        Assert.That(curve.Evaluate(distance), Is.EqualTo(1f), "distance " + distance);
                // AnimationCurve.Equals compares contents, so identity is what makes the curves separate.
                Assert.That(curves.Select(RuntimeHelpers.GetHashCode).Distinct().Count(), Is.EqualTo(3));
                Assert.That(Get<CharacterAbilityTypeGroup>(modifier, "m_abilityTypeGroup"), Is.Null);
            });
        }

        // OnTriggerEnter 0x5CB9B0 and OnTriggerStay 0x5CBC90 both start with the base
        // TryGetCharacter(other): a missing or invalid manager, or a collider that is not the current
        // character's collision collider, leaves the frame as it was.
        [Test]
        public void MagnetismEnterIgnoresAnythingButTheCurrentCharactersCollider()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CharacterManager>();
                var absent = owned.Host("Lucid magnetism no manager", false).AddComponent<CharacterMagnetismModifier>();
                absent.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                Call(absent, "OnTriggerEnter", new object[] { null });
                Assert.That(Get<Vector3>(absent, "m_worldUp"), Is.EqualTo(Vector3.zero), "no CharacterManager registered");
            });

            WithManager((owned, manager, character) =>
            {
                var modifier = owned.Host("Lucid magnetism", false).AddComponent<CharacterMagnetismModifier>();
                modifier.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                Collider characterCollider = character.GetComponent<BoxCollider>();
                Collider stranger = owned.Host("Lucid magnetism stranger", false).AddComponent<BoxCollider>();
                Set(character, "m_colliderCollision", characterCollider);

                Call(modifier, "OnTriggerEnter", characterCollider);
                Assert.That(Get<Vector3>(modifier, "m_worldUp"), Is.EqualTo(Vector3.zero), "no valid character");

                SetCurrent(manager, character, true);
                Call(modifier, "OnTriggerEnter", stranger);
                Assert.That(Get<Vector3>(modifier, "m_worldUp"), Is.EqualTo(Vector3.zero), "not the character's collider");
            });
        }

        // With the current character's collider, Enter stores the trigger's up vector and rebuilds the
        // forward (the character's normalised velocity projected onto the trigger plane, normalised)
        // and right (Cross(up, forward)) vectors; an idle character has no velocity, so both are zero.
        [Test]
        public void MagnetismEnterCapturesTheTriggersUpAndRebuildsTheFrame()
        {
            WithManager((owned, manager, character) =>
            {
                var modifier = owned.Host("Lucid magnetism frame", false).AddComponent<CharacterMagnetismModifier>();
                modifier.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                Collider characterCollider = character.GetComponent<BoxCollider>();
                Set(character, "m_colliderCollision", characterCollider);
                SetCurrent(manager, character, true);
                Set(modifier, "m_worldForward", new Vector3(1f, 2f, 3f));
                Set(modifier, "m_worldRight", new Vector3(4f, 5f, 6f));

                Call(modifier, "OnTriggerEnter", characterCollider);

                WorldMemberAccess.AreClose(modifier.transform.up, Get<Vector3>(modifier, "m_worldUp"), "up");
                WorldMemberAccess.AreClose(Vector3.zero, Get<Vector3>(modifier, "m_worldForward"), "forward");
                WorldMemberAccess.AreClose(Vector3.zero, Get<Vector3>(modifier, "m_worldRight"), "right");
                Assert.That(Get<Vector3>(modifier, "m_worldUp").x, Is.LessThan(-0.99f), "a quarter turn about z points up along -x");
            });
        }

        // Enter projects the character's normalised velocity onto the trigger plane (0x5cbb55),
        // normalises it (0x5cbb18) and takes right = Cross(up, forward) (0x5cbc2b-0x5cbc7c). A velocity of
        // (0.6, 0.8, 0) against up (0, 1, 0) gives forward (1, 0, 0) and right (0, 0, -1); an unprojected
        // forward would keep its 0.8 y part and a reversed cross product would give (0, 0, 1).
        [Test]
        public void MagnetismEnterProjectsTheTravelDirectionOntoTheTriggerPlane()
        {
            WithManager((owned, manager, character) =>
            {
                var modifier = owned.Host("Lucid magnetism projection", false).AddComponent<CharacterMagnetismModifier>();
                Collider characterCollider = character.GetComponent<BoxCollider>();
                Set(character, "m_colliderCollision", characterCollider);
                SetCurrent(manager, character, true);
                SetPrivate(character, "WorldVelocityNormalised", new Vector3(0.6f, 0.8f, 0f));

                Call(modifier, "OnTriggerEnter", characterCollider);

                WorldMemberAccess.AreClose(new Vector3(0f, 1f, 0f), Get<Vector3>(modifier, "m_worldUp"), "up");
                WorldMemberAccess.AreClose(new Vector3(1f, 0f, 0f), Get<Vector3>(modifier, "m_worldForward"), "forward");
                WorldMemberAccess.AreClose(new Vector3(0f, 0f, -1f), Get<Vector3>(modifier, "m_worldRight"), "right");
            });
        }

        // Stay stops before moving the character when an ability group is set and none of its abilities
        // is in use (AnyAbilityInUse at 0x5cbd72); moving it would fault here, for want of a body.
        [Test]
        public void MagnetismStayDoesNothingWhileNoAbilityOfTheGroupIsInUse()
        {
            WithManager((owned, manager, character) =>
            {
                var modifier = owned.Host("Lucid magnetism stay", false).AddComponent<CharacterMagnetismModifier>();
                Collider characterCollider = character.GetComponent<BoxCollider>();
                Set(character, "m_colliderCollision", characterCollider);
                SetCurrent(manager, character, true);
                var group = owned.Track(ScriptableObject.CreateInstance<CharacterAbilityTypeGroup>());
                var definition = new CharacterAbilityTypeGroup.AbilityTypeDefinition();
                Set(definition, "m_type", ActorAbilityType.Boss_BodySlam);
                group.Abilities.Add(definition);
                Set(modifier, "m_abilityTypeGroup", group);

                Assert.DoesNotThrow(() => Call(modifier, "OnTriggerStay", characterCollider));
            });
        }

        // Negative control for the guard above: with no ability group the same call proceeds to the
        // velocity update, whose first body access is the virtual WorldVelocity getter (vcall slot 14 at
        // 0x5cbeab, before the SetWorldVelocity vcall at 0x5cbf02); the absent body faults there.
        [Test]
        public void MagnetismStayMovesTheCharacterWhenNoAbilityGroupIsSet()
        {
            WithManager((owned, manager, character) =>
            {
                var modifier = owned.Host("Lucid magnetism stay control", false).AddComponent<CharacterMagnetismModifier>();
                Collider characterCollider = character.GetComponent<BoxCollider>();
                Set(character, "m_colliderCollision", characterCollider);
                SetCurrent(manager, character, true);

                // The fault must come from reading the character's velocity (the getter reaches the absent
                // body through GetTimeScaledBodyVelocity), not from the absent ability group in OnTriggerStay.
                Exception fault = Assert.Catch(() => Call(modifier, "OnTriggerStay", characterCollider));
                Assert.That(fault, Is.InstanceOf<NullReferenceException>());
                Assert.That(fault.StackTrace, Does.Contain("HardlightProject.Character.GetTimeScaledBodyVelocity"));
                Assert.That(fault.StackTrace, Does.Contain("HardlightProject.Character.get_WorldVelocity"));
            });
        }

        // ---- CharacterPause --------------------------------------------------------------------

        // PauseGame 0x6E60F0 returns while the manager reference is null (IsNull) or holds no valid
        // current character, before it asks for the TimeManager.
        [Test]
        public void PauseGameDoesNothingWithoutAValidCurrentCharacter()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CharacterManager>();
                var pause = owned.Host("Lucid pause no manager", false).AddComponent<CharacterPause>();
                Set(pause, "m_characterManagerRef", ProcessManager.GetSystemRef<CharacterManager>(null, true));

                Assert.DoesNotThrow(() => pause.PauseGame());
                Assert.That(Get<object>(pause, "m_pauseTimeOverride"), Is.Null);
            });

            WithManager((owned, manager, character) =>
            {
                var pause = owned.Host("Lucid pause", false).AddComponent<CharacterPause>();
                Set(pause, "m_characterManagerRef", ProcessManager.GetSystemRef<CharacterManager>(null, true));

                Assert.DoesNotThrow(() => pause.PauseGame());
                Assert.DoesNotThrow(() => pause.ResumeGame());
                Assert.That(Get<object>(pause, "m_pauseTimeOverride"), Is.Null);
            });
        }

        // A real TimeManager on an inactive host (no Awake), registered into its absent row for the case.
        // TimeManager.Reset (1.10.1 0x1B168D0) reads m_timeCategoryConfiguration without a null test
        // and the Editor sends Reset on AddComponent.
        private static TimeManager NewRegisteredTimeManager(Owned owned)
        {
            LogAssert.Expect(LogType.Exception, new Regex("^NullReferenceException: Object reference not set to an instance of an object$"));
            var manager = owned.Host("Lucid pause time manager", false).AddComponent<TimeManager>();
            int settingId = (int)typeof(TimeManager).GetField("SettingId", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();
            Get<StackableData>(manager, "m_timeSettings").SetBaseValue(settingId, new TimeSetting());
            return owned.Register(manager);
        }

        // RemoveTimeOverride 0x6E6260 removes the stored handle from the manager, clears the field and
        // answers whether there was one.
        [Test]
        public void RemoveTimeOverrideClearsTheHandleAndReportsWhetherThereWasOne()
        {
            Owned.Run(owned =>
            {
                TimeManager manager = NewRegisteredTimeManager(owned);
                var pause = owned.Host("Lucid pause override", false).AddComponent<CharacterPause>();

                Assert.That(Call(pause, "RemoveTimeOverride", manager), Is.False);

                Set(pause, "m_pauseTimeOverride", manager.ApplyTimeSetting(new TimeSetting()));
                Assert.That(Call(pause, "RemoveTimeOverride", manager), Is.True);
                Assert.That(Get<object>(pause, "m_pauseTimeOverride"), Is.Null);
                Assert.That(Call(pause, "RemoveTimeOverride", manager), Is.False);
            });
        }

        // OnDisable 0x6E63A0 looks the TimeManager up with GetSystemSafe and releases the handle only
        // when there is one; without a manager the handle is kept.
        [Test]
        public void PauseOnDisableReleasesTheHandleOnlyThroughARegisteredTimeManager()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<TimeManager>();
                var kept = owned.Host("Lucid pause no time manager", false).AddComponent<CharacterPause>();
                var handle = new StackableDataHandle();
                Set(kept, "m_pauseTimeOverride", handle);

                Call(kept, "OnDisable");
                Assert.That(Get<object>(kept, "m_pauseTimeOverride"), Is.SameAs(handle), "no TimeManager to release it through");
            });

            Owned.Run(owned =>
            {
                TimeManager manager = NewRegisteredTimeManager(owned);
                var pause = owned.Host("Lucid pause disable", false).AddComponent<CharacterPause>();
                Set(pause, "m_pauseTimeOverride", manager.ApplyTimeSetting(new TimeSetting()));

                Call(pause, "OnDisable");
                Assert.That(Get<object>(pause, "m_pauseTimeOverride"), Is.Null);
            });
        }
    }
}
