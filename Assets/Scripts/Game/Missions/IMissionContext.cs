using System.Collections.Generic;
using Hardlight.Analytics;

namespace HardlightProject
{
    // Original Game.Runtime020006f6: complete27-member interface. Three real
    //default interface methods retain native bodies; the other24 are actual
    //abstract declarations. Runtime support remains pending full graph validation.
    public interface IMissionContext
    {
        //06002603..0600260e, actual virtual slots0..11.
        MissionDefinition MissionDefinition { get; }
        GameplayLevelDefinition LevelDefinition { get; }
        IReadOnlyList<int> OverrideActiveObjectives { get; }
        IReadOnlyList<CharacterId> OverrideAllowedCharacters { get; }
        float BestTimeSeconds { get; }
        float OverrideTimeLimitSeconds { get; }
        LevelStartPositionDefinition OverrideStartPosition { get; }
        int RingXPEarned { get; }
        bool IgnoreSavedProgress { get; }
        bool IsReplay { get; }
        float RealTimeTaken { get; set; }

        //0600260f/ARM582758 and06002610/ARM5828c0: ordered interface
        //dispatch through definition slot0, level slot1, then calculator25/26.
        //Do not cache either property or replace the derived calculator.
        string MissionIndex => CalculateMissionIndex(MissionDefinition, LevelDefinition);
        AnalyticsMissionType AnalyticsMissionType => CalculateAnalyticsMissionType(MissionDefinition, LevelDefinition);

        //06002611..06002615, actual abstract virtual slots14..18.
        bool SaveLastLevelVisited { get; }
        string Description { get; }
        int ObjectiveTarget { get; }
        float NewTime { get; }
        void OnActiveMissionComplete(MissionManager missionManager, float timeElapsedSeconds, int score);

        //06002616/ARM582a28: read real time via slot10, add Single delta,
        //then dispatch its setter11. No elapsed-time clamp or pause check.
        void UpdateRealTimeTaken(float deltaTime) => RealTimeTaken += deltaTime;

        //06002617..0600261d, actual abstract virtual slots20..26. These
        //original analytics boundaries remain part of preserved implementations.
        void MarkNewAttempt(MissionState state);
        void SendMissionStartAnalytics(MissionState missionState);
        void SendMissionQuitAnalytics(MissionState missionState);
        void SendMissionFailedAnalytics(MissionState missionState);
        void SendMissionCompleteAnalytics(MissionState missionState);
        string CalculateMissionIndex(MissionDefinition missionDefinition, GameplayLevelDefinition levelDefinition);
        AnalyticsMissionType CalculateAnalyticsMissionType(MissionDefinition missionDefinition, GameplayLevelDefinition levelDefinition);
    }
}
