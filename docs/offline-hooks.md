# Offline hook registry

Every point where the offline adapters (namespace `ProjectLucid.Offline`) divert an original member. Decision D2: a hook is a check of the central switch `ProjectLucid.Offline.OfflineAdapters.Active` (`Packages/com.hardlight.hlunitycore/Runtime/Offline/OfflineAdapters.cs`) at the top of the hooked method, with the original body kept verbatim after it, so the original is always compiled and its generated names stay exact. Each hook in source carries a comment `Offline hook H-nnn: ...`; search for `OfflineAdapters.Active` or `Offline hook` to find them all.

`tools/tests/test_offline_hooks.py` checks that this table and the source agree. Add a row here for every new hook, and never reuse an ID.

The only `#if` left for a hook is H-011, where the original cannot be restored (its native import descriptors are unknown).

| ID | File | Member | Original and adapter |
|---|---|---|---|
| H-001 | `Packages/com.hardlight.hlunitycore/Runtime/Achievement.cs` | `ReportProgress` | The original reported progress to Social; we record it in the local save |
| H-002 | `Packages/com.hardlight.hlunitycore/Runtime/AchievementsManager.cs` | `Initialise` | The original loaded achievements from Social; we load them from the local save |
| H-003 | `Packages/com.hardlight.hlunitycore/Runtime/AchievementsManager.cs` | `AddBehaviour` | The original required an authenticated Social user; we bind to the local save |
| H-004 | `Packages/com.hardlight.hlunitycore/Runtime/AchievementsManager.cs` | `IssueChallenge` | The original routed to the GameCenter provider; we report the challenge as unavailable |
| H-005 | `Packages/com.hardlight.hlunitycore/Runtime/AchievementsManager.cs` | `Shutdown` | The original only cleared the dictionary; we also release the local adapter context |
| H-006 | `Packages/com.hardlight.hlunitycore/Runtime/GameCenterAccessPoint.cs` | `GameCenterAccessPoint` | The original selected the macOS GameCenter access point plugin; we select the local one |
| H-007 | `Packages/com.hardlight.hlunitycore/Runtime/GameCenterLocalPlayerUtility.cs` | `CreateNativeGameCenterLocalPlayer` | The original chose the stub or the macOS local player; we always return the local account player |
| H-008 | `Packages/com.hardlight.hlunitycore/Runtime/GameCenterLocalPlayerUtility.cs` | `CreateGameCenterLocalPlayerListener` | The original chose the stub or the macOS listener; we always return the local account listener |
| H-009 | `Packages/com.hardlight.hlunitycore/Runtime/HLCloud/Plugin/Cloud.cs` | `NativePluginInstance` | The original created the iCloud plugin; we create the local file cloud |
| H-010 | `Packages/com.hardlight.hlunitycore/Runtime/HLPropertyStore.cs` | `HLPropertyStore` | The original created the encrypted file storage; we create the local save storage (after the same singleton publication) |
| H-011 | `Packages/com.hardlight.hlunitycore/Runtime/Logging/HLUnityCoreNativeBridge.cs` | `Unity_CoreInitialise, Unity_GetLocaleString, Unity_GetLanguageCode, Unity_GetISO2CountryCode, Unity_GetClientCode` | The original called five native imports and their import descriptors are unknown; we call managed forwarders that always replace them (this is the one remaining `#if PROJECT_LUCID_ORIGINAL_UNITY_CORE_NATIVE_BRIDGE`) |
| H-012 | `Packages/com.hardlight.hlanalytics/Runtime/Analytics.cs` | `Analytics` | The original created the networked AnalyticsSystem; we create the local adapter that discards events |
| H-013 | `Packages/com.hardlight.hlinput/Runtime/HLInputModule.cs` | `Awake` | The original picked the active Apple or macOS controller provider; we select the portable provider |
| H-014 | `Assets/Scripts/Game/Application/App.cs` | `InitialiseAnalytics` | The original registered the analytics session and consent managers; we run the offline telemetry startup |
| H-015 | `Assets/Scripts/Game/Application/App.cs` | `OnApplicationFocus` | The original paused analytics and cleared the notification badge; we notify the offline telemetry startup and keep the save-on-unfocus |
| H-016 | `Assets/Scripts/Game/Application/App.cs` | `OnApplicationQuit` | The original shut down the analytics managers; we shut down the offline telemetry startup |
| H-017 | `Assets/Scripts/Game/Application/FiniteStateMachine/ApplicationStateLoadAssets.cs` | `DoOnEnter` | The original called Social.localUser.Authenticate; we run no platform authentication and report a failed login synchronously through LocalGameCenterLifecycle.ReportLogin (the original statement stays in the else branch) |
| H-018 | `Assets/Scripts/Game/Application/GameCenter/GameCenter.cs` | `OnShutdown` | The original shut down the achievements and unregistered; we also stop the local lifecycle and release the local providers |
| H-019 | `Assets/Scripts/Game/Application/GameCenter/GameCenter.cs` | `NativeInitialise` | The original initialised the native login and requested authentication; we report a failed login at once |
| H-020 | `Assets/Scripts/Game/Application/GameCenter/GameCenter.cs` | `IsLoggedIn` | The original read Social.localUser.authenticated; the local profile is never logged in |
| H-021 | `Assets/Scripts/Game/Application/GameCenter/GameCenter.cs` | `IsUnderage` | The original asked the native layer; the local profile is never underage |
| H-022 | `Assets/Scripts/Game/Application/GameCenter/GameCenter.cs` | `NativeLoginRequest` | The original authenticated through Social; we report a failed login at once |
| H-023 | `Assets/Scripts/Game/Application/GameCenter/GameCenter.cs` | `OnLoginComplete` | The original loaded the platform account and started the leaderboard and achievements; we start the local lifecycle |
| H-024 | `Assets/Scripts/Game/Application/GameCenter/GameCenter.cs` | `GetAccountId` | The original asked the native layer for the account id; we return the local profile id |
| H-025 | `Assets/Scripts/Game/Application/GameCenter/GameCenter.cs` | `GetAccountNickname` | The original asked the native layer for the nickname; we return the local profile name |
| H-026 | `Assets/Scripts/Game/Application/GameCenter/GameCenter.cs` | `ShowAchievementPage` | The original opened the GameCenter achievements UI; we raise the local achievements presentation |
| H-027 | `Assets/Scripts/Game/Application/GameCenter/GameCenter.cs` | `ShowLeaderboardPage` | The original opened the GameCenter leaderboard UI; we raise the local records presentation |
| H-028 | `Assets/Scripts/Game/Application/GameCenter/GameCenter.cs` | `OnUpdate` | The original polled the platform account; we drain the local save listener removal and poll the local lifecycle |
| H-029 | `Assets/Scripts/Game/Application/GameCenter/GameCenter.cs` | `CheckForGameCenterAccountIDChange` | The original compared the platform account id; we poll the local lifecycle |
| H-030 | `Assets/Scripts/Game/Application/GameCenter/Achievements.cs` | `Initialise` | The original synced with GameCenter and waited for the platform; we prepare the local achievement manager lease |
| H-031 | `Assets/Scripts/Game/Application/GameCenter/Achievements.cs` | `AddAllAchievements` | The adapter can stop the subscriptions; the original had no stop state |
| H-032 | `Assets/Scripts/Game/Application/GameCenter/Achievements.cs` | `AddCollectableAchievements` | The original waited for the enemy manager through OnSystemStartup; we invoke on a valid reference |
| H-033 | `Assets/Scripts/Game/Application/GameCenter/Achievements.cs` | `AddBossAchievements` | The adapter can stop the subscriptions and remembers the boss manager so they can be detached |
| H-034 | `Assets/Scripts/Game/Application/GameCenter/Achievements.cs` | `AddLevelAchievements` | The adapter can stop the subscriptions; the original had no stop state |
| H-035 | `Assets/Scripts/Game/Application/GameCenter/Achievements.cs` | `AddMissionAchievements` | The adapter can stop the subscriptions and remembers the mission manager so they can be detached |
| H-036 | `Assets/Scripts/Game/Application/GameCenter/Achievements.cs` | `AddEnemyAchievements` | The original waited for the character manager through OnSystemStartup; we invoke on a valid reference |
| H-037 | `Assets/Scripts/Game/Application/GameCenter/Achievements.cs` | `AddCharacterAchievements` | The adapter can stop the subscriptions; the original had no stop state |
| H-038 | `Assets/Scripts/Game/Application/GameCenter/Achievements.cs` | `SaveManagerValid` | The original waited for the save through OnLoadCompleted; we bind through the local save binding |
| H-039 | `Assets/Scripts/Game/Application/GameCenter/Achievements.cs` | `AddSaveDataAchievements` | The adapter can stop the subscriptions; the original had no stop state |
| H-040 | `Assets/Scripts/Game/Application/GameCenter/Achievements.cs` | `AddChallengeAchievements` | The adapter can stop the subscriptions and remembers the challenge manager so they can be detached |
| H-041 | `Assets/Scripts/Game/Application/GameCenter/Achievements.cs` | `AddTotalXPEarnedAchievement` | The adapter can stop the subscriptions and remembers the challenge manager so they can be detached |
| H-042 | `Assets/Scripts/Game/Application/GameCenter/Achievements.cs` | `AddProgressionAchievements` | The adapter can stop the subscriptions and remembers the mission manager (the same instance the original reads below) so they can be detached |
| H-043 | `Assets/Scripts/Game/Application/GameCenter/Achievements.cs` | `Shutdown` | The original unregistered the AchievementsManager and removed its startup subscriptions; we dispose the manager lease and unbind the presentation |
| H-044 | `Assets/Scripts/Game/Application/GameCenter/Achievements.cs` | `ResetAchievements` | The original reset the GameCenter achievements; offline there is no reset policy, so it throws |
| H-045 | `Assets/Scripts/Game/Application/GameCenter/LeaderboardManager.cs` | `LeaderboardManager` | The original registered itself and started the GameCenter leaderboard; we register through the local leaderboard registration |
| H-046 | `Assets/Scripts/Game/Application/GameCenter/LeaderboardManager.cs` | `InitGameCenterLeaderboard` | The original created the GameCenter leaderboard once the local player was ready; we require the local personal records to be ready |
| H-047 | `Assets/Scripts/Game/Application/GameCenter/LeaderboardManager.cs` | `InitGameCenterLeaderboard` | The original created the GameCenter leaderboard for the started local player; we require the local personal records to be ready |
| H-048 | `Assets/Scripts/Game/Application/GameCenter/LeaderboardManager.cs` | `Shutdown` | The original removed the pending startup callback and unregistered; we unsubscribe and unregister only this object's default-name registration |
| H-049 | `Assets/Scripts/Game/Application/GameCenter/LeaderboardManager.cs` | `ReportScore` | The original reported the score to Social; we record it as a local personal record |
| H-050 | `Assets/Scripts/Game/Application/GameCenter/LeaderboardManager.cs` | `ShowLeaderboardUI` | The original opened the GameCenter leaderboard UI; we raise the local records presentation |
| H-051 | `Assets/Scripts/Game/Application/GameCenter/LeaderboardManager.cs` | `IssueChallenge` | The original asked the GameCenter leaderboard to issue a challenge; we report it as unavailable |
| H-052 | `Assets/Scripts/Game/Application/GameCenter/LeaderboardManager.cs` | `TryGetLoggedInGameCenter` | The original required a logged-in GameCenter account; the local profile never is |
| H-053 | `Assets/Scripts/Game/Missions/TimeTrials/TimeTrialLeaderboardManager.cs` | `TimeTrialLeaderboardManager` | The original used the Apple time trial leaderboard; we use the local one (subscriptions stay as in the original) |
| H-054 | `Assets/Scripts/Game/Missions/Challenges/ChallengeLeaderboardIntegration_GameCenter.cs` | `CanShowLeaderboard` | The original asked the leaderboard system whether the account is logged in; we report whether the local personal records are ready |
