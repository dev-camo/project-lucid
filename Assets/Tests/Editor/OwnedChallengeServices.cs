using System;
using Hardlight;
using HardlightProject;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // The challenge and dream power store managers that every ProgressionCollectableSave takes. Its
    // ProgressionCollectableChallenges constructor (06002ae6) fetches both with GetSystem(null, true), which faults while
    // their rows hold no system, so a case that caches progress registers genuine ones. Both are built by their own
    // initialisers on inactive hosts, so Awake never runs and they stay unstarted.
    internal static class OwnedChallengeServices
    {
        // The rows ChallengeManager's field initialisers cache references from. Each is absent, idle or already held by
        // the case's own live system, which is borrowed and handed back as it was admitted.
        private static readonly Type[] ChallengeManagerRows =
        {
            typeof(DataManager), typeof(VisualQualityManager_SDT), typeof(CutsceneManager),
            typeof(MissionManager), typeof(SaveManager), typeof(OrnamentManager),
        };

        internal static void Register(Owned owned)
        {
            foreach (Type row in ChallengeManagerRows)
                owned.Lease(new VerificationFixtureOwnership.LifecycleRow(row.ToString(), true)); // the default row name is Type.ToString()
            // Added while an empty category lookup is cached for the editor's Reset and OnValidate.
            owned.Register(owned.Lease(new OwnedTimeScaledObjects("lucid-progression-challenges")).Add<ChallengeManager>("manager"));
            owned.Register(owned.Host("Lucid dream power store", false).AddComponent<DreamPowerStoreManager>());
        }
    }
}
