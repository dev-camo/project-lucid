using System.Text.RegularExpressions;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000088. Until finalized, replaces every
    // MatchHostRegEx match anywhere in an Addressables internal id (by default
    // http(s)://host:port with a letter, digit or hyphen host and a 2-5 digit
    // port) with the configured host; a blank host leaves ids unchanged.
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

        private string ReplaceHost(Match matchedHost)
        {
            return string.IsNullOrWhiteSpace(m_targetHost) ? matchedHost.ToString() : m_targetHost;
        }
    }
}
