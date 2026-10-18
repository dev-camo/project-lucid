using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000aca: a trigger volume that sends the current
    // character out of bounds, and hits enemies reported through ManualTriggerEnter.
    [RequireComponent(typeof(Collider))]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class OutOfBoundsArea : MonoBehaviour, ITriggerCollider
    {
        [SerializeField] private bool m_triggerInstantDeath = true;
        [SerializeField] private bool m_triggerRingLoss = true;
        private int m_enemyLayer;
        private SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();

        private void Start()
        {
            m_enemyLayer = LayerMask.NameToLayer("Enemy");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (m_characterManagerRef.IsNull())
                return;
            CharacterManager characterManager = m_characterManagerRef.Get();
            if (characterManager.IsCurrentCharacterColliderCollision(other) && characterManager.TryGetCurrentCharacter(out Character character))
                SetCharacterOutOfBounds(character);
        }

        // 06003e4f: only enemies are handled here; a character collider is ignored.
        public void ManualTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == m_enemyLayer)
                SetEnemyOutOfBounds(other);
        }

        private static void SetEnemyOutOfBounds(Collider enemyCollider)
        {
            if (enemyCollider.TryGetComponent(out Enemy enemy))
                enemy.ReceiveHit(null);
            else
                HLOutput.LogError("Game object is in the enemy layer but contains no enemy script.", enemyCollider);
        }

        private void SetCharacterOutOfBounds(Character character)
        {
            character.TriggerOutOfBounds(m_triggerInstantDeath, m_triggerRingLoss);
        }

        // Explicit so the .ctor (token 0x06003e52) sits between SetCharacterOutOfBounds and the
        // explicit interface GetComponent in metadata order, as in the original.
        public OutOfBoundsArea()
        {
        }

        T ITriggerCollider.GetComponent<T>() => GetComponent<T>();
    }
}
