using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200080f: looks at the current character every LateUpdate,
    // following each character change once the character manager is valid.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class TransformFacePlayer : MonoBehaviour
    {
        private Transform m_lookAtTransform;
        private SystemRef<CharacterManager> m_characterManagerRef;
        private CharacterManager m_characterManager;

        private void OnEnable()
        {
            m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();
            m_characterManagerRef.InvokeOnValid(RegisterCharacterManager);
        }

        private void RegisterCharacterManager(CharacterManager characterManager)
        {
            m_characterManager = characterManager;
            m_characterManager.RegisterOnCharacterChange(SetTarget);
        }

        private void SetTarget(Character character)
        {
            m_lookAtTransform = character.transform;
        }

        private void LateUpdate()
        {
            transform.LookAt(m_lookAtTransform);
        }

        // The cached manager is not cleared.
        private void OnDisable()
        {
            if (m_characterManager != null)
                m_characterManager.ReleaseOnCharacterChange(SetTarget);
        }
    }
}
