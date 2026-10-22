using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "TimeCategoryObject", menuName = "Hardlight/HLUnityCore/TimeCategoryObject")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class TimeCategoryObject : ScriptableObjectWithGuid
    {
        // Complete original fieldless type; 06000e81 / ARM64 1b1adc4.
        public TimeCategoryObject() { }
    }
}
