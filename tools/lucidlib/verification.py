"""Generate test receipts and compare them with the current project content."""

from __future__ import annotations

import base64
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import subprocess
import uuid
import xml.etree.ElementTree as ET

from .bootstrap import managed_path, write_json

UNITY_VERSION = "2022.3.54f1"
EDITMODE_TESTS = tuple("ProjectLucid.Tests.RecoveredBehaviorTests." + name for name in (
    "OriginalLevelManagedSystemsRetainOwnershipAndCallbackOrder",
    "OriginalSurfaceEventsRetainPersistentCallbacksAndIteratorOrder",
    "OriginalPrefabPoolValuesRetainContractsAndDefaults",
    "OriginalPrefabPoolRetainsReuseResizeAndDeferredReturnOrder",
    "OriginalEditableRibbonKnotsRetainInitializationAndCacheRules",
    "OriginalEditableRibbonProviderRetainsGeometryAndSurfaceQueries",
    "OriginalRibbonRuntimeRetainsEdgeRulesAndNaturalDistanceComparer",
    "OriginalInputBindingsRetainModifierNamesAndProviderContracts",
    "OriginalSplineRuntimeRetainsNativeProjectionAndUnorderedDistanceRules",
    "OriginalInputDebounceRetainsThresholdDirectionAndCrossingTiming",
    "OriginalSplinePrerequisitesRetainRecordsComparersAndCallbacks",
    "OriginalKnotRuntimePrerequisitesRetainContractsAndReceiverFaults",
    "OriginalSplineRenderDefinitionsRetainQualitySelectionAndUnitySerialization",
    "OriginalSurfaceEditorStylesCopyTheGenuineSkinInOnGUI",
    "OriginalRibbonBoundsRetainCornerOrderScratchTailAndTransforms",
    "OriginalCameraAxisAndRecenterRetainLookupDefaultsAndInitialization",
    "OriginalSplineMathRetainsCachingApproximationAndAliasedOutputs",
    "OriginalRibbonPrerequisitesRetainTransformsContractsAndMarkerOrder",
    "OriginalSurfacePhysicsAndResolverRetainOrderedQueriesAndProfilerConstruction",
    "RibbonAccessorMarkersAreRestoredByTheActualUnityCompilerPipeline",
    "OriginalGravityDescriptionRetainsAuthoredLimitsAndQueryOrder",
    "OriginalGravitySurfacesRetainTypedKeysAndUnitySerialization",
    "OriginalSurfaceTrackingRetainsCallbackAndMeshBoundsOrder",
    "OriginalColliderQueriesRetainProjectionFilteringAndWorldGeometry",
    "OriginalInstancedPoolsRetainMeshCloningRoutingAndManagerLifecycle",
    "OriginalHalfPipeTrajectoryRetainsAuthoredCurveAndUnitySerialization",
    "OriginalTerrainMetadataRetainsKeysRawEnumsAndUnitySerialization",
    "OriginalTrackerDefinitionsRetainInheritedKeysCurveAndUnitySerialization",
    "OriginalTrackerGroupsRetainTypedKeysComparersAndAuthoredArrays",
    "OriginalUIVisibilityOverridesRetainRegistryRowsAndHandleCallbackOrder",
    "OriginalTerrainEffectsRetainAnimationReferencesSquareCacheAndCallbacks",
    "OriginalSplineMetadataRetainsLookupInterpolationAndPartialCacheRules",
    "OriginalCharacterSkinDefinitionsRetainRenderWrappersAndOverrideDefaults",
    "OriginalCharacterArchetypeDefinitionsRetainAtlasWrappersGroupKeysAndCallbacks",
    "OriginalFullscreenDefinitionsRetainCurveWeightsLimitsAndTextureOverrides",
    "OriginalCollectableStateAndTierRetainWrappedCountsClampsAndDefaults",
    "OriginalUIBackedDefinitionsRetainAtlasReferencesCacheCountsAndReleaseOrder",
    "OriginalCameraDefinitionsRetainRecenterDefaultsCachedAnglesAndTypedOverrides",
    "OriginalRenderAndTargetDefinitionsRetainDefaultsDrawOrderAndMaterialQuirks",
    "OriginalSequenceDefinitionsRetainKeysDefaultsAndUnitySerialization",
    "OriginalCharacterDamageDefinitionsRetainDefaultsContractsAndCollectableChanges",
    "OriginalProgressionDefinitionsRetainStreaksGuidLookupAndWaypointSettings",
    "OriginalDataManagerDefinitionsRetainKeysDefaultsAndUnitySerialization",
    "OriginalTouchLayoutsRetainLookupFallbackAndSerializedDefaults",
    "OriginalVisualProxiesRetainRotationGatesAndSpacingOrder",
    "OriginalBlobShadowRetainsGravityRaycastAndEnableOrder",
    "OriginalPerformanceProfilesRetainFeatureAndThresholdRules",
    "OriginalDeviceProfilesRetainPlatformMatchingAndFallbacks",
    "OriginalCharacterConstantsAndControlsRetainDefaultsAndCallbackOrder",
    "OriginalGlyphMapsRetainFallbacksAndSerializedCallbacks",
    "OriginalUIStartupRetainsRegistryVisibilityAndTransitionOrder",
    "OriginalUICollectionsRetainIterationLookupAndCleanupQuirks",
    "OriginalUIManagersRetainIdentifierGuardsAndCloseDispatch",
    "OriginalActorFSMKeysRetainStorageLiteralsAndAliases",
    "OriginalAudioSourcesRetainClipStateFadeAndDelayOrder",
    "OriginalUIContainersRetainReferenceCountsCanvasAndParameters",
    "OriginalAudioDefinitionsRetainClipDefaultsMixerConversionAndJson",
    "OriginalAnalyticsDefinitionsRetainBandsEventsAndResourceOrder",
    "OriginalActorDefinitionsRetainCachesWeightedAudioAndUnityDefaults",
    "OriginalSplashScreenRetainsSelectionTimelineAndCallbackOrder",
    "OriginalSystemReferencesRetainStartupCallbacksAndUnityNullRules",
    "OriginalCheckedSystemLookupRetainsCapturedReferenceAndDiagnosticOrder",
    "OriginalMessageExchangeRetainsWeakIdentityMutationAndEngineReferences",
    "OriginalCharacterBrainRetainsDeferredActionsImpulseAndTimestampOrder",
    "OriginalActorAnimationDefinitionsRetainParameterAndIntervalSemantics",
    "OriginalStringTableRetainsLoadingCallbacksAndUnityLifecycle",
    "SuppliedLanguagesLoadThroughOriginalTableAndCallbacks",
    "OriginalLanguageRetainsSavedOverridesAndUnityLifecycle",
    "OriginalLocalisationConfigurationRetainsFieldsAndValidationOrder",
    "OriginalLocalisationEnumsRetainRegistriesComparersAndMutableMisses",
    "OriginalStringTablePrerequisitesRetainVersionAndCoroutineOrder",
    "SuppliedLanguageFilesRoundtripThroughOriginalCodecs",
    "OriginalClientDataAPIRetainsNativeCodecPoolsAndPartialFailures",
    "OriginalNetworkBufferRetainsCursorVarintAndUtf8Failures",
    "CodecMethodFlagsAreRestoredByTheActualUnityCompilerPipeline",
    "CodecVerificationRetryAfterOutputFailureCountsOnlyItsOwnChecks",
    "OriginalTimeScaledComponentRetainsCallbacksAndTimingPhases",
    "OriginalTimeSchedulerRetainsCategoryPauseCallbackAndEngineOrder",
    "OriginalSerializableDictionaryRetainsCallbackAndUnitySerializationOrder",
    "OriginalActorCollisionLeavesRetainMovementCacheAndTransformOrder",
    "OriginalLoggingHostRetainsNativeRoutingAndEngineLifecycle",
    "OriginalLoggingSingletonRetainsRegistrationAndCallbackOrder",
    "OriginalLoggingConfigurationRetainsFieldsAndUnityObjectLifetimes",
    "OriginalBugInfoRetainsIdentifierEqualityAndNullFailures",
    "OriginalFastActionsRetainDelegateMutationAndExceptionOrder",
    "OriginalSaveRecordsRetainNativeMergeCopyAndUnitySerialization",
    "OriginalMathUtilitiesRetainNativeArithmeticAndGenericConversions",
    "OriginalFormTraitsRetainNativeGeometryDefaultsAndSerialization",
    "OriginalPhysicsUtilitiesRetainPickingAndCapsuleSweepGeometry",
    "OriginalVector3UtilitiesRetainGeometryAndEngineInterpolation",
    "OriginalFastActionRetainsExactClassCompilerOptions",
    "OriginalMovementUtilitiesRetainNativeMathAndEngineConversions",
    "LoadedTypeModulesRetainExactDiskAndTokenIdentity",
    "OriginalVersionRetainsNativeParsingComparisonAndSerializedCache",
    "OriginalStateMachineDefinitionsRetainObjectNameKeysAndFailures",
    "OriginalDefinitionGroupsRetainNativeMergeAndSerializationOrder",
    "LoadedAttributeBlobsPreserveBoxedStringNullAndModuleIdentity",
    "OriginalMusicAndOrnamentsRetainSerializationAndAssetOwnership",
    "OriginalRankDefinitionRetainsIconLifecycleAndSerialization",
    "OriginalMissionFieldLeavesRetainKeysListsAndUnitySerialization",
    "OriginalHashEnumAttributeRetainsPlayerConstructorContract",
    "OriginalBindableRetainsNativeNotificationBindingAndComparisonOrder",
    "OriginalProgressionWidgetRetainsRealBaseFieldsAndCallbacks",
    "OriginalProgressionWidgetRetainsGenuineUnitySerializationAndRuntimeFields",
    "OriginalManagedAssetRetainsNativeClosureAndReferenceCounting",
    "OriginalManagedAssetUsesGenuineEngineCompletionAndRelease",
    "OriginalInspectorConditionsRetainAttributesAndComparisonOrder",
    "OriginalZoneThemeRetainsUnityLifecycleAndAssignmentOrder",
    "OriginalRewardRequirementRetainsFieldAndJsonIdentity",
    "ReadOnlyListsPreserveNativeComparisonMutationAndEnumerationOrder",
    "OriginalGameplayUiDefinitionRetainsAbstractContractsAndGuidBase",
    "OriginalSceneAuthoringAttributesRetainInheritedUsageAndValues",
    "OriginalSystemSceneDefinitionsBindAndRoundtripWithoutIdentityChanges",
    "OriginalBootSceneUnloadRetainsFactoryAndNativeOperation",
    "OriginalLevelDefinitionsRetainSceneNameAndNativeRuntimeContract",
    "StackableConfigurationPreservesNativeOrderCacheAndOperations",
    "OriginalConfigurationProviderRetainsUnityConstructionAndLookup",
    "EnumStringRegistryRetainsCompleteNativeTablesAndMutableMisses",
    "OriginalBootPersistenceStatesRetainKeysSaveAndShutdownOrder",
    "OriginalRuntimeDelegatesStayOutsideUnitySerialization",
    "OriginalStartingPointDefinitionsBindToMaintainedGuidType",
    "ScriptableGuidRetainsOriginalSerializationAndUnityNullRules",
    "CoroutineCallbacksRetainOriginalNextFrameOrdering",
    "EnumComparersRetainNativeHashesAndStaticIdentities",
    "ProgressRecordsRetainOriginalGraphAndUnityJson",
    "StateMachineStorageConditionsPreserveNativeTypeAndTimingSemantics",
    "MeshGeometryMatchesNativeStoresAndIndexBlob",
    "StateMachinePrimitivesMatchNativeHashAndIdentitySemantics",
    "StateMachineTransitionsPreserveNativeUpdateAndCallbackOrder",
    "StateMachineComponentsRetainOriginalUserAndLifecycleOrder",
    "StateMachineCompletionRetainsOriginalEndAndFinishedRules",
    "LocalStoragePersistsAndRecoversWithoutCloudNotifications",
    "CloudFacadePreservesOriginalCallbacksKeysAndConflictLifecycle",
    "SavePropertiesPreserveNativeIdentityAndCultureSemantics",
    "PropertyStoreRetainsNativeLifecycleAndRecoversLocalFiles",
    "SaveDataRetainsOriginalDefaultsDirtyChildrenAndJson",
    "ScriptPointersRequireExactLoadedAssetAndComponentIdentities",
    "SuppliedLayoutComponentsRetainAuthoredScalarsAfterScriptBinding",
    "UpstreamLayoutSizingPreservesPriorityFitAndAspectRules",
    "OriginalPipelineMarkerRetainsEngineFieldsAfterScriptBinding",
    "TimeConversionsRetainOriginalUnitsRoundingAndDateKinds",
    "SkyCubemapGpuOutputMatchesDecodedMetalCases",
    "EggmanLogoGpuOutputMatchesDecodedColorBlendAndLayerCases"))
