using HardlightProject;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight.Analytics
{
    // Original Game.Runtime0200001d; original automatic callbacks are retained
    // through the two lambdas. Their emitted layout still requires comparison.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public static class AnalyticsEventCollector
    {
        private const int ManualAnalyticsQueue = 1;
        private const string XPCurrencyType = "xp";
        private const string BlueCoinCurrencyType = "blue_coins";
        private const string ResourceChangeReasonMissions = "missions";
        private const string ResourceChangeReasonRewardTrack = "reward_track";
        private const string ResourceChangeReasonDreamPowerUnlock = "dream_power_unlock";
        private const string Unknown = "unknown";
        private static readonly SystemRef<DataManager> s_dataManagerRef = ProcessManager.GetSystemRef<DataManager>();

        //0600006d: original startup registers the OnClick callback and ignores
        // the returned subscription handle.
        static AnalyticsEventCollector()
        {
            var exchange = ProcessManager.GetSystemAutoCreate<MessageExchangeBoundCallbackArg<UIModernMessage>>();
            var message = new UIModernMessage(UIModernEventType.OnClick);
            exchange.SubscribeToMessage(in message,
                new MessageCallback<UIModernEvent>((in UIModernEvent msgId) => ButtonPressedEvent(msgId)));
        }

        //0600006e
        public static void AppStartEvent()
        {
            var analyticsEvent = AppStart.Create();
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //0600006f: the original Medium heading mode is reported as Auto.
        public static void SessionStartEvent(int sessionNumber, bool cameraInversion, CameraRecenterHeadingType cameraMode, string installDate)
        {
            var analyticsEvent = SessionStart.Create();
            analyticsEvent.SessionNumber = sessionNumber;
            analyticsEvent.CameraInverted = cameraInversion;
            analyticsEvent.CameraMode = cameraMode == CameraRecenterHeadingType.Medium ? "Auto" : "Manual";
            analyticsEvent.AppInstallDate = installDate;
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000070
        public static void SessionEndEvent(int sessionNumber, long sessionLength)
        {
            var analyticsEvent = SessionEnd.Create();
            analyticsEvent.SessionNumber = sessionNumber;
            analyticsEvent.SessionLength = sessionLength;
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000071
        public static void FTUEEvent(string stepName, string stepId, string userInput = "")
        {
            var analyticsEvent = FTUE.Create();
            analyticsEvent.StepName = stepName;
            analyticsEvent.StepID = stepId;
            analyticsEvent.UserInput = userInput;
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000072: tracking is checked before allocating the event.
        public static void ButtonPressedEvent(UIModernEvent buttonEvent)
        {
            if (!s_dataManagerRef.TryGet(out var dataManager)) return;
            if (!dataManager.AnalyticsConfiguration.IsButtonTracked(buttonEvent)) return;
            var analyticsEvent = ButtonPressed.Create();
            analyticsEvent.ButtonName = buttonEvent.name;
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000073: event construction precedes payload dereferences.
        public static void MissionStartEvent(AnalyticsMissionPayload payload)
        {
            var analyticsEvent = MissionStart.Create();
            var level = payload.MissionContext.LevelDefinition;
            var mission = payload.MissionState.Definition;
            analyticsEvent.ZoneName = Hardlight.Enums.EnumExtentions.GetString(level.DisplayName);
            analyticsEvent.ZoneIndex = HardlightProject.HardlightEnumExtensions.GetString(level.LevelSetupType);
            analyticsEvent.Act = Hardlight.Enums.EnumExtentions.GetString(level.ActName);
            analyticsEvent.MissionName = Hardlight.Enums.EnumExtentions.GetString(mission.MissionName);
            analyticsEvent.MissionIndex = payload.MissionContext.CalculateMissionIndex(mission, level);
            analyticsEvent.Character = HardlightProject.HardlightEnumExtensions.GetString(payload.Character.IdType);
            analyticsEvent.RsrTotal = CalculateSubObjectiveProgress(MissionType.RedStarRings, payload.MissionContext, mission, level.GetGUID());
            analyticsEvent.BlueCoinsTotal = CalculateSubObjectiveProgress(MissionType.BlueCoins, payload.MissionContext, mission, level.GetGUID());
            analyticsEvent.MusicNotesTotal = CalculateSubObjectiveProgress(MissionType.Jukebox, payload.MissionContext, mission, level.GetGUID());
            analyticsEvent.MissionType = Hardlight.Analytics.HardlightEnumExtensions.GetString(payload.MissionContext.CalculateAnalyticsMissionType(mission, level));
            analyticsEvent.IsReplay = payload.MissionContext.IsReplay;
            analyticsEvent.Attempts = payload.MissionState.Attempts;
            analyticsEvent.DreamPowerSlots = payload.GetDreamPowerSlotsAnalytics();
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000074: event construction precedes payload dereferences.
        public static void MissionCompleteEvent(AnalyticsMissionPayload payload)
        {
            var analyticsEvent = MissionComplete.Create();
            var level = payload.MissionContext.LevelDefinition;
            var mission = payload.MissionState.Definition;
            analyticsEvent.ZoneName = Hardlight.Enums.EnumExtentions.GetString(level.DisplayName);
            analyticsEvent.ZoneIndex = HardlightProject.HardlightEnumExtensions.GetString(level.LevelSetupType);
            analyticsEvent.Act = Hardlight.Enums.EnumExtentions.GetString(level.ActName);
            analyticsEvent.MissionName = Hardlight.Enums.EnumExtentions.GetString(mission.MissionName);
            analyticsEvent.MissionIndex = payload.MissionContext.CalculateMissionIndex(mission, level);
            analyticsEvent.Character = HardlightProject.HardlightEnumExtensions.GetString(payload.Character.IdType);
            analyticsEvent.DeathCount = payload.MissionState.DeathCount;
            // Original conversion has no milliseconds scaling. Exceptional
            // float-to-long results differ between the shipped architectures.
            analyticsEvent.MissionTime = unchecked((long)payload.MissionContext.RealTimeTaken);
            analyticsEvent.RsrTotal = CalculateSubObjectiveProgress(MissionType.RedStarRings, payload.MissionContext, mission, level.GetGUID());
            analyticsEvent.BlueCoinsTotal = CalculateSubObjectiveProgress(MissionType.BlueCoins, payload.MissionContext, mission, level.GetGUID());
            analyticsEvent.MusicNotesTotal = CalculateSubObjectiveProgress(MissionType.Jukebox, payload.MissionContext, mission, level.GetGUID());
            analyticsEvent.MissionType = Hardlight.Analytics.HardlightEnumExtensions.GetString(payload.MissionContext.CalculateAnalyticsMissionType(mission, level));
            analyticsEvent.IsReplay = payload.MissionContext.IsReplay;
            analyticsEvent.Attempts = payload.MissionState.Attempts;
            analyticsEvent.DreamPowerSlots = payload.GetDreamPowerSlotsAnalytics();
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000075: event construction precedes payload dereferences.
        public static void MissionFailedEvent(AnalyticsMissionPayload payload)
        {
            var analyticsEvent = MissionFailed.Create();
            var level = payload.MissionContext.LevelDefinition;
            var mission = payload.MissionState.Definition;
            analyticsEvent.ZoneName = Hardlight.Enums.EnumExtentions.GetString(level.DisplayName);
            analyticsEvent.ZoneIndex = HardlightProject.HardlightEnumExtensions.GetString(level.LevelSetupType);
            analyticsEvent.Act = Hardlight.Enums.EnumExtentions.GetString(level.ActName);
            analyticsEvent.MissionName = Hardlight.Enums.EnumExtentions.GetString(mission.MissionName);
            analyticsEvent.MissionIndex = payload.MissionContext.CalculateMissionIndex(mission, level);
            analyticsEvent.Character = HardlightProject.HardlightEnumExtensions.GetString(payload.Character.IdType);
            analyticsEvent.DeathCount = payload.MissionState.DeathCount;
            // Original conversion has no milliseconds scaling. Exceptional
            // float-to-long results differ between the shipped architectures.
            analyticsEvent.MissionTime = unchecked((long)payload.MissionContext.RealTimeTaken);
            analyticsEvent.RsrTotal = CalculateSubObjectiveProgress(MissionType.RedStarRings, payload.MissionContext, mission, level.GetGUID());
            analyticsEvent.BlueCoinsTotal = CalculateSubObjectiveProgress(MissionType.BlueCoins, payload.MissionContext, mission, level.GetGUID());
            analyticsEvent.MissionType = Hardlight.Analytics.HardlightEnumExtensions.GetString(payload.MissionContext.CalculateAnalyticsMissionType(mission, level));
            analyticsEvent.IsReplay = payload.MissionContext.IsReplay;
            analyticsEvent.Attempts = payload.MissionState.Attempts;
            analyticsEvent.DreamPowerSlots = payload.GetDreamPowerSlotsAnalytics();
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000076: event construction precedes payload dereferences.
        public static void MissionQuitEvent(AnalyticsMissionPayload payload)
        {
            var analyticsEvent = MissionQuit.Create();
            var level = payload.MissionContext.LevelDefinition;
            var mission = payload.MissionState.Definition;
            analyticsEvent.ZoneName = Hardlight.Enums.EnumExtentions.GetString(level.DisplayName);
            analyticsEvent.ZoneIndex = HardlightProject.HardlightEnumExtensions.GetString(level.LevelSetupType);
            analyticsEvent.Act = Hardlight.Enums.EnumExtentions.GetString(level.ActName);
            analyticsEvent.MissionName = Hardlight.Enums.EnumExtentions.GetString(mission.MissionName);
            analyticsEvent.MissionIndex = payload.MissionContext.CalculateMissionIndex(mission, level);
            analyticsEvent.Character = HardlightProject.HardlightEnumExtensions.GetString(payload.Character.IdType);
            analyticsEvent.DeathCount = payload.MissionState.DeathCount;
            // Original conversion has no milliseconds scaling. Exceptional
            // float-to-long results differ between the shipped architectures.
            analyticsEvent.MissionTime = unchecked((long)payload.MissionContext.RealTimeTaken);
            analyticsEvent.RsrTotal = CalculateSubObjectiveProgress(MissionType.RedStarRings, payload.MissionContext, mission, level.GetGUID());
            analyticsEvent.BlueCoinsTotal = CalculateSubObjectiveProgress(MissionType.BlueCoins, payload.MissionContext, mission, level.GetGUID());
            analyticsEvent.MissionType = Hardlight.Analytics.HardlightEnumExtensions.GetString(payload.MissionContext.CalculateAnalyticsMissionType(mission, level));
            analyticsEvent.IsReplay = payload.MissionContext.IsReplay;
            analyticsEvent.Attempts = payload.MissionState.Attempts;
            analyticsEvent.DreamPowerSlots = payload.GetDreamPowerSlotsAnalytics();
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000077
        public static void ChallengeStartEvent(AnalyticsChallengePayload payload)
        {
            var analyticsEvent = ChallengeStart.Create();
            var mission = payload.ChallengeContext.MissionDefinition;
            analyticsEvent.ChallengeName = Hardlight.Enums.EnumExtentions.GetString(mission.MissionName);
            analyticsEvent.ChallengeID = mission.GetGUID();
            analyticsEvent.ChallengeIndex = payload.ChallengeContext.ChallengeIndex;
            analyticsEvent.ChallengeSetIndex = payload.ChallengeContext.ChallengeSetIndex;
            analyticsEvent.IsReplay = payload.ChallengeContext.IsReplay;
            analyticsEvent.Attempts = payload.ChallengeState.Attempts;
            analyticsEvent.DreamPowerSlots = payload.GetDreamPowerSlotsAnalytics();
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000078
        public static void ChallengeCompleteEvent(AnalyticsChallengePayload payload)
        {
            var analyticsEvent = ChallengeComplete.Create();
            var mission = payload.ChallengeContext.MissionDefinition;
            analyticsEvent.ChallengeName = Hardlight.Enums.EnumExtentions.GetString(mission.MissionName);
            analyticsEvent.ChallengeID = mission.GetGUID();
            analyticsEvent.ChallengeIndex = payload.ChallengeContext.ChallengeIndex;
            analyticsEvent.ChallengeSetIndex = payload.ChallengeContext.ChallengeSetIndex;
            analyticsEvent.XpGained = payload.ChallengeState.ChallengeDefinition.XPReward;
            analyticsEvent.BonusXpGained = payload.ChallengeState.BonusXPAwarded;
            analyticsEvent.XpTotal = payload.TotalXp;
            analyticsEvent.Score = payload.Score;
            analyticsEvent.BestScore = payload.ChallengeState.BestScore;
            analyticsEvent.TotalScore = payload.TotalScore;
            analyticsEvent.ChallengeTime = payload.TimeElapsedSeconds;
            analyticsEvent.BestTime = payload.ChallengeState.BestTimeSeconds;
            analyticsEvent.IsReplay = payload.ChallengeContext.IsReplay;
            analyticsEvent.Attempts = payload.ChallengeState.Attempts;
            analyticsEvent.DreamPowerSlots = payload.GetDreamPowerSlotsAnalytics();
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000079
        public static void ChallengeFailedEvent(AnalyticsChallengePayload payload)
        {
            var analyticsEvent = ChallengeFailed.Create();
            var mission = payload.ChallengeContext.MissionDefinition;
            analyticsEvent.ChallengeName = Hardlight.Enums.EnumExtentions.GetString(mission.MissionName);
            analyticsEvent.ChallengeID = mission.GetGUID();
            analyticsEvent.ChallengeIndex = payload.ChallengeContext.ChallengeIndex;
            analyticsEvent.ChallengeSetIndex = payload.ChallengeContext.ChallengeSetIndex;
            analyticsEvent.IsReplay = payload.ChallengeContext.IsReplay;
            analyticsEvent.Attempts = payload.ChallengeState.Attempts;
            analyticsEvent.DreamPowerSlots = payload.GetDreamPowerSlotsAnalytics();
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //0600007a
        public static void ChallengeQuitEvent(AnalyticsChallengePayload payload)
        {
            var analyticsEvent = ChallengeQuit.Create();
            var mission = payload.ChallengeContext.MissionDefinition;
            analyticsEvent.ChallengeName = Hardlight.Enums.EnumExtentions.GetString(mission.MissionName);
            analyticsEvent.ChallengeID = mission.GetGUID();
            analyticsEvent.ChallengeIndex = payload.ChallengeContext.ChallengeIndex;
            analyticsEvent.ChallengeSetIndex = payload.ChallengeContext.ChallengeSetIndex;
            analyticsEvent.IsReplay = payload.ChallengeContext.IsReplay;
            analyticsEvent.Attempts = payload.ChallengeState.Attempts;
            analyticsEvent.DreamPowerSlots = payload.GetDreamPowerSlotsAnalytics();
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //0600007b
        public static void ZoneUnlockedEvent(GameplayLevelDefinition unlockedLevel, long playTimeMs)
        {
            var analyticsEvent = ZoneUnlocked.Create();
            analyticsEvent.ZoneName = Hardlight.Enums.EnumExtentions.GetString(unlockedLevel.DisplayName);
            analyticsEvent.ZoneIndex = HardlightProject.HardlightEnumExtensions.GetString(unlockedLevel.LevelSetupType);
            analyticsEvent.Act = Hardlight.Enums.EnumExtentions.GetString(unlockedLevel.ActName);
            analyticsEvent.TotalPlayTime = playTimeMs;
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //0600007c
        public static void SettingChangedEvent(string settingName, string oldSetting, string newSetting)
        {
            var analyticsEvent = SettingChanged.Create();
            analyticsEvent.SettingName = settingName;
            analyticsEvent.OldValue = oldSetting;
            analyticsEvent.NewValue = newSetting;
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //0600007d
        public static void CutsceneWatchedEvent(Cutscene cutscene)
        {
            var analyticsEvent = CutsceneWatched.Create();
            analyticsEvent.CutsceneName = HardlightProject.HardlightEnumExtensions.GetString(cutscene.Definition.Identifier);
            analyticsEvent.CutsceneSkipped = cutscene.WasSkipped;
            analyticsEvent.WatchTime = cutscene.TimeSpentPlaying;
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //0600007e: even an unknown category is queued after logging its error.
        public static void StatueCollectedEvent(OrnamentDefinition ornament, MissionDefinition missionDefinition = null, GameplayLevelDefinition levelDefinition = null, int xpThreshold = 0)
        {
            var analyticsEvent = StatueCollected.Create();
            analyticsEvent.StatueName = HardlightProject.HardlightEnumExtensions.GetString(ornament.OrnamentIdentifier);
            analyticsEvent.Source = HardlightProject.HardlightEnumExtensions.GetString(ornament.OrnamentCategory);
            analyticsEvent.XpThreshold = 0;
            switch (ornament.OrnamentCategory)
            {
                case OrnamentCategory.Challenge:
                    analyticsEvent.XpThreshold = xpThreshold;
                    break;
                case OrnamentCategory.Achievement:
                    break;
                case OrnamentCategory.Story:
                    if (missionDefinition != null && levelDefinition != null)
                    {
                        analyticsEvent.ZoneName = Hardlight.Enums.EnumExtentions.GetString(levelDefinition.DisplayName);
                        analyticsEvent.ZoneIndex = HardlightProject.HardlightEnumExtensions.GetString(levelDefinition.LevelSetupType);
                        analyticsEvent.Act = Hardlight.Enums.EnumExtentions.GetString(levelDefinition.ActName);
                        analyticsEvent.MissionName = Hardlight.Enums.EnumExtentions.GetString(missionDefinition.MissionName);
                        analyticsEvent.MissionIndex = MissionContextRegular.GetMissionIndex(missionDefinition, levelDefinition);
                    }
                    break;
                default:
                    HLOutput.LogError(string.Concat("Collected ornament from unknown category \"", HardlightProject.HardlightEnumExtensions.GetString(ornament.OrnamentCategory), "\""));
                    break;
            }
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //0600007f
        public static void JukeboxTrackEndEvent(MusicTrackEndEvent musicTrackEndEvent)
        {
            var analyticsEvent = JukeboxTrackEnd.Create();
            analyticsEvent.SongIDX = musicTrackEndEvent.TrackCache.Number;
            analyticsEvent.Reason = HardlightProject.HardlightEnumExtensions.GetString(musicTrackEndEvent.Reason);
            analyticsEvent.IdleStart = musicTrackEndEvent.WasIdleStart;
            analyticsEvent.ListenTime = musicTrackEndEvent.TrackListenedSeconds;
            analyticsEvent.SongLength = musicTrackEndEvent.TrackLengthSeconds;
            analyticsEvent.MissionRef = musicTrackEndEvent.GetGameLocation();
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000080
        public static void MissionBlueCoinCollectedEvent(MissionDefinition definition)
        {
            var analyticsEvent = ResourceChange.Create();
            analyticsEvent.CurrencyIsSpend = false;
            analyticsEvent.CurrencyType = BlueCoinCurrencyType;
            analyticsEvent.CurrencyDelta = 1;
            analyticsEvent.CurrencyReason = ResourceChangeReasonMissions;
            analyticsEvent.ItemID = definition.GetGUID();
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000081: reward-track currency events use the empty ItemID.
        public static void RewardTrackBlueCoinsAwardedEvent(ChallengeRewardBlueCoins challengeReward)
        {
            var analyticsEvent = ResourceChange.Create();
            analyticsEvent.CurrencyIsSpend = false;
            analyticsEvent.CurrencyType = BlueCoinCurrencyType;
            analyticsEvent.CurrencyDelta = challengeReward.BlueCoinAmount;
            analyticsEvent.CurrencyReason = ResourceChangeReasonRewardTrack;
            analyticsEvent.ItemID = string.Empty;
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000082: unchecked negation retains Int32.MinValue.
        public static void DreamPowerBoughtEvent(DreamPowerDefinition dreamPowerDefinition)
        {
            var analyticsEvent = ResourceChange.Create();
            analyticsEvent.CurrencyIsSpend = true;
            analyticsEvent.CurrencyType = BlueCoinCurrencyType;
            analyticsEvent.CurrencyDelta = unchecked(-dreamPowerDefinition.PurchaseCost);
            analyticsEvent.CurrencyReason = ResourceChangeReasonDreamPowerUnlock;
            analyticsEvent.ItemID = dreamPowerDefinition.GetStrippedDefinitionName();
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000083: original enum ToString is distinct from generated GetString.
        public static void XPGainedEvent(int xpGained, ChallengeManager.XPSource source, string itemId = null)
        {
            var analyticsEvent = ResourceChange.Create();
            analyticsEvent.CurrencyIsSpend = false;
            analyticsEvent.CurrencyType = XPCurrencyType;
            analyticsEvent.CurrencyDelta = xpGained;
            analyticsEvent.CurrencyReason = source.ToString();
            analyticsEvent.ItemID = string.IsNullOrWhiteSpace(itemId) ? Unknown : itemId;
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000084
        public static void XPSpentEvent(int xpSpent, ChallengeManager.XPSink sink, string itemId)
        {
            var analyticsEvent = ResourceChange.Create();
            analyticsEvent.CurrencyIsSpend = true;
            analyticsEvent.CurrencyType = XPCurrencyType;
            analyticsEvent.CurrencyDelta = unchecked(-xpSpent);
            analyticsEvent.CurrencyReason = sink.ToString();
            analyticsEvent.ItemID = string.IsNullOrWhiteSpace(itemId) ? Unknown : itemId;
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000085: slot8 calls GetAnalyticsName; reward GUID is another slot.
        public static void RewardTrackClaimedEvent(ChallengeReward challengeReward, RewardTrackType trackType, int rewardTrackIndex)
        {
            var analyticsEvent = RewardTrackClaim.Create();
            analyticsEvent.RewardIDX = rewardTrackIndex;
            analyticsEvent.XpThreshold = challengeReward.XPThreshold;
            analyticsEvent.ItemID = string.Concat(HardlightProject.HardlightEnumExtensions.GetString(trackType), "_", challengeReward.GetAnalyticsName());
            Analytics.PushAnalyticsEventToManualQueue(analyticsEvent, ManualAnalyticsQueue);
        }

        //06000086: sub-missions are read even when mission already has the
        // requested type. The genuine List.Find predicate faults on null items.
        private static int CalculateSubObjectiveProgress(MissionType missionType, IMissionContext activeMissionContext, MissionDefinition mission, string levelGUID)
        {
            var subMissions = activeMissionContext.MissionDefinition.SubMissions;
            if (mission.Type != missionType)
                mission = subMissions.Find(subMission => subMission.Type == missionType);
            if (mission == null) return 0;
            var missionManager = ProcessManager.GetSystemSafe<MissionManager>();
            if (missionManager == null) return 0;
            return missionManager.GetSaveDataForMission(mission.GetGUID(), levelGUID, null)?.CompletedObjectiveIndices?.Count ?? 0;
        }
    }
}
