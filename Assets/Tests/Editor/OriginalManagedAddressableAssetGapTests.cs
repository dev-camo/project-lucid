using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    public sealed class OriginalManagedAddressableAssetGapTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo s_loaded = typeof(ManagedAddressableAsset<Texture2D>).GetField("m_loadedAsset", Instance);
        private static readonly FieldInfo s_count = typeof(ManagedAddressableAsset<Texture2D>).GetField("m_refCount", Instance);
        private static readonly FieldInfo s_handle = typeof(ManagedAddressableAsset<Texture2D>).GetField("m_assetHandle", Instance);

        // ManagedAddressableAsset<T>.Load 0x06000504, shared x86-64 0x17bbc70:
        // `inc dword [rbx+0x38]` 0x17bbcad precedes the Unity inequality test of
        // the loaded asset (+0x18) 0x17bbcd7, which returns it without touching
        // the handle (+0x20). Unload 0x06000506 (0x17bbf40) decrements, returns
        // while positive 0x17bbf79, else stores 0 and clears the asset before
        // the handle IsValid gate 0x17bbfa1 on Release. Counting only uncached
        // loads, or not clamping a negative count, fails here. The uncached
        // path needs the process-wide Addressables instance and is not entered.
        [Test]
        public void CachedLoadsStillCountAndUnloadClampsBeforeClearingTheAsset()
        {
            Texture2D asset = null;
            try
            {
                asset = new Texture2D(1, 1) { name = "Lucid managed cached asset" };
                // The reference is never read on the cached path.
                var managed = new ManagedAddressableAsset<Texture2D>(null);
                s_loaded.SetValue(managed, asset);

                Assert.That(managed.Load(), Is.SameAs(asset));
                Assert.That(managed.Load(), Is.SameAs(asset));
                Assert.That(s_count.GetValue(managed), Is.EqualTo(2), "each cached load is counted");
                Assert.That(((AsyncOperationHandle<Texture2D>)s_handle.GetValue(managed)).IsValid(), Is.False,
                    "the cached path does not create a handle");

                managed.Unload();
                Assert.That(s_count.GetValue(managed), Is.EqualTo(1));
                Assert.That(s_loaded.GetValue(managed), Is.SameAs(asset), "a positive count keeps the asset");
                managed.Unload();
                Assert.That(s_count.GetValue(managed), Is.EqualTo(0));
                Assert.That(s_loaded.GetValue(managed), Is.Null);
                managed.Unload();
                Assert.That(s_count.GetValue(managed), Is.EqualTo(0), "an extra unload clamps to zero");
                Assert.That(asset != null, Is.True, "unloading never destroys the asset itself");
            }
            finally
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
        }
    }
}
