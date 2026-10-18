using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // HardlightProject.OutOfBoundsArea (TypeDef 0x02000aca), OutOfBoundsTest (0x02000ace) with its
    // iterator <CheckCharacterPosition>d__6 (0x02000acf), OutOfBoundsBox (0x02000acb) and
    // OutOfBoundsPlane (0x02000acc), against the 1.10.1 x86-64 bodies at 0x6E8680..0x6E9000.
    // Characters come from AddComponent<Character> (EditMode runs no Awake) with a recording
    // storage; the CharacterManager is an uninitialised instance reached through a reference made
    // for the case, with only its validity flag and current character set. Rows that field
    // initialisers create are leased and released idle.
    public sealed class OriginalOutOfBoundsTests
    {
        private const string NoEnemyScript = "Game object is in the enemy layer but contains no enemy script.";

        // Records every storage access by key name before forwarding to a real FSMStorage.
        private sealed class RecordingStorage : IGraphStorage
        {
            private readonly FSMStorage m_inner = new FSMStorage(0);
            public readonly List<string> Calls = new List<string>();

            private static string Name(GraphStorageKey key)
            {
                if (key.Equals(ActorFSMKeys.IsOutOfBounds)) return "IsOutOfBounds";
                if (key.Equals(ActorFSMKeys.TriggerDyingState)) return "TriggerDyingState";
                if (key.Equals(ActorFSMKeys.RespawnTeleportRequired)) return "RespawnTeleportRequired";
                if (key.Equals(ActorFSMKeys.RespawnEffectsComplete)) return "RespawnEffectsComplete";
                return key.ToString();
            }

            public void Initialise() => m_inner.Initialise();
            public void Clear() => m_inner.Clear();

            public void SetValue<T>(GraphStorageKey storageKey, T value)
            {
                Calls.Add("set " + Name(storageKey) + "=" + value);
                m_inner.SetValue(storageKey, value);
            }

            public T GetValue<T>(GraphStorageKey storageKey, T defaultValue = default, bool storeDefault = true)
            {
                Calls.Add("get " + Name(storageKey) + " default=" + defaultValue + " store=" + storeDefault);
                return m_inner.GetValue(storageKey, defaultValue, storeDefault);
            }

            public void RemoveValue<T>(GraphStorageKey storageKey) => m_inner.RemoveValue<T>(storageKey);
            public IReadOnlyDictionary<GraphStorageKey, object> GetCollection() => m_inner.GetCollection();

            // Seeds a value without recording it.
            public void Seed(GraphStorageKey key, bool value) => m_inner.SetValue(key, value);
        }

        private sealed class Scene
        {
            public Character Character;
            public RecordingStorage Storage;
            public CharacterManager Manager;
            public BoxCollider CharacterCollider;

            public SystemRef<CharacterManager> Reference() =>
                new SystemRef<CharacterManager>(ProcessManager.GetDefaultName<CharacterManager>(), Manager);

            public void SetValid(bool valid) => Set(Manager, "<CharacterValid>k__BackingField", valid);
        }

        // Character's field initialiser creates the empty GameplayIslandManager row.
        private static Scene NewScene(Owned owned)
        {
            owned.LeaseRow<GameplayIslandManager>();
            var scene = new Scene();
            GameObject host = owned.Host("Lucid out-of-bounds character");
            scene.CharacterCollider = host.AddComponent<BoxCollider>();
            scene.Character = host.AddComponent<Character>();
            scene.Storage = new RecordingStorage();
            Set(scene.Character, "<Storage>k__BackingField", scene.Storage);
            Set(scene.Character, "m_colliderCollision", scene.CharacterCollider);
            scene.Manager = (CharacterManager)FormatterServices.GetUninitializedObject(typeof(CharacterManager));
            Set(scene.Manager, "m_currentCharacter", scene.Character);
            scene.SetValid(true);
            return scene;
        }

        private static OutOfBoundsArea NewArea(Owned owned)
        {
            owned.LeaseRow<CharacterManager>();
            GameObject host = owned.Host("Lucid out-of-bounds area");
            host.AddComponent<BoxCollider>();
            return host.AddComponent<OutOfBoundsArea>();
        }

        [Test]
        public void DeclarationsKeepTheOriginalFlagsMembersAndAttributes()
        {
            AssertMembers(typeof(OutOfBoundsArea), 0x00100001,
                new[] { ("m_triggerInstantDeath", 0x0001), ("m_triggerRingLoss", 0x0001), ("m_enemyLayer", 0x0001), ("m_characterManagerRef", 0x0001) },
                new[] { ("Start", 0x0081), ("OnTriggerEnter", 0x0081), ("ManualTriggerEnter", 0x01e6), ("SetEnemyOutOfBounds", 0x0091),
                        ("SetCharacterOutOfBounds", 0x0081), (".ctor", 0x1886), ("HardlightProject.ITriggerCollider.GetComponent", 0x01e1) });
            Assert.That(WorldMemberAccess.AttributeRow(typeof(OutOfBoundsArea)), Is.EqualTo(
                "RequireComponent(Collider) Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)"));
            Assert.That(typeof(OutOfBoundsArea).GetInterfaces(), Is.EqualTo(new[] { typeof(ITriggerCollider) }));
            var explicitGetComponent = typeof(OutOfBoundsArea).GetMethod("HardlightProject.ITriggerCollider.GetComponent", Declared);
            Assert.That(explicitGetComponent.GetGenericArguments().Single().GetGenericParameterConstraints(), Is.Empty);

            AssertMembers(typeof(OutOfBoundsTest), 0x00100081,
                new[] { ("m_characterManagerRef", 0x0001), ("m_positionCheck", 0x0001), ("m_characterManager", 0x0001) },
                new[] { ("OnEnable", 0x0081), ("OnDisable", 0x0081), ("Init", 0x0081), ("CheckCharacterPosition", 0x0081),
                        ("PositionIsValid", 0x05c4), (".ctor", 0x1884) });
            AssertIl2CppOptions(typeof(OutOfBoundsTest), 1, 2);
            Type iterator = typeof(OutOfBoundsTest).GetNestedType("<CheckCharacterPosition>d__6", Declared);
            Assert.That(iterator, Is.Not.Null, "Roslyn's iterator ordinal 6 follows the three fields");
            Assert.That((int)iterator.Attributes, Is.EqualTo(0x00100103));
            Assert.That(iterator.GetFields(Declared).OrderBy(field => field.MetadataToken).Select(field => (field.Name, (int)field.Attributes)),
                Is.EqualTo(new[] { ("<>1__state", 0x0001), ("<>2__current", 0x0001), ("<>4__this", 0x0006) }));

            AssertMembers(typeof(OutOfBoundsBox), 0x00100001, new[] { ("m_boundingBox", 0x0001) },
                new[] { ("PositionIsValid", 0x00c4), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(OutOfBoundsBox), 1, 2);
            AssertMembers(typeof(OutOfBoundsPlane), 0x00100001, new[] { ("m_axis", 0x0001), ("m_minimumValue", 0x0001) },
                new[] { ("PositionIsValid", 0x00c4), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(OutOfBoundsPlane), 1, 2);
            Type axis = typeof(OutOfBoundsPlane).GetNestedType("Axis", Declared);
            Assert.That((int)axis.Attributes, Is.EqualTo(0x00000103));
            Assert.That(Enum.GetNames(axis).Zip(Enum.GetValues(axis).Cast<object>().Select(Convert.ToInt32), (name, value) => name + "=" + value),
                Is.EqualTo(new[] { "X=0", "Y=1", "Z=2" }));
        }

        // .ctor 0x6E8880 writes 0x101 to +0x20 (both flags true) and caches
        // GetSystemRef<CharacterManager>(null, true); Start 0x6E8680 stores NameToLayer("Enemy").
        // OutOfBoundsPlane .ctor 0x6E89C0 writes 0xC000000000000001: axis Y (1), minimum -2f.
        [Test]
        public void ConstructorsAndStartStoreTheNativeDefaults()
        {
            Owned.Run(owned =>
            {
                OutOfBoundsArea area = NewArea(owned);
                Assert.That(Get<bool>(area, "m_triggerInstantDeath"), Is.True);
                Assert.That(Get<bool>(area, "m_triggerRingLoss"), Is.True);
                Assert.That(Get<SystemRef<CharacterManager>>(area, "m_characterManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<CharacterManager>()));
                Set(area, "m_enemyLayer", 12345);
                Call(area, "Start");
                Assert.That(Get<int>(area, "m_enemyLayer"), Is.EqualTo(LayerMask.NameToLayer("Enemy")));

                var plane = owned.Host("Lucid out-of-bounds plane").AddComponent<OutOfBoundsPlane>();
                Assert.That(Convert.ToInt32(Get<object>(plane, "m_axis")), Is.EqualTo(1));
                Assert.That(Get<float>(plane, "m_minimumValue"), Is.EqualTo(-2f));
                Assert.That(Get<object>(plane, "m_characterManagerRef"), Is.Null, "OutOfBoundsTest .ctor 0x6E8930 only runs the base");
                var box = owned.Host("Lucid out-of-bounds box").AddComponent<OutOfBoundsBox>();
                Assert.That(Get<Bounds>(box, "m_boundingBox"), Is.EqualTo(default(Bounds)));
            });
        }

        // PositionIsValid 0x6E8940 picks x (0), the movshdup y (1) or the passed z (2), then
        // ucomiss component, minimum with setae: valid iff component >= minimum and ordered, so the
        // minimum itself is valid and NaN on either side is not. Any other axis raises
        // IndexOutOfRangeException("Invalid Vector3 index!"), the inlined Vector3 indexer.
        [Test]
        public void PlaneTruthTableUsesTheChosenAxisInclusivelyAndRejectsNaN()
        {
            Owned.Run(owned =>
            {
                var plane = owned.Host("Lucid out-of-bounds plane").AddComponent<OutOfBoundsPlane>();
                Type axisType = typeof(OutOfBoundsPlane).GetNestedType("Axis", Declared);
                var rows = new (int axis, Vector3 position, float minimum, bool valid)[]
                {
                    (0, new Vector3(1f, -50f, -50f), 1f, true),
                    (0, new Vector3(0.999f, 50f, 50f), 1f, false),
                    (1, new Vector3(-50f, 1f, -50f), 1f, true),
                    (1, new Vector3(50f, 0.999f, 50f), 1f, false),
                    (2, new Vector3(-50f, -50f, 1f), 1f, true),
                    (2, new Vector3(50f, 50f, 0.999f), 1f, false),
                    (1, new Vector3(0f, float.NaN, 0f), -1f, false),
                    (1, new Vector3(0f, 5f, 0f), float.NaN, false),
                    (1, new Vector3(0f, float.PositiveInfinity, 0f), float.PositiveInfinity, true),
                };
                foreach (var row in rows)
                {
                    Set(plane, "m_axis", Enum.ToObject(axisType, row.axis));
                    Set(plane, "m_minimumValue", row.minimum);
                    Assert.That((bool)Call(plane, "PositionIsValid", row.position), Is.EqualTo(row.valid), row.axis + " " + row.position.ToString("R") + " >= " + row.minimum);
                }
                Set(plane, "m_axis", Enum.ToObject(axisType, 3));
                var thrown = Assert.Throws<IndexOutOfRangeException>(() => Call(plane, "PositionIsValid", Vector3.zero));
                Assert.That(thrown.Message, Is.EqualTo("Invalid Vector3 index!"));
            });
        }

        // PositionIsValid 0x6E8910 tail-calls Bounds.Contains on +0x38 with the position.
        [Test]
        public void BoxAcceptsOnlyPositionsTheBoundsContain()
        {
            Owned.Run(owned =>
            {
                var box = owned.Host("Lucid out-of-bounds box").AddComponent<OutOfBoundsBox>();
                Set(box, "m_boundingBox", new Bounds(new Vector3(10f, 0f, 0f), new Vector3(2f, 2f, 2f)));
                Assert.That((bool)Call(box, "PositionIsValid", new Vector3(10.5f, -0.5f, 0.5f)), Is.True);
                Assert.That((bool)Call(box, "PositionIsValid", new Vector3(11.5f, 0f, 0f)), Is.False);
                Assert.That((bool)Call(box, "PositionIsValid", Vector3.zero), Is.False, "the box is in world space, not around this transform");
            });
        }

        // OnTriggerEnter 0x6E86C0: no work while the reference IsNull; otherwise
        // IsCurrentCharacterColliderCollision(other), then TryGetCurrentCharacter, then the inlined
        // SetCharacterOutOfBounds tail into Character.TriggerOutOfBounds. With TriggerDyingState
        // seeded, TriggerOutOfBounds stops after its two default-storing reads, which therefore
        // mark exactly the calls that reached it.
        [Test]
        public void TriggerEnterReachesTheCharacterOnlyThroughItsCollisionCollider()
        {
            Owned.Run(owned =>
            {
                Scene scene = NewScene(owned);
                scene.Storage.Seed(ActorFSMKeys.TriggerDyingState, true);
                OutOfBoundsArea area = NewArea(owned);
                Collider stranger = owned.Host("Lucid stranger").AddComponent<BoxCollider>();
                var reached = new[] { "get IsOutOfBounds default=False store=True", "get TriggerDyingState default=False store=True" };

                Call(area, "OnTriggerEnter", scene.CharacterCollider);
                Assert.That(scene.Storage.Calls, Is.Empty, "the leased reference is null");

                Set(area, "m_characterManagerRef", scene.Reference());
                Call(area, "OnTriggerEnter", stranger);
                Assert.That(scene.Storage.Calls, Is.Empty, "another collider");
                scene.SetValid(false);
                Call(area, "OnTriggerEnter", scene.CharacterCollider);
                Assert.That(scene.Storage.Calls, Is.Empty, "no valid current character");
                scene.SetValid(true);
                Call(area, "OnTriggerEnter", scene.CharacterCollider);
                Assert.That(scene.Storage.Calls, Is.EqualTo(reached));

                scene.Storage.Calls.Clear();
                Call(area, "SetCharacterOutOfBounds", scene.Character);
                Assert.That(scene.Storage.Calls, Is.EqualTo(reached), "SetCharacterOutOfBounds 0x6E8770 tail-calls TriggerOutOfBounds");
            });
        }

        // OnTriggerEnter (0x6E8754/0x6E8758) and SetCharacterOutOfBounds (0x6E8777/0x6E877B) pass
        // m_triggerInstantDeath (+0x20) as instantDeath and m_triggerRingLoss (+0x21) as
        // triggerRingLoss. Past its two gates TriggerOutOfBounds 0x734BD0 tests triggerRingLoss
        // first (0x734DA9), then instantDeath (0x734E6C), whose TriggerHardFailure 0x7346C0 first
        // stores TriggerDyingState true and then fails in EnableGameInput on the empty InputSystem
        // row. Swapped arguments would take the ring-loss branch and never record that store.
        [Test]
        public void InstantDeathFlagReachesTheHardFailureBeforeTheRingLossFlag()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<InputSystem>();
                Scene scene = NewScene(owned);
                OutOfBoundsArea area = NewArea(owned);
                Set(area, "m_characterManagerRef", scene.Reference());
                Set(area, "m_triggerInstantDeath", true);
                Set(area, "m_triggerRingLoss", false);
                var hardFailure = new[]
                {
                    "get IsOutOfBounds default=False store=True",
                    "get TriggerDyingState default=False store=True",
                    "set TriggerDyingState=True",
                };

                Assert.Catch(() => Call(area, "SetCharacterOutOfBounds", scene.Character), "EnableGameInput needs an InputSystem");
                Assert.That(scene.Storage.Calls, Is.EqualTo(hardFailure), "SetCharacterOutOfBounds");

                scene.Storage.Seed(ActorFSMKeys.TriggerDyingState, false);
                scene.Storage.Calls.Clear();
                Assert.Catch(() => Call(area, "OnTriggerEnter", scene.CharacterCollider), "EnableGameInput needs an InputSystem");
                Assert.That(scene.Storage.Calls, Is.EqualTo(hardFailure), "OnTriggerEnter");
            });
        }

        // ManualTriggerEnter 0x6E8790 compares other.gameObject.layer with m_enemyLayer and only then
        // tail-calls SetEnemyOutOfBounds 0x6E87D0, which calls Enemy.ReceiveHit(null) when
        // TryGetComponent<Enemy> finds one. With no hit effect, ReceiveHit completes the hit at once,
        // which marks the enemy dead. A character collider is never sent out of bounds here. With no
        // Enemy, HLOutput.LogError returns early because the leased HLUnityCore row is empty.
        [Test]
        public void ManualEnterHitsOnlyEnemiesOnTheEnemyLayer()
        {
            Owned.Run(owned =>
            {
                Scene scene = NewScene(owned);
                OutOfBoundsArea area = NewArea(owned);
                Set(area, "m_characterManagerRef", scene.Reference());
                owned.LeaseRow<EnemyManager>();
                owned.LeaseRow<HLUnityCore>();
                const int enemyLayer = 9;
                Set(area, "m_enemyLayer", enemyLayer);

                GameObject enemyHost = owned.Host("Lucid out-of-bounds enemy");
                Collider enemyCollider = enemyHost.AddComponent<BoxCollider>();
                Enemy enemy = enemyHost.AddComponent<Enemy>();
                Set(enemy, "<Storage>k__BackingField", new FSMStorage(0));

                area.ManualTriggerEnter(enemyCollider);
                Assert.That(enemy.IsDead, Is.False, "default layer 0 is not the enemy layer");
                enemyHost.layer = enemyLayer;
                area.ManualTriggerEnter(enemyCollider);
                Assert.That(enemy.IsDead, Is.True);
                Assert.That(enemy.IsDying, Is.False);

                scene.Character.gameObject.layer = enemyLayer;
                scene.Storage.Seed(ActorFSMKeys.TriggerDyingState, true);
                Assert.DoesNotThrow(() => area.ManualTriggerEnter(scene.CharacterCollider), NoEnemyScript);
                Assert.That(scene.Storage.Calls, Is.Empty, "ManualTriggerEnter never reaches TriggerOutOfBounds");
            });
        }

        // The explicit ITriggerCollider.GetComponent<T> (shared 0x9857F0) forwards to the
        // component's own GetComponent<T> through its RGCTX method.
        [Test]
        public void TriggerColliderGetComponentForwardsToTheGameObject()
        {
            Owned.Run(owned =>
            {
                OutOfBoundsArea area = NewArea(owned);
                ITriggerCollider trigger = area;
                Assert.That(trigger.GetComponent<BoxCollider>(), Is.SameAs(area.GetComponent<BoxCollider>()));
                Assert.That(trigger.GetComponent<OutOfBoundsArea>(), Is.SameAs(area));
                Assert.That(trigger.GetComponent<Rigidbody>(), Is.Null);
            });
        }

        // MoveNext 0x6E8C20 resumes every state 0..2 at the loop head. Without a valid character, or
        // with a valid position (vtable slot 4), it yields null as state 2. Otherwise it reads
        // RespawnTeleportRequired (+0x280) and RespawnEffectsComplete (+0x2A0) with storeDefault
        // false, ORs both results (0x6E8E90), and yields as state 1 when either is set; only when
        // neither is set does it store IsOutOfBounds (+0x240) true and yield as state 2.
        [Test]
        public void PositionCheckMarksTheCharacterOutOfBoundsUnlessARespawnIsUnderWay()
        {
            Owned.Run(owned =>
            {
                Scene scene = NewScene(owned);
                var plane = owned.Host("Lucid out-of-bounds plane").AddComponent<OutOfBoundsPlane>();
                Set(plane, "m_characterManager", scene.Manager);
                var check = (IEnumerator)Call(plane, "CheckCharacterPosition");
                Assert.That(Get<object>(check, "<>4__this"), Is.SameAs(plane));
                Assert.That(Get<int>(check, "<>1__state"), Is.EqualTo(0));
                var reads = new[] { "get RespawnTeleportRequired default=False store=False", "get RespawnEffectsComplete default=False store=False" };

                void Step(Vector3 position, int state, string[] calls, string label)
                {
                    Set(scene.Character, "<WorldPosition>k__BackingField", position);
                    scene.Storage.Calls.Clear();
                    Assert.That(check.MoveNext(), Is.True, label);
                    Assert.That(check.Current, Is.Null, label);
                    Assert.That(Get<int>(check, "<>1__state"), Is.EqualTo(state), label);
                    Assert.That(scene.Storage.Calls, Is.EqualTo(calls), label);
                }

                Vector3 below = new Vector3(0f, -3f, 0f);
                Step(new Vector3(0f, -2f, 0f), 2, new string[0], "a valid position");
                scene.SetValid(false);
                Step(below, 2, new string[0], "no valid character");
                scene.SetValid(true);
                scene.Storage.Seed(ActorFSMKeys.RespawnTeleportRequired, true);
                Step(below, 1, reads, "teleport required; the second flag is still read");
                scene.Storage.Seed(ActorFSMKeys.RespawnTeleportRequired, false);
                scene.Storage.Seed(ActorFSMKeys.RespawnEffectsComplete, true);
                Step(below, 1, reads, "respawn effects complete");
                scene.Storage.Seed(ActorFSMKeys.RespawnEffectsComplete, false);
                Step(below, 2, reads.Concat(new[] { "set IsOutOfBounds=True" }).ToArray(), "out of bounds");

                Assert.Throws<NotSupportedException>(() => check.Reset(), "Reset 0x6E8FC0");
                ((IDisposable)check).Dispose();
                Assert.That(Get<int>(check, "<>1__state"), Is.EqualTo(2), "Dispose 0x6E8C10 is a bare RET");
            });
        }

        // OnEnable 0x6E89E0 replaces the reference with GetSystemRef<CharacterManager>(null, true)
        // and passes Init to InvokeOnValid, which defers it while the row is empty. Init 0x6E8AF0
        // stores the manager before starting the iterator. OnDisable 0x6E8AD0 stops only a stored
        // coroutine. The fixture clears the deferred work it caused before the row is released.
        [Test]
        public void EnableDefersInitUntilTheCharacterManagerIsValid()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<CharacterManager>();
                var plane = owned.Host("Lucid out-of-bounds plane").AddComponent<OutOfBoundsPlane>();
                SystemRef<CharacterManager> reference = ProcessManager.GetSystemRef<CharacterManager>();
                try
                {
                    Call(plane, "OnEnable");
                    Assert.That(Get<SystemRef<CharacterManager>>(plane, "m_characterManagerRef"), Is.SameAs(reference));
                    List<Action<CharacterManager>> deferred = Get<FastAction<CharacterManager>>(reference, "m_actionOnSystemValid").GetInvocationList();
                    Assert.That(deferred.Select(action => (action.Target, action.Method.Name)), Is.EqualTo(new[] { ((object)plane, "Init") }));
                }
                finally
                {
                    Set(reference, "m_actionOnSystemValid", null);
                }
                Assert.That(Get<object>(plane, "m_characterManager"), Is.Null, "Init has not run");
                Assert.DoesNotThrow(() => Call(plane, "OnDisable"), "no coroutine is stored");
            });
        }
    }
}
