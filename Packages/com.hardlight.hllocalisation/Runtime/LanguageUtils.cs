// Complete original HLLocalisation.Runtime.dll Hardlight.Localisation.LanguageUtils 0x0200000b.
// Original six methods/nine fields, ordered attributes, optional defaults and closed base are retained.
// Bodies follow all twelve original ARM64/x86_64 spans; local spelling/source-form is inferred.
// The native power import is ___exp10f on both CPUs. Mathf.Pow(10f, decimalPlaces) preserves Single
// factor construction at source level; full native/BCL/overflow/NaN/Infinity parity requires separate
// controlled runtime checks. No rounding, formatter, StringTable readiness or locale fallback is invented.
// 0x06000026 FormatNumber; ARM64 [0x1a3ae9c,0x1a3aeb0); x86_64 [0x1a35180,0x1a351a0).
// 0x06000027 FormatNumber; ARM64 [0x1a3b10c,0x1a3b11c); x86_64 [0x1a353e0,0x1a353f0).
// 0x06000028 FormatNumber; ARM64 [0x1a3aeb0,0x1a3b10c); x86_64 [0x1a351a0,0x1a353e0).
// 0x06000029 InitialiseNumberFormatter; ARM64 [0x1a3b11c,0x1a3b294); x86_64 [0x1a353f0,0x1a35560).
// 0x0600002a GenerateAbbreviatedValue; ARM64 [0x1a3b294,0x1a3b320); x86_64 [0x1a35560,0x1a355e0).
// 0x0600002b .ctor; ARM64 [0x1a3b564,0x1a3b5cc); x86_64 [0x1a357f0,0x1a35840).
using System;
using System.Globalization;
using Hardlight.Enums;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight.Localisation
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class LanguageUtils : MonoSingleton<LanguageUtils>
    {
        [SerializeField]
        [HashEnum(typeof(Strings))]
        private Strings separatorID = Strings.NONE;
        [HashEnum(typeof(Strings))]
        [SerializeField]
        private Strings decimalID = Strings.NONE;
        [HashEnum(typeof(Strings))]
        [SerializeField]
        private Strings thousandsID = Strings.NONE;
        [HashEnum(typeof(Strings))]
        [SerializeField]
        private Strings millionsID = Strings.NONE;
        [HashEnum(typeof(Strings))]
        [SerializeField]
        private Strings billionsID = Strings.NONE;
        private string thousandsFormatString;
        private string millionsFormatString;
        private string billionsFormatString;
        private NumberFormatInfo m_numberFormatter;

        // HLLocalisation.Runtime.dll 0x06000026: original Int64 is converted to Double.
        public string FormatNumber(long numberToFormat)
        {
            return FormatNumber((double)numberToFormat, 0, false, false);
        }

        // HLLocalisation.Runtime.dll 0x06000027.
        public string FormatNumber(double numberToFormat)
        {
            return FormatNumber(numberToFormat, 0, false, false);
        }

        // HLLocalisation.Runtime.dll 0x06000028. ignoreZeroDecimal is unused in both
        // shipping CPU bodies; abbreviated output removes trailing fractional zeros.
        public string FormatNumber(double numberToFormat, int decimalPlaces,
            bool ignoreZeroDecimal = false, bool abbreviate = false)
        {
            if (m_numberFormatter == null)
                InitialiseNumberFormatter();
            if (m_numberFormatter == null)
                return numberToFormat.ToString();
            if (!abbreviate)
                return numberToFormat.ToString(string.Format("n{0}", decimalPlaces), m_numberFormatter);

            string formatString;
            double value = GenerateAbbreviatedValue(numberToFormat, out formatString);
            if (decimalPlaces > 0)
            {
                float factor = Mathf.Pow(10f, decimalPlaces);
                value = Math.Floor(value * factor) / factor;
                while (decimalPlaces > 0 && unchecked((int)(value * factor)) % 10 == 0)
                {
                    decimalPlaces--;
                    factor = Mathf.Pow(10f, decimalPlaces);
                }
            }
            return string.Format(formatString,
                value.ToString(string.Format("n{0}", decimalPlaces), m_numberFormatter));
        }

        // HLLocalisation.Runtime.dll 0x06000029: publish formatter before original
        // five table lookups and cached formats; BCL setters and their faults stay live.
        private void InitialiseNumberFormatter()
        {
            m_numberFormatter = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
            string groupSeparator = StringTable.GetString(separatorID);
            string decimalSeparator = StringTable.GetString(decimalID);
            thousandsFormatString = StringTable.GetString(thousandsID);
            millionsFormatString = StringTable.GetString(millionsID);
            billionsFormatString = StringTable.GetString(billionsID);
            m_numberFormatter.NumberGroupSeparator = groupSeparator;
            m_numberFormatter.NumberDecimalSeparator = decimalSeparator;
        }

        // HLLocalisation.Runtime.dll 0x0600002a: no absolute-value or minimum-size
        // gate. Negative/small values use thousands; NaN follows the billions branch.
        private double GenerateAbbreviatedValue(double numberToFormat, out string formatString)
        {
            double divisor;
            if (numberToFormat < 1000000d)
            {
                formatString = thousandsFormatString;
                divisor = 1000d;
            }
            else if (numberToFormat < 1000000000d)
            {
                formatString = millionsFormatString;
                divisor = 1000000d;
            }
            else
            {
                formatString = billionsFormatString;
                divisor = 1000000000d;
            }
            return numberToFormat / divisor;
        }

        // HLLocalisation.Runtime.dll 0x0600002b: five original NONE initializers,
        // then genuine MonoSingleton<LanguageUtils> base construction.
        public LanguageUtils() { }
    }
}
