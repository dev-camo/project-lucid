using System;
using System.Collections.Generic;
using System.Reflection;
using Hardlight.Analytics;

namespace ProjectLucid.Verification
{
    // Project Lucid verification: bound the original enum-label, comparer, and mutable-cache behavior.
    // The swap observer is test-only; it contributes no provider or production API.
    public static class AnalyticsEnumHelpersVerification
    {
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

        private sealed class SwapObserver<T> : IEqualityComparer<T>
        {
            private readonly Action swap;
            private readonly Func<T, int> hash;
            public bool Armed { get; set; }
            public bool Swapped { get; private set; }
            public SwapObserver(Action swap, Func<T, int> hash) { this.swap = swap; this.hash = hash; }
            public bool Equals(T x, T y) => EqualityComparer<T>.Default.Equals(x, y);
            public int GetHashCode(T value)
            {
                if (Armed && !Swapped) { Swapped = true; swap(); }
                return hash(value);
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static FieldInfo LookupField(string name)
        {
            FieldInfo field = typeof(HardlightEnumExtensions).GetField(name, PrivateStatic);
            if (field == null) throw new InvalidOperationException("Missing original lookup field " + name);
            return field;
        }

        public static void VerifyConsentState() => Verify(
            "AnalyticsConsentStateEnumToString",
            new[] { AnalyticsConsentState.None, AnalyticsConsentState.Consented, AnalyticsConsentState.Rejected, AnalyticsConsentState.Underage },
            new[] { "None", "Consented", "Rejected", "Underage" },
            value => (AnalyticsConsentState)value,
            value => HardlightEnumExtensions.GetString(value),
            new AnalyticsConsentStateEqualityComparer(),
            AnalyticsConsentState.None, (AnalyticsConsentState)0,
            AnalyticsConsentState.Rejected, AnalyticsConsentState.Underage,
            (AnalyticsConsentState)int.MinValue, AnalyticsConsentState.Rejected,
            (int)AnalyticsConsentState.Rejected,
            value => (int)value);

        public static void VerifyMissionType() => Verify(
            "AnalyticsMissionTypeEnumToString",
            new[] { AnalyticsMissionType.Boss, AnalyticsMissionType.Challenge, AnalyticsMissionType.Persistent, AnalyticsMissionType.Regular },
            new[] { "Boss", "Challenge", "Persistent", "Regular" },
            value => (AnalyticsMissionType)value,
            value => HardlightEnumExtensions.GetString(value),
            new AnalyticsMissionTypeEqualityComparer(),
            AnalyticsMissionType.Boss, (AnalyticsMissionType)(int)AnalyticsMissionType.Boss,
            AnalyticsMissionType.Regular, AnalyticsMissionType.Challenge,
            (AnalyticsMissionType)int.MinValue, AnalyticsMissionType.Regular,
            (int)AnalyticsMissionType.Regular,
            value => (int)value);

        private static void Verify<T>(string fieldName, T[] known, string[] labels, Func<int, T> fromInt,
            Func<T, string> format, IEqualityComparer<T> comparer, T equalLeft, T equalRight,
            T differentLeft, T differentRight, T minimum, T signed, int signedRaw, Func<T, int> hash)
        {
            FieldInfo field = LookupField(fieldName);
            // This first call runs the genuine original class initializer. Check all four values while
            // its original dictionary is still installed, before any fixture-created table is used.
            Check(format(known[0]) == labels[0], "original class initializer and first label");
            object saved = field.GetValue(null);
            try
            {
                var original = saved as Dictionary<T, string>;
                Check(original != null, "original static lookup is the expected dictionary type");
                for (int i = 0; i < known.Length; ++i)
                {
                    Check(original.TryGetValue(known[i], out string originalLabel) && originalLabel == labels[i], "original initialized entry " + labels[i]);
                    Check(format(known[i]) == labels[i], "original GetString label " + labels[i]);
                }

                var table = new Dictionary<T, string>(comparer);
                for (int i = 0; i < known.Length; ++i) table.Add(known[i], labels[i]);
                field.SetValue(null, table);

                table[known[0]] = "current value";
                Check(format(known[0]) == "current value", "lookup returns the current mutable cached value");
                table[known[1]] = null;
                Check(format(known[1]) == null, "stored null is returned as a cache hit");

                int[] raw = { 1, int.MaxValue, -1, int.MinValue };
                string[] expected = { "0x00000001", "0x7FFFFFFF", "0xFFFFFFFF", "0x80000000" };
                for (int i = 0; i < raw.Length; ++i)
                {
                    T value = fromInt(raw[i]);
                    string first = format(value);
                    Check(first == expected[i], "signed Int32 format " + expected[i]);
                    Check(ReferenceEquals(first, format(value)), "unknown lookup returns its cached string");
                    Check(table.TryGetValue(value, out string cached) && ReferenceEquals(first, cached), "miss stores in current dictionary");
                }

                Check(comparer.Equals(equalLeft, equalLeft), "comparer accepts identical enum values");
                Check(comparer.Equals(equalLeft, equalRight), "comparer accepts equal raw enum values");
                Check(!comparer.Equals(differentLeft, differentRight), "comparer distinguishes enum values");
                Check(comparer.GetHashCode(minimum) == int.MinValue, "comparer preserves Int32 minimum hash");
                Check(comparer.GetHashCode(signed) == signedRaw, "comparer preserves signed raw hash");
            }
            finally { field.SetValue(null, saved); }

            CheckFieldReread(field, known[0], fromInt(int.MinValue), format, comparer, hash);
        }

        private static void CheckFieldReread<T>(FieldInfo field, T known, T unknown,
            Func<T, string> format, IEqualityComparer<T> realComparer, Func<T, int> hash)
        {
            Check(format(known) != null, "original cache initialized before field replacement");
            object saved = field.GetValue(null);
            try
            {
                var replacement = new Dictionary<T, string>(realComparer);
                replacement.Add(unknown, "preexisting value");
                SwapObserver<T> observer = null;
                var oldReceiver = new Dictionary<T, string>(observer = new SwapObserver<T>(() => field.SetValue(null, replacement), hash));
                oldReceiver.Add(known, "seed"); // allocate a real bucket before arming the observer
                observer.Armed = true;
                field.SetValue(null, oldReceiver);

                string result = format(unknown);
                Check(observer.Swapped, "nonempty TryGetValue receiver triggered the one-shot field swap");
                Check(result == "0x80000000", "miss formats the original unknown value");
                Check(oldReceiver.TryGetValue(known, out string seed) && seed == "seed", "old receiver retains its seed");
                Check(!oldReceiver.ContainsKey(unknown), "old receiver did not receive the miss write");
                Check(replacement.TryGetValue(unknown, out string stored) && stored == result, "setter reread and overwrote the replacement field entry");
                Check(ReferenceEquals(result, format(unknown)), "later lookup hits the replacement cache");
            }
            finally { field.SetValue(null, saved); }
        }
    }
}
