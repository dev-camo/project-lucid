using System;
using Hardlight;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    // Original HLUnityCore.LogOrThrowException without a live host. The
    // configured in-place route is covered by LoggingHostVerification.
    public sealed class OriginalLoggingHostGapTests
    {
        // 0x060008d8 x86-64 0x1aef500: MonoSingleton IsNull (0x1aef54b) jumps
        // to `new NullReferenceException(message)` (0x1aef65d) and raises it;
        // only a live host reaches the LogInPlaceOfException test (+0x31).
        // Fails if a missing host returns silently, logs instead of throwing,
        // or throws another exception type or message.
        [Test]
        public void MissingHostThrowsNullReferenceWithTheSuppliedMessage()
        {
            Assert.That(HLUnityCore.IsNull(), Is.True, "A live foreign host would take the logging route.");
            NullReferenceException error = Assert.Throws<NullReferenceException>(
                () => HLUnityCore.LogOrThrowException("owned missing logging host"));
            Assert.That(error.Message, Is.EqualTo("owned missing logging host"));
            Assert.That(error.InnerException, Is.Null);
            Assert.That(HLUnityCore.IsNull(), Is.True);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
