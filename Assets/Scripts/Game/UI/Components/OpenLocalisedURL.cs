using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000a15; bodies 0x6B2DC0..0x6B3060 (1.10.1 x86-64).
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class OpenLocalisedURL : MonoBehaviour
    {
        [SerializeField]
        [Hardlight.Utils.HashEnum(typeof(Strings))]
        private Strings m_urlLocalised;
        [SerializeField]
        private UIModernEvent m_eventToFire;
        [SerializeField]
        private bool m_debugQRCodeInEditor;

        // 060039c5: the retail body contains only the OpenUrl path. Nothing in this build
        // reads m_debugQRCodeInEditor or calls OpenQRCode, so any branch that did is compiled out.
        public void Action_Open()
        {
            OpenUrl();
        }

        // 060039c6: shows the URL as a QR code by publishing the container parameters and its UI event.
        private void OpenQRCode()
        {
            App app = ProcessManager.GetSystem<App>(null, true);
            app.Storage.SetValue(AppFSMKeys.UIContainerParameters, new UIContainerQRCodeParameters(m_urlLocalised));
            app.AddUniqueUIModernEvent(m_eventToFire);
        }

        // 060039c7
        private void OpenUrl()
        {
            Application.OpenURL(StringTable.GetString(m_urlLocalised));
        }
    }
}
