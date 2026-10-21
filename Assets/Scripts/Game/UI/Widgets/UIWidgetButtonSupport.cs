using System;
using System.Text;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009a5; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIWidgetButtonSupport : MonoBehaviour
    {
        [SerializeField] private bool m_debugViewContainerInEditor;
        [SerializeField] private UIModernEvent m_openCustomerSupportEvent;

        // 0x060037ca (0x69B800)
        public void Action_SendEmail()
        {
            Application.OpenURL(GetEmailString());
        }

        // 0x060037cb (0x69B9F0)
        private void OpenEmailContainer()
        {
            ProcessManager.GetSystem<App>(null, true).AddUniqueUIModernEvent(m_openCustomerSupportEvent);
        }

        // 0x060037cc (0x69B860): a mailto link with the address, subject and body each percent-encoded.
        private static string GetEmailString()
        {
            StringBuilder body = GetEmailBody();
            var link = new StringBuilder();
            link.Append("mailto:");
            link.Append(Uri.EscapeDataString(StringTable.GetString(Strings.SETTINGS_LABEL_SUPPORT_EMAIL_ADDRESS)));
            link.Append("?subject=");
            link.Append(Uri.EscapeDataString(StringTable.GetString(Strings.SETTINGS_LABEL_SUPPORT_EMAIL_SUBJECT)));
            link.Append("&body=");
            link.Append(Uri.EscapeDataString(body.ToString()));
            return link.ToString();
        }

        // 0x060037cd (0x69BA70): both detail lines read SystemInfo.deviceModel in the shipped build.
        public static StringBuilder GetEmailBody()
        {
            var body = new StringBuilder();
            body.AppendLine(StringTable.GetString(Strings.SETTINGS_LABEL_SUPPORT_EMAIL_BODY_PREFIX));
            body.AppendLine();
            body.AppendFormat("{0} {1}\n", StringTable.GetString(Strings.SETTINGS_LABEL_SUPPORT_EMAIL_BODY_DETAIL_DEVICE_ID), SystemInfo.deviceModel);
            body.AppendFormat("{0} {1}\n", StringTable.GetString(Strings.SETTINGS_LABEL_SUPPORT_EMAIL_BODY_DETAIL_DEVICE_MODEL), SystemInfo.deviceModel);
            body.AppendFormat(StringTable.GetString(Strings.SETTINGS_LABEL_SETTINGS_VERSION_NUMBER), Application.version);
            body.AppendLine();
            body.AppendLine(StringTable.GetString(Strings.SETTINGS_LABEL_SUPPORT_EMAIL_BODY_SUFFIX));
            body.AppendLine();
            return body;
        }

        // 0x060037ce (0x69BC00)
        public UIWidgetButtonSupport()
        {
        }
    }
}
