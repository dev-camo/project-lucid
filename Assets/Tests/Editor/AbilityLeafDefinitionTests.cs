using System;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Verification;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests.EditMode
{
    public sealed class AbilityLeafDefinitionTests
    {
        [Test]
        public void OriginalDefinitionDeclarationsAndBaseOnlyBodiesRemainIntact()
        {
            Assert.That(AbilityLeafDefinitionVerification.VerifyOriginalDeclarationsAndBodies(), Is.EqualTo(104));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OwnedCameraCachesPreserveGetterAliasesAndAsymmetricNullFaultOrder()
        {
            Assert.That(AbilityLeafDefinitionVerification.VerifyOwnedCameraCacheAndFaultOrder(), Is.EqualTo(41));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OriginalRestoreRecordDefaultsAndMutableFieldValuesRemainIntact()
        {
            Assert.That(AbilityLeafDefinitionVerification.VerifyRestoreRecordDefaultsAndMutability(), Is.EqualTo(72));
            LogAssert.NoUnexpectedReceived();
        }

        // Original TypeDef 0x02000410 is [Serializable] (flags 0x00102001) with public
        // fields 0x04000d89..0x04000d90 in this order, so Unity data embedding the record
        // carries every field by name in token order. The set-then-get checks above pass
        // for any field; this catches a field turning [NonSerialized], private, a
        // property, renamed or reordered.
        [Test]
        public void OriginalRestoreRecordSerializesEveryOriginalFieldInTokenOrder()
        {
            var record = new VelocityRestoreParameters
            {
                RestoreIfGreater = true, BlendDurationSeconds = 0.25f, BlendX = true, BlendY = false, BlendZ = true,
                MaximumSlopeAngle = 42f, ExpireOnContact = 1 << 7, ExpireOnContactDotThreshold = -0.75f,
            };
            string json = JsonUtility.ToJson(record);
            string[] names = { "RestoreIfGreater", "BlendDurationSeconds", "BlendX", "BlendY", "BlendZ",
                "MaximumSlopeAngle", "ExpireOnContact", "ExpireOnContactDotThreshold" };
            int previous = -1;
            foreach (string name in names)
            {
                int at = json.IndexOf("\"" + name + "\":", StringComparison.Ordinal);
                Assert.That(at, Is.GreaterThan(previous), name + " in " + json);
                previous = at;
            }
            var copy = JsonUtility.FromJson<VelocityRestoreParameters>(json);
            Assert.That(copy, Is.Not.SameAs(record));
            Assert.That(copy.RestoreIfGreater, Is.True);
            Assert.That(copy.BlendDurationSeconds, Is.EqualTo(0.25f));
            Assert.That(copy.BlendX, Is.True);
            Assert.That(copy.BlendY, Is.False);
            Assert.That(copy.BlendZ, Is.True);
            Assert.That(copy.MaximumSlopeAngle, Is.EqualTo(42f));
            Assert.That(copy.ExpireOnContact.value, Is.EqualTo(1 << 7));
            Assert.That(copy.ExpireOnContactDotThreshold, Is.EqualTo(-0.75f));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
