using System;
using Hardlight;
using NUnit.Framework;

namespace ProjectLucid.Tests
{
    // SystemRef<T>.TryGet(out T) at 0x06000e20 (x86-64 0xc07280, ARM64 0xbf40d8) copies the
    // typed field but returns whether the untyped system is non-null (#465). TryGet<T2>
    // (0x06000e21) keeps its typed result.
    public sealed class OriginalSystemRefTryGetTests
    {
        private sealed class Original : ISystem { }
        private sealed class Replacement : ISystem { }

        [Test]
        public void TryGetReportsTheUntypedSystemAfterAnIncompatibleReplacement()
        {
            string name = "Lucid-systemref-tryget-" + Guid.NewGuid().ToString("N");
            using (var owned = new VerificationFixtureOwnership.RegistryRow(name))
            {
                var original = new Original();
                owned.Register(original);
                SystemRef<Original> reference = ProcessManager.GetSystemRef<Original>(name);
                Assert.That(reference.TryGet(out Original before), Is.True);
                Assert.That(before, Is.SameAs(original));

                owned.Register(new Replacement(), canReplace: true);
                Assert.That(ProcessManager.GetSystemRef<Original>(name), Is.SameAs(reference), "the cached reference is reused");
                Assert.That(reference.IsValid(), Is.True, "the untyped system is the replacement");
                Assert.That(reference.TryGet(out Original after), Is.True, "the result follows the untyped system");
                Assert.That(after, Is.Null, "the incompatible replacement clears the typed system");
                Assert.That(reference.TryGet<Original>(out Original typed), Is.False, "TryGet<T2> keeps its typed result");
                Assert.That(typed, Is.Null);
            }
        }
    }
}
