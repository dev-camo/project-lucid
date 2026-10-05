using System;
using System.Globalization;

namespace Hardlight
{
    // HLUnityCore.Runtime.dll, original type 0x0200028c. Behavior follows
    // the named ARM64 TimeUtils bodies; units and date-kind rules are intentional.
    public static class TimeUtils
    {
        public const int NanosecondsPerMicrosecond = 1000;
        public const int MicrosecondsPerMillisecond = 1000;
        public const int MillisecondsPerSecond = 1000;
        public const double SecondsPerMillisecond = 0.001;
        public const int SecondsPerMinute = 60;
        public const int MinutesPerHour = 60;
        public const int HoursPerDay = 24;
        public const int SecondsPerHour = 3600;
        public const int SecondsPerDay = 86400;
        public const int NanosecondsPerMillisecond = 1000000;
        public const int MillisecondsPerHour = 3600000;
        public const int MillisecondsPerMinute = 60000;
        public const int MillisecondsPerDay = 86400000;
        public static readonly DateTime Epoch;
        public const string DateTimeStringFormat = "dd/MM/yyyy HH:mm";
        public const string DateStringFormat = "dd/MM/yyyy";

        // 0x0600100b: 1970-01-01 at midnight, explicitly UTC.
        static TimeUtils() { Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc); }

        // 0x06000ff5/ff6: out resets before conversion; only range failures
        // are caught. These overloads use milliseconds rather than seconds.
        public static bool TryGetFromUnixTime(long unixTime, out DateTime dateTime)
        {
            dateTime = default(DateTime);
            try { dateTime = FromUnixTime(unixTime); return true; }
            catch (ArgumentOutOfRangeException) { return false; }
        }
        public static bool TryGetFromUnixTime(ulong unixTime, out DateTime dateTime)
        {
            dateTime = default(DateTime);
            try { dateTime = FromUnixTime(unixTime); return true; }
            catch (ArgumentOutOfRangeException) { return false; }
        }

        // 0x06000ff7/ff8: signed and unsigned floating-point conversions
        // feed DateTime.AddMilliseconds, retaining its runtime rounding.
        public static DateTime FromUnixTime(long unixTime) { return Epoch.AddMilliseconds(unixTime); }
        public static DateTime FromUnixTime(ulong unixTime) { return Epoch.AddMilliseconds(unixTime); }
        public static long ToUnixTime(DateTime date) { return ToUnixTimeMs(date); } // 0x06000ff9

        // 0x06000ffa: Convert rounds midpoint values to even. There is no
        // ToUniversalTime call on this path; wall-clock ticks are subtracted.
        public static long ToUnixTimeMs(this DateTime date)
        {
            return Convert.ToInt64((date - Epoch).TotalMilliseconds);
        }

        // 0x06000ffb: this separate path converts to UTC and truncates seconds.
        public static long ToUnixTimeSeconds(this DateTime date)
        {
            return (long)(date.ToUniversalTime() - Epoch).TotalSeconds;
        }

        // 0x06000ffc/ffd: invariant ParseExact with the short authored formats.
        // The original catches System.Exception, including null and bad dates.
        public static bool DateFromString(string dateString, out DateTime returnedDate)
        {
            try { returnedDate = DateTime.ParseExact(dateString, DateStringFormat, CultureInfo.InvariantCulture); return true; }
            catch (Exception) { returnedDate = default(DateTime); return false; }
        }
        public static bool DateTimeFromString(string dateTimeString, out DateTime returnedDateTime)
        {
            try { returnedDateTime = DateTime.ParseExact(dateTimeString, DateTimeStringFormat, CultureInfo.InvariantCulture); return true; }
            catch (Exception) { returnedDateTime = default(DateTime); return false; }
        }
        public static string DateToString(DateTime date) { return date.ToString(DateStringFormat); } // 0x06000ffe
        public static string DateTimeToString(DateTime dateTime) { return dateTime.ToString(DateTimeStringFormat); } // 0x06000fff
        public static string DateTimeToStringInvariant(DateTime dateTime) // 0x06001000
        {
            return dateTime.ToString(DateTimeStringFormat, CultureInfo.InvariantCulture);
        }

        // 0x06001001..1004: float and double precision paths are distinct.
        // Truncating casts are retained; overflow/NaN conversion is backend-specific.
        public static double MillisecondsToSeconds(long milliseconds) { return milliseconds * 0.001; }
        public static float DeltaMillisecondsToSeconds(long milliseconds) { return milliseconds * 0.001f; }
        public static int DeltaSecondsToMilliseconds(float seconds) { return (int)(seconds * 1000f); }
        public static long SecondsToMilliseconds(double seconds) { return (long)(seconds * 1000.0); }

        // 0x06001005: builds midnight from the supplied wall-clock calendar,
        // then compares rounded milliseconds. Equality selects the next day.
        // Overflow is unchecked and the returned date has Epoch's UTC kind.
        public static DateTime GetNextDatePastUtcMidnight(DateTime startTime, long millisecondsPastMidnight)
        {
            long start = startTime.ToUnixTimeMs();
            long midnight = new DateTime(startTime.Year, startTime.Month, startTime.Day).ToUnixTimeMs();
            long candidate = unchecked(midnight + millisecondsPastMidnight);
            if (start >= candidate) candidate = unchecked(candidate + MillisecondsPerDay);
            return FromUnixTime(candidate);
        }

        // 0x06001006/1007: strict previous boundary, inclusive current boundary.
        public static bool HasTimePassedThresholdSinceLastCheck(long previousCheck, long threshold, long currentTime)
        {
            return previousCheck < threshold && threshold <= currentTime;
        }
        public static bool HasTimePassedThresholdSinceLastCheck(DateTime previousCheck, DateTime threshold, DateTime currentTime)
        {
            return previousCheck < threshold && threshold <= currentTime;
        }
        public static DateTime Min(DateTime time, DateTime other) { return time <= other ? time : other; } // 0x06001008
        public static DateTime Max(DateTime time, DateTime other) { return time >= other ? time : other; } // 0x06001009

        // 0x0600100a: first lower-bound comparison takes priority, even if
        // callers pass reversed bounds. Equality preserves the input date kind.
        public static DateTime Clamp(this DateTime time, DateTime min, DateTime max)
        {
            if (time < min) return min;
            return time > max ? max : time;
        }
    }
}