EDITMODE_TESTS += ("ProjectLucid.Tests.ArtifactIdentityTests.SharedFingerprintIncludesAllPreparedAssetDirectories",)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalEncryptedSavePreservationTests.OriginalEncryptedSaveRetainsIndependentCbcZeroPaddingVectors",
    "ProjectLucid.Tests.OriginalEncryptedSavePreservationTests.OriginalEncryptedSaveRetainsUtf8ConstructorBoundaries",
    "ProjectLucid.Tests.OriginalEncryptedSavePreservationTests.OriginalEncryptedSaveRetainsDecryptCatchBoundaries",
    "ProjectLucid.Tests.OriginalEncryptedSavePreservationTests.OriginalPropertyStoreDefaultConstructorSelectsOfflineStorage",
    "ProjectLucid.Tests.OriginalCloudBehaviorTests.OriginalEditorCloudRetainsConstantGettersAndDiscardedWrites",
    "ProjectLucid.Tests.OriginalCloudBehaviorTests.OriginalEditorCloudRetainsLiveKeysAndStaleCaptures",
    "ProjectLucid.Tests.OriginalCloudBehaviorTests.OriginalCloudNotificationsUseRealUserInfoJsonAndReceiverOrder",
    "ProjectLucid.Tests.OriginalCloudBehaviorTests.OriginalCloudConnectCallbacksRetainDelegateCombineRemoveOrder",
    "ProjectLucid.Tests.OriginalCloudBehaviorTests.OriginalCloudFactorySelectsOfflineWithoutConstructingAppleProvider",
    "ProjectLucid.Tests.OriginalCloudBehaviorTests.OriginalMacCooldownRetainsRealSuppliedWaitIterator",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalAddressableManagerBehaviorTests.OriginalAddressableManagerRetainsManagedCacheAndIteratorContracts",
    "ProjectLucid.Tests.OriginalAddressableManagerBehaviorTests.OriginalAddressableManagerRetainsRealPackageHandleAndCallbackContracts",
    "ProjectLucid.Tests.OriginalAddressableManagerBehaviorTests.OriginalAddressableManagerRetainsRealComponentCloneAndFailureContracts",
    "ProjectLucid.Tests.OriginalCharacterTransformPreservationTests.OriginalReversalPredicatesRetainStrictNegativeMath",
    "ProjectLucid.Tests.OriginalCharacterTransformPreservationTests.OriginalReversalChunkRetainsDocumentedDeclarationScope",
    "ProjectLucid.Tests.OriginalCharacterTransformPreservationTests.OriginalTransformUtilityRetainsAllElevenDeclarations",
    "ProjectLucid.Tests.OriginalCharacterTransformPreservationTests.OriginalTransformLocalOperationsRetainCopyResetAndParentValues",
    "ProjectLucid.Tests.OriginalCharacterTransformPreservationTests.OriginalTransformHierarchyRetainsAncestorPathsAndWholeVectorScale",
    "ProjectLucid.Tests.OriginalCharacterTransformPreservationTests.OriginalTransformGeometryRetainsLocalRectsAndFourScreenCorners",
    "ProjectLucid.Tests.OriginalCharacterTransformPreservationTests.OriginalTransformChildrenRetainCollectionAndSiblingSortOrdering",
    "ProjectLucid.Tests.OriginalCharacterTransformPreservationTests.OriginalTransformNullAndDestroyedObjectsRetainBranchBoundaries",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalAnimationCurvePreservationTests.OriginalCurveHelperRetainsBothDeclarationsAndDefaults",
    "ProjectLucid.Tests.OriginalAnimationCurvePreservationTests.OriginalCurveHelperRetainsNullFailureOrdering",
    "ProjectLucid.Tests.OriginalAnimationCurvePreservationTests.OriginalCurveHelperRetainsCurrentFinalKeyTimes",
    "ProjectLucid.Tests.OriginalAnimationCurvePreservationTests.OriginalCurveAreaRetainsStepFormulaAndBoundaryDecisions",
    "ProjectLucid.Tests.OriginalStartingPositionPreservationTests.OriginalStartingPositionRetainsCompleteSerializedShape",
    "ProjectLucid.Tests.OriginalStartingPositionPreservationTests.OriginalStartingPositionRetainsDefaultReferences",
    "ProjectLucid.Tests.OriginalStartingPositionPreservationTests.OriginalStartingPositionRetainsAuthoredReferencesAcrossSetData",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalLedgeBrakePreservationTests.OriginalLedgeBrakingCachesRetainAuthoredAnglesAndExceptionalValues",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalMessageCompletionPreservationTests.FullOriginalCompletionApiRetainsSignaturesFieldsFlagsAndReadonlyArguments",
    "ProjectLucid.Tests.OriginalMessageCompletionPreservationTests.OriginalCallbacksPoolingMutationFaultsAndAllAritiesArePreserved",
    "ProjectLucid.Tests.OriginalMessageCompletionPreservationTests.OriginalTimeoutsCompleteWithoutBlockingTheUnitySynchronizationContext",
    "ProjectLucid.Tests.OriginalWaypointPreservationTests.OriginalWaypointDeclarationsPreserveFieldsOptionsAndCallbackContract",
    "ProjectLucid.Tests.OriginalWaypointPreservationTests.OriginalWaypointsPreserveOverridesDeferredLifecycleAndFailureOrdering",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalSerializedDictionaryFamilyPreservationTests.OriginalDictionaryGenericShapeAndAttributesRemainComplete",
    "ProjectLucid.Tests.OriginalSerializedDictionaryFamilyPreservationTests.OriginalReferenceDictionaryConversionsPreserveNullsAndValueTypes",
    "ProjectLucid.Tests.OriginalSerializedDictionaryFamilyPreservationTests.OriginalListPairRetainsMutationOrderAndCopyBoundaries",
    "ProjectLucid.Tests.OriginalSerializedDictionaryFamilyPreservationTests.OriginalListDictionaryPreservesCopiesAndPartialFailureState",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalBoundsOctreePreservationTests.OriginalOctreeDeclarationsRemainComplete",
    "ProjectLucid.Tests.OriginalBoundsOctreePreservationTests.OriginalOctantsRetainEqualityAndUnorderedChoices",
    "ProjectLucid.Tests.OriginalBoundsOctreePreservationTests.OriginalConstructionRetainsBoundsAndChildAliases",
    "ProjectLucid.Tests.OriginalBoundsOctreePreservationTests.OriginalInsertionAndCollisionQueriesRetainOrderingAndFaults",
    "ProjectLucid.Tests.OriginalBoundsOctreePreservationTests.OriginalReverseMigrationRetainsEqualityFailureState",
    "ProjectLucid.Tests.OriginalBoundsOctreePreservationTests.OriginalGrowthRetainsObjectsAndBoundedAbortMutations",
    "ProjectLucid.Tests.OriginalBoundsOctreePreservationTests.OriginalFrustumQueriesRetainGeometryAndPartialResults",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalTimeScaledUtilitiesPreservationTests.OriginalTimingDeclarationsAbsentManagerAndDelayedActionsRemainPreserved",
    "ProjectLucid.Tests.OriginalTimeScaledUtilitiesPreservationTests.OriginalTimingPresentManagerRetainsInactiveAndUnorderedDurationOrdering",
    "ProjectLucid.Tests.OriginalTimeScaledUtilitiesPreservationTests.OriginalTimingFixedYieldsRetainCapturedManagerAndActualCategoryScales",
    "ProjectLucid.Tests.OriginalTimeScaledUtilitiesPreservationTests.OriginalTimingFrameYieldsUseActualUnityDeltaAndUnorderedTimerSemantics",
    "ProjectLucid.Tests.OriginalTimeScaledUtilitiesPreservationTests.OriginalTimingPredicateAndNestedDelayFaultsPreserveReentryAndEmptyDispose",
    "ProjectLucid.Tests.OriginalGameTimeScaledUtilitiesPreservationTests.OriginalGameWaitDefersLookupAndRefreshesDestroyedConfiguration",
    "ProjectLucid.Tests.OriginalGameTimeScaledUtilitiesPreservationTests.OriginalGameTimingRetainsImmediateAndDeferredFaultPrefixes",
    "ProjectLucid.Tests.OriginalLayerComparisonPreservationTests.OriginalLayerAndComparisonOwnersRetainAllDeclarations",
    "ProjectLucid.Tests.OriginalLayerComparisonPreservationTests.OriginalLayerOperationsRetainSignedAndWrappedBits",
    "ProjectLucid.Tests.OriginalLayerComparisonPreservationTests.OriginalLayerObjectOverloadReadsRealAuthoredLayers",
    "ProjectLucid.Tests.OriginalLayerComparisonPreservationTests.OriginalFloatComparisonsRetainApproximationAndExceptionalValues",
    "ProjectLucid.Tests.OriginalLayerComparisonPreservationTests.OriginalIntegerComparisonsRetainSignedEndpoints",
    "ProjectLucid.Tests.OriginalLayerComparisonPreservationTests.OriginalUnknownComparisonsRetainTypedErrorsAndMessages",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalSaveRecordMergePreservationTests.OriginalSaveRecordsRetainCompleteDeclarationsAndSignedDefault",
    "ProjectLucid.Tests.OriginalSaveRecordMergePreservationTests.OriginalUnlockAndSeenMergesRetainFlagsAndRawDirtyState",
    "ProjectLucid.Tests.OriginalSaveRecordMergePreservationTests.OriginalStatsRetainZeroSignedOverflowAndMaximumMerge",
    "ProjectLucid.Tests.OriginalSaveRecordMergePreservationTests.OriginalBonusAndStoreStatesRetainUnknownEnumTransitions",
    "ProjectLucid.Tests.OriginalSaveRecordMergePreservationTests.OriginalCustomisationRetainsManagedGUIDFaultOrdering",
    "ProjectLucid.Tests.OriginalSaveRecordMergePreservationTests.OriginalCustomisationRetainsActualUnityDestroyedObjectGUIDPath",
    "ProjectLucid.Tests.OriginalSaveRecordMergePreservationTests.OriginalStoreCreationRetainsIdentityNullKeysAndDirtyState",
    "ProjectLucid.Tests.OriginalSaveRecordMergePreservationTests.OriginalStoreMergeRetainsAliasingSelfMergeAndPartialFaults",
    "ProjectLucid.Tests.OriginalSaveRecordMergePreservationTests.OriginalStoreCallbacksRetainDuplicatesAndFailurePrefixes",
    "ProjectLucid.Tests.OriginalSaveRecordMergePreservationTests.OriginalSaveRecordsRetainActualUnitySerializationBoundaries",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalArrayExtensionsPreservationTests.OriginalArrayExtensionsRetainCompleteDeclarations",
    "ProjectLucid.Tests.OriginalArrayExtensionsPreservationTests.OriginalArrayValidityAndContainsRetainNullAndEqualityRules",
    "ProjectLucid.Tests.OriginalArrayExtensionsPreservationTests.OriginalArrayDelegateConversionsRetainOrderAndFailurePrefixes",
    "ProjectLucid.Tests.OriginalArrayExtensionsPreservationTests.OriginalArrayImplicitConversionsRetainEnumAndCultureRules",
    "ProjectLucid.Tests.OriginalArrayExtensionsPreservationTests.OriginalArrayTryConversionsRetainFailureAndOutputRules",
    "ProjectLucid.Tests.OriginalArrayExtensionsPreservationTests.OriginalArraySwapsRetainEqualIndexAndFaultOrdering",
    "ProjectLucid.Tests.OriginalArrayExtensionsPreservationTests.OriginalArrayValidityRetainsDestroyedUnityWrappers",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalObjectExtensionsPreservationTests.OriginalObjectExtensionsRetainCompleteDeclarations",
    "ProjectLucid.Tests.OriginalObjectExtensionsPreservationTests.OriginalComponentQueriesRetainMissingAndInactiveResults",
    "ProjectLucid.Tests.OriginalObjectExtensionsPreservationTests.OriginalGameObjectQueriesRetainCreationAndExistingIdentity",
    "ProjectLucid.Tests.OriginalObjectExtensionsPreservationTests.OriginalImmediateObjectDestructionRetainsUnityWrappers",
    "ProjectLucid.Tests.OriginalObjectExtensionsPreservationTests.OriginalChildRemovalRetainsReverseHierarchyBehavior",
    "ProjectLucid.Tests.OriginalObjectExtensionsPreservationTests.OriginalComponentRemovalRetainsRequirementsAndExclusions",
    "ProjectLucid.Tests.OriginalObjectExtensionsPreservationTests.OriginalHierarchyPathRetainsNamesAndCurrentParents",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalGameCenterLocalPlayerPreservationTests.OriginalShippingGameCenterStubRetainsIdentityAndCallbackRules",
    "ProjectLucid.Tests.OriginalGameCenterLocalPlayerPreservationTests.OriginalGameCenterPlayerRetainsParsingAndIdentifierRules",
    "ProjectLucid.Tests.OriginalGameCenterLocalPlayerPreservationTests.OriginalGameCenterLocalPlayerRetainsAuthenticationAndCacheOrder",
    "ProjectLucid.Tests.OriginalGameCenterLocalPlayerPreservationTests.OriginalShippingGameCenterPhotoCoroutineRetainsWhiteTextureAndCleanup",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalLeaderboardPreservationTests.OriginalLeaderboardRecordsRetainParsingCultureAndIdentity",
    "ProjectLucid.Tests.OriginalLeaderboardPreservationTests.OriginalShippingLeaderboardRequestsRetainRecurrenceAndScoreCleanup",
    "ProjectLucid.Tests.OriginalLeaderboardPreservationTests.OriginalLeaderboardEntryRequestsRetainRanksAndStartedSubscriptionMismatch",
    "ProjectLucid.Tests.OriginalLeaderboardPreservationTests.OriginalLeaderboardFaultedIteratorsRetainEmptyDisposal",
    "ProjectLucid.Tests.OriginalLeaderboardPreservationTests.OriginalLeaderboardManagedCallbacksRetainSharedMapRouting",
    "ProjectLucid.Tests.OriginalLeaderboardPreservationTests.OriginalShippingLeaderboardImageRequestRetainsWhiteTextureAndCleanup",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalJsonHttpPreservationTests.OriginalJsonScalarConversionsRetainTypesDefaultsAndPoolSizes",
    "ProjectLucid.Tests.OriginalJsonHttpPreservationTests.OriginalJsonParserRetainsCursorsPermissiveInputAndFaults",
    "ProjectLucid.Tests.OriginalJsonHttpPreservationTests.OriginalJsonSerializationRetainsOrderingEscapesAndDisposal",
    "ProjectLucid.Tests.OriginalJsonHttpPreservationTests.OriginalJsonContainersRetainOwnershipReuseAndReleaseOrder",
    "ProjectLucid.Tests.OriginalJsonHttpPreservationTests.OriginalHttpNullRequestBoundariesRetainCallbacksAndIteratorFaults",
    "ProjectLucid.Tests.OriginalJsonHttpPreservationTests.JsonHttpPreservationFixturesRetainExistingStateAcrossRepeatsAndFaults",
    "ProjectLucid.Tests.OriginalCharacterRigPreservationTests.OriginalRigCacheRetainsExactTypesDuplicateOrderAndPartialFailures",
    "ProjectLucid.Tests.OriginalCharacterRigPreservationTests.OriginalRigAttachmentsRetainWorldPoseAndReverseSiblingOrder",
    "ProjectLucid.Tests.OriginalCharacterRigPreservationTests.OriginalRigPositionsRetainLocalPoseScaleAndAnimatorOrder",
    "ProjectLucid.Tests.OriginalCharacterRigPreservationTests.OriginalRigGhostMaterialsRetainNamesIndexedListsAndPartialFailures",
    "ProjectLucid.Tests.OriginalCharacterRigPreservationTests.OriginalSubstitutionRetainsWorldPoseActivationAndFaultOrder",
    "ProjectLucid.Tests.OriginalCharacterRigPreservationTests.OriginalPersistentSubstitutionRetainsParentAndBypassesRestoration",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalBoundMessagingTests.OriginalBoundMessageOrderFlagsAndMissingKeys",
    "ProjectLucid.Tests.OriginalBoundMessagingTests.OriginalBoundMessageDuplicatesNullAndFaultPrefixes",
    "ProjectLucid.Tests.OriginalBoundMessagingTests.OriginalBoundMessageLiveMutationAndNestedPublish",
    "ProjectLucid.Tests.OriginalBoundMessagingTests.OriginalBoundMessageInvalidationAndNullKeyState",
    "ProjectLucid.Tests.OriginalBoundMessagingTests.OriginalBoundMessageReadonlyHandlesAndParameterCasts",
    "ProjectLucid.Tests.OriginalBoundMessagingTests.OriginalBoundMessageExchangeApisRestoreProcessActions",
    "ProjectLucid.Tests.OriginalUITogglePreservationTests.OriginalUICanvasAndUnconditionalSetter",
    "ProjectLucid.Tests.OriginalUITogglePreservationTests.OriginalUITogglePointerAndFreshGroupCallbacks",
    "ProjectLucid.Tests.OriginalUITogglePreservationTests.OriginalUIMembershipAndSignedSelectionFaults",
    "ProjectLucid.Tests.OriginalUITogglePreservationTests.OriginalUILiveMutationAndSwitchOffFaults",
    "ProjectLucid.Tests.OriginalUITogglePreservationTests.OriginalUIElementLifecycleMembership",
    "ProjectLucid.Tests.OriginalUITogglePreservationTests.OriginalUIAnimationQueueCallbackAndDisableFaults",
    "ProjectLucid.Tests.OriginalUITogglePreservationTests.OriginalUIAnimationPointerAndSettingFaultPrefixes",
)

EDITMODE_TESTS += ('ProjectLucid.Tests.OriginalEnumerablePreservationTests.DeepEqualityPreservesNullFaultAndDisposalOrder', 'ProjectLucid.Tests.OriginalEnumerablePreservationTests.DeepHashPreservesSignedOrderedValuesAndCleanup', 'ProjectLucid.Tests.OriginalEnumerablePreservationTests.JoinPreservesBuilderAliasAndFaultPrefixes', 'ProjectLucid.Tests.OriginalEnumerablePreservationTests.CountPreservesCallbackReceiverMutationAndReentry', 'ProjectLucid.Tests.OriginalEnumerablePreservationTests.ZipPreservesDeferredAcquisitionAndAsymmetricCleanup')

EDITMODE_TESTS += (
    "ProjectLucid.Tests.EditMode.InputUtilitiesMappingTests.OriginalJoystickAxisAndMouseMappingsRemainIntact",
    "ProjectLucid.Tests.EditMode.InputUtilitiesTrackingTests.TrackingCachesPreservePublicationReentrancyAndFailureOrder",
    "ProjectLucid.Tests.EditMode.InputTypeResolverTouchTests.OriginalTouchFlagResolvesWithoutAddingPlatformDetection",
    "ProjectLucid.Tests.EditMode.InputTypeResolverButtonTests.OriginalOverridesAndSignedJoystickFallbacksRemainIntact",
    "ProjectLucid.Tests.EditMode.InputCallbackRoutingTests.OriginalDeviceRoutingFrameConsumptionAndSubscriptionLifetimesRemainIntact",
    "ProjectLucid.Tests.EditMode.InputDisableScopeTests.OriginalDisableScopesCallbacksAndInterruptedShutdownRemainIntact",
    "ProjectLucid.Tests.EditMode.InputMetadataPreservationTests.OriginalInputSlotsAndStrictRefusalsMatchTheLoadedModule",
    "ProjectLucid.Tests.EditMode.AppRatingBehaviorTests.UnsupportedProviderRemainsSelectedAndSilent",
    "ProjectLucid.Tests.EditMode.AppRatingNativeBindingTests.MacOSNativeBindingDeclarationRemainsPreserved",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.FailedCompletionDetachesOwnedEvaluatorAndPreservesThePrimaryError",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.FailedCompletionRetainsBothPrimaryAndRollbackErrorsWhileRevokingReadiness",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.RealLocalCompletionSurvivesDisposalAndAStoreReloadWithoutAnOpenGameplaySlot",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.MalformedOwnedCompletionValueFailsBeforePublishingEntriesOrReadiness",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.OwnedDisposalPreservesASeparatelyNamedRegistrationOfTheSameManager",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.OwnedCompletionFaultPreservesTheSameManagerAliasAndThePrimaryError",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.GenuineLeaderboardStartupFaultPreservesItsSameObjectAliasAndPrimaryError",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.GenuineLeaderboardSubscribedShutdownPreservesItsSameObjectNamedAlias",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalMainMenuFSMTests.SaveLoadFactoryIgnoresJsonAndRegistersAnOwnedOrdinaryState",
    "ProjectLucid.Tests.OriginalMainMenuFSMTests.SaveLoadNullUserFaultsBeforeTheSaveAndCoroutineBoundary",
    "ProjectLucid.Tests.OriginalMainMenuFSMTests.PublisherKeepsOwnedListIdentityOrderNullAndGenuineGuidEquality",
    "ProjectLucid.Tests.OriginalMainMenuFSMTests.PublisherInsertsDefaultNullBeforeItsOriginalListFault",
    "ProjectLucid.Tests.OriginalMainMenuFSMTests.AudioFactoriesCaptureEnumsFlagsAndTheOriginalReplayInitializer",
    "ProjectLucid.Tests.OriginalMainMenuFSMTests.AudioEnumParseFailuresPrecedeOwnedStateRegistration",
    "ProjectLucid.Tests.OriginalMainMenuFSMTests.UIFactoriesRetainNullLookupsAndTheRealGraphUserCastFault",
    "ProjectLucid.Tests.OriginalMainMenuFSMTests.MainMenuFamiliesRetainOriginalDeclarationsAndJsonFieldContracts",
    "ProjectLucid.Tests.OriginalMainMenuFSMTests.SaveCoroutineRetainsTheOriginalNaturalDeclarationShape",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalGuiPageLoaderTests.NormalInitialisationKeepsExactlyTheTwoShippedOverlayMappings",
    "ProjectLucid.Tests.OriginalGuiPageLoaderTests.MissingNumericRoutesReturnBeforeAnySceneRequest",
    "ProjectLucid.Tests.OriginalGuiPageLoaderTests.UnconstrainedGenericConversionFaultsPrecedeSceneLookup",
    "ProjectLucid.Tests.OriginalGuiPageLoaderTests.PermanentUnmappedIdentifierAppendsOnceAndRetainsItsOriginalRecord",
    "ProjectLucid.Tests.OriginalGuiPageLoaderTests.WholeOriginalDeclarationRetainsAllFifteenMethodsAndRealEnumProviders",
    "ProjectLucid.Tests.OriginalGuiPageLoaderTests.OwnedLeaseRestoresBorrowedIdentitiesAfterMutationAndFailure",
)

# Owned local access-point boundary through the preserved original facade.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OfflineAccessPointTests.DefaultFacadeReportsUnavailableHiddenUnfocusedAndNotPresenting",
    "ProjectLucid.Tests.OfflineAccessPointTests.ShowHideFocusAndHighlightsCannotAcquirePlatformState",
    "ProjectLucid.Tests.OfflineAccessPointTests.UnavailableGeometryRemainsStableAfterRepeatedPresentationRequests",
)

EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalFontAssetLanguageTests." + name for name in (
    "FontAuthoredDeclarationsRetainExactOriginalContracts",
    "FontConfigurationNamingAndSerializationUseGenuineOwnedData",
    "FontConfigurationNullCollectionAndEntryRetainPartialNamingFaults",
    "FontStaticWaitEmptyListYieldsBeforeCompletionAndEarlyDisposeDoesNotPublish",
    "FontStaticWaitBoxesInvalidHandlesAndPreservesNullFaultOrder",
))
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalFontReadinessTransitionTests.FontReadinessTransitionRetainsOriginalFactoryAndPrivateOverrideContracts",
    "ProjectLucid.Tests.OriginalFontReadinessTransitionTests.FontReadinessFactoryIgnoresJsonAndUsesTheGenuineOwnedTransitionRegistry",
)

EDITMODE_TESTS += tuple("OriginalBadgeStartTests." + name for name in (
    "BadgeDeclarationsRetainOriginalSerializedFieldsAndOptionalLeaf",
    "BadgeInactiveConstructionRetainsGenuineSerializedDefaults",
    "BadgeNonpositiveFalseAlertsDisableOwnedTargetBeforeTextUse",
    "StartAuthoredDeclarationsRetainOriginalBindingsAndCallbacksWithoutConstruction",
    "StartNaturalIteratorsRetainCompleteRolesWithoutStartingMenuFlow",
    "StartRewardsPublishCollectedBeforeTotalAndUseZerosForMissingEntry",
    "StartRewardsRetainOwnedCallbackAndNullFaultPublicationOrder",
))

PLAYMODE_TESTS = (
    "ProjectLucid.Tests.OfflineStartupTests.ReachesOriginalMainMenu",
    "ProjectLucid.Tests.LocalSaveSlotTests.CreateCopyDeleteAndRestart",
    "ProjectLucid.Tests.OriginalFirstActTests.CompletesOriginalFirstAct",
    "ProjectLucid.Tests.ContentParityTests.AllShippedContentHasVerifiedPlaythroughs",
    "ProjectLucid.Tests.DesktopServiceTests.PlatformCallbacksCompleteOffline",
    "ProjectLucid.Tests.ControlAndCameraTests.OriginalMovementAndCameraMatchReference")


PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalObjectDestructionLifecycleTests.OriginalComponentAndGameObjectDestroyRetainDeferredFrames",
    "ProjectLucid.Tests.OriginalObjectDestructionLifecycleTests.OriginalChildRemovalRetainsReverseCallbacksAndDeferredFrames",
    "ProjectLucid.Tests.OriginalObjectDestructionLifecycleTests.OriginalComponentRemovalRetainsDeferredExclusionsAndTransform",
)

