using System.Collections.Generic;
using System.Reflection;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Dreamteck.Splines.SplineUser in play mode, where Awake caches Application.isPlaying so that Rebuild queues and
    // Update/LateUpdate/FixedUpdate run the queued build as in the shipped player build (the editor compile rebuilds at once
    // while not playing; that branch is covered by OriginalDreamteckSplineUserTests). A synchronous test passes no frame, so the
    // engine's own callbacks do not run in between; they are invoked by hand. The computer holds the Linear polyline
    // (0,0,0) -> (10,0,0) -> (10,0,10) (cached samples at percents 0, 0.5, 1). TearDown destroys everything the test made.
    public sealed class OriginalDreamteckSplineUserPlayModeTests
    {
        private readonly List<GameObject> _owned = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            foreach (GameObject owned in _owned)
            {
                if (owned != null)
                {
                    Object.DestroyImmediate(owned);
                }
            }

            _owned.Clear();
        }

        private SplineComputer NewLine()
        {
            GameObject owner = new GameObject("line");
            _owned.Add(owner);
            SplineComputer computer = owner.AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            computer.SetPoints(new[]
            {
                new SplinePoint(new Vector3(0f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 10f))
            });
            computer.RebuildImmediate();
            return computer;
        }

        private SplineUser NewUser(SplineComputer computer = null)
        {
            GameObject owner = new GameObject("user");
            _owned.Add(owner);
            SplineUser user = owner.AddComponent<SplineUser>();
            if (computer != null)
            {
                user.spline = computer;
            }

            return user;
        }

        private static void Call(object target, string method)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(info, Is.Not.Null, method);
            info.Invoke(target, null);
        }

        // Update/LateUpdate/FixedUpdate 0x4cf060/0x4cf210/0x4cf260 only run when updateMethod matches; RunUpdate 0x4cf0b0 builds
        // and then raises onPostBuild in the same call.
        [Test]
        public void UpdateMethodSelectsTheCallbackThatBuilds()
        {
            string[] callbacks = { "Update", "FixedUpdate", "LateUpdate" };
            SplineUser.UpdateMethod[] methods = { SplineUser.UpdateMethod.Update, SplineUser.UpdateMethod.FixedUpdate, SplineUser.UpdateMethod.LateUpdate };
            SplineComputer computer = NewLine();
            for (int callback = 0; callback < callbacks.Length; callback++)
            {
                for (int method = 0; method < methods.Length; method++)
                {
                    SplineUser user = NewUser(computer);
                    user.updateMethod = methods[method];
                    int builds = 0;
                    user.onPostBuild += delegate { builds++; };
                    Call(user, callbacks[callback]);
                    Assert.That(builds, Is.EqualTo(callback == method ? 1 : 0), callbacks[callback] + " for " + methods[method]);
                    Assert.That(user.sampleCount, Is.EqualTo(callback == method ? 3 : 0));
                }
            }
        }

        // Rebuild 0x4cef30 and autoUpdate 0x4ce3a0: Rebuild only queues while autoUpdate is on; switching it on queues one.
        [Test]
        public void AutoUpdateGatesTheQueuedRebuild()
        {
            SplineUser user = NewUser(NewLine());
            int builds = 0;
            user.onPostBuild += delegate { builds++; };
            Call(user, "Update");
            Assert.That(builds, Is.EqualTo(1));
            user.autoUpdate = false;
            user.Rebuild();
            Call(user, "Update");
            Assert.That(builds, Is.EqualTo(1), "Rebuild is ignored without autoUpdate");
            user.autoUpdate = true;
            Call(user, "Update");
            Assert.That(builds, Is.EqualTo(2));
            Call(user, "Update");
            Assert.That(builds, Is.EqualTo(2), "nothing is queued any more");
        }
    }
}
