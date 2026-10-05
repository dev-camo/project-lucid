using System;
using System.Globalization;
using Hardlight;
using UnityEngine;

namespace ProjectLucid
{
    public static class TimeUtilsVerification
    {
        private static int checks;
        public static void Run()
        {
            checks = 0;
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Check(TimeUtils.Epoch == epoch && TimeUtils.Epoch.Kind == DateTimeKind.Utc, "UTC epoch constructor");
            DateTime value;
            Check(TimeUtils.TryGetFromUnixTime(1500L, out value) && value == epoch.AddSeconds(1.5), "signed milliseconds");
            Check(TimeUtils.TryGetFromUnixTime(1500UL, out value) && value == epoch.AddSeconds(1.5), "unsigned milliseconds");
            Check(TimeUtils.FromUnixTime(-1L).Ticks == epoch.Ticks - 10000, "negative millisecond");
            Check(TimeUtils.FromUnixTime(1UL).Kind == DateTimeKind.Utc, "unsigned return kind");
            Check(!TimeUtils.TryGetFromUnixTime(long.MaxValue, out value) && value == default(DateTime), "signed range failure resets output");
            Check(!TimeUtils.TryGetFromUnixTime(ulong.MaxValue, out value) && value == default(DateTime), "unsigned range failure resets output");
            Throws<ArgumentOutOfRangeException>(() => TimeUtils.FromUnixTime(long.MinValue), "signed direct conversion propagates range error");
            Throws<ArgumentOutOfRangeException>(() => TimeUtils.FromUnixTime(ulong.MaxValue), "unsigned direct conversion propagates range error");
            Check(TimeUtils.ToUnixTime(epoch.AddMilliseconds(1234)) == 1234, "legacy method uses milliseconds");
            Check(TimeUtils.ToUnixTimeMs(epoch.AddTicks(5000)) == 0, "half millisecond rounds even zero");
            Check(TimeUtils.ToUnixTimeMs(epoch.AddTicks(15000)) == 2, "one and half rounds even two");
            Check(TimeUtils.ToUnixTimeMs(epoch.AddTicks(25000)) == 2, "two and half rounds even two");
            Check(TimeUtils.ToUnixTimeMs(epoch.AddTicks(-15000)) == -2, "negative midpoint rounds even");
            Check(TimeUtils.ToUnixTimeSeconds(epoch.AddTicks(15000000)) == 1, "seconds truncate positive fraction");
            Check(TimeUtils.ToUnixTimeSeconds(epoch.AddTicks(-15000000)) == -1, "seconds truncate negative fraction");
            DateTime utc = new DateTime(2026, 10, 5, 12, 34, 56, DateTimeKind.Utc);
            DateTime local = utc.ToLocalTime();
            Check(TimeUtils.ToUnixTimeMs(local) == TimeUtils.ToUnixTimeMs(DateTime.SpecifyKind(local, DateTimeKind.Unspecified)), "milliseconds ignore local date kind");
            Check(TimeUtils.ToUnixTimeSeconds(local) == TimeUtils.ToUnixTimeSeconds(utc), "seconds convert local to UTC");
            Check(TimeUtils.ToUnixTimeMs(DateTime.SpecifyKind(utc, DateTimeKind.Local)) == TimeUtils.ToUnixTimeMs(utc), "milliseconds preserve wall-clock ticks");

            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fi-FI");
                Check(TimeUtils.DateFromString("05/10/2026", out value) && value == new DateTime(2026, 10, 5), "date parser is invariant");
                Check(value.Kind == DateTimeKind.Unspecified, "parsed date kind");
                Check(TimeUtils.DateTimeFromString("05/10/2026 12:34", out value) && value == new DateTime(2026, 10, 5, 12, 34, 0), "minute format parse");
                Check(!TimeUtils.DateFromString(null, out value) && value == default(DateTime), "null date is caught");
                Check(!TimeUtils.DateTimeFromString(null, out value) && value == default(DateTime), "null datetime is caught");
                Check(!TimeUtils.DateFromString("31/02/2026", out value) && value == default(DateTime), "bad calendar date is caught");
                Check(!TimeUtils.DateTimeFromString("05/10/2026 12:34:56", out value), "extra seconds rejected");
                Check(!TimeUtils.DateFromString("2026-10-05", out value), "alternate date layout rejected");
                Check(TimeUtils.DateToString(utc) == "05.10.2026", "date formatter uses current culture");
                Check(TimeUtils.DateTimeToString(utc) == "05.10.2026 12.34", "datetime formatter uses current culture");
                Check(TimeUtils.DateTimeToStringInvariant(utc) == "05/10/2026 12:34", "explicit invariant formatter");
            }
            finally { CultureInfo.CurrentCulture = original; }

