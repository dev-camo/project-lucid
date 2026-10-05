using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "TimeCategoryLookup", menuName = "HardlightProject/Core Game")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class TimeCategoryLookup : SystemConfigurationAsset
    {
        [SerializeField] public SerializableDictionary<TimeCategory, TimeCategoryObject> Dictionary =
            new SerializableDictionary<TimeCategory, TimeCategoryObject>(HardlightEnumComparers.TimeCategoryComparer);
        // Complete original 020007ee; 06002de3 / 5d0558 is genuinely empty.
        public override void Validate() { }
        // 06002de4 / 5d055c preserves the original explicit generated enum comparer.
        public TimeCategoryLookup() { }
    }
}
