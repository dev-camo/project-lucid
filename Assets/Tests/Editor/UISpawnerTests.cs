using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.UiPublishingFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UISpawner (Game.Runtime 0x02000a3b, x86-64 0x6B9150..0x6B93D0) as a real component on an inactive owned host over a real
    // UIManager whose container manager is scripted. The serialized container identifier is set directly: it is authored data.
    public sealed class UISpawnerTests
    {
        private sealed class Scene
        {
            public ScriptedExclusivityContainerManager Containers;
            public UIManager Manager;
            public UIContainerIdentifier Identifier;
            public UISpawner Spawner;
        }

        private static void Run(System.Action<Owned, Scene> body) => Owned.Run(owned =>
        {
            var scene = new Scene();
            scene.Manager = NewUiManager(owned, out scene.Containers);
            scene.Identifier = NewIdentifier(owned, "spawned");
            scene.Spawner = owned.Host("Lucid ui spawner", false).AddComponent<UISpawner>();
            Set(scene.Spawner, "m_container", scene.Identifier); // serialized
            body(owned, scene);
        });

        // dump.cs TypeDefIndex 2618 and the DummyDll rows (fields 0x04002af5, 0x04002af6).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UISpawner), 0x00100001,
                new (string, int)[] { ("m_container", 0x0001), ("m_uiManager", 0x0001) },
                new (string, int)[]
                {
                    ("Start", 0x0081), ("Initialise", 0x0081), ("Action_SpawnContainer", 0x0086),
                    ("Action_SpawnContainerWithCloseEvent", 0x0086), ("Action_DestroyContainer", 0x0086), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(UISpawner), 1, 2);
            Assert.That(typeof(UISpawner).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(AttributeTypes(typeof(UISpawner).GetField("m_container", Declared)),
                Is.EqualTo(new[] { typeof(TooltipAttribute), typeof(SerializeField) }));
            Assert.That(Tooltip(typeof(UISpawner).GetField("m_container", Declared)),
                Is.EqualTo("Container we want to open and close so we don't spawn duplicate UI events."));
        }

        // Start 0x6B9150 hands Initialise (0x6B9230, which stores the manager) to the UIManager reference; the registered
        // manager answers at once.
        [Test]
        public void StartTakesTheRegisteredUiManager()
        {
            Run((owned, scene) =>
            {
                Assert.That(Get<UIManager>(scene.Spawner, "m_uiManager"), Is.Null);

                Call(scene.Spawner, "Start");

                Assert.That(Get<UIManager>(scene.Spawner, "m_uiManager"), Is.SameAs(scene.Manager));
            });
        }

        // 0x6B9250: nothing is opened while the container is already open, so repeating the action opens it once.
        [Test]
        public void SpawningOpensTheContainerOnlyOnce()
        {
            Run((owned, scene) =>
            {
                NewContainer<UIContainerSimple>(owned, scene.Identifier);
                Call(scene.Spawner, "Start");

                scene.Spawner.Action_SpawnContainer();
                scene.Spawner.Action_SpawnContainer();

                Assert.That(scene.Containers.Log, Is.EqualTo(new[] { "open:spawned" }));
            });
        }

        // 0x6B9380: an open container is closed through the manager; closing again, or closing one never opened, does nothing.
        [Test]
        public void DestroyingClosesTheContainerOnlyWhileItIsOpen()
        {
            Run((owned, scene) =>
            {
                NewContainer<UIContainerSimple>(owned, scene.Identifier);
                Call(scene.Spawner, "Start");

                scene.Spawner.Action_DestroyContainer();
                Assert.That(scene.Containers.Log, Is.Empty, "never opened");

                scene.Spawner.Action_SpawnContainer();
                scene.Spawner.Action_DestroyContainer();
                scene.Spawner.Action_DestroyContainer();

                Assert.That(scene.Containers.Log, Is.EqualTo(new[] { "open:spawned", "close:spawned" }));
            });
        }

        // 0x6B9290: a newly opened container that has a close event slot is given the event, which it then publishes when it
        // closes (UIContainerWithCloseEvent.Action_Close).
        [Test]
        public void SpawningWithACloseEventHandsTheEventToTheOpenedContainer()
        {
            Run((owned, scene) =>
            {
                App app = NewApp(owned);
                var container = NewContainer<UIContainerWithCloseEvent>(owned, scene.Identifier);
                UIModernEvent closeEvent = NewEvent(owned, "spawned-closed");
                Call(scene.Spawner, "Start");

                scene.Spawner.Action_SpawnContainerWithCloseEvent(closeEvent);

                Assert.That(scene.Containers.Log, Is.EqualTo(new[] { "open:spawned" }));
                Assert.That(app.GetUIModernEvents(), Is.Empty);
                container.Action_Close();
                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { closeEvent }));
            });
        }

        // 0x6B92CF..0x6B92D6: an already open container is returned before it is touched, so a second spawn request does not
        // replace the event it already holds.
        [Test]
        public void SpawningWithACloseEventLeavesAnOpenContainerAlone()
        {
            Run((owned, scene) =>
            {
                App app = NewApp(owned);
                var container = NewContainer<UIContainerWithCloseEvent>(owned, scene.Identifier);
                UIModernEvent first = NewEvent(owned, "first"), second = NewEvent(owned, "second");
                Call(scene.Spawner, "Start");

                scene.Spawner.Action_SpawnContainerWithCloseEvent(first);
                scene.Spawner.Action_SpawnContainerWithCloseEvent(second);
                container.Action_Close();

                Assert.That(scene.Containers.Log, Is.EqualTo(new[] { "open:spawned" }));
                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { first }));
            });
        }

        // 0x6B92E5..0x6B930E: the opened container is tested with a class check; one without a close event slot is opened and
        // nothing more happens (no fault).
        [Test]
        public void SpawningWithACloseEventOpensAContainerWithoutACloseEventSlotAndStopsThere()
        {
            Run((owned, scene) =>
            {
                NewContainer<UIContainerSimple>(owned, scene.Identifier);
                UIModernEvent closeEvent = NewEvent(owned, "unused");
                Call(scene.Spawner, "Start");

                Assert.DoesNotThrow(() => scene.Spawner.Action_SpawnContainerWithCloseEvent(closeEvent));

                Assert.That(scene.Containers.Log, Is.EqualTo(new[] { "open:spawned" }));
            });
        }
    }
}
