using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

// Original HLInput.Runtime 02000002 is in the global namespace.
// Native-derived source proposal; no original runtime or numeric parity approval.
[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
[CreateAssetMenu(menuName = "Hardlight/HLInput/Modifiers/Curve")]
public class ModifierCurve : InputModifier
{
    [SerializeField] private AnimationCurve m_curve;

    // 06000001: evaluate before selecting the original sign; zero/NaN use -result.
    public override float Modify(float value, float deltaTime)
    {
        float modifiedValue = m_curve.Evaluate(Mathf.Abs(value));
        return value > 0f ? modifiedValue : -modifiedValue;
    }

    // 06000002: original XY-only magnitude and sign correction, preserving Z.
    public override Vector3 Modify(Vector3 inputValue, float deltaTime)
    {
        float magnitudeSquared = inputValue.x * inputValue.x + inputValue.y * inputValue.y;
        if (magnitudeSquared < 0.0001f) return Vector3.zero;
        float magnitude = magnitudeSquared < 1f ? Mathf.Sqrt(magnitudeSquared) : 1f;
        float scale = m_curve.Evaluate(magnitude) / magnitude;
        float x = inputValue.x * scale;
        if ((inputValue.x < 0f && x > 0f) || (inputValue.x > 0f && x < 0f)) x = -x;
        float y = inputValue.y * scale;
        if ((inputValue.y < 0f && y > 0f) || (inputValue.y > 0f && y < 0f)) y = -y;
        return new Vector3(x, y, inputValue.z);
    }

    public ModifierCurve() { } // 06000003: original base-only constructor.
}
