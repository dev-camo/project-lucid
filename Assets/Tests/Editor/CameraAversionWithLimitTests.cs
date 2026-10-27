using System;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CameraManagerClientSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CameraAversionWithLimit (TypeDef 0x02000256, x86-64 0x711C60..0x711F4F). The component is
    // a real TimeScaledComponent_SDT on an owned inactive child of a scaled, offset parent, built while an owned empty
    // time-category lookup is cached; the camera manager is a real system registered before Awake caches the reference.
    // OnEnable is not run, because it subscribes to the TimeManager; the output camera it would capture is taken by
    // calling its lambda with the manager, as the manager's startup callback does.
    public sealed class CameraAversionWithLimitTests
    {
        private sealed class Case
        {
            internal CameraAversionWithLimit Component;
            internal Transform Parent;
            internal Transform Child;
            internal Rig Rig;
        }

        // The parent sits at (10, 20, 30) with scale 2 and the child at local (1, 2, 3), so its rest position in the
        // world is (10 + 2, 20 + 4, 30 + 6) = (12, 24, 36).
        private static void WithComponent(Action<Owned, Case> body) => Owned.Run(owned =>
        {
            using (var time = new OwnedTimeScaledObjects("lucid-aversion"))
            {
                var scene = new Case { Rig = NewRig(owned, true, true) };
                scene.Parent = owned.Host("Lucid aversion parent", false).transform;
                scene.Parent.position = new Vector3(10f, 20f, 30f);
                scene.Parent.localScale = new Vector3(2f, 2f, 2f);
                scene.Child = owned.Host("Lucid aversion child", false, scene.Parent).transform;
                scene.Child.localPosition = new Vector3(1f, 2f, 3f);
                scene.Component = TimeScaledReferenceLease.Add<CameraAversionWithLimit>(scene.Child.gameObject);
                Call(scene.Component, "Awake");
                body(owned, scene);
            }
        });

        // The camera looks along (1, 2, 2) / 3, so a radius of 3 moves the position by exactly (1, 2, 2).
        private static void Look(Case scene, float radius, bool x, bool y, bool z)
        {
            scene.Rig.Brain.transform.rotation = Quaternion.LookRotation(new Vector3(1f, 2f, 2f));
            Set(scene.Component, "m_orbitRadius", radius);
            Set(scene.Component, "m_affectX", x);
            Set(scene.Component, "m_affectY", y);
            Set(scene.Component, "m_affectZ", z);
        }

        private static void AssertPosition(Vector3 expected, Vector3 actual, string label)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1e-4f), label + " x");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1e-4f), label + " y");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(1e-4f), label + " z");
        }

        // The constructor 0x711F00 stores 0x40000000 (2.0f) at +0x50 before the base constructor; no axis is affected.
        [Test]
        public void ADefaultComponentOrbitsAtRadiusTwoOnNoAxis()
        {
            WithComponent((owned, scene) =>
            {
                Assert.That(Get<float>(scene.Component, "m_orbitRadius"), Is.EqualTo(2f));
                Assert.That((Get<bool>(scene.Component, "m_affectX"), Get<bool>(scene.Component, "m_affectY"), Get<bool>(scene.Component, "m_affectZ")),
                    Is.EqualTo((false, false, false)));
            });
        }

        // The startup lambda 0x711F20 stores the manager's brain output camera (CinemachineBrain.get_OutputCamera on the
        // manager's +0x58 field).
        [Test]
        public void TheStartupCallbackCapturesTheBrainOutputCamera()
        {
            WithComponent((owned, scene) =>
            {
                Assert.That(Get<Camera>(scene.Component, "m_camera"), Is.Null);

                Call(scene.Component, "<OnEnable>b__10_0", scene.Rig.Manager);

                Assert.That(Get<Camera>(scene.Component, "m_camera"), Is.SameAs(scene.Rig.Camera));
            });
        }

        // InternalUpdate 0x711DC0 returns at once without a camera (0x711e0d..0x711e1e). Otherwise it takes the rest
        // position from the parent's TransformPoint of the local position captured in Awake (0x711e43..0x711e5d) and adds
        // the camera forward times the radius per enabled axis; an axis that is not enabled keeps the rest value (the
        // blends at 0x711e85 and 0x711ea5, and the -0.0f added at 0x711ebe for z). The result is the world position.
        [Test]
        public void UpdateWithoutACameraLeavesThePositionAlone()
        {
            WithComponent((owned, scene) =>
            {
                scene.Child.position = new Vector3(7f, 7f, 7f);
                Look(scene, 3f, true, true, true);

                Call(scene.Component, "InternalUpdate", 0.02f);

                AssertPosition(new Vector3(7f, 7f, 7f), scene.Child.position, "no camera");
            });
        }

        [Test]
        public void UpdateMovesFromTheCapturedRestPositionAlongTheCameraForwardOnTheEnabledAxes()
        {
            WithComponent((owned, scene) =>
            {
                Call(scene.Component, "<OnEnable>b__10_0", scene.Rig.Manager);
                var rest = new Vector3(12f, 24f, 36f);
                var offset = new Vector3(1f, 2f, 2f);

                for (int mask = 0; mask < 8; mask++)
                {
                    bool x = (mask & 1) != 0, y = (mask & 2) != 0, z = (mask & 4) != 0;
                    Look(scene, 3f, x, y, z);
                    // Moved away, to show that the position is rebuilt from the rest position and not from where it stands.
                    scene.Child.position = new Vector3(100f, 200f, 300f);

                    Call(scene.Component, "InternalUpdate", 0.02f);

                    var expected = new Vector3(rest.x + (x ? offset.x : 0f), rest.y + (y ? offset.y : 0f), rest.z + (z ? offset.z : 0f));
                    AssertPosition(expected, scene.Child.position, "axes " + (x ? "X" : "") + (y ? "Y" : "") + (z ? "Z" : "") + " (mask " + mask + ")");
                }
            });
        }

        // The rest position uses the local position stored in Awake, not the one at update time.
        [Test]
        public void TheRestPositionIsTheLocalPositionCapturedInAwake()
        {
            WithComponent((owned, scene) =>
            {
                Call(scene.Component, "<OnEnable>b__10_0", scene.Rig.Manager);
                scene.Child.localPosition = new Vector3(9f, 9f, 9f);
                Look(scene, 0f, true, true, true);

                Call(scene.Component, "InternalUpdate", 0.02f);

                AssertPosition(new Vector3(12f, 24f, 36f), scene.Child.position, "zero radius returns to the captured rest position");
            });
        }
    }
}
