using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // AppRatingRequesterBoss against the 1.10.1 x86-64 bodies cited per case. The rating request itself and the
    // cooldown-elapsed path (which stores the request time and schedules a save on a coroutine host) are not run here.
    public sealed class AppRatingRequesterBossTests
    {
        // The constructor (0x667B20) caches the MissionManager reference (GetSystemRef<MissionManager>(null, true),
        // 0x667B7A) before the base initialiser stores the seven day cooldown (0x40E00000 at +0x18).
        [Test]
        public void ConstructionCachesTheMissionManagerReferenceAndKeepsTheSevenDayCooldown()
        {
            Owned.Run(owned =>
            {
                owned.LeaseRow<MissionManager>();
                var requester = owned.Track(ScriptableObject.CreateInstance<AppRatingRequesterBoss>());
                Assert.That(Get<float>(requester, "m_requestCooldownDays"), Is.EqualTo(7f));
                Assert.That(Get<SystemRef<MissionManager>>(requester, "m_missionManagerRef"), Is.Not.Null);
                Assert.That(Get<SystemRef<MissionManager>>(requester, "m_missionManagerRef").IsNull(), Is.True, "no mission manager is registered");
                Assert.That(Get<GameplayLevelDefinition>(requester, "m_bossDefinition"), Is.Null);
            });
        }

        // 0x667A90 calls the base Validate first (0x667AC4) and returns false when it does: a rating requested within the
        // cooldown (the base check compares the seven days against the stored request time) does not fire, and the
        // refused check stores no new request time.
        [Test]
        public void ARecentRatingRequestKeepsTheBossRequesterFromFiring()
        {
            Owned.Run(owned =>
            {
                owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
                owned.LeaseRow<MissionManager>();
                SaveManager save = owned.Register(new SaveManager());
                var game = new SaveDataGame();
                Set(save, "m_currentSave", game);
                long now = TimeUtils.ToUnixTimeSeconds(DateTime.UtcNow);
                game.LastRatingRequestTimestampSeconds = now;
                var requester = owned.Track(ScriptableObject.CreateInstance<AppRatingRequesterBoss>());

                Assert.That(Call(requester, "Validate"), Is.False);
                Assert.That(game.LastRatingRequestTimestampSeconds, Is.EqualTo(now), "a refused request stores nothing");
            });
        }

        // 0x667ACB (je 0x667AE4) returns false only when the base is false; with the base true (cooldown elapsed, so the
        // request time is stored and RequestSave returns early on the disabled item) 0x667AE2 (je 0x667AED) reaches Get
        // only when IsNull is false. No mission manager is registered, so the result is false without a Get call.
        [Test]
        public void AnElapsedCooldownWithoutAMissionManagerStillRefusesTheBossRequest()
        {
            Owned.Run(owned =>
            {
                owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
                owned.LeaseRow<MissionManager>();
                SaveManager save = owned.Register(new SaveManager());
                var game = new SaveDataGame();
                game.DisableSaving();
                Set(save, "m_currentSave", game);
                game.LastRatingRequestTimestampSeconds = 0;
                var requester = owned.Track(ScriptableObject.CreateInstance<AppRatingRequesterBoss>());

                long before = TimeUtils.ToUnixTimeSeconds(DateTime.UtcNow);
                bool result = false;
                Assert.DoesNotThrow(() => result = (bool)Call(requester, "Validate"));
                long after = TimeUtils.ToUnixTimeSeconds(DateTime.UtcNow);

                Assert.That(result, Is.False);
                Assert.That(game.LastRatingRequestTimestampSeconds, Is.InRange(before, after), "the base check returned true and stored the request time");
            });
        }

        // 0x667A90 ends in the base call, then IsNull (0x667ADB) and a tail call of AnyMissionCompleted (0x667B0F) on
        // Get() with m_bossDefinition (+0x20): the request fires only for an elapsed cooldown, a registered manager and a
        // completed mission of the boss level. The manager is a real MissionManager over a real save.
        [Test]
        public void ValidateFiresOnlyForAnElapsedCooldownAndACompletedMissionOfTheBossLevel()
        {
            Owned.Run(owned =>
            {
                owned.LeaseActions(SystemAction.Initialise, SystemAction.Shutdown);
                owned.LeaseRow<MissionManager>();
                owned.LeaseRow<AudioManager>();
                owned.LeaseRow<TimeTrialLeaderboardManager>(); // MissionDefinition's field initialiser
                SaveManager save = owned.Register(new SaveManager());
                var game = new SaveDataGame();
                game.DisableSaving();
                Set(save, "m_currentSave", game); // Reflection: installs the open save the manager would hold after loading
                owned.Register(owned.Host("Lucid level manager", false).AddComponent<LevelManager>());

                const string LevelGuid = "lucid-boss-rating-level";
                const string MissionGuid = "lucid-boss-rating-mission";
                var mission = owned.Track(ScriptableObject.CreateInstance<MissionDefinition>());
                Set(mission, "m_guid", MissionGuid); // Reflection: authored GUID of a serialized definition
                var group = new MissionGroup();
                Set(group, "m_definitions", new List<MissionDefinition> { mission }); // Reflection: authored group contents
                var missionList = owned.Track(ScriptableObject.CreateInstance<MissionList>());
                Set(missionList, "m_groups", new List<MissionGroup> { group }); // Reflection: authored mission list
                var level = owned.Track(ScriptableObject.CreateInstance<GameplayLevelDefinition>());
                Set(level, "m_guid", LevelGuid); // Reflection: authored GUID of a serialized definition
                Set(level, "m_missionList", missionList); // Reflection: authored level data
                Set(level, "m_requirements", new List<RequirementGameplayLevelBase>()); // Reflection: authored level data

                MissionManager manager = null;
                VerificationFixtureOwnership.RunThenCleanUp(() =>
                    {
                        manager = new MissionManager();
                        ProcessManager.RegisterSystem(manager);
                        var requester = owned.Track(ScriptableObject.CreateInstance<AppRatingRequesterBoss>());
                        Set(requester, "m_bossDefinition", level); // Reflection: serialized field without a setter

                        game.LastRatingRequestTimestampSeconds = 0;
                        Assert.That(Call(requester, "Validate"), Is.False, "no mission of the boss level is complete");

                        game.GetOrCreateLevelData(LevelGuid).GetOrCreateMissionData(MissionGuid).Complete = true;
                        game.LastRatingRequestTimestampSeconds = 0;
                        Assert.That(Call(requester, "Validate"), Is.True, "a completed mission of the boss level, cooldown elapsed");

                        game.LastRatingRequestTimestampSeconds = TimeUtils.ToUnixTimeSeconds(DateTime.UtcNow);
                        Assert.That(Call(requester, "Validate"), Is.False, "the cooldown is checked first");
                    },
                    () =>
                    {
                        if (manager == null) return;
                        ProcessManager.UnregisterSystem(manager);
                        ProcessManager.UnsubscribeFromAllActions(manager);
                    });
            });
        }

        // The compiled call order keeps the short circuit: the base check, the manager test, then the mission query.
        [Test]
        public void ValidateAsksTheMissionManagerOnlyAfterTheBaseCheckAndAnExistingManager()
        {
            MethodInfo validate = typeof(AppRatingRequesterBoss).GetMethods(Declared).Single(m => m.Name == "Validate");
            Assert.That(OriginalILCallSequence.Names(OriginalILCallSequence.Calls(validate)), Is.EqualTo(new[]
            {
                "AppRatingRequesterBase.Validate", "SystemRef`1.IsNull", "SystemRef`1.Get", "MissionManager.AnyMissionCompleted",
            }));
        }

        [Test]
        public void DeclarationShapeKeepsTheAssetMenuAndTheSealedRequesterSurface()
        {
            Type type = typeof(AppRatingRequesterBoss);
            CustomAttributeData menu = type.GetCustomAttributesData().Single(d => d.AttributeType == typeof(CreateAssetMenuAttribute));
            Assert.That(menu.NamedArguments.Select(a => (a.MemberName, a.TypedValue.Value)).ToArray(), Is.EqualTo(new (string, object)[]
            {
                ("fileName", "AppRatingRequesterBoss"),
                ("menuName", "HardlightProject/DefinitionData/Definitions/AppRatingRequesterBoss"),
            }));
            Assert.That(type.IsSealed, Is.True);
            Assert.That(type.BaseType, Is.EqualTo(typeof(AppRatingRequesterBase)));
            Assert.That(type.GetField("m_bossDefinition", Declared).GetCustomAttribute<SerializeField>(), Is.Not.Null);
        }
    }
}
