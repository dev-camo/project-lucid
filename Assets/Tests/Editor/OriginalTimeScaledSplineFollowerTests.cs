using System;
using System.Collections;
using System.Reflection;
using Dreamteck.Splines;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime TimeScaledSplineFollower (TypeDef 0x02000801, x86-64 0x5FB790..0x5FBBD0). Components are
    // real, on owned inactive hosts that never wake. The TimeManager is a real component holding the Environment
    // category at scale 2, registered into its row; the follower is added with an owned empty category lookup
    // in place (its editor Reset/OnValidate read the TimeScaledComponent_SDT lookup), then given the manager's
    // category object. Followers and managers are never updated by the engine.
    public sealed class OriginalTimeScaledSplineFollowerTests
    {
        private const float Scale = 2f;
        private static readonly BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic;

        private sealed class Scene
        {
            public Owned Owned;
            public TimeManager Manager;
            public TimeCategoryObject Category;
            public TimeScaledSplineFollower Follower;
            public SplineFollower Spline;
        }

        // The follower component is added after the spline follower, so its editor OnValidate finds it.
        private static void WithFollower(Action<Scene> body, bool withSplineFollower = true) => Owned.Run(owned =>
        {
            // The row may already exist, idle, from an earlier case's TimeScaledComponent type initializer, so it is
            // leased rather than required absent; the manager is registered into it and unregistered before release.
            string row = ProcessManager.GetDefaultName<TimeManager>();
            owned.LeaseRow<TimeManager>();
            TimeManager manager = NewTimeManager(owned, TimeCategory.Environment, Scale);
            ProcessManager.RegisterSystem(manager, row);
            // Awake (0x1B1AE58) never runs on the inactive host; Subscribe needs the three per-phase dictionaries it adds.
            var phases = (IDictionary)Get<object>(manager, "m_updateObjects");
            var categoryLists = phases.GetType().GetGenericArguments()[1];
            foreach (UpdateOn phase in new[] { UpdateOn.FixedUpdate, UpdateOn.LateUpdate, UpdateOn.Update })
                phases.Add(phase, Activator.CreateInstance(categoryLists));
            owned.Lease(new CharacterTriggerTestSupport.ReleaseAction(() => ProcessManager.UnregisterSystem(row)));
            var lookup = (TimeCategoryLookup)typeof(TimeExtensions).GetField("s_config_Internal", Statics).GetValue(null);
            GameObject host = owned.Host("Lucid owned time scaled spline follower", false);
            SplineFollower spline = withSplineFollower ? host.AddComponent<SplineFollower>() : null;
            FieldInfo config = typeof(TimeScaledComponent_SDT).GetField("s_config_Internal", Statics);
            object prior = config.GetValue(null);
            var emptyLookup = ScriptableObject.CreateInstance<TimeCategoryLookup>();
            config.SetValue(null, emptyLookup);
            TimeScaledSplineFollower follower;
            try { follower = host.AddComponent<TimeScaledSplineFollower>(); }
            finally
            {
                config.SetValue(null, prior);
                Object.DestroyImmediate(emptyLookup);
            }
            follower.TimeCategoryObject = lookup.Dictionary[TimeCategory.Environment];
            body(new Scene { Owned = owned, Manager = manager, Category = follower.TimeCategoryObject, Follower = follower, Spline = spline });
        });

        private static float FollowSpeed(Scene scene) => Get<float>(scene.Follower, "m_followSpeed");
        private static float LastFollowSpeed(Scene scene) => Get<float>(scene.Follower, "m_lastFollowSpeed");

        private static int Subscriptions(Scene scene)
        {
            var byUpdate = (IDictionary)Get<object>(scene.Manager, "m_updateObjects");
            var byCategory = (IDictionary)byUpdate[scene.Follower.UpdateOn];
            return byCategory.Contains(scene.Category) ? ((IList)byCategory[scene.Category]).Count : 0;
        }

        // 0x5fb7b5..0x5fb7de: UnityEngine.Object.op_Inequality(m_splineFollower, null).
        [Test]
        public void CheckIsValidIsTrueOnlyWhileAFollowerIsAssigned()
        {
            WithFollower(scene =>
            {
                Assert.That(scene.Follower.CheckIsValid(), Is.True, "found by OnValidate when added");
                Object.DestroyImmediate(scene.Spline);
                Assert.That(scene.Follower.CheckIsValid(), Is.False, "a destroyed follower is Unity null");
            });
            WithFollower(scene => Assert.That(scene.Follower.CheckIsValid(), Is.False, "no follower"), withSplineFollower: false);
        }

        // 0x5fb840: base OnValidate, then (CanValidate, 0x6b1910) assigns GetComponent<SplineFollower>() only
        // while the field is null.
        [Test]
        public void OnValidateFindsTheSiblingFollowerOnlyWhileNoneIsAssigned()
        {
            WithFollower(scene =>
            {
                Assert.That(Get<object>(scene.Follower, "m_splineFollower"), Is.Null.Or.EqualTo(null));
                SplineFollower sibling = scene.Follower.gameObject.AddComponent<SplineFollower>();
                Call(scene.Follower, "OnValidate");
                Assert.That(Get<SplineFollower>(scene.Follower, "m_splineFollower"), Is.SameAs(sibling));

                SplineFollower other = scene.Owned.Host("Lucid owned other spline follower", false).AddComponent<SplineFollower>();
                Set(scene.Follower, "m_splineFollower", other);
                Call(scene.Follower, "OnValidate");
                Assert.That(Get<SplineFollower>(scene.Follower, "m_splineFollower"), Is.SameAs(other), "an assigned follower is kept");
            }, withSplineFollower: false);
        }

        // 0x5fb7f0: without a valid follower nothing happens (no base Initialise, no subscription).
        [Test]
        public void InitialiseWithoutAFollowerSubscribesNothingAndKeepsTheSpeeds()
        {
            WithFollower(scene =>
            {
                Call(scene.Follower, "Initialise", scene.Manager);
                Assert.That(Subscriptions(scene), Is.EqualTo(0));
                Assert.That((FollowSpeed(scene), LastFollowSpeed(scene)), Is.EqualTo((0f, 0f)));
            }, withSplineFollower: false);
        }

        // 0x5fb7f0: a valid follower subscribes through the base Initialise (0x5f8ca0) and both speeds take
        // the follower's current followSpeed (+0x2b8).
        [Test]
        public void InitialiseSubscribesAndCapturesTheFollowersSpeed()
        {
            WithFollower(scene =>
            {
                scene.Spline.followSpeed = 4f;
                Call(scene.Follower, "Initialise", scene.Manager);
                Assert.That(Subscriptions(scene), Is.EqualTo(1));
                Assert.That((FollowSpeed(scene), LastFollowSpeed(scene)), Is.EqualTo((4f, 4f)));
            });
        }

        // 0x5fb8f0 / 0x5fb9e0: the speed written is GetTimescale() * m_followSpeed; because it equals the last
        // written speed on the next update it is not captured again, so the scale does not compound.
        [Test]
        public void InternalUpdateScalesTheCapturedSpeedWithoutCompounding()
        {
            WithFollower(scene =>
            {
                scene.Spline.followSpeed = 4f;
                Call(scene.Follower, "Initialise", scene.Manager);
                Call(scene.Follower, "InternalUpdate", 123f);
                Assert.That(scene.Spline.followSpeed, Is.EqualTo(4f * Scale), "the delta time is not used");
                Assert.That((FollowSpeed(scene), LastFollowSpeed(scene)), Is.EqualTo((4f, 8f)));
                Call(scene.Follower, "InternalUpdate", 0f);
                Assert.That(scene.Spline.followSpeed, Is.EqualTo(4f * Scale), "a second update does not scale again");
            });
        }

        // 0x5fb9e0: a followSpeed that is not Mathf.Approximately the last written one was set elsewhere and
        // becomes the new unscaled speed.
        [Test]
        public void AnExternalSpeedChangeBecomesTheNewUnscaledSpeed()
        {
            WithFollower(scene =>
            {
                scene.Spline.followSpeed = 4f;
                Call(scene.Follower, "Initialise", scene.Manager);
                Call(scene.Follower, "InternalUpdate", 0f);
                scene.Spline.followSpeed = 3f;
                Call(scene.Follower, "InternalUpdate", 0f);
                Assert.That(FollowSpeed(scene), Is.EqualTo(3f));
                Assert.That(scene.Spline.followSpeed, Is.EqualTo(3f * Scale));
                Assert.That(LastFollowSpeed(scene), Is.EqualTo(3f * Scale));
            });
        }

        // 0x5fb9e0: a difference below the Mathf.Approximately tolerance (1e-6 relative) is not an external change.
        [Test]
        public void ASpeedWithinTheApproximateToleranceIsNotCaptured()
        {
            WithFollower(scene =>
            {
                scene.Spline.followSpeed = 4f;
                Call(scene.Follower, "Initialise", scene.Manager);
                Call(scene.Follower, "InternalUpdate", 0f);
                scene.Spline.followSpeed = 8f + 1e-7f;
                Call(scene.Follower, "InternalUpdate", 0f);
                Assert.That(FollowSpeed(scene), Is.EqualTo(4f));
                Assert.That(scene.Spline.followSpeed, Is.EqualTo(8f));
            });
        }

        // 0x5fbad0: the base OnPause (a no-op) and then the same body as the update.
        [Test]
        public void OnPauseAppliesTheTimescaleToTheFollowerSpeed()
        {
            WithFollower(scene =>
            {
                scene.Spline.followSpeed = 5f;
                Call(scene.Follower, "Initialise", scene.Manager);
                scene.Follower.OnPause();
                Assert.That(scene.Spline.followSpeed, Is.EqualTo(5f * Scale));
                Assert.That((FollowSpeed(scene), LastFollowSpeed(scene)), Is.EqualTo((5f, 10f)));
            });
        }
    }
}