# Fixture guard self-check for the live cloud facade proof; no product oracle.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.CloudFacadeTests.FixtureGuardRejectsBorrowedStoreStateWithoutInvocation",
)

# Medium coverage gaps in UI managers, buttons, highlighters, scroll geometry and cameras.

PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalUIContainerManagerGapTests.SingularCloseAllExceptFaultKeepsFieldWhileTransitionCloseClearsIt",
    "ProjectLucid.Tests.OriginalUIContainerManagerGapTests.DebugAndFreeReturnExternallyDestroyedCachedContainer",
    "ProjectLucid.Tests.OriginalUIInputVisibilityGapTests.DisabledControllerStillSubscribesWhenUIManagerBecomesValidLater",
    "ProjectLucid.Tests.OriginalHLUIEffectManagerGapTests.AwakeLogsBeforeRegistrationAndKeepsFirstCuePerKeyIncludingNullClip",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalMissionRingRewardPreservationTests.OriginalMissionRingRewardsPreserveAuthoredOrderAndInclusiveSignedThresholds",
)


EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalCoroutineEngineTests.OriginalCoroutineActualScaledUnscaledAndRealtimeGetters',
    'ProjectLucid.Tests.OriginalCoroutineEngineTests.OriginalCoroutineActualRegistrationPublicationFaultPrefix',
    'ProjectLucid.Tests.OriginalCoroutineEngineTests.OriginalCoroutineActualDestroyedHostRetainsUnityNullReference',
    'ProjectLucid.Tests.OriginalScrollSelectionEngineTests.OriginalScrollSelectionActualConstructorsPoliciesAndNullSelection',
    'ProjectLucid.Tests.OriginalScrollSelectionEngineTests.OriginalScrollSelectionActualSignedGeometryAndAlignmentCases',
    'ProjectLucid.Tests.OriginalScrollSelectionEngineTests.OriginalScrollSelectionActualInstantCallbackAndPartialFailureOrder',
    'ProjectLucid.Tests.OriginalScrollSelectionEngineTests.OriginalSpeedScrollUsesFixedDirectionsAndClampsOppositeSignsToTarget',
    'ProjectLucid.Tests.OriginalCoroutinePreservationTests.OriginalCoroutineWholeDeclarations',
    'ProjectLucid.Tests.OriginalCoroutinePreservationTests.OriginalCoroutineSingleYieldFaultAndDisposalOrder',
    'ProjectLucid.Tests.OriginalCoroutinePreservationTests.OriginalCoroutineSignedFrameCountsAndResumeOverflow',
    'ProjectLucid.Tests.OriginalCoroutinePreservationTests.OriginalCoroutinePredicateOrderAndPartialFailures',
    'ProjectLucid.Tests.OriginalCoroutinePreservationTests.OriginalCoroutineCachedWaitAndStartPredicatePolarity',
    'ProjectLucid.Tests.OriginalCoroutinePreservationTests.OriginalCoroutineLazyTimeAndNullHostBranches',
    'ProjectLucid.Verification.Tests.OriginalScrollRectPreservationTests.OriginalScrollRectDeclarationsAndMarker',
    'ProjectLucid.Verification.Tests.OriginalScrollRectPreservationTests.OriginalScrollRectRubberDeltaFixedVectors',
    'ProjectLucid.Verification.Tests.OriginalScrollRectPreservationTests.OriginalScrollRectSmallContentTruthTable',
    'ProjectLucid.Verification.Tests.OriginalScrollRectPreservationTests.OriginalScrollRectClampAndFailurePrefixes',
    'ProjectLucid.Tests.OriginalRectTransformPreservationTests.RectTransformOriginalDeclarationsAndNullFailurePrefixes',
    'ProjectLucid.Tests.OriginalRectTransformPreservationTests.RectTransformImmediateChildrenRetainsAllOriginalSiblingSlots',
    'ProjectLucid.Tests.OriginalRectTransformPreservationTests.RectTransformFullscreenResetRetainsOriginalUnwrittenProperties',
    'ProjectLucid.Tests.OriginalRectTransformPreservationTests.RectTransformEdgePointsRetainOriginalLocalGeometry',
)

EDITMODE_TESTS += (
    'ProjectLucid.Verification.OriginalRibbonVariantsTests.OriginalInertCapturedCurveGeometry',
    'ProjectLucid.Verification.OriginalRibbonVariantsTests.OriginalSegmentedCapturedCurveGeometry',
    'ProjectLucid.Verification.OriginalRibbonVariantsTests.OriginalInertCachesAndDistinctIndexRules',
    'ProjectLucid.Verification.OriginalRibbonVariantsTests.OriginalSegmentedCachesAndDistinctIndexRules',
    'ProjectLucid.Verification.OriginalRibbonVariantsTests.OriginalInertSerializedAndActualTransforms',
    'ProjectLucid.Verification.OriginalRibbonVariantsTests.OriginalSegmentedSerializedAndActualTransforms',
    'ProjectLucid.Verification.OriginalRibbonVariantsTests.OriginalInertNotificationPublicationOrder',
    'ProjectLucid.Verification.OriginalRibbonVariantsTests.OriginalSegmentedNotificationPublicationOrder',
    'ProjectLucid.Verification.OriginalRibbonVariantsTests.OriginalFacadeSelectsStoredVariant',
    'ProjectLucid.Verification.OriginalRibbonVariantsTests.OriginalFacadeCreatesAndRetainsOriginalChild',
    'ProjectLucid.Verification.OriginalRibbonVariantsTests.OriginalFacadePublishesBeforeOldCallback',
    'ProjectLucid.EditMode.OriginalInertRibbonManagedTests.InertLutRetainsNullBoundaryAndLengthNoOps',
    'ProjectLucid.EditMode.OriginalInertRibbonManagedTests.AlignmentCacheRetainsFirstMatchMissesFaultsAndCenterForwarding',
    'ProjectLucid.EditMode.OriginalInertRibbonManagedTests.SetTransformRetainsRawRotationAndOtherCapturedFields',
    'ProjectLucid.EditMode.OriginalInertRibbonManagedTests.SubSplineCopyRetainsBoundsReciprocalAndOriginalThrow',
)

PLAYMODE_TESTS += (
    'ProjectLucid.Tests.OriginalCoroutineSchedulingPlayTests.OriginalCoroutineActualScheduledFramesStopAndDuplicateLifetime',
)


EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalEventSystemPreservationTests.OriginalEventSystemDeclarationsCachesAndDataBits',
    'ProjectLucid.Tests.OriginalEventSystemPreservationTests.OriginalEventSystemValidationForwardingAndFaultOrder',
)

EDITMODE_TESTS += (
    "ProjectLucid.Verification.OriginalVisualQualityPreservationTests.OriginalConfigurationConstructorsPreserveAuthoredDefaults",
    "ProjectLucid.Verification.OriginalVisualQualityPreservationTests.OriginalConfigurationSelectionPreservesFirstFallbackAndFaultOrder",
    "ProjectLucid.Verification.OriginalVisualQualityPreservationTests.OriginalProfilesPreserveGuidEqualityAndLiveAliases",
    "ProjectLucid.Verification.OriginalVisualQualityPreservationTests.OriginalProfilesPreserveNestedCallbackSnapshotsAndLiveIdentity",
    "ProjectLucid.Verification.OriginalVisualQualityPreservationTests.OriginalProfilesPreserveCallbackAndNullTransitionFaultPrefixes",
)

def artifact_fingerprint(repo_root, prepared=False):
    """Hash UTF8 path/content rows in UTF16 code-unit order, shared with Unity."""
    root = Path(repo_root).resolve()
    bases = ("Assets/Recovered", "Assets/StreamingAssets") if prepared else (
        "Assets", "Packages", "ProjectSettings", "tools")
    files = []
    for base in bases:
        directory = root / base
        if directory.is_symlink():
            raise ValueError("Refusing a symlinked project directory: " + str(directory))
        if not directory.is_dir():
            continue
        for parent, directories, names in os.walk(directory):
            for name in directories:
                if (Path(parent) / name).is_symlink():
                    raise ValueError("Refusing a symlinked project directory: " + str(Path(parent) / name))
            directories[:] = [name for name in directories if not name.startswith(".")
                              and (prepared or name not in ("obj", "bin", "__pycache__"))]
            if not prepared and Path(parent) == root / "Assets":
                directories[:] = [name for name in directories if name not in ("Recovered", "StreamingAssets")]
                names = [name for name in names if name not in ("Recovered.meta", "StreamingAssets.meta")]
            for name in names:
                path = Path(parent) / name
                if path.is_symlink():
                    raise ValueError("Refusing a symlinked project file: " + str(path))
                if name.endswith((".pyc", ".pyo", ".tmp")) or name == ".DS_Store":
                    continue
                files.append((path.relative_to(root).as_posix(), path))
    result = hashlib.sha256()
    # Big-endian UTF16 bytes have the same order as C# StringComparer.Ordinal,
    # including supplementary characters that Python orders by code point.
    for relative, path in sorted(files, key=lambda row: row[0].encode("utf-16-be")):
        content = hashlib.sha256()
        with path.open("rb") as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                content.update(chunk)
        result.update((relative + "\0" + content.hexdigest() + "\n").encode("utf-8"))
    return result.hexdigest()


def current_identity(repo_root):
    return {"source_fingerprint": artifact_fingerprint(repo_root),
            "prepared_asset_fingerprint": artifact_fingerprint(repo_root, prepared=True)}


def find_editor():
    explicit = os.environ.get("LUCID_UNITY_EDITOR")
    if explicit:
        candidate = Path(explicit).expanduser()
        if not candidate.is_file():
            raise ValueError("LUCID_UNITY_EDITOR does not identify an Editor executable")
        return candidate
    system = platform.system()
    if system == "Darwin":
        candidate = Path("/Applications/Unity/Hub/Editor") / UNITY_VERSION / "Unity.app/Contents/MacOS/Unity"
    elif system == "Windows":
        candidate = Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "Unity/Hub/Editor" / UNITY_VERSION / "Editor/Unity.exe"
    else:
        candidate = Path.home() / "Unity/Hub/Editor" / UNITY_VERSION / "Editor/Unity"
    if not candidate.is_file():
        raise ValueError("Install Unity " + UNITY_VERSION + "; set LUCID_UNITY_EDITOR for a custom installation path")
    return candidate


def test_verdict(path, required):
    path = Path(path)
    if not path.is_file() or path.stat().st_size > 8 * 1024 * 1024:
        raise ValueError("Unity test XML is absent or exceeds the supported size")
    data = path.read_bytes()
    if b"<!DOCTYPE" in data.upper() or b"<!ENTITY" in data.upper():
        raise ValueError("Unsupported declarations in Unity test XML")
    try:
        root = ET.fromstring(data)
    except ET.ParseError as error:
        raise ValueError("Invalid Unity test XML: " + str(error)) from error
    diagnostics = []
    outcomes = {"Passed": "passed", "Failed": "failed", "Inconclusive": "inconclusive", "Skipped": "skipped"}
    named_outcomes = {}
    counts = {}
    # NUnit summary counters count case occurrences, not distinct fullnames.
    # Fold the complete tree so nested suites are checked without recursive calls.
    for node in reversed(list(root.iter())):
        errors = []
        actual = dict.fromkeys(("total", *outcomes.values()), 0)
        for child in node:
            for key, value in counts.pop(child).items():
                actual[key] += value
        if node.tag in ("test-run", "test-suite", "test-case"):
            result = node.get("result")
            name = node.get("fullname") or node.get("name") or "<unnamed>"
            if result not in outcomes:
                errors.append("Unsupported " + node.tag + " outcome for " + name + ": " + repr(result))
            elif result == "Failed":
                errors.append("Failed " + node.tag + ": " + name)
            if node.tag == "test-case":
                actual["total"] += 1
                if result in outcomes:
                    actual[outcomes[result]] += 1
                if node.get("fullname"):
                    named_outcomes.setdefault(node.get("fullname"), set()).add(result)
            else:
                # testcasecount counts discovered tests and may exceed filtered results.
                for key, value in actual.items():
                    declared = node.get(key)
                    if declared is not None and (re.fullmatch(r"[0-9]+", declared) is None or
                                                 (declared.lstrip("0") or "0") != str(value)):
                        errors.append("Inconsistent " + node.tag + " " + key + " count for " + name +
                                      ": " + repr(declared) + "; observed " + str(value))
        counts[node] = actual
        diagnostics.append(errors)
    errors = [error for group in reversed(diagnostics) for error in group]
    # Optional skipped/inconclusive cases are allowed. Every occurrence of a
    # required fullname must pass; one passing duplicate cannot erase a failure.
    passed = {name for name, values in named_outcomes.items() if values == {"Passed"}}
    required_names = set(required)
    missing = sorted(required_names - passed)
    conflicting = sorted(name for name in required_names if len(named_outcomes.get(name, ())) > 1)
    if conflicting:
        errors.append("Conflicting required test outcomes: " + ", ".join(conflicting))
    # Every case of a project test assembly must be required by its exact fullname, so a project
    # test cannot be skipped, renamed or dropped without a visible registry change.
    project_cases = {case.get("fullname") or "<unnamed>" for suite in root.iter("test-suite")
                     if suite.get("type") == "Assembly" and
                     re.split(r"[\\/]", suite.get("name") or "")[-1].startswith("ProjectLucid.")
                     for case in suite.iter("test-case")}
    unregistered = sorted(project_cases - required_names)
    if unregistered:
        errors.append("Unregistered project tests: " + ", ".join(unregistered))
    success = (root.tag == "test-run" and root.get("result") == "Passed" and root.get("failed") == "0"
               and not missing and not errors)
    return {"status": "complete" if success else "incomplete", "result": root.get("result"),
            "passed": len(passed), "missing_required_tests": missing,
            "conflicting_required_tests": conflicting, "errors": errors,
            "xml_sha256": hashlib.sha256(data).hexdigest()}


def run_tests(repo_root, work_dir, mode):
    if mode not in ("editmode", "playmode"):
        raise ValueError("Test mode must be editmode or playmode")
    editor = find_editor()
    before = current_identity(repo_root)
    xml = managed_path(work_dir, "reports", mode + ".xml")
    log = managed_path(work_dir, "reports", mode + ".log")
    xml.parent.mkdir(parents=True, exist_ok=True)
    # A failed Editor invocation must never accidentally reuse a passing result.
    xml.unlink(missing_ok=True)
    log.unlink(missing_ok=True)
    command = [str(editor), "-batchmode", "-projectPath", str(Path(repo_root).resolve()),
               "-runTests", "-testPlatform", "EditMode" if mode == "editmode" else "PlayMode",
               "-testResults", str(xml), "-logFile", str(log)]
    environment = os.environ.copy()
    environment["LUCID_RECOVERY_WORK_DIR"] = str(Path(work_dir).resolve())
    if mode == "editmode":
        from .fontreferencefixtures import build_import_fixture
        carrier_meta = Path(repo_root) / "Assets/Tests/Editor/ProjectLucidFontReferenceConversionCarrier.cs.meta"
        carrier_guids = re.findall(r"(?m)^guid: ([a-f0-9]{32})$", carrier_meta.read_text(encoding="utf-8"))
        if len(carrier_guids) != 1:
            raise ValueError("Test carrier MonoScript GUID is absent or ambiguous")
        fixture = build_import_fixture(carrier_guids[0])
        fixture["nonce"] = uuid.uuid4().hex
        converter_source = Path(repo_root) / "tools/lucidlib/fontreferences.py"
        fixture["converter_sha256"] = hashlib.sha256(converter_source.read_bytes()).hexdigest()
        fixture_path = managed_path(work_dir, "reports", "font-reference-import.json")
        write_json(fixture_path, fixture)
        environment["LUCID_FONT_REFERENCE_IMPORT_FIXTURE"] = str(fixture_path.resolve())
        environment["LUCID_FONT_REFERENCE_IMPORT_NONCE"] = fixture["nonce"]
    result = subprocess.run(command, env=environment, timeout=1800)
    after = current_identity(repo_root)
    errors = []
    version_match = re.search(r"Unity Editor version:\s+(\S+)", log.read_text(errors="replace")) if log.is_file() else None
    actual_version = version_match.group(1) if version_match else None
    if actual_version != UNITY_VERSION:
        errors.append("Test Editor version is absent or differs from " + UNITY_VERSION)
    if result.returncode:
        errors.append("Unity test process exited " + str(result.returncode))
    if before != after:
        errors.append("Project content changed during testing; rerun with the imported project")
    try:
        verdict = test_verdict(xml, EDITMODE_TESTS if mode == "editmode" else PLAYMODE_TESTS)
        errors.extend(verdict["errors"])
        if verdict["missing_required_tests"]:
            errors.append("Missing required tests: " + ", ".join(verdict["missing_required_tests"]))
    except ValueError as error:
        errors.append(str(error))
        verdict = {"status": "incomplete"}
    report = {"schema_version": 1, "generated_by": "ProjectLucid.run_tests", "mode": mode,
              "status": "complete" if not errors and verdict["status"] == "complete" else "incomplete",
              "unity_version": actual_version, "editor": str(editor), "exit_code": result.returncode,
              "xml": str(xml), "log": str(log), "verdict": verdict, "errors": errors, **after}
    write_json(managed_path(work_dir, "reports", mode + "-verification.json"), report)
    return report


def verified_receipt(work_dir, mode, identity):
    path = Path(work_dir) / "reports" / (mode + "-verification.json")
    receipt = json.loads(path.read_text())
    if not isinstance(receipt, dict):
        raise ValueError(mode + " test receipt is not a JSON object")
    if receipt.get("generated_by") != "ProjectLucid.run_tests" or receipt.get("status") != "complete":
        raise ValueError(mode + " has no successful generated test receipt")
    if receipt.get("unity_version") != UNITY_VERSION or any(receipt.get(key) != value for key, value in identity.items()):
        raise ValueError(mode + " test receipt is stale for the current project")
    xml = Path(work_dir) / "reports" / (mode + ".xml")
    verdict = test_verdict(xml, EDITMODE_TESTS if mode == "editmode" else PLAYMODE_TESTS)
    if verdict["status"] != "complete" or verdict["xml_sha256"] != receipt.get("verdict", {}).get("xml_sha256"):
        raise ValueError(mode + " test result is incomplete or differs from its generated receipt")
    return verdict


def _audit_context_bytes(root, identity, nonce):
    """Small exact-format context, parsed independently by the matching Editor."""
    if not isinstance(nonce, str) or re.fullmatch(r"[0-9a-f]{32}", nonce) is None or any(
            not isinstance(identity.get(key), str) or re.fullmatch(r"[0-9a-f]{64}", identity[key]) is None
            for key in ("source_fingerprint", "prepared_asset_fingerprint")):
        raise ValueError("Invalid reference audit content identity")
    encoded = base64.b64encode(str(root).encode("utf-8")).decode("ascii")
    value = ("ProjectLucid.audit-identity-context-v1\nalgorithm=sha256-path-content-v1\n"
             "nonce=" + nonce + "\nproject_root_base64=" + encoded + "\nunity_version=" + UNITY_VERSION +
             "\nsource_fingerprint=" + identity["source_fingerprint"] +
             "\nprepared_asset_fingerprint=" + identity["prepared_asset_fingerprint"] + "\n").encode("utf-8")
    if len(value) > 16384:
        raise ValueError("Reference audit identity context exceeds its supported size")
    return value


