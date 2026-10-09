using System;
using System.Collections;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Complete original Game.Runtime 0x02000802: one field and three methods.
    // The coroutine compiler's generated tokens/layout are not claimed identical.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class TimeScaledUtilities_SDT
    {
        // 0x04001e6c: original nullable cached configuration; Unity equality below.
        private static TimeCategoryLookup s_config_Internal;

        // 0x06002e67: retain lazy lookup and Unity destroyed-object semantics.
        private static TimeCategoryLookup s_config
        {
            get
            {
                if (s_config_Internal == null)
                    s_config_Internal = SystemConfiguration.GetConfig<TimeCategoryLookup>();
                return s_config_Internal;
            }
        }

        // 0x06002e68: immediate real configuration/category lookup, then real Core provider.
        public static Coroutine DelayFixedSeconds(float waitSeconds, TimeCategory timeCategory,
            Action action)
        {
            TimeCategoryObject category = s_config.Dictionary.TryGetWithDefault(timeCategory, null);
            return TimeScaledUtilities.DelayFixedSeconds(waitSeconds, category, action);
        }

        // 0x06002e69 plus original iterator 0x06002e6a..6f: one yield and deferred lookup.
        // Merely constructing this iterator does not request configuration or invoke earlyOut.
        public static IEnumerator WaitForFixedSeconds(float waitSeconds, TimeCategory timeCategory,
            Func<bool> earlyOut = null)
        {
            TimeCategoryObject category = s_config.Dictionary.TryGetWithDefault(timeCategory, null);
            yield return TimeScaledUtilities.WaitForFixedSeconds(waitSeconds, category, earlyOut);
        }
    }
}
