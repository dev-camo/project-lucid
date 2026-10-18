using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime type 02000ac3. Teleports the current character to a start position
    // of the current level, once both managers are available.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterTeleportLocal : MonoBehaviour
    {
        [SerializeField]
        private LevelStartPositionDefinition m_levelStartPositionDefinition;
        [SerializeField]
        private bool m_triggerOnAwake;
        private LevelManager m_levelManager;
        private CharacterManager m_characterManager;

        // 06003e15
        private void OnEnable() => ProcessManager.GetSystemRef<LevelManager>(null, true).InvokeOnValid(OnLevelManagerValid);

        // 06003e16
        private void OnLevelManagerValid(LevelManager levelManager)
        {
            m_levelManager = levelManager;
            ProcessManager.GetSystemRef<CharacterManager>(null, true).InvokeOnValid(OnCharacterManagerValid);
        }

        // 06003e17
        private void OnCharacterManagerValid(CharacterManager manager)
        {
            m_characterManager = manager;
            if (m_triggerOnAwake)
                Action_Teleport();
        }

        // 06003e18; the start transform also becomes the character's last respawn point.
        public void Action_Teleport()
        {
            if (!m_levelManager.TryGetCurrentLevel(out LevelManagerLevel level))
                return;
            Transform start = level.Data.GetStartingPosition(m_levelStartPositionDefinition);
            Character character = m_characterManager.GetCurrentCharacterUnsafe();
            character.Teleport(start.position, start.rotation, false, true);
            character.AllowCollisions(true);
            character.Storage.SetValue<Transform>(ActorFSMKeys.LastRespawnPoint, start);
        }

        // 06003e19
        public CharacterTeleportLocal() { }
    }
}