def _audit_wrapper_paths_supported(root):
    """UTF16 Ordinal and Python path order agree for these prepared paths.

    Preserve the existing algorithms: unusual paths use direct Editor hashing,
    whose result must still equal the wrapper's native content fingerprint.
    """
    for base in ("Assets/Recovered", "Assets/StreamingAssets"):
        for parent, directories, names in os.walk(Path(root) / base):
            directories[:] = [name for name in directories if not name.startswith(".")]
            for name in names:
                if name.endswith((".pyc", ".pyo", ".tmp")) or name == ".DS_Store":
                    continue
                relative = (Path(parent) / name).relative_to(root).as_posix()
                if "\\" in relative or any(ord(char) > 0xffff for char in relative):
                    return False
    return True


def _audit_read_bytes(work_dir, path, limit):
    """Read only owned, bounded evidence; reject changes during the read."""
    path = managed_path(work_dir, *path.relative_to(Path(work_dir)).parts)
    if not path.is_file() or path.stat().st_size > limit:
        raise ValueError("Reference audit evidence is absent or exceeds its supported size")
    before = path.stat()
    data = path.read_bytes()
    after = path.stat()
    fields = ("st_dev", "st_ino", "st_size", "st_mtime_ns", "st_ctime_ns")
    if not data or len(data) > limit or any(getattr(before, key) != getattr(after, key) for key in fields):
        raise ValueError("Reference audit evidence changed while reading")
    return data


def _audit_json_pairs(pairs):
    value = {}
    for key, item in pairs:
        if key in value:
            raise ValueError("Duplicate reference audit JSON field")
        value[key] = item
    return value


def _audit_invalid_constant(value):
    raise ValueError("Invalid reference audit JSON constant: " + value)


def _audit_check_report(report, identity, *, nonce=None, context_digest=None):
    if not isinstance(report, dict) or report.get("unity_version") != UNITY_VERSION or any(
            report.get(key) != value for key, value in identity.items()):
        raise ValueError("Unity reference audit does not match the current project content")
    expected = {"identity_mode": "wrapper-prepost-v1", "identity_status": "pending-wrapper",
                "status": "pending-identity-verification", "identity_nonce": nonce,
                "identity_context_sha256": context_digest} if nonce is not None else {
                    "identity_mode": "editor-content-sha256-v1", "identity_status": "complete"}
    if any(report.get(key) != value for key, value in expected.items()):
        raise ValueError("Unity reference audit identity handshake is incomplete or stale")
    if nonce is None and (report.get("identity_nonce") not in (None, "") or report.get("identity_context_sha256") not in (None, "")):
        raise ValueError("Direct reference audit unexpectedly contains supplied identity evidence")
    for key in ("scanned_assets", "unresolved_references"):
        if type(report.get(key)) is not int or not 0 <= report[key] <= 0x7fffffff:
            raise ValueError("Invalid reference audit count: " + key)
    for key in ("missing_guids", "invalid_script_bindings", "shader_errors"):
        if not isinstance(report.get(key), list):
            raise ValueError("Invalid reference audit diagnostics: " + key)
    for item in report["missing_guids"]:
        if (not isinstance(item, dict) or not isinstance(item.get("guid"), str) or re.fullmatch(r"[0-9a-f]{32}", item["guid"]) is None or
                not isinstance(item.get("examples"), list) or not 0 < len(item["examples"]) <= 8 or
                any(not isinstance(value, str) or not value for value in item["examples"])):
            raise ValueError("Invalid missing reference diagnostic")
    for key in ("invalid_script_bindings", "shader_errors"):
        if any(not isinstance(value, str) or not value for value in report[key]):
            raise ValueError("Invalid reference audit diagnostic text")
    unresolved = sum(len(report[key]) for key in ("missing_guids", "invalid_script_bindings", "shader_errors"))
    reference_status = "complete" if unresolved == 0 else "incomplete"
    if report["unresolved_references"] != unresolved or report.get("reference_status") != reference_status or (
            nonce is None and report.get("status") != reference_status):
        raise ValueError("Reference audit status differs from its diagnostics")


def _audit_require_current_source(root, identity, phase):
    # current_identity hashes source before its much larger prepared-data pass.
    # Recheck the small source tree after that pass and before publication.
    if artifact_fingerprint(root) != identity["source_fingerprint"]:
        raise ValueError("Maintained source changed " + phase)


def run_audit(repo_root, work_dir):
    """Publish an Editor audit only after native pre/post hashes agree."""
    root, work = Path(repo_root).resolve(strict=True), Path(os.path.abspath(os.fspath(work_dir)))
    editor = find_editor()
    output = managed_path(work, "reports", "unity-reference-audit.json")
    lock = managed_path(work, "locks", "reference-audit.lock")
    lock.parent.mkdir(parents=True, exist_ok=True)
    try:
        lock.mkdir()
    except FileExistsError as error:
        raise ValueError("Another reference audit owns the generated audit lock") from error
    try:
        before = current_identity(root)
        _audit_require_current_source(root, before, "while preparing the reference audit")
        nonce = uuid.uuid4().hex
        run = managed_path(work, "reports", "audit-runs", nonce)
        run.mkdir(parents=True, exist_ok=False)
        pending = managed_path(work, "reports", "audit-runs", nonce, "pending-audit.json")
        log = managed_path(work, "reports", "audit-runs", nonce, "editor.log")
        command = [str(editor), "-batchmode", "-quit", "-projectPath", str(root),
                   "-executeMethod", "ProjectLucid.Editor.LucidReferenceAudit.Run",
                   "-lucidAuditOutput", str(pending), "-logFile", str(log)]
        context = context_bytes = digest = None
        fast = _audit_wrapper_paths_supported(root)
        if fast:
            context = managed_path(work, "reports", "audit-runs", nonce, "identity-context.txt")
            context_bytes = _audit_context_bytes(root, before, nonce)
            with context.open("xb") as stream:
                stream.write(context_bytes)
                stream.flush()
                os.fsync(stream.fileno())
            digest = hashlib.sha256(context_bytes).hexdigest()
            command.extend(["-lucidAuditIdentityContext", str(context), "-lucidAuditIdentityDigest", digest,
                            "-lucidAuditIdentityNonce", nonce])
        result = subprocess.run(command, timeout=1800)
        if result.returncode or not pending.is_file():
            return {"status": "failed", "exit_code": result.returncode, "log": str(log),
                    "errors": ["Unity reference audit did not complete"]}
        raw = _audit_read_bytes(work, pending, 32 * 1024 * 1024)
        report = json.loads(raw, object_pairs_hook=_audit_json_pairs, parse_constant=_audit_invalid_constant)
        after = current_identity(root)
        _audit_require_current_source(root, after, "during post-audit identity verification")
        if before != after:
            raise ValueError("Project content changed during the Unity reference audit; rerun with stable imported content")
        if fast and _audit_read_bytes(work, context, 16384) != context_bytes:
            raise ValueError("Reference audit identity context changed during the Editor invocation")
        _audit_check_report(report, after, nonce=nonce if fast else None, context_digest=digest)
        # Recheck destinations/evidence immediately before the only authoritative
        # write. A failed or interrupted run never replaces the previous audit.
        output = managed_path(work, "reports", "unity-reference-audit.json")
        if _audit_read_bytes(work, pending, 32 * 1024 * 1024) != raw or (
                fast and _audit_read_bytes(work, context, 16384) != context_bytes):
            raise ValueError("Reference audit evidence changed before publication")
        _audit_require_current_source(root, after, "before reference audit publication")
        report.update(status=report["reference_status"], identity_status="complete",
                      generated_by="ProjectLucid.run_audit", identity_verified_by="native-python-prepost-v1",
                      staged_report_sha256=hashlib.sha256(raw).hexdigest(), staged_report_path=str(pending))
        write_json(output, report)
        return {"status": report["status"], "unresolved_references": report["unresolved_references"],
                "scanned_assets": report["scanned_assets"], "report_path": str(output), "log": str(log)}
    finally:
        lock.rmdir()

EDITMODE_TESTS += ('ProjectLucid.Tests.OriginalPerformanceDependencyPreservationTests.CopyPreservesAliasesAndDestinationCallbacks', 'ProjectLucid.Tests.OriginalPerformanceDependencyPreservationTests.DelegateAccessorsPreserveDuplicatesAndCapturedOrder', 'ProjectLucid.Tests.OriginalPerformanceDependencyPreservationTests.ComparisonsPreserveThresholdsAndMissingFeatureFallback', 'ProjectLucid.Tests.OriginalPerformanceDependencyPreservationTests.ProfileCallbacksPreserveReentryAndFaultPrefixes', 'ProjectLucid.Tests.OriginalPerformanceDependencyPreservationTests.ProfileSubscriptionsPreserveDuplicateAndFaultState')
# Owned-component cleanup failure controls; no original-behavior claim.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalPerformanceDependencyPreservationTests.OwnedGroupBodyFailureStaysPrimaryAndReleasesEveryHost",
    "ProjectLucid.Tests.OriginalPerformanceDependencyPreservationTests.OwnedGroupSecondHostAcquisitionFailureStaysPrimaryAndReleasesBothHosts",
    "ProjectLucid.Tests.OriginalPerformanceDependencyPreservationTests.OwnedGroupBodyFailureStaysFirstAheadOfReleaseFailures",
    "ProjectLucid.Tests.OriginalPerformanceDependencyPreservationTests.OwnedGroupThrowingReleaseStillReleasesLaterHostsAndRunsTheRegistryCheck",
)

EDITMODE_TESTS += ('ProjectLucid.Tests.OriginalNotificationsPreservationTests.ReceivedTokenPreservesStoreReentryAndFaultOrder', 'ProjectLucid.Tests.OriginalNotificationsPreservationTests.ConfigurationPreservesDefaultsAndOverrideAliases', 'ProjectLucid.Tests.OriginalGraphUserPreservationTests.TypedReferenceCastPreservesIdentityAndInvalidCastFault', 'ProjectLucid.Tests.OriginalGraphUserPreservationTests.TypedValueCastPreservesUnboxingCopyAndNullFault')

EDITMODE_TESTS += ('ProjectLucid.Tests.OriginalDelayModifierPreservationTests.ScalarUsesOldIndependentKeyTimers', 'ProjectLucid.Tests.OriginalDelayModifierPreservationTests.VectorUsesNewTimerAndResetPreservesFaultPrefix', 'ProjectLucid.Tests.OriginalDelayModifierPreservationTests.OriginalUnityConstructorCreatesCache')

EDITMODE_TESTS += ('ProjectLucid.Tests.EditMode.OriginalSplineFamilyTests.SplineKnotsPreserveSerializedFieldsAndSignedLUTQueries', 'ProjectLucid.Tests.EditMode.OriginalSplineFamilyTests.SplineKnotsRetainEightOriginalNotImplementedBodies', 'ProjectLucid.Tests.EditMode.OriginalSplineFamilyTests.SplineComponentsRetainDefaultsIndexFaultsAndParentCaching', 'ProjectLucid.Tests.EditMode.OriginalSplineFamilyTests.SplineCopiesEvaluateAnalyticStraightGeometryAndWorldTransforms', 'ProjectLucid.Tests.EditMode.OriginalSplineFamilyTests.SplineBoundsCacheAndLocalZDistanceRemainOriginal', 'ProjectLucid.Tests.EditMode.OriginalSplineFamilyTests.SplineEventsRetainTimestampAndDelegateFailurePrefixes', 'ProjectLucid.Tests.EditMode.OriginalSplineFamilyTests.SplineFacadePublishesReplacementBeforeOriginalCallback', 'ProjectLucid.Tests.EditMode.OriginalSplineFamilyTests.SplineTrackerUsesTrueResolverAndRetainsCallbackFailureState', 'ProjectLucid.Tests.EditMode.OriginalSplineFamilyTests.SplineSamplingCacheRestoresEveryPriorBitAfterOwnedFaults', 'ProjectLucid.Tests.EditMode.OriginalSplineFamilyTests.SplineSamplingCacheAccumulatesOwnedLineSegmentsToWholeLength')

EDITMODE_TESTS += ('ProjectLucid.Tests.EditMode.OriginalEnumPreservationTests.ParseRetainsCaseSensitiveNamesAndNumericValues', 'ProjectLucid.Tests.EditMode.OriginalEnumPreservationTests.SafeParseRetainsFallbackAcrossParseAndTypeFaults', 'ProjectLucid.Tests.EditMode.OriginalEnumPreservationTests.NamesAndValuesRetainAliasesAndIndependentArrays', 'ProjectLucid.Tests.EditMode.OriginalEnumPreservationTests.IntegerValuesRetainSignedConversionAndOverflow', 'ProjectLucid.Tests.EditMode.OriginalEnumPreservationTests.MembershipRequiresDefinedEnumValues', 'ProjectLucid.Tests.EditMode.OriginalEnumPreservationTests.ConversionRetainsOutValueAndFaultPrefixes', 'ProjectLucid.Tests.EditMode.OriginalEnumPreservationTests.OriginalConstraintsRemainObservable')

EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalCoreHelperPreservationTests.OpStringCreationMutationAndOverloads',
    'ProjectLucid.Tests.OriginalCoreHelperPreservationTests.OpStringCultureFormattingAndOriginalTrim',
    'ProjectLucid.Tests.OriginalCoreHelperPreservationTests.OpStringThreadCacheAndExceptionalRestoration',
    'ProjectLucid.Tests.OriginalCoreHelperPreservationTests.ListSearchLiveAliasAndOutFaults',
    'ProjectLucid.Tests.OriginalCoreHelperPreservationTests.ListUniqueRangeCallbacksAndDisposal',
    'ProjectLucid.Tests.OriginalCoreHelperPreservationTests.SeededShuffleSnapshotCallbacksAndPartialWrites',
    'ProjectLucid.Tests.OriginalCoreHelperPreservationTests.ResourcePathStringsAndOutFaultPrefixes',
    'ProjectLucid.Tests.OriginalCoreHelperPreservationTests.ResourceCombineCacheAliasesAndRestoration',
    'ProjectLucid.Tests.OriginalResourceEngineTests.CurrentSceneNameReadsOwnedActiveScene',
    'ProjectLucid.Tests.OriginalResourceEngineTests.UnityShuffleConsumesOneDrawBeforeReadsAndRestoresState',
)

EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalEntityActivationStateTests.ActivationTransitionOrderAndFirstTime',
    'ProjectLucid.Tests.OriginalEntityActivationStateTests.ActivationCallbackFaultAndLiveOppositeList',
    'ProjectLucid.Tests.OriginalEntityActivationStateTests.ActivationPredicateShortCircuitAndLiveMutation',
    'ProjectLucid.Tests.OriginalEntityActivationStateTests.ActivationShutdownAndClearFaultPrefix',
)

EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalUIInputComparerTests.InputOverridesSurviveSerializedRoundTrip',
    'ProjectLucid.Tests.OriginalUIInputComparerTests.UnknownSignedKeysRemainDistinct',
)

EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalMessageManagerPreservationTests.RealFiveProvidersInvalidateOldHandlesAndAllowFreshHandlesAfterRepeatedShutdown',
    'ProjectLucid.Tests.OriginalMessageManagerPreservationTests.FiveProvidersReceiveOrderedNullContextAndFaultPrefixRetries',
)

EDITMODE_TESTS += (
    'ProjectLucid.Tests.EditMode.AnalyticsConsentStateHelpersTests.OriginalLabelsComparersAndMutableCacheBehavior',
    'ProjectLucid.Tests.EditMode.AnalyticsMissionTypeHelpersTests.OriginalLabelsComparersAndMutableCacheBehavior',
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.EditMode.InputModifierOriginalBehaviorTests.DeadZoneHysteresisAndRadialCacheResetRemainOriginal",
    "ProjectLucid.Tests.EditMode.InputModifierOriginalBehaviorTests.CooldownScalarVectorTimingAndDebounceInputStateRemainOriginal",
    "ProjectLucid.Tests.EditMode.InputModifierOriginalBehaviorTests.CurveXYSignZAndScalarModifierBranchesRemainOriginal",
    "ProjectLucid.Tests.EditMode.InputModifierOriginalBehaviorTests.RepeatUsesRealPerInputStateAndUnityTimeWithoutRepairingScalarFault",
    "ProjectLucid.Tests.EditMode.InputModifierOriginalBehaviorTests.TriggerRegistersAndCallsBackBeforeDownAndResetStateStores",
    "ProjectLucid.Tests.EditMode.InputModifierOriginalBehaviorTests.TriggerFaultsAndHeldCallbacksPreserveOriginalStateAndRegistration",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.EditMode.InputRemainingOriginalBehaviorTests.OriginalConfigurationPathsAndInactiveProvidersRemainIntact",
    "ProjectLucid.Tests.EditMode.InputRemainingOriginalBehaviorTests.OriginalGameInputTriggerControlledCallbacksPreserveSubscriptionAndUnityEventOrder",
    "ProjectLucid.Tests.EditMode.InputRemainingOriginalBehaviorTests.OriginalGameInputTriggerControlledCallbacksPreserveNullAndCallbackFaultOrdering",
    "ProjectLucid.Tests.EditMode.InputRemainingOriginalBehaviorTests.OriginalGlyphDisplayUsesGenuineTextAndRawImageSetterOrdering",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.EditMode.AbilityLeafDefinitionTests.OriginalDefinitionDeclarationsAndBaseOnlyBodiesRemainIntact",
    "ProjectLucid.Tests.EditMode.AbilityLeafDefinitionTests.OwnedCameraCachesPreserveGetterAliasesAndAsymmetricNullFaultOrder",
    "ProjectLucid.Tests.EditMode.AbilityLeafDefinitionTests.OriginalRestoreRecordDefaultsAndMutableFieldValuesRemainIntact",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.EditMode.UIOriginalLeafTests.CurrentBuildEmissionMatchesRecordedSnapshot",
    "ProjectLucid.Tests.EditMode.UIOriginalLeafTests.OriginalMenuCameraUsesOwnedInactiveStoredCameraAndUnguardedToggle",
    "ProjectLucid.Tests.EditMode.UIOriginalLeafTests.OriginalUIConfigurationUsesOwnedInactiveStoredGettersAndPrimitiveJson",
    "ProjectLucid.Tests.EditMode.UIOriginalLeafTests.OriginalUIConfigurationLifecycleRegistersAndUnregistersBeforeUnguardedBridgeCalls",
    "ProjectLucid.Tests.EditMode.UIOriginalLeafTests.OriginalUIConfigurationLifecycleRevokesOwnedRegistrationAfterAssertionFault",
    "ProjectLucid.Tests.EditMode.UIOriginalLeafTests.OriginalAbstractUIBridgeAndSupplierPreserveCompleteMetadataAndBaseOnlyConstructors",
    "ProjectLucid.Tests.UIConcreteInputTests.ConcreteDeclarationsAndOwnedDefaults",
    "ProjectLucid.Tests.UIConcreteInputTests.EnumValidationPrecedesNullModuleFault",
    "ProjectLucid.Tests.UIConcreteInputTests.AllNullSuppliersFaultBeforeMapping",
    "ProjectLucid.Tests.UIConcreteInputTests.GenuineSupplierFieldAndGetterRoundtrip",
    "ProjectLucid.Tests.UIConcreteInputTests.GenuineTypedCallbacksAndLastInputMapping",
    "ProjectLucid.Tests.UIConcreteInputTests.OriginalLatchAndShutdownRemovalOrder",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.ControllerActionTests.CurrentBuildEmissionMatchesRecordedSnapshot",
    "ProjectLucid.Tests.ControllerActionTests.GenuineConstructorsAndPersistentSystemCache",
    "ProjectLucid.Tests.ControllerActionTests.HeldUpAndDisabledPublishBeforeBrainFaults",
    "ProjectLucid.Tests.ControllerActionTests.RejectedModifiersAndValidatorsRetainFlags",
    "ProjectLucid.Tests.ControllerActionTests.PrimarySecondaryMappingCallsAndRealClosures",
    "ProjectLucid.Tests.ControllerActionTests.NeverActiveInputSystemUsesExplicitOriginalCallbacks",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.CameraInputDisablerTests.CurrentDeclarationsAndBodiesMatchSnapshot",
    "ProjectLucid.Tests.CameraInputDisablerTests.OriginalAxesThresholdsAndSnapArguments",
    "ProjectLucid.Tests.CameraInputDisablerTests.OriginalCaptureAndRealDebounceRetainFaultOrder",
    "ProjectLucid.Tests.CameraInputDisablerTests.OriginalCameraSubscriptionsRetainForeignCallbacks",
    "ProjectLucid.Tests.CameraInputDisablerTests.OriginalDisablerGuardAndInactiveEnableCallbacks",
    "ProjectLucid.Tests.CameraInputDisablerTests.OriginalDisablerCapturedArrayAndPartialFault",
    "ProjectLucid.Tests.CameraInputDisablerTests.OriginalDisablerReentrancyAndLiveReleaseFaults",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.BootTransitionTests.CurrentBuildEmissionMatchesRecordedSnapshot",
    "ProjectLucid.Tests.BootTransitionTests.JsonDefaultsAndOriginalFactories",
    "ProjectLucid.Tests.BootTransitionTests.SaveLoadedTruthTableAndNoDefaultInsertion",
    "ProjectLucid.Tests.BootTransitionTests.UserAndTypedNullStorageFaults",
    "ProjectLucid.Tests.BootTransitionTests.ConstructorRegistrationBeforeJsonFault",
    "ProjectLucid.Tests.BootTransitionTests.NullMachineFactoryFaults",
    "ProjectLucid.Tests.BootTransitionTests.ShippingEditorPredicateAndIgnoredJson",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.ExclusionListTests.CurrentBuildEmissionMatchesRecordedSnapshot",
    "ProjectLucid.Tests.ExclusionListTests.ConstructorDefaultsAndWriteOnlyEnabled",
    "ProjectLucid.Tests.ExclusionListTests.DisabledShortCircuitBeforeNullInputs",
    "ProjectLucid.Tests.ExclusionListTests.EnabledArrayAndMessageFaultBoundaries",
    "ProjectLucid.Tests.ExclusionListTests.PartialCaseSensitiveAndEmptyNeedles",
    "ProjectLucid.Tests.ExclusionListTests.IndexedShortCircuitAndNullNeedleOrder",
    "ProjectLucid.Tests.ExclusionListTests.LiveArrayReplacementAndIndependentInstances",
    "ProjectLucid.Tests.ExclusionListTests.OriginalPublicFieldJsonOverwrite",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.HLHapticsTests.CurrentDeclarationsBodiesAndProviderSourcePins",
    "ProjectLucid.Tests.HLHapticsTests.ConfigurationDefaultsAndLiteralNativeFiles",
    "ProjectLucid.Tests.HLHapticsTests.ConfigurationJsonDuplicatesAndPrivateSerializedPath",
    "ProjectLucid.Tests.HLHapticsTests.EventDataAndNestedVibrationsJsonRoundTrip",
    "ProjectLucid.Tests.HLHapticsTests.InactiveManagerOwnedLifecycleDefaults",
    "ProjectLucid.Tests.HLHapticsTests.NaturalIteratorNonpositiveAndNullBoundaries",
)

