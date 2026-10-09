using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200062d, complete one-method declaration.
    // These are public ordinary fields, with no serialization attributes.
    public class TargetedEffect : IEffectData
    {
        public Vector3 Offset;
        public Transform Target;

        // 0x06002108; ARM 0x53ef90: Object base only, both fields default.
        public TargetedEffect() { }
    }
}
