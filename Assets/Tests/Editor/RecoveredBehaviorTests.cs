using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class RecoveredBehaviorTests
    {
        [Test]
        public void MeshGeometryMatchesNativeStoresAndIndexBlob()
        {
            MeshGenerationUtilitiesVerification.Run();
        }

        [Test]
        public void StateMachinePrimitivesMatchNativeHashAndIdentitySemantics()
        {
            FSMPrimitivesVerification.Run();
        }

        [Test]
        public void StateMachineTransitionsPreserveNativeUpdateAndCallbackOrder()
        {
            FSMInterpreterVerification.Run();
        }

        [Test]
        public void LocalStoragePersistsAndRecoversWithoutCloudNotifications()
        {
            LocalCloudVerification.Run();
        }

        [Test]
        public void SkyCubemapGpuOutputMatchesDecodedMetalCases()
        {
            SkyCubemapVerification.Run();
        }

        [Test]
        public void EggmanLogoGpuOutputMatchesDecodedColorBlendAndLayerCases()
        {
            EggmanLogoVerification.Run();
        }
    }
}
