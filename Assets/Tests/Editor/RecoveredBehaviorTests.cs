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
        public void CloudFacadePreservesOriginalCallbacksKeysAndConflictLifecycle()
        {
            CloudFacadeVerification.Run();
        }

        [Test]
        public void SavePropertiesPreserveNativeIdentityAndCultureSemantics()
        {
            PropertyListVerification.Run();
        }

        [Test]
        public void PropertyStoreRetainsNativeLifecycleAndRecoversLocalFiles()
        {
            PropertyStoreVerification.Run();
        }

        [Test]
        public void ScriptPointersRequireExactLoadedAssetAndComponentIdentities()
        {
            ScriptReferenceVerification.Run();
        }

        [Test]
        public void OriginalPipelineMarkerRetainsEngineFieldsAfterScriptBinding()
        {
            CinemachinePipelineVerification.Run();
        }

        [Test]
        public void TimeConversionsRetainOriginalUnitsRoundingAndDateKinds()
        {
            TimeUtilsVerification.Run();
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
