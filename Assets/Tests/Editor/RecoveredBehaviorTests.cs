using NUnit.Framework;

namespace ProjectLucid.Tests
{
    public sealed class RecoveredBehaviorTests
    {
        [Test]
        public void OriginalCharacterConstantsAndControlsRetainDefaultsAndCallbackOrder()
        {
            Assert.That(CharacterControlsVerification.RunManaged(), Is.EqualTo(60));
            Assert.That(CharacterControlsVerification.RunEngine(), Is.EqualTo(18));
        }

        [Test]
        public void OriginalGlyphMapsRetainFallbacksAndSerializedCallbacks()
        {
            Assert.That(GlyphStartupVerification.RunManaged(), Is.EqualTo(110));
            Assert.That(GlyphStartupVerification.RunEngine(), Is.EqualTo(14));
        }

        [Test]
        public void OriginalUIStartupRetainsRegistryVisibilityAndTransitionOrder()
        {
            Assert.That(UIStartupVerification.RunManaged(), Is.EqualTo(115));
        }

        [Test]
        public void OriginalUICollectionsRetainIterationLookupAndCleanupQuirks()
        {
            Assert.That(UICollectionsVerification.RunManaged(), Is.EqualTo(89));
        }

        [Test]
        public void OriginalUIManagersRetainIdentifierGuardsAndCloseDispatch()
        {
            Assert.That(UIManagerFamilyVerification.RunManaged(), Is.EqualTo(34));
        }

        [Test]
        public void OriginalActorFSMKeysRetainStorageLiteralsAndAliases()
        {
            Assert.That(ActorFSMKeyVerification.RunManaged(), Is.EqualTo(1217));
        }

        [Test]
        public void OriginalAudioSourcesRetainClipStateFadeAndDelayOrder()
        {
            Assert.That(AudioSourceRuntimeVerification.RunManaged(), Is.EqualTo(63));
            Assert.That(AudioSourceRuntimeVerification.RunEngine(), Is.EqualTo(23));
        }

