using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.WorldMemberAccess;

namespace ProjectLucid.Tests
{
    // HardlightProject.CatchConnector (TypeDef 0x02000abe, x86-64 0x6E3000..0x6E3870, constructor
    // 0x6E37E0, OnEnable lambda 0x6E3870). The connector sits on owned objects; Edit Mode runs no
    // Awake or OnEnable, so the cases call them. The field initialiser takes the typed TimeManager
    // reference, so every case leases that row; only the subscription case registers an owned
    // TimeManager into it, and unregisters it before the lease is released.
    public sealed class OriginalCatchConnectorTests
    {
        private sealed class Scene : IDisposable
        {
            private readonly VerificationFixtureOwnership.LifecycleRow m_timeRow;
            public readonly OwnedWorldObjects Objects = new OwnedWorldObjects("lucid-catch-connector");
            public readonly CatchConnector Connector;
            public readonly Transform Anchor;
            public readonly Transform Catch;

            // The connector starts at (5,5,5) turned by startYaw; Awake caches that pose and scale.
            public Scene(float startYaw = 0f, bool awake = true)
            {
                m_timeRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<TimeManager>());
                try
                {
                    GameObject host = Objects.Create("connector", new Vector3(5f, 5f, 5f), Quaternion.Euler(0f, startYaw, 0f));
                    Connector = host.AddComponent<CatchConnector>();
                    Anchor = Objects.Create("anchor", Vector3.zero, Quaternion.identity).transform;
                    Catch = Objects.Create("catch", new Vector3(0f, 3f, 0f), Quaternion.identity).transform;
                    if (awake)
                        Call(Connector, "Awake");
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                Objects.Dispose();
                m_timeRow.Dispose();
            }
        }

