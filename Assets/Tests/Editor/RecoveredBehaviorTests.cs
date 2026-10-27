using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ProjectLucid.Tests
{
    public sealed class RecoveredBehaviorTests
    {
        [Test]
        public void OriginalLevelManagedSystemsRetainOwnershipAndCallbackOrder()
        {
            Assert.That(ProjectLucid.Editor.LevelManagerSystemsVerification.RunManaged(), Is.EqualTo(81));
        }

        [Test]
        // Game.Runtime SurfaceEvent 0x02000a9e: OnSurfaceEnter 0x06003d1f (x86-64 0x6d2ef0)
        // invokes m_onEnter, then starts <ProcessStayEvents>d__7 (0x02000a9f) only when
        // m_onStay.GetPersistentEventCount() > 0; OnSurfaceExit 0x06003d20 (x86-64 0x6d3000)
        // stops and nulls the handle before m_onExit. MoveNext 0x06003d25 (x86-64 0x6d30e0)
        // invalidates state before the owner/m_onStay loads and invokes before yielding the
        // cached wait; Dispose 0x06003d24 (x86-64 0x6d30d0) is empty and Reset 0x06003d27
        // (x86-64 0x6d3160) throws NotSupportedException. The authored persistent-listener
        // start/stop path runs on frames in SurfaceEventLifecycleTests (PlayMode).
        public void OriginalSurfaceEventsRetainPersistentCallbacksAndIteratorOrder()
        {
            Assert.That(ProjectLucid.Verification.SurfaceEventVerification.Run(), Is.EqualTo(42));
        }

        [Test]
        // HLUnityCore.Runtime PooledPrefab`1 0x020001f1 .ctor 0x06000c98 (x86-64 shared body
        // 0x18d0830) stores the instance and the raw index with no clamp; ObjectUpdateType
        // 0x020001f2 literals Enable 0x0400068d / Disable 0x0400068e.
        public void OriginalPrefabPoolValuesRetainContractsAndDefaults()
        {
            Assert.That(ProjectLucid.Editor.PrefabPoolVerification.RunManaged(), Is.EqualTo(39));
        }

        [Test]
        // PrefabPool`1 0x020001f5 shared bodies: SpawnInstance 0x06000cc0 (x86-64 0x18dd7a0)
        // pops the active free stack before the inactive one (which reparents to the active
        // bucket), then sets position/rotation/scale and expands only when m_resizeWhenFull;
        // typed DespawnInstance 0x06000cc2 (x86-64 0x18ddac0) returns on destroyed pool or
        // index -1, otherwise pushes onto m_freeActiveObjects with no dedupe or reparent;
        // the boxed overload 0x06000cc1 (x86-64 0x18dd9c0) ignores other PooledPrefab types;
        // LateUpdate 0x06000cb9 (x86-64 0x18dd300) pushes inactive before
        // Transform.SetParent(inactive bucket index / 50, false).
        public void OriginalPrefabPoolRetainsReuseResizeAndDeferredReturnOrder()
        {
            Assert.That(ProjectLucid.Editor.PrefabPoolVerification.RunEngine(), Is.EqualTo(39));
        }

        [Test]
        public void OriginalEditableRibbonKnotsRetainInitializationAndCacheRules()
        {
            Assert.That(ProjectLucid.Editor.EditableRibbonProviderVerification.RunManaged(), Is.EqualTo(153));
        }

        [Test]
        public void OriginalEditableRibbonProviderRetainsGeometryAndSurfaceQueries()
        {
            Assert.That(ProjectLucid.Editor.EditableRibbonProviderVerification.RunEngine(), Is.EqualTo(39));
        }

        [Test]
        public void OriginalRibbonRuntimeRetainsEdgeRulesAndNaturalDistanceComparer()
        {
            Assert.That(ProjectLucid.Editor.RibbonSharedRuntimeVerification.RunManaged(), Is.EqualTo(177));
        }

        [Test]
        public void OriginalInputBindingsRetainModifierNamesAndProviderContracts()
        {
            Assert.That(ProjectLucid.Editor.InputBindingVerification.Run(), Is.EqualTo(114));
        }

        [Test]
        public void OriginalSplineRuntimeRetainsNativeProjectionAndUnorderedDistanceRules()
        {
            Assert.That(ProjectLucid.Editor.SplineSharedRuntimeVerification.RunManaged(), Is.EqualTo(201));
        }

        [Test]
        public void OriginalInputDebounceRetainsThresholdDirectionAndCrossingTiming()
        {
            Assert.That(ProjectLucid.Editor.InputDebounceVerification.Run(), Is.EqualTo(338));
        }

        [Test]
        public void OriginalSplinePrerequisitesRetainRecordsComparersAndCallbacks()
        {
            Assert.That(ProjectLucid.Editor.InertSplinePrerequisiteVerification.RunManaged(), Is.EqualTo(81));
        }

        [Test]
        public void OriginalKnotRuntimePrerequisitesRetainContractsAndReceiverFaults()
        {
            Assert.That(ProjectLucid.Editor.KnotRuntimePrerequisiteVerification.RunManaged(), Is.EqualTo(6));
        }

        [Test]
        public void OriginalSplineRenderDefinitionsRetainQualitySelectionAndUnitySerialization()
        {
            Assert.That(ProjectLucid.Editor.RibbonEditorDefinitionVerification.RunManaged(), Is.EqualTo(82));
            Assert.That(ProjectLucid.Editor.RibbonEditorDefinitionVerification.RunEngine(), Is.EqualTo(27));
        }

        [UnityTest]
        public IEnumerator OriginalSurfaceEditorStylesCopyTheGenuineSkinInOnGUI()
        {
            yield return ProjectLucid.Editor.RibbonEditorDefinitionGUIFixture.VerifyGUI();
        }

        [Test]
        public void OriginalRibbonBoundsRetainCornerOrderScratchTailAndTransforms()
        {
            Assert.That(ProjectLucid.Editor.RibbonBoundsAlignmentVerification.RunManaged(), Is.EqualTo(130));
            Assert.That(ProjectLucid.Editor.RibbonBoundsAlignmentVerification.RunEngine(), Is.EqualTo(29));
        }

        [Test]
        public void OriginalCameraAxisAndRecenterRetainLookupDefaultsAndInitialization()
        {
            Assert.That(ProjectLucid.Editor.CameraAxisRecenterVerification.RunManaged(), Is.EqualTo(199));
            Assert.That(ProjectLucid.Editor.CameraAxisRecenterVerification.RunEngine(), Is.EqualTo(95));
        }

        [Test]
        public void OriginalSplineMathRetainsCachingApproximationAndAliasedOutputs()
        {
            Assert.That(ProjectLucid.Editor.SplineMathVerification.RunManaged(), Is.EqualTo(199));
            Assert.That(ProjectLucid.Editor.SplineMathVerification.RunEngine(), Is.EqualTo(26));
        }

        [Test]
        public void OriginalRibbonPrerequisitesRetainTransformsContractsAndMarkerOrder()
        {
            Assert.That(ProjectLucid.Editor.RibbonPrerequisitesVerification.RunManaged(), Is.EqualTo(96));
            Assert.That(ProjectLucid.Editor.RibbonPrerequisitesVerification.RunEngine(), Is.EqualTo(26));
        }

        [Test]
        public void OriginalSurfacePhysicsAndResolverRetainOrderedQueriesAndProfilerConstruction()
        {
            Assert.That(ProjectLucid.Editor.SurfaceResolverVerification.Run(), Is.EqualTo(91));
        }

        [Test]
        public void RibbonAccessorMarkersAreRestoredByTheActualUnityCompilerPipeline()
        {
            Assert.That(RibbonPipelineVerification.Run(), Is.EqualTo(47));
        }

        [Test]
        public void OriginalGravityDescriptionRetainsAuthoredLimitsAndQueryOrder()
        {
            Assert.That(ProjectLucid.Editor.GravityDescriptionVerification.RunManaged(), Is.EqualTo(26));
            Assert.That(ProjectLucid.Editor.GravityDescriptionVerification.RunEngine(), Is.EqualTo(74));
        }

        [Test]
        // Game.Runtime GravitySurfaceDefinition 0x0200055f has exactly the seven public fields
        // 0x04001344..0x0400134a and a base-only .ctor 0x06001d21 (x86-64 0x54d8c0);
        // GravitySurfaceDefinitionGroup.GetKeyComparer 0x06001d23 (x86-64 0x54d8e0) returns
        // HardlightEnumComparers static slot 0x1a8 = GravitySurfaceTypeComparer 0x04000bcb.
        public void OriginalGravitySurfacesRetainTypedKeysAndUnitySerialization()
        {
            Assert.That(ProjectLucid.Editor.GravitySurfaceDefinitionVerification.RunManaged(), Is.EqualTo(43));
            Assert.That(ProjectLucid.Editor.GravitySurfaceDefinitionVerification.RunEngine(), Is.EqualTo(24));
        }

        [Test]
        public void OriginalSurfaceTrackingRetainsCallbackAndMeshBoundsOrder()
        {
            Assert.That(ProjectLucid.Editor.TrackerSurfaceVerification.RunManaged(), Is.EqualTo(92));
            Assert.That(ProjectLucid.Editor.TrackerSurfaceVerification.RunEngine(), Is.EqualTo(39));
        }

        [Test]
        public void OriginalColliderQueriesRetainProjectionFilteringAndWorldGeometry()
        {
            Assert.That(ProjectLucid.ColliderExtensionsVerification.RunManaged(), Is.EqualTo(65));
            Assert.That(ProjectLucid.ColliderExtensionsVerification.RunEngine(), Is.EqualTo(61));
        }

        [Test]
        public void OriginalInstancedPoolsRetainMeshCloningRoutingAndManagerLifecycle()
        {
            Assert.That(ProjectLucid.Editor.InstancedPoolingVerification.RunManaged(), Is.EqualTo(42));
            Assert.That(ProjectLucid.Editor.InstancedPoolingVerification.RunEngine(), Is.EqualTo(27));
        }

        [Test]
        public void OriginalHalfPipeTrajectoryRetainsAuthoredCurveAndUnitySerialization()
        {
            Assert.That(ProjectLucid.HalfPipeTrajectoryVerification.RunManaged(), Is.EqualTo(19));
            Assert.That(ProjectLucid.HalfPipeTrajectoryVerification.RunEngine(), Is.EqualTo(22));
        }

        [Test]
        // Game.Runtime TerrainMetadataDefinition 0x020005cd fields 0x040014b5..0x040014b7;
        // getters 0x06001f65..0x06001f67 (x86-64 0x55adc0/0x55add0/0x55ade0) load offsets
        // 0x18/0x20/0x28 directly. The tooltip, FormerlySerializedAs("metadataType") and
        // CreateAssetMenu strings are attribute arguments in the original global-metadata
        // attribute_data section (offsets 0xb898c8..0xb8992a and 0xb6a88e..0xb6a8ab).
        public void OriginalTerrainMetadataRetainsKeysRawEnumsAndUnitySerialization()
        {
            Assert.That(ProjectLucid.Editor.TerrainMetadataDefinitionVerification.RunManaged(), Is.EqualTo(32));
            Assert.That(ProjectLucid.Editor.TerrainMetadataDefinitionVerification.RunEngine(), Is.EqualTo(16));
        }

        [Test]
        public void OriginalTrackerDefinitionsRetainInheritedKeysCurveAndUnitySerialization()
        {
            Assert.That(ProjectLucid.Editor.TrackerMetadataDefinitionsVerification.RunManaged(), Is.EqualTo(123));
            Assert.That(ProjectLucid.Editor.TrackerMetadataDefinitionsVerification.RunEngine(), Is.EqualTo(58));
        }

        [Test]
        public void OriginalTrackerGroupsRetainTypedKeysComparersAndAuthoredArrays()
        {
            Assert.That(ProjectLucid.Editor.TrackerDefinitionGroupsVerification.RunManaged(), Is.EqualTo(92));
            Assert.That(ProjectLucid.Editor.TrackerDefinitionGroupsVerification.RunEngine(), Is.EqualTo(48));
        }

        [Test]
        public void OriginalUIVisibilityOverridesRetainRegistryRowsAndHandleCallbackOrder()
        {
            Assert.That(UIVisibilityGroupOverriderVerification.RunManaged(), Is.EqualTo(38));
            Assert.That(UIVisibilityGroupOverriderVerification.RunEngine(), Is.EqualTo(9));
        }

        [Test]
        public void OriginalTerrainEffectsRetainAnimationReferencesSquareCacheAndCallbacks()
        {
            Assert.That(TerrainEffectsDefinitionVerification.RunManaged(), Is.EqualTo(31));
            Assert.That(TerrainEffectsDefinitionVerification.RunEngine(), Is.EqualTo(17));
        }

        [Test]
        public void OriginalSplineMetadataRetainsLookupInterpolationAndPartialCacheRules()
        {
            Assert.That(ProjectLucid.Editor.MetadataCollisionVerification.RunManaged(), Is.EqualTo(119));
            Assert.That(ProjectLucid.Editor.MetadataCollisionVerification.RunEngine(), Is.EqualTo(30));
        }

        [Test]
        public void OriginalCharacterSkinDefinitionsRetainRenderWrappersAndOverrideDefaults()
        {
            Assert.That(CharacterSkinVerification.RunAll(), Is.EqualTo(50));
        }

        [Test]
        public void OriginalCharacterArchetypeDefinitionsRetainAtlasWrappersGroupKeysAndCallbacks()
        {
            Assert.That(CharacterArchetypeDefinitionVerification.RunManaged(), Is.EqualTo(60));
            Assert.That(CharacterArchetypeDefinitionVerification.RunEngine(), Is.EqualTo(16));
        }

        [Test]
        public void OriginalFullscreenDefinitionsRetainCurveWeightsLimitsAndTextureOverrides()
        {
            Assert.That(FullscreenDefinitionVerification.RunManaged(), Is.EqualTo(70));
            Assert.That(FullscreenDefinitionVerification.RunEngine(), Is.EqualTo(20));
        }

        [Test]
        public void OriginalCollectableStateAndTierRetainWrappedCountsClampsAndDefaults()
        {
            Assert.That(CollectableStateTierVerification.RunManaged(), Is.EqualTo(66));
        }

        [Test]
        public void OriginalUIBackedDefinitionsRetainAtlasReferencesCacheCountsAndReleaseOrder()
        {
            Assert.That(UIBackedDefinitionVerification.RunManaged(), Is.EqualTo(29));
            Assert.That(UIBackedDefinitionVerification.RunEngine(), Is.EqualTo(27));
        }

        [Test]
        public void OriginalCameraDefinitionsRetainRecenterDefaultsCachedAnglesAndTypedOverrides()
        {
            Assert.That(CharacterCameraDefinitionsVerification.Run(), Is.EqualTo(187));
        }

        [Test]
        public void OriginalRenderAndTargetDefinitionsRetainDefaultsDrawOrderAndMaterialQuirks()
        {
            Assert.That(RenderTargetDefinitionVerification.RunManaged(), Is.EqualTo(39));
            Assert.That(RenderTargetDefinitionVerification.RunEngine(), Is.EqualTo(25));
        }

        [Test]
        public void OriginalSequenceDefinitionsRetainKeysDefaultsAndUnitySerialization()
        {
            Assert.That(SequenceDefinitionVerification.RunManaged(), Is.EqualTo(32));
            Assert.That(SequenceDefinitionVerification.RunEngine(), Is.EqualTo(18));
        }

        [Test]
        // Game.Runtime CollectableChangeData.HasChange 0x06003b0c (x86-64 0x6bd130): true when
        // FixedChange != 0, else cmpneqss on FractionChange, so NaN counts as a change.
        // HazardDefinition .ctor 0x06001d29 (x86-64 0x54d9f0) stores AllowKnockback = 1,
        // both ActorFormType.None hashes 0xb6fa1284, BoostCooldownDurationSeconds 0.5f and
        // ZeroToFullVelocityScale 1f; AbilityTypeDefinition .ctor 0x06001bbc (x86-64
        // 0x5461e0) stores GracePeriod 0x3e4ccccd (0.2f).
        public void OriginalCharacterDamageDefinitionsRetainDefaultsContractsAndCollectableChanges()
        {
            Assert.That(CharacterDamagePrerequisitesVerification.Run(), Is.EqualTo(184));
        }

        [Test]
        public void OriginalProgressionDefinitionsRetainStreaksGuidLookupAndWaypointSettings()
        {
            Assert.That(ProgressionDefinitionVerification.RunManaged(), Is.EqualTo(57));
            Assert.That(ProgressionDefinitionVerification.RunEngine(), Is.EqualTo(26));
        }

        [Test]
        // ApplicationStateEvent.FindByName 0x06000627 (x86-64 0x53b620) tail-calls
        // ObjectUtils.FindByNameSafe 0x06001237 (x86-64 0x984420): first ordinal name match
        // in Resources.FindObjectsOfTypeAll order, which the duplicate-name check observes.
        public void OriginalDataManagerDefinitionsRetainKeysDefaultsAndUnitySerialization()
        {
            Assert.That(DataManagerDefinitionVerification.RunManaged(), Is.EqualTo(32));
            Assert.That(DataManagerDefinitionVerification.RunEngine(), Is.EqualTo(21));
        }

        [Test]
        public void OriginalTouchLayoutsRetainLookupFallbackAndSerializedDefaults()
        {
            Assert.That(TouchLayoutVerification.RunManaged(), Is.EqualTo(36));
            Assert.That(TouchLayoutVerification.RunEngine(), Is.EqualTo(20));
        }

        [Test]
        public void OriginalVisualProxiesRetainRotationGatesAndSpacingOrder()
        {
            Assert.That(ActorVisualProxyVerification.RunManaged(), Is.EqualTo(314));
            Assert.That(ActorVisualProxyVerification.RunEngine(), Is.EqualTo(91));
        }

        [Test]
        public void OriginalBlobShadowRetainsGravityRaycastAndEnableOrder()
        {
            Assert.That(BlobShadowVerification.RunManaged(), Is.EqualTo(21));
            Assert.That(BlobShadowVerification.RunUnityEngine(), Is.EqualTo(24));
        }

        [Test]
        public void OriginalPerformanceProfilesRetainFeatureAndThresholdRules()
        {
            Assert.That(PerformanceStartupVerification.RunManaged(), Is.EqualTo(184));
            Assert.That(PerformanceStartupVerification.RunEngine(), Is.EqualTo(12));
        }

        [Test]
        public void OriginalDeviceProfilesRetainPlatformMatchingAndFallbacks()
        {
            Assert.That(DevicePlatformVerification.RunManaged(), Is.EqualTo(524));
            Assert.That(DevicePlatformVerification.RunEngine(), Is.EqualTo(27));
        }

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
        // Game.Runtime clip constructors 0x060019fe/0x06001a15/0x06001a18 (x86-64 0x53dd00/
        // 0x53e600/0x53e630) only call the ScriptableObject base; HLAudioMixerDefinition
        // .ctor 0x06001a10 (x86-64 0x53e480) stores a SerializableDictionary built with
        // HardlightEnumComparers slot 0x1c8 (HLAudioMixerGroupIdentifierComparer) before the
        // base call; ConvertToDecibels 0x06001a0e (x86-64 0x53e2f0) is log10 * 20 unclamped.
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
            // eleven original analytics helper methods, and Unity's two
            // MonoScript generator methods, plus the twenty methods of the
            // restored ClientDataAPI.Dispatcher (47f078d), plus the nineteen
            // methods of the restored enum-string tables (79928904): ten in
            // Hardlight.HardlightEnumExtensions (nine GetString and the .cctor),
            // three in Hardlight.HardlightEnumExtentions, two each in
            // HardlightProject.HardlightEnumExtentions and Enums.EnumExtentions
            // (GetString and the .cctor), and Enums.EnumComparers' .ctor and .cctor.
            // 638 emitted method rows plus 120 fixed checks in the pinned compiler fixture.
            const int expectedChecks = 758;
            Assert.That(CodecPipelineVerification.Run(), Is.EqualTo(expectedChecks));
            Assert.That(CodecPipelineVerification.Run(), Is.EqualTo(expectedChecks),
                "A repeated verification in the same Editor domain must count its own checks.");
        }

        [Test]
        public void CodecVerificationRetryAfterOutputFailureCountsOnlyItsOwnChecks()
        {
            Assert.That(CodecPipelineVerification.RunAfterOutputFailure(), Is.EqualTo(758),
                "A failed output write must not add its completed checks to the unchanged-input retry.");
        }

        [Test]
        public void OriginalTimeScaledComponentRetainsCallbacksAndTimingPhases()
        {
            Assert.That(TimeScaledComponentVerification.Run(), Is.EqualTo(72));
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
        // Factory/metadata only; the SceneManager unload in DoOnEnter runs in the
        // PlayMode BootSceneUnloadTests.
        public void OriginalBootSceneUnloadRetainsFactoryAndStateMetadata()
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
        public void RecoveredYamlMissingScriptsLeaveTheReferenceAuditIncomplete()
        {
            ScriptReferenceVerification.RunRecoveredYaml();
        }

        [Test]
        public void RecoveredYamlObjectPointersRequireExactLocalOrLoadedTargets()
        {
            ScriptReferenceVerification.RunObjectPointers();
        }

        [Test]
        public void RecoveredYamlLocalPointerSyntaxSkipsStringTextAndRequiresUniqueAnchors()
        {
            ScriptReferenceVerification.RunLocalPointerSyntax();
        }

        [Test]
        public void SuppliedLayoutComponentsRetainAuthoredScalarsAfterScriptBinding()
        {
            UguiLayoutBindingVerification.RunAuthored();
        }

        [Test]
        public void UpstreamLayoutSizingPreservesPriorityFitAndAspectRules()
        {
            UguiLayoutBindingVerification.RunGeometry();
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

        [Test]
        public void ParticleMainCompilesEveryPassForDirect3D()
        {
            ParticleMainVerification.CompileForWindows();
        }

        [Test]
        public void ParticleMainCompilesEveryPassForOpenGL()
        {
            ParticleMainVerification.CompileForLinux();
        }

        [Test]
        public void ParticleMainGpuOutputMatchesDecodedSurfaceCases()
        {
            ParticleMainVerification.Run();
        }

        [Test]
        public void ParticleAnimMeshCompilesEveryPassForDirect3D()
        {
            ParticleAnimMeshVerification.CompileForWindows();
        }

        [Test]
        public void ParticleAnimMeshGpuOutputMatchesDecodedSurfaceCases()
        {
            ParticleAnimMeshVerification.Run();
        }

        [Test]
        public void ParticleAnimMeshCompilesEveryPassForOpenGLCore()
        {
            ParticleAnimMeshVerification.CompileForLinux();
        }

        [Test]
        public void ParticleAnimMeshRestoresDepthTextureWhenVerificationFails()
        {
            ParticleAnimMeshVerification.VerifyDepthTextureRestorationAfterFailure();
        }

        [Test]
        public void RecoveredShaderRigRestoresStateAfterDisposeAndFailedConstruction()
        {
            RecoveredShaderProof.VerifyRigRestoresState();
        }

        [Test]
        public void PfxAlphaBlendAddCompilesEveryPassForDirect3D()
        {
            PfxAlphaBlendAddVerification.CompileForWindows();
        }

        [Test]
        public void PfxAlphaBlendAddGpuOutputMatchesDecodedSurfaceCases()
        {
            PfxAlphaBlendAddVerification.Run();
        }

        [Test]
        public void CharGhostedCompilesEveryPassForDirect3D()
        {
            CharGhostedVerification.CompileForWindows();
        }

        [Test]
        public void CharGhostedGpuOutputMatchesDecodedSurfaceCases()
        {
            CharGhostedVerification.Run();
        }

        [Test]
        public void GameplayCollectableCompilesEveryPassForDirect3D()
        {
            GameplayCollectableVerification.CompileForWindows();
        }

        [Test]
        public void GameplayCollectableGpuOutputMatchesDecodedSurfaceCases()
        {
            GameplayCollectableVerification.Run();
        }

        [Test]
        public void MatcapCompilesEveryPassForDirect3D()
        {
            MatcapVerification.CompileForWindows();
        }

        [Test]
        public void MatcapGpuOutputMatchesDecodedSurfaceCases()
        {
            MatcapVerification.Run();
        }

        [Test]
        public void ParticleTrailCompilesEveryPassForDirect3D()
        {
            ParticleTrailVerification.CompileForWindows();
        }

        [Test]
        public void ParticleTrailGpuOutputMatchesDecodedSurfaceCases()
        {
            ParticleTrailVerification.Run();
        }

        [Test]
        public void PfxAlphaBlendAddCompilesEveryPassForOpenGL()
        {
            PfxAlphaBlendAddVerification.CompileForOpenGL();
        }

        [Test]
        public void PfxAlphaBlendAddPerspectiveFadeRetainsOriginalClipCurve()
        {
            PfxAlphaBlendAddVerification.VerifyPerspectiveFade();
        }

        [Test]
        public void ParticleTrailCompilesEveryPassForOpenGL()
        {
            ParticleTrailVerification.CompileForOpenGL();
        }

        [Test]
        public void ParticleTrailOrthographicDepthRetainsBothRowsAndProjectionSigns()
        {
            ParticleTrailVerification.VerifyOrthographicDepthRows();
        }

        [Test]
        public void UiBlurCompilesEveryPassForDirect3D()
        {
            UiBlurVerification.CompileForWindows();
        }

        [Test]
        public void UiBlurGpuOutputMatchesDecodedSurfaceCases()
        {
            UiBlurVerification.Run();
        }

        [Test]
        public void FullscreenEffectsCompilesEveryPassForDirect3D()
        {
            FullscreenEffectsVerification.CompileForWindows();
        }

        [Test]
        public void FullscreenEffectsGpuOutputMatchesDecodedSurfaceCases()
        {
            FullscreenEffectsVerification.Run();
        }

        [Test]
        public void EnvStandardLitCompilesEveryPassForDirect3D()
        {
            EnvStandardLitVerification.CompileForWindows();
        }

        [Test]
        public void EnvStandardLitGpuOutputMatchesDecodedSurfaceCases()
        {
            EnvStandardLitVerification.Run();
        }

        [Test]
        public void CharStandardCompilesEveryPassForDirect3D()
        {
            CharStandardVerification.CompileForWindows();
        }

        [Test]
        public void CharStandardGpuOutputMatchesDecodedSurfaceCases()
        {
            CharStandardVerification.Run();
        }

        [Test]
        public void EnvZone2GooCompilesEveryPassForDirect3D()
        {
            EnvZone2GooVerification.CompileForWindows();
        }

        [Test]
        public void EnvZone2GooCompilesEveryPassForOpenGL()
        {
            EnvZone2GooVerification.CompileForOpenGL();
        }

        [Test]
        public void EnvZone2GooGpuOutputMatchesDecodedSurfaceCases()
        {
            EnvZone2GooVerification.Run();
        }

        [Test]
        public void EnvStandardTriPlanarCompilesEveryPassForDirect3D()
        {
            EnvStandardTriPlanarVerification.CompileForWindows();
        }

        [Test]
        public void EnvStandardTriPlanarCompilesEveryPassForOpenGL()
        {
            EnvStandardTriPlanarVerification.CompileForOpenGL();
        }

        [Test]
        public void EnvStandardTriPlanarGpuOutputMatchesDecodedSurfaceCases()
        {
            EnvStandardTriPlanarVerification.Run();
        }

        [Test]
        public void EnvZone5TrackCompilesEveryPassForDirect3D()
        {
            EnvZone5TrackVerification.CompileForWindows();
        }

        [Test]
        public void EnvZone5TrackCompilesEveryPassForOpenGL()
        {
            EnvZone5TrackVerification.CompileForOpenGL();
        }

        [Test]
        public void EnvZone5TrackGpuOutputMatchesDecodedSurfaceCases()
        {
            EnvZone5TrackVerification.Run();
        }

        [Test]
        public void EnvStandardUnlitCompilesEveryPassForDirect3D()
        {
            EnvStandardUnlitVerification.CompileForWindows();
        }

        [Test]
        public void EnvStandardUnlitCompilesEveryPassForOpenGL()
        {
            EnvStandardUnlitVerification.CompileForOpenGL();
        }

        [Test]
        public void EnvStandardUnlitGpuOutputMatchesDecodedSurfaceCases()
        {
            EnvStandardUnlitVerification.Run();
        }

        [Test]
        public void HubStandardUnlitCompilesEveryPassForDirect3D()
        {
            HubStandardUnlitVerification.CompileForWindows();
        }

        [Test]
        public void HubStandardUnlitCompilesEveryPassForOpenGL()
        {
            HubStandardUnlitVerification.CompileForOpenGL();
        }

        [Test]
        public void HubStandardUnlitGpuOutputMatchesDecodedSurfaceCases()
        {
            HubStandardUnlitVerification.Run();
        }

        [Test]
        public void SkyboxPortalCompilesEveryPassForDirect3D()
        {
            SkyboxPortalVerification.CompileForWindows();
        }

        [Test]
        public void SkyboxPortalCompilesEveryPassForOpenGL()
        {
            SkyboxPortalVerification.CompileForOpenGL();
        }

        [Test]
        public void SkyboxPortalGpuOutputMatchesDecodedSurfaceCases()
        {
            SkyboxPortalVerification.Run();
        }

        [Test]
        public void GameplayTimedPlatformCompilesEveryPassForDirect3D()
        {
            GameplayTimedPlatformVerification.CompileForWindows();
        }

        [Test]
        public void GameplayTimedPlatformCompilesEveryPassForOpenGL()
        {
            GameplayTimedPlatformVerification.CompileForOpenGL();
        }

        [Test]
        public void GameplayTimedPlatformGpuOutputMatchesDecodedSurfaceCases()
        {
            GameplayTimedPlatformVerification.Run();
        }

        [Test]
        public void CharBoss03BodyBlobsCompilesEveryPassForDirect3D()
        {
            CharBoss03BodyBlobsVerification.CompileForWindows();
        }

        [Test]
        public void CharBoss03BodyBlobsCompilesEveryPassForOpenGL()
        {
            CharBoss03BodyBlobsVerification.CompileForOpenGL();
        }

        [Test]
        public void CharBoss03BodyBlobsGpuOutputMatchesDecodedSurfaceCases()
        {
            CharBoss03BodyBlobsVerification.Run();
        }

        // The shared tables the two enum-string verifiers write through the genuine
        // GetString miss path: three of the ActorDefinitionVerification tables (all
        // of them are checked by ActorVerifiersLeaveTheirCachedEnumTablesAsFound)
        // and the first HardlightEnumStringsVerification row.
        private static readonly string[] EnumVerifierTables =
        {
            "AbilityAimTargetTypeEnumToString", "ActorParticleTriggerTypeEnumToString",
            "ParticleEffectTypeEnumToString", "LevelSetupTypesEnumToString",
        };

        // Each table's dictionary object, then its keys and value references in order.
        private static System.Collections.Generic.List<object> EnumTables() => EnumTables(EnumVerifierTables);

        private static System.Collections.Generic.List<object> EnumTables(string[] names)
        {
            var state = new System.Collections.Generic.List<object>();
            foreach (string name in names)
            {
                var table = (IDictionary)typeof(HardlightProject.HardlightEnumExtensions)
                    .GetField(name, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
                state.Add(table);
                foreach (DictionaryEntry entry in table) { state.Add(entry.Key); state.Add(entry.Value); }
            }
            return state;
        }

        private static void AssertEnumTables(System.Collections.Generic.List<object> before, string step) =>
            AssertEnumTables(before, EnumVerifierTables, step);

        private static void AssertEnumTables(System.Collections.Generic.List<object> before, string[] names, string step)
        {
            System.Collections.Generic.List<object> after = EnumTables(names);
            Assert.That(after.Count, Is.EqualTo(before.Count), step + ": table entry count");
            for (int i = 0; i < before.Count; ++i)
                Assert.That(before[i] is System.Enum ? Equals(before[i], after[i]) : ReferenceEquals(before[i], after[i]), Is.True, step + ": entry " + i);
        }

        // Before their leases, ActorDefinitionVerification grew three tables by one
        // cached miss each and a later HardlightEnumStringsVerification run failed at
        // 'native initializer entry count'. Both orders, each twice.
        [Test]
        public void EnumStringVerifiersLeaveTheNativeTablesAsFoundInEitherOrder()
        {
            System.Collections.Generic.List<object> before = EnumTables();
            Assert.That(ActorDefinitionVerification.RunManaged(), Is.EqualTo(100));
            AssertEnumTables(before, "actor first");
            HardlightEnumStringsVerification.Run();
            AssertEnumTables(before, "enum strings after actor");
            HardlightEnumStringsVerification.Run();
            AssertEnumTables(before, "enum strings twice");
            Assert.That(ActorDefinitionVerification.RunManaged(), Is.EqualTo(100));
            AssertEnumTables(before, "actor after enum strings");
        }

        // Native run19 (4beb125) recorded the actor-definition and actor-animation
        // fixtures growing these tables through the original serialization callbacks'
        // GetString misses, in both their managed and engine runs. Each run, twice,
        // must leave every table's object, keys and value references as found.
        private static readonly string[] ActorVerifierTables =
        {
            "ActorAnimationTypeEnumToString", "FullscreenShaderParametersTypeEnumToString",
            "HLAudioClipIdentifierEnumToString", "LevelSetupTypesEnumToString", "ActorAudioTypesEnumToString",
            "ActorParticleTriggerTypeEnumToString", "ParticleEffectTypeEnumToString",
        };

        [Test]
        public void ActorVerifiersLeaveTheirCachedEnumTablesAsFound()
        {
            System.Collections.Generic.List<object> before = EnumTables(ActorVerifierTables);
            for (int pass = 0; pass < 2; ++pass)
            {
                Assert.That(ActorDefinitionVerification.RunManaged(), Is.EqualTo(100));
                AssertEnumTables(before, ActorVerifierTables, "actor definition managed " + pass);
                Assert.That(ActorDefinitionVerification.RunEngine(), Is.EqualTo(38));
                AssertEnumTables(before, ActorVerifierTables, "actor definition engine " + pass);
                Assert.That(ActorAnimationVerification.RunManaged(), Is.EqualTo(358));
                AssertEnumTables(before, ActorVerifierTables, "actor animation managed " + pass);
                Assert.That(ActorAnimationVerification.RunEngine(), Is.EqualTo(23));
                AssertEnumTables(before, ActorVerifierTables, "actor animation engine " + pass);
            }
        }

        // A failure at the end of each newly leased body still returns every table to
        // its admitted entries. The fault callback first caches its own genuine
        // GetString miss in a table that body leases, so the restore is observable
        // even where the fixture's own keys were already admitted.
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ActorVerifierFailuresStillRestoreTheirCachedEnumTables(int verifier)
        {
            System.Collections.Generic.List<object> before = EnumTables(ActorVerifierTables);
            var primary = new System.InvalidProgramException("injected after a cached miss");
            string site = verifier == 0 ? ActorDefinitionVerification.EngineChecksDoneSite
                : verifier == 1 ? ActorAnimationVerification.ManagedChecksDoneSite
                : ActorAnimationVerification.EngineChecksDoneSite;
            string tableName = verifier == 0 ? "HLAudioClipIdentifierEnumToString" : "ActorAnimationTypeEnumToString";
            var table = (IDictionary)typeof(HardlightProject.HardlightEnumExtensions)
                .GetField(tableName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
            object missKey = verifier == 0 ? (object)(HardlightProject.HLAudioClipIdentifier)int.MinValue : (HardlightProject.ActorAnimationType)int.MinValue;
            Assert.That(table.Contains(missKey), Is.False, "control key is absent before the run");
            bool cachedInsideLease = false;
            VerificationFixtureOwnership.FaultInjection = reached =>
            {
                if (reached != site) return;
                if (verifier == 0) HardlightProject.HardlightEnumExtensions.GetString((HardlightProject.HLAudioClipIdentifier)missKey);
                else HardlightProject.HardlightEnumExtensions.GetString((HardlightProject.ActorAnimationType)missKey);
                cachedInsideLease = table.Contains(missKey);
                throw primary;
            };
            try
            {
                var error = Assert.Throws<System.InvalidProgramException>(() =>
                {
                    if (verifier == 0) ActorDefinitionVerification.RunEngine();
                    else if (verifier == 1) ActorAnimationVerification.RunManaged();
                    else ActorAnimationVerification.RunEngine();
                });
                Assert.That(error, Is.SameAs(primary));
            }
            finally
            {
                VerificationFixtureOwnership.FaultInjection = null;
            }
            Assert.That(cachedInsideLease, Is.True, "the control's miss was cached inside the lease");
            AssertEnumTables(before, ActorVerifierTables, "after the injected failure");
        }

        // A failure after a miss is cached still returns every table to its admitted
        // entries, so the next enum-string run sees the native tables.
        [TestCase(0)]
        [TestCase(1)]
        public void EnumStringVerifierFailuresStillRestoreTheNativeTables(int verifier)
        {
            System.Collections.Generic.List<object> before = EnumTables();
            var primary = new System.InvalidProgramException("injected after a cached miss");
            string site = verifier == 0 ? HardlightEnumStringsVerification.UnknownMissCachedSite : ActorDefinitionVerification.ParticleDisplayCachedSite;
            VerificationFixtureOwnership.FaultInjection = reached => { if (reached == site) throw primary; };
            try
            {
                var error = Assert.Throws<System.InvalidProgramException>(() =>
                {
                    if (verifier == 0) HardlightEnumStringsVerification.Run();
                    else ActorDefinitionVerification.RunManaged();
                });
                Assert.That(error, Is.SameAs(primary));
            }
            finally
            {
                VerificationFixtureOwnership.FaultInjection = null;
            }
            AssertEnumTables(before, "after the injected failure");
            Assert.DoesNotThrow(HardlightEnumStringsVerification.Run);
        }
    }
}
