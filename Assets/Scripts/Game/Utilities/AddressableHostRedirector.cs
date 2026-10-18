using System.Text.RegularExpressions;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000088. Rewrites the scheme://host:port prefix
    // of every Addressables internal id to the configured host while alive.
    public class AddressableHostRedirector
    {
        private string m_targetHost = string.Empty;
        private readonly Regex m_defaultMatchHostRegEx;
        private Regex m_customMatchHostRegEx;

        public Regex MatchHostRegEx
        {
            get => m_customMatchHostRegEx ?? m_defaultMatchHostRegEx;
            set => m_customMatchHostRegEx = value;
        }

        public AddressableHostRedirector()
        {
            m_defaultMatchHostRegEx = new Regex("(http|https):\\/\\/([a-z]|[0-9]|-)+:[0-9]{2,5}",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
            Addressables.InternalIdTransformFunc += EditResourceRequest;
        }

        ~AddressableHostRedirector()
        {
            Addressables.InternalIdTransformFunc -= EditResourceRequest;
        }

        public void SetHostURL(string hostURl)
        {
            m_targetHost = hostURl;
        }

        private string EditResourceRequest(IResourceLocation resourceLocation)
        {
            return MatchHostRegEx.Replace(resourceLocation.InternalId, ReplaceHost);
        }

        // A blank target host keeps the matched host unchanged.
        private string ReplaceHost(Match matchedHost)
        {
            return string.IsNullOrWhiteSpace(m_targetHost) ? matchedHost.ToString() : m_targetHost;
        }
    }
}
