using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Hardlight
{
    // Original HLUnityCore.Runtime 02000082 (0600030b..06000326), bodies from the
    // 1.10.1 x86-64 binary. Every builder-based helper shares s_stringBuilder, so
    // none of them is reentrant or thread-safe, as shipped.
    public static class StringExtensions
    {
        private const int DigitCount = 2;
        // BeforeFieldInit with .cctor 0x06000324 (0x1ab6120): a field initialiser.
        private static readonly StringBuilder s_stringBuilder = new StringBuilder();

        // 0x0600030b (0x1ab3e70): any lower-case char fails; empty is true.
        public static bool IsUpper(this string value)
        {
            foreach (char character in value)
            {
                if (char.IsLower(character))
                {
                    return false;
                }
            }
            return true;
        }

        // 0x0600030c (0x1ab3f00): any upper-case char fails; empty is true.
        public static bool IsLower(this string value)
        {
            foreach (char character in value)
            {
                if (char.IsUpper(character))
                {
                    return false;
                }
            }
            return true;
        }

        // 0x0600030d (0x1ab3f90). Leading and trailing non-alphanumerics are dropped
        // (the trailing scan stops at index 1), inner ones collapse into a single '_'.
        // An upper-upper-lower run moves the last appended char right and writes '_'
        // before it, without updating the remembered characters.
        public static string ToSnake(this string value, CultureInfo cultureInfo = null, bool snakeBeforeDigitGroup = true, bool snakeAfterDigitGroup = true)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            cultureInfo = cultureInfo ?? CultureInfo.InvariantCulture;
            char previousLastAppendedChar = '\0';
            char lastAppendedChar = '\0';
            s_stringBuilder.Clear();

            int startIndex = 0;
            while (startIndex < value.Length && !char.IsLetterOrDigit(value[startIndex]))
            {
                startIndex++;
            }

            int endIndex = value.Length - 1;
            while (endIndex > 0 && !char.IsLetterOrDigit(value[endIndex]))
            {
                endIndex--;
            }

            for (int i = startIndex; i <= endIndex; i++)
            {
                char character = value[i];
                if (char.IsLetterOrDigit(character))
                {
                    if (snakeAfterDigitGroup && char.IsLetter(character) && char.IsDigit(lastAppendedChar)
                        || snakeBeforeDigitGroup && char.IsDigit(character) && char.IsLetter(lastAppendedChar))
                    {
                        AppendUnderscore();
                    }
                    else if (char.IsLower(lastAppendedChar) && char.IsUpper(character))
                    {
                        AppendUnderscore();
                    }
                    else if (char.IsUpper(previousLastAppendedChar) && char.IsUpper(lastAppendedChar) && char.IsLower(character))
                    {
                        s_stringBuilder.Length++;
                        s_stringBuilder[s_stringBuilder.Length - 1] = s_stringBuilder[s_stringBuilder.Length - 2];
                        s_stringBuilder[s_stringBuilder.Length - 2] = '_';
                    }
                    AppendCharacter(character);
                }
                else
                {
                    AppendUnderscore();
                }
            }
            return s_stringBuilder.ToString();

            // 0x06000325 (0x1ab4650): remember, then append lowered with the culture.
            void AppendCharacter(char character)
            {
                previousLastAppendedChar = lastAppendedChar;
                lastAppendedChar = character;
                s_stringBuilder.Append(char.ToLower(character, cultureInfo));
            }

            // 0x06000326 (0x1ab45e0): never two underscores in a row.
            void AppendUnderscore()
            {
                if (lastAppendedChar != '_')
                {
                    AppendCharacter('_');
                }
            }
        }

        // 0x0600030e (0x1ab4710): blank input is 0 rather than a parse failure.
        public static float ToFloatInvariant(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0f;
            }
            return float.Parse(value, CultureInfo.InvariantCulture);
        }

        // 0x0600030f (0x1ab4780): null throws; empty is blank.
        public static bool IsBlank(this string value)
        {
            foreach (char character in value)
            {
                if (!char.IsWhiteSpace(character))
                {
                    return false;
                }
            }
            return true;
        }

        // 0x06000310 (0x1ab4810): a null string[] separator splits on whitespace.
        public static string RemoveWhitespace(this string str)
        {
            return string.Join(string.Empty, str.Split((string[])null, StringSplitOptions.RemoveEmptyEntries));
        }

        // 0x06000311 (0x1ab4880): a space before an upper-case char that follows a
        // non-upper non-space, or ends an upper-case run followed by lower case.
        public static string AddSpaces(this string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            s_stringBuilder.Clear();
            s_stringBuilder.Append(text[0]);
            for (int i = 1; i < text.Length; i++)
            {
                char previous = text[i - 1];
                char current = text[i];
                bool previousIsUpper = char.IsUpper(previous);
                bool currentIsUpper = char.IsUpper(current);
                bool nextIsLower = i + 1 < text.Length && char.IsLower(text[i + 1]);
                if (currentIsUpper && !previousIsUpper && previous != ' ' || currentIsUpper && previousIsUpper && nextIsLower)
                {
                    s_stringBuilder.Append(' ');
                }
                s_stringBuilder.Append(current);
            }
            return s_stringBuilder.ToString();
        }

        // 0x06000312 (0x1ab4b60): the suffix is added beyond maxLength.
        public static string Truncate(this string text, int maxLength, string truncateSuffix = "")
        {
            return text.Length > maxLength ? text.Substring(0, maxLength) + truncateSuffix : text;
        }

        // 0x06000313 (0x1ab4ba0).
        public static string MakeNumeric(this string text, char characterException)
        {
            return text.MakeNumeric(new[] { characterException });
        }

        // 0x06000314 (0x1ab4c40): Hardlight ArrayExtensions.Contains, not IndexOf.
        public static string MakeNumeric(this string text, char[] characterExceptions = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
            if (characterExceptions == null)
            {
                characterExceptions = Array.Empty<char>();
            }

            s_stringBuilder.Clear();
            foreach (char character in text)
            {
                if (char.IsDigit(character) || characterExceptions.Contains(character))
                {
                    s_stringBuilder.Append(character);
                }
            }
            return s_stringBuilder.ToString();
        }

        // 0x06000315 (0x1ab4ee0).
        public static string MakeAlphanumeric(this string text, char characterException)
        {
            return text.MakeAlphanumeric(new[] { characterException });
        }

        // 0x06000316 (0x1ab4f80): Hardlight ArrayExtensions.Contains, not IndexOf.
        public static string MakeAlphanumeric(this string text, char[] characterExceptions = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
            if (characterExceptions == null)
            {
                characterExceptions = Array.Empty<char>();
            }

            s_stringBuilder.Clear();
            foreach (char character in text)
            {
                if (char.IsLetterOrDigit(character) || characterExceptions.Contains(character))
                {
                    s_stringBuilder.Append(character);
                }
            }
            return s_stringBuilder.ToString();
        }

        // 0x06000317 (0x1ab4b20): the negative index test precedes the text access,
        // so only a non-negative index throws on null text.
        public static bool TryGetCharacter(this string text, int index, out char character)
        {
            if (index >= 0 && index < text.Length)
            {
                character = text[index];
                return true;
            }
            character = '\0';
            return false;
        }

        // 0x06000318 (0x1ab5220): seconds are formatted before minutes.
        public static string Format0PlaceholderTime(this string str, int minutes, int seconds, bool addNegativeSign = true)
        {
            if ((minutes < 0 || seconds < 0) && addNegativeSign)
            {
                str += OpString.i + '-';
            }
            string secondsText = Get0PlaceholderFormat(seconds, DigitCount);
            string minutesText = Get0PlaceholderFormat(minutes, DigitCount);
            return str + (OpString.i + minutesText + ':' + secondsText);
        }

        // 0x06000319 (0x1ab53a0). The native absolute value is mov/negl/cmovs
        // (0x1ab5417/0x1ab541a) with no overflow path, so int.MinValue stays
        // int.MinValue. Math.Abs(int) would throw on Mono; the unchecked
        // negate-if-negative form keeps the native result there too.
        private static string Get0PlaceholderFormat(int number, int digitCount)
        {
            string text = string.Empty;
            int absoluteNumber = unchecked(number < 0 ? -number : number);
            int numberDigitCount = number == 0 ? 1 : (int)Math.Floor(Math.Log10(absoluteNumber)) + 1;
            for (int i = 0; i < digitCount - numberDigitCount; i++)
            {
                text += OpString.i + '0';
            }
            return text + (OpString.i + absoluteNumber);
        }

        // 0x0600031a (0x1ab5590): exceptions are checked before alphanumerics.
        public static bool HasSpecialChars(this string value, char[] characterExceptions = null)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if ((characterExceptions == null || !characterExceptions.Contains(value[i])) && !char.IsLetterOrDigit(value[i]))
                {
                    return true;
                }
            }
            return false;
        }

        // 0x0600031b (0x1ab56f0): an all-whitespace value reports its length.
        public static int GetIndentSize(this string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                {
                    return i;
                }
            }
            return value.Length;
        }

        // 0x0600031c (0x1ab57b0): index 0 is never inspected.
        public static int GetLastNonWhiteSpaceIndex(this string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }
            int index = value.Length - 1;
            while (index > 0 && char.IsWhiteSpace(value[index]))
            {
                index--;
            }
            return index;
        }

        // 0x0600031d (0x1ab5860): invariant-culture Contains and IndexOf.
        public static bool TryRemoveFirstSubstring(this string value, string substring, out string newValue)
        {
            newValue = value;
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(substring))
            {
                return false;
            }
            if (substring.Length > value.Length || !value.Contains(substring, StringComparison.InvariantCulture))
            {
                return false;
            }

            int index = value.IndexOf(substring, StringComparison.InvariantCulture);
            string start = index > 0 ? value.Substring(0, index) : string.Empty;
            int endIndex = index + substring.Length;
            int endLength = value.Length - endIndex;
            string end = endLength > 0 ? value.Substring(endIndex, endLength) : string.Empty;
            newValue = start + end;
            return true;
        }

        // 0x0600031e (0x1ab59c0): as above with an invariant-culture LastIndexOf.
        public static bool TryRemoveLastSubstring(this string value, string substring, out string newValue)
        {
            newValue = value;
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(substring))
            {
                return false;
            }
            if (substring.Length > value.Length || !value.Contains(substring, StringComparison.InvariantCulture))
            {
                return false;
            }

            int index = value.LastIndexOf(substring, StringComparison.InvariantCulture);
            string start = index > 0 ? value.Substring(0, index) : string.Empty;
            int endIndex = index + substring.Length;
            int endLength = value.Length - endIndex;
            string end = endLength > 0 ? value.Substring(endIndex, endLength) : string.Empty;
            newValue = start + end;
            return true;
        }

        // 0x0600031f (0x1ab5b20): insert after the leading whitespace.
        public static bool TryInsertFirstText(this string value, string firstText, out string newValue)
        {
            newValue = value;
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(firstText))
            {
                return false;
            }

            int index = 0;
            while (index < value.Length && char.IsWhiteSpace(value[index]))
            {
                index++;
            }
            newValue = value.Insert(index, firstText);
            return true;
        }

        // 0x06000320 (0x1ab5c30): insert before the trailing whitespace.
        public static bool TryInsertLastText(this string value, string lastText, out string newValue)
        {
            newValue = value;
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(lastText))
            {
                return false;
            }

            int index = value.Length;
            while (index > 0 && char.IsWhiteSpace(value[index - 1]))
            {
                index--;
            }
            newValue = value.Insert(index, lastText);
            return true;
        }

        // 0x06000321 (0x1ab5d40): one current-culture EndsWith suffix; a null trim
        // returns the input, including a null input.
        public static string TrimEndString(this string input, string trim)
        {
            if (trim != null && input.EndsWith(trim))
            {
                return input.Substring(0, input.Length - trim.Length);
            }
            return input;
        }

        // 0x06000322 (0x1ab5d90).
        public static bool IsIndexValid(this string value, int index)
        {
            return value != null && index >= 0 && index < value.Length;
        }

        // 0x06000323 (0x1ab5db0): index may equal Length. An empty sequence matches
        // only an empty string; running past the end is a mismatch.
        public static bool ContainsAt(this string str, int index, IEnumerable<char> chars)
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
