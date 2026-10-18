using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime TargetTrackingUtilities (0x02000a33, x86-64 0x6B7B10/0x6B7C40) and
    // its TargetValidDelegate (0x02000a34) over real HomingTargets, whose WorldPosition is their
    // BoxCollider's bounds centre. Each target's constructor caches the CharacterManager reference,
    // so that row is leased. The Editor may send OnValidate while a target is added, which reads
    // TimeScaledComponent_SDT's s_config cache, so an owned empty lookup stands in for it.
    public sealed class OriginalTargetTrackingUtilitiesTests
    {
        private static readonly FieldInfo TimeConfig = typeof(TimeScaledComponent_SDT).GetField("s_config_Internal", BindingFlags.Static | BindingFlags.NonPublic);

        private sealed class Targets
        {
            private readonly Owned m_owned;
            public Targets(Owned owned) { m_owned = owned; }

            public HomingTarget At(float x, float y, float z)
            {
                var position = new Vector3(x, y, z);
                GameObject host = m_owned.Host("Lucid homing target " + position);
                host.transform.position = position;
                var collider = host.AddComponent<BoxCollider>();
                var target = host.AddComponent<HomingTarget>();
                Set(target, "m_collider", collider);
                Assert.That(Vector3.Distance(target.WorldPosition, position), Is.LessThan(1e-5f), "the bounds centre is the target position");
                return target;
            }
        }

        private static void WithTargets(Action<Targets> body) => Owned.Run(owned =>
        {
            owned.LeaseRow<CharacterManager>();
            object prior = TimeConfig.GetValue(null);
            try
            {
                TimeConfig.SetValue(null, owned.Track(ScriptableObject.CreateInstance<TimeCategoryLookup>()));
                body(new Targets(owned));
            }
            finally
            {
                TimeConfig.SetValue(null, prior);
            }
        });

        // A 45 degree cone along +z from the origin.
        private static ConeVolume.ConeDescription Cone(float distance, float step) => new ConeVolume.ConeDescription
        {
            Origin = Vector3.zero, Rotation = Quaternion.identity, Distance = distance, Step = step, Tangents = Vector2.one
        };

        private static bool Project(ConeVolume.ConeDescription cone, List<HomingTarget> targets, ref List<HomingTarget> valid,
            out HomingTarget best, TargetTrackingUtilities.TargetValidDelegate filter = null) =>
            TargetTrackingUtilities.ProjectToTargetedObject(cone, targets, Vector3.forward, Quaternion.identity, ref valid, out best, filter);

        // Metadata of both TypeDefs. Both methods take bestTarget as out and an optional delegate
        // whose default has no Constant row (null).
        [Test]
        public void DeclarationsKeepTheOutTargetAndOptionalDelegate()
        {
            AssertMembers(typeof(TargetTrackingUtilities), 0x00100181, new (string, int)[0],
                new[] { ("ProjectToTargetedObject", 0x0096), ("ProjectStepToTargetedObject", 0x0091) });
            AssertIl2CppOptions(typeof(TargetTrackingUtilities), 2, 1);
            foreach (string name in new[] { "ProjectToTargetedObject", "ProjectStepToTargetedObject" })
            {
                ParameterInfo[] parameters = typeof(TargetTrackingUtilities).GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetParameters();
                Assert.That(parameters.Select(parameter => parameter.Name), Is.EqualTo(new[]
                    { "cone", "targets", "forward", "worldToLocal", "validTargets", "bestTarget", "targetValidDelegate" }), name);
                Assert.That(parameters[4].ParameterType.IsByRef && !parameters[4].IsOut, Is.True, name + " validTargets is ref");
                Assert.That((int)parameters[5].Attributes, Is.EqualTo(0x0002), name + " bestTarget is out");
                Assert.That((int)parameters[6].Attributes, Is.EqualTo(0x1010), name);
                Assert.That(parameters[6].DefaultValue, Is.Null, name);
            }

            Type validator = typeof(TargetTrackingUtilities.TargetValidDelegate);
            AssertMembers(validator, 0x00000102, new (string, int)[0],
                new[] { (".ctor", 0x1886), ("Invoke", 0x01c6), ("BeginInvoke", 0x01c6), ("EndInvoke", 0x01c6) });
            Assert.That(validator.BaseType, Is.EqualTo(typeof(MulticastDelegate)));
            MethodInfo invoke = validator.GetMethod("Invoke");
            Assert.That((invoke.ReturnType, invoke.GetParameters().Single().ParameterType), Is.EqualTo((typeof(bool), typeof(HomingTarget))));
        }

        // ProjectToTargetedObject 0x6B7B10 clears the list, then grows the cone by Step
        // (Mathf.Min with Distance, minss at 0x6b7bb2) until a step succeeds. A step
        // (0x6B7C40) keeps a target whose depth along forward (Dot at 0x6b7e17) is within
        // [0, Distance] and whose position lies in the cone centred on its projected point with
        // that depth as length (0x6b7e61, xmm5 = depth). Here the near target is first found at
        // 5, so the far one never joins the list; the wide one would pass a test sized by the
        // step distance instead of its depth.
        [Test]
        public void TheConeGrowsByStepUntilATargetIsFound()
        {
            WithTargets(targets =>
            {
                HomingTarget behind = targets.At(0f, 0f, -1f);
                HomingTarget wide = targets.At(2.4f, 0f, 2f);
                HomingTarget far = targets.At(0f, 0f, 6f);
                HomingTarget near = targets.At(0.5f, 0f, 3f);
                HomingTarget beyond = targets.At(0f, 0f, 12f);
                var list = new List<HomingTarget> { behind, wide, far, near, beyond };
                var valid = new List<HomingTarget> { beyond };
                List<HomingTarget> passed = valid;

                Assert.That(Project(Cone(10f, 2.5f), list, ref valid, out HomingTarget best), Is.True);
                Assert.That(valid, Is.SameAs(passed));
                Assert.That(valid, Is.EqualTo(new[] { near }));
                Assert.That(best, Is.SameAs(near));

                Assert.That(Project(Cone(10f, 10f), list, ref valid, out best), Is.True);
                Assert.That(valid, Is.EqualTo(new[] { far, near }), "one full-length step keeps both");
                Assert.That(best, Is.SameAs(near));
            });
        }

        // The best target has the smallest squared offset from the line plus squared depth
        // (0x6b7ea3..0x6b7f00); a later equal score replaces it (ja skips only a larger one,
        // 0x6b7f08). A delegate returning false skips the target before AddUnique (0x6b7e9e),
        // null entries are skipped (op_Equality 0x6b7d9f) and repeats are listed once.
        [Test]
        public void TheClosestValidTargetWinsAndALaterTieReplacesIt()
        {
            WithTargets(targets =>
            {
                HomingTarget onAxis = targets.At(0f, 0f, 3f);
                HomingTarget offAxis = targets.At(2f, 0f, 2.5f);
                HomingTarget firstTwin = targets.At(0f, 0f, 4f);
                HomingTarget secondTwin = targets.At(0f, 0f, 4f);
                HomingTarget atEnd = targets.At(0f, 0f, 10f);
                var valid = new List<HomingTarget>();
                ConeVolume.ConeDescription cone = Cone(10f, 10f);

                Assert.That(Project(cone, new List<HomingTarget> { offAxis, null, onAxis, onAxis }, ref valid, out HomingTarget best), Is.True);
                Assert.That(valid, Is.EqualTo(new[] { offAxis, onAxis }));
                Assert.That(best, Is.SameAs(onAxis));

                Assert.That(Project(cone, new List<HomingTarget> { firstTwin, secondTwin }, ref valid, out best), Is.True);
                Assert.That(best, Is.SameAs(secondTwin));

                var asked = new List<HomingTarget>();
                Assert.That(Project(cone, new List<HomingTarget> { offAxis, onAxis }, ref valid, out best, target =>
                {
                    asked.Add(target);
                    return target != onAxis;
                }), Is.True);
                Assert.That(asked, Is.EqualTo(new[] { offAxis, onAxis }));
                Assert.That(valid, Is.EqualTo(new[] { offAxis }));
                Assert.That(best, Is.SameAs(offAxis));

                Assert.That(Project(cone, new List<HomingTarget> { atEnd }, ref valid, out best), Is.True, "a depth equal to the distance is inside");
                Assert.That(best, Is.SameAs(atEnd));
            });
        }

        // With nothing valid every step fails and 0x6b7c10 stores a null best target. A zero
        // distance never enters the loop, yet the list is still cleared.
        [Test]
        public void NoValidTargetClearsTheListAndReturnsNull()
        {
            WithTargets(targets =>
            {
                HomingTarget behind = targets.At(0f, 0f, -1f);
                HomingTarget beyond = targets.At(0f, 0f, 12f);
                var valid = new List<HomingTarget> { behind };
                Assert.That(Project(Cone(10f, 2.5f), new List<HomingTarget> { behind, beyond }, ref valid, out HomingTarget best), Is.False);
                Assert.That(best, Is.Null);
                Assert.That(valid, Is.Empty);

                valid.Add(beyond);
                var rejected = new List<HomingTarget>();
                Assert.That(Project(Cone(0f, 1f), new List<HomingTarget> { behind }, ref valid, out best, target => { rejected.Add(target); return true; }), Is.False);
                Assert.That((best, valid.Count, rejected.Count), Is.EqualTo(((HomingTarget)null, 0, 0)));
            });
        }
    }
}
