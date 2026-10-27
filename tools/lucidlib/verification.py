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

from .bootstrap import release_work_lock, take_work_lock, managed_path, run_owned, write_json

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
    "OriginalBootSceneUnloadRetainsFactoryAndStateMetadata",
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
    "RecoveredYamlMissingScriptsLeaveTheReferenceAuditIncomplete",
    "RecoveredYamlObjectPointersRequireExactLocalOrLoadedTargets",
    "RecoveredYamlLocalPointerSyntaxSkipsStringTextAndRequiresUniqueAnchors",
    "SuppliedLayoutComponentsRetainAuthoredScalarsAfterScriptBinding",
    "UpstreamLayoutSizingPreservesPriorityFitAndAspectRules",
    "OriginalPipelineMarkerRetainsEngineFieldsAfterScriptBinding",
    "TimeConversionsRetainOriginalUnitsRoundingAndDateKinds",
    "SkyCubemapGpuOutputMatchesDecodedMetalCases",
    "EggmanLogoGpuOutputMatchesDecodedColorBlendAndLayerCases",
    "ParticleMainCompilesEveryPassForDirect3D",
    "ParticleMainGpuOutputMatchesDecodedSurfaceCases",
    "ParticleAnimMeshCompilesEveryPassForDirect3D",
    "ParticleAnimMeshGpuOutputMatchesDecodedSurfaceCases",
    "RecoveredShaderRigRestoresStateAfterDisposeAndFailedConstruction",
    "PfxAlphaBlendAddCompilesEveryPassForDirect3D",
    "PfxAlphaBlendAddGpuOutputMatchesDecodedSurfaceCases",
    "CharGhostedCompilesEveryPassForDirect3D",
    "CharGhostedGpuOutputMatchesDecodedSurfaceCases",
    "GameplayCollectableCompilesEveryPassForDirect3D",
    "GameplayCollectableGpuOutputMatchesDecodedSurfaceCases",
    "MatcapCompilesEveryPassForDirect3D",
    "MatcapGpuOutputMatchesDecodedSurfaceCases",
    "ParticleTrailCompilesEveryPassForDirect3D",
    "ParticleTrailGpuOutputMatchesDecodedSurfaceCases",
    "FullscreenEffectsCompilesEveryPassForDirect3D",
    "FullscreenEffectsGpuOutputMatchesDecodedSurfaceCases",
    "UiBlurCompilesEveryPassForDirect3D",
    "UiBlurGpuOutputMatchesDecodedSurfaceCases"))
# Particle depth and camera-fade portability across desktop graphics APIs (#448).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.RecoveredBehaviorTests.ParticleMainCompilesEveryPassForOpenGL",
    "ProjectLucid.Tests.PreparedMaterialShaderReferenceTests.PreparedParticleMainMaterialResolvesItsOriginalShader",
    "ProjectLucid.Tests.ParticleMainStageContractTests.ParticleMainVertexStagesKeepDecodedOutputs",
    "ProjectLucid.Tests.ParticleMainStageContractTests.ParticleMainDepthFragmentKeepsFourFloatLanes",
    "ProjectLucid.Tests.ParticleMainStageContractTests.ParticleMainRetainsDecodedFoveatedDynamicKeyword",
    "ProjectLucid.Tests.ParticleMainStageContractTests.ParticleMainKeywordScopesAndPassMasksMatchOriginal",
)
EDITMODE_TESTS += tuple("ProjectLucid.Tests.RecoveredBehaviorTests." + name for name in (
    "ParticleAnimMeshCompilesEveryPassForOpenGLCore",
    "ParticleAnimMeshRestoresDepthTextureWhenVerificationFails"))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ParticleAnimMeshStageContractTests." + name for name in (
    "ParticleAnimMeshKeywordScopesAndPassMasksMatchOriginal",
    "ParticleAnimMeshVertexStagesKeepDecodedOutputs",
    "ParticleAnimMeshFragmentStagesKeepDecodedFloatOutputs"))
EDITMODE_TESTS += ("ProjectLucid.Tests.PreparedMaterialShaderReferenceTests.PreparedParticleAnimMeshMaterialResolvesItsOriginalShader",)
# Environment shader compilation and surface output (#448).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.RecoveredBehaviorTests.EnvStandardLitCompilesEveryPassForDirect3D",
    "ProjectLucid.Tests.RecoveredBehaviorTests.EnvStandardLitGpuOutputMatchesDecodedSurfaceCases",
)
# Original environment shader stages, graph math, declarations and prepared binding (#448).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.EnvStandardLitContractTests.EnvStandardLitKeywordScopesAndPassMasksMatchOriginal",
    "ProjectLucid.Tests.EnvStandardLitContractTests.EnvStandardLitCompilesEveryPassForOpenGLCore",
    "ProjectLucid.Tests.EnvStandardLitContractTests.EnvStandardLitVertexStagesKeepDecodedOutputs",
    "ProjectLucid.Tests.EnvStandardLitContractTests.EnvStandardLitDepthFragmentKeepsFourFloatLanes",
    "ProjectLucid.Tests.EnvStandardLitContractTests.EnvStandardLitFresnelUsesMappedNormalsAndGraphViewNormalization",
    "ProjectLucid.Tests.EnvStandardLitContractTests.EnvStandardLitDebugInputsKeepDeclaredLightmapAndSHFields",
    "ProjectLucid.Tests.PreparedMaterialShaderReferenceTests.PreparedEnvStandardLitMaterialResolvesItsOriginalShader",
)

# Original forward alpha and actual MSAA coverage (#523).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.EnvStandardLitAlphaTests.ConstantAlphaTestOutputMatchesOriginalWithAndWithoutCoverageAvailability",
    "ProjectLucid.Tests.EnvStandardLitAlphaTests.InteriorAlphaGradientKeepsOriginalSharpeningAndRejection",
    "ProjectLucid.Tests.EnvStandardLitAlphaTests.EnabledMsaaCoverageUsesOriginalCutoffZeroAlpha",
)
# Character shader compilation and surface output (#448).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.RecoveredBehaviorTests.CharStandardCompilesEveryPassForDirect3D",
    "ProjectLucid.Tests.RecoveredBehaviorTests.CharStandardGpuOutputMatchesDecodedSurfaceCases",
)
# Environment, hub, sky, gameplay and boss shader compilation and surface output (#448).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.RecoveredBehaviorTests." + name for name in (
    "EnvZone2GooCompilesEveryPassForDirect3D",
    "EnvZone2GooGpuOutputMatchesDecodedSurfaceCases",
    "EnvStandardTriPlanarCompilesEveryPassForDirect3D",
    "EnvStandardTriPlanarGpuOutputMatchesDecodedSurfaceCases",
    "EnvZone5TrackCompilesEveryPassForDirect3D",
    "EnvZone5TrackGpuOutputMatchesDecodedSurfaceCases",
    "EnvStandardUnlitCompilesEveryPassForDirect3D",
    "EnvStandardUnlitGpuOutputMatchesDecodedSurfaceCases",
    "HubStandardUnlitCompilesEveryPassForDirect3D",
    "HubStandardUnlitGpuOutputMatchesDecodedSurfaceCases",
    "SkyboxPortalCompilesEveryPassForDirect3D",
    "SkyboxPortalGpuOutputMatchesDecodedSurfaceCases",
    "GameplayTimedPlatformCompilesEveryPassForDirect3D",
    "GameplayTimedPlatformGpuOutputMatchesDecodedSurfaceCases",
    "CharBoss03BodyBlobsCompilesEveryPassForDirect3D",
    "CharBoss03BodyBlobsGpuOutputMatchesDecodedSurfaceCases"))
# Batch-two shader portability to the Linux player (#448).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.RecoveredBehaviorTests." + name for name in (
    "EnvZone2GooCompilesEveryPassForOpenGL",
    "EnvStandardTriPlanarCompilesEveryPassForOpenGL",
    "EnvZone5TrackCompilesEveryPassForOpenGL",
    "EnvStandardUnlitCompilesEveryPassForOpenGL",
    "HubStandardUnlitCompilesEveryPassForOpenGL",
    "SkyboxPortalCompilesEveryPassForOpenGL",
    "GameplayTimedPlatformCompilesEveryPassForOpenGL",
    "CharBoss03BodyBlobsCompilesEveryPassForOpenGL",
))
# Batch-two original keyword tables, stage types and decoded unlit vertex payloads (#448).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.RecoveredShaderBatchTwoContractTests." + name for name in (
    "EnvZone2GooKeywordScopesAndPassMasksMatchOriginal",
    "EnvStandardTriPlanarKeywordScopesAndPassMasksMatchOriginal",
    "EnvZone5TrackKeywordScopesAndPassMasksMatchOriginal",
    "EnvStandardUnlitKeywordScopesAndPassMasksMatchOriginal",
    "HubStandardUnlitKeywordScopesAndPassMasksMatchOriginal",
    "SkyboxPortalKeywordScopesAndPassMasksMatchOriginal",
    "GameplayTimedPlatformKeywordScopesAndPassMasksMatchOriginal",
    "CharBoss03BodyBlobsKeywordScopesAndPassMasksMatchOriginal",
    "EnvStandardUnlitVaryingsKeepDecodedVertexOutputs",
    "HubStandardUnlitVaryingsKeepDecodedVertexOutputs",
    "SkyboxPortalVaryingsKeepDecodedVertexOutputs",
    "GameplayTimedPlatformVaryingsKeepDecodedVertexOutputs",
    "CharBoss03BodyBlobsVaryingsKeepDecodedVertexOutputs",
))
EDITMODE_TESTS += ("ProjectLucid.Tests.ArtifactIdentityTests.SharedFingerprintIncludesAllPreparedAssetDirectories",)

# Preserve literal host filename characters in content identity (#509).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.ArtifactIdentityTests.SharedFingerprintPreservesHostFilenameCharacters",
)

EDITMODE_TESTS += tuple("ProjectLucid.Tests.RecoveredBehaviorTests." + name for name in (
    "PfxAlphaBlendAddCompilesEveryPassForOpenGL",
    "PfxAlphaBlendAddPerspectiveFadeRetainsOriginalClipCurve",
    "ParticleTrailCompilesEveryPassForOpenGL",
    "ParticleTrailOrthographicDepthRetainsBothRowsAndProjectionSigns",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.PreparedMaterialShaderReferenceTests." + name for name in (
    "PreparedPfxAlphaBlendAddMaterialResolvesItsOriginalShader",
    "PreparedGhostedMaterialResolvesItsOriginalShader",
    "PreparedCollectableMaterialResolvesItsOriginalShader",
    "PreparedMatcapMaterialResolvesItsOriginalShader",
    "PreparedTrailMaterialResolvesItsOriginalShader",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.RecoveredShaderStageContractTests." + name for name in (
    "CollectableRetainsDepthAndShadowVertexPayloads",
    "MatcapRetainsBothUvStreamsAndShadowNormal",
    "TrailRetainsEveryPassVertexPayload",
    "GhostedVertexNormalizesBothProjectionViewVectors",
    "CollectableDepthFragmentRetainsFourFloatChannels",
    "MatcapDepthFragmentRetainsFourFloatChannels",
    "TrailDepthFragmentRetainsFourFloatChannels",
    "ShadersRetainOriginalKeywordScopesAndFloatOutputTypes",
))
EDITMODE_TESTS += ("ProjectLucid.Tests.DebugBackingFieldAttributeTests.DebugOnlyAllowanceForCompilerDebuggerBrowsableIsExactAndReleaseStaysStrict",)

# Same-GUID maintained ShaderLab references in prepared original Materials (#492).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.PreparedMaterialShaderReferenceTests.PreparedSkyMaterialResolvesItsOriginalShader",
    "ProjectLucid.Tests.PreparedMaterialShaderReferenceTests.PreparedEggmanMaterialResolvesItsOriginalShader",
    "ProjectLucid.Tests.PreparedMaterialShaderReferenceTests.PreparedBundledFullscreenMaterialResolvesItsOriginalShader",
    "ProjectLucid.Tests.PreparedMaterialShaderReferenceTests.PreparedResourcesFullscreenMaterialResolvesItsOriginalShader",
)

# Shipped fullscreen vertex payload, keyword masks and fragment types (#448).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.FullscreenShaderStageContractTests." + name for name in (
    "FullscreenVertexEntriesKeepDecodedViewDirections",
    "FullscreenAndBlurKeepDecodedKeywordMasksAndScopes",
    "FullscreenAndBlurKeepFloat4FragmentContracts",
))

# Build-target-qualified imported keyword expectations (#524).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ImportedKeywordContextTests." + name for name in (
    "OnlyTheCoreRpPredicateTargetsDeclareFoveated",
    "ImportedMaskDropsOnlyFoveatedWhereItCannotBeDeclared",
    "KeywordMismatchesAreRejectedInEitherContext",
))

# Exact loaded HideIf boxed String null evidence (#550).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.LoadedHideIfAttributeBlobTests." + name for name in (
    "TransformRotatorHideIfRetainsItsOriginalRawStringNull",
    "InventoryPublishesHideIfRawEvidenceWithoutChangingAttributeOrder",
    "HideIfReaderRequiresTheMembersOwnUniqueRowAndExactOverload",
    "HideIfBlobRejectsOtherNullTypesEmptyValuesAndMalformedEncoding",
    "LoadedModuleEvidenceRejectsMissingPathsAndMismatchedIdentity",
    "SameNamedAttributeFromAnotherAssemblyIsRejected",
))

# Original FastAction null conversion and checked mutation order (#469).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalFastActionNullListenerTests." + name for name in (
    "NullListenerReturnsTheSameInstanceForTheSelfType",
    "NullListenerOnASiblingSubclassReturnsNullWithoutThrowing",
    "NullListenerOnNullReturnsNull",
    "NonNullListenerOnASiblingStillThrowsAfterAdding",
    "SubtractionKeepsItsOwnResults",
))

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalEncryptedSavePreservationTests.OriginalEncryptedSaveRetainsIndependentCbcZeroPaddingVectors",
    "ProjectLucid.Tests.OriginalEncryptedSavePreservationTests.OriginalEncryptedSaveRetainsUtf8ConstructorBoundaries",
    "ProjectLucid.Tests.OriginalEncryptedSavePreservationTests.OriginalEncryptedSaveRetainsDecryptCatchBoundaries",
    "ProjectLucid.Tests.OriginalEncryptedSavePreservationTests.OriginalPropertyStoreDefaultConstructorSelectsOfflineStorage",
    "ProjectLucid.Tests.OriginalCloudBehaviorTests.OriginalEditorCloudRetainsConstantGettersAndDiscardedWrites",
    "ProjectLucid.Tests.OriginalCloudBehaviorTests.OriginalEditorCloudRetainsLiveKeysAndStaleCaptures",
    "ProjectLucid.Tests.OriginalCloudBehaviorTests.OriginalCloudNotificationsUseRealUserInfoJsonAndReceiverOrder",
    "ProjectLucid.Tests.OriginalCloudBehaviorTests.OriginalCloudConnectCallbacksRetainDelegateCombineRemoveOrder",
    "ProjectLucid.Tests.OriginalCloudBehaviorTests.CloudFactorySelectsOfflinePortWithoutConstructingAppleProvider",
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
    "ProjectLucid.Tests.EditMode.AppleInputMetadataPreservationTests.SourceApplePluginControllerProviderGettersKeepTheirOriginalSlotFlags",
    "ProjectLucid.Tests.EditMode.AppRatingBehaviorTests.UnsupportedProviderRemainsSelectedAndSilent",
    "ProjectLucid.Tests.EditMode.AppRatingNativeBindingTests.MacOSNativeBindingDeclarationRemainsPreserved",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.FailedCompletionDetachesOwnedEvaluatorAndPreservesThePrimaryError",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.FailedCompletionRetainsBothPrimaryAndRollbackErrorsWhileRevokingReadiness",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.RealLocalCompletionSurvivesDisposalAndAStoreReloadWithoutAnOpenGameplaySlot",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.MalformedOwnedCompletionValueFailsBeforePublishingEntriesOrReadiness",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.BelowTargetAndTrackingDisallowedEvaluationsWriteNoLocalCompletion",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.MalformedSecondCompletionValueFailsBeforePublishingTheValidFirstEntry",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.OwnedDisposalPreservesASeparatelyNamedRegistrationOfTheSameManager",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.OwnedCompletionFaultPreservesTheSameManagerAliasAndThePrimaryError",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.GenuineLeaderboardStartupFaultPreservesItsSameObjectAliasAndPrimaryError",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.GenuineLeaderboardSubscribedShutdownPreservesItsSameObjectNamedAlias",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.GenuineLeaderboardManagerShowUIDoesNothingOffline",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.GenuineChallengeLeaderboardIsUnavailableOfflineThroughTheOriginalLoginQuery",
)

# Persistent offline achievement reset and retained evaluators (#501).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.OfflineResetClearsAllAchievementProgressAndPersistsWithoutChangingOtherProperties",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.OfflineResetAllowsHydratedCompletedAchievementsToEarnAgainThroughTheirOwnedEvaluator",
    "ProjectLucid.Tests.EditMode.OfflineAchievementLifecycleTests.OfflineResetReportsPrimaryWriteFailureWithoutPublishingClearedEntriesAndCanRetry",
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

# Strict authored Image binding and owned cleanup boundaries (#547).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.UguiImageBindingTests.AllAuthoredImagesRetainValuesAndAssetIdentityAcrossRoundtrip",
    "ProjectLucid.Tests.UguiImageBindingTests.BuiltinSpriteAndMaterialReferencesResolveExactInstalledObjects",
    "ProjectLucid.Tests.UguiImageBindingTests.OriginalCullEventAncestryRetainsCompleteOrderedDeclarations",
    "ProjectLucid.Tests.UguiImageBindingTests.KnownEventAttributeOrdersRejectUnknownPermutationsAndValues",
    "ProjectLucid.Tests.UguiImageBindingTests.UnlistedEventFieldsStillRequireExactOriginalAttributeOrder",
    'ProjectLucid.Tests.UguiImageBindingTests.NonemptyBorrowedCollectionSnapshotDetectsMutationWithoutRepair("row")',
    'ProjectLucid.Tests.UguiImageBindingTests.NonemptyBorrowedCollectionSnapshotDetectsMutationWithoutRepair("count")',
    'ProjectLucid.Tests.UguiImageBindingTests.NonemptyBorrowedCollectionSnapshotDetectsMutationWithoutRepair("nested")',
    'ProjectLucid.Tests.UguiImageBindingTests.OwnedImageCleanupAttemptsEveryReleaseAndPreservesFirstFailure("folder",False)',
    'ProjectLucid.Tests.UguiImageBindingTests.OwnedImageCleanupAttemptsEveryReleaseAndPreservesFirstFailure("folder",True)',
    'ProjectLucid.Tests.UguiImageBindingTests.OwnedImageCleanupAttemptsEveryReleaseAndPreservesFirstFailure("setup",False)',
    'ProjectLucid.Tests.UguiImageBindingTests.OwnedImageCleanupAttemptsEveryReleaseAndPreservesFirstFailure("setup",True)',
    'ProjectLucid.Tests.UguiImageBindingTests.OwnedImageCleanupAttemptsEveryReleaseAndPreservesFirstFailure("body",False)',
    'ProjectLucid.Tests.UguiImageBindingTests.OwnedImageCleanupAttemptsEveryReleaseAndPreservesFirstFailure("body",True)',
    'ProjectLucid.Tests.UguiImageBindingTests.OwnedImageCleanupAttemptsEveryReleaseAndPreservesFirstFailure("none",False)',
    'ProjectLucid.Tests.UguiImageBindingTests.OwnedImageCleanupAttemptsEveryReleaseAndPreservesFirstFailure("none",True)',
)

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


# Fixture ownership: borrowed tangent scratch and actual dictionary slot reuse (#521).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.TangentScratchOwnershipTests." + name for name in (
    'FixtureDetachesScratchBeforeItsFirstComponent("generator",3)',
    'FixtureDetachesScratchBeforeItsFirstComponent("generator",4)',
    'FixtureDetachesScratchBeforeItsFirstComponent("bender",3)',
    'FixtureDetachesScratchBeforeItsFirstComponent("bender",4)',
    'FixtureReturnsBorrowedScratchAndCleansEveryOwnedResource("generator","none",3)',
    'FixtureReturnsBorrowedScratchAndCleansEveryOwnedResource("generator","body",3)',
    'FixtureReturnsBorrowedScratchAndCleansEveryOwnedResource("generator","setup",3)',
    'FixtureReturnsBorrowedScratchAndCleansEveryOwnedResource("generator","cleanup",3)',
    'FixtureReturnsBorrowedScratchAndCleansEveryOwnedResource("generator","none",4)',
    'FixtureReturnsBorrowedScratchAndCleansEveryOwnedResource("bender","none",3)',
    'FixtureReturnsBorrowedScratchAndCleansEveryOwnedResource("bender","body",3)',
    'FixtureReturnsBorrowedScratchAndCleansEveryOwnedResource("bender","setup",3)',
    'FixtureReturnsBorrowedScratchAndCleansEveryOwnedResource("bender","cleanup",3)',
    'FixtureReturnsBorrowedScratchAndCleansEveryOwnedResource("bender","none",4)',
    'FollowingTriangleMatchesUninterruptedOriginalAccumulation("generator")',
    'FollowingTriangleMatchesUninterruptedOriginalAccumulation("bender")',
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.HLHapticsRegistryOwnershipTests." + name for name in (
    'ExactOwnedRowMayOccupyAnySlotWhileForeignOrderIsPreserved(0)',
    'ExactOwnedRowMayOccupyAnySlotWhileForeignOrderIsPreserved(1)',
    'ExactOwnedRowMayOccupyAnySlotWhileForeignOrderIsPreserved(2)',
    'ComparatorRejectsForeignMutationOrUnprovenOwnedRow("key-identity")',
    'ComparatorRejectsForeignMutationOrUnprovenOwnedRow("value-identity")',
    'ComparatorRejectsForeignMutationOrUnprovenOwnedRow("order")',
    'ComparatorRejectsForeignMutationOrUnprovenOwnedRow("missing-foreign")',
    'ComparatorRejectsForeignMutationOrUnprovenOwnedRow("extra-foreign")',
    'ComparatorRejectsForeignMutationOrUnprovenOwnedRow("wrong-owned-key")',
    'ComparatorRejectsForeignMutationOrUnprovenOwnedRow("wrong-owned-value")',
    'ComparatorRejectsForeignMutationOrUnprovenOwnedRow("missing-owned")',
    'ExistingTargetRemainsForeignAndCannotBeReplaced',
))
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.HLHapticsRegistrySlotTests.GenuineManagerReusesAnOwnedEarlierSlotWithoutChangingForeignState",
)

# Fixture guard self-check for the live cloud facade proof; no product oracle.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.CloudFacadeTests.FixtureGuardRejectsBorrowedStoreStateWithoutInvocation",
)

# Medium coverage gaps in UI managers, buttons, highlighters, scroll geometry and cameras.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalUIGeometryGapTests." + name for name in (
    "NavigationEqualityTreatsDestroyedSelectablesAsUnityNull",
    "FitTrimmedImageWritesSquareSizeFromBoundsHeightAndReferencePixels",
    "VisibleAlignmentTargetsItemsBeyondEitherViewportEdge",
    "NearestChildChoosesMaxBookendForEqualAndUnorderedDistances",
))

PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalUIContainerManagerGapTests.SingularCloseAllExceptFaultKeepsFieldWhileTransitionCloseClearsIt",
    "ProjectLucid.Tests.OriginalUIContainerManagerGapTests.DebugAndFreeReturnExternallyDestroyedCachedContainer",
    "ProjectLucid.Tests.OriginalUIInputVisibilityGapTests.DisabledControllerStillSubscribesWhenUIManagerBecomesValidLater",
    "ProjectLucid.Tests.OriginalHLUIEffectManagerGapTests.AwakeLogsBeforeRegistrationAndKeepsFirstCuePerKeyIncludingNullClip",
    "ProjectLucid.Tests.OriginalStackedCameraGapTests.EnableLoadsAndDisableUnloadsThroughRegisteredManager",
    "ProjectLucid.Tests.OriginalStackedCameraGapTests.MissingManagerSkipsUnloadAndLateRegistrationStillLoadsDisabledCamera",
    "ProjectLucid.Tests.OriginalHLButtonGapTests.ButtonDownAndUpReadSoundActionAndIdAfterTheirEvent",
    "ProjectLucid.Tests.OriginalHLButtonGapTests.DisableInvokesDisabledSelectedOnlyWhenHighlightedAfterBaseWithoutNullGuard",
    "ProjectLucid.Tests.OriginalHLButtonGapTests.PointerUpExitAndEnterDispatchFromStateCapturedBeforeBase",
    "ProjectLucid.Tests.OriginalHLButtonHighlighterGapTests.TargetsForwardEachCallbackInOrderAndRereadTheArray",
    "ProjectLucid.Tests.OriginalHLButtonHighlighterGapTests.ActiveSetButtonStateStopsStoredCoroutineBeforeApplyingSynchronously",
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


def _read_test_xml(path):
    path = Path(path)
    if not path.is_file() or path.stat().st_size > 8 * 1024 * 1024:
        raise ValueError("Unity test XML is absent or exceeds the supported size")
    data = path.read_bytes()
    if b"<!DOCTYPE" in data.upper() or b"<!ENTITY" in data.upper():
        raise ValueError("Unsupported declarations in Unity test XML")
    try:
        return data, ET.fromstring(data)
    except ET.ParseError as error:
        raise ValueError("Invalid Unity test XML: " + str(error)) from error


def _project_cases(root):
    """Fullnames of the test cases inside ProjectLucid.* test assembly suites."""
    return {case.get("fullname") or "<unnamed>" for suite in root.iter("test-suite")
            if suite.get("type") == "Assembly" and
            re.split(r"[\\/]", suite.get("name") or "")[-1].startswith("ProjectLucid.")
            for case in suite.iter("test-case")}


def test_verdict(path, required):
    data, root = _read_test_xml(path)
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
    unregistered = sorted(_project_cases(root) - required_names)
    if unregistered:
        errors.append("Unregistered project tests: " + ", ".join(unregistered))
    success = (root.tag == "test-run" and root.get("result") == "Passed" and root.get("failed") == "0"
               and not missing and not errors)
    return {"status": "complete" if success else "incomplete", "result": root.get("result"),
            "passed": len(passed), "missing_required_tests": missing,
            "conflicting_required_tests": conflicting, "errors": errors,
            "xml_sha256": hashlib.sha256(data).hexdigest()}


def _first_use_row_changes(row_name):
    """The exact change list of a production first use that appends a null row."""
    row = "added system row '" + row_name + "'"
    typed = row + " typed reference '" + row_name + "'"
    return ("changed system row order", row, row + " untyped reference",
            row + " untyped reference OnSystemStartup observers",
            row + " untyped reference OnSystemShutdown observers",
            row + " untyped reference deferred InvokeOnValid work",
            typed, typed + " OnSystemStartup observers", typed + " OnSystemShutdown observers",
            typed + " deferred InvokeOnValid work", row + " typed reference names")


# Shared-state leaks a run may carry while a fixture is being fixed: exact test
# fullname -> the reason, naming the issue that tracks the fix, and the exact,
# ordered change list its leak record must carry. Only status "leak" can be
# listed, and a record whose changes differ in any line fails the run, so an
# entry never hides a further leak. A listed test that runs clean fails the run
# until its entry is removed. Every entry is temporary and needs review;
# permanent production side effects belong in ISOLATION_SIDE_EFFECTS instead.
ISOLATION_ALLOWLIST = {}

# Permanent side effects of original code that no fixture can undo: name ->
# the reason with its native evidence, the exact ordered change list, and, per
# mode the entry applies to, the fixtures any of whose cases may be the first
# in the run to cause it. A leak line is accepted as a side effect only when
# its changes are exactly these, its test is a case of a listed fixture for
# that mode, and no other line in the report already matched the entry. A side
# effect may simply not occur in a given ordering (an earlier case caused it,
# or the row already existed), so a run without it is still clean. Every entry
# needs review.
ISOLATION_SIDE_EFFECTS = {
    # TimeScaledUtilities' BeforeFieldInit initializer 0x06000ecc stores
    # GetSystemRef<TimeManager>(null, true) in its readonly s_timeManagerRef,
    # which auto-registers (0x06000e3f) the null 'Hardlight.TimeManager' row,
    # appended last. Removing the row would make every later registration build
    # a new row the readonly reference never sees. Native run19
    # (run19-928e689 editmode-isolation.jsonl line 773) recorded these changes,
    # without the SystemConfiguration row the fixture now releases, for
    # OriginalGameTimingRetainsImmediateAndDeferredFaultPrefixes, the first case
    # in run order to run TimeScaledUtilities code. Only these two fixtures run
    # it; OriginalCoreLowGapTests reads the iterator by reflection only.
    "TimeScaledUtilities..cctor": {
        "reason": "TimeScaledUtilities' type initializer 0x06000ecc appends the permanent 'Hardlight.TimeManager' row "
                  "that holds its readonly s_timeManagerRef; removing it would detach that reference",
        "changes": _first_use_row_changes("Hardlight.TimeManager"),
        "fixtures": {"editmode": ("ProjectLucid.Tests.OriginalGameTimeScaledUtilitiesPreservationTests",
                                  "ProjectLucid.Tests.OriginalTimeScaledUtilitiesPreservationTests")},
    },
    # TimeScaledComponent's BeforeFieldInit initializer 0x06000ec7 (x86-64
    # 0x1b19ff0) stores GetSystemRef<TimeManager>(null, true) (0x1b1a04a..
    # 0x1b1a051) in its readonly static m_timeManagerRef, which every
    # descendant shares, appending the same null 'Hardlight.TimeManager' row as
    # TimeScaledUtilities above. No other Play Mode fixture runs a
    # TimeScaledComponent descendant; the SurfaceMeshSplineGenerator Awake
    # fixture does (its OnDisable reads the reference), so its first case in
    # Play Mode order causes these changes once. Not yet recorded natively.
    "TimeScaledComponent..cctor": {
        "reason": "TimeScaledComponent's type initializer 0x06000ec7 appends the permanent 'Hardlight.TimeManager' "
                  "row that holds its readonly m_timeManagerRef; removing it would detach that reference",
        "changes": _first_use_row_changes("Hardlight.TimeManager"),
        "fixtures": {"playmode": ("ProjectLucid.Tests.SurfaceMeshSplineGeneratorAwakeTests",)},
    },
    # PerformanceDependency's static initializer 06000d75 (x86-64 0x1b10280)
    # stores GetSystemRef<ScalablePerformance>(null, true) (0x1b102e1) in its
    # readonly s_scalablePerformanceRef, appending the null
    # 'Hardlight.ScalablePerformance' row on the first use of the type in each
    # mode's run; removing it would detach that reference from every later
    # registration. The native Edit Mode runs (run19 at 928e689, and 4beb125)
    # recorded these eleven changes for the Gap fixture's Destroy case, the
    # first in run order to use the type. In Play Mode the Destroy play case is
    # the only case that runs the initializer; the other ScalablePerformance
    # cases lease the row instead.
    "PerformanceDependency..cctor": {
        "reason": "PerformanceDependency's type initializer 06000d75 appends the permanent "
                  "'Hardlight.ScalablePerformance' row that holds its readonly s_scalablePerformanceRef; removing it "
                  "would detach that reference",
        "changes": _first_use_row_changes("Hardlight.ScalablePerformance"),
        "fixtures": {"editmode": ("ProjectLucid.Tests.OriginalPerformanceDependencyGapTests",
                                  "ProjectLucid.Tests.OriginalPerformanceDependencyPreservationTests"),
                     "playmode": ("ProjectLucid.Tests.OriginalPerformanceDependencyDestroyPlayTests",)},
    },
    # InputActionSubscription's type initializer (IL 14 17 28 53 00 00 2B 80
    # C3 03 00 04 2A as ControllerActionCurrentFacts records it: ldnull;
    # ldc.i4.1; call GetSystemRef<InputSystem>; stsfld m_inputSystemRef; ret)
    # stores GetSystemRef<InputSystem>(null, true) in
    # its readonly m_inputSystemRef, appending the null
    # 'HardlightProject.InputSystem' row on the type's first use; removing it
    # would detach that reference from every later InputSystem registration.
    # Native run20 (run20-b487b1d editmode-isolation.jsonl line 324) recorded
    # these eleven changes for ControllerActionTests'
    # GenuineConstructorsAndPersistentSystemCache, the first case in run order
    # to use the type. Only these two Edit Mode fixtures construct an
    # InputActionSubscription; the Play Mode fixture runs the initializer in
    # OneTimeSetUp, outside every per-case comparison.
    "InputActionSubscription..cctor": {
        "reason": "InputActionSubscription's type initializer appends the permanent 'HardlightProject.InputSystem' row "
                  "that holds its readonly m_inputSystemRef; removing it would detach that reference",
        "changes": _first_use_row_changes("HardlightProject.InputSystem"),
        "fixtures": {"editmode": ("ProjectLucid.Tests.ControllerActionTests",
                                  "ProjectLucid.Tests.OriginalControllerSubscriptionGapTests")},
    },
}


def isolation_side_effect(test, mode, changes, side_effects=None):
    """The ISOLATION_SIDE_EFFECTS name a leak of test in mode with exactly these changes may be, or None."""
    side_effects = ISOLATION_SIDE_EFFECTS if side_effects is None else side_effects
    return next((name for name, entry in side_effects.items()
                 if tuple(changes) == tuple(entry["changes"]) and
                 any(test.startswith(fixture + ".") for fixture in entry["fixtures"].get(mode, ()))), None)


# Each mode's hook controls need their own single clean line. The first fails
# in Unity unless the recorder saw it start, so a passing control and its line
# show the hook ran in that mode.
ISOLATION_HOOK_CONTROLS = {
    "editmode": ("ProjectLucid.Tests.SharedStateIsolationHookTests.HookCapturedThisTestBeforeItsBody",
                 "ProjectLucid.Tests.SharedStateIsolationHookTests.LeakRecordNamesTheTestAndEachChange"),
    "playmode": ("ProjectLucid.Tests.SharedStateIsolationPlayModeHookTests.HookCapturedThisTestBeforeItsBody",
                 "ProjectLucid.Tests.SharedStateIsolationPlayModeHookTests.HookStaysOpenAcrossFrames"),
}
ISOLATION_FIELDS = {"nonce", "test", "status", "changes"}


def _isolation_record(line):
    """One SharedStateIsolationRecorder line, or None when it is malformed."""
    try:
        record = json.loads(line)
    except json.JSONDecodeError:
        return None
    if not isinstance(record, dict) or set(record) != ISOLATION_FIELDS:
        return None
    changes = record["changes"]
    if (not isinstance(record["nonce"], str) or not isinstance(record["test"], str) or not record["test"] or
            record["status"] not in ("clean", "leak", "unchecked") or not isinstance(changes, list) or
            not all(isinstance(change, str) and change for change in changes) or
            (record["status"] == "clean") != (not changes)):
        return None
    return record


def isolation_verdict(path, nonce, xml, mode, allowlist=None, side_effects=None):
    """Check SharedStateIsolationRecorder's JSON lines against the run's test XML.

    Every project test case needs a line carrying this run's nonce, and every
    line must be clean, a leak with exactly its allowlisted changes, or the
    single occurrence of a listed side effect. A missing report means the
    detector did not run, which fails rather than passes.
    """
    allowlist = ISOLATION_ALLOWLIST if allowlist is None else allowlist
    side_effects = ISOLATION_SIDE_EFFECTS if side_effects is None else side_effects
    path = Path(path)
    if not path.is_file():
        raise ValueError("Isolation report missing: " + str(path) + "; the per-test detector did not run")
    if path.stat().st_size > 64 * 1024 * 1024:
        raise ValueError("Isolation report exceeds the supported size: " + str(path))
    data = path.read_bytes()
    try:
        lines = data.decode("utf-8").splitlines()
    except UnicodeDecodeError as error:
        raise ValueError("Isolation report is not UTF-8: " + str(path)) from error
    _, root = _read_test_xml(xml)
    errors = []
    records = {}
    for number, line in enumerate(lines, 1):
        record = _isolation_record(line)
        if record is None:
            errors.append("Malformed isolation report line " + str(number))
        elif record["nonce"] != nonce:
            errors.append("Isolation report line " + str(number) + " is not from this run: " + record["test"])
        else:
            records.setdefault(record["test"], []).append(record)
    leaks, allowlisted, permanent = [], [], []
    seen_effects = {}
    for test in sorted(records):
        for record in records[test]:
            detail = "; ".join(record["changes"])
            effect = isolation_side_effect(test, mode, record["changes"], side_effects)
            if record["status"] == "unchecked":
                errors.append("Isolation unchecked for " + test + ": " + detail)
            elif record["status"] == "leak" and test in allowlist and record["changes"] == list(allowlist[test]["changes"]):
                allowlisted.append({"test": test, "changes": record["changes"], "reason": allowlist[test]["reason"]})
            elif record["status"] == "leak" and effect is not None:
                seen_effects.setdefault(effect, []).append(test)
                permanent.append({"test": test, "side_effect": effect, "changes": record["changes"],
                                  "reason": side_effects[effect]["reason"]})
            elif record["status"] == "leak" and test in allowlist:
                leaks.append({"test": test, "changes": record["changes"]})
                errors.append("Isolation leak in " + test + " differs from its allowlisted changes: " + detail)
            elif record["status"] == "leak":
                leaks.append({"test": test, "changes": record["changes"]})
                errors.append("Isolation leak in " + test + ": " + detail)
    # Only temporary allowlist entries must keep leaking; a side effect may not occur in this ordering.
    for test in sorted(set(allowlist) & set(records)):
        if all(record["status"] == "clean" for record in records[test]):
            errors.append("Allowlisted isolation leak no longer leaks; remove its entry: " + test)
    for effect in sorted(seen_effects):
        if len(seen_effects[effect]) > 1:
            errors.append("Isolation side effect " + effect + " seen more than once in one report: " +
                          ", ".join(seen_effects[effect]))
    for test in ISOLATION_HOOK_CONTROLS[mode]:
        if [record["status"] for record in records.get(test, ())] != ["clean"]:
            errors.append("Isolation hook control has no single clean line: " + test)
    unseen = sorted(_project_cases(root) - set(records))
    if unseen:
        errors.append("Isolation report has no line for: " + ", ".join(unseen))
    return {"status": "complete" if not errors else "incomplete", "report": str(path), "nonce": nonce,
            "sha256": hashlib.sha256(data).hexdigest(), "checked": sum(map(len, records.values())),
            "leaks": leaks, "allowlisted": allowlisted, "side_effects": permanent, "errors": errors}


def run_tests(repo_root, work_dir, mode):
    if mode not in ("editmode", "playmode"):
        raise ValueError("Test mode must be editmode or playmode")
    editor = find_editor()
    before = current_identity(repo_root)
    xml = managed_path(work_dir, "reports", mode + ".xml")
    log = managed_path(work_dir, "reports", mode + ".log")
    xml.parent.mkdir(parents=True, exist_ok=True)
    # A failed Editor invocation must never accidentally reuse a passing result.
    isolation = managed_path(work_dir, "reports", mode + "-isolation.jsonl")
    xml.unlink(missing_ok=True)
    log.unlink(missing_ok=True)
    # The recorder appends, so an earlier run's lines must not be read as this run's.
    isolation.unlink(missing_ok=True)
    command = [str(editor), "-batchmode", "-projectPath", str(Path(repo_root).resolve()),
               "-runTests", "-testPlatform", "EditMode" if mode == "editmode" else "PlayMode",
               "-testResults", str(xml), "-logFile", str(log)]
    environment = os.environ.copy()
    environment["LUCID_RECOVERY_WORK_DIR"] = str(Path(work_dir).resolve())
    isolation_nonce = uuid.uuid4().hex
    environment["LUCID_ISOLATION_REPORT"] = str(isolation.resolve())
    environment["LUCID_ISOLATION_NONCE"] = isolation_nonce
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
    result = run_owned(command, env=environment, timeout=1800)
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
    try:
        isolation_result = isolation_verdict(isolation, isolation_nonce, xml, mode)
        errors.extend(isolation_result["errors"])
    except ValueError as error:
        errors.append(str(error))
        isolation_result = {"status": "incomplete", "report": str(isolation), "nonce": isolation_nonce}
    report = {"schema_version": 1, "generated_by": "ProjectLucid.run_tests", "mode": mode,
              "status": "complete" if not errors and verdict["status"] == "complete" and
              isolation_result["status"] == "complete" else "incomplete",
              "unity_version": actual_version, "editor": str(editor), "exit_code": result.returncode,
              "xml": str(xml), "log": str(log), "verdict": verdict, "isolation": isolation_result,
              "errors": errors, **after}
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
    recorded = receipt.get("isolation")
    if not isinstance(recorded, dict) or not isinstance(recorded.get("nonce"), str):
        raise ValueError(mode + " test receipt has no isolation verdict")
    isolation = isolation_verdict(Path(work_dir) / "reports" / (mode + "-isolation.jsonl"), recorded["nonce"], xml, mode)
    if isolation["status"] != "complete" or isolation["sha256"] != recorded.get("sha256"):
        raise ValueError(mode + " isolation report is incomplete or differs from its generated receipt")
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

    Conservative policy: paths with a backslash or a character above U+FFFF use
    direct Editor hashing, whose result must still equal the wrapper's native
    content fingerprint. Literal POSIX filename characters are preserved;
    only host directory separators become forward slashes.
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
    diagnostics = ("missing_guids", "unresolved_object_references", "invalid_script_bindings", "shader_errors")
    for key in diagnostics:
        if not isinstance(report.get(key), list):
            raise ValueError("Invalid reference audit diagnostics: " + key)
    for item in report["missing_guids"]:
        if (not isinstance(item, dict) or not isinstance(item.get("guid"), str) or re.fullmatch(r"[0-9a-f]{32}", item["guid"]) is None or
                not isinstance(item.get("examples"), list) or not 0 < len(item["examples"]) <= 8 or
                any(not isinstance(value, str) or not value for value in item["examples"])):
            raise ValueError("Invalid missing reference diagnostic")
    # The Editor emits targets and examples in .NET ordinal (UTF-16 code unit)
    # order, at most eight distinct examples of at most 256 code units each.
    def ordinal(value):
        return value.encode("utf-16-be", "surrogatepass")

    previous = None
    for item in report["unresolved_object_references"]:
        if (not isinstance(item, dict) or any(not isinstance(item.get(key), str) or not item[key] for key in ("target", "reason")) or
                (previous is not None and ordinal(item["target"]) <= previous) or
                not isinstance(item.get("examples"), list) or not 0 < len(item["examples"]) <= 8 or
                any(not isinstance(value, str) or not value or len(ordinal(value)) > 512 for value in item["examples"]) or
                any(ordinal(left) >= ordinal(right) for left, right in zip(item["examples"], item["examples"][1:]))):
            raise ValueError("Invalid object reference diagnostic")
        previous = ordinal(item["target"])
    for key in ("invalid_script_bindings", "shader_errors"):
        if any(not isinstance(value, str) or not value for value in report[key]):
            raise ValueError("Invalid reference audit diagnostic text")
    unresolved = sum(len(report[key]) for key in diagnostics)
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
    lock = take_work_lock(managed_path(work, "locks", "reference-audit.lock"), "Another reference audit owns the generated audit lock")
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
        result = run_owned(command, timeout=1800)
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
        release_work_lock(lock)

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

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalSplineRuntimeLookupTests.InertRatioLookupsUseTheSelectedKnotLutSearchAndUnclampedLerp",
    "ProjectLucid.Tests.OriginalSplineRuntimeLookupTests.KnotControlPointsUseUnitScaleLocalKnotMatrices",
)

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

# Low-priority coverage gaps, round A: native-grounded single cases.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalLeaderboardGapTests.InitializersCreateGameCenterThenLeaderboardRowsBeforeTheManagerRow",
    "ProjectLucid.Tests.OriginalLeaderboardGapTests.AppShutdownClearsTheListenerWithoutDeinitialisingOrUnsubscribing",
    "ProjectLucid.Tests.OriginalLeaderboardGapTests.InvalidHolderCompletesBothRequestsFalseOnTheFirstStepWithoutListeners",
    "ProjectLucid.Tests.OriginalControllerProviderGapTests.SelectionPassesEachPollingRateToItsOwnOriginalWait",
    "ProjectLucid.Tests.OriginalAudioSourceDataGapTests.PresentSourceWithoutAClipFaultsOnTheClipLength",
    "ProjectLucid.Tests.OriginalLinkedLevelTestsGapTests.LiveScenesRenameEntriesThroughSceneNameAndDestroyedScenesKeepTheirNames",
    "ProjectLucid.Tests.OriginalNotificationsGapTests.EachOverrideIsCheckedAndReportedTemplateFirst",
    "ProjectLucid.Tests.OriginalUIGapTests.NonHLInputModuleIsStoredAsNoModule",
    "ProjectLucid.Tests.OriginalUIGapTests.StartActivatesTheOverriderWithoutAPlatformOrNullTest",
    "ProjectLucid.Tests.OriginalUIGapTests.ScrollLeftHandlerIsAMemberlessEventMarker",
    "ProjectLucid.Tests.OriginalUIGapTests.ScrollRightHandlerIsAMemberlessEventMarker",
    "ProjectLucid.Tests.OriginalPropertyStoreGapTests.StoreIsPublishedBeforeItsStorageConstructionFaults",
    "ProjectLucid.Tests.OriginalPropertyStoreGapTests.WipeAllSaveFilesLetsAnEnumerationFaultEscape",
    "ProjectLucid.Tests.OriginalGuiPageLoaderGapTests.RepeatedEnumValueFaultsAfterKeepingEarlierMappings",
    "ProjectLucid.Tests.OriginalBuildValidationGapTests.NullSeverityRecordFaultsUnlessAnEarlierRecordMatches",
    "ProjectLucid.Tests.OriginalBuildValidationGapTests.NullSceneExclusionFaultsUnlessAnEarlierExclusionMatches",
    "ProjectLucid.Tests.OriginalAnalyticsEventGapTests.SpawnBuildsAFiveEventPoolOnceAndInitialisesEachSpawnedEvent",
)
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalControllerProviderGapPlayTests.SecondInitialiseLeavesTheFirstPollingLoopRunningAfterShutdown",
    "ProjectLucid.Tests.OriginalUIVisibilityGroupMemberGapPlayTests.DestroyedWaitingMemberKeepsItsDeferredManagerCallback",
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

EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalControllerSubscriptionGapTests." + name for name in (
    "ApplyModifiersRejectsOnlyValuesStrictlyInsideTheFloatTolerance",
    "ApplyModifiersRunsTheAuthoredListInOrderBeforeTheTolerance",
    "SubscribeRegistersHeldThenUpAndUnsubscribeRemovesBoth",
    "InputDisabledSubscriptionFollowsTheStaticOnesOnlyForARegisteredSystem",
))

# HLCrossPromo full screen view, placement and launch button.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalCrossPromoFullScreenViewTests." + name for name in (
    "DeclarationsKeepOriginalConstantsFieldsAndIterator",
    "PreviousAndNextAdWrapAroundTheAdList",
    "EasingIsCubicInOutOverTheDoubledProgress",
    "ButtonTextFallsBackToPlayForFreeOnEveryPage",
    "ScrollingUiNeedsAtLeastTwoAds",
    "ShowWithAPromoNotReadyBuildsPipsAndRefreshesTheRequestedAd",
    "ShowSkipsTheDirectRefreshWhenReadyAndDisableReleasesEveryPage",
    "ShowWithAReadyCrossPromoRefreshesThroughTheImmediateInitialisedCallback",
    "ShowWhileSyncingPassesANegativeIndexStraightToTheRefresh",
    "DisableBeforeShowClearsPagesWithoutAPromo",
    "DockedPageNumberSnapsOnlyWithinAccuracy",
    "DragOnADockedPageOnlyRecordsThePage",
    "ReleaseOffAPageChoosesItsTargetFromTheDragDirection",
    "PageMovesTargetHalfStepsAndSkipTheCurrentPage",
    "ScrollAnimationDrawsItsFirstFrameAtTheStart",
    "ScrollAnimationContinuesWhileProgressEqualsTheDuration",
    "ScrollAnimationDocksAndRefreshesOnTheOuterPages",
))

EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalCrossPromoFullScreenPlacementTests." + name for name in (
    "DeclarationsKeepOriginalFields",
    "AwakeWaitsForTheSystemThenSubscribesShowUntilDestroyed",
    "AwakeWithARegisteredSystemInitialisesImmediately",
    "DestroyBeforeInitialisationOnlyLeavesTheStartupObserver",
    "EnableHidesTheViewAndCloseAlsoRaisesClosed",
))

EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalCrossPromoLaunchButtonTests." + name for name in (
    "DeclarationsDefaultsAndTheBaseGate",
    "InitialisedResetsImpressionsAndKeepsTheAdOnlyForTheSameList",
    "InitialisedShowsTheFirstAdWithAFreshImpressionCount",
    "ShowingAnAdSwapsTheIconAndCountsImpressionsOnlyWhenOneIsReturned",
    "LaunchRequestsTheCurrentAdThenRotates",
    "UpdateHidesOnAClosedGateAndRetriesHiddenContent",
    "EnableWaitsForTheSystemAndDisableLeavesInitialised",
    "EnableWithARegisteredSystemInitialisesAndDisableWithoutOneLeavesStartup",
))

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

# Low-priority coverage-gap cases: binder evaluation order.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalBinderGapTests.ArgumentsAreReadInOrderBeforeTheFormatAndOnlyANullFormatBecomesEmpty",
)

# Restored HLBinders.Runtime binder owners: targets, outputs, binders, toggles and TMP_Gradient nodes.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalBindersCoreTests." + name for name in (
    "BindTargetTypesItsMemberConvertsStringsAndKeepsTheLastTypeForAMissingMember",
    "BindTargetEqualityComparesOnlyTargetAndMemberName",
    "BindTargetTreatsADestroyedTargetAsUnityNull",
    "BinderOutputForwardsOnlyWhenSourceIsSetTargetExistsAndEnabled",
    "SimpleBinderInvertsOnlyBoolSourcesAndHidesFromTheNodeEditor",
    "AdvancedBinderTogglesOnlyNodeEditorNodesAndNeverCachesInTheEditor",
    "SetListValuePublishesACopyOnlyWhenTheElementsDiffer",
    "ConstantValueSettersRefreshEveryOutputInDeclarationOrder",
    "LogicalToggleRaisesOnlyTheFirstEqualInput",
    "LogicalToggle3RaisesOnlyTheFirstEqualInput",
    "TmpGradientNodesSelectAndPublishTheGradientObject",
    "RestoredOwnersKeepTheirOriginalMenuAttributes",
    "RestoredOwnersKeepTheirOriginalSerializedFieldsAndLifecycleFlags",
    "BindingUtilsLambdasKeepTheShippingGeneratedNames",
))

# Low-priority coverage-gap cases: diagnostics routed to a registered core.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalHLOutputDiagnosticGapTests.NullRangeReportsTheOriginalDiagnosticAndLeavesTheTarget",
    "ProjectLucid.Editor.OriginalHLOutputDiagnosticGapTests.NullIncludedFilesReachTheAddRangeDiagnosticAfterRequiredFiles",
    "ProjectLucid.Editor.OriginalHLOutputDiagnosticGapTests.InvalidVersionTextReportsEachOriginalDiagnosticThroughTheRegisteredCore",
)

# Low-priority coverage-gap cases: game data values and declarations.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalGameDataGapTests.FirstCollectionWrapsTheCountAtInt32MaxValue",
    "ProjectLucid.Tests.OriginalGameDataGapTests.IsNewStateSetterMarksTheStoreDirtyEvenForAnUnchangedValue",
    "ProjectLucid.Tests.OriginalGameDataGapTests.AnalyticsEnumsKeepTheOriginalHashedLiteralsAndLabels",
    "ProjectLucid.Tests.OriginalGameDataGapTests.LedgeBrakeFieldsKeepTheOriginalPerFieldAttributeOrder",
    "ProjectLucid.Tests.OriginalGameDataGapTests.PyramidMeshUsesTheOriginalCallOrderAndIndexInitializer",
    "ProjectLucid.Tests.OriginalGameDataGapTests.MeshDelegatesWithoutCallsLeaveTheMeshCallOrderEmpty",
)

# Low-priority coverage-gap cases: core utilities and registries.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalHLUnityCoreGapTests.BestMatchKeepsTheFirstTieAndRejectsScoresAtInt32MaxValue",
    "ProjectLucid.Tests.OriginalHLUnityCoreGapTests.ClassInitializerRegistersOnlyTheOriginalPrimitiveOperations",
)

# Low-priority coverage-gap cases: UI scroll, text and sound forwarding.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalUIScrollTextAudioGapTests.OffsetTestsMinFirstHorizontallyAndMaxFirstVertically",
    "ProjectLucid.Editor.OriginalUIScrollTextAudioGapTests.TextAndGradientHelpersForwardToOwnedComponentsAndIgnoreNull",
    "ProjectLucid.Editor.OriginalUIScrollTextAudioGapTests.NullClipStillSetsPitchAndVolumeAndReachesPlayOneShot",
)

# Low-priority coverage-gap cases: input modifier defaults and cutoff.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalInputModifierGapTests.ConstructorsKeepTheOriginalCooldownAndDeadZoneDefaults",
    "ProjectLucid.Tests.OriginalInputModifierGapTests.VectorCurveCutsOffBelowTheOriginalSquaredMagnitude",
)

# Low-priority coverage-gap cases: ribbon end edge.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalSplineGapTests.LastBlockEndEdgeUsesTheRealKnotCountAndInclusiveTolerance",
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
    "ProjectLucid.Editor.Tests.PortableControllerSelectionTests.SelectionUsesTheInputSystemProviderWithIndependentNotifications",
    "ProjectLucid.Editor.Tests.PortableControllerSelectionTests.ConnectionNotificationWorksBeforeSubscriptionAndAfterUnsubscription",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.LocalCoreNativeBridgeTests.LocalePolicyTracksTwoOwnedCultureScopes",
    "ProjectLucid.Tests.LocalCoreNativeBridgeTests.OfflineMacOSOutputPluginNeverCallsTheDevelopmentBuildImport",
    "ProjectLucid.Tests.LocalCoreNativeBridgeTests.OfflineMacOSOutputPluginReportsTheNativeLogErrorTestAsUnsupported",
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
    "ProjectLucid.Tests.OriginalMenuBloomPreservationTests.OriginalMethodsAndIteratorRetainNativeSignaturesAndSlotTypes",
    "ProjectLucid.Tests.OriginalMenuBloomPreservationTests.NormalInactiveAttachmentRetainsConstructorDefaultAndRequiredComponents",
)

PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalMenuBloomLifecycleTests.NonpositiveAndNaNDurationsCompleteSynchronouslyWithoutFading",
    "ProjectLucid.Tests.OriginalMenuBloomLifecycleTests.StallRetainsEntryAlphaRejectsReentryAndFinishOnlyReleasesIt",
    "ProjectLucid.Tests.OriginalMenuBloomLifecycleTests.PositiveFadeHoldsLastRisingSampleAndCompletesAfterRelease",
    "ProjectLucid.Tests.OriginalMenuBloomLifecycleTests.StoppingTheOwnedCoroutineDoesNotInventCancellationCleanup",
)

# Original automatic getters carry one CompilerGenerated each (DummyDll metadata).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalAutoGetterCompilerGeneratedTests.EntityActivationInstanceEntityGetterCarriesOneCompilerGenerated",
    "ProjectLucid.Tests.OriginalAutoGetterCompilerGeneratedTests.UIContainerLoadingParametersGettersCarryOneCompilerGenerated",
    "ProjectLucid.Tests.OriginalAutoGetterCompilerGeneratedTests.MetaGameStateGettersCarryOneCompilerGenerated",
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

# Genuine SurfaceEvent stay routine cadence on fixed steps.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.SurfaceEventLifecycleTests.AuthoredStayRoutineResumesOncePerFixedStep",
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
    'OriginalHLInputDebugMenuContractsTests.GlyphComparatorKeepsOriginalCompilerNamesAndOrdinalEnumNameOrder',
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
    'ProjectLucid.Tests.OriginalConfigurationAssetSerializationTests.RuntimeConfigurationWithoutAnAuthoredArrayFaultsOnEveryWrapperRead',
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
    'ProjectLucid.Tests.Editor.OriginalMenuConfigurationAssetSerializationTests.GenuineCoreFlagsEachReachOnlyTheirOwnGetterAndChallengesNeedGameCenter',
    'ProjectLucid.Tests.Editor.OriginalMenuConfigurationAssetSerializationTests.GenuineLocalisationDefaultsAndOppositeFlagsReachTheirOwnGetters',
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

# crosspromo-net: restored HLCrossPromo download handler and download/resource managers; no request is sent.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.Editor.OriginalCrossPromoNetworkTests." + name for name in (
    "DownloadHandler_ConstructorCreatesIncomingFolderAndStagesUnderTheTargetFileName",
    "DownloadHandler_CompleteContentNeedsMoreBytesThanTheThresholdAndAStream",
    "DownloadHandler_ExistingTargetIsReplacedOnlyWhenDuplicatesAreDeleted",
    "DownloadHandler_OpenFailureLogsReturnsFalseAndRetriesOnTheNextChunk",
    "DownloadHandler_ErrorReportsTheServerMessageWhileTheTransportErrorIsEmpty",
    "DownloadManager_StartsOneCoroutinePerLocalPathAndKeepsItPending",
    "DownloadManagerCacheOnly_MetadataAndSpritesShareOneIdentifierClaim",
    "DownloadManagerCacheOnly_FileDownloadIsNotImplementedAndClaimsNothing",
    "ResourceManager_ConstructorKeepsDestinationFilesAndRecreatesAnEmptyIncomingFolder",
    "ResourceManager_MetaDataDownloadsOverTheMetaFileAndReadsItBack",
    "ResourceManager_VerifyAdResourcesDownloadsMissingImagesAndPrunesUnnamedFiles",
    "ResourceManager_TextureRequestsUseThePoolThenTheDeviceThenADownload",
    "ResourceManager_ReleasesAndPendingStateReachOnlyTheirOwnCollaborator",
    "CacheOnly_MetaDataArrivesThroughTheMetadataCallbackAndVerifyOnlyRaisesPending",
    "CacheOnly_TextureMissDownloadsOnceThenServesTheDecodedSprite",
    "CacheOnly_TextureDownloadCopiesPendingFromTheDownloadManager",
    "CacheOnly_DefaultCompletionHandlerAcceptsAnUnsubscribedNotification",
))

# Restored CrossPromo and CrossPromoDataParser over recording collaborators and in-memory JSON.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalCrossPromoTests." + name for name in (
    "ConstructorSubscribesBeforeRequestingMetadataAndStartsTheSync",
    "EmptyMetadataEndsTheSyncWithoutInitialised",
    "JsonNullEndsTheSyncWithoutInitialised",
    "RejectedMetadataEndsTheSyncWithoutInitialised",
    "DisabledMetadataEndsTheSyncWithoutVerifyingAds",
    "EnabledMetadataVerifiesAdsAndFinishesWhenNoDownloadIsPending",
    "EnabledMetadataWithNothingPendingRaisesInitialisedAtOnce",
    "InitialisedAddCallsBackOnceReadyInsteadOfSubscribing",
    "TextureCallsForwardAndRaisersReachOnlyTheirOwnEvent",
    "CrossPromoDeclarationsKeepOriginalFlagsAndOrder",
    "PlatformSuffixedKeyWinsOverTheBareKey",
    "BlankPlatformReadsOnlyTheBareKey",
    "VersionRouterTakesTheExactAppVersion",
    "VersionRouterTakesTheHighestVersionBelowTheApp",
    "VersionRouterWithOnlyHigherVersionsFails",
    "MetadataFailuresStopAtTheFirstMissingStep",
    "SystemFlagOneAndLanguageTextAreRead",
    "EmptyLanguageTextFallsBackToDefault",
    "MissingLanguageTextFallsBackToDefault",
    "NullLanguageReadsDefault",
    "MissingPressToPlayTableGivesNull",
    "AdsKeepListOrderAndDropMissingDisabledOrIncompleteEntries",
    "MainImageOfExactlyTheScreenSizeWins",
    "MainImagePrefersTheClosestRatioThenTheClosestHeight",
    "MainImageHeightDifferenceOfIntMinValueDoesNotThrow",
    "ParserDeclarationsKeepOriginalFlagsOrderAndDefaults",
))

# Original procedural-mesh placement bodies on owned objects and in-memory surfaces.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalProceduralMeshPlacementTests." + name for name in (
    "PerformanceSetupConstructorAndResetStoreTheNativeDefaults",
    "PerformanceSetupComparesQualityLevelsOnly",
    "AwakeObservesTheScalablePerformanceReferenceOnce",
    "GetSetupCachesTheDefaultOrTheLowestProfileOfASortedCopy",
    "ProfileUpdatesDropTheSelectionAndRaiseOnUpdatedOnce",
    "AddErrorMessageAllocatesOnlyForANullList",
    "GetPathJoinsEveryAncestorFromTheRoot",
    "TransformBoundsSumsTheAbsoluteTransformedExtentAxes",
    "FrustumTestHonoursTheConfigFlagAndUsesTransformedBounds",
    "ConstructorsStoreTheNativeFieldDefaults",
    "ResetAddsPresentSurfacesAndOneDefaultMeshType",
    "SplineGridErrorsFollowTheNativeOrderAndPredicates",
    "RibbonRandomErrorsRejectOnlyNegativeMeshCounts",
    "MeshRandomErrorsReportOnlyMissingMeshesAndRatios",
    "HasValidSetupRecomputesValidityFromTheErrorList",
    "InvalidDoUpdateDespawnsOnlyWhenStorageExists",
    "DoUpdateDespawnsOnlyStrictlyBeyondTheDespawnDistance",
    "InitialiseSetupSquaresTheProfileDistancesAndSubscribesOnce",
    "InitialiseSetupCreatesADefaultSetupWhenNoneIsAssigned",
    "GetSetupSelectsTheHighestProfileNotAboveTheLiveAttributeLevel",
    "StartRegistersAndOnDestroyUnregistersTheDespawnedPlacement",
    "OnDestroyToleratesAMissingManagerAndSetup",
    "MissingStorageInvalidatesSpawnChecksAndEmptyStorageHasZeroBounds",
    "RibbonBoundsEncapsulateTheStartCentreAndEveryEdgeKnot",
    "SplineBoundsEncapsulateEveryKnotOfTheRange",
    "SplineAreaSumsKnotLengthsWithAnExclusiveEnd",
    "RibbonAreaIsTheTrapezoidSumOfEdgeWidthsTimesSpanLengths",
    "ManagerSubscribesToUpdateAndDispatchesDoUpdateInOrder",
))

# Original gravity volumes, hover fan, look-at and leave-checker bodies on owned objects.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalGravityVolumeTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsMembersAndAttributes",
    "ConstructorsStoreTheNativeDefaults",
    "TriggerCachedValuesPublishTheDescriptionAndATriggerCollider",
    "SphereGravityFollowsTheOffsetFromTheCentre",
    "HemisphereRejectsOnlyPointsStrictlyBelowItsPlane",
    "HemisphereKeepsTheSphereGravityForAnUnorderedHeight",
    "AxisGravityPointsAwayFromTheForwardAxis",
    "AxisCachedValuesLayOutTheColliderAlongForward",
    "CapsuleArcAppliesOnlyInsideItsInclusiveArc",
    "CapsuleArcDoesNotRejectAgainstUnorderedLimits",
    "CapPositionNegatesTheAngleAndAddsNoPosition",
    "BoxCachedValuesRaiseTheColliderAboveTheTransform",
    "BoxGravityTakesTheNearestFaceAndTheLastOfEqualFaces",
    "BoxGravityLetsAnUnorderedDistanceReplaceTheBestFace",
    "BoxFacesFollowTheCurrentColliderCentre",
    "PlaneGravityUsesOnlyTheHeightAlongUp",
    "PlaneCachedValuesLeaveADecoupledColliderLayout",
    "ClosestPointOnTorusMirrorsInsideAndClampsTheHeight",
    "TorusGravityUsesTheNormalAndTheClosestPointDistance",
    "TorusGravityMeasuresInWorldSpaceWithADirectionOnlyNormal",
    "TorusCachedValuesSpanTheGrownRing",
    "SurfacePlaneRecordsTheHeightBeforeTheActiveGate",
    "SurfacePlaneValidationTruthTable",
    "SurfacePlaneValidationReadsTheDescriptionUnguarded",
    "RenderForwardsToTheSourceWithoutANullCheck",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalLookAtTargetTests." + name for name in (
    "DeclarationsKeepTheOriginalFieldsAndMethods",
    "AwakeFixesTheUpAxisAndTheDefaultTargetPosition",
    "SetLookAtChoosesTheDefaultOnlyWithoutATarget",
    "UpdateTurnsTowardsTheTargetAndCanRetainTheVerticalAxis",
    "TheDefaultIsLookedAtOnceWithoutSmoothing",
    "TheDefaultIsKeptOnlyWhileTheVelocityIsAboveTheThreshold",
    "UpdatePassesTheAwakeUpAxisToLookAt",
    "SmoothingTreatsANonPositiveMaxSpeedAsUnclamped",
    "SmoothingKeepsTheDistanceAndHonoursAPositiveMaxSpeed",
    "TargetConstraintMovesOnlyPositiveOrUnorderedDistances",
    "ConstraintsPairTheParentAxesInTheNativeOrder(0)",
    "ConstraintsPairTheParentAxesInTheNativeOrder(1)",
    "ConstraintsPairTheParentAxesInTheNativeOrder(2)",
    "ConstraintsPairTheParentAxesInTheNativeOrder(3)",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalObjectLeaveCheckerTests." + name for name in (
    "DeclarationsAndConstructorDefaults",
    "StartAndOnValidateCacheTheSquaredThresholdAndTheBounds",
    "ActivateArmsTheCheckerAndClearsTheTimer",
    "HasObjectLeftCountsTheThresholdAndAMissingObjectAsLeft",
    "UnorderedThresholdsNeitherLeaveNorFire",
    "BoundsCheckRefreshesOnlyTheCachedCentre",
    "InternalUpdateFiresAtTheMinimumTimeAndKeepsTheFiringDelta",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHoverFanTests." + name for name in (
    "DeclarationsKeepTheOriginalFieldsAndMethods",
    "EnterLendsTheHoverDataAndExitRemovesIt",
    "ASecondEntryReplacesTheHandleWithoutRemovingTheFirst",
))

# Original out-of-bounds, platform, projection, platform-binding, follower and real-world-scale bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalOutOfBoundsTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsMembersAndAttributes",
    "ConstructorsAndStartStoreTheNativeDefaults",
    "PlaneTruthTableUsesTheChosenAxisInclusivelyAndRejectsNaN",
    "BoxAcceptsOnlyPositionsTheBoundsContain",
    "TriggerEnterReachesTheCharacterOnlyThroughItsCollisionCollider",
    "InstantDeathFlagReachesTheHardFailureBeforeTheRingLossFlag",
    "ManualEnterHitsOnlyEnemiesOnTheEnemyLayer",
    "TriggerColliderGetComponentForwardsToTheGameObject",
    "PositionCheckMarksTheCharacterOutOfBoundsUnlessARespawnIsUnderWay",
    "EnableDefersInitUntilTheCharacterManagerIsValid",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalPlatformTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsMembersAndAttributes",
    "ConstructorsStoreTheNativeDefaults",
    "ComponentAccessorsStoreTheirOwnFields",
    "CopyConstructorInstantiatesEachObjectUnderTheSameParent",
    "PieceSettersSkipDestroyedObjectsAndSetHeightKeepsXAndZ",
    "VariantStacksMidInstancesAndHidesThoseAboveTheHeight",
    "RemoveUnusedMidPieceInstancesDropsTheEntriesFromHeightMinusOne",
    "VariantInstanceStoresItsArgumentsAndIsNamedAfterItsAssetType",
    "DefinitionReturnsTheFirstMatchingPrefabAndTheGroupKeysByAssetType",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalProjectionPlacementTests." + name for name in (
    "DeclarationsKeepTheOriginalFieldsMethodsAndAttributes",
    "ConstructorInitialiseAndResetFollowTheNativeStores",
    "UpdateFollowsTheBallisticOffsetFromTheChosenEnd",
    "ARaycastHitSnapsThePointUntilTheTimerRunsOut",
    "ProjectionWithoutGravityAddsOnlyTheStart",
    "ProjectionTracesFromTheEndAndAddsTheFinalPositionWithoutAHit",
    "ProjectionRaycastsTheSummedStepsWhenTheTurnExceedsTheLimitOrAtTheEnd",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalPlatformBindingsTests." + name for name in (
    "DeclarationsKeepTheOriginalFieldsAndAttributes",
    "AwakeSetsOnlyTheMacOSFlag",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalRealWorldScaleTests." + name for name in (
    "DeclarationsAndConstructorDefaults",
    "ScalesTheTransformToTheDimensionsAtTheScreenDpi",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalPrototypeTransformFollowPlayerTests." + name for name in (
    "DeclarationsAndConstructorDefaults",
    "UpdateTurnsTowardsTheCharacterAndMovesAlongTheNewForward",
))

# Original transform rotators, movers, scalers, facing components and their triggers on owned objects.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTransformRotatorTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsMembersAndAttributes",
    "ConstructorsStoreTheNativeDefaults",
    "AwakeTakesTheTargetsWorldRotationAndOptionallyRandomSpeeds",
    "InternalUpdateRotatesLocallyBySpeedTimesMultiplierTimesDelta",
    "OnSynchroniseRestartsFromTheStartRotationAndAppliesTheWholeTime",
    "ClampToLimitWithAZeroLimitOnlyAccumulates",
    "ClampToLimitBelowTheLimitOnlyAccumulates",
    "ClampToLimitExactlyAtTheLimitIsReached",
    "ClampToLimitTakesBackAPositiveOvershoot",
    "ClampToLimitTakesBackANegativeOvershoot",
    "ClampToLimitCrossingZeroHoldsTheNegativeLimit",
    "ClampToLimitTakesTheCorrectionSignFromTheRotation",
    "ClampToLimitWithANegativeLimitIsAlwaysReached",
    "ClampToLimitWithANaNLimitIsReached",
    "ClampToLimitWithANaNAccumulationIsReached",
    "LimitsClampTheFirstReachedAxisAndRaiseTheEventEachStep",
    "RandomRotatorSnapsWithAZeroSmoothTime",
    "RandomRotatorSnapsWithANegativeSmoothTime",
    "RandomRotatorSnapsWithANaNSmoothTime",
    "RandomRotatorSmoothsFromTheCurrentRotationAfterDrawing",
    "RandomRotatorSmoothsAZeroMaxSpeedUnclamped",
    "RandomRotatorSmoothsANegativeMaxSpeedUnclamped",
    "RandomRotatorSmoothsANaNMaxSpeedUnclamped",
    "RandomRotatorSmoothsAtAPositiveMaxSpeed",
    "RandomRotatorSynchronisesTheLocalStartAsAWorldRotation",
    "SingleCurveSetsTheLocalYawAndCompletesStrictlyPastTheCurve",
    "SingleCurveCompletesWithoutAListener",
    "SnappedEnableStartsAnAxisForANaNAngleButNotForZero",
    "SnappedEnableStartsNothingForZeroAngles",
    "SnappedEnableStartsTheYAxisAlone",
    "SnappedRotationWaitsThenTurnsTheLocalRotationEachInterval",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTransformAnimatorTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsMembersAndAttributes",
    "ConstructorsStoreTheNativeDefaults",
    "MultiAxisStartSetsEachPostWrapModeAndTheInitialAngles",
    "MultiAxisAdvancesEveryAxisAndSamplesOnlyTheEnabledOnes",
    "OscillatorUiCallbackCachesThePositionAndStartsAnimating",
    "OscillatorOffsetsTheCachedPositionByASineWave",
    "OscillatorWrapsTheTimerWhenThePhaseIncludingOffsetPassesTwoPi",
    "PannerWrapsEachAxisAtTheSignedAmountAndZeroesZ",
    "PannerNeverWrapsAnAmountWithinEpsilonAndNegatesForANonPositiveSpeed",
    "ScalerBlendsTheScaleOnACosineWave",
    "ScaleTransformLerpsOnTheCurveOverItsLastKeyTime",
    "StartAndRevertScaleFromEachEndAndDisableRestoresTheOriginal",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTransformFacingAndTriggerTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsMembersAndAttributes",
    "SnapTakesTheTargetOnExactlyTheEnabledAxes",
    "RotationGravityCachesTheWorldRotatedGlobalGravityOnEnable",
    "FaceCameraWaitsForTheMainCameraAndUnsubscribesOnceSet",
    "FaceCameraShutdownDisablesAndRemovesItsHandler",
    "FaceCameraEnableSubscribesToARegisteredManager",
    "FaceCameraEnableDisablesUntilAManagerIsRegistered",
    "FacePlayerTargetsTheCurrentCharacterAndReleasesOnDisable",
    "FacePlayerEnableRegistersWithARegisteredManager",
    "TriggerPassesTheMatchingCollidersTransformToEnterAndExit",
    "TriggerDispatchesTheTargetAndNullOnExitVirtually",
    "LevelActivationSendsANullExitWhileTriggered",
    "PlatformEffectsPlayTheStartThenTheLoopAndTheWarningAlone",
))

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

# Prepared boot data read back through closed generic layouts (dictionary rows, highlight pointers).
EDITMODE_TESTS += (
    'ProjectLucid.Tests.Editor.OriginalBootDataPreparedAssetTests.PreparedTimeCategoryLookupReadsEveryAuthoredCategoryBack',
    'ProjectLucid.Tests.Editor.OriginalBootDataPreparedAssetTests.PreparedHighlightPrefabsKeepTheirDefinitions',
)

# The shipped quality levels select the prepared URP assets; FullscreenShaderManager's
# exact-type renderer feature lookups find their features on renderers 0 and 1.
EDITMODE_TESTS += (
    'ProjectLucid.Tests.Editor.OriginalRenderPipelineSelectionTests.EveryShippedQualityLevelSelectsItsPreparedPipelineAsset',
    'ProjectLucid.Tests.Editor.OriginalRenderPipelineSelectionTests.FullscreenShaderManagerLookupsFindTheirExactFeaturesAtEveryLevel',
)

# Owned closed storage and callbacks; supplied group payloads require separate qualification.
EDITMODE_TESTS += (
    'ProjectLucid.Tests.Editor.OriginalStateMachinesGroupSerializationTests.ClosedStateMachinesGroupRetainsThreeArgumentElementAndOriginalConstraints',
    'ProjectLucid.Tests.Editor.OriginalStateMachinesGroupSerializationTests.FsmDeclarationRetainsOriginalAttributeOrderAndCurrentConstructorTypes',
    'ProjectLucid.Tests.Editor.OriginalStateMachinesGroupSerializationTests.GenuineOwnedGroupPreservesOrderedNameAndDataStorageAfterReimport',
    'ProjectLucid.Tests.Editor.OriginalStateMachinesGroupSerializationTests.GenuineOwnedElementCallbacksRetainCurrentNameAndNullOrDestroyedNone',
)

# Hardlight.FiniteStateMachine field rows against dump.cs TypeDefIndex 8894.
EDITMODE_TESTS += (
    'ProjectLucid.Tests.Editor.OriginalFiniteStateMachineDeclarationTests.FieldRowsKeepOriginalBackingFieldPlacement',
)

# Hardlight.StackableData field and method rows against dump.cs TypeDefIndex 8796.
EDITMODE_TESTS += (
    'ProjectLucid.Tests.Editor.OriginalStackableDataDeclarationTests.FieldRowsKeepOriginalInstanceStaticAndEventOrder',
    'ProjectLucid.Tests.Editor.OriginalStackableDataDeclarationTests.MethodRowsKeepOriginalOrder',
    'ProjectLucid.Tests.Editor.OriginalStackableDataDeclarationTests.MethodRowsKeepOriginalOverloadOrderAndFlags',
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.Editor.VerificationAssemblyReferencesTests.VerificationAssemblyDeclaresAndResolvesPinnedUI",
    "ProjectLucid.Tests.Editor.VerificationAssemblyReferencesTests.PlayModeAssemblyDeclaresAndResolvesPinnedUI",
    "ProjectLucid.Tests.Editor.VerificationAssemblyReferencesTests.CoroutineScrollAssemblyDeclaresAndResolvesPinnedUI",
    "ProjectLucid.Tests.Editor.VerificationAssemblyReferencesTests.UnityUIRuntimeAssemblyDeclaresAndResolvesPinnedUI",
    "ProjectLucid.Tests.Editor.VerificationAssemblyReferencesTests.EventSystemsRuntimeAssemblyDeclaresAndResolvesPinnedUI",
    "ProjectLucid.Tests.Editor.VerificationAssemblyReferencesTests.CrossPromoRuntimeAssemblyDeclaresAndResolvesPinnedUI",
    "ProjectLucid.Tests.Editor.VerificationAssemblyReferencesTests.CrossPromoUIAssemblyDeclaresAndResolvesPinnedUI",
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
    "ProjectLucid.Editor.OriginalAppleTVRemoteSetterAttributesTests.ShowIfBlobDecoderRejectsNonStringBoxedNull",
    "ProjectLucid.Editor.OriginalAppleTVRemoteSetterAttributesTests.ShowIfBlobReaderReadsEachMembersOwnRow",
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

# FSM fixture ownership of the default FSMManager row; original GetManager registration stays separate.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.FSMFixtureManagerRowOwnershipTests.ColdRegistryRunsLeaveNoDefaultManagerRow",
    "ProjectLucid.Editor.FSMFixtureManagerRowOwnershipTests.EarlyFailureInsideTheRowScopeLeavesNoDefaultManagerRow",
    "ProjectLucid.Editor.FSMFixtureManagerRowOwnershipTests.CleanupThroughGetManagerLeavesALiveRowWhereTheRowScopeLeavesNone",
    "ProjectLucid.Editor.FSMFixtureManagerRowOwnershipTests.BorrowedLiveManagerKeepsItsInstanceAndHoldsNoFixtureMachines",
    "ProjectLucid.Editor.FSMFixtureManagerRowOwnershipTests.BorrowedEmptyRowKeepsItsObjectReferencesAndObservers",
)

# Fixture ownership of the default configuration row; original ProcessManager semantics stay separate.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.SystemConfigurationVerificationOwnershipTests.LiveForeignConfigurationIsRefusedBeforeAnyRegistryMutation",
    "ProjectLucid.Editor.SystemConfigurationVerificationOwnershipTests.DormantForeignConfigurationRowIsSetAsideAndRestoredUntouched",
    "ProjectLucid.Editor.SystemConfigurationVerificationOwnershipTests.RunFromAnAbsentRowLeavesNoConfigurationRow",
)

# Recovered-proof fixtures neither need nor touch borrowed OpString, cloud, configuration-row, name-table or LUT state.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.RecoveredFixtureBorrowedStateTests." + name for name in (
    "OpStringCacheScopeOwnsAFreshCacheAndRestoresEachPriorOwner(0)",
    "OpStringCacheScopeOwnsAFreshCacheAndRestoresEachPriorOwner(1)",
    "OpStringCacheScopeOwnsAFreshCacheAndRestoresEachPriorOwner(2)",
    "OpStringFixturesNeitherNeedNorTouchTheSharedCache(0)",
    "OpStringFixturesNeitherNeedNorTouchTheSharedCache(1)",
    "OpStringFixturesNeitherNeedNorTouchTheSharedCache(2)",
    "HLUnityCoreExtensionsFixtureNeitherNeedsNorTouchesTheSharedCache(0)",
    "HLUnityCoreExtensionsFixtureNeitherNeedsNorTouchesTheSharedCache(1)",
    "HLUnityCoreExtensionsFixtureNeitherNeedsNorTouchesTheSharedCache(2)",
    "LocalCloudNotificationsNeverReachEarlierSubscribers",
    "TimeSchedulerSetsADormantConfigurationRowAsideUntouched(False)",
    "TimeSchedulerSetsADormantConfigurationRowAsideUntouched(True)",
    "TimeSchedulerKeepsTheDormantRowAsideWhenItsTemporaryRowIsNotReleased",
    "TimeSchedulerRestoresTheRegistryLayoutAfterCreatingTheTimeManagerRow(False)",
    "TimeSchedulerRestoresTheRegistryLayoutAfterCreatingTheTimeManagerRow(True)",
    "EggmanCleanUpRunsEveryReleaseStepAfterAFailingOne(False)",
    "EggmanCleanUpRunsEveryReleaseStepAfterAFailingOne(True)",
    "FSMPrimitivesNeitherNeedNorChangeEarlierGraphNames",
    "SplineProofDoesNotNeedAPristineLutCache",
))

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
# Shipped Amplify Color types (Assembly-CSharp): declarations, hierarchy and behaviour from the shipped disassembly.
EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalAmplifyColorTests.ShippedTypeListMatches",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.DeclarationsMatchTheShippedAssembly",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.EditorOnlyMembersExistHereAndAreAbsentFromTheShippedTable",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.BaseTypesAndSealingMatchTheShippedHierarchy",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.ComponentAttributesMatchTheShippedMetadata",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.SerializedFieldAttributesAndConstantsMatchTheShippedMetadata",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.EnumValuesMatchTheShippedLiterals",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.ConstructorDefaultsMatchTheShippedConstructor",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.NewSharedInstanceIdIsAFreshGuidString",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.EnteringAVolumeInsertsItFirstAndIgnoresRepeats",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.BlendToStartsAnAnimatedBlendTowardsTheTargetLut",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.LutDimensionsMustBeSquareStripsAndResetAnisotropy",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.ShaderPassCombinesQualityColorSpaceHdrDitheringAndTonemapper(0,0,False,0,False,18)",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.ShaderPassCombinesQualityColorSpaceHdrDitheringAndTonemapper(1,1,False,0,False,1)",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.ShaderPassCombinesQualityColorSpaceHdrDitheringAndTonemapper(1,1,True,1,True,15)",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.ShaderPassCombinesQualityColorSpaceHdrDitheringAndTonemapper(0,0,True,3,False,23)",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.ShaderPassCombinesQualityColorSpaceHdrDitheringAndTonemapper(1,0,True,2,True,8)",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.ShaderPassNeedsTheOwnerCamera",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.VersionInfoReportsOneEightTwo",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.FieldTypesAreRecognisedByFullName",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.ContainerFindsVolumeEffectsByReferenceThenBySharedInstanceId",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.BlendingVolumeEffectsLerpsNumbersAndTakesTheSecondBoolean",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.ComponentFlagsListPublicBlendableFieldsAndEffectsApplyThem",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.AcceptableComponentsDropUnityComponentsButKeepTheDerivedEffect",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.BaseShaderIsFoundWithTheShippedProperties",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.BlendShaderIsFoundWithTheShippedProperties",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.BlendCacheShaderIsFoundWithTheShippedProperties",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.MaskShaderIsFoundWithTheShippedProperties",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.MaskBlendShaderIsFoundWithTheShippedProperties",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.DepthMaskShaderIsFoundWithTheShippedProperties",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.DepthMaskBlendShaderIsFoundWithTheShippedProperties",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.ProcessOnlyShaderIsFoundWithTheShippedProperties",
    "ProjectLucid.Editor.OriginalAmplifyColorTests.RenderMaskShaderIsFoundWithTheShippedProperties",
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
    "ProjectLucid.Editor.OriginalApplicationCoroutineIdentityTests.LoadAssetsMatchesTheOriginalGeneratedMembers",
    "ProjectLucid.Editor.OriginalApplicationCoroutineIdentityTests.UnloadUnusedAssetsMatchesTheOriginalGeneratedMembers",
    "ProjectLucid.Editor.OriginalApplicationCoroutineIdentityTests.RestartSetupMatchesTheOriginalGeneratedMembers",
    "ProjectLucid.Editor.OriginalApplicationCoroutineIdentityTests.InitialiseCharacterMatchesTheOriginalGeneratedMembers",
    "ProjectLucid.Editor.OriginalApplicationCoroutineIdentityTests.LoadLevelEnsureOneLevelMatchesTheOriginalGeneratedMembers",
    "ProjectLucid.Editor.OriginalApplicationCoroutineIdentityTests.LoadLevelSceneMatchesTheOriginalGeneratedMembers",
    "ProjectLucid.Editor.OriginalApplicationCoroutineIdentityTests.LoadOrUnloadSceneMatchesTheOriginalGeneratedMembers",
    "ProjectLucid.Editor.OriginalApplicationCoroutineIdentityTests.LoadThenUnloadSceneMatchesTheOriginalGeneratedMembers",
    "ProjectLucid.Editor.OriginalApplicationCoroutineIdentityTests.MetaGameSetupMatchesTheOriginalGeneratedMembers",
    "ProjectLucid.Editor.OriginalApplicationCoroutineIdentityTests.PlayIntroSequencesMatchesTheOriginalGeneratedMembers",
    "ProjectLucid.Editor.OriginalApplicationCoroutineIdentityTests.UnloadAllLevelsMatchesTheOriginalGeneratedMembers",
    "ProjectLucid.Editor.OriginalApplicationCoroutineIdentityTests.FadeTransitionMatchesTheOriginalGeneratedMembers",
    "ProjectLucid.Editor.OriginalApplicationCoroutineIdentityTests.StartUITransitionMatchesTheOriginalGeneratedMembers",
)

# ApplicationStateFadeTransition and ApplicationStateStartUITransition DoOnEnter closures
# (<>c__DisplayClass4_0 b__0/b__1 x86-64 0x645640/0x645660; <>c__DisplayClass7_0 b__0/b__1 0x654c80/0x654ca0).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalApplicationTransitionCallbackTests.FadeMidpointRunsTheCallbackBeforeRemovingItsParametersAndCompletionPublishesOnce",
    "ProjectLucid.Tests.OriginalApplicationTransitionCallbackTests.StartUIMidpointPublishesAndTimeoutReportsBeforePublishing",
)

EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalTransitionWidgetBinderCorrectionsTests.BinderNodesHideFromTheComponentMenuWithAnEmptyString",
    "ProjectLucid.Editor.OriginalTransitionWidgetBinderCorrectionsTests.BinderOwnersHideFromTheComponentMenuWithAnEmptyString",
    "ProjectLucid.Editor.OriginalTransitionWidgetBinderCorrectionsTests.AbilityTriggeredArgsDefaultToTriggered",
    "ProjectLucid.Editor.OriginalTransitionWidgetBinderCorrectionsTests.SingleToggleSelectsTheIncomingButtonNotTheEqualChild",
)

EDITMODE_TESTS += (
    "ProjectLucid.Editor.OriginalCharacterAbilityBodyOrderTests.AirAbilityEntryNudgeRunsBetweenTheVelocityTestAndTheForwardRead",
    "ProjectLucid.Editor.OriginalCharacterAbilityBodyOrderTests.AirAbilityUpdateRefreshesGravityThroughVectorInequality",
    "ProjectLucid.Editor.OriginalCharacterAbilityBodyOrderTests.EngineVectorInequalityMatchesTheNativeAirGravityGate",
    "ProjectLucid.Editor.OriginalCharacterAbilityBodyOrderTests.AirContinuousCheckReadsTheDefinitionInputBeforeTheAirAbilityHoldSetting",
    "ProjectLucid.Editor.OriginalCharacterAbilityBodyOrderTests.ChaosControlContinuousCheckNegatesOnlyTheAirAbilityHoldSetting",
    "ProjectLucid.Editor.OriginalCharacterAbilityBodyOrderTests.DelegateOnlyBodyIsNotACallOrMoveWitness",
    "ProjectLucid.Editor.OriginalCharacterAbilityBodyOrderTests.HalfPipeEarlyAirSuppressionEndsOnlyTheAirActiveImpulse",
    "ProjectLucid.Editor.OriginalCharacterAbilityBodyOrderTests.TargetedLocationRaycastDividesByOneMagnitudeWithoutNormalizing",
)

EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalCharacterExitTransitionTests.AirAbilityExitLeavesWhenTheCachedAbilityIsMissingOrInactive",
    "ProjectLucid.Tests.OriginalCharacterExitTransitionTests.HalfPipeAirExitLeavesOnDirectionOverrideBeforeReadingStorage",
    "ProjectLucid.Tests.OriginalCharacterExitTransitionTests.HalfPipeTransferExitLeavesOnContactOrDirectionOverrideBeforeReadingTransfer",
    "ProjectLucid.Tests.OriginalCharacterExitTransitionTests.ShouldRespawnReadsBothFlagsBeforeCombiningThem",
)

# Original HalfPipe LeanValue deadzone gate (x86-64 0xE61333 ucomiss / 0xE61339 jae).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalHalfPipeLeanValueTests.PositiveInputAboveTheDeadzoneKeepsItsSignAndSubtractsTheDeadzoneTwice",
    "ProjectLucid.Tests.OriginalHalfPipeLeanValueTests.NegativeInputAboveTheDeadzoneNegatesBeforeSubtractingTheDeadzone",
    "ProjectLucid.Tests.OriginalHalfPipeLeanValueTests.InputEqualToTheDeadzoneReturnsZero",
    "ProjectLucid.Tests.OriginalHalfPipeLeanValueTests.ZeroInputInsideTheDeadzoneReturnsZero",
    "ProjectLucid.Tests.OriginalHalfPipeLeanValueTests.NaNInputFallsThroughToTheArithmetic",
    "ProjectLucid.Tests.OriginalHalfPipeLeanValueTests.NaNDeadzoneFallsThroughToTheArithmetic",
    "ProjectLucid.Tests.OriginalHalfPipeLeanValueTests.NonZeroLeanStartStoresTheInputAndReturnsZero",
    "ProjectLucid.Tests.OriginalHalfPipeLeanValueTests.NaNLeanStartTakesTheDeadzonePathAndIsNotOverwritten",
)

EDITMODE_TESTS += (
    "ProjectLucid.Editor.ProcessAirControlDirectionFlowTests.GroundInputDirectionStoresThePreUpdateDirectionWhileTheVelocityUsesTheUpdatedOne",
    "ProjectLucid.Editor.ProcessAirControlDirectionFlowTests.AirUpdateForwardSpeedTurnsTheRefDirectionForFiniteSteering",
)

# SystemRef<T>.TryGet(out T) reports the untyped validity like 0x06000e20 (#465).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalSystemRefTryGetTests.TryGetReportsTheUntypedSystemAfterAnIncompatibleReplacement",
)



# Character shader serialized contracts and genuine fixture-failure controls (#448).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.CharStandardContractTests.CharStandardKeywordScopesAndPassMasksMatchOriginal",
    "ProjectLucid.Tests.CharStandardContractTests.CharStandardCompilesEveryPassForOpenGLCore",
    "ProjectLucid.Tests.CharStandardContractTests.CharStandardVertexStagesKeepDecodedOutputs",
    "ProjectLucid.Tests.CharStandardContractTests.CharStandardFragmentStagesKeepDecodedFloatOutputs",
    "ProjectLucid.Tests.CharStandardContractTests.CharStandardDebugInputsKeepDeclaredLightmapAndSHFields",
    "ProjectLucid.Tests.CharStandardContractTests.CharStandardAlphaModulateUsesPinnedPreparation",
    "ProjectLucid.Tests.CharStandardContractTests.CharStandardSetupFailureRestoresOwnedResources",
    "ProjectLucid.Tests.CharStandardContractTests.CharStandardBodyFailureRestoresOwnedResources",
    "ProjectLucid.Tests.PreparedMaterialShaderReferenceTests.PreparedCharStandardAmyMaterialResolvesItsOriginalShader",
)

# Project tests that ran without being required, including parameterized cases by exact fullname.
EDITMODE_TESTS += (
    'ProjectLucid.Tests.LocalPropertySaveTests.InvalidPrimaryRecoversBackupAndExplicitSavePreservesCorruption("version")',
    'ProjectLucid.Tests.LocalPropertySaveTests.InvalidPrimaryRecoversBackupAndExplicitSavePreservesCorruption("checksum")',
    'ProjectLucid.Tests.LocalPropertySaveTests.InvalidPrimaryRecoversBackupAndExplicitSavePreservesCorruption("missing-payload")',
    'ProjectLucid.Tests.LocalPropertySaveTests.TwoInvalidEnvelopesReportFinalErrorWithoutErasingEitherCopy("version")',
    'ProjectLucid.Tests.LocalPropertySaveTests.TwoInvalidEnvelopesReportFinalErrorWithoutErasingEitherCopy("checksum")',
    'ProjectLucid.Tests.LocalPropertySaveTests.TwoInvalidEnvelopesReportFinalErrorWithoutErasingEitherCopy("missing-payload")',
    "ProjectLucid.Tests.LocalPropertySaveTests.FailedWritesAreRetriedUpToFiveTimesLikeTheOriginal(1,True)",
    "ProjectLucid.Tests.LocalPropertySaveTests.FailedWritesAreRetriedUpToFiveTimesLikeTheOriginal(5,False)",
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
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.RegistrationCallbackFailureAfterForeignReplacementClaimsNothing",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.RegistrationCallbackFailureAfterPublicationStillReleasesTheOwnedRow",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.RegistrationCallbackFailureBeforePublicationLeavesNothingToRelease",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.ReplacementThatThrowsAfterPublishingStillReleasesTheOwnedRow",
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
    "ProjectLucid.Tests.FSMComponentTests.OriginalFSMComponents_ColdRegistryLeavesNoDefaultManagerRow",
    "ProjectLucid.Tests.FSMComponentTests.OriginalFSMComponents_ColdRegistryFailureAfterAcquisitionLeavesNoDefaultManagerRow",
    "ProjectLucid.Tests.FSMComponentTests.OriginalFSMComponents_ObservedNullRowIsRefusedUntouched",
    "ProjectLucid.Tests.FSMComponentTests.OriginalFSMComponents_BorrowedLiveManagerKeepsOnlyItsOwnMachines",
    "ProjectLucid.Tests.FSMComponentTests.OriginalFSMComponents_BorrowedIdleRowStaysRegisteredAndEmpty",
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

# Mixer fixture ownership when the test runner abandons an iterator (#489).
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.AudioMixerLifecycleTests.AbandonedPublicIteratorCleansUpThroughAttributedTearDown",
    "ProjectLucid.Tests.AudioMixerLifecycleTests.CompletedBodyLeavesCleanupToAttributedTearDown",
    "ProjectLucid.Tests.AudioMixerLifecycleTests.BodyFailureRetainsItsExceptionBeforeAttributedTearDown",
    "ProjectLucid.Tests.AudioMixerLifecycleTests.OverlappingIteratorsCannotReplaceTheActiveFixture",
    "ProjectLucid.Tests.AudioMixerLifecycleTests.TearDownReportsEveryCleanupFailureAfterTheBodyFailure",
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
    "ProjectLucid.Tests.OriginalSplashSelectionTests.VideoAssignmentUsesTheSelectedEntryUpToTheShorterList",
    "ProjectLucid.Tests.OriginalDataDefinitionsLookupTests.GetMergesLaterGroupsIntoFirstAndFaultsOnDuplicateKeys",
    "ProjectLucid.Tests.OriginalDataDefinitionsLookupTests.GetKeepsTheFirstGroupDictionaryWithItsKeyComparer",
    "ProjectLucid.Tests.OriginalDataDefinitionsLookupTests.GetKeepsAnEmptyFirstGroupDictionaryAndItsKeyComparer",
    "ProjectLucid.Tests.OriginalDataDefinitionsLookupTests.GetGroupReturnsFirstClrMatchIncludingDestroyedGroup",
    "ProjectLucid.Tests.OriginalFontReadinessUpdateTests.UpdateReadsAndStoresBothKeysEagerlyFromTheRegisteredApp",
)

# Original cloud facade property-store handler lifecycle and save modification.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.CloudFacadeHandlerTests.ShutdownRemovesSaveHandlerAndLeavesAfterConflictsHandlerSubscribed",
    "ProjectLucid.Tests.CloudFacadeHandlerTests.ModifySaveMutatesOnlySubscribedChangedPropertiesAndConsumesChanges",
)

# Original rolling ability leave reset and trigger getter on a really constructed ability.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.CharacterAbilityRollingTests.LeaveClearsTriggeredAndExitingFlags",
    "ProjectLucid.Tests.CharacterAbilityRollingTests.IsTriggeredReportsTriggeredOrExiting",
    "ProjectLucid.Tests.CharacterAbilityRollingTests.DefinitionStartsWithTheNativeSpeedThresholds",
)

# Original ability interface edges (metadata 0x02000081 -> IAbilityTrigger, 0x02000310 -> IAbility).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalAbilityInterfaceEdgeTests.TriggerableInheritsTheTriggerGetterAndDeclaresOnlyItsTwoContracts",
    "ProjectLucid.Tests.OriginalAbilityInterfaceEdgeTests.HomingAbilityIsAnAbilityAndDeclaresItsEightRowsInMetadataOrder",
    "ProjectLucid.Tests.OriginalAbilityInterfaceEdgeTests.AbilityIsAMemberlessRootInterface",
)

# Original hashed identifiers passed or compared by rolling, knockback and audio-source bodies.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalHashedIdentifierArgumentTests.RollingEntryQueriesTheCharacterRollAction",
    "ProjectLucid.Tests.OriginalHashedIdentifierArgumentTests.KnockbackTransitionLooksUpTheCharacterKnockbackAbility",
    "ProjectLucid.Tests.OriginalHashedIdentifierArgumentTests.AudioSourceEntryRoutesSfxThenAmbientAndOtherwiseMusic",
    "ProjectLucid.Tests.OriginalHashedIdentifierArgumentTests.CharacterActionFlagsAcceptsCharacterRollAndRejectsAnUndeclaredAction",
    "ProjectLucid.Tests.OriginalHashedIdentifierArgumentTests.DelegateOnlyKnockbackBodyIsNotALookupCallWitness",
    "ProjectLucid.Tests.OriginalHashedIdentifierArgumentTests.DelegateOnlyMixerBodyLeavesOnlyTheSourceGuardsInTheRoute",
)

# Original base forward-speed configured and effective minima (x86-64 0x739500).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.CharacterAbilityRollingTests.ForwardSpeedAcceleratesToConfiguredMinimumUnderPartialInput",
    "ProjectLucid.Tests.CharacterAbilityRollingTests.ForwardSpeedCapIsTheModifierAdjustedMinimum",
    "ProjectLucid.Tests.CharacterAbilityRollingTests.ForwardSpeedCurveRatioUsesTheEffectiveMinimum",
    "ProjectLucid.Tests.CharacterAbilityRollingTests.ClampedForwardSpeedStopsAtConfiguredMinimumAndFallsBackBelowTheEffectiveOne",
    "ProjectLucid.Tests.CharacterAbilityRollingTests.ForwardSpeedDecelerationFloorIsTheEffectiveMinimum",
)

# Owned App-row cleanup failure controls; no original-behavior claim.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.CharacterAbilityRollingTests.OwnedFailureAfterAppReferenceCreationReleasesTheEmptyRowAndKeepsThePrimary",
    "ProjectLucid.Tests.CharacterAbilityRollingTests.ObservedAppReferenceIsRetainedAndReportedWithoutReplacingThePrimary",
)

# Original air stomp, braking and burst attack definitions, abilities and FSM nodes (gr2-char-moves-01).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterAirStompBrakingBurstTests." + name for name in (
    "SlopeSettingsAreMinimumInclusiveMaximumExclusiveAndBoostOnlyWhenFlagged",
    "SlopeSettingsFirstMatchingRangeWinsWhenRangesOverlap",
    "SlopeSettingsMatchTheFirstEntryForANaNAngle",
    "SlopeSettingsReportNothingForAnEmptyList",
    "BrakingDefinitionStartsWithTheNativeThresholds",
    "BrakingCachedValuesSquareEachThresholdIntoItsOwnProperty",
    "BrakingCachedValuesFaultWithoutLedgeParametersBeforeSquaring",
    "DeclarationShapeDefinitionsNameTheirOwnAbilityScript",
    "DeclarationShapeBurstGroundLeavesKeepTheirOriginalBases",
    "BrakingTriggerMirrorsTheBrakingFlag",
    "BrakingDeceleratesOnlyWhileBrakingAndWhenDecelerationIsRequested",
    "BrakingMotionMultiplierScalesTheMinimumSpeed",
    "BrakingDecelerationReadsTheBrakeCurveAtTheElapsedBrakeTime",
    "BrakingForwardSpeedOverrideKeepsTheOriginalOptionalDefaults",
    "BrakingUpdateSmoothsTheBufferedIntentBeforeEvaluatingInput",
    "BrakingEvaluateInputMeasuresTheThreeNativeAngles",
    "BrakingEvaluateInputGatesOnInputSpeedAndAngleWithNativePolarity",
    "BrakingForwardSpeedNeverCallsTheMovementBase",
    "AirStompAbilityReadsTheStompActionAndEndsTheStompImpulse",
    "AirStompResetRotatesDownhillAfterTheAirBase",
    "AirStompDownhillRotationCallsTheNativeMathInOrder",
    "NodesCarryTheirOriginalGraphAuthoringAttributes",
    "BurstStatesKeepTheirGenericShape",
    "ClassFactoryRegistersTheNodesWithTheirArgumentTypes",
    "BurstStatesSizeTheOverlapBufferFromMaxTargets",
    "OnlyTheAirChaosStatePreventsDeactivation",
    "LedgeBufferStateSuppressesTheBoostSuspendCountdown",
    "AbilityTransitionsReadTheAbilityTypeFromTheirJson",
    "TriggerAbilityArgumentsDefaultToEnabled",
    "TriggerAbilityStateAppliesTheConfiguredChangeOnStartAndTheOppositeOnEnd",
    "AbilityFinishedTransitionReadsOnlyTheFinishedFlag",
    "AbilityFinishedTransitionIsFalseForAMissingAbilityAndFollowsTheFinishedFlag",
    "AirAbilityTransitionResetsALosingSecondaryBeforeReadingAirActive",
    "LedgeBufferStateRunsAirControlBetweenTheBaseUpdateAndTheTimerWrite",
    "BurstUpdateHitsOverlapsBeforeStrippingGroundVelocity",
    "BurstEntryKeepsHorizontalVelocityThenSpendsBoostThenChaosStamina",
    "ChaosEntryJudgesStaminaBeforeTheBaseEntryAndStartsDilationLast",
    "DisableAbilitiesStateLeavesEveryNonPassiveNonExceptedAbility",
))

# Original climbing, detector and half pipe ability definitions (gr2-char-moves-03).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterClimbDetectorHalfPipeDefinitionTests." + name for name in (
    "EnterClimbingConstructorsStoreTheNativeDefaults",
    "ClimbingLeavesStoreTheirOwnThresholds",
    "SurfaceAngleIsValidInsideTheLimitsWidenedByTheNativeTolerance",
    "SurfaceAngleFollowsTheAuthoredLimits",
    "DetectorRefreshesEverySecondByDefault",
    "DetectorCachedValuesSquareTheProximityDistance",
    "DetectorProximitySquareHasAPrivateSetter",
    "HalfPipeLeavesStoreTheTrackerOffset",
    "HalfPipeAirCachesTheCosineOfTheExclusionAngle",
    "HalfPipeAirCachedValuesFaultWithoutAnExclusionObject",
    "DeclarationShapeConcreteDefinitionsNameTheirOwnAbilityScript",
    "DeclarationShapeDefinitionsKeepTheirOriginalBases",
    "DeclarationShapeFieldsKeepTheirOriginalOrder",
    "DeclarationShapeConcreteDefinitionsAreCreatableFromTheAssetMenu",
))

# Original climbing entry abilities (gr2-char-moves-03).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterClimbingAbilityTests." + name for name in (
    "GroundClimbEntersOnlyWhileTheSquaredSpeedIsBelowTheSquaredThreshold",
    "GroundClimbMeasuresTheSlopeWithTheMovementUtilities",
    "AirClimbEntersWithoutInputWhenTheAngleOfAttackIsAtLeastTheMinimum",
    "AirClimbFallsBackToTheInputHeadingDotTheNormal",
    "AirClimbMinimumAngleOfAttackIsInclusiveAndNaNDoesNotEnter",
    "DirectionOverrideClimbConsumesTheRequestOnlyAfterAnOverrideWasActive",
    "DirectionOverrideClimbRemembersTheOverrideAfterTheBaseUpdate",
    "DirectionOverrideClimbReportsTheOverrideInTheDebugText",
    "SlopeAngleIsOneEightyMinusTheAngleBetweenGravityAndTheNormal",
    "ClimbableSurfaceNeedsACollisionAndASlopeInsideTheSurfaceLimits",
    "EnteringStartsTheTrackerAndLeavingClearsTheTriggerAndStopsIt",
    "DebugTextReportsWhetherClimbingCanStart",
    "UpdateTriggersAndPublishesTheEntryWhenEveryConditionHolds",
    "UpdateWaitsForTheReAttachCooldown",
    "UpdateStopsForAnUnclimbableSurfaceADirectionOverrideOrARefusal",
    "InitialiseBuildsTheTrackerAfterTheBaseAndLooksUpTheTimeManagerLast",
    "UpdateReadsTheTimestampAndTimeBeforeTheDirectionOverrideAndCanEnter",
    "AirClimbMeasuresOneAngleAndNormalisesTheInputHeading",
    "DeclarationShapeEnterClimbingKeepsItsConstraintInterfaceAndLeaves",
))

# Original detector abilities (gr2-char-moves-03).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterDetectorAbilityTests." + name for name in (
    "DetectsEveryIntervalAndRestartsTheTimer",
    "AZeroIntervalNeverDetectsAndNeverMovesTheTimer",
    "ANegativeOrNaNIntervalDetectsOnEveryUpdate",
    "DetectorUpdateRunsTheCharacterBaseFirst",
    "LevelActivationRecordsWhetherTheCharacterIsTheDetectedArchetype",
    "PublishingWithoutAMessageManagerStillRecordsTheMessage",
    "ArchetypeMessagesAreSentUnderTheApp",
    "ArchetypeDetectionIsAnEmptyBody",
    "ArchetypeEntrySubscribesToLevelActivationAndLeavingWithdraws",
    "LightspeedMessageStartsAsALightspeedDashWithNoSurface",
    "TheNearerEndOfTheSplineWithinProximityIsKept",
    "LightspeedMessageTakesTheCachedLocationEvenWithoutAManager",
    "LightspeedMessagesAreSentUnderTheCharacter",
    "DetectionQueriesTheTrackThenTestsTheViewRayThenPublishes",
    "DeclarationShapeDetectorsKeepTheirBasesFieldsAndLambdaNames",
))

# Original half pipe air and transfer abilities and the half pipe air state (gr2-char-moves-03).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterHalfPipeAbilityTests." + name for name in (
    "CachingStoresTheLipAndDividesTheGravityByTheCurrentMotionMultiplier",
    "GravityIsTheCachedGravityScaledByTheMotionMultiplierOfTheVelocity",
    "LeavingClearsTheSpeedAndTheHitTimeOnly",
    "ProjectingWithNothingToHitFailsAndReleasesTheTracingState",
    "GizmoDrawingFollowsTheBaseResult",
    "ProjectToHalfPipeCallOrderMatchesTheNative",
    "AirTimeGrowsAfterTheBaseUpdate",
    "AnEntryFacingTheDefaultNormalKeepsTheDefaultMotion",
    "AFreshEntryPicksTheHighestOverrideTheSpeedHasReached",
    "AChainedEntryReusesTheNextOverrideWithinItsActivationTime",
    "AnEntryOnTheExclusionAngleEndsTheChain",
    "AnEntryWithANaNDotIsNotExcluded",
    "EntryResetsTheAirTimeAndTheLeaveTime",
    "LeavingRecordsTheLeaveTimeAdvancesTheIndexAndStoresTheNormal",
    "LeavingWithoutOverridesLeavesTheIndex",
    "TheFixedUpdateSnapshotCapturesTheLipTheCharacterIsOn",
    "TheGizmoDrawsOnlyWhileThereIsAProjectedVelocity",
    "TextsReportTheAirTimeAndTheLeaveTime",
    "StatesReadTheirAbilityTypesFromTheirJson",
    "TheTransferAbilityTakesTheLipOfTheCharacter",
    "ATransferAbilityMissingFromTheCharacterGivesFalse",
    "StateUpdateFollowsTheNativeOrder",
    "StateUpdateScalesTheSpeedByTheStepAndStoresTheNativeFlagValues",
    "StateEntryProcessesTheTrajectoryBeforeProjectingTheCharacter",
    "StateLeaveRemovesTheColliderKeysThenRecordsTheLanding",
    "GenericHalfPipeStateStoresAndClearsItsGravityDirection",
    "DeclarationShapeHalfPipeTypesKeepTheirBasesAttributesAndMembers",
))

# Original controller axis, controller magnitude, burst exit and form transitions (gr2-char-moves-03).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterControllerFormTransitionTests." + name for name in (
    "BothAxesMustSatisfyTheirComparisons",
    "TheAbsoluteFlagsApplyToTheirOwnAxis",
    "TheRawFlagSelectsTheRawControllerMovement",
    "TheYComparisonIsEvaluatedEvenAfterTheXComparisonFails",
    "ANaNInputFailsAnOrderedComparison",
    "MagnitudeIsComparedWithTheSelectedMagnitude",
    "ANaNMagnitudeFailsOrderedComparisons",
    "ExitFiresWhenTheBurstTimerHasReachedTheDuration",
    "TheFormMatchesOrDiffersAccordingToTheInverseFlag",
    "NodesCarryTheirOriginalGraphAuthoringAttributes",
    "ClassFactoryRegistersTheNodesWithTheirArgumentTypes",
    "DeclarationShapeTransitionsKeepTheirFieldsAndArgumentClasses",
))

# Original modifier, ground boost, ground state and ground and minimum jump time transitions (gr2-char-moves-05).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterModifierDefinitionTests." + name for name in (
    "EveryModifierStartsWithAnEmptyModifyAbilitiesList",
    "CollectableModifiersStoreTheNativePackedDefaults",
    "AttractStoresNoDefaultsOfItsOwn",
    "RingSpeedCurvesRunFlatAtOneAndAreDistinct",
    "RingSpeedTierRingCountMinIsTheFirstTier",
    "TierDefinitionReadsItsSerializedFields",
    "DeclarationShapeDefinitionsNameTheirOwnAbilityScript",
    "DeclarationShapeDefinitionsKeepTheirOriginalBases",
    "DeclarationShapeIl2CppOptionsKeepTheirMetadataOrder",
    "DeclarationShapeDefinitionsAreCreatableFromTheAssetMenu",
    "DeclarationShapeFieldsKeepTheirOriginalOrderAndAuthoring",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterModifierAbilityTests." + name for name in (
    "InitialiseTakesTheCharacterAndTheMatchingDefinition",
    "ModifiesAbilitiesIsTrueOnlyForANonEmptyList",
    "WithoutListedAbilitiesEnterAndLeaveModifyOnceWithNoTarget",
    "RemovingVisitsEachPresentCharacterAbilityInListOrder",
    "AddingListedAbilitiesWaitsForTheFixedUpdateWhereRemovingDoesNot",
    "TheDeferredAddVisitsTheListedAbilitiesWithTheAdd",
    "AddingTakesTheCharactersHandleListForTheAbilityId",
    "EnteringAnAnimationReplacesTheOneRunningAndLeavingClearsIt",
    "RemovingLeavesTheAnimationAndDropsTheHandleList",
    "EnteringLeavesBeforeItEntersTheCharacterAnimation",
    "DamageSetsTheMaximumDamageOfTheDefinitionsType",
    "ReviveSetsTheReviveMultiplierOfTheDefinitionsType",
    "StartSetsTheStartingCountOfTheDefinitionsType",
    "AttractSetsTheTierIndexOnTheListedTypes",
    "AttractWithNoListedTypesAppliesToEveryTypeExceptNone",
    "AttractListensToCollectionsOnlyWhileItHasAnAnimationAndAPositiveTier",
    "AttractPlaysItsAnimationForTheListedTypes",
    "BoostOverridesTheBoostStaminaForItsCollectableType",
    "BoostWithoutAnAnimationStillAnswersTheMultiplier",
    "RingSpeedHooksTheModifiedAbilitysSpeedFunctionAndRecordsTheRingCount",
    "RingSpeedScalesEachSpeedByItsOwnCurveAtTheRingCount",
    "RingSpeedSamplesTheCurvesAtTheCurrentRingCount",
    "RingSpeedPlaysTheFirstNewlyReachedTierAndLeavesBelowTheLowestTier",
    "RingSpeedTierBoundariesAreInclusiveOnTheNewCountAndExclusiveOnTheOld",
    "RingSpeedStopsAtTheFirstNewlyReachedTierWhenAnUpdateCrossesTwo",
    "SwitchAvailableSetsAndClearsTheTargetsSwitchOverride",
    "DeclarationShapeModifierTypesKeepTheirBasesAndMembers",
    "DeclarationShapeLeafAbilitiesKeepTheirBasesFieldsAndOptions",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterGroundBoostTransitionTests." + name for name in (
    "GroundBoostCanTriggerUnlessTheOneShotFlagHolds",
    "GroundBoostInitialiseUpdatesTheBoostStaminaAfterTheBase",
    "GroundTransitionNeedsGroundTrackingAndNoClimbableContact",
    "GroundTransitionRefusesOnlyAClimbableLinkedContact",
    "GroundTransitionKeepsTheTrackingTypeOfItsJson",
    "MinimumJumpTimeFiresOnceTheJumpHasLastedItsMinimum",
    "MinimumJumpTimeReadsAMissingElapsedTimeAsZeroAndStoresIt",
    "MinimumJumpTimeLooksTheJumpUpByItsConfiguredType",
    "MovementClimbingStateReadsItsAbilityTypeFromItsJson",
    "DeclarationShapeNodesCarryTheirOriginalGraphAuthoringAttributesInOrder",
    "DeclarationShapeClassFactoryRegistersTheNodesWithTheirArgumentTypes",
    "DeclarationShapeTransitionsAndAbilityKeepTheirMembers",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterGroundStateTests." + name for name in (
    "LandingHoldRaisesASlowerVelocityToTheRecordedSpeedAlongItsDirection",
    "LandingHoldLeavesAFasterVelocityAlone",
    "LandingHoldEndsOnceTheElapsedTimeExceedsTheWindowButNotAtItsEdge",
    "LandingHoldIgnoresAZeroRecordedSpeed",
    "SlopeSettlingAppliesOnlyInsideTheAngleRangeUnderTheMinimumSpeed",
    "SlopeSettlingUsesTheDefinitionsLimitsWhenItOverridesTheConstants",
    "RemainTimerIsNotCountedWithoutAnyJumpOrBoostAbility",
    "RemainTimerCountsDownAndStopsAtZero",
    "RemainTimerOfZeroLeavesTheQueuedActionsAlone",
    "ExpiredRemainTimerHandsOverTheQueuedBoostAndResetsTheQueue",
    "RemainTimerStillRunningDoesNotHandOverTheQueue",
    "ExpiredRemainTimerKeepsTheQueueWhenNoAbilityTakesIt",
    "QueuedBoostGoesToTheOneShotInsideItsWindowAndToThePlainAbilityAfterwards",
    "QueuedJumpGoesToTheOneShotInsideItsWindowAndToThePlainAbilityAfterwards",
    "RemainTimerStillRunningReroutesAnActiveBoostToTheOneShot",
    "TurnGripCountsDownTheBoostRestoreTimer",
    "TurnGripIsZeroWhileTheGripRestoreTimerRuns",
    "TurnGripIsFullWithoutAUsableAgainstGravityCurve",
    "TurnGripFollowsTheSlopeCurvesAndSteersTheTurnInput",
    "LedgeDetectionRejectsAnUpDirectionOutsideThePlaneLimitBeforeCasting",
    "LedgeDetectionRecordsAWalkableHitAheadAndIsNotALedge",
    "LedgeDetectionTreatsASteepHitAheadAsALedgeWithoutRecording",
    "LedgeDetectionComparesTheStopRayHeightWithTheLedgeLimit",
    "LedgeDetectionRecordsTheCharacterPositionWhenBothRaysMiss",
    "LedgeBrakingDoesNothingForAStationaryCharacter",
    "LedgeBrakingLeavesAMovingCharacterAloneOutsideThePlaneLimit",
    "LedgeBrakingRemovesVelocityAlongTheLedgeInProportionToItsCloseness",
    "DeclarationShapeGroundStateKeepsItsBaseAndConstraints",
))

EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterRailAbilityTests." + name for name in (
    "RailSwitchDefinitionStoresTheNativeDefaults",
    "RailSwitchDefinitionCachesTheSquareOfTheProjectDistance",
    "RailBoostAndStandingDefinitionsStoreTheNativeRailDefaults",
    "DefinitionsNameTheirAbilityTypes",
    "RailBoostCanTriggerUnlessTheOneShotFlagIsSet",
    "RailStandingSetActiveStoresTheTriggeredFlag",
    "RailSwitchKeepsTheNativeSnapTolerance",
    "RailSwitchTargetingSettingsComeFromTheDefinition",
    "ResetClearsTheInputTheTargetAndTheTargetedLocation",
    "ResetWritesTheTurnAroundCooldownOnlyWhenAskedAndPositive",
    "ProjectedDirectionTurnsTheForwardToTheSideOfTheInput",
    "ProjectedDirectionIsRotatedByTheCharacter",
    "SwitchInProgressNeedsANeighbourAndARailTarget",
    "UpdateWithoutAnActiveTriggerAbilityChangesNothing",
    "TheFsmTargetRoundTripsThroughTheStorage",
    "DebugInfoNamesTheMovementAndTheTargetedSurface",
    "TransitionFiresOnlyWhileTheAbilityHasASwitchInProgress",
    "TransitionKeepsItsGraphNodeAttributes",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterRailAndGroundMovementStateTests." + name for name in (
    "LeafStatesReadTheirAbilityTypeFromJson",
    "OrbCollectWithoutAGhostedAnimationNameLeavesItUnset",
    "StatesKeepTheirGraphNodeNames",
    "GenericStatesKeepTheirParametersAndConstraints",
    "BoostStatesDoNotQueueActionsAndTheOthersDo",
    "QueuedJumpGoesToTheOneShotInsideItsWindowAndToThePlainAbilityAfterwards",
    "QueuedBoostGoesToTheOneShotInsideItsWindowAndToThePlainAbilityAfterwards",
    "RemainTimerWithNothingStoredIsSkippedWhenChecked",
    "RemainTimerWithoutAnyJumpOrBoostAbilityDoesNothing",
    "RemainTimerStillRunningCountsDownAndHandsOverOnlyForQueueingStates",
    "ExpiredRemainTimerHandsOverAndResetsTheQueueOnlyWithAnAirAbility",
    "ExpiredRemainTimerHandsTheQueuedJumpOverAndResetsTheQueueOnlyWithAnAirAbility",
    "UnorderedRemainTimerIsStoredAsZeroAndCountsAsRunOut",
    "OrbCollectPicksTheGhostedAnimationForTheReplayOutros",
    "OrbCollectDoesNotCallItsBaseAndHasAnEmptyUpdate",
    "SpinDashChargeLeavesInTheNativeOrder",
    "SpinDashChargeEntersByAnnouncingTheCharge",
    "RailSwitchLeavesByResettingTheAbilityAndRemovingItsValues",
    "GroundBoostEntersByActivatingTheStaminaThenTurningToTheInput",
    "BoostStatesSuspendTheStaminaOnLeaving",
    "RailBoostEntersByActivatingTheStaminaAndTheAbility",
    "RailMovementEntersInTheNativeOrder",
    "RailMovementUpdatesInTheNativeOrder",
    "RollingMovementEndsTheRollAndDefersLedgeBraking",
    "RailMovementSurfaceChangeInformsTheAbilityBeforeStoringTheRailValidity",
    "RailMovementLeaveEndsByDetachingTheAbility",
))

# Original homing attack abilities, running ability and state, and the rolling and setting transitions with their definitions (gr2-char-moves-07).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterHomingAttackDeclarationTests." + name for name in (
    "DefinitionsKeepTheOriginalShape",
    "AbilitiesKeepTheOriginalShape",
    "NodesKeepTheOriginalShape",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterHomingAttackDefinitionTests." + name for name in (
    "ConcreteHomingDefinitionsStartWithTheNativeInitialisers",
    "ConcreteHomingDefinitionsLeaveTheFlagsTimesAndZoneBoxesAtZero",
    "TargetDistanceIsTheDepthOfTheDetectionBox",
    "HasTargetTypeSearchesTheAuthoredList",
    "HasTargetTypeFaultsOnAnUnassignedList",
    "DefinitionsNameTheirOwnAbilityScript",
    "DefinitionsAreCreatableFromTheAssetMenu",
    "GroundHomingDefinitionsAlwaysReportTheirBoostDefinition",
    "RailsHomingDefinitionStartsAtOneEightyDegreesAndOneUnit",
    "RailsHomingCalculateCachedValuesSquaresTheDistanceAndRefreshesTheTargetingCache",
    "FieldsKeepTheirAuthoringAttributesInOrder",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterHomingAttackAbilityTests." + name for name in (
    "TargetZoneSizeAddsTheAdjustmentToTheBoxOfTheCurrentProjection",
    "TargetZoneOffsetFollowsTheCurrentProjection",
    "CameraProjectionFlagIsTheAuthoredOne",
    "SettingTheBoxTransformReplacesOnlyTheDepthAdjustment",
    "ProjectingFromTheCameraPlacesTheBoxAtTheCameraAndStoresTheDepthToTheCharacter",
    "ProjectingFromTheCharactersForwardClearsTheAdjustmentAndUsesItsPose",
    "ProjectingAlongTheInputLooksAtTheIntendedForward",
    "TheOneShotAttackAlwaysPlacesItsBoxAtTheCharacter",
    "ActivatingTheBoxObjectTogglesItsActiveState",
    "TheLockSpeedIsTheVelocityAlongTheTargetClampedBetweenTheMinimumAndMaximum",
    "TheLockSpeedMeasuresTheDotAndAppliesTheMinimumBeforeTheMaximum",
    "TheLockSpeedCanComeFromTheTimeToTheTarget",
    "NothingLocksWithoutAValidActivatedTargetableTarget",
    "ADirectionOverrideNeedsAnOverridingActiveTarget",
    "AComboHitCountsAndRestartsTheBufferTimer",
    "ComboHitsApplyThenUpdateTheTimeSettingAndClearingRemovesIt",
    "TheComboBufferCountsDownAndClearsTheComboWhenItRunsOut",
    "ClearingTheComboResetsTheCount",
    "AddingAValidTargetPoolsItWithItsMetaDataAndRemovingTakesItBack",
    "TheHomingMaskKeepsOutTargetsOnOtherLayers",
    "ATargetIsValidOnlyWhileTheAbilityIsEnabled",
    "AHitCountsTheComboClearsTheActiveTargetAndDisablesTheTarget",
    "WithoutAnActiveTargetNothingIsUpdated",
    "AnActiveTargetWithoutHomingMetaDataIsLeftAlone",
    "AValidTargetInReachStaysActiveAndAdvancesTheClock",
    "ATargetInsideTheDeactivationDistanceIsReleasedWithoutCancelling",
    "ASolidObstructionCancelsTheTargetButATriggerVolumeDoesNot",
    "AStoredLightspeedDashTargetReleasesTheTargetWithoutCancelling",
    "TheTimeoutIsExclusiveAndAnUnorderedTimeCountsAsInside",
    "ATargetThatLeftTheHierarchyIsCancelled",
    "ActivatingNotifiesTheTargetAndRecordsItsTypeAndAResetClock",
    "ActivatingWithoutATargetOnlyResetsTheClock",
    "DeactivatingDropsTheActiveTargetUnlessAOneShotIsActive",
    "TheDebugTextAddsTheBoxDepthAndTheTargetingAngles",
    "TheLightspeedDashAttackNeedsAndConsumesTheDashAction",
    "TheAirTargetAttackNeedsAndConsumesTheHomingAttackPress",
    "TheOneShotWindowsAreExclusive",
    "TheOneShotNeedsAPendingTargetNoActiveTargetAndAStoredTimestamp",
    "ACompletedOneShotPressFiresInsideTheWindowAroundTheClock",
    "AOneShotPressAtTheStoredTimestampIsIgnored",
    "APressExactlyOnTheClockUsesTheAfterWindow",
    "ALockRecordsWhenTheOneShotWasPressed",
    "TheOneShotFlagTracksActivationAndIsRemovedBeforeTheBaseDeactivates",
    "TheOneShotHooksTheHitCallbackOntoEveryTargetItIsGiven",
    "TheRollingAttackIsBlockedByASpinDashChargeAndTriggersOnALock",
    "TheRunningAttackNeedsAQueuedHomingAttackPress",
    "TheRunningAttackConsumesAQueuedPressEvenWithoutALock",
    "TheGroundAttackRejectsATargetNotTargetableFromTheGround",
    "TheGroundAttackAcceleratesWithItsForwardMotion",
    "CallOrderInitialiseSpawnsTheBoxThenHooksItsCallbacksAndDisablesIt",
    "CallOrderCloseDestroysTheBoxAfterTheBase",
    "CallOrderEnteringAndLeavingToggleTheBoxAfterTheBase",
    "CallOrderEnablingTheBoxSwitchesCollisionsBeforeSchedulingTheCallback",
    "CallOrderUpdatingRunsTheBaseThenPlacesTheBoxThenHandlesTheAttack",
    "CallOrderLeavingClearsTheComboLowersTheTriggerAndDropsAnOwnedTarget",
    "CallOrderTheLightspeedDashAttackEndsTheDashImpulseOnEntry",
    "CallOrderTheGroundAttackUpdateFeedsTheBoostStaminaLast",
    "CallOrderTheRunningAttackReadsTheBoostStaminaAfterTheBase",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterTargetingHomingAbilityTests." + name for name in (
    "AFreshAbilityIsNotTriggeredAndNeverOverridesOrProjectsFromTheCamera",
    "WithoutACameraManagerTheCameraProjectionNeedsTheFlagAndNoControllerMovement",
    "CallOrderTheCameraProjectionTestsTheHeadingOverrideBeforeTheInput",
    "TargetingValuesComeFromTheDefinitionOrItsOverride",
    "ActivationFollowsTheCharactersTargetingAvailability",
    "AdditionalSpeedNeedsACurveOfAtLeastTwoKeys",
    "EnteringKeepsTheTriggerOnlyWhileTheAbilityOwnsTheActiveTarget",
    "EnteringAnotherAbilitysActiveTargetDropsTheTrigger",
    "LeavingStartsTheCooldownAndRemovesTheAbilityFromThePool",
    "ResetClearsOnlyAPendingTargetThatIsValidForTheAbility",
    "ResetClearsThePendingTargetOfAnAbilityThatCanActivate",
    "CallOrderTheImmediateTargetNeedsAnActiveCharacterAndNoActiveTarget",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterRollingSettingTransitionTests." + name for name in (
    "RollingFiresWhileTheRollingTriggerDiffersFromTheInverseFlag",
    "RollingIsFalseWithoutTheRollingAbilityEvenWhenInverted",
    "RollingKeepsTheFlagsOfItsJson",
    "RollingClearsTheVelocityOnlyWhenItFiredAndIsAskedTo",
    "RollingToRunningFiresWhileTheRollingExitDiffersFromTheInverseFlag",
    "RollingToRunningReadsAMissingAbilityWithoutATest",
    "RollingToRunningKeepsTheFlagOfItsJson",
    "SettingHomingAttackReadsTheSavedHomingAttackMapping",
    "SettingTransitionsKeepAnOmittedMappingAtNone",
    "CallOrderSettingUpdateComparesTheMappingOfTheCharactersSavedSettings",
    "GroundRunningStateReadsItsAbilityTypeFromItsJson",
    "DeclarationShapeNodesCarryTheirOriginalGraphAuthoringAttributes",
    "DeclarationShapeClassFactoryRegistersTheNodesWithTheirArgumentTypes",
))

# Original deceleration-curve acceleration branch minss operand order.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.CharacterMovementDecelerationCurveTests.AccelerationKeepsAnUnorderedSum",
    "ProjectLucid.Tests.CharacterMovementDecelerationCurveTests.AccelerationClampsAFiniteSumToTheMinimum",
    "ProjectLucid.Tests.CharacterMovementDecelerationCurveTests.AccelerationKeepsAFiniteSumBelowTheMinimum",
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

# Original controller polling notification and live Count reads on an owned provider.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalControllerPollingGapTests.PollingNotifiesOnlyOnCountChangesAndKeepsTheEmptyCountLoop",
)

# Original access-point screen-coordinate width forwarding target.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalGameCenterAccessPointGapTests.ScreenCoordinateWidthForwardsToThePluginWidthSlot",
)

# Original ScreenManager lock flags across device orientations; owned Screen flags restored.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalScreenManagerGapTests.UnlockedToggleWritesLockFlagsFromTheDeviceOrientation",
)

# Original LogOrThrowException without a live logging host.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalLoggingHostGapTests.MissingHostThrowsNullReferenceWithTheSuppliedMessage",
)

# Original visual-quality feature fallback and Apply dispatch.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalVisualQualityGapTests.MissingAttributeSelectsMediumAndAppliesTheSelection",
)

# Original scaled and unscaled timed waits at time scale zero; time scale restored.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalCoroutineTimeSourceGapPlayTests.ZeroTimeScaleHoldsTheScaledWaitButEndsTheUnscaledWait",
)

# Original performance-dependency Hide/Destroy behaviour and OnDestroy handler removal.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalPerformanceDependencyGapTests.HideFollowsValidityAndDestroyKeepsAValidObject",
    "ProjectLucid.Tests.OriginalPerformanceDependencyGapTests.DestroyRemovesOnlyItsOwnProfileAndRegistryHandlers",
)

# Original performance-dependency Destroy behaviour on an owned inactive object.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalPerformanceDependencyDestroyPlayTests.InvalidProfileDestroysTheWholeGameObject",
)

# Original logged failure branches routed through an owned inactive HLUnityCore.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalLoggedFailureBranchTests.RigReplacementLogsUnexpectedInputAndMissingReplacementComponent",
    "ProjectLucid.Tests.OriginalLoggedFailureBranchTests.UnsupportedSplineLogsThenStillNotifiesTheUnchangedOldSelection",
    "ProjectLucid.Tests.OriginalLoggedFailureBranchTests.InvalidTriggerLogsYetSubscribesSwipeAndScrollWheelWhileUpAndHeldUseTheirPairs",
)

# Original LocalisedUIString target discovery on owned inactive text components.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalLocalisedUIStringTargetDiscoveryTests.RightToLeftTextSuppressesUguiDiscoveryAfterEveryTargetIsReset",
    "ProjectLucid.Tests.OriginalLocalisedUIStringTargetDiscoveryTests.PlainTextMeshProUguiIsDiscoveredWithoutRightToLeftText",
)

# Original CharacterCustomisationManager constructor registration order.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalCharacterCustomisationOrderTests.SaveRegistrationIsRequestedBeforeTheStringTableSubscription",
)

# Original FontAssetLanguageSetter handle release and string-handler readiness.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalFontLanguageHandleTests.LanguageChangeReleasesOldHandlesAndReadinessClearsAfterEveryHandleCompletes",
    "ProjectLucid.Tests.OriginalFontLanguageHandleTests.OnlyTheFirstMatchingPairingOfEachCollectionIsLoaded",
)

# Original alert badge text selection on an owned RTLTextMeshPro.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalAlertBadgeTextTests.AlertWinsOverCountAndOnlyCountsAboveTheMaximumUseTheFormat",
)

# Original HLInputDebugMenu steering-assist labels, glyph toggling and comparator.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalHLInputDebugMenuBehaviourTests.SteeringAssistLabelsFollowTheStoredDisabledFlagAndTheSetterStoresItsInverse",
    "ProjectLucid.Tests.OriginalHLInputDebugMenuBehaviourTests.SameVisibleTypeHidesOnlyTheParentWhileOtherRequestsReshowAndClearChildren",
    "ProjectLucid.Tests.OriginalHLInputDebugMenuBehaviourTests.GlyphComparatorOrdersInputsByOrdinalNameNotValue",
)

# Original ToggleCursorVisibility gameplay callback and input-module check.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.ToggleCursorVisibilityGameplayTests.GameplayCallbackIgnoresForceReleaseAndOtherModulesLeaveTheCursor",
)

# Original SameSpaceObjectAvoidance target direction and offset.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.SameSpaceObjectAvoidanceTargetTests.TargetUsesThePositionMinusCentreAngleAndReturnsTheRadialDirection",
)

# Leaderboard challenge callback reporting, through an owned inactive logging host.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalLeaderboardCallbackGapTests.ChallengeCallbackReportsOnlyNonEmptyErrorsRegardlessOfSendFlag",
)

# GameLevels cache publication and linked-test level lookup over owned level graphs.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalGameLevelsGapTests.CacheLevelsPublishesOnlyAfterCompileAndKeepsEntriesFromAFailedPass",
    "ProjectLucid.Tests.OriginalGameLevelsGapTests.GetLevelDelegatesOnlyCycleLevelsAndOtherwiseReturnsTheLinkedScene",
    "ProjectLucid.Tests.OriginalGameLevelsGapTests.GetLevelsRunsFindFirstAndRemovesGuidMatchesFromANewCopy",
)

# Application FSM scene, readiness and audio nodes over owned registry rows and services.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.SceneLoadedSelectsStorageBeforeEitherSceneSource",
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.AppReadyResolvesTheAppReferenceEvenForAnInvalidCommonHandle",
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.GuiReadySelectsStorageByFlagAndChecksUiConfigurationLast",
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.PlayAudioSuppressesOnlyZeroClipForAMissingSource",
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.UnloadScenePresentKeyUnloadsRemovesThenRepublishes",
)

# Application level nodes read the current level's SceneDefinition (+0x18), not LevelDefinition (+0x10).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalApplicationLevelSceneDefinitionTests.EnsureOneLevelKeepsALevelWhoseSceneDefinitionEqualsTheParameters",
    "ProjectLucid.Tests.OriginalApplicationLevelSceneDefinitionTests.EnsureOneLevelUnloadsWhenTheParametersEqualOnlyTheLevelDefinition",
    "ProjectLucid.Tests.OriginalApplicationLevelSceneDefinitionTests.EnsureOneLevelForceUnloadAnyAndNullParametersSelectTheUnload",
    "ProjectLucid.Tests.OriginalApplicationLevelSceneDefinitionTests.LoadLevelProcessKeepsTheTrackManagerWhenTheLastLoadedGuidIsTheSceneGuid",
    "ProjectLucid.Tests.OriginalApplicationLevelSceneDefinitionTests.LoadLevelProcessReplacesTheTrackManagerWhenTheLastLoadedGuidIsOnlyTheLevelGuid",
    "ProjectLucid.Tests.OriginalApplicationLevelSceneDefinitionTests.LoadLevelProcessReplacesAMissingTrackManagerEvenForTheSceneGuid",
    "ProjectLucid.Tests.OriginalApplicationLevelSceneDefinitionTests.IsSameLevelTreatsTheSceneDefinitionAsTheCurrentLevel",
    "ProjectLucid.Tests.OriginalApplicationLevelSceneDefinitionTests.IsSameLevelTreatsAMatchingLevelDefinitionAsADifferentLevel",
    "ProjectLucid.Tests.OriginalApplicationLevelSceneDefinitionTests.IsSameLevelWithoutACurrentLevelReturnsTheNegatedFlag",
)

# LoadAssets offline entry: no platform Social authentication, same GameCenter callback.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.LoadAssetsOfflineNotifiesGameCenterWithoutPlatformAuthentication",
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.LoadAssetsOfflineEntryReportsLoginThenLoadsInTheOriginalOrder",
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.LoadAssetsAuthoredBootAndFastLoadStatesEnterWithoutPlatformAuthentication",
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.LoadAssetsOfflineEntryPropagatesAStoppedLocalLifecycleBeforeDefinitionLoading",
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.GameCenterPageRequestsTakeTheOriginalNotLoggedInPathOffline",
)

# LoadAssets original Authenticate statement behind the offline hook: emission-only source self-check.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.LoadAssetsOriginalGameCenterSourceBranchIsUnchanged",
)

# Borrowed registry rows lose only the typed references cached while borrowed.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.BorrowedSystemRowTypedCacheTests.RemovesOnlyTypedReferencesCachedWhileBorrowed",
    "ProjectLucid.Tests.BorrowedSystemRowTypedCacheTests.ReportsAChangedPriorTypedReferenceWithoutRestoringIt",
)

# DefinitionDataType.GetData callback and insertion order through a recording test group.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalDefinitionDataTypeGapTests.GetDataAsksForTheComparerFirstAndRereadsDataAfterEachKey",
)

# LevelManagerSystems constructor replacement order with a test-only system.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalLevelManagerSystemsGapTests.ConstructorReplacementShutsTheOldSystemDownBeforeCreatingTheNewOne",
)

# LevelManagerSystems ownership-row timing during prefab replacement and shutdown.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalLevelManagerSystemsGapPlayTests.ShutdownCallbacksSeeTheObjectRowOnlyDuringFullTeardown",
)

# InstancedObjectPool re-registration and unknown-key behavior over an owned mesh.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalInstancedObjectPoolGapTests.ReRegistrationKeepsTheFirstPoolAndUnknownKeysAreSilentMisses",
)

# EntityActivationManager registration and activation transitions with recorded logic.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalEntityActivationRegistrationGapTests.RegisterCreatesBothRowsInactiveFirstAndDeregisterRemovesBeforeShutdown",
    "ProjectLucid.Tests.OriginalEntityActivationRegistrationGapTests.ActivateIndexesTheSourceRowWhileDeactivateProbesItAndNeitherRollsBack",
)

# Original preloaded out-handle initialization before lookup/conversion faults (#515).
EDITMODE_TESTS += (
    'ProjectLucid.Tests.AddressableManagerOutHandleTests.LookupInitializesOutputBeforeFallibleWork("null")',
    'ProjectLucid.Tests.AddressableManagerOutHandleTests.LookupInitializesOutputBeforeFallibleWork("invalid")',
    'ProjectLucid.Tests.AddressableManagerOutHandleTests.LookupInitializesOutputBeforeFallibleWork("missing")',
    'ProjectLucid.Tests.AddressableManagerOutHandleTests.LookupInitializesOutputBeforeFallibleWork("cached")',
)

# ManagedAddressableAsset cached-load counting and unload clamping.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalManagedAddressableAssetGapTests.CachedLoadsStillCountAndUnloadClampsBeforeClearingTheAsset",
)

# Low-priority native-grounded gap cases (core, UI, game, spline and physics).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalCoreLowGapTests.NestedStringsHashAndCompareCharByCharThroughRecursion",
    "ProjectLucid.Tests.OriginalCoreLowGapTests.NullTypeFaultsInTheLookupInsteadOfReturningTheDefaultSize",
    "ProjectLucid.Tests.OriginalCoreLowGapTests.FixedWaitReadsFixedDeltaTimeBeforeTheCategoryTimescale",
    "ProjectLucid.Tests.OriginalCoreLowGapTests.ForeignPhotoHashLeavesTheMatchingPhotoInPlace",
    "ProjectLucid.Tests.OriginalCoreLowGapTests.NullConfigurationArrayFaultsInsteadOfReadingAsEmpty",
    "ProjectLucid.Tests.OriginalUILowGapTests.FadeTransitionDefaultsTrueOnARealComponent",
    "ProjectLucid.Tests.OriginalUILowGapTests.DestroyedHostStopFaultKeepsTheRetainedScrollIterator",
    "ProjectLucid.Tests.OriginalUILowGapTests.NullToggleNotificationThrowsTheExactOriginalMessage",
    "ProjectLucid.Tests.OriginalGameLowGapTests.CloseContainerFactoryResolvesTheContainerBeforeTheEvent",
    "ProjectLucid.Tests.OriginalGameLowGapTests.FairPlayCatchLogsTheExceptionBeforeRequestingQuit",
    "ProjectLucid.Tests.OriginalGameLowGapTests.ZeroDistanceConeAdmitsNoPointIncludingItsCenter",
    "ProjectLucid.Tests.OriginalGameLowGapTests.DebugInfoKeepsTheExactHeadersCountsSeparatorsAndEmptyMarker",
    "ProjectLucid.Tests.OriginalGameLowGapTests.DestructorDispatchesTheOverriddenShutdown",
    "ProjectLucid.Tests.OriginalSplinePhysicsLowGapTests.InitialisePublishesTheArrayAndClearsTheCacheBeforeAnEntryFaults",
    "ProjectLucid.Tests.OriginalSplinePhysicsLowGapTests.GrowthChildrenUseTheOriginalOctantSignPattern",
)

# OriginalILCallSequence records only call, callvirt and newobj, never ldftn/ldvirtftn.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalILCallSequenceTests.DelegateCreationAddsNoCallForTheReferencedMethods",
    "ProjectLucid.Tests.OriginalILCallSequenceTests.RealCallsKeepIlOrderAroundAnInterleavedFunctionPointerLoad",
    "ProjectLucid.Tests.OriginalILCallSequenceTests.FindByNameDelegatesDoNotSatisfyTheLookupOrderFilter",
    "ProjectLucid.Tests.OriginalILCallSequenceTests.RealFindByNameCallsSatisfyTheLookupOrderFilterInIlOrder",
    "ProjectLucid.Tests.OriginalILCallSequenceTests.DelegateThenRealCallRecordsOnlyTheCallInstruction",
    "ProjectLucid.Tests.OriginalILCallSequenceTests.ConstrainedCallvirtIsRecordedAfterItsPrefix",
    "ProjectLucid.Tests.OriginalILCallSequenceTests.InstructionsExposeTheArgumentPushedBeforeEachCall",
    "ProjectLucid.Tests.OriginalILCallSequenceTests.SingleOperandReadsTheFloatLiteralBeforeEachCall",
)

# Low-priority gap cases that need real Awake/OnDestroy messages or an active scene object.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalLowGapPlayTests.MacConnectCallbackFindsTheNamedObjectAndSendsTheCommandData",
    "ProjectLucid.Tests.OriginalLowGapPlayTests.DestroyFaultsReleasingAMissingVideoTargetTexture",
    "ProjectLucid.Tests.OriginalLowGapPlayTests.AwakeRegistersBeforeApplyingAndDestroyDetachesBeforeUnregistering",
)

# Discriminating cases for tests the audit judged weak.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OfflineAccessPointTests.FacadeForwardsEachCallToItsOriginalPluginSlotIncludingWidthQuirk",
    "ProjectLucid.Tests.OfflineAccessPointTests.PluginSlotDelegatesWithoutDispatchAreNotForwardingCalls",
    "ProjectLucid.Tests.OriginalMainMenuFSMWeakRemediationTests.AudioClipParsePrecedesSourceParseWhenBothFail",
    "ProjectLucid.Tests.OriginalMainMenuFSMWeakRemediationTests.MainMenuFamiliesKeepOriginalCompleteAttributeRowOrder",
    "ProjectLucid.Tests.EditMode.AbilityLeafDefinitionTests.OriginalRestoreRecordSerializesEveryOriginalFieldInTokenOrder",
    "ProjectLucid.Tests.OriginalTimeScaledUtilitiesPreservationTests.OriginalTimingZeroCategoryScaleLeavesOnlyThePredicateToEndAFrameWait",
    "ProjectLucid.Editor.OriginalUIUtilsWeakRemediationTests.OriginalMonoBehaviourActivationForwardsBothFlagsToItsGameObject",
    "ProjectLucid.Editor.OriginalUIUtilsWeakRemediationTests.OriginalInteractableAndGradientForwardEachArgumentToItsOwnSetter",
    "ProjectLucid.Editor.OriginalUIUtilsWeakRemediationTests.OriginalTextHelpersForwardTextThenColourToTheOwnedTextComponent",
)
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CreatedRow_ReplacedOrExtraTypedEntryIsRetainedWithoutInvokingIt(False)",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.CreatedRow_ReplacedOrExtraTypedEntryIsRetainedWithoutInvokingIt(True)",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.IdleRow_IsBorrowedAndOnlyItsAddedTypedEntryIsRemoved(False)",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.IdleRow_IsBorrowedAndOnlyItsAddedTypedEntryIsRemoved(True)",
    'ProjectLucid.Tests.LifecycleSystemRowLeaseTests.ObservedOrOwnedRow_IsNotBorrowedAndItsWorkIsNotInvoked(False,"startup")',
    'ProjectLucid.Tests.LifecycleSystemRowLeaseTests.ObservedOrOwnedRow_IsNotBorrowedAndItsWorkIsNotInvoked(False,"shutdown")',
    'ProjectLucid.Tests.LifecycleSystemRowLeaseTests.ObservedOrOwnedRow_IsNotBorrowedAndItsWorkIsNotInvoked(False,"deferred")',
    'ProjectLucid.Tests.LifecycleSystemRowLeaseTests.ObservedOrOwnedRow_IsNotBorrowedAndItsWorkIsNotInvoked(False,"system")',
    'ProjectLucid.Tests.LifecycleSystemRowLeaseTests.ObservedOrOwnedRow_IsNotBorrowedAndItsWorkIsNotInvoked(True,"startup")',
    'ProjectLucid.Tests.LifecycleSystemRowLeaseTests.ObservedOrOwnedRow_IsNotBorrowedAndItsWorkIsNotInvoked(True,"shutdown")',
    'ProjectLucid.Tests.LifecycleSystemRowLeaseTests.ObservedOrOwnedRow_IsNotBorrowedAndItsWorkIsNotInvoked(True,"deferred")',
    'ProjectLucid.Tests.LifecycleSystemRowLeaseTests.ObservedOrOwnedRow_IsNotBorrowedAndItsWorkIsNotInvoked(True,"system")',
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.BorrowedRow_GainingUnownedWorkIsRetainedWithoutInvokingIt(False)",
    "ProjectLucid.Tests.LifecycleSystemRowLeaseTests.BorrowedRow_GainingUnownedWorkIsRetainedWithoutInvokingIt(True)",
)

# Native-grounded strengthening of weak original-behavior cases, kept in their own files.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.HLHapticsWeakRemediationTests.EventDataJsonUsesOriginalFieldOrderAndEnumLiterals",
    "ProjectLucid.Tests.HLHapticsWeakRemediationTests.UnavailableTriggerTestsNoneBeforeDebugFlagAndDefinitions",
    "OriginalMenuDataWeakRemediationTests.MenuPageFadeTransitionDefaultsTrueOnOwnedComponent",
    "ProjectLucid.Editor.OriginalHLUIEffectManagerWeakRemediationTests.OriginalEffectManagerSamplesPassRawTimeToLoopingCurves",
    "ProjectLucid.Tests.OriginalHLButtonWeakRemediationTests.InactiveButtonPropertiesRouteToTheirOwnBackingFields",
)

# Restored Hardlight.HLLogHandler (HLUnityCore.Runtime 0x06000a44..59) and its dependencies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLLogHandlerTests." + name for name in (
    "ConstructorCreatesOnlyTheQueueAndAwakeFallsBackToDefaults",
    "AwakeSelectsTheFirstCaseInsensitivePlatformRowAndSizesTheList",
    "MasksFilterTypesAndOnlyTheCurrentAllLogTypesValueAcceptsUnmappedTypes",
    "ShippedOsxSettingsRowDropsUnmappedTypesAndKeepsWarningErrorAssertStacks",
    "ExclusionListsSkipMatchesAndEnableExclusionListChangesTheFirstNamedList",
    "QueueUpdateCountsStackThenMessageLengthsAndRaisesAfterListing",
    "UpdateFlushesAtTheWrappedIntThresholdOnlyWithAutoExport",
    "PauseAndQuitFlushByConfiguredStageIgnoringThePauseArgument",
    "TextMarkdownAndJsonFormatsUseTheNativeLiterals",
    "ExportWritesChainedExtensionNamesAndSwallowsFailures",
    "OnEnableReceivesThreadedLogMessagesUntilOnDisable",
    "DependenciesKeepTheirShippedBehaviour",
))

# Restored Hardlight.HLLogConsole (HLUnityCore.Runtime 0x060009ec..0x06000a27) and its settings asset.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLLogConsoleTests." + name for name in (
    "ConstructorsUseTheShippedInitialisers",
    "ConfigurationCheckDisablesWithoutAssetOrSkin",
    "AwakeCopiesAssetDefaultsAndInitialisesStylesAndContents",
    "GeneralFontSizeUpdateSkipsMessageStylesAndKeepsTheHeaderOffset",
    "OnEnableSizesListsOnceAndSubscribesOnEveryEnable",
    "ReceivedMessagesRouteThroughImportantExtractAndDisplayMasks",
    "DynamicExclusionsCountRepeatsAndCollapseOtherCopies",
    "SortedRebuildClearAndTrimFollowTheirMasks",
    "ShowHideAndPageCountFollowTheShippedState",
    "ImportantMessageCheckOpensTheNextImportantMessage",
    "WindowRectInsetsTheSafeAreaAndClampsThePosition",
    "HelpersUseTheNativeLiteralsAndLimits",
))

# HLUnityCore save-game cluster restored from the 1.10.1 x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalGameCenterSaveGameTests." + name for name in (
    "ParsingKeepsTheWholeJoinAndAdjustsTheDateToUniversal",
    "ParsingRejectsMalformedJoinsAndClearsTheResult",
    "EqualityComparesOnlyTheJoinHashAndFaultsOnDefault",
    "ToStringUsesOriginalLabelsAndCurrentCultureDate",
    "StubCompletesEveryRequestSuccessfullyWithoutDataEvents",
    "FactoriesReturnStubsOnlyWhenForced",
    "UnsupportedServiceSubscribesAppActionsAndFailsEachRequestLazily",
    "UnsupportedServiceReportsThroughABorrowedDormantCoreRow",
    "ParsingUsesTheInvariantCultureNotTheCurrentOne",
    "StubServiceWiresListenerEventsOnlyBetweenAppInitialiseAndShutdown",
    "FetchKeepsOnlyMatchingEventsInTheSharedListAndUnsubscribes",
    "SaveLoadAndDeleteKeepOnlyMatchingEvents",
    "ResolveKeepsOnlyMatchingEventsInTheSharedResolveList",
))

# Spec for Hardlight.FullScreenTransitionController (HLUnityUI.Runtime 0x060000e0..ef).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalFullScreenTransitionControllerTests." + name for name in (
    "OriginalDeclarationsFieldOrderAndClosureShape",
    "ConstructorDefaultsBothDurationsToOneAndAwakeOnlyResetsTheState",
    "HeldTransitionWaitsAtTheMidpointUntilContinued",
    "PositiveOverridesReplaceTheSerializedDurationsAndAreStoredRaw",
    "ContinuousTransitionHalvesTheOverrideAndContinuesAfterTheMidpoint",
    "ContinuousMidpointThatResetsOrContinuesPreemptsTheAutomaticContinue",
    "StartTransitionOutsideHeldOutChangesNothingAndReturnsFalse",
    "StartTransitionFromHeldOutOverwritesBothCachesEvenWithNull",
    "ContinueTransitionOnlyFromHeldInAndAdoptsACompletionOnlyWhenNoneIsCached",
    "ResetTransitionFromAnyStateKeepsSpeedOverridesAndCaches",
    "PrivateStepsRunUnconditionallyFromAnyState",
    "EachLegWritesOnlyItsOwnOverride",
    "CachedCallbacksAreReadAfterTheCompletionEvents",
    "CachedCallbacksRunEvenWhenAListenerChangedTheState",
    "UndeclaredTransitionTypeTakesTheHeldBranch",
    "AnimationCompleteIgnoresUndeclaredStates",
    "ContinuousStoresNegativeAndNaNHalvesRaw",
    "MissingAnimatorFaultsAfterTheStateAndCachesAreWritten",
))

# Restored Hardlight.GreyscaleAndBrightnessModifier (HLUnityUI.Runtime 0x060002e1..e7).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalGreyscaleAndBrightnessModifierTests." + name for name in (
    "KeepsTheOriginalDeclaration",
    "ConstructionDefaultsBrightnessToOne",
    "SettersStoreUnclampedValuesAndDirtyTheGraphicOncePerCall",
    "SettersDirtyTheGraphicEvenWhenTheValueIsUnchanged",
    "SettersWithoutAGraphicOnlyStoreValues",
    "InactiveModifyMeshLeavesTheHelperUntouched",
    "DisabledModifyMeshLeavesTheHelperUntouched",
    "ModifyMeshReadsTheHelperOnlyWhenActive",
    "ActiveModifyMeshRewritesOnlyUV1AndUnsharesTheStream",
    "ActiveModifyMeshWithAnEmptyStreamKeepsUntriangulatedVertices",
))

# Restored Hardlight.HLExclusiveStateGroup (HLUnityUI.Runtime 0x06000633..3d) and HLStateSwitcher.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLExclusiveStateGroupTests." + name for name in (
    "DeclarationsKeepTheShippedAttributesAndFields",
    "DeclarationsKeepTheShippedMethodOrder",
    "SwitcherEnabledRaisesOnlyOnChangeAndStartReplaysTheState",
    "SwitcherEnabledWithoutOnEnabledFallsThroughToOnDisabled",
    "ElementKeyIsTheInstanceIdUntilSetKeyAndTheSetterIsEmpty",
    "ActivateDisablesOthersInOrderThenEnablesTheMatch",
    "DuplicateKeysEnableTheLastMatchAndNoMatchThrowsAfterDisabling",
    "ActivateEnableWithoutOnEnabledFallsThroughToOnDisabled",
    "ActivateDisablesADestroyedSwitcherAndThrowsForAMissingOne",
    "OnEnableIgnoresADestroyedDefaultSwitcher",
    "SetKeysAlsoKeysTheDefaultElementThatOnEnableActivates",
    "SetKeysLogsEachMissingKeyAndSurplusKeysOnce",
    "SetKeysThrowsForNullKeysAndForADefaultWithoutASwitcher",
    "SetKeysReadsTheSwitcherInstanceIdsOnlyForKeyedIndices",
))

# Restored Hardlight.HLScrollRectContentFocus (HLUnityUI.Runtime 0x0600056d..7c).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLScrollRectContentFocusTests." + name for name in (
    "DeclarationsKeepTheDumpedEnumsConstantsAndFieldOrder",
    "ConstructorAllocatesOnlyTheChildListAndFocusStartsDisabled",
    "AwakeFillsMissingReferencesAndCastsTheAssignedScrollRect",
    "AddToStartParentsAtSiblingZeroAndRecordsOnlyWithFocus",
    "AddToStartAtTheLimitEvictsAndReturnsTheEndChild",
    "ChildLimitEvictsOnceWhenTheCountReachesAPositiveLimit",
    "PositionDeltaSeesTheRebuiltChildAndTheUpdatedList",
    "AddToEndUsesTheLastSiblingAndEvictsTheStartChildAtTheLimit",
    "RemoveFromStartAndEndKeepWorldPositionAndRecordOnlyWithFocus",
    "RemoveContentChildReportsTheSearchIndexAndNeverSearchesTheLastNode",
    "InternalCalculatePositionDeltaUsesThePivotShareAndFlipsHorizontal",
    "RefocusAppliesTheOffsetAndPublishesCountersOnlyWhenPendingAndEnabled",
    "RefocusPublishesAfterApplyingAndZeroesEachCounterAfterItsInvoke",
    "AddedChildrenAndTheFlaggedHolderAreLaidOutImmediately",
    "LateUpdateAndOnDisableDispatchRefocusThroughTheVirtualSlot",
    "SetScrollRectNormalisedPositionMovesOnlyAnHLScrollRect",
    "IsChildVisibleToViewportWalksInwardAndRequiresStrictOverlap",
))

# Hardlight.OrientationSpecificRectTransformModifier (HLUnityUI.Runtime 0x0600053f..4d) behaviour specification.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalOrientationSpecificRectTransformModifierTests." + name for name in (
    "SettingTypeLiteralsAndIteratorNameMatchTheOriginal",
    "ConstructorCreatesAnEmptyDesiredListPerComponent",
    "AwakeFetchesTheSiblingModifierOnlyWhenTheFieldIsUnityNull",
    "SetAllValuesRoutesEverySettingTypeToItsModifierSetter",
    "SetAllValuesAppliesEntriesInListOrder",
    "SetAllValuesSkipsUnknownSettingTypesAndContinues",
    "SetAllValuesWithNoEntriesNeverReadsTheModifier",
    "InitialiseValuesSelectsLandscapeOnlyForTheLandscapeOrientation",
    "InitialiseValuesAppendsWithoutClearing",
    "InitialiseValuesWithoutTheSingletonFaultsBeforeWriting",
    "InitialiseValuesStoresTheOrientationBeforeAddingANullArray",
    "InitialiseAndSetValuesInitialisesVirtuallyThenApplies",
    "InitialiseAndSetValuesReappliesStaleEntriesAfterAnOrientationChange",
    "OnEnableSubscribesTheVirtualRefreshAndStartsTheCoroutine",
    "OnEnableWithoutALiveScreenManagerOnlyStartsTheCoroutine",
    "OnEnableStartsTheWaitForRequiredSystemsIterator",
    "OnDisableRemovesOnlyItsOwnSubscription",
    "WaitForRequiredSystemsPollsTheRegistryNotTheSingleton",
    "WaitForRequiredSystemsRefreshesImmediatelyWhenRegistered",
    "WaitForRequiredSystemsDoesNotWaitForTheSingleton",
    "WaitForRequiredSystemsIteratorRejectsReset",
))

# RectTransformModifier (HLUnityUI.Runtime 0x0600002b..62) caching, getters and setter guards.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalRectTransformModifierTests." + name for name in (
    "RectTransformIsCachedLazilyFromTheOwnTransform",
    "GetTransformReplacesOnlyAUnityNullCache",
    "GettersReadTheRectTransformComponents",
    "RightAndTopOffsetSkipWhenTheRawComponentEqualsTheValue",
    "LocalScaleSettersReplaceOneComponent",
))

# Issue #87: Language row admission and unwind controls.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.LanguageLifecycleAdmissionTests.LanguageObserverRow_RejectsWithoutInvokingOrConsuming(False)",
    "ProjectLucid.Tests.LanguageLifecycleAdmissionTests.LanguageObserverRow_RejectsWithoutInvokingOrConsuming(True)",
    "ProjectLucid.Tests.LanguageLifecycleAdmissionTests.LanguageRawKeyWithNullRow_RejectsBeforeReadingIt",
    "ProjectLucid.Tests.LanguageLifecycleAdmissionTests.LanguageCreatedRow_UnwindRetainsFirstCauseOrderAndOwnedRelease(False,False,False)",
    "ProjectLucid.Tests.LanguageLifecycleAdmissionTests.LanguageCreatedRow_UnwindRetainsFirstCauseOrderAndOwnedRelease(True,False,False)",
    "ProjectLucid.Tests.LanguageLifecycleAdmissionTests.LanguageCreatedRow_UnwindRetainsFirstCauseOrderAndOwnedRelease(False,True,False)",
    "ProjectLucid.Tests.LanguageLifecycleAdmissionTests.LanguageCreatedRow_UnwindRetainsFirstCauseOrderAndOwnedRelease(True,True,False)",
    "ProjectLucid.Tests.LanguageLifecycleAdmissionTests.LanguageCreatedRow_UnwindRetainsFirstCauseOrderAndOwnedRelease(False,True,True)",
    "ProjectLucid.Tests.LanguageLifecycleAdmissionTests.LanguageCreatedRow_UnwindRetainsFirstCauseOrderAndOwnedRelease(True,True,True)",
)

# Restored ConditionalOutput and its settings types against the x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ConditionalOutputTests." + name for name in (
    "QueueBeforeInitialiseStoresRawArgumentsWithoutReadingLookupOrCondition",
    "LookupGateAppliesOnlyAfterInitialiseAndFormatsOnlyEnabledConditionedCalls",
    "UnhandledBehaviourReportsAndNeitherQueuesNorFormatsTheMessage",
    "DumpLogQueueDrainsInOrderFormattingEachMessageOnce",
    "ToggleLogInvertsOnNullPersistsByFullNameAndRaisesAfterwards",
    "ToggleLogNullOnUnknownTypeThrowsBeforeWriting",
    "ToggleProofsRefuseBorrowedObserversWithoutRunningThem",
    "ConditionalLogWithConditionForwardsTheLoggerRuntimeType",
    "RetailStrippedGenericBodiesAreNoOps",
    "RetailStrippedInitialiseOnlyLatchesTheInitialisedFlag",
    "DumpLogQueueReplaysEveryFieldWithTheDelayedPrefix",
    "InitialiseLoggerLookupReadsPlayerPrefsOverTheDefaultAndRejectsDuplicates",
    "InitialiseSettingsWithoutConfigurationKeepsTheCurrentSettings",
    "CreatePreInitialiseSettingsUsesTheFirstEnumeratedProviderOnly",
    "StaticDefaultStateReadsAlwaysButWritesOnlyAfterInitialise",
    "SettingsDefaultTrueAndOverridesKeyedByFullName",
    "PreInitialiseSettingsDefaultsAndVirtualReadOrder",
))

# PayloadPropertyBuilder reflection, gating and pending-update bookkeeping.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalPayloadPropertyBuilderTests.ConstructorCreatesOnlyMissingPayloadPropertiesAndLeavesCreatedCollectionsEmpty",
    "ProjectLucid.Tests.OriginalPayloadPropertyBuilderTests.PropertiesIteratorWaitsForTheGateThenAppliesEveryAcceptedEntryInOrder",
    "ProjectLucid.Tests.OriginalPayloadPropertyBuilderTests.PropertiesIteratorClearsMatchingCollectionsOnceBeforeAppending(True)",
    "ProjectLucid.Tests.OriginalPayloadPropertyBuilderTests.PropertiesIteratorClearsMatchingCollectionsOnceBeforeAppending(False)",
    "ProjectLucid.Tests.OriginalPayloadPropertyBuilderTests.SinglePropertyIteratorHonoursItsClearFlagAndReadsThePayloadTypeLast(True)",
    "ProjectLucid.Tests.OriginalPayloadPropertyBuilderTests.SinglePropertyIteratorHonoursItsClearFlagAndReadsThePayloadTypeLast(False)",
    "ProjectLucid.Tests.OriginalPayloadPropertyBuilderTests.SinglePropertyIteratorStillCompletesWhenTheReaderRejectsTheEntry",
    "ProjectLucid.Tests.OriginalPayloadPropertyBuilderTests.ClearCollectionsOfTypeMatchesOpenGenericBasesOfTheElementType",
    "ProjectLucid.Tests.OriginalPayloadPropertyBuilderTests.DuplicateIdentifierRoutesUpdatesToTheFirstRegisteredPropertyOnly",
    "ProjectLucid.Tests.OriginalPayloadPropertyBuilderTests.SingleCleanupKeepsThePendingEntryWhileFullCleanupClearsTheTable",
    "ProjectLucid.Tests.OriginalPayloadPropertyBuilderTests.IteratorsKeepTheOriginalGeneratedNamesAndHoistedParameters",
)

# Original HLUnityCore.Runtime GenericMaths<T> against its fully shared x86-64 bodies.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalGenericMathsTests.SubIntegerImplementationsWrapAndCompareWithTheirOwnSignedness",
    "ProjectLucid.Tests.OriginalGenericMathsTests.WordImplementationsUseUncheckedTruncatingArithmetic",
    "ProjectLucid.Tests.OriginalGenericMathsTests.FloatingComparisonsAreFalseForNaNAndInclusiveOnlyWhereNamed",
    "ProjectLucid.Tests.OriginalGenericMathsTests.DecimalForwardsToTheDecimalOperators",
    "ProjectLucid.Tests.OriginalGenericMathsTests.StringOnlyConcatenatesAndRejectsTheOtherOperationsByName",
    "ProjectLucid.Tests.OriginalGenericMathsTests.UnityVectorsOnlyAddAndSubtract",
    "ProjectLucid.Tests.OriginalGenericMathsTests.SelfImplementingTypesRunOnOneActivatorCreatedInstance",
    "ProjectLucid.Tests.OriginalGenericMathsTests.UnsupportedTypesFailInitialisationWithTheOriginalMessage",
    "ProjectLucid.Tests.OriginalGenericMathsTests.EnumTypesFailInitialisationOnTheImplementationCast",
)

# HLUnityCore LegalModule and CountryRegulationIndexAPI restoration.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalLegalModuleTests.CountryEqualityMixesStringOperatorsAndChecksEveryField",
    "ProjectLucid.Tests.OriginalLegalModuleTests.CountryHashUsesOriginalMultiplierAndNullStringZero",
    "ProjectLucid.Tests.OriginalLegalModuleTests.DataConstructorsKeepOriginalDefaults",
    "ProjectLucid.Tests.OriginalLegalModuleTests.ConstructorValidatesInputsInOriginalOrderBeforeTheRegistry",
    "ProjectLucid.Tests.OriginalLegalModuleTests.ConstructedModuleGuardsPluginAndCountryAccess",
    "ProjectLucid.Tests.OriginalLegalModuleTests.AgeTypeFollowsOriginalThresholds",
    "ProjectLucid.Tests.OriginalLegalModuleTests.OptOutResetsConsentAndAdsThenRevokes",
    "ProjectLucid.Tests.OriginalLegalModuleTests.OptOutAdsRequiresAConsentedAdAgeMatchingThePlayer",
    "ProjectLucid.Tests.OriginalLegalModuleTests.ChangeAgeRevokesOnlyForExactRegulatedTypes",
    "ProjectLucid.Tests.OriginalLegalModuleTests.ConsentChecksUseCachedVersionAndModuleAdType",
    "ProjectLucid.Tests.OriginalLegalModuleTests.DeclarationsKeepOriginalOrdinalsAndAttributes",
    "ProjectLucid.Tests.OriginalLegalModuleTests.RequestAccessFoldsReconsentAndSamplesTracking",
    "ProjectLucid.Tests.OriginalLegalModuleTests.CountrySelectionIgnoresCaseAndFallsBackToDefault",
    "ProjectLucid.Tests.OriginalLegalModuleTests.ImproveExperienceGatesFollowCountryAndAgeClass",
    "ProjectLucid.Tests.OriginalLegalModuleTests.CountryUpdateAndSaveUpgradeChecks",
    "ProjectLucid.Tests.OriginalLegalModuleTests.LegalFlowDispatchesTheOriginalStateTable",
    "ProjectLucid.Tests.OriginalLegalModuleTests.PlayerAgeIsRequestedOnlyWhenUnset",
    "ProjectLucid.Tests.OriginalLegalModuleTests.ConsentAcceptRecordsVersionsAndAdvancesTheFlow",
    "ProjectLucid.Tests.OriginalLegalModuleTests.ConsentRejectionRevokesAndCompletesTheFlow",
    "ProjectLucid.Tests.OriginalLegalModuleTests.AdImprovementConsentAcceptAndCancel",
    "ProjectLucid.Tests.OriginalLegalModuleTests.AdImprovementWithoutAPromptChoosesTheTypeFromTheAgeClass",
    "ProjectLucid.Tests.OriginalLegalModuleTests.AdImprovementConfirmationAcceptAndCancel",
    "ProjectLucid.Tests.OriginalLegalModuleTests.AdTypeSelectionAnnouncesOnlyAChangeFromAPreviousType",
    "ProjectLucid.Tests.OriginalLegalModuleTests.TrackingNotDeterminedWaitsForTheCallback",
    "ProjectLucid.Tests.OriginalLegalModuleTests.LegalDocumentClearRunsInAFinallyEvenWhenTheCallbackThrows",
)

# Original GameCenter listener components, stub save-game listener and MacOS access-point plugin.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalGameCenterListenerComponentTests.ListenerComponentsNameTheirOwningGameObjectOnEveryRead",
    "ProjectLucid.Tests.OriginalGameCenterListenerComponentTests.LeaderboardCompletionsParseBooleansAndReportTheLastParsedHash",
    "ProjectLucid.Tests.OriginalGameCenterListenerComponentTests.LeaderboardParsingUsesTheFirstNonNullHolderUntilClear",
    "ProjectLucid.Tests.OriginalGameCenterListenerComponentTests.LeaderboardRemainingCompletionsReadTheirOwnFieldsAndReportTheHash",
    "ProjectLucid.Tests.OriginalGameCenterListenerComponentTests.LeaderboardImageReadsHeightBeforeWidthAndBuildsTheTextureBeforeTheDataCheck",
    "ProjectLucid.Tests.OriginalGameCenterListenerComponentTests.LeaderboardImageCaseLeavesNoTextureAfterRepeatedOrFailedRuns",
    "ProjectLucid.Tests.OriginalGameCenterListenerComponentTests.LeaderboardEntriesParseThroughTheFirstLocalPlayerHolderUntilClear",
    "ProjectLucid.Tests.OriginalGameCenterListenerComponentTests.SaveGameCallbacksForwardDataUnparsedAndParseCompletions",
    "ProjectLucid.Tests.OriginalGameCenterListenerComponentTests.SaveGameParsedCallbacksForwardTheSavedGameWithOrWithoutTheHash",
    "ProjectLucid.Tests.OriginalGameCenterListenerComponentTests.SaveGameRejectsUnparsableSavedGamesAndConflictNotificationsCarryNoHash",
    "ProjectLucid.Tests.OriginalGameCenterListenerComponentTests.StubSaveGameListenerHasNoNameAndEachTriggerRaisesOnlyItsEvent",
    "ProjectLucid.Tests.OriginalGameCenterListenerComponentTests.MacOSAccessPointFocusIsANoOpThatReportsUnfocused",
    "ProjectLucid.Tests.OriginalGameCenterListenerComponentTests.MacOSAccessPointOverridesForwardToTheirOriginalImports",
    "ProjectLucid.Tests.OriginalGameCenterListenerComponentTests.MacOSAccessPointImportLibraryIsTheInferredInternalEmissionCheck",
)

# Original HLUnityCore.Runtime extension and utility types against their x86-64 bodies.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.ToSnakeSplitsCaseRunsDigitGroupsAndSeparatorsAsShipped",
    "ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.AddSpacesSeparatesWordsAndCapitalRunsButNotAfterSpaces",
    "ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.Format0PlaceholderTimePadsAbsolutePartsWithOneLeadingSign",
    "ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.Format0PlaceholderTimeDoesNotThrowForIntMinValue",
    "ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.SmallStringHelpersKeepTheirNullAndBoundaryOrder",
    "ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.ClassificationAndWhitespaceHelpersMatchTheNativeScans",
    "ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.CharacterFiltersHonourTheirExceptionLists",
    "ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.SubstringEditsReportFailureWithTheOriginalValue",
    "ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.ContainsAtMatchesSequencesFromAnIndexOnStringsAndBuilders",
    "ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.ConditionalAppendsCheckTheConditionBeforeTheBuilder",
    "ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.TypeHelpersFormatAndConstructTypesAsShipped",
    "ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.UpdateOnGetStringUsesTheTableAndCachesUnnamedValues",
    "ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.NullableHelpersReturnDefaultsForEmptyValues",
    "ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.ArrayUtilsResizeInPlaceAndRejectOutOfRangeRemovals",
    "ProjectLucid.Tests.OriginalHLUnityCoreExtensionsTests.GetEscapedEscapesPlusOnlyBeforeTheQueryWhenTheUrlHasSpaces",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.ParentLookupsSkipSelfWhileChildLookupsIncludeSelfAndInactive",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.FindComponentInTreeSearchesDownwardsBeforeWalkingUp",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.NonRecursiveChildComponentsCoverOnlyDirectChildren",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.ChildNameAndTagSearchesIncludeTheSourceAndInactiveChildren",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.EnsureComponentReportsWhetherItAdded",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.SetLayerRecursivelyIgnoresUnknownNamesAndCoversTheSubtree",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.SetChildrenActiveTogglesDirectChildrenOnly",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.SortChildrenByNameUsesOrdinalOrder",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.GetAllChildrenLeafFirstListsEachSubtreeBeforeItsRoot",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.ActionForEachChildAtDepthVisitsTheTargetLevel",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.HierarchySortOrdersByGlobalHierarchyIndex",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.SpawnPrefabInstantiatesUnderTheParent",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.SpawnPrefabAtLocationUsesWorldSpace",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.AddMeshFilterAndRendererTurnsShadowsAndProbesOff",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.CopyComponentCopiesWritablePropertiesBeforeFields",
    "ProjectLucid.Tests.OriginalHLUnityCoreObjectUtilsTests.SceneSearchesReachInactiveObjectsOnlyWhenAsked",
    "ProjectLucid.Tests.OriginalHLUnityCoreAnimationScrollRectExtensionsTests.ResetScrollSnapsEnabledAxesToTheirStart",
    "ProjectLucid.Tests.OriginalHLUnityCoreAnimationScrollRectExtensionsTests.AnimationStopAndResetScrollReturnOnMissingTargets",
    "ProjectLucid.Tests.OriginalHLUnityCoreFileUtilitiesTests.LocalFileUriPrefixDependsOnTheLeadingSlash",
    "ProjectLucid.Tests.OriginalHLUnityCoreFileUtilitiesTests.SanitiseUnixPathFoldsSlashesAndResolvesDotSegments",
    "ProjectLucid.Tests.OriginalHLUnityCoreFileUtilitiesTests.WriteAllLinesWithoutFinalNewLineOmitsOnlyTheLastNewLine",
    "ProjectLucid.Tests.OriginalHLUnityCoreFileUtilitiesTests.WriteAllLinesWithoutFinalNewLineReturnsBeforeCreatingOnMissingInput",
    "ProjectLucid.Tests.OriginalHLUnityCoreFileUtilitiesTests.WriteAllLinesWithoutFinalNewLineReportsThroughABorrowedDormantCoreRow",
    "ProjectLucid.Tests.OriginalHLUnityCoreFileUtilitiesTests.ReadWriteLinesRoundTripsTheCallbackEdits",
    "ProjectLucid.Tests.OriginalHLUnityCoreFileUtilitiesTests.ReadWriteLinesPassesEachPathsLinesInParameterOrder",
    "ProjectLucid.Tests.OriginalHLUnityCoreFileUtilitiesTests.ClearDeletesSubdirectoriesBeforeFilesAndKeepsTheDirectory",
)

# HLUnityCore string/int-range/random-timeout/move/scriptable-object FSM nodes and graph serialisation.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalStringTimeoutFSMTests.CheckIntInRangeKeepsInclusiveSignedBoundsWithoutInsertingMissingValues",
    "ProjectLucid.Tests.OriginalStringTimeoutFSMTests.CheckStringUsesStaticOrdinalEqualityAndStoresMissingValueAsNull",
    "ProjectLucid.Tests.OriginalStringTimeoutFSMTests.StringAndRangeFactoriesKeepOriginalIdentifiersEnumParsingAndJson",
    "ProjectLucid.Tests.OriginalStringTimeoutFSMTests.TimeoutRandomRollsRawRangeThenResetsElapsedAndRemovesBothOnLeave",
    "ProjectLucid.Tests.OriginalStringTimeoutFSMTests.TimeoutRandomUpdateAdvancesBeforeReadingAndStoresMissingTimeoutAsZero",
    "ProjectLucid.Tests.OriginalStringTimeoutFSMTests.TimeoutRandomScopesNamesToTheTransitionAndRoundTripsJson",
    "ProjectLucid.Tests.OriginalStringTimeoutFSMTests.SetStringRegistersBeforeArgsAndWritesCapturedValueOnEnter",
    "ProjectLucid.Tests.OriginalStringTimeoutFSMTests.MoveGameObjectTranslatesInSelfSpaceBeforeRotatingByDelta",
    "ProjectLucid.Tests.OriginalStringTimeoutFSMTests.UsesScriptableObjectResolvesLoadedObjectsByNameAndLogsOnlyRealMisses",
    "ProjectLucid.Tests.OriginalStringTimeoutFSMTests.UsesScriptableObjectKeepsTheFirstLoadedObjectWithAnEqualName",
    "ProjectLucid.Tests.OriginalStringTimeoutFSMTests.GenerateFiniteStateMachineClassWritesShortClassNamesArgumentsAndLinks",
    "ProjectLucid.Tests.OriginalStringTimeoutFSMTests.GenerateMultipleFSMsReusesContainerAndReplacesOnlyTheMachineList",
    "ProjectLucid.Tests.OriginalStringTimeoutFSMTests.GenerateMultipleFSMsWritesTheMachineThenEachDependencyInOrderWithDuplicates",
    "ProjectLucid.Tests.OriginalStringTimeoutFSMTests.GenerateMultipleFSMsWithoutMachineWritesEveryManagerRegisteredFSM",
    "ProjectLucid.Tests.OriginalStringTimeoutFSMTests.SerialiseToFileRejectsBadPathsAndStillOverwritesAfterLoggingExistingFile",
)


# HLUnityCore.Runtime dispatch utilities restored from the 1.10.1 x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDispatchUtilitiesTests." + name for name in (
    "UpdateInfoCatchesUpWithOneRateSizedCallbackPerElapsedPeriod",
    "HandlesAreKeyedBySubscriberAloneAndImmediateUpdatePassesZero",
    "ConstructorSubscribesTheVirtualOnUpdateToTheUpdateAction",
    "AssignConsultsTheComparerWithTheCurrentValueFirst",
    "AcquireReleaseAndForceReleaseFollowTheOriginalCountOrder",
    "LoopWrapperChecksTheBudgetBeforeEachStep",
    "LoopWrapperRejectsUndefinedUpdateTypesBeforeTheStartCallback",
    "SchedulerResumesOnFrameAndTimeWaitsAndRemovesFinishedNodes",
    "SchedulerStepsNewestFirstAndWaitsForFinishedNodes",
    "SchedulerRejectsOtherYieldTypesAfterAdvancingTheFiber",
    "GenericDispatcherQueuesPerDispatcherTypeAndSharesOneReference",
    "StructExchangeKeysHandlesByPayloadTypeAndPublishesDefaults",
    "CompilerGeneratedNamesMatchTheOriginalMemberOrdinals",
))

# Original null scheduler removal preserves all state (#514).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.CoroutineSchedulerNullRemovalTests.NullRemovalPreservesSchedulerAndEveryNode(False)",
    "ProjectLucid.Tests.CoroutineSchedulerNullRemovalTests.NullRemovalPreservesSchedulerAndEveryNode(True)",
)

# HLUnityCore payload, key/value, serialised type reference and debug utility types.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalKeyValueParserTests." + name for name in (
    "DefaultParserSplitsLinesAtTheFirstEqualsAndTrimsKeysAndValues",
    "DisabledTrimKeepsSurroundingWhitespaceOnKeyAndValue",
    "TrimNullValuesDropsOnlyLinesWithoutASeparator",
    "RepeatedKeyKeepsTheLastValue",
    "CustomSeparatorsReplaceTheDefaultsForTheirOwnSplitOnly",
    "FluentSettersReturnTheSameParser",
    "BlankTextAndNullLinesGiveEmptyDictionaries",
    "BuildMetaDataWithoutItsResourceReturnsNullForEveryKey",
    "BuildMetaDataLookupReturnsStoredValuesAndNullForMissingKeys",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalSerialisedTypeReferenceTests." + name for name in (
    "InspectableTypeStoresFullNameWithTheSimpleAssemblyName",
    "InspectableTypeWithoutATypeSerialisesEmptyAndPrintsNone",
    "InspectableTypeResolvesItsNameAfterDeserialisation",
    "InspectableTypeBeforeSerialiseLeavesTheStoredNameUnchanged",
    "SerialisableEnumWritesTheQualifiedTypeAndValueTextOnlyWhenSerialised",
    "SerialisableEnumWithoutAValueSerialisesNulls",
    "SerialisableEnumParsesTheStoredTextBackAfterDeserialisation",
    "SerialisableEnumLogsAndClearsTheValueWhenTheTextDoesNotParse",
    "SerialisableEnumReportsAnUnknownTypeOnlyWhenAValueIsPresent",
    "ImplementsTypeAcceptsOnlyVisibleConcreteAssignableClasses",
    "ImplementsTypeWithAnInterfaceBaseRejectsStructsAndInterfaces",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalPayloadJsonTests." + name for name in (
    "ScalarHelpersReadOnlyMatchingJsonTypes",
    "StringArrayClearsTheExistingListEvenWhenTheArrayIsMissing",
    "StringArrayCreatesTheListOnlyForAConvertedEntry",
    "StringArrayLogsAndSkipsEntriesThatAreNotStrings",
    "ReaderTargetsPayloadJsonAndAcceptsEveryEntry",
    "PopulatorBuildsItsPropertiesAndBindsTheGateToTheOverride",
    "PopulatorGateDefaultsToOpen",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLDebugUtilityTests." + name for name in (
    "FormatMemorySizeSwitchesUnitStrictlyAboveEachBoundary",
    "FormatMemorySizeUsesTheCurrentCulture",
    "ConditionalNamesAndRetailDevelopmentCheck",
    "EveryAssertIsConditionalOnDevelopmentBuilds",
    "PredicateAssertsNeverInvokeTheirPredicate",
    "BoolErrorReportsOnlyAFailedTestAndWarnAndFalseStaySilent",
    "BoolTrueReportsAFailureWithTheContextName",
    "ArrowHeadDirectionTurnsBackByTheHeadAngle",
    "HealthDataUsesTheRetailFallbackOfThreeZeros",
    "ProfileBeginPushesAndEndPopsOnlyWhileEntriesRemain",
))

EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLUnityCoreMiscTests." + name for name in (
    "ClosureAndIteratorNamesFollowTheOriginalMemberOrder",
    "StaticInitialisersKeepTheOriginalBeforeFieldInitFlag",
    "PropertyListAndTimeUtilsKeepTheOriginalTypeFlagsAndStaticValues",
    "BarycentricWeightsVerticesInOrderWithTheNativeTolerance",
    "ContentComparerUsesOrdinalTextAndToleratesNullContent",
    "ManifestMetadataFormatsTheMappedAttributeVerbatim",
    "ObjectWithGuidRoundTripsAndRegeneratesOnlyInMemory",
    "GraphStorageAdjustFillAndRemoveKeepTheNativeArithmeticAndOrder",
    "CustomProfileCyclesLevelsInEnumValueOrderAndAppendsMediumRows",
    "CustomProfileLoadOrCreateAndDeleteTouchOnlyExistingFiles",
    "PackageManifestReadsAuthorAndOptionalDependencies",
    "ColourScopeCapturesFirstAndRestoresAllThree",
    "FirebaseAssetDefaultsAndSilentValidation",
    "BillOfMaterialsValidatesItsUnsetArrayDuringConstruction",
    "ManifestConfigDefaultPathsAreTheShippedLiterals",
    "OptionalParametersKeepTheOriginalDefaultFlags",
))

# Own Il2CppSetOption rows of Game.Runtime 0x02000a09, 0x02000a13 and 0x02000770 (DummyDll metadata order).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalGameRuntimeCheckOptionTests." + name for name in (
    "FSMStateLoaderDisablesArrayBoundsThenNullChecks",
    "FSMStateLoaderIteratorCarriesOnlyCompilerGenerated",
    "MeshGenerationUtilitiesDisablesArrayBoundsThenNullChecks",
    "RigidbodyForceTriggerAndLockRoutineInfoKeepTheirOwnOptionOrders",
))

# Specification of Hardlight.Flip (HLUnityUI.Runtime 0x060002c6..cd) from the x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalFlipTests." + name for name in (
    "DeclarationKeepsTheOriginalAttributesFieldsAndMethodOrder",
    "FreshComponentStartsWithBothFlipsOff",
    "ModifyVerticesMirrorsEachFlaggedAxisAcrossTheVertexBounds(True,False)",
    "ModifyVerticesMirrorsEachFlaggedAxisAcrossTheVertexBounds(False,True)",
    "ModifyVerticesMirrorsEachFlaggedAxisAcrossTheVertexBounds(True,True)",
    "ModifyVerticesLeavesEmptyListsAndUnflaggedVerticesUntouched",
    "ModifyVerticesReadsTheCountBeforeTheFlags",
    "ModifyVerticesKeepsASingleVertexInPlace",
    "ModifyVerticesBoundsFollowTheMinMaxOperandOrderForNaN",
    "ModifyVerticesMinBoundAndYLaneFollowTheOperandOrderForNaN",
    "ModifyVerticesSubtractsFromTheSummedBounds",
    "ModifyMeshRebuildsTheHelperFromTheMirroredTriangleStream",
    "ModifyMeshRunsWithoutAnActiveGuardOrFlagShortcut",
    "SettersWriteOnlyTheirOwnFieldAndDirtyVerticesOnlyOnChange(True)",
    "SettersWriteOnlyTheirOwnFieldAndDirtyVerticesOnlyOnChange(False)",
    "TogglesInvertOnlyTheirOwnFieldAndDirtyVerticesEveryCall(True)",
    "TogglesInvertOnlyTheirOwnFieldAndDirtyVerticesEveryCall(False)",
    "MarkDirtyAndSettersDirtyTheGraphicEvenWhenTheFlipIsDisabled",
    "MutatorsWithoutAGraphicStillWriteTheFields",
))


# Original HLUnityUI.Runtime ScalingTableLayoutGroup specified from its x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalScalingTableLayoutGroupTests." + name for name in (
    "DeclarationKeepsTheShippedSurface",
    "ConstructorSetsTheShippedSerializedDefaults",
    "PropertiesReadAndWriteTheirOwnSerializedFields",
    "SettersMarkTheLayoutForRebuildOnlyWhenTheValueChanges",
    "HorizontalInputSplitsTheWidthAndStretchesTheShortLastRow",
    "HorizontalInputWithZeroMaximumColumnsThrowsBeforeTheDeadGuard",
    "HorizontalInputNarrowsTheTableToFewerCellsThanColumns",
    "HorizontalInputClampsCellsToTheMinimumButReportsTheRectWidth",
    "VerticalFillDividesTheRectHeightAcrossTheTableRows",
    "VerticalOverflowDividesByTheSmallerOfExpectedAndTableRows(2)",
    "VerticalOverflowDividesByTheSmallerOfExpectedAndTableRows(5)",
    "VerticalUnknownRowAddModeDividesByZeroRows",
    "VerticalInputReportsTheClampedRowHeights",
    "SingleRowSkipsTheGuardedVerticalSpacingTerms",
    "SetLayoutHorizontalPlacesCellsFromTheStartCorner(0)",
    "SetLayoutHorizontalPlacesCellsFromTheStartCorner(1)",
    "SetLayoutHorizontalPlacesCellsFromTheStartCorner(2)",
    "SetLayoutHorizontalPlacesCellsFromTheStartCorner(3)",
    "SetLayoutAppliesTheCornerBitTestsToOutOfRangeCorners(-1)",
    "SetLayoutAppliesTheCornerBitTestsToOutOfRangeCorners(4)",
    "SetLayoutAppliesTheCornerBitTestsToOutOfRangeCorners(5)",
    "SetLayoutHorizontalAlignsOverflowingRowsByTheChildAlignment(0)",
    "SetLayoutHorizontalAlignsOverflowingRowsByTheChildAlignment(1)",
    "SetLayoutHorizontalRealignsOnlyWhenTheRowCellCountChanges",
    "SetLayoutHorizontalTakesTheTableShapeFromTheWidthTable",
    "SetLayoutVerticalPlacesRowsFromTheStartCornerAndReleasesTheRowHeights(0)",
    "SetLayoutVerticalPlacesRowsFromTheStartCornerAndReleasesTheRowHeights(1)",
    "SetLayoutVerticalPlacesRowsFromTheStartCornerAndReleasesTheRowHeights(2)",
    "SetLayoutVerticalPlacesRowsFromTheStartCornerAndReleasesTheRowHeights(3)",
    "SetLayoutVerticalAlignsOverflowingRowsByTheChildAlignment",
    "SetLayoutVerticalStopsARowOnlyAtTheExactChildCount",
    "SetLayoutVerticalTakesTheTableShapeFromTheWidthTable",
    "NoLayoutChildrenLeavesTheCachedInputsAndTablesUntouched",
    "OnEnableRunsTheQueuedLayoutRebuildImmediately",
))



# Original HLUnityUI.Runtime Hardlight.Skew behaviour specification from its x86-64 bodies.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalSkewTests.SkewRetainsWholeDeclarationAndOrderedAnnotations",
    "ProjectLucid.Tests.OriginalSkewTests.LerpIteratorRetainsTheOriginalGeneratedShape",
    "ProjectLucid.Tests.OriginalSkewTests.ConstructionDefaultsLerpTimeToTwoTenthsAndEverythingElseToZero",
    "ProjectLucid.Tests.OriginalSkewTests.InstantChangeStoresXAndYWithoutStartingACoroutine",
    "ProjectLucid.Tests.OriginalSkewTests.NonInstantChangeStoresFirstAndThenAsksTheEngineForACoroutine",
    "ProjectLucid.Tests.OriginalSkewTests.NonInstantChangeOnAnActiveOwnerCompletesSynchronouslyWithoutLerping",
    "ProjectLucid.Tests.OriginalSkewTests.EnableWithLerpFlagZeroesBothAxesAndReEnableCachesTheLiveValues",
    "ProjectLucid.Tests.OriginalSkewTests.EnableWithoutLerpFlagLeavesAuthoredSkew",
    "ProjectLucid.Tests.OriginalSkewTests.EnableWithLerpFlagRestoresAtOnceWhenAuthoredXIsNotPositive",
    "ProjectLucid.Tests.OriginalSkewTests.LerpFactoryCapturesOwnerAndArgumentLazily",
    "ProjectLucid.Tests.OriginalSkewTests.ResettingLerpScalesBothAxesByTheClampedTimerAndFinishesOnTheNextStep",
    "ProjectLucid.Tests.OriginalSkewTests.ExternalXAtOrAboveTheCachedValueEndsTheLerpAndRestoresTheCachedPair",
    "ProjectLucid.Tests.OriginalSkewTests.NonResettingLerpFinishesOnTheFirstStepWithoutYielding",
    "ProjectLucid.Tests.OriginalSkewTests.LerpTimerAdvancesByDeltaTimeOverLerpTime",
    "ProjectLucid.Tests.OriginalSkewTests.IteratorResetThrowsAndDisposeDoesNotStopTheLerp",
    "ProjectLucid.Tests.OriginalSkewTests.ModifyVerticesShearsAboutTheStreamWeightedCentre",
    "ProjectLucid.Tests.OriginalSkewTests.DisabledModifyMeshLeavesTheIndexedMeshUntouched",
    "ProjectLucid.Tests.OriginalSkewTests.ActiveModifyMeshRebuildsASkewedTriangleStream",
    "ProjectLucid.Tests.OriginalSkewTests.EnableDirtiesTheGraphicBeforeTheResetLerpZeroesTheSkew",
    "ProjectLucid.Tests.OriginalSkewTests.ModifyVerticesAddsEachEntryOverCountIntoTheCentre",
    "ProjectLucid.Tests.OriginalSkewTests.SkewChangesAndLerpStepsDoNotDirtyTheGraphic",
    "ProjectLucid.Tests.OriginalSkewTests.InactiveOwnerModifyMeshLeavesTheIndexedMeshUntouched",
    "ProjectLucid.Tests.OriginalSkewTests.ZeroLerpTimeStoresAnInfiniteTimerAndFinishesAfterOneYield",
)
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalSkewPlayTests.ZeroTimeScaleHoldsTheResetLerpAtZeroSkew",
)
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutPlayTests.ZeroTimeScaleSeparatesTheScaledAndUnscaledDelta",
)


# Restored UnityEngine.UI.Extensions.TableLayoutGroup (HLUnityUI.Runtime 0x060000a8..bb).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTableLayoutGroupTests." + name for name in (
    "DeclarationKeepsOriginalAttributesFieldsAndMembers",
    "ConstructorSeedsOneColumnOfNinetySixAndFlexibleThirtyTwoRows",
    "PropertiesReadAndWriteTheirOwnFields",
    "HorizontalInputAddsOccupiedWidthsAndOneMinimumSpacingPerColumn",
    "UniformHorizontalInputSharesTheFreeWidthWithTheMinimumAsFloor",
    "UniformHorizontalInputDividesByTheDeclaredColumnCount",
    "FlexibleVerticalInputTakesPerRowMaximaAndKeepsThePreferredHeights",
    "SingleRowVerticalInputAddsNoRowSpacing",
    "ZeroRowVerticalInputAddsNoNegativeRowSpacing",
    "HorizontalLayoutAlignsEachRowOnItsOwnCellsAndMirrorsOddCorners",
    "HorizontalLayoutReusesTheStartOffsetForConsecutiveFullRows",
    "HorizontalLayoutUsesTheSpacingFromTheInputPass",
    "EmptyColumnWidthsFallBackToTwoThreeHundredColumns",
    "VerticalLayoutStacksStoredRowHeightsAndReleasesThem",
    "VerticalLayoutAlignsTheTableHeightIncludingRowSpacing",
    "OnEnableForcesOneCanvasUpdate",
    "EnablingRebuildsTheLayoutWithinTheSameCall",
    "SettersMarkTheLayoutForRebuildOnlyWhenTheValueChanges",
))


# Restored orientation and configuration types of HLUnityUI.Runtime:
# Hardlight.UISystemConfiguration (0x060000c7..dc), Hardlight.NamedUnityEvent (0x060005d1),
# Hardlight.HardlightEnumExtensions (0x060002fe..ff),
# Hardlight.HLOrientationSpecificCanvasScalerSettings (0x06000534..38) and
# Hardlight.OrientationAndNotchSpecificRectTransformModifier (0x06000539..3e).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalUISystemConfigurationTests." + name for name in (
    "DeclarationKeepsTheOriginalConstantsFieldsAndHeaders",
    "ConstructorSeedsTheOriginalDefaults",
    "EachGetterReadsItsOwnField",
    "SceneFolderPathsConcatenateTheirPartsWithoutSeparators",
    "NamedUnityEventKeepsTwoPublicFieldsThatStartNull",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLUnityUIEnumExtensionsTests." + name for name in (
    "DeclarationKeepsTheOriginalStaticClassShape",
    "StaticMapUsesTheUIComparerAndTheOriginalEntryOrder",
    "GetStringReturnsTheCurrentMapEntry",
    "UnmappedValuesFormatAsPaddedHexAndAreCached",
))
# HLModernUI.Runtime widgets, input sliders and the ModernUI enum tables
# (02000037..39, 02000040..45, 0200004a, 0200001b, 0200000e, 0200000b, 0200004f..50).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalModernUIInputSliderTests." + name for name in (
    "ConstructorsSeedTheMaximumAndTheDownTrigger",
    "InputEventsMoveTheSliderByTheSignedClampedMagnitude",
    "DeselectAndClearSelectedDropTheSelection",
    "DelaySelectedSelectsOnlyAfterTheEndOfFrame",
    "EnableSubscribesBothInputsWithTheSerializedTriggerAndDisableRemovesThem",
    "SeekerSubscribesDownAndUpAfterTheBaseAndKeepsThemOnDisable",
    "SeekerHandlersRaiseTheirEventsOnlyWhileSelected",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalModernUIWidgetTests." + name for name in (
    "DebounceDelayIsRebuiltFromTheTimeoutOnEachEnable",
    "DebounceRoutineDisablesTheButtonUntilTheDelayThenRestoresIt",
    "DebounceIgnoresClicksOnAnInactiveOwnerAndDisableWithoutAHandle",
    "DebounceDisableRestoresTheButtonAndKeepsTheStoppedHandle",
    "ToggleSetupStoresTheLabelAndCallback",
    "SetSelectedBlocksOnlySelectingANonInteractableToggle",
    "ToggleForwardsTheRemainingCallsToTheUIToggle",
    "ToggleGroupNavigationAndResetUseTheirOwnFields",
    "TogglePublishesTheEventUnderOnClick",
    "CheckboxSetupAcceptsOnlyCheckboxParameters",
    "LocalisedButtonSetsItsLabelFromTheStringTable",
    "CollapsableTogglesTheContentHeight",
    "HotButtonConstructorDefaultsToNoInputAndTheSharedManagerReference",
    "HotButtonPressesOnlyAnInteractableButton",
    "HotButtonShowsTheGlyphWhileNavigationIsActive",
    "HotButtonEnableSubscribesDownAndAddsTheExclusiveInputOnlyWhenExclusive",
    "HotButtonDisableAlwaysRemovesTheExclusiveInput",
    "BroadcasterPublishesTheEventMatchingTheToggleState",
    "BroadcasterWithoutAnEventNeverCreatesTheExchange",
    "TransitionPlaysBeforeStoringItsCallbacks",
    "TransitionPlaysTheInClipThenTheLambdaPlaysTheOutClip",
    "TransitionReportsTheMidpointAndCompletionInOrder",
    "TransitionResetKeepsAnAssignedAnimation",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalModernUIEnumExtensionsTests." + name for name in (
    "DeclarationsKeepTheOriginalShapes",
    "StaticMapUsesTheModernUIComparerAndTheSelectionEntry",
    "UnmappedValuesFormatAsPaddedHexAndAreCached",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLAutoGeneratedEnumStringTablesTests." + name for name in (
    "HelpersKeepTheOriginalStaticClassShapes",
    "ErrorTypesComparerRegistryKeepsItsOriginalShape",
    "TablesUseTheOriginalComparersAndNativeAddOrder",
    "GetStringReturnsTheLiveTableEntry",
    "MissesFormatAsPaddedUppercaseHexAndAreCached",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLOrientationSpecificCanvasScalerSettingsTests." + name for name in (
    "DeclarationKeepsTheOriginalFieldsAndNestedSettingsClass",
    "SettingsConstructorDefaultsToExpandWithAFullMatch",
    "EvaluateWithoutAScalerReturnsBeforeReadingTheScreenManager",
    "EvaluateWithAScalerButNoScreenManagerThrows",
    "OnlyLandscapeUsesTheLandscapeSettings",
    "EachFlippedEvaluationSwapsTheCurrentReferenceResolutionAgain",
    "FlipFollowsTheSelectedSettingsFlagNotTheOrientation",
    "EvaluateWithNullSelectedSettingsThrowsBeforeWritingTheScaler",
    "OnEnableWithoutALiveScreenManagerDoesNothing",
    "OnEnableSubscribesEvaluateAndEvaluatesImmediately",
    "OnDisableRemovesOnlyThisComponentsListener",
    "OnDisableWithADestroyedScreenManagerDoesNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalOrientationAndNotchSpecificRectTransformModifierTests." + name for name in (
    "DeclarationKeepsTheOriginalBaseFieldsAndAttributes",
    "SetNotchEvaluatorReplacesTheStaticEvaluator",
    "WithoutANotchOnlyTheOrientationSettingsAreAppended",
    "NotchArrayFollowsTheUIAndDeviceOrientationPair",
    "TheEvaluatorRunsAfterTheOrientationSettingsAreAppended",
    "NotchOrientationIsReadAfterTheEvaluatorReturns",
    "AnEmptyNotchArrayAddsNothingAndANullOneThrows",
    "RefreshAppliesTheNotchEntryAfterTheOrientationEntry",
    "OnEnableSubscribesAndRefreshesImmediatelyWithoutACoroutine",
    "OnEnableWithoutAScreenManagerThrowsFromTheRefresh",
    "OnEnableWithADestroyedScreenManagerRefreshesWithoutSubscribing",
    "OnDisableRemovesOnlyThisComponentsListener",
    "OnDisableWithoutALiveScreenManagerDoesNothing",
))


# Original HLUnityUI.Runtime LABColor specified from its x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalLABColorTests." + name for name in (
    "DeclarationKeepsTheOriginalFieldsPropertiesAndMethodOrder",
    "ConstructorAndPropertiesUseTheirOwnChannel",
    "FromColorFollowsTheShippedGammaAndCubeRootBranches(0)",
    "FromColorFollowsTheShippedGammaAndCubeRootBranches(1)",
    "FromColorFollowsTheShippedGammaAndCubeRootBranches(2)",
    "FromColorFollowsTheShippedGammaAndCubeRootBranches(3)",
    "FromColorClampsXyzToTheWhitePointAndZero",
    "ColourConstructorCopiesFromColor",
    "ToColorClampsOnlyNegativeChannelsAndSetsOpaqueAlpha(0)",
    "ToColorClampsOnlyNegativeChannelsAndSetsOpaqueAlpha(1)",
    "ToColorClampsOnlyNegativeChannelsAndSetsOpaqueAlpha(2)",
    "ToColorClampsOnlyNegativeChannelsAndSetsOpaqueAlpha(3)",
    "ToColorClampsOnlyNegativeChannelsAndSetsOpaqueAlpha(4)",
    "LerpClampsTAndReturnsANewColour",
    "ColourLerpInterpolatesInLabSpaceAndDropsAlpha",
    "DistanceIsTheEuclideanDeltaE",
    "ToStringJoinsTheChannelsWithTheirLabels",
    "EqualsComparesChannelValuesAndNaNIsNeverEqual",
    "OperatorsCompareChannelsAndThrowForANullOperand",
    "HashCodeXorsTheChannelHashes",
))


# Original HLUnityUI.Runtime Hardlight.Rotate specified from its x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalRotateTests." + name for name in (
    "DeclarationKeepsTheOriginalAttributesFieldsAndNestedEnum",
    "ConstructionFillsTheShippedIndexTablesAndDefaultsToNone",
    "ModifyVerticesMovesOnlyPositionsThroughTheTable(1)",
    "ModifyVerticesMovesOnlyPositionsThroughTheTable(2)",
    "ModifyVerticesMovesOnlyPositionsThroughTheTable(3)",
    "ModifyVerticesMovesOnlyPositionsThroughTheTable(0)",
    "ModifyVerticesMovesOnlyPositionsThroughTheTable(7)",
    "ModifyVerticesReturnsForAnInactiveEffectOrAnEmptyList",
    "ModifyMeshRebuildsTheHelperFromTheRotatedStream",
    "AnimationPropertiesDirtyTheGraphicVerticesTwice",
))


# Original HLUnityUI.Runtime Hardlight.RaycastMask specified from its x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalRaycastMaskTests." + name for name in (
    "DeclarationKeepsTheOriginalAttributesFieldsAndFilter",
    "EveryPointHitsUntilStartHasRun",
    "SimpleImagesMapThePivotShiftedPointOverTheRectSize",
    "SimpleImagesTruncateNegativeTexelCoordinatesTowardZero",
    "PreserveAspectSamplesTheCentredSquare",
    "SlicedImagesFloorEachBorderBandSeparately",
    "EveryMappingStartsAtTheSpriteTextureRectOrigin",
    "SlicedImagesTakeEachAxisFromItsOwnBorderPair",
    "TiledAndFilledImagesAlwaysHit",
))


# Original HLUnityUI.Runtime Hardlight.GUIBlockerBoxColliderResizer specified from its x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalGUIBlockerBoxColliderResizerTests." + name for name in (
    "DeclarationKeepsTheOriginalAttributesFieldsAndOverrides",
    "EnablingFitsTheColliderToTheTrackedRect",
    "DimensionChangesRefitTheColliderToAnyTrackedRect",
    "MissingReferencesLeaveTheColliderUntouched",
    "DestroyedReferencesLeaveTheColliderUntouched",
))


# Original HLUnityUI.Runtime TriangleGradient specified from its x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTriangleGradientTests." + name for name in (
    "DeclarationKeepsTheOriginalAttributesFieldsAndConstants",
    "ConstructionDefaultsToWhiteUpwardsAndUnitScale",
    "PopulateMeshBuildsTheTriangleFromTheRectOriginAndRecoloursIt",
    "PopulateMeshNegatesTheRectOriginRatherThanUsingTheTopEdge",
    "ApplyGradientOffsetsAndScalesAboutTheMiddleThenLerpsColour32",
    "ApplyGradientStoresEachProjectionInAFreshDistanceArray",
    "ApplyGradientNormalisesOverTheProjectedRangeOnly",
    "ApplyGradientLeavesTheDistancesAloneForAnEmptyList",
))


# Original HLUnityUI.Runtime SonicDash.UI.Touchable specified from its x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTouchableTests." + name for name in (
    "DeclarationIsABareMaskableGraphicOverride",
    "PopulateMeshOnlyClearsTheHelper",
))

# Restored Hardlight.UINavigation (HLUnityUI.Runtime 0x0600037f..8d).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalUINavigationTests." + name for name in (
    "DeclarationKeepsOriginalFieldsFlagsAndAccessors",
    "DefaultNavigationIsAutomaticWithNoTargetsAndNoFlags",
    "SettersWriteOnlyTheirOwnField",
    "EqualsAlsoComparesEachOfTheThreeFlags",
    "EqualsUsesEngineEqualityForTargets",
))

# Restored Hardlight.UIEvent (HLUnityUI.Runtime 0x0600037b..7d).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalUIEventTests." + name for name in (
    "DeclarationIsAFieldlessCreatableAsset",
    "FindByNameReturnsOnlyAUIEventWithThatName",
))

# Restored Hardlight.UISoundEffect (HLUnityUI.Runtime 0x060002f8..fa).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalUISoundEffectTests." + name for name in (
    "DeclarationKeepsOriginalOptionsAndSerializedFields",
    "ConstructionSeedsASemitoneRangeFromMinusTwelveToTwo",
    "AssignedClipPlaysThroughTheSoundManagerAtItsBasePitch",
    "RandomisedClipUsesTheNextSemitoneOffset",
    "RandomisedPlayLeavesTheSharedRandomStateAsFound",
    "ClipWinsOverTheAudioType",
    "AudioTypeWithoutAClipPlaysThroughTheEffectManager",
    "ZeroAudioTypeIsSkippedEvenWithACueForIt",
    "PlayOnlyWhenActiveSkipsAnInactiveComponent",
    "OnEnablePlaysOnlyWithPlayOnAwake",
))

# Restored Hardlight.UISelectable (HLUnityUI.Runtime 0x0600038e..0x060003da).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalUISelectableTests." + name for name in (
    "DeclarationKeepsOriginalAttributesFieldOrderAndGeneratedIterator",
    "ConstructionEnablesInteractionHighlightingAndGroupsButNoNavigation",
    "AddKeepsTheGlobalListUniqueButAppendsToTheSceneList",
    "BlankSceneNamesUseOnlyTheGlobalList",
    "GetSelectablesRegistersAnEmptyListForAnUnknownScene",
    "RemoveTakesOneSceneEntryAndTheGlobalEntry",
    "InteractableSetterSkipsUnchangedValuesAndRespectsTheOverride",
    "InteractableChangeInvokesTheMatchingEventThenReevaluates",
    "ActiveNonInteractableSelectableBecomesDisabled",
    "DisabledSelectableReturnsToNormalWhenInactiveOrInteractable",
    "DifferingStoredStatesTransitionWithoutAChange",
    "OnEnableEvaluatesInstantlyAndRegistersAndOnDisableUndoesIt",
    "NonPointerEventsUsePointerInsideWithoutAConfiguration",
    "PointerHighlightNeedsNavigationValidityWhenSelectionIsHighlighted",
    "PointerEventsRequireTheConfiguration",
    "PointerDownHonoursOnlyTheLeftButton",
    "PointerDownOnANonInteractableSelectableSkipsTheBridgeAndDisables",
    "PointerEnterAndExitNeedAVisibleCursorAndCanRaiseNavigationEvents",
    "InvokeEventRunsTheEntryEventBeforeTheTypeSound",
    "MissingEventEntryStillPlaysTheTypeSoundWithIdZero",
    "GetEventInfoStoresAMissingEntryOnlyWhenAsked",
    "SetEventHandlerReplacesTheHandlerOfTheFirstMatchingEntry",
    "SetNavigationActionListensOnTheNavigateTowardEntry",
    "ExplicitNavigationReturnsEachDirectionTargetUnchecked",
    "AutomaticNavigationSearchesOnlyTheModeAxes",
    "MoveNavigatesOnlyAfterFindingATarget",
    "GetPointOnRectEdgeScalesByTheLargerComponent",
    "MaterialSwapCachesStartMaterialsAndSwapsOnDisabled",
    "CanvasGroupSearchStopsAtIgnoreParentGroups",
    "TransformSettersReevaluateOnlyOnChangeAndHighlightParentFallsBack",
    "IsSelectedAsksTheControllerWhenSelectionIsHighlighted",
    "IsSelectedAsksTheBridgeOnlyWithACurrentEventSystem",
    "PointerHighlightComparesThePressedObject",
    "HighlightObjectFollowsTheHighlightWithoutSelectionHighlighting",
    "SelectNeedsACurrentEventSystemButDeselectDoesNot",
    "NavigationSearchesUseTheRotatedDirection",
))

# Restored Hardlight.UISelectableController (HLUnityUI.Runtime 0x060003db..0x06000407).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalUISelectableControllerTests." + name for name in (
    "DeclarationKeepsOriginalFieldsAndGeneratedNames",
    "ConstructionSeedsTransitionTimeWeightingAndUnsupportedInput",
    "TrackRecentlySubmittedKeepsTheFiveNewestAndCopiesTheCurrentSelection",
    "ExclusiveScenesAreRegisteredOnceAndRemovedOnDemand",
    "ActivationForASelectionInputEnablesAndHidesTheCursor",
    "EnablingSelectsTheBestCandidateBelowTheRecoveryOrigin",
    "ActivationWithoutASelectionInputStaysDisabled",
    "InputTypeChangesAreIgnoredWhileInactiveOrRepeated",
    "InputTypeChangesToggleTheEnabledSystem",
    "DeactivateSystemOnlyActsWhileActive",
    "DisabledSystemRecordsAnySelectionWithoutValidation",
    "EnabledSelectionDeselectsThePreviousAndAnnouncesTheNew",
    "EnabledSelectionIgnoresAnInvalidSelectable",
    "DeselectWithNothingSelectedOnlyClearsTheLastKnown",
    "IsValidSelectableForNavigationRejectsEachFailingCondition",
    "FindSelectablePrefersAlignmentOverDistance",
    "FindSelectableSkipsTheCallerAndCandidatesNotAhead",
    "ExclusiveSceneLimitsTheSearchToItsOwnList",
    "FindSelectableFromATransformStartsAtItsRectEdge",
    "TryGetCachedSelectablePrefersLastKnownThenNewestValidSubmission",
    "TryGetCachedSelectableFallsBackToADownwardSearchFromTheRecoveryOrigin",
    "AppReloadingResetsTheBlockDelegateAndDisables",
    "RegisteredControllerWeightsSameSceneCandidatesAndKeepsTheScaledBest",
    "RegisteredControllerBlockReturnsNullUnlessTheOverrideListIsUsed",
    "AnyCornersVisibleMapsTheCornersAsScreenOrWorldPoints",
    "RegisteredControllerPassesItsBlockFlagToTheCornerTest",
    "MoveToNewParentInstantStepChoosesTheStartScale",
    "SpawnReparentsTheCachedHighlighterKeepingItsLocalPosition",
    "DeactivationLeavesTheHighlighterOnlyWhileBeingDestroyed",
    "RemoveHighlighterParksItAndReenablesRequiredBehaviours",
    "DisabledSystemStoresANonUISelectableAsNullButSelectsItsObject",
    "AppReloadingDeselectsTheCurrentSelectionAfterShowingTheCursor",
))

# Original HLUnityUI.Runtime UISelectableInputListener against its x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalUISelectableInputListenerTests." + name for name in (
    "DeclarationsKeepOriginalFlagsSignaturesAndAttributeRows",
    "InactiveListenerReturnsSerializedArrayAndStartsWithEmptyDelegateList",
    "SelectedButtonInputDefaultsGiveNullTextWhateverTheStringId",
    "SelectedButtonInputKeepsFirstProviderAndReadsItsTextEachTime",
    "ButtonSelectedAppendsClosuresAndRegistersTheDelegateAtTheEntryIndex",
    "ClosuresRereadTheEntrySupplierAndFireEveryMatchingEntryInOrder",
    "TriggeredButtonsArePressedBeforeTheirEntryEvent",
    "ButtonDeselectedUnregistersOnlyTheFirstEntryCountThenClearsInPlace",
    "SelectAndDeselectIgnoreAnotherSelectable",
    "SelectAndDeselectMatchOnlyTheWatchedSelectable",
    "EnableAndDisableOrderTheShutdownHookAndDeselectAroundAnUnregisteredController",
))

# Original HLUnityUI.Runtime UIClickable and UIButton against their x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalUIClickableTests." + name for name in (
    "ClickableDeclarationsKeepOriginalFlagsFieldsAndIteratorNames",
    "ButtonDeclarationsKeepOriginalFlagsFieldsAndAttributeRows",
    "ConstructorsSeedCooldownListAndExitFlagAndAccessorsUseTheirFields",
    "AcceptedPressIsRefusedOnlyDuringTheDisabledShakeAndStartsNoCooldownWhenInactive",
    "RejectedPressClearsTheClickWithoutAnimatingWhenAnimationIsDisabled",
    "RejectedPressDuringTheShakeRestartsItOnlyWhenItsCoroutineIsHeld",
    "ButtonPressReportsClickOrDisabledClickByInteractability",
    "ActionsPressBehindActivityAndRaiseButtonEventsOnlyWhenInteractable",
    "EventPropertiesAreViewsOntoTheirEventTableEntries",
    "PointerClickPressesOnlyForMouseOrFirstTouchUnlessClickthroughIsAllowed",
    "SubmitAlwaysPresses",
    "PointerClickClearsTheAcceptedClickWhileSubmitTransitionsOnlyWhenInteractable",
    "PointerDownRaisesButtonDownWhileHeldForAnyButton",
    "PointerUpDecidesButtonUpBeforeReleasingAndClearsTheExitMemory",
    "PointerExitRemembersAHeldExitOrReleasesItAfterTheBase",
    "PointerEnterReportsButtonDownForASelectedHeldReentryWhateverTheCursor",
    "ButtonDisabledWhileHighlightedReportsDisabledSelectedAfterTheBaseDisable",
    "PublishedMessagesCarryTheEventUnderTheirUIMessageType",
    "StateTransitionsStopAnimationsOnlyOnTheOriginalEdges",
    "StateTransitionsApplyTheSelectableMaterialSwap",
    "TransformCacheIsTakenDirectlyButRestoredAndRefreshedOnlyInPlayMode",
    "PressAndReleaseAnimationsStepOnceBeforeTheirFirstCheck(True,True)",
    "PressAndReleaseAnimationsStepOnceBeforeTheirFirstCheck(True,False)",
    "PressAndReleaseAnimationsStepOnceBeforeTheirFirstCheck(False,True)",
    "PressAndReleaseAnimationsStepOnceBeforeTheirFirstCheck(False,False)",
    "DisabledShakeOffsetsTheCachedPositionAndRestartsAfterAYield",
    "GlobalCooldownSetsTheFlagAcrossOneRealTimeWait",
    "StatusEffectorsRegisterOnceAndAreAppliedOnRegistration",
    "StatusEffectorsFollowTheInteractableFieldOnlyOnceRegistered",
))

# Original HLUnityUI.Runtime HLTappable against its x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLTappableTests." + name for name in (
    "DeclarationsKeepOriginalFlagsFieldsAndAttributeRows",
    "ConstructorSeedsTheEventAndThresholdAndTheAccessorsUseTheField",
    "PointerDownRecordsTheStartTimeOnly",
    "PointerUpTapsOnlyWhenStrictlyShorterThanTheThreshold",
))


# Original virtual clear-state dispatch and real input-bridge deselection (#482).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLSelectableClearStateTests." + name for name in (
    "InstantClearStateClearsBothPointerFlagsThenDeselects",
    "DisablingASelectedControlWithAnActiveCompanionHandlerClearsSelectionThroughTheBridge",
    "DisablingTheSoleSelectedHandlerRequestsDeselectionButRetainsTheModuleGuard",
))

# Compiler-generated HLSelectable iterator and HLSelectableInputListener closure (HLUnityUI.Runtime).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLSelectableGeneratedMembersTests." + name for name in (
    "WaitForRootObjectsStateMachineKeepsTheShippingName",
    "ButtonSelectedClosureKeepsTheShippingDisplayClassName",
    "WaitForRootObjectsIteratorEmissionShape",
))
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalHLSelectableScenePlayTests.WaitForRootObjectsYieldsOnceThenRecordsTheSceneAndRegisters",
)


# Compiler-generated closures and iterators of HTTPRequestBehaviour (HLNetworking.Runtime),
# BaseControllerProvider (HLInput.Runtime) and AnalyticsSystem (HLAnalytics.Runtime).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalSmallLibsGeneratedMembersTests." + name for name in (
    "HTTPRequestBehaviourClosureAndIteratorKeepTheShippingLayout",
    "BaseControllerProviderPollingIteratorKeepsTheShippingLayout",
    "AnalyticsSystemClosureAndIteratorsKeepTheShippingLayout",
))


# Restored HLUnityUI.Runtime recycling and scroll helpers: Hardlight.HLRecyclingScrollRect`2
# (0x06000653..67e), HLHorizontal/HLVerticalRecyclingScrollRect`2, HLRecyclingScrollRectElement`1,
# Hardlight.HLSimpleScroll, HLScrollRefreshWidget and HLParentScrollRectBlocker.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLRecyclingScrollRectTests." + name for name in (
    "DeclarationKeepsTheDumpedConstraintFieldsAndGeneratedNames",
    "ElementDeclarationAndDataTypeIsTheClosedTypeArgument",
    "ConstructorSeedsTheRingStateAndDefaults",
    "LoopingIndexHelpersKeepTheirOwnWrapRules",
    "AdjustSizeCountsTheResizeOffsetAndNeverNegativeSpacing",
    "PositionFromEntryIndexStepsByElementAndSpacing",
    "IndexOfFirstEntryCeilsTheScrolledStepsAndClamps",
    "SetupContentFloorsTheVisibleCountAndAddsAtTheEnd",
    "SetupContentDividesByTheElementAndSpacingStep",
    "SetupContentSplitsWithAnOffByOneEndShare",
    "SetupContentRemovesSurplusObjects",
    "RemoveObjectsTakesTheRingEndBeforeTheFirstObject",
    "RemoveObjectsWithNegativeFirstObjectThrowsAfterTrimmingTheEnd",
    "AddNewObjectsToEndInsertsAtTheFirstObjectSlot",
    "AddNewObjectsToEndRestartsANegativeFirstObjectAtZero",
    "InstantiateNewObjectRenamesInsertsInitialisesAndPositions",
    "BaseInstantiateParentsTheCloneAndBaseDestroyUsesObjectDestroy",
    "MoveFromStartToEndReassignsTheLeadingObjects",
    "MoveFromStartToEndJumpsPastAWholeRing",
    "NonPositiveMovesOnlyRewriteTheIndicesWhenMovingToTheStart",
    "MoveFromEndToStartReassignsFromTheNewFirstObject",
    "PositionChangedMovesTowardsTheTargetEntryAndNotifiesEveryObject",
    "PositionChangedCapsForwardMovesAndMovesBackFully",
    "PositionChangedNotifiesEveryListedObjectWhateverTheObjectCount",
    "ReloadAllDataWrapsEntriesAndObjectsIndependently",
    "ClearDestroysLastToFirstAndRestoresTheConstructorIndices",
    "SetEntriesAlwaysResetsTheScrollAfterRemovingObjects",
    "SetEntriesRefreshesTheViewBeforeReloadingTheData",
    "InactiveSetupAtEndOfFrameStartsNothingAndLeavesTheFlagClear",
    "SetupCoroutineResizesWaitsTwoFramesThenBuilds",
    "OnDisableNeverRemovesTheValueChangedListener",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLDirectionalRecyclingScrollRectTests." + name for name in (
    "DeclarationsKeepTheDumpedAlignmentFieldAndCentreDefault",
    "HorizontalSizesReadWidthsAndLeftRightPadding",
    "VerticalSizesReadHeightsAndTopBottomPadding",
    "ContentSizeSetterSizesBothContentsAlongTheAxis",
    "HorizontalChildPositionUsesTheVerticalAlignment",
    "VerticalChildPositionNegatesThePositionAndUsesTheHorizontalAlignment",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLParentScrollRectBlockerTests." + name for name in (
    "DeclarationKeepsTheDumpedInterfacesFieldsAndDisplayClass",
    "PointerTrackingFollowsTheFirstPointerUntilItsDragEnds",
    "PotentialDragReachesOnlyTheFirstParentHandlerForTheLeftButton",
    "PotentialDragSkipsAncestorsWithoutAHandler",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLScrollRefreshWidgetTests." + name for name in (
    "DeclarationKeepsTheDumpedFieldsNestedEventAndDefaults",
    "OnDragStoresTheClampedThresholdProgress",
    "OnDragScrubsThePullAnimation",
    "OnEndDragBelowTheActivationPercentageCancels",
    "OnEndDragAtTheActivationPercentageRefreshesUntilTheCallback",
    "OnEndDragWithNaNProgressCancels",
    "AwakeAndOnDestroySubscribeToTheScrollRectDragEvents",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLSimpleScrollTests." + name for name in (
    "DeclarationKeepsTheDumpedFieldsDefaultsAndIterator",
    "ScrollEventsArmTheInputScroll",
    "InputScrollMovesTheSingleEnabledAxisByThePixelStep",
    "LerpScrollStartsTheIteratorInsteadOfScrolling",
    "InputScrollWithBothOrNeitherAxisDoesNotMoveButDisarms",
    "TooSmallContentOrAnInactiveRectLeavesTheScrollArmed",
    "ClampedInputScrollUsesTheCachedContentBounds",
    "ClearUpLeavesShutdownAndUnregistersBothInputs",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLSimpleScrollPlayTests." + name for name in (
    "LerpAddsTheGrowthOfTheLerpEachFrameAndStopsShort",
    "PositiveValueSubtractsNegativeSteps",
    "NegativeValueAddsItsSteps",
    "InactiveRectEndsTheLerpAndLeavesTheScrollArmed",
    "PositiveValueSubtractsANegativeHorizontalStep",
    "LerpInputReplacesTheRunningLerp",
    "SetupSubscribesShutdownAndRegistersBothInputsOnPress",
    "SetupWithADestroyedConfigurationOnlySubscribesShutdown",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLRecyclingScrollRectPlayTests." + name for name in (
    "SizeChangesMarkDirtyOnlyWhenRegeneratingAndOnlyOnce",
))


# Restored UnityEngine.UI.HLCarousel (HLUnityUI.Runtime 0x06000089..91), Hardlight.HLCarouselSelection
# (0x06000560..564), the axis content focus subclasses (0x06000568..56b, 0x060005c9..5cc),
# HLPips (0x06000076..7a) and HLPipsElement (0x0600007b).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLCarouselTests." + name for name in (
    "DeclarationKeepsTheDumpedFieldsConstantsAndMembers",
    "SetWrapOffsetStoresTheVector",
    "SetupCarouselExcludesBothCapsAndEnablesTheHorizontalAxis",
    "SetupCarouselWithoutCapsCountsEveryChildOnTheVerticalAxis",
    "SetupCarouselBelowTwoItemsDisablesScrollingEvenForCapsOutsideTheContent",
    "SetupCarouselWithAnUndeclaredDirectionStoresAVerticalOffsetAndEnablesNoAxis",
    "SetupCarouselWithExactlyTwoItemsEnablesTheAxis",
    "SetupCarouselSubtractsOnlyTheEndCapWhenTheStartCapIsMissing",
    "SetupCarouselSubtractsOnlyTheStartCapWhenTheEndCapIsMissing",
    "WrapAxisPastTheEndMovesBackOneSpanAndKeepsTheVelocity",
    "WrapAxisBeforeTheStartMovesForwardOneSpan",
    "WrapAxisOnTheVerticalAxisMovesOnlyY",
    "WrapAxisInsideTheWrapBoundsChangesNothing(1250)",
    "WrapAxisInsideTheWrapBoundsChangesNothing(-250)",
    "WrapAxisInsideTheWrapBoundsChangesNothing(500)",
    "WrapAxisWithAZeroOffsetPinsAnInRangePositionToTheStart",
    "WrapAxisWithADenormalOffsetPinsAnInRangePositionToTheStart",
    "WrapAxisWithAnUndeclaredAxisThrowsBeforeWriting",
    "LateUpdateWrapsOnlyTheHorizontalAxisWhenBothAreEnabled",
    "LateUpdateWrapsTheVerticalAxisWithTheYOffset",
    "LateUpdateWithBothAxesDisabledDoesNotWrap",
    "LateUpdateRunsTheHLScrollRectSmallVelocityStopFirst",
    "CreateCapsCopiesTheOppositeEndsAndShiftsThemAlongX",
    "CreateCapsWithoutHorizontalShiftsAlongYAndAcceptsNullCallbacks",
    "DestroyCapsDestroysBothCapObjectsAndClearsTheFields",
    "RecreateCapsDestroysTheOldCapsBeforeCopyingTheItems",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLCarouselSelectionTests." + name for name in (
    "DeclarationOverridesTheFourSelectionPolicies",
    "PoliciesDisableNormalisingAndClampingEnableWrappingAndWidenTheBookends",
    "TargetScrollOffsetPastTheListEndIsUnclampedOnlyForTheCarousel",
    "TargetScrollOffsetBeforeTheListStartIsUnclampedOnlyForTheCarousel",
))
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.DeclarationOverridesBothSlotsAndHidesLateUpdate(True)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.DeclarationOverridesBothSlotsAndHidesLateUpdate(False)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.RefocusMovesTheHolderAlongItsAxis(True)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.RefocusMovesTheHolderAlongItsAxis(False)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.RefocusWritesTheOffsetEvenWhenFocusIsDisabled(True)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.RefocusWritesTheOffsetEvenWhenFocusIsDisabled(False)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.LateUpdateWithoutPreserveVelocityOnlySamplesTheVelocity(True)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.LateUpdateWithoutPreserveVelocityOnlySamplesTheVelocity(False)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.LateUpdateDoesNotRestoreAPreviousVelocityOfMinusEpsilon(True)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.LateUpdateDoesNotRestoreAPreviousVelocityOfMinusEpsilon(False)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.LateUpdateFlushesAPendingRefocusFirst(True)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.LateUpdateFlushesAPendingRefocusFirst(False)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.CalculatePositionDeltaUsesTheChildSizeOnItsOwnAxis(True,0)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.CalculatePositionDeltaUsesTheChildSizeOnItsOwnAxis(True,1)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.CalculatePositionDeltaUsesTheChildSizeOnItsOwnAxis(True,2)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.CalculatePositionDeltaUsesTheChildSizeOnItsOwnAxis(True,3)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.CalculatePositionDeltaUsesTheChildSizeOnItsOwnAxis(True,4)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.CalculatePositionDeltaUsesTheChildSizeOnItsOwnAxis(True,5)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.CalculatePositionDeltaUsesTheChildSizeOnItsOwnAxis(True,6)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.CalculatePositionDeltaUsesTheChildSizeOnItsOwnAxis(False,0)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.CalculatePositionDeltaUsesTheChildSizeOnItsOwnAxis(False,1)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.CalculatePositionDeltaUsesTheChildSizeOnItsOwnAxis(False,2)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.CalculatePositionDeltaUsesTheChildSizeOnItsOwnAxis(False,3)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.CalculatePositionDeltaUsesTheChildSizeOnItsOwnAxis(False,4)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.CalculatePositionDeltaUsesTheChildSizeOnItsOwnAxis(False,5)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.CalculatePositionDeltaUsesTheChildSizeOnItsOwnAxis(False,6)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.LateUpdateRestoresThePreviousVelocityOnlyAfterANegativeToPositiveFlip(True,5,-3,True)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.LateUpdateRestoresThePreviousVelocityOnlyAfterANegativeToPositiveFlip(True,0,-3,True)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.LateUpdateRestoresThePreviousVelocityOnlyAfterANegativeToPositiveFlip(True,-5,3,False)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.LateUpdateRestoresThePreviousVelocityOnlyAfterANegativeToPositiveFlip(True,5,3,False)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.LateUpdateRestoresThePreviousVelocityOnlyAfterANegativeToPositiveFlip(True,-5,-3,False)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.LateUpdateRestoresThePreviousVelocityOnlyAfterANegativeToPositiveFlip(False,5,-3,True)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.LateUpdateRestoresThePreviousVelocityOnlyAfterANegativeToPositiveFlip(False,0,-3,True)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.LateUpdateRestoresThePreviousVelocityOnlyAfterANegativeToPositiveFlip(False,-5,3,False)",
    "ProjectLucid.Tests.OriginalHLAxisScrollRectContentFocusTests.LateUpdateRestoresThePreviousVelocityOnlyAfterANegativeToPositiveFlip(False,5,3,False)",
)
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLPipsTests." + name for name in (
    "DeclarationsKeepTheDumpedFieldsAttributesAndMembers",
    "ConstructorStartsWithNoPreviousPipAndAnEmptyList",
    "SetupPipsInstantiatesUnderTheRootAndInitialisesOnlyTheActivePip",
    "SetupPipsAppendsToThePipsOfAnEarlierSetup",
    "SetupPipsWithAnOutOfRangeActiveIndexInitialisesEveryPipInactive",
    "SetupPipsWithoutAPrefabChangesNothing",
    "NextDeactivatesTheCurrentPipThenActivatesTheNextUntilTheLast",
    "PreviousDeactivatesTheCurrentPipThenActivatesThePreviousUntilTheFirst",
    "NextStopsAtThePipCountEvenWithMorePipsInTheList",
    "NextWithoutPipsAdvancesTheIndicesBeforeThrowing",
    "PreviousWithANegativeIndexAdvancesBeforeThrowing",
))
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalHLPipsPlayTests.DestroyPipsEmptiesTheListNowAndDestroysThePipsAtFrameEnd",
    "ProjectLucid.Tests.OriginalHLPipsPlayTests.DestroyPipsWalksBackwardsAndStopsAtADestroyedPip",
)


# Restored HLUnityUI.Runtime HLUIAnimationInOut family (0x060004d2..0x06000512), AnimatableEvent
# (0x060005cd..d0) and FullScreenTransitionManager (0x060000f0..f7).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.BaseRetainsWholeDeclarationAndMemberOrder",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.NestedEnumsRetainTheirLiterals",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.AutoAnimateOutIteratorRetainsTheGeneratedShape",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.ConstructionLeavesEveryFieldAtItsDefault",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.AwakeBuildsTheThreeWaitsFromTheirOwnSeconds",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.OnEnableAdoptsTheInitialStateAndAnimatesTowardsADifferentTarget(0,0,0)",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.OnEnableAdoptsTheInitialStateAndAnimatesTowardsADifferentTarget(0,1,1)",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.OnEnableAdoptsTheInitialStateAndAnimatesTowardsADifferentTarget(0,3,2)",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.OnEnableAdoptsTheInitialStateAndAnimatesTowardsADifferentTarget(0,2,0)",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.OnEnableAdoptsTheInitialStateAndAnimatesTowardsADifferentTarget(0,4,0)",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.OnEnableAdoptsTheInitialStateAndAnimatesTowardsADifferentTarget(2,1,0)",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.OnEnableAdoptsTheInitialStateAndAnimatesTowardsADifferentTarget(2,3,2)",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.OnEnableAdoptsTheInitialStateAndAnimatesTowardsADifferentTarget(4,1,1)",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.OnEnableAdoptsTheInitialStateAndAnimatesTowardsADifferentTarget(4,3,0)",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.OnEnableAdoptsTheInitialStateAndAnimatesTowardsADifferentTarget(2,2,0)",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.OnEnableComparesTheTargetWithTheInitialStateNotThePreviousState",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.AnimateNeedsADirectionAndAnActiveHierarchy",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.OnDisableStopsBothCoroutinesAndForceFinishes",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.AutoAnimateOutYieldsTheTimeoutWaitThenAnimatesOutOnce",
    "ProjectLucid.Tests.OriginalHLUIAnimationInOutTests.IndependentTimeDeltaFollowsTheUnscaledFlag",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.AlphaRetainsWholeDeclaration",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.AnimateInAndOutAreGuardedByTheCurrentState(0,1,2)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.AnimateInAndOutAreGuardedByTheCurrentState(1,0,2)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.AnimateInAndOutAreGuardedByTheCurrentState(2,0,2)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.AnimateInAndOutAreGuardedByTheCurrentState(3,1,0)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.AnimateInAndOutAreGuardedByTheCurrentState(4,1,0)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.ForceFinishSnapsToTheCurveEndForTheDirection(1)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.ForceFinishSnapsToTheCurveEndForTheDirection(2)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.ForceFinishWithoutADirectionOnlyStops",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.ForceFinishToleratesMissingTargets",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.OnEnableForceFinishesTheInitialStateBeforeDispatchingTheTarget",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.OnEnableForceFinishesAnInitialOut",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.OnEnableOnlyPreFinishesTheTwoRestingStates(0)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.OnEnableOnlyPreFinishesTheTwoRestingStates(2)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.OnEnableOnlyPreFinishesTheTwoRestingStates(4)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.SetFinalStateInSchedulesTheAutoAnimateOutOnlyWhenFlagged",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.FirstStepYieldsTheDelayUnlessItReversesARunningAnimation(0,1,0)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.FirstStepYieldsTheDelayUnlessItReversesARunningAnimation(2,1,0)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.FirstStepYieldsTheDelayUnlessItReversesARunningAnimation(4,1,2)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.FirstStepYieldsTheDelayUnlessItReversesARunningAnimation(3,2,1)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.FirstStepYieldsTheDelayUnlessItReversesARunningAnimation(4,2,1)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.FirstStepYieldsTheDelayUnlessItReversesARunningAnimation(2,2,2)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.LoopStepSamplesAtTheInTimeAndUpdatesProgress(1)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.LoopStepSamplesAtTheInTimeAndUpdatesProgress(2)",
    "ProjectLucid.Tests.OriginalHLAlphaInOutTests.RunCompletesAfterOneBodyWhenResumedAtTheEnd",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.ScaleRetainsWholeDeclaration",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.AwakeDefaultsTheTargetToItsOwnTransform",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.AnimationIterationClampsTheDirectionalTimeToTheGivenEnd(0)",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.AnimationIterationClampsTheDirectionalTimeToTheGivenEnd(1)",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.AnimationIterationClampsTheDirectionalTimeToTheGivenEnd(2)",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.AnimationIterationClampsTheDirectionalTimeToTheGivenEnd(3)",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.AnimationIterationClampsTheDirectionalTimeToTheGivenEnd(4)",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.AnimationIterationClampsTheDirectionalTimeToTheGivenEnd(5)",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.ForceFinishRequiresAnActiveHierarchyAndADirection",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.ForceFinishSamplesEachAxisAtItsOwnEnd(1)",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.ForceFinishSamplesEachAxisAtItsOwnEnd(2)",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.ForceFinishInSamplesThePingPongAxisAtItsOwnEnd",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.AnimateInActivatesTheObjectOnlyWhenFlaggedAndNotGuarded",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.AnimateOutNeverActivatesTheObject",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.RunAlwaysWaitsThenRestartsFromZeroAgainstTheLongerCurve(1)",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.RunAlwaysWaitsThenRestartsFromZeroAgainstTheLongerCurve(2)",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.RunUsesTheYEndWhenTheYCurveIsLonger",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.RunFinishesWithTheFinalStateAndClearsTheDirection",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.OnEnableForceFinishesAnInitialOut",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.OnEnableForceFinishesAnInitialIn",
    "ProjectLucid.Tests.OriginalHLScaleInOutTests.SetFinalStateInSchedulesTheAutoAnimateOutOnlyWhenFlagged",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.AnchorRetainsWholeDeclaration",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.ConstructionSeedsTheClampedOutAndStretchInAnchors",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.AwakeDefaultsTheTargetToItsOwnRectTransform",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.SetAnchorsPlacesTheClampedAndStretchedEdgesByDirection(0)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.SetAnchorsPlacesTheClampedAndStretchedEdgesByDirection(1)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.SetAnchorsPlacesTheClampedAndStretchedEdgesByDirection(2)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.SetAnchorsPlacesTheClampedAndStretchedEdgesByDirection(3)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.SetAnchorsPlacesTheClampedAndStretchedEdgesByDirection(4)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.SetAnchorsPlacesTheClampedAndStretchedEdgesByDirection(-1)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.SetAnchorTargetsFromConstantsDerivesBothPairsFromTheCurrentAnchors(0)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.SetAnchorTargetsFromConstantsDerivesBothPairsFromTheCurrentAnchors(1)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.SetAnchorTargetsFromConstantsDerivesBothPairsFromTheCurrentAnchors(2)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.SetAnchorTargetsFromConstantsDerivesBothPairsFromTheCurrentAnchors(3)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.SetAnchorTargetsFromConstantsDerivesBothPairsFromTheCurrentAnchors(7)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.ZeroWidthLeftToRightStoresPositiveZero",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.SetFinalStateAppliesTheAnchorsBeforeTheEventAndProgressAfterIt(True)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.SetFinalStateAppliesTheAnchorsBeforeTheEventAndProgressAfterIt(False)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.SetFinalStateInSchedulesTheAutoAnimateOutOnlyWhenFlagged",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.ForceFinishFinishesInOnlyForAnInDirectionAndKeepsTheDirection(0,3)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.ForceFinishFinishesInOnlyForAnInDirectionAndKeepsTheDirection(1,1)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.ForceFinishFinishesInOnlyForAnInDirectionAndKeepsTheDirection(2,3)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.OnEnablePlacesTheInitialPairWithoutEvents(1)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.OnEnablePlacesTheInitialPairWithoutEvents(3)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.OnEnablePlacesTheInitialPairWithoutEvents(0)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.LoopStepLerpsEachEdgeByTheMotionType(0)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.LoopStepLerpsEachEdgeByTheMotionType(1)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.LoopStepLerpsEachEdgeByTheMotionType(2)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.LoopStepLerpsEachEdgeByTheMotionType(3)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.LoopStepLerpsEachEdgeByTheMotionType(4)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.LoopStepLerpsEachEdgeByTheMotionType(5)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.LoopStepLerpsEachEdgeByTheMotionType(6)",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.OutRunResumesFromProgressAfterTheOutDelay",
    "ProjectLucid.Tests.OriginalHLAnchorTranslateInOutTests.RunFinishesInAndKeepsTheDirection",
    "ProjectLucid.Tests.OriginalAnimatableEventTests.AnimatableEventRetainsWholeDeclaration",
    "ProjectLucid.Tests.OriginalAnimatableEventTests.ConstructionCreatesAnEmptyDictionary",
    "ProjectLucid.Tests.OriginalAnimatableEventTests.AwakeMapsEachNameToItsFirstEventAndReportsDuplicates",
    "ProjectLucid.Tests.OriginalAnimatableEventTests.RepopulatingClearsThePreviousNames",
    "ProjectLucid.Tests.OriginalAnimatableEventTests.TriggerEventIgnoresNullEmptyAndUnknownNames",
    "ProjectLucid.Tests.OriginalAnimatableEventTests.ANullEventNameFaultsThePopulation",
    "ProjectLucid.Tests.OriginalFullScreenTransitionManagerTests.ManagerRetainsWholeDeclaration",
    "ProjectLucid.Tests.OriginalFullScreenTransitionManagerTests.ConstructionCreatesAnEmptyControllerTable",
    "ProjectLucid.Tests.OriginalFullScreenTransitionManagerTests.AwakeRegistersTheSingletonAndKeepsTheFirstControllerOfEachType",
    "ProjectLucid.Tests.OriginalFullScreenTransitionManagerTests.StartFallsBackToTheOwnCanvasAndReportsIt",
    "ProjectLucid.Tests.OriginalFullScreenTransitionManagerTests.IsTransitionActiveIsAnyStateButHeldOut",
    "ProjectLucid.Tests.OriginalFullScreenTransitionManagerTests.StartTransitionAppliesTheSortingFirstAndDelegates",
    "ProjectLucid.Tests.OriginalFullScreenTransitionManagerTests.ContinueTransitionOnlyContinuesAHeldInController",
    "ProjectLucid.Tests.OriginalFullScreenTransitionManagerTests.ResetTransitionsResetsOnlyRegisteredControllers",
    "ProjectLucid.Tests.OriginalFullScreenTransitionManagerTests.SortingOrderZeroNeverOverridesTheCanvas",
    "ProjectLucid.Tests.OriginalFullScreenTransitionManagerTests.ADifferentLayerNameMovesTheCanvasToThatLayer",
)

# Restored HLUnityUI.Runtime ContentFrameController (0x0600054e..5b) and ContentFrameControllerOverride (0x06000022..2a).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalContentFrameControllerTests." + name for name in (
    "DeclarationsKeepTheOriginalConstantsAndFieldOrder",
    "ConstructorSeedsTheLerpTimeAndLeavesTheFrameUninitialised",
    "EnsureInitialisationRunsOnceAndCapturesTheInitialAnchors",
    "ApplyWithoutAContentRootInitialisesButDoesNotLerp",
    "ApplyWithoutADodgedOrientationLerpsBackToTheInitialAnchors(False,False,3)",
    "ApplyWithoutADodgedOrientationLerpsBackToTheInitialAnchors(False,False,1)",
    "ApplyWithoutADodgedOrientationLerpsBackToTheInitialAnchors(True,False,1)",
    "ApplyWithoutADodgedOrientationLerpsBackToTheInitialAnchors(False,True,4)",
    "ApplyWithoutADodgedOrientationLerpsBackToTheInitialAnchors(True,True,5)",
    "ApplyWithoutASafeAreaSkipsTheInsetsAndTheOverride(True,False,3)",
    "ApplyWithoutASafeAreaSkipsTheInsetsAndTheOverride(False,True,1)",
    "UpdateAdvancesTheProgressAndLerpsFromTheStoredCurrentAnchors",
    "UpdateClampsTheFractionToTheUnitRange",
    "UpdateAtTheTransitionTimeOnlyClearsTheFlag",
    "OnEnableWithoutAnOverrideSubscribesBothCallbacksAndApplies",
    "OnEnableWithAnActiveOverrideRegistersApplyWithTheHandlerInstead",
    "OnEnableWithAnInactiveOverrideKeepsTheOrientationCallback",
    "OnEnableWithoutAScreenManagerOnlyRegistersWithTheHandler",
    "SetOverrideMovesTheSubscriptionsOnlyForTheFirstOverride",
    "SetNotchEvaluatorNeverCallsTheEvaluator",
    "OverrideWithoutAHandlerReportsNothingAndIgnoresRegistrations",
    "OverrideMarginsRequireTheirOwnFlagAndAnActiveHandler",
    "OverrideKeepsTheFirstHandlerAndForwardsRegistrations",
))

# Restored HLUnityUI.Runtime page components (0x06000513..33) and the PageStackSystem`1/OverlaySystem closure identities.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalPageNavigationComponentTests." + name for name in (
    "OpenMenuPageForwardsThePageAndTheReplaceFlag",
    "OpenDynamicMenuPageSendsTheChosenEntryAsPageData",
    "MoveBackMenuPageMovesBackWhenItCanAndOnlyThenLeavesTheCurrentPage",
    "MoveBackMenuPageReplacesWithTheDefaultPageOnlyWhenItCannotMoveBack",
    "CloseAllMenuPagesClosesWithTheStandardReason",
    "CloseDialogPageClosesTheCurrentPageOrEveryPage",
    "OpenDialogPageFollowsTheOpeningBehaviour",
    "DialogSystemListenerRelaysTheDialogSystemEvents",
    "MenuSystemListenerRelaysTheMenuSystemEvents",
    "PageScopesKeepEarlierSubscribersOutOfTheRaisedEvents",
    "OpenCloseOverlayPageShowsOnTheFirstOpenAndHidesWhenTheCountReturnsToZero",
    "OpenCloseOverlayPageIgnoresTheNoneIdentifier",
    "ToggleOverlayPageShowsAClosedPageAndHidesAnOpenOneWithoutFilteringNone",
    "PageStackSystemClosuresKeepTheOriginalGeneratedNamesAndCaptures",
    "OverlaySystemClosuresKeepTheOriginalGeneratedNamesAndCaptures",
))

# Restored HLUnityUI.Runtime ViewBase (0x060002b2..b6), ViewParameters (0x060002b7..ba) and DynamicPageLoad (0x06000518..1b).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalViewAndDynamicPageTests." + name for name in (
    "SetupStoresTheParametersBeforeOnSetupAndCompletesAfterIt",
    "SetupWithoutCompletionListenersStillCallsOnSetup",
    "RectTransformFallsBackToTheOwnTransformAndKeepsAnAssignedOne",
    "RectTransformReplacesADestroyedAssignedTransform",
    "SetActiveSetsTheOwnGameObject",
    "ViewParametersAreEqualExactlyWhenTheRuntimeTypesMatch",
    "CloneIsAShallowCopyOfTheRuntimeType",
    "OnShowShowsEveryMatchingEntryAndTheLastBecomesActive",
    "OnShowActivatesEachEntryBeforeShowingItAndStoresItAfter",
    "OnHideForwardsToTheActiveEntryOnly",
    "OnShowWithOtherParametersFaultsAtTheFirstEntry",
))


# Original HLUnityUI.Runtime ModifiedOnUISelection layers (0x02000094..98) from their x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalSelectionModifierTests." + name for name in (
    "ActivateRetainsTheOriginalDeclaration",
    "ResizeRetainsTheOriginalDeclaration",
    "SkewRetainsTheOriginalDeclaration",
    "SwapColourRetainsTheOriginalDeclaration",
    "SwapSpriteRetainsTheOriginalDeclaration",
    "ActivateUpdateFollowsTheCurrentEnabledFlagAtAnyLerp",
    "ActivateUpdateWithoutACurrentSourceThrowsAndLeavesTheObject",
    "ActivateDeactivatesWithoutReadingTheSourceAndInheritsANoOpActivate",
    "ResizeResetTakesTheOwnRectTransformOrNull",
    "ResizeUpdateMapsLeftBottomToOffsetMinAndNegatedRightTopToOffsetMax",
    "ResizeUpdateClampsTheLerpValue",
    "ResizeUpdateWithoutASourceThrowsBeforeStoringAnything",
    "ResizeUpdateWithoutARectStoresTheFirstPairThenThrows",
    "SkewResetTakesTheSiblingSkew",
    "SkewUpdateLerpsBothAxesAndAppliesThemInstantly",
    "SkewUpdateClampsTheLerpValue",
    "SkewUpdateFaultsOnSourcesBeforeStoringAndOnTheSkewAfterStoring",
    "SwapColourResetTakesTheSiblingImage",
    "SwapColourBlendsRgbThroughLabAndLerpsAlphaSeparately",
    "SwapColourReadsTheLabColourBackBeforeSettingTheAlpha",
    "SwapColourClampsTheAlphaLerp",
    "SwapColourFaultsBeforeStoringAnything",
    "SwapSpriteResetTakesTheSiblingImage",
    "SwapSpriteBeginMarksSwappedOnlyForTheSameSourceInstance",
    "SwapSpriteUpdateBelowHalfChangesNothing",
    "SwapSpriteUpdateFromHalfSwapsOnceToTheCurrentSource",
    "SwapSpriteUpdateWithANaNLerpSwaps",
    "SwapSpriteUpdateWithoutAnImageThrowsAndStaysUnswapped",
    "SwapSpriteActivateAppliesTheSourceAndKeepsTheSwappedFlag",
))


# Original HLUnityUI.Runtime HLEnumSelector`1 (0x0200000b) from its shared x86-64 bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalHLEnumSelectorTests." + name for name in (
    "EnumSelectorRetainsTheOriginalDeclaration",
    "InitLoadsTheEnumValuesAndStartsAtTheFirstMatch",
    "InitWithoutAMatchFallsBackToTheFirstValue",
    "InitSearchesWithTheSubclassCompare",
    "OnEnableRunsInit",
    "OnValidateRunsInit",
    "ValueReadsTheCurrentValueField",
    "IncrementWrapsAtTheCachedValueCount",
    "DecrementWrapsToTheLastCachedIndex",
    "DecrementFromOneKeepsIndexZero",
    "SetCurrentValueStoresTheValueAndMovesToTheFirstMatch",
    "SetCurrentValueWithoutAMatchKeepsTheValueAndTheIndex",
    "SetCurrentValueSearchesOnlyTheCachedCount",
    "InitOverAnEmptyEnumThrowsBeforeValidate",
))

# Field-only row admission for the StringTable, LoggingHost and MenuBloom lifecycle fixtures and the logging-host proof (issue #214).
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.BorrowedLifecycleRowAdmissionTests.StringTableFixture_RefusesObservedTableRow(False)",
    "ProjectLucid.Tests.BorrowedLifecycleRowAdmissionTests.StringTableFixture_RefusesObservedTableRow(True)",
    "ProjectLucid.Tests.BorrowedLifecycleRowAdmissionTests.StringTableFixture_RefusesObservedLanguageRow(False)",
    "ProjectLucid.Tests.BorrowedLifecycleRowAdmissionTests.StringTableFixture_RefusesObservedLanguageRow(True)",
    "ProjectLucid.Tests.BorrowedLifecycleRowAdmissionTests.StringTableFixture_RefusesObservedLoggingHostRow(False)",
    "ProjectLucid.Tests.BorrowedLifecycleRowAdmissionTests.StringTableFixture_RefusesObservedLoggingHostRow(True)",
    "ProjectLucid.Tests.BorrowedLifecycleRowAdmissionTests.LoggingHostFixture_RefusesObservedLoggingHostRow(False)",
    "ProjectLucid.Tests.BorrowedLifecycleRowAdmissionTests.LoggingHostFixture_RefusesObservedLoggingHostRow(True)",
    "ProjectLucid.Tests.BorrowedLifecycleRowAdmissionTests.MenuBloomFixture_RefusesObservedBloomRow(False)",
    "ProjectLucid.Tests.BorrowedLifecycleRowAdmissionTests.MenuBloomFixture_RefusesObservedBloomRow(True)",
    "ProjectLucid.Tests.BorrowedLifecycleRowAdmissionTests.StringTableFixture_RefusalLeavesEarlierIdleRowsUnchanged",
    "ProjectLucid.Tests.BorrowedLifecycleRowAdmissionTests.FixturesAdmitAbsentAndIdleRowsWithoutChangingThem",
    "ProjectLucid.Tests.BorrowedLifecycleRowAdmissionTests.CreatedRowIsCapturedFromTheRawEntryAfterActivation",
    "ProjectLucid.Tests.OriginalMenuBloomLifecycleTests.OwnedBloomRefusesAnObservedDormantRowBeforeActivation",
)
EDITMODE_TESTS += (
    "ProjectLucid.Tests.VerificationLifecycleRowTests.LoggingHostRowWithForeignStateIsRefusedWithoutInvokingIt(False,0)",
    "ProjectLucid.Tests.VerificationLifecycleRowTests.LoggingHostRowWithForeignStateIsRefusedWithoutInvokingIt(False,1)",
    "ProjectLucid.Tests.VerificationLifecycleRowTests.LoggingHostRowWithForeignStateIsRefusedWithoutInvokingIt(False,2)",
    "ProjectLucid.Tests.VerificationLifecycleRowTests.LoggingHostRowWithForeignStateIsRefusedWithoutInvokingIt(False,3)",
    "ProjectLucid.Tests.VerificationLifecycleRowTests.LoggingHostRowWithForeignStateIsRefusedWithoutInvokingIt(True,0)",
    "ProjectLucid.Tests.VerificationLifecycleRowTests.LoggingHostRowWithForeignStateIsRefusedWithoutInvokingIt(True,1)",
    "ProjectLucid.Tests.VerificationLifecycleRowTests.LoggingHostRowWithForeignStateIsRefusedWithoutInvokingIt(True,2)",
    "ProjectLucid.Tests.VerificationLifecycleRowTests.LoggingHostRowWithForeignStateIsRefusedWithoutInvokingIt(True,3)",
    "ProjectLucid.Tests.VerificationLifecycleRowTests.ConfigurationRowIsBorrowedWhenLiveAndRefusedWhenDormantWithForeignWork",
    "ProjectLucid.Tests.VerificationLifecycleRowTests.ReleaseRemovesOnlyAnIdleCreatedRow",
    "ProjectLucid.Tests.VerificationLifecycleRowTests.BorrowedRowReleaseRemovesOnlyClearedTypedEntriesAddedWhileBorrowed",
    "ProjectLucid.Tests.VerificationLifecycleRowTests.BorrowedLiveRowReleaseRemovesOnlyTypedEntriesAddedWhileBorrowed",
    "ProjectLucid.Tests.VerificationLifecycleRowTests.EngineProofRefusesAnObservedDormantHostRowBeforeAnyStep",
)


# CharacterTransition_Target.DoUpdate (Game.Runtime 0600179e) reads the active homing target.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalCharacterTransitionTargetTests.DoUpdateReadsTheActiveHomingTargetAndNeverThePendingCandidate",
    "ProjectLucid.Tests.OriginalCharacterTransitionTargetTests.DoUpdateTestsTheActiveTargetObjectThenTypeDistanceAndSurfaceInOriginalOrder",
)


# Own Il2CppSetOption check-option annotations on HLUnityCore owners (dump.cs metadata order).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalHLUnityCoreCheckOptionTests.CoreTypesKeepTheShippedOwnCheckOptionAnnotationsInOrder",
)


# Fixture isolation of borrowed shared state: the action-row, enum-table and
# action-pool leases, the untyped ReleaseEmptyRow check, the CheckedGet engine
# admission, first-failure cleanup, and the SharedStateSnapshot reader.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.RecoveredBehaviorTests.EnumStringVerifiersLeaveTheNativeTablesAsFoundInEitherOrder",
    "ProjectLucid.Tests.RecoveredBehaviorTests.EnumStringVerifierFailuresStillRestoreTheNativeTables(0)",
    "ProjectLucid.Tests.RecoveredBehaviorTests.EnumStringVerifierFailuresStillRestoreTheNativeTables(1)",
    "ProjectLucid.Tests.RecoveredBehaviorTests.ActorVerifiersLeaveTheirCachedEnumTablesAsFound",
    "ProjectLucid.Tests.RecoveredBehaviorTests.ActorVerifierFailuresStillRestoreTheirCachedEnumTables(0)",
    "ProjectLucid.Tests.RecoveredBehaviorTests.ActorVerifierFailuresStillRestoreTheirCachedEnumTables(1)",
    "ProjectLucid.Tests.RecoveredBehaviorTests.ActorVerifierFailuresStillRestoreTheirCachedEnumTables(2)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.ActionRowsRemoveAnAbsentRowTheFixtureCreated",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.ActionRowsRemoveTheCreatedRowWhenTheFixtureFailsAfterConstruction",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.ActionRowsKeepABorrowedEmptyRow",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.ActionRowsKeepABorrowedRowItsSubscribersAndCallbacks",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.ActionRowsLeaveUnrelatedActionRowsUntouched",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.ActionRowsKeepAndReportACreatedRowThatGainedASubscriber",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.ReleaseEmptyRowRefusesUntypedObserversAndDeferredWork(0)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.ReleaseEmptyRowRefusesUntypedObserversAndDeferredWork(1)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.ReleaseEmptyRowRefusesUntypedObserversAndDeferredWork(2)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.CheckedGetEngineFixtureRefusesBorrowedRowsWithoutTouchingThem(0)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.CheckedGetEngineFixtureRefusesBorrowedRowsWithoutTouchingThem(1)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.CheckedGetEngineFixtureRefusesBorrowedRowsWithoutTouchingThem(2)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.CheckedGetEngineFixtureRefusesBorrowedRowsWithoutTouchingThem(3)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.CheckedGetEngineFixtureRefusesBorrowedRowsWithoutTouchingThem(4)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.CheckedGetEngineFixtureRefusesBorrowedRowsWithoutTouchingThem(5)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.CheckedGetEngineFixtureRefusesBorrowedRowsWithoutTouchingThem(6)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.CheckedGetEngineFixtureRefusesBorrowedRowsWithoutTouchingThem(7)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.CheckedGetEngineFixtureRefusesBorrowedRowsWithoutTouchingThem(8)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.ActionPoolLeaseReturnsOnlyItsOwnActionsAfterAFailure",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.StaticDictionaryLeaseRestoresTheAdmittedTableAfterAFailure",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.RunThenCleanUpRunsEveryStepAndKeepsTheFirstFailure(0)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.RunThenCleanUpRunsEveryStepAndKeepsTheFirstFailure(1)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.RunThenCleanUpRunsEveryStepAndKeepsTheFirstFailure(2)",
    "ProjectLucid.Tests.FSMActionPoolOwnershipTests.FailureAfterAcquisitionReturnsOnlyTheFixturesActions(0)",
    "ProjectLucid.Tests.FSMActionPoolOwnershipTests.FailureAfterAcquisitionReturnsOnlyTheFixturesActions(1)",
    "ProjectLucid.Tests.FSMActionPoolOwnershipTests.FailureAfterAcquisitionReturnsOnlyTheFixturesActions(2)",
    "ProjectLucid.Tests.FSMActionPoolOwnershipTests.FailureAfterAcquisitionReturnsOnlyTheFixturesActions(3)",
    "ProjectLucid.Tests.FSMActionPoolOwnershipTests.FailureAfterAcquisitionReturnsOnlyTheFixturesActions(4)",
    "ProjectLucid.Tests.FSMActionPoolOwnershipTests.FailureAfterAcquisitionReturnsOnlyTheFixturesActions(5)",
    "ProjectLucid.Tests.FSMActionPoolOwnershipTests.FailureAfterAcquisitionReturnsOnlyTheFixturesActions(6)",
    "ProjectLucid.Tests.SharedStateSnapshotTests.UnchangedStateHasNoDifference",
    "ProjectLucid.Tests.SharedStateSnapshotTests.CreatedRowIsReportedUntilTheTestRemovesIt",
    "ProjectLucid.Tests.SharedStateSnapshotTests.ObserversDeferredWorkAndSystemsOnAnExistingRowAreReported",
    "ProjectLucid.Tests.SharedStateSnapshotTests.ActionSubscriptionAndAKeptEmptyRowAreReported",
    "ProjectLucid.Tests.SharedStateSnapshotTests.CachedEnumMissIsReported",
    "ProjectLucid.Tests.SharedStateSnapshotTests.CachedMissInEachHLAutoGeneratedTableOwnerIsReported",
    "ProjectLucid.Tests.SharedStateSnapshotTests.CachedMissInEachRemainingEnumTableOwnerIsReported",
    "ProjectLucid.Tests.SharedStateSnapshotTests.CloudConnectSubscriberIsReportedUntilRemoved",
    "ProjectLucid.Tests.SharedStateSnapshotTests.ReplacedOpStringCacheIsReportedUntilRestored",
)

# Existing FSM history drains even after its stored maximum is disabled (#475).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.FSMStorageHistoryClearTests." + name for name in (
    "ClearStateHistoryDrainsAfterTheMaximumIsZeroed",
    "ClearStateHistoryDrainsAfterTheMaximumIsNegative",
    "ClearStateHistoryDrainsAfterTheMaximumIsRemoved",
    "ClearDrainsBeforeDiscardingEntriesAfterTheMaximumIsZeroed",
    "ClearStateHistoryDrainsWithAPositiveMaximum",
))

# Original category lookup precedes the destination field read (#519).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.TimeSettingsConversionOrderTests.CategoryGetterRunsBeforeTheDestinationFieldIsRead",
)

# Remaining fixture/helper graph-name ownership, including failure exits (#517).
EDITMODE_TESTS += (
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.FixtureLifecycleReleasesNamesAfterEachInvocation("FairPlay",False)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.FixtureLifecycleReleasesNamesAfterEachInvocation("FairPlay",True)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.FixtureLifecycleReleasesNamesAfterEachInvocation("Splash",False)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.FixtureLifecycleReleasesNamesAfterEachInvocation("Splash",True)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.FixtureLifecycleReleasesNamesAfterEachInvocation("Arm",False)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.FixtureLifecycleReleasesNamesAfterEachInvocation("Arm",True)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.FixtureLifecycleReleasesNamesAfterEachInvocation("RowFailure",False)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.FixtureLifecycleReleasesNamesAfterEachInvocation("RowFailure",True)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.FixtureLifecycleReleasesNamesAfterEachInvocation("RowCleanup",False)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.FixtureLifecycleReleasesNamesAfterEachInvocation("RowCleanup",True)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.FixtureLifecycleReleasesNamesAfterEachInvocation("RowBorrowed",False)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.FixtureLifecycleReleasesNamesAfterEachInvocation("RowBorrowed",True)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.PublicHelperReleasesNamesAfterEachInvocation("Manager",False)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.PublicHelperReleasesNamesAfterEachInvocation("Manager",True)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.PublicHelperReleasesNamesAfterEachInvocation("Boot",False)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.PublicHelperReleasesNamesAfterEachInvocation("Boot",True)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.PublicHelperReleasesNamesAfterEachInvocation("BootManaged",False)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.PublicHelperReleasesNamesAfterEachInvocation("BootManaged",True)',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.ManagerDiagnosticsPreconditionFailureReleasesNames',
    'ProjectLucid.Tests.RemainingGraphNameOwnershipTests.ManagerProofPreservesThePreexistingFixedDiagnosticKey',
)

# Synthetic graph names stay owned across successful and failed fixtures (#476).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SyntheticGraphNameOwnershipTests." + name for name in (
    "StorageConditionProofLeavesNoConditionNamesAcrossRepeatedRuns",
    "StorageConditionProofFailureLeavesNoConditionNames",
    "NumericShortcutConstructorsReleaseTheirDerivedNames",
    "NumericShortcutConstructorFailureReleasesTheirDerivedNames",
))

# Original conversion diagnostics precede null-unbox rethrows (#477).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalHLUnityCoreMiscTests.ConvertValueLogsAndRethrowsANullUnboxFault",
)

# Original camera direction changes release secondary debounce on both axes (#480).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.CameraInputDisablerTests.OriginalDirectionReleaseOfTheSecondaryDebounce",
)

# Secondary fixture diagnostics cannot stop cleanup or replace its first failure (#470).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.RunThenCleanUpContinuesWhenTheWarningLoggerThrows(0)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.RunThenCleanUpContinuesWhenTheWarningLoggerThrows(1)",
    "ProjectLucid.Tests.VerificationFixtureOwnershipTests.RunThenCleanUpContinuesWhenTheFailureTextThrows",
)

# Original managed declaration contracts restored for issue #295.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalManagedDeclarationContractsTests." + name for name in (
    "NestedTypesKeepOriginalVisibilityAndSerializableFlags",
    "OutroSequenceDreamOrbBeginStaysPrivate",
    "SaveDataSettingsKeepsReadOnlyEditableListAndBeforeFieldInit",
    "DevelopmentOnlyMethodsKeepConditionalAttribute",
    "GameRuntimeOptionalParametersKeepOriginalDefaults",
    "UnityUiOptionalParametersKeepOriginalNullDefaults",
    "ProfilerAndLocalisationMenusKeepOriginalAttributeArguments",
))

# HLBinders format and conversion nodes restored from the 1.10.1 binary.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalBinderFormatNodeTests." + name for name in (
    "DateOffsetAddsTheOffsetToTheInputAfterReadingTheInputFirst",
    "DateStringFormatterConvertsEachDateToLocalTimeBeforeReadingTheFormat",
    "EnumToIntPublishesTheEnumValueAndZeroForANullReference",
    "EnumToStringReadsTheNameTwiceAndPublishesEmptyForANullReference",
    "IntConversionsUseTheNativeIntToSingleAndPlainToString",
    "IntToTimeSpanTreatsTheInputAsSeconds",
    "MemoryFormatterZeroExtendsTheIntSize",
    "NumberCutOffFormatterCapsOnlyAboveAPositiveCutOff",
    "NumberDeltaFormatterFollowsTheNativeSignSpaceAndFormatTable",
    "NumberPercentFormatterFollowsTheNativeFlagTable",
    "SingleToIntTruncatesTowardZero",
    "SingleToStringAppliesFloorThenCeilAndOnlyOneToFourDecimalPlaces",
    "StringFormatterSixArgsReadsEveryArgumentBeforeTheFormat",
    "TimeSpanToDaysAndSecondsAddAHalfAndTruncate",
    "TimeSpanToStringReadsOnlyTheFormatForTheLargestPositiveComponent",
    "TimeStampToDateTimeReadsUnixMilliseconds",
    "TraversalTimeDividesDistanceBySpeedIntoSeconds",
    "Vector2SplitterAndVectorDistanceUseTheVectorComponents",
    "VectorBuilderReadsEveryComponentBeforePublishing",
    "SetUpBindReferenceFieldsListsTheNativeReferenceOrder",
))

# HLSplines.Runtime LocationGenerator, ProceduralMesh_Type* and ProceduralMesh_StorageFor* restoration.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalProceduralMeshStorageTests." + name for name in (
    "ConvertBiasToRatioClampsPerHalfAndKeepsTheNativeNaNOperandOrder",
    "ConvertBiasToRatioWorldUnitScalesTheRatioByTheChosenHalfWidth",
    "ZoneTablesMatchTheOriginalFieldRvaBlobs",
    "GetTBiasPicksAWeightedZoneThenSpreadsInsideIt",
    "TriangleSizesAreHalfTheCrossMagnitude",
    "SplineRandomPositionsSpreadSamplesOverKnotsAndTakeTFromTheRandom",
    "SplineGridCarriesTheOvershootAndSkipsLeadingKnotsWithCeil",
    "RibbonGridEmitsCentreThenRightThenLeftColumns",
    "RibbonRandomWidthRatioLerpsTowardsTheSignedSide",
    "RibbonRandomWorldUnitSkipsRowsWhenBothScaledLimitsExceedTheRequestedOnes",
    "RibbonRandomWorldUnitInverseBiasShiftsByTheConvertedRatios",
    "MeshRandomPositionsAreAreaWeightedAndFoldIntoTheTriangle",
    "MeshTypesStartWithTheOriginalSerializedDefaults",
    "MissingPoolTypeFailsBeforeAnyManagerLookup",
    "NewStorageIsUnpopulatedAndNewLocationsStartActive",
    "UpdateRotationPostMultipliesTheRandomEulerDeviation",
    "UpdatePositionAddsTheRotatedRandomDeviation",
    "DespawnMeshesDropsLocationsOnlyWhenResetIsRequested",
    "FadesFinishAtOnceWithoutMeshTypesAndFadeOutKeepsLocations",
    "RandomLocationsDrawAScaleOnlyOffAUnitParentScaleOrWithARange",
    "GridLocationsDrawAScaleOnlyOffAUnitParentScaleOrWithARange",
    "RandomLocationCountsFollowTheRangeKindAndRoundHalfToEven",
    "GridLocationsShuffleTrimAndShareTheListAcrossTypes",
    "SpawnFromLocationsStopsAtAnExhaustedPoolAndSkipsInactiveOnlyWithTheCallback",
    "FadeScalesTowardsTheTargetAndSnapsOnceTheFadeTimeHasPassed",
    "CreateMeshStorageSplitsPerSubSurfaceOrSpansTheClampedRange",
    "DespawnSurfaceMeshesCountsOnlyPopulatedStorage",
    "IsTooFarAwayMeasuresInSurfaceSpaceAndFadesInsideFadeOutRange",
    "CheckForSpawnAndDespawnLoadBalancesAndFadesOnlyInsideFadeOutRange",
    "MeshStorageWrapsTheColliderInOneTwoPointStorage",
))

# Per-test isolation hook controls; the verdict also requires each one's own
# clean line in the mode's isolation report (ISOLATION_HOOK_CONTROLS).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.SharedStateIsolationHookTests.HookCapturedThisTestBeforeItsBody",
    "ProjectLucid.Tests.SharedStateIsolationHookTests.LeakRecordNamesTheTestAndEachChange",
)
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.SharedStateIsolationPlayModeHookTests.HookCapturedThisTestBeforeItsBody",
    "ProjectLucid.Tests.SharedStateIsolationPlayModeHookTests.HookStaysOpenAcrossFrames",
)

# Issue #288: SurfaceMeshSplineGenerator.Awake 0x06003d61 dispatches InitialiseSplineTrackers.
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.SurfaceMeshSplineGeneratorAwakeTests.AwakeDispatchesInitialiseSplineTrackersOnceAfterTheShutdownSubscriptionAndNeverReset",
    "ProjectLucid.Tests.SurfaceMeshSplineGeneratorAwakeTests.PipeAwakeBuildsTrackerAndProfileFromTheSuppliedSplineBeforeTheFirstDirtyUpdate",
)

# Original HLCrossPromo.Runtime value types, HashUtils, shared resource pool and CrossPromoPage.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalCrossPromoCoreTests." + name for name in (
    "FileSafeSha1RemovesEverySlashAndKeepsPlusAndPadding",
    "TextureConstructorDerivesLocalFileNameFromUrlOnly",
    "AdIdentityIsTheOrdinalNameAlone",
    "FirstAdIndexClampsMissesAndMissingListsToZero",
    "ConfigCopiesAssetValuesAndBuildsTheMetadataRequest",
    "ReferenceCountsHoldersAndKeepsReportingReleasedBelowZero",
    "PoolReleasesOnTheLastHolderAndCallsBackBeforeRemoval",
    "PoolWithoutCallbackStillRemovesReleasedEntries",
    "AdNamesCompareOrdinallyNotByCulture",
    "ReferenceEqualityCallsTheResourcesOwnEqualsWithoutANullGuard",
    "PoolReleasesOnlyTheFirstKeyHoldingAnEqualResource",
    "PageForwardsSpriteAndTextToItsComponents",
    "DeclarationsKeepOriginalFlagsFieldOrderAndDefaults",
    "MethodsAndInterfacesKeepOriginalFlagsOrderAndAttributes",
))

# HLBinders.Runtime logic, math, list and time nodes against the x86-64 retail bodies.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalLogicMathNodeTests." + name for name in (
    "EachNodeListsItsOriginalInputsAndConstructsDistinctReferences",
    "EqualityUsesTheFirstValuesEqualsAndANullFirstMatchesOnlyNull",
    "EnumIntEqualityComparesTheEnumAsItsIntegerValue",
    "IsNullAndIsStringNullOrEmptyTestTheValueRead",
    "AndOrAndNotFollowTheirTruthTablesAndShortCircuitOnFirst",
    "AllOfSkipsUnsetInputsAndStopsReadingOnceFalse",
    "AnyOfReadsOnlyTheFirstSixInputsAndStopsAtTheFirstTrue",
    "ComparisonsNeedAComparableFirstValueAndTestTheCompareToSign",
    "InSetMatchesTheItemThroughEqualsAndStopsAtTheFirstMatch",
    "IntegerAndLongArithmeticWrapsAndKeepsOperandOrder",
    "FloatArithmeticKeepsOperandOrder",
    "DivideUsesFloatDivisionAndOnlyTheLongFormGuardsZero",
    "AbsPublishesIntMinValueUnchangedAndKeepsOrdinaryMagnitudes",
    "AbsFloorMaxAndMinFollowTheirUnityAndSystemForms",
    "ListLengthCountsTheListAndKeepsThePreviousLengthWithoutOne",
    "ProgressWithAStartTimeClampsOnlyAtTheEnd",
    "ProgressWithoutAStartTimeReportsOnlyFinishedOrNot",
))

# Restored HLSplines.Runtime metadata value types, MetadataFixedGroup, MetadataTestMonoBehaviour,
# SplineEditorDefinition, StandaloneAutoProfiler, SplineVisualisation and RibbonVisualisation.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalSplineMetadataTests." + name for name in (
    "ConstructorsKeepTheBaseLabelAndResetStoresEachShippedLabel",
    "DefaultsAreFormattedFromEachOwnDefaultField",
    "ConstantSelectsAUnlessTReachesOneOrIsNaN",
    "FloatLinearIsAClampedLerpOfTheParsedValues",
    "ObjectTypesCopyOnlyAsStringAndNullReturnsA",
    "FixedGroupExposesItsOwnDataListToTheGroupQueries",
    "MetadataTestBehaviourAllocatesEveryContainer",
    "SplineDefinitionDefaultsQualityPickAndDisabledRendering",
    "EnabledShouldRenderPicksByCameraDistanceAndRenderQuality",
    "DefaultTakesTheFirstAssetLeftAfterRemovingTheDefaultNamedMatch",
    "StandaloneProfilerReportsTotalsIncludingInactiveSurfaces",
    "SplineMeshIsAFixedWidthStripWithChordLengthV",
    "RibbonMeshHangsSideQuadsOffTheCentreLine",
    "RibbonMeshUTakesEachSideDistanceFromItsOwnPair",
    "VisualisationsAllocateListsAndResetToTheRequiredComponent",
    "SplineTypeChangeMovesSubscriptionsAndStartsGeneration",
    "RibbonTypeChangeMovesSubscriptionsAndStartsGeneration",
    "SplineTypeChangeLeaksTheOldEditableAndStacksSubscriptions",
    "RibbonTypeChangeLeaksTheOldEditableAndStacksSubscriptions",
    "StoreMeshClearsAPassedScheduleOnlyWithAnEditableSurface",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalAddressableHostRedirectorTests." + name for name in (
    "ConstructionChainsTheIdTransformAndFinalizeUnchainsIt",
    "TheTargetHostReplacesEveryMatchedSchemeHostAndPort",
    "ABlankTargetHostKeepsTheMatchedHost",
    "ACustomRegexReplacesTheDefaultUntilItIsCleared",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalFxDeclarationTests." + name for name in (
    "AddressableHostRedirectorKeepsItsDeclaration",
    "TextureShadowAndCanvasHelpersKeepTheirDeclarations",
    "RendererAndColourHelpersKeepTheirDeclarations",
    "FullscreenShaderComponentsKeepTheirDeclarations",
    "SpawnerQualityAndBinderTargetsKeepTheirDeclarations",
    "CompilerGeneratedMembersKeepTheOriginalNames",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalFxComponentBehaviourTests." + name for name in (
    "FlashRenderersInvertEveryRendererOncePerInterval",
    "FlashRenderersFlashOnANaNTimer",
    "FlashRenderersEnableAllRenderersEnablesEach",
    "FlashRenderersOnEnableRunsTheBaseAndSchedulesDelayPlusInterval",
    "MaterialPropertyBlockSetterReplacesTheRendererBlock",
    "LerpColourIgnoresNearTargetsAndSetsTheFirstValueDirectly",
    "LerpColourTakesTheLerpBranchUnlessTheFirstValueIsPending",
    "LerpColourIteratorClampsAndEndsOnTheTarget",
    "LineRendererContainerAppendsUntilExactlyFullAndFlushes",
    "CanvasShaderModifierSetsTheOffsetUntilARendererHasNoMaterial",
    "PFXQualitySwitcherShowsTheVeryLowVariantOnlyAtLevelZero",
    "BlobShadowModifierTogglesTheCurrentCharactersShadow",
    "AssignAddressableTextureCallbackTargetsTheCamera",
    "InstancedObjectSpawnerTogglesAllButTheLastEntry",
    "InstancedObjectSpawnerThrowsOnTheEmptyMaterialListAndCleansUpChildren",
))
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OriginalLerpColourPlayTests.SetTargetColourStopsTheStoredRoutineAndLerpsFromTheCurrentValue",
    "ProjectLucid.Tests.OriginalLerpColourPlayTests.TheFirstStepAdvancesByScaledDeltaTimeTimesTheMultiplier",
)
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalFullscreenShaderComponentsTests." + name for name in (
    "AwakeTakesTheRegisteredManagerAndTheDurationDefaultsToOne",
    "SetDurationTreatsOnlyAnExactZeroAsUntilStopped",
    "StartEffectCountsAsPlayingEvenWhenTheOverrideFails",
    "StartEffectSetsPlayingAfterAnOverrideThatFinishesSynchronously",
    "StopEffectStopsTheTrackedOverrideOnlyWhilePlaying",
    "OnDestroyStopsAPlayingEffectUnlessItPersists",
    "UIBlurHoldsOneBlurRequestWhileEnabled",
    "UIBlurDisableWithoutAManagerDoesNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalParticleEffectBinderTargetTests." + name for name in (
    "ResetFillsOnlyAMissingParticleSystem",
    "SettersWriteTheirModuleProperty",
    "ChannelSettersClampOneChannelAndKeepTheOthers",
    "AChannelWriteFlattensAGradientStartColour",
))

# Restored Game.Runtime FX helpers: PrefabSpawner, PlayParticleSystemOnEnable,
# ShaderParameterBinderTarget, SceneMaterialManipulator, PrefabPoolActivation,
# RateOfChangeBindable and the projectile spawners. ShaderProperties was restored with the
# earlier FX helpers; only its published name is checked here.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalFxSpawnersAndShadersTests." + name for name in (
    "ShaderPropertiesPublishesTheDreamNoiseScaleOffsetName",
    "LoopOutputWrapsOutsideValuesByCeilingMultiplesOfTheRange",
    "PrefabSpawnerAwakeFallsBackToItsOwnTransformForAMissingPoint",
    "PrefabSpawnerParentsOrCopiesTheSpawnPointPoseForEveryPrefab",
    "ParticlePlayerFindsItsSystemOnResetAndPlaysItOnEnable",
    "BinderAwakeTakesTheSourceMaterialAndResolvesOnlyANamedProperty",
    "BinderSettersWriteThroughTheCachedPropertyId",
    "BinderResetKeepsAnAssignedMaterialAndNeedsARendererOtherwise",
    "MaterialAnimationEndingAtTimeZeroSetsNoColour",
    "MaterialTriggerTouchesOnlyMatchingConfigs",
    "MaterialTriggerEmitsTheInferredCallOrder",
    "PoolActivationSpawnsAtEveryLiveTransformWithTheDefaults",
    "PoolActivationResolvesOnlyAnEntityPoolAndToleratesNoLevelManager",
    "PoolActivationAwakeFailsWithoutALevelManagerAndQueuesNothing",
    "SpawnerConstructorDefaultsToEnemyMovementTimeAndBothReferences",
    "TriggerEntryResetsTheStayTimerThenFiresOnlyWhenConfigured",
    "TriggerStayWithoutFireOnStayLeavesTheTimer",
    "ResetElementReportsItsPositionAndUnityDestruction",
    "ResetElementEmitsACoolDownWithoutExceptions",
    "SpawnerAwakeEmitsTheTimeRequestBeforeTheManagerFetch",
    "StraightLineFireEmitsTheInferredCallOrder",
    "StraightLineFireEmitsTheHundredUnitAimDistance",
    "StraightLineProjectileEmitsWorldMovementAndASingleTransformFocus",
    "RateOfChangeBindableEmitsTheInferredOutputCalls",
    "CompilerGeneratedNamesMatchTheOriginalMemberOrdinals",
))

# Restored Game.Runtime HLAudio components and audio helpers over an owned AudioManager.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalAudioComponentPlaybackTests." + name for name in (
    "SfxCooldownDropsUpToTheWindowAndLetsNaNThrough",
    "SfxStopAndIsPlayingReadTheSourceThroughTheManager",
    "SfxComponentRequestsItsClipAndSetClipReplacesIt",
    "ButtonClicksRequestTheClipOnlyWhileEnabled",
    "ToggleOnRequestsTheClipAndOffOnlyASetOffClip",
    "RandomisedComponentsPickOneIdentifierPerRequest",
    "SpatialComponentMovesTheSourceBeforeRequestingTheOneShot",
    "AmbientLoadsItsClipAndPlaysItOnAwakeOnly",
    "AmbientSeekDrawsOnceAndADestroyedSourceIsSkipped",
    "AssignClipWaitsForTheManagerAndAssignsEvenANullClip",
    "ChangeMusicUsesItsTransitionOnlyWhenTheCustomFlagIsSet",
    "CharacterTriggerSubscribesBeforeWaitingAndPlaysOnEntry",
    "OneShotOnSourcePreloadsEveryIdentifierOnce",
    "OneShotOnSourcePlaysEveryOrOneLoadedClip",
    "OneShotOnSourceWaitsForMissingClipsBeforePlaying",
    "RandomLoopPicksBeforeGettingTheManagerAndLoopsTheClip",
    "PitchStepsThroughTheOffsetsAndWraps",
    "MixerAssignerSetsTheGroupOnlyWhenMixerAndGroupAreFound",
    "LowPassActivatorForwardsItsFilterToTheListener",
    "ShuffledLoopFollowsTheIntroSequenceOnlyWhenAskedTo",
    "ShuffledLoopUpdateIsGatedByTheIntroSequence",
    "SystemLookupsFaultOnTheirEmptyRows",
    "AmbientCooldownIsOrderedAndRepicksOnlyWithAChoice",
    "ShuffledLoopCooldownIsOrderedAndReshufflesAtLength",
    "ShuffledLoopPlaysOnlyWhileTheCharacterIsWithinTheOrderedRange",
    "AmbientDrawsThePanBeforeTheVolumeAndCentresItWhenOff",
    "FilterComponentSkipsNoneAndForwardsActivationToTheListener",
    "ConstructorsStoreTheirOriginalDefaults",
))

# Restored Game.Runtime animation helpers: AnimLayerWeightSetter, AnimParameterSetter,
# AnimationCustomDataCamera, AnimationCustomDataToMaterialProperty, AnimationEventListener,
# AnimationSampler, AnimationSpeedScaler, AnimationStartDelay, AnimatorBoolSetter and
# AnimatorCompleteAbilitySetter.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalAnimationHelperTests." + name for name in (
    "LayerWeightSetterWritesTheNamedLayerOnlyWithANameAndAnAnimator",
    "ParameterSetterWritesThroughTheWrappedHashOnlyForANamedParameter",
    "CameraDataOutputsAreTheMultiplierWhenOverriddenAndTheScaledValueOtherwise",
    "CameraDataPushesRawDepthOfFieldValuesIntoEveryDepthOfFieldComponent",
    "MaterialPropertyHashesTheEyeLiteralsAndANegativeIndexDoesNothing",
    "MaterialPropertyDrivesTheSharedMaterialOutsidePlayMode",
    "MaterialPropertyValidationClampsOnlyAnIndexPastTheEnd",
    "EventListenerInvokesTheLastActionIndexedForTheEventString",
    "SamplerPosesTheObjectAtTheFractionOfTheClipLengthAndDisablesTheAnimation",
    "SamplerValidationAddsAnAnimationOnlyWhenNoneIsFound",
    "SamplerValidationSamplesWithoutDisablingTheAnimation",
    "SpeedScalerLerpsTheClampedSpeedAndRewindsOnlyAnEnabledAnimationAtZero",
    "StartDelayPlaysAtOnceUnlessTheDelayIsPositive",
    "StartDelayIteratorWaitsTheCurrentDelayThenPlays",
    "BoolSetterHashesOnEveryEntryAndExitReusesTheCachedHash",
    "CompleteAbilitySetterResolvesTheBossOnEntryAndCompletesItsAbility",
))

# Restored Game.Runtime Animator helpers (lifetime object, boss ability setters,
# float modifiers, bindable, random trigger, sampler, events, camera and material drivers).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalAnimatorHelperTests." + name for name in (
    "LifetimeObjectInitialiseDisablesTheAnimatorAndRegistersEveryHazard",
    "LifetimeObjectResetRebindsTheAnimatorAndClearsElapsedTime",
    "LifetimeObjectUpdateStepsTheAnimatorAndDeactivatesOnlyASelfInitialisedExpiredObject",
    "LifetimeObjectEnableSelfInitialisesOnceThenRunsTheBaseEnable",
    "AttackStateSetterResolvesTheBossOnEntryAndStartsTheAttackAnimation",
    "TriggeredSetterSignalsTheTriggerOnEntryAndExitOfTheCachedBoss",
    "WindUpSetterWindsUpOnEntryAndWindsDownOnExit",
    "GetBoolEventFiresEachEdgeOnceAndTestsBothEdgesInOneFrame",
    "ModifyFloatNeedsBothNamesAndWritesTheProcessedSource",
    "NormaliseFloatClampsTheMagnitudeAndReturnsZeroForAnEmptyRange",
    "SmoothFloatAveragesTheRingOverTheConfiguredCount",
    "ParameterBindableKeepsTheOriginalGenericShape",
    "ParameterBindablePublishesTheFloatThroughTheAwakeHash",
    "RandomTriggerEntryWritesTheEntryIndexAndSchedulesFromRealTime",
    "RandomTriggerUpdateFiresOnlyAfterAPositiveScheduledTime",
    "RandomTriggerReadsTheRegisteredTimeManagerForItsCategory",
    "SamplerPlaysTheCurrentStateAtTheSampleFrameAndDisablesTheAnimator",
    "SamplerValidationFindsAChildAnimatorBeforeAddingOne",
    "SetBoolEventInvokesWithTheConfiguredStringAndBool",
    "SetFloatCurveEvaluatesNormalisedTimeAndSnapsToTheLastKeyOnExit",
    "SuppressWarningsResetFindsTheAnimatorAndAwakeSilencesIt",
    "CinemachineCameraTakesTheAnimatedFocalLengthAsATwentyFourMillimetreFieldOfView",
    "MaterialPropertyDataCachesEveryPairOnAwakeValidateAndUpdate",
    "MaterialPropertyDrivesTheIndexedMaterialOrEveryMaterial",
    "MaterialPropertyStartFillsAndDestroyReleasesTheMaterialList",
    "MaterialPropertyValidationClampsOnlyAnIndexPastTheEnd",
    "RendererEnabledStartsNotMetAndSwitchesOnlyOnAStrictCrossing",
    "RendererEnabledDisablesItselfWithoutAParameterAndResetFindsTheAnimator",
))

# Restored Game.Runtime InputHandler, GameInputSupplier and GameInputCharacterTriggerVolume.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalGameInputProviderTests." + name for name in (
    "InputHandlerDeclarationsKeepOriginalFlagsAndMemberOrder",
    "InputHandlerForwardsOnlyWhileRegisteredWithJoystickZero",
    "InputHandlerRegisterNeedsAnInputSystemAndRunsOnce",
    "SupplierDeclarationsKeepOriginalFlagsAndIteratorName",
    "SupplierEnableRegistersWithTheSystemMappingAndDisableUnregisters",
    "SupplierDisableSkipsADestroyedMapping",
    "SupplyInputWithoutACharacterManagerSuppliesNothing",
    "SupplyHeldInputHoldsWhileTimeRemainsThenReleasesWithZero",
    "TriggerVolumeDeclarationsKeepOriginalFlagsNestedTypesAndClosureName",
    "TriggerVolumeAwakeRegistersWithTheSystemMapping",
    "TriggerVolumeEnterPressesOnlyForItsSimulation",
    "TriggerVolumeHeldEntryPressesThenHoldsEachInputBeforeSubscribing",
    "TriggerVolumeDeferredReleaseReadsItsInputWhenItRuns",
    "TriggerVolumeUpdateHoldsEveryInputAndIgnoresDeltaTime",
    "TriggerVolumeHeldExitReleasesWithValueOneWithoutATimeManager",
    "TriggerVolumeExitPressesForTheExitSimulation",
    "TriggerVolumeStayReadsBoundsOnlyForAnUnfinishedDelay",
    "TriggerVolumeIsPausedStoresItsValue",
))

# Restored Game.Runtime InputEvent, InputEventSelectedOnly, RawInput and GameInputDisabledListener.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalInputEventTests." + name for name in (
    "InputEventDeclarationsKeepOriginalFlagsAndAttributeRows",
    "InputEventConstructorSeedsNoneImmediateAndTheAudioManagerReference",
    "InputEventEnableSubscribesItsVirtualOnInputOnButtonDown",
    "SuspendedInputEventDoesNothingUntilUnsuspended",
    "InputEventFetchesTheUIManagerBeforeReadingEitherList",
    "InputEventExclusiveContainersMustBeExclusivelyOpen",
    "InputEventStopsAtTheFirstOpenIgnoredContainer",
    "InputEventDefersToTheNextFrameWhenNotImmediate",
    "InvokeEventPlaysOnlyWithAClipASourceAndAManager",
    "InputEventPublishesTheUIEventUnderOnClick",
    "SelectedOnlyDeclarationsKeepOriginalFlagsAndAttributeRows",
    "SelectedOnlyRefusesWithoutNavigation",
    "SelectedOnlyChecksTheInteractableBeforeNavigation",
    "RawInputDeclarationsKeepOriginalFlags",
    "RawInputFiresOnceForNonZeroInputButFireAlwaysInvokes",
    "RawInputListensForSkipTheSplashPresses",
    "DisabledListenerDeclarationsKeepOriginalFlagsAndAttributeRows",
    "DisabledListenerAwakeTakesTheSystemMapping",
    "DisabledListenerEnableReportsTheCurrentStateAndDisableRestoresEnabled",
    "DisabledListenerFiresOnlyWhenTheWholeListChanges",
))

# Restored Game.Runtime GameInputButtonClick and GameActionButtonClick.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalGameButtonClickTests." + name for name in (
    "InputButtonDeclarationsKeepOriginalFlagsAndFieldOrder",
    "InputButtonConstructorSeedsDownAndAnEmptyBindable",
    "InputButtonEnableSubscribesTheSerializedInputWithItsTrigger",
    "InputButtonSetInputMovesTheSubscriptionAndZeroRestoresTheSerializedInput",
    "InputButtonOnInputClicksTheButton",
    "ActionButtonDeclarationsKeepOriginalFlagsAndIteratorNames",
    "ActionButtonConstructorSeedsTheLockTimeAndThreeBindables",
    "ActionButtonEnableCopiesTheSerializedValuesUnlessOverridden",
    "ActionButtonSettersStoreValuesAndNoneRestoresTheSerializedCombo",
    "ActionButtonSetActionKeepsALiveCoroutine",
    "ActionButtonBrainStateNeedsTheActionAndAnySetCombo",
    "ActionButtonCoroutineFaultsWithoutACharacterManager",
))

# Restored world-trigger components: AutoPlayTrigger(Conditional), EventCollisionTrigger,
# AutoRegisterHashedComponentGroup, DeactivateWhileContainersAreOpen and the small utilities.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalWorldTriggerSystemTests." + name for name in (
    "EventCollisionTriggerKeepsItsDeclarationsAndCachesTheIslandReference",
    "EnterAndExitInvokeOnlyForAPassingColliderAndMarkItActive",
    'StayRaisesEnterOrExitOnlyForAChangeWithMultipleTriggering(True,False,True,"enter:other,stay:other",True)',
    'StayRaisesEnterOrExitOnlyForAChangeWithMultipleTriggering(True,True,True,"stay:other",True)',
    'StayRaisesEnterOrExitOnlyForAChangeWithMultipleTriggering(False,False,True,"stay:other",True)',
    'StayRaisesEnterOrExitOnlyForAChangeWithMultipleTriggering(True,True,False,"exit:other",False)',
    'StayRaisesEnterOrExitOnlyForAChangeWithMultipleTriggering(True,False,False,"",False)',
    'StayRaisesEnterOrExitOnlyForAChangeWithMultipleTriggering(False,True,False,"",False)',
    "TriggersUpdateTheActiveSetBeforeRaisingTheirEvent",
    "StayReadsTheActiveSetBeforeTheFilterRuns",
    "DisableExitsEachLiveActiveColliderAndEnableForgetsThem",
    "ResetHooksInvokeTheResetEventAndReportTheTriggerState",
    "AwakeRegistersAsResetCapableAndWiresNothingWithoutResetListeners",
    "AwakeDefersTheLevelAndRespawnWiringWhenResetHasPersistentListeners",
    "LevelSubscriptionWaitsForTheNextActivationAndOnDestroyRemovesItWithResetListeners",
    "AutoRegisterGroupCachesTheIslandReferenceAndToleratesNoSections",
    "AutoRegisterGroupJoinsEverySectionContainingItAndLeavesThemOnDisable",
    "ContainerWatcherHidesItsObjectsOnlyForAListedContainer",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalWorldTriggerUtilityTests." + name for name in (
    "AutoPlayTriggersKeepTheirDeclarationsAndDefaultToThePlayerTag",
    "AutoPlayTriggerAcceptsItsTagOrAnyColliderWhenTheTagIsEmpty",
    "BoolEventProxyForwardsTheValueXorTheInverseFlag(False,False,False)",
    "BoolEventProxyForwardsTheValueXorTheInverseFlag(False,True,True)",
    "BoolEventProxyForwardsTheValueXorTheInverseFlag(True,False,True)",
    "BoolEventProxyForwardsTheValueXorTheInverseFlag(True,True,False)",
    "BoolEventProxyValidationReportsEachPersistentListenerTargetingItself",
    "CycleGameObjectListShowsOnlyTheWrappedIndex",
    "DelayedActivatorDefaultsAndStartsItsRoutineOnEnable",
    "DelayedActivatorDeactivatesAllThenActivatesPairsWithTheConfiguredWaits",
    "DelayedActivatorWaitsOnlyForAPositiveStartAndANonZeroGap(-1.0f,float.NaN,4)",
    "DelayedActivatorWaitsOnlyForAPositiveStartAndANonZeroGap(float.NaN,0.0f,2)",
    "DelayedActivatorWaitsOnlyForAPositiveStartAndANonZeroGap(0.0f,0.0f,2)",
    "DeleteChildrenRemovesClonesOrEveryChildWhenForced",
    "DestroyObjectDestroysAtOnceUnlessAPositiveDelayStartsTheRoutine",
    "DestroyObjectDestroysAtOnceWhileARoutineIsStored",
    "DestroyObjectRoutineWaitsThenDestroysTheGameObject",
    "EnumValuePairReturnsItsOwnKeyAndValue",
))

# Restored EntityActivationEvent, EntityActivator and the level and mission visibility entities.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalEntityActivationEntityTests." + name for name in (
    "EntityActivationEventOverridesInvokeOnlyTheirOwnEvent",
    "EntityActivatorRegistersWithItsHierarchyStateAndForwardsEvents",
    "EntityActivatorInitialiseRegistersThroughItsReferenceNotTheArgument",
    "LevelVisibilityEntityCachesBothRowReferences",
    "AwakeSubscribesToLevelActivationWhichRegistersTheEntity",
    "EntityManagerValidReregistersAtTheAwakeStateWithOnlyTheDefinedCallbacks",
    "EntityManagerValidLeavesAChildWhoseHierarchyStateMatchesTheAwakeState",
    "EntityEnableAndDisableSkipADestroyedEntityAndANullEvent",
    "OnDisableForwardsOnlyAnExternalDeactivationOfARegisteredEntity",
    "OnDisableIgnoresAChildHiddenOnlyByItsParent",
    "OnDestroyAndADestroyedLevelActivationUnsubscribe",
    "MissionVisibilityEntityKeepsDistinctMissionListsAndItsOwnAwakeState",
    "MissionVisibilityLevelActivationRestoresOnlyAChangedVisibility",
    "MissionVisibilityLevelActivationComparesTheObjectsOwnState",
))

# Restored EventInvocationInfo, the global signal publisher and receiver, MessageNameKeys,
# PersistentLevelObjectType`1/Boolean and the world-trigger components, timing and tracking utilities.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalEventInvocationInfoTests." + name for name in (
    "DeclarationsSupportedTypesAndConstructorStores",
    "DirectInvocationsSkipAMissingMethodAndPropagateFailures",
    "EnumInvocationBoxesTheParameterEnumAndFaultsWithoutAMethod",
    "TextInvocationConvertsSupportedParametersAndSwallowsEveryFault",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalGlobalSignalTests." + name for name in (
    "SignalComponentsKeepTheirDeclarationsAndDropASignalWithoutAManager",
    "ReceiverRespondsOnlyToItsSignalWhileEnabled",
    "ADeferredSubscriptionSurvivesDisableAndStartsWithTheManager",
    "MessageNameKeysHoldTheirOwnNames",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalPersistentLevelObjectTests." + name for name in (
    "DeclarationsAndConstructorDefaults",
    "AnUnsetIdAnnouncesTheDefaultWithoutTheLevelManager",
    "AnIdWaitsForTheLevelManagerAndSubscribesToLevelActivation",
    "ACurrentLevelIsHandledBeforeSubscribingAndChecksTheChallengeFirst",
    "SetPersistentStateStoresTheValueBeforeTheChallengeTest",
    "BooleanOverridesUseTheLevelSaveRecord",
    "OutsideAChallengeTheRecordIsWrittenBeforeTheStateChangeAndTheSaveIsRequestedLast",
    "AChallengeUsesItsOwnStateAndNeverReadsOrWritesTheSave",
    "ANonChallengeContextReadsTheSave",
    "TheSaveReadCreatesTheRecordWithTheDefaultValue",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalWorldTriggerComponentTests." + name for name in (
    "VisibilityPublishesTheFlagBeforeItsEvent",
    "ColliderGroupSetsEveryListedColliderAndStopsAtANullEntry",
    "HideOnAwakeDeactivatesTheRenderersObjects",
    "LevelDataActivationAppliesTheFlagAndItsInverse",
    "MaterialTriggerSendsEveryTriggerType",
    "MergeCollidersCombinesChildMeshesInWorldSpace",
    "SequentialActivatorActivatesOneObjectPerResume",
    "SiblingOrderEventMatchesFirstLastOrASpecificChild",
    "StickyControlsTriggerForwardsItsHooksAndEvents",
    "EnumMetadataValuesBindTheirEnumAndAssetMenu",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTimedEventActivatorTests." + name for name in (
    "DeclarationsAndConstructorDefaults",
    "TimedEventFiresUnlessTheClockIsBelowItsTime",
    "SequenceStartsAtMinusTheDelayUnlessStartedManually",
    "UpdateFiresElapsedEventsAndWrapsOrDisablesAtTheEnd",
    "UpdateUsesTheCountReadBeforeFiringAndANaNClockFiresEverything",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTargetTrackingUtilitiesTests." + name for name in (
    "DeclarationsKeepTheOutTargetAndOptionalDelegate",
    "TheConeGrowsByStepUntilATargetIsFound",
    "TheClosestValidTargetWinsAndALaterTieReplacesIt",
    "NoValidTargetClearsTheListAndReturnsNull",
    "TheScoreUsesTheProjectedPointsDistanceFromTheOrigin",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalSceneInformationTests." + name for name in (
    "DeclarationsKeepTheReadonlyPropertiesAndDefaultCount",
    "ChildCountAddsEveryDescendantToTheCount",
    "ConstructionNamesTheSceneAndCountsEveryObject",
    "ConstructionRequiresTheProfilerDataManager",
    "SnapshotsAccumulateAndOutputKeepsThem",
    "ProfilingIsForcedVisibleSnapshotsIncludeHiddenGroupsAndOutputHidesThem",
))

# Restored surface path generators, followers, animator and surface mesh generators.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalSurfacePathGeneratorTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "GetAxisPlacesBothValuesOnTheAlignedAxes(0,1.0f,0.0f,2.0f)",
    "GetAxisPlacesBothValuesOnTheAlignedAxes(1,1.0f,2.0f,0.0f)",
    "GetAxisPlacesBothValuesOnTheAlignedAxes(2,2.0f,1.0f,0.0f)",
    "GetAxisPlacesBothValuesOnTheAlignedAxes(3,0.0f,1.0f,2.0f)",
    "GetAxisPlacesBothValuesOnTheAlignedAxes(4,2.0f,0.0f,1.0f)",
    "GetAxisPlacesBothValuesOnTheAlignedAxes(5,0.0f,2.0f,1.0f)",
    "GetAxisPlacesBothValuesOnTheAlignedAxes(6,1.0f,0.0f,2.0f)",
    "GetAxisPlacesBothValuesOnTheAlignedAxes(-1,1.0f,0.0f,2.0f)",
    "GetOriginContinuesFromTheLastKnot",
    "LineStepsAlongItsTurnedForwardAndSkipsTheSharedStart",
    "LineTurnsByItsOrientationInsideTheOriginRotation",
    "SineAdvancesTheAngleAndTheDistanceTogether(XY,2.0f,2.0f,0.0f)",
    "SineAdvancesTheAngleAndTheDistanceTogether(ZY,0.0f,2.0f,2.0f)",
    "SineAdvancesTheAngleAndTheDistanceTogether(XZ,2.0f,0.0f,2.0f)",
    "EllipseStartsOnTheOriginAndFacesAlongItsTangent",
    "SpiralRisesAlongTheRemainingAxisAndTurnsAboutIt",
    "SpiralRotatesItsContinuedPointsAndTurnsByTheOriginRotation",
    "AnimationCurvePathAddsNoKnots",
    "RibbonPathsGenerateAsTheirSurfaceBases",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalSurfaceAnimatorTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "CachedValuesNeedAnInertSplineAndForOneKnotAnInertKnot",
    "SingleKnotTakesTheTargetPoseInSplineSpaceOutsideTheTolerance",
    "AllKnotsMoveTheSplineAlongOneOfItsAxes(0,3.0f,0.0f,0.0f)",
    "AllKnotsMoveTheSplineAlongOneOfItsAxes(1,0.0f,2.0f,0.0f)",
    "AllKnotsMoveTheSplineAlongOneOfItsAxes(2,0.0f,0.0f,1.0f)",
    "AllKnotsMoveTheSplineAlongOneOfItsAxes(3,0.0f,0.0f,0.0f)",
    "AllKnotsTakeTheAxisFromTheSplineTransform",
    "AllKnotsCompareTheSquaredOffsetWithTheTolerance(0.009f,False)",
    "AllKnotsCompareTheSquaredOffsetWithTheTolerance(0.011f,True)",
    "AnimatorSubscribesAndUnsubscribesWithItsOwnCategoryAndPhase",
    "AnimatorCachesAndUpdatesEveryKnotFromEachCallback",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalSurfaceObjectFollowerTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "ConstructorStoresTheNativeDefaults",
    "AwakeAddsResetTargetToTheLevelRestartAndOnDestroyRemovesIt",
    "OnDestroyWithoutALevelManagerLeavesNothingToRemove",
    "SetTargetPlacesTheFollowerFromTheChosenSource",
    "RotationFlipsOnTheFollowingUpdateOnceTheAngleExceedsTheLimit",
    "ReversedRotationAppliesTheInverseAfterTheSurfaceRotation",
    "RotationFlipNeedsAnOrderedGreaterAngle(float.NaN,False)",
    "RotationFlipNeedsAnOrderedGreaterAngle(179.0f,True)",
    "RotationFlipNeedsAnOrderedGreaterAngle(180.5f,False)",
    "SurfacesComeFromTheAttachedFacadesAndGizmosRefreshThem",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalSurfaceMeshGeneratorTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "SurfaceGeneratorStartsWithAnEmptyPathList",
    "AddQuadKeepsBothNativeTriangleOrders",
    "SpawnPrefabsClearsTheRootAndSkipsOnlyZeroSpacingOrAnEmptyRange",
    "SpawnPrefabsQueriesTheRibbonAtTheDistanceAndTheCrossSectionAtOffsetX",
    "PrefabSpawnerInstantiatesNothingInThePlayerBody",
    "SamplePointsCoverTheGridAndQuadsUseTheNativeIndex",
    "SkirtAlongZUsesThePreviousStepUv(0,0.0f)",
    "SkirtAlongZUsesThePreviousStepUv(3,1.0f)",
    "SkirtAlongXUsesThePreviousStepUv(0,0.0f)",
    "SkirtAlongXUsesThePreviousStepUv(2,1.0f)",
    "SkirtPointsWalkTheFourEdgesWithTheShippedIndices",
    "SkirtPointsKeepTheShippedIndicesOnANonSquarePlane",
    "UpdateSamplePointsNeedsBothAxisSplines(True)",
    "UpdateSamplePointsNeedsBothAxisSplines(False)",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalSurfaceMeshCrossSectionTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "TypesReturnTheirSerializedLiterals",
    "MeshGeneratorStartsWithEmptySectionAndPathLists",
    "PlaneOriginIsPulledBackAlongTheTiltedDirection",
    "PlaneSectionAddsTwoVerticesMovesTheKnotOriginAndJoinsTheNextKnot",
    "PlaneLocationFollowsTheTiltRotation",
    "EllipsePositionIsTheSineHeightAndTheCosineWidthOffset",
    "EllipseOriginStartsFromTheFirstSweptAngle",
    "EllipseSectionClockwiseSweepsToTheEndAngleAndTurnsTheKnot",
    "EllipseSectionAnticlockwiseSweepsBackwardsFromTheEndAngle",
    "EllipseLocationIsProportionalToTheArcLengthWithoutAStartOffset",
    "EllipseSectionTruncatesTheStepCountAndEndsAtTheRangeEnd",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTransformSurfaceTests." + name for name in (
    "DeclarationsOfTheStopAndTheTarget",
    "DeclarationsOfTheDescriptionAndTheCircle",
    "DeclarationsOfTheFollower",
    "StopConstructorStoresTheNativeDefaults",
    "StopInitialiseResetAndStopReachedHandleBothFlags",
    "StopActivateWaitsPastTheThresholdThenStartsEasing",
    "StopActivateTurnsTheTargetBackForEaseToLastAndNearest",
    "StopUpdateEasesToPausedThenRaisesTheEvent",
    "StopUpdateEasesToTheSurfaceEnd",
    "StopUpdateActivatesAPendingRequestOnlyBeforeTheThreshold",
    "DescriptionConstructorStoresTheNativeDefaults",
    "TimeAndSpeedLimitsFollowTheFollowMode",
    "SpeedLerpKeepsItsLimitsAndTimesTheCrossing",
    "ProgressByTimeForEachFollowMode",
    "TimeDistanceAndAngleLookups",
    "BaseSurfaceUpdateTeleportAndEvents",
    "SetPositionStoresTheLocalPositionOnlyWhenMoved",
    "OnValidateMeasuresAndSnapsPositionsAndSpeeds",
    "CircleConstructorStoresTheNativeDefaults",
    "CircleDistanceFollowsTheRadiusAndTheExtentsMultiplier",
    "CircleTimeByAngleIsRelativeToTheRange",
    "CirclePositionsAreBuiltInTheFollowersSpace",
    "CircleUpdateAdvancesAlongTheArcAndReportsTheOvershoot",
    "CircleRotationTurnsWithTheAngleAndTheDirection",
    "CircleSetPositionNeedsAMovedPositionOrAMatchingAxis",
    "FollowerConstructorStoresTheNativeDefaults",
    "FollowerSurfaceAccessAndExtents",
    "FollowerFindsTheSurfaceByDistance",
    "FollowerResetGivesEverySurfaceItsEndAndPreviousPoint",
    "FollowerResetChainsTheSpeedLimitsAcrossTheSurfaces",
    "FollowerResetRestoresTheTimeMultiplier",
    "FollowerIgnoresPlaybackOnAnInactiveObjectAndSetsEnabled",
    "FollowerOnDisableInvokesTheCompleteAction",
    "TargetStartModesPickTheInitialSurface",
    "TargetStartingReversedTurnsRoundOnce",
    "TargetSetDirectionPlayAndStop",
    "TargetReportsItsMovementToTheCollisionDataBelowIt",
    "TargetUpdateCarriesTheOvershootAcrossSurfacesAndPingPongs",
    "TargetUpdateLoopsOrFinishesAtTheEndOfTheChain",
    "TargetOrientatesWithTheOffsetRotationForTheDirection",
    "CircleBasisTurnsFromUpToTheAxisInTheFollowersSpace",
    "CircleRotationTurnsAboutTheAxisAndMultipliesInOrder",
    "TargetOffsetRotationFollowsTheSurfaceRotation",
    "TargetUpdateStopsAtTheJoinWhenTheFollowerIsDisabled",
    "TargetUpdateEndsAnEasedStopAtTheSurfaceEnd",
    "TargetFireEndEventsRaisesTheSurfaceEndOnlyWhenTheStopAllowsIt",
    "TargetUpdateSuppressesTheSurfaceEndAfterTurningRound",
    "ProgressByTimeClampsItsInterpolants",
    "ProgressAnimatedStartSpeedNeedsAPositiveFirstKeyTime",
    "FollowerResetGivesASingleSurfaceItsOwnEndSpeedAsTheNextSpeed",
    "TargetResetRestartsFromTheStartSurface",
    "FollowerResetTargetsEndsWithAnUpdateOfZero",
    "FollowerInternalUpdateScalesTheDeltaTimeByTheMultiplier",
    "DeclarationsOfTheLinePointSplineAndTransformDescriptions",
    "PointNeverOrientatesAndHasNoLength",
    "PointInitialiseLooksFromThePreviousPointAndEndsWhereItStarts",
    "PointAndTransformUpdateWaitForTheTotalTime",
    "PointRotationMultipliesInOrderAndTurnsRoundOnlyBackwards",
    "PointSetPositionStoresTheStartAndTheEndFollowsIt",
    "PointAndTransformOnValidateSwitchToTheTimeFollowMode",
    "TransformRotationsComeFromTheFollowedTransform",
    "TransformSetPositionFollowsTheTransformAndAlwaysStoresTheStart",
    "LineInitialiseStoresTheLookRotationsOnlyForALineWithALength",
    "LineInitialiseLooksAlongTheLocalDeltaOfARotatedFollower",
    "LineDistanceAndPositionsFollowTheStartAndEnd",
    "LineUpdateInterpolatesAndHoldsTheEndItRanTo",
    "LineUpdateThroughATransformUsesTheLocalPositions",
    "LineUpdateClampsTheProgressAndLerpsTheRotationOnRequest",
    "LineRotationMultipliesInOrderAndTurnsRoundOnlyBackwards",
    "SplineConstructorStoresTheSplineAndGetSplineReturnsIt",
    "SplineDistanceIsTheLengthOfTheSplineOrKeepsItsValue",
    "SplineRotationLocalIsRelativeToTheGivenTransform",
    "SplineRotationComesFromTheTrackerOrTheTransform",
    "SplineTeleportPlacesTheTrackerAndMovesItAgainstTheDirection",
    "SplineUpdateMovesTheTrackerByTheShareOfTheLengthCovered",
    "SplineOnValidateTrackerMeasuresTheEndAndRewinds",
    "SplineSetPositionAndSetPositionEndStoreTheGivenPositions",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalProcMeshPlacementUpdaterTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "ConstructorFaultsWithoutATimeManagerBeforeRegisteringItsShutdown",
))

# Restored trigger conditions, conditional trigger events and the small trigger components.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTriggerConditionTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "ParameterlessEvaluateNeedsAValidCurrentCharacterAndXorsTheInversion",
    "CharacterEvaluateXorsTheInversionForTheGivenCharacterOnly",
    "PersistentBooleanConditionInvertsOnlyOnTheParameterlessPath",
    "CharacterIdMatchesAnyListedId",
    "CharacterArchetypeMatchesAnyListedArchetype",
    "ControlsEnabledFollowsTheCharactersControlsModifier",
    "ActionStateReportsTheConfiguredActionsAppliedState",
    "VelocityMagnitudeComparesTheMagnitudeWithTheThresholdInOrder",
    "AbilityInUseAsksAboutTheSingleAbilityOrAnyOfTheGroup",
    "AbilityInUseGroupUsesEachGracePeriodAndStopsAtTheFirstAbilityInUse",
    "ScriptableObjectConditionAppliesTheAssetsInversionBeforeItsOwn",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalCachedTriggerConditionTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "EnablingSubscribesToTheCurrentCharactersPreFsmUpdate",
    "SubscribingAddsToThePreFsmListInsteadOfReplacingIt",
    "ALaterCharacterManagerStartupRegistersWithItsOwnCharacter",
    "AssetEvaluationNeedsAValidCurrentCharacterAndXorsTheInversion",
    "CachedAbilityInUseAnswersOnceUntilTheCacheIsReset",
    "CachedAbilityGroupRecomputesFromScratchAfterAReset",
    "CachedAbilityGroupUsesEachGracePeriodAndStopsAtTheFirstAbilityInUse",
    "CachedFsmBoolCachesTheStoredValueButNotTheComparison",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalValueTriggerConditionTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "CharacterFsmIntComparesTheStoredValueOrMinusOneWithTheThreshold",
    "CharacterFsmFloatComparesTheStoredValueOrZeroWithTheThreshold",
    "CharacterFsmBoolComparesTheStoredBooleanOrFalseAsZeroAndOne",
    "IntegerValueStartsFromItsStartValueAndAccumulates",
    "FloatValueStartsFromItsStartValueAndAccumulates",
    "BooleanValueIsReplacedByIncrementValue",
    "TimeValueKeepsItsCategoryAndStopsWithoutATimeManager",
    "TimeValueAddsTheFixedDeltaTimeOfItsCategoryBeforeEachWait",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalConditionalTriggerEventTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "ConditionsAreMetByAllByAnyAndByAnUnknownCheckType",
    "CountsReportTheLengthAndEveryPassingCondition",
    "EvaluateRaisesTheEventsOfTheMetStateInTheOriginalOrder",
    "DestroyRaisesTheNoLongerMetEventOnlyWhenRequestedAndMet",
    "OnlyAPersistentListenerMakesTheTriggerValid",
    "EnablingFollowsThePersistentObjectsAndLevelActivationUntilDisabled",
    "ALevelThatIsAlreadyActiveEvaluatesAgainWhenFollowed",
    "EnablingFaultsOnAConditionThatIsNotAPersistentBoolean",
    "ValidationReportsEveryConditionThatIsNotAPersistentBoolean",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTriggerEventComponentTests." + name for name in (
    "UnityEventTriggerDeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "EachLifecycleHookRaisesItsOwnEventAtOnce",
    "DelayedEventsAreDroppedWithoutTheCoroutineSystem",
    "ADelayedEventIsStartedOnTheStaticCoroutineHost",
    "TheDelayCoroutineWaitsForTheDelayBeforeRaisingTheEvent",
    "VisibilityEventGroupDeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "TheGroupRaisesWhenTheFirstBecomesVisibleAndWhenNoneIsLeft",
    "TheGroupSetsItsFlagBeforeRaisingAndClearsItAfterRaising",
    "TheRenderersAreCollectedFromTheActiveChildren",
    "TriggerCascadeDeclarationsAndTheMissingPoolFault",
    "TriggerSpawnsTheAmountAtTheOriginWithTheRotatedLaunchAndUnrotatedStartingVelocity",
    "TriggerUITransitionDeclarationsAndTheMissingAppFault",
    "TriggerUITransitionFaultsOnAMissingManagerBeforeTouchingTheAppStorage",
    "TriggerUITransitionKeepsOneMidpointHandlePerEnableInTheAppStorage",
    "TransitionAbilityKeepsTheAbilityTypeOfItsJsonArguments",
))
# Issue #312: Game.Runtime core FSM transitions built through their factories.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalCoreFsmTransitionTests.IsTvOsFiresOnlyWhenFalseCausesTheTransition",
    "ProjectLucid.Tests.OriginalCoreFsmTransitionTests.DevelopmentAndEditorOnlyNeverFireAndIgnoreTheirJson",
)
# Issue #314: state-message FSM nodes use the App storage, not the user's.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.PublishStateMessageAddsToAppStorageOnceAndLeavesTheUsersList",
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.RemoveStateMessageRemovesFromAppStorageAndAcceptsAnAbsentEvent",
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.StateMessageTransitionFiresOncePerPublishedAppEvent",
)
# Issue #320: Error state message and CheckCutscenePlaying truth table.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.ErrorStateLogsItsNameMachineUserAndMessageOnce",
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.CheckCutscenePlayingFollowsLoadingThenComparisonTruthTable",
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.CheckCutscenePlayingRejectsAnUnknownComparisonName",
)
# Issue #324: StopFullscreenShader stops overrides before dropping the handle.
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.StopFullscreenShaderStopsOverridesBeforeDroppingTheUsersHandle",
    "ProjectLucid.Tests.OriginalApplicationFsmGapTests.StopFullscreenShaderRunsOnItsTriggerEventAndClearsPendingStops",
)

# Restored char-core pickups, view targets, catch-release data and connector.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalCatchReleaseDefinitionTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "AttachConstructorStoresTheNativeDefaults",
    "CachedAlignmentCosineIsTheCosineOfTheToleranceInDegrees(0.0f,1.0f)",
    "CachedAlignmentCosineIsTheCosineOfTheToleranceInDegrees(60.0f,0.5f)",
    "CachedAlignmentCosineIsTheCosineOfTheToleranceInDegrees(90.0f,0.0f)",
    "CachedAlignmentCosineIsTheCosineOfTheToleranceInDegrees(180.0f,-1.0f)",
    "ReleaseConstructorStoresTheNativeDefaultsAndCachingChangesNothing",
    "CatchBuildsSeparateCatchPointsAndEvents",
    "EventsRaiseTheirOwnEventWithTheirArgument",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalCatchReleasePointsTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "ConstructorStoresTheNativeDefaults",
    "PointsAndPositionTypeDecideHasPointsAndIsPositionFree",
    "FreeAndClosestPositionTypesPickThePositionOrTheNearestActivePoint",
    "LastAndNextStepAlongThePointsFromTheClosestOne",
    "QuantizeClosestAngleTakesTheBestAlignedPointAndTheLastOfEquals",
    "AnUnsupportedPositionTypeThrowsOnlyWithPoints",
    "RotationTypesReturnTheRotationOfTheChosenPoint",
    "AReversedInvertedCatchTurnsThePointRotationAHalfTurnAboutGravity",
    "AnUnsupportedRotationTypeThrowsWithPoints",
    "FreeLookupSteersTheCharacterRotationFromTheInputAndTheAttachAngle",
    "ClosestRotationLookupPicksThePointFacingTheSteeredDirection",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalCatchConnectorTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "ConstructorStoresThePhysicsCategoryAndTheTimeManagerReference",
    "AwakeCachesTheAuthoredPose",
    "SetPivotsActivatesTheConnectorAndPicksTheAnchorAndItsBody",
    "ClearPivotsOnlyActsOnAConnectorThatSetPivotsSwitchedOn",
    "LateUpdateStretchesTheConnectorFromTheAnchorToTheCatchPoint",
    "LateUpdateExtendsTowardsTheCatchPointOverTheExtensionTime",
    "LateUpdateComposesTheLookRotationWithTheStartRotation",
    "LateUpdateFallsBackToTheConnectorsOwnTransformWithoutACatchTransform",
    "LateUpdateRotatesTheCatchOffsetByTheAnchorAndUsesTheAnchorBodyWhenPresent",
    "ConnectorSubscribesAndUnsubscribesForLateUpdateWithItsOwnCategory",
    "OnDisableReturnsWithoutATimeManager",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalViewTargetTests." + name for name in (
    "ViewTargetDeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "ConstructorStoresTheNativeDefaults",
    "AwakeHidesTheTargetAndRefreshRestartsTheTimer",
    "TheTargetSettersStoreTheTargetAndItsPosition",
    "SetupTakesThePositionFromTheParametersAndTheScaleFromTheTransform",
    "LateUpdateHidesTheTargetWithoutAPositionOrACameraManager",
    "WorldSpaceLateUpdateRefreshesThePositionAndPlaysTheCurves",
    "ATargetBehindTheCameraIsHiddenUnlessAllowed",
    "ScreenSpaceLateUpdateProjectsTheTargetAndFadesOverTheRenderRange",
    "ScreenSpaceWithoutARenderRangeStillProjectsAndNeverShortCircuits",
    "ScreenSpaceDepthBehindTheCameraHidesTheTargetUnlessAllowed",
    "IndicatorDeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "IndicatorConstructorStoresTheNativeDefaults",
    "IndicatorForwardsPoseToTheAnimatorAndShowsItOnlyDuringGameplay",
    "IndicatorSubscribesToGameplayControlOnSetupAndLeavesOnDestroy",
    "IndicatorSettersDriveTheirOwnAnimatorParameters",
    "IndicatorEnableRebindsTheAnimator",
    "IndicatorTargetMessagesLockOnAttackOrUnlock",
    "CheckIfVisibleOnlyRaycastsEveryNthCallWithACameraManager",
    "CheckIfVisibleCastsTheMainCameraRayThroughTheTarget",
    "CheckIfVisibleLimitsTheRayToTheDistanceOfTheSuppliedPosition",
    "CheckIfVisibleNeverRaycastsOnceTheCountIsPastTheThreshold",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalBossSupportTypesTests." + name for name in (
    "BossHomingTargetDeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "BossBurstableCrabManagerAndTriggerDeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "BossDebugAndVisualProxyModifierDeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "FourmenAndGuardianManagerDeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "FourmenVisualsDeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "BossHomingTargetConstructorKeepsTheIndicatorOnAndTheTargetTargetable",
    "BossHomingTargetAwakeWithoutAnIndicatorOnlyRunsTheHomingTargetAwake",
    "BossHomingTargetUpdatesChangeNothingBeforeAnIndicatorExists",
    "BossHomingTargetAwakeWithAnIndicatorInstantiatesAndSetsItUp",
    "BossHomingTargetEnablesAndDisablesItsIndicatorAndOnlyDisablingToleratesItsLoss",
    "BossHomingTargetWithAnAbilityGroupNeedsOneOfItsAbilitiesInUse",
    "BossHomingTargetWithoutAnAbilityGroupHitsThroughEitherCharacterCollider",
    "BossHomingTargetOnlyHitsWhileThePoolTargetsIt",
    "BossHomingTargetUpdateDecidesTargetabilityFromTheRayToItsIndicator",
    "BossBurstableComponentReportsTheHomingTargetNotTheColliderToItsBoss",
    "BossCrabManagerFlagsOnlyTheRegisteredTimedPoolOfItsWaveType",
    "BossCrabManagerDoesNothingWithoutAPrefabPoolManager",
    "BossManagerTriggerRaisesThePhaseContinueFlagOfTheManagerItStarted",
    "BossVisualProxyModifierActsOnlyOnTheBossOfItsDefinition",
    "BossDebugBindsItsInfoToggleToTheBossDebugInfoInput",
    "GuardianManagerClaimsTheFirstFreeRestPointInOrder",
    "GuardianManagerWithoutRestPointsHasNoFreeReturnPoint",
    "GuardianManagerRaisesFinalHitReadyOnlyWhenSubscribed",
    "GuardianManagerRegistersItselfOnAwakeAndLeavesOnDestroy",
    "FourmenManagerStartsWithAnEmptyBossesInMotionListOfItsOwn",
    "FourmenManagerRegistersOnAwakeTakesTheBossManagerAndLeavesOnDestroy",
    "FourmenManagerCountsTheActiveBossIndexUpAndResetsIt",
    "FourmenManagerCountsTheBossesAndFindsTheirIndex",
    "FourmenManagerConfiguresTheBossAtTheWrappedActiveIndex",
    "FourmenManagerKeepsEarlierActiveBossesActiveWhenConfiguringAnother",
    "FourmenManagerTracksTheActiveBossAndTheCompletedLoop",
    "FourmenManagerAveragesTheBossPositions",
    "FourmenManagerPairsEachBossLaserWithTheNextBossAroundTheRing",
    "FourmenManagerLetsALaterListingOfTheSameBossOverwriteTheEarlierPair",
    "FourmenVisualsAwakeDerivesTheDrillDepthsAndTakesTheTimeManager",
    "FourmenVisualsDrillWaitsForTheTimeManager",
    "FourmenVisualsDrillMovesTheHeightByTheElapsedFraction",
    "FourmenVisualsDrillEndsOnTheTargetHeight",
    "FourmenVisualsDrillSetsTheRotatorDirectionAndRestartsTheCoroutine",
    "FourmenVisualsHelmetPlaysTheOpenAndCloseClips",
    "FourmenVisualsSteadyFacesShowTheirMaterial",
    "FourmenVisualsFlashSwapsTheFacesOncePerElapsedInterval",
    "FourmenVisualsFlashingFacesStartOnTheirFirstMaterial",
    "FourmenVisualsFlashingFacesSwapAfterTheirOwnIntervalToTheirOwnSecondFace",
    "FourmenVisualsFlashDoesNotSwapWhenTheTimerEqualsTheInterval",
    "FourmenVisualsDangerousParticlesStopEmittingWithoutClearingTheMainSystem",
    "CompilerGeneratedMembersKeepTheirOriginalNamesAndHoistedFieldOrder",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalBossAbilityDefinitionTests." + name for name in (
    "BossAbilityDefinitionsKeepTheOriginalFlagsBasesAndTypeAttributes",
    "BossAbilityDefinitionsKeepTheOriginalFieldsInMetadataOrder",
    "BossAbilityDefinitionsKeepTheOriginalMethodsInMetadataOrder",
    "BossAbilityDefinitionsKeepTheirAssetMenuNames",
    "BossAbilityDefinitionsStartWithTheirNativeFieldDefaults",
    "BodySlamCalculatesItsSquaredRangeAndRadianAngles",
    "BossCrabClawAttackCalculatesTheSquaredNearRange",
    "ChargeAttackCalculatesTheSquaredDistanceAllowedFromOrigin",
    "MoveRandomCalculatesTheDistanceAllowedFromOriginInItsFourForms",
    "MoveRandomRoundsTheMultipliedMinimumToSingleBeforeSubtracting",
    "MoveToTargetCalculatesTheSquaredDistanceAllowedFromOrigin",
    "RetreatingJumpCalculatesTheSquaredJumpRange",
    "ScuttleSquaresEachOfItsFourDistancesIntoItsOwnField",
    "InRowsRebuildsTheLanesOfEveryAuthoredRow",
    "SpawnDataFillsEachLaneListFromItsOwnCsv",
    "SpawnDataChoosesTheReturnRowFromTheReturnableLanes",
    "EachDefinitionNamesTheAbilityScriptOfItsOwn",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalGuardianProjectileTests." + name for name in (
    "GuardianProjectileDeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "GuardianProjectileNestedTypesKeepTheOriginalShape",
    "ConstructorBuildsItsOwnEmptyTrackingCollections",
    "SettersStoreTheirValuesInTheirOwnFields",
    "UpdateFocusAimsAtTheTargetAtTheDefinitionVelocity",
    "SetTargetDirectionScalesTheDirectionWithoutNormalising",
    "ShouldTeleportToHitPointIsAlwaysFalse",
    "RestingAndExplodingProjectilesDoNotMove",
    "StraightMovementTranslatesInWorldSpaceByTheVelocityTimesTheStep",
    "StraightMovementStartsTheCullWhenTheDotProductIsNotBelowZero",
    "StraightMovementSkipsTheCullWhenAlreadyStartedWithoutACharacterOrWhileReturning",
    "OutboundArcMovementAdvancesTheAngleAgainstTheClockwiseFlag",
    "OutboundArcMovementStartsTheCullWhenItMovesAwayFromTheCharacter",
    "ReturningArcMovementSpiralsInTowardsTheReturnPoint",
    "ReturningArcMovementMeasuresInTheReturnTransformsSpaceAndAgainstTheOrigin",
    "ReturningFromOverheadReaimsAtTheReturnPointBeforeMoving",
    "LerpMovementEasesFromTheStartPositionAndArrivesPastOne",
    "LerpMovementAppliesTheEasingToTheProgress",
    "LerpMovementWithNaNProgressDoesNotArrive",
    "LerpMovementArrivesWithoutAnAuthoredEvent",
    "ForceEnterCoolDownLeavesReturningAndRestingProjectilesWhenExceptionsAreAllowed",
    "OnlyReturningProjectilesIgnoreThePlayersCollisionCollider",
    "FiredProjectilesDoNotHurtThePlayerWhileAHomingAttackIsInUse",
    "ReturningProjectilesStaggerTheBossTheyHit",
    "ReturningFromOverheadBouncesOffTheBossIntoARestPoint",
    "ReturningFromOverheadWithNoFreeRestPointKillsTheProjectile",
    "OverheadProjectilesExplodeInPlaceOnTheTrack",
    "DamageActionsReportTheHitToTheBoss",
    "UnexpectedDamageActionsThrowAnOutOfRangeException",
    "ReturnProjectileTurnsAFiredProjectileTowardsTheBoss",
    "FireReadsTheMovementAndDirectionFromTheBossStorageAndSetsUpTheArc",
    "FireWithAnEmptyBossStorageStartsAnAnticlockwiseArc",
    "SetSplineTracksTheWorldSplineWithOneTracker",
    "SplineMovementAdvancesAlongTheWorldSplineByVelocityTimesTheStep",
    "FinalHitReadinessInvokesTheAuthoredEventWhenThereIsOne",
    "OnDestroyUnsubscribesFromTheGuardianManagersFinalHit",
    "OnDestroyTreatsADestroyedGuardianManagerAsAbsent",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalCharCorePickupTests." + name for name in (
    "BreakableObjectDeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "BreakPlaysSpawnsHidesAndRaisesTheEventInOrder",
    "BreakSkipsDestroyedOptionalComponents",
    "ActorVisualProxyModifierDeclarationsKeepTheOriginalFlagsAndMethods",
    "TheModifierTogglesTheFormRenderersAndTheVisualProxy",
    "AnimationTriggerDeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "EnablingAndDisablingResetTheBookkeeping",
    "EnteringTracksAcceptedActorsOnceAndOnlyWhileEnabled",
    "LeavingForgetsOnlyPendingActors",
    "ComponentLookupOverloadsForwardTheLookupsActor",
    "PickupDeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "PickupConstructorStoresTheNativeDefaults",
    "SetAnimationReplacesTheDefaultClipWithTheWrapModeFromTheFlag",
    "IdleAndCollectStatesSwitchTheClipTheParticlesAndTheEffects",
    "InternalUpdateCountsTheTimersDownAndFiresEachAtZero",
    "EnablingIdlesTheAnimationMarksTheBindingValidAndSubscribesToDetectorMessages",
    "DisablingWithdrawsTheSubscriptionAndReleasesTheCullingGroup",
    "DisablingWithoutManagersStillReleasesTheCullingGroup",
    "CharacterActionsCollectOnEnterAndStartTheCountdownOnExit",
    "RechargingVisitsTheAirAbilitiesOfTheCurrentCharacter",
    "DetectorMessagesRegisterAndReleaseTheCharacterCallback",
    "SetRenderersEnablesOrDisablesEveryRenderer",
    "CullPlaysOrPausesTheParticlesAndTogglesTheRenderers",
    "SetupCustomCullingCreatesOneCullingGroupForTheCamera",
    "OnValidateAutoPopulatesTheRenderersWhenCustomBoundsAreOn",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalCharacterMuzzleTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsMembersAndAttributes",
    "MuzzleMirrorsTheCapturedRightPoseOntoTheLeftAndKeepsItsOwnCopy",
    "MuzzleNegatesExactlyTheXAndYRotationAndTheZScale",
    "ResetFindsTheFirstChildWithEachDefaultNameAndLeavesMissingOnesNull",
    "ResetOnAHostWithoutThoseChildrenLeavesATwoSlotArrayOfNulls",
    "ConstructorDefaultsTheThresholdToATenthAndResolvesBothManagerReferences",
    "AwakeBuildsOneMuzzlePerComponentFromScratch",
    "TheMuzzleSwitchesOnlyWhenTheCameraOffsetAlongForwardExceedsTheThreshold",
    "ASideOffsetOrAZeroOffsetNeverSwitchesAndTheRootsForwardDecidesTheSign",
    "AssignCameraStoresTheCameraAppliesTheSideAtOnceAndActivates",
    "AssignCameraWithTheCameraOnTheRootKeepsThePose",
    "AssignCameraOnADestroyedComponentChangesNothing",
    "LateUpdateAppliesTheSerialisedThresholdOnlyWhileActive",
    "WithoutValidManagersShutdownAndFinishingStopTracking",
    "EnableSubscribesToBothManagerReferencesAndDisableUnsubscribes",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalCharacterComponentTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsMethodsAndAttributes",
    "JumpTrackerStoresTheEnteringCharacterAndStartsNothing",
    "JumpTrackerFixedUpdateDoesNothingForACompletedJumpOrWithoutACharacter",
    "FootstepsFollowTheInitialisedCharactersCollisionEvents",
    "FootstepsWithoutTrackedSurfacesRestoreTheDefaults",
    "FootstepsIgnoreSurfacesWithoutEffectsAndRecalculateOnlyWhenATrackedSurfaceLeaves",
    "FootstepsTheFirstTrackedTerrainEffectOverridesTheDefaults",
    "ProxyAssignAlwaysClearsTheTargetOnExit",
    "ProxyAssignTargetsTheCurrentCharactersTransformOnlyWhileOneIsValid",
    "SplineAttachDefaultsToLegacyLogicAndOneDirection",
    "SplineAttachWithoutASplineNeitherNamesAKeyNorTouchesTheStorage",
    "SplineAttachStoresTheSplineUnlessAOneDirectionalTriggerIsBehindTheCharacter",
    "SplineAttachExitRemovesTheStoredValueOnlyWhenItHasASpline",
    "SplineAttachStoresAsISplineAndRemovesAsISurface",
    "SplineAttachActivateSplineSetsTheSplinesActiveState",
    "MagnetismStartsWithThreeSeparateFlatCurvesOfOne",
    "MagnetismEnterIgnoresAnythingButTheCurrentCharactersCollider",
    "MagnetismEnterCapturesTheTriggersUpAndRebuildsTheFrame",
    "MagnetismEnterProjectsTheTravelDirectionOntoTheTriggerPlane",
    "MagnetismStayDoesNothingWhileNoAbilityOfTheGroupIsInUse",
    "MagnetismStayMovesTheCharacterWhenNoAbilityGroupIsSet",
    "PauseGameDoesNothingWithoutAValidCurrentCharacter",
    "RemoveTimeOverrideClearsTheHandleAndReportsWhetherThereWasOne",
    "PauseOnDisableReleasesTheHandleOnlyThroughARegisteredTimeManager",
))

# Original catch-release definition, character triggers, direction influencers, conditional trigger events,
# climbing collision tracker and character events (gr2-char-core-02).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterTriggerComponentTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsMethodsAndAttributes",
    "ConstructorWritesTheNativeDefaultsAndBuildsTheThreeParts",
    "CachedValuesStoreTheCosineOfTheAlignmentToleranceInSinglePrecision",
    "EachCallbackSetsOnlyItsOwnTriggerAndTheBoolFollowsEnterAndExit",
    "UnnamedParametersAreSkipped",
    "FilterPassesEveryoneUnlessOnlyTheCurrentCharacterIsWanted",
    "FilterRejectsEveryActorWhileNoCharacterManagerIsRegistered",
    "AutoRailHoldsOneTrueOverrideWhileInsideAndReleasesItOnExit",
    "ResetRestoresTheStaminaAndCancelStartsTheCooldown",
    "StayFiresOnlyWhenContinuousAndTheCooldownDefaultsToOne",
    "AnUndeclaredTriggerTypeRaisesArgumentOutOfRange",
    "SingleSidedPassesAStationaryCharacterOrOneNotMovingAlongUp",
))
# Original stamina triggers, spline attach triggers, nested conditions, type collision trigger and the local
# character modifiers, homing attack, damageable object and surface teleport (gr2-char-core-04).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterStaminaTriggerTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsMethodsAndAttributes",
    "ConstructorsKeepTheNativeDefaults",
    "SplineAttachKeysAreTheRailAndTransporterAutoAttachKeys",
    "RailAttachIsSkippedWhileARailIsActiveAndOtherwiseAttaches",
    "TransporterAttachIsBlockedOnlyByTheTransporterAbilityWithoutGrace",
    "TypeCollisionAcceptsOnlyTheCurrentCharactersColliderOfAWantedType",
    "NestedConditionsCombineByTheirOwnCheckType",
    "ChaosTriggerStaminaIsTheCurrentCharactersChaosStamina",
    "ChaosTriggerDoesNothingWithoutACharacterOrAnAbility",
    "ChaosTriggerActivatesTheChaosStaminaWithTheBurstDefinitionAndNotifiesTheAbility",
    "StartRegistersTheCharacterChangeHandlerAndDestroyReleasesIt",
    "CharacterChangeStoresTheCharactersAbilityOfTheTriggersTypeAsT",
    "StartWithAValidCharacterStoresItsAbilityAtOnce",
    "DestroyWithoutAManagerReleasesNothingAndReportsNothing",
    "TypeCollisionWithoutAManagerMatchesNothingAndReportsNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterLocalComponentTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsMethodsAndAttributes",
    "ADamageableObjectRaisesItsHitEventOnlyWhenHit",
    "InitialiseSizesTheBoxFromTheAbilityAndCentresItHalfADepthAhead",
    "SetColliderDimensionsCentresTheBoxHalfItsDepthAheadOfTheOffset",
    "TriggerCallbacksForwardToTheAbilityAndAddOrRemoveTheEnabledTargets",
    "TheUiActionsToggleTheCurrentCharactersUi",
    "TheVisualProxyActionsToggleTheCurrentCharactersRenderersAndProxy",
    "LevelManagerCallbackStoresTheManagersItIsGiven",
    "TeleportStopsWithoutACurrentLevelAndAwakeTriggersIt",
    "ResetAndValidateDeriveTheColliderAndTheRayDistance",
    "ThePlayersColliderEntersAndLeavesTheTeleport",
    "DoTeleportMovesTheCharacterToTheFarSurfaceAlongItsDirection",
    "DoTeleportCastsAlongTheCharactersRotatedDirection",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterDirectionInfluencerTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsMethodsAndAttributes",
    "ConstructorsWriteTheNativeDefaults",
    "SelfDirectionsFollowTheTransformAxes",
    "AnUndeclaredDirectionTypeRaisesArgumentOutOfRange",
    "SurfaceForwardIsTheNearestLocationRotationAppliedToForward",
    "SplineAndRibbonDirectionsGoThroughTheSurfaceForward",
    "TheCharacterWorldPositionIsTheOnlyPositionPassedToGetDirection",
    "SpeedIsTheBlendCurveAtTheTimeInContactTimesTheSpeed",
    "FalloffSpeedScalesTheBaseSpeedByTheCurveAtTheSnappedDistance",
    "EnterRegistersTheCharacterAndAddsTheInfluenceAndTheVelocityCap",
    "EnteringTwiceFaultsOnTheDuplicateKeyBeforeAddingModifiers",
    "StayAdvancesTheTimeInContactAndUpdatesTheHeldInfluenceInPlace",
    "ExitReleasesBothOverridesAndForgetsTheCharacter",
    "CollisionCallbacksForwardTheFoundCharacterToTheTriggerCallbacks",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterEventsTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsMethodsAndClosures",
    "ConstructorWritesTheNativeDefaults",
    "AwakeResolvesTheManagerThroughTheBaseAndWiresNothingWithoutListeners",
    "AwakeAndDestroyWireTheLevelAndDamageEventsThroughTheirLambdas",
    "CallbacksInvokeTheirEventsAndTolerateUnsetOnes",
    "StayRequestsTheDelayedCallOnlyWithADelayTime",
    "TheDelayedCallRaisesTheDelayedEventsWithTheCapturedCharacter",
    "AirAbilitiesExitEndsOnlyTheAirActivateImpulse",
    "ResetSpinDashChargeDisablesTheCharge",
    "AirAbilitiesResetResetsHomingAbilitiesAndPublishesTheFlag",
    "AbilityLambdasAndOverloadsCallTheOriginalTargets",
    "FormResetsSwitchToTheDefaultFormWithTheNativeLockArguments",
    "ControlsAreDisabledByAFalseOverrideAndEnabledByRemovingIt",
    "TargetingHoldsAtMostOneFalseOverride",
    "InnerVolumeTargetingIsAStoredFlag",
    "AnAbilityDefinitionOverrideIsRecalculatedAndStored",
    "SpeedEventsReceiveTheCurveValueAtTheCharactersSpeed",
    "ResetTransformReturnsTheTargetToItsParent",
    "TargetIsTheFlattenedForwardOfTheCameraOrTheCharacter",
    "RotationOnAPlaneLooksAlongTheFlattenedForwardWithThePlaneUp",
    "RotationWithoutARibbonLooksAlongTheFlattenedForwardWithTheCharacterUp",
    "AbilityUpdatesAreSwitchedForTheListedTypesAndTheGroup",
    "ActionBodiesMakeTheNativeCalls",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterConditionalTriggerTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsMethodsAndClosure",
    "ConditionsAreCombinedByTheCheckType",
    "FilterAcceptsOnlyTheCurrentCharactersColliderWhileTheConditionsHold",
    "EnableClearsTheActiveCollidersAndMakesTheCollisionColliderModifiable",
    "EnterAndExitSubscribeToTheCharactersFixedUpdateIndependentlyOfTheConditions",
    "StayIsDrivenByTheColliderCallbackOnlyWithoutACollisionCollider",
    "TheFixedUpdateCallbackRunsTheStayHandlingForTheCharactersCollider",
    "DisableUntracksTheCurrentCharacter",
    "TheRegisteredCheckReadsTheTriggersCurrentConditions",
    "TrackingRegistersTheCollisionColliderCheckWithTheTrackManager",
    "ValidationReportsTheFourTriggerTypesTheFsmAlreadyFires",
    "CutsceneActionsLoadTheFirstValidCutsceneWithOrWithoutPlayback",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterClimbingCollisionTrackerTests." + name for name in (
    "DeclarationsKeepTheOriginalFlagsFieldsAndMethods",
    "ConstructorStoresTheCharacterAndTheLayerMask",
    "StartSubscribesToTheEnterAndStayEventsAndStopClearsTheState",
    "UpdatePublishesTheAveragedNormalOfTheContactsAndResetsTheAccrual",
    "TerrainIsClimbableOnlyOnAMaskedLayerAndWhenMarkedClimbable",
    "ContactCallbacksAccrueTheNegatedNormalsAndEnterRecordsTheVelocity",
    "GroundCheckAndUpdateUseTheNativeCalls",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterCatchReleaseTests." + name for name in (
    "DefinitionStartsWithAUnitGravityMultiplier",
    "DefinitionCachesTheCosineOfTheAttachAlignmentTolerance",
    "DefinitionCachingFaultsWithoutData",
    "DeclarationShapeDefinitionNamesItsAbilityScript",
    "TransitionIsRegisteredWithItsArgumentType",
    "TransitionReadsTheReleaseTypeAndInverseFlagFromItsJson",
    "TransitionComparesTheAbilitysReleaseTypeAndInvertsOnlyWhenTheAbilityExists",
    "AbilityMatchesTheReleaseInputTypeOfItsDataOnly",
    "AttachTimeCountsOnlyWhileTriggered",
    "EntryNeedsTheAlignedSpeedOrTheFullSpeedWhenTheMagnitudeIsUsed",
    "EntryChecksTheAlignmentConeOnlyWhenAToleranceIsSet",
    "EntryAtTheSpeedMinimumAndOnTheConeBoundaryStillAttaches",
    "EntryUsesTheTriggersForwardWithoutAToleranceAndItsRotatedAlignmentDirectionWithOne",
    "EntryIsIgnoredWhileTriggered",
    "ReversedEntryRaisesTheReversePointsEventWithANegatedNormalisedSpeed",
    "TheAttachCurveFeedsTheEnterEventAndTheReleaseCurveTheReleaseSpeed",
    "ReversalFollowsTheCharactersForwardWhenNotOrientatedFromVelocity",
    "EntryStoresTheReleaseSpeedAndTheRemainTimerOnlyForANonZeroRemainTime",
    "EntryRemembersTheCatchPointAndSnapsTheCharacterToItWhenAsked",
    "DynamicEntryResetsTheTriggersLocalPosition",
    "UpdateDerivesTheVelocityFromTheMovementSinceTheLastUpdate",
    "AttachParentsTheCharacterToTheTriggerAtTheOffsetBelowTheCatchPoint",
    "DynamicAttachMovesTheTriggerToTheCharacterAndRaisesTheConstraintEvent(False)",
    "DynamicAttachMovesTheTriggerToTheCharacterAndRaisesTheConstraintEvent(True)",
    "ExitDoesNothingWhileNotTriggered",
    "ReleaseTransformLaunchesAlongTheFacingAtTheReleaseSpeed",
    "ReleaseTransformScalesTheVelocityByTheMultiplierAtTheAttachTime",
    "ReleaseTransformReevaluatesTheSpeedAtTheWorldSpeedOnlyWhenAsked",
    "ReleaseTransformOffsetsAFixedPositionByTheAttachOffsetTurnedAgainstGravity",
    "RemovingTheParentConstraintsPlacesTheCharacterAtTheFakeParentAndStopsIt",
    "ExitWithBlockedModifiersLeavesTheAbilityWithoutReleasingTheCharacter",
    "ReleaseClearsTheConnectorBeforeExiting",
    "FormAndAnimationComeFromTheDataWhenItSetsThem",
    "TriggerBuildsItsOwnDataAndResetEvent",
    "LevelActivationRaisesTheResetEventWhileAResetDoesNot",
    "ExitEndAndSnapIgnoreAMissingAbilityAndForwardToAnExistingOne",
    "EntryTriggersTheCharactersAbilityDirectlyWhenNoInputIsRequired",
    "EntryKeepsAnAlreadyTriggeredAbilityWithoutEnteringItAgain",
    "StayWaitYieldsTwoFramesBeforeItReadsTheAttachInput",
    "StayAttachesOnTheInputAndStopsWithoutReleasingForANonContinuousInput",
    "StayReleasesTheAbilityWhenAContinuousHoldEnds",
    "StayReleasesTheAbilityWhenTheCharacterVanishesDuringAContinuousHold",
    "StayInTheVolumeDoesNotReenterWhileTheInputWaitIsRunning",
    "StateIsRegisteredAsAGraphNodeWithTheAbilityArguments",
    "StateBodiesCallInTheNativeOrder",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterClimbingStateTests." + name for name in (
    "DeclarationShapeClimbingKeepsItsGenericConstraintsAndConstants",
    "ClimbingBoostIsARegisteredNodeWithItsOwnFourHitBuffer",
    "SnapDistanceScalesWithTheStepAndHasAFloor",
    "FinishSnapshotAndRestoreUseTheClimbingRecords",
    "InitialVelocityIsTheLocalHorizontalVelocityOfTheMaintainedOrStoredOne",
    "InitialVelocityWithoutARecordIsZeroAndStoresTheDefault",
    "OrientationIsLimitedByTheMaximumRotationAndLooksAgainstGravity",
    "NothingToClimbGivesNoHit",
    "SurfaceOutsideTheClimbingAnglesIsRejectedButReportedAsTheFirstSlot",
    "OnlyCollidersTheTrackManagerMarksClimbableCount",
    "CastLengthIsTwiceTheSnapDistanceFromAboveTheCharacter",
    "TheLocalOffsetIsRotatedWithTheCharacter",
    "LimitedRotationRejectsAnAngleChangeBeyondTheMaximum",
    "SnappingWithoutAClimbablePositionFailsWithAZeroNormal",
    "SnappingOntoAWallPlacesTheCharacterJustIntoTheSurface",
    "SnappingTurnsTheCharacterToTheSurfaceByTheLimitedOrFullAngle",
    "VelocityFollowsTheInputWhenTheWholeStepIsClimbable",
    "VelocityFallsBackToTheForwardThenTheLateralThenNoStep",
    "VelocityIntoAnEdgeNormalIsRemovedAndTheNormalsAreCleared",
    "ClimbSpeedParametersAreTheLocalSpeedsOverTheirMaxima",
    "ClimbingBodiesCallInTheNativeOrder",
    "ClimbingBodiesTouchTheNativeStorageRecords",
    "ClimbingBoostBodiesCallInTheNativeOrder",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterJumpLedgeStateTests." + name for name in (
    "JumpStatesReadTheirArgumentsFromJson",
    "TheOtherStatesTakeTheAbilityTypeFromTheirJson",
    "JumpAddsThePowerCurveAlongTheJumpDirection",
    "ClearingTheTargetSplineRemovesTheDashTarget",
    "TheDashProxyIsTippedAQuarterTurnAndLaterReleased",
    "JumpEntryFollowsTheNativeOrder",
    "JumpUpdateFollowsTheNativeOrder",
    "JumpLeaveFollowsTheNativeOrder",
    "HalfPipeTransferStateFollowsTheNativeOrder",
    "KnockbackRecoveryStateFollowsTheNativeOrder",
    "LedgeMountStateFollowsTheNativeOrder",
    "LightspeedDashEntryFollowsTheNativeOrder",
    "LightspeedDashUpdateFollowsTheNativeOrder",
    "KnockbackRecoveryClampsTheBackwardSpeedWithTheNativeOperandOrder",
    "JumpKeepsJumpingByTheNativeKeysAndComparisons",
    "JumpEntryRemovesOnlyAnApproachingConnectionAndRaisesOnlyBelowTheUpwardFloor",
    "LedgeMountUsesTheNativeKeysVelocityComponentsAndPauseComparison",
    "LightspeedDashClearsTheTargetOnlyWhenTheLookAheadLeavesOrInvulnerabilityBegins",
    "LightspeedDashLeaveFollowsTheNativeOrder",
    "TheProxyOverrideMultipliesTheCharacterRotationByTheQuarterTurn",
    "NodesCarryTheirOriginalGraphAuthoringAttributes",
    "ClassFactoryRegistersTheStatesWithTheirArgumentTypes",
    "DeclarationShapeStatesKeepTheirFieldsBasesAndArgumentClasses",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterJumpLedgeTransitionTests." + name for name in (
    "HasAbilityTypeIsTrueWhenThePresenceDiffersFromTheInverseFlag",
    "LedgeBufferEndEndsWhenTheAirAbilityIsIdleOrTheTimerReachedTheDuration",
    "JumpFiresOnlyWhileTheAbilityJumpsAndNoStateTimerRuns",
    "TheJumpDirectionIsWorldUpOnRailsAndCatchReleaseAndTheContactNormalOtherwise",
    "ANonZeroExtraAngleRotatesTheDirectionAwayFromGravity",
    "TheFaceVelocityDirectionRecordFollowsTheConfiguredFlag",
    "EachJumpTransitionLooksForItsOwnAbilityClass",
    "LightspeedDashNeedsTheAbilityATrackManagerAndAStoredTarget",
    "LightspeedDashExitsWhenNoTargetSplineIsStored",
    "TheDashTransitionsReadTheTargetAsDifferentTypes",
    "TheJumpUpdateFollowsTheNativeOrder",
    "NodesCarryTheirOriginalGraphAuthoringAttributes",
    "ClassFactoryRegistersTheNodesWithTheirArgumentTypes",
    "DeclarationShapeTransitionsKeepTheirFieldsBasesAndArgumentClasses",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterLedgeMountInHubAbilityTests." + name for name in (
    "LedgeMountDefinitionStoresTheNativeDefaults",
    "InHubDefinitionStoresTheNativeDefaults",
    "DefinitionsNameTheirOwnAbilityScript",
    "DeclarationShapeDefinitionsKeepTheirFieldsAttributesAndMenus",
    "InHubOwnsItsOwnVelocityStorage",
    "InHubInitialiseFollowsTheNativeOrder",
    "ALedgeMountStartsUntriggeredWithZeroVectors",
    "LeavingClearsOnlyTheTrigger",
    "NothingInReachLeavesTheAbilityUntriggered",
    "ATiltedSurfaceOutsideTheClimbLimitsIsTheMountableLedge",
    "FlatGroundIsNotALedge",
    "IgnoreHitsBehindDropsHitsAtOrBehindTheCharacter",
    "ReverseSearchOrderDecidesWhichRayIsTriedFirst",
    "ANonMountableLedgeIsAppendedToTheStoredEdgeNormals",
    "TheCollisionDataAndTheClimbableFlagDecideBetweenMountableAndNonMountable",
    "LedgeMountUpdateFollowsTheNativeOrder",
    "LedgeSearchChecksEachHitInTheNativeOrder",
    "DeclarationShapeAbilitiesKeepTheirFieldsAndAccessors",
))

EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalSoftMaskTests." + name for name in (
    "MaskChannelPresetsAreTheNativeConstants",
    "ImageTypesMapToBorderModesAndOnlyThreeAreSupported",
    "MathrRemapsBetweenRectsInsideIsInclusiveAndBordersShrinkTheRect",
    "ANewSoftMaskStartsWithTheNativeFieldDefaults",
    "SpritePixelsPerUnitMultiplierIsClampedAtOneHundredth",
    "ReplacerChainRunsInAscendingOrderAndSkipsNullAndDestroyedResults",
    "MaterialReplacementsShareOneReplacementPerOriginalUntilTheLastRelease",
    "TheTextMeshProReplacerIsGlobalOrderTenAndIgnoresOtherShaders",
    "TheDefaultUiMaterialIsReplacedByTheResourcesShader",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalSoftMaskTests.TheResourcesShadersMatchTheShippedCompiledShaders(" + a + ")" for a in (
    '"SoftMask","Hidden/UI Default (Soft Masked)",False',
    '"SoftMaskETC1","Hidden/UI Default ETC1 (Soft Masked)",True',
    '"SoftMaskPremultipliedAlpha","Hidden/UI Default with Premultiplied Alpha (Soft Masked)",False',
    '"SoftMaskETC1PremultipliedAlpha","Hidden/UI Default ETC1 with Premultiplied Alpha (Soft Masked)",True',
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckMathTests." + name for name in (
    "TranscendentalWrappersReturnTheRuntimeMathValues",
    "ClampFamilyKeepsTheNativeComparisonOrder",
    "LerpAndInverseLerpClampTheirParameter",
    "LerpVector3ClampsTAndWidensToDoubleForTheArithmetic",
    "RoundSendsHalvesToEvenAndOtherwiseFloorsAddedHalf",
    "CeilAndFloorRoundTowardTheirDirection",
    "MoveNeverOvershootsAndAbsIsMagnitude",
    "AddInsertAndRemoveAtReplaceTheArray",
    "ContainsAndIndexOfUseEqualsAndMissingValuesAnswerZero",
    "ContainsSkipsElementsWhoseEqualsFaults",
    "SetLengthCopiesTheOverlapAndForEachWalksInOrder",
    "ShiftLeftRotatesAndShiftRightKeepsTheNativeAscendingSmear",
    "QuickSortSortsInPlaceAndComparesAgainstExactlyMinusOneAndOne",
    "DuplicateCurveCopiesKeysAndWrapModesAndGradientAnswersNull",
    "ProjectOnLineClampsToTheSegment",
    "VectorInverseLerpIsAnUnclampedDotRatio",
    "DistanceOnSphereUsesTheNativeThresholdsAndChordFormula",
    "FlattenVectorReplacesOneComponent",
    "BoundsConstructorsDeriveTheirFieldsLikeTheNativeBodies",
    "BoundsContainsIsInclusiveAndAcceptsNaN",
    "BoundsKeepsTheSerializedFieldOrder",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckMeshTests." + name for name in (
    "DefaultsAreEmptyArraysAndAZeroBounds",
    "CreateFromMeshReadsEveryChannelAndAppendsSubmeshes",
    "ClearResetsTheGeometryButNotTheFormatFlagOrWorkArrays",
    "WriteMeshAllocatesAndSkipsChannelsThatDoNotMatchTheVertexCount",
    "CopyDeepCopiesTheArraysAndBoundsButNotHasUpdate",
    "AbsorbReusesMatchingArraysAndReplacesBounds",
    "CombineRebasesTrianglesAndSizesEveryChannelToTheVertexTotal",
    "AddMeshesAppendsAfterTheBaseAndDropsOrFaultsOnBaseSubmeshes",
    "FlipTrianglesAndFlipFacesSwapTheOuterIndices",
    "TransformHelpersApplyPointAndVectorMatrices",
    "TransformMeshOnASnapshotWithoutVerticesOrNormalsReturnsSilently",
    "TransformMeshOnAUnityMeshReplacesNormalsWithTransformedVertexVectors",
    "CalculateTangentsAccumulatesIntoTheStaticScratchArrays",
    "GeneratePlaneTrianglesWalksRowsAndSkipsRowEnds",
    "TriangulateClipsEarsAndOnlyReversesWhenItFinishes",
    "AreaSnipAndInsideTriangleKeepTheirNativePredicates",
    "MakeDoublesidedAppendsBackFacesToASnapshot",
    "MakeDoublesidedAppendsBackFacesToAUnityMesh",
    "MakeDoublesidedHalfFillsTheSecondHalfInPlace",
    "BreakMeshSplitsEveryTriangleAndAveragesColors",
    "CopyReproducesTheChannelsAndSubmeshesOfAUnityMesh",
    "ToOBJStringWritesTheNativeLayoutAndNormalizesTheDecimalSeparator",
    "MatrixAccessorsReadAndWriteTheTranslationRotationAndColumnLengths",
    "HierarchyCountsAndAncestryFollowTheTransformTree",
    "MergeBoundsEncapsulatesChildMeshesAndFiltersNamesOnlyAtTheFirstLevel",
    "TransformSnapshotReadsTheLiveTransformAndDefaultsWithoutOne",
    "SettersChooseWhichSideUpdatePushesAndUpdateClearsTheFlags",
    "LossyScaleSetterRaisesTheScaleFlagAndUpdatePushesTheLocalScaleFields",
    "ChangeDetectionComparesStoredValuesWithTheLiveTransform",
    "GetComponentForwardsAndSetTransformDropsPendingSetters",
    "TransformSnapshotKeepsItsSerializedFieldLayout",
    "SnapshotHasUpdateIsEmittedVolatileFromBarrierEvidence",
    "TransformSnapshotFloatsAreEmittedVolatileFromBarrierEvidence",
    "PointAndDirectionTransformsForwardToTheWrappedTransformInEditMode",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplinePointTests." + name for name in (
    "TypeSetterOverwritesTheDirtyFlagWithTheChangeAndOnlyMirrorsForSmoothMirrored",
    "MirroredConstructorsDeriveTangent2FromTheTangent",
    "ExplicitTangent2ConstructorMakesACleanBrokenPoint",
    "CopyConstructorStartsCleanAndRederivesTangent2ForSmoothTypes",
    "SetPositionMovesBothTangentsAndLeavesTheDirtyFlag",
    "TangentSettersFollowThePointType",
    "ComponentSettersMarkTheSelectedFieldAndOnlyDirtyOnAChange",
    "FlattenReplacesOneComponentEverywhereAndAimsTheNormalAtTheAxis",
    "LerpPicksTheStrongestTypeAndDerivesTheDirtyFlagFromTheSetter",
    "LerpInterpolatesFieldsAndSplitsTheCubicForTheTangents",
    "EqualityUsesUnityToleranceExactSizeAndType",
    "HashCodeCombinesTheFieldsWithTheNativeMultipliers",
    "SerializedLayoutKeepsTheFieldOrderAndFormerNames",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineSampleTests." + name for name in (
    "ConstructorAndFastCopyMoveEverySerializedField",
    "RotationFallsBackWhenUpEqualsForward",
    "RightIsTheNormalisedCrossProductWithTheEqualPairFallbacks",
    "LerpOverloadsInterpolateEachFieldWithItsNativeInterpolator",
    "LerpClampsTheParameterInEveryInterpolator",
    "SerializedLayoutKeepsTheFieldOrder",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineTests." + name for name in (
    "ConstructorsStartEmptyWithTheShippedDefaults",
    "CloseNeedsThreePointsAndIsClosedNeedsThemToo",
    "MoveStepAndIterationsFollowTheSplineTypeAndClosure",
    "KnotParametrizationClampsAndTheMoveStepSetterIsInert",
    "PointPercentUsesThePointCountWhenClosedAndClamps",
    "BezierEvaluatesTheCubicAndItsUnnormalisedDerivative",
    "ClosedBezierRunsTheLastSegmentBackToPointZero",
    "LinearEvaluatesBetweenPointsAndAveragesTangentsOnRequest",
    "UniformCatmullRomUsesThePolynomialAndItsDerivative",
    "ChordalCatmullRomUsesTheBarryGoldmanPyramidAndItsDerivative",
    "PyramidAtTheKnotThresholdApproachesTheUniformCurve",
    "BSplineSmoothsThroughTheControlsAndDerivesForwardFromNeighbours",
    "OpenBSplineNearAnEndSlerpsTheOutgoingAndIncomingDirections",
    "ClosedBSplineWrapsItsNeighboursAcrossTheSeam",
    "ClosedUniformCatmullRomWrapsItsControlIndices",
    "EvaluateInterpolatesSizeColorAndNormalThroughTheCustomCurves",
    "ClosedEvaluateWrapsTheNextPointForSizeAndColor",
    "ClosedLinearTangentAtTheSeamKeepsTheUnclampedIndex",
    "EvaluateHandlesEmptyOnePointAndClosedPairLikeTheNativeBody",
    "EvaluateByPointIndexUsesThePointPercent",
    "EvaluateIntoAnArrayWalksFromFloorToCeilAndRepeatsTheEndPercent",
    "EvaluateIntoAnArrayReadsTheMoveStepBeforeReopeningAClosedPair",
    "EvaluatePositionsFollowsTheSameWalkAndFaultsOnANullArray",
    "EvaluateUniformSpacesSamplesByArcLength",
    "TravelWalksChordsAndInterpolatesInsideTheLastOne",
    "CalculateLengthSumsChordsAndReturnsZeroForAnInvertedRange",
    "ProjectFindsTheClosestPercent",
    "ProjectOnAClosedSplineChecksTheSecondHalfNearTheSeam",
    "RaycastReportsTheSquaredDistanceRatioOfTheFirstHitSegment",
    "RaycastAllCollectsEveryHitWithItsPercent",
    "BreakRotatesTheClosedPointsAndOpensTheSpline",
    "CatToBezierTangentsRewritesLinearAndUniformCatmullRomPoints",
    "CatToBezierTangentsScalesByTheNeighbourDistanceWhenTheKnotIsPositive",
    "FormatFromToClampsAndSwapsInvertedPairs",
    "ObsoleteOverloadsForwardToTheCurrentOnes",
    "SerializedShapeAndCompilerGeneratedNamesMatchTheShippedAssembly",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineThreadingTests." + name for name in (
    "LivePoolHasAtLeastTwoDefsAndAFreshDefIsIdle",
    "QueueAppendsToTheWorkerQueueInOrder",
    "RestartedThreadSleepsUntilInterruptedThenDrainsTheQueueOnItsOwnThread",
    "AHandlerExceptionEscapesUnloggedAndLeavesTheWorkerComputing",
    "AnAbortDuringTheSleepLogsTheThreadException",
    "InterruptNeedsAThreadAbortIgnoresDeadOnesAndRestartReplaces",
    "PrewarmThreadsStartsOnlyTheThreadsThatAreNotAlive",
    "StopAndQuittingAbortLiveThreads",
    "NestedDelegateTypesKeepTheirSignatures",
    "ABusyThreadDrainsALateQueueInTheSamePassInEditMode",
    "HandlersShareTheLockAndRunDoesNotWaitInEditMode",
    "RunCallsTheHandlerAtOnceInEditMode",
    "ThreadCountOnlyGrowsAndLeavesTheNewThreadsUnstartedInEditMode",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineModifierTests." + name for name in (
    "KeyStartsWithTheShippedDefaultsAndConstructorClampsTheRange",
    "KeyRangeSettersClampAndKeepTheCentreRangeOrdered",
    "KeyGlobalCenterMapsThroughTheFeatherRangeAndWraps",
    "KeyPositionMovesTheWholeFeatherRange",
    "KeyEvaluateRampsHoldsAndFalls",
    "KeyEvaluateOfAWrappedRangeUsesTheTailSegments",
    "KeyDuplicateYieldsAPlainKeyWithAClonedCurve",
    "BaseModifierIsAnEmptyPassThroughAndTheTwoSampleApplyDispatches",
    "ColorKeyBlendModesFollowTheNativeTruthTable",
    "ColorModifierKeysRoundTripAndApplyBlendsInOrder",
    "OffsetModifierShiftsAlongRightAndUpByTheWeightedOffsets",
    "RotationModifierAppliesKeyRotationsOnTopOfTheSampleRotation",
    "RotationModifierSteersTowardAnAliveLookTargetOnly",
    "SizeModifierAddsWeightedKeySizes",
    "FollowerSpeedModifierAddsAndMultipliesWithTheNativeClamps",
    "FollowerSpeedModifierKeysRoundTripThroughTheKeyList",
    "MeshScaleModifierBlendsScalesMultiplicatively",
    "MeshScaleModifierApplyAddsTheScaleMagnitudeToTheSize",
    "ObjectSequenceOrderedWalksAndWraps",
    "ObjectSequenceRandomNeverPicksTheLastElementAndReseedsOnlyOnChange",
    "SerializedLayoutKeepsTheFieldOrderAndAttributes",
    "ObjectSequenceFirstAndLastFallBackToNextForAnUnsetReferenceObject",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckNodeTests." + name for name in (
    "FreshNodeKeepsTheShippedDefaults",
    "ConnectingPullsThePointOntoTheNode",
    "ConnectNodeRejectsDuplicatesNullsAndBadIndices",
    "DisconnectRemovesBothSides",
    "MovingTheNodeMovesTheConnectedPointUnlessExcluded",
    "DestroyingTheNodeUnlinksTheComputer",
    "SmoothNodeMirrorsThePointAndFreeNodeDoesNot",
    "SmoothFreeNodeKeepsTheOtherTangentLengths",
    "SharedNodeLinksTheComputers",
    "GetConnectedComputersFaultsOnNullConnectedIndicesAndCreatesTheOtherLists",
    "GetNodesFollowsThePointRange",
    "ShiftNodesMovesFromTheTopAndTransferChecksTheTargets",
    "SerializedLayoutKeepsTheFieldOrderAndNestedTypes",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSampleCollectionTests." + name for name in (
    "ConstructorsAndLength",
    "SamplingValuesDefaultAndOptimized",
    "OptimizedSamplingAtTheLastRawIndexLeavesRawPercentZero",
    "ClippedSampleCountTruthTable",
    "EvaluateInterpolatesAndEmptyAnswersZero",
    "RangeEvaluationReturnsEndsPlusStoredSamples",
    "TravelWalksTheChordsAndGivesBackTheOvershoot",
    "TravelWrapsOverTheEndWhenTheClipIsInverted",
    "TravelGuards",
    "TravelWithOffsetShiftsTheSamples",
    "CalculateLengthSumsTheChords",
    "CalculateLengthWithOffsetMeasuresTheShiftedPolyline",
    "ProjectFindsTheNearestPointOnTheChords",
    "ProjectUniformUsesTheSampleIndicesForThePercent",
    "SerializedLayoutKeepsTheFieldOrder",
    "ProjectPrefersTheClosingSegmentWhenItIsNearerThanTheFirstOne",
    "TravelStopsAtTheLastSampleWhenAClipEndIsNaN",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineComputerTests." + name for name in (
    "FreshComputerKeepsTheShippedDefaults",
    "SampleRateIsClampedToTwo",
    "SpaceSwitchKeepsTheWorldPositions",
    "SamplesFollowTheLinearPolyline",
    "CalculateModeMatchesTheCachedWorldSamples",
    "OptimizedModeDropsCollinearSamplesAndKeepsCorners",
    "RaycastDerivesThePercentFromTheSquaredDistanceRatio",
    "SubscriptionsIgnoreDuplicatesAndReturnACopy",
    "PointSettersAppendAndIgnoreNegativeIndices",
    "PointPercentAndIndexTruthTable",
    "CloseNeedsThreePointsAndBreakReopens",
    "TriggerApisCreateGroupsLogMissingOnesAndDropTheUser",
    "GetSamplesSharesTheCachedArrays",
    "TransformHelpersUseTheCachedMatrices",
    "SerializedLayoutKeepsTheFieldOrderAndAttributes",
    "CompilerGeneratedNamesAndNestedTypesOfTheEditorCompile",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineTriggerTests." + name for name in (
    "ConstructorKeepsTheShippedDefaults",
    "CheckFollowsTheNativeTruthTable",
    "WorkOnceBlocksUntilResetAndMissesDoNotCount",
    "GroupResetRearmsWorkOnceTriggers",
    "GroupResetFaultsOnANullEntryAfterResettingEarlierOnes",
    "GetTriggersIncludesBothEnds",
    "GroupAddAndRemoveTriggers",
    "SerializedLayoutKeepsTheFieldOrderAndAttributes",
    "ClosureKeepsTheShippedCompilerName",
    "GroupChecksMarkCrossedTriggersSkipNullsAndIgnoreTheGroupFlagInEditMode",
    "ListenerRemovalWorksAndInvokeStaysSilentInEditMode",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineUserTests." + name for name in (
    "FreshUserKeepsTheShippedDefaults",
    "ChangingTheSplineMovesTheSubscription",
    "ClipSettersClampAndPushTheOtherEnd",
    "ClipAndUnclipMapTheOpenRange",
    "LoopedRangeWrapsOverTheEnd",
    "ClippedSamplesStartAndEndExactlyOnTheClipRange",
    "AngleCompensationWidensTheCornerAndLeavesTheEnds",
    "ModifySampleWithEmptyModifiersKeepsTheSample",
    "QueriesWorkInClippedPercents",
    "AnimationPropertiesReplaceTheClipRangeWithTheMirrors",
    "SerializedLayoutKeepsTheFieldOrderAndAttributes",
    "SetClipRangeAsksTheComputerBeforeComparingTheEnds",
    "EvaluateReturnsTheUnmodifiedSampleWhileGetSampleReturnsTheModifiedOne",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineUtilityTests." + name for name in (
    "MergeSideValues",
    "MergeAtTheEndAppendsTheAddedPointsBackwards",
    "MergeAtTheEndWithEndpointsDropsOnePoint",
    "MergeAtTheStartPrependsTheAddedPointsBackwards",
    "UnknownSideValuePlacesTheAddedPointsForwardsInFront",
    "MergeMovesTheUsersAndRemapsTheirClipRange",
    "MergeReconnectsTheNodesOfTheAddedSpline",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineFollowerTests." + name for name in (
    "FreshFollowerKeepsTheShippedDefaults",
    "MotionIsCreatedOnceAndPointsBackAtTheTracer",
    "SetPercentPlacesTheTransformOnTheSample",
    "SetDistanceTravelsFromTheStart",
    "SpeedDirectionAndDurationStayConsistent",
    "FollowToggleProjectsOnlyWithAutoStartPosition",
    "StartAndPostBuildFollowTheNativeConditions",
    "MoveByPercentStopsAtTheEndInDefaultWrap",
    "MoveByPercentLoopsAroundBothEnds",
    "MoveByPercentReflectsInPingPong",
    "MoveByPercentStopsAtTheBeginningInDefaultWrap",
    "MoveByDistanceWrapsWithTheRemainingDistance",
    "MoveByDistanceWrapsBackwards",
    "DoTravelUsesTheOffsetPathOnlyWhenPreservingUniformSpeed",
    "MissingPhysicsComponentsThrowTheShippedMessage",
    "PhysicsModeFetchesTheMatchingComponent",
    "DirectionAndDontLerpDirectionShapeTheAppliedRotation",
    "DefaultWrapTreatsAZeroPercentAfterTheEndAsStillAtTheEnd",
    "MotionAppliedIsRaisedAfterApplyingAndNotForAnUnknownPhysicsMode",
    "FollowerOverridesRememberTheLastClippedPercent",
    "SerializedLayoutKeepsTheFieldOrderAttributesAndNestedTypes",
    "ModifiedResultCarriesTheModifiersWhileResultStaysUnmodified",
    "CheckNodesReportsNothingInEditMode",
    "LateRunSkipsFollowInEditMode",
    "LoopWrapCompletesWithoutTriggersInEditMode",
    "SetPercentInvokesNoTriggerInEditMode",
    "SetPercentReportsNoNodeInEditMode",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckTransformModuleTests." + name for name in (
    "FreshModuleKeepsTheShippedDefaults",
    "ApplyFlagGroupsFollowTheNativeTruthTable",
    "DeserializationRecomputesTheOffsetFlags",
    "ApplyTransformMovesOnlyTheAppliedAxes",
    "ScaleIsBaseScaleTimesSampleSizeOnTheAppliedAxes",
    "OffsetMovesAlongTheSampleFrameScaledBySize",
    "RetainLocalPositionKeepsTheUnappliedLocalAxes",
    "RotationLooksAlongTheSampleAndBackwardNegatesForward",
    "RotationOffsetIsMultipliedOntoTheLookRotation",
    "UnappliedRotationAxesKeepTheInputEulerAngles",
    "RetainLocalRotationKeepsTheUnappliedLocalAxes",
    "TwoDimensionalRotationIsAPlanarAngle",
    "VelocityModesFollowTheNativeTruthTable",
    "VelocityKeepsTheUnappliedAxes",
    "RigidbodyEntryPointsScaleTheBodyAndFaultOnNull",
    "SerializedLayoutKeepsTheFieldOrderAndAttributes",
    "RigidbodyAngularVelocityIsUntouchedInEditMode",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplinePositionerTests." + name for name in (
    "FreshPositionerKeepsTheShippedDefaults",
    "SetPercentMovesTheTargetAndStoresThePosition",
    "PositionSetterComparesTheFloatFieldAsADouble",
    "DistanceModeMovesBySetDistanceAndStoresTheDistance",
    "SetDistanceInPercentModeStoresTheTravelledPercent",
    "PostBuildAppliesTheStoredPositionAgain",
    "FollowTargetPlacesTheDistanceFromTheFollowedTracer",
    "FollowSettersReapplyTheFollowOnlyWithATarget",
    "FollowLoopContinuesFromTheOtherEnd",
    "SelfReferenceIsRefusedAfterTheOldTargetWasReleased",
    "AwakeAndOnDestroyTieTheSerializedFollowTarget",
    "SerializedLayoutKeepsTheFieldOrderAndAttributes",
    "SetPercentAndSetDistanceReportNothingInEditMode",
))
# Retained absent-spline control beside the strengthened private-target guard oracle (#522).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.OriginalDreamteckSplineProjectorTests.LateRunWithoutASplineKeepsTheZeroResult",
)

EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineProjectorTests." + name for name in (
    "FreshProjectorKeepsTheShippedDefaults",
    "ResetTargetsTheOwnTransform",
    "TargetObjectTakesOverTheLegacyApplyTarget",
    "ANewComputerIsProjectedOverTheClipRangeAndClipped",
    "CalculateProjectionPlacesTheTargetObjectOnTheProjection",
    "AccurateModeProjectsOntoTheCurve",
    "ReachingTheEndsRaisesTheReachEventsOnce",
    "ReachingTheEndOfALoopingRangeContinuesFromTheStart",
    "LateRunProjectsOnlyAfterTheTargetMoved",
    "LateRunProjectsAMoveOfExactlyTheNativeEpsilon",
    "LateRunWithoutAProjectTargetOnlyRemembersThePosition",
    "SerializedLayoutKeepsTheFieldOrderAndAttributes",
    "CalculateProjectionReportsNothingInEditMode",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckLengthCalculatorTests." + name for name in (
    "FreshCalculatorAndEventKeepTheShippedDefaults",
    "CheckFollowsTheNativeTruthTable",
    "AddEventAppendsToACopyOfTheArray",
    "BuildChecksTheEventsAgainstTheChangedLength",
    "AwakeMeasuresOnlyTheClipRange",
    "BuildNoticesAChangeOfTheClipRangeAlone",
    "SerializedLayoutKeepsTheFieldOrderAndAttributes",
    "BlankUserOnlyForwardsToTheBase",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckColliderGeneratorTests." + name for name in (
    "FreshEdgeGeneratorKeepsTheShippedDefaults",
    "BuildTakesOneVertexPerSamplePositionInLocalSpace",
    "OffsetMovesTheVerticesAlongTheScaledPlanarNormal",
    "WithoutAColliderNothingIsLocalisedOrApplied",
    "TheLifecycleOverridesCallTheBase",
    "EdgeSerializedLayoutKeepsTheFieldOrderAndAttributes",
    "FreshPolygonGeneratorKeepsTheShippedDefaults",
    "PathTypeBuildsAClosedBandAroundTheSamples",
    "ShapeTypeBuildsOneVertexPerSample",
    "PolygonUpdateRateAndLocalSpaceFollowTheEdgeGenerator",
    "PolygonSerializedLayoutKeepsTheFieldOrderAndAttributes",
    "UpdateRateAppliesTheColliderAtOnceInEditMode",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckParticleControllerTests." + name for name in (
    "FreshControllerAndParticleStateKeepTheShippedDefaults",
    "SplinePercentFollowsTheNativeTable",
    "SystemSetterFetchesTheRendererAndResetFindsTheSystem",
    "LateRunSizesItsArraysFromTheSystem",
    "BornParticlesAreMarkedAndNotBornAgain",
    "BornParticleFollowsTheSplineFromTheBeginning",
    "EmitPointAndWrapPickThePercent",
    "OrderedEmissionCountsTheBirths",
    "OffsetAndRotationFollowTheSample",
    "HandleParticleSkipsTheMotionsThatMoveByThemselves",
    "VolumetricEmissionOffsetsTheParticle",
    "ByNormalMotionUsesTheStartSpeedAndTheForce",
    "ByNormalRandomizedTurnsUpAroundForward",
    "LocalSimulationSpaceRoundTripsThroughWorldSpace",
    "SerializedLayoutKeepsTheFieldOrderAndAttributes",
    "SplinePercentEditorProductNarrowsToTheFloatProduct",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckMeshGeneratorTests." + name for name in (
    "ComponentsAreFetchedByAwakeAndReset",
    "IndexFormatAndMarkDynamicReplaceTheMeshes",
    "BakedGeneratorNeverRebuilds",
    "BuildCreatesTheMeshesAndClearsThemBelowTwoSamples",
    "WriteMeshReportsTheSixteenBitVertexLimit",
    "AllocateMeshSizesTheArraysLikeTheNativeBody",
    "UvCoordinatesFollowTheModeTruthTable",
    "TypeInitializerStartsTheUvScratchAtZero",
    "DisposeRestoresTheUvScratchValueFoundBeforeTheTest",
    "DisposeDestroysTheMeshOfAnAlreadyDestroyedGenerator",
    "UniformUvDistanceStartsAtTheClipAndGrowsBySampleDistance",
    "BaseSizeAndColorHonourTheSplineFlags",
    "ColliderFollowsTheMeshAtTheUpdateRate",
    "DestroyingTheGeneratorRestoresTheHideFlagsOfFilterAndRenderer",
    "SerializedLayoutKeepsTheFieldOrderAttributesAndNestedTypes",
    "FreshGeneratorHasBuiltOnceInEditMode",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckPathGeneratorTests." + name for name in (
    "FreshPathGeneratorKeepsTheShippedDefaults",
    "SingleSliceRibbonFollowsTheLine",
    "SizeOffsetAndRotationShapeTheRibbon",
    "RotationIsNotAppliedTwiceToMultiSliceFacetNormals",
    "VertexColourMultipliesTheBaseColourAndTheGeneratorColour",
    "TwoSlicesWithAShapeCurveGetFacetNormals",
    "CompensatedCornersWidenTheCornerRow",
    "UvModesChangeTheVCoordinate",
    "SingleSampleBuildsAnEmptyMesh",
    "SerializedLayoutKeepsTheFieldOrderAttributesAndMenu",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineRendererTests." + name for name in (
    "FreshRendererKeepsTheShippedDefaults",
    "LateRunCountsFramesAndWrapsAboveTheInterval",
    "LateRunDoesNotUpdateTheCollider",
    "DefaultBuildFacesTheRowTowardsTheVertexDirection",
    "GenerateVerticesUsesTheDirectionAsGivenWhenOrthographic",
    "SampleSizeAndColourIgnoreTheUseSplineFlags",
    "OffsetMovesTheCentreAlongUpRightAndForward",
    "OffsetIsNotScaledBySampleSize",
    "RenderWithCameraOrientsTheMeshForTheCamera",
    "OnWillRenderObjectRendersForTheCurrentCameraElseTheMainCamera",
    "StartTakesTheProjectionOfTheCurrentCameraElseTheMainCameraAndCreatesNewMeshes",
    "SerializedLayoutKeepsTheFieldOrderAttributesAndMenu",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckObjectControllerTests." + name for name in (
    "FreshControllerKeepsTheShippedDefaults",
    "RotationOffsetIsTheMiddleOfTheLimitsAndPinsBoth",
    "SpawnCountInstantiatesTheCopiesFromTheSourcesInTurn",
    "SpawningNeedsASplineAndSources",
    "RaisingTheSpawnCountKeepsTheExistingCopies",
    "LoweringTheSpawnCountDestroysTheExtraCopies",
    "SpawnCountIsOnlyStoredForTheChildrenMethod",
    "RandomIterationPicksWithTheEngineRandom",
    "BuildPlacesTheCopiesEvenlyAlongTheSpline",
    "EvaluateOffsetShiftsAndWrapsThePercents",
    "ASingleCopyStandsAtTheEvaluateOffset",
    "AClosedSplineDividesByTheCopyCount",
    "ClipPositioningEvaluatesTheWholeSplineAndHidesCopiesOutsideTheClip",
    "StretchPositioningEvaluatesInTheClippedRange",
    "OffsetIsAppliedInTheSampleFrameOrTheWorldFrame",
    "RandomOffsetsFollowTheSeededGenerator",
    "ShellOffsetPlacesTheCopiesOnTheEllipse",
    "RotateByOffsetLooksAlongTheSampleForwardWithTheOffsetAsUp",
    "ScaleMultipliesTheBaseScaleBySizeAndTheMultiplier",
    "RandomScalesFollowTheSeededGenerator",
    "CustomRulesDriveOffsetAndRotation",
    "CustomObjectDistanceTravelsAlongTheSpline",
    "SampleSizeScalesTheLocalOffsetAndTheScaleButNotTheWorldOffset",
    "RandomObjectDistancesFollowTheSeededGenerator",
    "ClipPositioningTravelsOnTheComputerNotTheClippedUser",
    "StretchPositioningLeavesHiddenCopiesHidden",
    "ANullSourceEndsAnAttemptWithoutACopy",
    "RotateByOffsetKeepsTheEulerFactor",
    "GetChildrenTakesTheChildrenOver",
    "GetAllRecreatesTheControlsAndRereadsTheBaseScale",
    "ClearGivesTheBaseScaleBackAndHidesOrDestroys",
    "LateRunCollectsChangedChildrenAgain",
    "ABuildWithAnEmptySlotRespawns",
    "DelayedSpawnInstantiatesAtOnceOutsidePlayMode",
    "DelayedSpawnCoroutineRunsUpToAndIncludingTheSpawnCount",
    "ObjectControlKeepsItsDefaultsAndApplies",
    "ObjectControlSurvivesADestroyedObject",
    "SerializedLayoutKeepsTheFieldOrderAttributesAndNestedTypes",
    "AnIterationOutsideTheEnumDrawsWithTheEngineRandom",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckObjectControllerRuleTests." + name for name in (
    "BaseRuleAnswersTheSample",
    "SineOffsetRotatesTheWaveByTheAngle",
    "SineFrequencyAndOffsetShiftTheWave",
    "SineUsesTheSplinePercentWhenAsked",
    "SineScaleLerpsBetweenTheLimitsByTheWave",
    "OffsetSettersWrapIntoTheUnitRange",
    "SpiralOffsetTurnsTheUpVectorAndStretchesBySize",
    "SpiralPercentWrapsAndTheSplinePercentIgnoresTheOffset",
    "SpiralRotationTurnsTheSampleBackByTheSameAngle",
    "DefaultsAndSerializedLayoutMatchTheShippedRules",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplinePrimitiveTests." + name for name in (
    "FreshPrimitivesKeepTheShippedDefaults",
    "SplineTypesFollowTheNativeReturnValues",
    "BasePrimitiveCalculatesToNothing",
    "CapsuleBuildsSixSmoothPointsWithQuarterCircleHandles",
    "CapsuleScalesWithRadiusAndHeight",
    "EllipseBuildsFourSmoothPointsOnTheAxes",
    "LineSpreadsPointsEvenlyAndMirrorCentresIt",
    "LineDefaultsGiveASingleCentredSegment",
    "CreatePointsResetsTheSharedPointFieldsOnEveryRun",
    "NgonAndStarPlaceVerticesWithSetPosition",
    "RectangleSetsPositionAndTangentButNotTangent2",
    "RoundedRectangleInsetsTheEdgePointsAndAlternatesHandles",
    "RoundedRectangleHandleUsesTheExactNativeConstant",
    "CreatePointsMirrorsTangent2ThroughTheTypeSetterOnAReusedArray",
    "ClockwiseSpiralClimbsAQuarterTurnPerPoint",
    "CounterclockwiseSpiralMirrorsTheTurnAndFlipsTheTangentTerms",
    "SpiralLeanFollowsTheRadiusChangeAndItsDirection",
    "CalculateRotatesThenOffsetsEveryTangent",
    "TwoDimensionalRotationRollsAboutForwardAfterTheQuarterTurn",
    "CreateSplineSharesThePointsAndClosesClosedPrimitives",
    "UpdateSplineClosesOrBreaksOnTheSplineClosedState",
    "CreateSplineComputerBuildsALocalSpaceComputerWithoutSettingItsType",
    "UpdateSplineComputerAssignsTheTypeAndClosesOrBreaks",
    "PrimitiveFieldsKeepTheShippedOrderAndVisibility",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineParserTests." + name for name in (
    "ParserKeepsItsDefaults",
    "IsWhiteSpaceIsTrueForEmptyAndBlankStringsOnly",
    "ParseFloatArraySplitsOnCommasSpacesAndSigns",
    "ParseFloatArrayStartsANewNumberAtASecondDecimalPoint",
    "ParseFloatArrayCutsExponentsAtTheMinusSignAndAddsZeroForFailedParses",
    "ParseVector2PairsNumbersAndNegatesY",
    "ParseVector2NegatesAZeroYToNegativeZero",
    "ParseVector2WithFewerThanTwoNumbersLogsAndYieldsOneZero",
    "ParseFloatReturnsTheNumbersOrOneZeroWithAnError",
    "TranslateNegatesYAndMovesPositionAndTangents",
    "RotateTurnsAboutTheBackAxis",
    "ScaleMultipliesXyAndFlattensZ",
    "SkewsPutTheNegatedTangentInTheirMatrixElement",
    "MatrixTransformMapsTheAffineRowsAndFlipsTheTranslationY",
    "PushOrderMultipliesOnTheRightAndResetStartsOver",
    "ApplyWritesTheTransformedPointsBack",
    "DefinitionStartsWithTheShippedPenState",
    "DefinitionFromASplineCopiesTypeClosedStateAndPoints",
    "LastPointAccessorsHandleAnEmptyListAndReplaceTheLast",
    "CreateSmoothAddsAMirroredDirtyPointFromThePenState",
    "CreateBrokenAddsADirtyBrokenPointWithTangent2OnThePosition",
    "CreateLinearPutsTheTangentOnThePositionAndAddsASmoothPoint",
    "CreateClosingPointAppendsACopyOfTheFirstPoint",
    "CreateSplineBuildsTheSplineAndClosesClosedDefinitions",
    "CreateSplineComputerBuildsTheComputerFromTheDefinition",
    "TransformPushesEveryTransformationAndStoresThePointsBack",
    "ParserKeepsTheShippedNestedTypesAndFieldOrder",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckCsvTests." + name for name in (
    "ColumnTypeKeepsTheShippedValues",
    "MissingFileLeavesAnEmptyCsvWhoseBufferMembersFault",
    "WrongExtensionKeepsTheNameLogsAnErrorAndReadsNothing",
    "UppercaseExtensionIsAccepted",
    "DefaultColumnsFillThePenStateAndTheTangent2ColumnIsNotUsedForThePoint",
    "CustomColumnsAreCopiedAndReadInTheGivenOrder",
    "RowsWithTheWrongElementCountLogAndAreSkipped",
    "ValuesParseWithTheEnglishCultureAndFailuresBecomeZero",
    "ValuesParseWithNumberStylesAny",
    "ComputerConstructorExportsPositionTangentAndTangent2",
    "ComputerConstructorConvertsCatmullRomAndBSplineToBezier",
    "ClosedComputerExportsAClosedDefinitionThatCreatesAClosedSpline",
    "FlatAxesLeaveTheBufferUnchanged",
    "WriteBuildsTheTitleRowAndOneRowPerPoint",
    "WriteFormatsFloatsWithTheCurrentCulture",
    "WriteToAMissingDirectoryThrows",
    "WrittenFilesReadBackWithTheSameColumns",
    "CsvKeepsTheShippedFieldOrder",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckTubeGeneratorTests." + name for name in (
    "FreshTubeKeepsTheShippedDefaults",
    "RingsFollowTheLine",
    "SizeRotationOffsetAndRevolveShapeTheRing",
    "VertexColourMultipliesTheBaseColourAndTheGeneratorColour",
    "UvTwistAndUvModesShapeTheCoordinates",
    "FewerThanThreeSidesBuildNothing",
    "CapsAreOnlyBuiltWhereTheEndsAreVisible",
    "FlatCapsCopyTheEndRingsIntoFans",
    "FlatCapUvAndFanFollowTheCapScaleAndRevolve",
    "RoundCapsBendFromTheEndRingsToTheAxis",
    "RoundCapVContinuesTheBodyInEveryUvMode",
    "RoundCapVFollowsTheClipRangeInEveryUvMode",
    "UvRotationTurnsTheBodyAndTheCapCoordinates",
    "RoundCapsScaleBySampleSizeEvenWithoutSplineSize",
    "RoundCapCentresFollowTheOffset",
    "SerializedLayoutKeepsTheFieldOrderAttributesAndMenu",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckWaveformGeneratorTests." + name for name in (
    "FreshWaveformKeepsTheShippedDefaults",
    "SingleSliceRunsFromTheBaselineToTheSample",
    "SlicesInterpolateAndSymmetryMirrorsTheBaseline",
    "AxisChoosesTheDroppedCoordinate",
    "ANegativeAverageValueFlipsTheWinding",
    "UvWrapModesTruthTable",
    "AnAxisOutsideTheEnumDropsNothing",
    "OffsetMovesAlongTheAxisAndTheSampleRight",
    "TheBaselineLiesInTheSplinePlane",
    "SingleSampleBuildsAnEmptyMesh",
    "SerializedLayoutKeepsTheFieldOrderAttributesAndMenu",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSurfaceGeneratorTests." + name for name in (
    "FreshSurfaceKeepsTheShippedDefaults",
    "IsClockwiseFollowsTheShippedSum",
    "WriteTrisShiftsAndReversesTheTriangles",
    "GenerateSurfaceTrisTriangulatesTheOutlineAndFlipsOnRequest",
    "ProjectedVerticesAreSignedDistancesAlongThePlaneAxes",
    "IdentityVertsAreRelativeToTheCentreInTheNormalFrame",
    "FlatSurfaceFollowsTheOutline",
    "ColourOffsetAndUvTransformShapeTheFlatSurface",
    "ExpandPushesTheOutlineAlongTheReversedSampleRight",
    "ExtrudeBuildsTheBottomCopyAndTwoWallRims",
    "NegativeExtrudeTurnsTheSurfaceAndTheWallsOver",
    "SideUvScaleOffsetRotationAndUniformModeShapeTheWallCoordinates",
    "ExtrudeSplineSweepsTheOutlineAlongTheSpline",
    "ExtrudeOffsetAndUniformUvsFollowTheExtrudeSamples",
    "ExtrudeSplineRotatesTheOutlineAndAppliesTheOffsetInTheSampleFrame",
    "AnOutlineThatIsNotClockwiseTakesTheOtherBranches",
    "TransformAxesAreCachedAtAwakeAndRefreshedByLateUpdateWhenThreaded",
    "BuildMeshNeedsASplineWithPoints",
    "SerializedLayoutKeepsTheFieldOrderAttributesAndMenu",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineMeshTests." + name for name in (
    "FreshComponentKeepsTheShippedMeshName",
    "ResetAddsTheFirstChannel",
    "NoChannelsOrMeshesClearTheMesh",
    "AChannelWithoutMeshesBetweenOthersTakesNoCombineSlot",
    "PlaceStampsOneCopyPerCountAtEvenPercents",
    "PlaceFollowsTheChannelClipRange",
    "PlaceOffsetsMoveAlongTheSampleFrame",
    "PlaceRotationsComposeInTheShippedOrder",
    "PlaceCustomRotationComesLastAfterTheRandomAndGeneratorRotations",
    "PlaceScaleAndSampleSizeScaleTheCopyAndTheNormals",
    "PlaceColoursMultiplySampleMeshAndGeneratorColours",
    "PlaceCustomNormalReplacesTheSampleFrameAfterTheOffsets",
    "PlaceScaleModifierUsesThePercentSpaceOfItsFlag",
    "PlaceDrawsEachRandomSequenceInOrderAndRestartsItPerBuild",
    "PlaceHandlersAddTheirValuesAndSeeThePlacePercent",
    "ExtrudeBendsTheVertexGroupsOntoTheSpline",
    "ExtrudeSpacingInsetsEachSliceAndSetsTheLastResultFlag",
    "ExtrudeOffsetAngleAndScaleUseXyAndTheZAngleOnly",
    "ExtrudeCustomNormalReplacesTheSampleFrameAfterTheOffsets",
    "ExtrudeColoursMultiplySampleMeshAndGeneratorColours",
    "ExtrudeUvOverridesFollowTheMode",
    "ExtrudeHandlersSeeThePercentOfEachGroup",
    "ExtrudeDrawsOncePerCopyAndSharesTheDrawsBetweenItsGroups",
    "AutoCountFitsTheCopiesToTheClippedLength",
    "CombineListFollowsTheTotalCopyCount",
    "MaterialIdWritesTheTrianglesIntoThatSubmesh",
    "SerializedLayoutKeepsTheFieldOrderAttributesAndNestedTypes",
    "ChannelNestedTypesKeepTheShippedValues",
    "PlaceBuildsFromTheModifiedSample",
    "ExtrudeBuildsFromTheModifiedSample",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineMeshChannelTests." + name for name in (
    "FreshChannelKeepsTheShippedDefaults",
    "ChannelWithoutAnOwnerStoresWithoutRebuilding",
    "CopyToCopiesTheSettingsAndTheMeshesOnly",
    "NextMeshWalksTheMeshesAndWraps",
    "RandomOrderPicksFromTheSeededSequence",
    "ResetIterationRestartsOnlyTheEnabledSequences",
    "NextRandomOffsetLerpsEachAxisWithItsOwnDraw",
    "NextRandomRotationDrawsOncePerAxis",
    "RandomScalesAreFlatForExtrudeAndFullForPlace",
    "CustomValuesRunTheHandlersInOrder",
    "SerializedLayoutKeepsTheFieldOrderAndAttributes",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineMeshDefinitionTests." + name for name in (
    "DefinitionWithoutAMeshIsEmpty",
    "RefreshReadsTheMeshAndGroupsTheVertices",
    "RefreshKeepsMeshColoursAndFollowsTheSubmeshCount",
    "SettersRefreshOnlyForChangedValues",
    "BoundsContainTheOriginAndGrowByTheSpacing",
    "ThinBoundsLogAWarningWithTheMeshName",
    "TransformMovesRotatesAndScalesTheVerticesNormalsAndUvs",
    "MirrorNegatesTheAxisAndTurnsTheWinding",
    "MirrorTangentsFollowTheUvDirection",
    "CalculateTangentsOfAnEmptyDefinitionOnlyEmptiesTheTangents",
    "DoubleSidedDoublesTheMeshAndFlipFacesReversesIt",
    "RemoveInnerFacesDropsTheCapsOnTheExtremeZ",
    "VerticesGroupByZWithinTheMargin",
    "FindInsertIndexSearchesTheDescendingGroups",
    "WriteCopiesTheArraysAndKeepsFittingTargetArrays",
    "WriteWithAMaterialIdFillsOneSubmesh",
    "CopyKeepsTheArraysAndSomeSettings",
    "SubmeshAndVertexGroupKeepTheirData",
    "SerializedLayoutKeepsTheFieldOrderAndAttributes",
    "RefreshReturnsFalseForAMeshThatIsNotReadable",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckObjectBenderTests." + name for name in (
    "EnumsKeepTheShippedValues",
    "FreshBenderKeepsTheShippedDefaults",
    "BendPropertyCapturesTheLocalTransformAndTheParentRotation",
    "MeshNormalsAreRotatedAndNormalisedButColliderNormalsAreNot",
    "EditMeshesFollowTheirFlagsAndTheSharedColliderMeshIsNotCopied",
    "SwitchingTheMeshFlagOffRestoresTheOriginalMeshAndOnRereadsTheNormals",
    "SplineComputerOnTheTransformIsSnapshotAndTheParentFlagSkipsTheLookup",
    "UpdateReferencesCreatesOnePropertyPerTransformDepthFirst",
    "UpdateReferencesCarriesOverTheSettingsOfTheEarlierProperty",
    "BoundsStartAtTheOriginAndGrowWithVerticesAndChildOrigins",
    "ABenderOnTheSplineObjectSkipsItsOwnProperty",
    "PercentsFollowTheBoundsAndTheColliderArrayIsReallocatedWhenTheCountsDiffer",
    "GetevalResultOnTheZAxisOffsetsAlongRightAndUp",
    "GetevalResultOnTheXAxisRunsTheZLerpBackwardsAndCancelsTheSampleTurn",
    "GetevalResultOnTheYAxisUsesTheXAndZLerps",
    "CustomNormalAndForwardModesReplaceTheSampleVectors",
    "BendingTheMeshAlongTheLineRotatesItIntoTheSplinesFrame",
    "TheSampleSizeScalesTheOffsetsAndTheObject",
    "ADisabledPropertyIsNotBentAndApplyRotationOffKeepsTheOriginalRotation",
    "SwitchingBendOffRevertsTheTransformAndTheMesh",
    "ASplineOnAChildIsBentAndRestored",
    "AColliderOnTheSharedMeshTakesTheBentRendererMeshOnUpdate",
    "BendingTheMeshAlongTheYAxisFlipsTheThirdPercentage",
    "ABentChildComposesTheBendRotationWithItsOriginalRotation",
    "AColliderWithItsOwnMeshTakesABentCopyOfThatMesh",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineMorphTests." + name for name in (
    "EnumsAndChannelKeepTheShippedDefaults",
    "MorphKeepsItsDefaultsAndResetTakesTheComputerOnTheObject",
    "SerializedLayoutKeepsTheShippedOrderAndAttributes",
    "AddChannelCapturesTheSplineWithTheDefaultCurve",
    "MismatchedPointCountsAreRefusedWithAnError",
    "AnUnknownChannelNameLogsAndFallsBackToTheFirstChannel",
    "IndexedAndNamedWeightsAreClampedAndSnapshotsAreExchangedByReference",
    "PercentWeightsCrossFadeNeighbouringChannelsAndLoopWraps",
    "LinearBlendAddsTheWeightedDifferenceToTheBaseChannel",
    "TheTypeOfTheBasePointOnlyChangesAsTheMergeRulesSay",
    "SphericalChannelsSlerpPositionsInsteadOfLerping",
    "RemoveChannelKeepsTheOrderAndIgnoresBadIndicesAndClearEmpties",
    "RunUpdateKeepsTheCycleInsideZeroToOneByMode",
    "RunUpdateWrapsWithFloorWhenTheCycleIsMoreThanOneTurnOut",
    "RunUpdatePassesTheLoopFlagToSetWeightAndMovesByTheDirectionedStep",
    "EachUpdateCallbackRunsTheCycleOnlyForItsOwnMode",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSvgTests." + name for name in (
    "NestedTypesFieldsAndTheCachedLambdaKeepTheShippedLayout",
    "FileConstructorHandlesMissingWrongAndBrokenFiles",
    "RectangleBecomesAClosedLinearRectangleWithTheYFlip",
    "ARadiusMakesARoundedRectangleWithTheRadiusOnBothAxes",
    "CirclesAndEllipsesUseTheEllipsePrimitiveAtTheFlippedCentre",
    "LineBecomesTwoLinearPointsAndIsNumberedAfterTheEllipses",
    "PolygonsAndPolylinesReadPointPairs",
    "PathLinesFollowTheAbsoluteAndRelativeCommands",
    "CubicCurvesSetTheControlPointsAndRelativeOnesAddThePreviousPosition",
    "ShortCubicCurvesMirrorThePreviousTangent",
    "RelativeShortCubicCurvesDoNotOffsetTheMirroredTangent",
    "QuadraticCurvesBecomeCubicControlsAtTwoThirds",
    "AnArcBecomesBezierPiecesOnTheEllipse",
    "ArcsAcceptRelativeEndPointsAndKeepTheTravelDirectionInTheTangents",
    "EllipseParametersFollowTheEndpointConversion",
    "ArcHelpersMatchTheirDefinitions",
    "ParsePathSegmentGroupsNumbersPerCommandType",
    "TransformationCallsAreParsedWithTheirParameters",
    "TransformationNumbersAreParsedWithTheCurrentCulture",
    "GroupTransformationsApplyToTheirSubtreeOnly",
    "APathIsTransformedASecondTimeByTheWholeStack",
    "EachMoveStartsAPathAndARelativeMoveContinuesFromTheLastStoredOne",
    "ElementSelectionAndOrderOfTheCreatedSplines",
    "MapPointProjectsOntoTheAxisPlaneAndAttributeContentFallsBackToError",
    "WriteEncodesBezierPathsAndLinearPolylinesForEachAxis",
    "ComputersAreConvertedToBezierAndClosedOnesAreClosed",
))

# Dreamteck shipped player branches (Application.isPlaying true): queued Rebuild/RunUpdate, cached transforms and the
# Application.isPlaying gates the editor compile skips in EditMode.
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckTransformSnapshotPlayModeTests." + name for name in (
    "PointAndDirectionTransformsScaleThenRotateThenTranslate",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineThreadingPlayModeTests." + name for name in (
    "RunMovesToTheNextThreadWhileTheFirstIsComputingAndHandlersShareTheLock",
    "RunQueuesOnTheFirstIdleThreadAndWakesIt",
    "RunQueuesOnTheLastThreadWhenEveryThreadIsBusy",
    "ThreadCountOnlyGrowsAndStartsTheNewThreads",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineComputerPlayModeTests." + name for name in (
    "ResampleRebuildsUsersAndRaisesOnRebuild",
    "TriggerApisCreateGroupsLogMissingOnesAndDropTheUser",
    "UpdateModeSelectsTheCallbacksThatResample",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineTriggerPlayModeTests." + name for name in (
    "GroupChecksFireCrossedTriggersSkipNullsAndIgnoreTheGroupFlag",
    "ListenersReceiveInvokeAndRemovalWorks",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineUserPlayModeTests." + name for name in (
    "AutoUpdateGatesTheQueuedRebuild",
    "UpdateMethodSelectsTheCallbackThatBuilds",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineFollowerPlayModeTests." + name for name in (
    "CheckNodesCoversTheBackwardLoopAndTheBeginningEdge",
    "LateRunFollowsOnlyWhileFollowIsOn",
    "LoopWrapReportsTheTriggersOfBothStretchesInOrder",
    "SetPercentInvokesTheCrossedTriggersOfItsGroup",
    "SetPercentReportsTheNodesItPassed",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckTransformModulePlayModeTests." + name for name in (
    "OffsetSettersSkipEqualValuesRefreshTheFlagsAndRebuildTheUser",
    "RigidbodyAngularVelocityFollowsTheShippedRotationFlags",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckColliderGeneratorPlayModeTests." + name for name in (
    "OffsetSetterQueuesARebuildForAChangedValue",
    "PolygonSettersQueueARebuildForAChangedValue",
    "PolygonUpdateRateAndLocalSpaceFollowTheEdgeGenerator",
    "UpdateRateDefersTheColliderToLateRun",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckLengthCalculatorPlayModeTests." + name for name in (
    "AwakeBuildChecksTheEventsFromTheOldLengthBeforeItFiresTheExactTargets",
    "AwakeMeasuresAndFiresTheEventsWithTheExactLength",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplinePositionerPlayModeTests." + name for name in (
    "AnimatedPositionMovesTheTargetOnce",
    "SetPercentAndSetDistanceReportTriggersAndNodes",
    "TargetObjectReplacesTheOwnerAsTheMovedObject",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineProjectorPlayModeTests." + name for name in (
    "ANewComputerIsProjectedOntoAtOnce",
    "CalculateProjectionReportsTheTriggersAndNodesItPassed",
    "ReachingTheEndOfALoopingRangeContinuesFromTheStart",
    "SettersQueueARebuildOnlyWhereTheShippedOnesDo",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckMeshGeneratorPlayModeTests." + name for name in (
    "BakedGeneratorNeverRebuilds",
    "ConfigurationSettersSkipEqualValuesAndRebuildOtherwise",
    "FreshGeneratorKeepsTheShippedDefaults",
    "SizeStoresAnEqualValueWithoutRebuilding",
    "WriteMeshMovesTheMeshIntoLocalSpaceAndAppliesTheFaceOptions",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckPathGeneratorPlayModeTests." + name for name in (
    "CompensateCornersRebuildsOnChange",
    "ShapeCurveToggleCreatesAFlatCurveOrDropsIt",
    "ShapeExposureNeedsASplineAndADifferentValue",
    "ShapeSetterComparesKeysWithTheBlankCopy",
    "SlicesClampAtOneAndRebuildOnAnyDifferentValue",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineRendererPlayModeTests." + name for name in (
    "SlicesClampAtOneAndRebuildOnAnyDifferentValue",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineMeshChannelPlayModeTests." + name for name in (
    "CountIsRaisedToOneAndTheSeedRebuildsOnlyWithRandomOrder",
    "MeshListChangesRebuildAndRejectBadIndices",
    "RefreshReReadsEveryMeshAndRebuilds",
    "SettersSkipEqualValuesAndRebuildTheOwner",
    "SwapAndDuplicateRespectTheRange",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineMeshPlayModeTests." + name for name in (
    "AddChannelCreatesTheChannelAndOnlyTheMeshOverloadRebuilds",
    "AutoCountKeepsTheCountForAFlatAverage",
    "AutoCountKeepsTheCountForANegativeAverage",
    "RemoveChannelRebuildsAndRejectsABadIndex",
    "SwapChannelsExchangesTheChannelsAndIgnoresBadIndices",
    "UnknownChannelTypeDoesNotAdvanceTheCombineIndex",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckObjectControllerPlayModeTests." + name for name in (
    "ClearGivesTheBaseScaleBackAndHidesOrDestroys",
    "CustomScaleReadsTheOffsetRuleAsShipped",
    "CustomScaleUsesTheStaleContextOfTheOffsetRule",
    "NearlyEqualVectorsQueueNoRebuild",
    "RotationAppliesTheSampleRotationAndTheRandomEuler",
    "SettersQueueARebuildOnlyForChanges",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckObjectBenderPlayModeTests." + name for name in (
    "SettersQueueARebuildOnlyWithASplineAndOnlyForAChange",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSurfaceGeneratorPlayModeTests." + name for name in (
    "ExtrudeSplineFollowsTheComputerItIsGiven",
    "PlainSettersRebuildOnChangeOnly",
    "SideUvSettersStoreTheValueBeforeTheyCompare",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckTubeGeneratorPlayModeTests." + name for name in (
    "PlainSettersRebuildOnChangeOnly",
    "RoundCapLatitudeClampsAtOneAndOnlyRoundCapsRebuild",
    "SidesClampAtThreeAndRebuildOnAnyDifferentRawValue",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckWaveformGeneratorPlayModeTests." + name for name in (
    "SettersRebuildOnChangeAndSlicesClampAtOne",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTimeScaledSplineFollowerTests." + name for name in (
    "CheckIsValidIsTrueOnlyWhileAFollowerIsAssigned",
    "OnValidateFindsTheSiblingFollowerOnlyWhileNoneIsAssigned",
    "InitialiseWithoutAFollowerSubscribesNothingAndKeepsTheSpeeds",
    "InitialiseSubscribesAndCapturesTheFollowersSpeed",
    "InternalUpdateScalesTheCapturedSpeedWithoutCompounding",
    "AnExternalSpeedChangeBecomesTheNewUnscaledSpeed",
    "ASpeedWithinTheApproximateToleranceIsNotCaptured",
    "OnPauseAppliesTheTimescaleToTheFollowerSpeed",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalDreamteckSplineToHLSplineTests." + name for name in (
    "ConstructionDefaultsTheBaseRibbonWidthToOne",
    "GettersReturnTheSerializedFields",
    "InitialiseWithASplineStoresTheSplineAndComputersOnly",
    "InitialiseWithARibbonStoresTheWidthAsTheLastUsedWidth",
    "SerializedFieldsKeepTheirOriginalAttributes",
))
# Original rails proximity and targeting abilities and definitions, the air and ground homing attack states and the rail homing states (gr3-char-moves-08).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterRailsHomingDefinitionTests." + name for name in (
    "RailsProximityAirDefinitionStartsWithTheNativeDefaults",
    "RailsProximityGroundDefinitionWritesOnlyTheInheritedDefaults",
    "RailsTargetingDefinitionStartsWithTheNativeDefaults",
    "RailsProximityAirCachesTheCosineOfTheSurfaceNormalTolerance",
    "RailsLeafDefinitionsRefreshTheInheritedCachesWhenCached",
    "RailsTargetingCachesTheTangentOfEachConeAngleOnItsOwnAxis",
    "DefinitionsNameTheirAbilityScripts",
    "AuthoredFieldsKeepTheirRangesMinimaAndTooltips",
    "DefinitionsKeepTheOriginalDeclarationShape",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterRailsProximityAbilityTests." + name for name in (
    "TheAirLeafTriggersOffTheGroundAndTheGroundLeafOnIt",
    "ByDefaultAnyLocationWithASurfaceCanBeJoined",
    "WithoutTheRailTrackerTheToleranceCosineMustReachTheVelocityDot",
    "TheSurfaceNormalIsTheUpVectorOfTheLocationRotation",
    "WithTheRailTrackerTheHeightAlongTheNormalIsTestedAgainstTheAttachDistance",
    "TheTrackerHeightIsMeasuredFromTheLocationPosition",
    "ALocationWithoutASurfaceNeverJoinsInTheAir",
    "ATriggeredAbilityDoesNothingMore",
    "AnAbilityThatMayNotTriggerAsksForARefresh",
    "RisingAfterAJumpOnTheRailTrackerLeavesBeforeTheAutoAttachCheck",
    "AStoredAutoAttachRailOrAPendingRefreshLetsTheUpdateContinue",
    "WithoutATrackManagerTheProximityCheckFindsNothing",
    "TheAdditionalSpeedCurveComesFromTheDefinition",
    "TheDebugTextShowsWhetherTheIndicatorHasASurface",
    "AbilitiesKeepTheOriginalDeclarationShape",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterRailsTargetingAbilityTests." + name for name in (
    "TheCameraProjectionFlagAndSpeedCurveComeFromTheDefinition",
    "TheRailTargetActionLocksThePendingTargetAtTheDefinitionVelocities",
    "TheLockReadsTheAppliedRailTargetStateNotTheQueuedOne",
    "TheLockNeedsBothAValidPendingTargetAndTheQueuedAction",
    "AnAbilityThatOwnsTheActiveTargetLeavesAtOnce",
    "AnIdleAbilityClearsItsTrigger",
    "TheRailTrackerJumpAndThePositiveRailSwitchTimeBlockTheLock",
    "ARefreshRequestRemovesTheAbilityFromThePooledTargetsAndSpendsTheRequest",
    "ARefreshRequestClearsAnotherAbilitiesActiveTargetOnlyForAValidPendingTarget",
    "ResettingWhileARailIsTargetedMarksTheRailTargetCancelled",
    "ResettingMarksTheRailTargetCancelledWhoeverOwnsTheActiveRail",
    "ResettingAlsoRunsTheBaseResetAfterTheCancelMark",
    "ASurfaceIsTargetableUnlessItsMetadataSaysOtherwise",
    "TheDebugTextEndsWithTheProjectDistanceAndConeAngles",
    "TheAbilityKeepsTheOriginalDeclarationShape",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterHomingAttackAirAndRailHomingStateTests." + name for name in (
    "AirStatesReadTheirAbilityTypeAndIgnoredAbilityListFromJson",
    "GroundAndRailLeafStatesReadTheirAbilityTypeFromJson",
    "TheAirStateDebugTextNamesTheTargetItsSpeedAndTheDistance",
    "TheRailHomingStateDebugTextNamesTheTargetSpeedDistanceAndRotationalSpeed",
    "CallOrderTheAirStateEnterAndLeaveFollowTheNativeOrder",
    "CallOrderTheAirStateUpdateFollowsTheNativeOrder",
    "CallOrderTheRailHomingStateBodiesFollowTheNativeOrder",
    "AirStatesKeepTheOriginalDeclarationShape",
    "GroundStatesKeepTheOriginalDeclarationShape",
    "RailHomingStatesKeepTheOriginalDeclarationShape",
))

# Original transporter proximity ability, transporter states and trigger states (gr3-char-moves-09).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterTransporterAndTriggerStateTests." + name for name in (
    "TriggerStatesReadTheirArgumentsFromJson",
    "TransporterStatesReadTheirAbilityTypeFromJson",
    "TriggerStatesKeepTheOriginalShape",
    "EffectAndAbilityStatesKeepTheOriginalShape",
    "ArgumentClassesKeepTheOriginalShape",
    "AbilityAndDefinitionKeepTheOriginalShape",
    "StatesKeepTheirGraphNodeNames",
    "ArgumentFieldsKeepTheirGraphObjectTypes",
    "DefinitionNamesItsAbilityScriptAndKeepsTheRailsHomingDefaults",
    "ProximityCheckReadsTheStoredSurfaceOnlyForANonZeroDistance",
    "ProximityCheckWithoutATrackManagerReturnsBeforeReadingTheStorage",
    "ProximityCheckTrustsAStoredSurfaceAtAnyDistance",
    "ProximityCheckFailsWhenTheStoredSurfaceLocatesNoComponent",
    "UpdateLeavesATriggeredAbilityAlone",
    "UpdateTurnsTheRefreshFlagIntoWhetherASurfaceIsStored",
    "UpdateSkipsTheStoredSurfaceReadWhileTheRefreshFlagIsUp",
    "GravityStateDisablesGravityThroughAStoredHandle",
    "GravityStateLeavingGravityOnAddsNothing",
    "GravityStateEnablingRemovesTheEarlierDisablingOverride",
    "GravityStateReplacesItsEarlierOverrideInsteadOfStackingIt",
    "ClearHomingTargetEndsTheGracePeriodAndReleasesTheActiveTarget",
    "ClearHomingTargetCanStartTheGracePeriodInstead",
    "ControlLockDisablesTheControlsForTheLockTimeAndThenRestoresThem",
    "ZeroVelocityStopsTheCharacterOnEnterAndOnUpdateOnlyWhenAsked",
    "RespawnLeavingRemovesTheRetainedVelocityFlag",
    "RespawnFinishMarksTheEffectsComplete",
    "RespawnUpdateWithoutAPendingTeleportOnlyReadsTheFlag",
    "SnapCamerasWaitsTwoFixedUpdatesAndEndsWithoutCameraManagers",
    "MovementTipsTheProxyAQuarterTurnAndLaterReleasesIt",
    "CallOrderMovementSurfaceChangeChecksTheTransporterBeforeInformingTheAbility",
    "CallOrderMovementUpdatesVelocityThenOrientationThenTheTracker",
    "CallOrderMovementLeavesByDetachingThenStandingUpright",
    "CallOrderHomingLeavePlacesTheCharacterOnTheTargetBeforeClearingIt",
))

# Original chaos aim and chaos dash definitions, abilities, states and the target indicator controller (gr3-char-moves-10).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterAirChaosDeclarationTests." + name for name in (
    "DefinitionsKeepTheOriginalShape",
    "AbilitiesKeepTheOriginalShape",
    "StatesKeepTheOriginalShape",
    "TargetIndicatorControllerKeepsTheOriginalShape",
    "AuthoredAttributeStringsMatchTheOriginalMetadata",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterAirChaosDefinitionTests." + name for name in (
    "AimDefinitionStartsWithTheNativeRaycastAndLedgeDefaults",
    "AimDefinitionStartsWithTheNativeHomingDefaults",
    "AimDefinitionStartsWithTheNativeCurves",
    "AimDefinitionBuildsItsDictionariesWithTheEnumComparers",
    "AimAndDashDefinitionsNameTheirAbilityTypes",
    "AimCachedConeCosinesUseTheRawAngleAndTheDistanceIsSquared",
    "AimCachedConeHasZeroOriginAndRotationAndDegreeTangents",
    "AimCachedZoneSizeIsTheConeBoundsSize",
    "AimCachedRailTangentsAndLedgeCosinesUseDegrees",
    "AimCachedValuesCacheEveryAimTarget",
    "DashDefinitionStartsWithTheNativeValues",
    "DashCachedValuesTakeTheCosineOfTheOffsetAngleInDegrees",
    "AimTargetStartsWithIdentityRotationsAndBothArrowsEnabled",
    "HomingTargetAimDefinitionStartsEmpty",
    "AimTargetCachesTheLongerCurveOfEachArrow",
    "AimTargetAnimationClocksWrapIndependentlyAtTheirTotals",
    "AimTargetAnimationClockStaysNaNWithoutLooping",
    "AimTargetCameraOverrideIsTheFirstOverrideWithAnInclusiveRangeAroundTheAngle",
    "AimTargetCameraOverrideIsNullForNoOverridesAndFailsForNoList",
    "CameraOverrideDefinitionAlignsToForwardByDefaultAndReadsItsFields",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterAirChaosDashAbilityTests." + name for name in (
    "ActivateStoresTheTargetAndAimsEveryAirComponentAtItInCharacterSpace",
    "ActivateStoresThePositionRelativeToATargetTransform",
    "ActivateRefusesAWallReachedInLessThanTheMinimumTime",
    "ActivateAcceptsAWallReachedInExactlyTheMinimumTimeAndAnyOtherShortDash",
    "ActivateOffsetsAWallTargetBackAlongTheDash",
    "ActivateOffsetsACeilingWallTargetAlongWorldUp",
    "TargetPositionIsRelativeToTheTargetTransformWhenThereIsOne",
    "UpdateDirectionAimsTheComponentsAtTheMovingTarget",
    "PositionSnapLevelsTheCharacterAlongItsForward",
    "DoOnEnterSubscribesThePositionSnapToTheTriggerEvent",
    "CameraOverridePlacesTheCameraTargetAtTheDashTarget",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterAirChaosAimAbilityTests." + name for name in (
    "ConstructionBuildsTheForwardToNormalRotationAndEmptyCollections",
    "HomingContractReadsTheDefinitionZoneAndAcceptsNoTargets",
    "TriggerEnterRegistersTheColliderWithTheFirstDefinitionWhoseTypeIsFound",
    "TriggerEnterFindsTheTargetTypeOnAParentAndIgnoresColliderWithoutIt",
    "TriggerEnterFiltersHomingTargetsByTheDefinitionHomingTypes",
    "TriggerEnterFiltersEnemiesByEnabledStateAndEnemyTypes",
    "TriggerExitForgetsTheColliderAndToleratesAnUnknownOne",
    "ChildHomingTargetsAreDroppedWhenAnAncestorIsAlsoRegistered",
    "HitColliderIsTheNearestSolidColliderInTheCollisionMask",
    "ARaycastHitWithoutCollisionDataIsValid",
    "LedgeHitCastsDownFromAboveAndAheadOfTheAimPosition",
    "GroundProjectionPlacesTheGroundTargetOnTheTrackBelowTheAimPosition",
    "TransformFromHitTakesTheNormalPointOffsetAndRotation",
    "CameraOverrideWithoutCollisionDataIsTheTargetSlopeOverride",
    "CameraOverrideWithCollisionDataTakesTheColliderRotationAndReversesOnlyOnAStrictlyOpposedAxis",
    "TargetTypeEventsWithoutAnEntryOnlyRecordTheEnteredType",
    "UpdateUiReportsNothingWhileTheAimIsInactive",
    "UpdateUiLatchesTheScaleFromTheDistanceBeforeSmoothingAndAdvancesItOnlyWhileLatched",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterAirChaosStateTests." + name for name in (
    "TryGetAbilityAcceptsOnlyAnAbilityOfTheClosedType",
    "UpdateStopsTheCharacterWhenTheStateHasNoAbility",
    "ConstructInstanceBuildsTheNodeOfEachConcreteTypeFromItsJsonAbilityType",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UITargetIndicatorControllerTests." + name for name in (
    "ConstructionStoresTheNativeDefaults",
    "SetStoresEachArgumentInItsOwnField",
    "InitialiseStoresTheCharacterAndInitialisesEveryProjection",
    "OnlyTheThreeGroundTransformTypesAreGround",
    "TransformTypesPlaceTheTransformAtTheAimOrGroundTarget",
    "TranslateTypesFaceTheLocalToWorldUpRotation",
    "CameraFacingTypesKeepTheRotationTheCallbackGave",
    "OverrideRotationIsAppliedAfterTheGivenRotation",
    "ArrowAnimationRotatesAboutForwardAndOffsetsAlongTheTurnedUp",
    "UpdateTargetPlacesTheIndicatorPartsUnderTheTargetRotations",
    "UpdateTargetHidesTheDiscAndGroundRingWithoutAGroundTarget",
    "UpdateTargetScalesTheArrowsOrAdvancesTheirClocks",
    "UpdateTargetShowsTheAirProjectionPartsAndRestartsTheCycle",
    "ProjectionLocationMovesTheMarkerAndShowsTheGroundMarkerOnAHit",
    "ProjectionTargetIsShownAtTheHitOnlyWhenRequested",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.EnemyBrainTransitionTests." + name for name in (
    "EachFlagTransitionReadsOnlyItsOwnStorageKey",
    "AMissingFlagReadsFalseAndStoresTheDefault",
    "EnteringTheFocusTransitionsStoresNoDefaultAndKeepsAStoredTarget",
    "TargetLostIsTrueWithoutAFocusTargetAndStoresTheNullDefault",
    "TargetSpottedClearsTheFocusTargetOnEntry",
    "TargetSpottedSeesNothingWithoutASensedTarget",
    "TheWatchTimerRestartsOnEntryAccumulatesAndIsRemovedOnLeaving",
    "EmissionCheck_IdleAndWatchTimerComparesCompileToGreaterOrEqual",
    "IdleToMoveIsTrueWhenTheStoredTimeReachesTheIdleSeconds",
    "WatchToChaseIsTrueWhenTheAccumulatedTimeReachesTheTimeout",
    "TargetLostWithoutCachingNegatesTheReachabilityOfTheFocusTarget",
    "TargetLostWithCachingIsTrueWithoutACachedPosition",
    "TargetLostWithCachingTimesOutStrictlyAfterTheCachedLocationTimeout",
    "TargetLostWithCachingReadsTheReachabilityOfTheCachedPositionNotTheFocusTarget",
    "TargetSpottedWithCachingStoresTheOvershotPositionAndRestartsTheCacheTimer",
    "TargetSpottedWithCachingRejectsAnUnreachableOvershotPositionButKeepsTheFocusTarget",
    "TargetSpottedWithoutCachingStoresOnlyTheFocusTarget",
    "TargetOutOfSightRangeNegatesCanSeeTargetForTheFocusTarget",
    "ConstructInstanceReadsItsJsonBeforeBuildingAndRegistersTheTransition",
    "NodesCarryTheirOriginalGraphAuthoringAttributes",
    "ClassFactoryRegistersTheNodesWithTheirArgumentTypes",
    "DeclarationShapeTransitionsKeepTheirFlagsBasesAndArgumentClass",
    "FireProjectileTriggeredIsFalseWithoutTheAbilityAndFollowsItsTriggeredFlag",
    "FireProjectileTriggeredLooksUpTheAbilityTypeItWasConfiguredWith",
    "FireProjectileTriggeredIgnoresATriggeredAbilityOfAnotherClass",
    "FireProjectileTriggeredLeavesThePublishedTargetPositionOnTheBrain",
    "FireProjectileTriggeredReadsItsJsonBeforeBuildingAndRegistersTheTransition",
    "DeclarationShapeFireProjectileTriggeredKeepsItsBaseMenuAndMembers",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.EnemyStateTriggerGravityForceTests." + name for name in (
    "TriggerStartForwardsTheConfiguredFlagToTheEnemyBody",
    "TheFlagDefaultsToEnabled",
    "TheBodyDecidesWhetherTheForceCanBeEnabled",
    "TheEventTypeDecidesWhenTheStateFires",
    "ANullOrDestroyedEnemyIsSkipped",
    "NodeCarriesItsOriginalGraphAuthoringAttributes",
    "ClassFactoryRegistersTheNodeWithItsArgumentType",
    "DeclarationShapeKeepsFlagsBasesAndArgumentClass",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.EnemyNavigationTests." + name for name in (
    "ForwardStartsWithTenUnitsAheadAndInitialisesOnSetUp",
    "ForwardDestinationIsAHeadOfTheTransformAlongItsForward",
    "ForwardNeverConstrainsAndHasNothingToStartOrInterrupt",
    "HomeRadiusStartsWithTenAndFive",
    "SetUpTakesTheHomeFromTheTransform",
    "InterruptingOnlyRaisesTheFlagOfTheHomeRadiusNavigation",
    "ValidatingZeroesAMinimumDistanceThatReachesTheRadius",
    "TheDistanceConstraintReachesPointsWithinItsDistanceOfHome",
    "TheDistanceConstraintPullsAnUnreachablePositionBackToTheDistance",
    "TheVolumeConstraintFollowsTheColliderClosestPoint",
    "StartingPlacesTheFirstSpotAtTheFirstDrawAlongXAndTheSecondAlongZ",
    "StartingPlacesTheSpotsOnTheEnemysOwnPlane",
    "TheSpotSeparatesTheRotationOperandOrderAndTheDrawOrder",
    "StartingPicksTheFirstSpotWithinTheRadiusAndFarEnoughFromTheEnemy",
    "StartingAcceptsASpotExactlyOnTheRadiusAndExactlyAtTheMinimumDistance",
    "StartingKeepsTheTwentiethSpotWhenNoneQualifies",
    "StartingMeasuresTheMinimumDistanceFromTheEnemyProjectedOntoThePlane",
    "StartingMovesTheHomeToTheEnemyOnlyAfterAnInterruptWhenAsked",
    "SplineStartsWithAFollowDistanceOfThreeAndAStepOfOne",
    "WithoutASplineTheSplineNavigationStaysUninitialisedAndFallsBackToTheTransform",
    "NavigationsCarryTheirOriginalIl2CppOptions",
    "AuthoredFieldsKeepTheirEditorAttributes",
    "DeclarationShapeNavigationsKeepTheirFieldsAndMethodFlags",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.EnemyDebugTests." + name for name in (
    "TheContainerIdentifierIsTheEnemyDebugInfoName",
    "InitTakesTheContainerAndBindsTheBossDebugInputToTheOverlay",
    "InitWithoutTheContainerKeepsTheOwnerAndBindsNothing",
    "TogglingFlipsTheContainerAndRecordsTheStateItFound",
    "TheInputCallbackTogglesWhateverTheAmount",
    "DestroyingUnbindsTheOverlayCallback",
    "DeclarationShapeKeepsFlagsFieldsAndIl2CppOptions",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.BossTransitionTests." + name for name in (
    "WindUpTransitionsReadDifferentFlagsOfTheConfiguredOffensiveAbility",
    "BodyBrokenAccumulatesThePhaseWaitOnlyWhileTheBodyPointIsDestroyed",
    "BodyBrokenEnterResetsTheWaitAndLeaveClearsTheStoredAttackOnly",
    "BodyBrokenLeaveChainsToTheBaseEnterHook",
    "BothClawsBrokenNeedsBothClawPointsDestroyed",
    "BothClawsBrokenReadsTheRightClawPointEvenWhenTheLeftIsIntact",
    "BothClawsBrokenEnterResetsThePhaseWait",
    "ClearLineOfSightKeepsItsRangeAndConeAndNeverSetsThePreAttackWait",
    "ClearLineOfSightIsFalseWhenTheBossHasNoCharacterManager",
    "ClearLineOfSightDropsTheBaseResultAndPassesRangeFullConeAndCone",
    "ConsumeBossManagerFlagReportsTheManagersPhaseFlagOnce",
    "ExitedAnimationReadsTheStoredFlagAndLeaveClearsIt",
    "HealthCheckComparesTheBossHealthWithTheConfiguredValue",
    "HealthCheckLeaveChangesNothing",
    "HitPlayerReadsTheStoredFlagAndLeaveClearsBothFlags",
    "RangeTransitionsReadTheMoveToRangeFlagAndNegateOneOfThem",
    "ActiveBossTransitionsFollowTheBossActiveEntityFlag",
    "ActiveBossTimeoutFiresOnceTheTimerPassesTheTimeout",
    "TimerTransitionsDoNotFireOnANaNWait",
    "UsedClawCheckMatchesTheConfiguredClawAttackAndDeflatedState",
    "UsedClawCheckReadsTheClawPointBeforeComparingTheAttackAndIgnoresOtherClaws",
    "VulnerableTimeOutFiresWhileTheBossIsNotVulnerable",
    "WasHitReadsTheStoredFlagAndLeaveClearsIt",
    "BossesCountFiresOnlyWhileTheListedBossCountEqualsTheConfiguredNumber",
    "BossesInMotionPausesBeforeFiringWhileTheMotionStateHoldsAndResetsTheTimerOtherwise",
    "BossesInMotionFiresOnANaNTimer",
    "BossesInMotionEnterRestartsTheTimerAtZero",
    "FourmenTransitionsDropTheBaseResultBeforeTheirOwnWork",
    "HasCompletedActiveLoopClearsTheActiveBossesOnlyOnceTheLoopIsComplete",
    "HasCompletedActiveLoopIsTrueForNoBosses",
    "DestinationIsScuttleDistanceNeedsTheDesiredPositionStrictlyBetweenTheScuttleRanges",
    "DestinationIsScuttleDistanceIsFalseWithoutAScuttleAbilityOfTheConfiguredType",
    "GuardianMaxRangeReportsWhetherTheStayAheadAbilityIsAtItsMaximumRange",
    "GuardianWithinRangeReportsWhetherTheBossIsInsideTheArcAheadOfTheCharacter",
    "GuardianTransitionsLogAnErrorAndAreFalseWithoutAStayAheadAbilityOfTheConfiguredType",
    "AnimatorSetBossStorageBoolSerializesExactlyItsSixConfigurationFields",
    "DeclarationShapeTypesKeepTheirFlagsMembersAndAttributeOrder",
    "DeclarationShapeBasesAndJsonArgumentClassesMatchTheMetadata",
    "DeclarationShapeClassFactoryRegistersEachNodeWithItsArgumentType",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.BossAbilityDeclarationTests." + name for name in (
    "AbilityDeclarationsKeepTheOriginalFlagsBasesAttributesFieldsAndMethods",
    "StateDeclarationsKeepTheOriginalFlagsBasesAttributesFieldsAndMethods",
    "ProjectileSpawnAndShootBaseConstrainsItsDefinitionType",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.BossCrabAbilityTests." + name for name in (
    "LaserFenceReportsTheDefinitionsHazardShape",
    "RegrowClawCountsTheDeflatedAttacksPerClaw",
    "RegrowClawRegrowsOnceTheCountReachesTheConfiguredUsesAndRestartsTheCount",
    "DeathFlightStartsAlongTheCharactersForwardDirectionAtTheCurveHeight",
    "DeathFlightAdvancesByTheSpeedAndAcceleration",
    "BodySlamFlagsFollowTheNativeStores",
    "BodySlamWindingUpTruthTable",
    "BodySlamCanAttackBetweenTheNearAndTheGrowingFarRange",
    "BodySlamCanAttackTreatsANaNDistanceAsInRange",
    "BodySlamAttackingGlidesToTheTrianglePointThenEnds",
    "BodySlamAttackingUsesTheTrianglePointOfTheAttackIndex",
    "BodySlamUpdateFinishesTheAbilityWhenTheSlamEndsAndLeaveResets",
    "BodySlamEnterLaysOutTheTriangleAroundTheBossBearing",
    "BodySlamEnterStartsTheTriangleAtTheStoredAngleChange",
    "BodySlamEnterKeepsTheTriangleAfterTheFirstJump",
    "BodySlamStrikePublishesTheAttackStrikeEffect",
    "ClawAttackFlagsFollowTheNativeStores",
    "ClawAttackWindingUpWaitsOutTheExtraSecondsOfTheLongAttack",
    "ClawAttackUpdateRunsTheStrikeOnlyWhenWoundUp",
    "ClawAttackAttackingWaitsForTheCharacterThenTheAnimation",
    "ClawAttackCanAttackPicksTheClawOnTheSideOfTheCharacter",
    "ClawAttackCanAttackAlternatesTheClawWhenAClawIsDestroyed",
    "ClawAttackSkipPreparationAppliesTheStoredOverride",
    "ClawAttackEnterReadsTheAttackIndexAndLeaveResets",
    "ClawAttackStrikePublishesTheAttackStrikeEffect",
    "ClawAttackCharacterInAttackRangeIsTrueAtOrBeyondTheNearRange",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.BossMovementAbilityTests." + name for name in (
    "MoveTowardsTargetExposesNoTurningAndNoDefinitionBase",
    "MoveTowardsTargetGlidesAlongTheAccelerationCurveAndReachesItsTarget",
    "MoveTowardsTargetLeaveResetsTheDestinationTimerButKeepsTheMovementTimer",
    "MoveToArenaEdgeAimsAtTheEdgePointAtMinusNinetyDegrees",
    "MoveToBigCircleWithCounterSpinPlacesEachBossOppositeOnTheSmallCircle",
    "MoveToTriangleFormationSpacesTheBossesEvenlyAroundTheirCentroid",
    "MoveToTargetPointChoosesTheFixedTargetOrTheBossOrigin",
    "MoveBigCircleWithCounterSpinOrbitsAPointThatTravelsTheBigCircle",
    "MoveBigCircleWithDecayingRadiusSpiralsInFromTheBossBearing",
    "MoveBigCircleWithDecayingRadiusFinishesAtTheEndingRadiusNearTheChargingPoint",
    "TurnTowardTargetAimsAtTheOffsetToTheCharacter",
    "SpinOnSpotTurnsClockwiseUntilTheEndRotationIsReached",
    "SpinOnSpotTurnsAnticlockwiseThroughNegativeAngles",
    "SpinOnSpotAcceptsAnAngleAtTheToleranceBoundary",
    "StayAheadArcAngleIsTheGroundAngleFromForwardInDegrees",
    "StayAheadCalculatesTheCharacterAngleInRadiansAndTheBossAngleInDegrees",
    "StayAheadShouldActivateOnlyInsideTheArcAheadOfTheCharacter",
    "StayAheadShouldNotActivateWithoutACharacter",
    "StayAheadUpdateKeepsTheBossBetweenTheArcLimits",
    "StayAheadUpdateClampsCounterClockwiseAndWithAMinimumAboveTheMaximum",
    "StayAheadUpdateWrapsTheBossAngleAFullTurnTowardsTheCharacter",
    "StayAheadEnterResumesFromTheStoredAngleOrTheCharacterAndLeaveStoresIt",
    "ScuttleReportsTheSquaredRangesOfItsDefinition",
    "ScuttleFollowsTheDesiredPositionAndFinishesAfterWaitingAtIt",
    "RetreatingJumpReportsTheSquaredJumpRange",
    "RetreatingJumpFliesAlongTheCurvesAndEndsAtFullProgress",
    "RetreatingJumpEnterNotesThePositionAndLeaveResets",
    "MoveRandomStartsWithAnUnchosenArcAndTheDefinitionsTurning",
    "MoveRandomEnterChoosesACircleThroughTheBossAndAFinalTargetOnIt",
    "MoveRandomAcceleratesToTopSpeedAndThenCruises",
    "MoveRandomBrakesBeforeTheFinalTarget",
    "MoveRandomSetsUpANewCircleNearTheFinalTarget",
    "MoveRandomFinishesOnceTheBossAndItsTargetAreBeyondTheAllowedDistance",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.BossChargeAndRangedAbilityTests." + name for name in (
    "ChargeReportsTheDefinitionsTurningAndCanAlwaysAttack",
    "ChargeFlagsFollowTheNativeStores",
    "ChargeWindUpAimsAtTheCharacterWhileTheDebugWindUpRuns",
    "ChargeAcceleratesToThePlayerPositionThenDeceleratesAndEnds",
    "ChargeStopsWhenItLeavesTheAllowedDistanceHeadingOutward",
    "ChargeLeaveResetsTheChargeState",
    "RangedAttackReportsTheDefinitionsRangesAndNoTolerance",
    "RangedAttackFiresWavesOfShotsSeparatedByTheCooldowns",
    "RangedAttackSpreadsEachShotWithinTheAccuracy",
    "RangedAttackLeaveResetsTheWaves",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.BossProjectileAbilityTests." + name for name in (
    "ProjectileFlagsFollowTheNativeStores",
    "WindingUpTruthTable",
    "SpawningWaitsForTheAttackAnimationThenEnds",
    "LeaveClearsTheGroupAndCountsIt",
    "ProjectileStrikePublishesTheAttackStrikeEffect",
    "RowsTakesItsDefinitionFromItsOwnInitialise",
    "RowsEnterSetsUpTheRowPattern",
    "RowsEnterKeepsTheLanesAndSkipsTheSplinesOtherwise",
    "RowsEnterAvoidsTheRecentlyUsedPatterns",
    "RowsDecideTheReturningProjectileByGroupAndLane",
    "RowsLaneHelpersUseTheListedLanes",
    "RowsAttackingCountsTheLaunchTimeThenStrikes",
    "RowsUpdateRunsTheAttackAndFinishesWhenItIsOver",
    "OneAtATimeTakesItsOwnDefinition",
    "OneAtATimeEnterListsTheLanesAndShufflesTheSpawnOrder",
    "OneAtATimeSetProjectileCountDividesTheGroupTimes",
    "OneAtATimeDecidesTheReturningProjectile",
    "OneAtATimeSpawnsTheProjectilesOnACircleAroundTheSpawnPoint",
    "OneAtATimeSpawningWaitsOutTheDelayBeforeTheLaneMove",
    "OneAtATimeBlocksUsedLanesForTheIgnoreCount",
    "OneAtATimeAttackingStrikesOnceTheLaunchTimeHasRunOut",
    "OneAtATimeAttackingGlidesTheCurrentProjectileTowardsItsLane",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.BossStateTests." + name for name in (
    "ActiveBossSetupConfiguresTheFirstBossUnlessOneIsActive",
    "ActiveBossIncrementAdvancesTheIndexOnly",
    "ActiveBossChangePassesTheTurnToTheNextBoss",
    "ActiveBossResetRestartsTheRotationInTheNativeOrder",
    "ClearAttackOverrideRemovesOnlyTheOverride",
    "ClawRegrowthRepairsTheClawOnceTheUsesAreReached",
    "ClawRegrowthIgnoresOtherAttackTypes",
    "BodySlamStateTurnsTowardsTheCharacterAndMovesToTheJumpTarget",
    "BodySlamStateUsesTheInitialToleranceForTheFirstSlam",
    "DeathFlightStateSpinsAndMovesWhileEnabled",
    "CoolDownStateForcesTheTypeOnEntryAndLeaveAsConfigured",
    "CoolDownStateSparesTheResumingGuardianProjectilesOnlyWhenExceptionsAreAllowed",
    "DecideMovementStoresTheCircleTargetOppositeTheCharacter",
    "DecideMovementDropsTheTargetOntoTheTrack",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.BossAttackFlowAndEffectStateTests." + name for name in (
    "OffensiveAbilityStatesCallTheirNativeInterfaceMemberOnEntry",
    "GuardianReadyForFinalHitInvokesTheGuardianManagersCallback",
    "GuardianReadyForFinalHitWithoutACallbackDoesNothing",
    "KillProjectilesStateKillsTheTypeOnEntryAndLeaveAsConfigured",
    "RestoreAllHealthRefillsTheBossOnTheSelectedTriggerEvent",
    "SetAttackOverrideStoresTheConfiguredAttackType",
    "TriggerEffectStatesStoreTheEffectTypeOnTheirOwnEventOnly",
    "SetProjectileCountPassesTheCountToTheOneAtATimeAbility",
    "SetBossScaleScalesTheRotatorObjectOnTheConfiguredEvents",
    "SetProjectileSpawnPointMovesTheSpawnPointOnTheConfiguredEvents",
    "SetWorldYSetsTheHeightAndReenablesTheRotatorsSmoothing",
    "SnapBodyToRotationSetsTheLocalRotationAndReenablesTheRotatorsSmoothing",
    "SwitchActiveProjectileReturnColliderActivatesTheSpawnColliderAndDeactivatesTheReturnCollider",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.BossMovementAndTurnStateTests." + name for name in (
    "TurnToTargetTurnsTowardsTheAbilityOffsetUntilWithinTheTolerance",
    "TurnToTargetAccumulatesOnTheCurrentRotation",
    "TurnAwayFromTargetMeasuresFromTheReversedForwardDirection",
    "TurnToCharacterTurnsTowardsTheCurrentCharacter",
    "TurnToCharacterComparesTheAngleStrictlyAndTurnsNegativeAtZero",
    "TurnToBossManagerTargetMeasuresFromTheAveragePositionToTheBoss",
    "ForcedRotationReplacesTheRotationWithTheSpinAngle",
    "MoveToTargetDoesNothingWhileDisabledOrAlreadyAtTheTarget",
    "MoveToTargetPlacesTheBossOnTheTargetAndFeedsTheSquaredSpeed",
    "MoveToTargetTreatsAStoredApproximatelyZeroPreviousPositionAsTheTarget",
    "MoveToTargetTreatsAMomentumTargetAsTheHorizontalVelocity",
    "MoveToTargetDrivesTheSmoothedRotatorFromTheNormalisedLocalDirection",
    "MoveToTargetTakesTheTargetIntoTheHostTransformsLocalSpace",
    "MoveToTargetLeaveStopsTheBoss",
    "RetreatingJumpFollowsTheJumpTargetAndTurnsTowardsTheCharacter",
    "RetreatingJumpDoesNothingWhileTheAbilityIsDisabled",
    "ScuttleMovesBySignedCurveSpeedsAndTurnsTowardsTheCharacter",
    "ScuttleTakesTheTargetIntoTheHostTransformsLocalSpace",
    "ScuttleNegatesAZeroLocalAxis",
    "ScuttleDropsTheBossOntoTheTrackWithinTheRayLength",
    "ScuttleLeaveStopsTheBoss",
    "ShootLasersSwitchesTheEmitterOnAndOffExceptForTheSecondBossOfAPair",
    "ShootLasersDoesNothingWhileTheAbilityIsDisabled",
    "ShootLasersAimsStaticTargetsFromTheOriginToTheFencesTarget",
    "ShootLasersUpdateSkipsTheSecondBossOfAPairAndADisabledFence",
    "ShootLasersLeaveSwitchesTheLasersOffEvenWhileTheFenceIsDisabled",
    "ShootLasersAimsAtTheNextBossAndShapesTheHazardFromTheFence",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalFidelityGapTests." + name for name in (
    "PlayIntroSequencesArgsDefaultTheControlsDelayToATenthOfASecond",
    "BossesInMotionArgsDefaultToNoneInMotionCausingTheTransition",
    "UnsupportedPluginReportsThePlatformWhateverTheDevelopmentFlag",
    "ModuleInfoInterfaceMatchesTheShippedDeclaration",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.AnimatorSetBossStorageBoolTests." + name for name in (
    "EntryResolvesTheBossAndStoresTheEntryValueUnderTheNamedKey",
    "TheFirstEntryBuildsBothKeysAndLaterEntriesKeepThem",
    "AnEmptyOrMissingNameLeavesTheKeyDefault",
    "AnExitNameAloneStillBuildsTheExitKey",
    "ExitStoresTheExitValueIntoTheBossOfTheLastEntry",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.AppRatingRequesterBossTests." + name for name in (
    "ConstructionCachesTheMissionManagerReferenceAndKeepsTheSevenDayCooldown",
    "ARecentRatingRequestKeepsTheBossRequesterFromFiring",
    "AnElapsedCooldownWithoutAMissionManagerStillRefusesTheBossRequest",
    "ValidateFiresOnlyForAnElapsedCooldownAndACompletedMissionOfTheBossLevel",
    "ValidateAsksTheMissionManagerOnlyAfterTheBaseCheckAndAnExistingManager",
    "DeclarationShapeKeepsTheAssetMenuAndTheSealedRequesterSurface",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.LevelRequirementAppStorageTests." + name for name in (
    "TheAuthoredValueIsComparedWithTheStoredValueOrTheAuthoredDefault",
    "ALaterStoredValueIsReadAfterTheKeyWasCached",
    "ChallengesAreOpenOnlyForAStoredTrueFlag",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.LevelRequirementSaveDataTests." + name for name in (
    "AnyCompletedMissionOfTheLevelMeetsTheAtLeastOneRequirement",
    "ALevelRewardIsUnlockedOnlyOnceItsRecordIsCollected",
    "ARewardTrackIsUnlockedOnlyAfterItsOwnUnlockStateLeavesLocked",
    "ARewardTrackWithoutASaveManagerIsLocked",
    "ASpecificMissionIsCompleteWhenItsRecordInTheLookedUpLevelIsComplete",
    "ASpecificMissionWithoutTheManagersIsNotComplete",
    "ANotCompletedMissionRequirementNegatesTheRecordedCompletion",
    "ANotCompletedMissionRequirementWithoutAMissionManagerIsNotMet",
    "AChallengeRewardIsCollectedOnlyWhenItsSaveRecordIsCollected",
    "AChallengeRewardWithoutASaveManagerIsNotCollected",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.LevelRequirementChallengeFeatureTests." + name for name in (
    "TheFeatureIsSeenOnlyInTheFinalState",
    "TheFeatureIsUnlockedInEveryStatePastLocked",
    "TheShadowFtueIsAvailableOnceItsMissionIsCompleteAndTheFeatureIsNotLocked",
    "TheUnlockedStatesAreTheThreeStatesPastLockedInOrder",
    "WithoutAChallengeManagerNoFeatureRequirementIsMet",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.LevelRequirementProgressionTests." + name for name in (
    "ThePreviousActRequirementChecksOnlyTheFirstMissionOfThePreviousLevel",
    "ThePreviousZoneBossRequirementChecksTheFirstMissionOfTheBossOfThePreviousZone",
    "ACategoryIsLockedUntilEveryBossLevelOfThePreviousZoneIsFinished",
    "EveryGroupBeforeTheRequirementsGroupMustBeComplete",
    "EveryListedMissionMustBeCompleteInItsFirstLevel",
    "OnlyExistingPersistentTrackerStatesCanFailThePersistentRequirement",
    "ALevelOutsideTheGameLevelsDoesNotHaveEnoughRewards",
    "EveryRewardRequirementOfAListedLevelMustBeReached",
    "OrbGatedGroupsNeedAtLeastTheAuthoredOrbCountCollected",
    "AMissionHostedByTwoLevelsIsReadFromTheFirstHostingLevel",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.LevelRequirementDeclarationTests." + name for name in (
    "GameplayLevelRequirementsKeepTheirMetadata",
    "TheGenericAppValueRequirementKeepsItsConstraintAndSerializedFields",
    "MissionGroupAndCategoryRequirementsKeepTheirMetadata",
    "PromptSaveSequencedAndLeaderboardTypesKeepTheirMetadata",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.PromptTriggerActionTests." + name for name in (
    "HidingAllPromptsWithoutAPromptManagerDoesNothing",
    "HidingAllPromptsOfAnIdlePromptManagerLeavesNoActivePrompt",
    "HidingAllPromptsHidesEveryActivePrompt",
    "AnEffectWhosePromptIsRefusedCompletesAtOnce",
    "TheEffectCompletesWhenItsOwnPromptIsHiddenAndIgnoresOthers",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.TimeTrialLeaderboardDevTests." + name for name in (
    "TheDevLeaderboardIsAlwaysReadyAndIgnoresChallenges",
    "ASubmittedTimeReplacesTheLabelOfItsLeaderboardAndShutdownClearsThem",
    "ShowingALeaderboardOnlyReadsTheDebugMenuReference",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OfflineAnalyticsConsumersTests." + name for name in (
    "TheDefaultOfflineStartupLeavesBothManagersUnregisteredAndTheConsumersFollowThePolicy",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ApplicationTransitionCanTriggerAnalyticsConsentScreenTests." + name for name in (
    "WithoutAConsentManagerNoConsentScreenIsTriggeredDuringBoot",
    "WithTheAdaptersOffTheOriginalBodyNeedsTheConsentManager",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SaveGameUIActionsTests." + name for name in (
    "DeletingAllSavesClosesTheOpenSaveReplacesEverySlotAndStartsReopening",
    "DeletingAllSavesWithoutAnOpenSaveDoesNotStartReopening",
    "ResettingAnalyticsWithoutAnAnalyticsSaveTouchesNothing",
    "ResettingAnalyticsReplacesTheAnalyticsSaveAndReloadsBothConsumers",
    "WithTheAdaptersOffTheOriginalStatementsReloadBothConsumers",
    "ResettingAnalyticsWithoutTheAnalyticsConsumersStillReplacesTheAnalyticsSave",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.EnemyAbilityDefinitionTests." + name for name in (
    "MovementDefinitionsStartAt120DegreesTurnSpeedAnd3ForwardSpeed",
    "AirMovementStartsWithoutAHeightRestriction",
    "DeadTauntAndGroundAttackStartWithTheirNativeTimes",
    "FireProjectileStartsWithTheNativeConstructorValues",
    "FireProjectileCachesSquareTheRangeAndTakeTheSineOfEachVerticalLimit",
    "ValidatingAFireProjectileDefinitionRefreshesItsCaches",
    "FireProjectileGettersReturnTheAuthoredValues",
    "EachDefinitionNamesTheAbilityScriptOfTheSameSuffix",
    "DefinitionsKeepTheirBaseTypes",
    "DefinitionsKeepTheirAssetMenuEntries",
    "DefinitionsKeepTheirFlagsFieldsAndMethods",
    "SerializedFieldsKeepTheirAuthoringAttributes",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.EnemyBrainStateTests." + name for name in (
    "ConcreteStatesAreBuiltFromTheirNodeNamesAndRegisteredInTheirMachine",
    "StatesAreListedUnderTheEnemyBrainMenu",
    "StatesKeepTheirFlagsFieldsAndMethods",
    "TurnTowardsPositionTakesItsStorageKeyAndFlagFromTheJsonArguments",
    "AttackEnterRaisesTheAttackFlagOnlyWithAFocusTarget",
    "AttackLeaveClearsTheAttackFlagAndTheWindUpCompleteFlag",
    "AttackAndWindUpUpdatesStopTheMovement",
    "WindUpEnterStartsTheWindUpOnlyWithAFocusTarget",
    "WindUpLeaveClearsOnlyTheStartFlag",
    "CueTargetSpottedReactionRaisesTheAnimationTrigger",
    "IdleEnterUnlocksTheTargetStopsTheMovementAndRestartsTheTimer",
    "IdleUpdateAddsTheFrameTimeAndLeaveRemovesTheTimer",
    "MoveToDestinationEnterStartsOnlyAnInitialisedNavigation",
    "MoveToDestinationUpdateStoresWhetherTheDestinationWasReached",
    "MoveToDestinationLeaveRemovesTheReachedFlag",
    "ResetHomeEnterInterruptsTheNavigation",
    "MoveToTargetEnterLocksTheTargetWithOrWithoutAFocusTarget",
    "MoveToTargetUpdateSteersTowardsTheTargetUnlessAPositionIsCached",
    "MoveToTargetUpdateReadsTheFocusTargetEvenWhenAPositionIsCached",
    "MoveToTargetLeaveRemovesTheReachedFlagAndTheCachedPosition",
    "TurnTowardsPositionTurnsInPlaceAndRemovesTheSpotOnceFaced",
    "SteeringChoosesTurnForwardAndTheResultFromDistanceAndAngle",
    "SteeringTreatsTheToleranceDistanceItselfAsReached",
    "SteeringTurnsBeyondTheFaceAngleAndMovesBelowTheForwardLimit",
    "SteeringBoundariesSeparateTheNativePredicatesFromNeighbouringOnes",
    "SteeringNaNTolerancesFollowTheNativePackedComparisons",
    "SteeringIgnoresTheOffsetAlongTheUpDirection",
    "SteeringMeasuresFromTheEnemyPosition",
))
# Original enemy abilities, the ground, air, dead, idle, taunt, attack and fire projectile abilities, and the enemy movement state and transition.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.EnemyAbilityTests." + name for name in (
    "EmissionCheck_AbilitiesKeepTheirFlagsFieldsAndMethods",
    "InitialiseKeepsTheActorOnlyWhenItIsAnEnemy",
    "MovementKeepsOnlyItsOwnDefinitionClass",
    "TheVisualProxyIsSteeredOnlyWhenDetachedAndFollowsTheNavigationRotation",
    "GroundAttackAndGroundMovementWithTargetKeepTheProxyOnTheEnemyRotation",
    "MovementTriggersFromTheImpulseAndTheTargetLock",
    "MovementTurnsAndAcceleratesByTheCurvesOverHowLongTheImpulseHasBeenHeld",
    "MovementAppliesTheTurnInWorldSpaceForAPitchedEnemy",
    "MovementStopsForwardMotionWhileTheAlertAnimationPlays",
    "EachMovementAxisCountsItsOwnHoldTime",
    "MovementAnimationReportsSpeedTurningMovingAndLateral",
    "GroundMovementWithTargetAnimatesOnlySpeedAndTurning",
    "AirMovementRemovesTheVelocityAlongGravity",
    "AirMovementClampsTheHeightAboveTheGround",
    "AirMovementRaycastIgnoresTriggersAndHonoursTheAuthoredMask",
    "DeadRevivesTheEnemyWhenTheTimerReachesTheReviveTime",
    "DeadWithAZeroReviveTimeStaysDeadAndLeavesTheTimerAlone",
    "DeadStandsStillAndZeroesTheMovementAnimation",
    "IdleNeedsNoImpulseNoAttackAndGround",
    "IdleStandsStillConsumesASightingAndZeroesTheAnimation",
    "TauntEndsTheHitOnceItHasRunForTheTauntTime",
    "TauntWithAZeroTauntTimeNeverEnds",
    "TauntStandsStillAndZeroesTheMovementAnimation",
    "GroundAttackCompletesTheWindUpOnceTheTimerExceedsTheWindUpTime",
    "GroundAttackStoresTheFrameTimeAsTheDefaultOfAMissingTimer",
    "GroundAttackKeepsOnlyADownwardVelocityAndZeroesTheAnimation",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.EnemyFireProjectileAbilityTests." + name for name in (
    "EmissionCheck_FireProjectileKeepsItsFlagsFieldsAndMethods",
    "EnteringClearsTheTriggeredStateAndKillsAWarmingProjectile",
    "LeavingClearsTheTriggeredStateOnlyForAnEnteredAbility",
    "TriggeringRestartsTheClockAndPublishesTheTargetPosition",
    "TheNearestValidTargetIsChosenAndPublishedToTheBrain",
    "ATargetWithoutAnAimNodeIsSkippedAndNoTargetsLeavesTheAbilityIdle",
    "AnAccuracyOffsetStaysWithinItsRadius",
    "AnAccuracyOffsetIsTheSeededInsideUnitSphereSampleTimesTheAccuracy",
    "TargetValidChecksRangeAndTheElevationBand",
    "TargetValidIncludesTheLowerEndOfTheElevationBand",
    "TheProjectileFiresWhenTheWindUpTimeIsCrossedAndTheAbilityEndsAfterTheWindDown",
    "FiringAndFinishingCanHappenInTheSameUpdate",
    "AProjectilePastTheWindUpTimeIsNeverFiredAndTheAbilityNeverFinishes",
    "ANaNClockWithNoProjectileOnlyAdvancesTheClock",
    "TheTurnTowardsPositionStateConsumesThePositionTheAbilityPublished",
    "GizmosAreDrawnOnlyForAnEnteredTriggeredAbilityWithAShootNode",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.EnemyMovementStateTests." + name for name in (
    "EmissionCheck_StateAndTransitionsKeepTheirFlagsMembersAndMenus",
    "ClassFactoryRegistersTheStateAndTheTransitionWithTheirArgumentTypes",
    "TheStateIsBuiltFromItsAbilityArgumentAndRegisteredInItsMachine",
    "UpdateMovesTheEnemyThenItsProxyThenAnimatesWithTheStateAnimation",
    "UpdateStillRunsTheEffectBaseThatPublishesTheResolvedAnimation",
    "AttackWindUpCompleteReportsTheEnemyFlag",
    "AttackWindUpCompleteIsBuiltFromAnyArgumentsThroughTheFactory",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.BossVulnerableStateTests." + name for name in (
    "VulnerableEntryRestartsTheClockAndMarksTheBossVulnerable",
    "VulnerableUpdateEndsTheVulnerabilityOnlyOnceTheClockIsStrictlyPastTheTime",
    "VulnerableUpdateHonoursAndConsumesForceNotVulnerable",
    "VulnerableUpdateWithANaNTimeStaysVulnerableAndReadsTheForcedFlag",
    "VulnerableLeaveResetsTheClockAndTheFlags",
    "TargetEntryMarksTheTargetInactiveAfterTheVulnerableEntry",
    "TargetUpdateActivatesTheTargetOnceTheDelayIsReached",
    "TargetUpdateWithANaNDelayActivatesTheTarget",
    "TargetLeaveSwitchesTheTargetOffAndRemovesTheActiveFlag",
    "ClawsEntryActivatesTheClawThatAttackedAndPublishesTheVulnerableEffect",
    "ClawsEntryDoesNothingFurtherForAnyOtherAttack",
    "ClawsLeaveSwitchesBothClawsOff",
    "WindUpUpdatePublishesTheOffensiveAbilitysWoundUpFlag",
    "WindUpUpdateLeavesTheFlagAloneForAnAbilityThatIsNotOffensive",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SplineProjectileSpawnerTests." + name for name in (
    "FireWithNoConfiguredProjectilesTouchesNothing",
    "FireLooksUpTheConfiguredProjectileTypeInTheManagerPools",
    "FireSetsUpEachNightmareProjectileOnTheSplineAndSubscribesToItsExpiry",
    "FireReportsAnAcquiredProjectileThatIsNotANightmareProjectile",
    "TriggerExitSendsEveryFiredProjectileIntoCooldownWithoutExceptions",
    "ExpiredProjectileLeavesTheFiredListAndLosesTheSpawnerCallback",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.NightmareEggmanProjectileTests." + name for name in (
    "ConstructedProjectileHasTheNativeTuningDefaults",
    "TheTrivialOverridesAndSetSplineDoWhatTheNativeBodiesDo",
    "FireStartsBehindThePlayerOnTheSplineAndFiresTheProjectile",
    "FireKeepsThePhaseClockTellsTheVisualsAndReusesTheTrackerOnRefire",
    "MovementWithoutAWorldSplineDoesNothing",
    "MovingAheadAdvancesByVelocityTimesTheStep",
    "ReachingThePointAheadOfThePlayerStartsTheHold",
    "HoldingStaysAheadOfThePlayerUntilTheStayTimeIsPassed",
    "HoldingWithANaNStayTimeNeverStartsTheCharge",
    "ChargingClosesTheDistanceByTheCurveTimesTheReturnSpeed",
    "ChargingSamplesTheCurveAtTheClockAfterTheStep",
    "ReachedPlayerClosesAtThePlainSpeedAndCoolsDownAfterTheExplosionTime",
    "PhaseChangesAndStepsReachTheVisualBehaviours",
    "ACollidingVisualBehaviourSweepsFromWhereItWasLastFrame",
    "ASweepThroughTwoWallsReportsTheLastHitTheCastReturns",
    "TheProjectileCollidesThroughItsVisualBehaviours",
    "ABehaviourWithNestedBehavioursCollidesOnlyThroughThem",
    "PhaseSettingsStartWithTheNativeTimes",
    "AwakeRollsEveryPhaseOffsetAndCachesTheCharacterManager",
    "RegisteringAPhaseSelectsTheLastMatchingSettingsAndCarriesOffsets",
    "RegisteringAPhaseWithoutSettingsKeepsThePreviousSettings",
    "OffsetMovementScalesCurvedAxesByTheirCurves",
    "ChargingWithHomeInBlendsTowardsThePlayerAndWithoutItUsesTheOffset",
    "ReachedPlayerKeepsMovingByTheLastStep",
    "MovementIsPassedOnToTheNestedBehaviours",
    "RotationFollowsThePlayerOrTheMovementDirectionOfThePhase",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.BossSubstituteObjectTests." + name for name in (
    "EnablingSubstitutesTheBossWithTheDefinitionAndHidesItsVisualProxy",
    "EnablingKeepsABossThatIsAlreadyChosen",
    "EnablingToleratesABossWithoutAVisualProxy",
    "DisablingPlacesTheBossOnTheGroundWithinTheNativeReach",
    "FourmanEnablingSubstitutesTheVisualProxy",
    "FourmanEnablingLeavesTheReplacedObjectAloneWithoutAVisualProxy",
    "FourmenEnablingPicksTheBossAtTheAuthoredIndex",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.TimedBossCameraTargetTests." + name for name in (
    "InitialiseJoinsTheBossCameraGroupAndStartsTheLifetime",
    "DisablingLeavesTheCameraGroupUnlessTheBossIsGone",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ActorInformationViewTests." + name for name in (
    "AwakeTakesTheEntityActivationManagerReference",
    "EnablingBindsBothDebugInputsAndShowsOnlyTheFirstPanel",
    "DisablingUnbindsOnlyTheNextInfoInputAndForgetsTheActor",
    "NextInfoCyclesThroughThePanels",
    "ClearingFsmInfoChangesNothingInRetail",
    "UpdateWithoutAnActorLeavesTheTextsAlone",
    "UpdateWithAnActorRepublishesEveryText",
    "BossInformationTakesTheSceneBossBeforeBinding",
    "AbilityInfoParametersKeepTheBossAndTheAbility",
    "AbilityInfoSetupIgnoresOtherParameters",
    "AbilityInfoSetupHidesWithoutAnAbilityAndOtherwiseParentsToTheBoss",
    "AbilityInfoUpdateShowsTheAbilityTextAboveTheBoss",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.AutoPlayCameraStartPointTests." + name for name in (
    "AwakeForcesTheVirtualCamerasToTheStartPoint",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CameraAversionWithLimitTests." + name for name in (
    "ADefaultComponentOrbitsAtRadiusTwoOnNoAxis",
    "TheStartupCallbackCapturesTheBrainOutputCamera",
    "UpdateWithoutACameraLeavesThePositionAlone",
    "UpdateMovesFromTheCapturedRestPositionAlongTheCameraForwardOnTheEnabledAxes",
    "TheRestPositionIsTheLocalPositionCapturedInAwake",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CameraComponentDeclarationTests." + name for name in (
    "EmissionCheck_StartPointAndSimpleComponentsKeepTheirDeclarations",
    "EmissionCheck_FacingListenerAndCameraTypeComponentsKeepTheirDeclarations",
    "EmissionCheck_CinemachineClientComponentsKeepTheirDeclarations",
    "EmissionCheck_InputHandlerAndHeadingTriggerKeepTheirDeclarations",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CameraFacingListenerTests." + name for name in (
    "EnteringStayingAndLeavingFollowTheAlignmentThresholds",
    "AnAlignmentEqualToTheEnterThresholdEnters",
    "AnAlignmentEqualToTheLeaveThresholdStays",
    "AnInvalidCharacterOrALostLookTargetLeavesOnce",
    "EnablingForgetsTheEnteredState",
    "ResetDefaultsTheLookTargetToTheListenerItself",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CameraHeadingTriggerTests." + name for name in (
    "NoOverridesOrAnUnknownModeSelectNothing",
    "TheIncomingDirectionModeUsesTheDirectionFromTheCharacterToEachOverride",
    "TheAlignedModesPickTheOverrideFacingTheirDirection",
    "AMinimumAngleRuleRejectsOverridesWhoseCosineIsBelowItsLimit",
    "AMinimumAngleRuleKeepsAnOverrideWhoseCosineEqualsItsLimit",
    "TheClosestModeAnswersTheNearestOverrideThatPassesItsAngleRule",
    "TheClosestModeKeepsTheFirstOfTwoEquidistantOverrides",
    "AwakeCachesTheLimitCosineOfEveryOverride",
    "EnteringAppliesTheSelectedOverrideAndExitingRemovesIt",
    "ATriggerThatTriggersOnceIgnoresEveryLaterEnterAndExit",
    "ATriggerThatTriggersOnceOnlyClosesAfterReleasingALiveOverride",
    "ARepeatableTriggerAppliesAgainOnEveryEnter",
    "TheActionsApplyReplaceAndRemoveTheOverride",
    "ContinuousUpdatesSwitchTheOverrideOnlyAfterTheDelay",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CameraInputHandlerTests." + name for name in (
    "TheConstructorTakesTheCameraMappingFromTheCharacterConstants",
    "TheConstructorKeepsNoMappingWhenTheCharacterEntryIsAnotherConstantType",
    "TheConstructorFaultsWithoutTheCharacterConstants",
    "EachAxisIsReportedAsAHeldButtonOfItsDirectionWithTheAbsoluteValue",
    "NothingIsReportedWhileTheHandlerIsNotRegistered",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CameraTypeOverrideComponentTests." + name for name in (
    "RequestChangeStartTakesTheRegisteredCameraManager",
    "RequestingAChangeSwitchesTheCharacterCamera",
    "ApplyingAndRemovingTheCameraTypeMovesThatCamerasPriorityAndCompletes",
    "WithoutTheCameraManagerOnlyAHandlelessRemovalCompletes",
    "AProxyTargetTransformIsAssignedOnApplyAndReturnedToTheCharacterOnRemoval",
    "EnteringWithoutDelaysOverridesTheCameraTypeAndItsTargetsAndLeavingReleasesIt",
    "ADelayThatIsNotPositiveAppliesTheOverrideAndTheTargetsAtOnce",
    "EnteringAgainReleasesTheEarlierOverrideBeforeApplyingTheNewOne",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CinemachineAddTransformToTargetGroupTests." + name for name in (
    "ANewComponentJoinsAndLeavesWithUnitWeightAndRadius",
    "AddingAppendsTheOwnTransformWithTheAuthoredWeightAndRadius",
    "ClearingParksTheOtherMembersLastToFirstAndRestoringBringsThemBackInThatOrder",
    "RemovingWithoutTheRestoreFlagLeavesTheParkedMembersParked",
    "AddingTwiceReplacesTheParkedListWithTheFirstAddsOwnEntry",
    "EnableAndDisableFollowTheirOwnFlags",
    "OverridingTheTargetGroupRedirectsLaterAdds",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CinemachineClientComponentTests." + name for name in (
    "TheTargetIsAssignedOnceWhenTheMainCameraBecomesReady",
    "AnAlreadyReadyMainCameraAssignsTheTargetAtOnce",
    "EachTargetFlagControlsItsOwnTarget",
    "DisablingBeforeTheManagerExistsLeavesTheStartupSubscriptionInPlace",
    "ARecenterComponentResetsToHalfAndAllowsResetOnCameraLiveByDefault",
    "ActivatingALevelStoresTheResetValueInTheYAxis",
    "ACameraGoingLiveResetsTheYAxisWhenAllowedOrComingFromAListedCamera",
    "TheManagersYAxisRecenterRequestResetsTheAxisUntilTheComponentIsDisabled",
    "ASensitivityWithADefinitionSetsTheFourAxisParametersOnly",
    "TheManagersSensitivityChangesReachTheComponentUntilItIsDestroyed",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CinemachineCameraSupportComponentTests." + name for name in (
    "BothAxesAreEnabledByDefault",
    "EnabledAxesReadTheCameraManagersControllerMovement",
    "ADisabledAxisAndTheThirdAxisReadZero",
    "ANegativeAxisFaultsInTheVectorIndexer",
    "TheClipPlaneExtensionStartsWithItsAuthoredDefaults",
    "EnablingTheExtensionRecordsWhetherItsCameraIsAFreeLookCamera",
    "TheLookAtTransformIsTheVirtualCamerasLookAtTarget",
    "TheCharacterVariantLooksAtTheCurrentCharacterElseTheCamerasTarget",
    "AWallBetweenTheCameraAndTheTargetPushesTheCameraInFrontOfIt",
    "TheNearPlaneCornersAreSpreadByTheNearClipPlaneScale",
    "AnIgnoredColliderOnlySwitchesTheOcclusionFeaturesOn",
    "ThePostPipelineCallbackIgnoresOtherStagesCamerasThatAreNotLiveAndMissingTargets",
    "AFreeLookExtensionIgnoresTheCallbackOfItsRigs",
    "TheFrustumCornersAreRecomputedOnlyWhenTheLensChanges",
    "TheAssignerFollowsNothingAndLooksAtTheCharacterByDefault",
    "StartAssignsTheCurrentCharactersVisualProxyAsTheLookAtTarget",
    "AssignPlayerAsTargetFollowsOnlyWhenFollowIsEnabled",
    "ACharacterThatBecomesCurrentLaterIsAssignedThroughTheRegisteredCallback",
    "DestroyingTheAssignerReleasesItsCharacterChangeCallback",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CinemachineFreeLookRecenterTests." + name for name in (
    "TheDefaultYAxisValueAndRevertSpeedStartAtOneHalf",
    "AwakeAdoptsTheManagerRegistersTheCameraAndTakesTheDefaultSettings",
    "TheYAxisMovesTowardsTheTargetBySpeedTimesDeltaTime",
    "TheYAxisOverrideFlagRaisesTheStartAndEndEvents",
    "AnUnsetYAxisOverrideEventIsTolerated",
    "RecenterNowEnablesTheRecenteringWithTheSnapTime",
    "CameraXAxisSnapRecentersTheCameraHeadingWhetherOrNotACharacterIsValid",
    "FixedUpdateAdoptsTheCharactersHeadingOverrideAndRemembersItsPosition",
    "FixedUpdateWithoutACurrentCharacterLeavesThePreviousPosition",
    "FixedUpdateRunsTheBaseSnapTimeout",
    "TheTargetForwardHeadingIsTheCharactersForwardWhenNoProxyOrientsIt",
    "TheVelocityHeadingIsTheFlattenedVelocityUnlessItIsNearlyParallelToUp",
    "ThePositionDeltaHeadingIsTheFlattenedMoveSinceTheLastFixedUpdate",
    "AnUnknownHeadingTypeUsesTheCharactersOwnForwardAndUp",
    "AnActiveHeadingOverrideSuppliesItsTargetsOrientation",
    "TheCameraForwardAngleIsMeasuredFromTheTargetForward",
    "TheHeadingIsMaintainedWhileKeptForTheIdleCharacterOrInsideTheStoppedInputWindow",
    "AnExpiredWindowMaintainsTheHeadingOnlyWhileTheCameraIsWithinTheStoppedInputAngle",
    "WhileSnappingAnAngleAtOrAboveTheExitThresholdChangesNothing",
    "WhileSnappingASmallerOrUnorderedAngleEndsTheSnap",
    "NeverModeTurnsTheRecenteringOff",
    "CameraInputOrAHeldMaintainHeadingStopsRecenteringAndStampsTheTime",
    "AnIdleCharacterKeepsItsHeadingThroughCameraInputAndRecentersWithoutIt",
    "AlwaysModeRecentersWithoutInputUsingTheCalculatedTime",
    "ASmoothingValueEasesTheRecenteringTimeTowardsTheTarget",
    "TheDefaultModeRecentersWithHysteresisBetweenTheReEnableAndDisableAngles",
    "TheYAxisOverrideRevertsToTheDefaultValueThenEnds",
    "VerticalCameraInputEndsTheYAxisOverrideWithoutMovingTheAxis",
    "WithoutAHeadingOverrideTheWaitAfterInputStateIsCleared",
    "AnAppliedHeadingOverrideRecentersAtItsOwnTimeAndSkipsTheMode",
    "ADisabledHeadingOverrideIsIgnored",
    "CameraInputDuringAHeadingOverrideStartsTheWaitAndSuspendsOrKeepsTheRecentering",
    "TheWaitAfterInputCountsUpToTheResetTimeThenResumes",
    "AHeadingOverrideThatDoesNotUseTheTargetLeavesTheXRecenteringAlone",
    "AHeadingOverrideWithoutAnExplicitTimeCalculatesTheRecenteringTime",
    "AHeadingOverrideMovesTheYAxisToItsValueUnlessTheCameraIsPushedUp",
    "CameraXAxisOnUpdateEvaluatesTheFreeLookHeadingRecentering",
    "ManualXAxisTakesOverTheFreeLookXAxisWhenTheManagerIsReady",
    "ManualXAxisSnapEnablesItsOwnAxisRecenteringWithTheSnapTime",
    "TheRecenteringHeadingIsTheSignedAngleFromTheProxyForwardToTheCharacterForward",
    "ADegenerateProxyForwardReusesTheLastValidWorldForward",
    "TheXAxisLimitNarrowsTheRangeAroundTheRecenteringHeading",
    "ARangeThatHasReachedTheLimitOnlyReExpressesTheValueAroundTheHeading",
    "ManualXAxisOnUpdateCompensatesTheProxyRotationAndRestoresTheDefaultRange",
    "ManualXAxisCancelsTheAxisRecenteringWhenTheEvaluationSwitchesItOff",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CinemachineMixCharacterTests." + name for name in (
    "TheBaseWeighsEveryCameraByItsCurveAtTheTargetValue",
    "APositiveSmoothingTimeDampsTheValueBeforeWeighing",
    "ANonPositiveSmoothingTimeWeighsTheTargetDirectly",
    "StartAdoptsTheRegisteredCharacterManager",
    "TheBaseInternalUpdateChangesNoWeights",
    "TheMixerIsLiveOnlyWhileABrainShowsItsMixingCamera",
    "AMixerThatIsNotLiveChangesNoWeights",
    "HalfPipeBlendsTowardsTheSideTheCharacterEnteredFrom",
    "HalfPipeFaultsOnAStoredSideThatIsNeitherLeftNorRight",
    "LateralKeepsTheLastInputByDefault",
    "LateralBlendsTheNormalisedHorizontalInput",
    "LateralSkipsAnExactZeroOnlyWhilePreservingTheLastInput",
    "LateralThatIsNotLiveChangesNoWeights",
    "SpeedBlendsTheNormalisedScaledLocalVelocity",
    "SpeedClampsTheNormalisedValueToPlusOrMinusOne",
    "SpeedDefaultsToUnitAxisMultipliersAndThatIsNotLiveChangesNoWeights",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CinemachineParametersBlenderSourceTests." + name for name in (
    "ASourceWithoutSmoothingTakesTheTargetValue",
    "PositiveSmoothingDampsTowardsTheTargetKeepingItsVelocityAcrossCalls",
    "ANonPositiveSmoothingTimeTakesTheTargetValue",
    "StartAdoptsTheRegisteredCharacterManager",
    "TheBaseInternalUpdateLeavesTheBlendValueAlone",
    "LateralMovementReadsTheHorizontalComponentOfTheRawOrProcessedMovement",
    "LateralMovementWithoutACurrentCharacterLeavesTheBlendValue",
    "SpeedNormalisesTheScaledLocalVelocityByTheMaximumSpeed",
    "SpeedClampsTheNormalisedValueToPlusOrMinusOne",
    "SpeedDefaultsToUnitAxisMultipliers",
    "SpeedWithoutACurrentCharacterLeavesTheBlendValue",
    "IsGroundedBlendsToOneOnTheGroundAndZeroInTheAir",
    "IsGroundedWithoutACurrentCharacterLeavesTheBlendValue",
    "CameraInputBlendsTheMagnitudeOfTheCameraMovement",
    "CameraInputInBinaryModeBlendsOneForAnyMovementAndZeroForNone",
    "CameraInputWithoutTheCameraManagerLeavesTheBlendValue",
    "TimeSinceLiveTakesTheCameraOnItsHostWhenReset",
    "TimeSinceLiveWithoutTheCameraManagerLeavesTheBlendValue",
    "TimeSinceLiveCountsWhileLiveAndRestartsAfterGoingOffAir",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CinemachineParametersBlenderTests." + name for name in (
    "AnInactiveFloatParameterContributesZero",
    "AFloatParameterLerpsTowardsTheEndValueNamedByTheSignOfTheBlend",
    "LerpWithNegativeTakesTheSameTableWithTheEndValuesAsArguments",
    "AVectorParameterLerpsEachComponentAndAnInactiveOneIsZero",
    "TheBlendCurveIsSampledAtTheSourceBlendValueWhateverTheFrameTime",
    "TheDefaultBlendCurveIsTheIdentityRamp",
    "TheCallbackClampsTheFrameTimeAtZeroAndEvaluatesWithTheCurveResult",
    "ThePostPipelineCallbackDoesNothing",
    "AwakeConnectsTheExtensionThenInitialisesTheComponents",
    "EvaluateBlendsTheFieldOfViewAndTheDutchAngleOnlyWhenActive",
    "APhysicalCameraTurnsTheBlendedFieldOfViewIntoAFocalLength",
    "EvaluateWritesTheActiveDampingOffsetAndLookaheadRowsToEveryComponent",
    "EvaluateWithNothingActiveLeavesTheComponentsAndTheStateUntouched",
    "TheBasicBlenderTakesOneTransposerAndComposerFromTheVirtualCamera",
    "TheBasicBlenderLeavesNullEntriesWhenTheCameraHasNoComponents",
    "TheBasicBlenderWritesTheFollowOffsetOnlyWhenItsRowIsActive",
    "TheBasicBlenderSuppliesItsAuthoredParameters",
    "TheFreeLookBlenderTakesATransposerAndComposerFromEachRig",
    "TheFreeLookBlenderWritesTheActiveRigOrbitsAndLeavesTheOthers",
    "TheFreeLookTrackedOffsetsAreGatedByTheTopRowsActiveFlag",
    "TheFreeLookBlenderSuppliesItsAuthoredParameters",
    "WithoutABoostingCharacterTheBoostBlendStaysAtTheTransitionCurveStart",
    "TheBoostTimerFallsAtTheBlendOutSpeedAndIsHeldAtTheTransitionCurveEnd",
    "TheBoostBlenderDefaultsToUnitSpeedsAndAConstantTransitionCurve",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CinemachineRecenterHeadingBasicTests." + name for name in (
    "TheCallbackDoesNothingOutsideItsConfiguredStage",
    "TheCallbackDoesNothingWhileNotSnapping",
    "TheCallbackDoesNothingWithoutAFollowTarget",
    "SnappingOrbitsTheCameraTowardsTheSpotBehindTheTargetAtTheCurvedSpeed",
    "ACameraOnTheOtherSideOrbitsTheOtherWay",
    "TheStepNeverOvershootsTheRemainingAngle",
    "WithinTheExitThresholdTheSnapEndsAndTheCameraStaysPut",
    "TheOrbitIsMeasuredInTheFollowTargetsFrame",
    "RotateAroundPointTurnsThePositionAboutThePointAndComposesTheOrientation",
    "RotateAroundPointAppliesTheTurnBeforeTheExistingOrientation",
    "TheSettingsDefinitionIsTheAuthoredOne",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SequencedFollowerTests." + name for name in (
    "ConstructionStoresTheOriginalDefaults",
    "AnInactiveObjectCompletesImmediatelyWithoutMoving",
    "TheDataIsCastBeforeTheActivityCheck",
    "TheFollowerReachesTheTargetInStepsThenCompletes",
    "ADurationShorterThanAStepArrivesInTheFirstStep",
    "ANonPositiveDurationCompletesWithoutMoving",
    "ANonStaticOffsetFollowsTheTargetsRotation",
    "ScalingFollowsTheSameFractionOnlyWhenApplied",
    "TheStartOffsetAndScaleAreCapturedOnceAndTheTargetIsReadEveryStep",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SequencedAnimationTests." + name for name in (
    "ConstructionStoresTheOriginalDefaults",
    "ProgressIsTheFractionOfTheClipAndResetsToZero",
    "TheSpeedIsScaledWhilePlayingAndRestoredWhenTheClipEnds",
    "AZeroLengthClipCompletesInTheFirstCall",
    "TheCollectorRootDefaultsToTheAnimationsTransformOnlyWhenUnassigned",
    "TheCollectorRootFollowsTheTargetAlongItsUpVector",
    "WithoutTheCollectorFlagTheRootStaysWhereItIs",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SequencedCharacterAnimationTests." + name for name in (
    "TheTriggerTypeEnumKeepsItsOriginalMembers",
    "ConstructionLeavesTheTriggerTypeAtBoolTrue",
    "BoolTrueSetsTheParameterAndCompletes",
    "ANullCompletionCallbackIsSkipped",
    "BoolFalseClearsTheParameterAndCompletes",
    "TriggerFiresTheTriggerParameterAndCompletes",
    "ANameLessParameterOrNoCurrentCharacterDoesNothingAndNeverCompletes",
    "AnUnknownTriggerTypeThrowsWithoutSettingOrCompleting",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SequencedCharacterFsmValueSetterTests." + name for name in (
    "StartResolvesTheStorageKeyFromTheSerializedName",
    "TheBoolValueIsStoredOnTheCurrentCharacterAndCompletes",
    "TheIntValueIsStoredOnTheCurrentCharacterAndCompletes",
    "WithoutACurrentCharacterNothingIsStoredButTheEffectStillCompletes",
    "RemoveValueDropsOnlyTheSetterKey",
    "SerializedFieldsKeepTheirOriginalNamesAndAttributes",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OutroSequenceFadeMainDirectionalLightTests." + name for name in (
    "ConstructionStoresTheHalfSpeedMultiplier",
    "StartRemembersTheScenesSunAndItsIntensity",
    "UpdateLerpsTheIntensityTowardsTheMinimumWithAClampedTime",
    "UpdateDisablesTheComponentOnceTheLerpTimeReachesOne",
    "UpdateAddsTheMultipliedFrameDeltaToTheLerpTime",
    "OnDestroyRestoresTheRememberedIntensity",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ComicPanelTimelineTests." + name for name in (
    "ClipConstructionDefaultsTheScaleMultiplierToOne",
    "CreatePlayableCopiesTheAuthoredFieldsAndTheScaleMultiplierOnBothAxes",
    "TheTextureTakesTheTruncatedCutsceneSizeOnce",
    "ACreatePlayableWithoutAManagerDoesNotMarkTheClipInitialised",
    "OnDisableRestoresTheDefaultSizeWithoutMarkingTheClipInitialised",
    "TheTrackAcceptsComicPanelClips",
    "TheTrackMixerIsNamedAfterTheTrack",
    "OnGraphStartCountsTheInputsAndRegistersNothingOutsidePlayMode",
    "ProcessFrameSpawnsDrivesAndDespawnsThePanelByTheInputWeight",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.HoldToSkipInputTests." + name for name in (
    "ConstructionStoresTheOriginalDefaults",
    "SetLabelStringStoresTheIdentifierOnTheLabel",
    "StartResetsTheDisplayAndReadsItsSystems",
    "StartWithoutAModifierKeepsItNullAndWithoutTheDefinitionFaults",
    "TheButtonsPointerEventsHoldAndReleaseTheInput",
    "AZeroInputOnlyRecordsItselfAndAnyOtherLightsThePrompt",
    "TheHeldTimeAndBarFollowTheCategorysDeltaTime",
    "TheFullSkipEventFiresOnEveryUpdatePastTheRequirement",
    "AHeldTimeEqualToTheRequirementDoesNotFullSkip",
    "StartSubscribesEachCallbackOnItsTriggerAndOnDestroyRemovesThem",
    "AShortReleaseSkipsOnceOnlyWhenTapToSkipIsEnabled",
    "EndingClearsTheStateAndFadesThePromptOnlyAfterHeldInput",
    "EndingResetsTheDelayModifier",
    "AHeldTouchSuppliesFullStrengthOrTheModifiersResultEveryStepUntilReleased",
    "TheFadeOutHidesAtOnceWithoutATimeAndOtherwiseStepsTheAlphaDown",
    "ForceReleaseCursorRequestsTheReleaseFromTheApp",
    "OnDestroyClearsTheDisplayAndRevokesTheCursorButOnlyTheUpListenersAreRemoved",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CutsceneTheatreTests." + name for name in (
    "ConstructionStartsEmptyAndSubscribesToTheManagersFinishedEvent",
    "EnqueueTakesOnlyTheFirstCutsceneAndClaimsTheSessionKey",
    "EnqueueRemembersTheUiBackgroundMusic",
    "EnqueueAllQueuesTheSeenTheatreCutscenesInOrderAndClaimsTheKey",
    "EnqueueAllRemembersTheUiBackgroundMusic",
    "EnqueueAllDoesNothingWhileASessionIsRunning",
    "DequeueNextWaitsWhileACutsceneIsPlayingAndKeepsTheQueueOrder",
    "PlayWithoutAChosenCutsceneDoesNothing",
    "ASkippedCutsceneEmptiesTheQueueWhenTheManagerReportsItFinished",
    "CleanupDetachesAndClearsEverythingExceptTheSessionKey",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SequencedControlEffectTests." + name for name in (
    "ContinueAfterTheTriggerCompletesOnceAndTheNextTriggerWaitsAgain",
    "ContinueBeforeTheTriggerIsIgnoredUnlessAllowed",
    "OnlyTriggerOnceResolvesLaterTriggersImmediately",
    "AnEarlyContinueIsKeptWhenOnlyTriggeringOnce",
    "ContinueWithoutACompletionCallbackDoesNotFault",
    "ListenersReceiveTheEffectAndANonWaitingEventCompletesAtOnce",
    "WaitersKeepTheEffectOpenUntilTheLastOneEndsIt",
    "EachTriggerResetsTheWaitCount",
    "AnEndWithoutAStartStillCompletes",
    "BeginSequenceStartsTheCallerThenEndsItThroughItsOwnDelegate",
    "TriggerSubscribesToTheFollowerAndEnablesIt",
    "AnotherFollowersCompletionIsIgnored",
    "TheAuthoredFollowersCompletionUnsubscribesAndCompletesOnce",
    "TheAuthoredFollowersCompletionWithoutACallbackDoesNotFault",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SequencedParticleEffectTests." + name for name in (
    "ResetFindsTheSiblingSpawnerOnlyWhileNoneIsAssigned",
    "TargetedDataMovesTheSpawnPointWithTheScaledOffset",
    "TheSpawnPointIsLeftAloneWithoutAnApplicableOverride",
    "ANonWaitingSpawnWithoutACallbackDoesNotFault",
    "WaitingForTheParticlesIsTheDefault",
    "AnInactiveHostCompletesAtOnceWhetherOrNotItWaits",
    "DataThatIsNotTargetedIsRejectedInSystemMode",
    "ASystemThatIsNotPlayingEndsTheWaitAtOnce",
    "APlayingSystemFollowsTheTargetsUpOffsetPointWhenPlayingOnTarget",
    "APlayingSystemStaysPutWithoutPlayOnTarget",
    "AnActiveHostWatchesTheParticlesAndCompletesAtOnceOnlyWhenNotWaiting",
    "AWrapperCompletesTheEffectThroughItsOwnCallbackOnlyWhenWaiting",
    "OnValidateClearsTheUnusedReferenceForTheCurrentMode",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SequencedMotionAndMaterialTests." + name for name in (
    "MagnetismDefaultsToTenSpeedOneDistanceAndTwoSeconds",
    "MagnetismOnAnInactiveHostCompletesWithoutMoving",
    "MagnetismOnAnActiveHostStartsTheChaseAndWaitsForItToEnd",
    "TheChaseStepsByTheCategorysScaledFixedDeltaTime",
    "TheChaseSpeedsUpByOnePerStepUntilTheTimeoutEnds",
    "TheChaseGoesToThePointAlongTheTargetsUpAxisByTheOffsetsY",
    "ArrivalComparesTheSquaredDistanceWithTheMinimumDistance",
    "ATimeoutThatReachesExactlyZeroEndsTheChase",
    "ADestroyedTargetEndsTheChaseAfterTheNextResume",
    "PropertyAwakeCapturesTheCurrentValueAndResetPropertiesRestoresIt",
    "AZeroBlendTimeAppliesTheEndValueAndCompletesAtOnce",
    "ANegativeOrNaNBlendTimeIsNotTheImmediatePathOfTheTrigger",
    "TheBlendAdvancesByTheCategorysScaledFixedDeltaTime",
    "TheBlendAnimatesFromStartToEndOneFixedStepAtATime",
    "ANegativeOrNaNBlendTimeFinishesOnTheFirstStep",
    "AlphaChangesOnlyTheAlphaChannelAndResetRestoresIt",
    "TheWeightFollowsTheCurveThenSettlesOnTheFinalKey",
    "ACurveEndingAtTimeZeroIsStillSampledOnce",
    "TheCurveAdvancesByTheCategorysDeltaTimeNotItsFixedDeltaTime",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SequencedEffectDeclarationTests." + name for name in (
    "MagnetismKeepsTheOriginalDeclaration",
    "MaterialKeepsTheOriginalDeclaration",
    "MaterialAlphaKeepsTheOriginalDeclaration",
    "MaterialPropertyKeepsTheOriginalDeclaration",
    "ParticleEffectSpawnerKeepsTheOriginalDeclaration",
    "ParticlesKeepsTheOriginalDeclaration",
    "PostProcessingKeepsTheOriginalDeclaration",
    "TransformSurfaceFollowerKeepsTheOriginalDeclaration",
    "UnityEventKeepsTheOriginalDeclaration",
    "WaitForContinueKeepsTheOriginalDeclaration",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SubtitleTimelineTests." + name for name in (
    "MixerStartsWithEmptyTextAndSubtitlesEnabled",
    "TheLastClipWithAPositiveWeightSuppliesTheTextAlphaAndBackground",
    "WithoutAnActiveClipThePanelHidesAndTheLastTextAndAlphaAreKept",
    "ANegativeWeightIsSkippedButANaNWeightIsNot",
    "DisabledSubtitlesLeaveTheTextAndPanelUntouched",
    "ADifferentOrMissingBindingIsIgnored",
    "ADisabledFrameClearsTheAnyTextShowingFlag",
    "TheBackingPanelIsFoundOnlyOnce",
    "ClipCreatesABehaviourWithTheEditModeTextAndAuthoredBackground",
    "TrackCreatesASubtitleMixerWithTheRequestedInputCount",
    "TrackKeepsItsBindingAndClipTypes",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.TimeMachineTimelineTests." + name for name in (
    "BehaviourDefaultsToAMarkerWithAnEndSoftenTimeOfOneTenth",
    "ConditionMetReportsAndConsumesTheMatchingFlag",
    "AnUndefinedConditionIsNeverMetAndConsumesNothing",
    "OnGraphStartSubscribesTheConditionInputForTheUpTrigger",
    "ClipCreatesAClonedBehaviourPlayableFromItsAuthoredTemplate",
    "SkipBehaviourCopiesTheAuthoredFieldsAndTheSkipState",
    "SkipClipCreatesASkipBehaviourPlayableFromItsAuthoredBehaviour",
    "SkipBehaviourDoesNothingOutsidePlayMode",
    "MixerDoesNothingOutsidePlayMode",
    "TrackLabelsEveryClipByItsActionAndRegistersTheMarkers",
    "TrackKeepsItsClipTypeAndColour",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.TimelineTrackAndClipDeclarationTests." + name for name in (
    "SubtitleClipKeepsTheOriginalDeclaration",
    "SubtitleMixerKeepsTheOriginalDeclaration",
    "SubtitleTrackKeepsTheOriginalDeclaration",
    "TimeMachineBehaviourKeepsTheOriginalDeclaration",
    "TimeMachineClipKeepsTheOriginalDeclaration",
    "TimeMachineMixerBehaviourKeepsTheOriginalDeclaration",
    "TimeMachineSkipBehaviourKeepsTheOriginalDeclaration",
    "TimeMachineSkipClipKeepsTheOriginalDeclaration",
    "TimeMachineTrackKeepsTheOriginalDeclaration",
))
PLAYMODE_TESTS += tuple("ProjectLucid.Tests.TimeMachineMixerPlayModeTests." + name for name in (
    "APauseClipPausesTheDirectorOnlyOnce",
    "AJumpToTimeClipMovesTheDirectorToTheTimeOnce",
    "AJumpToMarkerClipMovesTheDirectorToTheMarkerClipsStart",
    "AJumpToEndClipLandsJustBeforeItsOwnEnd",
    "AConditionThatIsNeverMetLeavesTheDirectorPlayingUntouched",
    "AnInputClipConsumesThePressBeforeTheDirectorLookupAndStaysUnexecutedWithoutOne",
    "ClipsWithZeroOrNaNWeightOrAlreadyExecutedDoNotTouchThePendingPress",
    "LaterClipsInTheSameFrameOverrideEarlierTimeChanges",
    "AnInputClipWithADirectorPausesItAndIsMarkedExecuted",
))

# Restored timeline-time components: time-scaled components, manipulator, director, timeline events, hooks.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTimeScaledComponentTests." + name for name in (
    "EmissionCheck_ShaderDefaultsAndEditorTypeKeepTheirDeclarations",
    "TheShaderPublishesTheAccumulatedScaledTime",
    "TheShaderRestartsFromZeroOnlyAfterPassingTheMaximum",
    "ANaNMaximumNeverRestartsTheShader",
    "TheShaderResetSynchroniseZeroesTheAccumulatedTime",
    "OnlyTheFirstCinemachineCoreBecomesTheInstanceAndSubscribes",
    "ADestroyedCinemachineCoreInstanceIsReplaced",
    "TheCinemachineCoreOverridesCinemachineTimeWithTheScaledTime",
    "DisablingTheCinemachineCoreRestoresTheOverridesAndFreesTheInstance",
    "TheBlendShapeTakesItsLengthFromTheLastKeyWhenValidated",
    "TheBlendShapeDrivesShapeZeroAndStopsAtTheLength",
    "ANaNLengthStopsTheBlendShape",
    "PlayRestartsTheBlendAndDisablingResetsTheShape",
    "TheBlendShapeDoesNothingWithoutACurveOrARenderer",
    "TheAnimatorIsValidOnlyWhileItHasAnAnimator",
    "AValidAnimatorSubscribesWhenInitialised",
    "TheAnimatorSpeedIsWrittenOnlyWhenTheScaleChanges",
    "PausingDisablesTheAnimatorAndResumingRestoresItOnlyIfItWasEnabled",
    "TheAnimationIsValidOnlyWhileItHasAnAnimationAndSubscribesThen",
    "PlayClipAssignsTheClipAndTheScaleStartsAtOne",
    "AnIdleAnimationKeepsItsStateSpeeds",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTimeScaledPlayableDirectorTests." + name for name in (
    "ResetFindsTheSiblingDirector",
    "AwakeSwitchesToManualUpdateAndPlaysOnlyWhenAsked",
    "PlayRestartsFromZeroAndSubscribesOnce",
    "StopUnsubscribesAndRewinds",
    "ReachingTheAssetsDurationEndsThePlayback",
    "ATimeEqualToTheDurationEndsThePlayback",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTimeManipulatorTests." + name for name in (
    "ScaleTimeBlendsAlongTheCurveThenSettlesOnTheLastKey",
    "ScaleTimeWithResetAfterSkipsTheFinalBlendAndRunsTheCallback",
    "ScaleTimeWithResetAfterCleansUpBeforeTheCallback",
    "AnInstantCurveStepsOnceBeforeSettling",
    "CleanUpKeepsTheHandleWhileTheManagerIsAbsent",
    "ApplyTimeScalingStoresTheCallbackThenNeedsTheCoroutineHost",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTimelineEventTests." + name for name in (
    "PlayInvokesTheNamedMethodOnTheTargetsComponent",
    "ASingleParameterMethodReceivesTheConvertedArgumentValue",
    "AMethodWithTheWrongParameterCountIsNeverCalled",
    "NothingResolvesInEditModeUnlessTheClipAsksForIt",
    "LaterFramesPlayOnlyWhenTimeAdvances",
    "TheResolvedInformationIsKeptWhileTheKeyIsUnchanged",
    "ANoneKeyResolvesNothingAndDropsTheCachedInformation",
    "UnresolvableKeysFaultWithTheirOwnMessages",
    "TheClipHoldsAFreshBehaviourAndCreatesItsPlayable",
    "TheTrackHandsItsBoundObjectToEveryEventClip",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTimeManagerHelperTests." + name for name in (
    "TheSpeedSettingIndexIsTheFirstSettingTheTimeScaleExceeds",
    "SpeedingUpSelectsTheNextFasterSettingAndStopsAtTheFirst",
    "SlowingDownSelectsTheNextSlowerSettingAndStopsAtTheLast",
    "TheGamePauseToggleResumesFromZeroAndPausesOtherwise",
    "TheButtonLabelIsBoldOnlyForTheOverriddenSetting",
    "StartSubscribesToTheSaveLoadedAndSavedEvents",
    "ASaveStoresTheSavedGameSpeedInTheTimeManager",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalTimelineHookTests." + name for name in (
    "EmissionCheck_HookWrapperAndSequencedEventKeepTheirFlagsFieldsAndMethods",
    "TheHooksRaiseTheirEventsOnlyWhenAssigned",
    "EndingOrSkippingTheVideoStopsItAndRaisesTheCompletedEvent",
))

# Challenge rewards, meta-game unlocks, challenge binders, cross promotion launch, container and start-up, and the
# progress and Game Center views.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ChallengeFeatureRewardTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "FeatureRewardAnswersFromItsAuthoredNameAndOwnGuid",
    "FeatureRewardLoadsItsSpriteIntoTheSpriteCallbackAndUnloadsIt",
    "FeatureRewardSaveCollectsTheRecordWithItsOwnTrackGuid",
    "ChallengeModeRewardUnlocksTheChallengesFeatureOfItsCycle",
    "DreamPowersRewardUnlocksTheDreamPowerStoreItAuthors",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ChallengeDefinitionRewardTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ArchetypeRewardAnswersFromItsDefinition",
    "ArchetypeRewardLoadsTheDefinitionTextureThroughTheTextureCallback",
    "ArchetypeRewardGiveUnlocksOnlyThatArchetype",
    "ArchetypeRewardSaveUnlocksAndCollectsByTheDefinitionGuid",
    "ArchetypeRewardNameIsTheFirstCharacterOfThatArchetype",
    "ArchetypeRewardUnlockIsTheArchetypeUnlockOfItsDefinition",
    "BandRewardAnswersFromItsBand",
    "BandRewardLoadsTheBandImageThroughTheSpriteCallback",
    "BandRewardSaveCollectsTheRecordByTheBandGuid",
    "BandRewardUnlockCarriesTheBandThresholdAndTrack",
    "LevelRewardAnswersFromItsLevel",
    "LevelRewardGiveAndSaveCollectTheRecordWithTheRewardOwnGuid",
    "LevelRewardForwardsTheSpriteCallbackToTheLevelImageLoader",
    "LevelRewardUnlockIsTheLevelUnlock",
    "LevelRewardZoneNameIsTheTitleOfTheLevelZone",
    "SkinRewardAnswersFromItsSkin",
    "SkinRewardLoadsTheSkinThumbnailThroughTheSpriteCallback",
    "SkinRewardSaveUnlocksCollectsAndThenReachesForTheChallengeManager",
    "SkinRewardUnlockCarriesTheSkinThresholdAndTrack",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.MetaGameUnlockSkinAndStoreTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "SkinUnlockReportsItsSkinAndTheCharacterSkinKey",
    "SkinUnlockLoadsAndReleasesTheSkinThumbnail",
    "StoreUnlockReportsTheStoreAndAnswersTexturesWithNull",
    "StoreUnlockSaveFlagsTheStoreAsNewEvenWhenItWasSeen",
    "BandUnlockKeepsItsConstructorValuesAndAnswersTexturesWithNull",
    "BandUnlockMapsTheBandTypeToItsProgressionKey",
    "BandUnlockLoadsAndReleasesTheBandImageNotTheLockedBackground",
    "ItemUnlockReportsTheDreamPowerWithoutAWidgetOrAudio",
    "ItemUnlockLoadsTheIconSynchronouslyAndPassesTheTextureOrTheSprite",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ChallengeManagerBindersTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructorSetsTheLiteralFormatsAndFlags",
    "FeatureStateDrivesTheUnlockedAndNewBindables",
    "NewChallengeCountCountsTheChallengesWithoutAPositiveTimeOrScore",
    "AlertBadgeShowsForANewFeatureOrUnacknowledgedBonusXp",
    "AMissingOrDestroyedBadgeIsSkipped",
    "EnableTakesTheManagerFromTheRegistryOnceAndSubscribesEachTime",
    "DisableTouchesNothingWithoutAManagerAndFaultsWithoutAReference",
    "UpdatePublishesNothingWithoutAReadyManager",
    "ShortFormatsPickTheLargestPositiveUnitPair",
    "LongFormatFillsDaysHoursMinutesSeconds",
    "UpdateRebuildsTheTextOnlyWhenTheWholeSecondsChangeOrTheTextIsBlank",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerCrossPromoTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructorTakesTheRegistryReferencesAndAnEmptyHiddenList",
    "SetupClosesTheOpenContainersItHidesAndShowsTheRequestedAd",
    "SetupWithoutParametersSubscribesBeforeItFaults",
    "ShowForwardsTheSystemTheMetadataAndTheAdIndexToTheView",
    "CloseAnnouncesReopensTheHiddenContainersAndClosesItself",
    "CloseDoesNothingWithoutACrossPromoSystem",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CrossPromoLaunchButtonSdtTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructorStartsWithNoRequirementsAndTheRegistryUiManager",
    "TheLaunchButtonShowsOnlyWhileEveryUnlockRequirementIsMet",
    "ThePlaceholderReplacesTheIconWhileTheContentStaysActive",
    "LaunchRequestsTheFirstAdForCachedDataAndTheCurrentAdOtherwise",
    "RequestingTheFullScreenViewOpensTheCrossPromoContainerOnThatAd",
    "CachedDataIsNotUsedWithLocalStorageAndAnEnabledSystem",
    "CachedDataIsNotUsedWithLocalStorageAndADisabledSystem",
    "CachedDataIsUsedWithoutLocalStorageForAnEnabledSystem",
    "CachedDataIsNotUsedWithoutLocalStorageForADisabledSystem",
    "EnableShowsThePlaceholderForCachedData",
    "EnableLeavesTheBaseHiddenContentWithLocalStorage",
    "EnableLeavesTheBaseHiddenContentForADisabledSystem",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.HLCrossPromoStartTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "TheCoroutineHandlerForwardsToTheHostStartCoroutine",
    "StartWaitsForTheCoreAndTheLanguageThenRegistersACacheOnlySystem",
    "StartRegistersADiskBackedSystemWhenLocalStorageIsAvailable",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ProgressAndGameCenterViewTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "AwakeTakesTheDataManagerReferenceFromTheRegistry",
    "UpdatePublishesTheLoadProgressClampedToTheUnitRange",
    "UpdatePublishesNothingWithoutADataManager",
    "WithoutAGameCenterAwakePublishesFalseAndUpdateLeavesTheValue",
    "WithAGameCenterTheViewPublishesItsLoginState",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CameraProxyComponentTests." + name for name in (
    "ProxyAssignAlwaysClearsTheTargetOnExit",
    "ProxyAssignHandsTheCameraManagersProxyTargetToTheEvents",
    "EnteringPushesTheAuthoredSettingsToTheProxyTargetAndLeavingClearsThem",
    "EnteringAgainReplacesTheEarlierOverrideWithANewHandle",
    "DisablingAndClearingRemoveAnInstalledOverride",
    "WithoutACameraManagerNothingIsInstalledOrRemoved",
    "RecenteringAdoptsTheHeadingExtensionOfACameraThatFollowsIt",
    "RecenteringIgnoresACameraThatFollowsSomethingElse",
    "RecenteringKeepsAnEarlierExtensionWhenALaterCameraHasNone",
    "ACameraGoingLiveIsAdoptedLikeAnyOtherCamera",
    "TheManagerBecomingValidSubscribesToCamerasGoingLive",
    "WithoutAHeadingExtensionTheCharacterUpdateDoesNothing",
    "TheUpdateTurnsTheSmoothedProxyByTheExtensionsXAxisValue",
    "WorldSpaceAwakeAdoptsTheYAxisProxyManager",
    "WorldSpaceUpdateSetsThePositionAndTurnsTheChildByTheCompensatedForwardAngle",
    "WorldSpaceUpdateSubtractsTheProxysOwnHeadingFromTheForwardAngle",
    "WorldSpaceUpdateWithSmoothingSlerpsTheProxyTowardTheTargetFromItsCurrentRotation",
    "WorldSpaceUpdateWithRotationDisabledOnlyMovesTheProxy",
    "WorldSpaceUpdateWithRotationOverriddenOnlyMovesTheProxy",
    "WorldSpaceUpdateIsIgnoredOnceTheProxyIsDestroyed",
    "WorldSpaceSetRotationResetsTheChildOnlyWhileRotationUpdatesAreDisabled",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CinemachineCameraExtensionTests." + name for name in (
    "RelativeOffsetAddsTheFollowTargetsWorldDisplacementToThePositionCorrection",
    "RelativeOffsetIncludesTheFollowTargetsScale",
    "RelativeOffsetAccumulatesAndIgnoresOtherStages",
    "RelativeOffsetIgnoresACameraWithoutAFollowTarget",
    "ApplyingBelowTheLowerThresholdOnlyForgetsTheUnclampedValue",
    "ApplyingAtTheLowerThresholdStartsTheClamp",
    "StartingTheClampRemembersTheUnclampedValueAndNeverRaisesTheAxisPastTheClamp",
    "StartingTheClampAtOrAboveTheClampValueRemembersNothing",
    "AnAppliedClampKeepsItsRememberedValue",
    "RemovingWithoutAClampChangesNothing",
    "RemovingKeepsTheClampWhileTheAxisIsAboveTheRememberedValue",
    "RemovingReleasesTheClampWhenTheAxisReachesTheRememberedValue",
    "RemovingRaisesAnAxisBelowTheRememberedValueBackToIt",
    "RemovingReleasesAtOnceForANonPositiveRememberedValueOrAnAxisPastTheClamp",
    "ApplyingEasesTheAxisUpTowardTheClampByTheScaledCurveValue",
    "ApplyingNeverEasesPastTheClamp",
    "RemovingEasesTheAxisDownTowardTheRememberedValueByTheScaledCurveValue",
    "RemovingSnapsAnOvershootingStepBackToTheRememberedValueAndReleases",
    "RemovingANaNAxisStaysNaNAndKeepsTheClamp",
    "ApplyingWithANaNFrameTimeYieldsNaNInsteadOfTheClamp",
    "ResettingForgetsTheClampAndKeepsTheAxis",
    "AFreeLookCameraIsItsOwnFreeLookCamera",
    "ACameraOfAnotherKindHasNoFreeLookCamera",
    "ACameraIsResolvedThroughItsParentCamera",
    "ASlopeInsideTheBandStartsTheClampAndOneOutsideDoesNot",
    "BothSlopeBoundsAreInclusive",
    "AnUnorderedSlopeBoundTakesTheRemovePath",
    "ASlopeOutsideTheBandReleasesAStartedClamp",
    "EveryGuardLeavesTheClampUntouched",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CameraTriggerAndAssignerTests." + name for name in (
    "ASceneTriggerCameraStartsWithoutATimeoutAndWithPriorityOneHundred",
    "DisablingTheSceneCameraDropsItsPriorityToMinusOne",
    "EnteringRaisesTheCameraAndOptionallyLooksAtTheCharacter",
    "EnteringStartsTheTimeoutUnlessTheExitAfterIsExactlyMinusOne",
    "LeavingDropsTheCameraOnlyWhenNoTimeoutIsConfigured",
    "ExitingAnAlreadyDisabledCameraDestroysNothing",
    "ExitingAnActiveCameraDisablesIt",
    "TheTimeoutWaitsForTheAuthoredSecondsOnTheCameraCategoryAndThenExits",
    "AYUpOverrideTriggerRemovesItsOverrideOnDisableByDefault",
    "EnteringAddsTheYUpOverrideAndLeavingRemovesItButKeepsTheHandle",
    "EnteringAgainReplacesTheEarlierOverride",
    "DisablingRemovesTheYUpOverrideOnlyWhenTheFlagIsSet",
    "WithoutAYAxisProxyManagerTheYUpTriggerDoesNothing",
    "TheGimbalLockCameraRegistersOnceTheYAxisProxyManagerIsValid",
    "TheGimbalLockCameraKeepsWaitingWhileNoYAxisProxyManagerIsRegistered",
    "TheCameraProxyJoinsTheTargetGroupWhileEnabled",
    "TheStartupCallbackAddsTheManagersProxyTarget",
    "DisablingWithoutAProxyTargetRemovesNothing",
    "ThePlayerCharactersVisualProxyJoinsAndLeavesTheTargetGroup",
    "ACharacterChangeAddsTheNewCharactersVisualProxy",
    "ThePreCloseRemovalIgnoresTheCharacterValidityFlag",
    "ToggleRequiredSettingsSwitchesTheDefaultCamerasRecenteringAndInheritedPosition",
    "ToggleRequiredSettingsHandlesCamerasWithOnlyOneOfTheTwoParts",
    "ToggleRequiredSettingsSwitchesOnlyTheRecenterComponentOfANonFreeLookCamera",
    "ASnapTriggerStartsWithTheTwoTenthsSecondDelayAndSnapsOnEnterOnly",
    "EachTriggerDirectionSnapsOnlyWhenItsFlagIsSet",
    "ADelayThatIsNotPositiveSnapsAtOnce",
    "WithoutACameraManagerNothingSnaps",
    "TheTriggerParameterHasStartAndEndAndTheEffectStartsInTheEffectsCategory",
    "StartAppliesTheCameraTypeAndEndRemovesIt",
    "AnUnknownTriggerParameterThrows",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CameraProxyAndTriggerDeclarationTests." + name for name in (
    "EmissionCheck_CameraProxyComponentsKeepTheirDeclarations",
    "EmissionCheck_CinemachineExtensionsKeepTheirDeclarations",
    "EmissionCheck_CameraTriggersKeepTheirDeclarations",
    "EmissionCheck_AssignersAndTheSequencedEffectKeepTheirDeclarations",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CutsceneTheatreFsmNodeTests." + name for name in (
    "HasNextFiresWhenTheQueueEmptinessDiffersFromTheFlag",
    "IsPlayingFiresWhenThePlayingStateEqualsTheFlag",
    "ShouldLoadSceneWantsANamedSceneThatIsNotYetLoaded",
    "SetupMovesTheNextQueuedCutsceneIntoPlace",
    "PlayLeavesATheatreWithoutACutsceneIdle",
    "LoadSceneDoesNothingForACutsceneWithoutAScene",
    "UnloadSceneOnlyPublishesWhenThereIsNothingToUnload",
    "UnloadSceneKeepsASceneTheNextCutsceneShares",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ProgressionUnlockFsmTests." + name for name in (
    "TheCurrentUnlockIsTheHeadOfTheStoredList",
    "NoCurrentUnlockForAnEmptyListClearsTheOutValue",
    "TheTypedCurrentUnlockSkipsNonMatchesAndAlreadySeenIds",
    "RemovingAnUnlockTakesItOutOfTheStoredList",
    "RemovingByTypeKeepsTheOtherUnlocksInOrder",
    "WaitingForAMissingAnimationCompletesAtOnce",
    "EnteringShowsOneUnlockAndClearsTheConfiguredTypesAtTheLimit",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.NotificationManagerTests." + name for name in (
    "ARequestNeedsAConfiguredTypeASecondSessionAndNoGrantedPermission",
    "WithoutASessionManagerTheSavedSessionNumberStillGatesTheRequest",
    "WithTheAdaptersOffTheOriginalSessionComparisonGatesTheRequest",
    "LoadingTheSaveRestoresWhichNotificationsWereAlreadyRequested",
    "PermissionIsFullyGrantedOnlyAfterTheUserAllowedNotifications",
    "RequestingANotificationRecordsItInTheManagerAndTheSave",
    "RequestingAnUnconfiguredNotificationLeavesTheSaveAndTheConfiguredOnesUntouched",
    "WithoutAnalyticsDataTheSaveIsOnlySyncedByTheWaitingCoroutine",
    "TheThirtyDayTimerFiresThirtyDaysFromNow",
    "TheRequestTransitionFollowsTheManagerAndPublishesTheContainerParametersOnlyWhenArmed",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.GenericTriggerStateTests." + name for name in (
    "CameraTriggersDoNothingWithoutACameraManager",
    "TriggerTimedCameraAppliesItsOverrideOnEntry",
    "TheTimedCameraRoutineWaitsThenRemovesTheOverride",
    "TheTimedCameraRoutineLeavesAGoneCameraManagerAlone",
    "TriggerTimeScaleCreatesTheActiveTableOnceAndStartsItsRoutine",
    "TriggerTimeScaleEndClearsItsActiveFlag",
    "TheTimeScaleRoutineRunsWhileTheCurveHasTimeLeftAndTheFlagIsSet",
    "TheTimeScaleRoutineFinishesAtOnceForAnEmptyCurve",
    "OverrideGamePlayControlDisablesAndRestoresTheCurrentCharactersControls",
    "OverrideGamePlayControlUsesTheManagersCharacterNotTheUser",
    "TriggerFullscreenShaderKeepsAnExistingOverrideWithoutStartingAnother",
    "TriggerFullscreenShaderWithNeitherAHandleNorAManagerThrows",
    "TheFactoriesStoreTheNodeSettings",
    "TheBoolEventTriggerRunsTheEventMatchingTheStoredApplicationValue",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.MissionScorerEvaluatorTests." + name for name in (
    "TheLastBandWhoseDistanceIsStrictlyExceededScores",
    "TheSearchStopsAtTheFirstBandThatIsNotExceededEvenWhenALaterBandIsSmaller",
    "TheSelectedBandSuppliesTheAnimationAlongWithTheScore",
    "OnlyTheEnabledLocalAxesContributeToTheDistance",
    "AllThreeAxesAreEnabledByDefault",
    "TheOverrideTransformIsTheOriginAndItsRotationSetsTheLocalAxes",
    "ASingleBandAndANullEntryBothLeaveTheFirstBandSelected",
    "AnEmptyBandListFaultsReadingTheFirstBand",
    "TargetDistanceDefinitionExposesItsThreeAuthoredFields",
    "TheCurveIsReadAtTheRoundedUpSecondsAndMultipliesTheProximityScore",
    "TheTimeEvaluatorReturnsTheBandAnimationAndMultipliesTheSelectedBandScore",
    "EnteringTheProximityTargetScoresTheObjectiveAndCompletesIt",
    "EnteringTheSpeedCheckStoresTheScoreAndCompletesTheObjectiveEvenDuringItsCooldown",
    "TheInstanceExposesItsEvaluatorAndTheObjectiveItIsGiven",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.MissionInstancedTrackerTests." + name for name in (
    "InitialiseTakesTheTargetFromTheDefinitionAndPublishesItToTheState",
    "InitialiseAnnouncesTheLastObjectiveOnlyWhenOneMoreCompletesTheMission",
    "ObjectiveCompletionIsIgnoredUntilTheMissionStarts",
    "CompletingTheTargetNumberOfObjectivesCompletesTheMissionAndDetachesTheObjectives",
    "ARepeatedCompletionReportIsNotCountedAgainButStillReportsTheStatus",
    "ResettingACompletedObjectiveWithdrawsItFromTheCount",
    "TheIconIsTheFirstIncompleteObjectivesOrTheFirstObjectivesOnceAllAreComplete",
    "PersistedObjectiveCompletionIsRecordedInTheSaveAndNeedsNoStartedMission",
    "SavedObjectiveCompletionIsRestoredOnlyForAPersistentDefinitionThatIsNotATimeTrial",
    "ATrackerThatDoesNotPersistRestoresSavedObjectivesOnlyWhileTheSavedProgressIsZero",
    "AContextListingTheActiveObjectivesCountsThemAndRemovesTheOthers",
    "AStreakCompletedByTheObjectiveAddsItsScoreAsASecondBatch",
    "AReplayContextIgnoresSavedProgressAndSuppliesTheTarget",
    "ResettingTheMissionResetsItsObjectivesAndRequiresARestart",
    "ResettingACompleteMissionLeavesItsObjectivesComplete",
    "APreviouslyCompletedMissionMarksEveryObjectiveComplete",
    "ACompletedObjectiveAddsItsBonusSecondsToTheMissionTimerOnce",
    "AScoredObjectiveAddsItsPointsWithOrWithoutTheComboMultiplier",
    "DeclarationKeepsTheSerializedFieldsAndTheGateObjectivePrefix",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.MissionBossTrackerTests." + name for name in (
    "ReachingTheAuthoredNumberOfBossDefeatsCompletesTheMissionAndSoDoesEachLaterDefeat",
    "ANonPositiveCountCompletesOnTheFirstDefeat",
    "ResettingTheMissionRestartsTheDefeatCount",
    "ABossHardFailureFailsTheMissionOnlyWhenTheFailureBehaviourShowsResults",
    "TheTrackerJoinsTheBossCallbacksOnceAndLeavesThemWhenDestroyed",
    "RemovingRingsDeactivatesTheRingCollectablesOnSetupAndOnEachReset",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.MissionCollectableTrackerTests." + name for name in (
    "TheTrackedAmountGrowsByTheChangesAndAnIgnoredLossStillMovesTheBaseline",
    "ReachingTheTargetWithALargerGainClampsTheProgressToTheTarget",
    "LossesLowerTheTotalWhenNotIgnoredAndNeverBelowZero",
    "OnlyChangesFromTheAuthoredSourcesCountButEveryChangeMovesTheBaseline",
    "ChangesToOtherCollectableTypesAreIgnored",
    "SetupJoinsTheManagerAndRestartsTheProgressOnlyForAnIncompleteMission",
    "TheAmountAlreadyCollectedIsTheBaseline",
    "CompletionAndDestructionBothLeaveTheManager",
    "IgnoringLossesIsTheDefault",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.MissionReachGoalTrackerTests." + name for name in (
    "ReachingTheGoalCompletesTheMission",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.MissionInstancedOrbSpawningTrackerTests." + name for name in (
    "PartialCompletionCountsWithoutAStartedMissionOrAStatusCallback",
    "CompletingEveryObjectiveDetachesTheTrackerAndTriesToSpawnTheOrbAtTheLastObjective",
    "AnAuthoredOrbLocationReplacesTheLastObjectivesPositionAsTheSpawnPoint",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.MissionCollectableRewardTrackerTests." + name for name in (
    "StartRegistersTheRewardsAsOneGroupOnceAndTakesOverTheirRegistration",
    "StartDerivesTheStartDistanceFromTheUnionOfTheTriggerBounds",
    "CollectingEveryRewardCompletesTheMission",
    "DestroyingTheTrackerLeavesTheRewardsAndTheTriggers",
    "EnteringATriggerTwiceTracksItOnceAndLeavingItResetsTheMeasurements",
    "CompletingTheMissionStopsTrackingByEmptyingTheActiveTriggers",
    "TheMeasurementsFollowTheDistanceToTheFirstRewardAndTheCameraFacing",
    "CollectionInProgressFollowsTheFirstRewardsTriggeredFlagEachTick",
    "WithoutACurrentCharacterTheMeasurementsAreLeftAlone",
    "DeclarationKeepsTheAuthoredDefaults",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.MissionInstancedProgressUiTests." + name for name in (
    "TheParametersKeepEachArgumentInItsOwnProperty",
    "ASlotIsActivatedAndDeactivatedThroughItsBinding",
    "SettingTheSlotToItsCurrentStateWritesNothing",
    "OrderedSlotsLightThePreviouslyCompletedAndTheNewlyCompletedOnlyWhenNotCountingUp",
    "MissingIndexListsAreTreatedAsEmpty",
    "TheProgressTicksUpOneStepPerIntervalLightingTheNewlyCompletedSlots",
    "TheCompletionFlagComparesTheProgressWithTheTargetWhenTheCountEnds",
    "ACountAlreadyAtItsTargetDoesNotTick",
    "ANewlyCompletedIndexBeyondTheSlotsIsSkipped",
    "DisablingWithoutADefinitionDoesNothing",
    "SetupCountsUpOnlyForAnAnimatedIncreaseThatDidNotStartAtTheTarget",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.MissionScorerPrefabPoolTests." + name for name in (
    "TheFirstQualityEntryOfTheActiveLevelSuppliesTheDistanceAndRefreshRate",
    "ThePoolMayResizeOnlyWhenNeitherADistanceNorARefreshRateIsConfigured",
    "AnObjectiveIsInRangeBelowTheConfiguredDistanceOrAlwaysWithoutOne",
    "StartPoolsOnlyTheObjectivesOfTheConfiguredTypeAndRequestsTheRefresh",
    "TheRefreshWaitsTheConfiguredIntervalWhenThereIsNoCurrentCharacter",
    "ARefreshDespawnsIncompleteObjectivesOutOfRangeAndSkipsCompletedOnes",
    "ARefreshSpawnsAnInstanceForAnInRangeObjectiveAndBindsItsEvaluator",
    "ARefreshLeavesTheObjectiveInactiveWhenThePoolHasNoFreeInstance",
))

# Restored HardlightProject.ApplicationInfo and ApplicationInfoView (Game.Runtime 020000ad, 0200082c).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ApplicationInfoTests." + name for name in (
    "TheStampSerialisesItsCodeThenItsTime",
    "TheDefaultStampIsNaAtTheCurrentFileTime",
    "WithoutAFileTheDefaultReachesTheCallbackAtOnce",
    "TheViewStartsWithPlaceholders",
    "LoadedInformationFillsTheThreeBindables",
))

# Restored HardlightProject.ApplySafeArea (Game.Runtime 0200082e).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ApplySafeAreaTests." + name for name in (
    "ItAppliesOnAwakeByDefault",
    "ApplyOverwritesBothOffsetsFromTheSafeArea",
    "AwakeAppliesOnlyWhenTheFlagIsSet",
    "AHostWithoutARectTransformFaults",
))

# Restored HardlightProject.ButtonBinaryStateMessageListener (Game.Runtime 02000834).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ButtonBinaryStateMessageListenerTests." + name for name in (
    "TheTextStartsEmpty",
    "AwakeSubscribesTheStatusMessageForTheConfiguredButton",
    "AwakeSubscribesTheTextMessage",
    "NeitherHandlerCompletesTheMessage",
    "UnsetEventsAreSkipped",
    "OnDestroyUnsubscribesBothHandlers",
))

# Restored HardlightProject.OnControllerConnectedButton (Game.Runtime 020006b2).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnControllerConnectedButtonTests." + name for name in (
    "ConstructionSuppliesTheControllerConnectedInputAsADownPress",
    "EveryControllerTypeMapsToItsNativeInputType",
    "AConnectedControllerSetsTheInputTypeUnlessItIsASiriRemote",
    "ADisabledButtonDoesNothing",
    "TheLastControllerThatIsNotASiriRemoteDecidesTheInputType",
    "AnEmptyOrSiriRemoteOnlyProviderLeavesTheInputTypeAlone",
    'AnEnabledButtonFiresOnlyTheHandlerOfItsTrigger(0,"held")',
    'AnEnabledButtonFiresOnlyTheHandlerOfItsTrigger(1,"down")',
    'AnEnabledButtonFiresOnlyTheHandlerOfItsTrigger(2,"up")',
    "AnUnknownTriggerThrowsAndAMissingHandlerIsSkipped",
    "OnDestroyBeforeStartFaultsOnTheMissingControlMapping",
))

# Restored HardlightProject.OpenLocalisedURL (Game.Runtime 02000a15).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OpenLocalisedUrlTests." + name for name in (
    "TheQrCodePublishesTheUrlStringAndItsEventOnce",
))

# Restored HardlightProject.ScreenWidthHorizontalScrollGroup (Game.Runtime 0200083d).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ScreenWidthHorizontalScrollGroupTests." + name for name in (
    "ResetListsEveryChildInOrder",
    "ResetFaultsOnAChildWithoutARectTransform",
    "EnablingLaysTheChildrenOutOneCanvasWidthApart",
    "RefreshFollowsTheCurrentCanvasWidth",
    "ScrollingWrapsTheTargetIndexAroundTheChildren",
    "EachScrollStartsAtTheOldTargetsOffset",
    "ScrollingDoesNothingWithoutChildren",
    "ALargeNegativeOffsetLeavesANegativeTargetIndex",
    "TheLoopContinuesAtExactlyOneAndEndsAboveOneOrNaN(1.0f,True)",
    "TheLoopContinuesAtExactlyOneAndEndsAboveOneOrNaN(1.0f,False)",
    "TheLoopContinuesAtExactlyOneAndEndsAboveOneOrNaN(float.NaN,False)",
    "TheScrollHoldsTheStartWhileNoTimePasses",
    "TheScrollEndsOnTheExactTargetAndClearsTheRoutine",
))

# Restored HardlightProject.UIAnimatingCounter (Game.Runtime 02000842).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIAnimatingCounterTests." + name for name in (
    "ConstructionKeepsTwoSecondsAndEmptyBindings",
    "SetUpShowsTheStartAmountAtOnce",
    "OnlyANonZeroDurationReplacesTheAnimationTime",
    "TheStringIsRewrittenOnlyWhenTheValueChangesOrIsEmpty",
    "RoundingTiesGoToTheEvenNeighbour",
    "ProgressIsClampedBetweenTheStartAndEndAmounts",
    "AnInactiveCounterAnimatesLaterFromStartToEnd",
    "TheLoopRunsWhileTheElapsedTimeIsBelowTheDuration",
    "AnActiveCounterRunsTheStartOfItsAnimationAtOnce",
    "EnablingStartsTheDeferredAnimationOnly",
))

# Restored HardlightProject.UIContainerBossHealth (Game.Runtime 0200087b).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerBossHealthTests." + name for name in (
    "ConstructionStartsHiddenWithoutAHealthTotal",
    "SetupHidesThenShowsOnceTheManagerIsValid",
    "AValidManagerSizesAndFillsThePips",
    "WithoutABossTheContainerDeactivatesItself",
    "ANewTotalShowsThatManyFullPips",
    "AHealthChangeRecordsTheTargetAndStartsALerp",
    "LerpUpFillsCurrentMinusOnePipsFromTheOldTarget",
    "LerpUpFaultsWhenThePipsRunPastTheFills",
    "LerpDownEmptiesTheGivenFillAndEnds",
    "DestroyingUnsubscribesFromTheManager",
    "DestroyingAfterTheManagerWasDestroyedKeepsItsSubscriptions",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerValueSetterTests." + name for name in (
    "ASetterOnlyReachesALoadedContainer",
    "ValueProgressSetterForwardsTheFloatProgress",
    "SequenceProgressSetterForwardsTheEffectProgressOnlyToALoadedContainer",
    "ConditionalTriggerSetterEnablesAndDisablesTheContainerWithTheConditionCount",
    "ConditionalTriggerSetterNeverReportsFewerPassedConditionsThanBefore",
    "ConditionalTriggerSetterRestartsItsCountWhenEnabledAgain",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OrbDetectorUITests." + name for name in (
    "TheShownLevelsFollowTheWeightedProximity",
    "TheLevelWalkStopsAtTheFirstMinimumAboveTheProximity",
    "ANaNProximityShowsEveryLevel",
    "AnUnchangedProximityLeavesTheShownLevelsAlone",
    "CollectingAnOrbHidesEveryLevelAndStartsTheMusicRestore",
    "TheMusicRestoreEndsWhenTheElapsedTimeReachesTheLastKey",
    "ThePingTimerKeepsTheRemainderOfTheRepeatPeriod",
    "NoPingTimerRunsWithoutALevel",
    "IsCollectingOrbReadsTheIslandBindable",
    "TheMusicLowPassFollowsTheClampedProximity",
    "TheMusicRestoreSwitchesTheFilterOffAndAppliesTheLowerCutoff",
    "ASourceWithoutAFilterOrAnUnknownSourceRemembersNothing",
    "OnAudioManagerValidKeepsTheManager",
    "EnteringALevelPingsTheLastReachedLevelOnce",
    "ThePingNeedsAManagerASourceAClipAndNoOrbCollection",
    "EnablingResetsTheMusicEffectsAndHidesEveryLevel",
    "DisablingResetsTheMusicCutoff",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerUtilitiesTests." + name for name in (
    "WithoutASelectedLevelThereIsNoZoneAndTheBindersAreLeftAlone",
    "TheSelectedLevelAndItsZoneComeFromTheMetaGameState",
    "OnlyTheMissingLevelOrZoneIsTakenFromTheSelection",
    "TheGradientIsLayeredFromTheZoneThenTheLevelThenTheMission",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerHudBindingTests." + name for name in (
    "TheIslandNameIsTheFirstActiveIslandsKey",
    "AwakeSubscribesToTheIslandManagerAndOnDestroyUnsubscribes",
    "OnDestroyWithoutAnIslandManagerDoesNothing",
    "TheTailsChallengeBoardsAreNeverShownFromTheContainer",
    "SetLeaderboardStoresTheIdentifier",
    "TheLoadingImageIsTheLevelImageUnlessTheMissionOverridesIt",
    "SetupWithTheSameLevelByGuidChangesNothing",
    "OnDestroyWithNoDefinitionsDoesNothing",
    "ACounterWithoutARewardTypeDisablesItself",
    "TheCountTicksUpToTheTargetAfterTheInitialDelay",
    "ACountAtTheTargetWaitsOnlyTheInitialDelay",
    "ChangingTheRewardTypeReleasesThePreviousIcon",
    "OnDestroyReleasesNoIconWithoutADataManager",
    "DisablingTheRestrictionReleasesTheHeldIconOnce",
    "SetupShowsOnlyTheRequestedNotificationType",
    "SetupShowsAMatchingConfigurationBeforeRecordingTheRequest",
    "SetupTakesTheManagerBeforeItReadsTheParameters",
    "GameplayControlIsAppliedOnStartAndRestoredOnDestroy",
    "TheSkipActionsFlagTheCutsceneManager",
    "StartLabelsTheSkipInputOnlyDuringATheatreSession",
    "TheShownCountIsAheadOfTheSavedTotalOnlyForAnAnimatedPendingReward",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerHudDeclarationTests." + name for name in (
    "OrbDetectorUiDeclarations",
    "ValueSetterDeclarations",
    "GameplayContainerDeclarations",
    "NotificationRequestDeclarations",
    "OrbCollectableCounterDeclarations",
    "UtilitiesDeclarations",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerCloseEventTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "CloseEventIsPublishedOnceHoweverOftenTheContainerCloses",
    "AnUnsetOrDestroyedCloseEventPublishesNothingAndNeverAsksForTheApp",
    "SetCloseEventReplacesThePreviousEvent",
    "TheIntroStartsWithThreeIndependentEmptyBindables",
    "IntroCloseFirstPublishesTheCloseEventThenClosesItsOwnContainer",
    "IntroCloseWithoutACloseEventStillClosesItsOwnContainer",
    "IntroLeavesItsBindablesAloneWhenTheLevelManagerHasNoCurrentLevel",
    "IntroPublishesTheLevelNamesButNotAZoneIconThatWasNeverLoaded",
    "IntroStartQueuesItsCallbackUntilTheLevelManagerIsRegistered",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UISpawnerTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "StartTakesTheRegisteredUiManager",
    "SpawningOpensTheContainerOnlyOnce",
    "DestroyingClosesTheContainerOnlyWhileItIsOpen",
    "SpawningWithACloseEventHandsTheEventToTheOpenedContainer",
    "SpawningWithACloseEventLeavesAnOpenContainerAlone",
    "SpawningWithACloseEventOpensAContainerWithoutACloseEventSlotAndStopsThere",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIEventFromInputTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructorStartsWithNoIgnoredPlatformsAndTheRegistryAudioManagerReference",
    "EnablingBindsTheInputAndDisablingUnbindsIt",
    "AnyInputValuePublishesTheEventOnce",
    "AnIgnoredPlatformPublishesNothingAndNeedsNoApp",
    "ContainersGateThePublishOnlyWhenListedAndNotExclusivelyOpen",
    "AClipAndASourceWithAnAudioManagerPlayTheOneShot",
    "NoClipPlaysNothing",
    "NoSourcePlaysNothing",
    "NoAudioManagerPlaysNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIMenuNavigationActionsTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ContinueAndGoBackEachPublishTheirOwnEventInCallOrder",
    "AnUnsetEventIsPublishedAsNull",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIPointToTargetTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "TheSettersStoreTheirArguments",
    "ATargetStraightAboveGivesZeroDegrees",
    "ATargetToTheRightTurnsClockwise",
    "ATargetToTheLeftTurnsAnticlockwise",
    "ATargetStraightBelowGivesHalfATurn",
    "AnUpperRightTargetGivesFortyFiveDegreesClockwise",
    "ABehindCameraTargetStraightAboveFlipsTheDirection",
    "ABehindCameraTargetToTheRightFlipsTheDirection",
    "UpdateMeasuresFromTheWorldPositionAndResetsTheOtherAxes",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIReparentUtilityTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "TheChildIndexDefaultsToMinusOne",
    "AwakeTakesTheRectTransformOnlyOfAUiObject",
    "ZeroLevelsLeaveTheObjectUnderItsParent",
    "OneLevelMovesTheObjectUnderItsGrandparent",
    "TooManyLevelsStopAtTheRoot",
    "TheClimbKeepsTheWorldPositionOfTheObject",
    "TheChildIndexPlacesTheObjectAmongItsNewSiblings",
    "NoStretchKeepsTheResetOffsets",
    "StretchWidthZeroesOnlyTheHorizontalOffsets",
    "StretchHeightZeroesOnlyTheVerticalOffsets",
    "StretchBothZeroesEveryOffset",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UITargetingListenerTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructorBuildsTheBindablesAndTakesBothRegistryReferences",
    "EachMessageHandlerStoresItsMessageThenCompletes",
    "ACharacterChangeSubscribesTheThreeMessagesUnderTheNewCharacter",
    "ACharacterChangeClearsTheTargetingAndDetectorDataButNotTheBurstData",
    "ACharacterChangeWithdrawsTheSubscriptionsOfTheManagersCurrentCharacter",
    "StartFollowsTheCharacterManagerAndDestroyLetsGo",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UITextStringTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "TheConstructorStoresOnlyTheStringId",
    "ApplyFormatsTheLocalisedStringIntoTheLabel",
    "ApplyFaultsWithoutAnInitialisedStringOrWithoutArguments",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetAddressableSpriteTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructorBuildsTheBindableAndTakesTheRegistryManagerReference",
    "SettingAReferenceLoadsItsSpriteAndKeepsTheHandle",
    "ClearingTheReferenceKeepsTheLoadedSpriteAndANewReferenceReplacesIt",
    "ANewReferenceReleasesThePreviousHandle",
    "DestroyReleasesTheHandleOfTheLoadedSprite",
    "ANullReferenceNeedsNoAddressableManager",
    "TheLoadCallbackPublishesTheSprite",
    "DestroyDoesNothingWithoutAManagerOrAHandle",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetArchetypeImageSpawnerTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructorTakesTheRegistryDataManagerReference",
    "SpawningAnArchetypeShowsTheParentAndSetsUpAClone",
    "SpawningACharacterHandsOverItsDefinition",
    "SpawningAnArchetypeAndACharacterHandsOverBothDefinitions",
    "SpawningNothingHidesTheParentAndDropsThePreviousClone",
    "SpawningAgainReplacesTheClone",
    "AnUnknownArchetypeLeavesTheParentHiddenWithNothingSpawned",
    "AnUnknownCharacterThrowsAfterHidingTheParent",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetButtonImageCharacterTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "SetupShowsTheSwapIconWhenAsked",
    "SetupHidesTheSwapIconWhenNotAsked",
    "SetupWithoutASwapIconStillTakesTheCharacter",
    "SetupWithTheWrongParametersOnlyLogs",
    "StartLoadsTheAuthoredCharactersImage",
    "DestroyUnloadsTheCharactersImage",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetButtonImageTextureTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "SetupTakesTheAtlasedSpriteReference",
    "SetupWithTheWrongParametersOnlyLogs",
    "TheStartCallbackLoadsThePreloadedSpriteAndKeepsTheHandle",
    "StartWithoutAValidKeyNeverQueuesALoad",
    "ALoadedImageIsPublished",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetCategoryToggleTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ASelectedIncompleteTabShowsTheSelectedArt",
    "AnUnselectedCompleteTabShowsTheUnselectedArt",
    "AnEmptyCategoryCountsAsComplete",
    "SetupFlagsANewUnlockOnlyForAnAvailableUnseenOrnament",
    "HasAnyNewRescansOnlyForItsCategoryWhileTheFlagIsSet",
    "ChangingTheValueRunsTheBaseCallbackAndSwapsTheArt",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.MissionSelectDeclarationTests." + name for name in (
    "ActiveMissionDeclarationsMatchTheOriginalMetadata",
    "BonusZoneDeclarationsMatchTheOriginalMetadata",
    "SkipMissionIntroDeclarationsMatchTheOriginalMetadata",
    "LevelWidgetPairDeclarationsMatchTheOriginalMetadata",
    "SdtLevelDeclarationsMatchTheOriginalMetadata",
    "ToggleMissionDeclarationsMatchTheOriginalMetadata",
    "ToggleMissionParametersDeclarationsMatchTheOriginalMetadata",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetToggleMissionTests." + name for name in (
    "TheParametersKeepEachArgumentInItsOwnProperty",
    "OnlyTheCompleteFlagReachesItsProperty",
    "OnlyTheLockedFlagReachesItsProperty",
    "OnlyTheUnlockSeenFlagReachesItsProperty",
    "OnlyTheSubRewardsAnchorFlagReachesItsProperty",
    "SetupPublishesTheStatesOfAnUnlockedMission",
    "ALockedMissionHidesItsSubRewardStates",
    "AnUnlockedMissionThatIsNotSeenIsReadyToUnlock",
    "AnUnlockedMissionThatIsSeenIsNotReadyToUnlock",
    "ALockedMissionIsNotReadyToUnlock",
    "TheNoneStateDoesNotRaiseTheNewMarker",
    "TheSeenStateDoesNotRaiseTheNewMarker",
    "TheIsNewStateRaisesTheNewMarker",
    "SetIsNewAndSetRankIconVisibilityWriteTheirBindings",
    "TheBaseRankIconIsReplacedAndReleasedThroughItsOwnSlot",
    "TheBackgroundRankIconIsReplacedAndReleasedThroughItsOwnSlot",
    "TheForegroundRankIconIsReplacedAndReleasedThroughItsOwnSlot",
    "DisablingReleasesTheRankIconsAndTheRewardIcon",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetSdtLevelTests." + name for name in (
    "SetupPublishesTheNumberTheGroupIgnoredProgressAndTheDefaultMessage",
    "SetupPublishesTheActNameWhenNumericalNamesAreOff",
    "ALockedLevelHidesEveryRewardStateWithoutAskingTheMissionManager",
    "AnUnmetRewardRequirementShowsOneRestrictionWithTheRemainingCount",
    "RefreshingReusesTheRestrictionContainerAndUpdatesItsCount",
    "EachUnmetRequirementGetsItsOwnContainerAndMetOnesAreSkipped",
    "MetRequirementsShowNoRestriction",
    "AnAlwaysLockedLevelStillBuildsItsUnmetRestrictions",
    "AnAlwaysLockedLevelWithMetRequirementsShowsNoRestriction",
    "ThreeOfFourIsThreeQuarters",
    "OneOfThreeIsRoundedToSinglePrecision",
    "MoreCollectedThanTotalIsNotClamped",
    "NoneCollectedIsZero",
    "TheProgressionPercentOfAnUntrackedCollectableIsZeroAndOfAnEmptyOneNotANumber",
    "FourCollectedLeavesSixRemaining",
    "AnUntrackedCollectableLeavesTheFullCount",
    "OverCollectingLeavesANegativeRemainder",
    "TheCollectableDefinitionNeedsParametersWithAProgressAndARewardType",
    "DisablingReleasesTheRewardIconOnlyOnceItWasLoaded",
    "TheWidgetFollowsTheStringTableChangesBetweenStartAndDestroy",
    "RefreshHidesAHiddenLevel",
    "RefreshHidesAHiddenLevelThatIsAlsoLocked",
    "RefreshLocksALockedLevelWithoutHidingIt",
    "RefreshLeavesALevelWhoseUnlockWasSeenAlone",
    "RefreshLeavesAnAlwaysUnlockedLevelAlone",
    "OnlyABossLevelWithoutCompleteMissionsIsAHiddenBoss",
    "ANonBossLevelsRanksAreShown",
    "ABossLevelWithoutGroupsHidesItsRanks",
    "ABossLevelWithoutRankedMissionsHidesItsRanks",
    "ABossLevelWithARankedMissionShowsItsRanks",
    "BossRankLevelsSkipGroupsWhoseRequirementsFailAndKeepSearching",
    "SelectTurnsTheToggleOnAndNotifiesTheListenerOnce",
    "DeselectWithoutNotifyClearsTheToggleSilently",
    "NavigatingSelectsAnInteractableWidget",
    "NavigatingIgnoresANonInteractableWidget",
    "TurningNavigationOnEnablesAnUnlockedLevel",
    "TurningNavigationOnLeavesALockedLevelDisabled",
    "TurningNavigationOffDisablesAnUnlockedLevel",
    "TurningNavigationOffDisablesALockedLevel",
    "CompletingTheUnlockTurnsTheToggleOnOnce",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.MissionScreenContainerTests." + name for name in (
    "ALevelWidgetPairReturnsItsSerializedLevelAndWidget",
    "SkipMissionIntroTakesTheLevelManagerAndDoesNothingWithoutACurrentLevel",
    "ABonusZoneNumbersItsLevelsFromOneAndStartsWithoutAReveal",
    "OnlyAWidgetTurningOnChangesTheSelectedLevel",
    "RefreshingWithoutAMissionStateHidesTheMission",
    "RefreshingBeforeTheMissionManagerIsKnownHidesTheMissionButKeepsTheState",
    "AnIslandActivationRefreshesOnlyForADifferentState",
    "SettingTheRankAudioActiveRaisesItsFlagAndPlayingWithoutARankDoesNothing",
    "RankTimesAreNotSynchronisedWithBothOff",
    "RankTimesAreNotSynchronisedWithoutAShownRank",
    "RankTimesAreNotSynchronisedWithoutTheOption",
    "RankTimesReachTheMissionManagerWhenTheOptionAndTheShownRankAreBothOn",
    "AFailingMissionCountsDownFromItsLimitAndExtraTime",
    "TheCountdownIsEvaluatedInSinglePrecision",
    "AMissionThatDoesNotFailAtTheLimitKeepsTheTime",
    "AMissionThatIsNotATimeTrialShowsOnlyItsObjectiveIcon",
    "ABestTimeEqualToTheTheoreticalBestHidesTheRank",
    "ANaNBestTimeHidesTheRank",
    "ABestTimeAboveTheTheoreticalBestShowsTheRank",
    "TheHideOptionOffShowsTheRankWhateverTheBestTime",
    "ARankTimeEqualToTheElapsedTimeIsKept",
    "ARankTimeAfterTheElapsedTimeIsKept",
    "ARankTimeBeforeTheElapsedTimeIsLookedUpAgain",
    "ANaNRankTimeIsLookedUpAgain",
    "TheLowestRankIsNeverLookedUpAgain",
    "ABestTimeIsShownWhenEveryConditionHolds",
    "TheBestTimeIsHiddenWithTheOptionOff",
    "TheBestTimeIsHiddenForAMissionThatIsNotATimeTrial",
    "TheBestTimeIsHiddenWithoutAReachGoalTracker",
    "ABestTimeOfZeroIsNotShownButIsStillANewBest",
    "ANegativeBestTimeIsNotANewBest",
    "IconsAreKeptAndReleasedWhenTheContainerIsDestroyed",
    "StartTakesTheMissionManagerAndFollowsTheStringTable",
    "StartLeavesAForeignStringTableSubscriberSubscribedAndUncalled",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetCharacterHeadsSpawnerTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "AwakeTakesTheDataManagerReferenceAndTheListStartsEmpty",
    "HeadsAreSpawnedInCharacterOrderWithoutTheSwapIcon",
    "HeadsAreSpawnedInCharacterOrderWithTheSwapIcon",
    "AMatchingRequestKeepsTheExistingHeads",
    "ADifferentRequestRebuildsTheHeads",
    "ARequestForEveryCharacterShowsNoneWhenAsked",
    "ARequestForSomeCharactersShowsThemWhenNoneIfAllIsSet",
    "ARequestForEveryCharacterShowsAllWithoutTheFlag",
    "MoreIdsThanCharactersShowNoneWhenNoneIfAllIsSet",
    "AsManyIdsAsCharactersShowNoneEvenWhenMostAreUnknown",
    "AnEmptyRequestShowsNoneWithTheFlag",
    "AnEmptyRequestShowsNoneWithoutTheFlag",
    "UnknownIdsGetNoHead",
    "ARequestMatchesOnlyWhenEveryIdHasAHeadAndTheCountsAreEqual",
    "ClearingEmptiesTheListAndDestroysEveryHead",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetCrossPromoPageSdtTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructorBuildsThreeEmptyIndependentBindables",
    "AccessorsReadAndWriteTheirOwnBindables",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetCutsceneTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ALockedNewEntryIsClearedAndDisabled",
    "ALockedOldEntryIsDisabled",
    "AnUnlockedNewEntryIsMarkedNewAndEnabled",
    "AnUnlockedOldEntryIsEnabledButNotNew",
    "AnUnlockedEntryWithAValidKeyAsksTheAddressableManagerForTheImage",
    "AnUnlockedEntryWithAPreloadedImageShowsItAndKeepsTheHandle",
    "ALockedEntryDoesNotShowOrKeepAPreloadedImage",
    "ALockedEntryNeverLoadsItsImage",
    "TheSmallMembersWriteTheirBindables",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetImageArchetypeTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructorBuildsTwoEmptyIndependentBindables",
    "TheNormalImageTypeLoadsTheNormalIcon",
    "TheAlternateImageTypeLoadsTheAlternateIcon",
    "TheSilhouetteImageTypeLoadsTheSilhouetteIcon",
    "AnUndefinedImageTypeLoadsNoIcon",
    "AnArchetypeDefinitionWinsOverACharacterDefinition",
    "SetupWithNoDefinitionLoadsNothing",
    "DestroyUnloadsWhatSetupLoaded",
    "DestroyWithBothDefinitionsReleasesOnlyTheArchetypeIcon",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetSdtLoadingBarTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "NoProgressAnchorsAtTheCurveStart",
    "HalfProgressAnchorsAtTheCurveMiddle",
    "FullProgressAnchorsAtTheCurveEnd",
    "ProgressBeyondTheCurveClampsToItsEnd",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetSdtWaypointMarkerTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructorBuildsEightIndependentBindables",
    "AwakeTakesTheRectTransform",
    "SetupPublishesTheIconAndAVisibleDistanceIndicator",
    "SetupPublishesTheIconAndAHiddenDistanceIndicator",
    "AFrontTargetOnAnOverlayCanvasMovesTheMarkerToItsScreenPoint",
    "AFrontTargetOnACameraCanvasMovesTheMarkerToItsWorldPoint",
    "ABehindTargetKeepsItsNegativeDepth",
    "LockingTheYAxisKeepsTheMarkersHeightButPublishesTheScreenPoint",
    "APositiveSmoothTimeSmoothsTheMove",
    "AZeroSmoothTimeSnaps",
    "ANegativeSmoothTimeSnaps",
    "ANaNSmoothTimeSnaps",
    "TheDistanceIsTheLengthFromTheCharacterToTheTarget",
    "VisibleExactlyAtTheMinimum",
    "HiddenJustInsideTheMinimum",
    "VisibleExactlyAtTheMaximum",
    "HiddenJustBeyondTheMaximum",
    "ANegativeMaximumMeansNoLimit",
    "AZeroMaximumMeansNoLimit",
    "ANaNMaximumMeansNoLimit",
    "ANaNMinimumHidesTheMarker",
    "VisibleInsideTheWindow",
    "ATargetAboveShowsTheIndicatorPointingUp",
    "TheThresholdIsInclusive",
    "AHigherThresholdHidesTheIndicator",
    "ATargetBelowShowsTheIndicatorPointingDown",
    "AHiddenHeightIndicatorLeavesItsLastDirection",
    "CloseForgetsTheTargetAndResetsThePublishedState",
))

# Restored Game.Runtime on-screen controls: OnScreenInputButton, OnScreenBurstAttackButton, OnScreenCameraSnapButton,
# OnScreenControlDebugging, OnScreenControlGroupListener, UIContainerOnScreenControls, UserEditableUIBase,
# UserEditableUIComponentController and UserEditableUIFlipper.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenControlDeclarationTests." + name for name in (
    "OnScreenInputButtonKeepsItsInterfaceEventsAndIteratorName",
    "OnScreenBurstAttackButtonKeepsItsBindableFields",
    "OnScreenCameraSnapButtonKeepsItsFieldsAndHandlerOrder",
    "OnScreenControlDebuggingKeepsItsConstantsAndConditionalGui",
    "OnScreenControlGroupListenerKeepsItsSerializedFieldsAndVirtualHooks",
    "UIContainerOnScreenControlsKeepsItsFieldsAndClosureName",
    "UserEditableUIBaseKeepsItsFieldsAccessorsAndHooks",
    "UserEditableUIComponentControllerKeepsItsSerializedFieldRows",
    "UserEditableUIFlipperKeepsItsFieldsAndIteratorName",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenButtonTests." + name for name in (
    "EachRaiseCallsOnlyItsOwnEventWithJoystickZero",
    "RemovedHandlersAreNoLongerCalled",
    "SupplyCoroutineRaisesDownHeldAndUpOneFrameApart",
    "SupplySequenceStartsTheCoroutineOnTheButton",
    "BurstAttackMessageFlagsReachTheirOwnBindables",
    "BurstAttackBindablesAreThreeDistinctObjects",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenCameraSnapButtonTests." + name for name in (
    "EnablingListensToTheBrainAndTheEventDrivesTheButton",
    "DisablingStopsListeningToTheBrain",
    "DisablingWithoutACameraManagerDoesNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenControlDebuggingTests." + name for name in (
    "TouchPositionInfoListsEveryControlInOrderAndRestartsEachTime",
    "NoControlsGiveEmptyInfo",
    "ToggleFlipsTheDebugFlagEachTime",
    "OnGuiDoesNothingWhileTheDebugTextIsOff",
    "AwakeAndOnDestroyChangeNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenControlGroupListenerTests." + name for name in (
    "ShowAndHideRaiseTheirEventBeforeTheCompletion",
    "ToggleShowsForListedGroupsAndHidesForOthers",
    "AnEmptyGroupListAlwaysHides",
    "EnablingAppliesTheDefaultGroupWithoutAContainer",
    "EnablingWithAContainerThatHasNotAnnouncedUsesTheDefaultGroup",
    "ListenerFollowsTheContainersAnnouncementsUntilDestroyed",
    "WithoutAPublisherNothingIsSubscribed",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerOnScreenControlsTests." + name for name in (
    "AwakeAnnouncesTheDefaultGroupAndListensForCharacterChanges",
    "DestroyingStopsListeningToTheCharacterAndTheCharacterManager",
    "RequestsAreAnnouncedAndAnInAirRequestWaitsOutATeleport",
    "ChangingCharacterMovesTheRequestSubscription",
    "SettingsDriveTheStickSnapAndTertiaryButtons",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UserEditableUIBaseTests." + name for name in (
    "EachEditEventDrivesItsOwnHandler",
    "DestroyingStopsListeningToTheEditEvents",
    "DestroyingWithoutAnExchangeOnlyClearsTheInitialisedFlag",
    "UpdateWaitsForTheSettingsThenInitialises",
    "UpdateStaysOutOfEditModeWithoutTheStoredFlag",
    "InitialiseHandsDoInitialiseTheInvertedStoredFlag",
    "InitialiseBranchesOnTheLocalNotOnTheFieldDoInitialiseLeft",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UserEditableUIComponentControllerTests." + name for name in (
    "SaveManagerValidFindsTheInterfaceTheMenuCameraAndTheSavedComponent",
    "ScaleLimitsStartAtPointZeroFiveAndPointFive",
    "InitialiseKeepsThePrefabPlacementWithoutAListedDefault",
    "InitialiseUsesTheListedDefaultForItsSaveId",
    "InitialisePlacesTheSavedComponentAndRescalesIt",
    "ScaleAndPositionAreClamped",
    "BoundsAwayFromTheOriginOffsetTheNormalisedPosition",
    "AnOffCentrePivotOffsetsTheWorldPositionPerAxis",
    "ANearlyEqualScaleDoesNotRescaleTheComponent",
    "ADefaultPositionWithNoFreeSpaceIsZeroNotNaN",
    "UpdateFollowsTheTouchAndSavesThePlacement",
    "PinchScalesRelativeToTheScaleAtPinchBegin",
    "EditStartBacksUpTheSettingsAndCancelRestoresThem",
    "ALaterEditStartRefreshesTheSameBackup",
    "CancelPlacesTheComponentByTheRestoredUserSettings",
    "FlipMirrorsThePositionAndLaterMovesTheComponent",
    "UpdateAfterParentFlippedSavesWithoutMarkingAnUnflippedLayoutAsUserSet",
    "ResetRestoresTheDefaultPlacementAndClearsTheFlip",
    "PositionInfoNamesTheControlItsPositionAndScale",
    "ExitEditModeTellsTheComponentAndFiresTheEndEvent",
    "ValidatingChangesNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UserEditableUIFlipperTests." + name for name in (
    "InitialiseFlipAndResetEachStartTheDelayedUpdate",
    "ConfirmStoresTheFlipInTheSettingsBeforeTheyArePublished",
    "DelayedUpdateReversesTheLayoutAtTheEndOfTheFrame",
    "DelayedUpdateRePlacesTheActiveComponents",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OrnamentComponentTests." + name for name in (
    "EveryOrnamentResolvesTheCastShadowsPropertyIdWhenConstructed",
    "TheAccessorsReturnTheSerializedFields",
    "AwakeSwitchesTheThumbnailCameraObjectOff",
    "EnableAndDisableDriveTheCastShadowsKeywordAndStoreOneInTheFloatBothTimes",
    "EnableAndDisableWithoutARendererDoNothing",
    "ValidationFillsAnAbsentCameraAndAddsARotator",
    "ValidationReusesARotatorAlreadyOnTheObject",
    "ValidationKeepsAssignedReferencesAndAddsNothingThen",
    "ValidationTreatsADestroyedCameraAsAssignedAndADestroyedRotatorAsAbsent",
    "AdvancedValidationRunsTheBaseThenFindsOrAddsTheAnimatorSampler",
    "SimpleValidationRunsTheBaseThenFindsOrAddsTheAnimationSampler",
    "DeclarationsKeepTheOriginalMembersFlagsAndOrder",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OrnamentViewerTests." + name for name in (
    "TheCoroutineWaitsForTheRootThenInstantiatesTheOrnamentUnderIt",
    "ADestroyedRootKeepsTheCoroutineWaiting",
    "TheRotatorIsEnabledWithTheViewersYawSpeedAndAutomation",
    "TheYawOffsetIsDroppedOnlyForAPingPongingRotator",
    "ANullCallbackFaultsAfterTheOrnamentIsShown",
    "TheCoroutineCannotBeReset",
    "TheRootPublishesItselfOnTheExchangeUnderTheViewerMessage",
    "ReleasingRenderTexturesClearsEveryCamerasTarget",
    "DeclarationsKeepTheOriginalMembersFlagsAndGeneratedNames",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.MissionSequencedInstancedTrackerTests." + name for name in (
    "TheDefaultLimitActivatesOnlyTheFirstObjectiveOnStart",
    "StartActivatesTheFirstLimitObjectivesInOrderAndTheRestWait",
    "StartResetsEveryObjectiveWithoutReportingStatusBeforeActivating",
    "AStatusChangeBeforeStartIsIgnoredEntirely",
    "CompletingTheActiveObjectiveActivatesTheNextWaitingOne",
    "EachCompletionFreesOneSlotForTheFirstWaitingObjective",
    "ACompletedWaitingObjectiveIsSkippedWhenActivating",
    "TheBaseStatusChangeRunsAfterTheNextObjectiveIsActivated",
    "AResetObjectiveWaitsAndIsActivatedAgainInOrder",
    "APersistingTrackerHandlesReportsBeforeTheMissionStarts",
    "InitialiseEmptiesTheSetsUnlessTheMissionIsComplete",
    "DeclarationsKeepTheOriginalMembersFlagsAndAttributes",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerGameTipsTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructionCreatesTheBindableAndAnEmptyShuffledList",
    "OnEnableShufflesEveryTipOnceAndShowsTheFirst",
    "ShufflingAgainReplacesThePreviousOrder",
    "TheShuffleDrawsFromTheRandomState",
    "TipOverridesReplaceTheShuffledTips",
    "ASingleTipIsShownAgainOnEveryRefresh",
    "UpdateRefreshesOnceTheShowDurationIsReached",
    "UpdateKeepsTheTipWhileTheDurationIsLong",
    "ANaNDurationRefreshesEveryUpdate",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerCorruptionTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructionCreatesZeroedBindables",
    "CountsAreStoredWithoutTouchingTheProgressOrTheAnimation",
    "EnablingWithoutAProgressRootChangesNothing",
    "EnablingShowsTheMeterFromZeroAndPlaysTheIntro",
    "EnablingWithoutAnIntroClipStillShowsTheMeter",
    "ProgressOnAHiddenMeterEnablesItAndResetsTheProgress",
    "ProgressChangesWaitForTheIntroToFinish",
    "ZeroPlaysTheHitClipAndAnUnchangedValueDoesNothing",
    "NearlyEqualProgressIsTreatedAsUnchanged",
    "FullProgressPlaysTheOutro",
    "OtherProgressPlaysTheNormalClipOnlyWhenItIsNotCurrent",
    "PlayAnimationIgnoresAMissingClip",
    "TheDisablePredicateWaitsForTheClipToFinish",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerCollectableCounterTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructionStartsWithNoFormatOverrideAndTheRingRate",
    "TypeIsTheAuthoredCollectableType",
    "AnIncreaseIsShownAtOnceAndReportedAsPositive",
    "ADecreaseIsReportedAsNegativeWithItsSign",
    "AnUnchangedCountRaisesNoEvent",
    "MissingEventsAreSkipped",
    "AMissionStartCountsUpInsteadOfJumping",
    "AMissionStartReportsTheDropToZeroAgainstThePreviousCount",
    "RefreshInGameplayClearsOrShowsTheState",
    "ARunningCountIsRetargetedNotOverwritten",
    "TheCountAddsOnePerStepAndEndsAtTheTarget",
    "ASlowRingRateWaitsWithoutAddingAnything",
    "AZeroTargetEndsAtOnce",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerCharacterAbilityTests." + name for name in (
    "TheStaminaBasesMatchTheOriginalMetadata",
    "TheConcreteContainersMatchTheOriginalMetadata",
    "ConstructorsSetTheAuthoredDefaults",
    "SetupSubscribesTheCameraCallbackAndPublishesTheAbility",
    "AnAbilityOfAnotherTypeIsPublishedAsNull",
    "ChaosSetupLeavesTheAimAbilityNullForAnotherAbility",
    "AirShowsTheBarAboveTheCharacterWhileStaminaIsBelowFull",
    "AirHidesTheBarWhenStaminaIsFullAndTheRemainTimeIsZero",
    "AirKeepsTheBarDuringTheRemainTime",
    "AirDoesNothingForADestroyedCharacter",
    "ShownWithAnUpdateWritesTheStaminaValueAndRestartsTheTimer",
    "ShownWithoutAnUpdateKeepsTheStaminaValue",
    "ADrainingBarReadsFullWithTheTimeoutFractionRemaining",
    "ATimerAtTheRemainTimeHidesTheBar",
    "ResetTimerCopiesTheRemainTimeIntoTheTimer",
    "AnExpiredRechargeTimerClampsToZeroAndIsInactive",
    "ARunningRechargeTimerIsActive",
    "ADisabledStaminaUiHidesTheBarAtOnce",
    "AnActiveFullStaminaWritesItsValueAndCanTrigger",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerGameCenterAccessPointTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructionStartsWithHighlightsAndTheTwoWaits",
    "StartActivatesTheManagerAndLeavesThePlatformExtrasOffInTheEditor",
    "OnDestroyDeactivatesTheManager",
    "WithoutAManagerOnDestroyUpdateAndShowGameCenterDoNothing",
    "UpdateDoesNothingWhileTheAccessPointIsUnavailable",
    "AnUnavailableAccessPointClearsTheCachedVisibleState",
    "ADifferingCachedAvailableStateIsReplacedByTheManagersAnswer",
    "ACachedAvailableStateIsKeptOnceAvailableIsSet",
    "TheDelayedAppearanceWaitsThenShowsAndYieldsTheGlyphFade",
    "TheGlyphFadeStartsFromZeroAlphaAndWaitsForTheEndOfTheFrame",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerGameHUDTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructionCreatesFourFalseBindablesAndAnEmptyCharacterList",
    "TheObjectiveCallbacksSetAndClearTheCollectingFlag",
    "CharacterSelectIsInactiveWhileAnObjectiveIsCollected",
    "ChangingCharacterNeedsTheSelectionVisibleAndAllowed",
    "WithoutAMissionManagerTheCharacterSelectionIsShown",
    "ShowingCollectableViewsSubscribesToAValidManager",
    "ACollectableUpdateReachesOnlyTheCountersOfThatType",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerControllerRequiredTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "WithoutAnAppTheContinueButtonIsLeftAlone",
))

# Restored Game.Runtime on-screen touch controls: OnScreenButton, OnScreenControlGroupListenerButton,
# OnScreenControlInputHandler, OnScreenControlZone, OnScreenJoystickDebug, OnScreenTargetingButton,
# OnScreenTertiaryButton and OnScreenTouchZone.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenTouchControlDeclarationTests." + name for name in (
    "OnScreenButtonKeepsItsFieldsHandlersAndIteratorName",
    "OnScreenControlGroupListenerButtonKeepsItsFieldAndClosureName",
    "OnScreenTouchZoneKeepsItsFieldsAccessorsAndHooks",
    "OnScreenControlZoneKeepsItsAxisFieldsAndHooks",
    "OnScreenControlInputHandlerKeepsItsFieldsAndEmptyDebugMethods",
    "OnScreenJoystickDebugKeepsItsConditionalMembers",
    "OnScreenTargetingButtonKeepsItsBindableAndClosureName",
    "OnScreenTertiaryButtonKeepsItsFieldsAndMethodOrder",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenTouchZoneTests." + name for name in (
    "ProcessTouchSuppliesTheInputThenAPinchOnlyForADistanceAboveZero",
    "ReleasingSuppliesZeroMovement",
    "ScoreIsTheWorstValueUnlessThePositionIsInsideTheTouchArea",
    "ZoneIsActiveWhileItsObjectIsActiveInTheHierarchy",
    "NewTouchesAreTrackedByFingerAndOnlyWhileTheZoneIsAtItsPeak",
    "SingleTouchZonesTakeNewTouchesBelowTheirPeakToo",
    "UpdatingATouchReplacesOnlyATrackedFingersPosition",
    "ANewTouchNeedsAnActiveZoneAndMultiTouchOnceOneIsTracked",
    "TurningMultiTouchOffDropsEveryTouchButTurningItOnKeepsThem",
    "InitialiseAndShutdownClearTheTouchesAndShutdownReportsOnlyForAValidInputType",
    "ASingleTouchSuppliesItsPositionAndMovementThenReleasesOnce",
    "OneTouchOnAMultiTouchZoneSuppliesNoMovementAtFirstAndNoPinch",
    "ASingleTouchKeepsTheInitialPinchDistanceAndPreviousPositionOfEarlierTouches",
    "SeveralTouchesSupplyTheCentreOfTheirBoundsAndAPinchDistance",
    "ATightPinchIsRaisedToTheMinimumDistance",
    "ALostTouchHoldsTheLastPositionUntilEveryTouchIsGone",
    "ATouchIsIgnoredWhileAListedAbilityIsInUseOnTheCurrentCharacter",
    "TrackedTouchesSupplyNothingWhileAnIgnoredAbilityIsInUse",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenTouchZoneOrnamentTests." + name for name in (
    "AFreshZoneReportsNoDragAndNoImpulse",
    "TheDragAmountIsTheMovementAsAFractionOfTheWorldWidthOfTheTouchArea",
    "TheInputIsTheDragAmountOnTheXAxisOnly",
    "ReleasingKeepsTheLastMovementAsAnImpulseThatIsConsumedOnce",
    "ANegativeOrNaNImpulseIsClearedWhenConsumed",
    "TheZoneSuppliesNoGameInput",
    "InitialiseDropsTheTrackedTouches",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenTouchZoneEditableUITests." + name for name in (
    "AwakeStartsActiveOnlyWhenInitiallyActiveAndTheZoneNeedsTheObjectActiveToo",
    "SetActiveDecidesTheFlagOfAnActiveObject",
    "EditingSetsTheActiveFlagAndTurnsMultiTouchOnWithoutDroppingTouches",
    "TheRectTransformIsTheEditableComponent",
    "TheTouchPositionIsKeptAndReportedWithWhetherATouchIsTracked",
    "PinchInfoIsTheCurrentAndInitialDistanceOnlyWhileAPinchIsInProgress",
    "TheScoreIsZeroOnTheComponentOrWithATouchAndOtherwiseTheSquaredDistanceToItsCentre",
    "TheZoneSuppliesNoGameInput",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenControlZoneCameraTests." + name for name in (
    "TheConstructorSetsTheSensitivitiesTheDoubleTapTimeAndAnEnabledSnap",
    "InitialiseAndValidateCacheTheSquareOfTheCancelMagnitude",
    "TheCameraInputIsTheMovementScaledByTheSensitivityOverTheScreenSize",
    "ADefaultZoneNeverCountsATap",
    "ATapCountsTowardsADoubleTapAndMovementCancelsIt",
    "ADisabledSnapLeavesTheCountAndTheTotalAlone",
    "ADoubleTapStartsEveryListedInput",
    "TheTapCountExpiresOnceTheDoubleTapTimeHasPassed",
    "TheDoubleTapTimerAccumulatesTheUnscaledFrameTime",
    "TheTapCountExpiresWhenTheTimerEqualsTheDoubleTapTime",
    "ResettingTheSnapValuesClearsTheCountAndTheTimer",
    "TheDebugOverlayTogglesTheViewAndItsText",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenControlStickMovementTests." + name for name in (
    "TheConstructorSetsTheDefaultScaleToleranceAndSecondarySensitivities",
    "InitialiseShowsTheContainerScalesTheOuterGraphicsAndSetsTheOrigin",
    "TheStickFollowsTheSecondaryMovementOfTheCameraManagerUntilItIsDestroyed",
    "ASingleTouchStickDoesNotStartAsTheCameraControl",
    "TheDynamicFlagComesFromTheSaveDataAndAFlaggedStickMovesItsContainerToTheOrigin",
    "SetActiveShowsAndHidesTheContainer",
    "TheFirstTouchOfAPressPlacesTheOriginAtTheTouchForADynamicStickAndOnTheTransformForAFixedOne",
    "AFixedStickKeepsAnEarlierOriginWhileItsOriginTransformStaysPut",
    "AFixedStickTreatsAnOriginTransformMoveBelowTheVectorToleranceAsNoMove",
    "TheStickInputIsTheDirectionScaledByTheDistanceAndDrivesTheGraphics",
    "TheStateAndTheHapticsFollowTheMagnitudeAgainstTheToleranceAtBothEnds",
    "EnteringFineControlFromTheCentreDoesNotBuzzTheExitingEvent",
    "AMoveByAStickThatIsNotControllingTheCameraShowsTheContainerForTheLastInputType",
    "TheHeadingRotatesTheOuterAndTheStickTopBySignedAngle",
    "EverySetOriginBuzzesTheSelectEvent",
    "UpdatingTheTransformMeasuresTheDisplayAreaAgainAndCentresTheStick",
    "ReleasingRestsTheStickAndShowsTheContainerForTheLastInputType",
    "TheContainerIsShownForTheListedInputTypesOnly",
    "ACameraControllingStickSuppliesTheScaledMovementAndShowsForUnsupported",
    "ASingleTouchZoneThatControlsTheCameraTakesSeveralTouches",
    "EditingFollowsTheEnabledStateAndNotTheArgument",
    "TheTouchPositionIsTheLastTouchWhileATouchIsTracked",
    "PinchInfoIsReportedOnlyWhileAPinchIsInProgress",
    "TheRectTransformIsTheContainer",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenTouchZoneDerivedDeclarationTests." + name for name in (
    "OnScreenTouchZoneOrnamentKeepsItsFieldsAndHooks",
    "OnScreenTouchZoneEditableUIKeepsItsFieldsAndInterfaceMethods",
    "OnScreenControlZoneCameraKeepsItsFieldsHooksAndEntryClass",
    "OnScreenControlStickMovementKeepsItsFieldsStatesAndLambdaName",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenControlZoneTests." + name for name in (
    "TheFourAxesDefaultToLeftRightUpAndDown",
    "EachAxisHoldsTheInputOfItsSignWithTheValueOrItsMagnitude",
    "ThePressInputIsPressedWithTheFirstTouchAndReleasedAfterTheLastMovement",
    "InitialiseRegistersTheZoneAsAButtonProviderAndShutdownWithdrawsIt",
    "TheModifiersOfTheSavedSensitivityShapeTheMovementAndResetOnRelease",
    "ReleasingAZoneThatWasNeverTouchedResetsNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenUiButtonTests." + name for name in (
    "ResetTakesTheUiButtonOnTheSameObject",
    "AwakeHooksTheEventsOfTheTriggerAndDestroyingWithdrawsTheRegistration",
    "ExitingTheButtonStartsTheExitInputUnlessItIsNoneOrTheButtonIsNotInteractable",
    "DownAndUpSupplyEveryInputInOrderOnlyWhileTheButtonIsInteractable",
    "BeginningAHoldOnAnInactiveButtonPressesAndReleasesAtOnce",
    "DisablingAnIdleButtonCallsBackAtOnce",
    "TheGraphicIsShownForTheListedInputTypesOnly",
    "ADisabledHeldButtonCallsBackOnlyWhenItsHoldHasBeenReleased",
    "AHeldButtonHoldsItsInputsEachPassUntilItsObjectIsDeactivated",
    "ReleasingTheUiButtonEndsTheHoldOnTheNextPass",
    "DestroyingAHeldButtonReleasesItsInputs",
    "HidingTheListenerWaitsForAHeldButtonToBeReleased",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenControlInputHandlerTests." + name for name in (
    "EnablingFollowsTheLastInputTypeAndDisablingShutsTheZonesDownWithIt",
    "ZonesAreShutDownAsNotValidWhenTheLastInputTypeIsNotListed",
    "UpdateDoesNothingWithoutAnInputMonitor",
    "UpdateWaitsForATouchSourceThenClearsAndSuppliesTheActiveZones",
    "TouchesGoToTheLowestScoringZoneAndFollowTheirPhaseAfterTheFirstUpdate",
    "AZoneWithANotANumberScoreIsKeptOverALaterZone",
    "TheConstructorSeedsTheTouchListAndTheMouseFlag",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenJoystickDebugTests." + name for name in (
    "EnablingListensToTheCharacterInfoToggleAndDisablingStops",
    "TheDeadZoneImagesAreEnabledOnlyForTouchInputWhileTheCharacterInfoIsShowing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenTargetingButtonTests." + name for name in (
    "EnablingTurnsTargetingOff",
    "TargetingIsActiveWhenAnyAbilityOfAValidTargetIsInTheValidGroup",
    "EachButtonHasItsOwnTargetingBindable",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OnScreenTertiaryButtonTests." + name for name in (
    "ACharacterWithOneAbilityShowsItsIconAndOneWithBothShowsTheIconOfTheGroup",
    "TheIconsFollowTheAnnouncedGroupOnlyWhenItChanges",
    "WithoutAnAnnouncementTheGroupIsNone",
    "ANullCharacterLeavesTheIconsAsTheyWere",
    "AwakeListensForCharacterChangesAndDestroyingStops",
    "ValidatingChangesNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetSliderPipTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "SetupPublishesTheColourOfTheParameters",
    "EachPipHasItsOwnColourBinding",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetTogglePaginationTests." + name for name in (
    "TheWidgetDeclarationsMatchTheOriginalMetadata",
    "TheParametersDeclarationsMatchTheOriginalMetadata",
    "TheParametersKeepTheCallbackAnEmptyLabelAndEachFlag",
    "OnlyTheIsNewFlagReachesItsProperty",
    "OnlyTheLockedFlagReachesItsProperty",
    "OnlyTheUnlockSeenFlagReachesItsProperty",
    "OnlyTheIsVisibleFlagReachesItsProperty",
    "ConstructionBuildsTwoIndependentBindables",
    "TheToggleGroupElementIsTheAuthoredOne",
    "ANewPageThatIsNotSelectedIsMarkedNew",
    "ANewPageThatIsAlreadySelectedIsNotMarkedNew",
    "AnOldPageIsNotMarkedNewAndStaysHiddenWhenItIsNotVisible",
    "AnOldSelectedPageIsNotMarkedNew",
    "TheNewMarkerFollowsTheToggleOnceSetUp",
    "APageThatIsNotNewNeverShowsTheMarker",
    "SelectingRaisesTheSelectedEventAndDeselectingDoesNot",
    "AMissingSelectedEventIsSkipped",
    "RefreshWithoutParametersChangesNothing",
    "ALockedPageCannotBeInteractedWith",
    "AnUnlockedPageWhoseUnlockWasSeenIsLeftAlone",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerCutsceneTheatreTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "StartTakesTheManagersAndSetsUpEveryPageButton",
    "TheFirstPageHoldsTheFirstTwoValidCutscenes",
    "TheSecondPageHoldsTheNextTwo",
    "TheLastPageHoldsTheRemainder",
    "APagePastTheEndIsEmpty",
    "SelectingThePageSetsUpItsWidgetsFromTheSave",
    "ACutsceneSeenOnALevelIsUnlocked",
    "APlayedCutsceneIsNotNew",
    "AShortPageLeavesTheOtherWidgetsAsTheyWere",
    "DeselectingAPageChangesNothing",
    "LoadingAPageSetsItUp",
    "PressingACutsceneWhileTheTheatreIsOpenQueuesItAndMarksItPlayed",
    "PressingACutsceneWhileAnotherContainerIsOpenDoesNothing",
    "PlayAllQueuesTheSeenCutscenesWhileTheTheatreIsOpen",
    "PlayAllDoesNothingWhileAnotherContainerIsOpen",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerDivotCollectableCounterTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "NoCollectedItemFillsNoSlotButTheCounterUpdates",
    "OneCollectedItemFillsOneSlot",
    "TwoCollectedItemsFillTwoSlots",
    "ACollectedCountEqualToTheSlotCountSkipsTheWholeUpdate",
    "ACollectedCountAboveTheSlotCountSkipsTheWholeUpdate",
    "AFilledSlotIsNeverEmptiedByALaterUpdate",
    "WithoutSlotsTheCounterIsNotUpdated",
    "TheMetadataReachesTheBaseUpdate",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerLoadingTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "WithoutParametersTheDefaultImageIsShown",
    "WithoutALevelTheDefaultImageIsShownAndTheTipsAreLeftAlone",
    "ALevelWithoutAnImageKeyPublishesItsNullImage",
    "TheImageCallbackPublishesTheSprite",
    "AMissionWithoutAnOverrideLeavesTheLevelImageInCharge",
    "AMissionOverrideImageIsShownInsteadOfTheLevelImage",
    "TheMissionTipsAreHandedToTheTipsContainer",
    "TheTipsOfAnOverridingMissionAreStillHandedOver",
    "WithoutTipsInTheMissionOrAContainerNothingIsShuffled",
    "AnEmptyTipListIsNotShuffled",
    "DisablingUnloadsTheImagesOfTheRememberedDefinitions",
    "DisablingBeforeAnySetupUnloadsNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerLoadingSimpleTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructionNamesTheLoadingProgressMessage",
    "ProgressMessagesMoveTheBar",
    "AnotherMessageIsNotDelivered",
    "TheStartedEventFiresOnlyWithTheFirstProgress",
    "ARunWithoutAStartedEventStillCountsAsStarted",
    "DestroyingTheContainerStopsTheProgressUpdates",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerWaypointMarkersBaseTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructionTakesTheSystemReferencesAndAnEmptyMarkerList",
    "AwakeSubscribesToTheManagerRefreshesAndTakesTheRectTransform",
    "EachTargetWithADefinitionGetsAnActiveMarkerSetUpForIt",
    "ATargetWithoutADefinitionGetsNoMarker",
    "AnUndefinedTargetKeepsItsIndexSoEachRefreshAddsAnotherMarker",
    "RegisteringAndRemovingTargetsKeepsTheMarkersInStep",
    "DestroyingTheContainerStopsTheRefreshes",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerWaypointMarkersCompassTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructionBuildsTwoIndependentArrowBindables",
    "InFrontJustLeftOfTheStripIsLeft",
    "InFrontOnTheLeftEdgeIsInside",
    "InFrontAtTheCentreIsInside",
    "InFrontOnTheRightEdgeIsInside",
    "InFrontJustRightOfTheStripIsRight",
    "BehindJustRightOfTheCentreIsLeft",
    "BehindAtTheCentreIsRight",
    "BehindJustLeftOfTheCentreIsRight",
    "BehindFarRightIsLeft",
    "BehindFarLeftIsRight",
    "ALeftMarkerGoesToTheLeftEdgeInsetByThePadding",
    "ARightMarkerGoesToTheRightEdgeInsetByThePadding",
    "AMarkerWithNoDirectionIsLeftWhereItIs",
    "TheArrowsFollowTheMarkersOutsideTheStripOnTheSecondPass",
    "TheArrowsGoOutAgainWhenNoMarkerIsOutsideTheStrip",
    "AMarkerBehindTheCameraLightsTheMirroredArrow",
    "WithClampingOnMarkersOutsideTheStripAreMovedOntoItsEdges",
    "OnlyAMarkerOutsideTheStripIsSmoothed",
    "AnInactiveMarkerIsNotUpdated",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerWaypointMarkersScreenSpaceTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructionStartsWithNoListedInputTypes",
    "TheEllipseFollowsWhetherTheInputTypeIsListed",
    "EnablingAppliesTheLastInputTypeAndFollowsLaterChangesUntilDisabled",
    "APointOnTheXAxisMapsToTheEllipseEnd",
    "APointOnTheNegativeXAxisMapsToTheOtherEnd",
    "APointOnTheYAxisMapsToTheEllipseTop",
    "ADiagonalPointMapsToTheUnitCircleDiagonalScaledBack",
    "ThePointAtZeroMapsToZero",
    "TheDepthJoinsTheLengthAndIsDividedByIt",
    "AMarkerFarToTheRightIsCutToTheRightEnd",
    "AMarkerFarAboveIsCutToTheTop",
    "AMarkerOutsideTheEllipseButInsideItsBoxIsCutToTheEdgeInItsDirection",
    "AMarkerNearTheCentreOnTheXAxisStaysPut",
    "AMarkerInsideTheEllipseOffTheAxesStaysPut",
    "AMarkerJustInsideTheEdgeIsNotCut",
    "AMarkerWhoseSettingsHideTheDistanceKeepsItHiddenInsideTheEllipse",
    "ABehindMarkerToTheRightGoesToTheLeftEnd",
    "ABehindMarkerAboveGoesToTheBottom",
    "ABehindMarkerNearTheCentreIsStillPutOnTheEdge",
    "ABehindMarkerExactlyAtTheCentreStaysAtTheCentre",
    "EveryActiveVisibleMarkerIsClampedInTheSamePass",
    "WithoutASmoothTimeAMarkerSnapsToItsTarget",
    "WithASmoothTimeAMarkerHasNotArrived",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.ViewTargetIndicatorTests." + name for name in (
    "TheParametersMatchTheOriginalMetadata",
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructionStartsWithNoTrackedTypes",
    "AwakeHidesTheTargetAndTakesTheImageTransform",
    "ResetTakesTheCanvasOfTheSameObject",
    "ThePositionIsSetInCanvasUnits",
    "TheScaleAndColourBelongToTheImage",
    "ShowTargetTogglesTheTargetParentAndTheIndicatorProjectsToScreenSpace",
    "ADepthBelowTheMinimumIsOutOfRange",
    "ADepthOnTheMinimumIsInRange",
    "ADepthBetweenTheLimitsIsInRange",
    "ADepthOnTheMaximumIsInRange",
    "ADepthAboveTheMaximumIsOutOfRange",
    "ADepthThatIsNotANumberIsInRange",
    "SetupTakesTheTargetAndReplacesTheSpriteOnlyWhenOneIsGiven",
    "SetupWithPlainTargetParametersLeavesTheSpriteAlone",
    "ASurfaceDetectionOfAListedTypePinsTheSurfacePosition",
    "ADetectionThatDoesNotMatchClearsTheTarget",
    "AMessageThatCannotActivateOrIsNotListedClearsTheTarget",
    "AnUntrackedTypeIsPinnedAtTheMessagePosition",
    "ATrackedTypeWithoutHomingMetadataFollowsTheObjectTransform",
    "ATrackedTypeWithHomingMetadataFollowsTheTargetsCollider",
    "ATrackedMessageLeavesTheOtherFollowedReferenceInPlace",
    "AnUntrackedMessageDropsTheFollowedTransform",
    "AMatchingDetectionDropsTheFollowedTransform",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.XPCounterBufferedIncreaseTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "TheNestedEffectThresholdsMatchTheOriginalMetadata",
    "TheIteratorStateMachineMatchesTheOriginalMetadata",
    "AGainFiresTheLastReachedThresholdAndCountsUpFromTheLastSeenValue",
    "AGainJustBelowTheThresholdFiresNothing",
    "AGainEqualToTheThresholdFiresIt",
    "AGainJustAboveTheThresholdFiresIt",
    "ThePassOverTheThresholdsStopsAtTheFirstOneAboveTheGain",
    "AReachedThresholdWithoutAnEventStillSetsTheCountUpTime",
    "WithoutThresholdsAGainStillCountsUp",
    "NoGainShowsTheCurrentValueWithoutACountUp",
    "SpentExperienceShowsTheCurrentValueWithoutACountUp",
    "ThePassWorksWithoutACounterToAnimate",
    "EnablingOutsideTheOverviewTriggersAtOnce",
    "EnablingOnTheOverviewWithoutBonusExperienceTriggersAtOnce",
    "EnablingOnTheOverviewWithBonusExperienceHoldsTheLastSeenValue",
    "TheWaitStepsUntilTheBonusIsAcknowledgedAndThenTriggers",
    "ADisabledComponentWithNoWaitingCoroutineStopsNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.DebugCollectablesContainerTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ARowPublishesItsParametersAndKeepsBothActions",
    "ARowWithoutActionsIgnoresBothButtons",
    "UpdatingTheCollectedAmountLeavesTheRestAlone",
    "TheListChangesCollectablesByFiveByDefault",
    "StartListsOneRowPerRegisteredCollectable",
    "TheRowActionsAddAndRemoveTheChangeValueOfTheirOwnType",
    "TheAuthoredChangeValueSetsTheStep",
    "ARowShowsTheCappedCountWhenTheDefinitionHasATotal",
    "ChangingATypeWithoutAStateLeavesTheRowAlone",
    "ClosingAsksTheUiManagerToCloseTheIdentifier",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.FastLevelSelectAndFastLoadContainerTests." + name for name in (
    "TheLevelSelectDeclarationsMatchTheOriginalMetadata",
    "TheFastLoadDeclarationsMatchTheOriginalMetadata",
    "StartListsTheLevelsShownInLevelSelect",
    "ShowingAZoneStillListsEveryLevelOfTheGame",
    "PressingALevelButtonFastLoadsThatLevelAndQueuesTheLoadGameEvent",
    "ShowingACategoryAddsABackButtonAndPushesIt",
    "TheBackButtonShowsThePreviousList",
    "TheBackButtonOnTheFirstListShowsItAgainAndOnAnEmptyStackDoesNothing",
    "ASelectionThatIsNotALevelOrACategoryIsIgnored",
    "ConstructionTakesTheAppAndMissionManagerReferences",
    "TheLevelComesFromTheAppsLevelLoadParameters",
    "StartAddsAButtonPerMissionOfTheLoadingLevel",
    "AMissionlessLevelContinuesAtOnceWithTheFirstCharacterAndTheBonusZone",
    "ALevelOutsideTheZoneFiveCategoryLeavesTheBonusZoneUnset",
    "PressingAMissionButtonContinuesWithThatMission",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.DebugInformationViewTests." + name for name in (
    "TheEnemyViewDeclarationsMatchTheOriginalMetadata",
    "EnablingTakesTheAuthoredEnemyBeforeTheBaseBinds",
    "TheEnemyUpdateWithoutAnActorLeavesTheTextsAlone",
    "TheEnemyUpdatePublishesOnlyTheStateTextFromTheUnclearedBuilder",
    "TheFrameViewDeclarationsMatchTheOriginalMetadata",
    "TheAverageIsOverTheQueuedSamples",
    "TheWindowKeepsTheNewestSixtySamples",
    "TheFrameUpdatePublishesAllThreeTextsInTheirFormats",
    "TheTrackViewDeclarationsMatchTheOriginalMetadata",
    "TheTrackViewBindsTheNextInfoInputAndShowsOnlyTheFirstPanel",
    "TheTrackViewNextInfoCyclesThroughThePanels",
    "TheTrackUpdateWithoutATrackManagerLeavesTheTextAlone",
    "TheTrackUpdateClearsTheBuilderAndPublishesTheManagersPoolInfo",
    "TheTrackUpdatePublishesWhatTheManagersVirtualPoolInfoAppends",
    "TheCharacterViewDeclarationsMatchTheOriginalMetadata",
    "TheCharacterViewStartsWithFourTextsAndNoCharacter",
    "EnablingRegistersForCharacterChangesAndTellsTheDebugMenu",
    "EnablingWithAValidCharacterTakesItAtOnce",
    "ACharacterChangeStoresTheCharacterTwice",
    "DisablingReleasesTheCallbackAndTellsTheDebugMenu",
    "TheCharacterUpdateWithoutAnActorLeavesTheTextsAlone",
    "TheFrameCounterDeclarationsMatchTheOriginalMetadata",
    "TheFrameCounterPublishesTheStaticFrameRateEachUpdate",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIChallengeTrackRewardStatusTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructionStoresTheRewardAndTheTwoFlagsInTheirOwnFields",
    "ALockedUncollectedRewardIsNotReady",
    "ALockedCollectedRewardIsNotReady",
    "AnUnlockedUncollectedRewardIsReady",
    "AnUnlockedCollectedRewardIsNotReady",
    "SavingSavesTheRewardAndThenMarksItCollected",
    "ASaveThatFaultsLeavesTheRewardUncollected",
    "AnalyticsEventsPassTheTrackTypeAndTheRewardNumberInOrder",
    "TheMetaGameUnlockIsTheRewardsUnlockForTheStatusTrack",
    "ARewardlessStatusFaultsOnEveryReadOfTheReward",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerChallengeTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructionSetsTheXpFormatDefaultsAndCreatesTheBindables",
    "SetupLocalisesTheLabelsAndFollowsTheChallenge",
    "AScoreUpdateOfTheShownChallengeRefreshesTheBindables",
    "SettingUpAgainUnfollowsThePreviousChallenge",
    "AForeignParametersObjectFaultsAfterTheLabelsAreSet",
    "AnUpdateForAnotherChallengeIsIgnored",
    "TheBindablesReadTheChallengeStateAndItsDefinitions",
    "APositiveBestTimeIsCompleted",
    "AZeroBestTimeIsNotCompleted",
    "ANegativeBestTimeIsNotCompleted",
    "ANaNBestTimeIsNotCompleted",
    "ATinyPositiveBestTimeIsCompleted",
    "TheXpIsPlainWithBothOptionsOff",
    "TheXpUsesItsFormatWhenItsOptionIsOn",
    "TheRankOptionDoesNotFormatTheXp",
    "TheRankXpUsesTheRankFormatWhenItsOptionIsOn",
    "TheXpOptionDoesNotFormatTheRankXp",
    "TheFormattedXpBindableUsesTheXpFormatWhenEnabled",
    "NoCharactersGiveNoArchetype",
    "OneCharacterGivesItsArchetype",
    "CharactersOfOneArchetypeGiveIt",
    "AMismatchAtTheSecondCharacterKeepsTheFirstArchetype",
    "AMismatchReportsTheFirstCharactersArchetype",
    "ALateMismatchIsStillFound",
    "AnUnknownCharacterThrows",
    "TheCharactersAreShownAsOneArchetypeOrAsTheirOwnHeads",
    "NoCharactersShowNeitherAnArchetypeNorHeads",
    "WithoutAHeadsSpawnerTheArchetypeSpawnerIsLeftAlone",
    "ANeverPlayedChallengeOrOneWithoutRanksShowsNoRank",
    "AScoreAboveTheSThresholdEarnsTheSRank",
    "AScoreAtTheSThresholdEarnsTheSRank",
    "AScoreJustBelowTheSThresholdEarnsTheARank",
    "AScoreAtTheAThresholdEarnsTheARank",
    "AScoreBelowEveryThresholdEarnsNoRank",
    "DestroyingUnloadsTheLoadedIconsAndUnfollowsTheChallenge",
    "EveryUpdateKeepsAnotherLoadOfEachIcon",
    "DestroyingWithoutAChallengeDoesNothingElse",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerChallengeBonusTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "TheParametersKeepTheirThreeValuesApart",
    "ConstructionCreatesTheBindablesAndTheFormatDefault",
    "SetupPublishesTheValuesAndTheTwoFormattedTexts",
    "ForeignAndMissingParametersFaultBeforeAnythingIsPublished",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerChallengeBonusAwardedTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructionCreatesTheBindablesAndTheFormatDefault",
    "TheValidManagerSuppliesTheBonusAndTheTotalsBeforeAndAfter",
    "EnablingTakesTheRegisteredManager",
    "DestroyingAcknowledgesTheBonusAndForgetsTheManager",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerChallengeLeaderboardTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "TheParametersKeepTheirTwoValuesApart",
    "AwakeLocalisesTheScoreLabel",
    "SetupPublishesTheScoreAndMakesTheButtonFollowTheAvailability",
    "SetupWithoutAButtonOnlyPublishesTheValues",
    "ForeignAndMissingParametersFaultBeforeAnythingIsPublished",
    "PressingTheButtonShowsTheLeaderboardThroughTheChallengeManager",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerChallengeXPRewardTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructionSetsTheTickAndTakesTheManagerReferences",
    "TheImageSettersAndTheMidpointSignalStoreTheirValues",
    "AWholeNumberIsShownAsIs",
    "ANumberBelowHalfRoundsDown",
    "ANumberAboveHalfRoundsUp",
    "ZeroIsShown",
    "WithoutASliderTheSliderSetupDoesNothing",
    "TheSliderIsSetUpWithTheSpentAndTotalExperience",
    "NoRewardHidesTheRewardAndResetsTheSlider",
    "TheNextRewardIsShownAndItsImagesLoadedIntoTheBindables",
    "DestroyingUnloadsTheCachedRewardImage",
    "StartRefreshesTheLockedTrack",
    "ALockedTrackShowsNoRewardAndAResetSlider",
    "AnUnlockedTrackStartsTheUnlockAnimationAndTouchesNothingElse",
    "AnUnknownStateLogsAnErrorAndShowsNoReward",
    "ASeenTrackShowsItsProgressAndTheCollectableFlag",
    "ACollectableRewardIsShownBeforeTheNextUnreachedOne",
    "TheNextRewardIsMeasuredFromThePreviousOne",
    "AProgressOneShortOfTheRangeIsNotReached",
    "AProgressEqualToTheRangeIsReached",
    "AProgressAboveTheRangeIsReached",
    "WithoutAnyRewardLeftTheDisplayIsCleared",
    "TheLabelAnimationNeedsAnAnimation",
    "TheLabelCountsUpToTheTargetAndThenStopsTheAnimation",
    "ATargetOfZeroCountsNothing",
    "ANegativeTargetCountsNothing",
    "TheLabelWaitsForARunningAnimation",
    "WithoutAnAnimationTheTrackIsMarkedSeenAtOnce",
    "TheUnlockAnimationWaitsForTheMidpointSignal",
    "AnAnimationThatEndsReleasesTheUnlockAnimation",
    "AMidpointSignalGivenBeforehandSkipsTheWait",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetSDTToggleChallengeTests." + name for name in (
    "TheWidgetDeclarationsMatchTheOriginalMetadata",
    "TheParametersDeclarationsMatchTheOriginalMetadata",
    "TheParametersKeepTheLabelTheCallbackAndEachFlag",
    "OnlyTheIsNewFlagReachesItsProperty",
    "OnlyTheIsCompleteFlagReachesItsProperty",
    "OnlyTheIsBonusFlagReachesItsProperty",
    "OnlyTheIsShadowFlagReachesItsProperty",
    "TheOptionalFlagsAreFalseWhenLeftOut",
    "ConstructionBuildsFourIndependentBindables",
    "TheGroupElementIsTheToggles",
    "SetupPublishesTheLabelAndEachMarker",
    "ASelectedToggleReportsItselfSelectedThroughTheCallback",
    "TheMarkersArePublishedBeforeTheCallbackRuns",
    "AToggleThatIsNotSelectedDoesNotCallBack",
    "ASelectedToggleWithoutACallbackSetsUpQuietly",
    "ParametersOfAnotherClassFailTheCastAfterTheToggleSetup",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerChallengesOverviewTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "TheClosureClassesMatchTheOriginalMetadata",
    "ConstructionCreatesTheBindablesTheListAndTheNavigationReference",
    "StartHidesThePanelsLocalisesTheLabelAndClearsTheShadowFtueFlag",
    "TheContainerFollowsTheManagerUntilItIsDestroyed",
    "EachChallengeGetsANumberedToggleAndTheFirstUnfinishedOneIsSelected",
    "TheBonusToggleIsLastAndCompleteOnlyWhenEveryChallengeIs",
    "WhenEveryChallengeIsCompleteTheBonusToggleIsSelected",
    "WithoutChallengesTheBonusIsCompleteAndSelected",
    "AnUpdateClearsTheSelectedChallengeAndHidesThePanels",
    "MoreChallengesGrowTheListAndKeepTheBonusToggle",
    "TheShadowFtueShowsOneShadowToggle",
    "SelectingTheBonusSwapsThePanelsAndDeselectingHidesOnlyTheBonusPanel",
    "ANormalChallengeIsValidWhileTheManagerListsIt",
    "WhileTheShadowFtueIsToBeShownOnlyItsChallengeIsValid",
    "SelectingAndDeselectingAChallengeShowsAndHidesItsPanels",
    "SelectingAnInvalidChallengeClearsTheSelectionAndLeavesThePanels",
    "SelectingNoChallengeIsNotValidatedAndStillShowsTheInfoPanel",
    "ThePeriodBonusIsAnnouncedOnceAndTheLatchResetsWithNothingUnacknowledged",
    "WithoutTestModeThePanelIsHiddenAndTheDebugLabelStaysEmpty",
    "WithoutANavigationManagerNothingIsSelected",
    "LaunchDataWithoutASelectionOnlyClearsTheValidFlag",
    "LaunchDataForAnInvalidSelectionDropsTheSelection",
    "LaunchDataHandsTheChallengeToTheApplicationAndClearingRemovesIt",
    "ShowingTheInfoPopupQueuesTheInfoEventOnce",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerChallengeRewardTrackTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "TheClosureClassesMatchTheOriginalMetadata",
    "ConstructionSetsTheDefaultsAndCreatesTheBindablesAndTheList",
    "OneXpShortOfTheThresholdIsLocked",
    "ExactlyTheThresholdIsUnlocked",
    "PastTheThresholdIsUnlocked",
    "ACollectedRewardIsTakenFromItsSaveRecord",
    "NothingSpentSitsAtTheStart",
    "HalfWayToTheFirstReward",
    "AtTheFirstRewardTheSliderSitsOnIt",
    "AQuarterOfTheWayToTheSecondReward",
    "HalfWayToTheSecondReward",
    "AtTheLastRewardTheDivisionStaysFinite",
    "PastTheLastRewardTheSliderRunsOnByTheExcess",
    "WithoutExperienceOnlyMoreXpNeededShows",
    "EnoughExperienceOffersTheAddButton",
    "AnUncollectedRewardShowsTheCollectButton",
    "WhenEveryRewardIsCollectedOnlyAllCollectedShows",
    "WhileTheSliderAnimatesTheButtonsAreDisabledAndTheCollectButtonIsHidden",
    "WhileARewardUnlocksTheCollectButtonShowsEvenWithoutACollectableReward",
    "WhileARewardUnlocksWithSpendableXpTheAddButtonStaysHidden",
    "WhileTheSliderAnimatesAfterTheLastRewardTheAddButtonShowsAndAllCollectedDoesNot",
    "TheUnlockingDelayEndsTheUnlockingAndRefreshesTheBinders",
    "TheUnlockingDelayEndsTheUnlockingAndLeavesTheBindersWhenSkipped",
    "EachRewardGetsAButtonPlacedAlongTheTrack",
    "ARewardWithoutAPrefabOfItsSizeGetsNoButton",
    "WithoutPrefabsNoButtonAndNoFocusWaitAreMade",
    "ClickingAReadyRewardCollectsIt",
    "ClickingWithTheBindersSkippedLeavesThemAlone",
    "ClickingWhileTheSliderAnimatesDoesNothing",
    "ClickingWhileARewardUnlocksDoesNothing",
    "TheFirstReadyRewardIsCollectedAndNoneReadyAnswersFalse",
    "WithNoReadyRewardNothingIsCollected",
    "ActionCollectRewardCollectsTheFirstReadyRewardAndRefreshesTheBinders",
    "SpendingDoesNothingWhileBusyOrWithoutEnoughExperience",
    "ScrollingWithoutAShaderModifierDoesNothing",
    "DestroyingLeavesNoUnlockingCoroutine",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetButtonChallengeRewardTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "TheParametersDeclarationsMatchTheOriginalMetadata",
    "TheParametersKeepTheirValuesApart",
    "ConstructionSetsTheOffsetAndCreatesTheBindables",
    "ANextRewardIsLabelledNextWhateverItsStatus",
    "ANextCollectableRewardIsStillLabelledNext",
    "AnUnlockedUncollectedRewardInvitesCollecting",
    "ALockedRewardHasNoLabel",
    "ACollectedRewardHasNoLabel",
    "RefreshPublishesTheRewardTheXpTextTheInteractableStateAndTheGlow",
    "RefreshStoresTheNewStatusInTheParameters",
    "ACollectedRewardPlaysTheCollectedAnimation",
    "AnUncollectedUnlockedRewardPlaysTheReadyAnimation",
    "ALockedRewardPlaysTheHiddenAnimation",
    "ALockedRewardMarkedCollectedStillPlaysTheHiddenAnimation",
    "CollectingPicksTheClipOfTheRewardType",
    "ASetupButtonIsPlacedAtItsPositionLessHalfItsWidthPlusTheOffset",
    "TheLastButtonGetsNoOffset",
    "TheAuthoredOffsetAndTheWidthDecidePosition",
    "SetupWithForeignParametersFaults",
    "ClicksAndHighlightsReachTheParametersCallbacks",
    "TheSettersAndPropertiesReadAndWriteTheirOwnMembers",
    "DestroyingUnloadsTheRewardImage",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetChallengesSliderTests." + name for name in (
    "TheSliderDeclarationsMatchTheOriginalMetadata",
    "TheMiniSliderDeclarationsMatchTheOriginalMetadata",
    "TheConstructorsSetTheAnimationDurations",
    "TheSliderSetupSetsTheMaximumTheValueAndTheWidth",
    "TheMiniSliderSetupSetsTheMaximumAndTheValue",
    "ForeignParametersFaultInBothSetups",
    "TheAnchoredPositionIsTheWidgetsOwn",
    "ATargetEqualToTheValueFinishesAtOnce",
    "ATargetBelowTheValueFinishesAtOnce",
    "ACompletionWithoutACallbackIsFine",
    "ATargetAboveTheValueWaitsWithoutCompleting",
    "SettingAValueFinishesThePreviousAnimationsCallbackAndStoresTheNewOne",
    "SettingAValueWithoutAPreviousCallbackJustStoresTheNewOne",
    "APositiveAnimationTimeStartsFromEmptyAndWaits",
    "AZeroAnimationTimeEndsAfterEmptyingTheBar",
    "ANegativeAnimationTimeEndsAfterEmptyingTheBar",
    "ANaNAnimationTimeEndsAfterEmptyingTheBar",
    "TheLargeSliderEasesTowardTheTargetWithASmoothstep",
    "TheLargeSliderEasingWeightIsClampedAtOne",
    "TheMiniAnimationLerpsTheCurrentValueTowardTheParametersValue",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerWhatsNewTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "StartFetchesTheAppAndPlaysTheOpenClip",
    "StartPlaysNothingWithoutAnAnimationOrAnOpenClip",
    "ClosingPlaysTheCloseClipWhenTheScreenHasAnAnimation",
    "ClosingPublishesTheCloseEventWhenTheOpenClipOrTheAnimationIsMissing",
    "TheCloseFinishAndTheCloseHelperPublishTheCloseEventOnce",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerProgressionUnlockTests." + name for name in (
    "TheSimpleContainersMatchTheOriginalMetadata",
    "TheOrnamentAndRewardTrackContainersMatchTheOriginalMetadata",
    "BlueCoinsShowTheAmountWithAPlusSign",
    "BlueCoinsWithoutTheirUnlockPublishNothing",
    "BlueCoinsReleaseTheCoinIconWhenDestroyed",
    "ACharacterSkinShowsItsOwnNameAndTheUnlockBodyAndThumbnail",
    "ADreamPowerShowsItsNameDescriptionAndIcon",
    "ABandShowsTheFixedTitleItsBodyItsTierAndItsImage",
    "AMusicTrackReleasesItsImageWhenDestroyed",
    "AMusicTrackWithoutItsUnlockDoesNothing",
    "TheOrnamentCategorySelectsTheCategoryTextAndTheCategorySprite",
    "TheNextButtonIsShownWithoutAnUnlockAndWhenTheAnimationCompletes",
    "AnOrnamentIsRemovedFromThePendingUnlocksAndItsViewerDestroyed",
    "ARewardTrackShowsItsTextsIconThumbnailAndColours",
    "ARewardTrackWithoutItsUnlockDoesNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerProgressionTaskTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "WithoutAPendingUnlockNothingIsPublished",
    "AnUnlockWithoutTaskDetailsOnlyShowsTheNextButtonAndTheDefaultGlow",
    "BlueCoinsShowTheCoinIconAndTheChallengeDetails",
    "ACharacterSkinShowsItsThumbnailAndTheChallengeDetails",
    "ABandShowsItsImageAndTheChallengeDetails",
    "ARewardTrackShowsItsIconAndTheDetailsOfTheNamedTrack",
    "AMusicTrackShowsTheSpriteTextureWhenThereIsNoTextureAsset",
    "AMusicTrackShowsItsTextureAssetWhenTheReferenceIsValid",
    "ADreamPowerShowsItsNameIconAndTheDreamPowerSprite",
    "AMissionOrnamentShowsTheMissionNameActZoneAndSprite",
    "AnAchievementOrnamentShowsTheAchievementTitleAndSprite",
    "AnOrnamentWithOnlyChallengeExperienceShowsTheChallengeDetails",
    "DestroyingAContainerWithoutAnUnlockFaults",
    "TheRevealCompletionDoesNothing",
    "TheShownAnimationPublishesTheRevealEventOnce",
    "PressingTheButtonHidesItAndPublishesTheRevealEventWhenThereIsNoAnimation",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerPlayerProgressionTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "TheCharacterImageFollowsTheCompletedMissionsOfTheFirstLevel",
    "AnEmptyQueueExitsAtOnceAndAnimatingOutWithoutAWidgetExitsAgain",
    "OrnamentsBeyondTheCapAreSavedWithoutBeingShown",
    "UnlocksWithoutAWidgetArePassedOverUnsaved",
    "ASecondMissionGroupIsSkippedButASecondLevelIsShown",
    "ASetupStartsWithAnEmptySetOfShownTypes",
    "AMissionGroupAfterAGroupWithoutAWidgetIsStillShown",
    "UnlocksAreShownOneAtATimeAndSavedWhenContinued",
    "ANullTextureLeavesTheMaskedImageAlone",
    "AnimatingToTheMenuSavesTheShownUnlockDropsTheRestAndPicksTheMenuEvent",
    "AnimatingToTheMenuWithoutAShownWidgetDoesNotSave",
))

# Restored Hardlight.UIWidgetButtonDreamPowerSlot, UIWidgetImageDreamPowerSlot and HardlightProject.UIWidgetDreamPowerDescription.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.DreamPowerSlotWidgetTests." + name for name in (
    "EquippingASlotShowsTheIconAndPlaysTheEquipClip",
    "UnequippingASlotClearsTheIconAndPlaysTheUnequipClip",
    "AnEmptySlotIsValidForEquipOnlyForABoughtPower",
    "AnEquippedSlotIsNeverValidForEquipAndTakesTheUnequippedClip",
    "SettingInteractableQueuesTheStateWritesTheButtonAndTheBaseFlag",
    "ASlotWithoutATransitionStillWritesTheButtonAndTheBaseFlag",
    "SetInteractableOnlyWritesAChange",
    "TheSlotPublishesItsEventUnderOnClick",
    "TheImageSlotEquipShowsTheIconAndPlaysTheEquipClip",
    "TheImageSlotUnequipClearsTheIconAndPlaysTheUnequipClip",
    "TheImageSlotEnableAndDisablePlayTheirOwnClips",
    "ThePowerDescriptionShowsTheNameAndIconAndReleasesThemOnDestroy",
    "ThePowerDescriptionWithoutAPowerOnlySetsTheEmptyFlag",
    "ThePowerDescriptionDestroyedBeforeSetupReleasesNothing",
))

# Restored HardlightProject.UIDreamPowerSlots and its Slot record.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIDreamPowerSlotsTests." + name for name in (
    "EquippingABoughtPowerFillsTheSlotAndTheSavedLoadout",
    "APowerThatIsNotBoughtIsNotEquipped",
    "ARestrictedArchetypeThatDoesNotListThePowerRefusesIt",
    "EquippingAPowerClearsItFromEveryOtherSlot",
    "ADifferentObjectWithTheSameGuidIsTheSamePower",
    "ToggleEmptiesAnEquippedSlotAndFillsAnEmptyOne",
    "ToggleOfAnEmptySlotWithAPowerThatIsNotBoughtChangesNothing",
    "UnequipEmptiesTheSlotAndRemovesItsSavedEntry",
    "ConstructionBuildsOneUnequippedRecordPerWidget",
))

# Restored HardlightProject.UIWidgetSDTSkinToggle and its parameters.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetSDTSkinToggleTests." + name for name in (
    "SetupJoinsTheGroupShowsTheThumbnailAndRetainsIt",
    "LockedAndNewFollowTheSavedFlagsAndTheDefaultFlag",
    "TheTransitionShowsSelectedThenDisabledThenNormal",
    "EverySelectionEntryPointAppliesTheSkinAndRefreshesTheBinders",
    "AWidgetThatWasNeverSetUpIgnoresSelectionAndDestroy",
    "DestroyReleasesTheThumbnail",
    "TheParametersKeepTheirArguments",
))

# Restored HardlightProject.UIContainerCharacterSelect.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerCharacterSelectTests." + name for name in (
    "OnlyTheMissionsCharactersAreShown",
    "ChoosingTheCurrentCharacterOnlyPublishesTheContinueEventOnce",
    "SelectingADefinitionChoosesItsCharacter",
    "DestroyingTheScreenGivesGameplayControlBack",
    "StartTakesGameplayControlShowsTheCharactersAndSelectsTheCurrentCharactersWidget",
    "StartSelectsNoWidgetWhileNavigationIsInactive",
    "StartSelectsNoWidgetWithoutACurrentCharacter",
))

# Restored HardlightProject.UIContainerCharacterSelectMain.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerCharacterSelectMainTests." + name for name in (
    "AwakeDisablesContinuingAndSelectsTheFirstAllowedCharacter",
    "TheRememberedCharacterIsSelectedWhenTheMissionAllowsIt",
    "NoUsableMemoryFallsBackToTheFirstAllowedCharacter",
    "DestroyingTheScreenRemovesItsInputSubscription",
    "ChoosingACharacterMovesItToTheFrontOfTheRememberedList",
    "ChoosingACharacterStoresItAndEnablesContinuing",
    "ARegularContextIsNotChallengeMode",
    "AChallengeContextMakesTheScreenChallengeMode",
    "AnUnmetSkinRequirementTurnsTheSkinEntryOff",
    "ChoosingACharacterRefreshesTheEquippedPowersForItsArchetype",
    "OpeningSkinSelectionHandsOverTheCurrentCharacterAndPublishesTheEvent",
    "ControllerInputSelectsTheFirstMissionCharacterOnlyWhenNothingIsSelected",
    "ControllerInputSelectsTheToggleOfTheFirstListedCharacterNotTheFirstToggle",
    "OnlyANonDefaultCurrentSkinCounts",
))

# Restored HardlightProject.UIContainerCharacterSelectEquipDreamPower.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerCharacterSelectEquipDreamPowerTests." + name for name in (
    "StartShowsTheArchetypeAndTheSavedLoadoutOfADreamPowerMission",
    "AnUnrestrictedMissionShowsNoArchetypeAndKeepsPowersEnabled",
    "AMissionWithoutDreamPowersHidesTheLoadout",
    "RefreshingEnablesOrDisablesEachRecordedSlotByTheArchetype",
    "TheContainerAsksTheStoreWhichArchetypeMayUseAPower",
    "ApowerIsEquippedWhenAnyArchetypesLoadoutHoldsASlot",
    "DestroyReleasesTheArchetypeImage",
))

# Restored HardlightProject.UIContainerCharacterSkinSelection and its parameters.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerCharacterSkinSelectionTests." + name for name in (
    "AnUnlockedSkinIsWornAndShown",
    "ALockedSkinIsShownButNotWorn",
    "ASkinUnlockedByDefaultIsWornWithoutASavedUnlock",
    "ChangingSkinReleasesThePreviousThumbnailAndRepeatingDoesNothing",
    "ASkinTheCharacterAlreadyWearsRaisesNoEventAndPlaysNoAnimation",
    "ADifferentObjectWithTheShownSkinsGuidIsTheShownSkin",
    "UnloadAndDestroyReleaseTheShownThumbnail",
    "SetupTakesTheCharacterThenFaultsWithoutANavigationManagerBeforeMakingAToggle",
    "SetupMakesOneSkinToggleForEachListedSkin",
    "SetupSwitchesOnTheCurrentSkinsToggleSelectsItInTheNavigationAndShowsTheSkin",
    "SetupDoesNotSelectTheCurrentSkinsWidgetWhileNavigationIsInactive",
    "TheParametersKeepTheirCharacterDefinition",
))

# Restored HardlightProject.UIWidgetActiveCharacterImage.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetActiveCharacterImageTests." + name for name in (
    "EnablingShowsTheCurrentCharactersPortrait",
    "ANewCharacterReleasesTheOldPortraitAndShowsItsOwn",
    "AnUnlistedCharacterKeepsTheSpriteAndForgetsTheDefinition",
    "DisablingRemovesTheCallbackAndReleasesThePortrait",
    "UnloadImageReleasesTheHeldPortraitOnly",
    "StartDoesNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OrnamentGalleryWidgetTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "TheComposerParametersKeepTheRowDataList",
    "TheSeekerForwardsAChangedValueToItsEventOnly",
    "ThePointerEventsReportTheSliderValueToTheirOwnEvents",
    "AbsentEventsAreSkippedWithoutReadingTheSlider",
    "AnAvailableOrnamentShowsItsUnlockedArt",
    "AnUnavailableOrnamentShowsItsLockedArt",
    "AnUnlockedButUncollectedOrnamentStillShowsTheLockedArt",
    "AnAvailableUnseenOrnamentShowsTheNewMarker",
    "ASeenOrnamentHidesTheNewMarker",
    "AnUnavailableUnseenOrnamentHidesTheNewMarker",
    "AnUncollectedUnseenOrnamentHidesTheNewMarker",
    "SetupAssignsTheToggleGroup",
    "SelectingReportsTheOrnamentLocksTheToggleAndPlaysTheSound",
    "DeselectingRestoresTheToggleSilently",
    "ADefaultSelectedToggleSelectsItselfWithoutTheSound",
    "AToggleReusedForAnotherOrnamentIsReset",
    "AToggleReusedForTheSameDefinitionKeepsItsSelection",
    "AToggleReusedForADefinitionWithTheSameGuidKeepsItsSelection",
    "SelectingTheWidgetReportsItsTransformToTheHoverCallback",
    "DestroyingTheToggleUnloadsTheThumbnail",
    "ARowHoldsOneSetUpToggleForEachEntry",
    "ARowSetUpAgainReusesItsTogglesAndGrows",
    "ARowSetUpWithFewerEntriesDestroysTheLastToggles",
    "SelectFirstDoesNothingWithoutANavigationManager",
    "TheComposerSpawnsAndSetsUpOneRowPerRowOfData",
    "ClearingTheComposerDestroysEachChildOnce",
    "StartPublishesTheRenderTheNameAndAValidCompanion",
    "StartLeavesTheCompanionAloneWithoutAValidKey",
    "DestroyingTheWidgetUnloadsBothRenders",
    "SelectingTheCharacterRaisesTheEventAndSchedulesTheHighlight",
    "SelectingWithoutAnEventStillSchedulesTheHighlight",
    "TheHighlightMovesUnderItsContainerKeepingItsWorldPosition",
    "TheHighlightStaysPutWithoutAContainer",
    "AMissingHighlightIsLeftAlone",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerOrnamentGalleryTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "TheConstructorSetsTheAuthoredDefaults",
    "EnablingTakesTheServicesAndShowsTheAchievementCategory",
    "TheRowLengthAndTheOffsetFollowTheScreenShape",
    "ChoosingTheStoryTabListsItsThreeOrnamentsInTwoRows",
    "ChoosingTheChallengeTabListsItsOneOrnament",
    "ChoosingTheAchieverTabListsItsOneOrnament",
    "TheStoryOrnamentsFillTheRowsInOrder",
    "TheStoryTabSwitchedOffChangesNothing",
    "TheChallengeTabSwitchedOffChangesNothing",
    "TheAchieverTabSwitchedOffChangesNothing",
    "ChoosingATabForgetsTheCurrentOrnament",
    "AnUncollectedOrnamentEndsUpShownLocked",
    "TheTabCountsOnlyAvailableOrnaments",
    "SelectingANewAvailableOrnamentMarksItSeen",
    "SelectingASeenOrnamentChangesNothing",
    "SelectingANewUnavailableOrnamentChangesNothing",
    "SelectingASeenUnavailableOrnamentChangesNothing",
    "ASeenOrnamentWithoutARewardRecordStaysCollected",
    "ASeenOrnamentWhoseRewardIsNotCollectedYetIsNotCollected",
    "ASeenOrnamentWhoseRewardIsCollectedStaysCollected",
    "AnUnlockedOrnamentShowsItsNameAndLore",
    "ALockedStoryOrnamentShowsTheActTip",
    "ALockedChallengeOrnamentWithoutAPickerShowsThePlainTip",
    "ALockedAchievementOrnamentShowsThePlainTip",
    "TheInfoButtonTogglesThePanelOnlyWhileItIsInteractive",
    "StartConnectsTheInfoButtonToThePanelToggle",
    "AnOrnamentIsPlacedTurnedByItsYOffsetAndHiddenAtZeroScale",
    "APingPongingOrnamentIsNotTurnedByItsYOffset",
    "ANewOrnamentWaitsForTheOldOnesScaleDownThenReplacesIt",
    "ScalingUpEndsAtFullSize",
    "ScalingDownEndsAtZero",
    "AScaleUpStepAppliesTheCurveAtTheStart",
    "AScaleDownStepStartsFromThePresentScale",
    "AScaleDownReadsTheCurveAtTheInvertedProgress",
    "WaitingForAHandleYieldsItThenShowsItsPrefab",
    "ButtonRotationBecomesTheRotatorsImpulseOnce",
    "ANegativeButtonRotationIsNotAnImpulse",
    "LateUpdateWithoutAnOrnamentDoesNothing",
    "ReleasingTheFingerAppliesTheReleaseImpulseToTheRotator",
    "DraggingTurnsTheOrnamentDirectlyByTheDragAmount",
    "ASpinningOrnamentAboveTheThresholdIsNotDragged",
    "AnImpulseEqualToTheThresholdIsNotDragged",
    "AnImpulseBelowTheThresholdIsDragged",
    "ADragIgnoresASmallPingPongExtent",
    "ADragIgnoresALargePingPongExtent",
    "DestroyingTheGalleryDestroysTheOrnamentAndRequestsASave",
    "TheCountChangedEventAddsAndRemovesHandlers",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerMissionProgressTests." + name for name in (
    "TheParametersKeepTheirInstancedTracker",
    "DeclarationKeepsTheAuthoredDefaults",
    "TheGettersReadTheirSerializedFlags",
    "EnablingHidesTheGroupUnlessTheContainerIsAlwaysDisplayed",
    "TheProgressIconReplacesTheTextAndTheObjectiveIconOnlyWhileTheObjectivesAreIncomplete",
    "HideWaitsOutTheDisplayTimeThenFadesTheGroupAndDeactivatesTheContainer",
    "AZeroDisplayTimeStartsTheFadeOnTheFirstStep",
    "TheTrackersIconIsShownWhenItHasOne",
    "TheInstancedTrackerBackingFieldSitsBetweenTheSerializedFlagAndTheCoroutine",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerMissionBaseTests." + name for name in (
    "WithoutAnActiveContextTheNameIsTheFormattedMissionName",
    "OnlyTheActiveMissionTakesTheContextsDescription",
    "TheStringTableHandlerIsHeldOnceAndAppliesTheStateAgain",
    "ParametersWithoutAMissionStateWriteNothing",
    "TheActiveContextIsMatchedByGuidNotByReference",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerMissionFailureTests." + name for name in (
    "TheStoredMessageReplacesTheDefaultOnlyWhenItIsPresentAndNotZero",
    "ExitClearsTheMessageDropsTheMissionQueuesTheEventAndShowsNoFailureScreen",
    "ExitAndRestartRunOnlyOnceBetweenThem",
    "RestartClearsTheMessageAndLeavesTheExitEventAndFlagAlone",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerMissionRanksTests." + name for name in (
    "TheRankTimesCountDownFromTheTotalTimeWhenTheMissionFailsOnTheTimeLimit",
    "EachRankFillsItsOwnTextAndIconsWhichTheContainerReleasesWhenDestroyed",
    "ARankOfNoDisplayedTypeFillsNothing",
    "AChallengeShowsItsScoresWhateverTheMission",
    "WithoutRanksToShowTheContainerHidesThem",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerMissionResultsTests." + name for name in (
    "TheParametersKeepTheMissionAndTheLevel",
    "AMissionIsValidOnlyWhenEveryConditionHolds",
    "TheValidSubMissionsComeBeforeTheMissionItself",
    "SetupBuildsAProgressContainerForEachValidMissionAndOnDestroyClosesTheirIdentifiers",
    "TheShortContainerIsUsedWhenAskedAndWithoutCachedDataThePreviousProgressIsZero",
    "WithoutAValidMissionNothingIsBuiltAndShowIsFalse",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerMissionScorerTests." + name for name in (
    "DeclarationBuildsTheScoreTextsAndDelaysShowingPoints",
    "EnablingHidesTheRootAndDelaysShowingPointsAgain",
    "ClearValuesSetsTheRootsActiveState",
    "ShowPointsFollowsTheScorersTwoTimersOnceTheDelayIsOver",
    "NothingIsInitialisedWithoutASetUpActiveMission",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerMissionMusicPlayerTests." + name for name in (
    "EnablingUnlocksTheControlsAndKeepsTheBackgroundSource",
    "WithoutTheBackgroundSourceEnablingDoesNothing",
    "SeekingMutesTheSourceUntilTheSeekFinishes",
    "TurningRepeatOffClearsTheLoopingWithoutTouchingTheShuffleToggle",
    "SwitchingPlayOffDoesNothing",
    "UpdateShowsTheSourceTime",
    "TheClipLengthIsTheTotalTimeFormattedAsMinutesAndSeconds",
    "PlayingTheClipAlreadyPlayingLeavesTheSourceUntouched",
    "PlayingAnotherClipHandsItToTheBackgroundSource",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerMissionSelectTests." + name for name in (
    "ASavedNewStateIsReturnedOnlyForAMissionSomeGroupHolds",
    "AMissionShowsUnlockedOnlyOnceItIsComplete",
    "TheRankIconsFollowTheRankTheBestTimeEarns",
    "AMissionWithoutRanksHidesTheRankIcons",
    "AMissionWithoutALeaderboardShowsNoLeaderboards",
    "WithoutANavigationManagerOrAnActiveWidgetNavigationIsSkipped",
    "DestroyingTheContainerUnloadsItsZoneAccent",
    "TheJukeboxStaysLockedOnceASubMissionIsLocked",
    "RedStarRingsAndBlueCoinsFollowTheLastSubMissionOfTheirType",
    "WithoutSubMissionsTheSubRewardsAreHidden",
    "TheToggleCarriesItsIndexLabelAndJoinsTheToggleGroup",
    "GhostsAreMadeForEachMissionOfAnUnmetGhostedGroupAndOnlyMetMissionsAreCounted",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SettingsPageDeclarationTests." + name for name in (
    "UIContainerSettingsBaseMatchesTheOriginalMetadata",
    "UIContainerSettingsMatchesTheOriginalMetadata",
    "UIContainerSettingsAccessibilityMatchesTheOriginalMetadata",
    "UIContainerSettingsCameraMatchesTheOriginalMetadata",
    "UIContainerSettingsControlsMatchesTheOriginalMetadata",
    "UISettingsBackgroundImageMatchesTheOriginalMetadata",
    "UIWidgetSDTCheckboxMatchesTheOriginalMetadata",
    "UIWidgetSDTLanguageSelectMatchesTheOriginalMetadata",
    "UIWidgetSDTSliderMatchesTheOriginalMetadata",
    "TheOtherAttributesMatchTheOriginalMetadata",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerSettingsBaseTests." + name for name in (
    "SettingsAreEditedThenSavedThenTheHooksRun",
    "NotSavingStillRefreshesTheBinders",
    "ResettingToDefaultsResetsThenRefreshesSavesAndRefreshesAgain",
    "TheBaseAfterSaveHookDoesNothing",
    "ABooleanChangeIsReportedAndAnUnchangedOneIsNot",
    "AnEnumChangeIsReportedByName",
    "OtherValueTypesAreComparedAndReportedTheSameWay",
    "TwoNullReferenceSettingsAreEqualAndReportNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerSettingsTests." + name for name in (
    "EnablingPublishesTheMetaGameFlagAndTheGraphicsAvailability",
    "EnablingPublishesTheGameplayFlagAndAnUnavailableGraphicsSetting",
    "EachFlagHasItsOwnBinder",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerSettingsAccessibilityTests." + name for name in (
    "AwakePublishesTheSavedSettingsThenCachesTheGameSpeed",
    "RefreshBindersPublishesTheSettingsAndTheTimeLimits",
    "ThePercentageTruncatesTheSpeedProduct",
    "SettingTheGameSpeedStoresItAndLeavesTheSaveForExit",
    "ClosingThePageSavesAPendingGameSpeedAndReportsTheChange",
    "ClosingThePageWithoutChangesSavesAndReportsNothing",
    "ClosingThePageSavesButDoesNotReportAnUnchangedSpeed",
    "ResettingTheAccessibilitySettingsRestoresTheirDefaultsAndClearsThePendingSave",
    "TheResetButtonSavesTheDefaultsAndRefreshesThePage",
    "SettingSubtitlesSavesAtOnceAndClearsThePendingSpeedSave",
    "TheFailStateButtonsSaveTheirStateAndReportTheChange",
    "ChoosingTheCurrentFailStateAgainSavesButReportsNothing",
    "TheGameSpeedToggleIsReportedSavedAndReappliedWhenSwitchedOff",
    "SkippingTheIntroductionIsSavedWithoutAReport",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerSettingsCameraTests." + name for name in (
    "AwakePublishesTheSettingsThenCachesTheSelectedIndices",
    "RefreshBindersPublishesTheSavedCameraSettings",
    "ASavedConfigurationWithoutAnEntryKeepsTheDistanceString",
    "ASavedSensitivityMissingFromTheListFaults",
    "ClosingThePageReportsAChangedCameraDistance",
    "ClosingThePageReportsAChangedCameraSensitivity",
    "ClosingThePageReportsTheBoundSensitivityStringNotTheListEntry",
    "ClosingThePageWithBothChangedReportsTheSensitivityLast",
    "ClosingThePageWithoutChangesReportsNothing",
    "TheHeadingButtonsReportAutoAndManualAndSave",
    "ChoosingTheCurrentHeadingAgainSavesButReportsNothing",
    "TheInvertedControlsButtonsReportAndSave",
    "TheSnapBehindButtonVisibilityIsReportedAndSaved",
    "TheConfigurationSliderRoundsHalvesToEven",
    "TheConfigurationSliderRefreshesThePageAfterStoring",
    "TheSensitivitySliderRoundsHalvesToEven",
    "TheResetButtonRestoresTheCameraDefaults",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerSettingsControlsTests." + name for name in (
    "AwakePublishesTheSettingsThenCachesTheSelectedIndices",
    "RefreshBindersPublishesTheSavedControlSettings",
    "ASavedSensitivityMissingFromItsListFaults",
    "ClosingThePageReportsAChangedStickSensitivity",
    "ClosingThePageReportsAChangedSecondaryCameraSensitivity",
    "ClosingThePageReportsTheBoundSensitivityStringsNotTheListEntries",
    "ClosingThePageWithBothChangedReportsTheCameraSensitivityLast",
    "ClosingThePageWithoutChangesReportsNothing",
    "TheHomingAttackMappingButtonsReportStoreAndSave",
    "TheAirStompMappingButtonsReportStoreAndSave",
    "ChoosingTheCurrentMappingAgainSavesButReportsNothing",
    "TheMovementStickStyleButtonsReportTheDynamicFlag",
    "TheStickSensitivitySliderRoundsHalvesToEven",
    "TheSecondaryCameraSensitivitySliderRoundsHalvesToEven",
    "TheAirAbilityHoldIsReportedStoredAndSaved",
    "TheResetButtonRestoresTheControlDefaults",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UISettingsBackgroundImageTests." + name for name in (
    "AFreshComponentHoldsTheAuthoredMainMenuTint",
    "ResetFindsTheImageOnTheSameObject",
    "AwakeTintsTheBackgroundInTheMetaGame",
    "AwakeTintsTheBackgroundWhenNoCharacterIsInGameplay",
    "AwakeLeavesTheBackgroundAloneDuringGameplay",
    "AwakeWithoutAnImageDoesNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerChallengeResultsTests." + name for name in (
    "DeclarationKeepsTheAuthoredDefaults",
    "AFirstCompletionIsARecordThatAwardsXpAndRepeatsTheNewValuesAsThePreviousBest",
    "AFirstCompletionKeepsZeroAsThePreviousBestWhenTheBestIsNotShown",
    "ASlowerRunIsNoRecordAndTheOldScoreCarriesNoBonus",
    "ATimeRecordWithoutAScoreRecordIsStillANewRecord",
    "ANegativePreviousBestIsTreatedAsNoPreviousBest",
    "RepeatingThePreviousTimeAndScoreIsNoRecord",
    "TheRankForTheNewScoreLoadsItsIconsWhichTheContainerReleasesWhenDisabled",
    "AScoreWithoutARankHidesTheRanks",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerGameHUDMissionProgressTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "EachCompletableTrackerGetsAContainerFromItsDefaultPrefabUnderItsParent",
    "AContainerThatIsNotAlwaysDisplayedIsHiddenEvenWhenNotTiedToItsIsland",
    "GroupedTrackersShareOneGroupContainerPerMissionType",
    "AnOverrideIdentifierReplacesTheDefaultPrefabAndAForeignContainerGivesNone",
    "AnIslandBoundContainerIsShownOnlyForTheActiveIslandMission",
    "TheIslandEventIsHookedOnceWhateverTheNumberOfIslandBoundContainers",
    "WithoutIslandBoundContainersTheIslandEventIsNotHooked",
    "AGroupedTrackerRefreshesEveryContainerOfItsMissionType",
    "ClosingTheUiUnhooksTheTrackersAndForgetsTheContainers",
    "StartFollowsTheResetsAndAReloadRebuildsTheContainers",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerLevelResultsTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "TheTrackersResultsOverrideReplacesTheDefaultActiveMissionIdentifier",
    "AMissionCanBeReplayedWhenItIsAChallengeIncompleteOrACompleteTimeTrial",
    "AnActiveMissionContainerIsCopiedUnderTheRootWithItsRankAudioOn",
    "AMissionWithoutALeaderboardHidesTheLeaderboards",
    "OutsideAChallengeTheChallengeFlagsAreCleared",
    "AChallengeContextSetsUpACopyOfTheResultsContainerAndMirrorsItsNewRecordFlag",
    "WithoutARegularContextNoTimeTrialIsAnnounced",
    "ATimeTrialIsAnnouncedOnlyOnceItsUnlockIsRecordedAsSeen",
    "OnlyATimedReachGoalMissionAnnouncesATimeTrial",
    "ATimeTrialMissionOnlyRecordsItsUnlockAsSeenWhateverItsGoalFlag",
    "ExitPublishesTheExitEventOnlyOnce",
    "ExitStopsTheCurrentCharacterCollectingOrbs",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerLevelSelectTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ASelectedZoneFillsItsTitleSubtitleIconAndColours",
    "OnlyUnlockedUnseenCollectedOrnamentsAreCountedAcrossAllCollections",
    "WithNothingNewTheBadgeIsHidden",
    "WithoutABadgeTheNewOrnamentsAreStillBound",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerLevelSelectZoneTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "ConstructionKeepsTheAuthoredDefaults",
    "EachWidgetReportsItsToggleWithItsOwnLevel",
    "EveryWidgetButTheSelectedOneIsDeselectedQuietly",
    "ThePreselectedUnlockedLevelWinsOtherwiseTheLastUnlockedLevel",
    "TurningThePageOnEnablesTheNavigationOfUnlockedLevelsAndSelectsOne",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerLevelSelectZonesTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "LockedZonesAreListedAsLockedAndTheirPagesHidden",
    "UpdatingTheZoneTurnsOnlyThatPageOn",
    "ASelectedLevelIsAnnouncedAndEveryOtherLevelDeselected",
    "SelectingAZoneSelectsItsToggleTurnsItsPageOnAndAnnouncesIt",
    "TheNeighbourFlagsFollowTheActiveToggleAndTheToggleCount",
    "TheBindingsStartFalse",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerMissionTimerTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "EnablingResetsTheBonusTextAndHidesTheTimer",
    "ARootIsSwitchedToTheRequestedState",
    "ClearingValuesHidesTheTimerUnlessItIsVisible",
    "InitialisingHooksTheTimerAndCachesTheMissionSettings",
    "ATrackerWithoutATimerInitialisesWithoutHookingOne",
    "WithoutAnActiveMissionNothingIsInitialised",
    "ACountdownShowsTheTimeRemainingAndTurnsAlternateWhenLow",
    "MinutesPastAnHourAreTheWholeMinutesOfTheTotal",
    "ATimeRemainingOnTheThresholdIsNotLow",
    "BonusTimeShowsTheAmountAndRestartsTheDisplayCountdown",
    "WithoutAFailStateTheElapsedTimeIsShownAndOnlyATimeTrialGoesLow",
    "ANegativeTimeRemainingIsAlwaysAlternate",
    "AStoppedTimerHidesTheObjectAndAHiddenTimerKeepsItOff",
    "WithoutATimerTheObjectIsHidden",
    "ThePauseObjectsFollowTheTimersEnabledAndRunningState",
    "TheBonusTextShowsTheCurveStartWhileCountingDownAndSwitchesOffWhenOver",
    "DestroyingUnhooksTheTimer",
    "ReinitialisingKeepsASingleHook",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerMissionXpRewardsTests." + name for name in (
    "DeclarationsMatchTheOriginalMetadata",
    "AwakeShowsTheRingXpAndTheRunningTotal",
    "NoRingXpClearsTheRingFlag",
    "WithoutRingCollectableStateNothingIsTouched",
    "ARegularContextWithoutMissionXpShowsNoMissionXp",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetSDTCheckboxTests." + name for name in (
    "TogglingAppendsTheMatchingEventsToThePendingList",
    "TogglingOffAddsOnlyTheCloseEvents",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetSDTLanguageSelectTests." + name for name in (
    "AFreshWidgetHasAnUnchosenFlagAndNoListener",
    "SelectingAnnouncesTheWidgetsLanguage",
    "TheChosenFlagFollowsTheCachedLanguage",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetSDTSliderTests." + name for name in (
    "AFreshWidgetHoldsWhitePipColoursAndAnEmptyPipList",
    "StartDoesNothingForASliderWithoutWholeNumbers",
    "StartYieldsTheSameEndOfFrameWaitTwiceBeforeBuildingThePips",
    "StartSpawnsOnePipPerStepSpacedAcrossTheParentWidth",
    "TheMinimumValueDoesNotChangeThePipLayoutOrPaint",
    "StartPaintsThePipsAndFollowsTheSlider",
    "TheValueArgumentOfThePaintIsIgnored",
    "SettingTheValueMovesTheSliderAndRepaintsThroughTheListener",
    "SettingTheValueWithoutNotifyingStillRepaintsThePips",
    "DestroyingStopsTheSliderFromRepaintingThePips",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.DebugComponentDeclarationTests." + name for name in (
    "TheConfluenceLinkButtonKeepsItsSerializedUrl",
    "TheDeviceTypeUtilitiesIsAStaticClassWithOneUnusedConstant",
    "TheFlightStaminaGizmoDisplayKeepsItsFourSerializedFields",
    "TheProfilerDebugMenuKeepsItsTwoSerializedReferences",
    "TheProjectConsoleDerivesFromTheLogConsoleWithoutFieldsOrOptions",
    "TheDebugMenuPerformanceProfilesKeepsItsFeatureArrayAndFileNameConstants",
    "TheDebugTargetingHomingAttackKeepsItsSerializedConeSettingsAndPrivateState",
    "TheRuntimeIssueTrackerKeepsItsMembersAndItsLambdaOrdinals",
    "TheRuntimeIssueTrackerNestedTypesKeepTheirOriginalShape",
    "TheUIContainerDebugKeepsItsFieldsAndMethodsInTheOriginalOrder",
    "TheUIContainerDebugClosuresKeepTheirCompilerNamesAndCaptureOrder",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.DebugSupportComponentTests." + name for name in (
    "AFreshConfluenceButtonHoldsTheAuthoredPageUrl",
    "SettingTheUrlDoesNotReplaceIt",
    "TheDeviceTypeIsTheSystemDeviceModel",
    "AFreshFlightGizmoIsOpaqueWhiteWithAnEmptyRuler",
    "ResetTakesTheSphereColliderOfTheSameObject",
    "TheFlightGizmoDrawsNothingWithoutACollider",
    "StartEnablesTheProjectConsoleWhenTheConfigurationAllowsTheDeviceConsole",
    "StartDisablesTheProjectConsoleWhenTheConfigurationDoesNot",
    "OpeningTheConsoleFromTheDebugMenuShowsIt",
    "AFreshHomingAttackDebugHoldsTheAuthoredConeAndEmptyLists",
    "TheGizmoRebuildsTheConeFromTheTransformAndTheSerializedSettings",
    "TheGizmoFindsTheNearestTargetInTheConeAndRefillsBothListsEachTime",
    "TheCustomProfileFileLivesInThePersistentDataPath",
    "StartLeavesAListThatAlreadyHoldsACustomProfileAsItIs",
    "ACustomProfileAfterOtherEntriesIsEnoughAndNothingIsAdded",
    "WithoutACustomProfileTheUserProfileIsAppended",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.RuntimeIssueTrackerTests." + name for name in (
    "AFreshTrackerIsClosedWithAUnitUserScale",
    "OpeningNeedsPermissionUnlessForced",
    "ATrackerThatMayOpenOpensWithoutBeingForced",
    "OpeningAnOpenTrackerListensOnlyOnce",
    "ClosingAnOpenTrackerKeepsItsInputsUntilTheNextFrameAndMayReopen",
    "ClosingAnOpenTrackerForGoodKeepsItsInputsUntilTheNextFrame",
    "ClosingAHiddenTrackerOnlyAllowsItToOpenAgain",
    "ClosingAHiddenTrackerForGoodOnlyForbidsOpening",
    "TheCloseAndEndInputsCloseTheTracker",
    "ALevelOpensBothGatesAndLeavingClosesOnlyTheFirst",
    "StartSubscribesToTheSystemsAndOnDestroyRemovesTheSubscriptions",
    "OnDestroyToleratesDestroyedSystems",
    "TheFirstSupportedAttributeChoosesTheConfigurationsAndFollowsProfileChanges",
    "WithoutARuntimeIssueProfileTheTrackerIsDisabledAndKeepsItsConfigurations",
    "WithoutASupportedAttributeThePreviousConfigurationsRemain",
    "EachResolvablePathBecomesOneRowInConfigurationOrder",
    "EachFrameStatisticNameReadsItsOwnStatistic",
    "AFramePathNeedsTheFramePrefixAndAKnownStatisticName",
    "TheFirstPrefixMatchIsTakenAndItsNameAndSeparatorAreConsumed",
    "ANameThatIsTheWholePathLeavesThePathAndAnUnmatchedListLeavesTheEntryEmpty",
    "AFormattedNumberIsRoundedToTwoDecimalsAndKeepsItsUnits",
    "AWholeNumberGainsTwoDecimals",
    "WithoutASpaceTheStatFailsWithEmptyUnits",
    "ATextBeforeTheSpaceFailsButKeepsTheUnits",
    "ALeadingSpaceHasNoNumber",
    "AScanWithEveryRowValidArmsTheOpenWhenInvalidGate",
    "AValueOnTheLimitIsValid",
    "AnInvalidRowOpensAnAllowedTracker",
    "ALaterInvalidRowOpensTheTrackerToo",
    "AnInvalidRowDoesNotOpenATrackerThatMayNotOpen",
    "AnInvalidRowDoesNotOpenATrackerThatMayNotOpenWhenInvalid",
    "AnInvalidFirstRowEndsTheScanBeforeTheGateIsArmed",
    "AnInvalidLastRowAlsoLeavesTheGateDisarmed",
    "ADataRowBelowItsMaximumIsValid",
    "ADataRowBelowItsMinimumIsInvalid",
    "ATrackerWithNoRowsIsAlwaysValid",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.HLProfilerDebugMenuTests." + name for name in (
    "AwakeDoesNothing",
    "TheProfilerToggleClosesAnEnabledConsoleAndOpensADisabledOne",
    "TheTrackerToggleOpensAHiddenTrackerAndClosesAVisibleOne",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerDebugTests." + name for name in (
    "AFreshContainerHasNoPressedMissionButtonAndItsOwnBindables",
    "TheNextRespawnPointFollowsTheListAndWrapsToTheFirst",
    "AnUnknownOrMissingTransformGivesTheFirstRespawnPoint",
    "TheFirstPointOnATransformDecidesTheNextOne",
    "AnEmptyRespawnPointListFaultsAskingForTheFirstPoint",
    "ACurrentCharacterRevealsTheInvincibilityToggleAndOffersTheFirstRespawnPoint",
    "WithoutACurrentCharacterTheRespawnControlsStayHidden",
    "PressingTheRespawnButtonStepsTheCharacterThroughThePoints",
    "ToggleInvincibilityOnlyLooksTheCharacterUp",
    "TheMissionTimerToggleSetsTheUnlimitedTimeFlag",
    "WithoutAMissionSelectContainerNothingCanBeSetUpForIt",
    "AMissionWithoutAnObjectiveTargetOffersTheCompleteButton",
    "ASingleObjectiveMissionOffersTheCompleteButton",
    "AMissionOneObjectiveFromDoneOffersTheCompleteButton",
    "AMissionOneObjectiveShortOfThreeOffersTheCompleteButton",
    "AMissionWithTwoObjectivesLeftOffersTheObjectiveButton",
    "AMissionWithTwoOfThreeObjectivesLeftOffersTheObjectiveButton",
    "AMissionHalfwayThroughFourOffersTheObjectiveButton",
    "NothingIsOfferedWithoutAnActiveMissionButTheOldButtonsAreClosed",
    "TheObjectiveButtonLabelNamesTheMissionAndItsProgress",
    "TheObjectiveButtonDoesNothingForAStateWithoutAnInstancedTracker",
    "PressingTheObjectiveButtonCompletesOnlyTheFirstIncompleteObjectiveAndRebuilds",
    "PressingTheCompleteButtonQueuesTheTrackersCompletionForThePauseClose",
    "NoButtonIsOfferedForAMissionTheManagerDoesNotKnow",
    "ACompletedMissionWithAnUnrankedBestTimeGetsNoButton",
    "ACompletedMissionRankedSGetsNoButton",
    "ACompletedMissionRankedAGetsAButton",
    "AnIncompleteMissionIsOfferedACompleteMissionButton",
    "PressingTheMissionButtonSetsTheBestTimeJustUnderTheBestRank",
    "PressingTheButtonOfAMissionWithoutRanksKeepsTheBestTime",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetToggleCollectionsTests." + name for name in (
    "TheParametersCarryAnEmptyLabelTheCallbackAndTheDisplayNumber",
    "SetupShowsTheDisplayNumberOnTheBadgeAndNeverTheAlertMark",
    "UpdatingTheBadgeRewritesTheCount",
    "ChangingTheValueRunsTheCallbackThenPicksTheSpriteOfTheNewValue",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerDreamPowerStoreBlueCoinCostTests." + name for name in (
    "AProgressionLockedPowerShowsNoCost",
    "AnArchetypeLockedPowerShowsNoCost",
    "ABoughtPowerShowsNoCost",
    "APurchasablePowerShowsItsCost",
    "AnUnaffordablePowerShowsItsCost",
    "AnEquippedPowerShowsNoCost",
    "AStatePastEquippedShowsTheCost",
    "ANegativeStateShowsTheCost",
    "TheCostTextIsThePurchaseCostOfTheDefinitionWhateverTheState",
    "TheCostIsFormattedWithTheInvariantCultureNotTheCurrentOne",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerDreamPowerStoreInfoTests." + name for name in (
    "StartMarksANewStoreAsSeenAndRequestsASave",
    "StartLeavesAnUnflaggedStoreAlone",
    "StartLeavesASeenStoreAlone",
    "ClosingPlaysTheCloseClip",
    "TheFinishedCloseAnimationQueuesTheCloseEventOnce",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetToggleDreamPowerTests." + name for name in (
    "TheGroupElementAndTheNavigationAreTheSerializedReferences",
    "SetupPublishesTheTierTheBackgroundTheIconAndTheCost",
    "SetupFormatsTheCostWithTheCurrentCulture",
    "ThePowerOfTheFirstBandIsTierOne",
    "EachItemStateShowsItsFlagsAndPlaysItsClip",
    "SelectingAProgressionLockedPowerShowsNoSelection",
    "SelectingAnArchetypeLockedPowerShowsNoSelection",
    "SelectingABoughtPowerShowsTheSelection",
    "SelectingANegativeStatePowerShowsTheSelection",
    "SelectingAPurchasablePowerShowsTheSelection",
    "SelectingAnEquippedPowerShowsTheSelection",
    "DeselectingClearsTheSelectionAndRestoresTheItemStateVisuals",
    "NavigatingToAndAwayTogglesTheWidgetAndRunsTheSelectionSequence",
    "DestroyReleasesTheIconAndTheBandBackgroundOnlyAfterSetup",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerDreamPowerListTests." + name for name in (
    "StartShowsTheSavedLoadoutOfTheDefaultArchetypeInThreeWidgets",
    "ADreamPowerMissionShowsTheLoadoutOfItsArchetype",
    "AMissionWithoutDreamPowersHidesTheList",
    "AnUnavailableStoreHidesTheList",
    "AnEmptyLoadoutHidesTheList",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerDreamPowerStoreTests." + name for name in (
    "EnablingTheStoreSetsUpTheCounterTheArchetypeTheSlotsAndTheDisplays",
    "TheBlueCoinsAvailableAreTheCollectedCoinsLessTheSpentOnes",
    "TheArchetypeIsTheActiveMissionsOrTheDefault",
    "TheFirstBandAndTheCollectedBandsAreUnlocked",
    "TheDebugUnlockOpensEveryBand",
    "TheItemStateFollowsTheBandTheBoughtFlagTheArchetypeAndTheCoins",
    "TheWidgetParametersCarryTheStateAndACallbackOverThatState",
    "TheCounterShowsTheBoughtPowersOutOfAllPowers",
    "EachItemStateLabelsTheActionAndSetsItsAvailability",
    "TheLockMessageIsThePriceThenTheArchetypeReason",
    "TheSlotStatusReachesEverySlotButton",
    "ClearingTheSelectionNavigatesEveryWidgetAwayAndTurnsTheActionOff",
    "TheActionDoesNothingWithoutAvailabilityOrASelectedPower",
    "ChoosingAPurchasablePowerPublishesItsPriceDescriptionAndAction",
    "TheStorePriceFollowsTheCurrentCultureAndTheCostContainerTheInvariantOne",
    "TheLockMessageFollowsTheItemStateAndTheArchetype",
    "ACombinedIconMatchesExactlyItsArchetypeSet",
    "TheDescriptionShowsTheArchetypeOrTheMatchingCombinedIconAndReleasesTheLastOne",
    "AnOddCountOverTwoRowsPadsTheLastRow",
    "AnEvenCountOverTwoRowsNeedsNoPadding",
    "FewerPowersThanRowsPadTheRemainingRows",
    "OnePowerPerRowNeedsNoPadding",
    "OneRowTakesEveryPowerWithoutPadding",
    "EachClonedWidgetShowsTheStateOfItsPower",
    "SettingUpAgainClearsTheRecordsAndDestroysTheChildren",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerDreamPowerStoreEquipTests." + name for name in (
    "FinishingWhileChoosingASlotEquipsThePowerThereAndResumesBrowsing",
    "FinishingWhileBrowsingTogglesTheSlotWithoutAnimation",
    "StartingTheEquipSelectsTheFirstEmptySlotAfterAnEquippedFirstSlot",
    "StartingTheEquipSelectsTheFirstSlotWhenOnlyTheSecondIsEquipped",
    "StartingTheEquipSelectsTheOnlyEmptySlot",
    "StartingTheEquipFallsBackToTheFirstSlotWhenEveryOneIsEquipped",
    "TheActionOnAnEquippedPowerUnequipsItsSlot",
    "TheActionOnABoughtPowerInNoSlotStartsTheSlotSelection",
    "TheActionOnAnUnboughtPowerPurchasesItAndQueuesItsUnlock",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerCollectionsDreamPowerStoreTests." + name for name in (
    "EnablingShowsTheCollectionsTitleAndSwitchesTheInfoPartsOff",
    "EnablingWithoutTheInfoPartsStillShowsTheTitle",
    "EnablingEndsWithTheUiImageOfTheArchetypeNotTheBaseAlternateImage",
    "TheTitleShowsTheUiImageOfTheMissionsArchetype",
    "ChoosingAPowerReportsTheUnlockPromptOnlyForAPurchasableOne",
    "ARemovedSelectionHandlerNoLongerHearsTheSelection",
    "NavigatingAwayClearsThePurchasableFlag",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerCollectionsTests." + name for name in (
    "ThePromptIsCopiedAndTheSelectButtonIsShownOnlyForARealPrompt",
    "TheTabInputsMoveTheTabGroupFromAHalfUpwards",
    "NavigationFollowsTheStoredUnlockInProgressFlag",
    "TurningTheMusicTabOffOnlyStopsThePlayingAnimations",
    "ReselectingTheCurrentMusicTabKeepsThePlayingAnimations",
    "TurningTheStoreTabOffChangesNothing",
    "ReselectingTheCurrentStoreTabChangesNothing",
    "TheGalleryToggleShowsTheSelectButtonAndStoresTheTab",
    "TheStoreNavigatingAwayClearsThePurchasableFlag",
    "TheStoreTabIsSetUpWithoutABadge",
))
# Selected music-tab submission and actual deselection through the real toggle group (#481).
EDITMODE_TESTS += (
    "ProjectLucid.Tests.UIContainerCollectionsTests.SubmittingTheSelectedMusicToggleKeepsThePlayingAnimations",
)
EDITMODE_TESTS += tuple("ProjectLucid.Tests.DreamPowerStoreDeclarationTests." + name for name in (
    "TheUIContainerCollectionsDeclarationMatchesTheOriginalMetadata",
    "TheUIContainerCollectionsDreamPowerStoreDeclarationMatchesTheOriginalMetadata",
    "TheCollectionsDreamPowerStoreClosureCacheClassDeclarationMatchesTheOriginalMetadata",
    "TheUIContainerDreamPowerListDeclarationMatchesTheOriginalMetadata",
    "TheUIContainerDreamPowerStoreDeclarationMatchesTheOriginalMetadata",
    "TheUIContainerDreamPowerStoreCombinedArchetypeIconDeclarationMatchesTheOriginalMetadata",
    "TheWidgetParametersClosureClassDeclarationMatchesTheOriginalMetadata",
    "TheDreamPowerChangedClosureClassDeclarationMatchesTheOriginalMetadata",
    "TheUIContainerDreamPowerStoreBlueCoinCostDeclarationMatchesTheOriginalMetadata",
    "TheUIContainerDreamPowerStoreInfoDeclarationMatchesTheOriginalMetadata",
    "TheUIWidgetToggleCollectionsDeclarationMatchesTheOriginalMetadata",
    "TheUIWidgetToggleCollectionsParametersDeclarationMatchesTheOriginalMetadata",
    "TheUIWidgetToggleDreamPowerDeclarationMatchesTheOriginalMetadata",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.PauseAndSoundSettingsDeclarationTests." + name for name in (
    "InvertCameraSettingMatchesTheOriginalMetadata",
    "SoundLevelsOptionsValuesMatchesTheOriginalMetadata",
    "UIContainerAnalyticsConsentMatchesTheOriginalMetadata",
    "UIContainerCreditsMatchesTheOriginalMetadata",
    "UIContainerEmailSupportMatchesTheOriginalMetadata",
    "UIContainerPauseMatchesTheOriginalMetadata",
    "UIContainerPauseBackgroundMatchesTheOriginalMetadata",
    "UIContainerQRCodeMatchesTheOriginalMetadata",
    "UIContainerResetGameDataMatchesTheOriginalMetadata",
    "UIContainerSoundLevelsMatchesTheOriginalMetadata",
    "UIContainerSoundLevelsOptionsMatchesTheOriginalMetadata",
    "UIWidgetButtonSupportMatchesTheOriginalMetadata",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SoundLevelsOptionsTests." + name for name in (
    "TheValuesHoldTheFourLevelsInTheirOwnOrder",
    "ApplyingTheValuesWritesEachLevelToItsOwnSettingAndMarksThemChanged",
    "ApplyingTheValuesToNoSettingsFaults",
    "SetupPublishesTheLevelsAndGetValuesReadsThemBack",
    "SetupStoresTheMusicAndSfxCallbacksBeforeItSetsTheBindablesAndTheOthersAfter",
    "EachToggleSetsItsBindableBeforeCallingItsOwnCallback",
    "ATogglePressedWithoutACallbackStillSetsItsBindable",
    "TheSaveClickCallsItsCallbackAndFaultsWithoutOne",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SupportEmailPageTests." + name for name in (
    "TheEmailBodyIsThePrefixThenTheDeviceAndVersionLinesThenTheSuffix",
    "TheMailLinkEscapesTheAddressTheSubjectAndTheBodyIndividually",
    "OpeningTheEmailContainerQueuesTheSupportEventOnce",
    "EnablingTheEmailPageFormatsTheRequestTheAddressTheSubjectAndTheReport",
    "ClosingTheEmailPageQueuesTheCloseEventOnce",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerCreditsTests." + name for name in (
    "ANewPageScrollsAtSpeedOne",
    "WhileTheScrollToPointIsBelowTheEndPointTheParentMovesUpByTheFrameTimeTimesTheSpeedAndOneNullIsYielded",
    "ReachingTheEndHeightStopsTheScrollAndWaitsTheConfiguredSeconds",
    "TheScrollEndsOnTheFrameThePointReachesTheEndThenTheTriggerFiresOnceAfterTheWait",
    "AMissingEndTriggerIsSkipped",
    "AMissingMarkerPointFaultsTheFirstStep",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerQRCodeTests." + name for name in (
    "SetupShowsTheLocalisedAddress",
    "TheFirstOverrideForTheCurrentLanguageReplacesTheDefaultSprite",
    "WithoutAnOverrideForTheCurrentLanguageTheDefaultSpriteIsShown",
    "AStringWithoutALookupEntryKeepsTheSpriteAsItWas",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerResetGameDataTests." + name for name in (
    "DeletingWithAnOpenSaveClosesItMarksTheGameNotLoadedAndQueuesTheConfirmEvent",
    "DeletingWithoutAnOpenSaveOnlyQueuesTheConfirmEvent",
    "ADeleteTwiceQueuesTheConfirmEventOnce",
    "DeletingASaveOpenInANumberedSlotClearsThatSlot",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerAnalyticsConsentTests." + name for name in (
    "StartAppliesTheBaseGameplayControlThenPublishesTheManagersConsentState",
    "TheToggleSetsConsentedForOnAndRejectedForOff",
    "ClosingForAnUnderagePlayerLeavesTheManagerUntouched",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerSoundLevelsTests." + name for name in (
    "StartTakesTheServicesAndShowsTheStoredLevels",
    "LoadSettingsReadsTheLiveSettingsOfTheSaveManager",
    "EachToggleStoresItsOwnLevelAndGoesOnToTheMixer",
    "ResettingToDefaultsRestoresFullVolumeInTheLiveSettingsAndThePage",
    "SavingTheLevelsStoresACopyOfTheSettingsAndPublishesTheSaveEvent",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.InvertCameraSettingTests." + name for name in (
    "AwakeTakesTheSaveManager",
    "TheActionFlipsTheStoredFlagBothWaysAndSavesEachTime",
    "TheActionBeforeAwakeDoesNothing",
    "TheActionWithoutLoadedSettingsDoesNothing",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerPauseTests." + name for name in (
    "StartTakesTheManagersAndPublishesTheLevelAndTheQualityFlags",
    "TheQualityFlagsFollowTheFrameTimeAndTheDeviceCapability",
    "TheLevelManagerCallbackKeepsTheManagerAndAsksForTheMissionManager",
    "WithoutACurrentLevelOnlyTheMissionManagerIsKept",
    "ALevelWithoutAMissionOrAZonePublishesOnlyItsNames",
    "AnActiveMissionIsHandedToTheResultsAndRanksContainers",
    "ALevelInAZoneShowsTheZoneAccentTheActNumberAndNoBackground",
    "AZoneThatDoesNotListTheLevelPublishesActNumberZero",
    "ALevelListedTwiceInAZoneTakesTheFirstPosition",
    "CloseQueuesTheResumeEventOnce",
    "TheQuitAnalyticsGoThroughTheActiveContext",
    "DestroyingReleasesTheAccentAndForwardsThePauseCloseAction",
    "DestroyingWithoutAPauseCloseActionStartsNoWait",
    "DestroyingReleasesTheLevelImageOnlyWhenABackgroundIsPublished",
    "RestartAndRespawnEachStartOneCoroutine",
    "RespawnQueuesTheResumeEventThenWaitsForTheMenuToBeOpen",
    "RespawnOfAnOpenMenuPutsTheCurrentCharacterOutOfBoundsAtOnce",
    "RestartQueuesTheResumeEventWaitsForTheMenuThenRestartsTheLevel",
    "RestartOfAnOpenMenuStillYieldsAWaitThatIsAlreadySatisfied",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerPauseBackgroundTests." + name for name in (
    "WithoutTheRenderTextureEnablingSwitchesTheComponentOff",
    "EnablingWithTheRenderTextureKeepsTheManagerAndSubscribesToTheMainCameraEvent",
    "EnablingBeforeTheManagerRegistersInitialisesWhenItDoes",
    "InitialisingFromACameraTakesItsSizeAndCullingMaskAndStopsListening",
    "TheSizeIsHalvedWhileEitherDimensionExceedsTheQualityMaximum",
    "RenderingWhileTheCameraIsNotReadyDoesNothing",
    "UpdateRendersAndClearsTheFlagOnlyWithTheRenderTextureAndTheFlag",
    "DisablingLeavesTheCameraAloneWhenThereIsNoRenderTextureOrTheCameraIsNotReady",
    "AFreshQualitySettingHasNoMaximum",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SaveSlotAndSettingsPageDeclarationTests." + name for name in (
    "UIContainerSaveDataViewParametersMatchesTheOriginalMetadata",
    "UIWidgetToggleSaveDataMatchesTheOriginalMetadata",
    "UIContainerSaveSlotsMatchesTheOriginalMetadata",
    "UIContainerSettingsLanguageMatchesTheOriginalMetadata",
    "UIContainerSettingsLegalMatchesTheOriginalMetadata",
    "UIContainerSettingsNotificationsMatchesTheOriginalMetadata",
    "UIContainerSettingsQualityMatchesTheOriginalMetadata",
    "TheQualityPageAttributesMatchTheOriginalMetadata",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerSaveDataViewParametersTests." + name for name in (
    "TheParametersKeepTheSaveTheProgressTheLabelAndTheCallback",
    "NothingIsValidatedOrReplaced",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIWidgetToggleSaveDataTests." + name for name in (
    "AFreshWidgetHasFiveSeparateEmptyBinders",
    "ASlotWithoutASaveIsOnlyMarkedEmpty",
    "ASaveWithoutAVisitedLevelIsEmpty",
    "ASaveWhoseVisitedLevelIsBlankIsEmpty",
    "ASavedSlotPublishesTheCollectedOrbsAndTheOrnamentCount",
    "ProgressWithoutAnOrbTotalShowsNoOrbs",
    "TheSaveTimeIsShownInLocalTimeFromUnixMilliseconds",
    "ThePlayTimeFillsTheLocalisedPlayTimeString",
    "ThePlayTimeShowsTheHoursOfTheDayOnly",
    "OnlyTheTotalPlayTimeStatisticIsUsed",
    "SelectingTurnsTheToggleOnOnce",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerSaveSlotsTests." + name for name in (
    "TheSelectedSlotStartsAsTheOpenSlot",
    "EachStoredSaveIsSetUpOnTheToggleOfItsIndex",
    "OnlyTheOpenSlotsToggleIsSelected",
    "WithNoOpenSlotNoToggleIsSelected",
    "TurningAToggleOnSelectsItsOwnSlot",
    "AwakeClearsTheLoadingFlagAndSetsUpTheSlotsForTheRegisteredSaveManager",
    "SettingTheActiveSlotStoresTheIndex",
    "NavigatingWithoutANavigationManagerDoesNothing",
    "SelectingASaveStartsItAndRemembersTheChosenSlot",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerSettingsLanguageTests." + name for name in (
    "StartMarksTheCurrentLanguageAsChosenOnEveryButton",
    "StartCachesTheDefaultLanguageWhenNothingWasChosen",
    "SelectingAButtonChoosesItsLanguageOnEveryButton",
    "CachingALanguageTellsEveryButton",
    "ClosingWithANewLanguageAppliesItAndLeavesTheFontsNotReady",
    "ClosingWithTheSameLanguageReportsTheFontsReadyOnTheNextFrame",
    "ClosingMarksTheFontsNotReadyBeforeTheLanguageChangeIsAnnounced",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerSettingsLegalTests." + name for name in (
    "TheCreditsAreOfferedFromTheLevelSelect",
    "TheCreditsAreOfferedFromTheMainMenu",
    "TheCreditsAreOfferedWhenBothAreOpen",
    "TheCreditsAreHiddenFromAnyOtherPage",
    "TheConsentButtonFollowsTheConsentManagerAndIsIndependentOfTheCredits",
    "WithoutAConsentManagerTheConsentButtonIsHiddenAndTheCreditsAreUnchanged",
    "WithTheAdaptersOffTheOriginalBodyConsultsTheRegisteredConsentManager",
    "TheConstructorBuildsTwoSeparateBinders",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerSettingsNotificationsTests." + name for name in (
    "StartShowsAGrantedPermissionWithoutNotifying",
    "StartShowsARefusedPermissionWithoutNotifying",
    "TurningThePermissionOnGrantsIt",
    "TurningThePermissionOffRetractsIt",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.UIContainerSettingsQualityTests." + name for name in (
    "AwakeOpensOnTheActiveProfileAndTheLiveRenderSettings",
    "TheFirstLevelHoldingTheActiveProfileIsShown",
    "WithoutAMatchingLevelTheMiddleLevelIsShown",
    "ChoosingALevelGivesItsProfileTheDefaultProfilesLockedLevels",
    "ALowHalfRoundsDownToTheEvenLevel",
    "AMiddleHalfRoundsUpToTheEvenLevel",
    "AHighHalfRoundsDownToTheEvenLevel",
    "ABelowHalfValueRoundsDown",
    "ChoosingALevelShowsItsNameAndRestoresTheMenuRenderScale",
    "TheResolutionSliderIsShownWithOneDecimal",
    "ALowHalfSelectsTheFirstAntiAliasingSetting",
    "AHighHalfSelectsTheEvenAntiAliasingSetting",
    "AWholeValueSelectsThatAntiAliasingSetting",
    "TwoSettingsWithTheSameSampleCountShowTheFirst",
    "ANameWithoutAPlaceholderIsNotFormatted",
    "ClosingUnchangedReportsNothingAndSavesTheProfileAndScale",
    "ClosingWithAnotherLevelReportsTheQuality",
    "ClosingWithAnotherResolutionScaleReportsItAndAppliesIt",
    "ClosingWithAnotherAntiAliasingReportsItAndAppliesIt",
    "ClosingWithEverythingChangedReportsTheAntiAliasingLast",
    "RefreshingShowsTheLevelOfTheActiveProfile",
    "ResettingReturnsToTheDefaultProfileScaleAndAntiAliasing",
    "TheConstructorBuildsFourSeparateBinders",
))

# Offline boot hooks (FairPlay check, cross promotion, the splash skip at Boot) and the restored Apple Arcade splash assembly.
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OfflineBootHookTests." + name for name in (
    "TheAdapterSwitchIsOnAndTheVerificationOverrideRestoresIt",
    "FairPlayCheckEntersThroughItsSuccessPathWithoutTheNativeLibrary",
    "FairPlayCheckKeepsTheOriginalRegistrationAfterTheGuard",
    "TheNoContentResourceManagerReportsAFailedDownloadAndNoContent",
    "CrossPromoStartKeepsTheOriginalBodyAfterTheGuard",
    "BootArmsTheSplashSkipThenKeepsTheOriginalBody",
    "TheSplashListenerIsTheUnloaderBesideTheVideoPlayer",
    "TheSplashListenerCallWaitsUntilTheGraphHasLeftBoot",
    "ArmWaitsForTheNamedSceneAndLeavesTheEventThere",
))
# D14: actual enable/disable and authored reactivation of shipped cross-promo entry shapes (#502).
PLAYMODE_TESTS += (
    "ProjectLucid.Tests.OfflineCrossPromoEntryPointTests.ShippedEntryShapesStayHiddenAcrossAuthoredReactivationAndRestoreWithAdapterOff",
    "ProjectLucid.Tests.OfflineCrossPromoEntryPointTests.HiddenLauncherReleasesDeferredStartupAndIndependentPromptBeforeTheSystemExists",
)
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OfflineCrossPromoHookTests." + name for name in (
    "ColdStartWithLocalStorageStaysDisabledWithoutRequests",
    "ColdStartWithoutLocalStorageStaysDisabledWithoutRequests",
    "ExistingCacheWithLocalStorageStaysDisabledWithoutRequests",
    "ExistingCacheWithoutLocalStorageStaysDisabledWithoutRequests",
    "TryShowCurrentAdGuardsTheAdReadAndKeepsTheOriginalBody",
))
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalAppleArcadeSplashScreenTests." + name for name in (
    "SplashScreenPlayerKeepsItsOriginalDeclarations",
    "TheVideoProviderAndItsInterfaceKeepTheirOriginalDeclarations",
    "AspectTextOfAWideScreenIsNineteenPointFiveByNine",
    "AspectTextAtTheFloatNearest187IsNineteenPointFiveByNine",
    "AspectTextJustBelow187IsSixteenByNine",
    "AspectTextOfSixteenByNineIsSixteenByNine",
    "AspectTextAtTheFloatNearest177FallsBelowTheDoubleThreshold",
    "AspectTextAtTheFloatNearest16IsSixteenByTen",
    "AspectTextJustBelow16IsFourByThree",
    "AspectTextOfASquareScreenIsFourByThree",
    "ProviderHelpersKeepTheirNativeResults",
    "TheTaskCompletesWithWhateverTheResourcesRequestFound",
    "ActionOnVideoCompletedRestoresTheFrameRateThenRaisesTheEvent",
    "LoopPointReachedRestoresTheFrameRateThenRaisesTheEvent",
    "VideoCompletedRestoresTheFrameRateThenRaisesTheEvent",
    "ReadyHandlerFaultsOnANullSourceBeforeSwitchingFrameEventsOff",
    "TheDefaultTargetFrameRateIsSixty",
    "StartSkipsTheSplashThroughTheOriginalCompletionPathOnADesktopTarget",
    "OnlyDesktopTargetsSkipTheSplash",
    "StartKeepsTheOriginalBodyAfterTheGuard",
    "ABoundPlayerDoesNotCompleteWhileTheBootGraphIsInBoot",
    "ABoundPlayerCompletesAtOnceWhenTheGraphHasLeftBoot",
    "TheBoundCompletionRunsOnceAfterTheGraphLeavesBoot",
))

# ApplicationStateLoadingInitialiseSystems: the level systems and the loading event (#384).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalApplicationFsmGapTests." + name for name in (
    "LoadingInitialiseSystemsPublishesIntoTheUsersStorageOnce",
    "LoadingInitialiseSystemsAddsTheLevelSystemsInNativeOrder",
))

# Camera, character camera, cutscene and screen-controls trigger states (#385).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalApplicationFsmGapTests." + name for name in (
    "TriggerCharacterCameraTakesTheCameraOnEnterAndReleasesItOnLeave",
    "TriggerCameraReusesOrReleasesTheUsersOverrideHandle",
    "TriggerCutsceneConsumesAQueuedCutsceneWithoutPlayingAPreload",
    "TriggerCutsceneChoosesPreloadOrPlayByPreloadOnly",
    "TriggerScreenControlsPublishesItsGroupFromTheCharacterWhenMessagesExist",
))

# ApplicationStatePostLevelLoadReset, ApplicationStateInitialiseHLCrashReport and
# HLCrashReportSettings (#386).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OriginalApplicationFsmGapTests." + name for name in (
    "PostLevelLoadResetRebuildsThePoolsThenSwapsTheCameraAndRestarts",
    "InitialiseHLCrashReportStartsOnlyWhenCrashReportsAreEnabled",
))

# Windows and Linux players take the shipped macOS player branches (#503, H-070..H-076).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.OfflinePlatformHookTests." + name for name in (
    "OnlyTheWindowsAndLinuxPlayersTakeTheMacPlayerBranch",
    "InputMonitorsAreActiveOnWindowsAndLinuxWhereTheListNamesTheMacPlayer",
    "DefaultPerformanceProfileUsesTheMacDefinitionOnWindowsAndLinux",
    "BootDisplaySetupHookRepeatsTheMacPlayerBranch",
    "ResolutionSettingHookSkipsTheOriginalPlatformReturn",
    "DisplayChangeHookSkipsTheOriginalPlatformReturn",
    "StartScreenCursorHookReachesTheCursorBody",
    "HookPolarityCheckRejectsInvertedConditions",
))
EDITMODE_TESTS += ("ProjectLucid.Tests.UIContainerGameCenterAccessPointTests.StartEnablesTheCanvasScalerOnWindowsAndLinuxPlayersAsOnMacOS",)
# SurfaceTracking cone test from the projected axis point (#525).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.SurfaceTrackingConeOperandTests." + name for name in (
    "InteriorAndExteriorPointsKeepTheirResults",
    "RotatedBoundaryPointsFollowTheOriginalOperand",
))
EDITMODE_TESTS += tuple("ProjectLucid.Editor.Tests.InputSystemControllerProviderTests." + name for name in (
    "NotifiesOncePerPollAfterConnectDisconnectReplacementAndSwap",
    "NamesLetTheAuthoredIdentifiersPickEachGlyphSet",
    "BindingInputAndGlyphFollowEachGamepadThroughDisconnectReplacementAndReconnect",
    "ReconnectingTheSameGamepadStartsAFreshButtonHistory",
    "StoppingReleasesOnlyThatProvidersControllersAndRestartingRegistersAgain",
    "PollReadsEveryGamepadControlInGameControllerLayout",
    "SourceKeepsTheOriginalCastForGameControllerProvidersAndWithTheAdaptersOff",
    "MonitorFixtureReleasesEveryBindingCloneItCreates",
))
# StickToCollider reach and pull-down use the tangent of the angle (#581).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.CharacterPhysicsUtilitiesTests." + name for name in (
    "ReachIsTheStepTimesTheTangentOfTheAngle",
    "FloorBeyondTheTangentReachIsNotTouched",
    "PullDownIsTheTangentReachTimesTheForce",
))
# Distinct per-instance rendering-layer readback from real renderers (#516).
EDITMODE_TESTS += tuple("ProjectLucid.Tests.RenderingLayerInstancingTests." + name for name in (
    "EnvUnlitForwardWritesEachInstancesRenderingLayers",
    "TrackDepthNormalsWritesEachInstancesRenderingLayers",
    "RenderRestoresANondefaultGlobalMipBias",
    "FixtureCleanupFailureKeepsThePrimaryFailure",
))
