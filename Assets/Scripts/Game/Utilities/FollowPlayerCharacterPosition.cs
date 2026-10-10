using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000a07; genuine CharacterManager/Character remain
    // source dependencies. No stand-in player transform is supplied.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FollowPlayerCharacterPosition : TimeScaledComponent_SDT
    {
        [SerializeField] private bool m_setLocalPositionOnCancelFollow = true;
        [SerializeField]
        [ShowIf("m_setLocalPositionOnCancelFollow", null)]
        private Vector3 m_localPosition = Vector3.zero;
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();
        private Transform m_transform;
        private bool m_shouldFollow = true;

        protected override void Awake()
        {
            base.Awake();
            m_transform = transform;
        }

        // Query the real current character before testing the follow flag.
        // Native source reads the inherited Actor WorldPosition at offset 0x48.
        protected override void InternalUpdate(float deltaTime)
        {
            if (m_characterManagerRef.IsNull()) return;
            if (!m_characterManagerRef.Get().TryGetCurrentCharacter(out Character character)) return;
            if (!m_shouldFollow) return;
            m_transform.position = character.WorldPosition;
        }

        public void SetShouldFollow(bool shouldFollow)
        {
            m_shouldFollow = shouldFollow;
            if (!shouldFollow && m_setLocalPositionOnCancelFollow)
                m_transform.localPosition = m_localPosition;
        }

        // Original 0x0600398a. Serialized defaults, real registry reference and
        // follow flag initialize in original order before the genuine base.
        public FollowPlayerCharacterPosition() { }
    }
}
