using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Game.Runtime 0x020000ab. Full 67-key initialization from 0x06000591.
    // SceneOpKey deliberately retains the original enum string-registry dependency.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public static class AppFSMKeys
    {
        // Original field 0x0400034a; native static offset 0x0.
        public static readonly GraphStorageKey IsFastLoading = new GraphStorageKey("IsFastLoading", 0, 0);
        // Original field 0x0400034b; native static offset 0x10.
        public static readonly GraphStorageKey SceneOpKey = new GraphStorageKey(HardlightEnumExtensions.GetString(ApplicationSettings.SceneOperation), 0, 0);
        // Original field 0x0400034c; native static offset 0x20.
        public static readonly GraphStorageKey PauseTimeOverride = new GraphStorageKey("PauseTimeOverride", 0, 0);
        // Original field 0x0400034d; native static offset 0x30.
        public static readonly GraphStorageKey GameSaveLoaded = new GraphStorageKey("GameSaveLoaded", 0, 0);
        // Original field 0x0400034e; native static offset 0x40.
        public static readonly GraphStorageKey GameSettings = new GraphStorageKey("GameSettings", 0, 0);
        // Original field 0x0400034f; native static offset 0x50.
        public static readonly GraphStorageKey ControlsEditMode = new GraphStorageKey("ControlsEditMode", 0, 0);
        // Original field 0x04000350; native static offset 0x60.
        public static readonly GraphStorageKey SceneLoadQueued = new GraphStorageKey("SceneLoadQueued", 0, 0);
        // Original field 0x04000351; native static offset 0x70.
        public static readonly GraphStorageKey GameCenterAccountChanged = new GraphStorageKey("GameCenterAccountChanged", 0, 0);
        // Original field 0x04000352; native static offset 0x80.
        public static readonly GraphStorageKey CloudSaveChanged = new GraphStorageKey("CloudSaveChanged", 0, 0);
        // Original field 0x04000353; native static offset 0x90.
        public static readonly GraphStorageKey NewAppVersionRequired = new GraphStorageKey("NewAppVersionRequired", 0, 0);
        // Original field 0x04000354; native static offset 0xa0.
        public static readonly GraphStorageKey CurrentCharacterId = new GraphStorageKey("CurrentCharacterId", 0, 0);
        // Original field 0x04000355; native static offset 0xb0.
        public static readonly GraphStorageKey CurrentCharacterPausePosition = new GraphStorageKey("CurrentCharacterPausePosition", 0, 0);
        // Original field 0x04000356; native static offset 0xc0.
        public static readonly GraphStorageKey CurrentCharacterPauseVelocity = new GraphStorageKey("CurrentCharacterPauseVelocity", 0, 0);
        // Original field 0x04000357; native static offset 0xd0.
        public static readonly GraphStorageKey CharacterLastUsed = new GraphStorageKey("CharacterLastUsed", 0, 0);
        // Original field 0x04000358; native static offset 0xe0.
        public static readonly GraphStorageKey CharacterSelectionPreference = new GraphStorageKey("CharacterSelectionPreference", 0, 0);
        // Original field 0x04000359; native static offset 0xf0.
        public static readonly GraphStorageKey MetaGameState = new GraphStorageKey("MetaGameState", 0, 0);
        // Original field 0x0400035a; native static offset 0x100.
        public static readonly GraphStorageKey MetaGameActive = new GraphStorageKey("MetaGameActive", 0, 0);
        // Original field 0x0400035b; native static offset 0x110.
        public static readonly GraphStorageKey MetaGameUnlocks = new GraphStorageKey("MetaGameUnlocks", 0, 0);
        // Original field 0x0400035c; native static offset 0x120.
        public static readonly GraphStorageKey MetaGameUnlocksShown = new GraphStorageKey("MetaGameUnlocksShown", 0, 0);
        // Original field 0x0400035d; native static offset 0x130.
        public static readonly GraphStorageKey FirstTimeBossEntry = new GraphStorageKey("FirstTimeBossEntry", 0, 0);
        // Original field 0x0400035e; native static offset 0x140.
        public static readonly GraphStorageKey LastLevelUIVisitedGUID = new GraphStorageKey("LastLevelUIVisitedGUID", 0, 0);
        // Original field 0x0400035f; native static offset 0x150.
        public static readonly GraphStorageKey LastLevelUISelectedGUID = new GraphStorageKey("LastLevelUISelectedGUID", 0, 0);
        // Original field 0x04000360; native static offset 0x160.
        public static readonly GraphStorageKey LastLevelLoadedGUID = new GraphStorageKey("LastLevelLoadedGUID", 0, 0);
        // Original field 0x04000361; native static offset 0x170.
        public static readonly GraphStorageKey IsInPostGame = new GraphStorageKey("IsInPostGame", 0, 0);
        // Original field 0x04000362; native static offset 0x180.
        public static readonly GraphStorageKey GameplayActive = new GraphStorageKey("GameplayActive", 0, 0);
        // Original field 0x04000363; native static offset 0x190.
        public static readonly GraphStorageKey GameplayControlActive = new GraphStorageKey("GameplayControlActive", 0, 0);
        // Original field 0x04000364; native static offset 0x1a0.
        public static readonly GraphStorageKey LoadingParameters = new GraphStorageKey("LoadingParameters", 0, 0);
        // Original field 0x04000365; native static offset 0x1b0.
        public static readonly GraphStorageKey LevelLoadParameters = new GraphStorageKey("LevelLoadParameters", 0, 0);
        // Original field 0x04000366; native static offset 0x1c0.
        public static readonly GraphStorageKey LevelUnloadOperations = new GraphStorageKey("LevelUnloadOperations", 0, 0);
        // Original field 0x04000367; native static offset 0x1d0.
        public static readonly GraphStorageKey LevelPlayed = new GraphStorageKey("LevelPlayed", 0, 0);
        // Original field 0x04000368; native static offset 0x1e0.
        public static readonly GraphStorageKey LevelUnlocked = new GraphStorageKey("LevelUnlocked", 0, 0);
        // Original field 0x04000369; native static offset 0x1f0.
        public static readonly GraphStorageKey RestartInProgress = new GraphStorageKey("RestartInProgress", 0, 0);
        // Original field 0x0400036a; native static offset 0x200.
        public static readonly GraphStorageKey SkipOutroSequence = new GraphStorageKey("SkipOutroSequence", 0, 0);
        // Original field 0x0400036b; native static offset 0x210.
        public static readonly GraphStorageKey ShowFailureScreen = new GraphStorageKey("ShowFailureScreen", 0, 0);
        // Original field 0x0400036c; native static offset 0x220.
        public static readonly GraphStorageKey FailureScreenStringOverride = new GraphStorageKey("FailureScreenStringOverride", 0, 0);
        // Original field 0x0400036d; native static offset 0x230.
        public static readonly GraphStorageKey LoadedLevels = new GraphStorageKey("LoadedLevels", 0, 0);
        // Original field 0x0400036e; native static offset 0x240.
        public static readonly GraphStorageKey LoadingProgress = new GraphStorageKey("LoadingProgress", 0, 0);
        // Original field 0x0400036f; native static offset 0x250.
        public static readonly GraphStorageKey MissionIntroList = new GraphStorageKey("MissionIntroList", 0, 0);
        // Original field 0x04000370; native static offset 0x260.
        public static readonly GraphStorageKey MissionCharacterId = new GraphStorageKey("MissionCharacterId", 0, 0);
        // Original field 0x04000371; native static offset 0x270.
        public static readonly GraphStorageKey StateEvents = new GraphStorageKey("StateEvents", 0, 0);
        // Original field 0x04000372; native static offset 0x280.
        public static readonly GraphStorageKey UIModernEvents = new GraphStorageKey("UIModernEvents", 0, 0);
        // Original field 0x04000373; native static offset 0x290.
        public static readonly GraphStorageKey UIContainerParameters = new GraphStorageKey("UIContainerParameters", 0, 0);
        // Original field 0x04000374; native static offset 0x2a0.
        public static readonly GraphStorageKey UIContainerIdentifierOverride = new GraphStorageKey("UIContainerIdentifierOverride", 0, 0);
        // Original field 0x04000375; native static offset 0x2b0.
        public static readonly GraphStorageKey UIHUDFocusMode = new GraphStorageKey("UIHUDFocusMode", 0, 0);
        // Original field 0x04000376; native static offset 0x2c0.
        public static readonly GraphStorageKey OnPauseCloseEvent = new GraphStorageKey("OnPauseCloseEvent", 0, 0);
        // Original field 0x04000377; native static offset 0x2d0.
        public static readonly GraphStorageKey ReplayMode = new GraphStorageKey("ReplayMode", 0, 0);
        // Original field 0x04000378; native static offset 0x2e0.
        public static readonly GraphStorageKey InputAbilitySteeringAssistDisabled = new GraphStorageKey("InputAbilitySteeringAssistDisabled", 0, 0);
        // Original field 0x04000379; native static offset 0x2f0.
        public static readonly GraphStorageKey TransitionMidpointWaitHandles = new GraphStorageKey("TransitionMidpointWaitHandles", 0, 0);
        // Original field 0x0400037a; native static offset 0x300.
        public static readonly GraphStorageKey TransitionFadeParameters = new GraphStorageKey("TransitionFadeParameters", 0, 0);
        // Original field 0x0400037b; native static offset 0x310.
        public static readonly GraphStorageKey IntroSequenceHandle = new GraphStorageKey("IntroSequenceHandle", 0, 0);
        // Original field 0x0400037c; native static offset 0x320.
        public static readonly GraphStorageKey OutroSequenceHandle = new GraphStorageKey("OutroSequenceHandle", 0, 0);
        // Original field 0x0400037d; native static offset 0x330.
        public static readonly GraphStorageKey OutroGameInputDisabledHandle = new GraphStorageKey("OutroGameInputDisabledHandle", 0, 0);
        // Original field 0x0400037e; native static offset 0x340.
        public static readonly GraphStorageKey BootHLPropertyStoreList = new GraphStorageKey("BootHLPropertyStoreList", 0, 0);
        // Original field 0x0400037f; native static offset 0x350.
        public static readonly GraphStorageKey FontsFallbackPreloadReady = new GraphStorageKey("FontsFallbackPreloadReady", 0, 0);
        // Original field 0x04000380; native static offset 0x360.
        public static readonly GraphStorageKey FontsChangeLanguageReady = new GraphStorageKey("FontsChangeLanguageReady", 0, 0);
        // Original field 0x04000381; native static offset 0x370.
        public static readonly GraphStorageKey CutsceneQueued = new GraphStorageKey("CutsceneQueued", 0, 0);
        // Original field 0x04000382; native static offset 0x380.
        public static readonly GraphStorageKey CutsceneTheatre = new GraphStorageKey("CutsceneTheatre", 0, 0);
        // Original field 0x04000383; native static offset 0x390.
        public static readonly GraphStorageKey CutsceneSkipEndGame = new GraphStorageKey("CutsceneSkipEndGame", 0, 0);
        // Original field 0x04000384; native static offset 0x3a0.
        public static readonly GraphStorageKey ChallengesActive = new GraphStorageKey("ChallengesActive", 0, 0);
        // Original field 0x04000385; native static offset 0x3b0.
        public static readonly GraphStorageKey ChallengesRewardTrackType = new GraphStorageKey("ChallengesRewardTrackType", 0, 0);
        // Original field 0x04000386; native static offset 0x3c0.
        public static readonly GraphStorageKey SelectShadowFTUEChallenge = new GraphStorageKey("SelectShadowFTUEChallenge", 0, 0);
        // Original field 0x04000387; native static offset 0x3d0.
        public static readonly GraphStorageKey LastSeenXPValue = new GraphStorageKey("LastSeenXPValue", 0, 0);
        // Original field 0x04000388; native static offset 0x3e0.
        public static readonly GraphStorageKey BonusZoneActive = new GraphStorageKey("BonusZoneActive", 0, 0);
        // Original field 0x04000389; native static offset 0x3f0.
        public static readonly GraphStorageKey StartIsReady = new GraphStorageKey("StartIsReady", 0, 0);
        // Original field 0x0400038a; native static offset 0x400.
        public static readonly GraphStorageKey CollectionsDreamPowerInfoReady = new GraphStorageKey("CollectionsDreamPowerInfoReady", 0, 0);
        // Original field 0x0400038b; native static offset 0x410.
        public static readonly GraphStorageKey CollectionsTabLastOpened = new GraphStorageKey("CollectionsTabLastOpened", 0, 0);
        // Original field 0x0400038c; native static offset 0x420.
        public static readonly GraphStorageKey CollectionsUnlockInProgress = new GraphStorageKey("CollectionsUnlockInProgress", 0, 0);
    }
}
