using System;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.ChallengeRewardTestSupport;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // RequirementAppFSMValue<bool> (generic 0xBCB4F0, constructor 0xBCB610) and RequirementChallengesOpen
    // (0x557B70) over the real App storage of OwnedCharacterManagers. The storage is the App's own FSM storage,
    // written through its public SetValue.
    public sealed class LevelRequirementAppStorageTests
    {
        private const string Key = "lucid-requirement-app-value";

        private static void WithApp(Action<Owned, App> body) => Owned.Run(owned =>
        {
            owned.Lease(new OwnedCharacterManagers());
            body(owned, ProcessManager.GetSystem<App>());
        });

        private static RequirementAppFSMValueBool NewValueRequirement(Owned owned, bool valueToMatch, bool defaultValue) =>
            Create<RequirementAppFSMValueBool>(owned, "{\"m_appFsmKey\":\"" + Key + "\",\"m_valueToMatch\":" +
                (valueToMatch ? "true" : "false") + ",\"m_default\":" + (defaultValue ? "true" : "false") + "}");

        // The generic body compares the authored value with the stored one (0xBCB5FD, bool.Equals) and reads the
        // authored default (+0x29) only when TryGetValue fails (0xBCB59F branch). Each row below is one cell of
        // that truth table; the stored-value rows separate a body that always uses the default from one that
        // prefers the stored value, and the absent rows separate an ignored default from a used one.
        [Test]
        public void TheAuthoredValueIsComparedWithTheStoredValueOrTheAuthoredDefault()
        {
            // (valueToMatch, default, stored: 0 absent, 1 false, 2 true, expected)
            var rows = new (bool match, bool defaultValue, int stored, bool expected)[]
            {
                (true, true, 0, true), (true, false, 0, false), (false, true, 0, false), (false, false, 0, true),
                (true, false, 2, true), (true, true, 1, false), (false, false, 2, false), (false, true, 1, true),
            };
            foreach (var row in rows)
            {
                WithApp((owned, app) =>
                {
                    RequirementAppFSMValueBool requirement = NewValueRequirement(owned, row.match, row.defaultValue);
                    if (row.stored != 0) app.Storage.SetValue(new GraphStorageKey(Key, 0, 0), row.stored == 2);
                    Assert.That(requirement.RequirementsMet(null), Is.EqualTo(row.expected),
                        "match " + row.match + ", default " + row.defaultValue + ", stored " + row.stored);
                });
            }
        }

        // Only the storage key is cached (0xBCB52F tests it against the default key and builds it once); the
        // stored value itself is read on every call.
        [Test]
        public void ALaterStoredValueIsReadAfterTheKeyWasCached()
        {
            WithApp((owned, app) =>
            {
                RequirementAppFSMValueBool requirement = NewValueRequirement(owned, true, false);
                Assert.That(requirement.RequirementsMet(null), Is.False, "absent: the default false is compared");
                app.Storage.SetValue(new GraphStorageKey(Key, 0, 0), true);
                Assert.That(requirement.RequirementsMet(null), Is.True, "stored true");
                app.Storage.SetValue(new GraphStorageKey(Key, 0, 0), false);
                Assert.That(requirement.RequirementsMet(null), Is.False, "stored false");
            });
        }

        // 0x557BFD reads AppFSMKeys.ChallengesActive with TryGetValue<bool> and returns found && value (0x557C1A).
        [Test]
        public void ChallengesAreOpenOnlyForAStoredTrueFlag()
        {
            WithApp((owned, app) =>
            {
                RequirementChallengesOpen requirement = Create<RequirementChallengesOpen>(owned);
                Assert.That(requirement.RequirementsMet(null), Is.False, "absent flag");
                app.Storage.SetValue(AppFSMKeys.ChallengesActive, false);
                Assert.That(requirement.RequirementsMet(null), Is.False, "stored false");
                app.Storage.SetValue(AppFSMKeys.ChallengesActive, true);
                Assert.That(requirement.RequirementsMet(null), Is.True, "stored true");
            });
        }
    }
}
