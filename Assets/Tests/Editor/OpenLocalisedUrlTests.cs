using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // OpenLocalisedURL (Game.Runtime 02000a15, bodies 0x6B2DC0..0x6B3060).
    //
    // Not covered: Action_Open and OpenUrl, which call Application.OpenURL and would launch a browser.
    public sealed class OpenLocalisedUrlTests
    {
        // A genuine App built by its own initialisers on an inactive host (Awake does not run, so the
        // storage the FSM would fill is empty). Its field initialisers take the SaveManager and
        // UIScreenTransitionManager references.
        private static App NewApp(Owned owned)
        {
            owned.LeaseRow<SaveManager>();
            owned.LeaseRow<UIScreenTransitionManager>();
            App app = owned.Register(owned.Host("Lucid open url app", false).AddComponent<App>());
            app.Storage.SetValue(AppFSMKeys.UIModernEvents, new List<UIModernEvent>());
            return app;
        }

        // OpenQRCode 0x6B2EC0 fetches the App (0x6B2F49), stores UIContainerQRCodeParameters built from
        // m_urlLocalised under AppFSMKeys.UIContainerParameters (0x6B2F9C..0x6B3025) and then adds
        // m_eventToFire to the App's event list through AddUniqueUIModernEvent (0x6B3058).
        [Test]
        public void TheQrCodePublishesTheUrlStringAndItsEventOnce()
        {
            Owned.Run(owned =>
            {
                App app = NewApp(owned);
                var opener = owned.Host("Lucid open url", false).AddComponent<OpenLocalisedURL>();
                var qrEvent = owned.Track(ScriptableObject.CreateInstance<UIModernEvent>());
                Set(opener, "m_urlLocalised", Strings.MISSING);
                Set(opener, "m_eventToFire", qrEvent);

                Call(opener, "OpenQRCode");
                var parameters = app.Storage.GetValue<UIContainerQRCodeParameters>(AppFSMKeys.UIContainerParameters, null, false);
                Assert.That(parameters.URLStringId, Is.EqualTo(Strings.MISSING));
                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { qrEvent }));

                Call(opener, "OpenQRCode");
                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { qrEvent }), "the event is added only once");
            });
        }
    }
}
