using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetAddressableSprite (Game.Runtime 0x0200099a, x86-64 0x699730..0x699AA0) as a real component on an inactive owned host over a
    // real AddressableManager (registered into its absent row; Awake never runs). Sprites are loaded through the manager's preloaded
    // asset table, the route LoadAssetImmediate takes before any Addressables call: each sprite is the result of a completed operation of an
    // owned ResourceManager, entered in the manager's private lookup by its GUID (no public call fills it without an Addressables
    // load) and in its retained-handle set, so releasing it never reaches Addressables. Not covered: a load that is not preloaded, and
    // the Addressables release of a handle that is not retained.
    public sealed class UIWidgetAddressableSpriteTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private sealed class Scene
        {
            public AddressableManager Manager;
            public ResourceManager Resources;

            // A sprite the manager reports as preloaded under a fresh GUID, and the reference that names it.
            public AssetReferenceT<Sprite> Preload(Owned owned, out Sprite sprite)
            {
                sprite = NewSprite(owned);
                string guid = Guid.NewGuid().ToString("N");
                AsyncOperationHandle<Sprite> handle = Resources.CreateCompletedOperation(sprite, null);
                owned.Lease(new ReleaseAction(() => { if (handle.IsValid()) Resources.Release(handle); }));
                Get<Dictionary<string, AsyncOperationHandle>>(Manager, "m_preloadedAssetHandleLookup")[guid] = handle;
                Get<HashSet<AsyncOperationHandle>>(Manager, "m_preloadHandles").Add(handle);
                return new AssetReferenceT<Sprite>(guid);
            }
        }

        private static void Run(Action<Owned, Scene> body) => Owned.Run(owned =>
        {
            var scene = new Scene();
            scene.Manager = owned.Host("Lucid addressable manager", false).AddComponent<AddressableManager>();
            owned.Register(scene.Manager);
            scene.Resources = new ResourceManager();
            typeof(ResourceManager).GetField("CallbackHooksEnabled", Instance).SetValue(scene.Resources, false);
            owned.Lease(new ReleaseAction(() => scene.Resources.Dispose()));
            body(owned, scene);
        });

        private static UIWidgetAddressableSprite NewWidget(Owned owned) =>
            owned.Host("Lucid addressable sprite", false).AddComponent<UIWidgetAddressableSprite>();

        // dump.cs TypeDefIndex 2457 and the DummyDll rows (fields 0x0400282e..0x04002831; the callback is <RefreshBindings>b__6_0).
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(UIWidgetAddressableSprite), 0x00100001,
                new (string, int)[]
                {
                    ("LoadedSprite", 0x0026), ("m_addressablesManagerRef", 0x0021), ("m_spriteHandle", 0x0001), ("m_spriteReference", 0x0001),
                },
                new (string, int)[]
                {
                    ("OnDestroy", 0x0081), ("SetSpriteReference", 0x0086), ("RefreshBindings", 0x0081), ("TryUnloadAsset", 0x0081),
                    (".ctor", 0x1886), ("<RefreshBindings>b__6_0", 0x0081),
                });
            AssertIl2CppOptions(typeof(UIWidgetAddressableSprite), 1, 2);
            Assert.That(typeof(UIWidgetAddressableSprite).BaseType, Is.EqualTo(typeof(UIWidget)));
        }

        // The constructor 0x6999A0 builds the LoadedSprite bindable (+0x20) and takes the AddressableManager reference (+0x28).
        [Test]
        public void ConstructorBuildsTheBindableAndTakesTheRegistryManagerReference()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<AddressableManager>();
                UIWidgetAddressableSprite widget = NewWidget(owned);

                Assert.That(widget.LoadedSprite, Is.Not.Null);
                Assert.That(widget.LoadedSprite.Value, Is.Null);
                Assert.That(Get<SystemRef<AddressableManager>>(widget, "m_addressablesManagerRef"), Is.SameAs(ProcessManager.GetSystemRef<AddressableManager>()));
                Assert.That(Get<AsyncOperationHandle>(widget, "m_spriteHandle").IsValid(), Is.False);
            });
        }

        // SetSpriteReference 0x699820 stores the reference and refreshes: the sprite is loaded through the manager (the callback
        // <RefreshBindings>b__6_0 publishes it) and its handle is kept.
        [Test]
        public void SettingAReferenceLoadsItsSpriteAndKeepsTheHandle()
        {
            Run((owned, scene) =>
            {
                UIWidgetAddressableSprite widget = NewWidget(owned);
                AssetReferenceT<Sprite> reference = scene.Preload(owned, out Sprite sprite);

                widget.SetSpriteReference(reference);

                Assert.That(Get<AssetReferenceT<Sprite>>(widget, "m_spriteReference"), Is.SameAs(reference));
                Assert.That(widget.LoadedSprite.Value, Is.SameAs(sprite));
                var handle = Get<AsyncOperationHandle>(widget, "m_spriteHandle");
                Assert.That(handle.IsValid(), Is.True);
                Assert.That(handle.Result, Is.SameAs(sprite));
            });
        }

        // 0x6998B0: with no reference the refresh returns before it even fetches the manager, so the previous sprite and handle stay,
        // and a later reference replaces both.
        [Test]
        public void ClearingTheReferenceKeepsTheLoadedSpriteAndANewReferenceReplacesIt()
        {
            Run((owned, scene) =>
            {
                UIWidgetAddressableSprite widget = NewWidget(owned);
                AssetReferenceT<Sprite> first = scene.Preload(owned, out Sprite firstSprite);
                AssetReferenceT<Sprite> second = scene.Preload(owned, out Sprite secondSprite);
                widget.SetSpriteReference(first);

                widget.SetSpriteReference(null);
                Assert.That(Get<AssetReferenceT<Sprite>>(widget, "m_spriteReference"), Is.Null);
                Assert.That(widget.LoadedSprite.Value, Is.SameAs(firstSprite));
                Assert.That(Get<AsyncOperationHandle>(widget, "m_spriteHandle").Result, Is.SameAs(firstSprite));

                widget.SetSpriteReference(second);
                Assert.That(widget.LoadedSprite.Value, Is.SameAs(secondSprite));
                Assert.That(Get<AsyncOperationHandle>(widget, "m_spriteHandle").Result, Is.SameAs(secondSprite));
            });
        }

        // A null reference needs no manager at all: the manager reference is never read.
        [Test]
        public void ANullReferenceNeedsNoAddressableManager()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<AddressableManager>();
                UIWidgetAddressableSprite widget = NewWidget(owned);

                Assert.DoesNotThrow(() => widget.SetSpriteReference(null));
                Assert.That(widget.LoadedSprite.Value, Is.Null);
            });
        }

        // The callback <RefreshBindings>b__6_0 (0x699A70) is a plain write of the bindable.
        [Test]
        public void TheLoadCallbackPublishesTheSprite()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<AddressableManager>();
                UIWidgetAddressableSprite widget = NewWidget(owned);
                Sprite sprite = NewSprite(owned);

                Call(widget, "<RefreshBindings>b__6_0", sprite);

                Assert.That(widget.LoadedSprite.Value, Is.SameAs(sprite));
            });
        }

        // OnDestroy 0x699730 only reaches for the manager while the reference is valid; with no manager registered, or no handle yet,
        // it does nothing.
        [Test]
        public void DestroyDoesNothingWithoutAManagerOrAHandle()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<AddressableManager>();
                UIWidgetAddressableSprite widget = NewWidget(owned);

                Assert.DoesNotThrow(() => Call(widget, "OnDestroy"), "no manager registered");
            });
            Run((owned, scene) =>
            {
                UIWidgetAddressableSprite widget = NewWidget(owned);

                Assert.DoesNotThrow(() => Call(widget, "OnDestroy"), "a manager but no handle");
                Assert.DoesNotThrow(() => Call(widget, "TryUnloadAsset", scene.Manager), "TryUnloadAsset checks the handle first");
            });
        }
    }
}
