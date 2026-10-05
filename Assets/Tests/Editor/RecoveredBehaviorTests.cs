using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class RecoveredBehaviorTests
    {
        [Test]
        public void OriginalMathUtilitiesRetainNativeArithmeticAndGenericConversions()
        {
            Assert.That(MathFoundationVerification.Run(), Is.EqualTo(294));
        }

        [Test]
        public void OriginalFormTraitsRetainNativeGeometryDefaultsAndSerialization()
        {
            Assert.That(FormTraitsVerification.Run(), Is.EqualTo(35));
        }

        [Test]
        public void OriginalPhysicsUtilitiesRetainPickingAndCapsuleSweepGeometry()
        {
            // CompareTag reports one error for each of the two fixture hits.
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, "Tag: tag name is null or empty.");
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, "Tag: tag name is null or empty.");
            Assert.That(PhysicsFoundationVerification.Run(), Is.EqualTo(82));
        }

        [Test]
        public void OriginalVector3UtilitiesRetainGeometryAndEngineInterpolation()
        {
            Assert.That(Vector3FoundationVerification.Run(), Is.EqualTo(317));
        }

        [Test]
        public void OriginalFastActionRetainsExactClassCompilerOptions()
        {
            Assert.That(FastActionClassOptionsVerification.RunManaged(), Is.EqualTo(38));
        }

        [Test]
        public void OriginalMovementUtilitiesRetainNativeMathAndEngineConversions()
        {
            Assert.That(MovementFoundationVerification.Run(), Is.EqualTo(182));
        }

        [Test]
        public void LoadedTypeModulesRetainExactDiskAndTokenIdentity()
        {
            Assert.That(LoadedAttributeBlobVerification.RunLoadedTypes(), Is.EqualTo(31));
        }

        [Test]
        public void OriginalVersionRetainsNativeParsingComparisonAndSerializedCache()
        {
            VersionVerification.Run();
        }

        [Test]
        public void OriginalStateMachineDefinitionsRetainObjectNameKeysAndFailures()
        {
            StateMachinesGroupVerification.Run();
        }

        [Test]
        public void OriginalDefinitionGroupsRetainNativeMergeAndSerializationOrder()
        {
            DefinitionDataVerification.Run();
        }

        [Test]
        public void LoadedAttributeBlobsPreserveBoxedStringNullAndModuleIdentity()
        {
            Assert.That(LoadedAttributeBlobVerification.RunLoaded(), Is.EqualTo(50));
        }

        [Test]
        public void OriginalMusicAndOrnamentsRetainSerializationAndAssetOwnership()
        {
            MissionDisplayDefinitionVerification.Run();
        }

        [Test]
        public void OriginalRankDefinitionRetainsIconLifecycleAndSerialization()
        {
            RankDefinitionVerification.Run();
        }

        [Test]
        public void OriginalMissionFieldLeavesRetainKeysListsAndUnitySerialization()
        {
            MissionFieldLeavesVerification.Run();
        }

        [Test]
        public void OriginalHashEnumAttributeRetainsPlayerConstructorContract()
        {
            Assert.That(HashEnumAttributeVerification.RunManaged(), Is.EqualTo(12));
        }

        [Test]
        public void OriginalBindableRetainsNativeNotificationBindingAndComparisonOrder()
        {
            BindableVerification.Run();
        }

        [Test]
        public void OriginalProgressionWidgetRetainsRealBaseFieldsAndCallbacks()
        {
            UIWidgetProgressionVerification.Run();
        }

        [Test]
        public void OriginalProgressionWidgetRetainsGenuineUnitySerializationAndRuntimeFields()
        {
            UIWidgetProgressionSerializationVerification.Run();
        }

        [Test]
        public void OriginalManagedAssetRetainsNativeClosureAndReferenceCounting()
        {
            ManagedAddressableVerification.Run();
        }

        [Test]
        public void OriginalManagedAssetUsesGenuineEngineCompletionAndRelease()
        {
            ManagedAddressableUnityVerification.Run();
        }

        [Test]
        public void OriginalInspectorConditionsRetainAttributesAndComparisonOrder()
        {
            InspectorConditionVerification.Run();
        }

        [Test]
        public void OriginalZoneThemeRetainsUnityLifecycleAndAssignmentOrder()
        {
            ZoneThemeVerification.Run();
        }

        [Test]
        public void OriginalRewardRequirementRetainsFieldAndJsonIdentity()
        {
            RewardRequirementVerification.Run();
        }

        [Test]
        public void ReadOnlyListsPreserveNativeComparisonMutationAndEnumerationOrder()
        {
            ReadOnlyListVerification.Run();
        }

        [Test]
        public void OriginalGameplayUiDefinitionRetainsAbstractContractsAndGuidBase()
        {
            GameplayLevelUIEntryVerification.Run();
        }

        [Test]
        public void OriginalSceneAuthoringAttributesRetainInheritedUsageAndValues()
        {
            SceneAuthoringAttributeVerification.Run();
        }

        [Test]
        public void OriginalSystemSceneDefinitionsBindAndRoundtripWithoutIdentityChanges()
        {
            LevelDefinitionAssetVerification.Run();
        }

        [Test]
        public void OriginalBootSceneUnloadRetainsFactoryAndNativeOperation()
        {
            BootSceneUnloadVerification.Run();
        }

        [Test]
        public void OriginalLevelDefinitionsRetainSceneNameAndNativeRuntimeContract()
        {
            LevelDefinitionVerification.Run();
        }

        [Test]
        public void StackableConfigurationPreservesNativeOrderCacheAndOperations()
        {
            StackablePrimitiveVerification.Run();
            StackableDataVerification.Run();
        }

        [Test]
        public void OriginalConfigurationProviderRetainsUnityConstructionAndLookup()
        {
            SystemConfigurationVerification.Run();
        }

        [Test]
        public void EnumStringRegistryRetainsCompleteNativeTablesAndMutableMisses()
        {
            HardlightEnumStringsVerification.Run();
        }

        [Test]
        public void OriginalBootPersistenceStatesRetainKeysSaveAndShutdownOrder()
        {
            BootPersistenceStateVerification.Run();
        }

        [Test]
        public void OriginalRuntimeDelegatesStayOutsideUnitySerialization()
        {
            DelegateSerializationVerification.Run();
        }

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
