using System.Collections.Generic;
using System.Text;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 02000081 (06000306..0600030a), bodies from the
    // 1.10.1 x86-64 binary. A false condition returns before touching the builder,
    // so only a true condition throws on a null builder.
    public static class StringBuilderExtensions
    {
        // 0x06000306 (0x1ab3a50).
        public static void Append(this StringBuilder stringBuilder, bool condition, char character)
        {
            if (condition)
            {
                stringBuilder.Append(character);
            }
        }

        // 0x06000307 (0x1ab3a70).
        public static void Append(this StringBuilder stringBuilder, bool condition, string text)
        {
            if (condition)
            {
                stringBuilder.Append(text);
            }
        }

        // 0x06000308 (0x1ab3a90).
        public static void AppendLine(this StringBuilder stringBuilder, bool condition)
        {
            if (condition)
            {
                stringBuilder.AppendLine();
            }
        }

        // 0x06000309 (0x1ab3ab0).
        public static void AppendLine(this StringBuilder stringBuilder, bool condition, string text)
        {
            if (condition)
            {
                stringBuilder.AppendLine(text);
            }
        }

        // 0x0600030a (0x1ab3ad0): the StringExtensions.ContainsAt rule over a
        // builder. Index may equal Length; an empty sequence matches only an
        // empty builder; running past the end is a mismatch.
        public static bool ContainsAt(this StringBuilder str, int index, IEnumerable<char> chars)
        {
            if (index < 0 || index > str.Length)
            {
                return false;
            }

            bool hasChars = false;
            foreach (char character in chars)
            {
                hasChars = true;
                if (index >= str.Length || str[index] != character)
                {
                    return false;
                }
                index++;
            }
            return hasChars || str.Length == 0;
        }
    }
}
