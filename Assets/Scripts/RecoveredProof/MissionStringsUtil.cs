// Original whole MissionStringsUtil preservation. Localisation and formatting remain original.
// Timer NaN follows its <= comparison into numeric formatting; descriptions omit NaN time.
// CPU conversion/culture/localisation and null/malformed-format runtime equivalence remain held.
using Hardlight.Enums;
using Hardlight.Localisation;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Game0200071b: complete original three-method static provider, one constant.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class MissionStringsUtil
    {
        private const Strings TimerFormatDescription = Strings.MISSION_TIMER_FORMAT_DESCRIPTION;

        //060027d5, ARM59ae88/x865c2c90: localisation lookup always runs first.
        //Original literal globals are "-", "--", and "D2". Retain the <= test,
        //floor/truncation order, current-culture formatting and unguarded Format.
        //Exceptional-float conversion equivalence remains unproven.
        public static string GetMissionTimerFormat(float timeSeconds, Strings stringFormat = TimerFormatDescription)
        {
            string format = StringTable.GetString(stringFormat);
            if (timeSeconds <= 0f) return string.Format(format, "-", "--", "--");
            string minutes = Mathf.FloorToInt(timeSeconds / 60f).ToString();
            string seconds = Mathf.FloorToInt(timeSeconds % 60f).ToString("D2");
            string hundredths = ((int)((timeSeconds - Mathf.Floor(timeSeconds)) * 100f)).ToString("D2");
            return string.Format(format, minutes, seconds, hundredths);
        }

        //060027d6, ARM587d18/x865af5d0: original private overload is inlined in
        //the native caller; resolve the localisation string before formatting.
        public static string GetMissionDescription(Strings missionDescriptionId, float timeSeconds = 0f, int objectiveCount = 0) =>
            GetMissionDescription(StringTable.GetString(missionDescriptionId), timeSeconds, objectiveCount);

        //060027d7, ARM59b110/x865c2eb0: only positive time/count supply arguments.
        //Malformed/null formats retain String.Format's ordinary exceptions.
        private static string GetMissionDescription(string missionDescription, float timeSeconds = 0f, int objectiveCount = 0)
        {
            string timer = timeSeconds > 0f ? GetMissionTimerFormat(timeSeconds) : string.Empty;
            string count = objectiveCount > 0 ? objectiveCount.ToString() : string.Empty;
            return string.Format(missionDescription, timer, count);
        }
    }
}