PLAYMODE_TESTS += (
    "ProjectLucid.Tests.HLHapticsPlayTests.ManagerAwakeDebugSnapshotAndSafeTriggerOrder",
    "ProjectLucid.Tests.HLHapticsPlayTests.NaturalIteratorFinitePositiveWaitRepeatCounts",
    "ProjectLucid.Tests.HLHapticsPlayTests.NaturalIteratorLiveOwnedArrayAfterPositiveWait",
    "ProjectLucid.Tests.HLHapticsPlayTests.TriggerEventStartsACoroutineForEveryMatchingDefinition",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalConeVolumeTests.CurrentBuildEmissionMatchesRecordedSnapshot",
    "ProjectLucid.Tests.OriginalConeVolumeTests.OrdinaryDescriptionRetainsZeroInitializationAndValueCopies",
    "ProjectLucid.Tests.OriginalConeVolumeTests.EllipseRetainsInclusiveBoundariesAndIgnoresLocalDepth",
    "ProjectLucid.Tests.OriginalConeVolumeTests.AngleTableRetainsEveryOriginalBitAndMutableArrayIdentity",
    "ProjectLucid.Tests.OriginalConeVolumeTests.CacheReplacesWrongSizesAndRetainsArbitrarySixteenEntries",
    "ProjectLucid.Tests.OriginalConeVolumeTests.BoundsRetainAxisHalfTurnNegativeDistanceAndIgnoredStep",
    "ProjectLucid.Tests.OriginalConeVolumeTests.BoundsConsumeCallerMutationsOfTheActualCachedArray",
    "ProjectLucid.Tests.OriginalConeVolumeTests.BoundsRetainNonunitQuaternionWithoutNormalization",
    "ProjectLucid.Tests.OriginalMissionScorerComboStagePreservationTests.CurrentBuildEmissionMatchesRecordedSnapshot",
    "ProjectLucid.Tests.OriginalMissionScorerComboStagePreservationTests.OrdinaryConstructorPreservesAllFourDefaults",
    "ProjectLucid.Tests.OriginalMissionScorerComboStagePreservationTests.GenuineUnitySerializationPopulatesPrivateFieldsAndRoundTripsTheirOrder",
    "ProjectLucid.Tests.OriginalMissionScorerComboStagePreservationTests.IndependentlyConstructedInstancesKeepTheirOwnSerializedState",
)

EDITMODE_TESTS += (
    "ProjectLucid.Editor.Tests.OriginalHashedGroupConfigurationPreservationTests.CurrentBuildEmissionMatchesRecordedSnapshot",
    "ProjectLucid.Editor.Tests.OriginalHashedGroupConfigurationPreservationTests.GenuineOwnedCreationAndDestruction",
    "ProjectLucid.Editor.Tests.OriginalHashedGroupConfigurationPreservationTests.TwoGenuineOwnedInstancesKeepIndependentEngineState",
)

EDITMODE_TESTS += (
    "ProjectLucid.Editor.Tests.OriginalHLCrashReportPreservationTests.CurrentBuildEmissionMatchesRecordedSnapshot",
    "ProjectLucid.Editor.Tests.OriginalHLCrashReportPreservationTests.TrimRemovesOnlyLeadingIgnoredLinesWithInvariantCaseMatching",
    "ProjectLucid.Editor.Tests.OriginalHLCrashReportPreservationTests.FirstFunctionRetainsLiteralFirstLineAndDoesNotTrimPrefixes",
    "ProjectLucid.Editor.Tests.OriginalHLCrashReportPreservationTests.ReadonlyLogDataConstructorRetainsEverySuppliedIdentityAndInteger",
    "ProjectLucid.Editor.Tests.OriginalHLCrashReportPreservationTests.GenuineConfigurationCreationKeepsDefaultsAndIndependentOwnedAllocations",
    "ProjectLucid.Editor.Tests.OriginalHLCrashReportPreservationTests.RequiredNativeFilesDeduplicateWithoutEditingOwnedInputArrays",
    "ProjectLucid.Editor.Tests.OriginalHLCrashReportPreservationTests.NativePathsKeepAllThreeLiteralSuffixesAndUnsupportedValueFaults",
    "ProjectLucid.Editor.Tests.OriginalHLCrashReportPreservationTests.InitialiseWritesTheSameOwnedExclusionListAndRetainsNullFaults",
    "ProjectLucid.Editor.Tests.OriginalHLCrashReportPreservationTests.GenuineOwnedAppKeysRetainSuppliedDummyConstantForEverySerializedKey",
)
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalHLProfilerMarkerConverterTests.ConverterRetainsOriginalDeclarations",
    "ProjectLucid.Editor.OriginalHLProfilerMarkerConverterTests.TableRetainsOriginalEntriesAndInsertionOrder",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.MissionStringsFormattingTests.MissionTimer_NonpositiveMissingAndCustomFormats",
    "ProjectLucid.Tests.MissionStringsFormattingTests.MissionTimer_FiniteFractionalFloorAndTruncation",
    "ProjectLucid.Tests.MissionStringsFormattingTests.MissionDescription_PositiveArgumentsAndNaNDistinction",
    "ProjectLucid.Tests.MissionStringsFormattingTests.MissionFormats_OrdinaryFaultsAndPositiveTimerEvaluationOrder",
)
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalHLProfilerCategoryConverterTests.ConverterAndComparersRetainOriginalDeclarations",
    "ProjectLucid.Editor.OriginalHLProfilerCategoryConverterTests.TableMapsEveryCategoryInOrderUsingTheSharedComparer",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.UniqueCollectableStateDataTests.RegistrationAndCollectionKeepIndependentIdsAndCountOnlyOnce",
    "ProjectLucid.Tests.UniqueCollectableStateDataTests.ResetRetainsRegisteredIdsForRepeatedCollectionCycles",
    "ProjectLucid.Tests.UniqueCollectableStateDataTests.NullKeyFaultsRetainDistinctOutputWriteOrderAndExistingState",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.SequencedEffectTests.DefaultsAndNonPositiveDelaysKeepOriginalHooks",
    "ProjectLucid.Tests.SequencedEffectTests.InclusiveAndExclusiveGatesUseRealCurrentInput",
    "ProjectLucid.Tests.SequencedEffectTests.RejectedCallbackReentryAndFaultKeepLatestCompletion",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalChallengeRewardSaveTests.OriginalRewardMergeRetainsAllFlagsAndRawDirtyStates",
    "ProjectLucid.Tests.OriginalChallengeRewardSaveTests.OriginalRewardSettersAndEmptyHooksKeepSaveSemantics",
    "ProjectLucid.Tests.OriginalChallengeRewardSaveTests.RealUnityRewardSerializationKeepsOriginalDataAndRuntimeFlags",
)

EDITMODE_TESTS += (
    "ProjectLucid.Editor.Tests.OfflineAnalyticsTests.DefaultFacadeUsesOfflineProviderAndCompletesLifecycleLocally",
    "ProjectLucid.Editor.Tests.OfflineAnalyticsTests.DiscardReleasesOriginalEventCallbacksWithoutCollectingLateData",
    "ProjectLucid.Editor.Tests.OfflineAnalyticsTests.UnsupportedOriginalEventReusesItsStubWithoutInitialisingThePool",
    "ProjectLucid.Editor.Tests.OfflineAnalyticsTests.GenuineAnalyticsConfigurationRetainsItsAuthoredInspectorDefault",
)

EDITMODE_TESTS += (
    "ProjectLucid.Editor.Tests.PortableControllerSelectionTests.DefaultSelectionUsesOriginalUnityProviderWithIndependentNotifications",
    "ProjectLucid.Editor.Tests.PortableControllerSelectionTests.ConnectionNotificationWorksBeforeSubscriptionAndAfterUnsubscription",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.LocalCoreNativeBridgeTests.LocalePolicyTracksTwoOwnedCultureScopes",
    "ProjectLucid.Tests.LocalCoreNativeBridgeTests.CountryPolicyResolvesNeutralCulturesThroughTheirSpecificCulture",
    "ProjectLucid.Tests.LocalCoreNativeBridgeTests.CountryPolicyTracksEachSpecificCultureAfterInvariant",
    "ProjectLucid.Tests.LocalCoreNativeBridgeTests.CountryPolicyReturnsEmptyForUnmappedNeutralCultures",
    "ProjectLucid.Tests.LocalCoreNativeBridgeTests.OfflineClientCodeReturnsOneAndWritesZero",
    "ProjectLucid.Tests.LocalCoreNativeBridgeTests.MissingOwnerFaultsBeforeTheOfflineInitialiseBoundary",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.LocalAccountTests.OfflinePolicyFactoriesIgnoreForceStubAndKeepIdentitySeparateFromAuthentication",
    "ProjectLucid.Tests.LocalAccountTests.RealCallbacksPreserveOrderPayloadAndClear",
    "ProjectLucid.Tests.LocalAccountTests.CallbackFaultStopsLaterPhotoCompletionAndOwnedClearRecovers",
    "ProjectLucid.Tests.LocalAccountTests.GenuineFacadeDefaultsKeepFailedAuthenticationAndShutdownCleanup",
    "ProjectLucid.Tests.LocalAccountTests.GenuinePlayerPhotoIteratorCompletesWithoutYieldThroughLocalCallbacks",
)


EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalUIHighlightTests.WholeOwnersRetainSerializedFieldsAndPublicShowContract',
    'ProjectLucid.Tests.OriginalUIHighlightTests.NormalOwnedAttachmentKeepsReferenceGettersAndNaturallyObservedShowBranch',
    'ProjectLucid.Tests.OriginalUIHighlightTests.SerializedOwnedReferencesReturnUnchangedFromBothGetters',
    'ProjectLucid.Tests.OriginalUIHighlightTests.PlainOwnedAttachmentLeavesEventNullAndShowSkipsIt',
    'ProjectLucid.Tests.OriginalUIHighlightTests.GenuineEventRunsOwnedListenersSynchronouslyAndRemovesOnlyThoseListeners',
    'ProjectLucid.Tests.OriginalUIHighlightTests.ListenerExceptionEscapesShowSynchronouslyAndLaterOwnedListenerRemainsRegistered',
    'ProjectLucid.Tests.OriginalUIHighlightTests.TwoNormallyOwnedComponentsKeepDistinctEventAndDefinitionObjectIdentities',
)


EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalNumericFSMTests.SetFloatCapturesArgumentsAndPerformsSingleArithmetic',
    'ProjectLucid.Tests.OriginalNumericFSMTests.InvalidSetOperationsInsertZeroBeforeTheirOriginalFault',
    'ProjectLucid.Tests.OriginalNumericFSMTests.SetIntRetainsSignedUncheckedArithmeticAndCapturedValues',
    'ProjectLucid.Tests.OriginalNumericFSMTests.CheckFloatUsesStrictToleranceAndOrderedComparisonsWithoutInsertion',
    'ProjectLucid.Tests.OriginalNumericFSMTests.CheckFloatRangeKeepsInclusiveBoundsNaNAndMissingReadSemantics',
    'ProjectLucid.Tests.OriginalNumericFSMTests.CheckIntUsesSignedComparisonsAndMissingZeroWithoutInsertion',
    'ProjectLucid.Tests.OriginalNumericFSMTests.DiceRollUsesGenuineRangeBeforeStorageAndRestoresOwnedRandomState',
    'ProjectLucid.Tests.OriginalNumericFSMTests.GenuineFactoriesRetainKeysCaseInsensitiveEnumsAndJsonRoundTrips',
    'ProjectLucid.Tests.OriginalNumericFSMTests.NullSetArgumentsRetainNormalBaseRegistrationBeforeFailure',
    'ProjectLucid.Tests.OriginalNumericFSMTests.ShortcutConstructorsRetainOriginalDefaultIdentifierRoles',
    'ProjectLucid.Tests.OriginalNumericFSMTests.NumericFamiliesAndTooltipKeepGenuineMetadataContracts',
)


EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalMenuBloomPreservationTests.OriginalOwnerRetainsFieldsAttributesDefaultAndNaturalIteratorContract",
    "ProjectLucid.Tests.OriginalMenuBloomPreservationTests.NormalInactiveAttachmentRetainsConstructorDefaultAndRequiredComponents",
)

PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalMenuBloomLifecycleTests.NonpositiveAndNaNDurationsCompleteSynchronouslyWithoutFading",
    "ProjectLucid.Tests.OriginalMenuBloomLifecycleTests.StallRetainsEntryAlphaRejectsReentryAndFinishOnlyReleasesIt",
    "ProjectLucid.Tests.OriginalMenuBloomLifecycleTests.PositiveFadeHoldsLastRisingSampleAndCompletesAfterRelease",
    "ProjectLucid.Tests.OriginalMenuBloomLifecycleTests.StoppingTheOwnedCoroutineDoesNotInventCancellationCleanup",
)

# Original mutable Pair generic helper: owned declarations and behavior.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalPairPreservationTests.OriginalOwnerRetainsUnconstrainedMutableStaticAndGeneratedProperties",
    "ProjectLucid.Tests.OriginalPairPreservationTests.ConstructorsAndPropertiesRetainDefaultAndSuppliedValues",
    "ProjectLucid.Tests.OriginalPairPreservationTests.InstanceReusesOneMutablePairForEachOwnedClosedType",
)

# Original screen orientation and enum-obsolescence helpers with owned genuine reflection.
EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalScreenOrientationExtensionsPreservationTests.OriginalDeclarationsRetainStaticExtensionsAndEnumOnlyConstraint',
    'ProjectLucid.Tests.OriginalScreenOrientationExtensionsPreservationTests.LandscapeAcceptsOnlyTheTwoOriginalUnderlyingValues',
    'ProjectLucid.Tests.OriginalScreenOrientationExtensionsPreservationTests.ObsoleteAliasesRetainAnyNonobsoleteEquivalentValue',
    'ProjectLucid.Tests.OriginalScreenOrientationExtensionsPreservationTests.EnumBaseConstraintUsesTheActualBoxedEnumType',
    'ProjectLucid.Tests.OriginalScreenOrientationExtensionsPreservationTests.NullAndUndefinedValuesKeepManagedReflectionFaults',
)

EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalScreenManagerEditModeTests.CompleteDeclaredManagerAndEnumShapes',
    'ProjectLucid.Tests.OriginalScreenManagerEditModeTests.TwoNaturalIteratorsHaveTwelveManagedRoles',
    'ProjectLucid.Tests.OriginalScreenManagerEditModeTests.InactiveAttachmentRetainsOriginalConstructorDefaults',
    'ProjectLucid.Tests.OriginalScreenManagerEditModeTests.InactiveNoChangeSuppressesCompletion',
    'ProjectLucid.Tests.OriginalScreenManagerEditModeTests.InactiveLockedChangeCompletesOnceThenSameOrientationSuppresses',
    'ProjectLucid.Tests.OriginalScreenManagerEditModeTests.InactivePublicSubscriptionsRetainOwnedMembership',
    'ProjectLucid.Tests.OriginalScreenManagerEditModeTests.InactiveLockAndTogglePreserveOriginalGate',
)

PLAYMODE_TESTS += (
    'ProjectLucid.Tests.OriginalScreenManagerPlayModeTests.GenuinePlayLifecycleSavesOwnedPropertiesAndTearsDown',
)

