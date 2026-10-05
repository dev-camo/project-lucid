namespace Hardlight
{
    public static partial class StringExtensions
    {
        // HLUnityCore.Runtime 0x06000321; ARM64 0x1ab9178. Remove one complete
        // matching suffix only, using the original current-culture EndsWith.
        // Null suffix returns the original receiver, including a null receiver.
        public static string TrimEndString(this string text, string suffix)
        {
            if (suffix == null || !text.EndsWith(suffix)) return text;
            return text.Substring(0, text.Length - suffix.Length);
        }
    }
}
