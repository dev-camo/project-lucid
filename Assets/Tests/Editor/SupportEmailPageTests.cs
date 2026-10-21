using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.UiPublishingFixtures;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UIWidgetButtonSupport (Game.Runtime 0x020009a5; OpenEmailContainer 0x69B9F0, GetEmailString 0x69B860, GetEmailBody
    // 0x69BA70) and UIContainerEmailSupport (0x020008b5; OnEnable 0x62FC90, Action_Close 0x62FEB0) over an owned string
    // table and a genuine App. The strings are one distinct word per key, so a swapped key shows.
    //
    // Not covered: UIWidgetButtonSupport.Action_SendEmail, which passes the mail link to Application.OpenURL and would
    // launch the machine's mail client.
    public sealed class SupportEmailPageTests
    {
        private static readonly Dictionary<Strings, string> Table = new Dictionary<Strings, string>
        {
            { Strings.SETTINGS_LABEL_SUPPORT_EMAIL_BODY_PREFIX, "Prefix line" },
            { Strings.SETTINGS_LABEL_SUPPORT_EMAIL_BODY_DETAIL_DEVICE_ID, "Device id:" },
            { Strings.SETTINGS_LABEL_SUPPORT_EMAIL_BODY_DETAIL_DEVICE_MODEL, "Device model:" },
            { Strings.SETTINGS_LABEL_SETTINGS_VERSION_NUMBER, "Version {0}" },
            { Strings.SETTINGS_LABEL_SUPPORT_EMAIL_BODY_SUFFIX, "Suffix line" },
            { Strings.SETTINGS_LABEL_SUPPORT_EMAIL_ADDRESS, "help+desk@lucid.test" },
            { Strings.SETTINGS_LABEL_SUPPORT_EMAIL_SUBJECT, "Need help & more" },
            { Strings.SETTINGS_LABEL_TVOS_EMAIL_REQUEST, "Please write to" },
            { Strings.SETTINGS_LABEL_TVOS_EMAIL_ADDRESS, "Address:" },
            { Strings.SETTINGS_LABEL_TVOS_EMAIL_SUBJECT, "Subject:" },
        };

        // The report GetEmailBody builds (0x69BA70..0x69BBD7): the prefix as a line and an empty line, two detail lines
        // formatted "{0} {1}\n" (the label and SystemInfo.deviceModel both times: both calls go to get_deviceModel), the
        // version line from the version format with Application.version and no line end of its own before the empty
        // line, then the suffix as a line and an empty line. AppendLine writes the platform's line end.
        private static string ExpectedBody()
        {
            string newLine = Environment.NewLine;
            return "Prefix line" + newLine + newLine
                + "Device id: " + SystemInfo.deviceModel + "\n"
                + "Device model: " + SystemInfo.deviceModel + "\n"
                + "Version " + Application.version + newLine
                + "Suffix line" + newLine + newLine;
        }

        [Test]
        public void TheEmailBodyIsThePrefixThenTheDeviceAndVersionLinesThenTheSuffix()
        {
            OwnedStringTable.Run(Table, () =>
                Assert.That(UIWidgetButtonSupport.GetEmailBody().ToString(), Is.EqualTo(ExpectedBody())));
        }

        // GetEmailString 0x69B860 builds "mailto:" + address + "?subject=" + subject + "&body=" + body, each part escaped
        // with Uri.EscapeDataString (0x69B93E, 0x69B975, 0x69B9B3). The address contains a plus and an at sign, the subject
        // spaces and an ampersand.
        [Test]
        public void TheMailLinkEscapesTheAddressTheSubjectAndTheBodyIndividually()
        {
            // Reflection: GetEmailString is private and static; the public route, Action_SendEmail, opens the mail client.
            MethodInfo getEmailString = typeof(UIWidgetButtonSupport).GetMethod("GetEmailString", BindingFlags.NonPublic | BindingFlags.Static);

            OwnedStringTable.Run(Table, () =>
            {
                string link = (string)getEmailString.Invoke(null, null);

                Assert.That(link, Is.EqualTo("mailto:help%2Bdesk%40lucid.test?subject=Need%20help%20%26%20more&body="
                    + Uri.EscapeDataString(ExpectedBody())));
            });
        }

        // OpenEmailContainer 0x69B9F0 adds m_openCustomerSupportEvent to the App's event list; a second press adds
        // nothing (AddUniqueUIModernEvent at 0x530D60).
        [Test]
        public void OpeningTheEmailContainerQueuesTheSupportEventOnce()
        {
            Owned.Run(owned =>
            {
                App app = NewApp(owned);
                var widget = owned.Host("Lucid support button", false).AddComponent<UIWidgetButtonSupport>();
                UIModernEvent supportEvent = NewEvent(owned, "customer-support");
                Set(widget, "m_openCustomerSupportEvent", supportEvent); // serialized in the original

                Call(widget, "OpenEmailContainer");
                Call(widget, "OpenEmailContainer");

                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { supportEvent }));
            });
        }

        // OnEnable 0x62FC90 formats "{0}\n\n{1} {2}\n{3} {4} \n\n{5}" with the tvOS request, the tvOS address label, the
        // support address, the tvOS subject label, the support subject and the email body report.
        [Test]
        public void EnablingTheEmailPageFormatsTheRequestTheAddressTheSubjectAndTheReport()
        {
            Owned.Run(owned =>
            {
                var container = owned.Host("Lucid email support", false).AddComponent<UIContainerEmailSupport>();

                OwnedStringTable.Run(Table, () => Call(container, "OnEnable"));

                Assert.That(container.BodyText.Value, Is.EqualTo(
                    "Please write to\n\nAddress: help+desk@lucid.test\nSubject: Need help & more \n\n" + ExpectedBody()));
            });
        }

        // Action_Close 0x62FEB0 adds m_closeCustomerSupportEvent to the App's event list (AddUniqueUIModernEvent).
        [Test]
        public void ClosingTheEmailPageQueuesTheCloseEventOnce()
        {
            Owned.Run(owned =>
            {
                App app = NewApp(owned);
                var container = owned.Host("Lucid email support", false).AddComponent<UIContainerEmailSupport>();
                UIModernEvent closeEvent = NewEvent(owned, "close-customer-support");
                Set(container, "m_closeCustomerSupportEvent", closeEvent); // serialized in the original

                container.Action_Close();
                container.Action_Close();

                Assert.That(app.GetUIModernEvents(), Is.EqualTo(new[] { closeEvent }));
            });
        }
    }
}
