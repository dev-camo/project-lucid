using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public static class TimeExtensions
    {
        private static TimeCategoryLookup s_config_Internal;
        // Original 06002ded / ARM64 5d09b0 uses genuine Unity fake-null so a destroyed
        // configuration is retrieved again; no permanent CLR-null-only cache.
        private static TimeCategoryLookup s_config
        {
            get
            {
                if (s_config_Internal == null)
                    s_config_Internal = SystemConfiguration.GetConfig<TimeCategoryLookup>();
                return s_config_Internal;
            }
        }

        // Original 06002dee..df6 retain enum lookup, argument/default order and the
        // supplied release's names, including the original _SDT extension suffix.
        public static void SetCategory(this TimeSetting timeSetting, TimeCategory category, float timeScale)
        { timeSetting.SetCategory(GetScriptableObject(category), timeScale); }
        public static StackableDataHandle ApplyTimeSetting_SDT(this TimeManager timeManager, TimeSettings_SDT setting)
        {
            setting.ApplyTimeCategoryObjectConversion(s_config.Dictionary);
            return timeManager.ApplyTimeSetting(setting);
        }
        public static float GetFixedDeltaTime(this TimeManager timeManager, TimeCategory category)
        { return timeManager.GetFixedDeltaTime(GetScriptableObject(category)); }
        public static float GetDeltaTime(this TimeManager timeManager, TimeCategory category)
        { return timeManager.GetDeltaTime(GetScriptableObject(category)); }
        public static float GetTotalTime(this TimeManager timeManager, TimeCategory category)
        { return timeManager.GetTotalTime(GetScriptableObject(category)); }
        public static float GetTotalFixedTime(this TimeManager timeManager, TimeCategory category)
        { return timeManager.GetTotalFixedTime(GetScriptableObject(category)); }
        public static float GetTimescale(this TimeManager timeManager, TimeCategory category)
        { return timeManager.GetTimescale(GetScriptableObject(category)); }
        public static void Subscribe(this TimeManager timeManager, ITimeScaled timeScaled,
            TimeCategory category, UpdateOn updateOn = UpdateOn.Update, string path = "")
        { timeManager.Subscribe(timeScaled, GetScriptableObject(category), updateOn, path); }
        public static void Unsubscribe(this TimeManager timeManager, ITimeScaled timeScaled,
            TimeCategory category, UpdateOn updateOn = UpdateOn.Update)
        { timeManager.Unsubscribe(timeScaled, GetScriptableObject(category), updateOn); }
        // Original 06002df7 / 5d0b44 discards TryGetValue's bool and returns its null out value.
        private static TimeCategoryObject GetScriptableObject(TimeCategory category)
        {
            s_config.Dictionary.TryGetValue(category, out TimeCategoryObject categoryObject);
            return categoryObject;
        }
    }
}
