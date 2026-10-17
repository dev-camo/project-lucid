using System;

namespace Hardlight
{
    public static partial class StringExtensions
    {
        // HLUnityCore.Runtime 0x06000310; x86-64 0x1ab4810. A null separator
        // array splits on every whitespace character; the pieces are joined
        // back with string.Empty. A null receiver throws.
        public static string RemoveWhitespace(this string str)
        {
            return string.Join(string.Empty, str.Split((string[])null, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