        // Metadata of the TypeDef: the interface, fourteen fields, then the lifecycle methods, the
        // pivot methods, OnLateUpdate, the IsPaused accessors, the constructor and the OnEnable lambda
        // (ordinal 14 follows the thirteen fields and Awake).
        [Test]
        public void DeclarationsKeepTheOriginalFlagsFieldsAndMethods()
        {
            Assert.That((int)typeof(CatchConnector).Attributes, Is.EqualTo(0x00100001));
            Assert.That(typeof(CatchConnector).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(typeof(CatchConnector).GetInterfaces(), Does.Contain(typeof(ITimeScaled)));
            Assert.That(AttributeRow(typeof(CatchConnector)), Is.EqualTo("Il2CppSetOptionAttribute(2,False) Il2CppSetOptionAttribute(1,False)"));
            Assert.That(Fields(typeof(CatchConnector)), Is.EqualTo(new[]
            {
                "m_timeCategory:TimeCategory:0001", "m_anchor:Transform:0001", "m_offset:Single:0001", "m_timeToExtend:Single:0001",
                "m_alwaysActive:Boolean:0001", "m_scale:Vector3:0001", "m_startRotation:Quaternion:0001", "m_anchorTransform:Transform:0001",
                "m_anchorBody:Rigidbody:0001", "m_catchTransform:Transform:0001", "m_catchOffset:Vector3:0001", "m_time:Single:0001",
                "m_timeManagerRef:SystemRef`1:0021", "<IsPaused>k__BackingField:Boolean:0001"
            }));
            Assert.That(Methods(typeof(CatchConnector)), Is.EqualTo(new[]
            {
                "Awake:0081", "OnEnable:0081", "OnDisable:0081", "SetPivots:0086", "ClearPivots:0086", "OnLateUpdate:01e6",
                "get_IsPaused:09e6", "set_IsPaused:09e6", ".ctor:1886", "<OnEnable>b__14_0:0081"
            }));
            const BindingFlags field = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(AttributeRow(typeof(CatchConnector).GetField("m_timeCategory", field)), Is.EqualTo(
                "TooltipAttribute(Time category to update from.) SerializeField()"));
            Assert.That(AttributeRow(typeof(CatchConnector).GetField("m_anchor", field)), Is.EqualTo(
                "SerializeField() TooltipAttribute(If set, this is used to scale connector to as the pivot.  If not set, the attach point is used.)"));
            Assert.That(AttributeRow(typeof(CatchConnector).GetField("m_timeToExtend", field)), Is.EqualTo(
                "MinAttribute(0) TooltipAttribute(Time to extend to pivot.) SerializeField()"));
            MethodInfo lambda = typeof(CatchConnector).GetMethod("<OnEnable>b__14_0", field);
            Assert.That(lambda.GetParameters()[0].ParameterType, Is.EqualTo(typeof(TimeManager)));
        }

        // Constructor 0x6E37E0 stores PlayerPhysics (0x67B16BA4, +0x20) and the typed TimeManager
        // reference from GetSystemRef<TimeManager>(null, true) (+0x80); nothing else.
        [Test]
        public void ConstructorStoresThePhysicsCategoryAndTheTimeManagerReference()
        {
            using (var scene = new Scene(awake: false))
            {
                Assert.That((int)(TimeCategory)Get(scene.Connector, "m_timeCategory"), Is.EqualTo(0x67B16BA4));
                var reference = (SystemRef<TimeManager>)Get(scene.Connector, "m_timeManagerRef");
                Assert.That(reference.SystemName(), Is.EqualTo(ProcessManager.GetDefaultName<TimeManager>()));
                Assert.That(Get(scene.Connector, "m_alwaysActive"), Is.False);
                Assert.That(Get(scene.Connector, "m_time"), Is.Zero);
                Assert.That(scene.Connector.IsPaused, Is.False);
                scene.Connector.IsPaused = true;
                Assert.That(scene.Connector.IsPaused, Is.True);
            }
        }

        // Awake 0x6E3000 marks the connector always active and caches its local scale (+0x3C) and
        // rotation (+0x48).
        [Test]
        public void AwakeCachesTheAuthoredPose()
        {
            using (var scene = new Scene(awake: false))
            {
                scene.Connector.transform.localScale = new Vector3(2f, 3f, 4f);
                scene.Connector.transform.rotation = Quaternion.Euler(10f, 20f, 30f);
                Call(scene.Connector, "Awake");
                Assert.That(Get(scene.Connector, "m_alwaysActive"), Is.True);
                Assert.That((Vector3)Get(scene.Connector, "m_scale"), Is.EqualTo(new Vector3(2f, 3f, 4f)));
                Assert.That(Quaternion.Angle((Quaternion)Get(scene.Connector, "m_startRotation"), Quaternion.Euler(10f, 20f, 30f)), Is.LessThan(0.01f));
            }
        }

        // SetPivots 0x6E3170: an inactive connector is switched on and stops being always active
        // (0x6E31CD..0x6E31E8); the catch transform and offset are stored; an authored anchor (+0x28,
        // tested with op_Inequality at 0x6E3231) wins over the supplied one; the anchor body is the
        // anchor's Rigidbody or null; the extension timer restarts.
        [Test]
        public void SetPivotsActivatesTheConnectorAndPicksTheAnchorAndItsBody()
        {
            using (var scene = new Scene())
            {
                Transform decoy = scene.Objects.Create("decoy").transform;
                scene.Connector.gameObject.SetActive(false);
                Set(scene.Connector, "m_time", 5f);
                var offset = new Vector3(1f, 2f, 3f);
                scene.Connector.SetPivots(scene.Catch, scene.Anchor, offset);
                Assert.That(scene.Connector.gameObject.activeSelf, Is.True);
                Assert.That(Get(scene.Connector, "m_alwaysActive"), Is.False);
                Assert.That(Get(scene.Connector, "m_catchTransform"), Is.SameAs(scene.Catch));
                Assert.That((Vector3)Get(scene.Connector, "m_catchOffset"), Is.EqualTo(offset));
                Assert.That(Get(scene.Connector, "m_anchorTransform"), Is.SameAs(scene.Anchor));
                Assert.That((Rigidbody)Get(scene.Connector, "m_anchorBody") == null, Is.True);
                Assert.That(Get(scene.Connector, "m_time"), Is.Zero);

                // An active connector keeps its flag; a Rigidbody on the anchor is found; the authored anchor wins.
                Call(scene.Connector, "Awake");
                var body = scene.Anchor.gameObject.AddComponent<Rigidbody>();
                scene.Connector.SetPivots(scene.Catch, scene.Anchor, offset);
                Assert.That(Get(scene.Connector, "m_alwaysActive"), Is.True);
                Assert.That(Get(scene.Connector, "m_anchorBody"), Is.SameAs(body));
                Set(scene.Connector, "m_anchor", decoy);
                scene.Connector.SetPivots(scene.Catch, scene.Anchor, offset);
                Assert.That(Get(scene.Connector, "m_anchorTransform"), Is.SameAs(decoy));
                Assert.That((Rigidbody)Get(scene.Connector, "m_anchorBody") == null, Is.True, "the authored anchor has no body");
            }
        }

        // ClearPivots 0x6E3290 returns at once while m_alwaysActive is set; otherwise it clears the
        // catch transform, anchor and body and deactivates the connector.
        [Test]
        public void ClearPivotsOnlyActsOnAConnectorThatSetPivotsSwitchedOn()
        {
            using (var scene = new Scene())
            {
                scene.Connector.SetPivots(scene.Catch, scene.Anchor, Vector3.zero);
                scene.Connector.ClearPivots();
                Assert.That(scene.Connector.gameObject.activeSelf, Is.True, "always active: untouched");
                Assert.That(Get(scene.Connector, "m_catchTransform"), Is.SameAs(scene.Catch));

                scene.Connector.gameObject.SetActive(false);
                scene.Connector.SetPivots(scene.Catch, scene.Anchor, Vector3.zero);
                Assert.That(Get(scene.Connector, "m_alwaysActive"), Is.False);
                scene.Connector.ClearPivots();
                Assert.That(scene.Connector.gameObject.activeSelf, Is.False);
                Assert.That(Get(scene.Connector, "m_catchTransform") as Transform, Is.Null);
                Assert.That(Get(scene.Connector, "m_anchorTransform") as Transform, Is.Null);
                Assert.That((Rigidbody)Get(scene.Connector, "m_anchorBody") == null, Is.True);
            }
        }

        // OnLateUpdate 0x6E3300. An always-active connector without an anchor returns first. Otherwise
        // the end point is anchorRotation * catchOffset + catchTransform.position (the connector itself
        // when no catch transform is set); while m_time < m_timeToExtend it is lerped from the anchor by
        // m_time / m_timeToExtend and m_time advances (clamped to the extension time). The connector
        // sits m_offset past that point along the unit vector towards the anchor, its Y scale is the
        // distance plus m_offset, and the rotation is LookRotation(direction) * startRotation, or the
        // start rotation alone when an anchor is authored. The anchor is at the origin and the catch
        // point at (0,3,0) unless stated.
        [Test]
        public void LateUpdateStretchesTheConnectorFromTheAnchorToTheCatchPoint()
        {
            using (var scene = new Scene())
            {
                Transform connector = scene.Connector.transform;
                Set(scene.Connector, "m_offset", 0.5f);
                connector.position = new Vector3(5f, 5f, 5f);
                scene.Connector.OnLateUpdate(0.1f);
                AreClose(new Vector3(5f, 5f, 5f), connector.position, "always active without an anchor: nothing happens");

                scene.Connector.SetPivots(scene.Catch, scene.Anchor, Vector3.zero);
                scene.Connector.OnLateUpdate(0.1f);
                AreClose(new Vector3(0f, 2.5f, 0f), connector.position, "3 above the anchor, 0.5 towards it");
                Assert.That(connector.localScale.y, Is.EqualTo(3.5f).Within(1e-4f), "distance plus offset");
                AreClose(Vector3.down, connector.rotation * Vector3.forward, "looks from the catch point at the anchor");
                Assert.That(Get(scene.Connector, "m_time"), Is.Zero, "no extension time");
            }
        }

        [Test]
        public void LateUpdateExtendsTowardsTheCatchPointOverTheExtensionTime()
        {
            using (var scene = new Scene())
            {
                Set(scene.Connector, "m_offset", 0.5f);
                Set(scene.Connector, "m_timeToExtend", 2f);
                scene.Connector.SetPivots(scene.Catch, scene.Anchor, Vector3.zero);
                Set(scene.Connector, "m_time", 0.5f);
                scene.Connector.OnLateUpdate(0.25f);
                // A quarter of the way from the anchor: the end is at (0,0.75,0), 0.5 towards the anchor.
                AreClose(new Vector3(0f, 0.25f, 0f), scene.Connector.transform.position, "t = 0.5 / 2");
                Assert.That(scene.Connector.transform.localScale.y, Is.EqualTo(1.25f).Within(1e-4f));
                Assert.That((float)Get(scene.Connector, "m_time"), Is.EqualTo(0.75f).Within(1e-6f));

                Set(scene.Connector, "m_time", 1.9f);
                scene.Connector.OnLateUpdate(0.5f);
                Assert.That((float)Get(scene.Connector, "m_time"), Is.EqualTo(2f), "clamped to the extension time");

                // At the extension time it no longer extends and the time stays.
                scene.Connector.OnLateUpdate(0.5f);
                Assert.That((float)Get(scene.Connector, "m_time"), Is.EqualTo(2f));
                AreClose(new Vector3(0f, 2.5f, 0f), scene.Connector.transform.position, "fully extended");
            }
        }

        // Without a catch transform OnLateUpdate uses the connector's own transform as the catch point
        // (the op_Inequality at 0x6E33A7, then the connector transform in r15). The connector stands at
        // (0,4,0) and the anchor at the origin, so the distance is 4 and the connector stays put; falling
        // back to the anchor would give distance 0, and returning early would leave the scale alone.
        [Test]
        public void LateUpdateFallsBackToTheConnectorsOwnTransformWithoutACatchTransform()
        {
            using (var scene = new Scene())
            {
                Transform connector = scene.Connector.transform;
                scene.Connector.gameObject.SetActive(false);
                connector.position = new Vector3(0f, 4f, 0f);
                scene.Connector.SetPivots(null, scene.Anchor, Vector3.zero);
                Assert.That(Get(scene.Connector, "m_alwaysActive"), Is.False);
                Assert.That(Get(scene.Connector, "m_catchTransform") as Transform, Is.Null);

                scene.Connector.OnLateUpdate(0.1f);
                AreClose(new Vector3(0f, 4f, 0f), connector.position, "the connector is its own catch point");
                Assert.That(connector.localScale.y, Is.EqualTo(4f).Within(1e-4f), "distance from the connector to the anchor");
            }
        }

        // The catch offset is rotated by the anchor before it is added, and the start rotation is the
        // right-hand factor: with the catch at (3,3,0) the look rotation sends the connector's right to
        // +z, so a start of yaw 90 faces +z, while the opposite order would face (0,-0.71,0.71). An
        // authored anchor skips the look rotation, so the connector keeps its yaw-90 start.
        [Test]
        public void LateUpdateComposesTheLookRotationWithTheStartRotation()
        {
            using (var scene = new Scene(90f))
            {
                scene.Catch.position = new Vector3(3f, 3f, 0f);
                scene.Connector.SetPivots(scene.Catch, scene.Anchor, Vector3.zero);
                scene.Connector.OnLateUpdate(0.1f);
                AreClose(new Vector3(3f, 3f, 0f), scene.Connector.transform.position, "offset 0");
                Assert.That(scene.Connector.transform.localScale.y, Is.EqualTo(Mathf.Sqrt(18f)).Within(1e-4f));
                AreClose(Vector3.forward, scene.Connector.transform.rotation * Vector3.forward, "look rotation times yaw 90");

                Set(scene.Connector, "m_anchor", scene.Anchor);
                scene.Connector.OnLateUpdate(0.1f);
                AreClose(Vector3.right, scene.Connector.transform.rotation * Vector3.forward, "authored anchor: the start rotation alone");
            }
        }

        [Test]
        public void LateUpdateRotatesTheCatchOffsetByTheAnchorAndUsesTheAnchorBodyWhenPresent()
        {
            using (var scene = new Scene())
            {
                scene.Anchor.rotation = Quaternion.Euler(0f, 90f, 0f);
                scene.Connector.SetPivots(scene.Catch, scene.Anchor, new Vector3(0f, 0f, 2f));
                scene.Connector.OnLateUpdate(0.1f);
                // anchorRotation * (0,0,2) = (2,0,0), added to the catch point (0,3,0).
                AreClose(new Vector3(2f, 3f, 0f), scene.Connector.transform.position, "rotated offset");

                // With a Rigidbody on the anchor its pose is read instead of the transform's.
                scene.Anchor.rotation = Quaternion.identity;
                var body = scene.Anchor.gameObject.AddComponent<Rigidbody>();
                body.position = new Vector3(0f, 0f, 10f);
                scene.Connector.SetPivots(scene.Catch, scene.Anchor, Vector3.zero);
                scene.Connector.OnLateUpdate(0.1f);
                Assert.That(scene.Connector.transform.localScale.y, Is.EqualTo(Mathf.Sqrt(3f * 3f + 10f * 10f)).Within(1e-3f), "the body is at z = 10");
            }
        }

        // OnEnable 0x6E3050 hands <OnEnable>b__14_0 to InvokeOnValid, which runs it at once for a
        // registered TimeManager; the lambda tail-calls TimeExtensions.Subscribe with m_timeCategory
        // (edx from this+0x20 at 0x6E3899), LateUpdate (0x4F5A935C) and "". OnDisable 0x6E30E0 gets the
        // live system and tail-calls TimeExtensions.Unsubscribe with the same category and LateUpdate
        // (0x6E314D..0x6E3156). The category is set away from its default. The owned TimeManager stays
        // inactive: the Editor's Reset on AddComponent faults on its null configuration (logged, as the
        // maintained TimeManager verification expects), and Awake, which needs the shared
        // TimeCategoryConfiguration, never runs, so its three empty phase dictionaries are added here.
        // The TimeExtensions category lookup is leased.
        [Test]
        public void ConnectorSubscribesAndUnsubscribesForLateUpdateWithItsOwnCategory()
        {
            FieldInfo lookupField = typeof(TimeExtensions).GetField("s_config_Internal", BindingFlags.Static | BindingFlags.NonPublic);
            object priorLookup = lookupField.GetValue(null);
            using (var timeRow = new VerificationFixtureOwnership.LifecycleRow(ProcessManager.GetDefaultName<TimeManager>()))
            using (var objects = new OwnedWorldObjects("lucid-catch-connector-time"))
            {
                TimeManager manager = null;
                bool registered = false;
                VerificationFixtureOwnership.RunThenCleanUp(() =>
                {
                    var environment = objects.Own(ScriptableObject.CreateInstance<TimeCategoryObject>());
                    var playerPhysics = objects.Own(ScriptableObject.CreateInstance<TimeCategoryObject>());
                    Set(environment, "m_guid", "lucid-catch-connector-environment");
                    Set(playerPhysics, "m_guid", "lucid-catch-connector-player-physics");
                    var lookup = objects.Own(ScriptableObject.CreateInstance<TimeCategoryLookup>());
                    lookup.Dictionary.Add(TimeCategory.Environment, environment);
                    lookup.Dictionary.Add(TimeCategory.PlayerPhysics, playerPhysics);
                    lookupField.SetValue(null, lookup);

                    GameObject timeHost = objects.Create("time");
                    timeHost.SetActive(false);
                    LogAssert.Expect(LogType.Exception, new Regex("^NullReferenceException: Object reference not set to an instance of an object$"));
                    manager = timeHost.AddComponent<TimeManager>();
                    var phases = (IDictionary)Get(manager, "m_updateObjects");
                    Type phaseType = phases.GetType().GetGenericArguments()[1];
                    foreach (UpdateOn updateOn in new[] { UpdateOn.FixedUpdate, UpdateOn.LateUpdate, UpdateOn.Update })
                        phases.Add(updateOn, Activator.CreateInstance(phaseType));

                    GameObject host = objects.Create("connector");
                    host.SetActive(false);
                    var connector = host.AddComponent<CatchConnector>();
                    Set(connector, "m_timeCategory", TimeCategory.Environment);
                    ProcessManager.RegisterSystem(manager);
                    registered = true;

                    Call(connector, "OnEnable");
                    var late = (IDictionary)phases[UpdateOn.LateUpdate];
                    Assert.That(late.Count, Is.EqualTo(1));
                    Assert.That(late.Contains(environment), Is.True, "the connector's own category");
                    var subscriptions = (IList)late[environment];
                    Assert.That(subscriptions.Count, Is.EqualTo(1));
                    object subscription = subscriptions[0];
                    Assert.That(Get(subscription, "TimeScaledObject"), Is.SameAs(connector));
                    Assert.That(Get(subscription, "IsSubscribed"), Is.True);
                    Assert.That(Get(subscription, "TransformPath"), Is.EqualTo(""));
                    Assert.That(((IDictionary)phases[UpdateOn.FixedUpdate]).Count, Is.Zero);
                    Assert.That(((IDictionary)phases[UpdateOn.Update]).Count, Is.Zero, "not Subscribe's Update default");

                    Call(connector, "OnDisable");
                    Assert.That(Get(subscription, "IsSubscribed"), Is.False, "unsubscribed from the same category and phase");
                    Assert.That(subscriptions.Count, Is.EqualTo(1), "Unsubscribe only marks the entry");
                },
                () => { if (registered) ProcessManager.UnregisterSystem(manager); },
                objects.Dispose,
                () => lookupField.SetValue(null, priorLookup));
            }
        }

        // OnDisable returns while the reference's system IsNull() (0x6E3127) and, through the
        // separate reference test at 0x6E3118, while the reference itself is null.
        [Test]
        public void OnDisableReturnsWithoutATimeManager()
        {
            using (var scene = new Scene(awake: false))
            {
                var reference = (SystemRef<TimeManager>)Get(scene.Connector, "m_timeManagerRef");
                Assert.That(reference.IsNull(), Is.True);
                Assert.DoesNotThrow(() => Call(scene.Connector, "OnDisable"));
                Set(scene.Connector, "m_timeManagerRef", null);
                Assert.DoesNotThrow(() => Call(scene.Connector, "OnDisable"), "a null reference is not dereferenced");
            }
        }
    }
}
