using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class RecoveredBehaviorTests
    {
        [Test]
        public void OriginalStartingPointDefinitionsBindToMaintainedGuidType()
        {
            StartingPointAssetVerification.Run();
        }

        [Test]
        public void ScriptableGuidRetainsOriginalSerializationAndUnityNullRules()
        {
            ScriptableGuidVerification.Run();
        }

        [Test]
        public void CoroutineCallbacksRetainOriginalNextFrameOrdering()
        {
            CoroutineCallbackVerification.Run();
        }

        [Test]
        public void EnumComparersRetainNativeHashesAndStaticIdentities()
        {
            EnumComparerVerification.Run();
        }

        [Test]
        public void ProgressRecordsRetainOriginalGraphAndUnityJson()
        {
            SaveProgressRecordVerification.Run();
            SaveProgressSerializationVerification.Run();
        }

        [Test]
        public void StateMachineStorageConditionsPreserveNativeTypeAndTimingSemantics()
        {
            FSMStorageConditionVerification.Run();
        }

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
        public void StateMachineComponentsRetainOriginalUserAndLifecycleOrder()
        {
            FSMBehaviourVerification.Run();
        }

        [Test]
        public void StateMachineCompletionRetainsOriginalEndAndFinishedRules()
        {
            FSMCompletionVerification.Run();
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
        public void SaveDataRetainsOriginalDefaultsDirtyChildrenAndJson()
        {
            SaveDataVerification.Run();
            SaveDataSerializationVerification.Run();
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
