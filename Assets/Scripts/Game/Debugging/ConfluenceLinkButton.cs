using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020009f8; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ConfluenceLinkButton : MonoBehaviour
    {
        [HideInInspector]
        [SerializeField]
        private string m_url = "https://hardlight.atlassian.net/wiki/spaces/HL/pages/71368746/SDT+Environment+collision+workflow";

        // 06003947 (1.10.1 RVA 0x6AF7B0)
        public string URL => m_url;

        // 06003948 (1.10.1 RVA 0x6AF7C0)
        public void OpenLink()
        {
            Application.OpenURL(m_url);
        }

        // 06003949 (1.10.1 RVA 0x6AF820): the shipped body is empty, so the URL is never replaced.
        public void SetURL(string url) { }

        // 0600394a (1.10.1 RVA 0x6AF830): the field initialiser precedes the MonoBehaviour base call.
        public ConfluenceLinkButton() { }
    }
}
