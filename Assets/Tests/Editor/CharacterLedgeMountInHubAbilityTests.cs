using System;
using System.Collections.Generic;
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
using CameraType = HardlightProject.CameraType;
using Object = UnityEngine.Object;
using RangeAttribute = UnityEngine.RangeAttribute;

namespace ProjectLucid.Tests
{
    // The in-hub and ledge mount abilities with their definitions. Expected values come from the 1.10.1 x86-64
    // constructors and bodies cited per test; the constants are read from the binary's data section, never from the
    // implementation. The ledge search is run against real box colliders: a thin slab tilted 45 degrees about the
    // X axis has face normals (0, sqrt(1/2), +-sqrt(1/2)) that are known without running the code under test.
    public sealed class CharacterLedgeMountInHubAbilityTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const float Root = 0.70710678f;

        private readonly List<Object> m_owned = new List<Object>();

        private T Create<T>() where T : ScriptableObject
        {
            var definition = ScriptableObject.CreateInstance<T>();
            m_owned.Add(definition);
            return definition;
        }

        [TearDown]
        public void DestroyOwnedObjects()
        {
            for (int i = m_owned.Count - 1; i >= 0; i--)
                if (m_owned[i] != null) Object.DestroyImmediate(m_owned[i]);
            m_owned.Clear();
        }

        // ---- CharacterAbilityDefinition_LedgeMount (020004e2) ----

        // The constructor 0x543D30 stores one 16 byte load of the constants at VA 0x29a7730 (20, 10, 0.5, 0) over the forward
        // and upward mount velocities, the pause and the first component of the origin offset, one 8 byte load at 0x29a7450
        // (0.25, 0.25) over the other two, the single and the integer of the 8 byte immediate 0xC3F000000 (0.5 and 12) over the
        // raycast distance and step count, and nothing over the two flags. The inlined base constructors store the
        // TrackMask -1, the angle pair at 0x29a7710 (60, 120), the half second cooldown and the gravity multiplier 1.
        [Test]
        public void LedgeMountDefinitionStoresTheNativeDefaults()
        {
            var definition = Create<CharacterAbilityDefinition_LedgeMount>();
            Assert.That((definition.MountVelocityForward, definition.MountVelocityUpward, definition.PauseSeconds), Is.EqualTo((20f, 10f, 0.5f)));
            Assert.That(definition.RaycastOriginOffset, Is.EqualTo(new Vector3(0f, 0.25f, 0.25f)));
            Assert.That((definition.RaycastDistance, definition.RaycastSteps), Is.EqualTo((0.5f, 12)));
            Assert.That((definition.IgnoreHitsBehind, definition.ReverseSearchOrder), Is.EqualTo((false, false)));
            Assert.That((int)definition.TrackMask, Is.EqualTo(-1));
            Assert.That((definition.MinVerticalAngleSurface, definition.MaxVerticalAngleSurface, definition.ReAttachCooldownSeconds), Is.EqualTo((60f, 120f, 0.5f)));
            Assert.That(definition.GravityMultiplier, Is.EqualTo(1f));
        }

        // ---- CharacterAbilityDefinition_InHub (020004de) ----

        // The constructor 0x543980 stores the ground landing time 0.2 (0x3e4ccccd), the spline turn flag true and the ability
        // base gravity multiplier 1 (0x3f800000) through its inlined base constructors, and nothing over its own fields (the serialized class and curve fields are left out because the Editor serializer may fill them).
        [Test]
        public void InHubDefinitionStoresTheNativeDefaults()
        {
            var definition = Create<CharacterAbilityDefinition_InHub>();
            Assert.That(definition.LandingTime, Is.EqualTo(0.2f));
            Assert.That(definition.SplineTurnAngleCanBeOverriden, Is.True);
            Assert.That(definition.GravityMultiplier, Is.EqualTo(1f));
            Assert.That(definition.HubLevelDefinition == null, Is.True);
            Assert.That(definition.CameraType, Is.EqualTo(default(CameraType)));
        }

