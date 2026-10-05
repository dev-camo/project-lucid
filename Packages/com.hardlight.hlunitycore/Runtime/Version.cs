using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Hardlight.Utils
{
    // Original HLUnityCore.Runtime020002d6. Exact complete struct/fields;
    // registered HLOutput diagnostic routes remain explicit dependencies.
    [Serializable]
    public struct Version : IEquatable<Version>, IComparable<Version>
    {
        [SerializeField]
        [Tooltip("Insert a version in the format of 0.0.0, you can use as many numeric identifiers as you wish.")]
        private string m_version;
        private const uint Default = 0u;
        public static readonly string DefaultString = string.Format("{0}", Default);
        private const string Separator = ".";
        private const NumberStyles NumberStyleNone = NumberStyles.None;
        private static readonly NumberFormatInfo s_numberFormatInfoInvariant = NumberFormatInfo.InvariantInfo;
        private static StringBuilder s_stringBuilder = new StringBuilder();
        private uint[] m_numericIdentifiers;
        private bool m_isParsed;

        // 0600125b; ARM641b3e2b4. Keep the supplied array reference; empty/null becomes one0.
        public Version(params uint[] numericIdentifiers)
        {
            m_version = null;
            m_numericIdentifiers = numericIdentifiers;
            m_isParsed = false;
            if (m_numericIdentifiers == null || m_numericIdentifiers.Length == 0)
                m_numericIdentifiers = new uint[] { Default };
            m_version = GenerateStringFromNumericIdentifiers(m_numericIdentifiers);
            if (IsVersionStringValid(m_version, out string error))
                m_isParsed = true;
            else
            {
                HLOutput.LogError(error);
                m_isParsed = false;
            }
        }

        // 0600125c; ARM641b3e910. Invalid original text is logged then leaves all fields default.
        public Version(string version)
        {
            m_version = null;
            m_numericIdentifiers = null;
            m_isParsed = false;
            if (IsVersionStringValid(version, out string error))
            {
                m_version = version;
                ParseVersionString();
            }
            else
                HLOutput.LogError(error);
        }

        // 0600125d; ARM641b3ecb4. Only receiver is parsed; other is read as passed.
        // Major must match even at ordinal0. Missing tails are0; ordinal0 unsigned
        // underflow requests equality of every component rather than an empty-prefix result.
        public bool SubversionOf(Version other, uint ordinal)
        {
            ParseVersionString();
            if (m_numericIdentifiers[0] != other.m_numericIdentifiers[0])
                return false;
            int length = Math.Max(m_numericIdentifiers.Length, other.m_numericIdentifiers.Length);
            for (int i = 1; i < length; ++i)
            {
                if ((uint)i > unchecked(ordinal - 1))
                    break;
                uint a = i < m_numericIdentifiers.Length ? m_numericIdentifiers[i] : Default;
                uint b = i < other.m_numericIdentifiers.Length ? other.m_numericIdentifiers[i] : Default;
                if ((uint)i == unchecked(ordinal - 1))
                    return a <= b;
                if (a != b)
                    return false;
            }
            return true;
        }

        // 0600125e; ARM641b3e41c. Count snapshot precedes shared builder lock.
        private string GenerateStringFromNumericIdentifiers(IReadOnlyList<uint> numericIdentifiers)
        {
            int count = numericIdentifiers.Count;
            if (count == 0)
                return null;
            lock (s_stringBuilder)
            {
                s_stringBuilder.Clear();
                for (int i = 0; i < count; ++i)
                {
                    if (i != 0)
                        s_stringBuilder.Append(Separator);
                    s_stringBuilder.Append(numericIdentifiers[i].ToString());
                }
                return s_stringBuilder.ToString();
            }
        }

        // 0600125f; ARM641b3ee4c. Receiver first, then local by-value other;
        // unsigned numeric lexicographic order with missing components padded0.
        public int CompareTo(Version other)
        {
            ParseVersionString();
            other.ParseVersionString();
            int length = Math.Max(m_numericIdentifiers.Length, other.m_numericIdentifiers.Length);
            for (int i = 0; i < length; ++i)
            {
                uint a = i < m_numericIdentifiers.Length ? m_numericIdentifiers[i] : Default;
                uint b = i < other.m_numericIdentifiers.Length ? other.m_numericIdentifiers[i] : Default;
                if (a > b)
                    return 1;
                if (a < b)
                    return -1;
            }
            return 0;
        }

        // 06001260; ARM641b3ef9c. Wrong/null box returns false without parsing receiver.
        public override bool Equals(object obj) => obj is Version other && Equals(other);
        // 06001261; ARM641b3f0b8.
        public bool Equals(Version other) => CompareTo(other) == 0;
        // 06001262; ARM641b3f154. Preserve original array-identity hash quirk,
        // including distinct numeric-equal versions having unrelated hashes.
        public override int GetHashCode()
        {
            ParseVersionString();
            return m_numericIdentifiers == null ? 0 : m_numericIdentifiers.GetHashCode();
        }
        // 06001263; ARM641b3f1f4. Lazy parse mutates receiver's cache.
        public override string ToString()
        {
            ParseVersionString();
            return m_version;
        }

        // 06001264..69; ARM641b3f25c..69c, operators pass both structs by value.
        public static bool operator ==(Version a, Version b) => a.CompareTo(b) == 0;
        public static bool operator !=(Version a, Version b) => a.CompareTo(b) != 0;
        public static bool operator >(Version a, Version b) => a.CompareTo(b) == 1;
        public static bool operator <(Version a, Version b) => a.CompareTo(b) == -1;
        public static bool operator >=(Version a, Version b) => a.CompareTo(b) >= 0;
        public static bool operator <=(Version a, Version b) => a.CompareTo(b) <= 0;

        // 0600126a; ARM641b3ea38. The invariant digit validator precedes trimming;
        // the successful path retains ignored TryParse results and regenerates canonical text.
        private void ParseVersionString()
        {
            if (m_isParsed)
                return;
            if (string.IsNullOrEmpty(m_version))
                m_version = DefaultString;
            if (!IsVersionStringValid(m_version, out string error))
            {
                HLOutput.LogError(error);
                return;
            }
            string[] identifiers = m_version.Trim().Split(Separator, StringSplitOptions.RemoveEmptyEntries);
            m_numericIdentifiers = new uint[identifiers.Length];
            for (int i = 0; i < identifiers.Length; ++i)
                uint.TryParse(identifiers[i].Trim(), NumberStyleNone, s_numberFormatInfoInvariant, out m_numericIdentifiers[i]);
            m_version = GenerateStringFromNumericIdentifiers(m_numericIdentifiers);
            m_isParsed = true;
        }

        // 0600126b; ARM641b3f69c, same validator with an ignored out value.
        public static bool IsVersionStringValid(string versionString) => IsVersionStringValid(versionString, out string _);

        // 0600126c; ARM641b3e7a0. Empty entries retained for exact1-based validation diagnostics.
        public static bool IsVersionStringValid(string versionString, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(versionString))
            {
                error = "The version is null or empty";
                return false;
            }
            string[] identifiers = versionString.Split(Separator, StringSplitOptions.None);
            for (int i = 0; i < identifiers.Length; ++i)
                if (!IsNumericIdentifierValid(identifiers[i], i + 1, versionString, Separator, out error))
                    return false;
            return true;
        }

        // 0600126d; ARM641b3f710. UInt32/NumberStyles.None/invariant provider; no regex or semver substitute.
        private static bool IsNumericIdentifierValid(string versionToParse, int location, string versionString, string separatorString, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(versionToParse))
            {
                error = "The numeric identifier in location " + location.ToString() + " of version '" + versionString + "' split using '" + separatorString + "' as separator is null or empty.";
                return false;
            }
            if (uint.TryParse(versionToParse, NumberStyleNone, s_numberFormatInfoInvariant, out uint _))
                return true;
            error = "The numeric identifier in location " + location.ToString() + " of version '" + versionString + "' split using '" + separatorString + "' as separator, which is '" + versionToParse + "', is not an unsigned digit only number.";
            return false;
        }
        // 0600126e; ARM641b3fb08: DefaultString, invariant provider, sharedbuilder ordered static initializers above.
    }
}
