using System;
using System.Text;

namespace Hardlight
{
    public static partial class StringExtensions
    {
        private static readonly StringBuilder s_stringBuilder;

        // HLUnityCore.Runtime.dll:0x06000324; arm64 0x1ab956c.
        static StringExtensions() { s_stringBuilder = new StringBuilder(); }

        // 0x06000315; arm64 0x1ab81b8 delegates through a one-element char array.
        public static string MakeAlphanumeric(this string text, char characterException)
        {
            return text.MakeAlphanumeric(new[] { characterException });
        }

        // 0x06000316; arm64 0x1ab8274. Keep UTF-16 letters/digits and explicit
        // exceptions in order. Whitespace-only input is returned unchanged.
        // Retain the original shared builder and its single-thread assumption.
        public static string MakeAlphanumeric(this string text, char[] characterExceptions = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            if (characterExceptions == null) characterExceptions = Array.Empty<char>();
            s_stringBuilder.Clear();
            foreach (char character in text)
                if (char.IsLetterOrDigit(character) || Array.IndexOf(characterExceptions, character) >= 0)
                    s_stringBuilder.Append(character);
            return s_stringBuilder.ToString();
        }
    }
}
