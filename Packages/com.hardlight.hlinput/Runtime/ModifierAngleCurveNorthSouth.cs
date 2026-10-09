using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [CreateAssetMenu(menuName = "Hardlight/HLInput/Modifiers/Curve Angles North South")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ModifierAngleCurveNorthSouth : InputModifier
    {
        [SerializeField] private AnimationCurve m_curve;

        public override float Modify(float originalValue, float deltaTime) { return originalValue; } // 06000141

        // 06000142: original radius is the squared XY magnitude, not its root.
        // Exact native sincos/atan2 versus installed Mathf bit parity remains held.
        public override Vector3 Modify(Vector3 originalValue, float deltaTime)
        {
            float radius = Mathf.Abs(originalValue.x * originalValue.x + originalValue.y * originalValue.y);
            float angle = Mathf.Atan2(originalValue.x, originalValue.y) * Mathf.Rad2Deg;
            angle = m_curve.Evaluate(angle) * Mathf.Deg2Rad;
            return new Vector3(radius * Mathf.Sin(angle), radius * Mathf.Cos(angle), originalValue.z);
        }

        public ModifierAngleCurveNorthSouth() { } // 06000143
    }
}
