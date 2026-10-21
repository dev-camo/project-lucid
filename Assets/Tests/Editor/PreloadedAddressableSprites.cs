using System;
using System.Collections.Generic;
using System.Reflection;
using HardlightProject;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // An AddressableManager (a real component on an inactive owned host; Awake never runs) that answers sprite loads from its
    // preloaded asset table, the route LoadAsset and LoadAssetImmediate take before any Addressables call. Each sprite is the result of
    // a completed operation of an owned ResourceManager, entered in the manager's private lookup by its GUID (no public call fills it
    // without an Addressables load).
    internal sealed class PreloadedAddressableSprites
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        internal AddressableManager Manager { get; private set; }
        internal ResourceManager Resources { get; private set; }

        // The manager is registered into its absent row before the body runs, so widgets built inside take its reference.
        internal static void Run(Action<Owned, PreloadedAddressableSprites> body, bool registerManager = true) => Owned.Run(owned =>
        {
            var sprites = new PreloadedAddressableSprites();
            sprites.Manager = owned.Host("Lucid addressable manager", false).AddComponent<AddressableManager>();
            if (registerManager) owned.Register(sprites.Manager);
            sprites.Resources = new ResourceManager();
            typeof(ResourceManager).GetField("CallbackHooksEnabled", Instance).SetValue(sprites.Resources, false);
            owned.Lease(new ReleaseAction(() => sprites.Resources.Dispose()));
            body(owned, sprites);
        });

        // A sprite the manager reports as preloaded under a fresh GUID, and the reference that names it. A retained handle is also in
        // the manager's preload set, so releasing it never reaches Addressables; an unretained one is released like any other load.
        internal AssetReferenceT<Sprite> Preload(Owned owned, out Sprite sprite, bool retained = true)
        {
            sprite = NewSprite(owned);
            string guid = Guid.NewGuid().ToString("N");
            AsyncOperationHandle<Sprite> handle = Resources.CreateCompletedOperation(sprite, null);
            owned.Lease(new ReleaseAction(() => { if (handle.IsValid()) Resources.Release(handle); }));
            Get<Dictionary<string, AsyncOperationHandle>>(Manager, "m_preloadedAssetHandleLookup")[guid] = handle;
            if (retained) Get<HashSet<AsyncOperationHandle>>(Manager, "m_preloadHandles").Add(handle);
            return new AssetReferenceT<Sprite>(guid);
        }
    }
}