PLAYMODE_TESTS += (
    'ProjectLucid.Tests.UIInputTypeVisibilityLifecycleTests.SelectionTypesHidePointerGroupBeforeShowingControllerGroupAndBack',
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalSelectableBasePreservationTests.CompleteOriginalBasesRetainAbstractSlotsAndEmptyInstanceShape",
    "ProjectLucid.Tests.OriginalSelectableBasePreservationTests.OriginalEventInterfacesBindToDeclaredAbstractHandlers",
    "ProjectLucid.Tests.OriginalSelectableBasePreservationTests.DestructionParameterRetainsOriginalOptionalFalseDefault",
)

EDITMODE_TESTS += (
    "OriginalHLCurveTests.OriginalCurveAndCollectionRetainCompleteDeclarationContracts",
    "OriginalHLCurveTests.OriginalFreshCurveOwnsEmptyAnimationCurveAndPositiveZeroEnd",
    "OriginalHLCurveTests.OriginalNormalisedSamplesUseZeroToLastKeyAndAllThreeRoutes",
    "OriginalHLCurveTests.OriginalRawTimeSamplesPreserveOwnedLoopWrappingWithoutClamp",
    "OriginalHLCurveTests.OriginalCollectionSerializesSixOwnedCurvesAndDirectionThroughGetters",
)

EDITMODE_TESTS += (
    'ProjectLucid.Editor.OriginalUISoundEffectManagerTests.OriginalGlobalSoundManagerRetainsCompleteDeclarations',
    'ProjectLucid.Editor.OriginalUISoundEffectManagerTests.OriginalInactiveSoundManagerWithNullSourceSuppressesPlaybackRoutes',
    'ProjectLucid.Editor.OriginalUISoundEffectManagerTests.OriginalSoundManagerForwardsPitchVolumeToOwnedMutedAudioSource',
)

EDITMODE_TESTS += (
    'OriginalGradientTests.OriginalGradientRetainsWholeDeclarationAndOrderedAnnotations',
    'OriginalGradientTests.OriginalGradientNormalOwnedConstructionRetainsDefaults',
    'OriginalGradientTests.OriginalGradientPublicSettersRetainUnboundedFiniteValues',
    'OriginalGradientTests.OriginalGradientInactiveModifyMeshKeepsWholeTriangleStream',
    'OriginalGradientTests.OriginalGradientActiveModifyMeshReplacesColorAndUV1Only',
    'OriginalGradientTests.OriginalGradientActiveModifyMeshProjectsOffsetsScalesAndClamps',
    'OriginalGradientTests.OriginalGradientAwakeNormalizesAuthoredDirectionAndClampsScale',
)

EDITMODE_TESTS += (
    'ProjectLucid.Editor.OriginalHLUIEffectManagerTests.OriginalEffectManagerRetainsWholeTwoOwnerDeclarations',
    'ProjectLucid.Editor.OriginalHLUIEffectManagerTests.PopulatedCuesStopAtNoneAndNullClipsBeforeTheSoundManager',
    'ProjectLucid.Editor.OriginalHLUIEffectManagerTests.OriginalInactiveEffectManagerRetainsDefaultsAndEmptyReferenceSetter',
    'ProjectLucid.Editor.OriginalHLUIEffectManagerTests.OriginalEffectManagerSamplesEighteenGenuineSerializedFiniteCurves',
    'ProjectLucid.Editor.OriginalHLUIEffectManagerTests.OriginalEffectManagerReturnsGenuineOwnedSerializedVisualValues',
    'ProjectLucid.Editor.OriginalHLUIEffectManagerTests.OriginalAudioCueRoundTripsRawKeysAndEmptyManagerSuppressesDispatch',
    'ProjectLucid.Editor.OriginalEaseUtilitiesTests.OriginalDeclarationsAndOrderedOptionMetadata',
    'ProjectLucid.Editor.OriginalEaseUtilitiesTests.PolynomialNominalValuesAndRawInputs',
    'ProjectLucid.Editor.OriginalEaseUtilitiesTests.OriginalIntegralValuesAndUpperBranchDiscontinuity',
    'ProjectLucid.Editor.OriginalEaseUtilitiesTests.TranscendentalBounceAndBackNominalValues',
    'ProjectLucid.Editor.OriginalEaseUtilitiesTests.LiteralDispatcherAndDefaultPassThrough',
    'ProjectLucid.Editor.OriginalEaseUtilitiesTests.EndpointClampsAndNaNPropagation',
    'ProjectLucid.Editor.OriginalHLButtonHighlighterTests.OriginalDeclarationsAndAbstractModificationContract',
    'ProjectLucid.Editor.OriginalHLButtonHighlighterTests.OrdinaryTargetArraysPreserveEmptyAndNullFaults',
    'ProjectLucid.Editor.OriginalHLButtonHighlighterTests.OwnedTargetsDispatchEachCallbackToItsSlotInAscendingOrder',
    'ProjectLucid.Editor.OriginalHLButtonHighlighterTests.OwnedInactiveCanvasLayerOverloadsAndReset',
    'ProjectLucid.Editor.OriginalHLButtonHighlighterTests.OwnedInactiveCanvasLayerOverloadsWriteDistinctLayersThatResetKeeps',
    'ProjectLucid.Editor.OriginalHLButtonHighlighterTests.InactiveStatesCopyListsAndResetAnchorsWithoutCoroutine',
    'ProjectLucid.Editor.OriginalHLButtonHighlighterTests.NullDefaultFaultFollowsAnchorAndCacheRotation',
    'ProjectLucid.Editor.OriginalHLButtonHighlighterTests.MissingRectTransformFaultPrecedesStateMutation',
    'ProjectLucid.Editor.OriginalHLButtonHighlighterTests.GenuineRequiredBehavioursEnableBeforeNullElementFault',
    'ProjectLucid.Editor.OriginalHLButtonHighlighterTests.DrivenIteratorOrdersGrowEqualPassesAndPreservesShrinkIndexFault',
)

EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalFastStringBuilderTests.CompleteOriginalDeclarationsRetainVisibilityAndImplementationFlags',
    'ProjectLucid.Tests.OriginalFastStringBuilderTests.OrdinaryConstructorsAndConversionsPreserveUnicodeScalars',
    'ProjectLucid.Tests.OriginalFastStringBuilderTests.RawCapacityAccessAndNegativeLengthKeepShrinkOnlyPublication',
    'ProjectLucid.Tests.OriginalFastStringBuilderTests.FullCapacityCopyWritesForwardAndPublishesLengthOnlyOnSuccess',
    'ProjectLucid.Tests.OriginalFastStringBuilderTests.SelfInsertRejectsBeforeZeroCountAndSelfSubstringReadsAfterClear',
    'ProjectLucid.Tests.OriginalFastStringBuilderTests.NominalMutationsComposeAndSubstringOwnsIndependentState',
    'ProjectLucid.Tests.OriginalFastStringBuilderTests.BuilderReplacePreservesSkippedMatchAndAsymmetricShrinkQuirks',
    'ProjectLucid.Tests.OriginalFastStringBuilderTests.ConversionFailuresRetainPublishedLengthAndRawScalarStorage',
)

EDITMODE_TESTS += (
    'OriginalTMPSettingsSerializationTests.OwnedTMPSettingsExposesBuiltinVector2FieldsAndZeroChildren',
    'OriginalTMPSettingsSerializationTests.OwnedTMPSettingsOmitsNonSerializableLineBreakingTableBeforeHashSetTraversal',
)

EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalRTLShapingTests.CompleteHandwrittenDeclarationsAndOriginalOptionalDefaultsArePreserved',
    'ProjectLucid.Tests.OriginalRTLShapingTests.SignedChar32GateAndNumberSelectionRetainTheirBoundaries',
    'ProjectLucid.Tests.OriginalRTLShapingTests.FirstVisibleLetterAndTagClassificationPreserveTheOriginalThrow',
    'ProjectLucid.Tests.OriginalRTLShapingTests.GenuineGlyphTableAndAllFourLamJoinsRemainLiteral',
    'ProjectLucid.Tests.OriginalRTLShapingTests.YahAndNumberTagProtectionUseGenuinePublicTransformations',
    'ProjectLucid.Tests.OriginalRTLShapingTests.RichTextTagFieldsAndWholeTagReversalKeepOriginalOrder',
    'ProjectLucid.Tests.OriginalRTLShapingTests.TashkeelPositionsAndRepeatedRestorePreserveSharedStateSemantics',
    'ProjectLucid.Tests.OriginalRTLShapingTests.ShaddaCombinesOnlyItsOriginalFollowingMarks',
    'ProjectLucid.Tests.OriginalRTLShapingTests.LigatureReversalAppendsAndPreservesTheOriginalScalarQuirk',
    'ProjectLucid.Tests.OriginalRTLShapingTests.WholePipelineDefaultsAndSuccessiveCallsUseGenuineSharedBuffers',
)


# Original selectable-family controlled Editor obligations.
EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalHLButtonAndInputListenerTests.ButtonWholeDeclaredMembersKeepOriginalFlagsAndTypedSignatures',
    'ProjectLucid.Tests.OriginalHLButtonAndInputListenerTests.ListenerWholeNaturalFamilyKeepsOriginalFlagsAndTypedSignatures',
    'ProjectLucid.Tests.OriginalHLButtonAndInputListenerTests.InactiveButtonConstructsFiveIndependentMutableEvents',
    'ProjectLucid.Tests.OriginalHLButtonAndInputListenerTests.InactivePublicActionsReturnBeforeNullEventsOrSoundRoutes',
    'ProjectLucid.Tests.OriginalHLButtonAndInputListenerTests.InactiveAnimationDisabledPressReturnsFalseWithoutDispatch',
    'ProjectLucid.Tests.OriginalHLButtonAndInputListenerTests.InactiveListenerReturnsGenuineObservedArrayWithoutSetup',
    'ProjectLucid.Tests.OriginalHLButtonAndInputListenerTests.OrdinarySelectedButtonInputKeepsNullProviderAndZeroDefaults',
    'ProjectLucid.Tests.OriginalHLButtonAndInputListenerTests.OrdinarySelectedButtonInputKeepsFirstProviderAndReadsItsTextEachTime',
    'ProjectLucid.Tests.OriginalHLButtonAndInputListenerTests.InactiveNonInteractablePressDispatchesEventBeforeRereadingDisabledSound',
    'ProjectLucid.Tests.OriginalHLButtonAndInputListenerTests.InactiveListenerAppendsClosuresPressesBeforeEventsAndOrdersShutdownAroundFaults',
    'ProjectLucid.Editor.OriginalUIUtilsTests.OriginalUIUtilsRetainsCompleteGenericDefaultAndAnnotationContracts',
    'ProjectLucid.Editor.OriginalUIUtilsTests.OriginalFormattingUsesSingleConversionAndGenuineCultureProviders',
    'ProjectLucid.Editor.OriginalUIUtilsTests.OriginalRefColourUsesExactChannelsAndRetainsManagedNaNBranch',
    'ProjectLucid.Editor.OriginalUIUtilsTests.OriginalGenericSettersUseOwnedObjectEqualityDispatch',
    'ProjectLucid.Editor.OriginalUIUtilsTests.OriginalVisualHelpersForwardToGenuineOwnedUnityComponents',
    'ProjectLucid.Editor.OriginalUIUtilsTests.OriginalFrontPlaneProjectionUsesGenuineOwnedCamera',
    'ProjectLucid.Editor.OriginalUIUtilsTests.OriginalSpritePivotDividesDefaultHalfAndShrinkReturnsBeforeNativeSize',
    'ProjectLucid.Tests.OriginalHLNavigationTests.WholeNavigationDeclarationsRetainOriginalFieldsModesAndAccessors',
    'ProjectLucid.Tests.OriginalHLNavigationTests.OrdinaryNavigationPreservesZeroDefaultUndefinedModesAndValueCopies',
)

# Original number-formatting and UI-visibility Editor obligations.
EDITMODE_TESTS += tuple("OriginalLanguageUtilsTests." + name for name in (
    "LanguageUtilsRetainsWholeAuthoredTypeFieldsAndOrderedAnnotations",
    "LanguageUtilsRetainsWholeOverloadsPrivateInstanceLeavesAndTypedDefaults",
    "LanguageUtilsInactiveConstructionRetainsOriginalNoneAndNullCaches",
    "LanguageUtilsOwnedAbbreviationRetainsSignedStrictThresholdsAndNullOutputs",
    "LanguageUtilsOwnedAbbreviationRetainsNonfiniteAndSignedZeroResults",
    "LanguageUtilsOwnedAbbreviationPairsEachBranchWithItsFormatCache",
))
EDITMODE_TESTS += tuple("OriginalUIVisibilityControllerTests." + name for name in (
    "InputVisibilityRetainsCompleteAuthoredFieldsMethodsAndOrderedOptions",
    "PlatformVisibilityRetainsCompleteAuthoredBindingAndUnconditionalStartDeclaration",
    "InputVisibilityNaturalCaptureRetainsWholeCurrentRoleWithoutQueuedCallbackExecution",
    "InputVisibilityInactiveOwnedBindingsRoundTripWithoutBridgeOrSubscription",
    "PlatformVisibilityInactiveOwnedBindingRoundTripsWithoutActivatingOverrides",
    "PlatformVisibilityStartCallsOverridesUnconditionallyWithoutNullGuard",
))

# Original stacked-camera declaration obligations.
EDITMODE_TESTS += tuple("OriginalStackedCameraTests." + name for name in (
    "StackedCameraRetainsWholeOriginalTypeFieldsAndOrderedAnnotations",
    "StackedCameraRetainsSixOriginalMethodsConstructorAndParameterContracts",
))

# Genuine owned event, generic data, typed asset-reference and object-pointer serialization.
EDITMODE_TESTS += tuple("OriginalGenericSerializationFrontierTests." + name for name in (
    "OriginalGenericFieldSignaturesAndEditorProjectionRemainExplicit",
    "OwnedClosedUnityEventExposesExactEmptyPersistentCallTree",
    "OwnedPersistentCallPayloadRoundTripsWithoutDispatchOrTargetActivation",
    "OwnedTypedAddressableListsPreserveGuidOrderAndEditorChildrenWithoutLoads",
    "OwnedGenericDataSubstitutesIntWhileNonSerializableBindableAndDictionaryStayAbsent",
    "OwnedReferenceOnlyCarrierUsesPPtrPropertiesWithoutConstructingOriginalButtons",
))

EDITMODE_TESTS += tuple("OriginalMenuDataContractsTests." + name for name in (
    "Menu22TopLevelTypesKeepLiteralNamesAssemblyAndOriginalFlags",
    "ParametersKeepOriginalReadonlyFieldAndOrdinaryPublicContracts",
    "ParametersCloneAndEqualityRetainGenuineNullAndExactTypeBehavior",
    "ParametersEqualityRequiresTheExactRuntimeType",
    "RecordStoresSignedIdentifiersAndOwnedReferencesWithoutDispatch",
    "OverlayPendingValueDefaultAndCopiesRetainDataReferenceIdentity",
    "InitializerSerializedFieldsAndConditionalStartAreMetadataOnly",
    "MenuPageFadeFieldKeepsProtectedBooleanAndOrderedSerializationAttributes",
))

EDITMODE_TESTS += (
    'OriginalToggleCursorVisibilityContractsTests.AuthoredCursorOwnerKeepsAllFieldsMethodsAndOrderedOptionsWithoutConstruction',
    'OriginalToggleCursorVisibilityContractsTests.CachedStartPredicateKeepsWholeDiscoveredCompilerRoleWithoutStaticReads',
    'OriginalToggleCursorVisibilityContractsTests.StartIteratorKeepsCompleteInterfacesCaptureAndDebuggerAnnotationsWithoutMoving',
)

EDITMODE_TESTS += (
    'OriginalHLInputDebugMenuContractsTests.AuthoredDebugOwnerKeepsWholeDeclarationsAndOriginalParameterNamesWithoutConstruction',
    'OriginalHLInputDebugMenuContractsTests.DiscoveredGlyphComparatorCacheKeepsWholeRoleWithoutStaticValueReads',
)

EDITMODE_TESTS += (
    'OriginalLocalisedUIStringContractsTests.LocalisedOwnerKeepsWholeTypeAndOrderedOptionsWithoutConstruction',
    'OriginalLocalisedUIStringContractsTests.LocalisedFieldsKeepExactTargetTypesAndHashEnumSerializationOrder',
    'OriginalLocalisedUIStringContractsTests.LocalisedMethodsKeepPublicLifecycleAndExactNullOverrideContract',
    'OriginalLocalisedUIStringOwnedTextTests.InactiveOriginalDiscoveryWritesOwnedUguiTextAndEmptyOverrideWithoutLookup',
    'OriginalAssetReferenceProfileTests.OwnedClosedAssetReferenceExposesFourEditorChildrenWithoutAnOperation',
    'OriginalAssetReferenceProfileTests.AddressablesContractRealAssetAssignmentPreservesGuidAndTrueEditorFlagThroughSerialization',
    'OriginalAssetReferenceProfileTests.OwnedPublicNullClearSetsEditorFlagAndClearsGuidAndSubObjectName',
)

EDITMODE_TESTS += (
    'OriginalStringFormatterOwnedBindingTests.InactiveOriginalFormatterReadsSerializedOutputReferencesAndPublicRetarget',
)

EDITMODE_TESTS += (
    'OriginalStringFormatterVector2SerializationTests.InactiveOriginalFormatterPreservesVector2PayloadThroughEditorJson',
)

EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalApplicationReadinessTests.AppReadinessPreservesItsOriginalFactoryAndOverrideDeclarations',
    'ProjectLucid.Tests.OriginalApplicationReadinessTests.GuiReadinessPreservesItsOriginalStorageSelectionAndJsonDeclarations',
    'ProjectLucid.Tests.OriginalApplicationReadinessTests.OpenUiContainerPreservesItsOriginalFieldsFactoriesAndJsonDeclarations',
    'ProjectLucid.Tests.OriginalApplicationReadinessTests.AppReadinessEarlyPathsRetainOwnedStorageAndTheOriginalNullFault',
    'ProjectLucid.Tests.OriginalDefaultAndNewDictionaryOwnedBehaviorTests.MissingLookupInsertsOneFreshValuePerKeyOnlyInDictionaryWithNew',
    'ProjectLucid.Tests.OriginalDefaultAndNewDictionaryOwnedBehaviorTests.ExistingNullAndExistingValueRemainPresentWithoutReplacement',
    'ProjectLucid.Tests.OriginalDefaultAndNewDictionaryOwnedBehaviorTests.OriginalUnsupportedObjectOperationsThrowWithoutChangingOwnedContents',
    'ProjectLucid.Tests.OriginalDefaultAndNewDictionaryOwnedBehaviorTests.NongenericEnumerationWorksForNewButRemainsUnsupportedForDefault',
    'ProjectLucid.Tests.OriginalDefaultAndNewDictionaryOwnedBehaviorTests.FindValueVisitsTheObservedValueOrderAndStopsAtItsFirstMatch',
    'ProjectLucid.Tests.OriginalDefaultAndNewDictionaryOwnedBehaviorTests.FindValueLeavesAnEmptyNullPredicateUncalledAndPropagatesRealCallbackFaults',
)


EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalSplashBoundaryTests.BootAndUnloadKeepTheirOriginalSceneJsonAndStateContracts',
    'ProjectLucid.Tests.OriginalSplashBoundaryTests.IntroAndFairplayStatesKeepOriginalInheritanceAndRefDeclarations',
    'ProjectLucid.Tests.OriginalSplashBoundaryTests.ScenePredicatesKeepDistinctOriginalJsonAndStorageMethodContracts',
    'ProjectLucid.Tests.OriginalSplashBoundaryTests.FairPlayKeepsItsOriginalAssemblyImportAndExceptionClauseWithoutInvocation',
    'ProjectLucid.Tests.OriginalSplashBoundaryTests.BootFactoryOwnsItsOriginalSceneDataWithoutEnteringPlatformOrLoadCode',
    'ProjectLucid.Tests.OriginalSplashBoundaryTests.UnloadAbsentSceneKeyLeavesNormallyOwnedTablesAndUnrelatedValuesUntouched',
    'ProjectLucid.Tests.OriginalSplashBoundaryTests.UnloadMissingTableStoresOriginalNullDefaultBeforeItsManagedDictionaryFault',
)


EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalLinkedLevelTestsBoundaryTests.OriginalDeclarationsKeepScopedAuthoredRecordsAndAnnotations',
    'ProjectLucid.Tests.OriginalLinkedLevelTestsBoundaryTests.OwnedConstructorsAndCallbacksKeepOriginalNamesAndReferences',
    'ProjectLucid.Tests.OriginalLinkedLevelTestsBoundaryTests.NonCycleSearchSkipsUnmatchedDataAndContinuesAfterEmptyMatches',
    'ProjectLucid.Tests.OriginalLinkedLevelTestsBoundaryTests.OwnedJsonRoundTripKeepsNestedDataAndNullScene',
)

EDITMODE_TESTS += ('OriginalFontReferenceConversionTests.OwnedOriginalAndExplicitConvertedFontCollectionReferencesImportWithOriginalKeys',)

# Genuine owned configuration serialization; bootstrap and payload readiness remain separate.
EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalConfigurationAssetSerializationTests.ActiveConfigurationPreservesItsSerializedRuntimeReferenceAndGetter',
    'ProjectLucid.Tests.OriginalConfigurationAssetSerializationTests.RuntimeConfigurationKeepsTwelveOrderedSlotsAndCreatesReadonlyWrappers',
    'ProjectLucid.Tests.OriginalConfigurationAssetSerializationTests.GameLevelsPreservesBaseOverridesAndAuthoredReferenceOrderWithoutCaching',
    'ProjectLucid.Tests.OriginalConfigurationAssetSerializationTests.ActualPreparedConfigurationOwnersRetainOriginalPointersAndScopedLoadedFields',
)

EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalGameConfigurationTests.OriginalConfigurationDeclarationsRetainScopedFieldsMethodsAndAnnotations',
    'ProjectLucid.Tests.OriginalGameConfigurationTests.OwnedSeverityLookupKeepsFirstMatchFallbackAndPublicFailureLevels',
    'ProjectLucid.Tests.OriginalGameConfigurationTests.OwnedSceneExclusionsUseOrderedCaseSensitiveSubstringsAndBlankRejection',
    'ProjectLucid.Tests.OriginalGameConfigurationTests.OwnedBuildFeaturesPreserveOrderDuplicatesAndLiveReadOnlyViews',
    'ProjectLucid.Tests.OriginalGameConfigurationTests.OwnedConstructorFailureAndSerializedApplyKeepTheRealFeatureCache',
)

EDITMODE_TESTS += ("OriginalFontReferenceConversionTests.PythonConverterProducedFontCollectionOwnersImportTheirExactBytes",)

# Genuine owned menu configuration storage; original initialization stays separate.
EDITMODE_TESTS += (
    'ProjectLucid.Tests.Editor.OriginalMenuConfigurationAssetSerializationTests.GenuineOwnedCoreGraphRetainsSeparateBaseOverridesAndTwelveRuntimeSlots',
    'ProjectLucid.Tests.Editor.OriginalMenuConfigurationAssetSerializationTests.GenuineOwnedLocalisationPreservesAuthoredPathsFlagsAndOverrideOrder',
    'ProjectLucid.Tests.Editor.OriginalMenuConfigurationAssetSerializationTests.GenuineOwnedHashedRemainsFieldlessPlainScriptableObjectAfterReimport',
)

# Owned original procedural-mesh and CrossPromo configuration controls.
EDITMODE_TESTS += (
    'ProjectLucid.Tests.Editor.OriginalProceduralAndCrossPromoConfigurationTests.ProceduralMesh_DefaultsRemainDistinctFromAuthoredSettings',
    'ProjectLucid.Tests.Editor.OriginalProceduralAndCrossPromoConfigurationTests.CrossPromo_DefaultsRetainOriginalStringsWidthsAndUnwrittenFields',
    'ProjectLucid.Tests.Editor.OriginalProceduralAndCrossPromoConfigurationTests.AllSeventeenDirectGettersProjectGenuineSerializedTransactions',
    'ProjectLucid.Tests.Editor.OriginalProceduralAndCrossPromoConfigurationTests.AuthoredScalarSettingsRoundTripThroughOnlyFreshOwnedAssets',
    'ProjectLucid.Tests.Editor.OriginalProceduralAndCrossPromoConfigurationTests.ServerSelectsDevOnlyForZeroAndProdForEveryTestedNonzero',
    'ProjectLucid.Tests.Editor.OriginalProceduralAndCrossPromoConfigurationTests.EmptyValidatePreservesCompleteOwnedStateAndOriginalBaseReferences',
    'ProjectLucid.Tests.Editor.OriginalProceduralAndCrossPromoConfigurationTests.CompleteDeclaredContractAndOrderedAttributesRemainOriginal',
)

# Read-only supplied menu imports; initialization and provider payloads remain separate.
EDITMODE_TESTS += (
    'ProjectLucid.Tests.Editor.OriginalMenuPreparedAssetImportTests.ActualSuppliedMenuAssetsRetainOriginalScriptsFieldsGraphAndRuntimeOrder',
)

# Owned Loading factories and early faults; genuine App/system entry stays separate.
EDITMODE_TESTS += (
    'ProjectLucid.Tests.OriginalLoadingBootstrapTests.LoadingFactoryIgnoresJsonAndRegistersTheGenuineOwnedState',
    'ProjectLucid.Tests.OriginalLoadingBootstrapTests.LoadingConstructorKeepsOriginalStateReplacementWithoutProviderEntry',
    'ProjectLucid.Tests.OriginalLoadingBootstrapTests.LoadingNullUserFaultsBeforeAppSystemsAndRetainsOwnedRegistration',
    'ProjectLucid.Tests.OriginalLoadingBootstrapTests.LoadingRealWrongGraphUserFaultsBeforeSystemsAndRetainsOwnedStorage',
    'ProjectLucid.Tests.OriginalLoadingBootstrapTests.OriginalOwnersRetainDeclarationsTypedAttributesAndNaturalCallbacks',
    'ProjectLucid.Tests.OriginalLoadingBootstrapTests.CharacterEmptyOriginalReturnsAndDevelopmentConditionRemain',
)

# The root's pointers are qualified separately from referenced group payloads.
EDITMODE_TESTS += (
    'ProjectLucid.Tests.Editor.OriginalDataDefinitionsAssetImportTests.DataDefinitionsRetainsOriginalFieldsAndTypedAuthoringDeclarations',
    'ProjectLucid.Tests.Editor.OriginalDataDefinitionsAssetImportTests.GenuineOwnedDataDefinitionsRetainsFiftySevenNullSlotsAndMonoScriptAfterReimport',
    'ProjectLucid.Tests.Editor.OriginalDataDefinitionsAssetImportTests.ActualSuppliedDataDefinitionsPreservesScriptAndFiftySevenOrderedRawReferences',
)

# Owned closed storage and callbacks; supplied group payloads require separate qualification.
EDITMODE_TESTS += (
    'ProjectLucid.Tests.Editor.OriginalStateMachinesGroupSerializationTests.ClosedStateMachinesGroupRetainsThreeArgumentElementAndOriginalConstraints',
    'ProjectLucid.Tests.Editor.OriginalStateMachinesGroupSerializationTests.FsmDeclarationRetainsOriginalAttributeOrderAndCurrentConstructorTypes',
    'ProjectLucid.Tests.Editor.OriginalStateMachinesGroupSerializationTests.GenuineOwnedGroupPreservesOrderedNameAndDataStorageAfterReimport',
    'ProjectLucid.Tests.Editor.OriginalStateMachinesGroupSerializationTests.GenuineOwnedElementCallbacksRetainCurrentNameAndNullOrDestroyedNone',
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.Editor.VerificationAssemblyReferencesTests.VerificationAssemblyDeclaresAndResolvesPinnedUI",
    "ProjectLucid.Tests.Editor.VerificationAssemblyReferencesTests.PlayModeAssemblyDeclaresAndResolvesPinnedUI",
    "ProjectLucid.Tests.Editor.VerificationAssemblyReferencesTests.CoroutineScrollAssemblyDeclaresAndResolvesPinnedUI",
    "ProjectLucid.Tests.Editor.VerificationAssemblyReferencesTests.UnityUIRuntimeAssemblyDeclaresAndResolvesPinnedUI",
    "ProjectLucid.Tests.Editor.VerificationAssemblyReferencesTests.EventSystemsRuntimeAssemblyDeclaresAndResolvesPinnedUI",
)

# Compiled declaration flags only; gameplay and native layout stay separate.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalSealedTypeFlagsTests.LevelManagerLevelRetainsOriginalSealedFlags",
    "ProjectLucid.Editor.OriginalSealedTypeFlagsTests.GameTipsDefinitionRetainsOriginalSealedFlags",
)

# Fixture isolation from the live Addressables manager; package wait behavior stays separate.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.ManagedAddressableVerificationIsolationTests.ForeignDeferredCompletionIsNotDispatchedByTheVerification",
)

# Compiled declaration flags only; animation behavior and native layout stay separate.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalActorAnimatorFieldTests.AnimatorFieldRetainsOriginalPrivateInitOnlyFlags",
)

# Compiled declaration flags only; Unity serialization and gameplay stay separate.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalSerializableEnumFlagsTests.LowFilterTypeRetainsOriginalSerializableFlags",
    "ProjectLucid.Editor.OriginalSerializableEnumFlagsTests.ConstrainTypeRetainsOriginalSerializableFlags",
)

# Compiled declaration attributes only; inspector and tvOS remote behavior stay separate.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalAppleTVRemoteSetterAttributesTests.AllowExitToHomeRetainsOriginalOrderedAttributes",
)

# Compiled attributes and owned inactive serialization; scenes and gameplay stay separate.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalSerializedFieldAttributesTests.CutsceneTimelineDirectorRetainsOriginalSerializeField",
    "ProjectLucid.Editor.OriginalSerializedFieldAttributesTests.EffectListEffectsRetainsOriginalSerializeField",
    "ProjectLucid.Editor.OriginalSerializedFieldAttributesTests.RespawnPointFlagRetainsOriginalTooltipThenSerializeField",
    "ProjectLucid.Editor.OriginalSerializedFieldAttributesTests.OwnedRespawnPointSerializesTrueDisableFlag",
    "ProjectLucid.Editor.OriginalSerializedFieldAttributesTests.OwnedCutsceneSerializesDirectorThroughPrefabRoundTrip",
)

# Compiled field metadata order only; native layout and ability behavior stay separate.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalAbilityFieldOrderTests.IdleRetainsOriginalFieldOrder",
    "ProjectLucid.Editor.OriginalAbilityFieldOrderTests.GroundBoostBaseRetainsOriginalFieldOrder",
    "ProjectLucid.Editor.OriginalAbilityFieldOrderTests.JumpBaseRetainsOriginalFieldOrder",
    "ProjectLucid.Editor.OriginalAbilityFieldOrderTests.RailRetainsOriginalFieldOrder",
)

# Fixture ownership of process registry rows; original ProcessManager semantics stay separate.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.RepeatedRunsLeaveRegistryRowsAndSystemsUnchanged",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.BorrowedEmptyDefaultManagerRowKeepsItsReferencesAndObservers",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.ThrowingShutdownObserverDoesNotKeepLaterOwnedRowsRegistered",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.FailureDuringRunStillReleasesOwnedRowsAndRestoresTheRegistry",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.FailureInjectionLeavesABorrowedEmptyCoreRowUntouched",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.ShutdownObserverReplacementDoesNotLeaveAnOwnedReferenceValid",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.FailureAndReentrantReplacementLeaveNoOwnedReferenceValid",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.ReplacementActionSubscriptionsAreReleasedWithTheOwnedRow",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.MutableHashReplacementLosesItsActionWithTheOwnedRow",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.MutableHashResubscriptionReleasesEveryOwnedStoredEntry",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.PassLimitIsRecordedWhileLaterRowsAndActionsAreStillReleased",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.PassLimitKeepsAnEarlierCallbackFailure",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.ReferenceActivatedDuringTeardownIsNotNotifiedTwice",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.LaterRowCallbackRecreatingAReleasedNameIsSwept",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.TransientReplacementInALaterRowLosesItsActions",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.TransientSystemIsTrackedWhenAnEarlierStartupObserverThrows",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.RecursiveUnregisterInsideAShutdownObserverMatchesTheOriginal",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.EqualForeignSystemKeepsItsActionSubscription",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.ActionInjectionCasesPreserveAnAbsentActionRow",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.OwnedReferenceObserversAreRestoredInOrder",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.ObserverThatRemovesItselfStaysRemoved",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.OwnReferenceIsValidWhileItsObserversRun",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.ResumedOuterObserverGetsOriginalArgument",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.NestedDispatchTailFinishesBeforeCallerTail",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.GuardedRecursiveObserverContinuesAfterNestedCall",
    "ProjectLucid.Editor.FSMManagerVerificationOwnershipTests.CallbackCreatedReferenceNotifiedElsewhereIsNotNotifiedAgain",
)

# Fixture ownership of the global property store; production save behavior stays separate.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.PropertyListVerificationOwnershipTests.ForeignSaveHandlerSurvivesARefusedVerification",
    "ProjectLucid.Editor.PropertyListVerificationOwnershipTests.ForeignStoreSurvivesARefusedVerification",
    "ProjectLucid.Editor.PropertyListVerificationOwnershipTests.ColdVerificationReturnsToTheSameColdState",
    "ProjectLucid.Editor.PropertyListVerificationOwnershipTests.StorageConstructionFaultShutsDownThePublishedFixtureStoreAndAllowsRetry",
)

EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalHLProfilerDataModelCleanupTests.TeardownDisposesEveryOwnedRecorderAndKeepsAForeignOneRecording",
    "ProjectLucid.Editor.OriginalHLProfilerDataModelCleanupTests.LosingTheStoppedMarkerOwnershipLeavesItsRecorderLive",
    "ProjectLucid.Editor.OriginalHLProfilerDataModelTests.ConsoleAndManagerRetainOriginalDeclarations",
    "ProjectLucid.Editor.OriginalHLProfilerDataModelTests.ConsoleFieldInitializersMatchTheOriginalConstructor",
    "ProjectLucid.Editor.OriginalHLProfilerDataModelTests.SampleBufferAlwaysHoldsTheMaximumBecauseTheClampIsReversed",
    "ProjectLucid.Editor.OriginalHLProfilerDataModelTests.TrackedMarkersShareAGroupAndAppearInSnapshots",
    "ProjectLucid.Editor.OriginalHLProfilerDataModelTests.TwoColumnStatsAlternateColumnsAndClearTheBuilder",
)
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalHLAutoPlayTests.DeclarationsMatchTheShippedAssembly",
    "ProjectLucid.Editor.OriginalHLAutoPlayTests.StubbedRunnerReportsFailureAndHoldsNoScripts",
)
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalUIWidgetRetainLastSelectedTests.DeclarationsMatchTheShippedFirstpassAssembly",
)
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalUIWidgetRetainLastSelectedBehaviourTests.FixtureRefusesAnExistingDormantRowWithoutTouchingIt",
    "ProjectLucid.Editor.OriginalUIWidgetRetainLastSelectedBehaviourTests.MissingSelectionSubscribesEachWidgetOnceAndLeavesAutoSelectUntouched",
    "ProjectLucid.Editor.OriginalUIWidgetRetainLastSelectedBehaviourTests.OutOfRangeSelectionClearsEveryAutoSelectAndStillSubscribes",
    "ProjectLucid.Editor.OriginalUIWidgetRetainLastSelectedBehaviourTests.RepeatedRetrievalSubscribesAgainAndOnDisableRemovesOnePerWidget",
    "ProjectLucid.Editor.OriginalUIWidgetRetainLastSelectedBehaviourTests.OnSelectedStoresFirstMatchingIndexAndIgnoresForeignWidgets",
    "ProjectLucid.Editor.OriginalUIWidgetRetainLastSelectedBehaviourTests.OnSelectedMatchesDestroyedEntriesByUnityNullOrIdentity",
    "ProjectLucid.Editor.OriginalUIWidgetRetainLastSelectedBehaviourTests.ContainerClosedRetrievesOnlyWhenTheListedContainersAreExclusivelyOpen",
    "ProjectLucid.Editor.OriginalUIWidgetRetainLastSelectedBehaviourTests.AwakeLookupFailuresThrowBeforeAnySubscription",
    "ProjectLucid.Editor.OriginalUIWidgetRetainLastSelectedBehaviourTests.OnDestroyUnsubscribesOnlyWhileTheUIManagerReferenceIsValid",
    "ProjectLucid.Editor.OriginalUIWidgetRetainLastSelectedBehaviourTests.ResetFindsTheConcreteContainer",
)
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalUIWidgetRetainLastSelectedPlayTests.SuccessfulAwakeDefersRealNavigationSelectOfTheRememberedWidget",
)

# Logging host test fixture ownership of the SystemConfiguration row (startup row set aside per control); logging behavior stays separate.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.LoggingHostConfigurationOwnershipTests.OwnedConfigurationRemovesAnAbsentRowOnEveryRun",
    "ProjectLucid.Tests.LoggingHostConfigurationOwnershipTests.OwnedConfigurationBorrowsALiveRowAndRemovesOnlyItsOwnOverride",
    "ProjectLucid.Tests.LoggingHostConfigurationOwnershipTests.OwnedConfigurationRefusesADormantRowWithoutTouchingIt",
    "ProjectLucid.Tests.LoggingHostConfigurationOwnershipTests.OwnedConfigurationStillReleasesItsRowAfterAnOverrideRemovalFailure",
    "ProjectLucid.Tests.LoggingHostConfigurationOwnershipTests.OwnedConfigurationRemovesAnOverrideWhoseAddConfigThrew",
    "ProjectLucid.Tests.LoggingHostConfigurationOwnershipTests.OwnedConfigurationStillReleasesItsRowAfterAnOverrideScanFailure",
)
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalAssemblyCSharpStudioTypesTests.DeclarationsMatchTheShippedAssembly",
    "ProjectLucid.Editor.OriginalAssemblyCSharpStudioTypesTests.TimerShowsAndHidesItsSpawnedObjects",
    "ProjectLucid.Editor.OriginalAssemblyCSharpStudioBehaviourTests.TimerHidesAtExactlyZeroAndStopsCountingBelowMinusHalf",
    "ProjectLucid.Editor.OriginalAssemblyCSharpStudioBehaviourTests.LaserLineUsesTheLocalEndPointAndTheBallTheWorldEndPoint",
    "ProjectLucid.Editor.OriginalAssemblyCSharpStudioBehaviourTests.WallInvokesItsEventOnlyWhenTheBallIsStrictlyCloserThanNineUnits",
    "ProjectLucid.Editor.OriginalAssemblyCSharpStudioBehaviourTests.PowerSourceStartsAnEightSecondWorldSpaceLineOnlyForThePlayer",
    "ProjectLucid.Editor.OriginalAssemblyCSharpStudioBehaviourTests.PlayerPositionShaderGlobalIsTheWorldPosition",
    "ProjectLucid.Editor.OriginalAssemblyCSharpStudioBehaviourTests.DissolveRefetchesTheMainCameraAfterItsCameraWasDestroyed",
)
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalHLLocalisationRemainingTypesTests.LiteralContentFormatsNestedParametersAndKeepsTextOnFormatErrors",
    "ProjectLucid.Editor.OriginalHLLocalisationRemainingTypesTests.TableContentIsStoredAsItsCrcStringId",
    "ProjectLucid.Editor.OriginalHLLocalisationRemainingTypesTests.ReverseChildrenReversesSiblingOrder",
    "ProjectLucid.Editor.OriginalHLLocalisationRemainingTypesTests.DeclarationsMatchTheShippedAssembly",
)