        // 0x543920 and 0x543CD0 return the type of the ability each definition configures.
        [Test]
        public void DefinitionsNameTheirOwnAbilityScript()
        {
            Assert.That(Create<CharacterAbilityDefinition_InHub>().ScriptType, Is.EqualTo(typeof(CharacterAbility_InHub)));
            Assert.That(Create<CharacterAbilityDefinition_LedgeMount>().ScriptType, Is.EqualTo(typeof(CharacterAbility_LedgeMount)));
        }

        // Dump.cs field order, the author-facing ranges, tooltips and headers, and the asset menu entries.
        [Test]
        public void DeclarationShapeDefinitionsKeepTheirFieldsAttributesAndMenus()
        {
            const BindingFlags declared = Members | BindingFlags.DeclaredOnly;
            Assert.That(typeof(CharacterAbilityDefinition_InHub).GetFields(declared).OrderBy(f => f.MetadataToken).Select(f => f.Name),
                Is.EqualTo(new[] { "HubLevelDefinition", "CameraType", "VelocityStorageParameters", "ControlDeadzoneForExitSpeed", "ControlRestoration" }));
            Assert.That(typeof(CharacterAbilityDefinition_LedgeMount).GetFields(declared).OrderBy(f => f.MetadataToken).Select(f => f.Name),
                Is.EqualTo(new[]
                {
                    "MountVelocityForward", "MountVelocityUpward", "PauseSeconds", "RaycastOriginOffset", "RaycastDistance", "RaycastSteps",
                    "IgnoreHitsBehind", "ReverseSearchOrder",
                }));
            Assert.That(typeof(CharacterAbilityDefinition_InHub).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_MovementGround)));
            Assert.That(typeof(CharacterAbilityDefinition_LedgeMount).BaseType, Is.EqualTo(typeof(CharacterAbilityDefinition_EnterClimbing)));

            FieldInfo Field(string name) => typeof(CharacterAbilityDefinition_LedgeMount).GetField(name);
            foreach ((string name, float min, float max) in new[] { ("MountVelocityForward", 0f, 50f), ("MountVelocityUpward", 0f, 50f), ("PauseSeconds", 0f, 5f), ("RaycastSteps", 2f, 16f) })
            {
                RangeAttribute range = Field(name).GetCustomAttribute<RangeAttribute>(false);
                Assert.That((range.min, range.max), Is.EqualTo((min, max)), name);
            }
            Assert.That(Field("MountVelocityForward").GetCustomAttribute<HeaderAttribute>(false).header, Is.EqualTo("Dismount properties"));
            Assert.That(Field("RaycastOriginOffset").GetCustomAttribute<HeaderAttribute>(false).header, Is.EqualTo("Ledge detection properties"));
            Assert.That(Field("IgnoreHitsBehind").GetCustomAttribute<TooltipAttribute>(false).tooltip,
                Is.EqualTo("If true, ignore any edges detected that are \"behind\" the character in the local rotation."));
            Assert.That(typeof(CharacterAbilityDefinition_InHub).GetField("ControlRestoration").GetCustomAttribute<TooltipAttribute>(false).tooltip,
                Is.EqualTo("How much to dampen movement input by during the deadzone seconds."));

            foreach ((Type type, string menu) in new[] { (typeof(CharacterAbilityDefinition_InHub), "InHub"), (typeof(CharacterAbilityDefinition_LedgeMount), "Ledge Mount") })
            {
                CreateAssetMenuAttribute attribute = type.GetCustomAttribute<CreateAssetMenuAttribute>(false);
                Assert.That(attribute.fileName, Is.EqualTo(type.Name), type.Name);
                Assert.That(attribute.menuName, Is.EqualTo("HardlightProject/DefinitionData/AbilityDef/" + menu), type.Name);
                Assert.That(attribute.order, Is.EqualTo(1), type.Name);
            }
        }

        // ---- CharacterAbility_InHub (020002cf) ----

        // 0x747840 allocates the velocity storage before the base constructor runs; 0x747830 returns it.
        [Test]
        public void InHubOwnsItsOwnVelocityStorage()
        {
            WithAbility<CharacterAbility_InHub, CharacterAbilityDefinition_InHub>((owned, character, ability, definition) =>
            {
                Assert.That(ability, Is.InstanceOf<IVelocityStore>());
                Assert.That(ability.VelocityStorage, Is.Not.Null);
                Assert.That(((IVelocityStore)ability).VelocityStorage, Is.SameAs(ability.VelocityStorage));
                Assert.That(new CharacterAbility_InHub().VelocityStorage, Is.Not.SameAs(ability.VelocityStorage));
            });
        }

        // 0x747790: the ability base initialises first, then the gravity multiplier is taken from the definition, and the
        // storage is initialised from the definition's storage parameters and the character last.
        [Test]
        public void InHubInitialiseFollowsTheNativeOrder()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_InHub), "Initialise");
            int baseInitialise = IndexOf(calls, "CharacterAbility", "Initialise");
            int gravity = IndexOf(calls, "CharacterAbility", "set_GravityMultiplier");
            int storage = IndexOf(calls, "CharacterVelocityStorage", "Initialise");
            Assert.That(new[] { baseInitialise, gravity, storage }, Is.Ordered.Ascending.And.Unique);
            Assert.That(calls.Count(call => call.Name == "get_Definition"), Is.EqualTo(2), "the definition is read for the gravity and for the storage parameters");
        }

        // ---- CharacterAbility_LedgeMount (020002d1) ----

        // 0x7485E0 stores the zero vector in both normal and point; the ability starts untriggered.
        [Test]
        public void ALedgeMountStartsUntriggeredWithZeroVectors()
        {
            var ability = new CharacterAbility_LedgeMount();
            Assert.That(ability.IsTriggered, Is.False);
            Assert.That(ability.LedgeSurfaceNormal, Is.EqualTo(Vector3.zero));
            Assert.That(ability.LedgeMountPoint, Is.EqualTo(Vector3.zero));
            Assert.That(ability, Is.InstanceOf<IAbilityTrigger>());
        }

        // 0x747B50: leaving runs the ability base and clears only the trigger; the stored ledge is kept.
        [Test]
        public void LeavingClearsOnlyTheTrigger()
        {
            WithAbility<CharacterAbility_LedgeMount, CharacterAbilityDefinition_LedgeMount>((owned, character, ability, definition) =>
            {
                SetProperty(ability, "IsTriggered", true);
                SetProperty(ability, "LedgeSurfaceNormal", new Vector3(0f, 1f, 0f));
                SetProperty(ability, "LedgeMountPoint", new Vector3(1f, 2f, 3f));
                Call(ability, "DoOnLeave");
                Assert.That(ability.IsTriggered, Is.False);
                Assert.That(ability.LedgeSurfaceNormal, Is.EqualTo(new Vector3(0f, 1f, 0f)));
                Assert.That(ability.LedgeMountPoint, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            });
        }

        // A character at the origin looking along +Z with up along +Y, two raycast steps (the back ray and the down ray) and
        // a fresh definition otherwise: origin offset (0, 0.25, 0.25) puts the rays at (0, 0.25, 0.25), distance 0.5.
        private static void WithLedge(Action<Owned, CharacterGravityVectorProbe, CharacterAbility_LedgeMount, CharacterAbilityDefinition_LedgeMount> body) =>
            WithAbility<CharacterAbility_LedgeMount, CharacterAbilityDefinition_LedgeMount>((owned, character, ability, definition) =>
            {
                SetActorProperty(character, "WorldPosition", Vector3.zero);
                SetActorProperty(character, "UpDirection", Vector3.up);
                SetActorProperty(character, "ForwardDirection", Vector3.forward);
                definition.RaycastSteps = 2;
                body(owned, character, ability, definition);
            });

        // A box collider on an active owned host, tilted about X so that its local +Z face points (0, +r, +r) for a tilt of -45
        // degrees and its local +Y face points (0, +r, -r).
        private static BoxCollider Slab(Owned owned, Vector3 center, Vector3 size, float tilt)
        {
            GameObject host = owned.Host("Lucid ledge slab");
            host.transform.SetPositionAndRotation(center, Quaternion.Euler(tilt, 0f, 0f));
            BoxCollider box = host.AddComponent<BoxCollider>();
            box.size = size;
            Physics.SyncTransforms();
            return box;
        }

        private static void MountableSlab(Owned owned, float centerZ, float centerY = 0.25f) =>
            Slab(owned, new Vector3(0f, centerY, centerZ), new Vector3(2f, 2f, 0.2f), -45f);

        private static (bool mountable, RaycastHit mountableLedge, bool nonMountable, RaycastHit nonMountableLedge) FindLedges(CharacterAbility_LedgeMount ability)
        {
            var arguments = new object[4];
            Call(ability, "FindLedges", arguments);
            return ((bool)arguments[0], (RaycastHit)arguments[1], (bool)arguments[2], (RaycastHit)arguments[3]);
        }

        private static void AssertNear(Vector3 actual, Vector3 expected, string message) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-3f), message + " " + actual);

        // 0x747DC0 with no collider in reach: neither ledge is found, both hit structures stay zeroed, and the update leaves
        // the ability untriggered without touching the stored edge normals.
        [Test]
        public void NothingInReachLeavesTheAbilityUntriggered()
        {
            WithLedge((owned, character, ability, definition) =>
            {
                var result = FindLedges(ability);
                Assert.That((result.mountable, result.nonMountable), Is.EqualTo((false, false)));
                Assert.That(result.mountableLedge.collider == null && result.nonMountableLedge.collider == null, Is.True);
                SetProperty(ability, "IsTriggered", true);
                Call(ability, "DoOnUpdate", new CharacterBrain_Replay(), 0.02f);
                Assert.That(ability.IsTriggered, Is.False);
                Assert.That(character.Storage.HasValue(ActorFSMKeys.ClimbingNonMountableEdgeNormal), Is.False);
            });
        }

        // 0x747DC0 and 0x747B70: the back ray (0, 0, -1) from (0, 0.25, 0.25) meets the +Z face (normal (0, r, r)) of a slab
        // centred at (0, 0.25, -0.2) at distance 0.45 - 0.1 / r, that is (0, 0.25, -0.0586). The surface angle is 180 minus
        // the angle to gravity, 180 - 135 = 45, outside the 60 to 120 limits (0x748471 SurfaceAngleValid), so the hit is the
        // mountable ledge: the trigger is set and the normal and point are stored. The gravity multiplier is taken
        // from the definition by the inlined base update (0x747C0E).
        [Test]
        public void ATiltedSurfaceOutsideTheClimbLimitsIsTheMountableLedge()
        {
            WithLedge((owned, character, ability, definition) =>
            {
                MountableSlab(owned, -0.2f);
                definition.GravityMultiplier = 2.5f;
                Call(ability, "DoOnUpdate", new CharacterBrain_Replay(), 0.02f);
                Assert.That(ability.IsTriggered, Is.True);
                AssertNear(ability.LedgeSurfaceNormal, new Vector3(0f, Root, Root), "normal");
                AssertNear(ability.LedgeMountPoint, new Vector3(0f, 0.25f, 0.45f - 0.1f / Root - 0.2f), "point");
                Assert.That(ability.GravityMultiplier, Is.EqualTo(2.5f), "refreshed from the definition by the base update");
                Assert.That(character.Storage.HasValue(ActorFSMKeys.ClimbingNonMountableEdgeNormal), Is.False, "a mountable ledge records no edge");
            });
        }

        // 0x747DC0: ground that faces the character's up direction (a dot with up within 0.0001 of one, 0x7482A9
        // WithinTolerance) is skipped, so a flat floor under the down ray is no ledge at all.
        [Test]
        public void FlatGroundIsNotALedge()
        {
            WithLedge((owned, character, ability, definition) =>
            {
                Slab(owned, new Vector3(0f, -0.1f, 0.25f), new Vector3(4f, 0.2f, 4f), 0f);
                var result = FindLedges(ability);
                Assert.That((result.mountable, result.nonMountable), Is.EqualTo((false, false)));
            });
        }

        // 0x7480E9 and 0x748241: with the flag set a hit counts only when it is strictly in front of the plane through the
        // character normal to its forward direction (dot with the unit forward minus the character's, 0x748265 jbe skips
        // zero and below). The first slab is met at z = -0.0586 and is skipped, the second at z = 0.1 and is kept.
        [Test]
        public void IgnoreHitsBehindDropsHitsAtOrBehindTheCharacter()
        {
            foreach ((float centerZ, bool ignore, bool expected) in new[]
            {
                (-0.2f, false, true), (-0.2f, true, false), (-0.0414f, false, true), (-0.0414f, true, true),
            })
                WithLedge((owned, character, ability, definition) =>
                {
                    definition.IgnoreHitsBehind = ignore;
                    MountableSlab(owned, centerZ);
                    Assert.That(FindLedges(ability).mountable, Is.EqualTo(expected), "slab at " + centerZ + ", ignoring hits behind " + ignore);
                });
        }

        // 0x747E8D onwards: the rays are spread from the first to the second direction (the back ray then the down ray, or the
        // reverse when the flag is set, 0x747F0F cmp [+0xFD]) and the search ends at the first mountable hit. A slab met by
        // the down ray with a back-facing top (normal (0, r, -r), dot with forward below -0.0001) is the first non mountable
        // ledge; a slab met only by the back ray is mountable. Down first finds both, back first stops at the mountable one.
        [Test]
        public void ReverseSearchOrderDecidesWhichRayIsTriedFirst()
        {
            foreach ((bool reverse, bool expectNonMountable) in new[] { (false, false), (true, true) })
                WithLedge((owned, character, ability, definition) =>
                {
                    definition.ReverseSearchOrder = reverse;
                    definition.RaycastDistance = 2f;
                    Slab(owned, new Vector3(0f, -0.15f, 0.25f), new Vector3(2f, 0.05f, 0.2f), -45f);
                    MountableSlab(owned, -0.8f);
                    var result = FindLedges(ability);
                    Assert.That(result.mountable, Is.True, "reverse " + reverse);
                    AssertNear(result.mountableLedge.normal, new Vector3(0f, Root, Root), "mountable normal, reverse " + reverse);
                    Assert.That(result.nonMountable, Is.EqualTo(expectNonMountable), "reverse " + reverse);
                    if (expectNonMountable) AssertNear(result.nonMountableLedge.normal, new Vector3(0f, Root, -Root), "non mountable normal");
                });
        }

        // 0x747C81 and 0x747D2F: without a mountable ledge the first non mountable one is appended to the stored list of edge
        // normals and the trigger is cleared; a second update appends again.
        [Test]
        public void ANonMountableLedgeIsAppendedToTheStoredEdgeNormals()
        {
            WithLedge((owned, character, ability, definition) =>
            {
                var edges = new List<Vector3>();
                character.Storage.SetValue(ActorFSMKeys.ClimbingNonMountableEdgeNormal, edges);
                SetProperty(ability, "IsTriggered", true);
                Slab(owned, new Vector3(0f, -0.15f, 0.25f), new Vector3(2f, 0.05f, 0.2f), -45f);
                Call(ability, "DoOnUpdate", new CharacterBrain_Replay(), 0.02f);
                Assert.That(ability.IsTriggered, Is.False);
                Assert.That(edges.Count, Is.EqualTo(1));
                AssertNear(edges[0], new Vector3(0f, Root, -Root), "edge normal");
                Call(ability, "DoOnUpdate", new CharacterBrain_Replay(), 0.02f);
                Assert.That(edges.Count, Is.EqualTo(2));
                Assert.That(ability.LedgeSurfaceNormal, Is.EqualTo(Vector3.zero), "only a mountable ledge stores its surface");
            });
        }

        // 0x74847E-0x7484C1, after the surface angle check: a vertical slab met by the back ray has the normal (0, 0, 1), a surface
        // angle of 180 - 90 = 90 inside 60..120, so the search reaches the collision data. No collision data for the collider
        // (0x7484A2) makes the hit the mountable ledge; so does data that is not climbable, or climbable data whose object is on a
        // layer outside the TrackMask (TerrainClimbable false, taken as mountable at 0x7484BB); climbable data inside the mask records
        // the hit as the non mountable ledge and the search goes on.
        [Test]
        public void TheCollisionDataAndTheClimbableFlagDecideBetweenMountableAndNonMountable()
        {
            const int OtherLayer = 5;
            foreach ((bool hasData, bool climbable, int dataLayer, bool expectMountable) in new[]
            {
                (false, false, 0, true), (true, false, 0, true), (true, true, OtherLayer, true), (true, true, 0, false),
            })
                WithLedge((owned, character, ability, definition) =>
                {
                    definition.TrackMask = ~(1 << OtherLayer);
                    BoxCollider slab = Slab(owned, new Vector3(0f, 0.25f, -0.2f), new Vector3(2f, 2f, 0.2f), 0f);
                    TrackManager tracks = owned.Host("Lucid ledge tracks", false).AddComponent<TrackManager>();
                    HashedTrackGroup group = owned.Host("Lucid ledge group", false).AddComponent<HashedTrackGroup>();
                    tracks.Register(group);
                    Set(character, "m_trackManagerRef", new SystemRef<TrackManager>("TrackManager", tracks));
                    if (hasData)
                    {
                        GameObject dataHost = owned.Host("Lucid ledge data", false);
                        dataHost.AddComponent<BoxCollider>();
                        dataHost.layer = dataLayer;
                        var data = dataHost.AddComponent<CharacterCollisionData>();
                        typeof(CharacterCollisionData).GetProperty("IsClimbable").GetSetMethod(true).Invoke(data, new object[] { climbable });
                        Get<System.Collections.IDictionary>(group, "m_colliderToCharacterCollisionData").Add(slab, data);
                    }
                    string label = "data " + hasData + ", climbable " + climbable + ", data layer " + dataLayer;
                    var result = FindLedges(ability);
                    Assert.That((result.mountable, result.nonMountable), Is.EqualTo((expectMountable, !expectMountable)), label);
                    AssertNear((expectMountable ? result.mountableLedge : result.nonMountableLedge).normal, new Vector3(0f, 0f, 1f), "normal, " + label);
                });
        }

        // ---- IL witnesses ----

        // 0x747B70: the character ability base update runs first (its gravity multiplier refresh from the virtual ability
        // definition is inlined in the native body), then the ledges are searched; the surface and point are stored before
        // the trigger, and the edge list is fetched from the character's storage only on the non mountable path.
        [Test]
        public void LedgeMountUpdateFollowsTheNativeOrder()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_LedgeMount), "DoOnUpdate");
            int baseUpdate = IndexOf(calls, "CharacterAbility", "DoOnUpdate");
            int find = IndexOf(calls, "CharacterAbility_LedgeMount", "FindLedges");
            int normal = IndexOf(calls, "CharacterAbility_LedgeMount", "set_LedgeSurfaceNormal");
            int point = IndexOf(calls, "CharacterAbility_LedgeMount", "set_LedgeMountPoint");
            int trigger = IndexOf(calls, "CharacterAbility_LedgeMount", "set_IsTriggered");
            int edges = IndexOf(calls, "IGraphStorage", "GetValue");
            int add = IndexOf(calls, "List", "Add");
            Assert.That(new[] { baseUpdate, find, normal, point, trigger, edges, add }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(calls, "CharacterAbility_LedgeMount", "set_IsTriggered"), Is.EqualTo(2));
        }

        // 0x747DC0: the search reads the rays' bounds from the definition, spreads them with Vector3.Slerp, casts with
        // Physics.Raycast, and for a candidate hit checks (in this order) the forward plane, the flat ground, the previous normal (Vector3 equality, 0x7482F6),
        // the facing direction, the surface angle, the climbable collision data and the climbable terrain.
        [Test]
        public void LedgeSearchChecksEachHitInTheNativeOrder()
        {
            List<MethodBase> calls = Calls(typeof(CharacterAbility_LedgeMount), "FindLedges");
            int origin = IndexOf(calls, "Quaternion", "op_Multiply");
            int slerp = IndexOf(calls, "Vector3", "Slerp");
            int raycast = IndexOf(calls, "Physics", "Raycast");
            int flat = IndexOf(calls, "MathUtilities", "WithinTolerance");
            int previous = IndexOf(calls, "Vector3", "op_Equality");
            int angle = IndexOf(calls, "Vector3", "Angle");
            int surface = IndexOf(calls, "CharacterAbilityDefinition_EnterClimbing", "SurfaceAngleValid");
            int data = IndexOf(calls, "TrackManager", "TryGetCharacterCollisionData");
            int climbable = IndexOf(calls, "CharacterClimbingCollisionTracker", "TerrainClimbable");
            Assert.That(new[] { origin, slerp, raycast, flat, previous, angle, surface, data, climbable }, Is.Ordered.Ascending.And.Unique);
            Assert.That(Count(calls, "Physics", "Raycast"), Is.EqualTo(1));
            Assert.That(Count(calls, "Vector3", "Dot"), Is.EqualTo(4), "forward plane, hit behind, flat ground, facing");
        }

        // The declared shape: the trigger and the two vectors are read-only from outside, the update is sealed, and the
        // storage is a readonly property backing field.
        [Test]
        public void DeclarationShapeAbilitiesKeepTheirFieldsAndAccessors()
        {
            const BindingFlags declared = Members | BindingFlags.DeclaredOnly;
            Assert.That(typeof(CharacterAbility_LedgeMount).GetFields(declared).OrderBy(f => f.MetadataToken).Select(f => f.Name),
                Is.EqualTo(new[] { "<IsTriggered>k__BackingField", "<LedgeSurfaceNormal>k__BackingField", "<LedgeMountPoint>k__BackingField" }));
            foreach (string property in new[] { "IsTriggered", "LedgeSurfaceNormal", "LedgeMountPoint" })
            {
                PropertyInfo info = typeof(CharacterAbility_LedgeMount).GetProperty(property);
                Assert.That(info.GetGetMethod(false), Is.Not.Null, property);
                Assert.That(info.GetSetMethod(false), Is.Null, property);
                Assert.That(info.GetSetMethod(true).IsPrivate, Is.True, property);
            }
            MethodInfo update = typeof(CharacterAbility_LedgeMount).GetMethod("DoOnUpdate", declared);
            Assert.That(update.IsFinal && update.IsVirtual && update.IsFamily, Is.True);
            Assert.That(typeof(CharacterAbility_LedgeMount).GetMethod("FindLedges", declared).IsPrivate, Is.True);
            Assert.That(typeof(CharacterAbility_LedgeMount).BaseType, Is.EqualTo(typeof(CharacterAbility<CharacterAbilityDefinition_LedgeMount>)));

            FieldInfo[] storage = typeof(CharacterAbility_InHub).GetFields(declared);
            Assert.That(storage.Select(f => f.Name), Is.EqualTo(new[] { "<VelocityStorage>k__BackingField" }));
            Assert.That(storage[0].IsInitOnly && storage[0].IsPrivate, Is.True);
            Assert.That(typeof(CharacterAbility_InHub).BaseType, Is.EqualTo(typeof(CharacterAbility_MovementGround<CharacterAbilityDefinition_InHub>)));
            Assert.That(typeof(CharacterAbility_InHub).GetInterfaces(), Does.Contain(typeof(IVelocityStore)));
            Assert.That(typeof(CharacterAbility_LedgeMount).GetInterfaces(), Does.Contain(typeof(IAbilityTrigger)));
        }
    }
}
