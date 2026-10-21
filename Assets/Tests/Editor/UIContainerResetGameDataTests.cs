using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using static ProjectLucid.Tests.UiPublishingFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIContainerResetGameData (Game.Runtime 0x0200091d; Action_DeleteCurrentSave 0x678970) over the genuine SaveManager
    // (with an open save) and App of OwnedCharacterManagers. That open save sits in no numbered slot (its index stays at
    // the no-save-open value), so SaveManager.DeleteSave, which refuses the open slot, deletes nothing here.
    //
    // Not covered: the deletion of the numbered slot the save was open in (DeleteSave 0x678A3F with the index read at
    // 0x678A29 before CloseSave); no fixture opens a save in a numbered slot without the property store and cloud.
    public sealed class UIContainerResetGameDataTests
    {
        private static void Run(Action<Owned, App, SaveManager, UIContainerResetGameData, UIModernEvent> body) => Owned.Run(owned =>
        {
            owned.Lease(new OwnedCharacterManagers());
            App app = ProcessManager.GetSystem<App>();
            app.Storage.SetValue(AppFSMKeys.UIModernEvents, new List<UIModernEvent>());
            var container = owned.Host("Lucid reset game data", false).AddComponent<UIContainerResetGameData>();
            UIModernEvent confirm = NewEvent(owned, "confirm-delete");
            Set(container, "m_onConfirmDeleteUIEvent", confirm); // serialized in the original
            body(owned, app, ProcessManager.GetSystem<SaveManager>(), container, confirm);
        });

        // 0x678A21 tests the open save: it is closed (0x678A32), the game is marked not loaded (SetValue<bool> of GameSaveLoaded
        // with false, 0x678AF5) and the confirm event is queued last (0x678B17).
        [Test]
        public void DeletingWithAnOpenSaveClosesItMarksTheGameNotLoadedAndQueuesTheConfirmEvent()
        {
            Run((owned, app, save, container, confirm) =>
            {
                app.Storage.SetValue(AppFSMKeys.GameSaveLoaded, true);
                Assert.That(save.IsAnySaveOpen, Is.True);

                container.Action_DeleteCurrentSave();

                Assert.That(save.IsAnySaveOpen, Is.False);
                Assert.That(app.Storage.TryGetValue(AppFSMKeys.GameSaveLoaded, out bool loaded), Is.True);
                Assert.That(loaded, Is.False);
                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { confirm }));
            });
        }

        // With no save open (0x678A23 jumps to 0x678AFF) the save flag is not touched, but the confirm event is still queued.
        [Test]
        public void DeletingWithoutAnOpenSaveOnlyQueuesTheConfirmEvent()
        {
            Run((owned, app, save, container, confirm) =>
            {
                save.CloseSave();
                app.Storage.SetValue(AppFSMKeys.GameSaveLoaded, true);

                container.Action_DeleteCurrentSave();

                Assert.That(app.Storage.TryGetValue(AppFSMKeys.GameSaveLoaded, out bool loaded), Is.True);
                Assert.That(loaded, Is.True, "not written");
                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { confirm }));
            });
        }

        // A second press finds no open save and queues nothing more (the event list adds each event once).
        [Test]
        public void ADeleteTwiceQueuesTheConfirmEventOnce()
        {
            Run((owned, app, save, container, confirm) =>
            {
                container.Action_DeleteCurrentSave();
                container.Action_DeleteCurrentSave();

                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { confirm }));
            });
        }
    }
}
