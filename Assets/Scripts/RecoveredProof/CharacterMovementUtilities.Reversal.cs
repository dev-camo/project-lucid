using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020009ea, fieldless static type.
    // This compiled chunk contains original methods 0x06003903/0x06003904.
    // The remaining 15 original declarations are not restored here.
    // The original class also carried ExtensionAttribute, which C# emits when
    // genuine extension methods are present. Those declarations remain outside
    // this chunk, so its class attribute population is explicitly incomplete.
    // Preserve the original ArrayBoundsChecks and NullChecks options.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public static partial class CharacterMovementUtilities
    {
        // 0x06003903. Neither reversal predicate has a method ExtensionAttribute.
        public static bool AreDirectionsReversed(Vector3 fromDirection, Vector3 toDirection)
        {
            return Vector3.Dot(fromDirection, toDirection) < 0f;
        }

        // 0x06003904. Preserve from-then-to rotation of the genuine forward vector.
        public static bool AreRotationsReversed(Quaternion fromRotation, Quaternion toRotation)
        {
            Vector3 fromDirection = fromRotation * Vector3.forward;
            Vector3 toDirection = toRotation * Vector3.forward;
            return Vector3.Dot(fromDirection, toDirection) < 0f;
        }

    }
}
