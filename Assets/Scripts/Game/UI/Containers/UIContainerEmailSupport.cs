using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008b5; bodies translated from the 1.10.1 x86-64 binary.
    // The tvOS support page, which shows the support address and the device report because the platform cannot open a mail client.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerEmailSupport : UIContainer
    {
        [SerializeField] private UIModernEvent m_closeCustomerSupportEvent;
        public readonly Bindable<string> BodyText = new Bindable<string>();

        // 0x06003245 (0x62FC90)
        private void OnEnable()
        {
            BodyText.Value = string.Format("{0}\n\n{1} {2}\n{3} {4} \n\n{5}", new object[]
            {
                StringTable.GetString(Strings.SETTINGS_LABEL_TVOS_EMAIL_REQUEST),
                StringTable.GetString(Strings.SETTINGS_LABEL_TVOS_EMAIL_ADDRESS),
                StringTable.GetString(Strings.SETTINGS_LABEL_SUPPORT_EMAIL_ADDRESS),
                StringTable.GetString(Strings.SETTINGS_LABEL_TVOS_EMAIL_SUBJECT),
                StringTable.GetString(Strings.SETTINGS_LABEL_SUPPORT_EMAIL_SUBJECT),
                UIWidgetButtonSupport.GetEmailBody()
            });
        }

        // 0x06003246 (0x62FEB0)
        public void Action_Close()
        {
            ProcessManager.GetSystem<App>(null, true).AddUniqueUIModernEvent(m_closeCustomerSupportEvent);
        }

        // 0x06003247 (0x62FF30)
        public UIContainerEmailSupport()
        {
        }
    }
}
