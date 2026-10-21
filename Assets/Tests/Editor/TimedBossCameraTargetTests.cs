using HardlightProject;
using Cinemachine;
using Hardlight;
using NUnit.Framework;
using System;
using UnityEngine;
using static ProjectLucid.Tests.BossAbilityTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The boss camera target (Game.Runtime 0200067a TimedBossCameraTarget; x86-64 0x583430..0x5835A0). The target is a real component on an
    // owned inactive host; Edit Mode sends no OnDisable, so it is called directly.
    public sealed class TimedBossCameraTargetTests
    {
        private static void Disable(Component target) => Call(target, "OnDisable");

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