            Check(Math.Abs(TimeUtils.MillisecondsToSeconds(1501L) - 1.501) < 1e-12, "double duration units");
            Check(Math.Abs(TimeUtils.DeltaMillisecondsToSeconds(-1501L) + 1.501f) < 0.000001f, "float duration units");
            Check(TimeUtils.DeltaSecondsToMilliseconds(1.2345f) == 1234, "float truncation");
            Check(TimeUtils.DeltaSecondsToMilliseconds(-1.2345f) == -1234, "negative float truncation");
            Check(TimeUtils.SecondsToMilliseconds(1.2345) == 1234L, "double truncation");
            Check(TimeUtils.SecondsToMilliseconds(-1.2345) == -1234L, "negative double truncation");
            DateTime day = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
            Check(TimeUtils.GetNextDatePastUtcMidnight(day, 60000) == day.AddMinutes(1), "future offset today");
            Check(TimeUtils.GetNextDatePastUtcMidnight(day.AddMinutes(1), 60000) == day.AddDays(1).AddMinutes(1), "exact equality selects next day");
            Check(TimeUtils.GetNextDatePastUtcMidnight(day, -60000) == day.AddHours(23).AddMinutes(59), "negative offset uses one day adjustment");
            Check(TimeUtils.GetNextDatePastUtcMidnight(day, 172800000) == day.AddDays(2), "offset is not normalized into a day");
            DateTime midnightLocal = DateTime.SpecifyKind(day, DateTimeKind.Local);
            DateTime candidate = TimeUtils.GetNextDatePastUtcMidnight(midnightLocal, 60000);
            Check(candidate == day.AddMinutes(1) && candidate.Kind == DateTimeKind.Utc, "calendar wall-clock input with UTC result");
            Check(TimeUtils.HasTimePassedThresholdSinceLastCheck(9L, 10L, 10L), "inclusive current threshold");
            Check(!TimeUtils.HasTimePassedThresholdSinceLastCheck(10L, 10L, 11L), "strict previous threshold");
            Check(!TimeUtils.HasTimePassedThresholdSinceLastCheck(9L, 10L, 8L), "clock rollback does not cross threshold");
            Check(TimeUtils.HasTimePassedThresholdSinceLastCheck(day, day.AddTicks(1), day.AddTicks(1)), "datetime current threshold");
            Check(!TimeUtils.HasTimePassedThresholdSinceLastCheck(day, day, day.AddTicks(1)), "datetime previous threshold");
            DateTime sameTicksLocal = DateTime.SpecifyKind(day, DateTimeKind.Local);
            Check(TimeUtils.Min(sameTicksLocal, day).Kind == DateTimeKind.Local, "minimum tie returns first date kind");
            Check(TimeUtils.Max(sameTicksLocal, day).Kind == DateTimeKind.Local, "maximum tie returns first date kind");
            Check(day.AddDays(-1).Clamp(day, day.AddDays(1)) == day, "clamp below lower bound");
            Check(day.AddDays(2).Clamp(day, day.AddDays(1)) == day.AddDays(1), "clamp above upper bound");
            Check(sameTicksLocal.Clamp(day, day.AddDays(1)).Kind == DateTimeKind.Local, "clamp equality retains input kind");
            Check(day.Clamp(day.AddDays(1), day.AddDays(-1)) == day.AddDays(1), "reversed bounds preserve lower branch priority");
            Debug.Log("Original TimeUtils verification passed: " + checks + " checks; game save/progression remains unverified.");
        }
        private static void Check(bool value, string message)
        {
            checks++;
            if (!value) throw new InvalidOperationException("Original TimeUtils: " + message);
        }
        private static void Throws<T>(Action action, string message) where T : Exception
        {
            try { action(); } catch (T) { Check(true, message); return; }
            Check(false, message);
        }
    }
}
