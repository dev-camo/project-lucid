using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.CharCoreShared;
using UnityEditor.Animations;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Climbing = HardlightProject.CharacterState_Climbing<HardlightProject.CharacterAbility_MovementClimbingBoost, HardlightProject.CharacterAbilityDefinition_MovementClimbingBoost>;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CharacterState_Climbing<TAbility, TAbilityDef> (TypeDef 0x02000365, shared x86-64
    // bodies 0xE6C5F0..0xE6F680) and its leaf CharacterState_ClimbingBoost (0x02000366, 0x776090..0x776380),
    // exercised through the boost leaf's closed type. The character is a real component with a real Rigidbody,
    // a probe supplying the gravity vector, and a real TrackManager holding the climbable colliders; the ability
    // and the FSM node are built by their real constructors. The scene is far from the world origin so rays and
    // bounds only meet the fixture. Gravity points along -z, so the horizontal floors the fixtures build count as
    // climbable surfaces (their normal is perpendicular to gravity) and local z is the climb direction.
    public sealed class CharacterClimbingStateTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Vector3 Origin = new Vector3(50f, 50f, 50f);

        private static string Name() => "Lucid-climb-" + Guid.NewGuid().ToString("N");
        private static FiniteStateMachine Machine() => new FiniteStateMachine(Name(), skipAddToManager: true);

        private sealed class ClimbScene : IDisposable
        {
            private readonly List<IDisposable> m_leases = new List<IDisposable>();
            public readonly OwnedWorldObjects Objects = new OwnedWorldObjects("lucid-climbing");
            public readonly CharacterGravityVectorProbe Character;
            public readonly FSMStorage Storage = new FSMStorage(0);
            public readonly CharacterAbilityDefinition_MovementClimbingBoost Definition;
            public readonly CharacterAbility_MovementClimbingBoost Ability;
            public readonly CharacterState_ClimbingBoost State;
            public readonly TrackManager Tracks;
            public readonly HashedTrackGroup Group;

            public ClimbScene()
            {
                try
                {
                    Row(ProcessManager.GetDefaultName<GameplayIslandManager>());
                    Row(ProcessManager.GetDefaultName<App>());
                    Row(ProcessManager.GetDefaultName<TrackManager>());
                    GameObject host = Objects.Create("character", Origin, Quaternion.identity);
                    Character = host.AddComponent<CharacterGravityVectorProbe>();
                    Character.GravityVector = new Vector3(0f, 0f, -9.81f);
                    Set(Character, "m_body", host.AddComponent<Rigidbody>());
                    SetActorProperty(Character, "Storage", Storage);
                    SetCharacter("Settings", new CharacterSettings { Collider = new CharacterSettings.ColliderSettings() });
                    SetCharacter("Collider", new CharacterCollider(Character, null, null));
                    Character.SetWorldRotation(Quaternion.identity);
                    Character.SetWorldPosition(Origin + new Vector3(0f, 0.2f, 0f));
                    // Entering a climb creates the edge-normal list; the updates read it unguarded.
                    Storage.SetValue(ActorFSMKeys.ClimbingNonMountableEdgeNormal, new List<Vector3>());

                    Definition = Objects.Own(ScriptableObject.CreateInstance<CharacterAbilityDefinition_MovementClimbingBoost>());
                    Definition.BoostDefinition = new CharacterStamina.Definition();
                    Definition.LateralMotion = new CharacterAbilityDefinition_Climbing.ClimbingLateralMotion
                    {
                        Acceleration = 10f, SpeedMax = 5f, Deceleration = 10f, VerticalLateralSpeedBias = AnimationCurve.Constant(0f, 1f, 1f),
                    };
                    Definition.VerticalMotion = new CharacterAbilityDefinition_Climbing.ClimbingVerticalMotion
                    {
                        Acceleration = 10f, SpeedMaxUpward = 4f, SpeedMaxDownward = 2f, Deceleration = 10f,
                    };
                    Ability = new CharacterAbility_MovementClimbingBoost();
                    Ability.Initialise(null, Definition);
                    Set(Ability, "m_character", Character);

                    State = (CharacterState_ClimbingBoost)FSMClassFactory.ConstructState(Machine(), nameof(CharacterState_ClimbingBoost), new FSMIdentifier(Name()), "{}");

                    Tracks = Objects.Create("tracks").AddComponent<TrackManager>();
                    Group = Objects.Create("group").AddComponent<HashedTrackGroup>();
                    Tracks.Register(Group);
                    Link(Tracks);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            private void Row(string name) => m_leases.Add(new VerificationFixtureOwnership.LifecycleRow(name));

            public List<Vector3> EdgeNormals => Storage.GetValueOnly<List<Vector3>>(ActorFSMKeys.ClimbingNonMountableEdgeNormal);

            public void Move(Vector3 velocity) => Character.SetWorldVelocity(velocity);

            private void SetCharacter(string property, object value) =>
                typeof(Character).GetProperty(property, Members).GetSetMethod(true).Invoke(Character, new[] { value });

            // The character reads its track manager through its own reference, filled the way the registry would
            // and revoked afterwards, which leaves the row alone.
            private void Link(TrackManager manager)
            {
                SystemRef<TrackManager> reference = ProcessManager.GetSystemRef<TrackManager>(null, true);
                m_leases.Add(new SystemRefLease<TrackManager>(reference, manager));
                Set(Character, "m_trackManagerRef", reference);
            }

            // A solid box the character can climb (or not), registered with the track manager.
            public BoxCollider Surface(string name, Vector3 centre, Vector3 size, bool climbable = true)
            {
                var box = Objects.Create(name, centre, Quaternion.identity).AddComponent<BoxCollider>();
                box.size = size;
                var data = new CharacterCollisionData();
                typeof(CharacterCollisionData).GetProperty("IsClimbable").GetSetMethod(true).Invoke(data, new object[] { climbable });
                Get<IDictionary>(Group, "m_colliderToCharacterCollisionData").Add(box, data);
                Physics.SyncTransforms();
                return box;
            }

            // A floor whose top is at the character's feet (height Origin.y), centred under it.
            public BoxCollider Floor(Vector3 size, bool climbable = true) =>
                Surface("floor", Origin + new Vector3(0f, -0.5f, 0f), size, climbable);

            public void Dispose()
            {
                Objects.Dispose();
                for (int i = m_leases.Count - 1; i >= 0; i--) m_leases[i].Dispose();
                m_leases.Clear();
            }
        }

        private static object Invoke(object state, string name, params object[] arguments)
        {
            MethodInfo method = typeof(Climbing).GetMethod(name, Members | BindingFlags.DeclaredOnly);
            try
            {
                return method.Invoke(state, arguments);
            }
            catch (TargetInvocationException wrapped) when (wrapped.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(wrapped.InnerException).Throw();
                throw;
            }
        }

        private static void AreClose(Vector3 expected, Vector3 actual, float tolerance, string label) =>
            Assert.That((expected - actual).magnitude, Is.LessThan(tolerance), label + ": expected " + expected + " but was " + actual);

        private static List<string> Calls(Type type, string method) =>
            OriginalILCallSequence.Names(OriginalILCallSequence.Calls(type.GetMethod(method, Members | BindingFlags.DeclaredOnly))).ToList();

        // ---- Shape -------------------------------------------------------------------------------

        // Declaration-shape self-check (not a behaviour test): metadata of TypeDef 0x02000365 and 0x02000366 — an
        // abstract generic over an ability and its definition, constrained to the climbing ability and definition
        // families, with the five float constants, and a boost leaf over the boost ability and definition.
        [Test]
        public void DeclarationShapeClimbingKeepsItsGenericConstraintsAndConstants()
        {
            Type open = typeof(CharacterState_Climbing<,>);
            Assert.That(open.IsAbstract, Is.True);
            Assert.That(open.GetGenericArguments().Select(argument => argument.Name), Is.EqualTo(new[] { "TAbility", "TAbilityDef" }));
            Type[] abilityConstraints = open.GetGenericArguments()[0].GetGenericParameterConstraints();
            Assert.That(abilityConstraints, Has.Length.EqualTo(1));
            Assert.That(abilityConstraints[0].GetGenericTypeDefinition(), Is.EqualTo(typeof(CharacterAbility_Climbing<>)));
            Assert.That(open.GetGenericArguments()[1].GetGenericParameterConstraints(), Is.EqualTo(new[] { typeof(CharacterAbilityDefinition_Climbing) }));
            Assert.That(open.BaseType.GetGenericTypeDefinition(), Is.EqualTo(typeof(CharacterState_Ability<>)));

            var constants = new Dictionary<string, float>
            {
                { "SnapDistanceOnUpdate", 4f }, { "MinSnapDistanceOnUpdate", 0.02f }, { "SnapDistanceOnEnter", 1f },
                { "SnapGroundCollisionBuffer", 0.0001f }, { "MinCheckDistanceOnMovingSurface", 1f },
            };
            foreach (KeyValuePair<string, float> constant in constants)
            {
                FieldInfo field = open.GetField(constant.Key, Members);
                Assert.That(field.IsLiteral, Is.True, constant.Key);
                Assert.That((float)field.GetRawConstantValue(), Is.EqualTo(constant.Value), constant.Key);
            }
            FieldInfo hits = open.GetField("m_raycastHits", Members);
            Assert.That(hits.FieldType, Is.EqualTo(typeof(RaycastHit[])));
            Assert.That(hits.IsInitOnly, Is.True);

            Assert.That(typeof(CharacterState_ClimbingBoost).BaseType, Is.EqualTo(typeof(Climbing)));
        }

        // The boost leaf is a registered graph node with the shared ability arguments, and each node builds its
        // own four-slot raycast buffer (SZArrayNew 4 at 0xE6C63C).
        [Test]
        public void ClimbingBoostIsARegisteredNodeWithItsOwnFourHitBuffer()
        {
            Assert.That(FSMClassFactory.States.TryGetValue(nameof(CharacterState_ClimbingBoost), out JSONFactoryClass factory), Is.True);
            Assert.That(factory.ClassType, Is.EqualTo(typeof(CharacterState_ClimbingBoost)));
            Assert.That(factory.ConstructInstanceMethod.DeclaringType, Is.EqualTo(typeof(CharacterState_ClimbingBoost)));
            Assert.That(factory.JSONCtorArgsType.Name, Is.EqualTo("AbilityJSONCtorArgs"));
            Assert.That(factory.JSONCtorArgsType.DeclaringType, Is.EqualTo(typeof(ActorState_Ability)));
            Assert.That(typeof(CharacterState_ClimbingBoost).GetCustomAttribute<GraphNodeDefaultNameAttribute>(false).DefaultName, Is.EqualTo("ClimbingBoost"));
            Assert.That(typeof(CharacterState_ClimbingBoost).GetCustomAttribute<GraphNodeMenuFormatAttribute>(false).Format, Is.EqualTo("Character/{0}"));
            Assert.That(typeof(Climbing).IsAbstract, Is.True, "the generic base is not a node");
            Assert.That(FSMClassFactory.States.ContainsKey(typeof(CharacterState_Climbing<,>).Name), Is.False);

            IFSMState first = FSMClassFactory.ConstructState(Machine(), nameof(CharacterState_ClimbingBoost), new FSMIdentifier(Name()), "{}");
            IFSMState second = FSMClassFactory.ConstructState(Machine(), nameof(CharacterState_ClimbingBoost), new FSMIdentifier(Name()), "{}");
            var firstHits = Get<RaycastHit[]>(first, "m_raycastHits");
            Assert.That(firstHits, Has.Length.EqualTo(4));
            Assert.That(Get<RaycastHit[]>(second, "m_raycastHits"), Is.Not.SameAs(firstHits));
        }

        // ---- Small helpers -----------------------------------------------------------------------

        // 06001554 (0xE6F5C0): max(deltaTime * 4 * velocity, 0.02). The maximum keeps the second operand for an
        // unordered first (maxss), so an unordered step gives the floor.
        [Test]
        public void SnapDistanceScalesWithTheStepAndHasAFloor()
        {
            using (var scene = new ClimbScene())
            {
                foreach ((float deltaTime, float velocity, float expected) in new[]
                {
                    (0.5f, 2f, 4f), (0.01f, 1f, 0.04f), (0.001f, 1f, 0.02f), (0.5f, 0f, 0.02f), (0.5f, -3f, 0.02f), (float.NaN, 1f, 0.02f),
                })
                    Assert.That((float)Invoke(scene.State, "GetSnapDistanceOnUpdate", deltaTime, velocity), Is.EqualTo(expected).Within(1e-6f), deltaTime + " " + velocity);
            }
        }

        // 06001550 (0xE6F080) flags the edge as passed; 06001552 (0xE6F1D0) stores the world position and rotation;
        // 06001553 (0xE6F3D0) puts both back.
        [Test]
        public void FinishSnapshotAndRestoreUseTheClimbingRecords()
        {
            using (var scene = new ClimbScene())
            {
                Invoke(scene.State, "FinishClimbing", scene.Character);
                Assert.That(scene.Storage.GetValueOnly<bool>(ActorFSMKeys.ClimbingEdgePassed), Is.True);

                Quaternion rotation = Quaternion.Euler(10f, 20f, 30f);
                scene.Character.SetWorldRotation(rotation);
                scene.Character.SetWorldPosition(Origin + new Vector3(1f, 2f, 3f));
                Invoke(scene.State, "SnapshotPosition", scene.Character);
                Assert.That(scene.Storage.GetValueOnly<Vector3>(ActorFSMKeys.ClimbingLastKnownPosition), Is.EqualTo(Origin + new Vector3(1f, 2f, 3f)));
                Assert.That(scene.Storage.GetValueOnly<Quaternion>(ActorFSMKeys.ClimbingLastKnownRotation), Is.EqualTo(rotation));

                scene.Character.SetWorldRotation(Quaternion.identity);
                scene.Character.SetWorldPosition(Origin);
                Invoke(scene.State, "RestorePosition", scene.Character);
                Assert.That(scene.Character.WorldPosition, Is.EqualTo(Origin + new Vector3(1f, 2f, 3f)));
                Assert.That(Quaternion.Angle(scene.Character.WorldRotation, rotation), Is.LessThan(0.001f));
            }
        }

        // 06001549 (0xE6CA50): the maintain flag is consumed whenever it is present (even when false, 0xE6CAEA) and
        // chooses the character's current velocity over the one stored at terrain entry (0xE6CBA0); the result is
        // the local velocity with its y part removed (0xE6CC94 blendps). The character faces +x (yaw 90 degrees), so
        // a world velocity (0,3,5) is local (-5,0,0) and (4,2,0) is local (0,0,4).
        [Test]
        public void InitialVelocityIsTheLocalHorizontalVelocityOfTheMaintainedOrStoredOne()
        {
            using (var scene = new ClimbScene())
            {
                scene.Character.SetWorldRotation(Quaternion.Euler(0f, 90f, 0f));
                scene.Storage.SetValue(ActorFSMKeys.ClimbingWorldVelocityAtTerrainEnter, new Vector3(0f, 3f, 5f));
                scene.Move(new Vector3(4f, 2f, 0f));

                Invoke(null, "SetInitialVelocity", scene.Character);
                AreClose(new Vector3(-5f, 0f, 0f), scene.Character.LocalVelocity, 1e-4f, "stored velocity");
                Assert.That(scene.Storage.GetCollection().ContainsKey(ActorFSMKeys.ClimbingMaintainCurrentVelocityOnEnter), Is.False);

                scene.Move(new Vector3(4f, 2f, 0f));
                scene.Storage.SetValue(ActorFSMKeys.ClimbingMaintainCurrentVelocityOnEnter, true);
                Invoke(null, "SetInitialVelocity", scene.Character);
                AreClose(new Vector3(0f, 0f, 4f), scene.Character.LocalVelocity, 1e-4f, "maintained velocity");
                Assert.That(scene.Storage.GetCollection().ContainsKey(ActorFSMKeys.ClimbingMaintainCurrentVelocityOnEnter), Is.False, "the flag is consumed");
                AreClose(new Vector3(0f, 3f, 5f), scene.Storage.GetValueOnly<Vector3>(ActorFSMKeys.ClimbingWorldVelocityAtTerrainEnter), 1e-6f, "the stored velocity is not consumed");

                scene.Move(new Vector3(4f, 2f, 0f));
                scene.Storage.SetValue(ActorFSMKeys.ClimbingMaintainCurrentVelocityOnEnter, false);
                Invoke(null, "SetInitialVelocity", scene.Character);
                AreClose(new Vector3(-5f, 0f, 0f), scene.Character.LocalVelocity, 1e-4f, "a false flag still selects the stored velocity");
                Assert.That(scene.Storage.GetCollection().ContainsKey(ActorFSMKeys.ClimbingMaintainCurrentVelocityOnEnter), Is.False, "a false flag is consumed too");
            }
        }

        // Without a stored entry velocity the default zero is read and stored (GetValue with store-default, 0xE6CC5A).
        [Test]
        public void InitialVelocityWithoutARecordIsZeroAndStoresTheDefault()
        {
            using (var scene = new ClimbScene())
            {
                scene.Move(new Vector3(4f, 2f, 0f));
                Invoke(null, "SetInitialVelocity", scene.Character);
                AreClose(Vector3.zero, scene.Character.LocalVelocity, 1e-5f, "zero");
                Assert.That(scene.Storage.GetCollection().ContainsKey(ActorFSMKeys.ClimbingWorldVelocityAtTerrainEnter), Is.True);
            }
        }

        // ---- Orientation -------------------------------------------------------------------------

        // 0600154d (0xE6E550): the character turns to the surface normal, at most maxRotationDegrees from its
        // current up (an inlined Vector3.Angle then Slerp by max/angle, 0xE6E6E1..0xE6E6FD), looking away from gravity
        // (look direction = -Gravity, 0xE6E70F). Up (0,1,0) towards the normal (1,0,0) is 90 degrees.
        [Test]
        public void OrientationIsLimitedByTheMaximumRotationAndLooksAgainstGravity()
        {
            using (var scene = new ClimbScene())
            {
                scene.Character.GravityVector = new Vector3(0f, -9.81f, 0f);
                Invoke(scene.State, "OrientateToSurface", scene.Character, new Vector3(1f, 0f, 0f), 45f);
                AreClose(new Vector3(0.7071068f, 0.7071068f, 0f), scene.Character.UpDirection, 1e-4f, "half way");

                scene.Character.SetWorldRotation(Quaternion.identity);
                Invoke(scene.State, "OrientateToSurface", scene.Character, new Vector3(1f, 0f, 0f), 180f);
                AreClose(new Vector3(1f, 0f, 0f), scene.Character.UpDirection, 1e-4f, "unlimited");
                AreClose(new Vector3(0f, 1f, 0f), scene.Character.ForwardDirection, 1e-4f, "looking away from gravity");

                scene.Character.SetWorldRotation(Quaternion.identity);
                Invoke(scene.State, "OrientateToSurface", scene.Character, new Vector3(0.5f, 0.8660254f, 0f), 45f);
                AreClose(new Vector3(0.5f, 0.8660254f, 0f), scene.Character.UpDirection, 1e-4f, "inside the limit");
            }
        }

        // ---- Surface queries ---------------------------------------------------------------------

        private static object[] ClimbableArguments(ClimbScene scene, Vector3 contactNormal, Vector3 offset, float snap, bool limitRotation) =>
            new object[] { scene.Character, contactNormal, offset, snap, scene.Ability, null, limitRotation };

        // 0600154f (0xE6EC00): with nothing hit the result is false and the output is the first buffer slot, which is empty.
        [Test]
        public void NothingToClimbGivesNoHit()
        {
            using (var scene = new ClimbScene())
            {
                object[] arguments = ClimbableArguments(scene, Vector3.up, Vector3.zero, 1f, false);
                Assert.That((bool)Invoke(scene.State, "IsPositionClimbable", arguments), Is.False);
                Assert.That(((RaycastHit)arguments[5]).collider, Is.Null);
            }
        }

        // A hit whose normal is outside the climbing angles is skipped (0xE6EF2D test al / je 0xE6EEA0). Gravity
        // along -y makes a floor's -dot(normal, gravity) 1, outside the cone [-0.5, 0.5] of cos 120 and cos 60.
        // With nothing left the output is the first slot, which holds that first hit (0xE6F03C).
        [Test]
        public void SurfaceOutsideTheClimbingAnglesIsRejectedButReportedAsTheFirstSlot()
        {
            using (var scene = new ClimbScene())
            {
                scene.Character.GravityVector = new Vector3(0f, -9.81f, 0f);
                BoxCollider floor = scene.Floor(new Vector3(20f, 1f, 20f));
                object[] arguments = ClimbableArguments(scene, Vector3.up, Vector3.zero, 1f, false);
                Assert.That((bool)Invoke(scene.State, "IsPositionClimbable", arguments), Is.False);
                Assert.That(((RaycastHit)arguments[5]).collider, Is.SameAs(floor));
            }
        }

        // Only a collider the track manager knows and marks climbable counts (0xE6F01C TryGetCharacterCollisionData,
        // 0xE6F030 the climbable flag). A nearer collider the manager does not know hides nothing: it is skipped and
        // the climbable one behind it is accepted.
        [Test]
        public void OnlyCollidersTheTrackManagerMarksClimbableCount()
        {
            using (var scene = new ClimbScene())
            {
                BoxCollider floor = scene.Floor(new Vector3(20f, 1f, 20f), climbable: false);
                object[] arguments = ClimbableArguments(scene, Vector3.up, Vector3.zero, 1f, false);
                Assert.That((bool)Invoke(scene.State, "IsPositionClimbable", arguments), Is.False, "marked not climbable");
                Assert.That(((RaycastHit)arguments[5]).collider, Is.SameAs(floor));

                Get<IDictionary>(scene.Group, "m_colliderToCharacterCollisionData").Clear();
                Assert.That((bool)Invoke(scene.State, "IsPositionClimbable", ClimbableArguments(scene, Vector3.up, Vector3.zero, 1f, false)), Is.False, "unknown to the track manager");

                BoxCollider climbable = scene.Floor(new Vector3(20f, 1f, 20f));
                floor.enabled = false;
                Physics.SyncTransforms();
                arguments = ClimbableArguments(scene, Vector3.up, Vector3.zero, 1f, false);
                Assert.That((bool)Invoke(scene.State, "IsPositionClimbable", arguments), Is.True);
                Assert.That(((RaycastHit)arguments[5]).collider, Is.SameAs(climbable));
            }
        }

        // The cast starts snapDistance along the contact normal from the character and reaches twice that far
        // (0xE6EC93, 0xE6EDD3). The floor is 0.5 below the character, so it is 0.5 + snap from the start and in reach
        // exactly when snap >= 0.5: hit at snaps 1 and 0.6, missed at 0.4.
        [Test]
        public void CastLengthIsTwiceTheSnapDistanceFromAboveTheCharacter()
        {
            using (var scene = new ClimbScene())
            {
                scene.Character.SetWorldPosition(Origin + new Vector3(0f, 0.5f, 0f));
                scene.Floor(new Vector3(20f, 1f, 20f));
                Assert.That((bool)Invoke(scene.State, "IsPositionClimbable", ClimbableArguments(scene, Vector3.up, Vector3.zero, 1f, false)), Is.True);
                Assert.That((bool)Invoke(scene.State, "IsPositionClimbable", ClimbableArguments(scene, Vector3.up, Vector3.zero, 0.6f, false)), Is.True);
                Assert.That((bool)Invoke(scene.State, "IsPositionClimbable", ClimbableArguments(scene, Vector3.up, Vector3.zero, 0.4f, false)), Is.False);
            }
        }

        // The local offset is rotated by the character's rotation before it moves the cast (0xE6ED3B..0xE6ED56) and a
        // zero offset skips the rotation. A floor 0.1 wide in x: an offset of 5 along local z stays on it for a
        // character facing +z but leaves it for one facing +x.
        [Test]
        public void TheLocalOffsetIsRotatedWithTheCharacter()
        {
            using (var scene = new ClimbScene())
            {
                scene.Floor(new Vector3(0.1f, 1f, 20f));
                Assert.That((bool)Invoke(scene.State, "IsPositionClimbable", ClimbableArguments(scene, Vector3.up, new Vector3(0f, 0f, 5f), 0.5f, false)), Is.True);
                Assert.That((bool)Invoke(scene.State, "IsPositionClimbable", ClimbableArguments(scene, Vector3.up, new Vector3(5f, 0f, 0f), 0.5f, false)), Is.False);

                scene.Character.SetWorldRotation(Quaternion.Euler(0f, 90f, 0f));
                Assert.That((bool)Invoke(scene.State, "IsPositionClimbable", ClimbableArguments(scene, Vector3.up, new Vector3(0f, 0f, 5f), 0.5f, false)), Is.False);
                Assert.That((bool)Invoke(scene.State, "IsPositionClimbable", ClimbableArguments(scene, Vector3.up, new Vector3(5f, 0f, 0f), 0.5f, false)), Is.True);
            }
        }

        // With rotation limited, a surface whose normal differs from the contact normal by more than MaxAngleChange
        // is skipped (0xE6EFDE: reject when MaxAngleChangeCosine > dot, 0xE6EFE8). The wall faces -z; the contact normal
        // is 15 degrees off it, so the dot is cos 15 = 0.966: a 10 degree limit (cos 0.985) rejects, a 45 degree limit
        // does not. Gravity along -y makes the wall's normal valid.
        [Test]
        public void LimitedRotationRejectsAnAngleChangeBeyondTheMaximum()
        {
            using (var scene = new ClimbScene())
            {
                scene.Character.GravityVector = new Vector3(0f, -9.81f, 0f);
                scene.Surface("wall", Origin + new Vector3(0f, 0.2f, 1f), new Vector3(20f, 20f, 1f));
                Vector3 contact = new Vector3(0f, Mathf.Sin(15f * Mathf.Deg2Rad), -Mathf.Cos(15f * Mathf.Deg2Rad));

                scene.Definition.MaxAngleChange = 10f;
                scene.Definition.CalculateCachedValues();
                Assert.That((bool)Invoke(scene.State, "IsPositionClimbable", ClimbableArguments(scene, contact, Vector3.zero, 1f, true)), Is.False, "10 degrees, limited");
                Assert.That((bool)Invoke(scene.State, "IsPositionClimbable", ClimbableArguments(scene, contact, Vector3.zero, 1f, false)), Is.True, "10 degrees, unlimited");

                scene.Definition.MaxAngleChange = 45f;
                scene.Definition.CalculateCachedValues();
                Assert.That((bool)Invoke(scene.State, "IsPositionClimbable", ClimbableArguments(scene, contact, Vector3.zero, 1f, true)), Is.True, "45 degrees, limited");
            }
        }

        // 0600154e (0xE6E740): without a climbable position the result is false and the normal zero.
        [Test]
        public void SnappingWithoutAClimbablePositionFailsWithAZeroNormal()
        {
            using (var scene = new ClimbScene())
            {
                object[] arguments = { scene.Character, scene.Ability, 1f, Vector3.up, new Vector3(9f, 9f, 9f), 0f, false };
                Assert.That((bool)Invoke(scene.State, "SnapPositionToSurface", arguments), Is.False);
                Assert.That((Vector3)arguments[4], Is.EqualTo(Vector3.zero));
                Assert.That(scene.Storage.GetCollection().ContainsKey(ActorFSMKeys.ClimbingLastCollider), Is.False);
            }
        }

        // A snap onto a climbable wall: the normal is the hit's, the character is placed 0.0001 off the surface along
        // it (0xE6EB05..0xE6EB28), the collider is remembered (0xE6E9F6), and an unchanged normal leaves the
        // rotation alone (0xE6EB76). The wall faces -z at z = 0.5 from the character.
        [Test]
        public void SnappingOntoAWallPlacesTheCharacterJustOffTheSurface()
        {
            using (var scene = new ClimbScene())
            {
                scene.Character.GravityVector = new Vector3(0f, -9.81f, 0f);
                BoxCollider wall = scene.Surface("wall", Origin + new Vector3(0f, 0.2f, 1f), new Vector3(20f, 20f, 1f));
                object[] arguments = { scene.Character, scene.Ability, 1f, new Vector3(0f, 0f, -1f), null, 0f, false };
                Assert.That((bool)Invoke(scene.State, "SnapPositionToSurface", arguments), Is.True);
                AreClose(new Vector3(0f, 0f, -1f), (Vector3)arguments[4], 1e-5f, "normal");
                AreClose(Origin + new Vector3(0f, 0.2f, 0.5001f), scene.Character.WorldPosition, 2.5e-5f, "just off the surface");
                Assert.That(scene.Storage.GetValueOnly<Collider>(ActorFSMKeys.ClimbingLastCollider), Is.SameAs(wall));
                Assert.That(Quaternion.Angle(scene.Character.WorldRotation, Quaternion.identity), Is.LessThan(0.001f), "same normal: no turn");
            }
        }

        // 0xE6EBA9..0xE6EBF6: a changed normal turns the character to the surface, by the whole angle when the
        // rotation is not limited and by deltaTime times the definition's RotationSpeed (0xE6EBD7) when it is.
        [Test]
        public void SnappingTurnsTheCharacterToTheSurfaceByTheLimitedOrFullAngle()
        {
            using (var scene = new ClimbScene())
            {
                scene.Character.GravityVector = new Vector3(0f, -9.81f, 0f);
                scene.Surface("wall", Origin + new Vector3(0f, 0.2f, 1f), new Vector3(20f, 20f, 1f));
                Vector3 contact = new Vector3(0f, Mathf.Sin(15f * Mathf.Deg2Rad), -Mathf.Cos(15f * Mathf.Deg2Rad));

                scene.Definition.RotationSpeed = 360f;
                object[] limited = { scene.Character, scene.Ability, 1f, contact, null, 0.01f, true };
                Assert.That((bool)Invoke(scene.State, "SnapPositionToSurface", limited), Is.True);
                Assert.That(Vector3.Angle(scene.Character.UpDirection, Vector3.up), Is.EqualTo(3.6f).Within(0.05f), "0.01 s at 360 degrees per second");

                scene.Character.SetWorldRotation(Quaternion.identity);
                object[] full = { scene.Character, scene.Ability, 1f, contact, null, 0.01f, false };
                Assert.That((bool)Invoke(scene.State, "SnapPositionToSurface", full), Is.True);
                AreClose(new Vector3(0f, 0f, -1f), scene.Character.UpDirection, 1e-4f, "up follows the wall normal");
            }
        }

        // ---- Velocity ----------------------------------------------------------------------------

        // 0600154c (0xE6D960): with the whole step on a climbable surface the velocity is the lateral and forward
        // speeds after one update of the ability (acceleration 10 over 0.1 s towards the input maxima 5 and 4).
        [Test]
        public void VelocityFollowsTheInputWhenTheWholeStepIsClimbable()
        {
            using (var scene = new ClimbScene())
            {
                scene.Floor(new Vector3(20f, 1f, 20f));
                SetCharacterProperty(scene.Character, "ControllerMovement", new Vector2(1f, 1f));
                Invoke(scene.State, "UpdateVelocity", scene.Character, scene.Ability, 0.1f);
                AreClose(new Vector3(1f, 0f, 1f), scene.Character.LocalVelocity, 1e-4f, "full step");
            }
        }

        // The step falls back from the whole step to the forward part only (0xE6E3F4, lateral speed dropped), then to
        // the lateral part only (0xE6E458, forward speed dropped), then to standing still (0xE6E4B3).
        [Test]
        public void VelocityFallsBackToTheForwardThenTheLateralThenNoStep()
        {
            using (var scene = new ClimbScene())
            {
                SetCharacterProperty(scene.Character, "ControllerMovement", new Vector2(1f, 1f));

                BoxCollider narrowInX = scene.Floor(new Vector3(0.1f, 1f, 20f));
                Invoke(scene.State, "UpdateVelocity", scene.Character, scene.Ability, 0.1f);
                AreClose(new Vector3(0f, 0f, 1f), scene.Character.LocalVelocity, 1e-4f, "the lateral step leaves the surface");

                Get<IDictionary>(scene.Group, "m_colliderToCharacterCollisionData").Clear();
                narrowInX.enabled = false;
                scene.Character.SetLocalVelocity(Vector3.zero);
                BoxCollider narrowInZ = scene.Floor(new Vector3(20f, 1f, 0.1f));
                Invoke(scene.State, "UpdateVelocity", scene.Character, scene.Ability, 0.1f);
                AreClose(new Vector3(1f, 0f, 0f), scene.Character.LocalVelocity, 1e-4f, "only the lateral step stays on it");

                Get<IDictionary>(scene.Group, "m_colliderToCharacterCollisionData").Clear();
                narrowInZ.enabled = false;
                scene.Character.SetLocalVelocity(Vector3.zero);
                scene.Floor(new Vector3(0.1f, 1f, 0.1f));
                Invoke(scene.State, "UpdateVelocity", scene.Character, scene.Ability, 0.1f);
                AreClose(Vector3.zero, scene.Character.LocalVelocity, 1e-4f, "no step stays on it");
            }
        }

        private static void SetCharacterProperty(Character character, string property, object value) =>
            typeof(Character).GetProperty(property, Members).GetSetMethod(true).Invoke(character, new[] { value });

        // 0xE6DB8E..0xE6E264: the velocity into the average non-mountable edge normal is removed before the speeds
        // are updated, and the normals are then emptied. Local velocity (2,0,3) against an edge normal (1,0,0) keeps
        // (0,0,3); the ability then decelerates z by acceleration 10 * 0.1 to 2 and, with no input, x stays 0 (an
        // unprojected x of 2 decelerates to 1, which is the control).
        [Test]
        public void VelocityIntoAnEdgeNormalIsRemovedAndTheNormalsAreCleared()
        {
            using (var scene = new ClimbScene())
            {
                scene.Floor(new Vector3(20f, 1f, 20f));
                scene.Character.SetLocalVelocity(new Vector3(2f, 0f, 3f));
                scene.EdgeNormals.Add(new Vector3(1f, 0f, 0f));
                Invoke(scene.State, "UpdateVelocity", scene.Character, scene.Ability, 0.1f);
                AreClose(new Vector3(0f, 0f, 2f), scene.Character.LocalVelocity, 1e-4f, "projected");
                Assert.That(scene.EdgeNormals, Is.Empty);

                scene.Character.SetLocalVelocity(new Vector3(2f, 0f, 3f));
                Invoke(scene.State, "UpdateVelocity", scene.Character, scene.Ability, 0.1f);
                AreClose(new Vector3(1f, 0f, 2f), scene.Character.LocalVelocity, 1e-4f, "control: no edge normals, no projection");
            }
        }

        // ---- Animation ---------------------------------------------------------------------------

        // 06001555 (0xE6F5E0): the climb speeds are the local x clamped to the lateral maximum and the local z as
        // fractions of their maxima, the vertical one over the upward maximum for a non-negative speed and the downward
        // one otherwise (setb at 0xE6F64E). Maxima: lateral 5 (clamp), upward 4, downward 2.
        [Test]
        public void ClimbSpeedParametersAreTheLocalSpeedsOverTheirMaxima()
        {
            using (var scene = new ClimbScene())
            {
                var animator = scene.Objects.Create("animator").AddComponent<Animator>();
                var controller = scene.Objects.Own(new AnimatorController());
                controller.AddLayer("Base");
                controller.AddParameter("ClimbX", AnimatorControllerParameterType.Float);
                controller.AddParameter("ClimbY", AnimatorControllerParameterType.Float);
                animator.runtimeAnimatorController = controller;
                animator.Rebind();
                animator.Update(0f);
                SetActorProperty(scene.Character, "Animator", new ActorAnimator(animator));
                var definition = scene.Objects.Own(ScriptableObject.CreateInstance<ActorAnimationDefinition>());
                Set(definition, "m_parameters", new[]
                {
                    new ActorAnimationDefinition.AnimationParameter { AnimationType = ActorAnimationType.ClimbSpeedX, ParameterName = Parameter("ClimbX") },
                    new ActorAnimationDefinition.AnimationParameter { AnimationType = ActorAnimationType.ClimbSpeedY, ParameterName = Parameter("ClimbY") },
                });
                Call(definition, "RebuildAnimationParameters");
                Set(scene.State, "m_stateAnimationDefinition", definition);

                foreach ((Vector3 local, float x, float y) in new[]
                {
                    (new Vector3(6f, 0f, 10f), 1f, 2.5f), (new Vector3(-10f, 0f, -4f), -1f, -2f), (new Vector3(2f, 0f, 0f), 0.4f, 0f),
                    (new Vector3(-2.5f, 0f, 2f), -0.5f, 0.5f),
                })
                {
                    SetActorProperty(scene.Character, "LocalVelocity", local);
                    Invoke(scene.State, "UpdateAnimation", scene.Character, scene.Definition);
                    Assert.That(animator.GetFloat("ClimbX"), Is.EqualTo(x).Within(1e-5f), "x of " + local);
                    Assert.That(animator.GetFloat("ClimbY"), Is.EqualTo(y).Within(1e-5f), "y of " + local);
                }
            }
        }

        // ---- State bodies, witnessed on the compiled IL -----------------------------------------

        // Declaration-shape witness (not a behaviour test; a full ability stack with camera and stamina is not
        // buildable here). Entry 0xE6C6A0 runs the ability base, empties or creates the edge-normal list, updates the
        // jump angle, snaps (failure snapshots and flags the edge, 0xE6CA07), orients, snapshots and sets the initial
        // velocity; leaving 0xE6CCC0 removes six climbing records, empties the edge normals and the jump angle before
        // the leave time is stored last; the update 0xE6D470 restores a passed edge before snapping, then snapshots,
        // moves and animates, or flags/finishes the climb.
        [Test]
        public void ClimbingBodiesCallInTheNativeOrder()
        {
            List<string> enter = Calls(typeof(CharacterState_Climbing<,>), "DoOnEnter");
            Assert.That(enter.IndexOf("CharacterState_Ability`1.DoOnEnter"), Is.EqualTo(0));
            string snap = "CharacterState_Climbing`2.SnapPositionToSurface";
            string snapshot = "CharacterState_Climbing`2.SnapshotPosition";
            Assert.That(new[]
            {
                enter.IndexOf("CharacterAbility_Climbing`1.UpdateJumpAngleExtra"), enter.IndexOf(snap), enter.IndexOf(snapshot),
                enter.IndexOf("CharacterState_Climbing`2.FinishClimbing"), enter.IndexOf("CharacterState_Climbing`2.OrientateToSurface"),
                enter.LastIndexOf(snapshot), enter.IndexOf("CharacterState_Climbing`2.SetInitialVelocity"),
            }, Is.Ordered.Ascending.And.Unique, string.Join(", ", enter));
            Assert.That(enter.Count(call => call == snapshot), Is.EqualTo(2));
            Assert.That(enter, Does.Contain("ICollection`1.Clear"));

            List<string> leave = Calls(typeof(CharacterState_Climbing<,>), "DoOnLeave");
            Assert.That(leave.Count(call => call == "IGraphStorage.RemoveValue"), Is.EqualTo(6), string.Join(", ", leave));
            Assert.That(new[]
            {
                leave.IndexOf("CharacterState_Ability`1.DoOnLeave"), leave.IndexOf("IGraphStorage.RemoveValue"), leave.LastIndexOf("IGraphStorage.RemoveValue"),
                leave.IndexOf("ICollection`1.Clear"), leave.IndexOf("CharacterAbility_Climbing`1.UpdateJumpAngleExtra"),
                leave.IndexOf("TimeExtensions.GetTotalTime"), leave.LastIndexOf("IGraphStorage.SetValue"),
            }, Is.Ordered.Ascending.And.Unique, string.Join(", ", leave));

            List<string> update = Calls(typeof(CharacterState_Climbing<,>), "DoUpdate");
            string[] updateOrder =
            {
                "CharacterState_Ability`1.DoUpdate", "CharacterState_Ability`1.UpdateRemainTimer", "CharacterState_Climbing`2.RestorePosition",
                "CharacterState_Climbing`2.GetSnapDistanceOnUpdate", "CharacterState_Climbing`2.SnapPositionToSurface", "CharacterState_Climbing`2.SnapshotPosition",
                "CharacterState_Climbing`2.UpdateVelocity", "CharacterState_Climbing`2.UpdateAnimation", "CharacterState_Climbing`2.FinishClimbing",
            };
            Assert.That(updateOrder.Select(call => update.IndexOf(call)).ToArray(), Is.Ordered.Ascending.And.Unique, string.Join(", ", update));
        }

        // Entry 0x7761C0 spends the boost stamina for the definition's ability type and boost definition after the
        // climbing entry; leaving 0x776290 reads the boost definition before testing the trigger, then writes the suspend timer.
        [Test]
        public void ClimbingBoostBodiesCallInTheNativeOrder()
        {
            List<string> enter = Calls(typeof(CharacterState_ClimbingBoost), "DoOnEnter");
            Assert.That(new[]
            {
                enter.IndexOf("CharacterState_Climbing`2.DoOnEnter"), enter.IndexOf("CharacterStamina.Activate"),
            }, Is.Ordered.Ascending.And.Unique, string.Join(", ", enter));
            Assert.That(enter, Does.Contain("Character.get_BoostStamina"));

            List<string> leave = Calls(typeof(CharacterState_ClimbingBoost), "DoOnLeave");
            Assert.That(new[]
            {
                leave.IndexOf("CharacterState_Climbing`2.DoOnLeave"), leave.IndexOf("CharacterAbility_MovementClimbingBoost.get_IsTriggered"),
                leave.IndexOf("CharacterStamina.set_SuspendTimer"),
            }, Is.Ordered.Ascending.And.Unique, string.Join(", ", leave));
        }
    }
}
