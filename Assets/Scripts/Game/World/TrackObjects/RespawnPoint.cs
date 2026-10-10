using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000ad1, complete nine-method declaration.
    // Real Character, CharacterManager, and DebugMenu remain required providers.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(Collider))]
    public class RespawnPoint : MonoBehaviour
    {
        // Preserve the shipped spelling and ordinary private field. The original
        // metadata has no SerializeField attribute or initializers on these fields.
        private bool disableSetOnCollsion;
        private SystemRef<LevelRespawnPoints> m_levelRespawnPoints;
        private SystemRef<CharacterManager> m_characterManagerRef;

        // 0x06003e67; ARM 0x6c3920. Publish and subscribe the respawn provider
        // before acquiring the character-manager reference; both auto-register.
        private void Awake()
        {
            m_levelRespawnPoints = ProcessManager.GetSystemRef<LevelRespawnPoints>(null, true);
            m_levelRespawnPoints.InvokeOnValid(OnLevelRespawnPointsValid);
            m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);
        }

        // 0x06003e68; ARM 0x6c3a60. The supplied provider receives this
        // component directly, without deduplication or active-state checks.
        private void OnLevelRespawnPointsValid(LevelRespawnPoints levelManager) => levelManager.Add(this);

        // 0x06003e69/0x06003e6a; ARM 0x6c3a74/0x6c3a78: genuine RET bodies.
        // Disabling does not remove the registered component from its provider.
        private void OnEnable() { }
        private void OnDisable() { }

        // 0x06003e6b; ARM 0x6c3a7c. The ref itself remains unguarded;
        // GetSafe avoids Get's diagnosis, then ordinary managed null skips Remove.
        private void OnDestroy() => m_levelRespawnPoints.GetSafe()?.Remove(this);

        // 0x06003e6c; ARM 0x6c3ad8. Get the valid manager before testing the
        // disabled flag. The real collider predicate precedes its current query.
        private void OnTriggerEnter(Collider other)
        {
            Character character = null;
            if (m_characterManagerRef.IsNull()) return;
            CharacterManager characterManager = m_characterManagerRef.Get();
            if (disableSetOnCollsion) return;
            if (!characterManager.IsCurrentCharacterColliderCollision(other)) return;
            if (characterManager.TryGetCurrentCharacter(out character)) SetRespawnPoint(character);
        }

        // 0x06003e6d; ARM 0x6c3bb4. Resolve character storage and the genuine
        // graph key before reading this transform. No character null guard is added.
        public void SetRespawnPoint(Character character) =>
            character.Storage.SetValue(ActorFSMKeys.LastRespawnPoint, transform);

        // 0x06003e6e; ARM 0x6c3ccc. Set the point, then the teleport flag.
        // The original DebugMenu reference is acquired afterward; a valid one is
        // read and discarded. There is no additional menu or camera operation.
        public void ActivateRespawn()
        {
            Character character = ProcessManager.GetSystem<CharacterManager>(null, true).GetCurrentCharacterUnsafe();
            SetRespawnPoint(character);
            character.Storage.SetValue(ActorFSMKeys.RespawnTeleportRequired, true);
            SystemRef<DebugMenu> debugMenu = ProcessManager.GetSystemRef<DebugMenu>(null, true);
            if (!debugMenu.IsNull()) debugMenu.Get();
        }

        // 0x06003e6f; ARM 0x6c3ea4. MonoBehaviour base only; refs stay default
        // until Awake. Their absence is not silently repaired on later entry points.
        public RespawnPoint() { }
    }
}
