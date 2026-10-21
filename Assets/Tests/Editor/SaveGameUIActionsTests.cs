using System.Text.RegularExpressions;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // SaveGameUIActions (x86-64 0x5ECE80, 0x5ED410) over the SaveManager of OwnedCharacterManagers. That
    // fixture's coroutine host and the actions' own inactive host refuse coroutines, each logging the engine
    // error expected below: one for the RequestSave inside SaveManager.DeleteAllSaves, one for the reopening.
    public sealed class SaveGameUIActionsTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");

        // The open save is closed (0x5ecef5), every slot is replaced and a save requested (0x5ecf7e..0x5ecfa0), and
        // because a save was open its reopening is started on the component (0x5ecf74).
        [Test]
        public void DeletingAllSavesClosesTheOpenSaveReplacesEverySlotAndStartsReopening()
        {
            Owned.Run(owned =>
            {
                owned.Lease(new OwnedCharacterManagers());
                SaveManager save = ProcessManager.GetSystem<SaveManager>();
                var actions = owned.Host("Lucid save actions", false).AddComponent<SaveGameUIActions>();
                Assert.That(save.IsAnySaveOpen, Is.True);
                Assert.That(save.RawSaves[0], Is.Null.Or.Empty);

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                actions.Action_DeleteAllSaves();

                Assert.That(save.IsAnySaveOpen, Is.False, "closed; the reopening could not start on the inactive host");
                for (int slot = 0; slot < 3; ++slot)
                    Assert.That(save.RawSaves[slot], Is.Not.Null.And.Not.Empty, "slot " + slot + " holds a fresh save");
            });
        }

        // Without an open save only the delete and the save request run (0x5ecfa5 skips the reopening).
        [Test]
        public void DeletingAllSavesWithoutAnOpenSaveDoesNotStartReopening()
        {
            Owned.Run(owned =>
            {
                owned.Lease(new OwnedCharacterManagers());
                SaveManager save = ProcessManager.GetSystem<SaveManager>();
                save.CloseSave();
                var actions = owned.Host("Lucid save actions", false).AddComponent<SaveGameUIActions>();

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                actions.Action_DeleteAllSaves();

                Assert.That(save.IsAnySaveOpen, Is.False);
                for (int slot = 0; slot < 3; ++slot)
                    Assert.That(save.RawSaves[slot], Is.Not.Null.And.Not.Empty, "slot " + slot + " holds a fresh save");
            });
        }

        // The analytics save is rebuilt only when one exists (0x5ed4b5); without it neither analytics consumer
        // is looked up, so no consent or session manager is needed.
        [Test]
        public void ResettingAnalyticsWithoutAnAnalyticsSaveTouchesNothing()
        {
            Owned.Run(owned =>
            {
                owned.Lease(new OwnedCharacterManagers());
                SaveManager save = ProcessManager.GetSystem<SaveManager>();
                var actions = owned.Host("Lucid save actions", false).AddComponent<SaveGameUIActions>();
                Assert.That(save.HasSaveDataAnalytics(), Is.False);
                Assert.DoesNotThrow(actions.Action_ResetAnalyticsData);
                Assert.That(save.HasSaveDataAnalytics(), Is.False);
            });
        }
    }
}