# Owned genuine StringTable fixture (identity-checked registry row, original Awake and setters); no Language object.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalHLLocalisationRemainingTypesBehaviourTests.TableContentToStringReturnsTheOwnedStringTableEntryForBothConstructors",
    "ProjectLucid.Editor.OriginalHLLocalisationRemainingTypesBehaviourTests.TableContentFormatsTheLookedUpTextWithNestedParameters",
    "ProjectLucid.Editor.OriginalHLLocalisationRemainingTypesBehaviourTests.TableFormatMismatchKeepsTheLookedUpText",
    "ProjectLucid.Editor.OriginalHLLocalisationRemainingTypesBehaviourTests.TableHasContentDependsOnTheIdNotTheLookup",
    "ProjectLucid.Editor.OriginalHLLocalisationRemainingTypesBehaviourTests.EvaluateLeavesChildrenWhenNoSupportedLanguageMatches",
    "ProjectLucid.Editor.OriginalHLLocalisationRemainingTypesBehaviourTests.EvaluateLeavesChildrenForALeftToRightLanguage",
    "ProjectLucid.Editor.OriginalHLLocalisationRemainingTypesBehaviourTests.EvaluateReversesChildrenForARightToLeftLanguage",
    "ProjectLucid.Editor.OriginalHLLocalisationRemainingTypesBehaviourTests.ReverseChildrenHandlesZeroOneThreeFourAndFiveChildren",
    "ProjectLucid.Editor.OriginalHLLocalisationRemainingTypesBehaviourTests.FallbackDataGettersReturnTheirSerializedBackingLists",
)
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalReverseOrderOnLanguagePlayTests.ActivationAwakeReversesRightToLeftChildrenOnce",
    "ProjectLucid.Tests.OriginalReverseOrderOnLanguagePlayTests.ActivationAwakeLeavesLeftToRightChildren",
)
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalClientDataDispatcherTests.PacketsReachTheirHandlerOrFallBackToUnhandled",
    "ProjectLucid.Editor.OriginalClientDataDispatcherTests.HandlerExceptionsGoToTheDispatchErrorHandler",
    "ProjectLucid.Editor.OriginalClientDataDispatcherTests.TriggersReportWhetherAHandlerRan",
    "ProjectLucid.Editor.OriginalClientDataDispatcherRoutingTests.PacketIdsEqualTheOriginalDispatchLiterals",
    "ProjectLucid.Editor.OriginalClientDataDispatcherRoutingTests.EachLiteralIdReachesOnlyItsHandlerWithTheDecodedPayload",
    "ProjectLucid.Editor.OriginalClientDataDispatcherRoutingTests.KnownIdsWithoutAHandlerReachUnhandledUndecoded",
    "ProjectLucid.Editor.OriginalClientDataDispatcherRoutingTests.EachTriggerInvokesOnlyItsHandlerWithTheSameReference",
    "ProjectLucid.Editor.OriginalClientDataDispatcherRoutingTests.DecodeAndHandlerFailuresReachTheErrorHandlerWithTheCallersBuffer",
    "ProjectLucid.Editor.OriginalClientDataDispatcherRoutingTests.UnhandledAndUnknownHandlersShareTheCallersRefBuffer",
)
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalHLPhysicsSingletonTests.SingletonMatchesTheShippedDeclaration",
    "ProjectLucid.Editor.OriginalHLPhysicsSingletonTests.SoleConstructorIsTheOriginalPublicParameterlessOne",
)
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalHLPhysicsLifecycleTests.EngineCreationRegistersOnAwakeAndDestructionReleasesIt",
    "ProjectLucid.Tests.OriginalHLPhysicsLifecycleTests.FailureAfterAwakeStillReleasesOwnedStateAndKeepsTheFirstFailure",
)

EDITMODE_TESTS += (
    "ProjectLucid.Editor.LanguageVerificationRegistryOwnershipTests.LanguageProofLeavesNoLanguageRegistryRow",
)
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalApplicationStateInitialiseCharacterTests.FaultAfterTheMissionWaitTerminatesTheIterator",
)

EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalTransitionWidgetBinderCorrectionsTests.BinderNodesHideFromTheComponentMenuWithAnEmptyString",
    "ProjectLucid.Editor.OriginalTransitionWidgetBinderCorrectionsTests.AbilityTriggeredArgsDefaultToTriggered",
    "ProjectLucid.Editor.OriginalTransitionWidgetBinderCorrectionsTests.SingleToggleSelectsTheIncomingButtonNotTheEqualChild",
)

EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalCharacterAbilityBodyOrderTests.AirAbilityEntryNudgeRunsBetweenTheVelocityTestAndTheForwardRead",
    "ProjectLucid.Editor.OriginalCharacterAbilityBodyOrderTests.HalfPipeEarlyAirSuppressionEndsOnlyTheAirActiveImpulse",
    "ProjectLucid.Editor.OriginalCharacterAbilityBodyOrderTests.TargetedLocationRaycastDividesByOneMagnitudeWithoutNormalizing",
)


# Project tests that ran without being required, including parameterized cases by exact fullname.
EDITMODE_TESTS += (
    'ProjectLucid.Tests.LocalPropertySaveTests.InvalidPrimaryRecoversBackupAndExplicitSavePreservesCorruption("version")',
    'ProjectLucid.Tests.LocalPropertySaveTests.InvalidPrimaryRecoversBackupAndExplicitSavePreservesCorruption("checksum")',
    'ProjectLucid.Tests.LocalPropertySaveTests.InvalidPrimaryRecoversBackupAndExplicitSavePreservesCorruption("missing-payload")',
    'ProjectLucid.Tests.LocalPropertySaveTests.TwoInvalidEnvelopesReportFinalErrorWithoutErasingEitherCopy("version")',
    'ProjectLucid.Tests.LocalPropertySaveTests.TwoInvalidEnvelopesReportFinalErrorWithoutErasingEitherCopy("checksum")',
    'ProjectLucid.Tests.LocalPropertySaveTests.TwoInvalidEnvelopesReportFinalErrorWithoutErasingEitherCopy("missing-payload")',
    "ProjectLucid.Tests.OriginalConfigurationAssetSerializationTests.SerializationFixtureLeavesAnUnrelatedDirtyAssetUnsaved(0)",
    "ProjectLucid.Tests.OriginalConfigurationAssetSerializationTests.SerializationFixtureLeavesAnUnrelatedDirtyAssetUnsaved(1)",
    "ProjectLucid.Tests.OriginalConfigurationAssetSerializationTests.SerializationFixtureLeavesAnUnrelatedDirtyAssetUnsaved(2)",
    "ProjectLucid.Tests.OriginalHLUnityCoreUtilityTests.AbsoluteKeepsSizeWhenEndpointsRoundEqual",
    "ProjectLucid.Tests.OriginalHLUnityCoreUtilityTests.AbsoluteSwapsReversedEndpoints",
    "ProjectLucid.Tests.OriginalHLUnityCoreUtilityTests.BestMatchSkipsIntMinValueScore",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.CollisionRejectionKeepsOwnedSentinelObserversAndActionSubscription",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.DisposalRejectsAReplacementOwnerWithoutRevokingItsReferences",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.EmptyBorrowedRowAndForeignStaticStateAreAlsoRejected",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.FailedAcquisitionDoesNotReleaseASeparateOwner(False)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.FailedAcquisitionDoesNotReleaseASeparateOwner(True)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.GuardPreservesBorrowedRowsReferencesAndCallbacks",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.StringTableColdGuardRejectsRetainedRowsAndForeignReferences(0)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.StringTableColdGuardRejectsRetainedRowsAndForeignReferences(1)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.StringTableColdGuardRejectsRetainedRowsAndForeignReferences(2)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.StringTableColdGuardRejectsRetainedRowsAndForeignReferences(3)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.StringTableColdGuardRejectsRetainedRowsAndForeignReferences(4)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.StringTableColdGuardRejectsRetainedRowsAndForeignReferences(5)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.StringTableGuardNeverCallsBorrowedReadiness(False)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.StringTableGuardNeverCallsBorrowedReadiness(True)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.SystemRefEntryGuardRejectsBorrowedLoggingStateWithoutCallingIt",
)

# PlayMode cases that ran without being required, by exact fullname, from the run at 78add7a.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.AudioMixerLifecycleTests.OriginalMixerKeysNormalizeAndResetTheSuppliedMixerAcrossFrames",
    "ProjectLucid.Tests.AudioSourceLifecycleTests.OriginalAudioSourceDelaysFadesAndUntrackedOneShotsUseTheRealHost",
    "ProjectLucid.Tests.BootSceneUnloadTests.OriginalBootStateUnloadsNamedScene",
    "ProjectLucid.Tests.BundledLanguageRequestTests.PortableAsyncLoaderReadsEverySuppliedLanguageBeforeCallback",
    "ProjectLucid.Tests.CharacterBrainLifecycleTests.OriginalBrainReenablesThroughItsCoroutineHostAndAppliesDeferredActions",
    "ProjectLucid.Tests.CloudFacadeTests.OriginalCloudFacade_OverlappingCallbacksAndShutdown",
    "ProjectLucid.Tests.CoroutineHostTests.OriginalCoroutineHost_StartStopAndRegistryLifecycle",
    "ProjectLucid.Tests.CoroutinePrerequisitesTests.OriginalCoroutineHelpers_NestedTwoFramesAndBothStopOverloads",
    "ProjectLucid.Tests.FSMComponentTests.OriginalFSMComponents_EmbeddedStartDispatchAndLifecycle",
    "ProjectLucid.Tests.InstancedPoolingLifecycleTests.OriginalInstancedPool_CloneDestroyAndManagerLifecycle",
    "ProjectLucid.Tests.LanguageLifecycleTests.OriginalLanguage_AutomaticSavedOverrideAndCallbackCleanup",
    "ProjectLucid.Tests.LevelManagerSystemsLifecycleTests.OriginalLevelManagedSystemsLifecycle",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.BodyFailureAndUnownedPendingWork_RetainFirstCauseAndCleanupFailure",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CoroutineHostNullRow_RejectsObserversWithoutInvokingOrConsuming(False)",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CoroutineHostNullRow_RejectsObserversWithoutInvokingOrConsuming(True)",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CreatedNullRow_OnlyItsOwnClearedRowIsRemovedOnSuccessOrFailure(False)",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CreatedNullRow_OnlyItsOwnClearedRowIsRemovedOnSuccessOrFailure(True)",
    'ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CreatedRow_GainingUnownedWorkIsRetainedWithoutInvokingIt(False,"startup")',
    'ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CreatedRow_GainingUnownedWorkIsRetainedWithoutInvokingIt(False,"shutdown")',
    'ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CreatedRow_GainingUnownedWorkIsRetainedWithoutInvokingIt(False,"deferred")',
    'ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CreatedRow_GainingUnownedWorkIsRetainedWithoutInvokingIt(True,"startup")',
    'ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CreatedRow_GainingUnownedWorkIsRetainedWithoutInvokingIt(True,"shutdown")',
    'ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CreatedRow_GainingUnownedWorkIsRetainedWithoutInvokingIt(True,"deferred")',
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CreatedUntypedNullRow_CanBeReleasedBeforeTypedAcquisition",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.MultiplePreDisposeCleanupFailures_RetainActualObjectsAndContinueRestoration",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.PreDisposeCleanupAndDiagnosticFault_ContinueLaterOwnedStepsAndRetainFirstCause(0,False)",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.PreDisposeCleanupAndDiagnosticFault_ContinueLaterOwnedStepsAndRetainFirstCause(1,False)",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.PreDisposeCleanupAndDiagnosticFault_ContinueLaterOwnedStepsAndRetainFirstCause(2,False)",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.PreDisposeCleanupAndDiagnosticFault_ContinueLaterOwnedStepsAndRetainFirstCause(0,True)",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.PreDisposeCleanupAndDiagnosticFault_ContinueLaterOwnedStepsAndRetainFirstCause(1,True)",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.PreDisposeCleanupAndDiagnosticFault_ContinueLaterOwnedStepsAndRetainFirstCause(2,True)",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.RawKeyWithNullRow_RejectsBeforeReadingItsReferences",
    "ProjectLucid.Tests.LoggingHostTests.OriginalLoggingHost_RegisteredCallbacksAndRetainedNativeBridgeLifecycle",
    "ProjectLucid.Tests.PrefabPoolingLifecycleTests.OriginalPrefabPool_AutomaticLateUpdateAndDestroy",
    "ProjectLucid.Tests.SplashScreenLifecycleTests.OriginalSplashTracksTimelineFramesAndPublishesItsStopCallback",
    "ProjectLucid.Tests.StringTableLifecycleTests.OriginalStringTable_AutomaticLoadingDeferredHandlersAndCleanup",
    "ProjectLucid.Tests.SurfaceEventLifecycleTests.AuthoredStayListenersRunOnFixedFramesAndReentryKeepsEarlierRoutine",
    "ProjectLucid.Tests.UICollectionsLifecycleTests.OriginalDebugFreeLinkedStackManagersKeepOwnedCloneAndCleanupQuirks",
    "ProjectLucid.Tests.UIManagerFamilyLifecycleTests.OriginalSingularAndTransitionManagersRetainDeferredDestroyAndIdentifierRules",
    "ProjectLucid.Tests.UIStartupLifecycleTests.OriginalUIHostAndVisibilityMembersRetainOwnedRegistryAndCoroutineRules",
    "ProjectLucid.Tests.UIWidgetProgressionTests.OriginalAnimationRoutesUseConfiguredClipNamesAndSameLayerStop",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalEntityActivationUpdateTests.UpdateRunsTheInactivePassFirstAndBothListsInReverse",
)

# Positive controls for the managed StringTable fixture guards.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.StringTableGuardAcceptsOnlyTheOriginalReadinessInitializer",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.StringTableColdGuardAcceptsEmptyStateAndUnrelatedRows",
)

# Borrowed request subscribers are rejected before the bundled-language fixture mutates.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.BundledLanguagesRejectABorrowedStringRequestWithoutCallingIt",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.BundledLanguagesRejectAThrowingBorrowedStringRequestWithoutCallingIt",
)

# Native-literal mixer oracle; the missing-parameter branch runs on an owned copy.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.AudioMixerLifecycleTests.OriginalGetMusicVolumeReturnsUnityGainWhenTheParameterIsMissing",
)

# Coverage gaps: application enter order, localisation fallback/formatting,
# splash selection, definition lookup and font-readiness storage reads.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalApplicationEnterOrderTests.LoadingEnterRegistersSystemsInShippedOrderWithOnlyGameCenterGated",
    "ProjectLucid.Tests.OriginalApplicationEnterOrderTests.BootEnterKeepsDisplayStoreCacheThenLoadBeforeLoadedLevelsRead",
    "OriginalLocalisedUIStringLookupFallbackTests.FailedLookupWritesMissingMarkerOnlyForNonNoneIdentifierAndNullOverride",
    "OriginalLanguageUtilsFormatterTests.InitialiserPublishesFormatterAndCachesBeforeItsDecimalSeparatorFault",
    "OriginalLanguageUtilsFormatterTests.AbbreviationFloorsThenTrimsTrailingZerosIgnoringZeroDecimalFlag",
    "OriginalLanguageUtilsFormatterTests.InitialiserMapsEachIdentifierToItsShippedDestination",
    "ProjectLucid.Tests.OriginalSplashSelectionTests.SelectionFollowsSignedWidthDifferenceSequenceNotNearestWidth",
    "ProjectLucid.Tests.OriginalSplashSelectionTests.UnmatchedAspectFallsBackToTheFirstEntry",
    "ProjectLucid.Tests.OriginalSplashSelectionTests.FractionalWidthDifferencesFollowTheSelectionTolerance",
    "ProjectLucid.Tests.OriginalSplashSelectionTests.AspectRatioMatchesWithinToleranceButNotJustOutside",
    "ProjectLucid.Tests.OriginalDataDefinitionsLookupTests.GetMergesLaterGroupsIntoFirstAndFaultsOnDuplicateKeys",
    "ProjectLucid.Tests.OriginalDataDefinitionsLookupTests.GetKeepsTheFirstGroupDictionaryWithItsKeyComparer",
    "ProjectLucid.Tests.OriginalDataDefinitionsLookupTests.GetGroupReturnsFirstClrMatchIncludingDestroyedGroup",
    "ProjectLucid.Tests.OriginalFontReadinessUpdateTests.UpdateReadsAndStoresBothKeysEagerlyFromTheRegisteredApp",
)

# Original cloud facade property-store handler lifecycle and save modification.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.CloudFacadeHandlerTests.ShutdownRemovesSaveHandlerAndLeavesAfterConflictsHandlerSubscribed",
    "ProjectLucid.Tests.CloudFacadeHandlerTests.ModifySaveMutatesOnlySubscribedChangedPropertiesAndConsumesChanges",
)

# Original rolling ability leave reset on a really constructed ability.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.CharacterAbilityRollingTests.LeaveClearsTriggeredAndExitingFlags",
)

# Owned App-row cleanup failure controls; no original-behavior claim.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.CharacterAbilityRollingTests.OwnedFailureAfterAppReferenceCreationReleasesTheEmptyRowAndKeepsThePrimary",
    "ProjectLucid.Tests.CharacterAbilityRollingTests.ObservedAppReferenceIsRetainedAndReportedWithoutReplacingThePrimary",
)

# Original action-subscription release timing on a real coroutine host.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.InputActionSubscriptionTests.UnheldReleaseDefersOnUpToTheNextFrameAfterClearingDown",
)

# Original cursor input-type handling with a genuine EventSystem and HLInputModule.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.ToggleCursorVisibilityInputTypeTests.InputTypeChoosesCursorUnlessForceReleased",
)

# Isolated failure-path typed-reference capture controls for the lifecycle lease.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CaptureIfCreated_ReleasesAnUncapturedConstructorEntryAfterBodyFailureWithoutCreatingOne",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CaptureIfCreated_UncapturedRowAndAbsentEntryAreNoOpsAndCreateNothing",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CaptureIfCreated_AlreadyCapturedEntryIsANoOp",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CaptureIfCreated_ReplacedRowIsRefusedWithoutDeletionOrInvocationAndLaterCleanupContinues",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CaptureIfCreated_PendingForeignObserverIsNeitherInvokedNorDeleted",
)
