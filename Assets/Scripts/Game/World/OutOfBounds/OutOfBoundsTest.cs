using System.Collections;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000ace: polls the current character's position every
    // frame and marks it out of bounds when a derived test rejects the position.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public abstract class OutOfBoundsTest : MonoBehaviour
    {
        private SystemRef<CharacterManager> m_characterManagerRef;
        private Coroutine m_positionCheck;
        private CharacterManager m_characterManager;

        // 06003e58: the reference is fetched again on every enable.
        private void OnEnable()
        {
            m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>();
            m_characterManagerRef.InvokeOnValid(Init);
        }

        private void OnDisable()
        {
            if (m_positionCheck != null)
                StopCoroutine(m_positionCheck);
        }

        private void Init(CharacterManager characterManager)
        {
            m_characterManager = characterManager;
            m_positionCheck = StartCoroutine(CheckCharacterPosition());
        }

        // 06003e60: both respawn flags are read before either is tested, and neither
        // read stores a default. A frame spent respawning resumes at the loop head.
        private IEnumerator CheckCharacterPosition()
        {
            while (true)
            {
                if (m_characterManager.TryGetCurrentCharacter(out Character character) && !PositionIsValid(character.WorldPosition))
                {
                    bool teleportRequired = character.Storage.GetValue(ActorFSMKeys.RespawnTeleportRequired, false, false);
                    bool effectsComplete = character.Storage.GetValue(ActorFSMKeys.RespawnEffectsComplete, false, false);
                    if (teleportRequired || effectsComplete)
                    {
                        yield return null;
                        continue;
                    }
                    character.Storage.SetValue(ActorFSMKeys.IsOutOfBounds, true);
                }
                yield return null;
            }
        }

        protected abstract bool PositionIsValid(Vector3 position);
    }
}
