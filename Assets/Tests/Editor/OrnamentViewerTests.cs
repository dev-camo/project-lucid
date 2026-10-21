using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // OrnamentViewer (Game.Runtime 020006e4, bodies 0x5A01B0..0x5A0990 and the closure and iterator 0x5A05E0..0x5A0D90) and
    // OrnamentRoot (020006e2, 0x59FFC0). OnEnable, ShowOrnament and OnDestroy reach Addressables and the AddressableManager, so
    // they are not run here; the instantiating coroutine, the root announcement and the render-texture release are.
    public sealed class OrnamentViewerTests
    {
        private sealed class Rig
        {
            internal OrnamentViewer Viewer;
            internal OrnamentRoot Root;
            internal Ornament Prefab;
            internal int Instantiated;
        }

        // A viewer, a root and an ornament prefab (an inactive object carrying an Ornament with a rotator).
        private static Rig NewRig(Owned owned, float yOffset, float yRotationSpeed)
        {
            var rig = new Rig
            {
                Viewer = owned.Host("Lucid ornament viewer", false).AddComponent<OrnamentViewer>(),
                Root = owned.Host("Lucid ornament root", false).AddComponent<OrnamentRoot>(),
            };
            bool before = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true; // the editor's OnValidate adds the rotator inside AddComponent
            try
            {
                rig.Prefab = owned.Host("Lucid ornament prefab", false).AddComponent<Ornament>();
                Call(rig.Prefab, "OnValidate");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = before;
            }
            Set(rig.Prefab, "m_yRotationOffset", yOffset); // Reflection: authored serialized values
            Set(rig.Viewer, "m_yRotationSpeed", yRotationSpeed);
            return rig;
        }

        private static IEnumerator Load(Rig rig, Action callback = null) =>
            (IEnumerator)Call(rig.Viewer, "OnOrnamentLoaded", rig.Prefab.gameObject, callback ?? (() => rig.Instantiated++));

        private static void AssertRotation(Quaternion actual, float x, float y, float z, float w)
        {
            Assert.That(actual.x, Is.EqualTo(x).Within(1e-5f));
            Assert.That(actual.y, Is.EqualTo(y).Within(1e-5f));
            Assert.That(actual.z, Is.EqualTo(z).Within(1e-5f));
            Assert.That(actual.w, Is.EqualTo(w).Within(1e-5f));
        }

        // MoveNext 0x5A0A60 (state 0 or 1): while the root has not been announced the coroutine yields null and the
        // callback is not called; the OnOrnamentRootLoaded callback 0x5A03C0 stores the root.
        [Test]
        public void TheCoroutineWaitsForTheRootThenInstantiatesTheOrnamentUnderIt()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, 90f, 30f);
                IEnumerator loading = Load(rig);

                Assert.That(loading.MoveNext(), Is.True);
                Assert.That(loading.Current, Is.Null);
                Assert.That(loading.MoveNext(), Is.True, "still waiting");
                Assert.That(rig.Instantiated, Is.EqualTo(0));
                Assert.That(Get<Ornament>(rig.Viewer, "m_currentOrnament"), Is.Null);

                Call(rig.Viewer, "OnOrnamentRootLoaded", rig.Root);
                Assert.That(Get<OrnamentRoot>(rig.Viewer, "m_ornamentRoot"), Is.SameAs(rig.Root));
                Assert.That(loading.MoveNext(), Is.False);
                Assert.That(rig.Instantiated, Is.EqualTo(1));

                Ornament current = Get<Ornament>(rig.Viewer, "m_currentOrnament");
                Assert.That(current, Is.Not.Null);
                Assert.That(current, Is.Not.SameAs(rig.Prefab), "a clone is shown, not the prefab");
                Assert.That(current.transform.parent, Is.SameAs(rig.Root.transform));
                Assert.That(current.transform.localScale, Is.EqualTo(Vector3.one), "0x5A0CF8 sets the scale to one at the end");
                AssertRotation(current.transform.localRotation, 0f, 0.70710678f, 0f, 0.70710678f); // Euler(0, 90, 0)
            });
        }

        // 0x5A0C4A..0x5A0CB4: the clone's rotator is enabled, given the idle rotation (0, speed, 0) and switched to
        // automation (idle automation on, smoothing off).
        [Test]
        public void TheRotatorIsEnabledWithTheViewersYawSpeedAndAutomation()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, 90f, 30f);
                Call(rig.Viewer, "OnOrnamentRootLoaded", rig.Root);
                Assert.That(Load(rig).MoveNext(), Is.False);

                SmoothedTransformRotator rotator = Get<Ornament>(rig.Viewer, "m_currentOrnament").TransformRotator;
                Assert.That(rotator, Is.Not.SameAs(rig.Prefab.TransformRotator), "the clone's own rotator");
                Assert.That(rotator.enabled, Is.True);
                // Reflection: no accessor reports the idle speed or the two flags that ToggleAutomation writes.
                Assert.That(Get<Vector3>(rotator, "m_idleRotationSpeed"), Is.EqualTo(new Vector3(0f, 30f, 0f)));
                Assert.That(Get<bool>(rotator, "m_idleAutomation"), Is.True);
                Assert.That(Get<bool>(rotator, "m_smoothingEnabled"), Is.False);
            });
        }

        // 0x5A0BB6..0x5A0BE6: the yaw is the ornament's offset unless the rotator ping-pongs when idle, which needs
        // both the ping-pong and the idle-automation flags (PingPongWhenIdle); then the rotation is the identity.
        [Test]
        public void TheYawOffsetIsDroppedOnlyForAPingPongingRotator()
        {
            foreach ((bool pingPong, bool automation, float y, float w) in new[]
                     {
                         (false, false, 0.70710678f, 0.70710678f),
                         (true, false, 0.70710678f, 0.70710678f),
                         (false, true, 0.70710678f, 0.70710678f),
                         (true, true, 0f, 1f),
                     })
            {
                Owned.Run(owned =>
                {
                    Rig rig = NewRig(owned, 90f, 30f);
                    Set(rig.Prefab.TransformRotator, "m_pingPongWhenIdle", pingPong); // Reflection: authored rotator flags
                    Set(rig.Prefab.TransformRotator, "m_idleAutomation", automation);
                    Call(rig.Viewer, "OnOrnamentRootLoaded", rig.Root);
                    Assert.That(Load(rig).MoveNext(), Is.False);

                    Quaternion rotation = Get<Ornament>(rig.Viewer, "m_currentOrnament").transform.localRotation;
                    AssertRotation(rotation, 0f, y, 0f, w);
                });
            }
        }

        // The callback is called as a plain invocation (0x5A0D1D, after a null test that raises a null reference): a
        // missing callback faults only after the ornament has been set up.
        [Test]
        public void ANullCallbackFaultsAfterTheOrnamentIsShown()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, 0f, 0f);
                Call(rig.Viewer, "OnOrnamentRootLoaded", rig.Root);
                IEnumerator loading = (IEnumerator)Call(rig.Viewer, "OnOrnamentLoaded", rig.Prefab.gameObject, (Action)null);

                Assert.Throws<NullReferenceException>(() => loading.MoveNext());
                Assert.That(Get<Ornament>(rig.Viewer, "m_currentOrnament"), Is.Not.Null);
                Assert.That(Get<Ornament>(rig.Viewer, "m_currentOrnament").transform.localScale, Is.EqualTo(Vector3.one));
            });
        }

        // The state machine's Reset is not supported (0x5A0D50 raises NotSupportedException).
        [Test]
        public void TheCoroutineCannotBeReset()
        {
            Owned.Run(owned =>
            {
                Rig rig = NewRig(owned, 0f, 0f);
                Assert.Throws<NotSupportedException>(() => Load(rig).Reset());
            });
        }

        // OrnamentRoot.Awake 0x59FFC0 publishes itself on the string exchange under "OrnamentViewer"; the viewer's
        // OnEnable (0x5A02C9..0x5A031D) subscribes its OnOrnamentRootLoaded under the same message.
        [Test]
        public void TheRootAnnouncesItselfToTheViewerUnderTheViewersMessage()
        {
            Owned.Run(owned =>
            {
                var exchange = owned.Register(new MessageExchange<string>());
                var viewer = owned.Host("Lucid ornament viewer", false).AddComponent<OrnamentViewer>();
                var root = owned.Host("Lucid ornament root", false).AddComponent<OrnamentRoot>();
                var other = owned.Host("Lucid other root", false).AddComponent<OrnamentRoot>();
                MethodInfo handler = typeof(OrnamentViewer).GetMethod("OnOrnamentRootLoaded", BindingFlags.Instance | BindingFlags.NonPublic);
                var callback = (MessageCallback<OrnamentRoot>)Delegate.CreateDelegate(typeof(MessageCallback<OrnamentRoot>), viewer, handler);
                exchange.SubscribeToMessage(OrnamentViewer.MessageIdentifier, callback);

                exchange.PublishMessage("Another message", other);
                Assert.That(Get<OrnamentRoot>(viewer, "m_ornamentRoot"), Is.Null, "a different message identifier is not delivered");

                Call(root, "Awake");
                Assert.That(Get<OrnamentRoot>(viewer, "m_ornamentRoot"), Is.SameAs(root));
                Assert.That(OrnamentViewer.MessageIdentifier, Is.EqualTo("OrnamentViewer"));
            });
        }

        // ReleaseRenderTextures 0x5A0890: every camera with a target texture, active or not, has the texture released
        // and the target cleared; a camera without one is left alone. Cameras the Editor owns are put back afterwards.
        [Test]
        public void ReleasingRenderTexturesClearsEveryCamerasTarget()
        {
            Owned.Run(owned =>
            {
                var foreign = Resources.FindObjectsOfTypeAll<Camera>().Where(camera => camera.targetTexture != null)
                    .Select(camera => (camera, camera.targetTexture)).ToArray();
                try
                {
                    var texture = owned.Track(new RenderTexture(4, 4, 0));
                    texture.Create();
                    var withTarget = owned.Host("Lucid camera with a target", false).AddComponent<Camera>();
                    withTarget.targetTexture = texture;
                    var withoutTarget = owned.Host("Lucid camera without a target", true).AddComponent<Camera>();

                    typeof(OrnamentViewer).GetMethod("ReleaseRenderTextures", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

                    Assert.That(withTarget.targetTexture, Is.Null);
                    Assert.That(texture.IsCreated(), Is.False, "RenderTexture.Release was called");
                    Assert.That(withoutTarget.targetTexture, Is.Null);
                }
                finally
                {
                    foreach ((Camera camera, RenderTexture target) in foreign)
                        if (camera != null)
                            camera.targetTexture = target;
                }
            });
        }

        // Declarations from the DummyDll metadata. The generated closure and iterator names carry the member ordinals
        // 11 and 12 (fields, the constant and methods counted in declaration order).
        [Test]
        public void DeclarationsKeepTheOriginalMembersFlagsAndGeneratedNames()
        {
            AssertMembers(typeof(OrnamentViewer), 0x00100001,
                new[]
                {
                    ("m_yRotationSpeed", 0x0001), ("MessageIdentifier", 0x8056), ("m_ornamentHandle", 0x0001), ("m_ornamentRoot", 0x0001),
                    ("m_currentOrnament", 0x0001), ("m_previouslyActiveScene", 0x0001), ("m_lightingScene", 0x0001), ("m_messageExchange", 0x0001),
                },
                new[]
                {
                    ("OnEnable", 0x0081), ("OnOrnamentRootLoaded", 0x0081), ("OnSceneLoaded", 0x0081), ("ShowOrnament", 0x0086),
                    ("OnOrnamentLoaded", 0x0081), ("OnDestroy", 0x0081), ("ReleaseRenderTextures", 0x0091),
                    ("LogReleasingRenderTexture", 0x0091), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(OrnamentViewer), 2, 1); // metadata order: ArrayBoundsChecks, then NullChecks
            AssertMembers(typeof(OrnamentRoot), 0x00100001, new (string, int)[0], new[] { ("Awake", 0x0081), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(OrnamentRoot), 1, 2);

            Assert.That(typeof(OrnamentViewer).GetNestedTypes(BindingFlags.NonPublic).Select(type => type.Name).OrderBy(name => name).ToArray(),
                Is.EqualTo(new[] { "<>c__DisplayClass11_0", "<OnOrnamentLoaded>d__12" }));
            Assert.That(typeof(OrnamentViewer).GetField("MessageIdentifier").GetRawConstantValue(), Is.EqualTo("OrnamentViewer"));
            var logging = typeof(OrnamentViewer).GetMethod("LogReleasingRenderTexture", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(logging.GetCustomAttributes(typeof(ConditionalAttribute), false).Cast<ConditionalAttribute>().Single().ConditionString,
                Is.EqualTo("BUILD_DEVELOPMENT"));
            Assert.That(AttributeTypes(Field(typeof(OrnamentViewer), "m_yRotationSpeed")), Is.EqualTo(new[] { typeof(SerializeField) }));
        }
    }
}
