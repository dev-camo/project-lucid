using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 0200028e (06001017), body from the 1.10.1
    // x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public static class UrlUtils
    {
        // 0x06001017 (0x1b21650). Only the Uri parse is guarded: an invalid URL is
        // returned unchanged. A '+' is escaped only when the original also has a
        // space, and then only before the first invariant-culture '?'.
        public static string GetEscaped(string url)
        {
            string escapedUrl;
            try
            {
                escapedUrl = new Uri(url).AbsoluteUri;
            }
            catch (Exception)
            {
                return url;
            }

            if (url.Contains(" ") && url.Contains("+"))
            {
                int queryIndex = escapedUrl.IndexOf("?", StringComparison.InvariantCulture);
                if (queryIndex >= 0)
                {
                    return escapedUrl.Substring(0, queryIndex).Replace("+", "%2B") + escapedUrl.Substring(queryIndex, escapedUrl.Length - queryIndex);
                }
                return escapedUrl.Replace("+", "%2B");
            }
            return escapedUrl;
        }
    }
}
