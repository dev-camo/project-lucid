using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020007a8: a prototype helper that turns towards the current
    // character and moves forwards each frame.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class PrototypeTransformFollowPlayer : MonoBehaviour
    {
        [SerializeField] private float m_speed;
        [SerializeField] private float m_rotateSpeed;
        private SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();

        // 06002b12: it moves along the forward it has just been given.
        private void Update()
        {
            if (!m_characterManagerRef.TryGet(out CharacterManager characterManager) || !characterManager.TryGetCurrentCharacter(out Character character))
                return;
            Vector3 toCharacter = character.transform.position - transform.position;
            float maxRadiansDelta = m_rotateSpeed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(Vector3.RotateTowards(transform.forward, toCharacter, maxRadiansDelta, 0f));
            transform.position += transform.forward * m_speed * Time.deltaTime;
        }
    }
}