        [Test]
        public void OriginalUIContainersRetainReferenceCountsCanvasAndParameters()
        {
            UIContainerFoundationVerification.Run();
            var count = typeof(UIContainerFoundationVerification).GetField("checks",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(count.GetValue(null), Is.EqualTo(73));
        }

        [Test]
        public void OriginalAudioDefinitionsRetainClipDefaultsMixerConversionAndJson()
        {
            Assert.That(ActorAudioDefinitionVerification.RunManaged(), Is.EqualTo(52));
            Assert.That(ActorAudioDefinitionVerification.RunEngine(), Is.EqualTo(13));
        }

        [Test]
        public void OriginalAnalyticsDefinitionsRetainBandsEventsAndResourceOrder()
        {
            AnalyticsDefinitionVerification.Run();
            var count = typeof(AnalyticsDefinitionVerification).GetField("checks",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(count.GetValue(null), Is.EqualTo(79));
        }

        [Test]
        public void OriginalActorDefinitionsRetainCachesWeightedAudioAndUnityDefaults()
        {
            Assert.That(ActorDefinitionVerification.RunManaged(), Is.EqualTo(100));
            Assert.That(ActorDefinitionVerification.RunEngine(), Is.EqualTo(38));
        }

        [Test]
        public void OriginalSplashScreenRetainsSelectionTimelineAndCallbackOrder()
        {
            SplashScreenVerification.Run();
            var count = typeof(SplashScreenVerification).GetField("checks",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(count.GetValue(null), Is.EqualTo(50));
        }

        [Test]
        public void OriginalSystemReferencesRetainStartupCallbacksAndUnityNullRules()
        {
            SystemRefVerification.Run();
            var count = typeof(SystemRefVerification).GetField("checks",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(count.GetValue(null), Is.EqualTo(83));
        }

        [Test]
        public void OriginalCheckedSystemLookupRetainsCapturedReferenceAndDiagnosticOrder()
        {
            ProcessManagerCheckedGetVerification.Run();
            var count = typeof(ProcessManagerCheckedGetVerification).GetField("checks",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(count.GetValue(null), Is.EqualTo(55));
        }

        [Test]
        public void OriginalMessageExchangeRetainsWeakIdentityMutationAndEngineReferences()
        {
            Assert.That(MessageExchangeVerification.RunManaged(), Is.EqualTo(125));
            Assert.That(MessageExchangeVerification.RunActualUnityObjects(), Is.EqualTo(9));
        }

        [Test]
        public void OriginalCharacterBrainRetainsDeferredActionsImpulseAndTimestampOrder()
        {
            Assert.That(CharacterBrainVerification.RunManaged(), Is.EqualTo(725));
            Assert.That(CharacterBrainVerification.RunEngineJson(), Is.EqualTo(4));
        }

        [Test]
        public void OriginalActorAnimationDefinitionsRetainParameterAndIntervalSemantics()
        {
            Assert.That(ActorAnimationVerification.RunManaged(), Is.EqualTo(358));
            Assert.That(ActorAnimationVerification.RunEngine(), Is.EqualTo(23));
        }

        [Test]
        public void OriginalStringTableRetainsLoadingCallbacksAndUnityLifecycle()
        {
            StringTableVerification.Run();
            var count = typeof(StringTableVerification).GetField("checks",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(count.GetValue(null), Is.EqualTo(169));
        }

        [Test]
        public void SuppliedLanguagesLoadThroughOriginalTableAndCallbacks()
        {
            Assert.That(BundledStringTableVerification.Run(), Is.EqualTo(17));
        }

        [Test]
        public void OriginalLanguageRetainsSavedOverridesAndUnityLifecycle()
        {
            LanguageVerification.Run();
            var count = typeof(LanguageVerification).GetField("checks",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(count.GetValue(null), Is.EqualTo(193));
        }

        [Test]
        public void OriginalLocalisationConfigurationRetainsFieldsAndValidationOrder()
        {
            Assert.That(LocalisationFoundationVerification.RunManaged(), Is.EqualTo(93));
        }

        [Test]
        public void OriginalLocalisationEnumsRetainRegistriesComparersAndMutableMisses()
        {
            Assert.That(LocalisationEnumVerification.RunManaged(), Is.EqualTo(8110));
        }

        [Test]
        public void OriginalStringTablePrerequisitesRetainVersionAndCoroutineOrder()
        {
            Assert.That(StringTablePrerequisitesVerification.RunManaged(), Is.EqualTo(137));
        }

        [Test]
        public void SuppliedLanguageFilesRoundtripThroughOriginalCodecs()
        {
            Assert.That(ClientDataAPIVerification.RunBundledData(), Is.EqualTo(17));
        }

        [Test]
        public void OriginalClientDataAPIRetainsNativeCodecPoolsAndPartialFailures()
        {
            Assert.That(ClientDataAPIVerification.RunManaged(), Is.EqualTo(300));
        }

        [Test]
        public void OriginalNetworkBufferRetainsCursorVarintAndUtf8Failures()
        {
            Assert.That(NetworkBufferVerification.RunManaged(), Is.EqualTo(165));
        }

        [Test]
        public void CodecMethodFlagsAreRestoredByTheActualUnityCompilerPipeline()
        {
            // Preserve the complete module, including thirteen localisation
            // registry methods, thirty-five original UI registry/comparer methods,
            // and Unity's two MonoScript generator methods.
            Assert.That(CodecPipelineVerification.Run(), Is.EqualTo(698));
        }

        [Test]
        public void OriginalTimeSchedulerRetainsCategoryPauseCallbackAndEngineOrder()
        {
            Assert.That(TimeSchedulerVerification.Run(), Is.EqualTo(532));
        }

        [Test]
        public void OriginalSerializableDictionaryRetainsCallbackAndUnitySerializationOrder()
        {
            Assert.That(SerializableDictionaryVerification.Run(), Is.EqualTo(146));
        }

        [Test]
        public void OriginalActorCollisionLeavesRetainMovementCacheAndTransformOrder()
        {
            Assert.That(ActorCollisionLeavesVerification.RunManaged(), Is.EqualTo(171));
            Assert.That(ActorCollisionLeavesVerification.RunEngine(), Is.EqualTo(13));
        }

        [Test]
        public void OriginalLoggingHostRetainsNativeRoutingAndEngineLifecycle()
        {
            Assert.That(LoggingHostVerification.RunManaged(), Is.EqualTo(137));
            Assert.That(LoggingHostVerification.RunEngine(), Is.EqualTo(41));
        }

        [Test]
        public void OriginalLoggingSingletonRetainsRegistrationAndCallbackOrder()
        {
            Assert.That(LoggingSingletonVerification.RunManaged(), Is.EqualTo(75));
        }

        [Test]
        public void OriginalLoggingConfigurationRetainsFieldsAndUnityObjectLifetimes()
        {
            LoggingConfigurationVerification.Run();
            var count = typeof(LoggingConfigurationVerification).GetField("checks",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(count.GetValue(null), Is.EqualTo(65));
        }

        [Test]
        public void OriginalBugInfoRetainsIdentifierEqualityAndNullFailures()
        {
            Assert.That(BugInfoVerification.RunManaged(), Is.EqualTo(35));
        }

        [Test]
        public void OriginalFastActionsRetainDelegateMutationAndExceptionOrder()
        {
            Assert.That(FastActionDispatchVerification.RunManaged(), Is.EqualTo(82));
        }

        [Test]
        public void OriginalSaveRecordsRetainNativeMergeCopyAndUnitySerialization()
        {
            Assert.That(SaveRecordProgressionVerification.Run(), Is.EqualTo(126));
        }

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
