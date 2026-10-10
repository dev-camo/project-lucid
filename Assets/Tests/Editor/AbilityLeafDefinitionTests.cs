using NUnit.Framework;
using ProjectLucid.Verification;
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
    }
}
