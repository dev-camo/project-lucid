using System;
using System.Collections;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // The borrowed-row helper over a uniquely named, owned registry row. App
    // and LevelManager stand in only as cache keys; no system is registered.
    public sealed class BorrowedSystemRowTypedCacheTests
    {
        private static IDictionary Cache(string name)
        {
            object row = BorrowedSystemRowTypedCache.Registry[name];
            return (IDictionary)row.GetType().GetField("SystemRefDictionary", BindingFlags.Instance | BindingFlags.Public).GetValue(row);
        }

        [Test]
        public void RemovesOnlyTypedReferencesCachedWhileBorrowed()
        {
            string name = "Lucid-borrowed-row-" + Guid.NewGuid().ToString("N");
            Assert.That(BorrowedSystemRowTypedCache.TryBorrow(name), Is.Null, "an absent row is not borrowed");
            using (var owned = new VerificationFixtureOwnership.RegistryRow(name))
            {
                owned.Register(null);
                SystemRef<LevelManager> prior = ProcessManager.GetSystemRef<LevelManager>(name);
                string appKey = ProcessManager.GetDefaultName<App>();
                string priorKey = ProcessManager.GetDefaultName<LevelManager>();

                BorrowedSystemRowTypedCache borrowed = BorrowedSystemRowTypedCache.TryBorrow(name);
                Assert.That(borrowed, Is.Not.Null);
                SystemRef<App> added = ProcessManager.GetSystemRef<App>(name);
                Assert.That(ProcessManager.GetSystemRef<LevelManager>(name), Is.SameAs(prior));
                Assert.That(Cache(name)[appKey], Is.SameAs(added), "GetSystemRef<T> caches into a borrowed row");

                borrowed.Dispose();
                Assert.That(Cache(name).Contains(appKey), Is.False);
                Assert.That(Cache(name)[priorKey], Is.SameAs(prior), "a prior typed reference is kept");
                Assert.That(Cache(name).Count, Is.EqualTo(1));
            }
            Assert.That(BorrowedSystemRowTypedCache.Registry.Contains(name), Is.False);
        }

        [Test]
        public void ReportsAChangedPriorTypedReferenceWithoutRestoringIt()
        {
            string name = "Lucid-borrowed-row-" + Guid.NewGuid().ToString("N");
            using (var owned = new VerificationFixtureOwnership.RegistryRow(name))
            {
                owned.Register(null);
                SystemRef<LevelManager> prior = ProcessManager.GetSystemRef<LevelManager>(name);
                string priorKey = ProcessManager.GetDefaultName<LevelManager>();
                BorrowedSystemRowTypedCache borrowed = BorrowedSystemRowTypedCache.TryBorrow(name);
                SystemRef<App> added = ProcessManager.GetSystemRef<App>(name);
                IDictionary cache = Cache(name);
                cache.Remove(priorKey);
                try
                {
                    Assert.Throws<InvalidOperationException>(borrowed.Dispose);
                    Assert.That(cache.Contains(priorKey), Is.False, "a changed borrowed cache is reported, not rewritten");
                    Assert.That(cache[ProcessManager.GetDefaultName<App>()], Is.SameAs(added), "nothing is removed after a report");
                }
                finally { cache[priorKey] = prior; }
            }
        }
    }
}
