using System;
using System.IO;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // No save file is read, written or deleted: each case faults before any
    // file operation. The property-store singleton is restored afterwards.
    public sealed class OriginalPropertyStoreGapTests
    {
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        // HLPropertyStore..ctor 0x06000d03, x86-64 0x1b0ae30: after the field
        // initializers and Application.isPlaying 0x1b0aff5, the store is written
        // to s_internalInstance (static +0x48) 0x1b0b004 before the storage
        // object is allocated and constructed 0x1b0b031 and stored at +0x20.
        // The offline storage rejects a non-portable file name in its
        // constructor, so a faulting storage leaves the new store published
        // with no storage. Publishing after storage construction fails here.
        [Test]
        public void StoreIsPublishedBeforeItsStorageConstructionFaults()
        {
            Assert.That(ProjectLucid.Offline.OfflineAdapters.Active, Is.True, "Owned offline case requires the offline adapters.");
            FieldInfo singleton = typeof(HLPropertyStore).GetField("s_internalInstance", AnyStatic);
            object prior = singleton.GetValue(null);
            try
            {
                Assert.Throws<ArgumentException>(() => new HLPropertyStore("lucid-owned-key", 31, "lucid/not-a-file-component"));
                object published = singleton.GetValue(null);
                Assert.That(published, Is.InstanceOf<HLPropertyStore>());
                Assert.That(published, Is.Not.SameAs(prior));
                Assert.That(typeof(HLPropertyStore).GetField("m_clientVersion", AnyInstance).GetValue(published), Is.EqualTo(31));
                Assert.That(typeof(HLPropertyStore).GetField("m_propertyFileStorage", AnyInstance).GetValue(published), Is.Null);
                Assert.That(((HLPropertyStore)published).IsLoaded, Is.False);
            }
            finally
            {
                singleton.SetValue(null, prior);
            }
        }

        // HLSaveMethodEncrypted.WipeAllSaveFiles 0x06000d54, x86-64 0x1b0f010:
        // Directory.GetFiles 0x1b0f0ee runs before the per-file loop. The only
        // landing pad 0x1b0f187 logs and resumes that loop at 0x1b0f113, so it
        // covers each file's work, not the enumeration. A missing directory
        // therefore escapes. Widening the try over enumeration fails here.
        // TryGetSaveVersion's path construction is a string concatenation that
        // cannot fault, so its position outside that method's catch is not observable.
        [Test]
        public void WipeAllSaveFilesLetsAnEnumerationFaultEscape()
        {
            string folder = "lucid-owned-missing-" + Guid.NewGuid().ToString("N");
            Assert.That(Directory.Exists(Path.Combine(Application.persistentDataPath, folder)), Is.False);
            var storage = new HLSaveMethodEncrypted("lucid-owned-key-0123456789", folder + "/save");
            Assert.Throws<DirectoryNotFoundException>(() => storage.WipeAllSaveFiles());
            Assert.That(Directory.Exists(Path.Combine(Application.persistentDataPath, folder)), Is.False);
        }
    }
}
