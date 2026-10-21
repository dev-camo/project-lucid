using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetSDTCheckbox (Game.Runtime 0x020009c2, Action_Toggle 0x69FAD0, constructor 0x69FC80) as a real component on
    // an owned inactive host (it requires, and so adds, a Toggle), talking to the real App of OwnedCharacterManagers
    // whose pending modern UI event list the checkbox appends to.
    public sealed class UIWidgetSDTCheckboxTests
    {
        private static UIModernEvent NewEvent(Owned owned, string guid)
        {
            var uiEvent = owned.Track(ScriptableObject.CreateInstance<UIModernEvent>());
            Set(uiEvent, "m_guid", guid);
            return uiEvent;
        }

        // Action_Toggle selects the list by the flag (+0x20 for true, +0x28 for false, 0x69fb60), fetches the App's
        // pending list, then adds each entry of the selected list in order with AddUnique.
        [Test]
        public void TogglingAppendsTheMatchingEventsToThePendingList()
        {
            Owned.Run(owned =>
            {
                owned.Lease(new OwnedCharacterManagers());
                var pending = new List<UIModernEvent>();
                ProcessManager.GetSystem<App>().Storage.SetValue(AppFSMKeys.UIModernEvents, pending);
                UIModernEvent first = NewEvent(owned, "lucid-checkbox-first");
                UIModernEvent shared = NewEvent(owned, "lucid-checkbox-shared");
                UIModernEvent last = NewEvent(owned, "lucid-checkbox-last");
                var checkbox = owned.Host("Lucid checkbox", false).AddComponent<UIWidgetSDTCheckbox>();
                Assert.That(checkbox.GetComponent<Toggle>(), Is.Not.Null);
                Set(checkbox, "m_openEvents", new List<UIModernEvent> { first, shared }); // serialized in the original
                Set(checkbox, "m_closeEvents", new List<UIModernEvent> { shared, last }); // serialized in the original

                checkbox.Action_Toggle(true);
                Assert.That(pending, Is.EqualTo(new[] { first, shared }));

                checkbox.Action_Toggle(true);
                Assert.That(pending, Is.EqualTo(new[] { first, shared }), "events already pending are not added again");

                checkbox.Action_Toggle(false);
                Assert.That(pending, Is.EqualTo(new[] { first, shared, last }), "the close events follow, the shared one only once");
            });
        }

        // The events come from the list that matches the flag, not both.
        [Test]
        public void TogglingOffAddsOnlyTheCloseEvents()
        {
            Owned.Run(owned =>
            {
                owned.Lease(new OwnedCharacterManagers());
                var pending = new List<UIModernEvent>();
                ProcessManager.GetSystem<App>().Storage.SetValue(AppFSMKeys.UIModernEvents, pending);
                UIModernEvent open = NewEvent(owned, "lucid-checkbox-open");
                UIModernEvent close = NewEvent(owned, "lucid-checkbox-close");
                var checkbox = owned.Host("Lucid checkbox", false).AddComponent<UIWidgetSDTCheckbox>();
                Set(checkbox, "m_openEvents", new List<UIModernEvent> { open }); // serialized in the original
                Set(checkbox, "m_closeEvents", new List<UIModernEvent> { close }); // serialized in the original

                checkbox.Action_Toggle(false);

                Assert.That(pending, Is.EqualTo(new[] { close }));
            });
        }
    }
}
