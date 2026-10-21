using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cinemachine;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.BossAbilityTestSupport;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The cutscene stand-ins for bosses (Game.Runtime 02000445 SubstituteObjectBoss, 02000446 SubstituteObjectBossFourman, 02000447
    // SubstituteObjectBossFourmen; x86-64 0x7A52B0..0x7A59E0) and the boss camera target (0200067a TimedBossCameraTarget; 0x583430..
    // 0x5835A0). The substitutes are real components on owned inactive hosts; their OnEnable and OnDisable are called directly, as Edit
    // Mode sends neither. The bosses are real Boss components listed on a registered BossManager, found by their BossDefinition.
    // Not covered: the substitute positions and rotations of the base SubstituteObject, which have their own tests.
    public sealed class BossSubstituteObjectTests
    {
        private static readonly Regex ActorDefinitionSettingsFault =
            new Regex("^NullReferenceException: Object reference not set to an instance of an object$");

        // CreateInstance sends Awake and OnEnable, and both reach ActorDefinition.UpdateCachedValues, which faults on the unassigned
        // Settings: two logged exceptions each, as the other boss fixtures expect.
        private static BossDefinition NewBossDefinition(Owned owned)
        {
            for (int i = 0; i < 2; i++)
                LogAssert.Expect(LogType.Exception, ActorDefinitionSettingsFault);
            var definition = owned.Track(ScriptableObject.CreateInstance<BossDefinition>());
            definition.Traits = new BossTraits();
            return definition;
        }

        // Bosses 0..count-1 listed on a registered BossManager. The definition is the one the substitutes look for, except for the boss
        // at index 0 when decoy is set, which has a definition of its own. Every boss host is active, so hiding is observable.
        private static void WithBosses(int count, bool decoy, Action<Owned, BossDefinition, List<Boss>> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<SaveManager>(); // BossManager's field initialiser caches a typed SaveManager reference
            BossManager manager = owned.Register(owned.Host("Lucid substitute boss manager", false).AddComponent<BossManager>());
            BossDefinition definition = NewBossDefinition(owned);
            BossDefinition other = decoy ? NewBossDefinition(owned) : definition;
            var bosses = new List<Boss>();
            for (int i = 0; i < count; i++)
            {
                Boss boss = NewBoss(owned, new Vector3(i * 10f, 0f, 0f));
                boss.gameObject.SetActive(true);
                Set(boss, "m_definition", i == 0 ? other : definition); // serialized, no setter
                var root = owned.Host("Lucid substitute boss animation root " + i, true, boss.transform);
                Set(boss, "m_animationRoot", root.transform);
                var proxy = owned.Host("Lucid substitute boss proxy " + i).AddComponent<SmoothedTransformProxy>();
                SetActorProperty(boss, "VisualProxy", proxy);
                manager.Bosses.Add(boss);
                bosses.Add(boss);
            }
            body(owned, definition, bosses);
        });

        private static SubstituteObjectBoss NewSubstitute<T>(Owned owned, BossDefinition definition) where T : SubstituteObjectBoss
        {
            T substitute = owned.Host("Lucid substitute object boss", false).AddComponent<T>();
            Set(substitute, "m_bossDefinition", definition);
            return substitute;
        }

        private static void Enable(Component substitute) => Call(substitute, "OnEnable");

        private static void Disable(Component substitute) => Call(substitute, "OnDisable");

        // ---- SubstituteObjectBoss ----

        // 0x7A52B0: the boss with the definition replaces the real object: its visual proxy is hidden, the substitute remembers the
        // boss's game object and animation root, and the base then hides the boss itself.
        [Test]
        public void EnablingSubstitutesTheBossWithTheDefinitionAndHidesItsVisualProxy()
        {
            WithBosses(2, true, (owned, definition, bosses) =>
            {
                SubstituteObjectBoss substitute = NewSubstitute<SubstituteObjectBoss>(owned, definition);

                Enable(substitute);

                Boss boss = bosses[1];
                Assert.That(Get<Boss>(substitute, "m_replacedBoss"), Is.SameAs(boss), "the boss at index 0 has another definition");
                Assert.That(boss.VisualProxy.gameObject.activeSelf, Is.False);
                Assert.That(Get<GameObject>(substitute, "m_replacedObject"), Is.SameAs(boss.gameObject));
                Assert.That(Get<Transform>(substitute, "m_realAnimationRoot"), Is.SameAs(boss.AnimationRoot));
                Assert.That(boss.gameObject.activeSelf, Is.False, "the base hides the real object");
                Assert.That(bosses[0].gameObject.activeSelf, Is.True, "the other boss is untouched");
            });
        }

        // 0x7A5314: a boss that is already chosen is kept (the lookup only runs while the reference is null).
        [Test]
        public void EnablingKeepsABossThatIsAlreadyChosen()
        {
            WithBosses(2, true, (owned, definition, bosses) =>
            {
                SubstituteObjectBoss substitute = NewSubstitute<SubstituteObjectBoss>(owned, definition);
                Set(substitute, "m_replacedBoss", bosses[0]);

                Enable(substitute);

                Assert.That(Get<Boss>(substitute, "m_replacedBoss"), Is.SameAs(bosses[0]));
                Assert.That(Get<GameObject>(substitute, "m_replacedObject"), Is.SameAs(bosses[0].gameObject));
                Assert.That(bosses[0].VisualProxy.gameObject.activeSelf, Is.False);
                Assert.That(bosses[1].VisualProxy.gameObject.activeSelf, Is.True);
            });
        }

        // 0x7A5326: a boss without a visual proxy is replaced all the same.
        [Test]
        public void EnablingToleratesABossWithoutAVisualProxy()
        {
            WithBosses(2, true, (owned, definition, bosses) =>
            {
                SetActorProperty(bosses[1], "VisualProxy", null);
                SubstituteObjectBoss substitute = NewSubstitute<SubstituteObjectBoss>(owned, definition);

                Enable(substitute);

                Assert.That(Get<Boss>(substitute, "m_replacedBoss"), Is.SameAs(bosses[1]));
                Assert.That(Get<GameObject>(substitute, "m_replacedObject"), Is.SameAs(bosses[1].gameObject));
            });
        }

        // A ground collider whose top face is at the height; its centre sits half a unit below.
        private static void AddGround(Owned owned, float topHeight, string name)
        {
            GameObject ground = owned.Host(name);
            ground.transform.position = new Vector3(3f, topHeight - 0.5f, 5f);
            ground.AddComponent<BoxCollider>().size = new Vector3(20f, 1f, 20f);
        }

        // A collider whose underside is at the height.
        private static void AddCeiling(Owned owned, float undersideHeight)
        {
            GameObject ceiling = owned.Host("Lucid substitute boss ceiling");
            ceiling.transform.position = new Vector3(3f, undersideHeight + 0.5f, 5f);
            ceiling.AddComponent<BoxCollider>().size = new Vector3(20f, 1f, 20f);
        }

        // 0x7A53B0..0x7A55FB: disabling shows the boss and its proxy and animation root again (base), then looks for the ground along the
        // boss's down direction as far as (height + 50) * 2 from where the real object was, moving the object to the hit height and
        // keeping its horizontal position. Boss at (3,10,5): the reach is 120. A ground 10 below is found; one 110 below (top at -100)
        // is within the reach; one 135 below (top at -125) is out of it and nothing moves. With no ground below, the up direction is
        // tried: a ceiling at 12 moves the object to 12. Finally the boss resumes with zero velocity.
        [Test]
        public void DisablingPlacesTheBossOnTheGroundWithinTheNativeReach()
        {
            foreach ((string label, float groundTop, float ceilingUnderside, float expectedHeight) in new[]
            {
                ("ground below", 0f, float.NaN, 0f),
                ("ground at the edge of the reach", -100f, float.NaN, -100f),
                ("ground beyond the reach", -125f, float.NaN, 10f),
                ("ceiling above only", float.NaN, 12f, 12f),
                ("nothing", float.NaN, float.NaN, 10f),
            })
            {
                WithBosses(2, true, (owned, definition, bosses) =>
                {
                    SubstituteObjectBoss substitute = NewSubstitute<SubstituteObjectBoss>(owned, definition);
                    Enable(substitute);
                    Boss boss = bosses[1];
                    boss.transform.position = new Vector3(3f, 10f, 5f);
                    if (!float.IsNaN(groundTop)) AddGround(owned, groundTop, "Lucid substitute boss ground");
                    if (!float.IsNaN(ceilingUnderside)) AddCeiling(owned, ceilingUnderside);
                    Physics.SyncTransforms();
                    boss.SetWorldVelocity(new Vector3(1f, 2f, 3f));

                    Disable(substitute);

                    Assert.That(boss.gameObject.activeSelf, Is.True, label + ": the boss is shown again");
                    Assert.That(boss.VisualProxy.gameObject.activeSelf, Is.True, label + ": and its proxy");
                    Assert.That(boss.AnimationRoot.gameObject.activeSelf, Is.True, label + ": and its animation root");
                    AssertNear(boss.transform.position, new Vector3(3f, expectedHeight, 5f), label, 1e-3f);
                    Assert.That(boss.WorldVelocity, Is.EqualTo(Vector3.zero), label + ": the boss resumes at rest");
                });
            }
        }

        // ---- SubstituteObjectBossFourman ----

        // 0x7A5820: this stand-in replaces the visual proxy of the boss with the definition (not the boss), with the proxy's animation root.
        [Test]
        public void FourmanEnablingSubstitutesTheVisualProxy()
        {
            WithBosses(2, true, (owned, definition, bosses) =>
            {
                SubstituteObjectBossFourman substitute = owned.Host("Lucid substitute object fourman", false).AddComponent<SubstituteObjectBossFourman>();
                Set(substitute, "m_bossDefinition", definition);
                SmoothedTransformProxy proxy = bosses[1].VisualProxy;
                Transform proxyRoot = owned.Host("Lucid fourman proxy root").transform;
                Set(proxy, "m_animationRoot", proxyRoot);

                Enable(substitute);

                Assert.That(Get<GameObject>(substitute, "m_replacedObject"), Is.SameAs(proxy.gameObject));
                Assert.That(Get<Transform>(substitute, "m_realAnimationRoot"), Is.SameAs(proxyRoot));
                Assert.That(proxy.gameObject.activeSelf, Is.False, "the base hides it");
                Assert.That(bosses[1].gameObject.activeSelf, Is.True, "the boss itself stays");
            });
        }

        // 0x7A58C6..0x7A58EE: a boss without a visual proxy leaves the substitution as it was; the base still runs on what was there.
        [Test]
        public void FourmanEnablingLeavesTheReplacedObjectAloneWithoutAVisualProxy()
        {
            WithBosses(2, true, (owned, definition, bosses) =>
            {
                SetActorProperty(bosses[1], "VisualProxy", null);
                SubstituteObjectBossFourman substitute = owned.Host("Lucid substitute object fourman", false).AddComponent<SubstituteObjectBossFourman>();
                Set(substitute, "m_bossDefinition", definition);
                GameObject standIn = owned.Host("Lucid fourman prior object");
                Set(substitute, "m_replacedObject", standIn);

                Enable(substitute);

                Assert.That(Get<GameObject>(substitute, "m_replacedObject"), Is.SameAs(standIn));
                Assert.That(Get<Transform>(substitute, "m_realAnimationRoot"), Is.Null);
                Assert.That(standIn.activeSelf, Is.False, "the base hid the prior object");
            });
        }

        // ---- SubstituteObjectBossFourmen ----

        // 0x7A5950: the boss is the one at the authored index among the bosses with the definition, replacing any boss chosen before
        // (the assignment is unconditional); an index past the last such boss assigns null, and the boss base then looks the first one up.
        [Test]
        public void FourmenEnablingPicksTheBossAtTheAuthoredIndex()
        {
            WithBosses(4, true, (owned, definition, bosses) =>
            {
                // bosses[0] has another definition; bosses 1..3 are the three Fourmen
                foreach ((int index, int expected) in new[] { (0, 1), (1, 2), (2, 3), (5, 1) })
                {
                    foreach (Boss boss in bosses)
                    {
                        boss.gameObject.SetActive(true);
                        boss.VisualProxy.gameObject.SetActive(true);
                    }
                    SubstituteObjectBossFourmen substitute = NewSubstitute<SubstituteObjectBossFourmen>(owned, definition) as SubstituteObjectBossFourmen;
                    Set(substitute, "m_fourmenIndex", index);
                    Set(substitute, "m_replacedBoss", bosses[0]);

                    Enable(substitute);

                    Assert.That(Get<Boss>(substitute, "m_replacedBoss"), Is.SameAs(bosses[expected]), "index " + index);
                    Assert.That(Get<GameObject>(substitute, "m_replacedObject"), Is.SameAs(bosses[expected].gameObject), "index " + index);
                    Assert.That(bosses[expected].VisualProxy.gameObject.activeSelf, Is.False, "index " + index);
                }
            });
        }

        // ---- TimedBossCameraTarget ----

        private sealed class CameraScene
        {
            public Boss Boss;
            public CinemachineTargetGroup Group;
            public TimedBossCameraTarget Target;
        }

        // A boss whose camera assigner feeds a real target group, and a timed target on an owned inactive host.
        private static void WithCameraTarget(Action<Owned, CameraScene> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<TimeManager>(); // the time-scaled target takes the TimeManager row
            var scene = new CameraScene();
            scene.Boss = NewBoss(owned, Vector3.zero);
            scene.Group = owned.Host("Lucid boss camera target group", false).AddComponent<CinemachineTargetGroup>();
            var assigner = owned.Host("Lucid boss camera target assigner", false).AddComponent<CinemachineTargetGroupAssigner_Boss>();
            Set(assigner, "m_targetGroup", scene.Group); // serialized
            Set(scene.Boss, "m_cinemachineTargetGroupAssigner", assigner); // serialized
            scene.Target = EnemyAbilityTestSupport.AddTimeScaled<TimedBossCameraTarget>(owned.Host("Lucid timed boss camera target", false));
            body(owned, scene);
        });

        // 0x583430: the damageable is taken as the boss; the target joins the boss's camera group (weight and radius of one, the
        // assigner's authored values), and the timed lifetime starts as the base would start it.
        [Test]
        public void InitialiseJoinsTheBossCameraGroupAndStartsTheLifetime()
        {
            WithCameraTarget((owned, scene) =>
            {
                scene.Target.Initialise(scene.Boss, 7f);

                Assert.That(Get<Boss>(scene.Target, "m_boss"), Is.SameAs(scene.Boss));
                int member = scene.Group.FindMember(scene.Target.transform);
                Assert.That(member, Is.GreaterThanOrEqualTo(0), "the camera looks at the target");
                Assert.That(scene.Group.m_Targets[member].weight, Is.EqualTo(1f));
                Assert.That(scene.Group.m_Targets[member].radius, Is.EqualTo(1f));
                Assert.That(scene.Target.m_initialised, Is.True);
                Assert.That(Get<float>(scene.Target, "m_maxLifetime"), Is.EqualTo(7f));
            });
        }

        // 0x583520: disabling leaves the boss's camera group; a boss that no longer exists (Unity equality) is not asked to, and a target
        // that was never initialised has nobody to leave.
        [Test]
        public void DisablingLeavesTheCameraGroupUnlessTheBossIsGone()
        {
            WithCameraTarget((owned, scene) =>
            {
                scene.Target.Initialise(scene.Boss, 7f);
                Disable(scene.Target);
                Assert.That(scene.Group.FindMember(scene.Target.transform), Is.LessThan(0), "the target left the group");
            });

            WithCameraTarget((owned, scene) =>
            {
                scene.Target.Initialise(scene.Boss, 7f);
                UnityEngine.Object.DestroyImmediate(scene.Boss.gameObject);
                Disable(scene.Target);
                Assert.That(scene.Group.FindMember(scene.Target.transform), Is.GreaterThanOrEqualTo(0), "a destroyed boss is not asked to remove it");
            });

            WithCameraTarget((owned, scene) =>
            {
                Assert.DoesNotThrow(() => Disable(scene.Target));
                Assert.That(scene.Group.m_Targets.Length, Is.Zero);
            });
        }
    }
}
